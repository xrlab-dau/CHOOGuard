"""FR-006 dependency inventory; every policy pin and tool is synthetic."""
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
    known_paths = KNOWN_LOCKS
    extra_path = EXTRA_LOCK
    original_content = "synthetic-dependency==1.0.0\n"
    changed_content = "synthetic-dependency==2.0.0\n"

    def setUp(self):
        self.fixture = policy_tests.PolicyBaselineTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.case = self.fixture.case
        self.root = self.fixture.root
        for name in self.known_paths:
            self.write_dependency(name)
        self.case.tracked = list(self.known_paths)

    def write_dependency(self, name):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(self.original_content, encoding="utf-8")

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

    def test_changed_dependencies_block_original_pin(self):
        self.pin()
        for index, name in enumerate(self.known_paths):
            with self.subTest(dependency=name):
                path = self.root / name
                original = path.read_bytes()
                try:
                    path.write_text(self.changed_content, encoding="utf-8")
                    self.assert_mutation_blocked(name, "changed_paths", f"changed-{index}")
                finally:
                    path.write_bytes(original)

    def test_added_dependency_blocks_original_pin(self):
        self.pin()
        self.write_dependency(self.extra_path)
        self.case.tracked.append(self.extra_path)
        self.assert_mutation_blocked(self.extra_path, "unexpected_paths", "added")

    def test_deleted_dependencies_block_original_pin(self):
        self.write_dependency(self.extra_path)
        self.case.tracked.append(self.extra_path)
        self.pin()
        for index, name in enumerate((*self.known_paths, self.extra_path)):
            with self.subTest(dependency=name):
                path = self.root / name
                original = path.read_bytes()
                try:
                    path.unlink()
                    self.assert_mutation_blocked(name, "missing_paths", f"deleted-{index}")
                finally:
                    path.write_bytes(original)

    def test_known_dependency_presence_matches_scope(self):
        for index, name in enumerate(self.known_paths):
            with self.subTest(dependency=name):
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

    def test_candidate_includes_known_and_new_dependencies(self):
        self.write_dependency(self.extra_path)
        self.case.tracked.append(self.extra_path)
        target = "docs/evidence/R-07/lock-candidate.json"
        self.assertEqual(self.case.invoke("--write-policy-candidate", target), 0)
        candidate = json.loads((self.root / target).read_text(encoding="utf-8"))
        self.assertEqual(candidate["status"], "candidate")
        self.assertIsNone(candidate["approvalReference"])
        for name in (*self.known_paths, self.extra_path):
            with self.subTest(dependency=name):
                self.assertEqual(candidate["policySha256"].get(name), verifier.sha256(self.root / name))


class ReconstructionInputPolicyTests(ReconstructionLockPolicyTests):
    known_paths = (
        "reconstruction/requirements-colmap.in",
        "reconstruction/requirements-da3.in",
        "reconstruction/requirements-mapanything.in",
    )
    extra_path = "reconstruction/requirements-next-engine.in"


class EmbeddedUnityPackagePolicyTests(ReconstructionLockPolicyTests):
    known_paths = ("Packages/com.xrlab.chooguard.foundation/package.json",)
    extra_path = "Packages/com.example.synthetic/package.json"
    original_content = '{"dependencies": {"com.example.synthetic-dependency": "1.0.0"}}\n'
    changed_content = '{"dependencies": {"com.example.synthetic-dependency": "2.0.0"}}\n'


class DocsRequirementsPolicyTests(ReconstructionLockPolicyTests):
    known_paths = ("scripts/docs/requirements.txt",)
    extra_path = "scripts/docs/requirements-extra.txt"


class CSharpReviewProjectPolicyTests(ReconstructionLockPolicyTests):
    known_paths = ("scripts/dev/csharp-review/FoundationReview.csproj",)
    extra_path = "scripts/dev/csharp-review/ExtraReview.csproj"
    original_content = '<Project><ItemGroup><PackageReference Include="Synthetic.Dependency" Version="1.0.0" /></ItemGroup></Project>\n'
    changed_content = '<Project><ItemGroup><PackageReference Include="Synthetic.Dependency" Version="2.0.0" /></ItemGroup></Project>\n'

    def test_known_dependency_presence_matches_scope(self):
        # This branch has no C# review project. Future branch integration must
        # enter the reviewed set without inventing a required absent file now.
        for path in self.root.glob("scripts/dev/csharp-review/*.csproj"):
            path.unlink()
        self.case.tracked = []
        self.pin()
        code, output = self.fixture.invoke_preflight(*self.arguments)
        self.assertEqual(code, 0)
        self.assertTrue(json.loads(output)["policy_preflight_ok"])
        self.assertEqual(self.case.invoke(*self.arguments), 0)
        target = "docs/evidence/R-07/no-csharp-project-candidate.json"
        self.assertEqual(self.case.invoke("--write-policy-candidate", target), 0)


if __name__ == "__main__":
    unittest.main()
