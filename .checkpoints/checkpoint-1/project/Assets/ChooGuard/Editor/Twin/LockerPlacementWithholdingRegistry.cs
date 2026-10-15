#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    public static class LockerPlacementWithholdingRegistry
    {
        public const string Registry="content/world/observed-interiors/locker-placement-withholding.json";
        public const string Packet="workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/locker-withholding/locker-withholding-packet.json";
        const string PacketSha="899e420e338529b4ef9e545748604bd7e89caeb4b96ad282690d3e66657dc3c7";
        const string Root="실내 트윈 마감/2F 본관 보강";
        public static void Require(bool ok,string message){if(!ok)throw new InvalidDataException("Locker placement withholding: "+message);}
        public static string Hash(string path){Require(File.Exists(path),"missing "+path);using(var h=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        public static string Digest(Mesh mesh){Require(mesh!=null&&mesh.isReadable&&mesh.bindposes.Length==0&&mesh.boneWeights.Length==0&&mesh.blendShapeCount==0,"missing/unreviewed complete mesh");return ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh);}
        public static string PathOf(Transform t){var names=new List<string>();while(t!=null){names.Add(t.name);t=t.parent;}names.Reverse();return string.Join("/",names);}
        public static JObject ReadPacket(){Require(Hash(Packet)==PacketSha,"sealed packet changed");var p=JObject.Parse(File.ReadAllText(Packet));Require(p["meshes"].Count()==7&&(int)p["summary"]["render_triangle_removal"]==666&&(int)p["summary"]["collider_triangle_removal"]==12&&p["new_placement"].Type==JTokenType.Null,"scope changed");return p;}
        static JObject ReadInstalled(){Require(File.Exists(Registry),"installed disposition missing; regeneration would reintroduce an unsupported bank");var r=JObject.Parse(File.ReadAllText(Registry));Require((string)r["schema"]=="chooguard.locker-placement-withholding-registry.v1"&&(string)r["packet_sha256"]==PacketSha&&r["dependencies"].Count()==7,"installed disposition changed");return r;}
        static string AssetKey(string path)=>path?.Normalize(System.Text.NormalizationForm.FormC);
        static JToken Row(JObject p,string source)=>p["meshes"].SingleOrDefault(r=>AssetKey((string)r["full_channel_export"]["asset"])==AssetKey(source));
        public static void Frame(Matrix4x4 m,JToken rows){for(int r=0;r<4;r++)for(int c=0;c<4;c++)Require(float.IsFinite(m[r,c])&&float.IsFinite((float)rows[r][c])&&Mathf.Abs(m[r,c]-(float)rows[r][c])<.00004f,"owner frame changed");}
        static JToken CurrentElement(JObject p){var spec=JObject.Parse(File.ReadAllText((string)p["source_spec"]));var matches=spec["elements"].Where(e=>(string)e["id"]=="2FM-LOCKERS").ToArray();Require(matches.Length==1&&JToken.DeepEquals(matches[0],p["withheld_element"]),"source bank record lost/changed");return matches[0];}
        public static Mesh Source(JToken row)
        {
            var e=row["full_channel_export"];string path=(string)e["asset"];Require(Hash(path)==(string)row["source_asset_sha256"]&&Hash(path+".meta")==(string)row["source_meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)e["guid"],"original mesh identity changed");var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Require(m!=null&&!EditorUtility.IsDirty(m)&&Digest(m)==(string)e["complete_channel_digest"],"original complete channels changed");return m;
        }
        static Mesh Output(JToken row,JObject installed)
        {
            var matches=installed["dependencies"].Where(d=>(string)d["role"]==(string)row["role"]).ToArray();Require(matches.Length==1,"installed output missing/duplicate");var d=matches[0];string path=(string)d["asset"];Require(path=="Assets/ChooGuard/Art/StationInterior/LockerPlacementWithholding20261009/"+(string)row["role"]+".asset"&&Hash(path)==(string)d["file_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)d["guid"]&&(string)d["complete_channel_digest"]==(string)row["expected_output"]["expected_complete_channel_digest"],"installed output identity changed");var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Require(m!=null&&!EditorUtility.IsDirty(m)&&Digest(m)==(string)d["complete_channel_digest"],"installed complete channels changed");return m;
        }
        public static void EvidenceAndSources(JObject p)
        {
            CurrentElement(p);Require(Hash((string)p["source_metadata"])==(string)p["source_metadata_sha256"]&&Hash((string)p["source_controls"])==(string)p["source_controls_sha256"],"source evidence changed");
            foreach(var photo in p["photographic_support"])Require(Hash((string)photo["path"])==(string)photo["sha256"],"original clear-approach photograph changed");
            var capture=p["current_render_contradiction"];Require(Hash((string)capture["path"])==(string)capture["sha256"],"reviewed obstruction capture changed");
            foreach(var m in p["material_dependencies"])Require(Hash((string)m["path"])==(string)m["file_sha256"]&&Hash((string)m["path"]+".meta")== (string)m["meta_sha256"]&&AssetDatabase.AssetPathToGUID((string)m["path"])==(string)m["guid"],"source material changed");
            foreach(var row in p["meshes"]){Source(row);Require(Hash((string)row["full_channel_export"]["path"])==(string)row["full_channel_export"]["binary_sha256"]&&Hash((string)row["expected_output"]["path"])==(string)row["expected_output"]["binary_sha256"],"frozen complete-channel packet changed");}
        }
        public static Bounds ReferencedBounds(Mesh m)
        {
            var indices=Enumerable.Range(0,m.subMeshCount).SelectMany(s=>m.GetIndices(s)).ToArray();if(indices.Length==0)return new Bounds(Vector3.zero,Vector3.zero);var v=m.vertices;Require(indices.All(i=>i>=0&&i<v.Length),"invalid triangle index");var b=new Bounds(v[indices[0]],Vector3.zero);foreach(int i in indices)b.Encapsulate(v[i]);return b;
        }
        public static Mesh WithheldCopy(Mesh source,JToken row)
        {
            Require(Digest(source)==(string)row["full_channel_export"]["complete_channel_digest"]&&source.subMeshCount==1&&source.GetTopology(0)==MeshTopology.Triangles,"exact baseline changed");var indices=source.GetIndices(0);var remove=new HashSet<int>(row["selected_triangle_ordinals"].Select(x=>(int)x));Require(remove.Count==(int)row["selected_triangle_count"]&&remove.All(i=>i>=0&&i<indices.Length/3),"triangle selection changed");var keep=Enumerable.Range(0,indices.Length/3).Where(i=>!remove.Contains(i)).SelectMany(i=>indices.Skip(i*3).Take(3)).ToArray();var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name+"_Withheld_2FM_LOCKERS";copy.hideFlags=HideFlags.None;
            try{copy.SetIndices(keep,MeshTopology.Triangles,0,false);copy.bounds=ReferencedBounds(copy);Require(Digest(copy)==(string)row["expected_output"]["expected_complete_channel_digest"],"expected full-channel output mismatch");return copy;}catch{UnityEngine.Object.DestroyImmediate(copy);throw;}
        }
        public static bool HasCorrection(string source){if(source==null)return false;var p=ReadPacket();if(Row(p,source)==null)return false;ReadInstalled();return true;}
        public static string[] SourceAssetsForRoot(string ownerRoot){if(ownerRoot.Trim('/')!=Root)return Array.Empty<string>();var p=ReadPacket();ReadInstalled();CurrentElement(p);return p["meshes"].Select(r=>AssetKey((string)r["full_channel_export"]["asset"])).ToArray();}
        public static string[] InstalledAssetsForRoot(string ownerRoot){if(ownerRoot.Trim('/')!=Root)return Array.Empty<string>();var p=ReadPacket();var r=ReadInstalled();return p["meshes"].Select(row=>AssetDatabase.GetAssetPath(Output(row,r))).Concat(TicketWindowSillReplayRegistry.InstalledAssets()).Distinct().ToArray();}
        public static bool TryResolveInstalled(Mesh candidate,string source,out Mesh resolved)
        {
            resolved=null;if(TicketWindowSillReplayRegistry.TryResolveInstalled(candidate,source,out resolved))return true;var p=ReadPacket();var row=Row(p,source);if(row==null)return false;Source(row);var target=Output(row,ReadInstalled());if(candidate==null||AssetDatabase.GetAssetPath(candidate)!=AssetDatabase.GetAssetPath(target))return false;Require(Digest(candidate)==Digest(target),"saved candidate changed");resolved=target;return true;
        }
        public static Mesh VerifyCompositeBaseline(Mesh fresh,string source)
        {
            Mesh ticket;if(TicketWindowSillReplayRegistry.TryResolveForRebuild(fresh,source,out ticket))return fresh;var p=ReadPacket();var row=Row(p,source);if(row==null)return fresh;CurrentElement(p);var original=Source(row);ReadInstalled();Require(Digest(fresh)==(string)row["full_channel_export"]["complete_channel_digest"]&&fresh.indexFormat==original.indexFormat&&fresh.GetVertexAttributes().SequenceEqual(original.GetVertexAttributes())&&Vector3.Distance(fresh.bounds.center,original.bounds.center)<.00004f&&Vector3.Distance(fresh.bounds.size,original.bounds.size)<.00004f,"fresh generated complete baseline/layout changed; ordinal remapping is forbidden");return fresh;
        }
        public static Mesh ResolveComposedForRebuild(Mesh fresh,string source){Mesh ticket;if(TicketWindowSillReplayRegistry.TryResolveForRebuild(fresh,source,out ticket))return ticket;var p=ReadPacket();var row=Row(p,source);if(row==null)return fresh;Mesh saved;if(TryResolveInstalled(fresh,source,out saved))return saved;VerifyCompositeBaseline(fresh,source);return Output(row,ReadInstalled());}
        public static Mesh ResolveForRebuild(Mesh fresh,string source,string owner,Matrix4x4 world)
        {
            var p=ReadPacket();var row=Row(p,source);if(row==null)return fresh;Require(owner==(string)row["owner_receipt"]["owner"]["path"],"source owner changed");Frame(world,row["owner_receipt"]["owner"]["world_matrix"]);return ResolveComposedForRebuild(fresh,source);
        }
        public static void ApplyOwnerState(GameObject go,string source)
        {
            var p=ReadPacket();var row=Row(p,source);if(row==null)return;Require(PathOf(go.transform)==(string)row["owner_receipt"]["owner"]["path"],"rebuild owner changed");Frame(go.transform.localToWorldMatrix,row["owner_receipt"]["owner"]["world_matrix"]);if((int)row["owner_receipt"]["component"]["class_id"]==23){var r=go.GetComponent<MeshRenderer>();Require(r!=null,"generated renderer lost");r.enabled=!(bool)row["disable_renderer"];}
        }
        public static void WithholdGeneratedBankText(TextMesh text,string element,JObject geometry,JObject parameters)
        {
            if(element!="2FM-LOCKERS")return;var p=ReadPacket();ReadInstalled();var e=CurrentElement(p);Require(JToken.DeepEquals(geometry,e["geometry"])&&JToken.DeepEquals(parameters,e["params"]),"bank generation recipe changed");Require(text!=null&&text.text==(string)p["text_withholding"]["text"],"bank label lost/changed");var f=p["text_withholding"]["local_transform_fields"];var q=f["m_LocalPosition"];var expected=new Vector3((float)q["x"],(float)q["y"],(float)q["z"]);Require((text.transform.localPosition-expected).sqrMagnitude<1e-10f,"bank label pose changed");Frame(text.transform.parent.localToWorldMatrix,p["meshes"][0]["owner_receipt"]["owner"]["world_matrix"]);string owner=PathOf(text.transform);Require(owner==(string)p["text_withholding"]["path"]||text.transform.root.gameObject.hideFlags==HideFlags.HideAndDontSave,"bank text source root changed");text.GetComponent<MeshRenderer>().enabled=false;
        }
        public static void ValidateInstalledBeforeRebuild(string ownerRoot){if(ownerRoot.Trim('/')!=Root)return;VerifyInstalled(null);}
        public static string VerifyInstalled(string[] args)
        {
            Require(args==null||args.Length==0,"no overrides accepted");var p=ReadPacket();EvidenceAndSources(p);var installed=ReadInstalled();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).ToArray();var colliders=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).ToArray();
            foreach(var row in p["meshes"])
            {
                var proof=row["owner_receipt"];string path=(string)proof["owner"]["path"];var mesh=TicketWindowSillReplayRegistry.InstalledForWithholding((string)row["full_channel_export"]["asset"],Output(row,installed));if((int)proof["component"]["class_id"]==23)
                {
                    var fs=filters.Where(x=>PathOf(x.transform)==path).ToArray();Require(fs.Length==1,"source renderer lost/duplicated");var f=fs[0];var r=f.GetComponent<MeshRenderer>();Require(f.sharedMesh==mesh&&r!=null&&r.enabled==!(bool)row["disable_renderer"]&&r.additionalVertexStreams==null&&f.gameObject.activeInHierarchy,"withheld render binding changed");Require(r.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(proof["owner"]["materials"].Select(x=>(string)x)),"material slots changed");Frame(f.transform.localToWorldMatrix,proof["owner"]["world_matrix"]);
                }
                else
                {
                    var cs=colliders.Where(x=>PathOf(x.transform)==path).ToArray();Require(cs.Length==1,"source collision owner lost/duplicated");var c=cs[0];var f=proof["component_fields"]["MeshCollider"];Require(c.sharedMesh==mesh&&c.gameObject.activeInHierarchy&&c.enabled==((int)f["m_Enabled"]==1)&&!c.isTrigger&&!c.convex&&c.gameObject.layer==(int)proof["gameobject_layer"]&&(int)c.cookingOptions==(int)f["m_CookingOptions"]&&c.includeLayers.value==(int)f["m_IncludeLayers"]["m_Bits"]&&c.excludeLayers.value==(int)f["m_ExcludeLayers"]["m_Bits"]&&c.layerOverridePriority==(int)f["m_LayerOverridePriority"]&&c.providesContacts==((int)f["m_ProvidesContacts"]==1)&&c.sharedMaterial==null,"physical collider binding/settings changed");Frame(c.transform.localToWorldMatrix,proof["owner"]["world_matrix"]);
                }
            }
            var labels=UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene&&PathOf(x.transform)==(string)p["text_withholding"]["path"]).ToArray();Require(labels.Length==1&&labels[0].text==(string)p["text_withholding"]["text"]&&!labels[0].GetComponent<MeshRenderer>().enabled,"bank label missing/visible");return "EXACT_LOCKER_PLACEMENT_WITHHELD:666 rendered +12 collision triangles; no replacement position";
        }
    }
}
#endif
