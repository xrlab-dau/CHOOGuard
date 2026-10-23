import bpy, sys, json
from pathlib import Path
b=Path("/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/artbox/sources/models")
for src in sorted(b.glob("*/*.glb")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(src))
    bpy.context.scene.frame_set(0)
    texture_dir=src.parent/'textures';texture_dir.mkdir(exist_ok=True)
    images={}
    for im in bpy.data.images:
        if im.type=='IMAGE' and im.size[0]:
            name=''.join(c if c.isalnum() or c in '_-' else '_' for c in im.name)+'.png'
            p=texture_dir/name;im.filepath_raw=str(p);im.file_format='PNG';im.save();images[im.name]=str(p)
    materials=[]
    for mat in bpy.data.materials:
        slots=[]
        for node in mat.node_tree.nodes if mat.use_nodes else []:
            if node.type=='TEX_IMAGE' and node.image:
                slots.append({'slot':node.label or node.name,'file':images.get(node.image.name)})
        bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if mat.use_nodes else None
        materials.append({'name':mat.name,'textures':slots,'base_color':list(bs.inputs['Base Color'].default_value) if bs else list(mat.diffuse_color),'metallic':float(bs.inputs['Metallic'].default_value) if bs else 0,'roughness':float(bs.inputs['Roughness'].default_value) if bs else .5})
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(src.with_suffix('.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
    (src.parent/'UNITY-MATERIAL-SOURCES.json').write_text(json.dumps({'original_glb_preserved':True,'source':str(src),'materials':materials,'mesh_objects':[o.name for o in bpy.data.objects if o.type=='MESH'],'renderer_materials':{o.name:[m.name if m else None for m in o.data.materials] for o in bpy.data.objects if o.type=='MESH'}},indent=2))
    print('R25_DOWNLOADED_MODEL_FBX_READY',src.with_suffix('.fbx'))
