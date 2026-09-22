# Next Astra-low maker lane: specialist-runtime open-world client

Root decision after Jev007 and latest user clarifications. No new story. This task is queued after engine worker source/protocol becomes usable. Only Astra low implements; root owns all live Editor mutation and final evidence.

## Accepted target
Primary client is a scenario author exploring RANDOM emergencies for insight. Scenario document generation/export is EXCLUDED MVP. Later tutorial mode is future work. Current incidents: station fire/smoke AND crowd/bottleneck/evacuation/medical operations. Game controls and 3D art may abstract; underlying calculation/outcome needs specialist models. Do not promote current20s timer, animation, runtime smoke run or uncalibrated reference case to validated Busan twin.

Open world covers actual Busan station–Choryang–Jungang–Nampo–North Port, not just station. Use actual OSM roads/building locations and source station footprint. Game visual heights/details may simplify. Management-game HUD inspired by SimCity/Jurassic World Evolution3/RTS: full-screen world, top status, bottom tools, selected-unit/objectives overlay, all Korean. Current overbright tiny cyan station inside a dashboard is inadequate (Assets/.planning/2026-09-20-integrated-build/busan-korean-first.png).

## Write ownership
After engine-source handoff the same worker may exclusively own new App/Mvp/MvpPhysicsBridge.cs and MvpOpenWorldCamera.cs, existing MvpTrainingDirector.cs/MvpWorkspace.cs/MvpStationView.cs and Editor/MvpWorkspaceBuilder.cs/MvpStationBuilder.cs. No other Unity writer is active. Source geometry/data intake root-owned. Never modify unrelated scene roots or source evidence. Root invokes builders and saves the owned scene.

## Runtime contract
Interpreter workers/physics/.venv/bin/python; entry workers/physics/worker.py. Existing engine-lane protocol contract is authoritative. JSONL stdout only, local subprocess, no localhost. HELLO protocolVersion1; SUBMIT runId,generation,seed,population,scenario fire_smoke|crowd_medical,action start|advance|warn|evacuate|medical,stepSeconds; CANCEL. RESULT runId,generation,seed,simTime,engine,engineVersion,fdsStatus,fdsVersion,fireRequired,physicsReady,allEvacuated,total,evacuated,density,pressureIndicator,visibility,temperature,unsupported,agents[{id,x,z}], additive provenance allowed. Referencehall coordinates30x20m and its mapping must stay explicit, not whole-city physical accuracy.

Bridge: reader queue, parse/apply only main thread, dispose/cancel on scene teardown, bounded lines/queue, reject wrong generation, one advance in flight. Director: random seed/incident population; explicit warnings/evacuation/medical and team actions; advance from solver simTime, pause requests, local speed1/2/4; remove20s success. Missing/stale/unsupported required results -> Korean compute/unavailable state, no fake clear. Log actual user actions/results in memory; no draft export. Render actual solver positions with no separate movement authority. Pressure diagnostic is not physical crush force; medical assistance is not clinical outcome.

## World/UI
Station source built and saved successfully. Preserve imported TrainKitMvp bullet models, animated MiniCharacters, Noto SDF, Lucide icons. Render main3D camera directly, Pan WASD/mouse drag, scroll zoom, orbit; respect UI input capture. City source expanded boundary being acquired by root under asset-library/space-references/busan-reconstruction/. Chunk/merge static low-poly buildings/roads; avoid thousands of scripts/colliders. Exposure/material/camera bounds must show readable world objects. Bind components through Awake/OnEnable so saved scene works after play reload. Builders may refresh owned content idempotently, never overwrite unrelated scene objects.

No TDD/full suite. Root will do compile + one actual engine/client flow and representative viewport check. Report only compact graph with changed files, API and precise unverified scope. Stop when source ready or concrete blocker.
