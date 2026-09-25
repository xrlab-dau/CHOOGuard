using System;
using System.Collections.Generic;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;

namespace ChooGuard.Domain.Gameplay
{
    public sealed class ActionEffect
    {
        public bool Allowed { get; }
        public bool Complete { get; }
        public string Reason { get; }
        public IReadOnlyList<WorldEntity> Entities { get; }
        private ActionEffect(bool allowed, bool complete, string reason, params WorldEntity[] entities)
        { Allowed = allowed; Complete = complete; Reason = reason; Entities = Array.AsReadOnly(entities); }
        public static ActionEffect Block(string reason) => new ActionEffect(false, false, reason);
        public static ActionEffect Apply(bool complete, string reason, params WorldEntity[] entities) => new ActionEffect(true, complete, reason, entities);
    }

    /// <summary>The same physical/authorization predicates serve human and autonomous actor actions.</summary>
    public static class ActionRules
    {
        // The authored gameplay/tutorial placement radius, not a railway safety clearance.
        public const float CordonPlacementDistance = 2f;
        public static bool RoleAllows(ActorRole role, ActionVerb verb, WorldEntity target)
        {
            switch (verb)
            {
                case ActionVerb.Install:
                    return role == ActorRole.Maintenance || (role == ActorRole.StationStaff && target.DefinitionId == "inspection.tag.mount");
                case ActionVerb.Isolate: case ActionVerb.Remove: case ActionVerb.Fasten:
                case ActionVerb.Unfasten: case ActionVerb.Measure: return role == ActorRole.Maintenance;
                case ActionVerb.Clean: case ActionVerb.Refill: case ActionVerb.DisposeWaste:
                    return role == ActorRole.Cleaning || role == ActorRole.PassengerService;
                case ActionVerb.Cordon: return role == ActorRole.StationStaff || role == ActorRole.Maintenance;
                case ActionVerb.Verify:
                    return target.Kind == EntityKind.Part || target.Kind == EntityKind.Equipment || target.Kind == EntityKind.Socket
                        ? role == ActorRole.Maintenance : role != ActorRole.Citizen;
                default: return Enum.IsDefined(typeof(ActionVerb), verb);
            }
        }
        public static bool NeedsReservation(ActionVerb verb)
        {
            return verb != ActionVerb.Wait && verb != ActionVerb.Observe && verb != ActionVerb.MoveTo &&
                verb != ActionVerb.Report && verb != ActionVerb.RequestHelp && verb != ActionVerb.Consent;
        }
        public static ActionEffect Apply(IReadOnlyDictionary<StableId, WorldEntity> entities, ActorState actor,
            ActorActionIntent intent, ActionEvidence evidence)
        {
            if (!entities.TryGetValue(intent.TargetId, out var target)) return ActionEffect.Block("target_missing");
            if (!RoleAllows(actor.Role, intent.Verb, target)) return ActionEffect.Block("role_not_authorized");
            if (intent.Verb == ActionVerb.Wait) return ActionEffect.Apply(true, "waiting_for_relevant_change");
            if (evidence == null || evidence.TargetRevision != target.Revision) return ActionEffect.Block("current_evidence_required");
            if (intent.Verb == ActionVerb.MoveTo)
                return evidence.ActorPosition.DistanceSquared(target.Position) <= 1.0f
                    ? ActionEffect.Apply(true, "arrived") : ActionEffect.Apply(false, "approaching");
            if (!evidence.Visible) return ActionEffect.Block("occluded_or_unobserved");
            if (evidence.ActorPosition.DistanceSquared(target.Position) > 9f) return ActionEffect.Block("out_of_reach");
            bool conversational = intent.Verb == ActionVerb.Observe || intent.Verb == ActionVerb.Report || intent.Verb == ActionVerb.RequestHelp || intent.Verb == ActionVerb.Consent;
            if (!conversational && (!evidence.Contact || evidence.ContactPosition.DistanceSquared(target.Position) > 4f)) return ActionEffect.Block("physical_contact_required");
            var facts = TransitionKernel.Copy(target.Facts);
            var values = TransitionKernel.Copy(target.Measurements);
            WorldEntity tool = null;
            if (intent.ToolId.HasValue && !entities.TryGetValue(intent.ToolId.Value, out tool)) return ActionEffect.Block("tool_missing");
            string point = intent.WorkPointId ?? "main";
            switch (intent.Verb)
            {
                case ActionVerb.Observe:
                    facts["inspected"] = RuleTruth.TRUE;
                    facts["observed:" + actor.Id.Value + ":" + point] = RuleTruth.TRUE;
                    facts["observed:" + point] = RuleTruth.TRUE;
                    break;
                case ActionVerb.PickUp:
                    if (target.Fact("portable") != RuleTruth.TRUE) return ActionEffect.Block("carrying_permission_unknown");
                    if (target.Fact("installed") != RuleTruth.FALSE) return ActionEffect.Block("detachment_unconfirmed");
                    if (target.CustodianId.HasValue && !target.CustodianId.Value.Equals(actor.Id)) return ActionEffect.Block("held_by_other_actor");
                    if (target.Kind == EntityKind.SuspiciousItem || target.Fact("hazardous") != RuleTruth.FALSE) return ActionEffect.Block("unsafe_or_unknown_item");
                    if (target.Kind == EntityKind.PersonalItem && target.Fact("consent:" + actor.Id.Value) != RuleTruth.TRUE) return ActionEffect.Block("owner_consent_required");
                    foreach (var entity in entities.Values)
                        if (entity.Kind != EntityKind.Actor && !entity.Id.Equals(target.Id) && HeldBy(entity, actor.Id)) return ActionEffect.Block("hands_occupied");
                    var pickedUp = target.With(Next(target), custodianId: actor.Id, replaceCustodian: true, replaceParent: true);
                    var uncovered = RevalidateCordonTarget(entities, target, target.Position, true);
                    return uncovered == null ? ActionEffect.Apply(true, "custody_received", pickedUp) :
                        ActionEffect.Apply(true, "custody_received", pickedUp, uncovered);
                case ActionVerb.PutDown:
                    if (target.Kind == EntityKind.Actor) return ActionEffect.Block("support_responsibility_is_not_a_carried_object");
                    if (!HeldBy(target, actor.Id)) return ActionEffect.Block("not_in_actor_custody");
                    if (!evidence.Supported || !evidence.Aligned) return ActionEffect.Block("stable_clear_support_required");
                    StableId? supportId = null;
                    if (intent.RecipientId.HasValue)
                    {
                        if (!entities.TryGetValue(intent.RecipientId.Value, out var support) ||
                            (support.Kind != EntityKind.Container && support.Kind != EntityKind.Surface) ||
                            support.Position.DistanceSquared(evidence.ContactPosition) > 2.25f) return ActionEffect.Block("real_nearby_support_required");
                        if (!evidence.SupportId.HasValue || !evidence.SupportId.Value.Equals(support.Id))
                            return ActionEffect.Block("selected_support_not_physically_confirmed");
                        supportId = support.Id;
                    }
                    return ActionEffect.Apply(true, "placed_on_support", target.With(Next(target), position: evidence.ContactPosition,
                        replaceCustodian: true, parentId: supportId, replaceParent: true));
                case ActionVerb.Open: case ActionVerb.Close:
                    if (target.Kind != EntityKind.Door && target.Kind != EntityKind.Container && target.Kind != EntityKind.Equipment) return ActionEffect.Block("not_openable");
                    if (target.Fact("access.permitted") != RuleTruth.TRUE) return ActionEffect.Block("access_not_authorized");
                    if (target.Kind == EntityKind.Equipment && target.Fact("isolated") != RuleTruth.TRUE) return ActionEffect.Block("isolation_unconfirmed");
                    if (!evidence.Aligned) return ActionEffect.Apply(false, "joint_not_at_requested_position");
                    facts["open"] = intent.Verb == ActionVerb.Open ? RuleTruth.TRUE : RuleTruth.FALSE;
                    break;
                case ActionVerb.Isolate:
                    if (target.Fact("isolation.procedure.known") != RuleTruth.TRUE || target.Fact("isolation.permitted") != RuleTruth.TRUE)
                        return ActionEffect.Block("approved_isolation_procedure_missing");
                    if (!evidence.Aligned) return ActionEffect.Apply(false, "isolation_device_not_in_position");
                    facts["power.on"] = RuleTruth.FALSE; facts["isolated"] = RuleTruth.TRUE;
                    // Voltage absence is a separate measurement; never inferred from a switch position.
                    break;
                case ActionVerb.Install:
                    if (target.Kind != EntityKind.Socket || tool == null || tool.Kind != EntityKind.Part) return ActionEffect.Block("part_and_socket_required");
                    if (!HeldBy(tool, actor.Id) || tool.Fact("installed") != RuleTruth.FALSE) return ActionEffect.Block("part_custody_or_detachment_required");
                    if (target.Fact("accepts:" + tool.DefinitionId) != RuleTruth.TRUE || target.Fact("occupied") != RuleTruth.FALSE) return ActionEffect.Block("incompatible_or_occupied_socket");
                    bool tagMount = target.DefinitionId == "inspection.tag.mount" && tool.DefinitionId == "inspection.tag";
                    if (actor.Role == ActorRole.StationStaff && !tagMount) return ActionEffect.Block("only_inspection_tag_install_authorized");
                    if (!tagMount && target.Fact("isolated") != RuleTruth.TRUE) return ActionEffect.Block("isolation_unconfirmed");
                    if (!evidence.Aligned || !evidence.Supported) return ActionEffect.Block("alignment_and_support_required");
                    facts["occupied"] = RuleTruth.TRUE; facts["occupant:" + tool.Id.Value] = RuleTruth.TRUE;
                    var partFacts = TransitionKernel.Copy(tool.Facts); partFacts["installed"] = RuleTruth.TRUE; partFacts["verified"] = RuleTruth.UNKNOWN;
                    return ActionEffect.Apply(true, "installed_not_fastened", target.With(Next(target), facts: facts),
                        tool.With(Next(tool), position: target.Position, facts: partFacts, replaceCustodian: true, parentId: target.Id, replaceParent: true));
                case ActionVerb.Remove:
                    if (target.Kind != EntityKind.Part || target.Fact("installed") != RuleTruth.TRUE) return ActionEffect.Block("installed_part_required");
                    foreach (var held in entities.Values)
                        if (held.Kind != EntityKind.Actor && !held.Id.Equals(target.Id) && HeldBy(held, actor.Id)) return ActionEffect.Block("hands_occupied");
                    if (!evidence.Supported) return ActionEffect.Block("support_required_before_final_release");
                    foreach (var fact in target.Facts)
                        if (fact.Key.StartsWith("required.fastener:", StringComparison.Ordinal) && fact.Value == RuleTruth.TRUE &&
                            target.Fact("released:" + fact.Key.Substring(18)) != RuleTruth.TRUE) return ActionEffect.Block("fasteners_not_released");
                    if (!target.ParentId.HasValue || !entities.TryGetValue(target.ParentId.Value, out var socket) || socket.Fact("isolated") != RuleTruth.TRUE)
                        return ActionEffect.Block("isolation_unconfirmed");
                    facts["installed"] = RuleTruth.FALSE; facts["verified"] = RuleTruth.UNKNOWN;
                    var socketFacts = TransitionKernel.Copy(socket.Facts); socketFacts["occupied"] = RuleTruth.FALSE; socketFacts["occupant:" + target.Id.Value] = RuleTruth.FALSE;
                    return ActionEffect.Apply(true, "part_removed_to_custody", target.With(Next(target), facts: facts, custodianId: actor.Id, replaceCustodian: true, replaceParent: true),
                        socket.With(Next(socket), facts: socketFacts));
                case ActionVerb.Fasten: case ActionVerb.Unfasten:
                    if (tool == null || !HeldBy(tool, actor.Id) || tool.Fact("tool.fastening") != RuleTruth.TRUE) return ActionEffect.Block("compatible_held_tool_required");
                    if (target.Fact("required.fastener:" + point) != RuleTruth.TRUE) return ActionEffect.Block("authored_fastener_required");
                    if (target.Fact("installed") != RuleTruth.TRUE || !evidence.Aligned) return ActionEffect.Block("installed_aligned_part_required");
                    if (target.Fact("practice.fixture") != RuleTruth.TRUE) return ActionEffect.Block("approved_torque_angle_sequence_missing");
                    if (intent.Verb == ActionVerb.Unfasten && !evidence.Supported) return ActionEffect.Block("support_required_before_release");
                    string progressKey = (intent.Verb == ActionVerb.Fasten ? "progress:fasten:" : "progress:unfasten:") + point;
                    string reverseKey = (intent.Verb == ActionVerb.Fasten ? "progress:unfasten:" : "progress:fasten:") + point;
                    double previousProgress = Measure(values, progressKey, SiUnit.Dimensionless, 0);
                    double progress = Math.Min(1, previousProgress + evidence.Work);
                    if (evidence.Work <= 0) return ActionEffect.Apply(false, "waiting_for_tool_motion");
                    facts["fastened:" + point] = RuleTruth.FALSE;
                    facts["released:" + point] = RuleTruth.FALSE;
                    values[progressKey] = new SiValue(progress, SiUnit.Dimensionless);
                    values[reverseKey] = new SiValue(Math.Max(0,
                        Measure(values, reverseKey, SiUnit.Dimensionless, 1 - previousProgress) - (progress - previousProgress)), SiUnit.Dimensionless);
                    if (progress >= 1)
                    {
                        facts["fastened:" + point] = intent.Verb == ActionVerb.Fasten ? RuleTruth.TRUE : RuleTruth.FALSE;
                        facts["released:" + point] = intent.Verb == ActionVerb.Unfasten ? RuleTruth.TRUE : RuleTruth.FALSE;
                    }
                    facts["practice.verified"] = RuleTruth.FALSE;
                    return ActionEffect.Apply(progress >= 1, "practice_fastener_progress_not_certified_torque", target.With(Next(target), facts: facts, measurements: values));
                case ActionVerb.Verify:
                    if (target.Fact("practice.fixture") != RuleTruth.TRUE) return ActionEffect.Block("approved_acceptance_limits_missing");
                    bool hasRequirement = false;
                    foreach (var fact in target.Facts)
                    {
                        if (!fact.Key.StartsWith("required.fastener:", StringComparison.Ordinal) || fact.Value != RuleTruth.TRUE) continue;
                        hasRequirement = true;
                        if (target.Fact("fastened:" + fact.Key.Substring(18)) != RuleTruth.TRUE) return ActionEffect.Block("unfastened_or_unknown_point");
                    }
                    if (target.Kind == EntityKind.Surface)
                    { hasRequirement = true; if (target.Fact("dirty") != RuleTruth.FALSE || target.Fact("wet") != RuleTruth.FALSE) return ActionEffect.Block("residue_or_dryness_unconfirmed"); }
                    if (!hasRequirement) return ActionEffect.Block("completion_requirements_missing");
                    facts["practice.verified"] = RuleTruth.TRUE;
                    break;
                case ActionVerb.Measure:
                    if (tool == null || !HeldBy(tool, actor.Id) || tool.Fact("tool.measurement") != RuleTruth.TRUE || tool.Fact("calibration.valid") != RuleTruth.TRUE)
                        return ActionEffect.Block("valid_calibrated_instrument_required");
                    if (!evidence.Aligned || !target.Measurements.TryGetValue("sample:" + point, out var sample)) return ActionEffect.Block("valid_stable_sample_unavailable");
                    values["recorded:" + point] = sample; facts["sample.valid:" + point] = RuleTruth.TRUE;
                    break;
                case ActionVerb.Clean:
                    if (tool == null || !HeldBy(tool, actor.Id) || tool.Fact("tool.cleaning") != RuleTruth.TRUE) return ActionEffect.Block("held_cleaning_tool_required");
                    if (tool.Fact("contaminated") != RuleTruth.FALSE || target.Fact("safe.material.known") != RuleTruth.TRUE) return ActionEffect.Block("tool_or_material_unsafe_unknown");
                    if (!values.TryGetValue("soil:" + point, out var soil) || soil.Unit != SiUnit.Dimensionless) return ActionEffect.Block("authored_contact_region_required");
                    if (!tool.Measurements.TryGetValue("supply", out var supply) || supply.Unit != SiUnit.CubicMetre || supply.Value <= 0) return ActionEffect.Block("consumable_empty_or_unknown");
                    if (!tool.Measurements.TryGetValue("consumption.per-work", out var consumption) || consumption.Unit != SiUnit.CubicMetre || consumption.Value <= 0)
                        return ActionEffect.Block("authored_consumption_rate_missing");
                    if (evidence.Work <= 0) return ActionEffect.Apply(false, "waiting_for_contact_stroke");
                    // This is authored visible residue coverage, not a chemical/sterilization model.
                    double work = Math.Min(evidence.Work, Math.Min(soil.Value, supply.Value / consumption.Value));
                    if (work <= 0) return ActionEffect.Apply(true, "contact_region_clear");
                    values["soil:" + point] = new SiValue(Math.Max(0, soil.Value - work), SiUnit.Dimensionless);
                    var toolValues = TransitionKernel.Copy(tool.Measurements); toolValues["supply"] = new SiValue(Math.Max(0, supply.Value - work * consumption.Value), SiUnit.CubicMetre);
                    bool clear = true;
                    foreach (var value in values) if (value.Key.StartsWith("soil:", StringComparison.Ordinal) && value.Value.Value > 0) clear = false;
                    facts["dirty"] = clear ? RuleTruth.FALSE : RuleTruth.TRUE; facts["wet"] = RuleTruth.TRUE;
                    return ActionEffect.Apply(clear, "visible_residue_only_dryness_unconfirmed", target.With(Next(target), facts: facts, measurements: values), tool.With(Next(tool), measurements: toolValues));
                case ActionVerb.Refill:
                    if (tool == null || !HeldBy(tool, actor.Id) || target.Fact("accepts:" + tool.DefinitionId) != RuleTruth.TRUE) return ActionEffect.Block("compatible_supply_custody_required");
                    if (!tool.Measurements.TryGetValue("supply", out var available) || !values.TryGetValue("capacity", out var capacity) || !values.TryGetValue("supply", out var current) ||
                        available.Unit != capacity.Unit || current.Unit != capacity.Unit || available.Value <= 0) return ActionEffect.Block("supply_or_capacity_unconfirmed");
                    double transfer = Math.Min(available.Value, Math.Max(0, capacity.Value - current.Value));
                    if (transfer <= 0) return ActionEffect.Block("container_full");
                    values["supply"] = new SiValue(current.Value + transfer, current.Unit);
                    var remaining = TransitionKernel.Copy(tool.Measurements); remaining["supply"] = new SiValue(available.Value - transfer, available.Unit);
                    return ActionEffect.Apply(true, "supply_transferred", target.With(Next(target), measurements: values), tool.With(Next(tool), measurements: remaining));
                case ActionVerb.DisposeWaste:
                    if (!HeldBy(target, actor.Id) || target.Fact("waste.permitted") != RuleTruth.TRUE || target.Kind == EntityKind.PersonalItem || target.Kind == EntityKind.SuspiciousItem)
                        return ActionEffect.Block("permitted_waste_custody_required");
                    if (!intent.RecipientId.HasValue || !entities.TryGetValue(intent.RecipientId.Value, out var bin) || bin.Kind != EntityKind.Container || bin.Fact("capacity.available") != RuleTruth.TRUE)
                        return ActionEffect.Block("known_available_waste_container_required");
                    if (bin.Position.DistanceSquared(evidence.ContactPosition) > 2.25f) return ActionEffect.Block("waste_container_out_of_reach");
                    return ActionEffect.Apply(true, "waste_contained_not_destroyed", target.With(Next(target), position: bin.Position, replaceCustodian: true, parentId: bin.Id, replaceParent: true));
                case ActionVerb.Report:
                    facts["reported"] = RuleTruth.TRUE;
                    facts["report:" + actor.Id.Value + ":" + point] = RuleTruth.TRUE;
                    break;
                case ActionVerb.RequestHelp: facts["assistance.requested"] = RuleTruth.TRUE; break;
                case ActionVerb.Consent:
                    if (!target.Id.Equals(actor.Id) || !intent.RecipientId.HasValue) return ActionEffect.Block("only_actor_can_grant_own_consent");
                    facts["consent:" + intent.RecipientId.Value.Value] = RuleTruth.TRUE;
                    break;
                case ActionVerb.Escort:
                    if (target.Kind != EntityKind.Actor || target.Fact("consent:" + actor.Id.Value) != RuleTruth.TRUE) return ActionEffect.Block("person_consent_required");
                    if (target.CustodianId.HasValue && !target.CustodianId.Value.Equals(actor.Id)) return ActionEffect.Block("support_responsibility_already_assigned");
                    return ActionEffect.Apply(true, "escort_agreed_not_arrival", target.With(Next(target), parentId: actor.Id, replaceParent: true,
                        custodianId: actor.Id, replaceCustodian: true));
                case ActionVerb.Handoff:
                    if (!HeldBy(target, actor.Id) || !intent.RecipientId.HasValue || !entities.TryGetValue(intent.RecipientId.Value, out var recipient) || recipient.Kind != EntityKind.Actor)
                        return ActionEffect.Block("custody_and_receiver_required");
                    if (recipient.Fact("consent:" + actor.Id.Value) != RuleTruth.TRUE) return ActionEffect.Block("receiver_acceptance_required");
                    if (recipient.Position.DistanceSquared(evidence.ActorPosition) > 3.24f) return ActionEffect.Block("receiver_not_arrived");
                    if (target.Kind == EntityKind.Actor)
                    {
                        if (target.Fact("consent:" + recipient.Id.Value) != RuleTruth.TRUE || target.Position.DistanceSquared(recipient.Position) > 3.24f)
                            return ActionEffect.Block("passenger_consent_and_actual_arrival_required");
                        return ActionEffect.Apply(true, "support_responsibility_handed_over", target.With(Next(target),
                            custodianId: recipient.Id, replaceCustodian: true, parentId: recipient.Id, replaceParent: true));
                    }
                    foreach (var entity in entities.Values) if (entity.Kind != EntityKind.Actor && HeldBy(entity, recipient.Id)) return ActionEffect.Block("receiver_hands_occupied");
                    return ActionEffect.Apply(true, "physical_handoff_completed", target.With(Next(target), custodianId: recipient.Id, replaceCustodian: true));
                case ActionVerb.Cordon:
                    if (tool == null || !HeldBy(tool, actor.Id) || tool.Kind != EntityKind.Sign || !evidence.Supported) return ActionEffect.Block("physical_supported_sign_required");
                    if (!evidence.Aligned || tool.Position.DistanceSquared(target.Position) > CordonPlacementDistance * CordonPlacementDistance)
                        return ActionEffect.Block("sign_not_at_protected_location");
                    facts["cordoned"] = RuleTruth.TRUE;
                    facts["cordon.sign:" + tool.Id.Value] = RuleTruth.TRUE;
                    return ActionEffect.Apply(true, "access_marked_not_hazard_removed", target.With(Next(target), facts: facts),
                        tool.With(Next(tool), replaceCustodian: true, parentId: target.Id, replaceParent: true));
                default: return ActionEffect.Block("unsupported_action");
            }
            return ActionEffect.Apply(true, "completed", target.With(Next(target), facts: facts, measurements: values));
        }
        /// <summary>Pure pose revalidation; the world writer commits these records with the sign's actual pose.</summary>
        public static IReadOnlyList<WorldEntity> RevalidateCordons(IReadOnlyDictionary<StableId, WorldEntity> entities,
            StableId signId, WorldPoint actualPosition)
        {
            if (!entities.TryGetValue(signId, out var sign)) return Array.Empty<WorldEntity>();
            var target = RevalidateCordonTarget(entities, sign, actualPosition, sign.CustodianId.HasValue);
            return target == null ? Array.Empty<WorldEntity>() : new[] { target };
        }
        private static WorldEntity RevalidateCordonTarget(IReadOnlyDictionary<StableId, WorldEntity> entities,
            WorldEntity changedSign, WorldPoint actualPosition, bool removed)
        {
            if (changedSign.Kind != EntityKind.Sign || !changedSign.ParentId.HasValue ||
                !entities.TryGetValue(changedSign.ParentId.Value, out var target)) return null;
            string placement = "cordon.sign:" + changedSign.Id.Value;
            if (target.Fact(placement) != RuleTruth.TRUE) return null;
            bool stillPlaced = !removed && !changedSign.CustodianId.HasValue &&
                actualPosition.DistanceSquared(target.Position) <= CordonPlacementDistance * CordonPlacementDistance;
            bool protectedBySign = stillPlaced;
            if (!protectedBySign) foreach (var entity in entities.Values)
            {
                if (entity.Id.Equals(changedSign.Id) || entity.Kind != EntityKind.Sign ||
                    !entity.ParentId.Equals(target.Id) || entity.CustodianId.HasValue ||
                    target.Fact("cordon.sign:" + entity.Id.Value) != RuleTruth.TRUE) continue;
                if (entity.Position.DistanceSquared(target.Position) <= CordonPlacementDistance * CordonPlacementDistance)
                { protectedBySign = true; break; }
            }
            var truth = protectedBySign ? RuleTruth.TRUE : RuleTruth.FALSE;
            if (stillPlaced && target.Fact("cordoned") == truth) return null;
            var facts = TransitionKernel.Copy(target.Facts);
            facts[placement] = stillPlaced ? RuleTruth.TRUE : RuleTruth.FALSE;
            facts["cordoned"] = truth;
            return target.With(Next(target), facts: facts);
        }
        private static bool HeldBy(WorldEntity entity, StableId actor) => entity.CustodianId.HasValue && entity.CustodianId.Value.Equals(actor);
        private static long Next(WorldEntity entity) => checked(entity.Revision + 1);
        private static double Measure(IDictionary<string, SiValue> values, string key, SiUnit unit, double initialProgress)
        { return values.TryGetValue(key, out var value) && value.Unit == unit ? value.Value : initialProgress; }
    }
}
