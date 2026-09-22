var rows=new System.Collections.Generic.List<object>();
foreach(var name in new[]{"MainShell","ExitRoofs","PlatformsParking"})
{
var path="Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_"+name+".fbx";
var a=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
var mi=UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.ModelImporter;
if(a==null){rows.Add(new{name,imported=false});continue;}
var meshes=a.GetComponentsInChildren<UnityEngine.MeshFilter>(true);var first=true;var b=new UnityEngine.Bounds();
foreach(var f in meshes){var mb=f.sharedMesh.bounds;for(int i=0;i<8;i++){var p=f.transform.TransformPoint(mb.center+UnityEngine.Vector3.Scale(mb.extents,new UnityEngine.Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){b=new UnityEngine.Bounds(p,UnityEngine.Vector3.zero);first=false;}else b.Encapsulate(p);}}
var mats=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.SelectMany(a.GetComponentsInChildren<UnityEngine.Renderer>(true),r=>r.sharedMaterials)));
rows.Add(new{name,imported=true,meshes=meshes.Length,globalScale=mi.globalScale,useFileScale=mi.useFileScale,readable=mi.isReadable,min=new{x=b.min.x,y=b.min.y,z=b.min.z},max=new{x=b.max.x,y=b.max.y,z=b.max.z},materials=mats.Length,textureSlots=System.Linq.Enumerable.Count(mats,m=>m!=null&&m.mainTexture!=null),nullMaterials=System.Linq.Enumerable.Count(mats,m=>m==null),shaders=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(mats,m=>m==null?"NULL":m.shader.name)))});
}
return rows;
