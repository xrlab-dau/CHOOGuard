"""Package reviewed acquired column components without replacing their profiles."""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / ".planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution"
PACKET = ROOT / "workers/twin/output/floor2-raster-reconstruction-20261009/columns/column-geometry-packet.json"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def mesh(record):
    path = ROOT / record["binary_path"]
    if digest(path) != record["binary_sha256"]:
        raise ValueError("Changed acquired geometry")
    data = path.read_bytes()
    magic, version, count = struct.unpack_from("<iii", data)
    if (magic, version) != (0x4347564D, 1):
        raise ValueError("Unknown CGVM")
    offset = 12
    vertices = [list(struct.unpack_from("<fff", data, offset + i * 12)) for i in range(count)]
    offset += count * 12
    uv = [list(struct.unpack_from("<ff", data, offset + i * 8)) for i in range(count)]
    offset += count * 8
    indices_count = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    triangles = list(struct.unpack_from(f"<{indices_count}i", data, offset))
    if offset + indices_count * 4 != len(data) or any(i < 0 or i >= count for i in triangles):
        raise ValueError("Invalid acquired mesh indices/tail")
    return {"vertices": vertices, "triangles": triangles, "uv": uv}


def main():
    packet = json.loads(PACKET.read_text())
    frame = ROOT / packet["unified_frame"]
    source = ROOT / packet["source_plan"]
    if digest(frame) != packet["unified_frame_sha256"] or digest(source) != packet["source_plan_sha256"]:
        raise ValueError("Source authority changed")
    objects, materials = [], {}
    for row in packet["accepted"]:
        texture = ROOT / row["texture_path"]
        if digest(texture) != row["texture_sha256"]:
            raise ValueError("Source texture changed")
        material = "native_" + row["texture_sha256"][:12]
        materials[material] = {"texture": row["texture_path"], "texture_sha256": row["texture_sha256"],
                               "uv_scale": [1, -1], "uv_offset": [0, 1]}
        for geometry, inferred in [(row["geometry"], False), (row["inferred_hidden_stem"], True)]:
            if geometry is None:
                continue
            id_ = Path(geometry["binary_path"]).stem
            item = {"id": id_, "kind": "column_inferred_continuation" if inferred else "column_native",
                    "material": material, "collider": True, **mesh(geometry),
                    "source_feature_ids": row["source_ids"], "source_geometry": geometry,
                    "source_native_part": row["source_part_id"], "center_px": row["center_px"],
                    "height_inferred": inferred, "profile_invented": False}
            objects.append(item)
    result = {"schema": "chooguard.reviewed-floorplan-mesh-package.v1", "source": packet["source_plan"],
              "source_sha256": digest(source), "spec": str(PACKET.relative_to(ROOT)), "spec_sha256": digest(PACKET),
              "frame": str(frame.relative_to(ROOT)), "frame_sha256": digest(frame), "objects": objects,
              "material_sources": materials, "coordinate_space": "Unity world XYZ, Y up", "site_similarity_percent": None}
    output = OUT / "full2f-acquired-column-mesh-package.json"
    output.write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n")
    print(json.dumps({"objects": len(objects), "triangles": sum(len(o["triangles"]) // 3 for o in objects),
                      "materials": len(materials), "package_sha256": digest(output)}))


if __name__ == "__main__":
    main()
