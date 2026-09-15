# Engine-free Foundation integration runner

This runner compiles the actual tracked `Runtime/**/*.cs` sources (the Foundation
assembly declares `noEngineReferences: true`) and explicitly selected engine-free
multiplayer boundaries with real NUnit/NUnitLite 3.14.0. It does not substitute
UnityEngine, UnityEditor, transport, physics, or NUnit stubs.

## Run from the repository root

Install .NET SDK 8.0.425, then:

```sh
dotnet restore tests/headless/ChooGuard.Foundation.Headless.csproj --locked-mode
dotnet run --no-restore --project tests/headless/ChooGuard.Foundation.Headless.csproj -c Release -- --result=headless-results.xml --workers=0
```

The process exits nonzero on test failure. Read its NUnit XML together with the
exit code; missing, failed and excluded tests never count as passing. CI runs
this same command on Ubuntu 24.04 and Windows Server 2022 with a SHA-pinned setup
action, exact SDK and committed dependency lock, then checks for source drift.
The temporary compiler export used to bootstrap this session has been removed.

## Coverage and boundaries

All core `Tests/Editor/*.cs` fixtures are included except the 13 paths explicitly
listed in the project file. Those fixtures require UnityEngine/UnityEditor,
including `JsonUtility` and `Application.dataPath`; they are **excluded**, not
successful or NUnit-skipped tests. New core test files are automatically included:
an unexpected engine dependency fails compilation rather than silently dropping
coverage. Multiplayer fixtures outside the explicit include list remain excluded.

The added whole-Foundation fixture consumes the committed connected-world and
simulation profiles with .NET's `System.Text.Json` field serialization, validates
them using canonical C#, and checks all 169 region-pair routes, twelve portal
clearances, twenty admission identities and a shared-equipment race, one hundred
NPC ownerships and explicit handoffs, journal-delta replay/deduplication, observed
snapshot compression/reordered reassembly, deterministic two-incident recovery,
and team/command voice grants. The existing pure core tests additionally exercise
thermal conservation, smoke optics, crowd state and train/checkpoint logic.

These are **in-process synthetic contract tests**, not a replacement production
server or a second network stack. Twenty identities are not twenty running
clients; the one hundred NPCs in the ownership fixture are colocated logical
entities, not measured crowd motion. Virtual incident ticks are not a real-time
soak. The memory journal is not the production disk journal. The visibility
callback is deliberately true: this fixture does not prove occlusion. JSON field
round trips are not Unity `JsonUtility` or NGO wire compatibility. Generated voice
tokens do not establish microphone capture or a LiveKit session.

Unity import/compile, the full EditMode/PlayMode suites, generated scenes, materialized
LFS assets, physical colliders/rendering, actual transport/audio, Windows Player
builds, 20-client/60-minute tests, HMD and railway procedure validation remain
separate requirements. .NET JIT execution is not Unity Mono/IL2CPP verification.
Current Unity capability is recorded separately by CI; an unavailable license is
not concealed by the headless job's successful result.

## Runtime hardening in this work unit

Admission snapshots the validated world/shift and credential mapping, rejects
invalid/duplicate/null roster identities and malformed digests, and normalizes
valid hexadecimal digest case. Changing caller-owned configuration no longer
changes a running admission authority. To intentionally replace credentials,
construct a newly validated admission object; no implicit hot-reload is supplied.
Protocol identity syntax matches `AuthoritativeShift`'s bounded ASCII alphabet.

Portal membership rejects invalid/oversized radii before squaring clearance, uses
double intermediates for finite coordinates, and rejects degenerate segments.
`TryLocate` denies absent or nonfinite previous positions and invalid radii before
its current-region fast path. The existing vertical and endpoint tolerances are
retained; this does not certify Unity collider geometry.

Voice issuance rejects negative or overflowing token clocks before signing,
including the two administration-token paths. Valid 90-second join and 30-second
administration lifetimes are unchanged.

The same final 213-case harness was run against the original three production
files and the changed files: original 171 passed / 42 failed, candidate 213 passed /
0 failed. This is an author-side regression comparison, **not independent review
or AAA product acceptance**. See the immutable local-run receipt and requirements
coverage in `docs/evidence/foundation/proposed/2026-09-15-core-integration.json`.
Later remote runs are tied to their own commit SHA in Actions and PR #215.
The scoped context handoff is
[`foundation-core-integration-20260915.json`](../../docs/context/projections/foundation-core-integration-20260915.json);
it does not replace or regrade the canonical context graphs.

NUnit and NUnitLite are MIT-licensed upstream dependencies restored from NuGet.
No third-party binaries are committed, and no project license or publication
permission is granted by this runner.
