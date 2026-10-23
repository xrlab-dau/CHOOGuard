using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChooGuard.Editor.Twin;
namespace ChooGuard.Tests.EditMode
{
    public sealed class NativeCounterleafFragmentsSceneTests
    {
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static void SameChannels(Mesh source,Mesh result)
        {
            Assert.That(result.vertices,Is.EqualTo(source.vertices));Assert.That(result.normals,Is.EqualTo(source.normals));
            Assert.That(result.tangents,Is.EqualTo(source.tangents));Assert.That(result.colors,Is.EqualTo(source.colors));
            for(int channel=0;channel<8;channel++){var a=new List<Vector4>();var b=new List<Vector4>();source.GetUVs(channel,a);result.GetUVs(channel,b);Assert.That(b,Is.EqualTo(a));}
        }
        [Test]
        public void PreservedTwentyFourIndexedFacesRemainExactAndFilteringNeverChangesSourceChannels()
        {
            var fixture=JObject.Parse(File.ReadAllText("content/fixtures/architecture/native-counterleaf-face-selection.json"));
            var rows=(JArray)fixture["fragments"];Assert.That(rows.Count,Is.EqualTo(6));int total=0;
            foreach(var row in rows)
            {
                string path=(string)row["fragment_asset"];Assert.That(Hash(path),Is.EqualTo((string)row["fragment_file_sha256"]));
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(path);string before=ArchitecturalCorrectionRegistry.CompleteChannelDigest(source);
                var selected=row["selected_indices_by_submesh"].ToObject<int[][]>();
                var result=ReviewedNativeCounterleafRecoverySetup.PreserveSourceFaceSelection(source,selected);
                try
                {
                    SameChannels(source,result);Assert.That(result.subMeshCount,Is.EqualTo(source.subMeshCount));
                    for(int sub=0;sub<selected.Length;sub++)Assert.That(result.GetTriangles(sub),Is.EqualTo(selected[sub]));
                    Assert.That(result.triangles.Length/3,Is.EqualTo((int)row["retained_triangles"]));total+=result.triangles.Length/3;
                    Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(source),Is.EqualTo(before));
                }
                finally{UnityEngine.Object.DestroyImmediate(result);}
            }
            Assert.That(total,Is.EqualTo(24));
        }
        [Test]
        public void SelectionRejectsReversedRepeatedInventedAndCrossMaterialFaces()
        {
            var source=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.forward},normals=Enumerable.Repeat(Vector3.forward,4).ToArray()};
            source.subMeshCount=2;source.SetTriangles(new[]{0,1,2},0);source.SetTriangles(new[]{0,2,3},1);
            try
            {
                var valid=ReviewedNativeCounterleafRecoverySetup.PreserveSourceFaceSelection(source,new[]{new[]{0,1,2},Array.Empty<int>()});
                try{SameChannels(source,valid);Assert.That(valid.triangles,Is.EqualTo(new[]{0,1,2}));}finally{UnityEngine.Object.DestroyImmediate(valid);}
                foreach(var selection in new[]{new[]{new[]{1,0,2},Array.Empty<int>()},new[]{new[]{0,1,2,0,1,2},Array.Empty<int>()},new[]{new[]{0,1,3},Array.Empty<int>()},new[]{new[]{0,2,3},Array.Empty<int>()}})
                    Assert.Throws<InvalidDataException>(()=>ReviewedNativeCounterleafRecoverySetup.PreserveSourceFaceSelection(source,selection));
            }
            finally{UnityEngine.Object.DestroyImmediate(source);}
        }
    }
}
