#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChooGuard.Editor
{
    // Subtract explicit WORLD oriented boxes from a scene clone, preserving original assets.
    // Produces a persisted render/collision mesh with identical apertures, interpolated source UV/normals.
    public static class FpsSourceOpening
    {
        private struct Vertex {public Vector3 p,n;public Vector2 uv;public Color c;}
        private sealed class Box {public Plane[] planes;public Bounds aabb;}

        public static void Cut(MeshFilter target,Bounds opening,Vector3 euler,string generatedAsset)
        {
            if(AssetDatabase.LoadAssetAtPath<Mesh>(generatedAsset)!=null)throw new InvalidOperationException("Generated opening asset already exists");
            var mesh=Subtract(target,new[]{(opening,euler)},target.sharedMesh.name+"_AuthoredEntranceCut");
            AssetDatabase.CreateAsset(mesh,generatedAsset);Assign(target,mesh);
        }

        // All boxes are applied in one pass; triangles whose bounds miss a box skip its clipping exactly.
        public static Mesh Subtract(MeshFilter target,IReadOnlyList<(Bounds box,Vector3 euler)> openings,string meshName)
        {
            var source=target.sharedMesh;if(source==null||!source.isReadable)throw new InvalidOperationException("Readable reviewed source mesh required for opening");
            if(source.uv2.Length>0)throw new InvalidOperationException("Opening helper does not preserve baked secondary UV; review source first");
            var boxes=new List<Box>();foreach(var o in openings)boxes.Add(Make(o.box,o.euler));
            var positions=source.vertices;var normals=source.normals;var uvs=source.uv;var colors=source.colors;
            var vertices=new List<Vector3>();var outNormals=new List<Vector3>();var outUvs=new List<Vector2>();var outColors=new List<Color>();var submeshes=new List<int[]>();
            var work=new List<List<Vertex>>();var next=new List<List<Vertex>>();
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var indices=new List<int>();var triangles=source.GetTriangles(sub);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var polygon=new List<Vertex>(3);for(int j=0;j<3;j++){int index=triangles[i+j];polygon.Add(new Vertex{p=target.transform.TransformPoint(positions[index]),n=normals.Length==positions.Length?normals[index]:Vector3.up,uv=uvs.Length==positions.Length?uvs[index]:Vector2.zero,c=colors.Length==positions.Length?colors[index]:Color.white});}
                    work.Clear();work.Add(polygon);
                    foreach(var box in boxes)
                    {
                        next.Clear();
                        foreach(var poly in work)
                        {
                            if(!Overlaps(poly,box.aabb)){next.Add(poly);continue;}
                            // At each box plane keep the outside fragment, carry only the inside fragment onward.
                            var inside=poly;
                            foreach(var plane in box.planes)
                            {
                                if(inside.Count<3)break;var outside=Clip(inside,plane,false);if(outside.Count>=3)next.Add(outside);inside=Clip(inside,plane,true);
                            }
                        }
                        var swap=work;work=next;next=swap;
                    }
                    foreach(var poly in work)Emit(poly,target.transform,vertices,outNormals,outUvs,outColors,indices);
                }
                submeshes.Add(indices.ToArray());
            }
            var mesh=new Mesh{name=meshName,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(outNormals);mesh.SetUVs(0,outUvs);mesh.SetColors(outColors);mesh.subMeshCount=submeshes.Count;for(int sub=0;sub<submeshes.Count;sub++)mesh.SetTriangles(submeshes[sub],sub);mesh.RecalculateBounds();mesh.RecalculateTangents();
            return mesh;
        }

        public static void Assign(MeshFilter target,Mesh mesh)
        {
            target.sharedMesh=mesh;var collider=target.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=mesh;
        }

        private static Box Make(Bounds opening,Vector3 euler)
        {
            var rotation=Quaternion.Euler(euler);var planes=new List<Plane>();var aabb=new Bounds(opening.center,Vector3.zero);
            foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
            {
                var direction=rotation*axis;float extent=Vector3.Dot(opening.extents,axis);
                planes.Add(new Plane(direction,opening.center-direction*extent));
                planes.Add(new Plane(-direction,opening.center+direction*extent));
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)aabb.Encapsulate(opening.center+rotation*Vector3.Scale(opening.extents,new Vector3(x,y,z)));
            return new Box{planes=planes.ToArray(),aabb=aabb};
        }
        private static bool Overlaps(List<Vertex> poly,Bounds aabb)
        {
            var b=new Bounds(poly[0].p,Vector3.zero);for(int i=1;i<poly.Count;i++)b.Encapsulate(poly[i].p);return b.Intersects(aabb);
        }
        private static List<Vertex> Clip(List<Vertex> input,Plane plane,bool inside)
        {
            var output=new List<Vertex>();for(int i=0;i<input.Count;i++)
            {
                var a=input[i];var b=input[(i+1)%input.Count];float da=plane.GetDistanceToPoint(a.p),db=plane.GetDistanceToPoint(b.p);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;
                if(ia)output.Add(a);if(ia!=ib){float t=da/(da-db);output.Add(new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)});}
            }return output;
        }
        private static void Emit(List<Vertex> polygon,Transform target,List<Vector3> positions,List<Vector3> normals,List<Vector2> uv,List<Color> colors,List<int> triangles)
        {
            if(polygon.Count<3)return;int start=positions.Count;foreach(var v in polygon){positions.Add(target.InverseTransformPoint(v.p));normals.Add(v.n);uv.Add(v.uv);colors.Add(v.c);}for(int i=1;i<polygon.Count-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
    }
}
#endif
