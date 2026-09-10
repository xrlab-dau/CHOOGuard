import contextlib
import importlib.util
import io
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

MODULE_PATH = Path(__file__).resolve().parents[1] / "repository_policy.py"
SPEC = importlib.util.spec_from_file_location("repository_policy", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
SPEC.loader.exec_module(MODULE)


class PolicyPatternsTest(unittest.TestCase):
    def test_action_pin_requires_full_sha(self):
        self.assertTrue(MODULE.PINNED_ACTION.match("actions/checkout@" + "a" * 40))
        self.assertFalse(MODULE.PINNED_ACTION.match("actions/checkout@v4"))

    def test_secret_patterns(self):
        self.assertTrue(MODULE.SECRET_PATTERNS["GitHub token"].search("ghp_" + "a" * 30))
        self.assertTrue(MODULE.SECRET_PATTERNS["AWS access key"].search("AKIA" + "A" * 16))
        self.assertFalse(MODULE.SECRET_PATTERNS["private key"].search("public documentation"))


class GitPathPolicyTest(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.git("init", "-q")
        (self.root / "schemas").mkdir()
        (self.root / "schemas/scene-bundle.schema.json").write_text("{}", encoding="utf-8")
        self.git("add", ".")
        self.commit("base")
        self.base = self.git("rev-parse", "HEAD").strip()
        self.names = ["docs/한글 안내.md", "docs/비밀 키.pem"]
        (self.root / "docs").mkdir()
        for name in self.names:
            (self.root / name).write_text("fixture", encoding="utf-8")
        self.git("add", ".")
        self.commit("unicode paths")
        self.root_patch = patch.object(MODULE, "ROOT", self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)

    def git(self, *args):
        return subprocess.check_output(
            ["git", "-C", str(self.root), *args], encoding="utf-8", stderr=subprocess.PIPE
        )

    def commit(self, message):
        self.git("-c", "user.name=Policy test", "-c", "user.email=policy@example.invalid",
                 "-c", "commit.gpgsign=false", "commit", "-qm", message)

    def test_tracked_unicode_paths_under_korean_windows_encoding(self):
        # Real Git emits UTF-8 paths even when Python defaults to Windows CP949.
        with patch.object(subprocess, "_text_encoding", return_value="cp949"):
            files = MODULE.tracked_files()
            errors = MODULE.inspect(files)
        self.assertTrue(all(self.root / name in files for name in self.names))
        self.assertIn("forbidden credential/model file: docs/비밀 키.pem", errors)

    def test_changed_unicode_paths_are_inspected_without_git_quoting(self):
        with patch.object(subprocess, "_text_encoding", return_value="cp949"):
            files = MODULE.changed_files(self.base, "HEAD")
        self.assertEqual(set(files), {self.root / name for name in self.names})
        self.assertIn("forbidden credential/model file: docs/비밀 키.pem", MODULE.inspect(files))

    def test_normal_path_reports_changed_files_scope_honestly(self):
        # Rubric item 2: the report must never claim it inspected one scope
        # while having actually inspected another. On the ordinary, healthy
        # path (both revisions resolve, the diff itself succeeds), the report
        # must say "changed files in `BASE...HEAD`", never "all tracked
        # files" -- claiming a full scan happened when only the diff subset
        # was actually walked would be the silent-scope-lie this rubric item
        # is about, just in the opposite (over-claiming) direction from the
        # degraded-fallback case.
        report = self.root / "report-normal.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--base", self.base,
                                       "--head", "HEAD", "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured:
            status = MODULE.main()
        text = report.read_text(encoding="utf-8")
        self.assertEqual(status, 1)
        self.assertIn(f"Scope: changed files in `{self.base}...HEAD`", text)
        self.assertNotIn("Scope: all tracked files", text)
        # The scope was not degraded, so there must be no Notes section and
        # no fallback warning annotation on the healthy path.
        self.assertNotIn("## Notes", text)
        self.assertNotIn("::warning::", captured.getvalue())

    def test_no_base_argument_scans_all_tracked_files(self):
        # When invoked without --base at all (e.g. a plain push trigger with
        # no PR to diff against), main() must fall onto the `else:` branch
        # and actually walk `tracked_files()` -- not silently return an empty
        # file list, which would report a spuriously clean "Violations: 0"
        # for a repository that was never actually scanned.
        report = self.root / "report-no-base.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()):
            status = MODULE.main()
        text = report.read_text(encoding="utf-8")
        self.assertEqual(status, 1)
        self.assertIn("Scope: all tracked files", text)
        self.assertIn("Checked files:", text)
        checked = int(text.splitlines()[3].split(": ", 1)[1])
        self.assertGreater(checked, 0)
        self.assertIn("forbidden credential/model file: docs/비밀 키.pem", text)

    def test_absent_head_commit_is_reported_as_missing(self):
        # A squash-merged pull request leaves its head commit unreachable.
        self.assertEqual(MODULE.missing_commits(self.base, "HEAD"), [])
        absent = "0" * 40
        self.assertEqual(MODULE.missing_commits(self.base, absent), [absent])

    def test_unreachable_head_falls_back_to_tracked_files_without_crashing(self):
        absent = "0" * 40
        report = self.root / "report.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--base", self.base,
                                       "--head", absent, "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured:
            status = MODULE.main()
        text = report.read_text(encoding="utf-8")
        self.assertEqual(status, 1)
        self.assertIn("changed-file scope unavailable", text)
        self.assertIn("Scope: all tracked files", text)
        # The fallback still inspects the repository rather than reporting nothing.
        self.assertIn("forbidden credential/model file: docs/비밀 키.pem", text)
        # D2: the report must say which working tree the full scan actually
        # covered, since it may not be the PR head (e.g. checkout landed on
        # the base branch instead).
        actual_head = self.git("rev-parse", "HEAD").strip()
        self.assertIn(f"working tree currently checked out (`{actual_head}`)", text)
        # D3: real violations must render under their own heading, never as a
        # visual continuation of the advisory ## Notes bullet list.
        self.assertIn("## Violations", text)
        notes_index = text.index("## Notes")
        violations_index = text.index("## Violations")
        pem_index = text.index("forbidden credential/model file: docs/비밀 키.pem")
        self.assertLess(notes_index, violations_index)
        self.assertLess(violations_index, pem_index)
        # D4: a degraded/fallback scan must emit a visible GitHub Actions
        # annotation, not just prose buried in a report nobody opens.
        self.assertIn("::warning::", captured.getvalue())

    def test_fallback_with_no_violations_reports_success(self):
        # The PR's own success criterion: run 34438757897 crashed a *clean*
        # repository. A clean tree plus an unreachable head must exit 0, not
        # just "not crash". A mutant that hardcodes the fallback path to
        # return 1 regardless of `errors` would pass every other test but
        # must fail this one.
        clean_root = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, clean_root, ignore_errors=True)
        run = lambda *args: subprocess.check_output(
            ["git", "-C", str(clean_root), *args], encoding="utf-8", stderr=subprocess.PIPE
        )
        run("init", "-q")
        (clean_root / "schemas").mkdir()
        (clean_root / "schemas/scene-bundle.schema.json").write_text("{}", encoding="utf-8")
        run("add", ".")
        run("-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false",
            "commit", "-qm", "init")
        base = run("rev-parse", "HEAD").strip()
        absent = "0" * 40
        report = clean_root / "report.md"
        with patch.object(MODULE, "ROOT", clean_root), \
                patch.object(sys, "argv", ["repository_policy.py", "--base", base,
                                           "--head", absent, "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()):
            status = MODULE.main()
        text = report.read_text(encoding="utf-8")
        self.assertEqual(status, 0)
        self.assertIn("Violations: 0", text)
        # No violations exist, so no ## Violations heading should appear.
        self.assertNotIn("## Violations", text)

    def test_resolvable_but_undiffable_revisions_fall_back_cleanly(self):
        # D1/D8: two revisions can each individually resolve to a commit
        # (missing_commits() passes both) while still lacking a common merge
        # base, so `git diff base...head` itself raises. This is the same
        # failure signature (uncaught CalledProcessError, exit 128, no report
        # ever written) as the incident this PR exists to fix, just reached
        # through a different trigger than a fully-unresolvable SHA.
        original_branch = self.git("branch", "--show-current").strip()
        self.git("checkout", "-q", "--orphan", "unrelated")
        self.commit("unrelated root")
        unrelated_head = self.git("rev-parse", "HEAD").strip()
        self.git("checkout", "-q", original_branch)

        # Both revisions must resolve individually (missing_commits() would
        # otherwise trivially catch this the old way).
        self.assertEqual(MODULE.missing_commits(self.base, unrelated_head), [])

        report = self.root / "report-undiffable.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--base", self.base,
                                       "--head", unrelated_head, "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()):
            status = MODULE.main()  # must not raise CalledProcessError
        self.assertIn(status, (0, 1))
        text = report.read_text(encoding="utf-8")
        self.assertIn("Scope: all tracked files", text)
        self.assertIn("diff failed", text)

    def test_fail_open_policy_decision_is_documented(self):
        # Rubric item 5 / work-order item 3: fail-open-vs-fail-closed for a
        # security gate must be a recorded decision, not an implicit default
        # someone can delete without noticing. This does not re-derive the
        # behaviour from a mutant (the other tests already pin exit-code
        # behaviour); it only guards against the documentation silently
        # disappearing from the source.
        source = Path(MODULE.__file__).read_text(encoding="utf-8")
        self.assertIn("Fail-open policy decision", source)
        self.assertIn("strict-on-fallback", source)


if __name__ == "__main__":
    unittest.main()
