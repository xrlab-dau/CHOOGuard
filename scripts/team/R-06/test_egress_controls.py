"""Tests for the R-06 candidate research harness controls. Synthetic inputs, fake transport, temporary directories only."""
import contextlib
import copy
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import egress_controls as ec  # noqa: E402

CONTROL = ec.read_json(ec.CONTROL)
PROFILE = ec.read_json(ec.PROFILE)
SCHEMA = ec.read_json(ec.SCHEMA)


class Workspace(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="r06-test-")
        self.root = Path(self.tmp.name)
        self.research = self.root / "research"
        self.research.mkdir()
        self.ctx = ec.synthetic_context(PROFILE, SCHEMA, self.root, self.research)

    def tearDown(self):
        self.tmp.cleanup()


class Report(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.report = ec.build_report(CONTROL)
        cls.cases = {c["id"]: c for c in cls.report["cases"]}

    def test_every_synthetic_case_matches_and_report_carries_no_fixture_values(self):
        self.assertEqual(self.report["state"], "passed", [c["id"] for c in self.report["cases"] if not c["matched"]])
        self.assertEqual(self.report["syntheticMarkersInReport"], [])
        text = json.dumps(self.report, ensure_ascii=False)
        for marker in ("r06-run-", "r06-test-", "AppData", "synthetic-egress-ref", "synthetic-publish-ref"):
            self.assertNotIn(marker, text)

    def test_refusals_before_send_never_reach_the_transport(self):
        self.assertGreater(self.report["counts"]["refusedBeforeSend"], 0)
        self.assertEqual(self.report["counts"]["sendsOnRefusedBeforeSend"], 0)
        self.assertEqual(self.cases["R06-N17"]["observed"]["transportSends"], 1)
        self.assertEqual(self.cases["R06-N17"]["observed"]["redirectsFollowed"], 0)

    def test_the_four_stage_fields_are_independent(self):
        pending = self.cases["R06-P02"]["observed"]
        self.assertEqual((pending["egressDecision"], pending["outputSanitization"], pending["publicApproval"]), ("allowed:DEST-MODEL-AUTHOR", "clean", "pending"))
        output_refused = self.cases["R06-N18"]["observed"]
        self.assertEqual((output_refused["egressDecision"], output_refused["outputSanitization"], output_refused["publication"]),
                         ("allowed:DEST-MODEL-AUTHOR", "refused:output_contains_secret", "refused:sanitization_not_passed"))
        egress_refused = self.cases["R06-N09"]["observed"]
        self.assertEqual((egress_refused["inputClassification"], egress_refused["egressDecision"]), ("admitted:PUBLIC_SYNTHETIC", "refused:destination_not_in_profile"))
        bound = self.cases["R06-N29"]["observed"]
        self.assertEqual((bound["publicApproval"], bound["publication"]), ("approved", "refused:approval_bound_to_other_manifest"))
        self.assertEqual(sum(c["observed"]["publication"] == "published" for c in self.report["cases"]), 1)

    def test_every_run_is_recorded_through_m1_05_with_pending_approval(self):
        for case in self.report["cases"]:
            with self.subTest(case=case["id"]):
                self.assertEqual(case["record"]["approval"], "pending")
                self.assertRegex(case["record"]["manifestSha256"], r"^[0-9a-f]{64}$")
        self.assertEqual(self.cases["R06-N18"]["record"]["removals"], {"secret": 1})
        self.assertEqual(self.report["binding"]["schemaSha256"], CONTROL["inputs"]["artifact:21:M1-05-record-schema:candidate"]["sha256"])

    def test_catalog_covers_required_surfaces_and_states_capability(self):
        self.assertEqual({c["category"] for c in self.report["cases"]}, {"positive", "input", "egress", "output", "publication"})
        codes = {c["refusal"]["code"] for c in self.report["cases"] if c["refusal"]}
        for required in ("output_contains_secret", "output_contains_personal", "output_contains_absolute_path", "destination_not_in_profile", "redirect_to_other_host"):
            self.assertIn(required, codes)
        for case in self.report["cases"]:
            self.assertTrue(case["capability"])
            if case["category"] != "positive":
                self.assertTrue(case["expectedFailure"])


class Binding(unittest.TestCase):
    def run_main(self, *argv):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = ec.main(list(argv))
        return code, json.loads(out.getvalue())

    def test_digest_drift_or_missing_control_stops_before_any_case(self):
        mutations = (("schema", lambda c: c["inputs"]["artifact:21:M1-05-record-schema:candidate"].__setitem__("sha256", "0" * 64)),
                     ("pipeline", lambda c: c["inputs"]["artifact:21:M1-05-record-schema:candidate"]["pipeline"].__setitem__("sha256", "0" * 64)),
                     ("profile", lambda c: c["inputs"]["artifact:16:M0-03-execution-profile:candidate"].__setitem__("fileSha256", "0" * 64)))
        with tempfile.TemporaryDirectory() as tmp:
            for key, mutate in mutations:
                with self.subTest(binding=key):
                    changed = copy.deepcopy(CONTROL)
                    mutate(changed)
                    path = Path(tmp) / f"{key}.json"
                    path.write_text(json.dumps(changed), encoding="utf-8")
                    code, out = self.run_main("run", "--control", str(path))
                    self.assertEqual((code, out["state"]), (2, "cannot_start"))
                    self.assertIn(key + "_digest_mismatch", out["error"])
            self.assertEqual(self.run_main("run", "--control", str(Path(tmp) / "absent.json"))[0], 2)


class Stages(Workspace):
    def test_record_bundle_is_verified_and_public_candidate_has_no_free_text(self):
        result = {"inputClassification": "admitted:PUBLIC_SYNTHETIC", "egressDecision": "allowed:DEST-MODEL-AUTHOR",
                  "outputSanitization": "refused:output_contains_personal", "refusal": {"stage": "outputSanitization", "code": "output_contains_personal"}}
        work = self.root / "work"
        work.mkdir()
        record = ec.record_run(work, result, ec.REQ, ec.clean_body(excerpt="Mail " + ec.SYNTH_EMAIL))
        manifest = ec.record_pipeline.verify(work / "public")
        self.assertEqual(manifest["filesDigest"], record["filesDigest"])
        public = (work / "public" / "events.json").read_text(encoding="utf-8") + (work / "public" / "manifest.json").read_text(encoding="utf-8")
        for marker in ec.SYNTH_MARKERS + ("docs.example.org", "Release notes", "topicId"):
            self.assertNotIn(marker, public)
        self.assertEqual(record["removals"], {"personal": 1})

    def test_publication_creates_new_escaped_files_and_never_overwrites(self):
        body = {"claims": [{"id": "C-01", "claim": "pipes | and [brackets](x) stay text", "evidence": ec.clean_body()["claims"][0]["evidence"]}]}
        first = ec.run_request(self.ctx, dict(ec.REQ), [ec.ok(body)], {"approval": ec.approval_for(), "slug": "synthetic-escape"})
        self.assertEqual(first["publication"]["result"], "published")
        markdown = (self.research / "2026-09-15-synthetic-escape.md").read_text(encoding="utf-8")
        self.assertIn("pipes \\| and \\[brackets\\](x) stay text", markdown)
        before = {p.name: p.read_bytes() for p in self.research.iterdir()}
        second = ec.run_request(self.ctx, dict(ec.REQ), [ec.ok(body)], {"approval": ec.approval_for(), "slug": "synthetic-escape"})
        self.assertEqual(second["publication"]["result"], "refused:target_exists_no_overwrite")
        self.assertEqual({p.name: p.read_bytes() for p in self.research.iterdir()}, before)

    def test_approval_record_rules_are_read_from_the_m1_05_schema(self):
        record = {"schemaVersion": 1, "state": "approval_record", "manifestSha256": "a" * 64, "decision": "approved", "decisionRef": "synthetic-ref"}
        ec.validate_approval_record(SCHEMA, record)
        narrowed = copy.deepcopy(SCHEMA)
        narrowed["$defs"]["approval"]["properties"]["decision"]["enum"] = ["unknown", "pending", "rejected"]
        with self.assertRaises(ec.Refused):
            ec.validate_approval_record(narrowed, record)
        for broken in ({**record, "decisionRef": "   "}, {**record, "extra": 1}, {**record, "state": "public_manifest"}, {k: v for k, v in record.items() if k != "decision"}):
            with self.subTest(record=sorted(broken)):
                with self.assertRaises(ec.Refused):
                    ec.validate_approval_record(SCHEMA, broken)

    def test_sanitizer_accepts_clean_https_output_and_refuses_bypass_forms(self):
        entry = self.ctx["topics"]["T-PUB-01"]
        for body in (ec.clean_body(), ec.clean_body(url="https://sub.docs.example.org/a?page=2")):
            self.assertEqual(ec.sanitize_output(entry, body), body)
        cases = {"url_domain_not_allowed": ec.clean_body(url="https://example.org/x"), "url_credential_query": ec.clean_body(url="https://docs.example.org/x?API_KEY=1"),
                 "output_contains_absolute_path": ec.clean_body(excerpt="\\\\fileserver\\share\\x"), "output_contains_restricted_marker": ec.clean_body(excerpt="copied from private-data/x"),
                 "embedded_instruction": ec.clean_body(title="<tool_call> write"), "output_schema_invalid": {"claims": [{"id": "C-01", "claim": 3, "evidence": []}]}}
        for code, body in cases.items():
            with self.subTest(code=code):
                with self.assertRaises(ec.Refused) as caught:
                    ec.sanitize_output(entry, body)
                self.assertEqual(caught.exception.code, code)

    def test_redirect_hops_are_bounded_even_on_the_granted_host(self):
        entry = self.ctx["topics"]["T-PUB-01"]
        hop = {"status": 302, "location": "https://model-author.invalid/again"}
        transport = ec.FakeTransport(self.ctx["hosts"], [hop] * 5 + [ec.ok(ec.clean_body())])
        with self.assertRaises(ec.Refused) as caught:
            ec.exchange(transport, {"destinationId": "DEST-MODEL-AUTHOR"}, entry, ec.REQ)
        self.assertEqual((caught.exception.code, len(transport.sends)), ("redirect_to_other_host", 4))

    def test_egress_approval_owner_must_match_the_destination(self):
        entry = self.ctx["topics"]["T-PUB-01"]
        self.assertEqual(ec.authorize_egress(PROFILE, entry, dict(ec.REQ), self.ctx["approvals"])["approvalOwner"], "APR-EGRESS")
        publish_owner_only = [dict(a, owner="APR-PUBLISH") for a in self.ctx["approvals"]]
        with self.assertRaises(ec.Refused) as caught:
            ec.authorize_egress(PROFILE, entry, dict(ec.REQ), publish_owner_only)
        self.assertEqual(caught.exception.code, "egress_approval_not_recorded")


if __name__ == "__main__":
    unittest.main()
