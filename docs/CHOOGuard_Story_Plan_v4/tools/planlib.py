"""Read-only story graph validation and readiness inspection.
No command here starts a Unity job, grants authority, edits GitHub, or acquires a lock.
"""
from __future__ import annotations
import hashlib, json, unicodedata
from pathlib import Path, PurePosixPath
from collections import defaultdict
from graphlib import TopologicalSorter, CycleError
from typing import Any
from jsonschema import Draft202012Validator, FormatChecker
ROOT=Path(__file__).resolve().parents[1]
class PlanError(ValueError): pass

def read_json(p:Path)->Any:
 def pairs(items):
  d={}
  for k,v in items:
   if k in d: raise PlanError('DUPLICATE_JSON_KEY:'+k)
   d[k]=v
  return d
 def no_const(v): raise PlanError('NON_FINITE_JSON:'+v)
 return json.loads(p.read_text(encoding='utf8'), object_pairs_hook=pairs,parse_constant=no_const)

def digest(value:Any)->str:
 return hashlib.sha256(json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':'),allow_nan=False).encode('utf8')).hexdigest()

def safe_path(root:Path,value:str)->Path:
 if not isinstance(value,str) or not value or '\\' in value or ':' in value:
  raise PlanError('INVALID_PATH:'+str(value))
 p=PurePosixPath(value)
 if p.is_absolute() or any(x in ('..','.') for x in p.parts): raise PlanError('PATH_ESCAPE:'+value)
 if any(x.endswith((' ','.')) for x in p.parts): raise PlanError('WINDOWS_PATH_ALIAS:'+value)
 target=(root/str(p)).resolve()
 if not target.is_relative_to(root.resolve()): raise PlanError('PATH_ESCAPE:'+value)
 return target

def canonical_path(path:str)->str:
 safe_path(ROOT,path)
 return unicodedata.normalize('NFC',path).casefold().removesuffix('.meta')

def path_overlap(a:str,b:str)->bool:
 a,b=canonical_path(a),canonical_path(b)
 return a==b or a.startswith(b+'/') or b.startswith(a+'/')

def load(root=ROOT):return read_json(root/'plan.json')
def active(p:dict,cond:str,profile:str)->bool:
 if profile not in p['profiles']: raise PlanError('UNKNOWN_PROFILE:'+profile)
 if cond not in p['conditions']: raise PlanError('UNKNOWN_CONDITION:'+cond)
 return cond=='ALWAYS' or cond in p['profiles'][profile]

def phase_graph(p:dict,profile:str)->dict:
 ids=[s['id'] for s in p['stories']];ph=p['phaseOrder']
 graph={i+'@'+stage:set() for i in ids for stage in ph}
 for s in p['stories']:
  for before,after in zip(ph,ph[1:]):graph[s['id']+'@'+after].add(s['id']+'@'+before)
  for d in s['requires']:
   if active(p,d['condition'],profile):graph[s['id']+'@'+d['consumerStage']].add(d['producer']+'@'+d['producerStage'])
 return graph

def candidate_order(p:dict,profile='fixture')->list[str]:
 """Linear extension of candidate prerequisites only; runtime readiness is separate."""
 S={s['id']:s for s in p['stories']}
 g={i:set() for i in S}
 for s in S.values():
  for d in s['requires']:
   if d['consumerStage']=='candidate' and active(p,d['condition'],profile):g[s['id']].add(d['producer'])
 result=[]
 while g:
  ready=sorted((k for k,v in g.items() if not v),key=lambda k:(S[k]['orderRank'],k))
  if not ready:raise PlanError('CANDIDATE_CYCLE')
  k=ready[0];result.append(k);del g[k]
  for v in g.values():v.discard(k)
 return result

def ancestors(p:dict,profile='fixture'):
 S={s['id']:s for s in p['stories']};out={}
 for i in candidate_order(p,profile):
  direct={d['producer'] for d in S[i]['requires'] if d['consumerStage']=='candidate' and active(p,d['condition'],profile)}
  out[i]=set(direct)
  for x in direct:out[i]|=out[x]
 return out

def validate_plan(p:dict,root=ROOT)->list[str]:
 errors=[]
 def err(s): errors.append(s)
 for k,v in [('developmentMode','GREENFIELD'),('platform','UNITY_NATIVE_PC'),('presentation','UGUI_TMP_INPUT_SYSTEM')]:
  if p.get(k)!=v:err('FIXED_PRODUCT_CONSTRAINT:'+k)
 if p.get('productModes')!=['TUTORIAL','RANDOM_OPERATIONS_LAB']:err('PRODUCT_MODES_CHANGED')
 if p.get('implementationStatus')!='NOT_STARTED_BY_THIS_DELIVERABLE':err('PLAN_IS_NOT_EXECUTION')
 if p.get('planningPolicy',{}).get('executionAuthorizedByPlan') is not False:err('PLAN_GRANTED_AUTHORITY')
 schema=read_json(root/'schemas/story.schema.json');v=Draft202012Validator(schema,format_checker=FormatChecker())
 S={};paths=defaultdict(list);all_artifacts={};criterion_ids=set()
 taskmap={t['id']:t for t in read_json(root/'basis/v3/contracts/tasks.json')['tasks']}
 reqmap={q['id']:q for q in read_json(root/'basis/v3/contracts/requirements.json')['requirements']}
 for s in p['stories']:
  for x in v.iter_errors(s):err('STORY_SCHEMA:'+s.get('id','?')+':'+'.'.join(map(str,x.path))+':'+x.message)
  if s['id'] in S:err('DUPLICATE_STORY:'+s['id'])
  S[s['id']]=s
  if s['parentTaskId'] not in taskmap:err('UNKNOWN_PARENT:'+s['id']);continue
  if s['epicId']!=taskmap[s['parentTaskId']]['epicId']:err('WRONG_EPIC:'+s['id'])
  if s['requirementIds']!=taskmap[s['parentTaskId']]['requirementIds']:err('REQUIREMENT_DRIFT:'+s['id'])
  if s['supportsRequirementIds']!=taskmap[s['parentTaskId']].get('supportsRequirementIds',[]):err('SUPPORT_REQUIREMENT_DRIFT:'+s['id'])
  if not s['requirementIds'] and not s['supportsRequirementIds']:err('NO_REQUIREMENT_LINK:'+s['id'])
  for q in s['requirementIds']:
   if q not in reqmap:err('UNKNOWN_REQUIREMENT:'+q)
  for a in s['outputs']:
   if a['id'] in all_artifacts:err('DUPLICATE_ARTIFACT:'+a['id'])
   all_artifacts[a['id']]=s['id']
   if a['producer']!=s['id']:err('WRONG_OUTPUT_PRODUCER:'+a['id'])
   if a['paths']!=[w['path'] for w in s['writes']]:err('OUTPUT_PATH_DRIFT:'+s['id'])
  for a in s['acceptance']:
   if a['id'] in criterion_ids:err('DUPLICATE_CRITERION:'+a['id'])
   criterion_ids.add(a['id'])
   for f in ['given','when','then']:
    if not a[f].strip():err('EMPTY_ACCEPTANCE:'+a['id'])
  for w in s['writes']:
   try:paths[canonical_path(w['path'])].append((s['id'],w))
   except PlanError as e:err(str(e))
  for ref in s['requiredReads']:
   try:
    file=safe_path(root,ref['path'])
    if not file.is_file():err('MISSING_SPEC:'+ref['path'])
    elif hashlib.sha256(file.read_bytes()).hexdigest()!=ref['sha256']:err('SPEC_DIGEST_DRIFT:'+ref['path'])
   except PlanError as e:err(str(e))
  for ref in s['sourceRefs']:
   try:
    source=read_json(safe_path(root,ref['record']))
    records=source.get('sources',source.get('assets',[]));r=next((x for x in records if x['id']==ref['sourceId']),None)
    if r is None:err('MISSING_SOURCE:'+ref['sourceId'])
    elif (r.get('url') or r.get('sourceUrl'))!=ref['url']:err('SOURCE_URL_DRIFT:'+ref['sourceId'])
   except (OSError,PlanError) as e:err('SOURCE_ERROR:'+str(e))
  if not s['parentProductTestIds']==[reqmap[q]['acceptanceTestId'] for q in dict.fromkeys(s['requirementIds']+s['supportsRequirementIds']) if q in reqmap]:err('PRODUCT_TEST_MAPPING:'+s['id'])
  for e in s['externalInputs']:
   if e['condition'] not in p['conditions']:err('UNKNOWN_EXTERNAL_CONDITION:'+s['id'])
 for s in S.values():
  for d in s['requires']:
   if d['producer'] not in S:err('MISSING_PRODUCER:'+s['id']);continue
   if all_artifacts.get(d['artifactId'])!=d['producer']:err('DEPENDENCY_OUTPUT_OWNER:'+s['id'])
   if d['producerStage'] not in p['phaseOrder'] or d['consumerStage'] not in p['phaseOrder']:err('INVALID_STAGE:'+s['id'])
   if d['condition'] not in p['conditions']:err('UNKNOWN_CONDITION:'+s['id'])
 if {s['parentTaskId'] for s in S.values()}!=set(taskmap):err('PARENT_COVERAGE')
 if {q for s in S.values() for q in s['requirementIds']}!=set(reqmap):err('REQUIREMENT_COVERAGE')
 # Repeated file edits must have explicit candidate ordering; .meta and Windows aliases count.
 try:
  an=ancestors(p)
  for path,rs in paths.items():
   creators={w['createdBy'] for _,w in rs}
   if len(creators)!=1:err('MULTIPLE_CREATORS:'+path)
   for i,(a,wa) in enumerate(rs):
    if wa['createdBy'] not in S:err('UNKNOWN_FILE_CREATOR:'+path)
    for b,wb in rs[i+1:]:
     if a!=b and a not in an[b] and b not in an[a]:err('UNORDERED_SHARED_WRITE:'+a+':'+b+':'+path)
 except (PlanError,KeyError) as e:err('ORDER_ERROR:'+str(e))
 for profile in p['profiles']:
  try:
   graph=phase_graph(p,profile)
   known=set(graph)
   if any(x not in known for ds in graph.values() for x in ds):err('GRAPH_MISSING_PHASE:'+profile)
   tuple(TopologicalSorter(graph).static_order())
  except (CycleError,KeyError,PlanError) as e:err('PHASE_GRAPH:'+profile+':'+str(e))
 horizon=[]
 for w in p['windows']:
  if w['startDate'] is not None or w['endDate'] is not None or w['capacityHours'] is not None:err('UNJUSTIFIED_CALENDAR:'+w['id'])
  for i in w['storyIds']:
   if i not in S:err('UNKNOWN_WINDOW_STORY:'+i)
   elif S[i]['window']!=w['id']:err('WINDOW_DRIFT:'+i)
   horizon.append(i)
 if len(set(horizon))!=len(horizon):err('WINDOW_DUPLICATE')
 for i in horizon:
  if i not in S:continue
  for d in S[i]['requires']:
   if d['consumerStage'] in ['candidate','integration'] and d['condition']=='ALWAYS' and d['producer'] not in horizon:err('HORIZON_NOT_CLOSED:'+i+':'+d['producer'])
 pg={g['parentTaskId']:g for g in p['parentGates']}
 for k in taskmap:
  if k not in pg or set(pg[k]['children'])!={s['id'] for s in S.values() if s['parentTaskId']==k}:err('PARENT_GATE_DRIFT:'+k)
 return errors

def verify_file(root:Path,entry:dict)->bool:
 try:
  p=safe_path(root,entry['path'])
  return p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest()==entry['sha256']
 except (PlanError,OSError,KeyError):return False

def validate_state(p:dict,state:dict,root=ROOT)->list[str]:
 errors=[];S={s['id']:s for s in p['stories']};known_records={};external_ids={e['id'] for s in S.values() for e in s['externalInputs']}
 for e in Draft202012Validator(read_json(root/'schemas/progress.schema.json'),format_checker=FormatChecker()).iter_errors(state):errors.append('STATE_SCHEMA:'+e.message)
 if errors:return errors
 for r in state['records']:
  if r['id'] in known_records:errors.append('DUPLICATE_RECEIPT:'+r['id'])
  known_records[r['id']]=r
  if r['storyId'] not in S:errors.append('UNKNOWN_RECEIPT_STORY');continue
  if r['planDigest']!=digest(p) or r['storyDigest']!=digest(S[r['storyId']]):errors.append('STALE_RECEIPT:'+r['id'])
  if r['independentReview'] and r['executorId']==r['reviewerId']:errors.append('FALSE_INDEPENDENT_REVIEW:'+r['id'])
  if r['status']=='ACCEPTED':
   t=r['testResult']
   if t['executed']<1 or t['passed']!=t['executed'] or t['failed'] or t['notRun']:errors.append('UNTESTED_ACCEPTANCE:'+r['id'])
   if r['phase']=='qualification' and not r['independentReview']:errors.append('INDEPENDENT_REVIEW_REQUIRED:'+r['id'])
   for f in r['evidenceFiles']+r['artifacts']:
    if not verify_file(root,f):errors.append('MISSING_OR_CHANGED_EVIDENCE:'+r['id'])
   outputs={x['id'] for x in S[r['storyId']]['outputs']}
   if any(x['artifactId'] not in outputs for x in r['artifacts']):errors.append('WRONG_RECEIPT_ARTIFACT:'+r['id'])
 # Validate declared input receipt chain for the same profile; no fixture-to-field upgrade.
 for r in state['records']:
  if r['status']!='ACCEPTED' or r['storyId'] not in S:continue
  if r['phase'] in ['integration','qualification']:
   prev='candidate' if r['phase']=='integration' else 'integration'
   if not any(x['id'] in r['inputReceiptIds'] and x['status']=='ACCEPTED' and x['storyId']==r['storyId'] and x['phase']==prev and x['profile']==r['profile'] for x in state['records']):errors.append('UNPROVEN_OWN_STAGE:'+r['id'])
  for e in S[r['storyId']]['externalInputs']:
   if e['requiredAt']==r['phase'] and active(p,e['condition'],r['profile']):
    if not any(x['id']==e['id'] and x['state']=='SATISFIED' and x['evidenceFiles'] and all(verify_file(root,f) for f in x['evidenceFiles']) for x in state['externalInputs']):errors.append('UNPROVEN_EXTERNAL:'+r['id'])
  for inp in r['inputReceiptIds']:
   if inp not in known_records:errors.append('MISSING_INPUT_RECEIPT:'+r['id'])
  for d in S[r['storyId']]['requires']:
   if d['consumerStage']!=r['phase'] or not active(p,d['condition'],r['profile']):continue
   matches=[known_records[x] for x in r['inputReceiptIds'] if x in known_records]
   if not any(x['status']=='ACCEPTED' and x['storyId']==d['producer'] and x['phase']==d['producerStage'] and x['profile']==r['profile'] for x in matches):errors.append('UNPROVEN_INPUT:'+r['id'])
 seen_external=set()
 for e in state['externalInputs']:
  if e['id'] in seen_external:errors.append('DUPLICATE_EXTERNAL:'+e['id'])
  seen_external.add(e['id'])
  if e['id'] not in external_ids:errors.append('UNKNOWN_EXTERNAL_INPUT:'+e['id'])
  if e['state']=='SATISFIED' and (not e['evidenceFiles'] or not all(verify_file(root,f) for f in e['evidenceFiles'])):errors.append('EXTERNAL_WITHOUT_EVIDENCE:'+e['id'])
 accepted_slots=set()
 receipt_graph={r['id']:set(r['inputReceiptIds']) for r in state['records']}
 try:tuple(TopologicalSorter(receipt_graph).static_order())
 except CycleError:errors.append('RECEIPT_CYCLE')
 for r in state['records']:
  if r['status']=='ACCEPTED':
   slot=(r['storyId'],r['phase'],r['profile'])
   if slot in accepted_slots:errors.append('AMBIGUOUS_ACCEPTED_RECEIPT:'+r['storyId'])
   accepted_slots.add(slot)
 for c in state['claims']:
  if c['storyId'] not in S:errors.append('UNKNOWN_CLAIM_STORY')
  for f in c['paths']:
   try:canonical_path(f)
   except PlanError:errors.append('BAD_CLAIM_PATH')
 return errors

def readiness(p:dict,state:dict,story_id:str,phase:str,profile:str,root=ROOT)->dict:
 S={s['id']:s for s in p['stories']}
 if story_id not in S:raise PlanError('UNKNOWN_STORY:'+story_id)
 if phase not in p['phaseOrder']:raise PlanError('UNKNOWN_PHASE:'+phase)
 if profile not in p['profiles']:raise PlanError('UNKNOWN_PROFILE:'+profile)
 state_errors=validate_state(p,state,root)
 if state_errors:return {'status':'INVALID_STATE','reasons':state_errors,'executionAuthorized':False}
 s=S[story_id];reasons=[]
 records=state['records'];existing=[r for r in records if r['storyId']==story_id and r['phase']==phase and r['profile']==profile and r['status']=='ACCEPTED']
 if existing:return {'status':'EVIDENCE_RECORDED','reasons':[],'executionAuthorized':False,'caveat':'실제 테스트의 진실성과 검수 권한은 담당자가 확인해야 한다.'}
 for d in s['requires']:
  if d['consumerStage']==phase and active(p,d['condition'],profile):
   if not any(r['status']=='ACCEPTED' and r['storyId']==d['producer'] and r['phase']==d['producerStage'] and r['profile']==profile for r in records):reasons.append('MISSING_ARTIFACT:'+d['artifactId']+'@'+d['producerStage'])
 if phase in ['integration','qualification']:
  previous='candidate' if phase=='integration' else 'integration'
  if not any(r['status']=='ACCEPTED' and r['storyId']==story_id and r['phase']==previous and r['profile']==profile for r in records):reasons.append('MISSING_OWN_STAGE:'+previous)
 for e in s['externalInputs']:
  if e['requiredAt']==phase and active(p,e['condition'],profile):
   if not any(x['id']==e['id'] and x['state']=='SATISFIED' for x in state['externalInputs']):reasons.append('MISSING_EXTERNAL:'+e['id'])
 if phase!='prepare':
  for c in state['claims']:
   if c['status']=='ACTIVE' and c['storyId']!=story_id:
    if any(path_overlap(x['path'],y) for x in s['writes'] for y in c['paths']):reasons.append('ACTIVE_WRITE_CONFLICT:'+c['id'])
    if 'UNITY_EDITOR_SINGLE_INSTANCE' in c['resourceIds'] and s['epicId'] in ['CS-BOOT','CS-PLAY','CS-WORLD']:reasons.append('ACTIVE_RESOURCE_CONFLICT:'+c['id'])
 return {'status':('HOLD_SPECIFIC_PHASE' if reasons else 'ELIGIBLE_ON_RECORDED_INPUTS_NOT_AUTHORIZED'),'reasons':reasons,'executionAuthorized':False,
 'trustBoundary':'입력파일 일치·제출된 검토기록만 검사한다. 도구가 실제 실행·독립심사·권한 진실성을 증명하거나 잠금을 획득하지 않는다.'}

def parallel(p:dict,a:str,b:str,profile='fixture')->dict:
 S={s['id']:s for s in p['stories']};an=ancestors(p,profile)
 if a not in S or b not in S:raise PlanError('UNKNOWN_STORY')
 reasons=[]
 if a==b:reasons.append('SAME_STORY')
 if a in an[b] or b in an[a]:reasons.append('CANDIDATE_DEPENDENCY')
 overlap=[x['path'] for x in S[a]['writes'] for y in S[b]['writes'] if path_overlap(x['path'],y['path'])]
 if overlap:reasons.append('WRITE_CONFLICT')
 return {'a':a,'b':b,'parallelCandidate':not reasons,'reasons':reasons,'overlappingPaths':sorted(set(overlap)),
 'authorization':False,'extraChecks':['해당 profile/phase 실제 artifact','작업별 checkout·Editor·빌드폴더·DB 분리','active claim·개인 능력·WIP']}


def context_packet(p:dict,state:dict,story_id:str,phase='candidate',profile='fixture',root=ROOT)->dict:
 S={s['id']:s for s in p['stories']}
 if story_id not in S:raise PlanError('UNKNOWN_STORY:'+story_id)
 s=S[story_id]
 return {'complete':True,'requestedPhase':phase,'profile':profile,'storyDigest':digest(s),'planDigest':digest(p),
   'readiness':readiness(p,state,story_id,phase,profile,root),
   'fixedConstraints':{'greenfield':True,'platform':p['platform'],'presentation':p['presentation'],'modes':p['productModes'],'noLegacyEpicOrCodeDependency':True,'readinessIsNotAuthority':True},
   'story':s,'nextReads':s['specRefs'],'sourceContractsInline':False,
   'note':'complete는 이 packet이 잘리지 않았다는 뜻이다. 연결된 specRefs를 읽고 실제 artifact·경로 권한을 확인해야 실행 가능하다.'}
