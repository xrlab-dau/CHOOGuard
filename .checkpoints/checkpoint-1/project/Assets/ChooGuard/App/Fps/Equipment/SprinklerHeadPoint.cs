using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A closed sprinkler head (a <see cref="StationEquipment"/> of kind <see cref="HeadKind"/>): a pendant head through a ceiling tile or an upright head on
    /// exposed pipe. It belongs to a protection zone, the <c>valve</c> of its alarm valve. When its glass bulb bursts (a fire heats it to the rated temperature,
    /// or an accident breaks it) the body goes, the bare frame shows and water sprays until the zone's valve is shut. The numbers come from the placement
    /// entry (<c>valve</c>, <c>floor</c>, <c>reach</c>, <c>temp</c>, <c>mount</c>); the builder derives them from NFTC 103.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class SprinklerHeadPoint : MonoBehaviour
    {
        public const string HeadKind = "sprinkler_head";

        public StationEquipment Equipment { get; private set; }
        /// <summary>Key of the protection zone (its alarm valve's key).</summary>
        public string Valve { get; private set; } = "";
        /// <summary>Id of the pipe it hangs from or sits on.</summary>
        public string Pipe { get; private set; } = "";
        /// <summary>Horizontal distance in metres it protects (NFTC 103 2.7.3.4: 2.3 for fire-resistant construction).</summary>
        public float Reach { get; private set; } = 2.3f;
        /// <summary>Rated temperature of the glass bulb in degrees Celsius (68, red).</summary>
        public float Rating { get; private set; } = 68f;
        public bool Upright { get; private set; }
        /// <summary>Height of the floor below it in metres above sea level of the twin (what the staff stands on).</summary>
        public float FloorY { get; private set; }
        public bool Activated { get; private set; }
        /// <summary>Water is coming out of it now (activated and its zone not shut).</summary>
        public bool Discharging { get; private set; }

        public Vector3 FloorPoint => new Vector3(transform.position.x, FloorY, transform.position.z);
        /// <summary>Height of the head above its floor.</summary>
        public float MountHeight => transform.position.y - FloorY;

        private Renderer[] intact = System.Array.Empty<Renderer>();
        private GameObject live;
        private ParticleSystem spray;
        private GameObject puddle;

        /// <summary>Reads its numbers from the placement data; the director calls it when the shift's fire safety starts.</summary>
        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Valve = Equipment.Text("valve");
            Pipe = Equipment.Text("pipe");
            Reach = Equipment.Number("reach", 2.3f);
            Rating = Equipment.Number("temp", 68f);
            FloorY = Equipment.Number("floor", transform.position.y - 3f);
            Upright = Equipment.Text("mount") == "upright";
            var body = transform.Find("Body");
            if (body != null) intact = body.GetComponentsInChildren<Renderer>(true);
            Equipment.KeepOff = renderer => Activated && System.Array.IndexOf(intact, renderer) >= 0;
            var found = transform.Find(EquipmentSpawner.LiveChild);
            live = found != null ? found.gameObject : null;
        }

        /// <summary>
        /// The bulb bursts: the intact body is hidden (a renderer switched off stays off inside a static batch), the bare frame under "Live" shows and, with
        /// <paramref name="water"/>, water sprays down in the pendant pattern and wets the floor below. Without it the caller supplies its own water (a leak hazard).
        /// </summary>
        public void Activate(Material water, bool wetFloor)
        {
            if (Activated) return;
            Activated = true;
            Discharging = true;
            Equipment.State = "동작 · 살수 중";
            foreach (var renderer in intact) if (renderer != null) renderer.enabled = false;
            if (live != null) live.SetActive(true);
            if (water == null) return;
            spray = MakeSpray(water);
            if (!wetFloor) return;
            puddle = Props.Puddle(transform, FloorPoint, water);
            puddle.transform.localScale = Vector3.one * (Reach * 1.6f);
        }

        /// <summary>The zone's valve is shut: the water stops (the head stays burst until it is replaced).</summary>
        public void Stop()
        {
            if (!Activated || !Discharging) return;
            Discharging = false;
            Equipment.State = "동작 · 급수 차단";
            if (spray != null) { var emission = spray.emission; emission.rateOverTime = 0; }
        }

        private ParticleSystem MakeSpray(Material water)
        {
            var go = new GameObject("살수");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Upright ? Vector3.up * .05f : Vector3.down * .07f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            float drop = Mathf.Max(1f, MountHeight - .1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.gravityModifier = 1f;
            main.startLifetime = Mathf.Clamp(drop / 3.5f, .6f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f);
            main.startColor = new Color(.78f, .88f, .97f, .55f);
            main.maxParticles = 500;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 58f;
            shape.radius = .015f;
            shape.rotation = Upright ? new Vector3(-90, 0, 0) : new Vector3(90, 0, 0);
            var emission = ps.emission;
            emission.rateOverTime = 320f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = water;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>The head is replaced: the body shows again, the spray and the wet floor go, the state is normal.</summary>
        public void Replace()
        {
            Activated = false;
            Discharging = false;
            foreach (var renderer in intact) if (renderer != null) renderer.enabled = true;
            if (live != null) live.SetActive(false);
            if (spray != null) Destroy(spray.gameObject);
            if (puddle != null) Destroy(puddle);
            spray = null;
            puddle = null;
            Equipment.State = "정상";
        }
    }
}
