from pathlib import Path
import json,math,hashlib,importlib.util
import cv2,numpy as np
P=Path(__file__).resolve().parent; B=P.parent; C=B.parent; R=C.parents[1]
S=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/original-design-discovery/pascucci-primary-sign-subcontractor'
spec=importlib.util.spec_from_file_location('outline',C/'photo_outline_to_solid.py');ol=importlib.util.module_from_spec(spec);spec.loader.exec_module(ol)
origin=np.array([-36.700906,86.762281]);u=np.array([.999988074,-.004883823]);v=np.array([.004883823,.999988074]);yaw=math.degrees(-math.atan2(u[1],u[0]));q=lambda x,z:(origin+u*x+v*z).tolist()
materials={'bronze':{'color':[.30,.205,.105,1],'metallic':.75,'smoothness':.52},'dark_face':{'color':[.105,.072,.042,1],'metallic':.4,'smoothness':.4},'ink':{'color':[.70,.86,.86,1],'emission':[.02,.33,.49,1],'smoothness':.36},'frame':{'color':[.72,.70,.64,1],'metallic':.3,'smoothness':.4},'back':{'color':[.26,.18,.11,1]}}
meshes={};objects=[]
def add(kind,id,x,z,y,material,group,**kw):
 d={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':yaw,'material':material,'group':group,'source_ids':['PRIMARY_CONTRACTOR_RENEWAL','CURRENT_20260923_ART'],'qualification':'SOURCE_MATCHED_MODEL_NOT_SURVEY',**kw};objects.append(d);return d
# Complete contractor sign, rectified to a circular face before deriving physical ink.
photo=cv2.imread(str(S/'SOURCE-02.jpg')); H=cv2.getPerspectiveTransform(np.float32([[183,114],[473,114],[474,402],[183,402]]),np.float32([[0,0],[511,0],[511,511],[0,511]]));disc=cv2.warpPerspective(photo,H,(512,512)); hsv=cv2.cvtColor(disc,cv2.COLOR_BGR2HSV)
mask=cv2.inRange(hsv,np.array([75,80,145]),np.array([135,255,255]));yy,xx=np.ogrid[:512,:512];mask[((xx-255.5)**2+(yy-255.5)**2)>245**2]=0
cv2.imwrite(str(P/'gelato-source-rectified.jpg'),disc);cv2.imwrite(str(P/'gelato-ink-mask.png'),mask)
meshes['gelato_ink']=ol.closed_solid_from_mask(mask,.315,.315,.0016,minimum_area=3,epsilon=.28)
# Stand sits on existing counter, immediately left of its already modeled GN bank.
x,z=5.17,4.53
add('cylinder','Gelato_oval_foot',x,z,1.100,'bronze','GelatoStand',size=[.145,.004,.110],collider=False)
add('cylinder','Gelato_stem',x,z,1.157,'bronze','GelatoStand',size=[.021,.055,.021],collider=False)
add('cylinder','Gelato_round_housing',x,z,1.365,'bronze','GelatoStand',size=[.325,.010,.325],euler=[90,yaw,0],collider=False)
add('cylinder','Gelato_dark_front',x,z-.0108,1.365,'dark_face','GelatoStand',size=[.319,.0008,.319],euler=[90,yaw,0],collider=False)
add('library_mesh','Gelato_source_solid_ink',x,z-.0121,1.365,'ink','GelatoStand',mesh_key='gelato_ink',yaw=yaw+180,collider=False)
# Torrefazione framed artwork remains visibly present in exact current September 2026 photo.
# Rebuild intrinsic illustrated source ink as individual closed palette surfaces, not photo plane.
art=cv2.imread(str(S/'SOURCE-01.jpg'));H=cv2.getPerspectiveTransform(np.float32([[229,330],[441,329],[455,682],[218,683]]),np.float32([[0,0],[255,0],[255,419],[0,419]]));art=cv2.warpPerspective(art,H,(256,420));art=cv2.medianBlur(art,3)
cv2.setRNGSeed(2601);_,labels,colors=cv2.kmeans(art.reshape(-1,3).astype(np.float32),12,None,(cv2.TERM_CRITERIA_EPS+cv2.TERM_CRITERIA_MAX_ITER,40,.2),3,cv2.KMEANS_PP_CENTERS);labels=labels.reshape(420,256);colors=colors.astype(np.uint8)
cv2.imwrite(str(P/'torrefazione-source-rectified.jpg'),art)
# Current C043 rear support east face u8.8500114; artwork mounted on it, no wall/column mutation.
ax,az=8.878,5.26;cy=1.89;width=.74;height=1.215
add('box','Torrefazione_mount_back',ax-.012,az,cy,'back','TorrefazioneArt',size=[.018,height+.045,width+.045],collider=False)
for n,(dy,dz,sy,sz) in enumerate([(height/2+.015,0,.028,width+.055),(-height/2-.015,0,.028,width+.055),(0,width/2+.015,height,.028),(0,-width/2-.015,height,.028)]):
 add('box','Torrefazione_frame_'+str(n),ax,az+dz,cy+dy,'frame','TorrefazioneArt',size=[.024,sy,sz],collider=False)
for k in range(12):
 m=(labels==k).astype(np.uint8)*255;key='art_layer_'+str(k);mesh=ol.closed_solid_from_mask(m,width,height,.0007,minimum_area=2,epsilon=.27)
 if not mesh['vertices']:continue
 meshes[key]=mesh;materials[key]={'color':[float(c)/255 for c in colors[k][::-1]]+[1],'smoothness':.25}
 add('library_mesh','Torrefazione_source_color_'+str(k),ax+.013,az,cy,key,'TorrefazioneArt',mesh_key=key,yaw=yaw+90,collider=False)
packet={'store_key':'pascucci','origin_q':origin.tolist(),'floor_world_y':7,'materials':materials,'mesh_library':meshes,'objects':objects,'source_imports':[],'source_packet':str((P/'SOURCE-AND-PLACEMENT-BASIS-R26.json').relative_to(R)),'limits':['Dimensions and mounting position PHOTO_REFERENCED_MODEL, not measured.','Current 2026 photo corroborates artwork. Complete contractor image supplies exact ink; completed 2024 sign corroborates round housing/stem/base. No exact 2026 illumination state claim.','Do not add construction-stage chest or delete unmatched east tall refrigerator.']}
(P/'PRIMARY-DETAILS-PACKET-R26.json').write_text(json.dumps(packet,separators=(',',':')))
basis={'status':'PRIMARY_SOURCE_GEOMETRY_WITH_CURRENT_ART_CROSSCHECK','sources':[{'path':str(S.relative_to(R)),'url':'https://m.blog.naver.com/bym_story/223630128044','role':'Actual sign contractor renewal images: complete gelato sign contours and artwork.'},{'path':str((B/'sources/current2026-00-10.jpg').relative_to(R)),'url':'https://m.blog.naver.com/dorongation/224420732947','role':'2026-09-23 Torrefazione art remains west end of window seating, adjacent native rear support.'},{'path':str((B/'sources/branch-20240417-15.jpg').relative_to(R)),'url':'https://m.blog.naver.com/mongsangteller/223417891331','role':'Completed round bronze housing, stem, oval base at left of actual inset gelato bank.'}], 'chest_and_tall_fridge':{'classification':'different corners or unbound individual identity; no deletion/addition','primary_chest':'At art/west window corner during construction. Current2026 source does not positively establish continued chest inventory.','existing_tall_fridge':'Packet u15.4 east opposite window-end; preserve exact existing object1535920070. No primary proof same device/corner.'},'mounting':{'art_column':'NATIVE_COL_043_2F_MODEL GlobalObjectId target1378963359','native_east_face_u':8.850011421766565,'art_face_u':8.891,'art_center_v':5.26,'size_MODEL_m':[width,height],'support_unchanged':True,'gelato_counter_uv':[x,z],'countertop_world_y':8.095,'standbase_world_y':8.096},'geometry':{'artwork':'12-source-color actual illustrated contours extruded closed physical thin surfaces, independent frame and backing. No photo plane.','sign':'Actual source cyan ink mask closed solids with counters, round housing, stem, oval foot. Emissive material only, no Light component.','objects':len(objects),'vertices':sum(len(x['vertices']) for x in meshes.values())},'no_QA_Play_reopen_probe_capture':True}
(P/'SOURCE-AND-PLACEMENT-BASIS-R26.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
(P/'task_plan.md').write_text('# Pascucci R26 primary renewal details\n\n1. Source/current correspondence complete.\n2. Build independent artwork and actual solid gelato placard complete.\n3. C180 scoped apply/save and settled release in progress.\n\nConstruction-stage chest not reproduced; existing different/unbound tall fridge preserved. No QA.\n')
(P/'progress.md').open('a').write('Built '+str(len(objects))+' independent physical parts; source illustration color contours and complete sign ink, real housing/foot/frame. No source pixels as visible plane, no Light/Text components.\n')
print({'packet_sha':hashlib.sha256((P/'PRIMARY-DETAILS-PACKET-R26.json').read_bytes()).hexdigest(),'parts':len(objects),'meshes':len(meshes),'vertices':basis['geometry']['vertices']})
