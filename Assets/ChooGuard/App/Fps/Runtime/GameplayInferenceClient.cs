using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.App.Fps.Runtime
{
    public sealed class InferenceUsage
    {
        public long InputTokens { get; }
        public long OutputTokens { get; }
        public InferenceUsage(long input, long output) { InputTokens = input; OutputTokens = output; }
    }

    public sealed class InferenceSelection
    {
        public string Status { get; }
        public string Reason { get; }
        public string SelectedCandidateId { get; }
        public string RequestSha256 { get; }
        public string Model { get; }
        public IReadOnlyDictionary<string, double> Probabilities { get; }
        public InferenceUsage Usage { get; }
        public double? Confidence { get; }
        public double? UpstreamLatencyMs { get; }
        public InferenceSelection(string status, string reason, string selected = null, string hash = null, string model = null,
            IDictionary<string, double> probabilities = null, InferenceUsage usage = null, double? confidence = null, double? latency = null)
        {
            Status = status; Reason = reason; SelectedCandidateId = selected; RequestSha256 = hash; Model = model; Usage = usage;
            Probabilities = probabilities == null ? null : new ReadOnlyDictionary<string, double>(probabilities);
            Confidence = confidence; UpstreamLatencyMs = latency;
        }
    }

    public sealed class GameplayInferenceRequest
    {
        internal readonly JObject Wire;
        internal readonly byte[] Bytes;
        public string RequestSha256 { get; }
        public string Json => Encoding.UTF8.GetString(Bytes);
        internal GameplayInferenceRequest(JObject wire)
        {
            Wire = wire;
            Bytes = new UTF8Encoding(false, true).GetBytes(wire.ToString(Formatting.None));
            if (Bytes.Length > 65536) throw new ArgumentException("Inference body exceeds 64 KiB.");
            using (var sha = SHA256.Create()) RequestSha256 = BitConverter.ToString(sha.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
        }
    }

    public sealed class DialogueSelection
    {
        public string Status { get; internal set; }
        public string Reason { get; internal set; }
        public string Text { get; internal set; }
        public string Model { get; internal set; }
        public InferenceUsage Usage { get; internal set; }
    }

    /// <summary>Single-attempt authenticated transport. All JSON and identity validation precedes application.</summary>
    public sealed class GameplayInferenceClient : IDisposable
    {
        private sealed class ForecastScope
        {
            internal WorldSnapshot Basis;
            internal JArray ReadSet;
            internal JObject Perspective;
            internal readonly HashSet<string> Branches = new HashSet<string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, long> Sequences = new Dictionary<string, long>(StringComparer.Ordinal);
            internal bool Closed;
        }
        private readonly HttpClient http;
        private readonly string model;
        private readonly string dialogueModel;
        private readonly SemaphoreSlim registryGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, ForecastScope> forecasts = new Dictionary<string, ForecastScope>(StringComparer.Ordinal);
        private readonly Dictionary<StableId, ActorState> registeredActors = new Dictionary<StableId, ActorState>();
        private readonly Dictionary<StableId, long> registeredRevisions = new Dictionary<StableId, long>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HashSet<string> accountedUsage = new HashSet<string>(StringComparer.Ordinal);
        private string run;
        private long generation = -1, registeredTick;
        private bool disposed;
        public string LastLifecycleError { get; private set; }
        public event Action<string, InferenceUsage> UsageObserved;
        public bool HasDialogueProvider => !string.IsNullOrEmpty(dialogueModel);

        public GameplayInferenceClient(string baseUrl = "http://127.0.0.1:8788", string capability = null,
            string configuredModel = "jev-1.13.0", string configuredDialogueModel = null, HttpMessageHandler handler = null)
        {
            var address = new Uri(baseUrl, UriKind.Absolute);
            if (address.Scheme != "https" && !(address.Scheme == "http" && address.IsLoopback))
                throw new ArgumentException("Plain HTTP is permitted only on loopback.", nameof(baseUrl));
            if (!string.IsNullOrEmpty(address.UserInfo) || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
                throw new ArgumentException("Endpoint must not contain credentials, query or fragment.", nameof(baseUrl));
            model = configuredModel ?? throw new ArgumentNullException(nameof(configuredModel));
            dialogueModel = configuredDialogueModel;
            http = new HttpClient(handler ?? new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }, true)
            { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = Timeout.InfiniteTimeSpan };
            var token = capability ?? Environment.GetEnvironmentVariable("CHOOGUARD_GAMEPLAY_TOKEN");
            if (!string.IsNullOrEmpty(token)) http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<bool> RegisterOrUpdateSessionAsync(WorldSnapshot snapshot, CancellationToken token)
        {
            await registryGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (disposed) return false;
                bool fresh = run != snapshot.RunId.Value || generation != snapshot.Generation;
                if (run == snapshot.RunId.Value && snapshot.Generation < generation) { LastLifecycleError = "old_generation"; return false; }
                var actors = new JArray(); var revisions = new JArray();
                foreach (var actor in snapshot.Actors.Values)
                    if (fresh || !registeredActors.TryGetValue(actor.Id, out var old) || actor.RoleRevision > old.RoleRevision || actor.KnowledgeRevision > old.KnowledgeRevision)
                        actors.Add(new JObject { ["actorId"] = actor.Id.Value, ["roleId"] = actor.Role.ToString(), ["profileRevision"] = I64(actor.RoleRevision), ["observationRevision"] = I64(actor.KnowledgeRevision) });
                foreach (var entity in snapshot.Entities.Values)
                    if (fresh || !registeredRevisions.TryGetValue(entity.Id, out var old) || entity.Revision > old)
                        revisions.Add(new JObject { ["entityId"] = entity.Id.Value, ["revision"] = I64(entity.Revision) });
                var tick = fresh ? snapshot.Tick.Microseconds : Math.Max(snapshot.Tick.Microseconds, registeredTick);
                // Identity/metadata only, not model context. Batches bound even a 300-person registry below 64 KiB.
                int actorOffset = 0, revisionOffset = 0;
                do
                {
                    var actorBatch = new JArray(); var revisionBatch = new JArray();
                    for (int n = 0; n < 96 && actorOffset < actors.Count; n++) actorBatch.Add(actors[actorOffset++].DeepClone());
                    for (int n = 0; n < 96 && revisionOffset < revisions.Count; n++) revisionBatch.Add(revisions[revisionOffset++].DeepClone());
                    var wire = new JObject { ["schemaVersion"] = 1, ["runId"] = snapshot.RunId.Value, ["generation"] = Generation(snapshot.Generation),
                        ["mode"] = Mode(snapshot.Mode), ["tickUs"] = I64(tick), ["actors"] = actorBatch, ["revisions"] = revisionBatch };
                    var response = await PostAsync(fresh ? "session/register" : "session/update", new GameplayInferenceRequest(wire), token).ConfigureAwait(false);
                    if (!response.Success || (string)response.Body["status"] != (fresh ? "registered" : "updated") ||
                        (string)response.Body["runId"] != snapshot.RunId.Value || (long?)response.Body["generation"] != snapshot.Generation)
                    { LastLifecycleError = SafeReason(response.Body); return false; }
                    if (fresh) { forecasts.Clear(); registeredActors.Clear(); registeredRevisions.Clear(); }
                    run = snapshot.RunId.Value; generation = snapshot.Generation; registeredTick = tick; fresh = false;
                    foreach (JObject item in actorBatch)
                    {
                        var id = new StableId((string)item["actorId"]); registeredActors[id] = snapshot.Actors[id];
                    }
                    foreach (JObject item in revisionBatch)
                    {
                        var id = new StableId((string)item["entityId"]); registeredRevisions[id] = snapshot.Entities[id].Revision;
                    }
                } while (actorOffset < actors.Count || revisionOffset < revisions.Count);
                LastLifecycleError = null;
                return true;
            }
            catch (OperationCanceledException) { LastLifecycleError = "cancelled"; return false; }
            catch (HttpRequestException) { LastLifecycleError = "provider_error"; return false; }
            catch (JsonException) { LastLifecycleError = "invalid_provider_result"; return false; }
            finally { registryGate.Release(); }
        }

        public static GameplayInferenceRequest CreateNpcRequest(WorldSnapshot basis, ActorState actor,
            IReadOnlyList<GroundedCandidate> candidates, ActorActionIntent running = null, string requestId = null)
        {
            ValidateCandidates(candidates);
            if (actor.Plan == null) throw new ArgumentException("An autonomous goal is required.", nameof(actor));
            if (!basis.Entities.TryGetValue(actor.Id, out var actorEntity) || actorEntity.Kind != EntityKind.Actor)
                throw new ArgumentException("Deciding actor entity is missing from the basis.", nameof(basis));
            var observations = new JArray(); var memories = new JArray();
            for (int i = 0; i < actor.Memories.Count; i++)
            {
                var m = actor.Memories[i];
                if (!m.OwnerId.Equals(actor.Id)) throw new ArgumentException("Private observation owner mismatch.");
                var id = "observation-" + i.ToString(CultureInfo.InvariantCulture);
                var direct = m.Source != ObservationSource.Heard;
                observations.Add(new JObject { ["factId"] = id, ["subjectId"] = m.EntityId.Value, ["predicate"] = m.FactId,
                    ["truth"] = m.Value.ToString(), ["value"] = TruthValue(m.Value), ["sourceKind"] = direct ? "direct" : "report",
                    ["sourceId"] = m.InformantId?.Value ?? actor.Id.Value, ["observedTickUs"] = I64(m.Tick.Microseconds), ["validUntilTickUs"] = null });
            }
            // Relevant goal/assistance memories take precedence over merely recent unrelated sightings.
            for (int pass = 0; pass < 2 && memories.Count < 16; pass++)
                for (int i = actor.Memories.Count - 1; i >= 0 && memories.Count < 16; i--)
                {
                    var m = actor.Memories[i]; bool relevant = m.EntityId.Equals(actor.Plan.TargetId);
                    if (relevant != (pass == 0)) continue;
                    memories.Add(new JObject { ["memoryId"] = "memory-" + i, ["eventRefs"] = new JArray("observation-" + i),
                        ["summary"] = Limit(m.EntityId.Value + " " + m.FactId + " " + m.Value + (m.Source == ObservationSource.Heard ? " (전해 들음; 미검증)" : " (직접 관측)"), 512),
                        ["beliefStatus"] = m.Value == RuleTruth.CONFLICTED ? "disputed" : m.Source == ObservationSource.Heard ? "reported" : "observed",
                        ["rememberedTickUs"] = I64(m.Tick.Microseconds) });
                }
            var options = new JArray();
            foreach (var c in candidates)
            {
                if (!c.ActorId.Equals(actor.Id) || !c.Verb.HasValue) throw new ArgumentException("Candidate belongs to another actor.");
                var ids = new JArray(c.TargetId.Value);
                if (c.ToolId.HasValue && !c.ToolId.Value.Equals(c.TargetId)) ids.Add(c.ToolId.Value.Value);
                if (c.RecipientId.HasValue && !c.RecipientId.Value.Equals(c.TargetId) && !c.RecipientId.Equals(c.ToolId)) ids.Add(c.RecipientId.Value.Value);
                options.Add(new JObject { ["candidateId"] = c.CandidateId, ["actionId"] = c.TransitionId, ["targetIds"] = ids, ["summary"] = Limit(c.Summary, 512) });
            }
            var wire = Common(basis, requestId);
            wire["kind"] = "decision_request"; wire["actorId"] = actor.Id.Value; wire["decisionSeq"] = I64(actor.DecisionSequence);
            wire["basisTickUs"] = I64(basis.Tick.Microseconds); wire["expiresAtTickUs"] = I64(checked(basis.Tick.Microseconds + 5000000));
            wire["observationRevision"] = I64(actor.KnowledgeRevision); wire["deadlineMs"] = 2000;
            wire["actor"] = new JObject { ["roleId"] = actor.Role.ToString(), ["profileRevision"] = I64(actor.RoleRevision),
                ["persona"] = Limit(actor.Persona, 1024), ["currentActionId"] = running?.IntentId.Value,
                ["goals"] = new JArray(Limit(actor.Plan.Reason + (actor.Plan.BlockedReason == null ? "" : " 보류: " + actor.Plan.BlockedReason), 256)) };
            wire["observations"] = observations; wire["memories"] = memories; wire["candidates"] = options;
            wire["readSet"] = CandidateReads(candidates, actorEntity);
            return new GameplayInferenceRequest(wire);
        }

        public async Task<InferenceSelection> SelectNpcAsync(WorldSnapshot basis, ActorState actor, IReadOnlyList<GroundedCandidate> candidates,
            ActorActionIntent running, CancellationToken token)
        {
            var request = CreateNpcRequest(basis, actor, candidates, running);
            if (!await RegisterOrUpdateSessionAsync(basis, token).ConfigureAwait(false)) return Unavailable(LastLifecycleError, request);
            return await SelectAsync("npc/decision", request, token).ConfigureAwait(false);
        }

        /// <summary>Registers the root entity union across branches, not one combined model choice.</summary>
        public async Task<bool> RegisterFutureAsync(WorldSnapshot basis, string forecastId, IReadOnlyList<string> branchIds,
            IReadOnlyList<GroundedCandidate> candidates, CancellationToken token)
        {
            Id(forecastId);
            if (candidates == null || candidates.Count == 0) throw new ArgumentException("Forecast requires grounded scope candidates.", nameof(candidates));
            if (branchIds == null || branchIds.Count < 1 || branchIds.Count > 4) throw new ArgumentException("Forecast requires 1..4 branches.");
            if (!await RegisterOrUpdateSessionAsync(basis, token).ConfigureAwait(false)) return false;
            await registryGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var scope = new ForecastScope { Basis = basis, ReadSet = RootReads(basis, candidates), Perspective = Perspective(basis, candidates) };
                var branches = new JArray();
                foreach (var branch in branchIds)
                {
                    Id(branch); if (!scope.Branches.Add(branch)) throw new ArgumentException("Duplicate branch.");
                    branches.Add(new JObject { ["branchId"] = branch, ["perspective"] = scope.Perspective.DeepClone() });
                }
                var wire = new JObject { ["schemaVersion"] = 1, ["runId"] = basis.RunId.Value, ["generation"] = Generation(basis.Generation),
                    ["forecastId"] = forecastId, ["basisSnapshotId"] = basis.SnapshotId, ["basisTickUs"] = I64(basis.Tick.Microseconds),
                    ["horizonEndTickUs"] = I64(checked(basis.Tick.Microseconds + 30000000)), ["rulesetId"] = basis.RulesetId,
                    ["rulesetRevision"] = I64(basis.RulesetRevision), ["readSet"] = scope.ReadSet.DeepClone(), ["branches"] = branches };
                var response = await PostAsync("future/register", new GameplayInferenceRequest(wire), token).ConfigureAwait(false);
                if (!response.Success || (string)response.Body["status"] != "registered" || (string)response.Body["forecastId"] != forecastId)
                { LastLifecycleError = SafeReason(response.Body); return false; }
                forecasts.Add(forecastId, scope); LastLifecycleError = null; return true;
            }
            catch (OperationCanceledException) { LastLifecycleError = "cancelled"; return false; }
            catch (HttpRequestException) { LastLifecycleError = "provider_error"; return false; }
            catch (JsonException) { LastLifecycleError = "invalid_provider_result"; return false; }
            finally { registryGate.Release(); }
        }

        public async Task<bool> CloseFutureAsync(WorldSnapshot basis, string forecastId, CancellationToken token)
        {
            await registryGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (forecasts.TryGetValue(forecastId, out var scope)) scope.Closed = true;
                var wire = new JObject { ["schemaVersion"] = 1, ["runId"] = basis.RunId.Value, ["generation"] = Generation(basis.Generation), ["forecastId"] = forecastId };
                var response = await PostAsync("future/close", new GameplayInferenceRequest(wire), token).ConfigureAwait(false);
                bool ok = response.Success && (string)response.Body["status"] == "closed" && (string)response.Body["forecastId"] == forecastId;
                LastLifecycleError = ok ? null : SafeReason(response.Body); return ok;
            }
            catch (OperationCanceledException) { LastLifecycleError = "cancelled"; return false; }
            catch (HttpRequestException) { LastLifecycleError = "provider_error"; return false; }
            catch (JsonException) { LastLifecycleError = "invalid_provider_result"; return false; }
            finally { registryGate.Release(); }
        }

        public async Task<InferenceSelection> SelectFutureAsync(WorldSnapshot basis, WorldSnapshot hypothetical, string forecastId,
            string branchId, int depth, IReadOnlyList<GroundedCandidate> candidates, bool nextEnvironment, CancellationToken token)
        {
            GameplayInferenceRequest request;
            ForecastScope scope;
            await registryGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!forecasts.TryGetValue(forecastId, out scope) || scope.Closed || !scope.Branches.Contains(branchId) ||
                    scope.Basis.SnapshotId != basis.SnapshotId || generation != basis.Generation || run != basis.RunId.Value)
                    return new InferenceSelection("unavailable", "request_expired");
                scope.Sequences.TryGetValue(branchId, out var sequence);
                sequence = checked(sequence + 1); scope.Sequences[branchId] = sequence;
                request = CreateFutureRequest(basis, hypothetical, forecastId, branchId, sequence, depth, candidates, nextEnvironment,
                    scope.ReadSet, scope.Perspective);
            }
            finally { registryGate.Release(); }
            var result = await SelectAsync("future/step", request, token).ConfigureAwait(false);
            if (result.Status == "ok")
            {
                await registryGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    bool stale = scope.Closed || generation != basis.Generation || run != basis.RunId.Value;
                    foreach (JObject read in scope.ReadSet)
                        if (!registeredRevisions.TryGetValue(new StableId((string)read["entityId"]), out var revision) ||
                            I64(revision) != (string)read["revision"]) { stale = true; break; }
                    if (stale) return new InferenceSelection("unavailable", "request_expired", hash: result.RequestSha256, model: result.Model, usage: result.Usage);
                }
                finally { registryGate.Release(); }
            }
            return result;
        }

        private static GameplayInferenceRequest CreateFutureRequest(WorldSnapshot basis, WorldSnapshot state, string forecastId,
            string branchId, long sequence, int depth, IReadOnlyList<GroundedCandidate> candidates, bool nextEnvironment, JArray reads, JObject perspective)
        {
            ValidateCandidates(candidates);
            if (depth < 0 || depth > 3 || state.Tick.Microseconds < basis.Tick.Microseconds || state.Tick.Microseconds > basis.Tick.Microseconds + 30000000 ||
                (nextEnvironment && (depth != 0 || state.Tick.Microseconds != basis.Tick.Microseconds || (string)perspective["kind"] != "world")))
                throw new ArgumentException("Invalid forecast time or perspective.");
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            var options = new JArray();
            foreach (var c in candidates)
            {
                if (c.ActorId?.Value != (string)perspective["actorId"]) throw new ArgumentException("Mixed future perspectives.");
                var bindings = new JArray(new JObject { ["slot"] = "source", ["entityId"] = c.SourceId.Value }, new JObject { ["slot"] = "target", ["entityId"] = c.TargetId.Value });
                if (c.ToolId.HasValue) bindings.Add(new JObject { ["slot"] = "tool", ["entityId"] = c.ToolId.Value.Value });
                if (c.RecipientId.HasValue) bindings.Add(new JObject { ["slot"] = "recipient", ["entityId"] = c.RecipientId.Value.Value });
                var causes = new JArray();
                foreach (var cause in c.CauseFactIds) { causes.Add(cause); wanted.Add(cause); }
                if (c.TransitionId == "hold" || c.CandidateId == "env-hold")
                {
                    if ((string)perspective["kind"] != "world" || !TransitionKernel.IsEnvironmentHold(c) ||
                        !TransitionKernel.ReadSetCurrent(state.Entities, c.ReadSet) || !basis.Entities.TryGetValue(c.SourceId, out var anchor))
                        throw new ArgumentException("Invalid no-effect environment hold.");
                    bool grounded = false;
                    foreach (JObject read in reads)
                        if ((string)read["entityId"] == anchor.Id.Value && (string)read["revision"] == I64(anchor.Revision)) { grounded = true; break; }
                    if (!grounded) throw new ArgumentException("Environment hold requires a basis read-set anchor.");
                }
                else if (causes.Count == 0) throw new ArgumentException("Transition requires causal evidence.");
                if (causes.Count > 16) throw new ArgumentException("Transition requires bounded causal evidence.");
                options.Add(new JObject { ["candidateId"] = c.CandidateId, ["transitionId"] = c.TransitionId, ["actorId"] = c.ActorId?.Value,
                    ["bindings"] = bindings, ["parameterSetId"] = c.ParameterSetId, ["causeFactIds"] = causes, ["summary"] = Limit(c.Summary, 1536) });
            }
            var facts = new JArray();
            if ((string)perspective["kind"] == "world")
            {
                foreach (var entity in state.Entities.Values)
                    foreach (var fact in entity.Facts)
                    {
                        var id = TransitionKernel.FactId(entity.Id, fact.Key);
                        if (!wanted.Remove(id)) continue;
                        bool actual = basis.Entities.TryGetValue(entity.Id, out var root) && root.Fact(fact.Key) == fact.Value;
                        facts.Add(FutureFact(id, entity.Id, fact.Key, fact.Value, nextEnvironment || actual ? "actual" : "hypothetical", entity.Id, state.Tick));
                    }
            }
            else
            {
                var owner = new StableId((string)perspective["actorId"]);
                if (!state.Actors.TryGetValue(owner, out var actor)) throw new ArgumentException("Unknown future actor.");
                foreach (var memory in actor.Memories)
                {
                    var id = TransitionKernel.FactId(memory.EntityId, memory.FactId);
                    if (wanted.Remove(id)) facts.Add(FutureFact(id, memory.EntityId, memory.FactId, memory.Value,
                        memory.Source == ObservationSource.Heard ? "reported" : "hypothetical", memory.InformantId ?? owner, memory.Tick));
                }
            }
            if (wanted.Count != 0 || facts.Count < 1 || facts.Count > 64) throw new ArgumentException("Cause is not present in the permitted perspective.");
            var wire = Common(basis, null);
            wire["kind"] = "future_step_request"; wire["forecastId"] = forecastId; wire["branchId"] = branchId; wire["stepSeq"] = I64(sequence);
            wire["purpose"] = nextEnvironment ? "next_environment" : "counterfactual"; wire["basisSnapshotId"] = basis.SnapshotId;
            wire["basisTickUs"] = I64(basis.Tick.Microseconds); wire["stepTickUs"] = I64(state.Tick.Microseconds);
            wire["horizonEndTickUs"] = I64(checked(basis.Tick.Microseconds + 30000000)); wire["expiresAtTickUs"] = I64(checked(basis.Tick.Microseconds + 30000000));
            wire["depth"] = depth; wire["deadlineMs"] = 2000; wire["rulesetId"] = basis.RulesetId; wire["rulesetRevision"] = I64(basis.RulesetRevision);
            wire["perspective"] = perspective.DeepClone();
            if ((string)perspective["kind"] == "actor")
                wire["perspective"]["knowledgeRevision"] = I64(state.Actors[new StableId((string)perspective["actorId"])].KnowledgeRevision);
            wire["stateFacts"] = facts; wire["candidates"] = options; wire["readSet"] = reads.DeepClone();
            return new GameplayInferenceRequest(wire);
        }

        /// <summary>Optional free conversation only. Returned prose has no WorldSession reference or command authority.</summary>
        public async Task<DialogueSelection> GenerateDialogueAsync(WorldSnapshot basis, ActorState speaker, string playerMessage,
            long dialogueSequence, CancellationToken token)
        {
            if (!HasDialogueProvider) return new DialogueSelection { Status = "unavailable", Reason = "provider_not_configured" };
            if (string.IsNullOrWhiteSpace(playerMessage) || playerMessage.Length > 512)
                throw new ArgumentException("Conversation input must contain 1..512 characters.", nameof(playerMessage));
            var facts = new JArray();
            for (int i = speaker.Memories.Count - 1; i >= 0 && facts.Count < 32; i--)
            {
                var memory = speaker.Memories[i];
                if (!memory.OwnerId.Equals(speaker.Id)) throw new ArgumentException("Dialogue private knowledge owner mismatch.");
                facts.Add(new JObject { ["factId"] = "dialogue-fact-" + i,
                    ["text"] = Limit(memory.EntityId.Value + " " + memory.FactId + ": " + memory.Value +
                        (memory.Source == ObservationSource.Heard ? " (전해 들음, 사실 미검증)" : " (직접 관측)"), 256),
                    ["truth"] = memory.Source == ObservationSource.Heard ? "UNKNOWN" : memory.Value.ToString(),
                    ["sourceId"] = memory.InformantId?.Value ?? speaker.Id.Value });
            }
            var wire = Common(basis, null);
            wire["kind"] = "dialogue_request"; wire["actorId"] = speaker.Id.Value; wire["dialogueSeq"] = I64(dialogueSequence);
            wire["basisTickUs"] = I64(basis.Tick.Microseconds); wire["expiresAtTickUs"] = I64(checked(basis.Tick.Microseconds + 5000000));
            wire["deadlineMs"] = 2000; wire["roleId"] = speaker.Role.ToString(); wire["profileRevision"] = I64(speaker.RoleRevision);
            wire["observationRevision"] = I64(speaker.KnowledgeRevision); wire["channel"] = "free_conversation";
            wire["messages"] = new JArray(new JObject { ["role"] = "user", ["content"] = playerMessage });
            wire["knownFacts"] = facts; wire["truthSlots"] = new JArray();
            var request = new GameplayInferenceRequest(wire);
            if (!await RegisterOrUpdateSessionAsync(basis, token).ConfigureAwait(false))
                return new DialogueSelection { Status = "unavailable", Reason = LastLifecycleError };
            var clock = Stopwatch.StartNew();
            try
            {
                var response = await PostAsync("dialogue/generate", request, token).ConfigureAwait(false);
                var result = response.Body;
                Fields(result, "schemaVersion,runId,generation,requestId,requestSha256,kind,actorId,dialogueSeq,channel,authority,status,provider,model,text,factRefs,usage,upstreamLatencyMs,reasonCode");
                foreach (var key in "schemaVersion,runId,generation,requestId,actorId,dialogueSeq,channel".Split(','))
                    if (!JToken.DeepEquals(result[key], wire[key])) throw new JsonException("Dialogue identity mismatch.");
                if ((string)result["requestSha256"] != request.RequestSha256 || (string)result["kind"] != "dialogue_result" ||
                    (string)result["authority"] != "text_only" || (string)result["provider"] != "openai-compatible")
                    throw new JsonException("Uncorrelated dialogue result.");
                var usage = ReadUsage(result["usage"]); NumberOrNull(result["upstreamLatencyMs"], 0, 9007199254740991d);
                RecordUsage(request.RequestSha256, usage);
                string status = StringOrNull(result["status"]), reason = StringOrNull(result["reasonCode"]);
                if (!(result["factRefs"] is JArray references)) throw new JsonException("Dialogue fact refs missing.");
                if (status == "unavailable")
                {
                    if (!Null(result["text"]) || !UnavailableReason(reason) || references.Count != 0) throw new JsonException("Malformed unavailable dialogue.");
                    return new DialogueSelection { Status = status, Reason = reason, Usage = usage };
                }
                if (!response.Success || status != "ok" || reason != "generated" || (string)result["model"] != dialogueModel)
                    throw new JsonException("Unpinned dialogue provider.");
                var allowed = new HashSet<string>(StringComparer.Ordinal);
                foreach (JObject fact in facts) allowed.Add((string)fact["factId"]);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reference in references)
                {
                    var id = StringOrNull(reference);
                    if (id == null || !allowed.Contains(id) || !seen.Add(id)) throw new JsonException("Unknown dialogue fact reference.");
                }
                string text = StringOrNull(result["text"]);
                if (string.IsNullOrWhiteSpace(text) || text.Length > 512) throw new JsonException("Invalid dialogue text.");
                if (token.IsCancellationRequested || disposed || generation != basis.Generation || run != basis.RunId.Value ||
                    clock.ElapsedMilliseconds > 2000 || registeredTick >= basis.Tick.Microseconds + 5000000)
                    return new DialogueSelection { Status = "unavailable", Reason = "request_expired", Usage = usage };
                return new DialogueSelection { Status = "ok", Reason = "generated", Text = text, Model = dialogueModel, Usage = usage };
            }
            catch (OperationCanceledException) { return new DialogueSelection { Status = "unavailable", Reason = token.IsCancellationRequested ? "cancelled" : "timeout" }; }
            catch (HttpRequestException) { return new DialogueSelection { Status = "unavailable", Reason = "provider_error" }; }
            catch (JsonException) { return new DialogueSelection { Status = "unavailable", Reason = "invalid_provider_result" }; }
        }

        /// <summary>Explicit accounting lookup, not an inference retry. The broker keeps old-generation billing receipts.</summary>
        public Task<InferenceUsage> ReadRequestUsageAsync(StableId runId, long requestGeneration, string requestSha256, CancellationToken token) =>
            RequestControlAsync(runId, requestGeneration, requestSha256, false, token);

        public Task<InferenceUsage> CancelRequestAsync(StableId runId, long requestGeneration, string requestSha256, CancellationToken token) =>
            RequestControlAsync(runId, requestGeneration, requestSha256, true, token);

        private async Task<InferenceUsage> RequestControlAsync(StableId runId, long requestGeneration, string hash, bool cancel, CancellationToken token)
        {
            if (hash == null || hash.Length != 64) throw new ArgumentException("SHA-256 identity required.", nameof(hash));
            var request = new GameplayInferenceRequest(new JObject { ["schemaVersion"] = 1, ["runId"] = runId.Value,
                ["generation"] = Generation(requestGeneration), ["requestSha256"] = hash });
            var response = await PostAsync(cancel ? "request/cancel" : "request/usage", request, token).ConfigureAwait(false);
            if (!response.Success) return null;
            Fields(response.Body, "status,requestSha256,usage,finished");
            if ((string)response.Body["requestSha256"] != hash || response.Body["finished"].Type != JTokenType.Boolean ||
                ((string)response.Body["status"] != "known" && (string)response.Body["status"] != "unknown" && (string)response.Body["status"] != "cancelled"))
                throw new JsonException("Invalid accounting receipt.");
            var usage = ReadUsage(response.Body["usage"]); RecordUsage(hash, usage); return usage;
        }

        private void RecordUsage(string hash, InferenceUsage usage)
        {
            if (usage == null) return;
            lock (accountedUsage) { if (!accountedUsage.Add(hash)) return; }
            UsageObserved?.Invoke(hash, usage);
        }

        private async Task<InferenceSelection> SelectAsync(string route, GameplayInferenceRequest request, CancellationToken token)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                var response = await PostAsync(route, request, token).ConfigureAwait(false);
                var selected = ValidateResponse(request, response.Body.ToString(Formatting.None), model, response.Success);
                RecordUsage(request.RequestSha256, selected.Usage);
                if (selected.Status != "ok") return selected;
                if (token.IsCancellationRequested || disposed) return Unavailable("cancelled", request, selected.Usage);
                if (clock.ElapsedMilliseconds > (int)request.Wire["deadlineMs"] || run != (string)request.Wire["runId"] || generation != (long)request.Wire["generation"] ||
                    registeredTick >= long.Parse((string)request.Wire["expiresAtTickUs"], CultureInfo.InvariantCulture)) return Unavailable("request_expired", request, selected.Usage);
                return selected;
            }
            catch (OperationCanceledException) { return Unavailable(token.IsCancellationRequested || disposed ? "cancelled" : "timeout", request); }
            catch (HttpRequestException) { return Unavailable("provider_error", request); }
            catch (JsonException) { return Unavailable("invalid_provider_result", request); }
            catch (InvalidDataException) { return Unavailable("invalid_provider_result", request); }
        }

        public static InferenceSelection ValidateResponse(GameplayInferenceRequest request, string json, string expectedModel, bool httpSuccess = true)
        {
            if (json == null) throw new JsonException("Missing response.");
            Unicode(json);
            var response = ParseStrict(Encoding.UTF8.GetBytes(json));
            var future = (string)request.Wire["kind"] == "future_step_request";
            Fields(response, future ? "schemaVersion,runId,generation,requestId,forecastId,branchId,stepSeq,kind,requestSha256,status,provider,model,selectedCandidateId,probabilities,confidence,upstreamLatencyMs,usage,reasonCode" :
                "schemaVersion,runId,generation,requestId,actorId,decisionSeq,kind,requestSha256,status,provider,model,selectedCandidateId,probabilities,confidence,upstreamLatencyMs,usage,reasonCode");
            foreach (var key in (future ? "schemaVersion,runId,generation,requestId,forecastId,branchId,stepSeq" : "schemaVersion,runId,generation,requestId,actorId,decisionSeq").Split(','))
                if (!JToken.DeepEquals(response[key], request.Wire[key])) throw new JsonException("Response identity mismatch.");
            if (StringOrNull(response["kind"]) != (future ? "future_step_result" : "decision_result") || StringOrNull(response["requestSha256"]) != request.RequestSha256 ||
                StringOrNull(response["provider"]) != "typesafe-direct") throw new JsonException("Uncorrelated inference result.");
            var usage = ReadUsage(response["usage"]);
            var latency = NumberOrNull(response["upstreamLatencyMs"], 0, future ? 9007199254740991d : 60000d);
            if (future && latency.HasValue && latency.Value != Math.Truncate(latency.Value)) throw new JsonException("Future latency must be integral.");
            var resultModel = StringOrNull(response["model"]);
            if (resultModel != null && (resultModel.Length < 1 || resultModel.Length > 128)) throw new JsonException("Invalid model.");
            string status = StringOrNull(response["status"]), reason = StringOrNull(response["reasonCode"]);
            // Nullable unavailability is valid even when no model call occurred. Never inspect it as a success.
            if (status == "unavailable")
            {
                if (!Null(response["selectedCandidateId"]) || !Null(response["probabilities"]) || !Null(response["confidence"]) ||
                    !UnavailableReason(reason)) throw new JsonException("Invalid unavailable result.");
                return new InferenceSelection(status, reason, hash: request.RequestSha256, model: resultModel, usage: usage, latency: latency);
            }
            if (status != "ok" || !httpSuccess || reason != "selected" || resultModel != expectedModel) throw new JsonException("Invalid successful inference.");
            if (!future && (usage == null || latency == null)) throw new JsonException("NPC success requires measured usage and latency.");
            double confidence = Number(response["confidence"], 0, 1);
            string choice = StringOrNull(response["selectedCandidateId"]);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in (JArray)request.Wire["candidates"]) ids.Add((string)c["candidateId"]);
            if (choice == null || !ids.Contains(choice) || !(response["probabilities"] is JArray rows) || rows.Count != ids.Count) throw new JsonException("Candidate set mismatch.");
            var probabilities = new Dictionary<string, double>(StringComparer.Ordinal);
            double sum = 0, maximum = 0;
            foreach (var token in rows)
            {
                if (!(token is JObject row)) throw new JsonException("Invalid candidate probability.");
                Fields(row, "candidateId,probability"); var id = StringOrNull(row["candidateId"]);
                if (id == null || !ids.Contains(id) || probabilities.ContainsKey(id)) throw new JsonException("Candidate set mismatch.");
                var p = Number(row["probability"], 0, 1); probabilities.Add(id, p); sum += p; maximum = Math.Max(maximum, p);
            }
            if (Math.Abs(sum - 1) > .000001 || probabilities[choice] != maximum) throw new JsonException("Inconsistent choice probabilities.");
            return new InferenceSelection(status, reason, choice, request.RequestSha256, resultModel, probabilities, usage, confidence, latency);
        }

        private sealed class HttpResult { internal bool Success; internal JObject Body; }
        private sealed class StrictReader : JsonTextReader
        {
            internal StrictReader(TextReader input) : base(input) { }
            public override bool Read()
            {
                bool read = base.Read();
                if (read && (TokenType == JsonToken.PropertyName || TokenType == JsonToken.String) && QuoteChar != '"')
                    throw new JsonException("JSON property names and strings require double quotes.");
                return read;
            }
        }
        private async Task<HttpResult> PostAsync(string route, GameplayInferenceRequest request, CancellationToken token)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            using (var content = new ByteArrayContent(request.Bytes))
            {
                timeout.CancelAfter((int?)request.Wire["deadlineMs"] ?? 5000);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                using (var message = new HttpRequestMessage(HttpMethod.Post, route) { Content = content })
                using (var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var bytes = new MemoryStream())
                {
                    if (response.Content.Headers.ContentLength > 65536) throw new JsonException("Response too large.");
                    var buffer = new byte[4096];
                    while (true)
                    {
                        int count = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
                        if (count == 0) break;
                        if (bytes.Length + count > 65536) throw new JsonException("Response too large.");
                        bytes.Write(buffer, 0, count);
                    }
                    return new HttpResult { Success = response.IsSuccessStatusCode, Body = ParseStrict(bytes.ToArray()) };
                }
            }
        }

        internal static JObject ParseStrict(byte[] bytes)
        {
            if (bytes.Length > 65536) throw new JsonException("JSON too large.");
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { throw new JsonException("Invalid UTF-8."); }
            Unicode(text); CheckLexical(text);
            using (var input = new StringReader(text))
            using (var reader = new StrictReader(input) { MaxDepth = 16, DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            {
                var value = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                if (!(value is JObject obj) || reader.Read()) throw new JsonException("Invalid JSON root or trailing content.");
                foreach (var token in obj.DescendantsAndSelf())
                {
                    if (token.Type == JTokenType.Comment || token.Type == JTokenType.Undefined || token.Type == JTokenType.Constructor) throw new JsonException("Non-JSON token.");
                    if (token.Type == JTokenType.String) Unicode((string)token);
                    if (token is JProperty property) Unicode(property.Name);
                    if (token.Type == JTokenType.Float) { var number = (double)token; if (double.IsNaN(number) || double.IsInfinity(number)) throw new JsonException("Non-finite number."); }
                }
                return obj;
            }
        }

        // Json.NET deliberately accepts JavaScript extensions; reject those lexical forms before its structural parser.
        private static void CheckLexical(string text)
        {
            int i = 0; char previous = '\0';
            while (i < text.Length)
            {
                char c = text[i];
                if (c == ' ' || c == '\r' || c == '\n' || c == '\t') { i++; continue; }
                if (c == '"')
                {
                    i++; bool closed = false;
                    while (i < text.Length)
                    {
                        c = text[i++];
                        if (c == '"') { closed = true; break; }
                        if (c < 32) throw new JsonException("Control in string.");
                        if (c != '\\') continue;
                        if (i >= text.Length) throw new JsonException("Incomplete escape.");
                        c = text[i++];
                        if (c == 'u')
                        {
                            int value = ReadHex4(text, ref i);
                            if (value >= 0xd800 && value <= 0xdbff)
                            {
                                if (i + 2 > text.Length || text[i++] != '\\' || text[i++] != 'u') throw new JsonException("Unpaired escaped surrogate.");
                                int low = ReadHex4(text, ref i);
                                if (low < 0xdc00 || low > 0xdfff) throw new JsonException("Unpaired escaped surrogate.");
                            }
                            else if (value >= 0xdc00 && value <= 0xdfff) throw new JsonException("Unpaired escaped surrogate.");
                        }
                        else if ("\"\\/bfnrt".IndexOf(c) < 0) throw new JsonException("Invalid string escape.");
                    }
                    if (!closed) throw new JsonException("Unterminated string.");
                    previous = 's'; continue;
                }
                if (c == '-' || (c >= '0' && c <= '9'))
                {
                    if (c == '-') { i++; if (i == text.Length) throw new JsonException("Invalid number."); }
                    if (text[i] == '0') i++;
                    else { int start = i; while (i < text.Length && text[i] >= '0' && text[i] <= '9') i++; if (start == i) throw new JsonException("Invalid number."); }
                    if (i < text.Length && text[i] == '.') { i++; int start = i; while (i < text.Length && char.IsDigit(text[i])) i++; if (start == i) throw new JsonException("Invalid fraction."); }
                    if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
                    { i++; if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++; int start = i; while (i < text.Length && char.IsDigit(text[i])) i++; if (start == i) throw new JsonException("Invalid exponent."); }
                    if (i < text.Length && ",]} \r\n\t".IndexOf(text[i]) < 0) throw new JsonException("Noncanonical JSON number.");
                    previous = 'n'; continue;
                }
                if (c == 't' || c == 'f' || c == 'n')
                {
                    var literal = c == 't' ? "true" : c == 'f' ? "false" : "null";
                    if (i + literal.Length > text.Length || string.CompareOrdinal(text, i, literal, 0, literal.Length) != 0) throw new JsonException("Invalid literal.");
                    i += literal.Length; previous = 'v'; continue;
                }
                if ("{}[],:".IndexOf(c) < 0 || ((c == '}' || c == ']') && previous == ',')) throw new JsonException("Non-JSON syntax.");
                previous = c; i++;
            }
        }
        private static int ReadHex4(string text, ref int cursor)
        {
            int value = 0;
            for (int n = 0; n < 4; n++)
            {
                if (cursor >= text.Length || !Uri.IsHexDigit(text[cursor])) throw new JsonException("Invalid Unicode escape.");
                char c = text[cursor++]; value = value * 16 + (c <= '9' ? c - '0' : char.ToLowerInvariant(c) - 'a' + 10);
            }
            return value;
        }
        private static void Unicode(string text)
        {
            for (int i = 0; i < text.Length; i++)
                if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) throw new JsonException("Unpaired surrogate."); }
                else if (char.IsLowSurrogate(text[i])) throw new JsonException("Unpaired surrogate.");
        }
        private static JObject Common(WorldSnapshot basis, string requestId) => new JObject { ["schemaVersion"] = 1,
            ["runId"] = basis.RunId.Value, ["generation"] = Generation(basis.Generation), ["requestId"] = requestId ?? "request-" + Guid.NewGuid().ToString("N"), ["mode"] = Mode(basis.Mode) };
        private static JObject FutureFact(string id, StableId subject, string predicate, RuleTruth truth, string origin, StableId source, SimTick tick) =>
            new JObject { ["factId"] = id, ["subjectId"] = subject.Value, ["predicate"] = predicate, ["truth"] = truth.ToString(), ["value"] = TruthValue(truth),
                ["origin"] = origin, ["sourceId"] = source.Value, ["observedTickUs"] = I64(tick.Microseconds) };
        private static JToken TruthValue(RuleTruth value) => value == RuleTruth.TRUE ? new JValue(true) : value == RuleTruth.FALSE ? new JValue(false) : JValue.CreateNull();
        private static JObject Perspective(WorldSnapshot basis, IReadOnlyList<GroundedCandidate> candidates)
        {
            var actorId = candidates[0].ActorId;
            foreach (var candidate in candidates) if (!candidate.ActorId.Equals(actorId)) throw new ArgumentException("Mixed perspective candidates.");
            return actorId.HasValue ? new JObject { ["kind"] = "actor", ["actorId"] = actorId.Value.Value, ["knowledgeRevision"] = I64(basis.Actors[actorId.Value].KnowledgeRevision) } :
                new JObject { ["kind"] = "world", ["actorId"] = null, ["knowledgeRevision"] = null };
        }
        private static JArray RootReads(WorldSnapshot basis, IReadOnlyList<GroundedCandidate> candidates)
        {
            var values = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
                foreach (var read in candidate.ReadSet)
                {
                    if (values.ContainsKey(read.EntityId.Value)) continue;
                    if (!basis.Entities.TryGetValue(read.EntityId, out var root)) throw new ArgumentException("Unregistered forecast entity.");
                    values.Add(root.Id.Value, root.Revision);
                }
            return SerializeReads(values);
        }
        private static JArray CandidateReads(IReadOnlyList<GroundedCandidate> candidates, WorldEntity actor = null)
        {
            var values = new SortedDictionary<string, long>(StringComparer.Ordinal);
            if (actor != null) values.Add(actor.Id.Value, actor.Revision);
            foreach (var candidate in candidates)
                foreach (var read in candidate.ReadSet)
                {
                    if (values.TryGetValue(read.EntityId.Value, out var existing) && existing != read.Revision) throw new ArgumentException("Conflicting read revisions.");
                    values[read.EntityId.Value] = read.Revision;
                }
            return SerializeReads(values);
        }
        private static JArray SerializeReads(SortedDictionary<string, long> values)
        {
            if (values.Count == 0 || values.Count > 64) throw new ArgumentException("Read set must contain 1..64 entities.");
            var result = new JArray();
            foreach (var pair in values) result.Add(new JObject { ["entityId"] = pair.Key, ["revision"] = I64(pair.Value) });
            return result;
        }
        private static void ValidateCandidates(IReadOnlyList<GroundedCandidate> candidates)
        {
            if (candidates == null || candidates.Count < 2 || candidates.Count > 32) throw new ArgumentException("Choice requires 2..32 candidates.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates) { Id(candidate.CandidateId); if (!ids.Add(candidate.CandidateId)) throw new ArgumentException("Duplicate candidate."); }
        }
        private static string I64(long value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); return value.ToString(CultureInfo.InvariantCulture); }
        private static int Generation(long value) { if (value < 0 || value > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value)); return (int)value; }
        private static string Mode(GameplayMode mode) => mode == GameplayMode.Tutorial ? "TUTORIAL" : "RANDOM_OPERATIONS_LAB";
        private static void Id(string id) { _ = new StableId(id); }
        private static string Limit(string text, int count) => text.Length <= count ? text : text.Substring(0, char.IsHighSurrogate(text[count - 1]) ? count - 1 : count);
        private static bool Null(JToken token) => token != null && token.Type == JTokenType.Null;
        private static string StringOrNull(JToken token) { if (Null(token)) return null; if (token?.Type != JTokenType.String) throw new JsonException("Expected string."); return (string)token; }
        private static double Number(JToken token, double minimum, double maximum)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new JsonException("Expected numeric value.");
            double value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum) throw new JsonException("Numeric value out of range.");
            return value;
        }
        private static double? NumberOrNull(JToken token, double minimum, double maximum) => Null(token) ? (double?)null : Number(token, minimum, maximum);
        private static InferenceUsage ReadUsage(JToken token)
        {
            if (Null(token)) return null;
            if (!(token is JObject usage)) throw new JsonException("Invalid usage.");
            Fields(usage, "inputTokens,outputTokens");
            if (usage["inputTokens"].Type != JTokenType.Integer || usage["outputTokens"].Type != JTokenType.Integer) throw new JsonException("Usage must be integral.");
            return new InferenceUsage((long)Number(usage["inputTokens"], 0, 9007199254740991d), (long)Number(usage["outputTokens"], 0, 9007199254740991d));
        }
        private static void Fields(JObject value, string fields)
        {
            var expected = new HashSet<string>(fields.Split(','), StringComparer.Ordinal);
            foreach (var property in value.Properties()) if (!expected.Remove(property.Name)) throw new JsonException("Unexpected JSON field.");
            if (expected.Count != 0) throw new JsonException("Missing JSON field.");
        }
        private static bool UnavailableReason(string reason) => reason == "timeout" || reason == "rate_limited" || reason == "provider_error" ||
            reason == "invalid_provider_result" || reason == "cancelled" || reason == "queue_full" || reason == "budget_exhausted" || reason == "request_expired";
        private static string SafeReason(JObject body) => body["reasonCode"]?.Type == JTokenType.String && UnavailableReason((string)body["reasonCode"]) ? (string)body["reasonCode"] : "provider_error";
        private static InferenceSelection Unavailable(string reason, GameplayInferenceRequest request, InferenceUsage usage = null) =>
            new InferenceSelection("unavailable", reason ?? "provider_error", hash: request.RequestSha256, usage: usage);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; lifetime.Cancel(); http.Dispose();
            // In-flight awaiters own their semaphore release; disposing it here would race their finally blocks.
        }
    }
}
