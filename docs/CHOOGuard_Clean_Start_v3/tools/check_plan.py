"""Strict specification validator. It never runs Unity, changes a repository, or awards product qualification."""
from __future__ import annotations
import json,re,sys,unicodedata
from pathlib import Path, PureWindowsPath
from collections import defaultdict
ROOT=Path(__file__).resolve().parents[1]
FORBIDDEN=re.compile(r'\bEP\d{2}(?:-S\d{2})?\b|com\.xrlab\.chooguard|AuthoritativeShift|ICommitSink|work-orders/|aae4867c|github\.com/xrlab-dau/CHOOGuard')
def strict_json(text):
 def pairs(items):
  out={}
  for k,v in items:
   if k in out:raise ValueError('duplicate JSON key: '+k)
   out[k]=v
  return out
 def bad(x):raise ValueError('nonfinite JSON number: '+x)
 return json.loads(text,object_pairs_hook=pairs,parse_constant=bad)
def load(root=ROOT):
 def r(p):return strict_json((root/p).read_text(encoding='utf-8'))
 return {'baseline':r('contracts/baseline.json'),'tasks':r('contracts/tasks.json')['tasks'],'epics':r('contracts/epics.json')['epics'],'requirements':r('contracts/requirements.json')['requirements'],'tests':r('contracts/product-acceptance.json')['tests'],'sources':r('reference/sources.json')['sources'],'profiles':r('contracts/profiles.json'),'assemblies':r('contracts/assembly-layout.json')['modules'],'surfaces':r('contracts/surfaces.json')['surfaces']}
def path_key(path):
 if not isinstance(path,str) or not path.strip() or path.startswith(('/','\\')) or PureWindowsPath(path).drive or '\\' in path or any(x in ('..','') for x in path.split('/')) or any(c in path for c in ('\0',':')):raise ValueError('unsafe path: '+str(path))
 return unicodedata.normalize('NFC',path).casefold()
def cycle_errors(edges):
 color={};errors=[]
 def visit(node):
  if color.get(node)==1:errors.append('dependency cycle: '+node);return
  if color.get(node)==2:return
  color[node]=1
  for nxt in edges.get(node,[]):visit(nxt)
  color[node]=2
 for node in list(edges):visit(node)
 return errors

def phase_graph(d,profile):
 if profile not in d['profiles']['profiles']:raise ValueError('unknown profile: '+profile)
 flags=set(d['profiles']['profiles'][profile])|{'ALWAYS'}
 graph=defaultdict(list)
 for t in d['tasks']:
  id=t['id'];graph[id+':candidate'].append(id+':contract');graph[id+':integration'].append(id+':candidate');graph[id+':qualification'].append(id+':integration')
  for x in t['dependsOn']:
   if x['condition'] not in d['profiles']['conditions']:raise ValueError('unknown condition: '+x['condition'])
   if x['condition'] in flags:
    graph[id+':'+x['consumerStage']].append(x['taskId']+':'+x['stage'])
    # Unit implementation needs a producer contract, not its entire implementation.
    graph[id+':candidate'].append(x['taskId']+':contract')
 return dict(graph)

def validate(d):
 errors=[]
 def need(c,m):
  if not c:errors.append(m)
 def unique(items,label):
  ids=[x['id'] for x in items];need(len(ids)==len(set(ids)),label+' duplicate ID');return {x['id']:x for x in items}
 try:
  b=d['baseline'];need(b.get('developmentMode')=='GREENFIELD','baseline not greenfield')
  need(b.get('platform')=='UNITY_NATIVE_PC','wrong platform');need(b.get('presentation')=='UGUI_TMP_INPUT_SYSTEM','wrong native UI')
  for f in ['legacyCompatibilityRequired','legacyCodeRequired','priorEpicGraphInherited','priorArtifactAcceptanceInherited','fixedRegionIdsInherited','remoteMutationsPerformed','resetPerformed']:need(b.get(f) is False,'baseline '+f)
  need(b.get('productModeIds')==['TUTORIAL','RANDOM_OPERATIONS_LAB'],'wrong product modes')
  E=unique(d['epics'],'epic');T=unique(d['tasks'],'task');R=unique(d['requirements'],'requirement');A=unique(d['tests'],'product test');S=unique(d['sources'],'source')
  need(bool(E) and bool(T),'empty plan');need(set(R)=={f'REQ-{i:03}' for i in range(1,92)},'product requirement coverage');need(set(A)=={f'AT-{i:03}' for i in range(1,92)},'product test coverage')
  writes=defaultdict(list);testids=[]
  for t in d['tasks']:
   id=t['id'];need(t['epicId'] in E and id.startswith(t['epicId']+'.'),'task epic '+id)
   need(t.get('assignee') is None,'unexpected assignee '+id);need(t.get('status')=='SPECIFIED_NOT_IMPLEMENTED' and t.get('acceptanceState')=='NOT_RUN','false product state '+id)
   need(t['artifact']['id']=='A-'+id and t['artifact']['status']=='NOT_PRODUCED' and t['artifact'].get('ownerTask')==id,'wrong artifact '+id)
   need(set(t['artifact'].get('producesStages',[]))==set(d['profiles']['producerStages']),'producer stages missing '+id)
   need(len(t['algorithm'])>=3 and all(isinstance(x,str) and len(x.strip())>=8 for x in t['algorithm']),'empty implementation algorithm '+id)
   need(isinstance(t['contract'],str) and len(t['contract'].strip())>=16,'missing IO contract '+id)
   need(bool(t.get('implementationSpecRefs')) and bool(t.get('oracle')) and t['oracle'].get('status')=='NOT_RUN','missing concrete specification/oracle '+id)
   need(bool(t.get('supportsRequirementIds')) and set(t['supportsRequirementIds'])<=set(R),'missing supporting requirement '+id)
   need(set(t['sourceIds'])<=set(S),'unknown source '+id)
   need(len(t['acceptance'])>=2,'insufficient acceptance '+id)
   for a in t['acceptance']:
    testids.append(a.get('id'));need(a.get('status')=='NOT_RUN','false task test '+id)
    need(all(isinstance(a.get(k),str) and a[k].strip() for k in ['id','given','when','then']),'missing Given When Then '+id)
   for path in t['plannedFiles']+[t['verificationFile']]+[x['path'] for x in t['testTargets'] if x['path']!=t['verificationFile']]:
    try:writes[path_key(path)].append(id)
    except ValueError as e:errors.append(str(e))
   for x in t['testTargets']:
    need(x['kind'] in ['EDIT_MODE','DOCUMENT_REVIEW','PLAY_MODE','PLAYER_ACCEPTANCE','PLAYER_DISK','WORKER_INTEGRATION'],'test kind '+id);need(x['state']=='NOT_RUN','false target test '+id)
   if t['epicId']=='CS-PLAY':need({'PLAY_MODE','PLAYER_ACCEPTANCE'}<=set(x['kind'] for x in t['testTargets']),'native test coverage '+id)
   for x in t['dependsOn']:
    need(x['taskId'] in T,'unknown provider '+id);need(x['artifactId']=='A-'+x['taskId'],'wrong dependency artifact '+id)
    need(x.get('stage') in d['profiles']['producerStages'],'unknown producer stage '+id)
    need(x.get('consumerStage') in ['integration','qualification'],'invalid consumer '+id)
    need(x.get('condition') in d['profiles']['conditions'],'unknown condition '+id)
   need(set(t['requirementIds'])=={r['id'] for r in R.values() if r['taskId']==id},'requirement task binding '+id)
  need(len(testids)==len(set(testids)),'duplicate task test IDs')
  for p,owners in writes.items():need(len(owners)==1,'duplicate case-insensitive writer '+p)
  for e in E.values():need(set(e['taskIds'])=={t['id'] for t in T.values() if t['epicId']==e['id']},'epic membership '+e['id'])
  for r in R.values():
   need(r['taskId'] in T,'missing requirement owner '+r['id']);need(r['acceptanceTestId'] in A,'missing product test '+r['id']);need(set(r['sourceIds'])<=set(S),'missing source '+r['id'])
   if r['taskId'] in T:need(T[r['taskId']]['epicId']==r['epicId'],'requirement epic '+r['id'])
   if r['acceptanceTestId'] in A:
    a=A[r['acceptanceTestId']];need(r['id'] in a['requirementIds'] and a['taskId']==r['taskId'],'reverse acceptance link '+r['id'])
  for a in A.values():
   need(a['result']=='NOT_RUN','false acceptance result '+a['id']);need(a['taskId'] in T,'test owner '+a['id'])
   need(set(a['requirementIds'])=={r['id'] for r in R.values() if r['acceptanceTestId']==a['id']},'reverse requirement link '+a['id'])
  mods={m['name']:m for m in d['assemblies']};need(len(mods)==len(d['assemblies']),'duplicate assembly')
  for m in mods.values():
   need(m['owner'] in T and m['path'] in T[m['owner']]['plannedFiles'],'assembly has no owner '+m['name']);need(set(m['references'])<=set(mods),'unresolved assembly reference '+m['name'])
   if m['name'] in ['ChooGuard.Domain','ChooGuard.Contracts']:need(not m['engineReferences'],'domain depends on Unity')
  errors.extend(cycle_errors({m['name']:m['references'] for m in mods.values()}))
  for t in T.values():
   for p in t['plannedFiles']:
    if p.endswith('.cs') and p.startswith('Assets/'):
     owners=[m for m in mods.values() if p.startswith(m['root']+'/')]
     if '/Tests/' not in p:need(len(owners)==1,'unowned/ambiguous C# '+p)
     if '/Editor/' in p:need(any(m['editorOnly'] for m in owners),'Editor leak '+p)
  U=unique(d['surfaces'],'surface');need(set(U)=={f'S{i:02}' for i in range(1,13)},'surface coverage')
  for u in U.values():need(u['taskId'] in T and u['path'] in T[u['taskId']]['plannedFiles'],'surface missing implementation '+u['id']);need(u['runtime']=='UNITY_NATIVE' and u['status']=='NOT_IMPLEMENTED','surface platform/status')
  for profile in d['profiles']['profiles']:
   need(set(d['profiles']['profiles'][profile])<=set(d['profiles']['conditions']),'bad profile conditions')
   try:errors.extend(cycle_errors(phase_graph(d,profile)))
   except ValueError as e:errors.append(str(e))
  need(FORBIDDEN.search(json.dumps(d,ensure_ascii=False)) is None,'legacy execution dependency present')
 except (KeyError,TypeError,ValueError) as e:errors.append('MALFORMED_CONTRACT '+str(e))
 return errors

def check_files(root=ROOT):
 errors=[];d=load(root)
 for t in d['tasks']:
  p=root/'tasks'/(t['id']+'.json')
  if not p.exists() or strict_json(p.read_text())!=t:errors.append('task packet drift '+t['id'])
  for ref in t['implementationSpecRefs']:
   if not (root/ref).is_file():errors.append('missing detailed spec '+ref)
 for p in root.rglob('*.md'):
  if 'review' in p.relative_to(root).parts:continue
  text=p.read_text(encoding='utf-8')
  if FORBIDDEN.search(text):errors.append('legacy token '+str(p.relative_to(root)))
  for link in re.findall(r'\]\(([^)]+)\)',text):
   if link.startswith(('http:','https:','mailto:','#')):continue
   target=(p.parent/link.split('#')[0])
   if not target.exists():errors.append('broken link '+str(p.relative_to(root))+' -> '+link)
 return errors

def main():
 try:
  d=load();errors=validate(d)+check_files()
  out={'ok':not errors,'scope':'SPECIFICATION_STRUCTURE_NOT_PRODUCT_EXECUTION','epics':len(d['epics']),'tasks':len(d['tasks']),'requirements':len(d['requirements']),'profiles':len(d['profiles']['profiles']),'productExecution':'NOT_RUN','errors':errors}
 except (OSError,ValueError,KeyError) as e:out={'ok':False,'errors':[str(e)]}
 print(json.dumps(out,ensure_ascii=False,indent=2));return 0 if out['ok'] else 1
if __name__=='__main__':sys.exit(main())
