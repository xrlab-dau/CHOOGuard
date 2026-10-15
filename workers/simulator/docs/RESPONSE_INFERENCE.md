# Detailed response workflow contract

This module implements user-authorized **INFERRED_TRAINING** response behavior. Confidential KORAIL manuals are not an implementation prerequisite. The procedural detail is a simulator design, not an approved railway SOP, a clinical guideline, a measured site protocol, or evidence of training effectiveness.

The emergency generator remains the existing seeded causal object/NPC kernel. This module contains no emergency scenario list, scenario selector, incident quota, or model/network call. It derives response obligations from locally observed world conditions and delivered reports. A calm observed world creates no emergency-response obligations.

## Evidence and inference

| Element | Basis | Implementation boundary |
| --- | --- | --- |
| Subject, equipment, visible people, resources, location | Existing registered world entities and qualified interaction centers | Retain exact stable object IDs in observations/reports; no remote population oracle |
| Local observation | Actual current state, distance and explicit occlusion observations | Snapshot records what was known at that tick; subsequent world changes do not rewrite the report |
| Crew/staff notification and orderly assisted evacuation | Public passenger-safety principles, including [KORAIL passenger emergency guidance](https://info.korail.com/info/contents.do?key=970) | Site role assignments, authority chains and exact sequences below are inferred |
| Weather-aware reassessment | Public risk-awareness principles, including [KMA heavy-rain safety guidance](https://www.weather.go.kr/w/hazard/safety-guide/heavy-rain.do) | This module does not assert local flooding thresholds or real railway operating rules |
| Role permissions, ACK/readback, request/authorization/handover | Inferred generic coordination design authorized by the user | No claim of exact KORAIL terminology, dispatch permissions or confidential rescue-line procedures |
| Action durations, communication range/delay/loss, confidence | Configurable game parameters in `data/response-profiles.json` | `Confidence=.35` is an explicit inference marker, not a calibrated probability or measured accuracy |
| Cabinet access, isolation, probe check and lockout tag | Explicit local mechanical state from `MechanicalInteractions` | Normalized training mechanism, not real electrical qualification or a field-safe isolation certificate |
| Person assessment/support | Nonclinical observation, consent and local supporter relationship | No diagnosis, CPR dosage/algorithm, magic healing, instant recovery or unverified transport |
| Evacuation | Fresh current navigation evidence plus actual physical arrival | No invented floor, safe path, accessible route, teleportation or instantaneous rescue |

All site-specific parameters are intentionally inferable and configurable. The catalog SHA-256 is stored as eight exact UInt32-valued world fields. A changed or partially bound catalog cannot silently load into an already recorded world. Full source snapshot/geometry binding remains the integration layer's responsibility.

## Local APIs and ownership

`new ResponseWorkflows(world, profilesJson = null)` uses the built-in catalog or an explicitly identified catalog. `InitializeDefaultRoles()` assigns existing NPCs/staff to passenger/station-staff roles and registers explicitly tagged communication equipment. It creates no actors, desks, equipment, source geometry or navigation agents.

The integration layer may create clearly labeled inferred training control/technician/fire/medical actors at source-bound locations, then call `SetRole(id, role)` and `RegisterChannel(id, kind)`. Supported roles are `Passenger`, `StationStaff`, `TrainCrew`, `Control`, `FacilityTechnician`, `FireResponder` and `MedicalResponder`. Roles are world state. Mesh names never grant a capability or permission.

`Apply(ResponseInput)` validates the entire command before mutation. Rejected commands return a refusal outcome and preserve the world digest, journal, resources and sequence. The input/outcome must still be recorded in the simulator tape. Accepted commands consume an increasing per-actor sequence. Timed actions consume no more than one `StepSeconds` of effort per actor per world tick, even if several controls are pressed that tick.

`Step()` advances due virtual deliveries, follows current escort/path observations, completes only actual arrivals, and ends support when contact/consent is lost. It does not increment `World.Tick`. Every mutable observation, message, authorization, part-dependent verification, duration, escort and assistance flag belongs to `World.Entities`; reconstructing this engine on the same world preserves behavior without hidden workflow memory.

`Tasks(actorId)` returns obligations from that actor's fresh observations and delivered reports, plus receipt/readback/authorization tasks for delivered messages. `RecordIds` and `Inspect(id)` expose independent copies of records for the UI. Record tags and links preserve source/object/actor/recipient/resource identities; facts and progress are numeric state. All records are `render-only` so they cannot become duplicated causal hazard bodies.

## Concrete workflow

1. **Observe:** ray/contact and qualified semantic location determine the subject. The engine checks local range and occlusion, then records hazard values, observed people, visible resources, source version, tick and priority. A report cannot invent a remote hazard or person count.
2. **Report or request:** choose a recipient and face-to-face/radio/intercom/PA channel. Equipment power, charge, control reach, endpoint connection and coverage matter. A radio with mechanical controls requires PTT held. `report` shares facts; `request` also names `isolation`, `evacuation`, `medical`, `specialist` or `accessible-assistance`. Sending succeeds before delivery does.
3. **Confirm delivery:** seeded bounded delay and loss are local simulation mechanics. Equipment/network conditions are checked again when delivery is due. A lost report cannot be acknowledged. ACK and content readback are separate actions. The readback code binds the exact subject/observation message; the UI should show its factual report alongside the code.
4. **Authorize and communicate the decision:** a control-role recipient may authorize a delivered, acknowledged, read-back, still-fresh request for an appropriate existing executor. This creates a separate delayed instruction message. The executor must receive, ACK and read back that instruction before execution. A request or a control click does not make an unavailable specialist appear or make the executor omniscient.
5. **Perform and verify:** use actual part/tool mechanics. For isolation verification, a qualified cabinet must have switch at the isolated end stop, `power.isolated`, completed probe check and an applied lockout tag. Partial motion, absent tools or incomplete checks refuse procedural completion. Verification records evidence; it never repairs a fault or globally certifies an object as safe.
6. **Coordinate people and access:** keep-clear warnings need sustained effort. Access control additionally requires an actual nearby deployed barrier. Basic person assessment precedes supportive contact; consent is explicit. Support changes the supporter relation and existing game assistance input, never health/medical condition directly. It ends when the supporter leaves or consent is lost.
7. **Evacuate and hand over:** self-evacuation requires local evidence and a current complete path; directed/accessible evacuation additionally needs a communicated authorization. A goal is not an arrival. The engine waits for actual position feedback, pauses and releases its goal when a destination/path becomes invalid, and requires real qualified transport binding for an immobile person. A specialist handover requires a delivered/read-back report, local specialist and any required person assessment; it does not clear an incident or cure the patient.

Observations expire after a configurable interval. A stale report cannot start new actions or receive a new authorization. An old completed action does not silently become new evidence. Re-observe when conditions or route knowledge change.

## Navigation and contact adapter inputs

Use actual source-bound interaction bounds. Some source meshes have world vertices baked into geometry while their GameObject origin is `(0,0,0)`. The response engine reads `mech.center.x/y/z` when present; it must receive a qualified center rather than a fabricated source transform. Occlusion is `actor.State["response.occluded." + subjectId]` (`1` blocks observation/action).

For a person and an existing destination, the navigation adapter records the following only after real current path/capsule/hazard checks:

```text
person.State["response.route." + destinationId + ".complete"] = 1
person.State["response.route." + destinationId + ".verified.tick"] = world.Tick
person.State["response.route." + destinationId + ".risk"] = observed path exposure
person.State["response.route." + destinationId + ".accessible"] = 1 // only when qualified
```

`risk<=.1` is a normalized training parameter. The response engine never writes these path-evidence fields. Evidence must remain fresh during an escort. A wheelchair/stretcher alone is insufficient: an immobile person requires an explicitly qualified `accessible-transport` entity, `response.transport-bound.<resourceId>=1` from actual attachment/physics, a qualified accessible path, and subsequent real motion. Otherwise the refusal or pending-motion state stays visible.

## Verification scope

`ResponseWorkflowTests.Run()` covers local knowledge/source IDs, role boundaries, immutable inspection copies, delivery versus ACK/readback, distinct decision delivery, incorrect executor role, physical isolation/check/tag interlocks, power/coverage/PTT/outage failures, partial effort/time-budget enforcement, stale observations, actual evacuation arrival, blocked-route reassessment, qualified accessible transport, assessment/consent/support/handover, atomic negative inputs, seeded/order-stable replay, engine reconstruction, full catalog binding and legacy instant-assist bypass refusal.

These are portable state/contract checks. Actual Unity controls, geometry contacts, NPC navigation, graphics, and overall training effectiveness need their own evidence. Public principles and this catalog do not establish field-equivalent fidelity or approved operational procedure.
