"""Bind reviewed MAIN 1F wall footprints to the shared building frame.

The detached inset and held stall/glazing controls are never extruded here.
Existing source-PDF wall datum 0..6.7 is model-relative, not a measurement.
"""
import hashlib
import json
from pathlib import Path

from shapely.geometry import shape, mapping
from shapely.ops import unary_union

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / ".planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution"
BASE = ROOT / "workers/twin/output/floor1-architecture-source-trace-20261009"


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


registration_path = BASE / "registration/registration.json"
registration = json.loads(registration_path.read_text())
for pin in registration["pins"]:
    if sha(ROOT / pin["path"]) != pin["sha256"]:
        raise ValueError("Registration source changed: " + pin["path"])
source_path = BASE / "floor1-reviewed-wall-solids.json"
source = json.loads(source_path.read_text())
rows = [r for r in source["solids"] if r["region"] == "MAIN"]
assert len(rows) == 57 and all(r["source_geometry_ready"] for r in rows)
polygons = [(r, shape(r["polygon_px"])) for r in rows]
union = unary_union([p for _, p in polygons])
assert union.is_valid and not union.is_empty
parts = [union] if union.geom_type == "Polygon" else list(union.geoms)
frame = {
    "schema": "chooguard.main1f-common-source-frame-binding.v1",
    "source": source["source"], "source_sha256": source["source_sha256"],
    "pixel_to_world_XZ_row_matrix": registration["source1f_to_common_world_XZ_row_matrix"],
    "paired_source_registration": str(registration_path.relative_to(ROOT)),
    "paired_source_registration_sha256": sha(registration_path),
    "metric_dimensions_measured": False,
    "site_similarity_percent": None,
}
frame_path = OUT / "main1f-common-frame.json"
frame_path.write_text(json.dumps(frame, ensure_ascii=False, indent=2) + "\n")
spec = {
    "schema": "chooguard.main1f-reviewed-architecture.v1",
    "source": source["source"], "source_sha256": source["source_sha256"],
    "source_input_pins": [
        {"path": str(p.relative_to(ROOT)), "sha256": sha(p)}
        for p in [source_path, registration_path]
    ],
    "source_reviewed_wall_footprints": len(rows),
    "source_main_aperture_controls": 52,
    "source_detached_inset_installed": False,
    "held_stall_and_glazing_controls_installed": False,
    "native_relative_height_basis": "Existing source-PDF 1F walls 0..6.7 and actual 2F slab underside Y6.7; not surveyed. WCstall controls excluded; no inferred headers.",
    "solids": [
        {
            "id": f"PLAN1F-WALL-{i + 1:03d}", "kind": "wall",
            "polygon_px": mapping(p), "bottom_y": 0, "top_y": 6.7,
            "material": "plaster", "collider": True,
            "source_feature_ids": [r["id"] for r, q in polygons if p.intersects(q)],
            "height_measured": False,
        }
        for i, p in enumerate(parts)
    ],
    "geometry_qa": {"union_area_px2": union.area,
                    "raw_sum_area_px2": sum(p.area for _, p in polygons),
                    "duplicate_corner_overlap_removed_px2": sum(p.area for _, p in polygons) - union.area},
    "site_similarity_percent": None,
}
path = OUT / "main1f-reviewed-wall-spec.json"
path.write_text(json.dumps(spec, ensure_ascii=False, indent=2) + "\n")
print(json.dumps({"source_wall_footprints": len(rows), "merged_owners": len(parts),
                  "frame_sha256": sha(frame_path), "spec_sha256": sha(path)}))
