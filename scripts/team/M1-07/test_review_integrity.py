"""Synthetic integrity contract checks; no reviewer API or OS isolation claim.

Run with the default environment: `python3 scripts/team/M1-07/test_review_integrity.py`.
The temporary root is resolved to its real path before any supplier link guard sees it;
on hosts where the system temporary directory is reached through a link (for example
macOS TMPDIR=/var/folders/... under the /var -> private/var alias) an unresolved path is
correctly refused by the M1-02 link guard, which is not the contract under test here.
Refusal reasons are asserted as fixed codes so a refusal raised for an unrelated reason
cannot satisfy a negative case.
"""
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from review_integrity import Refused, begin, finalize, inspect, verify_receipt, write_generated

AUTHOR = {'sessionId': 'synthetic-author', 'provider': 'anthropic', 'model': 'claude-opus-5'}
REVIEWER = {'sessionId': 'synthetic-reviewer', 'provider': 'openai-codex', 'model': 'gpt-5.6-luna'}
SECOND_REVIEWER = {'sessionId': 'synthetic-reviewer-2', 'provider': 'openai-codex', 'model': 'gpt-5.6-luna'}
RESULT = {'outcome': 'approved', 'actionable': [], 'notes': 'synthetic fixture only'}
FINDING = {'id': 'syn-1', 'severity': 'P2', 'title': 'synthetic finding', 'problem': 'synthetic problem',
           'fix': 'synthetic fix', 'locations': ['fixture.txt:1']}
RUNS = Path(__file__).resolve().parents[3] / 'docs/team/M1-07/runs'


class ReviewIntegrity(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='m1-07-synthetic-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
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

    def assertRefused(self, code, function, *args, **kwargs):
        with self.assertRaises(Refused) as caught:
            function(*args, **kwargs)
        self.assertEqual(str(caught.exception), 'cannot_proceed: ' + code)

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

    def test_review_output_is_not_target_input_and_binds_the_requested_target(self):
        run = self.begin()
        before = inspect(run)
        self.assertEqual(before['uncommittedFiles'], before['target']['files'])
        receipt_hash = finalize(run, 'role:independent-reviewer', RESULT)
        after = inspect(run)
        self.assertEqual(after['target']['filesDigest'], before['target']['filesDigest'])
        self.assertEqual(after['target']['files'], before['target']['files'])
        self.assertNotIn('evidence/receipt.json', after['target']['files'])
        self.assertFalse((run.root / 'input/evidence').exists())
        receipt = self.verify(run, receipt_hash)
        self.assertEqual(receipt['requestSha256'], run.request_hash)
        self.assertEqual(receipt['targetFilesDigest'], before['target']['filesDigest'])

    def test_reviewer_identity_is_recorded_from_the_call_not_preassigned(self):
        run = self.begin()
        other = self.begin(output=self.root / 'run-2', reviewer=SECOND_REVIEWER)
        self.assertEqual(json.loads((run.root / 'request.json').read_bytes())['reviewer'], REVIEWER)
        self.assertEqual(json.loads((other.root / 'request.json').read_bytes())['reviewer'], SECOND_REVIEWER)
        receipt = self.verify(other, finalize(other, 'role:independent-reviewer', RESULT))
        self.assertEqual(receipt['reviewer'], SECOND_REVIEWER)
        self.assertNotEqual(receipt['reviewer'], REVIEWER)

    def test_committed_sample_manifest_and_receipt_hashes_agree(self):
        samples = sorted(RUNS.glob('*/sample-request.json'))
        self.assertTrue(samples, 'no committed sample request record was found')
        for request_path in samples:
            with self.subTest(sample=request_path.parent.name):
                target_bytes = request_path.with_name('sample-target.txt').read_bytes()
                request = json.loads(request_path.read_bytes())
                receipt = json.loads(request_path.with_name('sample-receipt.json').read_bytes())
                files = request['target']['files']
                self.assertEqual(set(files), {'fixture.txt'})
                self.assertEqual(files['fixture.txt']['sha256'], hashlib.sha256(target_bytes).hexdigest())
                self.assertEqual(files['fixture.txt']['bytes'], len(target_bytes))
                self.assertEqual(request['uncommittedFiles'], files)
                frozen = json.dumps(files, sort_keys=True, separators=(',', ':')).encode()
                self.assertEqual(request['target']['filesDigest'], hashlib.sha256(frozen).hexdigest())
                self.assertEqual(receipt['targetFilesDigest'], request['target']['filesDigest'])
                self.assertEqual(receipt['requestSha256'], hashlib.sha256(request_path.read_bytes()).hexdigest())
                self.assertEqual(receipt['reviewer'], request['reviewer'])
                self.assertEqual(receipt['scope'], request['scope'])
                self.assertNotIn('evidence/receipt.json', files)

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
        with self.assertRaises(Refused) as caught:
            verify_receipt(run, receipt_hash, current_source=self.source, base='a' * 40, head='b' * 40)
        self.assertEqual(str(caught.exception), 'cannot_proceed: current_writer_target_changed')
        (self.source / 'fixture.txt').write_bytes(b'synthetic source\n')
        with self.assertRaises(Refused) as caught:
            verify_receipt(run, receipt_hash, current_source=self.source, base='a' * 40, head='c' * 40)
        self.assertEqual(str(caught.exception), 'cannot_proceed: current_writer_target_changed')
        self.assertEqual((run.root / 'evidence/receipt.json').read_bytes(), before)

    def test_target_change_invalidates_receipt_and_preserves_old_result(self):
        run = self.begin()
        receipt_hash = finalize(run, 'role:independent-reviewer', RESULT)
        before = (run.root / 'evidence/receipt.json').read_bytes()
        (run.root / 'input/fixture.txt').write_bytes(b'changed target')
        self.assertRefused('target_bytes_changed', self.verify, run, receipt_hash)
        self.assertEqual((run.root / 'evidence/receipt.json').read_bytes(), before)

    def test_execution_source_edit_and_extra_target_file_are_refused(self):
        run = self.begin()
        (run.root / 'execution/fixture.txt').write_bytes(b'changed execution source')
        self.assertRefused('target_bytes_changed', finalize, run, 'role:independent-reviewer', RESULT)
        (run.root / 'execution/fixture.txt').write_bytes(b'synthetic source\n')
        (run.root / 'input/extra.txt').write_bytes(b'undeclared target')
        self.assertRefused('undeclared_target_file', finalize, run, 'role:independent-reviewer', RESULT)
        self.assertFalse((run.root / 'evidence/receipt.json').exists())

    def test_writer_cannot_create_or_overwrite_final_result_through_api(self):
        run = self.begin()
        self.assertRefused('writer_cannot_finalize', finalize, run, 'role:authoring-agent', RESULT)
        self.assertFalse((run.root / 'evidence/receipt.json').exists())
        finalize(run, 'role:independent-reviewer', RESULT)
        before = (run.root / 'evidence/receipt.json').read_bytes()
        self.assertRefused('writer_cannot_finalize', finalize, run, 'role:authoring-agent', RESULT)
        self.assertRefused('receipt_already_exists', finalize, run, 'role:independent-reviewer', RESULT)
        self.assertEqual((run.root / 'evidence/receipt.json').read_bytes(), before)

    def test_receipt_bound_to_another_target_cannot_be_replayed(self):
        first = self.begin()
        first_hash = finalize(first, 'role:independent-reviewer', RESULT)
        self.assertEqual(self.verify(first, first_hash)['targetFilesDigest'],
                         inspect(first)['target']['filesDigest'])
        (self.source / 'other.txt').write_bytes(b'other synthetic source\n')
        second = self.begin(names=['fixture.txt', 'other.txt'], output=self.root / 'run-2')
        self.assertNotEqual(inspect(second)['target']['filesDigest'], inspect(first)['target']['filesDigest'])
        foreign = (first.root / 'evidence/receipt.json').read_bytes()
        (second.root / 'evidence/receipt.json').write_bytes(foreign)
        self.assertRefused('receipt_binding_changed', self.verify, second,
                           hashlib.sha256(foreign).hexdigest())

    def test_request_and_receipt_tampering_fail_trusted_digest_check(self):
        run = self.begin()
        receipt_hash = finalize(run, 'role:independent-reviewer', RESULT)
        request_path = run.root / 'request.json'
        original = request_path.read_bytes()
        changed = json.loads(original)
        changed['target']['headRef'] = 'c' * 40
        request_path.write_text(json.dumps(changed), encoding='utf-8')
        self.assertRefused('request_digest_changed', self.verify, run, receipt_hash)
        request_path.write_bytes(original)
        with (run.root / 'evidence/receipt.json').open('ab') as output:
            output.write(b' ')
        self.assertRefused('receipt_digest_changed', self.verify, run, receipt_hash)

    def test_missing_same_provider_same_session_and_unlisted_model_refused(self):
        variants = [(dict(REVIEWER, provider=''), 'identity_missing_or_invalid'),
                    (dict(REVIEWER, model=''), 'identity_missing_or_invalid'),
                    (dict(REVIEWER, provider='openai/codex'), 'identity_missing_or_invalid'),
                    (dict(REVIEWER, sessionId=7), 'identity_missing_or_invalid'),
                    ({'sessionId': 'synthetic-reviewer', 'provider': 'openai-codex'}, 'identity_missing_or_invalid'),
                    (dict(REVIEWER, provider='anthropic'), 'reviewer_not_independent'),
                    (dict(REVIEWER, sessionId=AUTHOR['sessionId']), 'reviewer_not_independent'),
                    (dict(REVIEWER, model='unlisted'), 'model_outside_scope')]
        for reviewer, code in variants:
            with self.subTest(reviewer=reviewer):
                self.assertRefused(code, self.begin, reviewer=reviewer)
        self.assertFalse((self.root / 'run').exists())

    def test_output_overlap_self_reference_and_escaping_names_refused(self):
        for output in (self.source / 'run', self.source, self.root):
            with self.subTest(output=output):
                self.assertRefused('output_exists_or_overlaps_target', self.begin, output=output)
        escaping = ('../fixture.txt', '/fixture.txt', 'C:/fixture.txt', 'a\\b', '', '.git/config')
        for name in escaping:
            with self.subTest(name=name):
                self.assertRefused('invalid_relative_name', self.begin, names=[name])
        for name in ('request.json', 'input/x', 'execution/y', 'evidence/receipt.json'):
            with self.subTest(name=name):
                self.assertRefused('protocol_output_is_not_target_input', self.begin, names=[name])
        self.assertFalse((self.root / 'run').exists())

    def test_non_canonical_spellings_cannot_smuggle_protocol_output_names(self):
        for name in ('./evidence/receipt.json', './request.json', './input/x', './fixture.txt',
                     'evidence/./receipt.json', 'evidence//receipt.json', './/fixture.txt'):
            with self.subTest(name=name):
                self.assertRefused('non_canonical_relative_name', self.begin, names=[name])
        self.assertFalse((self.root / 'run').exists())
        for name in ('input/../fixture.txt', 'nested/../../fixture.txt'):
            with self.subTest(name=name):
                self.assertRefused('invalid_relative_name', self.begin, names=[name])
        for name in ('fixture.txt.x', 'nested/request.json'):
            with self.subTest(name=name):
                self.assertRefused('invalid_or_inaccessible_fixture', self.begin, names=[name])

    def test_linked_source_and_output_roots_are_still_refused(self):
        link = self.root / 'linked-source'
        try:
            link.symlink_to(self.source, target_is_directory=True)
        except OSError:
            self.skipTest('symlink creation is unavailable on this host')
        self.assertTrue(link.is_dir())
        self.assertRefused('linked_or_network_path', self.begin, source=link)
        self.assertRefused('linked_or_network_path', self.begin, output=link / 'run')
        self.assertFalse((self.root / 'run').exists())
        self.assertFalse((link / 'run').exists())

    def test_existing_run_is_never_reused(self):
        run = self.begin()
        before = (run.root / 'request.json').read_bytes()
        self.assertRefused('output_exists_or_overlaps_target', self.begin)
        self.assertEqual((run.root / 'request.json').read_bytes(), before)

    def test_only_generated_output_can_be_written_through_execution_api(self):
        run = self.begin()
        for name, code in (('fixture.txt', 'write_outside_generated_scope'),
                           ('generated', 'write_outside_generated_scope'),
                           ('../input/fixture.txt', 'invalid_relative_name'),
                           ('../evidence/receipt.json', 'invalid_relative_name'),
                           ('generated/../../fixture.txt', 'invalid_relative_name'),
                           ('./generated/x.txt', 'non_canonical_relative_name')):
            with self.subTest(name=name):
                self.assertRefused(code, write_generated, run, name, b'forbidden')
        self.assertEqual((run.root / 'execution/fixture.txt').read_bytes(), b'synthetic source\n')
        write_generated(run, 'generated/keep.txt', b'first revision')
        self.assertRefused('generated_output_exists', write_generated, run, 'generated/keep.txt', b'second revision')
        self.assertEqual((run.root / 'execution/generated/keep.txt').read_bytes(), b'first revision')

    def test_invalid_round_revision_and_result_schema_are_refused(self):
        for round_number in (0, 4, True, '1'):
            with self.subTest(round_number=round_number):
                self.assertRefused('invalid_round_or_revision', self.begin, round_number=round_number)
        for refs in ({'base': 'a' * 39}, {'head': 'B' * 40}, {'base': 'x' * 40}):
            with self.subTest(refs=refs):
                self.assertRefused('invalid_round_or_revision', self.begin, **refs)
        run = self.begin()
        invalid = [(dict(RESULT, outcome='unknown'), 'result_schema_mismatch'),
                   (dict(RESULT, extra='undeclared'), 'result_schema_mismatch'),
                   ({'outcome': 'approved'}, 'result_schema_mismatch'),
                   (dict(RESULT, actionable=['unvalidated finding']), 'finding_schema_mismatch'),
                   (dict(RESULT, actionable=[dict(FINDING, locations=[])]), 'finding_schema_mismatch'),
                   (dict(RESULT, actionable=[dict(FINDING, fix='   ')]), 'finding_schema_mismatch'),
                   (dict(RESULT, actionable=[FINDING]), 'findings_cannot_be_approved')]
        for result, code in invalid:
            with self.subTest(result=result):
                self.assertRefused(code, finalize, run, 'role:independent-reviewer', result)
        self.assertFalse((run.root / 'evidence/receipt.json').exists())
        consistent = dict(RESULT, outcome='changes_required', actionable=[FINDING])
        receipt = self.verify(run, finalize(run, 'role:independent-reviewer', consistent))
        self.assertEqual(receipt['result'], consistent)


if __name__ == '__main__':
    unittest.main(verbosity=2)
