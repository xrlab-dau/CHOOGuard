#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Twin;
using ChooGuard.Circulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Split only exact native triangles matched to the 615 archived platform tread instances.</summary>
    public static class ExistingPlatformEscalatorSetup
    {
        public const string Staging = "workers/twin/output/platform-escalators-20261009";
        public const string Output = "Assets/ChooGuard/Art/StationInterior/PlatformCirculation20261009";
        public const string Receipt = ".planning/2026-10-08-interior-runtime-completion/platform-escalator-installation.json";
        public const string SceneRoot = "FPSWorld/승강장 디딤판 운행";
        private const float Tolerance = .001f;
        private sealed class Legacy
        {
            public JToken Row; public MeshFilter Filter; public Mesh Original, Replacement;
            public int[][] Indices; public HashSet<int> Removed; public int[] SubmeshStarts;
            public Vector3[] Vertices,Normals; public Vector4[] Tangents; public Color[] Colours; public List<Vector4>[] Uv;
        }
        private sealed class Part
        {
            public JToken Row; public Mesh Mesh; public Material[] Materials; public Vector3 Origin;
            public Renderer Template; public Transform Installed; public Vector3[] ExpectedWorld;
        }
        private sealed class Prepared
        {
            public JObject Manifest; public Dictionary<string,Legacy> Legacy = new Dictionary<string,Legacy>(StringComparer.Ordinal);
            public Dictionary<string,Part> Parts = new Dictionary<string,Part>(StringComparer.Ordinal);
            public Transform Official; public StationPoints Points; public float SourceError;
        }
        private static Vector3 Vec(JToken p) => new Vector3((float)p[0],(float)p[1],(float)p[2]);
        private static Matrix4x4 Mat(JToken p) { var m = new Matrix4x4(); for (int r=0;r<4;r++) for(int c=0;c<4;c++)m[r,c]=(float)p[r][c]; return m; }
        private static string Hash(string path) { using(var sha=SHA256.Create()) using(var f=File.OpenRead(path))return Hex(sha.ComputeHash(f)); }
        private static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-","").ToLowerInvariant();
        public static string NativeGeometryDigest(Mesh mesh)
        {
            using(var stream=new MemoryStream())
            {
                using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    var v=mesh.vertices;var t=mesh.triangles;var n=mesh.normals;
                    w.Write(v.Length);w.Write(t.Length);w.Write(n.Length);
                    foreach(var p in v){w.Write(p.x);w.Write(p.y);w.Write(p.z);}foreach(int i in t)w.Write(i);
                    foreach(var p in n){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
                }
                stream.Position=0;using(var sha=SHA256.Create())return Hex(sha.ComputeHash(stream));
            }
        }
        private static void ValidateFrame(Transform actual, Matrix4x4 expected, string id)
        {
            var current=actual.localToWorldMatrix;
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)if(Mathf.Abs(current[r,c]-expected[r,c])>.00002f)
                throw new InvalidDataException("Source frame changed: "+id);
        }
        private static MeshFilter Resolve(string id)
        {
            if(!GlobalObjectId.TryParse(id,out var parsed))throw new InvalidDataException("Malformed exact native object id: "+id);
            var filter=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as MeshFilter;
            if(filter==null)throw new InvalidDataException("Original native object missing: "+id);
            return filter;
        }
        private static Prepared Prepare()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Platform source reconciliation requires edit mode");
            if(GameObject.Find(SceneRoot)!=null)throw new InvalidOperationException("Existing platform installation must be audited instead of duplicated");
            var data=new Prepared { Manifest=JObject.Parse(File.ReadAllText(Staging+"/platform-source-reconciliation.json")),
                Official=GameObject.Find("FPSWorld/공식 자료 부산역 역사")?.transform,
                Points=StationPoints.Load(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Emergency/station-points.json")) };
            if(data.Official==null || data.Official.parent==null || Vector3.Distance(data.Official.lossyScale,Vector3.one)>.0001f || Vector3.Distance(data.Official.parent.lossyScale,Vector3.one)>.0001f)
                throw new InvalidOperationException("Original official station frame missing or scaled");
            if(data.Official.gameObject.scene.isDirty)throw new InvalidOperationException("Save the preceding authorised scene edits before this independent transaction");
            if((int)data.Manifest["source_instances"]!=615||(int)data.Manifest["uniquely_matched_triangles"]!=19680||
                ((JArray)data.Manifest["lanes"]).Count!=15||(bool)data.Manifest["production_installed"])
                throw new InvalidDataException("Incorrect source reconciliation contract");
            var source=JObject.Parse(File.ReadAllText(Staging+"/shared-source-geometry.json"));
            if(Hash(Staging+"/shared-source-geometry.json")!=(string)data.Manifest["shared_geometry_sha256"])
                throw new InvalidDataException("Shared source geometry changed");
            var geometry=source["meshes"][0];var sourceVertices=((JArray)geometry["vertices"]).Select(Vec).ToArray();
            var sourceTriangles=((JArray)geometry["triangles"]).Select(t=>((JArray)t).Select(x=>(int)x).ToArray()).ToArray();
            if(sourceVertices.Length!=24||sourceTriangles.Length!=32||
                (string)geometry["vertices_sha256"]!="e282eaade559acdc9fe649d7fff693faf57d3277905b8db7e96a70022ea95dab"||
                (string)geometry["indices_sha256"]!="5a11bc5ef1695e52a2e8580cca0863021ac52f9ac6252817052460b532b4a6e1")
                throw new InvalidDataException("Original shared tread identity differs");
            foreach(var row in (JArray)data.Manifest["legacy_changes"])
            {
                var filter=Resolve((string)row["global_object_id"]);var mesh=filter.sharedMesh;
                if(!filter.transform.IsChildOf(data.Official)||mesh==null||!mesh.isReadable||
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh))!=(string)row["mesh_guid"]||
                    NativeGeometryDigest(mesh)!=(string)row["native_geometry_sha256"])
                    throw new InvalidDataException("Native source mesh changed: "+(string)row["path"]);
                ValidateFrame(filter.transform,Mat(row["world_matrix"]),(string)row["path"]);
                var renderer=filter.GetComponent<Renderer>();var expected=((JArray)row["materials"]).Select(x=>(string)x).ToArray();
                if(renderer==null||!renderer.enabled||!filter.gameObject.activeInHierarchy||
                    !renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(expected))
                    throw new InvalidDataException("Native source material or visibility changed: "+(string)row["path"]);
                var indices=Enumerable.Range(0,mesh.subMeshCount).Select(mesh.GetTriangles).ToArray();
                var starts=new int[indices.Length];for(int s=1;s<starts.Length;s++)starts[s]=starts[s-1]+indices[s-1].Length/3;
                var removed=new HashSet<int>(((JArray)row["triangles_remove"]).Select(x=>(int)x));
                if(removed.Count!=((JArray)row["triangles_remove"]).Count||removed.Any(i=>i<0||i>=mesh.triangles.Length/3))
                    throw new InvalidDataException("Repeated or invalid native source triangle ordinal");
                var channels=new List<Vector4>[8];for(int c=0;c<8;c++){channels[c]=new List<Vector4>();mesh.GetUVs(c,channels[c]);}
                data.Legacy.Add((string)row["global_object_id"],new Legacy{Row=row,Filter=filter,Original=mesh,Indices=indices,SubmeshStarts=starts,Removed=removed,
                    Vertices=mesh.vertices,Normals=mesh.normals,Tangents=mesh.tangents,Colours=mesh.colors,Uv=channels});
            }
            var claimed=new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach(var row in (JArray)data.Manifest["instances"])
                {
                    string id=(string)row["source_mesh_instance_id"];var matches=(JArray)row["matches"];
                    if(matches.Count!=32||matches.Select(x=>(int)x["source_triangle"]).Distinct().Count()!=32)
                        throw new InvalidDataException("Incomplete original source instance: "+id);
                    var worldSource=sourceVertices.Select(Mat(row["world_matrix"]).MultiplyPoint3x4).ToArray();
                    foreach(var match in matches)
                    {
                        string originalId=(string)match["legacy_global_object_id"];int ordinal=(int)match["legacy_flat_triangle"];
                        if(!data.Legacy.TryGetValue(originalId,out var legacy)||!legacy.Removed.Contains(ordinal)||!claimed.Add(originalId+":"+ordinal))
                            throw new InvalidDataException("Missing or ambiguous native source triangle claim");
                        int sub=Submesh(legacy,ordinal),local=ordinal-legacy.SubmeshStarts[sub];var ind=legacy.Indices[sub];var native=legacy.Vertices;
                        var expected=sourceTriangles[(int)match["source_triangle"]];
                        Vector3 a=legacy.Filter.transform.TransformPoint(native[ind[local*3]]),b=legacy.Filter.transform.TransformPoint(native[ind[local*3+1]]),c=legacy.Filter.transform.TransformPoint(native[ind[local*3+2]]);
                        if(!SourcePartReconciler.SameTriangle(a,b,c,worldSource[expected[0]],worldSource[expected[1]],worldSource[expected[2]],Tolerance))
                            throw new InvalidDataException("Source world triangle differs: "+id);
                        foreach(var p in new[]{a,b,c})data.SourceError=Mathf.Max(data.SourceError,worldSource.Min(x=>Vector3.Distance(p,x)));
                    }
                    data.Parts.Add(id,ExtractPart(row,data.Legacy));
                }
                if(claimed.Count!=19680||claimed.Count!=data.Legacy.Values.Sum(x=>x.Removed.Count)||data.Parts.Count!=615)
                    throw new InvalidDataException("Native/source face conservation failed");
                foreach(var row in (JArray)data.Manifest["lanes"])
                {
                    string id=(string)row["lane_id"];var lanes=data.Points.Escalators.Where(x=>x.id==id).ToArray();
                    // The catalog flag records a parallel fixed stair, not the type of this moving lane.
                    if(lanes.Length!=1||lanes[0].stairs!=(bool)row["stairs"]||lanes[0].up!=(bool)row["up"]||!id.StartsWith("esc-well-",StringComparison.Ordinal))
                        throw new InvalidDataException("Explicit platform lane differs: "+id);
                    var ids=((JArray)row["source_parts_high_to_low"]).Select(x=>(string)x).ToArray();
                    if(ids.Length!=41||ids.Distinct().Count()!=41||ids.Any(x=>!data.Parts.ContainsKey(x)|| (string)data.Parts[x].Row["lane_id"]!=id))
                        throw new InvalidDataException("Incomplete or cross-assigned source tread run: "+id);
                }
                return data;
            }
            catch { foreach(var part in data.Parts.Values)if(part.Mesh!=null)UnityEngine.Object.DestroyImmediate(part.Mesh);throw; }
        }
        public static string Audit()
        {
            var data=Prepare();
            try
            {
                float displacement=0;
                foreach(var part in data.Parts.Values)
                {
                    var vertices=part.Mesh.vertices;
                    for(int i=0;i<vertices.Length;i++)displacement=Mathf.Max(displacement,Vector3.Distance(vertices[i]+part.Origin,part.ExpectedWorld[i]));
                    if(part.Mesh.triangles.Length!=96||part.Mesh.uv.Length!=part.Mesh.vertexCount)throw new InvalidDataException("Native source UV or triangle count was lost");
                }
                if(displacement>.0001f)throw new InvalidDataException("Staged source geometry moved during separation");
                File.WriteAllText(Staging+"/live-native-audit.json",new JObject { ["status"]="CURRENT_NATIVE_SOURCE_MATCH_PASSED",["production_installed"]=false,["field_accuracy_verified"]=false,
                    ["source_treads"]=615,["native_faces"]=19680,["lanes"]=15,["source_frame_maximum_error_m"]=data.SourceError,["staged_source_displacement_m"]=displacement,
                    ["native_batches"]=data.Legacy.Count,["physical_rubber_mapping_confirmed"]=false,["hidden_return_route_created"]=false }.ToString());
                return "CG_PLATFORM_NATIVE_AUDIT treads=615 native_faces=19680 lanes=15 batches="+data.Legacy.Count+" transfer_max_m="+data.SourceError+" staged_zero_displacement_m="+displacement+" field_accuracy_verified=false";
            }
            finally { foreach(var part in data.Parts.Values)if(part.Mesh!=null)UnityEngine.Object.DestroyImmediate(part.Mesh); }
        }
        private static int Submesh(Legacy source,int flattenedOrdinal)
        {for(int s=source.Indices.Length-1;s>=0;s--)if(flattenedOrdinal>=source.SubmeshStarts[s])return s;throw new InvalidDataException("Negative source triangle ordinal");}
        private static Part ExtractPart(JToken row,Dictionary<string,Legacy> originals)
        {
            var origin=Vec(row["world_origin"]);var expectedWorld=new List<Vector3>();var vertices=new List<Vector3>();var normals=new List<Vector3>();var tangents=new List<Vector4>();var colours=new List<Color>();
            var uv=Enumerable.Range(0,8).Select(x=>new List<Vector4>()).ToArray();var mats=new List<Material>();var indices=new List<List<int>>();Renderer template=null;
            bool hasNormals=true,hasTangents=true,hasColours=true;var hasUv=Enumerable.Repeat(true,8).ToArray();
            foreach(var match in ((JArray)row["matches"]).OrderBy(x=>(int)x["source_triangle"]))
            {
                var legacy=originals[(string)match["legacy_global_object_id"]];int ordinal=(int)match["legacy_flat_triangle"],s=Submesh(legacy,ordinal),local=ordinal-legacy.SubmeshStarts[s];
                var mesh=legacy.Original;var renderer=legacy.Filter.GetComponent<Renderer>();template=template??renderer;var material=renderer.sharedMaterials[s];
                if(material==null)throw new InvalidDataException("Original tread material missing");int target=mats.IndexOf(material);if(target<0){target=mats.Count;mats.Add(material);indices.Add(new List<int>());}
                var v=legacy.Vertices;var n=legacy.Normals;var t=legacy.Tangents;var c=legacy.Colours;var sourceUv=legacy.Uv;
                for(int channel=0;channel<8;channel++)hasUv[channel]&=sourceUv[channel].Count==mesh.vertexCount;
                hasNormals&=n.Length==mesh.vertexCount;hasTangents&=t.Length==mesh.vertexCount;hasColours&=c.Length==mesh.vertexCount;
                var matrix=legacy.Filter.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                for(int k=0;k<3;k++)
                {
                    int at=legacy.Indices[s][local*3+k];indices[target].Add(vertices.Count);var world=matrix.MultiplyPoint3x4(v[at]);expectedWorld.Add(world);vertices.Add(world-origin);
                    normals.Add(n.Length==v.Length?normalMatrix.MultiplyVector(n[at]).normalized:Vector3.zero);
                    if(t.Length==v.Length){var tangent=matrix.MultiplyVector(new Vector3(t[at].x,t[at].y,t[at].z)).normalized;tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,t[at].w*Mathf.Sign(matrix.determinant)));}else tangents.Add(Vector4.zero);
                    colours.Add(c.Length==v.Length?c[at]:Color.white);
                    for(int channel=0;channel<8;channel++)uv[channel].Add(sourceUv[channel].Count==v.Length?sourceUv[channel][at]:Vector4.zero);
                }
            }
            var result=new Mesh{name="source_tread_"+(string)row["source_mesh_instance_id"],indexFormat=IndexFormat.UInt32};
            result.SetVertices(vertices);if(hasNormals)result.SetNormals(normals);if(hasTangents)result.SetTangents(tangents);if(hasColours)result.SetColors(colours);
            for(int channel=0;channel<8;channel++)if(hasUv[channel])result.SetUVs(channel,uv[channel]);result.subMeshCount=indices.Count;
            for(int s=0;s<indices.Count;s++)result.SetTriangles(indices[s],s,false);result.RecalculateBounds();
            return new Part{Row=row,Mesh=result,Materials=mats.ToArray(),Origin=origin,Template=template,ExpectedWorld=expectedWorld.ToArray()};
        }

        /// <summary>Remove exactly the selected triangle ordinals while retaining every native vertex channel and unrelated index in order.</summary>
        public static Mesh RemoveExactTriangles(Mesh original,IEnumerable<int> flattenedOrdinals)
        {
            if(original==null||!original.isReadable)throw new ArgumentException("Readable original native mesh required");
            var ordinals=flattenedOrdinals.ToArray();var remove=new HashSet<int>(ordinals);
            if(remove.Count!=ordinals.Length||remove.Any(i=>i<0||i>=original.triangles.Length/3))throw new ArgumentException("Source triangle ordinals must be unique and in range");
            var result=UnityEngine.Object.Instantiate(original);result.name=original.name+"_platform_treads_separated";int ordinal=0;
            for(int s=0;s<original.subMeshCount;s++)
            {
                var source=original.GetTriangles(s);var keep=new List<int>();
                for(int i=0;i<source.Length;i+=3,ordinal++)if(!remove.Contains(ordinal)){keep.Add(source[i]);keep.Add(source[i+1]);keep.Add(source[i+2]);}
                result.SetTriangles(keep,s,false);
            }
            result.RecalculateBounds();return result;
        }
        private static void EnsureFolder(string folder)
        {
            if(AssetDatabase.IsValidFolder(folder))return;string parent=Path.GetDirectoryName(folder).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
        }
        private static T Store<T>(T asset,string path,List<string> generated) where T:UnityEngine.Object
        {if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null)throw new InvalidOperationException("Preserve existing generated asset; explicit replacement audit required: "+path);AssetDatabase.CreateAsset(asset,path);generated.Add(path);return asset;}
        private static Vector3[] TopVertices(Part part)
        {
            var v=part.Mesh.vertices.Select(p=>p+part.Origin).ToArray();float height=v.Max(p=>p.y);
            var unique=new List<Vector3>();foreach(var p in v.Where(p=>p.y>=height-.0001f))if(!unique.Any(q=>(q-p).sqrMagnitude<1e-10f))unique.Add(p);
            if(unique.Count<3)throw new InvalidDataException("Original top tread surface missing");return unique.ToArray();
        }
        private static Vector3 Average(IEnumerable<Vector3> points){var a=points.ToArray();return a.Aggregate(Vector3.zero,(sum,p)=>sum+p)/a.Length;}
        public static Vector3[] CollapseCollinear(Vector3[] path,float tolerance=.0001f)
        {
            if(path==null||path.Length<2)throw new ArgumentException("Actual source top path required");var result=new List<Vector3>{path[0]};
            for(int i=1;i+1<path.Length;i++)
            {
                var ab=path[i+1]-result[result.Count-1];float f=ab.sqrMagnitude>1e-8f?Vector3.Dot(path[i]-result[result.Count-1],ab)/ab.sqrMagnitude:0;
                var foot=result[result.Count-1]+ab*f;
                if(f<0||f>1||Vector3.Distance(foot,path[i])>tolerance)result.Add(path[i]);
            }
            result.Add(path[path.Length-1]);return result.ToArray();
        }
        public static string Apply()
        {
            var data=Prepare();var generated=new List<string>();GameObject installedRoot=null;bool committed=false;
            var colliderBefore=data.Official.GetComponentsInChildren<MeshCollider>(true).ToDictionary(x=>x,x=>x.sharedMesh);
            var existingBindings=UnityEngine.Object.FindObjectsByType<EscalatorDriveBinding>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>(component:x,id:x.lane_id)).ToArray();
            var laneReceipts=new JArray();int rideCount=0;
            try
            {
                EnsureFolder(Output);EnsureFolder(Output+"/Treads");EnsureFolder(Output+"/Background");
                foreach(var legacy in data.Legacy.Values)
                {
                    legacy.Replacement=Store(RemoveExactTriangles(legacy.Original,legacy.Removed),Output+"/Background/"+(string)legacy.Row["mesh_guid"]+"_"+legacy.Filter.name+".asset",generated);
                }
                installedRoot=new GameObject("승강장 디딤판 운행");Undo.RegisterCreatedObjectUndo(installedRoot,"Separate original platform treads");
                installedRoot.transform.SetParent(data.Official.parent,false);installedRoot.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);installedRoot.transform.localScale=Vector3.one;
                foreach(var lane in (JArray)data.Manifest["lanes"])
                {
                    string id=(string)lane["lane_id"];var parts=((JArray)lane["source_parts_high_to_low"]).Select(x=>data.Parts[(string)x]).ToArray();
                    var at=new GameObject(id);at.SetActive(false);at.transform.SetParent(installedRoot.transform,false);at.transform.position=parts[0].Origin;
                    var binding=at.AddComponent<EscalatorDriveBinding>();binding.lane_id=id;var motion=at.AddComponent<EscalatorMotion>();
                    motion.Loops=Array.Empty<EscalatorMotion.Loop>();motion.Surfaces=Array.Empty<EscalatorMotion.Surface>();motion.StepSurfaces=Array.Empty<EscalatorMotion.StepSurface>();
                    var run=at.AddComponent<PlatformEscalatorRun>();run.Up=(bool)lane["up"];run.SourcePitch=(float)lane["observed_tread_pitch_m"];
                    run.ObservedOrigins=parts.Select(p=>at.transform.InverseTransformPoint(p.Origin)).ToArray();run.Treads=new Transform[41];
                    var axis=parts[40].Origin-parts[0].Origin;axis.y=0;axis.Normalize();run.ExposureAxis=at.transform.InverseTransformDirection(axis);
                    float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                    for(int i=0;i<parts.Length;i++)
                    {
                        var part=parts[i];var node=new GameObject("원본 디딤판 · "+((string)part.Row["source_mesh_instance_id"]).Substring(0,12));node.transform.SetParent(at.transform,false);node.transform.position=part.Origin;
                        node.isStatic=false;part.Installed=node.transform;run.Treads[i]=node.transform;
                        var filter=node.AddComponent<MeshFilter>();filter.sharedMesh=Store(part.Mesh,Output+"/Treads/"+(string)part.Row["source_mesh_instance_id"]+".asset",generated);
                        var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterials=part.Materials;renderer.shadowCastingMode=part.Template.shadowCastingMode;
                        renderer.receiveShadows=part.Template.receiveShadows;renderer.lightProbeUsage=part.Template.lightProbeUsage;renderer.reflectionProbeUsage=part.Template.reflectionProbeUsage;renderer.renderingLayerMask=part.Template.renderingLayerMask;
                        var identity=node.AddComponent<SourcePartIdentity>();identity.SourceId=(string)part.Row["source_component_id"];identity.SourceDefinition="계단_역.P#001";identity.SourcePath=((string)part.Row["source_path"]).Replace("/geometry","");
                        identity.Category="escalator_tread_component";identity.SourceSha256=(string)data.Manifest["source_sha256"];identity.ReconciliationReceipt=Receipt;
                        identity.LegacyGeometrySeparated=true;identity.TransferMaximumError=data.SourceError;identity.PreservedTriangles=32;identity.OperationStatus="visible_source_tread_common_lane_drive_clipped_entry_surface";
                        var world=part.Mesh.vertices.Select(node.transform.TransformPoint).ToArray();
                        foreach(var p in world){float d=Vector3.Dot(at.transform.InverseTransformPoint(p),run.ExposureAxis);minimum=Mathf.Min(minimum,d);maximum=Mathf.Max(maximum,d);}
                    }
                    run.ExposureMinimum=minimum;run.ExposureMaximum=maximum;
                    var tops=parts.Select(TopVertices).ToArray();var topPath=tops.Select(Average).ToList();
                    float first=tops[0].Min(p=>Vector3.Dot(p,axis)),last=tops[40].Max(p=>Vector3.Dot(p,axis));
                    topPath.Insert(0,Average(tops[0].Where(p=>Vector3.Dot(p,axis)<=first+.0001f)));
                    topPath.Add(Average(tops[40].Where(p=>Vector3.Dot(p,axis)>=last-.0001f)));
                    var path=CollapseCollinear(topPath.ToArray());
                    foreach(var point in topPath)
                    {
                        float error=float.PositiveInfinity;for(int k=0;k+1<path.Length;k++){var delta=path[k+1]-path[k];float f=delta.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector3.Dot(point-path[k],delta)/delta.sqrMagnitude):0;error=Mathf.Min(error,Vector3.Distance(point,path[k]+delta*f));}
                        if(error>.0002f)throw new InvalidDataException("Collapsed ride path differs from actual native tread surface: "+id);
                    }
                    var side=Vector3.Cross(Vector3.up,axis).normalized;
                    float width=tops[0].Max(p=>Vector3.Dot(p,side))-tops[0].Min(p=>Vector3.Dot(p,side));
                    if(width<1.19f||width>1.21f)throw new InvalidDataException("Actual original tread width differs: "+id);
                    for(int segment=0;segment+1<path.Length;segment++)
                    {
                        var a=path[segment];var b=path[segment+1];float length=Vector3.Distance(a,b);if(length<.0001f)throw new InvalidDataException("Collapsed source ride path contains a zero segment");
                        var volume=new GameObject("디딤판 이송 · "+segment);volume.transform.SetParent(at.transform,false);var rotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);
                        volume.transform.SetPositionAndRotation((a+b)*.5f+rotation*Vector3.up,rotation);
                        var box=volume.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(width,2,length);box.center=Vector3.zero;
                        var ride=volume.AddComponent<EscalatorRide>();ride.Direction=(bool)lane["up"]?EscalatorRide.RideDirection.Down:EscalatorRide.RideDirection.Up;
                        // Every volume's +Z follows high -> low; the Up/Down enum denotes axis sign here.
                        ride.HorizontalOnly=Mathf.Abs(a.y-b.y)<.0001f;rideCount++;
                    }
                    at.SetActive(true);motion.RebuildBindings();EditorUtility.SetDirty(run);EditorUtility.SetDirty(binding);EditorUtility.SetDirty(motion);
                    laneReceipts.Add(new JObject{["lane_id"]=id,["up"]=(bool)lane["up"],["original_treads"]=41,["source_pitch_m"]=run.SourcePitch,
                        ["ride_path_world"]=JArray.FromObject(path.Select(v=>new[]{v.x,v.y,v.z})),["original_top_width_m"]=width,
                        ["footprint_minimum"]=minimum,["footprint_maximum"]=maximum,["return_route_modelled"]=false,
                        ["moving_rubber_identified"]=false,["original_frame_and_glass_retained"]=true});
                }
                foreach(var legacy in data.Legacy.Values){Undo.RecordObject(legacy.Filter,"Separate exact source tread triangles");legacy.Filter.sharedMesh=legacy.Replacement;EditorUtility.SetDirty(legacy.Filter);}
                float closedError=0;
                foreach(var part in data.Parts.Values)
                {
                    var actual=part.Mesh.vertices.Select(part.Installed.TransformPoint).ToArray();var expected=part.ExpectedWorld;
                    for(int i=0;i<actual.Length;i++)closedError=Mathf.Max(closedError,Vector3.Distance(actual[i],expected[i]));
                }
                if(closedError>.0001f)throw new InvalidDataException("Installed zero-phase source geometry moved");
                foreach(var pair in colliderBefore)if(pair.Key==null||pair.Key.sharedMesh!=pair.Value)throw new InvalidDataException("Original platform collision surface changed");
                foreach(var old in existingBindings)if(old.component==null||old.component.lane_id!=old.id)throw new InvalidDataException("Existing connected flight changed");
                Platform6BankB1Replay.AfterMovingWellsBuilt();Platform6BanksB24PlacementReplay.AfterMovingWellsBuilt(); // parent-only rigid placement; run-local fields unchanged
                var report=new JObject{["schema"]="chooguard.platform-source-installation.v1",["production_installed"]=false,["field_accuracy_verified"]=false,
                    ["source_sha256"]=(string)data.Manifest["source_sha256"],["reconciliation_sha256"]=Hash(Staging+"/platform-source-reconciliation.json"),
                    ["original_treads"]=615,["preserved_source_triangles"]=19680,["lanes_connected"]=15,["native_batches_changed"]=data.Legacy.Count,
                    ["source_frame_max_error_m"]=data.SourceError,["zero_phase_binding_displacement_m"]=closedError,["original_mesh_colliders_preserved"]=colliderBefore.Count,
                    ["existing_lane_bindings_preserved"]=existingBindings.Length,["ride_volumes"]=rideCount,["closed_return_routes_created"]=0,["lanes"]=laneReceipts,
                    ["legacy_mesh_changes"]=new JArray(data.Legacy.Values.Select(x=>new JObject{["path"]=(string)x.Row["path"],["object_id"]=(string)x.Row["global_object_id"],
                        ["original_asset"]=AssetDatabase.GetAssetPath(x.Original),["replacement_asset"]=AssetDatabase.GetAssetPath(x.Replacement),["removed_triangles"]=x.Removed.Count}))};
                File.WriteAllText(Receipt+".pending",report.ToString());AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(data.Official.gameObject.scene);
                if(!EditorSceneManager.SaveScene(data.Official.gameObject.scene))throw new IOException("Platform source separation scene save did not commit");
                committed=true;report["production_installed"]=true;report["saved_scene_sha256"]=Hash(data.Official.gameObject.scene.path);
                File.WriteAllText(Receipt+".pending",report.ToString());File.Copy(Receipt+".pending",Receipt,true);File.Delete(Receipt+".pending");
                return "CG_PLATFORM_ESCALATOR_BINDING lanes=15 original_treads=615 preserved_triangles=19680 existing_bindings="+existingBindings.Length+" ride_volumes="+rideCount+" return_routes=0 rubber_mapping=unconfirmed field_accuracy_verified=false";
            }
            finally
            {
                if(!committed)
                {
                    foreach(var legacy in data.Legacy.Values)if(legacy.Filter!=null)legacy.Filter.sharedMesh=legacy.Original;
                    if(installedRoot!=null)UnityEngine.Object.DestroyImmediate(installedRoot);
                    foreach(string path in generated.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(path);
                    foreach(var part in data.Parts.Values)if(part.Mesh!=null&&!EditorUtility.IsPersistent(part.Mesh))UnityEngine.Object.DestroyImmediate(part.Mesh);
                    File.Delete(Receipt+".pending");
                }
            }
        }
    }
}
#endif
