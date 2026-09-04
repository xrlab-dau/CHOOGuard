#!/usr/bin/env python3
"""Deterministic repository policy checks for choo-guard."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAX_FILE_BYTES = 50 * 1024 * 1024
FORBIDDEN_PARTS = {"Library", "Temp", "Logs", "UserSettings", "MemoryCaptures", "Recordings"}
FORBIDDEN_SUFFIXES = {".ulf", ".pem", ".p12", ".pfx", ".ckpt", ".pth", ".pt", ".safetensors", ".onnx"}
SECRET_PATTERNS = {
    "private key": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    "GitHub token": re.compile(r"(?:ghp|github_pat)_[A-Za-z0-9_]{20,}"),
    "AWS access key": re.compile(r"AKIA[0-9A-Z]{16}"),
    "Hugging Face token": re.compile(r"hf_[A-Za-z0-9]{20,}"),
}
UNITY_TEXT_SUFFIXES = {".unity", ".prefab", ".asset", ".mat"}
ACTION_USE = re.compile(r"^\s*-?\s*uses:\s*([^\s#]+)", re.MULTILINE)
PINNED_ACTION = re.compile(r"^[^@]+@[0-9a-fA-F]{40}$")


def git(*args: str) -> str:
    return subprocess.check_output(["git", "-C", str(ROOT), *args], text=True).strip()


def tracked_files() -> list[Path]:
    output = git("ls-files", "-z")
    return [ROOT / item for item in output.split("\0") if item]


def changed_files(base: str, head: str) -> list[Path]:
    output = git("diff", "--name-only", "--diff-filter=ACMR", f"{base}...{head}")
    return [ROOT / item for item in output.splitlines() if item]


def relative(file: Path) -> str:
    return file.relative_to(ROOT).as_posix()


def inspect(files: list[Path]) -> list[str]:
    errors: list[str] = []
    tracked_rel = {relative(p) for p in tracked_files()}

    for file in files:
        rel = relative(file)
        parts = set(file.relative_to(ROOT).parts)
        if parts & FORBIDDEN_PARTS:
            errors.append(f"forbidden generated path: {rel}")
        if rel == ".env" or rel.startswith(".env.") and rel != ".env.example":
            errors.append(f"secret environment file: {rel}")
        if file.suffix.lower() in FORBIDDEN_SUFFIXES:
            errors.append(f"forbidden credential/model file: {rel}")
        if rel.startswith(("data/raw/", "data/restricted/", "private-data/", "checkpoints/", "models/")):
            errors.append(f"restricted data/model path: {rel}")
        if not file.exists() or not file.is_file():
            continue
        if file.stat().st_size > MAX_FILE_BYTES:
            errors.append(f"file exceeds 50 MiB policy limit: {rel} ({file.stat().st_size} bytes)")

        if rel.startswith("Assets/") and not rel.endswith(".meta"):
            meta = f"{rel}.meta"
            if meta not in tracked_rel:
                errors.append(f"missing Unity meta file: {meta}")

        if file.suffix.lower() in UNITY_TEXT_SUFFIXES:
            try:
                prefix = file.read_bytes()[:32]
            except OSError:
                prefix = b""
            if prefix and not prefix.startswith(b"%YAML"):
                errors.append(f"Unity asset is not Force Text YAML: {rel}")

        if file.stat().st_size <= 1_000_000:
            try:
                text = file.read_text(encoding="utf-8")
            except (UnicodeDecodeError, OSError):
                text = ""
            for name, pattern in SECRET_PATTERNS.items():
                if pattern.search(text):
                    errors.append(f"possible {name} in {rel}")

    for workflow in sorted((ROOT / ".github/workflows").glob("*.y*ml")):
        text = workflow.read_text(encoding="utf-8")
        for use in ACTION_USE.findall(text):
            if use.startswith(("./", "docker://")):
                continue
            if not PINNED_ACTION.match(use):
                errors.append(f"GitHub Action is not pinned to a full commit SHA: {relative(workflow)} -> {use}")

    manifest = ROOT / "Packages/manifest.json"
    if manifest.exists():
        try:
            dependencies = json.loads(manifest.read_text(encoding="utf-8")).get("dependencies", {})
        except json.JSONDecodeError as exc:
            errors.append(f"invalid Packages/manifest.json: {exc}")
        else:
            for package, value in dependencies.items():
                if isinstance(value, str) and "github.com" in value:
                    ref = value.rsplit("#", 1)[1] if "#" in value else ""
                    if not ref or ref in {"main", "master", "beta", "develop"}:
                        errors.append(f"Unity Git dependency must use an immutable tag or SHA: {package} -> {value}")

    schema = ROOT / "schemas/scene-bundle.schema.json"
    try:
        json.loads(schema.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"invalid SceneBundle schema: {exc}")

    required_manifest = {"schemaVersion", "sceneId", "createdAt", "sourceHash", "model", "coordinates", "assets", "qa", "dataClassification"}
    for scene_manifest in ROOT.rglob("scene_manifest.json"):
        if any(part in {"Library", "Temp", ".git"} for part in scene_manifest.parts):
            continue
        try:
            data = json.loads(scene_manifest.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            errors.append(f"invalid JSON: {relative(scene_manifest)}: {exc}")
            continue
        missing = sorted(required_manifest - set(data))
        if missing:
            errors.append(f"SceneBundle manifest missing {missing}: {relative(scene_manifest)}")
        qa = data.get("qa", {})
        if isinstance(qa, dict) and qa.get("approved") is True and qa.get("controlPointCount", 0) < 10:
            errors.append(f"approved SceneBundle needs at least 10 control points: {relative(scene_manifest)}")

    return sorted(set(errors))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base")
    parser.add_argument("--head", default="HEAD")
    parser.add_argument("--report")
    args = parser.parse_args()

    files = changed_files(args.base, args.head) if args.base else tracked_files()
    errors = inspect(files)
    lines = ["# Repository policy report", "", f"Checked files: {len(files)}", f"Violations: {len(errors)}", ""]
    lines.extend(f"- {item}" for item in errors)
    report = "\n".join(lines) + "\n"
    if args.report:
        Path(args.report).write_text(report, encoding="utf-8")
    print(report, end="")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
