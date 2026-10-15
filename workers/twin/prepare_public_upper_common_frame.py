"""Re-register acquired public upper architecture in the same raw plan frame as 2F."""
import copy
import hashlib
import json
import struct
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "workers/twin/output/whole-building-registration-20261009-pass2"
OUT = ROOT / "workers/twin/output/public-upper-common-frame-20261009"
FRAME = ROOT / ".planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/unified-frame.json"


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "meshes").mkdir(exist_ok=True)
    old = json.loads((BASE / "vworld-hall-local-registration.json").read_text())
    f = json.loads(FRAME.read_text())
    raw = np.array(old["source_to_official_2F_pixels"])
    pixel = np.array(f["pixel_to_world_XZ_row_matrix"])
    old_xz = np.array(old["source_to_Unity_world_XZ"])
    new_xz = np.r_[raw[:2] @ pixel[:2], [raw[2] @ pixel[:2] + pixel[2]]]
    old_to_new = np.r_[np.linalg.inv(old_xz[:2]) @ new_xz[:2],
                       [new_xz[2] - old_xz[2] @ np.linalg.inv(old_xz[:2]) @ new_xz[:2]]]
    registration = {**old, "status": "single_official_plan_frame_for_MAIN_Hall_public_upper",
                    "source_to_Unity_world_XZ": new_xz.tolist(), "main_and_Hall_separate_raster_frame": False,
                    "old_world_XZ_to_unified_world_XZ": old_to_new.tolist(),
                    "common_frame_path": str(FRAME.relative_to(ROOT)), "common_frame_sha256": sha(FRAME),
                    "previous_registration_path": str((BASE / "vworld-hall-local-registration.json").relative_to(ROOT)),
                    "previous_registration_sha256": sha(BASE / "vworld-hall-local-registration.json"),
                    "site_accuracy_verified": False}
    rp = OUT / "registration.json"
    rp.write_text(json.dumps(registration, ensure_ascii=False, indent=2) + "\n")
    original = BASE / "public-upper-mezzanine-mesh-export-manifest.json"
    manifest = copy.deepcopy(json.loads(original.read_text()))
    manifest["registration"] = rp.name
    manifest["registration_sha256"] = sha(rp)
    manifest["coordinate_frame"] = "Unity world XYZ; common official2F raw-pixel registration for MAIN/Hall/public upper; source Y unchanged"
    manifest["previous_manifest_pin"] = {"path": str(original.relative_to(ROOT)), "sha256": sha(original)}
    manifest.pop("preview_npz", None)
    manifest.pop("preview_npz_sha256", None)
    meshes = {}
    for row in manifest["parts"]:
        source = ROOT / row["binary_path"]
        if sha(source) != row["binary_sha256"]:
            raise ValueError("Acquired source changed")
        data = bytearray(source.read_bytes())
        magic, version, count = struct.unpack_from("<iii", data)
        if (magic, version) != (0x4347564D, 1):
            raise ValueError("CGVM format")
        vertices = np.frombuffer(data, dtype="<f4", count=count*3, offset=12).reshape(-1,3).copy()
        xz = np.c_[vertices[:,[0,2]].astype(float), np.ones(count)] @ old_to_new
        vertices[:,[0,2]] = xz.astype("<f4")
        data[12:12+count*12] = vertices.tobytes()
        path = OUT / "meshes" / source.name
        path.write_bytes(data)
        row["previous_binary_pin"] = {"path": row["binary_path"], "sha256": row["binary_sha256"]}
        row["binary_path"], row["binary_sha256"] = str(path.relative_to(ROOT)), sha(path)
        row["world_XYZ_bounds"] = [vertices.min(axis=0).tolist(), vertices.max(axis=0).tolist()]
        row["UV_indices_and_vertical_positions_unchanged"] = True
        meshes[row["part_id"]] = (vertices, bytes(data))
    mp = OUT / "public-upper-common-frame-manifest.json"
    mp.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n")
    waypoint_source = BASE / "public-upper-mezzanine-fixed-stair-walk-waypoints.json"
    waypoints = json.loads(waypoint_source.read_text())
    for q in waypoints["ordered_waypoints"]:
        q["world_XYZ"][0], q["world_XYZ"][2] = (np.r_[q["source_ENU_XY"],1] @ new_xz).tolist()
    waypoints["registration_path"], waypoints["registration_sha256"] = str(rp.relative_to(ROOT)), sha(rp)
    waypoints["previous_waypoint_pin"] = {"path": str(waypoint_source.relative_to(ROOT)), "sha256": sha(waypoint_source)}
    waypoints["runtime_walk_verified"] = False
    wp = OUT / "public-upper-common-frame-waypoints.json"
    wp.write_text(json.dumps(waypoints,ensure_ascii=False,indent=2)+"\n")
    receipt_path=ROOT/".planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/upper-recovery-transaction/source7-install-receipt.json"
    receipt=json.loads(receipt_path.read_text())
    objects=[]
    for row in receipt["objects"]:
        v,data=meshes[row["part_id"]]
        count=len(v);offset=12+count*12
        uv=np.frombuffer(data,dtype="<f4",count=count*2,offset=offset).reshape(-1,2)
        offset+=count*8;indices_count=struct.unpack_from("<i",data,offset)[0]
        indices=np.frombuffer(data,dtype="<i4",count=indices_count,offset=offset+4)
        objects.append({"id":row["part_id"],"owner_path":row["owner_path"],"source_asset":row["mesh_asset"],
                        "source_file_sha256":row["mesh_pin"]["file_sha256"],"vertices":v.tolist(),"uv":uv.tolist(),"triangles":indices.tolist(),
                        "material":"acquired_native","collider":True,"kind":row["category"],"source_feature":row["part_id"]})
    package={"schema":"chooguard.source-geometry-replacement.v1","source":f["source"],"source_sha256":f["source_sha256"],
             "frame":str(FRAME.relative_to(ROOT)),"frame_sha256":sha(FRAME),"manifest":str(mp.relative_to(ROOT)),"manifest_sha256":sha(mp),
             "source_receipt":str(receipt_path.relative_to(ROOT)),"source_receipt_sha256":sha(receipt_path),"objects":objects,"site_similarity_percent":None}
    pp=OUT/"source7-common-frame-replacements.json";pp.write_text(json.dumps(package,ensure_ascii=False,separators=(",",":"))+"\n")
    print(json.dumps({"parts":len(manifest["parts"]),"active_replacements":len(objects),"package_sha256":sha(pp),"common_XZ_determinant":float(np.linalg.det(new_xz[:2]))}))


if __name__=="__main__":main()
