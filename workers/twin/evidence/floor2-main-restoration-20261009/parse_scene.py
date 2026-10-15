from pathlib import Path
import re,yaml,json
E=Path(__file__).resolve().parent;s=json.loads((E/'current-inventory.json').read_text());text=Path(s['scene']).read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)};gos={};tfs={}
for i,b in blocks.items():
 if b.startswith('GameObject:\n'):gos[i]=yaml.safe_load(b)['GameObject']
 elif b.startswith(('Transform:\n','RectTransform:\n')):
  g=re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}',b,re.M);f=re.search(r'^  m_Father: \{fileID: (-?\d+)\}',b,re.M)
  if g and f:tfs[i]={'go':int(g[1]),'parent':int(f[1]),'block':b}
def path(tf):
 t=tfs[tf];name=gos.get(t['go'],{}).get('m_Name','?');return path(t['parent'])+'/'+name if t['parent'] in tfs else name
names=['다인명가','호두나니&부산오뎅','청도할매김밥','반월당닭강정'];out=[];doors=[];bindings=[]
for tf,t in tfs.items():
 name=gos.get(t['go'],{}).get('m_Name','')
 if any(n in name for n in names):
  row={'gameobject_id':t['go'],'transform_id':tf,'name':name,'path':path(tf),'transform':yaml.safe_load(t['block'])[t['block'].split(':')[0]]};go=gos[t['go']];row['components']=[{'id':x['component']['fileID'],'type':blocks[x['component']['fileID']].split(':')[0]} for x in go['m_Component'] if x['component']['fileID'] in blocks];out.append(row)
for i,b in blocks.items():
 if b.startswith('MonoBehaviour:\n') and 'Element:' in b and 'StationDoor' in b:
  d=yaml.safe_load(b)['MonoBehaviour'];go=d['m_GameObject']['fileID'];tt=next((tf for tf,t in tfs.items() if t['go']==go),None)
  if tt:bindings.append({'component_id':i,'transform_id':tt,'path':path(tt),'element':d['Element']})
  if tt and any(n in path(tt) for n in names):
   leaves=[]
   for l in d['Leaves']:
    ttleaf=l['Pivot']['fileID'];lt=yaml.safe_load(tfs[ttleaf]['block'])['Transform'];leaves.append({'transform_id':ttleaf,'path':path(ttleaf),'transform':lt,'leaf_fields':l})
   doors.append({'component_id':i,'transform_id':tt,'path':path(tt),'fields':d,'leaves':leaves})
(E/'scene-components.json').write_text(json.dumps({'named_components':out,'doors':doors,'all_door_bindings':bindings},ensure_ascii=False,indent=2)+'\n');print('named',[(x['path'],x['components']) for x in out]);print('doors',[(x['fields']['Element'],x['path']) for x in doors]);print('doorcount',len(bindings))
