#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using ChooGuard.App.Fps.Facilities;

namespace ChooGuard.Editor.Twin
{
    // This overrides one inferred generated operation. Original closed render/collision geometry remains intact.
    public static class Exit9DoorOperationRegistry
    {
        public const string Registry="content/world/observed-interiors/exit9-door-operation.json";
        public const string SourcePlan="content/world/observed-interiors/exit9-door-operation-source-plan.json";
        public const string Schema="chooguard.exit9-hinged-operation-registry.v1";
        public static void Require(bool value,string message) { if(!value)throw new InvalidDataException(message); }
        public static string Hash(string path)
        { using(var f=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
        public static string PathOf(Transform t)
        { var parts=new List<string>();while(t!=null){parts.Add(t.name);t=t.parent;}parts.Reverse();return string.Join("/",parts); }
        public static Vector3 V(JToken p)=>new Vector3((float)p["x"],(float)p["y"],(float)p["z"]);
        public static Quaternion Q(JToken p)=>new Quaternion((float)p["x"],(float)p["y"],(float)p["z"],(float)p["w"]);
        public static void Frame(Matrix4x4 m,JToken rows)
        {
            Require(rows is JArray a&&a.Count==4,"Missing reviewed exit9 frame");
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)Require(float.IsFinite(m[r,c])&&float.IsFinite((float)rows[r][c])&&Mathf.Abs(m[r,c]-(float)rows[r][c])<=.000025f,"Exit9 closed frame changed");
        }
        public static JObject ReadPlan()
        {
            if(!File.Exists(Registry))return null;
            var reg=JObject.Parse(File.ReadAllText(Registry));
            Require((string)reg["schema"]==Schema&&(string)reg["source_plan"]==SourcePlan&&File.Exists(SourcePlan)&&Hash(SourcePlan)==(string)reg["source_plan_sha256"],"Exit9 operation provenance changed");
            return JObject.Parse(File.ReadAllText((string)reg["source_plan"]));
        }
        static JArray AcceptedTuning(JObject plan)
        {
            JArray angles=File.Exists(Registry)?(JArray)JObject.Parse(File.ReadAllText(Registry))["accepted_swing_tuning_degrees"]:null;
            if(angles==null)angles=new JArray(((JArray)plan["leaves"]).Select(l=>(float)l["swing_tuning_degrees"]));
            Require(angles.Count==2,"Exit9 accepted angle count changed");
            for(int i=0;i<2;i++)ValidateTuning((float)angles[i],plan["leaves"][i]);
            return angles;
        }
        static void ValidateTuning(float angle,JToken row)
        {
            float cap=(float)row["swing_tuning_degrees"];
            Require(float.IsFinite(angle)&&float.IsFinite(cap)&&Mathf.Abs(cap)<=85&&Mathf.Abs(cap)>0&&angle*cap>=0&&Mathf.Abs(angle)<=Mathf.Abs(cap),"Exit9 accepted tuning exceeds the signed draft cap");
        }
        public static void Sources(JObject plan)
        {
            Require((string)plan["schema"]=="chooguard.exit9-hinged-operation-plan.v1"&&(bool?)plan["metric_width_verified"]==false&&(bool?)plan["physical_bank_count_verified"]==false&&(int)plan["model_bank_count_preserved"]==1,"Unreviewed exit9 scope");
            Require(Hash((string)plan["source_recipe"])==(string)plan["source_recipe_sha256"],"Exit9 source recipe changed");
            foreach(var s in (JArray)plan["source_evidence"])Require(Hash((string)s["path"])==(string)s["sha256"],"Exit9 photograph changed");
            Require(((JArray)plan["leaves"]).Count==2,"Only the existing two model leaves are accepted");
            foreach(var l in (JArray)plan["leaves"])
            {
                Require(Hash((string)l["mesh_asset"])==(string)l["mesh_file_sha256"]&&AssetDatabase.AssetPathToGUID((string)l["mesh_asset"])==(string)l["mesh_guid"],"Original exit9 leaf asset changed");
                foreach(var m in (JArray)l["materials"])Require(Hash((string)m["asset"])==(string)m["file_sha256"]&&AssetDatabase.AssetPathToGUID((string)m["asset"])==(string)m["guid"],"Original exit9 material changed");
            }
        }
        static void DoorFields(StationDoor d,JObject plan,bool installed)
        {
            var b=plan["baseline_operator"];
            Require(d.Element=="exit-9-doors"&&d.Index==0&&d.Use==(StationDoor.DoorUse)(int)b["Use"]&&d.Width==(float)b["Width"]&&d.Height==(float)b["Height"],"Exit9 operator identity/dimensions changed");
            Require(Vector3.Distance(d.Centre,V(b["Centre"]))<.00001f&&Vector3.Distance(d.Normal,V(b["Normal"]))<.00001f&&Vector3.Distance(d.Along,V(b["Along"]))<.00001f,"Exit9 opening frame changed");
            Require(d.Sealed==((string)b["Sealed"]??"")&&d.Floor==((string)b["Floor"]??"")&&d.CarId==((string)b["CarId"]??"")&&d.CallPanel==null&&d.Lantern==null&&d.Cabin==null&&d.CabinSize==Vector3.zero,"Exit9 unrelated operator fields changed");
            Require(d.Kind==(installed?StationDoor.DoorKind.Swing:StationDoor.DoorKind.Sliding)&&d.Label==(installed?(string)plan["replacement_label"]:(string)b["Label"])&&d.ObservedOpen==(installed?0:(float)b["ObservedOpen"]),"Exit9 operation baseline changed");
            Frame(d.transform.localToWorldMatrix,plan["operator_world_matrix"]);
        }
        public static void Geometry(Transform g,JToken row)
        {
            Require(g!=null&&g.name==(string)row["geometry_name"]&&g.GetComponents<Collider>().Length==1,"Exit9 leaf geometry owner changed");
            Frame(g.localToWorldMatrix,row["closed_world_matrix"]);
            var f=g.GetComponent<MeshFilter>();var r=g.GetComponent<MeshRenderer>();var c=g.GetComponent<BoxCollider>();
            Require(f!=null&&r!=null&&c!=null&&AssetDatabase.GetAssetPath(f.sharedMesh)==(string)row["mesh_asset"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(f.sharedMesh)==(string)row["mesh_complete_digest"],"Exit9 original attributed surface changed");
            var materials=(JArray)row["materials"];
            Require(r.sharedMaterials.Length==materials.Count,"Exit9 material slot count changed");
            for(int i=0;i<materials.Count;i++)Require(AssetDatabase.GetAssetPath(r.sharedMaterials[i])==(string)materials[i]["asset"],"Exit9 material slot order changed");
            var fields=row["collider_fields"];var so=new SerializedObject(c);
            foreach(string key in new[]{"m_Enabled","m_IsTrigger","m_ProvidesContacts"})Require((so.FindProperty(key).boolValue?1:0)==(int)fields[key],"Exit9 collider flag changed: "+key);
            Require(so.FindProperty("m_LayerOverridePriority").intValue==(int)fields["m_LayerOverridePriority"]&&c.sharedMaterial==null,"Exit9 physical policy changed");
            foreach(string key in new[]{"m_IncludeLayers","m_ExcludeLayers"})Require(so.FindProperty(key).FindPropertyRelative("m_Bits").intValue==(int)fields[key]["m_Bits"],"Exit9 collider layer mask changed");
            Require(Vector3.Distance(c.center,V(fields["m_Center"]))<.00001f&&Vector3.Distance(c.size,V(fields["m_Size"]))<.00001f,"Exit9 original box surface changed");
        }
        public static void VerifyOriginal(StationDoor d,JObject plan)
        {
            Sources(plan);DoorFields(d,plan,false);Require(d.Leaves.Length==2&&d.transform.childCount==2,"Exit9 unexpected modeled bank/children");
            for(int i=0;i<2;i++)
            {
                var l=d.Leaves[i];var row=plan["leaves"][i];var b=plan["baseline_operator"]["Leaves"][i];
                Require(l.Pivot!=null&&l.Pivot.parent==d.transform&&l.Swing==0&&l.Length==0&&Vector3.Distance(l.Slide,V(b["Slide"]))<.00001f,"Exit9 sliding baseline changed");
                Geometry(l.Pivot,row);
            }
        }
        public static void Apply(StationDoor d,JObject plan)
        {
            VerifyOriginal(d,plan);
            var angles=AcceptedTuning(plan);
            for(int i=0;i<2;i++)
            {
                var l=d.Leaves[i];var row=plan["leaves"][i];var geometry=l.Pivot;
                var hinge=new GameObject((string)row["hinge_name"]).transform;hinge.SetParent(d.transform,false);
                hinge.gameObject.layer=geometry.gameObject.layer;hinge.gameObject.tag=geometry.gameObject.tag;
                hinge.localPosition=d.transform.InverseTransformPoint(geometry.TransformPoint(V(row["model_hinge_local"])));
                hinge.localRotation=geometry.localRotation*(i==0?Quaternion.identity:Quaternion.Euler(0,180,0));
                geometry.SetParent(hinge,true); // Exact original closed mesh/box world pose and all source channels survive.
                l.Pivot=hinge;l.Slide=Vector3.zero;l.Length=(float)row["model_leaf_length"];
                float direction=(Quaternion.Inverse(hinge.localRotation)*d.Normal).z<0?1:-1;
                Require(direction==Mathf.Sign((float)row["swing_tuning_degrees"]),"Exit9 outward hinge axis changed");
                l.Swing=(float)angles[i];
            }
            d.Kind=StationDoor.DoorKind.Swing;d.Label=(string)plan["replacement_label"];d.ObservedOpen=0;
            Physics.SyncTransforms();VerifyInstalled(d,plan);
        }
        public static void VerifyInstalled(StationDoor d,JObject plan,bool requireAcceptedTuning=true)
        {
            Sources(plan);DoorFields(d,plan,true);Require(d.Leaves.Length==2&&d.transform.childCount==2,"Exit9 modeled bank count changed");
            var angles=AcceptedTuning(plan);
            for(int i=0;i<2;i++)
            {
                var l=d.Leaves[i];var row=plan["leaves"][i];var h=l.Pivot;
                Require(h!=null&&h.parent==d.transform&&h.name==(string)row["hinge_name"]&&h.childCount==1&&h.localScale==Vector3.one&&h.GetComponents<Component>().Length==1,"Exit9 hinge hierarchy changed");
                Frame(h.localToWorldMatrix,row["hinge_closed_world_matrix"]);
                ValidateTuning(l.Swing,row);
                Require(l.Slide==Vector3.zero&&Mathf.Abs(l.Length-(float)row["model_leaf_length"])<.00001f&&(!requireAcceptedTuning||Mathf.Abs(l.Swing-(float)angles[i])<.00001f),"Exit9 swing tuning changed: root must explicitly accept the measured correction");
                Geometry(h.GetChild(0),row);
            }
        }
        // Explicit root-owned acceptance after clearance measurement. This records tuning, never a runtime PASS.
        public static string RecordAcceptedTuning(StationDoor door)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Accept only the closed saved/edit-mode hinge pose");
            var plan=ReadPlan();Require(plan!=null,"Exit9 registry missing");VerifyInstalled(door,plan,false);
            var reg=JObject.Parse(File.ReadAllText(Registry));reg["accepted_swing_tuning_degrees"]=new JArray(door.Leaves.Select(l=>l.Swing));
            reg["runtime_clearance_verified"]=false;reg["runtime_character_traversal_verified"]=false;
            File.WriteAllText(Registry,reg.ToString());VerifyInstalled(door,plan);return "Exit9 accepted signed tuning recorded; physical proofs remain pending.";
        }
        // Called before shared mesh/material writes. Only the source root recorded in this narrow packet participates.
        public static void PreflightRebuild(string specPath,string rootPath)
        {
            var plan=ReadPlan();if(plan==null)return;
            string owner=(string)plan["operator_path"];owner=owner.Substring(0,owner.LastIndexOf('/'));
            if(rootPath!=owner)return;
            Require(Path.GetFullPath(specPath)==Path.GetFullPath((string)plan["source_recipe"]),"Exit9 rebuild uses a different source recipe");
            Sources(plan);VerifyInstalled(Find(),plan);
        }
        public static bool IsProtectedLeafAsset(string path)
        {
            if(path==null)return false;var plan=ReadPlan();if(plan==null)return false;
            return ((JArray)plan["leaves"]).Any(l=>(string)l["mesh_asset"]==path);
        }
        // The freshly combined candidate is validated before DoorMesh can Clear() the persisted source asset.
        public static void VerifyLeafForRebuild(Mesh candidate,string path)
        {
            var plan=ReadPlan();Require(plan!=null,"Exit9 replay plan missing");
            var rows=((JArray)plan["leaves"]).Where(l=>(string)l["mesh_asset"]==path).ToArray();
            Require(rows.Length==1&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(candidate)==(string)rows[0]["mesh_complete_digest"],"Generated Exit9 leaf channels/layout differ; original source asset has not been overwritten");
        }
        // Call after generated source leaves are assembled but before SwingClearance/DoorSeal.
        public static void ApplyForRebuild(StationDoor door)
        {
            if(door.Element!="exit-9-doors"||door.Index!=0)return;
            var plan=ReadPlan();if(plan==null)return;Apply(door,plan);
        }
        public static void VerifyForRebuild(StationDoor door)
        {
            if(door.Element!="exit-9-doors"||door.Index!=0)return;
            var plan=ReadPlan();if(plan!=null)VerifyInstalled(door,plan);
        }
        public static StationDoor Find()
        {
            var a=UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(d=>d.Element=="exit-9-doors"&&d.Index==0).ToArray();
            Require(a.Length==1,"Exit9 operator missing or duplicated");return a[0];
        }
        public static string VerifyScene()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Verify closed saved exit9 state outside play mode");
            var p=ReadPlan();Require(p!=null,"Exit9 registry missing");VerifyInstalled(Find(),p);
            return "PASS: Exit9 uses two hinge parents; original closed leaf channels, material slots, collider flags and world surfaces preserved. Width/bank count remain unmeasured. Actual clearance and character traversal receipts are recorded separately in the operation registry.";
        }
    }
}
#endif
