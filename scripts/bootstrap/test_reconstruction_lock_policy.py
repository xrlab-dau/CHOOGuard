"""FR-006 dependency-lock scope; every policy pin and tool is synthetic."""
from __future__ import annotations

import json
import unittest
from unittest.mock import patch

import test_policy_baseline as policy_tests

verifier = policy_tests.verifier
KNOWN_LOCKS = (
    "reconstruction/requirements-colmap.lock",
    "reconstruction/requirements-da3.lock",
    "reconstruction/requirements-mapanything.lock",
)
EXTRA_LOCK = "reconstruction/requirements-next-engine.lock"


class ReconstructionLockPolicyTests(unittest.TestCase):
    def setUp(self):
        self.fixture = policy_tests.PolicyBaselineTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.case = self.fixture.case
        self.root = self.fixture.root
        for name in KNOWN_LOCKS:
            (self.root / name).write_text("synthetic-dependency==1.0.0\n", encoding="utf-8")
        self.case.tracked = list(KNOWN_LOCKS)

    def pin(self):
        self.digest = self.fixture.write_manifest()
        self.manifest_bytes = self.fixture.manifest.read_bytes()
        self.arguments = ("--policy-manifest", str(self.fixture.manifest),
                          "--policy-manifest-sha256", self.digest)
        self.assertTrue(self.fixture.compare(self.digest)["ok"])

    def assert_mutation_blocked(self, name, diagnostic, run_id):
        calls = []
        original = self.case.fake_run

        def record(*command):
            calls.append(command)
            return original(*command)

        target = f"docs/evidence/R-07/lock-{run_id}.json"
        with patch.object(self.case, "fake_run", side_effect=record), \
                patch.object(verifier, "check_packages", wraps=verifier.check_packages) as packages:
            preflight_code, output = self.fixture.invoke_preflight(*self.arguments)
            code = self.case.invoke(*self.arguments, "--write", target)
        observation = json.loads(output)
        receipt = json.loads((self.root / target).read_text(encoding="utf-8"))
        details = {"preflight_exit": preflight_code, "receipt_exit": code,
                   "required_ok": receipt["required_ok"],
                   "tool_probes": receipt["tool_probes"], "calls": calls}
        self.assertEqual(self.fixture.manifest.read_bytes(), self.manifest_bytes)
        self.assertEqual(preflight_code, 1, details)
        self.assertFalse(observation["policy_preflight_ok"])
        self.assertFalse(observation["policy_baseline"]["ok"])
        self.assertIn(name, observation["policy_baseline"][diagnostic])
        self.assertEqual(code, 1, details)
        self.assertFalse(receipt["required_ok"])
        self.assertFalse(receipt["checks"]["policy_baseline"]["ok"])
        self.assertIn(name, receipt["checks"]["policy_baseline"][diagnostic])
        self.assertFalse(receipt["tool_probes"]["executed"])
        self.assertFalse(any(command[0] in {"node", "pi", "uv"} for command in calls))
        packages.assert_not_called()

    def test_changed_reconstruction_locks_block_original_pin(self):
        self.pin()
        for index, name in enumerate(KNOWN_LOCKS):
            with self.subTest(lock=name):
                path = self.root / name
                original = path.read_bytes()
                try:
                    path.write_text("synthetic-dependency==2.0.0\n", encoding="utf-8")
                    self.assert_mutation_blocked(name, "changed_paths", f"changed-{index}")
                finally:
                    path.write_bytes(original)

    def test_added_reconstruction_lock_blocks_original_pin(self):
        self.pin()
        (self.root / EXTRA_LOCK).write_text("synthetic-dependency==1.0.0\n", encoding="utf-8")
        self.case.tracked.append(EXTRA_LOCK)
        self.assert_mutation_blocked(EXTRA_LOCK, "unexpected_paths", "added")

    def test_deleted_reconstruction_locks_block_original_pin(self):
        (self.root / EXTRA_LOCK).write_text("synthetic-dependency==1.0.0\n", encoding="utf-8")
        self.case.tracked.append(EXTRA_LOCK)
        self.pin()
        for index, name in enumerate((*KNOWN_LOCKS, EXTRA_LOCK)):
            with self.subTest(lock=name):
                path = self.root / name
                original = path.read_bytes()
                try:
                    path.unlink()
                    self.assert_mutation_blocked(name, "missing_paths", f"deleted-{index}")
                finally:
                    path.write_bytes(original)

    def test_each_known_reconstruction_lock_is_required_before_candidate(self):
        for index, name in enumerate(KNOWN_LOCKS):
            with self.subTest(lock=name):
                path = self.root / name
                original = path.read_bytes()
                target = f"docs/evidence/R-07/missing-lock-{index}.json"
                try:
                    path.unlink()
                    code = self.case.invoke("--write-policy-candidate", target)
                    self.assertEqual(code, 1)
                    self.assertIn(name, verifier.policy_inventory(self.root)[1])
                    self.assertFalse((self.root / target).exists())
                finally:
                    path.write_bytes(original)

    def test_candidate_includes_known_and_new_reconstruction_locks(self):
        (self.root / EXTRA_LOCK).write_text("synthetic-dependency==1.0.0\n", encoding="utf-8")
        self.case.tracked.append(EXTRA_LOCK)
        target = "docs/evidence/R-07/lock-candidate.json"
        self.assertEqual(self.case.invoke("--write-policy-candidate", target), 0)
        candidate = json.loads((self.root / target).read_text(encoding="utf-8"))
        self.assertEqual(candidate["status"], "candidate")
        self.assertIsNone(candidate["approvalReference"])
        for name in (*KNOWN_LOCKS, EXTRA_LOCK):
            with self.subTest(lock=name):
                self.assertEqual(candidate["policySha256"].get(name), verifier.sha256(self.root / name))


if __name__ == "__main__":
    unittest.main()
