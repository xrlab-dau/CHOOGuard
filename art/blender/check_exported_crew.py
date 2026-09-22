import bpy,pathlib,json
ROOT=pathlib.Path(__file__).resolve().parents[2]
results=[]
for name in ['CrewOperations','CrewFire','CrewMedical']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 for action in list(bpy.data.actions):bpy.data.actions.remove(action)
 bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/ChooGuard/Art/ResponseSet'/(name+'.fbx')))
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');walk=next(a for a in bpy.data.actions if 'walk' in a.name.lower());rig.animation_data.action=walk;rig.animation_data.action_slot=walk.slots[0]
 poses=[]
 for frame in [1,14,26]:
  bpy.context.scene.frame_set(frame);bpy.context.view_layer.update();poses.append({'frame':frame,'armLeftQuaternion':list(rig.pose.bones['arm-left'].rotation_quaternion),'legLeftQuaternion':list(rig.pose.bones['leg-left'].rotation_quaternion),'legMatrix':[list(row) for row in rig.pose.bones['leg-left'].matrix]})
 assert poses[0]['legMatrix']!=poses[1]['legMatrix'],name+' exported WALK is static'
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 results.append({'source':name+'.fbx reimport','blender':bpy.app.version_string,'actions':[a.name for a in bpy.data.actions],'poses':poses,'meshCount':len(meshes),'vertices':sum(len(o.data.vertices) for o in meshes),'armatureModifiers':sum(m.type=='ARMATURE' for o in meshes for m in o.modifiers)})
(ROOT/'art/blender/exported-crew-receipt.json').write_text(json.dumps({'scope':'All three exported rigs change at actual WALK frames; Unity playback remains unverified','results':results},indent=2));print(json.dumps([{'source':r['source'],'meshCount':r['meshCount'],'vertices':r['vertices'],'motion':'observed'} for r in results]))
