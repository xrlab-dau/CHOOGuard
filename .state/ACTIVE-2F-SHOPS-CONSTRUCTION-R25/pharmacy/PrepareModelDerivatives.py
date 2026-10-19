import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
b=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/pharmacy').resolve();p=b/'sources/store-shelves'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/'store-shelves.glb'))
keep=[]
for ob in list(bpy.data.objects):
 if ob.type!='MESH':continue
 corners=[ob.matrix_world@Vector(v) for v in ob.bound_box]
 if min(v.y for v in corners)<-.6:continue
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 # Convert to world coordinates, then keep only one original face of double-sided display.
 for v in ob.data.vertices:v.co=ob.matrix_world@v.co
 ob.matrix_world.identity();bm=bmesh.new();bm.from_mesh(ob.data)
 geom=list(bm.verts)+list(bm.edges)+list(bm.faces);ret=bmesh.ops.bisect_plane(bm,geom=geom,plane_co=(0,.022,0),plane_no=(0,1,0),clear_outer=True,clear_inner=False,dist=.00001)
 edges=[e for e in ret['geom_cut'] if isinstance(e,bmesh.types.BMEdge)]
 if edges:
  try:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
  except:pass
 bm.to_mesh(ob.data);bm.free();ob.name='OriginalFreeShelf_'+ob.name;keep.append(ob)
bpy.ops.object.select_all(action='DESELECT')
for ob in keep:ob.select_set(True)
bpy.context.view_layer.objects.active=keep[0];bpy.ops.object.join();out=bpy.context.object;out.name='PeterD_SingleFace_PharmacyShelf_MODEL'
bpy.ops.export_scene.fbx(filepath=str(p/'SingleFace-Shelf.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
(p/'DERIVATION.json').write_text(json.dumps({'source':'Store Shelves by Peter_D, CC-BY4','original_unchanged':'store-shelves.glb','derivative':'SingleFace-Shelf.fbx','changes':'One original five-tier module selected; opposite face removed at localY0.022m. Fit and white/blue own materials applied to field-photo shelving. No source/otherAssets change.'},indent=2))
p=b/'sources/three-pill-boxes';bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/'three-pill-boxes.glb'))
for i,ob in enumerate([o for o in bpy.data.objects if o.type=='MESH']):
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;ob.name=f'iPau_ClosedMedicineCarton_{i}'
 bpy.ops.export_scene.fbx(filepath=str(p/f'MedicineCarton-{i}.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
# Add bevel facets to source-traced sign solids, preserving holes and physical topology.
p=b/'sign-model-authoring';sign=json.loads((p/'SOURCE-TRACED-SIGN-SOLIDS.json').read_text());out={}
for name,x in sign.items():
 bpy.ops.wm.read_factory_settings(use_empty=True);mesh=bpy.data.meshes.new(name);mesh.from_pydata(x['vertices'],[],[x['triangles'][i:i+3] for i in range(0,len(x['triangles']),3)]);mesh.update();ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
 bevel=ob.modifiers.new('Observed sign bevel','BEVEL');bevel.width=.002;bevel.segments=2;bevel.limit_method='ANGLE';bevel.angle_limit=math.radians(35);bpy.ops.object.modifier_apply(modifier=bevel.name);mesh=ob.data;mesh.calc_loop_triangles();out[name]={'vertices':[list(v.co) for v in mesh.vertices],'triangles':[i for t in mesh.loop_triangles for i in t.vertices],'depth_m':x['depth_m'],'bevel_m':.002,'bevel_segments':2}
(p/'SOURCE-TRACED-SIGN-BEVELED.json').write_text(json.dumps(out));print('OWN_DERIVATIVES_READY',[(k,len(v['vertices'])) for k,v in out.items()])
