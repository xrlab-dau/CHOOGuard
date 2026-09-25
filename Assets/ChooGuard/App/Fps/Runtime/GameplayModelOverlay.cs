using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>Separately authored attachment; missing overlay never becomes guessed station semantics.</summary>
    public sealed class GameplayModelOverlay : MonoBehaviour
    {
        public TextAsset SeedJson, TransitionJson;
        public string GeometryRevision;
        public FloorNavigationBinding[] Floors = Array.Empty<FloorNavigationBinding>();
        public PortalNavigationBinding[] Portals = Array.Empty<PortalNavigationBinding>();
        public DoorNavigationBinding[] Doors = Array.Empty<DoorNavigationBinding>();
        public FpsEntityBinding[] Entities = Array.Empty<FpsEntityBinding>();
        public void Validate()
        {
            if (SeedJson == null || TransitionJson == null || string.IsNullOrWhiteSpace(GeometryRevision) || Floors.Length == 0 || Entities.Length == 0)
                throw new InvalidOperationException("명시적 세계/전이/형상 revision/이동면/상호작용 바인딩이 모두 필요합니다.");
            foreach (var entity in Entities)
                if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId) || entity.ContactSurface == null)
                    throw new InvalidOperationException("모델 의미 바인딩에 실물 접촉 형상이 없습니다.");
        }
    }
}
