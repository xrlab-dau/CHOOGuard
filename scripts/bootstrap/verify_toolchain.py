#!/usr/bin/env python3
"""CHOOGuard 도구 체인 검증 영수증 생성기.

무엇을 검사하는가
- 저장소·브랜치, Node·Pi·uv 버전, .pi/settings.json 패키지 설치 여부, 조사 가상환경
- 추적 파일 중 금지 이름·확장자(대소문자 무시), 저장소 밖을 가리키는 심볼릭 링크(.pi/npm, .venv 항목 자체 포함)
- .pi/settings.json 의 어느 깊이에도 MCP·원격 실행 설정 키가 없는지
- 정책 파일 SHA-256 (AGENTS.md, CODEOWNERS, workflows, .pi/**, constitution, ADR, scripts/ci, 조사 lock)
- 환경 변수는 이름과 설정 여부만 기록한다. 값은 절대 기록하지 않는다.

종료 코드
- 필수 항목이 하나라도 실패하면 1. `--strict` 는 호환용이며 기본 동작과 같다.
- `--write` 경로가 규칙을 어기면 검사를 시작하지 않고 2.

영수증 규칙
- `docs/evidence/**/*.json` 아래의 새 파일에만 쓴다. 기존 파일·정책 파일·저장소 밖 경로는 거부한다.
- 영수증은 기계 관측 기록이다. M0-00 완료, 사람의 자료 분류, PM 승인의 증거가 아니다.

사용: python3 scripts/bootstrap/verify_toolchain.py --label school-pc [--write docs/evidence/M0-00/verify-school-pc-<run>.json]
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import fnmatch
import hashlib
import json
import os
import platform
import re
import shutil
import stat
import subprocess
import sys
from datetime import date
from pathlib import Path, PurePosixPath
from time import monotonic

ROOT = Path(__file__).resolve().parents[2]
PI_VERSION = "0.85.0"
NODE_MIN_MAJOR = 22
MACHINE_LABELS = ("local", "school-pc")
RECEIPT_ROOT = "docs/evidence"
# 파일 이름을 소문자로 바꾼 뒤 비교한다.
FORBIDDEN_TRACKED = [
    "*.ulf", "*.pem", "*.key", "*.p12", "*.pfx",  # 라이선스·자격·키
    "*.ckpt", "*.pth", "*.pt", "*.safetensors", "*.onnx",  # 모델 가중치
    "*.ply", "*.spz", "*.glb",  # 재구성 자산
    "*.mp4", "*.mov", "*.mxf", "*.arw", "*.cr2", "*.nef", "*.dng", "*.raw",  # 촬영 원본
]
ALLOWED_ENV_FILES = {".env.example"}
POLICY_GLOBS = [
    "AGENTS.md",
    ".github/CODEOWNERS",
    ".github/workflows/*.yml",
    ".github/workflows/*.yaml",
    ".pi/settings.json",
    ".pi/workflows.json",
    ".pi/agents/*.md",
    ".pi/workflows/*.yaml",
    ".pi/prompts/*.md",
    ".specify/memory/constitution.md",
    "docs/adr/*.md",
    "scripts/ci/*.py",
    "scripts/bootstrap/*",
    "tools/research/pyproject.toml",
    "tools/research/uv.lock",
    "tools/research/research.py",
    "Packages/manifest.json",
    "Packages/packages-lock.json",
    "reconstruction/pyproject.toml",
    # Existing setup inputs are required individually; the family glob also
    # makes additional reconstruction locks part of the exact reviewed set.
    "reconstruction/requirements-colmap.lock",
    "reconstruction/requirements-da3.lock",
    "reconstruction/requirements-mapanything.lock",
    "reconstruction/requirements-*.lock",
]
# Observed inventory is not an approved baseline. The now-tracked .pi/.specify
# policy families are included; only the alternative YAML extension is optional.
# 금지 파일 검사는 ignored 트리도 걷는다. prune 이 우회 경로가 되면 안 된다.
# .git 만 제외한다(내부 객체는 작업 입력이 아니다).
WORKSPACE_PRUNED_DIRS = {".git"}
# 의존성 관리 트리. 여기서는 아래 확장자의 의미가 달라진다.
# certifi 의 cacert.pem 은 자격이 아니라 CA 번들이고, venv 의 *.pth 는
# 모델 가중치가 아니라 Python 경로 설정 파일이다.
# 면제는 **검증된 루트 아래**에만 적용한다. 경로 세그먼트 이름만 보면
# 아무 데나 site-packages 디렉터리를 만들어 자격·가중치를 숨길 수 있다.
DEPENDENCY_ROOTS = ("tools/research/.venv/", ".pi/npm/node_modules/")
# 의존성 루트에서 실측으로 확인된 오탐만 면제한다(2026-09-06 기준 각 1건).
# 확장자 전체를 면제하면 모델 가중치·재구성 자산을 venv 안에 숨길 수 있다.
# 새 오탐이 생기면 파일 단위로 추가하고 근거를 남긴다.
ALLOWED_IN_DEPENDENCIES = (
    "certifi/cacert.pem",  # Exact package member; arbitrary cacert.pem is not exempt.
    "_virtualenv.pth",     # Exact virtualenv bootstrap file, not arbitrary model .pth.
)
WORKSPACE_SCAN_MAX_FILES = 200_000
WORKSPACE_SCAN_MAX_DIRS = 20_000
WORKSPACE_SCAN_MAX_SECONDS = 15
MAX_POLICY_MANIFEST_BYTES = 2_000_000
MAX_PACKAGE_MANIFEST_BYTES = 1_000_000
REQUIRED_POLICY_GLOBS = [p for p in POLICY_GLOBS if p != ".github/workflows/*.yaml"]
ENV_NAMES = ["ANTHROPIC_API_KEY", "OPENAI_API_KEY", "EXA_API_KEY", "RESEARCH_MODEL"]
DISALLOWED_SETTINGS_KEYS = {"mcp", "mcpServers", "execute_code", "remotePackages"}
REQUIRED_CHECKS = [
    "node", "pi", "uv", "pi_packages", "research_venv",
    "forbidden_tracked_files", "forbidden_workspace_files",
    "symlinks_outside_repo", "settings_disallowed_keys", "policy_hash_scope", "policy_baseline",
]


def run(*cmd: str) -> str | None:
    """명령의 stdout 을 돌려준다. 실행 파일 부재, 예외, 0이 아닌 종료 코드는 모두 None 이다."""
    exe = shutil.which(cmd[0])
    if not exe:
        return None
    try:
        # Decode in this thread: Windows pipe readers otherwise fail in a worker
        # thread when Git's UTF-8 paths meet a CP949 process locale.
        proc = subprocess.run([exe, *cmd[1:]], capture_output=True, timeout=30, cwd=ROOT)
        if proc.returncode != 0:
            return None
        output = proc.stdout.decode("utf-8").rstrip("\r\n")
    except (subprocess.SubprocessError, OSError, UnicodeError):
        return None
    return output


@contextmanager
def open_regular_file(path: Path):
    """Reject special files and avoid blocking on a substituted POSIX FIFO.

    This is not a filesystem snapshot or a deadline for blocked mount I/O.
    """
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
        raise ValueError("not a regular file")
    flags = os.O_RDONLY | getattr(os, "O_BINARY", 0)
    flags |= getattr(os, "O_NONBLOCK", 0) | getattr(os, "O_NOFOLLOW", 0)
    with os.fdopen(os.open(path, flags), "rb") as stream:
        info = os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            raise ValueError("not a regular file")
        yield stream


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open_regular_file(path) as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def tracked_files() -> list[str] | None:
    """git 추적 파일 목록. git 실행에 실패하면 None (빈 목록으로 취급하지 않는다)."""
    out = run("git", "ls-files", "-z")
    if out is None:
        return None
    return [f for f in out.split("\0") if f]


def is_forbidden_tracked(path: str) -> bool:
    name = PurePosixPath(path).name.lower()
    if name == ".env" or (name.startswith(".env.") and name not in ALLOWED_ENV_FILES):
        return True
    return any(fnmatch.fnmatchcase(name, pattern) for pattern in FORBIDDEN_TRACKED)


def tracked_file_check() -> dict:
    tracked = tracked_files()
    if tracked is None:
        return {"count": None, "suffixes": None, "ok": False, "reason": "git ls-files 실행 실패"}
    forbidden = sorted(f for f in tracked if is_forbidden_tracked(f))
    return {"count": len(forbidden), "suffixes": forbidden_kinds(forbidden), "ok": not forbidden}


def forbidden_kinds(paths: list[str]) -> list[str]:
    """Return fixed policy labels, never a user-controlled filename suffix."""
    kinds = set()
    for path in paths:
        name = PurePosixPath(path).name.lower()
        if name == ".env" or name.startswith(".env."):
            kinds.add(".env")
        else:
            kinds.update(pattern[1:] for pattern in FORBIDDEN_TRACKED if fnmatch.fnmatchcase(name, pattern))
    return sorted(kinds)


def is_forbidden_workspace(path: str) -> bool:
    """작업본 검사용 판정.

    촬영 원본·환경 파일·명백한 자격/라이선스는 의존성 트리 안이라도 금지다.
    `DEPENDENCY_ROOTS` 아래에서 실측으로 확인된 오탐만 파일 단위로 제외한다.
    추적 파일 검사(`is_forbidden_tracked`)는 이 완화를 적용하지 않는다.
    """
    if not is_forbidden_tracked(path):
        return False
    if not path.startswith(DEPENDENCY_ROOTS[0]):
        return True
    relative = path.removeprefix(DEPENDENCY_ROOTS[0])
    parts = PurePosixPath(relative).parts
    # Windows Lib/site-packages and POSIX lib[/pythonX.Y]/site-packages only.
    if len(parts) >= 3 and parts[0] in {"lib", "Lib"}:
        offset = 2 if parts[1] == "site-packages" else 3
        if offset == 3 and (len(parts) < 4 or not re.fullmatch(r"python3\.\d+", parts[1]) or parts[2] != "site-packages"):
            return True
        return "/".join(parts[offset:]) not in ALLOWED_IN_DEPENDENCIES
    return True


def scan_workspace(root: Path) -> dict:
    """Bounded filename/link scan. Never follow junctions or detected mounts.

    The elapsed budget is checked between filesystem calls. It cannot interrupt
    a blocked OS call or prove the absence of every platform's mount type.
    """
    root = root.resolve()
    started = monotonic()
    result = {"forbidden": [], "outside_links": [], "errors": [], "truncated": False,
              "limit": None, "files": 0, "directories": 0, "mounts": 0, "reparse_points": 0}
    scanned_dirs, internal_links, pruned_roots = set(), [], set()

    def on_error(error: OSError) -> None:
        name = getattr(error, "filename", None)
        result["errors"].append(str(name) if name else error.__class__.__name__)

    def over_budget() -> bool:
        limit = None
        if monotonic() - started > WORKSPACE_SCAN_MAX_SECONDS:
            limit = "elapsed_time"
        elif result["directories"] > WORKSPACE_SCAN_MAX_DIRS:
            limit = "directories"
        elif result["files"] > WORKSPACE_SCAN_MAX_FILES:
            limit = "files"
        if limit:
            result.update(truncated=True, limit=limit)
        return bool(limit)

    for dirpath, dirnames, filenames in os.walk(root, followlinks=False, onerror=on_error):
        here = Path(dirpath)
        result["directories"] += 1
        if over_budget():
            break
        scanned_dirs.add(here)
        if here == root:
            pruned_roots.update(d for d in dirnames if d in WORKSPACE_PRUNED_DIRS)
            dirnames[:] = [d for d in dirnames if d not in WORKSPACE_PRUNED_DIRS]
        file_names = set(filenames)
        for name in [*dirnames, *filenames]:
            entry = here / name
            rel = entry.relative_to(root).as_posix()
            if name in file_names:
                result["files"] += 1
            if over_budget():
                break
            if is_forbidden_workspace(rel):
                result["forbidden"].append(rel)
            try:
                info = entry.lstat()
                if stat.S_ISLNK(info.st_mode):
                    target = entry.resolve(strict=True)
                    if target != root and root not in target.parents:
                        result["outside_links"].append(rel)
                    else:
                        internal_links.append((target, name in dirnames))
                    if name in dirnames:
                        dirnames.remove(name)
                elif getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
                    result["reparse_points"] += 1
                    result["errors"].append("unverified_reparse_point")
                    if name in dirnames:
                        dirnames.remove(name)
                elif os.path.ismount(entry):
                    result["mounts"] += 1
                    result["errors"].append("unverified_mount")
                    if name in dirnames:
                        dirnames.remove(name)
            except (OSError, RuntimeError) as error:
                result["errors"].append(type(error).__name__)
                if name in dirnames:
                    dirnames.remove(name)
        if result["truncated"]:
            break
    for target, is_directory in internal_links:
        # Internal links are safe to skip only when their canonical target was
        # reached by this scan. Root aliases also expose any pruned subtree.
        covered = target if is_directory else target.parent
        if covered not in scanned_dirs or (target == root and pruned_roots):
            result["errors"].append("unscanned_link_target")
    result["forbidden"].sort()
    result["outside_links"].sort()
    # The final filesystem call and result processing may consume the budget.
    # It is too late to interrupt those calls, but never label that scan clean.
    if not result["truncated"]:
        over_budget()
    return result


def forbidden_workspace_files(root: Path) -> tuple[list[str], bool, list[str]]:
    """Compatibility wrapper; incomplete scans are never a clean result."""
    result = scan_workspace(root)
    return result["forbidden"], result["truncated"], result["errors"]


def policy_inventory(root: Path) -> tuple[dict[str, str], list[str], int]:
    """Inventory observed bytes without following policy links/reparse points."""
    root = root.resolve()
    hashes, missing, errors = {}, [], 0
    for pattern in POLICY_GLOBS:
        valid = 0
        try:
            for path in sorted(root.glob(pattern)):
                try:
                    if is_forbidden_tracked(path.relative_to(root).as_posix()):
                        raise ValueError("prohibited policy input")
                    for part in [path, *path.parents]:
                        if part == root:
                            break
                        info = part.lstat()
                        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
                            raise ValueError("linked policy path")
                    info = path.stat()
                    if stat.S_ISDIR(info.st_mode):
                        continue
                    if not stat.S_ISREG(info.st_mode):
                        raise ValueError("non-regular policy input")
                    if info.st_size == 0:
                        errors += 1
                        continue
                    hashes[path.relative_to(root).as_posix()] = sha256(path)
                    valid += 1
                except (OSError, ValueError, RuntimeError):
                    errors += 1
        except OSError:
            errors += 1
        if not valid and pattern in REQUIRED_POLICY_GLOBS:
            missing.append(pattern)
    return dict(sorted(hashes.items())), missing, errors


def missing_required_policy(root: Path) -> list[str]:
    return policy_inventory(root)[1]


def escaped_symlinks(root: Path) -> list[str]:
    """Compatibility wrapper; main also checks scan completeness."""
    return scan_workspace(root)["outside_links"]


def policy_baseline_check(root: Path, manifest: Path | None, expected_digest: str | None,
                          inventory=None) -> dict:
    """Compare with a caller-pinned, separately reviewed manifest.

    This comparison does not authenticate human signatures. A trusted caller
    must supply the approved digest independently of the current workspace.
    """
    hashes, missing, errors = inventory if inventory is not None else policy_inventory(root)
    check = {"ok": False, "status": "policy_approval_pending", "missing_globs": missing,
             "inventory_errors": errors, "missing_paths": [], "unexpected_paths": [], "changed_paths": [],
             "approval_authenticity": "not_verified_by_this_tool"}
    if manifest is None or expected_digest is None:
        return check
    if not re.fullmatch(r"[0-9a-f]{64}", expected_digest):
        check["status"] = "invalid_manifest_digest"
        return check

    def unique(pairs):
        values = {}
        for key, value in pairs:
            if key in values:
                raise ValueError("duplicate key")
            values[key] = value
        return values

    try:
        manifest = Path(manifest)
        with open_regular_file(manifest) as stream:
            raw = stream.read(MAX_POLICY_MANIFEST_BYTES + 1)
        if len(raw) > MAX_POLICY_MANIFEST_BYTES:
            raise ValueError("oversized manifest")
        digest = hashlib.sha256(raw).hexdigest()
        check["manifest_sha256"] = digest
        if digest != expected_digest:
            check["status"] = "manifest_digest_mismatch"
            return check
        data = json.loads(raw.decode("utf-8"), object_pairs_hook=unique)
        if not isinstance(data, dict) or set(data) != {"schemaVersion", "status", "approvalReference", "sourceCommit", "policySha256"}:
            raise ValueError("invalid manifest fields")
        if type(data["schemaVersion"]) is not int or data["schemaVersion"] != 1:
            raise ValueError("invalid schema version")
        if not isinstance(data["sourceCommit"], str) or not re.fullmatch(r"[0-9a-f]{40}", data["sourceCommit"]):
            raise ValueError("invalid source commit")
        expected = data["policySha256"]
        if not isinstance(expected, dict) or not expected:
            raise ValueError("empty policy set")
        for name, value in expected.items():
            if not isinstance(name, str) or PurePosixPath(name).is_absolute() or any(p in {"", ".", ".."} for p in name.split("/")) or "\\" in name or ":" in name:
                raise ValueError("invalid policy path")
            if is_forbidden_tracked(name):
                raise ValueError("prohibited expected policy input")
            if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{64}", value):
                raise ValueError("invalid policy hash")
        if data["status"] == "candidate" and data["approvalReference"] is None:
            return check
        reference = data["approvalReference"]
        if data["status"] != "approved" or not isinstance(reference, str) or not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", reference):
            raise ValueError("missing approval reference")
        check.update(missing_paths=sorted(set(expected) - set(hashes)),
                     unexpected_paths=sorted(set(hashes) - set(expected)),
                     changed_paths=sorted(p for p in set(hashes) & set(expected) if hashes[p] != expected[p]),
                     baseline_source_commit=data["sourceCommit"], approval_reference=reference)
        check["ok"] = not (missing or errors or check["missing_paths"] or check["unexpected_paths"] or check["changed_paths"])
        check["status"] = "baseline_match_not_runtime_approval" if check["ok"] else "policy_baseline_mismatch"
    except (OSError, ValueError, UnicodeError, RecursionError):
        check["status"] = "invalid_or_unreadable_manifest"
    return check


def nested_disallowed_keys(value, prefix: str = "") -> list[str]:
    """설정 JSON 의 모든 깊이에서 금지 키를 찾는다."""
    found: list[str] = []
    if isinstance(value, dict):
        for key, child in value.items():
            path = f"{prefix}.{key}" if prefix else str(key)
            if key in DISALLOWED_SETTINGS_KEYS:
                found.append(path)
            found.extend(nested_disallowed_keys(child, path))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            found.extend(nested_disallowed_keys(child, f"{prefix}[{index}]"))
    return found


def package_specs(settings: dict) -> list[tuple[str, str]] | None:
    """Validate declarations before using them as paths or reading metadata."""
    entries = settings.get("packages", [])
    if not isinstance(entries, list):
        return None
    specs = []
    for entry in entries:
        spec = entry.get("source") if isinstance(entry, dict) else entry
        if not isinstance(spec, str) or len(spec) > 512:
            return None
        spec = spec.removeprefix("npm:")  # settings.json 은 "npm:<name>@<version>" 형식
        if spec.startswith("@"):
            scope_name, _, want = spec[1:].partition("@")
            name = "@" + scope_name
        else:
            name, _, want = spec.partition("@")
        if len(name) > 214 or not re.fullmatch(r"(?:@[a-z0-9][a-z0-9._-]*/)?[a-z0-9][a-z0-9._-]*", name):
            return None
        specs.append((name, want))
    return specs


def check_packages(root: Path, settings: dict) -> tuple[list[dict], bool]:
    specs = package_specs(settings)
    if specs is None:
        return [], False
    rows, all_ok = [], True
    for name, want in specs:
        pkg = root / ".pi/npm/node_modules" / name / "package.json"
        have = None
        try:
            with open_regular_file(pkg) as stream:
                raw = stream.read(MAX_PACKAGE_MANIFEST_BYTES + 1)
            if len(raw) > MAX_PACKAGE_MANIFEST_BYTES:
                raise ValueError("package metadata exceeds read budget")
            metadata = json.loads(raw.decode("utf-8"))
            value = metadata.get("version") if isinstance(metadata, dict) else None
            if isinstance(value, str) and len(value) <= 128 and re.fullmatch(
                r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)"
                r"(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?", value
            ):
                have = value
        except (OSError, ValueError, RecursionError):
            pass
        ok = have is not None and (not want or have == want)
        all_ok &= ok
        rows.append({"package": name, "wanted": want or None, "installed": have, "ok": ok})
    return rows, all_ok


def receipt_target(root: Path, requested: str) -> Path:
    """docs/evidence 아래의 새 .json 파일만 허용한다. 위반은 ValueError."""
    root = root.resolve()
    rel = PurePosixPath(requested.replace("\\", "/"))
    if rel.is_absolute() or ".." in rel.parts or any(":" in part for part in rel.parts):
        raise ValueError(f"영수증 경로는 저장소 상대 경로여야 한다: {requested}")
    prefix = PurePosixPath(RECEIPT_ROOT).parts
    if rel.parts[:len(prefix)] != prefix or len(rel.parts) <= len(prefix):
        raise ValueError(f"영수증은 {RECEIPT_ROOT}/ 아래에만 쓴다: {requested}")
    out = root / rel
    if out.suffix != ".json":
        raise ValueError(f"영수증은 .json 파일이어야 한다: {requested}")
    current = root
    try:
        for part in rel.parts:
            current = current / part
            try:
                info = current.lstat()
            except FileNotFoundError:
                break
            if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
                raise ValueError("영수증 경로의 링크는 허용하지 않는다")
            if os.path.ismount(current):
                raise ValueError("영수증 경로의 mount는 검증되지 않았다")
            if current == out:
                raise ValueError("기존 파일은 덮어쓰지 않는다. 실행별 새 이름을 쓴다")
            if not stat.S_ISDIR(info.st_mode):
                raise ValueError("영수증 부모 경로는 일반 디렉터리여야 한다")
    except OSError as error:
        raise ValueError("영수증 경로를 확인할 수 없다") from error
    return out


def write_receipt(root: Path, requested: str, text: str) -> Path:
    """Recheck lexical parents at write time; exclusive creation stays append-only.

    These checks do not provide an atomic filesystem snapshot or protect against
    every concurrent parent substitution between the last check and the open.
    """
    target = receipt_target(root, requested)
    target.parent.mkdir(parents=True, exist_ok=True)
    target = receipt_target(root, requested)
    with target.open("x", encoding="utf-8") as stream:
        stream.write(text + "\n")
    return target


def main() -> int:
    ap = argparse.ArgumentParser(description="CHOOGuard 도구 체인 검증 영수증 생성기")
    ap.add_argument("--label", required=True, choices=MACHINE_LABELS, help="호스트명 대신 쓰는 공개 라벨")
    ap.add_argument("--write", help=f"새 영수증 JSON 경로 ({RECEIPT_ROOT}/ 아래, 기존 파일 불가)")
    ap.add_argument("--strict", action="store_true", help="호환용. 필수 항목 실패 시 종료 코드 1 은 기본 동작이다")
    ap.add_argument("--policy-manifest", type=Path, help="별도 검토한 정확 정책 경로/해시 manifest")
    ap.add_argument("--policy-manifest-sha256", help="신뢰된 호출자가 별도로 제공하는 승인 manifest SHA-256")
    ap.add_argument("--policy-preflight", action="store_true", help="정적 정책 게이트만 검사하며 도구 실행·영수증 작성을 하지 않음")
    ap.add_argument("--write-policy-candidate", help="도구 실행 없이 미승인 정책 후보를 새 evidence JSON에 작성")
    args = ap.parse_args()
    root = ROOT.resolve()
    if args.write == "" or args.write_policy_candidate == "":
        ap.error("영수증·정책 후보 경로는 빈 값일 수 없다")
    if bool(args.policy_manifest) != bool(args.policy_manifest_sha256):
        ap.error("--policy-manifest와 --policy-manifest-sha256을 함께 지정한다")
    if args.write_policy_candidate is not None and (args.write is not None or args.policy_manifest is not None or args.policy_preflight):
        ap.error("정책 후보 생성과 검증 영수증 생성을 분리한다")

    inventory = policy_inventory(root)
    hashes, policy_missing, policy_errors = inventory
    if args.write_policy_candidate is not None:
        if policy_missing or policy_errors:
            print("error: 정책 파일 누락/읽기 실패로 후보를 만들 수 없다", file=sys.stderr)
            return 1
        head = run("git", "rev-parse", "HEAD")
        if not head or not re.fullmatch(r"[0-9a-f]{40}", head):
            print("error: 기준 커밋을 확인할 수 없다", file=sys.stderr)
            return 1
        candidate = {"schemaVersion": 1, "status": "candidate", "approvalReference": None,
                     "sourceCommit": head, "policySha256": hashes}
        try:
            write_receipt(root, args.write_policy_candidate, json.dumps(candidate, ensure_ascii=False, indent=2))
        except (OSError, ValueError):
            print("error: 새 정책 후보를 저장할 수 없다", file=sys.stderr)
            return 2
        print("policy_approval_pending: candidate written; no tool/runtime approval")
        return 0

    out: Path | None = None
    if args.write is not None:
        try:
            out = receipt_target(root, args.write)
        except ValueError as error:
            print(f"error: {error}", file=sys.stderr)
            return 2

    forbidden_check = tracked_file_check()

    scan = scan_workspace(root)
    outside_links = scan["outside_links"]
    workspace_forbidden, workspace_truncated, workspace_errors = scan["forbidden"], scan["truncated"], scan["errors"]
    incomplete = workspace_truncated or bool(workspace_errors)
    # The scan can take seconds. Do not authorize probes with a policy snapshot
    # from before that scan when the actual inputs have since changed.
    after_scan = policy_inventory(root)
    scan_policy_stable = after_scan == inventory
    inventory = after_scan
    hashes, policy_missing, policy_errors = inventory
    baseline = policy_baseline_check(root, args.policy_manifest, args.policy_manifest_sha256, inventory)
    baseline["stable_across_scan"] = scan_policy_stable
    if not scan_policy_stable:
        baseline.update(ok=False, status="policy_changed_during_scan")
    can_probe = baseline["ok"] and forbidden_check["ok"] and not incomplete and not outside_links and not workspace_forbidden

    settings: dict | None
    try:
        if policy_missing or policy_errors:
            raise ValueError("unverified policy paths")
        with open_regular_file(root / ".pi/settings.json") as stream:
            settings_bytes = stream.read()
        if hashlib.sha256(settings_bytes).hexdigest() != hashes.get(".pi/settings.json"):
            baseline.update(ok=False, status="settings_changed_before_probes")
            raise ValueError("settings differ from verified bytes")
        settings = json.loads(settings_bytes.decode("utf-8"))
        if not isinstance(settings, dict):
            settings = None
    except (OSError, ValueError, RecursionError):
        settings = None
    if settings is None:
        disallowed_check = {"items": [], "ok": False, "reason": ".pi/settings.json 을 읽을 수 없다"}
    else:
        disallowed = sorted(nested_disallowed_keys(settings))
        disallowed_check = {"items": disallowed, "ok": not disallowed and package_specs(settings) is not None}

    can_probe = can_probe and disallowed_check["ok"]
    if args.policy_preflight:
        observation = {"record_type": "policy_preflight_not_receipt", "machine_label": args.label,
                       "policy_preflight_ok": bool(can_probe), "tool_probes": {"executed": False},
                       "policy_baseline": baseline, "settings_disallowed_keys": disallowed_check,
                       "workspace_scan_incomplete": bool(incomplete),
                       "forbidden_tracked_files_ok": forbidden_check["ok"],
                       "forbidden_workspace_files_ok": not incomplete and not workspace_forbidden,
                       "symlinks_outside_repo_ok": not incomplete and not outside_links}
        print(json.dumps(observation, ensure_ascii=False, indent=2))
        return 0 if can_probe else 1

    pkgs, pkgs_ok = check_packages(root, settings) if can_probe else ([], False)
    node_v = run("node", "--version") if can_probe else None
    try:
        node_ok = bool(node_v) and int(node_v.lstrip("v").split(".")[0]) >= NODE_MIN_MAJOR
    except ValueError:
        node_ok = False
    pi_lines = ((run("pi", "--version") if can_probe else None) or "").splitlines()
    pi_v = pi_lines[0] if pi_lines else None
    uv_v = run("uv", "--version") if can_probe else None

    # Probes may change non-policy files and links too. Keep the pre-probe gate,
    # then base the final receipt on a fresh bounded scan. Check policy last so
    # changes during this second scan cannot bless a stale policy snapshot.
    if can_probe:
        forbidden_check = tracked_file_check()
        scan = scan_workspace(root)
        outside_links = scan["outside_links"]
        workspace_forbidden, workspace_truncated, workspace_errors = scan["forbidden"], scan["truncated"], scan["errors"]
        incomplete = workspace_truncated or bool(workspace_errors)
        baseline["stable_across_probes"] = policy_inventory(root) == inventory
        if not baseline["stable_across_probes"]:
            baseline.update(ok=False, status="policy_changed_during_probes")

    receipt = {
        "unit": "M0-00",
        "record_type": "machine_observation_not_approval",
        "machine_label": args.label,
        "date": date.today().isoformat(),
        "platform": {"system": platform.system(), "release": platform.release(), "python": platform.python_version()},
        "git": {"branch": run("git", "branch", "--show-current"), "head": run("git", "rev-parse", "HEAD")},
        "tool_probes": {"executed": bool(can_probe),
                        "reason": "baseline_and_static_checks_matched" if can_probe else "policy_or_static_gate_not_satisfied"},
        "checks": {
            "node": {"version": node_v, "ok": node_ok},
            "pi": {"version": pi_v, "wanted": PI_VERSION, "ok": pi_v == PI_VERSION},
            "uv": {"version": uv_v, "ok": uv_v is not None},
            "pi_packages": {"rows": pkgs, "ok": pkgs_ok},
            "research_venv": {"ok": (root / "tools/research/.venv").is_dir()},
            "forbidden_tracked_files": forbidden_check,
            "forbidden_workspace_files": {
                # 경로를 영수증에 남기지 않는다. 촬영 파일명·위치명이 공개될 수 있다.
                # 순회가 잘렸으면 개수를 안다고 주장하지 않는다. null 은 '미상'이다.
                "count": None if incomplete else len(workspace_forbidden),
                "suffixes": None if incomplete else forbidden_kinds(workspace_forbidden),
                "scan_truncated": workspace_truncated,
                # 경로는 남기지 않는다. 접근 거부된 경로명도 민감할 수 있다.
                "scan_errors": len(workspace_errors),
                "scan_limit": scan["limit"],
                "files_observed": scan["files"],
                "directories_observed": scan["directories"],
                "unverified_mounts": scan["mounts"],
                "unverified_reparse_points": scan["reparse_points"],
                "ok": not workspace_forbidden and not workspace_truncated and not workspace_errors,
            },
            "policy_hash_scope": {"missing_globs": policy_missing, "read_errors": policy_errors,
                                  "ok": not policy_missing and not policy_errors},
            "policy_baseline": baseline,
            "symlinks_outside_repo": {"count": None if incomplete else len(outside_links),
                                      "scan_incomplete": bool(incomplete), "ok": not outside_links and not incomplete},
            "settings_disallowed_keys": disallowed_check,
            "unity_project": {"ok": (root / "ProjectSettings/ProjectVersion.txt").exists(), "required": False},
        },
        "env_present": {n: bool(os.environ.get(n)) for n in ENV_NAMES},
        "policy_sha256": hashes,
    }
    receipt["required_checks"] = REQUIRED_CHECKS
    receipt["required_ok"] = all(receipt["checks"][k]["ok"] for k in REQUIRED_CHECKS)

    if workspace_forbidden:
        print("금지 파일(콘솔 전용, 영수증 미기록):", file=sys.stderr)
        for rel in workspace_forbidden:
            print(f"  {rel}", file=sys.stderr)
    if workspace_errors:
        print(f"순회 오류 {len(workspace_errors)}건(콘솔 전용). 접근 거부된 트리는 검사되지 않았다:", file=sys.stderr)
        for name in workspace_errors:
            print(f"  {name}", file=sys.stderr)
    if workspace_truncated:
        print(f"순회 예산 초과({scan['limit']}). 통과로 처리하지 않는다.", file=sys.stderr)

    text = json.dumps(receipt, ensure_ascii=False, indent=2)
    if out is not None:
        try:
            out = write_receipt(root, args.write, text)
        except (OSError, ValueError):
            print("error: 새 검증 영수증을 저장할 수 없다. cannot_proceed", file=sys.stderr)
            return 2
        print(f"receipt written: {out.relative_to(root).as_posix()}")
    for k, v in receipt["checks"].items():
        print(f"[{'ok' if v['ok'] else 'FAIL'}] {k}")
    print(f"required_ok={receipt['required_ok']}")
    return 0 if receipt["required_ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
