import { mkdirSync, openSync, closeSync, readFileSync, writeFileSync, fsyncSync, renameSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { homedir } from 'node:os';
import { requireValue, WireError } from './gameplay-wire.mjs';

export function nanoUsd(value) {
  requireValue(typeof value === 'string' && /^(0|[1-9][0-9]*)(\.[0-9]{1,9})?$/.test(value), 'invalid_configuration');
  const [whole, fraction = ''] = value.split('.');
  return BigInt(whole) * 1000000000n + BigInt(fraction.padEnd(9, '0'));
}
export function usageCost(usage, prices) {
  return (BigInt(usage.inputTokens) * prices.input + BigInt(usage.outputTokens) * prices.output + 999999n) / 1000000n;
}
export function emptyLedger() {
  return { version: 1, requests: 0, chargedNanoUsd: '0', knownInputTokens: '0', knownOutputTokens: '0', unknownRequests: 0, overrun: false, runs: {} };
}
// A single host-wide writer covers both JEV endpoints and optional dialogue, including
// multiple provider keys belonging to the same billing account. Crash locks fail closed.
export function openLedger(directory = join(homedir(), '.chooguard', 'gameplay-broker')) {
  mkdirSync(directory, { recursive: true, mode: 0o700 });
  const lock = join(directory, 'writer.lock'), path = join(directory, 'ledger.json');
  let descriptor;
  try { descriptor = openSync(lock, 'wx', 0o600); }
  catch { throw new WireError(503, 'account_writer_locked'); }
  let data;
  try {
    writeFileSync(descriptor, String(process.pid)); fsyncSync(descriptor);
    try { data = JSON.parse(readFileSync(path, 'utf8')); }
    catch (error) { if (error.code !== 'ENOENT') throw error; data = emptyLedger(); }
    requireValue(data.version === 1 && Number.isSafeInteger(data.requests) && data.requests >= 0 && Number.isSafeInteger(data.unknownRequests) && data.unknownRequests >= 0 && typeof data.overrun === 'boolean' && data.runs && !Array.isArray(data.runs), 'invalid_ledger');
    for (const key of ['chargedNanoUsd', 'knownInputTokens', 'knownOutputTokens']) requireValue(typeof data[key] === 'string' && /^(0|[1-9][0-9]*)$/.test(data[key]), 'invalid_ledger');
    requireValue(Object.keys(data.runs).length <= 64, 'invalid_ledger');
    for (const generation of Object.values(data.runs)) requireValue(Number.isInteger(generation) && generation >= 0 && generation <= 2147483647, 'invalid_ledger');
  } catch {
    closeSync(descriptor); unlinkSync(lock); throw new WireError(503, 'ledger_unavailable');
  }
  let closed = false;
  return {
    data,
    save(next) {
      requireValue(!closed, 'ledger_unavailable', 503);
      const temp = `${path}.next`;
      const file = openSync(temp, 'w', 0o600);
      try { writeFileSync(file, JSON.stringify(next)); fsyncSync(file); } finally { closeSync(file); }
      renameSync(temp, path);
      // Directory fsync is unavailable on some Windows hosts; file content is still synced.
      if (process.platform !== 'win32') {
        const dir = openSync(directory, 'r');
        try { fsyncSync(dir); } finally { closeSync(dir); }
      }
    },
    close() { if (!closed) { closed = true; closeSync(descriptor); unlinkSync(lock); } }
  };
}
export class AdmissionLedger {
  constructor({ data = emptyLedger(), save = () => {}, maxRequests, maxSpendNanoUsd, prices }) {
    this.data = data; this.save = save; this.maxRequests = maxRequests;
    this.maxSpendNanoUsd = maxSpendNanoUsd; this.prices = prices;
    this.failed = false;
  }
  persist(next) {
    if (this.failed) throw new WireError(503, 'budget_exhausted');
    try { this.save(next); this.data = next; }
    catch { this.failed = true; throw new WireError(503, 'budget_exhausted'); }
  }
  register(runId, generation) {
    const previous = Object.hasOwn(this.data.runs, runId) ? this.data.runs[runId] : undefined;
    requireValue(previous === undefined || generation > previous, 'request_expired', 410);
    requireValue(previous !== undefined || Object.keys(this.data.runs).length < 64, 'queue_full', 503);
    this.persist({ ...this.data, runs: { ...this.data.runs, [runId]: generation } });
  }
  reserve(provider) {
    const prices = this.prices[provider];
    requireValue(prices, 'budget_exhausted', 503);
    const reservation = usageCost({ inputTokens: 69632, outputTokens: 65536 }, prices);
    requireValue(!this.failed && !this.data.overrun && this.data.requests < this.maxRequests && BigInt(this.data.chargedNanoUsd) + reservation <= this.maxSpendNanoUsd, 'budget_exhausted', 503);
    this.persist({ ...this.data, requests: this.data.requests + 1, unknownRequests: this.data.unknownRequests + 1, chargedNanoUsd: String(BigInt(this.data.chargedNanoUsd) + reservation) });
    return { provider, reservation, accounted: false };
  }
  account(ticket, usage) {
    if (ticket.accounted || !usage) return;
    ticket.accounted = true;
    const cost = usageCost(usage, this.prices[ticket.provider]);
    this.persist({ ...this.data,
      chargedNanoUsd: String(BigInt(this.data.chargedNanoUsd) - ticket.reservation + cost),
      knownInputTokens: String(BigInt(this.data.knownInputTokens) + BigInt(usage.inputTokens)),
      knownOutputTokens: String(BigInt(this.data.knownOutputTokens) + BigInt(usage.outputTokens)),
      unknownRequests: this.data.unknownRequests - 1,
      overrun: this.data.overrun || cost > ticket.reservation
    });
  }
  status() {
    const { requests, chargedNanoUsd, knownInputTokens, knownOutputTokens, unknownRequests, overrun } = this.data;
    return { requests, maxRequests: this.maxRequests, chargedNanoUsd, maxSpendNanoUsd: String(this.maxSpendNanoUsd), knownInputTokens, knownOutputTokens, unknownRequests, halted: this.failed || overrun };
  }
}
