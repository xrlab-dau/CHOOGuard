"""Reference-informed synthetic Blender meshes. Run Blender 4.5 LTS in background.
Public-reference observations: docs/art/public-station-references.md.
No image inputs, downloads, bake, render, or surveyed facility geometry. SI metres.
"""
import bpy, bmesh, math, json, hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/CHOOguardArt/Blender'
SOURCE=ROOT/'foundation/art/station-kit.blend'
PALETTE={'Floor':(.62,.63,.60,1),'Wall':(.72,.73,.70,1),'Metal':(.10,.13,.15,1),'Blue':(.035,.16,.29,1),'Red':(.68,.10,.08,1),'Yellow':(.88,.64,.15,1),'Green':(.10,.48,.30,1),'Screen':(.035,.14,.18,1),'White':(.89,.92,.93,1),'Orange':(1,.36,.05,1),
         'Stainless':(.62,.67,.70,1),'Stone':(.70,.70,.65,1),'Glass':(.64,.80,.83,.26),
         'Diffuser':(.91,.96,1,1),'Roof':(.88,.90,.88,1),'Rubber':(.035,.045,.05,1)}
SURFACES={'Metal':(0,.65),'Stainless':(.85,.28),'Stone':(.02,.58),
          'Glass':(0,.12),'Roof':(.10,.38),'Rubber':(0,.88),'Screen':(.05,.22)}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for block in list(bpy.data.materials): bpy.data.materials.remove(block)
M={}
for name,col in PALETTE.items():
    m=bpy.data.materials.new(name);m.diffuse_color=col;m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=col
    metallic,roughness=SURFACES.get(name,(0,.52))
    bs.inputs['Metallic'].default_value=metallic
    bs.inputs['Roughness'].default_value=roughness
    if name=='Glass':
        bs.inputs['Transmission Weight'].default_value=1
        bs.inputs['IOR'].default_value=1.45
        bs.inputs['Alpha'].default_value=col[3]
    if name=='Diffuser':
        bs.inputs['Emission Color'].default_value=col
        bs.inputs['Emission Strength'].default_value=1.4
    M[name]=m
assets=[]; parts=[]; group='Static'
def v(p): return (p[0],-p[2],p[1])
def finish(o,name,mat):
    o.name=group+'__'+name;o.data.materials.append(M[mat]);parts.append(o)
    return o
def box(name,p,s,mat='Metal',bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=v(p));o=bpy.context.object;o.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Manufactured edge radii','BEVEL');mod.width=min(bevel,min(s)*.22);mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
        # Main manufactured planes remain flat; only the rounded shoulder is smooth.
        for face in o.data.polygons:face.use_smooth=max(abs(n) for n in face.normal)<.9999
    return finish(o,name,mat)
def ellipsoid(name,p,s,mat='Metal'):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=v(p));o=bpy.context.object;o.scale=(s[0]/2,s[2]/2,s[1]/2)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for face in o.data.polygons: face.use_smooth=True
    return finish(o,name,mat)
def pipe(name,a,b,r,mat='Metal',vertices=16):
    a,b=Vector(v(a)),Vector(v(b));delta=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=delta.length,location=(a+b)/2)
    o=bpy.context.object;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    mod=o.modifiers.new('Edge round','BEVEL');mod.width=min(.009,r*.22);mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    for f in o.data.polygons: f.use_smooth=len(f.vertices)==4
    return finish(o,name,mat)
def curve(name,points,r,mat='Metal'):
    bpy.ops.object.select_all(action='DESELECT')
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.resolution_u=3;data.bevel_depth=r;data.bevel_resolution=2
    spline=data.splines.new('POLY');spline.points.add(len(points)-1)
    for dst,src in zip(spline.points,points): dst.co=(*v(src),1)
    o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.convert(target='MESH');o=bpy.context.object
    for face in o.data.polygons:face.use_smooth=True
    return finish(o,name,mat)
def unit_face_uv(o):
    """Each unit cube face uses a complete tile, not Blender's default cross atlas.

    The upward face is Unity X/Z mapped to U/V: Blender X and minus Blender Y.
    Keeping FloorModule unbeveled makes both its metre bounds and UV span exact.
    """
    mesh=o.data;uv=mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    for face in mesh.polygons:
        axis=max(range(3),key=lambda i:abs(face.normal[i]))
        for loop_index in face.loop_indices:
            point=mesh.vertices[mesh.loops[loop_index].vertex_index].co
            u,w=(point.x,-point.y) if axis==2 else (-point.y,point.z) if axis==0 else (point.x,point.z)
            uv.data[loop_index].uv=(u+.5,w+.5)
def perforated_sheet(name,p,s,plane='xy',columns=7,rows=4):
    """Punched rectangular cells with actual openings, without booleans or subdivision.

    Closed cell frames share coplanar boundaries. Internal faces stay inside the sheet;
    each cell has a consistent closed surface and a through-hole, at bounded cost.
    """
    width=s[0];height=s[1] if plane=='xy' else s[2]
    depth=s[2] if plane=='xy' else s[1]
    vertices=[];faces=[]
    for row in range(rows):
        for col in range(columns):
            u0=-width/2+col*width/columns;u1=u0+width/columns
            w0=-height/2+row*height/rows;w1=w0+height/rows
            cu=(u0+u1)/2;cw=(w0+w1)/2
            hu=min(.021,(u1-u0)*.22);hw=min(.030,(w1-w0)*.22)
            outer=[(u0,w0),(u1,w0),(u1,w1),(u0,w1)]
            inner=[(cu-hu,cw-hw),(cu+hu,cw-hw),(cu+hu,cw+hw),(cu-hu,cw+hw)]
            base=len(vertices)
            for n in (-depth/2,depth/2):
                for u,w in outer+inner:
                    point=(p[0]+u,p[1]+w,p[2]+n) if plane=='xy' else (p[0]+u,p[1]+n,p[2]+w)
                    vertices.append(v(point))
            for i in range(4):
                j=(i+1)%4
                faces.extend([(base+i,base+j,base+4+j,base+4+i),
                              (base+8+i,base+12+i,base+12+j,base+8+j),
                              (base+i,base+8+i,base+8+j,base+j),
                              (base+4+i,base+4+j,base+12+j,base+12+i)])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    return finish(o,name,'Stainless')
def frame(p,w,h,mat='Metal'):
    x,y,z=p
    for yy in (-h/2,h/2):box('BezelRail',(x,y+yy,z),(w,.035,.04),mat)
    for xx in (-w/2,w/2):box('BezelRail',(x+xx,y,z),(.035,h,.04),mat)
def bolts(p,w,h):
    x,y,z=p
    for dx in (-w/2,w/2):
        for dy in (-h/2,h/2):
            pipe('RecessedFastener',(x+dx,y+dy,z),(x+dx,y+dy,z-.01),.014,'Stainless',12)
            box('ScrewSlot',(x+dx,y+dy,z-.012),(.014,.003,.002),'Metal',0)
def start(name):
    global parts,group
    parts=[];group='Static';bpy.ops.object.select_all(action='DESELECT');return name
def save(name):
    global group
    # Merge by material and articulation group: reusable meshes with bounded draw calls.
    objects=[]
    buckets={}
    for o in parts: buckets.setdefault((o.name.split('__')[0],o.data.materials[0].name),[]).append(o)
    for key,matching in sorted(buckets.items()):
        bpy.ops.object.select_all(action='DESELECT')
        for o in matching:o.select_set(True)
        bpy.context.view_layer.objects.active=matching[0]
        if len(matching)>1:bpy.ops.object.join()
        o=bpy.context.object
        o.name=key[0]+'_'+key[1];bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');objects.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    OUT.mkdir(parents=True,exist_ok=True)
    points=[o.matrix_world@vertex.co for o in objects for vertex in o.data.vertices]
    unity_points=[(p.x,p.z,-p.y) for p in points]
    minimum=[min(p[i] for p in unity_points) for i in range(3)]
    maximum=[max(p[i] for p in unity_points) for i in range(3)]
    tri=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
    if sum(a['triangles'] for a in assets)+tri>100000:
        raise RuntimeError('Source triangle budget exceeded by '+name)
    # The two modules are copied as bare meshes by the Unity builder.
    limits={'WallModule':((-0.5,-0.5,-0.5),(.5,.5,.5)),
            'FloorModule':((-0.5,-0.5,-0.5),(.5,.5,.5)),
            'RoofTruss':((-7.5,0,-.1),(7.5,1.1,.1)),
            'ClerestoryBay':((-.5,-.5,-.06),(.5,.5,.06)),
            'DepartureBoard':((-1.6,-.5,-.075),(1.6,.5,.075)),
            'InformationIsland':((-.85,0,-.35),(.85,1.1,.35))}
    if name in limits:
        lower,upper=limits[name]
        if any(minimum[i]<lower[i]-.0001 or maximum[i]>upper[i]+.0001 for i in range(3)):
            raise RuntimeError('Authored envelope exceeded: '+name+' '+str((minimum,maximum)))
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='STRIP')
    collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
    for o in objects:
        for c in list(o.users_collection):c.objects.unlink(o)
        collection.objects.link(o)
    assets.append({'id':name,'triangles':tri,'meshParts':len(objects),
                   'boundsUnity':{'min':[round(x,6) for x in minimum],'max':[round(x,6) for x in maximum]},
                   'file':f'Assets/CHOOguardArt/Blender/{name}.fbx'})
    group='Static'

def console(name,kind):
    global group
    start(name)
    box('Foot',(0,-1.025,.05),(.7,.15,.6),'Stainless',.025)
    box('Pedestal',(0,-.65,.05),(.22,.7,.23),'Stainless',.018)
    box('ServiceChannel',(0,-.67,-.069),(.12,.48,.012),'Rubber',.004)
    box('Housing',(0,0,.0),(.79,.69,.29),'Red' if kind=='alarm' else 'Blue',.035)
    box('RearServiceDoor',(0,0,.155),(.65,.55,.022),'Stainless');bolts((0,0,-.165),.66,.56)
    for i in range(7):box('Vent',(.399,-.18+i*.045,.02),(.009,.017,.17),'Rubber',.001)
    frame((0,0,-.16),.71,.59,'Stainless')
    box('LowerLegend',(0,-.308,-.162),(.40,.029,.014),'White',.003)
    for x in (-.24,.24):
        box('FootPad',(x,-1.087,.05),(.15,.022,.39),'Rubber',.006)
    if kind=='alarm':
        pipe('ButtonRing',(0,0,-.17),(0,0,-.235),.17,'Stainless',32)
        ellipsoid('PushCap',(0,0,-.24),(.245,.245,.09),'Red')
        box('LabelPlate',(0,-.22,-.178),(.37,.065,.02),'White')
        box('GuardLip',(0,.19,-.206),(.40,.035,.095),'Stainless',.012)
    elif kind=='radio':
        box('Readout',(.13,.15,-.176),(.30,.13,.025),'Screen');frame((.13,.15,-.18),.32,.15)
        box('Handset',(-.21,0,-.225),(.095,.40,.095),'Rubber',.035)
        for y in (-.18,.18):ellipsoid('Receiver',(-.21,y,-.25),(.19,.14,.13),'Rubber')
        for i in range(4):box('SpeakerGrille',(-.21,.135+i*.026,-.318),(.11,.009,.005),'White',.001)
        for r in range(3):
            for c in range(3):box('Key',(.02+c*.10,-.01-r*.085,-.185),(.065,.05,.035),'White',.009)
        curve('CurledCable',[(-.21+.035*math.cos(i*.7),-.25-i*.006,-.23+.035*math.sin(i*.7)) for i in range(45)],.009,'Rubber')
        pipe('Antenna',(.30,.3,.04),(.30,.85,.04),.012,'Rubber')
    else:
        box('Display',(0,.045,-.177),(.60,.34,.027),'Screen',.018);frame((0,.045,-.182),.62,.36)
        if name=='RouteConsole':
            for x in (-.18,0,.18):box('RouteLane',(x,.045,-.195),(.012,.25,.004),'White',0)
            box('RouteLink',(0,.03,-.196),(.42,.012,.004),'Diffuser',0)
        elif name=='AssemblyRegister':
            for row in range(3):
                for col in range(2):box('RegisterRow',(-.145+col*.29,.13-row*.085,-.195),(.20,.015,.004),'White',0)
        else:
            for i in range(4):box('DiagramLine',(-.025,.15-i*.07,-.195),(.43,.012,.004),'White',0)
        for i in range(3):box('FunctionKey',(-.2+i*.2,-.23,-.183),(.13,.05,.026),'White')
        box('MapIndicator',(.17,.09,-.2),(.10,.09,.006),'Green')
    group='Status';box('LED',(.285,.24,-.191),(.065,.034,.028),'Screen',.01)
    group='Static';save(name)
for n,k in [('SituationPanel','panel'),('AlarmSimulator','alarm'),('RadioConsole','radio'),('RouteConsole','panel'),('AssemblyRegister','panel')]:console(n,k)
start('DirectionSign')
box('Base',(0,-1.025,.05),(.7,.15,.6),'Stainless');pipe('Pole',(0,-.94,.05),(0,-.32,.05),.07,'Stainless')
box('Board',(0,0,0),(.8,.7,.30),'Green',.025);frame((0,0,-.17),.74,.63,'Stainless')
box('SignFaceGlazing',(0,0,-.158),(.66,.54,.008),'Glass',.008)
box('RearMount',(0,-.20,.16),(.32,.10,.035),'Stainless',.008)
box('ArrowShaft',(0,0,-.17),(.08,.36,.025),'White')
for sign in (-1,1):pipe('ArrowHead',(0,.18,-.18),(sign*.14,.01,-.18),.028,'White')
group='Status';box('LED',(.28,-.24,-.18),(.06,.03,.03),'Screen');save('DirectionSign')
start('AccessGate')
box('Foot',(0,-1.025,.05),(.7,.15,.6),'Rubber');box('Pedestal',(0,-.47,0),(.60,.94,.36),'Stainless',.04)
box('Top',(0,.12,0),(.79,.46,.30),'Stainless',.035);box('Reader',(0,.16,-.163),(.35,.22,.035),'Screen')
frame((0,.16,-.19),.37,.24);bolts((0,-.40,-.2),.40,.7)
pipe('Hinge',(.1,.05,-.22),(.1,.05,.22),.10,'Stainless',24)
box('MaintenanceGasket',(0,-.48,-.186),(.42,.61,.012),'Rubber',.012)
box('MaintenancePanel',(0,-.48,-.195),(.39,.57,.012),'Stainless',.012)
box('ReaderIndicator',(0,.315,-.155),(.23,.012,.008),'Diffuser',.002)
group='MovingPart';box('Arm',(.30,.05,0),(.8,.12,.15),'Yellow')
for i in range(4):box('Stripe',(.02+i*.18,.05,-.078),(.08,.12,.009),'Metal',0)
group='Status';box('LED',(.28,.26,-.18),(.06,.035,.025),'Screen');save('AccessGate')
start('Bench')
for x in (-.9,.9):
    pipe('Leg',(x,.08,0),(x,.53,0),.065,'Stainless');box('Foot',(x,.06,0),(.20,.10,.62),'Rubber')
    for z in (-.22,.22):pipe('AnchorBolt',(x,.106,z),(x,.115,z),.018,'Metal',12)
pipe('CrossBar',(-1.12,.42,0),(1.12,.42,0),.05,'Stainless')
for i in (-1,0,1):
    x=i*.77
    perforated_sheet('PunchedSeat',(x,.58,-.015),(.71,.10,.68),'xz')
    perforated_sheet('PunchedBack',(x,.96,.27),(.71,.66,.10))
    # Rolled outer edges keep the stamped sheet silhouette distinct at player distance.
    pipe('BackTop',(x-.337,.96+.313,.27),(x+.337,.96+.313,.27),.017,'Stainless')
    pipe('SeatFront',(x-.337,.58,-.338),(x+.337,.58,-.338),.017,'Stainless')
for x in (-1.16,0,1.16):curve('Armrest',[(x,.58,.20),(x,.86,.20),(x,.90,.12),(x,.90,-.28),(x,.86,-.31)],.028,'Stainless')
save('Bench')
start('Pillar')
pipe('Shaft',(0,-1.6,0),(0,1.6,0),.325,'Roof',32)
for y,h,r,m in [(-1.45,.30,.35,'Stainless'),(1.52,.13,.35,'Roof'),(.08,.16,.329,'Stainless')]:pipe('Ring',(0,y-h/2,0),(0,y+h/2,0),r,m,32)
box('CladdingSeam',(0,0,.326),(.004,2.7,.003),'Metal',0)
save('Pillar')
start('InformationKiosk')
box('Plinth',(0,.065,0),(1.45,.13,.72),'Stainless',.035);box('Body',(0,.6,0),(1.35,1,.65),'Roof',.045)
box('ServiceDoor',(0,.6,-.337),(1.12,.80,.025),'Stainless');box('Pull',(.43,.66,-.38),(.04,.18,.04),'Rubber')
box('ScreenHousing',(0,1.56,0),(1.2,.91,.22),'Rubber',.04);box('Glass',(0,1.58,-.13),(1.04,.70,.022),'Screen')
for i in range(4):box('ScreenRow',(-.09,1.80-i*.15,-.145),(.71,.035,.008),'White')
box('Shelf',(0,1.1,-.23),(1.15,.06,.27),'Stainless');box('Canopy',(0,2.10,0),(1.65,.16,.75),'Blue')
box('TaskLight',(0,2.015,-.10),(1.2,.016,.10),'Diffuser',.004)
box('TicketSlot',(0,.90,-.355),(.37,.023,.012),'Rubber',.003)
for i in range(7):box('Vent',(.68,.35+i*.06,0),(.012,.025,.40),'Rubber')
save('InformationKiosk')
start('HazardIndicator')
box('Pedestal',(0,.15,0),(1.1,.30,1.1),'Red',.03);pipe('Mount',(0,.30,0),(0,.4,0),.34,'Stainless',32)
for x in (-.32,.32):curve('Guard',[(x,.35,-.23),(x,1.22,-.23),(x,1.29,0),(x,1.22,.23),(x,.35,.23)],.022,'Stainless')
for x in (-.4,-.2,0,.2,.4):box('BaseWarningStripe',(x,.303,-.28),(.085,.006,.35),'Yellow',0)
group='Beacon';ellipsoid('Lens',(0,.75,0),(.58,.83,.58),'Orange');save('HazardIndicator')
start('AssemblySign');box('Housing',(0,0,0),(1.7,.45,.10),'Green',.014);frame((0,0,-.07),1.64,.40,'Stainless')
for s in (-1,1):pipe('Mount',(s*.65,.22,0),(s*.65,.48,0),.02,'Stainless')
box('Arrow',(0,0,-.075),(.06,.28,.02),'White');pipe('Tip',(0,.14,-.085),(-.12,.02,-.085),.025,'White');pipe('Tip',(0,.14,-.085),(.12,.02,-.085),.025,'White');save('AssemblySign')
start('CeilingLight');box('Housing',(0,0,0),(2.6,.11,.32),'Roof',.012);box('Diffuser',(0,-.063,0),(2.35,.025,.23),'Diffuser',.01)
for x in (-1.14,1.14):box('EndCap',(x,0,0),(.10,.13,.34),'Stainless',.01)
for x in (-.9,-.6,-.3,0,.3,.6,.9):box('Louver',(x,-.077,0),(.015,.007,.23),'Stainless',.001)
save('CeilingLight')
start('WallModule');box('Panel',(0,0,0),(1,1,1),'Wall',.002);save('WallModule')
start('FloorModule');tile=box('Tile',(0,0,0),(1,1,1),'Stone',0);unit_face_uv(tile);save('FloorModule')
start('DoorFrame')
for x in (-1.5,1.5):
    box('Jamb',(x,1.5,0),(.13,3,.32),'Stainless',.01)
    box('Inlay',(x,1.5,-.163),(.037,2.8,.008),'Rubber',.002)
box('Header',(0,2.95,0),(3.12,.15,.32),'Stainless');bolts((-1.5,1.4,-.17),.06,2.3);save('DoorFrame')
start('Luggage')
box('Case',(0,.40,0),(.52,.66,.30),'Blue',.075)
for x in (-.20,.20):pipe('Wheel',(x,.065,-.1),(x,.065,.1),.065,'Rubber')
for x in (-.16,.16):pipe('HandleRod',(x,.70,.09),(x,1,.09),.012,'Stainless')
for x in (-.18,-.09,0,.09,.18):box('ShellRib',(x,.40,-.154),(.027,.48,.012),'Blue',.006)
box('Handle',(0,1,.09),(.35,.035,.045),'Rubber');save('Luggage')
start('RallyPoint')
box('Base',(0,.08,0),(.65,.16,.55),'Stainless');pipe('Stand',(0,.12,0),(0,.95,0),.05,'Stainless')
box('Plate',(0,1.15,0),(.70,.43,.13),'Green');ellipsoid('Head',(0,1.22,-.08),(.09,.09,.04),'White');box('Person',(0,1.08,-.08),(.13,.17,.035),'White')
frame((0,1.15,-.063),.64,.37,'Stainless')
group='Status';box('LED',(.26,1.28,-.09),(.065,.025,.018),'Screen');save('RallyPoint')
start('Glove')
box('Sleeve',(0,-.11,0),(.115,.19,.12),'Blue',.03);ellipsoid('Palm',(0,.035,.025),(.115,.15,.1),'Rubber')
box('Cuff',(0,-.035,0),(.125,.034,.13),'Yellow')
for i in range(4):
    x=-.041+i*.027;ellipsoid('Finger',(x,.126,.06),(.025,.10,.047),'Rubber');ellipsoid('Knuckle',(x,.078,.073),(.028,.033,.022),'Rubber')
box('CuffStitch',(0,-.012,.067),(.101,.004,.003),'White',0)
ellipsoid('Thumb',(-.065,.035,.044),(.04,.085,.055),'Rubber');save('Glove')
start('Evacuee')
# Identical mannequin: no face, feelings, clothing variation or real person likeness.
group='Torso';ellipsoid('Torso',(0,1.18,0),(.46,.55,.28),'Blue');ellipsoid('Pelvis',(0,.88,0),(.35,.24,.25),'Metal')
box('VestBand',(0,1.12,-.145),(.38,.055,.018),'Yellow');pipe('Neck',(0,1.40,0),(0,1.50,0),.06,'Wall')
box('JacketPlacket',(0,1.22,-.144),(.018,.34,.008),'Rubber',.003)
group='Head';ellipsoid('BlankHead',(0,1.62,0),(.24,.30,.23),'Wall')
for side,x in [('Left',-.13),('Right',.13)]:
    group=side+'Leg';ellipsoid('Thigh',(x,.68,0),(.16,.43,.18),'Metal');ellipsoid('Knee',(x,.47,-.01),(.145,.14,.15),'Wall');ellipsoid('Shin',(x,.28,0),(.12,.34,.13),'Metal');box('Shoe',(x,.06,-.045),(.17,.12,.30),'Rubber',.025)
for side,x in [('Left',-.28),('Right',.28)]:
    group=side+'Arm';ellipsoid('Shoulder',(x,1.31,0),(.18,.21,.20),'Blue');ellipsoid('UpperArm',(x,1.13,0),(.135,.30,.15),'Blue');ellipsoid('Elbow',(x,.98,0),(.115,.115,.115),'Wall');ellipsoid('Forearm',(x,.85,0),(.105,.22,.115),'Blue');ellipsoid('Hand',(x,.70,-.005),(.10,.13,.07),'Wall')
save('Evacuee')
start('NavigationArrow')
box('Shaft',(0,0,-.08),(.08,.015,.40),'Green',.01)
for side in (-1,1):pipe('Head',(0,0,.20),(side*.17,0,0),.034,'Green')
save('NavigationArrow')
start('AssemblyRing')
bpy.ops.mesh.primitive_torus_add(major_segments=48,minor_segments=8,location=(0,0,0),major_radius=.48,minor_radius=.02)
o=bpy.context.object
for face in o.data.polygons:face.use_smooth=True
finish(o,'Ring','Green');save('AssemblyRing')

# Reusable architecture: source dimensions are envelopes, never inferred station surveys.
start('RoofTruss')
for y in (.08,1.02):pipe('TubularChord',(-7.42,y,0),(7.42,y,0),.08,'Roof',24)
for cell in range(8):
    x0=-7.35+cell*(14.7/8);x1=x0+14.7/8
    y0,y1=(.15,.95) if cell%2==0 else (.95,.15)
    pipe('DiagonalWeb',(x0,y0,0),(x1,y1,0),.040,'Roof',16)
    pipe('VerticalWeb',(x0,.15,0),(x0,.95,0),.032,'Roof',16)
for x in (-7.43,7.43):
    box('EndBearingPlate',(x,.55,0),(.14,1.1,.20),'Stainless',.008)
    for y in (.18,.92):pipe('PlateFastener',(x,y,-.10),(x,y,-.09),.021,'Metal',12)
save('RoofTruss')

start('ClerestoryBay')
for y in (-.465,.465):box('HorizontalFrame',(0,y,0),(1,.07,.12),'Stainless',.008)
for x in (-.465,.465):box('VerticalFrame',(x,0,0),(.07,.86,.12),'Stainless',.008)
box('Glazing',(0,0,.018),(.86,.86,.012),'Glass',.002)
for x in (-.431,.431):box('GlazingSeal',(x,0,-.025),(.008,.86,.012),'Rubber',.002)
for y in (-.431,.431):box('GlazingSeal',(0,y,-.025),(.86,.008,.012),'Rubber',.002)
box('Mullion',(0,0,-.006),(.026,.86,.09),'Stainless',.004)
save('ClerestoryBay')

start('DepartureBoard')
box('Housing',(0,0,0),(3.2,1,.15),'Blue',.025)
box('DisplayGasket',(0,0,-.064),(3.08,.86,.012),'Rubber',.012)
box('ReadoutSurface',(0,-.025,-.070),(2.97,.71,.006),'Screen',.006)
frame((0,0,-.045),3.14,.92,'Stainless')
box('HeaderBand',(0,.325,-.072),(2.96,.12,.004),'Blue',.002)
for row in range(4):
    y=.19-row*.15
    box('RowRule',(0,y-.065,-.073),(2.90,.003,.002),'Metal',0)
    for x,width in [(-1.19,.26),(-.52,.59),(.37,.38),(1.03,.44)]:
        box('TimetableField',(x,y,-.074),(width,.021,.001),'White',0)
    box('ServiceStatus',(1.36,y,-.074),(.055,.022,.001),'Diffuser',0)
for x in (-1.41,1.41):
    pipe('RearMount',(x,-.28,.053),(x,.28,.053),.020,'Stainless',12)
save('DepartureBoard')

start('InformationIsland')
box('ToeKick',(0,.05,0),(1.48,.10,.58),'Rubber',.018)
box('CounterBody',(0,.555,.01),(1.58,.91,.61),'Blue',.035)
box('StoneFacing',(0,.56,-.308),(1.44,.75,.023),'Stone',.012)
box('CounterTop',(0,1.055,0),(1.7,.09,.7),'Stone',.025)
box('UnderCounterRail',(0,.997,-.305),(1.53,.022,.03),'Stainless',.005)
for x in (-.39,.39):
    box('RearAccessDoor',(x,.56,.322),(.72,.72,.015),'Stainless',.01)
    box('DoorPull',(x,.73,.335),(.13,.019,.020),'Rubber',.005)
# A small manufactured information pictogram; station text is a Unity label.
ellipsoid('InformationDot',(-.52,.72,-.334),(.06,.06,.022),'White')
box('InformationStem',(-.52,.60,-.334),(.047,.13,.022),'White',.005)
box('ServiceStrip',(.16,.74,-.329),(.73,.018,.008),'Diffuser',.002)
save('InformationIsland')

if len(assets)!=26 or len({a['id'] for a in assets})!=26:
    raise RuntimeError('Expected 22 existing assets and four architecture additions.')
# Organize source catalog in collections, each asset retains its local origin for re-export.
SOURCE.parent.mkdir(parents=True,exist_ok=True);bpy.context.preferences.filepaths.save_version=0
catalog=bpy.context.scene;catalog.name='00 - Asset catalog'
for i,a in enumerate(assets):
    col=bpy.data.collections[a['id']]
    asset_scene=bpy.data.scenes.new(a['id']);asset_scene.collection.children.link(col)
    catalog.collection.children.unlink(col)
    instance=bpy.data.objects.new(a['id'],None);instance.instance_type='COLLECTION';instance.instance_collection=col
    instance.location=((i%5)*3.5,(i//5)*4,1.1 if a['id'] in ('SituationPanel','AlarmSimulator','RadioConsole','RouteConsole','AssemblyRegister','DirectionSign','AccessGate') else 0)
    if a['id']=='RoofTruss':instance.location=(7.5,26,0)
    catalog.collection.objects.link(instance)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.overlay.show_extras=False;space.shading.color_type='MATERIAL'
            space.region_3d.view_location=(7,8,1)
            space.region_3d.view_distance=24
            space.region_3d.view_rotation=(Vector((7,8,1))-Vector((22,30,18))).to_track_quat('-Z','Y')
# Keep saved editor browsing state portable; never retain a workstation home path.
for screen in bpy.data.screens:
    for area in screen.areas:
        for space in area.spaces:
            if space.type=='FILE_BROWSER' and space.params is not None:
                space.params.directory=b'//'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
manifest={'generator':'chooguard-synthetic-blender-kit','version':2,'blender':bpy.app.version_string,
          'units':'metres','style':'reference-informed-realism','normalMode':'surface-dependent',
          'referenceProvenance':'docs/art/public-station-references.md','geometryStatus':'synthetic-unverified',
          'textureInputs':0,'materialNames':list(PALETTE),'assets':assets,
          'triangles':sum(x['triangles'] for x in assets),'sourceTriangleBudget':100000,
          'source':'foundation/art/station-kit.blend','noExternalInputs':True,'noBakeOrRender':True}
for a in assets:a['sha256']=hashlib.sha256((ROOT/a['file']).read_bytes()).hexdigest()
(ROOT/'foundation/art/asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('CHOO_ASSET_REPORT '+json.dumps(manifest))
