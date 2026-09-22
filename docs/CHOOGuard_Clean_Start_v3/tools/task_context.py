"""Task/phase/profile-scoped specification reader; no scheduling, runtime or authority."""
import argparse,json,sys
from pathlib import Path
from check_plan import ROOT,load

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('task');p.add_argument('--phase',choices=['prepare','candidate','integration','qualification'],default='prepare');p.add_argument('--profile',default='fixture');p.add_argument('--max-bytes',type=int,default=40000);a=p.parse_args()
 if a.max_bytes<1:p.error('--max-bytes must be positive')
 d=load();t=next((x for x in d['tasks'] if x['id']==a.task),None)
 if t is None or a.profile not in d['profiles']['profiles']:
  print(json.dumps({'complete':False,'error':'UNKNOWN_TASK_OR_PROFILE'}));return 2
 flags=set(d['profiles']['profiles'][a.profile])|{'ALWAYS'};active=[]
 if a.phase in ['integration','qualification']:
  for x in t['dependsOn']:
   if x['condition'] in flags and (x['consumerStage']==a.phase or a.phase=='qualification' and x['consumerStage']=='integration'):
    active.append({**x,'artifactRef':x['artifactId']+'.'+x['stage'],'availability':'NOT_PRODUCED_BY_THIS_DOCUMENT'})
 out={'complete':True,'purpose':'SPECIFICATION_CONTEXT_NOT_EXECUTION_PERMISSION','task':t,'phase':a.phase,'profile':a.profile,'activeDependencies':active,'requiredReading':t['contextPaths'],'productRequirements':[r for r in d['requirements'] if r['id'] in t['supportsRequirementIds']],'sources':[s for s in d['sources'] if s['id'] in t['sourceIds']],'executionAuthorityGranted':False,'productImplemented':False,'readiness':'REQUIRES_ACTUAL_INPUT_AND_RESULT_CHECK'}
 text=json.dumps(out,ensure_ascii=False,indent=2);n=len(text.encode('utf-8'))
 if n>a.max_bytes:
  print(json.dumps({'complete':False,'reason':'BUDGET_EXCEEDED_NOT_TRUNCATED','requiredBytes':n,'retry':f'python tools/task_context.py {a.task} --phase {a.phase} --profile {a.profile} --max-bytes {n}'},ensure_ascii=False));return 3
 print(text);return 0
if __name__=='__main__':sys.exit(main())
