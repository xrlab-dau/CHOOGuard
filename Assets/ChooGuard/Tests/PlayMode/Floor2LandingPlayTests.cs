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
    public sealed class Floor2LandingPlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator LandingFootprintRequiresExposedApprovedSupportForTheRealProductionWalker()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var route=fixture.Route(new Vector3(-1.2f,0,0),Vector3.zero,new Vector3(1.2f,0,0));
                yield return fixture.Walk(route,false,true);yield return fixture.Walk(route,true,true);
                fixture.Floor.enabled=false;Physics.SyncTransforms();
                yield return fixture.Walk(route,false,false);
            }
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator LandingRailingDetourWalksItsCurveAndRejectsTheBlockedShortcut()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                fixture.Box("OriginalFixtureRail",new Vector3(0,1,0),new Vector3(.5f,2,.5f));
                var route=fixture.Route(new Vector3(-1.5f,0,0),new Vector3(-.8f,0,-1),new Vector3(.8f,0,-1),new Vector3(1.5f,0,0));
                yield return fixture.Walk(route,false,true);yield return fixture.Walk(route,true,true);
                var blocked=fixture.Route(new Vector3(-1.5f,0,0),Vector3.zero,new Vector3(1.5f,0,0));
                yield return fixture.Walk(blocked,false,false);
            }
        }

    }
}
#endif
