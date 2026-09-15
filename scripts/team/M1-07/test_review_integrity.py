"""Synthetic integrity contract checks; no reviewer API or OS isolation claim."""
import json
import tempfile
import unittest
from pathlib import Path

from review_integrity import Refused, begin, finalize, verify_receipt, write_generated

AUTHOR = {'sessionId': 'synthetic-author', 'provider': 'anthropic', 'model': 'claude-opus-5'}
REVIEWER = {'sessionId': 'synthetic-reviewer', 'provider': 'openai-codex', 'model': 'gpt-5.6-luna'}
RESULT = {'outcome': 'approved', 'actionable': [], 'notes': 'synthetic fixture only'}


class ReviewIntegrity(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='m1-07-synthetic-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        (self.source / 'fixture.txt').write_bytes(b'synthetic source\n')

    def begin(self, **kwargs):
        args = dict(source=self.source, output=self.root / 'run', names=['fixture.txt'],
                    base='a' * 40, head='b' * 40, author=AUTHOR, reviewer=REVIEWER)
        args.update(kwargs)
        return begin(**args)

    def verify(self, run, receipt_hash):
        return verify_receipt(run, receipt_hash, current_source=self.source, base='a' * 40, head='b' * 40)

    def test_distinct_target_execution_and_evidence_preserve_hash_chain(self):
        run = self.begin()
        write_generated(run, 'generated/test.txt', b'synthetic execution output')
        receipt_hash = finalize(run, 'role:independent-reviewer', RESULT)
        receipt = self.verify(run, receipt_hash)
        self.assertEqual(receipt['requestSha256'], run.request_hash)
        self.assertEqual(receipt['reviewer'], REVIEWER)
        self.assertEqual(receipt['scope'], 'synthetic_integrity_fixture')
        self.assertEqual((run.root / 'input/fixture.txt').read_bytes(), b'synthetic source\n')
        self.assertFalse((run.root / 'input/generated').exists())
        self.assertTrue((run.root / 'evidence/receipt.json').is_file())

    def test_original_source_is_copied_without_shared_writes(self):
        run = self.begin()
        (self.source / 'fixture.txt').write_bytes(b'new writer revision')
        self.assertEqual((run.root / 'input/fixture.txt').read_bytes(), b'synthetic source\n')
        self.assertEqual((run.root / 'execution/fixture.txt').read_bytes(), b'synthetic source\n')

    def test_new_writer_revision_cannot_reuse_old_target_receipt(self):
        run = self.begin()
        receipt_hash = finalize(run, 'role:independent-reviewer', RESULT)
        verified = verify_receipt(run, receipt_hash, current_source=self.source, base='a' * 40, head='b' * 40)
        self.assertEqual(verified['requestSha256'], run.request_hash)
        before = (run.root / 'evidence/receipt.json').read_bytes()
        (self.source / 'fixture.txt').write_bytes(b'new writer revision')
        with self.assertRaises(Refused):
            verify_receipt(run, receipt_hash, current_source=self.source, base='a' * 40, head='b' * 40)
        (self.source / 'fixture.txt').write_bytes(b'synthetic source\n')
        with self.assertRaises(Refused):
            verify_receipt(run, receipt_hash, current_source=self.source, base='a' * 40, head='c' * 40)
        self.assertEqual((run.root / 'evidence/receipt.json').read_bytes(), before)

    def test_target_change_invalidates_receipt_and_preserves_old_result(self):
        run = self.begin()
        receipt_hash = finalize(run, 'role:independent-reviewer', RESULT)
        before = (run.root / 'evidence/receipt.json').read_bytes()
        (run.root / 'input/fixture.txt').write_bytes(b'changed target')
        with self.assertRaises(Refused):
            self.verify(run, receipt_hash)
        self.assertEqual((run.root / 'evidence/receipt.json').read_bytes(), before)

    def test_execution_source_edit_and_extra_target_file_are_refused(self):
        run = self.begin()
        (run.root / 'execution/fixture.txt').write_bytes(b'changed execution source')
        with self.assertRaises(Refused):
            finalize(run, 'role:independent-reviewer', RESULT)
        (run.root / 'execution/fixture.txt').write_bytes(b'synthetic source\n')
        (run.root / 'input/extra.txt').write_bytes(b'undeclared target')
        with self.assertRaises(Refused):
            finalize(run, 'role:independent-reviewer', RESULT)

    def test_writer_cannot_create_or_overwrite_final_result_through_api(self):
        run = self.begin()
        with self.assertRaises(Refused):
            finalize(run, 'role:authoring-agent', RESULT)
        self.assertFalse((run.root / 'evidence/receipt.json').exists())
        finalize(run, 'role:independent-reviewer', RESULT)
        before = (run.root / 'evidence/receipt.json').read_bytes()
        for actor in ('role:authoring-agent', 'role:independent-reviewer'):
            with self.subTest(actor=actor), self.assertRaises(Refused):
                finalize(run, actor, RESULT)
        self.assertEqual((run.root / 'evidence/receipt.json').read_bytes(), before)

    def test_request_and_receipt_tampering_fail_trusted_digest_check(self):
        run = self.begin()
        receipt_hash = finalize(run, 'role:independent-reviewer', RESULT)
        request_path = run.root / 'request.json'
        original = request_path.read_bytes()
        changed = json.loads(original)
        changed['target']['headRef'] = 'c' * 40
        request_path.write_text(json.dumps(changed), encoding='utf-8')
        with self.assertRaises(Refused):
            self.verify(run, receipt_hash)
        request_path.write_bytes(original)
        with (run.root / 'evidence/receipt.json').open('ab') as output:
            output.write(b' ')
        with self.assertRaises(Refused):
            self.verify(run, receipt_hash)

    def test_missing_same_provider_same_session_and_unlisted_model_refused(self):
        variants = [dict(REVIEWER, provider=''), dict(REVIEWER, provider='anthropic'),
                    dict(REVIEWER, sessionId=AUTHOR['sessionId']), dict(REVIEWER, model='unlisted')]
        for reviewer in variants:
            with self.subTest(reviewer=reviewer), self.assertRaises(Refused):
                self.begin(reviewer=reviewer)
        self.assertFalse((self.root / 'run').exists())

    def test_output_overlap_self_reference_and_escaping_names_refused(self):
        for output in (self.source / 'run', self.source, self.root):
            with self.subTest(output=output), self.assertRaises(Refused):
                self.begin(output=output)
        for name in ('../fixture.txt', '/fixture.txt', 'C:/fixture.txt', 'a\\b', 'request.json', 'evidence/receipt.json'):
            with self.subTest(name=name), self.assertRaises(Refused):
                self.begin(names=[name])

    def test_existing_run_is_never_reused(self):
        run = self.begin()
        before = (run.root / 'request.json').read_bytes()
        with self.assertRaises(Refused):
            self.begin()
        self.assertEqual((run.root / 'request.json').read_bytes(), before)

    def test_only_generated_output_can_be_written_through_execution_api(self):
        run = self.begin()
        for name in ('fixture.txt', '../input/fixture.txt', '../evidence/receipt.json', 'generated/../../fixture.txt'):
            with self.subTest(name=name), self.assertRaises(Refused):
                write_generated(run, name, b'forbidden')
        self.assertEqual((run.root / 'execution/fixture.txt').read_bytes(), b'synthetic source\n')

    def test_invalid_round_and_result_schema_are_refused(self):
        for round_number in (0, 4, True, '1'):
            with self.subTest(round_number=round_number), self.assertRaises(Refused):
                self.begin(round_number=round_number)
        run = self.begin()
        invalid = [dict(RESULT, outcome='unknown'), dict(RESULT, actionable=['unvalidated finding']),
                   dict(RESULT, extra='undeclared'), {'outcome': 'approved'}]
        for result in invalid:
            with self.subTest(result=result), self.assertRaises(Refused):
                finalize(run, 'role:independent-reviewer', result)


if __name__ == '__main__':
    unittest.main(verbosity=2)
