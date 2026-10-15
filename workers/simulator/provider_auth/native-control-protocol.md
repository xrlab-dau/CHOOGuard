# Unity FPS provider-menu control API

The product's provider/account/model/settings UI is the Unity FPS menu. The browser only performs the selected provider's OAuth authorization or device verification. The existing local HTML settings page remains a diagnostic/compatibility surface; it is not the product menu. Native OAuth callbacks display only a short game-return page, including cancellation/failure.

## Boundary

All `/native/v1/*` routes require this app's protected loopback native Bearer capability. Browser cookies alone never authorize these routes. The same capability is intentionally used by the first-party Unity process for inference and local account/settings control: it proves access to this app's private `0600` descriptor, not possession of a provider token. This adds no remotely reachable management service. Exact `127.0.0.1` listener/Host, Origin, Fetch-Site, no-redirect and request-body guards remain enforced. Provider access/refresh/ID tokens, device code, PKCE verifier and raw OAuth callback fields remain helper-internal.

A current `X-ChooGuard-Session` is mandatory for mutations and flow polling. Stale/missing generations return HTTP409 before applying the operation. `GET /native/v1/status` is read-only and does not require that header. Login first completing or reauthorizing the selected account can rotate the generation; the host should refresh native status and retry the same flow handle on 409, without starting login again. The host retains the immutable eight-field model selection for play and blocks settings changes while playing.

## Common response

Every successful route returns:

```json
{
  "schema":"native-provider-control-v1",
  "status":{"providers":[],"accounts":[],"selection":null,"session_generation":"...","cloud_enabled":false},
  "models":[]
}
```

`status` is the existing full multi-provider status DTO, including registered/unsupported provider reasons, account login/catalog/inference states, the exact eight-field `selection`, budgets, usage and one-shot allowance. The wire compatibility field `cloud_enabled` means explicit subscription use; the product UI labels it as subscription use. `models` is the union of cached account-entitled catalog rows with provider/account/model identity and catalog SHA; equal model IDs remain separate choices. Status polling never invokes model/catalog/OAuth requests or consumes an initial allowance.

| Method and path | Request | Behavior |
| --- | --- | --- |
| `GET /native/v1/status` | None | Cached provider/account/model/status snapshot only. |
| `POST /native/v1/catalog` | `{}` | Explicitly refresh model catalogs for signed-in accounts. Never inference. |
| `POST /native/v1/auth/start` | `{provider_id,account_id?:string or null,reconsent?:bool}` | Start explicitly chosen provider's own-app OAuth/device flow. No default provider fallback. |
| `GET /native/v1/auth/flows/{flow_id}` | None | Browser-flow status or one bounded device poll; respects provider polling interval. |
| `DELETE /native/v1/auth/flows/{flow_id}` | `{}` or empty | Cancel exactly that pending flow; other login flows and active play selection remain. |
| `POST /native/v1/selection` | `{provider_id,account_id,model_id,catalog_version}` | Atomically select one catalog-bound model, with subscription use disabled. Selecting a model never grants usage consent. |
| `DELETE /native/v1/selection` | `{}` or empty | Clear model selection and usage consent/initial allowance, retaining logged-in accounts. |
| `POST /native/v1/consent` | `{subscription_enabled:bool,initial_world_allowance?:{input_tokens,output_tokens},resume_after_usage_review?:bool}` | Explicit user consent or single-use initial-generation allowance; does not itself generate. A selected model is required to enable usage. |
| `POST /native/v1/logout` | `{account_id}` | Logout that account. Returns additional `logout:{local_session_cleared,remote_revocation_confirmed}`. Other accounts stay logged in. |

Unexpected fields, API keys, invalid account/provider combinations, noncatalog models and stale catalog SHA are rejected. A native route never automatically logs into a default provider, chooses another model/account, or falls back to developer API billing.

## Flow DTO

Start/poll/cancel responses add:

```json
{
  "flow":{
    "flow_id":"<opaque ephemeral handle>",
    "provider_id":"github-copilot",
    "status":"waiting_for_device",
    "expires_in_seconds":900,
    "poll_after_ms":5000,
    "error_code":null,
    "account_id":null,
    "verification_uri":"https://github.com/login/device",
    "user_code":"ABCD-EFGH"
  }
}
```

Browser OAuth start returns `authorization_url` instead of `verification_uri`/`user_code`. These launch/display fields appear only on the start response. Authorization URLs must use the exact registry HTTPS provider origin and authorization path; browser OAuth callback URI and S256 challenge must match this helper's stored flow. Device verification URLs cannot contain arbitrary query redirects. The `flow_id` differs from provider OAuth state and device code. Flow polls never return the device code, OAuth state, nonce, PKCE verifier or token set.

Flow `status` is one of `waiting_for_browser`, `waiting_for_device`, `completed`, `cancelled`, `expired`, `failed`. `account_id` is an opaque app registration handle, present after completion when unambiguous. The host uses the common status account list regardless. Polling stops on terminal states. Cancelling a flow prevents late callbacks or device polls from resurrecting credentials. Native callback pages expose no settings controls.

## Inference remains separate

Control routes never call `/v1/responses`, the Copilot model SDK, or `/v1/chat/completions`. Inference still requires the native capability, current generation, **exact eight-field `chooguard_selection`**, selected model, explicit usage consent and applicable budgets. Initial and NPC requests use the same tuple. Arming an initial allowance does not consume it; only an admitted inference attempt does. External output replay rules remain unchanged.

## Verification / deployment

`test_native_control.py` exercises actual ephemeral localhost HTTP servers and synthetic OAuth/JWT/device/catalog transports. It covers capability-vs-cookie scope, nonloopback/Host/Origin/Fetch-Site refusal, generation CAS, HTTPS/path/callback/PKCE validation, two-provider catalog collisions, explicit choice/consent, targeted logout/clear/cancel, expiry, late callback/device cancellation, cached status and unconsumed allowances.

The native-control source freeze is recorded separately in `native-control-implementation-receipt.json`. Implementing these routes does not hot-reload the running helper. Root authorizes deployment only after Unity/native integration review; no real login, provider inference or Unity execution is part of this auth lane's verification.
