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
    public sealed class Floor1Unit6VectorPlayTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SourceVectorSlidingDriverKeepsOriginalLeavesAndAllowsBidirectionalPhysicalCrossing()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var door=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,true);
                yield return fixture.DoorRoundtrip(door,true);
            }
        }
    }
}
#endif
