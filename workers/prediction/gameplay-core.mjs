import { createHash } from 'node:crypto';
import { performance } from 'node:perf_hooks';
import { MAX_BYTES, WireError, requireValue, parseStrict, createValidators, validateRequest, exactObject, validId, int64, unique, validateReadSet, validatePerspective, knownUsage, choiceResult, jevPayload } from './gameplay-wire.mjs';
import { validateDialogue, dialoguePayload, dialogueResult } from './gameplay-providers.mjs';

export const DEFAULT_LIMITS = Object.freeze({ dispatchPerSecond: 12, inFlight: 12, pending: 128, identities: 1024, results: 256, cacheMs: 60000, actors: 1024, revisions: 8192, activeForecasts: 32, forecasts: 256, branches: 4, depth: 3, steps: 4, horizonUs: 30000000, maxRuns: 16 });
const hash = raw => createHash('sha256').update(raw).digest('hex');
const tuple = (...parts) => JSON.stringify(parts);
const equal = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const reply = (status, body) => ({ status, body });
const safeReasons = new Set(['timeout', 'rate_limited', 'provider_error', 'invalid_provider_result', 'cancelled', 'queue_full', 'budget_exhausted', 'request_expired']);

export class GameplayBroker {
  constructor({ jev = null, dialogue = null, dialogueModel = null, ledger, validators = createValidators(), limits = {}, now = () => performance.now() }) {
    this.jev = jev; this.dialogue = dialogue; this.dialogueModel = dialogueModel;
    this.ledger = ledger; this.validators = validators; this.now = now;
    this.limits = { ...DEFAULT_LIMITS, ...limits };
    for (const [key, maximum] of Object.entries(DEFAULT_LIMITS)) requireValue(Number.isSafeInteger(this.limits[key]) && this.limits[key] > 0 && this.limits[key] <= maximum, 'invalid_configuration');
    this.sessions = new Map(); this.identities = new Map(); this.completed = [];
    this.pending = []; this.flights = new Set(); this.scopeFlights = new Map();
    this.dispatchTimes = []; this.foregroundStreak = 0; this.closed = false;
    this.timer = null;
  }
  identity(kind, r, sha) {
    const result = { schemaVersion: 1, runId: r.runId, generation: r.generation, requestId: r.requestId, requestSha256: sha };
    if (kind === 'future') Object.assign(result, { kind: 'future_step_result', forecastId: r.forecastId, branchId: r.branchId, stepSeq: r.stepSeq });
    else if (kind === 'dialogue') Object.assign(result, { kind: 'dialogue_result', actorId: r.actorId, dialogueSeq: r.dialogueSeq, channel: r.channel, authority: 'text_only' });
    else Object.assign(result, { kind: 'decision_result', actorId: r.actorId, decisionSeq: r.decisionSeq });
    return result;
  }
  unavailable(kind, r, sha, reason, usage = null, latency = null) {
    return { ...this.identity(kind, r, sha), status: 'unavailable', provider: kind === 'dialogue' ? 'openai-compatible' : 'typesafe-direct', model: null,
      ...(kind === 'dialogue' ? { text: null, factRefs: [] } : { selectedCandidateId: null, probabilities: null, confidence: null }),
      usage, upstreamLatencyMs: latency, reasonCode: reason };
  }
  session(r) {
    const session = this.sessions.get(r.runId);
    requireValue(session && session.generation === r.generation, 'request_expired', 410);
    return session;
  }
  validateSession(r) {
    exactObject(r, ['schemaVersion', 'runId', 'generation', 'mode', 'tickUs', 'actors', 'revisions']);
    requireValue(r.schemaVersion === 1 && validId(r.runId) && Number.isInteger(r.generation) && r.generation >= 0 && r.generation <= 2147483647);
    requireValue(['TUTORIAL', 'RANDOM_OPERATIONS_LAB'].includes(r.mode)); int64(r.tickUs);
    requireValue(Array.isArray(r.actors) && r.actors.length <= this.limits.actors);
    unique(r.actors, 'actorId');
    for (const actor of r.actors) {
      exactObject(actor, ['actorId', 'roleId', 'profileRevision', 'observationRevision']);
      requireValue(validId(actor.actorId) && validId(actor.roleId)); int64(actor.profileRevision); int64(actor.observationRevision);
    }
    requireValue(Array.isArray(r.revisions));
    if (r.revisions.length) validateReadSet(r.revisions, this.limits.revisions);
  }
  register(r) {
    this.validateSession(r);
    requireValue(this.sessions.has(r.runId) || this.sessions.size < this.limits.maxRuns, 'queue_full', 503);
    this.ledger.register(r.runId, r.generation);
    const old = this.sessions.get(r.runId);
    if (old) this.cancelWhere(entry => entry.r.runId === r.runId, 'request_expired');
    const session = { generation: r.generation, mode: r.mode, tickUs: r.tickUs, actors: new Map(), revisions: new Map(), forecasts: new Map() };
    this.sessions.set(r.runId, session);
    this.updateRegistry(session, r);
    return { status: 'registered', runId: r.runId, generation: r.generation };
  }
  updateRegistry(session, r) {
    const actorIds = new Set([...session.actors.keys(), ...r.actors.map(actor => actor.actorId)]);
    const revisionIds = new Set([...session.revisions.keys(), ...r.revisions.map(item => item.entityId)]);
    requireValue(actorIds.size <= this.limits.actors && revisionIds.size <= this.limits.revisions, 'queue_full', 503);
    for (const actor of r.actors) {
      const previous = session.actors.get(actor.actorId);
      if (previous) {
        requireValue(int64(actor.profileRevision) >= int64(previous.profileRevision) && int64(actor.observationRevision) >= int64(previous.observationRevision));
        requireValue(actor.roleId === previous.roleId || int64(actor.profileRevision) > int64(previous.profileRevision));
      }
    }
    for (const item of r.revisions) requireValue(!session.revisions.has(item.entityId) || int64(item.revision) >= int64(session.revisions.get(item.entityId)));
    session.tickUs = r.tickUs;
    for (const actor of r.actors) {
      const previous = session.actors.get(actor.actorId);
      session.actors.set(actor.actorId, { ...actor, npc: previous?.npc ?? { highwater: -1n }, dialogue: previous?.dialogue ?? { highwater: -1n } });
    }
    for (const item of r.revisions) session.revisions.set(item.entityId, item.revision);
  }
  update(r) {
    this.validateSession(r);
    const session = this.session(r);
    requireValue(session.mode === r.mode && int64(r.tickUs) >= int64(session.tickUs));
    this.updateRegistry(session, r);
    for (const [id, forecast] of session.forecasts) {
      if (!forecast.closed && (!this.freshReadSet(session, forecast.readSet) || !this.freshForecastActors(session, forecast) || int64(session.tickUs) > int64(forecast.horizonEndTickUs))) this.closeForecast(session, id);
    }
    this.cancelWhere(entry => entry.r.runId === r.runId && !this.isFresh(entry), 'request_expired');
    this.pump();
    return { status: 'updated', runId: r.runId, generation: r.generation };
  }
  freshReadSet(session, readSet) { return readSet.every(item => session.revisions.get(item.entityId) === item.revision); }
  freshForecastActors(session, forecast) {
    return forecast.actorBasis.every(basis => {
      const actor = session.actors.get(basis.actorId);
      return actor && ['roleId', 'profileRevision', 'observationRevision'].every(key => actor[key] === basis[key]);
    });
  }
  registerForecast(r) {
    r = structuredClone(r);
    exactObject(r, ['schemaVersion', 'runId', 'generation', 'forecastId', 'basisSnapshotId', 'basisTickUs', 'horizonEndTickUs', 'rulesetId', 'rulesetRevision', 'readSet', 'branches']);
    requireValue(r.schemaVersion === 1 && ['runId', 'forecastId', 'basisSnapshotId', 'rulesetId'].every(key => validId(r[key])));
    int64(r.rulesetRevision); validateReadSet(r.readSet);
    const session = this.session(r), basis = int64(r.basisTickUs), horizon = int64(r.horizonEndTickUs);
    requireValue(basis <= int64(session.tickUs) && horizon >= basis && horizon - basis <= BigInt(this.limits.horizonUs));
    requireValue(this.freshReadSet(session, r.readSet), 'request_expired', 410);
    requireValue(Array.isArray(r.branches) && r.branches.length > 0 && r.branches.length <= this.limits.branches);
    unique(r.branches, 'branchId');
    for (const branch of r.branches) {
      exactObject(branch, ['branchId', 'perspective']); requireValue(validId(branch.branchId)); validatePerspective(branch.perspective);
      if (branch.perspective.kind === 'actor') requireValue(session.actors.has(branch.perspective.actorId), 'actor_not_registered', 403);
    }
    const previous = session.forecasts.get(r.forecastId);
    const fingerprint = hash(Buffer.from(JSON.stringify(r)));
    if (previous) {
      requireValue(previous.fingerprint === fingerprint, 'identity_conflict', 409);
      requireValue(!previous.closed, 'request_expired', 410);
      return { status: 'registered', forecastId: r.forecastId };
    }
    requireValue(session.forecasts.size < this.limits.forecasts && [...session.forecasts.values()].filter(f => !f.closed).length < this.limits.activeForecasts, 'queue_full', 503);
    const actorBasis = r.branches.filter(branch => branch.perspective.kind === 'actor').map(branch => {
      const actor = session.actors.get(branch.perspective.actorId);
      return { actorId: actor.actorId, roleId: actor.roleId, profileRevision: actor.profileRevision, observationRevision: actor.observationRevision };
    });
    session.forecasts.set(r.forecastId, { ...r, fingerprint, actorBasis, closed: false, branches: new Map(r.branches.map(branch => [branch.branchId, { ...branch, highwater: -1n, steps: 0 }])) });
    return { status: 'registered', forecastId: r.forecastId };
  }
  closeForecast(session, id) {
    const forecast = session.forecasts.get(id);
    if (!forecast || forecast.closed) return;
    forecast.closed = true;
    this.cancelWhere(entry => entry.kind === 'future' && this.sessions.get(entry.r.runId) === session && entry.r.forecastId === id, 'request_expired');
    // Retain fingerprint and branch high-water marks until generation ends, but drop snapshots.
    forecast.readSet = []; forecast.branches.forEach(branch => { delete branch.perspective; });
  }
  closeFuture(r) {
    exactObject(r, ['schemaVersion', 'runId', 'generation', 'forecastId']);
    requireValue(r.schemaVersion === 1 && validId(r.forecastId));
    const session = this.session(r);
    requireValue(session.forecasts.has(r.forecastId), 'request_expired', 410);
    this.closeForecast(session, r.forecastId);
    return { status: 'closed', forecastId: r.forecastId };
  }
  scope(kind, r, session) {
    if (kind === 'future') {
      const forecast = session.forecasts.get(r.forecastId);
      requireValue(forecast && !forecast.closed, 'request_expired', 410);
      const branch = forecast.branches.get(r.branchId);
      requireValue(branch, 'branch_not_registered', 403);
      return { state: branch, key: tuple(r.runId, r.generation, 'future', r.forecastId, r.branchId), seq: int64(r.stepSeq) };
    }
    const actor = session.actors.get(r.actorId);
    requireValue(actor, 'actor_not_registered', 403);
    return { state: actor[kind], key: tuple(r.runId, r.generation, kind, r.actorId), seq: int64(kind === 'dialogue' ? r.dialogueSeq : r.decisionSeq) };
  }
  isFresh(entry) {
    const { kind, r } = entry, session = this.sessions.get(r.runId);
    if (!session || session.generation !== r.generation || session.mode !== r.mode || int64(session.tickUs) < int64(r.basisTickUs) || int64(session.tickUs) >= int64(r.expiresAtTickUs)) return false;
    if (kind === 'future') {
      const forecast = session.forecasts.get(r.forecastId), branch = forecast?.branches.get(r.branchId);
      return !!forecast && !forecast.closed && !!branch && branch.perspective.kind === r.perspective.kind && branch.perspective.actorId === r.perspective.actorId
        && (r.perspective.kind === 'world' || int64(r.perspective.knowledgeRevision) >= int64(branch.perspective.knowledgeRevision))
        && ['basisSnapshotId', 'basisTickUs', 'horizonEndTickUs', 'rulesetId', 'rulesetRevision'].every(key => forecast[key] === r[key])
        && equal(forecast.readSet, r.readSet) && this.freshReadSet(session, r.readSet) && this.freshForecastActors(session, forecast);
    }
    const actor = session.actors.get(r.actorId), profile = kind === 'dialogue' ? r : r.actor;
    return !!actor && actor.roleId === profile.roleId && actor.profileRevision === profile.profileRevision && actor.observationRevision === r.observationRevision
      && (kind === 'dialogue' || (this.freshReadSet(session, r.readSet)
        && r.observations.every(fact => fact.validUntilTickUs === null || int64(session.tickUs) < int64(fact.validUntilTickUs))));
  }
  prune() {
    const now = this.now();
    for (const entry of this.completed) if (entry.result && now - entry.finishedAt >= this.limits.cacheMs) entry.result = null;
    let results = this.completed.filter(entry => entry.result).length;
    for (const entry of this.completed) if (results > this.limits.results && entry.result) { entry.result = null; results--; }
    while (this.completed.length > this.limits.identities) {
      const entry = this.completed.shift(); this.identities.delete(entry.key);
    }
  }
  async submit(kind, raw) {
    requireValue(!this.closed, 'provider_error', 503);
    const r = parseStrict(raw);
    if (kind === 'dialogue') validateDialogue(r); else validateRequest(kind, r, this.validators, this.limits);
    const sha = hash(raw), session = this.session(r);
    this.prune();
    const key = kind === 'future' ? tuple(r.runId, r.generation, kind, r.forecastId, r.branchId, r.stepSeq)
      : tuple(r.runId, r.generation, kind, r.actorId, kind === 'npc' ? r.decisionSeq : r.dialogueSeq);
    const previous = this.identities.get(key);
    if (previous) {
      if (previous.sha !== sha || previous.r.requestId !== r.requestId) return reply(409, this.unavailable(kind, r, sha, 'request_expired'));
      if (!this.isFresh(previous)) return reply(410, this.unavailable(kind, r, sha, 'request_expired', previous.usage));
      if (!previous.finished) return previous.promise;
      return previous.result ?? reply(410, this.unavailable(kind, r, sha, 'request_expired', previous.usage));
    }
    let scope;
    try { scope = this.scope(kind, r, session); }
    catch (error) {
      if (error.status === 410) return reply(410, this.unavailable(kind, r, sha, 'request_expired'));
      throw error;
    }
    if (scope.seq <= scope.state.highwater) return reply(410, this.unavailable(kind, r, sha, 'request_expired'));
    requireValue(this.pending.length < this.limits.pending || this.pending.some(entry => entry.scope.key === scope.key), 'queue_full', 503);
    if (kind === 'future') {
      if (scope.state.lastDepth !== undefined) {
        requireValue(r.depth > scope.state.lastDepth && int64(r.stepTickUs) >= scope.state.lastStepTick);
        if (r.perspective.kind === 'actor') requireValue(int64(r.perspective.knowledgeRevision) >= scope.state.lastKnowledge);
      }
      const forecastPending = this.pending.filter(entry => entry.kind === 'future' && entry.scope.key !== scope.key).length;
      requireValue(forecastPending < Math.max(1, Math.floor(this.limits.pending * 3 / 4)), 'queue_full', 503);
    }
    // Freeze parsed input by ownership: callers never receive these object references.
    let resolve;
    const promise = new Promise(done => { resolve = done; });
    const entry = { kind, r, sha, key, scope, promise, resolve, queuedAt: this.now(), dueAt: this.now() + r.deadlineMs, finished: false, usage: null };
    scope.state.highwater = scope.seq;
    this.identities.set(key, entry);
    this.cancelWhere(other => other.scope.key === scope.key && !other.finished, 'cancelled');
    if (!this.isFresh(entry)) { this.finish(entry, 410, 'request_expired'); return promise; }
    if (kind === 'future' && scope.state.steps >= this.limits.steps) { this.finish(entry, 503, 'budget_exhausted'); return promise; }
    if (kind === 'npc') {
      const fingerprint = hash(Buffer.from(JSON.stringify({ actor: r.actor, observationRevision: r.observationRevision, observations: r.observations, memories: r.memories, candidates: r.candidates, readSet: r.readSet })));
      if (scope.state.fingerprint === fingerprint) { this.finish(entry, 410, 'request_expired'); return promise; }
      scope.state.fingerprint = fingerprint;
    }
    if (kind === 'dialogue' && r.channel === 'truth_slots') {
      const body = { ...this.identity(kind, r, sha), status: 'ok', provider: 'local-truth-slots', model: null, text: r.truthSlots.map(slot => slot.text).join('\n'), factRefs: r.truthSlots.map(slot => slot.factId), usage: null, upstreamLatencyMs: null, reasonCode: 'truth_slots' };
      this.complete(entry, reply(200, body)); return promise;
    }
    if (!(kind === 'dialogue' ? this.dialogue : this.jev)) { this.finish(entry, 503, 'provider_error'); return promise; }
    this.pending.push(entry); this.pump();
    return promise;
  }
  complete(entry, result) {
    if (entry.finished) return;
    entry.finished = true; entry.finishedAt = this.now(); entry.result = result;
    entry.resolve(result); this.completed.push(entry); this.prune();
  }
  finish(entry, status, reason) {
    const latency = entry.startedAt === undefined ? null : Math.min(60000, Math.max(0, Math.round(this.now() - entry.startedAt)));
    this.complete(entry, reply(status, this.unavailable(entry.kind, entry.r, entry.sha, reason, entry.usage, latency)));
  }
  cancelWhere(predicate, reason) {
    for (const entry of this.pending) if (!entry.finished && predicate(entry)) this.finish(entry, 410, reason);
    for (const entry of this.flights) if (!entry.finished && predicate(entry)) this.finish(entry, 410, reason);
    this.pending = this.pending.filter(entry => !entry.finished);
    // Do not release upstream slots early: known late usage must still be collected.
  }
  select() {
    const available = this.pending.filter(entry => !this.scopeFlights.has(entry.scope.key));
    const live = available.find(entry => entry.kind !== 'future');
    const future = available.find(entry => entry.kind === 'future');
    if (!live) { this.foregroundStreak = 0; return future; }
    if (!future) { this.foregroundStreak++; return live; }
    // FIFO within each class; live 3:1 forecast minimum service with aging override.
    if (this.foregroundStreak >= 3 || (this.foregroundStreak > 0 && this.now() - future.queuedAt >= 1000)) { this.foregroundStreak = 0; return future; }
    this.foregroundStreak++; return live;
  }
  pump() {
    if (this.closed) return;
    clearTimeout(this.timer); this.timer = null;
    const now = this.now();
    for (const entry of this.pending) if (!entry.finished && (now >= entry.dueAt || !this.isFresh(entry))) this.finish(entry, now >= entry.dueAt ? 504 : 410, now >= entry.dueAt ? 'timeout' : 'request_expired');
    for (const entry of this.flights) if (!entry.finished && now >= entry.dueAt) this.finish(entry, 504, 'timeout');
    this.pending = this.pending.filter(entry => !entry.finished);
    this.dispatchTimes = this.dispatchTimes.filter(time => now - time < 1000);
    while (this.pending.length && this.flights.size < this.limits.inFlight && this.dispatchTimes.length < this.limits.dispatchPerSecond) {
      const entry = this.select(); if (!entry) break;
      this.pending.splice(this.pending.indexOf(entry), 1);
      this.dispatch(entry);
    }
    if (this.pending.length || this.flights.size) {
      const deadlines = [...this.pending, ...this.flights].filter(entry => !entry.finished).map(entry => entry.dueAt);
      const nextDispatch = this.dispatchTimes.length >= this.limits.dispatchPerSecond ? this.dispatchTimes[0] + 1000 : now + 20;
      this.timer = setTimeout(() => this.pump(), Math.max(1, Math.min(100, nextDispatch - now, ...deadlines.map(time => time - now))));
      this.timer.unref?.();
    }
  }
  dispatch(entry) {
    const providerKind = entry.kind === 'dialogue' ? 'dialogue' : 'jev';
    const payload = providerKind === 'dialogue' ? dialoguePayload(entry.r, this.dialogueModel) : jevPayload(entry.kind, entry.r);
    if (Buffer.byteLength(JSON.stringify(payload)) > MAX_BYTES) { this.finish(entry, 422, 'provider_error'); return; }
    try { entry.ticket = this.ledger.reserve(providerKind); }
    catch { this.finish(entry, 503, 'budget_exhausted'); return; }
    entry.startedAt = this.now(); entry.controller = new AbortController();
    this.dispatchTimes.push(entry.startedAt); this.flights.add(entry); this.scopeFlights.set(entry.scope.key, entry);
    if (entry.kind === 'future') {
      entry.scope.state.steps++;
      entry.scope.state.lastDepth = entry.r.depth;
      entry.scope.state.lastStepTick = int64(entry.r.stepTickUs);
      if (entry.r.perspective.kind === 'actor') entry.scope.state.lastKnowledge = int64(entry.r.perspective.knowledgeRevision);
    }
    const provider = providerKind === 'dialogue' ? this.dialogue : this.jev;
    // Keep transport alive for bounded late billing (15s); caller deadline is independent.
    Promise.resolve().then(() => provider.request(payload, { signal: entry.controller.signal, timeoutMs: 15000 })).then(
      result => this.received(entry, result, null),
      error => this.received(entry, error?.providerResult, error)
    ).catch(() => { if (!entry.finished) this.finish(entry, 503, 'budget_exhausted'); }).finally(() => {
      this.flights.delete(entry);
      if (this.scopeFlights.get(entry.scope.key) === entry) this.scopeFlights.delete(entry.scope.key);
      this.pump();
    });
  }
  received(entry, raw, error) {
    entry.usage = knownUsage(raw, entry.kind === 'dialogue');
    // Account before cancellation/freshness checks, even for invalid choice and old generations.
    this.ledger.account(entry.ticket, entry.usage);
    if (entry.finished) return;
    if (this.now() >= entry.dueAt) { this.finish(entry, 504, 'timeout'); return; }
    if (!this.isFresh(entry)) { this.finish(entry, 410, 'request_expired'); return; }
    if (error) { const reason = safeReasons.has(error.code) ? error.code : 'provider_error'; this.finish(entry, reason === 'rate_limited' ? 429 : reason === 'timeout' ? 504 : 502, reason); return; }
    try {
      const selection = entry.kind === 'dialogue' ? dialogueResult(raw, this.dialogueModel) : choiceResult(raw, entry.r.candidates);
      requireValue(entry.usage, 'invalid_provider_result', 502);
      const body = { ...this.identity(entry.kind, entry.r, entry.sha), status: 'ok', provider: entry.kind === 'dialogue' ? 'openai-compatible' : 'typesafe-direct', ...selection,
        ...(entry.kind === 'dialogue' ? { factRefs: [] } : {}), usage: entry.usage,
        upstreamLatencyMs: Math.max(0, Math.round(this.now() - entry.startedAt)), reasonCode: entry.kind === 'dialogue' ? 'generated' : 'selected' };
      if (entry.kind !== 'dialogue') requireValue(this.validators[`${entry.kind}Result`](body), 'invalid_provider_result', 502);
      this.complete(entry, reply(200, body));
    } catch { this.finish(entry, 502, 'invalid_provider_result'); }
  }
  status() {
    return { status: this.closed ? 'closed' : 'ready', model: 'jev-1.13.0', jevConfigured: !!this.jev, dialogueConfigured: !!this.dialogue, retries: 0,
      pending: this.pending.length, inFlight: this.flights.size, identities: this.identities.size, runs: this.sessions.size,
      limits: this.limits, accounting: this.ledger.status(),
      oldestLiveWaitMs: this.age(false), oldestForecastWaitMs: this.age(true) };
  }
  age(future) { const entry = this.pending.find(entry => (entry.kind === 'future') === future); return entry ? Math.max(0, Math.round(this.now() - entry.queuedAt)) : 0; }
  requestControl(r, cancel = false) {
    exactObject(r, ['schemaVersion', 'runId', 'generation', 'requestSha256']);
    requireValue(r.schemaVersion === 1 && validId(r.runId) && typeof r.requestSha256 === 'string' && /^[a-f0-9]{64}$/.test(r.requestSha256));
    const entry = [...this.identities.values()].find(item => item.r.runId === r.runId && item.r.generation === r.generation && item.sha === r.requestSha256);
    requireValue(entry, 'request_expired', 410);
    if (cancel) this.cancelWhere(item => item === entry, 'cancelled');
    return { status: cancel ? 'cancelled' : entry.usage ? 'known' : 'unknown', requestSha256: entry.sha, usage: entry.usage, finished: entry.finished };
  }
  async route(path, raw) {
    const kind = { '/npc/decision': 'npc', '/future/step': 'future', '/dialogue/generate': 'dialogue' }[path];
    if (kind) {
      try { return await this.submit(kind, raw); }
      catch (error) {
        if (safeReasons.has(error.code)) {
          const r = parseStrict(raw); return reply(error.status, this.unavailable(kind, r, hash(raw), error.code));
        }
        throw error;
      }
    }
    const r = parseStrict(raw);
    if (path === '/request/cancel' || path === '/request/usage') return reply(200, this.requestControl(r, path === '/request/cancel'));
    const handler = { '/session/register': 'register', '/session/update': 'update', '/future/register': 'registerForecast', '/future/close': 'closeFuture' }[path];
    requireValue(handler, 'not_found', 404);
    return reply(200, this[handler](r));
  }
  async close() {
    this.closed = true; clearTimeout(this.timer);
    this.cancelWhere(() => true, 'cancelled');
    for (const entry of this.flights) entry.controller.abort();
    this.jev?.close?.(); this.dialogue?.close?.();
  }
}
