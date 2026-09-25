#!/usr/bin/env python3
"""Bounded execution evidence, not a calibrated validation suite."""
import json,pathlib,subprocess,sys,time
from runtime_package import launch_worker
HERE=pathlib.Path(__file__).resolve().parent
EVIDENCE=HERE/'evidence'
def execute(name,scenario,population,delay):
    started=time.monotonic();requests=[];results=[]
    with (EVIDENCE/(name+'.stderr.log')).open('w') as err:
        p=launch_worker(stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,bufsize=1)
        def send(req):
            requests.append(req);p.stdin.write(json.dumps(req)+'\n');p.stdin.flush();line=p.stdout.readline()
            if not line: raise RuntimeError('Worker EOF')
            response=json.loads(line);results.append(response)
            if response['kind']=='ERROR': raise RuntimeError(response)
            return response
        capability=send({'kind':'HELLO','protocolVersion':1})
        base={'kind':'SUBMIT','runId':name,'generation':1,'seed':7,'population':population,'scenario':scenario}
        first=send(dict(base,action='start',stepSeconds=0))
        for _ in range(delay): send(dict(base,action='advance',stepSeconds=1))
        current=send(dict(base,action='evacuate',stepSeconds=1))
        for _ in range(118-delay):
            if current['allEvacuated']: break
            current=send(dict(base,action='advance',stepSeconds=1))
        send(dict(base,action='medical',stepSeconds=0))
        send({'kind':'CANCEL','runId':name,'generation':1})
        p.stdin.close();p.wait(timeout=10)
    for suffix,records in [('requests',requests),('results',results)]:
        (EVIDENCE/f'{name}.{suffix}.jsonl').write_text(''.join(json.dumps(v,ensure_ascii=False)+'\n' for v in records))
    frames=[r for r in results if r['kind']=='RESULT']
    summary={'case':name,'scenario':scenario,'seed':7,'population':population,'exitCode':p.returncode,'wallSeconds':time.monotonic()-started,'simTime':current['simTime'],'evacuated':current['evacuated'],'allEvacuated':current['allEvacuated'],'maxDensity':max(r['density'] for r in frames),'maxPressureIndicator':max(r['pressureIndicator'] for r in frames),'maxTemperatureC':max(r['temperature'] for r in frames),'maxExtinctionPerM':max(r['extinction'] for r in frames),'maxSootDensityKgM3':max(r['sootDensity'] for r in frames),'minVisibilityM':min(r['visibility'] for r in frames),'maxFedToxic':max(r['maxFedToxic'] for r in frames),'maxFedConvectiveHeat':max(r['maxFedConvectiveHeat'] for r in frames),'firstPosition':first['agents'][0],'movedPosition':next(r['agents'][0] for r in frames if r['simTime']>delay and r['agents'])}
    return summary
if __name__=='__main__':
    summaries=[execute('crowd-72-seed7','crowd_medical',72,0),execute('fire-24-seed7','fire_smoke',24,25)]
    (EVIDENCE/'reproduction-summary.json').write_text(json.dumps(summaries,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(summaries,ensure_ascii=False,indent=2))
