using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChooGuard.Editor.Twin;

namespace ChooGuard.Tests.EditMode
{
    public sealed class BusanokFinishesSceneTests
    {
        [Serializable] sealed class Dependency { public string owner; }
        [Serializable] sealed class Entry { public string owner,source_asset;public Dependency[] companions,collider_companions; }
        [Serializable] sealed class Registry { public Entry[] entries; }
        [Test]
        public void SavedTenantFinishesPreserveOtherShopsAndSupportAcrossTheSourceFloorCut()
        {
            var saved=EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity");
            try
            {
                var entries=JsonUtility.FromJson<Registry>(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry)).entries;
                var floorEntry=entries.Single(e=>e.source_asset.EndsWith("/Box_Majibang_3F_L.asset",StringComparison.Ordinal));
                var ceilingEntry=entries.Single(e=>e.source_asset.EndsWith("/Box_Majibang_ShopCeiling.asset",StringComparison.Ordinal));
                var floor=GameObject.Find(floorEntry.owner).GetComponent<MeshFilter>();
                var ceiling=GameObject.Find(ceilingEntry.owner).GetComponent<MeshFilter>();
                var wood=GameObject.Find(floorEntry.companions.Single().owner).GetComponent<MeshFilter>();
                var sourceFloor=AssetDatabase.LoadAssetAtPath<Mesh>(floorEntry.source_asset);
                var sourceCeiling=AssetDatabase.LoadAssetAtPath<Mesh>(ceilingEntry.source_asset);
                var floorCollision=floor.GetComponent<MeshCollider>();
                var woodCollision=GameObject.Find(floorEntry.collider_companions.Single().owner).GetComponent<MeshCollider>();
                Assert.That(floorCollision.sharedMesh,Is.SameAs(floor.sharedMesh));
                Assert.That(woodCollision.sharedMesh,Is.SameAs(wood.sharedMesh));
                Assert.That(floor.sharedMesh.vertices.Take(sourceFloor.vertexCount),Is.EqualTo(sourceFloor.vertices));
                Assert.That(floor.sharedMesh.normals.Take(sourceFloor.vertexCount),Is.EqualTo(sourceFloor.normals));
                Assert.That(floor.sharedMesh.tangents.Take(sourceFloor.tangents.Length),Is.EqualTo(sourceFloor.tangents));
                for(int u=0;u<8;u++)
                {
                    var original=new List<Vector4>();var kept=new List<Vector4>();
                    sourceFloor.GetUVs(u,original);floor.sharedMesh.GetUVs(u,kept);
                    Assert.That(kept.Take(original.Count),Is.EqualTo(original));
                }
                Assert.That(ceiling.sharedMesh.triangles,Is.EqualTo(sourceCeiling.triangles.Skip(6)));
                Assert.That(ceiling.sharedMesh.vertices,Is.EqualTo(sourceCeiling.vertices));
                var shadow=ceiling.transform.Find(ceiling.name+"_ShadowCaster").GetComponent<MeshFilter>();
                Assert.That(shadow.sharedMesh,Is.SameAs(ceiling.sharedMesh));
                Physics.SyncTransforms();
                int insideProbes=0,outsideProbes=0;
                foreach(float x in new[]{26.501f,26.55f,29f,31.5f,33f,36.45f,36.499f})
                foreach(float z in new[]{-2.349f,-2.3f,0f,2.2f,4.5f,6.7f,6.749f})
                {
                    var point=floor.transform.TransformPoint(new Vector3(x,12.2f,z));
                    var ray=new Ray(point+Vector3.up*.25f,Vector3.down);
                    Assert.That(woodCollision.Raycast(ray,out var hit,.35f),Is.True,$"Missing tenant support at native {x},{z}");
                    Assert.That(Vector3.Distance(hit.point,point),Is.LessThan(.0002f));
                    Assert.That(floorCollision.Raycast(ray,out _,.35f),Is.False,"Tenant finish must transfer support without duplicate top faces");
                    insideProbes++;
                }
                var vertices=sourceFloor.vertices;var triangles=sourceFloor.triangles;
                for(int ordinal=0;ordinal<triangles.Length/3;ordinal++)
                {
                    if(ordinal==16||ordinal==18)continue;
                    var a=vertices[triangles[ordinal*3]];var b=vertices[triangles[ordinal*3+1]];var c=vertices[triangles[ordinal*3+2]];
                    if(Mathf.Abs(a.y-12.2f)>.00001f||Mathf.Abs(b.y-12.2f)>.00001f||Mathf.Abs(c.y-12.2f)>.00001f)continue;
                    var point=floor.transform.TransformPoint((a+b+c)/3f);var ray=new Ray(point+Vector3.up*.25f,Vector3.down);
                    Assert.That(floorCollision.Raycast(ray,out var hit,.35f),Is.True,"Unchanged concourse source support missing");
                    Assert.That(Vector3.Distance(hit.point,point),Is.LessThan(.0002f));outsideProbes++;
                }
                Assert.That(insideProbes,Is.EqualTo(49));Assert.That(outsideProbes,Is.GreaterThanOrEqualTo(3));
                Func<Mesh> resolve=()=>ArchitecturalCorrectionRegistry.ResolveForRebuild(sourceFloor,floorEntry.source_asset,floorEntry.owner,floor.transform.localToWorldMatrix);
                Assert.That(resolve(),Is.SameAs(floor.sharedMesh));
                try{woodCollision.enabled=false;Assert.Throws<InvalidDataException>(()=>resolve());}finally{woodCollision.enabled=true;}
                Assert.That(ArchitecturalCorrectionRegistry.ResolveForRebuild(sourceCeiling,ceilingEntry.source_asset,ceilingEntry.owner,ceiling.transform.localToWorldMatrix),Is.SameAs(ceiling.sharedMesh));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(saved.Any(s=>s.isLoaded)&&saved.Count(s=>s.isActive)==1&&saved.Where(s=>s.isLoaded).All(s=>!string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
        }
    }
}
