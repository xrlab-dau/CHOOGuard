from pathlib import Path
prefix=Path('asset-library/research-public/2026-09-21/quality-a/station-model-source/convert_station_batched.py').read_text().split('# Build reusable')[0];exec(prefix)
from mathutils.kdtree import KDTree
path='ROOT/g56/g0/g0/g12';ent=model.entities;T=Matrix.Identity(4)
for part in path.split('/')[1:]:
 g=list(ent.groups)[int(part[1:])];T=T@Matrix(g.transform);ent=g.entities
me,_=imp.write_mesh_data(ent,'EXACT_SOURCE_LETTERS');triangles=[]
for p in me.polygons:
 v=[T@me.vertices[i].co for i in p.vertices];triangles.append((v,me.materials[p.material_index].name))
kd=KDTree(len(triangles))
for i,(vv,_) in enumerate(triangles):kd.insert(sum(vv,Vector())/3,i)
kd.balance();points=[v for vs,_ in triangles for v in vs];lo=Vector(tuple(min(v[k] for v in points) for k in range(3)));hi=Vector(tuple(max(v[k] for v in points) for k in range(3)))
bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched.blend'));removed=[];matched=set()
for o in bpy.context.scene.objects:
 if o.type!='MESH' or o.get('source_layer')!='-부산역':continue
 old=o.data;keep=[];drop=[]
 for p in old.polygons:
  c=p.center
  if all(lo[k]-.001<=c[k]<=hi[k]+.001 for k in range(3)):
   _,idx,dist=kd.find(c)
   if dist<.0003 and old.materials[p.material_index].name==triangles[idx][1]:
    vv=[old.vertices[i].co for i in p.vertices];sv=triangles[idx][0]
    if all(min((v-w).length for w in sv)<.0003 for v in vv):drop.append(p.index);matched.add(idx);continue
  keep.append(p.index)
 if not drop:continue
 new=bpy.data.meshes.new(old.name+'_No3DLetters');new.from_pydata([v.co for v in old.vertices],[],[list(old.polygons[i].vertices) for i in keep]);new.update()
 for m in old.materials:new.materials.append(m)
 new.polygons.foreach_set('material_index',[old.polygons[i].material_index for i in keep]);new.polygons.foreach_set('use_smooth',[old.polygons[i].use_smooth for i in keep]);uv=new.uv_layers.new(name='UVMap');values=[]
 for i in keep:
  for li in old.polygons[i].loop_indices:values.extend(old.uv_layers.active.data[li].uv)
 uv.data.foreach_set('uv',values);o.data=new;removed.append({'chunk':o.name,'trianglesRemoved':len(drop)})
assert len(matched)==len(triangles),(len(matched),len(triangles));assert sum(r['trianglesRemoved'] for r in removed)==len(triangles)
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(D/'station-batched-no3dletters.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
 if o.type=='MESH' and o.get('source_layer')=='-부산역':o.select_set(True)
fbx=D/'OfficialBusanStation_MainShell.fbx';bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)
r={'status':'EXACT_ORIGINAL_3D_LETTER_GROUP_REMOVED','sourceGroupPath':path,'sourceGroupName':'unnamed original SKP group; stable source tree indices under archivedSHA','inspectionPreview':str(D/'sign-group-0.png'),'removedGeometry':'Korean BusanStation name plus English and Chinese letters, actual extruded source geometry','retained':'g13 supporting frame, original facade, texture-painted signs and all other source geometry','method':'Source original group triangles matched by all3worldvertices + material within0.3mm, not bounding-box deletion','sourceLetterTriangles':len(triangles),'matchedTriangles':len(matched),'affectedChunks':removed,'fbxSha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'fbxBytes':fbx.stat().st_size,'blend':str(D/'station-batched-no3dletters.blend'),'scopeLimit':'Only directly identified3Dletter group removed; font/textcomponents never generated. Other source painted signage retained.'};(D/'source-3d-letter-removal-receipt.json').write_text(json.dumps(r,ensure_ascii=False,indent=2));print(r,flush=True)
