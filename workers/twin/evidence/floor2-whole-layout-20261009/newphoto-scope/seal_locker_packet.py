from pathlib import Path
import json,hashlib,re,yaml,numpy as np
O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/locker-withholding');E=Path('workers/twin/evidence/floor2-whole-layout-20261009/newphoto-scope/locker-withholding')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();hb=lambda b:hashlib.sha256(b).hexdigest()
p=json.load(open(O/'locker-withholding-packet.json'));inv=json.load(open('.planning/2026-10-09-floor2-completion-pass2/after-facades/current-inventory.json'));scene=Path('Assets/ChooGuard/Scenes/FpsStation.unity');text=scene.read_text();blocks={int(i):(int(t),b)for t,i,b in re.findall(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
def rec(i,without_mesh=False):
 kind,b=blocks[i];d={'component_id':i,'class_id':kind,'block_sha256':hb(b.encode())};d['block_without_mesh_sha256']=hb(re.sub(r'^  m_Mesh:.*\n','',b,flags=re.M).encode());return d
def go(i):return int(re.search(r'm_GameObject: \{fileID: (\d+)',blocks[i][1]).group(1))
goNames={i:yaml.safe_load(b)['GameObject']['m_Name']for i,(k,b)in blocks.items()if k==1}
tByGo={go(i):i for i,(k,b)in blocks.items()if k==4}
def path(t):
 b=blocks[t][1];gid=go(t);parent=int(re.search(r'm_Father: \{fileID: (\d+)',b).group(1));return(path(parent)+'/'if parent else'')+goNames[gid]
matdeps={};allreceipt=[]
for patch in p['meshes']:
 role=patch['role'];entry=patch['full_channel_export'];s=next(m for m in inv['meshes']if m['id']==role);assert s['asset']==entry['asset']and sha(s['asset'])==s['asset_sha256']
 owners=[o for o in inv['renderers']if o.get('mesh')==role]+[o for o in inv['colliders']if o.get('mesh')==role];assert len(owners)==1
 o=owners[0];comp=int(o['global_object_id'].split('-')[-2]);gid=go(comp);tr=tByGo[gid];fields=yaml.safe_load(blocks[comp][1]);goFields=yaml.safe_load(blocks[gid][1])['GameObject'];isCollider=blocks[comp][0]==64
 receipt={'owner':o,'component':rec(comp),'transform':rec(tr),'gameobject_id':gid,'gameobject_layer':goFields['m_Layer'],'component_fields':fields}
 if not isCollider:
  mf=[i for i,(k,b)in blocks.items()if k==33 and go(i)==gid];assert len(mf)==1;receipt['meshfilter']=rec(mf[0]);receipt['renderer_enabled_after']=not patch['disable_renderer']
  for m in o['materials']:
   matdeps[m]={'path':m,'file_sha256':sha(m),'meta_sha256':sha(m+'.meta'),'guid':re.search(r'^guid: (\w+)',Path(m+'.meta').read_text(),re.M).group(1)}
 patch['owner_receipt']=receipt;patch['source_asset_sha256']=sha(entry['asset']);patch['source_meta_sha256']=sha(entry['asset']+'.meta');allreceipt.append(receipt)
texts=[]
for i,(k,b)in blocks.items():
 if k!=102:continue
 d=yaml.safe_load(b)['TextMesh'];gid=go(i);tr=tByGo[gid];ownerpath=path(tr)
 if d['m_Text']=='물품보관함 Lockers'and ownerpath.startswith('실내 트윈 마감/2F 본관 보강/'):
  render=[j for j,(kk,bb)in blocks.items()if kk==23 and go(j)==gid];assert len(render)==1
  texts.append({'path':ownerpath,'text':d['m_Text'],'text_component':rec(i),'renderer':rec(render[0]),'transform':rec(tr),'gameobject_id':gid,'local_transform_fields':yaml.safe_load(blocks[tr][1])['Transform'],'text_fields':d,'renderer_enabled_after':False})
assert len(texts)==1
p.update({'scene_at_seal_sha256':sha(scene),'material_dependencies':list(matdeps.values()),'text_withholding':texts[0],'acceptance_owner':'root','new_placement':None,'source_builder':'AgentScripts/MajibangBuilder.cs','source_builder_sha256':sha('AgentScripts/MajibangBuilder.cs'),'source_controls':'workers/twin/evidence/floor2-facility-field-sources-20261009/source-controls.json','source_controls_sha256':sha('workers/twin/evidence/floor2-facility-field-sources-20261009/source-controls.json'),'photographic_support':[{'path':'asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-nose-006.jpg','sha256':sha('asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-nose-006.jpg'),'supports':'Same frame identifies Namcheon beside down-mouth/connector; no bank in storefront approach.'},{'path':'asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-nose-008.jpg','sha256':sha('asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-nose-008.jpg'),'supports':'Direct clear storefront/connector approach; locker arrow directs beyond connector. 30m is signage text, not a registration distance.'}],'current_render_contradiction':{'path':'.planning/2026-10-09-floor2-completion-pass2/namcheon-captures/namcheon-6.png','sha256':sha('.planning/2026-10-09-floor2-completion-pass2/namcheon-captures/namcheon-6.png'),'supports':'The inferred generated bank occupies and occludes the positively supported Namcheon frontage/approach.'},'summary':{'render_triangle_removal':sum(x['selected_triangle_count']for x in p['meshes']if x['role']!='mesh-3476'),'collider_triangle_removal':12,'preserved_unrelated_render_triangles':sum(x['retained_triangle_count']for x in p['meshes']if x['role']!='mesh-3476'),'preserved_unrelated_collider_triangles':244,'unchanged_full_vertex_channel_buffers':True,'no_shared_selected_retained_vertices':True,'pure_bank_renderers_disabled':3,'text_renderers_disabled':1,'source_assets_untouched':True,'canonical_bank_record_retained_with_disposition_override':True,'metric_field_dimensions_claimed':False},'replay_contract':{'registry':'content/world/observed-interiors/locker-placement-withholding.json','descriptive_id':'withheld-inferred-2FM-LOCKERS-20261009','source_root':'실내 트윈 마감/2F 본관 보강','source_element_retained':True,'strict_source_recipe_equality':True,'exact_fresh_complete_channel_baseline_before_writes':True,'apply_only_saved_source_triangle_ordinals':True,'skip_replayed_current_bank_render_and_collision':True,'no_automatic_new_placement':True,'prior_guide_records_or_other_enhancement_elements_unchanged':True}})
(O/'locker-withholding-packet.json').write_text(json.dumps(p,ensure_ascii=False,indent=2)+'\n')
print('SEALED',sha(O/'locker-withholding-packet.json'),'Text',texts[0]['path'],'components',texts[0]['text_component']['component_id'],texts[0]['renderer']['component_id']);print(p['summary'])
