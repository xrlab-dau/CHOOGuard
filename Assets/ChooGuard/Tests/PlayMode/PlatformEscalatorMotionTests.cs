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
        [Test]
        public void EmptyControllerAndRideStubsCannotBindAnActualCatalogDrive()
        {
            var root = new GameObject("No source geometry binding fixture"); root.SetActive(false);
            var driveObject = new GameObject("Exact catalog drive fixture");
            try
            {
                var motion = root.AddComponent<EscalatorMotion>();
                var rideObject = new GameObject("Retained trigger stub"); rideObject.transform.SetParent(root.transform, false);
                rideObject.AddComponent<BoxCollider>().isTrigger = true;
                var ride = rideObject.AddComponent<EscalatorRide>();
                var binding = root.AddComponent<EscalatorDriveBinding>(); binding.lane_id = "source-less-fixture-lane";
                var drive = driveObject.AddComponent<Escalator>(); drive.enabled = false;
                drive.Setup(new StationPoints.EscalatorEntry { id = binding.lane_id, path = new[] { Vector3.zero, Vector3.forward } });
                Assert.That(binding.Bind(new[] { drive }), Is.False, "A trigger and an empty motion component are not physical source machinery.");
                Assert.That(binding.Bound, Is.False); Assert.That(binding.Issue, Is.EqualTo("missing_operable_parts"));
                Assert.That(motion.Drive, Is.Null); Assert.That(ride.Drive, Is.Null); Assert.That(ride.WorldVelocity, Is.EqualTo(Vector3.zero));
                var run = root.AddComponent<PlatformEscalatorRun>();
                run.Treads = new Transform[3]; run.ObservedOrigins = new[] { Vector3.zero, Vector3.forward, Vector3.forward * 2 };
                run.SourcePitch = 1; run.ExposureAxis = Vector3.forward; run.ExposureMinimum = 0; run.ExposureMaximum = 2;
                Assert.That(binding.Bind(new[] { drive }), Is.False, "An all-null retained tread array is explicitly absent source.");
                Assert.That(run.SourceAvailable, Is.False); Assert.That(run.IsBound, Is.False); Assert.That(run.AnimatedOriginalPartCount, Is.Zero);
                Assert.That(binding.Bound, Is.False); Assert.That(motion.Drive, Is.Null); Assert.That(ride.Drive, Is.Null);
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true), Is.Empty, "Refusal must not create replacement machinery.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(driveObject); }
        }

        [Test]
        public void PartialSourceLossRetainsTheStrictMissingTreadFailure()
        {
            var root = new GameObject("Partial source loss fixture"); root.SetActive(false);
            var driveObject = new GameObject("Exact catalog drive fixture"); var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.forward }; mesh.triangles = new[] { 0, 1, 2 };
            try
            {
                var motion = root.AddComponent<EscalatorMotion>(); var run = root.AddComponent<PlatformEscalatorRun>();
                var tread = new GameObject("Only one retained tread"); tread.transform.SetParent(root.transform, false);
                tread.AddComponent<MeshFilter>().sharedMesh = mesh; tread.AddComponent<MeshRenderer>();
                run.Treads = new[] { tread.transform, null }; run.ObservedOrigins = new[] { Vector3.zero, Vector3.forward };
                run.SourcePitch = 1; run.ExposureAxis = Vector3.forward; run.ExposureMinimum = 0; run.ExposureMaximum = 1;
                var binding = root.AddComponent<EscalatorDriveBinding>(); binding.lane_id = "partial-source-fixture-lane";
                var drive = driveObject.AddComponent<Escalator>(); drive.enabled = false;
                drive.Setup(new StationPoints.EscalatorEntry { id = binding.lane_id, path = new[] { Vector3.zero, Vector3.forward } });
                var error = Assert.Throws<System.InvalidOperationException>(() => binding.Bind(new[] { drive }));
                Assert.That(error.Message, Is.EqualTo("Native source tread geometry is missing at index 1"));
                Assert.That(binding.Bound, Is.False); Assert.That(motion.Drive, Is.Null);
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(driveObject); UnityEngine.Object.DestroyImmediate(mesh); }
        }

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

#if UNITY_EDITOR
namespace ChooGuard.Tests.PlayMode
{
    public sealed class PlatformEscalatorSceneTests
    {
        private GameObject testDrives;
        [UnityTest,Timeout(180000)]
        public IEnumerator EveryCurrentSourceLaneUsesItsExactCatalogDriveAndRetainedSourceMeshMovesWithoutChannelChanges()
        {
            var source=(Newtonsoft.Json.Linq.JArray)SourceGeometryContractFixtures.Read("current-preserved-source-parts")["parts"];
            var lanes=System.Linq.Enumerable.GroupBy(System.Linq.Enumerable.Where(source,row=>(string)row["category"]=="escalator_tread_component"),row=>((string)row["path"]).Split('/')[2]);
            var textAsset=new TextAsset(System.IO.File.ReadAllText("Assets/ChooGuard/Art/Emergency/station-points.json"));
            var points=StationPoints.Load(textAsset);UnityEngine.Object.DestroyImmediate(textAsset);
            foreach(var lane in lanes)
            {
                var entry=System.Linq.Enumerable.Single(points.Escalators,value=>value.id==lane.Key);
                var root=new GameObject("CurrentSourceLaneAlgorithmFixture");root.SetActive(false);
                var driveObject=new GameObject("ExactCatalogDriveFixture");
                var mesh=SourceGeometryContractFixtures.Mesh(System.Linq.Enumerable.First(lane));
                var originalVertices=mesh.vertices;var originalUv=mesh.uv;
                try
                {
                    var motion=root.AddComponent<ChooGuard.Circulation.EscalatorMotion>();var run=root.AddComponent<ChooGuard.Circulation.PlatformEscalatorRun>();
                    run.Treads=new Transform[3];run.ObservedOrigins=new[]{new Vector3(0,1,0),new Vector3(0,.8f,.35f),new Vector3(0,.6f,.7f)};
                    run.Up=entry.up;run.SourcePitch=.4f;run.ExposureAxis=Vector3.forward;run.ExposureMinimum=0;run.ExposureMaximum=1.1f;
                    for(int index=0;index<3;index++){var tread=new GameObject("RetainedSourceTreadFixture");tread.transform.SetParent(root.transform,false);tread.transform.localPosition=run.ObservedOrigins[index];tread.AddComponent<MeshFilter>().sharedMesh=mesh;tread.AddComponent<MeshRenderer>();run.Treads[index]=tread.transform;}
                    var drive=driveObject.AddComponent<Escalator>();drive.enabled=false;drive.Setup(entry);
                    var binding=root.AddComponent<EscalatorDriveBinding>();binding.lane_id=entry.id;root.SetActive(true);
                    Assert.That(binding.Bind(new[]{drive}),Is.True);Assert.That(motion.Drive,Is.SameAs(drive));
                    run.ApplyDistance(0);var before=run.Treads[1].TransformPoint(mesh.vertices[0]);
                    drive.AdvanceDrive(.2f);run.ApplyMotion();var moved=run.Treads[1].TransformPoint(mesh.vertices[0]);
                    Assert.That(Vector3.Distance(before,moved),Is.GreaterThan(.05f));drive.Stop();drive.AdvanceDrive(1);run.ApplyMotion();
                    Assert.That(run.Treads[1].TransformPoint(mesh.vertices[0]),Is.EqualTo(moved));
                    Assert.That(drive.Restart(out var reason),Is.True,reason);drive.AdvanceDrive(.1f);run.ApplyMotion();
                    Assert.That(Vector3.Distance(run.Treads[1].TransformPoint(mesh.vertices[0]),moved),Is.GreaterThan(.02f));
                    Assert.That(mesh.vertices,Is.EqualTo(originalVertices));Assert.That(mesh.uv,Is.EqualTo(originalUv));
                    binding.lane_id="unregistered-fixture-lane";Assert.That(binding.Bind(new[]{drive}),Is.False);Assert.That(binding.Issue,Is.EqualTo("missing_lane_id"));
                    run.enabled=false;for(int index=0;index<3;index++)Assert.That(run.Treads[index].localPosition,Is.EqualTo(run.ObservedOrigins[index]));
                    Assert.That(run.HasInventedReturnRoute,Is.False);Assert.That(motion.Loops.Length,Is.Zero);
                    yield return null;
                }
                finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(driveObject);}
            }
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
#endif
