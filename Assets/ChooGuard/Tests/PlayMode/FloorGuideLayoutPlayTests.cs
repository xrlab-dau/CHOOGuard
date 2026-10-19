#if UNITY_INCLUDE_TESTS
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
    public sealed class FloorGuideLayoutPlayTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator GuideOpeningUsesTheRealSlidingDriverAndNeverRetainsAHiddenStaticBlocker()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var door=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,true);
                yield return fixture.DoorRoundtrip(door,true);
                door.SetMode(StationDoor.OperatorMode.HoldOpen);yield return new WaitForSeconds(1.5f);
                fixture.Box("UnreleasedFixtureOpeningBlocker",new Vector3(0,1,0),new Vector3(3,2,.2f));
                var route=fixture.Route(new Vector3(0,0,-.7f),Vector3.zero,new Vector3(0,0,.7f));
                yield return fixture.Walk(route,false,false);
            }
        }
    }
}
#endif
