import tempfile
import unittest
from pathlib import Path
from unittest import mock

import build_foundation_candidates as builder


class FoundationBuildContractTests(unittest.TestCase):
    def test_targets_are_a_known_duplicate_free_subset(self):
        self.assertEqual(builder.parse_targets(",".join(reversed(builder.TARGETS))), list(builder.TARGETS))
        for text in ("", "windows-server,windows-server", "windows-server,android", "quest"):
            with self.assertRaises(ValueError):
                builder.parse_targets(text)

    def test_existing_output_root_is_never_reused(self):
        with tempfile.TemporaryDirectory() as tmp:
            fresh = builder.new_output_root(Path(tmp) / "run")
            self.assertTrue(fresh.is_dir())
            with self.assertRaises(ValueError):
                builder.new_output_root(fresh)

    def test_only_a_verified_positive_launch_is_runnable(self):
        self.assertEqual(builder.classify(False, False, None), "build_failed")
        self.assertEqual(builder.classify(True, False, "passed"), "built_launch_failed")
        self.assertEqual(builder.classify(True, True, "failed"), "built_launch_failed")
        self.assertEqual(builder.classify(True, True, None), "built_launch_failed")
        self.assertEqual(builder.classify(True, True, "blocked"), "built_launch_blocked")
        self.assertEqual(builder.classify(True, True, "passed"), builder.RUNNABLE)
        records = [
            {"target": "a", "status": builder.RUNNABLE, "payload": {"verified": True}, "launch": {"status": "passed"}},
            {"target": "b", "status": builder.RUNNABLE, "payload": {"verified": False}, "launch": {"status": "passed"}},
            {"target": "c", "status": "blocked", "blockedReason": "module missing"},
            {"target": "d", "status": "build_failed", "payload": {"verified": True}, "launch": {"status": "passed"}},
        ]
        self.assertEqual([r["target"] for r in builder.runnable(records)], ["a"])

    def test_every_unity_target_has_a_unique_output_and_blocked_record_without_support(self):
        builds = [spec["build"] for spec in builder.UNITY_TARGETS.values()]
        self.assertEqual(len(builds), len(set(builds)))
        self.assertEqual(set(builder.UNITY_TARGETS) | {"livekit"}, set(builder.TARGETS))
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            unity = root / "Editor" / "Unity.exe"
            unity.parent.mkdir()
            unity.write_text("")
            project, run_dir = root / "project", root / "run"
            project.mkdir()
            run_dir.mkdir()
            refused = {"argv": [], "exitCode": 1, "seconds": 0, "log": "", "logSha256": None}
            with mock.patch.object(builder, "run_logged", return_value=refused):
                record = builder.build_unity_target("mac-development", project, unity, run_dir)
            self.assertEqual(record["status"], "blocked")
            self.assertFalse(record["capability"]["available"])
            self.assertIn("MacStandaloneSupport", record["blockedReason"])
            self.assertNotIn("payload", record)
            (project / builder.UNITY_TARGETS["linux-server"]["build"]).mkdir(parents=True)
            record = builder.build_unity_target("linux-server", project, unity, run_dir)
            self.assertEqual(record["status"], "blocked")
            self.assertIn("already exists", record["blockedReason"])


if __name__ == "__main__":
    unittest.main()
