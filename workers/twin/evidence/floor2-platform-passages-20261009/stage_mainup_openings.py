from pathlib import Path
import json, math, struct, hashlib, re
import numpy as np
from shapely.geometry import Polygon,Point
E=Path('workers/twin/evidence/floor2-platform-passages-20261009');O=Path('workers/twin/output/floor2-platform-passages-20261009');
p=Path('.planning/2026-10-09-floor2-guide-restoration/architecture/current/current-scene-structure.json');x=json.loads(p.read_text());ms={m['id']:m for m in x['meshes']};s=math.sin(math.radians(16.2));c=math.cos(math.radians(16.2))
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def load(m):
 b=Path(m['binary']).read_bytes();n,k,_=struct.unpack_from('<iii',b);v=np.frombuffer(b,dtype='<f4',count=n*3,offset=12).reshape(-1,3);t=np.frombuffer(b,dtype='<i4',count=k,offset=12+n*12).reshape(-1,3);return v,t
checks=[]
def ck(name,value,detail=None):checks.append({'name':name,'pass':bool(value),'detail':detail});assert value,name
# Original native face controls, not a global guide warp.
controls={
 '6':{'id':'2F-guide-main-up-boundary','triangles':[30319,30320],'segment':[[-43.084,11.485],[-29.221,12.487]],'y':[7.01,10.45]},
 '5':{'id':'2F-guide-main-up-low-return','triangles':[53831],'expected_collider_triangles':[53826,53829,53831],'segment':[[-45.606,4.067],[-14.088,6.345]],'y':[7.01,8.25]}
}
# GUIDE local order constrains gap -33..-26.6. Existing native boundary ends -29.221.
# These bounds keep source returns: 0.2 from tenant26 edge; >=0.279 before native endpoint.
U0,U1=-32.8,-29.95
ck('gap_within_guide_local_bay',-33<U0<U1<-26.6)
rows=[]
source_rows=[]
for suffix,a in controls.items():
 control=next(rr for rr in x['colliders']if rr['path'].endswith('MainShell/OfficialStation_-부산역_'+suffix));cm=ms[control['mesh']];cv,ct=load(cm);MM=np.array(control['world_matrix']);cw=cv@MM[:3,:3].T+MM[:3,3];cc=np.column_stack((cw[:,0]*s+cw[:,2]*c,cw[:,0]*c-cw[:,2]*s,cw[:,1]));points=cc[ct[a['triangles']].ravel()];au,bu=np.polyfit(points[:,0],points[:,1],1);a['line_fit']=[float(au),float(bu)]
 for collection in ['renderers','colliders']:
  rr=next(rr for rr in x[collection]if rr['path'].endswith('MainShell/OfficialStation_-부산역_'+suffix));source_rows.append((suffix,a,collection,rr))
for suffix,a,collection,r in source_rows:
 label=suffix+'_'+collection;m=ms[r['mesh']];v,t=load(m);M=np.array(r['world_matrix']);w=v@M[:3,:3].T+M[:3,3];uvy=np.column_stack((w[:,0]*s+w[:,2]*c,w[:,0]*c-w[:,2]*s,w[:,1]));au,bu=a['line_fit'];p0=[U0,float(au*U0+bu)];p1=[U1,float(au*U1+bu)];half=.12
 L=np.linalg.norm(np.array(p1)-p0);along=(np.array(p1)-p0)/L;across=np.array([-along[1],along[0]]);y0,y1=a['y'];
 local=np.column_stack(((uvy[:,:2]-p0)@along,(uvy[:,:2]-p0)@across,uvy[:,2]));
 # Plane interior: component>=lo and component<=hi. Outside pieces partition the triangle difference.
 planes=[(0,1.,0.),(0,-1.,-L),(1,1.,-half),(1,-1.,-half),(2,1.,y0),(2,-1.,-y1)]
 def clip(poly,axis,sign,cut,inside):
  if len(poly)==0:return []
  out=[]
  for A,B in zip(poly,poly[1:]+poly[:1]):
   da=sign*A[axis]-cut;db=sign*B[axis]-cut;aa=da>=-1e-11 if inside else da<=1e-11;bb=db>=-1e-11 if inside else db<=1e-11
   if aa:out.append(A)
   if aa!=bb:out.append(A+(B-A)*(da/(da-db)))
  return out
 def area(poly):
  if len(poly)<3:return 0.
  return sum(np.linalg.norm(np.cross(poly[j]-poly[0],poly[j+1]-poly[0]))/2 for j in range(1,len(poly)-1))
 q=local[t];touch=(q[:,:,0].max(axis=1)>1e-5)&(q[:,:,0].min(axis=1)<L-1e-5)&(q[:,:,1].max(axis=1)>-half+1e-5)&(q[:,:,1].min(axis=1)<half-1e-5)&(q[:,:,2].max(axis=1)>y0+1e-5)&(q[:,:,2].min(axis=1)<y1-1e-5)
 hits=[];removed=0.;kept=0.;split=0;newfaces=0
 for j in np.flatnonzero(touch):
  poly=[p.copy()for p in q[j]];sourcearea=area(poly);inside=poly
  for z in planes:inside=clip(inside,*z,True)
  ai=area(inside)
  if ai<1e-10:continue
  normal=np.cross(w[t[j,1]]-w[t[j,0]],w[t[j,2]]-w[t[j,0]]);normal/=np.linalg.norm(normal)
  ck('non_horizontal_hit_'+suffix+'_'+str(j),abs(normal[1])<.1,normal.tolist())
  pieces=[];work=poly
  for z in planes:
   out=clip(work,*z,False)
   if area(out)>1e-10:pieces.append(out)
   work=clip(work,*z,True)
  keptarea=sum(area(z)for z in pieces);ck('area_balance_'+suffix+'_'+str(j),abs(sourcearea-ai-keptarea)<1e-7)
  hits.append({'triangle_ordinal':int(j),'source_area':sourcearea,'removed_area':ai,'retained_area':keptarea,'retained_pieces':len(pieces),'retained_piece_triangles':sum(len(z)-2 for z in pieces),'normal_world':normal.tolist()});removed+=ai;kept+=keptarea;newfaces+=sum(len(z)-2 for z in pieces)
 ck('positive_cut_area_'+suffix,removed>0)
 print('HITS',label,[(h['triangle_ordinal'],h['removed_area'])for h in hits]);ck('native_boundary_face_count_'+label,len(hits)==(2 if suffix=='6' else 3),[h['triangle_ordinal']for h in hits]);
 if collection=='colliders':ck('only_expected_collider_faces_'+suffix,set(h['triangle_ordinal']for h in hits)==set(a.get('expected_collider_triangles',a['triangles'])))
 # Full owner seals: native both MeshFilter and MeshCollider, no disconnected target guesses.
 renderowners=[rr for rr in x['renderers']if rr['mesh']==m['id']]
 colowners=[rr for rr in x['colliders']if rr.get('mesh')==m['id']]
 def owner(rr,kind):return {'path':rr['path'],'global_object_id':rr['global_object_id'],'type':kind,'world_matrix':rr['world_matrix']}
 subcounts=[int(z)for z in re.findall(r'    indexCount: (\d+)',Path(m['asset']).read_text())]
 ck('source_flattened_submesh_counts_'+suffix,sum(subcounts)==len(t)*3)
 cum=np.cumsum([0]+[k//3 for k in subcounts]);
 for h in hits:
  sub=int(np.searchsorted(cum,h['triangle_ordinal'],side='right')-1);h['submesh']=sub;h['submesh_triangle_ordinal']=h['triangle_ordinal']-int(cum[sub]);
 rows.append({'owner_role':collection,'source_asset':m['asset'],'source_guid':m['asset_guid'],'source_file_sha256':sha(m['asset']),'source_binary':m['binary'],'source_binary_sha256':m['binary_sha256'],'source_vertices':len(v),'source_triangles':len(t),'source_submesh_index_counts':subcounts,'wall_control_fit_V_from_U':a['line_fit'],'mesh_owners':[owner(rr,'MeshFilter')for rr in renderowners],'collider_owners':[owner(rr,'MeshCollider')for rr in colowners],'opening':{'id':a['id']+'-'+collection,'target':r['path'],'box':{'p0':p0,'p1':p1,'half':half,'y':a['y']},'by':'floor2_platform_passages','evidence':['Aug2026operatorguide main-up is native +U of retail26/locker32','source09-namcheon-000 actual photograph confirms LEFT/native+U glazed connector beside retail26, original solid doors on its other side','local bay bound retail26+U=-33 /ticket-wall-U=-26.6','exact native face controls; interpolation line, preserve wall outside gap and floorY7']},'independent_clip_expectation':{'hit_triangles':hits,'removed_area_m2':removed,'new_retained_piece_triangles':newfaces,'untouched_original_triangles':len(t)-len(hits),'original_vertices_retained':True,'floor_or_ceiling_faces_hit':False}})
for suffix in controls:
 groups=[rr for rr in rows if rr['opening']['id'].startswith(controls[suffix]['id'])];ck('independent_MF_MC_area_matches_'+suffix,abs(groups[0]['independent_clip_expectation']['removed_area_m2']-groups[1]['independent_clip_expectation']['removed_area_m2'])<1e-7)
# Continuous path floor support from exact original native upward faces.
probehits=[]
for V in np.linspace(0,19,39):
 U=(U0+U1)/2;best=None
 for rr in x['colliders']:
  if 'FPSWorld/공식 자료 부산역 역사/'not in rr['path']or rr.get('type')!='MeshCollider'or not rr.get('mesh')or not rr.get('active')or not rr.get('enabled'):continue
  mm=ms[rr['mesh']]
  if not mm.get('binary'):continue
  if rr['world_bounds_min'][1]>7.31 or rr['world_bounds_max'][1]<6.9:continue
  vv,tt=load(mm);MM=np.array(rr['world_matrix']);ww=vv@MM[:3,:3].T+MM[:3,3];uv=np.column_stack((ww[:,0]*s+ww[:,2]*c,ww[:,0]*c-ww[:,2]*s));pp=ww[tt];yy=pp[:,:,1];qq=uv[tt];bmin=qq.min(axis=1);bmax=qq.max(axis=1);mask=(yy.max(axis=1)<=7.31)&(yy.min(axis=1)>=6.9)&(bmin[:,0]<=U+1e-5)&(bmax[:,0]>=U-1e-5)&(bmin[:,1]<=V+1e-5)&(bmax[:,1]>=V-1e-5)
  for j in np.flatnonzero(mask):
   cross=np.cross(pp[j,1]-pp[j,0],pp[j,2]-pp[j,0]);
   if cross[1]<=0:continue
   A=np.column_stack((qq[j],np.ones(3)))
   if abs(np.linalg.det(A))<1e-9:continue
   bc=np.linalg.solve(A.T,[U,V,1.]);
   if min(bc)<-1e-5:continue
   Y=float(np.dot(bc,yy[j]));hit={'UV':[U,float(V)],'Y':Y,'owner':rr['path'],'mesh':mm['id'],'triangle':int(j)}
   if best is None or Y>best['Y']:best=hit
 probehits.append(best or {'UV':[U,float(V)],'native_floor_missing':True})
ck('native_floor_all28_gate_and_deck_samples',all('native_floor_missing'not in hit for hit in probehits if hit['UV'][1]>=5.5));
(E/'main-up-native-floor-support-qa.json').write_text(json.dumps({'native_supported_points':[hit for hit in probehits if 'native_floor_missing'not in hit],'front_approach_points_requiring_existing_generated_floor':[hit for hit in probehits if 'native_floor_missing'in hit],'no_floor_mesh_appended':True,'gate_cut_box_does_not_touch_any_horizontal_source_face':True,'note':'Native deck supports all28 samplesV5.5..19. FrontapproachV0..5 has existing generated2Ffloor; root actual probes confirm support and no newfloorisproposed.'},ensure_ascii=False,indent=2))
packet={'schema':'chooguard.guide-local-current-native-openings.v1','status':'STAGED_GUIDE_TOPOLOGY_BOUNDED_NATIVE_OPENINGS','scene_sha256':x['scene_file_sha256'],'source_export':str(p),'source_export_sha256':sha(p),'guide':'asset-library/research-public/2026-10-09/floor2-guide-passages/source01-yoonsscom-august-003.jpg','guide_sha256':sha('asset-library/research-public/2026-10-09/floor2-guide-passages/source01-yoonsscom-august-003.jpg'),'photo_controls':[{'path':'asset-library/research-public/2026-10-09/floor2-guide-passages/source09-namcheon-000.jpg','sha256':sha('asset-library/research-public/2026-10-09/floor2-guide-passages/source09-namcheon-000.jpg'),'url':'https://m.blog.naver.com/suziesuzie/224046364232','published':'2025-10-19','control':'retail26 LEFT glass connector and metal locker-labelled jamb; dimensionnotmeasured'}],'reference_only':True,'physical_accuracy_verified':False,'independent_field_measurements':0,'width_is_native_constrained_schematic_inference':True,'guide_local_controls':{'current_retail26_plus_U':-33,'current_ticketwall_minus_U':-26.6,'native_boundary_plus_U':-29.221,'native_right_jamb_min_U':-29.72053,'preserved_jamb_original_vertices':True,'portal_U':[U0,U1],'portal_clear_width_native_model_m':float(np.linalg.norm(np.array(rows[0]['opening']['box']['p1'])-rows[0]['opening']['box']['p0'])),'not_global_affine_registered':True},'mesh_rows':rows,'openings':[r['opening']for r in rows],'native_center_floor_probes':probehits,'must_preserve':['all source floors and ceilings','MainShell5 and6 outside exact local boxes','original SourcePartIdentity andplatform escalator bindings','existing upper-right Y7.2 deck/ramp anddiagonal registration columns','locker32 andretail26 in current scene (not relocated here)'],'remaining_gates':['Root current-mesh in-memory preview strict report compare','actual reciprocal capsule/CharacterController cross new opening','Retail26 LEFT connector photo corroborates guide-local side; actual dimension/independent field accuracy remains unmeasured','Confirm final rebuilt-source regression with live current-layer native meshes']}
(O/'main-up-native-openings-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2));(E/'main-up-extended-floor-clip-qa.json').write_text(json.dumps({'checks':checks,'passed':sum(q['pass']for q in checks),'failed':sum(not q['pass']for q in checks),'packet_sha256':sha(O/'main-up-native-openings-packet.json')},ensure_ascii=False,indent=2));print('QA',len(checks),'rows',[(r['opening']['id'],r['independent_clip_expectation']['removed_area_m2'])for r in rows])
