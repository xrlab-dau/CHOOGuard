import importlib.util
from pathlib import Path
import sys
import unittest
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
spec=importlib.util.spec_from_file_location('escalate',ROOT/'escalate_worker.py')
e=importlib.util.module_from_spec(spec);spec.loader.exec_module(e)

class EscalationTests(unittest.TestCase):
    def test_escalation_preserves_frozen_inputs(self):
        p={'sourceCommit':e.BASE,'source':'original','schema':{'const':1},'oracleSha256':e.ORACLE,'contract':'original contract'}
        new=e.escalation_packet(p)
        self.assertEqual(new['source'],p['source']);self.assertEqual(new['schema'],p['schema'])
        self.assertEqual(new['oracleSha256'],p['oracleSha256'])
        self.assertIn('85/87',new['contract']);self.assertEqual(p['contract'],'original contract')
    def test_another_oracle_is_never_used(self):
        with self.assertRaises(ValueError):e.escalation_packet({'sourceCommit':e.BASE,'oracleSha256':'0'*64})
    def test_model_transition_is_explicit(self):
        self.assertEqual(e.MODEL_REPO,'TheStageAI/Qwen3.5-9B-GGUF')
        self.assertNotEqual(e.MODEL_REPO,e.PREVIOUS_MODEL_REPO)

if __name__=='__main__':unittest.main()
