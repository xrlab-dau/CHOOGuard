# CI and delivery

## Always-on, license-free gates

- PR branch and title policy
- repository hygiene, secret patterns, prohibited data, and large-file checks
- Unity `.meta` and text serialization checks
- pinned GitHub Action and Unity Git dependency checks
- SceneBundle manifest contract checks
- safe PR risk triage without checking out PR code under `pull_request_target`

## Unity CI activation

Set repository variable `UNITY_CI_ENABLED=true` only after committing a Unity project and configuring protected secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

Prefer Unity Build Automation or a licensed self-hosted runner if storing personal account credentials is not acceptable. Never print these values.

## GPU CI activation

Register an isolated self-hosted Windows GPU runner with labels `self-hosted`, `Windows`, `X64`, and `choo-guard-gpu`, then set `GPU_CI_ENABLED=true`. The runner must not process untrusted external pull requests.

## Delivery

A signed `vX.Y.Z` tag builds a Windows OpenXR player and creates a draft GitHub Release. Publishing remains a human decision after headset verification.
