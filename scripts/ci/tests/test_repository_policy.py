import importlib.util
import subprocess
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


if __name__ == "__main__":
    unittest.main()
