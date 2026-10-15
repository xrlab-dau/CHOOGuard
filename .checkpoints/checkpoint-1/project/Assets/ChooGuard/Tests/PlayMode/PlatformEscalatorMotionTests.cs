using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChooGuard.Circulation;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Twin;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class PlatformEscalatorMotionTests
    {
        [UnityTest]
        public IEnumerator NativeTreadsMoveStopAndResumeWithTheSameRideDriveAndRestoreOnDisable()
        {
            var root=new GameObject("Observed tread test run");var driveObject=new GameObject("Exact lane drive");var mesh=new Mesh
            { vertices=new[]{new Vector3(-.6f,.2f,0),new Vector3(.6f,.2f,0),new Vector3(.6f,.2f,.4f),new Vector3(-.6f,.2f,.4f)},
              uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},triangles=new[]{0,2,1,0,3,2} };
            root.SetActive(false);var motion=root.AddComponent<EscalatorMotion>();var run=root.AddComponent<PlatformEscalatorRun>();
            run.Treads=new Transform[3];run.ObservedOrigins=new[]{new Vector3(0,1,0),new Vector3(0,.8f,.35f),new Vector3(0,.6f,.7f)};
            run.Up=false;run.SourcePitch=.4f;run.ExposureAxis=Vector3.forward;run.ExposureMinimum=0;run.ExposureMaximum=1.1f;
            for(int i=0;i<3;i++)
            {
                var node=new GameObject("Original tread "+i);node.transform.SetParent(root.transform,false);node.transform.localPosition=run.ObservedOrigins[i];
                node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>();run.Treads[i]=node.transform;
            }
            var rideObject=new GameObject("Actual source ride axis");rideObject.transform.SetParent(root.transform,false);rideObject.transform.rotation=Quaternion.LookRotation(new Vector3(0,-.2f,.35f));
            rideObject.AddComponent<BoxCollider>().isTrigger=true;var ride=rideObject.AddComponent<EscalatorRide>();ride.Direction=EscalatorRide.RideDirection.Up;
            var drive=driveObject.AddComponent<Escalator>();drive.enabled=false;drive.Setup(new StationPoints.EscalatorEntry { id="esc-well-test",label="Test",from="platform",to="concourse",up=false,path=new[]{new Vector3(0,1,0),new Vector3(0,.6f,.7f)} });
            var binding=root.AddComponent<EscalatorDriveBinding>();binding.lane_id="esc-well-test";
            try
            {
                root.SetActive(true);Assert.That(binding.Bind(new[]{drive}),Is.True);
                Assert.That(motion.Drive,Is.SameAs(drive));Assert.That(ride.Drive,Is.SameAs(drive));
                run.ApplyMotion();var original=run.Treads[1].localPosition;
                drive.AdvanceDrive(.2f);run.ApplyMotion();Assert.That(Vector3.Distance(run.Treads[1].localPosition,original),Is.GreaterThan(.05f));
                var moved=run.Treads[1].localPosition;double stoppedPhase=run.CurrentPhase;drive.Stop();drive.AdvanceDrive(1);run.ApplyMotion();
                Assert.That(run.Treads[1].localPosition,Is.EqualTo(moved));Assert.That(run.CurrentPhase,Is.EqualTo(stoppedPhase));Assert.That(ride.WorldVelocity,Is.EqualTo(Vector3.zero));
                Assert.That(drive.Restart(out var reason),Is.True,reason);drive.AdvanceDrive(.1f);run.ApplyMotion();
                Assert.That(Vector3.Distance(run.Treads[1].localPosition,moved),Is.GreaterThan(.02f));Assert.That(ride.WorldVelocity.magnitude,Is.GreaterThan(.49f));
                Assert.That(run.HasInventedReturnRoute,Is.False);Assert.That(motion.Loops.Length,Is.Zero);
                run.enabled=false;
                for(int i=0;i<3;i++){Assert.That(run.Treads[i].localPosition,Is.EqualTo(run.ObservedOrigins[i]));Assert.That(run.Treads[i].GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));}
                run.enabled=true;run.ApplyDistance(.1);
                Assert.That(System.Linq.Enumerable.Count(root.GetComponentsInChildren<Renderer>(),x=>x.name=="진입 디딤판 표면"),Is.EqualTo(1),"The deferred old entry surface must be hidden before a same-frame re-enable");
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(driveObject);UnityEngine.Object.Destroy(mesh); }
        }
    }
}

namespace ChooGuard.Tests.PlayMode
{
    public sealed class PlatformEscalatorSceneTests
    {
        private GameObject testDrives;
        [UnityTest,Timeout(180000)]
        public IEnumerator EveryInstalledSourceRunBindsToItsExactCatalogLaneAndOriginalNativeVerticesActuallyMove()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(ChooGuard.App.Fps.Shell.SceneFlow.StationScene,UnityEngine.SceneManagement.LoadSceneMode.Single);
            yield return null;
            var bindings=UnityEngine.Object.FindObjectsByType<EscalatorDriveBinding>(FindObjectsSortMode.None);
            Assert.That(bindings.Length,Is.EqualTo(21),"15 source platform lanes plus the six preserved existing lanes");
            var runs=UnityEngine.Object.FindObjectsByType<PlatformEscalatorRun>(FindObjectsSortMode.None);
            Assert.That(runs.Length,Is.EqualTo(15));
            var source=UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsSortMode.None);
            Assert.That(System.Linq.Enumerable.Count(source,x=>x.Category=="escalator_tread_component"),Is.EqualTo(615));
            var text=new TextAsset(System.IO.File.ReadAllText("Assets/ChooGuard/Art/Emergency/station-points.json"));var points=StationPoints.Load(text);UnityEngine.Object.Destroy(text);
            testDrives=new GameObject("Actual catalog source binding test controls");var drives=new System.Collections.Generic.List<Escalator>();
            foreach(var entry in points.Escalators)
            {
                var node=new GameObject(entry.id);node.transform.SetParent(testDrives.transform,false);var drive=node.AddComponent<Escalator>();drive.enabled=false;drive.Setup(entry);drives.Add(drive);
            }
            foreach(var binding in bindings)Assert.That(binding.Bind(drives),Is.True,binding.lane_id+" "+binding.Issue);
            foreach(var run in runs)
            {
                run.ApplyDistance(0);Assert.That(run.AnimatedOriginalPartCount,Is.EqualTo(41));Assert.That(run.HasInventedReturnRoute,Is.False);
                var binding=run.GetComponent<EscalatorDriveBinding>();var motion=run.GetComponent<EscalatorMotion>();
                Assert.That(motion.Drive,Is.SameAs(System.Linq.Enumerable.Single(drives,x=>x.Entry.id==binding.lane_id)));
                Assert.That(motion.Loops.Length,Is.Zero);
                foreach(var ride in run.GetComponentsInChildren<EscalatorRide>())Assert.That(ride.Drive,Is.SameAs(motion.Drive));
                foreach(var tread in run.Treads)
                {
                    var mesh=tread.GetComponent<MeshFilter>().sharedMesh;Assert.That(mesh.triangles.Length,Is.EqualTo(96));
                    Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount));Assert.That(tread.GetComponent<SourcePartIdentity>().LegacyGeometrySeparated,Is.True);
                }
            }
            var sampled=runs[0];var sampledDrive=(Escalator)sampled.GetComponent<EscalatorMotion>().Drive;
            var originalMesh=sampled.Treads[20].GetComponent<MeshFilter>().sharedMesh;var local=originalMesh.vertices[0];var before=sampled.Treads[20].TransformPoint(local);
            sampledDrive.AdvanceDrive(.2f);sampled.ApplyMotion();var moved=sampled.Treads[20].TransformPoint(local);
            Assert.That(Vector3.Distance(moved,before),Is.GreaterThan(.05f),"Actual retained native vertices must translate, rather than only a shader property changing");
            sampledDrive.Stop();sampledDrive.AdvanceDrive(1);sampled.ApplyMotion();Assert.That(Vector3.Distance(sampled.Treads[20].TransformPoint(local),moved),Is.LessThan(.00001f));
            Assert.That(sampledDrive.Restart(out var reason),Is.True,reason);sampledDrive.AdvanceDrive(.1f);sampled.ApplyMotion();
            Assert.That(Vector3.Distance(sampled.Treads[20].TransformPoint(local),moved),Is.GreaterThan(.02f));
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(testDrives!=null)UnityEngine.Object.Destroy(testDrives);Time.timeScale=1;
            var station=UnityEngine.SceneManagement.SceneManager.GetSceneByName(ChooGuard.App.Fps.Shell.SceneFlow.StationScene);
            if(station.isLoaded)
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(UnityEngine.SceneManagement.SceneManager.CreateScene("PlatformSourceTestScratch"));
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(station);
            }
        }
    }
}
