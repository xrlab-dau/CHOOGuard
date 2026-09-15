#!/usr/bin/env python3
"""Run original baseline suites with explicit platform/scope and fresh receipts.

This is verification infrastructure, not a model worker or a Unity substitute.
No original source is edited; all results/build products go outside the checkout.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import unittest
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

BASE='86bb82bf5655562e2577cf2b83553ff261d8d5a6'
SUITES={'context':'scripts/context','dev':'scripts/dev','art':'scripts/art',
        'ci':'scripts/ci/tests','livekit':'services/livekit'}


def test_state(code,total,passed,failed,skipped):
    if code or failed: return 'FAIL'
    if total<=0 or passed+skipped!=total: return 'BLOCKED'
    return 'PASS_WITH_SKIPS' if skipped else 'PASS'


def csharp_files(root):
    package=root/'Packages/com.xrlab.chooguard.foundation'
    runtime=sorted((package/'Runtime').rglob('*.cs'))
    if not runtime: raise ValueError('runtime_absent')
    if any('UnityEngine' in f.read_text(encoding='utf-8') or 'UnityEditor' in f.read_text(encoding='utf-8') for f in runtime):
        raise ValueError('runtime_not_pure')
    selected=[];excluded=[]
    for file in sorted((package/'Tests/Editor').glob('*.cs')):
        text=file.read_text(encoding='utf-8')
        (excluded if 'UnityEngine' in text or 'UnityEditor' in text else selected).append(file)
    return runtime+selected,excluded


def file_receipt(root,path):
    data=path.read_bytes()
    return {'path':path.relative_to(root).as_posix(),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}


def run_python(root,suite,out):
    if suite=='livekit' and platform.system()!='Darwin':
        return {'state':'BLOCKED','reason':'macos_privacy_backend_required','testsRun':0}
    path=root/SUITES[suite]
    selected=sorted(path.rglob('test_*.py'))
    if not selected: return {'state':'BLOCKED','reason':'test_files_absent','testsRun':0}
    os.chdir(root);sys.path.insert(0,str(root));sys.path.insert(0,str(path))
    loader=unittest.TestLoader(); tests=loader.discover(str(path),pattern='test_*.py')
    with (out/'test-log.txt').open('w',encoding='utf-8') as stream:
        result=unittest.TextTestRunner(stream=stream,verbosity=2).run(tests)
    failed=len(result.failures)+len(result.errors)+len(result.unexpectedSuccesses)
    skipped=len(result.skipped)+len(result.expectedFailures)
    passed=result.testsRun-failed-skipped
    return {'state':test_state(0 if result.wasSuccessful() else 1,result.testsRun,passed,failed,skipped),
            'testsRun':result.testsRun,'passed':passed,'failed':failed,'skipped':len(result.skipped),
            'expectedFailures':len(result.expectedFailures),'unexpectedSuccesses':len(result.unexpectedSuccesses),
            'scope':SUITES[suite], 'files':[file_receipt(root,p) for p in selected],
            'skipReasons':[{'test':str(t),'reason':reason} for t,reason in result.skipped]}


def run_csharp(root,out):
    files,excluded=csharp_files(root)
    project=out/'PureRuntime.csproj'
    items='\n'.join('<Compile Include="'+escape(str(p),{'"':'&quot;'})+'" />' for p in files)
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><IsTestProject>true</IsTestProject><IsPackable>false</IsPackable><Nullable>disable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup><ItemGroup>'+items+'</ItemGroup><ItemGroup><PackageReference Include="NUnit" Version="3.14.0"/><PackageReference Include="NUnit3TestAdapter" Version="4.6.0"/><PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1"/></ItemGroup></Project>',encoding='utf-8')
    receipt={'scope':'original pure Runtime C# plus original NUnit tests without Unity imports; not Unity/PhysX/PlayMode/NGO',
             'included':[file_receipt(root,p) for p in files],'excludedUnityTests':[p.relative_to(root).as_posix() for p in excluded]}
    with (out/'test-log.txt').open('w',encoding='utf-8') as log:
        p=subprocess.run(['dotnet','test',str(project),'--configuration','Release','--logger','trx;LogFileName=results.trx','--results-directory',str(out/'results')],cwd=root,stdout=log,stderr=subprocess.STDOUT,timeout=600)
    trx=out/'results/results.trx'
    if not trx.is_file(): return {**receipt,'state':'BLOCKED' if p.returncode==0 else 'FAIL','reason':'build_or_test_report_absent','exitCode':p.returncode,'testsRun':0}
    tree=ET.parse(trx);ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    counters=tree.find('.//t:Counters',ns)
    if counters is None: return {**receipt,'state':'BLOCKED','reason':'test_counters_absent','testsRun':0}
    total=int(counters.get('total','0'));passed=int(counters.get('passed','0'));failed=int(counters.get('failed','0'))
    skipped=total-passed-failed
    return {**receipt,'state':test_state(p.returncode,total,passed,failed,skipped),'exitCode':p.returncode,'testsRun':total,'passed':passed,'failed':failed,'skipped':skipped}


def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--suite',choices=[*SUITES,'csharp'],required=True);ap.add_argument('--output',type=Path,required=True)
    a=ap.parse_args();root=a.root.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
    ref=subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()
    if ref!=BASE: raise ValueError('source_commit_mismatch')
    before=subprocess.check_output(['git','-C',str(root),'diff','--binary','HEAD'])
    if before: raise ValueError('dirty_source_baseline')
    try:
        result=run_csharp(root,out) if a.suite=='csharp' else run_python(root,a.suite,out)
    except Exception as e:
        result={'state':'BLOCKED','reason':type(e).__name__,'testsRun':0}
        (out/'runner-error.txt').write_text(str(e),encoding='utf-8')
    after=subprocess.check_output(['git','-C',str(root),'diff','--binary','HEAD'])
    if after: result['state']='FAIL';result['trackedSourceMutation']=True
    result.update({'schemaVersion':1,'sourceCommit':ref,'suite':a.suite,'platform':platform.system(),
                   'pythonVersion':platform.python_version(),'AAAApproved':False,'trackedSourceUnchanged':not after})
    # Keep compiler dependency lock evidence, but never upload binaries/caches.
    for name in ('test-log.txt','runner-error.txt'):
        path=out/name
        if path.exists():
            text=path.read_text(encoding='utf-8').replace(str(root),'<SOURCE>').replace(str(out),'<EVIDENCE>')
            temp=os.environ.get('RUNNER_TEMP')
            if temp:text=text.replace(temp,'<TEMP>')
            path.write_text(text,encoding='utf-8')
    (out/'receipt.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('files','included','excludedUnityTests','skipReasons')}))
    return 0 if result['state'] in ('PASS','PASS_WITH_SKIPS') else 1

if __name__=='__main__':raise SystemExit(main())
