import json,hashlib,math,random,shutil
from pathlib import Path
B=Path(__file__).resolve().parent;R=B.parents[2];A='Assets/ChooGuard/Art/StationInterior/R25_Shops/banwoldang';S=B/'sources/models';P=B/'sign-model-authoring'
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
materials={
 'fascia':{'color':[.075,.074,.065,1],'metallic':.25,'smoothness':.42},
 'black':{'color':[.012,.014,.015,1],'metallic':.25,'smoothness':.45},
 'tile':{'color':[.88,.9,.89,1],'smoothness':.68},
 'grout':{'color':[.52,.54,.53,1],'smoothness':.3},
 'steel':{'color':[.59,.63,.64,1],'metallic':.92,'smoothness':.79},
 'gold_ladle':{'color':[.65,.37,.08,1],'metallic':.88,'smoothness':.83},
 'orange_tool':{'color':[1,.21,.012,1],'smoothness':.63},
 'glass':{'color':[.72,.94,.90,.16],'transparent':True,'metallic':.05,'smoothness':.93},
 'white_sign':{'color':[.95,.96,.93,1],'emission':[.9,.92,.88,1],'smoothness':.54},
 'red_seal':{'color':[.73,.009,.035,1],'emission':[.32,.002,.006,1],'smoothness':.46},
 'menu_back':{'color':[.89,.93,.93,1],'emission':[.35,.39,.39,1],'smoothness':.5},
 'frame':{'color':[.45,.40,.28,1],'metallic':.65,'smoothness':.6},
 'pendant_red':{'color':[.46,.008,.008,1],'metallic':.62,'smoothness':.82},
 'bulb':{'color':[1,.92,.67,1],'emission':[.6,.52,.32,1],'smoothness':.5},
 'sauce':{'color':[.27,.028,.006,1],'metallic':.03,'smoothness':.91},
 'chicken_glaze':{'color':[.46,.11,.022,1],'smoothness':.84,'texture_asset':A+'/Textures/ChickenScanAlbedo.png'},
 'pos_body':{'color':[.035,.038,.042,1],'metallic':.3,'smoothness':.52},
 'pos_screen':{'color':[.025,.03,.042,1],'metallic':.05,'smoothness':.82},
 'source_cup':{'color':[.94,.94,.88,1],'smoothness':.3},
 'wood':{'color':[.58,.40,.22,1],'smoothness':.4}}
for k,c in json.loads((P/'SOURCE-INK-COLORS.json').read_text()).items():materials[k]={'color':c+[1],'smoothness':.42,'emission':[v*.18 for v in c]+[1]}
imports=[]
def imp(src,dst):
 src=Path(src).resolve();imports.append({'source':str(src),'destination':dst,'sha256':sha(src)});return A+'/'+dst
assets={}
for key,f in [('tray','tray/tray-Unity.fbx'),('chicken','chicken/chicken-Unity.fbx'),('spatula','slotted-spatula/slotted-spatula-Unity.fbx'),('ladle','ladle/ladle-Unity.fbx'),('counter','counter/counter-source-unity.fbx'),('pos','pos/POS-Terminal.fbx')]:assets[key]=imp(S/f,'Models/'+Path(f).name)
imp(S/'chicken/textures/Image_0.png','Textures/ChickenScanAlbedo.png')
cup=B.parent/'free-assets/kenney-food-kit/Models/FBX format/cup.fbx';assets['cup']=imp(cup,'Models/KenneyCup.fbx')
credits=[]
for key in ['tray','chicken','slotted-spatula','ladle','counter','pos']:
 d=json.loads((S/key/'AUTHOR-METADATA.json').read_text());credits.append({'used_model':key,'name':d['name'],'author':d['user']['displayName'],'source_url':d['viewerUrl'],'license':d['license'],'modifications':{'tray':'Source tray scaled to visible rectangular mixing-pan proportions; clean stainless own URP material','chicken':'Own 2400-triangle derivative of source scan, varying rotations/sizes and sauce colored own URP using source albedo','slotted-spatula':'Actual slots retained; own orange source-photo material','ladle':'Original bowl/curved-handle mesh retained; own polished gold URP removes source rust texture','counter':'Source downloaded kitchen-counter mesh part only from existing source derivative; fitted undercounter unit with source handles/vent','pos':'Actual terminal and stand; all two source material slots mapped explicitly to own URP'}[key]})
credits.append({'used_model':'cup','author':'Kenney','name':'Food Kit cup','source_url':'https://kenney.nl/assets/food-kit','license':'CC0','modifications':'Own plain paper-cup URP; fitted small takeaway cup geometry'})
(S/'SOURCE-CREDITS.json').write_text(json.dumps(credits,ensure_ascii=False,indent=2));imp(S/'SOURCE-CREDITS.json','SOURCE-CREDITS.json')
objects=[]
def add(i,kind,q,y,material=None,size=None,group='',**kw):
 o={'id':i,'kind':kind,'q':q,'y':y,'group':group,'source_ids':kw.pop('source_ids',['JUL23-SUIN72-FULL00','JUL23-SUIN72-FULL03','JUL23-SUIN72-FULL06']),'qualification':'SOURCE_VISIBLE_PHOTO_PROPORTIONED_MODEL_NOT_FIELD_MEASUREMENT'}
 if material:o['material']=material
 if size:o['size']=size
 o.update(kw);objects.append(o);return o
def box(i,q,y,size,mat,group,**kw):return add(i,'box',q,y,mat,size,group,**kw)
def asset(i,key,q,y,fit,mp,group,yaw=0,**kw):return add(i,'asset',q,y,None,None,group,asset=assets[key],fit_size=fit,material_map=mp,yaw=yaw,**kw)
lib=json.loads((P/'SIGN-MESH-LIBRARY.json').read_text())
# Physical sign fascia on existing front datum. Native room, doors/glass/columns are retained.
box('FASCIA_SOURCE_DARK_METAL',[.316,-43.27],2.77,[.08,.76,5.72],'fascia','SourceFascia',yaw=-.22368)
for yy in [2.40,3.14]:box('FASCIA_EDGE_'+str(yy),[.262,-43.27],yy,[.035,.035,5.76],'black','SourceFascia',yaw=-.22368)
for key,mat,x in [('fascia_red_actual_seal','red_seal',.246),('fascia_white_actual_ink','white_sign',.234)]:add('SIGN_'+key,'library_mesh',[x,-43.27],2.77,mat,group='SourceClosedContourSign',mesh_key=key,yaw=-90.22368,collider=False)
# Source-visible tiled service counter, physical tiles and black stone cap.
box('COUNTER_PHYSICAL_CORE',[1.91,-43.62],.49,[.84,.98,4.80],'grout','ServiceCounter')
box('COUNTER_BLACK_STONE_TOP',[1.89,-43.62],1.005,[1.02,.055,4.92],'black','ServiceCounter')
for row in range(6):
 for col in range(16):
  z=-45.995+col*.30
  box('COUNTER_GLAZED_TILE_%02d_%02d'%(row,col),[1.480,z],.095+row*.15,[.018,.145,.294],'tile','CounterIndividualTiles',collider=False)
# Real downloaded source counter rear-facing fridge/shelves; only this trimmed undercounter source part used.
asset('DOWNLOADED_UNDERCOUNTER_PREP_UNIT','counter',[2.22,-44.75],.04,[2.35,.86,.69],{'wire_160160160':'steel','*':'steel'},'DownloadedPrep',yaw=90)
# Customer side glass sneeze guard, source black stanchions; no front room closure.
for z in [-45.96,-44.10,-42.22]:box('GUARD_UPRIGHT_'+str(z),[1.51,z],1.28,[.028,.51,.028],'black','CounterGlass')
box('SOURCE_SNEEZE_GLASS_FRONT',[1.52,-44.10],1.29,[.012,.43,3.72],'glass','CounterGlass',collider=False)
box('SOURCE_SNEEZE_GLASS_TOP',[1.91,-44.10],1.55,[.78,.012,3.72],'glass','CounterGlass',collider=False)
box('GUARD_TOP_BLACK_RAIL',[1.52,-44.10],1.56,[.03,.026,3.76],'black','CounterGlass')
# Real downloaded pan assets. Two observed food preparations, exact ricecake shapes remain an evidence/model gap.
for k,z in enumerate([-45.25,-44.08,-42.91]):
 asset('DOWNLOADED_STAINLESS_MIXING_PAN_%d'%k,'tray',[1.98,z],1.038,[1.08,.16,.64],{'Material.001':'steel'},'DownloadedMixingPans',yaw=90,collider=False)
 box('REAL_SAUCE_SURFACE_%d'%k,[1.98,z],1.09,[.54,.012,.93],'sauce','PhysicalSauce',collider=False)
# Multiple editable portions of the actual scanned chicken model, with photo-matched glossy sauce.
rng=random.Random(28)
for i in range(48):
 pan=0 if i<26 else 1;zc=[-45.25,-44.08][pan];x=1.79+rng.random()*.37;z=zc-.39+rng.random()*.78;h=.055+rng.random()*.035
 asset('SCANNED_SAUCE_CHICKEN_%02d'%i,'chicken',[x,z],1.10+(i%3)*.022,[.073+rng.random()*.045,h,.07+rng.random()*.04],{'material_0':'chicken_glaze'},'DownloadedChickenPortions',yaw=rng.random()*360,collider=False)
# Source orange slotted turners and golden ladle, all actual downloaded meshes.
for i,z in enumerate([-45.61,-44.40]):asset('DOWNLOADED_SOURCE_ORANGE_SLOTTED_TURNER_%d'%i,'spatula',[2.08,z],1.12,[.42,.06,.105],{'Metal':'orange_tool','Rubber_Handle1':'orange_tool'},'DownloadedUtensils',yaw=15+i*12,euler=[0,15+i*12,58],collider=False)
asset('DOWNLOADED_GOLD_SAUCE_LADLE','ladle',[1.91,-43.68],1.11,[.19,.43,.13],{'Material_25':'gold_ladle'},'DownloadedUtensils',euler=[34,25,0],collider=False)
# POS sits on the source right counter return; two original slots explicitly mapped.
asset('DOWNLOADED_POS_TERMINAL','pos',[1.96,-41.63],1.04,[.42,.40,.30],{'Material_2100454184':'pos_body','Material_2100454185':'pos_screen'},'DownloadedPOS',yaw=-90,collider=False)
for i in range(3):asset('DOWNLOADED_TAKEAWAY_CUP_%02d'%i,'cup',[2.25,-41.27-i*.17],1.04,[.11,.15,.11],{'*':'source_cup'},'DownloadedSmallwares',collider=False)
# Source-visible rear tile lining and source menu, without inventing concealed back-of-house rooms.
# Finish lies directly inside actual back wall. Native column body has no new intersections.
box('REAR_NATIVE_WALL_TILE_FINISH',[4.641,-43.35],1.71,[.018,2.75,5.53],'grout','SourceRearFinish')
for row in range(9):
 for col in range(19):
  z=-46.04+col*.294
  box('REAR_GLAZED_TILE_%02d_%02d'%(row,col),[4.627,z],.40+row*.30,[.011,.293,.287],'tile','RearIndividualTiles',collider=False)
# Menu at visible source service elevation, physically suspended in front of rear wall/column.
box('MENU_PHYSICAL_CASE',[3.54,-43.56],2.18,[.075,.90,3.86],'frame','PhysicalSourceMenu')
box('MENU_EMISSIVE_FACE',[3.494,-43.56],2.18,[.014,.82,3.75],'menu_back','PhysicalSourceMenu')
for key in json.loads((P/'SOURCE-INK-COLORS.json').read_text()):add('MENU_CLOSED_INK_'+key,'library_mesh',[3.483,-43.56],2.18,key,group='PhysicalSourceMenu',mesh_key=key,yaw=-90.22368,collider=False)
for z in [-45.10,-42.02]:box('MENU_HANGER_'+str(z),[3.54,z],2.82,[.016,.40,.016],'steel','PhysicalSourceMenu',collider=False)
# Shape-accurate red pendant shades; physical bulbs/emission only, never Unity Light components.
def lathe(profile,steps=36):
 v=[];t=[]
 for r,y in profile:
  for j in range(steps):a=2*math.pi*j/steps;v.append([r*math.cos(a),y,r*math.sin(a)])
 for k in range(len(profile)-1):
  for j in range(steps):a=k*steps+j;b=k*steps+(j+1)%steps;c=(k+1)*steps+(j+1)%steps;d=(k+1)*steps+j;t.extend([a,b,c,a,c,d])
 return {'vertices':v,'triangles':t}
lib['red_pendant_closed_shell']=lathe([(0,.24),(.035,.23),(.045,.17),(.10,.09),(.15,.035),(.152,.02),(.145,.01),(.135,.03),(.09,.10),(.038,.18),(0,.19)])
for i,z in enumerate([-44.93,-43.72,-42.51]):
 add('PENDANT_PHYSICAL_SHADE_%d'%i,'library_mesh',[2.00,z],2.22,'pendant_red',group='PhysicalFixturesNoLights',mesh_key='red_pendant_closed_shell',collider=False)
 box('PENDANT_CORD_%d'%i,[2.00,z],2.83,[.012,.72,.012],'black','PhysicalFixturesNoLights',collider=False)
 add('PENDANT_BULB_MESH_%d'%i,'sphere',[2.00,z],2.27,'bulb',[.055,.055,.055],'PhysicalFixturesNoLights',collider=False)
box('WHITE_SOURCE_TRACK',[1.15,-43.4],2.98,[.035,.035,5.0],'tile','PhysicalFixturesNoLights',collider=False)
for i,z in enumerate([-45.4,-44.5,-43.6,-42.7,-41.8]):
 box('TRACK_DROP_%d'%i,[1.15,z],2.85,[.014,.24,.014],'tile','PhysicalFixturesNoLights',collider=False)
 add('TRACK_BULB_MESH_%d'%i,'sphere',[1.15,z],2.71,'bulb',[.075,.10,.075],'PhysicalFixturesNoLights',collider=False)
# Source wall shelves on own north portion, short visible finish only; all source-hidden rooms left empty.
for i,y in enumerate([1.72,2.20]):box('VISIBLE_WOOD_SHELF_%d'%i,[3.12,-40.67],y,[.70,.035,.25],'wood','SourceVisibleShelves')
packet={'schema':'R25-source-bound-store-v5','store_key':'banwoldang','origin_q':[2.45,-43.28],'floor_world_y':7.0,'source_packet':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/banwoldang/SOURCE-EVIDENCE-R25.json','source_imports':imports,'materials':materials,'mesh_library':lib,'objects':objects,'metric_qualification':'All photo fittings MODEL, no measured SKU/digital twin completeness claim','native_shell_reused':True,'lights_created':False,'text_components_created':False}
(B/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
# Constructor uses current no-lights template, frozen script and packet SHA.
scope='C180_R25_BANWOLDANG_APPLY_001';s=(B.parent/'StoreBuilderR25-v5-NO-LIGHTS.template.cs').read_text();subs={'__STORE_KEY__':'banwoldang','__WORKER_THREAD__':'classic:/root/r25_banwoldang','__EXACT_SCOPE__':scope,'__PACKET_PATH__':str(B/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha(B/'STORE-PACKET-R25.json')}
for a,b in subs.items():s=s.replace(a,b)
(B/'ApplyStoreR25.cs').write_text(s)
(B/'FROZEN-PREPARED-INPUTS-R25.json').write_text(json.dumps({'scope':scope,'packet_sha256':sha(B/'STORE-PACKET-R25.json'),'script_sha256':sha(B/'ApplyStoreR25.cs'),'objects':len(objects),'downloaded_asset_instances':sum(o['kind']=='asset' for o in objects),'imports':len(imports),'no_light_or_text_specs':not any(o['kind'] in ['light','text'] for o in objects),'source_model_credits':credits},ensure_ascii=False,indent=2))
print('PREPARED',len(objects),'objects',sum(o['kind']=='asset' for o in objects),'downloaded instances','packet bytes',(B/'STORE-PACKET-R25.json').stat().st_size)
