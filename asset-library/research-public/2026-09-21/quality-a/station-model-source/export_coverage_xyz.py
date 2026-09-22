import bpy,pathlib,json,array,hashlib
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched.blend'));obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];cov=D/'coverage-source';rows=[]
for i,layer in enumerate(sorted({o['source_layer'] for o in obs}-{'나무','-기차'})):
 coords=array.array('f');count=0;zmin=1e9;zmax=-1e9
 for o in obs:
  if o['source_layer']!=layer:continue
  for p in o.data.polygons:
   if p.normal.z<.8 or p.area<.01:continue
   vv=[o.data.vertices[k].co for k in p.vertices];area=abs((vv[1].x-vv[0].x)*(vv[2].y-vv[0].y)-(vv[1].y-vv[0].y)*(vv[2].x-vv[0].x))/2
   if area<.01:continue
   for v in vv:coords.extend(v);zmin=min(zmin,v.z);zmax=max(zmax,v.z)
   count+=1
 path=cov/('layer_%02d.xyz.f32'%i);path.write_bytes(coords.tobytes());rows.append({'layer':layer,'file':str(path),'triangles':count,'format':'little-endian float32 sourceXYZ triples; Zup metres','zRange':[zmin,zmax],'suggestedRole':'ground_source_road' if any(w in layer for w in ['도로','콘타']) else 'building_projection' if layer in {'-부산역','-좌측 건물','-우측 건물','-유라시아 건물'} else 'mixed_review_required'})
(cov/'index-xyz.json').write_text(json.dumps({'sourceBlendSha256':hashlib.sha256((D/'station-batched.blend').read_bytes()).hexdigest(),'registration':str(D/'station-rigid-registration.json'),'layers':rows},ensure_ascii=False,indent=2));print('XYZ',len(rows),sum(r['triangles'] for r in rows))
