#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    public sealed class SourcePartSceneTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator PreservedSourceDoorIsActuallyMovedByTheExistingHandInteractionAndRemainsColliderBacked()
        {
            int capture=Time.captureFramerate;
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);
            yield return null;
            var sources=UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsSortMode.None);
            Assert.That(sources.Count(x=>x.Category!="escalator_tread_component"&&x.Category!="automatic_door_leaf_source_part"&&x.Category!="fixed_stair_architecture"&&x.Category!="partial_counterleaf_fragment_source_part"),Is.EqualTo(405));
            Assert.That(sources.Count(x=>x.Category=="escalator_tread_component"),Is.EqualTo(615));
            Assert.That(sources.Count(x=>x.Category=="automatic_door_leaf_source_part"),Is.EqualTo(9));
            Assert.That(sources.All(x=>x.LegacyGeometrySeparated&&!x.SiteAccuracyVerified),Is.True);
            var doors=sources.Where(x=>x.OperationStatus=="hinged_source_leaf_bound").Select(x=>x.GetComponentInParent<StationDoor>()).ToArray();
            Assert.That(doors.Length,Is.EqualTo(11));
            Assert.That(doors.All(x=>x.Leaves.Length==1&&x.Leaves[0].Pivot!=null&&x.Leaves[0].Pivot.GetComponent<Rigidbody>().isKinematic),Is.True);
            var door=doors.OrderBy(x=>x.World(Vector3.zero).x).First();
            var leaf=door.Leaves[0].Pivot;
            var mesh=leaf.GetComponentInChildren<MeshFilter>();
            Assert.That(mesh,Is.Not.Null);
            Assert.That(mesh.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(mesh.sharedMesh));
            Assert.That(mesh.sharedMesh.uv.Length,Is.EqualTo(mesh.sharedMesh.vertexCount));
            var vertices=mesh.sharedMesh.vertices;
            var before=vertices.Select(mesh.transform.TransformPoint).ToArray();
            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            Assert.That(responder,Is.Not.Null);
            responder.SetExternalInputMode(true);
            responder.RestorePhysicalPose(door.World(new Vector3(0,0,1.8f)),0,0);
            responder.Resume(false);
            Assert.That(door.TryInteract(responder,out _),Is.True);
            Time.captureFramerate=30;
            float until=Time.time+1.4f;
            while(Time.time<until&&door.Open<.85f)yield return null;
            Assert.That(door.Open,Is.GreaterThan(.8f));
            Assert.That(vertices.Select((p,i)=>Vector3.Distance(mesh.transform.TransformPoint(p),before[i])).Max(),Is.GreaterThan(.3f),"The original imported geometry must visibly rotate, rather than changing a text state alone.");
            Assert.That(mesh.GetComponent<MeshCollider>().enabled,Is.True);
            Assert.That(Quaternion.Angle(leaf.localRotation,door.Leaves[0].ClosedRotation),Is.GreaterThan(70));
            responder.Pause();Time.captureFramerate=capture;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;
            var station=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(station.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("SourcePartTestScratch"));yield return SceneManager.UnloadSceneAsync(station);}
        }
    }
}
#endif
