#!/usr/bin/env python3
"""Download one public, revision-pinned free model into this task's output. Never use auth."""
import hashlib
import json
import os
from pathlib import Path
import urllib.request
import urllib.parse

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "output/simulator/model-benchmark"
REPO = "mlx-community/Qwen2.5-1.5B-Instruct-4bit"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    os.environ["HF_HOME"] = str(OUT / "hf")
    os.environ["HF_HUB_CACHE"] = str(OUT / "hf/hub")
    os.environ["HF_HUB_DISABLE_IMPLICIT_TOKEN"] = "1"
    os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
    # Public urllib request has no Authorization header and does not inspect token files.
    with urllib.request.urlopen("https://huggingface.co/api/models/" + REPO + "?blobs=true", timeout=30) as response:
        metadata = json.load(response)
    revision = metadata["sha"]
    files = [entry for entry in metadata["siblings"] if entry["rfilename"].endswith((".json", ".safetensors", ".txt", ".md"))]
    expected = sum(entry.get("size", 0) for entry in files)
    if not 0 < expected < 1_200_000_000:
        raise RuntimeError("Model download outside approved 1.2 GB cap: " + str(expected))
    target = OUT / "models/Qwen2.5-1.5B-Instruct-4bit"
    print(json.dumps({"repository": REPO, "revision": revision, "expected_bytes": expected, "destination": str(target)}), flush=True)
    # Python urllib's verified TLS works here; the installed hub/httpx2 truststore raises OSStatus -26276.
    # Direct public revision URLs avoid that failing transport without disabling certificate checks.
    target.mkdir(parents=True, exist_ok=True)
    receipt = {"repository": REPO, "revision": revision, "expected_bytes": expected, "license": "apache-2.0", "authentication": "public urllib; no auth headers or credential discovery", "files": {}}
    for entry in files:
        path = target / entry["rfilename"]
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.stat().st_size != entry.get("size"):
            url = "https://huggingface.co/" + REPO + "/resolve/" + revision + "/" + urllib.parse.quote(entry["rfilename"])
            print("Downloading " + entry["rfilename"], flush=True)
            with urllib.request.urlopen(url, timeout=120) as response, path.with_suffix(path.suffix + ".partial").open("wb") as stream:
                for chunk in iter(lambda: response.read(1024 * 1024), b""):
                    stream.write(chunk)
            path.with_suffix(path.suffix + ".partial").replace(path)
        if path.stat().st_size != entry.get("size"):
            raise RuntimeError("Incomplete download: " + path.name)
        digest = hashlib.sha256()
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b""):
                digest.update(chunk)
        receipt["files"][entry["rfilename"]] = {"bytes": path.stat().st_size, "sha256": digest.hexdigest()}
        expected_sha = entry.get("lfs", {}).get("sha256")
        if expected_sha and digest.hexdigest() != expected_sha:
            raise RuntimeError("Published model SHA-256 mismatch: " + path.name)
    (OUT / "model-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(json.dumps({"download_complete": True, "files": len(files), "bytes": sum(v["bytes"] for v in receipt["files"].values())}), flush=True)


if __name__ == "__main__":
    main()
