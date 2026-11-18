#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using ChooGuard.Editor;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    public sealed class CurrentStationInventorySupplementTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SupplementPreservesHistoricalNumericAndDateTokensVerbatim(bool multiline)
        {
            const string record="{\"id\":\"base\",\"precise\":0.1764284484825797,\"when\":\"2026-10-19T17:10:48.1537310Z\",\"nested\":[{\"text\":\"[}\\\"\"}]}";
            string original="{\"objects\":["+record+"],\"counts\":{\"objects\":1},\"untouched\": 0.1457059681415558}";
            if(multiline)original=original.Replace("\"objects\":[","\"objects\":\n [").Replace(",\"counts\"",",\n\"counts\"");
            using(var writer=new StringWriter())
            {
                CurrentStationInventorySupplement.WritePreservingBase(writer,original,
                    new JObject{["counts"]=new JObject{["objects"]=2},["currentNativeCensus"]=new JObject{["objectCount"]=2}},
                    new JArray(new JObject{["id"]="added"}));
                string result=writer.ToString();var parsed=JObject.Parse(result);
                Assert.That(result,Does.Contain(record));Assert.That(result,Does.Contain("\"untouched\": 0.1457059681415558"));
                Assert.That(parsed["objects"].Count(),Is.EqualTo(2));Assert.That((string)parsed["objects"][1]["id"],Is.EqualTo("added"));
                Assert.That((int)parsed["counts"]["objects"],Is.EqualTo(2));Assert.That((int)parsed["currentNativeCensus"]["objectCount"],Is.EqualTo(2));
            }
        }
        [Test]
        public void SupplementHandlesEmptyObjectArrayAndReplacesExistingCensus()
        {
            using(var writer=new StringWriter())
            {
                CurrentStationInventorySupplement.WritePreservingBase(writer,"{\"objects\": [ ], \"currentNativeCensus\":{\"objectCount\":0}}",
                    new JObject{["currentNativeCensus"]=new JObject{["objectCount"]=1}},new JArray(new JObject{["id"]="first"}));
                var result=JObject.Parse(writer.ToString());Assert.That(result["objects"].Count(),Is.EqualTo(1));
                Assert.That((int)result["currentNativeCensus"]["objectCount"],Is.EqualTo(1));
            }
        }
        [TestCase("unchanged",true)]
        [TestCase("collider",false)]
        [TestCase("different_mesh",false)]
        [TestCase("offset",false)]
        [TestCase("scale",false)]
        [TestCase("color_shader",false)]
        [TestCase("empty_materials",false)]
        [TestCase("child_geometry",false)]
        [TestCase("different_name",false)]
        public void OnlyExactDepthPassDuplicatesAreExcludedFromCollision(string mutation,bool expected)
        {
            var parent=new GameObject("Saved KTX source",typeof(MeshFilter),typeof(MeshRenderer));
            var child=new GameObject("__KtxOpaqueTexelDepth",typeof(MeshFilter),typeof(MeshRenderer));
            child.transform.SetParent(parent.transform,false);
            var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward},triangles=new[]{0,1,2}};
            var other=new Mesh();var shader=Shader.Find("ChooGuard/KTX/OpaqueTexelDepth");Assert.That(shader,Is.Not.Null);
            var depth=new Material(shader);var color=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                parent.GetComponent<MeshFilter>().sharedMesh=mesh;child.GetComponent<MeshFilter>().sharedMesh=mesh;
                child.GetComponent<MeshRenderer>().sharedMaterials=new[]{depth};
                switch(mutation)
                {
                    case "collider":child.AddComponent<BoxCollider>();break;
                    case "different_mesh":child.GetComponent<MeshFilter>().sharedMesh=other;break;
                    case "offset":child.transform.localPosition=Vector3.right;break;
                    case "scale":child.transform.localScale=Vector3.one*2;break;
                    case "color_shader":child.GetComponent<MeshRenderer>().sharedMaterials=new[]{color};break;
                    case "empty_materials":child.GetComponent<MeshRenderer>().sharedMaterials=System.Array.Empty<Material>();break;
                    case "child_geometry":new GameObject("Unaccounted child").transform.SetParent(child.transform,false);break;
                    case "different_name":child.name="Source body";break;
                }
                Assert.That(CurrentStationInventorySupplement.IsDepthOnly(child),Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(parent);Object.DestroyImmediate(mesh);Object.DestroyImmediate(other);
                Object.DestroyImmediate(depth);Object.DestroyImmediate(color);
            }
        }
    }
}
#endif
