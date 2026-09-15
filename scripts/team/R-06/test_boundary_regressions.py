"""Additional behavioral counterexamples. All data and destinations are synthetic.

The fixture module is deliberately imported as a module (not a TestCase alias),
so unittest discovery does not count its original tests a second time.
"""
import copy
import json
import os
import stat
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import egress_controls as ec
import test_security_regressions as fixtures


class Workspace(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="r06-boundary-")
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.research = self.root / "research"
        self.research.mkdir()
        self.ctx = ec.synthetic_context(copy.deepcopy(fixtures.PROFILE), copy.deepcopy(fixtures.APPROVAL_SCHEMA), self.root, self.research)

    def run_case(self, request=None, body=None, responses=None, publication=None):
        try:
            return ec.run_request(self.ctx, request if request is not None else dict(ec.REQ),
                responses if responses is not None else [ec.ok(body if body is not None else ec.clean_body())], publication)
        except Exception as error:
            self.fail("Input escaped the controlled refusal path: " + type(error).__name__)


class BoundaryRegressions(Workspace):
    def test_unknown_field_name_is_not_reflected_in_public_result(self):
        request = dict(ec.REQ)
        request[ec.SYNTH_SECRET] = "synthetic"
        result = self.run_case(request=request)
        self.assertEqual(result["transportSends"], 0)
        self.assertIsNotNone(result["record"])
        self.assertNotIn(ec.SYNTH_SECRET, json.dumps(result))

    def test_invalid_url_authorities_cannot_pass_suffix_allowlist(self):
        for url in ("https://api_key=synthetic-fixture-not-a-key-0001.docs.example.org/x",
                    "https://synthetic.person%40example.invalid.docs.example.org/x",
                    "https://bad..docs.example.org/x",
                    "https://-bad.docs.example.org/x"):
            with self.subTest(url=url):
                result = self.run_case(body=ec.clean_body(url=url))
                self.assertTrue(result["outputSanitization"].startswith("refused:"))
                self.assertIsNotNone(result["record"])

    def test_non_list_domain_allowlist_is_rejected(self):
        entry = self.ctx["topics"]["T-PUB-01"]
        entry["allowedSourceDomains"] = "docs.example.org"
        result = self.run_case(body=ec.clean_body(url="https://unapproved.g/x"))
        self.assertEqual(result["transportSends"], 0)
        self.assertIsNotNone(result["refusal"])

    def test_registered_topic_text_is_checked_before_transport(self):
        for text in (ec.SYNTH_SECRET, ec.SYNTH_EMAIL, ec.SYNTH_POSIX_PATH):
            with self.subTest(kind=text.split("=")[0]):
                self.ctx["topics"]["T-PUB-01"]["topic"] = text
                result = self.run_case(publication={"approval": ec.approval_for(), "slug": "synthetic-metadata"})
                self.assertEqual(result["transportSends"], 0)
                self.assertNotIn(text, json.dumps(result))
        self.assertEqual(list(self.research.iterdir()), [])

    def test_egress_approval_reference_requires_nonblank_string(self):
        for ref in (True, 1, ["synthetic-ref"], {"ref": "synthetic-ref"}):
            with self.subTest(type=type(ref).__name__):
                self.ctx["approvals"] = [{"topicId": "T-PUB-01", "destinationId": "DEST-MODEL-AUTHOR",
                    "owner": "APR-EGRESS", "decision": "approved", "decisionRef": ref}]
                result = self.run_case()
                self.assertEqual(result["transportSends"], 0)
                self.assertEqual(result["refusal"]["code"], "egress_approval_not_recorded")

    def test_unknown_egress_disposition_is_default_denied(self):
        for rule in (None, "unknown", "ALLOW", [], {}):
            with self.subTest(rule=repr(rule)):
                self.ctx["profile"]["egress"]["classMatrix"]["PUBLIC_SYNTHETIC"]["modelEgress"] = rule
                result = self.run_case()
                self.assertEqual(result["transportSends"], 0)
                self.assertEqual(result["refusal"]["code"], "data_class_denied_for_model_egress")

    def test_redirect_count_survives_a_later_refusal(self):
        good = {"status": 302, "location": "https://model-author.invalid/next"}
        for last in ({"status": 302, "location": "https://collector.invalid/next"}, {"status": 500, "body": ec.clean_body()}):
            with self.subTest(status=last["status"]):
                result = self.run_case(responses=[good, good, last])
                self.assertIsNotNone(result["refusal"])
                self.assertEqual(result["transportSends"], 3)
                self.assertEqual(result["redirectsFollowed"], 2)

    @unittest.skipUnless(os.name == "posix", "POSIX mode-bit assertion only")
    def test_raw_record_is_owner_only_independent_of_umask(self):
        work = self.root / "work"
        work.mkdir()
        result = {"inputClassification": "admitted:PUBLIC_SYNTHETIC", "egressDecision": "allowed:DEST-MODEL-AUTHOR",
                  "outputSanitization": "clean", "refusal": None}
        old = os.umask(0o022)
        try:
            ec.record_run(work, result, ec.REQ, ec.clean_body())
        finally:
            os.umask(old)
        self.assertEqual(stat.S_IMODE((work / "raw.json").stat().st_mode), 0o600)

    def test_calendar_invalid_date_is_not_published(self):
        for date in ("2026-02-31", "2026-13-01", "0000-01-01", "２０２６-０９-１５"):
            with self.subTest(date=date):
                self.ctx["date"] = date
                result = self.run_case(publication={"approval": ec.approval_for(), "slug": "synthetic-calendar"})
                self.assertEqual(result["publication"]["result"], "refused:invalid_date")
        self.assertEqual(list(self.research.iterdir()), [])

    def test_read_json_rejects_ambiguous_duplicate_keys_and_nonfinite_numbers(self):
        for i, text in enumerate(('{"schemaVersion": 1, "schemaVersion": 2}', '{"value": NaN}', '{"value": Infinity}')):
            path = self.root / (str(i) + ".json")
            path.write_text(text)
            with self.subTest(case=i), self.assertRaises(ec.InputError):
                ec.read_json(path)

    def test_binding_missing_inputs_is_input_error_not_filesystem_traceback(self):
        control = {"inputs": {
            "artifact:21:M1-05-record-schema:candidate": {"sha256": "a" * 64, "pipeline": {"sha256": "a" * 64}},
            "artifact:16:M0-03-execution-profile:candidate": {"fileSha256": "b" * 64}}}
        with mock.patch.object(ec, "SCHEMA", self.root / "absent.json"):
            try:
                ec.check_binding(control)
            except ec.InputError:
                pass
            except Exception as error:
                self.fail("Binding escaped as " + type(error).__name__)
            else:
                self.fail("Missing input was accepted")

    def test_binding_incomplete_control_is_input_error(self):
        for control in (None, [], {}, {"inputs": {}}):
            with self.subTest(control=control):
                try:
                    ec.check_binding(control)
                except ec.InputError:
                    pass
                except Exception as error:
                    self.fail("Incomplete control escaped as " + type(error).__name__)
                else:
                    self.fail("Incomplete control was accepted")


class PublicationAndAuditRegressions(Workspace):
    def test_encoded_query_sensitive_values_are_not_sent(self):
        values = ("contact synthetic.person%40example.invalid", "api_key%3Dsynthetic-fixture-not-a-key-0001",
                  "api_key%253Dsynthetic-fixture-not-a-key-0001", "%2Fhome%2Fsynthetic%2Fnotes.txt")
        for index, query in enumerate(values):
            with self.subTest(case=index):
                result = self.run_case(request=dict(ec.REQ, queries=[query]))
                self.assertEqual(result["transportSends"], 0)
                self.assertIsNotNone(result["refusal"])

    @unittest.skipUnless(hasattr(os, "symlink"), "symlink capability required")
    def test_dangling_output_link_is_refused_before_any_file_is_written(self):
        target = self.research / "2026-09-15-synthetic-link.md"
        target.symlink_to(self.root / "not-created")
        result = self.run_case(publication={"approval": ec.approval_for(), "slug": "synthetic-link"})
        self.assertTrue(result["publication"]["result"].startswith("refused:"))
        self.assertEqual({p.name for p in self.research.iterdir()}, {target.name})
        self.assertFalse((self.root / "not-created").exists())

    @unittest.skipUnless(hasattr(os, "symlink"), "symlink capability required")
    def test_dangling_output_link_within_root_is_refused_before_the_first_write(self):
        # An in-root dangling target passes path containment. This case isolates
        # lexists from the separate containment guard that protected the outside-root case.
        target = self.research / "2026-09-15-synthetic-inside.md"
        absent = self.research / "not-created"
        target.symlink_to(absent)
        result = self.run_case(publication={"approval": ec.approval_for(), "slug": "synthetic-inside"})
        self.assertEqual(result["publication"]["result"], "refused:target_exists_no_overwrite")
        self.assertEqual({p.name for p in self.research.iterdir()}, {target.name})
        self.assertFalse(absent.exists())

    def test_second_output_io_failure_is_recorded_and_never_reports_success(self):
        import builtins
        real_open = builtins.open
        def fail_markdown(path, *args, **kwargs):
            if Path(path).name == "2026-09-15-synthetic-io.md":
                raise OSError("synthetic private detail must not enter public results")
            return real_open(path, *args, **kwargs)
        with mock.patch("builtins.open", side_effect=fail_markdown):
            result = self.run_case(publication={"approval": ec.approval_for(), "slug": "synthetic-io"})
        self.assertEqual(result["publication"]["result"], "refused:publication_io_failed")
        self.assertIsNotNone(result.get("publicationRecord"))
        self.assertNotIn("synthetic private detail", json.dumps(result))
        # A partial, already approved file is preserved as failure evidence, not deleted or called complete.
        self.assertTrue((self.research / "2026-09-15-synthetic-io.json").exists())
        self.assertFalse((self.research / "2026-09-15-synthetic-io.md").exists())

    def test_publication_outcome_is_written_through_the_record_pipeline(self):
        for decision in ("approved", "pending", "rejected"):
            with self.subTest(decision=decision):
                result = self.run_case(publication={"approval": ec.approval_for(decision=decision), "slug": "synthetic-" + decision})
                self.assertIsNotNone(result.get("publicationRecord"))
                self.assertEqual(result["publicationRecord"]["approval"], "pending")
        raw_records = [json.loads(p.read_bytes()) for p in self.root.glob("r06-run-*/publication/raw.json")]
        self.assertEqual(len(raw_records), 3)
        approvals = [e for r in raw_records for e in r["events"] if e["kind"] == "approval"]
        self.assertEqual(len(approvals), 3)
        self.assertEqual({e["content"] for e in approvals}, {"decision=approved", "decision=pending", "decision=rejected"})
        for p in self.root.glob("r06-run-*/publication/public"):
            ec.record_pipeline.verify(p)

    def test_non_string_request_keys_fail_closed_without_recording_exception(self):
        result = self.run_case(request={**ec.REQ, 1: "synthetic"})
        self.assertEqual(result["transportSends"], 0)
        self.assertIsNotNone(result["record"])
        self.assertEqual(result["refusal"]["code"], "field_not_allowed:unknown")

    def test_empty_requested_catalog_is_not_silently_replaced_by_passing_defaults(self):
        with self.assertRaises(ec.InputError):
            ec.run_cases(self.ctx["profile"], self.ctx["schema"], catalog=[])

    def test_conflicting_egress_decisions_are_not_treated_as_an_approval(self):
        valid = copy.deepcopy(self.ctx["approvals"][0])
        refused = dict(valid, decision="rejected", decisionRef="synthetic-revocation")
        for approvals in ([valid, refused], [refused, valid]):
            self.ctx["approvals"] = approvals
            result = self.run_case()
            self.assertEqual(result["transportSends"], 0)
            self.assertIsNotNone(result["refusal"])

    def test_absent_class_matrix_entry_does_not_escape_audit(self):
        for matrix in (None, [], "unknown"):
            with self.subTest(kind=type(matrix).__name__):
                self.ctx["profile"]["egress"]["classMatrix"]["PUBLIC_SYNTHETIC"] = matrix
                result = self.run_case()
                self.assertEqual(result["transportSends"], 0)
                self.assertIsNotNone(result["record"])

if __name__ == "__main__":
    unittest.main()
