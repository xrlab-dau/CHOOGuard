"""Read-only exact face masks for Unit4's authored facade downlights."""
from pathlib import Path
import sys,json,hashlib
import numpy as np
sys.path.insert(0,str(Path('workers/twin').resolve()))
from official_plan_compare import readbin
EV=Path(__file__).resolve().parent
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
scene=json.loads((EV/'current-inventory.json').read_text());mm={m['id']:m for m in scene['meshes']}
fronts=[e for e in json.loads((EV/'source-frontages.json').read_text()) if e['type']=='Storefront']
owners=[r for r in scene['renderers'] if r['path'] in ['실내 트윈 마감/1F/Kit_CeilingGrid','실내 트윈 마감/1F/Kit_Light_조명기구_downlight']]
checks=[];rows=[]
for front in fronts:
    a,b=np.array(front['geometry']['edge']);ex=(b-a)/np.linalg.norm(b-a);normal=np.array([-ex[1],ex[0]])
    if np.dot(np.array(front['params']['faceToward'])-(a+b)*.5,normal)<0:normal=-normal
    params=front['params'];H=params['height'];depth=params['fascia']['depth'];L=np.linalg.norm(b-a)
    at=.75
    while at<L-.3:
        center=a+ex*at+normal*depth*.5
        for r in owners:
            m=mm[r['mesh']];v,t=readbin(m);uv=np.c_[v[:,2],v[:,0]];ring=r['path'].endswith('/Kit_CeilingGrid');Y=H-(.006 if ring else .004);radius=.075 if ring else .05
            distance=np.linalg.norm(uv[t]-center,axis=2)
            eligible=(distance.max(1)<=radius+1e-5)&(np.max(abs(v[t,1]-Y),axis=1)<1e-5)
            ti=np.flatnonzero(eligible);vi=np.unique(t[ti]);wanted=(80,40) if ring else (60,20)
            checks.append({'id':front['id']+'_'+str(at)+'_'+('ring' if ring else 'lens'),'passed':(len(vi),len(ti))==wanted,'details':{'expected_vertices_triangles':wanted,'actual_vertices_triangles':[len(vi),len(ti)],'explicit_source_center_UV':center.tolist(),'explicit_source_Y':Y}})
            if len(vi):
                rows.append({'fixture_id':front['id']+'-fascia-downlight-'+str(at),'frontage_element':front['id'],'along_source_front':at,'source_center_UV':center.tolist(),'source_Y':Y,'role':'ring' if ring else 'lens','preserve_fixed_frontage':front['id'] in ['1F-unit04-front1','1F-unit04-front5'],'source_asset':m['asset'],'source_asset_guid':m['asset_guid'],'source_file_sha256':sha(m['asset']),'source_binary_sha256':m['binary_sha256'],'source_binary':m['binary'],'mesh':m['id'],'vertices':len(v),'triangles':len(t),'vertex_indices':vi.tolist(),'expected_positions':v[vi].tolist(),'selected_triangles':ti.tolist(),'owners':[{'path':r['path'],'global_object_id':r['global_object_id'],'world_matrix':r['world_matrix']}]})
        at+=1.5
report={'schema':'chooguard.unit4-facade-downlight-attribution.v1','source_inventory_sha256':sha(EV/'current-inventory.json'),'method':'Explicit KitStorefront default fascia downlight centres at s=.75 then1.5 pitch, offset normal*depth/2; exact current ring/lens faces, with existing1e-5 source float endpoint resolution. Ring and lens share one explicit anchor delta.','all_checks_passed':all(x['passed'] for x in checks),'checks':checks,'mesh_mask_rows':rows,'site_accuracy_verified':False,'production_writes':0,'mapping_requirement':'Translate each moving front6/front8 ring and lens by the same mapped source_center_UV delta; preserve circular radius and fixed front5 fixtures. Do not spatially warp ring/lens vertices.'}
(EV/'facade-downlight-masks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'checks':len(checks),'failed':[x['id'] for x in checks if not x['passed']],'rows':len(rows),'moving_fixtures':len(set(x['fixture_id'] for x in rows if not x['preserve_fixed_frontage'])),'fixed_fixtures':len(set(x['fixture_id'] for x in rows if x['preserve_fixed_frontage'])),'report_sha256':sha(EV/'facade-downlight-masks.json')},ensure_ascii=False))
