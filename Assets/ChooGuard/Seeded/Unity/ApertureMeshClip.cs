using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Seeded.Unity
{
    // Subtract an explicit source-qualified local AABB from one isolated mesh copy.
    // Original vertices/channels remain unchanged; boundary vertices interpolate
    // the source triangle's attributes. Skinned/morph/unknown channels refuse.
    public static class ApertureMeshClip
    {
        struct Vertex{public Vector3 P,B;public int Index;}
        public sealed class Result{public Mesh Mesh;public int OriginalTriangles,ResultTriangles,AffectedTriangles,AddedVertices;}
        public static Result Subtract(Mesh source,Bounds box)
        {
            if(source.blendShapeCount!=0||source.boneWeights.Length!=0)throw new InvalidOperationException("Unsupported skinned/morph source aperture");
            var attrs=source.GetVertexAttributes();
            if(attrs.Any(x=>x.attribute==VertexAttribute.BlendWeight||x.attribute==VertexAttribute.BlendIndices||x.format!=VertexAttributeFormat.Float32&&!(x.attribute==VertexAttribute.Color&&x.format==VertexAttributeFormat.UNorm8)))throw new InvalidOperationException("Unsupported source vertex-channel format");
            var positions=source.vertices.ToList();var normals=source.normals.ToList();var tangents=source.tangents.ToList();var colors=source.colors.ToList();var colors32=source.colors32.ToList();
            bool colorBytes=attrs.Any(x=>x.attribute==VertexAttribute.Color&&x.format==VertexAttributeFormat.UNorm8);
            var uvs=new List<Vector4>[8];var dimensions=new int[8];for(int c=0;c<8;c++){uvs[c]=new List<Vector4>();source.GetUVs(c,uvs[c]);dimensions[c]=source.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+c));}
            int n=source.vertexCount;var output=new List<int>[source.subMeshCount];var result=new Result();
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                if(source.GetTopology(sub)!=MeshTopology.Triangles)throw new InvalidOperationException("Triangle source required");
                output[sub]=new List<int>();var indices=source.GetTriangles(sub);
                for(int t=0;t<indices.Length;t+=3)
                {
                    result.OriginalTriangles++;var ids=new[]{indices[t],indices[t+1],indices[t+2]};
                    var polygon=new List<Vertex>{new Vertex{P=positions[ids[0]],B=Vector3.right,Index=ids[0]},new Vertex{P=positions[ids[1]],B=Vector3.up,Index=ids[1]},new Vertex{P=positions[ids[2]],B=Vector3.forward,Index=ids[2]}};
                    var kept=new List<List<Vertex>>();bool touched=true;
                    for(int plane=0;plane<6&&polygon.Count>0;plane++)
                    {
                        int axis=plane/2;bool lower=plane%2==0;float bound=lower?box.min[axis]:box.max[axis];
                        var outside=Clip(polygon,axis,bound,!lower);if(outside.Count>=3)kept.Add(outside);
                        polygon=Clip(polygon,axis,bound,lower);
                    }
                    // The remaining polygon lies inside the cut. If absent, the
                    // source triangle is untouched and retains its exact indices.
                    touched=polygon.Count>=3&&Area(polygon)>1e-12f;
                    if(!touched){output[sub].AddRange(ids);continue;}
                    result.AffectedTriangles++;
                    foreach(var piece in kept)
                    {
                        for(int j=1;j+1<piece.Count;j++)
                        {
                            var triangle=new[]{piece[0],piece[j],piece[j+1]};if(Vector3.Cross(triangle[1].P-triangle[0].P,triangle[2].P-triangle[0].P).sqrMagnitude<1e-16f)continue;
                            foreach(var v in triangle)
                            {
                                int index=v.Index;
                                if(index<0)
                                {
                                    index=positions.Count;positions.Add(v.P);var b=v.B;
                                    if(normals.Count>0)normals.Add((source.normals[ids[0]]*b.x+source.normals[ids[1]]*b.y+source.normals[ids[2]]*b.z).normalized);
                                    if(tangents.Count>0)tangents.Add(source.tangents[ids[0]]*b.x+source.tangents[ids[1]]*b.y+source.tangents[ids[2]]*b.z);
                                    if(colors.Count>0){Color color=source.colors[ids[0]]*b.x+source.colors[ids[1]]*b.y+source.colors[ids[2]]*b.z;colors.Add(color);colors32.Add((Color32)color);}
                                    for(int c=0;c<8;c++)if(dimensions[c]>0){var original=new List<Vector4>();source.GetUVs(c,original);uvs[c].Add(original[ids[0]]*b.x+original[ids[1]]*b.y+original[ids[2]]*b.z);}
                                }
                                output[sub].Add(index);
                            }
                        }
                    }
                }
            }
            if(result.AffectedTriangles==0)throw new InvalidOperationException("No source body triangles intersect the qualified aperture");
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+"_SOURCE_DOOR_APERTURE_DIAGNOSTIC";mesh.Clear();mesh.indexFormat=positions.Count>65535?IndexFormat.UInt32:source.indexFormat;mesh.SetVertices(positions);
            if(normals.Count>0)mesh.SetNormals(normals);if(tangents.Count>0)mesh.SetTangents(tangents);if(colors.Count>0){if(colorBytes)mesh.SetColors(colors32);else mesh.SetColors(colors);}
            for(int c=0;c<8;c++)switch(dimensions[c]){case 0:break;case 2:mesh.SetUVs(c,uvs[c].Select(v=>new Vector2(v.x,v.y)).ToList());break;case 3:mesh.SetUVs(c,uvs[c].Select(v=>new Vector3(v.x,v.y,v.z)).ToList());break;case 4:mesh.SetUVs(c,uvs[c]);break;default:UnityEngine.Object.DestroyImmediate(mesh);throw new InvalidOperationException("Unsupported source UV dimension");}
            mesh.subMeshCount=source.subMeshCount;for(int sub=0;sub<output.Length;sub++){mesh.SetTriangles(output[sub],sub);result.ResultTriangles+=output[sub].Count/3;}mesh.RecalculateBounds();
            result.Mesh=mesh;result.AddedVertices=positions.Count-n;return result;
        }
        static float Area(List<Vertex> p){float a=0;for(int j=1;j+1<p.Count;j++)a+=Vector3.Cross(p[j].P-p[0].P,p[j+1].P-p[0].P).magnitude;return a;}
        static List<Vertex> Clip(List<Vertex> input,int axis,float bound,bool greater)
        {
            var output=new List<Vertex>();if(input.Count==0)return output;var previous=input[input.Count-1];float pd=greater?previous.P[axis]-bound:bound-previous.P[axis];bool pin=pd>=0;
            foreach(var current in input)
            {
                float cd=greater?current.P[axis]-bound:bound-current.P[axis];bool cin=cd>=0;
                if(cin!=pin){float ratio=pd/(pd-cd);output.Add(new Vertex{P=Vector3.LerpUnclamped(previous.P,current.P,ratio),B=Vector3.LerpUnclamped(previous.B,current.B,ratio),Index=-1});}
                if(cin)output.Add(current);previous=current;pd=cd;pin=cin;
            }
            return output;
        }
    }
}
