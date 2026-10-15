# Errors and recovery

> For the complete documentation index, see [llms.txt](/llms.txt). Markdown versions of documentation pages are available by appending `.md` to the page URL.

## ChatGPT plan use isn’t enabled

This branch applies when consent is declined or the OAuth result lacks permission to use the user’s ChatGPT plan. Do not proceed to inference on this path.

After a successful OAuth exchange, check the returned scopes for `chatgpt.tokens.use.direct`. If the result contains a valid ID token without that permission, retain the sign-in but mark ChatGPT plan usage as disabled. A coding harness still needs a way to pay for inference, so offer a clear choice: enable ChatGPT plan usage or configure another supported billing path, such as the user’s own API key.

When the user explicitly chooses to enable ChatGPT plan usage from your tool’s settings, including after an earlier decline, repeat OAuth with the saved client ID and the **complete requested scope set**, explicitly including `chatgpt.tokens.use.direct`. Request consent with `force_reconsent=true` only after OpenAI confirms deployment for your integration; the existing OAuth `prompt=consent` mechanism remains supported before that rollout. Your app should not force consent on every ordinary sign-in. Recheck the granted scopes afterward. These are OAuth authorization parameters, not the unsupported Responses request-body `prompt` field.

## Errors and connection recovery

ChatGPT plan usage errors stop inference. OpenAI does not silently switch the request to another billing path. Preserve the actual HTTP status, response body shape, error code when present, and request ID.

### Before a stream opens

Do not assume every failure has the standard API `error` object. Direct-route admission can return a body such as `{"detail":"..."}` before a Responses request starts. Treat the message as diagnostic text, not a stable machine-readable code.

| Direct-admission status | Recovery                                                                                                                                     |
| ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `401`                   | The required signed identity or direct permission was not accepted. Check the selected ChatGPT account and granted scopes.                   |
| `403`                   | A policy or permission check, such as the permitted serving region, prevented admission. Surface the restriction and verify the integration. |
| `503`                   | Direct routing is unavailable or not enabled. Preserve credentials and use bounded backoff for temporary failures.                           |

### Structured Responses errors

When a Responses-layer error object is returned, use its exact `error.code` and `error.param`. The following codes have specific recovery actions; they are not an exhaustive list of authentication, routing, or transport failures.

| Returned code                                                                     | HTTP status | What to do                                                                                                                                                                                                                                                                                                    |
| --------------------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `subscription_sharing_user_not_eligible`                                          | 403         | ChatGPT plan usage is unavailable for the selected user, workspace, or policy. Explain the restriction; do not repeat the same request or loop through OAuth.                                                                                                                                                 |
| `subscription_sharing_usage_limit_exceeded`                                       | 429         | Pause new requests that use the user’s ChatGPT plan and link to [ChatGPT settings → Usage](https://chatgpt.com/settings/usage). Do not assume the entire plan is empty or infer a reset time from this code alone; an app-specific limit can also apply. |
| `subscription_sharing_usage_unavailable`                                          | 503         | Usage availability could not be checked. Preserve credentials and retry later with bounded backoff.                                                                                                                                                                                                           |
| `subscription_sharing_unsupported_capability`                                     | 400         | Inspect `error.param` and remove the unsupported input, tool, execution feature, model, or service-tier override. Do not retry the same invalid body.                                                                                                                                                         |
| `subscription_sharing_route_not_supported`                                        | 403         | Check the exact HTTP method and endpoint. The examples in [Models and inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference) use `POST /v1/responses`; support for another client type or route is not permission to use it here.                                                                   |
| `subscription_sharing_invalid_user`                                               | 401         | The subscriber context could not be validated. Preserve the request ID and diagnose the credential failure. Ask the user to sign in again after confirmed revocation or a terminal refresh error.                                                                                                             |
| `chatpass_v2_scope_not_authorized` or `chatpass_v2_invalid_authorization_context` | 403         | The signed permission context does not authorize the operation. Check the client and grant configuration rather than retrying or changing billing.                                                                                                                                                            |
| `subscription_sharing_user_unavailable`                                           | 503         | User/workspace information is temporarily unavailable. Preserve credentials and retry with bounded backoff.                                                                                                                                                                                                   |

### Disconnection

OpenAI does not currently notify your tool when a user disconnects the app in ChatGPT settings. When a request or refresh confirms the disconnection, stop using the invalid token set and ask the user to sign in again. Do not erase credentials solely because of a temporary network or infrastructure failure.

## Refresh errors

Handle errors by their machine-readable code:

- **Unusable refresh token:** `invalid_grant` on refresh, `invalid_refresh_token`, `token_expired`, `refresh_token_expired`, `refresh_token_invalidated`, or `refresh_token_reused`. Clear unusable tokens and repeat OAuth with the saved issued client ID.
- **Invalid client:** `invalid_client`. Fix the client configuration.

Continue to [Preview limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations).