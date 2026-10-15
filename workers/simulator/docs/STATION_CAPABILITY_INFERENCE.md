# Station capability inference

`StationCapabilityInference` adds explicitly labelled `INFERRED_TRAINING` helper entities after the immutable `WorldObjectRegistry` has inserted the full source inventory. It does not change source object records, mesh references, transforms, materials, active flags, the causal kernel or Unity geometry.

```csharp
var inference = new StationCapabilityInference(profileJson);
StationCapabilityReceipt receipt = inference.Apply(world, registry);
```

The caller must stage the exact `data/station-capability-profiles.json` bytes and bind them into its source manifest. `Apply` additionally stores all 256 profile hash bits, the registry fingerprint and the inference version as exact UInt32 words in `World.Fields`. This makes a profile or source change invalidate the world/replay digest.

## Evidence and grouping

Narrow generated source names identify candidate columns, wall/glazing/ceiling envelopes, door assemblies, explicit WOOD_DISPLAY_LINER material, lamps, downlights, monitors, elevator/escalator assemblies and named restroom fixtures. A generic `LIGHT` substring is deliberately insufficient: sidelights are glazing and stair flights are unrelated. Typed source associations, including the current KTX subtree, are excluded from new causes.

Active source parts aggregate into a semantic assembly by explicit source parent, stable family capture or shop envelope. This avoids independent failure trials for each glass frame, shade, trim, LOD fragment or render triangle. Existing source parents and every contributing source ID are retained as evidence links. Bounds from qualified serialized/native collider envelopes locate the actual assembly center. Disabled-collider bounds can locate visual geometry but certify no physical collision. Unsupported bounds leave the candidate in the unknown ledger.

The existing 1F hierarchy supplies an abstract public-space context volume. Its center is an envelope center, not a walking point. It gains no walkable, destination, exit or navigation capability; native source floor probes own those claims.

## Causal assumptions

The JSON profiles define all thermal, structural, fuel and utility assumptions. Opaque walls and glazing acquire no combustion or electrical capability. Only the explicit source-model wood-liner label selects the wood-like material profile. The normalized capacities, ignition limits, conductivity and wear values are game parameters, not measured site properties or incident frequencies.

An inferred electrical assembly links to one explicitly owned supply network per source zone. That network's initial energization is a visible profile assumption. An inferred restroom fixture links to one explicit pressurized water network per restroom zone. Neither network adds pipe/cable geometry or claims an as-built route. Isolation removes energy or pressure through the existing kernel. Initial fault/fire/leak flags are zero; profiles cannot seed incident flags or select an emergency scenario.

Thermal relationships connect only same-zone envelopes whose qualified bounds intersect within 0.1 model unit. This is an explicitly inferred adjacency hypothesis, not a contact or heat-transfer measurement. AABB candidates cannot establish occlusion, fire compartmentation or actual conduction. The receipt records this limitation.

## Door motion

Door helpers are passive barrier envelopes. The receipt reports an exact single active geometry-qualified leaf candidate only when its source name explicitly identifies a leaf or stall door; frames, signs and walls are excluded. Bounds do not establish hinge side, pivot, axis, travel, lock or clearance. No `door` capability, `source-leaf:` tag or source motion is granted. A separate reviewed mechanical contract must qualify movement.

## Atomicity and verification

`Apply` validates all profiles, source hashes, derived IDs and associations before modifying the world. Reapplying the same input is idempotent and preserves evolved mutable causal state. Derived IDs and seeded wear/insulation streams depend on stable assembly identity, so an unrelated source part cannot shift them. A stale or partial profile binding and a collision at any derived ID reject before mutation.

The focused tests cover grouping, inactive and unknown parts, typed aliases, material overreach, forced-incident rejection, stable streams, atomicity, full 4,115-object insertion and source-bound idempotence. A boundary fixture supplies water and an energized owned circuit, then repeats it with the supply isolated; only the powered fixture can produce electrical fault energy. That high test rate is confined to the fixture and is absent from production profiles.

The receipt contains assembly evidence, network ownership, profile assumptions, door motion candidates and unknown/unsupported categories. These are bounded training semantics. They do not certify all station equipment, room accessibility, material composition, public procedures or physical operation.
