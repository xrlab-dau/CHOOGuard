"""Combine disjoint reviewed raw-pixel traces without duplicate wall faces."""
import hashlib
import json
from pathlib import Path

from shapely.geometry import Polygon, LineString, shape, mapping
from shapely.ops import unary_union
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / ".planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution"
FRAME = json.loads((OUT / "unified-frame.json").read_text())
SOLIDS = [
    ROOT / "workers/twin/output/floor2-raster-reconstruction-20261009/main/main-reviewed-wall-solids.json",
    ROOT / "workers/twin/output/floor2-raster-reconstruction-20261009/hall/hall-reviewed-wall-solids-v2.json",
]
TRACES = [
    ROOT / "workers/twin/output/floor2-raster-reconstruction-20261009/main/main2f-source-vectors.json",
    ROOT / "workers/twin/output/floor2-raster-reconstruction-20261009/hall/hall-architecture-source-pixels-v2.json",
]

rows, input_pins, cuts = [], [], []
for path in SOLIDS:
    data = json.loads(path.read_text())
    if data["source_sha256"] != FRAME["source_sha256"]:
        raise ValueError("Different source authority")
    input_pins.append({"path": str(path.relative_to(ROOT)), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    for item in data["solids"]:
        poly = shape(item["polygon_px"]) if isinstance(item["polygon_px"], dict) else Polygon(item["polygon_px"])
        if not poly.is_valid:
            raise ValueError(item["id"])
        rows.append((item, poly))
for path in TRACES:
    data = json.loads(path.read_text())
    input_pins.append({"path": str(path.relative_to(ROOT)), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    for item in data["layers"].get("openings", []):
        endpoints = item.get("segment_px") or item.get("endpoints_px") or item.get("centerline_px")
        if endpoints:
            # Flat caps retain source jamb end locations; depth spans the local wall.
            cuts.append((item["id"], LineString(endpoints).buffer(2, cap_style=2)))
union = unary_union([poly for _, poly in rows])
resolved = union.difference(unary_union([poly for _, poly in cuts]))
parts = [resolved] if resolved.geom_type == "Polygon" else list(resolved.geoms)
solids = []
for index, poly in enumerate(parts):
    solids.append({"id": f"PLAN2F-WALL-{index+1:03d}", "kind": "wall",
                   "polygon_px": mapping(poly), "bottom_y": 7, "top_y": 10.44,
                   "material": "plaster", "collider": True,
                   "source_feature_ids": [item["id"] for item, p in rows if poly.intersects(p)],
                   "height_basis": "Existing model-relative2F partition height, independently unmeasured"})
spec = {"schema": "chooguard.full2f-reviewed-architecture.v1", "source": FRAME["source"],
        "source_sha256": FRAME["source_sha256"], "source_pixels": [631,496],
        "source_input_pins": input_pins, "solids": solids,
        "source_reviewed_wall_runs": len(rows), "source_opening_count": len(cuts),
        "geometry_qa": {"raw_sum_area_px2": sum(p.area for _, p in rows),
                        "union_area_px2": union.area, "after_openings_area_px2": resolved.area,
                        "duplicate_overlap_area_px2": sum(p.area for _, p in rows)-union.area,
                        "valid": resolved.is_valid,
                        "preserved_openings": [{"id": name,"remaining_wall_area_px2":resolved.intersection(p).area} for name,p in cuts]},
        "review_notes": ["Only reviewed drawn walls; shop-label rectangles never extruded.",
                         "Main/Hall/connection share source pixels and one world frame.",
                         "Wall overlaps merged; source door gaps preserved.",
                         "Ceilings, structural columns and floor apertures require independent geometry decisions."],
        "site_similarity_percent": None}
(OUT / "full2f-reviewed-wall-spec.json").write_text(json.dumps(spec,ensure_ascii=False,indent=2)+"\n")
im=Image.open(ROOT/FRAME["source"]).convert("RGB").resize((2524,1984))
draw=ImageDraw.Draw(im,"RGBA")
for poly in parts:
    draw.polygon([(x*4,y*4)for x,y in poly.exterior.coords],fill=(10,180,80,110),outline=(0,130,55,230))
for name,poly in cuts:
    draw.line([(x*4,y*4)for x,y in poly.exterior.coords],fill=(190,0,190,220),width=2)
im.save(OUT/"full2f-reviewed-wall-source-overlay.png")
print(json.dumps({"reviewed_runs":len(rows),"merged_wall_objects":len(solids),"drawn_openings":len(cuts),"source_area_px2":resolved.area}))
