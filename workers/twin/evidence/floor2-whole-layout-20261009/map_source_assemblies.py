from pathlib import Path
import json,struct,hashlib,itertools
import numpy as np
from scipy.spatial import cKDTree

E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009')
P=Path('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json');S=json.loads(P.read_text());MM={m['id']:m for m in S['meshes']}
D=json.loads((E/'source-region-geometry.json').read_text());A=json.loads((E/'source-circulation-origin-candidates.json').read_text());F=np.array(A['native_frame']);I=np.linalg.inv(F)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(m):
    b=Path(m['binary']).read_bytes();assert hashlib.sha256(b).hexdigest()==m['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',b)
    return np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3),np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3)
parts={'upper-fixed-stair':('stairs-upper-left',('ROOT/g62/g44/g1/',)),
       'upper-platform-escalator-pair':('escalator-upper-left',('ROOT/g62/g26/g0/g1/g0/','ROOT/g62/g26/g7/g1/g0/'))}
result=[]
for label,(region,prefixes)in parts.items():
    rows=[r for r in D['regions'][region]if r['source_path'].startswith(prefixes)and'local_vertices'not in r and'native_vertices'in r]
    lo=np.min([r['native_UVY_bounds'][0]for r in rows],0)-.01;hi=np.max([r['native_UVY_bounds'][1]for r in rows],0)+.01
    pool=[];poolrows=[];count=0
    for owner in S['renderers']:
        if not owner.get('mesh')or not owner['path'].startswith('FPSWorld/')or not MM[owner['mesh']].get('binary'):continue
        v,t=read(MM[owner['mesh']]);M=np.array(owner['world_matrix']);q=((v@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]];tri=q[t];mn=tri.min(1);mx=tri.max(1)
        ix=np.flatnonzero(np.all(mx>=lo,1)&np.all(mn<=hi,1))
        if not len(ix):continue
        pool.append(tri[ix]);poolrows.append({'owner':owner,'mesh':MM[owner['mesh']],'pool_start':count,'face_indices':ix.tolist()});count+=len(ix)
    pool=np.concatenate(pool);cent=pool.mean(1);tree=cKDTree(cent);perms=np.array(list(itertools.permutations(range(3))));maprows=[];mapped=0;missing=[];ambiguous=[];errors=[]
    for row in rows:
        q=np.array(row['native_vertices']);faces=np.array(row['source_polygons']);assert faces.shape[1]==3
        matches=[]
        for sourceix,tr in enumerate(q[faces]):
            cand=tree.query_ball_point(tr.mean(0),.0005);valid=[]
            for ix in cand:
                dis=np.linalg.norm(pool[ix][perms]-tr[None,:,:],axis=2).max(1);p=int(np.argmin(dis))
                if dis[p]<.0005:valid.append((ix,float(dis[p]),p))
            if not valid:missing.append({'source_id':row['source_id'],'source_face':sourceix});continue
            # Coincident render copies remain explicit; no arbitrary contributor choice.
            if len(valid)>1:ambiguous.append({'source_id':row['source_id'],'source_face':sourceix,'matches':len(valid)})
            mapped+=1
            for ix,err,perm in valid:
                r=next(x for x in poolrows if x['pool_start']<=ix<x['pool_start']+len(x['face_indices']));face=r['face_indices'][ix-r['pool_start']]
                matches.append({'source_face':sourceix,'current_owner_global_id':r['owner']['global_object_id'],'current_mesh_asset':r['mesh']['asset'],'current_triangle':face,'maximum_vertex_error_model':err,'vertex_permutation':perms[perm].tolist()});errors.append(err)
        maprows.append({'source_id':row['source_id'],'source_path':row['source_path'],'source_world_matrix':row['source_world_matrix'],'source_local_vertices_sha256':hashlib.sha256(np.array(row['source_local_vertices'],dtype='<f4').tobytes()).hexdigest(),'source_faces_sha256':hashlib.sha256(faces.astype('<i4').tobytes()).hexdigest(),'source_materials':row['material_slots'],'source_triangles':len(faces),'matches':matches})
    sourceparts=[x for x in A['records']if x['source_path'].startswith(prefixes)and x['source_path'].rsplit('/',1)[0].endswith(('g1','g0'))]
    result.append({'id':label,'guide_candidate':region,'source_prefixes':prefixes,'native_UVY_bounds':[lo.tolist(),hi.tolist()],'source_mesh_objects':len(rows),'source_triangles':sum(r['triangles']for r in rows),'mapped_source_triangles':mapped,'missing':missing,'ambiguous_current_matches':ambiguous,'maximum_vertex_error_model':max(errors)if errors else None,'current_owner_receipts':poolrows,'source_face_correspondence':maprows,'source_component_origins':sourceparts,'semantic_status':'Exact native assembly ownership; guide icon assignment remains candidate until independent route/visual control accepts it. No pose or topology mutation authorized.'})
out={'schema':'chooguard.floor2-exact-native-assemblies.v1','inventory':str(P),'inventory_sha256':sha(P),'scene_sha256':S['scene_file_sha256'],'source_geometry_receipt':str(E/'source-region-geometry.json'),'source_geometry_sha256':sha(E/'source-region-geometry.json'),'source_blend_sha256':D['source_blend_sha256'],'native_frame':F.tolist(),'assemblies':result,'geometry_mutated':False,'site_accuracy_verified':False}
(O/'exact-native-assemblies.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print('ASSEMBLIES',[(r['id'],r['source_triangles'],r['mapped_source_triangles'],len(r['missing']),len(r['ambiguous_current_matches']),r['maximum_vertex_error_model'])for r in result])
