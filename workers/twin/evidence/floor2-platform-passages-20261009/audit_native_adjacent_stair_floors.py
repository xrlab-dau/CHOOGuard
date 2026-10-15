from pathlib import Path
import json,math,hashlib
import numpy as np
from shapely.geometry import Polygon,Point,LineString
from shapely.strtree import STRtree
E=Path('workers/twin/evidence/floor2-platform-passages-20261009');O=Path('workers/twin/output/floor2-platform-passages-20261009')
# Reuse only the read-only loader definitions, not the executable packet writer.
scope={};exec((E/'stage_walk_spines.py').read_text().split('regions={')[0],scope)
x=scope['x'];ms=scope['ms'];load=scope['load'];sha=scope['sha'];geo=[]
for owner in x['colliders']:
    if not owner.get('active')or not owner.get('enabled')or owner.get('trigger')or owner.get('type')!='MeshCollider':continue
    if owner['world_bounds_min'][1]>7.8 or owner['world_bounds_max'][1]<-.2:continue
    if not owner['path'].startswith('FPSWorld/공식 자료 부산역 역사/'):continue
    m=ms.get(owner.get('mesh'))
    if not m or not m.get('binary'):continue
    v,t=load(m);M=np.array(owner['world_matrix']);w=v@M[:3,:3].T+M[:3,3];p=w[t];n=np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0]);length=np.linalg.norm(n,axis=1)
    good=(n[:,1]>.90*length)&(length>1e-9)&(p[:,:,1].max(axis=1)<=7.8)&(p[:,:,1].min(axis=1)>=-.2)
    ords=np.flatnonzero(good);geo.append((owner,m,p[good],ords))
P=Path('Assets/ChooGuard/Art/Emergency/station-points.json');points=json.loads(P.read_text());entries={a['id']:a for a in points['escalators']}
xyz=lambda p:np.array([p['x'],p['y'],p['z']])
def local_geometry(a,b,margin=4):
    lo=np.minimum(a[[0,2]],b[[0,2]])-margin;hi=np.maximum(a[[0,2]],b[[0,2]])+margin;rows=[];polys=[]
    for owner,m,p,ords in geo:
        q=p[:,:,[0,2]];ok=(q.max(axis=1)[:,0]>=lo[0])&(q.min(axis=1)[:,0]<=hi[0])&(q.max(axis=1)[:,1]>=lo[1])&(q.min(axis=1)[:,1]<=hi[1])
        for j in np.flatnonzero(ok):
            poly=Polygon(q[j])
            if poly.area<1e-9:continue
            rows.append((owner,m,p[j],int(ords[j])));polys.append(poly)
    return rows,polys,STRtree(polys)
def sample(uv,expected,rows,polys,index):
    candidates=[]
    for i in index.query(Point(uv).buffer(1e-6)):
        if not polys[i].buffer(1e-6).covers(Point(uv)):continue
        owner,m,p,ordinal=rows[i];q=p[:,[0,2]];A=np.column_stack((q,np.ones(3)))
        if abs(np.linalg.det(A))<1e-9:continue
        bary=np.linalg.solve(A.T,[uv[0],uv[1],1]);y=float(bary@p[:,1])
        if y<expected-.75 or y>expected+.75:continue
        candidates.append({'Y':y,'owner':owner['path'],'global_object_id':owner['global_object_id'],'mesh_asset':m['asset'],'mesh_guid':m['asset_guid'],'triangle':ordinal,'barycentric':bary.tolist()})
    return max(candidates,key=lambda a:a['Y'])if candidates else None
audits=[]
for id in ['esc-well-n-56','esc-well-s-1','esc-well-s-34']:
    e=entries[id];a=xyz(e['stairsTop']);b=xyz(e['stairsBottom']);rows,polys,index=local_geometry(a,b);line=LineString([a[[0,2]],b[[0,2]]]);length=line.length;probes=[]
    for t in np.linspace(0,1,int(math.ceil(length/.10))+1):
        expected=a+(b-a)*t;hit=sample(expected[[0,2]],expected[1],rows,polys,index);probes.append({'t':float(t),'world_expected_linear_stair_axis':expected.tolist(),'actual_native_surface':hit})
    heights=[p['actual_native_surface']['Y']for p in probes if p['actual_native_surface']];intervals=[]
    # Exact projected-face intervals additionally restricted by agreement with
    # linear stair height. Flat ground under a middle flight cannot cover it.
    for owner,m,p,ordinal in rows:
        q=p[:,[0,2]];part=Polygon(q).intersection(line)
        if part.is_empty:continue
        segments=[part]if part.geom_type=='LineString'else list(part.geoms)if part.geom_type=='MultiLineString'else []
        A=np.column_stack((q,np.ones(3)))
        if abs(np.linalg.det(A))<1e-9:continue
        for seg in segments:
            ts=sorted(line.project(Point(c))/length for c in [seg.coords[0],seg.coords[-1]])
            d=[]
            for t in ts:
                u=a+(b-a)*t;bc=np.linalg.solve(A.T,[u[0],u[2],1]);d.append(float(bc@p[:,1]-u[1]))
            low,high=ts
            if abs(d[1]-d[0])<1e-12:
                if abs(d[0])>.75:continue
            else:
                td=sorted(ts[0]+(bound-d[0])*(ts[1]-ts[0])/(d[1]-d[0])for bound in [-.75,.75]);low=max(low,td[0]);high=min(high,td[1])
            if high>low:intervals.append((low,high,owner['path'],ordinal))
    merged=[]
    for low,high,_,__ in sorted(intervals):
        if not merged or low>merged[-1][1]+1e-6:merged.append([low,high])
        else:merged[-1][1]=max(merged[-1][1],high)
    missing=sum(p['actual_native_surface']is None for p in probes);delta=max(abs(u-v)for u,v in zip(heights,heights[1:]))if len(heights)>1 else None
    audits.append({'lane_id':id,'canonical_stair_top':a.tolist(),'canonical_stair_bottom':b.tolist(),'sample_spacing_max_m':.10,'sample_count':len(probes),'missing_native_surface_samples':missing,'actual_native_surface_samples':probes,'maximum_adjacent_sample_height_change_m':delta,'linear_height_profile_tolerance_m':.75,'exact_projected_face_merged_intervals':merged,'continuous_native_step_floor_projection':len(merged)==1 and merged[0][0]<=1e-6 and merged[0][1]>=1-1e-6,'sample_height_changes_within_existing_nav_climb035':delta is not None and delta<=.3501,'upward_source_faces_only':True,'full_capsule_clearance_verified':False,'navmesh_link_created':False})
down=entries['esc-well-m-89-1'];a=xyz(down['path'][0]);b=xyz(down['path'][-1]);rows,polys,index=local_geometry(a,b,8);flat=b-a;flat[1]=0;flat/=np.linalg.norm(flat);side=np.array([flat[2],0,-flat[0]]);sections=[]
for fraction in [.2,.35,.5,.65,.8]:
    centre=a+(b-a)*fraction;cells=[]
    for offset in np.arange(-6,6.001,.05):
        point=centre+side*offset;hit=sample(point[[0,2]],centre[1],rows,polys,index);cells.append({'offset':float(offset),'actual_surface':hit})
    groups=[];i=0
    while i<len(cells):
        h=cells[i]['actual_surface']
        if h is None:i+=1;continue
        j=i
        while j+1<len(cells) and cells[j+1]['actual_surface']is not None and abs(cells[j+1]['actual_surface']['Y']-cells[j]['actual_surface']['Y'])<.05:j+=1
        groups.append({'offset_min':cells[i]['offset'],'offset_max':cells[j]['offset'],'sampled_width_m':(j-i+1)*.05,'Y_range':[min(c['actual_surface']['Y']for c in cells[i:j+1]),max(c['actual_surface']['Y']for c in cells[i:j+1])],'owners':sorted(set(c['actual_surface']['owner']for c in cells[i:j+1]))});i=j+1
    sections.append({'fraction':fraction,'expected_linear_flight_Y':float(centre[1]),'groups':groups})
receipt={'schema':'chooguard.native-adjacent-stair-floor-readonly-audit.v1','source_scene_inventory':scope['invpath'].as_posix(),'source_inventory_sha256':sha(scope['invpath']),'current_canonical_points':str(P),'current_canonical_points_sha256':sha(P),'source_native_frames_are_preserved_far_from_root_generated_front_patches':True,'native_stair_axis_audits':audits,'m89_paired_well_floor_sections':sections,'m89_fixed_stair_absence_proven':False,'interpretation':'This verifies source-upward floor on known canonical adjacent stair axes without constructing geometry or links. It does not certify physical capsule clearance, source flight operation type in the field, NavMesh continuity or boarding/arrival access. Paired-well stairs=false is not an absence proof because StationSurvey only populates StairsSize when lanes.Count==1.','root_actual_navigation_queries':'workers/twin/output/floor2-platform-passages-20261009/landing-access-navigation-probes.json','Unity_or_Assets_mutations_by_lane':False}
(O/'native-adjacent-stair-floor-audit.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2));print([(a['lane_id'],a['sample_count'],a['missing_native_surface_samples'],a['continuous_native_step_floor_projection'],a['maximum_adjacent_sample_height_change_m'])for a in audits]);print([(s['fraction'],[(g['offset_min'],g['offset_max'],g['sampled_width_m'])for g in s['groups']])for s in sections])
