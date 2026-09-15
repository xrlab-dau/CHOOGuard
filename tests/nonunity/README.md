# Non-Unity training core regression suite

With .NET SDK 8.0.423 installed, run from the repository root:

```sh
cd tests/nonunity
dotnet --version
dotnet test ChooGuard.NonUnity.Tests.csproj --configuration Release
```

`global.json` selects the exact SDK only for this test directory. It does not change Unity's compiler or repository-wide SDK selection. Running from another directory may select a different installed SDK.

The project compiles the actual Foundation `Runtime/**/*.cs` sources and a named set of existing NUnit tests, without Unity assemblies or replacement engine stubs. New tests cover the server command transaction, including synchronous adapter reentry, caller command mutation, ambiguous persistence failures, inactive evacuees, and physical-state publication while an action is being committed.

## Command transaction contract

`AuthoritativeShift` is still owned by one server thread. This change prevents synchronous callback reentry; it is not a multi-threading API. Queue input from other threads through the existing owner-thread adapter.

The authority snapshots the incoming `WorldCommand` before hashing or invoking visibility/interlock adapters. A nested submission returns `AuthorityBusy` without adding a receipt or reserving a sequence number. A caller may resubmit it after the outer call returns. This enum value is appended; existing wire numbers are unchanged.

Movement/input updates, physical publication, journal replay and discovery may not mutate the authority during a command transaction. Read-only snapshot methods remain available. `FaultPersistence` is deliberately allowed to interrupt the transaction and wins over any prepared state or successful callback return.

`ICommitSink.Append` must durably flush before returning. Any exception from that boundary means the write is uncertain: the authority pauses, disables input and returns `PersistenceUnavailable`, without advancing its in-memory sequence or recording acceptance. Do not retry writes on that instance. Recover a new authority from the existing integrity-checked journal; a fully persisted command is replayed and its original receipt handles a repeated request exactly once. A test sink models the difficult case where append recorded the commit before reporting failure. This does not emulate actual disk/controller failure.

Inactive evacuees cannot be claimed or handed off. Equipment `Active` retains its existing toggle meaning; an inactive/open equipment state is not generically blocked by the evacuee rule.

## Limits

Passing this suite does not prove Unity Editor/Player compilation, NGO transport, real voice, scene geometry, rendering, device performance, real storage durability, real multi-client load, railway procedure validity, or whole-project AAA acceptance. Those checks remain separate; no skipped Unity test is counted here. Package versions are test-project dependencies, not changes to Unity's manifest or lockfile.
