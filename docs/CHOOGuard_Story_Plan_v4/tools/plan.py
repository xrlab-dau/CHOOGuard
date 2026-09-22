#!/usr/bin/env python3
"""Read-only CLI. Run from this package; never executes product work."""
import argparse,json,sys
from pathlib import Path
from planlib import ROOT,load,read_json,digest,validate_plan,validate_state,readiness,candidate_order,parallel,context_packet,PlanError

ACTIVE_PLAN='docs/CHOOGuard_Story_Plan_v5/plan.json'
def active_plan_exists():
 return (ROOT.parents[1]/ACTIVE_PLAN).is_file()

def main():
 ap=argparse.ArgumentParser();sub=ap.add_subparsers(dest='cmd',required=True)
 sub.add_parser('validate')
 b=sub.add_parser('brief');b.add_argument('story');b.add_argument('--phase',choices=['prepare','candidate','integration','qualification'],default='candidate');b.add_argument('--profile',default='fixture');b.add_argument('--max-bytes',type=int,default=24000)
 o=sub.add_parser('order');o.add_argument('--profile',default='fixture');o.add_argument('--window');o.add_argument('--all',action='store_true')
 n=sub.add_parser('next');n.add_argument('--phase',choices=['prepare','candidate','integration','qualification'],default='prepare');n.add_argument('--profile',default='fixture');n.add_argument('--limit',type=int,default=5)
 q=sub.add_parser('parallel');q.add_argument('first');q.add_argument('second');q.add_argument('--profile',default='fixture')
 args=ap.parse_args();legacy_only=active_plan_exists()
 if legacy_only and args.cmd in ('next','order','parallel'):
  print(json.dumps({'error':'LEGACY_EXECUTION_DISABLED','mode':'LEGACY_REFERENCE_ONLY','activePlan':ACTIVE_PLAN,'executionAuthorized':False},ensure_ascii=False));return 2
 p=load();state=read_json(ROOT/'state/progress.json');S={s['id']:s for s in p['stories']}
 if args.cmd=='validate':
  e=validate_plan(p)+validate_state(p,state)
  payload={'valid':not e,'errors':e,'stories':len(S),'parents':len(p['parentGates']),'windows':len(p['windows']),'scope':'PLAN_AND_RECEIPT_STRUCTURE_NOT_PRODUCT_TESTS','planDigest':digest(p),'runtimeEvidenceRecords':len(state['records'])};code=1 if e else 0
 elif args.cmd=='brief':
  if args.story not in S:raise PlanError('UNKNOWN_STORY:'+args.story)
  s=S[args.story]
  payload=context_packet(p,state,args.story,args.phase,args.profile)
  if legacy_only:payload.update(mode='LEGACY_REFERENCE_ONLY',activePlan=ACTIVE_PLAN)
  encoded=json.dumps(payload,ensure_ascii=False,indent=2).encode()
  if args.max_bytes<0:raise PlanError('NEGATIVE_BUDGET')
  if len(encoded)>args.max_bytes:
   payload={'complete':False,'reason':'CONTEXT_BUDGET_EXCEEDED','requiredBytes':len(encoded),'budgetBytes':args.max_bytes,'nothingSilentlyTruncated':True,
    'nextCommand':f'python tools/plan.py brief {args.story} --phase {args.phase} --profile {args.profile} --max-bytes {len(encoded)+256}'};code=3
  else:code=0
 elif args.cmd=='order':
  order=candidate_order(p,args.profile);hs={i for w in p['windows'] for i in w['storyIds']}
  if args.window:
   if args.window not in {w['id'] for w in p['windows']}:raise PlanError('UNKNOWN_WINDOW:'+args.window)
   order=[i for i in order if S[i]['window']==args.window]
  elif not args.all:order=[i for i in order if i in hs]
  payload={'scope':'PLANNED_CANDIDATE_ORDER_NOT_READY_QUEUE','profile':args.profile,'calendarPromise':False,
   'items':[{'id':i,'title':S[i]['title'],'window':S[i]['window']} for i in order],
   'rule':'정렬은 개발 후보 순서다. 각 story의 integration prerequisites·실제 증거·claims는 따로 확인한다.'};code=0
 elif args.cmd=='next':
  if args.limit<1:raise PlanError('INVALID_LIMIT')
  rows=[]
  for i in candidate_order(p,args.profile):
   r=readiness(p,state,i,args.phase,args.profile)
   if r['status']=='ELIGIBLE_ON_RECORDED_INPUTS_NOT_AUTHORIZED':rows.append({'storyId':i,'title':S[i]['title'],'window':S[i]['window'],'readiness':r})
   if len(rows)>=args.limit:break
  payload={'phase':args.phase,'profile':args.profile,'recommendations':rows,'automaticallyStarted':False,'stateHasProductEvidence':bool(state['records'])};code=0
 else:payload=parallel(p,args.first,args.second,args.profile);code=0
 if legacy_only:payload.update(mode='LEGACY_REFERENCE_ONLY',activePlan=ACTIVE_PLAN)
 print(json.dumps(payload,ensure_ascii=False,indent=2));return code
if __name__=='__main__':
 try:sys.exit(main())
 except (PlanError,KeyError,ValueError,OSError) as e:
  print(json.dumps({'error':str(e),'complete':False,'executionAuthorized':False},ensure_ascii=False));sys.exit(2)
