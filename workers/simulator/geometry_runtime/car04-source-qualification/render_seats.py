import sys,json,re,math
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/simulator/geometry_runtime/car04-source-qualification'
sys.path.insert(0,str(OUT));exec((OUT/'source_scan.py').read_text().split('records=[]')[0])
# All original imported objects are used only to construct isolated diagnostic copies.
originals=list(bpy.data.objects);created=[]
textures={}
for meta in (ROOT/'Assets/ChooGuard/Art/Fps/KTX/Textures').glob('*.meta'):
 m=re.search(r'^guid: ([a-f0-9]+)',meta.read_text(),re.M)
 if m:textures[m.group(1)]=meta.with_suffix('')
materials={}
def material(name):
 if name in materials:return materials[name]
 m=bpy.data.materials.new('DIAGNOSTIC_'+name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();surface=n.new('ShaderNodeEmission');surface.inputs['Strength'].default_value=1;out=n.new('ShaderNodeOutputMaterial');m.node_tree.links.new(surface.outputs[0],out.inputs['Surface'])
 path=ROOT/'Assets/ChooGuard/Art/Fps/KTX/Materials'/(name+'.mat')
 guids=re.findall(r'm_Texture: \{fileID: 2800000, guid: ([a-f0-9]+)',path.read_text()) if path.exists() else []
 if guids and guids[0] in textures:
  image=n.new('ShaderNodeTexImage');image.image=bpy.data.images.load(str(textures[guids[0]]),check_existing=True);m.node_tree.links.new(image.outputs['Color'],surface.inputs['Color'])
 else:surface.inputs['Color'].default_value=(.7,.7,.7,1)
 materials[name]=m;return m
for obj in originals:
 if obj.type!='MESH' or not obj.name.startswith('Car04_') or '_Seat_' not in obj.name:continue
 vs=[frame(obj.matrix_world@v.co) for v in obj.data.vertices];obj.data.calc_loop_triangles();tri=[];loops=[]
 for t in obj.data.loop_triangles:
  coords=[vs[i] for i in t.vertices]
  tri.append(tuple(t.vertices));loops.append(tuple(t.loops))
 if not tri:continue
 mesh=bpy.data.meshes.new('Diagnostic_'+obj.name);mesh.from_pydata(vs,[],tri);mesh.update();item=bpy.data.objects.new(obj.name,mesh);bpy.context.collection.objects.link(item);created.append(item)
 for slot in obj.material_slots:mesh.materials.append(material(slot.material.name))
 if obj.data.uv_layers.active:
  uv=mesh.uv_layers.new(name='Source UV')
  for polygon,source_loops in zip(mesh.polygons,loops):
   for li,src in zip(polygon.loop_indices,source_loops):uv.data[li].uv=obj.data.uv_layers.active.data[src].uv
for obj in originals:bpy.data.objects.remove(obj,do_unlink=True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8;scene.render.resolution_x=1900;scene.render.resolution_y=560;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.world=bpy.data.worlds.new('Diagnostic world');scene.world.color=(.1,.1,.1);scene.view_settings.view_transform='Standard'
camera_data=bpy.data.cameras.new('Diagnostic camera');camera=bpy.data.objects.new('Diagnostic camera',camera_data);bpy.context.collection.objects.link(camera);scene.camera=camera;camera_data.type='ORTHO';camera_data.ortho_scale=14.5
for name,pos,target in [('source-seats-overhead',(-7.2,1.4,18),(-7.2,1.4,0)),('source-seats-oblique',(-7.2,-7,10),(-7.2,1.4,.9))]:
 camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
print('Source diagnostic renders completed. Original SHA unchanged:',coverage.digest(source)==before)
