"""Exercise the Bash entry point using harmless command shims, without installs."""
from __future__ import annotations

import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import tempfile
import unittest


@unittest.skipIf(os.name == "nt", "POSIX command shim tests")
class ShellBootstrapTests(unittest.TestCase):
    def setUp(self):
        self.shell = shutil.which("bash")
        if not self.shell:
            self.skipTest("Bash is not installed")
        scratch = Path(__file__).resolve().parents[2] / ".superpowers"
        scratch.mkdir(exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="shell-bootstrap-", dir=scratch)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        script_dir = self.root / "scripts" / "bootstrap"
        script_dir.mkdir(parents=True)
        self.script = script_dir / "bootstrap.sh"
        shutil.copyfile(Path(__file__).with_name("bootstrap.sh"), self.script)
        (self.root / "tools" / "research").mkdir(parents=True)
        package = self.root / ".pi/npm/node_modules/pi-agents/package.json"
        package.parent.mkdir(parents=True)
        package.write_text('{"version": "0.0.0-fixture"}\n', encoding="utf-8")
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
            '    print("22" if args[:1] == ["-p"] else "v22.0.0")\n'
            'elif tool == "pi": print("0.0.0-fixture")\n'
            'elif tool == "uv" and "sync" not in args: print("uv fixture")\n'
            'elif tool == "python3":\n'
            '    stage = "PREFLIGHT" if "--policy-preflight" in args else "VERIFY"\n'
            '    sys.exit(int(os.environ.get("BOOTSTRAP_TEST_" + stage + "_EXIT", "0")))\n',
            encoding="utf-8",
        )
        for tool in ("git", "node", "pi", "npm", "uv", "python3"):
            command = self.bin / tool
            command.write_text(
                f'#!/bin/sh\nexec {shlex.quote(sys.executable)} {shlex.quote(str(dispatcher))} {tool} "$@"\n',
                encoding="utf-8",
            )
            command.chmod(0o755)

    def run_bootstrap(self, label="local", auto_install=False, verify_args=(), **exit_codes):
        self.log.unlink(missing_ok=True)
        env = dict(os.environ)
        env["PATH"] = str(self.bin) + os.pathsep + env.get("PATH", "")
        env["MACHINE_LABEL"] = label
        env["AUTO_INSTALL"] = "1" if auto_install else "0"
        env["BOOTSTRAP_TEST_LOG"] = str(self.log)
        env.update({"BOOTSTRAP_TEST_" + name: str(value) for name, value in exit_codes.items()})
        result = subprocess.run([self.shell, str(self.script), *verify_args], env=env,
                                capture_output=True, timeout=60)
        self.calls = [json.loads(line) for line in self.log.read_text(encoding="utf-8").splitlines()] if self.log.exists() else []
        self.preflights = [call for call in self.calls if call[0] == "python3" and "--policy-preflight" in call]
        self.final_verifications = [call for call in self.calls if call[0] == "python3" and "--policy-preflight" not in call]
        return result

    def test_failed_preflight_stops_all_tools_even_with_install_authorization(self):
        for auto_install in (False, True):
            with self.subTest(auto_install=auto_install):
                result = self.run_bootstrap(auto_install=auto_install, PREFLIGHT_EXIT=1)
                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertEqual(len(self.preflights), 1)
                self.assertFalse(any(call[0] in ("node", "pi", "uv", "npm") for call in self.calls))
                self.assertEqual(self.final_verifications, [])

    def test_preflight_exit_code_is_preserved(self):
        result = self.run_bootstrap(PREFLIGHT_EXIT=19)
        self.assertEqual(result.returncode, 19, result.stderr)
        self.assertEqual(self.final_verifications, [])

    def test_manifest_and_receipt_arguments_are_forwarded_to_both_stages(self):
        args = ["--policy-manifest", "approved fixture.json", "--policy-manifest-sha256", "a" * 64,
                "--write", "docs/evidence/R-07/fixture receipt.json", "--strict"]
        result = self.run_bootstrap(verify_args=args)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(self.preflights), 1)
        self.assertEqual(len(self.final_verifications), 1)
        for call in self.preflights + self.final_verifications:
            forwarded = [arg for arg in call[1:] if arg != "--policy-preflight"]
            self.assertEqual(forwarded, ["scripts/bootstrap/verify_toolchain.py", "--label", "local", *args])

    def test_equals_arguments_are_forwarded_without_splitting(self):
        args = ["--policy-manifest=approved fixture.json", "--policy-manifest-sha256=" + "a" * 64,
                "--write=docs/evidence/R-07/fixture receipt.json"]
        result = self.run_bootstrap(verify_args=args)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(self.preflights), 1)
        self.assertEqual(self.preflights[0][-len(args):], args)
        self.assertEqual(self.final_verifications[0][-len(args):], args)

    def test_default_preflights_before_tools_and_never_installs_or_syncs(self):
        result = self.run_bootstrap()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(self.preflights), 1)
        self.assertEqual(len(self.final_verifications), 1)
        preflight_index = self.calls.index(self.preflights[0])
        for index, call in enumerate(self.calls):
            if call[0] in ("node", "pi", "uv", "npm"):
                self.assertGreater(index, preflight_index)
        self.assertFalse(any(call[0] == "npm" or call[:2] == ["uv", "sync"] for call in self.calls))
        self.assertEqual(self.calls[-1], self.final_verifications[0])

    def test_approved_preflight_preserves_explicit_install_path(self):
        result = self.run_bootstrap(auto_install=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(self.preflights), 1)
        preflight_index = self.calls.index(self.preflights[0])
        for command in (["npm", "install"], ["uv", "sync"]):
            indices = [i for i, call in enumerate(self.calls) if call[:2] == command]
            self.assertEqual(len(indices), 1)
            self.assertGreater(indices[0], preflight_index)
        self.assertEqual(len(self.final_verifications), 1)

    def test_final_verifier_exit_code_is_preserved(self):
        result = self.run_bootstrap(VERIFY_EXIT=17)
        self.assertEqual(result.returncode, 17, result.stderr)
        self.assertEqual(len(self.preflights), 1)
        self.assertEqual(len(self.final_verifications), 1)

    def test_standalone_verifier_modes_are_rejected_before_any_tool_calls(self):
        for args in (["--policy-preflight"], ["--policy-pre"],
                     ["--write-policy-candidate", "docs/evidence/R-07/candidate.json"],
                     ["--write-policy-candidate=docs/evidence/R-07/candidate.json"],
                     ["--write-policy", "docs/evidence/R-07/candidate.json"],
                     ["--help"], ["--hel"], ["-h"]):
            with self.subTest(args=args):
                result = self.run_bootstrap(auto_install=True, verify_args=args)
                self.assertEqual(result.returncode, 2, result.stderr)
                self.assertEqual(self.calls, [])

    def test_invalid_label_stops_before_tool_calls(self):
        result = self.run_bootstrap(label="invalid-label")
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertEqual(self.calls, [])


if __name__ == "__main__":
    unittest.main()
