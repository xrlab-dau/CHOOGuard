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
    public sealed class Main2fSourceCompletionPlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator SourceCompletionAcceptsOnlyOwnedExposedSupportAndNeverCompletesThroughAnUnreviewedPatch()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var route=fixture.Route(new Vector3(-1.2f,0,0),Vector3.zero,new Vector3(1.2f,0,0));
                yield return fixture.Walk(route,false,true);yield return fixture.Walk(route,true,true);
                fixture.Floor.enabled=false;
                fixture.Box("UnreviewedReplacement",Vector3.down*.1f,new Vector3(10,.2f,10));
                Physics.SyncTransforms();yield return fixture.Walk(route,false,false);
            }
        }

    }
}
#endif
