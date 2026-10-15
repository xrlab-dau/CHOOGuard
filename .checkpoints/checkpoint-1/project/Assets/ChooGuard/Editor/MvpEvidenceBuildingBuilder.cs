using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEngine;
namespace ChooGuard.Editor
{
 // Reviewed photo-derived exteriors only. No interior/fleet/camera/solver mutations.
 public static class MvpEvidenceBuildingBuilder
 {
  const string Folder="Assets/ChooGuard/Art/RestoredFacilities/";
  const string DepotRoot="Evidence restored fire facilities";
  const string StationRoot="Evidence restored station facade";
  static readonly string[] Names={"Central119Restored","Choryang119Restored","BusanStationRestored"};
  [Serializable] sealed class DepotReceipt { public string name;public float terrainDatum,minimumSampledTerrain,maximumSampledTerrain;public int terrainSamples,sourcePolygonVertices;public Vector3 frontA,frontB,outward,modelBoundsCenter,modelBoundsSize;public Vector3[] sourceFootprint;public Vector3[] apronBoundary,garageEntryAnchors,apronStagingAnchors,streetHandoffAnchors;public float garageFloorY,apronTopY;public string anchorProvenance="INFERRED from reviewed source footprint front edge and authored apron/bay geometry; not surveyed or proven continuous vehicle access";public string terrainMethod="Exact piecewise-affine terrain extrema over source footprint and inferred apron; source grid vertices and triangle-edge intersections; authored foundation, not surveyed"; }
  [Serializable] sealed class ApplyReceipt { public string validationSurface="Editor generation receipt; root must verify native rendering and access";public bool stationReferencesUnchanged;public DepotReceipt[] depots;public string[] retainedReferences; }
  static readonly Dictionary<string,Color> Colors=new Dictionary<string,Color>{
   {"White",new Color(.77f,.79f,.78f)},{"Red",new Color(.64f,.035f,.025f)},{"Glass",new Color(.075f,.16f,.19f)},
   {"Frame",new Color(.38f,.43f,.44f)},{"Dark",new Color(.055f,.07f,.075f)},{"Brown",new Color(.34f,.18f,.075f)},
   {"Paving",new Color(.45f,.44f,.4f)},{"Blue",new Color(.035f,.12f,.22f)}};
  static string DepotName(string id)=>id=="825455699"?Names[0]:id=="825456385"?Names[1]:null;
  public static bool ReplacementAvailable(string osmId)
  {
   string name=DepotName(osmId);var asset=name==null?null:AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".fbx");
   return asset!=null&&asset.GetComponentsInChildren<MeshFilter>(true).Any(x=>x.sharedMesh!=null&&x.sharedMesh.vertexCount>0);
  }
  public static bool ShouldExcludeGeneric(Transform station,string osmId)
  {
   string name=DepotName(osmId);if(name==null||!ReplacementAvailable(osmId))return false;
   var depots=FindDepotRoot(station);var instance=depots==null?null:depots.Find(name);
   return instance!=null&&instance.GetComponentsInChildren<MeshFilter>(true).Any(x=>x.sharedMesh!=null&&x.sharedMesh.vertexCount>0);
  }
  public static void ConfigureImports()
  {
   Directory.CreateDirectory(Folder+"Materials");AssetDatabase.Refresh();
   foreach(string name in Names)
   {
    var importer=AssetImporter.GetAtPath(Folder+name+".fbx") as ModelImporter;
    if(importer==null)throw new FileNotFoundException("Reviewed restored model not imported",Folder+name+".fbx");
    bool changed=importer.globalScale!=1f||!importer.useFileScale||!importer.isReadable||importer.importAnimation||importer.materialImportMode!=ModelImporterMaterialImportMode.ImportStandard;
    importer.globalScale=1f;importer.useFileScale=true;importer.isReadable=true;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    if(changed)importer.SaveAndReimport();
   }
  }
  // Root flow: BuildCityWithExclusions(existingStation); ApplyExisting(existingStation).
  // First establish actual reviewed depot instances, so a missing import never removes a generic building.
  public static void BuildCityWithExclusions(Transform station)
  {
   RequireStation(station);ConfigureImports();MvpWorldSurfaceBuilder.Read();ReplaceDepots(station,true);
   MvpCityBuilder.BuildUnder(station);
   var depots=station.Find(DepotRoot);var city=station.Find("도시 공개지형");if(depots!=null&&city!=null)depots.SetParent(city,false);
  }
  public static void ApplyExisting(Transform station)
  {
   RequireStation(station);var view=station.GetComponent<MvpStationView>();var retained=Retained(view);var envelope=view.WholeEnvelope;
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+Names[2]+".fbx");if(asset==null)throw new InvalidOperationException("Reviewed station facade missing.");
   var facade=UnityEngine.Object.Instantiate(asset,envelope,false);facade.name=StationRoot+" staging";facade.SetActive(false);
   try
   {
    Reset(facade.transform);ValidateMetres(facade.transform,2);BindMaterials(facade.transform);Layer(facade.transform);
    var receipts=ReplaceDepots(station);
    if(!retained.SequenceEqual(Retained(view)))throw new InvalidOperationException("Station references changed during facade preparation.");
    foreach(string name in new[]{StationRoot,"부산역 곡면 유리 역사"})
    {var previous=envelope.Find(name);if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);}
    facade.name=StationRoot;facade.SetActive(true);
    Directory.CreateDirectory("art/world");File.WriteAllText("art/world/building-restoration-apply-receipt.json",JsonUtility.ToJson(new ApplyReceipt{stationReferencesUnchanged=true,depots=receipts,retainedReferences=retained.Select(x=>x==null?"null":x.name+":"+x.GetInstanceID()).ToArray()},true));
    EditorUtility.SetDirty(station.gameObject);AssetDatabase.SaveAssets();
   }
   catch {UnityEngine.Object.DestroyImmediate(facade);throw;}
  }
  static void RequireStation(Transform station)
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Restoration requires edit mode.");
   if(station==null||station.GetComponent<MvpStationView>()==null||station.GetComponent<MvpStationView>().WholeEnvelope==null)throw new InvalidOperationException("Existing station view and envelope required; whole-station generation is forbidden here.");
  }
  static UnityEngine.Object[] Retained(MvpStationView view)
  {
   var items=new List<UnityEngine.Object>{view,view.WholeEnvelope,view.ViewCamera,view.Navigation,view.PlatformAnchor,view.ConcourseAnchor,view.ExitAnchor,view.IncidentMarker,view.CrowdRoot};
   if(view.FloorRoots!=null)items.AddRange(view.FloorRoots);if(view.Teams!=null)items.AddRange(view.Teams);
   items.AddRange(view.GetComponents<Component>());return items.ToArray();
  }
  static Transform FindDepotRoot(Transform station)=>station.Find(DepotRoot)??station.Find("도시 공개지형/"+DepotRoot);
  static DepotReceipt[] ReplaceDepots(Transform station,bool keepOutsideCity=false)
  {
   var assets=Names.Take(2).Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Folder+n+".fbx")).ToArray();
   if(assets.Any(a=>a==null))throw new InvalidOperationException("Both reviewed depot FBXs required; existing depots retained.");
   MvpWorldSurfaceBuilder.Read();var staged=new GameObject(DepotRoot+" staging");staged.transform.SetParent(station,false);staged.SetActive(false);var receipts=new List<DepotReceipt>();
   try
   {
    for(int i=0;i<2;i++)
    {
     var obj=UnityEngine.Object.Instantiate(assets[i],staged.transform,false);obj.name=Names[i];Reset(obj.transform);ValidateMetres(obj.transform,i);BindMaterials(obj.transform);
     var samples=SampleTerrain(Footprints[i]).Concat(SampleTerrain(Aprons[i])).ToArray();float low=samples.Min(p=>p.y),high=samples.Max(p=>p.y);float datum=high+.025f;
     obj.transform.localPosition=Vector3.up*datum;
     BuildSupport(staged.transform,Names[i]+" source footprint foundation",Footprints[i],datum);
     BuildSupport(staged.transform,Names[i]+" inferred apron support",Aprons[i],datum);
     var modelBounds=StationMeshBounds(obj.transform,station);var a=Fronts[i][0];var b=Fronts[i][1];var n=Outward[i];var entries=new Vector3[3];var parking=new Vector3[3];var handoffs=new Vector3[3];
     for(int j=0;j<3;j++){var p=Vector2.Lerp(a,b,(j+.5f)/3f);var entry=p+n*.2f;var stage=p+n*2.5f;var street=p+n*5.2f;entries[j]=new Vector3(entry.x,datum+.16f,entry.y);parking[j]=new Vector3(stage.x,datum+.06f,stage.y);handoffs[j]=new Vector3(street.x,MvpWorldSurfaceBuilder.Height(street.x,street.y),street.y);}
     receipts.Add(new DepotReceipt{name=Names[i],terrainDatum=datum,minimumSampledTerrain=low,maximumSampledTerrain=high,terrainSamples=samples.Length,sourcePolygonVertices=Footprints[i].Length,modelBoundsCenter=modelBounds.center,modelBoundsSize=modelBounds.size,sourceFootprint=Footprints[i].Select(p=>new Vector3(p.x,datum,p.y)).ToArray(),frontA=new Vector3(a.x,datum+.16f,a.y),frontB=new Vector3(b.x,datum+.16f,b.y),outward=new Vector3(n.x,0,n.y),garageFloorY=datum+.16f,apronTopY=datum+.06f,apronBoundary=Aprons[i].Select(p=>new Vector3(p.x,datum+.06f,p.y)).ToArray(),garageEntryAnchors=entries,apronStagingAnchors=parking,streetHandoffAnchors=handoffs});
    }
    Layer(staged.transform);var old=FindDepotRoot(station);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
    staged.name=DepotRoot;var city=station.Find("도시 공개지형");if(!keepOutsideCity&&city!=null)staged.transform.SetParent(city,false);staged.SetActive(true);
    Directory.CreateDirectory("art/world");File.WriteAllText("art/world/building-depot-placement-receipt.json",JsonUtility.ToJson(new ApplyReceipt{depots=receipts.ToArray(),stationReferencesUnchanged=true,retainedReferences=new[]{"coordinates: station local east/up/north metres; same placement datum as actual FBX and plinth; anchors inferred"}},true));return receipts.ToArray();
   }
   catch {UnityEngine.Object.DestroyImmediate(staged);throw;}
  }
  // Native Unity FBX import preserves metre extents but reverses source east/north
  // with this reviewed -Z-forward/Y-up export (root scale1, bakeAxisConversion=false).
  // Correct only each restored model instance; station/floor/navigation coordinates stay fixed.
  static void Reset(Transform t){t.localPosition=Vector3.zero;t.localRotation=Quaternion.Euler(0f,180f,0f);t.localScale=Vector3.one;}
  static Bounds StationMeshBounds(Transform model,Transform station)
  {
   bool first=true;var bounds=new Bounds();foreach(var f in model.GetComponentsInChildren<MeshFilter>(true))foreach(var v in f.sharedMesh.vertices)
   {var p=station.InverseTransformPoint(f.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}return bounds;
  }
  static void ValidateMetres(Transform root,int model)
  {
   var points=new List<Vector3>();foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
   {if(f.sharedMesh==null||!f.sharedMesh.isReadable)throw new InvalidOperationException("Reviewed FBX mesh must be readable for metre verification.");foreach(var v in f.sharedMesh.vertices)points.Add(root.parent.InverseTransformPoint(f.transform.TransformPoint(v)));}
   if(points.Count==0)throw new InvalidOperationException("Reviewed FBX has no geometry.");
   var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
   // Validate post-conversion bounds in the fixed parent source-coordinate frame, not
   // the model frame (which would cancel the required instance yaw). All three models
   // retain the same strict metre/origin bounds; native validation is required.
   if(Vector3.Distance(bounds.min,ExpectedMin[model])>.35f||Vector3.Distance(bounds.max,ExpectedMax[model])>.35f)throw new InvalidOperationException("Restored FBX origin/axis/metre bounds mismatch: "+Names[model]+" "+bounds);
  }
  static Material SourceMaterial(string key)
  {
   string path=Folder+"Materials/"+key+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit shader missing.");
   if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;material.SetColor("_BaseColor",Colors[key]);material.SetFloat("_Smoothness",key=="Glass"?.65f:.25f);material.SetFloat("_Metallic",key=="Glass"?.2f:0);EditorUtility.SetDirty(material);return material;
  }
  static void BindMaterials(Transform root)
  {
   foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
   {
    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{string key=m==null?null:Colors.Keys.FirstOrDefault(k=>m.name.StartsWith("Restoration_"+k,StringComparison.Ordinal));if(key==null)throw new InvalidOperationException("Unknown reviewed model material: "+(m==null?"null":m.name));return SourceMaterial(key);}).ToArray();
    renderer.gameObject.isStatic=false;GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);
   }
  }
  static IEnumerable<Vector3> SampleTerrain(Vector2[] polygon)
  {
   var h=MvpWorldSurfaceBuilder.Read().heightfield;
   if(h==null||h.step<=0)throw new InvalidDataException("Current source terrain grid required.");
   // The surface is piecewise affine on grid triangles. Its polygon maximum is at
   // a polygon corner, grid vertex, or polygon-edge/grid-triangle intersection.
   for(int i=0;i<polygon.Length;i++)foreach(var p in TerrainEdge(polygon[i],polygon[(i+1)%polygon.Length],h))
    yield return new Vector3(p.x,MvpWorldSurfaceBuilder.Height(p.x,p.y),p.y);
   float minX=polygon.Min(p=>p.x),maxX=polygon.Max(p=>p.x),minZ=polygon.Min(p=>p.y),maxZ=polygon.Max(p=>p.y);
   for(float x=h.minX+Mathf.Ceil((minX-h.minX)/h.step)*h.step;x<=maxX;x+=h.step)
    for(float z=h.minZ+Mathf.Ceil((minZ-h.minZ)/h.step)*h.step;z<=maxZ;z+=h.step)
     if(Inside(new Vector2(x,z),polygon))yield return new Vector3(x,MvpWorldSurfaceBuilder.Height(x,z),z);
  }
  static Vector2[] TerrainEdge(Vector2 a,Vector2 b,MvpWorldSurfaceBuilder.Heightfield h)
  {
   var cuts=new SortedSet<float>{0f,1f};
   Action<float,float,float> gridCuts=(start,end,origin)=>
   {if(Mathf.Abs(end-start)<.000001f)return;int low=Mathf.CeilToInt((Mathf.Min(start,end)-origin)/h.step),high=Mathf.FloorToInt((Mathf.Max(start,end)-origin)/h.step);for(int k=low;k<=high;k++){float t=(origin+k*h.step-start)/(end-start);if(t>0&&t<1)cuts.Add(t);}};
   gridCuts(a.x,b.x,h.minX);gridCuts(a.y,b.y,h.minZ);gridCuts(a.x-a.y,b.x-b.y,h.minX-h.minZ);
   return cuts.Select(t=>Vector2.Lerp(a,b,t)).ToArray();
  }
  static bool Inside(Vector2 p,Vector2[] polygon)
  {bool inside=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)if((polygon[i].y>p.y)!=(polygon[j].y>p.y)&&p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;return inside;}
  static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static void BuildSupport(Transform root,string name,Vector2[] source,float datum)
  {
   var poly=source.ToList();float area=0;for(int i=0;i<poly.Count;i++)area+=Cross(poly[i],poly[(i+1)%poly.Count]);if(area<0)poly.Reverse();
   var vertices=new List<Vector3>();var triangles=new List<int>();
   Action<Vector3,Vector3,Vector3> triangle=(a,b,c)=>{int k=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(k);triangles.Add(k+1);triangles.Add(k+2);};
   for(int i=0;i<poly.Count;i++)
   {
    var a=poly[i];var b=poly[(i+1)%poly.Count];var edge=TerrainEdge(a,b,MvpWorldSurfaceBuilder.Read().heightfield);
    for(int j=0;j<edge.Length-1;j++)
    {var p=edge[j];var q=edge[j+1];var pa=new Vector3(p.x,MvpWorldSurfaceBuilder.Height(p.x,p.y)-.08f,p.y);var pb=new Vector3(p.x,datum-.01f,p.y);var qb=new Vector3(q.x,datum-.01f,q.y);var qa=new Vector3(q.x,MvpWorldSurfaceBuilder.Height(q.x,q.y)-.08f,q.y);triangle(pa,pb,qb);triangle(pa,qb,qa);}
   }
   // Ear clipping preserves the full concave source floor instead of filling its L-shaped courtyard.
   var ids=Enumerable.Range(0,poly.Count).ToList();int guard=0;
   while(ids.Count>2&&guard++<poly.Count*poly.Count)
   {
    bool cut=false;for(int i=0;i<ids.Count;i++)
    {int a=ids[(i+ids.Count-1)%ids.Count],b=ids[i],c=ids[(i+1)%ids.Count];if(Cross(poly[b]-poly[a],poly[c]-poly[b])<=.000001f)continue;
     if(ids.Any(p=>p!=a&&p!=b&&p!=c&&Cross(poly[b]-poly[a],poly[p]-poly[a])>=0&&Cross(poly[c]-poly[b],poly[p]-poly[b])>=0&&Cross(poly[a]-poly[c],poly[p]-poly[c])>=0))continue;
     triangle(new Vector3(poly[a].x,datum-.01f,poly[a].y),new Vector3(poly[c].x,datum-.01f,poly[c].y),new Vector3(poly[b].x,datum-.01f,poly[b].y));ids.RemoveAt(i);cut=true;break;}
    if(!cut)throw new InvalidDataException("Cannot triangulate source foundation polygon.");
   }
   if(ids.Count>2)throw new InvalidDataException("Incomplete source foundation polygon.");
   var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x,v.z)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();Directory.CreateDirectory(Folder+"Foundations");mesh=MvpMeshPersistence.Store(Folder+"Foundations/"+name+".asset",mesh);
   var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=SourceMaterial("Paving");go.isStatic=false;
  }
  static void Layer(Transform root){root.gameObject.layer=29;root.gameObject.isStatic=false;GameObjectUtility.SetStaticEditorFlags(root.gameObject,0);foreach(Transform child in root)Layer(child);}
  // Frozen source polygons from reviewed building source graph; metres at shared WGS84 origin.
  static readonly Vector2[][] Fronts={new[]{new Vector2(-471.524994f,-855.19397f),new Vector2(-480.38501f,-891.606018f)},new[]{new Vector2(126.226997f,522.525024f),new Vector2(142.425995f,515.901001f)}};
  static readonly Vector2[] Outward={new Vector2(-0.971649f,0.236428f),new Vector2(-0.378494f,-0.925604f)};
  static readonly Vector2[][] Footprints={
   new[]{new Vector2(-428.654f,-870.823f),new Vector2(-471.525f,-855.194f),new Vector2(-480.385f,-891.606f),new Vector2(-463.621f,-897.896f),new Vector2(-456.054f,-872.916f),new Vector2(-430.265f,-883.391f)},
   new[]{new Vector2(126.017f,531.876f),new Vector2(123.887f,527.2f),new Vector2(126.227f,522.525f),new Vector2(142.426f,515.901f),new Vector2(147.589f,518.718f),new Vector2(152.652f,529.839f),new Vector2(148.345f,531.798f),new Vector2(144.557f,523.46f)}
  };
  static readonly Vector2[][] Aprons={
   new[]{new Vector2(-471.913666f,-855.099426f),new Vector2(-480.773682f,-891.511475f),new Vector2(-485.243256f,-890.423889f),new Vector2(-476.38324f,-854.011841f)},
   new[]{new Vector2(126.0756f,522.154785f),new Vector2(142.274597f,515.530762f),new Vector2(140.533524f,511.27298f),new Vector2(124.334526f,517.897034f)}
  };
  static readonly Vector3[] ExpectedMin={new Vector3(-485.243256f,0.0f,-898.013f),new Vector3(123.773247f,0.0f,511.27298f),new Vector3(-70.843643f,0.0f,-74.972771f)};
  static readonly Vector3[] ExpectedMax={new Vector3(-428.529999f,18.48f,-854.011841f),new Vector3(152.765762f,16.65f,531.989746f),new Vector3(1.963949f,17.549999f,86.788536f)};
 }
}
