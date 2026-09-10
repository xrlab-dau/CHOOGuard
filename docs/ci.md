# CI and delivery

## Always-on, license-free gates

- PR branch and title policy
- repository hygiene, secret patterns, prohibited data, and large-file checks
- Unity `.meta` and text serialization checks
- pinned GitHub Action and Unity Git dependency checks
- SceneBundle manifest contract checks
- safe PR risk triage without checking out PR code under `pull_request_target`

`python3 scripts/dev/check_foundation.py` runs the synthetic Foundation contract and negative-fixture tests without Unity or extra Python packages. The required quality workflow stores `foundation-data-report.json`. A passing report covers Python data checks only; its C# compilation, Unity and HMD fields remain `not_run`.

## Unity CI activation

Set repository variable `UNITY_CI_ENABLED=true` only after committing a Unity project and configuring protected secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

Prefer Unity Build Automation or a licensed self-hosted runner if storing personal account credentials is not acceptable. Never print these values.

## GPU CI activation

`nightly-gpu.yml` (display name "GPU Quality Gate (manual)") has no `schedule` trigger. It runs only on
`workflow_dispatch`. The intended runner is a shared university lab PC (school PC), not a server: it is online
only while a person is physically sitting at it, so a cron schedule would routinely fire with no runner
available. GitHub queues a job with unmatched `runs-on` labels for up to 24 hours and then fails it; with a
weekday cron and `cancel-in-progress: true`, the Friday run has no Monday successor within that window and
would fail every weekend. Manual dispatch avoids this: nobody starts a run the runner cannot immediately pick up.

Order of operations, in this exact sequence:

1. Register the runner on the school PC, scoped to this repository, with labels `self-hosted`, `Windows`,
   `X64`, and `choo-guard-gpu`. Run it in interactive/foreground mode (`run.cmd`), not as a Windows service:
   installing a service requires administrator rights on a shared university machine, and
   [ADR 0005](adr/0005-school-pc-unity-workstation.md) says installation-permission gaps are not something to
   route around (no execution-policy bypass, no user-path install as a substitute for admin approval).
2. Confirm the runner shows **Idle** under the repository's Settings -> Actions -> Runners before doing
   anything else. Do not set `GPU_CI_ENABLED=true` while the runner is offline or absent — the job is
   `if: vars.GPU_CI_ENABLED == 'true'`, so flipping this on with no runner online queues the next dispatched
   run straight into the 24-hour timeout.
3. Only then set the repository variable `GPU_CI_ENABLED=true`.
4. Trigger the run manually (`workflow_dispatch`) while the runner is present and idle. Watch it to
   completion; do not leave the shared PC while a job you started is still running.
5. When finished, either stop the runner process (leaves it "Offline" and safely skippable by future
   dispatches) or set `GPU_CI_ENABLED=false` again so nobody accidentally dispatches against an absent runner.

Runner version floor: this workflow's `actions/checkout` and `actions/upload-artifact` steps are pinned to
v7.0.1, which run on the Node 24 runtime and require **Actions Runner >= 2.327.1**. Register a runner at or
above that version; an older runner's first step (checkout) will fail immediately. By default a self-hosted
runner auto-updates itself when a job is assigned to it or within about a week of being online, so keeping the
runner online periodically (rather than leaving `--disableupdate` set) is what keeps it above the floor. If
automatic updates are disabled, GitHub stops queuing jobs to a runner that has not applied an available update
within 30 days — separately, any runner that never connects to GitHub at all for more than 14 days is
automatically deregistered.

Runner-group consequence for a public repository: `xrlab-dau/CHOOGuard` is public, and the org's only runner
group (`Default`) currently has `allows_public_repositories: false`. A runner registered under that group
cannot serve jobs for this repository until an organization owner either enables that flag on the group or
moves the runner to a group that allows public repositories. Confirm this before assuming a freshly registered
runner will pick up the job — an org owner must make that runner-group change; this document does not grant it.

The runner must not process untrusted external pull requests.

## Delivery

A signed `vX.Y.Z` tag builds a Windows OpenXR player and creates a draft GitHub Release. Publishing remains a human decision after headset verification.
