import bpy,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;A=P/'sources/construction/modern_shelf';bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(A/'ModernShelfOwn.fbx'))
original=[o for o in bpy.context.scene.objects if o.type=='MESH'];records=[]
def make_part(source,allowed,name,dz=0):
 me=source.data;idx={j:i for i,j in enumerate(sorted(allowed))};verts=[source.matrix_world@me.vertices[j].co+Vector((0,0,dz)) for j in sorted(allowed)];polys=[f for f in me.polygons if all(j in allowed for j in f.vertices)];nm=bpy.data.meshes.new(name);nm.from_pydata(verts,[],[[idx[j] for j in f.vertices] for f in polys]);nm.update();o=bpy.data.objects.new(name,nm);bpy.context.collection.objects.link(o)
 for ma in me.materials:nm.materials.append(ma)
 for nf,f in zip(nm.polygons,polys):nf.material_index=f.material_index
 if me.uv_layers:
  uv=nm.uv_layers.new(name='UVMap')
  for nf,f in zip(nm.polygons,polys):
   for a,b in zip(nf.loop_indices,f.loop_indices):uv.data[a].uv=me.uv_layers.active.data[b].uv
 return o
made=[]
for ob in original:
 adj=[[] for v in ob.data.vertices]
 for e in ob.data.edges:a,b=e.vertices;adj[a].append(b);adj[b].append(a)
 seen=set();low=set();noleg=set()
 for i in range(len(adj)):
  if i in seen:continue
  todo=[i];seen.add(i);ids=[]
  while todo:
   j=todo.pop();ids.append(j)
   for k in adj[j]:
    if k not in seen:seen.add(k);todo.append(k)
  vs=[ob.matrix_world@ob.data.vertices[j].co for j in ids];lo=min(v.z for v in vs);hi=max(v.z for v in vs)
  if lo>=-.9170:noleg.update(ids)
  if lo>=-.9170 and hi<=.3168:low.update(ids)
 nl=make_part(ob,noleg,'SourceNoLegs');made.append(nl)
 for n in range(3):made.append(make_part(ob,low,'SourceLowerCubbyStack_'+str(n),n*1.12633))
 records.append({'source':ob.name,'whole_vertices':len(ob.data.vertices),'no_leg_vertices':len(noleg),'lower_cubby_vertices':len(low),'stack_step_source':1.12633,'method':'retain original connected source boards/dividers, omit legs; repeat physical lower cubby three times; original source triangles and UV retained'})
for o in bpy.context.scene.objects:o.select_set(False)
for o in made:
 if o.name.startswith('SourceNoLegs'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(A/'ShelfNoLegsOwn.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
for o in bpy.context.scene.objects:o.select_set(False)
for o in made:
 if o.name.startswith('SourceLowerCubbyStack'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(A/'ShelfThreeCubbiesOwn.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
(P/'SOURCE-SHELF-PHOTO-ADAPTATION.json').write_text(json.dumps(records,indent=2))
