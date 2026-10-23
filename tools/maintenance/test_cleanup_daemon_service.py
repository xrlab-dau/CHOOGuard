"""Detached scheduler contract, using only a temporary project/config."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest

SCRIPT = Path(__file__).with_name("cleanup_daemon_service.py").resolve()


class DetachedServiceContract(unittest.TestCase):
    def test_start_cli_exits_but_service_lives_duplicate_start_and_stop(self):
        with tempfile.TemporaryDirectory(prefix="chooguard-cleanup-service-") as temporary:
            root = Path(temporary).resolve()
            state = root / ".state/cleanup-daemon"
            state.mkdir(parents=True)
            config = state / "config.json"
            cfg = json.loads(SCRIPT.with_name("cleanup-daemon.example.json").read_text())
            cfg.update(project_root=str(root), enabled=False, protected_paths=[])
            config.write_text(json.dumps(cfg))

            def command(action):
                env = os.environ.copy()
                env["PYTHONDONTWRITEBYTECODE"] = "1"
                result = subprocess.run([sys.executable, str(SCRIPT), "--config", str(config), action],
                                        text=True, capture_output=True, timeout=50, env=env, check=False)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                return json.loads(result.stdout)

            started = command("start")
            try:
                self.assertEqual(started["start_result"], "started_detached")
                self.assertTrue(started["process_alive_and_identified"])
                # The start command's process has exited. The new session remains.
                status = command("status")
                self.assertEqual(status["pid"], started["pid"])
                self.assertTrue(status["process_alive_and_identified"])
                duplicate = command("start")
                self.assertEqual(duplicate["start_result"], "already_running")
                self.assertEqual(duplicate["pid"], started["pid"])
                due = time.monotonic() + 5
                while not (state / "status.json").exists() and time.monotonic() < due:
                    time.sleep(0.05)
                heartbeat = json.loads((state / "status.json").read_text())
                self.assertEqual(heartbeat["status"], "disabled")
            finally:
                stopped = command("stop")
                self.assertEqual(stopped["stop_result"], "stopped")
            self.assertEqual(command("status")["status"], "stopped")


if __name__ == "__main__":
    unittest.main()
