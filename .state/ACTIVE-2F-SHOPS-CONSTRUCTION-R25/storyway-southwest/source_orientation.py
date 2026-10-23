import bpy,pathlib,json
from mathutils import Vector
p=pathlib.Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-southwest')
for key in ['chips','can']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.ops.import_scene.gltf(filepath=str(p/'sources/free-models'/key/(key+'.glb')))
 pp=[o.matrix_world@Vector(v) for o in bpy.context.scene.objects if o.type=='MESH' for v in o.bound_box];print(key,'WORLD_BOUNDS',[(min(v[i] for v in pp),max(v[i] for v in pp)) for i in range(3)])
