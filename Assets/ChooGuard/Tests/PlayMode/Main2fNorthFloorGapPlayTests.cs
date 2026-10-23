#if UNITY_INCLUDE_TESTS
using System.Collections;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Facilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // Historical installed-owner acceptance is superseded by the user-approved
    // whole-Scene reconstruction. Exercise the retained production algorithm in
    // isolated fixture geometry; never restore an old object or planning packet.
    public sealed class Main2fNorthFloorGapPlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator ApprovedAdjacentFloorSeamsSupportBothDirectionsAndMissingPatchCannotPass()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                fixture.Floor.enabled=false;
                var left=fixture.Box("ApprovedWestSlab",new Vector3(-2.5f,-.1f,0),new Vector3(5,.2f,10));
                var right=fixture.Box("ApprovedEastSlab",new Vector3(2.5f,-.1f,0),new Vector3(5,.2f,10));
                fixture.ApprovedFloors.Clear();fixture.ApprovedFloors.Add(left);fixture.ApprovedFloors.Add(right);
                var route=fixture.Route(new Vector3(-1.2f,0,0),Vector3.zero,new Vector3(1.2f,0,0));
                yield return fixture.Walk(route,false,true);yield return fixture.Walk(route,true,true);
                right.enabled=false;Physics.SyncTransforms();yield return fixture.Walk(route,false,false);
            }
        }

    }
}
#endif
