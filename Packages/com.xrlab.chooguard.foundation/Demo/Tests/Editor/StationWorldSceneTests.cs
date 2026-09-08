using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChooGuard.Foundation.Demo.Editor;
namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class StationWorldSceneTests
    {
        private string folder; private Scene scene; private Scene previous;
        [SetUp] public void Setup(){previous=SceneManager.GetActiveScene();folder="Assets/CHOOguardGenerated/WorldTests_"+Guid.NewGuid().ToString("N");scene=FoundationDemoSceneBuilder.Build(folder);}
        [TearDown] public void Cleanup(){if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);AssetDatabase.DeleteAsset(folder);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        [Test]
        public void RegisteredWorldBindingsRoutinesAndAlternateRoutesSurviveEachClosure()
        {
            var root=scene.GetRootGameObjects().Single();var world=root.GetComponentInChildren<StationWorldController>();Assert.That(world,Is.Not.Null);
            var config=JsonUtility.FromJson<StationWorldConfig>(AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/station-twin-profile.json").text);
            var graph=root.GetComponentInChildren<DemoWalkGraph>();graph.Rebuild();var version=graph.Version;
            foreach(var site in world.Portals)
            {
                Assert.That(site.transform.rotation,Is.EqualTo(Quaternion.identity));Assert.That(site.transform.lossyScale,Is.EqualTo(Vector3.one));
                Assert.That(site.CanActivate(new[]{site.Waypoint}),Is.False,"Never spawn a closure through an actor.");
                var south=site.Waypoint-Vector3.forward*1.3f;var north=site.Waypoint+Vector3.forward*1.3f;
                Assert.That(graph.Clear(south,north),Is.True,"Ordinary passage is usable.");
                site.SetState(true,true,false);graph.SetRestrictedAreas(new[]{site.ExclusionBounds});Assert.That(graph.Version,Is.GreaterThan(version));
                var pictograms=site.GetComponentsInChildren<Renderer>().Where(x=>x.name.StartsWith("Static_White")&&x.transform.parent.name=="Blender_AssemblySign").ToArray();
                Assert.That(pictograms.Length,Is.GreaterThan(0));
                foreach(var renderer in pictograms)
                {
                    var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                    Assert.That(properties.isEmpty,Is.True,"Keep the white direction pictogram visible when the backing changes state.");
                }
                Assert.That(graph.Clear(south,north),Is.False,"Physical/logical closure blocks direct passage.");
                var alternate=graph.Route(south,north);Assert.That(alternate.Length,Is.GreaterThan(2),site.SiteId);
                for(var i=1;i<alternate.Length;i++)Assert.That(graph.Clear(alternate[i-1],alternate[i]),Is.True);
                foreach(var point in config.routineStops)Assert.That(graph.Route(new Vector3(0,.1f,-3),point).Length,Is.GreaterThan(0),site.SiteId+" routine "+point);
                site.SetState(false,false,false);graph.SetRestrictedAreas(new Bounds[0]);Assert.That(graph.Clear(south,north),Is.True);
            }
        }
        [Test]
        public void PortalRequiresOppositeSidesInsideOpeningRatherThanProximity()
        {
            var portal=scene.GetRootGameObjects().Single().GetComponentInChildren<StationWorldController>().Portals[0];var approach=false;
            Assert.That(portal.TrackCrossing(portal.Waypoint-Vector3.forward*.5f,ref approach),Is.False);
            Assert.That(portal.TrackCrossing(portal.Waypoint-Vector3.forward*.3f,ref approach),Is.False);
            Assert.That(portal.TrackCrossing(portal.Waypoint+Vector3.forward*.5f+Vector3.right*10,ref approach),Is.False);
            Assert.That(portal.TrackCrossing(portal.Waypoint+Vector3.forward*.5f,ref approach),Is.False,"Going around outside the opening is not a crossing.");
            Assert.That(portal.TrackCrossing(portal.Waypoint-Vector3.forward*.5f,ref approach),Is.False);
            Assert.That(portal.TrackCrossing(portal.Waypoint+Vector3.forward*.5f,ref approach),Is.True);
        }
    }
}
