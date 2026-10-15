# Token reference

> For the complete documentation index, see [llms.txt](/llms.txt). Markdown versions of documentation pages are available by appending `.md` to the page URL.

## Token response and refresh tokens

With `offline_access` granted, the authorization-code exchange returns a `refresh_token` alongside the `access_token`. The token response includes `access_token`, `refresh_token`, `id_token`, `token_type`, `expires_in`, `scope`, and `earliest_refresh_at`. A successful code exchange returns HTTP `200`. A successful `grant_type=refresh_token` exchange also returns HTTP `200`, with a new access token and a replacement refresh token.

Read and save the `refresh_token` field from the token-endpoint response so your app can renew access. The refresh token is separate from the access-token claims and does not appear in the browser callback.

## Token lifetimes

Access tokens are valid for one hour (`expires_in: 3600`).

Refresh tokens are valid for 30 days. Each successful refresh returns a replacement refresh token with a fresh 30-day lifetime. Successive replacements have no fixed limit while each token remains valid.

Follow [Refreshing tokens](https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions#refreshing-tokens) to replace credentials safely.

## Access-token claims

The following reference describes the 10 top-level fields and two nested fields in the access-token payload. The example uses placeholders for account-specific and token-specific values.

| Field                                                        | Type    | Meaning                                                       |
| ------------------------------------------------------------ | ------- | ------------------------------------------------------------- |
| `sub`                                                        | string  | Opaque subject identifier.                                    |
| `aud`                                                        | string  | Resource audience; `https://api.openai.com/v1` in this flow.  |
| `client_id`                                                  | string  | Issued OAuth client ID associated with the token.             |
| `scope`                                                      | string  | Space-separated scopes granted to this token.                 |
| `https://api.openai.com/auth`                                | object  | OpenAI-managed authentication metadata.                       |
| `["https://api.openai.com/auth"]["per_user_salt"]`           | string  | Opaque salt value within the authentication metadata object.  |
| `["https://api.openai.com/auth"]["encrypted_auth_metadata"]` | string  | Opaque encrypted authentication metadata for OpenAI services. |
| `iss`                                                        | string  | Token issuer; `https://auth.openai.com` in production.        |
| `iat`                                                        | integer | Issued-at time in Unix seconds.                               |
| `exp`                                                        | integer | Expiration time in Unix seconds.                              |
| `jti`                                                        | string  | Token identifier.                                             |
| `nbf`                                                        | integer | Not-before time in Unix seconds.                              |

The two nested field paths above refer to keys inside the `https://api.openai.com/auth` object.

```json
{
  "sub": "<SUBJECT_IDENTIFIER>",
  "aud": "https://api.openai.com/v1",
  "client_id": "<ISSUED_CLIENT_ID>",
  "scope": "chatgpt.tokens.use.direct email offline_access openid profile resource.invoke",
  "https://api.openai.com/auth": {
    "per_user_salt": "<OPAQUE_SALT>",
    "encrypted_auth_metadata": "<OPAQUE_ENCRYPTED_AUTH_METADATA>"
  },
  "iss": "https://auth.openai.com",
  "iat": 1790032532,
  "exp": 1790036132,
  "jti": "<TOKEN_IDENTIFIER>",
  "nbf": 1790032532
}
```

Treat the OpenAI authentication metadata as opaque; your app does not need to interpret it to store or use the access token. `refresh_token` is a separate field in the OAuth token response.