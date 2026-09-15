"""Keep the dependency-free approval guard aligned with the canonical JSON Schema."""
import itertools
import json
from pathlib import Path
import unittest

from jsonschema import Draft202012Validator
import egress_controls as ec


class ApprovalConformanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schema = json.loads((Path(__file__).resolve().parents[3] /
            'docs/team/M1-05/record-schema.json').read_text(encoding='utf-8'))
        cls.validator = Draft202012Validator(cls.schema['$defs']['approval'])

    def check_record(self, record):
        try:
            ec.validate_approval_record(self.schema, record)
            actual = True
        except ec.Refused:
            actual = False
        self.assertEqual(actual, self.validator.is_valid(record), repr(record))

    def test_json_number_one_is_not_confused_with_boolean_true(self):
        for version in (1, 1.0, True, False, 0, 2, '1', None):
            self.check_record({'schemaVersion': version, 'state': 'approval_record',
                'manifestSha256': 'a'*64, 'decision': 'approved', 'decisionRef': 'synthetic-review'})

    def test_canonical_schema_and_runtime_agree_on_type_and_reference_matrix(self):
        for version, decision, reference in itertools.product(
                (1, 1.0, True, False, 0, 2, '1', None),
                ('approved', 'rejected', 'pending', 'unknown', 'other'),
                (None, '', ' ', '\t\n', 'synthetic-review', 1, [], {})):
            self.check_record({'schemaVersion': version, 'state': 'approval_record',
                'manifestSha256': 'a'*64, 'decision': decision, 'decisionRef': reference})


if __name__ == '__main__':
    unittest.main()
