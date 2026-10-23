"""Author source-bound 2F BnC scene packet. No Unity or existing Assets writes."""
import collections, hashlib, json, math, pathlib, random
from shapely.geometry import Polygon, Point
from shapely.ops import triangulate

BASE = pathlib.Path(__file__).resolve().parent
PROJECT = BASE.parents[2]
APPROVED = json.loads((BASE/'APPROVED-STORE-WORK-REGION.json').read_text())
REGION = Polygon(APPROVED['geometry_qx_minus_qy']['coordinates'][0])
SHELL = REGION.buffer(-0.085, join_style=2)
A = (62.07715767682876, -26.69856675532803)
B = (66.6917, -24.959912101612517)
W = math.dist(A, B)
U = ((B[0]-A[0])/W, (B[1]-A[1])/W)
IN = (U[1], -U[0])
FRONT_YAW = -math.degrees(math.atan2(U[1], U[0]))
OUT_YAW = math.degrees(math.atan2(-IN[0], -IN[1]))
objects, library = [], {}
random.seed(25)
materials = {
    'navy': {'color':[.033,.042,.066,1], 'smoothness':.38},
    'black': {'color':[.018,.021,.023,1], 'smoothness':.35},
    'cream': {'color':[.86,.83,.75,1], 'smoothness':.34},
    'white': {'color':[.94,.94,.89,1], 'smoothness':.32},
    'timber': {'color':[.50,.33,.16,1], 'smoothness':.42},
    'timber_light': {'color':[.67,.48,.28,1], 'smoothness':.35},
    'timber_dark': {'color':[.20,.12,.065,1], 'smoothness':.4},
    'steel': {'color':[.58,.61,.62,1], 'metallic':.88, 'smoothness':.52},
    'chrome': {'color':[.75,.77,.77,1], 'metallic':.95, 'smoothness':.78},
    'teal': {'color':[.20,.39,.40,1], 'metallic':.12, 'smoothness':.48},
    'glass': {'color':[.71,.84,.86,.14], 'transparent':True, 'smoothness':.96},
    'reeded': {'color':[.65,.80,.79,.52], 'transparent':True, 'smoothness':.79},
    'cellophane': {'color':[.96,.96,.92,.13], 'transparent':True, 'smoothness':.92},
    'gold': {'color':[.65,.44,.13,1], 'metallic':.78, 'smoothness':.62},
    'sign_white': {'color':[.93,.98,1,1], 'emission':[.8,.9,1,1]},
    'warm_lit': {'color':[1,.80,.37,1], 'emission':[1,.62,.19,1]},
    'display_lit': {'color':[1,.96,.79,1], 'emission':[1,.83,.58,1]},
    'paper_pink': {'color':[.91,.68,.69,1], 'smoothness':.2},
    'paper_ivory': {'color':[.94,.90,.77,1], 'smoothness':.24},
    'paper_orange': {'color':[.88,.34,.10,1], 'smoothness':.24},
    'paper_green': {'color':[.27,.50,.33,1], 'smoothness':.24},
    'bread_gold': {'color':[.65,.32,.09,1], 'smoothness':.24},
    'bread_pale': {'color':[.89,.68,.37,1], 'smoothness':.24},
    'chocolate': {'color':[.16,.063,.026,1], 'smoothness':.4},
    'berry': {'color':[.69,.043,.061,1], 'smoothness':.55},
}

def qfront(s, d=0):
    return [A[0]+U[0]*s+IN[0]*d, A[1]+U[1]*s+IN[1]*d]

def add(kind, name, q=None, y=0, size=None, mat='white', group='Details',
        yaw=0, source='GRIDA_ACTUAL', collider=False, **kw):
    o = dict(kind=kind, id=name, group=group, source_ids=[source],
             qualification='PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT', collider=collider)
    if q is not None:o.update(q=list(q), y=y)
    if size is not None:o['size']=list(size)
    if mat is not None:o['material']=mat
    if yaw:o['yaw']=yaw
    o.update(kw)
    objects.append(o)
    return o

def box(n,x,z,y,w,h,d,mat='white',group='Details',yaw=0,**kw):
    return add('box',n,[x,z],y,[w,h,d],mat,group,yaw,**kw)

def edge(n,a,b,bottom,height,thickness,mat,group='Booth',**kw):
    return add('edge',n,mat=mat,group=group,a=list(a),b=list(b),bottom=bottom,
               height=height,thickness=thickness,**kw)

def cyl(n,x,z,y,diam,h,mat='chrome',group='Details',**kw):
    return add('cylinder',n,[x,z],y,[diam,h/2,diam],mat,group,**kw)

def text(n,txt,q,y,width,height,yaw=OUT_YAW,group='Signage'):
    return add('text',n,q,y,mat=None,group=group,yaw=yaw,text=txt,width=width,height=height,
               color=[.94,.98,1,1],source='NAVER_CURRENT_FRONT')

def asset(n,asset_path,x,z,y,w,h,d,mat=None,yaw=0,group='FreeAssetModels',source='GRIDA_ACTUAL'):
    return add('asset',n,[x,z],y,mat=mat,group=group,yaw=yaw,asset=asset_path,
               fit_size=[w,h,d],source=source)

def polygon_slab(name, poly, bottom, height, mat, group):
    vertices=[];tris=[]
    for tri in triangulate(poly):
        if not poly.covers(tri.representative_point()):continue
        coords=list(tri.exterior.coords)[:-1];i=len(vertices)
        vertices.extend([[x-64,bottom,z+32]for x,z in coords])
        vertices.extend([[x-64,bottom+height,z+32]for x,z in coords])
        tris.extend([i,i+2,i+1,i+3,i+4,i+5])
    coords=list(poly.exterior.coords)
    for a,b in zip(coords,coords[1:]):
        i=len(vertices);vertices.extend([[a[0]-64,bottom,a[1]+32],[b[0]-64,bottom,b[1]+32],
                                       [b[0]-64,bottom+height,b[1]+32],[a[0]-64,bottom+height,a[1]+32]])
        tris.extend([i,i+1,i+2,i,i+2,i+3])
    add('mesh',name,[64,-32],0,mat=mat,group=group,vertices=vertices,triangles=tris)

def cabinet(name,x,z,w,d,height=.88,yaw=0,group='Cabinetry'):
    box(name+'_carcass',x,z,height/2,w,height,d,'timber_light',group,yaw,collider=True)
    # Panel frames are solid geometry, separately editable, not text placeholders.
    for off in [-.36, .36] if w>1.2 else [0]:
        cx=x+off*math.cos(math.radians(yaw));cz=z-off*math.sin(math.radians(yaw))
        # front local-Z face, translated in yaw basis
        fx=cx+math.sin(math.radians(yaw))*(d/2+.004)
        fz=cz+math.cos(math.radians(yaw))*(d/2+.004)
        box(name+f'_raised_panel_{off}',fx,fz,.43,min(.62,w-.08),.66,.025,'timber_dark',group,yaw)
        box(name+f'_inset_{off}',fx+.012*math.sin(math.radians(yaw)),fz+.012*math.cos(math.radians(yaw)),
            .43,min(.55,w-.15),.59,.028,'timber_light',group,yaw)
    box(name+'_slate_top',x,z,height+.023,w+.035,.046,d+.025,'cream',group,yaw)

def glasscase(name,q,width,depth=.68,yaw=FRONT_YAW):
    x,z=q
    box(name+'_compressor',x,z,.31,width,.62,depth,'steel','ColdDisplay',yaw,collider=True)
    for level in [.67,.93,1.18]:
        box(name+f'_glass_shelf_{level}',x,z,level,width-.06,.014,depth-.06,'glass','ColdDisplay',yaw)
        box(name+f'_LED_{level}',x,z,level+.025,width-.07,.012,.018,'display_lit','ColdDisplay',yaw)
    # actual transparent display volume: shelves remain visible, no opaque text-proxy block
    box(name+'_front_glazing',x+math.sin(math.radians(yaw))*depth/2,z+math.cos(math.radians(yaw))*depth/2,
        .95,width,.65,.012,'glass','ColdDisplay',yaw)
    for s in [-1,1]:
        ex=x+s*(width/2-.017)*math.cos(math.radians(yaw))
        ez=z-s*(width/2-.017)*math.sin(math.radians(yaw))
        box(name+f'_end_glass_{s}',ex,ez,.95,.012,.65,depth,'glass','ColdDisplay',yaw)
        box(name+f'_corner_post_{s}',ex,ez,.96,.019,.69,.019,'chrome','ColdDisplay',yaw)
    box(name+'_top_glass',x,z,1.28,width,.014,depth,'glass','ColdDisplay',yaw)
    # heat-exchanger grille, no fictional kitchen fuel plumbing
    for i in range(9):
        xx=x+(i-4)*width/12*math.cos(math.radians(yaw));zz=z-(i-4)*width/12*math.sin(math.radians(yaw))
        box(name+f'_vent_{i}',xx+math.sin(math.radians(yaw))*depth*.51,
            zz+math.cos(math.radians(yaw))*depth*.51,.18,.035,.17,.008,'black','ColdDisplay',yaw)

# Native boundary is not altered. Own thin linings sit INSIDE approved edges.
ring=list(SHELL.exterior.coords)
for i,(a,b) in enumerate(zip(ring,ring[1:])):
    # northern public opening is constructed separately
    if max(a[1],b[1])>-27.0 and abs(a[0]-b[0])>3:continue
    mat='cream' if (a[0]+b[0])/2<63 else 'glass'
    edge(f'approved_boundary_lining_{i}',a,b,.03,2.87,.045,mat)
    edge(f'boundary_base_steel_{i}',a,b,.03,.12,.045,'steel')
    edge(f'boundary_upper_cornice_{i}',a,b,2.72,.16,.10,'cream')

# Narrow open facade with compressed left accordion grille; no opaque wall across entrance.
for s,n in [(.12,'left'),(W-.12,'right')]:
    x,z=qfront(s,.12);box('navy_'+n+'_jamb',x,z,1.43,.22,2.86,.19,'navy','Booth',FRONT_YAW)
edge('navy_raised_sign_fascia',qfront(.05,.115),qfront(W-.05,.115),2.68,.59,.19,'navy')
edge('cream_header_above_fascia',qfront(.07,.115),qfront(W-.07,.115),3.27,.18,.18,'cream')
text('front_actual_white_lettering','부산명품빵집    비엔씨',qfront(W*.53,.008),2.985,W*.78,.275)
# Three rounded bread-shaped 3D logo strokes observed on the actual sign.
for j,h in enumerate([.22,.31,.22]):
    x,z=qfront(2.60+j*.10,-.003)
    box('sign_bread_emblem_stem_'+str(j),x,z,2.94,.075,h,.027,'sign_white','Signage',FRONT_YAW)
    add('sphere','sign_bread_emblem_cap_'+str(j),[x,z],2.94+h/2,[.075,.075,.028],'sign_white','Signage',FRONT_YAW)
for i in range(12):
    x,z=qfront(.26+i*.012,.13)
    box('compressed_black_accordion_leaf_'+str(i),x,z,1.31,.013,2.58,.16,'black','Entrance',FRONT_YAW)
edge('recessed_front_track',qfront(.21,.20),qfront(W-.21,.20),2.57,.035,.055,'steel','Entrance')

# Leave 0.92m unobstructed public entrance next to left stile.
pos=qfront(1.55,.53)
box('cashier_brushed_stainless_cabinet',*pos,.48,1.00,.96,.70,'steel','Cashier',FRONT_YAW,collider=True)
box('cashier_countertop',*pos,.983,1.04,.05,.73,'steel','Cashier',FRONT_YAW)
for c,s in enumerate([2.74,4.02]):glasscase('front_refrigerated_case_'+str(c),qfront(s,.53),1.26,yaw=FRONT_YAW)

# Open sales aisle + source-matched U-shaped timber display cabinetry.
for i,z in enumerate([-30.55,-32.10,-33.65,-35.20]):
    cabinet('right_wood_display_'+str(i),65.95,z,1.42,.58,yaw=90)
    # visible slatted elevated shelf, individually modeled timber strips
    for k in range(9):box(f'right_slatted_top_{i}_{k}',65.95+(k-4)*.055,z,1.00,.027,.055,1.39,'timber','Cabinetry')
    box('right_upper_shelf_'+str(i),65.97,z,1.43,.48,.055,1.42,'timber_light','Cabinetry')
for i,x in enumerate([63.15,64.57,65.83]):cabinet('rear_return_display_'+str(i),x,-36.25,1.30,.64)
for i,z in enumerate([-30.85,-32.42]):glasscase('left_interior_refrigerated_'+str(i),[62.85,z],1.48,.66,90)
cabinet('central_low_gift_island',64.50,-29.58,1.22,.72)
cabinet('central_gift_island_second',64.45,-33.83,.95,.68)

# Teal reeded-glass staff-front partition; service door is a 3D framed opening.
# Source evidence controls shape and role; exact source plan measurements are not transferred.
edge('staff_partition_lower', [63.23,-37.20],[66.24,-37.20],.05,.77,.09,'cream','StaffFront')
edge('staff_partition_upper',[62.27,-37.20],[66.24,-37.20],2.35,.48,.09,'cream','StaffFront')
for i in range(3):
    x=63.73+i*.91
    box('teal_reeded_frame_'+str(i),x,-37.19,1.57,.87,1.48,.08,'teal','StaffFront')
    box('reeded_staff_window_'+str(i),x,-37.125,1.57,.76,1.36,.016,'reeded','StaffFront')
    for k in range(18):box(f'glass_flute_{i}_{k}',x-.35+k*.041,-37.112,1.57,.008,1.35,.008,'glass','StaffFront')
    box('staff_window_mullion_'+str(i),x,-37.09,1.57,.025,1.36,.025,'teal','StaffFront')
# Side-facing staff door shown in contractor photograph; not a new public entry.
box('staff_service_door_reeded_glass',62.75,-37.16,1.10,.82,2.10,.032,'reeded','StaffFront')
for x in [62.30,63.20]:box('staff_door_jamb_'+str(x),x,-37.16,1.10,.055,2.20,.06,'teal','StaffFront')
box('staff_door_top_rail',62.75,-37.16,2.18,.95,.055,.06,'teal','StaffFront')
box('staff_door_handle',63.07,-37.105,1.00,.035,.24,.045,'chrome','StaffFront')

# Actual round projecting sign: solid plate, raised ring + letters, not photograph billboard.
sx,sz=63.05,-30.02
add('cylinder','round_BNC_sign_body',[sx,sz],2.20,[.72,.025,.72],'timber_dark','Signage',euler=[90,0,90])
add('cylinder','round_BNC_lit_disc',[sx+.038,sz],2.20,[.63,.009,.63],'warm_lit','Signage',euler=[90,0,90])
add('cylinder','round_BNC_inner_disc',[sx+.052,sz],2.20,[.56,.009,.56],'timber_dark','Signage',euler=[90,0,90])
text('round_actual_BNC_letters','B&C',[sx+.068,sz],2.20,.44,.22,yaw=90)
box('projecting_sign_bracket',sx-.24,sz,2.39,.52,.045,.035,'black','Signage')
# Source vertical advertising screen uses direct modeled bread imagery and brand marks.
box('vertical_ad_black_frame',62.47,-29.89,1.80,.045,1.30,.70,'black','Signage')
box('vertical_ad_light_panel',62.501,-29.89,1.80,.012,1.20,.61,'white','Signage')
text('vertical_ad_actual_brand','비엔씨',[62.517,-29.89],1.38,.45,.12,yaw=90)

# Original photographed cream coffer ceiling + warm gold medallions.
polygon_slab('approved_polygon_cream_ceiling',SHELL,2.91,.05,'cream','Ceiling')
for ix in range(3):
    for iz in range(8):
        x,z=62.85+ix*1.12,-28.75-iz*1.08
        if not SHELL.covers(Point(x,z).buffer(.55)):continue
        box(f'ceiling_coffer_panel_{ix}_{iz}',x,z,2.885,.98,.045,.94,'white','Ceiling')
        for dx in [-.49,.49]:box(f'coffer_moulding_x_{ix}_{iz}_{dx}',x+dx,z,2.84,.023,.055,.98,'cream','Ceiling')
        for dz in [-.47,.47]:box(f'coffer_moulding_z_{ix}_{iz}_{dz}',x,z+dz,2.84,.98,.055,.023,'cream','Ceiling')
        cyl(f'gold_medallion_{ix}_{iz}',x,z,2.826,.27,.020,'gold','Lighting')
        cyl(f'bulb_socket_{ix}_{iz}',x,z,2.785,.052,.08,'cream','Lighting')
        add('sphere',f'warm_visible_bulb_{ix}_{iz}',[x,z],2.72,[.060,.11,.060],'warm_lit','Lighting')
        if ix==1 and iz%2==0:add('light',f'own_warm_light_{iz}',[x,z],2.60,mat=None,group='Lighting',range=2.2,intensity=.20,color=[1,.86,.66,1])
for x in [62.43,66.26]:
    for j,z in enumerate([-29.0,-31.5,-34.0,-36.5]):
        box(f'track_rail_{x}_{j}',x,z,2.66,.033,.055,1.35,'white','Lighting')
        cyl(f'track_spot_body_{x}_{j}',x,z,2.55,.105,.20,'white','Lighting')
        cyl(f'track_spot_lens_{x}_{j}',x,z,2.44,.084,.012,'display_lit','Lighting')
for i,z in enumerate([-30.1,-34.3]):
    box('ceiling_AC_grille_'+str(i),64.5,z,2.875,.55,.04,.52,'cream','HVAC')
    for k in range(10):box(f'ac_slit_{i}_{k}',64.5,z+(k-4.5)*.043,2.846,.48,.009,.013,'black','HVAC')

# CC0 scanned real food assets. Their surface/albedo are retained, no word substitutes.
free_receipt=json.loads((BASE/'sources/FREE-3D-ASSET-ACQUISITION-R25.json').read_text())
imports=[]
for f in free_receipt['assets']:
    model=f['model'];name=pathlib.Path(f['source_path']).name
    dest='FreeModels/'+model+'/'+name
    imports.append({'source':str((PROJECT/f['source_path']).resolve()),'destination':dest,'sha256':f['sha256']})
    if f.get('derived_smoothness'):
        derived=f['derived_smoothness']
        imports.append({'source':str((PROJECT/derived['source_path']).resolve()),
            'destination':'FreeModels/'+model+'/metallic_smoothness.png','sha256':derived['sha256']})
for model in ['croissant','hamburger_buns','carrot_cake','strawberry_chocolate_cake']:
    materials['scan_'+model]={'color':[1,1,1,1],'smoothness':.24,
        'texture_asset':'Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/'+model+'/diff.jpg',
        'normal_asset':'Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/'+model+'/normal.exr',
        'metallic_gloss_asset':'Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/'+model+'/metallic_smoothness.png'}
for key in ['timber','timber_light','timber_dark']:
    materials[key].update(texture_asset='Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/wood_table_001/diff.jpg',
        normal_asset='Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/wood_table_001/normal.jpg',
        metallic_gloss_asset='Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/wood_table_001/metallic_smoothness.png')
def scanned(n,kind,x,z,y,w,h,d,yaw=0):
    return asset(n,'Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/'+kind+'/'+kind+'.fbx',
                 x,z,y,w,h,d,'scan_'+kind,yaw,'ScannedBakeryProducts','NAVER_CURRENT_PRODUCTS')

# Clear pinched retail bags: shared 3D mesh, real product visible inside each independent packet.
bagv=[[-.5,0,-.34],[.5,0,-.34],[.5,0,.34],[-.5,0,.34],[-.45,.72,-.28],[.45,.72,-.28],
      [.45,.72,.28],[-.45,.72,.28],[-.12,1,-.04],[.12,1,-.04],[.12,1,.04],[-.12,1,.04]]
bagt=[]
for lo,hi in [(0,4),(4,8)]:
    for k in range(4):a=lo+k;b=lo+(k+1)%4;c=hi+(k+1)%4;d=hi+k;bagt.extend([a,b,c,a,c,d])
library['pinched_clear_bag']={'vertices':bagv,'triangles':bagt}
def package(n,x,z,y,w=.21,h=.25,d=.17):
    add('library_mesh',n,[x,z],y,[w,h,d],'cellophane','IndividualRetailPackaging',mesh_key='pinched_clear_bag',source='NAVER_CURRENT_PRODUCTS')

# Dense slatted bread shelves, individual scans plus transparent bags and physical tray/price cards.
product_count=0
for row,z in enumerate([-30.40,-31.94,-33.48,-35.02]):
    for level,y in enumerate([.965,1.477]):
        for j in range(6):
            zz=z+(j-2.5)*.19
            for k in range(2):
                xx=65.80+k*.24
                kind='croissant' if (row+j+k)%3 else 'hamburger_buns'
                scanned(f'wrapped_bread_{row}_{level}_{j}_{k}',kind,xx,zz,y,.21,.12,.15,90+(row%2)*8)
                package(f'clear_bread_bag_{row}_{level}_{j}_{k}',xx,zz,y-.005)
                product_count+=1
        for j in range(2):box(f'physical_product_card_{row}_{level}_{j}',65.64,z+(j-.5)*.72,y+.10,.018,.075,.13,'paper_ivory','PriceCardSurfaces',90)
for j,x in enumerate([62.90,63.54,64.18,64.82,65.46,66.10]):
    for iz in range(2):
        for ix in range(2):
            xx=x+(ix-.5)*.18;zz=-36.25+(iz-.5)*.20
            scanned(f'rear_bread_{j}_{iz}_{ix}','croissant' if j%2 else 'hamburger_buns',xx,zz,.94,.20,.11,.16,j*16)
            package(f'rear_bread_packet_{j}_{iz}_{ix}',xx,zz,.93)
            product_count+=1

# Detailed gift boxes are physical open containers and individually wrapped contents.
for island,(cx,cz,w,d) in enumerate([(64.50,-29.58,1.22,.72),(64.45,-33.83,.95,.68)]):
    for ix in range(3):
        for iz in range(2):
            x=cx+(ix-1)*.31;z=cz+(iz-.5)*.29;mat=['paper_pink','paper_orange','paper_ivory'][(ix+iz)%3]
            box(f'giftbox_{island}_{ix}_{iz}_bottom',x,z,.96,.28,.06,.24,mat,'GiftPackages')
            for dx in [-.135,.135]:box(f'giftbox_{island}_{ix}_{iz}_side_{dx}',x+dx,z,1.005,.012,.12,.24,mat,'GiftPackages')
            for dz in [-.115,.115]:box(f'giftbox_{island}_{ix}_{iz}_end_{dz}',x,z+dz,1.005,.27,.12,.012,mat,'GiftPackages')
            for m in range(3):
                xx=x+(m-1)*.073
                scanned(f'giftbox_bun_{island}_{ix}_{iz}_{m}','hamburger_buns',xx,z,.995,.065,.055,.17)
                product_count+=1
            box(f'giftbox_{island}_{ix}_{iz}_clear_window',x,z,1.075,.27,.008,.23,'cellophane','GiftPackages')
            box(f'giftbox_{island}_{ix}_{iz}_ribbon',x,z,1.083,.024,.008,.24,'paper_green','GiftPackages')

# Cakes/refrigerated items on all actual shelves. All independent scan instances.
for case,s in enumerate([2.74,4.02]):
    for lev,y in enumerate([.69,.95,1.20]):
        for j in range(3):
            x,z=qfront(s+(j-1)*.32,.53)
            kind='strawberry_chocolate_cake' if (case+lev+j)%2 else 'carrot_cake'
            scanned(f'front_cold_cake_{case}_{lev}_{j}',kind,x,z,y,.25,.17,.25,FRONT_YAW)
            product_count+=1
for case,z in enumerate([-30.85,-32.42]):
    for lev,y in enumerate([.69,.95,1.20]):
        for j in range(4):
            zz=z+(j-1.5)*.30
            scanned(f'interior_cake_{case}_{lev}_{j}','strawberry_chocolate_cake' if j%2 else 'carrot_cake',62.85,zz,y,.24,.17,.24,90)
            product_count+=1

# CC0 Kenney computer meshes: actual POS hardware surfaces and handheld mouse.
pos_meshes='Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/'
for name,parts,fit in [('computerScreen',2,[.31,.30,.22]),('computerKeyboard',2,[.29,.035,.11]),('computerMouse',1,[.055,.03,.085])]:
    for k in range(parts):
        q=qfront(1.55+(.20 if name=='computerMouse' else 0),.43 if name=='computerScreen' else .26)
        add('mesh_ref',f'cc0_POS_{name}_{k}',q,1.012,mat='black' if k==0 else 'steel',group='Cashier',yaw=FRONT_YAW+180,
            mesh_asset=pos_meshes+f'kenney_furniture_{name}__{k}.asset',fit_size=fit)
box('physical_payment_card_terminal',*qfront(1.94,.25),1.056,.13,.12,.15,'black','Cashier',FRONT_YAW)
box('POS_receipt_printer',*qfront(1.18,.53),1.06,.16,.15,.20,'black','Cashier',FRONT_YAW)
box('POS_white_receipt_roll',*qfront(1.18,.47),1.12,.115,.02,.08,'paper_ivory','Cashier',FRONT_YAW)

# Observed staff work equipment (contractor photo open service door): realistic free fixtures.
# No invented gas installation or guessed full baking line behind unseen partitions.
kit='Assets/ChooGuard/Art/StationInterior/Kits/restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/fbx (unity)/'
asset('cc0_staff_stainless_worktable',kit+'kitchentable_A_large.fbx',63.24,-38.37,0,.72,.88,1.48,'steel',90,'VisibleStaffEquipment')
asset('cc0_staff_visible_sink',kit+'kitchentable_sink_large.fbx',65.41,-38.20,0,1.30,.90,.65,'steel',0,'VisibleStaffEquipment')
asset('cc0_staff_under_counter_cabinet',kit+'kitchencabinet.fbx',64.32,-38.50,0,.65,.87,.62,'cream',0,'VisibleStaffEquipment')
asset('cc0_bread_basket_front','Assets/ChooGuard/Art/StationInterior/FreeSourceAssetsC165_20261018/Prefabs/wicker_basket_01.prefab',65.90,-28.87,.94,.40,.22,.30,None,0,'GiftPackages')
asset('cc0_bread_basket_rear','Assets/ChooGuard/Art/StationInterior/FreeSourceAssetsC165_20261018/Prefabs/wicker_basket_01.prefab',65.46,-36.25,.94,.42,.22,.31,None,0,'GiftPackages')
# Printed gift packaging source rows, solid cartons with handles, not fake room labels.
for j in range(14):
    x=62.7+(j%7)*.50;z=-36.48 if j<7 else -38.83
    box(f'gift_carry_box_{j}',x,z,1.31,.31,.45,.20,['paper_orange','paper_pink','paper_ivory'][j%3],'GiftPackages')
    box(f'gift_handle_top_{j}',x,z,1.60,.13,.022,.02,'paper_ivory','GiftPackages')
    for dx in [-.065,.065]:box(f'gift_handle_leg_{j}_{dx}',x+dx,z,1.56,.018,.07,.02,'paper_ivory','GiftPackages')

packet={'store_key':'bnc-east','origin_q':[64,-32],'floor_world_y':7.0,
        'source_packet':str(BASE.relative_to(PROJECT)/'SOURCE-EVIDENCE-R25.json'),
        'materials':materials,'mesh_library':library,'source_imports':imports,'objects':objects,
        'approved_work_region':APPROVED['geometry_qx_minus_qy'],
        'source_model_metric_qualification':'PHOTO/DESIGN_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT',
        'source_original_design_caption_discrepancy':'Contractor project says2F, sourceproposal drawing title1층; only corroborated fixture/material forms used.',
        'free_asset_product_count':product_count}

# Geometry is constructed within the approved bay. Reject authoring dimensions that cross it.
# This is creation-time domain clipping, not a Scene QA/probe stage.
outside=[]
for o in objects:
    if o['kind'] in ['text','light','mesh'] or 'q' not in o:continue
    x,z=o['q']
    if not REGION.buffer(.015).covers(Point(x,z)):
        outside.append((o['id'],o['q']))
if outside:raise RuntimeError('Authoring center outside fixed approved work region:'+str(outside[:15]))

(BASE/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2))
sha=hashlib.sha256((BASE/'STORE-PACKET-R25.json').read_bytes()).hexdigest()
(BASE/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps({
    'store':'bnc-east','approved_region':'APPROVED-STORE-WORK-REGION.json','public_front_edge':[A,B],
    'front_length_MODEL_m':W,'front_tangent':U,'inward_normal':IN,'floor_y':7.0,
    'front_left_open_public_passage_MODEL_m':.92,'height_MODEL':'source drawing3.47m fascia/top and2.9m decorated ceiling',
    'placement_basis':'Exact approved R24 northernfront/H1, no relocation to shared Soban divider or nativefacade. Fixture dimensions inferred within accepted boundary.',
    'source_original_plan_discrepancy':'Contractor2F project vs printed1층 proposal label; do not treat original plan worldplacement or hiddenBOH as confirmed current asbuilt.',
    'source_assets':'CC0 PolyHaven scanned croissant/hamburgerbuns/carrotcake/strawberrychocolatecake; CC0 Kenney POS; CC0 KayKit observed servicefixtures; existing CC0 wickerbaskets',
    'actual_product_geometry_count':product_count,'group_counts':dict(collections.Counter(o['group']for o in objects)),
    'packet_sha256':sha,'source_assets_modified':False,'scene_QA':False},ensure_ascii=False,indent=2))
print(json.dumps({'object_count':len(objects),'scanned_products':product_count,'packet_sha256':sha,'groups':dict(collections.Counter(o['group']for o in objects))},indent=2))
