#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
namespace ChooGuard.Tests.PlayMode
{
    internal static class SourceGeometryContractFixtures
    {
        static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
        internal static JObject Read(string name)=>JObject.Parse(File.ReadAllText("content/fixtures/architecture/"+name+".json"));
        static string Norm(string value)=>value.Normalize(System.Text.NormalizationForm.FormC);
        static string Channels(Mesh mesh)
        {
            using(var memory=new MemoryStream())
            {
                using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))
                {
                    writer.Write(mesh.vertexCount);
                    for(int attribute=0;attribute<12;attribute++)
                    {
                        var values=new List<Vector4>();
                        if(attribute==0)values.AddRange(mesh.vertices.Select(value=>new Vector4(value.x,value.y,value.z,0)));
                        else if(attribute==1)values.AddRange(mesh.normals.Select(value=>new Vector4(value.x,value.y,value.z,0)));
                        else if(attribute==2)values.AddRange(mesh.tangents);
                        else if(attribute==3)values.AddRange(mesh.colors.Select(value=>new Vector4(value.r,value.g,value.b,value.a)));
                        else mesh.GetUVs(attribute-4,values);
                        writer.Write(values.Count);writer.Write(values.Count==0?0:mesh.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)attribute));
                        foreach(var value in values){writer.Write(value.x);writer.Write(value.y);writer.Write(value.z);writer.Write(value.w);}
                    }
                    writer.Write(mesh.subMeshCount);
                    for(int sub=0;sub<mesh.subMeshCount;sub++){var indices=mesh.GetIndices(sub);writer.Write((int)mesh.GetTopology(sub));writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);}
                }
                using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(memory.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        internal static Mesh Mesh(JToken pin)
        {
            string path=AssetDatabase.GUIDToAssetPath((string)pin["guid"]);
            Assert.That(Norm(path),Is.EqualTo(Norm((string)pin["asset"])));
            if(!hashes.ContainsKey(path))using(var sha=System.Security.Cryptography.SHA256.Create())hashes[path]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
            Assert.That(hashes[path],Is.EqualTo((string)pin["file_sha256"]));
            long required=long.Parse((string)pin["mesh_file_id"]);
            var candidates=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Where(mesh=>AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long local)&&guid==(string)pin["guid"]&&local==required).ToArray();
            Assert.That(candidates.Length,Is.EqualTo(1));var result=candidates[0];Assert.That(result.isReadable,Is.True);
            Assert.That(result.vertexCount,Is.EqualTo((int)pin["vertex_count"]));
            Assert.That(Channels(result),Is.EqualTo((string)pin["semantic_channels_sha256"]),"Native vertex channels or oriented submesh indices changed in memory");
            Assert.That(result.blendShapeCount,Is.Zero);Assert.That(result.bindposes,Is.Empty);Assert.That(result.boneWeights,Is.Empty);
            return result;
        }
        internal static SourcePartAutomaticButton Button(ArchitectureRuntimeFixture fixture,StationDoor door)
        {
            // Small extruded algorithm fixture: original mesh/UV/normals are the
            // immutable input. It is not represented as a surviving field plate.
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="ExtrudedPressPlateAlgorithmFixture";
            plate.transform.SetParent(fixture.Root.transform,false);plate.transform.localPosition=new Vector3(3,2,3);
            plate.transform.localScale=new Vector3(.02f,.12f,.06f);
            var control=plate.AddComponent<SourcePartAutomaticButton>();control.Door=door;control.Plate=plate.GetComponent<MeshFilter>();
            control.PressDirectionLocal=Vector3.left;control.Travel=.002f;return control;
        }
        internal static void CurrentSourceParts()
        {
            var pins=(JArray)Read("current-preserved-source-parts")["parts"];
            var actual=UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Assert.That(actual.Select(part=>part.SourceId).Distinct().Count(),Is.EqualTo(actual.Length));
            Assert.That(actual.Select(part=>part.SourceId),Is.EquivalentTo(pins.Select(pin=>(string)pin["source_id"])));
            foreach(var pin in pins)
            {
                var part=actual.Single(value=>value.SourceId==(string)pin["source_id"]);
                Assert.That(part.Category,Is.EqualTo((string)pin["category"]));Assert.That(part.SiteAccuracyVerified,Is.False);
                Assert.That(Norm(part.SourcePath),Is.EqualTo(Norm((string)pin["source_path"])));Assert.That(Norm(part.SourceDefinition),Is.EqualTo(Norm((string)pin["source_definition"])));
                Assert.That(part.OperationStatus,Is.EqualTo((string)pin["operation_status"]));
                Assert.That(part.PreservedTriangles,Is.EqualTo((int)pin["preserved_triangles"]));
                var expected=Mesh(pin);var filter=part.GetComponent<MeshFilter>();Assert.That(filter,Is.Not.Null);
                Assert.That(filter.sharedMesh,Is.SameAs(expected));Assert.That(expected.triangles.Length/3,Is.EqualTo(part.PreservedTriangles));
                Assert.That(expected.uv.Length,Is.EqualTo(expected.vertexCount));
            }
        }
    }
}
#endif
