# R31 guarded source-bank construction

Each batch has its own root-elected pose SHA, frozen source-model SHA, C180scope, frozen C# and broker request. prepare_batch_r31.py only freezes current authorized entries from ROOT-CONSTRUCTION-AUTHORITY-R31.json; it never calls native tools. A construction-module packet cannot be placed as a real bank.

WaitingBenchBuilderR31.template.cs creates only new MAIN2F_R31_PUBLIC_WAITING_BENCH_<sourceID> roots under existing FPSWorld/Architecture/2F and own Assets/ChooGuard/Art/StationInterior/PublicWaitingBenchesR31_20261019. It never changes existing object transforms/meshes/materials or original source files. Source normals, UVs, triangles and rigid transforms are imported as supplied. Glyphs are closed source contours; no prefab, Texture, Light or Text component is imported. Physical parts receive mesh colliders, glyph meshes remain separately editable without collider.

New mesh/material assets are shared by frozen source hash. SHARED-ASSET-REGISTER-R31.json records source SHA and actual own asset-file SHA; later scopes reuse exact unchanged own Assets and abort on user-modified cached assets. SaveAssetIfDirty is limited to each new asset, followed by actual Scene save. Rollback before a save affects only this request's created objects/assets, with exact current C180 guard.

All native calls run through current .state/SCENE-WORKER-C180/active-broker/scene_worker.py. The constructor checks ACTIVE/Native_inflight/exact thread/scope/request/grant nonce/command-source hash before mutations. Broker alone returns authority after a settled actual native result; UNKNOWN never expires by elapsed time. No QA, tests, Play, probes, captures or reopen stages.

WB01-001 has one actual source priority bank. STANDARD-TEN-002 has exactly the10individually root-elected standard banks; scopes execute serially, with no scope merging. Future source banks require their own registered immutable pose packet before freezing/execution.

FINAL-SEVENTEEN-003 is the separately frozen final17source-bank scope. Its root-elected packet includes the exact WB24/WB25 terminal-uncertainty corrections; the constructor applies their supplied world positions without adjustment. The previously applied11banks are never traversed or changed. All17reuse the already saved standard-family shared Assets. Actual save/native settlement/release are recorded in APPLIED-FINAL-SEVENTEEN-003.json and its C180 broker receipts.
