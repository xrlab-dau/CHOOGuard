"""Own R25 source-bound Hwang 2F packet author. No Scene read/write here."""
from pathlib import Path
import sys, json, math, hashlib, shutil, importlib.util
import numpy as np
import cv2

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'
OWN=BASE/'hwang-2f'
STAGE=OWN/'sources/construction'
STAGE.mkdir(exist_ok=True)
ASSET='Assets/ChooGuard/Art/StationInterior/R25_Shops/hwang-2f'

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def save(name,d): (OWN/name).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')

def author_sign():
    spec=importlib.util.spec_from_file_location('outline',BASE/'photo_outline_to_solid.py')
    outline=importlib.util.module_from_spec(spec);spec.loader.exec_module(outline)
    im=cv2.imread(str(OWN/'sources/ROOT-dhkstn6051-224434124410-00.jpg'))
    # Exact image corners of the source logo baseline and capheight, no font recreation.
    corners=np.float32([[192,110],[560,72],[562,193],[192,230]])
    mat=cv2.getPerspectiveTransform(corners,np.float32([[0,0],[899,0],[899,269],[0,269]]))
    rect=cv2.warpPerspective(im,mat,(900,270))
    mask=(cv2.cvtColor(rect,cv2.COLOR_BGR2GRAY)>175).astype('uint8')*255
    n,lab,stats,cent=cv2.connectedComponentsWithStats(mask)
    clean=np.zeros_like(mask)
    for i in range(1,n):
        if stats[i,cv2.CC_STAT_AREA]>95: clean[lab==i]=255
    clean=cv2.morphologyEx(clean,cv2.MORPH_CLOSE,np.ones((3,3),np.uint8))
    cv2.imwrite(str(STAGE/'source-logo-rectified.png'),rect)
    cv2.imwrite(str(STAGE/'source-logo-ink-mask.png'),clean)
    logo=outline.closed_solid_from_mask(clean,3.25,.83,.045,minimum_area=60,epsilon=.65)
    # Genuine printed historical plaque is traced as geometry too.
    c=np.float32([[118,137],[184,129],[184,215],[119,218]])
    m=cv2.getPerspectiveTransform(c,np.float32([[0,0],[239,0],[239,299],[0,299]]))
    plaque=cv2.warpPerspective(im,m,(240,300))
    gray=cv2.cvtColor(plaque,cv2.COLOR_BGR2GRAY)
    ink=(gray<175).astype('uint8')*255;ink[:12,:]=0;ink[-10:,:]=0;ink[:,:9]=0;ink[:,-9:]=0
    cv2.imwrite(str(STAGE/'source-since-plaque-rectified.png'),plaque)
    cv2.imwrite(str(STAGE/'source-since-plaque-ink.png'),ink)
    since=outline.closed_solid_from_mask(ink,.46,.60,.012,minimum_area=9,epsilon=.35)
    (STAGE/'source-logo-solids.json').write_text(json.dumps({'logo':logo,'since':since}))
    return logo,since

if __name__=='__main__' and '--sign-only' in sys.argv:
    a,b=author_sign();print('Authored source logo',len(a['vertices']),'vertices',a['source_polygons'],'source contours');sys.exit()

imports=[]
def stage_import(source,destination):
    source=Path(source)
    imports.append({'source':str(source.resolve()),'destination':destination,'sha256':sha(source)})
    return ASSET+'/'+destination

def owncopy(source,rel):
    target=STAGE/rel;target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,target)
    return target

def intersect(a,b,c,d):
    a,b,c,d=map(lambda t:np.asarray(t,float),[a,b,c,d])
    t=np.linalg.solve(np.array([b-a,c-d]).T,c-a)[0]
    return a+t*(b-a)

def derive_fit():
    data=json.loads((OWN/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text())
    # Long true face sections, copied verbatim from R19 immutable current vectors.
    west=[[-28.29403854649045,-6.755198389998743],[-28.291285236596124,-6.191988832166071]]
    east=[[-20.677397227472756,-6.431936101170698],[-20.68014462963346,-6.9951536773602765]]
    front=[[-20.466398501455917,-7.004197882019781],[-19.903182653961395,-7.006950953706608]]
    rear=[[-20.1051323223192,-13.498973713804775],[-20.668356108073773,-13.496235348874094]]
    rl=intersect(*west,*rear);rr=intersect(*east,*rear)
    fl=intersect(*west,*front);fr=intersect(*east,*front)
    xaxis=(rr-rl)/np.linalg.norm(rr-rl);zaxis=(fl-rl)/np.linalg.norm(fl-rl)
    width=np.linalg.norm(rr-rl);depth=np.linalg.norm(fl-rl)
    basis={'store_key':'hwang-2f','status':'ACTUAL_FACE_EXTENSIONS_INTERSECTIONS_WITH_PHOTO_BOUND_MODEL_FIT',
        'local_CAD':str(OWN/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json'),'local_CAD_sha256':sha(OWN/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json'),
        'face_anchors':{'C177_029_east_face':west,'C177_030_west_face':east,'C177_030_south_face_public_front':front,'C177_047_north_face_rear':rear},
        'computed_intersections_q':[rl.tolist(),rr.tolist(),fr.tolist(),fl.tolist(),rl.tolist()],
        'working_inset_m':.045,'width_MODEL_m':float(width),'depth_MODEL_m':float(depth),
        'geometry_semantics':'Actual fixed structural face relationships define bay. Source photographs define visible fixture forms and arrangement. All fixture dimensions/fit are MODEL, not measured/asbuilt equality.',
        'no_old_sourcebox_metrics':True,'no_native_room_mesh_here_at_7_15_8_4_10_5':'Official5 has zero sectionsegments in this bay; preserve all existing columns and approved floor. Own shop frame/visible partitions only.',
        'public_approach':'Entire outward +qZ beyond actual front line is excluded. No threshold/floor edits, no queue barriers outside footprint.',
        'BnCwest_coordination':'BnCWest constructor confirms own x<-29.10; native029 fixed and reused as structural neighbor. Hwang parts remain inside029eastface.',
        'source_epochs':['2026-10-07','2026-10-12','2026-05-18 navigation only'],
        'unseen_BOH':'No inferred hidden room program; only source-window-visible prep/hood/fan/wall appliance represented.'}
    save('MODEL-PLACEMENT-BASIS-R25.json',basis)
    return rl,xaxis,zaxis,float(width),float(depth)

def mesh_box(a,b):
    # Closed indexed box useful in authored composite mesh prototypes.
    x,y,z=a;X,Y,Z=b
    return {'vertices':[[x,y,z],[X,y,z],[X,Y,z],[x,Y,z],[x,y,Z],[X,y,Z],[X,Y,Z],[x,Y,Z]],
      'triangles':[0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5]}

def merge(parts):
    v=[];t=[]
    for m in parts:
        n=len(v);v.extend(m['vertices']);t.extend(n+i for i in m['triangles'])
    return {'vertices':v,'triangles':t}

def rounded_ring(width,depth,radius,n=8):
    pts=[]
    for cx,cz,start in [(width/2-radius,depth/2-radius,0),(-width/2+radius,depth/2-radius,90),(-width/2+radius,-depth/2+radius,180),(width/2-radius,-depth/2+radius,270)]:
        for j in range(n):
            a=math.radians(start+j*90/n);pts.append([cx+radius*math.cos(a),cz+radius*math.sin(a)])
    return pts

def tray_mesh():
    loops=[(.52,.40,.035,0),(.56,.44,.045,.045),(.56,.44,.045,.057),(.53,.41,.03,.057),(.49,.37,.025,.015)]
    v=[];t=[]
    for w,d,r,y in loops: v.extend([[x,y,z] for x,z in rounded_ring(w,d,r)])
    n=32
    for k in range(len(loops)-1):
        for j in range(n):
            a=k*n+j;b=k*n+(j+1)%n;c=(k+1)*n+(j+1)%n;d=(k+1)*n+j;t.extend([a,c,b,a,d,c])
    # Separate underside and interior floor form a closed stainless pan, no open proxy cube.
    v.extend([[0,0,0],[0,.015,0]]);bot=len(v)-2;inside=len(v)-1
    for j in range(n):t.extend([bot,(j+1)%n,j,inside,4*n+j,4*n+(j+1)%n])
    return {'vertices':v,'triangles':t}

def dome_mesh():
    # Thin closed half-cylinder cover, fitted to each genuine source GN display pan.
    v=[];t=[];n=20
    for thickness in [0,.004]:
        for x in [-.276,.276]:
            for i in range(n+1):
                a=i*math.pi/n;v.append([x,(.22-thickness)*math.sin(a),(.22-thickness)*math.cos(a)])
    stride=n+1
    for k in [0,2]:
        for j in range(n):
            a=k*stride+j;b=a+1;c=(k+1)*stride+j+1;d=c-1
            t.extend([a,b,c,a,c,d] if k==0 else [a,c,b,a,d,c])
    for k in [0,1]:
        for j in range(n):
            a=k*stride+j;b=a+1;c=(k+2)*stride+j+1;d=c-1;t.extend([a,c,b,a,d,c])
    for j in [0,n]:
        a=j;b=stride+j;c=3*stride+j;d=2*stride+j;t.extend([a,b,c,a,c,d])
    return {'vertices':v,'triangles':t}

def pill_mesh(length=.20,height=.055,width=.085,seed=1):
    rng=np.random.default_rng(seed);v=[];uv=[];t=[]
    for j in range(13):
        phi=math.pi*j/12
        for k in range(25):
            th=2*math.pi*k/24
            rough=1+float(rng.uniform(-.035,.035))
            x=length/2*math.cos(phi)*rough
            y=height/2+height/2*math.sin(phi)*math.sin(th)*rough
            z=width/2*math.sin(phi)*math.cos(th)*rough
            v.append([x,y,z]);uv.append([k/24,j/12])
    for j in range(12):
        for k in range(24):
            a=j*25+k;b=a+25;t.extend([a,b,a+1,a+1,b,b+1])
    return {'vertices':v,'triangles':t,'uv':uv}

def build():
    logo,since=author_sign();rl,xaxis,zaxis,W,D=derive_fit()
    angle=math.degrees(math.atan2(-xaxis[1],xaxis[0]))
    def q(u,v): return (rl+xaxis*u+zaxis*v).tolist()
    p={'store_key':'hwang-2f','origin_q':q(W/2,D/2),'floor_world_y':7,
       'source_packet':str(OWN/'SOURCE-EVIDENCE-R25.json'),'source_imports':imports,
       'materials':{},'mesh_library':{},'objects':[]}
    mats=p['materials'];meshes=p['mesh_library'];objects=p['objects']
    def material(name,col,metal=0,smooth=.4,**extra):
        mats[name]={'color':col,'metallic':metal,'smoothness':smooth,**extra};return name
    material('warm_white',[.83,.80,.70,1],0,.30)
    material('cream',[.94,.90,.78,1],0,.32)
    material('fascia',[.014,.022,.027,1],.2,.40)
    material('stainless',[.67,.71,.73,1],.92,.67)
    material('brushed_aluminium',[.38,.40,.41,1],.78,.46)
    material('ribbed_charcoal',[.073,.082,.087,1],.62,.40)
    material('white_plastic',[.93,.94,.91,1],0,.34)
    material('black_rubber',[.016,.019,.021,1],0,.26)
    material('clear_cover',[.93,.96,.97,.095],.05,.97,transparent=True)
    material('glass',[.70,.86,.91,.12],.1,.97,transparent=True)
    material('light_warm',[1,.80,.47,1],0,.60,emission=[1.7,1.15,.46,1])
    material('logo_white',[.97,.98,.91,1],.08,.50,emission=[1.7,1.8,1.5,1])
    material('since_ink',[.013,.029,.042,1],0,.40)
    material('screen',[.38,.64,.68,1],.1,.90,emission=[.12,.22,.24,1])
    material('bamboo',[.64,.44,.19,1],0,.28)
    material('seaweed',[.026,.039,.019,1],0,.62)
    material('broth',[.40,.23,.085,.95],.15,.85)
    material('paper',[.91,.90,.86,1],0,.24)

    # Source-derived surface samples supply plausible real grain and crumb.
    photograph=cv2.imread(str(OWN/'sources/ROOT-tkvlf14-224434048278-09.jpg'))
    wood=cv2.resize(photograph[393:430,15:235],(512,128))
    cv2.imwrite(str(STAGE/'source-light-wood-grain.png'),wood)
    woodtex=stage_import(STAGE/'source-light-wood-grain.png','Textures/source-light-wood-grain.png')
    material('photo_lightwood',[.94,.90,.82,1],0,.36,texture_asset=woodtex)
    crumb=cv2.resize(photograph[323:346,325:353],(256,256))
    cv2.imwrite(str(STAGE/'source-fried-crumb.png'),crumb)
    crumbtex=stage_import(STAGE/'source-fried-crumb.png','Textures/source-fried-crumb.png')
    material('fried_golden',[1,.85,.64,1],0,.34,texture_asset=crumbtex)
    material('fried_brown',[.69,.41,.14,1],0,.31,texture_asset=crumbtex)

    def add(kind,id,group,u,v,y,**kw):
        o={'kind':kind,'id':id,'group':group,'q':q(u,v),'y':y,'yaw':angle,
            'source_ids':['OCT12_01','OCT12_09','OCT07_00'],'qualification':'PHOTO_BOUND_MODEL_NOT_FIELD_MEASURED',**kw}
        objects.append(o);return o
    def box(id,g,u,v,y,s,mat,**kw):return add('box',id,g,u,v,y,size=s,material=mat,**kw)
    def lib(id,g,key,u,v,y,mat,**kw):
        kw.setdefault('collider',False)
        return add('library_mesh',id,g,u,v,y,mesh_key=key,material=mat,**kw)
    def cyl(id,g,u,v,y,diam,height,mat,**kw):return add('cylinder',id,g,u,v,y,size=[diam,height/2,diam],material=mat,collider=False,**kw)
    def edge(id,g,a,b,bottom,height,thickness,mat):
        objects.append({'kind':'edge','id':id,'group':g,'a':q(*a),'b':q(*b),'bottom':bottom,'height':height,'thickness':thickness,'material':mat,'source_ids':['OCT07_00','OCT12_01'],'qualification':'PHOTO_BOUND_MODEL_NOT_FIELD_MEASURED'})

    # Photo-bound open storefront, no native room exists in this actual structural cell.
    # Rear visible partition has an actual service aperture; its source-hidden program stays open/unspecified.
    edge('Own_Left_Interior_Wall','Booth',(.09,.09),(.09,D-.10),0,3.25,.09,'warm_white')
    edge('Own_Right_Interior_Wall','Booth',(W-.09,.09),(W-.09,D-.10),0,3.25,.09,'warm_white')
    edge('Own_Rear_Visible_Backing','Booth',(.09,.09),(W-.09,.09),0,3.25,.08,'stainless')
    # Partition is behind the source-visible warm case, opening retained.
    edge('Service_Partition_Low','Visible_Prep', (1.35,1.02),(W-.12,1.02),0,1.20,.09,'warm_white')
    edge('Service_Partition_Header','Visible_Prep',(1.35,1.02),(W-.12,1.02),2.45,.80,.09,'warm_white')
    edge('Service_Partition_West_Pier','Visible_Prep',(1.35,1.02),(2.15,1.02),1.20,1.25,.09,'warm_white')
    edge('Service_Partition_East_Pier','Visible_Prep',(6.55,1.02),(W-.12,1.02),1.20,1.25,.09,'warm_white')
    edge('Service_Window_Lower_Metal','Visible_Prep',(2.12,1.08),(6.58,1.08),1.17,.045,.055,'stainless')
    edge('Service_Window_Upper_Metal','Visible_Prep',(2.12,1.08),(6.58,1.08),2.43,.045,.055,'stainless')
    for u in [2.13,6.57]:box('Service_Window_Jamb_'+str(u),'Visible_Prep',u,1.08,1.81,[.045,1.25,.055],'stainless')
    box('Thin_Observed_Own_Ceiling','Booth',W/2,D/2,3.28,[W-.18,.08,D-.18],'cream')
    box('Black_Source_Fascia','Physical_Brand',W/2,D-.20,3.45,[W-.18,.90,.22],'fascia')
    box('Fascia_Lower_Extruded_Rim','Physical_Brand',W/2,D-.075,2.995,[W-.18,.045,.055],'black_rubber')
    # Jambs fit inside fixed structural faces; they do not recreate native columns.
    for u in [.15,W-.15]:box('Thin_Black_Shop_Jamb_'+str(round(u,3)),'Booth',u,D-.10,1.50,[.12,3,.12],'fascia')
    for i in range(9):
        u=.5+i*(W-1)/8;cyl('Fascia_Downlight_Housing_'+str(i),'Lighting',u,D-.14,2.98,.10,.06,'stainless')
        cyl('Fascia_Downlight_Lens_'+str(i),'Lighting',u,D-.14,2.942,.074,.012,'light_warm')
    meshes['source_hwang_logo']=logo;meshes['source_since_ink']=since
    lib('Actual_Source_Hwang_Closed_Letter_Solids','Physical_Brand','source_hwang_logo',W/2-.20,D-.04,3.47,'logo_white',collider=False)
    box('Since1940_White_Solid_Plaque','Physical_Brand',W/2+1.91,D-.065,3.47,[.48,.62,.045],'logo_white',collider=False)
    lib('Since1940_Source_Outlined_Ink_Solids','Physical_Brand','source_since_ink',W/2+1.91,D-.038,3.47,'since_ink')
    # Mounting spacers behind actual closed logo letters.
    for i in range(6):cyl('Logo_Mount_Spacer_'+str(i),'Physical_Brand',W/2-1.5+i*.55,D-.015,3.4,.025,.065,'stainless',euler=[90,angle,0])

    # Observed right-side glazed lights overlooking the hall, framed independently.
    edge('Side_Observed_Glass','Booth',(.15,1.50),(.15,4.95),1.05,1.55,.012,'glass')
    for v in [1.5,2.65,3.80,4.95]:box('Side_Window_Mullion_'+str(v),'Booth',.18,v,1.83,[.06,.06+1.55,.05],'fascia')
    for y in [1.05,2.60]:edge('Side_Window_Rail_'+str(y),'Booth',(.18,1.50),(.18,4.95),y,.05,.06,'fascia')

    # Counter modules have actual wood stepping, fluted aluminium fronts, light strips,
    # real rounded GN pan geometry, clear curved covers and source-shaped merchandise.
    meshes['GN_stainless_pan']=tray_mesh();meshes['transparent_domed_cover']=dome_mesh()
    meshes['fishcake_bar']=pill_mesh(.19,.045,.075,4)
    meshes['fishcake_roll']=pill_mesh(.18,.065,.065,8)
    meshes['croquette']=pill_mesh(.09,.075,.085,9)
    meshes['fishcake_ball']=pill_mesh(.055,.055,.055,2)
    meshes['fluted_front_1m']=merge([mesh_box([-.5,-.30,-.008],[.5,.30,.008])]+[
        mesh_box([-.5+i*.018,-.30,.008],[-.5+i*.018+.005,.30,.016]) for i in range(56)])

    # Source menu prints are information on physical card panels, never equipment substitutes.
    crops=[(20,70,177,278),(257,82,397,278),(519,80,663,280),(795,78,958,277)]
    for i,(x,y,X,Y) in enumerate(crops):
        image=photograph[y:Y,x:X];path=STAGE/f'actual-product-card-{i}.png';cv2.imwrite(str(path),image)
        tex=stage_import(path,f'Textures/actual-product-card-{i}.png')
        material('menu_'+str(i),[1,1,1,1],0,.50,texture_asset=tex)
    meshes['printed_card_plane']={'vertices':[[.13,-.18,0],[-.13,-.18,0],[-.13,.18,0],[.13,.18,0]],'triangles':[0,2,1,0,3,2],
       'uv':[[0,0],[1,0],[1,1],[0,1]]}

    def counter(id,u,v,length,depth=.98,height=.88,yaw=0):
        box(id+'_Toe','Warm_Display',u,v,.07,[length-.05,.14,depth-.04],'stainless',yaw=angle+yaw)
        box(id+'_Body','Warm_Display',u,v,.47,[length,.74,depth],'ribbed_charcoal',yaw=angle+yaw)
        # local-front ribs follow each source module, not a printed stripe texture.
        direction=np.array([math.sin(math.radians(yaw)),math.cos(math.radians(yaw))])
        lib(id+'_Actual_Fluted_Face','Warm_Display','fluted_front_1m',u+direction[0]*depth/2,v+direction[1]*depth/2,.47,'ribbed_charcoal',yaw=angle+yaw,size=[length,1,1])
        box(id+'_Wood_Lower_Level','Warm_Display',u,v,.90,[length,.065,depth+.03],'photo_lightwood',yaw=angle+yaw)
        box(id+'_Wood_Riser','Warm_Display',u-direction[0]*.26,v-direction[1]*.26,1.10,[length,.36,.45],'photo_lightwood',yaw=angle+yaw)
        box(id+'_Upper_Level','Warm_Display',u-direction[0]*.27,v-direction[1]*.27,1.30,[length,.06,.49],'photo_lightwood',yaw=angle+yaw)
        box(id+'_Warm_Underlight','Warm_Display',u-direction[0]*.06,v-direction[1]*.06,1.28,[length-.06,.02,.025],'light_warm',yaw=angle+yaw,collider=False)

    counter('Rear_Stepped_Warm_Case',4.04,2.50,5.44)
    counter('West_Return_Stepped_Warm_Case',.60,3.76,2.25,yaw=90)

    def pan_food(index,u,v,y,yaw=0):
        lib('GN_Pan_'+str(index),'GNPans_and_Covers','GN_stainless_pan',u,v,y,'stainless',yaw=angle+yaw)
        lib('Clear_Domed_Cover_'+str(index),'GNPans_and_Covers','transparent_domed_cover',u,v,y+.065,'clear_cover',yaw=angle+yaw)
        # Fine removable wire rack, real mesh rather than a dark empty tray.
        for j in range(8):
            off=-.22+j*.062
            c,s=math.cos(math.radians(yaw)),math.sin(math.radians(yaw))
            box(f'Rack_{index}_{j}','GNPans_and_Covers',u+c*off,v-s*off,y+.022,[.003,.003,.34],'stainless',yaw=angle+yaw,collider=False)
        for j in range(5):
            row=(j//3)*.11-.05;col=(j%3)*.145-.145
            c,s=math.cos(math.radians(yaw)),math.sin(math.radians(yaw))
            uu=u+c*col+s*row;vv=v-s*col+c*row
            kind=['fishcake_bar','fishcake_roll','croquette','fishcake_bar','fishcake_ball'][index%5]
            mat='seaweed' if index%7==3 else ('fried_brown' if index%3==2 else 'fried_golden')
            lib(f'Actual_Fishcake_{index}_{j}','Fishcake_Merchandise',kind,uu,vv,y+.027,mat,yaw=angle+yaw+(j%2)*7)
            if index%5==4:
                cyl(f'Fishcake_Skewer_{index}_{j}','Fishcake_Merchandise',uu,vv-.045,y+.047,.004,.25,'bamboo',euler=[90,angle+yaw,0])
        # Every few covers carry genuine current product-card printing on an actual solid.
        if index%2==0:
            du=math.sin(math.radians(yaw))*(-.07);dv=math.cos(math.radians(yaw))*(-.07)
            box('Product_Card_Body_'+str(index),'Real_Printed_Pan_Cards',u+du,v+dv,y+.40,[.26,.36,.012],'white_plastic',yaw=angle+yaw,collider=False)
            lib('Source_Printed_Product_Card_'+str(index),'Real_Printed_Pan_Cards','printed_card_plane',u+du+math.sin(math.radians(yaw))*.008,v+dv+math.cos(math.radians(yaw))*.008,y+.40,'menu_'+str((index//2)%4),yaw=angle+yaw)
            cyl('Product_Card_Clip_'+str(index),'Real_Printed_Pan_Cards',u+du,v+dv,y+.18,.025,.07,'clear_cover')
    index=0
    for row in range(2):
        for col in range(8):
            pan_food(index,1.66+col*.68,2.75-row*.50,.942+row*.39);index+=1
    for row in range(2):
        for col in range(4):
            pan_food(index,.84-row*.50,2.85+col*.60,.942+row*.39,90);index+=1

    # Observed checkout: grey flutes/lightwood, tilted screen monitors, actual free payment terminals.
    box('Checkout_Stainless_Toe','Checkout',2.57,5.52,.07,[4.50,.14,.65],'stainless')
    box('Checkout_Fluted_Cabinet','Checkout',2.57,5.52,.48,[4.50,.80,.68],'ribbed_charcoal')
    lib('Checkout_Fluted_Physical_Front','Checkout','fluted_front_1m',2.57,5.867,.48,'ribbed_charcoal',size=[4.50,1.20,1])
    box('Checkout_Thick_Wood_Worktop','Checkout',2.57,5.52,.93,[4.58,.08,.77],'photo_lightwood')
    box('Checkout_Concealed_Warm_Linear_Light','Checkout',2.57,5.898,.89,[4.48,.018,.025],'light_warm',collider=False)
    for i,u in enumerate([1.36,3.48]):
        box(f'Checkout_Monitor_Base_{i}','Checkout',u,5.40,.996,[.30,.045,.22],'white_plastic')
        box(f'Checkout_Monitor_Neck_{i}','Checkout',u,5.38,1.17,[.065,.34,.055],'stainless')
        box(f'Checkout_Tilted_Monitor_{i}','Checkout',u,5.44,1.40,[.45,.34,.050],'white_plastic',euler=[-16,angle,0])
        box(f'Checkout_Source_Screen_{i}','Checkout',u,5.473,1.40,[.404,.292,.010],'screen',euler=[-16,angle,0],collider=False)
        box(f'Checkout_Cash_Drawer_{i}','Checkout',u,5.55,1.01,[.38,.10,.30],'black_rubber')
        # Visible terminal cables are physical cylinders following the own counter top.
        cyl(f'Checkout_Cable_{i}','Checkout',u+.25,5.30,1.00,.009,.40,'black_rubber',euler=[0,angle,90])

    # Licensed downloaded physical equipment imported only under this own author's lease.
    fbx=stage_import(BASE/'free-assets/commercial-refrigerator/CommercialRefrigerator-Empty.fbx','FreeModels/CommercialRefrigerator.fbx')
    stage_import(BASE/'free-assets/commercial-refrigerator/SOURCE-LICENSE.md','FreeModels/CommercialRefrigerator-SOURCE-LICENSE.md')
    material('fridge_black_case',[.023,.028,.032,1],.55,.48)
    material('fridge_interior',[.69,.73,.76,1],.78,.57)
    refrig_map={'FridgeCase':'fridge_black_case','FridgeGlass':'glass','FridgeInterior':'fridge_interior','*':'stainless'}
    for i,(u,v,w,h) in enumerate([(7.00,4.85,.77,2.05),(7.00,3.80,.80,2.10),(7.00,2.70,.68,1.94)]):
        add('asset','Downloaded_Physical_Display_Refrigerator_'+str(i),'Downloaded_Visible_Equipment',u,v,.015,
            fit_size=[w,h,.66],yaw=angle-90,asset=fbx,material_map=refrig_map,
            source_ids=['OCT07_00','OCT07_01','FREE_REFRIGERATOR_CC_BY4'],
            qualification='DOWNLOADED_FREE_REFRIGERATOR_INDEPENDENT_PHOTO_ADAPTATION_NOT_EXACT_MANUFACTURER')
    # No IcyDrinks lettering or champagne textures: every actual imported materialslot gets explicit own URP.
    pos=stage_import(BASE/'free-assets/pos-terminal/POS-Terminal.fbx','FreeModels/POS-Terminal.fbx')
    stage_import(BASE/'free-assets/pos-terminal/ACQUISITION.json','FreeModels/POS-Terminal-ATTRIBUTION.json')
    # Exact source material-slot preservation with own URP PBR maps.
    for n in [0,2,3]:
        stage_import(BASE/f'free-assets/pos-terminal/textures/Image_{n}.png',f'Textures/POS-Image_{n}.png')
    mr=cv2.imread(str(BASE/'free-assets/pos-terminal/textures/Image_1.png'),cv2.IMREAD_UNCHANGED)
    mask=np.zeros((mr.shape[0],mr.shape[1],4),np.uint8)
    mask[:,:,2]=mr[:,:,0];mask[:,:,3]=255-mr[:,:,1]
    cv2.imwrite(str(STAGE/'POS-metallic_smoothness.png'),mask)
    metal=stage_import(STAGE/'POS-metallic_smoothness.png','Textures/POS-metallic_smoothness.png')
    material('pos_source_main',[1,1,1,1],1,.5,texture_asset=ASSET+'/Textures/POS-Image_0.png',normal_asset=ASSET+'/Textures/POS-Image_2.png',metallic_gloss_asset=metal)
    material('pos_source_secondary',[.07,.08,.09,1],.65,.45,normal_asset=ASSET+'/Textures/POS-Image_3.png')
    for i,u in enumerate([1.72,3.88]):add('asset','Actual_Downloaded_Card_POS_'+str(i),'Downloaded_Visible_Equipment',u,5.61,.975,
        fit_size=[.13,.095,.22],asset=pos,material_map={'Material_2100454184':'pos_source_main','Material_2100454185':'pos_source_secondary','*':'pos_source_secondary'},
        source_ids=['OCT07_00','FREE_POS_CC_BY4'],collider=False)

    # Free scanned source is not needed for products: photo fishcake silhouettes are custom editable solids.
    # Small visible prepware uses real downloaded CC0 Kenney assets with explicit own URP slots.
    plate=stage_import(BASE/'free-assets/kenney-food-kit/Models/FBX format/plate.fbx','FreeModels/Plate-CC0.fbx')
    pot=stage_import(BASE/'free-assets/kenney-food-kit/Models/FBX format/pot.fbx','FreeModels/Pot-CC0.fbx')
    spoon=stage_import(BASE/'free-assets/kenney-food-kit/Models/FBX format/utensil-spoon.fbx','FreeModels/Spoon-CC0.fbx')
    stage_import(BASE/'free-assets/kenney-food-kit/License.txt','FreeModels/Kenney-Food-Kit-License.txt')
    for i in range(6):add('asset','Downloaded_Stacked_Plate_'+str(i),'Visible_Prep',4.15,.62,.96+i*.018,asset=plate,fit_size=[.27,.025,.27],material_map={'*':'white_plastic'},collider=False,source_ids=['OCT12_01','FREE_KENNEY_CC0'])
    add('asset','Downloaded_Visible_Stainless_Prep_Pot','Visible_Prep',5.15,.57,.96,asset=pot,fit_size=[.30,.23,.26],material_map={'*':'stainless'},collider=False,source_ids=['OCT12_01','FREE_KENNEY_CC0'])
    add('asset','Downloaded_Visible_Service_Spoon','Visible_Prep',5.48,.64,.98,asset=spoon,fit_size=[.05,.025,.24],material_map={'*':'stainless'},collider=False,source_ids=['OCT12_09','FREE_KENNEY_CC0'])

    box('Visible_Stainless_Preparation_Table','Visible_Prep',4.35,.56,.90,[3.75,.075,.69],'stainless')
    for u in [2.57,6.13]:
        for v in [.28,.84]:cyl('Prep_Table_Leg_'+str(u)+'_'+str(v),'Visible_Prep',u,v,.44,.045,.86,'stainless')
    box('Source_Visible_Stainless_Hood','Visible_Prep',4.35,.33,2.12,[3.0,.36,.46],'stainless')
    box('Source_Visible_Hood_Lower_Lip','Visible_Prep',4.35,.55,1.94,[3.1,.065,.12],'brushed_aluminium')
    for i in range(9):box('Hood_Filter_'+str(i),'Visible_Prep',3.08+i*.32,.565,2.03,[.24,.15,.018],'brushed_aluminium')
    # Only appliances actually visible through the source service aperture.
    cyl('Source_Visible_Black_Wall_Fan','Visible_Prep',3.0,.23,2.37,.37,.10,'black_rubber',euler=[90,angle,0])
    cyl('Fan_Centre_Hub','Visible_Prep',3.0,.29,2.37,.075,.115,'brushed_aluminium',euler=[90,angle,0])
    for i in range(12):
        th=i*math.pi/6
        box('Fan_Grille_Spoke_'+str(i),'Visible_Prep',3+.09*math.cos(th),.296,2.37+.09*math.sin(th),[.19,.005,.005],'brushed_aluminium',euler=[0,angle,-i*30],collider=False)
    box('Source_Visible_White_Wall_Appliance','Visible_Prep',6.16,.26,2.42,[.44,.61,.24],'white_plastic')
    box('Source_Visible_Wall_Controller','Visible_Prep',5.64,.20,2.57,[.26,.27,.075],'white_plastic')

    # Physical tongs beside visible warm pans; source-specific functional form.
    for i,(u,v) in enumerate([(1.39,2.94),(.84,4.55),(4.65,5.54)]):
        for side in [-1,1]:
            box(f'Stainless_Tong_Arm_{i}_{side}','Smallwares',u+side*.023,v,.993,[.012,.014,.24],'stainless',yaw=angle+side*7,collider=False)
            box(f'Stainless_Tong_Grip_{i}_{side}','Smallwares',u+side*.032,v+.13,.993,[.043,.015,.065],'stainless',yaw=angle+side*7,collider=False)
        cyl(f'Tong_Spring_Hinge_{i}','Smallwares',u,v-.12,.996,.045,.015,'stainless')

    # Actual source black wire bag shelf on public-left inside the storefront.
    for u in [6.76,7.35]:
        for v in [5.66,6.14]:box('Gift_Shelf_Post_'+str(u)+'_'+str(v),'Gift_Shelf',u,v,.80,[.023,1.58,.023],'fascia')
    for j in range(4):
        y=.15+j*.37
        box('Gift_Shelf_Level_'+str(j),'Gift_Shelf',7.055,5.90,y,[.65,.025,.56],'fascia')
        for i in range(3):
            u=6.83+i*.23
            box(f'Physical_Gift_Bag_{j}_{i}','Gift_Merchandise',u,5.91,y+.15,[.19,.29,.11],'paper')
            # Gift packaging remains a physical bag volume; handle is separately modeled.
            for side in [-1,1]:box(f'Gift_Handle_{j}_{i}_{side}','Gift_Merchandise',u+side*.04,5.92,y+.33,[.009,.11,.009],'fascia',collider=False)
            box(f'Gift_Handle_Cross_{j}_{i}','Gift_Merchandise',u,5.92,y+.385,[.09,.009,.009],'fascia',collider=False)

    # A source photo print stays surface information on the real physical poster panel.
    front=cv2.imread(str(OWN/'sources/ROOT-dhkstn6051-224434124410-00.jpg'))
    poster=front[670:783,433:538];cv2.imwrite(str(STAGE/'source-checkout-poster.png'),poster)
    tex=stage_import(STAGE/'source-checkout-poster.png','Textures/source-checkout-poster.png')
    material('checkout_real_poster',[1,1,1,1],0,.25,texture_asset=tex)
    for i,u in enumerate([.70,1.65,2.60,3.55,4.36]):
        box('Checkout_Poster_Physical_Panel_'+str(i),'Printed_Source_Information',u,5.908,.50,[.49,.53,.012],'paper',collider=False)
        lib('Checkout_Actual_Poster_Print_'+str(i),'Printed_Source_Information','printed_card_plane',u,5.917,.50,'checkout_real_poster',size=[1.85,1.42,1])

    # Source track and recessed lights. Own light intensity only; no global rendering changes.
    for v in [1.40,4.50]:
        edge('Interior_Light_Track_'+str(v),'Lighting',(.65,v),(W-.65,v),3.08,.025,.025,'stainless')
        for i in range(6):
            u=1+i*1.1;cyl('Track_Light_Housing_'+str(v)+'_'+str(i),'Lighting',u,v,2.99,.065,.16,'stainless')
            cyl('Track_Light_Lens_'+str(v)+'_'+str(i),'Lighting',u,v,2.905,.055,.012,'light_warm')
    for i,(u,v) in enumerate([(2,2.6),(5.6,2.6),(2.2,5.1),(6,5.1)]):
        add('light','Observed_Interior_Warm_Light_'+str(i),'Lighting',u,v,2.83,color=[1,.87,.66,1],range=3.1,intensity=.42)

    p['construction_limits']=['Exact source apparatus maker/model and fielddimensions unknown. Independently photo-fitted models.',
        'Visible prep equipment only, hidden BOH utilities and room functions unqualified.',
        'Downloaded source fridge/POS shape near-match; no source manufacturer identity asserted.',
        'Actual branch geometry not claimed survey-equal, effective emergency simulation or entire2F completion.']
    save('STORE-PACKET-R25.json',p)
    template=(BASE/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text()
    for a,b in {'__STORE_KEY__':'hwang-2f','__WORKER_THREAD__':'classic:/root/r25_hwang_2f','__EXACT_SCOPE__':'C180_R25_HWANG_2F_APPLY_001','__PACKET_PATH__':str(OWN/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha(OWN/'STORE-PACKET-R25.json')}.items():template=template.replace(a,b)
    # Own copy supports source mesh UV; shared stable template remains untouched.
    needle='m.triangles=spec["triangles"].Select(v=>(int)v).ToArray();m.RecalculateNormals();'
    replacement='m.triangles=spec["triangles"].Select(v=>(int)v).ToArray();if(spec["uv"]!=null)m.uv=spec["uv"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();m.RecalculateNormals();'
    if needle not in template:raise RuntimeError('Own library UV insertion target absent')
    template=template.replace(needle,replacement,1)
    (OWN/'ApplyStoreR25.cs').write_text(template)
    print(json.dumps({'objects':len(objects),'imports':len(imports),'materials':len(mats),'mesh_prototypes':len(meshes),'width':W,'depth':D,'yaw':angle,'packet_sha256':sha(OWN/'STORE-PACKET-R25.json')}))

if __name__=='__main__':build()
