import bpy,json
from pathlib import Path
D=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-1F-LOCKERS-FREE-ASSETS-20261019')
bpy.ops.wm.open_mainfile(filepath=str(D/'downloaded-locker-working.blend'))
out=[]
for name in ['01','01_1']:
 o=bpy.data.objects[name];mesh=o.data
 adj={v.index:set() for v in mesh.vertices}
 for e in mesh.edges:a,b=e.vertices;adj[a].add(b);adj[b].add(a)
 seen=set();groups=[]
 for i in adj:
  if i in seen:continue
  todo=[i];seen.add(i);ids=[]
  while todo:
   v=todo.pop();ids.append(v)
   for n in adj[v]:
    if n not in seen:seen.add(n);todo.append(n)
  pts=[o.matrix_world@mesh.vertices[j].co for j in ids]
  lo=[min(v[k] for v in pts) for k in range(3)];hi=[max(v[k] for v in pts) for k in range(3)]
  groups.append(dict(vertex_indices=ids,min=lo,max=hi,vertices=len(ids),faces=sum(set(p.vertices)<=set(ids) for p in mesh.polygons)))
 out.append(dict(name=name,components=groups))
(D/'SOURCE-MESH-COMPONENTS.json').write_text(json.dumps(out,indent=2))
print(json.dumps([{**g,'components':[{k:v for k,v in c.items() if k!='vertex_indices'} for c in g['components']]} for g in out]))
