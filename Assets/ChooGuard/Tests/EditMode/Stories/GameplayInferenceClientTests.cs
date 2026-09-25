#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Application.Gameplay;
using ChooGuard.App.Fps.Runtime;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class GameplayInferenceClientTests
    {
        private static StableId Id(string value) => new StableId(value);
        private static GameplayInferenceRequest Request(bool includeActorEntity = true, bool includeActorRead = false, int targetCount = 1)
        {
            var target = new WorldEntity(Id("door"), "출입문", EntityKind.Door, 9007199254740993,
                new WorldPoint(0, 0, 0), Id("zone"));
            var plan = new GoalPlan(Id("goal"), GoalKind.KeepSchedule, target.Id, 0, "출입문에 도착하려고 합니다.", "destination.reached", new[] { ActionVerb.MoveTo });
            var actor = new ActorState(Id("npc"), ActorRole.Citizen, 9007199254740993, 9007199254740994,
                9007199254740995, false, "정확히 관측한 정보만 사용합니다.", plan);
            var person = new WorldEntity(actor.Id, "시민", EntityKind.Actor, 9007199254740997, new WorldPoint(0, 0, 0), Id("zone"));
            var entities = new Dictionary<StableId, WorldEntity> { [target.Id] = target };
            if (includeActorEntity) entities.Add(person.Id, person);
            var reads = new List<EntityReadVersion> { new EntityReadVersion(target.Id, target.Revision) };
            for (int i = 1; i < targetCount; i++)
            {
                var entity = new WorldEntity(Id("target-" + i), "대상", EntityKind.Surface, i, new WorldPoint(0, 0, 0), Id("zone"));
                entities.Add(entity.Id, entity); reads.Add(new EntityReadVersion(entity.Id, entity.Revision));
            }
            if (includeActorRead) reads.Add(new EntityReadVersion(person.Id, person.Revision));
            var basis = new WorldSnapshot(Id("run"), 3, 0, new SimTick(9007199254740996), GameplayMode.RandomOperationsLab,
                "rules", 1, entities, new Dictionary<StableId, ActorState> { [actor.Id] = actor });
            var candidates = new[] {
                new GroundedCandidate("move", "action:moveto", "gameplay.v1", actor.Id, target.Id, "이동", reads, Array.Empty<string>(), ActionVerb.MoveTo),
                new GroundedCandidate("wait", "action:wait", "gameplay.v1", actor.Id, target.Id, "대기", reads, Array.Empty<string>(), ActionVerb.Wait) };
            return GameplayInferenceClient.CreateNpcRequest(basis, actor, candidates, requestId: "request-a");
        }
        private static JObject Response(GameplayInferenceRequest request, bool available = true)
        {
            var sent = JObject.Parse(request.Json);
            return new JObject { ["schemaVersion"] = 1, ["runId"] = sent["runId"], ["generation"] = sent["generation"],
                ["requestId"] = sent["requestId"], ["actorId"] = sent["actorId"], ["decisionSeq"] = sent["decisionSeq"],
                ["kind"] = "decision_result", ["requestSha256"] = request.RequestSha256, ["status"] = available ? "ok" : "unavailable",
                ["provider"] = "typesafe-direct", ["model"] = available ? "jev-1.13.0" : null,
                ["selectedCandidateId"] = available ? "move" : null,
                ["probabilities"] = available ? new JArray(new JObject { ["candidateId"] = "move", ["probability"] = .7 }, new JObject { ["candidateId"] = "wait", ["probability"] = .3 }) : null,
                ["confidence"] = available ? new JValue(.8) : JValue.CreateNull(),
                ["upstreamLatencyMs"] = available ? new JValue(10) : JValue.CreateNull(),
                ["usage"] = available ? new JObject { ["inputTokens"] = 51, ["outputTokens"] = 9 } : null,
                ["reasonCode"] = available ? "selected" : "provider_error" };
        }

        [Test]
        public void SerializerKeepsInt64BeyondJavascriptPrecisionAndHashesExactUtf8()
        {
            var request = Request(); var body = JObject.Parse(request.Json);
            Assert.That(body["decisionSeq"].Type, Is.EqualTo(JTokenType.String));
            Assert.That((string)body["decisionSeq"], Is.EqualTo("9007199254740995"));
            Assert.That((string)body["readSet"][0]["revision"], Is.EqualTo("9007199254740993"));
            Assert.That((string)body["basisTickUs"], Is.EqualTo("9007199254740996"));
            using (var sha = SHA256.Create())
                Assert.That(request.RequestSha256, Is.EqualTo(BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(request.Json))).Replace("-", "").ToLowerInvariant()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NpcReadSetIncludesActualActorEntityRevisionExactlyOnce(bool alreadyRead)
        {
            var reads = (JArray)JObject.Parse(Request(includeActorRead: alreadyRead).Json)["readSet"];
            Assert.That(reads.Where(read => (string)read["entityId"] == "npc").Select(read => (string)read["revision"]),
                Is.EqualTo(new[] { "9007199254740997" }));
        }

        [Test]
        public void NpcReadSetRejectsMissingActorAndCountsActorAgainstTheBound()
        {
            Assert.Throws<ArgumentException>(() => Request(includeActorEntity: false));
            Assert.That(((JArray)JObject.Parse(Request(targetCount: 63).Json)["readSet"]).Count, Is.EqualTo(64));
            Assert.Throws<ArgumentException>(() => Request(targetCount: 64));
        }

        [TestCase(ActorRole.Citizen, EntityKind.Exit, GoalKind.KeepSchedule)]
        [TestCase(ActorRole.Cleaning, EntityKind.Surface, GoalKind.RestoreCleanliness)]
        [TestCase(ActorRole.Maintenance, EntityKind.Equipment, GoalKind.InspectEquipment)]
        public void OrdinaryPlannerRequestsGroundTheDecidingActor(ActorRole role, EntityKind kind, GoalKind goal)
        {
            var target = new WorldEntity(Id("target"), "일상 업무 대상", kind, 2, new WorldPoint(1, 0, 0), Id("zone"),
                facts: new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.TRUE });
            var person = new WorldEntity(Id("npc"), "사람", EntityKind.Actor, 41, new WorldPoint(0, 0, 0), Id("zone"));
            var actor = new ActorState(person.Id, role, 3, 4, 5, false, "");
            var planner = new NpcPlanner(actor.Id);
            actor = planner.FormGoal(planner.Observe(actor, target, new SimTick(0), ObservationSource.Sight));
            Assert.That(actor.Plan.Kind, Is.EqualTo(goal));
            var basis = new WorldSnapshot(Id("ordinary"), 0, 0, new SimTick(0), GameplayMode.RandomOperationsLab, "rules", 1,
                new Dictionary<StableId, WorldEntity> { [person.Id] = person, [target.Id] = target },
                new Dictionary<StableId, ActorState> { [actor.Id] = actor });
            var body = JObject.Parse(GameplayInferenceClient.CreateNpcRequest(basis, actor, planner.BuildCandidates(actor)).Json);
            Assert.That(body["readSet"].Single(read => (string)read["entityId"] == person.Id.Value)["revision"].Value<string>(), Is.EqualTo("41"));
        }

        private static WorldSnapshot EnvironmentBasis(out TransitionKernel kernel, int equipmentCount = 1)
        {
            var anchor = new WorldEntity(Id("a-anchor"), "구역", EntityKind.Zone, 9007199254740997, new WorldPoint(0, 0, 0), Id("a-anchor"));
            var entities = new Dictionary<StableId, WorldEntity> { [anchor.Id] = anchor };
            for (int i = 0; i < equipmentCount; i++)
            {
                var id = Id(i == 0 ? "equipment" : "equipment-" + i);
                entities.Add(id, new WorldEntity(id, "연습 설비", EntityKind.Equipment, 7, new WorldPoint(0, 0, 0), anchor.Id,
                    facts: new Dictionary<string, RuleTruth> { ["fault"] = RuleTruth.TRUE, ["ignited"] = RuleTruth.FALSE }));
            }
            kernel = new TransitionKernel(new[] { new TransitionDefinition("fault-ignition", "authored-lab", "연습 발화",
                EntityKind.Equipment, EntityKind.Equipment, TransitionRelation.Self,
                new Dictionary<string, RuleTruth> { ["fault"] = RuleTruth.TRUE }, null,
                new Dictionary<string, RuleTruth> { ["ignited"] = RuleTruth.TRUE }) });
            return new WorldSnapshot(Id("environment"), 0, 0, new SimTick(0), GameplayMode.RandomOperationsLab, "rules", 1,
                entities, new Dictionary<StableId, ActorState>());
        }

        // Transport fixture only: captures the real serializer output and explicitly returns no provider result.
        private sealed class FutureBodyHandler : HttpMessageHandler
        {
            public JObject FutureRequest;
            public JObject Registration;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
            {
                var raw = await message.Content.ReadAsByteArrayAsync();
                var body = JObject.Parse(Encoding.UTF8.GetString(raw));
                var response = new JObject { ["runId"] = body["runId"], ["generation"] = body["generation"], ["status"] = "registered" };
                var status = HttpStatusCode.OK;
                if (message.RequestUri.AbsolutePath == "/future/register")
                { Registration = body; response["forecastId"] = body["forecastId"]; }
                else if (message.RequestUri.AbsolutePath == "/future/step")
                {
                    FutureRequest = body; status = HttpStatusCode.ServiceUnavailable;
                    response["schemaVersion"] = 1; response["kind"] = "future_step_result";
                    foreach (var key in new[] { "requestId", "forecastId", "branchId", "stepSeq" }) response[key] = body[key];
                    using (var sha = SHA256.Create())
                        response["requestSha256"] = BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
                    response["status"] = "unavailable"; response["provider"] = "typesafe-direct"; response["reasonCode"] = "provider_error";
                    foreach (var key in new[] { "model", "selectedCandidateId", "probabilities", "confidence", "upstreamLatencyMs", "usage" }) response[key] = null;
                }
                return new HttpResponseMessage(status) { Content = new StringContent(response.ToString(Formatting.None), Encoding.UTF8, "application/json") };
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task EnvironmentBodiesIncludeGroundedNoEffectHoldWithoutInventedCauses(bool nextEnvironment)
        {
            var basis = EnvironmentBasis(out var kernel); var candidates = kernel.BuildEnvironmentCandidates(basis);
            var handler = new FutureBodyHandler();
            using (var client = new GameplayInferenceClient(handler: handler))
            {
                Assert.That(await client.RegisterFutureAsync(basis, "forecast", new[] { "root" }, candidates, CancellationToken.None), Is.True);
                var result = await client.SelectFutureAsync(basis, basis, "forecast", "root", 0, candidates, nextEnvironment, CancellationToken.None);
                Assert.That(result.Reason, Is.EqualTo("provider_error"));
                var body = handler.FutureRequest; var hold = body["candidates"].Single(c => (string)c["candidateId"] == "env-hold");
                Assert.That((string)hold["transitionId"], Is.EqualTo("hold"));
                Assert.That((string)hold["parameterSetId"], Is.EqualTo("none"));
                Assert.That(hold["actorId"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(hold["causeFactIds"], Is.Empty);
                Assert.That(hold["bindings"].Select(binding => (string)binding["entityId"]), Is.EqualTo(new[] { "a-anchor", "a-anchor" }));
                Assert.That(body["readSet"].Single(read => (string)read["entityId"] == "a-anchor")["revision"].Value<string>(), Is.EqualTo("9007199254740997"));
                Assert.That(body["stateFacts"].Select(fact => (string)fact["factId"]),
                    Is.EquivalentTo(new[] { TransitionKernel.FactId(Id("equipment"), "fault") }));
                Assert.That(kernel.ApplyHypothetical(basis, candidates.Single(c => c.CandidateId == "env-hold"), out _), Is.SameAs(basis));
            }
        }

        [Test]
        public async Task ForecastScopeUnionsBranchesAtBasisRevisionsWithoutTreatingThemAsOneChoice()
        {
            var basis = EnvironmentBasis(out var kernel, 20);
            var entities = TransitionKernel.Copy(basis.Entities); entities[Id("equipment")] = entities[Id("equipment")].With(8);
            var imagined = new WorldSnapshot(basis.RunId, basis.Generation, 1, basis.Tick, basis.Mode, basis.RulesetId,
                basis.RulesetRevision, entities, TransitionKernel.Copy(basis.Actors));
            var rootCandidates = kernel.BuildEnvironmentCandidates(basis);
            var branchCandidates = kernel.BuildEnvironmentCandidates(imagined);
            var scope = rootCandidates.Concat(branchCandidates).ToList();
            var handler = new FutureBodyHandler();
            using (var client = new GameplayInferenceClient(handler: handler))
            {
                Assert.That(await client.RegisterFutureAsync(basis, "forecast", new[] { "root", "alternative" }, scope, CancellationToken.None), Is.True);
                var reads = (JArray)handler.Registration["readSet"];
                Assert.That(reads.Select(read => (string)read["entityId"]), Is.EquivalentTo(basis.Entities.Keys.Select(id => id.Value)));
                Assert.That((string)reads.Single(read => (string)read["entityId"] == "equipment")["revision"], Is.EqualTo("7"));
                Assert.That((await client.SelectFutureAsync(basis, imagined, "forecast", "alternative", 0, branchCandidates, false, CancellationToken.None)).Reason,
                    Is.EqualTo("provider_error"));
                Assert.ThrowsAsync<ArgumentException>(() => client.SelectFutureAsync(basis, imagined, "forecast", "alternative", 0, scope, false, CancellationToken.None));
                Assert.ThrowsAsync<ArgumentException>(() => client.SelectFutureAsync(basis, basis, "forecast", "root", 0,
                    new[] { rootCandidates[0], rootCandidates[0] }, false, CancellationToken.None));
            }
        }

        [TestCase(63)]
        [TestCase(64)]
        public async Task ForecastScopeRetainsItsSixtyFourRootEntityBound(int equipmentCount)
        {
            var basis = EnvironmentBasis(out var kernel, equipmentCount);
            var scope = new List<GroundedCandidate>();
            for (int branchIndex = 0; branchIndex < 4; branchIndex++)
            {
                var entities = TransitionKernel.Copy(basis.Entities); int index = 0;
                foreach (var equipment in basis.Entities.Values.Where(entity => entity.Kind == EntityKind.Equipment))
                    if (index++ % 4 != branchIndex)
                        entities[equipment.Id] = equipment.With(8, facts: new Dictionary<string, RuleTruth>
                            { ["fault"] = RuleTruth.FALSE, ["ignited"] = RuleTruth.FALSE });
                var branch = new WorldSnapshot(basis.RunId, basis.Generation, 1, basis.Tick, basis.Mode, basis.RulesetId,
                    basis.RulesetRevision, entities, new Dictionary<StableId, ActorState>());
                scope.AddRange(kernel.BuildEnvironmentCandidates(branch));
            }
            var handler = new FutureBodyHandler();
            using (var client = new GameplayInferenceClient(handler: handler))
            {
                var branches = new[] { "a", "b", "c", "d" };
                if (equipmentCount == 64)
                    Assert.ThrowsAsync<ArgumentException>(() => client.RegisterFutureAsync(basis, "forecast", branches, scope, CancellationToken.None));
                else
                {
                    Assert.That(await client.RegisterFutureAsync(basis, "forecast", branches, scope, CancellationToken.None), Is.True);
                    Assert.That(((JArray)handler.Registration["readSet"]).Count, Is.EqualTo(64));
                }
            }
        }

        [TestCase("uncaused-effect")]
        [TestCase("wrong-parameters")]
        [TestCase("different-target")]
        [TestCase("missing-anchor-read")]
        [TestCase("stale-anchor-read")]
        [TestCase("caused-hold")]
        public void FutureSerializerRejectsUncausedEffectsAndMalformedHold(string invalid)
        {
            var basis = EnvironmentBasis(out var kernel); var candidates = new List<GroundedCandidate>(kernel.BuildEnvironmentCandidates(basis));
            int index = invalid == "uncaused-effect" ? 0 : 1; var original = candidates[index];
            candidates[index] = new GroundedCandidate(original.CandidateId, original.TransitionId,
                invalid == "wrong-parameters" ? "authored-lab" : original.ParameterSetId, null,
                invalid == "different-target" ? Id("equipment") : original.TargetId, original.Summary,
                invalid == "missing-anchor-read" ? Array.Empty<EntityReadVersion>() :
                    invalid == "stale-anchor-read" ? new[] { new EntityReadVersion(original.SourceId, original.ReadSet[0].Revision + 1) } : original.ReadSet,
                invalid == "caused-hold" ? candidates[0].CauseFactIds : Array.Empty<string>(), sourceId: original.SourceId);
            if (index == 1) Assert.That(kernel.TryApply(basis.Entities, candidates[index], out _, out _), Is.False);
            using (var client = new GameplayInferenceClient(handler: new FutureBodyHandler()))
                Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    await client.RegisterFutureAsync(basis, "forecast", new[] { "root" }, candidates, CancellationToken.None);
                    await client.SelectFutureAsync(basis, basis, "forecast", "root", 0, candidates, true, CancellationToken.None);
                });
        }

        [Test]
        public void NullableUnavailableDoesNotRequireASuccessfulModelOrDistribution()
        {
            var request = Request();
            var result = GameplayInferenceClient.ValidateResponse(request, Response(request, false).ToString(), "jev-1.13.0", false);
            Assert.That(result.Status, Is.EqualTo("unavailable"));
            Assert.That(result.SelectedCandidateId, Is.Null);
            Assert.That(result.Model, Is.Null);
            Assert.That(result.Probabilities, Is.Null);
            Assert.That(result.Usage, Is.Null);
        }

        [TestCase("requestId", "different-request")]
        [TestCase("actorId", "different-owner")]
        [TestCase("decisionSeq", "9007199254740996")]
        [TestCase("requestSha256", "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
        public void EvenUnavailableMustCorrelateToTheExactSentRequest(string field, string replacement)
        {
            var request = Request(); var response = Response(request, false); response[field] = replacement;
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0", false));
        }

        [Test]
        public void CorrectSuccessExposesActualDistributionAndKnownUsage()
        {
            var request = Request();
            var result = GameplayInferenceClient.ValidateResponse(request, Response(request).ToString(), "jev-1.13.0");
            Assert.That(result.SelectedCandidateId, Is.EqualTo("move"));
            Assert.That(result.Probabilities["wait"], Is.EqualTo(.3));
            Assert.That(result.Usage.InputTokens, Is.EqualTo(51));
            Assert.That(result.Usage.OutputTokens, Is.EqualTo(9));
        }

        [Test]
        public void UnknownOrRepeatedCandidateProbabilityIsRejectedWithoutRenormalization()
        {
            var request = Request(); var response = Response(request);
            response["probabilities"][1]["candidateId"] = "other";
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0"));
            response["probabilities"][1]["candidateId"] = "move";
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0"));
            response["probabilities"][1]["candidateId"] = "wait";
            response["probabilities"][1]["probability"] = .2;
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0"));
        }

        [Test]
        public void NonMaximumChoiceAndUnpinnedModelCannotExecute()
        {
            var request = Request(); var response = Response(request);
            response["selectedCandidateId"] = "wait";
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0"));
            response["selectedCandidateId"] = "move"; response["model"] = "different-model";
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0"));
        }

        [TestCase("{\"schemaVersion\":1,\"schemaVersion\":1}")]
        [TestCase("{\"escaped\":\"\\ud800\"}")]
        [TestCase("{\"number\":NaN}")]
        [TestCase("{\"number\":01}")]
        [TestCase("{\"value\":1,}")]
        [TestCase("{'single':1}")]
        [TestCase("{}{}")]
        public void NonStrictJsonIsRejectedBeforeSemanticUse(string malformed)
        {
            Assert.Catch<JsonException>(() => GameplayInferenceClient.ValidateResponse(Request(), malformed, "jev-1.13.0"));
        }

        [Test]
        public void UnavailableCannotSmuggleASelectionOrWorldPatch()
        {
            var request = Request(); var response = Response(request, false);
            response["selectedCandidateId"] = "move";
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0", false));
            response["selectedCandidateId"] = null; response["worldPatch"] = new JObject { ["safe"] = true };
            Assert.Throws<JsonException>(() => GameplayInferenceClient.ValidateResponse(request, response.ToString(), "jev-1.13.0", false));
        }
    }
}
#endif
