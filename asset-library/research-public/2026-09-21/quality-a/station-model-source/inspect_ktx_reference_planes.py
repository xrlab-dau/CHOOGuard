import bpy,pathlib,json
from mathutils import Vector
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'ktx-conversion-draft.blend'));rows=[]
for o in bpy.context.view_layer.objects:
 if o.type!='MESH':continue
 if not any(m and 'ktx-1' in m.name for m in o.data.materials):continue
 pts=[o.matrix_world@v.co for v in o.data.vertices];lo=[min(v[k] for v in pts) for k in range(3)];hi=[max(v[k] for v in pts) for k in range(3)]
 rows.append({'name':o.name,'materials':[m.name for m in o.data.materials],'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),'min':lo,'max':hi})
(D/'ktx-reference-plane-audit.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2));print(rows)
