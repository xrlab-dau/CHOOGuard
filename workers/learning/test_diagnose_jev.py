"""Offline evidence classification, chronology and payload-exclusion contracts."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from diagnose_jev import classify, diagnose


def record(http=200, answers=None, *, at="2026-10-13T01:00:00Z", purpose="judge", seconds=1):
    return {"at": at, "purpose": purpose, "lane": "Director", "http": http, "seconds": seconds,
            "request": {"questions": {"a": {"type": "score"}, "b": {"type": "score"}}},
            "answers": answers}


def complete(**kwargs):
    return record(answers={"a": {"Score": 1}, "b": {"Score": 0}}, **kwargs)


class DiagnosticsTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.path = self.root / "jev-run.jsonl"

    def write(self, records, path=None):
        path = path or self.path
        path.write_text("".join(json.dumps(row) + "\n" for row in records), encoding="utf-8")
        return path

    def test_http_categories_do_not_claim_network_root_causes(self):
        expected = {0: "no_http_response", -1: "no_http_response", 401: "http_auth_rejected",
                    403: "http_auth_rejected", 429: "http_rate_limited", 500: "http_server_error",
                    504: "http_server_error", 408: "other_http_status", 302: "other_http_status"}
        for status, outcome in expected.items():
            with self.subTest(status=status):
                self.assertEqual(classify(record(http=status))[2], outcome)

    def test_complete_partial_empty_unknown_and_unexpected_ids(self):
        self.assertEqual(classify(complete())[2], "complete_answers")
        self.assertEqual(classify(record(answers={"a": {"Score": 1}}))[2], "partial_answers")
        for answers in (None, {}):
            self.assertEqual(classify(record(answers=answers))[2], "http200_without_parsed_answers")
        unknown = complete()
        unknown.pop("request")
        self.assertEqual(classify(unknown)[2], "answers_coverage_unknown")
        self.assertEqual(classify(record(answers={"outside": {"Score": 1}}))[2], "unexpected_answer_ids")
        self.assertEqual(classify(complete(http=503))[2], "http_server_error")

    def test_invalid_numbers_and_answer_shapes_are_rejected(self):
        for changes in ({"http": True}, {"http": "200"}, {"http": 600}, {"http": 99},
                        {"seconds": True}, {"seconds": -1}, {"seconds": float("inf")},
                        {"seconds": 10**1000}, {"answers": []}, {"answers": {"a": {}}}):
            row = complete()
            row.update(changes)
            with self.subTest(changes=list(changes)), self.assertRaises(ValueError):
                classify(row)

    def test_durations_and_outcomes_are_separated_by_purpose(self):
        self.write([complete(seconds=1), complete(seconds=3, at="2026-10-13T01:00:01Z"),
                    record(http=504, purpose="crowd-urgent", seconds=84.5)])
        result = diagnose([self.path], "relay")
        self.assertTrue(result["evidence_valid"])
        groups = result["files"][0]["by_purpose"]
        self.assertEqual(groups["judge"]["seconds"], {"samples": 2, "median": 2, "p95_nearest_rank": 3, "max": 3})
        self.assertEqual(groups["crowd-urgent"]["seconds"]["max"], 84.5)
        self.assertEqual(result["connection_path_declared"], "relay")
        self.assertEqual(result["files"][0]["sha256"], hashlib.sha256(self.path.read_bytes()).hexdigest())

    def test_response_completion_order_not_append_order_drives_transitions(self):
        # Background writes can finish out of order; use distinct normalized timestamps.
        self.write([complete(at="2026-10-13T10:00:02+09:00"),
                    record(http=504, at="2026-10-13T01:00:00Z"),
                    record(answers={"a": {"Score": 1}}, at="2026-10-13T01:00:01Z")])
        row = diagnose([self.path])["files"][0]["by_purpose"]["judge"]["response_transitions"]
        self.assertEqual(row, {"available": True, "max_consecutive_not_complete": 2,
                               "trailing_not_complete": 0, "complete_after_not_complete": 1})

    def test_ambiguous_timestamp_or_invalid_record_disables_transition_claim(self):
        for rows in ([record(), complete()], [record(at="unknown"), complete()],
                     [record(at="2026-10-13T01:00:00"), complete()]):
            self.write(rows)
            result = diagnose([self.path])
            self.assertFalse(result["files"][0]["by_purpose"]["judge"]["response_transitions"]["available"])
        self.write([complete()])
        with self.path.open("a") as stream:
            stream.write("broken\n")
        result = diagnose([self.path])
        self.assertFalse(result["evidence_valid"])
        self.assertFalse(result["files"][0]["by_purpose"]["judge"]["response_transitions"]["available"])

    def test_separate_sessions_do_not_create_a_recovery_or_duplicate_counts(self):
        self.write([record(http=504)])
        second = self.write([complete(at="2026-10-13T02:00:00Z")], self.root / "second.jsonl")
        result = diagnose([self.path, second])
        self.assertTrue(result["evidence_valid"])
        self.assertEqual(result["files"][1]["by_purpose"]["judge"]["response_transitions"]["complete_after_not_complete"], 0)
        result = diagnose([self.path, self.path])
        self.assertFalse(result["evidence_valid"])
        self.assertTrue(result["duplicate_input_content"])
        self.assertEqual(result["totals"], {"http_server_error": 1})
        self.assertEqual(result["files"][1]["duplicate_of_file_index"], 1)

    def test_unknown_labels_paths_and_payloads_never_leave_the_reader(self):
        secret = "SENSITIVE_SENTINEL_do_not_export"
        row = complete()
        row.update({"purpose": secret, "lane": secret, "model": secret,
                    "endpoint": "https://" + secret + ".invalid", "authorization": secret})
        row["request"]["state"] = {"private": secret}
        row["request"]["questions"][secret] = {"instructions": secret}
        row["answers"][secret] = {"Choice": secret}
        path = self.write([row], self.root / (secret + ".jsonl"))
        result = diagnose([path])
        self.assertNotIn(secret, json.dumps(result))
        self.assertEqual(result["files"][0]["lanes"], {"other": 1})
        self.assertIn("other", result["files"][0]["by_purpose"])
        missing = diagnose([self.root / secret])
        self.assertNotIn(secret, json.dumps(missing))

    def test_bom_crlf_unterminated_lone_cr_nonfinite_json_and_invalid_utf8(self):
        encoded = json.dumps(complete()).encode()
        self.path.write_bytes(b"\xef\xbb\xbf" + encoded + b"\r\n")
        self.assertTrue(diagnose([self.path])["evidence_valid"])
        for content in (encoded, encoded + b"\r", b'{"seconds":NaN}\n', b"\xff\n", b"[]\n", b"\n"):
            self.path.write_bytes(content)
            self.assertFalse(diagnose([self.path])["evidence_valid"])

    def test_cli_success_means_evidence_read_not_service_health(self):
        self.write([record(http=504)])
        script = Path(__file__).with_name("diagnose_jev.py")
        result = subprocess.run([sys.executable, str(script), str(self.path)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)["totals"], {"http_server_error": 1})
        self.path.write_text("malformed SENSITIVE_SENTINEL\n", encoding="utf-8")
        result = subprocess.run([sys.executable, str(script), str(self.path)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 1)
        self.assertNotIn("SENSITIVE_SENTINEL", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
