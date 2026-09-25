import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { GameplayBroker, DEFAULT_LIMITS } from './gameplay-core.mjs';
import { AdmissionLedger, nanoUsd, openLedger, emptyLedger } from './gameplay-ledger.mjs';
import { createGameplayServer, configuration } from './gameplay-broker.mjs';
import { parseStrict, createValidators, validateRequest, choiceResult, WireError } from './gameplay-wire.mjs';
import { createJsonTransport } from './gameplay-providers.mjs';
import http from 'node:http';

const directory = new URL('../../docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/contracts/', import.meta.url);
const examples = Object.fromEntries(['npc-decision', 'future-step'].map(name => [name, JSON.parse(readFileSync(new URL(`${name}.examples.json`, directory), 'utf8'))]));
const validators = createValidators();
const npc = () => structuredClone(examples['npc-decision'].shapeCases[0].payload);
const future = () => structuredClone(examples['future-step'].shapeCases[0].payload);
const worldFuture = () => structuredClone(examples['future-step'].shapeCases.find(item => item.id === 'shape-world-next').payload);
const bytes = object => Buffer.from(JSON.stringify(object));
const sha = raw => createHash('sha256').update(raw).digest('hex');
const flush = () => new Promise(resolve => setImmediate(resolve));
function answer(payload, usage = { input_tokens: 101, output_tokens: 17 }) {
  const ids = Object.keys(payload.questions.selection.criteria);
  return { model: 'jev-1.13.0', answers: { selection: { type: 'choice', choice: ids[0], confidence: 0.5, probabilities: Object.fromEntries(ids.map((id, index) => [id, index === 0 ? 0.75 : 0.25 / (ids.length - 1)])) } }, usage };
}
function provider() {
  const calls = [];
  return { calls, request(payload, options) {
    let resolve, reject;
    const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
    calls.push({ payload, resolve, reject });
    options.signal.addEventListener('abort', () => reject(new WireError(503, 'cancelled')), { once: true });
    return promise;
  }, resolve(index = calls.length - 1) { calls[index].resolve(answer(calls[index].payload)); } };
}
function harness(t, options = {}) {
  let now = 0;
  const upstream = options.jev ?? provider();
  const ledger = options.ledger ?? new AdmissionLedger({ maxRequests: 1000, maxSpendNanoUsd: nanoUsd('100'), prices: { jev: { input: nanoUsd('0.042'), output: 0n }, dialogue: { input: nanoUsd('1'), output: nanoUsd('2') } } });
  const broker = new GameplayBroker({ jev: upstream, ledger, validators, now: () => now, ...options });
  t.after(() => broker.close());
  return { broker, upstream, ledger, advance(ms) { now += ms; broker.pump(); } };
}
function register(broker, requests) {
  const first = requests[0], actors = new Map(), revisions = new Map();
  for (const r of requests) {
    const actorId = r.actorId ?? r.perspective?.actorId;
    if (actorId) actors.set(actorId, { actorId, roleId: r.actor?.roleId ?? 'citizen', profileRevision: r.actor?.profileRevision ?? '1', observationRevision: r.observationRevision ?? r.perspective.knowledgeRevision });
    for (const revision of r.readSet) revisions.set(revision.entityId, structuredClone(revision));
  }
  const registration = { schemaVersion: 1, runId: first.runId, generation: first.generation, mode: first.mode, tickUs: first.basisTickUs, actors: [...actors.values()], revisions: [...revisions.values()] };
  broker.register(registration); return registration;
}
function registerFuture(broker, r) {
  return broker.registerForecast({ schemaVersion: 1, runId: r.runId, generation: r.generation, forecastId: r.forecastId, basisSnapshotId: r.basisSnapshotId, basisTickUs: r.basisTickUs, horizonEndTickUs: r.horizonEndTickUs, rulesetId: r.rulesetId, rulesetRevision: r.rulesetRevision, readSet: r.readSet, branches: [{ branchId: r.branchId, perspective: r.perspective }] });
}
function actorRequest(index) {
  const r = npc(); r.actorId = `staff-${index}`; r.readSet[0].entityId = r.actorId; return r;
}

test('approved schema examples and raw hashes remain compatible', () => {
  for (const [name, kind] of [['npc-decision', 'npc'], ['future-step', 'future']]) {
    for (const item of examples[name].shapeCases) assert.equal(validators[kind](item.payload), item.valid, `${name}:${item.id}`);
    const raw = Buffer.from(examples[name].rawRequestUtf8);
    validateRequest(kind, parseStrict(raw), validators, DEFAULT_LIMITS);
    const success = examples[name].shapeCases.find(item => item.id === 'shape-success').payload;
    assert.equal(sha(raw), success.requestSha256);
  }
});

test('lexical boundary rejects decoded duplicates, overflow, malformed UTF8 and depth', () => {
  const bad = ['{"x":1,"\\u0078":2}', '{"x":"\\ud800"}', '{"x":"\\udc00"}', '{"x":1e999}', '{"x":NaN}', '{"x":Infinity}', '{}true', '\ufeff{}', '[01]', '[1,]', '["\\q"]', '['.repeat(17) + '0' + ']'.repeat(17)];
  for (const raw of bad) assert.throws(() => parseStrict(Buffer.from(raw)), error => error.status === 400, raw);
  assert.throws(() => parseStrict(Buffer.from([0xc0, 0xaf])), error => error.status === 400);
  assert.throws(() => parseStrict(Buffer.alloc(65537, 32)), error => error.status === 413);
  assert.equal(parseStrict(Buffer.from('{"x":"\\ud840\\udc00"}')).x, '\u{20000}');
  assert.deepEqual([...parseStrict(Buffer.from('[[0]]'))[0]], [0]);
});

test('semantic validation rejects all inconsistent times, refs, owners and identities', () => {
  const npcChanges = [r => { r.decisionSeq = '9223372036854775808'; }, r => { r.observations[0].observedTickUs = '1000001'; }, r => { r.observations[1].validUntilTickUs = r.basisTickUs; }, r => { r.memories[0].rememberedTickUs = '1000001'; }, r => { r.candidates[1].candidateId = r.candidates[0].candidateId; }, r => { r.readSet[1].entityId = r.readSet[0].entityId; }, r => { r.expiresAtTickUs = r.basisTickUs; }];
  const futureChanges = [r => { r.perspective.knowledgeRevision = '9223372036854775808'; }, r => { r.stepTickUs = '999999'; }, r => { r.stepTickUs = '31000001'; }, r => { r.horizonEndTickUs = '31000001'; }, r => { r.depth = 4; }, r => { r.candidates[0].actorId = 'another-actor'; }, r => { r.candidates[0].causeFactIds = ['missing']; }, r => { r.candidates[0].bindings[1].slot = r.candidates[0].bindings[0].slot; }, r => { r.stateFacts[0].origin = 'actual'; }, r => { r.stateFacts[0].observedTickUs = '2000001'; }];
  for (const [kind, factory, mutations] of [['npc', npc, npcChanges], ['future', future, futureChanges]]) {
    for (const mutate of mutations) { const r = factory(); mutate(r); assert.throws(() => validateRequest(kind, r, validators, DEFAULT_LIMITS), error => error.status === 422); }
  }
});

test('NPC admission rejects a missing deciding actor rather than treating target reads as sufficient', async t => {
  const { broker } = harness(t, { jev: null });
  const r = npc(); register(broker, [r]);
  r.readSet = r.readSet.filter(read => read.entityId !== r.actorId);
  await assert.rejects(async () => broker.route('/npc/decision', bytes(r)), error => error.status === 422);
  assert.equal(broker.status().accounting.requests, 0);
});

test('world effect plus explicit no-effect hold reaches offline provider admission', async t => {
  const { broker } = harness(t, { jev: null });
  const r = worldFuture(); register(broker, [r]); registerFuture(broker, r);
  const result = await broker.route('/future/step', bytes(r));
  assert.equal(result.status, 503);
  assert.equal(result.body.reasonCode, 'provider_error');
  assert.equal(result.body.selectedCandidateId, null);
  assert.equal(result.body.model, null);
  assert.equal(result.body.requestSha256, sha(bytes(r)));
  assert.equal(broker.status().accounting.requests, 0);
});

test('empty causes are confined to the exact world hold with a shared read-set anchor', () => {
  const mutations = [
    (r, hold) => { hold.candidateId = 'renamed-hold'; },
    (r, hold) => { hold.transitionId = 'unregistered-effect'; },
    (r, hold) => { hold.parameterSetId = 'with-effects'; },
    (r, hold) => { hold.actorId = 'citizen-7'; r.perspective = { kind: 'actor', actorId: hold.actorId, knowledgeRevision: '1' }; r.purpose = 'counterfactual'; },
    (r, hold) => { hold.causeFactIds = [r.stateFacts[0].factId]; },
    (r, hold) => { hold.bindings[1].entityId = 'other-target'; r.readSet.push({ entityId: 'other-target', revision: '0' }); },
    (r, hold) => { hold.bindings.forEach(binding => { binding.entityId = 'unread-anchor'; }); },
    (r, hold) => { hold.bindings.pop(); },
    (r, hold) => { hold.bindings.push({ slot: 'tool', entityId: 'station-exterior' }); },
    (r, hold) => { hold.bindings[1].slot = 'recipient'; },
    r => { r.candidates[0].causeFactIds = []; }
  ];
  for (const mutate of mutations) {
    const r = worldFuture(); const hold = r.candidates.find(candidate => candidate.candidateId === 'env-hold');
    mutate(r, hold);
    assert.throws(() => validateRequest('future', r, validators, DEFAULT_LIMITS), error => error.status === 422);
  }
});

test('exact raw dedup coalesces flight and retains conflict after result eviction', async t => {
  const { broker, upstream, ledger } = harness(t, { limits: { results: 1, identities: 2 } });
  const a = actorRequest(1), b = actorRequest(2), c = actorRequest(3); register(broker, [a, b, c]);
  const raw = bytes(a), first = broker.submit('npc', raw), duplicate = broker.submit('npc', raw);
  await flush(); assert.equal(upstream.calls.length, 1);
  assert.equal((await broker.submit('npc', Buffer.concat([raw, Buffer.from(' ')]))).status, 409);
  upstream.resolve(); const result = await first; assert.equal((await duplicate).body.requestSha256, result.body.requestSha256);
  for (const r of [b, c]) { const pending = broker.submit('npc', bytes(r)); await flush(); upstream.resolve(); await pending; }
  assert.equal((await broker.submit('npc', bytes(b))).status, 410);
  b.requestId = 'different'; assert.equal((await broker.submit('npc', bytes(b))).status, 409);
  assert.equal((await broker.submit('npc', raw)).status, 410);
  a.requestId = 'new-old'; assert.equal((await broker.submit('npc', bytes(a))).status, 410);
  assert.equal(ledger.status().requests, 3);
});

test('late usage survives generation fence exactly once; never frees newer request', async t => {
  const { broker, upstream, ledger, advance } = harness(t);
  const a = npc(); const registration = register(broker, [a]);
  const first = broker.submit('npc', bytes(a)); await flush(); advance(2001);
  assert.equal((await first).body.reasonCode, 'timeout');
  assert.equal(ledger.status().unknownRequests, 1);
  registration.generation++; broker.register(registration);
  const newer = { ...a, generation: registration.generation };
  const second = broker.submit('npc', bytes(newer)); await flush();
  upstream.resolve(0); await flush();
  assert.equal(ledger.status().knownInputTokens, '101');
  assert.equal(broker.status().inFlight, 1);
  assert.equal((await broker.route('/npc/decision', bytes(a))).status, 410);
  upstream.resolve(1); assert.equal((await second).body.status, 'ok');
  await broker.submit('npc', bytes(newer)); assert.equal(ledger.status().knownInputTokens, '202');
});

test('actor coalescing keeps one transport slot and latest queued observation', async t => {
  const { broker, upstream } = harness(t);
  const a = npc(); const registration = register(broker, [a]);
  const first = broker.submit('npc', bytes(a)); await flush();
  const next = { ...a, decisionSeq: '18', requestId: 'new-request', observationRevision: '10' };
  registration.actors[0].observationRevision = '10'; broker.update(registration);
  const second = broker.submit('npc', bytes(next)); await flush();
  assert.equal((await first).body.status, 'unavailable'); assert.equal(upstream.calls.length, 1);
  upstream.resolve(0); await flush(); assert.equal(upstream.calls.length, 2);
  upstream.resolve(1); assert.equal((await second).body.status, 'ok');
});

test('forecast isolation keeps live sequence and actor-private provider snapshots separate', async t => {
  const { broker, upstream } = harness(t);
  const live = npc(), imagined = future();
  imagined.runId = live.runId; imagined.generation = live.generation; imagined.perspective.actorId = live.actorId;
  imagined.candidates.forEach(candidate => { candidate.actorId = live.actorId; }); imagined.readSet[0] = structuredClone(live.readSet[0]);
  const registry = register(broker, [imagined, live]); registerFuture(broker, imagined);
  const futurePending = broker.submit('future', bytes(imagined)); await flush();
  assert.equal(broker.sessions.get(live.runId).actors.get(live.actorId).npc.highwater, -1n);
  const livePending = broker.submit('npc', bytes(live)); await flush();
  assert.equal(upstream.calls[0].payload.state.perspective.actorId, live.actorId);
  assert.equal(Object.hasOwn(upstream.calls[0].payload.state, 'actor'), false);
  assert.equal(Object.hasOwn(upstream.calls[1].payload.state, 'stateFacts'), false);
  assert.equal(upstream.calls[1].payload.state.memories[0].memoryId, live.memories[0].memoryId);
  registry.revisions.find(item => item.entityId === 'portal-a').revision = '4'; broker.update(registry);
  assert.equal((await futurePending).body.reasonCode, 'request_expired');
  upstream.resolve(0); upstream.resolve(1); assert.equal((await livePending).body.status, 'ok');
  assert.throws(() => registerFuture(broker, imagined), error => error.status === 410);
});

test('shared account enforces aggregate rolling dispatch, inflight and minimum service', async t => {
  const { broker, upstream, advance } = harness(t, { limits: { inFlight: 2, dispatchPerSecond: 4 } });
  const requests = Array.from({ length: 8 }, (_, index) => actorRequest(index));
  const forecasts = Array.from({ length: 4 }, (_, index) => {
    const r = future(); r.runId = requests[0].runId; r.forecastId = `forecast-${index}`; return r;
  });
  register(broker, [...requests, ...forecasts]); forecasts.forEach(r => registerFuture(broker, r));
  const promises = [...forecasts.map(r => broker.submit('future', bytes(r))), ...requests.map(r => broker.submit('npc', bytes(r)))];
  await flush(); assert.equal(upstream.calls.length, 2); assert.equal(broker.status().inFlight, 2);
  upstream.resolve(0); upstream.resolve(1); await flush(); assert.equal(upstream.calls.length, 4);
  assert.equal(upstream.calls[2].payload.state.actor.roleId, requests[0].actor.roleId);
  upstream.resolve(2); upstream.resolve(3); await flush(); assert.equal(upstream.calls.length, 4);
  advance(1000); await flush(); assert.equal(upstream.calls.length, 6);
  upstream.resolve(4); upstream.resolve(5); await flush(); assert.equal(upstream.calls.length, 8);
  upstream.resolve(6); upstream.resolve(7); await flush();
  advance(1000); await Promise.all(promises);
  assert.equal(broker.status().accounting.requests, 8);
});

test('aged forecast backlog cannot starve live admission or service', async t => {
  const { broker, upstream, advance } = harness(t, { limits: { inFlight: 1, pending: 4 } });
  const a = npc(), forecasts = Array.from({ length: 6 }, (_, index) => { const r = future(); r.runId = a.runId; r.forecastId = `future-${index}`; r.deadlineMs = 5000; return r; });
  a.deadlineMs = 5000; register(broker, [a, ...forecasts]); forecasts.forEach(r => registerFuture(broker, r));
  const pending = forecasts.map(r => broker.route('/future/step', bytes(r)));
  const live = broker.route('/npc/decision', bytes(a)); await flush(); advance(1001);
  upstream.resolve(0); await flush();
  assert.ok(upstream.calls[1].payload.state.actor);
  upstream.resolve(1); assert.equal((await live).status, 200);
  await broker.close(); await Promise.all(pending);
});

test('unknown charges retain reservation and shared hard caps prevent later dispatch', async t => {
  const ledger = new AdmissionLedger({ maxRequests: 1, maxSpendNanoUsd: nanoUsd('1'), prices: { jev: { input: nanoUsd('0.042'), output: 0n } } });
  const { broker, upstream } = harness(t, { ledger }); const a = npc(), b = actorRequest(2); register(broker, [a, b]);
  const first = broker.submit('npc', bytes(a)); await flush();
  upstream.calls[0].reject(new Error('secret upstream payload')); assert.equal((await first).body.reasonCode, 'provider_error');
  assert.equal(ledger.status().unknownRequests, 1); assert.notEqual(ledger.status().chargedNanoUsd, '0');
  const second = await broker.submit('npc', bytes(b)); assert.equal(second.body.reasonCode, 'budget_exhausted');
  assert.equal(upstream.calls.length, 1);
});

test('invalid provider distribution is rejected without losing trusted usage', async t => {
  const { broker, upstream, ledger } = harness(t); const r = npc(); register(broker, [r]);
  const pending = broker.submit('npc', bytes(r)); await flush();
  const raw = answer(upstream.calls[0].payload); raw.answers.selection.probabilities[r.candidates[1].candidateId] = 0;
  upstream.calls[0].resolve(raw); const result = await pending;
  assert.equal(result.body.reasonCode, 'invalid_provider_result'); assert.equal(result.body.selectedCandidateId, null);
  assert.equal(ledger.status().knownInputTokens, '101');
  for (const change of [a => { a.answers.selection.choice = 'missing'; }, a => { a.answers.selection.choice = r.candidates[1].candidateId; }, a => { a.model = 'jev-latest'; }, a => { a.worldPatch = {}; }, a => { a.answers.selection.probabilities.extra = 0; }, a => { a.answers.selection.confidence = NaN; }]) {
    const value = answer(upstream.calls[0].payload); change(value); assert.throws(() => choiceResult(value, r.candidates));
  }
});

test('restarted ledger rejects same generation and locks competing account writer', t => {
  const path = mkdtempSync(join(tmpdir(), 'chooguard-ledger-'));
  let next;
  t.after(() => { next?.close(); rmSync(path, { recursive: true, force: true }); });
  const first = openLedger(path); assert.throws(() => openLedger(path), error => error.code === 'account_writer_locked');
  const ledger = new AdmissionLedger({ data: first.data, save: next => first.save(next), maxRequests: 10, maxSpendNanoUsd: nanoUsd('1'), prices: { jev: { input: nanoUsd('1'), output: 0n } } });
  ledger.register('constructor', 1); ledger.reserve('jev'); first.close();
  next = openLedger(path);
  const resumed = new AdmissionLedger({ data: next.data, save: value => next.save(value), maxRequests: 10, maxSpendNanoUsd: nanoUsd('1'), prices: { jev: { input: nanoUsd('1'), output: 0n } } });
  assert.equal(resumed.status().unknownRequests, 1);
  assert.throws(() => resumed.register('constructor', 1), error => error.status === 410); resumed.register('constructor', 2);
});

test('dialogue stays text-only, keeps verified slots separate, unavailable is not a generated answer', async t => {
  const { broker } = harness(t); const r = npc(); register(broker, [r]);
  const request = { schemaVersion: 1, kind: 'dialogue_request', runId: r.runId, generation: r.generation, requestId: 'dialogue-1', actorId: r.actorId, dialogueSeq: '0', mode: r.mode, basisTickUs: r.basisTickUs, expiresAtTickUs: r.expiresAtTickUs, deadlineMs: 2000, roleId: r.actor.roleId, profileRevision: r.actor.profileRevision, observationRevision: r.observationRevision, channel: 'free_conversation', messages: [{ role: 'user', content: '지금 뭘 하고 계세요?' }], knownFacts: [{ factId: 'fact-1', text: '인수 담당자가 아직 도착하지 않았습니다.', truth: 'FALSE', sourceId: 'direct-1' }], truthSlots: [] };
  const unavailable = await broker.submit('dialogue', bytes(request)); assert.equal(unavailable.body.status, 'unavailable'); assert.equal(unavailable.body.text, null);
  request.channel = 'truth_slots'; request.dialogueSeq = '1'; request.messages = []; request.truthSlots = [{ slotId: 'onsite', factId: 'fact-1', text: request.knownFacts[0].text }];
  const slot = await broker.submit('dialogue', bytes(request)); assert.equal(slot.body.text, request.knownFacts[0].text); assert.equal(slot.body.authority, 'text_only');
  request.dialogueSeq = '2'; request.truthSlots[0].text = '인계를 완료했습니다.';
  await assert.rejects(broker.submit('dialogue', bytes(request)), error => error.status === 422);
  assert.equal(broker.status().accounting.requests, 0);
});

test('localhost HTTP enforces capability, origin, body lexical rules and safe unavailability', async t => {
  const ledger = new AdmissionLedger({ data: emptyLedger(), maxRequests: 0, maxSpendNanoUsd: 0n, prices: {} });
  const { broker } = harness(t, { jev: null, ledger }); const token = 'a'.repeat(48);
  const server = createGameplayServer({ broker, token }); await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => { server.closeAllConnections(); server.close(); }); const base = `http://127.0.0.1:${server.address().port}`;
  assert.equal((await fetch(`${base}/status`)).status, 401);
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
  assert.equal((await fetch(`${base}/status`, { headers: { ...headers, Origin: 'https://evil.example' } })).status, 403);
  assert.equal((await fetch(`${base}/npc/decision`, { method: 'POST', headers, body: '{"x":1,"x":2}' })).status, 400);
  const r = npc(); const registration = register(broker, [r]);
  const response = await fetch(`${base}/npc/decision`, { method: 'POST', headers, body: JSON.stringify(r) });
  assert.equal(response.status, 503); const result = await response.json(); assert.equal(validators.npcResult(result), true); assert.equal(result.model, null);
  assert.equal(JSON.stringify(await (await fetch(`${base}/status`, { headers })).json()).includes(token), false);
  registration.actors = [null]; assert.throws(() => broker.update(registration), error => error.status === 422);
});

test('real HTTP provider transport never retries or exposes rejection text', async t => {
  let calls = 0;
  const server = http.createServer((req, res) => { calls++; req.resume(); res.writeHead(429, { 'Content-Type': 'application/json' }); res.end('{"error":"private-key-and-payload","usage":{"input_tokens":13,"output_tokens":0}}'); });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => { server.closeAllConnections(); server.close(); });
  const transport = createJsonTransport(`http://127.0.0.1:${server.address().port}/v1/chat/completions`, 'test-key'); t.after(() => transport.close());
  await assert.rejects(transport.request({ test: true }), error => error.code === 'rate_limited' && error.message === 'rate_limited' && error.providerResult.usage.input_tokens === 13);
  assert.equal(calls, 1);
});

test('paid configuration fails closed without explicit caps and prices', () => {
  const env = { CHOOGUARD_GAMEPLAY_TOKEN: 'b'.repeat(48), TYPESAFE_API_KEY: 'test-key' };
  assert.throws(() => configuration(env), error => error.code === 'request_cap_required');
  assert.equal(configuration({ CHOOGUARD_GAMEPLAY_TOKEN: env.CHOOGUARD_GAMEPLAY_TOKEN }).maxRequests, 0);
});

test('virtual actor knowledge advances without changing live knowledge; branch time cannot rewind', async t => {
  const { broker, upstream } = harness(t); const r = future(); register(broker, [r]); registerFuture(broker, r);
  const first = broker.submit('future', bytes(r)); await flush(); upstream.resolve(); await first;
  const next = structuredClone(r); next.requestId = 'next-step'; next.stepSeq = '2'; next.depth = 2;
  next.stepTickUs = '3000000'; next.perspective.knowledgeRevision = '13';
  const second = broker.submit('future', bytes(next)); await flush(); upstream.resolve(); assert.equal((await second).status, 200);
  assert.equal(broker.sessions.get(r.runId).actors.get(r.perspective.actorId).observationRevision, '12');
  next.stepSeq = '3'; next.requestId = 'bad-depth';
  await assert.rejects(broker.submit('future', bytes(next)), error => error.status === 422);
  next.depth = 3; next.perspective.knowledgeRevision = '12';
  await assert.rejects(broker.submit('future', bytes(next)), error => error.status === 422);
});

test('observation expiry invalidates a decision before its broader request deadline', async t => {
  const { broker, upstream } = harness(t); const r = npc(); r.expiresAtTickUs = '9000000';
  const registry = register(broker, [r]); const pending = broker.submit('npc', bytes(r)); await flush();
  registry.tickUs = '5000000'; broker.update(registry);
  const result = await pending; assert.equal(result.status, 410); assert.equal(result.body.reasonCode, 'request_expired');
  upstream.resolve(); await flush(); assert.equal(broker.status().accounting.knownInputTokens, '101');
});
