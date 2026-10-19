#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // This fixture deliberately does NOT inherit the retained-session selector.
    // Its one integration test executes the actual default provider preparation.
    public sealed class CurrentProviderNativeBindingPlayTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        ProviderAwakeDeferral deferral;
        static object Property(object target,string name)=>target.GetType().GetProperty(name,Members).GetValue(target);
        static object Field(object target,string name)=>target.GetType().GetField(name,Members).GetValue(target);

        static bool IndexedSurface(int vertices,params uint[] indexCounts)
        {
            var adapterType=Type.GetType("ChooGuard.Seeded.Unity.StationWorldAdapter, ChooGuard.Seeded",true);
            var method=adapterType.GetMethod("HasIndexedSourceGeometry",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null);
            try{return (bool)method.Invoke(null,new object[]{vertices,indexCounts});}
            catch(TargetInvocationException error) when(error.InnerException!=null)
            {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        [Test]
        public void NavigationSource_EmptyOrUnindexedBuffersCannotInventCollisionSurfaces()
        {
            Assert.That(IndexedSurface(0),Is.False);
            Assert.That(IndexedSurface(0,0),Is.False);
            Assert.That(IndexedSurface(0,0,0),Is.False);
            Assert.That(IndexedSurface(3,0),Is.False,"Unreferenced vertices are not indexed source faces");
        }
        [Test]
        public void NavigationSource_EmptySubmeshDoesNotDiscardAnotherIndexedSubmesh()
        {
            Assert.That(IndexedSurface(3,3),Is.True);
            Assert.That(IndexedSurface(3,0,3,0),Is.True);
            Assert.That(IndexedSurface(8,0,36),Is.True);
        }
        [Test]
        public void NavigationSource_InconsistentIndexedVertexMetadataRemainsAnExplicitFailure()
        {
            Assert.Throws<InvalidOperationException>(()=>IndexedSurface(0,3));
            Assert.Throws<ArgumentOutOfRangeException>(()=>IndexedSurface(-1,0));
            Assert.Throws<ArgumentNullException>(()=>IndexedSurface(0,null));
        }

        [UnityTest,Timeout(600000)]
        public IEnumerator ActualCurrentProvider_PreparesEveryNativeObjectIncludingInactivePrefabAndAlternativeLod()
        {
            var providerType=Type.GetType(RetainedEmergencySessionFixture.ProviderTypeName,true);
            Component provider=null;object adapter=null;Scene scene=default;
            GameObject[] anchors=null;string[] anchorIds=null;
            var native=new Dictionary<GameObject,(bool self,bool hierarchy)>();
            using(deferral=new ProviderAwakeDeferral(providerType))
            {
                yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);
                scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
                Assert.That(scene.isLoaded,Is.True);
                var providerRoots=scene.GetRootGameObjects().Where(root=>root.GetComponent(providerType)!=null).ToArray();
                Assert.That(providerRoots.Length,Is.EqualTo(1));
                Assert.That(providerRoots[0].name,Is.EqualTo(RetainedEmergencySessionFixture.ProviderRootName));
                Assert.That(providerRoots[0].activeInHierarchy,Is.True,"The current-provider fixture must keep the default provider active.");
                provider=providerRoots[0].GetComponent(providerType);
                Assert.That((bool)Property(provider,"IsPrepared"),Is.False,"Capture native objects before provider-created supports or agents.");
                adapter=Field(provider,"WorldSnapshot");Assert.That(adapter,Is.Not.Null);
                anchors=(GameObject[])Field(adapter,"LiveObjects");anchorIds=(string[])Field(adapter,"LiveObjectIds");
                Assert.That(anchors.Length,Is.EqualTo(2));Assert.That(anchorIds.Length,Is.EqualTo(2));
                CollectionAssert.AreEquivalent(new[]{"FPSWorld","ProbeVolumePerSceneData"},anchors.Select(root=>root.name).ToArray());
                Assert.That(anchors.Distinct().Count(),Is.EqualTo(2));Assert.That(anchorIds.Distinct(StringComparer.Ordinal).Count(),Is.EqualTo(2));
                foreach(var anchor in anchors)
                {
                    Assert.That(anchor.scene,Is.EqualTo(scene));Assert.That(anchor.transform.parent,Is.Null);
                    foreach(var transform in anchor.GetComponentsInChildren<Transform>(true))
                        native.Add(transform.gameObject,(transform.gameObject.activeSelf,transform.gameObject.activeInHierarchy));
                }
            }
            deferral=null;
            Assert.That(native.Count,Is.GreaterThan(0));Assert.That(native.Values.Any(flags=>!flags.hierarchy),Is.True,"Inactive native objects must remain in the source census.");
            var seed=providerType.GetField("InitialSeed",Members);
            if(string.IsNullOrEmpty((string)seed.GetValue(provider)))seed.SetValue(provider,"20261020");
            providerType.GetMethod("Prepare",Members).Invoke(provider,null);
            Assert.That(provider.gameObject.activeInHierarchy,Is.True);
            Assert.That((bool)Property(provider,"IsPrepared"),Is.True);
            Assert.That((bool)Property(adapter,"Prepared"),Is.True);
            Assert.That((string)Property(adapter,"NavigationHash"),Is.Not.EqualTo("absent"),"The real preparation must execute its actual source/nav guards.");

            var registry=Property(adapter,"Registry");Assert.That(registry,Is.Not.Null);
            var records=(IDictionary)Property(registry,"Objects");
            var identities=((IEnumerable)Property(adapter,"SourceIdentities")).Cast<Component>().ToArray();
            Assert.That(records.Count,Is.EqualTo(native.Count));Assert.That(identities.Length,Is.EqualTo(native.Count));
            Assert.That(identities.Select(identity=>identity.gameObject).Distinct().Count(),Is.EqualTo(native.Count));
            Assert.That(new HashSet<GameObject>(identities.Select(identity=>identity.gameObject)).SetEquals(native.Keys),Is.True,"Every captured native GameObject must have exactly one source identity, including inactive prefab descendants.");
            var identityType=identities[0].GetType();
            var idField=identityType.GetField("ObjectId",Members);var selfField=identityType.GetField("SourceActiveSelf",Members);var hierarchyField=identityType.GetField("SourceActiveInHierarchy",Members);
            var sourceIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var identity in identities)
            {
                string id=(string)idField.GetValue(identity);
                Assert.That(sourceIds.Add(id),Is.True,"Source identities must be unique.");Assert.That(records.Contains(id),Is.True);
                Assert.That((bool)selfField.GetValue(identity),Is.EqualTo(native[identity.gameObject].self));
                Assert.That((bool)hierarchyField.GetValue(identity),Is.EqualTo(native[identity.gameObject].hierarchy));
            }
            var navigation=(JObject)((JObject)Property(adapter,"Report"))["navigation"];
            var zeroSurface=(JArray)navigation["zero_surface_source_meshes"];
            Assert.That(zeroSurface,Is.Not.Null);
            var expectedZeroSurface=identities.Where(identity=>(bool)hierarchyField.GetValue(identity))
                .Where(identity=>identity.GetComponent<MeshFilter>()?.sharedMesh!=null&&identity.GetComponent<MeshRenderer>()?.enabled==true)
                .Where(identity=>{var mesh=identity.GetComponent<MeshFilter>().sharedMesh;return Enumerable.Range(0,mesh.subMeshCount).All(sub=>mesh.GetIndexCount(sub)==0);}).ToArray();
            CollectionAssert.AreEquivalent(expectedZeroSurface.Select(identity=>(string)idField.GetValue(identity)).ToArray(),zeroSurface.Values<JObject>().Select(row=>(string)row["object_id"]).ToArray());
            var supportParents=new HashSet<GameObject>(((IEnumerable)Field(adapter,"supports")).Cast<GameObject>().Select(support=>support.transform.parent.gameObject));
            foreach(var identity in expectedZeroSurface)
            {
                Assert.That(supportParents.Contains(identity.gameObject),Is.False,"No runtime collision surface may be invented for a source with no indexed primitives");
                Assert.That(identity.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(identityType.GetField("SourceMesh",Members).GetValue(identity)));
                Assert.That(sourceIds.Contains((string)idField.GetValue(identity)),Is.True,"Empty source references remain bound and inventoried");
            }
            Assert.That(sourceIds.Any(id=>id.Contains(":prefab:")),Is.True,"The current full native census must retain resolved prefab-instance identities.");
            Assert.That(records.Contains("unity:7518d664dafa749f2b6d5cba42bb798c:461453"),Is.False,"The explicitly retired WC door must never be rebound or restored.");
            var manifest=JObject.Parse(((TextAsset)Field(adapter,"SnapshotManifest")).text);
            Assert.That(native.Count,Is.EqualTo((int)manifest["currentFloorProvenance"]["fullNativeObjectCount"]));
            Assert.That((string)manifest["liveBindingSchema"],Is.EqualTo("chooguard.live-root-child-indices.v1"));
            CollectionAssert.AreEquivalent(manifest["liveBindingRoots"].Values<string>().ToArray(),anchorIds);
            Assert.That(((GameObject[])Field(adapter,"LiveObjects")).SequenceEqual(anchors),Is.True);
            var alternatives=manifest["renderOnlyAlternativeLodObjectIds"].Values<string>().ToArray();
            Assert.That(alternatives.Length,Is.EqualTo(72));Assert.That(alternatives.Distinct(StringComparer.Ordinal).Count(),Is.EqualTo(72));
            foreach(string id in alternatives)
            {
                Assert.That(sourceIds.Contains(id),Is.True,"Render-only alternative LODs are retained in inventory, even though navigation does not add them twice.");
                var provenance=(JObject)Property(records[id],"GeometryProvenance");Assert.That((bool?)provenance["renderOnlyAlternativeLod"],Is.True);
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadCurrentProviderTestScene()
        {
            deferral?.Dispose();deferral=null;
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(scene.isLoaded)
            {SceneManager.SetActiveScene(SceneManager.CreateScene("CurrentProviderNativeBindingScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
#endif
