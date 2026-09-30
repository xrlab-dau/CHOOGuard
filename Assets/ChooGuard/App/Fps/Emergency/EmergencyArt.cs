using System;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Edit-time generated data the shift needs at runtime: the whole-station navmesh and station data, the crowd, the
    /// KTX inner-door patches, the materials for things that appear during an incident, the hanging boards that can
    /// come down in an earthquake, and the recorded station sounds. Built by ChooGuard.Editor.EmergencySceneBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "ChooGuard/Emergency Art", fileName = "EmergencyArt")]
    public sealed class EmergencyArt : ScriptableObject
    {
        [Header("세계")]
        public NavMeshData WorldNavMesh;
        public TextAsset StationData;
        public CrowdCatalog Crowd;
        [Header("열차 객실 안쪽 자동문")]
        public TrainDoorPatch[] TrainDoors = Array.Empty<TrainDoorPatch>();
        [Header("사건 재질")]
        public Material Flame;
        public Material Smoke;
        [Header("통제선: 벨트 차단봉(스테인리스 기둥·무게 받침, 검은 벨트 통)과 인쇄 벨트")]
        public Mesh Stanchion;
        public Material CordonPost;
        public Material CordonHead;
        public Material CordonTape;
        [Header("출입문에 끼는 물건: 외투 자락(모직), 가죽 끈과 손가방")]
        public Mesh CoatFlap;
        public Material Coat;
        public Material Leather;
        [Header("소품 모델(Objaverse·Sketchfab CC BY, ThirdParty/Models/Objaverse)")]
        public GameObject SuitcaseModel;
        public GameObject HandbagModel;
        public GameObject MedicalBagModel;
        [Header("출동 장비(Objaverse·Sketchfab CC BY, ThirdParty/Models/Objaverse)")]
        public GameObject NozzleModel;
        public GameObject CotModel;
        public GameObject ToolboxModel;
        public GameObject FlashlightModel;
        public GameObject WetFloorSignModel;
        public GameObject EodRobotModel;
        public GameObject TrafficConeModel;
        [Tooltip("소방 호스(방수포 천): 바닥에 깔리는 선.")]
        public Material HoseMaterial;
        [Header("출동 차량(경광등 포함 프리팹: 소방 펌프차, 구급차, 순찰차, 경찰특공대 차량)")]
        public GameObject FireEngine;
        public GameObject Ambulance;
        public GameObject PoliceCar;
        public GameObject SwatVan;
        [Header("지진 때 떨어질 수 있는 매달린 안내판")]
        public HangingItem[] Hanging = Array.Empty<HangingItem>();
        [Header("설비 배치(화재 감지·소화·방화구획·감시, 전기·광장, 주방·가스): 그룹별 배치 파일과 프리팹")]
        public EquipmentCatalog Equipment;
        [Header("소리(녹음, ThirdParty/Audio/Station)")]
        public AudioClip ConcourseBed;
        public AudioClip PlatformBed;
        public AudioClip OutdoorBed;
        public AudioClip TrainArrive;
        public AudioClip TrainDepart;
        public AudioClip DoorOpen;
        public AudioClip DoorClose;
        public AudioClip Escalator;
        public AudioClip Fire;
        public AudioClip[] Footsteps = Array.Empty<AudioClip>();
        [Tooltip("PaLine 순서의 안내방송 음성(MeloTTS-Korean 으로 미리 만든 파일).")]
        public AudioClip[] Announcements = Array.Empty<AudioClip>();

        /// <summary>One car interior mesh split into a fixed part and the sliding vestibule door leaf (car-local slide).</summary>
        [Serializable]
        public sealed class TrainDoorPatch
        {
            public string Car;
            public string Source;
            public Mesh Fixed;
            public Mesh Leaf;
            public Vector3 Slide;
        }
        /// <summary>One renderer of a hanging board. Static-batched parts carry their mesh asset so a copy can fall.</summary>
        [Serializable]
        public sealed class HangingPart
        {
            public string Path;
            public Mesh Mesh;
            public Material[] Materials;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        [Serializable]
        public sealed class HangingItem
        {
            public string Label;
            public Vector3 Centre;
            public Vector3 Size;
            public HangingPart[] Parts;
        }
    }
}
