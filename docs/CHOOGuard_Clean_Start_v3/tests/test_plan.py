"""Planning contract tests and deliberately corrupted counterexamples, not Unity tests."""
import copy,sys,unittest,subprocess,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from check_plan import load,validate,check_files
class NewPlanTests(unittest.TestCase):
    def setUp(self):self.d=load()
    def test_valid_new_contract(self): self.assertEqual(validate(self.d),[])
    def test_link_and_packet_integrity(self): self.assertEqual(check_files(),[])
    def test_old_epic_injected_is_rejected(self):
        self.d['tasks'][0]['contract']='Requires EP00-S01';self.assertTrue(validate(self.d))
    def test_old_code_injected_is_rejected(self):
        self.d['tasks'][0]['contract']='Call AuthoritativeShift';self.assertTrue(validate(self.d))
    def test_old_graph_inheritance_rejected(self):
        self.d['baseline']['priorEpicGraphInherited']=True;self.assertTrue(validate(self.d))
    def test_fixed_old_regions_rejected(self):
        self.d['baseline']['fixedRegionIdsInherited']=True;self.assertTrue(validate(self.d))
    def test_missing_dependency_rejected(self):
        self.d['tasks'][0]['dependsOn']=[{'taskId':'CS-NOTFOUND.99','artifactId':'A-CS-NOTFOUND.99','consumerStage':'integration'}];self.assertTrue(validate(self.d))
    def test_cycle_rejected(self):
        self.d['tasks'][0]['dependsOn']=[{'taskId':'CS-BOOT.02','artifactId':'A-CS-BOOT.02','consumerStage':'integration'}];self.assertTrue(validate(self.d))
    def test_duplicate_writer_rejected(self):
        self.d['tasks'][1]['plannedFiles'].append(self.d['tasks'][0]['plannedFiles'][0]);self.assertTrue(validate(self.d))
    def test_missing_product_requirement_rejected(self):
        self.d['requirements'].pop();self.assertTrue(validate(self.d))
    def test_fake_product_pass_rejected(self):
        self.d['tests'][0]['result']='PASSED';self.assertTrue(validate(self.d))
    def test_third_user_mode_rejected(self):
        self.d['baseline']['productModeIds'].append('ANALYSIS');self.assertTrue(validate(self.d))
    def test_wrong_artifact_rejected(self):
        self.d['tasks'][1]['dependsOn'][0]['artifactId']='A-CS-OTHER.01';self.assertTrue(validate(self.d))
    def test_budget_never_silent_truncates(self):
        r=subprocess.run([sys.executable,str(ROOT/'tools/task_context.py'),'CS-BOOT.01','--max-bytes','10'],capture_output=True,text=True)
        self.assertEqual(r.returncode,3);self.assertFalse(json.loads(r.stdout)['complete']);self.assertNotIn('task',json.loads(r.stdout))
if __name__=='__main__':unittest.main()
