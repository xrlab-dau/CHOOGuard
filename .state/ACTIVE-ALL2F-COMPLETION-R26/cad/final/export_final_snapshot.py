#!/usr/bin/env python3
"""Export actual final R26 CAD source only after explicit root writers-settled trigger."""
import json, subprocess, sys
from pathlib import Path
ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad'
OWN=BASE/'final'
TAG='007'
def main():
 trigger=OWN/'ALL-WRITERS-SETTLED-TRIGGER.json'
 if not trigger.is_file() or json.loads(trigger.read_text()).get('root_explicit_message')!='ALL_WRITERS_SETTLED':
  raise SystemExit('Waiting for explicit root ALL_WRITERS_SETTLED; no export queued')
 name='r26-final-current2f-cad-source-'+TAG
 snapshot=BASE/('snapshot-'+TAG)
 request=ROOT/'.state/SCENE-WORKER-C180/requests'/(name+'.json')
 if request.exists() or snapshot.exists():raise SystemExit('Final source tag already exists; never replay a frozen request')
 cmd=[sys.executable,str(ROOT/'.state/SCENE-WORKER-C180/active-broker/scene_worker.py'),'run','--thread','classic:/root/r26_final_cad','--scope','C180_R26_FINAL_CAD_SNAPSHOT_'+TAG,'--file',str((OWN/'ExportFinal2FGeometryR26.cs').relative_to(ROOT)),'--entry','ExportFinal2FGeometryR26.Read','--name',name,'--timeout-ms','600000']
 cp=subprocess.run(cmd,cwd=ROOT)
 raise SystemExit(cp.returncode)
if __name__=='__main__':main()
