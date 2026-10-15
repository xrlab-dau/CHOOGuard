using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.Circulation
{
    /// <summary>
    /// Animates supplied step/handrail parts along authored closed loops and supplied handrail UVs.
    /// Paths and parts must come from the real model; this component creates neither parts nor return paths.
    /// One accumulated drive distance controls every loop and survives stop/restart.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EscalatorMotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Loop
        {
            public Transform[] Parts = Array.Empty<Transform>();
            [Tooltip("Closed route in this component's local coordinates, including the observed/modelled return route.")]
            public Vector3[] Path = Array.Empty<Vector3>();
            [Tooltip("Keep false for horizontal step treads; enable only for parts whose orientation follows their route.")]
            public bool FollowDirection;
        }

        [Serializable]
        public sealed class Surface
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public string TextureTransformProperty = "_BaseMap_ST";
            [Tooltip("UV displacement for one metre of belt travel, measured from the supplied handrail texture.")]
            public Vector2 UVPerMetre;
        }

        [Serializable] public sealed class StepSurface
        {
            public Renderer Renderer;
            public int MaterialIndex;
        }

        public Loop[] Loops = Array.Empty<Loop>();
        public Surface[] Surfaces = Array.Empty<Surface>();
        public StepSurface[] StepSurfaces = Array.Empty<StepSurface>();
        [SerializeField] private MonoBehaviour driveSource;
        private double boundDistance;
        private readonly List<LoopState> loops = new List<LoopState>();
        private readonly List<SurfaceState> surfaces = new List<SurfaceState>();
        private MaterialPropertyBlock properties;
        private readonly List<StepSurface> stepSurfaces = new List<StepSurface>();

        private sealed class LoopState
        {
            public Loop Binding;
            public float[] Distances, Offsets;
            public Quaternion[] RotationOffsets;
            public float Length;
        }

        private sealed class SurfaceState
        {
            public Surface Binding;
            public int Property;
            public Vector4 Rest;
        }

        public IEscalatorDrive Drive => driveSource != null ? driveSource as IEscalatorDrive : null;
        public int AnimatedPartCount { get; private set; }
        public int AnimatedSurfaceCount => surfaces.Count + stepSurfaces.Count;

        public void Bind(IEscalatorDrive source)
        {
            if (source != null && !(source is MonoBehaviour))
                throw new ArgumentException("The drive source must be a Unity component", nameof(source));
            if (ReferenceEquals(Drive, source)) return;
            driveSource = source as MonoBehaviour;
            RebuildBindings();
        }

        private void Awake() => RebuildBindings();
        private void LateUpdate() => ApplyMotion();

        /// <summary>Capture supplied current part positions after changing the authored bindings.</summary>
        public void RebuildBindings()
        {
            properties ??= new MaterialPropertyBlock();
            boundDistance = Drive?.TravelDistance ?? 0;
            loops.Clear(); surfaces.Clear(); stepSurfaces.Clear(); AnimatedPartCount = 0;
            foreach (var binding in Loops ?? Array.Empty<Loop>())
            {
                if (binding == null || binding.Path == null || binding.Path.Length < 2 || binding.Parts == null) continue;
                var state = new LoopState { Binding = binding, Distances = new float[binding.Path.Length + 1],
                    Offsets = new float[binding.Parts.Length], RotationOffsets = new Quaternion[binding.Parts.Length] };
                for (int i = 0; i < binding.Path.Length; i++)
                    state.Distances[i + 1] = state.Distances[i] + Vector3.Distance(transform.TransformPoint(binding.Path[i]), transform.TransformPoint(binding.Path[(i + 1) % binding.Path.Length]));
                state.Length = state.Distances[state.Distances.Length - 1];
                if (state.Length <= 1e-5f) continue;
                for (int i = 0; i < binding.Parts.Length; i++)
                {
                    var part = binding.Parts[i];
                    if (part == null) continue;
                    state.Offsets[i] = Project(state, part.position);
                    PointAt(state, state.Offsets[i], out var tangent);
                    state.RotationOffsets[i] = Quaternion.Inverse(Direction(tangent)) * part.rotation;
                    AnimatedPartCount++;
                }
                loops.Add(state);
            }
            foreach (var binding in Surfaces ?? Array.Empty<Surface>())
            {
                if (binding == null || binding.Renderer == null || string.IsNullOrEmpty(binding.TextureTransformProperty)) continue;
                var materials = binding.Renderer.sharedMaterials;
                if (binding.MaterialIndex < 0 || binding.MaterialIndex >= materials.Length) continue;
                var material = materials[binding.MaterialIndex];
                int property = Shader.PropertyToID(binding.TextureTransformProperty);
                if (material == null || !material.HasProperty(property)) continue;
                binding.Renderer.GetPropertyBlock(properties, binding.MaterialIndex);
                var rest = properties.HasProperty(property) ? properties.GetVector(property) : material.GetVector(property);
                surfaces.Add(new SurfaceState { Binding = binding, Property = property, Rest = rest });
            }
            foreach (var binding in StepSurfaces ?? Array.Empty<StepSurface>())
            {
                if (binding == null || binding.Renderer == null) continue;
                var materials = binding.Renderer.sharedMaterials;
                if (binding.MaterialIndex < 0 || binding.MaterialIndex >= materials.Length) continue;
                var material = materials[binding.MaterialIndex];
                if (material == null || !material.HasProperty("_DriveBound") || !material.HasProperty("_TravelMetres")) continue;
                stepSurfaces.Add(binding);
            }
        }

        /// <summary>Apply the current common phase. Does not advance time or consume world randomness.</summary>
        public void ApplyMotion()
        {
            var drive = Drive;
            if (drive == null) return;
            double travelled = drive.TravelDistance - boundDistance;
            foreach (var state in loops)
                for (int i = 0; i < state.Binding.Parts.Length; i++)
                {
                    var part = state.Binding.Parts[i];
                    if (part == null) continue;
                    float distance = (float)((state.Offsets[i] + travelled) % state.Length);
                    if (distance < 0) distance += state.Length;
                    part.position = transform.TransformPoint(PointAt(state, distance, out var tangent));
                    if (state.Binding.FollowDirection) part.rotation = Direction(tangent) * state.RotationOffsets[i];
                }
            foreach (var surface in surfaces)
            {
                var binding = surface.Binding;
                if (binding.Renderer == null) continue;
                var value = surface.Rest;
                value.z += (float)((travelled * binding.UVPerMetre.x) % 1d);
                value.w += (float)((travelled * binding.UVPerMetre.y) % 1d);
                binding.Renderer.GetPropertyBlock(properties, binding.MaterialIndex);
                properties.SetVector(surface.Property, value);
                binding.Renderer.SetPropertyBlock(properties, binding.MaterialIndex);
            }
            foreach (var surface in stepSurfaces)
            {
                if (surface.Renderer == null) continue;
                surface.Renderer.GetPropertyBlock(properties, surface.MaterialIndex);
                properties.SetFloat("_DriveBound", 1f);
                properties.SetFloat("_TravelMetres", (float)drive.TravelDistance);
                surface.Renderer.SetPropertyBlock(properties, surface.MaterialIndex);
            }
        }

        private Quaternion Direction(Vector3 tangent)
        {
            var forward = transform.TransformVector(tangent).normalized;
            var up = Mathf.Abs(Vector3.Dot(forward, transform.up)) > .99f ? transform.right : transform.up;
            return Quaternion.LookRotation(forward, up);
        }

        private static Vector3 PointAt(LoopState state, float distance, out Vector3 tangent)
        {
            var path = state.Binding.Path;
            for (int i = 0; i < path.Length; i++)
            {
                float length = state.Distances[i + 1] - state.Distances[i];
                if (length <= 1e-5f || distance > state.Distances[i + 1]) continue;
                tangent = path[(i + 1) % path.Length] - path[i];
                return Vector3.Lerp(path[i], path[(i + 1) % path.Length], (distance - state.Distances[i]) / length);
            }
            tangent = Vector3.forward;
            return path[0];
        }

        private float Project(LoopState state, Vector3 point)
        {
            float closest = float.PositiveInfinity, result = 0;
            var path = state.Binding.Path;
            for (int i = 0; i < path.Length; i++)
            {
                var start = transform.TransformPoint(path[i]);
                var segment = transform.TransformPoint(path[(i + 1) % path.Length]) - start;
                float along = segment.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude) : 0;
                float error = (point - start - segment * along).sqrMagnitude;
                if (error >= closest) continue;
                closest = error;
                result = state.Distances[i] + segment.magnitude * along;
            }
            return result;
        }
    }
}
