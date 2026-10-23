from pathlib import Path
import json,hashlib,datetime
P=Path(__file__).resolve().parent;ROOT=Path('/Users/um-yunsang/CHOOGuard')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
packet=P/'BLUE-LOCKERS-SUPPLEMENT-PACKET-002.json';p=json.loads(packet.read_text())
s=(P/'BlueLockersBuilderR26.cs').read_text().replace('BlueLockersBuilderR26','BlueLockersSupplementR26')
s=s.replace('StoreKey="blue-lockers"','StoreKey="blue-lockers-supplement"').replace('C180_R26_BLUE_LOCKERS_EQUIPMENT_001','C180_R26_BLUE_LOCKERS_VISIBLE_BANKS_002')
s=s.replace('constructor/BLUE-LOCKER-PACKET-R26.json','constructor/BLUE-LOCKERS-SUPPLEMENT-PACKET-002.json')
start=s.index('PacketSHA="')+len('PacketSHA="');end=s.index('"',start);s=s[:start]+sha(packet)+s[end:]
s=s.replace('All2FCompletionR26/BlueLockers"','All2FCompletionR26/BlueLockersSupplement002"')
s=s.replace('name="R26_BLUE_LOCKERS_901_926_SOURCE_MODEL"','name="R26_BLUE_LOCKERS_VISIBLE_BANKS_SUPPLEMENT_002"')
s=s.replace('APPLIED-BLUE-LOCKERS-R26.json','APPLIED-VISIBLE-BANKS-SUPPLEMENT-002.json')
at=s.index(' public static string Apply()')
extension='''
 static readonly JArray AdaptedOriginalParts=new JArray();
 static void ExtendOwnOriginalSourceCaps(string folder){
  const string original="FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT/R26_BLUE_LOCKERS_901_926_SOURCE_MODEL";
  var root=GameObject.Find(original);if(root==null)throw new Exception("Existing435SourceBankMissing; preserve current scene, no replay");
  foreach(string child in new[]{"Hood/PitchedHood","EndCaps/EndCap_1","EndCaps/EndCap_-1","EndCaps/EndCapWhiteArtwork","EndCaps/EndCapDarkWaves"}){
   var t=root.transform.Find(child);if(t==null)throw new Exception("ExactOwnOriginalPartMissing:"+child);var mf=t.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)throw new Exception("OwnPartMeshMissing");
   var previous=mf.sharedMesh;var derivative=UnityEngine.Object.Instantiate(previous);derivative.name="R26_OBSERVED_OPPOSED_FACE_"+Clean(child);var vertices=derivative.vertices;
   for(int i=0;i<vertices.Length;i++){var v=vertices[i];if(child.StartsWith("Hood/"))v.x=v.x*(1.58f/.78f)-.40f;else if(child.EndsWith("Artwork")||child.EndsWith("Waves"))v.x=v.x*(1.53f/.73f)+.40f;else v.z=v.z*(1.58f/.78f)-(.40f/.78f);vertices[i]=v;}
   derivative.vertices=vertices;derivative.RecalculateNormals();derivative.RecalculateBounds();string ap=folder+"/MESH_SOURCE_OPPOSED_JOIN_"+Clean(child)+".asset";AssetDatabase.CreateAsset(derivative,ap);CreatedAssets.Add(ap);
   Undo.RecordObject(mf,"ExtendOwnPhotoObservedOpposedFace");mf.sharedMesh=derivative;EditorUtility.SetDirty(mf);
   AdaptedOriginalParts.Add(new JObject{["object_id"]=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString(),["path"]=original+"/"+child,["original_source_mesh"]=AssetDatabase.GetAssetPath(previous),["independent_derivative"]=ap,["TRS_changed"]=false,["purpose"]="Source observed opposed face requires one full pitched hood/endcap profile; existing435object transforms preserved"});
  }
 }
'''
s=s[:at]+extension+s[at:]
s=s.replace('BuildMaterials(p,folder);var parent=Ensure(ParentPath);','BuildMaterials(p,folder);ExtendOwnOriginalSourceCaps(folder);var parent=Ensure(ParentPath);')
s=s.replace('SOURCE_OBSERVED_26_COMPARTMENT_BLUE_LOCKER_BANK_APPLIED_AND_SAVED','SOURCE_VISIBLE_BANK_FACES_SUPPLEMENT_APPLIED_AND_SAVED')
s=s.replace('["observed_compartments"]=26,["payment_kiosks"]=1,','["additional_model_compartments"]=104,["additional_payment_kiosks"]=4,["existing435object_TRS_changed"]=false,["adapted_own_original_parts"]=AdaptedOriginalParts,')
(P/'BlueLockersSupplementR26.cs').write_text(s)
basis={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'C180_R26_BLUE_LOCKERS_VISIBLE_BANKS_002','source_graph':'SOURCE-BANK-GRAPH-SUPPLEMENT-R26.json','new_equipment_faces':p['source_packet']['additional_editable_faces'],'new_compartments':104,'new_kiosks':4,'source_model_total_compartments':130,'count_limits':p['source_packet']['source_count_qualification'],'C_side_width_evidence':p['source_packet']['C_width_face_evidence'],'original435transforms_fixed':True,'own_original_caps_and_hood':'5 ownMeshFilter references use independent source-corrected wider derivatives; no original meshAsset edits and no transform changes','current_actual_native_installation_envelope':'CURRENT-ADDITIONAL-EQUIPMENT-ENVELOPE-002.json','floor_transition':{'actual_Native6_qy':[-14.40494,-14.35456],'actual_y':[7.0,7.01],'bankC_plinth_bottom_y':7.022,'new_supports':'52 short independent metal pads, actualqy outside trim strip; MODEL equipment supports, originalnativefloorunchanged'},'Light_Text_TMP_added':False,'QA_added':False,'article_total_count_used':False,'unseen_C_opposed_array_created':False,'all2F_complete':False}
(P/'SUPPLEMENT-MODEL-BASIS-002.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
print('prepared',len(p['objects']),'newparts, packetSHA',sha(packet))
