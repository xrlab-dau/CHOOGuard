"""Player smoke verdicts from the soak report."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import soak_verdict  # noqa: E402


def run(index=0, errors=0, exceptions=0, people=40, first=None):
    return {"index": index, "errors": errors, "exceptions": exceptions, "people": people, "firstError": first, "loadSeconds": 12.5, "maxFrameMs": 80}


class SoakVerdictTests(unittest.TestCase):
    def test_clean_run_passes(self):
        self.assertEqual(soak_verdict.evaluate({"runs": [run()]}, 1), ([], []))

    def test_missing_report_is_a_crash(self):
        errors, _ = soak_verdict.evaluate(None, 1, "139")
        self.assertEqual(len(errors), 1)
        self.assertIn("player exit 139", errors[0])

    def test_missing_shift_exception_and_empty_session_fail(self):
        errors, _ = soak_verdict.evaluate({"runs": [run(exceptions=2, first="NullReferenceException @ Crowd.Spawn"), run(1, people=0)]}, 3)
        self.assertEqual(errors, ["2 of 3 shift(s) completed",
                                  "shift 0: 2 exception(s), first: NullReferenceException @ Crowd.Spawn",
                                  "shift 1: the emergency session never spawned its crowd"])

    def test_logged_errors_only_warn(self):
        errors, warnings = soak_verdict.evaluate({"runs": [run(errors=3, first="Shader not supported")]}, 1)
        self.assertEqual(errors, [])
        self.assertEqual(warnings, ["shift 0: 3 logged error(s), first: Shader not supported"])

    def test_non_zero_exit_after_report_fails(self):
        errors, _ = soak_verdict.evaluate({"runs": [run()]}, 1, "3")
        self.assertEqual(errors, ["player exited 3 after writing the report"])


if __name__ == "__main__":
    unittest.main()
