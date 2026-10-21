import sys,json,hashlib,datetime,math,cv2,numpy as np
from pathlib import Path
from shapely.geometry import Polygon,box
from shapely import constrained_delaunay_triangles
from shapely.geometry.polygon import orient
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=Path(__file__).resolve().parent;SRC=BASE/'sources'
sys.path.insert(0,str(ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'))
from photo_outline_to_solid import closed_solid_from_mask
lib={};objects=[];imports=[];evidence=[]
C=np.array([38.5,-23.0]); floor=7.0
# One observed unit, qx is outward normal and qy the long cabinet tangent.
def add(name,u,y,n,size,mat,kind='cube',mesh=None,group='Bank',collider=False,yaw=90):
 o={'id':name,'kind':kind,'group':group,'q':[float(C[0]+n),float(-C[1]-u)],'y':y,'material':mat,'yaw':yaw,'collider':collider,'qualification':'2026_SOURCE_OBSERVED_TYPE_PHOTO_PROPORTIONED_PLACEMENT_MODEL_NOT_SURVEYED','source_ids':['SNOWRUN_20261001_08_10_12','YONILEE_20260928_07']}
 if size is not None:o['size']=size
 if mesh:o['mesh_key']=mesh
 objects.append(o);return o
def mesh_from_poly(p,depth=.02):
 p=orient(p,sign=1);vs=[];ts=[]
 def tri(a,b,c):
  i=len(vs);vs.extend([a,b,c]);ts.extend([i,i+1,i+2])
 for t in constrained_delaunay_triangles(p).geoms:
  a,b,c=list(t.exterior.coords)[:3]
  if np.cross(np.array(b)-a,np.array(c)-a)<0:b,c=c,b
  tri([*a,depth/2],[*b,depth/2],[*c,depth/2]);tri([*c,-depth/2],[*b,-depth/2],[*a,-depth/2])
 for ring in [p.exterior,*p.interiors]:
  pts=list(ring.coords)
  for a,b in zip(pts,pts[1:]):
   aa=[*a,-depth/2];bb=[*b,-depth/2];cc=[*b,depth/2];dd=[*a,depth/2];tri(aa,bb,cc);tri(aa,cc,dd)
 return {'vertices':vs,'triangles':ts}
def cylinder_mesh(radius,height,axis='z',segments=16):
 vs=[];ts=[]
 for s in [-1,1]:
  for i in range(segments):
   a=i*math.tau/segments;v=[radius*math.cos(a),radius*math.sin(a),s*height/2]
   if axis=='x':v=[v[2],v[0],v[1]]
   if axis=='y':v=[v[0],v[2],v[1]]
   vs.append(v)
 for i in range(segments):
  j=(i+1)%segments;ts.extend([i,j,segments+j,i,segments+j,segments+i])
 for i in range(1,segments-1):ts.extend([0,i+1,i,segments,segments+i,segments+i+1])
 return {'vertices':vs,'triangles':ts}
def warp(im,quad,width,height,name):
 H=cv2.getPerspectiveTransform(np.array(quad,np.float32),np.array([[0,0],[width-1,0],[width-1,height-1],[0,height-1]],np.float32));a=cv2.warpPerspective(im,H,(width,height));cv2.imwrite(str(SRC/(name+'-rectified.jpg')),a);return a
def contours(name,im,mask,w,h,u,y,n,mat,minimum=3,group='PhysicalSourceArtwork',yaw=90):
 cv2.imwrite(str(SRC/(name+'-contour-mask.png')),mask.astype('uint8')*255)
 m=closed_solid_from_mask(mask.astype('uint8')*255,w,h,.0015,minimum,.30);lib[name]=m
 add(name,u,y,n,[1,1,1],mat,'library_mesh',name,group,False,yaw)
 evidence.append({'name':name,'polygons':m['source_polygons'],'depth':.0015,'actual_photo_contours':True,'photo_sign_plane':False})
im10=cv2.imread(str(SRC/'original-snowrun-10.jpg'));im08=cv2.imread(str(SRC/'original-snowrun-08.jpg'));im12=cv2.imread(str(SRC/'original-snowrun-12.jpg'))
# Narrow actual inspection window is a hole in steel with independent tinted glass.
door=box(-.304,-.5225,.304,.5225).difference(box(-.139,-.322,-.081,.322))
lib['steel_door_with_actual_inspection_aperture']=mesh_from_poly(door,.024)
lib['lock_barrel']=cylinder_mesh(.009,.014)
lib['hinge_barrel']=cylinder_mesh(.008,.042,'y',12)
lib['top_hood_cross_section']=mesh_from_poly(Polygon([[-.39,0],[.39,0],[0,.24]]),8.66)
# Source exact body source is a downloaded CC-BY hollow cabinet, independently fitted.
source_shell='Assets/ChooGuard/Art/StationInterior/Downloaded1FLockers_20261019/Meshes/Downloaded_Hollow_Cabinet_Shell.asset'
source_lock='Assets/ChooGuard/Art/StationInterior/Downloaded1FLockers_20261019/Meshes/Downloaded_Lock_Barrel.asset'
width=.62;kw=.60;length=13*width+kw;left=-length/2
centres=[left+width*(i+.5)+(kw if i>=6 else 0) for i in range(13)]
kiosk_u=left+6*width+kw/2
for i,u in enumerate(centres):
 g=f'Compartments_{901+2*i}_{902+2*i}'
 o=add(g+'_DownloadedHollowShell',u,.065,0,None,'steelinside','mesh_ref',group=g,collider=True);o['mesh_asset']=source_shell;o['fit_size']=[.616,2.175,.75]
 add(g+'_InternalHorizontalShelf',u,1.145,0,[.588,.018,.704],'steelinside',group=g)
 add(g+'_BaseRail',u,.050,0,[.62,.10,.78],'darksteel',group=g,collider=True)
 for tier,y in enumerate([1.6825,.610]):
  number=901+i*2+tier;name=f'Locker_{number}'
  add(name+'_PowdercoatDoorWithTrueWindowHole',u,y,.391,[1,1,1],'cyan','library_mesh','steel_door_with_actual_inspection_aperture',g,True)
  add(name+'_DarkInspectionGlazing',u-.110,y,.390,[.055,.638,.006],'inspection',group=g)
  add(name+'_SilverLockHousing',u+.170,y-.015,.416,[.049,.086,.018],'silver',group=g)
  add(name+'_DarkLockRecess',u+.170,y-.025,.427,[.031,.037,.003],'black',group=g)
  add(name+'_KeyBarrel',u+.170,y-.025,.430,[1,1,1],'silver','library_mesh','lock_barrel',g)
  add(name+'_NumberBadge',u+.170,y+.014,.429,[.034,.018,.002],'white',group=g)
  # Upper/lower real pin hinges remain separate editable hardware.
  for hi,hy in enumerate([-.405,.405]):
   add(name+f'_Hinge{hi}_Leaf',u-.289,y+hy,.410,[.022,.041,.006],'silver',group=g)
   add(name+f'_Hinge{hi}_Pin',u-.300,y+hy,.414,[1,1,1],'silver','library_mesh','hinge_barrel',g)
 # low rear and top ventilation perforation recesses; physical perforated strip.
 for j in range(4):add(g+f'_BaseVent_{j}',u-.05+j*.022,.040,.395,[.007,.022,.003],'black',group=g)
# Gabled top is a real continuous closed metal hood, not a sign plane.
# top_hood is x/y cross-section with long z; orient long z onto qy through yaw0.
add('PitchedHood',0,2.24,0,[1,1,1],'roofblue','library_mesh','top_hood_cross_section','Hood',False,0)
for side in[-1,1]:
 add('EndCap_'+str(side),side*(length/2+.008),1.13,0,[.022,2.24,.78],'cyan',group='EndCaps',collider=True)
add('ContinuousBackSheet',0,1.14,-.385,[length,2.22,.022],'cyan',group='BackSheet',collider=True)
# Payment terminal after sixth compartment as actually shown on source12 UI.
g='PaymentTerminal'
add('KioskRearCase',kiosk_u,1.13,0,[kw-.008,2.24,.75],'navy',group=g,collider=True)
for x in[-1,1]:add('KioskFaceSide_'+str(x),kiosk_u+x*.258,1.26,.399,[.074,1.91,.045],'navy',group=g)
add('KioskTopPlate',kiosk_u,2.145,.408,[.596,.196,.043],'navy',group=g)
add('KioskLowerServiceDoor',kiosk_u,.345,.405,[.59,.59,.036],'navy',group=g)
add('KioskScreenBackRecess',kiosk_u,1.445,.406,[.438,.627,.020],'black',group=g)
for name,u,y,w,h in [('L',-.231,1.445,.026,.682),('R',.231,1.445,.026,.682),('T',0,1.773,.438,.027),('B',0,1.117,.438,.027)]:add('ScreenBezel_'+name,kiosk_u+u,y,.429,[w,h,.035],'darksteel',group=g)
# Runtime equipment screen content is distinct from modeled physical signs.
screen=warp(im12,[[365,0],[2570,0],[2090,1595],[742,1600]],720,920,'actual-payment-runtime-UI')
f=SRC/'actual-payment-runtime-UI.png';cv2.imwrite(str(f),screen);imports.append({'source':str(f),'destination':'UI/actual-payment-runtime-UI.png','sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
add('RecessedTouchscreen',kiosk_u,1.445,.417,[.435,.624,.008],'screen',group=g)
add('ControlConsoleSlopingBacking',kiosk_u,.999,.435,[.50,.213,.055],'silver',group=g)
add('BillRecess',kiosk_u-.115,1.030,.469,[.208,.026,.007],'black',group=g)
for e,yy in [('top',1.05),('bottom',1.011)]:add('BillLip_'+e,kiosk_u-.115,yy,.477,[.228,.009,.022],'silver',group=g)
add('KeypadBlueInset',kiosk_u-.084,.950,.477,[.274,.076,.015],'keyblue',group=g)
for row in range(2):
 for col in range(6):add(f'PhysicalKey_{row}_{col}',kiosk_u-.190+col*.042,.969-row*.038,.491,[.029,.026,.009],'silver',group=g)
add('OrangeCardReaderBody',kiosk_u+.184,1.022,.510,[.109,.173,.095],'orange',group=g)
add('CardReaderBlackScreen',kiosk_u+.184,1.029,.561,[.080,.102,.008],'black',group=g)
add('CardReaderGreenDisplayGlass',kiosk_u+.184,1.039,.567,[.058,.055,.005],'displayglass',group=g)
add('CardInsertionAperture',kiosk_u+.184,1.096,.560,[.074,.011,.014],'black',group=g)
add('ReceiptOutletRecess',kiosk_u+.020,.855,.436,[.185,.026,.008],'black',group=g)
add('ReceiptOutletLowerLip',kiosk_u+.020,.836,.452,[.211,.012,.033],'silver',group=g)
add('LowerServiceLock',kiosk_u+.225,.552,.432,[1,1,1],'silver','library_mesh','lock_barrel',g)
add('BaseServiceVent',kiosk_u,.140,.428,[.232,.077,.008],'black',group=g)
for j in range(6):add(f'ServiceVentFin_{j}',kiosk_u,.111+j*.012,.436,[.220,.004,.008],'darksteel',group=g)
# Physical serial markings transcribe the same actual observed screen unit901..926
# by contours, without generating text or using a font atlas.
top_points=[(452,586),(532,586),(608,586),(682,587),(755,588),(829,588),(970,589),(1038,589),(1107,589),(1175,589),(1241,589),(1307,589),(1371,590)]
bottom_points=[(490,797),(561,796),(630,797),(698,796),(765,795),(832,795),(959,791),(1024,790),(1086,789),(1148,788),(1210,788),(1271,786),(1330,786)]
for i,u in enumerate(centres):
 for tier,points,y in [(0,top_points,1.6825),(1,bottom_points,.610)]:
  cx,cy=points[i];crop=im12[int((cy-10)*3000/1920):int((cy+10)*3000/1920),int((cx-17)*3000/1920):int((cx+17)*3000/1920)]
  h=cv2.cvtColor(crop,cv2.COLOR_BGR2HSV);mm=(h[:,:,1]<92)&(h[:,:,2]>179)
  mm[:2]=False;mm[-2:]=False
  name=f'Number_{901+2*i+tier}_SourceContour';cv2.imwrite(str(SRC/(name+'-source.jpg')),crop)
  m=closed_solid_from_mask(mm.astype('uint8')*255,.029,.013,.0005,.7,.12);lib[name]=m
  add(name,u+.170,y+.014,.431,[1,1,1],'navy','library_mesh',name,f'Compartments_{901+2*i}_{902+2*i}')
# Source top physical camera and status carrier; no light components.
add('CameraSupport',kiosk_u,2.465,.02,[.065,.24,.070],'silver',group='Camera')
add('CameraWhiteDome',kiosk_u,2.515,.12,[.165,.125,.165],'white','sphere',group='Camera')
add('CameraLens',kiosk_u,2.505,.198,[.066,.053,.016],'black','sphere',group='Camera')
# Close source-contour kiosk wording from actual original photo10.
# Source3000 photograph corresponds to displayed966 coordinates, use exact original scale.
s=3000/1920
sign=warp(im10,np.array([[1067,447],[1104,462],[1104,496],[1067,482]])*s,300,180,'kiosk-actual-physical-word')
hsv=cv2.cvtColor(sign,cv2.COLOR_BGR2HSV);mask=(hsv[:,:,1]<80)&(hsv[:,:,2]>172);mask[:9]=False;mask[-8:]=False
contours('KioskActualWordContours',sign,mask,.40,.113,kiosk_u,2.146,.433,'white',3,group='PaymentTerminal')
# Source08 BUSAN and source decoration extracted in physical contours with counters.
# rectification of same door face; strictly color contours, never the source image as substrate.
word=warp(im08,np.array([[803,709],[914,732],[914,790],[803,764]])*s,700,260,'actual-busan-word')
hh=cv2.cvtColor(word,cv2.COLOR_BGR2HSV);mm=(hh[:,:,1]<82)&(hh[:,:,2]>165);mm[:8]=0;mm[-8:]=0
contours('BUSANActualContourMarking',word,mm,1.42,.28,1.38,1.173,.407,'white',14)
# Exact gull/lighthouse visible silhouette from nearest source10 lower door panels.
deco=warp(im10,np.array([[434,904],[963,721],[963,1162],[434,1280]])*s,960,690,'actual-gull-lighthouse')
hh=cv2.cvtColor(deco,cv2.COLOR_BGR2HSV)
for nm,mask,mat in [('WhiteSeaArtwork',(hh[:,:,1]<78)&(hh[:,:,2]>150),'white'),('NavyGullTips',(hh[:,:,0]>90)&(hh[:,:,0]<135)&(hh[:,:,1]>100)&(hh[:,:,2]<145),'navy'),('RedLighthouseCap',((hh[:,:,0]<12)|(hh[:,:,0]>170))&(hh[:,:,1]>115)&(hh[:,:,2]>105),'red')]:
 mask[:5]=0;mask[-5:]=0;contours(nm,deco,mask,2.22,.98,-2.20,.61,.410,mat,18)
# End-cap artwork uses actual source08 side panel, transformed into its physical plane.
side=warp(im08,np.array([[1233,454],[1515,466],[1505,1180],[1228,1262]])*s,460,1000,'actual-endcap')
hh=cv2.cvtColor(side,cv2.COLOR_BGR2HSV)
for nm,mask,mat in [('EndCapWhiteArtwork',(hh[:,:,1]<92)&(hh[:,:,2]>130),'white'),('EndCapDarkWaves',(hh[:,:,0]>95)&(hh[:,:,0]<135)&(hh[:,:,1]>80)&(hh[:,:,2]<125),'navy')]:
 mask[:12]=0;mask[-12:]=0
 m=closed_solid_from_mask(mask.astype('uint8')*255,.73,2.17,.0015,14,.4);lib[nm]=m
 o=add(nm,length/2+.021,1.14,0,[1,1,1],mat,'library_mesh',nm,'EndCaps',False,180)
 cv2.imwrite(str(SRC/(nm+'-contour-mask.png')),mask.astype('uint8')*255)
 evidence.append({'name':nm,'actual_source8_endcap':True,'closed_polygons':m['source_polygons'],'photo_plane':False})
# Actual service-price notice from same public source11, physically modeled contours.
source11=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/rail-police/sources/snowrun-11.jpg';pr=cv2.imread(str(source11))
price=warp(pr,[[287,177],[714,221],[752,530],[253,526]],560,440,'actual-price-notice')
hh=cv2.cvtColor(price,cv2.COLOR_BGR2HSV);mask=(hh[:,:,2]<145)&(hh[:,:,1]<135);mask[:8]=0;mask[-8:]=0
add('ActualPriceNoticeBacking',kiosk_u+.50,1.88,.414,[.36,.28,.002],'white',group='PhysicalSourceArtwork')
contours('ActualPriceNoticeContours',price,mask,.36,.28,kiosk_u+.50,1.88,.417,'navy',5)
materials={
 'cyan':{'color':[.036,.65,.79,1],'metallic':.25,'smoothness':.76},
 'roofblue':{'color':[.055,.39,.68,1],'metallic':.18,'smoothness':.58},
 'navy':{'color':[.035,.10,.23,1],'metallic':.15,'smoothness':.52},
 'steelinside':{'color':[.10,.15,.18,1],'metallic':.60,'smoothness':.40},
 'darksteel':{'color':[.17,.20,.24,1],'metallic':.7,'smoothness':.52},
 'silver':{'color':[.68,.72,.72,1],'metallic':.84,'smoothness':.65},
 'inspection':{'color':[.012,.025,.031,.90],'metallic':.10,'smoothness':.83,'transparent':True},
 'white':{'color':[.87,.92,.92,1],'metallic':.05,'smoothness':.5},
 'black':{'color':[.006,.009,.013,1],'metallic':.12,'smoothness':.6},
 'keyblue':{'color':[.045,.19,.47,1],'metallic':.1,'smoothness':.4},
 'orange':{'color':[1,.35,.026,1],'metallic':.05,'smoothness':.58},
 'red':{'color':[.80,.13,.11,1],'smoothness':.45},
 'displayglass':{'color':[.065,.21,.15,1],'smoothness':.82},
 'screen':{'color':[1,1,1,1],'smoothness':.74,'texture_asset':'Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/BlueLockers/UI/actual-payment-runtime-UI.png'}
}
packet={'store_key':'blue-lockers','floor_world_y':floor,'origin_q':[float(C[0]),float(-C[1])],'materials':materials,'source_imports':imports,'mesh_library':lib,'objects':objects,'source_packet':{'source_epoch':'2026-09/10; public provided July2026 corroboration','bank_count':1,'compartment_count':26,'payment_count':1,'screen_number_range':[901,926],'all144_or150_article_compartments_constructed':False,'pose_qualification':'CURRENT_NATIVE_HALL_GRAPH_AND_PHOTO_PROPORTIONED_MODEL_NOT_FIELD_MEASUREMENT','physical_photo_contours':evidence,'downloaded_source_mesh':source_shell,'source_license':'CC-BY-4.0 adventurer','source_url':'https://sketchfab.com/3d-models/locker-box-metal-9mb-26dc07440e3d40dc86a1f3fc9c01b0f3','no_Text_TMP_Light':True}}
(BASE/'BLUE-LOCKER-PACKET-R26.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
print(json.dumps({'objects':len(objects),'library_meshes':len(lib),'physical_sign_assemblies':len(evidence),'envelope_actual_q':{'qx':[38.09,39.08],'qy':[-27.35,-18.65]},'compartments':26}))
