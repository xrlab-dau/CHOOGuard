from pathlib import Path
import json,numpy as np,hashlib,re,sys
from shapely.geometry import Polygon,Point
sys.path.insert(0,'workers/twin');from official_plan_compare import readbin
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-main-restoration-20261009');s=json.loads((E/'current-inventory.json').read_text());audit=json.loads((O/'whole-guide-current-audit.json').read_text());display=json.loads((O/'dated-guide-display-proposal.json').read_text());scenecomp=json.loads((E/'whole-guide-scene-components.json').read_text());mm={m['id']:m for m in s['meshes']};F=np.array(audit['native_station_frame']);I=np.linalg.inv(F);sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest()
sp=['.planning/2026-09-24-interior-twin/specs/2f-main.json','.planning/2026-09-24-interior-twin/specs/box-3f/box-storefronts.json'];fronts=[(p,e) for p in sp for e in json.loads(Path(p).read_text())['elements'] if e['type']=='Storefront'];specific={11:'SF-2F-편의점_StoryWay',17:'2F-main-front-06-편의점',28:'2F-main-front-09-편의점',27:'2F-main-front-08-코레일라운지 편의점'};renderlookup=[]
for z in audit['retail_nodes']:
 n=z['guide_number'];name=z['current_name']
 if n==32:continue
 candidates=[(p,e) for p,e in fronts if e['id']==specific.get(n) or (n not in specific and e['id'].endswith('-'+name.replace(' ','_')))]
 if not candidates:continue
 p,e=candidates[0];fa=e['params']['fascia'];q=Polygon(z['current_native_polygon']);rends=[];z['station_door_contributors']=[d for d in scenecomp['all_doors'] if d['element']==e['id']]
 if fa.get('texture'):
  stem=Path(fa['texture']).stem;target='Kit_Lightbox_Kit_Tex_'+stem
  for r in s['renderers']:
   if r['path'].split('/')[-1] in [target,'Box_'+target] and '2F' in r['path']:
    m=mm[r['mesh']];v,t=readbin(m);M=np.array(r['world_matrix']);qq=((v@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]]
    # Exact source has per-label batch. Preserve non-target users if material texture shared elsewhere.
    if q.buffer(2).covers(Point(qq[:,:2].mean(0))):
     mats=[{'slot':j,'asset':f,'guid':re.search(r'^guid: (.*)$',Path(f+'.meta').read_text(),re.M)[1],'file_sha256':sha(f)} for j,f in enumerate(r['materials'])]
     rends.append({'owner':r,'mesh':m,'source_file_sha256':sha(m['asset']),'materials':mats,'texture':{'path':fa['texture'],'guid':re.search(r'^guid: (.*)$',Path(fa['texture']+'.meta').read_text(),re.M)[1],'sha256':sha(fa['texture'])},'full_face_count':len(t),'full_vertex_count':len(v),'actual_native_UVY_bounds':[qq.min(0).tolist(),qq.max(0).tolist()]})
 else:
  exact=[x for x in scenecomp['all_textmeshes'] if x['text']==fa.get('text') and q.buffer(2.5).covers(Point(x['native_UVY'][:2])) and 7<=x['native_UVY'][2]<=12]
  # Main #27 sign centre can fall outside truncated recipe; explicit spec/actual text binding is recorded separately if needed.
  if not exact and n==27:exact=[x for x in scenecomp['all_textmeshes'] if x['text']==fa.get('text') and '2F 본관/Sign' in x['path']]
  z['textmesh_contributors']=exact
 z['storefront_recipe']={'path':p,'sha256':sha(p),'element':e};z['texture_rendering_contributors']=rends
 for row in display['operations']:
  if row['guide_number']!=n:continue
  row['texture_rendering_contributors']=rends;row['storefront_recipe']=z['storefront_recipe'];row['field_changes']=[x for x in row['field_changes'] if x['kind']!='StationDoor.Label']+[{'kind':'StationDoor.Label','component_id':d['component_id'],'element_preserved':d['element'],'path':d['path'],'expected':d['label'],'proposed':row['proposed_display_label']+' 출입문','source_block_sha256':d['source_serialized_block_sha256']} for d in z['station_door_contributors']]
  if fa.get('text') and z['textmesh_contributors']:
   row['field_changes']=[x for x in row['field_changes'] if x['kind']!='TextMesh.m_Text']+[{'kind':'TextMesh.m_Text','component_id':x['component_id'],'path':x['path'],'expected':x['text'],'proposed':row['proposed_display_label'],'source_block_sha256':x['source_serialized_block_sha256']} for x in z['textmesh_contributors']]
  row['material_brand_rendering_requires_review']=bool(rends)
  if rends:row['material_brand_rendering_gate']='Use a new scoped material/texture override on only these renderer/component slots; original texture/material/shared users remain preserved. Dated operator label proves identity, not exact fascia artwork.'
# Source guide semantic ordering compares signs only; centroid dimensions never dictate a move.
rels=[]
for z in audit['semantic_relations']:
 if not(z['a'].startswith('guide-2f-retail-') and z['b'].startswith('guide-2f-retail-')):continue
 a=next(x for x in audit['retail_nodes'] if x['guide_number']==int(z['a'][-2:]));b=next(x for x in audit['retail_nodes'] if x['guide_number']==int(z['b'][-2:]));ap=Polygon(a['current_native_polygon']);bp=Polygon(b['current_native_polygon']);ag=np.mean(a['prior_april_polygon_normalized'],0);bg=np.mean(b['prior_april_polygon_normalized'],0);axis=int(np.argmax(abs(bg-ag)));delta=np.array(bp.centroid.coords[0])-np.array(ap.centroid.coords[0]);expected=-np.sign((bg-ag)[axis]);ok=np.sign(delta[axis])==expected;rels.append(z|{'source_diagram_primary_axis':'image x' if axis==0 else 'image y','native_primary_axis':'U' if axis==0 else 'V','native_order_consistent':bool(ok),'current_native_centroid_delta_UV':delta.tolist(),'current_native_polygon_minimum_gap_model_units':ap.distance(bp),'precision_policy':'Qualitative order check only; polygon gap is current model quantity, not measured real spacing.'})
audit['structural_relation_checks']=rels;audit['summary']={'retail_nodes':32,'exact_named_wall_owners':sum(len(x.get('current_named_wall_source_receipts',[])) for x in audit['retail_nodes']),'storefront_recipe_bindings':sum('storefront_recipe'in x for x in audit['retail_nodes']),'texture_rendering_owners':sum(len(x.get('texture_rendering_contributors',[])) for x in audit['retail_nodes']),'native_order_contradictions':[x for x in rels if not x['native_order_consistent']],'uncertainty':'Passing guide order does not certify absolute tenant depth/width/front plane. Six family controls and exact floor/core source constraints remain required for placement.'}
(O/'whole-guide-current-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2)+'\n');(O/'dated-guide-display-proposal.json').write_text(json.dumps(display,ensure_ascii=False,indent=2)+'\n');print(audit['summary']);print('display',[(x['guide_number'],len(x['field_changes']),len(x.get('texture_rendering_contributors',[]))) for x in display['operations']])
