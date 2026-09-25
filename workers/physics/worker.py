#!/usr/bin/env python3
"""Local JSONL worker: genuine CFSV3 + completed FDS reference fields.
No timer success, no field extrapolation, no clinical or whole-station claim.
"""
from __future__ import annotations
import base64, contextlib, hashlib, importlib, importlib.util, json, math, os, pathlib, sys, types

HERE=pathlib.Path(__file__).resolve().parent
ROOT=HERE.parent.parent
CASE=pathlib.Path(os.environ['CG_PHYSICS_CASE']) if 'CG_PHYSICS_CASE' in os.environ else HERE/'cases/reference-hall'
UPSTREAM=pathlib.Path(os.environ['CG_PHYSICS_UPSTREAM']) if 'CG_PHYSICS_UPSTREAM' in os.environ else HERE/'upstream'
MAX_LINE=1024*1024
MAX_INLINE=64*1024
VERSION='chooguard-reference-worker-3-native-fields'
SAMPLER_VERSION='native_nodes_nearest_v1'
COHORT_SCHEME='spawn-z-thirds-v1'
UNSUPPORTED='부산역 보정·격자수렴 미검증; 복사열 인체영향·HCN/자극가스·압착 접촉력·임상 생존/치료·양방향 화재 제어 미지원; 의료 명령은 지원 요청 상태만 기록'
UNITS={'position':'m','simTime':'s','temperature':'degC','extinction':'1/m','sootDensity':'kg/m3','visibility':'m','density':'person/m2','pressureIndicator':'1/s2','fedToxic':'1','fedConvectiveHeat':'1'}
GEOMETRY={'width':30,'depth':20,'height':4,'bottleneck':[24,25,9,11],'exit':[29,10],'spawn':[2,10,4,16],'fireBlock':[14,16,4,6],'frame':'reference-hall-meters: x=FDS-x,z=FDS-y,height=FDS-z'}
IMPORT_ERROR=None
try:
    for stream in (sys.stdin,sys.stdout,sys.stderr):
        if hasattr(stream,'reconfigure'): stream.reconfigure(encoding='utf-8')
    if not UPSTREAM.is_dir(): raise RuntimeError('PHYSICS_PACKAGE_MISSING: pinned upstream modules are not packaged')
    lock=json.loads((HERE/'upstream-lock.json').read_text())
    for filename,expected in lock['modules'].items():
        if hashlib.sha256((UPSTREAM/filename).read_bytes()).hexdigest()!=expected: raise RuntimeError('Upstream source hash mismatch: '+filename)
    with contextlib.redirect_stdout(sys.stderr):
        import jupedsim as jps
        import numpy as np
        from shapely.geometry import Polygon
        import fdsreader
        fdsreader.settings.ENABLE_CACHING=False  # Packaged reference inputs remain read-only; never unpickle a local cache.
        # Namespace load uses original, pinned files without executing optional GUI/scenario imports.
        package=types.ModuleType('_chooguard_pyfds'); package.__path__=[str(UPSTREAM)]
        sys.modules[package.__name__]=package
        sampling=importlib.import_module('_chooguard_pyfds.fds_sampling')
        smoke=importlib.import_module('_chooguard_pyfds.smoke_speed')
        fed=importlib.import_module('_chooguard_pyfds.fed')
except Exception as exc:
    IMPORT_ERROR=f'{type(exc).__name__}: {exc}'


def digest(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()


class NativeNodeSampler:
    """Local adapter; pinned upstream remains unchanged. Nearest node ties choose lower index."""
    def __init__(self, legacy):
        self._slice=legacy._slice
        subs=list(self._slice.subslices)
        if self._slice.cell_centered or len(subs)!=1: raise ValueError('Expected single native-node reference slice')
        self.sub=subs[0]; self.x=np.asarray(self.sub.mesh.coordinates['x']); self.y=np.asarray(self.sub.mesh.coordinates['y'])
        if self.sub.shape!=(61,41) or self.sub.data.shape!=(len(self._slice.times),61,41): raise ValueError('Unexpected reference field shape')
        if not np.array_equal(self.x,np.arange(61)*.5) or not np.array_equal(self.y,np.arange(41)*.5): raise ValueError('Unexpected native coordinates')
        times=np.asarray(self._slice.times)
        if not np.all(np.isfinite(times)) or not np.all(np.diff(times)>0) or times[0]!=0 or times[-1]!=120: raise ValueError('Invalid reference timestamps')
        if not np.all(np.isfinite(self.sub.data)): raise ValueError('Non-finite source field')
    def sample(self,t,x,y):
        if not all(math.isfinite(v) for v in (t,x,y)) or not 0<=t<=120 or not 0<=x<=30 or not 0<=y<=20: raise ValueError('Outside native field bounds')
        return float(self.sub.data[self._slice.get_nearest_timestep(t),int(np.argmin(abs(self.x-x))),int(np.argmin(abs(self.y-y)))])


class FireFields:
    def __init__(self):
        receipt=json.loads((CASE/'receipt.json').read_text())
        if receipt['solverExitCode']!=0 or not receipt['completed']:
            raise RuntimeError('FDS reference run not completed')
        if hashlib.sha256((CASE/'hall.fds').read_bytes()).hexdigest()!=receipt['deckSha256']:
            raise RuntimeError('FDS deck does not match completed output receipt')
        for name,sha in receipt['outputSha256'].items():
            if hashlib.sha256((CASE/name).read_bytes()).hexdigest()!=sha:
                raise RuntimeError('FDS output digest mismatch: '+name)
        self.receipt=receipt
        with contextlib.redirect_stdout(sys.stderr):
            simulation=fdsreader.Simulation(str(CASE))
            quantities={'k':'SOOT EXTINCTION COEFFICIENT','temperature':'TEMPERATURE','soot':'SOOT DENSITY','co':'CARBON MONOXIDE VOLUME FRACTION','co2':'CARBON DIOXIDE VOLUME FRACTION','o2':'OXYGEN VOLUME FRACTION'}
            self.samplers={key:NativeNodeSampler(sampling.load_slice_sampler(str(CASE),value,simulation=simulation,slice_height_m=1.5)) for key,value in quantities.items()}
            self.start=max(float(s._slice.times[0]) for s in self.samplers.values())
            self.end=min(float(s._slice.times[-1]) for s in self.samplers.values())
            for s in self.samplers.values():
                if abs(s._slice.extent.z_start-1.5)>.01: raise ValueError('FDS slice height mismatch')
        self.source_sha=hashlib.sha256((CASE/self.samplers['k'].sub.filename).read_bytes()).hexdigest()
        self.display_max=float(self.samplers['k'].sub.data.max()); self.frame_cache={}
        if self.samplers['k']._slice.quantity.unit!='1/m' or self.samplers['k'].sub.data.min()<0: raise ValueError('Invalid extinction source')
    def frame(self,t):
        self.check(t); sampler=self.samplers['k']; index=int(sampler._slice.get_nearest_timestep(t))
        if index not in self.frame_cache:
            raw=np.asarray(sampler.sub.data[index].T,dtype='<f4').tobytes(order='C')
            self.frame_cache[index]={'version':1,'caseId':'reference-hall-30x20-v1','quantity':'SOOT EXTINCTION COEFFICIENT','unit':'1/m','samplerVersion':SAMPLER_VERSION,'encoding':'base64-float32-le-row-major-yx','width':61,'height':41,'originX':0.,'originY':0.,'stepX':.5,'stepY':.5,'sampleHeight':1.5,'sampledIncidentTime':float(sampler._slice.times[index]),'validFrom':0.,'validUntil':120.,'displayMin':0.,'displayMax':self.display_max,'sourceSha256':self.source_sha,'deckSha256':self.receipt['deckSha256'],'payloadSha256':hashlib.sha256(raw).hexdigest(),'valuesBase64':base64.b64encode(raw).decode('ascii')}
        return dict(self.frame_cache[index],requestedIncidentTime=float(t))
    def check(self,t):
        if t < self.start-1e-6 or t>min(120,self.end)+1e-6:
            raise ValueError(f'FDS field interval exhausted: requested {t:g}s, available {self.start:g}..{min(120,self.end):g}s; no extrapolation')
    def sample(self,t,x,z):
        self.check(t)
        values={key:s.sample(t,x,z) for key,s in self.samplers.items()}
        if not all(math.isfinite(v) for v in values.values()): raise ValueError('Non-finite FDS field')
        if values['k']<-.001 or values['soot']<-.001: raise ValueError('Negative FDS smoke field')
        values['k']=max(0,values['k']); values['soot']=max(0,values['soot'])
        return values


class Session:
    def __init__(self,request,fire):
        if IMPORT_ERROR: raise RuntimeError(IMPORT_ERROR)
        self.run=request['runId']; self.generation=request['generation']; self.seed=request['seed']; self.total=request['population']; self.scenario=request['scenario']; self.fire=fire if self.scenario=='fire_smoke' else None
        if self.scenario=='fire_smoke' and fire is None: raise RuntimeError('Real FDS fields unavailable for this declared reference case')
        self.input_digest=digest({'seed':self.seed,'population':self.total,'scenario':self.scenario,'geometry':GEOMETRY,'dt':.05,'jupedsimVersion':jps.__version__,'deckSha256':fire.receipt['deckSha256'] if self.fire else None})
        self.phase='ordinary' if request['action']=='start_routine' else 'incident'; self.onset=-1. if self.phase=='ordinary' else 0.; self.incident_digest=''; self.routine={}
        self.warned=False; self.evacuating=False; self.medical=False; self.previous={}; self.toxic={}; self.heat={}; self.base={}; self.speed={}
        self.cohorts={}; self.released=[False,False,False]; self.release_revision=0; self.release_history=[]; self.final_outcomes={}; self.request_id=''; self.control_request_id=''; self.initial=[]
        geometry=Polygon([(0,0),(24,0),(24,9),(25,9),(25,0),(30,0),(30,20),(25,20),(25,11),(24,11),(24,20),(0,20)],holes=[[(14,4),(16,4),(16,6),(14,6)]])
        self.sim=jps.Simulation(model=jps.CollisionFreeSpeedModelV3(),geometry=geometry,dt=.05)
        exit_stage=self.sim.add_exit_stage([(28.5,9),(29.8,9),(29.8,11),(28.5,11)])
        journey=self.sim.add_journey(jps.JourneyDescription([exit_stage]))
        self.exit_stage=exit_stage; self.exit_journey=journey
        self.waypoints=[(5.,5.),(11.,8.),(20.,15.),(6.,15.)]
        self.routine_stages=[self.sim.add_waypoint_stage(p,.6) for p in self.waypoints]
        self.routine_journeys=[self.sim.add_journey(jps.JourneyDescription([stage])) for stage in self.routine_stages]
        points=jps.distributions.distribute_by_number(polygon=Polygon([(2,4),(10,4),(10,16),(2,16)]),number_of_agents=self.total,distance_to_agents=.5,distance_to_polygon=.3,seed=self.seed)
        rng=np.random.default_rng(self.seed)
        for point in points:
            base=float(rng.uniform(1.2,1.4)) if self.phase=='ordinary' else float(rng.uniform(1.05,1.45))
            agent_id=self.sim.add_agent(jps.CollisionFreeSpeedModelV3AgentParameters(position=point,journey_id=journey,stage_id=exit_stage,desired_speed=0,radius=.2,time_gap=1))
            self.base[agent_id]=base; self.toxic[agent_id]=0.; self.heat[agent_id]=0.; self.speed[agent_id]=(0.,0.)
            cohort=min(2,max(0,int((point[1]-4)/4))); self.cohorts[agent_id]=cohort
            index=len(self.initial)%len(self.waypoints)
            self.routine[agent_id]={'index':index,'until':-1.,'state':'moving','dwell':float(rng.uniform(4,12)) if self.phase=='ordinary' else 0.}
            if self.phase=='ordinary': self.sim.switch_agent_journey(agent_id,self.routine_journeys[index],self.routine_stages[index])
            self.initial.append({'ordinal':len(self.initial),'x':float(point[0]),'z':float(point[1]),'baseSpeed':base,'cohort':cohort,'radius':.2,'timeGap':1})
        self.initial_digest=digest({'scheme':COHORT_SCHEME,'agents':self.initial,'geometry':GEOMETRY,'dt':.05,'engineVersion':jps.__version__})
        self.input_digest=digest({'seed':self.seed,'population':self.total,'scenario':self.scenario,'initialStateDigest':self.initial_digest,'cohortScheme':COHORT_SCHEME,**({'samplerVersion':SAMPLER_VERSION} if self.fire else {}),'deckSha256':self.fire.receipt['deckSha256'] if self.fire else None})
        if self.phase=='incident': self.incident_digest=self.snapshot_digest()
    def snapshot_digest(self):
        return digest([{'id':int(a.id),'position':list(a.position),'cohort':self.cohorts[a.id],'toxic':self.toxic[a.id],'heat':self.heat[a.id]} for a in self.sim.agents()])
    def incident_time(self):
        return -1. if self.onset<0 else float(self.sim.elapsed_time()-self.onset)
    def advance(self,action,seconds,release_policy=None,cohort_id=-1,request_id=""):
        self.request_id=request_id
        if action=='begin_incident':
            if self.phase!='ordinary' or seconds!=0: raise ValueError('Incident onset requires ordinary phase and zero step')
            self.incident_digest=self.snapshot_digest(); self.onset=float(self.sim.elapsed_time()); self.phase='incident'
            self.released=[False,False,False]; self.evacuating=False
            for a in self.sim.agents():
                self.sim.switch_agent_journey(a.id,self.exit_journey,self.exit_stage); a.model.desired_speed=0.
        if self.phase=='ordinary' and action not in ('start_routine','advance'): raise ValueError('Response control requires incident')
        if self.phase=='incomplete': raise ValueError('FDS field interval exhausted; session incomplete')
        if self.fire and self.phase=='incident':
            try: self.fire.check(self.incident_time()+seconds)
            except ValueError:
                self.phase='incomplete'
                raise
        if action=='warn': self.warned=True
        if action=='evacuate':
            if release_policy is None: release_policy='all'
            targets=range(3) if cohort_id==-1 else [cohort_id]
            for cohort in targets:self.released[cohort]=release_policy!='hold'
            self.evacuating=any(self.released);self.release_revision+=1
            self.release_history.append({'revision':self.release_revision,'time':float(self.sim.elapsed_time()),'policy':release_policy,'cohortId':cohort_id,'requestId':request_id})
        if action in ('warn','evacuate','medical'):self.control_request_id=request_id
        if action=='medical': self.medical=True
        remaining=seconds
        while remaining>1e-8:
            delta=min(.05,remaining)
            positions={a.id:a.position for a in self.sim.agents()}
            for a in self.sim.agents():
                factor=1.
                if self.fire and self.phase=='incident':
                    values=self.fire.sample(self.incident_time(),*a.position)
                    factor=smoke.speed_factor_from_extinction(values['k'])
                    gases=fed.DefaultFedInputs(co_volume_fraction_percent=values['co']*100,co2_volume_fraction_percent=values['co2']*100,o2_volume_fraction_percent=values['o2']*100)
                    self.toxic[a.id]+=fed.default_fed_rate_per_minute(gases)*delta/60
                    self.heat[a.id]+=fed.default_heat_fed_rate_per_minute(fed.HeatFedInputs(values['temperature']))*delta/60
                if self.phase=='ordinary':
                    itinerary=self.routine[a.id]; target=self.waypoints[itinerary['index']]
                    if itinerary['state']=='moving' and math.dist(a.position,target)<=.7:
                        itinerary['state']='waiting'; itinerary['until']=self.sim.elapsed_time()+itinerary['dwell']
                    if itinerary['state']=='waiting' and self.sim.elapsed_time()>=itinerary['until']:
                        itinerary['index']=(itinerary['index']+1)%len(self.waypoints); itinerary['state']='moving'
                        self.sim.switch_agent_journey(a.id,self.routine_journeys[itinerary['index']],self.routine_stages[itinerary['index']])
                    a.model.desired_speed=self.base[a.id] if itinerary['state']=='moving' else 0.
                else: a.model.desired_speed=self.base[a.id]*factor if self.released[self.cohorts[a.id]] else 0.
            self.sim.iterate(round(delta/.05))
            active_ids={a.id for a in self.sim.agents()}
            for exited in set(positions)-active_ids:self.final_outcomes[exited]={'id':int(exited),'cohort':self.cohorts[exited],'exitTime':float(self.sim.elapsed_time()),'fedToxic':self.toxic[exited],'fedConvectiveHeat':self.heat[exited]}
            for a in self.sim.agents():
                p=positions[a.id]; self.speed[a.id]=((a.position[0]-p[0])/delta,(a.position[1]-p[1])/delta)
            remaining-=delta
            if self.phase=='incident' and not active_ids: self.phase='resolved'
    def result(self):
        agents=[{'id':int(a.id),'x':float(a.position[0]),'z':float(a.position[1]),'cohort':self.cohorts[a.id],'released':self.released[self.cohorts[a.id]],'routineState':self.routine[a.id]['state'] if self.phase=='ordinary' else 'responding','destinationId':('routine-'+str(self.routine[a.id]['index'])) if self.phase=='ordinary' else 'exit','fedToxic':self.toxic[a.id],'fedConvectiveHeat':self.heat[a.id]} for a in self.sim.agents()]
        cohort_stats=[]
        for cohort in range(3):
            ids=[i for i,c in self.cohorts.items() if c==cohort];active=[a for a in agents if a['cohort']==cohort];final=[v for v in self.final_outcomes.values() if v['cohort']==cohort]
            cohort_stats.append({'id':cohort,'total':len(ids),'active':len(active),'evacuated':len(final),'released':len(active) if self.released[cohort] else 0,'isReleased':self.released[cohort],'final':not active,'maxFedToxic':max((self.toxic[i] for i in ids),default=0.),'maxFedConvectiveHeat':max((self.heat[i] for i in ids),default=0.),'meanFinalFedToxic':sum(v['fedToxic'] for v in final)/max(1,len(final)),'meanFinalFedConvectiveHeat':sum(v['fedConvectiveHeat'] for v in final)/max(1,len(final)),'finalExitTime':max((v['exitTime'] for v in final),default=None)})
        density=pressure=0.
        for a in agents:
            local=[b for b in agents if (a['x']-b['x'])**2+(a['z']-b['z'])**2<=4]
            rho=len(local)/(math.pi*4)
            velocities=np.array([self.speed[b['id']] for b in local])
            diagnostic=rho*float(np.var(velocities,axis=0).sum())
            density=max(density,rho); pressure=max(pressure,diagnostic)
        temperature=20.; k=soot=0.; field_time=None
        if self.fire and self.phase=='incident':
            t=self.incident_time()
            samples=[self.fire.sample(t,a['x'],a['z']) for a in agents] or [self.fire.sample(t,29,10)]
            temperature=max(v['temperature'] for v in samples); k=max(v['k'] for v in samples); soot=max(v['soot'] for v in samples)
            sample=self.fire.samplers['k']._slice
            field_time=float(sample.times[sample.get_nearest_timestep(t)])
        evacuated=self.total-len(agents)
        return {'kind':'RESULT','protocolVersion':1,'runId':self.run,'generation':self.generation,'seed':self.seed,'simTime':float(self.sim.elapsed_time()),'lifecycleVersion':1,'phase':self.phase,'sessionSimTime':float(self.sim.elapsed_time()),'incidentOnsetSimTime':self.onset,'incidentTime':self.incident_time(),'incidentInitialDigest':self.incident_digest,'normalDepartedCount':0,'incidentEvacuatedCount':len(self.final_outcomes),'fieldCurrent':bool(self.fire and self.phase=='incident'),'samplerVersion':SAMPLER_VERSION if self.fire else '', 'hazardField':self.fire.frame(self.incident_time()) if self.fire and self.phase=='incident' else None,'engine':'JuPedSim CFSV3 + FDS reference' if self.fire else 'JuPedSim CFSV3','engineVersion':jps.__version__,'fdsStatus':'completed_reference' if self.fire else 'not_required','fdsVersion':'6.11.1' if self.fire else '', 'fireRequired':bool(self.fire),'physicsReady':True,'allEvacuated':evacuated==self.total,'total':self.total,'evacuated':evacuated,'density':density,'pressureIndicator':pressure,'visibility':min(30.,3/max(k,1e-12)),'temperature':temperature,'unsupported':UNSUPPORTED,'agents':agents,'extinction':k,'sootDensity':soot,'maxFedToxic':max(self.toxic.values(),default=0.),'maxFedConvectiveHeat':max(self.heat.values(),default=0.),'warned':self.warned,'evacuationOrdered':self.evacuating,'medicalAssistanceRequested':self.medical,'workerId':VERSION,'modelRef':'CollisionFreeSpeedModelV3','caseId':'reference-hall-30x20-v1','inputDigest':self.input_digest,'initialStateDigest':self.initial_digest,'cohortScheme':COHORT_SCHEME,'releaseRevision':self.release_revision,'releasePolicyHistoryDigest':digest(self.release_history),'releaseHistory':self.release_history[-32:],'cohortStats':cohort_stats,'finalOutcomes':list(self.final_outcomes.values()),'requestId':self.request_id,'lastControlRequestId':self.control_request_id,'boundaryRevision':0,'interval':{'end':self.sim.elapsed_time()},'units':UNITS,'frame':GEOMETRY['frame'],'fieldOwner':{'movement':'JuPedSim','fire':'FDS-batch','pressureIndicator':'derived-density-velocity-variance'},'fieldTime':field_time,'fieldValidUntil':min(120,self.fire.end) if self.fire else None,'pressureMeaning':'density times local 0.05-second velocity variance; not contact/crush force','visibilityMeaning':'minimum at active agents; capped at 30m display range','thermalDoseMeaning':'convective dose independent of toxic dose; no clinical mortality or heat incapacitation implemented'}


def emit(value):
    payload=json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False)
    if len(payload.encode())>MAX_INLINE: raise ValueError('Result inline limit exceeded')
    sys.stdout.write(payload+'\n');sys.stdout.flush()


def unique_pairs(pairs):
    value={}
    for k,v in pairs:
        if k in value: raise ValueError('Duplicate JSON key: '+k)
        value[k]=v
    return value


def validate_depth(value,depth=0):
    if depth>32: raise ValueError('JSON depth exceeds 32')
    if isinstance(value,dict):
        for v in value.values(): validate_depth(v,depth+1)
    elif isinstance(value,list):
        for v in value: validate_depth(v,depth+1)


def main():
    session=None; fire=None; fire_error=''; generations={}; cancelled=set(); handshake=False
    for_read=sys.stdin.buffer
    while True:
        raw=for_read.readline(MAX_LINE+1)
        if not raw: return
        request={}
        try:
            if len(raw)>MAX_LINE: raise ValueError('JSONL line exceeds 1 MiB')
            if not raw.endswith(b'\n'): raise ValueError('EOF before newline')
            if len(raw)>MAX_INLINE: raise ValueError('Inline request exceeds 64 KiB')
            request=json.loads(raw.decode('utf-8'),object_pairs_hook=unique_pairs,parse_constant=lambda x:(_ for _ in ()).throw(ValueError('Non-finite JSON number')))
            validate_depth(request)
            if not isinstance(request,dict): raise ValueError('Request must be object')
            kind=request.get('kind')
            if kind=='HELLO':
                if request.get('protocolVersion')!=1: raise ValueError('Unsupported protocolVersion')
                handshake=True
                if not IMPORT_ERROR and fire is None:
                    try: fire=FireFields()
                    except Exception as exc: fire_error=str(exc)
                emit({'kind':'CAPABILITIES','protocolVersion':1,'lifecycleVersion':1,'hazardFieldVersion':1,'hazardFieldSourceSha256':fire.source_sha if fire else '', 'hazardFieldDeckSha256':fire.receipt['deckSha256'] if fire else '', 'samplerVersion':SAMPLER_VERSION,'workerId':VERSION,'engine':'JuPedSim CFSV3','engineVersion':None if IMPORT_ERROR else jps.__version__,'available':IMPORT_ERROR is None,'fdsAvailable':fire is not None,'fdsVersion':'6.11.1' if fire else '', 'fireStatus':'completed_reference' if fire else 'unavailable','diagnostic':IMPORT_ERROR or fire_error,'maxPopulation':200,'maxStepSeconds':1.,'cohortScheme':COHORT_SCHEME,'releasePolicies':['all','staged','hold'],'dt':.05,'caseId':'reference-hall-30x20-v1','geometry':GEOMETRY,'units':UNITS,'unsupported':UNSUPPORTED})
                continue
            if not handshake: raise ValueError('HELLO required')
            if kind not in ('SUBMIT','CANCEL'): raise ValueError('Unknown kind')
            run=request.get('runId');generation=request.get('generation')
            if not isinstance(run,str) or not 1<=len(run)<=128: raise ValueError('Invalid runId')
            if type(generation) is not int or generation<0: raise ValueError('Invalid generation')
            if generation<generations.get(run,-1): raise ValueError('Stale generation fenced')
            if kind=='CANCEL':
                cancelled.add((run,generation));generations[run]=generation
                if session and session.run==run and session.generation<=generation: session=None
                emit({'kind':'CANCELLED','runId':run,'generation':generation});continue
            if (run,generation) in cancelled: raise ValueError('Cancelled generation fenced')
            if type(request.get('seed')) is not int or not 0<=request['seed']<2**32: raise ValueError('Invalid seed')
            if type(request.get('population')) is not int or not 1<=request['population']<=200: raise ValueError('Population must be 1..200')
            if request.get('scenario') not in ('fire_smoke','crowd_medical'): raise ValueError('Unknown scenario')
            action=request.get('action')
            if action not in ('start','start_routine','begin_incident','advance','warn','evacuate','medical'): raise ValueError('Unknown action')
            policy=request.get('releasePolicy');cohort=request.get('cohortId',-1);request_id=request.get('requestId','')
            if not isinstance(request_id,str) or len(request_id)>128:raise ValueError('Invalid requestId')
            if action=='evacuate':
                policy=policy or 'all'
                if policy not in ('all','staged','hold') or type(cohort) is not int or cohort not in (-1,0,1,2):raise ValueError('Invalid release policy/cohort')
                if policy=='all' and cohort!=-1:raise ValueError('all requires cohortId=-1')
                if policy=='staged' and cohort==-1:raise ValueError('staged requires one cohort')
            elif policy not in (None,'') or cohort!=-1:raise ValueError('Release fields require action=evacuate')
            seconds=request.get('stepSeconds',0)
            if type(seconds) not in (int,float) or not math.isfinite(seconds) or not 0<=seconds<=1 or abs(seconds/.05-round(seconds/.05))>1e-6: raise ValueError('stepSeconds must be 0..1 in .05s increments')
            if action=='start_routine' and request['population']!=50: raise ValueError('Routine reference population must be 50')
            if action in ('start','start_routine'):
                if session and session.run==run and session.generation==generation: raise ValueError('Session already started')
                session=Session(request,fire);generations[run]=generation
            if session is None or session.run!=run or session.generation!=generation: raise ValueError('No matching active session')
            if request['seed']!=session.seed or request['population']!=session.total or request['scenario']!=session.scenario: raise ValueError('Immutable session settings mismatch')
            session.advance(action,seconds,policy,cohort,request_id)
            emit(session.result())
        except Exception as exc:
            diagnostic=f'{type(exc).__name__}: {exc}'
            print(diagnostic,file=sys.stderr,flush=True)
            emit({'kind':'ERROR','lifecycleVersion':1,'phase':session.phase if session else '', 'sessionSimTime':float(session.sim.elapsed_time()) if session else 0.,'runId':request.get('runId') if isinstance(request,dict) else None,'generation':request.get('generation') if isinstance(request,dict) else None,'message':diagnostic,'physicsReady':False,'requestId':request.get('requestId','') if isinstance(request,dict) else ''})
            if len(raw)>MAX_LINE: return

if __name__=='__main__': main()
