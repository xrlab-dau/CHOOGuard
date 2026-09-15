"""R-06 binding/migration regressions; fixture bytes are synthetic, never accepted policy."""
import copy
import contextlib
import io
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from test_boundary_regressions import Workspace
from test_security_regressions import PROFILE, APPROVAL_SCHEMA
import egress_controls as ec


class BoundInputWorkspace(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="r06-contract-")
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        self.paths = {"SCHEMA": root / "schema.json", "PROFILE": root / "profile.json", "PIPELINE": ec.PIPELINE}
        self.paths["SCHEMA"].write_bytes(ec.record_pipeline.encoded(APPROVAL_SCHEMA))
        self.paths["PROFILE"].write_bytes(ec.record_pipeline.encoded(PROFILE))
        self.control = {
            "schemaVersion": 2,
            "inputs": {
                "artifact:21:M1-05-record-schema:candidate": {
                    "path": "docs/team/M1-05/record-schema.json", "schemaVersion": 1,
                    "sha256": ec.sha256(self.paths["SCHEMA"].read_bytes()),
                    "pipeline": {"path": "scripts/team/M1-05/record_pipeline.py", "sha256": ec.sha256(ec.PIPELINE.read_bytes())}},
                "artifact:16:M0-03-execution-profile:candidate": {
                    "path": "docs/team/M0-03/execution-profile.json", "fileSha256": ec.sha256(self.paths["PROFILE"].read_bytes()),
                    "policyHash": PROFILE["policy"]["policyHash"]["value"]}},
            "stages": {
                "outputSanitization": {"rulesetVersion": "R-06/sanitize-v2"},
                "publicApproval": {"approvalBinding": {"manifestKind": "r06-publication-manifest", "manifestSchemaVersion": 1,
                                                       "digestField": "publicationManifestSha256"}}}}
        self.schema_input = self.control["inputs"]["artifact:21:M1-05-record-schema:candidate"]
        self.profile_input = self.control["inputs"]["artifact:16:M0-03-execution-profile:candidate"]
        patcher = mock.patch.multiple(ec, **self.paths)
        patcher.start()
        self.addCleanup(patcher.stop)


class BoundInputs(BoundInputWorkspace):
    def test_matching_synthetic_binding_reports_the_observed_digest(self):
        result = ec.check_binding(self.control)
        self.assertEqual(result["schemaSha256"], self.schema_input["sha256"])
        self.assertEqual(result["profilePolicyHash"], PROFILE["policy"]["policyHash"]["value"])

    def test_previous_control_version_and_ruleset_cannot_validate_v2_code(self):
        for field, value in (("schemaVersion", 1), ("schemaVersion", True), ("rulesetVersion", "R-06/sanitize-v1")):
            control = copy.deepcopy(self.control)
            if field == "schemaVersion":
                control[field] = value
            else:
                control["stages"]["outputSanitization"][field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ec.InputError):
                ec.check_binding(control)

    def test_audit_manifest_cannot_be_declared_as_the_publication_approval_target(self):
        self.control["stages"]["publicApproval"]["approvalBinding"]["digestField"] = "manifestSha256"
        with self.assertRaises(ec.InputError):
            ec.check_binding(self.control)

    def test_control_cannot_report_substituted_logical_input_paths(self):
        for label in ("schema", "pipeline", "profile"):
            control = copy.deepcopy(self.control)
            a = control["inputs"]["artifact:21:M1-05-record-schema:candidate"]
            b = control["inputs"]["artifact:16:M0-03-execution-profile:candidate"]
            subject = a if label == "schema" else a["pipeline"] if label == "pipeline" else b
            subject["path"] = "docs/synthetic-other.json"
            with self.subTest(input=label), self.assertRaises(ec.InputError):
                ec.check_binding(control)

    def test_schema_version_type_and_policy_digest_are_verified_not_just_reported(self):
        self.schema_input["schemaVersion"] = True
        with self.assertRaises(ec.InputError):
            ec.check_binding(self.control)
        self.schema_input["schemaVersion"] = 1
        self.profile_input["policyHash"] = "0" * 64
        with self.assertRaises(ec.InputError):
            ec.check_binding(self.control)

    def test_fully_valid_control_with_missing_input_reports_input_unavailable(self):
        self.paths["PROFILE"].unlink()
        with self.assertRaisesRegex(ec.InputError, "^binding_input_unavailable$"):
            ec.check_binding(self.control)

    def test_invalid_json_with_a_matching_byte_digest_still_cannot_bind(self):
        payload = b'{"policy":{},"policy":{}}'
        self.paths["PROFILE"].write_bytes(payload)
        self.profile_input["fileSha256"] = ec.sha256(payload)
        with self.assertRaises(ec.InputError):
            ec.check_binding(self.control)



class CommandLineIntegration(BoundInputWorkspace):
    def run_command(self):
        control_file = self.paths["PROFILE"].parent / "control.json"
        control_file.write_bytes(ec.record_pipeline.encoded(self.control))
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = ec.main(["run", "--control", str(control_file)])
        return code, json.loads(out.getvalue())

    def test_cli_runs_all_builtin_cases_with_bound_synthetic_input_bytes(self):
        code, result = self.run_command()
        self.assertEqual(code, 0)
        self.assertEqual(result["state"], "passed")
        self.assertEqual(result["counts"]["cases"], 36)
        self.assertEqual(result["counts"]["matched"], 36)
        self.assertEqual(result["counts"]["sendsOnRefusedBeforeSend"], 0)
        self.assertEqual(result["syntheticMarkersInReport"], [])
        self.assertEqual(result["binding"]["profileFileSha256"], self.profile_input["fileSha256"])

    def test_cli_stops_on_real_fixture_byte_drift_without_a_case_report(self):
        self.paths["PROFILE"].write_bytes(self.paths["PROFILE"].read_bytes() + b" ")
        code, result = self.run_command()
        self.assertEqual(code, 2)
        self.assertEqual(result["state"], "cannot_start")
        self.assertEqual(result["error"], "binding:profile_digest_mismatch")
        self.assertNotIn("cases", result)

    def test_cli_missing_bound_input_is_a_fixed_error_not_a_traceback(self):
        self.paths["SCHEMA"].unlink()
        code, result = self.run_command()
        self.assertEqual(code, 2)
        self.assertEqual(result, {"state": "cannot_start", "error": "binding_input_unavailable"})


class ContractFlows(Workspace):
    def test_topic_hash_digest_matches_a_profile_hash_object(self):
        digest = self.ctx["profile"]["policy"]["policyHash"]["value"]
        self.ctx["topics"]["T-PUB-01"]["policyHash"] = digest
        result = self.run_case()
        self.assertIsNone(result["refusal"])
        self.assertEqual(result["transportSends"], 1)

    def test_synthetic_topic_stores_a_digest_not_a_shared_mutable_hash_object(self):
        topic = self.ctx["topics"]["T-PUB-01"]
        self.assertIsInstance(topic["policyHash"], str)
        before = copy.deepcopy(topic["policyHash"])
        self.ctx["profile"]["policy"]["policyHash"]["value"] = "9" * 64
        self.assertEqual(topic["policyHash"], before)
        result = self.run_case()
        self.assertEqual(result["transportSends"], 0)
        self.assertEqual(result["refusal"]["code"], "policy_hash_mismatch")

    def test_missing_transport_mapping_is_refused_and_audited_before_send(self):
        for value in (None, {}, {"DEST-MODEL-AUTHOR": "https://model-author.invalid"}):
            with self.subTest(mapping=repr(value)):
                self.ctx["hosts"] = value
                try:
                    result = self.run_case()
                except Exception as error:
                    self.fail("Missing transport configuration escaped as " + type(error).__name__)
                self.assertEqual(result["transportSends"], 0)
                self.assertEqual(result["refusal"]["code"], "transport_destination_unconfigured")
                self.assertIsNotNone(result["record"])

    def test_aggregate_report_preserves_the_post_publication_audit(self):
        cases = ec.run_cases(self.ctx["profile"], self.ctx["schema"], ec.case_catalog()[:1])
        self.assertEqual(cases[0]["publication"]["result"], "published")
        self.assertIsNotNone(cases[0].get("publicationRecord"))
        self.assertEqual(cases[0]["publicationRecord"]["approval"], "pending")

    def test_publication_audit_binds_actual_approved_file_hashes(self):
        result = self.run_case(publication={"approval": ec.approval_for(), "slug": "synthetic-audit-binding"})
        raw = next(self.root.glob("r06-run-*/publication/raw.json"))
        events = ec.record_pipeline.decode(raw.read_bytes())["events"]
        payload = "\n".join(e["content"] for e in events)
        self.assertIn(result["publication"]["manifestSha256"], payload)
        self.assertIn(result["record"]["manifestSha256"], payload)
        for digest in result["publication"]["sha256"].values():
            self.assertIn(digest, payload)


if __name__ == "__main__":
    unittest.main()
