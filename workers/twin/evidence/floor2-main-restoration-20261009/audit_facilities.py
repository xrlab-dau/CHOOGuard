from pathlib import Path
import json,hashlib,sys,re,numpy as np
from shapely.geometry import Polygon,Point
sys.path.insert(0,'workers/twin');from official_plan_compare import readbin
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-main-restoration-20261009');s=json.loads((E/'current-inventory.json').read_text());old=json.loads((E/'guide-semantic-original.json').read_text());a=json.loads((O/'whole-guide-current-audit.json').read_text());comp=json.loads((E/'whole-guide-scene-components.json').read_text());mm={m['id']:m for m in s['meshes']};F=np.array(a['native_station_frame']);I=np.linalg.inv(F);sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest();cache={}
def geo(r):
 m=mm[r['mesh']]
 if m['id'] not in cache:cache[m['id']]=readbin(m)
 v,t=cache[m['id']];M=np.array(r['world_matrix']);q=((v@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]];return v,t,q
sv=Path('content/world/observed-interiors/services-2f.json');svc=json.loads(sv.read_text());planpath=Path('.planning/2026-09-23-video-twin/majibang-plan.json');plan=json.loads(planpath.read_text());service={'wc-upper-female':'WC-2F-B1-F','wc-upper-male':'WC-2F-B1-M','wc-main-left-male':'WC-2F-N-M','wc-main-left-female':'WC-2F-N-F','wc-main-right-male':'WC-2F-S-M','wc-main-right-female':'WC-2F-S-F','station-office':'RM-2F-B-역무실','nursery':'RM-2F-B-수유방','railway-police':'RM-2F-S-철도경찰'}
# These are source inspection windows tied to known side/core/tenant context, never proposed facility dimensions.
regions={'escalator-upper-left':[60,64,80,84],'escalator-upper-mid':[10,72,27,81],'escalator-main-left':[17,-20,35,-12],'escalator-main-right':[-61,-20,-42,-12],'stairs-upper-left':[58,92,78,103],'stairs-left-corridor':[56,12,72,34],'stairs-entry-left':[22,-49,35,-28],'stairs-entry-right':[-12,-49,2,-27],'stairs-main-right':[-94,-26,-72,-8],'elevator-upper-toilet':[-12,76,1,87],'elevator-diagonal-entry':[52,18,64,34],'elevator-entry-left-pair':[22,-45,29,-29],'elevator-entry-right-pair':[-9,-46,2,-29],'entrance-upper':[-6,88,29,101],'entrance-diagonal':[54,24,59,31],'entrance-main-central':[5,-46,15,-29]}
poi=Path('Assets/ChooGuard/Art/Emergency/station-points.json');pois=json.loads(poi.read_text());passp=Path('workers/twin/output/floor2-platform-passages-20261009/four-guide-passages-native-audit.json');passes=json.loads(passp.read_text());out=[]
for base in old['facility_nodes']:
 z=dict(base);id=z['id'];owners=[];bounds=None;status='';gate=''
 if id in service:
  el=next(x for x in svc['elements'] if x['id']==service[id]);poly=Polygon(el['geometry']['polygon']);bounds=list(poly.bounds);g=el['geometry'];y0=g['y'];y1=g['y1'];pbuf=poly.buffer(.18)
  for r in s['renderers']+s['colliders']:
   if not r.get('mesh') or not r['path'].startswith('실내 트윈 마감/2F 편의/'):continue
   v,t,q=geo(r);tri=q[t];lo=tri.min(1);hi=tri.max(1);ok=(lo[:,0]>=bounds[0]-.18)&(hi[:,0]<=bounds[2]+.18)&(lo[:,1]>=bounds[1]-.18)&(hi[:,1]<=bounds[3]+.18)&(lo[:,2]>=y0-.03)&(hi[:,2]<=y1+.35);ids=np.flatnonzero(ok)
   ids=np.array([i for i in ids if all(pbuf.covers(Point(q[j,:2])) for j in t[i])],dtype=int)
   if len(ids):
    m=mm[r['mesh']];owners.append({'owner':r,'mesh':m,'source_file_sha256':sha(m['asset']),'spatial_triangle_candidates':ids.tolist(),'source_mask_method':'All source vertices within exact current recipe polygon buffered0.18 and declared Y; READONLY locality receipt, not independent contributor attribution or an authorized edit.','triangle_index_sha256':hashlib.sha256(t[ids].astype('<u4').tobytes()).hexdigest()})
  z['native_recipe']={'path':str(sv),'sha256':sha(sv),'element':el};z['native_polygon']=el['geometry']['polygon'];status='Guide gender/service identity + side/order match; exact enabled current source owners located; absolute room depth remains estimated'
  if id=='station-office':gate='Photos show green front glazing; current generic opaque front is a category mismatch. Exact pane/door/depth dimensions unresolved; inspect office-front-candidates.json.'
 elif id in ['ticket-machines','ticket-counter']:
  el=next(x for x in plan['secondFloorUnits'] if x['name']==('자동발매기' if id=='ticket-machines' else '매표창구'));u,v=el['u'],el['v'];bounds=[u[0],v[0],u[1],v[1]];z['native_polygon']=[[u[0],v[0]],[u[1],v[0]],[u[1],v[1]],[u[0],v[1]]];z['native_recipe']={'path':str(planpath),'sha256':sha(planpath),'element':el};names=['Ticket','매표'] if id=='ticket-counter' else ['Machine','Vending','자동발매'];owners=[{'owner':r,'mesh':mm[r['mesh']],'source_file_sha256':sha(mm[r['mesh']]['asset'])} for r in s['renderers'] if any(n in r['path'] for n in names) and '맞이방' in r['path']];status='Current service-front chain follows guide relative order; ticketcounterfront has nativewall fit, east end unobserved';gate='Current model width/depth not independent physical measurement; do not derive new dimensions from guide bar.'
 elif id=='information-desk':
  info=plan['secondFloorExtras']['info'];deg=np.deg2rad(plan['boxFrame']['rotDeg']);u,v=info['u'],info['v'];cx=np.array([u*np.cos(deg)-v*np.sin(deg),u*np.sin(deg)+v*np.cos(deg)])+plan['boxFrame']['origin'];bounds=[cx[0]-2.9,cx[1]-2.9,cx[0]+2.9,cx[1]+2.9];z['native_centre_UV']=cx.tolist();z['native_recipe']={'path':str(planpath),'sha256':sha(planpath),'element':info};owners=[{'owner':r,'mesh':mm[r['mesh']],'source_file_sha256':sha(mm[r['mesh']]['asset'])} for r in s['renderers'] if 'Box_Majibang_Info_' in r['path'] or r['path'].endswith('Box_Hall_TreeColumn_Shaft')];status='Keep: deskcentre matches current source TreeColumnB; source07 jointly supports desk/#13/office shared high-hall family';gate='Canopy appearance differs from photo but geometryanchor stays; cap17m candidate HOLD until close-camera/native registration.'
 elif id=='elevator-office':
  elid='B-ELEV2F-wing';door=next(x for x in comp['all_doors'] if x['element']==elid);bounds=[-2,54,6,60];z['operating_door']=door;z['native_centre_UVY']=door['native_UVY'];z['native_recipe']={'path':'.planning/2026-09-24-interior-twin/specs/box-3f/box-hall.json','sha256':sha('.planning/2026-09-24-interior-twin/specs/box-3f/box-hall.json'),'element_id':elid};status='Existing2F/3F operating elevator front exactly bound; guideoffice-EV localfamily plausible, no relocation';gate='Do not merge this with upper-toilet EV or mainentry paired source shafts.';owners=[{'owner':r,'mesh':mm[r['mesh']]} for r in s['renderers'] if r['path'].startswith(door['path']+'/')]
 elif id.startswith('tracks-'):
  x=passes['regions'][id];bounds=x['bounds_uv'];owners=x.get('mesh_owners',[])+x.get('collider_owners',[]);z['cross_lane_receipt']={'path':str(passp),'sha256':sha(passp),'region':id};status='Exact native passage owners/control matched by passage agent'
  if id=='tracks-main-up':status='Supported mismatch: old inferred mouthbehind#26 contradicts guide; root/passage agent relocating to#26/ticketwall gap';gate='Use current applied passage packet/scene receipt, not this pre-apply inspection bounds.'
 else:
  bounds=regions[id];status='Guide side/family established; source candidates located, unique facility-to-owner identity unresolved';gate='These are current source inspection bounds, not real facility measurements or proposed geometry. Multiple native components can occur; no metric move/removal authorized.'
  # For escalators use preserved source identities and exact matching renderer owners; no generated surrogate steps.
  if id.startswith('escalator'):
   for identity in comp['all_source_identities']:
    if not identity['fields']['Category'].startswith('escalator'):continue
    u,v,y=identity['native_UVY']
    if bounds[0]<=u<=bounds[2] and bounds[1]<=v<=bounds[3] and -.1<=y<=7.5:
     rr=[r for r in s['renderers'] if r['path']==identity['path'] and r.get('mesh')]
     for r in rr:owners.append({'source_identity':identity,'owner':r,'mesh':mm[r['mesh']],'full_source_part_retained':True})
   pos=[]
   for esc in pois['escalators']:
    if esc['id'].startswith(('esc-1f','esc-2f')):
     for endpoint in [esc['path'][0],esc['path'][-1]]:
      q=(np.array([endpoint[k] for k in 'xyz'])@I.T)[[2,0,1]]
      if 6.9<q[2]<7.2 and bounds[0]<=q[0]<=bounds[2] and bounds[1]<=q[1]<=bounds[3]:pos.append({'id':esc['id'],'endpoint_native_UVY':q.tolist()})
   z['station_point_controls']={'path':str(poi),'sha256':sha(poi),'matching_existing_routes':pos,'state':'Current baked navigation inventory, parent owns regeneration; not independent field measurement.'}
   if pos:status='Guide bank side/order matches named current native2F route endpoints + exact preserved escalator parts; no relocation'
  elif id=='entrance-diagonal':
   z['native_recipe']={'path':str(planpath),'sha256':sha(planpath),'opening_control':plan['doorOpenings'][0]};status='Existing diagonal nativeouterdoor line exactrecipecontrol U55.29..58.27/V25.6..30.2; public-guideentrance family matches';gate='Source passage agent owns nearby internal connector; preserve outer automatic door operators.'
  elif id=='entrance-main-central':
   z['native_recipe']={'path':str(planpath),'sha256':sha(planpath),'opening_control':plan['doorOpenings'][1]};status='Guide centralentry match: two nativeinnerdoor sets U6.35..9.25 /10..12.9,V−31.4..−30.2; four shopbays corrected around protected frame';gate='Root actual operation/capsule tests required, preserve public103 doorbindings.'
  # Native architectural regional candidates for unresolved EV/stairs/entrance, exact face ordinals retained.
  if not id.startswith('escalator'):
   for r in s['renderers']:
    if not r['path'].startswith('FPSWorld/공식 자료 부산역 역사/MainShell/') or not r.get('mesh'):continue
    v,t,q=geo(r);tri=q[t];lo=tri.min(1);hi=tri.max(1);norm=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);ln=np.linalg.norm(norm,axis=1);ok=(hi[:,0]>bounds[0])&(lo[:,0]<bounds[2])&(hi[:,1]>bounds[1])&(lo[:,1]<bounds[3])&(hi[:,2]>7.03)&(lo[:,2]<10.7)&(abs(norm[:,2])<ln*.5)&(hi[:,2]-lo[:,2]>.3);ids=np.flatnonzero(ok)
    if len(ids):owners.append({'owner':r,'mesh':mm[r['mesh']],'regional_vertical_face_candidates':ids.tolist(),'candidate_only_not_component_identity':True})
 z['native_control_bounds_UV']=bounds;z['native_bounds_meaning']='Current recipe bounds or source inspection window; never operator-guide metric dimension';z['existing_source_owner_controls']=owners;z['current_match_status']=status;z['remaining_gate']=gate;out.append(z)
report={'schema':'chooguard.main2f-33facility-native-controls.v1','scene_snapshot_sha256':a['current_scene_sha256'],'export_source_scene_sha256':s['scene_file_sha256'],'role':'reference_only','native_frame':F.tolist(),'guide_floor_label':'2F','guide_polygons_epoch':'April guide; same facility topology observed in August guide','facility_nodes':out,'summary':{'nodes':len(out),'with_existing_owner_controls':sum(bool(x['existing_source_owner_controls']) for x in out),'exact_recipe_or_cross_lane_control':sum(('native_recipe'in x or 'cross_lane_receipt'in x) for x in out),'unresolved_unique_source_assignment':[x['id'] for x in out if x['current_match_status'].startswith('Guide side/family')]},'preserved':['alloriginal sourceparts/core/stairs/EV geometry','currentfloorY7 andactualfloor/supportowners','1F','all103doorbindings','InfoTreeColumnB'],'site_accuracy_verified':False,'production_mutated':False};(O/'facility-native-controls.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(report['summary'])
