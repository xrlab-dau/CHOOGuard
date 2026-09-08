#!/usr/bin/env python3
"""Compile the exact System-only capture writer; this does not compile Unity code."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    relative = "Packages/com.xrlab.chooguard.foundation/Demo/Runtime/ReconstructionReviewController.cs"
    source = (root / relative).read_text(encoding="utf-8")
    marker = "    public sealed class ReconstructionCaptureWriter\n"
    if source.count(marker) != 1 or not source.rstrip().endswith("}\n}"):
        raise ValueError("Capture writer boundary changed; review this extraction before running.")
    body = source[source.index(marker):source.rfind("\n}")]
    imports = "\n".join(re.findall(r"^using System(?:\.[A-Za-z.]+)?;$", source, re.M))
    extracted = imports + "\nnamespace ChooGuard.Foundation.Demo\n{\n" + body + "\n}\n"
    if "UnityEngine" in extracted or "UnityEditor" in extracted:
        raise ValueError("The capture filesystem slice must not require a Unity substitute.")
    args.output.mkdir(parents=True, exist_ok=True)
    generated = args.output / "ReconstructionCaptureWriter.cs"
    generated.write_text(extracted, encoding="utf-8")
    paths = [root / relative]
    paths += sorted((root / "Packages/com.xrlab.chooguard.foundation/Runtime").glob("*.cs"))
    paths += [root / "Packages/com.xrlab.chooguard.foundation/Tests/Editor/TrainingSessionTests.cs"]
    paths += sorted(Path(__file__).parent.glob("*.cs"))
    paths += sorted(Path(__file__).parent.glob("*.csproj"))
    paths += [Path(__file__).resolve()]
    manifest = {
        "schemaVersion": 1,
        "commit": subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True).strip(),
        "scope": "Linux .NET 8 core NUnit tests and exact capture filesystem class slice",
        "unityCompilation": "not_run",
        "unitySerializationAndRendering": "not_run",
        "independentProviderReview": "not_run",
        "sourceSha256": {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
        "extractedClassSha256": hashlib.sha256(generated.read_bytes()).hexdigest(),
    }
    (args.output / "source-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({"prepared": str(generated), "scope": manifest["scope"]}))


if __name__ == "__main__":
    main()
