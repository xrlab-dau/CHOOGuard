"""부트스트랩 검증기의 실패 차단·영수증 경계를 재현한다.

실행: python3 -m unittest discover -s scripts/bootstrap -p 'test_*.py' -v
테스트는 네트워크와 실제 도구 설치를 호출하지 않는다.
임시 파일은 CLAUDE_JOB_DIR/tmp 또는 이 저장소의 .superpowers 아래에 둔다.
"""
from __future__ import annotations

import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

MODULE_PATH = Path(__file__).with_name("verify_toolchain.py")
spec = importlib.util.spec_from_file_location("verify_toolchain_under_test", MODULE_PATH)
assert spec is not None and spec.loader is not None
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class ToolchainBoundaryTests(unittest.TestCase):
    def setUp(self):
        job = os.environ.get("CLAUDE_JOB_DIR")
        scratch = Path(job) / "tmp" if job else MODULE_PATH.parents[2] / ".superpowers"
        scratch.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="toolchain-test-", dir=scratch)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "repo"
        self.root.mkdir()
        (self.root / ".pi").mkdir()
        (self.root / ".pi/settings.json").write_text('{"packages": []}\n', encoding="utf-8")
        (self.root / "tools/research/.venv").mkdir(parents=True)
        self.tracked = []
        self.uv = "uv 0.11.14"

    def fake_run(self, *cmd):
        if cmd[:2] == ("git", "ls-files"):
            if self.tracked is None:
                return None
            separator = "\0" if "-z" in cmd else "\n"
            return separator.join(self.tracked)
        return {
            ("node", "--version"): "v22.0.0",
            ("pi", "--version"): "0.85.0",
            ("uv", "--version"): self.uv,
            ("git", "branch", "--show-current"): "chore/test-fixture",
            ("git", "rev-parse", "HEAD"): "0" * 40,
        }.get(cmd)

    def invoke(self, *args):
        with patch.object(verifier, "ROOT", self.root), patch.object(verifier, "run", self.fake_run):
            with patch.object(sys, "argv", ["verify_toolchain.py", "--label", "local", *args]):
                with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                    try:
                        return verifier.main()
                    except SystemExit as error:
                        return error.code

    def test_missing_uv_fails_strict(self):
        self.uv = None
        self.assertNotEqual(self.invoke("--strict"), 0)

    def test_missing_research_environment_fails_strict(self):
        (self.root / "tools/research/.venv").rmdir()
        self.assertNotEqual(self.invoke("--strict"), 0)

    def test_failures_are_nonzero_without_strict_flag(self):
        self.tracked = ["private.key"]
        self.assertNotEqual(self.invoke(), 0)

    def test_env_variants_are_rejected(self):
        for name in [".env.local", ".env.production", "config/.env.staging"]:
            with self.subTest(name=name):
                self.tracked = [name]
                self.assertNotEqual(self.invoke("--strict"), 0)

    def test_env_example_is_not_a_secret_file(self):
        self.tracked = ["tools/research/.env.example"]
        self.assertEqual(self.invoke("--strict"), 0)

    def test_capture_extensions_are_rejected(self):
        for suffix in ["mp4", "mov", "mxf", "arw", "cr2", "nef", "dng", "raw", "PLY"]:
            with self.subTest(suffix=suffix):
                self.tracked = [f"fixture.{suffix}"]
                self.assertNotEqual(self.invoke("--strict"), 0)

    def test_receipt_cannot_escape_repository(self):
        target = self.root.parent / "outside.json"
        self.assertNotEqual(self.invoke("--write", "../outside.json", "--strict"), 0)
        self.assertFalse(target.exists())

    def test_receipt_cannot_overwrite_policy(self):
        target = self.root / "AGENTS.md"
        target.write_text("policy fixture", encoding="utf-8")
        self.assertNotEqual(self.invoke("--write", "AGENTS.md", "--strict"), 0)
        self.assertEqual(target.read_text(encoding="utf-8"), "policy fixture")

    def test_existing_receipt_is_append_only(self):
        target = self.root / "docs/evidence/M0-00/verify-local.json"
        target.parent.mkdir(parents=True)
        target.write_text("original receipt", encoding="utf-8")
        self.assertNotEqual(self.invoke("--write", "docs/evidence/M0-00/verify-local.json", "--strict"), 0)
        self.assertEqual(target.read_text(encoding="utf-8"), "original receipt")

    def test_package_tree_link_cannot_escape(self):
        link = self.root / ".pi/npm"
        outside = self.root.parent / "external-packages"
        outside.mkdir()
        try:
            link.symlink_to(outside, target_is_directory=True)
        except OSError:
            self.skipTest("이 환경에서는 심볼릭 링크 생성 권한이 없다")
        self.assertNotEqual(self.invoke("--strict"), 0)

    def test_failed_command_output_is_not_success(self):
        failed = subprocess.CompletedProcess(["node", "--version"], 1, stdout="v22.0.0", stderr="")
        with patch.object(verifier.shutil, "which", return_value="/fixture/node"):
            with patch.object(verifier.subprocess, "run", return_value=failed):
                self.assertIsNone(verifier.run("node", "--version"))

    def test_nested_disallowed_settings_keys_are_rejected(self):
        (self.root / ".pi/settings.json").write_text(
            '{"packages": [], "extensions": [{"mcpServers": {"unity": {}}}]}\n', encoding="utf-8"
        )
        self.assertNotEqual(self.invoke("--strict"), 0)

    def test_tracked_listing_failure_is_not_clean(self):
        self.tracked = None
        self.assertNotEqual(self.invoke("--strict"), 0)

    def test_required_hash_scope_covers_governance(self):
        required = {".github/CODEOWNERS", ".github/workflows/*.yml", "docs/adr/*.md"}
        self.assertTrue(required.issubset(set(verifier.POLICY_GLOBS)))


class BootstrapScriptTests(unittest.TestCase):
    """bootstrap.sh 는 라벨 없이 진행하지 않고, 승인 없이 설치·동기화하지 않는다."""

    SCRIPT = MODULE_PATH.with_name("bootstrap.sh")

    def setUp(self):
        if shutil.which("bash") is None:
            self.skipTest("bash 가 없다")
        job = os.environ.get("CLAUDE_JOB_DIR")
        scratch = Path(job) / "tmp" if job else MODULE_PATH.parents[2] / ".superpowers"
        scratch.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="bootstrap-test-", dir=scratch)
        self.addCleanup(self.temporary.cleanup)
        self.fake_bin = Path(self.temporary.name) / "bin"
        self.fake_bin.mkdir()
        self.call_log = Path(self.temporary.name) / "uv-calls.log"
        fake_uv = self.fake_bin / "uv"
        fake_uv.write_text(
            '#!/usr/bin/env bash\n'
            'printf "%s\\n" "$*" >> "$UV_CALL_LOG"\n'
            'if [ "$1" = "--version" ]; then echo "uv 0.0.0-fixture"; fi\n'
            'exit 0\n',
            encoding="utf-8",
        )
        fake_uv.chmod(0o755)

    def run_script(self, **extra_env):
        env = {k: v for k, v in os.environ.items() if k not in {"AUTO_INSTALL", "MACHINE_LABEL"}}
        env["PATH"] = f"{self.fake_bin}{os.pathsep}{env.get('PATH', '')}"
        env["UV_CALL_LOG"] = str(self.call_log)
        env.update(extra_env)
        return subprocess.run(["bash", str(self.SCRIPT)], env=env, capture_output=True, text=True, timeout=180)

    def uv_calls(self):
        return self.call_log.read_text(encoding="utf-8").splitlines() if self.call_log.exists() else []

    def test_missing_machine_label_stops_before_any_tool_call(self):
        result = self.run_script()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.uv_calls(), [])

    def test_sync_requires_explicit_install_approval(self):
        self.run_script(MACHINE_LABEL="local")
        self.assertFalse(any(call.startswith("sync") for call in self.uv_calls()))


if __name__ == "__main__":
    unittest.main()
