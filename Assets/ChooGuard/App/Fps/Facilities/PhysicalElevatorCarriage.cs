using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>
    /// A shared carriage made from an explicitly supplied existing cabin and its car door leaves. The other authored
    /// cabins are retained as source references. Only their known vertical endpoint transforms define the motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalElevatorCarriage : MonoBehaviour
    {
        [Serializable]
        public sealed class Stop
        {
            public StationDoor Door;
            public Transform SourceCabin;
            public Transform[] CarDoorLeaves = Array.Empty<Transform>();
        }

        public string CarId;
        public Stop[] Stops = Array.Empty<Stop>();
        public Transform Cabin { get; private set; }
        public bool Bound => car != null && Cabin != null && isActiveAndEnabled;
        public string BindingIssue { get; private set; }
        public float PositionY => Cabin != null ? Cabin.position.y : float.NaN;
        public int CarDoorCount => movingLeaves.Count;

        private sealed class SavedStop
        {
            public Stop Source;
            public StationDoor.Leaf[] Leaves;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Active;
            public bool[] LeafActive;
        }
        private sealed class MovingLeaf
        {
            public Transform Pivot, Parent;
            public Vector3 OriginalPosition, ClosedPosition, Slide;
            public Quaternion OriginalRotation;
            public bool Active;
        }
        private readonly List<SavedStop> saved = new List<SavedStop>();
        private readonly List<MovingLeaf> movingLeaves = new List<MovingLeaf>();
        private ElevatorCar car;
        private CabinDoorOperation doorOperation;

        public bool TryBind(ElevatorCar drive, out string reason)
        {
            if (car == drive && Bound) { reason = null; return true; }
            reason = Validate(drive);
            BindingIssue = reason;
            if (reason != null) return false;
            Unbind();
            car = drive;
            foreach (var landing in drive.Landings)
            {
                var source = Array.Find(Stops, stop => stop.Door == landing.Door);
                saved.Add(new SavedStop { Source = source, Leaves = source.Door.Leaves,
                    Position = source.SourceCabin.position, Rotation = source.SourceCabin.rotation,
                    Active = source.SourceCabin.gameObject.activeSelf,
                    LeafActive = Array.ConvertAll(source.CarDoorLeaves, pivot => pivot.gameObject.activeSelf) });
            }
            Cabin = saved[0].Source.SourceCabin;
            foreach (var leaf in saved[0].Leaves)
            {
                if (Array.IndexOf(saved[0].Source.CarDoorLeaves, leaf.Pivot) < 0) continue;
                var pivot = leaf.Pivot;
                var entry = new MovingLeaf { Pivot = pivot, Parent = pivot.parent, OriginalPosition = pivot.localPosition,
                    OriginalRotation = pivot.localRotation, Active = pivot.gameObject.activeSelf,
                    Slide = Cabin.InverseTransformVector(pivot.parent.TransformVector(leaf.ClosedRotation * leaf.Slide)) };
                pivot.SetParent(Cabin, true);
                entry.ClosedPosition = pivot.localPosition;
                movingLeaves.Add(entry);
            }
            foreach (var stop in saved)
            {
                var hall = new List<StationDoor.Leaf>();
                foreach (var leaf in stop.Leaves)
                    if (Array.IndexOf(stop.Source.CarDoorLeaves, leaf.Pivot) < 0) hall.Add(leaf);
                stop.Source.Door.Leaves = hall.ToArray();
                if (stop.Source.SourceCabin != Cabin)
                {
                    stop.Source.SourceCabin.gameObject.SetActive(false);
                    foreach (var pivot in stop.Source.CarDoorLeaves) pivot.gameObject.SetActive(false);
                }
            }
            // Kinematic motion keeps the existing floor/wall collider geometry on the same physical carriage.
            var body = Cabin.GetComponent<Rigidbody>();
            if (body == null) body = Cabin.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.None;
            doorOperation = Cabin.gameObject.AddComponent<CabinDoorOperation>();
            doorOperation.Car = drive;
            ApplyDoors(0);
            BindingIssue = null;
            return true;
        }

        private string Validate(ElevatorCar drive)
        {
            if (drive == null || string.IsNullOrWhiteSpace(CarId) || drive.Id != CarId) return "car_id_mismatch";
            if (Stops == null || Stops.Length < 2 || Stops.Length != drive.Landings.Count) return "stop_count_mismatch";
            var doors = new HashSet<StationDoor>();
            var cabins = new HashSet<Transform>();
            var parts = new HashSet<Transform>();
            Vector3 position = default, size = default;
            Quaternion rotation = default;
            for (int index = 0; index < Stops.Length; index++)
            {
                var stop = Stops[index];
                if (stop == null || stop.Door == null || stop.SourceCabin == null || stop.Door.Cabin != stop.SourceCabin ||
                    stop.Door.CarId != CarId || drive.IndexOf(stop.Door) < 0 || !doors.Add(stop.Door) || !cabins.Add(stop.SourceCabin)) return "source_stop_mismatch";
                if (stop.CarDoorLeaves == null || stop.CarDoorLeaves.Length != 2) return "car_leaves_missing";
                foreach (var pivot in stop.CarDoorLeaves)
                {
                    if (pivot == null || !parts.Add(pivot) || Array.Find(stop.Door.Leaves, leaf => leaf.Pivot == pivot) == null) return "source_leaf_mismatch";
                }
                var current = stop.SourceCabin.position;
                if (!Finite(current.x) || !Finite(current.y) || !Finite(current.z)) return "nonfinite_stop";
                if (index == 0) { position = current; size = stop.Door.CabinSize; rotation = stop.SourceCabin.rotation; }
                else if (new Vector2(current.x - position.x, current.z - position.z).magnitude > .002f ||
                    Quaternion.Angle(rotation, stop.SourceCabin.rotation) > .01f || Vector3.Distance(size, stop.Door.CabinSize) > .001f)
                    return "stops_are_not_one_existing_vertical_bank";
                if (size.x <= 0 || size.y <= 0 || size.z <= 0) return "invalid_cabin_dimensions";
            }
            for (int i = 0; i < Stops.Length; i++)
                for (int j = i + 1; j < Stops.Length; j++)
                    if (Mathf.Abs(Stops[i].SourceCabin.position.y - Stops[j].SourceCabin.position.y) < .1f) return "duplicate_vertical_stop";
            return null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public Vector3 Endpoint(int landing) => saved[landing].Position;

        /// <summary>Moves to the interpolated existing endpoints and returns the world displacement to carry occupants.</summary>
        public Vector3 Move(int from, int to, float fraction)
        {
            if (!Bound || from < 0 || to < 0 || from >= saved.Count || to >= saved.Count || !Finite(fraction)) return Vector3.zero;
            Vector3 before = Cabin.position;
            Cabin.position = Vector3.Lerp(saved[from].Position, saved[to].Position, Mathf.Clamp01(fraction));
            return Cabin.position - before;
        }

        public void ApplyDoors(float fraction)
        {
            foreach (var leaf in movingLeaves)
                if (leaf.Pivot != null) leaf.Pivot.localPosition = leaf.ClosedPosition + leaf.Slide * Mathf.Clamp01(fraction);
        }

        private void LateUpdate()
        {
            if (!Bound || car.Landings.Count <= car.At) return;
            ApplyDoors(car.Now == ElevatorCar.State.Moving ? 0 : car.Landings[car.At].Door.Open);
        }

        public void Unbind()
        {
            StopAllCoroutines();
            foreach (var leaf in movingLeaves)
            {
                if (leaf.Pivot == null || leaf.Parent == null) continue;
                leaf.Pivot.SetParent(leaf.Parent, false);
                leaf.Pivot.SetLocalPositionAndRotation(leaf.OriginalPosition, leaf.OriginalRotation);
                leaf.Pivot.gameObject.SetActive(leaf.Active);
            }
            foreach (var stop in saved)
            {
                if (stop.Source.Door != null) stop.Source.Door.Leaves = stop.Leaves;
                if (stop.Source.SourceCabin == null) continue;
                stop.Source.SourceCabin.SetPositionAndRotation(stop.Position, stop.Rotation);
                stop.Source.SourceCabin.gameObject.SetActive(stop.Active);
                for (int i = 0; i < stop.Source.CarDoorLeaves.Length; i++)
                {
                    var pivot = stop.Source.CarDoorLeaves[i];
                    if (pivot != null) pivot.gameObject.SetActive(stop.LeafActive[i]);
                }
            }
            ForgetBinding(true);
        }

        /// <summary>
        /// An owner/scene may already be inside recursive destruction. Restore only after a frame if this source owner
        /// survives drive-only removal; never reparent children from OnDestroy while their destination is being destroyed.
        /// </summary>
        public void DetachDestroyedDrive(ElevatorCar destroyedDrive)
        {
            if (!ReferenceEquals(car, destroyedDrive)) return;
            car = null;
            if (doorOperation != null) doorOperation.Car = null;
            if (isActiveAndEnabled && gameObject.scene.isLoaded) StartCoroutine(RestoreSurvivingSources());
            else ForgetBinding(false);
        }

        private IEnumerator RestoreSurvivingSources()
        {
            yield return null;
            if (this == null) yield break;
            if (gameObject.scene.isLoaded && saved.TrueForAll(stop => stop.Source.Door != null && stop.Source.SourceCabin != null))
                Unbind();
            else ForgetBinding(false);
        }

        private void ForgetBinding(bool removeOperation)
        {
            if (doorOperation != null)
            {
                doorOperation.Car = null;
                if (removeOperation) Destroy(doorOperation);
            }
            doorOperation = null;
            saved.Clear(); movingLeaves.Clear(); Cabin = null; car = null;
        }

        private void OnDestroy() => ForgetBinding(false);
    }

    /// <summary>The moving car door belongs to the current car, rather than its preserved lower landing source.</summary>
    public sealed class CabinDoorOperation : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsObservable
    {
        public ElevatorCar Car;
        public string DisplayName => "승강기 카문";
        public string InteractionPrompt => "문 열림 요청";
        public string StateText => Car == null ? "운행 연결 없음" : !Car.Running ? "운행 정지" :
            Car.Now == ElevatorCar.State.Moving ? "층 사이 이동 · 문 잠김" : Car.Landings[Car.At].Floor + "층";
        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = Car == null ? "운행 연결 없음" : !Car.Running ? "운행 정지 · 비상통화 버튼을 사용하세요" :
                Car.Now == ElevatorCar.State.Moving ? "층 사이에서는 문이 열리지 않습니다" : null;
            return reason == null;
        }
        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            if (!CanInteract(responder, out feedback)) return false;
            Car.OpenButton(); feedback = Car.Landings[Car.At].Floor + "층 문 열림 요청"; return true;
        }
        public string Observe(FirstPersonResponder responder) => DisplayName + " · " + StateText;
    }
}
