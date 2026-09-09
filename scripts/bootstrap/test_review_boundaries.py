"""PR #97 follow-up regressions; policy approval and tools are synthetic."""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import unittest
from unittest.mock import patch

import test_policy_baseline as policy_tests

verifier = policy_tests.verifier


class ReviewBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.fixture = policy_tests.PolicyBaselineTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.case = self.fixture.case
        self.root = self.fixture.root

    def link(self, path, target, directory=True):
        path.parent.mkdir(parents=True, exist_ok=True)
        try:
            path.symlink_to(target, target_is_directory=directory)
        except OSError as error:
            if isinstance(error, PermissionError) or getattr(error, "winerror", None) == 1314:
                self.skipTest("This environment cannot create symbolic links")
            raise

    def pinned_args(self):
        return ("--policy-manifest", str(self.fixture.manifest),
                "--policy-manifest-sha256", self.fixture.write_manifest())

    def on_last_probe(self, action):
        original = self.case.fake_run

        def mutate(*args):
            if args == ("uv", "--version"):
                action()
            return original(*args)

        self.case.fake_run = mutate

    def invoke_receipt(self, *args):
        target = "docs/evidence/R-07/review.json"
        code = self.case.invoke(*args, "--write", target)
        return code, json.loads((self.root / target).read_text(encoding="utf-8"))

    def test_forbidden_file_created_by_probe_fails_final_receipt(self):
        args = self.pinned_args()
        self.on_last_probe(lambda: (self.root / "private.key").write_text("synthetic fixture"))
        code, receipt = self.invoke_receipt(*args)
        self.assertEqual(code, 1)
        self.assertTrue(receipt["tool_probes"]["executed"])
        self.assertTrue(receipt["checks"]["policy_baseline"]["ok"])
        self.assertFalse(receipt["required_ok"])
        check = receipt["checks"]["forbidden_workspace_files"]
        self.assertEqual(check["count"], 1)
        self.assertEqual(check["suffixes"], [".key"])
        self.assertNotIn("private.key", json.dumps(receipt))

    def test_outside_link_created_by_probe_fails_final_receipt(self):
        outside = self.root.parent / "outside"
        outside.mkdir()
        # Check capability before invoking the tool callback.
        probe = self.root / "link-capability"
        self.link(probe, outside)
        probe.unlink()
        args = self.pinned_args()
        self.on_last_probe(lambda: self.link(self.root / "late-link", outside))
        code, receipt = self.invoke_receipt(*args)
        self.assertEqual(code, 1)
        self.assertTrue(receipt["tool_probes"]["executed"])
        self.assertFalse(receipt["required_ok"])
        self.assertEqual(receipt["checks"]["symlinks_outside_repo"]["count"], 1)

    def test_post_probe_scan_budget_exhaustion_is_unknown_not_clean(self):
        args = self.pinned_args()
        initial_directories = verifier.scan_workspace(self.root)["directories"]
        self.on_last_probe(lambda: (self.root / "late/a/b/c").mkdir(parents=True))
        with patch.object(verifier, "WORKSPACE_SCAN_MAX_DIRS", initial_directories + 1):
            code, receipt = self.invoke_receipt(*args)
        self.assertEqual(code, 1)
        self.assertTrue(receipt["tool_probes"]["executed"])
        check = receipt["checks"]["forbidden_workspace_files"]
        self.assertTrue(check["scan_truncated"])
        self.assertEqual(check["scan_limit"], "directories")
        self.assertIsNone(check["count"])
        self.assertIsNone(receipt["checks"]["symlinks_outside_repo"]["count"])
        self.assertFalse(receipt["required_ok"])

    def assert_receipt_link_rejected(self, link_path, destination, junction=False):
        destination.mkdir(parents=True, exist_ok=True)
        link = self.root / link_path
        if junction:
            link.parent.mkdir(parents=True, exist_ok=True)
            created = subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(destination)],
                                     capture_output=True, timeout=15)
            self.assertEqual(created.returncode, 0, created.stderr)
        else:
            self.link(link, destination)
        args = self.pinned_args()
        for option in ("--write-policy-candidate", "--write"):
            with self.subTest(option=option):
                name = option.removeprefix("--") + ".json"
                requested = link_path + ("/evidence/" if link_path == "docs" else "/") + name
                extra = args if option == "--write" else ()
                self.assertEqual(self.case.invoke(*extra, option, requested), 2)
                self.assertFalse((self.root / requested).exists())

    def test_evidence_root_link_cannot_redirect_either_writer_into_policy(self):
        self.assert_receipt_link_rejected("docs/evidence", self.root / "scripts/bootstrap")

    @unittest.skipUnless(os.name == "nt", "Native Windows receipt junction test")
    def test_evidence_root_junction_cannot_redirect_either_writer_into_policy(self):
        self.assert_receipt_link_rejected("docs/evidence", self.root / "scripts/bootstrap", junction=True)

    def test_receipt_paths_reject_drive_and_alternate_stream_components(self):
        for requested in ("docs/evidence/C:/receipt.json", "docs/evidence/C:receipt.json",
                          "docs/evidence/report:stream.json"):
            with self.subTest(requested=requested), self.assertRaises(ValueError):
                verifier.receipt_target(self.root, requested)

    def test_docs_ancestor_link_cannot_redirect_either_writer(self):
        destination = self.root / "alternate-docs"
        (self.root / "docs").rename(destination)
        self.link(self.root / "docs", destination)
        with self.assertRaises(ValueError):
            verifier.receipt_target(self.root, "docs/evidence/redirected.json")
        self.assertFalse((destination / "evidence/redirected.json").exists())

    def test_nested_evidence_link_is_rejected_even_inside_evidence(self):
        self.assert_receipt_link_rejected("docs/evidence/redirect", self.root / "docs/evidence/archive")

    def test_receipt_path_link_created_during_probe_is_rejected_before_write(self):
        evidence = self.root / "docs/evidence"
        evidence.mkdir(parents=True)
        destination = self.root / "scripts/bootstrap"
        probe = self.root / "link-capability"
        self.link(probe, destination)
        probe.unlink()
        args = self.pinned_args()

        def redirect():
            evidence.rmdir()
            self.link(evidence, destination)

        self.on_last_probe(redirect)
        self.assertEqual(self.case.invoke(*args, "--write", "docs/evidence/late.json"), 2)
        self.assertFalse((destination / "late.json").exists())

    def assert_git_input_rejected(self, relative_target, directory):
        target = self.root / relative_target
        if directory:
            target.mkdir(parents=True)
            (target / "private.key").write_text("synthetic fixture")
        else:
            target.parent.mkdir(parents=True)
            target.write_text("synthetic fixture")
        args = self.pinned_args()
        if directory:
            link = self.root / "tools/research/.venv"
            shutil.rmtree(link)
        else:
            link = self.root / "input.txt"
        self.link(link, target, directory)
        code, receipt = self.invoke_receipt(*args)
        self.assertEqual(code, 1)
        self.assertFalse(receipt["tool_probes"]["executed"])
        self.assertTrue(receipt["checks"]["policy_baseline"]["ok"])
        self.assertFalse(receipt["required_ok"])
        check = receipt["checks"]["forbidden_workspace_files"]
        self.assertGreater(check["scan_errors"], 0)
        self.assertIsNone(check["count"])
        self.assertNotIn("private.key", json.dumps(receipt))

    def test_dependency_link_to_git_root_is_unverified_input(self):
        self.assert_git_input_rejected(".git", True)

    def test_dependency_link_to_git_subdirectory_is_unverified_input(self):
        self.assert_git_input_rejected(".git/review-fixture", True)

    def test_file_link_to_git_metadata_is_unverified_input(self):
        self.assert_git_input_rejected(".git/private.key", False)

    def test_scanned_internal_dependency_link_still_allows_valid_receipt(self):
        target = self.root / "ordinary-input"
        target.mkdir()
        (target / "fixture.txt").write_text("synthetic fixture")
        args = self.pinned_args()
        link = self.root / "tools/research/.venv"
        shutil.rmtree(link)
        self.link(link, target)
        code, receipt = self.invoke_receipt(*args)
        self.assertEqual(code, 0)
        self.assertTrue(receipt["required_ok"])


if __name__ == "__main__":
    unittest.main()
