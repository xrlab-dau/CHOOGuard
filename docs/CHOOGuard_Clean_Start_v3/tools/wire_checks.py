"""Executable schema and semantic examples, not the Unity production validation layer."""
from pathlib import Path
from jsonschema import Draft202012Validator, FormatChecker
import json,re
ROOT=Path(__file__).resolve().parents[1]
def load_example(name):return json.loads((ROOT/'examples'/(name+'.json')).read_text())
def check(name,value,job=None):
 schema=json.loads((ROOT/'schemas'/(name+'.schema.json')).read_text());Draft202012Validator.check_schema(schema)
 errors=[e.json_path+': '+e.message for e in Draft202012Validator(schema,format_checker=FormatChecker()).iter_errors(value)]
 if errors:return errors
 def require(ok,msg):
  if not ok:errors.append(msg)
 if name=='CommandIntent':
  for k in ['actingTeamIds','targetIds']:require(len(value[k])==len(set(value[k])),'duplicate '+k)
  ids=[x['entityId'] for x in value['readSet']];require(len(ids)==len(set(ids)),'duplicate readSet')
 if name=='CommitBatch':
  require(value['receipt']['key']['runId']==value['runId'],'receipt run mismatch')
  if value['receipt']['status']=='ACCEPTED':require(value['receipt']['sequence'] is not None and value['receipt']['sequence']>value['expectedRevision'],'accepted receipt revision')
 if name=='ReceiptLookup':require(value['found']==(value['receipt'] is not None),'lookup presence mismatch')
 if name=='CommitReceipt':
  receipt=value['intentReceipt'];require(receipt['key']['runId']==value['runId'] and receipt['commitId']==value['commitId'] and receipt['sequence']==value['revision'],'commit receipt identity mismatch')
 if name=='CommandReceipt' and value['status']=='ACCEPTED':require(bool(value['commitId']) and value['sequence'] is not None,'accepted without durable identity')
 if name=='SimulationJob':require(value['endTickUs']>value['startTickUs'],'nonpositive interval')
 if name=='FieldBatch':
  require(value['validToTickUs']>value['validFromTickUs'],'invalid field interval')
  if job:
   for k in ['jobId','runId','generation','workerId','modelRef','inputDigest','boundaryRevision']:require(value[k]==job[k],'correlation '+k)
   require(value['validFromTickUs']==job['startTickUs'] and value['validToTickUs']==job['endTickUs'],'boundary interval mismatch')
   expected=job['outputContract']
   for k,target in [('fieldId','fieldId'),('fieldOwner','ownerWorkerId'),('unit','unit'),('frameId','frameId')]:require(value[k]==expected[target],'field contract mismatch '+k)
 if name=='Checkpoint':
  ws=value['workerStates'];ids=[w['workerId'] for w in ws]
  require(len(ids)==len(set(ids)),'duplicate worker state');require(len(value['requiredWorkers'])==len(set(value['requiredWorkers'])),'duplicate required worker');require(set(ids)==set(value['requiredWorkers']),'missing/unexpected worker')
  for w in ws:require(w['tickUs']==value['tickUs'] and w['cutSequence']==value['cutSequence'],'worker checkpoint cut mismatch')
 if name=='BranchRequest':require(value['newRunId']!=value['parentRunId'],'parent cannot be child')
 if name=='ActivityInterval':
  if value['completeness']=='COMPLETE':require(value['endMonoUs'] is not None and value['endMonoUs']>=value['startMonoUs'],'complete interval invalid')
  else:require(value['endMonoUs'] is None,'censored interval must not invent end')
 if name=='EvidenceRecord':
  if value['result']=='NOT_RUN':require(value['executedAt'] is None and bool(value['notRunReason']),'NOT_RUN has execution/needs reason')
  if value['result']=='PASSED_WITH_SCOPE':require(bool(value['executedAt']) and bool(value['outputRefs']) and value['notRunReason'] is None,'pass lacks execution evidence')
 if name=='ScriptIR':
  for s in value['steps']:
   if s['claimType'] in ['OBSERVED_IN_RUN','MANUAL_SUPPORTED_PROPOSAL','REVIEWED_INSTRUCTION']:require(bool(s['evidenceRefs']),'claim lacks evidence')
   if s['claimType']=='REVIEWED_INSTRUCTION':require(value['reviewRef'] is not None,'instruction lacks review')
 if name in ['SessionProjection','ProjectionQuery']:require((value['viewScope']=='AGENCY_KNOWLEDGE')==(value['agencyId'] is not None),'agency view binding')
 return errors

def sqlite_release_allowed(version):
 try:v=tuple(map(int,version.split('.')))
 except ValueError:return False
 return len(v)==3 and (v>=(3,51,3) or v[:2]==(3,44) and v[2]>=6 or v[:2]==(3,50) and v[2]>=7)

def union_duration(intervals):
 intervals=sorted(intervals);total=0;end=None
 for start,stop in intervals:
  if start<0 or stop<start:raise ValueError('invalid interval')
  if end is None or start>=end:total+=stop-start
  elif stop>end:total+=stop-end
  end=max(end or 0,stop)
 return total
