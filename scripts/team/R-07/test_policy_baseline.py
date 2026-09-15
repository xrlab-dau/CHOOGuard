"""R-07 negative tests: missing path, empty file, hash drift and target absence must fail observably."""
import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import policy_baseline as gate  # noqa: E402

GLOBS = ["AGENTS.md", ".github/CODEOWNERS", ".github/workflows/*.yml", "scripts/ci/*.py"]
REQUIRED = ["AGENTS.md", ".github/CODEOWNERS", ".github/workflows/*.yml"]


class Fixture:
    def __init__(self, root: Path):
        self.root = root
        for name, text in {"AGENTS.md": "agents\n", ".github/CODEOWNERS": "* @lead\n", ".github/workflows/ci.yml": "on: push\n",
                           ".github/workflows/release.yml": "on: tag\n", "scripts/ci/policy.py": "print('policy')\n",
                           "docs/outside.md": "not policy\n"}.items():
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(text.encode("utf-8"))

    def baseline(self):
        return gate.build(self.root, GLOBS, REQUIRED, source_ref="0" * 40)


def run_cli(*argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = gate.main([str(a) for a in argv])
    return code, json.loads(out.getvalue())


class PolicyBaselineTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.fixture = Fixture(self.base / "repo")
        self.baseline = self.fixture.baseline()

    def codes(self, report):
        return [(f["code"], f["path"]) for f in report["findings"]]

    def check(self):
        return gate.check(self.fixture.root, self.baseline, verifier_globs=GLOBS)

    def test_unchanged_scope_is_ok(self):
        report = self.check()
        self.assertEqual(report["state"], "ok")
        self.assertEqual(report["findings"], [])
        self.assertEqual(sorted(self.baseline["files"]), [".github/CODEOWNERS", ".github/workflows/ci.yml",
                                                          ".github/workflows/release.yml", "AGENTS.md", "scripts/ci/policy.py"])
        self.assertNotIn("docs/outside.md", self.baseline["files"])

    def test_missing_path_is_rejected(self):
        (self.fixture.root / ".github/workflows/release.yml").unlink()
        report = self.check()
        self.assertEqual(report["state"], "cannot_proceed")
        self.assertEqual(self.codes(report), [("missing_path", ".github/workflows/release.yml")])

    def test_empty_file_is_rejected(self):
        (self.fixture.root / ".github/CODEOWNERS").write_bytes(b"")
        report = self.check()
        self.assertEqual(self.codes(report), [("empty_file", ".github/CODEOWNERS"), ("required_glob_unmatched", ".github/CODEOWNERS")])

    def test_hash_drift_is_rejected_even_when_size_is_unchanged(self):
        path = self.fixture.root / "AGENTS.md"
        path.write_bytes(b"AGENTS\n")
        self.assertEqual(path.stat().st_size, self.baseline["files"]["AGENTS.md"]["bytes"])
        self.assertEqual(self.codes(self.check()), [("hash_drift", "AGENTS.md")])

    def test_new_file_inside_scope_is_rejected(self):
        (self.fixture.root / "scripts/ci/new_gate.py").write_text("print(1)\n", encoding="utf-8")
        (self.fixture.root / "docs/also_outside.md").write_text("x\n", encoding="utf-8")
        self.assertEqual(self.codes(self.check()), [("unexpected_file", "scripts/ci/new_gate.py")])

    def test_required_glob_without_any_file_is_rejected(self):
        for name in (".github/workflows/ci.yml", ".github/workflows/release.yml"):
            (self.fixture.root / name).unlink()
        self.assertEqual(self.codes(self.check()), [("missing_path", ".github/workflows/ci.yml"), ("missing_path", ".github/workflows/release.yml"),
                                                    ("required_glob_unmatched", ".github/workflows/*.yml")])

    def test_verifier_scope_change_is_rejected(self):
        report = gate.check(self.fixture.root, self.baseline, verifier_globs=GLOBS + ["docs/*.md"])
        self.assertEqual(self.codes(report), [("scope_changed", "docs/*.md")])
        reordered = gate.check(self.fixture.root, self.baseline, verifier_globs=list(reversed(GLOBS)))
        self.assertEqual(self.codes(reordered), [("scope_changed", "(order)")])

    def test_every_finding_is_reported_not_only_the_first(self):
        (self.fixture.root / "AGENTS.md").write_bytes(b"changed\n")
        (self.fixture.root / "scripts/ci/policy.py").unlink()
        (self.fixture.root / "scripts/ci/extra.py").write_text("x\n", encoding="utf-8")
        report = self.check()
        self.assertEqual(report["counts"]["hash_drift"], 1)
        self.assertEqual(report["counts"]["missing_path"], 1)
        self.assertEqual(report["counts"]["unexpected_file"], 1)

    def test_baseline_is_not_built_from_a_broken_scope(self):
        (self.fixture.root / "scripts/ci/empty.py").write_bytes(b"")
        with self.assertRaisesRegex(gate.TargetError, "^baseline_source_empty_file$"):
            self.fixture.baseline()
        (self.fixture.root / "scripts/ci/empty.py").unlink()
        (self.fixture.root / "AGENTS.md").unlink()
        with self.assertRaisesRegex(gate.TargetError, "^baseline_source_required_glob_unmatched$"):
            self.fixture.baseline()
        with self.assertRaisesRegex(gate.TargetError, "^target_root_missing$"):
            gate.build(self.base / "absent", GLOBS, REQUIRED)

    def test_invalid_baselines_are_refused(self):
        broken = [None, [], {**self.baseline, "schemaVersion": 2}, {**self.baseline, "kind": "other"}, {**self.baseline, "files": {}},
                  {**self.baseline, "globs": []}, {**self.baseline, "files": {"../AGENTS.md": self.baseline["files"]["AGENTS.md"]}},
                  {**self.baseline, "files": {"C:/AGENTS.md": self.baseline["files"]["AGENTS.md"]}},
                  {**self.baseline, "files": {"AGENTS.md": {"sha256": "A" * 64, "bytes": 7}}},
                  {**self.baseline, "files": {"AGENTS.md": {"sha256": "a" * 64, "bytes": 0}}},
                  {**self.baseline, "files": {"AGENTS.md": {"sha256": "a" * 64, "bytes": True}}}]
        for value in broken:
            with self.subTest(value=str(value)[:60]), self.assertRaisesRegex(gate.TargetError, "^baseline_invalid$"):
                gate.check(self.fixture.root, value, verifier_globs=GLOBS)

    def test_receipt_hashes_are_compared_with_the_baseline(self):
        hashes = {name: entry["sha256"] for name, entry in self.baseline["files"].items()}
        receipt = {"record_type": "machine_observation_not_approval", "required_ok": True, "policy_sha256": dict(hashes)}
        self.assertEqual(gate.check_receipt(receipt, self.baseline)["state"], "ok")
        drifted = dict(hashes, **{"AGENTS.md": "f" * 64})
        del drifted["scripts/ci/policy.py"]
        drifted["scripts/ci/extra.py"] = "e" * 64
        report = gate.check_receipt({**receipt, "policy_sha256": drifted}, self.baseline)
        self.assertEqual(self.codes(report), [("missing_path", "scripts/ci/policy.py"), ("hash_drift", "AGENTS.md"),
                                              ("unexpected_file", "scripts/ci/extra.py")])
        self.assertTrue(receipt["required_ok"], "the verifier's own verdict does not see drift; the baseline does")
        for bad in ({}, {"record_type": "other", "policy_sha256": hashes}, {**receipt, "policy_sha256": {}},
                    {**receipt, "policy_sha256": {"/abs": "a" * 64}}, {**receipt, "policy_sha256": {"AGENTS.md": "short"}}):
            with self.subTest(bad=str(bad)[:50]), self.assertRaisesRegex(gate.TargetError, "^receipt_invalid$"):
                gate.check_receipt(bad, self.baseline)

    def test_cli_exit_codes_and_target_absence(self):
        baseline_path = self.base / "baseline.json"
        gate.write_new(baseline_path, self.baseline)
        # The CLI checks against the real verifier POLICY_GLOBS, so use a baseline carrying that scope.
        real_scope = {**self.baseline, "globs": list(gate.verifier.POLICY_GLOBS)}
        real_path = self.base / "real-scope.json"
        gate.write_new(real_path, real_scope)

        code, report = run_cli("check", "--root", self.fixture.root, "--baseline", real_path)
        self.assertEqual(code, gate.EXIT_OK, report)
        (self.fixture.root / "AGENTS.md").write_bytes(b"drift\n")
        code, report = run_cli("check", "--root", self.fixture.root, "--baseline", real_path)
        self.assertEqual(code, gate.EXIT_FINDINGS)
        self.assertEqual(report["state"], "cannot_proceed")
        self.assertIn({"code": "hash_drift", "path": "AGENTS.md"}, report["results"][0]["findings"])
        self.assertNotIn(str(self.base), json.dumps(report), "no absolute path in public output")

        cases = [(["check", "--root", self.base / "absent", "--baseline", real_path], "target_root_missing"),
                 (["check", "--root", self.fixture.root, "--baseline", self.base / "absent.json"], "baseline_missing"),
                 (["check", "--root", self.fixture.root, "--baseline", real_path, "--receipt", self.base / "absent.json"], "receipt_missing"),
                 (["check", "--root", self.fixture.root, "--baseline", real_path, "--output", baseline_path], "output_exists"),
                 (["build", "--root", self.fixture.root, "--output", baseline_path], "output_exists"),
                 (["authority", "--root", self.fixture.root, "--profile", self.base / "absent.json"], "profile_missing")]
        (self.base / "malformed.json").write_text("{not json", encoding="utf-8")
        cases.append((["check", "--root", self.fixture.root, "--baseline", self.base / "malformed.json"], "baseline_invalid"))
        before = baseline_path.read_bytes()
        for argv, error in cases:
            with self.subTest(error=error):
                code, report = run_cli(*argv)
                self.assertEqual(code, gate.EXIT_CANNOT_START)
                self.assertEqual(report, {"schemaVersion": 1, "kind": gate.RESULT_KIND, "state": "cannot_proceed", "error": error})
        self.assertEqual(baseline_path.read_bytes(), before, "an existing file is never overwritten")

    def test_build_cli_writes_a_new_baseline_that_checks_clean(self):
        out = self.base / "new" / "baseline.json"
        code, report = run_cli("build", "--root", self.fixture.root, "--output", out, "--source-ref", "1" * 40)
        self.assertEqual((code, report["state"]), (gate.EXIT_OK, "baseline_written"))
        written = json.loads(out.read_text(encoding="utf-8"))
        self.assertEqual(written["globs"], list(gate.verifier.POLICY_GLOBS))
        self.assertEqual(report["baselineSha256"], gate.digest_json(written))
        code, result = run_cli("check", "--root", self.fixture.root, "--baseline", out)
        self.assertEqual(code, gate.EXIT_OK, result)

    def test_reuses_the_canonical_verifier_scope_and_hash(self):
        self.assertIs(gate.verifier.sha256, sys.modules["verify_toolchain"].sha256)
        self.assertEqual(gate.build(self.fixture.root)["globs"], list(gate.verifier.POLICY_GLOBS))
        self.assertEqual(gate.build(self.fixture.root)["requiredGlobs"], list(gate.verifier.REQUIRED_POLICY_GLOBS))
        path = self.fixture.root / "AGENTS.md"
        self.assertEqual(self.baseline["files"]["AGENTS.md"]["sha256"], gate.verifier.sha256(path))

    def test_authority_comparison_preserves_both_sources(self):
        profile = {"policy": {"policySources": [{"path": "AGENTS.md", "sha256AtRef": self.baseline["files"]["AGENTS.md"]["sha256"]},
                                                {"path": ".github/CODEOWNERS", "sha256AtRef": "0" * 64},
                                                {"path": "NOTICE.md", "sha256AtRef": "1" * 64}]}}
        report = gate.authority(self.fixture.root, GLOBS, profile)
        self.assertEqual(report["state"], "authority_unresolved")
        self.assertEqual(report["inBoth"], [".github/CODEOWNERS", "AGENTS.md"])
        self.assertEqual(report["onlyProfilePolicySources"], ["NOTICE.md"])
        self.assertEqual(report["onlyVerifierScope"], [".github/workflows/ci.yml", ".github/workflows/release.yml", "scripts/ci/policy.py"])
        self.assertEqual(report["profileSha256AtRefDiffersFromWorktree"], [".github/CODEOWNERS"])
        self.assertEqual(report["profileSourcesAbsentFromWorktree"], ["NOTICE.md"])
        aligned = {"policy": {"policySources": [{"path": p} for p in self.baseline["files"]]}}
        self.assertEqual(gate.authority(self.fixture.root, GLOBS, aligned)["state"], "aligned")
        with self.assertRaisesRegex(gate.TargetError, "^profile_invalid$"):
            gate.authority(self.fixture.root, GLOBS, {"policy": {"policySources": [{"path": "../x"}]}})


if __name__ == "__main__":
    unittest.main()
