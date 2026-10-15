#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Staged exact-source surgery. Root promotes and executes this after reviewing the frozen receipts.</summary>
    public static class ReviewedNativeSlidingLeafSetup
    {
        const float Cell=.02f,Tolerance=.001f;
        sealed class Face {public string Part;public int Ordinal;public Vector3[] World;}
        sealed class Owner {public MeshFilter Filter;public MeshCollider Collider;public Mesh Mesh;public Transform Transform;public int[][] Arrays;public Mesh Replacement;public string AssetPath;}
        sealed class Hit {public Owner Owner;public int Sub,Offset;public float Error;}
        public sealed class Result {public StationDoor[] InactiveDoors;public JObject Receipt;}
        static Vector3 V(JToken t)=>new Vector3((float)t[0],(float)t[1],(float)t[2]);
        static Vector3Int Key(Vector3 v)=>new Vector3Int(Mathf.FloorToInt(v.x/Cell),Mathf.FloorToInt(v.y/Cell),Mathf.FloorToInt(v.z/Cell));
        static Dictionary<Face,Hit> Match(Face[] faces,Owner[] owners)
        {
            var grid=new Dictionary<Vector3Int,List<Face>>();
            foreach(var f in faces)
            {
                var k=Key((f.World[0]+f.World[1]+f.World[2])/3);
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                {var q=k+new Vector3Int(x,y,z);if(!grid.TryGetValue(q,out var list))grid.Add(q,list=new List<Face>());list.Add(f);}
            }
            var found=faces.ToDictionary(x=>x,x=>new List<Hit>());
            foreach(var owner in owners)
            {
                var vertices=owner.Mesh.vertices.Select(owner.Transform.TransformPoint).ToArray();
                owner.Arrays=Enumerable.Range(0,owner.Mesh.subMeshCount).Select(sub=>owner.Mesh.GetTriangles(sub)).ToArray();
                for(int sub=0;sub<owner.Arrays.Length;sub++)
                {
                    var a=owner.Arrays[sub];
                    for(int at=0;at<a.Length;at+=3)
                    {
                        var w=new[]{vertices[a[at]],vertices[a[at+1]],vertices[a[at+2]]};
                        if(!grid.TryGetValue(Key((w[0]+w[1]+w[2])/3),out var candidates))continue;
                        foreach(var face in candidates)
                        {
                            if(!SourcePartReconciler.SameTriangle(face.World[0],face.World[1],face.World[2],w[0],w[1],w[2],Tolerance))continue;
                            found[face].Add(new Hit{Owner=owner,Sub=sub,Offset=at,Error=w.Max(p=>face.World.Min(q=>Vector3.Distance(p,q)))});
                        }
                    }
                }
            }
            foreach(var pair in found)if(pair.Value.Count!=1)throw new InvalidDataException("Native face owner is missing/ambiguous: "+pair.Key.Part+":"+pair.Key.Ordinal+" matches="+pair.Value.Count);
            var result=found.ToDictionary(x=>x.Key,x=>x.Value.Single());
            if(result.Values.GroupBy(x=>(x.Owner,x.Sub,x.Offset)).Any(x=>x.Count()!=1))throw new InvalidDataException("Two source faces claimed the same native triangle");
            return result;
        }
        static void Asset(Mesh mesh,string directory,string label,List<string> created)
        {
            string path=AssetDatabase.GenerateUniqueAssetPath(directory+"/"+label+".asset");
            AssetDatabase.CreateAsset(mesh,path);created.Add(path);
            if(AssetDatabase.LoadAssetAtPath<Mesh>(path)!=mesh)throw new IOException("Mesh asset creation did not commit: "+path);
            
        }
        static void PrepareBackground(Dictionary<Face,Hit> matches,string directory,string prefix,List<string> created)
        {
            foreach(var group in matches.Values.GroupBy(x=>x.Owner))
            {
                var owner=group.Key;owner.Replacement=UnityEngine.Object.Instantiate(owner.Mesh);owner.Replacement.name=owner.Mesh.name+"_exact_native_door_faces_removed";
                int removed=0;
                for(int sub=0;sub<owner.Arrays.Length;sub++)
                {
                    var remove=new HashSet<int>(group.Where(x=>x.Sub==sub).Select(x=>x.Offset));var old=owner.Arrays[sub];var keep=new List<int>(old.Length-remove.Count*3);
                    for(int at=0;at<old.Length;at+=3){if(remove.Contains(at)){removed++;continue;}keep.Add(old[at]);keep.Add(old[at+1]);keep.Add(old[at+2]);}
                    owner.Replacement.SetTriangles(keep,sub,false);
                }
                if(removed!=group.Count())throw new InvalidDataException("Native background face count changed");
                Asset(owner.Replacement,directory,prefix+"-"+owner.Transform.gameObject.GetInstanceID(),created);owner.AssetPath=AssetDatabase.GetAssetPath(owner.Replacement);
            }
        }
        static GameObject MakeLeaf(string part,JToken row,Dictionary<Face,Hit> matches,string directory,List<string> created,float maxError,string packetPath,string sourceSha)
        {
            var hits=matches.Where(x=>x.Key.Part==part).OrderBy(x=>x.Key.Ordinal).ToArray();var original=hits[0].Value.Owner;
            if(hits.Any(x=>x.Value.Owner!=original))throw new InvalidDataException("Source leaf crosses native mesh frames; explicit frame transfer is required: "+part);
            var source=original.Mesh;var p=source.vertices;var n=source.normals;var tangent=source.tangents;var color=source.colors;
            if(n.Length!=p.Length)throw new InvalidDataException("Native source lacks preserved normals");
            var sourceUv=new List<Vector4>[8];for(int c=0;c<8;c++){sourceUv[c]=new List<Vector4>();source.GetUVs(c,sourceUv[c]);if(sourceUv[c].Count!=0&&sourceUv[c].Count!=p.Length)throw new InvalidDataException("Native UV channel length differs");}
            var uv=Enumerable.Range(0,8).Select(_=>new List<Vector4>()).ToArray();var vertices=new List<Vector3>();var normals=new List<Vector3>();var tangents=new List<Vector4>();var colors=new List<Color>();var materialIndices=new Dictionary<Material,int>();var materials=new List<Material>();var arrays=new List<List<int>>();
            var renderer=original.Filter.GetComponent<Renderer>();
            foreach(var pair in hits)
            {
                var h=pair.Value;var material=renderer.sharedMaterials[h.Sub];
                if(!materialIndices.TryGetValue(material,out int channel)){channel=materials.Count;materialIndices.Add(material,channel);materials.Add(material);arrays.Add(new List<int>());}
                for(int c=0;c<3;c++)
                {
                    int old=original.Arrays[h.Sub][h.Offset+c],next=vertices.Count;vertices.Add(p[old]);normals.Add(n[old]);arrays[channel].Add(next);
                    if(tangent.Length==p.Length)tangents.Add(tangent[old]);if(color.Length==p.Length)colors.Add(color[old]);
                    for(int u=0;u<8;u++)if(sourceUv[u].Count!=0)uv[u].Add(sourceUv[u][old]);
                }
            }
            var mesh=new Mesh{name=part,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);if(tangents.Count>0)mesh.SetTangents(tangents);if(colors.Count>0)mesh.SetColors(colors);
            for(int u=0;u<8;u++)if(uv[u].Count>0)mesh.SetUVs(u,uv[u]);mesh.subMeshCount=arrays.Count;for(int s=0;s<arrays.Count;s++)mesh.SetTriangles(arrays[s],s,false);mesh.RecalculateBounds();
            if(mesh.triangles.Length/3!=hits.Length)throw new InvalidDataException("Source leaf triangle conservation failed");
            Asset(mesh,directory,part,created);
            var go=new GameObject(part);go.SetActive(false);go.transform.SetParent(original.Transform.parent,false);go.transform.localPosition=original.Transform.localPosition;go.transform.localRotation=original.Transform.localRotation;go.transform.localScale=original.Transform.localScale;go.layer=original.Transform.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();go.AddComponent<MeshCollider>().sharedMesh=mesh;
            var id=go.AddComponent<SourcePartIdentity>();id.SourceId=part;id.SourcePath=(string)row["source_path"];id.SourceDefinition="원본 유리 자동문 문짝";id.SourceSha256=sourceSha;id.Category="automatic_door_leaf_source_part";id.LegacyGeometrySeparated=true;id.PreservedTriangles=hits.Length;id.TransferMaximumError=maxError;id.ReconciliationReceipt=packetPath;id.OperationStatus="preserved_source_geometry_operation_not_bound";
            return go;
        }
        /// <summary>Returns inactive, fully configured operators. Caller owns activation, Unity tests, final receipt and scene save.</summary>
        public static Result Stage(string packetPath,string newAssetDirectory,IReadOnlyDictionary<string,StationDoor.DoorUse> reviewedUse)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit-mode native face surgery only");
            var packet=JObject.Parse(File.ReadAllText(packetPath));var selected=((JArray)packet["bindings"]).Where(x=>reviewedUse.ContainsKey((string)x["source_button_id"])).SelectMany(x=>((JArray)x["leaf_part_ids"]).Select(y=>(string)y)).ToHashSet();
            var rows=((JArray)packet["leaves"]).Where(x=>selected.Contains((string)x["part_id"])).ToArray();if(rows.Length==0)throw new InvalidDataException("No reviewed source opening");
            var current=UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsInactive.Include,FindObjectsSortMode.None);if(current.Any(x=>selected.Contains(x.SourceId)))throw new InvalidOperationException("Exact native leaf was already staged/installed");
            var faces=new List<Face>();
            foreach(var row in rows)
            {
                var vertices=((JArray)row["current_world_vertices"]).Select(V).ToArray();var tris=(JArray)row["triangles"];var ord=(JArray)row["source_triangle_indices"];
                for(int i=0;i<tris.Count;i++)faces.Add(new Face{Part=(string)row["part_id"],Ordinal=(int)ord[i],World=((JArray)tris[i]).Select(x=>vertices[(int)x]).ToArray()});
            }
            var official=GameObject.Find("FPSWorld/공식 자료 부산역 역사").transform;
            var renderOwners=official.GetComponentsInChildren<MeshFilter>(true).Where(x=>x.sharedMesh!=null&&x.gameObject.activeInHierarchy&&x.GetComponent<Renderer>()?.enabled==true).Select(x=>new Owner{Filter=x,Mesh=x.sharedMesh,Transform=x.transform}).ToArray();
            var collisionOwners=official.GetComponentsInChildren<MeshCollider>(true).Where(x=>x.sharedMesh!=null&&x.enabled&&x.gameObject.activeInHierarchy&&!x.isTrigger).Select(x=>new Owner{Collider=x,Mesh=x.sharedMesh,Transform=x.transform}).ToArray();
            // Both current renderer backgrounds and separately retained static collision backgrounds are audited independently.
            var visual=Match(faces.ToArray(),renderOwners);var collision=Match(faces.ToArray(),collisionOwners);float error=Mathf.Max(visual.Values.Max(x=>x.Error),collision.Values.Max(x=>x.Error));
            var originalButtons=current.Where(x=>reviewedUse.ContainsKey(x.SourceId)).Select(x=>(Node:x,Parent:x.transform.parent,Status:x.OperationStatus)).ToArray();
            var assets=new List<string>();var leaves=new List<GameObject>();StationDoor[] doors=null;bool applied=false,complete=false;
            Directory.CreateDirectory(newAssetDirectory);AssetDatabase.Refresh();
            try
            {
                PrepareBackground(visual,newAssetDirectory,"native-door-render-background",assets);PrepareBackground(collision,newAssetDirectory,"native-door-collision-background",assets);
                foreach(var row in rows)leaves.Add(MakeLeaf((string)row["part_id"],row,visual,newAssetDirectory,assets,error,packetPath,(string)packet["source_sha256"]));
                applied=true; // Rollback all owners even if an individual assignment fails halfway.
                foreach(var owner in visual.Values.Select(x=>x.Owner).Distinct())owner.Filter.sharedMesh=owner.Replacement;
                foreach(var owner in collision.Values.Select(x=>x.Owner).Distinct())owner.Collider.sharedMesh=owner.Replacement;
                doors=ReviewedNativeAutomaticDoorBindings.Bind(packetPath,reviewedUse);
                foreach(var leaf in leaves)leaf.SetActive(true); // Parents/operators remain inactive, so Awake still cannot see partial configuration.
                var receipt=new JObject{["schema"]="chooguard.reviewed-native-auto-door-staging.v1",["production_installed"]=false,["operators_configured_inactive"]=doors.Length,["new_source_leaf_identities"]=leaves.Count,["removed_visual_faces"]=visual.Count,["removed_static_collision_faces"]=collision.Count,["new_dynamic_leaf_faces"]=visual.Count,["max_source_transfer_error_m"]=error,["physical_accuracy_verified"]=false,["asset_paths"]=JArray.FromObject(assets),["operator_elements"]=JArray.FromObject(doors.Select(x=>x.Element))};
                complete=true;return new Result{InactiveDoors=doors,Receipt=receipt};
            }
            finally
            {
                if(!complete)
                {
                    if(applied){foreach(var owner in visual.Values.Select(x=>x.Owner).Distinct())if(owner.Filter!=null)owner.Filter.sharedMesh=owner.Mesh;foreach(var owner in collision.Values.Select(x=>x.Owner).Distinct())if(owner.Collider!=null)owner.Collider.sharedMesh=owner.Mesh;}
                    if(doors!=null)
                    {
                        // Reattach original405 button nodes before deleting any newly created subtree.
                        foreach(var original in originalButtons)if(original.Node!=null)
                        {
                            original.Node.transform.SetParent(original.Parent,true);original.Node.OperationStatus=original.Status;
                            var control=original.Node.GetComponent<SourcePartAutomaticButton>();if(control!=null)UnityEngine.Object.DestroyImmediate(control);
                        }
                        foreach(var door in doors)if(door!=null)UnityEngine.Object.DestroyImmediate(door.gameObject);
                    }
                    foreach(var leaf in leaves)if(leaf!=null)UnityEngine.Object.DestroyImmediate(leaf);
                    foreach(var path in assets.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(path);
                }
            }
        }
    }
}
#endif
