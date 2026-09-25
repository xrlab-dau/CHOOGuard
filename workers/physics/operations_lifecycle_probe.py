#!/usr/bin/env python3
"""Focused live JSONL lifecycle reproduction, no fixture physics."""
import json,pathlib,subprocess,sys
from runtime_package import launch_worker
root=pathlib.Path(__file__).resolve().parent
p=launch_worker(stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True)
def exchange(value):
 p.stdin.write(json.dumps(value)+'\n');p.stdin.flush();return json.loads(p.stdout.readline())
def command(action,seconds=0,**extra):
 global serial
 serial+=1
 return exchange(dict(kind='SUBMIT',runId=run,generation=1,seed=42,population=50,scenario=scenario,action=action,stepSeconds=seconds,requestId=str(serial),**extra))
def snapshot(r):return [(a['id'],a['x'],a['z'],a['cohort'],a['fedToxic'],a['fedConvectiveHeat']) for a in r['agents']]
serial=0;run='lifecycle-fire';scenario='fire_smoke'
try:
 cap=exchange(dict(kind='HELLO',protocolVersion=1)); assert cap['lifecycleVersion']==1 and cap['fdsAvailable']
 initial=command('start_routine'); assert initial['phase']=='ordinary'
 first=command('advance',1);assert snapshot(initial)!=snapshot(first)
 waiting=False
 for _ in range(120):
  before=command('advance',1);assert before['kind']=='RESULT';waiting|=any(a['routineState']=='waiting' for a in before['agents'])
 assert before['sessionSimTime']>120 and len(before['agents'])==50 and before['incidentTime']==-1 and waiting
 onset=command('begin_incident');assert snapshot(before)==snapshot(onset) and onset['sessionSimTime']==before['sessionSimTime'] and onset['incidentTime']==0 and onset['initialStateDigest']==initial['initialStateDigest']
 for _ in range(120):
  result=command('advance',1);assert result['kind']=='RESULT'
 rejected=command('advance',1); assert rejected['kind']=='ERROR' and 'no extrapolation' in rejected['message']
 run='legacy';scenario='crowd_medical';legacy=command('start');assert legacy['phase']=='incident' and legacy['incidentTime']==0
 result=command('evacuate',0,releasePolicy='all',cohortId=-1)
 for _ in range(180):
  result=command('advance',1)
  if result['phase']=='resolved':break
 assert result['phase']=='resolved' and result['evacuated']==50
 resolved_at=result['simTime']; result=command('advance',1); assert result['phase']=='resolved' and result['simTime']>resolved_at
 print(json.dumps(dict(engine=cap['engineVersion'],population=50,routineMoved=True,dwellObserved=waiting,routineSeconds=before['sessionSimTime'],onsetPreserved=True,incidentBoundarySeconds=120,beyondBoundaryRejected=True,legacyResolvedAt=resolved_at,recoveryTickAccepted=True)))
finally:
 p.terminate();p.wait(timeout=5)
