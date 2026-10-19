from pathlib import Path
import json,math,hashlib,shutil,sys,cv2,numpy as np
ROOT=Path('/Users/um-yunsang/CHOOGuard');B=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/information-desk/constructor';B.mkdir(exist_ok=True)
sys.path.insert(0,str(ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'));from photo_outline_to_solid import closed_solid_from_mask
objs=[];meshes={}
def obj(name,kind='mesh',position=(0,0,0),scale=(1,1,1),euler=(0,0,0),material='white',group='SourceVisiblePhysicalDesk',**kw):
 d=dict(id=name,kind=kind,position=list(position),scale=list(scale),euler=list(euler),material=material,group=group,source_ids=['BDI-NOV2025-P91'],qualification='SOURCE_VISIBLE_PHOTO_PROPORTIONED_MODEL_NOT_FIELD_MEASUREMENT');d.update(kw);objs.append(d)
def mesh(name,v,t,**kw):meshes[name]={'vertices':v,'triangles':t};obj(name,mesh_key=name,**kw)
def ring(name,ri,ro,y0,y1,a0=-180,a1=180,n=96,**kw):
 v=[];t=[]
 for i in range(n+1):
  a=math.radians(a0+(a1-a0)*i/n);v.extend([[r*math.sin(a),y,r*math.cos(a)]for r,y in [(ro,y0),(ro,y1),(ri,y0),(ri,y1)]])
 for i in range(n):
  k=i*4;l=k+4
  for ids in [[k,l,l+1,k+1],[k+3,l+3,l+2,k+2],[k+1,l+1,l+3,k+3],[k+2,l+2,l,k]]:
   t.extend([ids[0],ids[1],ids[2],ids[0],ids[2],ids[3]])
 if abs(a1-a0)<359.9:
  for ids in [[0,1,3,2],[n*4+2,n*4+3,n*4+1,n*4]]:t.extend([ids[0],ids[1],ids[2],ids[0],ids[2],ids[3]])
 mesh(name,v,t,**kw)
def beam(name,a,b,width=.035,mat='truss',group='Canopy_Truss'):
 a=np.array(a,float);b=np.array(b,float);d=b-a;length=np.linalg.norm(d);d/=length
 # Unity cylinder local +Y follows d: Euler z plus yaw gives exact direction.
 yaw=math.degrees(math.atan2(d[0],d[2]));pitch=math.degrees(math.acos(np.clip(d[1],-1,1)))
 obj(name,'cylinder',(a+b)/2,(width,length/2,width),(pitch,yaw,0),mat,group)
ring('Curved_Public_Counter_White_Enclosure',2.63,3.12,.12,1.02,-151,151,100,collider=True)
ring('Counter_Public_Curved_Charcoal_Plinth',2.62,3.125,.035,.28,-151,151,100,material='charcoal')
ring('White_Curved_Countertop',2.40,3.22,1.015,1.075,-151,151,100,material='countertop',collider=True)
ring('Staff_Inner_Worktop',1.32,2.43,.735,.785,-139,139,80,material='countertop',group='Staff_Equipment')
# Continuous source-observed charcoal wave on the front, independently editable finite sector.
v=[];t=[];N=80
for i in range(N+1):
 a=math.radians(-118+236*i/N);h=.30+.44*math.exp(-((i/N-.53)/.38)**4)
 for r,y in [(3.129,.275),(3.129,h),(3.146,.275),(3.146,h)]:v.append([r*math.sin(a),y,r*math.cos(a)])
for i in range(N):
 k=i*4;l=k+4
 for x in [[k,l,l+1,k+1],[k+3,l+3,l+2,k+2],[k+1,l+1,l+3,k+3],[k+2,l+2,l,k]]:t.extend([x[0],x[1],x[2],x[0],x[2],x[3]])
for x in [[0,1,3,2],[4*N+2,4*N+3,4*N+1,4*N]]:t.extend([x[0],x[1],x[2],x[0],x[2],x[3]])
mesh('Source_Counter_Charcoal_Wave_Front',v,t,material='charcoal')
ring('Existing_Column_Equipment_Dark_Wrap_Clearance',.612,.667,.10,2.70,material='charcoal',group='Column_Equipment_Cladding')
# Photo flared white cone is equipment cladding, existing confirmed steel column remains untouched.
v=[];t=[];N=80
for i in range(N+1):
 a=2*math.pi*i/N
 for r,y in [(.67,2.60),(1.08,3.38),(.63,2.60),(1.04,3.38)]:v.append([r*math.sin(a),y,r*math.cos(a)])
for i in range(N):
 k=i*4;l=k+4
 for x in [[k,l,l+1,k+1],[k+3,l+3,l+2,k+2],[k+1,l+1,l+3,k+3],[k+2,l+2,l,k]]:t.extend([x[0],x[1],x[2],x[0],x[2],x[3]])
mesh('White_Flared_Central_Equipment_Cap',v,t,material='white',group='Column_Equipment_Cladding')
ring('Black_Annular_Upper_Fascia',2.94,3.06,3.35,3.68,material='charcoal',group='Canopy_Truss')
ring('Black_Canopy_Lower_Ring',2.91,2.96,3.15,3.20,material='truss',group='Canopy_Truss')
for i in range(20):
 a=2*math.pi*i/20;b=2*math.pi*(i+1)/20
 beam(f'Radial_Upper_Chord_{i}',[1.0*math.sin(a),3.39,1.0*math.cos(a)],[2.97*math.sin(a),3.39,2.97*math.cos(a)],.035)
 beam(f'Radial_Lower_Chord_{i}',[1.0*math.sin(a),3.17,1.0*math.cos(a)],[2.93*math.sin(a),3.17,2.93*math.cos(a)],.035)
 beam(f'Radial_Diagonal_Web_{i}',[1.05*math.sin(a),3.39,1.05*math.cos(a)],[2.93*math.sin(a),3.17,2.93*math.cos(a)],.024)
 beam(f'Outer_Diagonal_Web_{i}',[2.94*math.sin(a),3.18,2.94*math.cos(a)],[2.95*math.sin(b),3.39,2.95*math.cos(b)],.022)
# Source-visible counter monitor terminals, reused downloaded CC-BY physical model; scale is MODEL.
asset='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/Models/POS-Terminal.fbx'
for i,(x,z,yaw) in enumerate([(-1.22,1.93,165),(1.15,1.98,195)]):
 obj(f'Downloaded_Licensed_Staff_Terminal_{i}','asset',(x,1.078,z),(1,1,1),(0,yaw,0),'charcoal','Staff_Equipment',asset=asset,fit_size=[.40,.35,.29],collider=False)
# Source-observed transparent service partitions, supported by physical metal feet and dividers.
for i,a in enumerate([-90,-50,45,95]):
 ar=math.radians(a);x,z=2.77*math.sin(ar),2.77*math.cos(ar)
 obj(f'Clear_Counter_Service_Shield_{i}','box',(x,1.36,z),(.80,.56,.014),(0,a,0),'glass','Counter_Public_Access')
 for j,dx in enumerate([-.30,.30]):
  obj(f'Clear_Shield_Weighted_Foot_{i}_{j}','box',(x+dx*math.cos(ar),1.084,z-dx*math.sin(ar)),(.10,.018,.13),(0,a,0),'steel','Counter_Public_Access')
for i,(x,z) in enumerate([(-2.3,1.52),(2.4,1.38),(-.45,2.91),(.67,2.87)]):
 obj(f'Source_Notice_Acrylic_Pocket_{i}','box',(x,1.27,z),(.21,.38,.025),(0,math.degrees(math.atan2(x,z)),0),'glass','Counter_Public_Access')
 obj(f'Source_Notice_White_Paper_{i}','box',(x,1.27,z+.005),(.185,.34,.003),(0,math.degrees(math.atan2(x,z)),0),'paper','Counter_Public_Access')
# Actual source geometry: side brochure rack; all compartments physical, contents broad color only.
for j,x in enumerate([3.62,4.22]):obj(f'Literature_Rack_Upright_{j}','cylinder',(x,.78,.12),(.025,.78,.025),material='truss',group='Literature_Rack')
for i,y in enumerate([.18,.48,.78,1.08,1.38]):
 obj(f'Literature_Rack_Inclined_Tray_{i}','box',(3.92,y,.12),(.64,.03,.25),(-12,0,0),'truss','Literature_Rack')
 obj(f'Literature_Rack_Front_Retaining_Rod_{i}','cylinder',(3.92,y+.055,.25),(.018,.32,.018),(0,0,90),'steel','Literature_Rack')
 for j in range(3):obj(f'Source_Brochure_Paper_Block_{i}_{j}','box',(3.71+j*.21,y+.07,.11),(.17,.12,.05),(-12,0,0),'paper','Literature_Rack')
obj('Literature_Rack_Left_Foot','box',(3.62,.035,.12),(.055,.05,.43),material='truss',group='Literature_Rack');obj('Literature_Rack_Right_Foot','box',(4.22,.035,.12),(.055,.05,.43),material='truss',group='Literature_Rack')
# Physical source-contour information symbol and actual station wordmark. No font/TMP or photo sign planes.
im=cv2.imread(str(B.parent/'BDI-P91-SOURCE-XREF695.jpeg'))
for name,crop,wh,pos,mat in [
 ('Source_Information_i_Enclosed_Contour',(253,146,299,194),(.41,.43),(0,2.08,.674),'white'),
 ('Source_KORAIL_Station_Annular_Wordmark',(122,0,209,29),(1.12,.29),(0,3.52,3.068),'white')]:
 x0,y0,x1,y1=crop;sub=im[y0:y1,x0:x1];gray=cv2.cvtColor(sub,cv2.COLOR_BGR2GRAY)
 threshold=95 if 'Annular' in name else 155
 mask=((gray>threshold)&(np.max(sub,axis=2)-np.min(sub,axis=2)<90)).astype(np.uint8)*255
 cv2.imwrite(str(B/(name+'-source-mask.png')),mask)
 meshes[name]=closed_solid_from_mask(mask,*wh,depth=.008,minimum_area=2,epsilon=.25)
 obj(name,mesh_key=name,position=pos,material=mat,group='Actual_Source_Contour_Signs')
# Two source-visible rectangular digital display housings hung on actual ring.
for i,(x,yaw) in enumerate([(-1.95,-28),(1.68,25)]):
 z=math.sqrt(2.72**2-x*x)
 obj(f'Information_Display_Physical_Casing_{i}','box',(x,2.77,z), (1.38,.82,.085),(0,yaw,0),'truss','Information_Displays')
 obj(f'Information_Display_Actual_Blue_Panel_{i}','box',(x,2.77,z+.052),(1.29,.73,.018),(0,yaw,0),'blue_screen','Information_Displays')
 beam(f'Information_Display_Suspension_{i}',[x,3.32,z],[x,3.12,z],.03,'truss','Information_Displays')
# Source-derived visible display markings are closed geometry, not generated font labels.
for i,(x,yaw,crop) in enumerate([(-1.95,-28,(104,73,217,130)),(1.68,25,(320,134,405,177))]):
 z=math.sqrt(2.72**2-x*x);x0,y0,x1,y1=crop;sub=im[y0:y1,x0:x1]
 mask=(np.min(sub,axis=2)>125).astype(np.uint8)*255
 name=f'Source_Display_Visible_Content_Contour_{i}';cv2.imwrite(str(B/(name+'-source-mask.png')),mask)
 meshes[name]=closed_solid_from_mask(mask,1.24,.68,depth=.002,minimum_area=2,epsilon=.22)
 obj(name,mesh_key=name,position=[x,2.77,z+.067],euler=[0,yaw,0],material='white',group='Information_Displays')
# Imported model attribution only; original source is not imported or mutated.
credit='POS terminal by Vladyslav.Yarovyi — CC BY 4.0. https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb\nExisting independently imported R25 source geometry reused as two photo-proportioned category terminals, not installed make/model identity.\nInformation desk custom curved shell/truss/sign contours derived from BDI researcher photos (Nov2025), no source photo sign planes.\n'
(B/'SOURCE-ATTRIBUTION.txt').write_text(credit)
Q=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]]);center=np.array([62.9020729,25.2392368]);q=Q@center;nq=np.array([.854,-.520]);nq/=np.linalg.norm(nq);nw=Q@nq;yaw=math.degrees(math.atan2(nw[0],nw[1]))
packet={'schema':'chooguard.r26.central_information_desk.source_geometry.v1','scope':'C180_R26_PUBLIC_INFORMATION_APPLY_001','author':'classic:/root/r26_public_information','existing_anchor_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1882180995-0','existing_anchor_path':'FPSWorld/Architecture/2F/MAIN2F_NATIVE_COLUMNS_SPLIT_C108/NATIVE_COL_024_2F_MODEL','anchor_world_xz':center.tolist(),'actual_q_center':q.tolist(),'floor_world_y':7,'yaw_world':yaw,'asset_folder':'Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/InformationDesk','parent_path':'FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT','group_name':'R26_CENTRAL_INFORMATION_DESK_SOURCE_MODEL','source_ids':['BDI-NOV2025-P91','RY2QVE0TUGK-LOCAL135'],'materials':{'white':{'color':[.82,.82,.78,1],'metallic':.15,'smoothness':.55},'countertop':{'color':[.78,.79,.77,1],'metallic':.22,'smoothness':.68},'charcoal':{'color':[.09,.105,.102,1],'metallic':.3,'smoothness':.38},'truss':{'color':[.026,.03,.029,1],'metallic':.6,'smoothness':.4},'steel':{'color':[.49,.51,.5,1],'metallic':.9,'smoothness':.68},'glass':{'color':[.75,.84,.84,.16],'metallic':.05,'smoothness':.8,'transparent':True},'paper':{'color':[.84,.83,.76,1],'metallic':0,'smoothness':.3},'blue_screen':{'color':[.016,.055,.35,1],'metallic':0,'smoothness':.7}},'mesh_library':meshes,'objects':objs,'attribution':credit,'binding_qualification':'MODEL structural source/scene correspondence; not user-confirmed individual survey','dimensions':'Photo proportional MODEL; actual native column axis fixed. Counter radius3.12, annular canopy3.06, equipment floor7.0, canopytop10.68; not measured asbuilt. Staff gap rear source-occluded practical access retained, not source-observed door count.'}
(B/'INFORMATION-DESK-PACKET-R26.json').write_text(json.dumps(packet,separators=(',',':')))
(B/'SOURCE-BINDING-R26.json').write_text(json.dumps({'status':'SOURCE_BOUND_MODEL_TO_EXISTING_NATIVE_COLUMN_024','primary_source':str((B.parent/'PRIMARY-SOURCE.json').relative_to(ROOT)),'primary_photos':['BDI-P91-SOURCE-XREF694.jpeg','BDI-P91-SOURCE-XREF695.jpeg'],'native_column_id':packet['existing_anchor_id'],'fixed_axis_world_xz':center.tolist(),'actual_q':q.tolist(),'reasoning':['BDI photographs show the desk centered around one of four large high-roof structural columns, with the new Busan pharmacy red cross and clock/schedule façade immediately behind.','Clean latest official drawing identifies pharmacy on the western/southwestern hall-return neighborhood. Accepted current pharmacy q[-61.084,-40.823], nearest fixed large support024q[-45.278,-50.434]; other same-row022 is farther along qx and other row032/028 nearer the northern restaurant/retail edge.','2023 continuous retained route local135 shows the annular desk on the pharmacy/schedule side beyond the central seating, consistent with024 rather than north restaurant/escalator028.','Actual current shared-mesh vertices and localized triangles within7m of all four large axes contain no native curved desk or annular canopy; current columns reused unchanged.'], 'qualification':'MODEL individual correspondence, not survey or new user confirmation','native_column_and_official_geometry_changed':False,'no_name_based_absence_claim':True,'no_source_image_scale_used_as_metric_authority':True},indent=2))
print('objects',len(objs),'meshes',len(meshes),'yaw',yaw,'packetSHA',hashlib.sha256((B/'INFORMATION-DESK-PACKET-R26.json').read_bytes()).hexdigest())
