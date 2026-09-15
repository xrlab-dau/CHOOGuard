import importlib.util
from pathlib import Path
import sys
import unittest
from unittest import mock
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
spec=importlib.util.spec_from_file_location('rework',ROOT/'rework_worker.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)

class ReworkTests(unittest.TestCase):
    def test_feedback_preserves_source_and_frozen_oracle_binding(self):
        p={'source':'original','schema':{'const':1},'sourceCommit':r.BASE,'oracleSha256':r.ORACLE,'contract':'frozen contract'}
        result=r.add_feedback(p)
        self.assertEqual(result['source'],p['source'])
        self.assertEqual(result['schema'],p['schema'])
        self.assertEqual(result['oracleSha256'],p['oracleSha256'])
        self.assertIn('frozen contract',result['contract'])
        self.assertIn('13/87',result['contract'])
        self.assertEqual(p['contract'],'frozen contract')
    def test_stale_packet_is_not_used(self):
        with self.assertRaises(ValueError):r.add_feedback({'sourceCommit':'wrong','oracleSha256':r.ORACLE})
    def test_other_model_hash_is_not_accepted(self):
        with self.assertRaises(ValueError):r.verify_model({'sha256':'0'*64})
        r.verify_model({'sha256':r.MODEL})
if __name__=='__main__':unittest.main()
