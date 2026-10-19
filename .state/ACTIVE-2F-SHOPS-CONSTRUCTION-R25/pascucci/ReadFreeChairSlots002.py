import bpy,json,pathlib,sys
p=pathlib.Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(p/'sources/modern_arm_chair_01/modern_arm_chair_01.fbx'))
d={'source':'sources/modern_arm_chair_01/modern_arm_chair_01.fbx','purpose':'Source asset material-slot authoring evidence; no Unity scene/native inspection','objects':[{'name':o.name,'slots':[m.name if m else None for m in o.data.materials]} for o in bpy.data.objects if o.type=='MESH']}
(p/'SOURCE-FBX-MATERIAL-SLOTS-002.json').write_text(json.dumps(d,indent=2));print(json.dumps(d))
