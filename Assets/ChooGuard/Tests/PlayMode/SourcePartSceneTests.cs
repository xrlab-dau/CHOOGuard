#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
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
    public sealed class SourcePartSceneTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator StoppingCurrentTreadRunsRestoresEveryApprovedSourceMeshAndPreservesItsChannelsAndIdentity()
        {
            using(new ProviderAwakeDeferral(Type.GetType(RetainedEmergencySessionFixture.ProviderTypeName,true)))
            {
                yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(ChooGuard.App.Fps.Shell.SceneFlow.StationScene,UnityEngine.SceneManagement.LoadSceneMode.Single);
                yield return null;
            }
            var runs=UnityEngine.Object.FindObjectsByType<ChooGuard.Circulation.PlatformEscalatorRun>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var states=runs.Select(run=>run.enabled).ToArray();
            try
            {
                foreach(var run in runs)run.enabled=false;
                SourceGeometryContractFixtures.CurrentSourceParts();
            }
            finally{for(int index=0;index<runs.Length;index++)runs[index].enabled=states[index];}
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SourcePreservingHingedDriverActuallyMovesItsColliderBackedLeafAndReturnsClosed()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var door=fixture.Door(StationDoor.DoorKind.Swing,StationDoor.DoorUse.Staff,true);
                yield return fixture.DoorRoundtrip(door,true);
            }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var station=UnityEngine.SceneManagement.SceneManager.GetSceneByName(ChooGuard.App.Fps.Shell.SceneFlow.StationScene);
            if(station.isLoaded)
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(UnityEngine.SceneManagement.SceneManager.CreateScene("CurrentSourceContractScratch"));
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(station);
            }
        }
    }
}
#endif
