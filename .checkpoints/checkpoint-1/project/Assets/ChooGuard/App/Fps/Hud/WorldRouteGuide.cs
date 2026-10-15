using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>Depth-tested floor marks for the next part of a verified incident route.</summary>
    public sealed class WorldRouteGuide : MonoBehaviour
    {
        private const float VisibleLength = 34f;
        private const float Interval = 1.25f;
        private readonly List<Vector3> route = new List<Vector3>();
        private readonly List<Vector3> backVertices = new List<Vector3>();
        private readonly List<Vector3> faceVertices = new List<Vector3>();
        private readonly List<int> backTriangles = new List<int>();
        private readonly List<int> faceTriangles = new List<int>();
        private Mesh backMesh, faceMesh;
        private MeshRenderer backRenderer, faceRenderer;
        private Material backMaterial, faceMaterial;

        public bool Visible => backRenderer != null && backRenderer.enabled;

        public static WorldRouteGuide Create(Transform parent)
        {
            var root = new GameObject("현장 이동 점선");
            root.transform.SetParent(parent, false);
            var guide = root.AddComponent<WorldRouteGuide>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[WorldRouteGuide] URP Unlit 셰이더를 찾지 못했습니다.");
                return guide;
            }
            guide.backMaterial = MakeMaterial(shader, "길 안내 외곽", new Color(.02f, .17f, .2f, .82f));
            guide.faceMaterial = MakeMaterial(shader, "길 안내 밝은 면", new Color(.56f, 1f, .92f, .94f));
            guide.faceMaterial.renderQueue = (int)RenderQueue.Transparent + 1;
            guide.backMesh = new Mesh { name = "길 안내 점선 외곽", hideFlags = HideFlags.DontSave };
            guide.faceMesh = new Mesh { name = "길 안내 점선 밝은 면", hideFlags = HideFlags.DontSave };
            guide.backMesh.MarkDynamic();
            guide.faceMesh.MarkDynamic();
            guide.backRenderer = MakeLayer(root.transform, "점선 외곽", guide.backMesh, guide.backMaterial);
            guide.faceRenderer = MakeLayer(root.transform, "점선과 화살표", guide.faceMesh, guide.faceMaterial);
            guide.Clear();
            return guide;
        }

        private static Material MakeMaterial(Shader shader, string name, Color color)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_Cull", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static MeshRenderer MakeLayer(Transform parent, string name, Mesh mesh, Material material)
        {
            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public void SetRoute(IReadOnlyList<Vector3> points)
        {
            route.Clear();
            if (points != null) for (int i = 0; i < points.Count; i++) route.Add(points[i]);
            Build();
        }

        public void Clear()
        {
            route.Clear();
            backMesh?.Clear();
            faceMesh?.Clear();
            if (backRenderer != null) backRenderer.enabled = false;
            if (faceRenderer != null) faceRenderer.enabled = false;
        }

        private void Build()
        {
            if (route.Count < 2 || backMesh == null || faceMesh == null) { Clear(); return; }
            backVertices.Clear();
            faceVertices.Clear();
            backTriangles.Clear();
            faceTriangles.Clear();
            float length = 0;
            for (int i = 1; i < route.Count; i++) length += Vector3.Distance(route[i - 1], route[i]);
            int mark = 0;
            for (float at = 1.4f; at < Mathf.Min(length - .25f, VisibleLength); at += Interval, mark++)
            {
                Vector3 forward = PointAt(Mathf.Min(at + .35f, length)) - PointAt(Mathf.Max(0, at - .35f));
                if (new Vector2(forward.x, forward.z).sqrMagnitude < .04f) continue;
                forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 center = PointAt(at);
                bool arrow = mark % 4 == 2;
                if (arrow)
                {
                    AddArrow(backVertices, backTriangles, center + Vector3.up * .09f, forward, right, .61f, .42f);
                    AddArrow(faceVertices, faceTriangles, center + Vector3.up * .1f, forward, right, .48f, .29f);
                }
                else
                {
                    AddDash(backVertices, backTriangles, center + Vector3.up * .09f, forward, right, .64f, .23f);
                    AddDash(faceVertices, faceTriangles, center + Vector3.up * .1f, forward, right, .51f, .12f);
                }
            }
            backMesh.Clear();
            faceMesh.Clear();
            backMesh.SetVertices(backVertices);
            backMesh.SetTriangles(backTriangles, 0);
            faceMesh.SetVertices(faceVertices);
            faceMesh.SetTriangles(faceTriangles, 0);
            backMesh.RecalculateBounds();
            faceMesh.RecalculateBounds();
            backRenderer.enabled = backVertices.Count > 0;
            faceRenderer.enabled = faceVertices.Count > 0;
        }

        private Vector3 PointAt(float distance)
        {
            for (int i = 1; i < route.Count; i++)
            {
                float length = Vector3.Distance(route[i - 1], route[i]);
                if (distance <= length || i == route.Count - 1)
                    return Vector3.Lerp(route[i - 1], route[i], length < .001f ? 0 : Mathf.Clamp01(distance / length));
                distance -= length;
            }
            return route[route.Count - 1];
        }

        private void AddDash(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 forward, Vector3 right, float length, float width)
        {
            int first = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(center - forward * (length * .5f) - right * (width * .5f)));
            vertices.Add(transform.InverseTransformPoint(center + forward * (length * .5f) - right * (width * .5f)));
            vertices.Add(transform.InverseTransformPoint(center + forward * (length * .5f) + right * (width * .5f)));
            vertices.Add(transform.InverseTransformPoint(center - forward * (length * .5f) + right * (width * .5f)));
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        private void AddArrow(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 forward, Vector3 right, float length, float width)
        {
            int first = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(center - forward * (length * .5f) - right * width));
            vertices.Add(transform.InverseTransformPoint(center + forward * (length * .5f)));
            vertices.Add(transform.InverseTransformPoint(center - forward * (length * .5f) + right * width));
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        }

        private void OnDestroy()
        {
            if (backMesh != null) Destroy(backMesh);
            if (faceMesh != null) Destroy(faceMesh);
            if (backMaterial != null) Destroy(backMaterial);
            if (faceMaterial != null) Destroy(faceMaterial);
        }
    }
}
