#!/usr/bin/env python3
"""Observed paired policy run. Same initial state; no claim staged is superior."""
import json,pathlib,subprocess,sys,time,math
from runtime_package import launch_worker
HERE=pathlib.Path(__file__).resolve().parent
OUT=HERE/'evidence';OUT.mkdir(exist_ok=True)
SEED=20260921;POPULATION=96

def run(policy):
    started=time.monotonic();name='paired-'+policy;records=[];requests=[];counter=0
    with (OUT/(name+'.stderr.log')).open('w') as stderr:
        process=launch_worker(stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=stderr,text=True,bufsize=1)
        def send(action=None,step=0,release=None,cohort=-1):
            nonlocal counter
            counter+=1
            req={'kind':'HELLO','protocolVersion':1} if action is None else {'kind':'SUBMIT','runId':name,'generation':1,'seed':SEED,'population':POPULATION,'scenario':'fire_smoke','action':action,'stepSeconds':step,'requestId':f'{policy}-{counter}'}
            if release is not None:req.update(releasePolicy=release,cohortId=cohort)
            requests.append(req);process.stdin.write(json.dumps(req)+'\n');process.stdin.flush();line=process.stdout.readline()
            if not line:raise RuntimeError('Unexpected worker EOF')
            result=json.loads(line);records.append(result)
            if result['kind']=='ERROR':raise RuntimeError(result)
            if action is not None:assert result['requestId']==req['requestId']
            return result
        send();initial=send('start');positions={a['id']:(a['x'],a['z']) for a in initial['agents']}
        held=send('evacuate',release='hold',cohort=-1);assert held['releaseRevision']==1
        send('warn')
        current=initial
        for _ in range(25):current=send('advance',1)
        movement=max(math.hypot(a['x']-positions[a['id']][0],a['z']-positions[a['id']][1]) for a in current['agents'])
        assert movement<1e-6,'Held cohort moved after warn'
        assert all(not c['isReleased'] for c in current['cohortStats'])
        waiting_dose=max(a['fedToxic'] for a in current['agents'])
        current=send('evacuate',release='all' if policy=='all' else 'staged',cohort=-1 if policy=='all' else 0)
        assert current['releaseRevision']==2
        while current['simTime']<120 and not current['allEvacuated']:
            if policy=='staged' and abs(current['simTime']-40)<.001:current=send('evacuate',release='staged',cohort=1)
            if policy=='staged' and abs(current['simTime']-55)<.001:current=send('evacuate',release='staged',cohort=2)
            current=send('advance',1)
        process.stdin.close();process.wait(timeout=10)
    for suffix,values in [('requests',requests),('results',records)]:
        (OUT/(name+'.'+suffix+'.jsonl')).write_text(''.join(json.dumps(v,ensure_ascii=False,separators=(',',':'))+'\n' for v in values))
    frames=[r for r in records if r['kind']=='RESULT']
    return {'policy':policy,'seed':SEED,'population':POPULATION,'scenario':'fire_smoke','initialStateDigest':initial['initialStateDigest'],'inputDigest':initial['inputDigest'],'releaseSchedule':[(25,'all')] if policy=='all' else [(25,0),(40,1),(55,2)],'completed':current['allEvacuated'],'timeSeconds':current['simTime'],'evacuated':current['evacuated'],'remaining':POPULATION-current['evacuated'],'peakDensity':max(r['density'] for r in frames),'peakFlowDiagnostic':max(r['pressureIndicator'] for r in frames),'maxFedToxic':current['maxFedToxic'],'maxFedConvectiveHeat':current['maxFedConvectiveHeat'],'heldMotionAfterWarnMeters':movement,'heldDoseAt25Seconds':waiting_dose,'releaseRevision':current['releaseRevision'],'releasePolicyHistoryDigest':current['releasePolicyHistoryDigest'],'cohorts':current['cohortStats'],'finalOutcomes':current['finalOutcomes'],'wallSeconds':time.monotonic()-started,'exitCode':process.returncode}

if __name__=='__main__':
    all_result=run('all');staged_result=run('staged')
    assert all_result['initialStateDigest']==staged_result['initialStateDigest'] and all_result['inputDigest']==staged_result['inputDigest']
    summary={'scope':'One uncalibrated 30x20m reference hall, same initial state, actual FDS replay. No clinical/crush/Busan accuracy claim. Controlled cohort compliance is a scenario assumption. Exposure and exit timestamps integrated/reported at .05s; FDS nearest fields at1s. No CFD rerun.','sameInitialState':True,'runs':[all_result,staged_result],'stagedMinusAll':{key:staged_result[key]-all_result[key] for key in ['timeSeconds','peakDensity','peakFlowDiagnostic','maxFedToxic','maxFedConvectiveHeat']}}
    (OUT/'paired-policy-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'sameInitialState':True,'stagedMinusAll':summary['stagedMinusAll'],'runs':[{k:r[k] for k in ['policy','completed','timeSeconds','evacuated','peakDensity','maxFedToxic']} for r in summary['runs']]},indent=2))
