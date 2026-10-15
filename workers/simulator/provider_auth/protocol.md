# Multi-provider subscription protocol v1

`connection.json` schema1 is unchanged. It contains only the loopback endpoint, ephemeral native capability and separate browser bootstrap; never export or log those values.

`GET /status` adds `protocol:multi-provider-subscription-v1`, `providers`, `accounts`, `selection`, `helper_implementation` and `total_admitted_attempts`. `provider`, `model` and `session_generation` match the current selection exactly. A null selection is not ready. A provider registry row must be registered, use `user_subscription`, support browser OAuth and both `initial_world`/`npc_plan` before native use.

The immutable selection has exactly eight string fields:

```json
{
  "provider_id":"openai-chatgpt",
  "account_id":"<opaque app-owned registration UUID>",
  "model_id":"<exact account catalog identifier>",
  "session_generation":"<opaque current session generation>",
  "catalog_version":"<SHA256 of scoped account model catalog>",
  "selection_version":"provider-selection-v1",
  "billing":"user_subscription",
  "registration":"own_app_public_oauth"
}
```

`GET /models` explicitly queries all signed-in accounts, without enabling generation or changing the selected account. Each row includes `provider_id`, `account_id`, `model_id`, `slug`, `display_name`, `catalog_version`, `billing`, `capabilities`, `selectable`, `reason`, and available subscription usage metadata. Equal model names in separate providers/accounts remain separate choices. An unavailable catalog is reported per account; no default model list substitutes for it.

Browser-only `POST /session` accepts `provider_id`, `account_id`, `model`, optional `catalog_version` compare-and-set, `cloud_enabled` (wire compatibility name; UI says subscription use), bounded `limits`, `initial_world_allowance` and same-account `resume_after_usage_review`. It validates the whole selection before committing. Login/start/cancel/logout are browser-only. Device authorization uses browser-only `POST /auth/device/poll` with `provider_id` and opaque state; no OAuth token or device code is returned to Unity/browser JSON.

Native `POST /v1/chat/completions` requires the private native Bearer capability, `X-ChooGuard-Session`, and `chooguard_selection` containing that exact eight-field object. Missing, partial, mismatched or stale bindings fail before inference. `chooguard_purpose` is `initial_world` or `npc_plan`; both route to the same selected account/model. The facade checks the same snapshot after refresh and after upstream success **or failure**. A changed snapshot returns HTTP409; an old response never applies to a new play selection. Nonselected account login/logout/catalog work preserves the active selection. No automatic fallback exists.

Completion `chooguard.selection` echoes the bound object and `billing:user_subscription`. Its opaque account UUID is not a secret; raw completion tapes may retain it. The canonical C# selection record hashes account identity and binds cache/replay metadata to the selected tuple. Never record account labels/email, OAuth tokens, local capability, browser cookies or callback queries. External outputs are recorded for deterministic local replay; model seed support is not claimed.

Budgets are admission reservations distinct from measured tokens. Ordinary and initial one-shot budgets are separate, with a global process cap of 24 admitted attempts across providers/purposes. Changing provider does not reset that global cap. An initial allowance is never recreated during upgrade, login, logout or selection changes.

Legacy `Provider` is retained only for existing regression/explicit old protocol tests. The production entrypoint uses `MultiProvider`; the new production inference path always requires the tuple.


The Unity FPS product menu uses [native-provider-control-v1](native-control-protocol.md) for provider/account/catalog/login/selection/consent operations. The browser is only the provider authorization surface. The prior HTML administration routes remain diagnostic compatibility, not the game UI. Native mutations require the same protected capability plus generation CAS; inference retains the mandatory exact selection tuple.
