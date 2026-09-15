import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest
import zipfile

SCRIPT = pathlib.Path(__file__).resolve().parents[1] / 'inventory.py'

def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.DEVNULL)

class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self.tmp.name) / 'repo'
        self.root.mkdir()
        git(self.root, 'init', '-q')
        git(self.root, 'config', 'user.name', 'synthetic')
        git(self.root, 'config', 'user.email', 'synthetic@example.invalid')
        files = {'Packages/a/Runtime/Core.cs': b'class Core {}\n' * 1601,
                 'Packages/a/Tests/CoreTests.cs': b'// test\n',
                 'docs/requirements.md': b'# requirements\nline without newline',
                 'Assets/Scene.unity': b'---\nscene\n',
                 '.agents/tool.py': b'print(1)\n',
                 'scripts/code.py': b'def f():\n    return 1\n',
                 'data.json': b'{}\n', 'Assets/art.png': b'\x89PNG\x00\xff',
                 'private-data/a.py': b'print("not exported")\n',
                 'scripts/empty.py': b''}
        for path, data in files.items():
            p = self.root / path; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(data)
        git(self.root, 'add', '.'); git(self.root, 'commit', '-qm', 'synthetic')
        self.sha = git(self.root, 'rev-parse', 'HEAD').decode().strip()
        self.output = pathlib.Path(self.tmp.name) / 'out'
    def tearDown(self):
        self.tmp.cleanup()
    def run_inventory(self, ref=None):
        return subprocess.run(['python3', str(SCRIPT), '--root', str(self.root), '--ref', ref or self.sha,
                               '--output', str(self.output)], capture_output=True, text=True)
    def test_exact_inventory_and_categories(self):
        r = self.run_inventory(); self.assertEqual(r.returncode, 0, r.stderr)
        data = json.loads((self.output / 'inventory.json').read_text())
        rows = {x['path']: x for x in data['files']}
        self.assertEqual(len(rows), 10)
        self.assertEqual(rows['Packages/a/Runtime/Core.cs']['lines'], 1601)
        self.assertEqual(rows['docs/requirements.md']['lines'], 2)
        self.assertEqual(rows['Assets/art.png']['category'], 'binary_or_non_utf8')
        self.assertEqual(rows['.agents/tool.py']['category'], 'vendored_tooling')
        self.assertEqual(rows['Assets/Scene.unity']['category'], 'serialized_asset')
        self.assertEqual(rows['Packages/a/Tests/CoreTests.cs']['category'], 'test_code')
        self.assertEqual(rows['scripts/empty.py']['lines'], 0)
        self.assertEqual(data['sourceCommit'], self.sha)
    def test_ranges_cover_all_source_lines_exactly_once(self):
        r = self.run_inventory(); self.assertEqual(r.returncode, 0, r.stderr)
        data = json.loads((self.output / 'inventory.json').read_text())
        tasks = json.loads((self.output / 'review-units.json').read_text())['units']
        for row in data['files']:
            if row['category'] not in ('source_code','test_code'): continue
            parts = [u for u in tasks if u['path'] == row['path']]
            actual = [n for u in parts for n in range(u['startLine'],u['endLine']+1)]
            self.assertEqual(actual, list(range(1,row['lines']+1)))
            self.assertTrue(all(u['state'] == 'UNREVIEWED' for u in parts))
    def test_export_uses_git_bytes_and_excludes_binary_vendored_and_restricted(self):
        (self.root/'scripts/code.py').write_text('DIRTY\n')
        (self.root/'scripts/untracked.py').write_text('UNTRACKED\n')
        r = self.run_inventory(); self.assertEqual(r.returncode, 0, r.stderr)
        with zipfile.ZipFile(self.output/'sources.zip') as z:
            self.assertEqual(z.read('scripts/code.py'), b'def f():\n    return 1\n')
            for path in ('scripts/untracked.py','Assets/art.png','.agents/tool.py','private-data/a.py','Assets/Scene.unity'):
                self.assertNotIn(path,z.namelist())
    def test_mutable_ref_is_rejected(self):
        r = self.run_inventory('HEAD'); self.assertNotEqual(r.returncode, 0)
        self.assertFalse(self.output.exists())
    def test_output_is_not_overwritten(self):
        self.output.mkdir(); (self.output/'sentinel').write_text('preserve')
        self.assertNotEqual(self.run_inventory().returncode,0)
        self.assertEqual((self.output/'sentinel').read_text(),'preserve')
    def test_archive_digest_and_file_digests_are_recomputable(self):
        import hashlib
        r=self.run_inventory(); self.assertEqual(r.returncode,0,r.stderr)
        data=json.loads((self.output/'inventory.json').read_text())
        self.assertEqual(hashlib.sha256((self.output/'sources.zip').read_bytes()).hexdigest(),data['archiveSha256'])
        with zipfile.ZipFile(self.output/'sources.zip') as z:
            for row in data['files']:
                if not row['exported']: continue
                blob=z.read(row['path'])
                self.assertEqual(hashlib.sha256(blob).hexdigest(),row['sha256'])
                self.assertEqual(hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest(),row['blob'])
    def test_repeat_is_deterministic(self):
        self.assertEqual(self.run_inventory().returncode,0)
        first=(self.output/'sources.zip').read_bytes()
        self.output=self.output.with_name('second')
        self.assertEqual(self.run_inventory().returncode,0)
        self.assertEqual(first,(self.output/'sources.zip').read_bytes())

if __name__ == '__main__': unittest.main()
