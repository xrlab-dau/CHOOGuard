import bpy,pathlib,json,hashlib
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched.blend'));layers={o.get('source_layer') for o in bpy.context.scene.objects if o.type=='MESH'};mats={m.name:m for o in bpy.context.scene.objects if o.type=='MESH' and o.get('source_layer') in layers for m in o.data.materials};out=D/'approved-textures';out.mkdir(exist_ok=True);rows=[]
for name,mat in sorted(mats.items()):
 nodes=mat.node_tree.nodes if mat.use_nodes else [];p=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None);imgnode=next((n for n in nodes if n.type=='TEX_IMAGE' and n.image),None);color=list(p.inputs['Base Color'].default_value) if p else list(mat.diffuse_color);image=imgnode.image if imgnode else None;tex='';sha=''
 if image:
  if not image.packed_file:raise RuntimeError('Unpacked texture '+image.name)
  data=bytes(image.packed_file.data);sha=hashlib.sha256(data).hexdigest();ext='.png' if data[:8]==b'\x89PNG\r\n\x1a\n' else '.jpg' if data[:2]==b'\xff\xd8' else '.bin';assert ext!='.bin';path=out/(sha[:20]+ext);path.write_bytes(data);tex='Assets/ChooGuard/Art/OfficialBusanStation/Textures/'+path.name
 rows.append({'sourceName':name,'colorRGBA':color,'colorSpace':'linear values from SDK importer sRGB^2.2','texturePath':tex,'textureSha256':sha,'textureSourceImage':image.name if image else '', 'alpha':float(p.inputs['Alpha'].default_value) if p else color[3],'roughness':float(p.inputs['Roughness'].default_value) if p else .5,'metallic':float(p.inputs['Metallic'].default_value) if p else 0,'sourceTextured':image is not None})
manifest={'schemaVersion':1,'materials':rows,'source':'Original SKP colors/textures; Blender importer defaults for roughness/metallic','segments':['MainShell','ExitRoofs','PlatformsParking','ContextPlaza','ContextNeighbours','Layer0PlatformsDetails','SiteTrains','SiteTrees'],'textureFiles':len(list(out.iterdir())),'noFallbackPolicy':'Unknown source material must fail explicitly; preserve sourceName mapping and texture UV0'};
for m in rows:
 m['sourceUnlinkedColorRGBA']=m['colorRGBA'];m['sourceUnlinkedAlpha']=m['alpha']
 if m['sourceTextured']:m['colorRGBA']=[1,1,1,1];m['alpha']=1;m['bindingNote']='Source image directly drives BSDF color/alpha; Unity white tint preserves appearance.'
(D/'official-station-material-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2));print('materials',len(rows),'textures',manifest['textureFiles'])
