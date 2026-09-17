"""M1-01/M1-05 synthetic integration tests; no live Editor or shared registry."""
import copy
import json
import tempfile
import unittest
from pathlib import Path
import subprocess
import sys
import shutil

from contention import BASE_A, BASE_B, REPO, SOURCES, load_suppliers, run_fixture, schema_errors


class ContentionRecords(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pipeline = load_suppliers()[1]
        cls.report, cls.bundle = run_fixture()
        cls.cases = {c['id']: c for c in cls.report['cases']}

    def test_all_synthetic_steps_match_contract(self):
        self.assertEqual(self.report['state'], 'passed', [c['id'] for c in self.report['cases'] if not c['passed']])
        self.assertTrue(all(c['passed'] for c in self.cases.values()))

    def test_overlapping_resources_preserve_conflicting_holder_scope_and_base(self):
        for kind in ('workspace-file', 'unity-project', 'unity-editor', 'generated-output', 'manifest'):
            with self.subTest(kind=kind):
                case = self.cases[kind + '-conflict']
                self.assertFalse(case['accepted'])
                self.assertEqual(case['reason'], 'overlap_with_active')
                self.assertEqual(case['details']['conflictingHolder'], 'synthetic-a')
                self.assertEqual(case['details']['conflictingBaseRef'], BASE_A)
                self.assertEqual(case['details']['conflictingScope'], case['scope'])

    def test_handoff_and_cancel_do_not_drop_old_holder_or_new_base(self):
        self.assertEqual(self.cases['handoff']['holder'], 'synthetic-a')
        self.assertEqual(self.cases['handoff']['baseRef'], BASE_A)
        self.assertEqual(self.cases['handoff-acquire']['holder'], 'synthetic-b')
        self.assertEqual(self.cases['handoff-acquire']['baseRef'], BASE_B)
        self.assertEqual(self.cases['handoff-acquire']['fence'], 2)
        self.assertFalse(self.cases['old-holder-write']['accepted'])
        self.assertTrue(self.cases['cancel']['accepted'])

    def test_recovery_requires_known_holder_matching_content_and_explicit_reacquire(self):
        self.assertEqual(self.cases['unknown-holder']['reason'], 'lease_fields_unrecordable')
        self.assertEqual(self.cases['expired-conflict']['reason'], 'overlap_with_expired')
        self.assertEqual(self.cases['dirty-recovery']['reason'], 'content_changed_since_last_authorized_write')
        self.assertFalse(self.cases['dirty-recovery']['accepted'])
        self.assertEqual(self.cases['recovery-retry']['baseRef'], BASE_B)
        self.assertEqual(self.cases['recovery-retry']['fence'], 2)
        self.assertEqual(self.cases['crash-retry']['baseRef'], BASE_B)
        self.assertEqual(self.cases['crash-retry']['fence'], 2)
        self.assertEqual(self.cases['dirty-still-blocks']['reason'], 'overlap_with_failed')

    def test_failure_retry_cancel_order_survives_public_projection_and_all_schemas(self):
        events = self.report['publicCandidate']['events']
        expected = [(c['eventKind'], c['eventOutcome']) for c in self.report['cases']]
        self.assertEqual([(e['kind'], e['outcome']) for e in events], expected)
        self.assertTrue({'failure', 'retry', 'cancel'} <= {e['kind'] for e in events})
        self.assertEqual(self.report['schemaErrors'], [])
        self.assertEqual(self.report['publicManifest']['approval'], 'pending')
        self.assertTrue(self.report['privateInputUnchanged'])
        self.assertTrue(self.report['journalMatchesCalls'])

    def test_changed_public_event_bytes_fail_manifest_verification(self):
        with tempfile.TemporaryDirectory(prefix='m1-06-tamper-',
                                         dir=str(Path(tempfile.gettempdir()).resolve())) as temporary:
            root = Path(temporary)
            for name, data in self.bundle.items():
                (root / name).write_bytes(data)
            self.assertEqual(self.pipeline.verify(root), self.report['publicManifest'])
            changed = copy.deepcopy(self.report['publicCandidate'])
            changed['events'][0]['outcome'] = 'unknown'
            (root / 'events.json').write_bytes(self.pipeline.encoded(changed))
            with self.assertRaisesRegex(self.pipeline.RecordError, 'digest mismatch'):
                self.pipeline.verify(root)

    def test_invalid_event_kind_is_rejected_by_central_schema(self):
        bad = copy.deepcopy(self.report['publicCandidate'])
        bad['events'][0]['kind'] = 'undeclared_lease_kind'
        self.assertTrue(schema_errors(bad))


class ContentionCLI(unittest.TestCase):
    def setUp(self):
        # Commit the working source in a disposable repo: tests also work before
        # the implementation is committed in the developer's checkout.
        temporary = tempfile.TemporaryDirectory(prefix='m1-06-cli-',
                                                dir=str(Path(tempfile.gettempdir()).resolve()))
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.repo = self.root / 'repo'
        self.repo.mkdir()
        for relative in SOURCES:
            target = self.repo / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(REPO / relative, target)
        self.git('init', '--quiet')
        self.commit()
        self.output = self.root / 'run'

    def git(self, *args):
        return subprocess.run(['git', '-c', 'core.autocrlf=false', '-c', 'commit.gpgsign=false',
                               '-c', 'core.hooksPath=' + str(self.root / 'no-hooks'),
                               '-c', 'user.name=Synthetic fixture', '-c', 'user.email=fixture@example.invalid',
                               *args], cwd=self.repo, check=True, capture_output=True, text=True)

    def commit(self):
        self.git('add', '--all')
        self.git('commit', '--quiet', '-m', 'Synthetic CLI fixture')

    def cli(self, *args):
        return subprocess.run([sys.executable, str(self.repo / SOURCES[4]), '--output', str(self.output), *args],
                              cwd=self.repo, capture_output=True, text=True)

    def assert_cannot_proceed(self, result):
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn('cannot_proceed', result.stderr)
        self.assertNotIn('Traceback', result.stderr)
        self.assertFalse(self.output.exists())

    def test_normal_cli_and_existing_output_refusal(self):
        result = self.cli()
        self.assertEqual(result.returncode, 0, result.stderr)
        receipt = json.loads((self.output / 'receipt.json').read_bytes())
        self.assertEqual(receipt['state'], 'passed')
        self.assertEqual(len(receipt['cases']), 33)
        self.assertEqual(receipt['skippedSteps'], [])
        before = {str(p.relative_to(self.output)): p.read_bytes() for p in self.output.rglob('*') if p.is_file()}
        self.assertEqual(self.cli().returncode, 2)
        after = {str(p.relative_to(self.output)): p.read_bytes() for p in self.output.rglob('*') if p.is_file()}
        self.assertEqual(before, after)

    def test_initial_and_reacquire_refusal_preserve_evidence_and_skip_dependents(self):
        for injection, failed, skipped in (
            ('initial', 'handoff-start', {'handoff', 'handoff-acquire', 'old-holder-write', 'cancel'}),
            ('reacquire', 'handoff-acquire', {'cancel'}),
        ):
            with self.subTest(injection=injection):
                self.output = self.root / injection
                result = self.cli('--inject-failure', injection)
                self.assertEqual(result.returncode, 1, result.stderr)
                receipt = json.loads((self.output / 'receipt.json').read_bytes())
                self.assertEqual(receipt['state'], 'failed')
                cases = {c['id']: c for c in receipt['cases']}
                self.assertEqual([c['id'] for c in cases.values() if not c['passed']], [failed])
                self.assertEqual(cases[failed]['reason'], 'lease_fields_unrecordable')
                self.assertEqual({s['id'] for s in receipt['skippedSteps']}, skipped)
                self.assertFalse(skipped & cases.keys())
                self.assertTrue(receipt['journalMatchesCalls'])
                self.assertEqual(sum(map(len, receipt['journals'].values())), len(cases))
                self.assertEqual(len(cases) + len(skipped), 33)
                self.assertTrue(receipt['eventOrderPreserved'])
                self.assertTrue(receipt['privateInputUnchanged'])

    def test_changed_suppliers_are_not_executed(self):
        for relative in (SOURCES[1], SOURCES[3]):
            with self.subTest(relative=relative):
                target = self.repo / relative
                original = target.read_bytes()
                target.write_bytes(original + b'\nfrom pathlib import Path\nPath(__file__).with_suffix(".marker").touch()\n')
                try:
                    self.assert_cannot_proceed(self.cli())
                    self.assertFalse(target.with_suffix('.marker').exists())
                finally:
                    target.write_bytes(original)

    def test_missing_suppliers_are_cannot_proceed(self):
        for relative in (SOURCES[1], SOURCES[3]):
            with self.subTest(relative=relative):
                target = self.repo / relative
                original = target.read_bytes()
                target.unlink()
                try:
                    self.assert_cannot_proceed(self.cli())
                finally:
                    target.write_bytes(original)

    def test_committed_supplier_syntax_error_is_cannot_proceed(self):
        target = self.repo / SOURCES[3]
        target.write_bytes(target.read_bytes() + b'\ninvalid syntax !!\n')
        self.commit()
        self.assert_cannot_proceed(self.cli())

    def test_unexpected_exception_retains_prior_safe_journal(self):
        target = self.repo / SOURCES[1]
        target.write_bytes(target.read_bytes() + b'\ndef fail(*args, **kwargs):\n    raise RuntimeError("private sentinel")\nLeaseRegistry.handoff = fail\n')
        self.commit()
        result = self.cli()
        self.assertEqual(result.returncode, 1, result.stderr)
        raw = (self.output / 'receipt.json').read_text(encoding='utf-8')
        receipt = json.loads(raw)
        self.assertEqual(receipt['state'], 'failed')
        self.assertEqual(receipt['failures'], [{'id': 'handoff', 'type': 'RuntimeError'}])
        self.assertTrue(receipt['journals']['handoff'])
        self.assertTrue(receipt['journalMatchesCalls'])
        self.assertNotIn('private sentinel', raw + result.stderr)


if __name__ == '__main__':
    unittest.main(verbosity=2)
