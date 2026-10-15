using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A CCTV camera of the station (a <see cref="StationEquipment"/> of kind <see cref="CameraKind"/>) whose picture the station office can look at: a ceiling dome
    /// sees all round, a bullet camera on a wall or a column sees the sector it points into. What the office learns from it (smoke, flames, a crowd at a shutter) is
    /// what the camera covers, no more (Railway Safety Act art. 39-3 and enforcement decree art. 30: the cameras cover platforms, concourses and lift/escalator ends
    /// and never a toilet). The numbers come from the placement entry (<c>range</c> metres along the floor, <c>fov</c> horizontal field of view in degrees, 360 for a dome,
    /// <c>floor</c> height of the floor it watches, <c>view</c> what it is aimed at in words).
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class SurveillanceCamera : MonoBehaviour
    {
        public const string CameraKind = "cctv_camera";

        public StationEquipment Equipment { get; private set; }
        public float Range { get; private set; } = 20f;
        public float Fov { get; private set; } = 360f;
        public float FloorY { get; private set; }
        /// <summary>What it is aimed at, for the office's answer: "2층 맞이방 개집표 앞".</summary>
        public string View { get; private set; } = "";

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Range = Equipment.Number("range", 20f);
            Fov = Equipment.Number("fov", 360f);
            FloorY = Equipment.Number("floor", transform.position.y - 3.5f);
            View = Equipment.Text("view");
        }

        /// <summary>Whether a person or a fire at <paramref name="point"/> (on the floor it watches) is in the picture.</summary>
        public bool Covers(Vector3 point)
        {
            if (Mathf.Abs(point.y - FloorY) > 2f) return false;
            var d = point - transform.position;
            d.y = 0;
            if (d.magnitude > Range) return false;
            if (Fov >= 359f) return true;
            var aim = transform.forward;
            aim.y = 0;
            return aim.sqrMagnitude < 1e-4f || Vector3.Angle(aim, d) <= Fov * .5f;
        }
    }
}
