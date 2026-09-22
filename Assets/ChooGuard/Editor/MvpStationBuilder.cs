using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor
{
 public static class MvpStationBuilder
 {
  const string Folder="Assets/ChooGuard/Settings/MvpStation";
  const int Layer=29;
  [Serializable] class Site { public Feature[] features; }
  [Serializable] class Feature { public string id,kind,reference; public Point[] points; }
  [Serializable] class Point { public float x,z; }
  static Material floorMat,edgeMat,glassMat,railMat,greenMat,trainMat,peopleMat;
  [MenuItem("ChooGuard/MVP/Build Busan Station")]
  public static void Build()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행하세요.");
   var scene=SceneManager.GetSceneByPath(MvpWorkspaceBuilder.ScenePath);
   if(!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("저장된 훈련 장면을 먼저 여세요.");
   var workspace=scene.GetRootGameObjects().Select(x=>x.GetComponent<MvpWorkspace>()).FirstOrDefault(x=>x!=null);
   if(workspace==null) throw new InvalidOperationException("훈련 화면이 없습니다.");
   Directory.CreateDirectory(Folder); AssetDatabase.Refresh();ConfigureHeroAssets();
   floorMat=Mat("바닥",new Color(.63f,.61f,.54f)); edgeMat=Mat("구조",new Color(.18f,.3f,.35f)); glassMat=Mat("유리",new Color(.16f,.3f,.35f)); railMat=Mat("선로",new Color(.12f,.17f,.22f)); greenMat=Mat("식재",new Color(.24f,.52f,.36f));
   trainMat=Mat("열차",Color.white); trainMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChooGuard/ThirdParty/Models/TrainKitMvp/Textures/colormap.png"));
   peopleMat=Mat("인물",Color.white); peopleMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChooGuard/ThirdParty/Models/MiniCharacters/Textures/colormap.png"));
   var old=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="부산역 훈련 공간"); if(old!=null) UnityEngine.Object.DestroyImmediate(old);
   var root=new GameObject("부산역 훈련 공간"); SceneManager.MoveGameObjectToScene(root,scene); root.transform.position=new Vector3(0,-1000,0);
   var view=root.AddComponent<MvpStationView>(); view.FloorRoots=new Transform[3]; for(int i=0;i<3;i++) view.FloorRoots[i]=Node(root.transform,(i+1)+"층");
   view.PlatformAnchor=Node(root.transform,"승강장 목적지"); view.PlatformAnchor.localPosition=new Vector3(48,.05f,4);
   view.ConcourseAnchor=Node(root.transform,"대합실 목적지"); view.ConcourseAnchor.localPosition=new Vector3(-20,5.05f,0);
   view.ExitAnchor=Node(root.transform,"출입구 목적지"); view.ExitAnchor.localPosition=new Vector3(-64,.05f,-4);
   var envelope=Node(root.transform,"전체 역사 외관"); view.WholeEnvelope=envelope;
   var data=JsonUtility.FromJson<Site>(File.ReadAllText("asset-library/space-references/busan-reconstruction/busan-site-selected.json"));
   var main=data.features.First(x=>x.id=="165346389"); var full=Points(main); var interior=Clip(full,68);
   HeroModel(envelope,"부산역 곡면 유리 역사","BusanFacade",Vector3.zero,0);
   for(int f=0;f<2;f++) { MvpFloorBuilder.Build(view.FloorRoots[f],"공개 외곽 기반 바닥",interior,f*5,f); Outline(view.FloorRoots[f],interior,f*5+.35f,edgeMat); }
   var third=new List<Vector2>{new Vector2(-13,-18),new Vector2(-6,-20),new Vector2(3,-5),new Vector2(12,-8),new Vector2(15,-1),new Vector2(-1,4),new Vector2(-5,-5)}.Select(p=>p*4).ToList();
   MvpFloorBuilder.Build(view.FloorRoots[2],"안내도 기반 식당가와 발코니",third,10,2); Outline(view.FloorRoots[2],third,10.4f,glassMat);
   foreach(var feature in data.features.Where(x=>x.kind=="platform")) { var poly=Points(feature); Slab(view.FloorRoots[0],"승강장 "+feature.reference,poly,.3f,floorMat); var a=poly.OrderBy(p=>p.y).First(); var b=poly.OrderByDescending(p=>p.y).First(); var delta=b-a; var side=new Vector2(delta.y,-delta.x).normalized*1.4f; Beam(view.FloorRoots[0],new Vector3(a.x+side.x,.1f,a.y+side.y),new Vector3(b.x+side.x,.1f,b.y+side.y),.35f,railMat); Label(view.FloorRoots[0],"승강장 "+feature.reference.Replace(";","·"),new Vector3((a.x+b.x)/2,2,(a.y+b.y)/2),1.1f); }
   foreach(var feature in data.features.Where(x=>x.id=="165346394")) Slab(view.FloorRoots[0],"연결동",Points(feature),1.5f,edgeMat);
   MvpInteriorBuilder.Build(view.FloorRoots,interior,third);
   foreach(var feature in data.features.Where(x=>x.kind=="platform")) MvpInteriorBuilder.Platform(view.FloorRoots[0],Points(feature));
   Box(envelope,"역 앞 광장",new Vector3(-92,-.25f,0),new Vector3(52,.5f,200),edgeMat);
   HeroModel(envelope,"소방 지휘 차량","FireEngine119",new Vector3(-100,0,-32),0);HeroModel(envelope,"구급 차량","Ambulance119",new Vector3(-100,0,8),0);
   Label(envelope,"부산역",new Vector3(-17,13,0),3);
   for(int i=0;i<2;i++)
   {
    float distance=0,previousLength=0;var direction=Quaternion.Euler(0,20,0)*Vector3.forward;
    for(int j=0;j<4;j++)
    {
     float length;var train=TrainModel(view.FloorRoots[0],j==0||j==3?"HighSpeedCab":"HighSpeedCoach",out length);
     if(j>0)distance+=(previousLength+length)*.5f+.3f;
     train.localPosition=new Vector3(16+i*40,.4f,-136)+direction*distance;
     train.localRotation=Quaternion.Euler(0,j==3?200:20,0);previousLength=length;
    }
   }

   view.Teams=new Transform[3]; var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/ChooGuard/Art/ResponseSet/CrewOperations.fbx").OfType<AnimationClip>().Where(x=>!x.name.StartsWith("__")).ToArray(); view.IdleClip=clips.FirstOrDefault(x=>x.name.ToLowerInvariant().Contains("idle")); view.WalkClip=clips.FirstOrDefault(x=>x.name.ToLowerInvariant().Contains("walk"));view.InteractClip=clips.FirstOrDefault(x=>x.name.ToLowerInvariant().Contains("interact"));
   var taskMaterialPath=Folder+"/작업선.mat";view.TaskLineMaterial=AssetDatabase.LoadAssetAtPath<Material>(taskMaterialPath);if(view.TaskLineMaterial==null){view.TaskLineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(view.TaskLineMaterial,taskMaterialPath);}view.TaskLineMaterial.SetColor("_BaseColor",new Color(.15f,.8f,.95f));EditorUtility.SetDirty(view.TaskLineMaterial);
   string[] names={"역무팀","소방팀","의료팀"}; for(int i=0;i<3;i++) { var team=Node(root.transform,names[i]); team.localPosition=new Vector3(-20,5.05f,(i-1)*2.2f); view.Teams[i]=team; HeroModel(team,names[i],i==0?"CrewOperations":i==1?"CrewFire":"CrewMedical",Vector3.zero,MvpSpatialMetrics.CrewHeight); Box(team,"팀 위치",new Vector3(0,-.15f,0),new Vector3(1.7f,.15f,1.7f),Mat("팀"+i,i==0?Color.cyan:i==1?new Color(1,.35f,.2f):new Color(.3f,1,.5f))); Label(team,names[i],new Vector3(0,3,0),1); }
   view.IncidentMarker=Node(root.transform,"훈련 상황 위치"); view.IncidentMarker.localPosition=MvpSpatialMetrics.Reference(15,5)+Vector3.up*1.5f; Box(view.IncidentMarker,"상황 표지",Vector3.zero,new Vector3(2,2,2),Mat("상황",new Color(1,.3f,.08f))); Label(view.IncidentMarker,"훈련 상황",new Vector3(0,2,0),1.4f); view.SetIncidentVisible(false);
   view.CrowdRoot=Node(root.transform,"국소 계산 보행자"); view.CrowdTemplate=view.Teams[0].GetChild(0).gameObject;
   MvpFloorBuilder.ReferencePatch(view.FloorRoots[1]);
   Box(view.FloorRoots[1],"기준 공간 병목 남측",MvpSpatialMetrics.Reference(24.5f,4.5f)+Vector3.up*.7f,new Vector3(1,1.4f,9),edgeMat);
   Box(view.FloorRoots[1],"기준 공간 병목 북측",MvpSpatialMetrics.Reference(24.5f,15.5f)+Vector3.up*.7f,new Vector3(1,1.4f,9),edgeMat);
   Box(view.FloorRoots[1],"기준 공간 출구 경계",MvpSpatialMetrics.Reference(30,10),new Vector3(.12f,.03f,2),Mat("출구그림기호",new Color(.3f,.65f,.4f)));
   Label(view.FloorRoots[1],"국소 기준 계산 · 30×20m",new Vector3(-4,6,-14),1.3f);
   var cam=Node(root.transform,"공간 카메라").gameObject.AddComponent<Camera>(); cam.orthographic=true; cam.cullingMask=1<<Layer; cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.045f,.075f,.1f); cam.farClipPlane=3000; cam.depth=10;cam.allowHDR=true; cam.nearClipPlane=.1f;
   var texture=AssetDatabase.LoadAssetAtPath<RenderTexture>(Folder+"/Station.renderTexture"); if(texture==null) { texture=new RenderTexture(2080,668,24){name="부산역 공간",antiAliasing=2}; AssetDatabase.CreateAsset(texture,Folder+"/Station.renderTexture"); } cam.targetTexture=null; view.Texture=texture; view.ViewCamera=cam;
   view.Navigation=cam.gameObject.AddComponent<MvpOpenWorldCamera>();view.Navigation.WorldRoot=root.transform;view.Navigation.Workspace=workspace;view.Navigation.Camera=cam;
   var light=Node(root.transform,"공간 조명").gameObject.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.15f; light.color=new Color(1f,.94f,.83f); light.shadows=LightShadows.Soft;light.shadowStrength=.6f; light.cullingMask=1<<Layer; light.transform.rotation=Quaternion.Euler(48,-35,0);
   MvpCityBuilder.BuildUnder(root.transform);
   MvpInteriorBuilder.BuildWorldHubs(root.transform);
   var presentation=root.AddComponent<MvpWorldPresentation>();presentation.Navigation=view.Navigation;presentation.ViewCamera=cam;
   var bridge=workspace.GetComponent<MvpPhysicsBridge>();if(bridge==null)bridge=workspace.gameObject.AddComponent<MvpPhysicsBridge>();
   var director=workspace.GetComponent<MvpTrainingDirector>();if(director==null)director=workspace.gameObject.AddComponent<MvpTrainingDirector>();director.Station=view;director.Physics=bridge;
   var tasks=workspace.GetComponent<MvpTeamTaskController>();if(tasks==null)tasks=workspace.gameObject.AddComponent<MvpTeamTaskController>();tasks.Workspace=workspace;tasks.Station=view;tasks.Director=director;director.TeamTasks=tasks;EditorUtility.SetDirty(tasks);
   workspace.CurrentFloor=0;workspace.RefreshLayout();SetLayer(root.transform); view.Bind(workspace);director.Bind(workspace);EditorUtility.SetDirty(director);EditorUtility.SetDirty(bridge); EditorUtility.SetDirty(workspace); EditorUtility.SetDirty(view); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene,MvpWorkspaceBuilder.ScenePath)) throw new IOException("훈련 장면 저장 실패");
  }
  const string HeroFolder="Assets/ChooGuard/Art/ResponseSet";
  static void ConfigureHeroAssets()
  {
   Directory.CreateDirectory(HeroFolder+"/Materials");AssetDatabase.Refresh();
   foreach(var name in new[]{"BusanFacade","HighSpeedCab","HighSpeedCoach","FireEngine119","Ambulance119","CrewOperations","CrewFire","CrewMedical"})
   {
    string path=HeroFolder+"/"+name+".fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(importer==null)throw new FileNotFoundException("생성된 아트 자료를 찾을 수 없습니다.",path);
    bool crew=name.StartsWith("Crew");bool changed=importer.materialImportMode!=ModelImporterMaterialImportMode.ImportStandard||importer.importAnimation!=crew||(crew&&importer.animationType!=ModelImporterAnimationType.Generic);
    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.importAnimation=crew;if(crew){importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;}
    if(changed)importer.SaveAndReimport();
   }
   ConfigureCrewAnimationLoops();
  }
  [MenuItem("ChooGuard/MVP/Configure Crew Animation Loops")]
  public static void ConfigureCrewAnimationLoops()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("편집 모드에서 동작 가져오기를 설정하세요.");
   foreach(var model in new[]{"CrewOperations","CrewFire","CrewMedical"})
   {
    string path=HeroFolder+"/"+model+".fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;
    if(importer==null)throw new FileNotFoundException("대응팀 FBX 가져오기 설정을 찾을 수 없습니다.",path);
    var clips=importer.clipAnimations;bool changed=clips.Length==0;if(clips.Length==0)clips=importer.defaultClipAnimations;
    bool idleFound=false,walkFound=false;
    foreach(var clip in clips)
    {
     string name=clip.name.ToLowerInvariant();bool idle=name.Contains("idle"),walk=name.Contains("walk"),interaction=name.Contains("interact");
     idleFound|=idle;walkFound|=walk;if(!idle&&!walk&&!interaction)continue;
     bool loop=idle||walk;
     if(clip.loopTime!=loop||clip.loopPose!=loop){clip.loopTime=loop;clip.loopPose=loop;changed=true;}
    }
    if(!idleFound||!walkFound)throw new InvalidDataException(model+"에 실제 대기/보행 동작이 없습니다.");
    if(changed){importer.clipAnimations=clips;importer.SaveAndReimport();}
   }
  }
  static Material HeroMaterial(string source)
  {
   string name=source.Split('.')[0].Replace(" (Instance)","");Color color;float metallic=0,smooth=.45f;
   switch(name){
    case "Pearl":color=new Color(.74f,.78f,.79f);metallic=.35f;smooth=.71f;break;
    case "Navy":color=new Color(.025f,.07f,.14f);metallic=.3f;break;
    case "Glass":color=new Color(.16f,.22f,.25f);metallic=.45f;smooth=.76f;break;
    case "Steel":color=new Color(.43f,.48f,.5f);metallic=.72f;smooth=.7f;break;
    case "Roof":color=new Color(.17f,.2f,.21f);metallic=.5f;break;
    case "Concrete":color=new Color(.55f,.53f,.47f);smooth=.15f;break;
    case "Red":color=new Color(.64f,.045f,.032f);metallic=.2f;break;
    case "AmbulanceWhite":color=new Color(.84f,.85f,.8f);metallic=.15f;break;
    case "Orange":color=new Color(.92f,.24f,.04f);break;
    case "Reflective":color=new Color(.78f,.88f,.18f);metallic=.15f;break;
    case "Rubber":color=new Color(.018f,.023f,.026f);smooth=.12f;break;
    case "Lamp":color=new Color(.72f,.88f,.92f);smooth=.8f;break;
    case "Skin":color=new Color(.55f,.34f,.23f);smooth=.35f;break;
    case "Uniform":color=new Color(.035f,.085f,.13f);smooth=.25f;break;
    case "PPE":color=new Color(.47f,.39f,.21f);smooth=.24f;break;
    case "Medic":color=new Color(.12f,.29f,.32f);smooth=.35f;break;
    default:color=new Color(.4f,.44f,.46f);break;
   }
   string path=HeroFolder+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smooth);
   if(name=="Concrete"||name=="Steel"){string set=name=="Concrete"?"Concrete031":"Metal032";material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChooGuard/ThirdParty/Textures/"+set+"/"+set+"_1K-JPG_Color.jpg"));}EditorUtility.SetDirty(material);return material;
  }
  static Transform HeroModel(Transform parent,string name,string model,Vector3 position,float height)
  {
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(HeroFolder+"/"+model+".fbx");if(asset==null)throw new FileNotFoundException(model);
   var obj=UnityEngine.Object.Instantiate(asset,parent);obj.name=name;obj.transform.localPosition=Vector3.zero;obj.transform.localRotation=Quaternion.identity;
   var renderers=obj.GetComponentsInChildren<Renderer>();foreach(var renderer in renderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>HeroMaterial(m!=null?m.name:"Pearl")).ToArray();
   if(height>0&&renderers.Length>0){var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);float factor=height/Mathf.Max(bounds.size.y,.01f);var offset=obj.transform.InverseTransformPoint(bounds.center-Vector3.up*bounds.size.y*.5f);obj.transform.localScale*=factor;obj.transform.localPosition=position-offset*factor;}else obj.transform.localPosition=position;
   return obj.transform;
  }
  static Transform TrainModel(Transform parent,string model,out float length)
  {
   var root=Node(parent,"고속 열차");var body=HeroModel(root,model,model,Vector3.zero,0);
   // Bounds in the unrotated model axes, including nested FBX transforms; no world AABB fitting.
   Bounds bounds=default;bool found=false;
   foreach(var filter in body.GetComponentsInChildren<MeshFilter>())
   {
    if(filter.sharedMesh==null)continue;var b=filter.sharedMesh.bounds;
    for(int corner=0;corner<8;corner++)
    {
     var p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
     p=body.InverseTransformPoint(filter.transform.TransformPoint(p));
     if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
    }
   }
   if(!found||bounds.size.y<=0)throw new InvalidDataException(model+"의 미터 모델 경계를 읽을 수 없습니다.");
   var scale=body.localScale;scale.y=3.8f/bounds.size.y;body.localScale=scale;
   length=bounds.size.z*Mathf.Abs(scale.z);
   body.localPosition=-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
   return root;
  }
  static Transform Node(Transform parent,string name) { var g=new GameObject(name); g.transform.SetParent(parent,false); return g.transform; }
  static Material Mat(string name,Color color) { var path=Folder+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); } m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.25f); EditorUtility.SetDirty(m); return m; }
  static List<Vector2> Points(Feature f) { var list=f.points.Select(p=>new Vector2(p.x,p.z)).ToList(); if(list.Count>1 && Vector2.Distance(list[0],list[list.Count-1])<.001f) list.RemoveAt(list.Count-1); return list; }
  static List<Vector2> Clip(List<Vector2> source,float z) { var result=new List<Vector2>(); for(int i=0;i<source.Count;i++) { var a=source[i]; var b=source[(i+1)%source.Count]; if(a.y<=z) result.Add(a); if((a.y<=z)!=(b.y<=z)) result.Add(Vector2.Lerp(a,b,(z-a.y)/(b.y-a.y))); } return result; }
  static void Slab(Transform parent,string name,List<Vector2> poly,float y,Material mat) { var node=Node(parent,name); var mesh=new Mesh{name=name}; var vertices=poly.Select(p=>new Vector3(p.x,y,p.y)).ToArray(); var indices=new List<int>(); var left=Enumerable.Range(0,poly.Count).ToList(); float area=0; for(int i=0;i<poly.Count;i++) area+=Cross(poly[i],poly[(i+1)%poly.Count]); if(area<0) left.Reverse(); int guard=0; while(left.Count>2 && guard++<poly.Count*poly.Count) { bool cut=false; for(int i=0;i<left.Count;i++) { int a=left[(i+left.Count-1)%left.Count],b=left[i],c=left[(i+1)%left.Count]; if(Cross(poly[b]-poly[a],poly[c]-poly[b])<=.00001f) continue; bool inside=left.Any(p=>p!=a&&p!=b&&p!=c&&Cross(poly[b]-poly[a],poly[p]-poly[a])>=0&&Cross(poly[c]-poly[b],poly[p]-poly[b])>=0&&Cross(poly[a]-poly[c],poly[p]-poly[c])>=0); if(inside) continue; indices.Add(a);indices.Add(c);indices.Add(b);left.RemoveAt(i);cut=true;break; } if(!cut) break; } mesh.vertices=vertices; mesh.triangles=indices.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); string path=Folder+"/mesh-"+parent.name+"-"+name+".asset";mesh=MvpMeshPersistence.Store(path,mesh); node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh; node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat; }
  static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static void Outline(Transform parent,List<Vector2> poly,float y,Material mat) { for(int i=0;i<poly.Count;i++) Beam(parent,new Vector3(poly[i].x,y,poly[i].y),new Vector3(poly[(i+1)%poly.Count].x,y,poly[(i+1)%poly.Count].y),.35f,mat); }
  static void Beam(Transform parent,Vector3 a,Vector3 b,float width,Material mat) { var t=Box(parent,"연결 구조",(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),mat); t.rotation=Quaternion.LookRotation(b-a); }
  static Transform Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat) { var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent,false); g.transform.localPosition=pos; g.transform.localScale=size; g.GetComponent<Renderer>().sharedMaterial=mat; UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); return g.transform; }
  static void Shops(Transform parent,float y,string label,Vector3 start) { for(int i=0;i<4;i++) Box(parent,label,start+new Vector3(i*3,y+1,0),new Vector3(2.7f,2,3),i%2==0?glassMat:edgeMat); Label(parent,label,start+new Vector3(4,y+3,0),1.3f); }
  static void Label(Transform parent,string text,Vector3 pos,float size) { /* Text belongs to the UI Canvas only. */ }
  static Transform Model(Transform parent,string name,string path,Vector3 pos,float height,Material mat) { var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(asset==null) return null; var g=UnityEngine.Object.Instantiate(asset,parent); g.name=name; g.transform.localPosition=Vector3.zero; var renderers=g.GetComponentsInChildren<Renderer>(); if(renderers.Length>0) { var bounds=renderers[0].bounds; foreach(var r in renderers) { bounds.Encapsulate(r.bounds); r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray(); } var scale=height/Mathf.Max(bounds.size.y,.01f); var offset=g.transform.InverseTransformPoint(bounds.center-new Vector3(0,bounds.size.y/2,0)); g.transform.localScale*=scale; g.transform.localPosition=pos-offset*scale; } else g.transform.localPosition=pos; return g.transform; }
  static void SetLayer(Transform t) { t.gameObject.layer=Layer; foreach(Transform child in t) SetLayer(child); }
 }
}
