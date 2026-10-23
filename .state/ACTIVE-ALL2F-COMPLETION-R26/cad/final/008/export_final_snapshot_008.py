#!/usr/bin/env python3
"""Fresh008 construction CAD source export after explicit root R30 writers-settled trigger."""
import json,subprocess,sys
from pathlib import Path
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad';OWN=BASE/'final/008';TAG='008'
def main():
 trigger=OWN/'ALL-WRITERS-SETTLED-008-TRIGGER.json'
 if not trigger.is_file() or json.loads(trigger.read_text()).get('root_explicit_message')!='ALL_WRITERS_SETTLED' or json.loads(trigger.read_text()).get('snapshot_tag')!='008':
  raise SystemExit('Waiting for explicit root ALL_WRITERS_SETTLED for008; no export queued')
 name='r26-final-current2f-cad-source-'+TAG
 if (BASE/('snapshot-'+TAG)).exists() or (ROOT/'.state/SCENE-WORKER-C180/requests'/(name+'.json')).exists():
  raise SystemExit('008 source already exists; never overwrite/replay frozen source')
 cmd=[sys.executable,str(ROOT/'.state/SCENE-WORKER-C180/active-broker/scene_worker.py'),'run','--thread','classic:/root/r26_final_cad','--scope','C180_R26_FINAL_CAD_SNAPSHOT_'+TAG,'--file',str((OWN/'ExportFinal2FGeometryR26_008.cs').relative_to(ROOT)),'--entry','ExportFinal2FGeometryR26_008.Read','--name',name,'--timeout-ms','600000']
 raise SystemExit(subprocess.run(cmd,cwd=ROOT).returncode)
if __name__=='__main__':main()
