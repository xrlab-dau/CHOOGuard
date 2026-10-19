using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.Circulation
{
    /// <summary>
    /// Animates the repeated visible treads through their retained source positions.
    /// End fragments are clipped to the original exposed footprint. No hidden return route is modelled.
    /// The existing EscalatorMotion supplies the same lane drive used by passenger transport and emergency controls.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EscalatorMotion))]
    public sealed class PlatformEscalatorRun : MonoBehaviour
    {
        public Transform[] Treads = Array.Empty<Transform>();
        [Tooltip("Actual native source origins, physically sorted high to low, in this component's local frame.")]
        public Vector3[] ObservedOrigins = Array.Empty<Vector3>();
        public bool Up;
        [Tooltip("Median distance between the retained source treads. This is source animation calibration, not a field measurement.")]
        public float SourcePitch = .4f;
        public Vector3 ExposureAxis = Vector3.forward;
        public float ExposureMinimum, ExposureMaximum;

        private sealed class Part
        {
            public Transform Transform;
            public MeshFilter Filter;
            public Mesh Original, Clipped;
            public Bounds Bounds;
            public Renderer Renderer;
        }
        private Part[] parts = Array.Empty<Part>();
        private Part boundary;
        private EscalatorMotion motion;
        private double boundDistance;
        private bool prepared;
        private bool sourceAbsent;
        public bool SourceAvailable { get; private set; }
        public bool IsBound => prepared;
        public double CurrentPhase { get; private set; }
        public int AnimatedOriginalPartCount => parts.Length;
        public bool HasInventedReturnRoute => false;

        private void Awake() { if (Application.isPlaying) Prepare(); }
        private void OnEnable() { if (Application.isPlaying) Prepare(); }
        private void LateUpdate() { if (!sourceAbsent) ApplyMotion(); }
        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();

        public void Prepare()
        {
            if (prepared || sourceAbsent) return;
            if (Treads == null || ObservedOrigins == null || Treads.Length < 2 || Treads.Length != ObservedOrigins.Length ||
                SourcePitch <= .001f || ExposureAxis.sqrMagnitude < .999f || ExposureMaximum <= ExposureMinimum)
                throw new InvalidOperationException("An exact observed tread run and exposed source footprint are required");
            // A retained controller can outlive all of its source treads. An entirely
            // empty source is unbound, not prepared motion. Partial loss still follows
            // the strict per-tread guards below; no geometry is reconstructed or guessed.
            bool hasReference = false;
            foreach (var tread in Treads) if (tread != null) { hasReference = true; break; }
            SourceAvailable = hasReference || GetComponentsInChildren<MeshFilter>(true).Length != 0 ||
                GetComponentsInChildren<Renderer>(true).Length != 0 || GetComponentsInChildren<Terrain>(true).Length != 0 ||
                GetComponentsInChildren<MeshCollider>(true).Length != 0;
            if (!SourceAvailable) { sourceAbsent = true; CurrentPhase = 0; return; }
            motion = GetComponent<EscalatorMotion>();
            boundDistance = motion != null && motion.Drive != null ? motion.Drive.TravelDistance : 0;
            parts = new Part[Treads.Length];
            for (int i = 0; i < Treads.Length; i++)
            {
                var t = Treads[i]; var filter = t == null ? null : t.GetComponent<MeshFilter>();
                var renderer = t == null ? null : t.GetComponent<Renderer>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable || renderer == null)
                    throw new InvalidOperationException("Native source tread geometry is missing at index " + i);
                parts[i] = new Part { Transform = t, Filter = filter, Original = filter.sharedMesh, Bounds = filter.sharedMesh.bounds, Renderer = renderer };
            }
            // This is a clipped repeated source surface at the entry, not a newly asserted physical mechanism or source identity.
            var source = parts[Up ? parts.Length - 1 : 0];
            var node = new GameObject("진입 디딤판 표면"); node.transform.SetParent(transform, false);
            node.transform.localRotation = source.Transform.localRotation; node.transform.localScale = source.Transform.localScale;
            var boundaryFilter = node.AddComponent<MeshFilter>(); boundaryFilter.sharedMesh = source.Original;
            var boundaryRenderer = node.AddComponent<MeshRenderer>(); boundaryRenderer.sharedMaterials = source.Renderer.sharedMaterials;
            boundaryRenderer.shadowCastingMode = source.Renderer.shadowCastingMode;
            boundaryRenderer.receiveShadows = source.Renderer.receiveShadows;
            boundaryRenderer.renderingLayerMask = source.Renderer.renderingLayerMask;
            boundary = new Part { Transform = node.transform, Filter = boundaryFilter, Original = source.Original, Bounds = source.Original.bounds, Renderer = boundaryRenderer };
            node.SetActive(false); prepared = true; CurrentPhase = 0;
        }

        public void ApplyMotion()
        {
            if (!prepared) Prepare();
            if (sourceAbsent) return;
            var drive = motion == null ? null : motion.Drive;
            if (drive == null) return;
            ApplyDistance(drive.TravelDistance - boundDistance);
        }

        /// <summary>Distance comes from the common lane drive; this function never advances time itself.</summary>
        public void ApplyDistance(double distance)
        {
            if (!prepared) Prepare();
            if (sourceAbsent) return;
            if (double.IsNaN(distance) || double.IsInfinity(distance)) return;
            double periods = distance / SourcePitch;
            double phase = periods - Math.Floor(periods);
            // Remove floating point residue at an exact repeated-tread boundary.
            if (phase < 1e-8 || 1 - phase < 1e-8) phase = 0;
            CurrentPhase = phase;
            float shift = (float)phase * (Up ? -1 : 1);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].Transform.localPosition = SampleObservedOrigin(ObservedOrigins, i + shift);
                if (phase == 0) parts[i].Filter.sharedMesh = parts[i].Original;
                else ClipToExposure(parts[i]);
            }
            boundary.Transform.gameObject.SetActive(phase != 0);
            if (phase == 0) return;
            boundary.Transform.localPosition = SampleObservedOrigin(ObservedOrigins, Up ? parts.Length + shift : -1 + shift);
            ClipToExposure(boundary);
        }

        /// <summary>Only adjacent observed origins are interpolated. At a visible boundary, the adjacent pitch is continued and clipped.</summary>
        public static Vector3 SampleObservedOrigin(Vector3[] origins, float index)
        {
            if (origins == null || origins.Length < 2 || float.IsNaN(index) || float.IsInfinity(index))
                throw new ArgumentException("Finite source index and at least two observed origins required");
            int segment = Mathf.Clamp(Mathf.FloorToInt(index), 0, origins.Length - 2);
            return Vector3.LerpUnclamped(origins[segment], origins[segment + 1], index - segment);
        }

        private void ClipToExposure(Part part)
        {
            // Project the retained native bounds once; only boundary fragments require per-vertex clipping.
            var matrix = transform.worldToLocalMatrix * part.Transform.localToWorldMatrix;
            float centre = Vector3.Dot(matrix.MultiplyPoint3x4(part.Bounds.center),ExposureAxis);
            var e = part.Bounds.extents;
            float extent = Mathf.Abs(Vector3.Dot(matrix.MultiplyVector(Vector3.right),ExposureAxis))*e.x +
                Mathf.Abs(Vector3.Dot(matrix.MultiplyVector(Vector3.up),ExposureAxis))*e.y +
                Mathf.Abs(Vector3.Dot(matrix.MultiplyVector(Vector3.forward),ExposureAxis))*e.z;
            float minimum = centre - extent, maximum = centre + extent;
            if (minimum >= ExposureMinimum - .00001f && maximum <= ExposureMaximum + .00001f)
            { part.Filter.sharedMesh = part.Original; return; }
            if (part.Clipped == null) part.Clipped = new Mesh { name = "노출 경계 디딤판", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            ClipMesh(part.Original, part.Transform, transform, ExposureAxis, ExposureMinimum, ExposureMaximum, part.Clipped);
            part.Filter.sharedMesh = part.Clipped;
        }

        public void Restore()
        {
            if (!prepared) { sourceAbsent = false; SourceAvailable = false; return; }
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Transform != null) part.Transform.localPosition = ObservedOrigins[i];
                if (part.Filter != null) part.Filter.sharedMesh = part.Original;
                DisposeMesh(part.Clipped);
            }
            if (boundary != null)
            {
                DisposeMesh(boundary.Clipped);
                if (boundary.Transform != null)
                {
                    // Destroy is deferred in Play Mode; hide this old entry surface before a same-frame re-enable creates its replacement.
                    boundary.Transform.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(boundary.Transform.gameObject); else DestroyImmediate(boundary.Transform.gameObject);
                }
            }
            parts = Array.Empty<Part>(); boundary = null; prepared = false; sourceAbsent = false; SourceAvailable = false; CurrentPhase = 0;
        }
        private static void DisposeMesh(Mesh mesh) { if (mesh == null) return; if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }

        private struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector4 Tangent;
            public Color Colour;
            public Vector4[] Uv;
            public float Along;
            public static Vertex Lerp(Vertex a, Vertex b, float f)
            {
                var v = new Vertex { Position = Vector3.LerpUnclamped(a.Position,b.Position,f),
                    Normal = Vector3.LerpUnclamped(a.Normal,b.Normal,f), Tangent = Vector4.LerpUnclamped(a.Tangent,b.Tangent,f),
                    Colour = Color.LerpUnclamped(a.Colour,b.Colour,f), Along = Mathf.LerpUnclamped(a.Along,b.Along,f), Uv = new Vector4[8] };
                for (int c = 0; c < 8; c++) v.Uv[c] = Vector4.LerpUnclamped(a.Uv[c],b.Uv[c],f);
                return v;
            }
        }
        private static List<Vertex> Clip(List<Vertex> input, float boundary, bool minimum)
        {
            var output = new List<Vertex>(); if (input.Count == 0) return output;
            var a = input[input.Count - 1]; bool aInside = minimum ? a.Along >= boundary : a.Along <= boundary;
            foreach (var b in input)
            {
                bool bInside = minimum ? b.Along >= boundary : b.Along <= boundary;
                if (aInside != bInside) output.Add(Vertex.Lerp(a,b,(boundary-a.Along)/(b.Along-a.Along)));
                if (bInside) output.Add(b);
                a = b; aInside = bInside;
            }
            return output;
        }

        /// <summary>Clip the actual source surface and all its supplied vertex attributes; never close the clipped faces.</summary>
        public static void ClipMesh(Mesh original, Transform frame, Transform run, Vector3 axis, float minimum, float maximum, Mesh result)
        {
            if (original == null || result == null || original == result || !original.isReadable || maximum <= minimum)
                throw new ArgumentException("Distinct readable source/result meshes and ordered exposure bounds required");
            var positions = original.vertices; var normals = original.normals; var tangents = original.tangents; var colours = original.colors;
            var uv = new List<Vector4>[8];
            for (int c = 0; c < 8; c++) { uv[c] = new List<Vector4>(); original.GetUVs(c,uv[c]); }
            var all = new Vertex[positions.Length];
            for (int i = 0; i < all.Length; i++)
            {
                var v = new Vertex { Position = positions[i], Normal = normals.Length == all.Length ? normals[i] : Vector3.zero,
                    Tangent = tangents.Length == all.Length ? tangents[i] : Vector4.zero, Colour = colours.Length == all.Length ? colours[i] : Color.white,
                    Uv = new Vector4[8], Along = Vector3.Dot(run.InverseTransformPoint(frame.TransformPoint(positions[i])),axis) };
                for (int c = 0; c < 8; c++) if (uv[c].Count == all.Length) v.Uv[c] = uv[c][i];
                all[i] = v;
            }
            var output = new List<Vertex>(); var indices = new List<int>[original.subMeshCount];
            for (int s = 0; s < indices.Length; s++)
            {
                indices[s] = new List<int>(); var source = original.GetTriangles(s);
                for (int t = 0; t < source.Length; t += 3)
                {
                    var polygon = new List<Vertex> { all[source[t]], all[source[t+1]], all[source[t+2]] };
                    polygon = Clip(Clip(polygon,minimum,true),maximum,false);
                    for (int k = 1; k + 1 < polygon.Count; k++)
                    {
                        // Repeated triangles are retained only when they have positive surface area.
                        if (Vector3.Cross(polygon[k].Position-polygon[0].Position,polygon[k+1].Position-polygon[0].Position).sqrMagnitude < 1e-16f) continue;
                        int start = output.Count; output.Add(polygon[0]); output.Add(polygon[k]); output.Add(polygon[k+1]);
                        indices[s].Add(start); indices[s].Add(start+1); indices[s].Add(start+2);
                    }
                }
            }
            result.Clear(); result.SetVertices(output.ConvertAll(v => v.Position));
            if (normals.Length == positions.Length) result.SetNormals(output.ConvertAll(v => v.Normal));
            if (tangents.Length == positions.Length) result.SetTangents(output.ConvertAll(v => v.Tangent));
            if (colours.Length == positions.Length) result.SetColors(output.ConvertAll(v => v.Colour));
            for (int c = 0; c < 8; c++) if (uv[c].Count == positions.Length) { int channel = c; result.SetUVs(c,output.ConvertAll(v => v.Uv[channel])); }
            result.subMeshCount = indices.Length;
            for (int s = 0; s < indices.Length; s++) result.SetTriangles(indices[s],s,false);
            result.RecalculateBounds();
        }
    }
}
