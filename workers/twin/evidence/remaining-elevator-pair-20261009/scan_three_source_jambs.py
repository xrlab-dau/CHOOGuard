import bpy,json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path('/Users/um-yunsang/CHOOGuard');out=root/'workers/twin/evidence/remaining-elevator-pair-20261009'
p=json.loads((root/'workers/twin/output/remaining-native-doors-20261009/sliding-leaf-dependencies.json').read_text());R=Matrix(p['source_to_current_world_rotation']);T=Vector(p['source_to_current_world_translation'])
children={}
for obj in bpy.data.objects:
 children.setdefault(obj.parent,[]).append(obj)
ids=['5089b725dad5e3c970223f5a','a1f9d8b72e353344d4136264','bf9da804a36b4cc7dfda9b91'];lookup={o.get('source_id'):o for o in bpy.data.objects if o.get('source_id') in ids};records=[]
def bounds(ps):return {'min':[min(v[k]for v in ps)for k in range(3)],'max':[max(v[k]for v in ps)for k in range(3)]} if ps else None
def descendants(o):
 for c in children.get(o,[]):
  yield c
  yield from descendants(c)
for sid in ids:
 leaf=lookup[sid];jamb=leaf.parent;inv=jamb.matrix_world.inverted();linv=leaf.matrix_world.inverted();meshes=[];closedpoints=[];jambpoints=[]
 close=jamb.matrix_world@Matrix.Translation(leaf.matrix_local.translation)
 for o in descendants(jamb):
  if o.type!='MESH':continue
  anc=o
  while anc is not None and anc.parent!=jamb:anc=anc.parent
  isleaf=anc==leaf
  pp=[inv@(o.matrix_world@v.co)for v in o.data.vertices]
  ww=[R@(o.matrix_world@v.co)+T for v in o.data.vertices]
  if isleaf:
   closedpoints +=[R@(close@(linv@(o.matrix_world@v.co)))+T for v in o.data.vertices]
  else:jambpoints += pp
  meshes.append({'source_id':o.get('source_id'),'source_path':o.get('source_path'),'name':o.name,'is_selected_leaf':isleaf,'direct_child_source_id':anc.get('source_id') if anc else None,'direct_child_name':anc.name if anc else None,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),'jamb_local_bounds':bounds(pp),'world_bounds':bounds(ww),'materials':[m.name if m else None for m in o.data.materials]})
 origin=R@jamb.matrix_world.translation+T; axes=[R@jamb.matrix_world.to_3x3().col[k]for k in range(3)]
 records.append({'source_id':sid,'jamb_source_id':jamb.get('source_id'),'jamb_source_path':jamb.get('source_path'),'jamb_source_world_matrix':[list(r)for r in jamb.matrix_world],'jamb_current_origin':list(origin),'jamb_current_axes':[list(a)for a in axes],'leaf_local_matrix':[list(r)for r in leaf.matrix_local],'closed_leaf_world_bounds':bounds(closedpoints),'non_selected_leaf_descendants_local_bounds':bounds(jambpoints),'mesh_records':meshes})
(out/'three-source-jambs.json').write_text(json.dumps({'source_to_current_rotation':[list(r)for r in R],'source_to_current_translation':list(T),'records':records},ensure_ascii=False,indent=2)+'\n')
print('THREE_SOURCE_JAMBS',[(r['source_id'],len(r['mesh_records']))for r in records])
