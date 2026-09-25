using System;
using System.IO;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Persistence;

namespace ChooGuard.App.Fps.Runtime
{
    public sealed class GameplayPersistenceStore : IGameplayCommitSink, IDisposable
    {
        private readonly GameplayJournal journal;
        private bool disposed;
        public GameplayPersistenceStore(string databasePath) { journal = new GameplayJournal(databasePath); }
        public WorldSession Open(WorldSnapshot initial, bool resume = true)
        {
            Check(); if (initial == null) throw new ArgumentNullException(nameof(initial));
            var checkpoint = journal.ReadCheckpoint(initial.RunId.Value);
            if (checkpoint != null && !resume) throw new InvalidOperationException("기존 run을 덮어쓰지 않습니다. 새 run ID를 사용하세요.");
            WorldSnapshot basis;
            if (checkpoint == null)
            {
                basis = initial;
                string json = GameplayCodec.SerializeSnapshot(initial);
                journal.AppendAndCheckpoint(initial.RunId.Value, initial.Generation, initial.Revision, "world.initial", string.Empty, json, json);
            }
            else
            {
                basis = GameplayCodec.DeserializeSnapshot(checkpoint.Json);
                if (!basis.RunId.Equals(initial.RunId) || basis.Mode != initial.Mode || basis.RulesetId != initial.RulesetId || basis.RulesetRevision != initial.RulesetRevision)
                    throw new InvalidDataException("저장 run/mode/규칙 판본이 현재 콘텐츠와 다릅니다.");
            }
            var session = new WorldSession(basis, this);
            try
            {
                foreach (var entry in journal.Read(initial.RunId.Value))
                {
                    if (entry.Sequence <= basis.Revision) continue;
                    var mutation = GameplayCodec.DeserializeMutation(entry.Json);
                    if (mutation.Revision != entry.Sequence || mutation.Generation != entry.Generation || mutation.Kind != entry.Kind)
                        throw new InvalidDataException("저장 envelope와 의미 전이의 상관관계가 다릅니다.");
                    session.ReplayCommitted(mutation);
                }
                session.AdvanceGeneration("storage-open-new-inference-lifetime");
                return session;
            }
            catch { session.Dispose(); throw; }
        }
        public void Commit(WorldMutation mutation)
        {
            Check();
            journal.Append(mutation.RunId.Value, mutation.Generation, mutation.Revision, mutation.Kind,
                mutation.ActorId.HasValue ? mutation.ActorId.Value.Value : string.Empty, GameplayCodec.SerializeMutation(mutation));
        }
        public void Save(WorldSession session)
        { Check(); if (session == null) throw new ArgumentNullException(nameof(session)); session.SaveCheckpoint(); }
        public void Dispose() { if (disposed) return; journal.Dispose(); disposed = true; }
        private void Check() { if (disposed) throw new ObjectDisposedException(nameof(GameplayPersistenceStore)); }
    }
}
