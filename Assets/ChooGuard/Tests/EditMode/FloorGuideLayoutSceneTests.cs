using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChooGuard.Editor.Twin;

namespace ChooGuard.Tests.EditMode
{
    // Retained source-asset algorithm contracts. Current saved floor bindings are checked separately.
    public sealed class FloorGuideLayoutSceneTests
    {
        [Serializable] sealed class Entry { public string source_asset,baseline_asset,replacement_asset,source_digest,replacement_digest; }
        [Serializable] sealed class Registry { public Entry[] meshes; }
        static Entry[] Entries() => JsonUtility.FromJson<Registry>(File.ReadAllText(FloorGuideLayoutRegistry.Registry)).meshes;
        static readonly string[] RearUnitIds={"10061928","10061929","10061927","10061931","10061932","10061930","10061933","10061935"};
        static string WallAsset(int number)=>"/Ground_Unit_"+RearUnitIds[number-9]+"_Walls.asset";
        static Vector3 World(float u,float v,float y)
        {
            float a=16.2f*Mathf.Deg2Rad;return new Vector3(u*Mathf.Sin(a)+v*Mathf.Cos(a),y,u*Mathf.Cos(a)-v*Mathf.Sin(a));
        }

        [Test]
        public void EndRestaurantAssetStageRetainsOriginalVerticesAndTheReviewedEnvelope()
        {
            var polygon=new[]{new Vector2(-71.65367f,-19.841028f),new Vector2(-71.02534f,-36.83128f),new Vector2(-78.310524f,-34.870068f),new Vector2(-80.65121f,-34.16302f),new Vector2(-90.31855f,-30.644545f),new Vector2(-90.46316f,-28.643759f),new Vector2(-86.72622f,-28.393736f),new Vector2(-86.64845f,-27.464f),new Vector2(-82.126f,-27.464f),new Vector2(-82.60702f,-20.57387f)};
            var entry=Entries().Single(e=>e.source_asset.EndsWith("/Ground_Unit_4912_Walls.asset",StringComparison.Ordinal));
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
            Assert.That(source.vertexCount,Is.EqualTo(40));Assert.That(mesh.vertexCount,Is.EqualTo(112));Assert.That(mesh.triangles.Length/3,Is.EqualTo(36));
            Assert.That(mesh.vertices.Take(source.vertexCount),Is.EqualTo(source.vertices));
            Assert.That(mesh.triangles.All(i=>i>=source.vertexCount),Is.True,"Old seven-point walls returned");
            foreach(var corner in polygon)Assert.That(mesh.vertices.Min(v=>Vector2.Distance(new Vector2(v.z,v.x),corner)),Is.LessThan(.002f),"Guide envelope corner missing");
            Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(source,entry.source_asset),Is.SameAs(mesh));
        }

        [Test]
        public void GuideStagePreservesAttributesAndRejectsChangedUVOrUnreviewedPositions()
        {
            var entries=Entries();Assert.That(entries.Length,Is.GreaterThanOrEqualTo(48));
            foreach(var entry in entries)
            {
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
                var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(source),Is.EqualTo(entry.source_digest));
                Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(target),Is.EqualTo(entry.replacement_digest));
                Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(source,entry.source_asset),Is.SameAs(target));
                Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(target,entry.source_asset),Is.SameAs(target));
                if(entry.source_asset.Contains("/1F_public/Kit_Clad_"))continue;
                Assert.That(target.normals.Take(source.vertexCount),Is.EqualTo(source.normals));
                Assert.That(target.tangents.Take(source.vertexCount),Is.EqualTo(source.tangents));
                for(int channel=0;channel<8;channel++)
                {
                    var before=new System.Collections.Generic.List<Vector4>();var after=new System.Collections.Generic.List<Vector4>();
                    source.GetUVs(channel,before);target.GetUVs(channel,after);Assert.That(after.Take(before.Count),Is.EqualTo(before));
                }
            }
            var selected=entries.Single(e=>e.source_asset.EndsWith(WallAsset(11),StringComparison.Ordinal));
            var mesh=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(selected.baseline_asset));
            try
            {
                var uv=mesh.uv;uv[0]+=Vector2.one*.01f;mesh.uv=uv;
                Assert.Throws<InvalidDataException>(()=>FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(mesh,selected.source_asset));
                mesh.uv=AssetDatabase.LoadAssetAtPath<Mesh>(selected.baseline_asset).uv;
                var vertices=mesh.vertices;vertices[0]+=Vector3.right*.01f;mesh.vertices=vertices;
                Assert.Throws<InvalidDataException>(()=>FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(mesh,selected.source_asset));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [Test]
        public void EightRearShopStagesRejectUnreviewedNativeSourceChanges()
        {
            var entries=Entries();
            foreach(int number in Enumerable.Range(9,8))
            {
                var entry=entries.Single(row=>row.source_asset.EndsWith(WallAsset(number),StringComparison.Ordinal));
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
                var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(source,entry.source_asset),Is.SameAs(target));
                Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(target),Is.EqualTo(entry.replacement_digest));
                var changed=UnityEngine.Object.Instantiate(source);
                try{var vertices=changed.vertices;vertices[0]+=Vector3.up*.01f;changed.vertices=vertices;Assert.Throws<InvalidDataException>(()=>FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(changed,entry.source_asset));}
                finally{UnityEngine.Object.DestroyImmediate(changed);}
            }
        }
        [Test]
        public void RawShopCollisionReplayKeepsItsExactPartitionAndWithholdsSecondaryCaps()
        {
            AssertPinnedShopCollisionPolicy();
        }
        [Test]
        public void ArtboxAssetStageKeepsNativeCornersAndTheReviewedGuideSetback()
        {
            var entry=Entries().Single(e=>e.source_asset.EndsWith("/Ground_Unit_4916_Walls.asset",StringComparison.Ordinal));
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
            var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
            var original=source.vertices;var actual=target.vertices;
            foreach(var corner in new[]{new Vector2(-1.439f,-34.939f),new Vector2(-.576f,-34.955f),new Vector2(-.658f,-35.146f),new Vector2(-1.42f,-35.205f),new Vector2(-.919f,-42.228f),new Vector2(6.464f,-41.912f)})
            {
                var vertices=Enumerable.Range(0,original.Length).Where(i=>Vector2.Distance(new Vector2(original[i].z,original[i].x),corner)<.002f).ToArray();
                foreach(int i in vertices)Assert.That(actual[i],Is.EqualTo(original[i]),"Fixed native corner moved");
            }
            foreach(var corner in new[]{new Vector2(-1.9620413f,-27.59978f),new Vector2(5.7687864f,-27.59978f),new Vector2(4.6491764f,-27.59978f)})
                Assert.That(actual.Min(v=>Vector2.Distance(new Vector2(v.z,v.x),corner)),Is.LessThan(.002f),"Guide aisle setback missing");
            Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(source,entry.source_asset),Is.SameAs(target));
        }
        [Test]
        public void BakerySourceAssetsKeepTheirExactSharedPartitionEndpoints()
        {
            var entries=Entries();
            foreach(int number in new[]{15,16})
            {
                var entry=entries.Single(row=>row.source_asset.EndsWith(WallAsset(number),StringComparison.Ordinal));
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(source,entry.source_asset),Is.SameAs(mesh));
                // Retained authored source basis, independent of any installed shop owner.
                var corners=mesh.vertices.Select(vertex=>Quaternion.Euler(0,16.2f,0)*vertex).ToArray();
                foreach(var endpoint in new[]{World(-6.593f,-6.761f,0),World(-5.62f,-20.708f,0)})
                    Assert.That(corners.Min(vertex=>Vector2.Distance(new Vector2(vertex.x,vertex.z),new Vector2(endpoint.x,endpoint.z))),Is.LessThan(.002f));
            }
        }
        [Test]
        public void LatestPublishedJointPartitionRetainsOtherFacesAndKeepsWithdrawnBOHFacesAbsent()
        {
            var fixture=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("content/fixtures/architecture/published-joint-partition.json"));
            var entry=Entries().Single(row=>row.source_asset.EndsWith("/1F_public/Kit_Joint.asset",StringComparison.Ordinal));
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
            var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
            var removed=fixture["removed_ordinals"].ToObject<int[]>();
            Assert.That(Enumerable.Range(76,16).Except(removed),Is.Empty,"The original withdrawn BOH faces cannot return");
            var result=InteriorEnvelopeRepair.KeepOtherTriangles(source,removed);
            try
            {
                Assert.That(result.triangles,Is.EqualTo(target.triangles));
                Assert.That(result.vertices,Is.EqualTo(source.vertices));
                Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(result),Is.EqualTo(entry.replacement_digest));
                Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(source,entry.source_asset),Is.SameAs(target));
            }
            finally{UnityEngine.Object.DestroyImmediate(result);}
        }
        static void AssertPinnedShopCollisionPolicy()
        {
            const string rawPath="Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/Kit_Colliders.asset";
            var entry=Entries().Single(e=>e.source_asset==rawPath);
            var raw=AssetDatabase.LoadAssetAtPath<Mesh>(rawPath);
            var baseline=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
            var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
            Assert.That(raw.vertexCount,Is.EqualTo(768));
            Assert.That(raw.triangles.Length/3,Is.EqualTo(384));
            Assert.That(baseline.triangles.Length/3,Is.EqualTo(132));
            Assert.That(FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(raw,rawPath),Is.SameAs(target));
            Assert.That(target.triangles,Is.EqualTo(baseline.triangles));
            foreach(int mutation in Enumerable.Range(0,5))
            {
                var changed=UnityEngine.Object.Instantiate(raw);
                try
                {
                    if(mutation==0){var a=changed.uv;a[0]+=Vector2.one*.01f;changed.uv=a;}
                    if(mutation==1){var a=changed.vertices;a[0]+=Vector3.up*.01f;changed.vertices=a;}
                    if(mutation==2){var a=changed.normals;a[0]=-a[0];changed.normals=a;}
                    if(mutation==3){var a=changed.tangents;a[0]+=Vector4.one*.01f;changed.tangents=a;}
                    if(mutation==4){var a=changed.triangles;int first=a[0];a[0]=a[1];a[1]=first;changed.triangles=a;}
                    Assert.Throws<InvalidDataException>(()=>FloorGuideLayoutRegistry.ResolveGuideStageForRebuild(changed,rawPath));
                }
                finally{UnityEngine.Object.DestroyImmediate(changed);}
            }
            // Retired Scene owners are not recreated. Exercise the renderer policy
            // on temporary fixture objects only, including their shadow children.
            var temporary=new GameObject("실내 트윈 마감"){hideFlags=HideFlags.HideAndDontSave};
            temporary.SetActive(false);
            try
            {
                var root=new GameObject("점포 내부 1F");root.transform.SetParent(temporary.transform,false);
                foreach(string name in new[]{"Kit_CeilingGrid","Kit_Light_조명기구_downlight","Kit_ShopCeiling_Kit_GridWhite"})
                {
                    var cap=Entries().Single(e=>e.source_asset=="Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/"+name+".asset");
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(cap.replacement_asset);
                    var owner=new GameObject(name);owner.transform.SetParent(root.transform,false);
                    owner.AddComponent<MeshFilter>().sharedMesh=mesh;owner.AddComponent<MeshRenderer>();
                    var shadow=new GameObject("ShadowCaster");shadow.transform.SetParent(owner.transform,false);
                    shadow.AddComponent<MeshFilter>().sharedMesh=mesh;
                    shadow.AddComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                FloorGuideLayoutRegistry.WithholdRetainedShopEnvelopeRenderers(root.transform);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,Is.EqualTo(6));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).All(r=>!r.enabled),Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(temporary);}
        }
    }
}
