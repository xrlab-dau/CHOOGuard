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
