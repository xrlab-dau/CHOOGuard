"""Convert pip's actual successful-install report into a platform-specific hash lock."""
from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    report = json.loads(args.report.read_text(encoding="utf-8"))
    lines = ["# Actual installed wheel closure from " + args.report.name,
             "# CPython 3.12 / macOS arm64 only; not a Windows compatibility claim.",
             "--only-binary=:all:"]
    for entry in sorted(report["install"], key=lambda item: item["metadata"]["name"].lower()):
        metadata = entry["metadata"]
        artifact = entry["download_info"]
        if not artifact["url"].endswith(".whl"):
            raise ValueError("Expected an installed binary wheel, not a source build")
        sha256 = artifact["archive_info"]["hashes"]["sha256"]
        lines.append(f"{metadata['name']}=={metadata['version']} --hash=sha256:{sha256}")
    args.output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"locked_wheels": len(report["install"]), "environment": report["environment"],
                      "lock": str(args.output)}))


if __name__ == "__main__":
    main()
