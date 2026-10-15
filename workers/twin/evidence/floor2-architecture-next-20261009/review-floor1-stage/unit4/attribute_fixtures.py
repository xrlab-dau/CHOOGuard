"""Bounded read-only Unit4 procedural assembly ownership masks."""
from pathlib import Path
import json, hashlib, sys, re
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
sys.path.insert(0,str(Path('workers/twin').resolve()))
from official_plan_compare import readbin

EV=Path(__file__).resolve().parent
BASE=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/current-before-unit4/current-scene-structure.json')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
(EV/'current-inventory.json').write_bytes(BASE.read_bytes())
scene=json.loads(BASE.read_text());mm={m['id']:m for m in scene['meshes']}
spec=json.loads((EV/'source-shop.json').read_text())
fixtures=[f for f in spec['params']['layout'] if f['fixture'] in ('shelf','gondola')]
rows=scene['renderers']+[dict(r,collider_only=True) for r in scene['colliders'] if r.get('type')=='MeshCollider']
allowed=['Kit_Shelf_','Kit_Joint','Kit_Product_Kit_Product','Kit_Colliders']
patches=[];checks=[];all_components=[]
def check(id,ok,details):checks.append({'id':id,'passed':bool(ok),'details':details})
fixture_bounds={}
for f in fixtures:
    heading=np.array(f['heading'],float);heading/=np.linalg.norm(heading)
    along=np.array([-heading[1],heading[0]])
    L=f['length'];two=f['fixture']=='gondola';d=f.get('depth',.4);h=f.get('height',1.5 if two else 2.)
    fixture_bounds[f['sourceIndex']]={'heading':heading,'along':along,'center':np.array(f['at'],float),'half_run':(L+.06)*.5,'depth_range':[-d-.03,d+.03] if two else [-.02,d+.04],'height':h,'two_sided':two,'source':f}
    check('positive_source_dimensions_'+str(f['sourceIndex']),L>0 and d>0 and h>0,{'sourceIndex':f['sourceIndex'],'length':L,'depth':d,'height':h})
for r in rows:
    if not r['active'] or not r['path'].startswith('실내 트윈 마감/점포 내부 1F/'):continue
    name=r['path'].split('/')[2]
    if not any(name.startswith(a) for a in allowed):continue
    m=mm[r['mesh']]
    if not m['binary']:continue
    v,t=readbin(m)
    # Explicit ShopInterior frame=root uses KP(UV,y)=(V,y,U).
    # Current batch has exactly the shared root frame; retain its full matrix.
    expected_matrix=spec.get('frame')=='root' and r['world_matrix'][0][3]==r['world_matrix'][1][3]==r['world_matrix'][2][3]==0.
    check('native_root_frame_'+m['id'],expected_matrix,{'path':r['path'],'world_matrix':r['world_matrix']})
    uvs=np.c_[v[:,2],v[:,0]]
    _,weld=np.unique(np.round(v,5),axis=0,return_inverse=True)
    a,b=weld[t].ravel(),weld[t[:,[1,2,0]]].ravel()
    _,lbl=connected_components(coo_matrix((np.ones(len(a)),(a,b)),shape=(weld.max()+1,weld.max()+1)),directed=False)
    labels=lbl[weld]
    candidates={}
    # The 1e-5 boundary allowance is the existing position-weld resolution,
    # solely accounting for float32 source endpoint arithmetic in exact
    # builder-sized boxes. It is not a source-equivalence certificate.
    for fi,q in fixture_bounds.items():
        rel=uvs-q['center'];ss=rel@q['along'];dd=rel@q['heading'];lo,hi=q['depth_range']
        candidates[fi]=(abs(ss)<=q['half_run']+1e-5)&(dd>=lo-1e-5)&(dd<=hi+1e-5)&(v[:,1]>=-1e-5)&(v[:,1]<=q['height']+1e-5)
    by_fixture={fi:[] for fi in fixture_bounds}
    for component in np.unique(labels[t[:,0]]):
        tri=np.flatnonzero(labels[t[:,0]]==component);ids=np.unique(t[tri])
        eligible=[fi for fi,within in candidates.items() if within[ids].all()]
        if not eligible:continue
        check('unique_assembly_'+m['id']+'_'+str(component),len(eligible)==1,{'eligible_sourceIndices':eligible,'triangles':tri.tolist()})
        if len(eligible)!=1:continue
        fi=eligible[0];by_fixture[fi].append((tri,ids))
        all_components.append({'source_asset':m['asset'],'mesh':m['id'],'sourceIndex':fi,'component':int(component),'vertex_indices':ids.tolist(),'triangle_ordinals':tri.tolist(),'uv_bounds':[uvs[ids].min(0).tolist(),uvs[ids].max(0).tolist()],'y_bounds':[float(v[ids,1].min()),float(v[ids,1].max())]})
    for fi,selected in by_fixture.items():
        if not selected:continue
        tri=np.sort(np.concatenate([x[0] for x in selected]));ids=np.unique(np.concatenate([x[1] for x in selected]));mask=np.zeros(len(v),bool);mask[ids]=True
        split=[np.flatnonzero(weld==x).tolist() for x in np.unique(weld[ids]) if not mask[weld==x].all()]
        check('assembly_closure_'+m['id']+'_'+str(fi),np.array_equal(np.flatnonzero(mask[t].any(1)),tri) and not split,{'triangles':len(tri),'vertices':len(ids),'split_welds':split})
        q=fixture_bounds[fi];f=q['source'];kind='products' if name.startswith('Kit_Product_') else 'collider' if r.get('collider_only') else 'body' if name.startswith('Kit_Shelf_') else 'base'
        if kind!='products':
            expected={'body':(264,132) if q['two_sided'] else (192,96),'base':(40,20) if q['two_sided'] else (20,10),'collider':(24,12)}[kind]
            check('builder_piece_count_'+m['id']+'_'+str(fi),(len(ids),len(tri))==expected,{'sourceIndex':fi,'kind':kind,'expected_vertices_triangles':expected,'actual_vertices_triangles':[len(ids),len(tri)]})
        else:
            # KBox(false,false) appends sixteen vertices/eight triangles per
            # product. Recomputed corners can straddle the 1e-5 weld rounding
            # boundary, so a complete box need not form a single rounded weld
            # component. Check the exact source append blocks independently.
            blocks=np.unique(ids//16)
            complete_blocks=all(mask[np.arange(k*16,k*16+16)].all() and np.isin(t,np.arange(k*16,k*16+16)).all(1).sum()==8 for k in blocks)
            check('product_primitive_sizes_'+m['id']+'_'+str(fi),len(v)%16==0 and complete_blocks,{'sourceIndex':fi,'product_weld_components':len(selected),'product_append_blocks':len(blocks),'expected_box_vertices_triangles':[16,8]})
        proof={'path':r['path'],'global_object_id':r['global_object_id'],'world_matrix':r['world_matrix']}
        row={'sourceIndex':fi,'fixture':f['fixture'],'assembly_anchor_UV':f['at'],'assembly_heading_UV':q['heading'].tolist(),'assembly_along_UV':q['along'].tolist(),'role':kind,'source_asset':m['asset'],'source_asset_guid':m['asset_guid'],'source_file_sha256':sha(m['asset']),'source_binary_sha256':m['binary_sha256'],'source_binary':m['binary'],'mesh':m['id'],'vertices':len(v),'triangles':len(t),'vertex_indices':ids.tolist(),'expected_positions':v[ids].tolist(),'selected_triangles':tri.tolist(),'component_count':len(selected),'uv_bounds':[uvs[ids].min(0).tolist(),uvs[ids].max(0).tolist()],'y_bounds':[float(v[ids,1].min()),float(v[ids,1].max())],'owners':[] if r.get('collider_only') else [proof],'collider_owners':[proof] if r.get('collider_only') else []}
        if kind=='products':row['product_box_count']=len(blocks);row['source_product_append_block_ordinals']=blocks.tolist()
        patches.append(row)
for fi,q in fixture_bounds.items():
    matches=[x for x in patches if x['sourceIndex']==fi]
    check('all_assembly_channels_'+str(fi),{x['role'] for x in matches}=={'body','base','collider','products'},{'sourceIndex':fi,'roles':sorted(set(x['role'] for x in matches)),'product_materials':sorted(x['source_asset'] for x in matches if x['role']=='products')})
    check('all_five_product_materials_'+str(fi),len([x for x in matches if x['role']=='products'])==5,{'sourceIndex':fi,'products_batches':len([x for x in matches if x['role']=='products'])})
for asset in set(x['source_asset'] for x in patches):
    matches=[x for x in patches if x['source_asset']==asset]
    selected=[set(x['vertex_indices']) for x in matches]
    check('assembly_masks_disjoint_'+Path(asset).name,all(not a.intersection(b) for i,a in enumerate(selected) for b in selected[i+1:]),{'source_asset':asset,'sourceIndices':[x['sourceIndex'] for x in matches]})
counts=[{'sourceIndex':fi,'fixture':q['source']['fixture'],'body':sum(len(x['vertex_indices']) for x in patches if x['sourceIndex']==fi and x['role']=='body'),'base':sum(len(x['vertex_indices']) for x in patches if x['sourceIndex']==fi and x['role']=='base'),'collider':sum(len(x['vertex_indices']) for x in patches if x['sourceIndex']==fi and x['role']=='collider'),'product_vertices':sum(len(x['vertex_indices']) for x in patches if x['sourceIndex']==fi and x['role']=='products'),'product_boxes':sum(x.get('product_box_count',0) for x in patches if x['sourceIndex']==fi and x['role']=='products')} for fi,q in fixture_bounds.items()]
report={'schema':'chooguard.unit4-procedural-assembly-attribution.v1','source_inventory':str(BASE),'source_inventory_sha256':sha(BASE),'scene_sha256':scene['scene_file_sha256'],'source_shop_spec_sha256':sha(EV/'source-shop.json'),'builder_source':'AgentScripts/MajibangBuilder.cs','method':'Explicit KShelf/KShopLayout source-anchor oriented bounds, all vertices of complete coordinate-welded components; static piece counts and product primitive dimensions independently verified. No arbitrary component centroid assignment.','site_accuracy_verified':False,'production_writes':0,'checks':checks,'all_checks_passed':all(x['passed'] for x in checks),'assembly_counts':counts,'mesh_mask_rows':patches,'source_components':all_components,'mapping_requirement':'Shelf source1 may follow the common structural UV map; each gondola source2/3/4 must use one exact rigid delta from its explicit assembly anchor across every body, base, product and collider mask.'}
(EV/'fixture-attribution-masks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'all_passed':report['all_checks_passed'],'checks':len(checks),'failed':[x['id'] for x in checks if not x['passed']],'assembly_counts':counts,'mask_rows':len(patches),'report_sha256':sha(EV/'fixture-attribution-masks.json')},ensure_ascii=False))
