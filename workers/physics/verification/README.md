# Bounded pedestrian component verification

Run from repository root:

```sh
workers/physics/.venv/bin/python workers/physics/verification/run_probes.py
```

`prespecified.json` fixes expectations before simulation. `receipt.json` records results, versions and SHA-256 digests; `trajectories.jsonl` retains each integration step. These use installed JuPedSim 1.4.2 CFSV3 with dt=0.05 s, as the worker does, but do not execute worker policy/fire coupling.

[NIST TN 1822 (2013)](https://nvlpubs.nist.gov/nistpubs/TechnicalNotes/NIST.TN.1822.pdf), sections 3.1.2 and 3.1.5, motivates assigned-speed, corner-boundary, and flow-constraint probes. The 40m speed measurement is performed at 0 and 45 degrees. The L geometry and 40-person flat bottleneck are adaptations, not exact NIST fixtures. A 0.05m corridor tolerance represents one integration step, and 1e-9m is geometry arithmetic tolerance. The 120s limit is an execution cap. No universal empirical accuracy or flow acceptance threshold is inferred. Width comparison is qualitative consistency only. Real-site validation requires independent observations, their uncertainties and intended-use criteria; it remains uncovered.

Results: 40m free travel in 40s at both angles; 20/20 corner exits at 31.15s; 40/40 exits through the 1m outlet at 40.8s versus 26.0s through 2m. No sampled center or step segment leaves the geometry. This does not establish whole-body clearance or pairwise non-overlap.

## Observed removal API discrepancy

The installed `Simulation.removed_agents()` docstring says returned agents are no longer accessible. Actual output returns IDs still present in `agents()` in that frame, disappearing on the following iteration. The initial observer therefore falsely failed conservation; `initial-api-semantics-receipt.json` preserves that observation. The revised observer counts actual identity-set departures and separately records API overlap steps (20, 39, 36). This is an observer correction, not relaxed conservation tolerance. Production accounting should explicitly distinguish pending and completed removal.

## Contact force source audit

Official version-pinned sources are retained under `sources/` with digests in the receipt:

- [SocialForceModel.cpp v1.4.2](https://raw.githubusercontent.com/PedestrianDynamics/jupedsim/v1.4.2/libsimulator/src/SocialForceModel.cpp), `ForceBetweenPoints`: overlap activates body stiffness times overlap and tangential friction terms. The total also includes exponential social repulsion. Its public Python state exposes velocity, mass and parameters, but no decomposed pair/wall force telemetry. A carefully matched reconstruction or engine instrumentation could report model-estimated contact force in N; it would require exact neighbor/wall selection and timestep states. Net acceleration or total social force cannot stand in for contact load. No calibrated physical load is established by selecting this model.
- [GeneralizedCentrifugalForceModel.cpp v1.4.2](https://raw.githubusercontent.com/PedestrianDynamics/jupedsim/v1.4.2/libsimulator/src/GeneralizedCentrifugalForceModel.cpp): neighbor repulsion depends on effective ellipse separation and relative velocity, with interpolation/caps; force-based nomenclature does not supply body-compression contact measurement. No public contact-force telemetry was found in the installed Python model API.

[Official SocialForce Python documentation](https://www.jupedsim.org/stable/_modules/jupedsim/models/social_force.html) describes body stiffness and friction units. Installed source and pinned C++ are the version authority; stable web docs may change. CFSV3 density × velocity variance is not Pa or contact force. No model switch was made.

Highest-value next change: establish an independent experimental bottleneck calibration/holdout dataset with measured widths, initial positions and flow/time uncertainty, then quantify CFSV3 residuals and timestep sensitivity. Separately implement an instrumented SocialForce research candidate only if contact load is required; validate force decomposition and numerical stability before any runtime replacement.
