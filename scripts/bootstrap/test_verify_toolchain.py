"""부트스트랩 검증기의 실패 차단·영수증 경계를 재현한다.

실행: python3 -m unittest discover -s scripts/bootstrap -p 'test_*.py' -v
테스트는 네트워크와 실제 도구 설치를 호출하지 않는다.
임시 파일은 CLAUDE_JOB_DIR/tmp 또는 이 저장소의 .superpowers 아래에 둔다.
"""
from __future__ import annotations

import contextlib
import hashlib
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
        self.write_required_policy_files()
        self.tracked = []
        self.uv = "uv 0.11.14"
        self.use_synthetic_baseline = True
        for pattern in verifier.POLICY_GLOBS:
            if not list(self.root.glob(pattern)):
                target = self.root / pattern.replace("*", "fixture")
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("{}\n" if target.suffix == ".json" else "# fixture\n")
        files = {path.relative_to(self.root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                 for pattern in verifier.POLICY_GLOBS for path in self.root.glob(pattern) if path.is_file()}
        self.baseline = self.root.parent / "synthetic-baseline.json"
        self.baseline.write_text(json.dumps({"schemaVersion": 1, "status": "approved",
            "approvalReference": "SYNTHETIC-TEST-ONLY", "sourceCommit": "0" * 40, "policySha256": files}))
        self.baseline_digest = hashlib.sha256(self.baseline.read_bytes()).hexdigest()

    def baseline_arguments(self, args):
        if self.use_synthetic_baseline and "--policy-manifest" not in args and "--write-policy-candidate" not in args:
            return (*args, "--policy-manifest", str(self.baseline), "--policy-manifest-sha256", self.baseline_digest)
        return args

    def test_complete_synthetic_baseline_allows_healthy_tools(self):
        self.assertEqual(self.invoke(), 0)

    def test_real_git_unicode_paths_ignore_system_text_encoding(self):
        subprocess.run(["git", "init", "-q", str(self.root)], check=True)
        name = " 한글 경로.md"
        (self.root / name).write_text("fixture", encoding="utf-8")
        subprocess.run(["git", "-C", str(self.root), "add", "--", name], check=True)
        with patch.object(verifier, "ROOT", self.root), patch.object(subprocess, "_text_encoding", return_value="cp949"):
            self.assertIn(name, verifier.tracked_files())

    def test_invalid_command_output_fails_closed_without_reader_thread_error(self):
        with patch.object(verifier, "ROOT", self.root):
            result = verifier.run(sys.executable, "-c", "import sys; sys.stdout.buffer.write(bytes([255]))")
        self.assertIsNone(result)

    def write_required_policy_files(self):
        """필수 정책 파일이 모두 있는 정상 픽스처. 개별 테스트가 지워서 누락을 재현한다."""
        (self.root / "AGENTS.md").write_text("# agents\n", encoding="utf-8")
        (self.root / ".github/workflows").mkdir(parents=True, exist_ok=True)
        (self.root / ".github/CODEOWNERS").write_text("* @team\n", encoding="utf-8")
        (self.root / ".github/workflows/gate.yml").write_text("name: gate\n", encoding="utf-8")
        (self.root / "scripts/ci").mkdir(parents=True, exist_ok=True)
        (self.root / "scripts/ci/repository_policy.py").write_text("# policy\n", encoding="utf-8")

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
        args = self.baseline_arguments(args)
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
        # The file-name exception is independent of the new policy approval gate.
        self.assertIn("[ok] forbidden_tracked_files", self.failed_checks("--strict"))

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

    def failed_checks(self, *args):
        """검사 이름별 ok 를 돌려준다. 종료 코드만 보면 다른 검사 실패로 오통과한다."""
        captured = io.StringIO()
        args = self.baseline_arguments(args)
        with patch.object(verifier, "ROOT", self.root), patch.object(verifier, "run", self.fake_run):
            with patch.object(sys, "argv", ["verify_toolchain.py", "--label", "local", *args]):
                with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(io.StringIO()):
                    try:
                        verifier.main()
                    except SystemExit:
                        pass
        return captured.getvalue()

    def assert_check_failed(self, name, *args):
        """해당 check 가 실패하고, 필수 검사에 속하며, 종료 코드가 0이 아님을 함께 본다.

        stdout 문자열만 보면 '진단은 출력하되 필수 검사에서 뺀' 변형을 놓친다.
        """
        self.assertIn(f"[FAIL] {name}", self.failed_checks(*args))
        self.assertIn(name, verifier.REQUIRED_CHECKS)
        self.assertNotEqual(self.invoke("--strict", *args), 0)

    def test_untracked_capture_file_is_rejected(self):
        """미추적 촬영 원본도 위생 검사 대상이다. git ls-files 목록에만 의존하지 않는다."""
        (self.root / "capture.mp4").write_bytes(b"stub")
        self.assert_check_failed("forbidden_workspace_files")
        self.assertNotEqual(self.invoke("--strict"), 0)

    def test_capture_file_inside_pruned_directory_is_rejected(self):
        """.venv·node_modules 안에 숨긴 금지 파일도 잡는다. prune 이 우회 경로가 되면 안 된다."""
        for parent in (".venv", "node_modules", "__pycache__"):
            with self.subTest(parent=parent):
                hidden = self.root / parent / "capture.mp4"
                hidden.parent.mkdir(parents=True, exist_ok=True)
                hidden.write_bytes(b"stub")
                try:
                    self.assert_check_failed("forbidden_workspace_files")
                finally:
                    hidden.unlink()

    def test_capture_original_inside_dependency_tree_is_rejected(self):
        """의존성 트리 안이라도 촬영 원본·자격 파일은 잡는다. 이것이 원 지적의 대상이다."""
        for rel in ("tools/research/.venv/lib/site-packages/capture.mp4",
                    ".pi/npm/node_modules/pkg/session.NEF",
                    "tools/research/.venv/lib/site-packages/license.ulf"):
            with self.subTest(rel=rel):
                target = self.root / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(b"stub")
                try:
                    self.assert_check_failed("forbidden_workspace_files")
                finally:
                    target.unlink()

    def test_model_weights_inside_dependency_root_are_still_rejected(self):
        """검증된 의존성 루트 안이라도 모델·재구성 자산은 차단한다.

        실측상 venv·npm 루트에 존재하는 것은 cacert.pem 과 _virtualenv.pth 뿐이다.
        나머지 확장자를 면제할 근거가 없다.
        """
        for name in ("model.pt", "weights.safetensors", "recon.ply", "scene.glb", "net.onnx"):
            with self.subTest(name=name):
                target = self.root / "tools/research/.venv/lib/site-packages" / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(b"stub")
                try:
                    self.assert_check_failed("forbidden_workspace_files")
                finally:
                    target.unlink()

    def test_dependency_tree_pem_and_pth_are_not_false_positives(self):
        """site-packages·node_modules 의 CA 번들과 경로 파일은 오탐하지 않는다.

        `*.pem`·`*.pth` 는 각각 자격·모델 가중치 패턴이지만 의존성 트리에서는
        certifi 인증서와 Python path 설정 파일이라 의미가 다르다.
        """
        for rel in ("tools/research/.venv/lib/site-packages/certifi/cacert.pem",
                    "tools/research/.venv/lib/site-packages/_virtualenv.pth"):
            target = self.root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"stub")
        self.assertNotIn("[FAIL] forbidden_workspace_files", self.failed_checks())

    def test_spoofed_dependency_directory_does_not_grant_exemption(self):
        """아무 데나 만든 site-packages·node_modules 는 면제 근거가 아니다.

        면제는 검증된 의존성 루트에만 적용한다. 경로 세그먼트 이름만 보면
        자격·가중치·재구성 자산을 그 이름의 디렉터리에 숨길 수 있다.
        """
        for rel in ("fake/site-packages/client.pem",
                    "docs/node_modules/model.pt",
                    "site-packages/weights.safetensors"):
            with self.subTest(rel=rel):
                target = self.root / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(b"stub")
                try:
                    self.assert_check_failed("forbidden_workspace_files")
                finally:
                    target.unlink()

    def test_unreadable_directory_fails_closed(self):
        """접근 거부된 디렉터리를 조용히 건너뛰고 통과하면 안 된다.

        os.walk 는 기본적으로 오류를 삼킨다. 읽을 수 없는 트리 안에
        금지 파일이 있어도 검사가 보지 못하므로 사유를 남기고 실패해야 한다.
        """
        blocked = self.root / "blocked"
        blocked.mkdir()
        (blocked / "capture.mp4").write_bytes(b"stub")
        # chmod does not deny directory reads on Windows. Exercise the real
        # os.walk error callback on every platform without changing OS ACLs.
        original_scandir = os.scandir

        def denied_scandir(path):
            if Path(path) == blocked:
                raise PermissionError(13, "Access denied", str(blocked))
            return original_scandir(path)

        with patch.object(os, "scandir", side_effect=denied_scandir):
            found, truncated, errors = verifier.forbidden_workspace_files(self.root)
            self.assertEqual(found, [])
            self.assertFalse(truncated)
            self.assertEqual(errors, [str(blocked)])
            self.assert_check_failed("forbidden_workspace_files")

    @unittest.skipUnless(hasattr(os, "geteuid"), "POSIX permissions are unavailable")
    def test_unreadable_directory_posix_permissions_fail_closed(self):
        """Retain the real POSIX permissions check in addition to fault injection."""
        if os.geteuid() == 0:
            self.skipTest("root 는 권한 거부를 재현할 수 없다")
        blocked = self.root / "blocked"
        blocked.mkdir()
        (blocked / "capture.mp4").write_bytes(b"stub")
        blocked.chmod(0o000)
        self.addCleanup(blocked.chmod, 0o755)
        self.assert_check_failed("forbidden_workspace_files")

    def test_truncated_scan_does_not_report_zero_findings(self):
        """예산 초과 영수증이 '금지 파일 없음'으로 읽히면 안 된다."""
        with patch.object(verifier, "WORKSPACE_SCAN_MAX_FILES", 1):
            receipt_dir = self.root / "docs/evidence/M0-00"
            receipt_dir.mkdir(parents=True)
            target = "docs/evidence/M0-00/trunc.json"
            self.invoke("--write", target)
            data = json.loads((self.root / target).read_text(encoding="utf-8"))
        check = data["checks"]["forbidden_workspace_files"]
        self.assertFalse(check["ok"])
        self.assertTrue(check["scan_truncated"])
        self.assertIsNone(check["count"], "순회가 잘렸으면 개수를 안다고 주장하지 않는다")

    def test_ambiguous_extension_outside_dependency_tree_is_rejected(self):
        """같은 확장자라도 의존성 트리 밖이면 잡는다."""
        (self.root / "keys").mkdir()
        (self.root / "keys/server.pem").write_bytes(b"stub")
        self.assert_check_failed("forbidden_workspace_files")

    def test_workspace_scan_budget_fails_closed(self):
        """순회 예산을 넘기면 통과가 아니라 사유를 남기고 실패한다."""
        with patch.object(verifier, "WORKSPACE_SCAN_MAX_FILES", 1):
            self.assert_check_failed("forbidden_workspace_files")

    def test_missing_required_policy_file_fails(self):
        """필수 정책 파일이 없으면 해시 범위가 무의미하므로 필수 검사가 실패한다."""
        (self.root / ".github/CODEOWNERS").unlink()
        self.assert_check_failed("policy_hash_scope")
        self.assertNotEqual(self.invoke("--strict"), 0)

    def test_empty_required_policy_file_does_not_satisfy_scope(self):
        """빈 파일 하나로 필수 정책 범위를 통과시킬 수 없다."""
        (self.root / ".github/CODEOWNERS").write_text("", encoding="utf-8")
        self.assert_check_failed("policy_hash_scope")

    def test_receipt_does_not_publish_forbidden_paths(self):
        """영수증에 금지 파일의 정확한 경로를 남기지 않는다. 촬영 파일명이 공개될 수 있다."""
        (self.root / "korail-secret-location.mp4").write_bytes(b"stub")
        receipt_dir = self.root / "docs/evidence/M0-00"
        receipt_dir.mkdir(parents=True)
        target = "docs/evidence/M0-00/r.json"
        self.invoke("--write", target)
        written = (self.root / target).read_text(encoding="utf-8")
        self.assertNotIn("korail-secret-location", written)
        self.assertIn("forbidden_workspace_files", written)

    def test_label_outside_allowed_values_is_rejected(self):
        """허용값 밖 라벨(호스트명 형태 포함)은 거부한다.

        커버리지 추가다. parser 의 허용값 강제는 이미 구현돼 있고 이 테스트는
        그 동작에 회귀 방지를 건다. RED 를 거친 결함 수정이 아니다.
        """
        for label in ("lab-pc-03", "invalid", "LOCAL"):
            with self.subTest(label=label):
                with patch.object(verifier, "ROOT", self.root), patch.object(verifier, "run", self.fake_run):
                    with patch.object(sys, "argv", ["verify_toolchain.py", "--label", label]):
                        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                            with self.assertRaises(SystemExit) as caught:
                                verifier.main()
                self.assertNotEqual(caught.exception.code, 0)

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
        self.npm_log = Path(self.temporary.name) / "npm-calls.log"
        fake_npm = self.fake_bin / "npm"
        fake_npm.write_text(
            '#!/usr/bin/env bash\n'
            'printf "%s\\n" "$*" >> "$NPM_CALL_LOG"\n'
            'exit 0\n',
            encoding="utf-8",
        )
        fake_npm.chmod(0o755)
        # pi 버전을 일부러 어긋나게 해 npm 설치 분기가 결정적으로 도달하게 한다.
        fake_pi = self.fake_bin / "pi"
        fake_pi.write_text('#!/usr/bin/env bash\necho "0.0.0-fixture"\n', encoding="utf-8")
        fake_pi.chmod(0o755)

    def run_script(self, **extra_env):
        env = {k: v for k, v in os.environ.items() if k not in {"AUTO_INSTALL", "MACHINE_LABEL"}}
        env["PATH"] = f"{self.fake_bin}{os.pathsep}{env.get('PATH', '')}"
        env["UV_CALL_LOG"] = str(self.call_log)
        env["NPM_CALL_LOG"] = str(self.npm_log)
        env.update(extra_env)
        return subprocess.run(["bash", str(self.SCRIPT)], env=env, capture_output=True, text=True, timeout=180)

    def uv_calls(self):
        return self.call_log.read_text(encoding="utf-8").splitlines() if self.call_log.exists() else []

    def npm_calls(self):
        return self.npm_log.read_text(encoding="utf-8").splitlines() if self.npm_log.exists() else []

    def test_global_install_requires_explicit_approval(self):
        """AUTO_INSTALL 없이는 npm 전역 설치도 실행하지 않는다.

        커버리지 추가다. bootstrap.sh 의 승인 게이트는 이미 구현돼 있고 이 테스트는
        uv sync 만 덮던 기존 시험의 공백(npm 경로)을 메운다. RED 를 거친 결함 수정이 아니다.
        """
        self.run_script(MACHINE_LABEL="local")
        self.assertFalse(any(call.startswith("install") for call in self.npm_calls()))

    def test_missing_machine_label_stops_before_any_tool_call(self):
        result = self.run_script()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.uv_calls(), [])

    def test_sync_requires_explicit_install_approval(self):
        self.run_script(MACHINE_LABEL="local")
        self.assertFalse(any(call.startswith("sync") for call in self.uv_calls()))


if __name__ == "__main__":
    unittest.main()
