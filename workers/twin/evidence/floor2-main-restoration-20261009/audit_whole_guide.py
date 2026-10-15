from pathlib import Path
import re,yaml,json,hashlib,sys,math
import numpy as np
from shapely.geometry import Polygon,Point
sys.path.insert(0,'workers/twin');from official_plan_compare import readbin
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-main-restoration-20261009');src=json.loads((E/'current-inventory.json').read_text());old=json.loads((E/'guide-semantic-original.json').read_text());packet=json.loads((O/'four-bay-packet.json').read_text());mm={m['id']:m for m in src['meshes']};scene=Path(src['scene']);txt=scene.read_text();sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',txt,re.M|re.S)};gos={};tfs={};components={};F=np.array(next(r for r in src['renderers'] if r['path']=='맞이방 · 원본 정합/Shop_다인명가_Walls')['world_matrix'])[:3,:3];I=np.linalg.inv(F)
for i,b in blocks.items():
 if b.startswith('GameObject:\n'):gos[i]=yaml.safe_load(b)['GameObject']
 elif b.startswith(('Transform:\n','RectTransform:\n')):
  d=yaml.safe_load(b)[b.split(':')[0]];tfs[i]=d
 elif b.startswith(('TextMesh:\n','MonoBehaviour:\n','MeshRenderer:\n','MeshCollider:\n')):components[i]=(b.split(':')[0],b)
def path(tf):
 if tf not in tfs:return '?'
 d=tfs[tf];n=gos.get(d['m_GameObject']['fileID'],{}).get('m_Name','?');par=d['m_Father']['fileID'];return path(par)+'/'+n if par in tfs else n
gtf={d['m_GameObject']['fileID']:i for i,d in tfs.items()};cacheM={}
def M(tf):
 if tf in cacheM:return cacheM[tf]
 d=tfs[tf];qq=d['m_LocalRotation'];x,y,z,w=[float(qq[k]) for k in 'xyzw'];R=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]);out=np.eye(4);out[:3,:3]=R@np.diag([float(d['m_LocalScale'][k]) for k in 'xyz']);out[:3,3]=[float(d['m_LocalPosition'][k]) for k in 'xyz'];pa=d['m_Father']['fileID'];out=M(pa)@out if pa in tfs else out;cacheM[tf]=out;return out
def pos(tf):return (M(tf)[:3,3]@I.T)[[2,0,1]].tolist()
textrows=[];doorrows=[];identityrows=[]
for cid,(ty,b) in components.items():
 if ty=='TextMesh' or (ty=='MonoBehaviour' and ('StationDoor' in b or '664c243f602454c7c88e9ec81a41831e' in b)):
  d=yaml.safe_load(b)[ty];go=d['m_GameObject']['fileID'];tf=gtf.get(go)
  if not tf:continue
  base={'component_id':cid,'gameobject_id':go,'transform_id':tf,'path':path(tf),'native_UVY':pos(tf),'source_serialized_block_sha256':hashlib.sha256(b.encode()).hexdigest()}
  if ty=='TextMesh':textrows.append(base|{'text':str(d['m_Text'])})
  elif 'StationDoor' in b:
   c=d.get('Centre',{'x':0,'y':0,'z':0}); cp=M(tf)@np.array([float(c[k]) for k in 'xyz']+[1]);base['native_UVY']=(cp[:3]@I.T)[[2,0,1]].tolist();doorrows.append(base|{'label':d.get('Label'),'element':d.get('Element'),'fields':d})
  else:identityrows.append(base|{'fields':d})
aug=['국제시장도나스','부산기장특산품','아메리칸트레일러','새부산약국','국제시장단팥빵','하와이안파라다이스','크리스피크림도넛','아트박스','떡카페 소미담','파스쿠찌','편의점','위니비니','부산별빛샌드','올리브영','비엔씨','환공어묵','편의점','영주당 부산과자점','꽃들','크리스피크림도넛','유부주머니 & 부산오뎅','반월당닭강정','청도할매김밥','복순도가','팔공티','남천할매떡볶이','코레일라운지 (CAFESTORYWAY)','편의점','소반한식','비엔씨제과','써브웨이','물품보관함']
rec=[];display=[];geocache={}
def geo(r):
 m=mm[r['mesh']]
 if m['id'] not in geocache:geocache[m['id']]=readbin(m)
 v,t=geocache[m['id']];a=np.array(r['world_matrix']);q=((v@a[:3,:3].T+a[:3,3])@I.T)[:,[2,0,1]];return v,t,q
for z in old['retail_nodes']:
 n=z['guide_number'];cur=z.get('current');entry={'guide_number':n,'dated_august_guide_label':aug[n-1],'prior_april_polygon_normalized':z['source_polygon_normalized'],'image_polygon_epoch':'April photographed guide, topology re-observed August; these are not digitized August pixel coordinates','diagrammatic_polygon_not_metric':True}
 if not cur:
  owners=[r for r in src['renderers'] if 'Locker' in r['path'] or '보관함' in r['path']];entry|={'current_name':'물품보관함','current_native_control_status':'main-up locker relation contradicted by guide; passage-agent owns bounded correction','owner_candidates':owners,'remaining_gate':'Native passage relocation requires original façade/floor control, not icon centroid.'};rec.append(entry);continue
 name=cur['unit_name'];poly=cur['native_root_uv_polygon'];fit=next((u for u in packet['units'] if u['boardNo']==n),None)
 if fit:
  a=fit['after'];poly=[[a['u'][0],a['v'][0]],[a['u'][1],a['v'][0]],[a['u'][1],a['v'][1]],[a['u'][0],a['v'][1]]]
 pp=Polygon(poly);bp=pp.buffer(.35);front=cur['face'];walls=[r for r in src['renderers'] if 'Shop_'+name+'_Walls' in r['path']];named=[]
 for r in walls:
  m=mm[r['mesh']];v,t,q=geo(r);live=fit is None;named.append({'owner':r,'mesh':m,'source_file_sha256':sha(m['asset']),'current_source_binding_status':'unchanged exact export source' if live else 'replaced by integrated four-bay packet; lookup current binding from root receipt','native_source_bounds_UVY':[q.min(0).tolist(),q.max(0).tolist()]})
 ts=[r for r in textrows if 6.5<=r['native_UVY'][2]<=12 and pp.buffer(2).covers(Point(r['native_UVY'][:2])) and (name.strip() in r['text'] or (name=='CAFFE PASCUCCI' and 'PASCUCCI' in r['text']) or (name=='편의점 StoryWay' and ('Story' in r['text'] or r['text'].strip()=='편의점')))]
 ds=[r for r in doorrows if '2F' in (r['element'] or '') and (name.replace(' ','_') in (r['element'] or '') or name in (r['element'] or '') or (name.strip() in (r['label'] or '') and pp.buffer(2).covers(Point(r['native_UVY'][:2]))))]
 ids=[r for r in identityrows if name in r['path'] and ('2F' in r['path'] or '맞이방' in r['path'])]
 fixtures=[r for r in src['renderers'] if name in r['path'] and ('점포 내부 2F' in r['path'] or '점포 2F' in r['path']) and 'counter' in r['path']]
 status='Source correspondence established; absolute footprint remains original guide-affine estimate, not field-verified'
 if n in [20,21,22,23]:status='Bounded guide/native-bay correction integrated by root; four-bay QA215PASS'
 if n in [13,14]:status='Photo/guide shared office-frontage order supported; hidden depth and exact coplanarity unresolved; no metric movement accepted'
 entry|={'current_name':name,'current_recipe_frame':cur['recipe_frame'],'current_native_polygon':poly,'current_front_face':front,'current_named_wall_source_receipts':named,'textmesh_contributors':ts,'station_door_contributors':ds,'source_part_identity_contributors':ids,'imported_counter_owners':fixtures,'structural_status':status,'identity_status':'same label' if name.strip()==aug[n-1] else 'dated identity or spelling/display differs','remaining_gate':'No global metric guide warp accepted; no hidden walls/depth inferred from colored zones.'}
 rec.append(entry)
 if name.strip()!=aug[n-1]:
  dr=[]
  for r in ts:
   oldtext=r['text'];newtext=oldtext.replace(name.strip(),aug[n-1]) if name.strip() in oldtext else aug[n-1];dr.append({'kind':'TextMesh.m_Text','component_id':r['component_id'],'path':r['path'],'expected':oldtext,'proposed':newtext,'source_block_sha256':r['source_serialized_block_sha256']})
  for r in ds:
   oldlabel=r['label'];dr.append({'kind':'StationDoor.Label','component_id':r['component_id'],'element_preserved':r['element'],'path':r['path'],'expected':oldlabel,'proposed':aug[n-1]+' 출입문','source_block_sha256':r['source_serialized_block_sha256']})
  display.append({'guide_number':n,'current_source_identity_name':name,'proposed_display_label':aug[n-1],'field_changes':dr,'source_identity_policy':'SourcePartIdentity has no Label field. Preserve SourceId/SourcePath/SourceDefinition/SourceSha256 and operation bindings; no source/path/ID rename.','material_brand_rendering_requires_review':True,'material_brand_rendering_gate':'Existing generated fascia textures may encode old branding; TextMesh/door labels alone do not replace texture graphics. Exact material/texture contributors to follow.'})
source={'image':'asset-library/research-public/2026-10-09/floor2-guide-passages/source01-yoonsscom-august-003.jpg','sha256':'18da27223565310ad72ef72bcf44a15378d9ea250446b8b111ef7a166d769330','url':'https://m.blog.naver.com/yoonsscom/224376213780','publication_date':'2026-08-12 13:34 Asia/Seoul','capture_date_author_claim':'2026-08-11','floor_label':'2F','pixel_dimensions':[5712,4284]}
base={'schema':'chooguard.main2f-whole-guide-audit.v1','role':'reference_only','current_scene_file':str(scene),'current_scene_sha256':hashlib.sha256(txt.encode()).hexdigest(),'export_scene_sha256':src['scene_file_sha256'],'guide':source,'native_station_frame':F.tolist(),'april_guide_polygon_source':{'path':old['source_image'],'sha256':old['source_sha256']},'accepted_global_metric_registration':None,'site_accuracy_verified':False,'retail_nodes':rec,'semantic_relations':old['semantic_relations'],'protected':'All103doorElements, original Source3DOriginals,1F, native core/frame, original source identities/path labels remain unchanged by this readonly proposal.'}
(O/'whole-guide-current-audit.json').write_text(json.dumps(base,ensure_ascii=False,indent=2)+'\n');(O/'dated-guide-display-proposal.json').write_text(json.dumps({'schema':'chooguard.main2f-dated-display-proposal.v1','current_scene_sha256':base['current_scene_sha256'],'guide':source,'floor':2,'operations':display,'notes':['Guide number is the stable correspondence key: #7 and#20 bothKrispy must not collapse.','CVS current names contain intentional trailing spaces; source string bytes and Elements must be preserved.','Name change describes the dated guide; this does not establish new internal fixture geometry.']},ensure_ascii=False,indent=2)+'\n');(E/'whole-guide-scene-components.json').write_text(json.dumps({'scene_sha256':base['current_scene_sha256'],'all_textmeshes':textrows,'all_doors':doorrows,'all_source_identities':identityrows},ensure_ascii=False,indent=2)+'\n');print({'retail_nodes':len(rec),'walls':sum(len(x.get('current_named_wall_source_receipts',[])) for x in rec),'labelchanges':len(display),'fieldchanges':sum(len(x['field_changes']) for x in display),'doorcount':len(doorrows),'identitycount':len(identityrows),'unmatchedText':[(x['guide_number'],x['current_source_identity_name']) for x in display if not any(y['kind']=='TextMesh.m_Text' for y in x['field_changes'])]})
