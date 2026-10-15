from pathlib import Path
import json,hashlib,struct,copy
import numpy as np
from scipy.spatial import cKDTree
from shapely.geometry import Polygon,box
from PIL import Image,ImageDraw
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope');P=O/'namcheon-source-front-packet.json';p=json.loads(P.read_text());hb=lambda b:hashlib.sha256(b).hexdigest();sha=lambda q:hb(Path(q).read_bytes());checks=[]
helpers=Path('workers/twin/evidence/floor2-whole-layout-20261009/seal_native_stair_channels.py').read_text();exec(helpers[helpers.index('def ints'):helpers.index('entries={')])
def check(k,v):checks.append({'id':k,'pass':bool(v)});assert v,k
allpatch=p['mesh_pose_patches']+p['native_wall_clips'];F=np.array(p['native_frame']);I=np.linalg.inv(F)
for r in allpatch:
 key=r.get('mesh_id',r.get('role'));e=r['full_channel_export'];d=read(e);o=r['expected_output'];nd=read({'path':o['path'],'binary_sha256':o['binary_sha256'],'vertex_count':o['vertex_count'],'submesh_count':o['submesh_count'],'blendshapes':0,'boneweights':0,'bindposes':0})
 check(key+' sourcefile',sha(e['asset'])==r['source_live_asset_sha256']);check(key+' source complete',complete(d)==e['complete_channel_digest']);check(key+' output complete',complete(nd)==o['expected_complete_channel_digest']);check(key+' slots',len(d['submeshes'])==len(nd['submeshes']));
 if r['kind']=='rigid_selected_vertices':
  ids=np.array(r['selected_vertex_indices']);un=np.setdiff1d(np.arange(len(d['vertices'])),ids);A=np.array(r['local_rigid_transform']);check(key+' other positions exact',np.array_equal(d['vertices'][un],nd['vertices'][un]));check(key+' rigid selection exact',np.array_equal(nd['vertices'][ids],(d['vertices'][ids].astype(float)@A[:3,:3].T+A[:3,3]).astype('<f4')));check(key+' index exact',all(np.array_equal(a['indices'],b['indices'])for a,b in zip(d['submeshes'],nd['submeshes'])));check(key+' selected membership',len(np.intersect1d(np.unique(np.concatenate([s['indices']for s in d['submeshes']]).reshape(-1,3)[r['selected_triangle_ordinals']]),un))==0);check(key+' UV exact',all(np.array_equal(a['data'],b['data'])and a['dimension']==b['dimension']for a,b in zip(d['uv'],nd['uv'])));
 else:
  count=len(d['vertices']);check(key+' original vertex channels exact',all(np.array_equal(d[k],nd[k][:len(d[k])])for k in ['vertices','normals','tangents','colors']));check(key+' original UV exact',all(np.array_equal(a['data'],b['data'][:len(a['data'])])and a['dimension']==b['dimension']for a,b in zip(d['uv'],nd['uv'])));changed={x['global_triangle_ordinal']for x in r['changed_source_faces']};check(key+' exact three native source faces',changed=={30319,30320,30324});
  # Recover all retained original triangles by their indices; appended indices identify only declared fragments.
  old=np.concatenate([s['indices']for s in d['submeshes']]).reshape(-1,3);new=np.concatenate([s['indices']for s in nd['submeshes']]).reshape(-1,3);oldkept=old[[i for i in range(len(old))if i not in changed]];newkept=new[np.all(new<count,axis=1)];check(key+' all other source triangles exact',np.array_equal(oldkept,newkept));
  for j,x in enumerate(r['changed_source_faces']):
   for z in x['generated_vertices']:
    bary=np.array(z['source_barycentric_weights']);ix=np.array(z['source_indices']);ni=z['vertex_index'];check(key+f' barycorner{j}-{ni}',np.array_equal(nd['vertices'][ni],(bary@d['vertices'][ix].astype(float)).astype('<f4')))
  area=sum(x['removed_area_model2']for x in r['changed_source_faces']);expected=7*3.44 if key.endswith('render')else 1.8*2.355;check(key+' removed bounded area',abs(area-expected)<1e-7)
check('door all fields preserved',p['door_parent_pose']['station_fields_before']==p['door_parent_pose']['station_fields_after']);check('door whole parent-local handling',p['door_parent_pose']['runtime_fields_are_parent_local_and_preserved']);check('floor not appended',p['summary']['source_floor_append_count']==0);check('no gallery duplicated',p['summary']['extra_gallery_or_escalator_count']==0);check('source floor supports25',all(any(abs(z['Y']-7)<.00005 for z in c['supports'])for c in json.load(open(p['source_support']['path']))['samples']))
# Exact source floor and overhead deck match to current source triangles, independent of photo metric assumptions.
source=json.load(open(E/'source-gallery-geometry.json'));floor=next(r for r in source['regions']['namcheon-gallery']if r['source_path']=='ROOT/g58/g32/g4/geometry');roof=next(r for r in source['regions']['namcheon-gallery']if r['source_path']=='ROOT/g58/g0/geometry');native=p['native_wall_clips'][0];d=read(native['full_channel_export']);M=np.array(native['owner_receipt']['owner']['world_matrix']);q=((d['vertices']@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]];tr=q[np.concatenate([s['indices']for s in d['submeshes']]).reshape(-1,3)];tree=cKDTree(tr.mean(1));cor=[]
for row,fi in [(floor,33)]:
 st=np.array(row['native_vertices'])[row['source_polygons'][fi]];cand=tree.query_ball_point(st.mean(0),.0005);valid=[]
 for i in cand:
  dis=np.linalg.norm(tr[i][:,None,:]-st[None,:,:],axis=2);err=max(dis.min(0).max(),dis.min(1).max())
  if err<.0005:valid.append({'current_triangle':i,'maximum_vertex_error_model':float(err)})
 check('positive original floor exact current owner',len(valid)==1);cor.append({'source_id':row['source_id'],'source_path':row['source_path'],'source_face':fi,'source_material_slots':row['material_slots'],'current_owner_global_id':native['owner_receipt']['owner']['global_object_id'],'matches':valid})
report={'schema':'chooguard.namcheon-packet-preflight.v1','packet':str(P),'packet_sha256':sha(P),'checks':checks,'passed':sum(z['pass']for z in checks),'failed':sum(not z['pass']for z in checks),'source_floor_current_correspondence':cor,'current_source_floor_geometry_independently_positive':True,'camera_fit_accepted':False,'production_or_Unity_called':False};(E/'namcheon-preflight.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print('PREFLIGHT',report['passed'],report['failed'])
# Inspection overlays provide pixels, not camera correspondences.
img=Image.open('asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-nose-008.jpg').convert('RGB');dd=ImageDraw.Draw(img);regions={'adjacent connector':[0,190,966,1090],'shared steel jamb':[914,188,1068,914],'Namcheon storefront':[1038,315,1435,858]}
for label,b in regions.items():dd.rectangle(b,outline='yellow',width=4);dd.text((b[0]+8,b[1]+8),label,fill='yellow')
img.save(E/'nose008-front-relation-overlay.jpg')
# Exact native source plan overlay before/after.
im=Image.new('RGB',(1500,1200),'white');dd=ImageDraw.Draw(im);xmin,xmax,vmin,vmax=-45,-25,-7,24
px=lambda u,v:((u-xmin)*65+75,(v-vmin)*34+50)
for u in range(-45,-24,2):dd.line([px(u,vmin),px(u,vmax)],fill=(225,225,225));dd.text(px(u,vmin),str(u),fill='black')
for v in range(-6,25,2):dd.line([px(xmin,v),px(xmax,v)],fill=(225,225,225));dd.text(px(xmin,v),str(v),fill='black')
old=[[-40,-3.31],[-33,-3.31],[-33,3.69],[-40,3.69]];DL=np.array(p['native_local_xyz_transform']);new=[]
for u,v in old:
 z=DL@np.array([v,7,u,1]);new.append([z[2],z[0]])
dd.line([px(*x)for x in old+[old[0]]],fill='red',width=5);dd.line([px(*x)for x in new+[new[0]]],fill='green',width=5);a,b=p['target_frontline_V_from_U'];dd.line([px(-43.084,a*-43.084+b),px(-29.221,a*-29.221+b)],fill='blue',width=3);dd.line([px(-32.8,a*-32.8+b),px(-29.95,a*-29.95+b)],fill='purple',width=12)
dd.text((35,10),'Exact native UV plan; red current26; green rigid candidate26; blue original wall; purple accepted adjacent connector; model dimensions preserved, no field metric claim',fill='black');dd.text(px(-39.8,-3),'Current26',fill='red');dd.text(px(-39.8,13),'Candidate26',fill='green');dd.text(px(-32.6,11),'Connector',fill='purple');im.save(E/'namcheon-native-before-after-plan.png')
# Provenance bundle remains bounded.
(E/'photo-world-controls.json').write_text(json.dumps({'photos':p['photos'],'inspected_pixel_regions':{'nose008':regions,'nose006':{'Namcheon_lettering':[2120,1720,2750,1950],'up_gallery_bank':[3650,1520,4032,2210]}},'exact_current_native_world_frontpoints':list(zip(p['front_endpoints_native_UVY'],p['front_endpoints_world'])),'accepted_2D_to_3D_correspondences':[],'camera':p['camera_fit'],'pixel_regions_are_not_metric_controls':True},ensure_ascii=False,indent=2)+'\n')
