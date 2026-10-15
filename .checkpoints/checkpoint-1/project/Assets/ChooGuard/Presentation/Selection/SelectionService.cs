using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using ChooGuard.Contracts;

namespace ChooGuard.Presentation.Selection
{
    // Supplied by the authorized projection adapter, never inferred from scene names/proximity.
    public interface ISelectionAuthority
    {
        bool TryRead(StableId entityId, out SelectionEntity entity);
    }

    public sealed class SelectionEntity
    {
        public StableId EntityId { get; }
        public StableId FloorId { get; }
        public bool Visible { get; }
        public bool Selectable { get; }
        public StableId? AgencyId { get; }
        public IReadOnlyList<StableId> CrewIds { get; }
        public ContentReference Source { get; }
        public SelectionEntity(StableId entityId, StableId floorId, bool visible, bool selectable,
            ContentReference source, StableId? agencyId = null, IEnumerable<StableId> crewIds = null)
        {
            if (!entityId.IsValid || !floorId.IsValid || (agencyId.HasValue && !agencyId.Value.IsValid))
                throw new ArgumentException("Invalid selection identity.");
            EntityId = entityId; FloorId = floorId; Visible = visible; Selectable = selectable;
            Source = source ?? throw new ArgumentNullException(nameof(source)); AgencyId = agencyId;
            var crew = new List<StableId>();
            if (crewIds != null) foreach (var id in crewIds)
            {
                if (!id.IsValid) throw new ArgumentException("Invalid crew identity.");
                if (!crew.Contains(id)) crew.Add(id);
            }
            CrewIds = new ReadOnlyCollection<StableId>(crew);
        }
    }

    public sealed class SelectionSet
    {
        public long Version { get; }
        public IReadOnlyList<StableId> EntityIds { get; }
        public IReadOnlyList<SelectionEntity> Entities { get; }
        internal SelectionSet(long version, List<SelectionEntity> entities)
        {
            Version = version;
            Entities = new ReadOnlyCollection<SelectionEntity>(entities.ToArray());
            var ids = new List<StableId>();
            foreach (var entity in entities) ids.Add(entity.EntityId);
            EntityIds = new ReadOnlyCollection<StableId>(ids);
        }
    }

    // List and world adapters share this instance. World adapter calls only for router-owned
    // World pointer-up; this service neither polls input nor expands vehicle/crew identities.
    public sealed class SelectionService
    {
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly ISelectionAuthority authority;
        public SelectionSet Current { get; private set; } = new SelectionSet(0, new List<SelectionEntity>());
        public event Action<SelectionSet> Changed;
        public SelectionService(ISelectionAuthority authority)
        { this.authority = authority ?? throw new ArgumentNullException(nameof(authority)); }
        public SelectionSet Replace(IEnumerable<StableId> candidates, StableId activeFloor)
        {
            CheckThread();
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (!activeFloor.IsValid) throw new ArgumentException("Invalid floor.", nameof(activeFloor));
            var entities = new List<SelectionEntity>();
            var seen = new HashSet<StableId>();
            foreach (var id in candidates)
            {
                if (!id.IsValid) throw new ArgumentException("Invalid entity identity.", nameof(candidates));
                if (seen.Add(id) && authority.TryRead(id, out var entity) && entity != null &&
                    entity.EntityId.Equals(id) && entity.Visible && entity.Selectable && entity.FloorId.Equals(activeFloor))
                    entities.Add(entity);
            }
            entities.Sort((a, b) => StringComparer.Ordinal.Compare(a.EntityId.Value, b.EntityId.Value));
            Current = new SelectionSet(checked(Current.Version + 1), entities);
            Changed?.Invoke(Current);
            return Current;
        }
        // Call on projection/visibility/floor changes, not only on user clicks.
        public SelectionSet Refresh(StableId activeFloor) => Replace(Current.EntityIds, activeFloor);
        public void Clear()
        {
            CheckThread();
            Current = new SelectionSet(checked(Current.Version + 1), new List<SelectionEntity>());
            Changed?.Invoke(Current);
        }
        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Selection is owned by its creating UI thread.");
        }
    }
}
