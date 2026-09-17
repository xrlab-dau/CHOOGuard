"""Isolated Git fixtures for the committed M1-07 supplier preflight."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


SUPPLIERS = ('scripts/dev/native_manifest.py', 'scripts/team/M1-02/permission_boundary.py',
             'scripts/bootstrap/verify_toolchain.py')


class SupplierPreflight(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='m107-preflight-',
                                                dir=str(Path(tempfile.gettempdir()).resolve()))
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.repo = self.root / 'repo'
        self.repo.mkdir()
        original = Path(__file__).resolve().parents[3]
        self.contract_path = 'docs/team/M1-07/review-contract.json'
        contract = json.loads((original / self.contract_path).read_bytes())
        for item in contract['sources']:
            target = self.repo / item['path']
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((original / item['path']).read_bytes().replace(b'\r\n', b'\n'))
        self.git('init', '--quiet')
        self.git('add', '--all')
        self.git('commit', '--quiet', '-m', 'Synthetic supplier binding')
        ref = self.git('rev-parse', 'HEAD').stdout.strip()
        for item in contract['sources']:
            item['ref'] = ref
        target = self.repo / self.contract_path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(contract), encoding='utf-8')
        target = self.repo / 'scripts/team/M1-07/review_integrity.py'
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(original / target.relative_to(self.repo), target)
        self.git('add', '--all')
        self.git('commit', '--quiet', '-m', 'Synthetic consumer')

    def git(self, *args):
        return subprocess.run(['git', '-c', 'core.autocrlf=false', '-c', 'commit.gpgsign=false',
                               '-c', 'core.hooksPath=' + str(self.root / 'no-hooks'),
                               '-c', 'user.name=Synthetic fixture', '-c', 'user.email=fixture@example.invalid',
                               *args], cwd=self.repo, check=True, capture_output=True, text=True)

    def load_api(self):
        name = 'm107_fixture_' + next(tempfile._get_candidate_names())
        script = self.repo / 'scripts/team/M1-07/review_integrity.py'
        spec = importlib.util.spec_from_file_location(name, script)
        api = importlib.util.module_from_spec(spec)
        sys.modules[name] = api
        self.addCleanup(sys.modules.pop, name, None)
        spec.loader.exec_module(api)
        return api

    def invoke(self):
        script = '''import sys
from pathlib import Path
sys.path.insert(0, 'scripts/team/M1-07')
import review_integrity as api
root = Path(sys.argv[1])
source = root / 'source'
source.mkdir(exist_ok=True)
(source / 'fixture.txt').write_bytes(b'synthetic source')
try:
    run = api.begin(source, root / 'run', ['fixture.txt'], 'a'*40, 'b'*40,
                    {'sessionId':'author','provider':'anthropic','model':'claude-opus-5'},
                    {'sessionId':'reviewer','provider':'openai-codex','model':'gpt-5.6-luna'})
    api.finalize(run, 'role:independent-reviewer', {'outcome':'approved','actionable':[],'notes':'synthetic'})
except api.Refused as error:
    print(str(error))
    raise SystemExit(2)
'''
        return subprocess.run([sys.executable, '-c', script, str(self.root)], cwd=self.repo,
                              capture_output=True, text=True)

    def test_clean_supplier_closure_allows_normal_round(self):
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue((self.root / 'run/evidence/receipt.json').is_file())

    def test_repeated_clean_load_reuses_pair_but_rechecks_source_bytes(self):
        api = self.load_api()
        with patch.object(api, 'module', wraps=api.module) as loaded:
            api.load_suppliers()
            first_native, first_boundary = api.native, api.boundary
            api.load_suppliers()
        self.assertIs(api.native, first_native)
        self.assertIs(api.boundary, first_boundary)
        self.assertEqual(loaded.call_count, 2)
        target = self.repo / SUPPLIERS[0]
        target.write_bytes(target.read_bytes() + b'\n# source drift\n')
        with self.assertRaisesRegex(api.Refused, 'supplier_source_drift'):
            api.load_suppliers()

    def test_changed_or_missing_supplier_closure_refuses_before_execution(self):
        for relative in SUPPLIERS:
            target = self.repo / relative
            original = target.read_bytes()
            for missing in (False, True):
                with self.subTest(supplier=relative, missing=missing):
                    if missing:
                        target.unlink()
                    else:
                        target.write_bytes(original + b'\nfrom pathlib import Path\nPath(__file__).with_suffix(".marker").touch()\n')
                    try:
                        result = self.invoke()
                        self.assertEqual(result.returncode, 2, result.stderr)
                        self.assertIn('supplier_source_drift', result.stdout)
                        self.assertNotIn('Traceback', result.stderr)
                        self.assertFalse(target.with_suffix('.marker').exists())
                        self.assertFalse((self.root / 'run').exists())
                    finally:
                        target.write_bytes(original)

    def test_import_failure_is_refused_without_partially_publishing_caches(self):
        api = self.load_api()
        original_module = api.module

        def fail_after_native(name, relative):
            loaded = original_module(name, relative)
            if relative == SUPPLIERS[1]:
                raise RuntimeError('synthetic boundary import failure')
            return loaded

        with patch.object(api, 'module', side_effect=fail_after_native):
            with self.assertRaisesRegex(api.Refused, 'supplier_preflight_failed'):
                api.load_suppliers()
        self.assertIsNone(api._bindings)
        self.assertIsNone(api._contract_hash)
        self.assertIsNone(api.native)
        self.assertIsNone(api.boundary)
        api.load_suppliers()
        self.assertIsNotNone(api.native)
        self.assertIsNotNone(api.boundary)

    def test_invalid_committed_supplier_binding_is_refused_before_import(self):
        contract_file = self.repo / self.contract_path
        original = json.loads(contract_file.read_text(encoding='utf-8'))
        for field, value in (('ref', 'not-a-valid-commit'), ('sha256', 'not-a-valid-digest')):
            with self.subTest(field=field):
                contract = json.loads(json.dumps(original))
                next(row for row in contract['sources'] if row['path'] == SUPPLIERS[0])[field] = value
                contract_file.write_text(json.dumps(contract), encoding='utf-8')
                self.git('add', self.contract_path)
                self.git('commit', '--quiet', '-m', 'Invalid synthetic supplier binding')
                api = self.load_api()
                with patch.object(api, 'module', wraps=api.module) as loaded:
                    with self.assertRaisesRegex(api.Refused, 'supplier_binding_invalid'):
                        api.load_suppliers()
                self.assertEqual(loaded.call_count, 0)
                self.assertIsNone(api.native)
                self.assertIsNone(api.boundary)
                contract_file.write_text(json.dumps(original), encoding='utf-8')
                self.git('add', self.contract_path)
                self.git('commit', '--quiet', '-m', 'Restore synthetic supplier binding')


if __name__ == '__main__':
    unittest.main(verbosity=2)
