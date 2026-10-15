#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class FloorGuideLayoutPlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("FloorGuideLayoutScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator ElevenMovedShopDoorsMoveTheirRealLeavesAndKeepTheNewOpeningPhysicallyClear()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);
            yield return null;Time.captureFramerate=20;
            var all=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None);
            var doors=Enumerable.Range(9,8).Select(n=>all.Single(d=>d.Element=="1F-unit"+n.ToString("D2")+"-front1"))
                .Concat(new[]{"1F-unit04-front6","1F-unit04-front8","1F-unit08-front5"}.Select(id=>all.Single(d=>d.Element==id))).ToArray();
            foreach(var door in doors){Assert.That(door.Kind,Is.EqualTo(StationDoor.DoorKind.Sliding));door.SetMode(StationDoor.OperatorMode.Locked);}
            yield return new WaitForSeconds(2.3f);
            var leaves=doors.SelectMany(d=>d.Leaves).ToArray();var closed=leaves.Select(l=>l.Pivot.localPosition).ToArray();
            foreach(var door in doors)door.SetMode(StationDoor.OperatorMode.HoldOpen);
            yield return new WaitForSeconds(1.6f);Physics.SyncTransforms();
            foreach(var door in doors)
            {
                Assert.That(door.Open,Is.GreaterThan(.99f));
                var direction=(door.World(new Vector3(0,0,1))-door.World(Vector3.zero)).normalized;
                var hits=Physics.CapsuleCastAll(door.World(new Vector3(0,.3f,-.7f)),door.World(new Vector3(0,1.4f,-.7f)),.18f,direction,1.4f,~0,QueryTriggerInteraction.Ignore);
                Assert.That(hits,Is.Empty,"Moved door opening still contains a static blocker: "+door.Element+" "+string.Join(",",hits.Select(h=>h.collider.name)));
            }
            for(int i=0;i<leaves.Length;i++)Assert.That(Vector3.Distance(leaves[i].Pivot.localPosition,closed[i]+leaves[i].ClosedRotation*leaves[i].Slide),Is.LessThan(.002f));
            foreach(var door in doors)door.SetMode(StationDoor.OperatorMode.Locked);
            yield return new WaitForSeconds(2.3f);
            for(int i=0;i<leaves.Length;i++)Assert.That(Vector3.Distance(leaves[i].Pivot.localPosition,closed[i]),Is.LessThan(.002f));
        }
    }
}
#endif
