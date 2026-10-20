"""Exercise actual CI shell with a harmless fake editor; require focused/full isolation and strict workflow order."""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
WORKFLOW = SCRIPTS.parent / "workflows/unity.yml"
sys.path.insert(0, str(SCRIPTS))
import unity_results  # noqa: E402


class FocusedRouteShellTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="Unity 경로 fixture ")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        fake_python = self.root / "fake editor.py"
        fake_python.write_text('''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
import xml.etree.ElementTree as ET
args = sys.argv[1:]
with open(os.environ["FAKE_EDITOR_ARGS"], "a", encoding="utf-8") as out:
    out.write(json.dumps(args, ensure_ascii=False) + "\\n")
name = args[args.index("-testFilter") + 1] if "-testFilter" in args else "Full.Suite.Test"
root = ET.Element("test-run", total="1", passed="1", failed="0", skipped="0", inconclusive="0", result="Passed")
ET.SubElement(root, "test-case", fullname=name, result="Passed")
Path(args[args.index("-testResults") + 1]).write_text(ET.tostring(root, encoding="unicode"), encoding="utf-8")
Path(args[args.index("-logFile") + 1]).write_text("fake editor only; no Unity execution\\n", encoding="utf-8")
sys.exit(int(os.environ.get("FAKE_EDITOR_EXIT", "0")))
''', encoding="utf-8")
        # Git Bash cannot rely on a Windows .py association or a python3 shebang.
        # Invoke this test process's exact Python through an executable Bash wrapper.
        self.editor = self.root / "fake editor.sh"
        self.editor.write_text('#!/bin/bash\nexec "$FAKE_EDITOR_PYTHON" "$FAKE_EDITOR_SCRIPT" "$@"\n', encoding="utf-8")
        self.editor.chmod(0o755)
        if os.name == "nt":
            git_bash = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Git/bin/bash.exe"
            self.bash = str(git_bash) if git_bash.is_file() else shutil.which("bash")
        else:
            self.bash = shutil.which("bash")
        self.assertIsNotNone(self.bash, "The CI shell fixture requires Bash, supplied by Git Bash on Windows runners.")
        self.args = self.root / "editor-args.jsonl"
        self.output = self.root / "step-output"
        self.logs = self.root / "logs"
        self.environment = {key: value for key, value in os.environ.items()
                            if key not in ("UNITY_SERIAL", "UNITY_EMAIL", "UNITY_PASSWORD", "GH_TOKEN", "GITHUB_TOKEN")}
        self.environment.update(RUNNER_TEMP=self.root.as_posix(), RUNNER_OS="Windows", UNITY_EDITOR=self.editor.as_posix(),
                                UNITY_PROJECT=(self.root / "한글 project").as_posix(), UNITY_LOGS=self.logs.as_posix(),
                                GITHUB_OUTPUT=self.output.as_posix(), FAKE_EDITOR_ARGS=self.args.as_posix(),
                                FAKE_EDITOR_PYTHON=Path(sys.executable).as_posix(), FAKE_EDITOR_SCRIPT=fake_python.as_posix(),
                                PYTHONDONTWRITEBYTECODE="1")

    def run_shell(self, *args, editor_exit="0"):
        env = {**self.environment, "FAKE_EDITOR_EXIT": editor_exit}
        result = subprocess.run([self.bash, (SCRIPTS / "unity_ci.sh").as_posix(), *args], env=env,
                                capture_output=True, check=False, timeout=10)
        calls = [json.loads(line) for line in self.args.read_text(encoding="utf-8").splitlines()] if self.args.exists() else []
        return result, calls

    def test_exact_focus_filter_uses_playmode_gpu_and_distinct_outputs(self):
        result, calls = self.run_shell("routegraph")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(calls), 1)
        args = calls[0]
        self.assertEqual(args[args.index("-testPlatform") + 1], "PlayMode")
        self.assertEqual(args[args.index("-testFilter") + 1], unity_results.FOCUSED_ROUTE_TEST)
        self.assertEqual(args.count("-testFilter"), 1)
        self.assertNotIn("-nographics", args)
        self.assertEqual(Path(args[args.index("-testResults") + 1]).resolve(), (self.logs / "RouteGraph-results.xml").resolve())
        self.assertEqual(Path(args[args.index("-logFile") + 1]).resolve(), (self.logs / "RouteGraph.log").resolve())
        self.assertEqual(Path(args[args.index("-projectPath") + 1]).resolve(), Path(self.environment["UNITY_PROJECT"]).resolve())
        self.assertEqual(unity_results.evaluate("RouteGraph", str(self.logs / "RouteGraph-results.xml"), "0").errors, [])

    def test_complete_play_remains_unfiltered_and_cannot_overwrite_focused_artifacts(self):
        self.run_shell("routegraph")
        focused_xml = (self.logs / "RouteGraph-results.xml").read_bytes()
        focused_log = (self.logs / "RouteGraph.log").read_bytes()
        result, calls = self.run_shell("test", "PlayMode")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(calls), 2)
        self.assertNotIn("-testFilter", calls[1])
        self.assertEqual(Path(calls[1][calls[1].index("-testResults") + 1]).resolve(), (self.logs / "PlayMode-results.xml").resolve())
        self.assertTrue((self.logs / "PlayMode.log").is_file())
        self.assertEqual((self.logs / "RouteGraph-results.xml").read_bytes(), focused_xml)
        self.assertEqual((self.logs / "RouteGraph.log").read_bytes(), focused_log)

    def test_editor_exit_is_recorded_for_strict_verdict_without_hiding_failure(self):
        result, _ = self.run_shell("routegraph", editor_exit="134")
        self.assertEqual(result.returncode, 0)  # Existing shell contract: verdict owns pass/fail.
        self.assertIn("exit_code=134", self.output.read_text(encoding="utf-8"))
        errors = unity_results.evaluate("RouteGraph", str(self.logs / "RouteGraph-results.xml"), "134").errors
        self.assertTrue(any("disagrees" in error for error in errors))

    def test_focus_command_rejects_extra_filter_arguments_without_launching_editor(self):
        result, calls = self.run_shell("routegraph", "Other.Test")
        self.assertEqual(result.returncode, 64)
        self.assertEqual(calls, [])


class FocusedRouteWorkflowTests(unittest.TestCase):
    def test_edit_verdict_then_strict_focus_then_required_complete_play(self):
        text = WORKFLOW.read_text(encoding="utf-8")
        steps = re.split(r"(?m)^      - ", text)
        step = lambda prefix: next(value for value in steps if value.startswith(prefix))
        edit = step("id: editverdict\n")
        focused = step("id: routegraph\n")
        focus_verdict = step("id: routeverdict\n")
        full = step("id: playmode\n")
        self.assertLess(text.index(edit), text.index(focused))
        self.assertLess(text.index(focused), text.index(focus_verdict))
        self.assertLess(text.index(focus_verdict), text.index(full))
        self.assertIn('unity_results.py "EditMode:', edit)
        condition = "if: always() && steps.activate.outcome == 'success' && steps.editverdict.outcome == 'success'"
        self.assertIn(condition, focused)
        self.assertIn(condition, focus_verdict)
        self.assertIn("run: .github/scripts/unity_ci.sh routegraph", focused)
        self.assertIn('unity_results.py "RouteGraph:', focus_verdict)
        self.assertIn(condition + " && steps.routeverdict.outcome == 'success'", full)
        self.assertEqual(text.count("      - id: editverdict\n"), 1)
        self.assertEqual(text.count("      - id: routegraph\n"), 1)
        self.assertNotIn("steps.routegraph", full)
        self.assertIn("timeout-minutes: 60", full)
        self.assertIn("run: .github/scripts/unity_ci.sh test PlayMode", full)
        final = step("name: Verdict\n")
        for mode in ("EditMode", "PlayMode", "RouteGraph"):
            self.assertIn(f'"{mode}:$RUNNER_TEMP/unity/{mode}-results.xml:', final)
        self.assertIn("if: always() && steps.activate.outcome != 'skipped'", step("name: Return the licence\n"))
        self.assertIn("if: always()", step("name: Remove credentials from logs\n"))
        self.assertIn("path: ${{ runner.temp }}/unity", step("name: Upload results and logs\n"))
        self.assertNotIn("continue-on-error", edit + focused + focus_verdict + full + final)

    def test_failed_skipped_or_cancelled_focus_verdict_blocks_full_play(self):
        text = WORKFLOW.read_text(encoding="utf-8")
        full = next(value for value in re.split(r"(?m)^      - ", text) if value.startswith("id: playmode\n"))
        condition = re.search(r"(?m)^        if: (.+)$", full).group(1)

        def allowed(outcomes):
            verdicts = []
            for term in condition.split(" && "):
                if term == "always()":
                    verdicts.append(True)
                    continue
                match = re.fullmatch(r"steps\.([a-z]+)\.outcome == 'success'", term)
                self.assertIsNotNone(match, "Unknown workflow condition must get explicit fixture coverage")
                verdicts.append(outcomes.get(match.group(1)) == "success")
            return all(verdicts)

        success = dict(activate="success", editverdict="success", routeverdict="success")
        self.assertTrue(allowed(success))
        for outcome in ("failure", "skipped", "cancelled", ""):
            with self.subTest(focus_verdict=outcome):
                self.assertFalse(allowed({**success, "routeverdict": outcome}))
        self.assertFalse(allowed({**success, "editverdict": "failure"}))
        self.assertFalse(allowed({**success, "activate": "failure"}))

    def test_aggregate_cannot_pass_without_focused_and_complete_play_results(self):
        with tempfile.TemporaryDirectory(prefix="Required Unity verdicts ") as temporary:
            root = Path(temporary)
            summary = root / "summary.md"
            for focus_pass, full_present, expected in ((False, False, 1), (False, True, 1), (True, False, 1), (True, True, 0)):
                with self.subTest(focus_pass=focus_pass, full_present=full_present):
                    for mode in ("EditMode", "PlayMode", "RouteGraph"):
                        path = root / (mode + "-results.xml")
                        path.unlink(missing_ok=True)
                        if mode == "PlayMode" and not full_present:
                            continue
                        passed = mode != "RouteGraph" or focus_pass
                        name = unity_results.FOCUSED_ROUTE_TEST if mode == "RouteGraph" else mode + ".AnyTest"
                        path.write_text('<test-run total="1" passed="' + str(int(passed)) + '" failed="' + str(int(not passed)) +
                                        '" skipped="0" inconclusive="0" result="' + ("Passed" if passed else "Failed") +
                                        '"><test-case fullname="' + name + '" result="' + ("Passed" if passed else "Failed") + '"/></test-run>', encoding="utf-8")
                    specs = [f"{mode}:{root / (mode + '-results.xml')}:{2 if mode == 'RouteGraph' and not focus_pass else 0}"
                             for mode in ("EditMode", "PlayMode", "RouteGraph")]
                    env = {**os.environ, "GITHUB_STEP_SUMMARY": str(summary), "PYTHONIOENCODING": "cp1252"}
                    result = subprocess.run([sys.executable, str(SCRIPTS / "unity_results.py"), *specs],
                                            capture_output=True, check=False, env=env, timeout=10)
                    self.assertEqual(result.returncode, expected, result.stderr)


if __name__ == "__main__":
    unittest.main()
