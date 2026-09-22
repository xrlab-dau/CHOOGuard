#!/usr/bin/env python3
"""Actual reference-field migration probe; no CFD rerun or synthetic field inputs."""
import base64,copy,hashlib,json,pathlib,subprocess,sys
import numpy as np
import worker

def decode(frame,source,deck):
    assert frame['sourceSha256']==source and frame['deckSha256']==deck
    assert (frame['width'],frame['height'])==(61,41)
    assert frame['encoding']=='base64-float32-le-row-major-yx' and frame['unit']=='1/m'
    assert 0<=frame['requestedIncidentTime']<=120 and 0<=frame['sampledIncidentTime']<=120
    raw=base64.b64decode(frame['valuesBase64'],validate=True)
    assert len(raw)==10004 and hashlib.sha256(raw).hexdigest()==frame['payloadSha256']
    a=np.frombuffer(raw,dtype='<f4').reshape(41,61)
    assert np.all(np.isfinite(a)) and np.min(a)>=0
    return a

def main():
    fire=worker.FireFields(); sampler=fire.samplers['k']; legacy=worker.sampling.SliceFieldSampler(sampler._slice)
    session=worker.Session(dict(runId='probe',generation=1,seed=42,population=50,scenario='fire_smoke',action='start'),fire)
    points=[a.position for a in session.sim.agents()]+[(15.1,6.1),(29,10),(0,0),(30,20)]
    frames=[]
    for time in (0,30,60,120):
        frame=fire.frame(time);decoded=decode(frame,fire.source_sha,fire.receipt['deckSha256']);ti=int(sampler._slice.get_nearest_timestep(time))
        assert np.array_equal(decoded,sampler.sub.data[ti].T)
        delta=[]
        for x,y in points:
            i=int(np.argmin(abs(sampler.x-x)));j=int(np.argmin(abs(sampler.y-y)))
            assert float(decoded[j,i])==sampler.sample(time,x,y)
            delta.append(abs(legacy.sample(time,x,y)-sampler.sample(time,x,y)))
        frames.append(dict(requestedTime=time,sampledTime=frame['sampledIncidentTime'],payloadSha256=frame['payloadSha256'],rawMin=float(decoded.min()),rawMax=float(decoded.max()),pointCount=len(points),legacyPointDifferences=sum(d>0 for d in delta),legacyMaxAbsoluteDifference=max(delta)))
    negatives=[]
    frame=fire.frame(30)
    for name,change in [('source',dict(sourceSha256='0'*64)),('deck',dict(deckSha256='0'*64)),('payload',dict(payloadSha256='0'*64)),('shape',dict(width=60)),('time',dict(requestedIncidentTime=121))]:
        bad=dict(frame,**change)
        try:decode(bad,fire.source_sha,fire.receipt['deckSha256'])
        except AssertionError:negatives.append(name)
        else:raise AssertionError(name+' incorrectly accepted')
    try:fire.frame(120.05)
    except ValueError:negatives.append('field-boundary')
    else:raise AssertionError('field boundary accepted')
    p=subprocess.Popen([sys.executable,str(worker.HERE/'worker.py')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True)
    def exchange(r):
        p.stdin.write(json.dumps(r)+'\n');p.stdin.flush();line=p.stdout.readline();assert len(line.encode())<65536;return json.loads(line),len(line.encode())
    try:
        cap,_=exchange(dict(kind='HELLO',protocolVersion=1));assert cap['hazardFieldVersion']==1
        r=dict(kind='SUBMIT',runId='field',generation=1,seed=42,population=50,scenario='fire_smoke',stepSeconds=0)
        ordinary,_=exchange(dict(r,action='start_routine'));assert ordinary['hazardField'] is None
        incident,line_bytes=exchange(dict(r,action='begin_incident'));decode(incident['hazardField'],cap['hazardFieldSourceSha256'],cap['hazardFieldDeckSha256'])
        assert incident['fieldCurrent'] and incident['samplerVersion']=='native_nodes_nearest_v1'
    finally:p.terminate();p.wait(timeout=5)
    return dict(status='PASS',samplerVersion=worker.SAMPLER_VERSION,frames=frames,negativeCases=negatives,negativeSurface='Python wire contract consumer; C# TryDecodeHazardField runtime validation is root integration work',liveWorker=dict(ordinaryFieldAbsent=True,incidentFrameDecoded=True,lineBytes=line_bytes),legacyFireMetrics='Previous fire receipts used legacy cell-centre-like indexing; do not reuse as native-node outcomes. Above differences are extinction values at54 fixed locations, not dose/outcome validation.',sourceSha256=fire.source_sha,deckSha256=fire.receipt['deckSha256'])
if __name__=='__main__':
    result=main(); destination=worker.ROOT/'.planning/2026-09-20-integrated-build/hazard-field-probe-receipt.json';destination.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
