import bpy
from mathutils import Vector
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.ops.import_scene.fbx(filepath='/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/ThirdParty/Models/MiniCharacters/character-male-a.fbx')
for o in bpy.context.scene.objects:
 if o.type=='ARMATURE':
  o.animation_data_clear();print('MATRIX',o.name,o.matrix_world)
  for b in o.data.bones:print('WORLD BONE',b.name,tuple(o.matrix_world@b.head_local))
 if o.type=='MESH':
  c=[o.matrix_world@Vector(v) for v in o.bound_box];print('WORLD MESH',o.name,[[min(v[i] for v in c),max(v[i] for v in c)] for i in range(3)], 'groups',[g.name for g in o.vertex_groups])
