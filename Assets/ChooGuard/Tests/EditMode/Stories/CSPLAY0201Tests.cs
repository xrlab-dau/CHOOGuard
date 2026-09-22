#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using ChooGuard.Contracts;
using ChooGuard.Presentation.Selection;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSPLAY0201Tests
    {
        internal sealed class Authority : ISelectionAuthority
        {
            public readonly Dictionary<StableId, SelectionEntity> Entities = new Dictionary<StableId, SelectionEntity>();
            public bool TryRead(StableId id, out SelectionEntity entity) => Entities.TryGetValue(id, out entity);
        }
        internal static StableId Id(string value) => new StableId(value);
        internal static ContentReference Source => new ContentReference(Id("source"), 1, new string('a', 64));
        internal static SelectionEntity Entity(string id, string floor = "floor", bool visible = true, bool selectable = true)
            => new SelectionEntity(Id(id), Id(floor), visible, selectable, Source);
        [Test]
        public void ListAndWorldUseSameCanonicalImmutableSnapshot()
        {
            var authority = new Authority();
            authority.Entities.Add(Id("team"), Entity("team"));
            authority.Entities.Add(Id("task"), Entity("task"));
            var service = new SelectionService(authority);
            var list = service.Replace(new[] { Id("team"), Id("task"), Id("team") }, Id("floor"));
            var world = service.Replace(new[] { Id("task"), Id("team") }, Id("floor"));
            Assert.That(world.EntityIds, Is.EqualTo(list.EntityIds));
            Assert.That(service.Current, Is.SameAs(world));
            service.Clear();
            Assert.That(world.EntityIds.Count, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<StableId>)world.EntityIds).Clear());
        }
        [Test]
        public void HiddenOtherFloorDeniedUnknownAreExcludedAndRefreshRevokes()
        {
            var authority = new Authority();
            foreach (var e in new[] { Entity("ok"), Entity("hidden", visible: false), Entity("other", "upstairs"), Entity("denied", selectable: false) })
                authority.Entities.Add(e.EntityId, e);
            var service = new SelectionService(authority);
            service.Replace(new[] { Id("ok"), Id("hidden"), Id("other"), Id("denied"), Id("unknown") }, Id("floor"));
            Assert.That(service.Current.EntityIds, Is.EqualTo(new[] { Id("ok") }));
            authority.Entities[Id("ok")] = Entity("ok", selectable: false);
            service.Refresh(Id("floor"));
            Assert.That(service.Current.EntityIds, Is.Empty);
        }
        [Test]
        public void AgencyAndCrewAreOnlyExplicitSourcedMetadataNotImplicitSelection()
        {
            var authority = new Authority();
            var crew = new List<StableId> { Id("crew") };
            authority.Entities[Id("vehicle")] = new SelectionEntity(Id("vehicle"), Id("floor"), true, true, Source, Id("agency"), crew);
            crew.Clear();
            var service = new SelectionService(authority);
            var result = service.Replace(new[] { Id("vehicle") }, Id("floor"));
            Assert.That(result.EntityIds, Is.EqualTo(new[] { Id("vehicle") }));
            Assert.That(result.Entities[0].CrewIds, Is.EqualTo(new[] { Id("crew") }));
            Assert.That(Entity("unknown").AgencyId, Is.Null);
            Assert.Throws<ArgumentNullException>(() => new SelectionEntity(Id("x"), Id("floor"), true, true, null));
        }
    }
}
#endif
