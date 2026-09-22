import bpy,sys,pathlib,json
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source'
sys.path.insert(0,str(D/'importer-isolated'))
import addon_utils
addon_utils.enable("sketchup_importer",default_set=True,persistent=False)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
result=bpy.ops.import_scene.skp(filepath=str(D/'ktx-extracted/KTX.skp'))
bpy.context.view_layer.update();obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];p=[o.matrix_world@v.co for o in obs for v in o.data.vertices];lo=[min(v[k] for v in p) for k in range(3)];hi=[max(v[k] for v in p) for k in range(3)]
receipt={'result':list(result),'meshObjects':len(obs),'vertices':sum(len(o.data.vertices) for o in obs),'polygons':sum(len(o.data.polygons) for o in obs),'boundsRawBlender':{'min':lo,'max':hi,'size':[hi[k]-lo[k] for k in range(3)]},'objects':[o.name for o in bpy.context.scene.objects][:150],'materials':[m.name for m in bpy.data.materials],'unitSystem':bpy.context.scene.unit_settings.system,'unitScale':bpy.context.scene.unit_settings.scale_length}
(D/'ktx-geometry-audit.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(D/'ktx-imported.blend'))
print('AUDIT_SUCCESS',json.dumps(receipt)[:500])
