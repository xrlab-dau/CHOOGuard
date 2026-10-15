from pathlib import Path
import json,struct,hashlib
import numpy as np

E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009');P=Path('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json')
S=json.loads(P.read_text());MM={m['id']:m for m in S['meshes']};G=json.loads(Path('workers/twin/output/floor2-main-restoration-20261009/whole-guide-current-audit.json').read_text());F=np.array(G['native_station_frame']);I=np.linalg.inv(F)
B=json.loads(Path('.planning/2026-09-23-video-twin/majibang-plan.json').read_text())['boxFrame'];theta=np.deg2rad(B['rotDeg']);R=np.array([[np.cos(theta),-np.sin(theta)],[np.sin(theta),np.cos(theta)]]);cache={}
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def geo(r):
    m=MM[r['mesh']]
    if m['id']not in cache:
        b=Path(m['binary']).read_bytes();assert hashlib.sha256(b).hexdigest()==m['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',b);cache[m['id']]=(np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3),np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3))
    v,t=cache[m['id']];M=np.array(r['world_matrix']);q=((v@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]];return q[t]
surfaces=[]
for kind in['renderers','colliders']:
    for r in S[kind]:
        if not r.get('mesh')or not MM[r['mesh']].get('binary')or not r['active']:continue
        if kind=='colliders'and(not r['enabled']or r.get('trigger')):continue
        if kind=='renderers'and not r['renderer_enabled']:continue
        # Operating leaf sets are reviewed by root. This query finds static mesh obstruction only.
        if any(x in r['path']for x in['문짝','원본 자동문','원본 문 ·','승강기 카','KTXSource']):continue
        tri=geo(r)
        if not len(tri):continue
        lo=tri.min((0,1));hi=tri.max((0,1))
        if hi[2]<6.97 or lo[2]>9.4:continue
        surfaces.append({'kind':kind,'owner':r,'mesh':MM[r['mesh']],'tri':tri,'bounds':[lo,hi]})
def ray(tri,o,d,maxdist):
    e1=tri[:,1]-tri[:,0];e2=tri[:,2]-tri[:,0];p=np.cross(d,e2);det=(e1*p).sum(1);inv=np.divide(1,det,out=np.zeros(len(det)),where=abs(det)>1e-10);s=o-tri[:,0];u=(s*p).sum(1)*inv;q=np.cross(s,e1);v=(q*d).sum(1)*inv;dist=(q*e2).sum(1)*inv
    return np.flatnonzero((abs(det)>1e-10)&(u>=-1e-7)&(v>=-1e-7)&(u+v<=1+1e-7)&(dist>=0)&(dist<=maxdist))
report=[]
for n in G['retail_nodes']:
    e=n.get('storefront_recipe',{}).get('element')
    if not e or n['guide_number']in[20,21,22,23]:continue
    edge=np.array(e['geometry']['edge'],float)
    if e.get('frame')=='box':edge=edge@R.T+np.array(B['origin'])
    along=(edge[1]-edge[0]);along/=np.linalg.norm(along);normal=np.array([-along[1],along[0]])
    # Geometric direction sign does not affect symmetric crossing test.
    for di,door in enumerate(e['params']['doors']):
        centre=edge[0]+along*door['at'];width=door['width'];height=e['params'].get('doorHeight',2.2);samples=[];found={}
        for du in np.linspace(-width/2+.15,width/2-.15,9):
            for y in[7.25,7.65,8.05,8.45,8.85]:
                p=centre+along*du;o=np.array([p[0]+normal[0]*.4,p[1]+normal[1]*.4,y]);d=np.array([-normal[0],-normal[1],0]);samples.append([*p,y]);
                for s in surfaces:
                    lo,hi=s['bounds']
                    if o[2]<lo[2]or o[2]>hi[2]or np.any(p<lo[:2]-.5)or np.any(p>hi[:2]+.5):continue
                    ix=ray(s['tri'],o,d,.8)
                    if not len(ix):continue
                    key=(s['kind'],s['owner']['global_object_id'])
                    if key not in found:found[key]={'kind':s['kind'],'owner':s['owner'],'mesh':s['mesh'],'hit_samples':0,'hit_triangles':set()}
                    found[key]['hit_samples']+=1;found[key]['hit_triangles'].update(int(x)for x in ix)
        blockers=[r|{'hit_triangles':sorted(r['hit_triangles'])}for r in found.values()]
        report.append({'guide_unit':n['guide_number'],'current_label':n['current_name'],'element':e['id'],'current_edge_native_UV':edge.tolist(),'door_index':di,'aperture_type':door['type'],'aperture_centre_UV':centre.tolist(),'existing_recipe_aperture_width_model':width,'rays':len(samples),'crossing_static_mesh_owners':blockers,'scope':'Static renderer/collider samples across current aperture plane only; excludes movable leaves. No interior walkability or real dimensions claim.'})
out={'schema':'chooguard.floor2-current-static-apertures.v1','scene_sha256':S['scene_file_sha256'],'inventory':str(P),'inventory_sha256':sha(P),'native_frame':F.tolist(),'apertures':report,'omitted_four_native_entry_bays':'already tested current native doors, root owns operators/physics; old recipes intentionally not reused after stored source mesh correction','summary':{'apertures':len(report),'with_static_mesh_crossing':[(r['guide_unit'],[(x['owner']['path'],x['hit_samples'])for x in r['crossing_static_mesh_owners']])for r in report if r['crossing_static_mesh_owners']]},'production_mutated':False}
(O/'current-shop-aperture-audit.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(out['summary'])
