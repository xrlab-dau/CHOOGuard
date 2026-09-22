import unittest,sys,copy,json
from pathlib import Path
R=Path(__file__).resolve().parents[1];sys.path.insert(0,str(R/'tools'))
from wire_checks import check,load_example,sqlite_release_allowed,union_duration
class WireContracts(unittest.TestCase):
 def test_all_valid_examples(self):
  for p in (R/'examples').glob('*.json'):self.assertEqual(check(p.stem,json.loads(p.read_text())),[],p.stem)
 def test_unknown_field(self):
  d=load_example('CommandIntent');d['executeShell']='bad';self.assertTrue(check('CommandIntent',d))
 def test_duplicate_target(self):
  d=load_example('CommandIntent');d['targetIds']*=2;self.assertIn('duplicate targetIds',check('CommandIntent',d))
 def test_accepted_without_commit(self):
  d=load_example('CommandReceipt');d['commitId']=None;self.assertTrue(check('CommandReceipt',d))
 def test_bad_id(self):
  d=load_example('ReceiptKey');d['requesterId']='../escape';self.assertTrue(check('ReceiptKey',d))
 def test_receipt_identity_has_requester(self):
  d=load_example('ReceiptKey');del d['requesterId'];self.assertTrue(check('ReceiptKey',d))
 def test_stale_result_generation(self):
  d=load_example('FieldBatch');d['generation']=1;self.assertTrue(check('FieldBatch',d,load_example('SimulationJob')))
 def test_foreign_result_run(self):
  d=load_example('FieldBatch');d['runId']='other';self.assertTrue(check('FieldBatch',d,load_example('SimulationJob')))
 def test_old_result_input(self):
  d=load_example('FieldBatch');d['inputDigest']='f'*64;self.assertTrue(check('FieldBatch',d,load_example('SimulationJob')))
 def test_invalid_result_interval(self):
  d=load_example('FieldBatch');d['validToTickUs']=0;self.assertTrue(check('FieldBatch',d))
 def test_negative_simtime(self):
  d=load_example('SimulationJob');d['startTickUs']=-1;self.assertTrue(check('SimulationJob',d))
 def test_missing_worker_checkpoint(self):
  d=load_example('Checkpoint');d['workerStates']=[];self.assertTrue(check('Checkpoint',d))
 def test_duplicate_worker_checkpoint(self):
  d=load_example('Checkpoint');d['workerStates']*=2;self.assertTrue(check('Checkpoint',d))
 def test_worker_at_different_cut(self):
  d=load_example('Checkpoint');d['workerStates'][0]['cutSequence']=2;self.assertTrue(check('Checkpoint',d))
 def test_branch_cannot_overwrite_parent(self):
  d=load_example('BranchRequest');d['newRunId']=d['parentRunId'];self.assertTrue(check('BranchRequest',d))
 def test_fake_evidence_pass(self):
  d=load_example('EvidenceRecord');d['result']='PASSED_WITH_SCOPE';self.assertTrue(check('EvidenceRecord',d))
 def test_not_run_cannot_have_execution_time(self):
  d=load_example('EvidenceRecord');d['executedAt']='2026-09-19T00:00:00Z';self.assertTrue(check('EvidenceRecord',d))
 def test_observed_claim_needs_evidence(self):
  d=load_example('ScriptIR');d['steps'][0]['claimType']='OBSERVED_IN_RUN';self.assertTrue(check('ScriptIR',d))
 def test_reviewed_step_needs_review(self):
  d=load_example('ScriptIR');d['steps'][0]['claimType']='REVIEWED_INSTRUCTION';self.assertTrue(check('ScriptIR',d))
 def test_agency_view_needs_agency(self):
  d=load_example('ProjectionQuery');d['viewScope']='AGENCY_KNOWLEDGE';self.assertTrue(check('ProjectionQuery',d))
 def test_overlap_duration(self):self.assertEqual(union_duration([(0,10),(5,15)]),15)
 def test_reverse_duration_rejected(self):
  with self.assertRaises(ValueError):union_duration([(10,5)])
 def test_censored_not_completed(self):
  d=load_example('ActivityInterval');d['endMonoUs']=None;self.assertTrue(check('ActivityInterval',d))
 def test_sqlite_fixed_versions(self):
  for v in ['3.51.3','3.52.0','3.44.6','3.50.7']:self.assertTrue(sqlite_release_allowed(v),v)
 def test_sqlite_unfixed_not_claimed(self):
  for v in ['3.51.2','3.50.6','3.46.1','3.44.5','unknown']:self.assertFalse(sqlite_release_allowed(v),v)

class FurtherBoundaryReview(unittest.TestCase):
 def test_foreign_field_owner(self):
  d=load_example('FieldBatch');d['fieldOwner']='worker-foreign';self.assertTrue(check('FieldBatch',d,load_example('SimulationJob')))
 def test_correct_but_wrong_unit(self):
  d=load_example('FieldBatch');d['unit']='Pa';self.assertTrue(check('FieldBatch',d,load_example('SimulationJob')))
 def test_wrong_frame(self):
  d=load_example('FieldBatch');d['frameId']='vehicle-other';self.assertTrue(check('FieldBatch',d,load_example('SimulationJob')))
 def test_commit_with_foreign_receipt(self):
  d=load_example('CommitBatch');d['receipt']['key']['runId']='run-b';self.assertTrue(check('CommitBatch',d))
