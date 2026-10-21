import bpy,json
from pathlib import Path
base=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/subway/sources/free-models/water_bottle')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(base/'WaterBottle.glb'))
tex=base/'textures';tex.mkdir(exist_ok=True)
manifest={}
for mat in bpy.data.materials:
 if not mat.use_nodes:continue
 info={}
 for n in mat.node_tree.nodes:
  if n.type=='TEX_IMAGE' and n.image:
   im=n.image;path=tex/(im.name.split('.')[0]+'.png');im.file_format='PNG';im.filepath_raw=str(path);im.save();info[n.label or n.name]=str(path)
 manifest[mat.name]=info
(base/'BLENDER-MATERIALS.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(base/'WaterBottle.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,path_mode='RELATIVE')
print('EXPORTED_WATERBOTTLE')
