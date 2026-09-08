# CI and delivery

## Always-on, license-free gates

- PR branch and title policy
- repository hygiene, secret patterns, prohibited data, and large-file checks
- Unity `.meta` and text serialization checks
- pinned GitHub Action and Unity Git dependency checks
- SceneBundle manifest contract checks
- safe PR risk triage without checking out PR code under `pull_request_target`
- bootstrap boundary regression tests on Ubuntu 24.04 and native Windows Server 2022, including the Windows PowerShell entry point on Windows

The bootstrap jobs install Python 3.12 and use synthetic command shims for installation failure tests. They do not install Pi or approve a real policy baseline. Each run uploads test logs and the tested checkout commit. Linux skips Windows-only tests; a Linux success must not be reported as native Windows verification. These jobs do not establish school PC ACL, Unity, HMD, or runtime performance acceptance. See [R-07 evidence](evidence/R-07/README.md) for the staged policy gate and its separate approval requirements.

`python3 scripts/dev/check_foundation.py` runs the synthetic Foundation contract and negative-fixture tests without Unity or extra Python packages. The required quality workflow stores `foundation-data-report.json`. A passing report covers Python data checks only; its C# compilation, Unity and HMD fields remain `not_run`.

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
