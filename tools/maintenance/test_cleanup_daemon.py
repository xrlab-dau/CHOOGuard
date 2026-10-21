"""Filesystem contract tests. All writes/deletions stay in TemporaryDirectory."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("cleanup_daemon", Path(__file__).with_name("cleanup_daemon.py"))
daemon = importlib.util.module_from_spec(spec)
spec.loader.exec_module(daemon)


class CleanupContract(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="chooguard-cleanup-contract-")
        self.addCleanup(self.temp.cleanup)
        # macOS /var is a symlink; use the physical fixture root intentionally.
        self.root = Path(self.temp.name).resolve()
        state = self.root / ".state"
        state.mkdir()
        self.write(daemon.AUTHORITY, {"status": "RELEASED", "Native_inflight": False})
        self.write(daemon.QUEUE, {"active_request": None, "pending": []})
        self.write(daemon.POLICY, {"allowed_workers": []})
        (self.root / daemon.QUEUE_LOCK).touch()
        self.config = self.root / ".state/cleanup-daemon/config.json"
        cfg = json.loads(Path(__file__).with_name("cleanup-daemon.example.json").read_text())
        cfg.update(project_root=str(self.root), enabled=True, allowed_roots=[".planning/retired"],
                   protected_paths=[], active_plans=[])
        self.write(str(self.config.relative_to(self.root)), cfg)

    def write(self, path, value):
        file = self.root / path
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text(json.dumps(value))

    def artifact(self, path=".planning/retired/mesh.json"):
        file = self.root / path
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text("retired generated intermediate")
        old = time.time() - 7200
        os.utime(file, (old, old))
        return file

    def cli(self, command, *extra):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = daemon.main(["--config", str(self.config), command, *extra])
        return code, json.loads(output.getvalue())

    def enroll(self, path=".planning/retired/mesh.json", *extra):
        return self.cli("enroll", "--manifest-id", "retired-001", "--path", path,
                        "--reason", "Explicit root retirement after task completion",
                        "--retire-after", "2020-01-01T00:00:00Z", *extra)

    def test_expired_exact_file_removed_and_heartbeat_written(self):
        file = self.artifact()
        self.assertEqual(self.enroll()[0], 0)
        code, result = self.cli("cycle")
        self.assertEqual(code, 0)
        self.assertEqual(result["deleted_files"], 1)
        self.assertFalse(file.exists())
        self.assertIn("last_cycle_utc", result)
        self.assertIn("next_due_utc", result)
        self.assertEqual(self.cli("cycle")[1]["skip_reasons"], {"already_absent": 1})

    def test_dry_run_does_not_remove(self):
        file = self.artifact()
        self.enroll()
        self.assertEqual(self.cli("dry-run")[1]["eligible"], 1)
        self.assertTrue(file.exists())

    def test_changed_snapshot_preserved(self):
        file = self.artifact()
        self.enroll()
        file.write_text("new active work")
        result = self.cli("cycle")[1]
        self.assertTrue(file.exists())
        self.assertEqual(result["deleted_files"], 0)
        self.assertIn("File changed since manifest approval", result["skip_reasons"])

    def test_active_native_lease_yields(self):
        file = self.artifact()
        self.enroll()
        self.write(daemon.AUTHORITY, {"status": "ACTIVE", "Native_inflight": True})
        self.assertEqual(self.cli("cycle")[1]["status"], "native_writer_busy")
        self.assertTrue(file.exists())

    def test_pending_native_request_yields(self):
        file = self.artifact()
        self.enroll()
        self.write(daemon.QUEUE, {"active_request": None, "pending": ["next-author"]})
        self.assertEqual(self.cli("cycle")[1]["status"], "native_writer_busy")
        self.assertTrue(file.exists())

    def test_source_and_range_escape_cannot_enroll(self):
        file = self.artifact("source-evidence/original.pdf")
        self.assertEqual(self.enroll("source-evidence/original.pdf", "--allow-root", "source-evidence")[0], 1)
        self.assertEqual(self.enroll("../outside.json")[0], 1)
        self.assertTrue(file.exists())

    def test_symlink_parent_and_leaf_cannot_enroll(self):
        target = self.artifact("elsewhere/kept.json")
        directory = self.root / ".planning/retired"
        directory.mkdir(parents=True)
        (directory / "leaf").symlink_to(target)
        (directory / "parent").symlink_to(target.parent, target_is_directory=True)
        self.assertEqual(self.enroll(".planning/retired/leaf")[0], 1)
        self.assertEqual(self.enroll(".planning/retired/parent/kept.json")[0], 1)
        self.assertTrue(target.exists())

    def test_new_policy_protection_wins_after_enrollment(self):
        file = self.artifact()
        self.enroll()
        self.write(daemon.POLICY, {"allowed_workers": [{"allowed_scope_prefixes": ["C180_ACTIVE_"],
                   "script_roots": [".planning/retired/"], "may_execute_scene_save": True}]})
        self.assertEqual(self.cli("cycle")[1]["deleted_files"], 0)
        self.assertTrue(file.exists())

    def test_changed_manifest_refuses_and_sets_backoff(self):
        file = self.artifact()
        _, enrolled = self.enroll()
        manifest = self.root / enrolled["manifest"]
        manifest.write_text(manifest.read_text() + " ")
        result = self.cli("cycle")[1]
        self.assertEqual(result["status"], "refused")
        self.assertIn("retry_after_epoch", result)
        self.assertEqual(self.cli("cycle")[1]["status"], "error_backoff")
        self.assertTrue(file.exists())

    def test_disabled_and_no_candidate_cycles_record_heartbeat(self):
        cfg = json.loads(self.config.read_text())
        cfg["enabled"] = False
        self.write(str(self.config.relative_to(self.root)), cfg)
        self.assertEqual(self.cli("cycle")[1]["status"], "disabled")
        cfg["enabled"] = True
        self.write(str(self.config.relative_to(self.root)), cfg)
        result = self.cli("cycle")[1]
        self.assertEqual(result["status"], "complete")
        self.assertEqual(result["scanned"], 0)
        self.assertIn("next_due_utc", result)

    def recurring_fixture(self):
        file = self.artifact(".planning/retired/prev.log")
        cfg = json.loads(self.config.read_text())
        cfg["recurring_expired_files"] = [{"path": ".planning/retired/prev.log",
                                         "quiet_seconds": 3600, "reason": "Root exact temporary leaf rule"}]
        self.write(str(self.config.relative_to(self.root)), cfg)
        first = self.cli("cycle")[1]
        self.assertTrue(file.exists())
        self.assertEqual(first["deleted_files"], 0)
        status = self.root / daemon.STATE_DIR / "status.json"
        prior = json.loads(status.read_text())
        prior["recurring_observations"][".planning/retired/prev.log"]["stable_since_epoch"] -= 7200
        status.write_text(json.dumps(prior))
        return file

    def test_exact_recurring_temp_observed_then_deleted_and_regeneration_waits(self):
        file = self.recurring_fixture()
        with patch.object(daemon, "closed_handles", return_value=(True, "closed")):
            self.assertEqual(self.cli("cycle")[1]["deleted_files"], 1)
        self.assertFalse(file.exists())
        self.artifact(".planning/retired/prev.log")
        with patch.object(daemon, "closed_handles") as query:
            self.assertEqual(self.cli("cycle")[1]["deleted_files"], 0)
            query.assert_not_called()
        self.assertTrue(file.exists())

    def test_recurring_open_handle_or_unknown_query_preserves_file(self):
        file = self.recurring_fixture()
        for detail in ("open_file_handles", "open_handle_check_unavailable"):
            with patch.object(daemon, "closed_handles", return_value=(False, detail)):
                result = self.cli("cycle")[1]
                self.assertEqual(result["deleted_files"], 0)
                self.assertIn(detail, result["skip_reasons"])
                self.assertTrue(file.exists())

    def test_busy_cycle_preserves_recurring_observation_clock(self):
        self.recurring_fixture()
        before = self.cli("status")[1]["recurring_observations"]
        self.write(daemon.AUTHORITY, {"status": "ACTIVE", "Native_inflight": True})
        busy = self.cli("cycle")[1]
        self.assertEqual(busy["status"], "native_writer_busy")
        self.assertEqual(busy["recurring_observations"], before)

    def test_lsof_contract_unknown_is_not_closed(self):
        from subprocess import CompletedProcess, TimeoutExpired
        with patch.object(daemon.subprocess, "run", return_value=CompletedProcess([], 1, "", "")) as query:
            self.assertEqual(daemon.closed_handles(self.root, "one.log"), (True, "closed"))
            self.assertEqual(query.call_args.kwargs["timeout"], 5)
            self.assertEqual(query.call_args.args[0][-1], str(self.root / "one.log"))
        with patch.object(daemon.subprocess, "run", return_value=CompletedProcess([], 0, "123\n", "")):
            self.assertFalse(daemon.closed_handles(self.root, "one.log")[0])
        with patch.object(daemon.subprocess, "run", side_effect=TimeoutExpired("lsof", 5)):
            self.assertFalse(daemon.closed_handles(self.root, "one.log")[0])


if __name__ == "__main__":
    unittest.main()
