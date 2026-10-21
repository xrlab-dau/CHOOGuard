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
    public sealed class LockerApproachPlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator WithheldBankCannotRemainAHiddenColliderAndTenantDoorKeepsItsPhysicalCrossing()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var bank=fixture.Box("WithheldFixtureBank",new Vector3(0,1,0),new Vector3(.6f,2,2));
                var route=fixture.Route(new Vector3(-1.2f,0,0),Vector3.zero,new Vector3(1.2f,0,0));
                yield return fixture.Walk(route,false,false);
                bank.enabled=false;Physics.SyncTransforms();
                yield return fixture.Walk(route,false,true);yield return fixture.Walk(route,true,true);
                var door=fixture.Door(StationDoor.DoorKind.Sliding,StationDoor.DoorUse.Tenant,true);
                yield return fixture.DoorRoundtrip(door,true);
            }
        }

    }
}
#endif
