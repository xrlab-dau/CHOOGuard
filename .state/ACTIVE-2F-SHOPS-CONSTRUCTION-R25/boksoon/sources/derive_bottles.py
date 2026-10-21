import bpy,json
from pathlib import Path
from mathutils import Vector
p=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/boksoon/sources/models')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/'fridge/CommercialRefrigerator.glb'))
o=bpy.data.objects['ChampagneBottles'];mesh=o.data
links=[[]for v in mesh.vertices]
for e in mesh.edges:
 a,b=e.vertices;links[a].append(b);links[b].append(a)
seen=set();components=[]
for i in range(len(links)):
 if i in seen:continue
 todo=[i];seen.add(i);indices=[]
 while todo:
  a=todo.pop();indices.append(a)
  for b in links[a]:
   if b not in seen:seen.add(b);todo.append(b)
 vs=[o.matrix_world@mesh.vertices[k].co for k in indices]
 mn=Vector(tuple(min(v[k]for v in vs)for k in range(3)));mx=Vector(tuple(max(v[k]for v in vs)for k in range(3)))
 components.append({'ids':indices,'min':list(mn),'max':list(mx)})
(p/'bottle-components.json').write_text(json.dumps([{k:v for k,v in c.items()if k!='ids'}|{'n':len(c['ids'])}for c in components],indent=2))
print('components',len(components));print(json.dumps([{k:v for k,v in c.items()if k!='ids'}|{'n':len(c['ids'])}for c in components[:15]]))
# Find complete first champagne bottle via lowest shelf/leftmost column; each material disconnected but same X/Y center.
large=[c for c in components if len(c['ids'])>100 and c['min'][2]<.43];base=min(large,key=lambda c:c['min'][0]);cx=(base['min'][0]+base['max'][0])/2;cy=(base['min'][1]+base['max'][1])/2
selected=[]
for c in components:
 x=(c['min'][0]+c['max'][0])/2;y=(c['min'][1]+c['max'][1])/2
 if abs(x-cx)<.012 and abs(y-cy)<.012 and c['min'][2]<.70:selected+=c['ids']
selected=set(selected);polys=[f for f in mesh.polygons if all(v in selected for v in f.vertices)]
used=sorted(set(v for f in polys for v in f.vertices));imap={v:i for i,v in enumerate(used)};verts=[o.matrix_world@mesh.vertices[v].co for v in used]
mn=Vector(tuple(min(v[k]for v in verts)for k in range(3)));mx=Vector(tuple(max(v[k]for v in verts)for k in range(3)));pivot=Vector(((mn.x+mx.x)/2,(mn.y+mx.y)/2,mn.z))
m=bpy.data.meshes.new('FreeChampagneBottle_Derived');m.from_pydata([v-pivot for v in verts],[],[[imap[v]for v in f.vertices]for f in polys]);m.update()
for mat in mesh.materials:m.materials.append(mat)
for a,b in zip(m.polygons,polys):a.material_index=b.material_index
bpy.ops.object.select_all(action='DESELECT');new=bpy.data.objects.new('FreeChampagneBottle_Derived',m);bpy.context.collection.objects.link(new);new.select_set(True);bpy.context.view_layer.objects.active=new
bpy.ops.export_scene.fbx(filepath=str(p/'fridge/ChampagneBottle_SOURCE_DERIVATIVE.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
(p/'fridge/BOTTLE-DERIVATION.json').write_text(json.dumps({'source':'CommercialRefrigerator.glb','source_credit':'Sean Thomas (foon), Khronos, CC-BY4.0','source_unchanged':True,'selected_center':[cx,cy],'source_bounds':[list(mn),list(mx)],'faces':len(polys),'vertices':len(verts),'qualification':'One actual downloaded champagne bottle mesh isolated; independently fitted opaque makgeolli/clear neck foil URP; exact Boksoon bottle SKU not asserted'},indent=2))
