#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using ChooGuard.App.Fps.Facilities;

namespace ChooGuard.Editor.Twin
{
    // Appearance overrides are keyed by the existing builder asset and exact scene owner.
    // Original recipe names, source assets, native transforms and operation bindings stay intact.
    public static class ObservedFacadeRegistry
    {
        public const string Registry = "content/world/observed-interiors/observed-facades.json";
        public const string Schema = "chooguard.observed-facades.v1";
        public static string Digest(string path)
        {
            using(var file=File.OpenRead(path)) using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        public static string PathOf(Transform transform)
        {
            var names=new List<string>();
            for(var t=transform;t!=null;t=t.parent) names.Add(t.name);
            names.Reverse(); return string.Join("/",names);
        }
        public static JArray Matrix(Matrix4x4 m) => new JArray(Enumerable.Range(0,4)
            .Select(r=>new JArray(Enumerable.Range(0,4).Select(c=>m[r,c]))));
        public static void Frame(Matrix4x4 actual,JToken expected)
        {
            if(!(expected is JArray rows)||rows.Count!=4) throw new InvalidDataException("Facade matrix missing");
            for(int r=0;r<4;r++) for(int c=0;c<4;c++)
            {
                float e=(float)expected[r][c];
                if(!float.IsFinite(actual[r,c])||!float.IsFinite(e)||Mathf.Abs(actual[r,c]-e)>.00002f)
                    throw new InvalidDataException("Facade owner frame changed");
            }
        }
        public static JObject Read()
        {
            if(!File.Exists(Registry)) return new JObject { ["schema"]=Schema,["meshes"]=new JArray(),["texts"]=new JArray(),["doors"]=new JArray() };
            var d=JObject.Parse(File.ReadAllText(Registry));
            if((string)d["schema"]!=Schema || (bool?)d["site_accuracy_verified"]!=false)
                throw new InvalidDataException("Observed facade registry contract changed");
            if(!File.Exists((string)d["source_plan"])||Digest((string)d["source_plan"])!=(string)d["source_plan_sha256"])
                throw new InvalidDataException("Observed facade source plan changed");
            return d;
        }
        public static bool HasCorrection(string asset) => ((JArray)Read()["meshes"]).Any(x=>(string)x["source_asset"]==asset);
        public static string[] SourceAssetsForRoot(string root) => ((JArray)Read()["meshes"])
            .Where(x=>((string)x["owner"]).StartsWith(root.Trim('/')+"/",StringComparison.Ordinal))
            .Select(x=>(string)x["source_asset"]).Distinct().ToArray();
        public static Mesh CheckedMesh(string path,string guid,string digest,string fileDigest)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null || AssetDatabase.AssetPathToGUID(path)!=guid || !File.Exists(path) || Digest(path)!=fileDigest ||
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh)!=digest)
                throw new InvalidDataException("Pinned facade mesh changed: "+path);
            return mesh;
        }
        public static Material CheckedMaterial(JToken row,string prefix)
        {
            string p=(string)row[prefix+"_asset"];
            var m=AssetDatabase.LoadAssetAtPath<Material>(p);
            if(m==null || AssetDatabase.AssetPathToGUID(p)!=(string)row[prefix+"_guid"] || !File.Exists(p) ||
                Digest(p)!=(string)row[prefix+"_file_sha256"])
                throw new InvalidDataException("Pinned scoped facade material changed");
            return m;
        }
        static Mesh Baseline(JToken row)
        {
            CheckedMaterial(row,"source_material");
            return CheckedMesh((string)row["baseline_asset"],(string)row["baseline_guid"],
                (string)row["baseline_digest"],(string)row["baseline_file_sha256"]);
        }
        static JToken Single(JArray rows,Func<JToken,bool> filter)
        {
            var a=rows.Where(filter).ToArray();
            if(a.Length>1) throw new InvalidDataException("Ambiguous observed facade owner");
            return a.SingleOrDefault();
        }
        // Input must already be resolved through architectural and guide registries.
        // Returning input for material-only rows never leaks a transient mesh into the scene.
        public static Mesh ResolveForRebuild(Mesh generated,string asset,string owner,Matrix4x4 frame)
        {
            var row=Single((JArray)Read()["meshes"],x=>(string)x["source_asset"]==asset);
            if(row==null) return generated;
            if(owner!=(string)row["owner"]) throw new InvalidDataException("Facade rebuild owner changed");
            Frame(frame,row["world_matrix"]); var baseline=Baseline(row);
            if(generated==null || ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)!=(string)row["baseline_digest"])
                throw new InvalidDataException("Fresh facade source channels changed");
            if(generated.indexFormat!=baseline.indexFormat||!generated.GetVertexAttributes().SequenceEqual(baseline.GetVertexAttributes())||
                Vector3.Distance(generated.bounds.center,baseline.bounds.center)>.00002f||
                Vector3.Distance(generated.bounds.size,baseline.bounds.size)>.00002f||generated.blendShapeCount!=0||
                generated.bindposes.Length!=0||generated.boneWeights.Length!=0)
                throw new InvalidDataException("Fresh facade source layout or bounds changed");
            if(row["replacement_mesh_asset"]==null) return generated;
            return CheckedMesh((string)row["replacement_mesh_asset"],(string)row["replacement_mesh_guid"],
                (string)row["replacement_mesh_digest"],(string)row["replacement_mesh_file_sha256"]);
        }
        static void CheckPhoto(JToken row)
        {
            string p=(string)row["texture_asset"];
            if(!File.Exists((string)row["source_artifact"]) || Digest((string)row["source_artifact"])!=(string)row["photo_sha256"] ||
                !File.Exists(p)||Digest(p)!=(string)row["photo_sha256"] || AssetDatabase.AssetPathToGUID(p)!=(string)row["texture_guid"])
                throw new InvalidDataException("Original photographed sign bytes changed");
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(p);var ti=AssetImporter.GetAtPath(p) as TextureImporter;
            if(tex==null||tex.width!=(int)row["image_width"]||tex.height!=(int)row["image_height"]||ti==null||
                ti.wrapMode!=TextureWrapMode.Clamp||ti.mipmapEnabled||ti.textureCompression!=TextureImporterCompression.Uncompressed||
                ti.npotScale!=TextureImporterNPOTScale.None||!ti.sRGBTexture||ti.filterMode!=FilterMode.Bilinear)
                throw new InvalidDataException("Photograph import calibration changed");
            CheckedMaterial(row,"material");
            var material=AssetDatabase.LoadAssetAtPath<Material>((string)row["material_asset"]);
            if(material.mainTexture!=tex) throw new InvalidDataException("Photograph material binding changed");
            CheckedMesh((string)row["mesh_asset"],(string)row["mesh_guid"],(string)row["mesh_digest"],(string)row["mesh_file_sha256"]);
        }
        static void RestorePhoto(Transform parent,JToken row)
        {
            if(row==null) return;
            CheckPhoto(row);
            string name=(string)row["child_name"];
            var matches=Enumerable.Range(0,parent.childCount).Select(parent.GetChild).Where(t=>t.name==name).ToArray();
            if(matches.Length>1) throw new InvalidDataException("Duplicate photographed sign child");
            Transform child;
            if(matches.Length==0)
            {
                child=new GameObject(name).transform;child.SetParent(parent,false);
                child.gameObject.AddComponent<MeshFilter>();child.gameObject.AddComponent<MeshRenderer>();
                Undo.RegisterCreatedObjectUndo(child.gameObject,"Restore photographed station sign");
            }
            else child=matches[0];
            if(child.GetComponentsInChildren<Collider>(true).Length!=0) throw new InvalidDataException("Sign texture cannot own a collider");
            if(child.GetComponent<MeshFilter>()==null||child.GetComponent<MeshRenderer>()==null)
                throw new InvalidDataException("Photographic sign child components changed");
            child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;
            child.localScale=row["local_scale"].ToObject<Vector3>();
            child.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["mesh_asset"]);
            var r=child.GetComponent<MeshRenderer>();r.sharedMaterial=CheckedMaterial(row,"material");
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            GameObjectUtility.SetStaticEditorFlags(child.gameObject,StaticEditorFlags.BatchingStatic);
        }
        public static void ApplyAppearance(MeshFilter filter,string asset)
        {
            if(filter==null) throw new InvalidDataException("Facade filter missing");
            var row=Single((JArray)Read()["meshes"],x=>(string)x["source_asset"]==asset);
            if(row==null) return;
            if(PathOf(filter.transform)!=(string)row["owner"]) throw new InvalidDataException("Facade appearance owner changed");
            Frame(filter.transform.localToWorldMatrix,row["world_matrix"]);Baseline(row);
            if(filter.GetComponents<Collider>().Length!=0) throw new InvalidDataException("Fascia cannot replace collision geometry");
            string expected=(string)row["replacement_mesh_digest"]??(string)row["baseline_digest"];
            if(ArchitecturalCorrectionRegistry.CompleteChannelDigest(filter.sharedMesh)!=expected)
                throw new InvalidDataException("Assigned facade mesh changed");
            var renderer=filter.GetComponent<MeshRenderer>();
            if(renderer==null) throw new InvalidDataException("Facade renderer missing");
            if(row["replacement_mesh_asset"]!=null)
                renderer.sharedMaterials=new[]{CheckedMaterial(row,"source_material"),CheckedMaterial(row,"replacement_material")};
            else renderer.sharedMaterial=CheckedMaterial(row,"replacement_material");
            RestorePhoto(filter.transform,row["photo"]);
        }
        static void TextPose(TextMesh t,JToken row,bool installed)
        {
            Frame(Matrix4x4.TRS(t.transform.localPosition,t.transform.localRotation,Vector3.one),row["local_pose_without_scale"]);
            Vector3 e=row[installed?"replacement_scale":"baseline_scale"].ToObject<Vector3>();
            if(Vector3.Distance(t.transform.localScale,e)>.00002f) throw new InvalidDataException("Observed sign scale changed");
        }
        public static void ApplyText(TextMesh t,float width,float height,float padW,float padH)
        {
            var row=Single((JArray)Read()["texts"],x=>(string)x["owner"]==PathOf(t.transform));
            if(row==null) return;
            if(Mathf.Abs(width-(float)row["width"])>.00002f||Mathf.Abs(height-(float)row["height"])>.00002f||
                Mathf.Abs(padW-(float)row["pad_width"])>.00002f||Mathf.Abs(padH-(float)row["pad_height"])>.00002f)
                throw new InvalidDataException("Observed sign board dimensions changed");
            bool installed=t.text==(string)row["replacement_text"];
            if(!installed&&t.text!=(string)row["expected_text"]) throw new InvalidDataException("Observed sign source text changed");
            TextPose(t,row,installed); t.text=(string)row["replacement_text"];
            t.font.RequestCharactersInTexture(t.text,t.fontSize,t.fontStyle);
            var renderer=t.GetComponent<MeshRenderer>();var natural=renderer.localBounds.size;
            if(!float.IsFinite(natural.x)||!float.IsFinite(natural.y)||natural.x<=0||natural.y<=0)
                throw new InvalidDataException("Updated sign font produced empty geometry");
            t.transform.localScale=Vector3.one*Mathf.Min((width-padW)/natural.x,(height-padH)/natural.y);
            TextPose(t,row,true);
            renderer.enabled=row["photo"]==null;
            RestorePhoto(t.transform,row["photo"]);
        }
        public static void ApplyDoorDisplay(StationDoor door)
        {
            var row=Single((JArray)Read()["doors"],x=>(string)x["element"]==door.Element);
            if(row==null) return;
            if(door.Label!=(string)row["expected_label"]&&door.Label!=(string)row["replacement_label"])
                throw new InvalidDataException("Observed tenant door source label changed");
            door.Label=(string)row["replacement_label"];
        }
        public static string NavigationLabel(string id,string label)
        {
            var row=Single(Read()["point_displays"] as JArray??new JArray(),x=>(string)x["id"]==id);
            if(row==null)return label;
            if(label!=(string)row["expected_label"]&&label!=(string)row["replacement_label"])
                throw new InvalidDataException("Observed navigation display source label changed");
            return (string)row["replacement_label"];
        }
        // Read-only production verification. This is not a site-measurement receipt.
        public static JObject VerifyScene()
        {
            var d=Read();int meshes=0,texts=0,doors=0,photos=0;
            void Photo(Transform parent,JToken row)
            {
                if(row==null)return;CheckPhoto(row);var child=parent.Find((string)row["child_name"]);
                if(child==null||child.GetComponentsInChildren<Collider>(true).Length!=0||child.localPosition!=Vector3.zero||child.localRotation!=Quaternion.identity)
                    throw new InvalidDataException("Photograph child missing or transformed");
                if(Vector3.Distance(child.localScale,row["local_scale"].ToObject<Vector3>())>.00002f||
                    AssetDatabase.GetAssetPath(child.GetComponent<MeshFilter>().sharedMesh)!=(string)row["mesh_asset"]||
                    child.GetComponent<MeshRenderer>().sharedMaterial!=CheckedMaterial(row,"material")||!child.GetComponent<MeshRenderer>().enabled)
                    throw new InvalidDataException("Photograph child binding changed");photos++;
            }
            foreach(var row in (JArray)d["meshes"])
            {
                var owners=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(f=>PathOf(f.transform)==(string)row["owner"]).ToArray();
                if(owners.Length!=1)throw new InvalidDataException("Observed fascia owner count changed");
                var f=owners[0];Baseline(row);Frame(f.transform.localToWorldMatrix,row["world_matrix"]);
                if(f.GetComponents<Collider>().Length!=0||ArchitecturalCorrectionRegistry.CompleteChannelDigest(f.sharedMesh)!=
                    ((string)row["replacement_mesh_digest"]??(string)row["baseline_digest"]))throw new InvalidDataException("Observed fascia geometry changed");
                var expected=row["replacement_mesh_asset"]==null?new[]{CheckedMaterial(row,"replacement_material")}:
                    new[]{CheckedMaterial(row,"source_material"),CheckedMaterial(row,"replacement_material")};
                if(!f.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(expected))throw new InvalidDataException("Scoped fascia materials changed");
                Photo(f.transform,row["photo"]);meshes++;
            }
            foreach(var row in (JArray)d["texts"])
            {
                var a=UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(t=>PathOf(t.transform)==(string)row["owner"]).ToArray();
                if(a.Length!=1||a[0].text!=(string)row["replacement_text"])throw new InvalidDataException("Observed tenant text changed");
                TextPose(a[0],row,true);if(a[0].GetComponent<MeshRenderer>().enabled!=(row["photo"]==null))throw new InvalidDataException("Photographic sign visibility changed");
                Photo(a[0].transform,row["photo"]);texts++;
            }
            foreach(var row in (JArray)d["doors"])
            {
                var a=UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(t=>t.Element==(string)row["element"]).ToArray();
                if(a.Length!=1||a[0].Label!=(string)row["replacement_label"])throw new InvalidDataException("Observed tenant door label changed");doors++;
            }
            foreach(var row in d["point_displays"] as JArray??new JArray())
            {
                var points=(JArray)JObject.Parse(File.ReadAllText((string)row["station_points_asset"]))["points"];
                var point=(JObject)Single(points,p=>(string)p["id"]==(string)row["id"]);
                if(point==null||(string)point["label"]!=(string)row["replacement_label"])
                    throw new InvalidDataException("Observed navigation place label changed");
                var fields=(JObject)point.DeepClone();fields.Remove("label");
                if(!JToken.DeepEquals(fields,row["preserved_fields"]))
                    throw new InvalidDataException("Observed place display must retain its source ID and position");
            }
            return new JObject{["schema"]="chooguard.observed-facade-verification.v1",["meshes"]=meshes,["texts"]=texts,["door_labels"]=doors,
                ["photo_planes"]=photos,["verified_utc"]=DateTime.UtcNow.ToString("o"),["site_accuracy_verified"]=false};
        }
    }
}
#endif
