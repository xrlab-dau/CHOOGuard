#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Keep installed photographic structure corrections when the matching source kit is regenerated.</summary>
    public static class ArchitecturalCorrectionRegistry
    {
        public const string Registry = "content/world/observed-interiors/structural-corrections.json";
        static string PathOf(Transform transform)
        {
            var names = new System.Collections.Generic.List<string>();
            while (transform != null) { names.Add(transform.name); transform = transform.parent; }
            names.Reverse(); return string.Join("/", names);
        }
        static JArray Matrix(Matrix4x4 matrix)
        {
            var rows = new JArray();
            for (int r = 0; r < 4; r++) { var row = new JArray(); for (int c = 0; c < 4; c++) row.Add(matrix[r, c]); rows.Add(row); }
            return rows;
        }
        public static string ChannelDigest(Mesh mesh)
        {
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(InteriorEnvelopeRepair.MeshDigest(mesh));
                    var uv = mesh.uv; var tangents = mesh.tangents; writer.Write(uv.Length); writer.Write(tangents.Length);
                    foreach (var p in uv) { writer.Write(p.x); writer.Write(p.y); }
                    foreach (var p in tangents) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); }
                }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(memory.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public static string CompleteChannelDigest(Mesh mesh)
        {
            using(var memory=new MemoryStream())
            {
                using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))
                {
                    writer.Write(ChannelDigest(mesh));writer.Write(mesh.subMeshCount);
                    for(int s=0;s<mesh.subMeshCount;s++){writer.Write((int)mesh.GetTopology(s));var indices=mesh.GetIndices(s);writer.Write(indices.Length);foreach(int i in indices)writer.Write(i);}
                    bool color=mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color);writer.Write(color);
                    if(color)writer.Write((int)mesh.GetVertexAttributeFormat(UnityEngine.Rendering.VertexAttribute.Color));
                    var colors=mesh.colors;writer.Write(colors.Length);foreach(var c in colors){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                    for(int u=0;u<8;u++)
                    {
                        var values=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(u,values);
                        writer.Write(values.Count);writer.Write(values.Count==0?0:mesh.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+u)));
                        foreach(var v in values){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(v.w);}
                    }
                }
                using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(memory.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        static JObject Read() => File.Exists(Registry) ? JObject.Parse(File.ReadAllText(Registry)) :
            new JObject { ["schema"] = "chooguard.saved-architectural-corrections.v1", ["site_accuracy_verified"] = false, ["entries"] = new JArray() };
        // AssetDatabase can return decomposed Korean paths on macOS while the
        // generator constructs the same path from composed JSON zone names.
        static string SourceKey(string asset) => asset?.Normalize(System.Text.NormalizationForm.FormC);
        static bool SameSource(string a,string b) => string.Equals(SourceKey(a),SourceKey(b),StringComparison.Ordinal);
        public static bool HasCorrection(string originalAsset) => originalAsset != null &&
            ((JArray)Read()["entries"]).Any(row => SameSource((string)row["source_asset"],originalAsset));
        public static string[] SourceAssetsForRoot(string ownerRoot) => ((JArray)Read()["entries"])
            .Where(row => ((string)row["owner"]).StartsWith(ownerRoot.Trim('/') + "/", StringComparison.Ordinal))
            .Select(Main1FPostGuideWallReplay.SourceForEnumeration).Select(SourceKey).ToArray();
        static string FileDigest(string path)
        {
            using(var sha=SHA256.Create()) using(var file=File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        static bool ProvenRebuildVariant(JToken row,string digest)
        {
            if (!(row["verified_rebuild_variants"] is JArray variants)) return false;
            foreach(var variant in variants.Where(v => (string)v["channels_sha256"] == digest))
            {
                string path=(string)variant["certificate"];
                if(!File.Exists(path)||FileDigest(path)!=(string)variant["certificate_sha256"])continue;
                var proof=JObject.Parse(File.ReadAllText(path));
                var original=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["source_asset"]);
                if ((string)proof["status"] == "ATTRIBUTED_NATIVE_SURFACES_EQUIVALENT" &&
                    (bool?)proof["exact_position_uv_oriented_triangle_multiset_equal"] == true &&
                    (string)proof["original_channels_sha256"] == (string)row["source_channels_sha256"] &&
                    (string)proof["rebuilt_channels_sha256"] == digest &&
                    (double)proof["maximum_corner_normal_difference"] == 0 && (double)proof["maximum_corner_tangent_difference"] == 0 &&
                    original != null && ChannelDigest(original) == (string)row["source_channels_sha256"])
                    return true;
            }
            return false;
        }
        public static Mesh ResolveForRebuild(Mesh generated, string originalAsset, string owner, Matrix4x4 worldMatrix)
        {
            if (Floor1ElectricalRearWallRegistry.HasNativeCorrection(originalAsset)) return Floor1ElectricalRearWallRegistry.ResolveOrRejectBeforeNativeReplacement(generated, originalAsset, owner, worldMatrix);
            if (StairInfillOmissionRegistry.HasCorrection(originalAsset)) return StairInfillOmissionRegistry.ResolveForRebuild(generated, originalAsset, owner, worldMatrix);
            Mesh installedComposite;
            if (NamcheonSourceFrontRegistry.TryResolveInstalled(generated, originalAsset, out installedComposite)) return installedComposite;
            var matches = ((JArray)Read()["entries"]).Where(row => SameSource((string)row["source_asset"],originalAsset)).ToArray();
            if (matches.Length == 0) return NamcheonSourceFrontRegistry.VerifyArchitecturalIntermediate(generated, originalAsset);
            if (matches.Length != 1) throw new InvalidDataException("Ambiguous canonical structural correction");
            var row = matches[0];
            string digest = ChannelDigest(generated);
            if ((string)row["owner"] != owner || (digest != (string)row["source_channels_sha256"] && !ProvenRebuildVariant(row,digest)))
                throw new InvalidDataException("Rebuilt source differs from photographed structural correction: " + owner);
            if(row["source_complete_channels_sha256"]!=null&&CompleteChannelDigest(generated)!=(string)row["source_complete_channels_sha256"])
                throw new InvalidDataException("Rebuilt complete source attributes changed");
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                if (float.IsNaN(worldMatrix[r, c]) || float.IsInfinity(worldMatrix[r, c]) ||
                    Mathf.Abs(worldMatrix[r, c] - (float)row["world_matrix"][r][c]) > .00002f)
                    throw new InvalidDataException("Rebuilt structural correction frame changed");
            string path = (string)row["replacement_asset"];
            var replacement = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (replacement == null || AssetDatabase.AssetPathToGUID(path) != (string)row["replacement_guid"] ||
                ChannelDigest(replacement) != (string)row["replacement_channels_sha256"])
                throw new InvalidDataException("Saved structural correction asset changed or missing");
            if(row["replacement_complete_channels_sha256"]!=null&&CompleteChannelDigest(replacement)!=(string)row["replacement_complete_channels_sha256"])
                throw new InvalidDataException("Saved complete replacement attributes changed");
            UpperRetiredSurfaceCollisionGuard.ValidateResolved(row, replacement);
            var required = row["companions"] as JArray ?? new JArray();
            if (row["companion_owner"] != null) required.Add(new JObject { ["owner"] = row["companion_owner"],
                ["asset"] = row["companion_asset"], ["channels_sha256"] = row["companion_channels_sha256"] });
            foreach (var dependency in required)
            {
                var companions = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(f => PathOf(f.transform) == (string)dependency["owner"]).ToArray();
                if (companions.Length != 1 || !companions[0].gameObject.activeInHierarchy || companions[0].GetComponent<MeshRenderer>() == null ||
                    !companions[0].GetComponent<MeshRenderer>().enabled || AssetDatabase.GetAssetPath(companions[0].sharedMesh) != (string)dependency["asset"] ||
                    ChannelDigest(companions[0].sharedMesh) != (string)dependency["channels_sha256"])
                    throw new InvalidDataException("Observed replacement surface must exist beside the rebuilt source");
                var companionMatrix = dependency["world_matrix"] ?? row["world_matrix"];
                for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                    if (!float.IsFinite(companions[0].transform.localToWorldMatrix[r,c]) || !float.IsFinite((float)companionMatrix[r][c]) ||
                        Mathf.Abs(companions[0].transform.localToWorldMatrix[r,c] - (float)companionMatrix[r][c]) > .00002f)
                        throw new InvalidDataException("Observed replacement surface frame changed");
                if(dependency["complete_channels_sha256"]!=null&&CompleteChannelDigest(companions[0].sharedMesh)!=(string)dependency["complete_channels_sha256"])
                    throw new InvalidDataException("Observed complete surface attributes changed");
                if (dependency["materials"] is JArray materials)
                {
                    var actual = companions[0].GetComponent<MeshRenderer>().sharedMaterials;
                    if (actual.Length != materials.Count) throw new InvalidDataException("Observed surface materials changed");
                    for (int i=0;i<actual.Length;i++)
                        if (AssetDatabase.GetAssetPath(actual[i]) != (string)materials[i]["asset"] ||
                            AssetDatabase.AssetPathToGUID((string)materials[i]["asset"]) != (string)materials[i]["guid"])
                            throw new InvalidDataException("Observed surface material identity changed");
                }
            }
            foreach(var dependency in row["collider_companions"] as JArray ?? new JArray())
            {
                var colliderMatches=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(c=>PathOf(c.transform)==(string)dependency["owner"]).ToArray();
                if(colliderMatches.Length!=1||!colliderMatches[0].gameObject.activeInHierarchy||!colliderMatches[0].enabled||colliderMatches[0].isTrigger!=(bool)dependency["is_trigger"]||
                    colliderMatches[0].convex!=(bool)dependency["convex"]||colliderMatches[0].gameObject.layer!=(int)dependency["layer"]||colliderMatches[0].sharedMesh==null||
                    AssetDatabase.GetAssetPath(colliderMatches[0].sharedMesh)!=(string)dependency["asset"]||
                    AssetDatabase.AssetPathToGUID((string)dependency["asset"])!=(string)dependency["guid"]||
                    CompleteChannelDigest(colliderMatches[0].sharedMesh)!=(string)dependency["complete_channels_sha256"])
                    throw new InvalidDataException("Observed fixed boundary collision changed or missing");
                var matrix=colliderMatches[0].transform.localToWorldMatrix;
                for(int r=0;r<4;r++)for(int c=0;c<4;c++)
                    if(!float.IsFinite(matrix[r,c])||!float.IsFinite((float)dependency["world_matrix"][r][c])||Mathf.Abs(matrix[r,c]-(float)dependency["world_matrix"][r][c])>.00002f)
                        throw new InvalidDataException("Observed fixed collision frame changed");
            }
            return NamcheonSourceFrontRegistry.VerifyArchitecturalIntermediate(replacement, originalAsset);
        }
        // Opt-in replay of directly attached, saved derived surfaces. Validate the existing
        // source correction before calling this during a batch rebuild; never infer geometry.
        public static void RestoreDerivedChildrenForRebuild(MeshFilter target, string sourceAsset)
        {
            if (StairInfillOmissionRegistry.HasCorrection(sourceAsset)) { StairInfillOmissionRegistry.RestoreCompanionsForRebuild(target, sourceAsset); return; }
            var row=((JArray)Read()["entries"]).SingleOrDefault(r=>SameSource((string)r["source_asset"],sourceAsset));
            if((string)row?["rebuild_companion_policy"]!="preserve_exact_derived_children")return;
            if(target==null||PathOf(target.transform)!=(string)row["owner"]||
                AssetDatabase.GetAssetPath(target.sharedMesh)!=(string)row["replacement_asset"]||
                CompleteChannelDigest(target.sharedMesh)!=(string)row["replacement_complete_channels_sha256"])
                throw new InvalidDataException("Derived surface rebuild owner changed");
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)
                if(!float.IsFinite(target.transform.localToWorldMatrix[r,c])||!float.IsFinite((float)row["world_matrix"][r][c])||
                    Mathf.Abs(target.transform.localToWorldMatrix[r,c]-(float)row["world_matrix"][r][c])>.00002f)
                    throw new InvalidDataException("Derived surface rebuild frame changed");
            var plans=new System.Collections.Generic.List<(string name,Mesh mesh,Material[] materials,JToken proof)>();
            foreach(var dependency in (JArray)row["companions"])
            {
                string prefix=(string)row["owner"]+"/",owner=(string)dependency["owner"];
                if(!owner.StartsWith(prefix,StringComparison.Ordinal)||owner.Substring(prefix.Length).Contains("/"))
                    throw new InvalidDataException("Only direct derived surface children can be replayed");
                string name=owner.Substring(prefix.Length),path=(string)dependency["asset"];
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(string.IsNullOrEmpty(name)||target.transform.Find(name)!=null||mesh==null||EditorUtility.IsDirty(mesh)||
                    AssetDatabase.AssetPathToGUID(path)!=(string)dependency["guid"]||
                    CompleteChannelDigest(mesh)!=(string)dependency["complete_channels_sha256"])
                    throw new InvalidDataException("Derived surface dependency changed or duplicated");
                var materials=((JArray)dependency["materials"]).Select(m=>{
                    string materialPath=(string)m["asset"];var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null||EditorUtility.IsDirty(material)||AssetDatabase.AssetPathToGUID(materialPath)!=(string)m["guid"]||
                        FileDigest(materialPath)!=(string)m["file_sha256"])
                        throw new InvalidDataException("Derived surface material changed");return material;
                }).ToArray();
                plans.Add((name,mesh,materials,dependency));
            }
            if(plans.Select(p=>p.name).Distinct().Count()!=plans.Count)
                throw new InvalidDataException("Duplicate derived surface names in registry");
            foreach(var plan in plans)
            {
                var child=new GameObject(plan.name);child.transform.SetParent(target.transform,false);
                child.layer=(int)plan.proof["layer"];child.AddComponent<MeshFilter>().sharedMesh=plan.mesh;
                var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterials=plan.materials;
                renderer.shadowCastingMode=(UnityEngine.Rendering.ShadowCastingMode)(int)plan.proof["shadow_casting_mode"];
                renderer.receiveShadows=(bool)plan.proof["receive_shadows"];
            }
        }

        public static void PublishSavedCorrection(MeshFilter target, Mesh original, MeshFilter[] companions, string evidence, MeshCollider[] colliderCompanions=null)
        {
            if (target == null || original == null || companions == null || companions.Length == 0 || companions.Any(c => c == null) || target.gameObject.scene.isDirty ||
                EditorApplication.isCompiling || EditorApplication.isPlaying)
                throw new InvalidOperationException("A saved installed correction and companion surface are required");
            var registry = Read(); var entries = (JArray)registry["entries"];
            string sourcePath = AssetDatabase.GetAssetPath(original), replacementPath = AssetDatabase.GetAssetPath(target.sharedMesh);
            if (sourcePath == replacementPath || string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(replacementPath) ||
                companions.Any(c => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(c.sharedMesh)))) throw new InvalidDataException("Persistent distinct meshes required");
            colliderCompanions=colliderCompanions??Array.Empty<MeshCollider>();
            if(colliderCompanions.Any(c=>c==null||!c.enabled||!c.gameObject.activeInHierarchy||c.isTrigger||c.convex||c.sharedMesh==null||string.IsNullOrEmpty(AssetDatabase.GetAssetPath(c.sharedMesh))))
                throw new InvalidDataException("Persistent active fixed collision companions required");
            var row = new JObject { ["owner"] = PathOf(target.transform), ["source_asset"] = sourcePath,
                ["source_channels_sha256"] = ChannelDigest(original), ["replacement_asset"] = replacementPath,
                ["replacement_complete_channels_sha256"]=CompleteChannelDigest(target.sharedMesh),
                ["replacement_guid"] = AssetDatabase.AssetPathToGUID(replacementPath), ["replacement_channels_sha256"] = ChannelDigest(target.sharedMesh),
                ["world_matrix"] = Matrix(target.transform.localToWorldMatrix),
                ["companions"] = new JArray(companions.Select(companion => new JObject { ["owner"] = PathOf(companion.transform),
                    ["asset"] = AssetDatabase.GetAssetPath(companion.sharedMesh), ["channels_sha256"] = ChannelDigest(companion.sharedMesh),
                    ["complete_channels_sha256"]=CompleteChannelDigest(companion.sharedMesh),
                    ["materials"] = new JArray(companion.GetComponent<MeshRenderer>().sharedMaterials.Select(m => new JObject {
                        ["asset"] = AssetDatabase.GetAssetPath(m), ["guid"] = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)) })) })),
                ["collider_companions"]=new JArray(colliderCompanions.Select(c=>new JObject{["owner"]=PathOf(c.transform),["asset"]=AssetDatabase.GetAssetPath(c.sharedMesh),
                    ["guid"]=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c.sharedMesh)),["complete_channels_sha256"]=CompleteChannelDigest(c.sharedMesh),
                    ["world_matrix"]=Matrix(c.transform.localToWorldMatrix),["is_trigger"]=c.isTrigger,["convex"]=c.convex,["layer"]=c.gameObject.layer})),
                ["source_evidence"] = evidence, ["site_accuracy_verified"] = false };
            // Complete ordered source enforcement is opt-in for the new collision dependency path.
            // Legacy certified source permutations keep their existing pinned equivalence contract.
            if(colliderCompanions.Length>0)row["source_complete_channels_sha256"]=CompleteChannelDigest(original);
            foreach (var old in entries.Where(t => SameSource((string)t["source_asset"],sourcePath)).ToArray()) old.Remove(); entries.Add(row);
            registry["published_utc"] = DateTime.UtcNow.ToString("o");
            File.WriteAllText(Registry + ".pending", registry.ToString());
            if (File.Exists(Registry)) File.Replace(Registry + ".pending", Registry, null); else File.Move(Registry + ".pending", Registry);
        }
        public static string PublishInstalledFrontage()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Stable editor required");
            var receipt = JObject.Parse(File.ReadAllText(ObservedFrontageRepair.Receipt));
            if (!(bool)receipt["production_installed"]) throw new InvalidDataException("Correction has not been installed");
            var evidence = JObject.Parse(File.ReadAllText(ObservedFrontageRepair.Evidence));
            var registry = Read(); var entries = (JArray)registry["entries"];
            var filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(f => f.gameObject.scene.name == "FpsStation").ToArray();
            foreach (var installed in (JArray)receipt["owners"])
            {
                string owner = (string)installed["owner"]; var filter = filters.Single(f => PathOf(f.transform) == owner);
                var original = AssetDatabase.LoadAssetAtPath<Mesh>((string)installed["original_asset"]);
                if (InteriorEnvelopeRepair.MeshDigest(original) != (string)installed["source_mesh_sha256"] ||
                    InteriorEnvelopeRepair.MeshDigest(filter.sharedMesh) != (string)installed["replacement_mesh_sha256"] ||
                    AssetDatabase.GetAssetPath(filter.sharedMesh) != (string)installed["replacement_asset"])
                    throw new InvalidDataException("Installed correction no longer matches its source receipt");
                var source = ((JArray)evidence["faces"]).First(f => (string)f["owner"] == owner);
                for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                    if (Mathf.Abs(filter.transform.localToWorldMatrix[r, c] - (float)source["world_matrix"][r][c]) > .00002f)
                        throw new InvalidDataException("Installed correction frame changed");
                var entry = new JObject { ["owner"] = owner, ["source_asset"] = (string)installed["original_asset"],
                    ["source_channels_sha256"] = ChannelDigest(original), ["replacement_asset"] = (string)installed["replacement_asset"],
                    ["replacement_guid"] = AssetDatabase.AssetPathToGUID((string)installed["replacement_asset"]),
                    ["replacement_channels_sha256"] = ChannelDigest(filter.sharedMesh), ["world_matrix"] = Matrix(filter.transform.localToWorldMatrix),
                    ["source_video"] = "https://www.youtube.com/watch?v=3TLdCQCjeXE", ["source_views"] = new JArray(57, 61, 66, 68),
                    ["source_evidence"] = ObservedFrontageRepair.Evidence, ["site_accuracy_verified"] = false,
                    ["scope"] = "Retain the source mesh's observed-pier clearance when identical geometry is regenerated; changed geometry requires a new comparison." };
                foreach (var old in entries.Where(t => SameSource((string)t["source_asset"],(string)entry["source_asset"])).ToArray()) old.Remove();
                entries.Add(entry);
            }
            registry["published_utc"] = DateTime.UtcNow.ToString("o");
            File.WriteAllText(Registry + ".pending", registry.ToString());
            if (File.Exists(Registry)) File.Replace(Registry + ".pending", Registry, null); else File.Move(Registry + ".pending", Registry);
            return "Canonical saved structural corrections: " + entries.Count;
        }
    }
}
#endif
