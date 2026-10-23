# CHOOGuard end-user subscription model connection

The Unity FPS menu lets each end user choose providers, inspect signed-in accounts, load the union of their entitled models and select **one provider/account/model for play**. The local helper exposes the authenticated [native control API](native-control-protocol.md); the browser is used only to complete provider OAuth or device verification. The same immutable selection serves initial causal-condition generation and NPC planning. There is no developer LLM API key, API-billing fallback, automatic account cycling, or automatic model/provider rotation. Local world ticks and object manipulation do not call a model.

A successful login does not establish model entitlement or available subscription quota. The UI reports account login, catalog, permission and inference states separately. Account catalogs come from that account's provider; model names are not a hardcoded access list.

## Current support and evidence

| Provider | Browser login and inference | Required configuration / boundary |
| --- | --- | --- |
| OpenAI / ChatGPT | Implemented own-app public dynamic OAuth, issued client ID, PKCE/OIDC, account catalog and public Responses subscription route. | Existing actual end-user call used **36,145 input / 53 output tokens** and returned `changes:[]`; empty initial conditions are valid. This previous single-provider receipt is separate from the new multi-provider mock verification. |
| GitHub Copilot | Implemented this app's own public-client device OAuth, `/user` identity verification, SDK account model catalog and tool-free selected-model inference. | Requires **CHOOGuard's own device-enabled GitHub OAuth App public Client ID**. This public application identifier is not an LLM API key. No app secret, PAT, another harness's Client ID, or CLI login is accepted. Exact SDK/runtime are installed locally and the network-denied native handshake passes. Actual user login, subscription catalog and inference have not been performed in this lane. |
| Claude / Anthropic | Direct Claude.ai subscription login is unavailable in this third-party adapter. | Current [Anthropic policy](https://code.claude.com/docs/en/legal-and-compliance) prohibits third-party subscription credential routing; the unmodified Claude Code exception does not authorize this adapter. No API-key fallback. |
| Google Gemini CLI / Antigravity | Visible as unavailable for the requested mode. | Needs an officially permitted own-app OAuth registration, Code Assist/project/quota contract, account-entitled catalog and compatible subscription inference adapter. A harness's embedded client ID is not an app registration. |
| Amazon Q, Kimi, Qwen, MiniMax | Visible as unavailable for the requested mode. | Needs provider permission for this app's public OAuth/device registration, entitlement/catalog, refresh and no-extra-API-billing inference contract. Successful harness login alone is insufficient. |
| Prime Intellect / OpenRouter | Unavailable for subscription-only mode under the verified current contracts. | An API-credit account or model API key does not satisfy this requirement. A separately permitted end-user subscription contract would need its own adapter. |

This is a pluggable registry with truthful support states, **not a claim that every provider or every advertised model is usable**. Copilot policy-disabled models and the `auto` router are shown as unavailable. Provider model metadata can expose subscription usage multipliers; provider subscription limits still apply. The app never changes an account's paid-usage settings.

## Helper lifecycle and diagnostic web page

Install [requirements.txt](requirements.txt) in a task-local virtual environment. The
`cryptography` pin includes the fixes for the four dependency alerts affecting the
previous 46.0.7 pin; this does not establish that those vulnerable APIs were reachable
through this helper. See the [upstream changes](https://cryptography.io/en/50.0.2/changelog/).
The helper still requires POSIX `fcntl`; native Windows execution is not supported.
Upstream 49+ also removed Intel macOS and 32-bit Windows support, so use a supported
64-bit Linux or Apple Silicon macOS environment for this helper.

CI installs the same runtime pins through the hash-locked
[provider-auth requirements](../../../.github/requirements/provider-auth.txt) in an
isolated environment and runs the existing mock OAuth/JWT, multi-provider, localhost
native-control and UI tests. These tests use synthetic accounts and transports;
they do not log in to a provider or consume model quota.

Deployment state is recorded in the dated [activation receipt](desktop-upgrade-activation.json). On 2026-10-14 the user explicitly requested execution, and the app-owned helper was upgraded to the multi-provider implementation while retaining its existing account file bytes. The current model selection and subscription consent are empty until the user explicitly chooses them. Service activation and automatic desktop browser opening are separate results; see that receipt for the macOS browser-opening limitation.

For an isolated fresh app-private directory and port, with no existing credentials:

```bash
python3 workers/simulator/provider_auth/sidecar.py \
  --private-dir /tmp/chooguard-provider-demo-private --port 18954 --open-browser
```

Do not use that example directory for long-term credentials. The normal protected directory is `~/.local/share/chooguard/provider/`, outside the repository, `0700` with atomic `0600` files. One helper holds an exclusive store lock. No OMP, pi, Prime Agent, Codex, Claude or desktop-app credential file is read.

To explicitly upgrade the existing **app-owned** helper on the desktop, open [Upgrade-and-Open-Providers.command](Upgrade-and-Open-Providers.command). This launcher is opt-in: it checks the exact owned PID's creation/command identity, gracefully stops only that helper, starts the new implementation, verifies its loaded source stamp, and opens its diagnostic browser page. The product settings remain inside the Unity FPS menu. It refuses an unowned process. It preserves account and browser-session file bytes. It makes no OAuth or model request and does not start Unity.

Equivalent commands:

```bash
# Read-only inspection; no restart.
python3 workers/simulator/provider_auth/upgrade_helper.py

# Explicit opt-in restart and browser opening.
python3 workers/simulator/provider_auth/upgrade_helper.py --apply --open-browser
```

The prior play selection and one-shot allowance are memory-bound, so an upgrade requires **selecting the model and explicitly allowing subscription use again**. It never recreates the consumed initial-generation allowance. Existing OAuth registrations remain; a provider can still require reauthorization if its own token has expired or been revoked. Old native clients without the eight-field selection contract cannot use the new helper; use the updated simulator module together with it.

## Configure this app's GitHub Copilot connection

Follow the official [GitHub OAuth setup for Copilot SDK](https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/github-oauth) and [OAuth App device-flow documentation](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow). The app operator registers a GitHub OAuth App for CHOOGuard once and enables its device flow. This is deployment setup; end users do not create OAuth apps or supply developer keys. Once configured, end users only choose the provider and sign in. Configure the app's public Client ID, without embedding a Client Secret:

```bash
python3 workers/simulator/provider_auth/upgrade_helper.py --apply --open-browser \
  --github-client-id YOUR_CHOOGUARD_PUBLIC_OAUTH_APP_CLIENT_ID
```

This explicit action stores only that public registration in the app-private `provider-registrations.json`. A fresh helper also accepts `--github-client-id`. Without registration, the UI shows why Copilot cannot be selected. The end user chooses GitHub Copilot, opens GitHub's verification page, enters the displayed short code and authorizes their own account. The device code stays private in the helper. Token polling respects interval/slow-down/expiry; every token issuance verifies `/user`. Expiring device tokens refresh without an app secret, and refresh/logout races cannot resurrect old tokens.

The helper passes only this app's user token to `CopilotClient(github_token=..., use_logged_in_user=False)`. The runtime uses its own private base directory, a clean explicit process environment, empty mode and no keychain probe. SDK sessions have empty tool and MCP allowlists, deny all permission requests, and disable skills, config discovery, hooks, host Git operations, session stores and memory. The selected model is explicit; `auto` and BYOK paths are refused. All usage records must identify that exact selected model. No coding harness tools are exposed to NPCs.

Copilot logout clears local credentials without retaining an app secret. Remote OAuth revocation cannot be confirmed through the secret-requiring application API; the UI directs the user to GitHub's connection management if desired.

## Exact local SDK/runtime installation

The reviewed source commit is `github/copilot-sdk@810c483ae426344c2a5fee61025f9e3bd457cca9`. The official published `github-copilot-sdk==1.0.22rc1` wheel has SHA256 `ef122dd8385a1d8b8d62797afabd72f9ec0235564a48be76d3dac1c77d8debc7`; its `client.py`, `session.py` and protocol module exactly match that source. The wheel pins official runtime `1.0.98-1`. The SDK downloader verifies the release checksum before staging the runtime locally.

```bash
python3 workers/simulator/provider_auth/install_copilot_sdk.py --download-runtime
```

This installs only task-local `.sdk-deps/` and `.sdk-runtime/`, without login or runtime execution. Compatible `pydantic>=2.11`, `httpx>=0.24.0`, `python-dateutil>=2.9.0.post0`, setuptools/wheel and the auth [requirements](requirements.txt) are needed in the selected Python environment. The installer reports missing dependencies rather than disabling TLS verification or silently replacing global packages. Source/native hashes are in `sdk-install-receipt.json`. Startup rejects a global SDK fallback, missing/mutated SDK files, or an unverified/missing runtime.

`copilot-native-probe.json` records a real native handshake/Ping with `sandbox-exec deny network*`, empty mode, no token, `use_logged_in_user=False`, zero login/model requests, and no `HOME` override. It proves the installed runtime starts locally; it does not prove actual Copilot account entitlement or model inference.

## Native contract and budgets

The protected `connection.json` descriptor remains schema1 with an ephemeral local native capability and a separate browser bootstrap. OAuth access/refresh/ID tokens never leave the helper. Browser cookies cannot invoke inference. Host/Origin/SameSite/CSP/PKCE/OIDC guards remain enforced.

The new [protocol](protocol.md) requires `X-ChooGuard-Session` and the exact eight-field `chooguard_selection` for **both** `initial_world` and `npc_plan`. Changing the selected provider/account/model, removing its catalog permission, or logging it out invalidates old requests and results with HTTP409. Logging into or out of unrelated accounts preserves the current selection and consent. Changing play models requires stopping the old play session and starting a new one; the app never quietly continues NPC requests under a different model.

Ordinary requests default to 12 attempts, 12,000 conservative input reservation units and 3,000 output tokens, bounded by 24/24,000/6,000. The initial world uses a separate one-shot allowance, visibly defaulting to 1,048,576 input reservation units / 2,048 output tokens, capped at 1,048,576/4,096; the installed Unity initial generator requests at most512 output tokens. Failed attempts consume reservations and the single-use allowance. Actual measured token usage is separate from admission estimates. A hard 24-attempt cap applies across all providers and both purposes in a helper process. Quota failures stop that account and never trigger automatic fallback.

World objects and NPCs remain the generation input. Models may propose bounded upstream conditions and typed NPC plans; the seeded causal kernel, action guards, replay records and navigation remain authoritative. An external model's outputs are not guaranteed deterministic from a seed alone, so replay retains the accepted outputs.

## Verification boundary

- Existing ChatGPT actual subscription receipt: `output/simulator/provider-live/final/subscription-live.json`; accepted no-change native application and 40-tick local replay are separate receipts.
- New multi-provider tests use two independent mock OAuth transports, colliding model names, exact selected routing for initial generation and NPC planning, stale in-flight success/failure rejection, account isolation, catalogs, quota and missing tuple refusal.
- Copilot tests verify official installed SDK signatures, tool-free session arguments, exact usage model, device polling, refresh, logout and concurrent consent changes. Native handshake is network-denied. Actual GitHub login/inference remains unverified.
- Temporary-directory helper launch/upgrade tests preserve fake account files and require no real provider access. The original implementation tests did not restart the prior helper; the later user-authorized deployment is recorded separately in `desktop-upgrade-activation.json`.
- UI script syntax/DOM-harness checks are local verification; they are not a native desktop browser acceptance claim. No Unity execution is part of this work.

## Primary sources

- [OpenAI own-app public sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in), [account models and inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference), [token refresh](https://developers.openai.com/siwc/token-sharing-open-source/token-reference).
- [GitHub Copilot SDK user authentication](https://docs.github.com/en/copilot/how-tos/copilot-sdk/auth/authenticate), [own OAuth app setup](https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/github-oauth), [public-client device authorization](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow).
- [Reviewed Copilot SDK source](https://github.com/github/copilot-sdk/tree/810c483ae426344c2a5fee61025f9e3bd457cca9), [pinned published distribution](https://pypi.org/project/github-copilot-sdk/1.0.22rc1/).
- [Anthropic legal and compliance](https://code.claude.com/docs/en/legal-and-compliance).
