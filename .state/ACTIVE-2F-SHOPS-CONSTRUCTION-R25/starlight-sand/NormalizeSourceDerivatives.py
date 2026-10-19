import bpy,json,pathlib,numpy as np,shutil
from mathutils import Vector,Matrix
p=pathlib.Path(__file__).resolve().parent;s=p/'sources';m=s/'models'
# Independent source-model derivatives only. Originals GLB immutable.
for key in ['financier','cookie','giftbox']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(m/key/(key+'.fbx')))
 if key=='financier':
  obs=[o for o in bpy.data.objects if o.type=='MESH'];o=max(obs,key=lambda x:len(x.data.polygons))
  for z in obs:
   if z!=o:bpy.data.objects.remove(z,do_unlink=True)
  arr=np.array([o.matrix_world@v.co for v in o.data.vertices]);mean=arr.mean(axis=0);val,vec=np.linalg.eigh(np.cov((arr-mean).T));basis=vec[:,np.argsort(val)[::-1]]
  if np.linalg.det(basis)<0:basis[:,1]*=-1
  aligned=(arr-mean)@basis;aligned[:,2]-=aligned[:,2].min()
  for v,a in zip(o.data.vertices,aligned):v.co=Vector(a)
  o.matrix_world=Matrix.Identity(4)
  bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('OwnSmallSourcePastry','DECIMATE');mod.ratio=.5;bpy.ops.object.modifier_apply(modifier=mod.name)
  print('financier PCAextent',aligned.max(axis=0)-aligned.min(axis=0))
 elif key=='cookie':
  for o in bpy.data.objects:
   if o.type=='MESH':
    bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('OwnSmallSourceCookie','DECIMATE');mod.ratio=.18;bpy.ops.object.modifier_apply(modifier=mod.name)
 elif key=='giftbox':
  for o in list(bpy.data.objects):
   if o.type=='MESH' and any(mm and mm.name=='M_Lines' for mm in o.data.materials):bpy.data.objects.remove(o,do_unlink=True)
 dst=m/(key+'-own');dst.mkdir(exist_ok=True)
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(dst/(key+'-own.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
 (dst/'DERIVATIVE-BASIS.json').write_text(json.dumps({'source_original':str(s/({'cookie':'cookieReal','giftbox':'giftbox','financier':'financier'}[key]+'.glb')),'source_original_modified':False,'changes':{'financier':'PCA align scanned pastry, exclude small auxiliary source piece,3000face own derivative','cookie':'own4320face complete source-cookie derivative','giftbox':'remove source ribbon only for current source unbowed cartons'}[key],'does_not_establish_exact_brand_SKU':True},indent=2))
# Shared actual downloaded source counter copied to own staging. No Assets write yet.
src=p.parent/'american-trailer/sources/free-models/counter';dst=s/'models/counter';shutil.copytree(src,dst,dirs_exist_ok=True)
src=p.parent/'bnc-east/sources/free-assets/wood_table_001';dst=s/'wood-pbr';shutil.copytree(src,dst,dirs_exist_ok=True)
print('OWN_NORMALIZED_SOURCE_MODELS_READY')
