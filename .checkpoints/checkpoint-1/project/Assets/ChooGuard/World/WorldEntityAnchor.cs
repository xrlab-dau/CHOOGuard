using System;
using ChooGuard.Contracts;
using UnityEngine;

namespace ChooGuard.World
{
    public enum WorldRepresentationKind { Space, Door, MetreReference }

    /// <summary>표시 전용 불변 DTO. 실제 SessionProjection.EntitiesRef resolver는 아직 구현하지 않는다.</summary>
    public sealed class EntityVisualProjection
    {
        public StableId EntityId { get; }
        public long Revision { get; }
        public bool DoorOpen { get; }
        public bool Visible { get; }
        public EntityVisualProjection(StableId entityId, long revision, bool doorOpen, bool visible)
        {
            if (!entityId.IsValid) throw new ArgumentException("유효한 entity ID가 필요합니다.", nameof(entityId));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            EntityId = entityId; Revision = revision; DoorOpen = doorOpen; Visible = visible;
        }
    }

    /// <summary>하나의 run/authority 읽기 스트림에 대한 결속 토큰. 명령 권한이나 인증 증명이 아니다.</summary>
    public sealed class WorldProjectionBinding
    {
        public StableId RunId { get; }
        public StableId AuthorityId { get; }
        public StableId FrameId { get; }
        public StableId EntityId { get; }
        internal WorldProjectionBinding(StableId runId, StableId authorityId, StableId frameId, StableId entityId)
        { RunId = runId; AuthorityId = authorityId; FrameId = frameId; EntityId = entityId; }
    }

    /// <summary>Transform은 표시 수단일 뿐 Domain 상태/좌표 정본이 아니다.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldEntityAnchor : MonoBehaviour
    {
        [SerializeField] private string entityId;
        [SerializeField] private string frameId;
        [SerializeField] private WorldRepresentationKind representation;
        [SerializeField] private bool initialized;
        private WorldProjectionBinding binding;
        private long projectedRevision = -1;
        public StableId EntityId => new StableId(entityId);
        public StableId FrameId => new StableId(frameId);
        public WorldRepresentationKind Representation => representation;
        public SiUnit Unit => SiUnit.Metre;
        public string QualificationLabel => "SYNTHETIC_FIXTURE / NOT_FIELD_APPROVED";
        public long ProjectedRevision => projectedRevision;
        public bool ProjectedDoorOpen { get; private set; }
        public bool ProjectedVisible { get; private set; } = true;

        /// <summary>생성 시 1회만 사용하는 메타데이터 설정. 업무 명령 API가 아니다.</summary>
        public void Initialize(StableId entity, StableId frame, WorldRepresentationKind kind)
        {
            if (initialized) throw new InvalidOperationException("이미 초기화된 앵커입니다.");
            if (!entity.IsValid || !frame.IsValid) throw new ArgumentException("유효한 entity/frame ID가 필요합니다.");
            if (!Enum.IsDefined(typeof(WorldRepresentationKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            ValidateGeometry();
            entityId = entity.Value; frameId = frame.Value; representation = kind; initialized = true;
        }

        public WorldProjectionBinding BindProjection(StableId runId, StableId authorityId, StableId frame, StableId entity)
        {
            if (!runId.IsValid || !authorityId.IsValid || !frame.IsValid || !entity.IsValid)
                throw new ArgumentException("유효한 projection 결속 ID가 필요합니다.");
            if (!initialized || !FrameId.Equals(frame) || !EntityId.Equals(entity))
                throw new ArgumentException("앵커와 projection frame/entity가 다릅니다.");
            if (binding != null)
            {
                if (!binding.RunId.Equals(runId) || !binding.AuthorityId.Equals(authorityId))
                    throw new InvalidOperationException("다른 run/authority는 새 앵커에 결속해야 합니다.");
                return binding;
            }
            binding = new WorldProjectionBinding(runId, authorityId, frame, entity);
            return binding;
        }

        /// <summary>같은 revision도 거부한다. 표시 갱신은 collider나 논리 객체를 비활성화하지 않는다.</summary>
        public void ApplyProjection(WorldProjectionBinding source, EntityVisualProjection projection)
        {
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (source == null || !ReferenceEquals(binding, source)) throw new ArgumentException("결속되지 않은 projection 스트림입니다.", nameof(source));
            if (!projection.EntityId.Equals(EntityId)) throw new ArgumentException("projection 대상이 다릅니다.", nameof(projection));
            if (projection.Revision <= projectedRevision) throw new InvalidOperationException("동일하거나 오래된 projection revision입니다.");
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = projection.Visible;
            var visual = transform.Find("Visual");
            if (representation == WorldRepresentationKind.Door && visual != null)
                visual.localRotation = Quaternion.Euler(0, projection.DoorOpen ? 90 : 0, 0);
            ProjectedDoorOpen = projection.DoorOpen;
            ProjectedVisible = projection.Visible;
            projectedRevision = projection.Revision;
        }

        public void ValidateGeometry()
        {
            // 부모의 음수 축이 서로 상쇄되어도 허용하지 않는다.
            for (var current = transform; current != null; current = current.parent)
            {
                ValidateScale(current.localScale);
                ValidateFinite(current.localPosition);
            }
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                ValidateScale(child.localScale);
                ValidateFinite(child.localPosition);
            }
        }
        public static void ValidateScale(Vector3 scale)
        {
            ValidateFinite(scale);
            if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0) throw new ArgumentOutOfRangeException(nameof(scale), "축 scale은 양수여야 합니다.");
        }
        private static void ValidateFinite(Vector3 value)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z))
                throw new ArgumentOutOfRangeException(nameof(value), "유한 좌표/scale만 허용합니다.");
        }
    }
}
