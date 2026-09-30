"""Unity test verdicts: the results XML and the editor exit code must agree."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import unity_results  # noqa: E402

PASSING = """<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" testcasecount="3" result="Passed" total="3" passed="2" failed="0" inconclusive="0" skipped="1" duration="42.5">
  <test-suite type="TestSuite" name="ChooGuard" total="3" passed="2" failed="0">
    <test-case name="A" fullname="ChooGuard.T.A" result="Passed"/>
    <test-case name="B" fullname="ChooGuard.T.B" result="Passed"/>
    <test-case name="C" fullname="ChooGuard.T.C" result="Skipped" label="Ignored"/>
  </test-suite>
</test-run>"""

FAILING = """<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" total="2" passed="1" failed="1" inconclusive="1" skipped="0" duration="7">
  <test-suite type="TestFixture" name="Doors">
    <test-case name="Opens" fullname="ChooGuard.Doors.Opens" result="Passed"/>
    <test-case name="Closes" fullname="ChooGuard.Doors.Closes" result="Failed">
      <failure><message><![CDATA[  Expected: True
  But was:  False
]]></message><stack-trace><![CDATA[at ChooGuard.Doors.Closes () [0x0001] in Doors.cs:12]]></stack-trace></failure>
    </test-case>
  </test-suite>
</test-run>"""

EMPTY = '<test-run id="2" total="0" passed="0" failed="0" skipped="0" duration="0"/>'


class VerdictTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)

    def xml(self, text):
        path = Path(self.dir.name) / f"r{len(list(Path(self.dir.name).iterdir()))}.xml"
        path.write_text(text, encoding="utf-8")
        return str(path)

    def test_all_passed_with_exit_zero(self):
        r = unity_results.evaluate("EditMode", self.xml(PASSING), "0")
        self.assertEqual((r.total, r.passed, r.skipped, r.errors), (3, 2, 1, []))

    def test_failures_are_listed_with_their_message(self):
        r = unity_results.evaluate("PlayMode", self.xml(FAILING), "2")
        self.assertEqual(r.errors, ["1 test(s) failed"])
        self.assertEqual(r.failures[0][0], "ChooGuard.Doors.Closes")
        self.assertTrue(r.failures[0][1].startswith("Expected: True"))
        self.assertEqual(r.warnings, ["1 inconclusive test(s)"])

    def test_missing_results_fail_even_when_the_editor_exit_looks_clean(self):
        r = unity_results.evaluate("EditMode", str(Path(self.dir.name) / "absent.xml"), "0")
        self.assertEqual(len(r.errors), 1)
        self.assertIn("no results file", r.errors[0])

    def test_zero_tests_fail(self):
        self.assertIn("0 tests ran (filtered out, or the test assemblies did not load)", unity_results.evaluate("EditMode", self.xml(EMPTY), "0").errors)

    def test_exit_code_must_agree_with_the_results(self):
        crashed_after_passing = unity_results.evaluate("EditMode", self.xml(PASSING), "134")
        self.assertIn("editor exit code 134 disagrees with the results (0 failed)", crashed_after_passing.errors)
        failed_but_exit_zero = unity_results.evaluate("PlayMode", self.xml(FAILING), "0")
        self.assertIn("editor exit code 0 disagrees with the results (1 failed)", failed_but_exit_zero.errors)
        not_run = unity_results.evaluate("PlayMode", self.xml(PASSING), "")
        self.assertIn("editor exit code unknown (step did not run?)", not_run.errors)

    def test_main_exit_status_and_summary(self):
        good, bad = self.xml(PASSING), self.xml(FAILING)
        self.assertEqual(unity_results.main([f"EditMode:{good}:0"]), 0)
        self.assertEqual(unity_results.main([f"EditMode:{good}:0", f"PlayMode:{bad}:2"]), 1)
        text = unity_results.render([unity_results.evaluate("PlayMode", bad, "2")])
        self.assertIn("| PlayMode | 2 | 1 | 1 | 0 | 1 |", text)
        self.assertIn("**ChooGuard.Doors.Closes** — Expected: True", text)

    def test_spec_keeps_a_windows_drive_letter_in_the_results_path(self):
        # Windows runners pass RUNNER_TEMP as D:\a\_temp; splitting at every colon lost the results file (run 36396147103).
        self.assertEqual(unity_results.parse_spec(r"EditMode:D:\a\_temp/unity/EditMode-results.xml:0"),
                         ("EditMode", r"D:\a\_temp/unity/EditMode-results.xml", "0"))
        self.assertEqual(unity_results.parse_spec("PlayMode:/tmp/unity/PlayMode-results.xml:"),
                         ("PlayMode", "/tmp/unity/PlayMode-results.xml", ""))


if __name__ == "__main__":
    unittest.main()
