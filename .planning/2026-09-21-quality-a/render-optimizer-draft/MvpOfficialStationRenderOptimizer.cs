using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Editor
{
    // Draft: compile and native equivalence/performance review required before adoption.
    // Keeps original MeshFilters/meshes/colliders for existing picking and rollback.
    public static class MvpOfficialStationRenderOptimizer
    {
        public const string VisualPrefix = "Official material batches ";
        const string GeneratedFolder = "Assets/ChooGuard/Generated/OfficialStationRenderBatches";
        const float PositionTolerance = .0003f;
        const float BoundsTolerance = .001f;

        [Serializable] public sealed class SegmentReceipt
        {
            public string segment, segmentGlobalId, visualChild;
            public int sourceRenderers, sourceMaterialSlots, emptySubmeshes, nonemptySubmeshes;
            public int opaqueBatches, transparentRendererCopies, outputRenderers, outputMaterialSlots;
            public long sourceTriangles, outputTriangles;
            public Vector3 sourceMin, sourceMax, outputMin, outputMax;
            public bool channelsVerified, materialReferencesPreserved, originalFiltersAndCollidersPreserved;
            public string[] originalRendererGlobalIds;
            public bool[] originalEnabled;
        }
        [Serializable] public sealed class Receipt
        {
            public string status, generatedAssetFolder, validationSurface;
            public SegmentReceipt[] segments;
            public string transparentPolicy = "No cross-renderer transparent merge. Keep each original renderer's transparent submesh order and original bounds in one visual copy.";
            public string sourceTraversalPolicy = "Original MeshFilters remain unchanged. Future tools traversing segment descendants must skip IsOptimizedVisual; existing collider and picker references are retained.";
            public bool staticBatchingUsed, sceneSaved;
        }
        sealed class Channels
        {
            public Vector3[] positions, normals;
            public Vector4[] tangents;
            public Color[] colors;
            public readonly List<Vector4>[] uv = new List<Vector4>[8];
            public readonly int[] uvDimensions = new int[8];
            public string layout;
            public Channels(Mesh mesh)
            {
                positions=mesh.vertices; normals=mesh.normals; tangents=mesh.tangents; colors=mesh.colors;
                if(normals.Length!=0&&normals.Length!=positions.Length || tangents.Length!=0&&tangents.Length!=positions.Length || colors.Length!=0&&colors.Length!=positions.Length)
                    throw new InvalidDataException("Incomplete source vertex channel.");
                layout=(normals.Length>0?"N":"-")+(tangents.Length>0?"T":"-")+(colors.Length>0?"C":"-");
                for(int i=0;i<8;i++)
                {
                    uv[i]=new List<Vector4>();mesh.GetUVs(i,uv[i]);
                    if(uv[i].Count!=0&&uv[i].Count!=positions.Length)throw new InvalidDataException("Incomplete UV channel.");
                    uvDimensions[i]=uv[i].Count==0?0:mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+i));
                    if(uvDimensions[i]!=0&&(uvDimensions[i]<2||uvDimensions[i]>4))throw new InvalidDataException("Unsupported UV dimensionality; refusing implicit conversion.");
                    layout+="/"+uvDimensions[i];
                }
            }
        }
        sealed class Source
        {
            public MeshFilter filter; public MeshRenderer renderer; public Mesh mesh; public Material[] materials;
            public Channels channels; public Matrix4x4 matrix, normalMatrix; public string state;
            public bool mirrored, wasEnabled; public Bounds localBounds;
        }
        sealed class Draw { public Source source; public Material material; public int[] indices; public int submesh; }
        sealed class Span { public Draw draw; public int outputSubmesh, start; public int[] map; }
        sealed class Batch
        {
            public readonly List<Draw> draws=new List<Draw>(); public bool transparent;
            public Mesh mesh; public MeshRenderer renderer; public readonly List<Span> spans=new List<Span>();
        }
        sealed class SegmentWork
        {
            public Transform root; public GameObject stage; public Source[] sources;
            public MeshCollider[] colliders; public Mesh[] colliderMeshes;
            public readonly List<Batch> batches=new List<Batch>(); public SegmentReceipt receipt;
        }

        public static bool IsOptimizedVisual(Transform item)
        {
            for(var t=item;t!=null;t=t.parent)if(t.name.StartsWith(VisualPrefix,StringComparison.Ordinal))return true;
            return false;
        }

        // Does not save the scene. Caller owns native comparison, scene save and performance measurements.
        public static Receipt OptimizeExisting(Transform[] segmentRoots,string receiptPath)
        {
            RequireEditMode();
            if(segmentRoots==null||segmentRoots.Length==0||segmentRoots.Any(x=>x==null)||segmentRoots.Distinct().Count()!=segmentRoots.Length)
                throw new ArgumentException("Explicit distinct source segment roots required.");
            foreach(var a in segmentRoots)foreach(var b in segmentRoots)if(a!=b&&a.IsChildOf(b))throw new ArgumentException("Overlapping segment scopes.");
            if(string.IsNullOrWhiteSpace(receiptPath))throw new ArgumentException("Receipt path required.");
            if(File.Exists(receiptPath))throw new IOException("Use a fresh receipt path; prior receipts are never overwritten during optimization.");
            var work=new List<SegmentWork>();string runFolder=GeneratedFolder+"/run-"+Guid.NewGuid().ToString("N");
            var generated=new List<Mesh>();
            try
            {
                // Preflight every source before generating assets or disabling any renderer.
                foreach(var root in segmentRoots)work.Add(Inspect(root));
                EnsureFolder(runFolder);
                foreach(var segment in work)
                {
                    segment.stage=new GameObject(VisualPrefix+Guid.NewGuid().ToString("N"));
                    segment.stage.transform.SetParent(segment.root,false);segment.stage.SetActive(false);
                    segment.receipt.visualChild=segment.stage.name;
                    for(int i=0;i<segment.batches.Count;i++)
                    {
                        var batch=segment.batches[i];var mesh=Build(batch);generated.Add(mesh);
                        string path=runFolder+"/s"+work.IndexOf(segment)+"-b"+i+".asset";
                        batch.mesh=MvpMeshPersistence.Store(path,mesh); // Explicit channels + UploadMeshData(false).
                        if(!AssetDatabase.Contains(batch.mesh)||!batch.mesh.isReadable)throw new InvalidOperationException("Persistent readable batch required.");
                        var go=new GameObject(batch.transparent?"Transparent source renderer "+i:"Opaque material batch "+i,typeof(MeshFilter),typeof(MeshRenderer));
                        go.transform.SetParent(segment.stage.transform,false);go.layer=batch.draws[0].source.renderer.gameObject.layer;
                        go.isStatic=false;GameObjectUtility.SetStaticEditorFlags(go,0);
                        go.GetComponent<MeshFilter>().sharedMesh=batch.mesh;batch.renderer=go.GetComponent<MeshRenderer>();
                        batch.renderer.sharedMaterials=batch.transparent?batch.draws.Select(d=>d.material).ToArray():batch.draws.Select(d=>d.material).Distinct().ToArray();
                        CopyState(batch.draws[0].source.renderer,batch.renderer);
                        if(batch.transparent)batch.renderer.localBounds=TransformBounds(batch.draws[0].source.matrix,batch.draws[0].source.localBounds);
                        Validate(batch);
                    }
                    ValidateSegment(segment);
                }
                // One synchronous editor transaction: validated inactive children first, source renderers second.
                foreach(var segment in work)VerifyOriginalReferences(segment);
                foreach(var segment in work)
                {
                    foreach(var source in segment.sources)SetEnabled(source.renderer,false);
                    segment.stage.SetActive(true);
                }
                foreach(var segment in work)
                {
                    VerifyOriginalReferences(segment,false);
                    EditorSceneManager.MarkSceneDirty(segment.root.gameObject.scene);
                }
                var receipt=new Receipt{status="APPLIED_EQUIVALENT_RENDER_BATCHES_NATIVE_VISUAL_AND_PERFORMANCE_PENDING",generatedAssetFolder=runFolder,segments=work.Select(x=>x.receipt).ToArray(),validationSurface="Editor mesh-channel/index/material/bounds readback; not GPU rendering or FPS acceptance.",staticBatchingUsed=false,sceneSaved=false};
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(receiptPath)));
                File.WriteAllText(receiptPath,JsonUtility.ToJson(receipt,true));AssetDatabase.SaveAssets();return receipt;
            }
            catch
            {
                foreach(var segment in work)
                {
                    if(segment.stage!=null)segment.stage.SetActive(false);
                    if(segment.sources!=null)foreach(var source in segment.sources)if(source.renderer!=null)SetEnabled(source.renderer,source.wasEnabled);
                    if(segment.stage!=null)UnityEngine.Object.DestroyImmediate(segment.stage);
                }
                if(File.Exists(receiptPath))File.Delete(receiptPath); // This call required a fresh receipt path.
                if(AssetDatabase.IsValidFolder(runFolder))AssetDatabase.DeleteAsset(runFolder); // Only this run's new assets.
                foreach(var mesh in generated)if(mesh!=null&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        public static void RestoreFromReceipt(string receiptPath)
        {
            RequireEditMode();var r=JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
            if(r==null||r.segments==null)throw new InvalidDataException("Optimization receipt missing.");
            var stages=new List<GameObject>();var restore=new List<Tuple<MeshRenderer,bool>>();var roots=new List<Transform>();
            foreach(var row in r.segments)
            {
                var root=Resolve(row.segmentGlobalId) as Transform;
                if(root==null||row.originalRendererGlobalIds.Length!=row.originalEnabled.Length)throw new InvalidOperationException("Source scene differs; restore preflight failed.");
                var stage=root.Find(row.visualChild);if(stage==null||!stage.name.StartsWith(VisualPrefix,StringComparison.Ordinal))throw new InvalidOperationException("Owned visual stage unavailable.");
                roots.Add(root);stages.Add(stage.gameObject);
                for(int i=0;i<row.originalRendererGlobalIds.Length;i++)
                {
                    var renderer=Resolve(row.originalRendererGlobalIds[i]) as MeshRenderer;
                    if(renderer==null||!renderer.transform.IsChildOf(root))throw new InvalidOperationException("Original renderer unavailable.");
                    restore.Add(Tuple.Create(renderer,row.originalEnabled[i]));
                }
            }
            foreach(var stage in stages)stage.SetActive(false);
            foreach(var entry in restore)SetEnabled(entry.Item1,entry.Item2);
            foreach(var root in roots)EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            r.status="RESTORED_ORIGINAL_RENDERERS_OPTIMIZED_ASSETS_RETAINED";File.WriteAllText(receiptPath,JsonUtility.ToJson(r,true));
        }

        static SegmentWork Inspect(Transform root)
        {
            if(!root.gameObject.scene.IsValid()||string.IsNullOrEmpty(root.gameObject.scene.path))throw new InvalidOperationException("Saved scene source required for persistent restoration IDs.");
            foreach(Transform child in root)if(child.name.StartsWith(VisualPrefix,StringComparison.Ordinal))throw new InvalidOperationException("Existing optimizer stage must be reviewed/restored before another run.");
            var sources=new List<Source>();var work=new SegmentWork{root=root};
            var receipt=new SegmentReceipt{segment=root.name,segmentGlobalId=GlobalObjectId.GetGlobalObjectIdSlow(root).ToString()};work.receipt=receipt;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled)continue;
                var filter=renderer.GetComponent<MeshFilter>();var mesh=filter==null?null:filter.sharedMesh;
                if(mesh==null||!mesh.isReadable||mesh.blendShapeCount!=0||renderer.additionalVertexStreams!=null||renderer.HasPropertyBlock()||renderer.isPartOfStaticBatch)
                    throw new InvalidOperationException("Unsupported source mesh, deformation, property block or static batch.");
                if(renderer.lightmapIndex>=0&&renderer.lightmapIndex<65534||renderer.realtimeLightmapIndex>=0&&renderer.realtimeLightmapIndex<65534||renderer.lightProbeProxyVolumeOverride!=null)
                    throw new InvalidOperationException("Lightmapped/proxy-probed rendering cannot be merged without a separate lighting-equivalence plan.");
                if(renderer.lightProbeUsage!=LightProbeUsage.Off&&LightmapSettings.lightProbes!=null&&LightmapSettings.lightProbes.count>0)
                    throw new InvalidOperationException("Active light probes sample by renderer position; source-preserving merge is unavailable.");
                if(renderer.reflectionProbeUsage!=ReflectionProbeUsage.Off&&UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Any(p=>p.isActiveAndEnabled))
                    throw new InvalidOperationException("Active reflection probes require an independent per-renderer equivalence audit.");
                var materials=renderer.sharedMaterials;
                if(materials.Length!=mesh.subMeshCount||materials.Any(m=>m==null))throw new InvalidDataException("Material/submesh mapping is not one-to-one; refusing implicit draw changes.");
                var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                if(!Finite(matrix)||Mathf.Abs(matrix.determinant)<1e-12f)throw new InvalidDataException("Invalid source transform.");
                sources.Add(new Source{filter=filter,renderer=renderer,mesh=mesh,materials=materials,channels=new Channels(mesh),matrix=matrix,normalMatrix=matrix.inverse.transpose,mirrored=matrix.determinant<0,wasEnabled=renderer.enabled,state=StateKey(renderer),localBounds=renderer.localBounds});
            }
            if(sources.Count==0)throw new InvalidOperationException("No enabled source chunk renderers.");
            work.sources=sources.ToArray();work.colliders=root.GetComponentsInChildren<MeshCollider>(true);work.colliderMeshes=work.colliders.Select(c=>c.sharedMesh).ToArray();
            var opaque=new Dictionary<string,Batch>();
            foreach(var source in sources)
            {
                var transparent=new Batch{transparent=true};receipt.sourceMaterialSlots+=source.materials.Length;
                for(int sub=0;sub<source.mesh.subMeshCount;sub++)
                {
                    if(source.mesh.GetTopology(sub)!=MeshTopology.Triangles)throw new InvalidDataException("Triangle source only.");
                    var indices=source.mesh.GetIndices(sub,true);
                    if(indices.Length==0){receipt.emptySubmeshes++;continue;}
                    if(indices.Length%3!=0||indices.Any(i=>i<0||i>=source.channels.positions.Length))throw new InvalidDataException("Invalid source triangle indices.");
                    receipt.nonemptySubmeshes++;receipt.sourceTriangles+=indices.Length/3;
                    var draw=new Draw{source=source,material=source.materials[sub],indices=indices,submesh=sub};
                    if(IsTransparent(draw.material))transparent.draws.Add(draw);
                    else
                    {
                        string key=source.channels.layout+"|"+source.state; // Same-material draws coalesce into separate submeshes inside one state-compatible segment mesh.
                        if(!opaque.TryGetValue(key,out var batch)){batch=new Batch();opaque.Add(key,batch);work.batches.Add(batch);receipt.opaqueBatches++;}
                        batch.draws.Add(draw);
                    }
                }
                if(transparent.draws.Count>0){work.batches.Add(transparent);receipt.transparentRendererCopies++;}
            }
            receipt.sourceRenderers=sources.Count;receipt.originalRendererGlobalIds=sources.Select(s=>GlobalObjectId.GetGlobalObjectIdSlow(s.renderer).ToString()).ToArray();receipt.originalEnabled=sources.Select(s=>s.wasEnabled).ToArray();return work;
        }

        static Mesh Build(Batch batch)
        {
            var first=batch.draws[0].source.channels;var positions=new List<Vector3>();var normals=new List<Vector3>();var tangents=new List<Vector4>();var colors=new List<Color>();
            var uv=Enumerable.Range(0,8).Select(_=>new List<Vector4>()).ToArray();var allIndices=new List<List<int>>();
            var maps=new Dictionary<Source,int[]>();
            var materialSubmeshes=new Dictionary<Material,int>();
            foreach(var draw in batch.draws)
            {
                var source=draw.source;var channels=source.channels;
                if(channels.layout!=first.layout)throw new InvalidDataException("Channel layouts differ inside a batch.");
                if(!maps.TryGetValue(source,out var map)){map=Enumerable.Repeat(-1,channels.positions.Length).ToArray();maps.Add(source,map);}
                int outputSub;
                if(batch.transparent){outputSub=allIndices.Count;allIndices.Add(new List<int>());}
                else if(!materialSubmeshes.TryGetValue(draw.material,out outputSub)){outputSub=allIndices.Count;materialSubmeshes.Add(draw.material,outputSub);allIndices.Add(new List<int>());}
                var indices=allIndices[outputSub];int start=indices.Count;
                for(int i=0;i<draw.indices.Length;i++)
                {
                    int v=draw.indices[i];if(map[v]>=0)continue;
                    map[v]=positions.Count;positions.Add(source.matrix.MultiplyPoint3x4(channels.positions[v]));
                    if(first.normals.Length>0)normals.Add(source.normalMatrix.MultiplyVector(channels.normals[v]).normalized);
                    if(first.tangents.Length>0){var t=channels.tangents[v];var xyz=source.matrix.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;tangents.Add(new Vector4(xyz.x,xyz.y,xyz.z,t.w*(source.mirrored?-1:1)));}
                    if(first.colors.Length>0)colors.Add(channels.colors[v]);
                    for(int ch=0;ch<8;ch++)if(first.uvDimensions[ch]>0)uv[ch].Add(channels.uv[ch][v]);
                }
                for(int i=0;i<draw.indices.Length;i+=3){indices.Add(map[draw.indices[i]]);indices.Add(map[draw.indices[i+(source.mirrored?2:1)]]);indices.Add(map[draw.indices[i+(source.mirrored?1:2)]]);}
                batch.spans.Add(new Span{draw=draw,outputSubmesh=outputSub,start=start,map=map});
            }
            var mesh=new Mesh{name="Official source material batch",indexFormat=IndexFormat.UInt32};mesh.SetVertices(positions);
            if(normals.Count>0)mesh.SetNormals(normals);if(tangents.Count>0)mesh.SetTangents(tangents);if(colors.Count>0)mesh.SetColors(colors);
            for(int ch=0;ch<8;ch++)
            {
                if(first.uvDimensions[ch]==2)mesh.SetUVs(ch,uv[ch].Select(v=>new Vector2(v.x,v.y)).ToList());
                else if(first.uvDimensions[ch]==3)mesh.SetUVs(ch,uv[ch].Select(v=>new Vector3(v.x,v.y,v.z)).ToList());
                else if(first.uvDimensions[ch]==4)mesh.SetUVs(ch,uv[ch]);
            }
            mesh.subMeshCount=allIndices.Count;for(int i=0;i<allIndices.Count;i++)mesh.SetIndices(allIndices[i].ToArray(),MeshTopology.Triangles,i,false);
            mesh.RecalculateBounds();mesh.UploadMeshData(false);return mesh;
        }

        static void Validate(Batch batch)
        {
            var output=new Channels(batch.mesh);var first=batch.draws[0].source.channels;
            if(output.layout!=first.layout||batch.mesh.indexFormat!=IndexFormat.UInt32)throw new InvalidDataException("Persisted channel layout/index format changed.");
            var indices=Enumerable.Range(0,batch.mesh.subMeshCount).Select(s=>batch.mesh.GetIndices(s,true)).ToArray();var checkedSources=new HashSet<Source>();
            foreach(var span in batch.spans)
            {
                var draw=span.draw;var source=draw.source;var input=source.channels;
                if(batch.renderer.sharedMaterials[span.outputSubmesh]!=draw.material)throw new InvalidDataException("Material reference changed.");
                for(int i=0;i<draw.indices.Length;i++)
                {
                    int local=i%3,sourceOffset=i-local+(source.mirrored&&local!=0?3-local:local);
                    if(indices[span.outputSubmesh][span.start+i]!=span.map[draw.indices[sourceOffset]])throw new InvalidDataException("Triangle/corner order changed.");
                }
                if(!checkedSources.Add(source))continue;
                for(int v=0;v<span.map.Length;v++)
                {
                    int o=span.map[v];if(o<0)continue;
                    if((output.positions[o]-source.matrix.MultiplyPoint3x4(input.positions[v])).sqrMagnitude>PositionTolerance*PositionTolerance)throw new InvalidDataException("Position changed.");
                    if(input.normals.Length>0&&(output.normals[o]-source.normalMatrix.MultiplyVector(input.normals[v]).normalized).sqrMagnitude>1e-10f)throw new InvalidDataException("Normal changed.");
                    if(input.tangents.Length>0){var t=input.tangents[v];var xyz=source.matrix.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;var expected=new Vector4(xyz.x,xyz.y,xyz.z,t.w*(source.mirrored?-1:1));if((output.tangents[o]-expected).sqrMagnitude>1e-10f)throw new InvalidDataException("Tangent changed.");}
                    if(input.colors.Length>0&&!output.colors[o].Equals(input.colors[v]))throw new InvalidDataException("Vertex color changed.");
                    for(int ch=0;ch<8;ch++)if(input.uvDimensions[ch]>0&&!output.uv[ch][o].Equals(input.uv[ch][v]))throw new InvalidDataException("UV changed.");
                }
            }
            long before=batch.draws.Sum(d=>(long)d.indices.Length);long after=indices.Sum(i=>(long)i.Length);if(before!=after)throw new InvalidDataException("Triangle count changed.");
        }

        static void ValidateSegment(SegmentWork work)
        {
            var sourceBounds=PointBounds(work.sources.SelectMany(s=>s.channels.positions.Select(p=>s.matrix.MultiplyPoint3x4(p))));
            var outputBounds=PointBounds(work.batches.SelectMany(b=>b.mesh.vertices));
            if(Vector3.Distance(sourceBounds.min,outputBounds.min)>BoundsTolerance||Vector3.Distance(sourceBounds.max,outputBounds.max)>BoundsTolerance)
                throw new InvalidDataException("Segment source metre bounds changed; unused extreme vertices are not silently discarded.");
            var r=work.receipt;r.sourceMin=sourceBounds.min;r.sourceMax=sourceBounds.max;r.outputMin=outputBounds.min;r.outputMax=outputBounds.max;
            r.outputRenderers=work.batches.Count;r.outputMaterialSlots=work.batches.Sum(b=>b.renderer.sharedMaterials.Length);r.outputTriangles=work.batches.Sum(b=>(long)b.mesh.triangles.Length/3);
            if(r.sourceTriangles!=r.outputTriangles)throw new InvalidDataException("Segment triangle count changed.");
            r.channelsVerified=true;r.materialReferencesPreserved=true;r.originalFiltersAndCollidersPreserved=true;
        }
        static void VerifyOriginalReferences(SegmentWork work,bool checkEnabled=true)
        {
            foreach(var source in work.sources)
            {
                if(source.filter==null||source.renderer==null||source.filter.sharedMesh!=source.mesh||!source.renderer.sharedMaterials.SequenceEqual(source.materials)||StateKey(source.renderer)!=source.state||!(work.root.worldToLocalMatrix*source.filter.transform.localToWorldMatrix).Equals(source.matrix)||checkEnabled&&source.renderer.enabled!=source.wasEnabled)
                    throw new InvalidOperationException("Original source reference/state changed during staging.");
            }
            for(int i=0;i<work.colliders.Length;i++)if(work.colliders[i]==null||work.colliders[i].sharedMesh!=work.colliderMeshes[i])throw new InvalidOperationException("Original collider changed.");
        }
        static void SetEnabled(MeshRenderer renderer,bool enabled){renderer.enabled=enabled;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        static bool IsTransparent(Material m)=>m.renderQueue>=(int)RenderQueue.Transparent||m.GetTag("RenderType",false,"")=="Transparent"||m.HasProperty("_Surface")&&m.GetFloat("_Surface")>.5f;
        static string StateKey(MeshRenderer r)=>r.gameObject.layer+"/"+r.forceRenderingOff+"/"+r.shadowCastingMode+"/"+r.receiveShadows+"/"+r.lightProbeUsage+"/"+r.reflectionProbeUsage+"/"+(r.probeAnchor==null?0:r.probeAnchor.GetInstanceID())+"/"+r.motionVectorGenerationMode+"/"+r.allowOcclusionWhenDynamic+"/"+r.renderingLayerMask+"/"+r.sortingLayerID+"/"+r.sortingOrder+"/"+r.rendererPriority;
        static void CopyState(MeshRenderer a,MeshRenderer b)
        {
            b.forceRenderingOff=a.forceRenderingOff;b.shadowCastingMode=a.shadowCastingMode;b.receiveShadows=a.receiveShadows;b.lightProbeUsage=a.lightProbeUsage;b.reflectionProbeUsage=a.reflectionProbeUsage;b.probeAnchor=a.probeAnchor;b.motionVectorGenerationMode=a.motionVectorGenerationMode;b.allowOcclusionWhenDynamic=a.allowOcclusionWhenDynamic;b.renderingLayerMask=a.renderingLayerMask;b.sortingLayerID=a.sortingLayerID;b.sortingOrder=a.sortingOrder;b.rendererPriority=a.rendererPriority;
        }
        static Bounds TransformBounds(Matrix4x4 m,Bounds b)=>PointBounds(Enumerable.Range(0,8).Select(i=>m.MultiplyPoint3x4(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z))));
        static Bounds PointBounds(IEnumerable<Vector3> points){bool first=true;var b=new Bounds();foreach(var p in points){if(!Finite(p))throw new InvalidDataException("Nonfinite vertex.");if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}if(first)throw new InvalidDataException("Empty geometry.");return b;}
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        static bool Finite(Matrix4x4 m){for(int i=0;i<16;i++)if(float.IsNaN(m[i])||float.IsInfinity(m[i]))return false;return true;}
        static UnityEngine.Object Resolve(string id)=>GlobalObjectId.TryParse(id,out var parsed)?GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed):null;
        static void RequireEditMode(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Renderer optimization/restore requires edit mode.");}
        static void EnsureFolder(string path){var parts=path.Split('/');string parent=parts[0];for(int i=1;i<parts.Length;i++){string child=parent+"/"+parts[i];if(!AssetDatabase.IsValidFolder(child))AssetDatabase.CreateFolder(parent,parts[i]);parent=child;}}
    }
}
