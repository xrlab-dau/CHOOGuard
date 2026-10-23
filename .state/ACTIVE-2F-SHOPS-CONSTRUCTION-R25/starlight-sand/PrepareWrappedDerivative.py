import bpy,pathlib
p=pathlib.Path(__file__).resolve().parent/'sources/models/wrapped-biscuit/wrapped-biscuit.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(p))
for o in bpy.data.objects:
 if o.type=='MESH' and len(o.data.polygons)>2000:
  bpy.context.view_layer.objects.active=o;m=o.modifiers.new('OwnSmallWrappingDerivative','DECIMATE');m.ratio=2000/len(o.data.polygons);bpy.ops.object.modifier_apply(modifier=m.name)
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(p),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
print('OWN_2000FACE_WRAPPER_READY')
