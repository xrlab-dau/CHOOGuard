import bpy,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;A=P/'sources/construction';bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(A/'modern_shelf/ModernShelfOwn.fbx'))
rows=[]
for ob in bpy.context.scene.objects:
 if ob.type!='MESH':continue
 adj=[[] for v in ob.data.vertices]
 for e in ob.data.edges:
  a,b=e.vertices;adj[a].append(b);adj[b].append(a)
 seen=set()
 for i in range(len(adj)):
  if i in seen:continue
  todo=[i];seen.add(i);ids=[]
  while todo:
   j=todo.pop();ids.append(j)
   for k in adj[j]:
    if k not in seen:seen.add(k);todo.append(k)
  vs=[ob.matrix_world@ob.data.vertices[j].co for j in ids];lo=[min(v[k] for v in vs) for k in range(3)];hi=[max(v[k] for v in vs) for k in range(3)];rows.append({'verts':len(ids),'lo':lo,'hi':hi,'size':[hi[k]-lo[k] for k in range(3)]})
(P/'SOURCE-SHELF-PART-SURFACES.json').write_text(json.dumps(rows,indent=2))
print('SHELF_PARTS',json.dumps(rows))
