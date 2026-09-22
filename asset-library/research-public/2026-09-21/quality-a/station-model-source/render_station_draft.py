import bpy,pathlib,json,math,hashlib
from mathutils import Vector
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched.blend'));s=bpy.context.scene;manifest=json.loads((D/'station-batched-manifest.json').read_text());original=[o for o in s.objects if o.type=='MESH'];stationlayers={'-부산역','-부산역_출구지붕','-부산역_선로상층부','-부산역_선로상층부(주차장)'}
s.render.engine='BLENDER_EEVEE_NEXT';s.render.resolution_x=1440;s.render.resolution_y=900;s.render.resolution_percentage=100;s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.35,.4,.45,1);s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.7;s.render.image_settings.file_format='PNG'
bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.rotation_euler=(.4,-.6,-.4);sun.data.energy=2.5
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.clip_end=5000
# Source authored first camera gives an immediate truthful material/exterior comparison.
cameras=json.loads((D/'station-source-cameras.json').read_text());camdata=cameras[0];eye,target,up=camdata['orientation'];cam.location=eye;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='PERSP';cam.data.angle_y=math.radians(camdata['fov'] or 50)
s.render.filepath=str(D/'station-source-camera-preview.png');bpy.ops.render.render(write_still=True)
# Isolate the four station building/roof layers without adjoining city and source helpers.
selected=[o for o in original if o.get('source_layer') in stationlayers]
for o in original:o.hide_render=o not in selected
pts=[o.matrix_world@v.co for o in selected for v in o.data.vertices];lo=Vector(tuple(min(v[k] for v in pts) for k in range(3)));hi=Vector(tuple(max(v[k] for v in pts) for k in range(3)));target=(lo+hi)/2
cam.data.type='ORTHO';cam.location=target+Vector((-250,-450,320));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();q=cam.rotation_euler.to_quaternion().inverted();vp=[q@(v-target) for v in pts];xmin,xmax=min(v.x for v in vp),max(v.x for v in vp);ymin,ymax=min(v.y for v in vp),max(v.y for v in vp);cam.location+=cam.rotation_euler.to_quaternion()@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0));cam.data.ortho_scale=max(xmax-xmin,(ymax-ymin)*1440/900)*1.12
s.render.filepath=str(D/'station-isolated-overview.png');bpy.ops.render.render(write_still=True)
# Draw actual horizontal face area histogram to audit authored slab elevations.
hist={}
for o in selected:
 for p in o.data.polygons:
  if abs(p.normal.z)>.98:
   z=round(p.center.z/.25)*.25;hist[z]=hist.get(z,0)+p.area
bpy.ops.object.select_all(action='DESELECT')
for o in selected:o.select_set(True)
segments={'MainShell':{'-부산역'},'ExitRoofs':{'-부산역_출구지붕'},'PlatformsParking':{'-부산역_선로상층부','-부산역_선로상층부(주차장)'},'ContextPlaza':{'-대형도로','-유라시아 도로','-좌측 콘타','-우측 콘타','-길건너 콘타'},'ContextNeighbours':{'-유라시아 건물','-좌측 건물','-우측 건물'}}
exports=[]
for seg,layers in segments.items():
 bpy.ops.object.select_all(action='DESELECT');obs=[o for o in original if o.get('source_layer') in layers]
 if not obs:continue
 for o in obs:o.select_set(True)
 fbx=D/('OfficialBusanStation_'+seg+'.fbx');bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)
 p=[v.co for o in obs for v in o.data.vertices];exports.append({'segment':seg,'file':str(fbx),'sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'bytes':fbx.stat().st_size,'layers':sorted(layers),'meshes':len(obs),'triangles':sum(len(o.data.polygons) for o in obs),'bounds':{'min':[min(v[k] for v in p) for k in range(3)],'max':[max(v[k] for v in p) for k in range(3)]}})
 (D/'station-segment-export-progress.json').write_text(json.dumps(exports,ensure_ascii=False,indent=2))
receipt={'status':'DRAFT_SOURCE_LAYERS_NOT_NATIVE_REGISTERED','exports':exports,'layerSelection':sorted(stationlayers),'meshes':len(selected),'vertices':sum(len(o.data.vertices) for o in selected),'triangles':sum(len(o.data.polygons) for o in selected),'boundsSourceMetres':{'min':list(lo),'max':list(hi),'size':list(hi-lo)},'horizontalFaceAreaByElevationQuarterMetre':sorted(hist.items(),key=lambda x:-x[1])[:40],'registration':'Not georeferenced. Preserve SDK metre scale; fit rigid yaw/translation from >=3 facade/rail landmarks against OSM, then report residuals before native placement. No anisotropic or arbitrary height scale.','floorPolicy':'Existing gameplay0/5/10m anchors are authored; source elevations must be compared before visual shell replacement. Do not silently relocate gameplay floors.','worldText':'Original branding/sign textures may exist; draft is not yet publication compliant.'}
(D/'station-draft-export-receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2));print(receipt['boundsSourceMetres'],flush=True)
