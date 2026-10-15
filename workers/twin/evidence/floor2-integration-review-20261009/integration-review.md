# Floor 2 walking-route integration review

Scope: staged files in `workers/twin/output/floor2-landing-solution-20261009` versus current Assets, read-only. No Unity execution or scene changes. Root owns final integration and runtime checks. Drafts were being revised concurrently; `static-contract-evidence.json` records the inspected file hashes.

## Revision follow-up

The landing owner revised the drafts after this review. Static re-read confirms R1 removed, R2 changed to full-3D same-foot rebind, R3 has WalkingLinkFailed with immediate failure and explicit same-foot GoTo recovery, R6 tests the full curve and connectors, R7 BakedFloor constructor rolls back, R8 checks actual intermediate support, and R9 rejects null entries and blank owners. R4 now has body-owned controller cleanup and disable/destroy cancellation, but existing StopCoroutine replacement methods still need to invoke Cancel explicitly. The original finding sections below retain the evidence and requested verification.

Residual work at this follow-up snapshot:

- R5: Close does not yet invalidate live graph cache results.
- R4: Cancel the body-owned controller before each existing StopCoroutine replacement/cancellation path.
- R7: Pre-parse walking entries before StationWorld adds navmesh and original transport objects; malformed new resource data should not leak that state.
- R9 minor: ID/proof whitespace strings still pass; use IsNullOrWhiteSpace.
- Required runtime evidence: real runtime owners/attachment connectors in four directions at NPC/responder speeds; full-3D no-snap agent rebind, physical blocker failure, and cancellation cleanup. None was executed by this reviewer.

## Verified boundaries

- The authoritative first-two canonical result file contains n56 low and s1 high only, each in two directions: four actual CharacterController traversals at radius 0.3 m, height 1.72 m; passed=true, original geometry unchanged. This does not validate runtime NavMesh attachment connectors or the newer feedback walker.
- The draft JSON contains exactly these two routes. It explicitly records `runtime_NavMesh_attachment_connectors_passed=false` for each.
- Current `Assets/ChooGuard/Art/Emergency/station-points.json` contains 21 escalators and 1 elevator, SHA256 `029cf4f05c090c35d2e1443928b59228df50fe5c6d8cc5edbd45576e41e6ae0e`.
- Staged runtime route Setup assigns the walking MonoBehaviour as NavMesh link owner. PersonBody reads `data.owner as IStationLink`, so the intended owner dispatch is present; runtime attachment/owner retrieval still needs a direct check.
- Existing editor transport link loops remain one-way for escalators and bidirectional between elevator stops.
- BakedFloor uses shifted immutable navmesh plus resource curves; guidance has no new read of `Closed`, runtime obstacles, or floor raycast state. RouteGraph.Survey deliberately excludes live walking closure from Weight, preserving the knowledge boundary.

## Fixes already observed during review

1. StationRouteBuilder excludes walking-link crossings from generic graph edges and keeps measured walking IDs on dedicated endpoint arcs. This avoids tagging an arbitrary composite path with a single walking owner and avoids GuideRoute endpoint mismatches.
2. Dedicated graph arc length includes measured curve plus the two sampled-node connectors. Walking endpoint sampling was narrowed to 0.12 m.
3. BakedFloor now inserts the complete canonical curve including exact Start and End, preserving connectors from returned NavMesh corners instead of cutting directly to the interior curve.
4. GuideRoute's new direct-entry Hop branch was removed; its existing BakedFloor expansion now handles measured walking geometry.
5. PersonBody guards `Agent.isOnNavMesh` before reading `Agent.nextPosition`.

## Remaining findings at inspected snapshot

### R1 — Definite compile blocker: undeclared editor link list

`StationNavigationBuilder.integration.cs:140`, RebuildElevatorStops, calls `AddTemporaryLinks(links)` but this method declares only instance/log; the links list exists in a different method. Its finally removes only the navmesh instance.

Small patch: remove this unnecessary call from elevator-only rebuilding, or add a local list and remove all temporary links in finally. Landing owner/root notified.

### R2 — Agent rebind accepts arbitrary vertical movement

`PersonBody.integration.cs:409` clears `apart.y` before checking the re-enabled agent's displacement. A same-XZ position on a different vertical surface passes the check. Re-enabling updatePosition can then move the body vertically despite the claimed same-foot completion.

Small patch: check full 3D displacement against the allowed rebind tolerance, verify isOnNavMesh first, and record body/nextPosition before and after enable and the following frame. Restore updatePosition only after this succeeds.

### R3 — Failed walkers become permanently disabled without responder recovery

`PersonBody.integration.cs:412–415` disables Agent, clears hasGoal/goalPending and marks NoRoute. `GoTo` called later sets goalPending and waits for OnNavMesh, but no path re-enables this Agent. Current responders may advance their arrival fallback after being stationary, or retry further goals forever with Agent still disabled.

Small patch: define an explicit failure state. If same-foot on-mesh rebind is permitted, restore a stopped agent at the exact actual body only after the same full-3D check; close/block the failed route and permit normal replanning. Otherwise expose terminal failure to owner logic so it does not report arrival or repeatedly queue unreachable goals. Never CompleteOffMeshLink or Warp on failure.

### R4 — Temporary CC cleanup is not owned by PersonBody cancellation lifecycle

`StationWalkingRoute.cs:141–148` creates an unparented actor, disables the body Agent and enables CC before entering try. Its finally only schedules Destroy and calls finished. PersonBody has StopCoroutine paths and GO teardown but no explicit handle for this controller. The nested coroutine's finally cannot be the sole ownership contract for cancellation; setup exceptions also occur outside try.

Small patch: give body/link traversal an owned disposable controller handle, invoke cleanup from cancellation/disable/destroy paths, put setup inside try, disable CC before deferred Destroy, and clear Scripted/Riding/traversal state deterministically. Apply own-collider ignore after activating CC and before Move; validate the resulting ignore pair explicitly rather than assuming the disabled-CC call persists.

### R5 — Cached graph routes ignore newly closed walking links for up to 10 seconds

`StationWalkingRoute.cs:100` closes/removes a link without graph version invalidation. `RouteGraph.integration.cs:275` returns any same-version result younger than ResultSeconds=10, bypassing Weight's walking-Closed check.

Small patch: a walking-link closure revision participates in the live result cache version/key, or closure notifies RouteGraph to invalidate live caches. Survey and immutable BakedFloor must continue to ignore this hidden live closure.

### R6 — Hazard cost uses the unused chord instead of measured curve

RouteGraph.Touches still tests only From→To. The supplied curves deviate substantially from those endpoint chords; a fire at an interior measured point can miss the live edge penalty entirely. `static-contract-evidence.json` gives exact interior-point fixtures and chord distances.

Small patch: for a walking edge, test each measured curve segment and both sampled endpoint connectors. Ordinary and existing transport edge behavior can remain unchanged.

### R7 — Constructor exception can leak shifted navmesh/links

`BakedFloor.integration.cs:28–34` adds shifted data and earlier links, then throws on a later link failure. Construction never returns, so callers cannot Dispose the partially created object.

Small patch: wrap link creation in try/catch, call Dispose on failure, then rethrow. Pre-parse/validate all walking resources before StationWorld creates runtime objects to avoid introducing a new post-transport resource failure leak.

### R8 — Actual intermediate footprint is not rechecked after collision deflection

`StationWalkingRoute.cs:165–169` checks Floor(next), then allows the CC's actual position to differ horizontally by up to 0.15 m. That proposed footprint certificate does not prove the actual footprint. Only final support is checked at actor.transform.position.

Small patch: after each Move, check Floor(actual actor position) and actual floor registration as well, fail immediately at the actual position without endpoint snapping.

### R9 — Data validation should fail deliberately for malformed new route entries

Load calls r.Validate without checking r==null; `routes:[null]` gives NullReferenceException. Whitespace IDs/proofs and null/blank floor owner entries pass Validate and fail much later during traversal. Finite path coordinate checking and max curve length are already present.

Small patch: reject null route entries and blank required strings/owner paths with descriptive InvalidOperationException; use ordinal ID comparison. The proof string is provenance metadata, so do not treat physicalVerified=true as a runtime proof bypass.

## Focused verification targets

1. **Compiler**: integrate drafts locally and compile Editor/runtime assemblies; R1 must be caught before baking anything.
2. **Exact runtime connectors/owner, four directions**: actual StationWorld-created owned links, real PersonBody including own solid colliders, radius .3/height 1.72, NPC and responder speeds. Record actual body foot → canonical start → all measured corners → canonical end → OffMeshLinkData.endPos. Assert actual support each step and full-3D no-snap rebind. No direct transform stepping except following actual CC output.
3. **Physical negative fixtures**: put a solid blocker on the interior curve, then a floor-support gap under an attachment connector. Both must report failure at the actual position, never complete/warp or claim arrived; repeated GoTo has defined behavior.
4. **Cancellation/exception cleanup**: cancel mid-walk, deactivate/destroy body, and replace traversal using existing FollowInFrame/ReturnToNavMesh entry points. Assert no temporary CC remains after the frame and state does not retain Scripted/Riding/traversal. A setup exception must also leave no controller.
5. **Live closure versus knowledge**: warm live Explore/Toward cache, Close route, query at +.1s and assert immediate live exclusion. The same guidance Survey/BakedFloor output remains unchanged absent new known rules.
6. **Graph curve contract**: only dedicated endpoint walking IDs, both directions, length equals expanded BakedFloor path within centimeter quantization. Add a danger of radius .5m at the supplied interior-point fixture; live graph must charge DangerCost although the endpoint chord misses it. All original 21 escalator directions and elevator connections remain intact.
7. **Malformed resource/rollback**: null route, duplicate IDs, blank owners, NaN/Infinity point, invalid version. Each deliberately rejected before creating permanent nav state. Force later BakedFloor link setup failure and assert earlier links and shifted navmesh are removed.

Stop condition met: bounded findings and focused patches/tests delivered to root and landing solution owner. No Unity PASS claimed by this reviewer.
