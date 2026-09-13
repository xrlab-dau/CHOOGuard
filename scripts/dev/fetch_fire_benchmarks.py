#!/usr/bin/env python3
"""Exact-source Steckler adapter. No generic CFAST parser or validation PASS claim."""
import argparse
import csv
import hashlib
import io
import json
import math
import re
import urllib.request
from pathlib import Path

CFAST = "b43e74e0f43c704582c5782f5446597ce729a07f"
EXPERIMENT = "896a9a86fe7aa0c4b785ffee6c4a9a594ef3ec9f"
HASHES = {
    10: ("99e73b8fa4fe3b1bfee325de1395839de284a5c255d94ad44c020e326c828138", "d18603d4cd427a25ac4203beef62bea8c9d322237348f93e05a44fd9d2912c5a", "17c23875df8762dd4d7ac02b5e80c2510a32c331b0821b5c6060b09e17949bb7"),
    14: ("2251e079d0dc0a951d77dd76d0921643786bd895d9ec5e5c04879891861bf0de", "39051138abe50f1dcd0710a38e26c81351d57e717a7618a1b2179d12d51a1f90", "6d531ff464429ab08cf01b75e657b89059524550f8af1d0c8fd3474764f01363"),
    20: ("faff2c60feab9f3661038a83d234d0714704291c984309be471a7a5a94d68f3a", "5ed342107236f10397116b405bd512f32c8d54f09733b45003ee4cb6ae15137a", "81c9de95496802ed73fea63989f26a7d9f561a70df3d0de1cde3be4019c2d48e"),
}


def verified_bytes(url, expected):
    with urllib.request.urlopen(url, timeout=30) as response:
        raw = response.read(100001)
    if len(raw) > 100000 or hashlib.sha256(raw).hexdigest() != expected:
        raise ValueError(f"Pinned fire reference mismatch: {url}")
    return raw


def number(text, key):
    match = re.search(r"\b" + re.escape(key) + r"\s*=\s*([0-9.+-]+)", text)
    if not match:
        raise ValueError(f"Missing reference field {key}")
    result = float(match[1])
    if not math.isfinite(result):
        raise ValueError("Nonfinite reference input")
    return result


def case(case_id):
    urls = [
        f"https://raw.githubusercontent.com/firemodels/cfast/{CFAST}/Validation/Steckler_Compartment/Steckler_{case_id:03}.in",
        f"https://raw.githubusercontent.com/firemodels/exp/{EXPERIMENT}/Steckler_Compartment/Steckler_Test_{case_id}_layer.csv",
        f"https://raw.githubusercontent.com/firemodels/exp/{EXPERIMENT}/Steckler_Compartment/Steckler_Test_{case_id}.csv",
    ]
    raw = [verified_bytes(url, digest) for url, digest in zip(urls, HASHES[case_id])]
    source = raw[0].decode()
    geometry = next(line for line in source.splitlines() if "DEPTH =" in line and "HEIGHT =" in line)
    vent = next(line for line in source.splitlines() if line.startswith("&VENT"))
    material = next(line for line in source.splitlines() if "CONDUCTIVITY =" in line)
    table = next(line for line in source.splitlines() if "DATA = 0," in line)
    fire = [float(v.strip()) for v in table.split("DATA =", 1)[1].split("/")[0].split(",")]
    width, depth, height = [number(geometry, k) for k in ("WIDTH", "DEPTH", "HEIGHT")]
    area = 2 * width * depth + 2 * (width + depth) * height
    layers = list(csv.DictReader(io.StringIO(raw[1].decode()), skipinitialspace=True))
    if len(layers) != 4 or layers[1]["Height"] != layers[2]["Height"]:
        raise ValueError("This adapter requires the four-row Steckler layer summary")
    samples = []
    for row in csv.DictReader(io.StringIO(raw[2].decode()), skipinitialspace=True):
        velocity = float(row["V_C"])
        samples.append({"ElevationM": float(row["Height"]), "HasCenterVelocity": math.isfinite(velocity),
                        "CenterVelocityMS": velocity if math.isfinite(velocity) else 0})
    return {
        "Id": f"Steckler_{case_id:03}", "Role": "calibration" if case_id == 10 else "holdout",
        "Sources": [{"Url": u, "Sha256": h} for u, h in zip(urls, HASHES[case_id])],
        "DurationSeconds": number(source, "SIMULATION"), "SteadyWindowStartSeconds": 1500,
        "Definition": {
            "SchemaVersion": 1, "ModelVersion": "conservative-two-zone-1",
            "PressureConvention": "uniform-thermodynamic-reference-with-ambient-hydrostatic-offset",
            "AmbientTemperatureK": number(source, "INTERIOR_TEMPERATURE") + 273.15,
            "AmbientPressurePa": number(source, "PRESSURE"), "SpecificHeatCp": 1012, "Gamma": 1.4, "GravityMPerS2": 9.81,
            "Cells": [{"Id": "room", "WidthM": width, "DepthM": depth, "HeightM": height, "FloorElevationM": 0,
                       "InitialUpperVolumeFraction": .01,
                       "WallHeatCapacityJPerK": area * number(material, "THICKNESS") * number(material, "DENSITY") * number(material, "SPECIFIC_HEAT") * 1000,
                       "InsideHeatTransferWPerM2K": 13.55255681695491, "OutsideHeatTransferWPerM2K": 6.577414129868992,
                       "WallThicknessM": number(material, "THICKNESS"), "WallConductivityWPerMK": number(material, "CONDUCTIVITY")}],
            "Doors": [{"Id": "door", "FromCellId": "room", "ToCellId": "", "WidthM": number(vent, "WIDTH"),
                       "HeightM": number(vent, "HEIGHT"), "BottomElevationM": number(vent, "BOTTOM"),
                       "DischargeCoefficient": .7, "OpeningFraction": 1}], "Fans": []},
        "Forcing": {"Sources": [{"CellId": "room", "HeatReleaseW": fire[1] * 1000,
                                  "FuelMassKgPerSecond": fire[1] / number(source, "HEAT_OF_COMBUSTION"),
                                  "SmokeMassKgPerSecond": fire[1] / number(source, "HEAT_OF_COMBUSTION") * fire[5],
                                  "RadiationFraction": number(source, "RADIATIVE_FRACTION"),
                                  "HeightM": fire[2], "PlumeAreaM2": fire[3], "PlumeMultiplier": 2.1062109674855467, "EnablePlume": True}],
                    "Doors": [], "Fans": []},
        "ReferenceUpperC": float(layers[2]["Temp"]), "ReferenceLowerC": float(layers[0]["Temp"]),
        "ReferenceInterfaceM": float(layers[1]["Height"]), "VelocitySamples": samples,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Write the pinned, public numeric fixture into this repository")
    args = parser.parse_args()
    target = Path(__file__).resolve().parents[2] / "foundation/physics/validation/steckler-cases.json"
    data = {"SchemaVersion": 1, "Classification": "public_experiment_numeric_adapter_not_validation_pass",
            "Calibration": "Three effective coefficients fitted only to Steckler10 layer scalars in a prior reduced-model research prototype; frozen for14/20. Actual C# outputs must be compared independently.",
            "Limits": ["Four-row layer summaries are not time series or direct optical smoke measurements.",
                       "HasCenterVelocity=false preserves source NaN; numeric0 then is an unused serialization slot, never a measurement.",
                       "Centerline jet and nominal area-mean velocities are distinct comparison operators.",
                       "The simplified reference Fiberboard wall is not a measured multilayer apparatus reconstruction.",
                       "Effective plume/wall coefficients are calibration inputs, not exact CFAST parity or railway parameters."],
            "Cases": [case(i) for i in HASHES]}
    serialized = json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.write:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(serialized)
    elif not target.is_file() or target.read_text() != serialized:
        raise SystemExit("Reference adapter differs or is missing; review then use --write")
    print(json.dumps({"cases": 3, "sources": 9, "path": str(target.relative_to(target.parents[3])),
                      "sha256": hashlib.sha256(serialized.encode()).hexdigest(), "status": "written" if args.write else "verified"}))


if __name__ == "__main__":
    main()
