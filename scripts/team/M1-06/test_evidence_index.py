"""Check immutable M1-06 evidence at HEAD; requires complete Git history."""
import hashlib
import json
from pathlib import Path
import subprocess
import unittest

REPO = Path(__file__).resolve().parents[3]
INDEX = 'docs/evidence/M1-06/contention-receipt-candidate-index.json'
SOURCE_PATHS = {
    'docs/team/M1-01/session-contract.json',
    'scripts/team/M1-01/writer_lease.py',
    'docs/team/M1-05/record-schema.json',
    'scripts/team/M1-05/record_pipeline.py',
    'scripts/team/M1-06/contention.py',
    'scripts/team/M1-06/test_contention.py',
}


def blob(ref, path):
    return subprocess.check_output(['git', 'show', ref + ':' + path], cwd=REPO)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


class CommittedEvidenceIndex(unittest.TestCase):
    def test_all_committed_runs_resolve_to_exact_outputs_and_sources(self):
        revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=REPO, text=True).strip()
        index = json.loads(blob(revision, INDEX))
        self.assertTrue(index['runs'])
        self.assertEqual(len({r['id'] for r in index['runs']}), len(index['runs']))
        for run in index['runs']:
            with self.subTest(run=run['id']):
                prefix = 'docs/evidence/M1-06/runs/' + run['id'] + '/'
                self.assertEqual(set(run['files']), {prefix + name for name in (
                    'receipt.json', 'public/events.json', 'public/manifest.json')})
                for path, expected in run['files'].items():
                    data = blob(revision, path)  # Missing committed file fails here.
                    self.assertEqual(len(data), expected['bytes'], path)
                    self.assertEqual(sha256(data), expected['sha256'], path)
                receipt = json.loads(blob(revision, prefix + 'receipt.json'))
                self.assertEqual(receipt['sourceRevision'], run['sourceRevision'])
                self.assertEqual(receipt['state'], run['state'])
                self.assertEqual(len(receipt['cases']), run['steps'])
                self.assertEqual(set(receipt['sourceFiles']), SOURCE_PATHS)
                # An explicitly recorded equivalent commit verifies historical
                # bytes after a rebase; it does not replace the execution ref.
                source_ref = run.get('sourceVerificationRevision', run['sourceRevision'])
                self.assertRegex(source_ref, r'^[0-9a-f]{40}$')
                reachable = subprocess.run(['git', 'merge-base', '--is-ancestor', source_ref, revision], cwd=REPO)
                self.assertEqual(reachable.returncode, 0, source_ref)
                for path, expected in receipt['sourceFiles'].items():
                    data = blob(source_ref, path)
                    self.assertEqual(sha256(data), expected['gitBlobSha256'], path)
                    # Recorded checkout bytes may have Git's LF/CRLF conversion.
                    lf = data.replace(b'\r\n', b'\n')
                    self.assertIn(expected['sha256'], {sha256(data), sha256(lf), sha256(lf.replace(b'\n', b'\r\n'))}, path)


if __name__ == '__main__':
    unittest.main(verbosity=2)
