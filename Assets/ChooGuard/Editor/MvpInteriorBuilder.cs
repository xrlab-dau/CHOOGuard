using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Mvp;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
 // Public-guide zoning is source informed. Dimensions and all furnishings are derived visual geometry.
 // These meshes are intentionally collider-free and do not alter the solver reference hall.
 public static class MvpInteriorBuilder
 {
  const string Folder="Assets/ChooGuard/Settings/MvpInterior";
  static Material stone, plaster, steel, glass, oak, navy, light, tactile;
  static Mesh bevel;
  public static void Build(Transform[] floors, List<Vector2> footprint, List<Vector2> balcony)
  {
   Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
   stone=Mat("석회석",new Color(.68f,.65f,.56f),.15f);plaster=Mat("밝은벽",new Color(.82f,.8f,.73f),.12f);
   steel=Mat("스테인리스",new Color(.4f,.46f,.47f),.65f,.65f);glass=Mat("청록창호",new Color(.23f,.43f,.46f),.82f,.3f);
   oak=Mat("목재가구",new Color(.42f,.26f,.13f),.3f);navy=Mat("안내남색",new Color(.025f,.105f,.16f),.3f);
   light=Mat("안내백색",new Color(.88f,.93f,.88f),.5f);tactile=Mat("점자유도",new Color(.7f,.56f,.2f),.1f);
   bevel=BevelMesh();
   for(int f=0;f<3;f++)
   {
    float y=f*MvpSpatialMetrics.FloorHeight;var old=floors[f].Find("공개 안내도 기반 내부 · 미터 시각 재구성");if(old!=null)Object.DestroyImmediate(old.gameObject);var root=Node(floors[f],"공개 안내도 기반 내부 · 미터 시각 재구성");var poly=f==2?balcony:footprint;
    Perimeter(root,poly,y,f);Columns(root,poly,y,f);
    foreach(var zone in MvpStationZones.Layout(poly,f))FunctionalZone(root,zone,f==2&&zone.Kind.Contains("코어")?y-5:y);
    Batch(root);
   }
  }
  [MenuItem("ChooGuard/MVP/Upgrade Existing Functional Zones")]
  public static void UpgradeExistingFunctionalZones()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("편집 모드에서 내부 구획을 갱신하세요.");
   var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MvpWorkspaceBuilder.ScenePath);
   var station=scene.GetRootGameObjects().Select(g=>g.GetComponent<MvpStationView>()).FirstOrDefault(v=>v!=null);
   if(station==null||station.FloorRoots==null||station.FloorRoots.Length!=3)throw new System.InvalidOperationException("기존 역사 세 층이 필요합니다.");
   var outlines=new List<Vector2>[3];
   for(int f=0;f<3;f++)
   {
    var slab=station.FloorRoots[f].Find(f==2?"안내도 기반 식당가와 발코니":"공개 외곽 기반 바닥");
    if(slab==null)throw new System.IO.InvalidDataException("기존 바닥이 없습니다.");
    var mesh=slab.GetComponent<MeshFilter>().sharedMesh;var ids=mesh.GetTriangles(0).Distinct().OrderBy(i=>i);var vertices=mesh.vertices;
    outlines[f]=ids.Select(i=>new Vector2(vertices[i].x,vertices[i].z)).ToList();
   }
   Build(station.FloorRoots,outlines[0],outlines[2]);
   foreach(var floor in station.FloorRoots)foreach(var t in floor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=floor.gameObject.layer;
   AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
   // Root owns the scene save and official rendered acceptance; no city, art or physics rebuilding.
  }
  static void FunctionalZone(Transform parent,MvpStationZones.Zone zone,float y)
  {
   var r=zone.Bounds;var root=Node(parent,zone.Kind);root.localPosition=new Vector3(r.center.x,y,r.center.y);
   float w=r.width,d=r.height;
   if(zone.Kind.Contains("포털"))
   {
    for(int side=-1;side<=1;side+=2){Part(root,"포털 구조벽",new Vector3(side*(w/2-.35f),2,0),new Vector3(.7f,4,d),stone);Part(root,"진입 금속 프레임",new Vector3(side*(w/2-.8f),1.6f,.35f),new Vector3(.12f,3.2f,.15f),steel);}
    Part(root,"큰 출입 상부 프레임",new Vector3(0,3.8f,0),new Vector3(w,.4f,d),plaster);Sign(root,"방향",new Vector3(0,3.2f,d/2-.1f),1.1f);
   }
   else if(zone.Kind.Contains("코어"))
   {
    Stair(root,new Vector3(-2,0,-d/2+.5f),true);Stair(root,new Vector3(0,0,-d/2+.5f),true);Lift(root,new Vector3(2.7f,0,-d/2+1));
    for(int side=-1;side<=1;side+=2)Part(root,"동선 코어 측벽",new Vector3(side*(w/2-.15f),1.1f,0),new Vector3(.2f,2.2f,d),plaster);
   }
   else if(zone.Kind.Contains("좌석"))
   {
    for(int row=0;row<2;row++)for(int col=0;col<2;col++){var p=new Vector3(-2+col*4,0,-2+row*4);Bench(root,p);if(zone.Kind.Contains("식당"))Table(root,p+Vector3.forward*.85f);}
    // Low end planters define an island without closing pedestrian approaches.
    for(int side=-1;side<=1;side+=2)Part(root,"좌석섬 끝 낮은 석재",new Vector3(side*(w/2-.4f),.25f,0),new Vector3(.55f,.5f,3),stone);
   }
   else
   {
    Part(root,"공간 후벽",new Vector3(0,1.6f,-d/2+.1f),new Vector3(w,3.2f,.2f),plaster);
    for(int side=-1;side<=1;side+=2)Part(root,"공간 측벽",new Vector3(side*(w/2-.1f),1.6f,0),new Vector3(.2f,3.2f,d),plaster);
    Part(root,"상점 전면 프레임",new Vector3(0,3.05f,d/2-.1f),new Vector3(w,.3f,.25f),navy);
    if(zone.Kind.Contains("역무")){Part(root,"역무 전면 창",new Vector3(-w*.22f,1.5f,d/2-.1f),new Vector3(w*.48f,1.8f,.08f),glass);Counter(root,new Vector3(0,0,0),"안내");}
    else{for(int i=-1;i<=1;i++)Counter(root,new Vector3(i*2.4f,0,d/2-1.1f),zone.Kind.Contains("매표")?"승차권":"안내");for(int j=0;j<3;j++)Part(root,"후면 진열 선반",new Vector3(0,.65f+j*.55f,-d/2+.45f),new Vector3(w-.8f,.08f,.6f),oak);}
   }
  }
  public static void Platform(Transform floor,List<Vector2> poly)
  {
   if(poly.Count<3)return;Vector2 a=poly[0],b=a;float longest=0;
   for(int i=0;i<poly.Count;i++)for(int j=i+1;j<poly.Count;j++)if((poly[i]-poly[j]).sqrMagnitude>longest){longest=(poly[i]-poly[j]).sqrMagnitude;a=poly[i];b=poly[j];}
   var root=Node(floor,"승강장 구조 · 파생 캐노피");var mid=(a+b)*.5f;var dir=(b-a).normalized;float length=Mathf.Sqrt(longest);
   root.localPosition=new Vector3(mid.x,0,mid.y);root.localRotation=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));
   // Narrow canopy leaves the platform and trains visible in the 1F cutaway.
   Part(root,"접힌 금속 지붕",new Vector3(0,4.65f,0),new Vector3(4.5f,.12f,length*.92f),steel);
   for(float z=-length*.42f;z<length*.43f;z+=7){Part(root,"캐노피 기둥",new Vector3(0,2.45f,z),new Vector3(.16f,4.3f,.16f),steel);Part(root,"횡보",new Vector3(0,4.5f,z),new Vector3(4.7f,.12f,.12f),steel);}
   for(int side=-1;side<=1;side+=2)Part(root,"승강장 안전선",new Vector3(side*2.6f,.33f,0),new Vector3(.1f,.025f,length*.94f),tactile);
   Batch(root);
  }
  [System.Serializable] sealed class WorldPacket { public float sourceScale; public HubProp[] props; }
  [System.Serializable] sealed class HubProp { public string kind,sourceId; public float x,z,size,footprintWidth,footprintDepth,angle; }
  public static void BuildWorldHubs(Transform parent)
  {
   const string path="art/world/world-layers.json";
   if(!File.Exists(path)){Debug.LogWarning("훈련 거점 생성 누락: "+path+"가 없습니다. 대체 좌표를 사용하지 않습니다.");return;}
   var data=JsonUtility.FromJson<WorldPacket>(File.ReadAllText(path));
   if(data==null||data.props==null||!Mathf.Approximately(data.sourceScale,1f)){Debug.LogWarning("훈련 거점 생성 누락: props 또는 sourceScale 1 계약이 없습니다.");return;}
   string[] kinds={"PortTerminal","JagalchiMarket"},ids={"368597686","382696296"},titles={"부산항 국제여객터미널","자갈치시장"};
   for(int i=0;i<kinds.Length;i++)
   {
    var source=data.props.FirstOrDefault(p=>p.kind==kinds[i]&&p.sourceId==ids[i]);
    if(source==null){Debug.LogWarning("훈련 거점 생성 누락: "+kinds[i]+" sourceId="+ids[i]+"가 없습니다. 대체 좌표를 사용하지 않습니다.");continue;}
    var shell=parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==kinds[i]);
    if(shell==null){Debug.LogWarning("훈련 거점 생성 누락: "+kinds[i]+" sourceId="+ids[i]+" 외관이 없습니다.");continue;}
    var hub=BuildTrainingHub(parent,titles[i],new Vector3(source.x*data.sourceScale,.04f,source.z*data.sourceScale),source.footprintWidth*data.sourceScale,source.footprintDepth*data.sourceScale,kinds[i]=="JagalchiMarket");hub.localRotation=Quaternion.Euler(0,source.angle,0);
    var view=hub.gameObject.AddComponent<MvpInteriorHubView>();view.ViewCamera=parent.GetComponent<MvpStationView>().ViewCamera;view.ShellRenderers=shell.GetComponentsInChildren<Renderer>(true);view.InteriorRenderers=hub.GetComponentsInChildren<Renderer>(true);view.SourceId=ids[i];view.RevealRadius=Mathf.Max(source.footprintWidth,source.footprintDepth)*data.sourceScale*.55f;view.MaximumOrthographicSize=Mathf.Max(60,view.RevealRadius*1.1f);
   }
  }
  // Call after Build. Anchor must be a verified source centroid converted by the metre-native world scale.
  // This is a game-authored training fit-out, not an assertion of real terminal or market room layouts.
  public static Transform BuildTrainingHub(Transform parent,string title,Vector3 sourceAnchor,float footprintWidth,float footprintDepth,bool market)
  {
   var root=Node(parent,title+" · 공개홀 및 훈련용 내부");root.localPosition=sourceAnchor;
   // Source bounding dimensions locate and size this visual fit-out; not a measured architectural plan.
   float width=Mathf.Max(12,footprintWidth*.88f),depth=Mathf.Max(8,footprintDepth*.84f);
   Part(root,"공개홀 바닥",new Vector3(0,.08f,0),new Vector3(width,.16f,depth),stone);
   Part(root,"후면 창호벽",new Vector3(0,1.2f,-depth/2),new Vector3(width,2.4f,.18f),glass);
   for(int side=-1;side<=1;side+=2){Part(root,"측면 낮은 벽",new Vector3(side*width/2,.65f,0),new Vector3(.18f,1.3f,depth),plaster);Lift(root,new Vector3(side*(width/2-1.5f),0,-depth/2+1.5f));Facilities(root,new Vector3(side*(width/2-2),0,depth/2-1.5f));}
   for(float x=-width/2+4;x<width/2-3;x+=10)
   {
    for(int side=-1;side<=1;side+=2)Part(root,"홀 구조 기둥",new Vector3(x,1.65f,side*(depth/2-1)),new Vector3(.35f,3.3f,.35f),plaster);
   }
   if(market)
   {
    // Official BISCO 1F guide: long central circulation, paired seafood stall rows, end services.
    for(float x=-width/2+5;x<width/2-4;x+=3)
    {
     for(int row=0;row<4;row++)
     {
      var stall=Node(root,"수산물 점포열 · 공식 안내도 기반");stall.localPosition=new Vector3(x,0,(row-1.5f)*6);
      Part(stall,"수산물 판매대",new Vector3(0,.55f,0),new Vector3(2.7f,1.1f,1.35f),steel);
      Part(stall,"수조 테두리",new Vector3(0,1.15f,0),new Vector3(2.75f,.18f,1.4f),light);
      Part(stall,"수조 내부",new Vector3(0,1.25f,0),new Vector3(2.4f,.035f,1.1f),glass);
      for(int j=-1;j<=1;j++)Part(stall,"진열 구획",new Vector3(j*.8f,1.29f,0),new Vector3(.045f,.08f,1.1f),steel);
     }
    }
    Stair(root,new Vector3(0,0,-depth/2+1),true);
    Sign(root,"수산물시장 · 중앙 통로",new Vector3(0,2.8f,0),width*.7f);
   }
   else
   {
    // Terminal public-hall archetype; not a claim of verified terminal room positions.
    for(float x=-width*.3f;x<width*.3f;x+=3.5f)Shop(root,new Vector3(x,0,-depth*.35f),3.3f,"승선권 · 안내");
    for(int zone=0;zone<4;zone++)for(int row=0;row<8;row++)for(int col=0;col<8;col++)Bench(root,new Vector3(-width*.32f+zone*width*.19f+col*2.5f,0,-10+row*2.4f));
    for(int side=-1;side<=1;side+=2)Stair(root,new Vector3(side*width*.33f,0,depth*.15f),true);
    Sign(root,"여객 대기홀 · 승선 안내",new Vector3(0,3.2f,0),width*.7f);
   }
   // Separate temporary training operations, visibly distinct from public facilities.
   Counter(root,new Vector3(-4,0,depth/2-1),"훈련 임시 통제");Counter(root,new Vector3(1,0,depth/2-1),"훈련 의료 지원");
   Sign(root,title+"  공개홀",new Vector3(0,3.5f,-depth/2),width*.7f);Batch(root);return root;
  }
  static void Batch(Transform root)
  {
   var groups=new Dictionary<Material,List<CombineInstance>>();var parts=new List<GameObject>();
   foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
   {
    if(filter.sharedMesh!=bevel)continue;var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;
    var material=renderer.sharedMaterial;if(!groups.ContainsKey(material))groups[material]=new List<CombineInstance>();
    groups[material].Add(new CombineInstance{mesh=bevel,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});parts.Add(filter.gameObject);
   }
   foreach(var group in groups)
   {
    var mesh=new Mesh{name=root.name+" · "+group.Key.name};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(group.Value.ToArray(),true,true);
    string key=root.parent.name+"-"+root.name+"-"+root.GetSiblingIndex()+"-"+group.Key.name;string path=Folder+"/"+key+".asset";
    mesh=MvpMeshPersistence.Store(path,mesh);
    var node=Node(root,"재질별 통합 · "+group.Key.name);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
   }
   foreach(var part in parts)Object.DestroyImmediate(part);
  }
  static void Perimeter(Transform root,List<Vector2> poly,float y,int floor)
  {
   for(int i=0;i<poly.Count;i++)
   {
    var a=poly[i];var b=poly[(i+1)%poly.Count];float len=Vector2.Distance(a,b);if(len<.8f)continue;
    var bay=Node(root,"외벽 · 창호 · 출입 개구부");bay.localPosition=new Vector3((a.x+b.x)/2,y,(a.y+b.y)/2);bay.localRotation=Quaternion.LookRotation(new Vector3(b.x-a.x,0,b.y-a.y));
    int count=Mathf.Max(1,Mathf.CeilToInt(len/2.5f));float pitch=len/count;
    for(int j=0;j<count;j++)
    {
     float z=-len/2+(j+.5f)*pitch;bool door=(i%4==0&&j==count/2&&floor<2);
     Part(bay,"창호 수직틀",new Vector3(0,1.45f,z-pitch/2),new Vector3(.13f,2.9f,.1f),steel);
     if(!door){Part(bay,"벽 하부",new Vector3(0,.35f,z),new Vector3(.18f,.7f,pitch),stone);Part(bay,"유리 패널",new Vector3(0,1.7f,z),new Vector3(.055f,1.95f,pitch-.12f),glass);}
     else {Part(bay,"출입구 상인방 · 높이 2.1m",new Vector3(0,2.2f,z),new Vector3(.18f,.2f,pitch),plaster);Sign(bay,"출입구",new Vector3(0,2.55f,z),.8f);}
    }
    Part(bay,"창호 상인방",new Vector3(0,2.85f,0),new Vector3(.2f,.17f,len),plaster);
   }
  }
  static void Columns(Transform root,List<Vector2> poly,float y,int floor)
  {
   for(float x=-52;x<92;x+=8)for(float z=-76;z<68;z+=10)
   {
    if(!MvpStationZones.Fits(poly,new Rect(x-.4f,z-.4f,.8f,.8f))||MvpStationZones.Reserved(new Rect(x-.4f,z-.4f,.8f,.8f),floor))continue;
    if(floor==1&&x>-53&&x<-21&&z>-49&&z<-27)continue;
    Part(root,"석재 기둥 주각",new Vector3(x,y+.15f,z),new Vector3(.7f,.3f,.7f),stone);
    Part(root,"원주형 구조 기둥",new Vector3(x,y+2.475f,z),new Vector3(.42f,4.35f,.42f),plaster);
    Part(root,"기둥 머리",new Vector3(x,y+4.7f,z),new Vector3(.7f,.22f,.7f),steel);
    if(Inside(poly,new Vector2(x+7.8f,z)))Part(root,"노출 구조보",new Vector3(x+4,y+4.85f,z),new Vector3(8,.2f,.2f),plaster);
   }
  }
  static void Shop(Transform root,Vector3 p,float width,string title)
  {
   var t=Node(root,title+" · 개방 점포");t.localPosition=p;
   Part(t,"후벽",new Vector3(0,1.2f,-1),new Vector3(width,2.4f,.15f),plaster);
   for(int s=-1;s<=1;s+=2){Part(t,"측벽",new Vector3(s*width/2,1.2f,0),new Vector3(.1f,2.4f,2),plaster);Part(t,"전면 기둥",new Vector3(s*width/2,1.2f,1),new Vector3(.12f,2.4f,.12f),steel);}
   Part(t,"간판 띠",new Vector3(0,2.25f,1),new Vector3(width,.38f,.16f),navy);Sign(t,title,new Vector3(0,2.27f,1.12f),width*.9f);
   Part(t,"목재 판매대",new Vector3(0,.5f,.55f),new Vector3(width*.8f,1,.5f),oak);Part(t,"석재 상판",new Vector3(0,1.04f,.55f),new Vector3(width*.84f,.09f,.58f),stone);
   for(int j=0;j<3;j++)Part(t,"진열 선반",new Vector3(0,.65f+j*.5f,-.75f),new Vector3(width*.85f,.06f,.35f),oak);
  }
  static void Facilities(Transform root,Vector3 p)
  {
   var t=Node(root,"공용 화장실 · 안내도 구역");t.localPosition=p;
   Part(t,"후벽",new Vector3(0,1.1f,-1),new Vector3(3,2.2f,.14f),plaster);
   for(int i=0;i<3;i++)Part(t,"위생 구획벽",new Vector3(-1.4f+i*1.4f,1.1f,0),new Vector3(.12f,2.2f,2),plaster);
   Sign(t,"화장실",new Vector3(0,2.4f,1),3);
   for(int i=0;i<2;i++){Part(t,"세면대",new Vector3(-.7f+i*1.4f,.7f,-.65f),new Vector3(.55f,.2f,.45f),light);Part(t,"거울",new Vector3(-.7f+i*1.4f,1.35f,-.88f),new Vector3(.5f,.6f,.04f),glass);}
  }
  static void Bench(Transform root,Vector3 p){var t=Node(root,"대합실 연결 의자");t.localPosition=p;Part(t,"좌판",new Vector3(0,.39f,0),new Vector3(2,.12f,.55f),oak);Part(t,"등받이",new Vector3(0,.75f,-.23f),new Vector3(2,.58f,.1f),oak);for(int s=-1;s<=1;s+=2)Part(t,"금속 다리",new Vector3(s*.75f,.2f,0),new Vector3(.1f,.4f,.48f),steel);}
  static void Table(Transform root,Vector3 p){Part(root,"식당 테이블 상판",p+Vector3.up*.85f,new Vector3(1.4f,.1f,1),stone);Part(root,"테이블 받침",p+Vector3.up*.4f,new Vector3(.15f,.8f,.15f),steel);}
  static void Kiosk(Transform root,Vector3 p){var t=Node(root,"자동 발매기");t.localPosition=p;Part(t,"발매기 본체",new Vector3(0,.65f,0),new Vector3(.65f,1.3f,.45f),steel);Part(t,"화면",new Vector3(0,.95f,.235f),new Vector3(.5f,.45f,.025f),navy);Part(t,"발권 슬롯",new Vector3(0,.4f,.235f),new Vector3(.3f,.05f,.025f),navy);Sign(t,"승차권",new Vector3(0,1.6f,0),1.2f);}
  static void Counter(Transform root,Vector3 p,string title){Part(root,title+" 안내대",p+Vector3.up*.5f,new Vector3(2.5f,1,.8f),navy);Part(root,"안내대 상판",p+Vector3.up*1.04f,new Vector3(2.65f,.08f,.95f),stone);Sign(root,title,p+Vector3.up*2,3);}
  static void Lift(Transform root,Vector3 p){var t=Node(root,"승강기 · 층별 개구부");t.localPosition=p;Part(t,"승강기 후벽",new Vector3(0,1.4f,-.7f),new Vector3(1.6f,2.8f,.12f),plaster);for(int s=-1;s<=1;s+=2){Part(t,"문틀",new Vector3(s*.75f,1.4f,0),new Vector3(.15f,2.8f,1.5f),steel);Part(t,"승강기 문",new Vector3(s*.32f,1.05f,.7f),new Vector3(.62f,2.1f,.07f),glass);}Sign(t,"승강기",new Vector3(0,2.6f,.8f),1.8f);}
  static void Stair(Transform root,Vector3 p,bool escalator)
  {
   var t=Node(root,escalator?"에스컬레이터 · 층간 동선":"계단 · 층간 동선");t.localPosition=p;
   // Twenty-five risers reach the existing five-unit floor pitch exactly.
   float width=escalator?1.05f:1.6f;
   for(int i=0;i<25;i++)Part(t,"계단 디딤판",new Vector3(0,.1f+i*.2f,i*.25f),new Vector3(width,.2f,.28f),escalator?steel:stone);
   Part(t,"상층 연결참",new Vector3(0,4.94f,6.4f),new Vector3(width,.12f,.8f),stone);
   float angle=-Mathf.Atan2(4.8f,6)*Mathf.Rad2Deg;
   for(int s=-1;s<=1;s+=2){var rail=Part(t,"연속 손잡이",new Vector3(s*(escalator?.65f:.9f),3.4f,3),new Vector3(.09f,.09f,Mathf.Sqrt(36+4.8f*4.8f)),steel);rail.localRotation=Quaternion.Euler(angle,0,0);for(int i=0;i<7;i++)Part(t,"난간 지지대",new Vector3(s*(escalator?.65f:.9f),.6f+i*.8f,i),new Vector3(.055f,1,.055f),steel);}

  }
  static bool Inside(List<Vector2> p,Vector2 q){bool inside=false;for(int i=0,j=p.Count-1;i<p.Count;j=i++)if((p[i].y>q.y)!=(p[j].y>q.y)&&q.x<(p[j].x-p[i].x)*(q.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;return inside;}
  static Transform Node(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
  static Transform Part(Transform parent,string name,Vector3 pos,Vector3 size,Material mat){var t=Node(parent,name);t.localPosition=pos;t.localScale=size;t.gameObject.AddComponent<MeshFilter>().sharedMesh=bevel;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;}
  static Material Mat(string name,Color color,float smooth,float metallic=0){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);EditorUtility.SetDirty(m);return m;}
  static void Sign(Transform root,string purpose,Vector3 p,float width)
  {
   // Geometry-only pictograms: no world text objects, textures or font assets.
   var t=Node(root,"안내 그림기호");t.localPosition=p;float w=Mathf.Clamp(width,.6f,1.2f);
   Part(t,"그림기호 바탕",Vector3.zero,new Vector3(w,.65f,.06f),navy);
   if(purpose.Contains("의료")){Part(t,"십자 세로",new Vector3(0,0,.04f),new Vector3(.12f,.42f,.02f),light);Part(t,"십자 가로",new Vector3(0,0,.04f),new Vector3(.42f,.12f,.02f),light);}
   else if(purpose.Contains("화장실")){for(int side=-1;side<=1;side+=2){Part(t,"사람 머리",new Vector3(side*.17f,.16f,.04f),new Vector3(.1f,.1f,.02f),light);Part(t,"사람 몸",new Vector3(side*.17f,-.04f,.04f),new Vector3(.14f,.25f,.02f),light);}}
   else {Part(t,"방향 화살 몸",new Vector3(-.04f,0,.04f),new Vector3(.42f,.09f,.02f),light);for(int side=-1;side<=1;side+=2){var arm=Part(t,"방향 화살 끝",new Vector3(.13f,side*.07f,.04f),new Vector3(.2f,.07f,.02f),light);arm.localRotation=Quaternion.Euler(0,0,side*-45);}}
  }
  static Mesh BevelMesh()
  {
   string path=Folder+"/공유 모서리 마감.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
   var vertices=new List<Vector3>();var triangles=new List<int>();var uvs=new List<Vector2>();
   // Chamfered rectangular section with cap and bevel rings, shared by all architectural modules.
   Vector2[] ring={new Vector2(-.44f,-.5f),new Vector2(.44f,-.5f),new Vector2(.5f,-.44f),new Vector2(.5f,.44f),new Vector2(.44f,.5f),new Vector2(-.44f,.5f),new Vector2(-.5f,.44f),new Vector2(-.5f,-.44f)};
   for(int r=0;r<4;r++)for(int i=0;i<8;i++){float y=r==0?-.5f:r==1?-.44f:r==2?.44f:.5f;float s=r==0||r==3?.88f:1;vertices.Add(new Vector3(ring[i].x*s,y,ring[i].y*s));uvs.Add(new Vector2(i/8f,r/3f));}
   for(int r=0;r<3;r++)for(int i=0;i<8;i++){int a=r*8+i,b=r*8+(i+1)%8,c=a+8,d=b+8;triangles.AddRange(new[]{a,c,b,b,c,d});}
   for(int i=1;i<7;i++){triangles.AddRange(new[]{0,i,i+1,24,24+i+1,24+i});}
   var mesh=new Mesh{name="공유 모서리 마감"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uvs);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
  }
 }
}
