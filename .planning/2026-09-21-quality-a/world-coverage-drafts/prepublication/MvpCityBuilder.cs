using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor
{
    public static class MvpCityBuilder
    {
        private const string Folder="Assets/ChooGuard/Settings/MvpCity";
        private const float Scale=1f; // One Unity unit is one source metre.
        [Serializable] private sealed class Point { public float x,z; }
        [Serializable] private sealed class Feature { public string id,kind,name,subtype,height,levels;public Point[] points; }
        [Serializable] private sealed class Limits { public float minX,maxX,minZ,maxZ; }
        [Serializable] private sealed class Data { public Feature[] features;public Limits requestedBounds; }
        [Serializable] private sealed class HeightReceipt { public string source,sourceSha256,validationSurface;public int explicitHeightTags,generatedBuildings;public MvpBuildingHeightResolver.Resolution[] features; }
        private sealed class Batch
        {
            public readonly List<Vector3> V=new List<Vector3>();public readonly List<int> T=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c) { int start=V.Count;V.Add(a);V.Add(b);V.Add(c);T.Add(start);T.Add(start+1);T.Add(start+2); }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d) { Triangle(a,b,c);Triangle(a,c,d); }
        }
        [MenuItem("ChooGuard/MVP/Build Connected Busan City")]
        public static void Build()
        {
            var scene=SceneManager.GetSceneByPath(MvpWorkspaceBuilder.ScenePath);
            if(!scene.IsValid()||!scene.isLoaded||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("저장된 훈련 장면을 편집 모드로 여세요.");
            var station=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="부산역 훈련 공간");if(station==null)throw new InvalidOperationException("부산역을 먼저 생성하세요.");
            BuildUnder(station.transform);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,MvpWorkspaceBuilder.ScenePath);
        }
        public static void BuildUnder(Transform station)
        {
            string source="asset-library/space-references/busan-reconstruction/busan-openworld.json";
            if(!File.Exists(source))throw new FileNotFoundException("공개 도시 지형 자료가 없습니다.",source);
            var data=JsonUtility.FromJson<Data>(File.ReadAllText(source));if(data.requestedBounds==null)throw new InvalidDataException("도시 경계가 없습니다.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var old=station.Find("도시 공개지형");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("도시 공개지형");root.transform.SetParent(station,false);root.layer=29;
            var layers=MvpWorldSurfaceBuilder.Read();MvpWorldSurfaceBuilder.BuildUnder(root.transform,layers);var archetypes=layers.buildings.ToDictionary(x=>x.id,x=>x.archetype);
            var ground=new Batch();var water=new Batch();var roads=new Batch();var footways=new Batch();var rail=new Batch();var buildings=Enumerable.Range(0,6).Select(_=>new Batch()).ToArray();var windows=new Batch();var roofDetail=new Batch();var trim=new Batch();var markings=new Batch();
            var bound=data.requestedBounds;
            int built=0;
            var heightRows=data.features.Where(f=>f.kind=="building").ToDictionary(f=>f.id,f=>MvpBuildingHeightResolver.Resolve(f.id,f.height,f.levels));
            foreach(var feature in data.features)
            {
                if(feature.points==null||feature.points.Length<2||feature.kind=="water")continue;
                if(feature.kind=="building"||feature.kind=="water")
                {
                    if(feature.id=="165346389"||feature.id=="165346394"){heightRows[feature.id].geometryStatus="existing_station_source_floor_and_facade_retained";continue;}
                    if(MvpEvidenceBuildingBuilder.ShouldExcludeGeneric(station,feature.id)){heightRows[feature.id].geometryStatus="reviewed_source_footprint_replacement_present";continue;}
                    if((feature.id=="480601129"&&AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChooGuard/Art/WorldSet/BusanTower.fbx")!=null)||(feature.id=="368597686"&&AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChooGuard/Art/WorldSet/PortTerminal.fbx")!=null)||(feature.id=="382696296"&&AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChooGuard/Art/WorldSet/JagalchiMarket.fbx")!=null)){heightRows[feature.id].geometryStatus="existing_landmark_replacement";continue;}
                    var polygon=Clip(feature.points.Select(p=>new Vector2(p.x,p.z)).ToList(),bound);if(polygon.Count<3)continue;
                    for(int i=polygon.Count-1;i>0;i--)if((polygon[i]-polygon[i-1]).sqrMagnitude<.001f)polygon.RemoveAt(i);
                    if((polygon[0]-polygon[polygon.Count-1]).sqrMagnitude<.001f)polygon.RemoveAt(polygon.Count-1);if(polygon.Count<3)continue;
                    polygon=polygon.Select(p=>p*Scale).ToList();
                    var detailBatches=new[]{buildings[archetypes.TryGetValue(feature.id,out var typeIndex)?typeIndex:0],windows,roofDetail,trim};var starts=detailBatches.Select(x=>x.V.Count).ToArray();
                    var centroid=polygon.Aggregate(Vector2.zero,(sum,p)=>sum+p)/polygon.Count;float baseHeight=MvpWorldSurfaceBuilder.Height(centroid.x,centroid.y);foreach(var corner in polygon)baseHeight=Mathf.Max(baseHeight,MvpWorldSurfaceBuilder.Height(corner.x,corner.y)); // Raise a support plinth on slopes; skirts reach the shared terrain.
                    if(feature.kind=="water") { Roof(water,polygon,-.55f);continue; }
                    int color=archetypes.TryGetValue(feature.id,out var archetype)?archetype:0;
                    var heightResolution=heightRows[feature.id];float height=heightResolution.metres;heightResolution.terrainDatum=baseHeight;heightResolution.geometryStatus="generated_source_polygon_shell";float signed=0;for(int i=0;i<polygon.Count;i++)signed+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);if(signed<0)polygon.Reverse();Roof(buildings[color],polygon,height);
                    for(int i=0;i<polygon.Count;i++) { var a=polygon[i];var b=polygon[(i+1)%polygon.Count];buildings[color].Quad(new Vector3(a.x,-.15f,a.y),new Vector3(a.x,height,a.y),new Vector3(b.x,height,b.y),new Vector3(b.x,-.15f,b.y)); }
                    // Window rhythm, parapets and compact rooftop plant are visual detail, not source dimensions.
                    for(int edge=0;edge<polygon.Count;edge++)
                    {
                     if(height<3f)continue; // Do not place authored full-storey details above a short source roof.
                     var a=polygon[edge];var b=polygon[(edge+1)%polygon.Count];var delta=b-a;float length=delta.magnitude;if(length<2.4f)continue;
                     var direction=delta/length;var outward=new Vector2(direction.y,-direction.x)*.04f;int count=Mathf.Clamp(Mathf.FloorToInt(length/(color==2||color==5?3.2f:4.0f)),1,28);int rows=Mathf.Clamp(Mathf.FloorToInt(height/3.4f),1,20);
                     for(int row=0;row<rows;row++)for(int column=0;column<count;column++)
                     {
                      var middle=Vector2.Lerp(a,b,(column+.5f)/count)+outward;var left=middle-direction*Mathf.Min(.76f,length/count*.3f);var right=middle+direction*Mathf.Min(.76f,length/count*.3f);float bottom=1.0f+row*(height-2.4f)/rows;
                      windows.Quad(new Vector3(left.x,bottom,left.y),new Vector3(left.x,bottom+(color==2||color==5?2.4f:1.44f),left.y),new Vector3(right.x,bottom+(color==2||color==5?2.4f:1.44f),right.y),new Vector3(right.x,bottom,right.y));
                     }
                     // Continuous storefront/base and floor bands distinguish public/office/retail massing.
                     for(int band=0;band<(color==2||color==5?rows:2);band++)
                     {
                         float by=band==0?.40f:color==2||color==5?1f+band*height/rows:height-.80f;
                         var aa=a+outward*1.4f;var bb=b+outward*1.4f;
                         trim.Quad(new Vector3(aa.x,by,aa.y),new Vector3(aa.x,by+.20f,aa.y),new Vector3(bb.x,by+.20f,bb.y),new Vector3(bb.x,by,bb.y));
                     }
                     if(edge==0&&length>3f){var mid=(a+b)*.5f+outward*2;var l=mid-direction*.6f;var r=mid+direction*.6f;windows.Quad(new Vector3(l.x,0,l.y),new Vector3(l.x,2.1f,l.y),new Vector3(r.x,2.1f,r.y),new Vector3(r.x,0,r.y));if(color==1||color==3)RoofBox(trim,new Vector3(mid.x,2.3f,mid.y),new Vector3(2.4f,.16f,1.2f));}
                     roofDetail.Quad(new Vector3(a.x,height,a.y),new Vector3(a.x,height+.48f,a.y),new Vector3(b.x,height+.48f,b.y),new Vector3(b.x,height,b.y));
                    }
                    if(polygon.Count==4&&height>10f){var center=polygon.Aggregate(Vector2.zero,(sum,p)=>sum+p)/polygon.Count;RoofBox(roofDetail,new Vector3(center.x,height+.68f,center.y),new Vector3(2.2f,1.36f,1.6f));}
                    for(int bi=0;bi<detailBatches.Length;bi++)for(int vi=starts[bi];vi<detailBatches[bi].V.Count;vi++){var v=detailBatches[bi].V[vi];v.y=v.y<=-.14f?Mathf.Min(baseHeight-.15f,MvpWorldSurfaceBuilder.Height(v.x,v.z)-.15f):v.y+baseHeight;detailBatches[bi].V[vi]=v;}
                    built++;
                }
                else if(feature.kind=="road"||feature.kind=="rail")
                {
                    bool foot=feature.subtype=="footway"||feature.subtype=="path"||feature.subtype=="steps"||feature.subtype=="pedestrian";
                    float width=feature.kind=="rail"?1.6f:foot?2.8f:feature.subtype=="primary"||feature.subtype=="trunk"?16:feature.subtype=="secondary"?12:6f;
                    var batch=feature.kind=="rail"?rail:foot?footways:roads;float y=feature.kind=="rail"?.06f:foot?.04f:-.05f;
                    for(int i=1;i<feature.points.Length;i++)
                    {
                        var a=new Vector2(feature.points[i-1].x,feature.points[i-1].z);var b=new Vector2(feature.points[i].x,feature.points[i].z);
                        if(!ClipSegment(ref a,ref b,bound))continue;a*=Scale;b*=Scale;var d=b-a;if(d.sqrMagnitude<.0001f)continue;var side=new Vector2(-d.y,d.x).normalized*width*.5f;
                        batch.Quad(new Vector3(a.x-side.x,y,a.y-side.y),new Vector3(a.x+side.x,y,a.y+side.y),new Vector3(b.x+side.x,y,b.y+side.y),new Vector3(b.x-side.x,y,b.y-side.y));
                        if(feature.kind=="rail")
                        {
                            var dir=d.normalized;var gauge=side.normalized*.7175f;
                            foreach(float sign in new[]{-1f,1f}){var aa=a+gauge*sign;var bb=b+gauge*sign;var w=side.normalized*.035f;markings.Quad(new Vector3(aa.x-w.x,y+.025f,aa.y-w.y),new Vector3(aa.x+w.x,y+.025f,aa.y+w.y),new Vector3(bb.x+w.x,y+.025f,bb.y+w.y),new Vector3(bb.x-w.x,y+.025f,bb.y-w.y));}
                        }
                        else if(!foot&&width>=12)
                        {
                            var dir=d.normalized;var normal=side.normalized*.10f;int dashCount=Mathf.Min(80,Mathf.FloorToInt(d.magnitude/10f));
                            for(int dash=0;dash<dashCount;dash++){var aa=a+dir*(dash*10f+1.6f);var bb=aa+dir*4.8f;markings.Quad(new Vector3(aa.x-normal.x,y+.018f,aa.y-normal.y),new Vector3(aa.x+normal.x,y+.018f,aa.y+normal.y),new Vector3(bb.x+normal.x,y+.018f,bb.y+normal.y),new Vector3(bb.x-normal.x,y+.018f,bb.y-normal.y));}
                        }
                    }
                }
            }
            Emit(root.transform,"도시 지면",ground,Material("지면",new Color(.22f,.29f,.26f)));
            Emit(root.transform,"항만 수면",water,Material("수면",new Color(.075f,.25f,.32f)));
            // Continuous buffered asphalt and curbs are emitted by world surface builder.
            // Walkways use the same tessellated relief as terrain.
            Emit(root.transform,"철도",rail,Material("철도",new Color(.18f,.2f,.21f)));
            Emit(root.transform,"도시 창호",windows,Material("창호",new Color(.075f,.12f,.15f)));Emit(root.transform,"옥상 설비와 파라펫",roofDetail,Material("옥상",new Color(.28f,.3f,.3f)));
            Emit(root.transform,"입면 석재 띠",trim,Material("석회석",new Color(.74f,.69f,.57f)));Emit(root.transform,"차선과 레일",markings,Material("차선",new Color(.81f,.79f,.66f)));
            Color[] colors={new Color(.63f,.55f,.45f),new Color(.64f,.43f,.32f),new Color(.43f,.54f,.57f),new Color(.75f,.71f,.61f),new Color(.49f,.55f,.53f),new Color(.57f,.65f,.67f)};
            for(int i=0;i<6;i++)Emit(root.transform,"건물군"+i,buildings[i],Material("건물"+i,colors[i]));
            using(var sha=System.Security.Cryptography.SHA256.Create())
            {var receipt=new HeightReceipt{source=source,sourceSha256=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-","").ToLowerInvariant(),validationSurface="Actual CityBuilder shared resolver output; heights exclude terrain datum and authored rooftop details; not surveyed height or product acceptance",explicitHeightTags=heightRows.Values.Count(r=>r.provenance=="OSM_EXPLICIT_HEIGHT_TAG_NOT_SURVEYED"),generatedBuildings=built,features=heightRows.Values.ToArray()};Directory.CreateDirectory("art/world");File.WriteAllText("art/world/building-height-generation-receipt.json",JsonUtility.ToJson(receipt,true));}
            root.name="도시 공개지형";Debug.Log("공개자료 도시 생성: 건물 "+built+"개 · 나머지 높이는 게임 표현");AssetDatabase.SaveAssets();
        }
        private static Material Material(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",name=="창호"?.6f:.12f);material.SetFloat("_Metallic",name=="창호"?.25f:0);EditorUtility.SetDirty(material);return material;
        }
        private static void Emit(Transform root,string name,Batch batch,Material material)
        {
            if(batch.V.Count==0)return;
            var chunks=new Dictionary<Vector2Int,Batch>();
            for(int i=0;i<batch.V.Count;i+=3){var center=(batch.V[i]+batch.V[i+1]+batch.V[i+2])/3;var key=new Vector2Int(Mathf.FloorToInt(center.x/512),Mathf.FloorToInt(center.z/512));if(!chunks.TryGetValue(key,out var chunk)){chunk=new Batch();chunks[key]=chunk;}chunk.Triangle(batch.V[i],batch.V[i+1],batch.V[i+2]);}
            if(name=="보행로"||name=="철도"||name=="차선과 레일")foreach(var chunk in chunks.Values)for(int vi=0;vi<chunk.V.Count;vi++){var v=chunk.V[vi];v.y+=MvpWorldSurfaceBuilder.Height(v.x,v.z);chunk.V[vi]=v;}
            foreach(var pair in chunks)EmitChunk(root,name+"_"+pair.Key.x+"_"+pair.Key.y,pair.Value,material);
        }
        private static void EmitChunk(Transform root,string name,Batch batch,Material material)
        {
            if(batch.V.Count==0)return;var mesh=new Mesh { name=name,indexFormat=IndexFormat.UInt32 };mesh.SetVertices(batch.V);mesh.SetTriangles(batch.T,0);mesh.SetUVs(0,batch.V.Select(v=>new Vector2((v.x+v.z)*.5f,v.y*.5f)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Folder+"/"+name+".asset";mesh=MvpMeshPersistence.Store(path,mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.layer=29;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;go.isStatic=false;GameObjectUtility.SetStaticEditorFlags(go,0);
        }
        private static void RoofBox(Batch batch,Vector3 c,Vector3 size)
        {
            var p=new Vector3[8];for(int i=0;i<8;i++)p[i]=c+new Vector3((i&1)==0?-size.x*.5f:size.x*.5f,(i&2)==0?-size.y*.5f:size.y*.5f,(i&4)==0?-size.z*.5f:size.z*.5f);
            batch.Quad(p[2],p[6],p[7],p[3]);batch.Quad(p[0],p[2],p[3],p[1]);batch.Quad(p[1],p[3],p[7],p[5]);batch.Quad(p[5],p[7],p[6],p[4]);batch.Quad(p[4],p[6],p[2],p[0]);
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static void Roof(Batch batch,List<Vector2> polygon,float y)
        {
            var ids=Enumerable.Range(0,polygon.Count).ToList();float area=0;for(int i=0;i<polygon.Count;i++)area+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);if(area<0)ids.Reverse();int guard=0;
            while(ids.Count>2&&guard++<polygon.Count*polygon.Count)
            {
                bool cut=false;for(int i=0;i<ids.Count;i++) { int a=ids[(i+ids.Count-1)%ids.Count],b=ids[i],c=ids[(i+1)%ids.Count];if(Cross(polygon[b]-polygon[a],polygon[c]-polygon[b])<=.00001f)continue;
                    bool inside=ids.Any(p=>p!=a&&p!=b&&p!=c&&Cross(polygon[b]-polygon[a],polygon[p]-polygon[a])>=0&&Cross(polygon[c]-polygon[b],polygon[p]-polygon[b])>=0&&Cross(polygon[a]-polygon[c],polygon[p]-polygon[c])>=0);if(inside)continue;
                    batch.Triangle(new Vector3(polygon[a].x,y,polygon[a].y),new Vector3(polygon[c].x,y,polygon[c].y),new Vector3(polygon[b].x,y,polygon[b].y));ids.RemoveAt(i);cut=true;break; }
                if(!cut)break;
            }
        }
        private static List<Vector2> Clip(List<Vector2> polygon,Limits bounds)
        {
            for(int edge=0;edge<4;edge++) { var result=new List<Vector2>();if(polygon.Count==0)return result;for(int i=0;i<polygon.Count;i++) { var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=Distance(a,edge,bounds),db=Distance(b,edge,bounds);if(da>=0)result.Add(a);if((da>=0)!=(db>=0))result.Add(Vector2.Lerp(a,b,da/(da-db))); }polygon=result; }return polygon;
        }
        private static float Distance(Vector2 p,int edge,Limits b)=>edge==0?p.x-b.minX:edge==1?b.maxX-p.x:edge==2?p.y-b.minZ:b.maxZ-p.y;
        private static bool ClipSegment(ref Vector2 a,ref Vector2 b,Limits bounds)
        {
            for(int edge=0;edge<4;edge++){float da=Distance(a,edge,bounds),db=Distance(b,edge,bounds);if(da<0&&db<0)return false;if((da>=0)!=(db>=0)){var point=Vector2.Lerp(a,b,da/(da-db));if(da<0)a=point;else b=point;}}return true;
        }
    }
}
