# Supplementary C# review execution

This .NET 8 harness links the unchanged six Foundation `Runtime/*.cs` files and
the existing `Tests/Editor/TrainingSessionTests.cs`. NUnit and NUnitLite are pinned
to 3.14.0, and the source is compiled as C# 7.3. No Unity assembly or Unity API
stub is used. The filesystem fixture serializes receipts with System.Text.Json;
the actual Unity JsonUtility path remains untested here.

It also includes the local capture-writer tests. Before building, run
`prepare_capture_source.py` to extract the exact pure C# capture-writer class into
an output directory outside the checkout. Pass that generated file through the
`CaptureSourceFile` MSBuild property. The source extraction and its hash must be
preserved as evidence; it does not compile the surrounding Unity controller.

The current `TrainingSessionTests` fixture contains **37 cases**: 28 `TestCase`
attributes and nine standalone `Test` attributes. CI must verify that all 37
cases in this fixture are discovered and passed, with no skips. Additional
fixtures must be counted separately.

`CanonicalScenarioTests.cs` is deliberately excluded. Its 15 tests use actual
Unity `JsonUtility` and package resolution and must run in Unity. This harness
does not establish Unity compilation, EditMode, PlayMode, scene, graphics, HMD,
different-provider review or final task acceptance.

From the repository root, after preparing the capture source and using the
CI-pinned .NET SDK:

```sh
python3 scripts/dev/csharp-review/prepare_capture_source.py --output /absolute/output/path
dotnet restore scripts/dev/csharp-review/FoundationReview.csproj --configfile scripts/dev/csharp-review/NuGet.config
dotnet build scripts/dev/csharp-review/FoundationReview.csproj --no-restore --configuration Release -p:CaptureSourceFile=/absolute/output/path/ReconstructionCaptureWriter.cs
dotnet scripts/dev/csharp-review/bin/Release/net8.0/FoundationReview.dll --workers=1 --labels=All "--result=/absolute/output/path/core-tests.xml;format=nunit3"
```

Set `DOTNET_CLI_HOME` and `NUGET_PACKAGES` outside tracked sources and disable
telemetry with `DOTNET_CLI_TELEMETRY_OPTOUT=1`. `bin`, `obj` and local results are
ignored. Preserve the tested commit, source SHA-256 hashes, resolved dependency
manifest (`obj/project.assets.json`), command logs and NUnit XML as CI artifacts.
Installation or compilation alone is not a passing test receipt.
