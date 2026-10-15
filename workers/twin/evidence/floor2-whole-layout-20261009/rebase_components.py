from pathlib import Path
import re,json,hashlib,yaml
import numpy as np
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009')
scene=Path('Assets/ChooGuard/Scenes/FpsStation.unity');txt=scene.read_text();digest=hashlib.sha256(txt.encode()).hexdigest()
blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',txt,re.M|re.S)}
names={};tfs={};gtf={};F=np.array(json.loads(Path('workers/twin/output/floor2-main-restoration-20261009/whole-guide-current-audit.json').read_text())['native_station_frame']);I=np.linalg.inv(F)
for cid,b in blocks.items():
    if b.startswith('GameObject:\n'):
        n=re.search(r'^  m_Name: (.*)$',b,re.M);names[cid]=n[1]if n else'?'
    if b.startswith('Transform:\n'):
        go=re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}',b,re.M);father=re.search(r'^  m_Father: \{fileID: (-?\d+)\}',b,re.M)
        if not go or not father:continue
        d={'go':int(go[1]),'parent':int(father[1]),'source_block_sha256':hashlib.sha256(b.encode()).hexdigest()}
        for key in['m_LocalRotation','m_LocalPosition','m_LocalScale']:
            m=re.search(r'^  '+key+r': (.*)$',b,re.M);d[key]=yaml.safe_load(m[1])
        tfs[cid]=d;gtf[d['go']]=cid
cache={}
def M(tid):
    if tid in cache:return cache[tid]
    d=tfs[tid];x,y,z,w=[float(d['m_LocalRotation'][k])for k in'xyzw'];R=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]);m=np.eye(4);m[:3,:3]=R@np.diag([float(d['m_LocalScale'][k])for k in'xyz']);m[:3,3]=[float(d['m_LocalPosition'][k])for k in'xyz'];m=M(d['parent'])@m if d['parent']in tfs else m;cache[tid]=m;return m
def path(tid):
    d=tfs[tid];n=names.get(d['go'],'?');return path(d['parent'])+'/'+n if d['parent']in tfs else n
doors=[];identities=[]
for cid,b in blocks.items():
    if not b.startswith('MonoBehaviour:\n')or not('StationDoor'in b or'664c243f602454c7c88e9ec81a41831e'in b):continue
    d=yaml.safe_load(b)['MonoBehaviour'];tid=gtf.get(d['m_GameObject']['fileID'])
    if not tid:continue
    m=M(tid);c=d.get('Centre',{'x':0,'y':0,'z':0});p=m@np.array([float(c[k])for k in'xyz']+[1]);q=(p[:3]@I.T)[[2,0,1]]
    row={'component_id':cid,'gameobject_id':d['m_GameObject']['fileID'],'transform_id':tid,'path':path(tid),'native_UVY':q.tolist(),'world_matrix':m.tolist(),'source_component_block_sha256':hashlib.sha256(b.encode()).hexdigest(),'source_transform_block_sha256':tfs[tid]['source_block_sha256'],'fields':d}
    if 'StationDoor'in b:doors.append(row)
    else:identities.append(row)
out={'schema':'chooguard.floor2-current-component-rebase.v1','scene':str(scene),'scene_sha256':digest,'all_station_doors':doors,'all_source_identities':identities,'production_mutated':False}
(E/'current-components.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print('REBASE',digest,len(doors),len(identities))
