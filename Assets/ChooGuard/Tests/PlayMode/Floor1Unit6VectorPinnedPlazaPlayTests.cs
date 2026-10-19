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
    public sealed class Floor1Unit6VectorPinnedPlazaPlayTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator PinnedOwnedSupportRejectsAPlazaSubstituteWhileSourceVectorDriverKeepsItsLeafReturn()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var route=fixture.Route(new Vector3(-1.2f,0,0),Vector3.zero,new Vector3(1.2f,0,0));
                yield return fixture.Walk(route,false,true);fixture.Floor.enabled=false;
                var substitute=fixture.Box("UnreviewedPlazaSubstitute",Vector3.down*.1f,new Vector3(10,.2f,10));
                yield return fixture.Walk(route,false,false);substitute.enabled=false;fixture.Floor.enabled=true;
                var door=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,true);
                yield return fixture.DoorRoundtrip(door,true);
            }
        }
    }
}
#endif
