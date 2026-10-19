"""Exact Git input IDs drive the actual workflow shell; all GitHub calls are fixtures."""
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import textwrap
import unittest
from unittest import mock

SCRIPTS = Path(__file__).resolve().parents[1]
WORKFLOW = SCRIPTS.parent / "workflows/unity.yml"
sys.path.insert(0, str(SCRIPTS))
import unity_tree_inputs as inputs

BASE, HEAD = "a" * 40, "b" * 40
REPO = "fixture/repo"
CONTRACTS = "docs/CHOOGuard_Story_Plan_v4/basis/v3/contracts"


def oid(label):
    return hashlib.sha256(label.encode()).hexdigest()[:40]


def fixture(changed=None, docs_only=False):
    records = {}

    def tree(label, entries):
        sha = oid(label)
        records[f"repos/{REPO}/git/trees/{sha}"] = {
            "sha": sha, "truncated": False,
            "tree": [{"path": name, "type": "tree", "mode": "040000", "sha": value}
                     for name, value in entries.items()]}
        return sha

    left, right = {}, {}
    for path in ["Assets", "Packages", "ProjectSettings", "content"]:
        left[path] = oid(path)
        right[path] = oid(path + " changed") if changed == path else left[path]
    contract_changed = changed == CONTRACTS
    plans = []
    for side in ["base", "head"]:
        suffix = side if contract_changed else "same"
        contracts = oid("contracts " + (side if contract_changed else "same"))
        v3 = tree("v3 " + suffix, {"contracts": contracts})
        basis = tree("basis " + suffix, {"v3": v3})
        plans.append(tree("plan " + suffix, {"basis": basis}))
    docs_changed = docs_only or contract_changed or changed == "docs/build"
    for index, (side, target) in enumerate([("base", left), ("head", right)]):
        build = oid("build " + (side if changed == "docs/build" else "same"))
        target["docs"] = tree("docs " + (side if docs_changed else "same"),
                              {"build": build, "CHOOGuard_Story_Plan_v4": plans[index]})
        records[f"repos/{REPO}/git/trees/{target['docs']}"]["tree"].append({
            "path": "README.md", "type": "blob", "mode": "100644",
            "sha": oid("readme " + (side if docs_only else "same"))})
    left_tree, right_tree = tree("root base", left), tree("root head", right)
    records[f"repos/{REPO}/git/commits/{BASE}"] = {"sha": BASE, "tree": {"sha": left_tree}}
    records[f"repos/{REPO}/git/commits/{HEAD}"] = {"sha": HEAD, "tree": {"sha": right_tree}}
    # The observed defective diff response is deliberately available, but must never be consulted.
    records[f"repos/{REPO}/pulls/305"] = {"changed_files": 0, "additions": 0, "deletions": 0}
    records[f"repos/{REPO}/pulls/305/files?per_page=100"] = {
        "headers": {}, "files": [{"filename": f".checkpoints/retired/{i}.txt"} for i in range(100)]}
    return records


class GitInputTests(unittest.TestCase):
    def compare(self, records):
        calls = []

        def request(path):
            calls.append(path)
            return records[path]

        result = inputs.GitInputs(REPO, request=request).first_changed(BASE, HEAD)
        return result, calls

    def test_zero_metadata_and_partial_nonunity_file_list_cannot_hide_changed_assets(self):
        changed, calls = self.compare(fixture(changed="Assets"))
        self.assertEqual(changed, "Assets")
        self.assertEqual(len(calls), 4)  # Two commits and two nonrecursive root directories.
        self.assertFalse(any("/pulls/" in p or "/compare/" in p or "recursive" in p for p in calls))

    def test_docs_only_change_skips_without_fetching_unrelated_files_or_subtree_contents(self):
        records = fixture(docs_only=True)
        changed, calls = self.compare(records)
        self.assertIsNone(changed)
        self.assertEqual(len(calls), len(set(calls)))
        self.assertFalse(any("/blobs/" in p or "recursive" in p for p in calls))
        for label in ["Assets", "Packages", "ProjectSettings", "content", "build same", "contracts same"]:
            self.assertNotIn(f"repos/{REPO}/git/trees/{oid(label)}", calls)

    def test_each_exact_input_subtree_is_detected(self):
        for path in inputs.INPUTS:
            with self.subTest(path=path):
                self.assertEqual(self.compare(fixture(changed=path))[0], path)

    def test_added_or_removed_input_is_a_change(self):
        for commit in [BASE, HEAD]:
            with self.subTest(commit=commit):
                records = fixture()
                root = records[f"repos/{REPO}/git/commits/{commit}"]["tree"]["sha"]
                records[f"repos/{REPO}/git/trees/{root}"]["tree"] = [
                    entry for entry in records[f"repos/{REPO}/git/trees/{root}"]["tree"]
                    if entry["path"] != "ProjectSettings"]
                self.assertEqual(self.compare(records)[0], "ProjectSettings")

    def test_identical_commits_need_no_api(self):
        request = mock.Mock(side_effect=AssertionError("API not expected"))
        self.assertIsNone(inputs.GitInputs(REPO, request=request).first_changed(BASE, BASE))
        request.assert_not_called()

    def test_truncated_wrong_identity_and_invalid_metadata_fail_conservatively(self):
        for mutation in ["truncated", "sha", "tree"]:
            with self.subTest(mutation=mutation):
                records = fixture()
                root = records[f"repos/{REPO}/git/commits/{HEAD}"]["tree"]["sha"]
                row = records[f"repos/{REPO}/git/trees/{root}"]
                row[mutation] = {"truncated": True, "sha": "0" * 40, "tree": None}[mutation]
                with self.assertRaises(inputs.ComparisonError):
                    self.compare(records)

    def test_failed_gh_api_is_nonzero_without_raw_diagnostics_or_token_arguments(self):
        output = io.StringIO()
        error = subprocess.CompletedProcess([], 1, b"", b"private token diagnostic")
        with mock.patch.object(inputs.subprocess, "run", return_value=error) as run, \
                contextlib.redirect_stderr(output):
            self.assertEqual(inputs.main([REPO, BASE, HEAD]), 1)
        self.assertEqual(run.call_args.args[0], ["gh", "api", f"repos/{REPO}/git/commits/{BASE}"])
        self.assertNotIn("private token", output.getvalue())
        self.assertIn("conservatively", output.getvalue())


class UnityGateTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix="unity gate ")
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.output = self.root / "outputs"
        self.calls = self.root / "gh-args"
        self.fixtures = self.root / "responses.json"
        binary = self.root / "bin"
        binary.mkdir()
        gh = binary / "gh"
        gh.write_text('#!/usr/bin/env python3\nimport json,os,sys\n'
                      'with open(os.environ["FIXTURE_GH_ARGS"],"a") as out: out.write(json.dumps(sys.argv[1:])+"\\n")\n'
                      'if os.environ.get("FIXTURE_GH_FAIL")=="1": sys.exit(1)\n'
                      'records=json.load(open(os.environ["FIXTURE_GH_RESPONSES"]))\n'
                      'if len(sys.argv)!=3 or sys.argv[1]!="api" or sys.argv[2] not in records: sys.exit(2)\n'
                      'print(json.dumps(records[sys.argv[2]]))\n')
        gh.chmod(0o755)
        self.binary = binary
        helper = self.root / ".github/scripts/unity_tree_inputs.py"
        helper.parent.mkdir(parents=True)
        shutil.copyfile(SCRIPTS / "unity_tree_inputs.py", helper)
        gate = WORKFLOW.read_text().split("      - id: decide\n", 1)[1].split("\n  tests:", 1)[0]
        self.script = self.root / "gate.sh"
        self.script.write_text(textwrap.dedent(gate.split("        run: |\n", 1)[1]))

    def run_gate(self, records=None, **overrides):
        self.output.unlink(missing_ok=True)
        self.calls.unlink(missing_ok=True)
        self.fixtures.write_text(json.dumps(fixture() if records is None else records))
        env = os.environ.copy()
        env.pop("GITHUB_TOKEN", None)
        env.pop("GH_TOKEN", None)
        env.update(ENABLED="true", HAS_SERIAL="true", HAS_EMAIL="true", HAS_PASSWORD="true",
                   EVENT="pull_request", REF="refs/pull/305/merge", REPO=REPO,
                   PR_NUMBER="305", PR_REPO=REPO, PR_BASE_SHA=BASE, PR_HEAD_SHA=HEAD,
                   BEFORE=BASE, AFTER=HEAD, OS_INPUT="all", BUILD_INPUT="true",
                   GITHUB_OUTPUT=str(self.output), FIXTURE_GH_FAIL="0",
                   FIXTURE_GH_RESPONSES=str(self.fixtures), FIXTURE_GH_ARGS=str(self.calls),
                   PATH=str(self.binary) + os.pathsep + env["PATH"], PYTHONDONTWRITEBYTECODE="1")
        env.update(overrides)
        result = subprocess.run(["bash", "--noprofile", "--norc", "-e", "-o", "pipefail", str(self.script)],
                                cwd=self.root, env=env, capture_output=True, text=True, timeout=20)
        self.assertEqual(result.returncode, 0, result.stderr)
        outputs = dict(line.split("=", 1) for line in self.output.read_text().splitlines())
        outputs["tests"] = json.loads(outputs["tests"])
        return outputs, result.stdout

    def assert_pr_runs(self, outputs):
        self.assertEqual(outputs["tests"], ["windows", "macos"])
        self.assertEqual(outputs["build"], "false")
        self.assertEqual(outputs["reason"], "")

    def assert_push_runs(self, outputs):
        self.assertEqual(outputs["tests"], ["windows", "macos", "linux"])
        self.assertEqual(outputs["build"], "true")
        self.assertEqual(outputs["reason"], "")

    def test_actual_partial_diff_fixture_runs_pr_from_changed_git_tree(self):
        outputs, stdout = self.run_gate(fixture(changed="Assets"))
        self.assert_pr_runs(outputs)
        self.assertIn("Unity input tree changed: Assets", stdout)
        calls = [json.loads(line) for line in self.calls.read_text().splitlines()]
        self.assertTrue(all(args[0] == "api" and "/git/" in args[1] for args in calls))
        self.assertEqual(len(calls), 4)

    def test_docs_only_pr_still_skips(self):
        outputs, _ = self.run_gate(fixture(docs_only=True))
        self.assertEqual(outputs["tests"], [])
        self.assertIn("does not touch", outputs["reason"])

    def test_narrow_docs_test_inputs_still_run(self):
        for path in ["docs/build", CONTRACTS]:
            with self.subTest(path=path):
                self.assert_pr_runs(self.run_gate(fixture(changed=path))[0])

    def test_changed_push_runs_all_and_builds(self):
        self.assert_push_runs(self.run_gate(fixture(changed="ProjectSettings"),
                                           EVENT="push", REF="refs/heads/develop")[0])

    def test_docs_only_or_identical_push_can_skip(self):
        for overrides in [{}, {"AFTER": BASE}]:
            with self.subTest(overrides=overrides):
                outputs, _ = self.run_gate(fixture(docs_only=True), EVENT="push",
                                           REF="refs/heads/develop", **overrides)
                self.assertEqual(outputs["tests"], [])
                self.assertEqual(outputs["build"], "false")

    def test_api_failure_schedules_pr_and_push_conservatively(self):
        self.assert_pr_runs(self.run_gate(FIXTURE_GH_FAIL="1")[0])
        self.assert_push_runs(self.run_gate(EVENT="push", REF="refs/heads/develop", FIXTURE_GH_FAIL="1")[0])

    def test_unknown_commit_or_truncated_tree_schedules_conservatively(self):
        self.assert_pr_runs(self.run_gate(PR_HEAD_SHA="unknown")[0])
        records = fixture()
        root = records[f"repos/{REPO}/git/commits/{HEAD}"]["tree"]["sha"]
        records[f"repos/{REPO}/git/trees/{root}"]["truncated"] = True
        self.assert_pr_runs(self.run_gate(records)[0])

    def test_disabled_missing_licence_and_fork_guards_remain(self):
        for overrides in [{"ENABLED": "false"}, {"HAS_EMAIL": "false"}, {"PR_REPO": "fork/repo"}]:
            with self.subTest(overrides=overrides):
                outputs, _ = self.run_gate(fixture(changed="Assets"), **overrides)
                self.assertEqual(outputs["tests"], [])
                self.assertFalse(self.calls.exists())

    def test_tag_first_push_schedule_and_dispatch_remain(self):
        for overrides in [{"EVENT": "push", "REF": "refs/tags/v1"},
                          {"EVENT": "push", "BEFORE": "0" * 40}, {"EVENT": "schedule"}]:
            with self.subTest(overrides=overrides):
                self.assert_push_runs(self.run_gate(**overrides)[0])
                self.assertFalse(self.calls.exists())
        outputs, _ = self.run_gate(EVENT="workflow_dispatch", OS_INPUT="linux", BUILD_INPUT="false")
        self.assertEqual(outputs["tests"], ["linux"])
        self.assertEqual(outputs["build"], "false")
        self.assertFalse(self.calls.exists())


if __name__ == "__main__":
    unittest.main()
