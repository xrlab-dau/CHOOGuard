#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
namespace ChooGuard.Tests.PlayMode
{
    // Isolated retained driver contracts and/or exact current source witnesses.
    // No historical Scene geometry or owner placement is recreated.
    public sealed class SourcePushPlateLifecycleTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator ExtrudedPlateKeepsMountingVerticesRejectsInvalidInputsAndRestoresEverySourceChannel()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                Time.captureFramerate=120;
                var door=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,true);
                var control=SourceGeometryContractFixtures.Button(fixture,door);var plate=control.Plate;var source=plate.sharedMesh;
                var positions=source.vertices;var normals=source.normals;var tangents=source.tangents;var uv=source.uv;
                Assert.That(control.CanInteract(null,out var reason),Is.True,reason);Assert.That(plate.sharedMesh,Is.Not.SameAs(source));
                int accepted=control.AcceptedPresses;
                foreach(float invalid in new[]{0f,float.NaN,.02f}){control.Travel=invalid;Assert.That(control.TryInteract(null,out _),Is.False);}
                control.Travel=.002f;control.PressDirectionLocal=Vector3.zero;Assert.That(control.TryInteract(null,out _),Is.False);
                control.PressDirectionLocal=Vector3.left;door.SetMode(StationDoor.OperatorMode.Locked);Assert.That(control.TryInteract(null,out _),Is.False);
                Assert.That(control.AcceptedPresses,Is.EqualTo(accepted));door.SetMode(StationDoor.OperatorMode.Auto);
                Assert.That(control.TryInteract(null,out reason),Is.True,reason);yield return null;
                var pressed=plate.sharedMesh.vertices;int moving=0;
                for(int index=0;index<positions.Length;index++)
                {
                    if(positions[index].x>source.bounds.max.x*.8f){Assert.That(plate.transform.TransformVector(pressed[index]-positions[index]).magnitude,Is.InRange(.0017f,.00201f));Assert.That(pressed[index].x,Is.LessThan(positions[index].x));moving++;}
                    else Assert.That(pressed[index],Is.EqualTo(positions[index]));
                }
                Assert.That(moving,Is.EqualTo(12));for(int frame=0;frame<30;frame++)yield return null;
                Assert.That(plate.sharedMesh.vertices,Is.EqualTo(positions));Assert.That(plate.sharedMesh.normals,Is.EqualTo(normals));
                Assert.That(plate.sharedMesh.tangents,Is.EqualTo(tangents));Assert.That(plate.sharedMesh.uv,Is.EqualTo(uv));
                UnityEngine.Object.Destroy(control);yield return null;Assert.That(plate.sharedMesh,Is.SameAs(source));
            }
        }
    }
}
#endif
