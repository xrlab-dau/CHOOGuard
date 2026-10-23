from pathlib import Path
import json, math, hashlib
from shapely.geometry import box as sbox, Point
from shapely.ops import triangulate
W=Path(__file__).resolve().parent
P={'status':'ORIGINAL_ELECTRICAL_DESIGN_MIGRATED_TO_CURRENT_OWN_FURNITURE_MODEL','source_design':'E003/E004/E009/E010/E011, July2026, not as-built survey','objects':[], 'mesh_library':{},'assemblies':[], 'materials':{
 'enamel':{'color':[.84,.85,.84,1],'metallic':.5,'smoothness':.35},'white':{'color':[.91,.90,.87,1],'metallic':0,'smoothness':.4},'black':{'color':[.025,.028,.03,1],'metallic':0,'smoothness':.34},'steel':{'color':[.58,.61,.62,1],'metallic':.88,'smoothness':.58},'copper':{'color':[.57,.27,.12,1],'metallic':.9,'smoothness':.4},'blue':{'color':[.04,.16,.51,1],'metallic':0,'smoothness':.24},'green':{'color':[.08,.39,.12,1],'metallic':0,'smoothness':.28},'yellow':{'color':[.85,.7,.10,1],'metallic':0,'smoothness':.28},'red':{'color':[.56,.04,.025,1],'metallic':0,'smoothness':.22},'glass':{'color':[.30,.39,.43,.22],'metallic':.1,'smoothness':.9,'transparent':True},'diffuser':{'color':[.87,.87,.82,1],'metallic':0,'smoothness':.5}}}
O=P['objects'];L=P['mesh_library']
def group(name,basis):P['assemblies'].append({'name':name,'basis':basis,'classification':'SOURCE_DESIGN_MODEL_NOT_MEASURED_ASBUILT'})
def part(g,n,pos,size,mat='enamel',kind='box',euler=None,mesh=None):
 o={'group':g,'id':n,'kind':kind,'position':pos,'size':size,'material':mat,'collider':kind=='box' and min(size)>.012}
 if euler:o['euler']=euler
 if mesh:o['mesh']=mesh
 O.append(o);return o
def b(g,n,x,y,z,w,h,d,m='enamel'):return part(g,n,[x,y,z],[w,h,d],m)
def cyl(g,n,x,y,z,dia,height,m='steel',rot=None):return part(g,n,[x,y,z],[dia,height/2,dia],m,'cylinder',rot)
def line(g,n,a,c,diam=.016,m='steel'):
 v=[c[i]-a[i] for i in range(3)];ln=math.sqrt(sum(x*x for x in v));
 if ln<1e-6:return
 o=part(g,n,[(a[i]+c[i])/2 for i in range(3)],[diam,ln/2,diam],m,'conduit');o['axis']=v

def shape_mesh(key,poly,thick):
 verts=[];tri=[]
 for tr in triangulate(poly):
  if not poly.covers(tr):continue
  pts=list(tr.exterior.coords)[:3];j=len(verts)
  verts += [[x,y,0] for x,y in pts]+[[x,y,thick] for x,y in pts]
  tri += [j,j+2,j+1,j+3,j+4,j+5]
 for ring in [poly.exterior,*poly.interiors]:
  pts=list(ring.coords)
  for (x,y),(a,c) in zip(pts,pts[1:]):
   j=len(verts);verts += [[x,y,0],[a,c,0],[a,c,thick],[x,y,thick]];tri += [j,j+1,j+2,j,j+2,j+3]
 L[key]={'vertices':verts,'triangles':tri};return key
# True cut-outs, rather than painted dark socket holes.
plate=sbox(-.06,-.037,.06,.037)
for cx in [-.03,.03]:plate=plate.difference(Point(cx,0).buffer(.021,resolution=20))
shape_mesh('duplex_plate_real_apertures',plate,.006)
inner=Point(0,0).buffer(.0185,resolution=20)
for cx in [-.0095,.0095]:inner=inner.difference(Point(cx,0).buffer(.0026,resolution=8))
shape_mesh('schuko_socket_pin_apertures',inner,.003)
ring=Point(0,0).buffer(.021,resolution=20).difference(Point(0,0).buffer(.0185,resolution=20));shape_mesh('schuko_cup_wall',ring,.017)
# Distribution board, top FL+1800, 700W1100H180D: storage face of current divider x4.36.
g='LP_CVS_DISTRIBUTION_BOARD';group(g,'E004 p5 board dimensions and circuit schedule; E009 p10 source side-storage partition; actual current storage_partition_2_16, doorway held fixed.')
x0=4.365;zc=3.43;yc=1.25
b(g,'steel_backplate',x0+.009,yc,zc,.018,1.10,.70)
for z in [zc-.344,zc+.344]:b(g,'hollow_enclosure_side_'+str(z),x0+.09,yc,z,.18,1.10,.012)
for y in [yc-.544,yc+.544]:b(g,'hollow_enclosure_end_'+str(y),x0+.09,y,zc,.18,.012,.688)
# Separate front frame/hinged editable door, with physical meter aperture and lower ventilation slots.
xf=x0+.19
for z in [zc-.337,zc+.337]:b(g,'door_edge_'+str(z),xf,yc,z,.018,1.075,.025)
for y in [yc-.525,yc+.525]:b(g,'door_end_'+str(y),xf,y,zc,.018,.025,.65)
# Meter observation aperture spans y1.42..1.69, z3.23..3.60.
b(g,'door_panel_below_window',xf,1.0725,zc,.018,.695,.65)
b(g,'door_panel_above_window',xf,1.7375,zc,.018,.085,.65)
b(g,'door_panel_window_left',xf,1.555,zc-.255,.018,.28,.14)
b(g,'door_panel_window_right',xf,1.555,zc+.255,.018,.28,.14)
b(g,'meter_clear_observation_pane',xf+.003,1.555,zc,.007,.26,.37,'glass')
# Main molded-case breaker and metering mechanism; protected behind separate door.
b(g,'main_4pole_MCCB_50AF_50AT',x0+.082,1.34,zc+.12,.09,.15,.16,'black')
for k in range(4):b(g,'main_pole_toggle_'+str(k),x0+.139,1.34,zc+.068+k*.035,.028,.049,.026,'black')
b(g,'main_handle_crossbar',x0+.157,1.338,zc+.12,.014,.025,.145,'black')
b(g,'electronic_3phase_meter',x0+.075,1.58,zc,.115,.145,.19,'white')
b(g,'meter_recessed_display',x0+.135,1.60,zc,.006,.045,.10,'black')
for yy in [.90,1.60]:
 cyl(g,'door_hinge_'+str(yy),xf-.016,yy,zc+.344,.018,.1,'steel')
cyl(g,'recessed_door_latch',xf+.014,1.27,zc-.27,.025,.012,'steel',[0,0,90])
# Thirteen source schedule branches (CKT1,3..14); ratings are metadata, not a printed text proxy.
branches=[('OH1_4P_ELB',1),('L1_2P_ELB',3),('EX1_2P_ELB',4),('R1_2P_AFCI',5),('SPARE_A_2P_ELB',6),('R2_2P_ELB',7),('SPARE_B_2P_ELB',8),('J1_2P_ELB',9),('EH1_2P_ELB',10),('R3_2P_ELB',11),('SPARE_C_2P_ELB',12),('SPARE_D_2P_ELB',13),('SPARE_E_2P_ELB',14)]
for i,(name,num) in enumerate(branches):
 y=1.15-(i//2)*.055;z=zc+(-.16 if i%2==0 else .12)
 b(g,'breaker_'+name,x0+.076,y,z,.085,.047,.115 if i else .145,'white')
 b(g,'breaker_toggle_'+name,x0+.125,y,z,.023,.025,.026,'black')
 for dz in [-.035,.035]:cyl(g,'screw_'+str(num)+'_'+str(dz),x0+.124,y+.015,z+dz,.009,.004,'steel',[0,0,90])
for i,m in enumerate(['red','black','blue','white']):b(g,'insulated_phase_link_'+str(i),x0+.038,1.29,zc-.23+i*.028,.014,.065,.012,m)
for z,m in [(zc-.26,'green'),(zc+.26,'blue')]:
 b(g,m+'_terminal_rail',x0+.045,.79,z,.028,.045,.085,'copper')
 for dz in [-.028,0,.028]:cyl(g,m+'_terminal_screw'+str(dz),x0+.063,.79,z+dz,.008,.004,'steel',[0,0,90])
# Clear flame-retardant deadfront over live terminals, individual component remains editable.
b(g,'clear_deadfront_shield',x0+.142,1.06,zc,.004,.55,.54,'glass')
# Source separate 300x300x150mm pullbox above ceiling, no opening of existing ceiling.
g='UPPER_PULLBOX_AND_CABLE_DUCT';group(g,'E009 pullbox300x300x150 above own storage false ceiling; E004 vertical steel duct300x100 from board to own ceiling, no station main feeders.')
b(g,'pull_box_body',4.48,3.35,zc,.15,.30,.30)
b(g,'pull_box_cover',4.56,3.35,zc,.009,.27,.27)
for y in [3.23,3.47]:
 for z in [zc-.12,zc+.12]:cyl(g,'cover_screw_'+str(y)+str(z),4.568,y,z,.008,.003,'steel',[0,0,90])
b(g,'steel_duct_back',4.375,2.49,zc,.015,1.38,.30)
for z in [zc-.145,zc+.145]:b(g,'steel_duct_side_'+str(z),4.415,2.49,z,.095,1.38,.01)
b(g,'removable_duct_cover',4.467,2.49,zc,.012,1.38,.30)
line(g,'ceiling_pullbox_to_duct',[4.48,3.19,zc],[4.48,3.35,zc])
# Source three 15A duplex receptacles. Positions follow existing counter cheek and storage divider surfaces.
def socket(g,n,x,y,z,face='right',powerstrip=False):
 # reusable local XY faceplate points +Z; rotate face normal towards +X for side wall, +Z for strip front.
 rot=[0,90,0] if face=='right' else [0,0,0]
 if face=='up':rot=[-90,0,0]
 def localpos(a,c,d):
  if face=='right':return [x+d,y+c,z-a]
  if face=='up':return [x+a,y+d,z-c]
  return [x+a,y+c,z+d]
 part(g,n+'_actual_cutout_faceplate',[x,y,z],[1,1,1],'white','mesh',rot,'duplex_plate_real_apertures')
 for k,cx in enumerate([-.03,.03]):
  pp=localpos(cx,0,-.017);part(g,n+'_hollow_cup_'+str(k),pp,[1,1,1],'white','mesh',rot,'schuko_cup_wall')
  pp=localpos(cx,0,-.019);part(g,n+'_real_pin_holes_'+str(k),pp,[1,1,1],'white','mesh',rot,'schuko_socket_pin_apertures')
  for sy in [-.017,.017]:
   pp=localpos(cx,sy,-.007);part(g,n+'_earth_spring_'+str(k)+str(sy),pp,[.012,.002,.012],'steel','box',rot)
  # Separate dark recess back is behind the real through apertures.
  pp=localpos(cx,0,-.024);part(g,n+'_dark_recess_'+str(k),pp,[.039,.039,.003],'black','box',rot)
  pp=localpos(cx,.023,-.006);part(g,n+'_socket_fire_patch_'+str(k),pp,[.024,.006,.002],'red','box',rot)
 # source mounting backing box, not a structural wall cut.
 pp=localpos(0,0,-.029);part(g,n+'_concealed_back_box',pp,[.12,.075,.023],'enamel','box',rot)
g='RECEPTACLES_AND_POS_MULTITAP';group(g,'E009 p10 three earthed duplex15A receptacles, H300; two counter furniture recesses, one storage. E003/E009 six-way auto-fire-extinguishing POS multitap with3m cord. Existing counter preserved.')
socket(g,'COUNTER_R1_MAIN',4.205,.30,4.23)
socket(g,'COUNTER_R1_INDIRECT',4.205,.30,4.45)
socket(g,'STORAGE_R3',4.373,.30,2.79)
# Three two-socket units set into one six-way fire-retardant strip, accessible from rear counter.
b(g,'POS_sixway_strip_body',3.10,.30,4.44,.39,.035,.095,'white')
for i,x in enumerate([2.97,3.10,3.23]):socket(g,'POS_sixway_pair_'+str(i),x,.319,4.44,'up',True)
b(g,'POS_strip_master_switch',2.92,.324,4.44,.022,.012,.04,'red')
# Switches directly on current existing rear partition, source H1200. Separate plate/rockers.
g='SOURCE_LIGHTING_AND_EQUIPMENT_SWITCHES';group(g,'E009/E010/E011 marked storage switch cluster; source1200mm center; 3gang hall/storage illumination plus2gang sign/door and2gang display/counter.')
for i,(name,n) in enumerate([('L1_HALL_STORE',3),('J1_SIGN_AUTODOOR',2),('R2_DISPLAY_COUNTER',2)]):
 z=5.68-i*.16;b(g,name+'_plate',4.375,1.2,z,.012,.12,.10,'white')
 for k in range(n):b(g,name+'_rocker_'+str(k),4.385,1.2,z-.036+(k+.5)*(.072/n),.014,.068,.065/n,'white')
# SMPS assemblies shown in furniture. Rear3 + left3 + one right lower module remain inside actual cabinets.
g='SOURCE_DISPLAY_POWER_SUPPLIES';group(g,'E009 furniture-concealed SMPS symbols:3rear,3left,1right at source depth2.7/currentcabinet2.56. E009 second right supply at source depth1.05 conflicts with fixed native support/current cupboard absence; not fabricated or buried in nativecolumn.')
smps=[('REAR_A',.52,5.64),('REAR_B',2.0,5.64),('REAR_C',3.64,5.64),('LEFT_A',.37,4.7),('LEFT_B',.37,3.0),('LEFT_C',.37,1.35),('RIGHT_A',3.995,2.56)]
for n,x,z in smps:
 b(g,n+'_power_supply_body',x,.18,z,.20,.045,.09,'steel')
 for k in range(7):b(g,n+'_vent_louvre_'+str(k),x-.075+k*.025,.204,z,.003,.002,.07,'black')
 b(g,n+'_terminal_guard',x+.075,.188,z+.05,.06,.035,.015,'black')
 for k in range(3):cyl(g,n+'_terminal_screw_'+str(k),x+.055+k*.018,.208,z+.05,.007,.002,'steel')
 line(g,n+'_input_pigtail',[x+.1,.18,z],[x+.14,.18,z+.05],.007,'black')
# Local source-concealed 16mm electrical routing, never chase native columns or cut floors.
g='SOURCE_LOCAL_CONDUIT_AND_JOINT_BOXES';group(g,'E009/E010/E011 SF16 non-waterproofsteelconduit routing, furniture concealed atbase, ceiling routes aboveownfalseceiling, source7+4+4jointboxes. Excludes upstreamoutsidebooth feeders and unbound lower-right native-column region.')
# Source floor-embedded branches are modeled40mm below acceptedfinishedfloor, depthMODEL; no floor mesh changes and no raised portal-crossing pipe.
for n,a,c in [
 ('R1_board_floor_vertical',[4.40,1.20,3.43],[4.40,-.04,3.43]),
 ('R1_floor_approach',[4.40,-.04,3.43],[4.17,-.04,3.43]),
 ('R1_counter_buried_feed',[4.17,-.04,3.43],[4.17,-.04,4.45]),
 ('R1_counter_socket_riser_A',[4.17,-.04,4.23],[4.17,.30,4.23]),
 ('R1_counter_socket_riser_B',[4.17,-.04,4.45],[4.17,.30,4.45]),
 ('R1_counter_socket_stub_A',[4.17,.30,4.23],[4.205,.30,4.23]),
 ('R1_counter_socket_stub_B',[4.17,.30,4.45],[4.205,.30,4.45]),
 ('R3_storage',[4.40,.30,3.43],[4.40,.30,2.79]),
 ('R2_to_base',[4.40,.30,3.43],[4.40,-.04,3.43]),
 ('R2_behind_right_cabinet',[4.40,-.04,3.43],[4.40,-.04,5.72]),
 ('R2_right_supply_buried',[4.40,-.04,3.43],[4.40,-.04,2.56]),
 ('R2_rear_furniture_run',[4.40,-.04,5.72],[.25,-.04,5.72]),
 ('R2_left_furniture_run',[.25,-.04,5.72],[.25,-.04,.35]),
 ('SW_cluster_to_ceiling',[4.40,1.20,5.38],[4.40,3.28,5.38]),
 ('SW_upper_trunk',[4.40,3.28,5.38],[4.40,3.28,3.43]),
 ('AC_local_supply',[4.40,3.61,3.43],[2.165,3.61,3.43]),
 ('AC_into_terminal',[2.165,3.61,3.43],[2.165,3.61,2.83]),
 ('DOOR_own_header_local',[4.86,3.19,.425],[1.98,3.19,.425]),
 ('DOOR_header_drop',[1.98,3.19,.425],[1.98,2.32,.425]),
 ('HALL_ceilingtrunk',[3.665,3.61,4.30],[.665,3.61,4.30])]:line(g,n,a,c)
for i,x in enumerate([.665,1.565,2.765,3.665]):
 line(g,'ceiling_rail_feed_'+str(i),[x,3.61,4.30],[x,3.61,1.0]);line(g,'ceiling_rail_drop_'+str(i),[x,3.61,4.30],[x,3.39,4.30])
# Actual source 102x102x54 junctionboxes, distinct network functions, not arbitrary room objects.
joints=[('R1_COUNTER',4.17,.075,4.23),('R2_REAR',4.40,.075,5.72),('R2_REAR_MID',2.0,.075,5.72),('R2_LEFT_FRONT',.25,.075,.35),('R2_LEFT_MID',.25,.075,2.15),('R2_LEFT_REAR',.25,.075,5.0),('R2_RIGHT',4.40,.075,2.56),('L1_A',.665,3.61,4.30),('L1_B',1.565,3.61,4.30),('L1_C',2.765,3.61,4.30),('L1_D',3.665,3.61,4.30),('EH1_AC',2.165,3.61,2.83),('J1_AUTODOOR',1.98,2.32,.425),('J1_SIGN',4.86,3.19,.425),('J1_RETURN',4.40,3.28,3.43)]
for n,x,y,z in joints:b(g,n+'_102_joint_box',x,y,z,.102,.054,.102);b(g,n+'_removable_cover',x,y+.03,z,.098,.005,.098)
for n,x,z in smps:
 base=[x,-.04,z]
 junction=[.25,-.04,z] if n.startswith('LEFT') else ([x,-.04,5.72] if n.startswith('REAR') else [4.40,-.04,z])
 line(g,n+'_floor_spur',junction,base)
 line(g,n+'_furniture_riser',base,[x,.18,z])
# Protective cable3m for actual POS support furniture, routed under rear accessible shelf.
path=[[3.10,.28,4.44],[3.65,.28,4.44],[4.10,.28,4.44],[4.10,.3,4.23],[4.20,.3,4.23]]
for i,(a,c) in enumerate(zip(path,path[1:])):line(g,'POS_supply_cord_'+str(i),a,c,.008,'black')
vv=[];tt=[]
for i in range(36):
 a=2*math.pi*i/36
 for j in range(6):
  c=2*math.pi*j/6;vv.append([(.09+.004*math.cos(c))*math.cos(a),.004*math.sin(c),(.09+.004*math.cos(c))*math.sin(a)])
for i in range(36):
 for j in range(6):
  a=i*6+j;bb=((i+1)%36)*6+j;c=((i+1)%36)*6+(j+1)%6;dd=i*6+(j+1)%6;tt.extend([a,bb,c,a,c,dd])
L['POS_spare_cord_coil']={'vertices':vv,'triangles':tt}
for i in range(3):part(g,'POS_spare_cord_coil_'+str(i),[3.40,.28+i*.009,4.40],[1,1,1],'black','mesh',mesh='POS_spare_cord_coil')
# Missing storage fixtures are physical housings only; source counts don't restore deleted lights.
g='SOURCE_STORAGE_FIXTURE_HARDWARE_ONLY';group(g,'E010 3six-inchdownlight physicalcases atstoragefront/rear matchingcurrentceilingbottom3.2025; the source13th1200mmT7wouldhitfixedcurrentframeinitsdrawnorientationandisheld; Lightcomponentsremainabsent.')
for i,z in enumerate([2.89,4.10,5.34]):
 cyl(g,'storage_downlight_housing_'+str(i),4.865,3.195,z,.172,.024,'white')
 cyl(g,'storage_downlight_diffuser_'+str(i),4.865,3.181,z,.145,.004,'diffuser')
# Source1200mmfrontT7notadded: currentclearspan1.01m andfixedframewouldintersect. No guessedshorterreplacement.
# All source-accurate equipment constraints and exclusions kept in metadata.
P['scope_exclusions']=['Source floor-conduit burialdepth40mm MODEL, not survey; no floor mesh is cut and portal crossings stay belowfinish','No upstream OLIVEYOUNG main distribution or corridor feeder invented','No originalsourceAssets or nativecolumn/shell/floor/circulation/otherstore changes','Source13th1200mmT7atstoragefrontnotplaced because1.01mcurrentclearwidthdoesnotfithardwarewithoutfixedframesintersection','Source second right-side SMPS atdepth1.05 not placed because currentnativecolumnclearance overrides symbolpixels','No Light/Text/TMP or printed-sign planes','Manufacturer-unspecified modeled casings/conduit dimensions are source-design MODEL, not surveyedfieldvalues','Currentcounter/door/shell/merchandise positions stayfixed; actualphoto sourceepochOct2026, designJuly2026']
P['counts']={'physical_parts':len(O),'equipment_assemblies':len(P['assemblies']),'distribution_boards':1,'pullboxes':1,'branch_breakers':13,'duplex_receptacles':3,'sixway_powerstrips':1,'smps':len(smps),'junctionboxes':len(joints),'new_downlight_housings':3,'unplaced_source_T7_conflict':1,'unplaced_source_SMPS_conflict':1,'Light_components_added':0}
(W/'ELECTRICAL-PACKET-R26.json').write_text(json.dumps(P,ensure_ascii=False,indent=2))
print(json.dumps(P['counts'],ensure_ascii=False));print('sha256',hashlib.sha256((W/'ELECTRICAL-PACKET-R26.json').read_bytes()).hexdigest())
