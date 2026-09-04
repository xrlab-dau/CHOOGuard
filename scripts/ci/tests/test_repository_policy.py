import importlib.util
import unittest
from pathlib import Path

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


if __name__ == "__main__":
    unittest.main()
