#!/usr/bin/env python3
"""Prepare/analyze a bounded three-client Schema3 moving-rider/recovery probe.
Real player execution is an explicit future path for root; authoring tests launch nothing.
"""
import argparse
import copy
import hashlib
import json
import math
import os
import plistlib
import secrets
import signal
import subprocess
import time
from pathlib import Path

from probe_journal import ProbeError, integer, read_journal, strict_json

NAMES = ('instructor', 'rider-a', 'rider-b')
RIDER_NAMES = NAMES[1:]
EQUIPMENT = 'equipment.rolling_stock_mainline'
MAINLINE = 'rolling_stock_mainline'
CONCOURSE = 'station_concourse_2f'
MAX_EVENTS = 128 * 1024 * 1024
MAX_LINE = 4 * 1024 * 1024
VIEW_FIELDS = set('Observed Position TeamId RoleId Instructor ProtocolVersion SpatialProfileId RegionId FrameId PortalId MovementStatus LocalPosition RequiredRegions Portals Physical'.split())
OBSERVED_FIELDS = set('WorldId ShiftId ParticipantId Sequence SimulationTick Paused TotalReports Entities Reports'.split())
ENTITY_FIELDS = set('EntityId RegionId RequiredRoleId LeaderId FrameId Kind Position LocalPosition Revision Active'.split())
REPORT_FIELDS = set('ReportId FromParticipantId ToTeamId EntityId RegionId FrameId Position LocalPosition ObservedRevision Sequence ObservedSimulationTick ObservedFrame AcknowledgedBy'.split())
FRAME_FIELDS = {'FrameId', 'Origin', 'YawDegrees'}
PHYSICAL_FIELDS = {'Frames', 'Bodies', 'Trains', 'Regions', 'Smoke'}
BODY_FIELDS = set('ParticipantId TeamId RoleId RegionId FrameId Position LocalPosition'.split())
TRAIN_FIELDS = {'EntityId', 'FrameId', 'SignedSpeedMS', 'DoorOpen'}
SMOKE_CELL_FIELDS = set('Id CenterX CenterY CenterZ YawDegrees WidthM DepthM BoundingHeightM FloorRiseM SignedFloorGradientX InterfaceHeightAboveMinFloorM UpperExtinctionPerM LowerExtinctionPerM'.split())
FORBIDDEN_KEYS = set('SimulationCheckpoint SimulationDefinitionHash Checkpoint CrowdCheckpoint MotionCheckpoint DirectorState DirectorCheckpoint IncidentCheckpoint Seed RandomState Schedule Choices FutureIncidents FutureEvents Receipts Tickets Secret SecretSha256'.split())


def require(condition, code):
    if not condition:
        raise ProbeError(code)


def private_json(path, data):
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, 'O_NOFOLLOW', 0), 0o600), 'w', encoding='utf-8') as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2, allow_nan=False); stream.write('\n')


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''):digest.update(data)
    return digest.hexdigest()


def local(frame, point):
    a = math.radians(frame['YawDegrees']); x = point['X'] - frame['Origin']['X']; z = point['Z'] - frame['Origin']['Z']
    return {'X': math.cos(a)*x-math.sin(a)*z, 'Y': point['Y']-frame['Origin']['Y'], 'Z': math.sin(a)*x+math.cos(a)*z}


def world_point(frame, point):
    a=math.radians(frame['YawDegrees']);c,s=math.cos(a),math.sin(a)
    return {'X':frame['Origin']['X']+c*point['X']+s*point['Z'], 'Y':frame['Origin']['Y']+point['Y'],
            'Z':frame['Origin']['Z']-s*point['X']+c*point['Z']}


def distance(a,b):
    return math.sqrt(sum((a[k]-b[k])**2 for k in ('X','Y','Z')))


def prepare(project, session):
    project, session = Path(project).resolve(), Path(session).resolve()
    require(session.parent.is_dir(), 'SESSION_PARENT_MISSING')
    sources = {'World':project/'foundation/network/connected-world-layout.json',
               'Geometry':project/'foundation/world/connected-world-profile.json',
               'Simulation':project/'foundation/world/foundation-simulation-profile.json'}
    source = {key:strict_json(path.read_bytes()) for key,path in sources.items()}
    world, geometry, simulation = (copy.deepcopy(source[key]) for key in ('World','Geometry','Simulation'))
    require(world.get('SchemaVersion') == 2 and len(geometry['Regions']) == 13, 'SOURCE_SCHEMA_MISMATCH')
    try:session.mkdir(mode=0o700)
    except FileExistsError as exc:raise ProbeError('SESSION_EXISTS_NO_OVERWRITE') from exc
    for folder in ('credentials','probes','records','wave1','wave2'):(session/folder).mkdir(mode=0o700)
    world.update(ShiftId='simulation-probe-'+secrets.token_hex(16),Participants=[],Reports=[],Receipts=[],Sequence=0,Paused=False)
    world['Entities']=[e for e in world['Entities'] if e['Kind']==0]
    regions={r['Id']:r for r in geometry['Regions']};frames={f['FrameId']:f for f in geometry['Frames']};tickets=[]
    for name in NAMES:
        region=regions[CONCOURSE if name=='instructor' else MAINLINE];point=copy.deepcopy(region['Hub'])
        if name=='instructor':point['Z']-=1.5
        else:point['X']+=-.5 if name=='rider-a' else .5;point['Z']-=1.0
        world['Participants'].append({'ParticipantId':name,'TeamId':'command' if name=='instructor' else 'team-a' if name=='rider-a' else 'team-b',
            'RoleId':'instructor' if name=='instructor' else 'role-01' if name=='rider-a' else 'role-02','IsInstructor':name=='instructor',
            'InputEnabled':False,'RegionId':region['Id'],'FrameId':region['FrameId'],'PortalId':'','Position':point,
            'LocalPosition':local(frames[region['FrameId']],point),'ObservedIds':[]})
        secret=secrets.token_urlsafe(32);tickets.append({'ParticipantId':name,'SecretSha256':hashlib.sha256(secret.encode()).hexdigest()})
        private_json(session/'credentials'/f'{name}.json',{'WorldId':world['WorldId'],'ShiftId':world['ShiftId'],'ParticipantId':name,'Secret':secret})
        race={'AtSeconds':4,'CommandId':'race-'+name,'Kind':0,'TargetId':EQUIPMENT,'ExpectedRevision':0,'Argument':''}
        first=[] if name=='instructor' else [race,{**race,'AtSeconds':5},
            {'AtSeconds':7,'CommandId':'report-'+name+'-incident-1','Kind':2,'TargetId':'incident-1','Argument':'command'},
            {'AtSeconds':7.5,'CommandId':'report-'+name+'-incident-2','Kind':2,'TargetId':'incident-2','Argument':'command'}]
        second=([{'AtSeconds':3,'CommandId':'resume-simulation-probe','Kind':7,'TargetId':'','Argument':''}] if name=='instructor' else [{**race,'AtSeconds':4.5}])
        for wave,steps,seconds in ((1,first,15),(2,second,8)):
            private_json(session/'probes'/f'wave{wave}-{name}.json',{'Steps':steps,'Waypoints':[],'ExitWhenRouteComplete':False,'ExitAfterSeconds':seconds})
    simulation['ProfileId']+='-moving-rider-probe';simulation['NpcCount']=2;simulation['NpcSpawnRegions']=[CONCOURSE]
    wanted={'fire-concourse','fire-mainline'}
    simulation['Schedule'].update(ProfileId=simulation['Schedule']['ProfileId']+'-probe',MaximumActive=2,FirstOnsetTick=20,MinimumQuietTicks=1,MaximumQuietTicks=1)
    simulation['Schedule']['Choices']=[c for c in simulation['Schedule']['Choices'] if c['Id'] in wanted]
    simulation['Incidents']=[i for i in simulation['Incidents'] if i['ChoiceId'] in wanted]
    require(len(simulation['Schedule']['Choices'])==2 and all(c['Kind']==0 for c in simulation['Schedule']['Choices']), 'TWO_FIRE_CHOICES_REQUIRED')
    for train in simulation['Trains']:train['Operation']['Stops'][0]['DwellSeconds']=.5
    private_json(session/'server-session.json',{'World':world,'Tickets':tickets});private_json(session/'server-simulation.json',simulation)
    plan={'Schema':1,'Status':'PREPARED_NOT_RUN','Names':list(NAMES),'WorldId':world['WorldId'],'ShiftId':world['ShiftId'],
          'SpatialProfileId':geometry['ProfileId'],'FirstOnsetTick':20,'Wave1Seconds':12,'Wave1MaximumSeconds':15,'Wave2MaximumSeconds':8,
          'DefaultApp':str(project/'Builds/FoundationSimulationMac/ChooGuardFoundation.app'),
          'SourceHashes':{key:sha(path) for key,path in sources.items()},'FixtureHashes':{},
          'Scope':'three real protocol clients, two NPC, two synthetic fires, moving riding/in-car operations/recovery; no boarding route, WAN, voice, renderer or load acceptance'}
    plan['FixtureHashes']={str(path.relative_to(session)):sha(path) for path in [session/'server-session.json',session/'server-simulation.json',*sorted((session/'credentials').glob('*.json')),*sorted((session/'probes').glob('*.json'))]}
    private_json(session/'plan.json',plan)
    return plan


def events(path, *, final=True):
    path=Path(path);require(path.is_file(),'EVENTS_MISSING');require(path.stat().st_size<=MAX_EVENTS,'EVENTS_TOO_LARGE')
    rows=[]
    with path.open('rb') as stream:
        for raw in stream:
            require(len(raw)<=MAX_LINE,'EVENT_LINE_TOO_LARGE')
            if not raw.endswith(b'\n'):
                if final:raise ProbeError('TRUNCATED_EVENT_LINE')
                break
            row=strict_json(raw);require(isinstance(row,dict) and isinstance(row.get('Kind'),str),'EVENT_ENVELOPE')
            if 'Data' not in row:row['Data']=strict_json(row['Json'])
            require(isinstance(row.get('Time'),(int,float)) and math.isfinite(row['Time']),'EVENT_TIME')
            rows.append(row)
    return rows


def schema(value, allowed, code):
    require(isinstance(value,dict) and set(value)<=allowed,code)


def point(value):
    schema(value,{'X','Y','Z'},'POINT_FIELDS')
    require(set(value)=={'X','Y','Z'} and all(isinstance(v,(int,float)) and not isinstance(v,bool) and math.isfinite(v) for v in value.values()),'POINT_VALUE')


def frame(value):
    schema(value,FRAME_FIELDS,'FRAME_FIELDS');point(value['Origin'])
    require(isinstance(value['YawDegrees'],(int,float)) and math.isfinite(value['YawDegrees']),'FRAME_YAW')


def hidden_fields(value):
    if isinstance(value,dict):
        require(not(set(value)&FORBIDDEN_KEYS),'PRIVATE_OR_FUTURE_FIELD_IN_VIEW')
        for item in value.values():hidden_fields(item)
    elif isinstance(value,list):
        for item in value:hidden_fields(item)
    elif isinstance(value,str):
        require(not value.startswith(('CGP1:','CGP2:','CGC1:','CGC2:','CGWM1:','CGMP1:')),'CHECKPOINT_STRING_IN_VIEW')


def validate_view(view, name, plan, knowledge):
    hidden_fields(view);schema(view,VIEW_FIELDS,'VIEW_FIELDS')
    require(view.get('ProtocolVersion')==3 and view.get('SpatialProfileId')==plan['SpatialProfileId'],'SCHEMA3_VIEW_REQUIRED')
    observed=view['Observed'];schema(observed,OBSERVED_FIELDS,'OBSERVED_FIELDS')
    require((observed['WorldId'],observed['ShiftId'],observed['ParticipantId'])==(plan['WorldId'],plan['ShiftId'],name),'VIEW_IDENTITY')
    sequence,tick=integer(observed['Sequence']),integer(observed['SimulationTick'])
    require(isinstance(observed['Paused'],bool),'PAUSED_TYPE')
    require(sequence in knowledge and name in knowledge[sequence],'KNOWLEDGE_SEQUENCE_MISSING')
    seen=knowledge[sequence][name]
    for entity in observed['Entities']:
        schema(entity,ENTITY_FIELDS,'ENTITY_FIELDS');point(entity['Position']);point(entity['LocalPosition'])
        require(integer(entity['Kind'])<=3 and isinstance(entity['Active'],bool),'ENTITY_STATE_TYPE')
        integer(entity['Revision'])
        if entity['Kind']==2 or entity['EntityId'].startswith('incident-'):
            require(entity['Kind']==2 and entity['Active'] and entity['EntityId'] in seen,'UNOBSERVED_INCIDENT_EXPOSED')
            require(tick>=plan['FirstOnsetTick'],'INCIDENT_BEFORE_ONSET')
    team='command' if name=='instructor' else 'team-a' if name=='rider-a' else 'team-b'
    require(view['TeamId']==team and view['Instructor']==(name=='instructor') and isinstance(view['Instructor'],bool),'VIEW_ROLE_TEAM')
    for report in observed['Reports']:
        schema(report,REPORT_FIELDS,'REPORT_FIELDS');require(report['ToTeamId']==team,'REPORT_WRONG_TEAM')
        require(integer(report['ObservedSimulationTick'])<=tick and integer(report['Sequence'])<=sequence,'REPORT_FROM_FUTURE')
        point(report['Position']);point(report['LocalPosition']);frame(report['ObservedFrame'])
        require(distance(world_point(report['ObservedFrame'],report['LocalPosition']),report['Position'])<=.001,'REPORT_FRAME_POSE')
    physical=view['Physical'];schema(physical,PHYSICAL_FIELDS,'PHYSICAL_FIELDS')
    frames={f['FrameId']:f for f in physical['Frames']};require(len(frames)==len(physical['Frames']),'DUPLICATE_FRAME')
    for f in frames.values():frame(f)
    point(view['Position']);point(view['LocalPosition']);require(view['FrameId'] in frames,'ACTOR_FRAME_MISSING')
    require(distance(world_point(frames[view['FrameId']],view['LocalPosition']),view['Position'])<=.001,'ACTOR_FRAME_POSE')
    for body in physical['Bodies']:
        schema(body,BODY_FIELDS,'BODY_FIELDS');point(body['Position']);point(body['LocalPosition'])
        require(body['FrameId'] in frames and distance(world_point(frames[body['FrameId']],body['LocalPosition']),body['Position'])<=.001,'BODY_FRAME_POSE')
    for train in physical['Trains']:
        schema(train,TRAIN_FIELDS,'TRAIN_FIELDS');require(isinstance(train['SignedSpeedMS'],(int,float)) and not isinstance(train['SignedSpeedMS'],bool) and math.isfinite(train['SignedSpeedMS']) and isinstance(train['DoorOpen'],bool),'TRAIN_SPEED')
    for region in physical['Regions']:schema(region,{'RegionId','LightingOn','PublicAddressAvailable'},'REGION_EFFECT_FIELDS')
    for smoke in physical['Smoke']:
        schema(smoke,{'RegionId','Cell','UpperTemperatureK','LowerTemperatureK'},'SMOKE_FIELDS');schema(smoke['Cell'],SMOKE_CELL_FIELDS,'SMOKE_CELL_FIELDS')
    return view


def receipts(rows, command):return [r['Data'] for r in rows if r['Kind']=='receipt' and r['Data'].get('CommandId')==command]

def views(rows):return [r['Data'] for r in rows if r['Kind']=='view']

def observed_train(view):
    return next((t for t in view['Physical']['Trains'] if t['EntityId']=='train.mainline'),None)


def client_metric_baseline(rows, metric_rows):
    data=views(rows);require(data and metric_rows,'CLIENT_METRICS_OR_VIEWS_MISSING')
    require(metric_rows[0].get('Schema')==1 and metric_rows[0].get('Role')=='client' and metric_rows[0].get('Kind')=='interval','CLIENT_METRICS_SCHEMA')
    require(integer(metric_rows[0].get('SimulationTickStart'))==data[0]['Observed']['SimulationTick'],'CLIENT_METRICS_FIRST_VIEW_BASELINE')
    return metric_rows[0]['SimulationTickStart']


def analyze_data(plan, wave1, wave2, journal1, journal2, metrics, execution=None):
    checks={};issues=[]
    try:
        for wave,journal in ((wave1,journal1),(wave2,journal2)):
            for name in NAMES:
                require(views(wave.get(name,[])),'CLIENT_VIEWS_MISSING')
                for v in views(wave[name]):validate_view(v,name,plan,journal['KnowledgeBySequence'])
        checks['AllViewsSchema3PrivateAndKnowledgeChecked']=True
        race={name:receipts(wave1[name],'race-'+name) for name in RIDER_NAMES}
        require(all(len(rows)==2 for rows in race.values()),'TWO_RACE_RECEIPTS_REQUIRED')
        require(sorted(rows[0]['Code'] for rows in race.values())==[0,7],'RACE_NOT_ACCEPTED_AND_STALE')
        winner=next(name for name,rows in race.items() if rows[0]['Code']==0);winning=race[winner][0]
        require(race[winner][1]==winning,'DUPLICATE_CHANGED_RECEIPT')
        require(all(rows[1]['Code']==7 for name,rows in race.items() if name!=winner),'LOSER_DUPLICATE_NOT_STALE')
        durable1,durable2=journal1['State'],journal2['State']
        require(all(sum(e['Kind']==1 for e in d['Entities'])==2 and sum(e['Kind']==3 for e in d['Entities'])==2 for d in (durable1,durable2)),'TWO_NPC_TWO_TRAINS_REQUIRED')
        require(winning in durable1['Receipts'] and winning in durable2['Receipts'],'APPROVED_RECEIPT_NOT_RECOVERED')
        require(len([r for r in durable2['Receipts'] if r['ParticipantId']==winner and r['CommandId']=='race-'+winner])==1,'APPROVED_COMMAND_REPLAYED_TWICE')
        repeated=receipts(wave2[winner],'race-'+winner);require(repeated and all(r==winning for r in repeated),'RECOVERED_WINNER_RECEIPT_CHANGED')
        checks['RaceCodes']={name:rows[0]['Code'] for name,rows in race.items()};checks['ApprovedDuplicateSequence']=winning['Sequence']
        incident_map={e['EntityId']:e['RegionId'] for e in durable1['Entities'] if e['Kind']==2 and e['Active']}
        require(set(incident_map)=={'incident-1','incident-2'} and set(incident_map.values())=={MAINLINE,CONCOURSE},'TWO_ACTIVE_FIRE_REGIONS_MISSING')
        own_incident=next(i for i,r in incident_map.items() if r==MAINLINE)
        for name in RIDER_NAMES:
            for incident in ('incident-1','incident-2'):
                result=receipts(wave1[name],'report-'+name+'-'+incident)
                require(len(result)==1,'REPORT_RECEIPT_MISSING')
                require(result[0]['Code']==(0 if incident==own_incident else 10),'REPORT_OBSERVATION_GUARD')
        base_reports=[r for r in durable1['Reports'] if r['FromParticipantId'] in RIDER_NAMES]
        require(len(base_reports)==2 and all(r['EntityId']==own_incident and r['ToTeamId']=='command' for r in base_reports),'APPROVED_REPORTS_MISSING')
        require(all(r in durable2['Reports'] for r in base_reports),'DURABLE_REPORT_CHANGED')
        require(all(any(r in v['Observed']['Reports'] for v in views(wave1['instructor'])) for r in base_reports),'COMMAND_TEAM_REPORT_DELIVERY_MISSING')
        first_instructor=views(wave2['instructor'])[0]
        require(all(r in first_instructor['Observed']['Reports'] for r in base_reports),'RECOVERED_REPORT_VIEW_CHANGED')
        checks['ApprovedReportsRecovered']=len(base_reports)
        for name in NAMES:
            first=views(wave2[name])[0];saved=next(p for p in durable1['Participants'] if p['ParticipantId']==name)
            require(first['Observed']['Paused'] and first['Observed']['SimulationTick']==durable1['SimulationTick'],'RECOVERY_FIRST_VIEW_NOT_PAUSED_BOUNDARY')
            require((first['RegionId'],first['FrameId'])==(saved['RegionId'],saved['FrameId']) and distance(first['LocalPosition'],saved['LocalPosition'])<=.001,'RECOVERED_LOCAL_POSE_CHANGED')
            require(all(f in durable1['Frames'] for f in first['Physical']['Frames']),'RECOVERED_FRAME_CHANGED')
        checks['PausedDurableBoundaryTick']=durable1['SimulationTick']
        motion={}
        for name in RIDER_NAMES:
            first,last=views(wave1[name])[0],views(wave1[name])[-1]
            require(all(v['FrameId']=='train-mainline' and v['RegionId']==MAINLINE for v in views(wave1[name])+views(wave2[name])),'RIDER_LEFT_CAR_SCOPE')
            require(max(distance(first['LocalPosition'],v['LocalPosition']) for v in views(wave1[name]))<=.001,'RIDER_LOCAL_POSE_DRIFT')
            require(distance(first['Position'],last['Position'])>.01,'WAVE1_TRAIN_DID_NOT_MOVE')
            require(any(observed_train(v) and abs(observed_train(v)['SignedSpeedMS'])>0 for v in views(wave1[name])),'TRAIN_SPEED_NOT_OBSERVED')
            before=next((v for v in views(wave1[name]) if v['Observed']['SimulationTick']==durable1['SimulationTick']),None)
            after=views(wave2[name])[0]
            require(before is not None and observed_train(before) is not None and observed_train(after) is not None,'MATCHED_DURABLE_TRAIN_VIEW_MISSING')
            require(observed_train(before)==observed_train(after),'RECOVERED_TRAIN_SPEED_OR_DOOR_CHANGED')
            resumed=[v for v in views(wave2[name]) if not v['Observed']['Paused']]
            require(resumed and resumed[-1]['Observed']['SimulationTick']>after['Observed']['SimulationTick'],'RESUMED_PHYSICS_PROGRESS_MISSING')
            require(distance(after['Position'],resumed[-1]['Position'])>.01 and distance(after['LocalPosition'],resumed[-1]['LocalPosition'])<=.001,'RESUMED_RIDING_CHANGED')
            motion[name]={'Wave1DisplacementM':distance(first['Position'],last['Position']),'AfterResumeDisplacementM':distance(after['Position'],resumed[-1]['Position'])}
        resume=receipts(wave2['instructor'],'resume-simulation-probe');require(len(resume)==1 and resume[0]['Code']==0,'INSTRUCTOR_RESUME_NOT_ACCEPTED')
        checks['MovingRiders']=motion
        checks['ClientFirstViewMetricBaselines']={f'wave{w}-{n}':client_metric_baseline(wave[n],metrics[(w,n)]) for w,wave in ((1,wave1),(2,wave2)) for n in NAMES}
        actual=bool(execution and execution.get('Source')=='owned-player-run' and execution.get('RealPopen') is True and execution.get('ServerSIGKILL') is True)
        if actual:
            require(isinstance(execution.get('BinarySha256'),str) and len(execution['BinarySha256'])==64,'ACTUAL_BINARY_HASH_MISSING')
            require(execution.get('ExitCodes',{}).get('wave1-server')==-9,'SIGKILL_EXIT_EVIDENCE_MISSING')
            require(not execution.get('Failure') and execution['Wave1Seconds']<=15 and execution['Wave2Seconds']<=8,'WAVE_TIME_BOUND_EXCEEDED')
        status='PASS' if actual else 'CHECKS_PASSED_NOT_RUN'
    except (ProbeError,KeyError,TypeError,ValueError,StopIteration) as exc:
        issues.append(str(exc) if isinstance(exc,ProbeError) else 'MALFORMED_OR_INCOMPLETE_EVIDENCE')
        status='INCOMPLETE' if any(word in issues[0] for word in ('MISSING','REQUIRED','INCOMPLETE')) else 'FAIL'
    return {'Schema':1,'Status':status,'Checks':checks,'Issues':issues,'Scope':plan['Scope'],
            'NotVerified':['boarding routes','20/100/2 load','WAN','voice','GPU/rendered action latency','field fidelity/procedures']}


def binary_for(app):
    app=Path(app).resolve()
    if app.suffix=='.app':
        with (app/'Contents/Info.plist').open('rb') as stream:name=plistlib.load(stream)['CFBundleExecutable']
        require(isinstance(name,str) and Path(name).name==name and '/' not in name and '\\' not in name,'UNSAFE_APP_EXECUTABLE')
        binary=(app/'Contents/MacOS'/name).resolve();require(binary.is_relative_to((app/'Contents/MacOS').resolve()),'APP_EXECUTABLE_ESCAPE')
    else:binary=app
    require(binary.is_file() and os.access(binary,os.X_OK),'EXECUTABLE_MISSING')
    return binary


def command(binary,session,wave,name,port):
    session=Path(session);folder=session/f'wave{wave}'
    result=[str(binary),'-batchmode','-nographics','-logFile',str(folder/f'{name}.player.log'),
            '--cg-port',str(port),'--cg-address','127.0.0.1','--cg-listen','127.0.0.1',
            '--cg-evidence',str(folder/f'{name}.events.jsonl'),'--cg-metrics',str(folder/f'{name}.metrics.jsonl')]
    if name=='server':result+=['--cg-mode','server','--cg-session',str(session/'server-session.json'),'--cg-records',str(session/'records'),'--cg-simulation',str(session/'server-simulation.json'),'--cg-phase-profile',str(folder/'server.phases.jsonl')]
    else:result+=['--cg-mode','client','--cg-credential',str(session/'credentials'/f'{name}.json'),'--cg-probe',str(session/'probes'/f'wave{wave}-{name}.json')]
    return result


class ProcessOwner:
    def __init__(self,session,popen=subprocess.Popen,kill_group=os.killpg if hasattr(os,'killpg') else None):
        self.session=Path(session);self.popen=popen;self.kill_group=kill_group;self.processes={};self.handles=[];self.stopped=set()
    def launch(self,wave,name,argv):
        require(os.name=='posix','SIGKILL_PROBE_REQUIRES_POSIX')
        key=(wave,name);require(key not in self.processes,'DUPLICATE_PROCESS')
        folder=self.session/f'wave{wave}'
        require(not any((folder/f'{name}.{suffix}').exists() for suffix in ('player.log','events.jsonl','metrics.jsonl')),'EXISTING_WAVE_OUTPUT')
        handle=os.fdopen(os.open(folder/f'{name}.stdio.log',os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600),'wb');self.handles.append(handle)
        process=self.popen(argv,stdout=handle,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL,start_new_session=True,shell=False,cwd=str(self.session))
        self.processes[key]=process;return process
    def stop(self,key,*,crash=False):
        if key in self.stopped:return
        process=self.processes[key]
        if process.poll() is not None:
            self.stopped.add(key);return
        try:self.kill_group(process.pid,signal.SIGKILL if crash else signal.SIGTERM)
        except ProcessLookupError:pass
        try:process.wait(timeout=2 if crash else 3)
        except subprocess.TimeoutExpired:
            try:self.kill_group(process.pid,signal.SIGKILL)
            except ProcessLookupError:pass
            process.wait(timeout=3)
        self.stopped.add(key)
    def close(self):
        errors=[]
        for key in self.processes:
            try:self.stop(key)
            except (OSError,subprocess.TimeoutExpired):errors.append('OWNED_CLEANUP_FAILED')
        for handle in self.handles:handle.close()
        require(not errors,'OWNED_CLEANUP_FAILED')


def snapshot_records(source,destination):
    source,destination=Path(source),Path(destination);destination.mkdir(mode=0o700)
    for name in ('checkpoint-v3.json','actions-v3.jsonl'):
        path=source/name;require(path.is_file(),'JOURNAL_FILES_MISSING')
        with path.open('rb') as origin,os.fdopen(os.open(destination/name,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600),'wb') as target:
            while True:
                data=origin.read(1024*1024)
                if not data:break
                target.write(data)


def raw_metrics(path):
    path=Path(path);require(path.is_file(),'CLIENT_METRICS_MISSING')
    require(path.stat().st_size<=4*1024*1024,'METRICS_FILE_BOUND')
    with path.open('rb') as stream:
        rows=[]
        for raw in stream:
            require(raw.endswith(b'\n'),'TRUNCATED_METRIC_LINE');rows.append(strict_json(raw))
    return rows


def runtime_error_log_counts(session):
    counts={}
    markers=(b'exception:',b'startup failed',b'world simulation transaction failed',b'checkpoint unavailable',b'fatal error',b'invalid server snapshot')
    for wave in (1,2):
        for name in ('server',*NAMES):
            path=Path(session)/f'wave{wave}'/f'{name}.player.log'
            if not path.is_file():continue
            require(path.stat().st_size<=MAX_EVENTS,'PLAYER_LOG_BOUND')
            count=0
            with path.open('rb') as stream:
                for line in stream:
                    if any(marker in line.lower() for marker in markers):count+=1
            if count:counts[f'wave{wave}-{name}']=count
    return counts


def analyze(session):
    session=Path(session);plan=strict_json((session/'plan.json').read_bytes())
    try:
        wave1={n:events(session/'wave1'/f'{n}.events.jsonl') for n in NAMES}
        wave2={n:events(session/'wave2'/f'{n}.events.jsonl') for n in NAMES}
        journal1=read_journal(session/'wave1-records');journal2=read_journal(session/'records')
        metrics={(w,n):raw_metrics(session/f'wave{w}'/f'{n}.metrics.jsonl') for w in (1,2) for n in NAMES}
        execution=strict_json((session/'execution.json').read_bytes()) if (session/'execution.json').is_file() else None
        if execution:
            require(execution.get('FixtureHashes')==plan['FixtureHashes'],'FIXTURE_HASH_MISMATCH')
            for name,digest in plan['FixtureHashes'].items():require(sha(session/name)==digest,'FIXTURE_CHANGED')
        result=analyze_data(plan,wave1,wave2,journal1,journal2,metrics,execution)
        result['JournalSourceHashes']={'wave1':journal1['SourceHashes'],'final':journal2['SourceHashes']}
        result['TruncatedUnacknowledgedTailBytes']=journal1['TruncatedTailBytes']
    except (ProbeError,OSError,KeyError,TypeError,ValueError) as exc:
        result={'Schema':1,'Status':'INCOMPLETE','Issues':[str(exc) if isinstance(exc,ProbeError) else 'EVIDENCE_UNAVAILABLE_OR_INVALID'],'Scope':plan['Scope']}
    result['RuntimeErrorLogCounts']=runtime_error_log_counts(session)
    if result['RuntimeErrorLogCounts']:
        result.update(Status='FAIL',Issues=result.get('Issues',[])+['RUNTIME_ERROR_LOG'])
    return result


def run(app,session,port=18979,*,popen=subprocess.Popen,clock=time.monotonic,sleep=time.sleep):
    """Future real execution path for root. Never called with real Popen by authoring tests."""
    session=Path(session).resolve();plan=strict_json((session/'plan.json').read_bytes());binary=binary_for(app)
    for name,expected in plan['FixtureHashes'].items():require(sha(session/name)==expected,'FIXTURE_CHANGED')
    require(plan.get('Wave1Seconds')==12 and plan.get('Wave1MaximumSeconds')==15 and plan.get('Wave2MaximumSeconds')==8,'FIXED_WAVE_BOUNDS_REQUIRED')
    require(1<=port<=65535,'PORT_RANGE');owner=ProcessOwner(session,popen=popen)
    execution={'Source':'owned-player-run','RealPopen':popen is subprocess.Popen,'BinarySha256':sha(binary),
               'FixtureHashes':plan['FixtureHashes'],'ServerSIGKILL':False,'Wave1Seconds':None,'Wave2Seconds':None}
    failure=None
    def wait_server(wave):
        process=owner.launch(wave,'server',command(binary,session,wave,'server',port));deadline=clock()+40
        while True:
            require(process.poll() is None,'SERVER_STARTUP_EXIT')
            path=session/f'wave{wave}'/'server.events.jsonl'
            if path.exists() and any(r['Kind']=='server_started' for r in events(path,final=False)):return process
            require(clock()<deadline,'SERVER_STARTUP_TIMEOUT');sleep(.05)
    try:
        wait_server(1)
        started=clock()
        for name in NAMES:owner.launch(1,name,command(binary,session,1,name,port))
        while clock()-started<plan['Wave1Seconds']:
            require(all(owner.processes[(1,n)].poll() is None for n in ('server',*NAMES)),'WAVE1_EARLY_EXIT')
            sleep(.05)
        execution['Wave1Seconds']=clock()-started
        owner.stop((1,'server'),crash=True);execution['ServerSIGKILL']=owner.processes[(1,'server')].poll()==-signal.SIGKILL
        for name in NAMES:owner.stop((1,name))
        snapshot_records(session/'records',session/'wave1-records')
        wait_server(2)
        started=clock()
        for name in NAMES:owner.launch(2,name,command(binary,session,2,name,port))
        while clock()-started<7.5:
            require(all(owner.processes[(2,n)].poll() is None for n in ('server',*NAMES)),'WAVE2_EARLY_EXIT')
            sleep(.05)
        execution['Wave2Seconds']=clock()-started
        for name in NAMES:owner.stop((2,name))
        owner.stop((2,'server'))
    except (ProbeError,OSError,subprocess.TimeoutExpired,KeyboardInterrupt) as exc:
        failure=str(exc) if isinstance(exc,ProbeError) else 'EXECUTION_ABORTED'
    finally:
        try:owner.close()
        except (ProbeError,OSError):failure='OWNED_CLEANUP_FAILED'
        execution['Failure']=failure
        execution['ExitCodes']={f'wave{w}-{name}':p.poll() for (w,name),p in owner.processes.items()}
        private_json(session/'execution.json',execution)
    result=analyze(session)
    if failure:result.update(Status='FAIL',Issues=[failure]+result.get('Issues',[]))
    private_json(session/'result.json',result)
    print(json.dumps({'Status':result['Status'],'IssueCount':len(result.get('Issues',[])),'Scope':'bounded moving-rider protocol probe'}))
    return result


class SafeParser(argparse.ArgumentParser):
    def error(self,message):self.exit(2,'{"Status":"ERROR","Code":"INVALID_ARGUMENTS"}\n')


def main(argv=None):
    parser=SafeParser(description=__doc__);parser.add_argument('--project',type=Path,default=Path.cwd());parser.add_argument('--session',type=Path,required=True)
    parser.add_argument('--app',type=Path);parser.add_argument('--port',type=int,default=18979)
    mode=parser.add_mutually_exclusive_group();mode.add_argument('--prepare-only',action='store_true');mode.add_argument('--analyze-only',action='store_true');mode.add_argument('--run-prepared',action='store_true')
    args=parser.parse_args(argv)
    try:
        if args.analyze_only:
            result=analyze(args.session);print(json.dumps({'Status':result['Status'],'IssueCount':len(result.get('Issues',[]))}));return 0 if result['Status']=='PASS' else 1
        plan=strict_json((args.session/'plan.json').read_bytes()) if args.run_prepared else prepare(args.project,args.session)
        if args.prepare_only:
            print(json.dumps({'Status':'PREPARED_NOT_RUN','RequestedClients':3,'RequestedNpcCount':2,'Wave1MaximumSeconds':15,'Wave2MaximumSeconds':8}));return 0
        result=run(args.app or plan['DefaultApp'],args.session,args.port);return 0 if result['Status']=='PASS' else 1
    except (ProbeError,OSError,KeyError,TypeError,ValueError):
        print('{"Status":"ERROR","Code":"PREPARATION_OR_EVIDENCE_FAILURE"}');return 2


if __name__=='__main__':raise SystemExit(main())
