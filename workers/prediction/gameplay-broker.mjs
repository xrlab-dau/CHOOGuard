#!/usr/bin/env node
import http from 'node:http';
import { createHash, timingSafeEqual } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { GameplayBroker } from './gameplay-core.mjs';
import { AdmissionLedger, nanoUsd, openLedger } from './gameplay-ledger.mjs';
import { createJevProvider, createJsonTransport } from './gameplay-providers.mjs';
import { MAX_BYTES, WireError, createValidators, requireValue } from './gameplay-wire.mjs';

const keyValid = key => typeof key === 'string' && key.length >= 1 && key.length <= 8192 && !/[\x00-\x20\x7f]/.test(key);
const digest = value => createHash('sha256').update(value).digest();
export function configuration(env = process.env) {
  const token = env.CHOOGUARD_GAMEPLAY_TOKEN;
  requireValue(typeof token === 'string' && /^[A-Za-z0-9_-]{32,256}$/.test(token), 'capability_unavailable');
  const port = Number(env.CHOOGUARD_GAMEPLAY_PORT ?? 8788);
  requireValue(Number.isInteger(port) && port >= 1 && port <= 65535, 'invalid_configuration');
  const apiKey = env.TYPESAFE_API_KEY || null;
  requireValue(apiKey === null || keyValid(apiKey), 'credential_unavailable');
  const dialogueKey = env.CHOOGUARD_DIALOGUE_KEY || null;
  const dialogueEndpoint = env.CHOOGUARD_DIALOGUE_ENDPOINT || null;
  const dialogueModel = env.CHOOGUARD_DIALOGUE_MODEL || null;
  const dialogueAny = dialogueKey || dialogueEndpoint || dialogueModel;
  requireValue(!dialogueAny || (keyValid(dialogueKey) && typeof dialogueModel === 'string' && dialogueModel.length <= 128 && !/[\r\n]/.test(dialogueModel) && dialogueEndpoint), 'invalid_configuration');
  const paid = !!apiKey || !!dialogueAny;
  const maxRequests = paid ? Number(env.CHOOGUARD_MAX_REQUESTS) : 0;
  requireValue(Number.isSafeInteger(maxRequests) && maxRequests >= (paid ? 1 : 0) && maxRequests <= 1000000, 'request_cap_required');
  const maxSpendNanoUsd = paid ? nanoUsd(env.CHOOGUARD_MAX_SPEND_USD) : 0n;
  requireValue(!paid || maxSpendNanoUsd > 0n, 'spend_cap_required');
  const prices = {};
  if (apiKey) prices.jev = { input: nanoUsd(env.CHOOGUARD_JEV_INPUT_USD_PER_MILLION), output: nanoUsd(env.CHOOGUARD_JEV_OUTPUT_USD_PER_MILLION) };
  if (dialogueAny) prices.dialogue = { input: nanoUsd(env.CHOOGUARD_DIALOGUE_INPUT_USD_PER_MILLION), output: nanoUsd(env.CHOOGUARD_DIALOGUE_OUTPUT_USD_PER_MILLION) };
  for (const price of Object.values(prices)) requireValue(price.input + price.output > 0n, 'pricing_required');
  const dispatchPerSecond = Number(env.CHOOGUARD_ACCOUNT_DISPATCH_PER_SECOND ?? 12);
  const inFlight = Number(env.CHOOGUARD_ACCOUNT_INFLIGHT ?? 12);
  const schemaDirectory = env.CHOOGUARD_GAMEPLAY_SCHEMA_DIR ? pathToFileURL(resolve(env.CHOOGUARD_GAMEPLAY_SCHEMA_DIR) + '/') : undefined;
  return { token, port, apiKey, dialogueKey, dialogueEndpoint, dialogueModel, maxRequests, maxSpendNanoUsd, prices, limits: { dispatchPerSecond, inFlight }, schemaDirectory };
}
function send(res, status, payload) {
  if (res.destroyed || res.writableEnded) return;
  const body = JSON.stringify(payload);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(body), 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
  res.end(body);
}
export function createGameplayServer({ broker, token }) {
  requireValue(typeof token === 'string' && /^[A-Za-z0-9_-]{32,256}$/.test(token), 'capability_unavailable');
  const expected = digest(`Bearer ${token}`);
  let connections = 0;
  const server = http.createServer(async (req, res) => {
    if (req.headers.origin !== undefined || !['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(req.socket.remoteAddress)) {
      send(res, 403, { status: 'unavailable', reasonCode: 'origin_rejected' }); return;
    }
    if (typeof req.headers.authorization !== 'string' || !timingSafeEqual(expected, digest(req.headers.authorization))) {
      send(res, 401, { status: 'unavailable', reasonCode: 'unauthorized' }); return;
    }
    if (!/^127\.0\.0\.1(?::[0-9]{1,5})?$/.test(req.headers.host ?? '')) {
      send(res, 403, { status: 'unavailable', reasonCode: 'host_rejected' }); return;
    }
    if (req.method === 'GET' && req.url === '/status') { send(res, 200, broker.status()); return; }
    const paths = ['/npc/decision', '/future/step', '/dialogue/generate', '/session/register', '/session/update', '/future/register', '/future/close', '/request/cancel', '/request/usage'];
    if (req.method !== 'POST' || !paths.includes(req.url)) { send(res, 404, { status: 'unavailable', reasonCode: 'not_found' }); return; }
    if (!/^application\/json(?:;\s*charset=utf-8)?$/i.test(req.headers['content-type'] ?? '') || req.headers['content-encoding']) {
      send(res, 415, { status: 'unavailable', reasonCode: 'unsupported_content_type' }); return;
    }
    if (connections > 256) { send(res, 429, { status: 'unavailable', reasonCode: 'rate_limited' }); return; }
    const chunks = []; let size = 0;
    try {
      const raw = await new Promise((resolveBody, reject) => {
        const timeout = setTimeout(() => { reject(new WireError(408, 'body_timeout')); req.pause(); }, 5000);
        const stop = () => clearTimeout(timeout);
        req.on('data', chunk => {
          size += chunk.length;
          if (size > MAX_BYTES) { stop(); reject(new WireError(413, 'body_too_large')); req.pause(); }
          else chunks.push(chunk);
        });
        req.once('end', () => { stop(); resolveBody(Buffer.concat(chunks)); });
        req.once('error', () => { stop(); reject(new WireError(400, 'invalid_request')); });
        req.once('aborted', () => { stop(); reject(new WireError(400, 'invalid_request')); });
      });
      const result = await broker.route(req.url, raw);
      send(res, result.status, result.body);
    } catch (error) {
      send(res, error instanceof WireError ? error.status : 500, { status: 'unavailable', reasonCode: error instanceof WireError ? error.code : 'internal_error' });
      if (error.status === 413 || error.status === 408) res.once('finish', () => req.socket.destroy());
    }
  });
  server.on('connection', socket => { connections++; socket.once('close', () => { connections--; }); });
  server.maxConnections = 256; server.maxRequestsPerSocket = 256;
  server.headersTimeout = 5000; server.requestTimeout = 10000; server.keepAliveTimeout = 5000;
  return server;
}
export async function startGameplayBroker(config = configuration()) {
  const durable = openLedger();
  let broker, server;
  try {
    const ledger = new AdmissionLedger({ ...config, data: durable.data, save: next => durable.save(next) });
    broker = new GameplayBroker({ ledger, jev: createJevProvider(config.apiKey), dialogue: config.dialogueEndpoint ? createJsonTransport(config.dialogueEndpoint, config.dialogueKey) : null, dialogueModel: config.dialogueModel, validators: createValidators(config.schemaDirectory), limits: config.limits });
    server = createGameplayServer({ broker, token: config.token });
    await new Promise((done, fail) => { server.once('error', fail); server.listen(config.port, '127.0.0.1', done); });
    let closing;
    return { server, broker, close() {
      if (!closing) closing = (async () => {
        const stopped = new Promise(done => server.close(done));
        try { await broker.close(); }
        finally {
          server.closeAllConnections();
          await stopped;
          durable.close();
        }
      })();
      return closing;
    } };
  } catch (error) {
    try { await broker?.close(); }
    finally { server?.close(); server?.closeAllConnections(); durable.close(); }
    throw error;
  }
}
async function main() {
  const args = process.argv.slice(2);
  requireValue(args.every(arg => arg === '--key-stdin' || arg === '--owner-stdin') && args.length <= 1, 'unsupported_option');
  if (args.includes('--key-stdin')) {
    const input = createInterface({ input: process.stdin, terminal: false });
    process.env.TYPESAFE_API_KEY = await new Promise(resolveKey => {
      input.once('line', line => { resolveKey(line); input.close(); process.stdin.pause(); });
      input.once('close', () => resolveKey(''));
    });
    requireValue(keyValid(process.env.TYPESAFE_API_KEY), 'credential_unavailable');
  }
  let runtime, input, shutdownRequested = false;
  const close = () => {
    if (shutdownRequested) return;
    shutdownRequested = true;
    if (input) { input.close(); process.stdin.destroy(); }
    runtime?.close().catch(() => { process.exitCode = 1; });
  };
  // Only a launcher that owns this private inherited pipe can request shutdown.
  // External HTTP clients (even authenticated ones) have no shutdown endpoint.
  if (args.includes('--owner-stdin')) {
    input = createInterface({ input: process.stdin, terminal: false });
    input.on('line', line => { if (line === 'shutdown') close(); });
    input.once('close', close); // Owner exit/EOF also releases the writer lock.
    input.once('error', close);
  }
  process.once('SIGINT', close); process.once('SIGTERM', close);
  try {
    runtime = await startGameplayBroker();
    if (shutdownRequested) await runtime.close();
    else process.stdout.write('chooguard_gameplay_ready\n');
  } catch (error) {
    close();
    throw error;
  }
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => {
    const codes = new Set(['capability_unavailable', 'credential_unavailable', 'invalid_configuration', 'request_cap_required', 'spend_cap_required', 'pricing_required', 'account_writer_locked', 'ledger_unavailable', 'unsupported_option']);
    process.stderr.write(`${codes.has(error.code) ? error.code : 'startup_unavailable'}\n`); process.exitCode = 1;
  });
}
