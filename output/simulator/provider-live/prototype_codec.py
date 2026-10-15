import json,os,collections
from pathlib import Path
root=Path.cwd();p=json.load(open('output/simulator/provider-live/whole-world-request.json'));g=p['graph']
ids={'@world'}
for r in g['entities']:ids.update(x for x in [r[0],r[1],r[10]] if x)
for ls in g['link_sets']:ids.update(ls)
for r in g['visuals']:ids.update(x for x in r[:2] if x)
ids=sorted(ids);idx={s:i for i,s in enumerate(ids)}
prev='';codes=[]
for s in ids:
 n=0
 while n<min(len(prev),len(s)) and prev[n]==s[n]:n+=1
 codes.append([n,s[n:]]);prev=s
rows=[];groups=[];groupidx={}
for r in g['entities']:
 common=[r[1],r[2],r[4],r[5],r[7],r[8],r[9],r[10]]
 common[0]=idx.get(common[0]);common[-1]=idx.get(common[-1]);key=json.dumps(common)
 if key not in groupidx:groupidx[key]=len(groups);groups.append({'common':common,'rows':[]})
 groups[groupidx[key]]['rows'].append([idx[r[0]],r[3],r[6]])
cg={k:v for k,v in g.items() if k not in ['entities','visuals','entity_columns','visual_columns','link_sets']}
cg.update(identity_codec='prefix-suffix-v1: each [prefix_length,suffix] uses previous decoded string prefix; null references mean no identity',identities=codes,
 group_columns=['parent_ref','kind','tag_set','state_schema','link_set','conditional_operation_set','kernel_semantic','physical_parent_ref'],entity_groups=groups,
 row_columns=['entity_ref','xyz','state_values'],link_sets=[[idx[s] for s in ls] for ls in g['link_sets']],
 visual_columns=['identity_ref','physical_parent_ref','qualification_status'],visuals=[[idx[r[0]],idx.get(r[1]),r[2]] for r in g['visuals']])
q=dict(p,graph=cg);q['output_contract']=q['output_contract'].replace('entity_id,','entity_ref:identity-table index (or entity_id),')
text=json.dumps(q,separators=(',',':'),ensure_ascii=False)
Path('output/simulator/provider-live/prototype-compact.json').write_text(text)
os.environ.update(HF_HUB_DISABLE_IMPLICIT_TOKEN='1',HF_HUB_DISABLE_TELEMETRY='1',TRANSFORMERS_OFFLINE='1')
from transformers import AutoTokenizer
t=AutoTokenizer.from_pretrained(str(root/'output/simulator/model-benchmark/models/Qwen2.5-1.5B-Instruct-4bit'),local_files_only=True,trust_remote_code=False)
print('groups',len(groups),'identities',len(ids),'bytes',len(text),'tokens',len(t.encode(text,add_special_tokens=False)))
for k,v in cg.items():print(k,len(t.encode(json.dumps(v,separators=(',',':')),add_special_tokens=False)))
# columnwise integer delta encoding of source rounded numeric summaries
for group in groups:
 rs=group.pop('rows');group['entity_refs']=[r[0] for r in rs]
 for index,name in [(1,'xyz'),(2,'state_values')]:
  width=len(rs[0][index]);columns=[]
  for col in range(width):
   values=[int(round(r[index][col]*10000)) for r in rs]
   if len(set(values))==1:columns.append(values[0])
   else:columns.append([values[0]]+[a-b for a,b in zip(values[1:],values)])
  group[name+'_columns']=columns
cg['row_columns']='Each group entity_refs defines row order. xyz/state_values columns are constant integers or arrays of first integer then successive deltas. All numbers scale1/10000; reconstruct each column with cumulative sums.'
text=json.dumps(q,separators=(',',':'),ensure_ascii=False)
Path('output/simulator/provider-live/prototype-columnar.json').write_text(text)
print('columnar bytes',len(text),'tokens',len(t.encode(text,add_special_tokens=False)))
