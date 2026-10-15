"""Offline Unit8 region-only append mesh packet. Never writes Unity/Assets."""
from pathlib import Path
import json,re,struct,hashlib,yaml,math
import numpy as np
from shapely import constrained_delaunay_triangles
from shapely.geometry import Polygon,LineString,Point,mapping
from shapely.ops import unary_union
from shapely.affinity import translate

ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'workers/twin/output/floor1-multiview-next-20261009'
OUT=BASE/'unit8-rebuild';PIECES=OUT/'pieces';PIECES.mkdir(exist_ok=True)
EX=ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/current-before-unit8/current-scene-structure.json'
D=json.loads(EX.read_text());MM={x['id']:x for x in D['meshes']}
RV=json.loads((BASE/'remaining-guide-coverage/constrained-proposals8-review.json').read_text())
MASK=json.loads((OUT/'owner-mask-inspection.json').read_text())
DY=json.loads((OUT/'door-native-yaml.json').read_text())
OLD=Polygon(RV['current_polygon_UV']);NEW=Polygon(RV['proposed_polygon_UV']);COMMON=OLD.intersection(NEW)
OP=np.array(RV['current_polygon_UV']);NP=np.array(RV['proposed_polygon_UV'])
C=.9602937;S=.278991133;UP=np.array([0.,1.,0.]);FRAME=np.array([[C,0,S],[0,1,0],[-S,0,C]])
P=lambda q,y=0:np.array([q[1],y,q[0]],float)
UV=lambda w:np.column_stack([S*w[:,0]+C*w[:,2],C*w[:,0]-S*w[:,2]])
sha=lambda b:hashlib.sha256(b).hexdigest()
def world(r,v):
 m=np.array(r['world_matrix']);return v@m[:3,:3].T+m[:3,3]
cache={}
def binary(mid):
 if mid not in cache:
  raw=(ROOT/MM[mid]['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',raw)
  cache[mid]=(np.frombuffer(raw,'<f4',nv*3,12).reshape(nv,3).astype(float),np.frombuffer(raw,'<i4',ni,12+nv*12).reshape(-1,3))
 return cache[mid]
assets={}
def asset(mid):
 if mid not in assets:
  f=ROOT/MM[mid]['asset'];body=f.read_bytes();a=yaml.safe_load(body.decode().split('Mesh:\n',1)[1]);raw=bytes.fromhex(a['m_VertexData']['_typelessdata']);nv=a['m_VertexData']['m_VertexCount'];stride=len(raw)//nv
  assert all(x['stream']==0 and x['format']==0 for x in a['m_VertexData']['m_Channels'] if x['dimension'])
  channels={i:np.ndarray((nv,x['dimension']),dtype='<f4',buffer=raw,offset=x['offset'],strides=(stride,4)).copy() for i,x in enumerate(a['m_VertexData']['m_Channels']) if x['dimension']}
  dtype='<u4' if a['m_IndexFormat']==1 else '<u2';tri=np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),dtype).reshape(-1,3).astype(int)
  assets[mid]=(a,raw,stride,channels,tri,sha(body))
 return assets[mid]
def dump(name,d):
 p=OUT/name;p.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n');return str(p.relative_to(ROOT))

class Piece:
 def __init__(self):self.v=[];self.n=[];self.uv=[];self.t=[];self.tan=[];self.discarded_tiny_triangle_area=0.
 def tri(self,pts,uvs,n):
  pts=np.array(pts,float);uvs=np.array(uvs,float);n=np.array(n,float);n/=np.linalg.norm(n)
  area=np.linalg.norm(np.cross(pts[1]-pts[0],pts[2]-pts[0]))/2
  if area<1e-8:self.discarded_tiny_triangle_area+=float(area);return
  if np.dot(np.cross(pts[1]-pts[0],pts[2]-pts[0]),n)<0:pts=pts[[0,2,1]];uvs=uvs[[0,2,1]]
  base=len(self.v);self.v.extend(pts.tolist());self.uv.extend(uvs.tolist());self.n.extend([n.tolist()]*3);self.t.append([base,base+1,base+2])
 def quad(self,pts,uvs,n):
  pts=np.array(pts,float);uvs=np.array(uvs,float);n=np.array(n,float);n/=np.linalg.norm(n);base=len(self.v)
  self.v.extend(pts.tolist());self.uv.extend(uvs.tolist());self.n.extend([n.tolist()]*4)
  self.t.extend([[base,base+1,base+2],[base,base+2,base+3]] if np.dot(np.cross(pts[1]-pts[0],pts[2]-pts[0]),n)>=0 else [[base,base+2,base+1],[base,base+3,base+2]])
 def rect(self,o,ex,ey,w,h,n,uv0=(0,0)):
  o=np.array(o);ex=np.array(ex);ey=np.array(ey);u=np.array(uv0)
  self.quad([o,o+ex*w,o+ex*w+ey*h,o+ey*h],[u,u+[w,0],u+[w,h],u+[0,h]],n)
 def box(self,c,x,y,z,size,bottom=True,back=True):
  c=np.array(c);x=np.array(x);y=np.array(y);z=np.array(z);sx,sy,sz=size;hx=x*sx/2;hy=y*sy/2;hz=z*sz/2
  self.rect(c-hx-hy+hz,x,y,sx,sy,z)
  if back:self.rect(c-hx-hy-hz,x,y,sx,sy,-z)
  self.rect(c+hx-hy-hz,z,y,sz,sy,x);self.rect(c-hx-hy-hz,z,y,sz,sy,-x)
  self.rect(c-hx+hy-hz,x,z,sx,sz,y)
  if bottom:self.rect(c-hx-hy-hz,x,z,sx,sz,-y)
 def fill(self,poly,y,n,origin,angle=0):
  co,si=np.cos(angle),np.sin(angle);origin=np.array(origin)
  for tr in constrained_delaunay_triangles(poly).geoms:
   q=np.array(tr.exterior.coords)[:3];delta=q-origin;uv=np.column_stack([delta[:,0]*co+delta[:,1]*si,-delta[:,0]*si+delta[:,1]*co]);self.tri([P(x,y) for x in q],uv,n)
 def copied(self,mid,faces,translation=np.zeros(3)):
  a,raw,stride,ch,t,h=asset(mid);ids=sorted(set(t[faces].flatten().tolist()));imap={i:len(self.v)+k for k,i in enumerate(ids)}
  self.v.extend((ch[0][ids]+translation).tolist());self.n.extend(ch[1][ids].tolist());self.tan.extend(ch[2][ids].tolist());self.uv.extend(ch[4][ids].tolist());self.t.extend([[imap[int(i)] for i in f] for f in t[faces]])
 def finish(self):
  v=np.array(self.v,float);n=np.array(self.n,float);uv=np.array(self.uv,float);t=np.array(self.t,int)
  if not len(t):return
  if len(self.tan)!=len(v):
   ta=np.zeros_like(v);bi=np.zeros_like(v)
   for tri in t:
    e1,e2=v[tri[1]]-v[tri[0]],v[tri[2]]-v[tri[0]];d1,d2=uv[tri[1]]-uv[tri[0]],uv[tri[2]]-uv[tri[0]];det=d1[0]*d2[1]-d1[1]*d2[0]
    if abs(det)<1e-10:raise ValueError('Degenerate UV triangle')
    tan=(e1*d2[1]-e2*d1[1])/det;bit=(e2*d1[0]-e1*d2[0])/det
    for k in tri:ta[k]+=tan;bi[k]+=bit
   ta-=n*(ta*n).sum(1)[:,None];ta/=np.linalg.norm(ta,axis=1)[:,None];sg=np.where((np.cross(n,ta)*bi).sum(1)<0,-1.,1.);self.tan=np.column_stack([ta,sg]).tolist()
  self.v=np.array(self.v,'<f4').tolist();self.n=np.array(self.n,'<f4').tolist();self.uv=np.array(self.uv,'<f4').tolist();self.tan=np.array(self.tan,'<f4').tolist()
 def payload(self):
  self.finish();return {'frame':'native station localXYZ=(V,Y,U)','vertices':self.v,'normals':self.n,'tangents':self.tan,'uv0':self.uv,'triangles':self.t,'submesh':0}

ROWS={x['mesh']['id']:x for x in MASK['owners']}
for mid,row in ROWS.items():
 row['mesh']=MM[mid];row['owner']=next(x for x in D['renderers']+D['colliders'] if x['global_object_id']==row['owner']['global_object_id'])
B={mid:Piece() for mid in ROWS}
def ensure_mask(mid,path,mode,expected):
 r=next(x for x in D['renderers'] if x['path']==path);v,t=binary(mid);q=UV(world(r,v));yy=world(r,v)[:,1]
 line=LineString([OP[4],OP[5],OP[6]])
 fs=[i for i,f in enumerate(t) if all(OLD.buffer(.045).covers(Point(z)) for z in q[f])] if mode=='shop' else [i for i,f in enumerate(t) if all(line.distance(Point(z))<.36 for z in q[f]) and abs(float(yy[f].mean())-2.994)<.002]
 assert len(fs)==expected,(mid,len(fs),expected)
 a,raw,stride,ch,tr,bodyhash=asset(mid)
 ROWS[mid]={'owner':r,'mesh':MM[mid],'face_ordinals':fs,'vertex_indices':sorted(set(t[fs].flatten().tolist())),'channels':a['m_VertexData']['m_Channels'],'submeshes':a['m_SubMeshes'],'asset_sha256_at_read':bodyhash,'mask_reason':mode+' downlight annuli'};B[mid]=Piece()
ensure_mask('mesh-0573','실내 트윈 마감/점포 내부 1F/Kit_CeilingGrid','shop',320)
ensure_mask('mesh-0571','실내 트윈 마감/1F/Kit_CeilingGrid','front',400)

# Actual authored door anchor, aligned to the new front tangent closest oldAlong.
sd=DY['StationDoor'];localcentre=np.array([sd['Centre'][k] for k in ['x','y','z']]);oldcentre=np.array([localcentre[2],localcentre[0]])
frontA,frontZ=NP[1],NP[0];frontdelta=frontZ-frontA;L=np.linalg.norm(frontdelta);exuv=frontdelta/L
doorat=float(np.dot(oldcentre-frontA,exuv));doorat=np.clip(doorat,.9,L-.9);doorcentre=frontA+exuv*doorat
ex=P(exuv);n=np.array([-ex[2],0,ex[0]]);assert n[2]>0
normaluv=np.array([n[2],n[0]]);doorends=[doorcentre-exuv*.9,doorcentre+exuv*.9]
A=P(frontA);H=3.;tr=2.55;mw=.05;md=.12;kick=.15;gap=(doorat-.9,doorat+.9)

# Wall envelope: all non-front edges, double-sided existing plaster material, full4.8 height.
area_sign=1 if NEW.exterior.is_ccw else -1
for i in range(len(NP)):
 if i==0:continue
 a,z=NP[i],NP[(i+1)%len(NP)];du=(z-a)/np.linalg.norm(z-a);inn=np.array([-du[1],du[0]])*area_sign
 o=P(a);ed=P(du);length=np.linalg.norm(z-a);out=-P(inn)
 B['mesh-2088'].rect(o,ed,UP,length,4.8,out);B['mesh-2088'].rect(o,ed,UP,length,4.8,-out)
 # Preserve source internal finishes: the native end/core-adjacent edge8 keeps wood.
 inside=P(inn);origin=o+inside*.02+UP*.1;height=2.9
 if i==8:
  xs=[0]+[k*1.2 for k in range(1,int(length/1.2)+1) if k*1.2<length-.001]+[length]
  for x0,x1 in zip(xs,xs[1:]):
   gap2=.002;pw=x1-x0-.004;ph=height-.004;po=origin+ed*(x0+gap2)+UP*gap2;wood=B['mesh-0924'];jo=B['mesh-2307']
   wood.rect(po,ed,UP,pw,ph,inside,(x0+gap2,.1+gap2));back=po-inside*.01
   wood.rect(back,ed,inside,pw,.01,-UP);wood.rect(back+UP*ph,ed,inside,pw,.01,UP);wood.rect(back,UP,inside,ph,.01,-ed);wood.rect(back+ed*pw,UP,inside,ph,.01,ed)
   jo.rect(origin+ed*x0-inside*.012,ed,UP,x1-x0,height,inside)
 else:B['mesh-0124'].rect(origin,ed,UP,length,height,inside,(0,.1))
 B['mesh-2307'].box(o+inside*.026+ed*length/2+UP*.05,ed,UP,inside,[length,.1,.012],False,False)

# Existing granite and GridWhite ceiling channels are retained; align floor UV to old recipe.
oldfront=OP[6]-OP[5];angle=math.atan2(oldfront[1],oldfront[0])
B['mesh-0551'].fill(NEW,.004,UP,OP[5],angle)
B['mesh-0907'].fill(NEW,3.,-UP,OP[5],0)

# Rebuild one coherent static frontage, leaving exactly the existing nominal1.8 gap.
runs=[(0,gap[0]),(gap[1],L)];posts=[]
for lo,hi in runs:
 bays=max(1,int(np.rint((hi-lo)/1.5)));posts.extend(np.linspace(lo,hi,bays+1).tolist())
posts=sorted(set(round(x,9) for x in posts+[gap[0],gap[1]]))
for x in posts:B['mesh-0200'].box(A+ex*x+UP*H/2,ex,UP,n,[mw,H,md])
B['mesh-0200'].box(A+ex*L/2+UP*(H-.04),ex,UP,n,[L,.08,md+.01])
B['mesh-0200'].box(A+ex*L/2+UP*tr,ex,UP,n,[L,.07,md])
for x0,x1 in zip(posts,posts[1:]):
 s0=x0+mw/2;s1=x1-mw/2;sm=(s0+s1)/2;w=s1-s0
 if w<=.01:continue
 if not gap[0]<sm<gap[1]:
  B['mesh-2613'].box(A+ex*sm+UP*kick/2,ex,UP,n,[w,kick,.04])
  B['mesh-0200'].box(A+ex*sm+UP*(kick+.025),ex,UP,n,[w,.05,md])
  for side in [n,-n]:B['mesh-2355'].rect(A+ex*s0+UP*.2,ex,UP,w,tr-.035-kick-.05,side)
  for side in [n,-n]:B['mesh-3455'].rect(A+ex*s0,ex,UP,w,H,side)
 for side in [n,-n]:B['mesh-2355'].rect(A+ex*s0+UP*(tr+.035),ex,UP,w,H-.08-tr-.035,side)
lo=max(0,doorat-1.8);hi=min(L,doorat+1.8)
B['mesh-2613'].box(A+ex*(lo+hi)/2+UP*(2.515+.09)-n*.15,ex,UP,n,[hi-lo,.18,.12])
B['mesh-2738'].box(A+ex*L/2+UP*3.3+n*.1,ex,UP,n,[L,.6,.30])
# Exact source gypsum bulkhead construction:1.2 panel pitch/.008 joint,3.6→4.8.
xs=[0]+[k*1.2 for k in range(1,int(L/1.2)+1) if k*1.2<L-.001]+[L]
for x0,x1 in zip(xs,xs[1:]):
 po=A+ex*(x0+.004)+UP*3.604;w=x1-x0-.008;h=1.192;sk=B['mesh-0203'];jo=B['mesh-0886']
 sk.rect(po,ex,UP,w,h,n,(x0+.004,3.604));back=po-n*.01
 sk.rect(back,ex,n,w,.01,-UP);sk.rect(back+UP*h,ex,n,w,.01,UP);sk.rect(back,UP,n,h,.01,-ex);sk.rect(back+ex*w,UP,n,h,.01,ex)
 jo.rect(A+ex*x0+UP*3.6-n*.012,ex,UP,x1-x0,1.2,n)

# Reposition only the existing18 downlight discs and their annuli, retaining source bytes/UVs.
LIGHT_QA=[]
oldline=LineString([OP[4],OP[5],OP[6]])
for mid,groupn,kind in [('mesh-1940',20,'shop'),('mesh-0573',40,'shop'),('mesh-0310',20,'front'),('mesh-0571',40,'front')]:
 row=ROWS[mid];fs=row['face_ordinals'];assert len(fs)%groupn==0;aa,raw,st,ch,t,hs=asset(mid)
 for k in range(0,len(fs),groupn):
  faces=fs[k:k+groupn];ids=sorted(set(t[faces].flatten().tolist()));q=ch[0][ids][:,[2,0]].astype(float);centre=q.mean(0)
  if kind=='front':
   distance=oldline.project(Point(centre));newq=frontA+exuv*(distance/oldline.length*L)+normaluv*.125
  else:
   safe=COMMON.buffer(-.10);point=Point(centre)
   if safe.covers(point):newq=centre
   else:
    from shapely.ops import nearest_points
    newq=np.array(nearest_points(safe,point)[0].coords[0])
  delta=P(newq-centre);B[mid].copied(mid,faces,delta)
  LIGHT_QA.append({'mesh':mid,'source_faces':faces,'before_centre_UV':centre.tolist(),'after_centre_UV':newq.tolist(),'kind':kind,'contents_count_preserved':True})

# Native top-floor coverage, source faces retained. Fill only the exact missing flush region.
native_receipt=json.loads((BASE/'remaining-guide-coverage/native-original-floor-support.json').read_text())
support=[];floorowners=[]
for f in native_receipt['floor_owners']:
 r=next(x for x in D['renderers'] if x['global_object_id']==f['global_object_id'])
 col=next((x for x in D['colliders'] if x['path']==r['path'] and x.get('mesh')==r['mesh'] and x.get('active',True) and x.get('enabled',True) and not x.get('trigger')),None)
 if not col:continue
 v,t=binary(r['mesh']);w=world(col,v);q=UV(w);parts=[];faces=[]
 for i in f['selected_floor_face_ordinals']:
  trw=w[t[i]]
  if np.max(abs(trw[:,1]))>.002:continue
  if np.cross(trw[1]-trw[0],trw[2]-trw[0])[1]<=1e-10:continue
  p=Polygon(q[t[i]])
  if p.is_valid and p.area>1e-8 and p.intersection(NEW).area>1e-8:parts.append(p);faces.append(int(i))
 if parts:
  u=unary_union(parts);support.extend(parts);selected=w[t[faces]];cross=np.cross(selected[:,1]-selected[:,0],selected[:,2]-selected[:,0]);normals=cross/np.linalg.norm(cross,axis=1)[:,None]
  floorowners.append({'owner':r,'collider':col,'mesh':MM[r['mesh']],'faces':faces,'worldY_tolerance':.002,'actual_worldY_range':[float(selected[:,:,1].min()),float(selected[:,:,1].max())],'minimum_up_normal_y':float(normals[:,1].min()),'covered_area':u.intersection(NEW).area})
nativefloor=unary_union(support);missing=NEW.difference(nativefloor)
floor_tolerance=2e-5;assert NEW.difference(nativefloor.buffer(floor_tolerance)).area<1e-10
lo,hi=0.,floor_tolerance
for _ in range(30):
 mid=(lo+hi)/2
 if NEW.difference(nativefloor.buffer(mid)).area<1e-12:hi=mid
 else:lo=mid
required_floor_buffer=hi
float32_corner_epsilon=float(np.max(abs(np.spacing(NP.astype('<f4')))))

# Whole authored door root moves rigidly; leaf geometry/local poses/fields/slides remain unchanged.
oldalong=np.array([sd['Along'][k] for k in ['x','y','z']]);oldalongworld=FRAME@oldalong;newalongworld=FRAME@ex
theta=math.atan2(-newalongworld[2],newalongworld[0])-math.atan2(-oldalongworld[2],oldalongworld[0])
qrot=[0.,math.sin(theta/2),0.,math.cos(theta/2)];Q=np.array([[math.cos(theta),0,math.sin(theta)],[0,1,0],[-math.sin(theta),0,math.cos(theta)]])
oldworldcentre=FRAME@localcentre;newworldcentre=FRAME@P(doorcentre);translation=newworldcentre-Q@oldworldcentre
motions=[{'operation':'world_rigid_delta','path':'실내 트윈 마감/1F/문 · 양산국밥 출입문','gameobject_file_id':DY['gameobject_file_id'],'transform_file_id':DY['transform_file_id'],'StationDoor_component_file_id':DY['StationDoor_component_file_id'],
 'quaternion_world_xyzw':qrot,'translation_world':translation.tolist(),'before_world_matrix':np.vstack([np.column_stack([FRAME,np.zeros(3)]),[0,0,0,1]]).tolist(),
 'actual_old_centre_world':oldworldcentre.tolist(),'target_centre_world':newworldcentre.tolist(),'target_centre_UV':doorcentre.tolist(),
 'preserve_all_StationDoor_local_fields':True,'preserve_leaf_meshes_vertices_materials_colliders_and_local_poses':True,'preserve_leaf_slide_vectors':True}]

# Exact scene transforms for prefab roots and the existing fixed-size fascia sign.
scene=(ROOT/D['scene']).read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',scene,re.M|re.S)}
def transform_for_gameobject(go):
 g=yaml.safe_load(blocks[go])['GameObject'];i=next(x['component']['fileID'] for x in g['m_Component'] if blocks[x['component']['fileID']].startswith('Transform:\n'));return i,yaml.safe_load(blocks[i])['Transform']
def parentpose(go):
 i,tf=transform_for_gameobject(go);p=np.array([tf['m_LocalPosition'][k] for k in ['x','y','z']]);return i,tf,FRAME@p

# Actual readable prefab hulls, not guide-invented furniture. Prefer keeping valid placements.
fixture=[]
for r in D['renderers']:
 if '/SHOP-1F-08' not in r['path']:continue
 if MM[r['mesh']].get('binary'):
  v,t=binary(r['mesh']);hull=Polygon(UV(world(r,v))).convex_hull;footprint_method='readable source mesh hull'
 else:
  lo,hi=r['world_bounds_min'],r['world_bounds_max'];corners=np.array([[x,0,z] for x,z in [(lo[0],lo[2]),(hi[0],lo[2]),(hi[0],hi[2]),(lo[0],hi[2])]])
  hull=Polygon(UV(corners));footprint_method='conservative actual renderer world-AABB projected to stationUV; prefab mesh unchanged'
 name=r['path'].split('/')[2];go=next(x['gameobject_file_id'] for x in DY['matching_gameobjects'] if x['name']==name);ti,tf,pos=parentpose(go)
 fixture.append({'renderer':r,'hull':hull,'name':name,'gameobject':go,'tfid':ti,'transform':tf,'position':pos,'footprint_method':footprint_method})
door_keepout=Polygon([doorcentre+exuv*x+normaluv*z for x,z in [(-1.2,-1.5),(1.2,-1.5),(1.2,1.5),(-1.2,1.5)]])
fixed=[];move=[]
for f in fixture:
 if COMMON.buffer(-.10).covers(f['hull']) and not f['hull'].intersects(door_keepout.buffer(.10)):fixed.append(f)
 else:move.append(f)
occupied=[f['hull'].buffer(.10) for f in fixed];FIXTURE_QA=[]
for f in fixed:FIXTURE_QA.append({'name':f['name'],'renderer_id':f['renderer']['global_object_id'],'operation':'preserve','footprint_UV':mapping(f['hull']),'outside_common_area':0.,'footprint_method':f['footprint_method']})
for f in sorted(move,key=lambda x:x['name']):
 choices=[]
 for du in np.arange(-4,4.001,.1):
  for dv in np.arange(-4,4.001,.1):
   if du*du+dv*dv<1e-12:continue
   hp=translate(f['hull'],xoff=float(du),yoff=float(dv))
   if not COMMON.buffer(-.10).covers(hp) or hp.intersects(door_keepout.buffer(.10)) or any(hp.intersects(x) for x in occupied):continue
   choices.append((du*du+dv*dv,float(du),float(dv),hp))
 assert choices,'No bounded existing-fixture placement: '+f['name']
 _,du,dv,hp=min(choices,key=lambda x:(x[0],x[1],x[2]));occupied.append(hp.buffer(.10));dw=FRAME@P([du,dv])
 motions.append({'operation':'world_rigid_delta','path':'실내 트윈 마감/점포 내부 1F/'+f['name'],'gameobject_file_id':f['gameobject'],'transform_file_id':f['tfid'],'quaternion_world_xyzw':[0.,0.,0.,1.],
  'translation_world':dw.tolist(),'before_position_world':f['position'].tolist(),'after_position_world':(f['position']+dw).tolist(),'preserve_rotation_scale_children_and_source_meshes':True})
 FIXTURE_QA.append({'name':f['name'],'renderer_id':f['renderer']['global_object_id'],'operation':'translate','delta_UV':[du,dv],'footprint_UV':mapping(hp),'outside_common_area':hp.difference(COMMON).area,'footprint_method':f['footprint_method']})

sign=next(x for x in DY['matching_gameobjects'] if x['name']=='Sign · 양산국밥');ti,tf,pos=parentpose(sign['gameobject_file_id'])
signTarget=FRAME@(A+ex*L/2+UP*3.3+n*.254)
motions.append({'operation':'world_rigid_delta','path':'실내 트윈 마감/1F/Sign · 양산국밥','gameobject_file_id':sign['gameobject_file_id'],'transform_file_id':ti,'quaternion_world_xyzw':qrot,'translation_world':(signTarget-Q@pos).tolist(),'before_position_world':pos.tolist(),'after_position_world':signTarget.tolist(),'preserve_text_font_materials_and_scale':True})

# Original active native floors support the entire target within the existing float tolerance.
# No numerical-sliver support geometry is created, and Ground_Floor/ContextPlaza stay byte exact.
for old_artifact in ['pieces/mesh-2687.json','pieces/unit8-flush-support.json']:(OUT/old_artifact).unlink(missing_ok=True)
for motion in motions:motion['world_rigid_delta']={'rotation':motion['quaternion_world_xyzw'],'translation':motion['translation_world']}

# Current exact mask channels/materials and append-ready payloads. Original arrays never rewritten.
replacements=[];piecechecks=[]
for mid,row in ROWS.items():
 piece=B[mid];pd=piece.payload();a,raw,stride,ch,t,bodyhash=asset(mid);fs=row['face_ordinals'];ids=sorted(set(t[fs].flatten().tolist()));nv=len(ch[0]);keep=sorted(set(range(len(t)))-set(fs))
 snapv,snapt=binary(mid);assert np.array_equal(t[fs],snapt[fs]),mid+' selected triangle order changed'
 if ids:assert np.max(abs(ch[0][ids]-snapv[ids]))<2e-5,mid+' selected source positions changed since export'
 vf=np.array(pd['vertices']);nf=np.array(pd['normals']);tt=np.array(pd['triangles']);tn=np.array(pd['tangents']);cross=np.cross(vf[tt[:,1]]-vf[tt[:,0]],vf[tt[:,2]]-vf[tt[:,0]]);areas=np.linalg.norm(cross,axis=1)/2
 assert areas.min()>1e-9 and np.min(np.sum(cross*nf[tt[:,0]],axis=1))>0
 assert np.max(abs(np.linalg.norm(nf,axis=1)-1))<2e-5 and np.max(abs((tn[:,:3]*nf).sum(1)))<2e-5
 # Full original vertex bytes/UVs/normals/tangents and retained index order remain untouched.
 selectedbytes=b''.join(raw[i*stride:(i+1)*stride] for i in ids);keepindexbytes=np.array(t[keep],'<u4').tobytes()
 owners=[x for x in D['renderers']+D['colliders'] if x.get('mesh')==mid]
 pf=dump('pieces/'+mid+'.json',pd)
 materials=row['owner'].get('materials',[])
 replacements.append({'source_mesh_id':mid,'source_asset':row['mesh']['asset'],'source_asset_guid':row['mesh']['asset_guid'],'source_binary_sha256':row['mesh']['binary_sha256'],
  'source_asset_file_sha256_at_read':bodyhash,'source_vertex_count':nv,'source_triangle_count':len(t),'source_channels':a['m_VertexData']['m_Channels'],'source_submeshes':a['m_SubMeshes'],
  'owners':owners,'source_materials':materials,'removed_triangle_ordinals':fs,'selected_triangle_indices':t[fs].tolist(),'selected_index_bytes_sha256':sha(np.array(t[fs],'<u4').tobytes()),'source_index_format':a['m_IndexFormat'],'selected_vertex_indices':ids,'selected_vertex_channel_bytes_sha256':sha(selectedbytes),'retained_index_bytes_sha256':sha(keepindexbytes),
  'preserve_original_vertex_channel_bytes':True,'preserve_unrelated_triangle_order_indices_and_materials':True,'allow_rebase_unrelated_vertices_if_selected_source_bytes_match':True,
  'append_piece':pf,'append_vertices':pd['vertices'],'append_normals':pd['normals'],'append_tangents':pd['tangents'],'append_uv0':pd['uv0'],
  'added_triangles':[[nv+i for i in tri] for tri in pd['triangles']],'added_triangles_submesh':0,'new_owner_required':False})
 piecechecks.append({'mesh':mid,'removed_triangles':len(fs),'kept_triangles':len(keep),'added_vertices':len(vf),'added_triangles':len(tt),'min_triangle_area':float(areas.min()),'discarded_tiny_triangle_area':piece.discarded_tiny_triangle_area,'source_selected_snapshot_match':True,'windings_normals_tangents_pass':True,'unrelated_index_sequence_preserved':True})

packet={'schema':'chooguard.unit8-constrained-guide-offline-rebuild.v1','status':'OFFLINE_GEOMETRY_READY','source_export':str(EX.relative_to(ROOT)),'source_export_scene_sha256':D['scene_file_sha256'],
 'guide_source':RV['guide_source'],'guide_image_sha256':RV['guide_image_sha256'],'reference_only':True,'physical_accuracy_verified':False,
 'frame':'stationUV; all append vertices native localXYZ=(V,Y,U), existing16.2degree frame preserved','current_polygon_UV':OP.tolist(),'proposed_polygon_UV':NP.tolist(),
 'mesh_replacements':replacements,'new_mesh_pieces':[],'native_floor_append_required':False,'world_rigid_motions':motions,
 'door':{'element':'1F-unit08-front5','component_file_id':DY['StationDoor_component_file_id'],'root_transform_file_id':DY['transform_file_id'],
  'front_edge_indices':[0,1],'front_geometry_orientation_UV':[frontA.tolist(),frontZ.tolist()],'old_actual_centre_UV':oldcentre.tolist(),'new_centre_UV':doorcentre.tolist(),'new_along_UV':exuv.tolist(),'new_outward_normal_UV':normaluv.tolist(),
  'nominal_aperture_endpoints_UV':[x.tolist() for x in doorends],'nominal_width':1.8,'existing_leaf_height':sd['Height'],'static_frame_clear_width':1.75,'existing_fully_open_leaf_clear_width_approx':1.77,
  'authored_local_fields_and_slides_unchanged':True,'moving_leaf_renderer_ids':[1697899130,1443537033],'moving_leaf_collider_ids':[1697899128,1443537031]},
 'floor_support':{'native_top_floor_owners':floorowners,'native_supported_area':NEW.intersection(nativefloor).area,'new_flush_support_area':0.,'complete_area':NEW.area,'added_footprint_area':NEW.difference(OLD).area,
  'raw_uncovered_area':missing.area,'raw_uncovered_geometry_UV':mapping(missing),'max_required_native_floor_buffer_model_units':required_floor_buffer,'float32_corner_spacing_model_units':float32_corner_epsilon,
  'existing_geometry_tolerance_model_units':floor_tolerance,'uncovered_area_at_existing_tolerance':NEW.difference(nativefloor.buffer(floor_tolerance)).area,'numerical_sliver_geometry_omitted':True,'native_Ground_Floor_and_ContextPlaza_unmodified':True},
 'fixture_placements':FIXTURE_QA,'existing_light_placements':LIGHT_QA,
 'preserve':{'all_MainShell_assets_faces_vertices':True,'all_Room_ToiletRoom_and_core_faces':True,'all_other_units_and_contents':True,'source_Room_faces':[48,54,72,204,205],'end_core_approach_polygon_UV':RV['preserved_native_core_approach']},
 'import_rules':['Read actual current assets. Validate selected Unit8 vertex-byte hash and original triangle indices; permit unrelated Unit4 vertex changes.','Keep all original vertex/channel arrays byte exact, remove only listed source face ordinals, append provided arrays and triangles; do not recalculate original normals/tangents.','Reuse exact source material/submesh assignments. Update every source renderer/MeshCollider owner referencing each edited mesh.','Apply world rigid deltas once to listed roots; their children/local door fields remain unchanged.','Preserve native Ground_Floor/ContextPlaza byte exact. Their active upward Y≈0 collision faces provide target floor support; omit numerical-sliver geometry within existing2e−5 tolerance. The rebuilt granite overlay atY.004 supplies the finish.','No canonical source/spec rewrite by this lane; root performs reviewed production transaction and runtime checks.']}
dump('unit8-replacement-packet.json',packet)
dump('geometry-qa.json',{'schema':'chooguard.unit8-offline-rebuild-qa.v1','status':'PASS','pieces':piecechecks,'floor_support':packet['floor_support'],'fixture_count_before_after':[7,len(FIXTURE_QA)],'light_discs_before_after':[18,len([x for x in LIGHT_QA if x['mesh'] in ['mesh-1940','mesh-0310']])],'door_root_rigid_delta_local_fields_unchanged':True,'snapshot_scene_sha256':D['scene_file_sha256'],'runtime_verified':False})
print(json.dumps({'status':packet['status'],'mesh_replacements':len(replacements),'raw_numerical_floor_sliver_area':missing.area,'max_floor_residual_distance':required_floor_buffer,'float32_epsilon':float32_corner_epsilon,'native_support_area':NEW.intersection(nativefloor).area,'motions':len(motions),'fixtures':[(x['name'],x['operation'],x.get('delta_UV')) for x in FIXTURE_QA],'source_removed_faces':sum(len(x['removed_triangle_ordinals']) for x in replacements),'new_triangles':sum(len(x['added_triangles']) for x in replacements),'door_centre':doorcentre.tolist()},ensure_ascii=False))
