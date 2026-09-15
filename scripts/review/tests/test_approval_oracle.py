import importlib.util
import pathlib
import unittest

PATH=pathlib.Path(__file__).resolve().parents[1]/'approval_oracle.py'
spec=importlib.util.spec_from_file_location('oracle',PATH)
o=importlib.util.module_from_spec(spec); spec.loader.exec_module(o)

class CandidateContainmentTests(unittest.TestCase):
    def test_pure_function_allowed(self):
        self.assertIsNone(o.load_candidate('def validate_approval_record(schema, record):\n    return None\n')({},{}))
    def test_import_file_network_reflection_recursion_and_mutation_rejected(self):
        for statement in ('import os','open("x")','__import__("os")','record.__class__','eval("1")','validate_approval_record(schema, record)', 'record["x"] = 1', 'while True:\n        pass','return globals()'):
            with self.subTest(statement=statement), self.assertRaises((ValueError,SyntaxError)):
                o.load_candidate('def validate_approval_record(schema, record):\n    '+statement+'\n')
    def test_top_level_side_effects_rejected(self):
        with self.assertRaises(ValueError):
            o.load_candidate('print("x")\ndef validate_approval_record(schema,record):\n    return None')
    def test_decorators_and_default_evaluation_rejected(self):
        for s in ('@print\ndef validate_approval_record(schema, record):\n    return None', 'def validate_approval_record(schema, record=print("x")):\n    return None'):
            with self.assertRaises(ValueError): o.load_candidate(s)
    def test_nested_function_cannot_replace_a_trusted_global(self):
        with self.assertRaises(ValueError):
            o.load_candidate('def validate_approval_record(schema, record):\n    def Refused(x):\n        return Refused(x)\n    return None')
    def test_arbitrary_call_rejected(self):
        with self.assertRaises(ValueError):
            o.load_candidate('def validate_approval_record(schema, record):\n    f = record["call"]\n    return f()')

if __name__=='__main__': unittest.main()
