from pathlib import Path
import json,math,hashlib,datetime,importlib.util
import cv2,numpy as np
R=Path.cwd();P=Path(__file__).parent;S=P/'source-tracing';S.mkdir(exist_ok=True)
sp=importlib.util.spec_from_file_location('solid',R/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/photo_outline_to_solid.py');m=importlib.util.module_from_spec(sp);sp.loader.exec_module(m)
source=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/rail-police/sources/GC042P24075-public-original.jpg';im=cv2.imread(str(source));obs=[];meshes={};objects=[]
slope=(.262288-.228207)/(66.694072-59.449273);angle=math.degrees(math.atan(slope));front=lambda x:.228207+slope*(x-59.449273);centre=lambda x:front(x)-.09
materials={
 'beige':{'color':[.675,.673,.606,1],'metallic':.08,'smoothness':.38},'joint':{'color':[.43,.44,.40,1],'smoothness':.22},'doorgray':{'color':[.46,.49,.46,1],'metallic':.20,'smoothness':.35},
 'silver':{'color':[.62,.65,.64,1],'metallic':.88,'smoothness':.67},'darksteel':{'color':[.18,.22,.20,1],'metallic':.7,'smoothness':.45},'black':{'color':[.018,.022,.022,1],'smoothness':.3},
 'frosted':{'color':[.54,.58,.57,1],'metallic':.13,'smoothness':.62},'wood':{'color':[.43,.24,.07,1],'metallic':0,'smoothness':.37},
 'white':{'color':[.91,.92,.90,1],'smoothness':.3},'navy':{'color':[.025,.07,.25,1],'smoothness':.36},'cyanblue':{'color':[.045,.43,.68,1],'smoothness':.40},'red':{'color':[.49,.065,.045,1],'smoothness':.42}}
def part(id,g,kind,x,y,qy,size,mat,rot=None,collider=False):
 a={'id':id,'group':g,'kind':kind,'q':[x,-qy],'y':y,'size':size,'material':mat,'euler':rot if rot is not None else[0,angle,0],'collider':collider,'qualification':'2013actualsource_shape;currentexistinghostandhumanproportionedMODEL_not2026asbuilt_or_fieldsurvey'};objects.append(a);return a
def box(id,g,x,y,qy,size,mat,rot=None,col=False):return part(id,g,'box',x,y,qy,size,mat,rot,col)
def cy(id,g,x,y,qy,size,mat,rot=None):return part(id,g,'cylinder',x,y,qy,size,mat,rot)
def inkpart(id,g,x,y,qy,key,mat):
 a=part(id,g,'mesh',x,y,qy,[1,1,1],mat,[0,180+angle,0]);a['mesh_key']=key;return a

def trace(key,quad,shape,width,height,select,area=12):
 w,h=shape;dst=np.float32([[0,0],[w-1,0],[w-1,h-1],[0,h-1]]);tf=cv2.getPerspectiveTransform(np.float32(quad),dst);a=cv2.warpPerspective(im,tf,(w,h));hsv=cv2.cvtColor(a,cv2.COLOR_BGR2HSV);gray=cv2.cvtColor(a,cv2.COLOR_BGR2GRAY)
 if select=='white': mask=np.uint8((hsv[:,:,1]<18)&(hsv[:,:,2]>245))*255
 elif select=='dark':mask=np.uint8(gray<(210 if key=='HeaderRailroadActualDarkMark' else 110))*255
 elif select=='navy':mask=np.uint8((hsv[:,:,0]>85)&(hsv[:,:,0]<145)&(hsv[:,:,1]>55)&(hsv[:,:,2]<195))*255
 else:raise ValueError(select)
 mask[:3]=0;mask[-3:]=0;mask[:,:3]=0;mask[:,-3:]=0
 # Source frame/background are not glyphs; remove boundary-connected components.
 count,lab,stats,_=cv2.connectedComponentsWithStats(mask,8)
 for j in range(1,count):
  xx,yy,ww,hh,aa=stats[j]
  if xx<=3 or yy<=3 or xx+ww>=w-3 or yy+hh>=h-3 or aa>mask.size*.35:mask[lab==j]=0
 cv2.imwrite(str(S/(key+'-source-reference.jpg')),a);cv2.imwrite(str(S/(key+'-glyph-mask.png')),mask)
 solid=m.closed_solid_from_mask(mask,width,height,depth=.0035,minimum_area=area,epsilon=.30)
 if not solid['triangles']:raise RuntimeError('No actual source glyph contours: '+key)
 meshes[key]={'vertices':solid['vertices'],'triangles':solid['triangles'],'source_polygons':solid['source_polygons'],'closed_front_back_sides':True,'real_glyph_counters_preserved':True,'font_or_text_component':False,'photo_plane':False}
 obs.append({'key':key,'source':str(source.relative_to(R)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'quad_px':quad,'output_trace_px':shape,'physical_MODEL_width_height':[width,height],'extractor':select,'source_polygons':solid['source_polygons'],'usage':'Reference-only photograph -> closed3D sourceglyphs; no raster imported intoUnity'});return key

# Finite front-only panel patch, narrower than existing7.24m structural bay.
patch=[59.70,65.15];door=[60.05,61.05,7,9.23];window=[63.05,63.95,7.88,8.94]
xs=sorted(set([*patch,door[0],door[1],61.7,62.65,window[0],window[1]]));ys=sorted(set([7.13,window[2],window[3],door[3],9.65]));ii=0
for a,b in zip(xs,xs[1:]):
 for c,d in zip(ys,ys[1:]):
  x=(a+b)/2;y=(c+d)/2
  if any(q[0]<x<q[1] and q[2]<y<q[3] for q in [door,window]):continue
  box(f'BeigePanel_{ii:02}','FrontFinish/IndividuallyEditablePanels',x,y-7,front(x)+.014,[b-a-.003,d-c-.003,.022],'beige');ii+=1
# Panel backing/joint is bounded to the same finish cells, never across apertures.
for a,b in zip(xs,xs[1:]):
 for c,d in zip(ys,ys[1:]):
  x=(a+b)/2;y=(c+d)/2
  if any(q[0]<x<q[1] and q[2]<y<q[3] for q in [door,window]):continue
  box(f'PanelJointBacking_{len(objects):03}','FrontFinish/JointBacking',x,y-7,front(x)+.0015,[b-a,d-c,.004],'joint')
for a,b in [(patch[0],door[0]),(door[1],patch[1])]:
 x=(a+b)/2;box('BlackStonePlinth_'+str(a),'FrontFinish/Plinth',x,.065,front(x)+.021,[b-a,.13,.03],'black')
# Single editable hinge-pivot metal leaf, frame, threshold, hinges, pull and round lock.
a,b=door[:2];dc=(a+b)/2;dh=door[3]-7-.018;qy=centre(dc)+.012
box('OpaqueGraySingleLeaf','Entrance/OperableLeaf',dc,dh/2+.008,qy,[b-a-.035,dh,.046],'doorgray',None,True)
for side,x in [('left',a-.022),('right',b+.022)]:box('SteelJamb_'+side,'Entrance/FixedFrame',x,dh/2+.01,centre(x),[.048,dh+.04,.236],'darksteel',None,True)
box('SteelHead','Entrance/FixedFrame',dc,door[3]-7+.012,centre(dc),[b-a+.095,.048,.236],'darksteel',None,True)
box('Threshold','Entrance/FixedFrame',dc,.007,centre(dc),[b-a+.09,.014,.238],'silver')
for side,x in [('left',a+.017),('right',b-.017)]:box('LeafEdge_'+side,'Entrance/OperableLeaf',x,dh/2+.008,qy,[.012,dh-.02,.052],'silver')
for name,y in [('bottom',.018),('top',dh-.005)]:box('LeafEdge_'+name,'Entrance/OperableLeaf',dc,y,qy,[b-a-.04,.012,.052],'silver')
for i,y in enumerate([.30,1.11,1.94]):cy('VisibleHinge_'+str(i),'Entrance/OperableLeaf',a+.008,y,qy+.038,[.018,.043,.018],'silver',[0,angle,0])
# Closed pull loop built as actual rods/bends and two attachment mounts, no label.
hx=b-.105;hy=1.07;hq=qy+.057
for side,yy in [('top',hy+.075),('bottom',hy-.075)]:
 cy('PullMount_'+side,'Entrance/OperableLeaf',hx,yy,hq,[.018,.015,.018],'darksteel',[90,angle,0]);box('PullLoop_'+side,'Entrance/OperableLeaf',hx,yy,hq+.022,[.041,.015,.025],'black')
box('VerticalPullGrip','Entrance/OperableLeaf',hx+.019,hy,hq+.034,[.016,.145,.016],'black')
cy('RoundLockRose','Entrance/OperableLeaf',hx,.86,hq,[.075,.012,.075],'silver',[90,angle,0]);part('RoundKnob','Entrance/OperableLeaf','sphere',hx,.86,hq+.035,[.059,.059,.063],'silver',[0,angle,0])
# Barred/frosted public service window. No speculative view into hidden police interior.
wa,wb,lo,hi=window;wc=(wa+wb)/2;wy=(lo+hi)/2-7;wh=hi-lo;gqy=centre(wc)+.016
box('FrostedWindowGlass','BarredWindow/Glass',wc,wy,gqy,[wb-wa-.045,wh-.045,.018],'frosted',None,True)
for side,x in [('left',wa-.019),('right',wb+.019)]:box('WindowFrame_'+side,'BarredWindow/Frame',x,wy,centre(x),[.04,wh+.08,.224],'silver',None,True)
for side,y in [('bottom',lo-7-.018),('top',hi-7+.018)]:box('WindowFrame_'+side,'BarredWindow/Frame',wc,y,centre(wc),[wb-wa+.08,.04,.224],'silver',None,True)
for i in range(11):
 x=wa+.052+(wb-wa-.104)*i/10;cy(f'VerticalSecurityBar_{i+1:02}','BarredWindow/IndividualBars',x,wy,front(x)+.007,[.012,wh/2-.014,.012],'silver')
for name,y in [('mid',lo-7+wh*.46),('upper',hi-7-.035),('lower',lo-7+.035)]:box('BarCrossMember_'+name,'BarredWindow/IndividualBars',wc,y,front(wc)+.010,[wb-wa-.03,.014,.015],'silver')
# Actual wooden vertical police-center identity plaque and its traced source glyphs.
key=trace('WoodPoliceCenterActualGlyphs',[[149,417],[194,424],[193,579],[149,570]],[250,900],.24,.865,'dark',20)
x=61.32;y=1.25;fq=front(x)+.030
box('WoodenPoliceCenterPlaque','IdentityPlaque/PhysicalBoard',x,y,fq,[.268,.895,.027],'wood')
for side,xx in [('left',x-.135),('right',x+.135)]:box('PlaqueSilverEdge_'+side,'IdentityPlaque/PhysicalBoard',xx,y,fq,[.006,.905,.031],'silver')
inkpart('PoliceCenterClosedSourceGlyphs','IdentityPlaque/Direct3DGlyphs',x,y,fq+.018,key,'black')
# Permanent video-information notice only; historic2013campaignposter and illegible paragraphs are not invented.
key=trace('ActualVideoInformationHeaderGlyphs',[[202,420],[239,424],[239,449],[201,447]],[500,330],.42,.277,'dark',24)
x=61.84;y=1.55;fq=front(x)+.027
box('VideoInformationNoticeBacking','PermanentNotices/CCTV',x,y,fq,[.456,.315,.016],'silver');box('VideoInformationNoticeWhiteInset','PermanentNotices/CCTV',x,y,fq+.012,[.42,.277,.008],'white');inkpart('VideoInformationActual3DGlyphs','PermanentNotices/CCTV',x,y,fq+.019,key,'navy')
# Source blue/white access plaque, traced only the source navy print/frame; no inventednoticewords.
key=trace('ActualBluePublicNoticeContours',[[201,482],[240,481],[240,523],[200,524]],[480,510],.42,.446,'navy',20)
x=61.84;y=.98;fq=front(x)+.028
box('SourceBlueNoticeWhiteBoard','PermanentNotices/BluePlaque',x,y,fq,[.445,.468,.018],'white');inkpart('SourceBlueNoticeClosedPhysicalInk','PermanentNotices/BluePlaque',x,y,fq+.013,key,'navy')
# Source upper identity sign is a real enclosure and directly modeled source characters, not a raster sign.
key=trace('UpperPoliceHeaderActualWhiteGlyphs',[[284,56],[379,100],[379,171],[284,130]],[1000,600],1.03,.56,'white',50)
x=61.20;y=3.20;fq=front(x)+.055
box('PoliceHeaderMetalEnclosure','UpperSourceIdentitySign/Frame',x,y,fq,[1.43,.65,.12],'darksteel')
box('PoliceHeaderWhiteIdentitySection','UpperSourceIdentitySign/Frame',x-.535,y,fq+.066,[.34,.59,.014],'white')
box('PoliceHeaderCyanBlueInfill','UpperSourceIdentitySign/Frame',x+.175,y,fq+.066,[1.03,.59,.014],'cyanblue')
inkpart('PoliceHeaderClosed3DSourceGlyphs','UpperSourceIdentitySign/Direct3DGlyphs',x+.175,y,fq+.078,key,'white')
key=trace('HeaderRailroadActualDarkMark',[[225,23],[275,48],[275,126],[225,105]],[360,600],.30,.50,'dark',25)
inkpart('HeaderRailroadClosed3DSourceMark','UpperSourceIdentitySign/Direct3DGlyphs',x-.535,y,fq+.078,key,'navy')
# Visible small red public alarm/button beside the door, source-bodied only.
x=59.83;y=1.67;fq=front(x)+.035
box('PublicAlarmMount','PublicDoorHardware/Alarm',x,y,fq,[.075,.105,.025],'silver');part('RedAlarmButton','PublicDoorHardware/Alarm','sphere',x,y,fq+.020,[.055,.064,.038],'red',[0,angle,0])
# No separate old generic door/window/sign object exists within this host-owned band in CAD007.
wall=json.loads((P/'CURRENT-HOST-R18-SOURCE-REGISTER.json').read_text());snap=json.loads((P/'CURRENT-HOST-SNAPSHOT007.json').read_text())
packet={'schema':'chooguard.r29.police_existing_front.physical_construction.v1','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'author':'classic:/root/r28_police_multiplan','scope':'C180_R29_POLICE_EXISTING_FRONT_APPLY_001','authority':str((P/'USER-AUTHORITY-R29.json').relative_to(R)),
 'binding':'.state/ACTIVE-ALL2F-COMPLETION-R26/public/rail-police/multiplan-r28/POLICE-FOUR-PLAN-HOST-PROPOSAL-R28.json','classification':'ROOT_ELECTED_SOURCE_QUALIFIED_MODEL_on_existing_host111;notindividualcurrent2026photoalias_or99percent',
 'source_epoch':'Exterior2013-10-26; inside2025-07 contractor crosscheck does not prove current publicface. No construction-stage props/interior reproduced.',
 'wall':{'object_id':wall['object_id'],'path':wall['path'],'expected_render_mesh':snap['source_mesh_asset'],'expected_material_assets':[x['asset'] for x in snap['materials']],'expected_collider_mesh':wall['colliders'][0]['mesh_asset'],'surface_qx0':59.449273,'surface_front_qy0':.228207,'slope_qy_per_qx':slope,'thickness':.18,'apertures':[{'role':'singleentry','qx':door[:2],'world_y':door[2:]},{'role':'barredwindow','qx':window[:2],'world_y':window[2:]}]},
 'floor_world_y':7,'finite_front_finish_patch_actual_qx':patch,'unchanged_remaining_host_width':True,'group_pivots':{'Entrance/OperableLeaf':{'q':[door[0]+.009,-qy],'y':0,'euler':[0,angle,0]}},
 'materials':materials,'mesh_library':meshes,'objects':objects,'source_extractions':obs,'unobserved_or_stale_not_reproduced':['2013campaignposter','unreadablefineprint','hiddenpoliceinterior','2025constructionboxes/ladders/unfinishedceiling','unqualifiedwholebayphoto-stretch'],
 'old_host_associated_parts':{'inventory':'HOST-ASSOCIATED-GENERIC-FRONT-PARTS-INVENTORY.json','separate_objects_found_other_than_host':0,'no_unrelated_objects_deleted':True},
 'door_MODEL_dimensions_m':{'clear_opening_width':door[1]-door[0],'clear_opening_height':door[3]-7,'leaf_height':dh,'hinge_pivot_independently_editable':True},
 'preserved':['native','floors','columns','nextfront1528514897','publicportalqx51..59','1F','WC','stores','Native6','hosttop/base/XZ/otherfaceoutsidefiniteapertures'],
 'Text_TMP_Lights_created':False,'raster_source_imported_to_Unity':False,'agent_QA_Play_probe_reopen_capture':False,'whole2F_complete':False}
(P/'POLICE-FRONT-PACKET-R29.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
(P/'SOURCE-ATTRIBUTION.txt').write_text('Historical exterior photo: Academy of Korean Studies, Digital Busan Culture Encyclopedia, mediaGC042P24075, photographed2013-10-26. https://www.grandculture.net/busan/toc/GC04213952\nPhotograph retained as reference-only source outsideUnity. Sign words/marks are directly constructed closed physical source-contour meshes; no photographic sign plane or font atlas. Source2025contractor https://m.blog.naver.com/rokmc23wp/223921517918 supplies interior/renovation context only, not publicdoor correspondence. No source photography is imported as a visibleUnity texture. Host and unmeasured dimensions are qualifiedMODEL, not surveyed2026asbuilt.\n')
print(json.dumps({'objects':len(objects),'mesh_glyph_sets':len(meshes),'glyph_polygons':sum(x['source_polygons'] for x in meshes.values()),'packet_sha256':hashlib.sha256((P/'POLICE-FRONT-PACKET-R29.json').read_bytes()).hexdigest(),'packet_bytes':(P/'POLICE-FRONT-PACKET-R29.json').stat().st_size}))
