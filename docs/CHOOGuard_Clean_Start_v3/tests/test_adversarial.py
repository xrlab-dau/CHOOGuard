import copy,json,sys,unittest,subprocess
from pathlib import Path
R=Path(__file__).resolve().parents[1];sys.path.insert(0,str(R/'tools'))
from check_plan import load,validate,strict_json,phase_graph,cycle_errors
class AdversarialPlan(unittest.TestCase):
 def setUp(self):self.d=load()
 def rejects(self,needle):self.assertTrue(any(needle in e for e in validate(self.d)),validate(self.d))
 def test_wrong_platform(self):self.d['baseline']['platform']='WEB_BROWSER';self.rejects('platform')
 def test_wrong_ui(self):self.d['baseline']['presentation']='HTML_DOM';self.rejects('UI')
 def test_unknown_producer_stage(self):self.d['tasks'][1]['dependsOn'][0]['stage']='imaginary';self.rejects('producer stage')
 def test_unknown_condition(self):self.d['tasks'][1]['dependsOn'][0]['condition']='MAYBE';self.rejects('condition')
 def test_duplicate_requirement(self):self.d['requirements'].append(copy.deepcopy(self.d['requirements'][0]));self.rejects('requirement duplicate')
 def test_duplicate_test(self):self.d['tests'].append(copy.deepcopy(self.d['tests'][0]));self.rejects('test duplicate')
 def test_wrong_test_requirement(self):self.d['tests'][0]['requirementIds']=['REQ-091'];self.rejects('requirement link')
 def test_wrong_test_owner(self):self.d['tests'][0]['taskId']='CS-BOOT.01';self.rejects('acceptance link')
 def test_empty_algorithms(self):self.d['tasks'][0]['algorithm']=['','',''];self.rejects('algorithm')
 def test_missing_gwt(self):self.d['tasks'][0]['acceptance']=[{'status':'NOT_RUN'},{'status':'NOT_RUN'}];self.rejects('Given When Then')
 def test_windows_case_collision(self):self.d['tasks'][1]['plannedFiles'].append(self.d['tasks'][0]['plannedFiles'][0].lower());self.rejects('case-insensitive')
 def test_absolute_windows_path(self):self.d['tasks'][0]['plannedFiles'].append('C:\\private\\unsafe.cs');self.rejects('unsafe path')
 def test_missing_scene_surface_owner(self):self.d['surfaces'][7]['path']='Assets/Ghost.prefab';self.rejects('surface missing')
 def test_native_tests_not_replaceable_by_editmode(self):self.d['tasks'][next(i for i,t in enumerate(self.d['tasks']) if t['id']=='CS-PLAY.01')]['testTargets']=[{'kind':'EDIT_MODE','path':'tests/just.cs','state':'NOT_RUN'}];self.rejects('native test coverage')
 def test_missing_assembly_root(self):self.d['assemblies']=[m for m in self.d['assemblies'] if m['name']!='ChooGuard.World'];self.rejects('unowned/ambiguous')
 def test_domain_cannot_reference_engine(self):next(m for m in self.d['assemblies'] if m['name']=='ChooGuard.Domain')['engineReferences']=True;self.rejects('domain depends')
 def test_unknown_assembly(self):self.d['assemblies'][0]['references'].append('Assembly-CSharp');self.rejects('unresolved assembly')
 def test_profile_graphs_acyclic(self):
  for p in self.d['profiles']['profiles']:self.assertEqual(cycle_errors(phase_graph(self.d,p)),[],p)
 def test_real_stage_cycle_not_just_missing_field(self):
  self.d['tasks'][0]['dependsOn']=[{'taskId':'CS-BOOT.02','artifactId':'A-CS-BOOT.02','stage':'integration','consumerStage':'integration','condition':'ALWAYS'}];self.d['tasks'][1]['dependsOn'][0]['stage']='integration';self.rejects('cycle')
 def test_unknown_profile_is_error(self):
  with self.assertRaises(ValueError):phase_graph(self.d,'unconfigured')
 def test_strict_json_rejects_duplicate_keys(self):
  with self.assertRaises(ValueError):strict_json('{"run":1,"run":2}')
 def test_strict_json_rejects_nan(self):
  with self.assertRaises(ValueError):strict_json('{"q":NaN}')
 def test_fixture_no_external_worker_gate(self):
  graph=phase_graph(self.d,'fixture');self.assertNotIn('CS-SIM.01:candidate',graph['CS-LAB.01:integration'])
 def test_coupled_requires_worker_before_runtime(self):
  graph=phase_graph(self.d,'coupled');self.assertIn('CS-SIM.01:candidate',graph['CS-LAB.01:integration'])
 def test_small_budget_preserves_phase_profile(self):
  c=subprocess.run([sys.executable,str(R/'tools/task_context.py'),'CS-SIM.05','--phase','integration','--profile','coupled','--max-bytes','1'],capture_output=True,text=True)
  self.assertEqual(c.returncode,3);d=json.loads(c.stdout);self.assertIn('--phase integration --profile coupled',d['retry']);self.assertNotIn('task',d)
