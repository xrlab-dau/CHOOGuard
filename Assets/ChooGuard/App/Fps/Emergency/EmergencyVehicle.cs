using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A parked emergency vehicle with its warning lights on: the beacon materials (named Beacon_Red / Beacon_Blue by the
    /// builder) flash in turn four times a second and two small coloured lights over the roof flash with them, so the
    /// vehicle reads from across the square. Built as a prefab by ChooGuard.Editor.EmergencySceneBuilder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EmergencyVehicle : MonoBehaviour
    {
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private const float Half = .25f;

        private readonly List<(Material Material, Color Colour, bool Red)> beacons = new List<(Material, Color, bool)>();
        private Light red, blue;
        private bool redPhase;
        private float next;

        private void Start()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var shared = renderer.sharedMaterials;
                bool any = false;
                foreach (var m in shared) any |= m != null && m.name.StartsWith("Beacon_", System.StringComparison.Ordinal);
                if (!any) continue;
                var instances = renderer.materials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null || !shared[i].name.StartsWith("Beacon_", System.StringComparison.Ordinal)) continue;
                    bool isRed = shared[i].name.EndsWith("Red", System.StringComparison.Ordinal);
                    instances[i].EnableKeyword("_EMISSION");
                    beacons.Add((instances[i], isRed ? new Color(1f, .08f, .05f) : new Color(.1f, .25f, 1f), isRed));
                }
            }
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var renderer in GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            red = Lamp("경광등 적색", new Color(1f, .1f, .06f), bounds, -.4f);
            blue = Lamp("경광등 청색", new Color(.15f, .3f, 1f), bounds, .4f);
            Flash();
        }

        private Light Lamp(string name, Color colour, Bounds bounds, float side)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(bounds.center.x, bounds.max.y + .25f, bounds.center.z) + transform.right * side;
            var lamp = go.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = colour;
            lamp.range = 14;
            lamp.intensity = 0;
            lamp.shadows = LightShadows.None;
            return lamp;
        }

        private void Update()
        {
            if (Time.time >= next) Flash();
        }

        private void Flash()
        {
            next = Time.time + Half;
            redPhase = !redPhase;
            foreach (var (material, colour, isRed) in beacons) material.SetColor(Emission, isRed == redPhase ? colour * 6f : Color.black);
            if (red != null) red.intensity = redPhase ? 3f : 0;
            if (blue != null) blue.intensity = redPhase ? 0 : 3f;
        }
    }
}
