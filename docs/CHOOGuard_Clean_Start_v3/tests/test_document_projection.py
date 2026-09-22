import unittest,sys,json
from pathlib import Path
R=Path(__file__).resolve().parents[1];sys.path.insert(0,str(R/'tools'))
from render_specs import outputs
from wire_checks import check,load_example
class ProjectionAndAdditionalWire(unittest.TestCase):
 def test_generated_documents_match_contracts(self):
  for p,text in outputs().items():self.assertEqual((R/p).read_text(),text,p)
 def test_receipt_lookup_presence(self):
  d=load_example('ReceiptLookup');d['found']=True;self.assertTrue(check('ReceiptLookup',d))
 def test_commit_receipt_identity(self):
  d=load_example('CommitReceipt');d['runId']='different';self.assertTrue(check('CommitReceipt',d))
 def test_open_inputs_have_responsible_task(self):
  tasks={t['id'] for t in json.loads((R/'contracts/tasks.json').read_text())['tasks']}
  for x in json.loads((R/'contracts/open-inputs.json').read_text())['inputs']:
   self.assertIn(x['ownerTask'],tasks);self.assertTrue(x['nonBlockingAlternative']);self.assertIn(x['requiredAt'],['candidate','integration','qualification'])
