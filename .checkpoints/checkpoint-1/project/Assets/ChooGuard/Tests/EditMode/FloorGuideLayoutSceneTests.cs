using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChooGuard.Editor.Twin;
using ChooGuard.App.Fps.Facilities;

namespace ChooGuard.Tests.EditMode
{
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
        static bool Inside(Vector2 point,Vector2[] polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                if((polygon[i].y>point.y)!=(polygon[j].y>point.y)&&point.x<(polygon[j].x-polygon[i].x)*(point.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;
            return inside;
        }
        [Test]
        public void EndRestaurantUsesTheGuideEnvelopeAndRetainsItsOriginalFlushFloorAndDoorLeaves()
        {
            InStation(()=>
            {
                var polygon=new[]{new Vector2(-71.65367f,-19.841028f),new Vector2(-71.02534f,-36.83128f),new Vector2(-78.310524f,-34.870068f),new Vector2(-80.65121f,-34.16302f),new Vector2(-90.31855f,-30.644545f),new Vector2(-90.46316f,-28.643759f),new Vector2(-86.72622f,-28.393736f),new Vector2(-86.64845f,-27.464f),new Vector2(-82.126f,-27.464f),new Vector2(-82.60702f,-20.57387f)};
                var entry=Entries().Single(e=>e.source_asset.EndsWith("/Ground_Unit_4912_Walls.asset",StringComparison.Ordinal));
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                Assert.That(source.vertexCount,Is.EqualTo(40));Assert.That(mesh.vertexCount,Is.EqualTo(112));Assert.That(mesh.triangles.Length/3,Is.EqualTo(36));
                Assert.That(mesh.vertices.Take(source.vertexCount),Is.EqualTo(source.vertices));
                Assert.That(mesh.triangles.All(i=>i>=source.vertexCount),Is.True,"Old seven-point walls returned");
                foreach(var corner in polygon)Assert.That(mesh.vertices.Min(v=>Vector2.Distance(new Vector2(v.z,v.x),corner)),Is.LessThan(.002f),"Guide envelope corner missing");
                var owner=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(f=>f.sharedMesh==mesh);
                Assert.That(owner.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(mesh));
                var nativeFloor=GameObject.Find("1층 · 원본 정합/Ground_Floor").GetComponent<MeshCollider>();
                var plazaFloor=GameObject.Find("FPSWorld/공식 자료 부산역 주변 공간/ContextPlaza/OfficialStation_-유라시아 도로_0").GetComponent<MeshCollider>();
                Physics.SyncTransforms();int probes=0;
                for(float u=-90.375f;u<-71;u+=.75f)for(float v=-36.375f;v<-19;v+=.75f)
                {
                    if(!Inside(new Vector2(u,v),polygon))continue;
                    var ray=new Ray(World(u,v,.8f),Vector3.down);
                    bool flush=nativeFloor.Raycast(ray,out var a,1f)&&Mathf.Abs(a.point.y)<.002f;
                    flush|=plazaFloor.Raycast(ray,out var b,1f)&&Mathf.Abs(b.point.y)<.002f;
                    Assert.That(flush,Is.True,"Missing original floor under new restaurant: "+u+","+v);probes++;
                }
                Assert.That(probes,Is.GreaterThan(200));
                var door=UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(d=>d.Element=="1F-unit08-front5");
                Assert.That(Vector3.Distance(door.World(Vector3.zero),World(-71.572425f,-22.037893f,0)),Is.LessThan(.002f));
                Assert.That(door.Width,Is.EqualTo(1.8f));Assert.That(door.Leaves.Length,Is.EqualTo(2));
                Assert.That(door.Leaves[0].Slide.x,Is.EqualTo(-.9f));Assert.That(door.Leaves[1].Slide.x,Is.EqualTo(.9f));
            });
        }
        static void InStation(Action action)
        {
            var saved=EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity");
            try { action(); }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(saved.Any(s=>s.isLoaded)&&saved.Count(s=>s.isActive)==1&&saved.Where(s=>s.isLoaded).All(s=>!string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
        }
        [Test]
        public void SavedSourceLayersPreserveAttributesAndRejectChangedUVOrUnreviewedPositions()
        {
            var entries=Entries();Assert.That(entries.Length,Is.GreaterThanOrEqualTo(48));
            foreach(var entry in entries)
            {
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
                var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(source),Is.EqualTo(entry.source_digest));
                Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(target),Is.EqualTo(entry.replacement_digest));
                Assert.That(FloorGuideLayoutRegistry.ResolveForRebuild(source,entry.source_asset),Is.SameAs(target));
                Assert.That(FloorGuideLayoutRegistry.ResolveForRebuild(target,entry.source_asset),Is.SameAs(target));
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
                Assert.Throws<InvalidDataException>(()=>FloorGuideLayoutRegistry.ResolveForRebuild(mesh,selected.source_asset));
                mesh.uv=AssetDatabase.LoadAssetAtPath<Mesh>(selected.baseline_asset).uv;
                var vertices=mesh.vertices;vertices[0]+=Vector3.right*.01f;mesh.vertices=vertices;
                Assert.Throws<InvalidDataException>(()=>FloorGuideLayoutRegistry.ResolveForRebuild(mesh,selected.source_asset));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [Test]
        public void SavedEightRearShopsUseTheirMovedCollisionAndDoorsWhileAisleHasFloorSupport()
        {
            InStation(()=>
            {
                var entries=Entries();var doors=UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                foreach(int number in Enumerable.Range(9,8))
                {
                    var entry=entries.Single(e=>e.source_asset.EndsWith(WallAsset(number),StringComparison.Ordinal));
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                    var owners=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.sharedMesh==mesh).ToArray();
                    Assert.That(owners.Length,Is.EqualTo(1));Assert.That(owners[0].GetComponent<MeshCollider>().sharedMesh,Is.SameAs(mesh));
                    var door=doors.Single(d=>d.Element=="1F-unit"+number.ToString("D2")+"-front1");
                    Assert.That(door.Usable,Is.True);Assert.That(door.Leaves.Length,Is.GreaterThan(0));
                    foreach(var leaf in door.Leaves)Assert.That(leaf.Pivot.IsChildOf(door.transform),Is.True);
                    var centre=door.transform.TransformPoint(door.Centre);
                    Assert.That(Physics.Raycast(centre+Vector3.up*.4f,Vector3.down,out _,.8f,~0,QueryTriggerInteraction.Ignore),Is.True,"Missing support under moved shop doorway");
                }
                Physics.SyncTransforms();
                var floor=GameObject.Find("1층 · 원본 정합/Ground_Floor").GetComponent<MeshCollider>();
                foreach(float u in new[]{-42f,-35f,-30f,-23f,-15f})
                    Assert.That(floor.Raycast(new Ray(World(u,-25f,.8f),Vector3.down),out _,1.2f),Is.True,"Guide food aisle lost original floor support");
            });
        }
        [Test]
        public void RawShopCollisionReplayKeepsItsExactPartitionAndWithholdsSecondaryCaps()
        {
            const string rawPath="Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/Kit_Colliders.asset";
            var entry=Entries().Single(e=>e.source_asset==rawPath);
            var raw=AssetDatabase.LoadAssetAtPath<Mesh>(rawPath);var baseline=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
            Assert.That(raw.vertexCount,Is.EqualTo(768));Assert.That(raw.triangles.Length/3,Is.EqualTo(384));
            Assert.That(baseline.triangles.Length/3,Is.EqualTo(132));
            Assert.That(FloorGuideLayoutRegistry.ResolveForRebuild(raw,rawPath),Is.SameAs(target));
            Assert.That(target.triangles,Is.EqualTo(baseline.triangles),"Removed duplicate shop walls returned to collision");
            foreach(int mutation in Enumerable.Range(0,5))
            {
                var changed=UnityEngine.Object.Instantiate(raw);
                try
                {
                    if(mutation==0){var values=changed.uv;values[0]+=Vector2.one*.01f;changed.uv=values;}
                    if(mutation==1){var values=changed.vertices;values[0]+=Vector3.up*.01f;changed.vertices=values;}
                    if(mutation==2){var values=changed.normals;values[0]=-values[0];changed.normals=values;}
                    if(mutation==3){var values=changed.tangents;values[0]+=Vector4.one*.01f;changed.tangents=values;}
                    if(mutation==4){var values=changed.triangles;int first=values[0];values[0]=values[1];values[1]=first;changed.triangles=values;}
                    Assert.Throws<InvalidDataException>(()=>FloorGuideLayoutRegistry.ResolveForRebuild(changed,rawPath),"Changed source channel was accepted");
                }
                finally{UnityEngine.Object.DestroyImmediate(changed);}
            }
            InStation(()=>
            {
                var collision=GameObject.Find("실내 트윈 마감/점포 내부 1F/Kit_Colliders").GetComponent<MeshCollider>();Assert.That(collision.sharedMesh,Is.SameAs(target));
                var temporary=new GameObject("실내 트윈 마감"){hideFlags=HideFlags.HideAndDontSave};temporary.SetActive(false);
                try
                {
                    var root=new GameObject("점포 내부 1F");root.transform.SetParent(temporary.transform,false);
                    foreach(string name in new[]{"Kit_CeilingGrid","Kit_Light_조명기구_downlight","Kit_ShopCeiling_Kit_GridWhite"})
                    {
                        var cap=Entries().Single(e=>e.source_asset=="Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/"+name+".asset");
                        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(cap.replacement_asset);
                        var existing=GameObject.Find("실내 트윈 마감/점포 내부 1F/"+name);
                        Assert.That(existing.GetComponentsInChildren<MeshRenderer>(true).All(r=>!r.enabled),Is.True,"Retained duplicate cap is visible in saved scene");
                        var owner=new GameObject(name);owner.transform.SetParent(root.transform,false);owner.AddComponent<MeshFilter>().sharedMesh=mesh;owner.AddComponent<MeshRenderer>();
                        var shadow=new GameObject("ShadowCaster");shadow.transform.SetParent(owner.transform,false);shadow.AddComponent<MeshFilter>().sharedMesh=mesh;shadow.AddComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                    FloorGuideLayoutRegistry.WithholdRetainedShopEnvelopeRenderers(root.transform);
                    Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,Is.EqualTo(6));
                    Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).All(r=>!r.enabled),Is.True,"Rebuild enables a duplicate envelope or its shadow");
                }
                finally{UnityEngine.Object.DestroyImmediate(temporary);}
            });
        }
        [Test]
        public void ArtboxKeepsNativeFrontCornersAndReleasesASupportedUnblockedAisle()
        {
            InStation(()=>
            {
                var entry=Entries().Single(e=>e.source_asset.EndsWith("/Ground_Unit_4916_Walls.asset",StringComparison.Ordinal));
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.baseline_asset);
                var target=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                var owner=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(f=>f.sharedMesh==target);
                var original=source.vertices;var actual=target.vertices;
                foreach(var corner in new[]{new Vector2(-1.439f,-34.939f),new Vector2(-.576f,-34.955f),new Vector2(-.658f,-35.146f),new Vector2(-1.42f,-35.205f),new Vector2(-.919f,-42.228f),new Vector2(6.464f,-41.912f)})
                {
                    var vertices=Enumerable.Range(0,original.Length).Where(i=>Vector2.Distance(new Vector2(original[i].z,original[i].x),corner)<.002f).ToArray();
                    foreach(int i in vertices)Assert.That(actual[i],Is.EqualTo(original[i]),"Fixed native corner moved");
                }
                foreach(var corner in new[]{new Vector2(-1.9620413f,-27.59978f),new Vector2(5.7687864f,-27.59978f),new Vector2(4.6491764f,-27.59978f)})
                    Assert.That(actual.Min(v=>Vector2.Distance(new Vector2(v.z,v.x),corner)),Is.LessThan(.002f),"Guide aisle setback missing");
                Assert.That(owner.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(target));
                Physics.SyncTransforms();var floor=GameObject.Find("1층 · 원본 정합/Ground_Floor").GetComponent<MeshCollider>();
                foreach(float u in new[]{-1f,1f,3f,5f})
                    Assert.That(floor.Raycast(new Ray(World(u,-25f,.8f),Vector3.down),out var hit,1f)&&Mathf.Abs(hit.point.y)<.002f,Is.True,"Released strip lost its flush original floor");
                var direction=World(1,0,0);
                var blockers=Physics.CapsuleCastAll(World(-4,-25,.3f),World(-4,-25,1.4f),.18f,direction,10f,~0,QueryTriggerInteraction.Ignore);
                Assert.That(blockers,Is.Empty,"ARTBOX structure or fixtures still block the released guide aisle: "+string.Join(",",blockers.Select(h=>h.collider.name)));
            });
        }
        [Test]
        public void BakeryAndNeighbourHaveTheSameGuidePartitionEndpointsInTheSavedGeometry()
        {
            InStation(()=>
            {
                var entries=Entries();
                foreach(int number in new[]{15,16})
                {
                    var entry=entries.Single(e=>e.source_asset.EndsWith(WallAsset(number),StringComparison.Ordinal));
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset);
                    var owner=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(f=>f.sharedMesh==mesh);
                    var corners=mesh.vertices.Select(v=>owner.transform.TransformPoint(v)).ToArray();
                    foreach(var endpoint in new[]{World(-6.593f,-6.761f,0),World(-5.62f,-20.708f,0)})
                        Assert.That(corners.Min(v=>Vector2.Distance(new Vector2(v.x,v.z),new Vector2(endpoint.x,endpoint.z))),Is.LessThan(.002f),"Shared partition endpoint no longer joins the neighbour");
                    var centre=World(-6.1065f,-13.7345f,1.5f);
                    var normal=World(1f,0f,0f);
                    Assert.That(owner.GetComponent<MeshCollider>().Raycast(new Ray(centre-normal*.5f,normal),out _,1f),Is.True,"Shared partition lost physical collision");
                }
            });
        }
        [Test]
        public void ShortBOHReturnKeepsOriginalOtherFacesAndNoLongerBlocksTheGuideAisle()
        {
            InStation(()=>
            {
                var joint=Entries().Single(e=>e.source_asset.EndsWith("/1F_public/Kit_Joint.asset",StringComparison.Ordinal));
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(joint.baseline_asset);var target=AssetDatabase.LoadAssetAtPath<Mesh>(joint.replacement_asset);
                var expected=source.triangles.Take(76*3).Concat(source.triangles.Skip(92*3)).ToArray();
                Assert.That(target.triangles,Is.EqualTo(expected));Assert.That(target.vertices,Is.EqualTo(source.vertices));
                var collider=GameObject.Find("실내 트윈 마감/1F 공용/Kit_Colliders").GetComponent<MeshCollider>();
                var ray=new Ray(World(-45f,-14f,1.5f),World(1f,0f,0f));
                Assert.That(collider.Raycast(ray,out _,5f),Is.False,"Old BOH return still crosses the restored corridor");
                var shortRay=new Ray(World(-43.1f,-11f,1.5f),World(0f,1f,0f));
                Assert.That(collider.Raycast(shortRay,out var hit,3f),Is.True,"Short retained BOH return lost collision");
                Assert.That(hit.distance,Is.InRange(1.45f,1.75f));
            });
        }
    }
}
