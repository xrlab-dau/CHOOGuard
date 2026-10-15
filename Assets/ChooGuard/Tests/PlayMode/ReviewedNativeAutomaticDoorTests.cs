#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using ChooGuard.App.Fps.Shell;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>Staged integration test to run after root exact-face reconciliation and reviewed bindings.</summary>
    public sealed class ReviewedNativeAutomaticDoorTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("NativeDoorTestScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
        static readonly string[] MainButtons={"f0efc5e621349b0847729fb9","9783ce238277b47411a9f63a","8d6345ca613539080b115218","de9e2b4e5f7a2cb6ab995d09","325cc2b0f1dba500aab93dfc"};
        static readonly string[] SwingLeaves={"5089b725dad5e3c970223f5a","a1f9d8b72e353344d4136264","bf9da804a36b4cc7dfda9b91"};
        [UnityTest,Timeout(300000)]
        public IEnumerator NamedNativeButtonsRequestTheirOwnGlassLeavesAndPreserveEveryOriginalComponent()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;Time.captureFramerate=20;
            var identities=Object.FindObjectsByType<SourcePartIdentity>(FindObjectsSortMode.None);
            Assert.That(identities.Count(x=>x.Category=="automatic_door_leaf_source_part"),Is.EqualTo(9));
            Assert.That(identities.Count(x=>x.Category!="escalator_tread_component"&&x.Category!="automatic_door_leaf_source_part"&&x.Category!="fixed_stair_architecture"&&x.Category!="partial_counterleaf_fragment_source_part"),Is.EqualTo(405));
            var fragments=identities.Where(x=>x.Category=="partial_counterleaf_fragment_source_part").ToArray();
            CollectionAssert.AreEquivalent(new[]{"501b0b5be16448a0d5c95d78","5d4f4242d54a39380767bf43","ab427631ad34251695dc2ee6","2db06c0adff1128fd9c0c5fb","8f09a2b4ce0d2f2a58e31347","d1b879fc4e65e56b6c367995"},fragments.Select(x=>x.SourceId));
            Assert.That(fragments.Sum(x=>x.PreservedTriangles),Is.EqualTo(24));
            Assert.That(fragments.All(x=>!x.LegacyGeometrySeparated&&x.OperationStatus=="partial_counterleaf_"+x.PreservedTriangles+"_of_1054_source_faces_restored_operation_unbound"&&x.GetComponentInParent<StationDoor>()==null),Is.True);
            var fixedStair=identities.Single(x=>x.Category=="fixed_stair_architecture");
            Assert.That(fixedStair.SourceId,Is.EqualTo("93f4f5ec5996ef5390adf44a"));
            Assert.That(fixedStair.PreservedTriangles,Is.EqualTo(1306));
            Assert.That(identities.Count(x=>x.Category=="escalator_tread_component"),Is.EqualTo(615));
            foreach(var id in SwingLeaves)Assert.That(identities.Single(x=>x.SourceId==id).OperationStatus,Is.EqualTo("source_panel_existing_controller_leaf_mapping_unconfirmed"),"Authored-open swing leaf must not be silently reassigned to a sliding controller");
            var controls=identities.Where(x=>MainButtons.Contains(x.SourceId)).Select(x=>x.GetComponent<SourcePartAutomaticButton>()).ToArray();
            Assert.That(controls.Length,Is.EqualTo(5));Assert.That(controls.All(x=>x!=null&&x.Door!=null),Is.True);
            Assert.That(controls.Select(x=>x.Door.Element).Distinct().Count(),Is.EqualTo(5));
            foreach(var control in controls)
            {
                var door=control.Door;Assert.That(door.Kind,Is.EqualTo(StationDoor.DoorKind.Sliding));Assert.That(door.Usable,Is.True);Assert.That(door.Leaves.Length,Is.EqualTo(control.GetComponent<SourcePartIdentity>().SourceId=="325cc2b0f1dba500aab93dfc"?1:2));
                Assert.That(door.Leaves.All(x=>x.Pivot!=null&&x.Pivot.GetComponent<Rigidbody>().isKinematic),Is.True);
                var filters=door.Leaves.SelectMany(x=>x.Pivot.GetComponentsInChildren<MeshFilter>()).Where(x=>x.GetComponentInParent<SourcePartIdentity>().Category=="automatic_door_leaf_source_part").ToArray();
                Assert.That(filters.Length,Is.EqualTo(door.Leaves.Length));Assert.That(filters.All(x=>x.GetComponent<MeshCollider>().sharedMesh==x.sharedMesh),Is.True);
                var otherDoors=controls.Where(x=>x!=control).Select(x=>x.Door).ToArray();var otherPositions=otherDoors.SelectMany(x=>x.Leaves.Select(l=>l.Pivot.position)).ToArray();
                var before=filters.Select(x=>x.sharedMesh.vertices.Select(x.transform.TransformPoint).ToArray()).ToArray();int accepted=control.AcceptedPresses;
                Assert.That(control.TryInteract(null,out _),Is.True);Assert.That(control.AcceptedPresses,Is.EqualTo(accepted+1));
                float until=Time.time+2f;while(Time.time<until&&door.Open<.85f)yield return null;
                Assert.That(door.Open,Is.GreaterThan(.8f));
                for(int i=0;i<filters.Length;i++)Assert.That(filters[i].sharedMesh.vertices.Select((v,k)=>Vector3.Distance(filters[i].transform.TransformPoint(v),before[i][k])).Min(),Is.GreaterThan(1f),"Every real original leaf vertex must translate, including collision-backed glass");
                var afterOthers=otherDoors.SelectMany(x=>x.Leaves.Select(l=>l.Pivot.position)).ToArray();for(int i=0;i<afterOthers.Length;i++)Assert.That(Vector3.Distance(afterOthers[i],otherPositions[i]),Is.LessThan(.001f),"A button must not move a different source opening");
                float closeUntil=Time.time+8f;while(Time.time<closeUntil&&door.Open>.01f)yield return null;
                Assert.That(door.Open,Is.LessThan(.02f));
                for(int i=0;i<filters.Length;i++)Assert.That(filters[i].sharedMesh.vertices.Select((v,k)=>Vector3.Distance(filters[i].transform.TransformPoint(v),before[i][k])).Max(),Is.LessThan(.03f),"Door must return to exact original closed frame");
            }
        }
    }
}
#endif
