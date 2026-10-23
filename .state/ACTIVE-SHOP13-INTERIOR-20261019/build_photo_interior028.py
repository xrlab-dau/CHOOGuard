from pathlib import Path
import json,math,numpy as np
B=Path('.state/ACTIVE-SHOP13-INTERIOR-20261019');objs=json.loads((B/'CURRENT-ACCEPTED-BOOTH-INTERIOR-INPUTS-027.json').read_text())['objects'];D={int(o['id'].split('-')[-2]):o for o in objs};parts=[];mats={
'cream':{'color':'#E6E1CC','metallic':0,'smoothness':.28},'wood':{'color':'#A97C3B','metallic':0,'smoothness':.4},'darkwood':{'color':'#412C1D','metallic':0,'smoothness':.37},'bench':{'color':'#2E2019','metallic':0,'smoothness':.32},'steel':{'color':'#B5BFBE','metallic':.82,'smoothness':.68},'black':{'color':'#181A18','metallic':.12,'smoothness':.78},'brass':{'color':'#AB873B','metallic':.72,'smoothness':.48},'ceramic':{'color':'#E5E7DF','metallic':0,'smoothness':.62},'blue':{'color':'#193B68','metallic':0,'smoothness':.6},'warm':{'color':'#FFF4DA','metallic':0,'smoothness':.3,'emission':'#FFE8B7'},'white':{'color':'#E3E6DC','metallic':0,'smoothness':.32},'vent':{'color':'#353B37','metallic':.1,'smoothness':.2},'clear':{'color':'#D4DDDA50','metallic':0,'smoothness':.75,'transparent':True}}
A=-.34290633335024717;C=-.9393696006089878
src={'room':'current-photo-inputs/pyj727174-02-original.jpg','wall':'current-photo-inputs/pyj727174-03-original.jpg','bench':'current-photo-inputs/pyj727174-04-original.jpg','hood':'current-photo-inputs/csu03102-04-original.jpg','wash':'current-photo-inputs/bsftl-11-original.jpg','niche':'current-photo-inputs/mdc0318-11-original.jpg','cashier':'current-photo-inputs/babyrim-00-original.jpg','cabinet':'current-photo-inputs/pyj727174-05-original.jpg'}
def world(v):x,y,z=v;return [A*x+C*z,y,C*x-A*z]
def add(name,mat,v,t,source,collider=False):parts.append({'name':name,'material':mat,'vertices':[world(p) for p in v],'triangles':t,'source':src[source],'collider':collider})
def cubegeom(c,s):
 c=np.array(c);h=np.array(s)/2;vs=[];ts=[];corn=[c+h*np.array(v) for v in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
 for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]:
  k=len(vs);vs.extend([corn[i].tolist() for i in f]);ts.extend([k,k+1,k+2,k,k+2,k+3])
 return vs,ts
# q basis reflects XZ, reverse triangle winding when converting to world.
def box(name,c,s,mat,source,solid=False):v,t=cubegeom(c,s);add(name,mat,v,t,source,solid)
def combineboxes(name,boxes,mat,source):
 v=[];t=[]
 for c,s in boxes:
  vv,tt=cubegeom(c,s);n=len(v);v+=vv;t +=[n+x for x in tt]
 add(name,mat,v,t,source)
def cyl(name,c,rx,rz,h,mat,source,n=32):
 x,y,z=c;v=[];t=[]
 for k in range(n):
  a=k*2*math.pi/n;b=(k+1)*2*math.pi/n;v.extend([[x+rx*math.cos(a),y-h/2,z+rz*math.sin(a)],[x+rx*math.cos(b),y-h/2,z+rz*math.sin(b)],[x+rx*math.cos(b),y+h/2,z+rz*math.sin(b)],[x+rx*math.cos(a),y+h/2,z+rz*math.sin(a)]]);o=4*k;t.extend([o,o+2,o+1,o,o+3,o+2])
 v += [[x,y-h/2,z],[x,y+h/2,z]];lo=len(v)-2;hi=len(v)-1
 for k in range(n):o=4*k;t += [lo,o,o+1,hi,o+2,o+3]
 add(name,mat,v,t,source)
def tube(name,path,r,mat,source,n=10):
 pp=np.array(path,float);v=[];t=[]
 for i,p in enumerate(pp):
  ax=pp[min(i+1,len(pp)-1)]-pp[max(i-1,0)];ax/=np.linalg.norm(ax);ref=np.array([1.,0,0]) if abs(ax[0])<.9 else np.array([0.,0,1]);a=np.cross(ax,ref);a/=np.linalg.norm(a);b=np.cross(ax,a)
  v.extend([(p+r*(math.cos(k*2*math.pi/n)*a+math.sin(k*2*math.pi/n)*b)).tolist() for k in range(n)])
 for i in range(len(pp)-1):
  for k in range(n):a=i*n+k;b=i*n+(k+1)%n;t +=[a,b,b+n,a,b+n,a+n]
 add(name,mat,v,t,source)
# Accepted-booth interior partition: exact shell remains untouched. Existing north WC and frontglass are external contacts.
box('kitchen_long_cream_partition_between_staff_and_cook_apertures',[86.66,1.9725,20.425],[.12,3.875,3.25],'cream','wall',True)
box('kitchen_cook_aperture_upper_header',[86.66,3.655,22.665],[.12,.51,1.23],'cream','hood',True)
box('kitchen_front_cream_return_inside_fixed_glass',[86.66,1.9725,23.885],[.12,3.875,1.21],'cream','hood',True)
box('kitchen_north_profile_contact_inside_accepted_booth',[86.66,1.9725,14.81],[.12,3.875,.12],'cream','wall',True)
# Individually editable rounded edge at the tall cooking aperture; radius matches photographic soft lining, no column body added.
cyl('cooking_aperture_soft_cream_partition_jamb',[86.67,1.9725,23.32],.15,.15,3.875,'cream','hood',24)
box('long_wall_stainless_low_toe',[86.594,.085,20.425],[.014,.10,3.25],'steel','wall')
box('front_return_stainless_low_toe',[86.594,.085,23.96],[.014,.10,1.06],'steel','hood')
# Wood bench modules: dark continuous backs, independent seat/base and underseat warm strip.
for i,(x,z,length,side) in enumerate([(77.15,15.14,2.0,'north'),(79.32,15.14,2.0,'north'),(81.49,15.14,2.0,'north'),(86.31,19.40,.95,'east'),(86.31,20.45,.95,'east'),(86.31,21.50,.95,'east'),(86.31,15.90,1.12,'east')]):
 if side=='north':
  box(f'bench_{i}_raised_wood_base',[x,.255,z+.10],[length,.44,.48],'wood','bench',True);box(f'bench_{i}_dark_seat',[x,.49,z+.13],[length,.06,.52],'bench','bench');box(f'bench_{i}_dark_continuous_back',[x,.77,z-.12],[length,.46,.075],'bench','bench');box(f'bench_{i}_underseat_light',[x,.13,z+.35],[length-.06,.022,.015],'warm','bench')
 else:
  box(f'bench_{i}_raised_wood_base',[x,.255,z],[.48,.44,length],'wood','bench',True);box(f'bench_{i}_dark_seat',[x-.03,.49,z],[.52,.06,length],'bench','bench');box(f'bench_{i}_dark_continuous_back',[x+.19,.77,z],[.075,.46,length],'bench','bench');box(f'bench_{i}_underseat_light',[x-.25,.13,z],[.015,.022,length-.06],'warm','bench')
# Existing frontiers carry the three actual square inserts; new long bench wall receives source-visible inset squares.
for i,z in enumerate([19.1,19.62,20.14,20.66,21.18,21.70]):box('long_wall_square_warm_inset_'+str(i),[86.592,1.36,z],[.009,.115,.115],'warm','bench')
# Current hood, visible cupboard fronts and dish rack behind the cooking opening.
box('visible_cookside_steel_worktop',[87.24,.89,22.65],[.64,.045,1.08],'steel','hood',True)
for x in [86.98,87.51]:
 for z in [22.17,23.12]:box(f'visible_worktable_leg_{x}_{z}',[x,.46,z],[.035,.83,.035],'steel','hood')
box('visible_worktable_lower_open_shelf',[87.24,.26,22.65],[.57,.035,1.01],'steel','hood')
# Hood panels follow visible folded surfaces; unobserved duct network is deliberately absent.
box('hood_visible_upper_steel_face',[87.57,3.12,22.59],[.035,.34,1.10],'steel','hood')
box('hood_visible_lower_steel_lip',[87.34,2.48,22.59],[.43,.075,1.10],'steel','hood')
v=[[87.55,2.95,22.04],[87.55,2.95,23.14],[87.13,2.52,23.14],[87.13,2.52,22.04]];add('hood_visible_sloping_canopy','steel',v,[0,2,1,0,3,2],'hood')
for i,z in enumerate([22.315,22.865]):box('visible_brown_upper_cupboard_front_'+str(i),[87.62,3.44,z],[.038,.28,.52],'wood','hood')
for z in [22.58,22.61]:box('upper_cupboard_small_dark_handle_'+str(z),[87.59,3.40,z],[.028,.065,.018],'black','hood')
for x in [87.03,87.42]:
 for z in [22.82,23.16]:box(f'open_dishrack_brown_post_{x}_{z}',[x,1.75,z],[.035,1.92,.035],'darkwood','hood')
for i,y in enumerate([.89,1.24,1.61,1.98,2.32]):box('visible_brown_dishrack_shelf_'+str(i),[87.23,y,22.99],[.44,.035,.39],'darkwood','hood')
for stack,z in enumerate([22.91,23.08]):
 for i in range(7):cyl(f'visible_white_plate_stack_{stack}_{i}',[87.16,1.65+i*.017,z],.078,.078,.012,'ceramic','hood',18)
# Source-visible rinsing apparatus inside the kitchen, separately qualified2024source.
box('washstation_visible_steel_workface',[89.08,.885,23.68],[1.28,.04,.55],'steel','wash',True)
box('washstation_front_lip',[89.08,.835,23.39],[1.28,.075,.025],'steel','wash')
for x in [88.49,89.67]:
 for z in [23.45,23.90]:box(f'washstation_visible_support_{x}_{z}',[x,.46,z],[.035,.83,.035],'steel','wash')
tube('washstation_visible_chrome_riser',[[88.97,.91,23.82],[88.97,1.49,23.82],[88.97,1.75,23.82]],.018,'steel','wash')
tube('washstation_spray_hose',[[88.97,1.7,23.82],[88.94,1.92,23.8],[88.80,2.05,23.76],[88.62,2.02,23.72],[88.53,1.85,23.68],[88.57,1.66,23.65]],.013,'black','wash')
tube('washstation_visible_spray_head',[[88.57,1.66,23.65],[88.57,1.51,23.65]],.033,'steel','wash')
for st,x in enumerate([89.30,89.51]):
 for i in range(8+st*2):cyl(f'visible_brass_bowl_rim_{st}_{i}',[x,.93+i*.027,23.52],.085,.085,.018,'brass','wash',20)
# Black circular pedestal table seen in the whole hall view.
cyl('source_black_round_table_top',[81.80,.755,18.90],.70,.70,.055,'black','room',48);cyl('source_black_round_table_stem',[81.80,.37,18.90],.14,.14,.71,'black','room',24);cyl('source_black_round_table_low_foot',[81.80,.06,18.90],.36,.36,.05,'black','room',32)
# Three-level dark cabinet by retained column neighborhood. Six rattan panels remain individually editable.
xc,zc=82.05,22.55
for x in [xc-.32,xc+.32]:box('cabinet_vertical_stile_'+str(x),[x,1.0,zc],[.045,1.93,.38],'darkwood','cabinet')
for y in [.07,.69,1.31,1.93]:box('cabinet_level_rail_'+str(y),[xc,y,zc],[.68,.045,.38],'darkwood','cabinet')
box('cabinet_minimum_rear_skin',[xc,1.,zc-.18],[.65,1.86,.02],'darkwood','cabinet')
for level in range(3):
 for side in [-1,1]:
  x=xc+side*.164;y=.39+level*.62;z=zc+.202;w=.292;h=.55
  combineboxes(f'cabinet_door_frame_{level}_{side}',[([x-w/2,y,z],[.025,h,.025]),([x+w/2,y,z],[.025,h,.025]),([x,y-h/2,z],[w,.025,.025]),([x,y+h/2,z],[w,.025,.025])],'darkwood','cabinet')
  woven=[]
  for k in range(13):woven.append(([x-w/2+.025+k*(w-.05)/12,y,z-.008],[.007,h-.05,.009]))
  for k in range(23):woven.append(([x,y-h/2+.025+k*(h-.05)/22,z-.003],[w-.05,.006,.007]))
  combineboxes(f'cabinet_independent_rattan_weave_{level}_{side}',woven,'wood','cabinet');cyl(f'cabinet_brass_latch_{level}_{side}',[x-side*.10,y,zc+.23],.022,.022,.025,'brass','cabinet',12)
# Reuse the source observed lattice storage at the aperture's public side; add photographed blue-white vessels only.
for i,z in enumerate([23.45,23.88]):
 cyl(f'visible_bluewhite_vessel_body_{i}',[86.20,1.98,z],.125,.125,.28,'ceramic','hood',24)
 cyl(f'visible_bluewhite_vessel_neck_{i}',[86.20,2.15,z],.065,.065,.06,'ceramic','hood',20)
 cyl(f'visible_bluewhite_vessel_lid_{i}',[86.20,2.19,z],.080,.080,.018,'ceramic','hood',20)
 for h in [1.90,2.04]:
  path=[[86.20+.126*math.cos(k*math.pi/12),h,z+.126*math.sin(k*math.pi/12)] for k in range(25)]
  tube(f'visible_bluewhite_vessel_blue_band_{i}_{h}',path,.009,'blue','hood',8)
# Ceiling cassettes and soft radial fan blades, fit to current3.91ceiling rather than an added structural ceiling.
for num,(x,z) in enumerate([(78.70,17.75),(81.0,20.15),(84.5,22.15)]):
 box(f'ceiling_cassette_{num}_white_frame',[x,3.845,z],[1.02,.10,1.02],'white','room');box(f'ceiling_cassette_{num}_dark_grille',[x,3.786,z],[.62,.016,.62],'vent','room')
 bars=[]
 for k in range(11):bars.append(([x-.29+k*.058,3.774,z],[.018,.014,.60]));bars.append(([x,3.773,z-.29+k*.058],[.60,.014,.018]))
 combineboxes(f'ceiling_cassette_{num}_individual_grille',bars,'white','room')
 for k,(dx,dz,sx,sz) in enumerate([(-.43,0,.11,.72),(.43,0,.11,.72),(0,-.43,.72,.11),(0,.43,.72,.11)]):box(f'ceiling_cassette_{num}_air_louver_{k}',[x+dx,3.774,z+dz],[sx,.022,sz],'white','room')
 cyl(f'ceiling_cassette_{num}_fan_hub',[x,3.70,z],.055,.055,.10,'white','room',16)
 for k in range(6):
  theta=k*math.pi/3;v=[]
  for rad,ang in [(.08,theta-.12),(.52,theta+.12),(.57,theta+.30),(.12,theta+.20)]:v.append([x+rad*math.cos(ang),3.665,z+rad*math.sin(ang)])
  add(f'ceiling_cassette_{num}_clear_fan_blade_{k}','clear',v,[0,1,2,0,2,3,2,1,0,3,2,0],'room')
for n,(x,z) in enumerate([(76.8,18.0),(79.0,18.0),(81.4,18.0),(84.9,18.0),(77.9,20.0),(80.1,20.0),(84.5,20.0),(78.8,23.0),(81.0,23.0),(85.1,23.0),(85.8,16.0),(90.0,19.6)]):
 cyl(f'photo_round_downlight_{n}_trim',[x,3.897,z],.095,.095,.015,'white','room',24);cyl(f'photo_round_downlight_{n}_diffuser',[x,3.886,z],.077,.077,.009,'warm','room',24)
# Shared-source furniture instances: exact retained source meshes/materials, new owned GameObjects only.
clones=[]
def table(name,x,z,scale=(.74,.74,.74),yaw=0):clones.append({'name':name,'prototype_id':1922815291,'q':[x,z],'y':.035,'scale':scale,'source':src['room'],'q_forward':[math.sin(yaw),math.cos(yaw)]})
def chair(name,x,z,face):clones.append({'name':name,'prototype_id':1461621253,'q':[x,z],'y':.035,'scale':[.8,.8,.8],'q_forward':face,'source':src['room']})
table('source_central_four_top',79.30,19.30)
for k,(x,z,f) in enumerate([(78.47,19.30,[1,0]),(80.13,19.30,[-1,0]),(79.30,18.54,[0,1]),(79.30,20.06,[0,-1])]):chair('source_central_four_top_chair_'+str(k),x,z,f)
for k,(x,z,f) in enumerate([(80.85,18.90,[1,0]),(82.75,18.90,[-1,0]),(81.80,19.85,[0,-1])]):chair('source_black_round_table_chair_'+str(k),x,z,f)
for i,x in enumerate([77.15,79.32,81.49]):table('source_rear_bench_two_top_'+str(i),x,16.02,[.47,.74,.54]);chair('source_rear_bench_public_chair_'+str(i),x,16.72,[0,-1])
for i,z in enumerate([19.4,20.45,21.50]):table('source_long_wall_bench_two_top_'+str(i),85.85,z,[.35,.74,.65]);chair('source_long_wall_public_chair_'+str(i),85.25,z,[1,0])
table('source_niche_bench_two_top',85.85,15.90,[.35,.74,.65]);chair('source_niche_bench_public_chair',85.25,15.90,[1,0])
for i,x in enumerate([79.0,81.1]):
 table('source_front_glazing_two_top_'+str(i),x,23.40,[.47,.74,.54]);chair('source_front_glazing_left_chair_'+str(i),x-.74,23.40,[1,0]);chair('source_front_glazing_right_chair_'+str(i),x+.74,23.40,[-1,0])
table('source_west_window_two_top',76.23,23.35,[.47,.74,.54],math.pi/2);chair('source_west_window_chair_0',76.23,22.61,[0,1]);chair('source_west_window_chair_1',76.23,24.09,[0,-1])
# Replace only old generic own-store dining pieces; preserve all user-deleted or inactive objects.
deactivate=[o for o in objs if o['active_hierarchy'] and o['name'].startswith('GukbapDining_') and ( '_table__' in o['name'] or '_chair' in o['name'])]
moves=[]
for ident,q,f in [(2089983115,[86.53,17.4],[-1,0]),(1367566275,[86.95,17.4],[-1,0]),(1931069275,[86.90,17.4],[-1,0]),(2144491061,[87.60,18.35],[-1,0]),(399383994,[86.20,23.65],[-1,0])]:moves.append({'target':D[ident],'q':q,'q_forward':f,'activate':True,'source':src['niche'] if ident!=399383994 else src['hood']})
# Baked q coordinates reflect handedness, so reverse every triangle once for outward world normals.
for p in parts:
 t=p['triangles'];p['triangles']=[q for k in range(0,len(t),3) for q in [t[k],t[k+2],t[k+1]]]
packet={'schema':'shop13.accepted-booth.photo-interior028.v1','status':'READY_DIRECT_APPLY_INSIDE_ACCEPTED_BOOTH_ONLY','source_reading':src,'user_authority':str(B/'USER-AUTHORITY.json'),'accepted_booth_contacts':{'west_shell_x':75.13,'east_shell_x':92.30,'front_shell_y_at_mainentry':24.55669,'north_current_wc_wall_y':14.74,'public_main_entry_q':[83.71686,24.55669],'hall_entry_q':[75.13158,16.60169],'shell_geometry_changes':False},'interior_partition':{'kitchen_side':'east side behind source-observed service/cook openings','frontier_inner_face_x':86.60,'room_extent_within_current_booth':[86.60,14.89,92.25,24.49],'field_measurement_claim':False,'historical58_84m2forced':False,'source_layout_method':'Use accepted booth entries/perimeter asfixed coordinates; photo-relative furniture and opening relationships only. No oldNativeAglobalremap or013replay.'},'materials':mats,'parts':parts,'clones':clones,'moves':moves,'deactivate_old_generic_dining':deactivate,'source_prototypes':{str(i):D[i] for i in [1922815291,1461621253]},'retain_photo_existing_table_and_user_deleted_seat':True,'QA_tests_probes_reopen_captures':False,'explicit_limits':['Photographic construction within useraccepted region, not surveyed asbuilt','Source2024visiblewashdetail separately qualified from2026hood/currentfridge photos','No unseen boilers, burner rows or concealed utility network fabricated','All fixedboothshell/native/floors/columns/otherowners/userdeletions remain unchanged']}
(B/'PHOTO-INTERIOR-CONSTRUCTION-PACKET-028.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':'))+'\n');print('PARTS',len(parts),'CLONES',len(clones),'MOVED_GROUPS',len(moves),'OLD_GENERIC_ROOTS',len(deactivate))
