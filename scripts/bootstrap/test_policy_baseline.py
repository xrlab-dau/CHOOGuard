"""R-07 regressions. All approvals, files and tool versions here are synthetic."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

import test_verify_toolchain as baseline_tests

verifier = baseline_tests.verifier


class PolicyBaselineTests(unittest.TestCase):
    def setUp(self):
        self.case = baseline_tests.ToolchainBoundaryTests()
        self.case.setUp()
        self.case.use_synthetic_baseline = False
        self.addCleanup(self.case.doCleanups)
        self.root = self.case.root
        for pattern in verifier.POLICY_GLOBS:
            if not list(self.root.glob(pattern)):
                target = self.root / pattern.replace("*", "fixture")
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("{}\n" if target.suffix == ".json" else "# fixture\n")
        self.manifest = self.root.parent / "baseline.json"

    def write_manifest(self, status="approved", changes=None):
        files = {}
        for pattern in verifier.POLICY_GLOBS:
            for path in self.root.glob(pattern):
                if path.is_file():
                    files[path.relative_to(self.root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        data = {"schemaVersion": 1, "status": status,
                "approvalReference": "SYNTHETIC-TEST-ONLY" if status == "approved" else None,
                "sourceCommit": "0" * 40, "policySha256": files}
        if changes:
            changes(data)
        self.manifest.write_text(json.dumps(data, sort_keys=True), encoding="utf-8")
        return hashlib.sha256(self.manifest.read_bytes()).hexdigest()

    def compare(self, digest=None):
        return verifier.policy_baseline_check(self.root, self.manifest, digest or self.write_manifest())

    def test_no_baseline_cannot_claim_required_ok(self):
        target = "docs/evidence/M0-00/pending.json"
        self.assertEqual(self.case.invoke("--write", target), 1)
        receipt = json.loads((self.root / target).read_text())
        self.assertFalse(receipt["required_ok"])
        self.assertEqual(receipt["checks"]["policy_baseline"]["status"], "policy_approval_pending")

    def test_exact_externally_pinned_baseline_matches(self):
        check = self.compare()
        self.assertTrue(check["ok"], check)
        self.assertEqual(check["status"], "baseline_match_not_runtime_approval")

    def test_main_matches_pin_and_blocks_later_policy_change(self):
        digest = self.write_manifest()
        args = ("--policy-manifest", str(self.manifest), "--policy-manifest-sha256", digest)
        self.assertEqual(self.case.invoke(*args), 0)
        (self.root / "AGENTS.md").write_text("changed policy\n")
        self.assertEqual(self.case.invoke(*args), 1)

    def test_candidate_mode_does_not_invoke_agent_or_package_tools(self):
        calls = []
        original = self.case.fake_run
        def record(*args):
            calls.append(args)
            return original(*args)
        self.case.fake_run = record
        target = "docs/evidence/R-07/candidate.json"
        self.assertEqual(self.case.invoke("--write-policy-candidate", target), 0)
        self.assertEqual(calls, [("git", "rev-parse", "HEAD")])
        data = json.loads((self.root / target).read_text())
        self.assertEqual(data["status"], "candidate")
        self.assertIsNone(data["approvalReference"])
        self.assertEqual(self.case.invoke("--write-policy-candidate", target), 2)

    def test_candidate_is_not_approval_even_with_matching_pin(self):
        check = self.compare(self.write_manifest(status="candidate"))
        self.assertFalse(check["ok"])
        self.assertEqual(check["status"], "policy_approval_pending")

    def test_pending_baseline_does_not_invoke_agent_or_package_tools(self):
        calls = []
        original = self.case.fake_run
        def record(*args):
            calls.append(args)
            return original(*args)
        self.case.fake_run = record
        self.assertEqual(self.case.invoke(), 1)
        self.assertFalse(any(args[0] in {"pi", "node", "uv"} for args in calls), calls)

    def test_modified_policy_does_not_accept_fresh_self_observed_hash(self):
        digest = self.write_manifest()
        (self.root / "AGENTS.md").write_text("changed policy\n")
        check = self.compare(digest)
        self.assertFalse(check["ok"])
        self.assertIn("AGENTS.md", check["changed_paths"])

    def test_new_policy_file_requires_review_of_exact_set(self):
        digest = self.write_manifest()
        (self.root / ".pi/agents/extra.md").write_text("extra policy\n")
        check = self.compare(digest)
        self.assertFalse(check["ok"])
        self.assertIn(".pi/agents/extra.md", check["unexpected_paths"])

    def test_deleted_member_fails_even_when_glob_still_has_a_file(self):
        extra = self.root / ".pi/agents/extra.md"
        extra.write_text("extra policy\n")
        digest = self.write_manifest()
        extra.unlink()
        check = self.compare(digest)
        self.assertFalse(check["ok"])
        self.assertIn(".pi/agents/extra.md", check["missing_paths"])

    def test_missing_required_policy_family_is_not_blessed_by_new_manifest(self):
        for path in self.root.glob(".pi/agents/*.md"):
            path.unlink()
        check = self.compare()
        self.assertFalse(check["ok"])
        self.assertIn(".pi/agents/*.md", check["missing_globs"])

    def test_manifest_changes_fail_against_original_pin(self):
        digest = self.write_manifest()
        self.manifest.write_text(self.manifest.read_text() + "\n")
        self.assertEqual(self.compare(digest)["status"], "manifest_digest_mismatch")

    def test_missing_manifest_pin_is_not_accepted(self):
        self.write_manifest()
        self.assertFalse(verifier.policy_baseline_check(self.root, self.manifest, None)["ok"])

    def test_duplicate_manifest_key_is_rejected(self):
        self.write_manifest()
        self.manifest.write_text(self.manifest.read_text().replace('"schemaVersion": 1', '"schemaVersion": 1, "schemaVersion": 1'))
        digest = hashlib.sha256(self.manifest.read_bytes()).hexdigest()
        self.assertFalse(self.compare(digest)["ok"])

    def test_approved_manifest_needs_a_reference(self):
        digest = self.write_manifest(changes=lambda d: d.update(approvalReference=None))
        self.assertFalse(self.compare(digest)["ok"])

    def test_unsafe_or_noncanonical_manifest_paths_are_rejected(self):
        for name in ("../policy", "/policy", "a/../AGENTS.md", "a//b", "a\\b"):
            with self.subTest(name=name):
                digest = self.write_manifest(changes=lambda d: d["policySha256"].update({name: "0" * 64}))
                self.assertFalse(self.compare(digest)["ok"])

    def test_policy_symlink_is_not_read_as_approved_content(self):
        digest = self.write_manifest()
        outside = self.root.parent / "outside-policy.md"
        outside.write_text("must not be read\n")
        path = self.root / "AGENTS.md"
        path.unlink()
        try:
            path.symlink_to(outside)
        except OSError:
            self.skipTest("symlink creation is unavailable")
        check = self.compare(digest)
        self.assertFalse(check["ok"])
        self.assertGreater(check["inventory_errors"], 0)


class WorkspaceBudgetTests(unittest.TestCase):
    def setUp(self):
        self.case = baseline_tests.ToolchainBoundaryTests()
        self.case.setUp()
        self.addCleanup(self.case.doCleanups)
        self.root = self.case.root

    def test_empty_directory_budget_fails_closed(self):
        for index in range(8):
            (self.root / f"empty-{index}").mkdir()
        with patch.object(verifier, "WORKSPACE_SCAN_MAX_DIRS", 2, create=True):
            result = verifier.scan_workspace(self.root)
        self.assertTrue(result["truncated"])
        self.assertEqual(result["limit"], "directories")

    def test_elapsed_time_budget_fails_closed(self):
        with patch.object(verifier, "monotonic", side_effect=[0, 20, 21], create=True):
            result = verifier.scan_workspace(self.root)
        self.assertTrue(result["truncated"])
        self.assertEqual(result["limit"], "elapsed_time")

    def test_external_symlink_inside_venv_is_not_pruned(self):
        outside = self.root.parent / "external"
        outside.mkdir()
        link = self.root / "tools/research/.venv/linked"
        try:
            link.symlink_to(outside, target_is_directory=True)
        except OSError:
            self.skipTest("symlink creation is unavailable")
        result = verifier.scan_workspace(self.root)
        self.assertIn("tools/research/.venv/linked", result["outside_links"])

    def test_mount_point_is_not_traversed_or_reported_clean(self):
        mount = self.root / "mounted"
        mount.mkdir()
        with patch.object(verifier.os.path, "ismount", side_effect=lambda p: Path(p) == mount):
            result = verifier.scan_workspace(self.root)
        self.assertEqual(result["mounts"], 1)
        self.assertGreater(len(result["errors"]), 0)

    @unittest.skipUnless(os.name == "nt", "Native Windows junction test")
    def test_native_windows_junction_is_not_traversed_or_reported_clean(self):
        outside = self.root.parent / "junction-target"
        outside.mkdir()
        (outside / "capture.mp4").write_bytes(b"synthetic")
        junction = self.root / "tools/research/.venv/junction"
        created = subprocess.run(["cmd", "/c", "mklink", "/J", str(junction), str(outside)],
                                 capture_output=True, timeout=15)
        self.assertEqual(created.returncode, 0, created.stderr)
        result = verifier.scan_workspace(self.root)
        self.assertGreaterEqual(result["reparse_points"], 1)
        self.assertIn("unverified_reparse_point", result["errors"])
        self.assertEqual(result["forbidden"], [], "The external target must not be traversed")

    def test_unknown_pth_and_misplaced_ca_file_are_not_dependency_exceptions(self):
        for relative in ("tools/research/.venv/lib/site-packages/weights.pth",
                         "tools/research/.venv/lib/site-packages/unrelated/cacert.pem",
                         ".pi/npm/node_modules/pkg/cacert.pem"):
            with self.subTest(relative=relative):
                self.assertTrue(verifier.is_forbidden_workspace(relative))

    def test_nested_git_directory_is_still_workspace_input(self):
        target = self.root / "nested/.git/capture.mp4"
        target.parent.mkdir(parents=True)
        target.write_bytes(b"synthetic")
        self.assertIn("nested/.git/capture.mp4", verifier.scan_workspace(self.root)["forbidden"])

    def test_receipt_root_symlink_cannot_write_outside_repo(self):
        outside = self.root.parent / "outside-evidence"
        outside.mkdir()
        link = self.root / "docs/evidence"
        link.parent.mkdir(exist_ok=True)
        try:
            link.symlink_to(outside, target_is_directory=True)
        except OSError:
            self.skipTest("symlink creation is unavailable")
        with self.assertRaises(ValueError):
            verifier.receipt_target(self.root, "docs/evidence/receipt.json")


if __name__ == "__main__":
    unittest.main()
