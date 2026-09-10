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
2. Confirm, as an org owner (or ask one to confirm), that the runner group serving this runner has
   "Allow public repositories" enabled — see the runner-group note below. A runner in a group that blocks
   public repositories will register successfully but will never be dispatched a job from this repository,
   silently, with no error shown at registration time.
3. Confirm the runner shows **Idle** under the repository's Settings -> Actions -> Runners before doing
   anything else. Do not set `GPU_CI_ENABLED=true` while the runner is offline or absent — the job is
   `if: vars.GPU_CI_ENABLED == 'true'`, so flipping this on with no runner online queues the next dispatched
   run straight into the 24-hour timeout ([self-hosted runners reference](https://docs.github.com/en/actions/reference/runners/self-hosted-runners)).
4. Only then set the repository variable `GPU_CI_ENABLED=true`.
5. Trigger the run manually (`workflow_dispatch`) while the runner is present and idle. Watch it to
   completion; do not leave the shared PC while a job you started is still running.
6. When finished, either stop the runner process (leaves it "Offline" and safely skippable by future
   dispatches) or set `GPU_CI_ENABLED=false` again so nobody accidentally dispatches against an absent runner.

Why `concurrency: { group: nightly-gpu, cancel-in-progress: true }` is still kept even though the cron is gone:
with `workflow_dispatch`-only triggering, its job is no longer "cancel Friday's leftover run before Monday's" —
it now exists so that if someone dispatches a second run before a stuck or abandoned first run finishes (or
before anyone remembers to stop the runner from a previous session), the newer dispatch cancels the stale one
rather than queuing behind it toward its own 24-hour timeout.

Runner version floor: this workflow's `actions/checkout` and `actions/upload-artifact` steps are pinned to
v7.0.1, which run on the Node 24 runtime and require **Actions Runner >= 2.327.1**. Register a runner at or
above that version; an older runner's first step (checkout) will fail immediately. By default a self-hosted
runner auto-updates itself when a job is assigned to it, or within a week of a new runner version's *release*
if it has not been assigned any jobs in that window — the grace period is anchored to the release date of the
new version, not to how long the runner itself has been online, so an intermittently-online school PC is not
automatically "safe" just because it connects periodically
([communication reference](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#communication)).
That automatic update is what is expected to keep this runner above the v2.327.1 floor; do not pass
`--disableupdate` when registering it. If automatic updates are ever explicitly disabled, GitHub stops queuing
jobs to that runner once it has gone more than 30 days without applying an available update
([runner software updates](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#runner-software-updates-on-self-hosted-runners)).
This is a separate mechanism from removal: a non-ephemeral self-hosted runner that has not *connected* to
GitHub at all for more than 14 days is automatically deregistered from the repository regardless of the update
setting
([removing self-hosted runners](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/remove-runners)).
For this school-PC runner, the practical read is: showing up and running it every so often keeps it both
updated and registered; going quiet for two-plus weeks risks deregistration independent of the update setting.

Runner-group consequence for a public repository: `xrlab-dau/CHOOGuard` is public, and the org's only runner
group (`Default`) currently has `allows_public_repositories: false`. A runner registered under that group
cannot serve jobs for this repository until an organization owner either enables that flag on the group or
moves the runner to a group that allows public repositories. Confirm this before assuming a freshly registered
runner will pick up the job — an org owner must make that runner-group change; this document does not grant it.
Prefer scoping any such group to this repository rather than flipping the flag org-wide, since that setting
exposes every runner in the group to every public repository the org hosts.

The runner must not process untrusted external pull requests.

## Delivery

A signed `vX.Y.Z` tag builds a Windows OpenXR player and creates a draft GitHub Release. Publishing remains a human decision after headset verification.
