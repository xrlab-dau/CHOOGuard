# Actual subscription acceptance

These tools own only the CHOOGuard app's local sidecar lifecycle and use its protected `connection.json`. They never read `accounts.json`, API keys, environment credentials, Codex/OMP/pi auth or other apps' storage. Starting, opening and inspecting the helper do not start OAuth, load remote models or perform inference.

From the repository root:

```bash
python3 workers/simulator/provider_runtime/session.py start
python3 workers/simulator/provider_runtime/session.py open
python3 workers/simulator/provider_runtime/session.py status
```

On macOS, double-click `open-provider-ui.command` to reopen the local page. The launcher reads the private browser bootstrap internally and never prints it. The safe visible address is `http://127.0.0.1:18953/`; a fresh browser needs the launcher to establish its session. Browser launch success means the OS accepted the open request, not that a person finished login.

In that page the end user must separately:

1. Choose **ChatGPT로 계속** and finish the provider's sign-in and permission page.
2. Select their saved registration, check **구독 모델 사용** and press **구독 사용 선택 적용**.
3. Press **구독 계정의 모델 불러오기**, select a model and press **선택한 모델 사용**.
4. Review the one-initial-world limits and press **구독으로 초기 월드 생성 1회 허용**.

Login alone leaves subscription model use disabled. The internal `cloud_enabled` field means remote use of the signed-in ChatGPT subscription; it never selects API-key billing. Changing account, selected model, login or cloud state invalidates the one-shot allowance. A new allowance requires another explicit browser click. No runner can enable these choices: its only local routes are `GET /status` and the single native inference route.

Before any inference, inspect the unmodified whole-world request exported by `EmergenceComposer.CreateRequest`:

```bash
python3 workers/simulator/provider_runtime/acceptance.py inspect \
  --request output/simulator/provider-live/whole-world-request.json \
  --report workers/simulator/provider_runtime/receipts/preflight.json
```

`inspect` is local and never consumes the allowance. With all browser choices complete, explicitly run one acceptance attempt:

```bash
python3 workers/simulator/provider_runtime/acceptance.py run-once \
  --request output/simulator/provider-live/whole-world-request.json \
  --report workers/simulator/provider_runtime/receipts/live-acceptance.json
```

There is one native POST, no retry, account/model cycling or key fallback. The runner transmits every graph entity and visual mapping in the source request unchanged. It captures the account/model session generation internally, checks its freshness in the sidecar and after completion, and requires the browser-armed allowance to be consumed exactly once. Native output remains capped at 512 tokens to match the current C# emergence provider, even if the browser's separate allowance is larger.

The safe report records source/body/response/proposal SHA256 values, seed, basis digest, graph counts, selected model, provider-returned model when present, measured token usage and latency. It excludes account labels/IDs, capabilities, browser fragments, tokens and remote diagnostics. The selected model slug and provider-returned model are recorded separately because a provider may resolve a slug to a dated model. An absent provider model is reported as absent, not inferred.

Successful responses must have the exact basis digest and decimal seed, at most 16 unique changes, semantic targets, current property/tag rules, finite numeric values and allowed bounds. Unsupported fields, derived outcomes, untyped targets, stale sessions, incomplete streams and usage/allowance mismatch fail the attempt. Quiescence (`changes:[]`) is valid. The exported `.completion.json` contains only model, usage and the validated proposal fields; free-text reasons and private generation metadata are omitted. Native C# atomic application and exact post-state/replay verification remain separate requirements. The report never claims they passed.

Cleanup is scoped to the helper PID and its recorded process-creation identity:

```bash
python3 workers/simulator/provider_runtime/session.py stop
```

An externally started helper is retained, and a missing/reused PID is never signaled. Graceful stop deletes the ephemeral descriptor and preserves app registrations. `--private-dir` exists for isolated local installations/tests and must be outside this repository with owner-only permissions. Helpers do not download dependencies at startup.

Synthetic verification:

```bash
python3 workers/simulator/provider_auth/test_sidecar.py
python3 workers/simulator/provider_runtime/test_runtime.py
```

The current runtime tests use actual local HTTP and a scoped subprocess, synthetic RSA/OAuth/catalog/Responses, negative proposal fixtures, local consent boundaries, stale-session guards, safe reports, protected files and PID cleanup. Synthetic success is explicitly labeled and cannot become live-inference evidence.

CI runs this suite with `python -m unittest -v test_runtime` from this directory,
using the isolated, hash-locked provider-auth environment. It also runs
`test_upgrade_helper` from `provider_auth`. Module invocation does not overwrite the
local `test-report.json`; these tests only manage their own temporary helpers and
never upgrade an installed user session.

The reader also accepts the lossless `prefix-column-v1` graph: UTF-16 ordinal prefix identities, exact group columns, integer numeric columns divided by 10000, nullable parent references and shared identity references for links/visuals. It validates the complete entity/visual counts after decoding and retains the original compact JSON unchanged in the outgoing user message. Proposals may contain exactly one of `entity_ref` or `entity_id`; references are resolved before type/range/duplicate validation. Exported completion changes use canonical `entity_id` values for replay compatibility. Unknown or untyped references cannot create hazards. Legacy graph tests remain active.

Actual acceptance receipts include the protected process launch record's active sidecar hash separately from current disk source hashes. A pending UI fix on disk does not imply a running helper loaded that fix. Do not restart a helper with a user's pending one-shot allowance just to deploy UI changes; preserve the live session until that action is finished or explicitly cancelled.
