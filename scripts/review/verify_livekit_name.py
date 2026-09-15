#!/usr/bin/env python3
"""Reproduce and verify a supervisor-authored diagnostic fix in a temporary macOS source copy."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

BASE='86bb82bf5655562e2577cf2b83553ff261d8d5a6'
SOURCES={'configure_local.py':'3c0b87707fe6aaef3fced49fad1620016da45890c042a5cc2569ce626b953b9f',
         'test_configure_local.py':'ea988b5c0dbdf423b3bf66f8e800bc94dc76083c5f675bd9217363c2db86cc41'}
PATCH='2feeaa76cbe260a24408e31e2199863aee959be6ad85abdf4a2e9089608b8dce'


def run_suite(work,log):
    p=subprocess.run([sys.executable,'-B','-m','unittest','discover','-s',str(work),'-p','test_*.py','-v'],capture_output=True,text=True,timeout=240)
    text=p.stdout+p.stderr
    count=re.search(r'Ran (\d+) tests?',text)
    log.write_text(text.replace(str(work),'<SYNTHETIC_SOURCE>').replace(os.environ.get('RUNNER_TEMP','<NEVER>'),'<TEMP>'),encoding='utf-8')
    return {'exitCode':p.returncode,'testsRun':int(count.group(1)) if count else 0,'logSha256':hashlib.sha256(log.read_bytes()).hexdigest()}


def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--root',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    args=ap.parse_args();out=args.output.resolve();out.mkdir(parents=True,exist_ok=False)
    if sys.platform!='darwin':
        (out/'receipt.json').write_text(json.dumps({'state':'BLOCKED','reason':'macos_required','AAAApproved':False})+'\n');return 2
    proposals=Path(__file__).resolve().parent/'proposals';patch=proposals/'livekit-name-preflight.patch'
    if hashlib.sha256(patch.read_bytes()).hexdigest()!=PATCH:raise ValueError('patch_drift')
    checkout=out/'source';work=checkout/'services/livekit';work.mkdir(parents=True)
    for name,expected in SOURCES.items():
        data=subprocess.check_output(['git','-C',str(args.root),'cat-file','blob',BASE+':services/livekit/'+name],timeout=30)
        if hashlib.sha256(data).hexdigest()!=expected:raise ValueError('source_drift')
        (work/name).write_bytes(data)
    subprocess.run(['git','init','-q',str(checkout)],check=True)
    original=run_suite(work,out/'original-test-log.txt')
    subprocess.run(['git','-C',str(checkout),'apply','--check',str(patch)],check=True)
    subprocess.run(['git','-C',str(checkout),'apply',str(patch)],check=True)
    (work/'test_name_preflight.py').write_bytes((proposals/'test_name_preflight.py').read_bytes())
    changed=hashlib.sha256((work/'configure_local.py').read_bytes()).hexdigest()
    candidate=run_suite(work,out/'candidate-test-log.txt')
    # Revert only our change in the disposable source copy and prove new tests fail.
    subprocess.run(['git','-C',str(checkout),'apply','--reverse',str(patch)],check=True)
    mutation=run_suite(work,out/'reverted-test-log.txt')
    result={'schemaVersion':1,'sourceCommit':BASE,'authorRole':'supervisor','independentWorkerClaim':False,
            'patchSha256':PATCH,'sourceSha256':SOURCES,'candidateModuleSha256':changed,
            'original':original,'candidate':candidate,'reverted':mutation,
            'state':'LOCAL_SCOPE_PASS' if candidate['exitCode']==0 and candidate['testsRun']==62 and original['exitCode']!=0 and mutation['exitCode']!=0 else 'REWORK',
            'AAAApproved':False,'limits':'macOS diagnostic/preflight scope only; original ownership/ACL/rollback checks retained. No real service/network/voice or whole-project acceptance.'}
    (out/'receipt.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result));return 0 if result['state']=='LOCAL_SCOPE_PASS' else 2

if __name__=='__main__':raise SystemExit(main())
