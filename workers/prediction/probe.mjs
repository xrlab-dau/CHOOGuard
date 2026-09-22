#!/usr/bin/env node
// Root-only live helper. Reads a real exported accepted snapshot; never fabricates physics.
import { readFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { performance } from 'node:perf_hooks';
const file = process.argv[2];
if (!file) { process.stderr.write('usage: node workers/prediction/probe.mjs REAL_SNAPSHOT_JSON\n'); process.exit(2); }
const basis = JSON.parse(await readFile(file, 'utf8'));
const results = [];
for (const label of ['cold-process-first','warm-persistent-1','warm-persistent-2']) {
  // Client canonical stateHash excludes snapshotId/stateHash; changing request identity avoids cache.
  const snapshot = { ...basis, snapshotId: randomUUID() };
  const start = performance.now();
  try {
    const response = await fetch('http://127.0.0.1:18764/forecast', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(snapshot), signal: AbortSignal.timeout(4500) });
    const result = await response.json();
    results.push({ label, httpStatus: response.status, wallLatencyMs: Math.round(performance.now() - start), ...result });
  } catch { results.push({ label, status: 'unavailable', reasonCode: 'probe_transport_unavailable', wallLatencyMs: Math.round(performance.now() - start) }); }
}
process.stdout.write(JSON.stringify({ validationSurface: 'Actual loopback→gateway noul calls; first is process-cold only if the service was just restarted, not guaranteed model-cold. No calibration claim.', results }, null, 2) + '\n');
