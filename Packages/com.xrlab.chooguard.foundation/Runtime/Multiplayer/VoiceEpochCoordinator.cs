using System;
using System.Collections.Generic;
using System.Linq;

namespace ChooGuard.Foundation.Multiplayer
{
    public enum VoiceEpochActionKind
    {
        DeleteRoom,
        CreateRoom,
        CommitIntent,
        CommitReady
    }

    [Serializable]
    public sealed class VoiceEpochManifest
    {
        public const int CurrentSchema = 3;
        public int Schema = CurrentSchema;
        public string WorldId;
        public string ShiftId;
        public string Epoch;
        public string[] Rooms;
        public string[] PendingOperations;
        public bool Ready;

        public VoiceEpochManifest Clone()
        {
            return new VoiceEpochManifest
            {
                Schema = Schema,
                WorldId = WorldId,
                ShiftId = ShiftId,
                Epoch = Epoch,
                Rooms = Rooms == null ? null : Rooms.ToArray(),
                PendingOperations = PendingOperations == null ? null : PendingOperations.ToArray(),
                Ready = Ready
            };
        }
    }

    public sealed class VoiceEpochAction
    {
        internal VoiceEpochAction(VoiceEpochActionKind kind, string room, VoiceEpochManifest manifest)
        {
            Kind = kind;
            Room = room;
            Manifest = manifest == null ? null : manifest.Clone();
        }

        public VoiceEpochActionKind Kind { get; private set; }
        public string Room { get; private set; }
        public VoiceEpochManifest Manifest { get; private set; }
    }

    public sealed class VoiceEpochStoreRead
    {
        private VoiceEpochStoreRead(bool exists, bool readable, VoiceEpochManifest manifest)
        {
            Exists = exists;
            Readable = readable;
            Manifest = manifest;
        }

        public bool Exists { get; private set; }
        public bool Readable { get; private set; }
        public VoiceEpochManifest Manifest { get; private set; }

        public static VoiceEpochStoreRead Missing() => new VoiceEpochStoreRead(false, true, null);
        public static VoiceEpochStoreRead Invalid() => new VoiceEpochStoreRead(true, false, null);
        public static VoiceEpochStoreRead Valid(VoiceEpochManifest manifest) => new VoiceEpochStoreRead(true, true, manifest);
    }

    public interface IVoiceEpochStore
    {
        VoiceEpochStoreRead Read();
        bool TryCommit(VoiceEpochManifest manifest);
    }

    public sealed class VoiceEpochCoordinator
    {
        private readonly string worldId;
        private readonly string shiftId;
        private readonly Func<string, string[]> expectedRooms;
        private readonly IVoiceEpochStore store;
        private readonly Queue<VoiceEpochAction> actions = new Queue<VoiceEpochAction>();
        private VoiceEpochManifest active;
        private VoiceEpochAction current;

        public VoiceEpochCoordinator(string worldId, string shiftId, Func<string, string[]> expectedRooms, IVoiceEpochStore store)
        {
            if (string.IsNullOrEmpty(worldId)) throw new ArgumentException("Voice world is required.");
            if (string.IsNullOrEmpty(shiftId)) throw new ArgumentException("Voice shift is required.");
            this.worldId = worldId;
            this.shiftId = shiftId;
            this.expectedRooms = expectedRooms ?? throw new ArgumentNullException(nameof(expectedRooms));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public string Failure { get; private set; }
        public VoiceEpochManifest Manifest => active == null ? null : active.Clone();
        public VoiceEpochManifest DurableManifest
        {
            get
            {
                var read = ReadStore();
                return read.Readable && read.Exists && IsValid(read.Manifest) ? read.Manifest.Clone() : null;
            }
        }
        public bool HasPendingAction => current != null || actions.Count != 0;
        public bool InitialReadFailed { get; private set; }
        public bool InitialAuthorityMissing { get; private set; }
        public bool InitialAuthorityValidated { get; private set; }
        public bool AuthorityUncertain { get; private set; }

        public bool StartProvision(string epoch)
        {
            Reset();
            var read = ReadStore();
            if (!read.Readable)
            {
                InitialReadFailed = true;
                AuthorityUncertain = true;
                return Fail("음성 복구 기록 확인 필요");
            }
            if (read.Exists && !IsValid(read.Manifest))
            {
                AuthorityUncertain = true;
                return Fail("음성 복구 기록 확인 필요");
            }

            InitialAuthorityMissing = !read.Exists;
            InitialAuthorityValidated = read.Exists;
            active = read.Manifest == null ? null : read.Manifest.Clone();
            if (active != null && active.PendingOperations.Length != 0)
            {
                AuthorityUncertain = true;
                return Fail("음성 원격 작업 완료 확인 필요");
            }
            if (active != null)
                EnqueueDeletes(active.Rooms);

            var rawExpectedRooms = expectedRooms(epoch);
            if (!HasValidRawRooms(rawExpectedRooms))
                return Fail("음성 방 구성 확인 필요");

            var candidate = new VoiceEpochManifest
            {
                WorldId = worldId,
                ShiftId = shiftId,
                Epoch = epoch,
                Rooms = OrderedRooms(rawExpectedRooms),
                PendingOperations = Array.Empty<string>(),
                Ready = false
            };
            if (!IsValid(candidate))
                return Fail("음성 방 구성 확인 필요");
            Enqueue(new VoiceEpochAction(VoiceEpochActionKind.CommitIntent, null, candidate));
            foreach (var room in candidate.Rooms)
                Enqueue(new VoiceEpochAction(VoiceEpochActionKind.CreateRoom, room, candidate));
            Enqueue(new VoiceEpochAction(VoiceEpochActionKind.CommitReady, null,
                new VoiceEpochManifest
                {
                    WorldId = candidate.WorldId, ShiftId = candidate.ShiftId, Epoch = candidate.Epoch,
                    Rooms = candidate.Rooms.ToArray(), PendingOperations = Array.Empty<string>(), Ready = true
                }));
            active = candidate;
            Ready = false;
            return true;
        }

        public bool StartRetire()
        {
            Reset();
            var read = ReadStore();
            if (!read.Readable || read.Exists && !IsValid(read.Manifest))
            {
                AuthorityUncertain = true;
                return Fail("음성 복구 기록 확인 필요");
            }
            InitialAuthorityMissing = !read.Exists;
            InitialAuthorityValidated = read.Exists;
            if (!read.Exists)
            {
                Ready = true;
                return true;
            }
            active = read.Manifest.Clone();
            if (active.PendingOperations.Length != 0)
            {
                AuthorityUncertain = true;
                return Fail("음성 원격 작업 완료 확인 필요");
            }
            EnqueueDeletes(active.Rooms);
            return true;
        }

        public bool TryGetAction(out VoiceEpochAction action)
        {
            if (current == null && actions.Count != 0)
                current = actions.Dequeue();
            action = current;
            return action != null;
        }

        public bool Complete(VoiceEpochAction action, bool success)
        {
            if (action == null || current != action)
                throw new InvalidOperationException("Voice epoch action completion is out of order.");
            if (!success)
                return Fail(ActionFailure(action));

            if (action.Kind == VoiceEpochActionKind.CommitIntent || action.Kind == VoiceEpochActionKind.CommitReady)
            {
                if (!CommitAndReRead(action.Manifest))
                    return Fail("음성 복구 기록 저장 확인 필요");
                active = action.Manifest.Clone();
                Ready = action.Kind == VoiceEpochActionKind.CommitReady;
            }
            current = null;
            if (actions.Count == 0 && active != null && active.Ready)
                Ready = true;
            return true;
        }

        private bool CommitAndReRead(VoiceEpochManifest manifest)
        {
            var acknowledged = false;
            var commitThrew = false;
            try { acknowledged = store.TryCommit(manifest); }
            catch { commitThrew = true; }
            var reread = ReadStore();
            if (reread.Readable && reread.Exists && IsValid(reread.Manifest) && IsSame(reread.Manifest, manifest))
                return true;
            if (!acknowledged && !commitThrew && InitialAuthorityMissing && reread.Readable && !reread.Exists)
                return false;
            AuthorityUncertain = true;
            return false;
        }

        private VoiceEpochStoreRead ReadStore()
        {
            try { return store.Read() ?? VoiceEpochStoreRead.Invalid(); }
            catch { return VoiceEpochStoreRead.Invalid(); }
        }

        private bool IsValid(VoiceEpochManifest manifest)
        {
            if (manifest == null || manifest.Schema != VoiceEpochManifest.CurrentSchema ||
                manifest.WorldId != worldId || manifest.ShiftId != shiftId ||
                !Guid.TryParseExact(manifest.Epoch, "N", out _)) return false;
            var rawExpectedRooms = expectedRooms(manifest.Epoch);
            if (!HasValidRawRooms(rawExpectedRooms)) return false;
            var expected = OrderedRooms(rawExpectedRooms);
            return manifest.Rooms != null && manifest.Rooms.All(room => !string.IsNullOrEmpty(room)) &&
                manifest.Rooms.Distinct(StringComparer.Ordinal).Count() == manifest.Rooms.Length &&
                manifest.PendingOperations != null &&
                manifest.PendingOperations.All(operation => !string.IsNullOrEmpty(operation)) &&
                manifest.PendingOperations.Distinct(StringComparer.Ordinal).Count() == manifest.PendingOperations.Length &&
                expected.SequenceEqual(OrderedRooms(manifest.Rooms), StringComparer.Ordinal);
        }

        private void EnqueueDeletes(IEnumerable<string> rooms)
        {
            foreach (var room in OrderedRooms(rooms))
                Enqueue(new VoiceEpochAction(VoiceEpochActionKind.DeleteRoom, room, active));
        }

        private void Enqueue(VoiceEpochAction action) => actions.Enqueue(action);

        private bool Fail(string failure)
        {
            Failed = true;
            Failure = failure;
            Ready = false;
            current = null;
            actions.Clear();
            return false;
        }

        private void Reset()
        {
            actions.Clear();
            current = null;
            active = null;
            Ready = false;
            Failed = false;
            Failure = null;
            InitialReadFailed = false;
            InitialAuthorityMissing = false;
            InitialAuthorityValidated = false;
            AuthorityUncertain = false;
        }

        private static string ActionFailure(VoiceEpochAction action)
        {
            switch (action.Kind)
            {
                case VoiceEpochActionKind.DeleteRoom: return "이전 음성 채널 정리 실패";
                case VoiceEpochActionKind.CreateRoom: return "음성 방 연결 실패";
                default: return "음성 복구 기록 저장 확인 필요";
            }
        }

        private static bool HasValidRawRooms(string[] rooms)
        {
            return rooms != null && rooms.Length != 0 &&
                rooms.All(room => !string.IsNullOrEmpty(room)) &&
                rooms.Distinct(StringComparer.Ordinal).Count() == rooms.Length;
        }

        private static string[] OrderedRooms(IEnumerable<string> rooms)
        {
            return (rooms ?? Array.Empty<string>()).Where(room => !string.IsNullOrEmpty(room))
                .Distinct(StringComparer.Ordinal).OrderBy(room => room, StringComparer.Ordinal).ToArray();
        }

        private static bool IsSame(VoiceEpochManifest left, VoiceEpochManifest right)
        {
            return left != null && right != null && left.Schema == right.Schema && left.WorldId == right.WorldId &&
                left.ShiftId == right.ShiftId && left.Epoch == right.Epoch && left.Ready == right.Ready &&
                left.Rooms != null && right.Rooms != null && left.Rooms.SequenceEqual(right.Rooms, StringComparer.Ordinal) &&
                left.PendingOperations != null && right.PendingOperations != null &&
                left.PendingOperations.SequenceEqual(right.PendingOperations, StringComparer.Ordinal);
        }
    }
}
