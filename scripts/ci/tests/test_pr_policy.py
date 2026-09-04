import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "pr_policy.py"
SPEC = importlib.util.spec_from_file_location("pr_policy", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
SPEC.loader.exec_module(MODULE)


class PullRequestPolicyPatternsTest(unittest.TestCase):
    def test_develop_branch_patterns(self):
        self.assertTrue(MODULE.DEVELOP_SOURCES.match("feature/12-xr-rig"))
        self.assertTrue(MODULE.DEVELOP_SOURCES.match("experiment/da3-ply"))
        self.assertTrue(MODULE.DEVELOP_SOURCES.match("dependabot/pip/reconstruction/ruff-1.0"))
        self.assertFalse(MODULE.DEVELOP_SOURCES.match("release/0.1.0"))

    def test_main_branch_patterns(self):
        self.assertTrue(MODULE.MAIN_SOURCES.match("release/0.1.0"))
        self.assertTrue(MODULE.MAIN_SOURCES.match("hotfix/v0.1.1"))
        self.assertFalse(MODULE.MAIN_SOURCES.match("feature/foo"))

    def test_conventional_title(self):
        self.assertTrue(MODULE.TITLE.match("feat(xr): add emergency interaction"))
        self.assertFalse(MODULE.TITLE.match("updated stuff"))


if __name__ == "__main__":
    unittest.main()
