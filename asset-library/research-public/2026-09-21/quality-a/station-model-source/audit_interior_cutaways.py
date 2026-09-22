import bpy,pathlib,json,math,time
from mathutils import Vector
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched-no3dletters.blend'));s=bpy.context.scene;allowed={'-부산역','Layer0','-부산역_선로상층부','-부산역_선로상층부(주차장)'};obs=[o for o in s.objects if o.type=='MESH' and o.get('source_layer') in allowed];original={o:o.data for o in obs}
for o in s.objects:
 if o.type=='MESH':o.hide_render=o not in obs
s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=16;s.render.resolution_x=1500;s.render.resolution_y=1200;s.render.resolution_percentage=100;s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.35,.4,.45,1);s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.8
bpy.ops.object.light_add(type='SUN');bpy.context.object.rotation_euler=(.4,-.6,-.4);bpy.context.object.data.energy=2.5
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';cam.data.clip_end=5000;target=Vector((80,60,5));cam.location=target+Vector((-170,-300,850));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=340
framePoints=[v.co for o in obs if o.get('source_layer')!='Layer0' for v in o.data.vertices];q=cam.rotation_euler.to_quaternion().inverted();vp=[q@(v-target) for v in framePoints];xmin,xmax=min(v.x for v in vp),max(v.x for v in vp);ymin,ymax=min(v.y for v in vp),max(v.y for v in vp);cam.location+=cam.rotation_euler.to_quaternion()@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0));cam.data.ortho_scale=max(xmax-xmin,(ymax-ymin)*1500/1200)*1.12
rows=[]
def clip(vertices,h):
 out=[]
 for i,a in enumerate(vertices):
  b=vertices[(i+1)%len(vertices)];ina=a[0].z<=h;inb=b[0].z<=h
  if ina:out.append(a)
  if ina!=inb:
   t=(h-a[0].z)/(b[0].z-a[0].z);out.append((a[0].lerp(b[0],t),a[1].lerp(b[1],t)))
 return out
for height in [4,10,16]:
 t0=time.time();layers={};total=0
 for o,old in original.items():
  verts=[];faces=[];mats=[];uvs=[];smooth=[];oldUV=old.uv_layers.active
  for p in old.polygons:
   vv=[(old.vertices[old.loops[li].vertex_index].co,oldUV.data[li].uv if oldUV else Vector((0,0))) for li in p.loop_indices]
   if min(v[0].z for v in vv)>height:continue
   poly=clip(vv,height)
   if len(poly)<3:continue
   offset=len(verts);verts.extend(tuple(v[0]) for v in poly)
   for k in range(1,len(poly)-1):
    ids=[0,k,k+1];faces.append(tuple(offset+i for i in ids));mats.append(p.material_index);smooth.append(p.use_smooth)
    for i in ids:uvs.extend(poly[i][1])
  me=bpy.data.meshes.new('AUDIT_CUT_'+str(height));me.from_pydata(verts,[],faces);me.update()
  for m in old.materials:me.materials.append(m)
  me.polygons.foreach_set('material_index',mats);me.polygons.foreach_set('use_smooth',smooth);uv=me.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',uvs);o.data=me;total+=len(faces);layers[o['source_layer']]=layers.get(o['source_layer'],0)+len(faces)
 out=D/('station-cutaway-z'+str(height)+'m.png');s.render.filepath=str(out);bpy.ops.render.render(write_still=True);rows.append({'cutSourceZMetres':height,'preview':str(out),'trianglesBelowPlane':total,'layersTriangles':layers,'seconds':time.time()-t0,'method':'Exact pertriangle z-plane clipping with UV interpolation; no fabricated cut caps or new floors. Geometry only exists for audit render.'})
 for o,old in original.items():
  temporary=o.data;o.data=old;bpy.data.meshes.remove(temporary)
 (D/'interior-cutaway-render-receipt.json').write_text(json.dumps({'sourceBlend':'station-batched-no3dletters.blend','matchedCamera':{'target':list(target),'orthographicScaleMetres':cam.data.ortho_scale,'direction':[-170,-300,850]},'rows':rows,'AssetsChanges':0,'sceneSaved':False},indent=2))
 print('CUT_DONE',height,flush=True)
