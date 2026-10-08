"""Offline capture quality: a successful Unity test is not complete learning data."""
import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from validate_capture import validate


def answered(purpose="judge"):
    return {"http": 200, "purpose": purpose, "answers": {"candidate": {"Score": 1}}}


def shift(index=0):
    return {
        "shift": index, "seed": 1000 + index * 8, "keyPresent": True,
        "jevRejected": False, "requests": 2, "jevLogLines": 2,
        "answeredLines": 2, "answeredByPurpose": {"judge": 1, "crowd-routine": 1},
        "drained": True, "pendingRequests": 0, "malformedLines": 0, "errorLogs": 0,
        "director": {"rounds": 1, "unanswered_rounds": 0},
        "hazardsSeen": 0, "playerKnew": False,
        "jsonl": f"jev-run/jev-shift-{index:02d}.jsonl",
    }


class CaptureTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.summary = self.root / "shifts-run.json"
        self.rows = [shift()]
        self.write_log(self.rows[0], [answered(), answered("crowd-routine")])

    def write_log(self, row, records):
        path = self.root / row["jsonl"]
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("".join(json.dumps(record) + "\n" for record in records), encoding="utf-8")
        return path

    def check(self, expected=1):
        self.summary.write_text(json.dumps(self.rows), encoding="utf-8")
        return validate(self.summary, expected)

    def assert_bad(self, report, fragment):
        self.assertFalse(report["ok"])
        self.assertIn(fragment, "\n".join(report["errors"]))

    def test_complete_multiple_shifts_without_incidents_pass(self):
        self.rows.append(shift(1))
        self.write_log(self.rows[1], [answered(), answered("crowd-routine")])
        discarded = self.root / "jev-run/discarded/jev-shift-00-attempt-0.jsonl"
        discarded.parent.mkdir()
        discarded.write_text("discarded attempt is not a selected shift", encoding="utf-8")
        report = self.check(expected=2)
        self.assertTrue(report["ok"], report)
        self.assertEqual(report["errors"], [])

    def test_unlogged_director_rejections_fail_even_when_every_logged_request_answered(self):
        self.rows[0]["director"] = {"rounds": 370, "unanswered_rounds": 377}
        self.assert_bad(self.check(), "director.unanswered_rounds")

    def test_missing_legacy_fields_are_not_assumed_zero(self):
        original = copy.deepcopy(self.rows[0])
        for field in ("director", "requests", "keyPresent", "drained", "answeredByPurpose"):
            with self.subTest(field=field):
                self.rows[0] = copy.deepcopy(original)
                del self.rows[0][field]
                self.assert_bad(self.check(), field)

    def test_failed_or_unfinished_shift_is_rejected(self):
        for field, value in (("keyPresent", False), ("jevRejected", True), ("drained", False),
                             ("pendingRequests", 1), ("malformedLines", 1), ("errorLogs", 1)):
            with self.subTest(field=field):
                self.rows = [shift()]
                self.rows[0][field] = value
                self.assert_bad(self.check(), field)

    def test_count_types_are_strict_and_nonnegative(self):
        for value in (None, True, -1, 1.5, "0"):
            with self.subTest(value=value):
                self.rows[0]["director"]["unanswered_rounds"] = value
                self.assert_bad(self.check(), "director.unanswered_rounds")
        self.rows = [shift()]
        self.rows[0]["drained"] = 1
        self.assert_bad(self.check(), "drained")

    def test_partial_or_duplicate_shifts_fail(self):
        self.assert_bad(self.check(expected=2), "expected 2")
        self.rows.append(copy.deepcopy(self.rows[0]))
        self.assert_bad(self.check(expected=2), "shift indices")
        self.rows[1]["shift"] = 1
        self.assert_bad(self.check(expected=2), "same JSONL")

    def test_missing_log_and_changed_counts_fail(self):
        (self.root / self.rows[0]["jsonl"]).unlink()
        self.assert_bad(self.check(), "JSONL")
        self.write_log(self.rows[0], [answered()])
        self.assert_bad(self.check(), "jevLogLines")

    def test_requests_lost_to_log_limit_fail(self):
        self.rows[0]["requests"] = 3
        self.assert_bad(self.check(), "requests")

    def test_partial_final_line_and_malformed_records_fail(self):
        for text in (json.dumps(answered()), "{broken}\n", "null\n", "[]\n", "\n", "   \n"):
            with self.subTest(text=text):
                (self.root / self.rows[0]["jsonl"]).write_text(text, encoding="utf-8")
                self.assert_bad(self.check(), "JSONL")

    def test_windows_bom_and_crlf_preserve_valid_capture_counts(self):
        records = [answered(), answered("crowd-routine")]
        text = "".join(json.dumps(record) + "\r\n" for record in records)
        (self.root / self.rows[0]["jsonl"]).write_bytes(text.encode("utf-8-sig"))
        self.summary.write_bytes(json.dumps(self.rows).encode("utf-8-sig"))
        self.assertTrue(validate(self.summary, 1)["ok"])

    def test_lone_carriage_return_is_not_a_completed_harness_record(self):
        text = json.dumps(answered()) + "\n" + json.dumps(answered("crowd-routine")) + "\r"
        (self.root / self.rows[0]["jsonl"]).write_bytes(text.encode("utf-8"))
        self.assert_bad(self.check(), "newline-terminated")

    def test_nonfinite_or_invalid_utf8_log_is_not_complete(self):
        for data in (b'{"http": 200, "answers": {"q": {"Score": NaN}}}\n', b'\xff\n'):
            with self.subTest(data=data):
                (self.root / self.rows[0]["jsonl"]).write_bytes(data)
                self.assert_bad(self.check(), "JSONL")

    def test_answer_counts_and_purpose_distribution_are_verified(self):
        self.write_log(self.rows[0], [answered(), answered()])
        self.assert_bad(self.check(), "answeredByPurpose")
        self.write_log(self.rows[0], [answered(), {"http": 200, "answers": {}}])
        self.assert_bad(self.check(), "answeredLines")

    def test_recorded_http_failures_warn_when_counts_are_honest(self):
        self.write_log(self.rows[0], [answered(), {"http": 429, "purpose": "crowd-routine", "answers": {}}])
        self.rows[0]["answeredLines"] = 1
        self.rows[0]["answeredByPurpose"] = {"judge": 1}
        report = self.check()
        self.assertTrue(report["ok"], report)
        self.assertTrue(report["warnings"])

    def test_no_successful_answers_fail(self):
        self.write_log(self.rows[0], [{"http": 429, "answers": {}}, {"http": 0, "answers": {}}])
        self.rows[0]["answeredLines"] = 0
        self.rows[0]["answeredByPurpose"] = {}
        self.assert_bad(self.check(), "answeredLines")

    def test_log_must_stay_inside_capture_and_not_be_a_discarded_attempt(self):
        for path in ("../outside.jsonl", str(self.root.parent / "outside.jsonl"),
                     "jev-run/discarded/jev-shift-00-attempt-0.jsonl"):
            with self.subTest(path=path):
                self.rows[0]["jsonl"] = path
                self.assert_bad(self.check(), "jsonl")

    def test_invalid_summary_shapes_and_nonfinite_json_fail(self):
        for text in ("[]", "{}", "null", "[null]", "[", '[{"shift": NaN}]'):
            with self.subTest(text=text):
                self.summary.write_text(text, encoding="utf-8")
                self.assertFalse(validate(self.summary, 1)["ok"])
        self.summary.unlink()
        self.assert_bad(validate(self.summary, 1), "summary")

    def test_cli_verdict_and_exit_code_agree_for_success_and_failure(self):
        script = Path(__file__).with_name("validate_capture.py")
        for unanswered, expected in ((0, 0), (377, 1)):
            with self.subTest(unanswered=unanswered):
                self.rows[0]["director"]["unanswered_rounds"] = unanswered
                self.check()
                result = subprocess.run([sys.executable, str(script), str(self.summary),
                                         "--expected-shifts", "1"], capture_output=True, text=True)
                self.assertEqual(result.returncode, expected, result.stderr)
                self.assertEqual(json.loads(result.stdout)["ok"], expected == 0)


if __name__ == "__main__":
    unittest.main()
