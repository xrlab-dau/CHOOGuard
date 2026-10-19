import bpy, json
from pathlib import Path

root=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/commercial-refrigerator')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root/'CommercialRefrigerator.glb'))
# This is a new derivative of the downloaded free model. The unrelated champagne
# stock is removed, while its detailed refrigerated case, door and glazing remain.
removed=[]
for obj in list(bpy.data.objects):
    if obj.name.startswith('ChampagneBottles'):
        removed.append(obj.name)
        bpy.data.objects.remove(obj,do_unlink=True)
bpy.context.scene.frame_set(0)
textures=root/'textures'; textures.mkdir(exist_ok=True)
image_files={}
for im in bpy.data.images:
    if im.type=='IMAGE' and im.size[0]:
        name=''.join(c if c.isalnum() or c in '_-' else '_' for c in im.name)+'.png'
        target=textures/name
        im.filepath_raw=str(target);im.file_format='PNG';im.save()
        image_files[im.name]=str(target)
materials=[]
for m in bpy.data.materials:
    inputs={}
    for node in m.node_tree.nodes if m.use_nodes else []:
        if node.type=='TEX_IMAGE' and node.image:
            inputs[node.label or node.name]={'image':node.image.name,'file':image_files.get(node.image.name),'outputs_to':[link.to_socket.name for link in node.outputs[0].links]}
    materials.append({'name':m.name,'textures':inputs})
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root/'CommercialRefrigerator-Empty.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
(root/'UNITY-DERIVATIVE-MATERIALS.json').write_text(json.dumps({'source_credit':'Commercial Fridge by Sean Thomas (foon), Khronos contributors','license':'CC-BY-4.0','source_unchanged':True,'removed_unrelated_stock':removed,'materials':materials,'source_meshes':[{'name':o.name,'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)} for o in bpy.data.objects if o.type=='MESH'],'URP_glass_requires_transparent_material':True},indent=2))
print('R25_FREE_FRIDGE_DERIVATIVE_EXPORTED')
