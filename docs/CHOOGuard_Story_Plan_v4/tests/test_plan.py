import copy,hashlib,json,subprocess,sys,tempfile,shutil,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools'))
from planlib import *

class StaticPlanTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.p=load()
 def changed(self):return copy.deepcopy(self.p)
 def test_bootstrap_owns_initial_assemblies(self):
  owner='CS-BOOT.01.01'
  targets={
   'Assets/ChooGuard/Editor/ChooGuard.Editor.asmdef':'CS-BOOT.02.01',
   'Assets/ChooGuard/Tests/EditMode/ChooGuard.EditModeTests.asmdef':'CS-BOOT.03.01',
   'Assets/ChooGuard/Tests/PlayMode/ChooGuard.PlayModeTests.asmdef':'CS-BOOT.03.01'}
  for path,successor in targets.items():
   with self.subTest(path=path):
    rows=[(s['id'],w) for s in self.p['stories'] for w in s['writes'] if w['path']==path]
    self.assertEqual([(sid,w['operation'],w['createdBy']) for sid,w in rows],[(owner,'CREATE',owner),(successor,'MODIFY',owner)])
 def test_bootstrap_tmp_whitelist_and_metadata_are_owned(self):
  owner='CS-BOOT.01.01'
  resources={
   'Assets/ChooGuard/Settings/Resources/TMP Settings.asset',
   'Assets/ChooGuard/Settings/TMP/Fonts/ChooGuard Bootstrap SDF.asset',
   'Assets/ChooGuard/Settings/TMP/Shaders/TMP_SDF-Mobile.shader',
   'Assets/ChooGuard/Settings/TMP/Shaders/TMPro_Properties.cginc',
   'Assets/ChooGuard/Settings/TMP/LineBreaking/Leading Characters.txt',
   'Assets/ChooGuard/Settings/TMP/LineBreaking/Following Characters.txt',
   'Assets/ChooGuard/ThirdPartyNotices/LiberationSans-SDF-OFL.txt'}
  metadata={path+'.meta' for path in resources}
  for path in resources:
   metadata.update(str(parent)+'.meta' for parent in Path(path).parents if str(parent) not in ['.','Assets'])
  expected=resources|metadata
  story=next(s for s in self.p['stories'] if s['id']==owner)
  writes={w['path']:w for w in story['writes']}
  self.assertEqual(len(writes),len(story['writes']))
  for path in sorted(expected):
   with self.subTest(path=path):
    self.assertIn(path,writes)
    self.assertEqual((writes[path]['operation'],writes[path]['createdBy']),('CREATE',owner))
    self.assertIn(path,story['outputs'][0]['paths'])
  prefixes=('Assets/ChooGuard/Settings/TMP/','Assets/ChooGuard/Settings/Resources/','Assets/ChooGuard/ThirdPartyNotices/')
  actual={path for path in writes if path.startswith(prefixes) or path in metadata}
  self.assertEqual(actual,expected)
 def test_ontology_write_targets_equal_canonical_writes(self):
  nodes=read_json(ROOT/'ontology/work-graph.jsonld')['@graph']
  indexed={n['@id']:n for n in nodes}
  self.assertEqual(len(indexed),len(nodes))
  for s in self.p['stories']:
   with self.subTest(story=s['id']):
    refs=indexed['urn:chooguard:work:'+s['id']]['writeTarget']
    paths=[w['path'] for w in s['writes']]
    self.assertEqual(refs,['urn:chooguard:work:path:'+hashlib.sha256(p.encode('utf8')).hexdigest()[:20] for p in paths])
    self.assertEqual([indexed[ref]['path'] for ref in refs],paths)
    self.assertTrue(all(indexed[ref]['@type']=='cg:WriteTarget' for ref in refs))
 def test_complete_plan_validates(self):self.assertEqual(validate_plan(self.p),[])
 def test_all_profiles_have_acyclic_phase_graphs(self):
  for profile in self.p['profiles']:self.assertEqual(len(tuple(TopologicalSorter(phase_graph(self.p,profile)).static_order())),len(self.p['stories'])*4)
 def test_platform_regression_rejected(self):
  p=self.changed();p['platform']='WEB';self.assertIn('FIXED_PRODUCT_CONSTRAINT:platform',validate_plan(p))
 def test_third_mode_rejected(self):
  p=self.changed();p['productModes'].append('RESEARCH');self.assertIn('PRODUCT_MODES_CHANGED',validate_plan(p))
 def test_missing_parent_rejected(self):
  p=self.changed();p['stories'][0]['parentTaskId']='EP00';self.assertTrue(any('UNKNOWN_PARENT' in x for x in validate_plan(p)))
 def test_empty_steps_or_given_rejected(self):
  p=self.changed();p['stories'][0]['acceptance'][0]['given']='';self.assertTrue(validate_plan(p))
 def test_wrong_artifact_producer_rejected(self):
  p=self.changed();s=next(s for s in p['stories'] if s['requires']);s['requires'][0]['artifactId']='OUT-CS-MISSING.01.01';self.assertTrue(any('OUTPUT_OWNER' in x for x in validate_plan(p)))
 def test_unknown_condition_is_not_false(self):
  with self.assertRaises(PlanError):active(self.p,'SILENT_FALSE','fixture')
 def test_unknown_profile_rejected(self):
  with self.assertRaises(PlanError):active(self.p,'ALWAYS','missing')
 def test_cycle_detected(self):
  p=self.changed();a,b=p['stories'][:2];a['requires'].append({'producer':b['id'],'artifactId':'OUT-'+b['id'],'producerStage':'candidate','consumerStage':'candidate','condition':'ALWAYS','why':'test cycle'})
  self.assertTrue(any('CYCLE' in x.upper() or 'PHASE_GRAPH' in x for x in validate_plan(p)))
 def test_parent_children_cannot_disappear(self):
  p=self.changed();p['parentGates'][0]['children'].pop();self.assertTrue(any('PARENT_GATE_DRIFT' in x for x in validate_plan(p)))
 def test_scientific_input_does_not_block_fixture_coding(self):
  p=self.p;s=next(s for s in p['stories'] if s['id']=='CS-SIM.04.01')
  self.assertFalse(any(e['requiredAt']=='candidate' and 'HOLDOUT' in e['id'] for e in s['externalInputs']))
 def test_source_url_cannot_silently_change(self):
  p=self.changed();s=next(s for s in p['stories'] if s['sourceRefs']);s['sourceRefs'][0]['url']='https://example.org/changed';self.assertTrue(any('SOURCE_URL_DRIFT' in e for e in validate_plan(p)))
 def test_requirement_support_links_preserved(self):
  p=self.changed();s=next(s for s in p['stories'] if s['supportsRequirementIds']);s['supportsRequirementIds']=[];self.assertTrue(any('SUPPORT_REQUIREMENT_DRIFT' in e for e in validate_plan(p)))
 def test_short_horizon_closed(self):
  hs={i for w in self.p['windows'] for i in w['storyIds']}
  for s in self.p['stories']:
   if s['id'] in hs:
    self.assertTrue(all(d['producer'] in hs for d in s['requires'] if d['consumerStage']=='candidate' and d['condition']=='ALWAYS'))
 def test_integration_horizon_is_also_closed(self):
  hs={i for w in self.p['windows'] for i in w['storyIds']}
  for story in self.p['stories']:
   if story['id'] in hs:
    self.assertTrue(all(d['producer'] in hs for d in story['requires'] if d['consumerStage'] in ['candidate','integration'] and d['condition']=='ALWAYS'))
 def test_every_leaf_has_existing_parent_and_technical_specs(self):
  for s in self.p['stories']:
   self.assertTrue((ROOT/s['parentSpecRef']).is_file())
   self.assertTrue(all((ROOT/x).is_file() for x in s['specRefs']))
 def test_no_calendar_fabricated(self):
  p=self.changed();p['windows'][0]['endDate']='2026-09-25';self.assertTrue(any('UNJUSTIFIED_CALENDAR' in e for e in validate_plan(p)))
 def test_parallel_does_not_mean_authorized(self):
  r=parallel(self.p,'CS-PROOF.01.01','CS-BOOT.01.01');self.assertTrue(r['parallelCandidate']);self.assertFalse(r['authorization'])
 def test_sequential_writers_are_not_parallel(self):
  self.assertFalse(parallel(self.p,'CS-BOOT.01.01','CS-BOOT.01.02')['parallelCandidate'])
 def test_meta_and_case_alias_detected(self):
  self.assertTrue(path_overlap('Assets/A.prefab','assets/a.prefab.meta'))
 def test_path_escape_rejected(self):
  for p in ['../outside','/tmp/a','C:/x','Assets/../x','Assets/foo.']:
   with self.assertRaises(PlanError):safe_path(ROOT,p)
 def test_duplicate_json_key_and_nonfinite_rejected(self):
  with tempfile.TemporaryDirectory() as td:
   q=Path(td)/'x.json'
   for text in ['{"id":1,"id":2}','{"n":NaN}']:
    q.write_text(text)
    with self.assertRaises(PlanError):read_json(q)
 def test_priority_never_overrides_dependency(self):
  seq=candidate_order(self.p);pos={i:n for n,i in enumerate(seq)}
  for s in self.p['stories']:
   for d in s['requires']:
    if d['consumerStage']=='candidate' and d['condition']=='ALWAYS':self.assertLess(pos[d['producer']],pos[s['id']])
 def test_jsonld_is_parseable_and_story_count_matches(self):
  from rdflib import Graph,URIRef,RDF
  g=Graph().parse(ROOT/'ontology/work-graph.jsonld',format='json-ld');self.assertEqual(len(set(g.subjects(RDF.type,URIRef('urn:chooguard:work:Story')))),len(self.p['stories']))
 def test_schema_ontology_not_actual_execution(self):
  from rdflib import Graph,URIRef,RDF
  g=Graph().parse(ROOT/'ontology/work-graph.jsonld',format='json-ld');self.assertFalse(list(g.subjects(RDF.type,URIRef('http://www.w3.org/ns/prov#Activity'))))
 def test_ontology_turtle_and_shapes_parse(self):
  from rdflib import Graph
  self.assertGreater(len(Graph().parse(ROOT/'ontology/vocabulary.ttl',format='turtle')),0)
  self.assertGreater(len(Graph().parse(ROOT/'ontology/shapes.ttl',format='turtle')),0)

class ReceiptTests(unittest.TestCase):
 def setUp(self):
  self.p=load();self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
  shutil.copytree(ROOT/'schemas',self.root/'schemas');(self.root/'evidence').mkdir();(self.root/'evidence/raw.txt').write_text('TEST_ONLY synthetic output\n')
  self.f={'path':'evidence/raw.txt','sha256':hashlib.sha256((self.root/'evidence/raw.txt').read_bytes()).hexdigest()}
  self.state={'schemaVersion':'4.0.0','records':[],'externalInputs':[],'claims':[],'note':'UNIT TEST ONLY'}
 def tearDown(self):self.tmp.cleanup()
 def rec(self,story='CS-PROOF.01.01',phase='candidate'):
  s=next(s for s in self.p['stories'] if s['id']==story)
  return {'id':'test-'+story+'-'+phase,'storyId':story,'phase':phase,'profile':'fixture','status':'ACCEPTED','planDigest':digest(self.p),'storyDigest':digest(s),'codeRevision':'TEST_ONLY','executorId':'worker-test','reviewerId':'reviewer-test','reviewScope':'SYNTHETIC_TEST_ONLY','independentReview':True,'recordedAt':'2026-09-19T00:00:00Z','inputReceiptIds':[],
  'testResult':{'executed':1,'passed':1,'failed':0,'notRun':0},'artifacts':[{'artifactId':'OUT-'+story,**self.f}],'evidenceFiles':[self.f]}
 def test_empty_state_valid_no_execution(self):self.assertEqual(validate_state(self.p,self.state,self.root),[])
 def test_synthetic_receipt_schema_positive(self):
  self.state['records']=[self.rec()];self.assertEqual(validate_state(self.p,self.state,self.root),[])
 def test_no_records_not_ready_to_integrate(self):
  r=readiness(self.p,self.state,'CS-PROOF.01.01','integration','fixture',self.root);self.assertEqual(r['status'],'HOLD_SPECIFIC_PHASE')
 def test_not_run_is_not_accepted(self):
  r=self.rec();r['testResult']={'executed':0,'passed':0,'failed':0,'notRun':1};self.state['records']=[r];self.assertTrue(any('UNTESTED_ACCEPTANCE' in x for x in validate_state(self.p,self.state,self.root)))
 def test_missing_or_modified_file_invalidates_receipt(self):
  self.state['records']=[self.rec()];(self.root/'evidence/raw.txt').write_text('changed');self.assertTrue(any('EVIDENCE' in x for x in validate_state(self.p,self.state,self.root)))
 def test_plan_revision_invalidates_receipt(self):
  r=self.rec();r['planDigest']='0'*64;self.state['records']=[r];self.assertTrue(any('STALE_RECEIPT' in x for x in validate_state(self.p,self.state,self.root)))
 def test_self_review_not_independent(self):
  r=self.rec();r['reviewerId']=r['executorId'];self.state['records']=[r];self.assertTrue(any('FALSE_INDEPENDENT' in x for x in validate_state(self.p,self.state,self.root)))
 def test_external_satisfied_needs_evidence(self):
  self.state['externalInputs']=[{'id':'EXT-UNITY','state':'SATISFIED','evidenceFiles':[]}];self.assertTrue(any('EXTERNAL_WITHOUT_EVIDENCE' in x for x in validate_state(self.p,self.state,self.root)))
 def test_stage_receipt_cannot_skip_own_candidate(self):
  self.state['records']=[self.rec(phase='integration')];self.assertTrue(any('OWN_STAGE' in x for x in validate_state(self.p,self.state,self.root)))
 def test_ambiguous_external_status_rejected(self):
  self.state['externalInputs']=[{'id':'EXT-UNITY','state':'SATISFIED','evidenceFiles':[self.f]},{'id':'EXT-UNITY','state':'MISSING','evidenceFiles':[]}]
  self.assertTrue(any('DUPLICATE_EXTERNAL' in x for x in validate_state(self.p,self.state,self.root)))
 def test_accepted_receipt_cannot_skip_external_input(self):
  self.state['records']=[self.rec(story='CS-BOOT.01.01')];self.assertTrue(any('UNPROVEN_EXTERNAL' in x for x in validate_state(self.p,self.state,self.root)))
 def test_active_claim_has_real_remaining_effect(self):
  self.state['claims']=[{'id':'x','storyId':'CS-SHIP.03.01','phase':'candidate','status':'ACTIVE','paths':['research/discovery'],'resourceIds':[]}]
  r=readiness(self.p,self.state,'CS-PROOF.01.01','candidate','fixture',self.root);self.assertIn('ACTIVE_WRITE_CONFLICT:x',r['reasons'])

class CliTests(unittest.TestCase):
 def run_cli(self,*args):return subprocess.run([sys.executable,'-B',str(ROOT/'tools/plan.py'),*args],capture_output=True,text=True,timeout=30)
 def test_budget_overflow_never_truncates_to_complete(self):
  r=self.run_cli('brief','CS-OPS.02.02','--max-bytes','10');self.assertEqual(r.returncode,3);data=json.loads(r.stdout);self.assertFalse(data['complete']);self.assertIn('nextCommand',data);self.assertEqual(data['mode'],'LEGACY_REFERENCE_ONLY')
 def test_unknown_story_not_a_blank_success(self):self.assertEqual(self.run_cli('brief','EP02').returncode,2)
 def test_prepare_can_read_but_not_authorize(self):
  r=self.run_cli('brief','CS-BOOT.01.01','--phase','prepare','--max-bytes','64000');self.assertEqual(r.returncode,0);d=json.loads(r.stdout);self.assertTrue(d['complete']);self.assertFalse(d['readiness']['executionAuthorized']);self.assertEqual(d['mode'],'LEGACY_REFERENCE_ONLY')
 def test_order_is_not_ready_list(self):
  for args in [('order',),('next',),('parallel','CS-BOOT.01.01','CS-PACK.01.01')]:
   with self.subTest(command=args[0]):
    r=self.run_cli(*args);self.assertEqual(r.returncode,2);d=json.loads(r.stdout)
    self.assertEqual(d['error'],'LEGACY_EXECUTION_DISABLED');self.assertFalse(d['executionAuthorized'])
    self.assertEqual(d['activePlan'],'docs/CHOOGuard_Story_Plan_v5/plan.json');self.assertNotIn('recommendations',d);self.assertNotIn('items',d)
 def invoke_main(self,args,active,stubs=None):
  import contextlib,io,plan
  from unittest.mock import patch
  with contextlib.ExitStack() as stack:
   stack.enter_context(patch.object(sys,'argv',['plan.py',*args]))
   # Simulate absent/present v5 without deleting or moving user files.
   stack.enter_context(patch.object(plan,'active_plan_exists',return_value=active))
   for name,value in (stubs or {}).items():stack.enter_context(patch.object(plan,name,return_value=value))
   output=stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
   code=plan.main()
  return code,json.loads(output.getvalue())
 def test_order_without_v5_keeps_legacy_behavior(self):
  code,data=self.invoke_main(['order'],False)
  self.assertEqual(code,0);self.assertEqual(data['scope'],'PLANNED_CANDIDATE_ORDER_NOT_READY_QUEUE');self.assertFalse(data['calendarPromise']);self.assertNotIn('mode',data)
 def test_other_commands_without_v5_keep_legacy_behavior(self):
  for args,key in [(['next'],'recommendations'),(['parallel','CS-BOOT.01.01','CS-PACK.01.01'],'canParallel'),(['brief','CS-BOOT.01.01','--max-bytes','64000'],'complete')]:
   with self.subTest(command=args[0]):
    code,data=self.invoke_main(args,False);self.assertEqual(code,0);self.assertNotIn('mode',data)
    if args[0]!='parallel':self.assertIn(key,data)
    else:self.assertNotIn('error',data)
 def test_validate_reference_marker_without_rerunning_full_validator(self):
  # CLI payload test only: full legacy structural/receipt validation is out of scope.
  for active in (True,False):
   code,data=self.invoke_main(['validate'],active,{'validate_plan':[],'validate_state':[]})
   self.assertEqual(code,0);self.assertTrue(data['valid'])
   if active:self.assertEqual(data['mode'],'LEGACY_REFERENCE_ONLY')
   else:self.assertNotIn('mode',data)

if __name__=='__main__':unittest.main()
