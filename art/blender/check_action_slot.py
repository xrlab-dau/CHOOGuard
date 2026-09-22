import bpy,json
bpy.ops.wm.open_mainfile(filepath='/Users/um-yunsang/CHOOGuard/art/blender/response_set.blend')
r=bpy.data.objects['CrewOperationsRig'];a=next(a for a in bpy.data.actions if '|walk|' in a.name)
print('ACTION',a.name,'SLOTS',[(s.identifier,s.target_id_type) for s in a.slots]);print('CURVES',len(a.fcurves));print('BONE before',r.pose.bones['arm-left'].rotation_mode,tuple(r.pose.bones['arm-left'].rotation_quaternion))
r.animation_data.action=a;r.animation_data.action_slot=a.slots[0]
for f in [1,14,26]:
 bpy.context.scene.frame_set(f);bpy.context.view_layer.update();print('FRAME',f,tuple(r.pose.bones['arm-left'].rotation_quaternion),tuple(r.pose.bones['leg-left'].rotation_quaternion))
print('FCURVE',[(c.data_path,c.array_index,[(p.co.x,p.co.y) for p in c.keyframe_points][:2]) for c in list(a.fcurves)[:8]])
