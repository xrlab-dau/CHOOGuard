import sys,json,math,hashlib,datetime,cv2,numpy as np
from pathlib import Path
from scipy.spatial import ConvexHull
sys.path.insert(0,str(Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25').resolve()))
from photo_outline_to_solid import closed_solid_from_mask
BASE=Path(__file__).resolve().parent;ROOT=Path('/Users/um-yunsang/CHOOGuard');SRC=BASE/'sources';SRC.mkdir(exist_ok=True)
source=ROOT/'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/self-ticket/selected-source-frames/source_240p0s.jpg'
a=cv2.imread(str(source)); b=cv2.imread(str(ROOT/'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/self-ticket/selected-source-frames/source_238p0s.jpg'))
lib={};objs=[];assets=[];signs=[]
# ACTUAL q basis; packet uses (qx,-qy). Finite selected bay from actual native/CAD faces.
P0=np.array([27.480003596460556,-.012185982485971858]);P1=np.array([35.229910003646005,.024268873095050134]);T=(P1-P0)/np.linalg.norm(P1-P0);N=np.array([-T[1],T[0]])
C=np.array([31.42,float(P0[1]+(31.42-P0[0])*T[1]/T[0])]);origin=[float(C[0]),float(-C[1])];yaw=180+math.degrees(math.atan2(T[1],T[0]))
def q(u,v):
 pt=C+T*u+N*v
 return [round(float(pt[0]),7),round(float(-pt[1]),7)]
def obj(id,u,y,v,size,mat,kind='library_mesh',mesh='chamfer',collider=True,angle=0,group='Installation'):
 d={'id':id,'kind':kind,'group':group,'q':q(u,v),'y':y,'size':size,'material':mat,'yaw':yaw+angle,'collider':collider,'source_ids':['VIDEO_20230501_238_240'],'qualification':'SOURCE_EPOCH_PHOTO_PROPORTIONED_MODEL_NOT_SURVEYED'}
 if kind=='library_mesh':d['mesh_key']=mesh
 objs.append(d);return d

def bevelbox(b=.08):
 pts=[]
 for axis in range(3):
  for sign in [-1,1]:
   other=[k for k in range(3)if k!=axis]
   for sa in[-1,1]:
    for sb in[-1,1]:
     p=[0.,0.,0.];p[axis]=sign*.5;p[other[0]]=sa*(.5-b);p[other[1]]=sb*(.5-b);pts.append(p)
 h=ConvexHull(np.array(pts));tri=[]
 for ind,eq in zip(h.simplices,h.equations):
  vs=np.array(pts)[ind]
  if np.dot(np.cross(vs[1]-vs[0],vs[2]-vs[0]),eq[:3])<0:ind=ind[[0,2,1]]
  tri.extend(map(int,ind))
 return {'vertices':pts,'triangles':tri}
lib['chamfer']=bevelbox(.035);lib['rounder']=bevelbox(.12)

def ring(name,w,h,openingw,openingh,depth,opencentery=0):
 # Independently editable physical frame, no front surface closing aperture.
 for ident,x,y,ww,hh in [('top',0,(h+openingh)/4,w,(h-openingh)/2),('bottom',0,-(h+openingh)/4,w,(h-openingh)/2),('left',-(w+openingw)/4,0,(w-openingw)/2,openingh),('right',(w+openingw)/4,0,(w-openingw)/2,openingh)]:
  yield ident,x,y,ww,hh,depth

def warp(im,quad,w,h,name):
 H=cv2.getPerspectiveTransform(np.array(quad,np.float32),np.array([[0,0],[w-1,0],[w-1,h-1],[0,h-1]],np.float32));r=cv2.warpPerspective(im,H,(w,h));cv2.imwrite(str(SRC/(name+'-rectified.jpg')),r);return r

def solid(name,im,mask,width,height,u,y,v,mat,depth=.003,mina=3):
 mask=mask.astype('uint8')*255;cv2.imwrite(str(SRC/(name+'-contour-mask.png')),mask)
 m=closed_solid_from_mask(mask,width,height,depth=depth,minimum_area=mina,epsilon=.35)
 lib[name]=m;obj(name,u,y,v,[1,1,1],mat,mesh=name,collider=False,group='PhysicalSourceContourSigns')
 signs.append({'name':name,'source_image':str(source.relative_to(ROOT)),'rectified_source':str((SRC/(name+'-rectified.jpg')).relative_to(ROOT)),'mesh_polygons':m['source_polygons'],'front_normal':'actual positive qy, model sign closed front/back/sides with counters','physical_depth':depth,'photo_plane':False})
 return name
fas=warp(a,[[168,477],[868,503],[867,548],[168,522]],1600,105,'tickets-fascia')
hsv=cv2.cvtColor(fas,cv2.COLOR_BGR2HSV);mask=(hsv[:,:,2]>100)&(hsv[:,:,1]<145);mask[:8]=0;mask[-5:]=0
solid('tickets-fascia',fas,mask,4.70,.28,-.38,2.15,-.11,'white',.005,8)
# source-backed physical facade/installation rail, not an enclosed architectural wall
obj('EquipmentRearCarrier',0,1.25,-.95,[6.57,2.50,.045],'offwhite',group='EquipmentCarrier')
obj('EquipmentLowerBlackRail',0,.065,-.53,[6.57,.13,.24],'black',group='EquipmentCarrier')
obj('TicketsFasciaHousing',0,2.15,-.145,[6.57,.34,.12],'navy',group='EquipmentCarrier')
obj('FasciaTopSteelEdge',0,2.333,-.145,[6.61,.022,.14],'steel',group='EquipmentCarrier')
# Physical upper display bodies; source screen image separately imported as electronic UI only.
for i,u in enumerate([-1.94,1.66]):
 g=f'Display_{i+1}'
 obj(g+'_RearSupport',u,2.81,-.89,[.09,1.13,.09],'steel',group=g)
 obj(g+'_Enclosure',u,2.92,-.59,[2.52,1.11,.20],'charcoal',group=g)
 # Source image is dynamic digital display content, not a physical printed-sign substitute.
 im=warp(b,([[13,255],[675,316],[674,495],[13,457]] if i==0 else [[919,334],[1368,382],[1363,531],[917,480]]),960,400,g+'_RuntimeScreen')
 f=SRC/(g+'_RuntimeScreen.png');cv2.imwrite(str(f),im)
 rel='UI/'+f.name;ap='Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/SelfTicket/'+rel
 assets.append({'source':str(f),'destination':rel,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
 obj(g+'_ActualDigitalScreen',u,2.92,-.476,[2.36,.96,.012],'screen'+str(i),group=g,collider=False)
# Poster carrier panels alternate cyan and white as observed; no generic painted shop labels.
blue=warp(a,[[813,549],[916,551],[916,853],[813,858]],240,740,'cyan-infill')
h=cv2.cvtColor(blue,cv2.COLOR_BGR2HSV)
yellow=(h[:,:,0]>15)&(h[:,:,0]<45)&(h[:,:,1]>80)&(h[:,:,2]>110)
white=(h[:,:,1]<85)&(h[:,:,2]>155)
# Save source-contour prototype, then individually place only the physical glyphs over matching substrate.
for nm,m,mat in [('cyan-ad-white',white,'white'),('cyan-ad-yellow',yellow,'yellow')]:
 lib[nm]=closed_solid_from_mask(m.astype('uint8')*255,.42,1.67,.0015,4,.30)
 cv2.imwrite(str(SRC/(nm+'-contour-mask.png')),m.astype('uint8')*255)
wp=warp(a,[[547,570],[654,570],[654,848],[547,848]],240,700,'white-infill');hh=cv2.cvtColor(wp,cv2.COLOR_BGR2HSV)
for nm,m in [('white-ad-blue',(hh[:,:,0]>85)&(hh[:,:,0]<140)&(hh[:,:,1]>70)&(hh[:,:,2]<225)),('white-ad-red',((hh[:,:,0]<10)|(hh[:,:,0]>140))&(hh[:,:,1]>30)&(hh[:,:,2]<180))]:
 lib[nm]=closed_solid_from_mask(m.astype('uint8')*255,.42,1.52,.0015,4,.30);cv2.imwrite(str(SRC/(nm+'-contour-mask.png')),m.astype('uint8')*255)
centres=np.linspace(-2.50,2.50,5).tolist()
for i,u in enumerate((np.array(centres[:-1])+np.array(centres[1:]))/2):
 g=f'AdvertisingCarrier_{i+1}';v=-.62
 obj(g+'_substrate',float(u),1.09,v,[.44,1.79,.055],'cyan' if i%2==0 else 'white',group=g)
 for side in[-1,1]:obj(g+'_steel_mullion_'+str(side),float(u)+side*.235,1.12,v,[.027,2.01,.07],'steel',group=g)
 for nm,mat,yy in ([('cyan-ad-white','white',1.09),('cyan-ad-yellow','yellow',1.09)]if i%2==0 else [('white-ad-blue','navy',1.09),('white-ad-red','red',1.09)]):obj(g+'_'+nm,float(u),yy,v+.032,[1,1,1],mat,mesh=nm,group=g,collider=False)
# source contour used on actual machine's printed lower door, cyan top category plate.
lab=warp(a,[[958,790],[1068,787],[1068,816],[958,818]],380,90,'machine-tickets-word')
ll=cv2.cvtColor(lab,cv2.COLOR_BGR2HSV);lm=cv2.cvtColor(lab,cv2.COLOR_BGR2GRAY)<143
lib['machine-tickets-word']=closed_solid_from_mask(lm.astype('uint8')*255,.56,.115,.0015,7,.3);cv2.imwrite(str(SRC/'machine-tickets-word-contour-mask.png'),lm.astype('uint8')*255)
header=warp(a,[[382,575],[502,575],[502,601],[382,601]],380,80,'machine-header')
hh=cv2.cvtColor(header,cv2.COLOR_BGR2HSV);hm=(hh[:,:,2]<125)&(hh[:,:,1]>15)
lib['machine-header']=closed_solid_from_mask(hm.astype('uint8')*255,.64,.10,.0015,4,.28);cv2.imwrite(str(SRC/'machine-header-contour-mask.png'),hm.astype('uint8')*255)
for i,u in enumerate(centres):
 g=f'TicketMachine_{i+1:02d}';v=-.16 # cabinet face localfront .26 -> actual qy about .10, recessed from piers .38
 def part(n,x,y,z,size,mat='offwhite',kind='library_mesh',mesh='chamfer',col=True):return obj(g+'_'+n,u-x,y,v+z,size,mat,kind,mesh,col,group=g)
 # Real separate enclosure panels; blue display/control apertures remain open.
 part('LowerDoor',0,.485,.045,[.77,.82,.46]);part('LeftSide',-.381,1.305,-.02,[.048,.84,.52]);part('RightSide',.381,1.305,-.02,[.048,.84,.52]);part('RearEnclosure',0,.93,-.253,[.77,1.72,.034]);part('TopCap',0,1.755,-.007,[.81,.09,.53]);part('LowerServiceDoorSeam',0,.106,.281,[.72,.009,.003],'darksteel',col=False)
 part('BaseFootplate',0,.047,0,[.79,.06,.51],'darksteel');
 for x in[-.30,.30]:
  for z in[-.19,.19]:part('Foot_'+str(x)+'_'+str(z),x,.017,z,[.065,.034,.065],'black',col=False)
 part('HeaderWhiteFace',0,1.674,.254,[.78,.13,.042]);part('HeaderCyanCategory',0,1.675,.279,[.655,.105,.009],'palecyan',col=False)
 part('CategoryPhysicalContours',0,1.675,.285,[1,1,1],'navy',mesh='machine-header',col=False)
 # open blue ring around recessed glass touchscreen
 for nm,x,y,w,h,d in ring('Screen',.68,.43,.565,.307,.055):part('ScreenSurround_'+nm,x,1.43+y,.271,[w,h,d],'periwinkle')
 part('TouchscreenRecess',0,1.43,.242,[.563,.306,.008],'screenblack',col=False)
 # Blue sloping control fascia structure, ports made from independent lips/inner recesses.
 part('ControlBacking',0,1.05,.224,[.678,.315,.024],'periwinkle');
 for x in[-.33,.33]:part('ControlSideRail'+str(x),x,1.05,.269,[.026,.34,.09],'periwinkle')
 for yy in[.90,1.20]:part('ControlEdge'+str(yy),0,yy,.267,[.66,.026,.08],'periwinkle')
 # cash/card slots are open dark recessed wells with stainless upper/lower/side lips.
 ports=[('BillAccept',-.12,1.114,.24,.027),('CardReader',.222,1.127,.105,.039),('TicketOutlet',-.115,.995,.285,.061),('ReceiptPort',.225,1.045,.105,.033),('CoinReturn',.225,.963,.11,.040)]
 for nm,x,yy,w,h in ports:
  part(nm+'_Recess',x,yy,.242,[w,h,.009],'black',col=False)
  for edge,xx,yyy,ww,hhh,dd in ring(nm,w+.022,h+.021,w,h,.035):part(nm+'_'+edge,x+xx,yy+yyy,.266,[ww,hhh,dd],'steel',col=False)
 part('CoinInputBezel',.226,1.178,.272,[.062,.032,.018],'steel',mesh='rounder',col=False)
 part('CoinInputSlot',.226,1.178,.284,[.046,.006,.005],'black',col=False)
 # A real projected collection tray with lip, side returns and depressed dark interior.
 part('TrayBase',0,.845,.342,[.70,.034,.245],'periwinkle');part('TrayBlackInterior',0,.866,.333,[.56,.006,.155],'black',col=False)
 part('TrayFrontLip',0,.883,.463,[.70,.046,.028],'periwinkle');
 for x in[-.337,.337]:part('TraySideWall'+str(x),x,.89,.351,[.026,.065,.242],'periwinkle')
 # physical membrane buttons, speaker ports and fasteners
 for k in range(3):part('FunctionButton'+str(k),-.245,1.18-k*.045,.286,[.053,.019,.009],'palecyan',col=False)
 for k in range(4):part('SpeakerSlit'+str(k),.287,1.208+k*.012,.274,[.033,.004,.006],'black',col=False)
 for x in[-.315,.315]:
  for yy in[.925,1.195,1.625]:part('FaceFastener'+str(x)+'_'+str(yy),x,yy,.300,[.009,.009,.003],'steel',mesh='rounder',col=False)
 # six real short side ventilation louvres per cabinet, physical security lock
 for k in range(6):part('SideVent'+str(k),.407,.345+k*.032,-.035,[.008,.009,.14],'darksteel',col=False)
 part('ServiceLock',.307,.67,.282,[.017,.017,.004],'steel',mesh='rounder',col=False)
 part('PrintedTicketsSourceContours',0,.461,.282,[1,1,1],'palecyan',mesh='machine-tickets-word',col=False)
 # Cable routes follow own cabinet back into installed equipment carrier, topology MODEL.
 for k,(yy,zz,hh,dd) in enumerate([(.22,-.291,.35,.014),(.06,-.51,.014,.44)]):part('PowerCableConduit'+str(k),.29,yy,zz,[.017,hh,dd],'black',col=False)
 part('RearSocketCover',.29,.315,-.279,[.08,.09,.016],'darksteel',col=False)
# Independent timetable installation notice backing and actual source-derived printed notice contours.
notice=warp(b,[[678,359],[906,385],[906,513],[677,490]],500,310,'delay-notice')
hh=cv2.cvtColor(notice,cv2.COLOR_BGR2HSV)
obj('DelayNoticeBacking',-.04,2.89,-.57,[1.32,.84,.025],'white',group='EquipmentNotices')
solid('delay-notice-blue',notice,(hh[:,:,0]>85)&(hh[:,:,0]<145)&(hh[:,:,1]>50),1.27,.79,-.04,2.89,-.552,'navy',.0015,3)
solid('delay-notice-dark',notice,(hh[:,:,2]<110)&(hh[:,:,1]<120),1.27,.79,-.04,2.89,-.550,'navy',.0015,3)
# Marking strips are physical source markings only; main floor unchanged.
for i,u in enumerate(centres):obj(f'WaitingLineStrip_{i+1}',u,.001,-1 if False else 1.00,[.84,.0015,.045],'yellow',group='PhysicalFloorMarkings',collider=False)
materials={'offwhite':{'color':[.83,.84,.83,1],'smoothness':.43},'white':{'color':[.93,.94,.94,1]},'steel':{'color':[.56,.59,.62,1],'metallic':.83,'smoothness':.72},'darksteel':{'color':[.15,.17,.18,1],'metallic':.72,'smoothness':.48},'black':{'color':[.013,.017,.021,1]},'charcoal':{'color':[.035,.038,.044,1]},'navy':{'color':[.012,.037,.15,1]},'periwinkle':{'color':[.25,.36,.64,1],'smoothness':.34},'cyan':{'color':[.015,.38,.68,1]},'palecyan':{'color':[.32,.61,.73,1]},'yellow':{'color':[.93,.75,.02,1]},'red':{'color':[.60,.015,.025,1]},'screenblack':{'color':[.006,.014,.022,1],'metallic':.1,'smoothness':.88}}
for i in range(2):materials['screen'+str(i)]={'color':[1,1,1,1],'texture_asset':'Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/SelfTicket/UI/Display_'+str(i+1)+'_RuntimeScreen.png','smoothness':.65,'emission':[.12,.16,.13,1]}
packet={'store_key':'self-ticket','floor_world_y':7,'origin_q':origin,'materials':materials,'mesh_library':lib,'objects':objs,'source_imports':assets,'source_packet':'.state/ACTIVE-ALL2F-COMPLETION-R26/public/self-ticket/constructor/SOURCE-EVIDENCE-R26.json','classification':'SOURCE_REFERENCED_VISIBLE_EQUIPMENT_MODEL_2023_EPOCH_NO_SURVEYED_EQUALITY','selected_actual_clear_span':[27.875890887951087,34.834027290892024],'placement_line_actual_q':{'origin':C.tolist(),'tangent':T.tolist(),'normal':N.tolist(),'public_direction':'positive actualqy','packet_warning':'qx,-actualqy'},'source_signs':signs,'new_room_or_structural_wall':False,'native_geometry_changes':False,'Light_or_Text':False}
p=BASE/'SELF-TICKET-PACKET-R26.json';p.write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
print('packet',p,'objects',len(objs),'meshprototypes',len(lib),'bytes',p.stat().st_size)
