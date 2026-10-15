using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;

namespace ChooGuard.App.Mvp
{
    public sealed class MvpTeamView
    {
        public string Id { get; }
        public string Name { get; }
        public string AgencyId { get; }
        public string LocationId { get; }
        public string Status { get; }
        public MvpTeamView(string id, string name, string agencyId, string locationId, string status)
        { Id = id; Name = name; AgencyId = agencyId; LocationId = locationId; Status = status; }
    }

    /// <summary>Synthetic training only. Local MVP atomic snapshot; not the production K02 store or outbox.</summary>
    public sealed class MvpOperationsPort : IOperationsPort, IDisposable
    {
        public const string RunId = "mvp-synthetic-run";
        public const string RequesterId = "mvp-local-trainee";
        public static string[] TargetIds => new[] { "platform", "concourse", "exit" };
        readonly object gate = new object();
        readonly string path;
        readonly FileStream lease;
        bool disposed;
        long revision;
        List<MvpTeamView> teams = InitialTeams();
        Dictionary<ReceiptKey, CommandReceipt> receipts = new Dictionary<ReceiptKey, CommandReceipt>();
        public long Revision { get { lock (gate) { Alive(); return revision; } } }
        public IReadOnlyList<MvpTeamView> Teams { get { lock (gate) { Alive(); return teams.AsReadOnly(); } } }
        static StableId Id(string value) => new StableId(value);
        static List<MvpTeamView> InitialTeams() => new List<MvpTeamView> {
            new MvpTeamView("ops-1", "Operations", "operations", "concourse", "Ready"),
            new MvpTeamView("fire-1", "Fire", "fire", "concourse", "Ready"),
            new MvpTeamView("medical-1", "Medical", "medical", "concourse", "Ready") };
        public MvpOperationsPort(string storageDirectory)
        {
            if (string.IsNullOrWhiteSpace(storageDirectory)) throw new ArgumentException("훈련 저장 경로가 필요합니다.");
            Directory.CreateDirectory(storageDirectory);
            path = Path.Combine(storageDirectory, "synthetic-mvp.snapshot");
            lease = new FileStream(Path.Combine(storageDirectory, "synthetic-mvp.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try { if (File.Exists(path)) Load(); } catch { lease.Dispose(); throw; }
        }
        void Alive() { if (disposed) throw new ObjectDisposedException(nameof(MvpOperationsPort)); }
        public CommandIntent CreateIntent(string teamId, string actionId, string targetId)
        {
            lock (gate)
            {
                Alive();
                var team = teams.Find(t => t.Id == teamId);
                var payload = Payload(teamId, actionId, targetId, revision);
                return new CommandIntent(new ReceiptKey(Id(RunId), Id(RequesterId), Id(Guid.NewGuid().ToString("N"))),
                    Id(team == null ? "unknown" : team.AgencyId), new[] { Id(teamId) }, Id(actionId), new[] { Id(targetId) },
                    new[] { new EntityRevision(Id("mvp-state"), revision) }, payload, new UtcTimestamp(DateTimeOffset.UtcNow));
            }
        }
        static ContentReference Payload(string team, string action, string target, long rev) => Ref("mvp-payload", rev, Encode(w => { w.Write(team); w.Write(action); w.Write(target); w.Write(rev); }));
        string Validate(CommandIntent i)
        {
            if (i.Key.RunId.Value != RunId || i.Key.RequesterId.Value != RequesterId) return "훈련 세션 또는 요청자를 확인할 수 없습니다.";
            if (i.ActingTeamIds.Count != 1 || i.TargetIds.Count != 1) return "훈련팀과 목적지를 각각 하나씩 선택해 주세요.";
            var team = teams.Find(t => t.Id == i.ActingTeamIds[0].Value);
            if (team == null || team.AgencyId != i.ActingAgencyId.Value) return "훈련팀 정보를 확인할 수 없습니다.";
            if (i.ActionId.Value != "move" && i.ActionId.Value != "hold") return "지원하지 않는 명령입니다.";
            if (!TargetIds.Contains(i.TargetIds[0].Value)) return "훈련 목적지를 확인할 수 없습니다.";
            if (i.ReadSet.Count != 1 || i.ReadSet[0].EntityId.Value != "mvp-state" || i.ReadSet[0].Revision != revision) return "훈련 상태가 변경되었습니다. 명령을 다시 검토해 주세요.";
            var expected = Payload(team.Id, i.ActionId.Value, i.TargetIds[0].Value, revision);
            if (i.PayloadRef.Id.Value != expected.Id.Value || i.PayloadRef.Revision != expected.Revision || i.PayloadRef.Sha256 != expected.Sha256) return "검토한 명령과 요청 내용이 일치하지 않습니다.";
            return null;
        }
        static TargetResult[] Results(CommandIntent i, string error) => i.TargetIds.Select(t => new TargetResult(t,
            error == null ? TargetStatus.ACCEPTED : TargetStatus.REJECTED, error == null ? new Reason[0] : new[] {
                new Reason(Id("mvp-rejected"), ReasonAxis.evidence, t, new ContentReference[0], error) })).ToArray();
        public Task<PreviewResult> PreviewAsync(CommandIntent intent, CancellationToken cancellation)
        {
            lock (gate) { Alive(); cancellation.ThrowIfCancellationRequested(); if (intent == null) throw new ArgumentNullException(nameof(intent));
                var error = Validate(intent);
                return Task.FromResult(new PreviewResult(intent.Key, intent.ReadSet, Results(intent, error),
                    Ref("mvp-proposed-effects", revision, Encode(w => { w.Write(Fingerprint(intent)); w.Write(error ?? "synthetic-only"); })), checked(revision + 1))); }
        }
        public Task<CommandReceipt> SubmitAsync(CommandIntent intent, CancellationToken cancellation)
        {
            lock (gate)
            {
                Alive(); cancellation.ThrowIfCancellationRequested(); if (intent == null) throw new ArgumentNullException(nameof(intent));
                string fingerprint = Fingerprint(intent);
                if (receipts.TryGetValue(intent.Key, out var previous))
                    return Task.FromResult(previous.Fingerprint == fingerprint ? previous : new CommandReceipt(intent.Key, fingerprint,
                        ReceiptStatus.CONFLICT, null, null, Results(intent, "기존 처리결과와 다른 명령입니다. 원래 명령의 처리결과를 확인해 주세요.")));
                string error = Validate(intent);
                long nextRevision = error == null ? checked(revision + 1) : revision;
                var nextTeams = new List<MvpTeamView>(teams);
                if (error == null)
                {
                    int index = nextTeams.FindIndex(t => t.Id == intent.ActingTeamIds[0].Value);
                    var t = nextTeams[index];
                    nextTeams[index] = new MvpTeamView(t.Id, t.Name, t.AgencyId, intent.ActionId.Value == "move" ? intent.TargetIds[0].Value : t.LocationId,
                        intent.ActionId.Value == "move" ? "Moved" : "Holding");
                }
                var receipt = new CommandReceipt(intent.Key, fingerprint, error == null ? ReceiptStatus.ACCEPTED : ReceiptStatus.REJECTED,
                    error == null ? Guid.NewGuid().ToString("N") : null, error == null ? (Sequence?)new Sequence(nextRevision) : null, Results(intent, error));
                var nextReceipts = new Dictionary<ReceiptKey, CommandReceipt>(receipts) { [intent.Key] = receipt };
                Save(nextRevision, nextTeams, nextReceipts, cancellation);
                revision = nextRevision; teams = nextTeams; receipts = nextReceipts;
                return Task.FromResult(receipt);
            }
        }
        public Task<ReceiptLookup> ReadReceiptAsync(ReceiptKey key, CancellationToken cancellation)
        { lock (gate) { Alive(); cancellation.ThrowIfCancellationRequested(); receipts.TryGetValue(key, out var receipt); return Task.FromResult(new ReceiptLookup(receipt != null, receipt)); } }
        public Task<SessionProjection> ReadProjectionAsync(ProjectionQuery query, CancellationToken cancellation)
        {
            lock (gate) { Alive(); cancellation.ThrowIfCancellationRequested();
                if (query.RunId.Value != RunId || query.RequesterId.Value != RequesterId || query.ViewScope != ViewScope.AUTHOR_ANALYSIS || query.AgencyId.HasValue)
                    throw new UnauthorizedAccessException("이 기기의 가상 훈련 보기만 지원합니다.");
                var entityBytes = Encode(w => { foreach (var t in teams) { w.Write(t.Id); w.Write(t.LocationId); w.Write(t.Status); } });
                return Task.FromResult(new SessionProjection(Id(RunId), revision, new SimTick(0), query.ViewScope, null,
                    Ref("mvp-entities", revision, entityBytes), Ref("mvp-tasks", revision, new byte[0]), Ref("mvp-reasons", revision, new byte[0]))); }
        }
        static byte[] Encode(Action<BinaryWriter> action) { using (var m = new MemoryStream()) { using (var w = new BinaryWriter(m, Encoding.UTF8, true)) action(w); return m.ToArray(); } }
        static string Hash(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static ContentReference Ref(string id, long rev, byte[] bytes) => new ContentReference(Id(id), rev, Hash(bytes));
        static string Fingerprint(CommandIntent i) => Hash(Encode(w => {
            w.Write(i.Key.RunId.Value); w.Write(i.Key.RequesterId.Value); w.Write(i.Key.IntentId.Value); w.Write(i.ActingAgencyId.Value);
            w.Write(i.ActingTeamIds.Count); foreach (var t in i.ActingTeamIds) w.Write(t.Value);
            w.Write(i.ActionId.Value); w.Write(i.TargetIds.Count); foreach (var t in i.TargetIds) w.Write(t.Value);
            w.Write(i.ReadSet.Count); foreach (var r in i.ReadSet) { w.Write(r.EntityId.Value); w.Write(r.Revision); }
            w.Write(i.PayloadRef.Id.Value); w.Write(i.PayloadRef.Revision); w.Write(i.PayloadRef.Sha256);
        }));
        void Save(long rev, List<MvpTeamView> values, Dictionary<ReceiptKey, CommandReceipt> saved, CancellationToken cancellation)
        {
            byte[] body = Encode(w => {
                w.Write("CHOOGuard-local-MVP-v1"); w.Write(rev);
                foreach (var t in values) { w.Write(t.LocationId); w.Write(t.Status); }
                w.Write(saved.Count);
                foreach (var r in saved.Values) {
                    w.Write(r.Key.RunId.Value); w.Write(r.Key.RequesterId.Value); w.Write(r.Key.IntentId.Value); w.Write(r.Fingerprint);
                    w.Write((int)r.Status); w.Write(r.CommitId ?? ""); w.Write(r.Sequence.HasValue ? r.Sequence.Value.Value : -1);
                    w.Write(r.TargetResults.Count);
                    foreach (var t in r.TargetResults) { w.Write(t.TargetId.Value); w.Write((int)t.Status); w.Write(t.Reasons.Count == 0 ? "" : t.Reasons[0].Message); }
                }
            });
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (var f = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { using (var w = new BinaryWriter(f, Encoding.UTF8, true)) { w.Write(Hash(body)); w.Write(body.Length); w.Write(body); w.Flush(); } f.Flush(true); }
                cancellation.ThrowIfCancellationRequested();
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            } finally { if (File.Exists(temporary)) { try { File.Delete(temporary); } catch (IOException) { } } }
        }
        void Load()
        {
            try {
                using (var f = File.OpenRead(path)) using (var r = new BinaryReader(f)) {
                    string checksum = r.ReadString(); int length = r.ReadInt32();
                    if (length < 0 || length > 64000000) throw new InvalidDataException("저장된 훈련 자료의 크기가 올바르지 않습니다.");
                    byte[] body = r.ReadBytes(length);
                    if (body.Length != length || f.Position != f.Length || Hash(body) != checksum) throw new InvalidDataException("저장된 훈련 자료의 무결성을 확인할 수 없습니다.");
                    using (var m = new MemoryStream(body)) using (var b = new BinaryReader(m)) {
                        if (b.ReadString() != "CHOOGuard-local-MVP-v1") throw new InvalidDataException("지원하지 않는 훈련 저장 형식입니다.");
                        revision = b.ReadInt64(); if (revision < 0) throw new InvalidDataException("훈련 상태 버전이 올바르지 않습니다.");
                        teams = InitialTeams();
                        for (int n = 0; n < teams.Count; n++) { var t = teams[n]; string location = b.ReadString(), status = b.ReadString();
                            if (!TargetIds.Contains(location) || (status != "Ready" && status != "Moved" && status != "Holding")) throw new InvalidDataException("저장된 팀 상태가 올바르지 않습니다.");
                            teams[n] = new MvpTeamView(t.Id, t.Name, t.AgencyId, location, status); }
                        int count = b.ReadInt32(); if (count < 0 || count > 100000) throw new InvalidDataException("저장된 처리결과 수가 올바르지 않습니다.");
                        for (int n = 0; n < count; n++) {
                            var key = new ReceiptKey(Id(b.ReadString()), Id(b.ReadString()), Id(b.ReadString())); string fingerprint = b.ReadString();
                            var status = (ReceiptStatus)b.ReadInt32(); string commit = b.ReadString(); long seq = b.ReadInt64();
                            int targets = b.ReadInt32(); if (targets < 1 || targets > 4096) throw new InvalidDataException("저장된 목적지가 올바르지 않습니다.");
                            var results = new List<TargetResult>();
                            for (int k = 0; k < targets; k++) { var target = Id(b.ReadString()); var ts = (TargetStatus)b.ReadInt32(); string reason = b.ReadString();
                                results.Add(new TargetResult(target, ts, reason.Length == 0 ? new Reason[0] : new[] { new Reason(Id("mvp-rejected"), ReasonAxis.evidence, target, new ContentReference[0], reason) })); }
                            receipts.Add(key, new CommandReceipt(key, fingerprint, status, commit.Length == 0 ? null : commit, seq < 0 ? (Sequence?)null : new Sequence(seq), results));
                        }
                        if (m.Position != m.Length) throw new InvalidDataException("훈련 자료에 알 수 없는 내용이 있습니다.");
                    }
                }
            } catch (Exception e) when (e is IOException || e is ArgumentException || e is OverflowException) { throw new InvalidDataException("저장된 훈련 자료를 읽을 수 없습니다. 기존 자료는 초기화하지 않고 보존했습니다.", e); }
        }
        public void Dispose() { lock (gate) { if (disposed) return; disposed = true; lease.Dispose(); } }
    }
}
