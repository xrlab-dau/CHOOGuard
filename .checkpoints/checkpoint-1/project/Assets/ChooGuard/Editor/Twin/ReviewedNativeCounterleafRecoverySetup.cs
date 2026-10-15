#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.App.Fps.Twin;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Root-owned staging of 24 absent native faces only. No background edits, controller, hinge or closed pose.</summary>
    public static class ReviewedNativeCounterleafRecoverySetup
    {
        const float FrameTolerance=.0001f,SourceTolerance=.0003f,PlaneTolerance=.0004f;
        const double AreaTolerance=1e-10;
        const string Category="partial_counterleaf_fragment_source_part";
        const string ReviewedPacketSha256="df1e591f2e6918235ec2f0e8f51743f7d016b24252474944fe503fe25faf08e2";
        static readonly Dictionary<string,int[]> ApprovedFaces=new Dictionary<string,int[]>
        {
            {"501b0b5be16448a0d5c95d78",new[]{2,3}}, {"5d4f4242d54a39380767bf43",new[]{0,1,2,3}},
            {"ab427631ad34251695dc2ee6",new[]{0,1,8,11,17,19}}, {"2db06c0adff1128fd9c0c5fb",new[]{0,1,2,3}},
            {"8f09a2b4ce0d2f2a58e31347",new[]{0,1,8,11,17,19}}, {"d1b879fc4e65e56b6c367995",new[]{2,3}}
        };
        sealed class Prepared
        {
            public JToken Row;public MeshFilter Source;public Mesh Mesh;public Transform Parent;
            public Matrix4x4 Local;public Material[] Materials;public int[][] Selected;public Vector3[][] Faces;
        }
        public sealed class Result
        {
            public GameObject[] InactiveFragments;
            public string[] CreatedAssetPaths;
            public JObject Receipt;
            internal Action Validate;
            internal string[] CreatedFolders;
            public void Activate()
            {
                if(InactiveFragments.Any(g=>g==null))throw new InvalidOperationException("Staged partial source fragments were removed");
                Validate();foreach(var g in InactiveFragments)g.SetActive(true);
                Receipt["fragments_active"]=true;
            }
            public void Rollback()
            {
                foreach(var g in InactiveFragments.Reverse())if(g!=null)UnityEngine.Object.DestroyImmediate(g);
                foreach(string path in CreatedAssetPaths.Reverse())if(AssetDatabase.LoadMainAssetAtPath(path)!=null)AssetDatabase.DeleteAsset(path);
                foreach(string path in CreatedFolders.Reverse())
                    if(AssetDatabase.IsValidFolder(path)&&Directory.Exists(path)&&!Directory.EnumerateFileSystemEntries(path).Any())AssetDatabase.DeleteAsset(path);
            }
        }
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static Vector3 V(JToken t)=>new Vector3((float)t[0],(float)t[1],(float)t[2]);
        static Matrix4x4 M(JToken t)
        {
            var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=(float)t[r][c];return m;
        }
        static string Hash(string path)
        {
            using(var sha=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
        }
        static void Frame(Matrix4x4 actual,JToken frozen,string label)
        {
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)
                Require(float.IsFinite(actual[r,c])&&Mathf.Abs(actual[r,c]-(float)frozen[r][c])<=FrameTolerance,"Native frame changed: "+label);
        }
        static string Attributes(Mesh mesh)
        {
            using(var buffer=new MemoryStream())using(var w=new BinaryWriter(buffer))
            {
                w.Write(mesh.vertexCount);
                foreach(var d in mesh.GetVertexAttributes()){w.Write((int)d.attribute);w.Write((int)d.format);w.Write(d.dimension);w.Write(d.stream);}
                foreach(var v in mesh.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
                foreach(var v in mesh.normals){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
                foreach(var v in mesh.tangents){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}
                foreach(var v in mesh.colors){w.Write(v.r);w.Write(v.g);w.Write(v.b);w.Write(v.a);}
                for(int channel=0;channel<8;channel++)
                {
                    var uv=new List<Vector4>();mesh.GetUVs(channel,uv);w.Write(channel);w.Write(uv.Count);
                    foreach(var v in uv){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}
                }
                w.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        static void Folder(string path,List<string> created)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            int split=path.LastIndexOf('/');Require(split>0,"Invalid dedicated asset folder");Folder(path.Substring(0,split),created);
            AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));created.Add(path);
        }
        static void CleanSource(Mesh mesh,Material[] materials)
        {
            Require(!EditorUtility.IsDirty(mesh),"Unsaved native mesh changes prevent source-channel proof");
            foreach(var material in materials)
            {
                Require(material!=null&&!EditorUtility.IsDirty(material),"Unsaved native material changes prevent source proof");
                foreach(string property in material.GetTexturePropertyNames())
                {
                    var texture=material.GetTexture(property);
                    Require(texture==null||!EditorUtility.IsDirty(texture),"Unsaved native material texture changes prevent source proof");
                }
            }
        }
        static void CopyRendering(MeshRenderer source,MeshRenderer target)
        {
            target.enabled=source.enabled;target.shadowCastingMode=source.shadowCastingMode;target.receiveShadows=source.receiveShadows;
            target.motionVectorGenerationMode=source.motionVectorGenerationMode;target.lightProbeUsage=source.lightProbeUsage;
            target.reflectionProbeUsage=source.reflectionProbeUsage;target.probeAnchor=source.probeAnchor;
            target.lightProbeProxyVolumeOverride=source.lightProbeProxyVolumeOverride;target.renderingLayerMask=source.renderingLayerMask;
            target.allowOcclusionWhenDynamic=source.allowOcclusionWhenDynamic;target.forceRenderingOff=source.forceRenderingOff;
            target.sortingLayerID=source.sortingLayerID;target.sortingOrder=source.sortingOrder;target.rendererPriority=source.rendererPriority;
            target.rayTracingMode=source.rayTracingMode;target.staticShadowCaster=source.staticShadowCaster;
            target.receiveGI=source.receiveGI;target.scaleInLightmap=source.scaleInLightmap;target.stitchLightmapSeams=source.stitchLightmapSeams;
            target.lightmapIndex=source.lightmapIndex;target.lightmapScaleOffset=source.lightmapScaleOffset;
            target.realtimeLightmapIndex=source.realtimeLightmapIndex;target.realtimeLightmapScaleOffset=source.realtimeLightmapScaleOffset;
            var block=new MaterialPropertyBlock();source.GetPropertyBlock(block);target.SetPropertyBlock(block.isEmpty?null:block);
            for(int i=0;i<source.sharedMaterials.Length;i++){block.Clear();source.GetPropertyBlock(block,i);target.SetPropertyBlock(block.isEmpty?null:block,i);}
            GameObjectUtility.SetStaticEditorFlags(target.gameObject,GameObjectUtility.GetStaticEditorFlags(source.gameObject));
        }
        static void RenderingEquals(MeshRenderer source,MeshRenderer target)
        {
            Require(target.enabled==source.enabled&&target.shadowCastingMode==source.shadowCastingMode&&target.receiveShadows==source.receiveShadows&&
                target.motionVectorGenerationMode==source.motionVectorGenerationMode&&target.lightProbeUsage==source.lightProbeUsage&&
                target.reflectionProbeUsage==source.reflectionProbeUsage&&target.probeAnchor==source.probeAnchor&&
                target.lightProbeProxyVolumeOverride==source.lightProbeProxyVolumeOverride&&target.renderingLayerMask==source.renderingLayerMask&&
                target.allowOcclusionWhenDynamic==source.allowOcclusionWhenDynamic&&target.forceRenderingOff==source.forceRenderingOff&&
                target.sortingLayerID==source.sortingLayerID&&target.sortingOrder==source.sortingOrder&&target.rendererPriority==source.rendererPriority&&
                target.rayTracingMode==source.rayTracingMode&&target.staticShadowCaster==source.staticShadowCaster&&
                target.receiveGI==source.receiveGI&&target.scaleInLightmap==source.scaleInLightmap&&target.stitchLightmapSeams==source.stitchLightmapSeams&&
                target.lightmapIndex==source.lightmapIndex&&target.lightmapScaleOffset==source.lightmapScaleOffset&&
                target.realtimeLightmapIndex==source.realtimeLightmapIndex&&target.realtimeLightmapScaleOffset==source.realtimeLightmapScaleOffset&&
                GameObjectUtility.GetStaticEditorFlags(target.gameObject)==GameObjectUtility.GetStaticEditorFlags(source.gameObject),"Native renderer/static settings changed");
        }
        struct P2
        {
            public double X,Y;public P2(double x,double y){X=x;Y=y;}
        }
        static double Cross(P2 a,P2 b,P2 p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
        static double PolygonArea(List<P2> p)
        {
            double a=0;for(int i=0;i<p.Count;i++){var b=p[(i+1)%p.Count];a+=p[i].X*b.Y-b.X*p[i].Y;}return a*.5;
        }
        static double Intersection(Vector3[] source,Vector3[] candidate)
        {
            Vector3 n=Vector3.Cross(source[1]-source[0],source[2]-source[0]);
            if(n.sqrMagnitude<1e-20f)return 0;n.Normalize();
            // Absolute distance deliberately includes both normal signs and reflected/mirrored native frames.
            if(candidate.Any(p=>Mathf.Abs(Vector3.Dot(p-source[0],n))>PlaneTolerance))return 0;
            Vector3 x=(source[1]-source[0]).normalized,y=Vector3.Cross(n,x);
            Func<Vector3,P2> project=p=>new P2(Vector3.Dot(p-source[0],x),Vector3.Dot(p-source[0],y));
            var clip=source.Select(project).ToList();if(PolygonArea(clip)<0)clip.Reverse();
            var polygon=candidate.Select(project).ToList();
            for(int edge=0;edge<3&&polygon.Count>0;edge++)
            {
                P2 a=clip[edge],b=clip[(edge+1)%3];var next=new List<P2>();
                for(int i=0;i<polygon.Count;i++)
                {
                    P2 first=polygon[i],last=polygon[(i+1)%polygon.Count];double df=Cross(a,b,first),dl=Cross(a,b,last);
                    bool inf=df>=0,inl=dl>=0;if(inf)next.Add(first);
                    if(inf!=inl)
                    {
                        double denominator=df-dl;if(Math.Abs(denominator)<1e-24)continue;double t=df/denominator;
                        next.Add(new P2(first.X+(last.X-first.X)*t,first.Y+(last.Y-first.Y)*t));
                    }
                }
                polygon=next;
            }
            return polygon.Count<3?0:Math.Abs(PolygonArea(polygon));
        }
        static Bounds WorldBounds(Mesh mesh,Transform frame)
        {
            var b=mesh.bounds;var result=new Bounds(frame.TransformPoint(b.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                result.Encapsulate(frame.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
            return result;
        }
        static void NoOverlap(Vector3[][] faces,string scene,HashSet<GameObject> ignore)
        {
            var bounds=faces.Select(f=>{var b=new Bounds(f[0],Vector3.zero);b.Encapsulate(f[1]);b.Encapsulate(f[2]);b.Expand(PlaneTolerance*2);return b;}).ToArray();
            var sources=new List<(Mesh Mesh,Transform Frame,string Kind)>();
            foreach(var f in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(f.gameObject.scene.path==scene&&!ignore.Contains(f.gameObject)&&f.sharedMesh!=null)sources.Add((f.sharedMesh,f.transform,"MeshFilter"));
            foreach(var c in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(c.gameObject.scene.path==scene&&!ignore.Contains(c.gameObject)&&c.sharedMesh!=null)sources.Add((c.sharedMesh,c.transform,"MeshCollider"));
            foreach(var s in sources)
            {
                var ownerBounds=WorldBounds(s.Mesh,s.Frame);if(!bounds.Any(b=>b.Intersects(ownerBounds)))continue;
                Require(s.Mesh.isReadable,"Unreadable nearby geometry prevents absence proof: "+PathOf(s.Frame));
                var vertices=s.Mesh.vertices.Select(s.Frame.TransformPoint).ToArray();var ix=s.Mesh.triangles;
                for(int at=0;at<ix.Length;at+=3)
                {
                    var t=new[]{vertices[ix[at]],vertices[ix[at+1]],vertices[ix[at+2]]};var b=new Bounds(t[0],Vector3.zero);b.Encapsulate(t[1]);b.Encapsulate(t[2]);
                    for(int f=0;f<faces.Length;f++)
                        if(bounds[f].Intersects(b))Require(Intersection(faces[f],t)<=AreaTolerance,"Absent native face intersects current "+s.Kind+": "+PathOf(s.Frame));
                }
            }
        }
        static Prepared Prepare(JToken row,string scene,Transform[] transforms,MeshFilter[] filters)
        {
            string id=(string)row["source_id"],parentName="N_"+(string)row["target_counterleaf_source_id"];
            var parents=transforms.Where(t=>t.name==parentName&&t.gameObject.scene.path==scene).ToArray();
            Require(parents.Length==1,"Target native counterleaf is missing/ambiguous: "+parentName);var parent=parents.Single();
            Frame(parent.localToWorldMatrix,row["target_counterleaf_world_matrix"],parentName);
            Require(GlobalObjectId.GetGlobalObjectIdSlow(parent).targetObjectId==(ulong)row["target_counterleaf_transform_file_id"],"Target counterleaf serialized identity changed");
            var references=filters.Where(f=>PathOf(f.transform)==(string)row["reference_renderer_path"]&&f.gameObject.scene.path==scene).ToArray();
            Require(references.Length==1,"Complete native counterpart missing/ambiguous: "+id);var reference=references.Single();
            Require(GlobalObjectId.GetGlobalObjectIdSlow(reference).ToString()==(string)row["reference_renderer_global_object_id"],"Native counterpart MeshFilter identity changed");
            Frame(reference.transform.localToWorldMatrix,row["reference_renderer_world_matrix"],id);
            var mesh=reference.sharedMesh;Require(mesh!=null&&mesh.isReadable,"Native counterpart mesh is unavailable");
            string asset=(string)row["reference_mesh_asset"];
            Require(AssetDatabase.GetAssetPath(mesh)==asset&&AssetDatabase.AssetPathToGUID(asset)==(string)row["reference_mesh_guid"]&&
                mesh.name==(string)row["reference_mesh_name"]&&Hash(asset)==(string)row["reference_mesh_asset_file_sha256"],"Native counterpart mesh asset changed");
            Require(mesh.vertexCount==(int)row["reference_mesh_vertex_count"],"Native counterpart vertex count changed");
            var frozenVertices=((JArray)row["reference_native_vertices"]).Select(V).ToArray();var vertices=mesh.vertices;
            Require(vertices.Length==frozenVertices.Length&&vertices.Zip(frozenVertices,(a,b)=>a.Equals(b)).All(x=>x),"Native counterpart vertex channels changed");
            var frozenTriangles=((JArray)row["reference_mesh_triangles"]).SelectMany(t=>((JArray)t).Select(x=>(int)x)).ToArray();
            Require(mesh.triangles.SequenceEqual(frozenTriangles),"Native counterpart triangle order changed");
            var renderer=reference.GetComponent<MeshRenderer>();Require(renderer!=null,"Native counterpart renderer missing");
            var nativeCollision=reference.GetComponent<MeshCollider>();
            Require(nativeCollision!=null&&nativeCollision.sharedMesh==mesh,"Native counterpart collision source missing/differs");
            var materials=renderer.sharedMaterials;var expected=(JArray)row["materials"];
            Require(materials.Length==expected.Count&&materials.Length==mesh.subMeshCount,"Native ordered material/submesh identity changed");
            CleanSource(mesh,materials);
            for(int i=0;i<materials.Length;i++)
            {
                string path=(string)expected[i]["asset"];
                Require(materials[i]!=null&&AssetDatabase.GetAssetPath(materials[i])==path&&AssetDatabase.AssetPathToGUID(path)==(string)expected[i]["guid"]&&
                    Hash(path)==(string)expected[i]["sha256"],"Native material identity changed");
            }
            var arrays=Enumerable.Range(0,mesh.subMeshCount).Select(sub=>mesh.GetTriangles(sub)).ToArray();
            var selected=((JArray)row["selected_faces"]).ToDictionary(f=>(int)f["native_flattened_triangle_ordinal"]);
            Require(selected.Count==(int)row["restored_triangles"],"Partial source face count differs");
            var keep=arrays.Select(_=>new List<int>()).ToArray();var faces=new List<Vector3[]>();int flat=0;
            Matrix4x4 local=M(row["local_matrix_from_complete_counterpart"]),planned=parent.localToWorldMatrix*local;
            Frame(planned,row["planned_world_matrix"],"planned partial source frame");
            for(int sub=0;sub<arrays.Length;sub++)for(int at=0;at<arrays[sub].Length;at+=3,flat++)
            {
                if(!selected.TryGetValue(flat,out var f))continue;var ids=new[]{arrays[sub][at],arrays[sub][at+1],arrays[sub][at+2]};
                Require(ids.SequenceEqual(((JArray)f["native_vertex_indices"]).Select(x=>(int)x)),"Native selected source corner indices changed");
                Require(sub==(int)f["original_material_slot"],"Native selected face material slot changed");
                var w=ids.Select(i=>planned.MultiplyPoint3x4(vertices[i])).ToArray();var expectedWorld=((JArray)f["planned_native_world_triangle"]).Select(V).ToArray();
                Require(w.Zip(expectedWorld,Vector3.Distance).Max()<=FrameTolerance,"Partial source world frame changed");
                var original=((JArray)f["expected_original_world_triangle"]).Select(V).ToArray();
                var mapping=((JArray)f["native_to_original_corner_mapping"]).Select(x=>(int)x).ToArray();
                Require(Enumerable.Range(0,3).Max(i=>Vector3.Distance(w[i],original[mapping[i]]))<=SourceTolerance,"Recovered partial source differs from original authored pose");
                int parity=Vector3.Dot(Vector3.Cross(w[1]-w[0],w[2]-w[0]),Vector3.Cross(original[1]-original[0],original[2]-original[0]))>0?1:-1;
                Require(parity==(int)f["native_to_original_winding_parity"],"Native mirrored/source winding contract changed");
                keep[sub].AddRange(ids);faces.Add(w);
            }
            Require(faces.Count==selected.Count,"Selected native source face missing");
            return new Prepared{Row=row,Source=reference,Mesh=mesh,Parent=parent,Local=local,Materials=materials,Selected=keep.Select(x=>x.ToArray()).ToArray(),Faces=faces.ToArray()};
        }
        static void SetLocal(Transform transform,JToken row)
        {
            Matrix4x4 matrix=M(row["local_matrix_from_complete_counterpart"]),rotation=M(new JArray(
                Enumerable.Range(0,4).Select(r=>new JArray(Enumerable.Range(0,4).Select(c=>r<3&&c<3?(float)row["local_frame_rotation_matrix"][r][c]:(r==c?1f:0f))))));
            transform.localPosition=matrix.GetColumn(3);transform.localScale=V(row["local_frame_scale"]);
            transform.localRotation=Quaternion.LookRotation(rotation.GetColumn(2),rotation.GetColumn(1));
        }
        /// <summary>Return configured inactive fragments. Caller owns activation, tests, journal, scene save and final receipt.</summary>
        public static Result Stage(string packetPath,string newAssetDirectory)
        {
            Require(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Stable Edit mode required");
            Require(Hash(packetPath)==ReviewedPacketSha256,"Partial-source packet bytes differ from the reviewed original24 faces");
            var packet=JObject.Parse(File.ReadAllText(packetPath));string scene=(string)packet["frozen_scene"];
            Require(SceneManager.GetActiveScene().path==scene&&!SceneManager.GetActiveScene().isDirty,"Frozen saved production scene required");
            Require(Hash(scene)==(string)packet["frozen_scene_sha256"],"Saved production scene changed since absence proof");
            foreach(string key in new[]{"scene_export","source_geometry","absence_audit","offline_qa"})
                Require(Hash((string)packet[key])==(string)packet[key+"_sha256"],"Frozen partial-source evidence changed: "+key);
            var proof=JObject.Parse(File.ReadAllText((string)packet["offline_qa"]));
            Require((bool)proof["accepted"]&&(int)proof["source_faces"]==24&&(float)proof["near_coplanar_plane_tolerance_model_units"]>=PlaneTolerance,"Absent face QA contract differs");
            Require(newAssetDirectory=="Assets/ChooGuard/Art/Fps/NativeCounterleafFragments20261009","Dedicated new partial source asset folder required");
            var rows=((JArray)packet["fragments"]).ToArray();Require(rows.Length==6&&rows.Sum(r=>(int)r["restored_triangles"])==24,"Only approved six partial source parts / 24 faces may be staged");
            var sourceIds=rows.Select(r=>(string)r["source_id"]).ToArray();Require(sourceIds.Distinct().Count()==6,"Duplicate partial source identity");
            Require(sourceIds.OrderBy(x=>x).SequenceEqual(ApprovedFaces.Keys.OrderBy(x=>x)),"Only approved partial native source identities may be restored");
            foreach(var row in rows)
                Require(((JArray)row["selected_faces"]).Select(f=>(int)f["source_triangle_ordinal"]).OrderBy(x=>x).SequenceEqual(ApprovedFaces[(string)row["source_id"]]),"Only approved absent original source face ordinals may be restored");
            Require(sourceIds.OrderBy(x=>x).SequenceEqual(((JArray)packet["expected_created_source_ids"]).Select(x=>(string)x).OrderBy(x=>x)),"Partial source identity set changed");
            Require(!UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(x=>sourceIds.Contains(x.SourceId)),"Partial source identity already exists");
            var transforms=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var prepared=rows.Select(r=>Prepare(r,scene,transforms,filters)).ToArray();
            var allFaces=prepared.SelectMany(p=>p.Faces).ToArray();NoOverlap(allFaces,scene,new HashSet<GameObject>());
            var created=new List<string>();var folders=new List<string>();var objects=new List<GameObject>();var resultRows=new JArray();var transientMeshes=new List<Mesh>();
            bool complete=false;
            try
            {
                Folder(newAssetDirectory,folders);
                foreach(var p in prepared)
                {
                    string id=(string)p.Row["source_id"],attributes=Attributes(p.Mesh);var mesh=UnityEngine.Object.Instantiate(p.Mesh);
                    transientMeshes.Add(mesh);
                    mesh.name=id+"_partial_absent_source_faces";
                    for(int sub=0;sub<p.Selected.Length;sub++)mesh.SetTriangles(p.Selected[sub],sub,false);
                    Require(Attributes(mesh)==attributes,"Partial native mesh did not preserve all original vertex attributes");
                    Require(mesh.subMeshCount==p.Mesh.subMeshCount&&mesh.triangles.Length/3==(int)p.Row["restored_triangles"],"Partial native mesh face/submesh conservation failed");
                    string asset=AssetDatabase.GenerateUniqueAssetPath(newAssetDirectory+"/"+id+".asset");
                    AssetDatabase.CreateAsset(mesh,asset);created.Add(asset);
                    transientMeshes.Remove(mesh);
                    var go=new GameObject("N_"+id+"_partial_recovery");go.SetActive(false);objects.Add(go);
                    go.transform.SetParent(p.Parent,false);go.layer=p.Source.gameObject.layer;SetLocal(go.transform,p.Row);
                    Frame(go.transform.localToWorldMatrix,p.Row["planned_world_matrix"],id);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var fragmentRenderer=go.AddComponent<MeshRenderer>();fragmentRenderer.sharedMaterials=p.Materials;
                    CopyRendering(p.Source.GetComponent<MeshRenderer>(),fragmentRenderer);RenderingEquals(p.Source.GetComponent<MeshRenderer>(),fragmentRenderer);
                    var collision=go.AddComponent<MeshCollider>();var nativeCollision=p.Source.GetComponent<MeshCollider>();
                    collision.convex=nativeCollision.convex;collision.cookingOptions=nativeCollision.cookingOptions;
                    collision.sharedMaterial=nativeCollision.sharedMaterial;collision.isTrigger=nativeCollision.isTrigger;
                    collision.contactOffset=nativeCollision.contactOffset;collision.enabled=nativeCollision.enabled;
                    collision.includeLayers=nativeCollision.includeLayers;collision.excludeLayers=nativeCollision.excludeLayers;
                    collision.layerOverridePriority=nativeCollision.layerOverridePriority;collision.providesContacts=nativeCollision.providesContacts;
                    collision.sharedMesh=mesh;
                    var identity=go.AddComponent<SourcePartIdentity>();identity.SourceId=id;identity.SourcePath=(string)p.Row["source_path"];
                    identity.SourceDefinition=(string)p.Row["source_definition"];identity.SourceSha256=(string)p.Row["source_sha256"];
                    identity.Category=Category;identity.LegacyGeometrySeparated=false;identity.PreservedTriangles=(int)p.Row["restored_triangles"];
                    identity.TransferMaximumError=((JArray)p.Row["selected_faces"]).Max(f=>(float)f["maximum_corner_difference"]);
                    identity.ReconciliationReceipt=packetPath;identity.OperationStatus="partial_counterleaf_"+identity.PreservedTriangles+"_of_1054_source_faces_restored_operation_unbound";
                    resultRows.Add(new JObject{["source_id"]=id,["category"]=Category,["source_path"]=identity.SourcePath,
                        ["parent_source_id"]=p.Row["target_counterleaf_source_id"],["source_total_part_faces"]=p.Row["source_total_triangles"],
                        ["restored_faces"]=identity.PreservedTriangles,["part_source_fraction"]=p.Row["source_triangle_fraction"],
                        ["counterleaf_source_fraction"]=p.Row["counterleaf_new_face_fraction"],["operation_bound"]=false,
                        ["vertex_attributes_sha256"]=attributes,["asset"]=asset,["owner"]=PathOf(go.transform),
                        ["native_renderer_settings_copied"]=true,["static_editor_flags"]=(int)GameObjectUtility.GetStaticEditorFlags(go),
                        ["source_lightmap_index"]=fragmentRenderer.lightmapIndex,["source_realtime_lightmap_index"]=fragmentRenderer.realtimeLightmapIndex,
                        ["native_collider_properties_copied"]=true,["convex"]=collision.convex,["trigger"]=collision.isTrigger,
                        ["contact_offset"]=collision.contactOffset,["cooking_options"]=(int)collision.cookingOptions});
                }
                var frozenObjects=objects.ToArray();var result=new Result{InactiveFragments=frozenObjects,CreatedAssetPaths=created.ToArray(),CreatedFolders=folders.ToArray(),
                    Receipt=new JObject{["schema"]="chooguard.native-counterleaf-partial-staging.v1",["production_installed"]=false,
                        ["new_partial_fragments"]=6,["restored_native_faces"]=24,["complete_counterleaf_recovery"]=false,["operation_bound"]=false,
                        ["authored_pose_changed"]=false,["background_geometry_changed"]=false,["no_overlap_live_checked"]=true,
                        ["reviewed_packet_sha256"]=ReviewedPacketSha256,
                        ["renderer_state_not_copied"]="Native static-batch buffer membership and source world bounds are not reassigned. Unity derives these for each new owner; original mesh bounds/channels and static editor flags are preserved.",
                        ["plane_tolerance_model_units"]=PlaneTolerance,["partial_fragments"]=resultRows,["created_asset_paths"]=JArray.FromObject(created)}};
                result.Validate=()=>
                {
                    Require(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Stable Edit mode activation required");
                    Require(Hash(packetPath)==ReviewedPacketSha256,"Reviewed partial-source packet changed before activation");
                    Require(Hash(scene)==(string)packet["frozen_scene_sha256"],"Saved scene changed before partial fragment activation");
                    foreach(var p in prepared)Prepare(p.Row,scene,transforms,filters);
                    var actualFaces=new List<Vector3[]>();float maximumOriginalError=0;
                    foreach(var go in frozenObjects)
                    {
                        var p=prepared.Single(v=>(string)v.Row["source_id"]==go.GetComponent<SourcePartIdentity>().SourceId);
                        Frame(go.transform.localToWorldMatrix,p.Row["planned_world_matrix"],"staged partial source");
                        Require(Attributes(go.GetComponent<MeshFilter>().sharedMesh)==Attributes(p.Mesh),"Staged partial vertex channels changed");
                        Require(go.GetComponent<MeshFilter>().sharedMesh.triangles.SequenceEqual(p.Selected.SelectMany(x=>x)),"Staged partial selected face set changed");
                        Require(go.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(p.Materials)&&go.GetComponent<MeshCollider>().sharedMesh==go.GetComponent<MeshFilter>().sharedMesh,"Staged partial material/collision identity changed");
                        RenderingEquals(p.Source.GetComponent<MeshRenderer>(),go.GetComponent<MeshRenderer>());
                        var partial=go.GetComponent<MeshFilter>().sharedMesh;var vertices=partial.vertices;var indices=partial.triangles;
                        var witnesses=((JArray)p.Row["selected_faces"]).OrderBy(f=>(int)f["native_flattened_triangle_ordinal"]).ToArray();
                        Require(indices.Length==witnesses.Length*3,"Actual partial fragment witness count differs");
                        for(int face=0;face<witnesses.Length;face++)
                        {
                            var witness=witnesses[face];var world=new[]{go.transform.TransformPoint(vertices[indices[face*3]]),
                                go.transform.TransformPoint(vertices[indices[face*3+1]]),go.transform.TransformPoint(vertices[indices[face*3+2]])};
                            var original=((JArray)witness["expected_original_world_triangle"]).Select(V).ToArray();
                            var mapping=((JArray)witness["native_to_original_corner_mapping"]).Select(x=>(int)x).ToArray();
                            float error=Enumerable.Range(0,3).Max(i=>Vector3.Distance(world[i],original[mapping[i]]));
                            Require(error<=SourceTolerance,"Actual instantiated fragment differs from original authored world geometry");
                            maximumOriginalError=Mathf.Max(maximumOriginalError,error);
                            int parity=Vector3.Dot(Vector3.Cross(world[1]-world[0],world[2]-world[0]),
                                Vector3.Cross(original[1]-original[0],original[2]-original[0]))>0?1:-1;
                            Require(parity==(int)witness["native_to_original_winding_parity"],"Actual fragment reflected/native winding changed");
                            actualFaces.Add(world);
                        }
                        var c=go.GetComponent<MeshCollider>();var native=p.Source.GetComponent<MeshCollider>();
                        Require(c.convex==native.convex&&c.cookingOptions==native.cookingOptions&&c.sharedMaterial==native.sharedMaterial&&
                            c.isTrigger==native.isTrigger&&c.contactOffset==native.contactOffset&&c.enabled==native.enabled&&
                            c.includeLayers==native.includeLayers&&c.excludeLayers==native.excludeLayers&&
                            c.layerOverridePriority==native.layerOverridePriority&&c.providesContacts==native.providesContacts,"Staged native collider properties changed");
                    }
                    Require(actualFaces.Count==24,"Actual partial fragment face count differs");
                    NoOverlap(actualFaces.ToArray(),scene,new HashSet<GameObject>(frozenObjects));
                    result.Receipt["actual_fragment_world_no_overlap_checked"]=true;
                    result.Receipt["actual_fragment_maximum_original_world_corner_error"]=maximumOriginalError;
                };
                complete=true;return result;
            }
            finally
            {
                if(!complete)new Result{InactiveFragments=objects.ToArray(),CreatedAssetPaths=created.ToArray(),CreatedFolders=folders.ToArray()}.Rollback();
                foreach(var mesh in transientMeshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
#endif
