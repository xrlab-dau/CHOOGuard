# Isolated standalone graphics diagnostic

The macOS standalone player builds successfully from the existing isolated simulator scene. This is a supported alternative to the Editor graphics startup route. The diagnostic is opt-in and does not attach unless the player receives `-chooguard-graphics-proof <directory>`.

The automation execution context still prevents actual simulator rendering. The player aborts in macOS application registration before creating its graphics device or executing game scripts. The native probe independently creates the Apple M1 Metal device, submits a render command, and reads the expected RGBA pixel. GPU availability therefore does not substitute for simulator images.

Actual receipts and logs are under `output/simulator/platform-ktx-unity/diagnostics/graphics-resume/`:

- `desktop-probe.json`: Metal command/readback PASS, online display count 0, current CG session unavailable.
- `staging-receipt.json`: exact two diagnostic C# scripts and two assembly definitions staged only into the isolated project.
- `process-build.json` and `build-receipt.json`: actual Unity 6000.3.23f1 macOS standalone build succeeded, zero errors/warnings, Metal graphics API.
- `process-player.json` and `player.log`: bounded actual player startup aborted with SIGABRT in application registration/display initialization. No screenshot or runtime acceptance result was produced by this player run.
- `outcome.json`: source preservation and process cleanup evidence.

The existing twenty canonical Runtime/Editor sources were not restaged or changed by this lane. Diagnostic C# scripts remain under `workers/simulator/graphics_runtime/`; their isolated copies are under `Assets/Simulator/GraphicsResume/`. The orchestrator must reconcile them deliberately if promoted into the canonical staging manifest.

From a normal desktop Terminal, the exact built-player invocation is:

```sh
cd /Users/um-yunsang/CHOOGuard/output/simulator/platform-ktx-unity
./diagnostics/graphics-resume/PlatformGraphics.app/Contents/MacOS/CHOOGuard \
  -batchmode -force-metal \
  -logFile /Users/um-yunsang/CHOOGuard/output/simulator/platform-ktx-unity/diagnostics/graphics-resume/desktop-player.log \
  -chooguard-graphics-proof /Users/um-yunsang/CHOOGuard/output/simulator/platform-ktx-unity/diagnostics/graphics-resume/desktop-proof
```

The capture component writes an FPS view, source platform/KTX overview, door/interior views for all seven qualified operational cars, and an actual standalone runtime acceptance result. It uses URP single-camera render requests and hashes every image. Visual review remains required after those files exist. The held Car04 door/seats are not activated by this diagnostic.

Supported APIs/flags were checked against Unity's primary documentation:

- [Unity 6000.3 Player command-line arguments](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/player)
- [Unity 6000.3 Editor command-line arguments](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor)
- [Camera render requests](https://docs.unity3d.com/cn/6000.0/ScriptReference/Camera.SubmitRenderRequest.html)

Neither global registration, signing, credentials, shared editor state, nor original source assets were changed. Unity's normal build output includes its player bundle; no manual code signing or app-registration repair was attempted.
