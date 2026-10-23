using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    public sealed class CurrentApprovedFloorBindingTests
    {
        static string PathOf(Transform value)=>value.parent==null?value.name:PathOf(value.parent)+"/"+value.name;
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        [Test]
        public void EveryCurrentApprovedFloorKeepsItsExactActiveRenderAndPhysicalMeshBindings()
        {
            var fixture=JObject.Parse(File.ReadAllText("content/fixtures/architecture/current-approved-floor-bindings.json"));
            var rows=(JArray)fixture["floors"];Assert.That(rows.Count,Is.GreaterThan(0));
            Assert.That(rows.Select(row=>(string)row["global_object_id"]).Distinct().Count(),Is.EqualTo(rows.Count));
            var saved=EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity");
            try
            {
                var hashes=new Dictionary<string,string>();
                foreach(var row in rows)
                {
                    Assert.That(GlobalObjectId.TryParse((string)row["global_object_id"],out var identity),Is.True);
                    var owner=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity) as GameObject;
                    Assert.That(owner,Is.Not.Null,(string)row["path"]);Assert.That(PathOf(owner.transform),Is.EqualTo((string)row["path"]));
                    Assert.That(owner.activeInHierarchy,Is.True);
                    var renderer=owner.GetComponent<MeshRenderer>();var filter=owner.GetComponent<MeshFilter>();var collider=owner.GetComponent<MeshCollider>();
                    Assert.That(renderer!=null&&renderer.enabled&&filter!=null&&collider!=null,Is.True);
                    Assert.That(collider.enabled&&!collider.isTrigger&&!collider.convex,Is.True);
                    foreach(var pair in new[]{new{role="render_mesh",mesh=filter.sharedMesh},new{role="collision_mesh",mesh=collider.sharedMesh}})
                    {
                        var pin=row[pair.role];string path=(string)pin["asset"];
                        Assert.That(pair.mesh,Is.Not.Null);Assert.That(AssetDatabase.GetAssetPath(pair.mesh).Normalize(System.Text.NormalizationForm.FormC),Is.EqualTo(path.Normalize(System.Text.NormalizationForm.FormC)));
                        Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo((string)pin["guid"]));
                        if(!hashes.ContainsKey(path))hashes[path]=Hash(path);
                        Assert.That(hashes[path],Is.EqualTo((string)pin["file_sha256"]));
                        Assert.That(pair.mesh.triangles.Length,Is.GreaterThan(0));
                    }
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(saved.Any(row=>row.isLoaded)&&saved.Count(row=>row.isActive)==1&&saved.Where(row=>row.isLoaded).All(row=>!string.IsNullOrEmpty(row.path)))EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
        }
    }
}
