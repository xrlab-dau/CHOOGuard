# CHOOGuard continuous cleanup worker

The root orchestrator enrolls completed, superseded generated files. A short
launchd process checks the approved entries every five minutes, deletes only
eligible exact files, writes a compact heartbeat, and exits. A detached, small
local supervisor is available when launchd is inaccessible. Neither option has
a resident agent/model process, network access, Unity calls, recursive traversal
or wildcard deletion. Source assets and active work remain protected.

## Ownership and files

- `cleanup_daemon.py`: Python standard library service and enrollment CLI.
- `cleanup_daemon_service.py`: detached local fallback scheduler with start,
  status and stop commands.
- `cleanup-daemon.example.json`: disabled configuration with no deletion scope.
- `cleanup-daemon.schema.json`, `cleanup-retirement.schema.json`: documented formats.
- `com.chooguard.cleanup-daemon.plist`: staged LaunchAgent; installation is a root
  orchestrator action.
- `.state/cleanup-daemon/config.json`: root-maintained actual configuration.
- `.state/cleanup-daemon/retirements/*.json`: immutable exact-file retirements.
- `.state/cleanup-daemon/active-protections.json`: optional live protection list,
  `{"protected_paths":[".planning/current-task", "workers/current-worker"]}`.
- `.state/cleanup-daemon/status.json`: last cycle, next due, available bytes,
  pressure, counters, manifest cursors and recurring observations.
- `.state/cleanup-daemon/events.jsonl`: compact event log, rotated at 512 KiB
  with two backups by default; no unbounded stdout/stderr log.

No service files or operational state are kept in `~/.local/state`.

## Root installation and scheduling

The following are operator commands. The implementation worker does not execute
installation, bootstrap or a real project deletion.

1. Copy `cleanup-daemon.example.json` to `.state/cleanup-daemon/config.json`.
2. Set `enabled` to `true`. Explicitly register current `active_plans` and original
   input paths outside the hard-protected source directories. Add only exact
   intended cleanup roots to `allowed_roots`. Do not allow a live producer's
   intermediate files until that producer retires them.
3. Run `doctor` and `dry-run`, then stage the plist at
   `~/Library/LaunchAgents/com.chooguard.cleanup-daemon.plist`.
4. Start the periodic worker:

```sh
launchctl bootstrap "gui/$(id -u)" "$HOME/Library/LaunchAgents/com.chooguard.cleanup-daemon.plist"
launchctl kickstart "gui/$(id -u)/com.chooguard.cleanup-daemon"
```

`RunAtLoad=true`, `StartInterval=300`, `ProcessType=Background`, low priority I/O
and nice 15 keep this a brief periodic task. There is no `KeepAlive` or sleeping
Python loop. A normal cycle has a 20-second work budget. Pausing the service
does not reclaim or change any Unity writer lease.

Stop scheduling:

```sh
launchctl bootout "gui/$(id -u)/com.chooguard.cleanup-daemon"
```

Resume by repeating `bootstrap`. Inspect scheduling:

```sh
launchctl print "gui/$(id -u)/com.chooguard.cleanup-daemon"
```

For a configuration pause, set `enabled:false`; cycles continue recording a
`disabled` heartbeat. `last_cycle_utc` and `next_due_utc` show scheduler progress
even when no candidate exists. `status` reports current available disk space
without deleting files. A slept or logged-out Mac can run later than the nominal
`next_due_utc`; the service does not claim that an overdue schedule ran.

### Detached fallback when launchd is inaccessible

The tool host may refuse `launchctl` calls with `Reentrancy avoided` even when
the staged LaunchAgent is valid. In that environment root can start the same
cleanup cycles through a detached local supervisor:

```sh
/usr/bin/python3 tools/maintenance/cleanup_daemon_service.py --config .state/cleanup-daemon/config.json start
/usr/bin/python3 tools/maintenance/cleanup_daemon_service.py --config .state/cleanup-daemon/config.json status
/usr/bin/python3 tools/maintenance/cleanup_daemon_service.py --config .state/cleanup-daemon/config.json stop
```

`start` returns after readiness; `start_new_session`, closed inherited file
descriptors and `/dev/null` streams allow the service to survive the invoking
tool command. A small idle Python supervisor waits between five-minute starts;
it does no scanning or deletion itself. Its only worker is a bounded existing
`cycle` subprocess, with a 35-second timeout and two-second termination grace.
No model agent or busy polling remains in the background.

`.state/cleanup-daemon/service.lock` permits one fallback supervisor. Its
`service.json` records PID, kernel process start time, a unique instance token,
last cycle exit, next due and completed cycles. `status` verifies the full
process identity. `stop` signals only that identified process, handles TERM/INT
normally, lets a current bounded cycle finish, and records `stopped`. A stale
or unknown PID is never killed or taken over. Duplicate `start` returns
`already_running`. The regular `cycle.lock` also prevents concurrent deletes if
launchd becomes active later; the staged LaunchAgent remains available for a
later normal login. Root may stop the fallback when normal launchd scheduling
is available.

## Commands

All commands require the explicit actual configuration:

```sh
/usr/bin/python3 tools/maintenance/cleanup_daemon.py --config .state/cleanup-daemon/config.json doctor
/usr/bin/python3 tools/maintenance/cleanup_daemon.py --config .state/cleanup-daemon/config.json dry-run
/usr/bin/python3 tools/maintenance/cleanup_daemon.py --config .state/cleanup-daemon/config.json status
```

`cycle` is the deletion command when `enabled:true`; root executes it only after
enrolling reviewed files. `dry-run` computes candidates without deleting them
or replacing the apply heartbeat. It may create its own empty state/lock folder.

### Retire completed generated intermediates

After completion, the root supplies exact, source-free paths, a reason and a
unique ID. Enrollment takes stat fingerprints, writes the immutable manifest,
adds its SHA256 to the config, and performs **no deletion**:

```sh
/usr/bin/python3 tools/maintenance/cleanup_daemon.py --config .state/cleanup-daemon/config.json enroll \
  --manifest-id completed-task-intermediates-001 \
  --allow-root .planning/completed-task \
  --path .planning/completed-task/generated-intermediate.json \
  --reason 'Root retires this superseded generated intermediate after applying the final assets'
```

For many files, use `--paths-file /absolute/path/to/exact-relative-paths.json`
with a JSON array of project-relative **file paths**. No directory/glob expansion
occurs. At most 10,000 files fit one manifest; use distinct IDs for later batches.
`--allow-root` explicitly extends the root whitelist and cannot overlap protected
roots. Existing dynamic protections always override it. A still-authorized
worker's script roots stay protected; retire that worker's authority and remove
the finished task from explicit active protection only when the work is settled.

By default retirement is no earlier than enrollment time plus the configured
quiet period (at least one hour). Root may provide an explicit timezone-qualified
`--retire-after` for already old, reviewed files. Every file must also have an
unchanged device/inode/size/mtime tuple and an old enough mtime. Any later edit
holds the file permanently under that immutable manifest; root must explicitly
re-enroll a subsequently retired revision under a new ID.

### Track exactly approved recurring temporary leaves

Only exact paths named by root are recurring; pressure never adds other files.
For the reviewed previous Unity import log leaves, use:

```json
{
  "allowed_roots": ["Logs"],
  "recurring_expired_files": [
    {"path":"Logs/AssetImportWorker0-prev.log","quiet_seconds":3600,"reason":"Root-approved previous import-worker temporary log"},
    {"path":"Logs/AssetImportWorker1-prev.log","quiet_seconds":3600,"reason":"Root-approved previous import-worker temporary log"}
  ]
}
```

These fields merge with the rest of the actual configuration. The current
`Editor.log`, current import-worker log, other logs and all Unity caches are
outside these two rules. There are at most eight explicit recurring leaves.

The service observes each current fingerprint. A new or changed fingerprint
restarts the stable-observation clock. It must stay unchanged for the whole
quiet period, also have old enough mtime, pass current path/native protections,
and have **no open handle** from `/usr/sbin/lsof -nP -t -- EXACT_PATH` (5-second
timeout). Missing lsof, timeout, stderr or an ambiguous result holds the file.
The final stat and C180 state are checked again before unlink. After deletion,
any newly regenerated file starts its own observation period. Root can use an
immutable manifest to retire the already reviewed initial old version sooner;
recurring monitoring then handles later regenerated versions.

## Protections and limits

Hard-protected roots: `Assets`, `Packages`, `ProjectSettings`, `.git`, `Library`,
`Temp`, `UserSettings`, `obj`, `source-evidence`, `asset-library`,
`workers/twin/evidence`, `.state`, `tools/maintenance`, `AGENTS.md`, `.agents`,
`.codex`, `.checkpoints`. Explicit/active protections cover any other original
source and current task location. All Scene files live in the protected Assets
root. No pruning, garbage collection or cache-wide removal is implemented.

Each unlink takes a brief shared lock on the actual C180 queue lock and checks
`Native_inflight:false`, released/idle writer state, no active request, and no
pending request. It yields to Unity work immediately. Policy script roots,
grant/receipt folders, `.planning/.active_plan` and current explicit protections
are re-read; missing or malformed authority fails closed. The cleanup process
never changes the C180 policy, queue, lease, source or scene.

Only single-link regular files are eligible. Physical parent directory file
descriptors and `O_NOFOLLOW` block symlinks and path escape. Directories,
hardlinks, missing files and changed fingerprints are held/skipped. No recursive
directory removal occurs, so small empty directories may remain.

One cycle lock prevents overlap. Defaults: at most 2,000 entries observed,
1,000 files removed, 4 GiB logical bytes unlinked and 20 seconds of work.
Immutable manifests are bounded to 8 MiB each and the registry to 128 entries.
Config/manifests never expand based on disk pressure or filename patterns.
30 GiB normal and 12 GiB critical are telemetry thresholds, not wider permission.
Errors back off from five minutes to at most one hour; busy writers are not
errors. Available-space delta is recorded separately from logical unlink bytes
because APFS clones, snapshots and concurrent writers affect actual free space.

## Isolated implementation checks

```sh
PYTHONDONTWRITEBYTECODE=1 /usr/bin/python3 -m unittest discover -s tools/maintenance -p 'test_cleanup_daemon*.py'
```

All fixture creation/removal is confined to a physical temporary directory.
Contracts cover approved expiry, no-write preview, snapshot changes, lease and
queue protection, source/range/symlink guards, live protection updates, changed
manifest backoff, heartbeats, recurring reappearance and open-handle uncertainty.
The detached fixture checks that the start command exits while the new process
survives, duplicate starts reuse one PID, a disabled cycle records a heartbeat,
and stop terminates the identified temporary service. Busy native cycles retain
existing recurring observation clocks.
They do not open, inspect, validate or change the Unity scene.
