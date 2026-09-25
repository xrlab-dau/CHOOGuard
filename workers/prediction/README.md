# Jev internal operational kernel service

Current active API: `GET /health`, `POST /turnaround` on `127.0.0.1:18764`. The former public-forecast `/forecast` endpoint is retired (404); the old `probe.mjs` is historical and is not an operational-kernel probe. No public probability UI belongs to this slice.

Official wire is locally inspected `@typesafe-ai/sdk`0.6.0: direct `POST https://ai-gateway.vercel.sh/typesafe/v1/systemone` with fixed `typesafe-ai/jev` model, `{state,questions,model}` and typed `{type:"noul",noul:0..1}` answer. Node built-ins only; persistent HTTPS agent, one upstream flight across endpoints,3s deadline,0 retries,32KiB request/64KiB response bounds and32 in-memory exact cached responses. Upstream errors expose only safe status/code/allowlisted schema paths.

## Root launch

Preferred hidden stdin (the terminal prompts without echo; key is not placed in args/files/history):

```sh
python3 - <<'PY'
import getpass, subprocess
secret = getpass.getpass('Vercel gateway key: ')
process = subprocess.Popen(['node', 'workers/prediction/jev-proxy.mjs', '--key-stdin'], stdin=subprocess.PIPE)
process.stdin.write((secret + '\n').encode())
process.stdin.close()
del secret
try:
    process.wait()
except KeyboardInterrupt:
    process.terminate()
    process.wait()
PY
```

Root may alternatively provide `VERCEL_AI_GATEWAY_API_KEY` in the inherited environment and run `node workers/prediction/jev-proxy.mjs`. Do not paste a key into a command argument, source file, Unity asset, or captured tool output. Service logs only a fixed readiness marker/safe startup error enum; upstream bodies/headers/errors are never logged or echoed. `SIGTERM`/`SIGINT` cleanly close listener and upstream sockets. No process is launched by implementation author.

## Internal turnaround protocol

Request fields: `schemaVersion:1,requestId,inputHash,operationId,agencyId,logicalTeamId,runId,generation,normalSeconds,extendedSeconds,workload,physics,cohorts,operationHistory`.

- `workload`: `workType,memberCount,operationStartedSim,onsiteDurationSeconds,returnStartedSim,travelMetres`; all times are actual accepted simulation values. `onsiteDurationSeconds` is cumulative duration, not an absolute timestamp.
- `physics`: actual accepted total/evacuated/releaseRevision/phase, finite density/pressureIndicator/visibility/temperature/maxFedToxic/maxFedConvectiveHeat/incidentTime and field/role control booleans.
- Up to16 cohort records and last8 actual operation event labels. Identifiers remain local/echo provenance; model state contains compact workload and observations, without request/run/agency/team IDs.
- Question is latent **normal versus extended workload regime**, never the probability of being ready at a deadline. Response echoes request/inputHash/operation/run/generation and captured timing profile, with `hasProbability:true,probabilityNormal` only on validated live noul success. Unavailable has `hasProbability:false` and no probability field.

The Unity kernel computes `E[T] = pNormal*normalSeconds + (1-pNormal)*extendedSeconds`. Default15/45seconds are authored training-game durations, not surveyed119 procedure or calibrated personnel physics. Profile must remain ordered within15..45seconds.

`PrepareTurnaround` starts the read-only classification during return. Unity has at most one flight and a bounded16 pending preparations. `ResolveTurnaround` at base arrival freezes the matching result or explicit normal baseline immediately. A missing/invalid/late response has **no fabricated probability** (`ProbabilityNormal=null`, `HasProbability=false`, `Source=baseline`). Later replies cannot alter the immutable decision/timer. Reset cancels outstanding requests and clears decisions.

Root owns credentials and live/native verification; implementation author performs syntax checks only. This endpoint cannot issue commands or mutate physical/control/graph state. The timed-activity owner consumes ExpectedSeconds in an accepted-time countdown.

## FPS gameplay broker (separate process)

`gameplay-broker.mjs` does **not** alter `/turnaround`. It listens on
`127.0.0.1:8788` and uses direct
[`POST https://api.typesafe.ai/v1/systemone`](https://docs.typesafe.ai/api.md),
explicit model `jev-1.13.0`, and one **Choice** over exactly the caller's grounded
candidates. Its independent provider is not the Vercel gateway. The verified
`.tools/jev/jev.mjs` uses the same direct wire and `TYPESAFE_API_KEY`.
Neither JEV text generation nor numeric physics generation is implemented or
implied. Keys and upstream bodies/errors are never logged.

### Install, launch, and verification

```sh
npm ci --prefix workers/prediction --ignore-scripts --no-audit --no-fund
node workers/prediction/gameplay-broker.mjs
# Integration owner: deterministic boundary suite, no external paid requests
npm test --prefix workers/prediction
```

Node 22–26 is accepted; use a maintained Node LTS runtime for deployment.
AJV `8.17.1` and its resolved transitive dependencies are locked. JSON Schema
2020-12 request/result shapes are loaded directly from the approved design
`contracts/{npc-decision,future-step}.schema.json`; there is no divergent copy.
For packaging, ship those two files and set `CHOOGUARD_GAMEPLAY_SCHEMA_DIR` to
their directory. The runtime has no network schema resolver.

Every HTTP request, including `GET /status`, requires
`Authorization: Bearer <CHOOGUARD_GAMEPLAY_TOKEN>`. The owner supplies a random,
per-launch 32–256 character base64url/hex capability to both consumer and worker.
Bind is always loopback; the port can be explicitly overridden with
`CHOOGUARD_GAMEPLAY_PORT`. Browser `Origin` headers and non-loopback Host values
are rejected. POST bodies must be `application/json` (optional UTF-8 charset).
Do not put capabilities in query strings.

With only `CHOOGUARD_GAMEPLAY_TOKEN` configured the server starts honestly
offline: inference returns typed `unavailable`, not a fake decision. A direct
key requires **all** of these explicit settings before startup:

| Setting | Meaning |
|---|---|
| `TYPESAFE_API_KEY` | Direct TypeSafe account key, not a Vercel key |
| `CHOOGUARD_MAX_REQUESTS` | Positive integer lifetime dispatch cap, at most 1,000,000 |
| `CHOOGUARD_MAX_SPEND_USD` | Positive USD admission cap, at most 9 decimal places |
| `CHOOGUARD_JEV_INPUT_USD_PER_MILLION` | Account's explicit input-token price |
| `CHOOGUARD_JEV_OUTPUT_USD_PER_MILLION` | Explicit output-token price; zero is allowed |
| `CHOOGUARD_ACCOUNT_DISPATCH_PER_SECOND` | Optional lower account limit, 1–12; default 12 |
| `CHOOGUARD_ACCOUNT_INFLIGHT` | Optional lower account concurrency, 1–12; default 12 |

No rate or budget is silently chosen as paid-call authorization. Check the
account's actual prices/quota before configuring them. Zero prices for both
input and output are rejected. Separate processes using `.tools/jev`, another
machine, or the unchanged Vercel service are **not** coordinated by this worker:
stop competing direct-account callers or allocate lower account limits and a
provider-side account spend cap. This is a local personal-credential service,
not a distributed account gateway.

Alternative direct-key input avoids a key in command history/argv:

```sh
# Capability and explicit cap/price settings must already be in the inherited
# protected environment shared with the consumer. The key prompt does not echo.
python3 - <<'PY'
import getpass
import subprocess
key = getpass.getpass('Direct TypeSafe key: ')
child = subprocess.Popen(
    ['node', 'workers/prediction/gameplay-broker.mjs', '--key-stdin'],
    stdin=subprocess.PIPE)
child.stdin.write((key + '\n').encode())
child.stdin.close()
del key
try:
    child.wait()
except KeyboardInterrupt:
    child.terminate()
    child.wait()
PY
```

The existing runner's configuration path was inspected, not any secret value.
At implementation-time, inherited `TYPESAFE_API_KEY`,
`VERCEL_AI_GATEWAY_API_KEY`, and `CHOOGUARD_GAMEPLAY_TOKEN` were absent.
No credential file is auto-read. Do not capture `env`, shell profile contents,
the key prompt's response, or authorization headers in a tool transcript.

### Session and snapshot registration

All JSON objects reject extra or missing properties. All IDs and decimal
Int64 strings follow the approved schemas. This authenticated registration
boundary is owned by the trusted gameplay snapshot builder, not by model text.

`POST /session/register`:

```json
{
  "schemaVersion": 1,
  "runId": "run-1",
  "generation": 1,
  "mode": "RANDOM_OPERATIONS_LAB",
  "tickUs": "1000000",
  "actors": [
    {"actorId":"staff-7","roleId":"station-staff","profileRevision":"3","observationRevision":"9"}
  ],
  "revisions": [{"entityId":"staff-7","revision":"3"}]
}
```

It returns `200 {"status":"registered","runId":"run-1","generation":1}`.
The same object shape goes to `/session/update`, which returns status `updated`.
Updates are **delta merges**: empty actor/revision arrays are allowed.
Tick, actor profile/observation revision and entity revisions cannot decrease.
Changing role requires a higher profile revision. Mode is immutable within a
generation. Include every entity appearing in a decision/future read set.
Registry updates are not simulation writes; they mirror accepted state.

Registration requires a strictly newer generation for an existing run; it
cancels old requests and starts fresh actor/forecast sequences. Restarts require
a higher generation too. At most 16 current runs, 1,024 retained actors and 8,192
entity revisions per run are admitted. These limits stop admission rather than
discard high-water marks. Large actor populations can register in bounded delta
batches, each at most 64 KiB.

`POST /future/register` requires exactly:

```json
{
  "schemaVersion":1,"runId":"run-1","generation":1,
  "forecastId":"forecast-1","basisSnapshotId":"snapshot-1",
  "basisTickUs":"1000000","horizonEndTickUs":"31000000",
  "rulesetId":"causal-rules","rulesetRevision":"1",
  "readSet":[{"entityId":"staff-7","revision":"3"}],
  "branches":[{"branchId":"branch-1","perspective":{"kind":"actor","actorId":"staff-7","knowledgeRevision":"9"}}]
}
```

It returns `200 {"status":"registered","forecastId":"forecast-1"}`. Register
all branch IDs up front (at most 4). The exact snapshot, ruleset, read-set array
and each branch's perspective kind/owner are bound to subsequent `/future/step`
requests. Virtual actor knowledge revision may increase from its registered
initial value, never decrease below the last dispatched step. After a dispatch,
next depth must increase and step time cannot decrease. These virtual revisions
do not advance live actor knowledge. World perspective uses null actor/knowledge
revision. Actor perspective must name a registered owner and every candidate
must belong to that owner.
The snapshot builder must exclude other actors' private facts and validate
transition IDs, parameter sets, physical bindings and source evidence against
the actual world registry; this broker has no Unity world database and cannot
prove truth from an arbitrary string. No semantic success here grants execution
authority. The consumer rechecks current rules, knowledge and resources before
using a result.

`POST /future/close` takes exactly
`{"schemaVersion":1,"runId":"run-1","generation":1,"forecastId":"forecast-1"}`
and returns `200 {"status":"closed","forecastId":"forecast-1"}`.
Closed IDs cannot be reopened in the same generation. Read-set changes, actor
role/profile/knowledge changes and elapsed horizon also terminate affected
forecasts. Active forecasts are capped at 32, retained forecast terminal
records at 256 per generation. Each branch gets at most 4 dispatches, depth at
most 3, horizon at most 30 simulation seconds. Forecast high-water marks and
actor decision sequences are independent.

### Inference lifecycle, limits, and accounting

`POST /npc/decision` and `POST /future/step` take the approved design wires
unchanged. Success and valid-request unavailable responses conform to their
respective result schemas, including nullable model/choice/probabilities/usage.
Malformed lexical JSON is 400, malformed shape/relationships 422, unauthorized
actor/branch 403, retained-identity conflict 409, expired identity/generation
410, rate limit 429, queue/budget/provider unavailability 503, provider result
failure 502, wall deadline 504. No arbitrary probability normalization occurs.

The parser rejects decoded duplicate keys, invalid UTF-8/BOM, lone surrogate,
nonfinite numeric tokens, trailing material, depth greater than 16, and bodies
larger than 64 KiB. Int64 overflow, invalid time order/expiry, duplicate candidate,
fact, read-set and binding-slot identities, missing cause refs, wrong perspective
owner, unsupported horizon/depth and nonmaximal or incomplete distributions are
rejected separately from schema validation.

* One shared rolling 12 dispatch/second, 12 actual transport flights, pending
  128 and retry 0 covers live NPC, forecast and optional paid dialogue calls.
  FIFO live and forecast classes use live 3:1 minimum service; aged forecast
  traffic cannot take consecutive slots while live traffic waits. Forecasts
  cannot consume the final quarter of pending admission capacity.
* Each actor decision / forecast branch has one upstream flight and at most its
  newest queued replacement. Coalesced duplicates share the same result. New
  relevant state supersedes old caller results without prematurely releasing
  transport capacity. Unchanged NPC context is not repeatedly billed just
  because request ID, sequence or wall time changed.
* Exact body SHA-256 and request ID bind the generation-scoped sequence.
  Keep high-water marks throughout the generation. Retain 1,024 completed
  identities, up to 256 results for at most 60 seconds. Removing a result does
  not remove its retained conflict check; once identity is evicted, old
  sequences return 410 rather than re-infer.
* Caller deadline includes queue time. Transport remains bounded to 15 seconds
  to collect known late billing, while the caller already receives timeout or
  cancellation. Sim expiry is driven by `/session/update`, not wall-time
  extrapolation (pause does not invent simulation time).
* `POST /request/cancel` and `/request/usage` take exactly
  `{schemaVersion:1,runId,generation,requestSha256}`. Cancellation closes only
  the matching consumer request; usage query returns `status:"known"|"unknown"`,
  `requestSha256`, nullable `usage`, and `finished`. A cancelled response uses
  status `cancelled`. Retained old-generation usage remains queryable without
  re-admitting inference. Cache eviction returns 410.

The host-wide exclusive writer and fsynced ledger are
`~/.chooguard/gameplay-broker/{writer.lock,ledger.json}` (on Windows, the user's
home). All routes/provider keys share one writer/account budget. A second
broker fails closed. Ledger contains counts, cost and run-generation fences,
not credentials or private prompts. A crash leaves the lock: an operator must
confirm the recorded process is gone before removing only the stale lock; never
delete the ledger to reset a spent cap. No automatic unsafe lock stealing.

Before each dispatch the ledger durably reserves one request and a conservative
token-cost ceiling (69,632 input + 65,536 output tokens using the explicitly
configured prices); if it cannot fit, no call is made. Known usage adjusts that
reservation exactly once even for cancelled, expired, invalid-result and
old-generation replies. Unknown usage keeps the reservation: it is not free.
Unexpected usage above the reservation or persistence failure halts further
admission. This cannot constrain a provider that changes prices or bills hidden
work beyond its response: the provider's own account spend cap remains required
for an external invoice guarantee.

### Separate optional Korean dialogue

`/dialogue/generate` is **not JEV**. Optional free conversation requires all of:
`CHOOGUARD_DIALOGUE_ENDPOINT` (exact OpenAI-compatible `/chat/completions` URL),
`CHOOGUARD_DIALOGUE_MODEL` (exact returned model ID), `CHOOGUARD_DIALOGUE_KEY`,
`CHOOGUARD_DIALOGUE_INPUT_USD_PER_MILLION`,
`CHOOGUARD_DIALOGUE_OUTPUT_USD_PER_MILLION`, and the shared request/spend caps.
HTTPS is required except explicit `127.0.0.1`/`::1` local providers. No redirects
or implicit vendor/model choices. No config means unavailable, never synthetic
free conversation.

Its exact request fields are
`schemaVersion:1,kind:"dialogue_request",runId,generation,requestId,actorId,
dialogueSeq,mode,basisTickUs,expiresAtTickUs,deadlineMs,roleId,profileRevision,
observationRevision,channel,messages,knownFacts,truthSlots`.
All sequences/ticks/revisions are canonical Int64 strings. Actor identity and
current role/profile/observation registration must match.

* `channel:"free_conversation"`: 1–8 `{role:"user"|"assistant",content}` messages,
  at most 512 code points each, empty truthSlots.
* `knownFacts`: at most 32 `{factId,text,truth,sourceId}` records; text at most
  256 code points; truth uses the four approved RuleTruth strings. Supply only
  the speaking actor's shareable knowledge, not listener-private/world facts.
* `channel:"truth_slots"`: empty messages, 1–8 `{slotId,factId,text}` slots.
  Each slot must reference a supplied known TRUE/FALSE fact with exactly equal
  text; combined output is at most 512 code points. This path returns the
  trusted caller's already-authored fact wording locally, with no model call.
* Free output must have one completed assistant choice, the exact configured
  model, known usage, Korean text at most 512 code points, and no tool calls.
  Truncated, non-Korean, oversized or tool-calling output is unavailable.

Response fields are the correlation identity plus `kind:"dialogue_result"`,
`channel,authority:"text_only",status,provider,model,text,factRefs,usage,
upstreamLatencyMs,reasonCode`. Free success uses provider `openai-compatible`,
reason `generated`, no asserted factRefs; truth-slot success uses
`local-truth-slots`, null model/usage/latency and reason `truth_slots`. Failure
has null text. UI must visibly distinguish free speech from verified slots;
generated text never writes world state or certifies completion/safety.

### Embedding and evidence

Exports: `configuration(env)`, `createGameplayServer({broker,token})`,
`startGameplayBroker(config)` (returns `{server,broker,close}`),
`GameplayBroker`, `AdmissionLedger`, `createValidators`, `parseStrict`,
`createJevProvider`, and `createJsonTransport`. Provider injection and fake
clocks exist only for deterministic boundary tests; the executable exposes no
fake-answer mode.

A no-credential actual localhost smoke was exercised on Node 26.7.0:
unauthenticated status 401; duplicate decoded key 400; session/forecast
registration 200; real NPC/future routes unavailable 503; Korean trusted
truth-slot response 200; **zero paid dispatches**. A separate deterministic
provider-boundary smoke exercised two successive virtual actor steps, unchanged
live decision sequence, simulation-expired live response 410, and late usage
accounting of all three calls exactly once (303 input tokens, unknown count 0);
that smoke made no external calls. The permanent regression suite was authored
but left for the integration owner to execute. Real paid JEV/dialogue calls,
Korean model selection evaluation, C# end-to-end integration and Windows
runtime verification are not claimed by these worker smokes.
