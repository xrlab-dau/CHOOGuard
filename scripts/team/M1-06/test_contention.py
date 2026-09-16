"""M1-01/M1-05 synthetic integration tests; no live Editor or shared registry."""
import copy
import json
import tempfile
import unittest
from pathlib import Path
import subprocess
import sys

from contention import BASE_A, BASE_B, load_suppliers, run_fixture, schema_errors


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
        with tempfile.TemporaryDirectory(prefix='m1-06-tamper-') as temporary:
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

    def test_cli_precondition_failure_preserves_failure_receipt_and_skips_handoff(self):
        with tempfile.TemporaryDirectory(prefix='m1-06-cli-failure-') as temporary:
            output = Path(temporary) / 'run'
            completed = subprocess.run([sys.executable, 'scripts/team/M1-06/contention.py', '--output', str(output),
                                        '--inject-precondition-failure'], capture_output=True, text=True)
            self.assertEqual(completed.returncode, 1, completed.stderr)
            receipt = json.loads((output / 'receipt.json').read_bytes())
            self.assertEqual(receipt['state'], 'failed')
            self.assertIn('handoff-dependent-skipped', {c['id'] for c in receipt['cases']})
            self.assertEqual(receipt['cases'][10]['reason'], 'lease_fields_unrecordable')
            self.assertFalse(any(c['id'] == 'handoff' for c in receipt['cases']))


if __name__ == '__main__':
    unittest.main(verbosity=2)
