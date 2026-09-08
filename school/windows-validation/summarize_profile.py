"""Validate measurement completeness before reporting a rendered-player profile."""
from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path
import statistics


def summarize(result: dict, rows: list[dict], warmup: float = 60) -> dict:
    elapsed, requested = float(result["elapsedSeconds"]), float(result["requestedSeconds"])
    if not all(math.isfinite(v) for v in (elapsed, requested, warmup)) or requested < 15 or warmup < 0:
        raise ValueError("Invalid duration or warmup")
    if result["status"] != "completed" or result["errors"] or elapsed < requested:
        raise ValueError("Run did not complete cleanly for the requested interval")
    if result["batchMode"] or result["cameraRenderCallbacks"] < result["frames"] * .9:
        raise ValueError("Update throughput is not rendered-frame evidence")
    metrics = ("seconds", "frames", "fps", "frame_p95_ms", "working_set_bytes", "private_bytes", "unity_allocated_bytes")
    data = [{k: float(row[k]) for k in metrics} for row in rows]
    if not data or any(not math.isfinite(v) or v < 0 for row in data for v in row.values()):
        raise ValueError("Missing, negative or nonfinite measurements")
    if sum(row["frames"] for row in data) != result["frames"] or abs(data[-1]["seconds"]-elapsed) > 1:
        raise ValueError("Sample totals or final time do not match the receipt")
    if any(b["seconds"] <= a["seconds"] or b["seconds"]-a["seconds"] > 5 for a,b in zip(data,data[1:])):
        raise ValueError("Nonmonotonic samples or an unmeasured interval")
    measured = [row for row in data if row["seconds"] > warmup]
    if len(measured) < 5:
        raise ValueError("Insufficient samples after warmup")
    mib = 1024*1024
    return {
        "elapsed_seconds": elapsed, "warmup_seconds": warmup, "samples_after_warmup": len(measured),
        "mean_camera_callbacks_per_second": result["cameraRenderCallbacks"]/elapsed,
        "median_one_second_update_fps": statistics.median(row["fps"] for row in measured),
        "lowest_one_second_update_fps": min(row["fps"] for row in measured),
        "highest_one_second_p95_frame_ms": max(row["frame_p95_ms"] for row in measured),
        "peak_working_set_mib": max(row["working_set_bytes"] for row in measured)/mib,
        "peak_private_mib": max(row["private_bytes"] for row in measured)/mib,
        "peak_unity_allocated_mib": max(row["unity_allocated_bytes"] for row in measured)/mib,
        "working_set_change_after_warmup_mib": (measured[-1]["working_set_bytes"]-measured[0]["working_set_bytes"])/mib,
        "limits": "Camera render callbacks and CPU frame intervals, not hardware presentation or GPU-only timing. Instrumented unattended workload; no manual/HMD acceptance or leak-free claim."
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--warmup", type=float, default=60)
    args = parser.parse_args()
    result = json.loads((args.directory/"result.json").read_text(encoding="utf-8"))
    with (args.directory/"samples.csv").open(encoding="utf-8", newline="") as stream:
        rows = list(csv.DictReader(stream))
    print(json.dumps(summarize(result, rows, args.warmup), indent=2))
