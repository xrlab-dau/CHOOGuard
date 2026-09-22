import bpy,pathlib,json,hashlib,array
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched.blend'));obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];extra={'Layer0PlatformsDetails':{'Layer0'},'SiteTrains':{'-기차'},'SiteTrees':{'나무'}};exports=json.loads((D/'station-draft-export-receipt.json').read_text())['exports']
for seg,layers in extra.items():
 bpy.ops.object.select_all(action='DESELECT');selected=[o for o in obs if o.get('source_layer') in layers]
 for o in selected:o.select_set(True)
 fbx=D/('OfficialBusanStation_'+seg+'.fbx');bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)
 p=[v.co for o in selected for v in o.data.vertices];exports.append({'segment':seg,'file':str(fbx),'sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'bytes':fbx.stat().st_size,'layers':sorted(layers),'meshes':len(selected),'triangles':sum(len(o.data.polygons) for o in selected),'bounds':{'min':[min(v[k] for v in p) for k in range(3)],'max':[max(v[k] for v in p) for k in range(3)]}})
(D/'station-fullsite-segments.json').write_text(json.dumps(exports,ensure_ascii=False,indent=2));assert sum(e['meshes'] for e in exports)==len(obs)==51
cov=D/'coverage-source';cov.mkdir(exist_ok=True);indices=[]
for i,layer in enumerate(sorted({o['source_layer'] for o in obs} - {'나무','-기차'})):
 coords=array.array('f');triangles=0
 for o in obs:
  if o['source_layer']!=layer:continue
  for p in o.data.polygons:
   if p.normal.z<.8 or p.area<.01:continue
   assert len(p.vertices)==3
   vv=[o.data.vertices[k].co for k in p.vertices];area=abs((vv[1].x-vv[0].x)*(vv[2].y-vv[0].y)-(vv[1].y-vv[0].y)*(vv[2].x-vv[0].x))/2
   if area<.01:continue
   for v in vv:coords.extend((v.x,v.y))
   triangles+=1
 path=cov/('layer_%02d.f32'%i);path.write_bytes(coords.tobytes());indices.append({'layer':layer,'file':str(path),'triangles':triangles,'format':'little-endian float32 sourceXY triples'})
(cov/'index.json').write_text(json.dumps(indices,ensure_ascii=False,indent=2));print('FULLSITE',len(exports),sum(e['meshes'] for e in exports),'coverage triangles',sum(r['triangles'] for r in indices))
