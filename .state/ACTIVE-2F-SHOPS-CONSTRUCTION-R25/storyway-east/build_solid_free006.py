from pathlib import Path
import numpy as np,json,struct,math,hashlib,shutil
from shapely.geometry import Polygon,Point
from shapely.ops import triangulate,unary_union
import cv2
P=Path(__file__).parent;S=P/'free-models-source';S.mkdir(exist_ok=True);R=Path.cwd();records=[];materials={};textures={};sources=[]
X,Y=43.3742,-30.8655
q=lambda u,d:[X+u,Y-d]
# Source brand outline supplied by root: real counterforms/holes, local +Z outward.
brand=json.load(open(P/'sign-model-authoring/PHOTO-TRACED-STORYWAY-EXTRUDED-SOLID.json'));v=np.array(brand['vertices']);xmin,ymin,zmin=v.min(0);xmax,ymax,zmax=v.max(0);cx=(xmin+xmax)/2;cy=(ymin+ymax)/2
for id,at,bottom,width,depth,mat in [('PHOTO_TRACED_MAIN_WHITE_STORYWAY',q(4.102,.003),2.94,3.07,.035,'signWhite'),('PHOTO_TRACED_DOOR_ORANGE_STORYWAY',q(1.40,.122),1.76,.76,.004,'signOrange')]:
 vv=v.copy();sc=width/(xmax-xmin);vv[:,0]=(vv[:,0]-cx)*sc;vv[:,1]=(vv[:,1]-cy)*sc;vv[:,2]=vv[:,2]/.035*depth
 records.append({'id':id,'mesh_key':id,'kind':'mesh','group':'SOLID_PHOTO_TRACED_SIGNS','q':at,'y':bottom,'yaw':0,'vertices':vv.tolist(),'triangles':brand['triangles'],'material':mat,'source_id':'PHOTO_TRACED_BRAND_OUTLINE_AND_EXACT_2026_WHITEFINISH','front_normal':[0,0,1],'contains_text_component':False})
# A single continuous smile arc with real thickness; all outward-facing surfaces are solid geometry.
def extrusion(poly,depth):
 vs=[];ts=[]
 def tri(a,b,c):
  j=len(vs);vs.extend([a,b,c]);ts.extend([j,j+1,j+2])
 ps=list(poly.geoms)if poly.geom_type=='MultiPolygon'else[poly]
 for p in ps:
  for t in triangulate(p):
   if not p.covers(t.representative_point()):continue
   c=list(t.exterior.coords)[:3]
   # CCW xy front faces positive Z.
   cross=(c[1][0]-c[0][0])*(c[2][1]-c[0][1])-(c[1][1]-c[0][1])*(c[2][0]-c[0][0])
   if cross<0:c=c[::-1]
   tri([*c[0],0],[*c[1],0],[*c[2],0]);tri([*c[2],-depth],[*c[1],-depth],[*c[0],-depth])
  for ring in [p.exterior,*p.interiors]:
   c=list(ring.coords)
   for a,b in zip(c,c[1:]):tri([*a,-depth],[*b,-depth],[*b,0]);tri([*a,-depth],[*b,0],[*a,0])
 return vs,ts
outer=[(.22*math.cos(a),.22*math.sin(a))for a in np.linspace(math.pi,2*math.pi,65)]
inner=[(.16*math.cos(a),.16*math.sin(a))for a in np.linspace(2*math.pi,math.pi,65)]
smile=Polygon(outer+inner).union(Point(-.19,0).buffer(.03,resolution=16)).union(Point(.19,0).buffer(.03,resolution=16))
for suffix,u,d,h,s,depth,mat in [('MAIN',1.72,.003,2.96,1,.035,'signWhite'),('DOOR',.88,.122,1.76,.25,.004,'signOrange')]:
 vs,ts=extrusion(smile,depth);vv=np.array(vs);vv[:,:2]*=s
 records.append({'id':'CONTINUOUS_SOLID_SMILE_'+suffix,'mesh_key':'CONTINUOUS_SOLID_SMILE_'+suffix,'kind':'mesh','group':'SOLID_PHOTO_TRACED_SIGNS','q':q(u,d),'y':h,'yaw':0,'vertices':vv.tolist(),'triangles':ts,'material':mat,'source_id':'NAVER_DJAGYTT_20260518_15_ICON','front_normal':[0,0,1]})
 # Hemispheres, no assembled spheres/cylinder bead approximation.
 for i,ma in enumerate(['dotBlue','dotLime','dotYellow','signWhite','signWhite']):
  a=math.radians(20+i*35);du=.23*math.cos(a)*s;dh=.23*math.sin(a)*s;radius=.0325*s;vv=[];tt=[]
  for row in range(9):
   theta=(row/8)*math.pi/2
   for k in range(25):
    phi=(k/24)*2*math.pi;vv.append([radius*math.sin(theta)*math.cos(phi),radius*math.sin(theta)*math.sin(phi),radius*math.cos(theta)])
  for row in range(8):
   for k in range(24):
    j=row*25+k;tt.extend([j,j+25,j+1,j+1,j+25,j+26])
  records.append({'id':'HEMISPHERE_DOT_%s_%s'%(suffix,i),'mesh_key':'HEMISPHERE_DOT_'+suffix,'kind':'mesh','group':'SOLID_PHOTO_TRACED_SIGNS','q':q(u+du,d+.014*s),'y':h+dh,'yaw':0,'vertices':vv,'triangles':tt,'material':ma if suffix=='MAIN'else'signOrange','source_id':'NAVER_DJAGYTT_20260518_15_MULTICOLOR_DOTS','front_normal':[0,0,1]})
# Category letters are solid outline extrusions derived from source-observed narrow sans letterform.
# Text exists only in this authoring program, never as runtime font/TMP labels.
# Exact white category raster is rectified only for letterform reconstruction, never as a visible plane.
im=cv2.imread(str(P/'sign-model-authoring/FASTFOOD-SOURCE-ENLARGED.png'))
m=((im.min(2)>78)&((im.max(2)-im.min(2))<95)).astype('uint8')*255
keep=np.zeros(m.shape,np.uint8);cv2.fillConvexPoly(keep,np.array([[285,207],[800,130],[811,247],[290,318]],np.int32),255);m&=keep
m[234:267,489:533]=0;m[223:280,450:473]=0
m=cv2.morphologyEx(m,cv2.MORPH_CLOSE,np.ones((5,5),np.uint8))
m=cv2.warpAffine(m,np.array([[1,0,0],[.125,1,-30]],float),(m.shape[1],m.shape[0]),flags=cv2.INTER_NEAREST)
cv2.imwrite(str(P/'sign-model-authoring/FASTFOOD-OUTLINE-SOLID-SOURCE-MASK.png'),m)
contours,hh=cv2.findContours(m,cv2.RETR_CCOMP,cv2.CHAIN_APPROX_SIMPLE);polys=[]
for ii,c in enumerate(contours):
 if hh[0][ii][3]!=-1 or cv2.contourArea(c)<40:continue
 c=cv2.approxPolyDP(c,.55,True).reshape(-1,2);holes=[];child=hh[0][ii][2]
 while child!=-1:
  if cv2.contourArea(contours[child])>10:holes.append(cv2.approxPolyDP(contours[child],.55,True).reshape(-1,2).tolist())
  child=hh[0][child][0]
 poly=Polygon(c.tolist(),holes).buffer(0)
 if not poly.is_empty:polys.append(poly)
shape=unary_union(polys);lo=shape.bounds
from shapely.affinity import translate,scale
shape=translate(shape,xoff=-(lo[0]+lo[2])/2,yoff=-(lo[1]+lo[3])/2)
# +Z is customer outward: localX is reflected so source left-to-right reads correctly from that face.
shape=scale(shape,xfact=-2.0/(lo[2]-lo[0]),yfact=-.18/(lo[3]-lo[1]),origin=(0,0))
vv,tt=extrusion(shape,.008)
records.append({'id':'SOLID_FAST_FOOD_CATEGORY_LETTERS','mesh_key':'SOLID_FAST_FOOD_CATEGORY_LETTERS','kind':'mesh','group':'SOLID_PHOTO_TRACED_SIGNS','q':q(2.85,2.655),'y':2.56,'yaw':0,'vertices':vv,'triangles':tt,'material':'signWhite','source_id':'NAVER_DJAGYTT_20260518_15_CATEGORY_CV_OUTLINE','front_normal':[0,0,1],'contains_text_component':False})
materials.update({'signWhite':{'color':[.99,.99,.97,1],'metallic':.10,'smoothness':.55,'emission':[.7,.7,.66,1]},'signOrange':{'color':[.90,.29,.045,1],'metallic':.05,'smoothness':.45},'dotBlue':{'color':[.16,.61,.86,1],'smoothness':.6},'dotLime':{'color':[.58,.81,.23,1],'smoothness':.6},'dotYellow':{'color':[.96,.78,.16,1],'smoothness':.6}})
# Read actual downloaded free GLB geometry and node transforms, preserving UVs and materials.
def readglb(p):
 b=p.read_bytes();n,t=struct.unpack_from('<II',b,12);d=json.loads(b[20:20+n]);length,t=struct.unpack_from('<II',b,20+n);return d,b[28+n:28+n+length],b

def accessor(d,b,i):
 a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];dt={5126:'<f4',5125:'<u4',5123:'<u2',5122:'<i2',5121:'u1',5120:'i1'}[a['componentType']];nc={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];stride=v.get('byteStride',np.dtype(dt).itemsize*nc);off=v.get('byteOffset',0)+a.get('byteOffset',0);out=np.ndarray((a['count'],nc),dtype=dt,buffer=b,offset=off,strides=(stride,np.dtype(dt).itemsize)).copy()
 if a.get('normalized')and a['componentType']!=5126:out=out.astype(float)/np.iinfo(dt).max
 return out

def m4(n):
 if 'matrix'in n:return np.array(n['matrix']).reshape(4,4).T
 x,y,z,w=n.get('rotation',[0,0,0,1]);m=np.eye(4);m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0]);return m

def extract(d,b,nodeids):
 parents={c:i for i,n in enumerate(d['nodes'])for c in n.get('children',[])}
 def world(i):return(world(parents[i])if i in parents else np.eye(4))@m4(d['nodes'][i])
 out=[]
 for i in nodeids:
  nd=d['nodes'][i]
  if'mesh'not in nd:continue
  for j,pr in enumerate(d['meshes'][nd['mesh']]['primitives']):
   vv=accessor(d,b,pr['attributes']['POSITION']);vv=(world(i)@np.column_stack([vv,np.ones(len(vv))]).T).T[:,:3];vv[:,2]*=-1
   ii=accessor(d,b,pr['indices']).flatten().reshape(-1,3)[:,[0,2,1]].flatten();uv=accessor(d,b,pr['attributes']['TEXCOORD_0'])if'TEXCOORD_0'in pr['attributes']else np.zeros((len(vv),2));out.append((i,j,nd.get('name'),vv,ii,uv,pr.get('material',0)))
 return out

def matsfrom(d,b,prefix,sourcebase=None):
 for i,m in enumerate(d.get('materials',[])):
  a=m.get('pbrMetallicRoughness',{});k=prefix+str(i);materials[k]={'color':a.get('baseColorFactor',[1,1,1,1]),'metallic':a.get('metallicFactor',0),'smoothness':1-a.get('roughnessFactor',1.0),'source_name':m.get('name')}
  ti=a.get('baseColorTexture',{}).get('index')
  if ti is not None:
   im=d['images'][d['textures'][ti]['source']]
   if'bufferView'in im:
    bv=d['bufferViews'][im['bufferView']];raw=b[bv.get('byteOffset',0):bv.get('byteOffset',0)+bv['byteLength']];f=S/(prefix+'texture-'+str(i)+'.png');f.write_bytes(raw);materials[k]['texture_source']=str(f.relative_to(R))
   elif 'uri' in im and sourcebase is not None:
    original=sourcebase/im['uri'];f=S/(prefix+'texture-'+str(i)+original.suffix);shutil.copy2(original,f);materials[k]['texture_source']=str(f.relative_to(R))

def addmodel(parts,prefix,at,bottom,size,yaw=0,matprefix='',source=''):
 allv=np.concatenate([x[3]for x in parts]);lo=allv.min(0);hi=allv.max(0);center=(lo+hi)/2
 for i,j,name,v,ind,uv,ma in parts:
  vv=(v-center)/np.maximum(hi-lo,1e-7)*np.array(size);vv[:,1]+=size[1]/2
  records.append({'id':prefix+'_'+str(i)+'_'+str(j),'mesh_key':source+'_'+str(i)+'_'+str(j)+'_'+str(size),'kind':'mesh','group':'REAL_WEB_FREE_3D_MODELS','q':at,'y':bottom,'yaw':yaw,'vertices':vv.tolist(),'triangles':ind.tolist(),'uv':uv.tolist(),'material':matprefix+str(ma),'source_id':source,'source_mesh_name':name})
# New personal web download, original source assembly stays intact. Only source monitor/payment components reused.
path=S/'innerscene-pos-original.glb';d,b,raw=readglb(path);matsfrom(d,b,'POS')
addmodel(extract(d,b,[10,15,16,17,20,21,22,23,25]),'REAL_CC0_POS_MONITOR',q(6.74,7.43),1.037,[.32,.34,.19],0,'POS','INNERSCENE_CC0_POS')
addmodel(extract(d,b,[5,24,27,28,29,30,31,32,33]),'REAL_CC0_PAYMENT_MACHINE',q(6.89,7.70),1.04,[.10,.13,.14],0,'POS','INNERSCENE_CC0_POS')
sources.append({'id':'INNERSCENE_CC0_POS','url':'https://www.innerscene.com/tools/library/3d-parts/checkout-counter-with-pos-original-retail-fixture-3fcfb566','download':'https://www.innerscene.com/api/library/checkout-counter-with-pos-original-retail-fixture-3fcfb566/download','license':'CC0-1.0','original_sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw),'source_file':str(path.relative_to(R)),'adaptation':'Monitor, pedestal and payment terminal actual mesh components only. Entire checkout/conveyor omitted; fitted within observed rear service-device envelope.'})
# Real downloaded food kit forms replace all generic can/bottle/package stock proxies.
kp=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/kenney-food-kit/Models/GLB format');prototypes={}
for n in ['soda-bottle','soda-can','bag-flat','chocolate-wrapper']:
 f=kp/(n+'.glb');dd,bb,rr=readglb(f);mp='KENNEY_'+n.replace('-','_')+'_';matsfrom(dd,bb,mp,f.parent);prototypes[n]=(extract(dd,bb,list(range(len(dd['nodes'])))),mp)
 sources.append({'id':'KENNEY_CC0_'+n,'url':'https://kenney.nl/assets/food-kit','download':'https://kenney.nl/media/pages/assets/food-kit/83086fa91c-1719418518/kenney_food-kit.zip','license':'CC0-1.0','source_file':str(f),'source_sha256':hashlib.sha256(rr).hexdigest(),'bytes':len(rr),'adaptation':'Original mesh and UV retained, scaled to source-visible category/product envelope; no branded SKU/current inventory claim.'})
for tier in range(6):
 for j in range(20):
  u=2.85-4.55/2+.20+j*(4.55-.4)/19;d=3.1-.06;h=.38+tier*.30
  typ='soda-can'if tier in [1,4]and j%3!=0 else'soda-bottle';pr,mp=prototypes[typ];size=[.105,.235,.105]if typ=='soda-bottle'else[.105,.205,.105]
  addmodel(pr,'REAL_KENNEY_CHILLED_PRODUCT_%02d_%02d'%(tier,j),q(u,d),h+.015,size,0,mp,'KENNEY_CC0_'+typ)
for tier in range(4):
 for j in range(6):
  typ='chocolate-wrapper'if j%2 else'bag-flat';pr,mp=prototypes[typ]
  addmodel(pr,'REAL_KENNEY_RIGHT_PACK_%02d_%02d'%(tier,j),q(6.53+j*.155,1.98),.26+tier*.30+.02,[.12,.18,.26],0,mp,'KENNEY_CC0_'+typ)
remove_groups=['MAY26_Source_Signs','MAY26_Observed_Chilled_Products','MAY26_Entry_Right_Stock']
remove_names=['MAY26_cashier_small_visible_black_device','MAY26_cashier_device_stand']
packet={'schema':'chooguard.storyway.solid_signs_real_free_assets006.v1','store_key':'storyway-east','origin_q':[X,Y],'objects':records,'materials':materials,'remove_exact_own_groups':remove_groups,'remove_exact_own_names':remove_names,'licensed_source_models':sources,'front_normal_basis':{'approved_front_qy':Y,'public_hall_qy_direction':'INCREASE','solid_glyph_mesh_front_normal_local':[0,0,1],'mesh_yaw':0,'prior_TMP_yaw_not_reused':True},'sign_geometry':{'brand_outline_polygons':brand['source_polygons'],'original_brand_letter_holes_preserved':True,'source_photograph_role':'Brand outline only; east internal layout remains2026Naver source.','main_depth_m':.035,'door_depth_m':.004,'smile':'One continuous extruded U arc with round endcaps;5 physical hemispherical colored dots','runtime_TMP_or_TextMesh_components':False,'visible_source_raster_billboards':False},'preserve':'Exact own source chiller/pegstand/capsule shell, R24region, originalnative/floors/columns/transports/WC/1F/otherstores/sourceAssets','excluded_asset':'CCBYSAclosedcommercialfridgecase not substituted into sourceOPENchiller; changed profile would contradict2026field image.','limitations':['Equipment/stock is licensed free mesh adapted to observed role and envelope; no claim of exactvendor model or liveSKU','HiddenBOH/circuits/originalarchitecturaldesign/mural remain unqualified'],'SceneQA_Play_tests_reopen_capture':False}
f=P/'SOLID-SIGNS-REAL-FREE-ASSETS006.json';f.write_text(json.dumps(packet,ensure_ascii=False,indent=2));print('objects',len(records),'uniqueMeshes',len({x['mesh_key']for x in records}),'SHA',hashlib.sha256(f.read_bytes()).hexdigest())
