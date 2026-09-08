# Official Editor MCP: later closing observation

This is a transcription of the later observation in [PR #96](https://github.com/xrlab-dau/CHOOGuard/pull/96), read during review on 2026-09-08. The PR head was `2edb77970678206d60bb07950d9378f63641ddb4` and its reported update time was `2026-09-08T10:52:11Z`. This reviewer did not run the school Editor or inspect its private logs.

The author reported that at **19:50 KST (10:50 UTC)**, an Editor restart status request with a three-second limit recorded `Main thread operation timed out after 3000ms`. Subsequent project-targeted MCP status was ready with compiling=false, and scene/selection reads succeeded. The last Console query still contained **one error**. The session stopped without further repair or retesting.

The earlier [smoke receipt](2026-09-08-official-editor-mcp.json), recorded at 10:42 UTC, preserves the earlier 19:41 KST test result: EditMode 52/52, PlayMode 6/6 and zero errors at that checkpoint. Its bytes and source coverage hashes remain unchanged. The later timeout does not retroactively fail those tests, but the earlier result does not establish a clean Console at session closure or after every reconnect. A new Editor session must read the actual project status and Console before resuming mutations; no clean rerun is claimed here.
