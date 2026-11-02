// CPU-only geometry/BVH derivative. Source Mesh, renderer, importer and collider bindings remain unchanged.
// Data extraction API foundation: Unity MeshApiExamples e82668f26fbf615d3ee4b1a5941c8fe167d3d485 (MIT).
using System;
using UnityEngine;
namespace ChooGuard.App.Fps.TrainDeparture
{
    [Serializable] public sealed class VisualSubmesh { public int[] Indices=Array.Empty<int>(); }
    [Serializable] public sealed class VisualMeshData
    {
        public Vector3[] Vertices=Array.Empty<Vector3>(), Normals=Array.Empty<Vector3>();
        public Vector2[] Uv=Array.Empty<Vector2>();
        public VisualSubmesh[] Submeshes=Array.Empty<VisualSubmesh>();
        public VisualTriangleBvh Bvh;
        public long PayloadBytes
        {
            get {long bytes=Vertices.LongLength*12+Normals.LongLength*12+Uv.LongLength*8+(Bvh?.PayloadBytes??0);
                foreach(var sub in Submeshes)bytes+=sub.Indices.LongLength*4;return bytes;}
        }
        public void Validate()
        {
            if(Vertices==null||Normals==null||Uv==null||Submeshes==null)throw new ArgumentException("Missing geometry payload");
            foreach(var v in Vertices)if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z))throw new ArgumentException("Nonfinite vertex");
            foreach(var sub in Submeshes)
            {
                if(sub==null||sub.Indices==null||sub.Indices.Length%3!=0)throw new ArgumentException("Invalid triangle submesh");
                foreach(int index in sub.Indices)if(index<0||index>=Vertices.Length)throw new ArgumentException("Triangle index outside vertex buffer");
            }
        }
    }
    [PreferBinarySerialization]
    public sealed class VisualGeometryCache : ScriptableObject
    {
        public const int CurrentVersion=1;
        public int Version=CurrentVersion;
        public Mesh Source;
        public string SourceGuid, SourceDependencyHash;
        public long SourceLocalId;
        public VisualMeshData Data;
        public bool Matches(Mesh mesh)
        {
            if(Version!=CurrentVersion||Source!=mesh||mesh==null||Data==null||Data.Bvh==null||Data.Vertices.Length!=mesh.vertexCount||Data.Submeshes.Length!=mesh.subMeshCount)return false;
            for(int s=0;s<Data.Submeshes.Length;s++)if(Data.Submeshes[s].Indices.LongLength!=(long)mesh.GetIndexCount(s))return false;
            return true;
        }
    }
}
