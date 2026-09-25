import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { createServer, createConnection } from 'node:net';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const executable = fileURLToPath(new URL('./gameplay-broker.mjs', import.meta.url));
const capability = 'lifecycle-private-capability-0123456789abcdef';
async function bounded(promise, milliseconds = 5000) {
  let timer;
  try {
    return await Promise.race([promise, new Promise((_, reject) => {
      timer = setTimeout(() => reject(new Error('broker lifecycle deadline exceeded')), milliseconds);
    })]);
  } finally { clearTimeout(timer); }
}
async function reservePort() {
  const server = createServer();
  await new Promise((done, fail) => { server.once('error', fail); server.listen(0, '127.0.0.1', done); });
  return server;
}
async function unusedPort() {
  const reservation = await reservePort(), port = reservation.address().port;
  await new Promise(done => reservation.close(done));
  return port;
}
function fixture(t) {
  const home = mkdtempSync(join(tmpdir(), 'chooguard-broker-lifecycle-'));
  const lock = join(home, '.chooguard', 'gameplay-broker', 'writer.lock');
  const children = [];
  t.after(async () => {
    for (const runtime of children) {
      if (runtime.child.exitCode === null && runtime.child.signalCode === null) runtime.child.kill('SIGKILL');
      await bounded(runtime.exited);
    }
    // Cleanup follows confirmed process exits, never removal of an active writer lock.
    rmSync(home, { recursive: true, force: true });
  });
  return { home, lock, launch(port, owned = true) {
    const env = { ...process.env, HOME: home, USERPROFILE: home,
      CHOOGUARD_GAMEPLAY_TOKEN: capability, CHOOGUARD_GAMEPLAY_PORT: String(port),
      TYPESAFE_API_KEY: '', CHOOGUARD_DIALOGUE_KEY: '', CHOOGUARD_DIALOGUE_ENDPOINT: '', CHOOGUARD_DIALOGUE_MODEL: '' };
    delete env.NODE_OPTIONS; delete env.NODE_PATH; delete env.CHOOGUARD_GAMEPLAY_SCHEMA_DIR;
    delete env.CHOOGUARD_ACCOUNT_DISPATCH_PER_SECOND; delete env.CHOOGUARD_ACCOUNT_INFLIGHT;
    const child = spawn(process.execPath, [executable, ...(owned ? ['--owner-stdin'] : [])], { env, stdio: ['pipe', 'pipe', 'pipe'] });
    let output = '', errors = '', readyResolve, readyReject;
    const ready = new Promise((done, fail) => { readyResolve = done; readyReject = fail; });
    ready.catch(() => {}); // Failed-start cases await exit instead of readiness.
    child.stdout.setEncoding('utf8'); child.stderr.setEncoding('utf8');
    child.stdout.on('data', chunk => { output += chunk; if (output.includes('chooguard_gameplay_ready\n')) readyResolve(); });
    child.stderr.on('data', chunk => { errors += chunk; });
    child.stdin.on('error', () => {});
    const exited = new Promise((done, fail) => {
      child.once('error', error => { readyReject(error); fail(error); });
      child.once('close', (code, signal) => { readyReject(new Error('broker exited before readiness')); done({ code, signal }); });
    });
    const runtime = { child, ready, exited, logs: () => output + errors };
    children.push(runtime);
    return runtime;
  } };
}
async function request(port, path = '/status', body) {
  const response = await fetch(`http://127.0.0.1:${port}${path}`, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { Authorization: `Bearer ${capability}`, 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(2000)
  });
  return { status: response.status, body: await response.json() };
}
async function cleanExit(runtime) {
  assert.deepEqual(await bounded(runtime.exited, 3000), { code: 0, signal: null });
  assert.ok(!runtime.logs().includes(capability), 'private capability must not be logged');
}

test('owned shutdown releases the default ledger for a second launch without weakening the writer lock', async t => {
  const f = fixture(t), port = await unusedPort();
  const first = f.launch(port); await bounded(first.ready);
  const registration = { schemaVersion: 1, runId: 'lifecycle-run', generation: 1, mode: 'RANDOM_OPERATIONS_LAB', tickUs: '0', actors: [], revisions: [] };
  assert.equal((await request(port, '/session/register', registration)).status, 200);
  const owner = readFileSync(f.lock, 'utf8');
  const competitor = f.launch(await unusedPort());
  assert.deepEqual(await bounded(competitor.exited), { code: 1, signal: null });
  assert.equal(competitor.logs(), 'account_writer_locked\n');
  assert.equal(readFileSync(f.lock, 'utf8'), owner);
  assert.equal((await request(port)).body.status, 'ready');

  // An incomplete HTTP body must not delay the private owner channel or ledger release.
  const stalled = createConnection({ host: '127.0.0.1', port });
  t.after(() => stalled.destroy());
  await new Promise((done, fail) => { stalled.once('connect', done); stalled.once('error', fail); });
  stalled.write(`POST /session/register HTTP/1.1\r\nHost: 127.0.0.1:${port}\r\nAuthorization: Bearer ${capability}\r\nContent-Type: application/json\r\nContent-Length: 100\r\n\r\n{`);
  first.child.stdin.write('shut'); first.child.stdin.write('down\n');
  await cleanExit(first); assert.equal(existsSync(f.lock), false);

  const second = f.launch(port); await bounded(second.ready);
  assert.equal((await request(port, '/session/register', registration)).status, 410);
  assert.equal((await request(port, '/session/register', { ...registration, generation: 2 })).status, 200);
  second.child.stdin.end('shutdown\n');
  await cleanExit(second); assert.equal(existsSync(f.lock), false);
});

test('external brokers ignore client stdin shutdown and expose no HTTP shutdown endpoint', async t => {
  const f = fixture(t), port = await unusedPort();
  const external = f.launch(port, false); await bounded(external.ready);
  external.child.stdin.end('shutdown\n');
  assert.equal((await request(port, '/shutdown', {})).status, 404);
  assert.equal((await request(port)).body.status, 'ready');
  assert.equal(external.child.exitCode, null);
  assert.equal(readFileSync(f.lock, 'utf8'), String(external.child.pid));
  assert.ok(!external.logs().includes(capability), 'external capabilities must not be logged');
});

test('owned pipe EOF releases the lock and a failed listener startup also cleans up', async t => {
  const f = fixture(t), occupied = await reservePort(), port = occupied.address().port;
  t.after(() => { if (occupied.listening) occupied.close(); });
  const failed = f.launch(port);
  assert.deepEqual(await bounded(failed.exited), { code: 1, signal: null });
  assert.equal(failed.logs(), 'startup_unavailable\n');
  assert.equal(existsSync(f.lock), false);
  await new Promise(done => occupied.close(done));

  const started = f.launch(port); await bounded(started.ready);
  started.child.stdin.end();
  await cleanExit(started); assert.equal(existsSync(f.lock), false);

  const interrupted = f.launch(port);
  interrupted.child.stdin.end('shutdown\n');
  assert.deepEqual(await bounded(interrupted.exited), { code: 0, signal: null });
  assert.equal(existsSync(f.lock), false);
  assert.ok(!interrupted.logs().includes(capability), 'early shutdown must not log credentials');
});

test('a crashed writer remains fail-closed rather than guessing PID liveness', async t => {
  const f = fixture(t), port = await unusedPort();
  const crashed = f.launch(port); await bounded(crashed.ready);
  const owner = readFileSync(f.lock, 'utf8');
  crashed.child.kill('SIGKILL'); await bounded(crashed.exited);
  const restart = f.launch(port);
  assert.deepEqual(await bounded(restart.exited), { code: 1, signal: null });
  assert.equal(restart.logs(), 'account_writer_locked\n');
  assert.equal(readFileSync(f.lock, 'utf8'), owner);
});
