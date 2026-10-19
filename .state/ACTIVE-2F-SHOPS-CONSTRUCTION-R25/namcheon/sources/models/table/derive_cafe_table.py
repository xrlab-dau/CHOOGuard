import bpy,json
from mathutils import Vector
from pathlib import Path
p=Path(__file__).parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p/'CafeTableAndChairs.glb'))
keep=bpy.data.objects['Object_37']
verts=[keep.matrix_world@v.co for v in keep.data.vertices]
mn=Vector(tuple(min(v[i] for v in verts) for i in range(3)));mx=Vector(tuple(max(v[i] for v in verts) for i in range(3)))
print('BASE_WORLD_BOUNDS',list(mn),list(mx))
for obj in list(bpy.data.objects):
 if obj!=keep:bpy.data.objects.remove(obj,do_unlink=True)
keep.parent=None;keep.matrix_world.identity()
# glTF importer parent world axes already applied into world vertices; align narrow horizontal extent and height.
for v,w in zip(keep.data.vertices,verts):v.co=w
keep.name='DownloadedCafeTable_SteelCrossFoot'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(p/'CafeTableSteelBase.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
(p/'DERIVATION.json').write_text(json.dumps({'source_mesh':'Object_37','source_world_bounds':[list(mn),list(mx)],'removed':'source chairs and slatted tabletop excluded','source_original_preserved':True,'replacement_top':'separate smooth charcoal laminate slab matched to field photo11/12','scope':'own derivative only'},indent=2))
