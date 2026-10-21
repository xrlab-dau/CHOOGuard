from pathlib import Path
import json,math,hashlib,copy,datetime
from shapely.geometry import Polygon,box
R=Path('/Users/um-yunsang/CHOOGuard');B=Path(__file__).parent;O=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/lounge/constructor'
old=json.load(open(O/'PUBLIC-LOUNGE-PACKET-R26.json')); receipt=json.load(open(O/'APPLIED-PUBLIC-LOUNGE-R26.json'));ids0={x['id']:x['object_id'] for x in receipt['objects']}; clean=lambda s:''.join(c if c.isalnum() or c in '_-' else '_' for c in s); ids={x['id']:ids0[clean(x['id'])] for x in old['objects']}
p={'store_key':'public-lounge-original-r28','origin_q':old['origin_q'],'floor_world_y':7,'source_packet':'.state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/lounge/constructor/SOURCE-RELATIONSHIPS-R28.json','materials':copy.deepcopy(old['materials']),'objects':[],'mesh_library':{},'source_imports':[],'mutations':[],'removals':[],'own_existing_root':receipt['root_path']}
p['materials'].update({'stainless':{'color':[.56,.58,.59,1],'metallic':.88,'smoothness':.52},'warmgray':{'color':[.72,.71,.67,1],'metallic':0,'smoothness':.22},'white-finish':{'color':[.88,.87,.83,1],'metallic':0,'smoothness':.26},'black-rubber':{'color':[.025,.025,.027,1],'metallic':0,'smoothness':.18},'brass':{'color':[.48,.32,.13,1],'metallic':.8,'smoothness':.45},'red-switch':{'color':[.38,.035,.025,1],'metallic':0,'smoothness':.3}})
for m in p['materials'].values():m.pop('texture_asset',None)
def add(n,k,x,z,y,sz,mat='stainless',g='Original_2023_Kitchen_Fixtures',yaw=0,**kw):
 a={'id':n,'kind':k,'q':[x,z],'y':y,'size':sz,'material':mat,'group':g,'yaw':yaw,'source_ids':['OPERATOR_ORIGINAL_2022_12_ISSUED2023_BUSAN_P11_P20_P21','CURRENT2026_CAFE_STORYWAY_PHOTOS'],'qualification':'ORIGINAL_DESIGN_DIMENSION_SOURCE_MODEL_WITH_CURRENT_FIELD_RECONCILIATION_NOT_SURVEYED_2026_ASBUILT'};a.update(kw);p['objects'].append(a);return a
def b(n,x,z,y,s,m='stainless',g='Original_2023_Kitchen_Fixtures',**kw):return add(n,'box',x,z,y,s,m,g,**kw)
def cy(n,x,z,y,r,h,m='stainless',g='Original_2023_Kitchen_Fixtures',**kw):return add(n,'cylinder',x,z,y,[r*2,h/2,r*2],m,g,**kw)
def imp(n,asset,x,z,y,size,g='Downloaded_Source_Adapted_Models',yaw=90):
 a=add(n,'asset',x,z,y,None,'stainless',g,yaw,asset=asset,fit_size=size);a.pop('size');a.pop('material');return a
# Delete only obsolete own bulky approximations. Existing freeespresso/POS retained and moved below.
remove_groups={'Curved_Glass_Chilled_Case','Chilled_Case_Visible_Water','Chilled_Case_Visible_Foods'}
for o in old['objects']:
 n=o['id'];g=o['group']
 if g in remove_groups or n.startswith('Counter_Separate_Cabinet_') or n in ['Cafe_Counter_White_Worktop','Counter_Source_Toekick','Visible_Back_Counter_Cabinet','Visible_Back_Counter_Worktop'] or n in ['Visible_Blender_Base_1','Visible_Blender_Base_2']:
  p['removals'].append({'id':ids[n],'name':n,'reason':'replace own oversized generic equipment with original-source hollow fabrication and current-source curvedcase'});continue
 m={'id':ids[n],'name':n}
 if n=='Cafe_Counter_Source_Wood_Front':m.update(q=[40.465,-40.36],y=.46,size=[.024,.87,6.38],material='white-finish')
 elif n=='Downloaded_Source_Proportioned_Commercial_Espresso':m.update(q=[42.79,-39.50],y=.936,fit_size=[.723,.580,.540],yaw=90)
 elif n=='Downloaded_POS_Receipt_Card_Equipment':m.update(q=[40.94,-37.72],y=.956)
 elif g=='Coffee_Visible_Cups':m.update(q=[42.79+(o['q'][0]-42.175),-39.45+(o['q'][1]+38.975)],y=1.516)
 elif n.startswith('Visible_Blender_'):
  j=int(n.rsplit('_',1)[1]);basez=-38.45+j*.25
  if 'Jug' in n:m.update(q=[42.77,basez],y=1.247 if j==0 else 1.0635,size=[.15,.255,.16])
  elif 'Lid' in n:m.update(q=[42.77,basez],y=1.383 if j==0 else 1.20,size=[.17,.02,.18])
  elif 'Handle' in n:m.update(q=[42.665,basez],y=1.247 if j==0 else 1.0635,size=[.025,.18,.025])
  else:m.update(q=[42.77,basez],y=1.045,size=[.223,.218,.248])
 elif g=='Counter_Equipment_Screen':m.update(q=[o['q'][0]-.71,o['q'][1]-1.98])
 elif g=='Self_Corner' or n.startswith('Self_Corner_Actual_Korean'):m.update(q=[o['q'][0]-.71,o['q'][1]-6.52])
 elif g.startswith('Window_Lounge_Chair_'):
  j=int(g.rsplit('_',1)[1]);dx=-2.8;dz=-1.20
  m.update(q=[o['q'][0]+dx,o['q'][1]+dz])
 elif g=='White_Round_Lounge_Tables':m.update(q=[o['q'][0]-2.8,o['q'][1]-1.20])
 elif g=='Charging_Equipment':m.update(q=[37.5+(o['q'][0]-(38.55 if '_0' in n.split('Recess')[0] or '_0_' in n else 40.35)),(-44.65 if '_0' in n.split('Recess')[0] or '_0_' in n else -42.20)])
 if len(m)>2:p['mutations'].append(m)
# Exact front7.78m and rear7.82m fabrication. Worktops hollow, clear 950mm staff aisle.
FX,RX,Z=40.94,42.82,-39.66
# Fronttop split around physical showcase1.20mopening, not a solid counter enclosingequipment.
b('FU002_front_worktop_12mm_artificial_stone',FX,-40.36,.95,[.95,.012,6.38],'counter-white','Original_FU002_Front_Counter')
for end in [-43.55,-37.17]:b(f'FU002_separate_end_cheek_{end}',FX,end,.445,[.95,.89,.018],'white-finish','Original_FU002_Front_Counter')
b('FU002_upper_front_fabricated_steel_rail',40.478,-40.36,.887,[.024,.06,6.38],'stainless','Original_FU002_Front_Counter')
# Reartop separate pieces produce exact1000mm sink opening and145/630/367 inset fixtures.
rear_start,rear_end=-35.75,-43.57
# Exact12mmrearworktop with all actual insetcutouts. Preserve holes, no solid slab covering equipment.
import numpy as np
from shapely import constrained_delaunay_triangles
worktop=box(42.365,-41.04,43.275,-35.75)
for xx,zz,ww,dd in [(42.80,-36.92,.98,.64),(42.60,-40.48,.145,.130),(42.80,-40.24,.202,.630),(42.77,-38.68,.182,.268),(42.80,-40.79,.367,.417)]:
 worktop=worktop.difference(box(xx-dd/2,zz-ww/2,xx+dd/2,zz+ww/2))
verts=[];tris=[]
def tri(a,c,d):
 k=len(verts);verts.extend([a,c,d]);tris.extend([k,k+1,k+2])
def pp(x,z,y):return [x-42.82,y,z+38.395]
for tt in constrained_delaunay_triangles(worktop).geoms:
 a,c,d=list(tt.exterior.coords)[:3]
 tri(pp(*a,.006),pp(*d,.006),pp(*c,.006));tri(pp(*a,-.006),pp(*c,-.006),pp(*d,-.006))
for ring in [worktop.exterior,*worktop.interiors]:
 pts=list(ring.coords)
 for a,c in zip(pts,pts[1:]):
  tri(pp(*a,-.006),pp(*c,-.006),pp(*c,.006));tri(pp(*a,-.006),pp(*c,.006),pp(*a,.006))
add('FU001_actual_12mm_rearworktop_with_sink_rinser_icebin_cutouts','mesh',42.82,-38.395,.93,[1,1,1],'counter-white','Original_FU001_Back_Counter',vertices=verts,triangles=tris)
# Bottom shelf, open service cavities, thin doors only where not original dishwasher/sterilizer.
b('FU001_rear_low_shelf',RX,-38.395,.10,[.86,.02,5.29],'white-finish','Original_FU001_Back_Counter')
for zz in [-35.75,-37.42,-37.99,-38.53,-39.56,-41.04]:b('FU001_vertical_carcass_'+str(zz),RX,zz,.51,[.86,.82,.018],'white-finish','Original_FU001_Back_Counter')
for j,(zz,w) in enumerate([(-36.4,1.20),(-39.05,.96),(-40.3,1.1)]):
 b(f'FU001_openable_storage_door_{j}',42.365,zz,.50,[.02,.76,w],'white-finish','Original_FU001_Back_Counter')
 b(f'FU001_storage_pull_{j}',42.345,zz+.18,.70,[.025,.018,.15],'silver','Original_FU001_Back_Counter')
b('FU001_white_300mm_backsplash',43.287,-38.395,1.08,[.016,.30,5.29],'white-finish','Original_FU001_Back_Counter')
# Source full commercial sinkfreeasset (licensed model) fitted in real opening. Actual sourcegeometry retained.
imp('Original_P11_stainless_sink_with_faucet','Assets/ChooGuard/Art/StationInterior/R25_Shops/soban/DownloadedQuality003/StainlessSink/StainlessSink.fbx',42.80,-36.92,.74,[.98,.49,.64],yaw=90)
# Source18 compact oven (500x385.3x415.8), freeasset derivative, not text/label.
imp('Original_P11_18_compact_electric_oven','Assets/ChooGuard/Art/StationInterior/R25_Shops/redbean-bread/FreeModels/kaykit/oven.fbx',42.79,-36.08,.936,[.50,.4158,.3853],yaw=90)
# Hollow appliances with separate accessible door, shelf, realhandle/controls and rearservicevoid.
def appliance(n,x,z,bottom,w,d,h,doors=1,glass=False,g='Original_2023_Kitchen_Fixtures'):
 y=bottom+h/2
 for sx in [-1,1]:b(n+'_side_'+str(sx),x+sx*(d/2-.008),z,y,[.016,h,w],'stainless',g)
 for sy in [0,1]:b(n+'_cap_'+str(sy),x,z,bottom+.01+sy*(h-.02),[d,.02,w],'stainless',g)
 b(n+'_back',x+d/2-.01,z,y,[.02,h,w],'stainless',g)
 for j in range(doors):
  zz=z-w/2+w*(j+.5)/doors;ww=w/doors-.012
  b(n+'_separate_door_'+str(j),x-d/2-.004,zz,y,[.018,h-.045,ww],'iceglass' if glass else'stainless',g)
  b(n+'_door_seal_'+str(j),x-d/2+.005,zz,y,[.012,h-.02,ww+.008],'black-rubber',g)
  b(n+'_door_handle_'+str(j),x-d/2-.039,zz+ww*.30,y+.13,[.045,.025,min(.20,ww*.6)],'black',g)
 for sy in [.30,.65]:b(n+'_inner_shelf_'+str(sy),x,z,bottom+h*sy,[d-.06,.012,w-.06],'white-finish',g)
 for j in range(8):b(n+'_condenser_grille_'+str(j),x-d/2-.014,z-w*.32+j*w*.09,bottom+.08,[.016,.09,.007],'black',g)
 for dz in [-w*.38,w*.38]:
  for dx in [-d*.34,d*.34]:cy(n+'_leveling_foot_'+str(dx)+str(dz),x+dx,z+dz,bottom-.025,.018,.05,'black-rubber',g)
 b(n+'_control_face',x-d/2-.016,z-w*.30,bottom+h-.08,[.015,.045,.09],'black',g)
# Exactsource 11/12dimensions; openable doors face staffaisle +x rather than publicwhitefront.
# appliance defaultopensnegativeX; rotate localpart coordinates to positiveX forunder-frontcoldtables.
for num,w,z0,doors in [(12,1.806,-39.05,3),(11,1.506,-40.806,2)]:
 start=len(p['objects']);appliance(f'Original_P11_{num}_refrigerated_table',FX,z0,.035,w,.800,.840,doors)
 for o in p['objects'][start:]:o['q'][0]=2*FX-o['q'][0]
# Rearface units opennegativeX into950mmaislestaff,notpublicoutside.
appliance('Original_P11_09_dishwasher',42.74,-37.72,.38,.555,.500,.450,1)
appliance('Original_P11_10_sterilizer',42.74,-38.31,.32,.521,.478,.510,1,True)
appliance('Original_P11_13_tall_fridge',42.82,-43.12,.035,.740,.803,1.910,1)
appliance('Original_P11_14_reused_ice_maker',42.82,-41.44,.035,.633,.506,1.200,1)
appliance('Original_P11_15_reused_ice_maker',42.82,-42.16,.035,.704,.520,1.600,1)
# Hotwater preciseJS-1, grinderANB060, realspout/driptrayandbeanshopper.
x,z=42.77,-38.97
b('Original_P11_04_hotwater_body',x,z,1.2125,[.353,.553,.243],'white-finish')
b('Original_P11_04_hotwater_front_face',x-.182,z,1.24,[.016,.40,.21],'black')
for j in [-1,1]:
 b('Original_P11_04_hotwater_spout_'+str(j),x-.225,z+j*.065,1.18,[.09,.018,.018],'stainless')
 b('Original_P11_04_hotwater_selector_'+str(j),x-.195,z+j*.065,1.34,[.025,.035,.036],'red-switch' if j<0 else'blueplate')
b('Original_P11_04_hotwater_drip_tray',x-.185,z,.978,[.14,.032,.23],'black')
x,z=42.77,-40.04
b('Original_P11_03_grinder_motor',x,z,1.036,[.16,.20,.13],'silver')
cy('Original_P11_03_grinder_hopper',x,z,1.186,.052,.10,'iceglass')
cy('Original_P11_03_grinder_hopper_lid',x,z,1.241,.06,.012,'black')
b('Original_P11_03_grinder_outlet',x-.10,z,1.08,[.07,.04,.04],'black')
# Dipwell145x130, rinser630x202, knockbox182x268x100,icebin367x417: hollowinsets.
def inset(n,x,z,w,d,depth):
 bottom=.929-depth
 for sx in [-1,1]:b(n+'_rimx_'+str(sx),x+sx*d/2,z,.932,[.014,.012,w+.026],'stainless')
 for sz in [-1,1]:b(n+'_rimz_'+str(sz),x,z+sz*w/2,.932,[d,.012,.014],'stainless')
 for sx in [-1,1]:b(n+'_bowl_side_'+str(sx),x+sx*(d/2-.004),z,.929-depth/2,[.008,depth,w],'stainless')
 for sz in [-1,1]:b(n+'_bowl_end_'+str(sz),x,z+sz*(w/2-.004),.929-depth/2,[d,depth,.008],'stainless')
 b(n+'_bottom',x,z,bottom,[d,.008,w],'stainless');cy(n+'_drain',x,z,bottom+.006,.015,.006,'black')
inset('Original_P11_05_dipwell',42.60,-40.48,.145,.130,.12)
inset('Original_P11_06_pitcher_rinser',42.80,-40.24,.202,.630,.07)
for j in range(11):b('Original_P11_06_rinser_grating_'+str(j),42.54+j*.052,-40.24,.939,[.008,.008,.18],'silver')
cy('Original_P11_06_rinser_push_disk',42.80,-40.24,.952,.035,.018,'black-rubber')
inset('Original_P11_07_knockbox',42.77,-38.68,.182,.268,.10)
cy('Original_P11_07_knockbar',42.77,-38.68,.949,.015,.19,'black-rubber',euler=[90,0,0])
inset('Original_P11_08_icebin',42.80,-40.79,.367,.417,.29)
b('Original_P11_08_separate_lid',42.80,-40.79,.95,[.417,.022,.367],'stainless')
b('Original_P11_08_lid_handle',42.80,-40.79,.972,[.11,.025,.025],'black')
# Physicalutility appliances, valvesandserviceconnectionsfromplumbing/electrical source.
appliance('Original_P11_16_30litre_waterheater',42.78,-36.9,.26,.453,.453,.390,1)
for j in range(2):
 z=-39.85-j*.14;cy(f'Original_P11_17_filter_cartridge_{j}',43.09,z,.47,.04,.49,'white-finish')
 b(f'Original_P11_17_filter_bracket_{j}',43.20,z,.73,[.10,.06,.10],'stainless')
 cy(f'Original_P11_17_filter_valve_{j}',43.08,z,.745,.025,.04,'brass')
# Cylindricalpipebetween localXYZpoints; exactclosedgeometrynotlabel.
def pipe(n,a,c,r=.012,mat='stainless',g='Original_Physical_Utility_Connections'):
 dx,dy,dz=[c[i]-a[i] for i in range(3)];L=math.sqrt(dx*dx+dy*dy+dz*dz);mid=[(a[i]+c[i])/2 for i in range(3)]
 # quaternionfromvertical computedas meshvertices inownqxycoordinates toavoidEuleraxisambiguity.
 v=[];t=[];direction=[dx/L,dy/L,dz/L];ref=[1,0,0] if abs(direction[0])<.9 else[0,0,1]
 cross=lambda a,b:[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
 u=cross(direction,ref);norm=sum(q*q for q in u)**.5;u=[q/norm for q in u];vv=cross(direction,u)
 for pos in [a,c]:
  for i in range(12):
   angle=i*math.tau/12;v.append([pos[j]-mid[j]+r*(u[j]*math.cos(angle)+vv[j]*math.sin(angle)) for j in range(3)])
 for i in range(12):j=(i+1)%12;t.extend([i,j,j+12,i,j+12,i+12])
 for i in range(1,11):t.extend([0,i+1,i,12,12+i,12+i+1])
 add(n,'mesh',mid[0],mid[2],mid[1],[1,1,1],mat,g,vertices=v,triangles=t)
for j,z in enumerate([-36.92,-38.97,-39.50,-40.38,-40.79,-41.44,-42.16]):
 pipe(f'Original_water_service_branch_{j}',[43.18,.25,z],[43.18,.89,z],.010,'brass')
 pipe(f'Original_water_service_elbow_{j}',[43.18,.89,z],[42.86,.89,z],.010,'brass')
 b(f'Original_water_isolation_valve_{j}',43.18,z,.39,[.06,.028,.025],'red-switch','Original_Physical_Utility_Connections')
pipe('Original_underworktop_watermain',[43.18,.25,-36.7],[43.18,.25,-42.45],.013,'brass')
pipe('Original_sink_drain_drop',[42.85,.74,-36.92],[42.85,.22,-36.92],.025,'black-rubber')
pipe('Original_sink_drain_outlet',[42.85,.22,-36.92],[43.22,.22,-36.92],.025,'black-rubber')
for j,z in enumerate([-36.1,-37.72,-38.31,-38.45,-38.97,-39.50,-40.04,-41.44,-42.16,-43.12]):
 b(f'Original_220V_outlet_plate_{j}',43.274,z,.75,[.016,.09,.075],'white-finish','Original_Physical_Utility_Connections')
 for k in [-1,1]:
  cy(f'Original_220V_round_socket_{j}_{k}',43.263,z+k*.019,.75,.013,.008,'black',euler=[0,0,90],g='Original_Physical_Utility_Connections')
  b(f'Original_220V_contact_{j}_{k}',43.257,z+k*.019,.75,[.006,.018,.004],'black-rubber','Original_Physical_Utility_Connections')
 pipe(f'Original_appliance_powercord_{j}',[43.25,.74,z],[43.07,.50 if j in [1,2,7,8,9] else 1.0,z],.004,'black-rubber')
# Current-source curvedshowcase, floor-standing, original1200slot. Preserve contemporarycurvedprofile; originalheight1500.
G='Current_Field_Curved_Showcase';X,Z=40.94,-36.53;W,D=1.20,.70
for zz in [-W/2,W/2]:b('Curved_case_base_end_'+str(zz),X,Z+zz,.35,[D,.65,.025],'black',G)
for yy in [.035,.67]:b('Curved_case_base_cap_'+str(yy),X,Z,yy,[D,.035,W],'black',G)
b('Curved_case_base_rear',X+D/2,Z,.35,[.025,.66,W],'black',G)
b('Curved_case_lower_real_glass_door',X-D/2-.005,Z,.36,[.008,.56,W-.065],'iceglass',G)
b('Curved_case_lower_glass_handle',X-D/2-.028,Z-.43,.36,[.032,.17,.026],'white',G)
for yy in [.75,1.04]:b('Curved_case_openable_shelf_'+str(yy),X,Z,yy,[.62,.014,W-.04],'silver',G)
# Curvedclosedglassshellupperfront fromsourcefield, plusseparatebackslidingdoors.
v=[];t=[];N=16
for j in range(N+1):
 theta=j/N*math.pi/2
 for zz in [-W/2,W/2]:v.append([-D/2+.24*(1-math.cos(theta)),.76+.70*math.sin(theta),zz])
for j in range(N):a=j*2;t.extend([a,a+2,a+3,a,a+3,a+1])
n=len(v);v += [[x+.006,y,z] for x,y,z in v]
for tri in [t[i:i+3] for i in range(0,len(t),3)]:t.extend([tri[2]+n,tri[1]+n,tri[0]+n])
for j in range(N):
 for k in [0,1]:a=j*2+k;c=(j+1)*2+k;t.extend([a,c,c+n,a,c+n,a+n])
for a,c in [(0,1),(N*2,N*2+1)]:t.extend([a,c,c+n,a,c+n,a+n])
add('Curved_case_actual_source_glass','mesh',X,Z,0,[1,1,1],'iceglass',G,vertices=v,triangles=t)
for zz in [-W/2,W/2]:b('Curved_case_clear_end_'+str(zz),X,Z+zz,1.11,[D,.70,.006],'iceglass',G)
b('Curved_case_glass_flat_top',X+.13,Z,1.463,[.44,.006,W],'iceglass',G)
for j in [-1,1]:b('Curved_case_rear_sliding_panel_'+str(j),X+.355,Z+j*.285,1.11,[.006,.68,.57],'iceglass',G)
for j in range(4):
 imp(f'Downloaded_visible_pastry_{j}','Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/hamburger_buns/hamburger_buns.fbx',X-.01,Z-.43+j*.285,1.048,[.14,.08,.12],g=G,yaw=90)
 for side in [-1,1]:b(f'Actual_pastry_pouch_{j}_{side}',X+side*.09,Z-.43+j*.285,1.17,[.006,.23,.17],'white',G,collider=False)
for r in range(2):
 for j in range(6):
  xx=X-.17+r*.20;zz=Z-.46+j*.18;cy(f'Actual_water_bottle_{r}_{j}',xx,zz,.36,.032,.22,'blue-water',G);cy(f'Actual_water_bottle_cap_{r}_{j}',xx,zz,.482,.021,.024,'white',G)
# SourceCH3100finish, actualcurrentopen square100grid. Ownkitchenfinish only, structuralroof fixed.
G='Original_2023_CH3100_Own_Kitchen_Finish'
b('Original_CH3100_rear_gypsum_finish_band',42.82,-39.66,3.11,[.91,.02,7.82],'white-finish',G)
for i in range(20):b(f'Original_100mm_grid_longitudinal_{i}',40.465+i*.1,-39.66,3.115,[.003,.05,7.82],'white-finish',G,collider=False)
for j in range(79):b(f'Original_100mm_grid_cross_{j}',41.415,-35.75-j*.1,3.115,[1.9,.05,.003],'white-finish',G,collider=False)
for j,z in enumerate([-36.6,-38.65,-40.5,-42.4]):
 cy(f'Current_source_downlight_physical_bezel_{j}',42.82,z,3.087,.052,.012,'silver',G)
 cy(f'Current_source_downlight_glass_lens_{j}',42.82,z,3.079,.041,.006,'white-finish',G)
# Photovisiblewall-mounted2heademergency, detectorphysicalbodies; no Lightcomponents.
b('Current_source_emergency_twin_head_battery',43.27,-38.2,2.45,[.11,.24,.31],'white-finish',G)
for j in [-1,1]:
 b(f'Current_source_emergency_separate_head_{j}',43.20,-38.2+j*.13,2.64,[.10,.115,.12],'white-finish',G)
 b(f'Current_source_emergency_head_diffuser_{j}',43.143,-38.2+j*.13,2.64,[.008,.085,.09],'white',G)
cy('Current_source_wall_detector',43.262,-37.75,2.55,.055,.035,'white-finish',G,euler=[0,0,90])
# Record no new surveyedpositions. Offline envelopecut enforces source-modelfit, notagentQAstage.
env=Polygon(json.load(open(O.parent/'LATER-R24-BOUNDED-PUBLIC-LOUNGE-AUTHORING-BASIS-R26.json'))['working_envelope_qx_minus_qy']['coordinates'][0])
# Equipmentroomsource originalrough scale retained; no geometryoutside acceptedcell.
relation={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'originals':{'architecture':str(Path('.state/ACTIVE-ALL2F-COMPLETION-R26/original-design-discovery/storyway-public-contracts/operator-archive-r27/transport-mirror-r28/lounge2023-full-browser-headers-FILE00.pdf')),'viewed_pages':[6,10,11,20,21],'sheets':'Busan I-106, I-201, FU001, FU002; cover/indexYongsan templateleftover not placementauthority','epoch':'2022designissued2023, hiddenequipment DESIGN MODEL; current2026sourcewinschangedshowcase/3pitchers/onepoweredblender'},'current_photos':['public/lounge/sources/kgs6897/FIELD-05.jpg','public/lounge/sources/suneunjaeyongin/FIELD-04.jpg','public/lounge/sources/dail2yeon/FIELD-07.jpg'],'fixed_envelope':str((O.parent/'LATER-R24-BOUNDED-PUBLIC-LOUNGE-AUTHORING-BASIS-R26.json').relative_to(R)),'source_dimension_m':{'front_counter_length':7.78,'rear_counter_length':7.82,'front_depth':.95,'rear_depth':.91,'staff_aisle':.95,'finish_ceiling':3.10,'refrigerated_tables':[1.806,1.506],'18_source_equipment_entries':'existingespresso+1poweredblender2photovisiblesparejugs+grinder+hotwater+dipwell+rinser+knockbox+icebin+dishwasher+sterilizer+2coldtables+tallfridge+2reusedicemakers+30Lwaterheater+2filters+oven'},'current_change':'correct old4.2mfront/tinyrear genericcounter; physical source equipment and hollow utilities; retain own sourcebench andsourcecurrentfourchairs relocatedwithinowncell toretain950mmstaffaisle; currentcurvedcasefloorstanding1.5m top ratherthan incorrectlyperched2.03m; actualwhitecounterfront and300mmbacksplash; physicalonlyCH3100plainrearband and100mmsquaregrid','preserve':'Allnative/Storywaypartywall/columns/floor/C176/X/openings/otherstores/1F/currentphysicalsigns untouched; originalassetssharedread-only','original_source_assets_changed':False,'Light_Text_TMP_created':False,'new_floor_wall_native_cut':False,'agent_QA_tests_Play_probe_reopen_capture':False,'counts':{'add':len(p['objects']),'modify':len(p['mutations']),'remove':len(p['removals'])}}
(B/'SOURCE-RELATIONSHIPS-R28.json').write_text(json.dumps(relation,ensure_ascii=False,indent=2));(B/'LOUNGE-ORIGINAL-PACKET-R28.json').write_text(json.dumps(p,ensure_ascii=False,separators=(',',':')))
print(json.dumps(relation['counts']))
