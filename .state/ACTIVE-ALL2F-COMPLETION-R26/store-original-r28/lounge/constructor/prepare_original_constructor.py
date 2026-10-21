from pathlib import Path
import hashlib,json
R=Path('/Users/um-yunsang/CHOOGuard');B=Path(__file__).parent;O=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/lounge/constructor'
p=B/'LOUNGE-ORIGINAL-PACKET-R28.json';sha=hashlib.sha256(p.read_bytes()).hexdigest()
s=(O/'PublicLoungeBuilderR26.cs').read_text().replace('PublicLoungeBuilderR26','LoungeOriginalBuilderR28').replace('classic:/root/r26_public_lounge','classic:/root/r28_lounge_original').replace('C180_R26_PUBLIC_LOUNGE_VISIBLE_FITOUT_001','C180_R28_LOUNGE_ORIGINAL_SOURCE_COMPLETION_001').replace('StoreKey="public-lounge"','StoreKey="public-lounge-original-r28"')
s=s.replace('.state/ACTIVE-ALL2F-COMPLETION-R26/public/lounge/constructor/PUBLIC-LOUNGE-PACKET-R26.json',str(p.relative_to(R))).replace('d0047b66d22a1442ddae8b5f788b1d3a6ff6df4420bb17cd8989dfc4852f0d63',sha)
s=s.replace('const string ParentPath="FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT";','const string ParentPath="FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT/R26_PUBLIC_LOUNGE_SOURCE_VISIBLE_FITOUT_MODEL";')
s=s.replace('string folder="Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/PublicLounge";string name="R26_PUBLIC_LOUNGE_SOURCE_VISIBLE_FITOUT_MODEL";','string folder="Assets/ChooGuard/Art/StationInterior/All2FCompletionR28/PublicLoungeOriginal";string name="R28_PUBLIC_LOUNGE_ORIGINAL_SOURCE_COMPLETION_MODEL";')
s=s.replace('string output=".state/ACTIVE-ALL2F-COMPLETION-R26/public/lounge/constructor/APPLIED-PUBLIC-LOUNGE-R26.json";','string output=".state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/lounge/constructor/APPLIED-LOUNGE-ORIGINAL-R28.json";')
s=s.replace('["status"]="PUBLIC_LOUNGE_SOURCE_VISIBLE_FITOUT_APPLIED_AND_SAVED"','["status"]="LOUNGE_ORIGINAL_DIMENSIONED_KITCHEN_AND_PHYSICAL_SERVICES_APPLIED_AND_SAVED"')
# Importneverused; keepownsourceboundarycurrent.
s=s.replace('Root+"/.state/ACTIVE-ALL2F-COMPLETION-R26/public/lounge/constructor/"','Root+"/.state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/lounge/constructor/"')
# Exactownmutations resolvedbeforefirstchange, no globalfindsforunrelatedgeometry.
insert='''
 static GameObject OwnObject(JToken row,Transform own){GlobalObjectId id;if(!GlobalObjectId.TryParse((string)row["id"],out id))throw new Exception("Own original object ID invalid");var go=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;if(go==null||!go.transform.IsChildOf(own))throw new Exception("Own original object missing or outside lounge");return go;}
 static void ModifyExisting(JObject p,Vector2 origin,JArray modified,JArray removed){
  var own=GameObject.Find((string)p["own_existing_root"]);if(own==null)throw new Exception("Own sourcefitout missing");
  var mutations=((JArray)p["mutations"]).Select(row=>new{row,go=OwnObject(row,own.transform)}).ToArray();
  var removals=((JArray)p["removals"]).Select(row=>new{row,go=OwnObject(row,own.transform)}).ToArray();
  foreach(var item in mutations){Guard();var row=item.row;var go=item.go;Undo.RecordObject(go.transform,"Lounge original dimensioned placement");
   if(row["q"]!=null)go.transform.localPosition=new Vector3((float)row["q"][0]-origin.x,(float?)row["y"]??go.transform.localPosition.y,(float)row["q"][1]-origin.y);else if(row["y"]!=null){var v=go.transform.localPosition;v.y=(float)row["y"];go.transform.localPosition=v;}
   if(row["size"]!=null)go.transform.localScale=Size(row["size"]);
   if(row["yaw"]!=null)go.transform.localRotation=Quaternion.Euler(0,(float)row["yaw"],0);
   if(row["fit_size"]!=null){if(go.transform.childCount!=1)throw new Exception("Own free model wrapper changed");var model=go.transform.GetChild(0);var bounds=new Bounds();bool first=true;foreach(var mf in model.GetComponentsInChildren<MeshFilter>(true)){if(mf.sharedMesh==null)continue;var bb=mf.sharedMesh.bounds;for(int c=0;c<8;c++){var local=bb.center+Vector3.Scale(bb.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));var v=go.transform.InverseTransformPoint(mf.transform.TransformPoint(local));if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}}if(first)throw new Exception("Own espresso model geometry missing");var want=Size(row["fit_size"]);var factor=new Vector3(want.x/bounds.size.x,want.y/bounds.size.y,want.z/bounds.size.z);Undo.RecordObject(model,"Original espresso dimensions");model.localScale=Vector3.Scale(model.localScale,factor);model.localPosition=Vector3.Scale(model.localPosition,factor);var collider=go.GetComponent<BoxCollider>();if(collider!=null){Undo.RecordObject(collider,"Original espresso exact physical bounds");collider.center=new Vector3(0,want.y*.5f,0);collider.size=want;}}
   if(row["material"]!=null){var renderer=go.GetComponent<Renderer>();if(renderer==null)throw new Exception("Own sourcefront renderer missing");Undo.RecordObject(renderer,"Original white fabricatedcounter finish");renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>MaterialFor((string)row["material"])).ToArray();}
   modified.Add(new JObject{["object_id"]=row["id"],["name"]=row["name"],["source_original_dimension_fit"]=true});
  }
  foreach(var item in removals){Guard();removed.Add(item.row.DeepClone());Undo.DestroyObjectImmediate(item.go);}
 }
'''
s=s.replace(' public static string Apply(){',insert+' public static string Apply(){')
s=s.replace('var applied=new JArray();var createdObjects','var applied=new JArray();var modified=new JArray();var removed=new JArray();var createdObjects')
s=s.replace('BuildMaterials(p,folder);var parent=Ensure(ParentPath);','BuildMaterials(p,folder);ModifyExisting(p,origin,modified,removed);var parent=Ensure(ParentPath);')
s=s.replace('["object_count"]=applied.Count,["created_asset_paths"]','["object_count"]=applied.Count,["modified_objects"]=modified,["modified_count"]=modified.Count,["removed_objects"]=removed,["removed_count"]=removed.Count,["created_asset_paths"]')
s=s.replace('SceneView.RepaintAll();','')
(B/'LoungeOriginalBuilderR28.cs').write_text(s)
print(json.dumps({'packet_sha256':sha,'added':len(json.loads(p.read_text())['objects']),'script':str(B/'LoungeOriginalBuilderR28.cs')}))
