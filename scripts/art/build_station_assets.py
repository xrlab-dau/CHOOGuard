"""Synthetic Blender source meshes. Run Blender 4.5 LTS --background --python this_file.
No photos, downloads, bake, render, or facility data. SI metres; Unity +Y up/+Z forward.
"""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/CHOOguardArt/Blender'
SOURCE=ROOT/'foundation/art/station-kit.blend'
PALETTE={'Floor':(.22,.24,.25,1),'Wall':(.56,.60,.61,1),'Metal':(.10,.13,.15,1),'Blue':(.09,.27,.40,1),'Red':(.68,.10,.08,1),'Yellow':(.88,.64,.15,1),'Green':(.10,.48,.30,1),'Screen':(.08,.58,.65,1),'White':(.86,.89,.87,1),'Orange':(1,.36,.05,1)}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for block in list(bpy.data.materials): bpy.data.materials.remove(block)
M={}
for name,col in PALETTE.items():
    m=bpy.data.materials.new(name);m.diffuse_color=col;m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=col
    bs.inputs['Metallic'].default_value=0
    bs.inputs['Roughness'].default_value=1
    M[name]=m
assets=[]; parts=[]; group='Static'
def v(p): return (p[0],-p[2],p[1])
def finish(o,name,mat):
    o.name=group+'__'+name;o.data.materials.append(M[mat]);parts.append(o)
    return o
def box(name,p,s,mat='Metal',bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=v(p));o=bpy.context.object;o.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Manufactured edge radii','BEVEL');mod.width=min(bevel,min(s)*.22);mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
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
    bpy.ops.object.convert(target='MESH');o=bpy.context.object;return finish(o,name,mat)
def frame(p,w,h,mat='Metal'):
    x,y,z=p
    for yy in (-h/2,h/2):box('BezelRail',(x,y+yy,z),(w,.035,.04),mat)
    for xx in (-w/2,w/2):box('BezelRail',(x+xx,y,z),(.035,h,.04),mat)
def bolts(p,w,h):
    x,y,z=p
    for dx in (-w/2,w/2):
        for dy in (-h/2,h/2):
            pipe('RecessedFastener',(x+dx,y+dy,z),(x+dx,y+dy,z-.01),.014,'White',12)
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
    for o in objects:
        for face in o.data.polygons:face.use_smooth=False
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='STRIP')
    tri=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
    collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
    for o in objects:
        for c in list(o.users_collection):c.objects.unlink(o)
        collection.objects.link(o)
    assets.append({'id':name,'triangles':tri,'meshParts':len(objects),'file':f'Assets/CHOOguardArt/Blender/{name}.fbx'})
    group='Static'

def console(name,kind):
    global group
    start(name)
    box('Foot',(0,-1.025,.05),(.7,.15,.6),'Metal',.035)
    box('Pedestal',(0,-.65,.05),(.22,.7,.23),'Wall')
    box('Housing',(0,0,.0),(.79,.69,.29),'Red' if kind=='alarm' else 'Blue',.06)
    box('RearServiceDoor',(0,0,.155),(.65,.55,.022),'Metal');bolts((0,0,-.165),.66,.56)
    for i in range(7):box('Vent',(.399,-.18+i*.045,.02),(.009,.017,.17),'Metal',.001)
    frame((0,0,-.16),.71,.59)
    if kind=='alarm':
        pipe('ButtonRing',(0,0,-.17),(0,0,-.235),.17,'White',32)
        ellipsoid('PushCap',(0,0,-.24),(.245,.245,.09),'Red')
        box('LabelPlate',(0,-.22,-.178),(.37,.065,.02),'White')
    elif kind=='radio':
        box('Readout',(.13,.15,-.176),(.30,.13,.025),'Screen');frame((.13,.15,-.18),.32,.15)
        box('Handset',(-.21,0,-.225),(.095,.40,.095),'Metal',.04)
        for y in (-.18,.18):ellipsoid('Receiver',(-.21,y,-.25),(.19,.14,.13),'Metal')
        for i in range(4):box('SpeakerGrille',(-.21,.135+i*.026,-.318),(.11,.009,.005),'White',.001)
        for r in range(3):
            for c in range(3):box('Key',(.02+c*.10,-.01-r*.085,-.185),(.065,.05,.035),'White',.009)
        curve('CurledCable',[(-.21+.035*math.cos(i*.7),-.25-i*.006,-.23+.035*math.sin(i*.7)) for i in range(45)],.009)
        pipe('Antenna',(.30,.3,.04),(.30,.85,.04),.012)
    else:
        box('Display',(0,.045,-.177),(.60,.34,.027),'Screen',.018);frame((0,.045,-.182),.62,.36)
        for i in range(4):box('DiagramLine',(-.025,.15-i*.07,-.195),(.43,.012,.004),'White',0)
        for i in range(3):box('FunctionKey',(-.2+i*.2,-.23,-.183),(.13,.05,.026),'White')
        box('MapIndicator',(.17,.09,-.2),(.10,.09,.006),'Green')
    group='Status';box('LED',(.285,.24,-.191),(.065,.034,.028),'Screen',.01)
    group='Static';save(name)
for n,k in [('SituationPanel','panel'),('AlarmSimulator','alarm'),('RadioConsole','radio'),('RouteConsole','panel'),('AssemblyRegister','panel')]:console(n,k)
start('DirectionSign')
box('Base',(0,-1.025,.05),(.7,.15,.6));pipe('Pole',(0,-.94,.05),(0,-.32,.05),.07,'White')
box('Board',(0,0,0),(.8,.7,.30),'Green',.045);frame((0,0,-.17),.74,.63,'White')
box('ArrowShaft',(0,0,-.17),(.08,.36,.025),'White')
for sign in (-1,1):pipe('ArrowHead',(0,.18,-.18),(sign*.14,.01,-.18),.028,'White')
group='Status';box('LED',(.28,-.24,-.18),(.06,.03,.03),'Screen');save('DirectionSign')
start('AccessGate')
box('Foot',(0,-1.025,.05),(.7,.15,.6));box('Pedestal',(0,-.47,0),(.60,.94,.36),'Wall',.06)
box('Top',(0,.12,0),(.79,.46,.30),'Metal',.05);box('Reader',(0,.16,-.163),(.35,.22,.035),'Screen')
frame((0,.16,-.19),.37,.24);bolts((0,-.40,-.2),.40,.7)
pipe('Hinge',(.1,.05,-.22),(.1,.05,.22),.10,'White',24)
group='MovingPart';box('Arm',(.30,.05,0),(.8,.12,.15),'Yellow')
for i in range(4):box('Stripe',(.02+i*.18,.05,-.078),(.08,.12,.009),'Metal',0)
group='Status';box('LED',(.28,.26,-.18),(.06,.035,.025),'Screen');save('AccessGate')
start('Bench')
for x in (-.9,.9):
    pipe('Leg',(x,.08,0),(x,.53,0),.065,'White');box('Foot',(x,.06,0),(.20,.10,.62),'Metal')
pipe('CrossBar',(-1.12,.42,0),(1.12,.42,0),.05,'White')
for i in (-1,0,1):
    x=i*.77;box('Seat',(x,.58,-.015),(.71,.10,.68),'Blue',.065);box('Back',(x,.96,.27),(.71,.66,.10),'Blue',.065)
    for j in range(6):box('Slat',(x-.26+j*.104,.635,-.02),(.009,.008,.52),'Metal',.002)
for x in (-1.16,0,1.16):curve('Armrest',[(x,.58,.20),(x,.86,.20),(x,.90,.12),(x,.90,-.28),(x,.86,-.31)],.028,'White')
save('Bench')
start('Pillar')
pipe('Shaft',(0,-1.6,0),(0,1.6,0),.325,'Wall',32)
for y,h,r,m in [(-1.45,.30,.35,'Metal'),(1.52,.13,.35,'White'),(.08,.16,.329,'Blue')]:pipe('Ring',(0,y-h/2,0),(0,y+h/2,0),r,m,32)
save('Pillar')
start('InformationKiosk')
box('Plinth',(0,.065,0),(1.45,.13,.72),'Metal',.06);box('Body',(0,.6,0),(1.35,1,.65),'Wall',.08)
box('ServiceDoor',(0,.6,-.337),(1.12,.80,.025),'Blue');box('Pull',(.43,.66,-.38),(.04,.18,.04),'White')
box('ScreenHousing',(0,1.56,0),(1.2,.91,.22),'Metal',.07);box('Glass',(0,1.58,-.13),(1.04,.70,.022),'Screen')
for i in range(4):box('ScreenRow',(-.09,1.80-i*.15,-.145),(.71,.035,.008),'White')
box('Shelf',(0,1.1,-.23),(1.15,.06,.27),'Metal');box('Canopy',(0,2.10,0),(1.65,.16,.75),'Blue')
for i in range(7):box('Vent',(.68,.35+i*.06,0),(.012,.025,.40),'Metal')
save('InformationKiosk')
start('HazardIndicator')
box('Pedestal',(0,.15,0),(1.1,.30,1.1),'Red',.05);pipe('Mount',(0,.30,0),(0,.4,0),.34,'Metal',32)
for x in (-.32,.32):curve('Guard',[(x,.35,-.23),(x,1.22,-.23),(x,1.29,0),(x,1.22,.23),(x,.35,.23)],.022,'White')
group='Beacon';ellipsoid('Lens',(0,.75,0),(.58,.83,.58),'Orange');save('HazardIndicator')
start('AssemblySign');box('Housing',(0,0,0),(1.7,.45,.10),'Green');frame((0,0,-.07),1.64,.40,'White')
for s in (-1,1):pipe('Mount',(s*.65,.22,0),(s*.65,.48,0),.02,'Metal')
box('Arrow',(0,0,-.075),(.06,.28,.02),'White');pipe('Tip',(0,.14,-.085),(-.12,.02,-.085),.025,'White');pipe('Tip',(0,.14,-.085),(.12,.02,-.085),.025,'White');save('AssemblySign')
start('CeilingLight');box('Housing',(0,0,0),(2.6,.11,.32),'Metal');box('Diffuser',(0,-.063,0),(2.35,.025,.23),'White')
for x in (-1.14,1.14):box('EndCap',(x,0,0),(.10,.13,.34),'Wall');save_dummy=None
save('CeilingLight')
start('WallModule');box('Panel',(0,0,0),(1,1,1),'Wall',.018);save('WallModule')
start('FloorModule');box('Tile',(0,0,0),(1,1,1),'Floor',.008);save('FloorModule')
start('DoorFrame')
for x in (-1.5,1.5):box('Jamb',(x,1.5,0),(.13,3,.32),'Metal')
box('Header',(0,2.95,0),(3.12,.15,.32),'Blue');bolts((-1.5,1.4,-.17),.06,2.3);save('DoorFrame')
start('Luggage')
box('Case',(0,.40,0),(.52,.66,.30),'Blue',.075)
for x in (-.20,.20):pipe('Wheel',(x,.065,-.1),(x,.065,.1),.065,'Metal')
for x in (-.16,.16):pipe('HandleRod',(x,.70,.09),(x,1,.09),.012,'White')
box('Handle',(0,1,.09),(.35,.035,.045),'Metal');save('Luggage')
start('RallyPoint')
box('Base',(0,.08,0),(.65,.16,.55),'Metal');pipe('Stand',(0,.12,0),(0,.95,0),.05,'White')
box('Plate',(0,1.15,0),(.70,.43,.13),'Green');ellipsoid('Head',(0,1.22,-.08),(.09,.09,.04),'White');box('Person',(0,1.08,-.08),(.13,.17,.035),'White')
group='Status';box('LED',(.26,1.28,-.09),(.065,.025,.018),'Screen');save('RallyPoint')
start('Glove')
box('Sleeve',(0,-.11,0),(.115,.19,.12),'Blue',.03);ellipsoid('Palm',(0,.035,.025),(.115,.15,.1),'Metal')
box('Cuff',(0,-.035,0),(.125,.034,.13),'Yellow')
for i in range(4):
    x=-.041+i*.027;ellipsoid('Finger',(x,.126,.06),(.025,.10,.047),'Metal');ellipsoid('Knuckle',(x,.078,.073),(.028,.033,.022),'White')
ellipsoid('Thumb',(-.065,.035,.044),(.04,.085,.055),'Metal');save('Glove')
start('Evacuee')
# Identical mannequin: no face, feelings, clothing variation or real person likeness.
group='Torso';ellipsoid('Torso',(0,1.18,0),(.46,.55,.28),'Blue');ellipsoid('Pelvis',(0,.88,0),(.35,.24,.25),'Metal')
box('VestBand',(0,1.12,-.145),(.38,.055,.018),'Yellow');pipe('Neck',(0,1.40,0),(0,1.50,0),.06,'Wall')
group='Head';ellipsoid('BlankHead',(0,1.62,0),(.24,.30,.23),'Wall')
for side,x in [('Left',-.13),('Right',.13)]:
    group=side+'Leg';ellipsoid('Thigh',(x,.68,0),(.16,.43,.18),'Metal');ellipsoid('Knee',(x,.47,-.01),(.145,.14,.15),'Wall');ellipsoid('Shin',(x,.28,0),(.12,.34,.13),'Metal');box('Shoe',(x,.06,-.045),(.17,.12,.30),'Metal',.04)
for side,x in [('Left',-.28),('Right',.28)]:
    group=side+'Arm';ellipsoid('Shoulder',(x,1.31,0),(.18,.21,.20),'Blue');ellipsoid('UpperArm',(x,1.13,0),(.135,.30,.15),'Blue');ellipsoid('Elbow',(x,.98,0),(.115,.115,.115),'Wall');ellipsoid('Forearm',(x,.85,0),(.105,.22,.115),'Blue');ellipsoid('Hand',(x,.70,-.005),(.10,.13,.07),'Wall')
save('Evacuee')
start('NavigationArrow')
box('Shaft',(0,0,-.08),(.08,.015,.40),'Green',.01)
for side in (-1,1):pipe('Head',(0,0,.20),(side*.17,0,0),.034,'Green')
save('NavigationArrow')
start('AssemblyRing')
bpy.ops.mesh.primitive_torus_add(major_segments=48,minor_segments=8,location=(0,0,0),major_radius=.48,minor_radius=.02)
o=bpy.context.object;finish(o,'Ring','Green');save('AssemblyRing')
# Organize source catalog in collections, each asset retains its local origin for re-export.
SOURCE.parent.mkdir(parents=True,exist_ok=True);bpy.context.preferences.filepaths.save_version=0
catalog=bpy.context.scene;catalog.name='00 - Asset catalog'
for i,a in enumerate(assets):
    col=bpy.data.collections[a['id']]
    asset_scene=bpy.data.scenes.new(a['id']);asset_scene.collection.children.link(col)
    catalog.collection.children.unlink(col)
    instance=bpy.data.objects.new(a['id'],None);instance.instance_type='COLLECTION';instance.instance_collection=col
    instance.location=((i%5)*3.5,(i//5)*4,1.1 if a['id'] in ('SituationPanel','AlarmSimulator','RadioConsole','RouteConsole','AssemblyRegister','DirectionSign','AccessGate') else 0)
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
manifest={'generator':'chooguard-synthetic-blender-kit','version':1,'blender':bpy.app.version_string,'units':'metres','style':'flat','normalMode':'face','textureInputs':0,'assets':assets,'triangles':sum(x['triangles'] for x in assets),'source':'foundation/art/station-kit.blend','noExternalInputs':True,'noBakeOrRender':True}
for a in assets:a['sha256']=hashlib.sha256((ROOT/a['file']).read_bytes()).hexdigest()
(ROOT/'foundation/art/asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('CHOO_ASSET_REPORT '+json.dumps(manifest))
