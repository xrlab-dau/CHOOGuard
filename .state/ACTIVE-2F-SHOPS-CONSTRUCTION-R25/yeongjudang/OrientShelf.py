import bpy,math
from pathlib import Path
from mathutils import Matrix
P=Path(__file__).resolve().parent;A=P/'sources/construction'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(A/'modern_shelf/modern_shelf.fbx'))
rot=Matrix.Rotation(math.pi/2,4,'Z')
for ob in list(bpy.context.scene.objects):
 if ob.parent is None:ob.matrix_world=rot@ob.matrix_world
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(A/'modern_shelf/ModernShelfOwn.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
