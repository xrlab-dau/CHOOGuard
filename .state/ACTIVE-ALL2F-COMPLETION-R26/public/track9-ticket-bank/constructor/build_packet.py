from pathlib import Path
import json,math,hashlib,sys,cv2,numpy as np
ROOT=Path('/Users/um-yunsang/CHOOGuard');B=Path(__file__).resolve().parent;S=B/'sources';S.mkdir(exist_ok=True)
sys.path.insert(0,str(ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'));from photo_outline_to_solid import closed_solid_from_mask
SOURCE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/service-fronts/sources/baitfishingrod-224287119584/SOURCE-06.jpg';im=cv2.imread(str(SOURCE))
left=-3.0840591960538477;right=4.512008922750918;C=(left+right)/2;W=right-left-.04
Q=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]])
world=Q@np.array([C,0.]);fw=Q@np.array([0.,1.]);yaw=math.degrees(math.atan2(fw[0],fw[1]));objs=[];meshes={}
def o(name,pos,size,mat='white',group='ActualFourTicketMachines',kind='box',euler=(0,0,0),collider=False,**kw):
 d={'id':name,'kind':kind,'position':list(pos),'scale':list(size),'euler':list(euler),'material':mat,'group':group,'collider':collider,'source_ids':['BAITFISHINGROD-202605-SOURCE06-TRACK9-FOUR-TVM'],'qualification':'ACTUAL_VISIBLE_SOURCE_FAMILY_PHOTO_PROPORTIONED_MODEL_IN_FIXED_NATIVE_BAY_NOT_SURVEYED'};d.update(kw);objs.append(d)
def rounded(key,w,h,d,r=.035):
 pts=[]
 for cx,cy,start in[(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
  for i in range(9):
   a=math.radians(start+i*90/8);pts.append([cx+r*math.cos(a),cy+r*math.sin(a)])
 n=len(pts);v=[[x,y,z]for z in[-d/2,d/2]for x,y in pts];t=[]
 for i in range(1,n-1):t.extend([0,i+1,i,n,n+i,n+i+1])
 for i in range(n):
  j=(i+1)%n;t.extend([i,j,n+j,i,n+j,n+i])
 meshes[key]={'vertices':v,'triangles':t}
def rr(name,pos,size,mat='white',group='ActualFourTicketMachines',r=.035,collider=False):
 key=name+'_shape';rounded(key,*size,min(r,size[0]/3,size[1]/3));o(name,pos,(1,1,1),mat,group,'mesh',collider=collider,mesh_key=key)
def shape(name,verts,tris,pos=(0,0,0),mat='navy',group='PhysicalSourceContourSigns',scale=(1,1,1),euler=(0,0,0)):
 meshes[name]={'vertices':verts,'triangles':tris};o(name,pos,scale,mat,group,'mesh',euler,mesh_key=name)
def warp(quad,w,h,name):
 H=cv2.getPerspectiveTransform(np.array(quad,np.float32),np.array([[0,0],[w-1,0],[w-1,h-1],[0,h-1]],np.float32));p=cv2.warpPerspective(im,H,(w,h));cv2.imwrite(str(S/(name+'.png')),p);return p
fas=warp([[0,111],[852,103],[855,192],[0,200]],1700,190,'Actual-Track9-Tickets-Fascia-Rectified');hsv=cv2.cvtColor(fas,cv2.COLOR_BGR2HSV);mask=(hsv[:,:,1]<115)&(hsv[:,:,2]>146);mask[:22]=False;mask[-13:]=False;mask[:,:55]=False;mask[:,-55:]=False
cv2.imwrite(str(S/'Actual-Track9-Tickets-Fascia-Contour.png'),mask.astype('uint8')*255);m=closed_solid_from_mask(mask.astype('uint8')*255,W,.71,.004,minimum_area=8,epsilon=.3);meshes['ActualSourceTicketsMultilingual']=m
o('ActualSourceTicketsMultilingual',(0,3.13,-.001),(1,1,1),'white','PhysicalSourceContourSigns','mesh',mesh_key='ActualSourceTicketsMultilingual')
# Source equipment cladding, NOT new architectural walls. Central retained partition tip remains fixed and is reused between the 2+2 machines.
o('Cobalt_Fascia_Actual_Source',(0,3.13,-.021),(W,.73,.035),'navy','SourceEquipmentRecess',collider=True)
# Carrier back sheets stop around the actual existing central wall thickness (qx .586.. .785).
central_lo=.586028618316381;central_hi=.80371765391317;actual_sides=[(left+.02,central_lo-.008),(central_hi+.008,right-.02)]
for k,(a,b)in enumerate(actual_sides):
 x=C-(a+b)/2;ww=b-a;o('White_Equipment_Carrier_'+str(k),(x,1.43,-.695),(ww,2.65,.028),'white','SourceEquipmentRecess')
 o('Dark_Equipment_Plinth_'+str(k),(x,.075,-.57),(ww,.15,.25),'darkgray','SourceEquipmentRecess')
for sign in[-1,1]:
 x=sign*(W/2-.055);o('Cobalt_Source_Vertical_Return_'+str(sign),(x,1.40,-.019),(.11,2.80,.035),'navy','SourceEquipmentRecess')
 o('Perimeter_Metal_Jamb_'+str(sign),(x-sign*.069,1.39,-.013),(.018,2.75,.021),'steel','SourceEquipmentRecess')
 o('Perimeter_White_Fixture_Diffuser_'+str(sign),(x-sign*.082,1.39,-.001),(.008,2.74,.009),'diffuser','PhysicalOnlyFixture')
o('Perimeter_Metal_Header',(0,2.765,-.013),(W-.24,.018,.021),'steel','SourceEquipmentRecess')
o('Perimeter_White_Header_Diffuser',(0,2.752,-.001),(W-.25,.008,.009),'diffuser','PhysicalOnlyFixture')
# Runtime touchscreen UI is a source electronic display, separate from physical sign bodies.
ui=warp([[106,272],[151,273],[150,359],[103,359]],320,600,'Track9-Touchscreen-Runtime-UI');ui_file=S/'Track9-Touchscreen-Runtime-UI.png'
# Exact visible printed face legend near the top right of first unit, not a font/text component.
legend=warp([[166,268],[187,268],[187,299],[166,299]],105,155,'Actual-Machine-Face-Legend');hh=cv2.cvtColor(legend,cv2.COLOR_BGR2HSV);lm=(hh[:,:,1]<125)&(hh[:,:,2]>126);lm[:5]=False;lm[-5:]=False;lm[:,:4]=False;lm[:,-4:]=False
cv2.imwrite(str(S/'Actual-Machine-Face-Legend-Contour.png'),lm.astype('uint8')*255);meshes['MachineSourceFaceLegend']=closed_solid_from_mask(lm.astype('uint8')*255,.154,.226,.0013,minimum_area=3,epsilon=.25)
# All four bodies repeat the visible same installed hardware family, with real separate panels and wells.
centres=np.array([3,1,-1,-3])*W/8
for k,x in enumerate(centres):
 g=f'Track9_TVM_{k+1:02d}';front=-.055;back=-.645;w=.84
 def p(n,dx,y,z,size,mat='black',kind='box',euler=(0,0,0),col=False,**kw):o(g+'_'+n,(x+dx,y,z),size,mat,g,kind,euler,col,**kw)
 rr(g+'_Black_Rounded_Face',(x,1.48,front-.035),(.84,1.38,.065),'black',g,.045)
 p('Rear_Gray_Cabinet',0,1.15,back+.03,(.84,1.94,.06),'gray',col=True)
 for side in[-1,1]:p('Side_Gray_Shell_'+str(side),side*.412,1.15,(front+back)/2,(.035,1.94,.57),'gray',col=True)
 p('Upper_Gray_Cap',0,2.155,(front+back)/2,(.86,.055,.59),'gray',col=True)
 p('Gray_Pedestal_Lower_Door',0,.445,front-.038,(.80,.61,.045),'gray',col=True)
 for yy in[.148,.737]:p('Pedestal_Service_Seam_'+str(yy),0,yy,front-.010,(.75,.008,.004),'darkgray')
 for dx in[-.394,.394]:p('Pedestal_Side_Seam_'+str(dx),dx,.443,front-.009,(.005,.58,.004),'darkgray')
 p('Base_Foot_Platform',0,.075,-.34,(.89,.09,.64),'darkgray',col=True)
 for dx in[-.32,.32]:
  for zz in[-.56,-.15]:p('Rubber_Leveling_Foot_'+str(dx)+'_'+str(zz),dx,.024,zz,(.063,.042,.065),'black')
 p('Touchscreen_Black_Recess',.17,1.716,front+.003,(.47,.87,.008),'screendark')
 p('Actual_Digital_Touchscreen_UI',.17,1.716,front+.009,(.405,.785,.006),'screenui')
 # narrow right-hand control tower is image-right, hence local negative X.
 p('Source_Instruction_Legend',-.25,1.965,front+.008,(1,1,1),'white','mesh',mesh_key='MachineSourceFaceLegend')
 p('Card_Reader_Recess',-.255,1.46,front-.002,(.12,.16,.022),'darksteel')
 p('Card_Reader_White_Bezel',-.255,1.46,front+.019,(.078,.083,.008),'steel')
 p('Card_Reader_Black_Payment_Well',-.255,1.46,front+.025,(.056,.059,.008),'black')
 p('Card_Insert_Slot',-.255,1.40,front+.019,(.094,.011,.010),'steel')
 p('Card_Insert_Dark_Interior',-.255,1.40,front+.025,(.078,.004,.006),'black')
 p('Receipt_Black_Well',-.255,1.31,front+.001,(.135,.039,.020),'darksteel')
 for dx in[-.065,.065]:p('Receipt_Side_Lip_'+str(dx),-.255+dx,1.31,front+.019,(.012,.046,.021),'steel')
 p('Receipt_Upper_Lip',-.255,1.329,front+.019,(.14,.010,.021),'steel')
 p('Receipt_Lower_Lip',-.255,1.290,front+.019,(.14,.010,.021),'steel')
 p('Bill_Acceptor_Black_Recess',-.235,1.12,front+.001,(.16,.07,.013),'darksteel')
 p('Bill_Acceptor_Metal_Upper_Lip',-.235,1.148,front+.021,(.16,.016,.027),'steel')
 p('Bill_Acceptor_Metal_Lower_Lip',-.235,1.093,front+.021,(.16,.016,.027),'steel')
 p('Ticket_Collection_Dark_Aperture',.12,.985,front-.004,(.43,.14,.018),'screendark')
 for dx in[-.221,.221]:p('Ticket_Aperture_Side_'+str(dx),.12+dx,.985,front+.025,(.018,.16,.065),'black')
 p('Ticket_Aperture_Upper_Lip',.12,1.059,front+.025,(.46,.018,.065),'black')
 p('Ticket_Aperture_Lower_Tray',.12,.913,front+.039,(.46,.020,.09),'gray')
 p('Tray_Projected_Gray_Shelf',0,.779,front+.081,(.92,.052,.25),'gray',col=True)
 p('Tray_Recessed_Black_Bed',0,.810,front+.08,(.75,.008,.16),'black')
 for dx in[-.442,.442]:p('Tray_Side_Edge_'+str(dx),dx,.820,front+.08,(.026,.055,.23),'gray')
 for row in range(4):
  for col in range(3):p(f'Touch_Control_Tactile_Button_{row}_{col}',-.245+(col-1)*.029,1.265-row*.024,front+.019,(.018,.014,.009),'darksteel')
 for j in range(6):p('Speaker_Grille_Slit_'+str(j),-.258,1.67+j*.012,front+.009,(.077,.003,.005),'darksteel')
 for dx in[-.382,.382]:
  for yy in[.94,1.78,2.12]:p('Front_Security_Fastener_'+str(dx)+'_'+str(yy),dx,yy,front+.008,(.008,.008,.003),'steel')
 p('Service_Door_Key_Cylinder',-.33,.615,front-.009,(.016,.005,.016),'steel','cylinder',(90,0,0))
 for j in range(7):p('Lower_Side_Vent_'+str(j),.433,.27+j*.025,-.37,(.005,.008,.21),'darkgray')
 # Keep unseen service wiring and internal printing mechanisms absent.
materials={'white':([.81,.82,.79,1],0,.42),'navy':([.008,.039,.16,1],.12,.45),'gray':([.40,.43,.43,1],.36,.51),'darkgray':([.045,.056,.057,1],.12,.35),'black':([.006,.010,.013,1],.17,.68),'darksteel':([.05,.07,.075,1],.66,.63),'steel':([.52,.57,.58,1],.92,.75),'screendark':([.002,.008,.010,1],.15,.88),'diffuser':([.88,.91,.89,1],0,.62),'screenui':([1,1,1,1],.05,.7)}
mm={k:{'color':c,'metallic':m,'smoothness':sm}for k,(c,m,sm)in materials.items()};mm['screenui']['texture_asset']='Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/Track9TicketBank/UI/Track9-Touchscreen-Runtime-UI.png'
snap=json.loads((ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-001/CURRENT-2F-MESH-SNAPSHOT.json').read_text());anchor=next(o for o in snap['objects']if '-32779078-0' in o['object_id']);a=(np.array(anchor['bounds_min_world'])+anchor['bounds_max_world'])/2
credit='Actual source May2026 https://m.blog.naver.com/baitfishingrod/224287119584 SOURCE06. Four modern gray/black installed ticket machine family and multilingual ticket fascia directly visible; repeated ports geometry photo-proportioned MODEL, not manufacturer CAD or survey. Source header and face legend made as closed actual-ink contour meshes with counters. Electronic screen UI is separately source-cropped from actual screen, not physical sign substitution. Independent source-shaped cabinet geometry used because publicly downloaded candidate free low-poly ticket machine was British EMR hardware and differs; no wrong-brand donor substituted. Original/free asset search record carried in SOURCE-EVIDENCE-R26.json. Hidden camera, photo seat, curtain, internal service equipment omitted. This is a ticket bank, not a photo booth. No native/structural wall modifications. Existing5machine bank elsewhere stays fixed.\n'
p={'schema':'chooguard.source_bound_track9_four_tvm.r26.v1','author':'classic:/root/r26_photo_booth','scope':'C180_R26_TRACK9_TICKET_BANK_APPLY_001','existing_anchor_id':anchor['object_id'],'existing_anchor_path':anchor['path'],'anchor_world_xz':a[[0,2]].tolist(),'origin_world_xz':world.tolist(),'origin_actual_q':[C,0.],'floor_world_y':7.,'yaw_world':yaw,'asset_folder':'Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/Track9TicketBank','parent_path':'FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT','group_name':'R26_TRACK9_FOUR_MODERN_TICKET_MACHINES_SOURCE_MODEL','materials':mm,'mesh_library':meshes,'objects':objs,'source_imports':[{'source':str(ui_file),'destination':'UI/'+ui_file.name,'sha256':hashlib.sha256(ui_file.read_bytes()).hexdigest()}],'binding_qualification':'Current May2026 nurse/OliveYoung/Track9 bank source correspondence. Four modern machines fitted by current front pier clear faces; actual retained central generated partition tip reused between middle machines. Photographic dimensions MODEL, not measured asbuilt. All existing native/current architectural geometry preserved.','attribution':credit,'source_visible_machine_count':4,'current_clear_qx':[left,right],'central_wall_retained_qx':[central_lo,central_hi],'existing_different5machine_bank_untouched':True}
(B/'TRACK9-TICKET-BANK-PACKET-R26.json').write_text(json.dumps(p,separators=(',',':'),allow_nan=False));(B/'SOURCE-ATTRIBUTION.txt').write_text(credit);print('objects',len(objs),'meshlib',len(meshes),'origin',p['origin_actual_q'],'yaw',yaw,'sha',hashlib.sha256((B/'TRACK9-TICKET-BANK-PACKET-R26.json').read_bytes()).hexdigest())
