from pathlib import Path
import json, hashlib, math, re, shutil
import numpy as np
from PIL import Image
from shapely.geometry import box as polybox
from shapely import constrained_delaunay_triangles

B=Path(__file__).resolve().parent
ROOT=B.parents[2]
ASSET='Assets/ChooGuard/Art/StationInterior/R25_Shops/artbox'
basis=json.loads((B/'MODEL-PLACEMENT-BASIS-R25.json').read_text())
u=np.array(basis['u']);v=np.array(basis['v']);start=np.array(basis['working_polygon_q'][0])
W=basis['u_limits'][1]-basis['u_limits'][0];D=basis['v_limits'][1]-basis['v_limits'][0]
yaw=math.degrees(math.atan2(-u[1],u[0]))
def q(x,z):return (start+u*x+v*z).tolist()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
materials={
 'black':{'color':[.025,.028,.034,1],'metallic':.42,'smoothness':.48},
 'white':{'color':[.9,.89,.84,1],'smoothness':.38},
 'steel':{'color':[.46,.48,.5,1],'metallic':.85,'smoothness':.65},
 'glass':{'color':[.73,.9,.91,.16],'transparent':True,'smoothness':.95,'metallic':.08},
 'acrylic':{'color':[.87,.91,.95,.24],'transparent':True,'smoothness':.82},
 'light':{'color':[1,.94,.8,1],'emission':[1.7,1.55,1.2,1]},
 'signwhite':{'color':[1,1,1,1],'emission':[1.65,1.65,1.65,1],'smoothness':.48},
 'signred':{'color':[.75,.005,.015,1],'emission':[.85,.002,.01,1],'smoothness':.55},
 'red':{'color':[.55,.008,.02,1],'smoothness':.3},
 'pink':{'color':[.8,.42,.55,1],'smoothness':.35},
 'mint':{'color':[.36,.7,.59,1],'smoothness':.35},
 'blue':{'color':[.22,.45,.74,1],'smoothness':.35},
 'yellow':{'color':[.95,.73,.12,1],'smoothness':.3},
 'paper':{'color':[.91,.88,.78,1],'smoothness':.14},
 'lavender':{'color':[.56,.4,.72,1],'smoothness':.3},
}
objects=[];imports=[];library={};maps={};used=[]
def add(kind,id,x,z,y,group,**kw):
 s={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':yaw,'group':group,'source_ids':['ARTBOX_2F_OFFICIAL_20260612','ARTBOX_2F_GTEE_20260720'],'qualification':'SOURCE_FORM_AND_RELATIONSHIP_MODEL_NOT_SURVEYED'}
 s.update(kw);objects.append(s);return s
def box(id,x,z,y,size,mat='black',group='Shell',**kw):return add('cube',id,x,z,y,group,size=size,material=mat,**kw)
def cyl(id,x,z,y,diam,height,mat='steel',group='Hardware',**kw):return add('cylinder',id,x,z,y,group,size=[diam,height/2,diam],material=mat,**kw)
def stage(p,dest):
 p=p.resolve();imports.append({'source':str(p),'destination':dest,'sha256':sha(p)});return ASSET+'/'+dest
def clean(s):return re.sub(r'[^A-Za-z0-9_]','_',s)

# Preserve source materials, real UVs, normal maps, and metallic/roughness response.
for name in ['notebook','pencilholder','knitbears','deskfan','phonecase','phonecase2','shoppingbasket','giftbox']:
 folder=B/'sources/models'/name;j=json.loads((folder/'UNITY-MATERIAL-SOURCES.json').read_text())
 stage(folder/(name+'.fbx'),'Models/'+name+'/'+name+'.fbx')
 stage(folder/'LICENSE-SOURCE.json','Models/'+name+'/LICENSE-SOURCE.json')
 matkeys={}
 for mi,m in enumerate(j['materials']):
  mk='SRC_'+name+'_'+str(mi);matkeys[m['name']]=mk
  spec={'color':m['base_color'],'metallic':m['metallic'],'smoothness':1-m['roughness']}
  for t in m['textures']:
   slot=t['slot'].upper();source=Path(t['file'])
   if not source.exists():continue
   if slot=='BASE COLOR':spec['texture_asset']=stage(source,'Models/'+name+'/base_'+str(mi)+'.png');spec['color']=[1,1,1,1]
   elif slot=='NORMAL MAP':spec['normal_asset']=stage(source,'Models/'+name+'/'+str(mi)+'_normal.png')
   elif slot=='METALLIC ROUGHNESS':
    mr=np.array(Image.open(source).convert('RGB'));out=np.empty((*mr.shape[:2],4),dtype=np.uint8);out[:,:,:3]=mr[:,:,2,None];out[:,:,3]=255-mr[:,:,1]
    p=folder/('urp_'+str(mi)+'_metallic_smoothness.png');Image.fromarray(out).save(p)
    spec['metallic_gloss_asset']=stage(p,'Models/'+name+'/'+str(mi)+'_metallic_smoothness.png')
  materials[mk]=spec
 maps[name]={n:matkeys[ms[0]] for n,ms in j['renderer_materials'].items() if ms and ms[0] in matkeys}
 maps[name]['*']=next(iter(matkeys.values()))
 used.append({'name':name,'source_asset':'Models/'+name+'/'+name+'.fbx','source':json.loads((folder/'LICENSE-SOURCE.json').read_text())})

# Exact downloaded modern POS model from the immutable shared source collection.
pos=B.parent/'free-assets/pos-terminal';posmap={}
stage(pos/'POS-Terminal.fbx','Models/pos/POS-Terminal.fbx')
stage(pos/'AUTHOR-METADATA.json','Models/pos/AUTHOR-METADATA.json')
posdiff=stage(pos/'textures/Image_0.png','Models/pos/base.png')
posnormal=stage(pos/'textures/Image_2.png','Models/pos/shell_normal.png')
posdetail=stage(pos/'textures/Image_3.png','Models/pos/detail_normal.png')
materials['POS_shell']={'color':[1,1,1,1],'texture_asset':posdiff,'normal_asset':posnormal,'metallic':.3,'smoothness':.5}
materials['POS_detail']={'color':[.035,.038,.042,1],'normal_asset':posdetail,'metallic':.4,'smoothness':.48}
maps['pos']={'Terminal001_Material #2100454184_0':'POS_shell','Terminal001_Material #2100454185_0':'POS_detail','*':'POS_shell'}
used.append({'name':'pos','source_asset':'Models/pos/POS-Terminal.fbx','source':json.loads((pos/'AUTHOR-METADATA.json').read_text())})
def asset(name,id,x,z,y,fit,group='RealDownloadedMerchandise',turn=0,material=None):
 path=ASSET+('/Models/pos/POS-Terminal.fbx' if name=='pos' else '/Models/'+name+'/'+name+'.fbx')
 kw={'asset':path,'fit_size':fit,'collider':False,'yaw':yaw+turn,'material_map':maps[name],
 'source_ids':['FREE_'+name,'ARTBOX_2F_OFFICIAL_20260612'],'qualification':'DOWNLOADED_CC_BY_PHYSICAL_FORM_MODEL_VARIANT_NOT_EXACT_BRANCH_SKU'}
 if material:kw={'asset':path,'fit_size':fit,'collider':False,'yaw':yaw+turn,'material':material,'source_ids':['FREE_'+name,'ARTBOX_2F_OFFICIAL_20260612'],'qualification':'DOWNLOADED_MODEL_WITH_PHOTO_MATCHED_COLOR_MODEL_VARIANT'}
 return add('asset',id,x,z,y,group,**kw)

# The actual retained station Main floor and supports remain the floor and structure.
# No generated floor, source/native surface, or neighbor part is edited or duplicated.
box('OwnWestPartition',.035,D/2,1.62,[.07,3.24,D-.06],'white')
box('OwnEastInteriorPartition',W-.035,D/2,1.62,[.07,3.24,D-.06],'white')
box('OwnRearLowPartition',W/2,D-.035,.9,[W-.14,1.8,.07],'white')
box('OwnRearHighGlazing',W/2,D-.04,2.45,[W-.17,1.28,.012],'glass',collider=True)
for x in np.linspace(.12,W-.12,7):box('RearGlazingMullion_'+str(round(x,2)),x,D-.04,2.45,[.045,1.32,.06])
box('OwnLowWhiteCeiling',W/2,D/2,3.32,[W-.14,.10,D-.12],'white')
box('FrontActualBlackFascia',W/2,.075,2.99,[W-.08,.67,.15])
box('FrontTopCap',W/2,.12,3.34,[W-.07,.055,.22])
opening=[1.77,W-1.77]
for side,(a,b) in enumerate([(0.06,opening[0]),(opening[1],W-.06)]):
 box('FrontGlass_'+str(side),(a+b)/2,.08,1.31,[b-a-.045,2.58,.014],'glass')
 for x in [a,(a+b)/2,b]:box('FrontMetalMullion_'+str(side)+'_'+str(round(x,2)),x,.075,1.31,[.055,2.62,.095])
 for y in [.055,2.60]:box('FrontRail_'+str(side)+'_'+str(y),(a+b)/2,.075,y,[b-a,.055,.095])
 # Actual smallwares shelf inside the glazed display bay.
 for lev in range(4):box('WindowGlassShelf_'+str(side)+'_'+str(lev),(a+b)/2,.37,.32+lev*.49,[b-a-.14,.018,.51],'acrylic','FrontWindowDisplays',collider=False)

# Source paired doors stand open inward; central gap remains physically clear.
for side,x in enumerate([opening[0]+.04,opening[1]-.04]):
 box('OpenDoorGlass_'+str(side),x,.82,1.29,[.014,2.50,1.48],'glass','EntryDoors')
 for z in [.10,1.56]:box('OpenDoorStile_'+str(side)+'_'+str(z),x,z,1.29,[.048,2.53,.045],'black','EntryDoors')
 for h in [.045,2.54]:box('OpenDoorRail_'+str(side)+'_'+str(h),x,.82,h,[.048,.045,1.51],'black','EntryDoors')
 cyl('OpenDoorHandle_'+str(side),x+(.065 if side==0 else -.065),1.40,1.16,.022,.32,'steel','EntryDoors',collider=False)
 for h in [.22,2.30]:box('OpenDoorHinge_'+str(side)+'_'+str(h),x,.13,h,[.075,.11,.04],'steel','EntryDoors')
 # Source white EAS portal hardware, not a label or simple filled obstruction.
 ex=x+(.16 if side==0 else -.16)
 box('EASBase_'+str(side),ex,.38,.075,[.13,.15,.35],'white','EntryHardware')
 for z in [.23,.53]:box('EASUpright_'+str(side)+'_'+str(z),ex,z,.76,[.048,1.24,.047],'white','EntryHardware')
 box('EASTop_'+str(side),ex,.38,1.40,[.05,.055,.35],'white','EntryHardware')

# Photo-traced actual 2026 letter/mark outlines. All holes, backs, sidewalls and
# two-millimetre bevels are physical geometry; no fonts, TMP, or picture sign.
for c in ['white','red']:
 j=json.loads((B/('sign-'+c+'-beveled.json')).read_text());library['ActualSourceLogo_'+c]={'vertices':j['vertices'],'triangles':j['triangles']}
 add('library_mesh','ActualPhotoARTBOX_'+c,W/2,.001,3.0,'ActualSourceSolidSign',mesh_key='ActualSourceLogo_'+c,material='sign'+c,collider=False,yaw=yaw+180)
for x in np.linspace(1.45,W-1.45,6):
 box('SignMountSpacer_'+str(round(x,2)),x,.025,2.99,[.03,.16,.04],'steel','ActualSourceSolidSign',collider=False)
box('ConcealedSignDriver',W-.30,.18,3.13,[.30,.10,.08],'black','ActualSourceSolidSign',collider=False)

# White low ceiling with real circular housings and two source-visible cassettes.
for xi,x in enumerate(np.linspace(.65,W-.65,5)):
 for zi,z in enumerate(np.linspace(.65,D-.65,5)):
  cyl('DownlightTrim_'+str(xi)+'_'+str(zi),x,z,3.255,.155,.025,'white','CeilingLights',collider=False)
  cyl('DownlightLens_'+str(xi)+'_'+str(zi),x,z,3.237,.117,.012,'light','CeilingLights',collider=False)
for n,(x,z) in enumerate([(1.6,1.6),(W-1.6,4.6)]):
 box('CassetteFrame_'+str(n),x,z,3.23,[.86,.08,.86],'white','CeilingEquipment',collider=False)
 box('CassetteInset_'+str(n),x,z,3.18,[.63,.015,.63],'black','CeilingEquipment',collider=False)
 for i in range(16):box('CassetteVent_'+str(n)+'_'+str(i),x-.28+i*.037,z,3.167,[.012,.012,.57],'white','CeilingEquipment',collider=False)
for x in [1.8,W-1.8]:
 add('light','InteriorLight_'+str(round(x,1)),x,D/2,2.92,'CeilingLights',color=[1,.94,.85,1],range=4.3,intensity=.55)

# Store-specific dark shelving frames, editable shelves and real open peg slots.
panel=polybox(-.5,-.5,.5,.5)
for xx in np.linspace(-.44,.44,9):
 for yy in np.linspace(-.46,.46,16):panel=panel.difference(polybox(xx-.006,yy-.0028,xx+.006,yy+.0028))
verts=[];tris=[]
def tri3(a,b,c):
 k=len(verts);verts.extend([a,b,c]);tris.extend([k,k+1,k+2])
for t in constrained_delaunay_triangles(panel).geoms:
 a,b,c=list(t.exterior.coords)[:3]
 tri3([*a,0],[*b,0],[*c,0]);tri3([*c,-.025],[*b,-.025],[*a,-.025])
for ring in [panel.exterior,*panel.interiors]:
 for a,b in zip(list(ring.coords),list(ring.coords)[1:]):
  tri3([*a,0],[*a,-.025],[*b,-.025]);tri3([*a,0],[*b,-.025],[*b,0])
library['PerforatedRetailPegboard']={'vertices':verts,'triangles':tris}
def shelf(id,x,z,width,depth,height,turn=0):
 # Plan coordinates relative to one rack, using its local direction.
 angle=math.radians(turn);dx=np.array([math.cos(angle),-math.sin(angle)]);dz=np.array([math.sin(angle),math.cos(angle)])
 def part(n,xx,zz,y,size,mat='black'):
  p=np.array([x,z])+dx*xx+dz*zz;return box(id+'_'+n,float(p[0]),float(p[1]),y,size,mat,'SourceShelving',yaw=yaw+turn,collider=False)
 part('base',0,0,.13,[width,.23,depth])
 pp=np.array([x,z])+dz*(depth/2-.025)
 add('library_mesh',id+'_PhysicalPerforatedPegboard',float(pp[0]),float(pp[1]),height/2,'SourceShelving',mesh_key='PerforatedRetailPegboard',material='black',size=[width,height,1],yaw=yaw+turn+180,collider=False)
 for xx in [-width/2+.025,width/2-.025]:part('upright_'+str(xx),xx,depth/2-.025,height/2,[.045,height,.06])
 for k,y in enumerate(np.linspace(.28,height-.17,5)):
  part('shelf_'+str(k),0,0,float(y),[width,.027,depth]);part('labelrail_'+str(k),0,-depth/2+.015,float(y)+.025,[width,.043,.025],'white')
 return dx,dz
for side in [0,1]:
 x=.36 if side==0 else W-.36;turn=90 if side==0 else -90
 for k,z in enumerate([2.14,3.38,4.62]):
  shelf('WallRack_'+str(side)+'_'+str(k),x,z,1.17,.48,2.30,turn)
  # Source upper back-lit product category screen assemblies.
  box('CategoryHousing_'+str(side)+'_'+str(k),x,z,2.49,[.10,.30,.94],'black','SourceShelving',collider=False)
  box('CategoryIlluminatedPanel_'+str(side)+'_'+str(k),x+(.055 if side==0 else -.055),z,2.49,[.012,.24,.85],'light','SourceShelving',collider=False)

for k,x in enumerate([1.50,2.81,4.12,5.43]):shelf('RearRack_'+str(k),x,D-.34,1.23,.46,1.82)
for k,(x,z) in enumerate([(2.0,3.95),(W-2.0,3.95)]):
 # Two physical gondola runs; visible source sales aisles preserved between them.
 shelf('GondolaFront_'+str(k),x,z-.69,.98,.57,1.48)
 shelf('GondolaBack_'+str(k),x,z+.54,.98,.57,1.48,180)
box('SeasonalFrontTableBase',W/2,1.55,.38,[1.75,.76,.94],'white','SeasonalGondola')
box('SeasonalFrontTableTop',W/2,1.55,.77,[1.84,.045,1.02],'steel','SeasonalGondola')
box('SeasonalRiser',W/2,1.81,1.01,[1.7,.45,.32],'white','SeasonalGondola')
for i,x in enumerate([W/2-.53,W/2,W/2+.53]):asset('deskfan','DownloadedRealCageDeskFan_'+str(i),x,1.59,1.25,[.26,.36,.20],turn=180)

# The original current branch shows dense small packaged goods; these are actual
# downloaded three-dimensional case/notebook/pen/toy models, not names on boxes.
palette=['pink','mint','blue','yellow','lavender','white']
for side in [0,1]:
 for bay,z in enumerate([2.14,3.38,4.62]):
  for level in range(4):
   y=.31+level*.45
   for item in range(4):
    zz=z-.43+item*.28;x=.39 if side==0 else W-.39
    # Downloaded rounded phone shell has real camera apertures and side edges.
    asset('phonecase2','CaseWall_'+str(side)+'_'+str(bay)+'_'+str(level)+'_'+str(item),x,zz,y,[.075,.16,.014],turn=90 if side==0 else -90,material=palette[(bay+level+item+side)%6])
  for level in range(3):
   # Open notebooks retain source ring binding, pages, pen and separate cover.
   asset('notebook','NotebookWall_'+str(side)+'_'+str(bay)+'_'+str(level),.35 if side==0 else W-.35,z,.36+level*.56,[.34,.035,.24],turn=90 if side==0 else -90)
  asset('pencilholder','PensWall_'+str(side)+'_'+str(bay),.34 if side==0 else W-.34,z+.40,1.75,[.16,.25,.15])

for k,x in enumerate([1.50,2.81,4.12,5.43]):
 for level in range(3):
  asset('giftbox','GiftRear_'+str(k)+'_'+str(level),x-.3,D-.38,.31+level*.49,[.26,.24,.25])
  asset('knitbears','ToysRear_'+str(k)+'_'+str(level),x+.28,D-.36,.31+level*.49,[.48,.29,.24],turn=180)

for k,x in enumerate([2.0,W-2.0]):
 for level in range(3):
  y=.31+level*.43
  for z in [2.98,3.55,4.25,4.8]:
   asset('phonecase','PhoneAccessory_'+str(k)+'_'+str(level)+'_'+str(z),x,z,y,[.11,.20,.018],turn=180 if k==0 else 0)
  asset('notebook','RingNotebookGondola_'+str(k)+'_'+str(level),x,3.17,y,[.33,.035,.24])
 for z in [3.3,4.6]:asset('pencilholder','PensGondola_'+str(k)+'_'+str(z),x,z,1.52,[.16,.25,.15])

# Actual closed umbrella bundles model the photo-visible merchandise: fluted
# cloth, strap, telescoping shaft and handle are physical separate parts.
def fluted_bundle():
 vertices=[];tris=[];n=24
 for yy,r in [(0,.027),(.035,.035),(.38,.039),(.48,.018)]:
  for i in range(n):
   a=2*math.pi*i/n;rr=r*(1 if i%2==0 else .85);vertices.append([rr*math.cos(a),yy,rr*math.sin(a)])
 for j in range(3):
  for i in range(n):a=j*n+i;b=j*n+(i+1)%n;c=(j+1)*n+i;d=(j+1)*n+(i+1)%n;tris.extend([a,c,b,b,c,d])
 for j in [0,3]:
  center=len(vertices);vertices.append([0,j/3*.48,0])
  for i in range(n):tris.extend([center,j*n+i,j*n+(i+1)%n] if j==0 else [center,j*n+(i+1)%n,j*n+i])
 return {'vertices':vertices,'triangles':tris}
library['FlutedClosedUmbrella']=fluted_bundle()
for row in range(2):
 for i in range(12):
  x=.18+i*.124;z=.30+row*.21;y=.35+row*.49;mat=palette[(i+row)%6]
  add('library_mesh','ClosedUmbrellaCloth_'+str(row)+'_'+str(i),x,z,y,'FrontWindowDisplays',mesh_key='FlutedClosedUmbrella',material=mat,collider=False)
  cyl('UmbrellaStrap_'+str(row)+'_'+str(i),x,z,y+.20,.080,.025,mat,'FrontWindowDisplays',collider=False)
  cyl('UmbrellaShaft_'+str(row)+'_'+str(i),x,z,y+.50,.012,.055,'steel','FrontWindowDisplays',collider=False)
  cyl('UmbrellaHandle_'+str(row)+'_'+str(i),x,z,y+.55,.035,.065,'black','FrontWindowDisplays',collider=False)
for side in [0,1]:
 for k in range(6):
  x=(.22+k*.24) if side==0 else (W-1.58+k*.24)
  asset('phonecase','WindowAccessory_'+str(side)+'_'+str(k),x,.36,1.36,[.13,.23,.018],turn=180,material=palette[(k+side)%6])
for i in range(9):
 asset('shoppingbasket','ActualDownloadedBasket_'+str(i),W-2.04,.62,.03+i*.046,[.43,.21,.30],'EntryHardware',material='black')
 # Branch source red/black stacked basket lip, each independently editable.
 for z in [.47,.77]:box('BasketRedLip_'+str(i)+'_'+str(z),W-2.04,z,.22+i*.046,[.43,.013,.014],'red','EntryHardware',collider=False)

# Photo-visible staff checkout is at rear side; exact hidden counter dimensions
# are qualified MODEL. Real downloaded POS is used on the actual counter.
box('CheckoutCounter',W-1.03,D-1.15,.48,[1.23,.96,.60],'white','Checkout')
box('CheckoutCountertop',W-1.03,D-1.15,.98,[1.28,.045,.65],'black','Checkout')
asset('pos','DownloadedPOS',W-.94,D-1.15,1.01,[.36,.37,.29],'Checkout',turn=180)
box('CashDrawer',W-1.12,D-1.08,1.025,[.37,.065,.34],'black','Checkout',collider=False)

# Import attribution remains beside every own runtime model asset.
packet={'store_key':'artbox','origin_q':q(W/2,D/2),'floor_world_y':7.0,'source_packet':'SOURCE-EVIDENCE-R25.json','materials':materials,'source_imports':imports,'mesh_library':library,'objects':objects,
 'MODEL_qualification':'Exact branch photographs guide visible forms and relative arrangement; dimensions, hidden checkout, merchandise variants and tenant rear limits remain MODEL, not surveyed/asbuilt equality.'}
(B/'STORE-PACKET-R25.json').write_text(json.dumps(packet,separators=(',',':')))
(B/'ACTUAL-MODELS-USED-R25.json').write_text(json.dumps({'used':used,'actual_downloaded_asset_instance_count':sum(o['kind']=='asset' for o in objects),'source_unrelated_umbrella_prototype_not_used':True,'primitive_text_labels':0,'TMP_objects':0,'sign_photo_rectangles':0},indent=2))
scope='C180_R25_ARTBOX_APPLY_001'
src=(B.parent/'StoreBuilderR25-v3-FREE-ASSETS.template.cs').read_text().replace('__STORE_KEY__','artbox').replace('__WORKER_THREAD__','classic:/root/r25_artbox').replace('__EXACT_SCOPE__',scope).replace('__PACKET_PATH__',str(B/'STORE-PACKET-R25.json')).replace('__PACKET_SHA256__',sha(B/'STORE-PACKET-R25.json'))
(B/'ApplyStoreR25.cs').write_text(src)
print(json.dumps({'objects':len(objects),'assets':sum(o['kind']=='asset' for o in objects),'imports':len(imports),'room_MODEL_m':[W,D],'packet_sha256':sha(B/'STORE-PACKET-R25.json')}))
