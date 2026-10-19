from pathlib import Path
import json,hashlib,math,sys,cv2,numpy as np,shutil
from PIL import Image
ROOT=Path('/Users/um-yunsang/CHOOGuard');R=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/flowers';S=R/'sources';A='Assets/ChooGuard/Art/StationInterior/R25_Shops/flowers';imports=[];materials={};objects=[];lib={}
def material(k,c=(.9,.9,.9,1),**kw):materials[k]={'color':list(c),**kw};return k
def imp(p,dest):
 p=Path(p);imports.append({'source':str(p.resolve()),'destination':dest,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()});return A+'/'+dest
white=material('cabinet_white',(.92,.92,.89,1),smoothness=.48);black=material('charcoal',(.045,.05,.055,1),smoothness=.48);steel=material('brushed_stainless',(.6,.64,.65,1),metallic=.8,smoothness=.6);glass=material('glass',(.8,.95,.96,.15),transparent=True,smoothness=.97);gold=material('amber_sign',(.98,.71,.08,1),emission=[1.5,.86,.05,1]);glow=material('sign_white',(.95,.98,1,1),emission=[3,3.4,3.8,1]);warm=material('warm_led',(1,.94,.76,1),emission=[2.5,2.3,1.7,1]);green=material('soil_dark',(.08,.1,.03,1));ribbon=material('ribbon_red',(.69,.03,.05,1),smoothness=.35)
# Own material adaptations retain the actual downloaded flower geometry/UVs.
for variant,hue,sat in [('red',0,230),('yellow',24,215),('pink',164,95),('cream',24,40)]:
 for i in range(5):
  src=S/'rose_bouquet'/f'Image_{i}.png';im=cv2.imread(str(src),cv2.IMREAD_UNCHANGED)
  if variant!='red':
   hsv=cv2.cvtColor(im[:,:,:3],cv2.COLOR_BGR2HSV);sel=(im[:,:,2]>im[:,:,1]*1.4)&(im[:,:,2]>im[:,:,0]*1.4);hsv[:,:,0][sel]=hue;hsv[:,:,1][sel]=sat;hsv[:,:,2][sel]=np.clip(hsv[:,:,2][sel].astype('float')*.7+85,0,255).astype('uint8');im=cv2.cvtColor(hsv,cv2.COLOR_HSV2BGR)
  p=S/'rose_bouquet'/f'{variant}_petal_{i}.png';cv2.imwrite(str(p),im);t=imp(p,f'Models/rose_bouquet/{p.name}');material(f'{variant}_petal_{i}',texture_asset=t,smoothness=.26,double_sided=True)
papertex=imp(S/'rose_bouquet/Image_5.png','Models/rose_bouquet/paper_fibers.png');material('kraft',texture_asset=papertex,smoothness=.15,double_sided=True);material('pink_paper',(1,.73,.81,1),texture_asset=papertex,smoothness=.12,double_sided=True);material('cream_paper',(1,.96,.84,1),texture_asset=papertex,smoothness=.12,double_sided=True)
rose=imp(S/'rose_bouquet/rose_bouquet-Unity.fbx','Models/rose_bouquet/rose_bouquet.fbx')
shelf=imp(S/'white_shelf/white_shelf.fbx','Models/white_shelf.fbx');modern=imp(S/'modern_shelf/modern_shelf.fbx','Models/modern_shelf.fbx')
# Poly Haven CC0 source assets. Per-leaf alpha preserved in our URP material.
ph={}
for key in ['potted_plant_01','potted_plant_04','ceramic_vase_01','wicker_basket_02']:
 ph[key]=imp(S/key/(key+'-Unity.fbx'),'Models/'+key+'.fbx')
 if key=='potted_plant_01':
  leaves=Image.open(S/key/(key+'_leaves_diff_1k.png')).convert('RGBA');alpha=Image.open(S/key/(key+'_leaves_alpha_1k.png')).convert('L');leaves.putalpha(alpha);p=S/key/'leaves_rgba.png';leaves.save(p)
  material('plant01_leaves',texture_asset=imp(p,'Models/potted_plant_01/leaves_rgba.png'),alpha_clip=True,double_sided=True,smoothness=.3)
  material('plant01_pot',texture_asset=imp(S/key/(key+'_pot_diff_1k.png'),'Models/potted_plant_01/pot_diff.png'),normal_asset=imp(S/key/(key+'_pot_nor_gl_1k.png'),'Models/potted_plant_01/pot_normal.png'),smoothness=.22)
 else:
  texture=imp(S/key/(key+'_diff_1k.jpg'),'Models/'+key+'/diff.jpg');material(key,texture_asset=texture,smoothness=.5 if 'vase' in key else .25)
shared=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets'
fridge=imp(shared/'commercial-refrigerator/CommercialRefrigerator-Empty.fbx','Models/flower_cooler.fbx');pos=imp(shared/'pos-terminal/POS-Terminal.fbx','Models/POS.fbx')
material('pos_body',(.24,.25,.26,1),texture_asset=imp(shared/'pos-terminal/textures/Image_0.png','Models/pos/basecolor.png'),smoothness=.5)
# Helpers: q coordinates remain in actual native room; meters are qualified MODEL.
yaw=.22415
counter=0
def obj(kind,id,q,y,**kw):
 o={'kind':kind,'id':id,'group':kw.pop('group','Interior'),'q':list(q),'y':y,'yaw':kw.pop('yaw',yaw),'source_ids':kw.pop('source_ids',['PHOTO2024','VIDEO2023']),'classification':'MODEL photo-informed fit and placement',**kw};objects.append(o);return o
def box(id,q,y,size,mat=white,**kw):return obj('box',id,q,y,size=size,material=mat,**kw)
def asset(id,q,y,sz,a,mat=None,**kw):
 o=obj('asset',id,q,y,fit_size=sz,asset=a,**kw)
 if mat:o['material']=mat
 return o
def bouquet(id,q,y,w=.32,h=.62,variant='red',yaw1=None):
 mm={'*':'kraft','rose_bouquet_Material':'ribbon_red','rose_bouquet_Material.002':'kraft','rose_bouquet_Material.001':variant+'_petal_2','rose_bouquet_Material_9.003':variant+'_petal_0','rose_bouquet_petal-red03':variant+'_petal_1','rose_bouquet_petal-red04':variant+'_petal_3','rose_bouquet_red-petal01':variant+'_petal_4'}
 for k,v in list(mm.items()):mm[k.replace('.','_')]=v
 o=asset(id,q,y,[w,h,w*.92],rose,material_map=mm,collider=False,group='Bouquets',yaw=yaw if yaw1 is None else yaw1)
 return o
def plant(id,q,y,h=.48,kind='potted_plant_04'):
 mm={'*':kind}
 if kind=='potted_plant_01':mm={'*':'plant01_leaves','potted_plant_01_potted_plant_01_pot':'plant01_pot','potted_plant_01_potted_plant_01_leaves':'plant01_leaves'}
 return asset(id,q,y,[h*.55,h,h*.55],ph[kind],material_map=mm,collider=False,group='Plants')
# Front cabinets follow the field photo: two display runs separated by a real aisle.
for side,cx in [('source_left',-13.40),('source_right',-16.99)]:
 for j in range(3):
  x=cx+(j-1)*.65
  asset(side+'_downloaded_display_cabinet_'+str(j),[x,-30.66],.035,[.64,.62,.60],shelf,white,group='DisplayCabinets')
  # Actual photo has closed slab front doors. Adapt downloaded cabinet carcass with individual panels/seams.
  box(side+'_front_door_'+str(j),[x,-30.346],.36,[.622,.58,.018],white,group='DisplayCabinets')
  box(side+'_worktop_'+str(j),[x,-30.65],.68,[.652,.035,.62],white,group='DisplayCabinets')
  asset(side+'_raised_rear_display_'+str(j),[x,-30.99],.69,[.64,.35,.30],shelf,white,group='DisplayCabinets')
  # small caster wheels support photo-observed movable display modules
  for dx in [-.25,.25]:obj('cylinder',side+f'_caster_{j}_{dx}',[x+dx,-30.48],.046,size=[.06,.028,.06],material=black,collider=False,euler=[90,0,0],group='DisplayCabinets')
# Back flower cooler, retaining actual downloaded hardware, double glass doors and shelves.
for j,x in enumerate([-14.08,-13.22]):
 asset('downloaded_glass_flower_refrigerator_leaf_'+str(j),[x,-33.93],.02,[.83,2.05,.70],fridge,material_map={'*':'brushed_stainless','FridgeCase':'cabinet_white','FridgeDoor':'cabinet_white','FridgeDoorInner':'brushed_stainless','FridgeGlass':'glass','FridgeInterior':'brushed_stainless'},group='FloristEquipment')
# Rear work/checkout station and source-visible wrapping items.
asset('downloaded_wrapping_workstation',[-16.52,-33.87],.03,[1.55,.90,.70],shelf,white,group='Workstation');box('wrapping_worktop',[-16.52,-33.87],.95,[1.60,.045,.75],white,group='Workstation')
asset('downloaded_POS_terminal',[-16.05,-33.68],.98,[.30,.35,.28],pos,'pos_body',group='Workstation',yaw=180+yaw,collider=False)
# Right open plant display unit from a downloaded real shelf with white photo-adapted finish.
asset('downloaded_right_rear_plant_shelf',[-17.03,-32.66],.06,[1.70,1.65,.45],modern,white,group='PlantDisplay')
for y in [.55,1.10,1.62]:
 for j in range(5):plant('shelf_pot_'+str(y)+'_'+str(j),[-17.70+j*.32,-32.58],y,h=.24+(j%2)*.12)
for j in range(4):plant('tall_foliage_pot_'+str(j),[-17.78+j*.40,-32.70],1.72,h=.50+(j%2)*.10,kind='potted_plant_01')
# Merchandise: larger bouquets on rear tiers; small gift bunches at lower tiers.
for side,cx in [('left',-13.40),('right',-16.99)]:
 for j in range(5):bouquet(side+'_large_wrapped_bouquet_'+str(j),[cx+(j-2)*.34,-30.96],1.04,w=.32,h=.62,variant=['yellow','red','cream','pink','yellow'][j],yaw1=[0,12,-8,5,-10][j])
 for j in range(8):bouquet(side+'_small_wrapped_bunch_'+str(j),[cx+(j-3.5)*.215,-30.48],.70,w=.18,h=.34,variant=['cream','pink','yellow','red'][j%4],yaw1=j*17)
# Vase and basket products are actual downloaded models, arranged amongst bouquets.
for j in range(6):
 x=-17.7+j*.27
 asset('downloaded_white_vase_'+str(j),[x,-31.24],1.06,[.13,.28,.13],ph['ceramic_vase_01'],'ceramic_vase_01',collider=False,group='Giftware')
for j,x in enumerate([-13.95,-12.85,-17.6]):
 asset('downloaded_wicker_basket_'+str(j),[x,-31.58],.06,[.48,.43,.38],ph['wicker_basket_02'],'wicker_basket_02',collider=False,group='Giftware')
 bouquet('basket_bouquet_'+str(j),[x,-31.58],.40,w=.40,h=.54,variant=['pink','cream','yellow'][j])
# Cooler stock, actual flower assets behind the source glass doors.
for level in [0,.65,1.25]:
 for j in range(3):bouquet('cooler_flowers_'+str(level)+'_'+str(j),[-14.17+j*.50,-33.65],.12+level,w=.27,h=.52,variant=['pink','cream','red'][j])
# Photo-visible transparent gift cases: physical open backbox with edges, no image cards.
for j,x in enumerate([-17.63,-17.13,-16.63]):
 box('gift_case_base_'+str(j),[x,-30.94],1.08,[.38,.035,.29],white,collider=False,group='GiftCases')
 for dx in [-.19,.19]:box('gift_case_side_'+str(j)+str(dx),[x+dx,-30.94],1.32,[.008,.48,.29],glass,collider=False,group='GiftCases')
 box('gift_case_front_'+str(j),[x,-30.79],1.32,[.38,.48,.008],glass,collider=False,group='GiftCases')
 box('gift_case_top_'+str(j),[x,-30.94],1.56,[.38,.008,.29],glass,collider=False,group='GiftCases')
 bouquet('case_flower_'+str(j),[x,-30.94],1.10,w=.24,h=.40,variant=['pink','cream','yellow'][j])
# Work supplies: rolled paper with real tube profiles, spools, scissors physical geometry.
for j in range(5):
 obj('cylinder','paper_roll_'+str(j),[-17.10+j*.12,-33.91],1.035,size=[.075,.05,.075],material=['kraft','pink_paper','cream_paper'][j%3],collider=False,group='Workstation')
 obj('cylinder','ribbon_spool_'+str(j),[-16.9+j*.13,-33.62],.996,size=[.065,.015,.065],material='ribbon_red' if j%2 else white,collider=False,group='Workstation')
# Lighting: visible physical housing/rails, all local to reused room shell.
for z in [-30.95,-32.45]:
 box('white_track_'+str(z),[-15.2,z],2.76,[5.36,.035,.045],white,group='Lighting',collider=False)
 for j in range(7):
  x=-17.7+j*.83
  obj('cylinder','spot_body_'+str(z)+'_'+str(j),[x,z],2.63,size=[.09,.09,.09],material=white,collider=False,group='Lighting')
  obj('cylinder','spot_lens_'+str(z)+'_'+str(j),[x,z],2.535,size=[.073,.007,.073],material=warm,collider=False,group='Lighting')
  obj('light','spot_light_'+str(z)+'_'+str(j),[x,z],2.46,range=1.8,intensity=.18,color=[1,.94,.81,1],group='Lighting')
# Actual-source physical sign. Native existing opening/header untouched.
box('sign_physical_backing_panel',[-15.20,-30.23],3.20,[2.04,.74,.065],black,group='SourceTracedSign')
box('source_amber_logo_plaque',[-14.57,-30.178],3.20,[.43,.56,.050],gold,group='SourceTracedSign')
# Thin independent metal edges on actual plaque; pictogram saturated in source, not falsely invented.
for dx in [-.225,.225]:box('logo_vertical_rim_'+str(dx),[-14.57+dx,-30.145],3.20,[.012,.58,.012],steel,collider=False,group='SourceTracedSign')
for dy in [-.29,.29]:box('logo_horizontal_rim_'+str(dy),[-14.57,-30.145],3.20+dy,[.45,.012,.012],steel,collider=False,group='SourceTracedSign')
mesh=json.load(open(R/'SIGN-PHYSICAL-PHOTO-CONTOUR-MESH.json'));obj('mesh','actual_photo_traced_kkotdeul_closed_beveled_letters',[-15.20,-30.164],3.20,vertices=mesh['vertices'],triangles=mesh['triangles'],material=glow,collider=False,group='SourceTracedSign',source_ids=['PHOTO2024_SIGN_ACTUAL_CONTOUR'])
for dx in [-.7,.7]:box('sign_mount_standoff_'+str(dx),[-15.20+dx,-30.275],3.20,[.03,.03,.06],steel,collider=False,group='SourceTracedSign')
# Limit absent data, rather than hide it behind a completeness percentage.
imp(S/'ATTRIBUTION.txt','SOURCE-LICENSE-ATTRIBUTION.txt')
packet={'store_key':'flowers','origin_q':[-15.2,-32.25],'floor_world_y':7.0,'source_packet':'SOURCE-EVIDENCE-R25.json','placement_basis':'MODEL-PLACEMENT-BASIS-R25.json','source_imports':imports,'materials':materials,'mesh_library':lib,'objects':objects,'no_text_components':True,'native_shell_and_entrance_reused':True,'complete_digital_twin_claim':False,'limits':['Same branch source2023/2024; exact2026 merchandise, hidden layout and measured sizes not established.','Photo brand plaque pictogram is saturated and insufficient for exact inner emblem. Actual plaque and readable source letter contours built; no font or imageplane substitute.','Downloaded floral merchandise shape adapted to source-visible color/packaging pattern; not individual botanical inventory identity.']}
P=R/'FLOWERS-PHYSICAL-FREE-ASSET-PACKET-R25.json';P.write_text(json.dumps(packet,ensure_ascii=False));sha=hashlib.sha256(P.read_bytes()).hexdigest()
cs=(R.parent/'StoreBuilderR25-v3-FREE-ASSETS.template.cs').read_text()
for k,v in {'__STORE_KEY__':'flowers','__WORKER_THREAD__':'classic:/root/r25_flowers','__EXACT_SCOPE__':'C180_R25_FLOWERS_APPLY_001','__PACKET_PATH__':str(P.relative_to(ROOT)),'__PACKET_SHA256__':sha}.items():cs=cs.replace(k,v)
# Own importer/material support for physically double-sided plant leaves and opacity cutouts.
cs=cs.replace('AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);Materials[prop.Name]=m;', '''if((bool?)a["double_sided"]==true)m.SetFloat("_Cull",0);
   if((bool?)a["alpha_clip"]==true){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.45f);m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;}
   AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);Materials[prop.Name]=m;''')
(R/'ApplyStoreR25.cs').write_text(cs)
(R/'FROZEN-PACKET-R25.json').write_text(json.dumps({'packet':str(P.relative_to(ROOT)),'sha256':sha,'objects':len(objects),'imports':len(imports),'asset_instances':sum(o['kind']=='asset' for o in objects),'text_count':0,'native_mutation_count':0},indent=2));print('FROZEN',sha,'objects',len(objects),'imports',len(imports))
