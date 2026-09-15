# Engine-free Foundation regression runner

Runs the **actual Git-tracked C# Foundation runtime**, whose assembly declares
`noEngineReferences: true`, with the real NUnit/NUnitLite 3.14.0 framework on .NET 8.
No Unity engine or NUnit stubs are substituted.

From the repository root:

```sh
dotnet run --project tests/headless/ChooGuard.Foundation.Headless.csproj -c Release -- --result=headless-results.xml --workers=0
```

All `Runtime/**/*.cs` files and the engine-free `Tests/Editor` fixtures compile
from their canonical paths. The 13 explicit fixture exclusions in the project
file require UnityEngine/UnityEditor (including JsonUtility and Application data
paths). They are **not executed**, not passing or NUnit-skipped tests. New fixture
files are included automatically; an unexpected engine dependency fails the
build instead of silently removing coverage. NUnit failures propagate as a
nonzero process exit. Check the XML and process result together.

This is supplementary fast regression coverage, **not** Unity import/compilation,
EditMode/PlayMode, scenes, colliders, rendering, real transport/audio, Windows
Player builds, measured 20-client/60-minute acceptance, or railway validation.
The .NET runtime/JIT differs from Unity Mono/IL2CPP. Unity tests remain required.
NuGet sources and direct dependency versions are explicit; the generated lockfile
must be retained after the first restore, and CI then uses locked restore.

NUnit and NUnitLite are MIT-licensed upstream dependencies, restored from NuGet;
no third-party binaries are committed or new project license granted.
