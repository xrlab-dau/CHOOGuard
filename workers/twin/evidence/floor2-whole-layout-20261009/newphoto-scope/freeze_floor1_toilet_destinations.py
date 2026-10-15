from pathlib import Path
import json,hashlib,re,yaml,numpy as np
O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/floor1-toilet-destinations');sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();specPath=Path('content/world/observed-interiors/floor1-source-pdf-20261009.json');spec=json.loads(specPath.read_text());scene=Path('Assets/ChooGuard/Scenes/FpsStation.unity');blocks={int(i):(int(cls),body)for cls,i,body in re.findall(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',scene.read_text(),re.M|re.S)};obj={i:yaml.safe_load(b)for i,(cls,b)in blocks.items()};gos={i:d['GameObject']for i,d in obj.items()if'GameObject'in d};trs={i:d['Transform']for i,d in obj.items()if'Transform'in d};goTr={t['m_GameObject']['fileID']:i for i,t in trs.items()};paths={};mats={}
def pth(t):
 if t in paths:return paths[t]
 tr=trs[t];parent=tr['m_Father']['fileID'];name=gos[tr['m_GameObject']['fileID']]['m_Name'];paths[t]=(pth(parent)+'/'if parent else'')+name;return paths[t]
def mat(t):
 if t in mats:return mats[t]
 tr=trs[t];p=tr['m_LocalPosition'];s=tr['m_LocalScale'];q=tr['m_LocalRotation'];x,y,z,w=[q[k]for k in 'xyzw'];r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]);m=np.eye(4);m[:3,:3]=r@np.diag([s[k]for k in 'xyz']);m[:3,3]=[p[k]for k in 'xyz'];parent=tr['m_Father']['fileID'];mats[t]=mat(parent)@m if parent else m;return mats[t]
roottr=next(i for i in trs if pth(i)==spec['root']);rootMatrix=mat(roottr);rootlocal='root_xyz = (native_V, native_Y, native_U)';assetRoot=Path('Assets/ChooGuard/Art/StationInterior/Kit/1F_PDF_source_20261009');guidMap={re.search(r'^guid: ([0-9a-f]+)',p.read_text(),re.M).group(1):Path(str(p)[:-5])for p in assetRoot.glob('*.asset.meta')};rooms=[]
for row in spec['elements']:
 if row['type']!='SourceDiagramRoom':continue
 poly=np.array(row['geometry']['polygon']);op=row['params']['openings'][0];idx=op['edge'];a=poly[idx];b=poly[(idx+1)%len(poly)];along=(b-a)/np.linalg.norm(b-a);uv=a+along*op['at'];local=np.array([uv[1],row['geometry']['y'],uv[0]]);world=(rootMatrix@np.r_[local,1])[:3];area=np.sum(poly[:,0]*np.roll(poly[:,1],-1)-poly[:,1]*np.roll(poly[:,0],-1))/2;interior=np.array([-along[1],along[0]])*np.sign(area);inlocal=np.array([interior[1],0,interior[0]]);inworld=rootMatrix[:3,:3]@inlocal;geometry=[]
 for cid,d in obj.items():
  kind='MeshFilter'if'MeshFilter'in d else'MeshCollider'if'MeshCollider'in d else None
  if not kind:continue
  c=d[kind];go=c['m_GameObject']['fileID'];t=goTr.get(go)
  if t is None:continue
  path=pth(t)
  if not path.startswith(spec['root']+'/SourceTrace_'+row['id']+'_'):continue
  mesh=c['m_Mesh'];asset=guidMap.get(mesh.get('guid'))
  if asset is None:raise ValueError((cid,path,mesh))
  geometry.append({'path':path,'component':kind,'component_id':cid,'mesh_file_id':mesh['fileID'],'asset':str(asset),'asset_guid':mesh['guid'],'asset_sha256':sha(asset),'asset_meta_sha256':sha(str(asset)+'.meta'),'world_matrix':mat(t).tolist(),'saved_component_block_sha256':hashlib.sha256(blocks[cid][1].encode()).hexdigest()})
 assert len(geometry)>=3,(row['id'],geometry)
 rooms.append({'room_id':row['id'],'point_id':'toilet-1f-source-'+row['id'],'label':'화장실 · 1층 ('+row['id']+')','gender':'unassigned','spec_row':row,'spec_row_canonical_sha256':hashlib.sha256(json.dumps(row,ensure_ascii=False,separators=(',',':'),sort_keys=True).encode()).hexdigest(),'opening_native_UV':uv.tolist(),'mouth_root_xyz':local.tolist(),'mouth_world_xyz':world.tolist(),'inward_root_xyz':inlocal.tolist(),'inward_world_xyz':inworld.tolist(),'source_edge_native_UV':[a.tolist(),b.tolist()],'opening_model_width':op['width'],'installed_geometry':geometry})
registry={'schema':'chooguard.source-pdf-toilet-destinations.v1','source_spec':str(specPath),'source_spec_sha256':sha(specPath),'source_registration':spec['basis']['registration'],'source_registration_sha256':sha(spec['basis']['registration']),'source_scene_epoch_sha256':sha(scene),'source_epoch_is_discovery_only':True,'installed_root':spec['root'],'installed_root_world_matrix':rootMatrix.tolist(),'native_mapping':rootlocal,'entries':rooms,'policy':{'point_ids':[r['point_id']for r in rooms],'source_mouth_only':True,'maximum_nav_3d_distance':.2,'maximum_nav_horizontal_distance':.15,'maximum_nav_vertical_distance':.15,'controller_radius':.3,'controller_height':1.72,'floor_height_tolerance':.08,'probe_route':'complete HallCentre path using original station-points ESC/EV endpoints and canonical physical-verified walking links; no moved endpoint','site_dimensions_verified':False,'source_collider_floor_finish_allowed':True,'underlying_native_floor_unverified_per_spec':True,'new_floor_or_wall_mutation_allowed':False},'acceptance':'ROOT_PROBE_PENDING'}
(O/'floor1-source-pdf-toilet-destinations.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2)+'\n');print('rooms',[(r['room_id'],r['mouth_root_xyz'],len(r['installed_geometry']))for r in rooms]);print('SHA',sha(O/'floor1-source-pdf-toilet-destinations.json'))
