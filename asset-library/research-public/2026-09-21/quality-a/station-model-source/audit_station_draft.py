import bpy,pathlib,json,hashlib
from collections import defaultdict
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched.blend'))
obs=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('source_layer')=='-부산역'];stats=defaultdict(lambda:{'area':0,'count':0,'min':[1e9]*3,'max':[-1e9]*3});glass=[]
for o in obs:
 for p in o.data.polygons:
  mat=o.data.materials[p.material_index];d=stats[mat.name];d['area']+=p.area;d['count']+=1
  for i in p.vertices:
   v=o.data.vertices[i].co
   for k in range(3):d['min'][k]=min(d['min'][k],v[k]);d['max'][k]=max(d['max'][k],v[k])
  if abs(p.normal.z)<.05 and ('유리' in mat.name or '창' in mat.name):
   glass.append({'material':mat.name,'center':list(p.center),'normal':list(p.normal),'area':p.area})
report={'materialStats':dict(sorted(stats.items(),key=lambda x:-x[1]['area'])),'glassVerticalFaces':glass,'materialSlotsDrawUpperEstimate':sum(len(o.data.materials) for o in obs),'originalImages':len(bpy.data.images),'packedImages':sum(i.packed_file is not None for i in bpy.data.images)};(D/'station-material-and-facade-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print('mainmaterialslots',report['materialSlotsDrawUpperEstimate'],'images',report['packedImages'])
# Roundtrip actual segmented export: source axis/metre invariant, no scene mutation.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.ops.import_scene.fbx(filepath=str(D/'OfficialBusanStation_MainShell.fbx'));bpy.context.view_layer.update();p=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices];bounds={'min':[min(v[k] for v in p) for k in range(3)],'max':[max(v[k] for v in p) for k in range(3)]};exp=next(e for e in json.loads((D/'station-draft-export-receipt.json').read_text())['exports'] if e['segment']=='MainShell')['bounds'];err=max(abs(bounds[k][i]-exp[k][i]) for k in bounds for i in range(3));assert err<.001;(D/'station-fbx-roundtrip.json').write_text(json.dumps({'surface':'Blender FBX import only; Unity pending','bounds':bounds,'maxErrorMetres':err,'pass':True},indent=2));print('roundtrip',err)
