using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A KTX car's platform door leaf. The train manager (열차팀장) works the exterior doors; the station staff member asks for
    /// one over the radio (research K2, JEV 010: radio_crew_request) and the crew answers by the stage of the service.
    /// </summary>
    public sealed class TrainDoorControl : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        private TrainService service;
        private TrainService.Car car;
        private string name_;

        public void Bind(TrainService owner, TrainService.Car door)
        {
            service = owner;
            car = door;
            name_ = "KTX " + door.Label + " 출입문 (열차팀장 취급)";
        }

        public string DisplayName => name_;
        public string InteractionPrompt => "무전 · 열차팀장에게 개방 요청";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (service == null) { reason = ""; return false; }
            if (service.DoorsOpen > .98f || car.CrewOpen > .98f) { reason = "열려 있음 · 여닫기는 열차팀장이 합니다"; return false; }
            if (service.Stage == TrainService.Phase.Away || service.Stage == TrainService.Phase.Departing) { reason = "운행 중"; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            service.Ask(car);
            return true;
        }
    }

    /// <summary>
    /// The automatic door between a KTX vestibule and the saloon. It opens for anyone who comes close; the green
    /// '1분 열림' button above it holds it open for a minute (for luggage or a wheelchair; research K1).
    /// </summary>
    public sealed class TrainInnerDoorControl : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public const float HoldSeconds = 60;
        private TrainService.InnerDoor door;
        private static Material lamp;

        /// <summary>
        /// Puts the button above the door on its vestibule side and makes both the leaf and the button pressable. The door
        /// leaf mesh runs up into the ceiling cavity, so the height comes from the vestibule ceiling (just under it, at most
        /// 1.95 m above the car floor) and the button sits on the partition face found by a ray.
        /// </summary>
        internal static void Build(TrainService service, TrainService.InnerDoor door, Bounds closed, Vector3 vestibule, float floor)
        {
            door.Leaf.gameObject.AddComponent<TrainInnerDoorControl>().door = door;
            var slide = door.Leaf.parent.TransformVector(door.Slide);
            slide.y = 0;
            if (slide.sqrMagnitude < 1e-6f) return;
            var normal = Vector3.Cross(Vector3.up, slide.normalized);
            var toward = vestibule - closed.center;
            if (Vector3.Dot(toward, normal) < 0) normal = -normal;
            float depth = Mathf.Abs(Vector3.Dot(closed.extents, new Vector3(Mathf.Abs(normal.x), 0, Mathf.Abs(normal.z))));
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "1분 열림 버튼";
            button.transform.SetParent(door.Leaf.parent, false);
            Physics.SyncTransforms();
            var front = new Vector3(closed.center.x, floor + 1.2f, closed.center.z) + normal * (depth + .35f);
            float ceiling = Physics.Raycast(front, Vector3.up, out var roof, 2f, ~0, QueryTriggerInteraction.Ignore) ? roof.point.y : floor + 2.1f;
            var above = new Vector3(closed.center.x, Mathf.Min(ceiling - .07f, floor + 1.95f), closed.center.z);
            var surface = above + normal * (depth + .02f);
            if (Physics.Raycast(above + normal * .7f, -normal, out var hit, .75f, ~0, QueryTriggerInteraction.Ignore)) surface = hit.point + normal * .014f;
            button.transform.position = surface;
            button.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
            // 차량 모델은 축척이 걸린 부모 아래에 있다: 월드 크기 7 x 5 cm, 두께 2.5 cm 로 맞춘다.
            var scale = door.Leaf.parent.lossyScale;
            button.transform.localScale = new Vector3(.07f / scale.x, .05f / scale.y, .025f / scale.z);
            if (lamp == null)
            {
                lamp = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "1분 열림 버튼 (초록)" };
                lamp.SetColor("_BaseColor", new Color(.1f, .85f, .35f));
            }
            button.GetComponent<MeshRenderer>().sharedMaterial = lamp;
            button.AddComponent<TrainInnerDoorControl>().door = door;
        }

        public string DisplayName => "KTX 객실 통로 자동문";
        public string InteractionPrompt => Time.time < door.HoldUntil ? "1분 열림 다시 누르기" : "1분 열림 버튼 누르기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            return door != null;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (door == null) return false;
            door.HoldUntil = Time.time + HoldSeconds;
            feedback = "통로 자동문을 1분 동안 열어 둡니다";
            return true;
        }
    }
}
