#!/usr/bin/env python3
"""Materialize exact desktop SDK binaries and the reviewed Pipeline interoperability patch.

Run after Unity has resolved Packages/manifest.json. Default is verification only;
--apply downloads hash-pinned public binaries and repairs generated PackageCache.
No global Git/MCP settings, Unity YAML or package version is edited.
"""
import argparse
import hashlib
import json
import os
import sys
import urllib.request
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[2]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def verified_write(path, data, expected_hash, expected_size):
    if len(data) != expected_size or digest(data) != expected_hash:
        raise ValueError("Downloaded dependency size or SHA-256 mismatch")
    temporary = path.with_name(path.name + ".cg-download")
    try:
        with temporary.open("wb") as output:
            output.write(data)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def guarded_patch(path, before_hash, replacement):
    current = path.read_bytes()
    if current == replacement:
        return False
    if digest(current) != before_hash:
        raise ValueError("Pipeline source differs from the reviewed integration patch")
    verified_write(path, replacement, digest(replacement), len(replacement))
    return True


def package(project, name, version):
    cache = (project / "Library/PackageCache").resolve()
    matches = []
    for candidate in cache.glob(name + "@*"):
        metadata = candidate / "package.json"
        if metadata.is_file() and json.loads(metadata.read_text()).get("version") == version:
            resolved = candidate.resolve()
            if not resolved.is_relative_to(cache):
                raise ValueError("Package is outside this project's generated cache")
            matches.append(resolved)
    if len(matches) != 1:
        raise ValueError(f"Resolve exactly {name}@{version} with Unity first")
    return matches[0]


def download_objects(objects):
    request = urllib.request.Request(
        "https://github.com/livekit/client-sdk-unity.git/info/lfs/objects/batch",
        data=json.dumps({"operation": "download", "transfers": ["basic"],
                         "objects": [{"oid": x["sha256"], "size": x["size"]} for x in objects]}).encode(),
        headers={"Accept": "application/vnd.git-lfs+json", "Content-Type": "application/vnd.git-lfs+json"})
    with urllib.request.urlopen(request, timeout=45) as response:
        batch = json.load(response)
    found = {x["oid"]: x for x in batch.get("objects", [])}
    for expected in objects:
        item = found.get(expected["sha256"], {})
        action = item.get("actions", {}).get("download", {})
        url = action.get("href", "")
        if not url.startswith("https://") or item.get("size") != expected["size"]:
            raise ValueError("LFS did not return the exact required dependency")
        # Do not log signed object URLs or headers. Trust is the pinned SHA-256, not the redirect.
        with urllib.request.urlopen(urllib.request.Request(url, headers=action.get("header", {})), timeout=45) as response:
            data = response.read(expected["size"] + 1)
        yield expected, data


def prepare(project, apply=False):
    contract = json.loads((project / "foundation/network/dependency-contract.json").read_text())
    lock = json.loads((project / "Packages/packages-lock.json").read_text())["dependencies"]
    for name, key in [("com.unity.netcode.gameobjects", "netcode"), ("com.unity.transport", "transport")]:
        if lock[name]["version"] != contract[key]:
            raise ValueError("Unexpected game networking version")
    if lock["io.livekit.livekit-sdk"].get("hash") != contract["livekitCommit"]:
        raise ValueError("Unexpected LiveKit SDK Git commit")
    sdk = package(project, "io.livekit.livekit-sdk", contract["livekitSdk"])
    required = contract["desktopBinaries"]
    missing = []
    for item in required:
        target = (sdk / item["path"]).resolve()
        if not target.is_relative_to(sdk) or target.suffix not in {".dll", ".dylib", ".so"}:
            raise ValueError("Invalid dependency binary path")
        data = target.read_bytes() if target.is_file() else b""
        if len(data) != item["size"] or digest(data) != item["sha256"]:
            missing.append(item)
    if missing and not apply:
        raise ValueError(f"{len(missing)} desktop dependency binaries are missing or differ; run --apply")
    if missing:
        for item, data in download_objects(missing):
            verified_write(sdk / item["path"], data, item["sha256"], item["size"])
            print("Materialized " + item["path"], file=sys.stderr)
    patch = contract["pipelineIntegration"]
    pipeline = package(project, patch["package"], patch["version"])
    target = (pipeline / patch["path"]).resolve()
    if not target.is_relative_to(pipeline) or target.suffix != ".asmdef":
        raise ValueError("Invalid Pipeline patch path")
    original = target.read_bytes()
    contents = json.loads(original)
    contents["overrideReferences"] = True
    contents["precompiledReferences"] = patch["precompiledReferences"]
    replacement = (json.dumps(contents, indent=4) + "\n").encode()
    if digest(replacement) != patch["afterSha256"]:
        raise ValueError("Pipeline patch does not match the reviewed result")
    if digest(original) != patch["afterSha256"]:
        if not apply:
            raise ValueError("Reviewed Pipeline interoperability patch is not applied; run --apply")
        guarded_patch(target, patch["beforeSha256"], replacement)
    return {"status": "dependency_files_verified", "livekitCommit": contract["livekitCommit"],
            "binaries": len(required), "pipelinePatchSha256": patch["afterSha256"],
            "runtimeAcceptance": "not_assessed"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=PROJECT)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    try:
        print(json.dumps(prepare(args.project.resolve(), args.apply), indent=2))
    except (ValueError, KeyError, OSError) as error:
        print(type(error).__name__ + ": dependency preparation failed; " +
              (str(error) if isinstance(error, (ValueError, KeyError)) else "inspect local dependency state"), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
