import base64
import contextlib
import copy
import io
import json
import os
import stat
import tempfile
import unittest
import zlib
from pathlib import Path
from unittest import mock

import probe_journal as journal
import run_simulation_probe as probe

ROOT=Path(__file__).resolve().parent
PROJECT=Path(os.environ.get('CHOOGUARD_PROJECT',Path(__file__).resolve().parents[2]))


def point(x=0,y=0,z=0):return {'X':x,'Y':y,'Z':z}

def frame(x):return {'FrameId':'train-mainline','Origin':point(x,2,15),'YawDegrees':0}

def report(name,sequence):
    return {'ReportId':'report-'+str(sequence),'FromParticipantId':name,'ToTeamId':'command','EntityId':'incident-1',
            'RegionId':probe.MAINLINE,'FrameId':'train-mainline','Position':point(107,2,15),'LocalPosition':point(),
            'ObservedRevision':0,'Sequence':sequence,'ObservedSimulationTick':140,'ObservedFrame':frame(107),'AcknowledgedBy':[]}

def receipt(name,command,code,sequence):
    return {'WorldId':'w','ShiftId':'s','ParticipantId':name,'CommandId':command,'Fingerprint':'f'*64,'Code':code,'Sequence':sequence}

def entity(id,kind,region,frame_id,local_position,world_position,revision=0):
    return {'EntityId':id,'Kind':kind,'RegionId':region,'FrameId':frame_id,'LocalPosition':local_position,'Position':world_position,
            'RequiredRoleId':'','LeaderId':'','Revision':revision,'Active':True}

def synthetic():
    plan={'WorldId':'w','ShiftId':'s','SpatialProfileId':'p','FirstOnsetTick':20,'Scope':'synthetic test only'}
    reports=[report('rider-a',5),report('rider-b',6)]
    winning=receipt('rider-a','race-rider-a',0,4)
    frames=[{'FrameId':'world','Origin':point(),'YawDegrees':0},frame(110),{'FrameId':'train-metro','Origin':point(300,-8,15),'YawDegrees':180}]
    participants=[]
    for name in probe.NAMES:
        local=point(0,6,-1.5) if name=='instructor' else point(-.5 if name=='rider-a' else .5,0,-1)
        participants.append({'ParticipantId':name,'RegionId':probe.CONCOURSE if name=='instructor' else probe.MAINLINE,
            'FrameId':'world' if name=='instructor' else 'train-mainline','LocalPosition':local,
            'Position':local if name=='instructor' else probe.world_point(frame(110),local),
            'ObservedIds':['incident-2'] if name=='instructor' else ['incident-1']})
    entities=[entity('incident-1',2,probe.MAINLINE,'train-mainline',point(),point(110,2,15)),entity('incident-2',2,probe.CONCOURSE,'world',point(0,6,0),point(0,6,0)),
              entity('npc-000',1,probe.CONCOURSE,'world',point(1,6,1),point(1,6,1)),entity('npc-001',1,probe.CONCOURSE,'world',point(2,6,1),point(2,6,1)),
              entity('train.mainline',3,probe.MAINLINE,'train-mainline',point(),point(110,2,15)),entity('train.metro',3,'rolling_stock_metro','train-metro',point(),point(300,-8,15))]
    state={'SchemaVersion':3,'WorldId':'w','ShiftId':'s','Sequence':6,'SimulationTick':200,'Participants':participants,'Frames':frames,'Entities':entities,'Receipts':[winning],'Reports':reports}
    know={0:{n:set() for n in probe.NAMES}}
    for sequence in range(1,8):
        know[sequence]={'instructor':{'incident-2'},'rider-a':{'incident-1'} if sequence>=2 else set(),'rider-b':{'incident-1'} if sequence>=3 else set()}
    j1={'State':copy.deepcopy(state),'KnowledgeBySequence':know};j2=copy.deepcopy(j1);j2['State']['SimulationTick']=240;j2['State']['Sequence']=7
    def view(name,tick,sequence,x,paused=False):
        p=next(p for p in participants if p['ParticipantId']==name);visible=[]
        if sequence:
            wanted='incident-2' if name=='instructor' else 'incident-1'
            visible=[copy.deepcopy(e) for e in entities if e['EntityId']==wanted]
            if name!='instructor':visible[0]['Position']=point(x,2,15)
        local=copy.deepcopy(p['LocalPosition']);position=local if name=='instructor' else probe.world_point(frame(x),local)
        return {'ProtocolVersion':3,'SpatialProfileId':'p','RegionId':p['RegionId'],'FrameId':p['FrameId'],'PortalId':'','Position':position,'LocalPosition':local,
                'TeamId':'command' if name=='instructor' else 'team-a' if name=='rider-a' else 'team-b','RoleId':'instructor' if name=='instructor' else 'role-01' if name=='rider-a' else 'role-02',
                'Instructor':name=='instructor','RequiredRegions':[p['RegionId']],'Portals':[],'MovementStatus':'',
                'Observed':{'WorldId':'w','ShiftId':'s','ParticipantId':name,'Sequence':sequence,'SimulationTick':tick,'Paused':paused,'TotalReports':2 if name=='instructor' and sequence>=6 else 0,
                            'Entities':visible,'Reports':copy.deepcopy(reports) if name=='instructor' and sequence>=6 else []},
                'Physical':{'Frames':[frames[0],frame(x)],'Bodies':[],'Trains':[{'EntityId':'train.mainline','FrameId':'train-mainline','SignedSpeedMS':3 if tick>=40 else 0,'DoorOpen':False}],
                            'Regions':[],'Smoke':[]}}
    def event(kind,data,time):return {'Kind':kind,'Data':data,'Time':time}
    w1={n:[event('view',view(n,1,0,100),.5),event('view',view(n,40,3,102),2),event('view',view(n,200,6,110),10)] for n in probe.NAMES}
    w2={n:[event('view',view(n,200,6,110,True),.5),event('view',view(n,210,7,111),4),event('view',view(n,240,7,114),6)] for n in probe.NAMES}
    for name in probe.RIDER_NAMES:
        r=winning if name=='rider-a' else receipt(name,'race-'+name,7,4)
        w1[name]+=[event('receipt',copy.deepcopy(r),4),event('receipt',copy.deepcopy(r),5),event('receipt',receipt(name,'report-'+name+'-incident-1',0,5 if name=='rider-a' else 6),7),
                   event('receipt',receipt(name,'report-'+name+'-incident-2',10,6),7.5)]
        w1[name].sort(key=lambda e:e['Time']);w2[name].append(event('receipt',copy.deepcopy(r),4.5));w2[name].sort(key=lambda e:e['Time'])
    w2['instructor'].append(event('receipt',receipt('instructor','resume-simulation-probe',0,7),3));w2['instructor'].sort(key=lambda e:e['Time'])
    metrics={(w,n):[{'Schema':1,'Kind':'interval','Role':'client','SimulationTickStart':1 if w==1 else 200}] for w in (1,2) for n in probe.NAMES}
    return plan,w1,w2,j1,j2,metrics


class PreparationTests(unittest.TestCase):
    def test_exact_private_fixture_no_source_changes_or_execution(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
            path=Path(tmp)/'session';before=(PROJECT/'foundation/world/foundation-simulation-profile.json').read_bytes()
            with mock.patch.object(probe,'run',side_effect=AssertionError('no real execution')):
                plan=probe.prepare(PROJECT,path)
            session=journal.strict_json((path/'server-session.json').read_bytes());sim=journal.strict_json((path/'server-simulation.json').read_bytes())
            self.assertEqual(len(session['World']['Participants']),3);self.assertEqual(sim['NpcCount'],2);self.assertEqual(sim['NpcSpawnRegions'],[probe.CONCOURSE])
            self.assertEqual(sim['Schedule']['FirstOnsetTick'],20);self.assertEqual(sim['Schedule']['MinimumQuietTicks'],1);self.assertEqual(sim['Schedule']['MaximumQuietTicks'],1)
            self.assertEqual(len(sim['Schedule']['Choices']),2);self.assertTrue(all(c['Kind']==0 for c in sim['Schedule']['Choices']))
            self.assertTrue(all(t['Operation']['Stops'][0]['DwellSeconds']==.5 for t in sim['Trains']))
            self.assertTrue(plan['DefaultApp'].endswith('/Builds/FoundationSimulationMac/ChooGuardFoundation.app'))
            for name in probe.RIDER_NAMES:
                first=journal.strict_json((path/'probes'/f'wave1-{name}.json').read_bytes())
                self.assertEqual([s['AtSeconds'] for s in first['Steps']],[4,5,7,7.5]);self.assertEqual(first['Steps'][0]['CommandId'],first['Steps'][1]['CommandId'])
                self.assertEqual(first['Waypoints'],[]);self.assertEqual(first['ExitAfterSeconds'],15)
                self.assertEqual(first['Steps'][2]['Argument'],'command')
            second=journal.strict_json((path/'probes/wave2-instructor.json').read_bytes());self.assertEqual(second['Steps'][0]['AtSeconds'],3);self.assertEqual(second['ExitAfterSeconds'],8)
            if os.name=='posix':
                for p in [path,*path.rglob('*')]:self.assertEqual(stat.S_IMODE(p.stat().st_mode),0o700 if p.is_dir() else 0o600)
            with self.assertRaises(journal.ProbeError):probe.prepare(PROJECT,path)
            self.assertEqual(before,(PROJECT/'foundation/world/foundation-simulation-profile.json').read_bytes())
            for name in probe.NAMES:
                argv=probe.command(Path('/not-executed'),path,1,name,12345);self.assertNotIn('--cg-simulation',argv);self.assertIn('--cg-evidence',argv);self.assertIn('--cg-metrics',argv)
            server=probe.command(Path('/not-executed'),path,2,'server',12345);self.assertIn('--cg-simulation',server);self.assertIn('--cg-records',server)

    def test_prepare_cli_does_not_print_private_values(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
            output=io.StringIO()
            with mock.patch.object(probe,'run',side_effect=AssertionError('must not run')),contextlib.redirect_stdout(output):
                self.assertEqual(probe.main(['--project',str(PROJECT),'--session',str(Path(tmp)/'s'),'--prepare-only']),0)
            self.assertNotIn(tmp,output.getvalue());self.assertNotIn('Secret',output.getvalue());self.assertEqual(json.loads(output.getvalue())['Status'],'PREPARED_NOT_RUN')


class AnalysisTests(unittest.TestCase):
    def test_valid_synthetic_fixture_never_claims_actual_pass(self):
        result=probe.analyze_data(*synthetic())
        self.assertEqual(result['Status'],'CHECKS_PASSED_NOT_RUN',result)
        self.assertEqual(result['Checks']['ApprovedReportsRecovered'],2)
        self.assertEqual(result['Checks']['ClientFirstViewMetricBaselines']['wave2-rider-a'],200)

    def test_unseen_incident_and_checkpoint_or_future_fields_are_rejected(self):
        for change in ('unseen','checkpoint','future','wrong_kind'):
            data=list(synthetic());v=data[1]['rider-a'][0]['Data']
            if change=='unseen':v['Observed']['Entities']=[copy.deepcopy(data[3]['State']['Entities'][0])]
            elif change=='checkpoint':v['SimulationCheckpoint']='CGP2:never-public'
            elif change=='future':v['Physical']['Schedule']={'FirstOnsetTick':20}
            else:
                e=copy.deepcopy(data[3]['State']['Entities'][0]);e['Kind']='2';v['Observed']['Entities']=[e]
            self.assertEqual(probe.analyze_data(*data)['Status'],'FAIL',change)

    def test_reports_do_not_authorize_unseen_entity_streaming(self):
        data=list(synthetic());self.assertEqual(probe.analyze_data(*data)['Status'],'CHECKS_PASSED_NOT_RUN')
        data[2]['instructor'][0]['Data']['Observed']['Entities'].append(copy.deepcopy(data[3]['State']['Entities'][0]))
        self.assertEqual(probe.analyze_data(*data)['Issues'],['UNOBSERVED_INCIDENT_EXPOSED'])

    def test_duplicate_winner_sequence_and_recovered_report_must_match(self):
        data=list(synthetic());winner=probe.receipts(data[1]['rider-a'],'race-rider-a');winner[1]['Sequence']+=1
        self.assertEqual(probe.analyze_data(*data)['Issues'],['DUPLICATE_CHANGED_RECEIPT'])
        data=list(synthetic());data[4]['State']['Reports'][0]['Position']['X']+=1
        self.assertEqual(probe.analyze_data(*data)['Issues'],['DURABLE_REPORT_CHANGED'])

    def test_loser_rejection_sequence_may_advance_without_becoming_an_approval(self):
        data=list(synthetic());probe.receipts(data[1]['rider-b'],'race-rider-b')[1]['Sequence']+=1
        self.assertEqual(probe.analyze_data(*data)['Status'],'CHECKS_PASSED_NOT_RUN')

    def test_recovery_uses_durable_tick_and_missing_matching_train_view_is_unknown(self):
        data=list(synthetic());data[2]['rider-a'][0]['Data']['Observed']['SimulationTick']+=1
        self.assertEqual(probe.analyze_data(*data)['Issues'],['RECOVERY_FIRST_VIEW_NOT_PAUSED_BOUNDARY'])
        data=list(synthetic());data[1]['rider-a']=[r for r in data[1]['rider-a'] if r['Kind']!='view' or r['Data']['Observed']['SimulationTick']!=200]
        self.assertEqual(probe.analyze_data(*data)['Status'],'INCOMPLETE')

    def test_motion_without_local_pose_constancy_and_old_metric_zero_baseline_fail(self):
        data=list(synthetic());last=probe.views(data[1]['rider-a'])[-1];last['LocalPosition']['X']+=.2;last['Position']['X']+=.2
        self.assertEqual(probe.analyze_data(*data)['Issues'],['RIDER_LOCAL_POSE_DRIFT'])
        data=list(synthetic());data[5][(2,'rider-b')][0]['SimulationTickStart']=0
        self.assertEqual(probe.analyze_data(*data)['Issues'],['CLIENT_METRICS_FIRST_VIEW_BASELINE'])


def envelope(kind,ordinal,sequence,tick,previous,body):
    plain=json.dumps(body,separators=(',',':')).encode();compressor=zlib.compressobj(wbits=-zlib.MAX_WBITS);compressed=compressor.compress(plain)+compressor.flush()
    result={'Schema':3,'Kind':kind,'WorldId':'w','ShiftId':'s','DefinitionHash':'d'*64,'Ordinal':ordinal,'Sequence':sequence,'Tick':tick,'PreviousHash':previous,
            'PlainBytes':len(plain),'Payload':base64.b64encode(compressed).decode()}
    result['Hash']=journal.digest(result);return result


class JournalTests(unittest.TestCase):
    def test_hash_chain_approval_knowledge_and_durable_tail_are_read_without_printing(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
            p=Path(tmp);participant={'ParticipantId':'a','ObservedIds':[]};state={'SchemaVersion':3,'WorldId':'w','ShiftId':'s','SimulationDefinitionHash':'d'*64,
                'SimulationTick':0,'Sequence':0,'Participants':[participant],'Frames':[],'Entities':[],'Reports':[],'Receipts':[]}
            cp=envelope('checkpoint',0,0,0,journal.EMPTY_HASH,state)
            person={'ParticipantId':'a','ObservedIds':['incident-1']};rc=receipt('a','discover',0,1)
            command={'WorldSchemaVersion':3,'SimulationTick':20,'SimulationDefinitionHash':'d'*64,'Receipt':rc,'Participants':[person],'Entities':[], 'Frames':[],'Reports':[],'SimulationCheckpoint':'private-cp','Paused':False}
            first=envelope('command',1,1,20,journal.EMPTY_HASH,command)
            boundary={'Sequence':1,'Participants':[person],'Entities':[],'Frames':[],'Checkpoint':'private-cp-2','Paused':False}
            second=envelope('boundary',2,1,40,first['Hash'],boundary)
            (p/'checkpoint-v3.json').write_text(json.dumps(cp));(p/'actions-v3.jsonl').write_text(json.dumps(first)+'\n'+json.dumps(second)+'\n'+'{"partial":')
            result=journal.read_journal(p)
            self.assertEqual(result['State']['SimulationTick'],40);self.assertEqual(result['State']['Receipts'],[rc]);self.assertEqual(result['KnowledgeBySequence'][1]['a'],{'incident-1'})
            self.assertGreater(result['TruncatedTailBytes'],0)
            second['PreviousHash']='f'*64;second['Hash']=journal.digest(second);(p/'actions-v3.jsonl').write_text(json.dumps(first)+'\n'+json.dumps(second)+'\n')
            with self.assertRaisesRegex(journal.ProbeError,'JOURNAL_ORDER'):journal.read_journal(p)

    def test_bad_digest_inflate_length_and_duplicate_json_are_rejected(self):
        e=envelope('checkpoint',0,0,0,journal.EMPTY_HASH,{'x':1});e['Hash']='0'*64
        with self.assertRaises(journal.ProbeError):journal.decode(e)
        e=envelope('checkpoint',0,0,0,journal.EMPTY_HASH,{'x':1});e['PlainBytes']+=1;e['Hash']=journal.digest(e)
        with self.assertRaises(journal.ProbeError):journal.decode(e)
        with self.assertRaises(journal.ProbeError):journal.strict_json('{"x":1,"x":2}')

    def test_partial_event_is_waited_for_during_poll_but_incomplete_at_final(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
            p=Path(tmp)/'events.jsonl';p.write_text(json.dumps({'Kind':'view','Json':'{}','Time':1})+'\n'+ '{"partial"')
            self.assertEqual(len(probe.events(p,final=False)),1)
            with self.assertRaisesRegex(journal.ProbeError,'TRUNCATED_EVENT_LINE'):probe.events(p)


class ProcessTests(unittest.TestCase):
    def test_exited_owned_process_is_reaped_without_signalling_a_reused_group(self):
        process=mock.Mock(pid=9000);process.poll.return_value=0
        owner=probe.ProcessOwner(Path('.'),kill_group=mock.Mock())
        owner.processes[(1,'server')]=process
        owner.stop((1,'server'));owner.kill_group.assert_not_called()

    def test_incomplete_evidence_keeps_runtime_error_diagnostic(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
            p=Path(tmp);(p/'wave1').mkdir();(p/'plan.json').write_text(json.dumps({'Scope':'fixture'}))
            (p/'wave1/rider-a.player.log').write_text('Invalid server snapshot: OverflowException\n')
            result=probe.analyze(p)
            self.assertEqual(result['RuntimeErrorLogCounts'],{'wave1-rider-a':1})
            self.assertIn('RUNTIME_ERROR_LOG',result['Issues'])

    def test_only_owned_groups_receive_sigkill_and_cleanup(self):
        class Fake:
            def __init__(self,pid):self.pid=pid;self.code=None
            def poll(self):return self.code
            def wait(self,timeout=None):return self.code
        with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
            p=Path(tmp);(p/'wave1').mkdir();made=[];calls=[];options=[]
            def popen(argv,**kwargs):options.append(kwargs);v=Fake(9000+len(made));made.append(v);return v
            def kill(pid,sig):calls.append((pid,sig));next(v for v in made if v.pid==pid).code=-sig
            owner=probe.ProcessOwner(p,popen=popen,kill_group=kill);owner.launch(1,'server',['not-executed']);owner.launch(1,'rider-a',['not-executed'])
            owner.stop((1,'server'),crash=True);self.assertEqual(made[0].code,-9);owner.close()
            self.assertEqual({pid for pid,_ in calls},{9000,9001});self.assertTrue(all(o['start_new_session'] and not o['shell'] for o in options));self.assertTrue(all(h.closed for h in owner.handles))


if __name__=='__main__':unittest.main()
