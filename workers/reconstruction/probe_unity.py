"""Build an isolated analytic Blender-to-Unity import probe, never a game asset."""
import argparse
import json
import math
import os
from pathlib import Path
import shutil
import re
import subprocess
import sys

import trimesh

from prepare_blender import prepare
from surface_cloud import convert, sha256_file


def probe(output: Path, blender: Path, unity: Path, version: str, timeout: float) -> dict:
    if not re.fullmatch(r"\d+\.\d+\.\d+[abfp]\d+", version):
        raise ValueError("Supply the installed Unity version, e.g. 6000.3.23f1")
    if not math.isfinite(timeout) or timeout <= 0:
        raise ValueError("timeout-seconds must be finite and positive")
    output = output.resolve()
    blender = blender.resolve(strict=True)
    unity = unity.resolve(strict=True)
    output.mkdir(parents=True, exist_ok=False)
    source = output / "tetrahedron.ply"
    # Unequal dimensions and a nonzero origin expose axis, handedness and scale errors.
    trimesh.Trimesh(
        vertices=[[.2, .3, .4], [2.2, .3, .4], [.2, 3.3, .4], [.2, .3, 4.4]],
        faces=[[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]],
        vertex_colors=[[255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255], [255, 255, 0, 255]],
        process=False).export(source)
    convert(source, output / "sampling", count=1000, seed=42, meters_per_unit=1,
            origin=[0, 0, 0], axes="right-handed X east / Y north / Z up analytic fixture",
            source_kind="test-fixture", source_reference="analytic asymmetric tetrahedron; not a building",
            scale_reference="defined in metres")
    prepare(source, output / "sampling/receipt.json", output / "handoff", blender,
            source_forward="Y", source_up="Z", frame_reference="analytic right-handed X east / Y north / Z up")
    project = output / "unity-project"
    for name in ("Assets/Editor", "Packages", "ProjectSettings"):
        (project / name).mkdir(parents=True, exist_ok=True)
    shutil.copy2(output / "handoff/prepared.fbx", project / "Assets/Prepared.fbx")
    shutil.copy2(Path(__file__).with_name("HandoffProbe.cs"), project / "Assets/Editor/HandoffProbe.cs")
    (project / "Packages/manifest.json").write_text('{"dependencies": {}}\n', encoding="utf-8")
    (project / "ProjectSettings/ProjectVersion.txt").write_text(f"m_EditorVersion: {version}\n", encoding="utf-8")
    expected = {"sourceKind": "test-fixture", "fbxSha256": sha256_file(project / "Assets/Prepared.fbx"),
                "worldVertices": [.2, .4, .3, 2.2, .4, .3, .2, .4, 3.3, .2, 4.4, .3],
                "triangles": 4, "requireColors": True}
    (project / "probe_expected.json").write_text(json.dumps(expected, indent=2) + "\n", encoding="utf-8")
    command = [str(unity), "-batchmode", "-nographics", "-quit", "-projectPath", str(project),
               "-executeMethod", "HandoffProbe.Run", "-logFile", str(project / "editor.log")]
    (output / "request.json").write_text(json.dumps({"command": command, "expected": expected,
        "unity_executable_sha256": sha256_file(unity)}, indent=2) + "\n", encoding="utf-8")
    with (output / "process.log").open("xb") as log:
        result = subprocess.run(command, cwd=project, stdout=log, stderr=subprocess.STDOUT,
                                timeout=timeout, check=False,
                                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    if result.returncode:
        raise ValueError(f"Unity exited {result.returncode}; inspect unity-project/editor.log")
    report = json.loads((project / "probe_result.json").read_text(encoding="utf-8"))
    if report["status"] != "fixture_import_passed" or report["fbxSha256"] != expected["fbxSha256"]:
        raise ValueError("Unity did not confirm the expected fixture import")
    if report["unityVersion"] != version:
        raise ValueError("Unity executable version differs from the requested project version")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--blender", type=Path, required=True)
    parser.add_argument("--unity", type=Path, required=True)
    parser.add_argument("--unity-version", required=True)
    parser.add_argument("--timeout-seconds", type=float, default=900)
    args = parser.parse_args()
    try:
        report = probe(args.output_dir, args.blender, args.unity, args.unity_version, args.timeout_seconds)
    except (ValueError, OSError, KeyError, subprocess.SubprocessError) as exc:
        print(f"unity-probe: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(report))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
