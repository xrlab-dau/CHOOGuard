from pathlib import Path
import json, math, hashlib, sys
import cv2, numpy as np
ROOT=Path('/Users/um-yunsang/CHOOGuard');B=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/nursing-room/constructor';S=B.parent/'wonew11-224339225506'
sys.path.insert(0,str(ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'));from photo_outline_to_solid import closed_solid_from_mask
objs=[];meshes={}
def o(name,pos,size,mat='white',group='SourceVisibleInterior',kind='box',euler=(0,0,0),**kw):
 d=dict(id=name,kind=kind,position=list(pos),scale=list(size),euler=list(euler),material=mat,group=group,source_ids=kw.pop('source_ids',['WONEW11-202607-FIELD']),qualification='SOURCE_VISIBLE_PHOTO_PROPORTIONED_MODEL_IN_CURRENT_GENERATED_CELL_NOT_SURVEYED');d.update(kw);objs.append(d)
def mesh(name,v,t,pos=(0,0,0),size=(1,1,1),mat='white',group='SourceVisibleInterior',euler=(0,0,0)):
 meshes[name]={'vertices':v,'triangles':t};o(name,pos,size,mat,group,'mesh',euler,mesh_key=name)
def beam(name,a,b,r=.012,mat='steel',group='SourceVisibleInterior'):
 a=np.array(a);b=np.array(b);d=b-a;length=np.linalg.norm(d)
 if length<1e-8:return
 d=d/length
 o(name,(a+b)/2,(r,length/2,r),mat,group,'cylinder',(math.degrees(math.acos(np.clip(d[1],-1,1))),math.degrees(math.atan2(d[0],d[2])),0))
def rounded(name,w,h,d,r=.04):
 v=[];t=[];points=[]
 for cx,cy,start in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
  for i in range(8):
   a=math.radians(start+i*90/7);points.append([cx+r*math.cos(a),cy+r*math.sin(a)])
 n=len(points);v=[[x,y,z]for z in[-d/2,d/2]for x,y in points]
 for i in range(1,n-1):t.extend([0,i+1,i,n,n+i,n+i+1])
 for i in range(n):
  j=(i+1)%n;t.extend([i,j,n+j,i,n+j,n+i])
 meshes[name]={'vertices':v,'triangles':t}
def rr(name,pos,size,mat='white',group='SourceVisibleInterior',euler=(0,0,0),r=.04):
 key=name+'_shape';rounded(key,*size,r=min(r,size[0]/3,size[1]/3));o(name,pos,(1,1,1),mat,group,'mesh',euler,mesh_key=key)
def source_obj(name,objfile,pos,size,mat='gray',group='DownloadedCC0PhysicalModels',euler=(0,0,0)):
 v=[];t=[]
 for line in objfile.read_text().splitlines():
  a=line.split()
  if not a:continue
  if a[0]=='v':v.append([float(x)for x in a[1:4]])
  elif a[0]=='f':
   ix=[int(x.split('/')[0])-1 for x in a[1:]]
   for k in range(1,len(ix)-1):t.extend([ix[0],ix[k],ix[k+1]])
 v=np.array(v);lo=v.min(0);hi=v.max(0);v=(v-(lo+hi)/2)/(hi-lo)*size
 mesh(name,v.tolist(),t,pos,mat=mat,group=group,euler=euler)
objdir=B/'downloaded-free-models/Models/OBJ format'
# Actual entrance and call bell, cut only own generated 174 wall. Door slides aside in modeled open pose.
DOOR_DX=(-12.913583944800076-8.263383310596382)/2+11.50
for x in[-.58,.58]:o('Stainless_Door_Jamb_'+str(x),(x+DOOR_DX,1.23,-.11),(.055,2.46,.28),'steel','EntranceAndCallBell',collider=True)
o('Stainless_Sliding_Door_Header',(0,2.47,-.11),(1.22,.15,.29),'steel','EntranceAndCallBell',collider=True)
o('Door_Track_Header_Slot',(0,2.37,-.265),(1.10,.025,.014),'black','EntranceAndCallBell')
# Source open door parked to left of opening, not a solid wall blocker.
for dx in[-.52,.52]:o('Sliding_Glass_Leaf_Frame_'+str(dx),(-1.05+dx,1.16,-.27),(.022,2.30,.026),'steel','SlidingGlazedDoor')
o('Sliding_Frosted_Turquoise_Glass',(-1.05,1.16,-.27),(1.02,2.28,.018),'frosted','SlidingGlazedDoor',collider=True)
for y in[.018,2.30]:o('Sliding_Leaf_Horizontal_Frame_'+str(y),(-1.05,y,-.27),(1.08,.03,.035),'steel','SlidingGlazedDoor')
o('Intercom_Stainless_Enclosure',(.91,1.15,-.272),(.155,.26,.04),'steel','EntranceAndCallBell')
o('Intercom_Black_Front',(.91,1.18,-.298),(.115,.14,.016),'black','EntranceAndCallBell')
o('Intercom_Call_Button',(.91,1.155,-.31),(.025,.010,.025),'steel','EntranceAndCallBell','cylinder',(90,0,0))
for i in range(7):o('Intercom_Speaker_Slit_'+str(i),(.91,1.06+i*.006,-.298),(.065,.002,.012),'black','EntranceAndCallBell')
# Entry privacy screen source04. Opening to left remains 1.2m clear of native column terminal.
o('Entry_Privacy_Pale_Green_Screen',(.47,1.10,1.23),(1.20,2.20,.045),'sage','PrivacyPartitions',collider=True)
for x in[-.15,1.09]:o('Entry_Privacy_White_Jamb_'+str(x),(x,1.10,1.23),(.035,2.24,.065),'white','PrivacyPartitions')
o('Entry_Privacy_Top_White_Rail',(.47,2.21,1.23),(1.27,.04,.065),'white','PrivacyPartitions')
# Photo-visible seating left of entry: long armless gray bench, source-shaped from downloaded CC0 design couch.
source_obj('CC0_Downloaded_Design_Sofa_Photo_Adapted',objdir/'loungeDesignSofa.obj',(-3.98,.47,2.77),(2.10,.83,.69),'gray',euler=(0,90,0))
# Source floor-mounted highchair3, cream molded tray/back, timber folding frame.
for k,x in enumerate([-3.35,-2.52,-1.69]):
 g='ThreeSourceHighchairs';z=4.17
 for dx in[-.20,.20]:
  for az in[-.19,.19]:beam(f'Highchair_{k}_Wood_Leg_{dx}_{az}',[x+dx,.04,z+az],[x+dx*.65,.71,z-az*.40],.026,'wood',g)
 rr(f'Highchair_{k}_Cream_Seat',(x,.60,z),(.43,.35,.075),'cream',g,euler=(90,0,0),r=.05)
 rr(f'Highchair_{k}_Cream_Back',(x,.88,z+.15),(.43,.42,.07),'cream',g,r=.10)
 rr(f'Highchair_{k}_Molded_Tray',(x,.76,z-.21),(.54,.40,.045),'cream',g,euler=(90,0,0),r=.09)
 o(f'Highchair_{k}_Tray_Rim',(x,.79,z-.39),(.49,.03,.025),'cream',g)
 o(f'Highchair_{k}_Front_Footrest',(x,.30,z-.23),(.40,.035,.11),'wood',g)
 for dx in[-.15,.15]:o(f'Highchair_{k}_Back_Slot_{dx}',(x+dx,.88,z+.108),(.022,.14,.004),'seatshade',g)
# Two observed changing units, front-of-right section, timber sides and washable cream basin pad.
for k,x in enumerate([1.66,2.73]):
 g='TwoSourceChangingTables';z=.40
 for dx in[-.46,.46]:o(f'Changing_{k}_Wood_Side_{dx}',(x+dx,.46,z),(.035,.91,.63),'wood',g,collider=True)
 o(f'Changing_{k}_Wood_Back',(x,.50,.09),(.95,1.0,.035),'wood',g)
 o(f'Changing_{k}_Lower_Shelf',(x,.22,z),(.91,.035,.59),'wood',g)
 rr(f'Changing_{k}_Cream_Changing_Pad',(x,.82,z),(.86,.58,.055),'cream',g,euler=(90,0,0),r=.05)
 for dx in[-.41,.41]:o(f'Changing_{k}_High_Cream_Rail_{dx}',(x+dx,.94,z),(.045,.24,.62),'cream',g)
 o(f'Changing_{k}_Back_Cream_Rail',(x,.99,.105),(.86,.28,.035),'cream',g)
 for dx in[-.46,.46]:o(f'Changing_{k}_Lower_Shelf_Foot_{dx}',(x+dx,.06,z),(.05,.1,.62),'wood',g)
# Source shows FOUR curtain entries: two opposite two, retained as four compact visible bays.
for row,(zback,zfront,sign) in enumerate([(1.43,2.48,1),(4.55,3.47,-1)]):
 g='FourSourceCurtainedNursingBays'
 for k,(xa,xb) in enumerate([(1.07,2.19),(2.23,3.35)]):
  zc=(zback+zfront)/2;dep=abs(zfront-zback);cx=(xa+xb)/2
  o(f'Nursing_Bay_{row}_{k}_Rear_Pale_Green',(cx,1.05,zback),(xb-xa,2.10,.035),'sage',g,collider=True)
  for x in[xa,xb]:o(f'Nursing_Bay_{row}_{k}_Side_{x}',(x,1.05,zc),(.035,2.10,dep),'sage',g,collider=True)
  for x in[xa,xb]:o(f'Nursing_Bay_{row}_{k}_White_Front_Jamb_{x}',(x,1.05,zfront),(.035,2.14,.065),'white',g)
  o(f'Nursing_Bay_{row}_{k}_Top_Rail',(cx,2.11,zfront),(xb-xa+.04,.04,.065),'white',g)
  # retract curtain toward the left edge, retain entry access, lower white fabric band.
  v=[];t=[];N=24
  for i in range(N+1):
   xx=xa+.035+.26*i/N;zz=zfront+.024*math.sin(i/N*math.pi*10)
   v.extend([[xx,.25,zz],[xx,2.05,zz],[xx,.25,zz+.004],[xx,2.05,zz+.004]])
  for i in range(N):
   a=i*4;b=a+4
   for ids in[[a,b,b+1,a+1],[a+3,b+3,b+2,a+2],[a+1,b+1,b+3,a+3],[a+2,b+2,b,a]]:t.extend([ids[0],ids[1],ids[2],ids[0],ids[2],ids[3]])
  mesh(f'Nursing_Bay_{row}_{k}_Pleated_Pink_Curtain',v,t,mat='curtain',group=g)
  o(f'Nursing_Bay_{row}_{k}_White_Curtain_Hem',(xa+.165,.15,zfront),(.27,.24,.032),'white',g)
  # actual compact armless nursing chair, downloaded CC0 model independently edited source-colored.
  source_obj(f'CC0_Nursing_Chair_{row}_{k}',objdir/'loungeDesignSofa.obj',(cx,.46,zc),(.72,.82,.70),'gray',g,euler=(0,0 if sign==1 else 180,0))
  o(f'Nursing_Bay_{row}_{k}_Rear_Wood_Table',(cx,.76,zback-sign*.10),(.75,.035,.16),'wood',g)
  # Source12 visible U-shaped feeding pillow on chair back, not a solid torus hole filler.
  if row==0 and k==0:
   vv=[];tt=[];ns=48;nr=10
   for i in range(ns+1):
    a=math.radians(35+290*i/ns)
    for h in range(nr):
     b=2*math.pi*h/nr;r=.23+.09*math.cos(b);vv.append([cx+r*math.cos(a),.91+.055*math.sin(b),zc+.12+r*math.sin(a)*.55])
   for i in range(ns):
    for h in range(nr):
     a=i*nr+h;b=i*nr+(h+1)%nr;c=(i+1)*nr+(h+1)%nr;d=(i+1)*nr+h;tt.extend([a,b,c,a,c,d])
   mesh('Source_Nursing_U_Pillow',vv,tt,mat='pillow',group=g)
# Source food preparation cabinet facing central entry, left of nursing aisle.
g='SinkMicrowaveSterilizer';cx=-.50;zc=4.29
# lower four cabinets physical hollow shell, rim countertop around actual basin aperture.
for dx in[-.94,.94]:o('Preparation_Cabinet_End_'+str(dx),(cx+dx,.43,zc),(.035,.83,.58),'white',g)
o('Preparation_Cabinet_Rear',(cx,.43,4.57),(1.92,.83,.035),'white',g)
o('Preparation_Cabinet_Bottom',(cx,.09,zc),(1.90,.04,.58),'white',g)
for k in range(4):o('Lower_Cabinet_Door_'+str(k),(cx-.72+k*.48,.46,3.985),(.47,.74,.025),'white',g)
o('Left_Solid_Countertop',(cx-.54,.87,zc),(.86,.055,.64),'counter',g)
# actual single hollow basin, category asset already acquired licensed CGIME06, fit source basin proportions.
# Free source base used for basin bowl only, retain cabinet separately.
# Reuse only hollow metal sink bowl from the freshly downloaded CC0 kitchenSink, not its unrelated cabinet.
sv=[];sf=[];group='';mtl=''
for line in (objdir/'kitchenSink.obj').read_text().splitlines():
 aa=line.split()
 if not aa:continue
 if aa[0]=='v':sv.append([float(x)for x in aa[1:4]])
 elif aa[0]=='g':group=aa[1]
 elif aa[0]=='usemtl':mtl=aa[1]
 elif aa[0]=='f' and group=='kitchenSink' and mtl in ['metal','metalDark']:
  ids=[int(x.split('/')[0])-1 for x in aa[1:]]
  for ii in range(1,len(ids)-1):sf.append([ids[0],ids[ii],ids[ii+1]])
sv=np.array(sv);used=np.unique(np.array(sf));lo=sv[used].min(0);hi=sv[used].max(0);tv=(sv-(lo+hi)/2)/(hi-lo)*[.92,.23,.56];seen=set();tt=[]
for tri in sf:
 key=tuple(sorted(tuple(np.round(tv[i],7))for i in tri))
 if key not in seen:tt.extend(tri);seen.add(key)
mesh('Downloaded_CC0_Hollow_Stainless_Basin',tv.tolist(),tt,(cx+.44,.755,zc),mat='steel',group=g)

o('Counter_Front_Rim',(cx+.45,.87,3.995),(1.0,.055,.055),'counter',g);o('Counter_Back_Rim',(cx+.45,.87,4.58),(1.0,.055,.055),'counter',g)
for x in[cx-.04,cx+.95]:o('Counter_Sink_Side_Rim_'+str(x),(x,.87,zc),(.055,.055,.64),'counter',g)
# source upper shelf open cubbies, actual oval-window microwave source-adapted downloaded base.
for x in[cx-.96,cx,cx+.96]:o('Upper_Cubby_Upright_'+str(x),(x,1.83,4.43),(.035,.79,.36),'white',g)
for y in[1.42,2.23]:o('Upper_Cubby_Horizontal_'+str(y),(cx,y,4.43),(1.96,.035,.37),'white',g)
o('Upper_Right_Cubby_Shelf',(cx+.48,1.81,4.43),(.93,.025,.35),'white',g)
o('Upper_Cubby_Back',(cx,1.83,4.60),(1.96,.79,.025),'white',g)
source_obj('CC0_Downloaded_Microwave_Source_Adapted',objdir/'kitchenMicrowave.obj',(cx-.47,1.62,4.39),(.65,.35,.38),'white',g,euler=(0,180,0))
# oval black window physical front, actual LG observed face proportion.
o('Microwave_Oval_Black_Window',(cx-.52,1.63,4.18),(.47,.015,.27),'black',g,'sphere',(90,0,0))
o('Microwave_Main_Dial',(cx-.19,1.68,4.17),(.052,.012,.052),'gray',g,'cylinder',(90,0,0))
o('Microwave_Blue_Start_Button',(cx-.19,1.50,4.17),(.024,.01,.024),'blue',g,'cylinder',(90,0,0))
for i in range(3):o('Microwave_Control_Button_'+str(i),(cx-.19,1.60-i*.029,4.165),(.022,.009,.01),'steel',g)
rr('Black_Front_Sterilizer',(cx-.66,1.09,4.27),(.36,.40,.34),'white',g)
o('Sterilizer_Gloss_Black_Door',(cx-.66,1.10,4.09),(.31,.33,.012),'black',g)
o('Formula_Equipment_Cylinder',(cx-.22,1.07,4.31),(.15,.20,.15),'white',g,'cylinder')
# two source-visible taps arched, physical metal tubing.
for n,(x,y,r)in enumerate([(cx+.72,1.09,.135),(cx+.16,1.0,.10)]):
 pts=[[x,.89,4.48],[x,y,4.48]]
 for i in range(13):
  a=math.pi*i/12;pts.append([x,y+r*math.sin(a),4.48-r+r*math.cos(a)])
 for i in range(len(pts)-1):beam(f'Gooseneck_Tap_{n}_Tube_{i}',pts[i],pts[i+1],.012,'steel',g)
for k,x in enumerate([cx-.80,cx-.10]):
 o('Source_Appliance_Outlet_'+str(k),(x,1.30,4.615),(.11,.10,.018),'white',g)
 for h in[-.025,.025]:o(f'Outlet_{k}_Socket_{h}',(x+h,1.30,4.600),(.014,.018,.007),'black',g)
# subway tile backsplash physical grout grid, not a room from an unrelated source.
o('White_Subway_Tile_Backsplash',(cx,1.17,4.625),(1.96,.55,.018),'tile',g)
for i in range(6):o('Backsplash_Horizontal_Grout_'+str(i),(cx,.91+i*.105,4.611),(1.96,.003,.004),'grout',g)
for row in range(5):
 for i in range(7):
  x=cx-.96+i*.31+(row%2)*.155
  if x<cx+.95:o(f'Backsplash_Vertical_Grout_{row}_{i}',(x,.9625+row*.105,4.610),(.003,.102,.004),'grout',g)
# source water dispenser with taps and black drip well; refuse-bin and cleaning bottle visible alongside.
g='WaterAndCleaning';x=-2.18;z=1.77
rr('Water_Purifier_Tall_White_Base',(x,.57,z),(.38,1.14,.38),'white',g)
o('Water_Purifier_Black_Dispense_Recess',(x,.99,z-.205),(.29,.26,.022),'black',g)
for dx in[-.075,.075]:o('Water_Purifier_Tap_'+str(dx),(x+dx,1.05,z-.24),(.025,.075,.03),'steel',g)
o('Water_Purifier_Drip_Tray',(x,.84,z-.23),(.31,.025,.10),'gray',g)
rr('Water_Purifier_Upper_White_Case',(x,1.28,z),(.37,.29,.36),'white',g)
rr('Visible_Cream_Refuse_Bin',(x-.53,.31,z),(.30,.58,.34),'cream',g)
o('Refuse_Bin_Recessed_Opening',(x-.53,.59,z-.17),(.22,.08,.012),'black',g)
o('Blue_Cleaning_Bottle',(x-.34,.16,z-.10),(.052,.14,.052),'blue',g,'cylinder')
# Interior source green finishes, physically separate thin panels; existing wall and native column remain untouched.
g='InteriorSourceFinishes'
o('Rear_Sage_Interior_Liner',(-4.55,1.18,3.00),(.012,2.36,2.91),'sage',g)
o('Native002_Inside_Green_Cladding',(-3.05,1.18,1.34),(2.95,2.36,.012),'sage',g)
# World signs directly from original source contours, not Text/TMP or photo planes.
im=cv2.imread(str(S/'FIELD-00.jpg'))
def contour(name,crop,size,pos,mat='white',threshold=160,group='ActualSourceContourSigns'):
 x0,y0,x1,y1=crop;sub=im[y0:y1,x0:x1];gray=cv2.cvtColor(sub,cv2.COLOR_BGR2GRAY);mask=((gray>threshold)&((sub.max(2)-sub.min(2))<75)).astype('uint8')*255
 cv2.imwrite(str(B/(name+'-mask.png')),mask);meshes[name]=closed_solid_from_mask(mask,*size,depth=.003,minimum_area=2,epsilon=.25);o(name,pos,(1,1,1),mat,group,'mesh',(0,180,0),mesh_key=name)
contour('Actual_Source_Nursing_Baby_Name',(219,162,324,228),(.45,.26),(-.92,1.96,-.283))
contour('Actual_Source_Two_Baby_Face_Door_Ink',(349,311,550,415),(.93,.42),(-1.05,1.16,-.287))
# Two observed operational notice boards: real source outlines + colored printed content converted to separate contour volumes.
for k,(crop,pos,wh)in enumerate([((597,149,735,261),(.96,1.91,-.282),(.44,.36)),((236,232,284,299),(-.92,1.59,-.282),(.26,.29))]):
 x0,y0,x1,y1=crop;sub=im[y0:y1,x0:x1];w,h=wh;o(f'Source_Notice_Backing_{k}',pos,(w,h,.018),'paper','ActualSourceContourSigns')
 for channel,mat in [('dark','black'),('blue','blue'),('red','red')]:
  hsv=cv2.cvtColor(sub,cv2.COLOR_BGR2HSV)
  if channel=='dark':mask=((sub.max(2)<130)&(hsv[:,:,1]<120)).astype('uint8')*255
  elif channel=='blue':mask=((hsv[:,:,0]>85)&(hsv[:,:,0]<125)&(hsv[:,:,1]>65)&(hsv[:,:,2]>65)).astype('uint8')*255
  else:mask=(((hsv[:,:,0]<12)|(hsv[:,:,0]>168))&(hsv[:,:,1]>70)&(hsv[:,:,2]>75)).astype('uint8')*255
  name=f'Actual_Source_Notice_{k}_{channel}_Contours';meshes[name]=closed_solid_from_mask(mask,w*.94,h*.93,depth=.0015,minimum_area=1,epsilon=.25);o(name,(pos[0],pos[1],pos[2]-.012),(1,1,1),mat,'ActualSourceContourSigns','mesh',(0,180,0),mesh_key=name)
# Move own entry assembly into the actual free native-pier bay. The nursing furniture remains bounded in the existing cell.
for a in objs:
 if a['group'] in ['EntranceAndCallBell','SlidingGlazedDoor','ActualSourceContourSigns'] and not a['id'].startswith('Stainless_Door_Jamb_'):
  a['position'][0]+=DOOR_DX
 if a['id'].startswith('Entry_Privacy_'):
  a['position'][0]+=DOOR_DX
# room source floor finish is modeled as thin removable surface inside fixed boundaries excluding native solids, not base slab mutation.
# No addition of a base slab/floor hole; accepted current floor material remains unchanged in this lane.
materials={
'white':([.82,.83,.80,1],0,.35),'cream':([.76,.72,.63,1],0,.4),'gray':([.19,.20,.22,1],0,.42),'black':([.016,.019,.021,1],.1,.65),'steel':([.54,.57,.58,1],.92,.72),'wood':([.47,.33,.18,1],0,.28),'sage':([.48,.54,.39,1],0,.23),'curtain':([.55,.40,.44,1],0,.14),'frosted':([.29,.58,.56,.72],.05,.43),'seatshade':([.25,.22,.18,1],0,.1),'counter':([.77,.77,.72,1],.08,.48),'tile':([.85,.86,.80,1],0,.63),'grout':([.10,.11,.10,1],0,.12),'blue':([.08,.33,.48,1],0,.35),'pillow':([.70,.63,.48,1],0,.14),'paper':([.85,.84,.75,1],0,.25),'red':([.55,.12,.12,1],0,.3)}
mm={k:{'color':c,'metallic':m,'smoothness':s,'transparent':k=='frosted'}for k,(c,m,s)in materials.items()}
Q=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]]);qorigin=np.array([-4.0552,-11.50]);world=Q@qorigin;forward=Q@np.array([1.,0.]);yaw=math.degrees(math.atan2(forward[0],forward[1]))
credit='Kenney Furniture Kit, Kenney (https://kenney.nl/assets/furniture-kit), CC0. Original downloaded ZIP and License retained with SHA256 in constructor/downloaded-free-models. Independently adapted microwave and armless design sofa/chair source meshes, not exact installed make or surveyed dimensions.\nHollow basin extracted from the same downloaded CC0 kitchenSink model metal surfaces only; unrelated cabinet and duplicate coincident source triangles excluded. No heavy CGIME06 sink imported.\nActual visible room furniture, physical contour signs and equipment are photo-proportioned MODEL from https://m.blog.naver.com/wonew11/224339225506 (July2026 source photos). Room placement is bounded existing generated nursing cell, no field survey or calibrated99percent claim.\n'
p={'schema':'chooguard.r26.source_nursing_room.v1','author':'classic:/root/r26_nursing_room','scope':'C180_R26_NURSING_ROOM_APPLY_001','wall_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1748468989-0','wall_mesh_asset':'Assets/ChooGuard/Art/StationInterior/GuidePhotoWallsR14_20261019/R14_WALL_DERIVATIVE_06.asset','entry_qy_interval':[-11.50+DOOR_DX-.58,-11.50+DOOR_DX+.58],'entry_world_y_max':9.45,'entry_qualification':'MODEL aperture centered in the actual clear structural bay between fixed column002 terminal qY-12.9136 and near native0791 qY-8.26338; actual July2026 shows entry in brown nurse wall behind OliveYoung, not exact unique metric door alias.','origin_world_xz':world.tolist(),'origin_actual_q':qorigin.tolist(),'floor_world_y':7,'yaw_world':yaw,'asset_folder':'Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/NursingRoom','parent_path':'FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT','group_name':'R26_NURSING_ROOM_SOURCE_MODEL','materials':mm,'mesh_library':meshes,'objects':objs,'attribution':credit,'actual_source_counts':{'changing_tables':2,'highchairs':3,'curtain_bays':4,'public_gray_sofa':1,'microwave':1,'sterilizer':1,'formula_unit':1,'water_purifier':1,'sink':1},'geometry_qualification':'Photo-proportioned source semantic MODEL within accepted existing generated cell; walls/natives/columns remain fixed except authorized local aperture in generated174wall. No exact asbuilt measure or99percent claim.'}
(B/'NURSING-ROOM-PACKET-R26.json').write_text(json.dumps(p,separators=(',',':'),allow_nan=False));(B/'SOURCE-ATTRIBUTION.txt').write_text(credit);print('objects',len(objs),'meshcount',len(meshes),'yaw',yaw,'sha',hashlib.sha256((B/'NURSING-ROOM-PACKET-R26.json').read_bytes()).hexdigest())
