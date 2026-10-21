from pathlib import Path
import json,math,struct,hashlib,numpy as np
P=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/gijang');packet=json.load(open(P/'partplacement.json'));byid={o['id']:o for o in packet['objects']};basis=json.load(open(P/'MODEL-PLACEMENT-BASIS-R25.json'));A,B,C,D=json.load(open(P/'APPROVED-STORE-WORK-REGION.json'))['geometry_qx_minus_qy']['coordinates'][0][:-1]
L=math.dist(B,C);T=np.array(C)-B;T/=L;N=np.array([T[1],-T[0]]);BT=np.array(D)-A;BL=float(np.linalg.norm(BT));BT/=BL;BN=np.array([-BT[1],BT[0]])
q=lambda u,v:(np.array(B)+u*T+v*N).tolist();qb=lambda u,v:(np.array(A)+u*BT+v*BN).tolist();FY=-math.degrees(math.atan2(T[1],T[0]));BY=-math.degrees(math.atan2(BT[1],BT[0]));records=[]
def read_accessor(d,blob,i):
 a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];dtype={5126:'<f4',5125:'<u4',5123:'<u2',5122:'<i2',5121:'u1',5120:'i1'}[a['componentType']];nc={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];st=v.get('byteStride',np.dtype(dtype).itemsize*nc);ofs=v.get('byteOffset',0)+a.get('byteOffset',0)
 out=np.ndarray((a['count'],nc),dtype=dtype,buffer=blob,offset=ofs,strides=(st,np.dtype(dtype).itemsize)).copy()
 if a.get('normalized')and a['componentType']!=5126:out=out.astype(float)/np.iinfo(dtype).max
 return out
shelf=json.load(open(P/'wooden_display_shelves_01_gltf.gltf'));shelfblob=(P/'wooden_display_shelves_01.bin').read_bytes();prim=shelf['meshes'][0]['primitives'][0];v=read_accessor(shelf,shelfblob,prim['attributes']['POSITION']);uv=read_accessor(shelf,shelfblob,prim['attributes']['TEXCOORD_0']);ind=read_accessor(shelf,shelfblob,prim['indices']).flatten().reshape(-1,3)[:,[0,2,1]].flatten();v=v[:,[2,1,0]];lo=v.min(0);hi=v.max(0);v=(v-(lo+hi)/2)/(hi-lo);v*=np.array([3.50,2.30,.36]);v[:,1]+=1.23
records.append({'id':'PH_CC0_REAL_WOODEN_DISPLAY_SHELVES_NO_DRAWERS','kind':'mesh','q':qb(1.86,.25),'y':0,'yaw':BY,'vertices':v.tolist(),'triangles':ind.tolist(),'uv':uv.tolist(),'material':'wood_display_pbr','source_id':'POLYHAVEN_WOODEN_DISPLAY_SHELVES_01','source_note':'Freshdownloadedactualshelfbodyonly;3whitebottomdrawersomittedtomatchopenproductbays;originalUVbevelsretained.'})
# Freshdownloaded CC0 realistic retail machine/components; glTF node transforms retained.
glb=(P/'innerscene-pos.glb').read_bytes();n,_=struct.unpack_from('<II',glb,12);d=json.loads(glb[20:20+n]);blen,btype=struct.unpack_from('<II',glb,20+n);blob=glb[28+n:28+n+blen]
def mat4(node):
 if 'matrix'in node:return np.array(node['matrix']).reshape(4,4).T
 m=np.eye(4);s=np.array(node.get('scale',[1,1,1]));x,y,z,w=node.get('rotation',[0,0,0,1]);m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(s);m[:3,3]=node.get('translation',[0,0,0]);return m
parent={c:i for i,nd in enumerate(d['nodes'])for c in nd.get('children',[])}
def world(i):return (world(parent[i])if i in parent else np.eye(4))@mat4(d['nodes'][i])
def components(ids,prefix,at,bottom,size,yaw,material_prefix='RET'):
 parts=[]
 for i in ids:
  node=d['nodes'][i]
  for pi,pr in enumerate(d['meshes'][node['mesh']]['primitives']):
   vv=read_accessor(d,blob,pr['attributes']['POSITION']);vv=(world(i)@np.column_stack([vv,np.ones(len(vv))]).T).T[:,:3];vv[:,2]*=-1
   ii=read_accessor(d,blob,pr['indices']).flatten().reshape(-1,3)[:,[0,2,1]].flatten()
   uu=read_accessor(d,blob,pr['attributes']['TEXCOORD_0'])if'TEXCOORD_0'in pr['attributes']else np.zeros((len(vv),2))
   parts.append([i,pi,node.get('name'),vv,ii,uu,pr.get('material',0)])
 allv=np.concatenate([x[3]for x in parts]);lo=allv.min(0);hi=allv.max(0);center=(lo+hi)/2
 for i,pi,name,vv,ii,uu,ma in parts:
  vv=(vv-center)/np.maximum(hi-lo,1e-7)*np.array(size);vv[:,1]+=bottom+size[1]/2
  records.append({'id':prefix+'_'+str(i)+'_'+str(pi),'kind':'mesh','q':at,'y':0,'yaw':yaw,'vertices':vv.tolist(),'triangles':ii.tolist(),'uv':uu.tolist(),'material':material_prefix+str(ma),'source_id':'INNERSCENE_CC0_RETAIL_CHECKOUT_POS','source_mesh_name':name,'qualification':'FREE_3D_ASSET_SOURCE_GEOMETRY_FITTED_WITHIN_EXISTING_OWN_FIXTURE_ENVELOPE_MODEL'})
components([2,3,8,11,12,13,14,35,37,38],'CC0_REAL_RETAIL_COUNTER',q(1.37,.88),.03,[2.30,.90,.47],FY)
components([10,15,16,17,20,21,22,23,25],'CC0_REAL_POS_MONITOR',q(.44,1.43),.98,[.32,.34,.19],FY)
components([5,24,27,28,29,30,31,32,33],'CC0_REAL_PAYMENT_MACHINE',q(.72,1.11),.965,[.10,.13,.14],FY)
# Five source-observed boxed gifts now actual downloaded CC0 modeled carton, no textname substitute.
for k in range(5):components([26],'CC0_REAL_GIFT_CARTON_'+str(k),q(.43+k*.31,1.13),.97,[.26,.28,.13],FY,'GIFT')
remove=[o['id']for o in packet['objects']if (o['kind']=='text'and o.get('group')in['ProductLabels','ProductSigns']and not o['id'].startswith('rear_bay_')) or ('rear_bay_'in o['id']and('wood_upright'in o['id']or'_shelf_'in o['id'])) or o['id'].startswith('counter_gift_carton_') or o['id'].startswith('kenney_POS_') or o['id']in['payment_reader_MODEL','front_sales_counter_body']]
# Product cartons'name text included in ProductLabels, so listing duplicates harmless but avoidthem.
remove=sorted(set(remove));mats={}
for i,m in enumerate(d['materials']):
 pbr=m.get('pbrMetallicRoughness',{});mats['RET'+str(i)]={'color':pbr.get('baseColorFactor',[.7,.7,.7,1]),'metallic':pbr.get('metallicFactor',0),'roughness':pbr.get('roughnessFactor',.5),'source_name':m.get('name'),'gltf_texture_index':pbr.get('baseColorTexture',{}).get('index')};mats['GIFT'+str(i)]={'color':[.58,.44,.27,1],'metallic':0,'roughness':.65}
upgrade={'schema':'chooguard.gijang.freeassetupgrade004','store_key':'gijang','source_licensed_assets':[{'id':'POLYHAVEN_WOODEN_DISPLAY_SHELVES_01','url':'https://polyhaven.com/a/wooden_display_shelves_01','license':'CC0','license_url':'https://polyhaven.com/license','source_files':'FRESH-WEB-ASSET-DOWNLOAD-004.json','preview_personally_viewed':True},{'id':'INNERSCENE_CC0_RETAIL_CHECKOUT_POS','url':'https://www.innerscene.com/tools/library/3d-parts/checkout-counter-with-pos-original-retail-fixture-3fcfb566','license':'CC0','license_url':'https://creativecommons.org/publicdomain/zero/1.0/','glb_sha256':hashlib.sha256(glb).hexdigest(),'bytes':len(glb),'preview_personally_viewed':True}],'remove_exact_own_part_names':remove,'replace_labels_by_actual_print_surface':True,'retain_physical_sealed_pouches':66,'approved_region_unchanged':True,'native_floor_columns_openings_other_shops_unchanged':True,'objects':records,'materials':mats,'field_metric_status':'PHOTOGRAPH_SHAPES_AND_FIXED_APPROVED_BOUNDARY;HIDDENPOSMODEL_NOT_SURVEY','source_epoch':'2023/24photography','source_asset_quality_choice':'photorealPBRwoodenfixtureandactualmodeledPOS/cabinet/cartonparts;noKayKit/Kenneytoyfixtureadded','scene_QA_reopen_Play_capture':False}
(P/'freeasset-upgrade004.json').write_text(json.dumps(upgrade,ensure_ascii=False,indent=2));print('remove',len(remove),'newassetparts',len(records),'meshtriangles',sum(len(x['triangles'])//3 for x in records),'SHA',hashlib.sha256((P/'freeasset-upgrade004.json').read_bytes()).hexdigest())
# Extract material-images from actual downloaded GLB, not invented textures.
for i,im in enumerate(d.get('images',[])):
 if'bufferView'in im:
  bv=d['bufferViews'][im['bufferView']];bb=blob[bv.get('byteOffset',0):bv.get('byteOffset',0)+bv['byteLength']];ext='png'if im.get('mimeType')=='image/png'else'jpg';(P/f'innerscene-retail-texture-{i}.{ext}').write_bytes(bb)
print('images',[(i,x.get('mimeType'))for i,x in enumerate(d.get('images',[]))]);print('textureRefs',d.get('textures'))
