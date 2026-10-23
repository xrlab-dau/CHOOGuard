import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
B=Path(__file__).resolve().parent;P=B/'sources/tongs';bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(P/'tongs-original.glb'))
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
info=[]
for o in objs:
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 # Bake importer hierarchy transform to independent source geometry.
 o.data.transform(o.matrix_world);o.matrix_world=Matrix.Identity(4);o.parent=None
 if len(o.data.polygons)>3000:
  m=o.modifiers.new('Own source silhouette retaining derivative','DECIMATE');m.ratio=min(1,5000/len(o.data.polygons));bpy.ops.object.modifier_apply(modifier=m.name)
 for poly in o.data.polygons:poly.use_smooth=True
 info.append({'name':o.name,'materials':[m.name for m in o.data.materials],'polygons':len(o.data.polygons)})
pts=[o.matrix_world@v.co for o in objs for v in o.data.vertices];mn=Vector([min(v[i] for v in pts) for i in range(3)]);mx=Vector([max(v[i] for v in pts) for i in range(3)]);size=mx-mn;axis=max(range(3),key=lambda i:size[i])
# Canonical Blender Z is the actual utensil length; imported Unity Y becomes its upright length.
rot=Matrix.Identity(3)
if axis==0:rot=Matrix.Rotation(math.pi/2,3,'Y')
if axis==1:rot=Matrix.Rotation(math.pi/2,3,'X')
for o in objs:
 for v in o.data.vertices:v.co=rot@(v.co-(mn+mx)/2)
pts=[v.co for o in objs for v in o.data.vertices];mn=Vector([min(v[i] for v in pts) for i in range(3)]);mx=Vector([max(v[i] for v in pts) for i in range(3)]);size=mx-mn
# Actual-source silhouette fitted to a source-photo MODEL length .34m, uniform scale.
s=.34/size.z
for o in objs:
 for v in o.data.vertices:v.co*=s
for o in list(bpy.data.objects):
 if o.type not in {'MESH','EMPTY'}:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(P/'Tongs-R26-Unity.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='STRIP',embed_textures=False)
(P/'UNITY-MATERIAL-SLOTS.json').write_text(json.dumps({'original_glb_unchanged':True,'derivative':'Tongs-R26-Unity.fbx','source_shape':'Actual scalloped stainless food tongs; own independent smooth derivative','unity_model_y_length_m':.34,'uniform_source_scale':s,'source_bounds':list(size),'objects':info,'slots':sorted({m.name for o in objs for m in o.data.materials})},indent=2));print('READY_TONGS',info)
