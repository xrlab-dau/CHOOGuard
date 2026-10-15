"""Read-only barycentric support probes on byte-matched original source faces."""
exec(open('workers/twin/evidence/floor2-architecture-next-20261009/audit_native_entry.py').read().split('canvas=')[0])

surfaces=[]
for r in s['renderers']:
    if not r['active'] or not r['renderer_enabled']:continue
    if not r['path'].startswith('FPSWorld/공식 자료 부산역 역사/MainShell'):continue
    m=meshes[r['mesh']]
    if not m['binary']:continue
    v,t=read(r['mesh']);p=(np.c_[v,np.ones(len(v))]@(station_from_world@np.array(r['world_matrix'])).T)[:,:3];tri=p[t]
    lo=tri.min(1);hi=tri.max(1);n=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);mag=np.linalg.norm(n,axis=1)
    mask=(hi[:,2]>-3)&(lo[:,2]<25)&(hi[:,0]>-61)&(lo[:,0]<-26)&(hi[:,1]>6.6)&(lo[:,1]<7.6)&(np.abs(n[:,1])>.95*mag)&(mag>1e-8)
    for i in np.flatnonzero(mask):surfaces.append((r['path'],r['mesh'],int(i),tri[i]))

def probe(u,v):
    hits=[]
    for path,mid,i,t in surfaces:
        ab=t[1,[2,0]]-t[0,[2,0]];ac=t[2,[2,0]]-t[0,[2,0]];det=np.linalg.det(np.column_stack([ab,ac]))
        if abs(det)<1e-9:continue
        bc=np.linalg.solve(np.column_stack([ab,ac]),[u-t[0,2],v-t[0,0]])
        if min(*bc,1-sum(bc))>=-1e-6:
            y=t[0,1]+bc[0]*(t[1,1]-t[0,1])+bc[1]*(t[2,1]-t[0,1]);hits.append({'path':path,'mesh':mid,'triangle':i,'y':float(y)})
    return hits

units=[('20-left',[18.65,22.1],[-38.4,-33.6]),('21-left',[18.65,22.1],[-43.2,-38.8]),('23-right',[-1.3,1.9],[-40.2,-35.4]),('22-right',[-1.3,1.9],[-45.0,-40.6])]
result=[]
for name,u,v in units:
    probes=[]
    for a in np.linspace(u[0]+.15,u[1]-.15,5):
        for b in np.linspace(v[0]+.15,v[1]-.15,5):probes.append({'u':float(a),'v':float(b),'source_hits':probe(a,b)})
    result.append({'id':name,'u':u,'v':v,'probes':probes,'supported_count':sum(bool(p['source_hits']) for p in probes),'probe_count':len(probes)})
out={'schema':'chooguard.entry-original-support-probes.v1','role':'source_geometry_not_field_measurement','source_scene_export_sha256':s['scene_file_sha256'],'scene_inventory_sha256':hashlib.sha256(SCENE.read_bytes()).hexdigest(),'source_mesh_receipts':[meshes[mid] for mid in cache],'source_horizontal_face_count':len(surfaces),'units':result}
(OUT/'entry-floor-support.json').write_text(json.dumps(out,ensure_ascii=False,indent=2));print(json.dumps([{'id':x['id'],'supported':x['supported_count'],'n':x['probe_count'],'heights':sorted(set(round(h['y'],4) for p in x['probes'] for h in p['source_hits']))} for x in result]))
