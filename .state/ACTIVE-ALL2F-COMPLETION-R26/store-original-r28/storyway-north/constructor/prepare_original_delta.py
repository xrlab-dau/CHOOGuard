from pathlib import Path
import json,math,hashlib,re
P=Path('.state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/storyway-north').resolve();O=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-north').resolve()
b=json.load(open(O/'MODEL-PLACEMENT-BASIS-R25.json'));old=json.load(open(O/'STORE-PACKET-R25.json'));receipt=json.load(open(O/'APPLIED-STORE-R25.json'));ids={x['id']:x['object_id'] for x in receipt['objects']};specs={re.sub('[^\\w-]','_',x['id']):x for x in old['objects']};snapshot=json.load(open('.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-007/CURRENT-2F-MESH-SNAPSHOT.json'))
root=receipt['root_path'];W=b['model_width_m'];D=b['model_depth_m'];base=b['native_axis_base_q'];u=b['u'];v=b['v'];x0,z0,*_=b['working_uv'];yaw=-math.degrees(math.atan2(u[1],u[0]));origin=old['origin_q']
def q(x,z):return [base[i]+u[i]*(x0+x)+v[i]*(z0+z) for i in range(2)]
def pos(x,z,y):
 a=q(x,z);return [a[0]-origin[0],y,a[1]-origin[1]]
ops=[];new=[];delnames=[]
def target(n):return {'name':n,'object_id':ids[n]}
def move(n,x,z,y,fit=None,turn=None,mat=None):
 d=target(n);d.update(position=pos(x,z,y));
 if fit:
  a=specs[n].get('fit_size') or specs[n].get('size');d['scale_multiplier']=[fit[i]/a[i] for i in range(3)]
 if turn is not None:d['yaw']=yaw+turn
 if mat:d['material']=mat
 ops.append(d)
def remove(n):delnames.append(target(n))
def add(n,x,z,y,size,mat='white',turn=0,kind='box',**kw):
 d={'name':n,'position':pos(x,z,y),'size':size,'material':mat,'yaw':yaw+turn,'kind':kind};d.update(kw);new.append(d);return d
# Existing accepted floor/exterior/signs fixed. Only own finish ceiling and its cassette follow original2.960m underside.
ceiling=[]
for x in receipt['objects']:
 n=x['id']
 if n.startswith('Acoustic_Ceiling_Tile_'):
  found=any(t['object_id']==x['object_id'] for t in snapshot['objects'])
  if found:ceiling.append({'name':n,'object_id':x['object_id'],'delta_y':-.1175})
ceiling.append({**target('Ceiling_Suspension_Frame'),'delta_y':-.1175})
# Source's central2double-sided gondolas+2ends replace guessed2x2islands; downloaded geometry retained.
shelf_names=[f'Central_Stocked_Gondola_{c}_{s}_{side}' for c in range(2) for s in range(2) for side in range(2)]
placements=[(3.43,3.85-.225,0),(3.43,3.85+.225,180),(4.33,3.85-.225,0),(4.33,3.85+.225,180),(2.755,3.85,90),(5.005,3.85,270)]
for n,(x,z,t) in zip(shelf_names,placements):move(n,x,z,.015,[.9,1.32,.45],t)
for n in shelf_names[len(placements):]:remove(n)
# Stock unchanged in identity/mesh; move current category models onto revised original gondola levels.
for c in range(2):
 for s in range(2):
  for level,yy in enumerate([.18,.43,.68,.93,1.18]):
   for k in range(6):
    n=f'Visible_Packaged_Goods_{c}_{s}_{level}_{k}';move(n,3.43+c*.9-.24+(k%3)*.24,3.85+(-.18 if s==0 else .18)+(k//3)*(.12 if s else -.12),yy)
# Original cooler4D replaces separate stand-alone cabinet bodies; reuse downloaded glass-door source branch below.
for i in range(4):remove(f'Cool_Drink_Source_Cabinet_{i}')
# Open case1.955m wide×.705m deep×1.98m high retains existing downloaded source shelf geometry.
for i in range(3):
 x=2.005+i*(1.955/3)+1.955/6
 move(f'Open_FastFood_Refrigerated_Source_Shelf_{i}',x,1.68,.14,[1.955/3-.008,1.78,.65])
 move(f'Open_Chiller_Compressor_Base_{i}',x,1.68,.15,[1.955/3-.003,.27,.705])
 move(f'Open_Chiller_Upper_Canopy_{i}',x,1.68,1.925,[1.955/3-.003,.11,.705])
 for yy in [.29,.68,1.07,1.46,1.85]:
  n=f'Open_Chiller_Shelf_LED_{i}_{yy}'
  n=re.sub('[^\\w-]','_',n);move(n,x,2.015,min(yy,1.86),[1.955/3-.05,.018,.018])
# Current backpaint band is moved to actual equipment/BOH front; source3color repetitions add physical glass panels.
move('Rear_Yellow_Upper_Band',W/2,2.055,2.53,[W-.18,.76,.03])
for j in range(12):add(f'Original_Backpaint_Band_{j:02}',.22+j*((W-.44)/12)+(W-.44)/24,2.075,2.53,[(W-.44)/12-.006,.76,.005],['pantone134','pantone1225','pantone137'][j%3])
for bank in range(7):
 xbase=2.005+(bank+.5)*(1.955/3) if bank<3 else W-.13-2.9+(bank-3+.5)*(2.9/4)
 for row,yy in enumerate([.32,.69,1.06,1.43,1.77]):
  for k in range(5):move(f'Refrigerated_Physical_Stock_{bank}_{row}_{k}',xbase-.24+k*.12,1.70,yy)
# Counter follows original fore-left block without changing public shop/hall footprint.
move('Own_Source_Checkout_Counter',.49,4.85,.015,[1.30,.90,.50],90)
move('Source_POS_Terminal',.48,4.7,.935,None,90)
# Existing accessory stand moves to solid-side corner beyond east door, not into an entrance.
for x in old['objects']:
 n=re.sub('[^\\w-]','_',x['id'])
 if n.startswith('Entry_Rack_') or n.startswith('Entry_Accessories_Physical_Pack_'):
  a=x['q']; # from currentq to ownbasis coords, translate only.
  a2=[a[i]+u[i]*1.28+v[i]*(-.08) for i in range(2)]
  ops.append({**target(n),'position':[a2[0]-origin[0],x['y'],a2[1]-origin[1]]})
# Replace own unbroken glazing and guessed narrow east doorway with original1800mm clear paired slide entrances.
for n in ['North_Glass_Face','East_Interior_Glass_Return','East_Entry_Open_Leaf','East_Automatic_Door_Head','North_Bottom_Track_5_93']:
 remove(n)
for n in ids:
 if n.startswith('North_Aluminium_Mullion_') or n.startswith('East_Entry_Jamb_') or n.startswith('East_Glass_Mullion_'):remove(n)
front_a,front_b=2.34,4.14;east_a,east_b=2.95,4.75
for n,x,z,s in [('North_Fixed_West',front_a/2,D,[front_a,.0,.01]),('North_Fixed_East',(front_b+W)/2,D,[W-front_b,.0,.01]),('East_Fixed_Rear',W,east_a/2,[.01,.0,east_a]),('East_Fixed_Front',W,(east_b+D)/2,[.01,.0,D-east_b])]:
 s[1]=2.45;add(n,x,z,1.265,s,'glass')
for side,axisvals in [('North',[.04,1.20,front_a,front_b,5.10,W-.04]),('East',[.04,1.90,east_a,east_b,D-.04])]:
 for j,a in enumerate(axisvals):add(f'Original_{side}_Jamb_{j}',a if side=='North' else W,D if side=='North' else a,1.265,[.045,2.53,.065] if side=='North' else [.065,2.53,.045],'warmgray')
for side,a,b1 in [('North',front_a,front_b),('East',east_a,east_b)]:
 for leaf,coord in [('Left',a-.45),('Right',b1+.45)]:
  x,z=(coord,D-.035) if side=='North' else (W-.035,coord)
  add(f'{side}_Retracted_Actual_Sliding_Leaf_{leaf}',x,z,1.245,[.9,2.41,.010],'glass',0 if side=='North' else 90)
  for yy in [.055,2.435]:add(f'{side}_{leaf}_Leaf_Rail_{yy}',x,z,yy,[.9,.03,.025],'warmgray',0 if side=='North' else 90)
  add(f'{side}_{leaf}_Leaf_Handle',x,z+.028 if side=='North' else z,1.05,[.018,.15,.025],'black',0 if side=='North' else 90)
 add(f'{side}_Automatic_Drive_Head',(a+b1)/2 if side=='North' else W,D if side=='North' else (a+b1)/2,2.50,[3.69,.12,.14],'warmgray',0 if side=='North' else 90)
 add(f'{side}_Motion_Sensor',(a+b1)/2 if side=='North' else W+.08,D+.08 if side=='North' else (a+b1)/2,2.5,[.15,.045,.035],'black',0 if side=='North' else 90)
for j,(a,b1) in enumerate([(0,front_a),(front_b,W)]):add(f'North_Actual_Fixed_Lower_Track_{j}',(a+b1)/2,D,.0225,[b1-a,.045,.055],'warmgray')
# RearBOHpartition and one staff access now supported by original. No hall/exteriorcut.
for n,a,z,size in [('BOH_LeftReturn',.55,2.02,[.90,2.96,.045]),('BOH_RightReturn',1.95,2.02,[.10,2.96,.045]),('BOH_OpenCase_Back',2.9825,1.285,[1.955,2.96,.045]),('BOH_StaffDoor_Header',1.45,2.02,[.90,.885,.045])]:
 add(n,a,z,2.5175 if 'Header' in n else 1.48,size,'white')
add('BOH_Staff_Door_Leaf',1.45,2.025,1.0375,[.85,2.075,.035],'coolgray',0)
for xx in [1.0125,1.8875]:add(f'BOH_Staff_Door_Jamb_{xx}',xx,2.025,1.0375,[.025,2.075,.06],'warmgray')
add('BOH_Staff_Lever',1.78,2.065,1.05,[.12,.025,.025],'metal')
# Actual2.9x1.9x2.3walk-in shell; side staffdoor from original cooler plan, existing floor remains.
CX=W-.13-2.9;CZ=.13;CW=2.9;CD=1.9
add('WalkInCooler_Back',CX+CW/2,CZ,1.15,[CW,2.3,.07],'white')
add('WalkInCooler_Right',CX+CW,CZ+CD/2,1.15,[.07,2.3,CD],'white')
add('WalkInCooler_Left_Rear',CX,CZ+.235,1.15,[.07,2.3,.47],'white')
add('WalkInCooler_Left_Front',CX,CZ+1.66,1.15,[.07,2.3,.48],'white')
add('WalkInCooler_Staffdoor_Header',CX,CZ+.975,2.15,[.07,.3,.99],'white')
add('WalkInCooler_Insulated_Staff_Door',CX,CZ+.975,1.0,[.06,2,.95],'white')
add('WalkInCooler_Staff_Release_Handle',CX-.055,CZ+1.32,1.05,[.045,.20,.04],'metal')
add('WalkInCooler_Insulated_Top',CX+CW/2,CZ+CD/2,2.265,[CW,.07,CD],'white')
for j in range(4):
 add(f'WalkInCooler_Licensed_Glass_Door_{j+1}',CX+(j+.5)*(CW/4),CZ+CD,0,[CW/4-.018,2.075,.075],'white',kind='clone',source_path=root+'/DownloadedSourceModels/Cool_Drink_Source_Cabinet_0/Commercial-Fridge/FridgeDoor',collider=True)
for j in range(5):add(f'WalkInCooler_Front_Mullion_{j}',CX+j*(CW/4),CZ+CD,1.04,[.025,2.08,.07],'metal')
for j,yy in enumerate([.28,.65,1.02,1.39,1.76]):add(f'WalkInCooler_Stock_Shelf_{j}',CX+CW/2,1.62,yy,[CW-.12,.025,.7],'white')
# Source stockroom3angle racks use actual licensed shelf asset, exact original1000x300x1800 envelope.
for j in range(3):add(f'Original_BOH_Angle_Rack_{j+1}',.70+j*1.05,.34,.015,[1,1.8,.3],'white',kind='asset',asset='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/Models/SingleFace-Shelf.fbx',material_source='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/MAT_shelf_enamel.mat')
# Three front wall gondolas from original, additional downloaded source geometry with unobstructed centre doorway.
for j in range(3):add(f'Original_Public_Wall_Gondola_{j+1}',4.65+j*.90,D-.29,.015,[.90,1.32,.45],'white',turn=180,kind='asset',asset='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/Models/SingleFace-Shelf.fbx',material_source='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/MAT_shelf_enamel.mat')
# Exact original novelty/chest category equipment in own leftwall workzone; no fictionaltextdevices.
add('Original_Chest_Freezer_1600x700x950',.46,2.95,.475,[.70,.95,1.60],'white')
for zz in [2.52,3.37]:add(f'Chest_Freezer_Sliding_Lid_{zz}',.46,zz,.963,[.66,.015,.78],'glass')
for zz in [2.52,3.37]:add(f'Chest_Freezer_Lid_Handle_{zz}',.46,zz,.99,[.11,.024,.025],'metal')
add('Novelty_Case_Base',.46,2.95,1.015,[.50,.13,1.67],'warmgray')
add('Novelty_Case_Back',.22,2.95,1.50,[.025,.93,1.67],'white')
add('Novelty_Case_Front',.71,2.95,1.50,[.01,.93,1.67],'glass')
add('Novelty_Case_Top',.46,2.95,1.99,[.50,.025,1.67],'white')
for zz in [2.115,3.785]:add(f'Novelty_Case_End_{zz}',.46,zz,1.5,[.50,.93,.01],'glass')
# Original panel schedule explicitly700W1000H180D, 18circuits; physicalclosedpanel exterior and accessibleinternaldeadfront.
add('LP_CVS_Metal_Enclosure_700x1000x180',.145,1.10,1.50,[.18,1,.70],'metal')
add('LP_CVS_Hinged_Door',.246,1.10,1.50,[.022,.98,.68],'coolgray')
add('LP_CVS_Door_Handle',.264,.84,1.50,[.025,.13,.025],'black')
for r in range(9):
 for c in range(2):
  add(f'LP_CVS_Breaker_{r*2+c+1:02}',.245,.90+c*.27,1.86-r*.08,[.018,.045,.10],'black')
add('LP_CVS_Incoming_Cable_Duct',.115,1.10,2.48,[.10,.93,.15],'coolgray')
for j,(x,z,yy) in enumerate([(CX-.08,1.0,.3),(2.95,1.95,.3),(.10,2.95,.3),(.12,4.75,.3),(.10,.6,.3)]):
 add(f'Original_Power_Receptacle_{j+1}',x,z,yy,[.025,.12,.075],'white')
 for dz in [-.015,.015]:add(f'Power_Receptacle_{j+1}_Contact_{dz}',x+.015,z+dz,yy,[.006,.025,.007],'black')
# Source narrow1.6mmhairlineplinth, actualwhiteMg board is confined to newBOH walls.
for n,x,z,size in [('Rear',W/2,.055,[W-.16,.10,.0016]),('West',.055,D/2,[.0016,.10,D-.16])]:add(f'Original_Stainless_Hairline_Skirting_{n}',x,z,.05,size,'metal')
# Source-derived contour signs and current floor/native geometry remain unchanged.
p={'schema':'north.original.source.delta.r28','existing_root':root,'new_group':'R28_ORIGINAL_SOURCE_INTERIOR_DETAIL','asset_folder':'Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/R28_Original_Source_Details','origin_q':origin,'source_epoch':'2018-02-07 design; public source images2023..24; no surveyed/asbuilt assertion','accepted_shell_width_depth':[W,D],'ceiling_finish_underside_m':2.96,'ceiling_delta_operations':ceiling,'cassette_group':'R26_SOURCE_PHOTO_FOUR_WAY_CASSETTE_HVAC','cassette_delta_y':-.1175,'modify':ops,'remove':delnames,'objects':new,'materials':{'white':[.90,.91,.89,1],'warmgray':[.22,.24,.24,1],'coolgray':[.65,.67,.68,1],'metal':[.62,.65,.66,1],'black':[.025,.03,.035,1],'glass':[.78,.88,.89,.10],'pantone134':[1,.82,.43,1],'pantone1225':[1,.73,.25,1],'pantone137':[1,.56,.14,1]},'qualifications':['Original-design dimensions retained for individualcomponents; current placement migrationinsideacceptedfootprint is MODEL.','Original hiddenBOH is now primary-supported designMODEL; currentasbuilt exactepochunmeasured.','Two retracted doorfacesclear1800mm;2.45m doorheight fitted to accepted existing fascia,notoriginal2.76m norstationheight.','No nativefloor/column/hallboundary orprimarysignmodified; noLight/Text/TMP.']}
(P/'constructor/ORIGINAL-NORTH-DELTA-R28.json').write_text(json.dumps(p,ensure_ascii=False,indent=2));print(json.dumps({'modify':len(ops),'remove':len(delnames),'new':len(new),'ceiling':len(ceiling),'sha256':hashlib.sha256((P/'constructor/ORIGINAL-NORTH-DELTA-R28.json').read_bytes()).hexdigest()}))
