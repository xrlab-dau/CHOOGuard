from helpers import *
packet=json.loads((O/'packet-draft.json').read_text());checks=[];geometry=[];protected_walls=[];floor_results=[];capsule_results=[];rays=[]
def check(i,ok,d):checks.append({'id':i,'passed':bool(ok),'details':d})
FRAME=np.array(next(r for r in P['mesh_patches'] if r['source_asset'].endswith('/Shop_다인명가_Walls.asset'))['owners'][0]['world_matrix'])[:3,:3];INV=np.linalg.inv(FRAME);uvy=lambda w:(w@INV.T)[:,[2,0,1]]
for row in packet['mesh_patches']:
 d=meshread(row['source_asset']);v,t=d['v'],d['t'];removed=set(row['remove_triangle_ordinals']);keep=np.array([i for i in range(len(t)) if i not in removed]);owners=row['owners']+row['collider_owners'];M=np.array(owners[0]['world_matrix']);w=v@M[:3,:3].T+M[:3,3];geometry.append({'source':row['source_asset'],'world_vertices':w,'triangles':t[keep],'current_retained_source_triangle_ordinals':keep,'scope':'staged_generated_static'})
 for unit in row['unit_contributor_masks']:
  rr=unit['current_retained_unit_faces'];q=v[:,[2,0,1]];native=next(n for n in packet['native_operator_preservation'] if n['unit']==unit['guide_number']);top=native['native_centre_UVY'][2]+float(native['fields']['Height']);bound=[]
  if rr:bound=[q[t[rr]].reshape(-1,3).min(0).tolist(),q[t[rr]].reshape(-1,3).max(0).tolist()]
  check(Path(row['source_asset']).name+'_all_remaining_own_front_above_'+str(unit['guide_number']),not rr or bound[0][2]>top,{'retained_current_unit_faces':rr,'UVY_bounds':bound,'native_nominal_top_Y':top})
# Current generated source walls remain exactly unchanged. Their three planar sides leave the frontage open.
for u in packet['units']:
 old=next(r for r in P['mesh_patches'] if Path(r['source_asset']).name=='Shop_'+next(m['source_name'] for r in packet['mesh_patches'] for m in r['unit_contributor_masks'] if m['guide_number']==u['unit'])+'_Walls.asset');src=REG[old['source_asset']]['replacement_asset'];d=meshread(src);guid=re.search(r'^guid: (.*)$',Path(src+'.meta').read_text(),re.M)[1];owners,cols=ownerproof(guid);M=np.array((owners+cols)[0]['world_matrix']);w=d['v']@M[:3,:3].T+M[:3,3];t=d['t'];q=d['v'][:,[2,0,1]];a=np.array(u['bounds']['u']);b=np.array(u['bounds']['v']);front=a[0] if u['unit']==20 else a[1];atfront=np.all(q[t,0]==np.float32(front),axis=1);check('no_generated_closing_wall_'+str(u['unit']),not atfront.any(),{'source':src,'source_sha256':sha(src),'front_U':front,'full_front_plane_faces':np.flatnonzero(atfront).tolist(),'three_double_sided_wall_planes':[{'faces':list(range(i,i+4)),'UVY_min':q[t[i:i+4]].reshape(-1,3).min(0).tolist(),'UVY_max':q[t[i:i+4]].reshape(-1,3).max(0).tolist()} for i in range(0,len(t),4)]});protected_walls.append({'unit':u['unit'],'source_asset':src,'source_sha256':sha(src),'source_asset_guid':guid,'owners':owners,'collider_owners':cols});geometry.append({'source':src,'world_vertices':w,'triangles':t,'current_retained_source_triangle_ordinals':np.arange(len(t)),'scope':'unchanged_generated_wall'})
# Source collision floor + source static jamb/background; exact saved asset and current owner transform.
fsource='Assets/ChooGuard/Art/Fps/NativeAutomaticDoors20261009/native-door-collision-background-2809044.asset';fd=meshread(fsource);fguid=re.search(r'^guid: (.*)$',Path(fsource+'.meta').read_text(),re.M)[1];fo,fc=ownerproof(fguid);fowner=next(c for c in fc if c['component_id']==127163919);M=np.array(fowner['world_matrix']);fw=fd['v']@M[:3,:3].T+M[:3,3];fq=uvy(fw);ft=fd['t'];tri=fw[ft];cross=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);normal=np.divide(cross,np.linalg.norm(cross,axis=1)[:,None],out=np.zeros_like(cross),where=np.linalg.norm(cross,axis=1)[:,None]>0);floorids=np.flatnonzero((abs(tri[:,:,1]-7).max(1)<.002)&(normal[:,1]>.9));floor_polys=[];selectedfloor=[]
regions=[]
for n in packet['native_operator_preservation']:
 c=np.array(n['world_centre']);ex=np.array(n['world_along']);no=np.array(n['world_normal']);width=float(n['fields']['Width']);r=Polygon(uvy(np.array([c-ex*width/2-no*.7,c+ex*width/2-no*.7,c+ex*width/2+no*.7,c-ex*width/2+no*.7]))[:,:2]);cc=LineString(uvy(np.array([c-no*.7,c+no*.7]))[:,:2]).buffer(.3,quad_segs=64);regions.append((n['unit'],r,cc))
for i in floorids:
 pp=Polygon(fq[ft[i],:2])
 if pp.is_valid and pp.area>0 and any(pp.intersection(r.union(cc)).area>0 for _,r,cc in regions):floor_polys.append(pp);selectedfloor.append(int(i))
floorunion=unary_union(floor_polys)
for unit,ap,cc in regions:
 missing=ap.difference(floorunion.buffer(2e-5)).area;cmissing=cc.difference(floorunion.buffer(2e-5)).area;res={'unit':unit,'original_aperture_UV_polygon':list(ap.exterior.coords),'original_aperture_model_area':ap.area,'capsule_radius':.3,'capsule_central_path_normal_span':[-.7,.7],'capsule_floor_footprint_area':cc.area,'missing_aperture_area_at_existing_2e5_float_tol':missing,'missing_capsule_floor_area_at_existing_2e5_float_tol':cmissing,'no_new_floor_faces':True};floor_results.append(res);check('full_native_aperture_and_capsule_floor_'+str(unit),missing<1e-10 and cmissing<1e-10,res)
geometry.append({'source':fsource,'world_vertices':fw,'triangles':ft,'current_retained_source_triangle_ordinals':np.arange(len(ft)),'scope':'unchanged_original_jamb_background','floor_mask':((abs(tri[:,:,1]-7).max(1)<.002)&(normal[:,1]>.9))})
# Simulate native hold-open leaf geometry only; no transforms/assets are changed.
simulated=[]
for n in packet['native_operator_preservation']:
 for l in n['leaf_subtrees']:
  delta=np.array(l['simulated_hold_open_world_delta'])
  for node in l['nodes']:
   for comp in node['components']:
    if comp['type']!='MeshCollider' or not comp['mesh_guid']:continue
    b=blocks[comp['component_id']];name=Path(node['path']).name;asset=Path('Assets/ChooGuard/Art/Fps/NativeAutomaticDoors20261009')/(name+'.asset')
    if not asset.exists():continue
    guid=re.search(r'^guid: (.*)$',Path(str(asset)+'.meta').read_text(),re.M)[1];assert guid==comp['mesh_guid'];d=meshread(asset);M=np.array(node['world_matrix']);w=d['v']@M[:3,:3].T+M[:3,3]+delta;simulated.append({'unit':n['unit'],'source':str(asset),'source_sha256':sha(asset),'source_guid':guid,'collider_component_id':comp['component_id'],'world_vertices':w,'triangles':d['t'],'current_retained_source_triangle_ordinals':np.arange(len(d['t'])),'scope':'simulated_original_primary_leaf_hold_open','world_rigid_delta':delta.tolist()})
check('six_native_primary_leaf_simulations',len(simulated)==6,{'count':len(simulated),'not_a_runtime_pose_test':True})
# Exact minimum triangle-to-swept capsule-axis rectangle distance. Capsules centered on original door, actual test .3 radius.
def pointtri(p,t):
 a,b,c=t;ab=b-a;ac=c-a;ap=p-a;d1=np.dot(ab,ap);d2=np.dot(ac,ap)
 if d1<=0 and d2<=0:return np.linalg.norm(ap)
 bp=p-b;d3=np.dot(ab,bp);d4=np.dot(ac,bp)
 if d3>=0 and d4<=d3:return np.linalg.norm(bp)
 vc=d1*d4-d3*d2
 if vc<=0 and d1>=0 and d3<=0:return np.linalg.norm(p-(a+ab*d1/(d1-d3)))
 cp=p-c;d5=np.dot(ab,cp);d6=np.dot(ac,cp)
 if d6>=0 and d5<=d6:return np.linalg.norm(cp)
 vb=d5*d2-d1*d6
 if vb<=0 and d2>=0 and d6<=0:return np.linalg.norm(p-(a+ac*d2/(d2-d6)))
 va=d3*d6-d5*d4
 if va<=0 and d4-d3>=0 and d5-d6>=0:return np.linalg.norm(p-(b+(c-b)*(d4-d3)/(d4-d3+d5-d6)))
 z=va+vb+vc
 if abs(z)<1e-24:return min(np.linalg.norm(ap),np.linalg.norm(bp),np.linalg.norm(cp))
 return np.linalg.norm(p-(a+ab*(vb/z)+ac*(vc/z)))
def segseg(p1,q1,p2,q2):
 d1=q1-p1;d2=q2-p2;r=p1-p2;a=np.dot(d1,d1);e=np.dot(d2,d2);f=np.dot(d2,r)
 if a<=1e-24 and e<=1e-24:return np.linalg.norm(r)
 if a<=1e-24:s=0;t=np.clip(f/e,0,1)
 else:
  c=np.dot(d1,r)
  if e<=1e-24:t=0;s=np.clip(-c/a,0,1)
  else:
   b=np.dot(d1,d2);den=a*e-b*b;s=np.clip((b*f-c*e)/den,0,1) if abs(den)>1e-24 else 0;t=(b*s+f)/e
   if t<0:t=0;s=np.clip(-c/a,0,1)
   elif t>1:t=1;s=np.clip((b-c)/a,0,1)
 return np.linalg.norm((p1+d1*s)-(p2+d2*t))
def segmenthit(a,b,t):
 d=b-a;e1=t[1]-t[0];e2=t[2]-t[0];h=np.cross(d,e2);det=np.dot(e1,h)
 if abs(det)<1e-12:return False
 inv=1/det;z=a-t[0];u=inv*np.dot(z,h);q=np.cross(z,e1);v=inv*np.dot(d,q);dist=inv*np.dot(e2,q);return 0<=u<=1 and 0<=v and u+v<=1 and 0<=dist<=1

def tritri(a,b):
 if any(segmenthit(a[i],a[(i+1)%3],b) or segmenthit(b[i],b[(i+1)%3],a) for i in range(3)):return 0.
 ds=[pointtri(p,b) for p in a]+[pointtri(p,a) for p in b];ds.extend(segseg(a[i],a[(i+1)%3],b[j],b[(j+1)%3]) for i in range(3) for j in range(3));return min(ds)
for n in packet['native_operator_preservation']:
 c=np.array(n['world_centre']);no=np.array(n['world_normal']);ex=np.array(n['world_along']);width=float(n['fields']['Width']);height=float(n['fields']['Height']);rect=np.array([c-no*.7+[0,.3,0],c+no*.7+[0,.3,0],c+no*.7+[0,1.42,0],c-no*.7+[0,1.42,0]]);recttris=[rect[[0,1,2]],rect[[0,2,3]]];lo=rect.min(0)-.3;hi=rect.max(0)+.3;distances=[]
 for g in geometry+[s for s in simulated if s['unit']==n['unit']]:
  tri=g['world_vertices'][g['triangles']];near=np.flatnonzero((tri.max(1)>=lo).all(1)&(tri.min(1)<=hi).all(1))
  if 'floor_mask' in g:near=near[~g['floor_mask'][near]]
  minimum=None;hit=[]
  for i in near:
   dist=min(tritri(tri[i],r) for r in recttris);minimum=dist if minimum is None else min(minimum,dist)
   if dist<.3:hit.append({'source_face_ordinal':int(g['current_retained_source_triangle_ordinals'][i]),'axis_distance':dist})
  distances.append({'source':g['source'],'scope':g['scope'],'near_triangles':len(near),'minimum_axis_distance_if_near':minimum,'collisions_for_radius03':hit})
 check('swept03_capsule_geometry_'+str(n['unit']),all(not x['collisions_for_radius03'] for x in distances),{'unit':n['unit'],'axis_rectangle_world':rect.tolist(),'radius':.3,'normal_centre_travel':[-.7,.7],'minimum_distance_test':'exact triangle distance to swept vertical capsule-axis rectangle; floor top support excluded','sources':distances,'original_secondary_FBX_handles_not_simulated':True,'runtime_not_verified':True});capsule_results.append({'unit':n['unit'],'sources':distances})
 # Full nominal static frontage rays deliberately exclude primary native leaves: open-operator motion remains runtime gate.
 hit=[]
 for s in np.linspace(-width/2,width/2,9):
  for y in [.05,.4,1.1,1.7,2.2,height]:
   start=c+ex*s+[0,y,0]+no*.4;direc=-no
   for g in geometry:
    if g['scope']=='unchanged_original_jamb_background':continue
    tr=g['world_vertices'][g['triangles']];e1=tr[:,1]-tr[:,0];e2=tr[:,2]-tr[:,0];hh=np.cross(np.broadcast_to(direc,e2.shape),e2);aa=(e1*hh).sum(1);valid=abs(aa)>1e-12;ii=np.divide(1,aa,out=np.zeros_like(aa),where=valid);z=start-tr[:,0];u=ii*(z*hh).sum(1);qq=np.cross(z,e1);v=ii*(qq*direc).sum(1);d=ii*(e2*qq).sum(1);hits=np.flatnonzero(valid&(u>=-1e-8)&(v>=-1e-8)&(u+v<=1+1e-8)&(d>=0)&(d<=1.1))
    if len(hits):hit.append({'s':float(s),'height':y,'source':g['source'],'source_faces':[int(g['current_retained_source_triangle_ordinals'][i]) for i in hits]})
 check('54_full_nominal_frontage_rays_'+str(n['unit']),not hit,{'ray_count':54,'hits':hit,'not_all_world_collision':True});rays.append({'unit':n['unit'],'rays':54,'hits':hit})
report={'schema':'chooguard.remaining-native-front-geometry-qa.v1','source_scene_sha256':packet['scene_sha256'],'status':'PASS_OFFLINE_GEOMETRY' if all(c['passed'] for c in checks) else 'REJECT','summary':{'checks':len(checks),'passed':sum(c['passed'] for c in checks),'full_nominal_aperture_rays':162,'simulated_native_primary_leaf_colliders':len(simulated),'source_apertures_floor_covered':len(floor_results),'runtime_clearance_verified':False},'checks':checks,'floor_source':{'asset':fsource,'file_sha256':sha(fsource),'guid':fguid,'owner':fowner,'upward_floor_face_ordinals':selectedfloor,'existing_coordinate_tolerance':2e-5},'floor_regions':floor_results,'unchanged_generated_wall_receipts':protected_walls,'simulated_primary_leaf_sources':[{k:v for k,v in s.items() if k not in ['world_vertices','triangles','current_retained_source_triangle_ordinals']} for s in simulated],'capsule_static_geometry':capsule_results,'full_width_rays':rays,'limits':['Exact proposed generated static and source0 original jamb/background + six primary native leaf meshes simulated at declared HoldOpen. Secondary FBX handles and other live-world colliders require root actualPhysics/CC test.','No runtime or field measurement claim; fixed nominal widths and .3 capsule geometry are distinct.']};(E/'geometry-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':report['status'],'summary':report['summary'],'failures':[{'id':c['id'],'details':c['details']} for c in checks if not c['passed']]},ensure_ascii=False,indent=2))
