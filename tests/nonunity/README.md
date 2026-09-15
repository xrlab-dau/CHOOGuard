# Engine-independent simulation regression suite

From this directory:

```sh
dotnet restore --locked-mode
dotnet test --no-restore --logger trx
```

The project compiles the actual `Packages/com.xrlab.chooguard.foundation/Runtime`
C# sources and explicitly selected original NUnit tests. It adds clearance,
malformed movement input and mutable snapshot ownership regressions. No Unity
Editor, Unity assemblies, scene files or Unity stubs are loaded. The canonical
public connected-world JSON is copied to the output directory unchanged.

`global.json` selects the .NET 8 SDK feature band available on the runner.
NuGet packages are pinned in `packages.lock.json`; do not silently regenerate
the lock to make a restore failure pass. The lock covers test dependencies only,
not the separately versioned Unity package graph.

From the repository root, the Python and JavaScript portions run with:

```sh
python -m pip install -r tests/nonunity/requirements.txt
python -B -m unittest discover -s scripts/team/R-06 -p 'test_*.py' -v
python -B -m unittest discover -s scripts/team/M1-05 -p 'test_*.py' -v
python -B scripts/team/R-06/egress_controls.py run
node --test scripts/context/*.test.mjs
python -B -m unittest discover -s services/livekit -p test_name_preflight.py -v
```

The full LiveKit private-filesystem suite must additionally run on its supported
macOS backend; the platform-neutral name preflight test mocks that boundary only
to assert ordering. A passing preflight is not ACL or credential storage proof.

The R-06 transport and all training inputs in these tests are synthetic. Passing
this suite does not establish real network/voice/load behavior, Unity
EditMode/PlayMode success, authentic facility geometry, validated railway
procedures, institutional training efficacy or project AAA acceptance. Core
clearance guards retain the authored dimensions and historical movement
conventions; no scoring or response procedure is changed by these repairs.
