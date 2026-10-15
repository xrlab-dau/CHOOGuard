# Registration and sign-in

> For the complete documentation index, see [llms.txt](/llms.txt). Markdown versions of documentation pages are available by appending `.md` to the page URL.

Start first-time registration with `client_id=dynamic_agent_client`, include the host’s `ext_agent_host_id`, and set `agent_name_hint` to your app’s actual name, used consistently across installations. After the user names and authorizes the agent, the callback returns its issued `client_id`. Save it with the registration for that ChatGPT account and reuse it for later sign-ins to the same account. Send the same `ext_agent_host_id` for the same host. `dynamic_agent_client` is the first-time registration entrypoint, not the client ID to save or use for token exchange. This direct flow needs neither a client secret nor a partner API key.




## 1. Choose a ChatGPT account and prepare the host

When the user first opens your tool, show a Sign in with ChatGPT button labeled **Continue with ChatGPT**, following the [UI/UX Guidelines](https://developers.openai.com/siwc/ui-ux-guidelines) for approved OpenAI branding. On later launches, let them choose a saved ChatGPT account or add another account or workspace. Keep a new sign-in attempt separate from the active account until its identity is validated.

Choose and persist this host’s `ext_agent_host_id` before its first sign-in, or reuse its existing value for the same host. For initial dynamic registration, use your app’s actual name consistently as `agent_name_hint`. See [A client vs. an agent host](https://developers.openai.com/siwc/token-sharing-open-source#a-client-vs-an-agent-host) and [Managing ChatGPT accounts](https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions#managing-chatgpt-accounts) for these choices.

## 2. Start authorization

Start your callback listener and generate a fresh random `state`, OIDC `nonce`, and PKCE verifier for each attempt. Keep those values and the exact callback URI until the callback completes.

For a new ChatGPT account, send `client_id=dynamic_agent_client`. Include your app’s `agent_name_hint` as well as the host’s `ext_agent_host_id`.

For a previously registered account, use its saved issued `client_id` instead of `client_id=dynamic_agent_client`. A retained `id_token_hint` identifies the original account; a saved email can also be sent as `login_hint`.

**Expected returning sign-in:** For routine reauthorization with an issued dynamic-client ID, without `id_token_hint` the user sees the account selector screen, then redirects, with no workspace selection or consent screen. With `id_token_hint`, the flow skips account selection and redirects. The client stays bound to its registered workspace.

Open the system browser at `https://auth.openai.com/api/accounts/authorize` and URL-encode each query parameter value. Send the following parameters in the authorization request:

| Parameter               | Value and purpose                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `client_id`             | `dynamic_agent_client` when the user registers their client for the first time. For future reauthorization through the regular OAuth flow, reuse the same issued client ID returned during registration.                                                                                                                                                                                                                                                                                                                                                                     |
| `agent_name_hint`       | Your app’s actual name, such as `OpenClaw`. Use it consistently across installations and include it only on initial dynamic-registration requests. Omit it from reauthorization with an issued client ID. The user can edit the name before approval; it is display metadata, not identity.                                                                                                                                                                                                                                                                                  |
| `ext_agent_host_id`     | Required host identifier, distinct for each host.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `id_token_hint`         | For reauthorization, the ID token retained from this account’s previous successful sign-in. The hint may be expired; it identifies the account, not a valid authenticated session. Omit it after signing out or if none was retained. Under the [expected returning sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in#2-start-authorization), it skips the unified session selector.                                                                                                                                                                                          |
| `login_hint`            | For reauthorization, optional email hint, saved from this account’s validated ID token. For example, `login_hint=user%40example.com`. If you also send `id_token_hint`, keep both hints associated with the same selected ChatGPT account. Used to pre-select the user account as part of the OAuth flow.                                                                                                                                                                                                                                                                    |
| `response_type`         | `code`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `redirect_uri`          | Use an HTTP loopback callback on `127.0.0.1` from initial registration onward, for example `http://127.0.0.1:1455/auth/callback`. Start the listener before opening the browser. Later sign-ins may use another available port, such as `http://127.0.0.1:54321/auth/callback`; only the port may vary. Keep the scheme, host, and path unchanged: `/callback` does not match `/auth/callback`. Within each authorization attempt, send the exact same URI, including its selected port, in the authorization request and code exchange. Do not substitute with `localhost`. |
| `scope`                 | Identity scopes: `openid profile email`. ChatGPT plan usage scopes: `offline_access resource.invoke chatgpt.tokens.use.direct`.                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `resource`              | `https://api.openai.com/v1`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `state`                 | A fresh random value bound to this authorization attempt; validate it on return.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `nonce`                 | A fresh random value for ID-token validation.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `code_challenge_method` | `S256`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `code_challenge`        | The base64url-encoded SHA-256 digest of the PKCE verifier, without padding.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |

For a new registration, the user signs in and reviews the requested permissions in the browser. Returning sign-in follows the expected flow above. The browser then returns to your loopback callback.

## 3. Handle the callback and exchange the code

If the callback contains `error=access_denied`, validate `state`, stop the attempt, and do not exchange a code. See [ChatGPT plan use isn’t enabled](https://developers.openai.com/siwc/token-sharing-open-source/errors-and-recovery#chatgpt-plan-use-isnt-enabled) for authorizing it later. The new-registration callback supplies the client ID used in the exchange. A successful **new registration** returns `code`, `state`, and the issued `client_id`. The callback may also include `scope`, as shown below. On reauthorization, the callback may omit `client_id`; retain the exact client ID already associated with the pending request. If the callback supplies a different client ID, reject the result rather than replacing the selected account’s registration. Use the token response’s granted scopes when deciding whether ChatGPT plan usage is enabled:

```text
YOUR_CALLBACK
  ?code=<AUTHORIZATION_CODE>
  &scope=chatgpt.tokens.use.direct+email+offline_access+openid+profile+resource.invoke
  &state=<ORIGINAL_STATE>
  &client_id=<ISSUED_CLIENT_ID>
```

1. Validate `state` against the pending authorization attempt and handle an OAuth `error` before using the result.
2. For new registration, retain the issued `client_id`, such as `oaiapp_...`. Do not save `dynamic_agent_client` as the connection’s issued ID. If a new-registration callback does not include an issued ID, treat registration as incomplete.
3. **Exchange the code.** POST a form-encoded `authorization_code` grant to `https://auth.openai.com/api/accounts/oauth/token` with the issued `client_id`, `code`, `code_verifier`, the same `redirect_uri`, and the same `resource`. No client secret is required.

If code exchange returns `invalid_grant`, discard that code and start a fresh authorization with the issued client ID retained for this attempt. The token endpoint returns the credentials and granted scopes used in the next steps.




## 4. Validate the ID token and ChatGPT plan permission

**Validate the ID token.** Verify its signature against OpenAI’s published JWKS. Check the issuer, audience against the issued client ID, expiration, and the nonce saved for this attempt. Use its validated `sub` as the account identity. Email and `sub` are not workspace identifiers; retain the issued client ID with the verified identity to keep registrations separate. The signature-validation example in [On your website](https://developers.openai.com/siwc/website) also applies here; this OSS flow remains a public client without a secret.

For a returning account, confirm the new ID token’s verified identity matches the selected account before replacing credentials. Check the token response’s granted scopes for `chatgpt.tokens.use.direct` before proceeding to inference. A valid ID token alone does not authorize ChatGPT plan usage.




## 5. Store credentials in a local file

Keep a separate protected credential record for each issued client ID and its verified account identity. Save the validated identity, tokens, granted scopes, and expiry information so the tool can resume and refresh after restart. Retain the ID token so your tool can provide `id_token_hint` on a later sign-in. Keep the stable host ID with the runtime it identifies. The example below shows one local credential record; paths and filenames are app-defined. The issued client ID comes from the new-registration callback. Build `scopes` from the returned, space-separated `scope` value.

```json
{
  "email": "user@example.com",
  "issuer": "https://auth.openai.com",
  "subject": "<VALIDATED_ID_TOKEN_SUBJECT>",
  "client_id": "<ISSUED_CLIENT_ID>",
  "ext_agent_host_id": "urn:uuid:...",
  "id_token": "<ID_TOKEN>",
  "access_token": "<ACCESS_TOKEN>",
  "refresh_token": "<REFRESH_TOKEN>",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scopes": [
    "chatgpt.tokens.use.direct",
    "email",
    "offline_access",
    "openid",
    "profile",
    "resource.invoke"
  ],
  "saved_at": "<UTC timestamp when your app receives this token response>"
}
```

Store credential files under an app-defined path such as `~/.config/YOUR_TOOL/`. Write files atomically with owner-only permissions (`0600` on Unix), and never commit or log them. Replace the access token, expiry, granted scopes, and rotating refresh token together after a successful refresh.