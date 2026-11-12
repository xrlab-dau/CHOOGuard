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
        static Mesh CompactionFixture(bool compact)
        {
            var mesh=new Mesh();
            mesh.vertices=compact
                ?new[]{Vector3.right,Vector3.forward,Vector3.zero}
                :new[]{Vector3.one*99,Vector3.zero,Vector3.right,Vector3.forward};
            mesh.normals=Enumerable.Repeat(Vector3.up,mesh.vertexCount).ToArray();
            mesh.tangents=Enumerable.Repeat(new Vector4(1,0,0,-1),mesh.vertexCount).ToArray();
            mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
            mesh.uv=mesh.vertices.Select(value=>new Vector2(value.x,value.z)).ToArray();
            mesh.uv8=mesh.uv;
            mesh.triangles=compact?new[]{2,0,1}:new[]{1,2,3};
            return mesh;
        }
        [Test]
        public void IndexedSourceContractAcceptsUnusedVertexRemovalAndIndexRenumbering()
        {
            var original=CompactionFixture(false);var compact=CompactionFixture(true);
            try{Assert.That(SourceGeometryContractFixtures.IndexedChannels(compact),Is.EqualTo(SourceGeometryContractFixtures.IndexedChannels(original)));}
            finally{UnityEngine.Object.DestroyImmediate(original);UnityEngine.Object.DestroyImmediate(compact);}
        }
        [TestCase("position")]
        [TestCase("normal")]
        [TestCase("tangent")]
        [TestCase("color")]
        [TestCase("uv")]
        [TestCase("uv8")]
        [TestCase("uv_dimension")]
        [TestCase("winding")]
        [TestCase("material_slot")]
        [TestCase("topology")]
        public void IndexedSourceContractRejectsReferencedGeometryOrChannelChanges(string mutation)
        {
            var original=CompactionFixture(false);var compact=CompactionFixture(true);
            try
            {
                switch(mutation)
                {
                    case "position":var vertices=compact.vertices;vertices[0]+=Vector3.up;compact.vertices=vertices;break;
                    case "normal":var normals=compact.normals;normals[0]=Vector3.down;compact.normals=normals;break;
                    case "tangent":var tangents=compact.tangents;tangents[0].w=1;compact.tangents=tangents;break;
                    case "color":var colors=compact.colors;colors[0]=Color.red;compact.colors=colors;break;
                    case "uv":var uv=compact.uv;uv[0]=Vector2.zero;compact.uv=uv;break;
                    case "uv8":var uv8=compact.uv8;uv8[0]=Vector2.zero;compact.uv8=uv8;break;
                    case "uv_dimension":compact.SetUVs(0,compact.uv.Select(value=>new Vector3(value.x,value.y,0)).ToList());break;
                    case "winding":compact.triangles=new[]{2,1,0};break;
                    case "material_slot":compact.subMeshCount=2;compact.SetTriangles(Array.Empty<int>(),0);compact.SetTriangles(new[]{2,0,1},1);break;
                    case "topology":compact.SetIndices(new[]{2,0,1},MeshTopology.Points,0);break;
                    default:Assert.Fail("Unknown mutation");break;
                }
                Assert.That(SourceGeometryContractFixtures.IndexedChannels(compact),Is.Not.EqualTo(SourceGeometryContractFixtures.IndexedChannels(original)),mutation);
            }
            finally{UnityEngine.Object.DestroyImmediate(original);UnityEngine.Object.DestroyImmediate(compact);}
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator StoppingCurrentTreadRunsRestoresEveryApprovedSourceMeshAndPreservesItsChannelsAndIdentity()
        {
            using(new ProviderAwakeDeferral(Type.GetType(RetainedEmergencySessionFixture.ProviderTypeName,true)))
            {
                // Inspect the actual source map independently of the shipping scene list.
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/ChooGuard/Scenes/FpsStation.unity",
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
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
