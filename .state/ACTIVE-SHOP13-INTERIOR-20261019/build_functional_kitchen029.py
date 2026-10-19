"""New, explicitly inferred equipment. Never imports/replays the retired013 room."""
from pathlib import Path
import json, math
import numpy as np

B = Path('.state/ACTIVE-SHOP13-INTERIOR-20261019')
A, C = -.34290633335024717, -.9393696006089878
FLOOR = .035
assemblies = []
current = None

def world(p):
    x, y, z = p
    return [A*x+C*z, y, C*x-A*z]

def start(name, q, front=(0, 1), floor=FLOOR, collider=None, function=''):
    global current
    current = dict(name=name+'_MODEL_029', q=list(q), y=floor, front=list(front), function=function,
                   qualification='USER_AUTHORIZED_INFERENCE_NOT_SITE_INVENTORY', groups={}, collider=collider)
    assemblies.append(current)

def emit(name, mat, verts, triangles):
    key = name+'|'+mat
    g = current['groups'].setdefault(key, dict(name=name, material=mat, vertices=[], triangles=[]))
    n = len(g['vertices'])
    g['vertices'].extend(verts)
    g['triangles'].extend(n+i for i in triangles)

def quad(name, mat, v, normal):
    a = np.array(v, float)
    if np.dot(np.cross(a[1]-a[0], a[2]-a[0]), normal) < 0:
        a = a[[0, 3, 2, 1]]
    emit(name, mat, a.tolist(), [0, 1, 2, 0, 2, 3])

def box(name, mat, c, s):
    c, h = np.array(c), np.array(s)/2
    pts = [c+h*np.array(p) for p in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                                    (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    for f, normal in [((0,3,2,1),(0,0,-1)),((4,5,6,7),(0,0,1)),((0,1,5,4),(0,-1,0)),
                      ((3,7,6,2),(0,1,0)),((0,4,7,3),(-1,0,0)),((1,2,6,5),(1,0,0))]:
        quad(name, mat, [pts[i] for i in f], normal)

def lathe(name, mat, c, profile, n=32):
    verts, triangles = [], []
    for y,r in profile:
        for i in range(n):
            t = 2*math.pi*i/n
            verts.append([c[0]+r*math.cos(t), c[1]+y, c[2]+r*math.sin(t)])
    for j in range(len(profile)-1):
        for i in range(n):
            a, b = j*n+i, j*n+(i+1)%n
            triangles += [a,b+n,b,a,a+n,b+n]
    emit(name, mat, verts, triangles)

def cyl(name, mat, c, r, h, n=20):
    lathe(name, mat, c, [(-h/2,0),(-h/2,r),(h/2,r),(h/2,0)], n)

def tube(name, mat, path, r, n=10):
    pts = np.array(path, float)
    vs, ts = [], []
    for i,p in enumerate(pts):
        tangent = pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]
        tangent /= np.linalg.norm(tangent)
        ref = np.array([1.,0,0]) if abs(tangent[0]) < .9 else np.array([0.,0,1.])
        x = np.cross(tangent,ref); x /= np.linalg.norm(x); z = np.cross(tangent,x)
        vs += [(p+r*(math.cos(k*2*math.pi/n)*x+math.sin(k*2*math.pi/n)*z)).tolist() for k in range(n)]
    for j in range(len(pts)-1):
        for k in range(n):
            a,b=j*n+k,j*n+(k+1)%n
            ts += [a,b,b+n,a,b+n,a+n]
    emit(name, mat, vs, ts)

def knob(name, x, y, z, r=.027):
    tube(name, 'rubber', [[x,y,z-.012],[x,y,z+.015]], r, 20)
    box(name+'_index', 'white', [x,y+r*.62,z+.016], [.006,r*.55,.004])

def handle(name, x, y, z, length=.21, vertical=True):
    if vertical:
        path=[[x,y-length/2,z],[x,y-length/2,z+.035],[x,y+length/2,z+.035],[x,y+length/2,z]]
    else:
        path=[[x-length/2,y,z],[x-length/2,y,z+.035],[x+length/2,y,z+.035],[x+length/2,y,z]]
    tube(name, 'steel', path, .012, 10)

def feet(w, d, h=.15):
    for x in [-w/2+.065,w/2-.065]:
        for z in [-d/2+.065,d/2-.065]:
            cyl('adjustable_feet', 'steel', [x,h/2,z], .025, h)
            cyl('rubber_footpads','rubber',[x,.012,z],.032,.024)

def table(w, d, h, shelf=True):
    box('folded_top', 'steel', [0,h-.02,0], [w,.04,d])
    for x in [-w/2+.045,w/2-.045]:
        for z in [-d/2+.045,d/2-.045]:
            box('square_tube_legs', 'steel', [x,(h-.04)/2,z], [.035,h-.04,.035])
            cyl('adjustable_ends','rubber',[x,.018,z],.021,.036,12)
    if shelf: box('lower_open_shelf','steel',[0,.22,0],[w-.06,.025,d-.06])
    for z in [-d/2+.03,d/2-.03]:
        box('edge_fold', 'steel', [0,h-.075,z], [w-.03,.07,.025])

def rectring(w,d,r,y,center=(0,0),steps=5):
    pts=[]
    for x,z,a in [(w/2-r,d/2-r,0),(-w/2+r,d/2-r,90),(-w/2+r,-d/2+r,180),(w/2-r,-d/2+r,270)]:
        for k in range(steps):
            t=math.radians(a+90*k/(steps-1))
            pts.append([center[0]+x+r*math.cos(t),y,center[1]+z+r*math.sin(t)])
    return pts

def basin(name, center, w,d,top,depth, mat='steel'):
    outer=rectring(w,d,0,top,center)
    lip=rectring(w-.045,d-.045,.035,top-.009,center)
    bottom=rectring(w-.12,d-.12,.035,top-depth,center)
    for i in range(len(outer)):
        j=(i+1)%len(outer)
        quad(name+'_rolled_rim',mat,[outer[i],outer[j],lip[j],lip[i]],[0,1,0])
        c=(np.array(lip[i])+np.array(lip[j]))/2
        quad(name+'_open_basin',mat,[lip[i],lip[j],bottom[j],bottom[i]],
             [center[0]-c[0],.12,center[1]-c[2]])
    vs=[[center[0],top-depth,center[1]]]+bottom
    emit(name+'_basin_bottom',mat,vs,[a for k in range(len(bottom)) for a in [0,(k+1)%len(bottom)+1,k+1]])
    cyl(name+'_drain','rubber',[center[0],top-depth+.001,center[1]],.024,.003,16)
    cyl(name+'_drain_grate','steel',[center[0],top-depth+.004,center[1]],.014,.003,12)

def open_top(name,w,d,h,center,holew,holed):
    x,z=center
    lx,rx=x-holew/2,x+holew/2
    bz,fz=z-holed/2,z+holed/2
    for c,s in [(((-w/2+lx)/2,h-.01,0),(lx+w/2,.02,d)),
                (((rx+w/2)/2,h-.01,0),(w/2-rx,.02,d)),
                ((x,h-.01,(-d/2+bz)/2),(holew,.02,bz+d/2)),
                ((x,h-.01,(fz+d/2)/2),(holew,.02,d/2-fz))]:
        box(name,'steel',c,s)

def bowl(name,x,y,z,r=.10,mat='ceramic'):
    lathe(name,mat,[x,y,z],[(0,0),(0,r*.52),(.018,r*.62),(.07,r),(.077,r),(.077,r-.012),(.025,r*.50),(.023,0)],24)

def plate_stack(name,x,y,z,n=8,r=.12):
    for i in range(n):
        lathe(name,'ceramic',[x,y+i*.014,z],[(0,0),(0,r*.56),(.006,r),(.012,r),(.014,r*.95),(.007,r*.50),(.007,0)],24)

def foodbox(name,c,s):
    box(name,'foodplastic',c,s)
    box(name+'_lid','white',[c[0],c[1]+s[1]/2+.008,c[2]],[s[0]+.02,.016,s[2]+.02])
    box(name+'_label_holder','label',[c[0],c[1]+.01,c[2]+s[2]/2+.001],[s[0]*.44,.048,.003])

def upright(name,q,w,doors=4):
    start(name,q,collider=[w,1.90,.80],function='rear ingredient cold storage')
    feet(w,.8)
    box('insulated_case','steel',[0,1.01,0],[w,1.72,.78])
    box('compressor_recess','darksteel',[0,.285,.404],[w-.045,.23,.025])
    for k in range(9): box('lower_vent_louvres','steel',[0,.19+k*.023,.422],[w-.07,.010,.019])
    columns=2 if doors==4 else 1
    dw=(w-.05)/columns
    for row in range(2):
        for col in range(columns):
            x=(col-(columns-1)/2)*dw
            y=.776+row*.665
            box('door_gaskets','rubber',[x,y,.399],[dw-.009,.642,.016])
            box('separate_door_'+str(row)+'_'+str(col),'steel',[x,y,.420],[dw-.025,.624,.030])
            hx=x+(dw/2-.058)*(1 if col==0 else -1)
            handle('door_handles',hx,y+.01,.440,.25)
            for dy in [-.22,.22]:box('door_hinge_blocks','steel',[x-dw/2+.015,y+dy,.438],[.018,.05,.02])
    box('upper_header','steel',[0,1.83,.409],[w-.035,.10,.025])
    box('display_bezel','rubber',[w/2-.12,1.836,.427],[.15,.055,.012])
    box('temperature_display','lcd',[w/2-.12,1.84,.434],[.10,.028,.003])

upright('rear_four_door_refrigerator',(89.50,15.50),1.26,4)
upright('rear_two_door_freezer',(87.85,15.50),.65,2)

start('north_east_food_storage_rack',(91.78,16.65),(-1,0),collider=[1.5,1.8,.65],function='dry food storage above floor')
for x in [-.72,.72]:
    for z in [-.28,.28]:cyl('tubular_posts','steel',[x,.9,z],.019,1.8,16)
for y in [.20,.64,1.08,1.52]:
    for z in [-.3,.3]:box('shelf_perimeter','steel',[0,y,z],[1.5,.025,.025])
    for x in [-.737,.737]:box('shelf_perimeter','steel',[x,y,0],[.025,.025,.6])
    for k in range(24):box('wire_shelf_grid','steel',[-.70+k*1.4/23,y,0],[.008,.008,.6])
for x in [-.44,0,.44]:
    foodbox('lower_lidded_ingredient_bins',[x,.39,0],[.34,.35,.43])
    foodbox('middle_lidded_food_boxes',[x,.79,0],[.34,.25,.43])
    foodbox('upper_food_boxes',[x,1.21,0],[.34,.22,.43])
for x in [-.54,-.32,.26,.48]:
    lathe('cooking_oil_and_sauce_bottles','amber',[x,1.535,0],[(0,0),(0,.055),(.22,.055),(.26,.024),(.31,.024),(.31,0)],16)
    cyl('bottle_caps','rubber',[x,1.85,0],.026,.025,16)

start('central_refrigerated_preparation_bench',(89.15,18.80),(-1,0),collider=[1.5,.85,.7],function='cold prep opposite hot line')
feet(1.5,.7,.14)
box('insulated_bench_body','steel',[0,.465,0],[1.5,.65,.68])
box('heavy_worktop','steel',[0,.827,0],[1.53,.046,.73])
for i,x in enumerate([-.25,.32]):
    box('door_gaskets','rubber',[x,.465,.347],[.55,.55,.014])
    box('prep_cold_door_'+str(i),'steel',[x,.465,.367],[.532,.53,.025])
    handle('recessed_horizontal_pull',x,.68,.383,.35,False)
box('compressor_dark_vent','darksteel',[-.63,.445,.354],[.18,.54,.015])
for y in np.linspace(.23,.67,13):box('compressor_vent_blades','steel',[-.63,float(y),.371],[.17,.012,.018])
box('prep_green_board','green',[-.35,.862,0],[.38,.022,.32])
box('prep_white_board','white',[.17,.862,0],[.35,.022,.32])
box('knife_blade_on_board','steel',[.15,.879,.01],[.17,.003,.035])
box('knife_handle_on_board','rubber',[.02,.885,.01],[.10,.015,.035])
for i,x in enumerate([-.42,-.10,.22]):
    basin('ingredient_pan_'+str(i),(x,-.20),.25,.17,.91,.08)
    box('lidded_food_pan_'+str(i),'steel',[x,.917,-.20],[.235,.012,.155])

start('central_preparation_extension',(89.15,17.55),(-1,0),collider=[.9,.85,.7],function='receiving and preparation')
table(.9,.7,.85)
lathe('mixing_bowl','steel',[-.16,.853,0],[(0,0),(0,.06),(.025,.08),(.16,.19),(.17,.19),(.17,.181),(.025,.065),(.02,0)],28)
box('receiving_tray','steel',[.25,.87,0],[.30,.025,.43])
foodbox('lower_food_container',[0,.37,0],[.62,.25,.46])

start('entry_handwash_sink',(87.03,19.30),(1,0),collider=[.48,.87,.45],function='handwash beside staff access, outside doorway')
open_top('sink_top',.48,.45,.85,(0,0),.40,.34)
basin('handwash',(0,0),.40,.34,.85,.19)
for x in [-.2,.2]:box('wall_console_support','steel',[x,.57,-.18],[.028,.45,.028])
tube('gooseneck_faucet','steel',[[0,.85,-.19],[0,1.10,-.19],[0,1.16,-.15],[0,1.16,-.02],[0,1.09,.01]],.012)
box('soap_dispenser_body','white',[.15,1.30,-.25],[.105,.20,.07])
box('soap_window','lcd',[.15,1.31,-.211],[.055,.09,.006])
box('paper_towel_dispenser','white',[-.15,1.52,-.24],[.20,.22,.08])
box('paper_towel','label',[-.15,1.36,-.23],[.12,.12,.004])

start('rice_service_bench',(87.10,20.70),(1,0),collider=[1.35,.65,.60],function='rice holding next to plating passage')
table(1.35,.6,.62)
foodbox('covered_rice_bin',[-.32,.37,0],[.46,.26,.44])
foodbox('spare_rice_bin',[.28,.37,0],[.44,.26,.44])
for i,z in enumerate([20.35,21.02]):
    start('commercial_rice_cooker_'+str(i),(87.10,z),(1,0),floor=FLOOR+.62,function='rice cooking and holding')
    lathe('double_wall_body','steel',[0,0,0],[(0,0),(0,.17),(.025,.21),(.27,.225),(.30,.215),(.30,0)],32)
    lathe('rounded_lid','steel',[0,.30,0],[(0,0),(0,.218),(.022,.20),(.055,.15),(.07,.04),(.07,0)],32)
    box('lid_handle','rubber',[0,.407,0],[.16,.045,.05])
    for x in [-.245,.245]:box('side_insulated_handles','rubber',[x,.22,0],[.08,.045,.10])
    box('control_panel','rubber',[0,.12,.219],[.16,.08,.025])
    box('control_lcd','lcd',[-.035,.13,.234],[.055,.027,.003])
    knob('rice_switch',.047,.12,.235,.018)

start('low_broth_double_range',(91.70,19.10),(-1,0),collider=[1.35,1.12,.90],function='main gukbap broth production')
feet(1.35,.9,.13)
box('heavy_range_case','steel',[0,.275,0],[1.35,.31,.90])
box('range_castiron_top','darksteel',[0,.456,0],[1.32,.055,.87])
for i,x in enumerate([-.335,.335]):
    cyl('castiron_burner_'+str(i),'rubber',[x,.494,0],.17,.03,24)
    lathe('burner_rings','darksteel',[x,.516,0],[(0,.115),(0,.16),(.014,.16),(.014,.115),(0,.115)],24)
    for t in [0,math.pi/2,math.pi,3*math.pi/2]:
        tube('pot_supports','darksteel',[[x+.09*math.cos(t),.52,.09*math.sin(t)],[x+.25*math.cos(t),.52,.25*math.sin(t)]],.017)
    knob('broth_control_knobs',x,.31,.467,.035)
for i,z in enumerate([18.765,19.435]):
    start('broth_stockpot_'+str(i),(91.70,z),(-1,0),floor=FLOOR+.54,function='large stockpot for broth; generic inferred apparatus')
    lathe('open_stainless_stockpot','steel',[0,0,0],[(0,0),(0,.245),(.025,.276),(.50,.276),(.515,.292),(.530,.292),(.530,.266),(.035,.258),(.027,0)],40)
    for side in [-1,1]:
        tube('welded_pot_handles','steel',[[0,.35,side*.273],[0,.36,side*.355],[0,.46,side*.355],[0,.47,side*.273]],.017)
    if i==0:
        lathe('domed_stockpot_lid','steel',[0,.535,0],[(0,0),(0,.292),(.018,.285),(.05,.20),(.065,.065),(.065,0)],36)
        handle('lid_bridge',0,.62,0,.14,False)
    else:
        cyl('broth_surface','broth',[0,.445,0],.258,.003,36)
        tube('stock_ladle','steel',[[.11,.35,-.05],[.11,.66,-.05],[.11,.78,-.09]],.009)
        bowl('ladle_cup',.11,.32,-.05,.06,'steel')

start('secondary_two_burner_range',(91.73,20.30),(-1,0),collider=[.90,.95,.70],function='cooking side dishes and smaller pots')
feet(.9,.7,.14)
box('range_cabinet','steel',[0,.455,0],[.9,.63,.7])
box('open_lower_recess','darksteel',[0,.32,.359],[.78,.22,.012])
box('range_top','darksteel',[0,.796,0],[.92,.045,.72])
for x in [-.23,.23]:
    cyl('burner_cap','rubber',[x,.835,0],.085,.025,24)
    for t in [0,math.pi/2,math.pi,3*math.pi/2]:
        tube('cast_grate','darksteel',[[x+.04*math.cos(t),.86,.04*math.sin(t)],[x+.16*math.cos(t),.86,.16*math.sin(t)]],.011)
    knob('range_front_knobs',x,.66,.37)
lathe('small_sauce_pot','steel',[-.23,.87,0],[(0,0),(0,.12),(.18,.12),(.19,.127),(.19,.112),(.015,.108),(.015,0)],28)
tube('sauce_pot_insulated_handle','rubber',[[-.23,.99,.12],[-.23,1.0,.31]],.023)
bowl('black_cooking_pot',.23,.87,0,.12,'black')

start('freestanding_hotline_splashguard',(92.155,19.55),(-1,0),function='equipment-mounted splashguard inside glazing; shell untouched')
box('steel_splash_sheet','steel',[0,1.23,0],[2.45,.84,.018])
for x in [-1.18,1.18]:box('independent_support_uprights','steel',[x,.80,-.015],[.025,1.55,.025])

start('hotline_canopy_and_short_plenum',(91.60,19.55),(-1,0),function='inferred local canopy ending below existing ceiling; no roof or exterior penetration')
w,d=2.50,1.18
lower=[[-w/2,2.12,-d/2],[w/2,2.12,-d/2],[w/2,2.12,d/2],[-w/2,2.12,d/2]]
upper=[[-w/2+.12,2.56,-d/2+.12],[w/2-.12,2.56,-d/2+.12],[w/2-.12,2.56,d/2-.12],[-w/2+.12,2.56,d/2-.12]]
for i,norm in enumerate([(0,0,-1),(1,0,0),(0,0,1),(-1,0,0)]):
    j=(i+1)%4
    quad('folded_canopy_faces','steel',[lower[i],lower[j],upper[j],upper[i]],norm)
quad('canopy_top','steel',upper,[0,1,0])
box('dark_filter_recess','darksteel',[0,2.135,0],[2.35,.025,1.03])
for i in range(6):
    x=-1.0+i*.40
    for z in [-.46,.46]:box('individual_filter_frames','steel',[x,2.105,z],[.37,.035,.025])
    for u in [x-.18,x+.18]:box('individual_filter_frames','steel',[u,2.105,0],[.025,.035,.94])
    for k in range(10):
        z=-.42+k*.093
        quad('angled_baffle_blades','steel',[[x-.165,2.10,z],[x+.165,2.10,z],[x+.165,2.15,z+.065],[x-.165,2.15,z+.065]],[0,-1,0])
for x in [-.81,.81]:box('hood_work_lights','warm',[x,2.09,.44],[.25,.018,.035])
box('rectangular_short_plenum','steel',[0,3.16,-.16],[1.15,1.20,.42])
for y in [2.62,3.18,3.72]:
    box('duct_fold_seams','darksteel',[0,y,.057],[1.16,.018,.016])
for x in [-1.05,1.05]:
    tube('ceiling_hangers','steel',[[x,2.50,-.34],[x,3.80,-.34]],.012)

start('electric_ttukbaegi_finishing_surface',(87.33,22.45),(-1,0),floor=.913,function='four-position electric finishing surface under retained photo hood')
box('stainless_countertop_appliance','steel',[0,.065,0],[.52,.13,.40])
box('dark_induction_surface','black',[0,.138,0],[.50,.016,.38])
for u in [-.135,.135]:
    for v in [-.095,.095]:
        lathe('induction_ring_marks','darksteel',[u,.148,v],[(0,.071),(0,.080),(.001,.080),(.001,.071),(0,.071)],24)
        lathe('open_ttukbaegi_bowls','black',[u,.15,v],[(0,0),(0,.055),(.018,.065),(.095,.085),(.107,.088),(.107,.076),(.026,.055),(.022,0)],28)
        cyl('ttukbaegi_broth','broth',[u,.23,v],.071,.002,24)
for u in [-.19,-.065,.065,.19]:knob('finishing_controls',u,.07,.214,.017)

start('plating_support_bench',(88.10,22.60),(-1,0),collider=[1.10,.87,.65],function='clean bowls and plating beside retained pass')
table(1.10,.65,.87)
for u in [-.35,-.08,.20]:plate_stack('clean_soup_saucers',u,.875,-.06,7,.115)
for u in [-.32,.02,.35]:
    for k in range(3):bowl('clean_soup_bowls',u,.91+k*.028,.14,.09,'ceramic')
foodbox('lower_service_bowl_storage',[0,.38,0],[.78,.28,.44])

# Replace ONLY the prior own solid workface. The source spray, lip, legs and bowls remain.
start('existing_wash_top_open_presoak_basin',(89.08,23.68),(0,-1),function='same-footprint replacement of own028 solid worktop by open sink')
open_top('same_footprint_sink_rim',1.28,.55,.87,(.22,0),.54,.42)
basin('deep_presoak_sink',(.22,0),.54,.42,.87,.23)
for x in np.linspace(-.58,-.12,11):box('drainboard_embossed_ribs','steel',[float(x),.873,0],[.007,.005,.36])
tube('visible_sink_trap','steel',[[.22,.635,0],[.22,.46,0],[.30,.40,0],[.39,.43,-.06],[.39,.57,-.18]],.022)

start('hood_dishwasher',(90.12,23.68),(0,-1),collider=[.650,1.405,.720],function='presoak then hood washer then clean drying landing')
feet(.65,.72,.14)
box('washer_lower_body','steel',[0,.475,0],[.65,.67,.72])
box('washer_chamber_dark_seal','rubber',[0,.97,.0],[.625,.35,.695])
box('washer_hood_front','steel',[0,1.145,.365],[.65,.45,.025])
box('washer_hood_back','steel',[0,1.145,-.365],[.65,.45,.025])
for x in [-.317,.317]:box('washer_hood_sides','steel',[x,1.145,0],[.025,.45,.72])
box('washer_hood_lid','steel',[0,1.39,0],[.675,.03,.745])
box('washer_control_bar','darksteel',[0,.73,.375],[.57,.065,.025])
box('wash_cycle_display','lcd',[-.13,.735,.389],[.12,.03,.003])
for x in [.09,.19]:knob('wash_cycle_buttons',x,.735,.393,.016)
tube('hood_lifting_handle','steel',[[-.32,1.0,0],[-.38,.91,.25],[-.38,.91,.47],[.38,.91,.47],[.38,.91,.25],[.32,1.0,0]],.018)
box('wash_rack_front_edge','rackplastic',[0,.867,.363],[.55,.04,.025])
for x in [-.25,-.15,-.05,.05,.15,.25]:box('wash_rack_grid','rackplastic',[x,.858,0],[.008,.012,.55])

start('clean_dish_drying_landing',(91.23,23.68),(0,-1),collider=[1.40,.87,.65],function='clean exit side of washer; drying and clean storage')
table(1.40,.65,.87)
for x in [-.65,.65]:
    for z in [-.27,.27]:box('upper_drying_rack_posts','steel',[x,1.26,z],[.022,.78,.022])
for y in [1.19,1.62]:
    for z in [-.27,.27]:box('drying_rack_frame','steel',[0,y,z],[1.35,.025,.025])
    for x in np.linspace(-.61,.61,26):box('open_drying_grid','steel',[float(x),y,0],[.007,.007,.54])
for x in [-.46,-.18,.10,.38]:plate_stack('lower_clean_plate_stacks',x,.25,0,10,.105)
for x in [-.42,-.14,.14,.42]:
    for k in range(3):bowl('drying_bowls',x,1.20+k*.03,0,.095)
for x in [-.5,-.3,-.1,.1,.3,.5]:
    lathe('upper_inverted_cups','ceramic',[x,1.63,0],[(0,0),(0,.043),(.09,.034),(.10,.030),(.10,0)],18)

start('dish_return_trolley',(88.00,23.75),(0,-1),collider=[.50,.78,.48],function='dirty dish return at wash entrance, separate from clean landing')
for y in [.16,.47,.76]:
    box('dish_trolley_trays','steel',[0,y,0],[.50,.025,.48])
    for z in [-.23,.23]:box('tray_upstands','steel',[0,y+.035,z],[.5,.055,.012])
for x in [-.22,.22]:
    for z in [-.21,.21]:
        tube('trolley_posts','steel',[[x,.10,z],[x,.80,z]],.013)
        cyl('caster_wheels','rubber',[x,.065,z],.038,.045,16)
foodbox('dish_return_tub',[0,.85,0],[.42,.15,.38])

start('wash_side_pedal_bin',(87.12,24.03),(0,-1),collider=[.34,.56,.37],function='wash-side waste away from clean prep')
box('bin_body','darksteel',[0,.27,0],[.34,.52,.37])
box('bin_lid','steel',[0,.545,0],[.355,.035,.385])
box('pedal','rubber',[0,.045,.224],[.17,.02,.09])

start('surface_drain_grille_hotline',(90.34,19.30),(-1,0),function='surface-only inferred drain representation; existing floor not cut')
box('drain_bezel','darksteel',[0,.005,0],[1.6,.010,.16])
for x in np.linspace(-.76,.76,38):box('grille_bars','steel',[float(x),.012,0],[.016,.012,.145])

for i,(x,z) in enumerate([(89.2,17.65),(89.45,21.7)]):
    start('kitchen_ceiling_work_light_'+str(i),(x,z),function='interior ceiling fitting only')
    box('linear_light_case','white',[0,3.813,0],[1.15,.055,.18])
    box('linear_light_diffuser','warm',[0,3.78,0],[1.08,.012,.13])

materials={
 'steel':{'reuse':'Assets/ChooGuard/Art/StationInterior/Shop13AcceptedBoothPhotoInterior20261019/Materials/S13Photo028_steel.mat'},
 'black':{'reuse':'Assets/ChooGuard/Art/StationInterior/Shop13AcceptedBoothPhotoInterior20261019/Materials/S13Photo028_black.mat'},
 'white':{'reuse':'Assets/ChooGuard/Art/StationInterior/Shop13AcceptedBoothPhotoInterior20261019/Materials/S13Photo028_white.mat'},
 'ceramic':{'reuse':'Assets/ChooGuard/Art/StationInterior/Shop13AcceptedBoothPhotoInterior20261019/Materials/S13Photo028_ceramic.mat'},
 'warm':{'reuse':'Assets/ChooGuard/Art/StationInterior/Shop13AcceptedBoothPhotoInterior20261019/Materials/S13Photo028_warm.mat'},
 'darksteel':{'color':'#4E585B','metallic':.72,'smoothness':.42},
 'rubber':{'color':'#222625','metallic':.04,'smoothness':.22},
 'lcd':{'color':'#468E96','metallic':.05,'smoothness':.45},
 'foodplastic':{'color':'#DDD8BF','metallic':0,'smoothness':.28},
 'label':{'color':'#EEEDE4','metallic':0,'smoothness':.18},
 'rackplastic':{'color':'#819CA6','metallic':0,'smoothness':.34},
 'amber':{'color':'#9E692C','metallic':0,'smoothness':.52},
 'green':{'color':'#4A7056','metallic':0,'smoothness':.18},
 'broth':{'color':'#C8B687','metallic':0,'smoothness':.38},
}

for ass in assemblies:
    fx,fz=ass['front'];rx,rz=fz,-fx
    converted=[]
    for g in ass['groups'].values():
        # Baked relative to a separately editable appliance root; no reflected TRS.
        g['vertices']=[world([rx*u+fx*w,v,rz*u+fz*w]) for u,v,w in g['vertices']]
        ts=g['triangles'];g['triangles']=[t for k in range(0,len(ts),3) for t in [ts[k],ts[k+2],ts[k+1]]]
        converted.append(g)
    ass['groups']=converted

old=json.loads((B/'APPLIED-PHOTO-INTERIOR-028.json').read_text())
top=next(x for x in old['created_objects'] if x['path'].endswith('/washstation_visible_steel_workface'))
packet=dict(schema='shop13.user-authorized-functional-kitchen029.v1',status='READY_USER_AUTHORIZED_INFERRED_KITCHEN_ONLY',
            user_authority=str(B/'USER-AUTHORITY-KITCHEN-029.json'),decisions=str(B/'KITCHEN-LAYOUT-DECISIONS-029.json'),
            materials=materials,assemblies=assemblies,deactivate_solid_wash_top=top,
            base028_root_id=old['root_id'],base028_root_path=old['root_path'],
            no_original_asset_or_shell_change=True,no_agent_QA_tests_probes_reopen_captures=True)
(B/'FUNCTIONAL-KITCHEN-CONSTRUCTION-PACKET-029.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':'))+'\n')
print(json.dumps({'assemblies':len(assemblies),'editable_mesh_groups':sum(len(a['groups']) for a in assemblies),
                  'vertices':sum(len(g['vertices']) for a in assemblies for g in a['groups']),
                  'inference_authorized':True,'scene_changes_yet':False}))
