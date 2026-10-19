using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChooGuard.Editor.Twin;
namespace ChooGuard.Tests.EditMode
{
    public sealed class BusanokReturnSceneTests
    {
        static void SameChannels(Mesh source,Mesh result)
        {
            Assert.That(result.vertices,Is.EqualTo(source.vertices));Assert.That(result.normals,Is.EqualTo(source.normals));
            Assert.That(result.tangents,Is.EqualTo(source.tangents));Assert.That(result.colors,Is.EqualTo(source.colors));
            for(int channel=0;channel<8;channel++){var a=new List<Vector4>();var b=new List<Vector4>();source.GetUVs(channel,a);result.GetUVs(channel,b);Assert.That(b,Is.EqualTo(a));}
        }
        [Test]
        public void CanonicalReturnFixturePreservesCollisionTransferAndRejectsEveryChangedDependency()
        {
            using(var fixture=new CanonicalStructuralDependencyFixture("/Box_Shop_부산옥_Walls.asset"))
            {
                Assert.That(fixture.Resolve(),Is.SameAs(fixture.Target));
                var barrier=fixture.Colliders.Single();var row=fixture.Row;
                var sourceMatrix=CanonicalStructuralDependencyFixture.Matrix(row["world_matrix"]);
                var centre=sourceMatrix.MultiplyPoint3x4(new Vector3(36.5f,13.4f,2.2f));
                var normal=sourceMatrix.MultiplyVector(Vector3.right).normalized;
                var retainedOwner=new GameObject("IsolatedRetainedWallCollision");
                try
                {
                    CanonicalStructuralDependencyFixture.Place(retainedOwner.transform,sourceMatrix);
                    var retained=retainedOwner.AddComponent<MeshCollider>();retained.sharedMesh=fixture.Target;
                    Physics.SyncTransforms();
                    Assert.That(barrier.Raycast(new Ray(centre-normal*.5f,normal),out var hit,1f),Is.True);
                    Assert.That(Vector3.Distance(hit.point,centre),Is.LessThan(.0002f));
                    Assert.That(retained.Raycast(new Ray(centre-normal*.5f,normal),out _,1f),Is.False,"Transferred source face cannot remain as a duplicate opaque wall");
                }
                finally{UnityEngine.Object.DestroyImmediate(retainedOwner);}
                try{barrier.enabled=false;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}finally{barrier.enabled=true;}
                try
                {
                    barrier.convex=true;Assert.That(barrier.convex,Is.True);Assert.Throws<InvalidDataException>(()=>fixture.Resolve());
                    barrier.isTrigger=true;Assert.That(barrier.isTrigger,Is.True);Assert.Throws<InvalidDataException>(()=>fixture.Resolve());
                }
                finally{barrier.isTrigger=false;barrier.convex=false;}
                var mesh=barrier.sharedMesh;
                try{barrier.sharedMesh=null;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}finally{barrier.sharedMesh=mesh;}
                var position=barrier.transform.position;
                try{barrier.transform.position=position+Vector3.right*.001f;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}finally{barrier.transform.position=position;}
                var surface=fixture.Surfaces[0];var renderer=surface.GetComponent<MeshRenderer>();
                var material=renderer.sharedMaterial;var otherMaterial=new Material(material);
                try{renderer.sharedMaterial=otherMaterial;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}
                finally{renderer.sharedMaterial=material;UnityEngine.Object.DestroyImmediate(otherMaterial);}
                mesh=surface.sharedMesh;var otherMesh=UnityEngine.Object.Instantiate(mesh);
                try{surface.sharedMesh=otherMesh;Assert.Throws<InvalidDataException>(()=>fixture.Resolve());}
                finally{surface.sharedMesh=mesh;UnityEngine.Object.DestroyImmediate(otherMesh);}
                Assert.That(fixture.Resolve(),Is.SameAs(fixture.Target));
            }
        }
        [Test]
        public void FixedReturnInstallerKeepsItsEightOriginalFacesAndRejectsAChangedNativeBoundary()
        {
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ChooGuard/Art/StationInterior/Majibang/Box_Shop_부산옥_Walls.asset");
            string before=ArchitecturalCorrectionRegistry.CompleteChannelDigest(source);
            var result=BusanokGlazedReturnSetup.RetainNativeWall(source);
            try{SameChannels(source,result);Assert.That(result.triangles,Is.EqualTo(source.triangles.Take(24)));Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(source),Is.EqualTo(before));}
            finally{UnityEngine.Object.DestroyImmediate(result);}
            var changed=UnityEngine.Object.Instantiate(source);
            try{var vertices=changed.vertices;vertices[source.triangles[24]]+=Vector3.right*.01f;changed.vertices=vertices;Assert.Throws<InvalidDataException>(()=>BusanokGlazedReturnSetup.RetainNativeWall(changed));}
            finally{UnityEngine.Object.DestroyImmediate(changed);}
        }
    }
    internal sealed class CanonicalStructuralDependencyFixture:IDisposable
    {
        readonly SceneSetup[] saved;
        internal readonly JToken Row;
        internal readonly Mesh Source,Target;
        internal readonly List<MeshFilter> Surfaces=new List<MeshFilter>();
        internal readonly List<MeshCollider> Colliders=new List<MeshCollider>();
        internal CanonicalStructuralDependencyFixture(string suffix)
        {
            saved=EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Row=((JArray)JObject.Parse(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry))["entries"])
                .Single(row=>((string)row["source_asset"]).EndsWith(suffix,StringComparison.Ordinal));
            Source=AssetDatabase.LoadAssetAtPath<Mesh>((string)Row["source_asset"]);
            Target=AssetDatabase.LoadAssetAtPath<Mesh>((string)Row["replacement_asset"]);
            foreach(var dependency in Row["companions"] as JArray??new JArray())
            {
                var owner=Owner((string)dependency["owner"]);Place(owner.transform,Matrix(dependency["world_matrix"]??Row["world_matrix"]));
                var filter=owner.AddComponent<MeshFilter>();filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>((string)dependency["asset"]);
                var renderer=owner.AddComponent<MeshRenderer>();renderer.sharedMaterials=((JArray)dependency["materials"]).Select(row=>AssetDatabase.LoadAssetAtPath<Material>((string)row["asset"])).ToArray();
                Surfaces.Add(filter);
            }
            foreach(var dependency in Row["collider_companions"] as JArray??new JArray())
            {
                var owner=Owner((string)dependency["owner"]);Place(owner.transform,Matrix(dependency["world_matrix"]));owner.layer=(int)dependency["layer"];
                var collider=owner.AddComponent<MeshCollider>();collider.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>((string)dependency["asset"]);
                collider.convex=(bool)dependency["convex"];collider.isTrigger=(bool)dependency["is_trigger"];Colliders.Add(collider);
            }
        }
        static GameObject Owner(string path)
        {
            Transform parent=null;
            foreach(string name in path.Split('/'))
            {
                var child=parent==null?GameObject.Find(name)?.transform:parent.Find(name);
                if(child==null){child=new GameObject(name).transform;child.SetParent(parent,false);}parent=child;
            }
            return parent.gameObject;
        }
        internal static Matrix4x4 Matrix(JToken values)
        {var matrix=new Matrix4x4();for(int row=0;row<4;row++)for(int col=0;col<4;col++)matrix[row,col]=(float)values[row][col];return matrix;}
        internal static void Place(Transform owner,Matrix4x4 matrix)
        {owner.SetPositionAndRotation(matrix.GetColumn(3),matrix.rotation);owner.localScale=matrix.lossyScale;}
        internal Mesh Resolve()=>ArchitecturalCorrectionRegistry.ResolveStructuralStageForRebuild(Source,(string)Row["source_asset"],(string)Row["owner"],Matrix(Row["world_matrix"]));
        public void Dispose()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            if(saved.Any(row=>row.isLoaded)&&saved.Count(row=>row.isActive)==1&&saved.Where(row=>row.isLoaded).All(row=>!string.IsNullOrEmpty(row.path)))EditorSceneManager.RestoreSceneManagerSetup(saved);
        }
    }

}
