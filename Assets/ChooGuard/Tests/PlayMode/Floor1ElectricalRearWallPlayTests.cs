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
    public sealed class Floor1ElectricalRearWallPlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator StaffRearPortalDriverMovesOriginalSingleAndPairedLeavesAndReturnsClosed()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var single=fixture.Door(StationDoor.DoorKind.Swing,StationDoor.DoorUse.Staff,false);
                single.transform.localPosition=Vector3.right*3;
                yield return fixture.DoorRoundtrip(single,false);
                Object.DestroyImmediate(single.gameObject);
                var paired=fixture.Door(StationDoor.DoorKind.Swing,StationDoor.DoorUse.Staff,true);
                yield return fixture.DoorRoundtrip(paired,true);
            }
        }

    }
}
#endif
