"""Read one new task. Does not grant execution authority or run Unity."""
from __future__ import annotations
import argparse,json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('task');p.add_argument('--max-bytes',type=int,default=40000);args=p.parse_args()
    if args.max_bytes<1: p.error('--max-bytes must be positive')
    tasks=json.loads((ROOT/'contracts/tasks.json').read_text(encoding='utf-8'))['tasks']
    task=next((x for x in tasks if x['id']==args.task),None)
    if task is None:
        print(json.dumps({'complete':False,'error':'UNKNOWN_TASK','validTasks':[t['id'] for t in tasks]},ensure_ascii=False));return 2
    reqs=json.loads((ROOT/'contracts/requirements.json').read_text(encoding='utf-8'))['requirements']
    sources=json.loads((ROOT/'reference/sources.json').read_text(encoding='utf-8'))['sources']
    out={'complete':True,'purpose':'READ_ONLY_NEW_TASK_CONTEXT','task':task,
         'requirements':[r for r in reqs if r['id'] in task['requirementIds']],
         'sources':[s for s in sources if s['id'] in task['sourceIds']],
         'mustAlsoRead':['PRODUCT_BASELINE.md','CONTRACTS.md'],
         'executionAuthorityGranted':False,'productImplemented':False}
    text=json.dumps(out,ensure_ascii=False,indent=2)
    n=len(text.encode('utf-8'))
    if n>args.max_bytes:
        print(json.dumps({'complete':False,'reason':'BUDGET_EXCEEDED_NOT_TRUNCATED','requiredBytes':n,
                          'retry':f'python tools/task_context.py {args.task} --max-bytes {n}'},ensure_ascii=False));return 3
    print(text);return 0
if __name__=='__main__':sys.exit(main())
