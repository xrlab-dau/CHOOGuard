#!/usr/bin/env python3
"""Stop only task-local backend/bridge processes whose recorded PIDs still match their commands."""
import json
from pathlib import Path
import os
import signal
import subprocess
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'output/simulator/model-benchmark'
for receipt,needle in [('server-process.json','workers/simulator/model_service/launch.py'),('bridge-process.json','workers/simulator/model_service/bridge.py')]:
    path=OUT/receipt
    if not path.exists():continue
    record=json.loads(path.read_text());pid=record['pid']
    query=subprocess.run(['ps','-p',str(pid),'-o','command='],capture_output=True,text=True)
    if query.returncode or needle not in query.stdout:
        print(f'{receipt}: already stopped or PID no longer matches');continue
    os.kill(pid,signal.SIGTERM);record['stopped_by_task_helper']=True;path.write_text(json.dumps(record,indent=2)+'\n');print(f'Stopped owned PID{pid}')
