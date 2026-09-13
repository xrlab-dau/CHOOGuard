using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable] public sealed class VoiceServiceConfig
    {
        public string HttpUrl, WebSocketUrl, ApiKey, ApiSecret;
    }

    public enum FileReadState { Success, NotFound, Failure }

    public sealed class FileReadResult
    {
        private FileReadResult(FileReadState state, byte[] bytes) { State = state; Bytes = bytes; }
        public FileReadState State { get; private set; }
        public byte[] Bytes { get; private set; }
        public static FileReadResult Success(byte[] bytes) => new FileReadResult(FileReadState.Success, bytes == null ? null : bytes.ToArray());
        public static FileReadResult NotFound() => new FileReadResult(FileReadState.NotFound, null);
        public static FileReadResult Failure() => new FileReadResult(FileReadState.Failure, null);
    }

    public interface IVoiceEpochFileWriter : IDisposable
    {
        void Write(string json);
        void FlushWriter();
        void FlushDurable();
    }

    public interface IVoiceEpochFiles
    {
        FileReadResult ReadCanonical(string path);
        void CreateDirectory(string path);
        IVoiceEpochFileWriter OpenTemporary(string path);
        void MoveTemporary(string temporary, string canonical);
        void ReplaceTemporary(string temporary, string canonical, string backup);
        void DeleteTemporary(string temporary);
    }

    public interface IVoiceServiceTransport
    {
        IEnumerator Call(string method, string json, string administrationRoom, Action<bool> complete);
    }

    internal interface IDurableVoiceDispatchStore
    {
        bool TryBeginDispatch(string operation);
        bool TryCompleteDispatch(string operation);
    }

    /// <summary>
    /// A single-process, single-writer canonical file adapter. It verifies the canonical bytes after every
    /// commit attempt, including acknowledgement loss. It does not claim protection from arbitrary OS or power loss.
    /// </summary>
    public sealed class PersistentVoiceEpochStore : IVoiceEpochStore, IDurableVoiceDispatchStore
    {
        private sealed class SystemFiles : IVoiceEpochFiles
        {
            private sealed class Writer : IVoiceEpochFileWriter
            {
                private readonly FileStream stream;
                private readonly StreamWriter writer;
                public Writer(string path)
                {
                    stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                    writer = new StreamWriter(stream, new UTF8Encoding(false));
                }
                public void Write(string json) => writer.Write(json);
                public void FlushWriter() => writer.Flush();
                public void FlushDurable() => stream.Flush(true);
                public void Dispose() { writer.Dispose(); stream.Dispose(); }
            }

            public FileReadResult ReadCanonical(string path)
            {
                try { return FileReadResult.Success(File.ReadAllBytes(path)); }
                catch (FileNotFoundException) { return FileReadResult.NotFound(); }
                catch (DirectoryNotFoundException) { return FileReadResult.NotFound(); }
                catch { return FileReadResult.Failure(); }
            }
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public IVoiceEpochFileWriter OpenTemporary(string path) => new Writer(path);
            public void MoveTemporary(string temporary, string canonical) => File.Move(temporary, canonical);
            public void ReplaceTemporary(string temporary, string canonical, string backup) => File.Replace(temporary, canonical, backup);
            public void DeleteTemporary(string temporary) { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string path;
        private readonly IVoiceEpochFiles files;
        private bool hasObservation;
        private FileReadResult observedCanonical;

        public PersistentVoiceEpochStore(string path) : this(path, new SystemFiles()) { }
        public PersistentVoiceEpochStore(string path, IVoiceEpochFiles files)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.files = files ?? throw new ArgumentNullException(nameof(files));
        }

        public VoiceEpochStoreRead Read()
        {
            FileReadResult read;
            try { read = files.ReadCanonical(path); }
            catch { return VoiceEpochStoreRead.Invalid(); }
            if (read == null || read.State == FileReadState.Failure) return VoiceEpochStoreRead.Invalid();
            if (read.State == FileReadState.NotFound)
            {
                Observe(read);
                return VoiceEpochStoreRead.Missing();
            }
            if (!TryDecode(read.Bytes, out var manifest)) return VoiceEpochStoreRead.Invalid();
            Observe(read);
            return VoiceEpochStoreRead.Valid(manifest);
        }

        public bool TryBeginDispatch(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return false;
            var read = Read();
            if (!read.Readable || !read.Exists || read.Manifest.PendingOperations.Length != 0) return false;
            var pending = read.Manifest.Clone();
            pending.PendingOperations = new[] { operation };
            return TryCommit(pending);
        }

        public bool TryCompleteDispatch(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return false;
            var read = Read();
            if (!read.Readable || !read.Exists || read.Manifest.PendingOperations.Length != 1 ||
                read.Manifest.PendingOperations[0] != operation) return false;
            var settled = read.Manifest.Clone();
            settled.PendingOperations = Array.Empty<string>();
            return TryCommit(settled);
        }

        public bool TryCommit(VoiceEpochManifest manifest)
        {
            if (!IsPersistable(manifest)) return false;
            var json = JsonUtility.ToJson(manifest);
            if (!TryDecode(Utf8.GetBytes(json), out var roundTrip) || !IsExact(roundTrip, manifest)) return false;
            var expected = Utf8.GetBytes(json);
            var temporary = path + ".tmp";
            FileReadResult initial;
            try { initial = files.ReadCanonical(path); }
            catch { return false; }
            if (initial == null || initial.State == FileReadState.Failure ||
                initial.State == FileReadState.Success && !TryDecode(initial.Bytes, out _)) return false;
            if (hasObservation && !SameObservation(observedCanonical, initial)) return false;

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) files.CreateDirectory(directory);
                using (var writer = files.OpenTemporary(temporary))
                {
                    writer.Write(json);
                    writer.FlushWriter();
                    writer.FlushDurable();
                }

                var beforeCommit = files.ReadCanonical(path);
                if (beforeCommit == null || beforeCommit.State == FileReadState.Failure) return false;
                if (initial.State == FileReadState.NotFound)
                {
                    if (beforeCommit.State == FileReadState.Success)
                        return CanonicalEquals(beforeCommit, expected);
                    files.MoveTemporary(temporary, path);
                }
                else
                {
                    if (beforeCommit.State != FileReadState.Success || !BytesEqual(initial.Bytes, beforeCommit.Bytes)) return false;
                    files.ReplaceTemporary(temporary, path, path + ".bak");
                }
            }
            catch { }
            finally { try { files.DeleteTemporary(temporary); } catch { } }

            try
            {
                var committed = files.ReadCanonical(path);
                if (!CanonicalEquals(committed, expected)) return false;
                Observe(committed);
                return true;
            }
            catch { return false; }
        }

        private void Observe(FileReadResult read)
        {
            hasObservation = true;
            observedCanonical = read.State == FileReadState.Success ? FileReadResult.Success(read.Bytes) : FileReadResult.NotFound();
        }

        private static bool SameObservation(FileReadResult left, FileReadResult right)
        {
            return left != null && right != null && left.State == right.State &&
                (left.State != FileReadState.Success || BytesEqual(left.Bytes, right.Bytes));
        }

        private static bool CanonicalEquals(FileReadResult read, byte[] expected)
        {
            return read != null && read.State == FileReadState.Success && BytesEqual(read.Bytes, expected) &&
                TryDecode(read.Bytes, out var manifest) && IsPersistable(manifest);
        }

        private static bool TryDecode(byte[] bytes, out VoiceEpochManifest manifest)
        {
            manifest = null;
            if (bytes == null) return false;
            try
            {
                var json = Utf8.GetString(bytes);
                if (!HasAllFields(json)) return false;
                manifest = JsonUtility.FromJson<VoiceEpochManifest>(json);
            }
            catch { return false; }
            return IsPersistable(manifest);
        }

        private static bool HasAllFields(string json)
        {
            var first = new VoiceEpochManifest
            {
                Schema = int.MinValue, WorldId = "missing-world-a", ShiftId = "missing-shift-a",
                Epoch = "missing-epoch-a", Rooms = new[] { "missing-rooms-a" },
                PendingOperations = new[] { "missing-operation-a" }, Ready = false
            };
            var second = new VoiceEpochManifest
            {
                Schema = int.MaxValue, WorldId = "missing-world-b", ShiftId = "missing-shift-b",
                Epoch = "missing-epoch-b", Rooms = new[] { "missing-rooms-b" },
                PendingOperations = new[] { "missing-operation-b" }, Ready = true
            };
            JsonUtility.FromJsonOverwrite(json, first);
            JsonUtility.FromJsonOverwrite(json, second);
            return first.Schema == second.Schema && first.WorldId == second.WorldId && first.ShiftId == second.ShiftId &&
                first.Epoch == second.Epoch && first.Ready == second.Ready && first.Rooms != null && second.Rooms != null &&
                first.Rooms.SequenceEqual(second.Rooms, StringComparer.Ordinal) && first.PendingOperations != null &&
                second.PendingOperations != null && first.PendingOperations.SequenceEqual(second.PendingOperations, StringComparer.Ordinal);
        }

        private static bool IsPersistable(VoiceEpochManifest manifest)
        {
            return manifest != null && manifest.Schema == VoiceEpochManifest.CurrentSchema &&
                !string.IsNullOrEmpty(manifest.WorldId) && !string.IsNullOrEmpty(manifest.ShiftId) &&
                Guid.TryParseExact(manifest.Epoch, "N", out _) && manifest.Rooms != null && manifest.Rooms.Length != 0 &&
                manifest.Rooms.All(room => !string.IsNullOrEmpty(room)) &&
                manifest.Rooms.Distinct(StringComparer.Ordinal).Count() == manifest.Rooms.Length &&
                manifest.PendingOperations != null &&
                manifest.PendingOperations.All(operation => !string.IsNullOrEmpty(operation)) &&
                manifest.PendingOperations.Distinct(StringComparer.Ordinal).Count() == manifest.PendingOperations.Length;
        }

        private static bool IsExact(VoiceEpochManifest left, VoiceEpochManifest right)
        {
            return left != null && right != null && left.Schema == right.Schema && left.WorldId == right.WorldId &&
                left.ShiftId == right.ShiftId && left.Epoch == right.Epoch && left.Ready == right.Ready &&
                left.Rooms != null && right.Rooms != null && left.Rooms.SequenceEqual(right.Rooms, StringComparer.Ordinal) &&
                left.PendingOperations != null && right.PendingOperations != null &&
                left.PendingOperations.SequenceEqual(right.PendingOperations, StringComparer.Ordinal);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            return left != null && right != null && left.SequenceEqual(right);
        }
    }

    public sealed class ServerVoiceService : MonoBehaviour
    {
        [Serializable] private sealed class CreateRoom { public string name; public int max_participants = 20; public int empty_timeout = 300; }
        [Serializable] private sealed class DeleteRoom { public string room; }
        [Serializable] private sealed class RemoveParticipant { public string room, identity; }

        private enum OperationKind { Provision, Retirement }

        private sealed class OperationLease
        {
            public OperationLease(OperationKind kind, object owner, int generation)
            {
                Kind = kind; Owner = owner; Generation = generation; AcceptingDispatch = true;
            }
            public OperationKind Kind { get; private set; }
            public object Owner { get; private set; }
            public int Generation { get; private set; }
            public bool AcceptingDispatch { get; set; }
            public int PendingCalls { get; set; }
        }

        private sealed class RemoteCallLease
        {
            public OperationLease Operation;
            public bool DispatchStarted;
            public bool CallbackReceived;
            public bool IterationCompleted;
            public int ActiveGuards;
        }

        private VoiceServiceConfig config;
        private WorldState world;
        private VoiceTokenIssuer issuer;
        private VoiceEpochCoordinator coordinator;
        private VoiceEpochManifest roomState;
        private string stateFile;
        private IVoiceEpochStore epochStore;
        private IVoiceServiceTransport transport;
        private Func<string> epochFactory;
        private int requestedGeneration, completedGeneration, provisionOperationGeneration, retirementOperationGeneration;
        private object provisionOwner, retirementOwner;
        private OperationLease provisionLease, retirementLease;
        private bool ready, provisioning, rotationRequested, retiring, retirementRunning, configured;
        private bool initialAuthorityKnownEmpty = true, authorityEverValidated, authorityUncertain, failedProvision;
        private bool initialReadRetryAllowed;
        private readonly HashSet<string> revocations = new HashSet<string>();
        public bool Ready => ready && roomState != null && roomState.Ready && completedGeneration == requestedGeneration &&
            !provisioning && !rotationRequested && !retiring && revocations.Count == 0;
        public bool RetiredSuccessfully { get; private set; }
        public string Status { get; private set; } = "음성 준비 중";

        public VoiceEpochManifest DurableManifest
        {
            get
            {
                var read = SafeRead();
                return read.Readable && read.Exists && IsOwned(read.Manifest) ? read.Manifest.Clone() : null;
            }
        }

        public void Configure(VoiceServiceConfig serviceConfig, WorldState initialWorld, string recordsDirectory)
        {
            EnsureConfigurationMayStart();
            var configuredStateFile = Path.Combine(recordsDirectory, "voice-rooms-v1.json");
            ConfigureCore(serviceConfig, initialWorld, new PersistentVoiceEpochStore(configuredStateFile), null,
                () => Guid.NewGuid().ToString("N"), () => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            stateFile = configuredStateFile;
            StartCoroutine(ProvisionLoop());
        }

        public void ConfigureForTests(VoiceServiceConfig serviceConfig, WorldState initialWorld,
            IVoiceEpochStore store, IVoiceServiceTransport serviceTransport, Func<string> nextEpoch, Func<long> clock)
        {
            EnsureConfigurationMayStart();
            ConfigureCore(serviceConfig, initialWorld, store, serviceTransport, nextEpoch, clock);
        }

        private void EnsureConfigurationMayStart()
        {
            if (configured || provisioning || retiring || retirementRunning)
                throw new InvalidOperationException("Voice service configuration already owns this lifecycle.");
        }

        private void ConfigureCore(VoiceServiceConfig serviceConfig, WorldState initialWorld,
            IVoiceEpochStore store, IVoiceServiceTransport serviceTransport, Func<string> nextEpoch, Func<long> clock)
        {
            if (serviceConfig == null) throw new ArgumentNullException(nameof(serviceConfig));
            RequireEndpoint(serviceConfig.HttpUrl, "http", "https");
            RequireEndpoint(serviceConfig.WebSocketUrl, "ws", "wss");
            config = serviceConfig; world = initialWorld == null ? throw new ArgumentNullException(nameof(initialWorld)) : initialWorld.Copy();
            issuer = new VoiceTokenIssuer(config.ApiKey, config.ApiSecret, clock ?? throw new ArgumentNullException(nameof(clock)));
            epochStore = store ?? throw new ArgumentNullException(nameof(store));
            transport = serviceTransport;
            epochFactory = nextEpoch ?? throw new ArgumentNullException(nameof(nextEpoch));
            roomState = null; ready = false; RetiredSuccessfully = false; Status = "음성 준비 중";
            configured = true; initialAuthorityKnownEmpty = true; authorityEverValidated = false; authorityUncertain = false;
            failedProvision = false; provisionOwner = null; provisionOperationGeneration = 0; provisionLease = null;
            retirementOwner = null; retirementOperationGeneration = 0; retirementLease = null;
        }

        /// <summary>
        /// Explicit recovery entrypoint used by the server after a failed provision. Call only while no provision
        /// coroutine is running. It reconstructs ownership from the canonical store and keeps grants closed on failure.
        /// </summary>
        public IEnumerator RetryFailedProvision()
        {
            if (provisioning || retiring || HasPendingRemoteCalls) yield break;
            yield return ProvisionLoop();
        }

        public void RevokeConnection(string participantId)
        {
            if (retiring || !world.Participants.Any(p => p.ParticipantId == participantId)) return;
            ready = false; rotationRequested = true; requestedGeneration++; revocations.Add(participantId);
            if (!provisioning && !failedProvision) StartCoroutine(ProvisionLoop());
        }

        private IEnumerator ProvisionLoop()
        {
            var owner = new object();
            if (provisionOwner != null || provisioning || retiring || retirementRunning || HasPendingRemoteCalls) yield break;
            provisionOwner = owner;
            provisioning = true;
            var operationGeneration = ++provisionOperationGeneration;
            var lease = new OperationLease(OperationKind.Provision, owner, operationGeneration);
            provisionLease = lease;
            try
            {
                do
                {
                    if (!OwnsProvision(owner, operationGeneration)) yield break;
                    rotationRequested = false;
                    if (authorityUncertain)
                    {
                        if (!initialReadRetryAllowed)
                        {
                            roomState = null; ready = false; Status = "음성 복구 기록 확인 필요"; yield break;
                        }
                        var read = SafeRead();
                        if (!OwnsProvision(owner, operationGeneration)) yield break;
                        if (!read.Readable || read.Exists && !IsOwned(read.Manifest))
                        {
                            if (read.Readable) initialReadRetryAllowed = false;
                            roomState = null; ready = false; Status = "음성 복구 기록 확인 필요"; yield break;
                        }
                        authorityUncertain = false;
                        initialReadRetryAllowed = false;
                        initialAuthorityKnownEmpty = !read.Exists;
                        authorityEverValidated = read.Exists;
                    }
                    if (revocations.Count != 0)
                    {
                        var read = SafeRead();
                        if (!OwnsProvision(owner, operationGeneration)) yield break;
                        if (!read.Readable || read.Exists && !IsOwned(read.Manifest))
                        {
                            authorityUncertain = true;
                            roomState = null; ready = false; Status = "음성 복구 기록 확인 필요"; yield break;
                        }
                        if (!read.Exists)
                        {
                            if (!initialAuthorityKnownEmpty || authorityEverValidated || authorityUncertain)
                            {
                                roomState = null; ready = false; Status = "음성 복구 기록 확인 필요"; yield break;
                            }
                            revocations.Clear();
                        }
                        else
                        {
                            authorityEverValidated = true;
                            initialAuthorityKnownEmpty = false;
                            roomState = read.Manifest.Clone();
                            foreach (var id in revocations.ToArray())
                            {
                                var participant = world.Participants.Single(p => p.ParticipantId == id);
                                foreach (var room in new[] { VoiceTokenIssuer.Room(world.WorldId, world.ShiftId, roomState.Epoch, "team." + participant.TeamId),
                                    VoiceTokenIssuer.Room(world.WorldId, world.ShiftId, roomState.Epoch, "command") }.Distinct())
                                {
                                    if (!OwnsProvision(owner, operationGeneration)) yield break;
                                    var ok = false;
                                    yield return Call(lease, "RemoveParticipant", JsonUtility.ToJson(new RemoveParticipant { room = room, identity = id }), value => ok = value, room);
                                    if (!OwnsProvision(owner, operationGeneration)) yield break;
                                    if (!ok) { ready = false; Status = "음성 권한 철회 실패"; yield break; }
                                }
                                revocations.Remove(id);
                            }
                        }
                    }
                    if (!retiring)
                    {
                        var generation = requestedGeneration;
                        yield return Provision(generation, owner, operationGeneration, lease);
                    }
                } while (rotationRequested && !retiring && OwnsProvision(owner, operationGeneration));
            }
            finally
            {
                lease.AcceptingDispatch = false;
                if (ReferenceEquals(provisionOwner, owner) && provisionOperationGeneration == operationGeneration)
                {
                    provisionOwner = null;
                    provisioning = false;
                }
                if (ReferenceEquals(provisionLease, lease) && lease.PendingCalls == 0) provisionLease = null;
            }
        }

        private IEnumerator Provision(int generation, object owner, int operationGeneration, OperationLease lease)
        {
            if (!OwnsProvision(owner, operationGeneration)) yield break;
            ready = false;
            var provisionCoordinator = new VoiceEpochCoordinator(world.WorldId, world.ShiftId, ExpectedRooms, epochStore);
            coordinator = provisionCoordinator;
            if (!provisionCoordinator.StartProvision(epochFactory()))
            {
                if (!OwnsProvision(owner, operationGeneration)) yield break;
                initialReadRetryAllowed = provisionCoordinator.InitialReadFailed &&
                    initialAuthorityKnownEmpty && !authorityEverValidated && !authorityUncertain;
                ObserveCoordinatorAuthority(provisionCoordinator);
                failedProvision = true;
                Status = provisionCoordinator.Failure;
                roomState = ValidatedDurableManifest();
                yield break;
            }
            if (!OwnsProvision(owner, operationGeneration)) yield break;
            if (provisionCoordinator.InitialAuthorityMissing && (!initialAuthorityKnownEmpty || authorityEverValidated))
            {
                authorityUncertain = true;
                initialAuthorityKnownEmpty = false;
                failedProvision = true;
                roomState = null; ready = false; Status = "음성 복구 기록 확인 필요";
                yield break;
            }
            ObserveCoordinatorAuthority(provisionCoordinator);
            roomState = provisionCoordinator.Manifest;

            VoiceEpochAction action;
            while (provisionCoordinator.TryGetAction(out action))
            {
                if (!OwnsProvision(owner, operationGeneration)) yield break;
                var success = true;
                if (action.Kind == VoiceEpochActionKind.CreateRoom)
                {
                    initialAuthorityKnownEmpty = false;
                    var ok = false;
                    yield return Call(lease, "CreateRoom", JsonUtility.ToJson(new CreateRoom { name = action.Room }), value => ok = value);
                    success = ok;
                }
                else if (action.Kind == VoiceEpochActionKind.DeleteRoom)
                {
                    var ok = false;
                    yield return Call(lease, "DeleteRoom", JsonUtility.ToJson(new DeleteRoom { room = action.Room }), value => ok = value);
                    success = ok;
                }
                if (!OwnsProvision(owner, operationGeneration)) yield break;
                if (!provisionCoordinator.Complete(action, success))
                {
                    ObserveCoordinatorAuthority(provisionCoordinator);
                    failedProvision = true;
                    Status = provisionCoordinator.Failure;
                    roomState = ValidatedDurableManifest();
                    if (roomState != null) authorityEverValidated = true;
                    yield break;
                }
                roomState = provisionCoordinator.Manifest;
                if (action.Kind == VoiceEpochActionKind.CommitIntent || action.Kind == VoiceEpochActionKind.CommitReady)
                {
                    authorityEverValidated = true;
                    initialAuthorityKnownEmpty = false;
                }
            }

            if (!OwnsProvision(owner, operationGeneration)) yield break;
            completedGeneration = generation;
            failedProvision = false;
            ready = provisionCoordinator.Ready && generation == requestedGeneration && revocations.Count == 0 && !rotationRequested && !retiring;
            Status = ready ? "음성 채널 준비됨" : "음성 권한 회전 대기";
            if (ready) Debug.Log("Voice rooms provisioned with a new authorization epoch.");
        }

        private bool OwnsProvision(object owner, int operationGeneration)
        {
            return ReferenceEquals(provisionOwner, owner) && provisionOperationGeneration == operationGeneration &&
                provisioning && !retiring && !retirementRunning;
        }

        private bool HasPendingRemoteCalls => provisionLease != null && provisionLease.PendingCalls != 0 ||
            retirementLease != null && retirementLease.PendingCalls != 0;

        private bool LeaseMayDispatch(OperationLease lease)
        {
            if (lease == null || !lease.AcceptingDispatch) return false;
            if (lease.Kind == OperationKind.Provision)
                return ReferenceEquals(provisionLease, lease) && OwnsProvision(lease.Owner, lease.Generation);
            return ReferenceEquals(retirementLease, lease) && ReferenceEquals(retirementOwner, lease.Owner) &&
                retirementOperationGeneration == lease.Generation && retirementRunning && retiring;
        }

        private IEnumerator Call(OperationLease operation, string method, string json, Action<bool> complete,
            string administrationRoom = null)
        {
            if (!LeaseMayDispatch(operation)) yield break;
            var dispatchStore = epochStore as IDurableVoiceDispatchStore;
            var dispatchId = method + ":" + Guid.NewGuid().ToString("N");
            if (dispatchStore != null && !dispatchStore.TryBeginDispatch(dispatchId))
            {
                MarkAuthorityUncertain("음성 원격 작업 기록 저장 확인 필요");
                yield break;
            }
            var call = new RemoteCallLease { Operation = operation, ActiveGuards = 1 };
            operation.PendingCalls++;
            try
            {
                if (!LeaseMayDispatch(operation)) yield break;
                if (transport != null)
                {
                    if (!LeaseMayDispatch(operation)) yield break;
                    var request = transport.Call(method, json, administrationRoom, value =>
                    {
                        call.CallbackReceived = true;
                        if (operation.AcceptingDispatch) complete(value);
                        else if (call.DispatchStarted) ReconcileCompletedDetachedCall(operation);
                        ReleaseCallGuard(call);
                    });
                    if (request == null) yield break;
                    try
                    {
                        while (true)
                        {
                            if (!LeaseMayAdvance(call)) yield break;
                            call.DispatchStarted = true;
                            if (!request.MoveNext()) { call.IterationCompleted = true; break; }
                            var nested = request.Current as IEnumerator;
                            if (nested == null)
                            {
                                yield return request.Current;
                                continue;
                            }
                            call.ActiveGuards++;
                            try
                            {
                                var nestedStack = new Stack<IEnumerator>();
                                nestedStack.Push(nested);
                                try
                                {
                                    while (nestedStack.Count != 0)
                                    {
                                        if (!LeaseMayAdvance(call)) yield break;
                                        var current = nestedStack.Peek();
                                        if (!current.MoveNext())
                                        {
                                            (current as IDisposable)?.Dispose();
                                            nestedStack.Pop();
                                            continue;
                                        }
                                        var child = current.Current as IEnumerator;
                                        if (child != null) nestedStack.Push(child);
                                        else yield return current.Current;
                                    }
                                }
                                finally
                                {
                                    while (nestedStack.Count != 0)
                                        (nestedStack.Pop() as IDisposable)?.Dispose();
                                }
                            }
                            finally { ReleaseCallGuard(call); }
                        }
                    }
                    finally { (request as IDisposable)?.Dispose(); }
                    yield break;
                }
                using (var request = new UnityWebRequest(config.HttpUrl.TrimEnd('/') + "/twirp/livekit.RoomService/" + method, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 15;
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", "Bearer " + (administrationRoom == null ? issuer.RoomAdministrationToken() : issuer.ParticipantAdministrationToken(administrationRoom)));
                    if (!LeaseMayDispatch(operation)) yield break;
                    call.DispatchStarted = true;
                    yield return request.SendWebRequest();
                    call.IterationCompleted = true;
                    var ok = IsSuccessfulResponse(method, request.result, request.responseCode);
                    if (!ok) Debug.LogWarning("Voice room service operation failed: " + method + " / HTTP " + request.responseCode);
                    call.CallbackReceived = true;
                    if (operation.AcceptingDispatch) complete(ok);
                    else ReconcileCompletedDetachedCall(operation);
                    ReleaseCallGuard(call);
                }
            }
            finally
            {
                var settled = !call.DispatchStarted || call.CallbackReceived;
                if (dispatchStore != null && settled && !dispatchStore.TryCompleteDispatch(dispatchId))
                    MarkAuthorityUncertain("음성 원격 작업 완료 기록 확인 필요");
                if (call.DispatchStarted && !call.IterationCompleted && !call.CallbackReceived)
                {
                    MarkAuthorityUncertain("음성 원격 작업 완료 확인 필요");
                    ReleaseCallGuard(call);
                }
                else if (settled) ReleaseCallGuard(call);
            }
        }

        private bool LeaseMayAdvance(RemoteCallLease call)
        {
            return call != null && call.ActiveGuards != 0 &&
                (LeaseMayDispatch(call.Operation) || call.DispatchStarted && !call.CallbackReceived ||
                    !call.DispatchStarted && call.ActiveGuards > 1);
        }

        private void ReleaseCallGuard(RemoteCallLease call)
        {
            if (call == null || call.ActiveGuards == 0) return;
            call.ActiveGuards--;
            if (call.ActiveGuards != 0) return;
            var operation = call.Operation;
            operation.PendingCalls--;
            if (!operation.AcceptingDispatch && operation.PendingCalls == 0)
            {
                if (ReferenceEquals(provisionLease, operation)) provisionLease = null;
                if (ReferenceEquals(retirementLease, operation)) retirementLease = null;
            }
        }

        public static bool IsSuccessfulResponse(string method, UnityWebRequest.Result result, long responseCode)
        {
            return result == UnityWebRequest.Result.Success || result == UnityWebRequest.Result.ProtocolError &&
                (method == "DeleteRoom" || method == "RemoveParticipant") && responseCode == 404;
        }

        public VoiceGrant Grant(ParticipantState participant, string channel)
        {
            if (!AuthoritativelyReady) return null;
            return issuer.Join(world.WorldId, world.ShiftId, roomState.Epoch, participant, channel, config.WebSocketUrl);
        }

        private bool AuthoritativelyReady => Ready;

        public IEnumerator RetireRooms()
        {
            if (retirementRunning) yield break;
            var owner = new object();
            var operationGeneration = ++retirementOperationGeneration;
            var lease = new OperationLease(OperationKind.Retirement, owner, operationGeneration);
            retirementOwner = owner;
            retirementLease = lease;
            retirementRunning = true; retiring = true; ready = false; RetiredSuccessfully = false;
            try
            {
                while (provisioning || provisionLease != null && provisionLease.PendingCalls != 0) yield return null;
                var retirementCoordinator = new VoiceEpochCoordinator(world.WorldId, world.ShiftId, ExpectedRooms, epochStore);
                coordinator = retirementCoordinator;
                if (!retirementCoordinator.StartRetire())
                {
                    ObserveCoordinatorAuthority(retirementCoordinator);
                    Status = retirementCoordinator.Failure;
                    roomState = ValidatedDurableManifest();
                    yield break;
                }
                if (retirementCoordinator.InitialAuthorityMissing &&
                    (!initialAuthorityKnownEmpty || authorityEverValidated || authorityUncertain))
                {
                    authorityUncertain = true;
                    initialAuthorityKnownEmpty = false;
                    roomState = null;
                    Status = "음성 복구 기록 확인 필요";
                    yield break;
                }
                ObserveCoordinatorAuthority(retirementCoordinator);
                roomState = retirementCoordinator.Manifest;

                VoiceEpochAction action;
                while (retirementCoordinator.TryGetAction(out action))
                {
                    var ok = false;
                    yield return Call(lease, "DeleteRoom", JsonUtility.ToJson(new DeleteRoom { room = action.Room }), value => ok = value);
                    if (!LeaseMayDispatch(lease)) yield break;
                    if (!retirementCoordinator.Complete(action, ok))
                    {
                        Status = retirementCoordinator.Failure;
                        roomState = ValidatedDurableManifest();
                        yield break;
                    }
                    roomState = retirementCoordinator.Manifest;
                }
                RetiredSuccessfully = true;
                Status = "음성 채널 종료됨";
            }
            finally
            {
                lease.AcceptingDispatch = false;
                if (ReferenceEquals(retirementOwner, owner) && retirementOperationGeneration == operationGeneration)
                {
                    retirementOwner = null;
                    retirementRunning = false;
                }
                if (ReferenceEquals(retirementLease, lease) && lease.PendingCalls == 0) retirementLease = null;
            }
        }

        private void ReconcileCompletedDetachedCall(OperationLease operation)
        {
            var read = SafeRead();
            if (!read.Readable || read.Exists && !IsOwned(read.Manifest))
            {
                MarkAuthorityUncertain("음성 원격 작업 완료 후 복구 기록 확인 필요");
                roomState = null;
                return;
            }
            roomState = read.Exists ? read.Manifest.Clone() : null;
            ready = false;
            failedProvision = operation.Kind == OperationKind.Provision;
            Status = read.Exists ? "음성 원격 작업 완료 후 복구 대기" : "음성 원격 작업 완료 후 소유 기록 없음";
        }

        private void MarkAuthorityUncertain(string status)
        {
            initialReadRetryAllowed = false;
            authorityUncertain = true;
            initialAuthorityKnownEmpty = false;
            ready = false;
            Status = status;
        }

        private void ObserveCoordinatorAuthority(VoiceEpochCoordinator owner)
        {
            if (owner.InitialAuthorityValidated)
            {
                authorityEverValidated = true;
                initialAuthorityKnownEmpty = false;
            }
            if (owner.AuthorityUncertain)
            {
                authorityUncertain = true;
                initialAuthorityKnownEmpty = false;
            }
        }

        private VoiceEpochStoreRead SafeRead()
        {
            try { return epochStore == null ? VoiceEpochStoreRead.Invalid() : epochStore.Read(); }
            catch { return VoiceEpochStoreRead.Invalid(); }
        }

        private VoiceEpochManifest ValidatedDurableManifest()
        {
            var read = SafeRead();
            return read.Readable && read.Exists && IsOwned(read.Manifest) ? read.Manifest.Clone() : null;
        }

        private bool IsOwned(VoiceEpochManifest manifest)
        {
            if (manifest == null || manifest.Schema != VoiceEpochManifest.CurrentSchema ||
                manifest.WorldId != world.WorldId || manifest.ShiftId != world.ShiftId ||
                !Guid.TryParseExact(manifest.Epoch, "N", out _) || manifest.Rooms == null || manifest.Rooms.Length == 0 ||
                manifest.Rooms.Any(string.IsNullOrEmpty) || manifest.Rooms.Distinct(StringComparer.Ordinal).Count() != manifest.Rooms.Length ||
                manifest.PendingOperations == null || manifest.PendingOperations.Any(string.IsNullOrEmpty) ||
                manifest.PendingOperations.Distinct(StringComparer.Ordinal).Count() != manifest.PendingOperations.Length)
                return false;
            var expected = ExpectedRooms(manifest.Epoch).OrderBy(value => value, StringComparer.Ordinal);
            return expected.SequenceEqual(manifest.Rooms.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
        }

        private string[] ExpectedRooms(string epoch) => world.Participants.Select(p =>
            VoiceTokenIssuer.Room(world.WorldId, world.ShiftId, epoch, "team." + p.TeamId))
            .Concat(new[] { VoiceTokenIssuer.Room(world.WorldId, world.ShiftId, epoch, "command") }).Distinct().ToArray();

        private static void RequireEndpoint(string value, string localScheme, string secureScheme)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !(uri.Scheme == secureScheme || uri.Scheme == localScheme && uri.IsLoopback))
                throw new ArgumentException("Voice endpoints require TLS outside loopback.");
        }
    }
}
