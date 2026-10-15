#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps.Shell;
using ChooGuard.App.Fps.Twin;
using ChooGuard.App.Fps.Facilities;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class SourcePushPlateLifecycleTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator ActualFivePlatesKeepMountingVerticesAndRestoreTheirSourceMeshAndChannels()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;
            Time.captureFramerate=120;
            var controls=Object.FindObjectsByType<SourcePartAutomaticButton>(FindObjectsSortMode.None);
            Assert.That(controls.Length,Is.EqualTo(5));
            foreach(var control in controls)
            {
                control.enabled=false;var plate=control.Plate;var source=plate.sharedMesh;
                var positions=source.vertices;var normals=source.normals;var tangents=source.tangents;
                control.enabled=true;
                // Active component configuration/re-enable must initialize lazily from the original source.
                Assert.That(control.CanInteract(null,out var reason),Is.True,reason);
                Assert.That(plate.sharedMesh,Is.Not.SameAs(source));
                int accepted=control.AcceptedPresses;
                foreach(float invalid in new[]{0f,float.NaN,.02f})
                {
                    control.Travel=invalid;Assert.That(control.TryInteract(null,out _),Is.False);
                }
                control.Travel=.002f;control.PressDirectionLocal=Vector3.zero;
                Assert.That(control.TryInteract(null,out _),Is.False);
                control.PressDirectionLocal=Vector3.left;control.Door.SetMode(StationDoor.OperatorMode.Locked);
                Assert.That(control.TryInteract(null,out _),Is.False);
                Assert.That(control.AcceptedPresses,Is.EqualTo(accepted));
                control.Door.SetMode(StationDoor.OperatorMode.Auto);
                Assert.That(control.TryInteract(null,out reason),Is.True,reason);yield return null;
                var pressed=plate.sharedMesh.vertices;int moving=0;
                for(int i=0;i<positions.Length;i++)
                {
                    float worldMove=plate.transform.TransformVector(pressed[i]-positions[i]).magnitude;
                    if(positions[i].x>source.bounds.max.x*.8f)
                    {
                        Assert.That(worldMove,Is.InRange(.0017f,.00201f));Assert.That(pressed[i].x,Is.LessThan(positions[i].x));moving++;
                    }
                    else Assert.That(pressed[i],Is.EqualTo(positions[i]),"Original mounting/back vertices must remain fixed in the moving leaf's space.");
                }
                Assert.That(moving,Is.EqualTo(12));
                for(int frame=0;frame<30;frame++)yield return null;
                CollectionAssert.AreEqual(positions,plate.sharedMesh.vertices);
                CollectionAssert.AreEqual(normals,plate.sharedMesh.normals);
                CollectionAssert.AreEqual(tangents,plate.sharedMesh.tangents);
                Object.Destroy(control);yield return null;
                Assert.That(plate,Is.Not.Null);Assert.That(plate.sharedMesh,Is.SameAs(source),"Removing a controller must preserve the surviving original plate renderer.");
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("SourcePlateTestScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
#endif
