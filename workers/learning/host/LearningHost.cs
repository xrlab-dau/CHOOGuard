using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using ChooGuard.Application.Gameplay;
using ChooGuard.Application.Gameplay.Content;
using ChooGuard.App.Fps.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Learning
{
    // A bounded authored open-floor training adapter, NOT Unity PhysX/FDS, clinical, or railway certification.
    // Python chooses a skill index; this process alone owns time, motion evidence, actions and causal effects.
    public sealed class LearningHost : IDisposable
    {
        private const int Slots = 32, Features = 40;
        private readonly GameplayContent content;
        private readonly string journalRoot;
        private readonly GameplayInferenceClient inference;
        private WorldSession world;
        private ResearchJournal journal;
        private TransitionKernel kernel;
        private NpcPlanner planner;
        private StableId owner, goal, cleaningTool;
        private List<GroundedCandidate> candidates = new List<GroundedCandidate>();
        private JObject episode;
        private WorldPoint origin;
        private bool memory, ended = true, acquisitionRewarded;
        private int steps, invalidActions, blockedActions, environmentSequence;
        private string lastReceipt = "reset", outcome = "running";

        public LearningHost(GameplayContent content, string journalRoot)
        {
            this.content = content; this.journalRoot = journalRoot;
            inference = new GameplayInferenceClient(
                Environment.GetEnvironmentVariable("CHOOGUARD_GAMEPLAY_URL") ?? "http://127.0.0.1:8788");
        }

        public JObject Handle(JObject request)
        {
            switch ((string)request["command"])
            {
                case "reset": return Reset((JObject)request["episode"], request.Value<bool?>("memory") ?? true);
                case "step": return Step(request.Value<int>("action"));
                case "jev_decision": return JevDecision();
                default: throw new ArgumentException("Unknown research command.");
            }
        }

        private JObject Reset(JObject specification, bool retainMemory)
        {
            if (specification == null) throw new ArgumentException("Episode initial conditions are required.");
            int seed = specification.Value<int>("seed");
            int maximum = specification.Value<int>("max_steps");
            if (maximum < 1 || maximum > 256) throw new ArgumentException("max_steps must be in [1,256].");
            if (specification.Value<double>("max_seconds") <= 0 || specification.Value<double>("max_seconds") > 1800)
                throw new ArgumentException("max_seconds must be in (0,1800].");
            DisposeWorld(); episode = (JObject)specification.DeepClone(); memory = retainMemory;
            var initial = GameplayInitialWorld.Create(content.Seed, 8, GameplayMode.RandomOperationsLab);
            var entities = TransitionKernel.Copy(initial.Entities);
            var actors = TransitionKernel.Copy(initial.Actors);
            var controlled = actors.Values.OrderBy(a => a.Id.Value, StringComparer.Ordinal)
                .First(a => !a.IsHuman && a.Role == ActorRole.Cleaning);
            owner = controlled.Id;
            var surface = entities.Values.OrderBy(e => e.Id.Value, StringComparer.Ordinal)
                .First(e => e.Kind == EntityKind.Surface && e.Fact("dirty") == RuleTruth.TRUE);
            var tool = entities.Values.OrderBy(e => e.Id.Value, StringComparer.Ordinal)
                .First(e => e.Kind == EntityKind.Tool && e.Fact("tool.cleaning") == RuleTruth.TRUE);
            goal = surface.Id; cleaningTool = tool.Id;
            origin = new WorldPoint(surface.Position.X, content.Seed.PopulationOrigin.Y, surface.Position.Z);
            var random = new Random(seed);
            float jitter = (float)(random.NextDouble() * .8 - .4);
            string placement = specification.Value<string>("placement");
            float side;
            switch (placement)
            {
                case "west-near": side = -3; break;
                case "east-near": side = 3; break;
                case "north-far": side = 7; break;
                case "south-far": side = -7; break;
                default: throw new ArgumentException("Unknown authored training placement.");
            }
            bool northSouth = placement.EndsWith("far", StringComparison.Ordinal);
            var actorPosition = Offset(northSouth ? jitter : side, northSouth ? side : jitter);
            entities[owner] = entities[owner].With(0, position: actorPosition);
            entities[cleaningTool] = tool.With(0, position: Offset(side * .5f, 1 + jitter));
            actors[owner] = controlled.With(plan: new GoalPlan(new StableId("research-cleaning-goal"),
                GoalKind.RestoreCleanliness, goal, 0, "Restore visible authored residue; not cleanliness certification",
                "dirty:FALSE", new[] { ActionVerb.Clean }), replacePlan: true,
                memories: Array.Empty<ActorObservation>());
            ApplyInitialIntervention(entities, specification.Value<string>("intervention"));
            // A hidden canary is an explicit regression input, never an observation or decision feature.
            if (specification["private_canary"] != null)
            {
                var hiddenFacts = TransitionKernel.Copy(entities[goal].Facts);
                hiddenFacts["research.private.canary"] = specification.Value<bool>("private_canary") ? RuleTruth.TRUE : RuleTruth.FALSE;
                entities[goal] = entities[goal].With(0, facts: hiddenFacts);
            }
            var selected = new List<TransitionDefinition>();
            var ids = specification["rule_combination"] as JArray ?? throw new ArgumentException("Explicit causal rule combination required.");
            foreach (var id in ids.Values<string>())
            {
                var definition = content.Transitions.FirstOrDefault(rule => rule.Id == id);
                if (definition == null || selected.Contains(definition)) throw new ArgumentException("Unknown or duplicate authored causal rule.");
                selected.Add(definition);
            }
            kernel = new TransitionKernel(selected);
            var run = new StableId("research-" + Guid.NewGuid().ToString("N"));
            initial = new WorldSnapshot(run, 0, 0, new SimTick(0), GameplayMode.RandomOperationsLab,
                initial.RulesetId, initial.RulesetRevision, entities, actors);
            journal = new ResearchJournal(journalRoot, initial);
            world = new WorldSession(initial, journal);
            planner = new NpcPlanner(owner);
            steps = invalidActions = blockedActions = environmentSequence = 0;
            acquisitionRewarded = false; ended = false; outcome = "running"; lastReceipt = "reset";
            Perceive();
            return Response(0, false, false);
        }

        private void ApplyInitialIntervention(Dictionary<StableId, WorldEntity> entities, string intervention)
        {
            bool lowSupply = false, relocate = false, leak = false, reportedItem = false;
            switch (intervention)
            {
                case "none": break;
                case "tool-relocated": relocate = true; break;
                case "leak-and-relocated": leak = relocate = true; break;
                case "low-supply-and-reported-item": lowSupply = reportedItem = true; break;
                default: throw new ArgumentException("Unknown authored initial-condition intervention.");
            }
            if (leak)
            {
                var source = entities.Values.First(e => e.Kind == EntityKind.Equipment && e.Facts.ContainsKey("cause.leak.active"));
                var facts = TransitionKernel.Copy(source.Facts);
                facts["cause.leak.active"] = RuleTruth.TRUE;
                entities[source.Id] = source.With(0, facts: facts);
            }
            if (reportedItem)
            {
                var item = entities.Values.First(e => e.Kind == EntityKind.PersonalItem && e.Fact("owner.known") == RuleTruth.FALSE);
                var facts = TransitionKernel.Copy(item.Facts);
                facts["reported"] = RuleTruth.TRUE;
                entities[item.Id] = item.With(0, facts: facts);
            }
            if (lowSupply)
            {
                var tool = entities[cleaningTool]; var values = TransitionKernel.Copy(tool.Measurements);
                if (!values.TryGetValue("supply", out var supply) || supply.Unit != SiUnit.CubicMetre)
                    throw new InvalidOperationException("Authored cleaning tool supply measurement required.");
                values["supply"] = new SiValue(Math.Min(supply.Value, .000025), SiUnit.CubicMetre);
                entities[cleaningTool] = tool.With(0, measurements: values);
            }
            if (relocate) entities[cleaningTool] = entities[cleaningTool].With(0, position: Offset(-4, -4));
        }

        private JObject Step(int action)
        {
            if (world == null || ended) throw new InvalidOperationException("Reset is required before step.");
            double startSeconds = world.Tick.Microseconds / 1000000.0;
            double reward = 0;
            if (action < 0 || action >= candidates.Count)
            {
                // Do not run, describe or distinguish a forbidden hidden-world action.
                invalidActions++; lastReceipt = "candidate_not_available"; reward -= .2;
                Tick();
            }
            else
            {
                var selected = candidates[action];
                var receipt = ExecuteSkill(owner, selected);
                lastReceipt = receipt == null ? "training_geometry_out_of_scope" :
                    receipt.Phase == ActionPhase.Blocked || receipt.Phase == ActionPhase.Failed ? "action_not_completed" : receipt.Phase.ToString();
                if (receipt != null && (receipt.Phase == ActionPhase.Blocked || receipt.Phase == ActionPhase.Failed))
                { blockedActions++; reward -= .15; }
                if (receipt != null && receipt.Phase == ActionPhase.Completed && selected.Verb == ActionVerb.PickUp &&
                    selected.TargetId.Equals(cleaningTool) && !acquisitionRewarded)
                { acquisitionRewarded = true; reward += .25; }
            }
            world.FlushPoses();
            FixedBackgroundWait();
            AdvanceEnvironment();
            steps++;
            reward -= .025 * (world.Tick.Microseconds / 1000000.0 - startSeconds);
            bool terminated = false, truncated = false;
            world.TryGetEntity(goal, out var target);
            world.TryGetEntity(owner, out var self);
            bool hazard = self.Fact("hazardous") == RuleTruth.TRUE;
            // Only an authored zone fact is a research hazard outcome; unknown is not a safe/unsafe diagnosis.
            if (world.TryGetEntity(self.ZoneId, out var zone))
            {
                hazard |= zone.Fact("hazard") == RuleTruth.TRUE || zone.Fact("hazardous") == RuleTruth.TRUE;
                if (zone.Fact("slip.attention") == RuleTruth.TRUE) reward -= .02;
            }
            if (outcome == "model_scope_exceeded") truncated = true;
            else if (hazard) { outcome = "authored_hazard"; reward -= 2; terminated = true; }
            else if (target.Fact("dirty") == RuleTruth.FALSE) { outcome = "task_completed"; reward += 4; terminated = true; }
            else if (steps >= episode.Value<int>("max_steps") || world.Tick.Microseconds / 1000000.0 >= episode.Value<double>("max_seconds"))
            { outcome = "time_limit"; truncated = true; }
            ended = terminated || truncated;
            Perceive();
            return Response(Math.Max(-5, Math.Min(5, reward)), terminated, truncated);
        }

        private ActionReceipt ExecuteSkill(StableId actorId, GroundedCandidate candidate)
        {
            world.TryGetEntity(actorId, out var actorBody);
            world.TryGetEntity(candidate.TargetId, out var target);
            if (!Inside(actorBody.Position) || !Inside(target.Position))
            { outcome = "model_scope_exceeded"; return null; }
            // Every high-level manipulation first uses the same MoveTo action contract for approach.
            if (candidate.Verb != ActionVerb.Wait && actorBody.Position.DistanceSquared(target.Position) > 1)
            {
                var move = world.CreateIntent(actorId, ActionVerb.MoveTo, target.Id);
                var accepted = world.RequestAction(move);
                if (accepted.Phase == ActionPhase.Blocked) return accepted;
                var carried = world.Snapshot().Entities.Values.Where(item => item.CustodianId.Equals(actorId)).Select(item => item.Id).ToArray();
                for (int tick = 0; tick < 48; tick++)
                {
                    world.TryGetEntity(actorId, out actorBody);
                    float dx = target.Position.X - actorBody.Position.X, dz = target.Position.Z - actorBody.Position.Z;
                    float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                    float fraction = distance <= .0001f ? 0 : Math.Min(.75f / distance, 1);
                    var pose = new WorldPoint(actorBody.Position.X + dx * fraction, origin.Y,
                        actorBody.Position.Z + dz * fraction);
                    world.SetActorPose(actorId, pose);
                    foreach (var item in carried) world.SetPhysicalPose(item, pose);
                    Tick();
                    if (world.Tick.Microseconds / 1000000.0 >= episode.Value<double>("max_seconds"))
                        return world.CancelAction(actorId, move.IntentId, "research_time_horizon");
                    accepted = world.AdvanceAction(actorId, move.IntentId,
                        new ActionEvidence(pose, target.Position, Visible(pose, target.Position), false, false, false, 0, target.Revision));
                    if (accepted.Phase == ActionPhase.Completed || accepted.Phase == ActionPhase.Blocked) break;
                }
                if (accepted.Phase != ActionPhase.Completed)
                    return world.CancelAction(actorId, move.IntentId, "research_approach_budget");
                if (candidate.Verb == ActionVerb.MoveTo) return accepted;
            }
            string workPoint = null;
            if (candidate.Verb == ActionVerb.Clean)
            {
                // Authored visible contact regions are executed by the adapter, not reimplemented in Python.
                workPoint = target.Measurements.Where(p => p.Key.StartsWith("soil:", StringComparison.Ordinal) && p.Value.Value > 0)
                    .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key.Substring(5)).FirstOrDefault();
            }
            var intent = world.CreateIntent(actorId, candidate.Verb.Value, target.Id, candidate.ToolId, candidate.RecipientId, workPoint);
            var receipt = world.RequestAction(intent);
            if (receipt.Phase == ActionPhase.Blocked) { Tick(); return receipt; }
            if (receipt.Phase == ActionPhase.Completed) { Tick(); return receipt; }
            for (int sample = 0; sample < 12; sample++)
            {
                Tick();
                if (world.Tick.Microseconds / 1000000.0 >= episode.Value<double>("max_seconds"))
                    return world.CancelAction(actorId, intent.IntentId, "research_time_horizon");
                world.TryGetEntity(actorId, out actorBody); world.TryGetEntity(target.Id, out target);
                bool contact = actorBody.Position.DistanceSquared(target.Position) <= 1.21f;
                bool supported = Math.Abs(target.Position.Y - origin.Y) <= 2;
                receipt = world.AdvanceAction(actorId, intent.IntentId,
                    new ActionEvidence(actorBody.Position, target.Position, Visible(actorBody.Position, target.Position),
                        contact, supported, contact && supported, contact ? .25 : 0, target.Revision));
                if (receipt.Phase == ActionPhase.Completed || receipt.Phase == ActionPhase.Blocked || receipt.Phase == ActionPhase.Failed) break;
            }
            if (world.GetActiveIntent(actorId) != null)
                receipt = world.CancelAction(actorId, intent.IntentId, "research_skill_budget");
            if (actorId.Equals(owner) && receipt.Phase == ActionPhase.Completed)
            {
                world.TryGetActor(owner, out var actor);
                world.TryGetEntity(target.Id, out target);
                world.UpdateActor(planner.ObserveOwnEffect(actor, target, candidate.Verb.Value, world.Tick), "research-own-action-observation");
            }
            return receipt;
        }

        private void FixedBackgroundWait()
        {
            foreach (var actor in world.Snapshot().Actors.Values)
            {
                if (actor.Id.Equals(owner)) continue;
                world.RequestAction(world.CreateIntent(actor.Id, ActionVerb.Wait, actor.Id));
            }
        }

        private void AdvanceEnvironment()
        {
            var basis = world.Snapshot();
            var available = kernel.BuildEnvironmentCandidates(basis, 32);
            if (available.Count == 0) return;
            // Fixed selection is deliberately isolated to research, never labelled JEV inference.
            var choice = available.OrderBy(c => c.CandidateId, StringComparer.Ordinal).First();
            string reason;
            if (!world.CommitEnvironment(kernel, basis, choice, "research-baseline-" + (++environmentSequence),
                "explicit-fixed-research-baseline-not-inference", out reason))
                throw new InvalidOperationException("Authoritative research transition rejected: " + reason);
        }

        private void Perceive()
        {
            world.TryGetActor(owner, out var actor);
            if (!memory)
            {
                planner = new NpcPlanner(owner);
                actor = actor.With(memories: Array.Empty<ActorObservation>(), knownEntities: Array.Empty<WorldEntity>());
            }
            world.TryGetEntity(owner, out var self);
            foreach (var entity in world.Snapshot().Entities.Values.OrderBy(e => e.Id.Value, StringComparer.Ordinal))
                if (Visible(self.Position, entity.Position)) actor = planner.Observe(actor, entity, world.Tick, ObservationSource.Sight);
            // The assigned target's identity/location is known, but assignment never grants its hidden facts.
            if (!planner.GetKnownEntities(actor).ContainsKey(goal))
            {
                world.TryGetEntity(goal, out var assigned);
                var identity = new WorldEntity(assigned.Id, assigned.Label, assigned.Kind, 0, assigned.Position, assigned.ZoneId);
                actor = planner.Observe(actor, identity, world.Tick, ObservationSource.Assignment);
            }
            world.UpdateActor(actor, "research-private-perception");
            candidates = planner.BuildCandidates(actor);
            candidates.RemoveAll(c => !c.Verb.HasValue || !planner.GetKnownEntities(actor).TryGetValue(c.TargetId, out var entity) ||
                !ActionRules.RoleAllows(actor.Role, c.Verb.Value, entity));
            if (candidates.Count > Slots) throw new InvalidOperationException("Candidate space exceeds declared observation capacity.");
        }

        private JObject Response(double reward, bool terminated, bool truncated)
        {
            world.TryGetActor(owner, out var actor); world.TryGetEntity(owner, out var self);
            var rows = new JArray(); var mask = new JArray(); var choices = new JArray();
            for (int slot = 0; slot < Slots; slot++)
            {
                var features = new double[Features];
                if (slot < candidates.Count)
                {
                    var candidate = candidates[slot]; var entity = planner.GetKnownEntities(actor)[candidate.TargetId];
                    features[(int)candidate.Verb.Value] = 1;
                    features[24] = (int)entity.Kind / 15.0;
                    features[25] = Math.Min(1, Math.Sqrt(self.Position.DistanceSquared(entity.Position)) / 30);
                    features[26] = Known(entity, "dirty"); features[27] = Known(entity, "wet");
                    features[28] = Known(entity, "hazardous"); features[29] = Known(entity, "portable");
                    features[30] = Known(entity, "installed"); features[31] = entity.CustodianId.Equals(owner) ? 1 : 0;
                    features[32] = candidate.TargetId.Equals(goal) ? 1 : 0;
                    features[33] = candidate.ToolId.HasValue ? 1 : 0;
                    features[34] = candidate.RecipientId.HasValue ? 1 : 0;
                    features[35] = Math.Max(-1, Math.Min(1, (entity.Position.X - self.Position.X) / 30));
                    features[36] = Math.Max(-1, Math.Min(1, (entity.Position.Z - self.Position.Z) / 30));
                    choices.Add(new JObject { ["id"] = candidate.CandidateId, ["verb"] = candidate.Verb.Value.ToString(),
                        ["target"] = candidate.TargetId.Value, ["tool"] = candidate.ToolId?.Value });
                }
                rows.Add(new JArray(features)); mask.Add(slot < candidates.Count ? 1 : 0);
            }
            var actorFeatures = new double[8]; actorFeatures[(int)actor.Role] = 1;
            actorFeatures[5] = Math.Min(1, steps / (double)episode.Value<int>("max_steps"));
            actorFeatures[6] = Math.Min(1, world.Tick.Microseconds / 1000000.0 / episode.Value<double>("max_seconds"));
            actorFeatures[7] = Math.Min(1, actor.Memories.Count / 64.0);
            return new JObject { ["ok"] = true, ["reward"] = reward, ["terminated"] = terminated, ["truncated"] = truncated,
                ["observation"] = new JObject { ["self"] = new JArray(actorFeatures), ["candidates"] = rows, ["available"] = mask },
                ["candidates"] = choices,
                ["info"] = new JObject { ["outcome"] = outcome, ["receipt"] = lastReceipt, ["invalid_actions"] = invalidActions,
                    ["blocked_actions"] = blockedActions, ["simulated_seconds"] = world.Tick.Microseconds / 1000000.0,
                    ["environment_transitions"] = environmentSequence,
                    ["journal_run"] = world.RunId.Value,
                    ["success"] = outcome == "task_completed", ["scope"] = "authored-training-open-floor" } };
        }

        private JObject JevDecision()
        {
            if (world == null || ended) throw new InvalidOperationException("No active episode.");
            world.TryGetActor(owner, out var actor);
            actor = actor.With(decisionSequence: checked(actor.DecisionSequence + 1));
            world.UpdateActor(actor, "research-fresh-jev-decision");
            var basis = world.Snapshot();
            InferenceSelection result;
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                result = inference.SelectNpcAsync(basis, actor, candidates, null, deadline.Token).GetAwaiter().GetResult();
            int index = result.Status == "ok" ? candidates.FindIndex(candidate => candidate.CandidateId == result.SelectedCandidateId) : -1;
            return new JObject { ["ok"] = true, ["action"] = index < 0 ? JValue.CreateNull() : new JValue(index),
                ["inference"] = new JObject { ["status"] = index >= 0 ? "selected" : result.Status, ["reason"] = result.Reason,
                    ["request_sha256"] = result.RequestSha256, ["model"] = result.Model,
                    ["input_tokens"] = result.Usage?.InputTokens, ["output_tokens"] = result.Usage?.OutputTokens,
                    ["upstream_latency_ms"] = result.UpstreamLatencyMs } };
        }

        private WorldPoint Offset(float x, float z) => new WorldPoint(origin.X + x, origin.Y, origin.Z + z);
        private bool Inside(WorldPoint p) => Math.Abs(p.X - origin.X) <= 30 && Math.Abs(p.Z - origin.Z) <= 30 && Math.Abs(p.Y - origin.Y) <= 2;
        private bool Visible(WorldPoint a, WorldPoint b) => Inside(a) && Inside(b) && a.DistanceSquared(b) <= 144;
        private static double Known(WorldEntity entity, string fact) => entity.Fact(fact) == RuleTruth.TRUE ? 1 : entity.Fact(fact) == RuleTruth.FALSE ? -1 : 0;
        private void Tick() => world.AdvanceTime(new SimTick(Math.Min(
            checked(world.Tick.Microseconds + 500000), checked((long)(episode.Value<double>("max_seconds") * 1000000)))));
        private void DisposeWorld() { world?.Dispose(); world = null; journal?.Dispose(); journal = null; }
        public void Dispose() { DisposeWorld(); inference.Dispose(); }

        private sealed class ResearchJournal : IGameplayCommitSink, IDisposable
        {
            private readonly StreamWriter stream;
            internal ResearchJournal(string root, WorldSnapshot initial)
            {
                string directory = Path.Combine(root, initial.RunId.Value); Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "initial.json"), GameplayCodec.SerializeSnapshot(initial), new UTF8Encoding(false));
                stream = new StreamWriter(Path.Combine(directory, "mutations.jsonl"), false, new UTF8Encoding(false));
            }
            public void Commit(WorldMutation mutation)
            { stream.WriteLine(GameplayCodec.SerializeMutation(mutation)); stream.Flush(); }
            public void Dispose() => stream.Dispose();
        }

        public static int Main(string[] args)
        {
            Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
            if (args.Length != 3) { Console.Error.WriteLine("LearningHost seed.json transitions.json research-journal-directory"); return 2; }
            try
            {
                var content = GameplayContentLoader.Load(File.ReadAllText(args[0]), File.ReadAllText(args[1]));
                using (var host = new LearningHost(content, args[2]))
                {
                    string line;
                    while ((line = Console.ReadLine()) != null)
                    {
                        try { Console.WriteLine(host.Handle(JObject.Parse(line)).ToString(Formatting.None)); }
                        catch (Exception error)
                        {
                            Console.Error.WriteLine(error.ToString());
                            Console.WriteLine(new JObject { ["ok"] = false, ["error"] = error.GetType().Name + ": " + error.Message }.ToString(Formatting.None));
                        }
                    }
                }
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error.ToString()); return 1; }
        }
    }
}
