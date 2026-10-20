"""Unity test verdicts: the results XML and the editor exit code must agree."""
import os
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
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

    def focused_xml(self, fullname=unity_results.FOCUSED_ROUTE_TEST, case_result="Passed", **counters):
        attrs = dict(total="1", passed="1", failed="0", skipped="0", inconclusive="0", duration="3", result="Passed")
        attrs.update({key: str(value) for key, value in counters.items()})
        root = ET.Element("test-run", attrs)
        ET.SubElement(root, "test-case", fullname=fullname, result=case_result)
        return self.xml(ET.tostring(root, encoding="unicode"))

    def test_focused_route_accepts_only_one_exact_passed_test(self):
        result = unity_results.evaluate("RouteGraph", self.focused_xml(), "0")
        self.assertEqual((result.total, result.passed, result.errors, result.warnings), (1, 1, [], []))

    def test_focused_route_rejects_wrong_fullname_even_when_aggregate_passes(self):
        for name in ("", "Other.BakedGraph", unity_results.FOCUSED_ROUTE_TEST + "Extra"):
            with self.subTest(fullname=name):
                errors = unity_results.evaluate("RouteGraph", self.focused_xml(fullname=name), "0").errors
                self.assertTrue(any("fullname mismatch" in error for error in errors))

    def test_focused_route_requires_consistent_exact_single_pass_counters(self):
        for counters in (dict(total=0, passed=0), dict(total=2, passed=2), dict(skipped=1),
                         dict(inconclusive=1), dict(failed=1, passed=0)):
            with self.subTest(counters=counters):
                errors = unity_results.evaluate("RouteGraph", self.focused_xml(**counters), "0").errors
                self.assertTrue(any("exactly one passed test" in error for error in errors))

    def test_focused_route_checks_case_records_in_addition_to_aggregate_counts(self):
        for count in (0, 2):
            with self.subTest(case_records=count):
                root = ET.Element("test-run", total="1", passed="1", failed="0", result="Passed")
                for _ in range(count):
                    ET.SubElement(root, "test-case", fullname=unity_results.FOCUSED_ROUTE_TEST, result="Passed")
                errors = unity_results.evaluate("RouteGraph", self.xml(ET.tostring(root, encoding="unicode")), "0").errors
                self.assertTrue(any("test-case records" in error for error in errors))

    def test_focused_route_rejects_skipped_inconclusive_failed_case_or_nonpassed_root(self):
        for case_result in ("Skipped", "Inconclusive", "Failed"):
            with self.subTest(case_result=case_result):
                errors = unity_results.evaluate("RouteGraph", self.focused_xml(case_result=case_result), "0").errors
                self.assertIn("focused RouteGraph test-case result must be Passed", errors)
        errors = unity_results.evaluate("RouteGraph", self.focused_xml(result="Failed"), "0").errors
        self.assertIn("focused RouteGraph test-run result must be Passed", errors)

    def test_focused_route_preserves_missing_results_and_editor_exit_guards(self):
        self.assertTrue(unity_results.evaluate("RouteGraph", str(Path(self.dir.name) / "absent.xml"), "0").errors)
        for code in ("", "2", "134"):
            with self.subTest(exit_code=code):
                self.assertTrue(unity_results.evaluate("RouteGraph", self.focused_xml(), code).errors)
        self.assertEqual(unity_results.parse_spec(r"RouteGraph:D:\a\_temp/unity/RouteGraph-results.xml:0"),
                         ("RouteGraph", r"D:\a\_temp/unity/RouteGraph-results.xml", "0"))

    def test_focused_cli_keeps_korean_mismatch_readable_under_windows_legacy_encoding(self):
        xml = self.focused_xml(fullname="다른시험.잘못된필터")
        summary = Path(self.dir.name) / "focused-summary.md"
        env = {**os.environ, "PYTHONIOENCODING": "cp1252", "GITHUB_STEP_SUMMARY": str(summary)}
        result = subprocess.run([sys.executable, str(Path(unity_results.__file__)), f"RouteGraph:{xml}:0"],
                                env=env, capture_output=True, check=False)
        self.assertEqual(result.returncode, 1)
        self.assertIn("다른시험.잘못된필터", result.stdout.decode("utf-8"))
        self.assertNotIn(b"UnicodeEncodeError", result.stderr)
        self.assertIn("다른시험.잘못된필터", summary.read_text(encoding="utf-8"))

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

    def test_cli_reports_korean_failure_under_a_windows_legacy_output_encoding(self):
        xml = self.xml(FAILING.replace("Closes", "출입문닫기").replace("Expected: True", "예상: 참"))
        summary = Path(self.dir.name) / "summary.md"
        env = {**os.environ, "PYTHONIOENCODING": "cp1252", "GITHUB_STEP_SUMMARY": str(summary)}
        result = subprocess.run(
            [sys.executable, str(Path(unity_results.__file__)), f"PlayMode:{xml}:2"],
            env=env, capture_output=True, check=False,
        )
        self.assertEqual(result.returncode, 1)
        output = result.stdout.decode("utf-8")
        self.assertIn("ChooGuard.Doors.출입문닫기", output)
        self.assertIn("예상: 참", output)
        self.assertIn("1 test(s) failed", output)
        self.assertNotIn(b"UnicodeEncodeError", result.stderr)
        self.assertIn("출입문닫기", summary.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
