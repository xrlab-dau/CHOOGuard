"""Exercise the Windows entry point with native command shims, without installs."""
from __future__ import annotations

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


@unittest.skipUnless(os.name == "nt", "Windows native command tests")
class PowerShellBootstrapTests(unittest.TestCase):
    def setUp(self):
        self.shell = shutil.which("powershell") or shutil.which("pwsh")
        if not self.shell:
            self.skipTest("PowerShell is not installed")
        repository = Path(__file__).resolve().parents[2]
        scratch = repository / ".superpowers"
        scratch.mkdir(exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="windows-bootstrap-", dir=scratch)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        script_dir = self.root / "scripts" / "bootstrap"
        script_dir.mkdir(parents=True)
        self.script = script_dir / "bootstrap.ps1"
        shutil.copyfile(Path(__file__).with_name("bootstrap.ps1"), self.script)
        (self.root / "tools" / "research").mkdir(parents=True)
        self.bin = self.root / "bin"
        self.bin.mkdir()
        self.log = self.root / "calls.jsonl"
        dispatcher = self.bin / "dispatch.py"
        dispatcher.write_text(
            'import json, os, sys\n'
            'tool, *args = sys.argv[1:]\n'
            'with open(os.environ["BOOTSTRAP_TEST_LOG"], "a", encoding="utf-8") as log:\n'
            '    log.write(json.dumps([tool, *args]) + "\\n")\n'
            'if tool == "git": print("0" * 40 if "HEAD" in args else "fixture")\n'
            'elif tool == "node":\n'
            '    print(os.environ.get("BOOTSTRAP_TEST_NODE_VERSION", "v22.0.0"))\n'
            '    sys.exit(int(os.environ.get("BOOTSTRAP_TEST_NODE_EXIT", "0")))\n'
            'elif tool == "pi": print("0.0.0-fixture")\n'
            'elif tool == "uv" and "sync" not in args: print("uv fixture")\n'
            'elif tool == "npm": sys.exit(int(os.environ.get("BOOTSTRAP_TEST_NPM_EXIT", "0")))\n'
            'elif tool == "uv": sys.exit(int(os.environ.get("BOOTSTRAP_TEST_UV_EXIT", "0")))\n'
            'elif tool == "python": sys.exit(int(os.environ.get("BOOTSTRAP_TEST_VERIFY_EXIT", "0")))\n',
            encoding="utf-8",
        )
        for tool in ("git", "node", "pi", "npm", "uv", "python"):
            (self.bin / (tool + ".cmd")).write_text(
                f'@echo off\n"{sys.executable}" "{dispatcher}" {tool} %*\nexit /b %errorlevel%\n',
                encoding="utf-8",
            )

    def run_bootstrap(self, label="school-pc", auto_install=False, **exit_codes):
        env = dict(os.environ)
        env["PATH"] = str(self.bin) + os.pathsep + env.get("PATH", "")
        env["BOOTSTRAP_TEST_LOG"] = str(self.log)
        env.update({"BOOTSTRAP_TEST_" + name: str(value) for name, value in exit_codes.items()})
        command = [self.shell, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                   "-File", str(self.script), "-MachineLabel", label]
        if auto_install:
            command.append("-AutoInstall")
        result = subprocess.run(command, env=env, capture_output=True, timeout=60)
        self.calls = [json.loads(line) for line in self.log.read_text(encoding="utf-8").splitlines()] if self.log.exists() else []
        return result

    def test_default_never_installs_or_syncs(self):
        result = self.run_bootstrap()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertFalse(any(call[0] == "npm" for call in self.calls))
        self.assertFalse(any(call[:2] == ["uv", "sync"] for call in self.calls))
        self.assertTrue(any(call[0] == "python" for call in self.calls))

    def test_installed_node_version_check_survives_powershell_quoting(self):
        if not shutil.which("node.exe"):
            self.skipTest("Native Node.js is not installed")
        # Keep installation/verifier shims, but execute the actual Node binary.
        (self.bin / "node.cmd").unlink()
        result = self.run_bootstrap()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn(b"SyntaxError", result.stderr)
        self.assertTrue(any(call[0] == "python" for call in self.calls))

    def test_node_failure_stops_before_installs_and_verification(self):
        result = self.run_bootstrap(auto_install=True, NODE_EXIT=19)
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(any(call[0] in ("npm", "uv", "python") for call in self.calls))

    def test_malformed_node_version_stops_before_installs_and_verification(self):
        result = self.run_bootstrap(auto_install=True, NODE_VERSION="invalid")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(any(call[0] in ("npm", "uv", "python") for call in self.calls))

    def test_verifier_exit_code_is_preserved(self):
        result = self.run_bootstrap(VERIFY_EXIT=17)
        self.assertEqual(result.returncode, 17, result.stderr)

    def test_failed_native_npm_stops_before_sync_and_verification(self):
        result = self.run_bootstrap(auto_install=True, NPM_EXIT=23)
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(any(call[:2] == ["npm", "install"] for call in self.calls))
        self.assertFalse(any(call[:2] == ["uv", "sync"] or call[0] == "python" for call in self.calls))

    def test_failed_native_uv_stops_before_verification(self):
        result = self.run_bootstrap(auto_install=True, UV_EXIT=29)
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(any(call[:2] == ["uv", "sync"] for call in self.calls))
        self.assertFalse(any(call[0] == "python" for call in self.calls))

    def test_invalid_label_stops_before_tool_calls(self):
        result = self.run_bootstrap(label="invalid-label")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.calls, [])


if __name__ == "__main__":
    unittest.main()
