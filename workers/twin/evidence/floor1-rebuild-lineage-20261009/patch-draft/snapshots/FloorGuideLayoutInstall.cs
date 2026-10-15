using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ChooGuard.Editor.Twin;
using ChooGuard.App.Fps.Facilities;

public static class FloorGuideLayoutInstall
{
    const string Work=".planning/2026-10-08-interior-runtime-completion/floor-guide-layout";
    const string Art="Assets/ChooGuard/Art/StationInterior/FloorGuideLayout20261009";
    static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    static Vector3 V(JToken t)=>new Vector3((float)t[0],(float)t[1],(float)t[2]);
    static Quaternion Q(JToken t)=>new Quaternion((float)t[0],(float)t[1],(float)t[2],(float)t[3]);
    static JArray Vec(Vector3 v)=>new JArray(v.x,v.y,v.z);
    static string PathOf(Transform t){var n=new List<string>();while(t!=null){n.Add(t.name);t=t.parent;}n.Reverse();return string.Join("/",n);}
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;Folder(Path.GetDirectoryName(p).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\','/'),Path.GetFileName(p));}
    static void CheckFrame(Transform t,JToken rows){for(int r=0;r<4;r++)for(int c=0;c<4;c++)if(Mathf.Abs(t.localToWorldMatrix[r,c]-(float)rows[r][c])>.000025f)throw new InvalidDataException("Changed source frame: "+PathOf(t));}
    static Transform[] ResolveMotion(JToken row,Transform[] transforms,StationDoor[] doors)
    {if((string)row["kind"]=="door")return doors.Where(d=>d.Element==(string)row["door_element"]).Select(d=>d.transform).ToArray();return transforms.Where(t=>PathOf(t)==(string)row["path"]).ToArray();}
    sealed class Patch{public JToken Row;public Mesh Original,Replacement;public string Asset,ExpectedDigest;public MeshFilter[] Filters;public MeshCollider[] Colliders;}
    sealed class Motion{public JToken Row;public Transform Target;public Vector3 Before,Delta;public Quaternion BeforeRotation,Rotation;public bool Rigid;}
    static Mesh BuildPatch(Mesh mesh,JToken row)
    {
        var vertices=mesh.vertices;var ids=((JArray)row["vertex_indices"]).Select(i=>(int)i).ToArray();
        if(ids.Distinct().Count()!=ids.Length||ids.Any(i=>i<0||i>=vertices.Length)||ids.Length!=((JArray)row["expected_positions"]).Count||ids.Length!=((JArray)row["replacement_positions"]).Count)throw new InvalidDataException("Invalid vertex selection");
        var clone=UnityEngine.Object.Instantiate(mesh);clone.name=mesh.name+"_FloorGuide1F";clone.hideFlags=HideFlags.None;
        if(ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone)!=ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh))throw new InvalidDataException("Clone changed vertex channels");
        for(int i=0;i<ids.Length;i++){if(Vector3.Distance(vertices[ids[i]],V(row["expected_positions"][i]))>.000002f)throw new InvalidDataException("Source corner changed");vertices[ids[i]]=V(row["replacement_positions"][i]);}
        clone.vertices=vertices;
        int appended=0;
        if(row["append_vertices"] is JArray extra)
        {
            appended=extra.Count;
            if(appended==0||mesh.normals.Length!=mesh.vertexCount||mesh.tangents.Length!=mesh.vertexCount||mesh.uv.Length!=mesh.vertexCount||mesh.colors.Length!=0||mesh.boneWeights.Length!=0||mesh.bindposes.Length!=0||mesh.blendShapeCount!=0)throw new InvalidDataException("Unsupported appended source channels");
            for(int channel=1;channel<8;channel++){var values=new List<Vector4>();mesh.GetUVs(channel,values);if(values.Count!=0)throw new InvalidDataException("Unreviewed additional UV channel");}
            var normals=row["append_normals"] as JArray;var tangents=row["append_tangents"] as JArray;var uv=row["append_uv0"] as JArray;
            if(normals?.Count!=appended||tangents?.Count!=appended||uv?.Count!=appended)throw new InvalidDataException("Incomplete appended vertex attributes");
            clone.vertices=vertices.Concat(extra.Select(V)).ToArray();
            clone.normals=mesh.normals.Concat(normals.Select(V)).ToArray();
            clone.tangents=mesh.tangents.Concat(tangents.Select(t=>new Vector4((float)t[0],(float)t[1],(float)t[2],(float)t[3]))).ToArray();
            clone.uv=mesh.uv.Concat(uv.Select(t=>new Vector2((float)t[0],(float)t[1]))).ToArray();
            if(extra.Concat(normals).Concat(tangents).Concat(uv).SelectMany(t=>((JArray)t).Select(x=>(float)x)).Any(x=>!float.IsFinite(x))||normals.Select(V).Any(n=>Mathf.Abs(n.sqrMagnitude-1)>.0001f)||tangents.Any(t=>Mathf.Abs(new Vector3((float)t[0],(float)t[1],(float)t[2]).sqrMagnitude-1)>.0001f||Mathf.Abs(Mathf.Abs((float)t[3])-1)>.0001f)||Enumerable.Range(0,appended).Any(i=>Mathf.Abs(Vector3.Dot(V(normals[i]),V(tangents[i])))>.0001f))throw new InvalidDataException("Invalid appended vertex attributes");
        }
        if(row["removed_triangle_ordinals"] is JArray removed)
        {
            if(mesh.subMeshCount!=1||mesh.GetTopology(0)!=MeshTopology.Triangles)throw new InvalidDataException("BOH topology must be the reviewed single triangle batch");
            var old=mesh.GetTriangles(0);var faces=removed.Select(x=>(int)x).ToHashSet();
            if(faces.Count!=removed.Count||faces.Any(i=>i<0||i>=old.Length/3))throw new InvalidDataException("Invalid face selection");
            var retained=Enumerable.Range(0,old.Length/3).Where(i=>!faces.Contains(i)).SelectMany(i=>old.Skip(i*3).Take(3)).ToList();
            if(row["added_triangles"] is JArray added)
            {
                var newIndices=added.SelectMany(x=>((JArray)x).Select(i=>(int)i)).ToArray();
                if(newIndices.Length!=added.Count*3||newIndices.Any(i=>i<0||i>=mesh.vertexCount+appended||(!(i>=mesh.vertexCount)&&!ids.Contains(i))||retained.Contains(i)))throw new InvalidDataException("New profile would change a retained face");
                retained.AddRange(newIndices);
            }
            clone.SetTriangles(retained,0,false);
        }
        if(row["attribute_overrides"] is JArray attrs)
        {
            var normals=clone.normals;var tangents=clone.tangents;var uv=clone.uv;
            if(normals.Length!=clone.vertexCount||tangents.Length!=clone.vertexCount||uv.Length!=clone.vertexCount||attrs.Select(x=>(int)x["index"]).Distinct().Count()!=attrs.Count)throw new InvalidDataException("Unexpected profile attributes");
            foreach(var a in attrs){int i=(int)a["index"];if(!ids.Contains(i))throw new InvalidDataException("Attribute outside selected profile");normals[i]=V(a["normal"]);var t=a["tangent"];tangents[i]=new Vector4((float)t[0],(float)t[1],(float)t[2],(float)t[3]);uv[i]=new Vector2((float)a["uv0"][0],(float)a["uv0"][1]);}
            clone.normals=normals;clone.tangents=tangents;clone.uv=uv;
        }
        if(row["added_triangles"] is JArray triangles)
        {
            var positions=clone.vertices;var normals=clone.normals;
            foreach(var face in triangles){int a=(int)face[0],b=(int)face[1],c=(int)face[2];var cross=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]);if(cross.sqrMagnitude<1e-12f||Vector3.Dot(cross,normals[a]+normals[b]+normals[c])<=0)throw new InvalidDataException("Degenerate or inverted appended face");}
        }
        clone.RecalculateBounds();return clone;
    }
    public static string Apply(string[] args)
    {
        if(args==null||args.Length!=1)throw new ArgumentException("Apply <reviewed packet path>");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||scene.isDirty||EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Saved stable actual station required");
        var packet=JObject.Parse(File.ReadAllText(args[0]));
        string batch=(string)packet["batch_id"]??"floor1";
        if(!System.Text.RegularExpressions.Regex.IsMatch(batch,"^[a-z0-9_-]+$"))throw new InvalidDataException("Unsafe batch name");
        string art=batch=="floor1"?Art:Art+"/"+batch;
        if(((JArray)packet["rejected_owners"]).Count!=0||Hash(scene.path)!=(string)packet["scene_sha256"]||Hash((string)packet["guide"])!=(string)packet["guide_sha256"]||Hash((string)packet["proposal"])!=(string)packet["proposal_sha256"])throw new InvalidDataException("Unreviewed packet or changed evidence/scene");
        if(packet["fixture_attribution"]!=null&&Hash((string)packet["fixture_attribution"])!=(string)packet["fixture_attribution_sha256"])throw new InvalidDataException("Changed fixture assembly attribution");
        foreach(var evidence in packet["additional_evidence"] as JArray??new JArray())if(Hash((string)evidence["path"])!=(string)evidence["sha256"])throw new InvalidDataException("Changed supporting geometry evidence");
        var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.gameObject.scene==scene).ToArray();
        var colliders=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.gameObject.scene==scene).ToArray();
        var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var doors=UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(d=>d.gameObject.scene==scene).ToArray();
        var filterBefore=filters.ToDictionary(f=>f,f=>f.sharedMesh);var colliderBefore=colliders.ToDictionary(c=>c,c=>c.sharedMesh);
        var positionBefore=transforms.ToDictionary(t=>t,t=>t.localPosition);var rotationBefore=transforms.ToDictionary(t=>t,t=>t.localRotation);var scaleBefore=transforms.ToDictionary(t=>t,t=>t.localScale);
        var patches=new List<Patch>();var motions=new List<Motion>();var originalFiles=new Dictionary<string,string>();
        foreach(var row in (JArray)packet["mesh_patches"])
        {
            string path=(string)row["source_asset"];
            if(Hash(path)!=(string)row["source_file_sha256"]||AssetDatabase.AssetPathToGUID(path)!=(string)row["source_asset_guid"])throw new InvalidDataException("Changed source asset: "+path);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null||!mesh.isReadable||EditorUtility.IsDirty(mesh)||mesh.vertexCount!=(int)row["vertices"])throw new InvalidDataException("Unavailable/dirty source mesh");
            originalFiles[path]=Hash(path);originalFiles[path+".meta"]=Hash(path+".meta");
            var owners=filters.Where(f=>f.sharedMesh==mesh).ToArray();var listed=(JArray)row["owners"];
            if(owners.Length!=listed.Count)throw new InvalidDataException("Unlisted shared renderer instance: "+path);
            foreach(var owner in owners)
            {
                var proof=listed.SingleOrDefault(x=>(string)x["global_object_id"]==GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString());
                if(proof==null||PathOf(owner.transform)!=(string)proof["path"])throw new InvalidDataException("Owner identity changed");
                if(PathOf(owner.transform).StartsWith("FPSWorld/",StringComparison.Ordinal))throw new InvalidDataException("Protected native building shell");
                CheckFrame(owner.transform,proof["world_matrix"]);
            }
            var cols=colliders.Where(c=>c.sharedMesh==mesh).ToArray();
            if(row["boh_evidence"]!=null&&Hash((string)row["boh_evidence"])!=(string)row["boh_evidence_sha256"])throw new InvalidDataException("Changed BOH evidence");
            if(row["collider_owners"] is JArray colliderProof)
            {
                if(cols.Length!=colliderProof.Count)throw new InvalidDataException("Unlisted collider owner");
                foreach(var col in cols){var proof=colliderProof.SingleOrDefault(x=>(string)x["global_object_id"]==GlobalObjectId.GetGlobalObjectIdSlow(col).ToString());if(proof==null||PathOf(col.transform)!=(string)proof["path"]||PathOf(col.transform).StartsWith("FPSWorld/",StringComparison.Ordinal))throw new InvalidDataException("Collider owner changed");CheckFrame(col.transform,proof["world_matrix"]);}
            }
            else foreach(var col in cols)if(!owners.Any(f=>f.transform.localToWorldMatrix==col.transform.localToWorldMatrix))throw new InvalidDataException("Unlisted collider source frame");
            var clone=BuildPatch(mesh,row);patches.Add(new Patch{Row=row,Original=mesh,Replacement=clone,Filters=owners,Colliders=cols,ExpectedDigest=ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone)});
        }
        foreach(var row in (JArray)packet["movable_transforms"])
        {
            var targets=ResolveMotion(row,transforms,doors);if(targets.Length!=1)throw new InvalidDataException("Missing/ambiguous movable object: "+row);
            var t=targets[0];if(PathOf(t).StartsWith("FPSWorld/",StringComparison.Ordinal))throw new InvalidDataException("Protected native object movement");
            bool rigid=row["world_rigid_delta"]!=null;var rotation=rigid?Q(row["world_rigid_delta"]["rotation"]):Quaternion.identity;
            if(Mathf.Abs(Quaternion.Dot(rotation,rotation)-1)>.00001f)throw new InvalidDataException("Invalid rigid motion rotation");
            motions.Add(new Motion{Row=row,Target=t,Before=t.position,BeforeRotation=t.rotation,Delta=V(rigid?row["world_rigid_delta"]["translation"]:row["world_delta"]),Rotation=rotation,Rigid=rigid});
        }
        if(motions.Select(m=>m.Target).Distinct().Count()!=motions.Count||motions.Any(a=>motions.Any(b=>a!=b&&a.Target.IsChildOf(b.Target))))throw new InvalidDataException("Nested/repeated instance movement");
        string backup=Work+"/before-"+batch+"-guide.unity";File.Copy(scene.path,backup,false);
        var created=new List<string>();var registryBefore=File.Exists(FloorGuideLayoutRegistry.Registry)?File.ReadAllText(FloorGuideLayoutRegistry.Registry):null;
        try
        {
            Folder(art);int number=0;
            foreach(var p in patches){p.Asset=art+"/1F-"+(number++).ToString("D3")+".asset";if(File.Exists(p.Asset))throw new InvalidDataException("Existing output must not be overwritten");AssetDatabase.CreateAsset(p.Replacement,p.Asset);created.Add(p.Asset);foreach(var f in p.Filters)f.sharedMesh=p.Replacement;foreach(var c in p.Colliders)c.sharedMesh=p.Replacement;}
            foreach(var m in motions){m.Target.position=m.Rotation*m.Before+m.Delta;if(m.Rigid)m.Target.rotation=m.Rotation*m.BeforeRotation;}Physics.SyncTransforms();
            foreach(var f in filters)if(f.sharedMesh!=filterBefore[f]&&!patches.Any(p=>p.Filters.Contains(f)&&f.sharedMesh==p.Replacement))throw new InvalidDataException("Unrelated renderer changed");
            foreach(var c in colliders)if(c.sharedMesh!=colliderBefore[c]&&!patches.Any(p=>p.Colliders.Contains(c)&&c.sharedMesh==p.Replacement))throw new InvalidDataException("Unrelated collision changed");
            foreach(var t in transforms)if((t.localRotation!=rotationBefore[t]&&!motions.Any(m=>m.Target==t&&m.Rigid&&Quaternion.Angle(t.rotation,m.Rotation*m.BeforeRotation)<.0001f))||t.localScale!=scaleBefore[t]||(!motions.Any(m=>m.Target==t)&&t.localPosition!=positionBefore[t]))throw new InvalidDataException("Unrelated local pose changed");
            foreach(var p in patches)if(ArchitecturalCorrectionRegistry.CompleteChannelDigest(p.Replacement)!=p.ExpectedDigest)throw new InvalidDataException("Unexpected replacement mutation");
            AssetDatabase.SaveAssets();foreach(var f in originalFiles)if(Hash(f.Key)!=f.Value)throw new InvalidDataException("Source asset changed on disk");
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed");
            var oldCorrections=JObject.Parse(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry));
            var registry=registryBefore==null?new JObject{["schema"]="chooguard.floor-guide-layout.v1",["site_accuracy_verified"]=false,["meshes"]=new JArray(),["motions"]=new JArray(),["units"]=new JArray()}:JObject.Parse(registryBefore);var entries=(JArray)registry["meshes"];
            foreach(var p in patches)
            {
                string actualBaseline=AssetDatabase.GetAssetPath(p.Original);var guidePrior=entries.SingleOrDefault(r=>(string)r["replacement_asset"]==actualBaseline);
                var prior=((JArray)oldCorrections["entries"]).SingleOrDefault(r=>(string)r["replacement_asset"]==actualBaseline);
                string builder=guidePrior!=null?(string)guidePrior["source_asset"]:prior==null?actualBaseline:(string)prior["source_asset"];
                string baseline=guidePrior!=null?(string)guidePrior["baseline_asset"]:actualBaseline;
                string sourceDigest=guidePrior!=null?(string)guidePrior["source_digest"]:ArchitecturalCorrectionRegistry.CompleteChannelDigest(p.Original);
                var variants=guidePrior?["verified_rebuild_variants"]?.DeepClone();
                foreach(var row in entries.Where(r=>(string)r["source_asset"]==builder).ToArray())row.Remove();
                var entry=new JObject{["source_asset"]=builder,["baseline_asset"]=baseline,["patch_baseline_asset"]=actualBaseline,["source_digest"]=sourceDigest,["replacement_asset"]=p.Asset,["replacement_digest"]=ArchitecturalCorrectionRegistry.CompleteChannelDigest(p.Replacement),["packet"]=args[0],["packet_sha256"]=Hash(args[0])};
                if(variants!=null)entry["verified_rebuild_variants"]=variants;
                entries.Add(entry);
            }
            foreach(var m in motions)((JArray)registry["motions"]).Add(m.Row.DeepClone());foreach(var u in (JArray)packet["units"])((JArray)registry["units"]).Add(u.DeepClone());
            registry["scene_sha256"]=Hash(scene.path);registry["guide"]=packet["guide"].DeepClone();File.WriteAllText(FloorGuideLayoutRegistry.Registry,registry.ToString());
            var receipt=new JObject{["production_installed"]=true,["floor"]=1,["before_scene_sha256"]=packet["scene_sha256"],["scene_sha256"]=Hash(scene.path),["packet"]=args[0],["packet_sha256"]=Hash(args[0]),["mesh_count"]=patches.Count,["moved_instances"]=motions.Count,["source_assets_unchanged"]=true,["unrelated_local_poses_unchanged"]=true,["units"]=packet["units"].DeepClone(),["meshes"]=new JArray(patches.Select(p=>new JObject{["source_asset"]=AssetDatabase.GetAssetPath(p.Original),["replacement_asset"]=p.Asset,["source_digest"]=ArchitecturalCorrectionRegistry.CompleteChannelDigest(p.Original),["replacement_digest"]=ArchitecturalCorrectionRegistry.CompleteChannelDigest(p.Replacement),["collider_count"]=p.Colliders.Length})),["motions"]=new JArray(motions.Select(m=>new JObject{["global_object_id"]=GlobalObjectId.GetGlobalObjectIdSlow(m.Target).ToString(),["path"]=PathOf(m.Target),["before_position"]=Vec(m.Before),["after_position"]=Vec(m.Target.position)}))};
            File.WriteAllText(Work+"/"+batch+"-installation.json",receipt.ToString());return "Saved guide-aligned 1F "+batch+": "+patches.Count+" mesh batches, "+motions.Count+" instances";
        }
        catch
        {
            foreach(var p in patches){foreach(var f in p.Filters)f.sharedMesh=p.Original;foreach(var c in p.Colliders)c.sharedMesh=p.Original;}foreach(var m in motions){m.Target.position=m.Before;m.Target.rotation=m.BeforeRotation;}
            EditorSceneManager.SaveScene(scene);File.Copy(backup,scene.path,true);foreach(var path in created)AssetDatabase.DeleteAsset(path);
            if(registryBefore!=null)File.WriteAllText(FloorGuideLayoutRegistry.Registry,registryBefore);else if(File.Exists(FloorGuideLayoutRegistry.Registry))File.Delete(FloorGuideLayoutRegistry.Registry);throw;
        }
    }
}
