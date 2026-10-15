#if UNITY_INCLUDE_TESTS
using ChooGuard.Editor.Twin;
using ChooGuard.App.Fps.Facilities;
using NUnit.Framework;
using UnityEngine;
namespace ChooGuard.Tests.EditMode
{
    public sealed class SourceReconciliationTests
    {
        [Test] public void FaceMatchingIgnoresIndexOrderButRejectsDifferentGeometry()
        {
            var a=new Vector3(10,7,20);var b=a+Vector3.right;var c=a+Vector3.up;
            Assert.That(SourcePartReconciler.SameTriangle(a,b,c,c,a,b,.001f),Is.True);
            Assert.That(SourcePartReconciler.SameTriangle(a,b,c,a,b,c+Vector3.forward*.002f,.001f),Is.False);
            Assert.That(SourcePartReconciler.SameTriangle(a,b,c,a,a,c,.001f),Is.False);
        }
        [Test] public void LockedTenantDoorCannotClaimAnAcceptedFireAlarmButtonRequest()
        {
            var go=new GameObject("tenant operator contract");
            bool previousAlarm=ChooGuard.App.Fps.Facilities.StationSignals.FireAlarm;
            try
            {
                var door=go.AddComponent<StationDoor>();door.Kind=StationDoor.DoorKind.Sliding;door.Use=StationDoor.DoorUse.Tenant;door.SetMode(StationDoor.OperatorMode.Locked);
                ChooGuard.App.Fps.Facilities.StationSignals.FireAlarm=true;
                Assert.That(door.RequestButtonOpen(out _),Is.False);
                door.Use=StationDoor.DoorUse.Public;
                Assert.That(door.RequestButtonOpen(out _),Is.True);
            }
            finally {Object.DestroyImmediate(go);ChooGuard.App.Fps.Facilities.StationSignals.FireAlarm=previousAlarm;}
        }
    }
}
#endif
