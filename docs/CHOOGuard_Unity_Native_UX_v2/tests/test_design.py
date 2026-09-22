"""Static handoff checks. These do not compile C# or exercise Unity."""
import json
import unittest
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
class NativeDesignChecks(unittest.TestCase):
    def setUp(self):
        self.contract = json.loads((ROOT/'contracts/native-ux.json').read_text())
    def test_target(self):
        self.assertEqual(self.contract['runtime'], 'UNITY_PC_NATIVE')
        self.assertFalse(self.contract['webView'])
        self.assertEqual(self.contract['uiStack'], ['uGUI', 'TextMeshPro'])
    def test_screen_ids_preserved(self):
        self.assertEqual({s['id'] for s in self.contract['screens']}, {f'S{i:02}' for i in range(1,13)})
    def test_two_modes(self):
        self.assertEqual(self.contract['modes'], ['TUTORIAL', 'RANDOM_OPERATIONS_LAB'])
    def test_no_runtime_pass(self):
        self.assertEqual(self.contract['unityCompilation'], 'NOT_RUN')
        self.assertEqual(self.contract['unityRendering'], 'NOT_RUN')
        self.assertEqual(self.contract['productIntegration'], 'NOT_RUN')
    def test_input_and_binding_spec(self):
        self.assertIn('UI', self.contract['actionMaps'])
        self.assertIn('Operations', self.contract['actionMaps'])
        self.assertIn('Camera', self.contract['actionMaps'])
        self.assertEqual(self.contract['inputPriority'][0], 'MODAL')
    def test_native_sources(self):
        source = ROOT/'unity/Assets/CHOOGuardUXNativeV2'
        editor = (source/'Editor/NativeUXSceneBuilder.cs').read_text()
        router = (source/'Runtime/NativePreviewRouter.cs').read_text()
        for s in self.contract['screens']:
            self.assertIn('"'+s['id']+'"', editor)
        self.assertIn('CanvasScaler.ScaleMode.ScaleWithScreenSize', editor)
        self.assertIn('RenderMode.ScreenSpaceOverlay', editor)
        self.assertIn('PrefabUtility.SaveAsPrefabAsset', editor)
        self.assertIn('EditorSceneManager.SaveScene', editor)
        self.assertNotIn('Time.timeScale', router)
        self.assertNotIn('Application.OpenURL', router)
    def test_runtime_tests_not_fabricated(self):
        cases = json.loads((ROOT/'contracts/unity-acceptance.json').read_text())
        self.assertGreaterEqual(len(cases), 12)
        self.assertTrue(all(c['status']=='NOT_RUN' for c in cases))
    def test_no_web_or_fonts_in_package(self):
        forbidden={'.html','.css','.js','.tsx','.ttf','.otf','.woff','.woff2'}
        self.assertFalse([p.name for p in ROOT.rglob('*') if p.is_file() and p.suffix in forbidden])
if __name__=='__main__':
    unittest.main(verbosity=2)
