import bpy,json
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath='/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/ThirdParty/Models/MiniCharacters/character-male-a.fbx')
for o in bpy.context.scene.objects:
 print('OBJECT',o.name,o.type,tuple(o.dimensions),tuple(o.location),tuple(o.scale))
 if o.type=='ARMATURE':
  for b in o.data.bones:print('BONE',b.name,tuple(b.head_local),tuple(b.tail_local))
print('ACTIONS',[(a.name,a.frame_range[:]) for a in bpy.data.actions])
