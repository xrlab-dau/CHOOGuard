import importlib.util
import pathlib
import tempfile
import unittest

PATH=pathlib.Path(__file__).resolve().parents[1]/'baseline_batch.py'
spec=importlib.util.spec_from_file_location('batch',PATH)
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)

class ReceiptTests(unittest.TestCase):
    def test_no_tests_and_nonzero_cannot_pass(self):
        self.assertEqual(b.test_state(0,0,0,0,0),'BLOCKED')
        self.assertEqual(b.test_state(1,10,10,0,0),'FAIL')
    def test_skips_do_not_become_passes(self):
        self.assertEqual(b.test_state(0,10,8,0,2),'PASS_WITH_SKIPS')
        self.assertEqual(b.test_state(0,10,10,0,0),'PASS')
        self.assertEqual(b.test_state(0,10,8,2,0),'FAIL')
    def test_compile_manifest_excludes_unity_dependent_tests(self):
        with tempfile.TemporaryDirectory() as d:
            root=pathlib.Path(d); package=root/'Packages/com.xrlab.chooguard.foundation'
            (package/'Runtime').mkdir(parents=True); (package/'Tests/Editor').mkdir(parents=True)
            (package/'Runtime/Core.cs').write_text('class Core {}')
            (package/'Tests/Editor/PureTests.cs').write_text('using NUnit.Framework;')
            (package/'Tests/Editor/UnityTests.cs').write_text('using UnityEngine;')
            selected,excluded=b.csharp_files(root)
            self.assertEqual([p.name for p in selected],['Core.cs','PureTests.cs'])
            self.assertEqual([p.name for p in excluded],['UnityTests.cs'])
    def test_runtime_unity_reference_blocks_pure_subset(self):
        with tempfile.TemporaryDirectory() as d:
            root=pathlib.Path(d); p=root/'Packages/com.xrlab.chooguard.foundation/Runtime'
            p.mkdir(parents=True); (p/'Core.cs').write_text('using UnityEngine;')
            with self.assertRaises(ValueError): b.csharp_files(root)

if __name__=='__main__':unittest.main()
