#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>One observed fixed tenant return. No production source asset or door is edited.</summary>
    public static class BusanokGlazedReturnSetup
    {
        const string Packet = "workers/twin/output/busanok-glazed-return-20261009/busanok-glazed-return-packet.json";
        const string Assets = "Assets/ChooGuard/Art/StationInterior/BusanokReturn20261009";
        const string RootName = "관측 점포 경계 3F";
        const float Epsilon = .0001f;
        public sealed class Result
        {
            public GameObject InactiveRoot;
            public MeshFilter TargetFilter;
            public MeshCollider TargetCollider;
            public Mesh OriginalMesh;
            public Mesh OriginalColliderMesh;
            public MeshFilter[] CompanionFilters;
            public MeshCollider CompanionCollider;
            public string[] CreatedAssetPaths;
            public JObject Receipt;
        }
        sealed class Source
        {
            public MeshFilter Filter;
            public Mesh Mesh;
            public Material Material;
            public Vector3[] Vertices, Normals;
            public Vector4[] Tangents;
            public Color[] Colors;
            public Color32[] Colors32;
            public bool ColorIsBytes;
            public int[] Triangles;
            public List<Vector4>[] Uv = new List<Vector4>[8];
            public int[] UvDimensions = new int[8];
        }
        static string Hash(byte[] b)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
        public static string GeometryHash(Mesh mesh)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            var v=mesh.vertices; var t=mesh.triangles; var n=mesh.normals;
            writer.Write(v.Length); writer.Write(t.Length); writer.Write(n.Length);
            foreach(var p in v){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
            foreach(int i in t)writer.Write(i);
            foreach(var p in n){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
            writer.Flush(); return Hash(stream.ToArray());
        }
        static void Require(bool ok,string message)
        { if(!ok)throw new InvalidDataException("Busanok return preflight: "+message); }
        static Source Read(JToken row)
        {
            var r=row["renderer"]; var m=row["mesh"];
            var go=GameObject.Find((string)r["path"]); Require(go!=null,"missing exact owner "+r["path"]);
            var f=go.GetComponent<MeshFilter>();var renderer=go.GetComponent<MeshRenderer>();
            Require(f!=null&&f.sharedMesh!=null&&renderer!=null&&renderer.enabled&&go.activeInHierarchy,"owner not active/readable");
            // Scene inventory records the geometry owner's MeshFilter ID alongside renderer state.
            Require(GlobalObjectId.GetGlobalObjectIdSlow(f).ToString()==(string)r["global_object_id"],"source MeshFilter identity changed");
            Require(AssetDatabase.GetAssetPath(f.sharedMesh)==(string)m["asset"],"source asset changed");
            Require(Hash(File.ReadAllBytes((string)m["asset"]))==(string)row["asset_file_sha256"],"source asset bytes changed");
            Require(GeometryHash(f.sharedMesh)==(string)m["binary_sha256"],"source facet/channel geometry changed");
            var expected=(JArray)r["world_matrix"];var actual=f.transform.localToWorldMatrix;
            for(int a=0;a<4;a++)for(int b=0;b<4;b++)Require(Mathf.Abs(actual[a,b]-(float)expected[a][b])<Epsilon,"source frame changed");
            Require(f.sharedMesh.subMeshCount==1&&renderer.sharedMaterials.Length==1,"single material owner contract changed");
            Require(AssetDatabase.GetAssetPath(renderer.sharedMaterial)==(string)r["materials"][0],"retained material changed");
            var s=new Source{Filter=f,Mesh=f.sharedMesh,Material=renderer.sharedMaterial,Vertices=f.sharedMesh.vertices,Normals=f.sharedMesh.normals,Tangents=f.sharedMesh.tangents,Colors=f.sharedMesh.colors,Colors32=f.sharedMesh.colors32,Triangles=f.sharedMesh.triangles};
            Require(s.Normals.Length==s.Vertices.Length,"source normals missing");
            Require(s.Mesh.bindposes.Length==0&&!s.Mesh.HasVertexAttribute(VertexAttribute.BlendWeight)&&!s.Mesh.HasVertexAttribute(VertexAttribute.BlendIndices),"unexpected skinned geometry");
            s.ColorIsBytes=s.Mesh.HasVertexAttribute(VertexAttribute.Color)&&s.Mesh.GetVertexAttributeFormat(VertexAttribute.Color)==VertexAttributeFormat.UNorm8;
            for(int u=0;u<8;u++)
            {
                s.Uv[u]=new List<Vector4>();s.Mesh.GetUVs(u,s.Uv[u]);
                Require(s.Uv[u].Count==0||s.Uv[u].Count==s.Vertices.Length,"UV channel length changed");
                s.UvDimensions[u]=s.Uv[u].Count==0?0:s.Mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+u));
            }
            return s;
        }
        sealed class Accumulator
        {
            readonly Source source;
            readonly List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();
            readonly List<Vector4> tangents=new List<Vector4>();
            readonly List<Color> colors=new List<Color>();readonly List<Color32> colors32=new List<Color32>();
            readonly List<int> triangles=new List<int>();readonly List<Vector4>[] uv=Enumerable.Range(0,8).Select(_=>new List<Vector4>()).ToArray();
            public Accumulator(Source s){source=s;}
            public void Append(int[] ordinals,Matrix4x4 map)
            {
                var normalMap=map.inverse.transpose;var indices=new Dictionary<int,int>();
                foreach(int ordinal in ordinals)
                {
                    Require(ordinal>=0&&ordinal*3+2<source.Triangles.Length,"profile triangle out of range");
                    for(int c=0;c<3;c++)
                    {
                        int old=source.Triangles[ordinal*3+c];
                        if(!indices.TryGetValue(old,out int next))
                        {
                            next=v.Count;indices.Add(old,next);v.Add(map.MultiplyPoint3x4(source.Vertices[old]));n.Add(normalMap.MultiplyVector(source.Normals[old]).normalized);
                            if(source.Tangents.Length==source.Vertices.Length){var t=source.Tangents[old];var axis=map.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;tangents.Add(new Vector4(axis.x,axis.y,axis.z,t.w));}
                            if(source.Colors.Length==source.Vertices.Length){colors.Add(source.Colors[old]);colors32.Add(source.Colors32[old]);}
                            for(int u=0;u<8;u++)if(source.Uv[u].Count!=0)uv[u].Add(source.Uv[u][old]);
                        }
                        triangles.Add(next);
                    }
                }
            }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetNormals(n);
                if(tangents.Count!=0)mesh.SetTangents(tangents);
                if(colors.Count!=0){if(source.ColorIsBytes)mesh.SetColors(colors32);else mesh.SetColors(colors);}
                for(int u=0;u<8;u++)if(uv[u].Count!=0)
                {
                    int dim=source.UvDimensions[u];
                    if(dim==2)mesh.SetUVs(u,uv[u].Select(x=>new Vector2(x.x,x.y)).ToList());
                    else if(dim==3)mesh.SetUVs(u,uv[u].Select(x=>new Vector3(x.x,x.y,x.z)).ToList());
                    else {Require(dim==4,"unsupported native UV dimension");mesh.SetUVs(u,uv[u]);}
                }
                mesh.SetTriangles(triangles,0,false);mesh.RecalculateBounds();
                Require(mesh.vertices.Length==mesh.normals.Length,"companion channels not complete");return mesh;
            }
        }
        static int[] Profile(JObject packet,string name)=>((JArray)packet["profiles"][name]).Select(x=>(int)x).ToArray();
        static Matrix4x4 Turn(float sourceStart,float targetU,float lengthScale)
        {
            // Positive rotation maps source front normal +u to return outward +v.
            // The new row runs from front corner toward the existing opaque back wall.
            return Matrix4x4.Translate(new Vector3(36.5f,0,targetU))*Matrix4x4.Rotate(Quaternion.Euler(0,90,0))*Matrix4x4.Scale(new Vector3(lengthScale,1,1))*Matrix4x4.Translate(new Vector3(-sourceStart,0,-6.75f));
        }
        static (float min,float max) Span(Source s,int[] profile,int axis)
        {
            var indices=profile.SelectMany(o=>s.Triangles.Skip(o*3).Take(3)).Distinct();
            var values=indices.Select(i=>s.Vertices[i][axis]).ToArray();return(values.Min(),values.Max());
        }
        static Mesh Retain(Source wall) => RetainNativeWall(wall.Mesh);
        // Pure installer stage; no Scene owner, material, source witness or geometry is created.
        public static Mesh RetainNativeWall(Mesh source)
        {
            Require(source!=null&&source.isReadable&&source.subMeshCount==1,"Readable original wall required");
            var wall=new Source{Mesh=source,Vertices=source.vertices,Triangles=source.triangles};

            Require(wall.Vertices.Length==24&&wall.Triangles.Length==36,"original wall is not exact12-face owner");
            for(int o=8;o<12;o++)foreach(int i in wall.Triangles.Skip(o*3).Take(3))Require(Mathf.Abs(wall.Vertices[i].x-36.5f)<Epsilon,"selected face is not v36.5");
            var mesh=UnityEngine.Object.Instantiate(wall.Mesh);mesh.name="Busanok_Walls_retained8faces";mesh.SetTriangles(wall.Triangles.Take(24).ToArray(),0,false);
            Require(mesh.vertices.SequenceEqual(wall.Mesh.vertices)&&mesh.normals.SequenceEqual(wall.Mesh.normals)&&mesh.tangents.SequenceEqual(wall.Mesh.tangents),"retained source attributes changed");
            for(int u=0;u<8;u++){var a=new List<Vector4>();var b=new List<Vector4>();mesh.GetUVs(u,a);wall.Mesh.GetUVs(u,b);Require(a.SequenceEqual(b),"retained UV changed");}
            Require(mesh.colors.SequenceEqual(wall.Mesh.colors)&&mesh.colors32.SequenceEqual(wall.Mesh.colors32),"retained colors changed");return mesh;
        }
        static void Asset(Mesh mesh,string directory,List<string> created)
        {
            string path=AssetDatabase.GenerateUniqueAssetPath(directory+"/"+mesh.name+".asset");AssetDatabase.CreateAsset(mesh,path);created.Add(path);
            Require(AssetDatabase.LoadAssetAtPath<Mesh>(path)==mesh,"asset creation failed");
        }
        static MeshFilter Companion(GameObject root,string name,Mesh mesh,Material material,int layer)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.layer=layer;go.isStatic=true;
            var f=go.AddComponent<MeshFilter>();f.sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return f;
        }
        public static Result Stage()=>Stage(Packet,Assets);
        /// <summary>Returns an inactive complete patch. Caller activates, tests, saves, publishes registry, or rolls back.</summary>
        public static Result Stage(string packetPath,string assetDirectory)
        {
            Require(!EditorApplication.isPlaying,"edit mode only");var packet=JObject.Parse(File.ReadAllText(packetPath));
            Require(SceneManager.GetActiveScene().path==(string)packet["expected_scene"],"wrong scene");
            Require(assetDirectory.StartsWith("Assets/ChooGuard/Art/StationInterior/BusanokReturn",StringComparison.Ordinal),"fresh companion asset directory required");
            Require(GameObject.Find("실내 트윈 마감/"+RootName)==null,"correction frame already exists");
            var sources=((JObject)packet["sources"]).Properties().ToDictionary(x=>x.Name,x=>Read(x.Value));var wall=sources["wall"];
            var collider=wall.Filter.GetComponent<MeshCollider>();Require(collider!=null&&collider.enabled&&!collider.isTrigger,"fixed source MeshCollider missing");
            Require(GlobalObjectId.GetGlobalObjectIdSlow(collider).ToString()==(string)packet["wall_collider"]["global_object_id"],"static collision owner changed");
            Require(collider.sharedMesh==wall.Mesh&&GeometryHash(collider.sharedMesh)==(string)packet["sources"]["wall"]["mesh"]["binary_sha256"],"render/collision original source differs");
            foreach(var s in sources.Values)for(int a=0;a<4;a++)for(int b=0;b<4;b++)Require(Mathf.Abs(s.Filter.transform.localToWorldMatrix[a,b]-wall.Filter.transform.localToWorldMatrix[a,b])<Epsilon,"profile frame differs");
            var oldFilters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).ToDictionary(f=>f,f=>f.sharedMesh);
            var oldColliders=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None).ToDictionary(c=>c,c=>c.sharedMesh);
            var parent=GameObject.Find("실내 트윈 마감");Require(parent!=null,"retained scene parent missing");
            var memory=new List<Mesh>();var created=new List<string>();GameObject root=null;bool assigned=false,complete=false;Mesh originalCollider=collider.sharedMesh;
            try
            {
                var retained=Retain(wall);memory.Add(retained);
                var glass=new Accumulator(sources["glass"]);var frame=new Accumulator(sources["frame"]);var kick=new Accumulator(sources["kick"]);var fascia=new Accumulator(sources["fascia"]);
                int bays=(int)packet["target"]["bays"];Require(bays==7,"reviewed reference cadence changed");float pitch=9.1f/bays;
                var lower=Profile(packet,"glass_lower");var transom=Profile(packet,"glass_transom");var gs=Span(sources["glass"],lower,0);
                var gb=Span(sources["glass"],lower,1);var gt=Span(sources["glass"],transom,1);
                Require(Mathf.Abs(gb.min-12.4f)<Epsilon&&Mathf.Abs(gb.max-14.515f)<Epsilon&&Mathf.Abs(gt.min-14.585f)<Epsilon&&Mathf.Abs(gt.max-14.97f)<Epsilon,"native frontal glass levels changed");
                for(int k=0;k<bays;k++)
                {
                    // First pane meets the existing corner post's inner face; that post stays untouched.
                    float near=k==0?6.69f:6.75f-k*pitch-.025f, far=6.75f-(k+1)*pitch+.025f;float width=near-far;
                    var map=Turn(gs.min,near,width/(gs.max-gs.min));glass.Append(lower,map);glass.Append(transom,map);
                    var bottom=Profile(packet,"bottom_frame");var bs=Span(sources["frame"],bottom,0);frame.Append(bottom,Turn(bs.min,near,width/(bs.max-bs.min)));
                    var ks=Span(sources["kick"],Profile(packet,"kick"),0);kick.Append(Profile(packet,"kick"),Turn(ks.min,near,width/(ks.max-ks.min)));
                }
                // Add only the7 new posts; use the retained front endpoint post at the shared corner.
                for(int k=1;k<=bays;k++)frame.Append(Profile(packet,"vertical_frame"),Turn(26.5f,6.75f-k*pitch,1));
                frame.Append(Profile(packet,"top_frame"),Turn(26.5f,6.685f,9.035f/10));
                frame.Append(Profile(packet,"transom_frame"),Turn(26.5f,6.69f,9.04f/10));
                // Start behind the native front fascia's inneru6.7 plane, so coplanar top faces do not overlap.
                fascia.Append(Profile(packet,"fascia"),Turn(26.5f,6.7f,9.05f/10));
                var gm=glass.Finish("Busanok_fixed_return_glass");var fm=frame.Finish("Busanok_fixed_return_native_frames");var km=kick.Finish("Busanok_fixed_return_native_kick");var sm=fascia.Finish("Busanok_fixed_return_opaque_fascia");
                var barrier=new Accumulator(wall);barrier.Append(new[]{8,9,10,11},Matrix4x4.identity);var cm=barrier.Finish("Busanok_fixed_return_collision_exact4faces");
                memory.AddRange(new[]{gm,fm,km,sm,cm});
                Require(gm.triangles.Length/3==56&&fm.triangles.Length/3==192&&km.triangles.Length/3==84&&sm.triangles.Length/3==12&&cm.triangles.Length/3==4,"companion face conservation failed");
                root=new GameObject(RootName);root.SetActive(false);root.transform.SetParent(parent.transform,false);
                var local=parent.transform.worldToLocalMatrix*wall.Filter.transform.localToWorldMatrix;
                root.transform.localPosition=local.GetColumn(3);root.transform.localRotation=Quaternion.LookRotation(local.GetColumn(2),local.GetColumn(1));root.transform.localScale=new Vector3(local.GetColumn(0).magnitude,local.GetColumn(1).magnitude,local.GetColumn(2).magnitude);
                for(int a=0;a<4;a++)for(int b=0;b<4;b++)Require(Mathf.Abs(root.transform.localToWorldMatrix[a,b]-wall.Filter.transform.localToWorldMatrix[a,b])<Epsilon,"new inactive frame transfer failed");
                var filters=new[]{Companion(root,"부산옥 고정 유리 끝면",gm,sources["glass"].Material,wall.Filter.gameObject.layer),Companion(root,"부산옥 끝면 원본 단면 프레임",fm,sources["frame"].Material,wall.Filter.gameObject.layer),Companion(root,"부산옥 끝면 원본 단면 걸레받이",km,sources["kick"].Material,wall.Filter.gameObject.layer),Companion(root,"부산옥 끝면 불투명 상부 띠",sm,sources["fascia"].Material,wall.Filter.gameObject.layer)};
                var barrierGo=new GameObject("부산옥 고정 끝면 충돌면");barrierGo.transform.SetParent(root.transform,false);barrierGo.layer=wall.Filter.gameObject.layer;barrierGo.isStatic=true;
                var fixedCollider=barrierGo.AddComponent<MeshCollider>();fixedCollider.sharedMesh=cm;fixedCollider.convex=false;fixedCollider.isTrigger=false;
                Directory.CreateDirectory(assetDirectory);AssetDatabase.Refresh();foreach(var mesh in memory)Asset(mesh,assetDirectory,created);
                assigned=true;wall.Filter.sharedMesh=retained;collider.sharedMesh=retained;
                foreach(var kv in oldFilters)if(kv.Key!=wall.Filter)Require(kv.Key.sharedMesh==kv.Value,"another renderer source changed");
                foreach(var kv in oldColliders)if(kv.Key!=collider)Require(kv.Key.sharedMesh==kv.Value,"another collision source changed");
                foreach(var s in sources.Values)Require(GeometryHash(s.Mesh)==(string)packet["sources"][sources.First(p=>p.Value==s).Key]["mesh"]["binary_sha256"],"original profile was edited");
                var receipt=new JObject{["schema"]="chooguard.busanok-fixed-glazed-return-staging.v1",["production_installed"]=false,["physical_accuracy_verified"]=false,["reference_only"]=true,["packet_sha256"]=Hash(File.ReadAllBytes(packetPath)),["inactive_complete_root"]=root.name,["root_world_matrix"]=JArray.FromObject(Enumerable.Range(0,4).Select(i=>Enumerable.Range(0,4).Select(j=>root.transform.localToWorldMatrix[i,j]).ToArray()).ToArray()),["removed_renderer_triangles"]=4,["removed_original_collider_triangles"]=4,["retained_wall_triangles"]=8,["fixed_barrier_companion_triangles"]=4,["visible_companion_triangles"]=344,["reference_estimated_bays"]=7,["original_source_meshes_unchanged"]=true,["other_renderers_unchanged"]=oldFilters.Count-1,["other_mesh_colliders_unchanged"]=oldColliders.Count-1,["channels"]=JObject.FromObject(sources.ToDictionary(p=>p.Key,p=>new{normals=p.Value.Normals.Length,tangents=p.Value.Tangents.Length,colors=p.Value.Colors.Length,uv_dimensions=p.Value.UvDimensions})),["created_asset_paths"]=JArray.FromObject(created)};
                complete=true;return new Result{InactiveRoot=root,TargetFilter=wall.Filter,TargetCollider=collider,OriginalMesh=wall.Mesh,OriginalColliderMesh=originalCollider,CompanionFilters=filters,CompanionCollider=fixedCollider,CreatedAssetPaths=created.ToArray(),Receipt=receipt};
            }
            finally
            {
                if(!complete)
                {
                    if(assigned){wall.Filter.sharedMesh=wall.Mesh;collider.sharedMesh=originalCollider;}
                    if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                    foreach(string path in created.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(path);
                    foreach(var mesh in memory)if(mesh!=null&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
#endif
