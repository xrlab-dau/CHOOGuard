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
    public sealed class ReviewedNativeAutomaticDoorTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator NamedButtonMovesOnlyItsReconciledDoorAndPreservedNativeLeafChannelsReturnExactly()
        {
            var pins=(JArray)SourceGeometryContractFixtures.Read("native-source-driver-meshes")["leaves"];
            Assert.That(pins.Count,Is.GreaterThan(0));
            foreach(var pin in pins)
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var target=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,false);
                var other=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,false);other.transform.localPosition=Vector3.right*4;
                var source=SourceGeometryContractFixtures.Mesh(pin);var raw=new GameObject("PinnedNativeLeafMesh");raw.transform.SetParent(target.Leaves[0].Pivot,false);
                raw.AddComponent<MeshFilter>().sharedMesh=source;raw.AddComponent<MeshRenderer>();raw.AddComponent<MeshCollider>().sharedMesh=source;
                var vertices=source.vertices;var normals=source.normals;var tangents=source.tangents;var uv=source.uv;
                var before=vertices.Select(raw.transform.TransformPoint).ToArray();var otherClosed=other.Leaves[0].Pivot.localPosition;
                var control=SourceGeometryContractFixtures.Button(fixture,target);
                Assert.That(control.TryInteract(null,out _),Is.True);Assert.That(control.AcceptedPresses,Is.EqualTo(1));
                yield return new WaitForSeconds(1.5f);Assert.That(target.Open,Is.GreaterThan(.99f));
                Assert.That(vertices.Select((vertex,index)=>Vector3.Distance(raw.transform.TransformPoint(vertex),before[index])).Min(),Is.GreaterThan(.9f));
                Assert.That(other.Leaves[0].Pivot.localPosition,Is.EqualTo(otherClosed));
                target.SetMode(StationDoor.OperatorMode.Locked);yield return new WaitForSeconds(2.8f);
                Assert.That(target.Open,Is.LessThan(.01f));Assert.That(vertices.Select((vertex,index)=>Vector3.Distance(raw.transform.TransformPoint(vertex),before[index])).Max(),Is.LessThan(.001f));
                Assert.That(source.vertices,Is.EqualTo(vertices));Assert.That(source.normals,Is.EqualTo(normals));Assert.That(source.tangents,Is.EqualTo(tangents));Assert.That(source.uv,Is.EqualTo(uv));
            }
        }
    }
}
#endif
