"""Canonical R-06 control migration checks, plus an upstream compatibility case.

The date-refusal assertion is adapted from test_egress_controls.py at
86bb82bf5655562e2577cf2b83553ff261d8d5a6, not a claim to run that entire suite.
"""
import json
import tempfile
import unittest
from pathlib import Path

from test_boundary_regressions import Workspace
import egress_controls as ec


class CanonicalControl(unittest.TestCase):
    def test_canonical_control_selects_v2_publication_contract(self):
        control = ec.read_json(ec.CONTROL)
        self.assertEqual(control["schemaVersion"], 2)
        self.assertEqual(control["stages"]["outputSanitization"]["rulesetVersion"], ec.RULESET)
        self.assertEqual(control["stages"]["publicApproval"]["approvalBinding"], ec.APPROVAL_BINDING)

    def test_upstream_byte_pins_are_preserved_not_replaced_with_test_fixture_hashes(self):
        control = ec.read_json(ec.CONTROL)
        inputs = control["inputs"]
        self.assertEqual(inputs["artifact:21:M1-05-record-schema:candidate"]["sha256"],
                         "451fcc0bb395e7c75d196bec43c91bac7228dff62dff9864196d1bdedd474427")
        self.assertEqual(inputs["artifact:21:M1-05-record-schema:candidate"]["pipeline"]["sha256"],
                         "8b0f94c0aef552d9787524abe558816435293d5cd487eb5592d2e397faba554b")
        self.assertEqual(inputs["artifact:16:M0-03-execution-profile:candidate"]["fileSha256"],
                         "787fbcf6fc8dc7a63e31b54835b87a32eaa2a68d2c71e1b2ab9b4e4b36eaf5fc")
        self.assertFalse(control["canonicalWriteAllowed"])


class UpstreamDateCompatibility(Workspace):
    def test_date_traversal_returns_invalid_date_even_without_a_publication_candidate(self):
        record = {"manifestSha256": ec.sha256(b'{}'), "_manifestBytes": b'{}'}
        approval = ec.approval_for()(record)
        for date in ("../../etc", "2026-02-31"):
            with self.subTest(date=date), self.assertRaises(ec.Refused) as caught:
                ec.publish(self.ctx["schema"], {"outputSanitization": "clean"}, self.ctx["topics"]["T-PUB-01"],
                           ec.clean_body(), record, approval, self.research, "safe-slug", date)
            self.assertEqual(caught.exception.code, "invalid_date")
        self.assertEqual(list(self.research.iterdir()), [])

    def test_valid_date_does_not_make_an_old_audit_only_approval_usable(self):
        record = {"manifestSha256": ec.sha256(b'{}'), "_manifestBytes": b'{}'}
        with self.assertRaisesRegex(ec.Refused, "^publication_candidate_missing$"):
            ec.publish(self.ctx["schema"], {"outputSanitization": "clean"}, self.ctx["topics"]["T-PUB-01"],
                       ec.clean_body(), record, ec.approval_for()(record), self.research, "safe-slug", "2026-09-15")


if __name__ == "__main__":
    unittest.main()
