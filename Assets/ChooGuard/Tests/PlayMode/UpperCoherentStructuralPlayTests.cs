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
    public sealed class UpperCoherentStructuralPlayTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator StockResponderRoundtripPreservesActualControllerSettingsAndContinuousGroundedMotion()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                yield return fixture.ResponderRoundtrip();
            }
        }

    }
}
#endif
