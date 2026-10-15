# Browser subscription OAuth sidecar

## Active plan
1. Verify current primary protocol and authority boundaries (in progress).
2. Implement isolated loopback HTTP sidecar, private per-registration storage, OIDC/PKCE, consent and budgets.
3. Implement selected-account catalog and strict completed Responses SSE bridge.
4. Run mock OAuth/JWT/network/local HTTP tests; document exact Unity contract and pending live login.

## Authority
Only provider_auth/* owned. No existing CLI credentials, reused client IDs, C#/Unity/shared config, real login or inference. No nested agents. Planning-with-files active task restored; this file records this branch without changing active-plan pointer.

## Verified primary evidence
Downloaded current official sign-in.md, models-and-inference.md, token-reference.md, profiles-and-sessions.md, errors-and-recovery.md from https://developers.openai.com/siwc/token-sharing-open-source/ .
First authorization dynamic_agent_client with stable host ID+CHOOGuard agent_name_hint; callback-issued actual client ID for exchange and later login. Identity validation must verify signature, iss/aud/exp/nonce and returning subject. Direct permission scope required. Responses public endpoint store=false/stream=true and response.completed mandatory. Catalog models order/visibility list. Serialized rotating refresh with resource and no scope. Logout revocation discovery endpoint before local clear; preserve registration mapping. Usage-limit errors stop requests, no billing or account bypass.

## Completed implementation
- Python stdlib loopback HTTP sidecar + maintained PyJWT2.15.1/cryptography46.0.7 signature verification. Own app directory only, atomic0600 files/0700 directory, one-process lock, stable host and per-registration identities; no other credential reads.
- Dynamic public-client registration, state/nonce/PKCE and identity verification; exact issued-client exchange/relogin, scoped permission gate, private rotating refresh and revocation.
- Separate ephemeral native capability and browser fragment→HttpOnlySameSiteStrict session; Host/Origin/Sec-Fetch checks with state-protected cross-site OAuth callback exception. Native session-generation header guards account/model changes before and after refresh.
- Selected-account visibility-list model ordering, explicit browser cloud choice, friendly account/model/usage/remaining allowance UI, logout/settings and bounded retry controls.
- Public Responses bridge requires store=false/stream=true and response.completed. Late failure/incomplete/truncation rejects all partial output. Safe error codes/status/body shape/param/request IDs preserved without raw diagnostics/tokens.
- Native C# response_format=json_object and source seed accepted/validated; provider seed/temperature/ChatCompletions-specific format omitted upstream, final JSON-object contract checked. Seeded prompt/tape remains reproduction authority.
- Ordinary12request/12kinput/3koutput budget; separately explicit one initial generation allowance default192k conservative input/2048output, maxima256k/4096, hard24total attempts. Full current root-measured153812unit request fits only explicit initial allowance; no object truncation. Missing usage on failed streams charged separately from measured actual tokens.
-429quota stop persists and cannot be bypassed by account changes; user can reviewusage then explicit same-registrationretry. Temporary503 uses bounded15–120s backoff. No API billing fallback/extra-credit automation/Claude subscription route.

## Review corrections and errors
- System pip TLS verification failed; did not disable TLS. Downloaded current PyJWT wheel through verified urllib HTTPS/PyPI JSON and verified SHA256 before task-local extraction; reference/dependency-receipt.json records it.
- Initial broad patch context mismatch caused no file mutation; corrected smaller exact patch.
- Independent review closed captured-model/refresh cross-account race, repeated browser bootstrap, C# response_format/seed compatibility, and user-facing raw status dump.
- Node syntax check caught Python triple-string newline escaping in browser join; replaced with String.fromCharCode(10), added actual rendered-script node--check regression.
- Independent P2 review closed late terminal-refresh/logout clearing newer reauthorization; both compare attempted token snapshot and have deterministic reentrant mock tests. Refresh success cannot resurrect concurrent logout.

## Final verification
- `python3 workers/simulator/provider_auth/test_sidecar.py`: PASS50 tests,6.928s, exit0, source_changed_during_run=false.
- Receipt: provider_auth/test-report.json. Sidecar SHA256 7692757191ec62f9ba3fcda802126f80f01ef39274885a6336aee280134bc7a6.
- Actual ephemeral HTTP servers tested capability/Host/Origin, malformed JSON, cookie bootstrap/reopen, cross-site callback state protection, completed bridge response and old-generation409 before network. Browser script parses and page makes no provider requests on open.
- Live provider login=false, live provider inference=false, existing CLI credentials read=false. Mock credentials existed only in private temporary test directories and were deleted. No live sidecar/browser auth process remains.
- README contains ready-to-run commands, exact native/browser contract, explicit budgets, source links and pending actual user-login/inference limits. Root owns C# integration and copy of receipt to output/simulator/provider-auth-tests.json.

## Phase status
1. Primary protocol/authority verification: complete.
2. Isolated OAuth/storage/UI implementation: complete.
3. Catalog/Responses/budgets implementation: complete.
4. Mock protocol/actual localHTTP tests and documentation: complete.

Actual end-user sign-in, account eligibility, live catalog and completed Responses execution remain separate pending external verification. This branch stops after integrated deliverable; no child agents.

## 2026-10-14 actual acceptance continuation

- Repaired stale initial consent: every session generation rotation invalidates the initial allowance. Explicit arming occurs after rotation and requires a selected model. Account/model/login/cloud changes cannot inherit a previous one-shot allowance.
- Preserve selected catalog slug and optional provider-returned model separately; do not infer returned identity from the request. Added distinct-model synthetic regression.
- Graceful SIGTERM now runs descriptor deletion and releases the app lock; it retains registrations.
- Current provider_auth receipt: 53 tests pass, source_changed_during_run=false. New provider_runtime receipt: 11 actual localhost/synthetic tests pass, including protected descriptor, no-network readiness, one attempt, stale generation, negative proposal contract, secret-free outputs and scoped PID cleanup.
- Live helper started through runtime owner PID 98451; this supersedes the earlier no-live-process operational note. It remains signed out, cloud disabled, allowance unarmed and measured usage zero. No actual OAuth/provider inference performed. The root owns eventual stop through provider_runtime/session.py stop.
- Actual 961-entity/456-visual request tested unchanged through synthetic localhost Responses only, 166129 bytes and 166779 conservative input reservation units. This is not real-provider inference evidence.
- Both Python browser-open and direct macOS /usr/bin/open failed in this tool environment. Safe GUI handoff is provider_runtime/open-provider-ui.command or session.py open; private bootstrap values are loaded internally and never printed. Root graphics lane owns any GUI environment recovery.

## 2026-10-14 multi-provider implementation freeze

Production entrypoint now uses isolated provider account views, unified account-entitled catalog and one mandatory eight-field provider/account/model selection for both initial causal generation and NPC plans. No model/API-key/other-account fallback. Nonselected logins/catalog/logout preserve current play selection; stale success/failure normalizes to HTTP409. Device polling commits account auth only and cannot resurrect old consent or logged-out credentials.

OpenAI real prior one-call evidence remains historical and existing PID357 was not restarted. GitHub Copilot owns a public-client device flow and model-only SDK adapter; app operator registration is required, end users then only browser-login. Official SDK1.0.22rc1 wheel and1.0.98-1 runtime installed locally, reviewed source critical files match, native handshake passed with network denied and zero login/model requests. Anthropic policy restriction and remaining unsupported provider contract requirements are explicit.

Latest verification: legacy55, multi-provider17, Copilot13, opt-in temporary-helper3, UI Node DOM1 passed. Source changes during base regression false. SDK source installation initially failed due pip native trust-store isolation; resolved by using the official hash-pinned published wheel and existing compatible dependencies without networked build isolation, retaining TLS verification. Native headless desktop browser acceptance and real GitHub subscription inference remain unverified.

Opt-in launcher only stops a verified app-runtime-owned PID; account/browser files byte-exact in temporary fixture. Existing357, original Unity, other application credentials unchanged. No actual model request was sent in this worker lane.
