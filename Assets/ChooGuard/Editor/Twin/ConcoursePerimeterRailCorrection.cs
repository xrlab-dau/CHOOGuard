#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor.Twin
{
    public static class ConcoursePerimeterRailCorrection
    {
        const string Work = ".planning/2026-10-10-digital-twin-fidelity-rebuild/perimeter-rail";
        const string Art = "Assets/ChooGuard/Art/StationInterior/ConcourseFidelity20261010";
        public const string Receipt = "content/world/observed-interiors/concourse-perimeter-rail-20261010.json";
        const string Native6 = "FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6";
        static void Need(bool v, string m) { if (!v) throw new InvalidDataException(m); }
        static string Hash(string p) => ConcourseFidelityCorrection.Hash(p);
        static JObject State(Transform t) => Platform6BanksB24PlacementReplay.CarrierFields(t);
        static JObject Pin(UnityEngine.Object o) { string p = AssetDatabase.GetAssetPath(o); return new JObject { ["path"] = p, ["sha256"] = Hash(p), ["meta_sha256"] = Hash(p + ".meta"), ["guid"] = AssetDatabase.AssetPathToGUID(p) }; }
        static Mesh Retire(Mesh source, JToken selected)
        {
            var remove = new HashSet<int>(selected.Select(t => (int)t));
            Need(remove.SetEquals(Enumerable.Range(520, 14)), "Exactly reviewed original rail faces required");
            var copy = UnityEngine.Object.Instantiate(source); int ordinal = 0, removed = 0;
            for (int sm = 0; sm < source.subMeshCount; sm++)
            {
                var indices = source.GetTriangles(sm); var keep = new List<int>();
                for (int i = 0; i < indices.Length; i += 3, ordinal++)
                {
                    if (remove.Contains(ordinal)) { Need(sm == 1, "Unexpected original rail material slot"); removed++; }
                    else { keep.Add(indices[i]); keep.Add(indices[i + 1]); keep.Add(indices[i + 2]); }
                }
                copy.SetTriangles(keep, sm, false);
            }
            Need(removed == 14 && source.vertices.SequenceEqual(copy.vertices) && source.normals.SequenceEqual(copy.normals) && source.tangents.SequenceEqual(copy.tangents) && source.colors.SequenceEqual(copy.colors) && source.bounds == copy.bounds && source.boneWeights.SequenceEqual(copy.boneWeights) && source.bindposes.SequenceEqual(copy.bindposes) && source.GetVertexAttributes().SequenceEqual(copy.GetVertexAttributes()), "Original source vertex channels changed");
            for (int u = 0; u < 8; u++) { var a = new List<Vector4>(); var b = new List<Vector4>(); source.GetUVs(u,a); copy.GetUVs(u,b); Need(a.SequenceEqual(b), "Source UV changed"); }
            return copy;
        }
        public static string Install(string[] args)
        {
            Need(args.Length == 1 && !EditorApplication.isPlaying, "Pinned editmode request required");
            var request = JObject.Parse(File.ReadAllText(args[0])); var scene = SceneManager.GetActiveScene();
            Need(!scene.isDirty && Hash(scene.path) == (string)request["scene_sha256"] && !File.Exists(Receipt), "Clean predecessor epoch required");
            foreach (var pin in request["files"]) Need(Hash((string)pin["path"]) == (string)pin["sha256"], "Changed source/request evidence");
            ConcourseFidelityCorrection.Verify(Array.Empty<string>());
            var recipe = JObject.Parse(File.ReadAllText((string)request["recipe"]));
            var t = Platform6BankB1Replay.Find(Native6); var mf = t.GetComponent<MeshFilter>(); var mc = t.GetComponent<MeshCollider>(); var mr = t.GetComponent<MeshRenderer>();
            Mesh oldRender = mf.sharedMesh, oldCollision = mc.sharedMesh;
            Need(Hash(AssetDatabase.GetAssetPath(oldRender)) == (string)recipe["current_render_asset_sha256"] && Hash(AssetDatabase.GetAssetPath(oldCollision)) == (string)recipe["current_collider_asset_sha256"], "Native source predecessor changed");
            Need(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mr.sharedMaterials[1])) == (string)recipe["material_guid"], "Rail source material differs");
            Directory.CreateDirectory(Work); File.Copy(scene.path, Work + "/FpsStation-before.unity", false); File.Copy(ConcourseFidelityCorrection.Registry, Work + "/registry-before.json", false);
            var before = State(t); var sources = new JArray(Pin(oldRender), Pin(oldCollision)); var created = new List<string>(); GameObject go = null;
            try
            {
                var render = Retire(oldRender, recipe["retire_current_render_triangles"]); var collision = Retire(oldCollision, recipe["retire_current_collider_triangles"]);
                go = new GameObject("P6-2F-SOURCE-STEPPED-PERIMETER-RAIL"); go.transform.SetParent(t.parent, false); go.layer = t.gameObject.layer;
                var world = recipe["new_mesh"]["vertices_world"].Select(v => new Vector3((float)v[0], (float)v[1], (float)v[2])).ToArray();
                var rail = new Mesh { name = go.name }; rail.vertices = world.Select(go.transform.InverseTransformPoint).ToArray(); rail.triangles = recipe["new_mesh"]["triangles"].Select(v => (int)v).ToArray();
                Need(rail.vertexCount == 20 && rail.triangles.Length == 108, "Coherent swept profile changed");
                var local = rail.vertices; var indices = rail.triangles; var edges = new Dictionary<(Vector3,Vector3),int>();
                for (int i = 0; i < indices.Length; i += 3) { var a=local[indices[i]]; var b=local[indices[i+1]]; var c=local[indices[i+2]]; Need(Vector3.Cross(b-a,c-a).sqrMagnitude>0,"Degenerate rail face"); foreach(var edge in new[]{(a,b),(b,c),(c,a)}) edges[edge]=edges.TryGetValue(edge,out int n)?n+1:1; }
                Need(edges.All(e=>e.Value==1&&edges.TryGetValue((e.Key.Item2,e.Key.Item1),out int n)&&n==1),"Rail must remain a closed oriented profile");
                var uv = new Vector2[world.Length]; float length = 0;
                for(int i=0;i<world.Length;i++){ if(i>0 && i%4==0) length+=Vector3.Distance((world[i]+world[i+1])*.5f,(world[i-4]+world[i-3])*.5f); uv[i]=new Vector2(length,(world[i].y-world[0].y)+(i%4==1||i%4==2?.2f:0)); }
                rail.uv=uv; rail.RecalculateNormals(); rail.RecalculateBounds();
                foreach(var item in new[]{(mesh:render,name:"Native6_PlanPerimeter_Render.asset"),(mesh:collision,name:"Native6_PlanPerimeter_Collision.asset"),(mesh:rail,name:"P6-2F-SOURCE-STEPPED-PERIMETER-RAIL.asset")}) { string path=Art+"/"+item.name; Need(!File.Exists(path),"Existing rail output"); item.mesh.name=Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(item.mesh,path); created.Add(path); }
                mf.sharedMesh=render; mc.sharedMesh=collision; go.AddComponent<MeshFilter>().sharedMesh=rail; go.AddComponent<MeshRenderer>().sharedMaterial=mr.sharedMaterials[1]; var guard=go.AddComponent<MeshCollider>(); guard.cookingOptions=(MeshColliderCookingOptions)30; guard.sharedMesh=rail;
                var after=State(t); var normalized=(JObject)after.DeepClone(); foreach(string path in new[]{"filter.MeshFilter.m_Mesh","collider.MeshCollider.m_Mesh"}) normalized.SelectToken(path).Replace(before.SelectToken(path).DeepClone()); Need(JToken.DeepEquals(before,normalized),"Unrelated native6 fields changed");
                AssetDatabase.SaveAssets(); foreach(var pin in sources) Need(Hash((string)pin["path"])==(string)pin["sha256"],"Original asset modified"); Physics.SyncTransforms();
                var owner = new JObject { ["owner"] = Platform6BankB1Replay.PathOf(go.transform), ["state"] = State(go.transform), ["asset"] = Pin(rail) };
                EditorSceneManager.MarkSceneDirty(scene); Need(EditorSceneManager.SaveScene(scene),"Rail save failed");
                var receipt = new JObject { ["schema"]="chooguard.concourse-perimeter-rail.v1",["status"]="INSTALLED_REFERENCE_GEOMETRY_RUNTIME_PENDING",["request"]=args[0],["request_sha256"]=Hash(args[0]),["before_scene_sha256"]=request["scene_sha256"],["scene_sha256"]=Hash(scene.path),["source_assets"]=sources,["outputs"]=new JArray(Pin(render),Pin(collision),Pin(rail)),["native6_before"]=before,["native6_after"]=after,["added_rail_owner"]=owner,["source_profile"]=recipe["profile_in_current_model_frame"],["source_centerline_px"]=recipe["target_source_centerline_px"],["retired_original_faces_per_role"]=14,["all_unselected_triangles_and_original_vertex_channels_unchanged"]=true,["new_rail_UV_reconstructed"]=true,["site_accuracy_verified"]=false,["whole_building_complete"]=false };
                var registry=JObject.Parse(File.ReadAllText(Work+"/registry-before.json")); registry["scene_sha256"]=receipt["scene_sha256"];registry["native6_after"]=after;
                foreach(var pin in receipt["outputs"])((JArray)registry["outputs"]).Add(pin.DeepClone());
                registry["added_perimeter_rail_owner"]=owner; registry["perimeter_rail_correction_receipt"]=Receipt;
                File.WriteAllText(ConcourseFidelityCorrection.Registry,registry.ToString()); File.WriteAllText(Receipt,receipt.ToString()); File.WriteAllText(Work+"/receipt.json",receipt.ToString());
                ConcourseFidelityCorrection.Verify(Array.Empty<string>()); return "SAVED_SOURCE_PERIMETER_RAIL "+Hash(scene.path);
            }
            catch
            {
                mf.sharedMesh=oldRender;mc.sharedMesh=oldCollision;if(go!=null)UnityEngine.Object.DestroyImmediate(go);foreach(string p in created)AssetDatabase.DeleteAsset(p);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.Copy(Work+"/FpsStation-before.unity",scene.path,true);File.Copy(Work+"/registry-before.json",ConcourseFidelityCorrection.Registry,true);if(File.Exists(Receipt))File.Delete(Receipt);EditorSceneManager.OpenScene(scene.path,OpenSceneMode.Single);throw;
            }
        }
    }
}
#endif
