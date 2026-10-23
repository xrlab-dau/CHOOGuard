from pathlib import Path
import json,gzip,struct,hashlib
import numpy as np
from shapely.geometry import Polygon,Point,LineString,MultiPoint
from shapely.ops import polygonize,unary_union
from shapely.geometry.polygon import orient
from shapely import constrained_delaunay_triangles
R=Path('/Users/um-yunsang/CHOOGuard');B=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/nursing-room/constructor';Q=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]])
old=json.loads((B/'NURSING-ROOM-PACKET-R26.json').read_text());nwc=json.loads((R/'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/inputs/CURRENT-WALLS-AND-NATIVE-FEATURE-SECTIONS-R18.json').read_text())
# Fixed actual inside wall long faces, not marked-image rectangle bounds.
lines={
 'left_public_wall174':[[-4.09622014764172,-7.8144216757984655],[-4.055203416043973,-16.209950860403275]],
 'right_wall125':[ [.6642979253868474,-16.09688876034855],[.6329024252582823,-9.670841215353134]],
 'back_wall873':[[-2.745654600690631,-16.113549702300563],[.7542968318490009,-16.096449524312657]],
 'front_wall146':[[-3.0857828742757394,-7.899485467981807],[4.514115604099729,-7.862355068051952]]}
def intersect(aa,bb):
 a,b=np.array(aa);c,d=np.array(bb);A=np.column_stack((b-a,-(d-c)));return (a+(b-a)*np.linalg.solve(A,c-a)[0]).tolist()
ring=[intersect(lines['left_public_wall174'],lines['back_wall873']),intersect(lines['right_wall125'],lines['back_wall873']),intersect(lines['right_wall125'],lines['front_wall146']),intersect(lines['left_public_wall174'],lines['front_wall146'])]
region=Polygon(ring)
snap=json.loads((R/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-001/CURRENT-2F-MESH-SNAPSHOT.json').read_text());cols=[o for o in snap['objects']if any('-'+ix+'-'in o['object_id']for ix in ['1195977420','1688583679'])];need={o['mesh_index']for o in cols};pool={}
with gzip.open(R/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-001/CURRENT-2F-UNIQUE-MESH-GEOMETRY.bin.gz','rb')as f:
 while h:=f.read(8):
  ix,n=struct.unpack('<ii',h);v=np.frombuffer(f.read(n*12),'<f4').reshape(-1,3).copy();ns=struct.unpack('<i',f.read(4))[0];ts=[]
  for k in range(ns):
   top,ni=struct.unpack('<ii',f.read(8));t=np.frombuffer(f.read(ni*4),'<i4').copy()
   if top==0:ts.append(t.reshape(-1,3))
  if ix in need:pool[ix]=(v,np.concatenate(ts))
sourcecol=[]
for c in cols:
 v,t=pool[c['mesh_index']];m=np.array(c['world_matrix_column_major']).reshape(4,4,order='F');v=v@m[:3,:3].T+m[:3,3];q=v[:,[0,2]]@Q.T;segs=[]
 for tri in t:
  vv=v[tri];pp=[]
  for k in range(3):
   a,b=vv[k],vv[(k+1)%3]
   if(a[1]-9.77)*(b[1]-9.77)<0:
    w=a+(b-a)*(9.77-a[1])/(b[1]-a[1]);pp.append(np.round(Q@w[[0,2]],5))
  if len(pp)==2 and np.linalg.norm(pp[0]-pp[1])>1e-7:segs.append(LineString(pp))
 pp=list(polygonize(unary_union(segs)))
 if not pp:raise RuntimeError('Native section cannot form actual closed column profile; do not hull-replace.')
 body=unary_union(pp);sourcecol.append({'id':c['object_id'],'path':c['path'],'cross_section_y':9.77,'actual_profile':[list(p.exterior.coords)for p in([body]if body.geom_type=='Polygon'else body.geoms)]});region=region.difference(body)
# Local X = actualqY+11.5, localZ = actualqX+4.0552; preserve exact inclinations of current wall long faces.
def local_ring(r):return [(y+11.5,x+4.0552)for x,y in r]
poly=Polygon(local_ring(region.exterior.coords),[local_ring(r.coords)for r in region.interiors])
# Ceiling finish is observed. Apertures ONLY in this new lining for existing equipment; no native/floor cuts.
holes=[Polygon([(1.11-.45,1.72-.525),(1.11+.45,1.72-.525),(1.11+.45,1.72+.525),(1.11-.45,1.72+.525)]),Point(1.75,.56).buffer(.178,resolution=40),Point(.02,.58).buffer(.150,resolution=40)]
for x,z in[(1.,.57),(-.22,1.91),(-2.13,3.06)]:holes.append(Point(x,z).buffer(.088,resolution=40))
poly=poly.difference(unary_union(holes));poly=orient(poly,1);y0=2.75;y1=2.79;verts=[];tris=[]
def add(a,b,c):
 k=len(verts);verts.extend([a,b,c]);tris.extend([k,k+1,k+2])
def p(a,y):return [a[0],y,a[1]]
for tri in constrained_delaunay_triangles(poly).geoms:
 a,b,c=list(tri.exterior.coords)[:3]
 if(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])<0:b,c=c,b
 add(p(a,y0),p(b,y0),p(c,y0));add(p(c,y1),p(b,y1),p(a,y1))
for rr in[poly.exterior,*poly.interiors]:
 cc=list(rr.coords)
 for a,b in zip(cc,cc[1:]):
  aa,bb,dd,ee=p(a,y0),p(b,y0),p(b,y1),p(a,y1);add(aa,dd,bb);add(aa,ee,dd)
fixture=json.loads((B/'APPLIED-NURSING-CEILING-BODIES-002.json').read_text());p003={'schema':'chooguard.r26.nursing_observed_low_finish003','author':'classic:/root/r26_nursing_room','scope':'C180_R26_NURSING_ROOM_LOW_CEILING_003','room_root_id':json.loads((B/'APPLIED-NURSING-ROOM-R26.json').read_text())['root_object_id'],'room_root_path':fixture['root_path'].rsplit('/',1)[0],'existing_fixture_group_id':fixture['root_object_id'],'existing_fixture_group_path':fixture['root_path'],'expected_fixture_parts':55,'actual_fixed_wall_faces':lines,'exact_room_inner_ring_actual_q':ring,'native_column_profiles_excluded':sourcecol,'finish_geometry':{'vertices':verts,'triangles':tris},'new_finish_underface_world_y':9.75,'new_finish_top_world_y':9.79,'old_wrong_role_fixture_mount_world_y':float(fixture['mount_world_y']),'fixture_rigid_world_y_delta':9.75-float(fixture['mount_world_y']),'asset_folder':'Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/NursingRoomLowCeiling003','new_finish_name':'SOURCE04_PLAIN_WHITE_DROPPED_ROOM_CEILING_FINISH_003','source':'https://m.blog.naver.com/wonew11/224339225506 FIELD04','source_qualification':'Low finish ceiling visibly just above 2.45mdoorhead and2.24mprivacy screen. 2.75m ownfinishunderside is photo-proportioned MODEL, not measured. Native5 structural ceiling12.1m has a different role and remains unchanged.','bounded_terminal_edge_qualification':'Right wall125 long face extends only as the terminal edge of this own bounded-room lining where its physical wall stops; lining does not claim or cap adjoining staff room. All finish remains on nursing side of actual current wallface axis.','finish_area_model_m2':poly.area,'native_or_floor_cuts':False,'generic_ceiling_tile_grid':False,'created_Lights_Text_TMP':False,'initial242_parts_modified':False,'finish_apertures_count':6}
(B/'NURSING-LOW-CEILING-PACKET-003.json').write_text(json.dumps(p003,separators=(',',':'),allow_nan=False));print('area',poly.area,'verts',len(verts),'holes',len(poly.interiors),'delta',p003['fixture_rigid_world_y_delta'],'sha',hashlib.sha256((B/'NURSING-LOW-CEILING-PACKET-003.json').read_bytes()).hexdigest())
