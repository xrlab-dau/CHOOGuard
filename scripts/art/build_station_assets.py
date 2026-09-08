"""Reference-informed synthetic Blender meshes. Run Blender 4.5 LTS in background.
Public-reference observations: docs/art/public-station-references.md.
No image inputs, downloads, bake, render, or surveyed facility geometry. SI metres.
"""
import bpy, bmesh, math, json, hashlib, re, sys
from types import SimpleNamespace
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/CHOOguardArt/Blender'
SOURCE=ROOT/'foundation/art/station-kit.blend'
PALETTE={'Floor':(.62,.63,.60,1),'Wall':(.72,.73,.70,1),'Metal':(.10,.13,.15,1),'Blue':(.035,.16,.29,1),'Red':(.68,.10,.08,1),'Yellow':(.88,.64,.15,1),'Green':(.10,.48,.30,1),'Screen':(.035,.14,.18,1),'White':(.89,.92,.93,1),'Orange':(1,.36,.05,1),
         'Stainless':(.62,.67,.70,1),'Stone':(.70,.70,.65,1),'Glass':(.64,.80,.83,.26),
         'Diffuser':(.91,.96,1,1),'Roof':(.88,.90,.88,1),'Rubber':(.035,.045,.05,1),'Wood':(.57,.30,.105,1),'Fabric':(.08,.115,.16,1),'ClearGlass':(.80,.88,.90,.18),'Ceiling':(.50,.53,.54,1)}
SURFACES={'Metal':(0,.65),'Stainless':(.85,.28),'Stone':(.02,.58),
          'Glass':(0,.12),'ClearGlass':(0,.10),'Roof':(.10,.38),'Rubber':(0,.88),'Screen':(.05,.22),'Wood':(0,.48),'Fabric':(0,.88)}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for block in list(bpy.data.materials): bpy.data.materials.remove(block)
M={}
for name,col in PALETTE.items():
    m=bpy.data.materials.new(name);m.diffuse_color=col;m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=col
    metallic,roughness=SURFACES.get(name,(0,.52))
    bs.inputs['Metallic'].default_value=metallic
    bs.inputs['Roughness'].default_value=roughness
    if name in {'Glass','ClearGlass'}:
        bs.inputs['Transmission Weight'].default_value=1
        bs.inputs['IOR'].default_value=1.45
        bs.inputs['Alpha'].default_value=col[3]
    if name=='Diffuser':
        bs.inputs['Emission Color'].default_value=col
        bs.inputs['Emission Strength'].default_value=1.4
    M[name]=m
assets=[]; parts=[]; group='Static'; current_module=''
def v(p): return (-p[0],-p[2],p[1])  # Unity FBX importer reverses X handedness.
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

    The upward face is Unity X/Z mapped to U/V: minus Blender X and minus Blender Y.
    Keeping FloorModule unbeveled makes both its metre bounds and UV span exact.
    """
    mesh=o.data;uv=mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    for face in mesh.polygons:
        axis=max(range(3),key=lambda i:abs(face.normal[i]))
        for loop_index in face.loop_indices:
            point=mesh.vertices[mesh.loops[loop_index].vertex_index].co
            u,w=(-point.x,-point.y) if axis==2 else (-point.y,point.z) if axis==0 else (-point.x,point.z)
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
def set_group(name):
    global group
    group=name

def tilt(objects,degrees,pivot=(0,0,0)):
    point=Vector(v(pivot))
    matrix=Matrix.Translation(point)@Matrix.Rotation(math.radians(degrees),4,'X')@Matrix.Translation(-point)
    for obj in objects:obj.matrix_world=matrix@obj.matrix_world

def unity_bounds(objects):
    points=[o.matrix_world@vertex.co for o in objects for vertex in o.data.vertices]
    points=[(-p.x,p.z,-p.y) for p in points]
    return {'min':[round(min(p[i] for p in points),6) for i in range(3)],
            'max':[round(max(p[i] for p in points),6) for i in range(3)]}

COLLISION_COMPONENTS={
 'SituationPanel':['ShallowRearHousing','AluminiumSideRail','SyntheticVesaPedestal','SyntheticVesaPedestalFoot'],
 'AlarmSimulator':['MountBackplate','RedWeatherproofBackbox','SyntheticCallpointPost','SyntheticCallpointPostFoot'],
 'RadioConsole':['WedgeHousing','PagingDeskPedestal','PagingDeskPedestalFoot','MicrophoneWindscreen'],
 'RouteConsole':['FoldedCabinet','ScreenRearCase','RoundedBasePlate'],
 'AssemblyRegister':['RoundedTabletEnclosure','SlenderPost','WeightedDiscBase'],
 'DirectionSign':['SignRearPlate','PortraitFrame','RoundPost','RoundPostFoot'],
 'AccessGate':['RoundedGateCabinet','SlopedReaderLid','TransparentSwingLeaf'],
 'RallyPoint':['PlateBacking','PortablePostAndFoot'],
 'HazardIndicator':['SyntheticBeaconFoot','SyntheticBeaconPost','DomedBeaconLens'],
 'Bench':['TimberSeatSlat','TimberApron','StainlessPedestal','SeatDivider'],
 'InformationKiosk':['UprightCabinet','Plinth','HeaderPanel'],
 'InformationIsland':['CurvedCounterShell','CurvedCounterTop'],
 'Luggage':['HardshellFront','HardshellBack','DoubleCasterWheel','PullGrip'],
}

def save(name):
    global group
    # The mannequin source is sculpted toward -Z like the equipment. Align its visible front
    # with the Unity actor's +Z travel direction, retaining left/right X and all joint origins.
    if name=='Evacuee':
        for obj in parts:
            inverse=obj.matrix_world.inverted()
            for vertex in obj.data.vertices:
                world=obj.matrix_world@vertex.co;world.y=-world.y;vertex.co=inverse@world
            bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    # Custom lofts need authored UVs too; avoid a fabric texture sampling the same zero UV everywhere.
    for obj in parts:
        if obj.data.uv_layers:continue
        uv=obj.data.uv_layers.new(name='UVMap');coords=[v.co for v in obj.data.vertices]
        lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)));hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
        for face in obj.data.polygons:
            axis=max(range(3),key=lambda i:abs(face.normal[i]));axes=([1,2] if axis==0 else [0,2] if axis==1 else [0,1])
            for loop in face.loop_indices:
                co=obj.data.vertices[obj.data.loops[loop].vertex_index].co
                uv.data[loop].uv=tuple((co[i]-lo[i])/max(hi[i]-lo[i],1e-6) for i in axes)
    # Preserve the authored feature inventory before batching destroys component names.
    component_objects={}
    for obj in parts:
        name_part=re.sub(r'\.\d{3}$','',obj.name.split('__',1)[-1])
        component_objects.setdefault(name_part,[]).append(obj)
    components={key:{'count':len(items),'boundsUnity':unity_bounds(items)} for key,items in sorted(component_objects.items())}
    collision_boxes=[]
    for component in COLLISION_COMPONENTS.get(name,[]):
        if component not in component_objects:raise RuntimeError('Collision source missing: '+name+'/'+component)
        objects_for_component=component_objects[component]
        # Keep articulation separate; fixed boxes are expressed in the model's original local metres.
        for index,obj in enumerate(objects_for_component):
            parent='moving' if obj.name.startswith('MovingPart__') else 'model'
            label=component if len(objects_for_component)==1 else component+'_'+str(index)
            collision_boxes.append({'part':label,'component':component,'parent':parent,'boundsUnity':unity_bounds([obj])})
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
    unity_points=[(-p.x,p.z,-p.y) for p in points]
    minimum=[min(p[i] for p in unity_points) for i in range(3)]
    maximum=[max(p[i] for p in unity_points) for i in range(3)]
    tri=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
    if sum(a['triangles'] for a in assets)+tri>150000:
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
                   'file':f'Assets/CHOOguardArt/Blender/{name}.fbx','sourceModule':current_module,'components':components,'collisionBoxes':collision_boxes})
    group='Static'

# Each family has its own individually referenced shape authoring, not one universal console.
sys.path.insert(0,str(Path(__file__).resolve().parent))
from station_equipment import build as build_equipment
from station_furniture import build as build_furniture
from station_architecture import build as build_architecture
context=SimpleNamespace(start=start,save=save,box=box,pipe=pipe,curve=curve,ellipsoid=ellipsoid,
                        frame=frame,bolts=bolts,finish=finish,v=v,unit_face_uv=unit_face_uv,
                        set_group=set_group,parts=lambda:parts,tilt=tilt,materials=M)
for module,builder in [('station_equipment',build_equipment),('station_furniture',build_furniture),('station_architecture',build_architecture)]:
    current_module='scripts/art/'+module+'.py'
    builder(context)

if len(assets)!=33 or len({a['id'] for a in assets})!=33:
    raise RuntimeError('Expected 33 individually referenced asset families.')
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
manifest={'generator':'chooguard-synthetic-blender-kit','version':3,'blender':bpy.app.version_string,
          'units':'metres','coordinateAdapter':'Unity(x,y,z) to Blender(-x,-z,y); FBX import X reversal verified by asymmetric geometry test','style':'reference-informed-realism','normalMode':'surface-dependent',
          'referenceProvenance':'foundation/art/object-references.json','geometryStatus':'synthetic-unverified',
          'textureInputs':0,'materialNames':list(PALETTE),'assets':assets,
          'triangles':sum(x['triangles'] for x in assets),'sourceTriangleBudget':150000,
          'source':'foundation/art/station-kit.blend','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'noImageTextureInputs':True,'noDownloadedMeshInputs':True,'noBakeOrRender':True}
manifest['sourceModuleSha256']={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in sorted({a['sourceModule'] for a in assets})}
for a in assets:a['sha256']=hashlib.sha256((ROOT/a['file']).read_bytes()).hexdigest()
(ROOT/'foundation/art/asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('CHOO_ASSET_REPORT '+json.dumps({'assets':len(assets),'triangles':manifest['triangles'],'parts':sum(a['meshParts'] for a in assets),'manifest':'foundation/art/asset-manifest.json'}))
