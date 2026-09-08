using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChooGuard.Foundation.Demo.Editor;

namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class ReconstructionReviewTests
    {
        [Test]
        public void BatchStartupMayReplaceOnlyOneCleanUntitledScene()
        {
            Assert.That(ReconstructionReviewBuilder.CanReplaceBatchStartup(true,1,"",false),Is.True);
            Assert.That(ReconstructionReviewBuilder.CanReplaceBatchStartup(false,1,"",false),Is.False);
            Assert.That(ReconstructionReviewBuilder.CanReplaceBatchStartup(true,2,"",false),Is.False);
            Assert.That(ReconstructionReviewBuilder.CanReplaceBatchStartup(true,1,"Assets/Saved.unity",false),Is.False);
            Assert.That(ReconstructionReviewBuilder.CanReplaceBatchStartup(true,1,"",true),Is.False);
        }

        [Test]
        public void ReviewCoordinateReflectionPreservesAsymmetricFixtureWithoutMetricScaling()
        {
            Assert.That(ReconstructionReviewBuilder.ReviewToUnityPoint(new Vector3(2,3,-7)), Is.EqualTo(new Vector3(2,3,7)));
            Assert.That(ReconstructionReviewBuilder.ReviewToUnityPoint(new Vector3(-5,-1,4)), Is.EqualTo(new Vector3(-5,-1,-4)));
        }

        [Test]
        public void ImportedGeometryRequiresCorrectBoundsColorsTrianglesAndNoCollision()
        {
            var go = new GameObject("fixture"); var mesh = new Mesh();
            try
            {
                mesh.vertices = new[]{new Vector3(-2,1,3),new Vector3(4,1,3),new Vector3(-2,5,7)};
                mesh.triangles = new[]{0,1,2}; mesh.colors32 = new[]{new Color32(255,0,0,255),new Color32(0,255,0,255),new Color32(0,0,255,255)};
                mesh.RecalculateBounds(); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>();
                var expected = new ReconstructionReviewBuilder.ReviewView { id="fixture", @object="fixture", vertices=3, outputTriangles=1,
                    expectedUnityBounds=new ReconstructionReviewBuilder.ReviewBounds {center=mesh.bounds.center,size=mesh.bounds.size}, colorSamples=Samples(mesh) };
                var receipt=ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected);
                Assert.That(receipt.importedVertices,Is.EqualTo(3)); Assert.That(receipt.triangles,Is.EqualTo(1));
                go.transform.localScale=Vector3.one*100;
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected));
                go.transform.localScale=Vector3.one; go.transform.rotation=Quaternion.Euler(0,180,0);
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected));
                go.transform.rotation=Quaternion.identity; var collider=go.AddComponent<BoxCollider>();
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected));
                UnityEngine.Object.DestroyImmediate(collider); mesh.colors32=Array.Empty<Color32>();
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected));
            }
            finally {UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(mesh);}
        }

        [Test]
        public void ImportedLinearColorSamplesRejectChannelSwapGammaAndWrongSamplePosition()
        {
            var go=new GameObject("color-fixture");var mesh=new Mesh();
            try
            {
                mesh.vertices=new[]{new Vector3(-2,1,3),new Vector3(4,1,3),new Vector3(-2,5,7)};mesh.triangles=new[]{0,1,2};
                var linear=new[]{new Color(.16f,.34f,.67f,1),new Color(.7f,.2f,.4f,1),new Color(.1f,.6f,.3f,1)};
                mesh.colors=linear;mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>();
                var expected=new ReconstructionReviewBuilder.ReviewView{id="color-fixture",@object="color-fixture",vertices=3,outputTriangles=1,
                    expectedUnityBounds=new ReconstructionReviewBuilder.ReviewBounds{center=mesh.bounds.center,size=mesh.bounds.size},colorSamples=Samples(mesh)};
                Assert.That(ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected).colorSamplesValidated,Is.EqualTo(3));
                mesh.colors=linear.Select(c=>new Color(c.b,c.g,c.r,c.a)).ToArray();
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected));
                mesh.colors=linear.Select(c=>c.gamma).ToArray();
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected));
                // One 8-bit step of export/import rounding is permitted; another gamma conversion is not.
                mesh.colors32=linear.Select(c=>(Color32)c).ToArray();
                Assert.That(ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected).maximumColorError,Is.LessThanOrEqualTo(.008f));
                expected.colorSamples[0].position+=Vector3.right*.1f;
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ValidateImportedView(go.transform,expected));
            }
            finally {UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);}
        }

        [Test]
        public void ManifestRejectsMetricCollisionAndWrongCoordinateContract()
        {
            var json=ValidManifest();
            Assert.That(ReconstructionReviewBuilder.ParseManifest(json).unit,Is.EqualTo("model_relative"));
            foreach(var invalid in new[]{json.Replace("model_relative","meters"),json.Replace("\"metersPerUnit\":null","\"metersPerUnit\":1"),
                json.Replace("\"collisionApproved\":false","\"collisionApproved\":true"),json.Replace("0,0,-1,0","0,0,1,0"),
                json.Replace("\"id\":\"fixture\"","\"id\":\"\""),json.Replace(new string('a',64),"bad"),
                json.Replace("linear-rgb","srgb"),json.Replace("\"colorEncoding\":\"linear-rgb\",",""),
                json.Replace("\"colorSamples\":","\"missingColorSamples\":"),json.Replace("\"r\":0.2","\"r\":2.0")})
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ParseManifest(invalid));
        }

        [Test]
        public void ShadingDefaultsToObservedAndRejectsUnknownOrAmbiguousModes()
        {
            var json=ValidManifest();
            Assert.That(ReconstructionReviewBuilder.ParseManifest(json).shadingMode,Is.EqualTo("observed-unlit"));
            Assert.That(ReconstructionReviewBuilder.ParseManifest(json.Insert(1,"\"shadingMode\":\"authored-lit\",")).shadingMode,Is.EqualTo("authored-lit"));
            foreach(var field in new[]{"\"shadingMode\":\"photoreal\",","\"shadingMode\":null,","\"shadingMode\":\"\",",
                "\"shadingMode\":\"observed-unlit\",\"shadingMode\":\"authored-lit\","})
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ParseManifest(json.Insert(1,field)));
        }

        [TestCase("observed-unlit",0,"CHOOGuard/ReconstructionVertexColor")]
        [TestCase("authored-lit",2,"CHOOGuard/ReconstructionVertexColorLit")]
        public void OnlyAuthoredStudyReceivesReviewLightsAndLitShader(string mode,int lightCount,string shaderName)
        {
            var root=new GameObject("shading-fixture");
            try
            {
                ReconstructionReviewBuilder.ConfigureStudyLighting(root.transform,mode);
                var lights=root.GetComponentsInChildren<Light>();Assert.That(lights.Length,Is.EqualTo(lightCount));
                Assert.That(ReconstructionReviewBuilder.ShaderNameForMode(mode),Is.EqualTo(shaderName));
                Assert.That(Shader.Find(shaderName),Is.Not.Null);
                foreach(var light in lights){Assert.That(light.type,Is.EqualTo(LightType.Directional));Assert.That(light.intensity,Is.InRange(.1f,1f));}
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ViewNavigationKeepsOneAssemblyActiveForOneOrTwoViews(int count)
        {
            var root=new GameObject("view-fixture");
            try
            {
                var camera=new GameObject("camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
                var models=Enumerable.Range(0,count).Select(i=>{var model=new GameObject("assembly-"+i).transform;model.SetParent(root.transform,false);return model;}).ToArray();
                var controller=root.AddComponent<ReconstructionReviewController>();
                controller.Configure(models,models.Select(x=>x.name).ToArray(),models.Select(x=>new Bounds(Vector3.forward,Vector3.one)).ToArray(),camera,"fixture");
                Assert.That(controller.ActiveViewCount,Is.EqualTo(1));
                controller.CycleView(1);Assert.That(controller.SelectedView,Is.EqualTo(1%count));Assert.That(controller.ActiveViewCount,Is.EqualTo(1));
                controller.CycleView(-1);Assert.That(controller.SelectedView,Is.EqualTo(0));Assert.That(controller.ActiveViewCount,Is.EqualTo(1));
                Assert.Throws<ArgumentOutOfRangeException>(()=>controller.SelectView(count));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test]
        public void SourceMustBeOwnedLocalOutputAndItsHashesMustMatch()
        {
            Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ReadSource("Packages"));
            var dir=Path.Combine("reconstruction/output","viewer-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir,"review-manifest.blender.json"),ValidManifest());
                File.WriteAllText(Path.Combine(dir,"refined-review.fbx"),"fixture");
                File.WriteAllText(Path.Combine(dir,"review-manifest.json"),"{}");
                File.WriteAllText(Path.Combine(dir,"review-surfaces.glb"),"fixture");
                Assert.Throws<InvalidOperationException>(()=>ReconstructionReviewBuilder.ReadSource(dir));
            }
            finally {Directory.Delete(dir,true);}
        }

        [Test]
        public void SourceAvailableBuildPreservesBoundsOneViewAndSerializedOriginCamera()
        {
            var source=ReconstructionReviewBuilder.SourceDirectoryFromArguments();
            if(!File.Exists(Path.Combine(source,"review-manifest.blender.json")))Assert.Ignore("Local reconstruction output is intentionally not Git-tracked.");
            var manifest=ReconstructionReviewBuilder.ReadSource(source).manifest;
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var folder=ReconstructionReviewBuilder.DefaultGeneratedRoot+"/Tests_"+Guid.NewGuid().ToString("N");
            Scene scene=default;
            try
            {
                scene=ReconstructionReviewBuilder.Build(source,folder);var path=scene.path;
                if(SceneManager.sceneCount>1)
                {EditorSceneManager.CloseScene(scene,true);scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);}
                else scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var root=scene.GetRootGameObjects().Single();var controller=root.GetComponent<ReconstructionReviewController>();
                Assert.That(controller,Is.Not.Null);Assert.That(controller.ViewCount,Is.EqualTo(manifest.views.Length));
                Assert.That(controller.SelectedView,Is.EqualTo(0));Assert.That(controller.ActiveViewCount,Is.EqualTo(1));
                var camera=root.GetComponentInChildren<Camera>();Assert.That(camera.transform.position,Is.EqualTo(Vector3.zero));
                Assert.That(Vector3.Dot(camera.transform.forward,Vector3.forward),Is.GreaterThan(.99999f));
                Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);Assert.That(root.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
                controller.CycleView(1);Assert.That(controller.ActiveViewCount,Is.EqualTo(1));Assert.That(controller.SelectedView,Is.EqualTo(1%manifest.views.Length));
                Assert.Throws<ArgumentOutOfRangeException>(()=>controller.SelectView(manifest.views.Length));
                Assert.That(root.GetComponentsInChildren<Light>(true).Length,Is.EqualTo(manifest.shadingMode=="authored-lit"?2:0));
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    Assert.That(renderer.sharedMaterials.All(material=>material.shader.name==ReconstructionReviewBuilder.ShaderNameForMode(manifest.shadingMode)),Is.True);
                var transforms=root.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.transform.localToWorldMatrix).ToArray();
                controller.FrameSelected();controller.ResetCamera();
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.transform.localToWorldMatrix).ToArray(),Is.EqualTo(transforms),"Camera controls never normalize or move source meshes.");
                Assert.That(File.ReadAllText(folder+"/import-review.json"),Does.Contain("model_relative"));
                Assert.That(File.ReadAllText(folder+"/import-review.json"),Does.Contain("not a training SceneBundle"));
            }
            finally
            {
                // A batch startup has no persisted scene setup. Never unload its last
                // scene or restore an empty setup: that dirties subsequent scene tests.
                if(setup.Any(x=>x.isLoaded)&&setup.Count(x=>x.isActive)==1)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static ReconstructionReviewBuilder.ColorSample[] Samples(Mesh mesh)=>mesh.vertices.Select((point,index)=>
            new ReconstructionReviewBuilder.ColorSample{position=point,color=mesh.colors[index]}).ToArray();

        private static string ValidManifest() => "{\"schemaVersion\":\"blender-reconstruction-review-1\",\"unit\":\"model_relative\",\"metersPerUnit\":null,\"collisionApproved\":false,\"colorEncoding\":\"linear-rgb\","+
            "\"fbxSha256\":\""+new string('a',64)+"\",\"inputManifestSha256\":\""+new string('b',64)+"\",\"sourceGlbSha256\":\""+new string('c',64)+"\","+
            "\"unityFromReviewRowMajor\":[1,0,0,0,0,1,0,0,0,0,-1,0,0,0,0,1],\"views\":[{\"id\":\"fixture\",\"object\":\"fixture\",\"vertices\":3,\"outputTriangles\":1,"+
            "\"expectedUnityBounds\":{\"center\":{\"x\":0,\"y\":0,\"z\":2},\"size\":{\"x\":2,\"y\":1,\"z\":1}},\"colorSamples\":["+
            "{\"position\":{\"x\":0,\"y\":0,\"z\":2},\"color\":{\"r\":0.2,\"g\":0.4,\"b\":0.6,\"a\":1}},"+
            "{\"position\":{\"x\":1,\"y\":0,\"z\":2},\"color\":{\"r\":0.3,\"g\":0.5,\"b\":0.7,\"a\":1}},"+
            "{\"position\":{\"x\":0,\"y\":1,\"z\":2},\"color\":{\"r\":0.4,\"g\":0.6,\"b\":0.8,\"a\":1}}]}]}";
    }
}
