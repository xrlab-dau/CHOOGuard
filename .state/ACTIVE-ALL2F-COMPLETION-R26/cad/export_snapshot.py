#!/usr/bin/env python3
"""Queue a fresh, uniquely scoped actual geometry snapshot through the existing C180 broker."""
import argparse,json,re,subprocess,sys
from pathlib import Path
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad'
def main():
 p=argparse.ArgumentParser();p.add_argument('--tag',required=True,help='New unique numeric tag, e.g.003');p.add_argument('--queue-only',action='store_true');a=p.parse_args()
 if not re.fullmatch(r'\d{3,6}',a.tag):raise SystemExit('Use a new 3..6 digit tag')
 name=f'r26-current-2f-cad-source-{a.tag}';script=BASE/f'ExportCurrent2FGeometryR26-{a.tag}.cs';snapshot=f'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-{a.tag}'
 if script.exists() or (ROOT/'.state/SCENE-WORKER-C180/requests'/f'{name}.json').exists() or (ROOT/snapshot).exists():raise SystemExit('Tag already exists; never replay/overwrite a prior snapshot request')
 text=(BASE/'ExportCurrent2FGeometryR26.cs').read_text()
 text=re.sub(r'C180_R26_CURRENT_2F_CAD_SNAPSHOT_\d+',f'C180_R26_CURRENT_2F_CAD_SNAPSHOT_{a.tag}',text)
 text=re.sub(r'\.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-\d+',snapshot,text)
 script.write_text(text)
 cmd=[sys.executable,str(ROOT/'.state/SCENE-WORKER-C180/active-broker/scene_worker.py'),'request' if a.queue_only else 'run','--thread','classic:/root/r26_live_cad','--scope',f'C180_R26_CURRENT_2F_CAD_SNAPSHOT_{a.tag}','--file',str(script.relative_to(ROOT)),'--entry','ExportCurrent2FGeometryR26.Read','--name',name,'--timeout-ms','600000']
 cp=subprocess.run(cmd,cwd=ROOT);raise SystemExit(cp.returncode)
if __name__=='__main__':main()
