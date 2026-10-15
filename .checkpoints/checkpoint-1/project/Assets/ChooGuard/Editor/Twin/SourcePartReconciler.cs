#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Separate only geometry already present in the production scene. Transfer tolerance is not a site accuracy claim.</summary>
    public static class SourcePartReconciler
    {
        public const string SourceDirectory = "asset-library/research-public/2026-10-08/measured-twin-parts/semantic";
        public const string ModelPath = "Assets/ChooGuard/Art/Fps/SourceParts20261008/station-operable-candidates.fbx";
        public const string OutputDirectory = ".planning/2026-10-08-interior-runtime-completion";
        const float Cell = .02f, Tolerance = .001f;
        sealed class Face { public Vector3 A,B,C; public int Mesh,Index,Matches; public string Material; }
        sealed class Legacy { public MeshFilter Filter; public int[][] Indices; public int[][] Owners; }
        sealed class State
        {
            public GameObject Candidate;
            public Transform Official;
            public MeshFilter[] Meshes;
            public List<Face> Faces = new List<Face>();
            public List<Legacy> Legacy = new List<Legacy>();
            public int[] Totals,Matched,Ambiguous;
            public float FrameError;
        }
        static State audited;
        static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x/Cell),Mathf.FloorToInt(p.y/Cell),Mathf.FloorToInt(p.z/Cell));
        public static bool SameTriangle(Vector3 a,Vector3 b,Vector3 c,Vector3 x,Vector3 y,Vector3 z,float tolerance)
        {
            float t=tolerance*tolerance;
            return Near(a,x,t)&&((Near(b,y,t)&&Near(c,z,t))||(Near(b,z,t)&&Near(c,y,t))) ||
                   Near(a,y,t)&&((Near(b,x,t)&&Near(c,z,t))||(Near(b,z,t)&&Near(c,x,t))) ||
                   Near(a,z,t)&&((Near(b,x,t)&&Near(c,y,t))||(Near(b,y,t)&&Near(c,x,t)));
        }
        public static string Install()
        {
            if(GameObject.Find("FPSWorld/원본 분리 부품")!=null) throw new InvalidOperationException("Existing installation must be audited instead of duplicated");
            Audit();var state=audited;
            var metadata=JObject.Parse(File.ReadAllText(SourceDirectory+"/candidate-manifest.json"));
            var initialWorld=state.Meshes.ToDictionary(x=>x.name,x=>x.sharedMesh.vertices.Select(v=>x.transform.TransformPoint(v)).ToArray());
            var keep=new HashSet<string>();var good=new bool[state.Meshes.Length];
            for(int i=0;i<good.Length;i++){good[i]=state.Totals[i]>0&&state.Matched[i]==state.Totals[i]&&state.Ambiguous[i]==0;if(good[i])keep.Add(state.Meshes[i].name.Substring(2));}
            var changes=new List<(MeshFilter filter,Mesh original,Mesh replacement)>();
            var colliderChanges=new List<(MeshCollider collider,Mesh original)>();
            var transferred=new List<(Transform node,Transform parent)>();
            var hiddenRenderers=new List<(Renderer renderer,bool enabled)>();
            var cutRows=new JArray();int cut=0;bool installed=false;
            string assetDirectory="Assets/ChooGuard/Art/Fps/SourceParts20261008/SeparatedBackground";
            Directory.CreateDirectory(assetDirectory);AssetDatabase.Refresh();
            try
            {
                foreach(var legacy in state.Legacy)
                {
                    var arrays=new int[legacy.Indices.Length][];int removed=0;
                    for(int s=0;s<arrays.Length;s++)
                    {
                        var output=new List<int>(legacy.Indices[s].Length);
                        for(int t=0;t<legacy.Owners[s].Length;t++)
                        {
                            int fi=legacy.Owners[s][t];
                            if(fi>=0&&good[state.Faces[fi].Mesh]){removed++;continue;}
                            output.Add(legacy.Indices[s][t*3]);output.Add(legacy.Indices[s][t*3+1]);output.Add(legacy.Indices[s][t*3+2]);
                        }
                        arrays[s]=output.ToArray();
                    }
                    if(removed==0)continue;
                    var original=legacy.Filter.sharedMesh;var replacement=UnityEngine.Object.Instantiate(original);replacement.name=original.name+"_source_parts_separated";
                    for(int s=0;s<arrays.Length;s++)replacement.SetTriangles(arrays[s],s,false);
                    string target=assetDirectory+"/"+legacy.Filter.name+".asset";
                    if(AssetDatabase.LoadAssetAtPath<Mesh>(target)!=null)throw new InvalidOperationException("Existing generated mesh asset requires explicit update audit: "+target);
                    AssetDatabase.CreateAsset(replacement,target);changes.Add((legacy.Filter,original,replacement));
                    cutRows.Add(new JObject { ["renderer"]=legacy.Filter.name,["original_mesh_asset"]=AssetDatabase.GetAssetPath(original),["separated_mesh_asset"]=target,["original_triangles"]=original.triangles.Length/3,["removed_triangles"]=removed,["remaining_triangles"]=replacement.triangles.Length/3 });cut+=removed;
                }
                int expected=Enumerable.Range(0,good.Length).Where(i=>good[i]).Sum(i=>state.Totals[i]);
                if(cut!=expected||cut==0)throw new InvalidDataException("Face conservation failed before installation: removed="+cut+" separated="+expected);
                foreach(var change in changes)
                {
                    Undo.RecordObject(change.filter,"Separate original station parts");change.filter.sharedMesh=change.replacement;
                    foreach(var collider in state.Official.GetComponentsInChildren<MeshCollider>(true))
                        if(collider.sharedMesh==change.original && SameFrame(collider.transform,change.filter.transform))
                        {Undo.RecordObject(collider,"Separate source collision faces");colliderChanges.Add((collider,collider.sharedMesh));collider.sharedMesh=change.replacement;}
                }
                foreach(var filter in state.Meshes)
                    if(!keep.Contains(filter.name.Substring(2)))UnityEngine.Object.DestroyImmediate(filter.gameObject);
                // Prune empty transport nodes while preserving the complete hierarchy of retained original mesh instances.
                foreach(var node in state.Candidate.GetComponentsInChildren<Transform>(true).Reverse())
                    if(node!=state.Candidate.transform&&node.childCount==0&&node.GetComponent<MeshFilter>()==null)UnityEngine.Object.DestroyImmediate(node.gameObject);
                state.Candidate.name="원본 분리 부품";
                foreach(var node in state.Candidate.GetComponentsInChildren<Transform>(true))node.gameObject.hideFlags=HideFlags.None;
                state.Candidate.transform.SetParent(state.Official.parent,true);state.Candidate.SetActive(true);
                foreach(var filter in state.Candidate.GetComponentsInChildren<MeshFilter>(true))
                {
                    filter.gameObject.isStatic=false;
                    if(filter.sharedMesh.triangles.Length>0){var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
                }
                var nodes=state.Candidate.GetComponentsInChildren<Transform>(true).ToDictionary(x=>x.name,x=>x,StringComparer.Ordinal);
                var identities=new List<(JToken row,ChooGuard.App.Fps.Twin.SourcePartIdentity identity)>();
                foreach(var row in (JArray)metadata["semantic_components"])
                {
                    string id=(string)row["source_id"];
                    if((bool)row["hidden"]||!((JArray)row["descendant_mesh_ids"]).All(x=>keep.Contains((string)x))||!nodes.TryGetValue("N_"+id,out var node))continue;
                    var identity=node.gameObject.AddComponent<ChooGuard.App.Fps.Twin.SourcePartIdentity>();
                    identity.SourceId=id;identity.SourceDefinition=(string)row["source_definition"];identity.SourcePath=(string)row["source_path"];identity.Category=(string)row["category"];identity.SourceSha256=(string)metadata["source_sha256"];identity.ReconciliationReceipt=OutputDirectory+"/source-part-installation.json";identity.LegacyGeometrySeparated=true;identity.TransferMaximumError=state.FrameError;
                    identity.PreservedTriangles=node.GetComponentsInChildren<MeshFilter>(true).Sum(x=>x.sharedMesh.triangles.Length/3);
                    identity.OperationStatus="preserved_source_geometry_operation_not_bound";identities.Add((row,identity));
                }
                var operationRows=new JArray();int doors=0,existingDoors=0,buttons=0;
                foreach(var pair in identities)
                {
                    if(pair.identity.Category!="door_component")continue;
                    var geometry=metadata["component_geometry"][pair.identity.SourceId];var minimum=ReadVector(geometry["local_bounds"]["min"]);var maximum=ReadVector(geometry["local_bounds"]["max"]);var size=maximum-minimum;
                    // Source assemblies, jambs and frames never rotate as if they were a leaf.
                    if(size.x<.65f||size.x>1.2f||size.y>.23f||size.z<1.8f||size.z>2.3f||Mathf.Abs(minimum.x)>.04f){pair.identity.OperationStatus="door_leaf_pivot_requires_review";continue;}
                    var sourceMatrix=ReadMatrix(pair.row["world_matrix"]);
                    Vector3 centre=SourceToWorld(sourceMatrix.MultiplyPoint3x4(new Vector3((minimum.x+maximum.x)*.5f,(minimum.y+maximum.y)*.5f,minimum.z)),state.Official);
                    Vector3 normal=SourceVectorToWorld(sourceMatrix.MultiplyVector(Vector3.up),state.Official);normal.y=0;normal.Normalize();
                    var nearby=UnityEngine.Object.FindObjectsByType<ChooGuard.App.Fps.Facilities.StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>Mathf.Abs(x.World(Vector3.zero).y-centre.y)<.15f&&Vector3.Distance(x.World(new Vector3(0,0,0)),centre)<(x.Width*.5f+size.x*.5f+.1f)).ToArray();
                    if(nearby.Length>0)
                    {
                        var matchingLeaves=nearby.SelectMany(x=>x.Leaves.Select(l=>(door:x,leaf:l))).Where(x=>x.leaf.Pivot!=null&&Vector3.Distance(x.leaf.Pivot.position,pair.identity.transform.position)<.03f&&Mathf.Abs(x.leaf.Length-size.x)<.05f).ToArray();
                        if(matchingLeaves.Length==1)
                        {
                            var mapped=matchingLeaves[0];
                            foreach(var renderer in mapped.leaf.Pivot.GetComponentsInChildren<Renderer>(true)){hiddenRenderers.Add((renderer,renderer.enabled));renderer.enabled=false;}
                            transferred.Add((pair.identity.transform,pair.identity.transform.parent));
                            pair.identity.transform.SetParent(mapped.leaf.Pivot,true);
                            pair.identity.OperationStatus="existing_door_leaf_source_visual_bound";existingDoors++;
                            operationRows.Add(new JObject {["source_id"]=pair.identity.SourceId,["operation"]="existing_hinged_door_source_leaf",["door_element"]=mapped.door.Element,["hinge_transfer_error_m"]=Vector3.Distance(mapped.leaf.Pivot.position,pair.identity.transform.position)});
                        }
                        else pair.identity.OperationStatus="source_panel_existing_controller_leaf_mapping_unconfirmed";
                        continue;
                    }
                    var wrapper=new GameObject("원본 문 · "+pair.identity.SourceId);
                    wrapper.transform.SetParent(pair.identity.transform.parent,true);wrapper.transform.SetPositionAndRotation(pair.identity.transform.position,Quaternion.LookRotation(normal,Vector3.up));
                    var pivot=new GameObject("원본 경첩").transform;pivot.SetParent(wrapper.transform,false);pair.identity.transform.SetParent(pivot,true);
                    var door=wrapper.AddComponent<ChooGuard.App.Fps.Facilities.StationDoor>();door.Element="source-"+pair.identity.SourceId;door.Label=pair.identity.SourceDefinition;door.Kind=ChooGuard.App.Fps.Facilities.StationDoor.DoorKind.Swing;door.Use=ChooGuard.App.Fps.Facilities.StationDoor.DoorUse.Staff;door.Width=size.x;door.Height=size.z;door.Centre=wrapper.transform.InverseTransformPoint(centre);door.Normal=Vector3.forward;door.Along=Vector3.right;
                    door.Leaves=new[]{new ChooGuard.App.Fps.Facilities.StationDoor.Leaf {Pivot=pivot,Swing=90,Length=size.x}};
                    pair.identity.OperationStatus="hinged_source_leaf_bound";doors++;
                    operationRows.Add(new JObject {["source_id"]=pair.identity.SourceId,["operation"]="hinged_source_leaf",["position"]=JArray.FromObject(new[]{centre.x,centre.y,centre.z}),["pivot_basis"]="source_origin_at_leaf_edge",["swing_degrees_simulation_tuning"]=90});
                }
                foreach(var pair in identities)
                {
                    if(pair.identity.Category!="automatic_door_button")continue;
                    var at=pair.identity.transform.position;
                    var targets=UnityEngine.Object.FindObjectsByType<ChooGuard.App.Fps.Facilities.StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.Kind==ChooGuard.App.Fps.Facilities.StationDoor.DoorKind.Sliding&&x.Usable&&Mathf.Abs(x.Local(at).z)<.3f&&x.Local(at).y>.6f&&x.Local(at).y<1.6f&&Mathf.Abs(x.Local(at).x)<x.Width*.5f+.5f).ToArray();
                    if(targets.Length!=1){pair.identity.OperationStatus="push_plate_target_unconfirmed";continue;}
                    var button=pair.identity.gameObject.AddComponent<ChooGuard.App.Fps.Twin.SourcePartAutomaticButton>();button.Door=targets[0];button.Plate=pair.identity.GetComponentInChildren<MeshFilter>();button.PressDirectionLocal=Vector3.left;buttons++;pair.identity.OperationStatus="push_plate_mapped_to_existing_operator";
                    operationRows.Add(new JObject {["source_id"]=pair.identity.SourceId,["operation"]="automatic_door_push_plate",["door_element"]=targets[0].Element,["mapping_basis"]="unique_existing_opening_plane_and_button_mount_height",["press_travel_simulation_tuning_m"] = .002f});
                }
                float installedFrameError=0;
                foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                {
                    if(!initialWorld.TryGetValue(filter.name,out var originalWorld))continue;
                    var vertices=filter.sharedMesh.vertices;
                    if(vertices.Length!=originalWorld.Length)throw new InvalidDataException("Source vertex count changed during operation binding");
                    for(int v=0;v<vertices.Length;v++)installedFrameError=Mathf.Max(installedFrameError,Vector3.Distance(filter.transform.TransformPoint(vertices[v]),originalWorld[v]));
                }
                if(installedFrameError>Tolerance)throw new InvalidDataException("Source closed geometry moved while binding: "+installedFrameError);
                Platform6BankB1Replay.AfterSourcePartsBuilt();Platform6BanksB24PlacementReplay.AfterSourcePartsBuilt(); // exact B1 source placement, after original zero-phase checks
                var report=new JObject {["schema"]="chooguard.production-source-installation.v1",["physical_accuracy_verified"]=false,["production_installed"]=true,["source_mesh_instances"]=keep.Count,["preserved_triangles"]=cut,["source_components"]=identities.Count,["new_source_door_leaves"]=doors,["existing_door_controllers_preserved"]=existingDoors,["mapped_push_plates"]=buttons,["source_frame_max_error_m"]=state.FrameError,["operation_bind_max_vertex_displacement_m"]=installedFrameError,["mesh_changes"]=cutRows,["operations"]=operationRows,["component_operations"]=JArray.FromObject(identities.Select(x=>new {source_id=x.identity.SourceId,category=x.identity.Category,operation_status=x.identity.OperationStatus}))};
                string receipt=OutputDirectory+"/source-part-installation.json";
                report["production_installed"]=false;
                File.WriteAllText(receipt+".pending",report.ToString());
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(state.Official.gameObject.scene);
                if(!EditorSceneManager.SaveScene(state.Official.gameObject.scene))throw new IOException("Source separation scene save did not commit");
                installed=true;
                report["production_installed"]=true;
                using(var sha=System.Security.Cryptography.SHA256.Create()) using(var stream=File.OpenRead(state.Official.gameObject.scene.path))report["saved_scene_sha256"]=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                File.WriteAllText(receipt+".pending",report.ToString());
                File.Move(receipt+".pending",receipt);
                Debug.Log("CG_SOURCE_PART_INSTALL meshes="+keep.Count+" conserved_triangles="+cut+" new_doors="+doors+" buttons="+buttons);
                return "Installed "+keep.Count+" exact legacy mesh instances; new source doors="+doors+", mapped buttons="+buttons;
            }
            finally
            {
                if(!installed)
                {
                    foreach(var transfer in transferred)if(transfer.node!=null)transfer.node.SetParent(transfer.parent,true);
                    foreach(var entry in hiddenRenderers)if(entry.renderer!=null)entry.renderer.enabled=entry.enabled;
                    foreach(var change in changes){change.filter.sharedMesh=change.original;AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(change.replacement));}
                    foreach(var change in colliderChanges)change.collider.sharedMesh=change.original;
                    if(state.Candidate!=null)UnityEngine.Object.DestroyImmediate(state.Candidate);
                    File.Delete(OutputDirectory+"/source-part-installation.json.pending");
                    audited=null;
                }
            }
        }
        static bool SameFrame(Transform a,Transform b) => Vector3.Distance(a.position,b.position)<.0001f&&Quaternion.Angle(a.rotation,b.rotation)<.001f&&Vector3.Distance(a.lossyScale,b.lossyScale)<.0001f;
        static Vector3 ReadVector(JToken v) => new Vector3((float)v[0],(float)v[1],(float)v[2]);
        static Matrix4x4 ReadMatrix(JToken matrix) {var result=Matrix4x4.identity;for(int r=0;r<4;r++)for(int c=0;c<4;c++)result[r,c]=(float)matrix[r][c];return result;}
        static Vector3 SourceVectorToWorld(Vector3 v,Transform official) => official.rotation*new Vector3(-v.x,v.z,-v.y);

        static bool Near(Vector3 a,Vector3 b,float t) => (a-b).sqrMagnitude<=t;
        static Vector3 SourceToWorld(Vector3 v,Transform official)
        {
            // Native FBX changes source Z-up metres to (-X,Z,-Y). Preserve the actual existing official root frame.
            return official.position + official.rotation * new Vector3(-v.x,v.z,-v.y);
        }
        public static void Audit()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Source surgery requires edit mode");
            Directory.CreateDirectory(OutputDirectory);
            foreach(var prior in Resources.FindObjectsOfTypeAll<GameObject>()) if(prior.name=="SOURCE_PARTS_AUDIT_PREVIEW"&&!EditorUtility.IsPersistent(prior)) UnityEngine.Object.DestroyImmediate(prior);
            var official = GameObject.Find("FPSWorld/공식 자료 부산역 역사")?.transform;
            if(official==null || Vector3.Distance(official.lossyScale,Vector3.one)>.0001f) throw new InvalidOperationException("Existing station frame missing or scaled");
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if(asset==null) throw new InvalidOperationException("Source candidate model missing");
            var candidate=UnityEngine.Object.Instantiate(asset); candidate.name="SOURCE_PARTS_AUDIT_PREVIEW"; candidate.hideFlags=HideFlags.DontSave;
            candidate.transform.SetPositionAndRotation(official.position+official.rotation*asset.transform.localPosition,official.rotation*asset.transform.localRotation);
            candidate.transform.localScale=asset.transform.localScale;
            candidate.SetActive(false);
            bool auditCompleted=false;
            try
            {
            var state=new State { Candidate=candidate,Official=official,Meshes=candidate.GetComponentsInChildren<MeshFilter>(true) };
            var receipt=new Dictionary<string,JObject>(StringComparer.Ordinal);
            foreach(var line in File.ReadLines(SourceDirectory+"/world-face-receipts.jsonl")) {var item=JObject.Parse(line);receipt.Add((string)item["unity_name"],item);}
            var oldMaterialType=typeof(ChooGuard.Editor.MvpOfficialStationBinder).GetNestedType("SourceMaterial",System.Reflection.BindingFlags.NonPublic);
            var oldNames=new Dictionary<string,string>();
            foreach(var entry in (JArray)JObject.Parse(File.ReadAllText("asset-library/research-public/2026-09-21/quality-a/station-model-source/official-station-material-manifest.json"))["materials"])
            {
                var record=JsonUtility.FromJson(entry.ToString(),oldMaterialType);
                string encoded=JsonUtility.ToJson(record),hash;
                using(var sha=System.Security.Cryptography.SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(encoded))).Replace("-","").ToLowerInvariant();
                oldNames["source-"+hash.Substring(0,20)]=(string)entry["sourceName"];
            }
            var metadata=JObject.Parse(File.ReadAllText(SourceDirectory+"/candidate-manifest.json"));
            var hidden = new HashSet<string>(((JArray)metadata["objects"]).Where(x=>(bool)x["effective_hidden"]).Select(x=>"N_"+(string)x["source_id"]));
            state.Totals=new int[state.Meshes.Length];state.Matched=new int[state.Meshes.Length];state.Ambiguous=new int[state.Meshes.Length];
            var buckets=new Dictionary<Vector3Int,List<int>>();
            for(int m=0;m<state.Meshes.Length;m++)
            {
                var filter=state.Meshes[m]; if(!receipt.TryGetValue(filter.name,out var item)) throw new InvalidDataException("Unexpected imported source mesh "+filter.name);
                var vertices=filter.sharedMesh.vertices;var matrix=filter.transform.localToWorldMatrix;
                var sourceVertices=((JArray)item["source_world_vertices"]).Select(v=>SourceToWorld(new Vector3((float)v[0],(float)v[1],(float)v[2]),official)).ToArray();
                // Every imported vertex must belong to the original source geometry in the known production frame.
                foreach(var v in vertices)
                {
                    var w=matrix.MultiplyPoint3x4(v);float distance=float.PositiveInfinity;
                    foreach(var p in sourceVertices) distance=Mathf.Min(distance,(w-p).sqrMagnitude);
                    state.FrameError=Mathf.Max(state.FrameError,Mathf.Sqrt(distance));
                }
                if(state.FrameError>Tolerance) throw new InvalidDataException("Imported/source coordinate transfer error "+state.FrameError+" exceeds tolerance; no scene geometry changed");
                if(hidden.Contains(filter.name)) continue;
                state.Totals[m]=filter.sharedMesh.triangles.Length/3;
                var sourceMaterials=filter.GetComponent<Renderer>().sharedMaterials;
                for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                {
                var triangles=filter.sharedMesh.GetTriangles(sub);
                string materialName=sourceMaterials[sub].name;
                if(materialName=="Material.001")materialName="Material";
                for(int t=0;t<triangles.Length;t+=3)
                {
                    var f=new Face { A=matrix.MultiplyPoint3x4(vertices[triangles[t]]),B=matrix.MultiplyPoint3x4(vertices[triangles[t+1]]),C=matrix.MultiplyPoint3x4(vertices[triangles[t+2]]),Mesh=m,Index=t/3,Material=materialName };
                    var key=Key((f.A+f.B+f.C)/3);if(!buckets.TryGetValue(key,out var bucket)) {bucket=new List<int>();buckets.Add(key,bucket);}bucket.Add(state.Faces.Count);state.Faces.Add(f);
                }
                }
            }
            foreach(var filter in official.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(mesh==null||!mesh.isReadable) throw new InvalidDataException("Legacy mesh must be readable "+filter.name);
                var vertices=mesh.vertices;var world=new Vector3[vertices.Length];var matrix=filter.transform.localToWorldMatrix;
                for(int i=0;i<world.Length;i++) world[i]=matrix.MultiplyPoint3x4(vertices[i]);
                var legacy=new Legacy {Filter=filter,Indices=new int[mesh.subMeshCount][],Owners=new int[mesh.subMeshCount][]};
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    var material=filter.GetComponent<Renderer>().sharedMaterials[s];
                    if(!oldNames.TryGetValue(material.name,out string sourceMaterial)) throw new InvalidDataException("Unknown legacy material identity: "+material.name);
                    var indices=mesh.GetTriangles(s);var owners=Enumerable.Repeat(-1,indices.Length/3).ToArray();legacy.Indices[s]=indices;legacy.Owners[s]=owners;
                    for(int t=0;t<indices.Length;t+=3)
                    {
                        var a=world[indices[t]];var b=world[indices[t+1]];var c=world[indices[t+2]];var centre=(a+b+c)/3;var low=Key(centre-Vector3.one*Tolerance);var high=Key(centre+Vector3.one*Tolerance);int found=-1;
                        for(int dx=low.x;dx<=high.x;dx++) for(int dy=low.y;dy<=high.y;dy++) for(int dz=low.z;dz<=high.z;dz++)
                            if(buckets.TryGetValue(new Vector3Int(dx,dy,dz),out var bucket)) foreach(int fi in bucket)
                            {
                                var f=state.Faces[fi];
                                if(f.Material!=sourceMaterial || !SameTriangle(a,b,c,f.A,f.B,f.C,Tolerance))continue;
                                if(found>=0) {state.Ambiguous[f.Mesh]++;state.Ambiguous[state.Faces[found].Mesh]++;found=-2;} else if(found==-1) found=fi; else state.Ambiguous[f.Mesh]++;
                            }
                        if(found>=0) {owners[t/3]=found;state.Faces[found].Matches++;}
                    }
                }
                state.Legacy.Add(legacy);
            }
            foreach(var face in state.Faces) {if(face.Matches==1)state.Matched[face.Mesh]++;else if(face.Matches>1)state.Ambiguous[face.Mesh]++;}
            var rows=new JArray();int complete=0;
            for(int m=0;m<state.Meshes.Length;m++)
            {
                bool match=state.Totals[m]>0&&state.Matched[m]==state.Totals[m]&&state.Ambiguous[m]==0;if(match)complete++;
                rows.Add(new JObject { ["source_id"]=state.Meshes[m].name.Substring(2),["source_triangles"]=state.Totals[m],["unique_matching_triangles"]=state.Matched[m],["ambiguous_triangles"]=state.Ambiguous[m],["complete"]=match });
            }
            candidate.SetActive(false);audited=state;
            File.WriteAllText(OutputDirectory+"/source-part-audit.json",new JObject { ["schema"]="chooguard.production-source-reconciliation.v1",["physical_accuracy_verified"]=false,["production_installed"]=false,["transfer_tolerance_m"]=Tolerance,["source_frame_max_error_m"]=state.FrameError,["complete_meshes"]=complete,["candidate_meshes"]=state.Meshes.Length,["legacy_meshes"]=state.Legacy.Count,["meshes"]=rows }.ToString());
            Debug.Log("CG_SOURCE_PART_AUDIT complete="+complete+" candidates="+state.Meshes.Length+" frame_error="+state.FrameError);
            auditCompleted=true;
            }
            finally {if(!auditCompleted){if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);audited=null;}}
        }
    }
}
#endif
