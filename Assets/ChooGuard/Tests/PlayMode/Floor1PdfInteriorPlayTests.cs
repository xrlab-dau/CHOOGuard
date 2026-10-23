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
    public sealed class Floor1PdfInteriorPlayTestsDraft
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator HingedSourceRoomDriverKeepsColliderBackedLeavesAndSupportsBothCrossingDirections()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var door=fixture.Door(StationDoor.DoorKind.Swing,StationDoor.DoorUse.Staff,true);
                yield return fixture.DoorRoundtrip(door,true);
                door.Sealed="fixture:source room withheld";
                Assert.That(door.TryInteract(fixture.Staff,out _),Is.False);
                Assert.That(door.PassableByPeople,Is.False);
            }
        }

    }
}
#endif
