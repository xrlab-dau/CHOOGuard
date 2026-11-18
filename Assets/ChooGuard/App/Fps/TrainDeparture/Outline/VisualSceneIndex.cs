// Spatial module: complete Nition BoundsOctree sources in ThirdParty (BSD-2-Clause).
// Narrowphase: Godot production kernel in VisualTriangle (MIT). Bounds never admit a visual hit.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using ChooGuard.App.Fps.TrainDeparture.Spatial;
namespace ChooGuard.App.Fps.TrainDeparture
{
    public struct VisualAimHit
    {
        public Renderer Renderer;
        public Vector3 Point;
        public float Distance;
        public int Submesh, Triangle;
    }
    public sealed class VisualSceneIndex
    {
        internal sealed class Record
        {
            public Renderer Renderer;
            public VisualMeshData Mesh;
            public Mesh SourceMesh;
            public bool StaticBatchSource;
            public Bounds SourceBounds;
            public Bounds WorldBounds => StaticBatchSource ? TransformBounds(Renderer.transform.localToWorldMatrix, SourceBounds) : Renderer.bounds;
            public int LodGroup = -1, LodMask;
            public VisualSurface[] Surfaces;
            public Bounds IndexBounds;
            public int IndexKind;
        }
        struct Candidate { public int Id; public float Near; }
        sealed class CandidateOrder : IComparer<Candidate>
        {
            public int Compare(Candidate a, Candidate b) { int c = a.Near.CompareTo(b.Near); return c != 0 ? c : a.Id.CompareTo(b.Id); }
        }
        readonly List<Record> records = new List<Record>();
        readonly List<int> dynamicIds = new List<int>(), candidates = new List<int>();
        readonly List<Candidate> ordered = new List<Candidate>();
        readonly CandidateOrder order = new CandidateOrder();
        readonly Dictionary<Mesh, VisualMeshData> meshes = new Dictionary<Mesh, VisualMeshData>();
        readonly List<VisualTriangleRef> triangleCandidates = new List<VisualTriangleRef>();
        int[] traversalStack = new int[64];
        sealed class LodState { public LODGroup Group; public float[] Heights; public int Selected; }
        readonly List<LodState> lods = new List<LodState>();
        readonly Dictionary<Material, VisualSurface> surfaces = new Dictionary<Material, VisualSurface>();
        readonly List<string> diagnostics = new List<string>();
        BoundsOctree<int> station, train;
        Transform trainRoot;
        public int RendererCount => records.Count;
        public int StaticCount { get; private set; }
        public int TrainCount { get; private set; }
        public int DynamicCount => dynamicIds.Count;
        public int UnsupportedCount { get; private set; }
        public int LastCandidates { get; private set; }
        public long LastTriangles { get; private set; }
        public long LastBvhNodes { get; private set; }
        public int CachedMeshes { get; private set; }
        public int RuntimeBuiltMeshes { get; private set; }
        public int EmptyMeshesSkipped { get; private set; }
        public int StaticBatchRecovered { get; private set; }
        public int StaticBatchMissing { get; private set; }
        public long GeometryBytes { get; private set; }
        public double InitializationMs { get; private set; }
        public double LastQueryMs { get; private set; }
        public IReadOnlyList<string> Diagnostics => diagnostics;
        internal Record SelectedRecord { get; private set; }
        public void Build(Renderer[] renderers, Transform root, HashSet<Transform> moving, VisualAlphaCache[] alpha, VisualGeometryCatalog catalog = null, VisualSourceBinding[] sourceBindings = null)
        {
            long start = Stopwatch.GetTimestamp();
            records.Clear(); meshes.Clear(); surfaces.Clear(); diagnostics.Clear(); dynamicIds.Clear();
            GeometryBytes = 0; UnsupportedCount = StaticCount = TrainCount = 0; trainRoot = root; SelectedRecord = null;
            CachedMeshes = RuntimeBuiltMeshes = EmptyMeshesSkipped = StaticBatchRecovered = StaticBatchMissing = 0;
            var originals = new Dictionary<Renderer, Mesh>();
            if (sourceBindings != null) foreach (var binding in sourceBindings)
                if (binding != null && binding.Renderer != null && binding.Source != null) originals.Add(binding.Renderer, binding.Source);
            var cached = new Dictionary<Mesh, VisualGeometryCache>();
            if (catalog != null) foreach (var cache in catalog.Caches)
                if (cache != null && cache.Source != null) cached[cache.Source] = cache;
            lods.Clear(); var memberships = new Dictionary<Renderer, KeyValuePair<int,int>>();
            foreach (var group in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var levels = group.GetLODs(); var state = new LodState { Group=group, Heights=new float[levels.Length] };
                int groupId=lods.Count; lods.Add(state);
                if (group.fadeMode != LODFadeMode.None) diagnostics.Add(group.name + ": unsupported LOD crossfade");
                for (int l=0;l<levels.Length;l++)
                {
                    state.Heights[l]=levels[l].screenRelativeTransitionHeight;
                    foreach (var member in levels[l].renderers) if (member != null)
                    {
                        int mask=1<<l;
                        if(memberships.TryGetValue(member,out var prior) && prior.Key==groupId)mask|=prior.Value;
                        memberships[member]=new KeyValuePair<int,int>(groupId,mask);
                    }
                }
            }
            var seen = new HashSet<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null || !seen.Add(renderer) || renderer.name.Contains("__KtxOpaqueTexelDepth")) continue;
                var materials = renderer.sharedMaterials;
                bool helper = false;
                foreach (var material in materials) if (material != null && material.shader != null && material.shader.name.Contains("OpaqueTexelDepth")) helper = true;
                if (helper) continue;
                var r = new Record { Renderer = renderer };
                var filter = renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>() : null;
                var mesh = filter != null ? filter.sharedMesh : null;
                if (renderer.isPartOfStaticBatch)
                {
                    // Combined mesh is in batch/world space and contains other renderers. NEVER inspect it as this object.
                    // Reuse MeshApiExamples' original mesh + object Transform pairing (MIT), not renderer shader matrix.
                    if (originals.TryGetValue(renderer, out var source) && cached.ContainsKey(source))
                    { mesh = source; r.StaticBatchSource = true; }
                    else { mesh = null; StaticBatchMissing++; diagnostics.Add(renderer.name + ": missing pre-batch source binding"); }
                }
                r.SourceMesh = mesh;
                if (mesh != null && mesh.vertexCount == 0) { EmptyMeshesSkipped++; continue; }
                if (memberships.TryGetValue(renderer,out var membership)) { r.LodGroup=membership.Key; r.LodMask=membership.Value; }
                if (mesh != null && mesh.subMeshCount > 0 && !renderer.HasPropertyBlock())
                {
                    if (!meshes.TryGetValue(mesh, out r.Mesh))
                    {
                        if (cached.TryGetValue(mesh,out var cache))
                        {
                            if (cache.Matches(mesh)) {r.Mesh=cache.Data;CachedMeshes++;}
                            else diagnostics.Add(mesh.name + ": stale CPU geometry cache; bake again");
                        }
                        else if (mesh.isReadable)
                        {
                            var g = new VisualMeshData {Vertices=mesh.vertices,Normals=mesh.normals,Uv=mesh.uv,Submeshes=new VisualSubmesh[mesh.subMeshCount]};
                            bool supported=true;
                            for(int i=0;i<g.Submeshes.Length;i++)
                            {
                                if(mesh.GetTopology(i)!=MeshTopology.Triangles){supported=false;break;}
                                g.Submeshes[i]=new VisualSubmesh {Indices=mesh.GetTriangles(i,true)};
                            }
                            if(supported){g.Validate();g.Bvh=VisualTriangleBvh.Build(g.Vertices,g.Submeshes);r.Mesh=g;RuntimeBuiltMeshes++;}
                        }
                        meshes.Add(mesh,r.Mesh);
                        if(r.Mesh!=null)
                        {
                            GeometryBytes+=r.Mesh.PayloadBytes;
                            triangleCandidates.Capacity=Math.Max(triangleCandidates.Capacity,r.Mesh.Bvh.Triangles.Length);
                            if(traversalStack.Length<r.Mesh.Bvh.Depth+1)traversalStack=new int[r.Mesh.Bvh.Depth+1];
                        }
                    }
                }
                if (r.StaticBatchSource && r.Mesh == null) { r.StaticBatchSource = false; StaticBatchMissing++; }
                if (r.StaticBatchSource && r.Mesh != null)
                {
                    // BVH root is source-local triangle bounds, not the combined Renderer.bounds.
                    var nodes = r.Mesh.Bvh.Nodes;
                    if (nodes.Length > 0) { var n = nodes[nodes.Length - 1]; r.SourceBounds.SetMinMax(n.Min, n.Max); }
                    StaticBatchRecovered++;
                }
                if (r.Mesh != null && materials.Length > 0)
                {
                    // Unity draws extra slots with the final submesh. Missing slots are unsupported to avoid guessed coverage.
                    if (materials.Length < r.Mesh.Submeshes.Length) r.Mesh = null;
                    else
                    {
                        r.Surfaces = new VisualSurface[materials.Length];
                        for (int i = 0; i < materials.Length; i++)
                        {
                            var m = materials[i]; VisualSurface s;
                            if (m == null) s = VisualSurface.Capture(null, alpha);
                            else if (!surfaces.TryGetValue(m, out s)) { s = VisualSurface.Capture(m, alpha); surfaces.Add(m, s); }
                            r.Surfaces[i] = s;
                            if (!s.Supported) diagnostics.Add(renderer.name + ": " + s.Diagnostic);
                        }
                    }
                }
                else r.Mesh = null;
                bool unsupported = r.Mesh == null;
                if (r.Surfaces != null) foreach (var s in r.Surfaces) unsupported |= !s.Supported;
                if (unsupported) { UnsupportedCount++; if (r.Mesh == null) diagnostics.Add(renderer.name + ": unreadable/non-triangle/non-MeshRenderer/property-block/missing material geometry"); }
                bool dynamic = !(renderer is MeshRenderer);
                for (Transform t = renderer.transform; t != null; t = t.parent)
                {
                    if (moving.Contains(t) || t.GetComponent<Animator>() != null || (t != root && t.GetComponent<Rigidbody>() != null)) dynamic = true;
                }
                r.IndexKind = dynamic ? 2 : root != null && renderer.transform.IsChildOf(root) ? 1 : 0;
                r.IndexBounds = r.IndexKind == 1 ? TransformBounds(root.worldToLocalMatrix, r.WorldBounds) : r.WorldBounds;
                int id = records.Count; records.Add(r);
                if (dynamic) dynamicIds.Add(id);
                else if (r.IndexKind == 1) TrainCount++; else StaticCount++;
            }
            station = BuildTree(0, StaticCount); train = BuildTree(1, TrainCount);
            candidates.Capacity = Math.Max(candidates.Capacity, records.Count);
            ordered.Capacity = Math.Max(ordered.Capacity, records.Count);
            InitializationMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
        BoundsOctree<int> BuildTree(int kind, int count)
        {
            Bounds all = default; bool first = true;
            foreach (var r in records) if (r.IndexKind == kind) { if (first) { all = r.IndexBounds; first = false; } else all.Encapsulate(r.IndexBounds); }
            float size = Mathf.Max(2, Mathf.Max(all.size.x, Mathf.Max(all.size.y, all.size.z)) + 2);
            var tree = new BoundsOctree<int>(size, all.center, 1, 1.25f);
            for (int i = 0; i < records.Count; i++) if (records[i].IndexKind == kind) tree.Add(i, records[i].IndexBounds);
            if (tree.Count != count) throw new InvalidOperationException("Visual index coverage mismatch: " + tree.Count + "/" + count);
            return tree;
        }
        public static Bounds TransformBounds(Matrix4x4 matrix, Bounds b)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(b.min), Vector3.zero);
            for (int i = 1; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            return result;
        }
        public bool Resolve(Camera camera, out VisualAimHit hit)
        {
            hit = default; SelectedRecord = null; LastTriangles = 0; LastCandidates = 0; LastBvhNodes = 0;
            if (camera == null || station == null) return false;
            long start = Stopwatch.GetTimestamp();
            foreach(var lod in lods)
            {
                var group=lod.Group;
                if(group==null || !group.enabled || group.fadeMode!=LODFadeMode.None){lod.Selected=-2;continue;}
                lod.Selected=VisualLodSelection.Select(lod.Heights,VisualLodSelection.WorldSize(group),
                    Vector3.Distance(group.transform.TransformPoint(group.localReferencePoint),camera.transform.position),
                    camera.fieldOfView,camera.orthographic,camera.orthographicSize,QualitySettings.lodBias,QualitySettings.maximumLODLevel);
            }
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            float best = camera.farClipPlane, uncertain = float.PositiveInfinity;
            candidates.Clear(); ordered.Clear(); station.GetColliding(candidates, ray, best);
            if (trainRoot != null)
            {
                var m = trainRoot.worldToLocalMatrix; var d = m.MultiplyVector(ray.direction);
                train.GetColliding(candidates, new Ray(m.MultiplyPoint3x4(ray.origin), d), best * d.magnitude);
            }
            foreach (int id in dynamicIds) candidates.Add(id);
            foreach (int id in candidates)
            {
                var r = records[id]; var renderer = r.Renderer;
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || !renderer.gameObject.activeInHierarchy ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                if(r.LodGroup>=0)
                {
                    int level=lods[r.LodGroup].Selected;
                    if(level==-1 || level>=0 && (r.LodMask & (1<<level))==0)continue;
                }
                if (r.WorldBounds.IntersectRay(ray, out float near) && near <= best) ordered.Add(new Candidate { Id = id, Near = near });
            }
            ordered.Sort(order); LastCandidates = ordered.Count;
            int previous = -1;
            foreach (var c in ordered)
            {
                if (c.Id == previous) continue; previous = c.Id;
                if (c.Near > best) break;
                var r = records[c.Id];
                if (r.Mesh == null || r.LodGroup>=0 && lods[r.LodGroup].Selected==-2) { uncertain = Mathf.Min(uncertain, c.Near); continue; }
                var worldToLocal = r.Renderer.transform.worldToLocalMatrix;
                var origin = worldToLocal.MultiplyPoint3x4(ray.origin);
                var direction = worldToLocal.MultiplyVector(ray.direction); // NEVER normalize: t remains world distance.
                var geometry = r.Mesh;
                LastBvhNodes += geometry.Bvh.Collect(origin,direction,best,triangleCandidates,traversalStack);
                float determinantSign=Mathf.Sign(r.Renderer.transform.localToWorldMatrix.determinant);
                Matrix4x4 localToWorld=r.Renderer.transform.localToWorldMatrix;
                Matrix4x4 normalToWorld=worldToLocal.transpose;
                foreach(var triangle in triangleCandidates)
                {
                    int sub=triangle.Submesh,j=triangle.Offset;
                    var indices=geometry.Submeshes[sub].Indices;
                    LastTriangles++;
                    int a=indices[j],b=indices[j+1],cidx=indices[j+2];
                    if(!VisualTriangle.Hit(origin,direction,geometry.Vertices[a],geometry.Vertices[b],geometry.Vertices[cidx],
                        out float t,out float u,out float v,out float det)||t<camera.nearClipPlane||t>=best)continue;
                    // Additional material slots render the final submesh. Test geometry once, then each contributing material.
                    int lastSlot=sub==geometry.Submeshes.Length-1?r.Surfaces.Length-1:sub;
                    for(int slot=sub;slot<=lastSlot;slot++)
                    {
                        var surface=r.Surfaces[slot];float facing=det*determinantSign;
                        if(surface.Cull==2&&facing<=0||surface.Cull==1&&facing>=0)continue;
                        if(!surface.Supported || surface.NeedsAlpha && surface.SampleTextureAlpha && surface.Texture != null &&
                            (surface.BoxMap?geometry.Normals.Length!=geometry.Vertices.Length:geometry.Uv.Length!=geometry.Vertices.Length))
                        {uncertain=Mathf.Min(uncertain,t);continue;}
                        Vector2 uv=Vector2.zero;
                        if(surface.NeedsAlpha && surface.SampleTextureAlpha && surface.Texture != null)
                        {
                            if(surface.BoxMap)
                                uv=VisualSurface.BoxUv(localToWorld.MultiplyPoint3x4(geometry.Vertices[a]),normalToWorld.MultiplyVector(geometry.Normals[a]))*(1-u-v)+
                                   VisualSurface.BoxUv(localToWorld.MultiplyPoint3x4(geometry.Vertices[b]),normalToWorld.MultiplyVector(geometry.Normals[b]))*u+
                                   VisualSurface.BoxUv(localToWorld.MultiplyPoint3x4(geometry.Vertices[cidx]),normalToWorld.MultiplyVector(geometry.Normals[cidx]))*v;
                            else uv=geometry.Uv[a]*(1-u-v)+geometry.Uv[b]*u+geometry.Uv[cidx]*v;
                        }
                        if(!surface.Accept(uv)){if(!surface.Supported)uncertain=Mathf.Min(uncertain,t);continue;}
                        best=t;SelectedRecord=r;
                        hit=new VisualAimHit {Renderer=r.Renderer,Point=ray.GetPoint(t),Distance=t,Submesh=sub,Triangle=j/3};
                    }
                }
            }
            LastQueryMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            if (uncertain <= best) { hit = default; SelectedRecord = null; }
            return hit.Renderer != null;
        }
    }
}
