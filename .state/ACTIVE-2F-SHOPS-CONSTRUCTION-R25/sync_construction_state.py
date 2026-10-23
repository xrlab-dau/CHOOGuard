import json,pathlib,datetime,argparse
p=argparse.ArgumentParser();p.add_argument('--complete',action='append',default=[]);p.add_argument('--active',nargs='*',required=True);a=p.parse_args();B=pathlib.Path(__file__).parent;now=datetime.datetime.now(datetime.timezone.utc).isoformat()
def read(p):return json.loads(p.read_text())
def write(p,j):p.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n')
idx=read(B/'ACTUAL-SAVED-STORES-INDEX-R25.json');q=read(B/'CONSTRUCTION-QUEUE-R25.json')
for key in a.complete:
 paths=sorted((B/key).rglob('APPLIED-*.json'),key=lambda p:p.stat().st_mtime);rs=[]
 for path in paths:
  j=read(path);status=j.get('status','')
  if 'SAVED' not in status.upper():continue
  rs.append({'path':str(path),'utc':j.get('utc'),'status':status,'scene_sha256_after':j.get('scene_sha256_after')})
 if not rs:raise RuntimeError('No actual saved receipt: '+key)
 idx['stores'][key]={'status':'SOURCE_BOUND_PHYSICAL_MODELS_AND_SOLID_SIGNS_APPLIED_SAVED','actual_receipts':rs}
for s in q['stores']:
 k=s['store_key']
 if k in idx['stores']:
  s.update(status='ACTUALLY_APPLIED_AND_SAVED',actual_initial_receipt=str(B/k/'APPLIED-STORE-R25.json'),initial_receipt=str(B/k/'APPLIED-STORE-R25.json'))
 elif k in a.active:s['status']='ACTIVE_DEDICATED_SOURCE_NATIVE_CONSTRUCTOR'
 elif k in q.get('source_binding_unresolved_store_keys',[]):s['status']='SOURCE_BINDING_UNRESOLVED_NOT_APPLIED'
 else:s['status']='WAITING_DEDICATED_SOURCE_AND_CAD_CONSTRUCTOR'
n=len(idx['stores']);pending=sum(s['status']=='WAITING_DEDICATED_SOURCE_AND_CAD_CONSTRUCTOR' for s in q['stores'])
idx.update(utc=now,other_current_constructor_lanes=a.active,actual_initialized_store_count=n);q.update(utc=now,actual_saved_count=n,active_count=len(a.active),pending_count=pending,unresolved_source_count=len(q.get('source_binding_unresolved_store_keys',[])))
write(B/'ACTUAL-SAVED-STORES-INDEX-R25.json',idx);write(B/'CONSTRUCTION-QUEUE-R25.json',q)
p=pathlib.Path('.state/CURRENT-2F-SHOPS-CONSTRUCTION-R25.json');d=read(p);d.update(utc=now,actual_initialized_store_count=n,active_constructor_lanes=a.active,pending_store_count=pending,unresolved_source_store_keys=q.get('source_binding_unresolved_store_keys',[]));write(p,d)
p=B/'task_plan.md';lines=p.read_text().splitlines();lines=[f'3. IN_PROGRESS remaining user-selected red neighborhoods: {n} stores actually applied/saved; active dedicated constructors {", ".join(a.active)}; {pending} selected neighborhoods queued; source-unresolved {q.get("source_binding_unresolved_store_keys", [])}. Exclude four unmarked older proposals.' if l.startswith('3. ') else l for l in lines];p.write_text('\n'.join(lines)+'\n')
with (B/'progress.md').open('a') as f:f.write(f'\n{now}: Integrated saved receipts {a.complete}; actual saved {n}, active {a.active}, queued {pending}. No new user confirmation/QA/99percent claim.\n')
print(json.dumps({'saved':n,'active':a.active,'pending':pending},ensure_ascii=False))
