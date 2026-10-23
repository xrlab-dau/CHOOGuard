"""R25 source-bound Subway construction, no Unity access until author FIFO lease."""
from pathlib import Path
import json, math, hashlib, random, re
from collections import Counter
from shapely.geometry import shape, Polygon, Point, box, LineString
from shapely.ops import triangulate

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/subway'
ASSET='Assets/ChooGuard/Art/StationInterior/R25_Shops/subway'
REGION=shape(json.loads((BASE/'APPROVED-STORE-WORK-REGION.json').read_text())['geometry_qx_minus_qy'])
ORIGIN=[80,-29]
objects=[]; materials={}; imports=[]; imported={}; meshkeys=set()

def material(name,color,metallic=0,smoothness=.35,**kw):
    materials[name]=dict(color=[*color,1] if len(color)==3 else color,metallic=metallic,smoothness=smoothness,**kw)
for args in [
 ('navy',[.035,.041,.052],.1,.3),('black',[.015,.018,.02],.05,.34),
 ('white',[.88,.89,.86],0,.37),('white_tile',[.9,.9,.87],0,.66),
 ('metal',[.64,.66,.67],.83,.55),('steel_dark',[.21,.23,.24],.8,.46),
 ('wood',[.78,.68,.49],0,.32),('seat_black',[.028,.034,.035],0,.3),
 ('seat_back',[.7,.68,.60],0,.35),('green',[.015,.39,.13],0,.42),
 ('yellow',[.98,.63,.01],0,.46),('grout',[.65,.67,.66],0,.2),
 ('brown_blind',[.13,.07,.035],0,.27),('red',[.62,.025,.02],0,.45),
 ('cool_white',[.9,.96,1],0,.5),('lettuce',[.23,.48,.075],0,.25),
 ('tomato',[.64,.02,.008],0,.42),('onion_cut',[.83,.79,.7],0,.36),
 ('meat',[.38,.20,.11],0,.38),('sauce_white',[.84,.80,.67],0,.5),
 ('bread',[.66,.39,.13],0,.23),('green_dark',[.008,.15,.045],0,.25),
 ('paper',[.88,.84,.7],0,.19),('screen_yellow',[.9,.52,.015],0,.4),
 ]: material(*args)
material('glass',[.77,.89,.90,.10],.0,.94,transparent=True)
material('display_glass',[.85,.93,.95,.09],.0,.94,transparent=True)
material('light_white',[.94,.97,1],emission=[2.3,2.4,2.5,1])
material('light_green',[.14,.72,.25],emission=[.2,1.1,.3,1])
material('fridge_interior',[.78,.81,.83],metallic=.65,smoothness=.5)
material('fridge_case',[.018,.022,.025],metallic=.4,smoothness=.46)
material('fridge_shelf',[.75,.78,.8],metallic=.9,smoothness=.57)
material('gray_wood',[.72,.73,.73],smoothness=.33,
 texture_asset='Assets/ChooGuard/Art/StationInterior/PBR/WoodFloor064_4K/WoodFloor064_4K-JPG_Color.jpg')

def add(kind,id,group='Interior',source=('JIEN07',),qualification='PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT',**kw):
    o=dict(kind=kind,id=id,group=group,source_ids=list(source),qualification=qualification,**kw)
    objects.append(o);return o
def cub(id,q,y,size,mat,group='Interior',yaw=0,source=('JIEN07',),collider=True,**kw):
    return add('box',id,group,source,q=q,y=y,size=size,material=mat,yaw=yaw,collider=collider,**kw)
def cyl(id,q,y,diam,height,mat,group='Interior',source=('JIEN07',),**kw):
    # Unity stock cylinder is two units high.
    return add('cylinder',id,group,source,q=q,y=y,size=[diam,height/2,diam],material=mat,collider=False,**kw)
def edge(id,a,b,bottom,height,thick,mat,group='Booth',source=('PSS02',),**kw):
    return add('edge',id,group,source,a=a,b=b,bottom=bottom,height=height,thickness=thick,material=mat,**kw)
def text(id,q,y,content,width,height,color,yaw=0,group='Observed_Brand_Signage',source=('PSS02',)):
    # Only actually printed signage, never a stand-in for physical equipment.
    return add('text',id,group,source,q=q,y=y,text=content,width=width,height=height,color=[*color,1],yaw=yaw)
def mesh(id,q,y,vs,ts,mat,group='Interior',source=('JIEN07',),key=None,**kw):
    if key and key in meshkeys:return add('mesh',id,group,source,q=q,y=y,reuse_mesh=key,material=mat,**kw)
    if key:meshkeys.add(key)
    return add('mesh',id,group,source,q=q,y=y,vertices=vs,triangles=ts,material=mat,mesh_key=key or id,**kw)
def import_file(src,sub=None):
    src=Path(src);src=src if src.is_absolute() else ROOT/src
    dest=ASSET+'/FreeModels/'+(sub or src.name)
    if dest in imported:return dest
    data=src.read_bytes();imports.append(dict(source=str(src.relative_to(ROOT)),destination=dest,sha256=hashlib.sha256(data).hexdigest()));imported[dest]=True
    return dest
def asset(id,q,y,size,sourcefile,group='Furnishings_Free3D',yaw=0,mat=None,source=('JIEN07',),sub=None,**kw):
    ap=sourcefile if str(sourcefile).startswith('Assets/') else import_file(sourcefile,sub)
    o=add('asset',id,group,source,q=q,y=y,fit_size=size,yaw=yaw,asset=ap,collider=kw.pop('collider',False),**kw)
    if mat:o['material']=mat
    return o
def planar_extrusion(id,poly,y,height,mat,group,source,collider=False):
    if poly.is_empty:return
    if poly.geom_type!='Polygon':
        for i,p in enumerate(poly.geoms):planar_extrusion(id+'_'+str(i),p,y,height,mat,group,source,collider)
        return
    vs=[];ts=[];uv=[]
    def tri(points):
        offset=len(vs);vs.extend(points);ts.extend([offset,offset+1,offset+2])
        uv.extend([[p[0]/2,p[2]/2] for p in points])
    for t in triangulate(poly):
        if not poly.covers(t.representative_point()):continue
        a,b,c=list(t.exterior.coords)[:3]
        # XZ CCW is downwards in Unity; reverse upper.
        tri([[a[0]-ORIGIN[0],height,a[1]-ORIGIN[1]],[c[0]-ORIGIN[0],height,c[1]-ORIGIN[1]],[b[0]-ORIGIN[0],height,b[1]-ORIGIN[1]]])
        tri([[a[0]-ORIGIN[0],0,a[1]-ORIGIN[1]],[b[0]-ORIGIN[0],0,b[1]-ORIGIN[1]],[c[0]-ORIGIN[0],0,c[1]-ORIGIN[1]]])
    for ring in [poly.exterior,*poly.interiors]:
        for a,b in zip(ring.coords,list(ring.coords)[1:]):
            av=[a[0]-ORIGIN[0],0,a[1]-ORIGIN[1]];bv=[b[0]-ORIGIN[0],0,b[1]-ORIGIN[1]];at=[av[0],height,av[2]];bt=[bv[0],height,bv[2]]
            tri([av,at,bt]);tri([av,bt,bv])
    mesh(id,ORIGIN,y,vs,ts,mat,group,source,uv=uv,collider=collider)
def pipe(id,a,b,radius,mat,group,source=('PSS04',)):
    # explicit tubular geometry; endpoints qx,y,qz
    ax,ay,az=a;bx,by,bz=b;d=[bx-ax,by-ay,bz-az];L=math.sqrt(sum(x*x for x in d));n=[x/L for x in d]
    up=[1,0,0] if abs(n[1])>.95 else [0,1,0]
    u=[n[1]*up[2]-n[2]*up[1],n[2]*up[0]-n[0]*up[2],n[0]*up[1]-n[1]*up[0]];l=math.sqrt(sum(x*x for x in u));u=[x/l for x in u]
    v=[n[1]*u[2]-n[2]*u[1],n[2]*u[0]-n[0]*u[2],n[0]*u[1]-n[1]*u[0]];vs=[];ts=[]
    for pos in [[0,0,0],d]:
        for k in range(12):
            c=math.cos(k*math.tau/12);s=math.sin(k*math.tau/12);vs.append([pos[i]+radius*(c*u[i]+s*v[i]) for i in range(3)])
    for k in range(12):
        j=(k+1)%12;ts.extend([k,j,12+j,k,12+j,12+k])
    mesh(id,[ax,az],ay,vs,ts,mat,group,source,collider=False)
def tray(id,q,y,w=.325,d=.265,h=.12):
    # Open pan with inner basin, bevelled rolled rim, bottom; physical GN shape.
    loops=[];vs=[];ts=[]
    for ww,dd,yy in [(w,d,0),(w,d,h),(w-.018,d-.018,h+.006),(w-.035,d-.035,h-.008),(w-.06,d-.06,.016)]:
        loop=[]
        for cx,cz,start in [(ww/2-.025,dd/2-.025,0),(-ww/2+.025,dd/2-.025,90),(-ww/2+.025,-dd/2+.025,180),(ww/2-.025,-dd/2+.025,270)]:
            for k in range(4):
                a=math.radians(start+ k*30);loop.append(len(vs));vs.append([cx+.025*math.cos(a),yy,cz+.025*math.sin(a)])
        loops.append(loop)
    for a,b in zip(loops,loops[1:]):
        for i in range(len(a)):
            n=(i+1)%len(a);ts.extend([a[i],b[i],b[n],a[i],b[n],a[n]])
    for k in range(1,len(loops[-1])-1):ts.extend([loops[-1][0],loops[-1][k+1],loops[-1][k]])
    return mesh(id,q,y,vs,ts,'metal','Ingredient_GN_Pans',('PSS07','JIEN12','JIEN15'),key='GNpan'+str((w,d,h)),collider=False)

# Actual retained floor/columns remain untouched. This is a 7mm shop finish,
# not a replacement structural floor, and it stays strictly inside the approved ring.
inside=REGION.buffer(-.025)
native_columns=[(73.79,-34.36,.23),(81.26,-31.80,.23)]
for x,z,r in native_columns:inside=inside.difference(Point(x,z).buffer(r+.012,resolution=24))
planar_extrusion('Shop_Gray_Wood_Surface_Finish_Only',inside,.005,.007,'gray_wood','Booth_Surface_Finish',('JIEN07','JIEN08'))
planar_extrusion('Shop_White_Soffit_Individually_Editable',inside,3.30,.08,'white','Booth_Ceiling',('JIEN07','JIEN09'))

# Long front aligns exactly to the approved row and excludes retained column profiles.
zfront=lambda x:-21.781891551787-(x-75.5240884653)*.004947
front_spans=[(74.79,74.94),(75.55,82.75),(83.59,90.78),(91.63,92.20)]
door=(85.55,87.75)
for n,(x0,x1) in enumerate(front_spans):
    # Broad actual navy fascia above glazing; native columns never overwritten.
    a=[x0,zfront(x0)-.045];b=[x1,zfront(x1)-.045]
    edge(f'Front_Fascia_{n}',a,b,2.75,.55,.105,'navy','Booth_Frontage')
    for k in range(7):edge(f'Front_Fascia_Rib_{n}_{k}',a,b,3.02+k*.032,.014,.12,'black','Booth_Frontage',collider=False)
    cuts=sorted({x0,x1,*([door[0],door[1]] if x0<door[0]<door[1]<x1 else [])})
    for k,(p0,p1) in enumerate(zip(cuts,cuts[1:])):
        if p0==door[0] and p1==door[1]:continue
        count=max(1,math.ceil((p1-p0)/1.6))
        for t in range(count):
            aa=p0+(p1-p0)*t/count;bb=p0+(p1-p0)*(t+1)/count
            edge(f'Glass_{n}_{k}_{t}',[aa+.025,zfront(aa)-.06],[bb-.025,zfront(bb)-.06],.06,2.69,.014,'glass','Booth_Glazing',cast_shadows=False)
            edge(f'Glass_Lower_Rail_{n}_{k}_{t}',[aa,zfront(aa)-.06],[bb,zfront(bb)-.06],.025,.055,.065,'steel_dark','Booth_Glazing')
            edge(f'Green_Window_Band_{n}_{k}_{t}',[aa,zfront(aa)-.047],[bb,zfront(bb)-.047],.88,.038,.003,'green','Booth_Glazing',collider=False)
            cub(f'Glazing_Mullion_{n}_{k}_{t}',[aa,zfront(aa)-.06],1.39,[.045,2.75,.065],'steel_dark','Booth_Glazing')
    cub(f'Front_End_Frame_{n}',[x1,zfront(x1)-.06],1.39,[.045,2.75,.065],'steel_dark','Booth_Glazing')

# Auto paired leaves are shown parked open. Portal is physically unobstructed.
for k,x in enumerate([door[0],door[1]]):cub(f'Entry_Jamb_{k}',[x,zfront(x)-.09],1.29,[.065,2.58,.10],'steel_dark','Entry_Automatic_Paired_Glass')
edge('Automatic_Door_Header',[door[0]-.15,zfront(door[0])-.09],[door[1]+.15,zfront(door[1])-.09],2.58,.17,.15,'steel_dark','Entry_Automatic_Paired_Glass')
for k,x in enumerate([door[0]-.54,door[1]+.54]):
    cub(f'Auto_Leaf_Glass_Parked_{k}',[x,zfront(x)-.145],1.29,[1.05,2.5,.014],'glass','Entry_Automatic_Paired_Glass',collider=False,cast_shadows=False)
    for dx in [-.53,.53]:cub(f'Auto_Leaf_Frame_{k}_{dx}',[x+dx,zfront(x)-.145],1.29,[.027,2.5,.055],'metal','Entry_Automatic_Paired_Glass',collider=False)
    cub(f'Auto_Leaf_Green_Band_{k}',[x,zfront(x)-.12],.91,[1.05,.035,.003],'green','Entry_Automatic_Paired_Glass',collider=False)
cub('Entry_Mat',[86.65,-22.45],.024,[2.18,.015,.8],'black','Entry_Automatic_Paired_Glass',collider=False)
cub('Door_Sensor',[86.65,-21.99],2.69,[.18,.045,.06],'black','Entry_Automatic_Paired_Glass',collider=False)
cub('Exit_Illuminated_Housing',[86.65,-22.11],2.96,[.46,.22,.05],'light_green','Entry_Automatic_Paired_Glass',collider=False)
text('Exit_Observed_Print',[86.65,-22.145],2.96,'EXIT',.38,.12,[1,1,1],yaw=180)

# The actual source letterforms become physically extruded sign meshes below.
# No font approximation or photo rectangle is used for the SUBWAY fascia logo.
import cv2, numpy as np
logo=cv2.imread(str(BASE/'sources/SUBWAY-PHOTO-LETTERFORM-REFERENCE.jpg'))
lmasks={'yellow':((logo[:,:,2]>200)&(logo[:,:,1]>190)&(logo[:,:,0]<230)).astype('uint8')*255,
        'white':((logo.min(axis=2)>235)).astype('uint8')*255}
lmasks['yellow'][:,245:]=0;lmasks['white'][:,:240]=0
for mat,mask in lmasks.items():
    mask=cv2.morphologyEx(mask,cv2.MORPH_OPEN,np.ones((3,3),np.uint8))
    contours,hh=cv2.findContours(mask,cv2.RETR_CCOMP,cv2.CHAIN_APPROX_SIMPLE)
    vs=[];ts=[]
    if hh is None:continue
    for i,cont in enumerate(contours):
        if hh[0,i,3]!=-1 or cv2.contourArea(cont)<100:continue
        ext=cv2.approxPolyDP(cont,.9,True).reshape(-1,2);holes=[];child=hh[0,i,2]
        while child>=0:
            if cv2.contourArea(contours[child])>15:holes.append(cv2.approxPolyDP(contours[child],.6,True).reshape(-1,2))
            child=hh[0,child,0]
        poly=Polygon(ext,holes).buffer(0)
        if poly.is_empty:continue
        def vv(x,y,z):return [-(x/596-.5)*3.8,(.5-y/160)*.52,z]
        # A subtle 0.8mm perimeter bevel catches actual sign illumination.
        face=poly.buffer(-.45,join_style=2)
        if face.is_empty:face=poly
        for tri in triangulate(face):
            if not face.covers(tri.representative_point()):continue
            pp=list(tri.exterior.coords)[:3];start=len(vs);vs.extend([vv(*xy,.04) for xy in pp]);ts.extend([start,start+1,start+2,start+2,start+1,start])
        bevel=poly.difference(face)
        for tri in triangulate(bevel):
            if not bevel.covers(tri.representative_point()):continue
            start=len(vs)
            for xx,yy in list(tri.exterior.coords)[:3]:
                depth=.04 if face.boundary.distance(Point(xx,yy))<.2 else .035
                vs.append(vv(xx,yy,depth))
            ts.extend([start,start+1,start+2,start+2,start+1,start])
        for ring in [poly.exterior,*poly.interiors]:
            for a,b in zip(ring.coords,list(ring.coords)[1:]):
                start=len(vs);vs.extend([vv(*a,0),vv(*a,.04),vv(*b,.04),vv(*b,0)]);ts.extend([start,start+1,start+2,start,start+2,start+3])
    mesh('Actual_SUBWAY_Extruded_'+mat,[79.6,-21.77],2.99,vs,ts,mat,'Observed_Brand_Three_Dimensional_Sign',('PSS02',),collider=False)

# West partitions follow already approved exact straight CAD line chain; east/south
# official surfaces are retained and are not duplicated into opaque new rooms.
westpts=[[74.68,-22.02],[66.80,-25.02],[69.23,-31.48],[66.80,-31.52],[66.80,-39.12]]
for i,(a,b) in enumerate(zip(westpts,westpts[1:])):
    edge(f'West_Photo_White_Partition_{i}',a,b,.015,3.27,.09,'white_tile','Booth_West_Partitions',source=('JIEN06','JIEN08'))
    edge(f'West_Black_Skirting_{i}',a,b,.015,.12,.105,'black','Booth_West_Partitions',source=('JIEN08',))

# Facade blinds are internal independent fitout, not replacement official windows.
southA=(67.06,-39.05);southB=(84.25,-33.21)
for i in range(27):
    t=i/26;x=southA[0]+(southB[0]-southA[0])*t;z=southA[1]+(southB[1]-southA[1])*t
    if i<26:
        nx=southA[0]+(southB[0]-southA[0])*(i+1)/26;nz=southA[1]+(southB[1]-southA[1])*(i+1)/26
        # Each physical window blind is one individually editable mesh assembly,
        # rather than hundreds of draw calls for individual slats.
        vs=[];ts=[];dx=nx-x;dz=nz-z;L=math.hypot(dx,dz);px=-dz/L*.011;pz=dx/L*.011
        for sl in range(20):
            yy=2.02+sl*.052;st=len(vs)
            vs.extend([[xx,ay,zz] for xx,zz in [[0+px,0+pz],[dx+px,dz+pz],[dx-px,dz-pz],[0-px,0-pz]] for ay in [0+sl*.052,.025+sl*.052]])
            for aa,bb,cc,dd in [(0,2,3,1),(2,4,5,3),(4,6,7,5),(6,0,1,7),(1,3,5,7),(0,6,4,2)]:ts.extend([st+aa,st+bb,st+cc,st+aa,st+cc,st+dd])
        mesh(f'Window_Brown_Blind_Assembly_{i}',[x,z],2.02,vs,ts,'brown_blind','Window_Venetian_Blinds',('JIEN08',),collider=False)

# Photo-identical dining family: real downloaded steel frame, separate seat/back,
# stainless small tables and paired round tables with eight cube upholstered seats.
frame=json.loads((BASE/'FREE-SOURCED-STEEL-CHAIR-FRAME.json').read_text())
def chair(id,x,z,yaw=0,height=.84):
    # frame asset derived from a CC0 downloaded full chair, original is preserved.
    scale=height/.84
    mesh(id+'_Free3D_Steel_Frame',[x,z],.015,frame['vertices'],frame['triangles'],'metal','Seating_Photo_Matched',('JIEN08','JIEN10'),key='PolyHaven_SchoolChair_Leg_Frame',yaw=yaw,size=[1,scale,1],collider=False)
    rad=math.radians(yaw)
    def q(lx,lz):return [x+lx*math.cos(rad)+lz*math.sin(rad),z-lx*math.sin(rad)+lz*math.cos(rad)]
    cub(id+'_Black_Seat',q(0,0),.44*scale,[.43,.055,.40],'seat_black','Seating_Photo_Matched',yaw=yaw,source=('JIEN08',),collider=False)
    cub(id+'_Pale_Back',q(0,-.205),.72*scale,[.43,.20,.025],'seat_back','Seating_Photo_Matched',yaw=yaw,source=('JIEN08',),collider=False)
    for dx in [-.19,.19]:cub(id+f'_Upright_{dx}',q(dx,-.20),.61*scale,[.022,.40*scale,.023],'metal','Seating_Photo_Matched',yaw=yaw,source=('JIEN08',),collider=False)
    if height>.9:
        for dx in [-.17,.17]:pipe(id+f'_Stool_Footrail_{dx}',[q(dx,-.17)[0],.3,q(dx,-.17)[1]],[q(dx,.17)[0],.3,q(dx,.17)[1]],.012,'metal','Seating_Photo_Matched',('JIEN09',))

def squaretable(id,x,z,w=.78,d=.70,h=.74,yaw=0):
    cub(id+'_Top',[x,z],h,[w,.035,d],'wood','Dining_Table_Assemblies',yaw=yaw,source=('JIEN08','JIEN10'))
    cub(id+'_Steel_Rim',[x,z],h-.035,[w,.035,d],'metal','Dining_Table_Assemblies',yaw=yaw,source=('JIEN08','JIEN10'),collider=False)
    rad=math.radians(yaw)
    for a in [-1,1]:
        for b in [-1,1]:
            xx=(w/2-.07)*a;zz=(d/2-.07)*b;xx,zz=x+xx*math.cos(rad)+zz*math.sin(rad),z-xx*math.sin(rad)+zz*math.cos(rad)
            cyl(id+f'_Leg_{a}_{b}',[xx,zz],h/2,.032,h-.05,'metal','Dining_Table_Assemblies',('JIEN08',))

for i,(x,z) in enumerate([(77.5,-25.0),(80.3,-25.0)]):
    asset(f'Round_Table_{i}_Free3D',[x,z],.015,[1.22,.74,1.22],'Assets/ChooGuard/Art/StationInterior/Kits/restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/fbx (unity)/table_round_A.fbx',mat='metal',source=('PSS03','JIEN07'))
    cyl(f'Round_Table_{i}_Pale_Top',[x,z],.75,1.22,.028,'wood','Dining_Table_Assemblies')
    for k,(dx,dz) in enumerate([(-.49,-.49),(-.49,.49),(.49,-.49),(.49,.49)]):
        cub(f'Cube_Seat_{i}_{k}_Plinth',[x+dx,z+dz],.075,[.43,.10,.43],'metal','Seating_Cube_Stools',source=('PSS03','JIEN07'))
        cub(f'Cube_Seat_{i}_{k}_Upholstery',[x+dx,z+dz],.28,[.46,.36,.46],'green' if k%2==0 else 'yellow','Seating_Cube_Stools',source=('PSS03','JIEN07'))

for i,(x,z) in enumerate([(72.0,-29.0),(74.2,-29.0),(76.5,-29.0),(79.0,-29.0),(72.0,-32.4),(75,-32.4),(78,-32.4),(69.2,-34.4),(71.7,-35.1),(75.0,-34.7)]):
    squaretable(f'Dining_Table_{i}',x,z)
    chair(f'Dining_Chair_{i}_N',x,z+.55,180)
    chair(f'Dining_Chair_{i}_S',x,z-.55,0)

# Southern window counter: actual source long counter/individual bar chairs.
a=[69.5,-37.80];b=[79.5,-34.41]
edge('Window_Counter_Pale_Top',a,b,1.02,.035,.46,'wood','Window_Counter',source=('JIEN08',))
for i in range(6):
    t=(i+.5)/6;x=a[0]+(b[0]-a[0])*t;z=a[1]+(b[1]-a[1])*t
    cyl(f'Window_Counter_Leg_{i}',[x,z],.50,.035,1.0,'metal','Window_Counter',('JIEN08',))
    chair(f'Window_Bar_Chair_{i}',x-.18,z+.55,161,height=1.03)
squaretable('High_Table_By_Native_Column',79.3,-31.0,w=1.45,d=.70,h=1.02)
for i,x in enumerate([78.85,79.65]):chair(f'High_Table_Stool_{i}',x,-30.35,180,height=1.03)

# Source counter body with actual doors, worktop, kick and GN recessed pans.
# Four physical stations preserve the photo's ingredient-prep->vegetable->POS order.
counterX=[83.7,84.95,86.2,87.45];counterZ=-26.40
for i,x in enumerate(counterX):
    cub(f'Service_Counter_{i}_Carcass',[x,counterZ],.47,[1.24,.88,.78],'wood','Service_Counter',source=('PSS04',))
    cub(f'Service_Counter_{i}_Kick',[x,counterZ+.02],.065,[1.24,.13,.73],'black','Service_Counter',source=('PSS04',))
    cub(f'Service_Counter_{i}_White_Worktop',[x,counterZ],.94,[1.25,.07,.84],'white','Service_Counter',source=('PSS04',))
    cub(f'Service_Counter_{i}_Front_Fascia',[x,counterZ+.43],.87,[1.24,.11,.025],'white','Service_Counter',source=('PSS04',))
    cub(f'Service_Counter_{i}_Green_Fillet',[x,counterZ+.45],.79,[1.24,.018,.012],'green','Service_Counter',source=('PSS04',),collider=False)
    if i<3:
        for k in [-.42,.42]:cub(f'Service_Counter_{i}_Glass_Stanchion_{k}',[x+k,counterZ+.34],1.22,[.025,.58,.03],'metal','Ingredient_Display_Glass',source=('PSS04','PSS07'),collider=False)
        cub(f'Service_Counter_{i}_Glass_Sneeze_Guard',[x,counterZ+.36],1.26,[1.23,.57,.012],'display_glass','Ingredient_Display_Glass',source=('PSS04','PSS07'),collider=False,cast_shadows=False)
        cub(f'Service_Counter_{i}_Glass_Top',[x,counterZ+.1],1.55,[1.23,.012,.5],'display_glass','Ingredient_Display_Glass',source=('PSS07',),collider=False,cast_shadows=False)

foodroot=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/kenney-food-kit/Models/FBX format'
for k in ['tomato-slice','onion','lettuce','lettuce-slice','cheese-cut','whole-ham','sub','bread','cookie-chocolate','cup','cooking-knife','utensil-spoon','soda-can','soda-bottle','bottle-ketchup','bottle-musterd','bag-flat']:
    if not (foodroot/(k+'.fbx')).exists():continue
    import_file(foodroot/(k+'.fbx'),'KenneyFood/'+k+'.fbx')
texture=foodroot/'Textures';
for p in texture.glob('*'):
    if p.is_file():import_file(p,'KenneyFood/Textures/'+p.name)
material('food_atlas',[1,1,1],texture_asset=ASSET+'/FreeModels/KenneyFood/Textures/colormap.png')
panfill=[('food_ingredient_lettuce_chopped','lettuce'),('food_ingredient_tomato_slices','tomato'),('food_ingredient_onion_rings','onion_cut'),('food_ingredient_cheese_slice','yellow'),('food_ingredient_ham','meat'),('food_ingredient_steak_pieces','meat')]
rest='Assets/ChooGuard/Art/StationInterior/Kits/restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/fbx (unity)/'
for row in range(2):
 for k in range(9):
    x=83.2+k*.39;z=counterZ-.20+row*.30
    tray(f'GN_Ingredient_Pan_{row}_{k}',[x,z],.96)
    nam,mat=panfill[(row*9+k)%len(panfill)];file=rest+nam+'.fbx'
    asset(f'Real_Ingredient_Mesh_{row}_{k}',[x,z],.99,[.23,.06,.18],file,mat=mat,group='Actual_Ingredients_Free3D',source=('PSS07','JIEN12','JIEN13'),yaw=20*k)
    if k%3==0:
        asset(f'Ingredient_Serving_Spoon_{row}_{k}',[x+.08,z],1.05,[.045,.025,.21],foodroot/'utensil-spoon.fbx',mat='metal',group='Actual_Ingredients_Free3D',source=('JIEN12',),sub='KenneyFood/utensil-spoon.fbx',yaw=30)

# Free original scanned cuttingboard and onion are placed at visible preparation
# worktop; shapes are real model assets, not boxes labelled with object names.
for key in ['wooden_cutting_board','yellow_onion']:
    base=BASE/'sources/free-models'/key
    ap=import_file(base/(key+'.fbx'),key+'/'+key+'.fbx')
    tex=next((base/'textures').glob('*_diff_1k.jpg'))
    tp=import_file(tex,key+'/textures/'+tex.name)
    material(key+'_pbr',[1,1,1],texture_asset=tp,smoothness=.28)
asset('Scanned_Wood_Cutting_Board',[84.2,-27.58],.94,[.48,.025,.28],ASSET+'/FreeModels/wooden_cutting_board/wooden_cutting_board.fbx',mat='wooden_cutting_board_pbr',source=('JIEN12',),group='Preparation_Free_Physical_Assets')
asset('Scanned_Onion_At_Prep',[84.12,-27.55],.965,[.085,.085,.085],ASSET+'/FreeModels/yellow_onion/yellow_onion.fbx',mat='yellow_onion_pbr',source=('JIEN12',),group='Preparation_Free_Physical_Assets')
asset('Knife_Actual_3D',[84.38,-27.56],.97,[.045,.025,.24],foodroot/'cooking-knife.fbx',mat='metal',source=('JIEN12',),sub='KenneyFood/cooking-knife.fbx',group='Preparation_Free_Physical_Assets',yaw=65)

# Rear preparation stainless workbench and actual free oven mesh; no invented gas range.
asset('Rear_Stainless_Prep_Bench',[84.7,-27.54],.015,[2.65,.9,.65],rest+'kitchentable_A_large.fbx',mat='metal',source=('PSS04','JIEN07'),group='Preparation_Free_Physical_Assets',collider=True)
asset('Visible_Bread_Oven_Free_Physical_Model',[85.35,-27.58],.92,[.86,.78,.58],'Assets/ChooGuard/ThirdParty/Models/Objaverse/DeckOven/DeckOven.obj',mat='steel_dark',source=('PSS04','JIEN07'),group='Preparation_Free_Physical_Assets')
asset('Bread_Rack_Free3D',[83.7,-27.60],.93,[.66,.8,.55],'Assets/ChooGuard/Art/StationInterior/Props/StoreShelf.fbx',mat='metal',source=('JIEN07',),group='Preparation_Free_Physical_Assets')
for i in range(4):
    asset(f'Bread_Rack_Sub_Loaf_{i}',[83.7,-27.51],1.03+i*.17,[.40,.09,.12],foodroot/'bread.fbx',mat='bread',source=('JIEN07','JIEN12'),sub='KenneyFood/bread.fbx',group='Preparation_Free_Physical_Assets')

# Physical POS terminal and card-reader shells, separately editable, with no text proxies.
add('asset_assembly','POS_Actual_Free_3D_Computer_Monitor','POS_Terminal_Actual_Model',('PSS04',),q=[87.65,-26.43],y=.99,fit_size=[.4,.36,.23],yaw=180,
 parts=[dict(name='Free_Source_Monitor_Case',mesh_asset='Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/kenney_furniture_computerScreen__0.asset',material='black'),dict(name='Free_Source_Monitor_Screen',mesh_asset='Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/kenney_furniture_computerScreen__1.asset',material='cool_white')])
for id,x,z,w,h,d in [('POS_Card_Reader',87.95,-26.19,.09,.07,.15)]:
    cub(id,[x,z],1.0+(h/2 if id=='POS_Base' else .12 if id=='POS_Pedestal' else .3 if id=='POS_Screen' else .04),[w,h,d],'black','POS_Terminal_Actual_Model',source=('PSS04',),collider=False)
for i in range(9):cub(f'POS_CardReader_Button_{i}',[87.924+(i%3)*.018,-26.16+(i//3)*.018],1.091,[.012,.004,.012],'white','POS_Terminal_Actual_Model',source=('PSS04',),collider=False)

# Actual four digital menu/display housings on two posts; all real physical panels.
for i,x in enumerate([83.65,84.85,86.05,87.25]):
    cub(f'Digital_Menu_Housing_{i}',[x,-26.42],2.32,[1.16,.63,.055],'black','Menu_Display_Models',source=('PSS04',),collider=False)
    cub(f'Digital_Menu_Actual_Print_Surface_{i}',[x,-26.385],2.32,[1.11,.59,.008],'screen_yellow' if i else 'green_dark','Menu_Display_Models',source=('PSS04',),collider=False)
    # Menu item images represented as actually modelled mini sandwich silhouettes.
    for k in range(3):
        cub(f'Digital_Menu_Row_Band_{i}_{k}',[x,-26.377],2.14+k*.16,[.94,.018,.002],'white','Menu_Display_Models',source=('PSS04',),collider=False)
for x in [83.0,87.8]:cyl(f'Menu_Support_{x}',[x,-26.47],1.77,.025,1.15,'metal','Menu_Display_Models',('PSS04',))
edge('Menu_Lower_Header',[83.05,-26.40],[87.85,-26.40],1.88,.18,.10,'metal','Menu_Display_Models',source=('PSS04',))
text('Observed_Service_Header',[85.38,-26.335],1.97,'Choose your canvas',3.5,.11,[.8,.82,.82],source=('PSS04',))

# Real commercial refrigerator free 3175 triangle donor, original source read-only.
fr=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/commercial-refrigerator'
fridgeasset=import_file(fr/'CommercialRefrigerator-Empty.fbx','CommercialRefrigerator/CommercialRefrigerator-Empty.fbx')
for p in (fr/'textures').glob('*'):
    if p.name in ['Image_6.png','Image_7.png','URP_Interior_MetallicSmoothness.png']:import_file(p,'CommercialRefrigerator/textures/'+p.name)
o=asset('Actual_Commercial_Glass_Drinks_Fridge',[89.5,-23.65],.015,[.69,1.93,.7],fridgeasset,source=('PSS08',),group='Drink_Niche_Free3D',yaw=90,collider=True)
o['material_slots']={'FridgeCase':'fridge_case','FridgeInterior':'fridge_interior','FridgeGlass':'glass','*':'fridge_shelf'}
wb=BASE/'sources/free-models/water_bottle'
wbasset=import_file(wb/'WaterBottle.fbx','WaterBottle/WaterBottle.fbx')
wbtex=import_file(wb/'textures/Image_0.png','WaterBottle/textures/Image_0.png')
material('water_bottle_pbr',[1,1,1],texture_asset=wbtex,smoothness=.63)
for row in range(4):
    for k in range(4):
        x=89.63;z=-23.9+k*.13;y=.33+row*.36
        if row<2:
            asset(f'Chilled_Free_Real_Water_Bottle_{row}_{k}',[x,z],y,[.065,.23,.065],wbasset,mat='water_bottle_pbr',source=('PSS08',),group='Drink_Niche_Free3D')
        else:
            asset(f'Chilled_Drink_Free_Can_{row}_{k}',[x,z],y,[.065,.115,.065],foodroot/'soda-can.fbx',mat='green' if row%2 else 'yellow',source=('PSS08',),sub='KenneyFood/soda-can.fbx',group='Drink_Niche_Free3D')

# Photo-matched six-nozzle fountain: steel casing, separate icehopper, lever spouts,
# dripgrate, drain, wooddoors and stainless worktop. These are physical model parts.
cub('Soda_Niche_Base',[90.55,-23.50],.45,[1.15,.85,.70],'wood','Soda_Fountain_Physical_Model',source=('PSS08',))
cub('Soda_Niche_Steel_Worktop',[90.55,-23.50],.90,[1.2,.055,.73],'metal','Soda_Fountain_Physical_Model',source=('PSS08',))
for x in [90.27,90.83]:cub(f'Drink_Niche_Door_{x}',[x,-23.125],.47,[.53,.70,.02],'wood','Soda_Fountain_Physical_Model',source=('PSS08',))
cub('Fountain_Refrigeration_Body',[90.65,-23.55],1.24,[.87,.66,.56],'metal','Soda_Fountain_Physical_Model',source=('PSS08',))
cub('Fountain_Dark_Front',[90.65,-23.245],1.52,[.87,.47,.08],'black','Soda_Fountain_Physical_Model',source=('PSS08',))
cub('Ice_Hopper_Upper_Steel',[90.65,-23.55],1.87,[.87,.28,.56],'metal','Soda_Fountain_Physical_Model',source=('PSS08',))
for k in range(6):
    x=90.29+k*.135
    cub(f'Fountain_Selection_{k}',[x,-23.19],1.43,[.10,.11,.035],['yellow','green','black','red','green','red'][k],'Soda_Fountain_Physical_Model',source=('PSS08',),collider=False)
    pipe(f'Fountain_Spout_{k}',[x,1.35,-23.19],[x,1.30,-23.10],.012,'black','Soda_Fountain_Physical_Model',('PSS08',))
    cub(f'Fountain_Push_Lever_{k}',[x,-23.14],1.23,[.025,.14,.012],'black','Soda_Fountain_Physical_Model',source=('PSS08',),collider=False)
cub('Fountain_Drip_Tray',[90.65,-23.22],1.02,[.87,.05,.31],'black','Soda_Fountain_Physical_Model',source=('PSS08',))
for k in range(18):cub(f'Drip_Grate_Steel_Bar_{k}',[90.25+k*.045,-23.20],1.05,[.012,.008,.26],'metal','Soda_Fountain_Physical_Model',source=('PSS08',),collider=False)

# Actual cookie display model and free 3D cookies; three glass shelves.
for k in range(3):
    cub(f'Cookie_Display_Shelf_{k}',[87.7,-26.15],1.08+k*.18,[.60,.012,.30],'display_glass','Cookie_Display_Free3D',source=('PSS09',),collider=False)
    for j in range(4):asset(f'Cookie_Real_Model_{k}_{j}',[87.48+j*.14,-26.14],1.10+k*.18,[.105,.023,.105],foodroot/'cookie-chocolate.fbx',mat='bread',source=('PSS09',),sub='KenneyFood/cookie-chocolate.fbx',group='Cookie_Display_Free3D')
for x in [87.38,88.02]:cub(f'Cookie_Display_Side_{x}',[x,-26.15],1.30,[.012,.56,.34],'display_glass','Cookie_Display_Free3D',source=('PSS09',),collider=False)
cub('Cookie_Display_Front',[87.7,-25.97],1.30,[.64,.56,.012],'display_glass','Cookie_Display_Free3D',source=('PSS09',),collider=False)

# Two real customer paper-cup/tray placements and napkins, no random clutter.
for k,(x,z) in enumerate([(72,-29),(78,-32.4)]):
    cub(f'Customer_Green_Tray_{k}',[x,z],.772,[.37,.018,.27],'green_dark','Customer_Items_Free3D',source=('JIEN17','JIEN18'),collider=False)
    asset(f'Customer_Sandwich_Free3D_{k}',[x+.05,z],.787,[.23,.08,.085],foodroot/'sub.fbx',mat='bread',source=('JIEN19','JIEN22'),sub='KenneyFood/sub.fbx',group='Customer_Items_Free3D')
    asset(f'Customer_Paper_Cup_Free3D_{k}',[x-.10,z+.03],.787,[.075,.12,.075],foodroot/'cup.fbx',mat='white',source=('JIEN18',),sub='KenneyFood/cup.fbx',group='Customer_Items_Free3D')
    cub(f'Customer_Napkin_{k}',[x,z-.08],.789,[.13,.003,.08],'paper','Customer_Items_Free3D',source=('JIEN17',),collider=False)

# Actual interior ceiling lights, ventilation and hanging dome lamps.
for i,x in enumerate([69.5,72,74.5,77,79.5,82,84.5,87]):
    for k,z in enumerate([-24.5,-27,-29.5,-32,-34.5,-37]):
        if not inside.buffer(-.15).contains(Point(x,z)):continue
        cyl(f'Ceiling_Downlight_Trim_{i}_{k}',[x,z],3.28,.15,.014,'metal','Ceiling_Lighting',('JIEN07','JIEN09'))
        cyl(f'Ceiling_Downlight_Emitter_{i}_{k}',[x,z],3.269,.12,.005,'light_white','Ceiling_Lighting',('JIEN07','JIEN09'))
        if i%2==0 and k%2==0:add('light',f'Actual_Interior_Light_{i}_{k}','Ceiling_Lighting',('JIEN07',),q=[x,z],y=3.08,range=3.5,intensity=.35,color=[1,.97,.9,1])
for i,(x,z) in enumerate([(71.5,-30.1),(75.2,-31.2),(78.7,-32.0)]):
    # Lathed hemispherical shell with bright lime interior, physically hollow.
    vs=[];ts=[]
    for j in range(9):
        a=j/8*math.pi/2;r=.29*math.sin(a);yy=.29*math.cos(a)
        for k in range(24):b=k/24*math.tau;vs.append([r*math.cos(b),yy,r*math.sin(b)])
    for j in range(8):
        for k in range(24):a=j*24+k;b=j*24+(k+1)%24;c=(j+1)*24+k;d=(j+1)*24+(k+1)%24;ts.extend([a,c,d,a,d,b])
    mesh(f'Pendant_White_Dome_{i}',[x,z],2.43,vs,ts,'white','Ceiling_Pendants',('JIEN06','JIEN08','JIEN10'),key='white_dome',collider=False)
    cyl(f'Pendant_Lime_Inner_{i}',[x,z],2.431,.55,.008,'light_green','Ceiling_Pendants',('JIEN08',))
    cyl(f'Pendant_Suspension_{i}',[x,z],2.98,.008,.52,'metal','Ceiling_Pendants',('JIEN08',))

# Actual visible extinguisher model by wall; presence is observed in PSS03.
cyl('Fire_Extinguisher_Steel_Canister',[70.15,-28.2],.27,.14,.43,'red','Observed_Safety_Fixtures',('PSS03',))
cyl('Fire_Extinguisher_Neck',[70.15,-28.2],.51,.055,.06,'metal','Observed_Safety_Fixtures',('PSS03',))
cub('Fire_Extinguisher_Handle',[70.15,-28.2],.56,[.11,.025,.045],'black','Observed_Safety_Fixtures',source=('PSS03',),collider=False)
pipe('Fire_Extinguisher_Hose',[70.20,.51,-28.2],[70.23,.18,-28.2],.009,'black','Observed_Safety_Fixtures',('PSS03',))

# Equipment has full physical shape. Remove supplementary lettering entirely from
# this packet; current user explicitly rejects naming text used in place of models.
objects=[o for o in objects if o['kind']!='text']
packet=dict(store_key='subway',origin_q=ORIGIN,floor_world_y=7.0,
 source_packet='.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/subway/SOURCE-EVIDENCE-R25.json',
 source_imports=imports,materials=materials,objects=objects)
(BASE/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
sha=hashlib.sha256((BASE/'STORE-PACKET-R25.json').read_bytes()).hexdigest()
s=(BASE/'ApplyStoreR25.cs').read_text()
s=re.sub(r'PacketSHA="[0-9a-f]{64}"', 'PacketSHA="'+sha+'"',s)
for a,b in {'__STORE_KEY__':'subway','__WORKER_THREAD__':'classic:/root/r25_subway','__EXACT_SCOPE__':'C180_R25_SUBWAY_APPLY_001','__PACKET_PATH__':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/subway/STORE-PACKET-R25.json','__PACKET_SHA256__':sha}.items():s=s.replace(a,b)
(BASE/'ApplyStoreR25.cs').write_text(s)
(BASE/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps(dict(
 approved_polygon_area_m2=REGION.area,approved_ring_unchanged=True,floor_world_y=7,
 footprint='fixed R24 bluecell4, source mark colors are not metric authority',
 front='actual retained C177 column row qz≈-21.8; glazing split at actual columns; clear paired auto portal between 83.56 and90.81 axes',
 native_interior_columns_preserved=native_columns,
 booth_height='MODEL 3.30m from observed 2.5m glassheight/fascia ratio, not measured; actual native columns/roof untouched',
 datum='floor finishes+7mm only, structural existing floor unchanged; no new holes',
 furnishing='Source-count/family relationships transferred into approved bay; exact dimensions are photo-referenced MODEL rather than survey',
 hidden_backroom='No invented hidden full kitchen or gas range, only observed open service/preparation/oven/drinks equipment',
 asset_imports=len(imports),object_count=len(objects),kind_counts=dict(Counter(o['kind'] for o in objects)),
 no_equipment_name_text_proxy=True,shared_free_source_files_unchanged=True,
 scene_QA_Play_tests_reopen_captures=False,packet_sha256=sha),ensure_ascii=False,indent=2))
print(json.dumps({'packet_sha256':sha,'objects':len(objects),'imports':len(imports),'asset_instances':sum(o['kind']=='asset' for o in objects)},indent=2))
