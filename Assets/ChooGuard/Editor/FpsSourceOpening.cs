#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChooGuard.Editor
{
    // Subtract an explicit WORLD oriented box from a scene clone, preserving original assets.
    // Produces a persisted render/collision mesh with identical aperture, interpolated source UV/normals.
    public static class FpsSourceOpening
    {
        private struct Vertex {public Vector3 p,n;public Vector2 uv;public Color c;}
        public static void Cut(MeshFilter target,Bounds opening,Vector3 euler,string generatedAsset)
        {
            var source=target.sharedMesh;if(source==null||!source.isReadable)throw new InvalidOperationException("Readable reviewed source mesh required for opening");
            var positions=source.vertices;var normals=source.normals;var uvs=source.uv;var colors=source.colors;
            if(source.uv2.Length>0)throw new InvalidOperationException("Opening helper does not preserve baked secondary UV; review source first");
            var vertices=new List<Vector3>();var outNormals=new List<Vector3>();var outUvs=new List<Vector2>();var outColors=new List<Color>();var submeshes=new List<int[]>();
            var rotation=Quaternion.Euler(euler);var planes=new List<Plane>();
            foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
            {
                var direction=rotation*axis;float extent=Vector3.Dot(opening.extents,axis);
                planes.Add(new Plane(direction,opening.center-direction*extent));
                planes.Add(new Plane(-direction,opening.center+direction*extent));
            }
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var indices=new List<int>();var triangles=source.GetTriangles(sub);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var polygon=new List<Vertex>();for(int j=0;j<3;j++){int index=triangles[i+j];polygon.Add(new Vertex{p=target.transform.TransformPoint(positions[index]),n=normals.Length==positions.Length?normals[index]:Vector3.up,uv=uvs.Length==positions.Length?uvs[index]:Vector2.zero,c=colors.Length==positions.Length?colors[index]:Color.white});}
                    // At each box plane emit the outside fragment, carry only inside fragment onward.
                    foreach(var plane in planes)
                    {
                        if(polygon.Count<3)break;Emit(Clip(polygon,plane,false),target.transform,vertices,outNormals,outUvs,outColors,indices);polygon=Clip(polygon,plane,true);
                    }
                }
                submeshes.Add(indices.ToArray());
            }
            var mesh=new Mesh{name=source.name+"_AuthoredEntranceCut",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(outNormals);mesh.SetUVs(0,outUvs);mesh.SetColors(outColors);mesh.subMeshCount=submeshes.Count;for(int sub=0;sub<submeshes.Count;sub++)mesh.SetTriangles(submeshes[sub],sub);mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(AssetDatabase.LoadAssetAtPath<Mesh>(generatedAsset)!=null)throw new InvalidOperationException("Generated opening asset already exists");AssetDatabase.CreateAsset(mesh,generatedAsset);target.sharedMesh=mesh;
            var collider=target.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=mesh;
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
