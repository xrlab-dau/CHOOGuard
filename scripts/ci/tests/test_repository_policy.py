import argparse
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

    def test_empty_string_base_is_treated_as_degraded_not_as_no_base(self):
        # BD-2 (round-3 verdict): `--base ""` must NOT be silently treated
        # the same as omitting `--base` entirely. `if args.base:` used to be
        # falsy for both `None` (flag omitted) and `""` (flag given but
        # empty), collapsing an abnormal case -- a CI context variable that
        # resolved to an empty string, e.g. `${{ github.event.pull_request
        # .base.sha }}` evaluating empty -- into the same silent, unwarned
        # full-tree scan as the perfectly normal push-event path. Every other
        # degraded case in this function (unresolvable revision, no merge
        # base) gets a `::warning::` and a `## Notes` entry; this one must
        # too, or it is the one silent gap left in an otherwise loud system.
        report = self.root / "report-empty-base.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--base", "",
                                       "--head", "HEAD", "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured:
            status = MODULE.main()
        text = report.read_text(encoding="utf-8")
        self.assertEqual(status, 1)
        self.assertIn("Scope: all tracked files", text)
        self.assertIn("## Notes", text)
        self.assertIn("empty", text.lower())
        self.assertIn("::warning::", captured.getvalue())
        # Still must actually scan something, not report a spuriously clean
        # tree just because the base was garbage.
        self.assertIn("forbidden credential/model file: docs/비밀 키.pem", text)

    def test_no_base_and_empty_base_are_distinguishable_in_the_report(self):
        # Guards against a regression that satisfies the previous test in
        # isolation but re-merges the two code paths so their *output* is
        # identical again (e.g. by making the omitted-base branch also emit
        # a warning). The two must remain observably different: omitting
        # `--base` is the ordinary push-event path (no warning, no Notes);
        # supplying an empty `--base` is a degraded/unexpected path (warning
        # + Notes). If a future change collapses these back together in
        # either direction, this test must fail.
        no_base_report = self.root / "report-nb.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--report", str(no_base_report)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured_nb:
            MODULE.main()
        empty_base_report = self.root / "report-eb.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--base", "",
                                       "--report", str(empty_base_report)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured_eb:
            MODULE.main()
        self.assertNotIn("::warning::", captured_nb.getvalue())
        self.assertIn("::warning::", captured_eb.getvalue())
        self.assertNotIn("## Notes", no_base_report.read_text(encoding="utf-8"))
        self.assertIn("## Notes", empty_base_report.read_text(encoding="utf-8"))

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
        # The fixture's working tree (self.root, checked out back on
        # `original_branch`) deterministically contains docs/비밀 키.pem, a
        # forbidden credential file, so the fallback full scan must always
        # find it and exit 1 -- not merely "0 or 1", which would pass even if
        # the fallback silently scanned nothing.
        self.assertEqual(status, 1)
        text = report.read_text(encoding="utf-8")
        self.assertIn("Scope: all tracked files", text)
        self.assertIn("diff failed", text)
        self.assertIn("forbidden credential/model file: docs/비밀 키.pem", text)

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
        # Tie the prose promise to the actual CLI surface so the two cannot
        # silently drift apart: the comment promises that switching to
        # fail-closed would be done via a new --strict-on-fallback flag, not
        # that the flag already exists. If a `--strict-on-fallback` flag is
        # ever added to argparse without updating/removing this note (or vice
        # versa), this assertion is the tripwire.
        help_text = self._parser_help()
        self.assertNotIn("--strict-on-fallback", help_text)

    def _parser_help(self) -> str:
        with patch.object(sys, "argv", ["repository_policy.py", "--help"]), \
                contextlib.redirect_stdout(io.StringIO()) as captured:
            with self.assertRaises(SystemExit):
                MODULE.main()
        return captured.getvalue()

    def test_missing_commits_probe_suppresses_stderr(self):
        # Regression guard: the resolvability probe in missing_commits() must
        # run with stderr=subprocess.DEVNULL, otherwise git's raw
        # "fatal: ... unknown revision" (and, on a shallow/promisor clone,
        # fetch-attempt chatter) leaks straight into the raw CI log instead of
        # being cleanly summarised in the report. Wrap subprocess.run so every
        # call made while resolving revisions is inspected directly, rather
        # than only checking behaviour indirectly.
        calls = []
        real_run = subprocess.run

        def spy(*args, **kwargs):
            calls.append(kwargs)
            return real_run(*args, **kwargs)

        with patch.object(subprocess, "run", side_effect=spy):
            MODULE.missing_commits(self.base, "0" * 40)

        self.assertTrue(calls, "missing_commits() made no subprocess.run calls to inspect")
        for kwargs in calls:
            self.assertEqual(kwargs.get("stderr"), subprocess.DEVNULL)

    def test_report_write_failure_does_not_crash_after_successful_scan(self):
        # Work order (a): a scan that already completed must never die on the
        # final write step. Both a missing parent directory and a --report
        # path that is itself a directory must produce a clean, non-zero,
        # non-crashing status -- not an uncaught OSError subclass -- and the
        # already-computed report text must still reach stdout so the run is
        # not a total loss even though the file on disk was never written.
        missing_parent = self.root / "no_such_parent_dir_xyz" / "report.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--report", str(missing_parent)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured:
            status = MODULE.main()
        self.assertIsInstance(status, int)
        self.assertNotEqual(status, 0)
        self.assertFalse(missing_parent.exists())
        out = captured.getvalue()
        self.assertIn("# Repository policy report", out)
        self.assertIn("::error::", out)

        directory_target = self.root  # an existing directory, not a file
        with patch.object(sys, "argv", ["repository_policy.py", "--report", str(directory_target)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured2:
            status2 = MODULE.main()
        self.assertIsInstance(status2, int)
        self.assertNotEqual(status2, 0)
        out2 = captured2.getvalue()
        self.assertIn("# Repository policy report", out2)
        self.assertIn("::error::", out2)

    def test_report_write_failure_status_is_distinguishable_from_violations(self):
        # A report-write failure must not masquerade as "policy violations
        # found" (status 1 from the normal violations path): a human or a
        # future workflow step reading only the numeric exit code needs to be
        # able to tell an infra failure (couldn't write the report) apart
        # from a real, successfully-recorded policy violation.
        missing_parent = self.root / "another_missing_dir" / "report.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--report", str(missing_parent)]), \
                contextlib.redirect_stdout(io.StringIO()):
            write_failure_status = MODULE.main()

        ok_report = self.root / "ok-report.md"
        with patch.object(sys, "argv", ["repository_policy.py", "--base", self.base,
                                       "--head", "HEAD", "--report", str(ok_report)]), \
                contextlib.redirect_stdout(io.StringIO()):
            violations_status = MODULE.main()

        self.assertEqual(violations_status, 1)
        self.assertNotEqual(write_failure_status, violations_status)


class CrashSafetyTest(unittest.TestCase):
    """Round-4 BLOCKING item (b): an uncaught exception inside main()'s
    logic must never produce an exit code indistinguishable from "policy
    violations found" (1) or "clean" (0), and must never surface as a raw,
    unannotated traceback with no stdout signal a CI consumer could act on.
    """

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.root_patch = patch.object(MODULE, "ROOT", self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)

    def test_uncaught_exception_from_tracked_files_does_not_crash_the_process(self):
        # Reproduction of the exact scenario named in the work order:
        # tracked_files()'s underlying `git ls-files` call raises an
        # uncaught CalledProcessError. Before this fix this propagated all
        # the way out of main(), which -- run as `sys.exit(main())` from the
        # command line -- would print a raw Python traceback and exit with
        # whatever bare-exception default Python uses (1), identical to the
        # legitimate "violations found" exit code.
        def boom():
            raise subprocess.CalledProcessError(128, ["git", "ls-files", "-z"])

        report = self.root / "crash-report.md"
        with patch.object(MODULE, "tracked_files", side_effect=boom), \
                patch.object(sys, "argv", ["repository_policy.py", "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()) as captured:
            status = MODULE.main()  # must return, never raise
        self.assertIsInstance(status, int)
        self.assertIn("::error::", captured.getvalue())
        self.assertIn("crashed unexpectedly", captured.getvalue())

    def test_crash_exit_code_is_distinct_from_violations_and_write_failure_and_clean(self):
        # The crash path must have its own reserved exit code (3), not
        # collide with 0 (clean), 1 (violations found), or 2 (report write
        # failed) -- otherwise a caller reading only the numeric exit code
        # cannot tell an unanticipated crash apart from those three
        # deliberately-computed outcomes.
        def boom():
            raise RuntimeError("simulated unanticipated failure")

        report = self.root / "crash-report-2.md"
        with patch.object(MODULE, "tracked_files", side_effect=boom), \
                patch.object(sys, "argv", ["repository_policy.py", "--report", str(report)]), \
                contextlib.redirect_stdout(io.StringIO()):
            crash_status = MODULE.main()
        self.assertEqual(crash_status, 3)
        self.assertNotIn(crash_status, (0, 1, 2))

    def test_mutation_check_without_the_guard_the_crash_propagates_raw(self):
        # Self-check that this test class actually discriminates guarded vs.
        # unguarded behaviour: calling the pre-fix `_run()` directly (the
        # helper main() now wraps in try/except) must still raise, proving
        # the safety net lives specifically in main()'s wrapping, not
        # incidentally somewhere else that would mask a future regression
        # (e.g. someone reverting main() to call `_run()` directly without
        # the try/except).
        def boom():
            raise subprocess.CalledProcessError(128, ["git", "ls-files", "-z"])

        report = self.root / "crash-report-3.md"
        with patch.object(MODULE, "tracked_files", side_effect=boom):
            args = argparse.Namespace(base=None, head="HEAD", report=str(report))
            with self.assertRaises(subprocess.CalledProcessError):
                MODULE._run(args)


class ShellWrapperContractTest(unittest.TestCase):
    """Proves the *deployed* shell semantics in
    .github/workflows/required-quality-gate.yml, not just this module's
    in-process return value.

    Round-3's headline finding: GitHub Actions' default `run:` shell is
    `bash --noprofile --norc -eo pipefail`. The old workflow did
    `python ... || status=$?` then unconditionally `cat report.md >>
    "$GITHUB_STEP_SUMMARY"` then `exit "$status"`. When the report was never
    written (this script's own `return 2` path), `cat` itself fails under
    `errexit` and the shell exits on THAT failure, before `exit "$status"`
    ever runs -- so the job's real, GitHub-visible exit code silently
    collapsed to `cat`'s 1, indistinguishable from "policy violations
    found". A unit test that only calls MODULE.main() in-process cannot see
    this: it never spawns the real shell, so it cannot observe that the
    shell -- not the script -- decides the job's actual exit code.

    This test extracts the literal `run:` block out of the real workflow
    YAML (so it fails the moment the workflow drifts from what is verified
    here) and executes it with `bash -eo pipefail`, substituting a fake
    `python` on PATH that stands in for repository_policy.py's three
    possible outcomes (clean, violations, report-write failure). This
    isolates the shell-wrapper contract from the real script/repo state,
    matching exactly how the round-3 critic reproduced the bug.
    """

    WORKFLOW_PATH = Path(__file__).resolve().parents[3] / ".github/workflows/required-quality-gate.yml"

    _YAML_EXTRACT_SCRIPT = (
        "require 'yaml'\n"
        "data = YAML.safe_load(File.read(ARGV[0]), aliases: true)\n"
        "step = data['jobs']['policy']['steps'].find { |s| s['name'] == ARGV[1] }\n"
        "abort(\"step not found: #{ARGV[1]}\") unless step\n"
        "run = step['run']\n"
        "abort(\"step has no run: block: #{ARGV[1]}\") unless run\n"
        "STDOUT.write(run)\n"
    )

    @classmethod
    def _extract_run_block(cls, step_name: str) -> str:
        """Pull the literal `run:` block body out of the named step using a
        genuine YAML parser (Ruby's stdlib Psych, via `YAML.safe_load`),
        not a hand-rolled indentation/string scanner.

        This closes a tautology risk the round-4 critic flagged as a
        possibility: a line-oriented extractor is itself untested code that
        could silently diverge from what GitHub Actions' own YAML parser
        sees (block-scalar edge cases, `- name:` vs `name:` indentation
        levels, comments inside the block, anchors/aliases elsewhere in the
        file), and a test built on a broken extractor could pass while
        exercising the wrong text -- or fail to exist at all if extraction
        silently returns an empty string it doesn't notice. Routing through
        the same `ruby -e 'require "yaml"; ...'` machinery already used by
        the workflow's own "Validate YAML syntax" step ties this test to a
        parser that is independently known to load the file successfully.
        """
        result = subprocess.run(
            ["ruby", "-ryaml", "-e", cls._YAML_EXTRACT_SCRIPT, str(cls.WORKFLOW_PATH), step_name],
            capture_output=True,
            text=True,
            timeout=15,
        )
        assert result.returncode == 0, (
            f"ruby YAML extraction failed for step {step_name!r}: {result.stderr}"
        )
        body = result.stdout
        assert body.strip(), f"failed to extract a non-empty run: block for step {step_name!r}"
        return body

    def setUp(self):
        self.assertTrue(self.WORKFLOW_PATH.is_file(), self.WORKFLOW_PATH)
        self.tmp = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.bindir = self.tmp / "bin"
        self.bindir.mkdir()
        self.workdir = self.tmp / "work"
        self.workdir.mkdir()
        self.summary = self.tmp / "summary.txt"
        self.summary.write_text("", encoding="utf-8")

    def _install_fake_python(self, mode: str) -> None:
        # A stand-in for `python scripts/ci/repository_policy.py ...`: reads
        # a scenario from PYTHON_FAKE_MODE and reproduces exactly the
        # observable contract this script promises (report file written or
        # not, exit code), without depending on the real script or a real
        # git repository. Also honours `--report <path>` so a future editor
        # of the workflow that changes the report filename still exercises
        # the right file.
        fake = self.bindir / "python"
        fake.write_text(
            "#!/bin/bash\n"
            "set -e\n"
            "report=repository-policy-report.md\n"
            "args=(\"$@\")\n"
            "for ((i=0; i<${#args[@]}; i++)); do\n"
            "  if [[ \"${args[$i]}\" == \"--report\" ]]; then\n"
            "    report=\"${args[$((i+1))]}\"\n"
            "  fi\n"
            "done\n"
            "case \"$PYTHON_FAKE_MODE\" in\n"
            "  clean)\n"
            "    printf 'Scope: all tracked files\\nChecked files: 3\\nViolations: 0\\n' > \"$report\"\n"
            "    exit 0\n"
            "    ;;\n"
            "  violations)\n"
            "    printf 'Scope: all tracked files\\nChecked files: 3\\nViolations: 1\\n' > \"$report\"\n"
            "    exit 1\n"
            "    ;;\n"
            "  writefail)\n"
            "    echo '::error::failed to write repository policy report' >&2\n"
            "    exit 2\n"
            "    ;;\n"
            "  crash)\n"
            "    printf 'Scope: all tracked files\\nChecked files: 3\\nViolations: 0\\n' > \"$report\"\n"
            "    exit 3\n"
            "    ;;\n"
            "  unreadable)\n"
            "    printf 'Scope: all tracked files\\nChecked files: 3\\nViolations: 0\\n' > \"$report\"\n"
            "    chmod 000 \"$report\"\n"
            "    exit 0\n"
            "    ;;\n"
            "  *)\n"
            "    echo \"unknown PYTHON_FAKE_MODE: $PYTHON_FAKE_MODE\" >&2\n"
            "    exit 99\n"
            "    ;;\n"
            "esac\n",
            encoding="utf-8",
        )
        fake.chmod(0o755)

    def _run_step(self, step_name: str, mode: str) -> subprocess.CompletedProcess:
        self._install_fake_python(mode)
        script = self._extract_run_block(step_name)
        env = {
            "PATH": f"{self.bindir}:/usr/bin:/bin",
            "PYTHON_FAKE_MODE": mode,
            "GITHUB_STEP_SUMMARY": str(self.summary),
            "GITHUB_EVENT_PATH": str(self.tmp / "event.json"),
            "EVENT_NAME": "pull_request",
            "BASE_SHA": "0" * 40,
            "HEAD_SHA": "1" * 40,
        }
        (self.tmp / "event.json").write_text("{}", encoding="utf-8")
        return subprocess.run(
            ["bash", "--noprofile", "--norc", "-eo", "pipefail", "-c", script],
            cwd=self.workdir,
            env=env,
            capture_output=True,
            text=True,
            timeout=30,
        )

    def test_repository_step_clean_scan_exits_zero(self):
        result = self._run_step("Validate repository and changed files", "clean")
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_repository_step_violations_exit_one(self):
        result = self._run_step("Validate repository and changed files", "violations")
        self.assertEqual(result.returncode, 1, result.stderr)

    def test_repository_step_crash_exits_three(self):
        # The script's own top-level crash handler (repository_policy.py's
        # `_run`/`main` split) returns 3 for an unanticipated exception. The
        # shell wrapper must pass that through untouched -- not collapse it
        # to 1 ("violations") or 0 ("clean").
        result = self._run_step("Validate repository and changed files", "crash")
        self.assertEqual(result.returncode, 3, result.stderr)

    def test_repository_step_unreadable_report_does_not_flip_clean_exit(self):
        # Round-4 finding (both candidates hit variants of this): a `cat`
        # failure on an *existing* report from a *successful* (exit 0) scan
        # must never replace the real exit status, but it must not be
        # silently swallowed either -- a stdout ::warning:: must appear so a
        # human knows the Job Summary page is incomplete.
        result = self._run_step("Validate repository and changed files", "unreadable")
        self.assertEqual(
            result.returncode, 0,
            f"an unreadable report must not flip a genuine exit 0 to anything else; "
            f"stdout={result.stdout!r} stderr={result.stderr!r}",
        )
        self.assertIn("::warning::", result.stdout, result.stdout)
        self.assertIn("failed to append", result.stdout)

    def test_repository_step_missing_report_error_reaches_real_stdout(self):
        # Round-4 BLOCKING item (a): the workflow's own `::error::...` line
        # for the "report was never written" branch must be sent to the
        # step's actual stdout stream, not merely appended to the
        # $GITHUB_STEP_SUMMARY *file*. GitHub Actions only recognises
        # workflow commands (`::error::`, `::warning::`, ...) when they
        # appear on a step's stdout; text redirected into a file the step
        # happens to write to is inert on the Checks tab even though it
        # looks identical as a string. This test proves the annotation is
        # observable on the *captured subprocess stdout*, independent of
        # whatever also landed in the summary file.
        result = self._run_step("Validate repository and changed files", "writefail")
        self.assertIn("::error::", result.stdout, f"stdout={result.stdout!r}")
        self.assertIn("was not written", result.stdout)

    def test_repository_step_report_write_failure_exits_two_not_one(self):
        # THIS is the test that would have caught round-3's headline defect:
        # it proves the *shell*, not just the script, ultimately reports
        # exit code 2 -- not 1 (which the old workflow silently produced via
        # `cat`'s own failure under errexit when the report was never
        # written).
        result = self._run_step("Validate repository and changed files", "writefail")
        self.assertEqual(
            result.returncode, 2,
            f"expected the real shell-observed exit code to be 2 (report-write "
            f"failure), got {result.returncode}. stdout={result.stdout!r} "
            f"stderr={result.stderr!r}",
        )
        # The missing-report branch must still leave a trace in the step
        # summary a human would actually read, not just vanish.
        summary_text = self.summary.read_text(encoding="utf-8")
        self.assertIn("::error::", summary_text)
        self.assertIn("was not written", summary_text)

    def test_pr_metadata_step_report_write_failure_exits_two_not_one(self):
        # The sibling step (`pr_policy.py` / pr-policy-report.md) has the
        # exact same shell-masking shape and must get the exact same fix.
        result = self._run_step("Validate PR metadata", "writefail")
        self.assertEqual(result.returncode, 2, f"stdout={result.stdout!r} stderr={result.stderr!r}")
        summary_text = self.summary.read_text(encoding="utf-8")
        self.assertIn("::error::", summary_text)

    def test_pr_metadata_step_missing_report_error_reaches_real_stdout(self):
        # Round-4 BLOCKING item (a), sibling step. Same proof as the
        # repository-policy step: the annotation must be observable on
        # actual stdout, not only inside the summary file.
        result = self._run_step("Validate PR metadata", "writefail")
        self.assertIn("::error::", result.stdout, f"stdout={result.stdout!r}")
        self.assertIn("was not written", result.stdout)

    def test_pr_metadata_step_clean_scan_exits_zero(self):
        # Round-4 BD-2: the PR-metadata step previously only had its
        # fragment string-matched, never dynamically executed end-to-end
        # the way the repository-policy step was. Give it the same full
        # clean/violations/crash/unreadable coverage.
        result = self._run_step("Validate PR metadata", "clean")
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_pr_metadata_step_violations_exit_one(self):
        result = self._run_step("Validate PR metadata", "violations")
        self.assertEqual(result.returncode, 1, result.stderr)

    def test_pr_metadata_step_crash_exits_three(self):
        result = self._run_step("Validate PR metadata", "crash")
        self.assertEqual(result.returncode, 3, result.stderr)

    def test_pr_metadata_step_unreadable_report_does_not_flip_clean_exit(self):
        result = self._run_step("Validate PR metadata", "unreadable")
        self.assertEqual(
            result.returncode, 0,
            f"an unreadable report must not flip a genuine exit 0 to anything else; "
            f"stdout={result.stdout!r} stderr={result.stderr!r}",
        )
        self.assertIn("::warning::", result.stdout, result.stdout)
        self.assertIn("failed to append", result.stdout)

    def test_reverting_the_if_guard_reproduces_the_round3_bug(self):
        # Mutation-style self-check: with the OLD (unguarded) `cat` line,
        # the same writefail scenario must observably regress to exit 1,
        # proving this test suite actually discriminates between the fixed
        # and broken workflow text rather than passing regardless of it.
        broken_script = (
            'status=0\n'
            './bin_python_stub || status=$?\n'
            'cat repository-policy-report.md >> "$GITHUB_STEP_SUMMARY"\n'
            'exit "$status"\n'
        )
        self._install_fake_python("writefail")
        stub = self.workdir / "bin_python_stub"
        stub.write_text(
            "#!/bin/bash\nexec \"$PYTHON_FAKE_PY\" scripts/ci/repository_policy.py --report repository-policy-report.md\n",
            encoding="utf-8",
        )
        stub.chmod(0o755)
        env = {
            "PATH": f"{self.bindir}:/usr/bin:/bin",
            "PYTHON_FAKE_MODE": "writefail",
            "PYTHON_FAKE_PY": str(self.bindir / "python"),
            "GITHUB_STEP_SUMMARY": str(self.summary),
        }
        result = subprocess.run(
            ["bash", "--noprofile", "--norc", "-eo", "pipefail", "-c", broken_script],
            cwd=self.workdir,
            env=env,
            capture_output=True,
            text=True,
            timeout=30,
        )
        self.assertEqual(
            result.returncode, 1,
            "sanity check failed: the deliberately-unguarded 'cat' fragment "
            "was expected to reproduce the round-3 masking bug (exit 1, not "
            "2); if this now returns 2 the sanity fixture itself is wrong.",
        )

    def test_mutation_check_bare_or_true_silently_swallows_cat_failure(self):
        # Round-4 finding against the `|| true` shape specifically: it never
        # flips the exit code (good), but it also emits zero signal that
        # anything went wrong (bad). This self-check proves our test suite
        # can actually tell the difference -- a regression back to bare
        # `|| true` must make `test_repository_step_unreadable_report_does_
        # not_flip_clean_exit`'s ::warning:: assertion fail, which this test
        # confirms by directly reproducing the old shape here.
        old_shape_script = (
            'status=0\n'
            './bin_python_stub || status=$?\n'
            'if [ -f repository-policy-report.md ]; then\n'
            '  cat repository-policy-report.md >> "$GITHUB_STEP_SUMMARY" || true\n'
            'else\n'
            '  echo "::error::missing" >> "$GITHUB_STEP_SUMMARY"\n'
            'fi\n'
            'exit "$status"\n'
        )
        self._install_fake_python("unreadable")
        stub = self.workdir / "bin_python_stub"
        stub.write_text(
            "#!/bin/bash\nexec \"$PYTHON_FAKE_PY\" scripts/ci/repository_policy.py --report repository-policy-report.md\n",
            encoding="utf-8",
        )
        stub.chmod(0o755)
        env = {
            "PATH": f"{self.bindir}:/usr/bin:/bin",
            "PYTHON_FAKE_MODE": "unreadable",
            "PYTHON_FAKE_PY": str(self.bindir / "python"),
            "GITHUB_STEP_SUMMARY": str(self.summary),
        }
        result = subprocess.run(
            ["bash", "--noprofile", "--norc", "-eo", "pipefail", "-c", old_shape_script],
            cwd=self.workdir,
            env=env,
            capture_output=True,
            text=True,
            timeout=30,
        )
        # The old `|| true` shape: exit code correctly stays 0 (it never
        # regressed on THAT axis), but it produces no ::warning::/::error::
        # anywhere -- proving this mutant is real and our fixed workflow
        # text (asserted elsewhere to contain a stdout ::warning::) is doing
        # actual, necessary work, not decorating an already-safe fragment.
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("::warning::", result.stdout)
        self.assertNotIn("::warning::", self.summary.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
