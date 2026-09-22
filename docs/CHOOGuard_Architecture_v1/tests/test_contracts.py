import copy,importlib.util,json,sqlite3,sys,unittest
from pathlib import Path
import jsonschema
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from validate_contracts import validate_semantics, validate_graph, input_match

def load(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
def ex(n):return load('examples/'+n+'.json')
class DocumentTests(unittest.TestCase):
 def test_01_modules_acyclic(self):validate_graph(load('contracts/architecture.json'))
 def test_02_cycle_rejected(self):
  x=load('contracts/architecture.json'); x['components'][8]['compileDependsOn']=['UI']
  with self.assertRaises(ValueError):validate_graph(x)
 def test_03_unknown_module_rejected(self):
  x=load('contracts/architecture.json');x['components'][0]['compileDependsOn'].append('GHOST')
  with self.assertRaises(ValueError):validate_graph(x)
 def test_04_ninety_one_requirements_preserved(self):
  a=load('basis/requirements.json')['requirements'];b=load('contracts/traceability.json')['requirements']
  self.assertEqual(len(a),91);self.assertEqual({r['id'] for r in a},{r.get('requirementId',r.get('id')) for r in b})
 def test_05_ep_packets_preserve_ids(self):
  for ep in load('basis/epics.json')['epics']:
   p=load('agent-packets/'+ep['epicId']+'.json');self.assertEqual(p['requirementIds'],ep['requirementIds']);self.assertEqual(p['acceptanceTestIds'],ep['testIds'])
   for path in p['read']:self.assertTrue((ROOT/path).is_file(),path)
 def test_06_only_native_two_modes(self):
  a=load('contracts/architecture.json');self.assertEqual(a['runtime'],'UNITY_NATIVE_PC');self.assertEqual(a['ui'],'UGUI_TMP');self.assertEqual(a['userModes'],['TUTORIAL','RANDOM_OPERATIONS_LAB'])
 def test_07_ten_schemas_valid(self):
  ss=list((ROOT/'schemas').glob('*.json'));self.assertEqual(len(ss),10)
  for p in ss:jsonschema.Draft202012Validator.check_schema(json.loads(p.read_text()))
 def test_08_examples_validate(self):
  for p in (ROOT/'examples').glob('*.json'):
   d=json.loads(p.read_text());jsonschema.validate(d,load('schemas/'+p.stem+'.schema.json'));validate_semantics(p.stem,d)
 def test_09_examples_are_not_real(self):
  for p in (ROOT/'examples').glob('*.json'):self.assertEqual(json.loads(p.read_text())['dataClass'],'SYNTHETIC_FIXTURE')
 def test_10_single_quantity_template_owner(self):
  q=load('contracts/quantity-owners.json');fields=q['fields'];self.assertEqual(len(fields),len({a['pattern'] for a in fields}));self.assertFalse(q['rendererMayWriteDomainState'])
 def test_11_product_tests_not_run(self):
  for t in load('contracts/runtime-test-plan.json')['tests']:self.assertEqual(t['status'],'NOT_RUN')
 def test_12_native_sources_baseline(self):
  a=load('contracts/repository-baseline.json');self.assertIn('aae4867c71c96f34f8fe8a252cddd9aa0412394f',json.dumps(a))
 def test_13_diagram_endpoints(self):
  vs=load('contracts/diagrams.json')['views'];self.assertEqual(len(vs),10)
  for v in vs:
   ids={n['id'] for n in v['nodes']};self.assertEqual(len(ids),len(v['nodes']))
   for e in v['edges']:self.assertIn(e['source'],ids);self.assertIn(e['target'],ids)
   for ext in ['dot','svg','png','mmd']:self.assertTrue((ROOT/'diagrams'/(v['id']+'.'+ext)).stat().st_size>50)
 def test_14_source_ids_unique(self):
  x=load('contracts/sources.json')['sources'];self.assertEqual(len(x),len({s['id'] for s in x}))
 def test_15_exact_checkpoint_missing_worker(self):
  d=ex('checkpoint');d['workers']=[]
  with self.assertRaises(ValueError):validate_semantics('checkpoint',d)
 def test_16_checkpoint_wrong_time(self):
  d=ex('checkpoint');d['workers'][0]['atTicks']='999999'
  with self.assertRaises(ValueError):validate_semantics('checkpoint',d)
 def test_17_checkpoint_wrong_input(self):
  d=ex('checkpoint');d['workers'][0]['inputHash']='f'*64
  with self.assertRaises(ValueError):validate_semantics('checkpoint',d)
 def test_18_checkpoint_duplicate_worker(self):
  d=ex('checkpoint');d['workers'].append(copy.deepcopy(d['workers'][0]))
  with self.assertRaises(ValueError):validate_semantics('checkpoint',d)
 def test_19_complete_inexact_rejected(self):
  d=ex('checkpoint');d['workers'][0]['restartPolicy']='NONE'
  with self.assertRaises(ValueError):validate_semantics('checkpoint',d)
 def test_20_ticks_overflow(self):
  d=ex('checkpoint');d['simulationTicks']='9999999999999999999'
  with self.assertRaises(ValueError):validate_semantics('checkpoint',d)
 def test_21_fake_pass_rejected(self):
  d=ex('qualification');d['status']='PASSED_WITH_SCOPE'
  with self.assertRaises(jsonschema.ValidationError):jsonschema.validate(d,load('schemas/qualification.schema.json'))
 def test_22_ai_approval_rejected(self):
  d=ex('script-ir');d['reviewState']='APPROVED'
  with self.assertRaises(jsonschema.ValidationError):jsonschema.validate(d,load('schemas/script-ir.schema.json'))
 def test_23_exact_worker_without_restore(self):
  d=ex('worker-capability');d['supports']['restore']=False
  with self.assertRaises(jsonschema.ValidationError):jsonschema.validate(d,load('schemas/worker-capability.schema.json'))
 def test_24_stale_result_input(self):
  d=ex('field-batch');expected={k:d[k] for k in ['runId','jobId','workerId','workerEpoch','inputHash','boundaryRevision']};expected['inputHash']='a'*64
  self.assertFalse(input_match(d,expected))
 def test_25_stale_result_epoch(self):
  d=ex('field-batch');expected={k:d[k] for k in ['runId','jobId','workerId','workerEpoch','inputHash','boundaryRevision']};expected['workerEpoch']+=1
  self.assertFalse(input_match(d,expected))
 def test_26_current_result_matches(self):
  d=ex('field-batch');expected={k:d[k] for k in ['runId','jobId','workerId','workerEpoch','inputHash','boundaryRevision']};self.assertTrue(input_match(d,expected))
 def test_27_field_time_backwards(self):
  d=ex('field-batch');d['time']['fromTicks']='9000000';d['time']['toTicks']='1'
  with self.assertRaises(ValueError):validate_semantics('field-batch',d)
 def test_28_no_auth_in_approved_text(self):
  d=ex('script-ir');self.assertEqual(d['reviewState'],'DRAFT')
  # A free-text word never creates a qualification record. Schema exposes no authority grant field.
  self.assertNotIn('authorityRef',d)
 def test_29_acceptance_needs_positive_sequence(self):
  d=ex('command-receipt');d['committedSeq']=0
  with self.assertRaises(jsonschema.ValidationError):jsonschema.validate(d,load('schemas/command-receipt.schema.json'))
class SQLFixtureTests(unittest.TestCase):
 def setUp(self):
  self.db=sqlite3.connect(':memory:');self.db.executescript((ROOT/'database/schema.sql').read_text());self.db.execute("INSERT INTO project VALUES('p',1)");self.db.execute("INSERT INTO run(run_id,project_id,input_hash,mode) VALUES('r','p',?,'TUTORIAL')",('a'*64,));self.db.commit()
 def tearDown(self):self.db.close()
 def test_30_ddl_loads(self):self.assertEqual(self.db.execute("SELECT count(*) FROM sqlite_master WHERE type='table'").fetchone()[0],15)
 def test_31_exclusive_hold(self):
  self.db.execute("INSERT INTO resource_hold VALUES('r','person-1','task-1')")
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("INSERT INTO resource_hold VALUES('r','person-1','task-2')")
 def test_32_atomic_reservation_rollback(self):
  self.db.execute("INSERT INTO resource_hold VALUES('r','busy','old')");self.db.commit()
  try:
   with self.db:
    self.db.execute("INSERT INTO ledger VALUES('r',1,'evt','0','RESERVED',?,?)",('a'*64,'b'*64))
    self.db.execute("INSERT INTO receipt VALUES('r','author','intent','fp','ACCEPTED',1,'{}')")
    self.db.execute("INSERT INTO outbox VALUES('o','r',1,'j','h','PENDING')")
    self.db.execute("INSERT INTO resource_hold VALUES('r','free','new')")
    self.db.execute("INSERT INTO resource_hold VALUES('r','busy','new')")
  except sqlite3.IntegrityError:pass
  for t in ['ledger','receipt','outbox']:self.assertEqual(self.db.execute('SELECT count(*) FROM '+t).fetchone()[0],0)
  self.assertEqual(self.db.execute('SELECT resource_id FROM resource_hold').fetchall(),[('busy',)])
 def test_33_receipt_unique(self):
  self.db.execute("INSERT INTO receipt VALUES('r','author','intent','fp','REJECTED',NULL,'{}')")
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("INSERT INTO receipt VALUES('r','author','intent','fp','REJECTED',NULL,'{}')")
 def add_event(self):self.db.execute("INSERT INTO ledger VALUES('r',1,'evt','0','TEST',?,?)",('a'*64,'b'*64))
 def test_34_ledger_cannot_update(self):
  self.add_event()
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("UPDATE ledger SET kind='EDITED'")
 def test_35_ledger_cannot_delete(self):
  self.add_event()
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("DELETE FROM ledger")
 def test_36_accept_without_seq_rejected(self):
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("INSERT INTO receipt VALUES('r','a','i','fp','ACCEPTED',NULL,'{}')")
 def test_37_zero_seq_rejected(self):
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("INSERT INTO receipt VALUES('r','a','i','fp','ACCEPTED',0,'{}')")
 def test_38_foreign_run_rejected(self):
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("INSERT INTO resource_hold VALUES('missing','person','task')")
 def test_39_mode_restricted(self):
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("INSERT INTO run(run_id,project_id,input_hash,mode) VALUES('other','p','a','WEB_MODE')")
if __name__=='__main__':unittest.main()
