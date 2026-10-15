# Detailed local mechanical interactions

This module is `INFERRED_TRAINING`. Its normalized ranges, speeds, friction, spring rates, key classes, charge rates and effect coefficients are simulated training parameters. They are not measured force, an approved KORAIL operating procedure, a confidential manual reconstruction, or a claim that every visible object is operable.

`Runtime/Core/MechanicalInteractions.cs` is engine independent. It stores every part position, grip, carried tool, lock, check progress, tag, charge, contact and duration budget in `World.Entities.State`; the catalog hash is bound to `World.Fields` in eight exact UInt32 words. Resistance variation is sampled once using the seed and stable owner ID. It neither rolls scenarios nor calls models on input. Replay consumes the same ordered control and physics observations, and checks pre/post world digests and outcomes.

`data/mechanical-profiles.json` is the serialized public default catalog. A resource named `MechanicalProfiles` can provide the same catalog to the isolated Unity build. The source defaults remain available when the resource is absent. Profiles are hinged and sliding doors, a breaker cabinet, an extinguisher, a radio, small tools and a generic tool connector. Unknown source objects expose inspection only. Assigning a profile requires an explicit owner and profile; names do not make a mesh movable or create material/hazard capabilities.

## Integration contract

1. After the world, registry and generated training proxies are ready, add `DetailedManipulationController` to the isolated simulator and call `Initialize(simulator)`. This creates inferred controls and four local training tools. The key contains explicit owner-ID compatibility for generated training cabinet locks. Other keys require an explicit `mech.key-owner.<owner-id>` binding.
2. Register only source-qualified runtime door leaves using `RegisterLeaf(ownerId, leaf, closedLocal, openLocal, projectedBySimulator:true)`. Existing typed-door projection remains the owner of those transforms. The controller records actual collider/renderer bounds as the interaction center; baked source vertices often make the GameObject origin unsuitable. No source shared mesh, material or asset is modified.
3. At the fixed tick, after actor observations and before `kernel.Step`, call `CollectInputs()` and `Tick(inputs)`. Add the input and outcome arrays to the simulator tape. Replay supplies the recorded `MechanicalInput[]` instead of collecting live mouse input. Do not call `Tick` twice at the same world tick.
4. Initialize source centers, inferred capabilities, mechanical parts, roles, channels and tools before the initial model request and its pre-digest. The generated record's post-digest must equal the eventual run-header initial digest exactly. StartRun rebinds idempotently to that complete baseline; replay rebinds to its saved initialized world and rejects any digest change. Held controls and pending input queues are reset during replay loading.
5. Disable legacy success-button operations for owners with `mech.profile == 1`, including indirect suppression through a carried detailed extinguisher. Otherwise the legacy operation catalog can bypass locks, partial movement, the pin, tools, checks and charge use. NPC operations need the same gate or actual mechanical inputs.

Portable API:

```csharp
var mechanics = new MechanicalInteractions(world, profilesJson: null);
mechanics.InitializeDefaults(registry);
mechanics.Attach(explicitOwnerId, "hinged-door");
var inspection = mechanics.Inspect(anyKnownEntityId);
JArray outcomes = mechanics.Tick(recordedOrQueuedInputs);
string tape = mechanics.SerializeRecording();
// On the same initialized pre-state and tick:
otherMechanics.Replay(JObject.Parse(recordedLine));
```

Each input contains tick, sequence, actor, owner, part, tool, action, analog effort, duration, contact and clearance. Durations cannot exceed the fixed tick, including multiple inputs to the same part. Malformed input and wrong key/tool, ungripped, locked, jammed, occluded, blocked and invalid-sequence inputs leave state, resources, ownership and journal unchanged. Batch tick and duplicate-sequence validation runs before applying the batch. Independent valid inputs in the same batch can still succeed when another input is rejected.

## Controls and local effects

- Left mouse grips a real hit part; release relinquishes the grip. Drag or wheel applies analog movement. Hold Shift to reverse it. Pressed controls move while held and return by their simulated spring after release. Sub-tick press/release events retain their partial duration in the fixed-tick queue.
- F carries a small tool or body. Releasing its grip retains it as carried equipment; G places it after a local clearance query. Tab selects the tool to use, but a selected tool must actually be carried. V/B connects or disconnects a compatible socket after seating/loosening its parts.
- Cabinet work requires opening the cover, moving the switch to its off stop, a held compatible voltage probe for a cumulative one-second local check, and a carried tag. Applying a tag consumes one finite tag and prevents reenergizing. The public state keys are `mech.part.switch`, `power.isolated`, `mech.checked` and `mech.tagged`.
- An extinguisher requires pulling its pin before squeezing the lever. Charge decreases with lever state and duration. The nozzle supplies an actual Unity ray observation; the portable core validates origin proximity, a normalized direction, target-point proximity, a forward hit within four MODEL units, and the target's existing explicit `thermal` capability. A qualified hit reduces that target's simulated fire and adds a finite local deposit. Wrong aim still wastes charge. No target name grants a fire capability. Releasing the spring lever stops flow.
- Radio power, channel, push-to-talk, charge and receiver availability are separate state. Holding PTT alone does not establish a contact. A one-second contact check with an existing radio/communications receiver qualifies delivery; loss of owner or receiver availability clears that contact.
- Registered leaf clearance uses `SourceLeafClearance.Measure`: actual indexed triangle vertices, centroids and a surface sampling grid move through a finite translation. Broad bounds select possible colliders; forward and reverse per-collider rays test the actual surfaces, while actual NPC capsules use a small skin sweep. Initial tangent/separating primitive contacts are reported. No train-interior name filter or source collider disabling is used. Unreadable geometry or exhausted query budgets refuse movement. Results include sample resolution, triangle/test counts, hit points and source blockers. This is a measured sampled sweep, with finite spatial resolution; replay projects its recorded clearance rather than independently resimulating physics.

## Verification boundary

`Tests/MechanicalInteractionsTests.cs` checks partial motion, exact key/tool compatibility, latch/lock/jam/contact/clearance rejection, springs, duration limits, cabinet check/tag order and resources, aimed discharge and wrong-aim charge waste, carry/place/connect, radio contact/power, exact digest replay, source inspection without motion, and absence of incident forcing. The bounded portable test receipt is `output/simulator/mechanical-checks/receipt.json`.

`PlatformSeededSimulator` defaults to detailed mode for a complete snapshot. Its public `DetailedControls`, `ResponseControls`, `QueueMechanicalInput` and `QueueResponseInput` expose the same queued fixed-tick path to native acceptance. `ResponseInteractionController.Engine` exposes the portable workflow engine. The run tape contains `mechanical_inputs`, `mechanical_outcomes`, `response_inputs`, `response_outcomes`, `response_observations`, and existing motion/navigation observations. Replay projects recorded observations and requires exact outcomes, transitions, navigation state and digests.

The source/API compiler passes against installed Unity assemblies. That proves compilation only. Runtime acceptance still requires the authorized isolated Unity build: click each real part, exercise short press/release and held controls, verify both leaves of all nine typed doors, block each leaf with a source object and NPC capsule, verify capsule navigation at partial openings, perform cabinet negative sequences, aim a discharge at an explicit thermal target through clear and occluded rays, and replay the same tape. Shared scene save/reopen and original geometry were not accessed by this implementation lane.
