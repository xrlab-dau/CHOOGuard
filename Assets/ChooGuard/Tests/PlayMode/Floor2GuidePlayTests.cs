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
    public sealed class Floor2GuidePlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator GuideSlidingOperatorRejectsLockedAndSealedRequestsAndAcceptsOwnedButton()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var door=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,true);
                yield return null;door.SetMode(StationDoor.OperatorMode.Locked);
                Assert.That(door.RequestButtonOpen(out _),Is.False);
                door.SetMode(StationDoor.OperatorMode.Auto);door.Sealed="fixture:unverified opening";
                Assert.That(door.RequestButtonOpen(out _),Is.False);door.Sealed="";
                Assert.That(door.RequestButtonOpen(out _),Is.True);
                yield return new WaitForSeconds(1.5f);Assert.That(door.Open,Is.GreaterThan(.99f));
            }
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator PairedSlidingGuideLeavesMoveAndPermitGroundedBidirectionalCrossingThenCloseExactly()
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
