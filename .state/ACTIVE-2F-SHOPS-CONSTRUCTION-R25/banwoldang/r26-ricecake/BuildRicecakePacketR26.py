import json,math,random,hashlib
from pathlib import Path
B=Path(__file__).resolve().parent
# A directly modeled closed ricecake cut extrusion. Actual ricecake, not another-food asset.
def ricecake(key,bend,taper,cut0,cut1):
 N=32;L=.12;R=.015;v=[];side=[];bottom=[];cap=[];profile=[(0,.92),(.012,.985),(.034,1.005),(.10,1),(.23,1),(.39,1.015),(.55,1),(.72,.99),(.88,1),(.966,1),(.988,.985),(1,.92)]
 rng=random.Random(key)
 for k,(u,rr) in enumerate(profile):
  cX=(u-.5)*L;cY=.0007*math.sin(u*math.pi);cZ=bend*math.sin(math.pi*u);rad=R*rr*(1+taper*(u-.5))
  for j in range(N):
   a=2*math.pi*j/N;co=math.cos(a);sn=math.sin(a);noise=1+.012*math.sin(j*2.7+k*.9+key);yy=co*rad*noise;zz=sn*rad*.97*noise
   slope=(cut0*(1-u)**8+cut1*u**8)*yy
   v.append([cX+slope,cY+yy,cZ+zz])
 for k in range(len(profile)-1):
  for j in range(N):
   a=k*N+j;b=k*N+(j+1)%N;c=(k+1)*N+(j+1)%N;d=(k+1)*N+j
   dst=bottom if math.cos(2*math.pi*(j+.5)/N)<-.46 else side;dst.extend([a,b,c,a,c,d])
 # Independent cap vertices preserve physically flat angled cut faces and hard normals.
 for end in [0,1]:
  base=len(v);u=end;yy=.0007*math.sin(u*math.pi);zz=bend*math.sin(math.pi*u);v.append([(u-.5)*L,yy,zz]);ring=0 if end==0 else (len(profile)-1)*N
  v.extend([v[ring+j][:] for j in range(N)])
  for j in range(N):
   a=base;b=base+1+j;c=base+1+(j+1)%N;cap.extend([a,c,b] if end==0 else [a,b,c])
 return {'vertices':v,'submeshes':[side,cap,bottom],'actual_closed_food':'ricecake short cut cylinder with soft rim, slight bend and oblique flat ends','length_m_MODEL':L,'diameter_m_MODEL':R*2}
meshes={('ricecake_%02d'%i):ricecake(i,b,t,c0,c1) for i,(b,t,c0,c1) in enumerate([(0,.06,.10,-.12),(.002,.10,.18,.08),(-.003,-.08,-.13,.12),(.004,.05,.07,-.20),(-.001,.11,-.18,-.05),(.005,-.06,.13,.15)])}
# Direct source-observed piece centers/axis proportions from actual July04, not a surveyed recipe or exact count.
pieces=[(191,211,128,38,-15),(289,225,108,32,-52),(364,229,111,30,32),(403,253,111,34,35),(318,275,133,37,-11),(183,284,118,31,-23),(128,288,134,32,-40),(218,325,98,30,-92),(279,318,118,34,71),(352,315,119,37,-10),(433,298,116,33,-22),(513,339,114,32,0),(604,314,153,38,13),(650,363,111,35,62),(566,539,138,42,79),(587,411,97,35,20),(474,398,111,38,-42),(402,389,99,33,-45),(347,393,102,33,67),(305,422,104,32,80),(253,450,108,35,90),(164,432,140,37,-14),(120,500,143,39,24),(82,550,113,34,31),(215,512,142,37,32),(344,496,109,35,49),(422,463,105,35,-45),(494,443,100,34,-53),(421,529,85,30,-46),(379,470,82,31,-65),(651,650,150,42,28),(348,222,77,29,-45),(265,366,93,32,75),(241,227,102,30,-10),(535,464,98,35,45),(89,346,112,31,-30),(234,394,98,31,-20),(184,360,103,33,-20),(278,560,108,33,45),(485,357,86,31,0)]
colors=json.loads((B/'sources/SOURCE-RICECAKE-COLOR-SAMPLES.json').read_text())['rgb_percentiles'];mats={}
for key,q in [('glaze_gold',40),('glaze_caramel',50),('glaze_warm',60)]:mats[key]={'color':colors[str(q)]+[1],'metallic':0,'smoothness':.91}
mats['cut_ends']={'color':colors['60']+[1],'metallic':0,'smoothness':.82};mats['sauce_contact']={'color':[.51,.20,.012,1],'metallic':0,'smoothness':.94};mats['tongs_steel']={'color':[.66,.70,.71,1],'metallic':.96,'smoothness':.87}
objects=[];rng=random.Random(26028)
for i,(px,py,ll,dd,angle) in enumerate(pieces):
 x=.48-(px-35)/815*.96;z=.24-(py-180)/560*.48;length=ll*.00112;diameter=length/(3.15+(i%7)*.18)
 layer=1 if 160<px<530 and 235<py<490 else 0
 if i in [9,17,18,32,36]:layer=2
 y=.052+diameter*.5+layer*.014
 # Photo image-left maps pan local+X; photo top is pan local+Z back. Axis angle is source-relative.
 yaw=-angle;tilt=rng.uniform(-5,5) if layer else rng.uniform(-2,2)
 objects.append({'id':'R26_SOURCE_RICECAKE_%02d'%i,'mesh_key':'ricecake_%02d'%(i%6),'position':[x,y,z],'euler':[0,yaw,tilt],'scale':[length/.12,diameter/.03,diameter/.03],'materials':[['glaze_gold','glaze_caramel','glaze_warm'][i%3],'cut_ends','sauce_contact'],'source_pixel_center':[px,py],'source_length_diameter_ratio':ll/dd,'source_ids':['JUL23-SUIN72-FULL04','SEP16-SHYL23-FULL02'],'qualification':'PHOTO_SOURCE_GEOMETRY_MODEL_NOT_MEASURED_EXACT_FOOD_COUNT'})
tongsm=json.loads((B/'sources/tongs/UNITY-MATERIAL-SLOTS.json').read_text());src=B/'sources/tongs/Tongs-R26-Unity.fbx';h=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet={'store_key':'banwoldang','source_authority':'.state/ACTIVE-ALL2F-COMPLETION-R26/USER-AUTHORITY.json','existing_root':'FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_banwoldang_SOURCE_BOUND_MODEL','existing_tray_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-690801489-0','existing_tray_name':'DOWNLOADED_STAINLESS_MIXING_PAN_2','group_name':'R26_SOURCE_RICECAKE_AND_TONGS_002','asset_folder':'Assets/ChooGuard/Art/StationInterior/R25_Shops/banwoldang/R26_Ricecake_Completion_002','source_imports':[{'source':str(src),'destination':'Tongs-R26-Unity.fbx','sha256':h(src)},{'source':str(B/'sources/tongs/SOURCE-LICENSE.json'),'destination':'Tongs-SOURCE-LICENSE.json','sha256':h(B/'sources/tongs/SOURCE-LICENSE.json')}],'materials':mats,'mesh_library':meshes,'ricecake_objects':objects,'tongs':{'id':'R26_DOWNLOADED_SOURCE_STAINLESS_TONGS','asset_relative':'Tongs-R26-Unity.fbx','position':[-.18,.25,.16],'euler':[18,5,180],'scale':[1,1,1],'material_map':{k:'tongs_steel' for k in tongsm['slots']},'source_ids':['JUL23-SUIN72-FULL04','SKETCHFAB-ATANDMI-74f71d92c648446a8b9f4089bb440f4e'],'qualification':'DOWNLOADED_SOURCE_FOOD_TONGS_PHOTO_FITTED_MODEL_NOT_EXACT_SKU'},'constraints':{'only_new_children_in_existing_pan2':True,'existing_geometry_moved':False,'Light_Text_TMP_created':False,'no_QA_tests_Play_probe_reopen_capture':True,'source_photo_is_not_object_surface':True}}
(B/'RICECAKE-PACKET-R26.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
(B/'MODEL-PLACEMENT-BASIS-R26.json').write_text(json.dumps({'scope':'Existing pan2 visible food area only','parent_tray_id':packet['existing_tray_id'],'parent_tray_name':packet['existing_tray_name'],'parent_original_q':[1.98,-42.91],'parent_original_bottom_above_floor_m':1.038,'pose_authority':'Actual current saved tray transform is reused without alteration; new child offsets follow that tray, not stale world bounds. No source/native/floor/column/booth/equipment/counter geometry moved.','food_local_pan_region_m':{'X':[-.50,.50],'Z':[-.29,.29],'sauce_surface_Y':.052},'placement_basis':'July04 observed short-cylinder pieces form left heap with isolated right/front pieces. Source-relative pixel centers and axis angles mapped into existing tray interior as MODEL. Count40 is an editable representation, not surveyed or exact inventory.','geometry_basis':'Closed shallow soft cylinders with flat/angled cut ends, rounded cut rim, nonuniform lengths/diameters, modest curvature, lower sauce contact and photo-sampled glossy gold-orange material. No substitute food/labels/photo proxies.','photo_color_samples':'sources/SOURCE-RICECAKE-COLOR-SAMPLES.json','extra_source_omission_completed':'One pair scalloped stainless serving tongs visible back/right of July04 pan; actual downloaded CC-BY model, own smooth derivative with explicit URP all-source-slot material map.','preserved':'Every existing source/model/transform/material/floor/column/native/other-store object; no scene Light/Text/TMP; no hidden kitchen addition.'},ensure_ascii=False,indent=2))
print('PREPARED',len(objects),'ricecake pieces; six closed variants; actual tongs; packetsha',h(B/'RICECAKE-PACKET-R26.json'))
