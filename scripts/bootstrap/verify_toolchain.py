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
import fnmatch
import hashlib
import json
import os
import platform
import shutil
import subprocess
import sys
from datetime import date
from pathlib import Path, PurePosixPath

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
]
# POLICY_GLOBS 중 하나라도 파일이 없으면 정책 해시 범위가 무의미하다.
# .pi/ 와 .specify/ 는 PM 승인 전까지 미추적이므로 필수에서 제외한다.
REQUIRED_POLICY_GLOBS = [
    "AGENTS.md",
    ".github/CODEOWNERS",
    ".github/workflows/*.yml",
    "scripts/ci/*.py",
]
ENV_NAMES = ["ANTHROPIC_API_KEY", "OPENAI_API_KEY", "EXA_API_KEY", "RESEARCH_MODEL"]
DISALLOWED_SETTINGS_KEYS = {"mcp", "mcpServers", "execute_code", "remotePackages"}
PRUNED_DIRS = {".git", ".venv", "node_modules", "__pycache__"}
REQUIRED_CHECKS = [
    "node", "pi", "uv", "pi_packages", "research_venv",
    "forbidden_tracked_files", "forbidden_workspace_files",
    "symlinks_outside_repo", "settings_disallowed_keys", "policy_hash_scope",
]


def run(*cmd: str) -> str | None:
    """명령의 stdout 을 돌려준다. 실행 파일 부재, 예외, 0이 아닌 종료 코드는 모두 None 이다."""
    exe = shutil.which(cmd[0])
    if not exe:
        return None
    try:
        proc = subprocess.run([exe, *cmd[1:]], capture_output=True, text=True, timeout=30, cwd=ROOT)
    except (subprocess.SubprocessError, OSError):
        return None
    if proc.returncode != 0:
        return None
    return proc.stdout.strip()


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
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


def forbidden_workspace_files(root: Path) -> list[str]:
    """추적 여부와 무관하게 작업본 전체에서 금지 파일을 찾는다.

    git ls-files 는 미추적·ignored 입력을 보지 못한다. 촬영 원본이나 자격 파일이
    커밋되지 않은 채 작업본에 있으면 모델 전송·도구 실행 경계 밖으로 샐 수 있다.
    """
    found: list[str] = []
    for dirpath, dirnames, filenames in os.walk(root, followlinks=False):
        here = Path(dirpath)
        for name in filenames:
            rel = (here / name).relative_to(root).as_posix()
            if is_forbidden_tracked(rel):
                found.append(rel)
        dirnames[:] = [d for d in dirnames if d not in PRUNED_DIRS]
    return sorted(found)


def missing_required_policy(root: Path) -> list[str]:
    """필수 정책 glob 중 파일이 하나도 없는 패턴."""
    return [p for p in REQUIRED_POLICY_GLOBS if not any(f.is_file() for f in root.glob(p))]


def escaped_symlinks(root: Path) -> list[str]:
    """저장소 밖을 가리키는 링크. 제외 디렉터리 내부는 걷지 않지만 그 항목 자체는 검사한다."""
    root = root.resolve()
    found: list[str] = []
    for dirpath, dirnames, filenames in os.walk(root, followlinks=False):
        here = Path(dirpath)
        for name in [*dirnames, *filenames]:
            entry = here / name
            if entry.is_symlink():
                target = entry.resolve()
                if target != root and root not in target.parents:
                    found.append(entry.relative_to(root).as_posix())
        rel = here.relative_to(root).as_posix()
        dirnames[:] = [d for d in dirnames if d not in PRUNED_DIRS and not (rel == ".pi" and d == "npm")]
    return sorted(found)


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


def check_packages(root: Path, settings: dict) -> tuple[list[dict], bool]:
    rows, all_ok = [], True
    for entry in settings.get("packages", []):
        spec = entry if isinstance(entry, str) else entry.get("source", "")
        spec = spec.removeprefix("npm:")  # settings.json 은 "npm:<name>@<version>" 형식
        if spec.startswith("@"):
            scope_name, _, want = spec[1:].partition("@")
            name = "@" + scope_name
        else:
            name, _, want = spec.partition("@")
        pkg = root / ".pi/npm/node_modules" / name / "package.json"
        have = None
        if pkg.is_file():
            try:
                have = json.loads(pkg.read_text(encoding="utf-8")).get("version")
            except (OSError, ValueError):
                have = None
        ok = have is not None and (not want or have == want)
        all_ok &= ok
        rows.append({"package": name, "wanted": want or None, "installed": have, "ok": ok})
    return rows, all_ok


def receipt_target(root: Path, requested: str) -> Path:
    """docs/evidence 아래의 새 .json 파일만 허용한다. 위반은 ValueError."""
    root = root.resolve()
    rel = PurePosixPath(requested.replace("\\", "/"))
    if rel.is_absolute() or ".." in rel.parts:
        raise ValueError(f"영수증 경로는 저장소 상대 경로여야 한다: {requested}")
    out = (root / rel).resolve()
    evidence = (root / RECEIPT_ROOT).resolve()
    if evidence not in out.parents:
        raise ValueError(f"영수증은 {RECEIPT_ROOT}/ 아래에만 쓴다: {requested}")
    if out.suffix != ".json":
        raise ValueError(f"영수증은 .json 파일이어야 한다: {requested}")
    if out.exists() or out.is_symlink():
        raise ValueError(f"기존 파일은 덮어쓰지 않는다. 실행별 새 이름을 쓴다: {requested}")
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description="CHOOGuard 도구 체인 검증 영수증 생성기")
    ap.add_argument("--label", required=True, choices=MACHINE_LABELS, help="호스트명 대신 쓰는 공개 라벨")
    ap.add_argument("--write", help=f"새 영수증 JSON 경로 ({RECEIPT_ROOT}/ 아래, 기존 파일 불가)")
    ap.add_argument("--strict", action="store_true", help="호환용. 필수 항목 실패 시 종료 코드 1 은 기본 동작이다")
    args = ap.parse_args()
    root = ROOT.resolve()

    out: Path | None = None
    if args.write:
        try:
            out = receipt_target(root, args.write)
        except ValueError as error:
            print(f"error: {error}", file=sys.stderr)
            return 2

    tracked = tracked_files()
    if tracked is None:
        forbidden_check = {"items": [], "ok": False, "reason": "git ls-files 실행 실패"}
    else:
        forbidden = sorted(f for f in tracked if is_forbidden_tracked(f))
        forbidden_check = {"items": forbidden, "ok": not forbidden}

    settings: dict | None
    try:
        settings = json.loads((root / ".pi/settings.json").read_text(encoding="utf-8"))
        if not isinstance(settings, dict):
            settings = None
    except (OSError, ValueError):
        settings = None
    if settings is None:
        disallowed_check = {"items": [], "ok": False, "reason": ".pi/settings.json 을 읽을 수 없다"}
        pkgs, pkgs_ok = [], False
    else:
        disallowed = sorted(nested_disallowed_keys(settings))
        disallowed_check = {"items": disallowed, "ok": not disallowed}
        pkgs, pkgs_ok = check_packages(root, settings)

    node_v = run("node", "--version")
    try:
        node_ok = bool(node_v) and int(node_v.lstrip("v").split(".")[0]) >= NODE_MIN_MAJOR
    except ValueError:
        node_ok = False
    pi_lines = (run("pi", "--version") or "").splitlines()
    pi_v = pi_lines[0] if pi_lines else None
    uv_v = run("uv", "--version")
    outside_links = escaped_symlinks(root)
    workspace_forbidden = forbidden_workspace_files(root)
    policy_missing = missing_required_policy(root)

    hashes = {}
    for pattern in POLICY_GLOBS:
        for f in sorted(root.glob(pattern)):
            if f.is_file():
                hashes[f.relative_to(root).as_posix()] = sha256(f)

    receipt = {
        "unit": "M0-00",
        "record_type": "machine_observation_not_approval",
        "machine_label": args.label,
        "date": date.today().isoformat(),
        "platform": {"system": platform.system(), "release": platform.release(), "python": platform.python_version()},
        "git": {"branch": run("git", "branch", "--show-current"), "head": run("git", "rev-parse", "HEAD")},
        "checks": {
            "node": {"version": node_v, "ok": node_ok},
            "pi": {"version": pi_v, "wanted": PI_VERSION, "ok": pi_v == PI_VERSION},
            "uv": {"version": uv_v, "ok": uv_v is not None},
            "pi_packages": {"rows": pkgs, "ok": pkgs_ok},
            "research_venv": {"ok": (root / "tools/research/.venv").is_dir()},
            "forbidden_tracked_files": forbidden_check,
            "forbidden_workspace_files": {"items": workspace_forbidden, "ok": not workspace_forbidden},
            "policy_hash_scope": {"missing_globs": policy_missing, "ok": not policy_missing},
            "symlinks_outside_repo": {"items": outside_links, "ok": not outside_links},
            "settings_disallowed_keys": disallowed_check,
            "unity_project": {"ok": (root / "ProjectSettings/ProjectVersion.txt").exists(), "required": False},
        },
        "env_present": {n: bool(os.environ.get(n)) for n in ENV_NAMES},
        "policy_sha256": hashes,
    }
    receipt["required_checks"] = REQUIRED_CHECKS
    receipt["required_ok"] = all(receipt["checks"][k]["ok"] for k in REQUIRED_CHECKS)

    text = json.dumps(receipt, ensure_ascii=False, indent=2)
    if out is not None:
        out.parent.mkdir(parents=True, exist_ok=True)
        try:
            with out.open("x", encoding="utf-8") as f:
                f.write(text + "\n")
        except FileExistsError:
            print(f"error: 기존 파일은 덮어쓰지 않는다: {args.write}", file=sys.stderr)
            return 2
        print(f"receipt written: {out.relative_to(root).as_posix()}")
    for k, v in receipt["checks"].items():
        print(f"[{'ok' if v['ok'] else 'FAIL'}] {k}")
    print(f"required_ok={receipt['required_ok']}")
    return 0 if receipt["required_ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
