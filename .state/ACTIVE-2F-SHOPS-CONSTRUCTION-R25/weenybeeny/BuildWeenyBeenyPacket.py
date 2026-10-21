from pathlib import Path
import sys,json,math,hashlib,random,datetime,shutil
import numpy as np,cv2
from shapely.geometry import Polygon
from shapely.geometry.polygon import orient
from shapely import constrained_delaunay_triangles

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'
OWN=BASE/'weenybeeny'
AUTHOR=OWN/'authoring';AUTHOR.mkdir(exist_ok=True)
sys.path.insert(0,str(BASE))
from photo_outline_to_solid import masked_polygons
B=json.loads((OWN/'MODEL-PLACEMENT-BASIS-R25.json').read_text())
O=np.array(B['origin_q']);U=np.array(B['u_front_right']);V=np.array(B['v_into_store'])
W=B['width_m_MODEL'];D=B['depth_m_MODEL'];YAW=math.degrees(math.atan2(V[0],V[1]))
ASSET='Assets/ChooGuard/Art/StationInterior/R25_Shops/weenybeeny'
P={'store_key':'weenybeeny','origin_q':O.tolist(),'floor_world_y':7.0,'source_packet':str(OWN/'SOURCE-EVIDENCE-R25.json'),'materials':{},'source_imports':[],'mesh_library':{},'objects':[]}
M=P['materials'];objs=P['objects'];rng=random.Random(25)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def q(x,z):return (O+U*x+V*z).tolist()
def material(k,c,**kw):M[k]={'color':c,**kw}
def imp(src,dest):
 src=Path(src).resolve();P['source_imports'].append({'source':str(src),'destination':dest,'sha256':sha(src)});return ASSET+'/'+dest
def add(kind,id,x,z,y,material=None,group='SourceVisible',**kw):
 a={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':YAW,'group':group,'source_ids':['SEP2026_'+('08_11_BULK' if group.startswith('Bulk') else '02_16_BRANCH')],'qualification':'PHOTO_PROPORTIONED_MODEL_NOT_SURVEYED','collider':False,**kw}
 if material:a['material']=material
 objs.append(a);return a
def box(id,x,z,y,sx,sy,sz,m,group='Booth',**kw):return add('box',id,x,z,y,m,group,size=[sx,sy,sz],**kw)
def cyl(id,x,z,y,diam,height,m,group='Fixtures',**kw):return add('cylinder',id,x,z,y,m,group,size=[diam,height/2,diam],**kw)
def asset(id,source,x,z,y,fit,mat=None,group='DownloadedPhysicalModels',**kw):
 return add('asset',id,x,z,y,None,group,asset=source,fit_size=fit,material_map={'*':mat} if mat else kw.pop('material_map'),**kw)
def tri_mesh():return {'vertices':[],'triangles':[]}
def face(m,a,b,c):
 n=len(m['vertices']);m['vertices'] += [list(map(float,t)) for t in (a,b,c)];m['triangles'] += [n,n+1,n+2]
def quad(m,a,b,c,d):face(m,a,b,c);face(m,a,c,d)
def lib(id,mesh):P['mesh_library'][id]=mesh;return id
def inst(id,key,x,z,y,m,group='Fixtures',**kw):return add('library_mesh',id,x,z,y,m,group,mesh_key=key,**kw)
def bevel_mask(mask,width,height,depth=.055,bevel=.003):
 h,w=mask.shape;out=[]
 for poly in masked_polygons(mask,minimum_area=12,epsilon=.32):
  p=Polygon([[-(x/w-.5)*width,(.5-y/h)*height] for x,y in poly.exterior.coords],[[[-(x/w-.5)*width,(.5-y/h)*height] for x,y in ring.coords] for ring in poly.interiors]);p=orient(p,sign=1)
  inset=p.buffer(-bevel,join_style=2)
  if inset.is_empty:inset=p;bb=0
  else:bb=bevel
  m=tri_mesh()
  def cap(shape,z,reverse=False,bevelshape=False):
   pieces=[shape] if shape.geom_type=='Polygon' else list(shape.geoms)
   for piece in pieces:
    if piece.is_empty:continue
    for t in constrained_delaunay_triangles(piece).geoms:
     pts=list(t.exterior.coords)[:3]
     if np.cross(np.array(pts[1])-pts[0],np.array(pts[2])-pts[0])<0:pts[1],pts[2]=pts[2],pts[1]
     pp=[]
     for x,y in pts:
      zz=z
      if bevelshape:
       from shapely.geometry import Point
       zz=-bb+min(bb,p.boundary.distance(Point(x,y)))
      pp.append([x,y,zz])
     if reverse:pp.reverse()
     face(m,*pp)
  cap(inset,0);cap(p,-depth,True)
  if bb:cap(p.difference(inset),0,False,True)
  for ring in [p.exterior,*p.interiors]:
   rr=list(ring.coords)
   for a,b in zip(rr,rr[1:]):quad(m,[*a,-depth],[*b,-depth],[*b,-bb],[*a,-bb])
  out.append(m)
 return out

# Own URP materials. Source identities are explicitly mapped for every imported slot.
material('red',[.72,.008,.027,1],smoothness=.68)
material('red_edge',[.48,.005,.015,1],smoothness=.7)
material('white',[.91,.9,.84,1],smoothness=.46)
material('black',[.018,.018,.018,1],smoothness=.38)
material('steel',[.62,.65,.65,1],metallic=.92,smoothness=.78)
material('clear',[.88,.95,.98,.11],transparent=True,smoothness=.96)
material('clear_pack',[.96,.98,1,.16],transparent=True,smoothness=.82)
material('logo_light',[.95,.98,.88,1],emission=[1.25,1.35,1.12,1],smoothness=.55)
material('ceiling_light',[1,.96,.87,1],emission=[1.3,1.16,.96,1],smoothness=.25)
material('pink',[.85,.33,.43,1],smoothness=.28)
palette=[[.72,.025,.035,1],[.95,.57,.012,1],[.95,.8,.08,1],[.02,.49,.25,1],[.06,.5,.75,1],[.8,.37,.58,1],[.86,.85,.69,1],[.16,.055,.027,1]]
for i,c in enumerate(palette):material('candy'+str(i),c,smoothness=.76)

# Downloaded CC0 food/smallwares. Shared source files remain immutable.
ken=BASE/'free-assets/kenney-food-kit'
color=imp(ken/'Models/OBJ format/Textures/colormap.png','Sources/Kenney/colormap.png')
material('kenney_source',[1,1,1,1],texture_asset=color,smoothness=.48)
models={}
for n in ['utensil-spoon','bowl','candy-bar-wrapper','candy-bar','glass']:
 models[n]=imp(ken/f'Models/FBX format/{n}.fbx',f'Sources/Kenney/{n}.fbx')
imp(ken/'License.txt','Sources/Kenney/License.txt')
gummy=imp(OWN/'sources/free-models/gummy-bear/gummy-bear.fbx','Sources/GummyBear/gummy-bear.fbx')
imp(OWN/'sources/free-models/gummy-bear/SOURCE-LICENSE.json','Sources/GummyBear/SOURCE-LICENSE.json')
lolli=imp(OWN/'sources/free-models/lollipop/lollipop.fbx','Sources/Lollipop/lollipop.fbx')
imp(OWN/'sources/free-models/lollipop/SOURCE-LICENSE.json','Sources/Lollipop/SOURCE-LICENSE.json')
tex=OWN/'sources/free-models/lollipop/textures'
for n,k in [('Image_0.png','lollipop0'),('Image_4.png','lollipop1'),('Image_6.png','lollipop2'),('Image_3.png','stick')]:
 ta=imp(tex/n,'Sources/Lollipop/'+n);material(k,[1,1,1,1],texture_asset=ta,smoothness=.72 if k!='stick' else .35)
normal=imp(tex/'Image_2.png','Sources/Lollipop/normal.png')
metal=cv2.imread(str(tex/'Image_1.png'),cv2.IMREAD_UNCHANGED);out=np.zeros((*metal.shape[:2],4),dtype=np.uint8)
# OpenCV BGR: source B metallic to Unity R, G roughness to alpha smoothness.
out[:,:,2]=metal[:,:,0];out[:,:,3]=255-metal[:,:,1]
cv2.imwrite(str(AUTHOR/'lollipop_metallic_smoothness.png'),out)
packed=imp(AUTHOR/'lollipop_metallic_smoothness.png','Sources/Lollipop/metallic_smoothness.png')
M['lollipop0'].update(normal_asset=normal,metallic_gloss_asset=packed)
lm={'Lollipop':'lollipop0','Lollipop_1':'lollipop1','Lollipop_2':'lollipop2','Stick':'stick','Stick_1':'stick','Stick_2':'stick','*':'stick'}
lm.update({'Lollipop_Lollipop_0':'lollipop0','Lollipop_Lollipop #1_0':'lollipop1','Lollipop_Lollipop #2_0':'lollipop2','Lollipop_Lollipop__1_0':'lollipop1','Lollipop_Lollipop__2_0':'lollipop2','Stick_Stick_0':'stick','Stick_Stick #1_0':'stick','Stick_Stick #2_0':'stick','Lollipop #1':'lollipop1','Lollipop #2':'lollipop2'})

# Procedural finish derived from observed porous dark fascia; no sign-photo plane.
noise=np.full((256,256,3),99,dtype=np.uint8);rr=np.random.default_rng(25)
noise=np.clip(noise.astype(float)+rr.normal(0,6,noise.shape[:2])[:,:,None],0,255).astype('uint8')
for i in range(6500):
 x,y=rr.integers(0,256,2);cv2.circle(noise,(int(x),int(y)),int(rr.integers(0,2)),(28,30,31),-1)
cv2.imwrite(str(AUTHOR/'source_porous_fascia_finish.png'),noise)
fas=imp(AUTHOR/'source_porous_fascia_finish.png','Finishes/porous_fascia.png')
material('fascia',[.8,.8,.8,1],texture_asset=fas,smoothness=.18)

# Source outline primary brand: individually editable letter bodies, closed sides/back and small bevel.
mask=cv2.imread(str(AUTHOR/'primary-sign-source-ink-mask.png'),0)
letters=bevel_mask(mask,5.65,.98,.065,.003)
for i,mesh in enumerate(letters):
 key=lib('PRIMARY_PHOTO_LETTER_%02d'%i,mesh)
 inst('PRIMARY_FASCIA_PHOTO_LETTER_%02d'%i,key,W/2,.005,3.09,'logo_light','PhysicalPhotoSigns',yaw=180+YAW,source_ids=['MAY2026_01_ACTUAL_FASCIA','SEP2026_02_CROSSCONFIRM'])
# Physical frame/panel, seams and source visible vertical standoffs.
for i in range(6):box('POROUS_FASCIA_TILE_%02d'%i,(i+.5)*(W-.09)/6+.045,.14,3.09,(W-.09)/6-.008,.88,.16,'fascia','PhysicalPhotoSigns')
box('FASCIA_LOWER_DARK_LIP',W/2,.108,2.62,W-.07,.10,.25,'black','PhysicalPhotoSigns')
for x in [1.0,6.0]:box('LETTER_STANDOFF_%03d'%int(x*100),x,.085,2.96,.035,.51,.09,'steel','PhysicalPhotoSigns')

# Actual source automatic glazed front. Clear central opening retained, single leaf slid onto own right panel.
door0=W/2-.675;door1=W/2+.675
for a,b,n in [(.05,door0,'LEFT'),(door1,W-.05,'RIGHT')]:
 box('FIXED_FRONT_GLAZING_'+n,(a+b)/2,.09,1.26,b-a-.05,2.45,.014,'clear','Booth',cast_shadows=False)
 box('FRONT_SILL_'+n,(a+b)/2,.09,.035,b-a,.06,.085,'steel','Booth')
for i,x in enumerate([.05,door0,door1,W-.05]):box('FRONT_UPRIGHT_%02d'%i,x,.11,1.25,.052,2.5,.1,'steel','Booth',collider=True)
box('AUTODOOR_TOP_TRACK',W/2,.11,2.5,W-.08,.18,.14,'steel','Booth')
box('AUTODOOR_OPEN_LEAF',door1+.66,.155,1.24,1.32,2.38,.013,'clear','Booth',cast_shadows=False)
for x,n in [(door1+.02,'LEFT'),(door1+1.3,'RIGHT')]:box('SLIDING_LEAF_FRAME_'+n,x,.155,1.24,.032,2.4,.045,'steel','Booth')
box('SLIDING_LEAF_BASE',door1+.66,.155,.055,1.32,.045,.05,'steel','Booth')
box('AUTODOOR_SENSOR',W/2,.035,2.47,.15,.045,.045,'black','Booth')
for i,z in enumerate([.08,D-.05]):box('WEST_GLAZED_CORNER_UPRIGHT_%02d'%i,.058,z,1.3,.065,2.6,.065,'steel','Booth',collider=True)
box('WEST_SOURCE_GLAZING',.058,D/2,1.3,.014,2.52,D-.16,'clear','Booth',cast_shadows=False)
box('WEST_TOP_RAIL',.058,D/2,2.6,.09,.08,D-.1,'steel','Booth')
# Reuse actual native east wall: only own interior photo finish, no duplicate room wall.
box('OWN_EAST_RED_FIXTURE_FINISH',W-.057,D/2,1.27,.018,2.5,D-.16,'red','FixtureBacking')
box('OWN_REAR_RED_FIXTURE_BACKING',W/2,D-.055,1.27,W-.14,2.5,.025,'red','FixtureBacking')
box('OWN_SOURCE_CEILING',W/2,D/2,2.78,W-.13,.055,D-.16,'white','Ceiling')
for x in [1.2,3.5,5.8]:
 for z in [.85,2.1,3.25]:
  cyl('RECESSED_DOWNLIGHT_%d_%d'%(x*100,z*100),x,z,2.737,.18,.016,'ceiling_light','Ceiling')
for x in [2.25,5.2]:
 box('HVAC_CASSETTE_FRAME_%d'%int(x*100),x,2.1,2.735,.62,.025,.62,'white','Ceiling')
 for j in range(8):box('HVAC_LOUVRE_%d_%d'%(x*100,j),x,1.86+j*.065,2.718,.48,.008,.016,'steel','Ceiling')

# Front clear floor remains the actual fixed station floor, with source checker pattern recorded as unmodified gap.
# New structural floor geometry or floor material mutation is deliberately absent.

# Source bulk-bin clear D profile. Each shell and curved lid are independently editable physical meshes.
def d_profile(width=.48,h=.34,depth=.43):
 # y,z cross section: flat rear/top with a half-rounded front.
 r=h/2;rear=depth/2;center=-depth/2+r
 return [(h/2,rear),(h/2,center)]+[(r*math.cos(t),center-r*math.sin(t)) for t in np.linspace(0,math.pi,21)]+[(-h/2,rear)]
def bin_shape(lid=False):
 m=tri_mesh();w=.48;pr=d_profile()
 if lid:
  for a,b in zip(pr[1:-1],pr[2:-1]):
   for dx in [0,.004]:
    quad(m,[-w/2,a[0],a[1]+dx],[w/2,a[0],a[1]+dx],[w/2,b[0],b[1]+dx],[-w/2,b[0],b[1]+dx])
  for x in [-w/2,w/2]:
   for a,b in zip(pr[1:-1],pr[2:-1]):quad(m,[x,a[0],a[1]],[x,b[0],b[1]],[x,b[0],b[1]+.004],[x,a[0],a[1]+.004])
 else:
  # solid thin side plates, base and top/back are transparent separate real faces.
  poly=Polygon(pr)
  for side in [-1,1]:
   x=side*w/2
   for t in constrained_delaunay_triangles(poly).geoms:
    a,b,c=list(t.exterior.coords)[:3];face(m,[x,*a],[x,*c],[x,*b]);face(m,[x-side*.003,*a],[x-side*.003,*b],[x-side*.003,*c])
   pp=list(poly.exterior.coords)
   for a,b in zip(pp,pp[1:]):quad(m,[x,*a],[x,*b],[x-side*.003,*b],[x-side*.003,*a])
  for a,b in [(pr[-1],pr[0]),(pr[0],pr[1]),(pr[-2],pr[-1])]:quad(m,[-w/2,*a],[w/2,*a],[w/2,*b],[-w/2,*b])
 return m
lib('D_BIN_CLEAR_BODY',bin_shape());lib('D_BIN_CURVED_LID',bin_shape(True))
# Source U-shaped red scoop grips: rounded curved mesh, independent from actual downloaded spoon bowl.
def tube(points,r=.015,n=8):
 m=tri_mesh()
 for a,b in zip(points,points[1:]):
  a=np.array(a);b=np.array(b);t=(b-a)/np.linalg.norm(b-a);ref=np.array([0,1,0])
  if abs(np.dot(t,ref))>.95:ref=np.array([1,0,0])
  aa=np.cross(t,ref);aa=aa/np.linalg.norm(aa);bb=np.cross(t,aa)
  for i in range(n):
   t0=i*2*math.pi/n;t1=(i+1)*2*math.pi/n;v0=r*(aa*math.cos(t0)+bb*math.sin(t0));v1=r*(aa*math.cos(t1)+bb*math.sin(t1));quad(m,a+v0,b+v0,b+v1,a+v1)
 return m
grip=[[-.07,0,.05],[-.07,0,-.06]]+[[.07*math.cos(t),0,-.06-.07*math.sin(t)] for t in np.linspace(math.pi,0,13)]+[[.07,0,.05]]
lib('SOURCE_U_SCOOP_HANDLE',tube(grip,.013))

for row in range(4):
 z=D-.42-row*.105;y=.38+row*.38
 box('BULK_RACK_RAIL_%d'%row,4.0,z+.12,y-.18,5.7,.033,.045,'steel','BulkRail')
 for col in range(10):
  x=1.38+col*.55;idx=row*10+col;tag='%02d'%idx
  inst('BULK_CLEAR_BODY_'+tag,'D_BIN_CLEAR_BODY',x,z,y,'clear','BulkBins')
  inst('BULK_HINGED_CURVED_LID_'+tag,'D_BIN_CURVED_LID',x,z-.008,y,'clear','BulkBins')
  add('sphere','BULK_RED_LID_KNOB_'+tag,x,z-.236,y+.015,'red','BulkBins',size=[.04,.04,.023])
  inst('BULK_SOURCE_U_GRIP_'+tag,'SOURCE_U_SCOOP_HANDLE',x,z-.265,y-.15,'red','BulkScoops')
  asset('BULK_ACTUAL_DOWNLOADED_SPOON_'+tag,models['utensil-spoon'],x,z-.18,y-.17,[.09,.025,.22],'steel','BulkScoops',euler=[0,YAW,0])
  for j in range(7):
   xx=x+rng.uniform(-.17,.17);zz=z+rng.uniform(-.11,.10);yy=y-.12+rng.uniform(0,.14)
   asset('BULK_DOWNLOADED_GUMMY_'+tag+'_%02d'%j,gummy,xx,zz,yy,[.055,.075,.033],'candy'+str((row+col+j%2)%8),'BulkDownloadedCandy',euler=[rng.uniform(40,90),YAW+rng.uniform(0,360),rng.uniform(-40,40)])
  for j in range(5):
   add('sphere','BULK_JELLY_BALL_'+tag+'_%02d'%j,x+rng.uniform(-.17,.17),z+rng.uniform(-.12,.12),y-.07+rng.uniform(0,.08),'candy'+str((row+col)%8),'BulkCandy',size=[.037,.028,.039])
for x in [1.04,3.8,6.72]:box('BULK_RED_RACK_UPRIGHT_%d'%int(x*100),x,D-.22,1.12,.06,2.15,.1,'red','BulkRail')

# Upper vertical gravity dispensers observed above the stepped bulk row.
for i in range(10):
 x=1.38+i*.55;z=D-.29
 cyl('GRAVITY_CLEAR_HOPPER_%02d'%i,x,z,2.05,.34,.72,'clear','BulkGravity')
 cyl('GRAVITY_RED_CAP_%02d'%i,x,z,2.425,.37,.045,'red','BulkGravity')
 cyl('GRAVITY_RED_NECK_%02d'%i,x,z,1.665,.23,.07,'red','BulkGravity')
 cyl('GRAVITY_STEEL_NOZZLE_%02d'%i,x,z,1.57,.16,.12,'steel','BulkGravity')
 box('GRAVITY_PULL_HANDLE_%02d'%i,x,z-.18,1.58,.13,.025,.07,'red','BulkGravity')
 for j in range(12):
  add('sphere','GRAVITY_CANDY_%02d_%02d'%(i,j),x+rng.uniform(-.105,.105),z+rng.uniform(-.10,.10),1.77+j*.041,'candy'+str(i%8),'BulkGravityMerchandise',size=[.05,.046,.05])
 # exact source shape repeated within real red curved header, no generic text.
 cyl('GRAVITY_HEADER_CANOPY_%02d'%i,x,z,2.58,.53,.21,'red','BulkGravity')

# Photo visible freestanding three-tier round island left of entry.
ix,iz=1.32,1.22
cyl('ROUND_ISLAND_BASE',ix,iz,.26,1.13,.5,'white','RoundSourceDisplay',collider=True)
for y,diam in [(.53,1.18),(.95,.94),(1.34,.72)]:
 cyl('ROUND_RED_SHELF_%03d'%int(y*100),ix,iz,y,diam,.055,'red','RoundSourceDisplay')
 for j in range(10):
  angle=j*2*math.pi/10;xx=ix+math.cos(angle)*diam*.36;zz=iz+math.sin(angle)*diam*.36
  asset('ISLAND_WRAPPED_CANDY_%03d_%02d'%(y*100,j),models['candy-bar-wrapper'],xx,zz,y+.033,[.12,.17,.06],'kenney_source','DownloadedMerchandise',yaw=YAW-math.degrees(angle))
for i in range(6):
 a=i*math.pi/3;x=ix+.32*math.cos(a);z=iz+.32*math.sin(a)
 asset('ISLAND_ACTUAL_CLEAR_JAR_%02d'%i,models['glass'],x,z,.98,[.12,.22,.12],'clear','DownloadedMerchandise')
 for j in range(2):asset('ISLAND_JAR_GUMMY_%02d_%02d'%(i,j),gummy,x,z,1.0+j*.06,[.05,.07,.035],'candy'+str(i),'DownloadedMerchandise')
box('ROUND_DISPLAY_CENTRAL_POST',ix,iz,1.04,.055,1.15,.055,'steel','RoundSourceDisplay')

# Right white shelf gondola with source red backing and actual physical packaged candy/lollipop assets.
gx,gz=5.65,1.27
box('RIGHT_GONDOLA_RED_BACK',gx,gz, .68,1.55,1.25,.035,'red','Gondola')
box('RIGHT_GONDOLA_BASE',gx,gz,.16,1.62,.30,.7,'white','Gondola',collider=True)
for y in [.34,.68,1.02]:
 box('RIGHT_WHITE_SHELF_%d'%int(y*100),gx,gz-.03,y,1.64,.035,.68,'white','Gondola')
 for j in range(9):
  asset('GONDOLA_ACTUAL_WRAPPER_%d_%02d'%(y*100,j),models['candy-bar-wrapper'],gx-.68+j*.17,gz-.14,y+.023,[.12,.16,.065],'kenney_source','DownloadedMerchandise')
 for j in range(5):asset('GONDOLA_CHOCOLATE_%d_%02d'%(y*100,j),models['candy-bar'],gx-.62+j*.29,gz+.19,y+.024,[.14,.04,.09],'candy7','DownloadedMerchandise')
for i in range(5):
 x=4.98+i*.33
 box('LOLLIPOP_BLACK_PEG_BASE_%02d'%i,x,gz,1.08,.23,.03,.22,'black','PegTrees')
 box('LOLLIPOP_BLACK_PEG_TRUNK_%02d'%i,x,gz,1.31,.025,.48,.025,'black','PegTrees')
 for j in range(2):asset('DOWNLOADED_LOLLIPOP_CLUSTER_%02d_%02d'%(i,j),lolli,x,gz+(.07 if j else -.07),1.12+j*.18,[.25,.32,.045],group='DownloadedLollipops',material_map=lm,yaw=YAW+180*(j%2))

# Source bowl on a red stand near the right entry glazing.
asset('ACTUAL_DOWNLOADED_CLEAR_BOWL',models['bowl'],4.6,.68,.91,[.48,.24,.48],'clear','SourceBowl')
cyl('BOWL_RED_COLLAR',4.6,.68,.92,.48,.035,'red','SourceBowl')
for x,z in [(4.42,.55),(4.77,.55),(4.6,.86)]:box('BOWL_RED_STAND_%d_%d'%(x*100,z*100),x,z,.46,.019,.9,.019,'red','SourceBowl')
for i in range(12):asset('BOWL_ACTUAL_GUMMY_%02d'%i,gummy,4.6+rng.uniform(-.12,.12),.68+rng.uniform(-.12,.12),.98+rng.uniform(0,.08),[.05,.075,.035],'candy'+str(i%8),'DownloadedMerchandise')

# Source pink rear pegboard with suspended actual candy wrappers, not photographs as equipment.
box('PINK_SOURCE_PEGBOARD',.59,D-.08,1.38,.87,2.2,.025,'pink','Pegboard')
for row in range(5):
 for col in range(4):
  x=.25+col*.225;y=.48+row*.37
  box('PEGBOARD_CHROME_HOOK_%d_%d'%(row,col),x,D-.19,y+.13,.012,.012,.20,'steel','Pegboard')
  asset('PEG_HANGING_REAL_WRAPPER_%d_%d'%(row,col),models['candy-bar-wrapper'],x,D-.28,y,[.16,.24,.055],'kenney_source','DownloadedMerchandise')

# Compact license and evidence provenance travel with own runtime asset folder.
credits='R25 위니비니 source-adapted physical shop.\nGummy Bear by Isabella Baranyk, CC-BY4.0, https://sketchfab.com/3d-models/6c807780df124dbfb7105dd64160bb2b\nLollipop by Francesca Cepparulo_3D, CC-BY4.0, https://sketchfab.com/3d-models/158655fd3f77415db3f853a3b6bed622\nFood Kit2.0 by Kenney, CC0, https://kenney.nl/assets/food-kit\nDownloaded source shapes adapted to photo proportions, own URP materials. No exact SKU/dimensions or complete digital-twin claim.\nField photographs: https://m.blog.naver.com/pig9951/224292041260 (2026-05-21), https://m.blog.naver.com/zzang2mama/224404805472 (2026-09-08). Source photos evidence only; primary sign converted to closed solid outline, no whole photograph sign plane.\n'
(AUTHOR/'CREDITS.txt').write_text(credits);imp(AUTHOR/'CREDITS.txt','CREDITS.txt')
photos=[]
for name,viewed in [('ROOT-pig9951-224292041260',[1]),('ROOT-zzang2mama-224404805472',[2,3,4,5,6,7,8,9,10,11,12,15,16])]:
 d=json.loads((OWN/'sources'/f'{name}-INDEX.json').read_text());photos.append({'branch_url':d['url'],'title':d['title'],'source_date':d['source_date'],'directly_viewed_full':[x for x in d['images'] if x['index'] in viewed],'observed':'Actual Busan station branch storefront, automated glazing, porous gray fascia with stand-off luminous serif letters, red transparent stepped bulk bins/scoops, upper cylindrical dispensers, round red/white island, hanging lollipop/candy fixtures.','not_metric_authority':True})
evidence={'store_key':'weenybeeny','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'actual_branch_photos':photos,'original_branch_design_drawings':{'status':'NOT_ACQUIRED','searched':['위니비니 부산역점 도면 인테리어 설계','위니비니 부산역 인테리어 도면 filetype:pdf'],'no_exact_branch_plan_found':True},'physical_models':[json.loads((OWN/f'sources/free-models/{n}/SOURCE-LICENSE.json').read_text()) for n in ['gummy-bear','lollipop']]+[json.loads((BASE/'free-assets/KENNEY-FOOD-KIT-ACQUISITION.json').read_text())],'source_preview_directly_viewed':['gummy-bear/preview.jpg','lollipop/preview.jpg','Kenney/utensil-spoon.png','Kenney/candy-bar-wrapper.png'],'sign':{'main_source':'MAY2026_01 actual branch photo','mask':str(AUTHOR/'primary-sign-source-ink-mask.png'),'letters':len(letters),'geometry':'closed front/back/side/bevel3mm; letter holes preserved; source physical standoffs and panel','source_used_as_visible_plane':False,'text_or_font_component':False,'secondary_bean_sign':'Not reconstructed because source photo clips upper contour; no invented full silhouette'},'official_brand_navigation':{'url':'https://www.weenybeeny.com/','image_url':'https://cmix5044.img16.kr/11657/img/logo.png','local':str(OWN/'sources/OFFICIAL-BRAND-LOGO.png'),'directly_viewed':True,'use':'identity cross-check only, actual branch photo drives physical main sign'},'placement_basis':str(OWN/'MODEL-PLACEMENT-BASIS-R25.json'),'limits':['All dimensions/proportions and per-bin product quantities MODEL; not surveyed.','Original branch construction drawings and exact hidden spaces not acquired; no invented BOH.','Checker floor visible in photos left as explicit finish gap to preserve existing fixed floor.','Secondary upper-clipped bean sign not reconstructed; no invented outline.','Downloaded generic candy assets are type/shape matches, not exact branch SKU or scanned station merchandise.','No QA/Play/tests/probes/reopen/capture performed; user owns validation.'],'all2F_complete':False,'99percent_achieved':False}
(OWN/'SOURCE-EVIDENCE-R25.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
(OWN/'STORE-PACKET-R25.json').write_text(json.dumps(P,ensure_ascii=False,separators=(',',':')))
stats={'packet_sha256':sha(OWN/'STORE-PACKET-R25.json'),'objects':len(objs),'kinds':{k:sum(x['kind']==k for x in objs) for k in sorted(set(x['kind'] for x in objs))},'imports':len(P['source_imports']),'materials':len(M),'source_outline_letter_parts':len(letters),'free_source_instances':sum(x['kind']=='asset' for x in objs),'bulk_bins':40,'gravity_dispensers':10}
(OWN/'AUTHORING-SUMMARY-R25.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2))
template=(BASE/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text()
for a,b in {'__STORE_KEY__':'weenybeeny','__WORKER_THREAD__':'classic:/root/r25_weenybeeny','__EXACT_SCOPE__':'C180_R25_WEENYBEENY_APPLY_001','__PACKET_PATH__':str(OWN/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':stats['packet_sha256']}.items():template=template.replace(a,b)
(OWN/'ApplyStoreR25.cs').write_text(template)
print(json.dumps(stats,ensure_ascii=False,indent=2))
