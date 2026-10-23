from pathlib import Path
import hashlib,re,json
ROOT=Path('/Users/um-yunsang/CHOOGuard');W=Path(__file__).resolve().parent
src=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/busan-oden/ApplyStoreR25.cs';s=src.read_text();s=s.replace('using TMPro;','')
s=s.replace('StoreBuilderR25','SouthwestDainFitoutR29').replace('const string StoreKey="busan-oden",Thread="classic:/root/r25_busan_oden",Scope="C180_R25_BUSAN_ODEN_APPLY_001";','const string StoreKey="southwest-dain",Thread="classic:/root/r29_southwest_field_fitout",Scope="C180_R29_SW_FIELD_FITOUT_APPLY_001";')
packet=W/'FROZEN-SOURCE-OBSERVED-DAIN-PACKET-R29.json';sha=hashlib.sha256(packet.read_bytes()).hexdigest();s=re.sub(r' const string Packet=.*?;',f' const string Packet="{packet}",PacketSHA="{sha}";',s,count=1)
s=s.replace('TMP_FontAsset localFont=null;','')
a=s.index('     if(kind=="text"){');b=s.index('      Mesh mesh;',a)
s=s[:a]+'     if(kind=="text" || kind=="light")throw new Exception("No scene Light or Text components permitted");\n     {\n'+s[b:]
s=s.replace('if(kind=="text")go.transform.localRotation=go.transform.localRotation*Quaternion.Euler(0,180,0);','')
s=s.replace('"Assets/ChooGuard/Art/StationInterior/R25_Shops/"+StoreKey','"Assets/ChooGuard/Art/StationInterior/R29_SW_Dain/"+StoreKey')
s=s.replace('string name="R25_"+Clean(StoreKey)+"_SOURCE_BOUND_MODEL"','string name="R29_"+Clean(StoreKey)+"_SOURCE_OBSERVED_MODEL"')
s=s.replace('string name="R25_"','string name="R29_"')
s=s.replace('string output=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/"+StoreKey+"/APPLIED-STORE-R25.json";','string output=".state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/southwest-dain/constructor/APPLIED-SOURCE-OBSERVED-DAIN-R29.json";')
s=s.replace('m.SetColor("_BaseColor",ColorOf(a["color"]));','m.SetColor("_BaseColor",ColorOf(a["color"]));if((bool?)a["alpha_clip"]==true){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.5f);m.EnableKeyword("_ALPHATEST_ON");}if((bool?)a["double_sided"]==true)m.SetFloat("_Cull",0);')
# Imported models remain independent own instances; prevent accidental model-space text or lights from any prefab.
needle='model.transform.localPosition=Vector3.zero;'
s=s.replace(needle,needle+'foreach(var c in model.GetComponentsInChildren<Component>(true)){if(c==null)continue;string n=c.GetType().Name;if(c is Light || c is TextMesh || n=="TextMeshPro" || n=="TextMeshProUGUI")UnityEngine.Object.DestroyImmediate(c);}')
s=s.replace('["99percent_achieved"]=false,','["99percent_achieved"]=false,["source_epoch_qualification"]="ObservedDain2022/2024, guidephotographed2026; notOctober2026survey",["new_shell_or_ceiling"]=false,["all_displayed_letters_closed_mesh"]=true,["source_placement_basis"]=Packet,')
s=s.replace('"R25_"+Clean(StoreKey)+"_SOURCE_BOUND_MODEL"','"R29_"+Clean(StoreKey)+"_SOURCE_OBSERVED_MODEL"')
(W/'SouthwestDainFitoutR29.cs').write_text(s)
(W/'CONSTRUCTION-PACKET-IDENTITY-R29.json').write_text(json.dumps({'packet':str(packet),'sha256':sha,'builder':str(W/'SouthwestDainFitoutR29.cs'),'entry':'SouthwestDainFitoutR29.Apply','scope':'C180_R29_SW_FIELD_FITOUT_APPLY_001','no_QA':True},indent=2))
print(json.dumps({'builder':str(W/'SouthwestDainFitoutR29.cs'),'packet_sha256':sha,'entry':'SouthwestDainFitoutR29.Apply'}))
