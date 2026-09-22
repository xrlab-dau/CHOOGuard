import bpy,pathlib,json,math,hashlib
from mathutils import Vector
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'ktx-conversion-draft.blend'));s=bpy.context.scene
removed=[]
for name in ['G-Object','G-Object.001','G-Object.002','G-Object.009']:
 o=bpy.data.objects.get(name);assert o and o.type=='MESH' and len(o.data.polygons)==2 and len(o.data.vertices)==4
 removed.append({'object':name,'materials':[m.name for m in o.data.materials],'triangles':2});bpy.data.objects.remove(o,do_unlink=True)
(D/'ktx-reference-removal-receipt.json').write_text(json.dumps({'removed':removed,'method':'Exact reviewed four original planar reference objects; three texture blueprints and one17x13m horizontal helper plane; no vehicle body removal','sourcePreserved':True},ensure_ascii=False,indent=2))
# Evaluate actual placed instances, excluding source collection definitions that are not visible.
info=[{'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,'hideRender':o.hide_render,'instance':o.instance_type,'collection':o.instance_collection.name if o.instance_collection else None} for o in s.objects if o.parent is None];(D/'ktx-roots.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
pts=[];count=0
for i in bpy.context.evaluated_depsgraph_get().object_instances:
 o=i.object
 if o.type=='MESH' and not o.hide_render:
  count+=1;pts.extend(i.matrix_world@v.co for v in o.data.vertices)
lo=Vector(tuple(min(v[k] for v in pts) for k in range(3)));hi=Vector(tuple(max(v[k] for v in pts) for k in range(3)));target=(lo+hi)/2
s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
s.render.engine='BLENDER_EEVEE_NEXT';s.render.resolution_x=1500;s.render.resolution_y=850;s.render.resolution_percentage=100;s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.35,.4,.45,1);s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6
bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.rotation_euler=(.4,-.6,-.4);sun.data.energy=2.5
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';cam.location=target+Vector((-50,-90,50));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();q=cam.rotation_euler.to_quaternion().inverted();vp=[q@(v-target) for v in pts];xmin,xmax=min(v.x for v in vp),max(v.x for v in vp);ymin,ymax=min(v.y for v in vp),max(v.y for v in vp);cam.location+=cam.rotation_euler.to_quaternion()@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0));cam.data.ortho_scale=max(xmax-xmin,(ymax-ymin)*1500/850)*1.13
s.render.filepath=str(D/'ktx-clean-source-material-preview.png');bpy.ops.render.render(write_still=True)
# Realize only actual scene instances for interchange, without modifying source file.
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.view_layer.objects:
 if o.type in {'MESH','EMPTY'}:o.select_set(True)
bpy.context.view_layer.objects.active=next(o for o in bpy.context.view_layer.objects if o.select_get());bpy.ops.object.duplicates_make_real(use_base_parent=True,use_hierarchy=True)
bpy.ops.wm.save_as_mainfile(filepath=str(D/'ktx-clean-conversion-draft.blend'))
fbx=D/'OfficialKtxCleanDraft.fbx';bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)
manifest={'status':'DRAFT_NOT_UNITY_APPLIED','identity':'KTX rolling stock, not station','sourceVersion':'22.0.354','sourceUnits':'SDK importer outputs metre-scale geometry; metric dimensions not surveyed','visibleEvaluatedMeshes':count,'bounds':{'min':list(lo),'max':list(hi),'size':list(hi-lo)},'fbxSha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'fbxBytes':fbx.stat().st_size,'sourceMaterialsPreserved':True,'nativeValidation':False,'worldTextNotice':'Original train has branding and texture text; review/removal needed to meet project no-world-text requirement before publication.'}
(D/'ktx-clean-conversion-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2));print(manifest)
