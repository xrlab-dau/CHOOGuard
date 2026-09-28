# Findings

- Current project and installed Unity agree on 6000.3.23f1.
- Existing PID 35931 has CHOOGuard projectPath and has run over three days; parent is 1.
- Current Editor.log is a separate project-less launch at 22:34 that ended package manager.
- Existing working tree has many modifications; preserve them.
- Prior memory path is directly_supported by current ProjectVersion/process evidence; older delivery details are near_match_only and irrelevant.
- Root/home AGENTS.md and navigation guide files are absent; use session instructions.

- PID 35931 owns the actual project UnityLockfile but is absent from current GUI application list. Old hidden process prevents a new project instance.
- Preserved available scene recovery/layout files before targeted process shutdown.

- Independent 2-second stack sample: stale Editor main thread waits in GetOrInitializeILPPProcess -> LaunchExecutable -> NSConcreteTask waitUntilExit (177 samples).
- New Editor PID 99219 owns new project lock; live log shows ILPP processing successfully.

- API update was already finished at inspection time; root did not click the dialog. API-updater log names exactly the three reported files. Current source uses Rigidbody.linearVelocity.
- Clarification: source snapshots were captured AFTER that update, and are not backups of pre-update source.
- Verified foreground Editor title FpsStation - CHOOGuard, Unity 6000.3.23f1; scene-loaded log present and zero C# compiler errors. Existing obsolete-API warnings remain.
- Tracked git status entries unchanged vs startup snapshot; this is not a byte-level no-change claim.
