# Findings

Prior sign-quad fit failures are navigation hints only, not structural controls.

- Existing corner controls target an authored Kit stainless skirting mesh, not original SKP geometry. Column footprint 1.94 by 1.4 model units comes from past class-b relative reconstruction. It is a model control, never an independent field control.
- The prior side-foot triangulation has 5.60px maximum residual and was rejected; near-foot <=2.83px and far-foot <=0.33px.
- All three controls currently lie on the same walking floor. A sole visible edge fixes a scale and yaw but cannot validate column depth or absolute registration independently.

- Enlarged original source views show the prior near-foot mark below the brick-floor corner, on an unrelated blue floor stripe. New controls will use actual brick-floor junctions and the same column vertical corner, with held-out frame 000003.
- Current assigned front and side edge lengths are 1.43 and 1.97 model units respectively. The source front appears substantially wider than its side, so owner/corner ordering and intrinsic shape must be tested before any station transform export.

- Same side-wall ceiling corner remains visible in all three frames and will provide a noncoplanar direct structural control; near-corner ceiling is clipped in frames 2 and 3 and is not invented.
- CV LSD yielded no usable long corner segments; annotation uncertainty remains explicit, not silently replaced by heuristic line detection.
