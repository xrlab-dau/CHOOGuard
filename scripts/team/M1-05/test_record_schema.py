"""Approval evidence regression tests; requires the jsonschema test dependency."""
import json
from pathlib import Path
import unittest

from jsonschema import Draft202012Validator


class ApprovalSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = Path(__file__).resolve().parents[3] / "docs/team/M1-05/record-schema.json"
        schema = json.loads(path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(schema)
        cls.validator = Draft202012Validator(schema)

    def valid(self, decision, reference):
        return self.validator.is_valid({"schemaVersion": 1, "state": "approval_record",
                                        "manifestSha256": "a" * 64, "decision": decision,
                                        "decisionRef": reference})

    def test_terminal_decisions_require_nonblank_reference(self):
        for decision in ("approved", "rejected"):
            for reference in (None, "", " ", "\t\n"):
                with self.subTest(decision=decision, reference=reference):
                    self.assertFalse(self.valid(decision, reference))
            self.assertTrue(self.valid(decision, "synthetic-review-record-001"))

    def test_nonterminal_decisions_allow_null_or_nonblank_reference(self):
        for decision in ("unknown", "pending"):
            for reference in (None, "synthetic-review-record-001"):
                with self.subTest(decision=decision, reference=reference):
                    self.assertTrue(self.valid(decision, reference))
            for reference in ("", " ", "\t\n"):
                self.assertFalse(self.valid(decision, reference))


if __name__ == "__main__":
    unittest.main()
