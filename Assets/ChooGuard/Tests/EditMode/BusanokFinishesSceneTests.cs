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
    public sealed class BusanokFinishesSceneTests
    {
        static void SameChannels(Mesh source,Mesh result)
        {
            Assert.That(result.vertices,Is.EqualTo(source.vertices));Assert.That(result.normals,Is.EqualTo(source.normals));
            Assert.That(result.tangents,Is.EqualTo(source.tangents));Assert.That(result.colors,Is.EqualTo(source.colors));
            for(int channel=0;channel<8;channel++){var a=new List<Vector4>();var b=new List<Vector4>();source.GetUVs(channel,a);result.GetUVs(channel,b);Assert.That(b,Is.EqualTo(a));}
        }
        [Test]
        public void CanonicalCeilingInstallerRequiresItsExactActiveMaterialAndUnmovedSourceCompanion()
        {
            using(var fixture=new CanonicalStructuralDependencyFixture("/Box_Majibang_ShopCeiling.asset"))
            {
                Assert.That(fixture.Resolve(),Is.SameAs(fixture.Target));
                var surface=fixture.Surfaces.Single();var renderer=surface.GetComponent<MeshRenderer>();
                try{renderer.enabled=false;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}finally{renderer.enabled=true;}
                try{surface.gameObject.SetActive(false);Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}finally{surface.gameObject.SetActive(true);}
                var position=surface.transform.position;
                try{surface.transform.position=position+Vector3.up*.001f;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}finally{surface.transform.position=position;}
                var original=renderer.sharedMaterial;var other=new Material(original);
                try{renderer.sharedMaterial=other;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}
                finally{renderer.sharedMaterial=original;UnityEngine.Object.DestroyImmediate(other);}
                Assert.That(fixture.Resolve(),Is.SameAs(fixture.Target));
            }
        }
        [Test]
        public void NativeCeilingInstallerRetainsEveryOtherSourceFaceAndOriginalChannel()
        {
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ChooGuard/Art/StationInterior/Majibang/Box_Majibang_ShopCeiling.asset");
            string before=ArchitecturalCorrectionRegistry.CompleteChannelDigest(source);
            var result=BusanokFinishesSetup.RetainNativeCeiling(source);
            try{SameChannels(source,result);Assert.That(result.triangles,Is.EqualTo(source.triangles.Skip(6)));Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(source),Is.EqualTo(before));}
            finally{UnityEngine.Object.DestroyImmediate(result);}
            var changed=UnityEngine.Object.Instantiate(source);
            try{var vertices=changed.vertices;vertices[0]+=Vector3.up*.01f;changed.vertices=vertices;Assert.Throws<InvalidDataException>(()=>BusanokFinishesSetup.RetainNativeCeiling(changed));}
            finally{UnityEngine.Object.DestroyImmediate(changed);}
        }
        [Test]
        public void SupersededPublicFloorUsesTheExplicitCanonicalOmissionContract()
        {
            var rows=(JArray)JObject.Parse(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry))["entries"];
            var row=rows.Single(entry=>((string)entry["source_asset"]).EndsWith("/Box_Majibang_3F_L.asset",StringComparison.Ordinal));
            Assert.That((string)row["upper_native_operation"],Is.EqualTo("retire_entire_superseded_legacy_public_architectural_surface"));
            Assert.That((string)row["upper_native_collision_policy"]["action"],Is.EqualTo("omit_on_rebuild"));
            var retained=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["replacement_asset"]);
            Assert.That(retained.triangles,Is.Empty);Assert.That(UpperRetiredSurfaceCollisionGuard.HasNonDegenerateTriangle(retained),Is.False);
            Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(retained),Is.EqualTo((string)row["replacement_complete_channels_sha256"]));
        }
    }
}
