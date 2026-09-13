#!/usr/bin/env python3
"""Pinned TXT-v1 adapter, centimetres verified against the matching HDF5 SI archive.

Raw public numeric trajectories stay in Temp. Only per-run/binned aggregate
measurements enter foundation/physics/validation. No image/video is downloaded.
"""
import argparse
import hashlib
import json
import math
import statistics
import zipfile
from collections import defaultdict
from pathlib import Path
from urllib.request import urlopen

BASE = "https://ped.fz-juelich.de/experiments/2006.11.27_Duesseldorf_Casern/data/fd/fd1_b070/linie/"
PINNED = {
    "2006corridor2_metadata.json": "1dd97cd3f423fa483c4d5c787c4d7573c5e491f9b4fbd031a5fab82efeef5291",
    "2006corridor2_trajectories_cam1_txt.zip": "0831deedd35b843600c67bf493e6e77515fa824816824951625de5510b347b0d",
    "2006corridor2_trajectories_cam1_h5.zip": "defe7c05f6de725e9aa5950536c82d31e6b07ba2036a15cc4411d4ad3fe0d3b2",
}
RUNS = ("n14", "n17", "n17_2", "n20", "n22", "n25", "n28", "n34", "n39", "n45", "n56", "n62", "n70")
CALIBRATION = frozenset(("n14", "n25", "n39", "n62"))


def parse_txt(raw):
    """Version 1 is explicit four-column ID/frame/x_cm/y_cm; no layout guessing."""
    frames = defaultdict(dict)
    for line in raw.decode("ascii").splitlines():
        if not line.strip():
            continue
        fields = line.split()
        if len(fields) != 4:
            raise ValueError("Expected four TXT-v1 columns")
        person, frame = int(fields[0]), int(fields[1])
        if person < 0 or frame < 0 or person in frames[frame]:
            raise ValueError("Negative or duplicate identity/frame")
        frames[frame][person] = (float(fields[2]) / 100, float(fields[3]) / 100)
    return dict(frames)


def measurement_events(frames):
    """Method B variant: preceding frame of negative x=0 crossing, 25fps,
    [-2,2]m occupancy, centred frame +/-5 (0.4s) Euclidean velocity.
    Skip an event if ANY occupant lacks a finite velocity window.
    Never impute missing measurements as zero.
    """
    events = []
    for frame in sorted(frames):
        current, following = frames[frame], frames.get(frame + 1, {})
        crossings = [person for person, (x, _) in current.items()
                     if math.isfinite(x) and x >= 0 and person in following and following[person][0] < 0]
        if not crossings:
            continue
        occupants = [person for person, (x, y) in current.items() if math.isfinite(x) and math.isfinite(y) and -2 <= x <= 2]
        speeds = []
        for person in occupants:
            before, after = frames.get(frame - 5, {}).get(person), frames.get(frame + 5, {}).get(person)
            if before is None or after is None or not all(math.isfinite(v) for v in before + after):
                break
            speeds.append(math.hypot(after[0] - before[0], after[1] - before[1]) / .4)
        if not occupants or len(speeds) != len(occupants):
            continue
        for _ in crossings:
            events.append({"Frame": frame, "DensityPM": len(occupants) / 4, "SpeedMS": statistics.mean(speeds)})
    return events


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    raw_dir = args.root / "Temp/ChooGuardCrowdSources"
    raw_dir.mkdir(parents=True, exist_ok=True)
    sources = []
    for name, expected in PINNED.items():
        path = raw_dir / name
        if not path.exists():
            path.write_bytes(urlopen(BASE + name, timeout=40).read())
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != expected:
            raise ValueError(f"Pinned source changed: {name}: {actual}")
        sources.append({"Url": BASE + name, "Sha256": actual, "Bytes": path.stat().st_size})
    metadata = json.loads((raw_dir / "2006corridor2_metadata.json").read_text())
    declared = {run["run_name"]: run for run in metadata["experiment"]["parameters"]}
    cases = []
    with zipfile.ZipFile(raw_dir / "2006corridor2_trajectories_cam1_txt.zip") as archive:
        if set(archive.namelist()) != {f"fd1_{run}.txt" for run in RUNS}:
            raise ValueError("Unexpected corridor archive run set")
        for run in RUNS:
            raw = archive.read(f"fd1_{run}.txt")
            frames = parse_txt(raw)
            events = measurement_events(frames)
            if not events:
                raise ValueError(f"No complete measurement events: {run}")
            bins = defaultdict(list)
            for event in events:
                bins[event["DensityPM"]].append(event["SpeedMS"])
            cases.append({
                "Id": run, "Role": "calibration" if run in CALIBRATION else "holdout",
                "OriginalParticipants": declared[f"efd1_{run}"]["number_participants"],
                "TrajectorySha256": hashlib.sha256(raw).hexdigest(),
                "FirstAvailableFrame": min(frames), "LastAvailableFrame": max(frames),
                "FirstMeasuredFrame": min(e["Frame"] for e in events), "LastMeasuredFrame": max(e["Frame"] for e in events),
                "MeasuredEvents": len(events), "Rows": sum(len(v) for v in frames.values()),
                "MeanDensityPM": statistics.mean(e["DensityPM"] for e in events),
                "MeanSpeedMS": statistics.mean(e["SpeedMS"] for e in events),
                "SpeedStdDevMS": statistics.pstdev(e["SpeedMS"] for e in events),
                "Bins": [{"DensityPM": density, "Events": len(values), "MeanSpeedMS": statistics.mean(values)} for density, values in sorted(bins.items())],
            })
    result = {"SchemaVersion": 1, "AdapterVersion": "juelich-camera1-txt-si-method-b-1", "ArchiveDoi": "10.34735/ped.2006.1",
              "Attribution": "Forschungszentrum Juelich; Seyfried et al.; DFG KL 1873/1-1 and SE 1789/1-1",
              "SourcePaper": "https://arxiv.org/pdf/0810.1945", "Sources": sources,
              "Units": "x/y metres after TXT centimetres /100; density person/metre; speed metre/second",
              "Measurement": "25fps; x=0 negative crossing; preceding integer frame; [-2,2]m occupancy; frame +/-5 central 0.4s Euclidean speed; skip incomplete occupant windows",
              "TimeWindowLimit": "Published paper restricts analysis to stationary states but exact per-run frame bounds are absent from this metadata/archive. This adapter uses all complete available crossing windows; no exact reproduction of published stationary selection is claimed.",
              "ComparisonLimit": "Condition on measured mean local density in an equally spaced periodic synthetic corridor; camera FOV is not full initial state. No bottleneck capacity, transient trajectory, passenger, railway or field validation.",
              "Calibration": "Whole runs n14,n25,n39,n62; all other9 holdouts with n17/n17_2 kept together. Fixed radius0.15m/person repulsion5/range0.1m. Select v0/T by run-weighted squared mean-speed residual of uniform equilibrium; freeze before actual C# holdout runs. No posthoc empirical PASS threshold.",
              "Cases": cases}
    output = args.root / "foundation/physics/validation/juelich-corridor-aggregates.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + "\n")
    print(json.dumps({"output": str(output.relative_to(args.root)), "runs": len(cases), "rows": sum(c["Rows"] for c in cases)}, indent=2))


if __name__ == "__main__":
    main()
