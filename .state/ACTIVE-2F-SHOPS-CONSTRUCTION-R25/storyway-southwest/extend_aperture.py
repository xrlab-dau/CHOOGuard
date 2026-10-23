from pathlib import Path
import hashlib
p=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-southwest');f=p/'ApplyStoreR25.cs';s=f.read_text();original='Assets/ChooGuard/Art/StationInterior/PhotoLocationTwinR15_Batch04_20261019/R15_WALL_001.asset';sha=hashlib.sha256(Path(original).read_bytes()).hexdigest()
code=r'''
 static JObject OwnApertureReceipt;
 static void BuildOwnSourceEntrance(string folder,JObject packet){
  Guard();const string expectedPath="ORIGINAL_PATH",expectedSHA="ORIGINAL_SHA";
  GlobalObjectId gid;if(!GlobalObjectId.TryParse("GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-718422858-0",out gid))throw new Exception("Own generated front identity parse");
  var go=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;if(go==null||go.name!="R11-WALL-FOOTPRINT-015_P1_1")throw new Exception("Exact own generated storefront required");
  var mf=go.GetComponent<MeshFilter>();var original=mf==null?null:mf.sharedMesh;
  if(original==null||AssetDatabase.GetAssetPath(original)!=expectedPath||Sha(Root+"/"+expectedPath)!=expectedSHA||original.subMeshCount!=8)throw new Exception("Current generated source identity changed");
  var old=original.vertices;var ix=original.GetTriangles(4);if(ix.Length!=36)throw new Exception("Current generated pane source is not the identified box");
  float xmin=float.PositiveInfinity,xmax=float.NegativeInfinity,zmin=float.PositiveInfinity,zmax=float.NegativeInfinity,ymin=float.PositiveInfinity,ymax=float.NegativeInfinity;
  foreach(var i in ix){var w=go.transform.TransformPoint(old[i]);float x=-.34290633335f*w.x-.93936960061f*w.z,z=.93936960061f*w.x-.34290633335f*w.z;xmin=Mathf.Min(xmin,x);xmax=Mathf.Max(xmax,x);zmin=Mathf.Min(zmin,z);zmax=Mathf.Max(zmax,z);ymin=Mathf.Min(ymin,w.y);ymax=Mathf.Max(ymax,w.y);}
  if(Mathf.Abs(zmin+24.659073f)>.002f||Mathf.Abs(zmax+24.647072f)>.002f||Mathf.Abs(ymin-7.1f)>.002f||Mathf.Abs(ymax-9.62f)>.002f)throw new Exception("Identified generated glass face changed");
  // Exact current mullion inside faces. The100mm original low threshold/frame is kept.
  const float left=-48.26294f,right=-46.96796f,head=9.45f;
  var mesh=UnityEngine.Object.Instantiate(original);mesh.name="R25_SW_OWN_GENERATED_GLASS_SOURCE_ENTRANCE";
  var verts=new List<Vector3>(original.vertices);var normals=new List<Vector3>(original.normals);var uv=new List<Vector2>(original.uv);while(normals.Count<verts.Count)normals.Add(Vector3.up);while(uv.Count<verts.Count)uv.Add(Vector2.zero);var ids=new List<int>();
  Action<float,float,float,float> pane=(xa,xb,ya,yb)=>{var cube=Primitive("box");int start=verts.Count;var vv=cube.vertices;var nn=cube.normals;var tt=cube.uv;for(int i=0;i<vv.Length;i++){float qx=(xa+xb)*.5f+vv[i].x*(xb-xa),qy=(zmin+zmax)*.5f+vv[i].z*(zmax-zmin),wy=(ya+yb)*.5f+vv[i].y*(yb-ya);var w=new Vector3(-.34290633335f*qx+.93936960061f*qy,wy,-.93936960061f*qx-.34290633335f*qy);verts.Add(go.transform.InverseTransformPoint(w));normals.Add(go.transform.InverseTransformDirection(new Vector3(-.34290633335f*nn[i].x+.93936960061f*nn[i].z,nn[i].y,-.93936960061f*nn[i].x-.34290633335f*nn[i].z)).normalized);uv.Add(tt[i]);}foreach(int i in cube.triangles)ids.Add(start+i);};
  pane(xmin,left,ymin,ymax);pane(right,xmax,ymin,ymax);pane(left,right,head,ymax);
  mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(ids,4);mesh.RecalculateBounds();string path=folder+"/MESH_OWN_GENERATED_FRONTGLASS_OPEN_ENTRY.asset";AssetDatabase.CreateAsset(mesh,path);CreatedAssets.Add(path);
  var colliders=go.GetComponents<MeshCollider>();if(colliders.Length!=1||colliders[0].sharedMesh==null)throw new Exception("Exact generated current collision component required");
  var cp=(JObject)packet["generated_collider_aperture"];var originalCollision=colliders[0].sharedMesh;string collisionPath=(string)cp["source_mesh"];
  if(AssetDatabase.GetAssetPath(originalCollision)!=collisionPath||Sha(Root+"/"+collisionPath)!=(string)cp["source_sha256"]||originalCollision.vertexCount!=(int)cp["original_vertex_count"])throw new Exception("Exact separate generated collision source changed");
  var collisionMesh=UnityEngine.Object.Instantiate(originalCollision);collisionMesh.name="R25_SW_OWN_GENERATED_COLLISION_LOCAL_ENTRANCE";var cv=new List<Vector3>(originalCollision.vertices);cv.AddRange(cp["appended_vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])));collisionMesh.SetVertices(cv);int sub=0;foreach(var tr in cp["submesh_triangles"])collisionMesh.SetTriangles(tr.Select(v=>(int)v).ToArray(),sub++);collisionMesh.RecalculateNormals();collisionMesh.RecalculateBounds();string collisionNew=folder+"/MESH_OWN_GENERATED_COLLISION_OPEN_ENTRY.asset";AssetDatabase.CreateAsset(collisionMesh,collisionNew);CreatedAssets.Add(collisionNew);
  Undo.RecordObject(mf,"Own source visible storefront entrance");Undo.RecordObject(colliders[0],"Own source visible entrance collider");mf.sharedMesh=mesh;colliders[0].sharedMesh=null;colliders[0].sharedMesh=collisionMesh;EditorUtility.SetDirty(mf);EditorUtility.SetDirty(colliders[0]);
  OwnApertureReceipt=new JObject{["authority"]="OWN-GENERATED-STOREFRONT-APERTURE-AUTHORITY-002.json",["object_id"]=gid.ToString(),["original_mesh"]=expectedPath,["original_mesh_sha256"]=expectedSHA,["own_derivative_mesh"]=path,["changed_submesh"]=4,["aperture_qx_min"]=left,["aperture_qx_max"]=right,["aperture_worldY_min"]=ymin,["aperture_worldY_max"]=head,["original_low_frame_top_worldY"]=7.1f,["other_submeshes_frames_native_floor_and_TRS_preserved"]=true,["matching_collider_saved"]=true,["original_separate_collider_source"]=collisionPath,["independent_collider_derivative"]=collisionNew,["collider_original_sha256"]=cp["source_sha256"],["collision_outside_entry_preserved"]=true,["metric_status"]="CURRENT_MULLION_FACE_BOUNDED_PHOTO_PROPORTION_MODEL_NOT_SURVEY"};
 }
'''.replace('ORIGINAL_PATH',original).replace('ORIGINAL_SHA',sha)
s=s.replace(' public static string Apply()',code+'\n public static string Apply()')
s=s.replace('   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);','   BuildOwnSourceEntrance(folder,p);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);')
s=s.replace('["QA_Play_tests_reopen_capture"]=false};','["QA_Play_tests_reopen_capture"]=false,["own_generated_storefront_entrance"]=OwnApertureReceipt};')
f.write_text(s)
print('own aperture function inserted; source SHA',sha)
