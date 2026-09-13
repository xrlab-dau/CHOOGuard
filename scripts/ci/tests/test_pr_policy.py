import contextlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

MODULE_PATH = Path(__file__).resolve().parents[1] / "pr_policy.py"
SPEC = importlib.util.spec_from_file_location("pr_policy", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
SPEC.loader.exec_module(MODULE)


class PullRequestPolicyPatternsTest(unittest.TestCase):
    def test_develop_branch_patterns(self):
        self.assertTrue(MODULE.DEVELOP_SOURCES.match("feature/12-xr-rig"))
        self.assertTrue(MODULE.DEVELOP_SOURCES.match("experiment/da3-ply"))
        self.assertTrue(MODULE.DEVELOP_SOURCES.match("dependabot/pip/reconstruction/ruff-1.0"))
        self.assertFalse(MODULE.DEVELOP_SOURCES.match("release/0.1.0"))

    def test_main_branch_patterns(self):
        self.assertTrue(MODULE.MAIN_SOURCES.match("release/0.1.0"))
        self.assertTrue(MODULE.MAIN_SOURCES.match("hotfix/v0.1.1"))
        self.assertFalse(MODULE.MAIN_SOURCES.match("feature/foo"))

    def test_conventional_title(self):
        self.assertTrue(MODULE.TITLE.match("feat(xr): add emergency interaction"))
        self.assertFalse(MODULE.TITLE.match("updated stuff"))


COMPLIANT_BODY = (
    "## Summary\nwhat changed\n\nRefs #1\n\n"
    "## Verification\nran tests\n\n## Data and safety\nnone\n\n## Evidence\nlogs\n"
)


class GateExitContractTest(unittest.TestCase):
    """Covers the 0/1/2/3 exit contract, none of which had any coverage."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def event(self, **overrides):
        pull_request = {
            "head": {"ref": "feature/12-xr-rig"},
            "base": {"ref": "develop"},
            "title": "feat(xr): add teleport validation",
            "body": COMPLIANT_BODY,
        }
        pull_request.update(overrides.pop("pull_request", {}))
        payload = {"pull_request": pull_request, "sender": {"login": "someone"}}
        payload.update(overrides)
        path = self.root / f"event-{len(list(self.root.iterdir()))}.json"
        path.write_text(json.dumps(payload), encoding="utf-8")
        return path

    def run_gate(self, event_path, report=None):
        argv = ["pr_policy.py", "--event", str(event_path)]
        if report is not None:
            argv += ["--report", str(report)]
        stdout = io.StringIO()
        with patch.object(sys, "argv", argv), contextlib.redirect_stdout(stdout):
            status = MODULE.main()
        return status, stdout.getvalue()

    def write_raw(self, text):
        path = self.root / "raw.json"
        path.write_text(text, encoding="utf-8")
        return path

    # --- 0 and 1 -----------------------------------------------------------

    def test_compliant_pull_request_exits_zero_and_writes_report(self):
        report = self.root / "report.md"
        status, out = self.run_gate(self.event(), report)
        self.assertEqual(status, 0)
        self.assertIn("- Violations: `0`", report.read_text(encoding="utf-8"))
        self.assertIn("- Violations: `0`", out)

    def test_bad_branch_and_title_exit_one_with_named_violations(self):
        report = self.root / "report.md"
        event = self.event(pull_request={"head": {"ref": "nope"}, "title": "updated stuff"})
        status, _ = self.run_gate(event, report)
        text = report.read_text(encoding="utf-8")
        self.assertEqual(status, 1)
        self.assertIn("cannot target `develop`", text)
        self.assertIn("Conventional Commits", text)

    def test_dependabot_is_exempt_from_issue_and_template_requirements(self):
        event = self.event(
            pull_request={"head": {"ref": "dependabot/github_actions/actions/cache-6.1.0"},
                          "title": "chore(deps): bump actions/cache from 4.3.0 to 6.1.0",
                          "body": "plain dependabot body"},
            sender={"login": "dependabot[bot]"},
        )
        status, out = self.run_gate(event)
        self.assertEqual(status, 0, msg=out)

    def test_non_dependabot_missing_template_headings_is_a_violation(self):
        event = self.event(pull_request={"body": "Refs #1 only"})
        status, out = self.run_gate(event)
        self.assertEqual(status, 1)
        for heading in ("## Summary", "## Verification", "## Data and safety", "## Evidence"):
            self.assertIn(f"missing `{heading}`", out)

    # --- 2: report write failure ------------------------------------------

    def test_unwritable_report_path_exits_two_and_still_prints_the_report(self):
        # Parent directory does not exist: write_text raises FileNotFoundError
        # (an OSError) *after* the metadata check already succeeded.
        status, out = self.run_gate(self.event(), self.root / "missing-dir" / "report.md")
        self.assertEqual(status, 2, msg="a report-write failure must not be reported as 1 (violations)")
        self.assertIn("::error::failed to write PR policy report", out)
        self.assertIn("# PR policy report", out, msg="the computed report must survive on stdout")

    def test_report_path_that_is_a_directory_exits_two(self):
        directory = self.root / "report-dir"
        directory.mkdir()
        status, out = self.run_gate(self.event(), directory)
        self.assertEqual(status, 2)
        self.assertIn("::error::failed to write PR policy report", out)

    def test_report_write_failure_on_a_violating_pr_still_exits_two(self):
        # 2 must win over 1: the operator cannot trust a "violations found"
        # signal when the report naming those violations was never written.
        event = self.event(pull_request={"title": "updated stuff"})
        status, _ = self.run_gate(event, self.root / "missing-dir" / "report.md")
        self.assertEqual(status, 2)

    # --- 3: cannot evaluate ------------------------------------------------

    def assert_anticipated(self, out, fragment):
        """An anticipated input failure must be named, not reported as a crash.

        Without this, routing EventPayloadError through the generic handler
        would still echo the message and the assertion could not tell the two
        paths apart.
        """
        self.assertIn("::error::", out)
        self.assertIn(fragment, out)
        self.assertIn("could not evaluate the pull request", out)
        self.assertNotIn("crashed unexpectedly", out)

    def test_missing_event_file_exits_three(self):
        status, out = self.run_gate(self.root / "does-not-exist.json")
        self.assertEqual(status, 3)
        self.assert_anticipated(out, "cannot read event payload")

    def test_malformed_json_exits_three(self):
        status, out = self.run_gate(self.write_raw("{not json"))
        self.assertEqual(status, 3)
        self.assert_anticipated(out, "not valid JSON")

    def test_json_that_is_not_an_object_exits_three(self):
        status, out = self.run_gate(self.write_raw("[1, 2, 3]"))
        self.assertEqual(status, 3)
        self.assert_anticipated(out, "not a JSON object")

    def test_payload_without_pull_request_exits_three(self):
        status, out = self.run_gate(self.write_raw(json.dumps({"sender": {"login": "x"}})))
        self.assertEqual(status, 3)
        self.assert_anticipated(out, "no `pull_request` object")

    def test_pull_request_without_base_ref_exits_three(self):
        raw = json.dumps({"pull_request": {"head": {"ref": "feature/x"}, "title": "feat(x): y"}})
        status, out = self.run_gate(self.write_raw(raw))
        self.assertEqual(status, 3)
        self.assert_anticipated(out, "`pull_request.base.ref`")

    def test_no_report_flag_still_returns_the_check_result(self):
        # The plain `--event` invocation had no coverage at all.
        status, out = self.run_gate(self.event())
        self.assertEqual(status, 0)
        self.assertIn("# PR policy report", out)

    def test_unexpected_crash_exits_three_not_one(self):
        # stderr carries the deliberate traceback; keep it out of the test log.
        with patch.object(MODULE, "load_pull_request", side_effect=RuntimeError("boom")), \
                contextlib.redirect_stderr(io.StringIO()):
            status, out = self.run_gate(self.event())
        self.assertEqual(status, 3)
        self.assertIn("crashed unexpectedly", out)
        self.assertIn("RuntimeError", out)

    # --- null-valued fields ------------------------------------------------

    def test_null_sender_does_not_crash(self):
        # `"sender": null` is valid JSON; payload.get("sender", {}) would hand
        # None straight through to .get("login").
        raw = json.dumps({
            "pull_request": {"head": {"ref": "feature/x"}, "base": {"ref": "develop"},
                             "title": "feat(x): valid title here", "body": COMPLIANT_BODY},
            "sender": None,
        })
        status, out = self.run_gate(self.write_raw(raw))
        self.assertEqual(status, 0, msg=out)

    def test_null_title_is_a_violation_not_a_crash(self):
        event = self.event(pull_request={"title": None})
        status, out = self.run_gate(event)
        self.assertEqual(status, 1, msg=out)
        self.assertIn("Conventional Commits", out)
        self.assertNotIn("crashed unexpectedly", out)

    def test_dependabot_head_ref_is_exempt_even_when_sender_is_null(self):
        raw = json.dumps({
            "pull_request": {"head": {"ref": "dependabot/github_actions/actions/cache-6.1.0"},
                             "base": {"ref": "develop"},
                             "title": "chore(deps): bump actions/cache from 4.3.0 to 6.1.0",
                             "body": "no template"},
            "sender": None,
        })
        status, out = self.run_gate(self.write_raw(raw))
        self.assertEqual(status, 0, msg=out)


class ExitContractDocumentationTest(unittest.TestCase):
    """The contract is only useful if it stays documented next to the code."""

    def test_exit_codes_are_documented_in_source(self):
        source = MODULE_PATH.read_text(encoding="utf-8")
        for fragment in ("0 = metadata checked, no violations",
                         "2 = metadata checked but the report could not be written",
                         "3 = the check could not be evaluated",
                         "Fail-closed, unlike repository_policy.py"):
            self.assertIn(fragment, source)


if __name__ == "__main__":
    unittest.main()
