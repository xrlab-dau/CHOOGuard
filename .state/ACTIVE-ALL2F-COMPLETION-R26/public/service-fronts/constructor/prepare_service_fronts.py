from pathlib import Path
import json,math,hashlib,datetime,sys,importlib.util
import numpy as np,cv2
R=Path('/Users/um-yunsang/CHOOGuard');P=Path(__file__).parent;S=P/'sources';S.mkdir(exist_ok=True)
sp=importlib.util.spec_from_file_location('outline',R/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/photo_outline_to_solid.py');m=importlib.util.module_from_spec(sp);sp.loader.exec_module(m)
A=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]])
def world(qx,y,qy):
 x,z=A.T@np.array([qx,qy]);return [float(x),float(y),float(z)]
def sourcepath(user,pid,i):
 d=json.load(open(P.parent/'sources'/f'{user}-{pid}'/'SOURCE-INDEX.json'));return R/next(a['file']for a in d['images']if a['index']==i)
materials={
 'door':{'color':[.68,.68,.645,1],'metallic':.18,'smoothness':.38},'silver':{'color':[.62,.64,.65,1],'metallic':.9,'smoothness':.7},'black':{'color':[.025,.028,.032,1],'metallic':.1,'smoothness':.35},
 'white':{'color':[.91,.91,.88,1],'metallic':.05,'smoothness':.35},'paper':{'color':[.96,.96,.94,1],'smoothness':.18},'navy':{'color':[.035,.055,.18,1],'smoothness':.25},'blue':{'color':[.04,.10,.37,1],'smoothness':.4},'red':{'color':[.6,.11,.09,1],'smoothness':.25},'yellow':{'color':[.95,.73,.11,1],'smoothness':.25},'glass':{'color':[.035,.05,.06,1],'metallic':.25,'smoothness':.9},'scratch':{'color':[.60,.60,.565,1],'smoothness':.2}}
objects=[];meshes={}; source_records=[]
def part(id,group,kind,qx,y,qy,size,mat,rot=None,collider=False):
 a={'id':id,'group':group,'kind':kind,'q':[qx,-qy],'y':y,'material':mat,'size':size,'collider':collider,'qualification':'SOURCE_SHAPED_BOUNDED_SEMANTIC_CORNER_MODEL_NOT_FIELD_MEASUREMENT'}
 if rot is not None:a['euler']=rot
 objects.append(a);return a

def box(id,g,x,y,z,s,mat,rot=None,collider=False):return part(id,g,'box',x,y,z,s,mat,rot,collider)
def meshpart(id,g,qx,y,qy,key,mat,yaw=180):
 a=part(id,g,'mesh',qx,y,qy,[1,1,1],mat,[0,yaw,0],False);a['mesh_key']=key;return a

def rectify(src,quad,shape,key):
 im=cv2.imread(str(src));w,h=shape;dst=np.float32([[0,0],[w-1,0],[w-1,h-1],[0,h-1]]);mat=cv2.getPerspectiveTransform(np.float32(quad),dst);out=cv2.warpPerspective(im,mat,(w,h));cv2.imwrite(str(S/(key+'-rectified.jpg')),out)
 source_records.append({'source':str(src.relative_to(R)),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'quad_source_pixels':quad,'rectified_size':shape,'purpose':'Actual field source ink extraction, not scene/camera capture'});return out

def solid(key,img,width,height,selector='dark',minimum=3):
 hsv=cv2.cvtColor(img,cv2.COLOR_BGR2HSV);g=cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
 if selector=='dark':mask=np.uint8(g<145)*255
 elif selector=='blue':mask=np.uint8((hsv[:,:,0]>85)&(hsv[:,:,0]<140)&(hsv[:,:,1]>65)&(hsv[:,:,2]<210))*255
 elif selector=='black':mask=np.uint8((hsv[:,:,1]<115)&(hsv[:,:,2]<145))*255
 elif selector=='yellow':mask=np.uint8((hsv[:,:,0]>12)&(hsv[:,:,0]<40)&(hsv[:,:,1]>80)&(hsv[:,:,2]>100))*255
 elif selector=='red':mask=np.uint8(((hsv[:,:,0]<12)|(hsv[:,:,0]>165))&(hsv[:,:,1]>65)&(hsv[:,:,2]<245))*255
 else:raise ValueError(selector)
 if key=='StationRoomLostFoundSourceInk':
  count,labels,stats,_=cv2.connectedComponentsWithStats(mask,8)
  for ii in range(1,count):
   xx,yy,ww,hh,_=stats[ii]
   if xx==0 or yy==0 or xx+ww==mask.shape[1] or yy+hh==mask.shape[0]:mask[labels==ii]=0
 cv2.imwrite(str(S/(key+'-source-mask.png')),mask)
 z=m.closed_solid_from_mask(mask,width,height,depth=.0015,minimum_area=minimum,epsilon=.24);meshes[key]={'vertices':z['vertices'],'triangles':z['triangles'],'source_polygons':z['source_polygons'],'real_holes_preserved':True,'photographic_plane':False};return key

# Accepted corner: return1777742185 positive qY and long publicface992392245 negative qX.
# Right jamb is fixed by actual return/992junction; source door uses 0.70m right-jamb allowance.
lo=-1.68;hi=.12;door_h=2.24;door_c=(lo+hi)/2;wallqy=-29.4000;slope=.004886
qyf=lambda x:wallqy+slope*(x-door_c)
angle=math.degrees(math.atan(slope));front=lambda x:qyf(x)+.105
for side,x in [('left',lo+.453),('right',hi-.453)]:
 box('SteelLeaf_'+side,'Entrance/Leaf_'+side,x,door_h/2,qyf(x),[.887,door_h-.016,.044],'door',[0,angle,0],True)
 # Per-leaf metal edge strips preserve separately editable leaves, fixed left and operable right.
 for e,dx in [('left',-.442),('right',.442)]:box('LeafEdge_'+side+'_'+e,'Entrance/Leaf_'+side,x+dx,door_h/2,qyf(x+dx)+.024,[.009,door_h-.018,.01],'silver',[0,angle,0])
 for e,yy in [('bottom',.018),('top',door_h-.02)]:box('LeafEdge_'+side+'_'+e,'Entrance/Leaf_'+side,x,yy,qyf(x)+.024,[.88,.016,.01],'silver',[0,angle,0])
for side,x in [('left',lo-.025),('right',hi+.025)]:
 box('JambFrame_'+side,'Entrance/Frame',x,door_h/2,qyf(x),[.048,door_h+.05,.22],'silver',[0,angle,0],True)
box('HeadFrame','Entrance/Frame',door_c,door_h+.025,qyf(door_c),[hi-lo+.10,.05,.22],'silver',[0,angle,0],True)
box('Threshold','Entrance/Frame',door_c,.007,qyf(door_c),[hi-lo+.09,.014,.225],'silver',[0,angle,0])
# Actual knobs/lever geometry: left fixed roundknob and right horizontal lever.
for side,x in [('left',door_c-.075),('right',door_c+.075)]:
 z=qyf(x)+.049
 part('HandleRose_'+side,'Entrance/Leaf_'+side,'cylinder',x,1.03,z,[.075,.009,.075],'silver',[90,0,0])
 if side=='left':part('FixedLeafKnob','Entrance/Leaf_left','sphere',x,1.03,z+.035,[.059,.059,.065],'silver')
 else:
  part('LeverSpindle','Entrance/Leaf_right','cylinder',x,1.03,z+.027,[.023,.024,.023],'silver',[90,0,0]);box('PullLever','Entrance/Leaf_right',x+.068,1.03,z+.052,[.145,.022,.029],'silver')
for side,x in [('left',lo+.014),('right',hi-.014)]:
 for k,y in enumerate([.3,1.12,1.98]):part('SteelHinge_'+side+str(k),'Entrance/Leaf_'+side,'cylinder',x,y,qyf(x)+.04,[.019,.04,.019],'silver')
# Actual source-notice contours on the leaves.
src=sourcepath('baitfishingrod','224287119584',9)
left=rectify(src,[[260,309],[343,310],[344,365],[260,364]],[500,320],'fixed-leaf-notice')
right=rectify(src,[[374,372],[416,374],[417,407],[375,406]],[400,300],'pull-leaf-notice')
for key,img,x,y,w,h in [('FixedNotice',left,door_c-.20,1.38,.27,.173),('PullNotice',right,door_c+.23,1.17,.20,.15)]:
 z=qyf(x)+.0245;box(key+'Paper','Entrance/Leaf_'+('left'if x<door_c else'right'),x,y,z,[w,h,.002],'paper',[0,angle,0])
 for color in ['blue','red','black']:
  meshpart(key+'_'+color,'Entrance/Leaf_'+('left'if x<door_c else'right'),x,y,z+.003,solid(key+'_'+color,img,w,h,color),('navy'if color=='blue'else'red'if color=='red'else'black'),180+angle)
# Tiny actual paired vertical pull strips; source-shaped strips with true source ink only.
for i,quad in enumerate([[[339,373],[352,374],[351,410],[339,409]],[[358,373],[370,373],[370,409],[358,409]]]):
 im=rectify(src,quad,[100,300],'centre-strip-'+str(i));x=door_c+(-.031 if i==0 else .031);y=1.175;z=qyf(x)+.025
 box('CentreStripPaper'+str(i),'Entrance/Notices',x,y,z,[.045,.13,.002],'paper',[0,angle,0]);meshpart('CentreStripInk'+str(i),'Entrance/Notices',x,y,z+.003,solid('CentreStripInk'+str(i),im,.045,.13,'dark',2),'navy',180+angle)
# Source stationroom blue plaque with white negative-space icon/text.
plaque=rectify(src,[[633,90],[740,91],[741,233],[635,233]],[430,570],'stationroom-plaque');x=.46;y=1.82;z=front(x)+.004
box('StationRoomPlaqueWhiteBase','Entrance/JambNotices',x,y,z,[.31,.411,.008],'paper',[0,angle,0]);meshpart('StationRoomPlaqueSourceBlue','Entrance/JambNotices',x,y,z+.006,solid('StationRoomPlaqueSourceBlue',plaque,.31,.411,'blue',8),'navy',180+angle)
# Small actual film-report notice below plaque. Source lines are physical color contours.
film=rectify(src,[[623,246],[773,242],[774,348],[624,350]],[600,430],'film-report-notice');x=.46;y=1.37;z=front(x)+.004
box('FilmNoticeBacking','Entrance/JambNotices',x,y,z,[.42,.301,.004],'white',[0,angle,0])
for col in ['blue','red','yellow','black']:
 meshpart('FilmNoticeSource_'+col,'Entrance/JambNotices',x,y,z+.004,solid('FilmNoticeSource_'+col,film,.42,.301,col,7),'navy'if col=='blue'else col,180+angle)
small=rectify(src,[[784,241],[862,239],[864,278],[786,280]],[460,230],'stationroom-small-plaque')
box('StationRoomSmallPlaqueBacking','Entrance/JambNotices',.647,1.72,-29.08,[.008,.115,.23],'white')
meshpart('StationRoomSmallPlaqueInk','Entrance/JambNotices',.641,1.72,-29.08,solid('StationRoomSmallPlaqueInk',small,.23,.115,'dark',8),'black',270)
# Source hanging sign: physical white infill and navy source ink, closed contours/counters.
signsrc=sourcepath('yungong_124','224001198372',15)
sign=rectify(signsrc,[[84,365],[612,397],[610,499],[88,470]],[1100,230],'stationroom-lostfound-projecting-sign')
key=solid('StationRoomLostFoundSourceInk',sign,1.32,.276,'dark',8)
sx=-.13;sy=2.48;sz=-28.05
box('ProjectingSignMetalEnclosure','ProjectingSourceSign',sx,sy,sz,[1.43,.377,.13],'silver',None,False)
for face,normal in [('hall',1),('reverse',-1)]:
 zz=sz+normal*.073;box('ProjectingSignBlackRim_'+face,'ProjectingSourceSign',sx,sy,zz,[1.382,.33,.025],'black');box('ProjectingSignWhiteInfill_'+face,'ProjectingSourceSign',sx,sy,zz+normal*.015,[1.32,.276,.008],'white')
 meshpart('ProjectingSignClosedSourceInk_'+face,'ProjectingSourceSign',sx,sy,zz+normal*.023,key,'navy',180 if normal==1 else 0)
box('ProjectingSignWallMount','ProjectingSourceSign',.635,sy,sz,[.065,.57,.18],'silver',None,False)
for y in [sy-.2,sy+.2]:part('ProjectingSignMountBolt_'+str(y),'ProjectingSourceSign','sphere',.593,y,sz,[.016,.016,.016],'silver')
# Source information kiosk: white floor-standing enclosure, blue perimeter, dark actual electronic display.
kx=.548;ky=-28.67;kw=.82;kh=2.12;kd=.20
box('InformationKioskBody','InformationKiosk',kx,kh/2,ky,[kd,kh,kw],'white',None,True)
# Front normal -qX. Blue strips fit actual wall attachment, no aperture on992.
for name,yy,zz,ss in [('header',1.98,ky,[.016,.28,kw]),('bottom',.045,ky,[.016,.09,kw]),('sideA',1.08,ky-kw/2+.015,[.016,1.90,.03]),('sideB',1.08,ky+kw/2-.015,[.016,1.90,.03])]:box('InformationKioskBlue_'+name,'InformationKiosk',kx-kd/2-.009,yy,zz,ss,'blue')
box('InformationKioskDisplayBezel','InformationKiosk',kx-kd/2-.02,1.57,ky,[.025,.64,.72],'black')
box('InformationKioskDisplayGlass','InformationKiosk',kx-kd/2-.037,1.57,ky,[.012,.568,.645],'glass')
box('InformationKioskLowerPanelJoint','InformationKiosk',kx-kd/2-.012,.87,ky,[.006,.006,.76],'silver')
for side in [-1,1]:part('InformationKioskBaseFoot'+str(side),'InformationKiosk','cylinder',kx,.012,ky+side*.31,[.045,.012,.045],'black')
# Source kiosk header lettering rectified from SOURCE08; no photo panel.
ksrc=sourcepath('baitfishingrod','224287119584',8)
khead=rectify(ksrc,[[645,300],[714,255],[720,319],[648,355]],[600,220],'information-kiosk-source-header')
# White source lettering only, excludes blue background, so exact reversed/infill forms remain physical solids.
hsv=cv2.cvtColor(khead,cv2.COLOR_BGR2HSV);mask=np.uint8((hsv[:,:,1]<100)&(hsv[:,:,2]>125))*255
# Distant right-hand blank source header portion is retained as blue equipment, not invented words.
z=m.closed_solid_from_mask(mask,.63,.18,depth=.0015,minimum_area=25,epsilon=.25);meshes['InformationKioskHeaderWhite']={'vertices':z['vertices'],'triangles':z['triangles'],'source_polygons':z['source_polygons'],'real_holes_preserved':True,'photographic_plane':False};cv2.imwrite(str(S/'information-kiosk-header-white-mask.png'),mask)
meshpart('InformationKioskPhysicalHeaderInk','InformationKiosk',kx-kd/2-.021,1.98,ky,'InformationKioskHeaderWhite','paper',270)
# Actual return source and source-hash guard; constructor cuts only the finite selected source owner.
d=json.load(open(R/'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/inputs/CURRENT-WALLS-AND-NATIVE-FEATURE-SECTIONS-R18.json'));wall=next(x for x in d['nonWC_walls']if'-1777742185-'in x['object_id'])
packet={'schema':'chooguard.r26.source_bound_public_service_fronts.v1','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'SOURCE_CORNER_BOUND_CONSTRUCTION_MODEL','root_authority':str((P.parent/'SOURCE-BOUND-SERVICE-FRONTS-AUTHORITY-R26.json').relative_to(R)),'origin_q':[0,0],'floor_world_y':7,'wall':{'object_id':wall['object_id'],'path':wall['path'],'expected_render_mesh':wall['mesh_asset'],'expected_material_assets':wall['material_assets'],'expected_collider_mesh':wall['colliders'][0]['mesh_asset'],'aperture':{'qx':[lo,hi],'world_y':[7,7+door_h+.015],'qy':'full localwallthickness only, other geometry fixed'},'cap_material':3},'door_dimensions_MODEL_m':{'leaf_width':.887,'leaf_height':door_h-.016,'opening_width':hi-lo,'opening_height':door_h+.015,'right_stone_jamb_to992junction':.69968374,'not_field_surveyed':True},'corner_basis':{'current_return1777742185':'R15Batch02 acceptedsourcequalified, positiveqy publicnormal','adjacent_longwall992392245':'negativeqx publicnormal','source_corner_relation':'SOURCE08 right longwall kiosk and SOURCE09 transverse paired steeldoor visible across same corner, independentSOURCE09/12daonyoung, SOURCE05/06immoanaa, SOURCE15yungong support commonpublicfront','source_offsets':'4.83m actualreturn width, not source claimednormalrecession','door_position_rule':'Right jamb against actual992junction with source-sized plaque allowance; aperture ontransversereturn, not arbitrarymidpointoflongwall992','protected':'873429150 and1748468989nurseroom unchanged; otherthreepermittedfacegeometry retained; no native/floor/columns/shops/1F/WC/transports changes'},'materials':materials,'mesh_library':meshes,'objects':objects,'source_extractions':source_records,'source_raster_planes':False,'Light_Text_TMP_created':False,'QA_probes_reopen_Play_captures':False,'all2F_complete':False}
(P/'SERVICE-FRONTS-PACKET-R26.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
print('parts',len(objects),'meshcontours',len(meshes),'packetbytes',(P/'SERVICE-FRONTS-PACKET-R26.json').stat().st_size)
