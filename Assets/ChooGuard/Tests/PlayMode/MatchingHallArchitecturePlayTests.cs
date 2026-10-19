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
    public sealed class MatchingHallArchitecturePlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator HallSourcePassagePreservesCurvedWalkingInBothDirectionsAtWalkingAndResponderSpeeds()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                fixture.Box("RetainedFixtureBoundary",new Vector3(0,1,0),new Vector3(.5f,2,.5f));
                var route=fixture.Route(new Vector3(-1.5f,0,0),new Vector3(-.8f,0,-1),new Vector3(.8f,0,-1),new Vector3(1.5f,0,0));
                foreach(float speed in new[]{1.3f,2.5f})foreach(bool reverse in new[]{false,true})yield return fixture.Walk(route,reverse,true,speed);
            }
        }

    }
}
#endif
