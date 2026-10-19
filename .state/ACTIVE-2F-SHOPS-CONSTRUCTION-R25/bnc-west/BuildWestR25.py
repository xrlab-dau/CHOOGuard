from pathlib import Path
import json, math, hashlib, shutil, sys
import cv2, numpy as np

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'
OWN=BASE/'bnc-west'
ASSET='Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-west'
sys.path.insert(0,str(BASE))
from photo_outline_to_solid import closed_solid_from_mask

def save(path,data): path.write_text(json.dumps(data,ensure_ascii=False,indent=2))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

# Current actual face lines, selected from full R19 native column sections.
cad=json.loads((OWN/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text())
cols={}
for o in cad['objects']:
    if 'C177_UPPER_' in o['path']:
        key=o['path'].split('C177_UPPER_')[1][:3]
        segs=o['sections']['7.15000']
        cols[key]={'object_id':o['object_id'],'path':o['path'],'segments':segs}
e=np.array([8.3999,-.038]); e=e/np.linalg.norm(e)
n=np.array([-e[1],e[0]])
def face(key,axis,which):
    ss=[np.array(s) for s in cols[key]['segments'] if math.dist(s[0],s[1])>.70]
    ss=[s for s in ss if abs(np.dot((s[1]-s[0])/np.linalg.norm(s[1]-s[0]),axis))<.02]
    return (max if which=='max' else min)(ss,key=lambda s:np.dot(s.mean(axis=0),axis))
west=face('028',e,'max');east=face('029',e,'min')
front=face('028',n,'min'); rear=face('045',n,'max')
def intersect(a,b):
    m=np.stack([a[1]-a[0],-(b[1]-b[0])],axis=1)
    return a[0]+np.linalg.solve(m,b[0]-a[0])[0]*(a[1]-a[0])
O=intersect(west,front); E=intersect(east,front); B=intersect(west,rear)
W=float(np.dot(E-O,e));D=float(-np.dot(B-O,n));theta=math.degrees(math.atan2(-e[1],e[0]))
def q(x,d):return [round(float(v),6) for v in O+e*x-n*d]
foot=[q(.035,.035),q(W-.035,.035),q(W-.035,D-.035),q(.035,D-.035),q(.035,.035)]
save(OWN/'MODEL-PLACEMENT-BASIS-R25.json',{
 'store_key':'bnc-west','status':'CURRENT_ACTUAL_COLUMN_FACE_DERIVED_MODEL',
 'source_cad':cad['source'],'source_sha256':cad['source_sha256'],
 'anchors':{k:{'object_id':v['object_id'],'path':v['path']} for k,v in cols.items()},
 'face_lines':{'028_east':west.tolist(),'029_west':east.tolist(),'028_south_front':front.tolist(),'045_north_rear':rear.tolist()},
 'origin_q':O.tolist(),'east_unit':e.tolist(),'public_outward_unit':n.tolist(),
 'native_face_width_m':W,'native_face_depth_m':D,'own_footprint_q':foot,
 'working_inset_m':.035,'front_sign_outward_yaw':theta,
 'derivation':'Intersection and extensions of actual current native column faces, with own inset. Colored mark only selects neighborhood; old source candidate dimensions not used.',
 'branch_binding':'May2026 field05 + independently fetched Oct9/15 exact matching 부산명품빵집 west frontage. Primary GRIDA 2022 completed photos match current frontage and interior.',
 'plan_use':'2021-10-25 original proposal semantic adjacency only; title says1층 while primary contractor identifies actual2F remodel2022. Current photos supersede old proposal typography; no source drawing scaled or warped into CAD.',
 'fixed_references':'All native columns and current floor, west escalators/guards, adjacent Hwang029-eastface bay, all saved stores/1F/WCs/user deletions untouched.',
 'public_access':'Front west own customer opening x=.30..1.22, existing long-side circulation outside own footprint remains clear. No original native door/window changes.',
 'metric_qualification':'PHOTO_REFERENCED_MODEL_NOT_SURVEYED_EQUALITY',
 'no_scene_validation':True})

imports=[]
def imp(path,dest):
    path=Path(path);imports.append({'source':str(path),'destination':dest,'sha256':sha(path)});return ASSET+'/'+dest
def copy_model_import(src,dest):
    dst=OWN/'sources/models'/dest;dst.parent.mkdir(parents=True,exist_ok=True)
    if not dst.exists():shutil.copy2(src,dst)
    return imp(dst,'FreeModels/'+dest)

materials={}
def mat(name,color,**kw):materials[name]={'color':color,'smoothness':.4,**kw}
for name,col in {
 'navy':[.024,.031,.055,1],'black':[.015,.018,.022,1],
 'cream':[.87,.85,.78,1],'white':[.95,.94,.88,1],
 'steel':[.59,.62,.63,1],'chrome':[.72,.74,.75,1],
 'teal':[.19,.39,.40,1],'gold':[.65,.43,.13,1],
 'wood':[.70,.49,.28,1],'wood_dark':[.27,.15,.07,1],
 'pink':[.94,.70,.74,1],'paper':[.94,.89,.77,1],
 'orange':[.92,.35,.08,1],'green':[.19,.41,.26,1],
 'macaron_pink':[.93,.51,.62,1],'macaron_green':[.56,.73,.36,1],
 'macaron_yellow':[.96,.81,.31,1],'chocolate':[.17,.065,.028,1],
 'brass_dark':[.15,.12,.085,1], 'red':[.68,.04,.055,1]}.items():mat(name,col)
materials['steel'].update(metallic=.88,smoothness=.58)
materials['chrome'].update(metallic=.97,smoothness=.8)
materials['gold'].update(metallic=.8,smoothness=.65)
mat('glass',[.78,.88,.90,.10],transparent=True,smoothness=.97)
mat('reeded',[.62,.77,.76,.36],transparent=True,smoothness=.78)
mat('wrap',[.95,.97,.94,.11],transparent=True,smoothness=.95)
mat('sign_white',[.94,.99,1,1],emission=[.85,.94,1,1])
mat('warm_lit',[1,.87,.51,1],emission=[1.3,.95,.40,1])
mat('case_light',[1,.95,.78,1],emission=[.8,.69,.43,1])
for key in ['wood_table_001','croissant','hamburger_buns','carrot_cake','strawberry_chocolate_cake']:
    folder=OWN/'sources/models'/key
    tex=imp(folder/'diff.jpg',f'PBR/{key}/diff.jpg')
    normal=next(x for x in folder.glob('normal.*'))
    norm=imp(normal,f'PBR/{key}/{normal.name}')
    gloss=imp(folder/'metallic_smoothness.png',f'PBR/{key}/metallic_smoothness.png')
    if key=='wood_table_001':
        for k in ['wood','wood_dark']:
            materials[k].update(texture_asset=tex,normal_asset=norm,metallic_gloss_asset=gloss)
    else:mat('scan_'+key,[1,1,1,1],texture_asset=tex,normal_asset=norm,metallic_gloss_asset=gloss,smoothness=.24)

models={key:imp(OWN/'sources/models'/key/(key+'.fbx'),f'FreeModels/{key}/{key}.fbx') for key in ['croissant','hamburger_buns','carrot_cake','strawberry_chocolate_cake']}
models['display']=imp(OWN/'sources/models/display/50e0f0f7e37b4a80a6b809a8831835c2.fbx','FreeModels/display/BakeryDisplay.fbx')
models['counter']=imp(OWN/'sources/models/counter/Checkout-Counter.fbx','FreeModels/counter/Checkout-Counter.fbx')
models['pos']=imp(OWN/'sources/models/pos/POS-Terminal.fbx','FreeModels/pos/POS-Terminal.fbx')
pos=OWN/'sources/models/pos'
mat('pos',[1,1,1,1],texture_asset=imp(pos/'textures/Image_0.png','PBR/pos/base.png'),normal_asset=imp(pos/'textures/Image_2.png','PBR/pos/normal.png'),metallic_gloss_asset=imp(pos/'metallic_smoothness.png','PBR/pos/metallic_smoothness.png'))
for key in ['loaf','loaf-baguette','muffin','plate-rectangle']:
    models[key]=copy_model_import(BASE/'free-assets/kenney-food-kit/Models/FBX format'/(key+'.fbx'),'kenney/'+key+'.fbx')
kenneytex=copy_model_import(BASE/'free-assets/kenney-food-kit/Models/FBX format/Textures/colormap.png','kenney/colormap.png')
if kenneytex:mat('kenney',[1,1,1,1],texture_asset=kenneytex)
else:mat('kenney',[.76,.47,.21,1])
credits={
 'BakeryDisplay':{'author':'mmcmindes','url':'https://sketchfab.com/3d-models/bakery-display-case-50e0f0f7e37b4a80a6b809a8831835c2','license':'CC-BY-4.0','changes':'Own URP material mapping, metric fitted source instances; original retained'},
 'Counter':{'author':'siyarasm','url':'https://sketchfab.com/3d-models/industrial-kitchen-bun-counter-50a9dc05104048ffba83eca72a003873','license':'CC-BY-4.0','changes':'Previously source-derived lower counter only; own dimensions/materials'},
 'POS':{'author':'Vladyslav.Yarovyi','url':'https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb','license':'CC-BY-4.0','changes':'Own PBR and fit'},
 'PolyHaven':{'author':'Poly Haven contributors','urls':['https://polyhaven.com/a/'+k for k in ['croissant','hamburger_buns','carrot_cake','strawberry_chocolate_cake','wood_table_001']],'license':'CC0-1.0','changes':'Own source copies, roughness to smoothness maps; not exact product SKU'},
 'Kenney':{'author':'Kenney','url':'https://kenney.nl/assets/food-kit','license':'CC0-1.0'}}
save(OWN/'sources/ATTRIBUTION-R25.json',credits)
imp(OWN/'sources/ATTRIBUTION-R25.json','Credits/ATTRIBUTION-R25.json')
for folder,names in [('display',['metadata.json','ACQUISITION.json']),('pos',['ACQUISITION.json']),('counter',['SOURCE-ATTRIBUTION.txt'])]:
    for name in names:
        f=OWN/'sources/models'/folder/name
        if f.exists():imp(f,f'Credits/{folder}-{name}')

# Source photo -> rectified ink outlines -> closed, thick meshes. No fonts.
signfolder=OWN/'sign-model-authoring';signfolder.mkdir(exist_ok=True)
image=cv2.imread(str(OWN/'sources/woongyultarot-01-full.jpg'))
quad=np.float32([[95,303],[633,286],[634,342],[96,358]])
dst=np.float32([[0,0],[1079,0],[1079,111],[0,111]])
rect=cv2.warpPerspective(image,cv2.getPerspectiveTransform(quad,dst),(1080,112))
mask=cv2.inRange(rect,np.array([170,170,170]),np.array([255,255,255]))
mask[:7]=0;mask[-5:]=0;mask[:,:5]=0;mask[:,-5:]=0
cv2.imwrite(str(signfolder/'WEST-FRONT-PHOTO-RECTIFIED.png'),rect)
cv2.imwrite(str(signfolder/'WEST-FRONT-INK-MASK.png'),mask)
frontsolid=closed_solid_from_mask(mask,4.22,.438,.038,minimum_area=28,epsilon=.5)
save(signfolder/'WEST-FRONT-SOURCE-SOLID.json',frontsolid)

bladephoto=cv2.imread(str(OWN/'sources/woongyultarot-18-full.jpg'))
crop=bladephoto[116:287,519:684]
crop=cv2.resize(crop,(500,500))
blademask=((crop[:,:,1]>155)&(crop[:,:,2]>165)&(crop[:,:,2]>crop[:,:,0]*1.13)).astype('uint8')*255
yy,xx=np.indices(blademask.shape); rr=np.sqrt((xx-250)**2+(yy-250)**2)
blademask[rr>249]=0
cv2.imwrite(str(signfolder/'WEST-BLADE-LOGO-MASK.png'),blademask)
bladesolid=closed_solid_from_mask(blademask,.64,.64,.024,minimum_area=35,epsilon=.45)
if bladesolid['source_polygons']==0:raise ValueError('Actual blade source ink required')
save(signfolder/'WEST-BLADE-SOURCE-SOLID.json',bladesolid)

objects=[];library={}
def obj(kind,id,x,d,y,group='Booth',material=None,yaw=0,**kw):
    a={'kind':kind,'id':id,'group':group,'q':q(x,d),'y':y,'yaw':yaw+theta,
       'source_ids':['WEST_CURRENT_OCT2026','GRIDA_2022_ACTUAL'],
       'qualification':'CURRENT_COLUMN_ANCHORED_SOURCE_REFERENCED_MODEL_NOT_SURVEY',**kw}
    if material:a['material']=material
    objects.append(a);return a
def box(id,x,d,y,size,mat,group='Booth',yaw=0,collider=True):return obj('box',id,x,d,y,group,mat,yaw,size=size,collider=collider)
def cyl(id,x,d,y,diam,height,mat,group='Details',euler=None):
    a=obj('cylinder',id,x,d,y,group,mat,size=[diam,height/2,diam],collider=False)
    if euler:a['euler']=euler
    return a
def asset(id,key,x,d,y,size,group='Downloaded_Source_Models',yaw=0,map=None,collider=True):
    return obj('asset',id,x,d,y,group,yaw=yaw,asset=models[key],fit_size=size,material_map=map or {'*':'scan_'+key},collider=collider)
def frame(id,x,d,y,w,h,thick,mat,group='Framed_Panels',yaw=0):
    # XY frame on +Z face, transformed around its pivot later for side faces.
    for j,(dx,dy,s) in enumerate([(-w/2,0,[thick,h,.025]),(w/2,0,[thick,h,.025]),(0,-h/2,[w,thick,.025]),(0,h/2,[w,thick,.025])]):
        rad=math.radians(yaw);xx=x+dx*math.cos(rad);dd=d+dx*math.sin(rad)
        box(id+str(j),xx,dd,y+dy,s,mat,group,yaw,False)
def printpanel(id,img,x,d,y,w,h,yaw=0):
    dest=imp(img,'Prints/'+img.name);mat(id+'_print',[1,1,1,1],texture_asset=dest,emission=[.10,.10,.08,1])
    box(id+'_housing',x,d+.018,y,[w+.055,h+.055,.055],'black','Actual_Source_Print_Panels',yaw)
    obj('mesh',id+'_source_print',x,d-.016,y,'Actual_Source_Print_Panels',id+'_print',yaw,
        vertices=[[w/2,h/2,0],[-w/2,h/2,0],[-w/2,-h/2,0],[w/2,-h/2,0]],triangles=[0,1,2,0,2,3],uv=[[0,1],[1,1],[1,0],[0,0]],collider=False)

# Navy source façade remains inside column-face clear bay, with full open entry.
box('Front_Navy_Source_Fascia',W/2,.17,2.61,[W-.13,.64,.24],'navy')
for x in [.19,W-.19]:box('Front_Jamb_'+str(round(x,2)),x,.17,1.15,[.25,2.30,.24],'navy')
box('Source_Upper_Cream_Transom',W/2,.23,3.15,[W-.13,.44,.18],'cream')
box('Front_Grille_Toptrack',W/2,.25,2.265,[W-.65,.055,.055],'black','Entry_Hardware',collider=False)
for i in range(15):
    box('Compressed_Folding_Grille_'+str(i),W-.51-i*.019,.31,1.15,[.011,2.18,.15],'black','Entry_Hardware',yaw=(18 if i%2 else -18),collider=False)
obj('mesh','Actual_West_Front_Luminous_Source_Letter_Solids',W/2,.025,2.62,'Physical_Source_Signs','sign_white',
    vertices=frontsolid['vertices'],triangles=frontsolid['triangles'],collider=False)
for x in [1.3,3.1,4.9,6.7]:
    cyl('Front_Recessed_Source_Downlight_'+str(x),x,.32,2.275,.07,.015,'case_light','Own_Lights')

# Stainless checkout source furniture and actual downloaded cake showcases.
asset('Downloaded_Checkout_Counter','counter',5.65,.65,0,[2.45,1.0,.76],map={'*':'steel'})
box('Checkout_Cream_Solid_Worktop',5.65,.65,1.035,[2.49,.055,.80],'white','Sales_Counter')
asset('Downloaded_POS','pos',5.70,.67,1.065,[.40,.42,.35],yaw=180,map={'Material_2100454184':'pos','Material_2100454185':'black','*':'black'})
box('Cardreader_Base',5.09,.46,1.08,[.17,.035,.20],'black','Checkout_Equipment',collider=False)
box('Cardreader_Screen',5.09,.46,1.125,[.14,.055,.14],'black','Checkout_Equipment',collider=False)
for i,x in enumerate([2.02,3.48]):
    asset('Downloaded_Front_Cake_Case_'+str(i),'display',x,.66,.06,[1.40,1.11,.79],map={'R120G120B120':'glass','R127G127B127':'steel','Woods__Plastics.Finish_Carpentry.Wood.Pine.png':'steel','*':'steel'})
    box('Cake_Case_Lower_Steel_Apron_'+str(i),x,.249,.255,[1.42,.38,.035],'steel','Sales_Counter')
    for y in [.40,.77,1.12]:box('Cake_Case_LED_'+str(i)+'_'+str(y),x,.32,y,[1.33,.016,.018],'case_light','Case_Lighting',collider=False)

# Observed cream / teal reeded glazed factory divider. Doors remain open.
partitionX=4.43
box('Teal_Partition_Cream_Header',partitionX,3.70,2.66,[.12, .48,4.87],'cream')
for i,d in enumerate([1.85,2.75,3.65,4.55,5.45]):
    if i==0:continue  # Actual staff circulation opening at front of divider.
    box('Teal_Reeded_Panel_'+str(i),partitionX,d,1.62,[.018,1.40,.84],'reeded','Staff_Glazed_Partition',collider=False)
    for dd in [d-.44,d+.44]:box('Teal_Divider_Upright_'+str(i)+'_'+str(dd),partitionX,dd,1.62,[.045,1.46,.04],'teal','Staff_Glazed_Partition')
    for yy in [.90,1.62,2.34]:box('Teal_Divider_Rail_'+str(i)+'_'+str(yy),partitionX,d,yy,[.045,.035,.90],'teal','Staff_Glazed_Partition',collider=False)
    for j in range(33):box('Reeded_Glass_Rib_'+str(i)+'_'+str(j),partitionX-.013,d-.40+j*.025,1.62,[.008,1.36,.004],'glass','Reeded_Glass_Detail',collider=False)
    box('Teal_Partition_Lower_Panel_'+str(i),partitionX,d,.43,[.08,.82,.90],'cream','Staff_Glazed_Partition')
box('Menu_Column_Source_Cream_Pier',partitionX,1.50,1.33,[.52,.25+2.4,.30],'cream')
photo=cv2.imread(str(OWN/'sources/woongyultarot-18-full.jpg'))
src=np.float32([[148,120],[400,138],[411,564],[145,587]])
menu=cv2.warpPerspective(photo,cv2.getPerspectiveTransform(src,np.float32([[0,0],[399,0],[399,799],[0,799]])),(400,800))
menufile=OWN/'sources/WEST-ACTUAL-MENU-PRINT.png';cv2.imwrite(str(menufile),menu)
printpanel('Actual_Observed_Vertical_Menu',menufile,partitionX,1.323,1.65,.48,1.15)

# Round blade: physical body, source B/C/crossed-pin/ring solid and brackets.
def diskmesh(radius,depth,N=96):
    mask=np.zeros((500,500),np.uint8);cv2.circle(mask,(250,250),245,255,-1)
    return closed_solid_from_mask(mask,radius*2,radius*2,depth,minimum_area=20,epsilon=.8)
disk=diskmesh(.43,.075)
obj('mesh','Round_Blade_Brass_Housing',4.08,1.67,2.11,'Physical_Source_Signs','brass_dark',yaw=-90,vertices=disk['vertices'],triangles=disk['triangles'],collider=False)
obj('mesh','Round_Blade_Closed_Source_Logo',4.056,1.67,2.11,'Physical_Source_Signs','warm_lit',yaw=-90,vertices=bladesolid['vertices'],triangles=bladesolid['triangles'],collider=False)
for yy in [1.78,2.44]:box('Round_Blade_Standoff_'+str(yy),4.26,1.67,yy,[.38,.035,.035],'black','Physical_Source_Signs',collider=False)

# Primary source U retail furniture. Original free cabinet derivatives provide
# actual furniture meshes; fitted oak panel/slat detail follows current photos.
asset('Downloaded_West_Long_Display_Base','counter',.65,3.74,0,[4.40,.88,.83],yaw=90,map={'*':'wood'})
asset('Downloaded_Rear_Display_Base','counter',2.36,5.91,0,[3.50,.88,.72],map={'*':'wood'})
for j,d in enumerate([2.05,3.15,4.25,5.35]):
    box('West_Display_Oak_Panel_'+str(j),1.083,d,.46,[.024,.67,1.02],'wood','Oak_Panel_Detail')
    # Source panel framing on aisle-facing +X side.
    for dd in [d-.47,d+.47]:box('West_Panel_Rail_'+str(j)+'_'+str(dd),1.108,dd,.46,[.035,.64,.027],'wood_dark','Oak_Panel_Detail',collider=False)
    for yy in [.15,.77]:box('West_Panel_Vert_'+str(j)+'_'+str(yy),1.108,d,yy,[.035,.024,.97],'wood_dark','Oak_Panel_Detail',collider=False)
for j in range(60):box('West_Wood_Slatted_Display_'+str(j),.65,1.60+j*.073, .925,[.83,.035,.038],'wood_dark','Slatted_Display_Surfaces',collider=False)
for j,x in enumerate([1.12,2.27,3.42]):
    box('Rear_Oak_Panel_'+str(j),x,5.52,.46,[1.03,.67,.023],'wood','Oak_Panel_Detail')
    frame('Rear_Panel_Frame_'+str(j),x,5.503,.46,.96,.62,.022,'wood_dark','Oak_Panel_Detail')
for j in range(46):box('Rear_Wood_Slats_'+str(j),.65+j*.074,5.91,.925,[.038,.035,.70],'wood_dark','Slatted_Display_Surfaces',collider=False)
for y in [1.45,1.94]:
    box('West_Upper_Open_Bread_Shelf_'+str(y),.65,3.74,y,[.78,.038,4.40],'wood','Open_Source_Shelves',collider=False)
    box('Rear_Upper_Open_Bread_Shelf_'+str(y),2.36,5.92,y,[3.50,.038,.64],'wood','Open_Source_Shelves',collider=False)
for d in [1.61,3.68,5.91]:box('West_Shelf_Navy_Upright_'+str(d),.26,d,1.48,[.034,1.19,.034],'navy','Open_Source_Shelves',collider=False)
for x in [.65,2.34,4.08]:box('Rear_Shelf_Navy_Upright_'+str(x),x,6.19,1.48,[.034,1.19,.034],'navy','Open_Source_Shelves',collider=False)
for i,d in enumerate([2.70,3.91,5.12]):
    asset('Downloaded_Inner_Cake_Case_'+str(i),'display',3.92,d,.02,[1.16,1.22,.75],yaw=-90,map={'R120G120B120':'glass','R127G127B127':'steel','Woods__Plastics.Finish_Carpentry.Wood.Pine.png':'steel','*':'steel'})
    box('Inner_Case_Linear_LED_'+str(i),3.55,d,1.17,[.018,.017,1.10],'case_light','Case_Lighting',collider=False)

# Actual side and rear glass display windows: original current columns remain
# exposed; own mullions are slim and inside the structural clear bay.
for i,d in enumerate([2.15,3.45,4.75,5.85]):
    depth=1.23 if i<3 else .90
    box('West_Hall_Glass_'+str(i),.07,d,1.42,[.014,2.18,depth],'glass','Outer_Source_Glazing',collider=False)
    box('West_Window_Mullion_'+str(i),.07,d-depth/2,1.42,[.048,2.27,.045],'navy','Outer_Source_Glazing')
box('West_Window_Lower_Rail',.07,3.80,.31,[.05,.045,4.98],'navy','Outer_Source_Glazing')
box('West_Window_Upper_Rail',.07,3.80,2.54,[.05,.045,4.98],'navy','Outer_Source_Glazing')
for i,x in enumerate([.75,2.18,3.61]):
    box('Rear_Glass_'+str(i),x,D-.09,1.75,[1.36,1.27,.014],'glass','Outer_Source_Glazing',collider=False)
    box('Rear_Glass_Mullion_'+str(i),x-.70,D-.09,1.75,[.045,1.35,.045],'navy','Outer_Source_Glazing')
box('Rear_Upper_Cream_Frieze',2.17,D-.10,2.66,[4.18,.50,.08],'cream')
box('East_Own_Cream_Side_Panel',W-.10,3.78,1.46,[.10,2.90,4.98],'cream')
box('Prep_Rear_Cream_Panel',6.03,D-.10,1.46,[2.75,2.90,.10],'cream')

# Photo-visible trolley behind checkout. Build the observed open steel rack
# with physical tray rims and castors, never a filled named equipment box.
rx,rd=6.10,1.88
for xx in [rx-.30,rx+.30]:
    for dd in [rd-.32,rd+.32]:
        box('Rack_Upright_'+str(xx)+'_'+str(dd),xx,dd,1.03,[.026,1.70,.026],'steel','Visible_Bakery_Rack')
        cyl('Rack_Castor_'+str(xx)+'_'+str(dd),xx,dd,.10,.085,.032,'black','Visible_Bakery_Rack',[90,theta,0])
for i in range(12):
    y=.27+i*.126
    box('Rack_Tray_'+str(i),rx,rd,y,[.57,.012,.60],'steel','Visible_Bakery_Rack',collider=False)
    for dx in [-.284,.284]:box('Rack_Tray_Rim_'+str(i)+'_'+str(dx),rx+dx,rd,y+.012,[.012,.026,.61],'steel','Visible_Bakery_Rack',collider=False)
    box('Rack_Tray_Rear_Rim_'+str(i),rx,rd+.30,y+.012,[.58,.026,.012],'steel','Visible_Bakery_Rack',collider=False)
    for dx in [-.302,.302]:box('Rack_Runner_'+str(i)+'_'+str(dx),rx+dx,rd,y-.02,[.025,.025,.64],'steel','Visible_Bakery_Rack',collider=False)

# Source coffer ceiling, gold medallions, warm bulbs, track spot housings.
box('Sales_Ceiling_Cream_Main_Plate',2.27,3.62,2.99,[4.30,.10,5.31],'cream','Own_Source_Ceiling')
for x in [1.0,2.27,3.54]:
    for d in [2.0,3.1,4.2,5.3]:
        box('Coffer_Recess_'+str(x)+'_'+str(d),x,d,2.925,[1.17,.025,1.0],'white','Own_Source_Ceiling',collider=False)
        # Horizontal underside coffer trim.
        for dx in [-.57,.57]:box('Coffer_Trim_X_'+str(x)+'_'+str(d)+'_'+str(dx),x+dx,d,2.901,[.036,.032,1.0],'cream','Own_Source_Ceiling',collider=False)
        for dd in [-.48,.48]:box('Coffer_Trim_D_'+str(x)+'_'+str(d)+'_'+str(dd),x,d+dd,2.901,[1.16,.032,.036],'cream','Own_Source_Ceiling',collider=False)
        cyl('Gold_Ceiling_Medallion_'+str(x)+'_'+str(d),x,d,2.89,.25,.018,'gold','Own_Lights')
        obj('sphere','Warm_Bulb_'+str(x)+'_'+str(d),x,d,2.80,'Own_Lights','warm_lit',size=[.050,.090,.050],collider=False)
        obj('light','Own_Warm_Illumination_'+str(x)+'_'+str(d),x,d,2.67,'Own_Lights',range=2.6,intensity=.13,color=[1,.84,.58,1])
for x in [.32,4.17]:
    box('Long_White_Track_'+str(x),x,3.70,2.72,[.04,.045,4.85],'white','Own_Lights',collider=False)
    for d in [1.75,3.15,4.55,5.85]:
        cyl('Source_Track_Spot_Housing_'+str(x)+'_'+str(d),x,d,2.62,.11,.17,'white','Own_Lights')
        cyl('Source_Track_Spot_Lens_'+str(x)+'_'+str(d),x,d,2.526,.088,.01,'case_light','Own_Lights')
for vi,d in enumerate([2.55,4.75]):
    ventmask=np.zeros((300,300),np.uint8)
    for r in [142,108,74,40]:cv2.circle(ventmask,(150,150),r,255,10)
    ventsolid=closed_solid_from_mask(ventmask,.31,.31,.012,minimum_area=20,epsilon=.65)
    obj('mesh','Observed_Circular_Ceiling_Air_Vent_'+str(vi),2.27,d,2.883,'Own_Source_Ceiling','steel',
        vertices=ventsolid['vertices'],triangles=ventsolid['triangles'],euler=[90,theta,0],collider=False)

# Dense category merchandise is editable geometry from real free models.
product_count=0
def food(key,x,d,y,width=.19,depth=.14,height=.105,yaw=0,wrapped=True):
    global product_count
    product_count+=1;id='Downloaded_Scanned_Product_'+str(product_count)
    asset(id,key,x,d,y,[width,height,depth],'Downloaded_Bakery_Products',yaw,{'*':'scan_'+key},False)
    if wrapped:
        box(id+'_Clear_Wrapping',x,d,y+height*.53,[width*1.08,height*1.16,depth*1.08],'wrap','Individual_Clear_Packaging',collider=False)
        cyl(id+'_Orange_Sticker',x,d,y+height*1.10,.029,.0015,'orange','Individual_Clear_Packaging')
def tray(id,x,d,y,width,depth,yaw=0):
    asset(id,'plate-rectangle',x,d,y,[width,.022,depth],'Downloaded_Serving_Trays',yaw,{'*':'paper'},False)
for side in ['west','rear']:
    for level,y in enumerate([.96,1.48,1.97]):
        for group in range(5 if side=='west' else 4):
            if side=='west':x,d=.67,1.95+group*.78
            else:x,d=.98+group*.81,5.91
            tray('Physical_Tray_'+side+str(level)+str(group),x,d,y,.64,.55)
            for a in range(2):
                for b in range(2):
                    xx=x-.15+a*.29;dd=d-.13+b*.25
                    key='croissant' if (group+level+a+b)%3 else 'hamburger_buns'
                    food(key,xx,dd,y+.025,.22,.16,.115,yaw=(group*13)%28)
for x in [2.02,3.48]:
    for level,y in enumerate([.46,.81]):
        for i in range(4):
            key='strawberry_chocolate_cake' if (i+level)%2 else 'carrot_cake'
            food(key,x-.48+i*.32,.62,y,.22,.20,.17,wrapped=False)
for d in [2.70,3.91,5.12]:
    for level,y in enumerate([.44,.82,1.10]):
        for i in range(4):food('strawberry_chocolate_cake' if i%2 else 'carrot_cake',3.91,d-.41+i*.28,y,.20,.20,.15,yaw=90,wrapped=False)
for i in range(11):
    x=1.28+i*.29
    box('Rear_Gift_Carton_'+str(i),x,5.95,2.13,[.25,.30,.24],['paper','orange','green','pink'][i%4],'Physical_Gift_Boxes',collider=False)
    box('Rear_Gift_Box_Lid_'+str(i),x,5.95,2.29,[.258,.025,.248],'paper','Physical_Gift_Boxes',collider=False)
for x in [1.58,1.95,2.32,2.69,3.06,3.43]:
    box('Cake_Gift_Box_'+str(x),x,.66,1.25,[.31,.15,.29],'paper','Physical_Gift_Boxes',collider=False)
    box('Cake_Box_Clear_Window_'+str(x),x,.66,1.33,[.23,.005,.21],'glass','Physical_Gift_Boxes',collider=False)
    food('hamburger_buns',x,.66,1.28,.19,.16,.08,wrapped=False)
for i in range(8):
    d=2.0+i*.45
    asset('Downloaded_Source_Whole_Loaf_'+str(i),'loaf',.64,d,1.97,[.24,.24,.28],'Downloaded_Bakery_Products',map={'*':'kenney'},collider=False)
    box('Loaf_Clear_Bag_'+str(i),.64,d,2.11,[.265,.31,.30],'wrap','Individual_Clear_Packaging',collider=False)
# Individual macarons with two closed shells + cream rather than text/image.
for i in range(15):
    x=1.50+i*.135
    for yy in [.84,.895]:
        a=obj('sphere','Macaron_Shell_'+str(i)+'_'+str(yy),x,.57,yy,'Physical_Macarons',['macaron_pink','macaron_green','macaron_yellow'][i%3],size=[.106,.050,.106],collider=False)
    cyl('Macaron_Cream_'+str(i),x,.57,.868,.092,.018,'white','Physical_Macarons')

packet={'store_key':'bnc-west','origin_q':O.tolist(),'floor_world_y':7.0,
 'source_packet':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/bnc-west/SOURCE-EVIDENCE-R25.json',
 'materials':materials,'source_imports':imports,'objects':objects,
 'free_asset_product_count':product_count,'placement_basis':'MODEL-PLACEMENT-BASIS-R25.json',
 'source_metric_qualification':'Source semantic relationships fitted to current native faces, no sourceplan warp or surveyed equality.',
 'no_hidden_generic_kitchen':True,'no_TMP_or_fonts_or_wholephoto_signs':True}
save(OWN/'STORE-PACKET-R25.json',packet)
scope='C180_R25_BNC_WEST_APPLY_001'
t=(BASE/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text()
for k,v in {'__STORE_KEY__':'bnc-west','__WORKER_THREAD__':'classic:/root/r25_bnc_west','__EXACT_SCOPE__':scope,'__PACKET_PATH__':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/bnc-west/STORE-PACKET-R25.json','__PACKET_SHA256__':sha(OWN/'STORE-PACKET-R25.json')}.items():t=t.replace(k,v)
(OWN/'ApplyStoreR25.cs').write_text(t)
print(json.dumps({'native_width':W,'native_depth':D,'yaw':theta,'objects':len(objects),'asset_instances':sum(x['kind']=='asset' for x in objects),'scanned_products':product_count,'imports':len(imports),'packet_sha':sha(OWN/'STORE-PACKET-R25.json'),'frontsign_polygons':frontsolid['source_polygons'],'blade_polygons':bladesolid['source_polygons']},indent=2))
