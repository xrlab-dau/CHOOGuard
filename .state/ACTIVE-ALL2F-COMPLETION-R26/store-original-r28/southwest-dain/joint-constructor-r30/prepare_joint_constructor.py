from pathlib import Path
import json,hashlib,re
R=Path('/Users/um-yunsang/CHOOGuard');B=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/southwest-dain';W=B/'joint-constructor-r30';S=B/'constructor'
authority=B/'ROOT-JOINT-CONSTRUCTION-AUTHORITY-R30.json';a=json.loads(authority.read_text());raw=(S/'PENDING-DAIN-RETURN-SERVICE-PACKET-R29.json').read_bytes();sha=hashlib.sha256(raw).hexdigest()
if sha!='6b65be790b9c99f490ad96d26b86197dbab4b20700bd45b7d91eef416d703084' or a['supplement_packet_sha256']!=sha:raise RuntimeError('Final root authority exact packet mismatch')
p=json.loads(raw)
if len(p['objects'])!=230:raise RuntimeError('Exact230 supplement required')
frozen=W/'FROZEN-DAIN-RETURN-SERVICE-PACKET-R30.json';frozen.write_bytes(raw);frozen.chmod(0o444)
moves={'Flowers':a['allowed_exact_Flowers_transforms'],'POS':a['allowed_exact_own_POS_transform'],'authority':str(authority),'authority_sha256':hashlib.sha256(authority.read_bytes()).hexdigest()}
mp=W/'FROZEN-EXACT-12-TRANSFORMS-R30.json';mp.write_text(json.dumps(moves,ensure_ascii=False,separators=(',',':')));msha=hashlib.sha256(mp.read_bytes()).hexdigest();mp.chmod(0o444)
s=(S/'SouthwestDainFitoutR29.cs').read_text().replace('SouthwestDainFitoutR29','SouthwestJointReturnR30')
s=s.replace('Scope="C180_R29_SW_FIELD_FITOUT_LOWER_LEG_001"','Scope="C180_R30_SW_JOINT_RETURN_APPLY_001"')
s=re.sub(r' const string Packet=.*?;',f' const string Packet="{frozen}",PacketSHA="{sha}";\n const string Moves="{mp}",MovesSHA="{msha}";',s,count=1)
s=s.replace('"Assets/ChooGuard/Art/StationInterior/R29_SW_Dain/"+StoreKey','"Assets/ChooGuard/Art/StationInterior/R30_SW_Dain_JointReturn/"+StoreKey')
s=s.replace('var m=new Mesh{name="R25_"+StoreKey+"_"+Clean(key)};m.vertices=', 'var m=new Mesh{name="R30_"+StoreKey+"_"+Clean(key)};if(((JArray)spec["vertices"]).Count>65000)m.indexFormat=IndexFormat.UInt32;m.vertices=')
old='  if(GameObject.Find(ParentPath+"/"+name)!=null)throw new Exception("Ownstorealreadyexists; no automaticdelete/replay");'
new='''  var existing=GameObject.Find(ParentPath+"/"+name);if(existing==null)throw new Exception("ExistingSavedDain42RootRequired");
  if(existing.transform.Find("R30_RETURN_SOURCE_SUPPLEMENT")!=null)throw new Exception("NoJointSupplementReplay");
  if(Sha(Moves)!=MovesSHA)throw new Exception("ExactFrozen12TransformsRequired");var movements=JObject.Parse(File.ReadAllText(Moves));
  var jointAuthority=JObject.Parse(File.ReadAllText((string)movements["authority"]));if((string)jointAuthority["supplement_packet_sha256"]!=PacketSHA || (int)jointAuthority["new_own_Dain_parts"]!=230)throw new Exception("CurrentRootJointAuthorityRequired");
  if(((JArray)p["objects"]).Count!=230)throw new Exception("Exact230NewPartsRequired");
  var targets=new List<KeyValuePair<JToken,Transform>>();foreach(var x in (JArray)movements["Flowers"]){var t=ExactTarget(x,ParentPath+"/R25_flowers_SOURCE_BOUND_MODEL");targets.Add(new KeyValuePair<JToken,Transform>(x,t));}
  var posTarget=ExactTarget(movements["POS"],ParentPath+"/"+name);targets.Add(new KeyValuePair<JToken,Transform>(movements["POS"],posTarget));
  var moved=new JArray();'''
if old not in s:raise RuntimeError('Rootappend match missing')
s=s.replace(old,new)
old='''try{Guard();EnsureAssetDir(folder);PrepareImports(p,folder);BuildMaterials(p,folder);var parent=Ensure(ParentPath);var own=new GameObject(name);Undo.RegisterCreatedObjectUndo(own,"R25ownstore");own.transform.SetParent(parent,false);
   own.transform.position=new Vector3(-.34290633335f*origin.x+.93936960061f*origin.y,floor,-.93936960061f*origin.x-.34290633335f*origin.y);
   own.transform.rotation=Quaternion.LookRotation(new Vector3(.93936960061f,0,-.34290633335f),Vector3.up);own.transform.localScale=Vector3.one;
   var groups=new Dictionary<string,Transform>();groups[""]=own.transform;'''
new='''try{Guard();EnsureAssetDir(folder);PrepareImports(p,folder);BuildMaterials(p,folder);
   foreach(var pair in targets){Guard();var spec=pair.Key;var t=pair.Value;Vector3 beforePosition=t.position;Quaternion beforeRotation=t.rotation;Undo.RecordObject(t,"R30ExactSourceBoundEquipmentCorrection");
    if(spec["delta_packet_q"]!=null){var delta=spec["delta_packet_q"];float dx=(float)delta[0],dy=(float)delta[1];t.position+=new Vector3(-.34290633335f*dx+.93936960061f*dy,0,-.93936960061f*dx-.34290633335f*dy);float rotation=(float)spec["proposed_yaw"]-(float)spec["old_yaw"];if(Mathf.Abs(rotation)>.001f)t.rotation=Quaternion.AngleAxis(rotation,Vector3.up)*t.rotation;}
    else{var q=spec["proposed_q"];float x=(float)q[0],y=(float)q[1];t.position=new Vector3(-.34290633335f*x+.93936960061f*y,beforePosition.y,-.93936960061f*x-.34290633335f*y);t.rotation=existing.transform.rotation*Quaternion.Euler(0,(float)spec["proposed_yaw"],0);}
    EditorUtility.SetDirty(t);moved.Add(new JObject{["object_id"]=spec["current_object_id"].DeepClone(),["name"]=t.name,["world_position_before"]=V(beforePosition),["world_position_after"]=V(t.position),["world_rotation_before"]=V(beforeRotation.eulerAngles),["world_rotation_after"]=V(t.rotation.eulerAngles),["local_scale_unchanged"]=V(t.localScale),["source_mesh_material_children_preserved"]=true,["glyph_children_follow_actual_wrapper"]=true});
   }
   var own=existing;var supplement=new GameObject("R30_RETURN_SOURCE_SUPPLEMENT");Undo.RegisterCreatedObjectUndo(supplement,"R30SourceReturnSupplement");supplement.transform.SetParent(own.transform,false);
   var groups=new Dictionary<string,Transform>();groups[""]=supplement.transform;'''
if old not in s:raise RuntimeError('try block match missing')
s=s.replace(old,new)
s=s.replace('groupObject.transform.SetParent(own.transform,false)','groupObject.transform.SetParent(supplement.transform,false)')
s=s.replace('SceneView.RepaintAll();','')
s=s.replace('["status"]="OWN_STORE_APPLIED_AND_SAVED"','["status"]="JOINT_SOURCE_RETURN_FITOUT_APPLIED_AND_SAVED"')
s=s.replace('["objects"]=applied,["object_count"]=applied.Count,','["objects"]=applied,["object_count"]=applied.Count,["existing_transforms_changed"]=moved,["exact_existing_transform_count"]=moved.Count,["joint_authority"]=movements["authority"].DeepClone(),["supplement_root"]=ParentPath+"/"+name+"/R30_RETURN_SOURCE_SUPPLEMENT",')
s=s.replace('["pastry_counter_status"]="REQUIRED_HELD_FIXED_FLOWERS_OCCUPANCY_CORRESPONDENCE"','["pastry_counter_status"]="SOURCE_OBSERVED_CASE_STOCK_COUNTER_CASHIER_AND_RETURN_HEADER_APPLIED_QUALIFIED_MODEL"')
s=s.replace('string output=".state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/southwest-dain/constructor/APPLIED-SOURCE-OBSERVED-DAIN-R29.json"','string output=".state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/southwest-dain/joint-constructor-r30/APPLIED-JOINT-SOURCE-RETURN-R30.json"')
helper=''' static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static Transform ExactTarget(JToken spec,string requiredParent){GlobalObjectId id;if(!GlobalObjectId.TryParse((string)spec["current_object_id"],out id))throw new Exception("ExactCurrentObjectIDRequired");var o=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);var go=o as GameObject;var t=go!=null?go.transform:o as Transform;if(t==null||!PathOf(t).StartsWith(requiredParent+"/",StringComparison.Ordinal))throw new Exception("ExactScopedEquipmentOwnershipRequired:"+(string)spec["current_object_id"]);var p=t.position;float qx=-.34290633335f*p.x-.93936960061f*p.z,qy=.93936960061f*p.x-.34290633335f*p.z;var old=spec["old_q"];if(Mathf.Abs(qx-(float)old[0])>.025f||Mathf.Abs(qy-(float)old[1])>.025f)throw new Exception("SourceBoundOldEquipmentPoseChanged:"+t.name);return t;}
'''
s=s.replace(' public static string Apply(){',helper+' public static string Apply(){')
file=W/'SouthwestJointReturnR30.cs';file.write_text(s)
request={'thread':'classic:/root/r29_southwest_field_fitout','scope':'C180_R30_SW_JOINT_RETURN_APPLY_001','name':'R30-SW-JOINT-RETURN-APPLY-001','file':str(file.relative_to(R)),'entry':'SouthwestJointReturnR30.Apply','timeout_ms':600000}
(W/'C180-REQUEST-R30.json').write_text(json.dumps(request,indent=2))
(W/'IMMUTABLE-CONSTRUCTION-IDENTITY-R30.json').write_text(json.dumps({'packet':str(frozen),'sha256':sha,'moves':str(mp),'moves_sha256':msha,'script':str(file),'script_sha256':hashlib.sha256(file.read_bytes()).hexdigest(),'root_authority':str(authority),'request':request,'no_QA':True},indent=2))
print(json.dumps({'script':str(file),'packet_sha256':sha,'new_parts':230,'exact_existing_transform_count':len(moves['Flowers'])+1,'request':request['name']}))
