# Grounded former-locker approach test draft

Root installs `LockerApproachPlayTests.cs` in the existing PlayMode assembly and runs `FullCapsuleCrossesFormerLockerBankAndNamcheonDoorwayAndReturnsGrounded` after locker withholding. Offline compile is PASS with zero warnings/errors; Unity execution remains root-owned.

The controller uses radius .3m/height1.72m and begins once in the original locker source frame at U-37.5,V7.680001,Y7. It continuously crosses the exact old front/back, joins `door.World(local z=+1)`, enters at z=-1, reverses through the doorway and former bank, and returns to the initial grounded position. Each <=.05m horizontal move requires grounded/below collision, existing native floor ray support, source floor height and route progress. No actor repositioning or controller disable occurs after the sole initial placement, and no existing collider or floor mesh is changed.

The complete continuous radius footprint lies inside original convex source floor face33. All230 sampled centers/3910 ring points also pass; source face33 uniquely corresponds to installed native collider triangle61246, with mirrored source winding recorded. Exact original bank collider volume analytically obstructs this capsule; this is an offline counterfactual, not an actual negative Unity fixture.

The test pins sealed source packet hashes, bank/door/floor frames, unchanged door fields and source floor triangle correspondence. Changed source parts strictly fail. The runtime writes `.planning/2026-10-09-floor2-completion-pass2/locker-approach-play-proof.json`, including actual positions, native coordinates, grounding, floor ray triangle and actual checkpoints. `passed` begins false and becomes true only after full continuous return.

No field dimensions or new physical locker location are claimed. Existing sealed locker installer/replay packet and its handoff remain byte-identical.
