#!/usr/bin/env python3
"""Compile actual portable provider/coordinator/guarded execution and run against the local MLX service."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import os
import math
import time

ROOT = Path(__file__).resolve().parents[3]
HERE = ROOT / 'workers/simulator'
OUT = ROOT / 'output/simulator/model-benchmark'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--timeout', default='120')
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('checks', HERE/'run_checks.py')
    checks = importlib.util.module_from_spec(spec); spec.loader.exec_module(checks)
    mono, compiler = checks.toolchain()
    build=OUT/'build'; build.mkdir(parents=True,exist_ok=True)
    if (OUT/'results.json').exists():
        history=OUT/'history'/time.strftime('%Y%m%d-%H%M%S');history.mkdir(parents=True)
        for name in ['results.json','paired-trace.json','planning-records.json','local-choice-evaluation.json']:
            if (OUT/name).exists():shutil.copy2(OUT/name,history/name)
    sources=sorted([*HERE.glob('Runtime/Core/*.cs'),*HERE.glob('Runtime/Planning/*.cs'),HERE/'Tools/ModelBenchmark.cs'])
    hashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
    snapshots=[]
    for p in sources:
        snapshot=build/'source'/p.relative_to(HERE);snapshot.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,snapshot);snapshots.append(snapshot)
    wrapper=build/'Entry.cs';wrapper.write_text('public static class LocalModelBenchmarkEntry {public static int Main(string[] a){return ModelBenchmark.Run(a);}}\n')
    dll=next((ROOT/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    shutil.copy2(dll,build/'Newtonsoft.Json.dll')
    exe=build/'LocalModelBenchmark.exe'
    subprocess.run([str(mono),str(compiler),'-nologo','-langversion:latest','-target:exe','-out:'+str(exe),'-r:'+str(dll),'-r:System.Net.Http.dll','-r:'+str(mono.parent.parent/'lib/mono/4.5/Facades/netstandard.dll'),*map(str,snapshots),str(wrapper)],check=True,cwd=ROOT)
    # Loopback calls only. Mono must not send the local request through a shell-provided proxy.
    env=dict(os.environ);env['NO_PROXY']='127.0.0.1,localhost';env['no_proxy']='127.0.0.1,localhost'
    result=subprocess.run([str(mono),str(exe),'--output',str(OUT),'--timeout',args.timeout],cwd=ROOT,env=env)
    p=OUT/'results.json'
    if p.exists():
        report=json.loads(p.read_text());report['source_hashes']=hashes
        report['source_changed_during_run']=any(hashlib.sha256((ROOT/n).read_bytes()).hexdigest()!=v for n,v in hashes.items())
        report['model_receipt_hash']=hashlib.sha256((OUT/'model-receipt.json').read_bytes()).hexdigest()
        report['model_revision']=json.loads((OUT/'model-receipt.json').read_text())['revision']
        report['environment_receipt_hash']=hashlib.sha256((OUT/'environment-receipt.json').read_bytes()).hexdigest()
        report['model_hashes']=json.loads((OUT/'model-receipt.json').read_text())['files']
        report['config']['backend']='MLX CPU; pinned int4 weights dequantized once in RAM to float32'
        report['config']['python']=json.loads((OUT/'server-process.json').read_text())['python']
        trace=json.loads((OUT/'paired-trace.json').read_text());rows=trace['baseline']+trace['cached']
        latencies=sorted(r['latency_ms'] for r in rows if not r['cache_hit'])
        report['p50_latency_ms']=latencies[math.ceil(.5*len(latencies))-1]
        report['p95_latency_ms']=latencies[math.ceil(.95*len(latencies))-1]
        report['first_paired_request_latency_ms']=trace['baseline'][0]['latency_ms']
        report['warmup_status']='backend weights already loaded; prompt KV cache disabled'
        report['input_token_reduction_fraction']=1-report['event_cache']['measured_input_tokens']/report['baseline']['measured_input_tokens']
        report['output_token_reduction_fraction']=1-report['event_cache']['measured_output_tokens']/report['baseline']['measured_output_tokens']
        report['call_reduction_fraction']=1-report['event_cache']['generator_calls']/report['baseline']['generator_calls']
        report['exact_one_step_instruction_compliant_observations']=sum(len(r.get('steps') or [])==1 for r in rows)
        report['exact_one_step_instruction_compliant_generator_outputs']=sum(len(r.get('steps') or [])==1 for r in rows if not r['cache_hit'])
        report['guarded_execution_valid_observations']=sum(r['execution_valid'] for r in rows)
        report['matched_observation_hashes']=all(a['observation_hash']==b['observation_hash'] for a,b in zip(trace['baseline'],trace['cached']))
        report['matched_requested_context_hashes']=all(a['request_hash']==b['request_hash'] for a,b in zip(trace['baseline'],trace['cached']) if b['request_hash'])
        report['model_service_source_hashes']={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (HERE/'model_service').glob('*') if p.is_file()}
        report['config_hash']=hashlib.sha256(json.dumps(report['config'],sort_keys=True,separators=(',',':')).encode()).hexdigest()
        p.write_text(json.dumps(report,indent=2)+'\n')
    return result.returncode

if __name__=='__main__':
    raise SystemExit(main())
