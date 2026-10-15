using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChooGuard.Editor.Twin;

namespace ChooGuard.Tests.EditMode
{
    public sealed class BusanokReturnSceneTests
    {
        [Serializable] sealed class Dependency { public string owner,asset; }
        [Serializable] sealed class Entry { public string owner,source_asset,replacement_asset;public Dependency[] companions,collider_companions; }
        [Serializable] sealed class Registry { public Entry[] entries; }
        [Test]
        public void ActualGlazedReturnKeepsItsBoundaryAndRebuildRejectsMissingCollisionOrChangedSurface()
        {
            var saved=EditorSceneManager.GetSceneManagerSetup();
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity");
            try
            {
                var entries=JsonUtility.FromJson<Registry>(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry)).entries;
                var entry=entries.Single(e=>e.source_asset.EndsWith("/Box_Shop_부산옥_Walls.asset",StringComparison.Ordinal));
                var target=GameObject.Find(entry.owner).GetComponent<MeshFilter>();
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(entry.source_asset);
                var barrier=GameObject.Find(entry.collider_companions.Single().owner).GetComponent<MeshCollider>();
                Func<Mesh> resolve=()=>ArchitecturalCorrectionRegistry.ResolveForRebuild(source,entry.source_asset,entry.owner,target.transform.localToWorldMatrix);
                Assert.That(resolve(),Is.SameAs(target.sharedMesh));
                Assert.That(target.sharedMesh.triangles,Is.EqualTo(source.triangles.Take(24).ToArray()));
                Physics.SyncTransforms();
                var centre=target.transform.TransformPoint(new Vector3(36.5f,13.4f,2.2f));
                var normal=target.transform.TransformVector(Vector3.right).normalized;
                Assert.That(barrier.Raycast(new Ray(centre-normal*.5f,normal),out var hit,1f),Is.True,"The fixed glazed return must retain its real boundary");
                Assert.That(Vector3.Distance(hit.point,centre),Is.LessThan(.0002f));
                Assert.That(target.GetComponent<MeshCollider>().Raycast(new Ray(centre-normal*.5f,normal),out _,1f),Is.False,"The old opaque return must be transferred, not duplicated");
                try{barrier.enabled=false;Assert.Throws<InvalidDataException>(()=>resolve());}finally{barrier.enabled=true;}
                // Unity refuses a trigger on a concave mesh, so first construct an actual
                // convex/trigger state instead of asserting against an ignored assignment.
                try
                {
                    barrier.convex=true;Assert.That(barrier.convex,Is.True);
                    Assert.Throws<InvalidDataException>(()=>resolve());
                    barrier.isTrigger=true;Assert.That(barrier.isTrigger,Is.True);
                    Assert.Throws<InvalidDataException>(()=>resolve());
                }
                finally{barrier.isTrigger=false;barrier.convex=false;}
                var barrierMesh=barrier.sharedMesh;
                try{barrier.sharedMesh=null;Assert.Throws<InvalidDataException>(()=>resolve());}finally{barrier.sharedMesh=barrierMesh;}
                var position=barrier.transform.localPosition;
                try{barrier.transform.localPosition=position+Vector3.right*.001f;Assert.Throws<InvalidDataException>(()=>resolve());}finally{barrier.transform.localPosition=position;}
                var surface=GameObject.Find(entry.companions[0].owner).GetComponent<MeshFilter>();
                var originalMaterial=surface.GetComponent<MeshRenderer>().sharedMaterial;
                var replacementMaterial=new Material(originalMaterial){hideFlags=HideFlags.HideAndDontSave};
                try{surface.GetComponent<MeshRenderer>().sharedMaterial=replacementMaterial;Assert.Throws<InvalidDataException>(()=>resolve());}
                finally{surface.GetComponent<MeshRenderer>().sharedMaterial=originalMaterial;UnityEngine.Object.DestroyImmediate(replacementMaterial);}
                var originalSurface=surface.sharedMesh;
                var replacementSurface=UnityEngine.Object.Instantiate(originalSurface);
                try{surface.sharedMesh=replacementSurface;Assert.Throws<InvalidDataException>(()=>resolve());}
                finally{surface.sharedMesh=originalSurface;UnityEngine.Object.DestroyImmediate(replacementSurface);}
                Assert.That(resolve(),Is.SameAs(target.sharedMesh));
            }
            finally
            {
                // All fixture mutations above are reverted. Discard the test-loaded scene before restoring
                // the original saved setup; this test never writes the production scene.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                // Unity's EditMode runner can start with an unsaved, empty test scene;
                // that setup has no restorable loaded/active asset path.
                if(saved.Any(s=>s.isLoaded)&&saved.Count(s=>s.isActive)==1&&
                   saved.Where(s=>s.isLoaded).All(s=>!string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
        }
    }
}
