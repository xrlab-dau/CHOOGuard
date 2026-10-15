"""Execute selected public interior reference blocks, retaining actual relative geometry.

Each block has its own unknown scale/origin. No scene-wide alignment, fabricated
surface filling, field certificate, or production placement is emitted.
"""
from __future__ import annotations

import argparse
from argparse import Namespace
import json
from pathlib import Path
import shutil
import subprocess
import struct
import sys
import time

import metric_pipeline as metric
import reference_study

BLOCKS = [
    {"id": "retail-corner", "clip": "RY2qvE0Tugk_041-154.webm", "start": 68,
     "duration": 20, "mask": .82, "visible": "shop corner, cafe glass frontage, ceiling and columns"},
    {"id": "food-court", "clip": "RY2qvE0Tugk_215-670.webm", "start": 32,
     "duration": 26, "mask": .72, "visible": "food court counters, slat ceiling and upper furniture"},
    {"id": "ticketing-wall", "clip": "RY2qvE0Tugk_215-670.webm", "start": 238,
     "duration": 22, "mask": .74, "visible": "ticket-machine wall, information boards, pillar and ceiling"},
    {"id": "retail-corridor", "clip": "RY2qvE0Tugk_215-670.webm", "start": 294,
     "duration": 26, "mask": .85, "visible": "retail corridor ceiling, glass fronts and columns"},
    {"id": "information-corridor", "clip": "RY2qvE0Tugk_215-670.webm", "start": 332,
     "duration": 22, "mask": .85, "visible": "adjacent corridor, information column and ceiling"},
    {"id": "information-corridor-static", "clip": "RY2qvE0Tugk_215-670.webm", "start": 336,
     "duration": 18, "mask": .85, "visible": "connected stable continuation after the unconnectable camera turn"},
    {"id": "corridor-connector", "clip": "RY2qvE0Tugk_215-670.webm", "start": 312,
     "duration": 30, "mask": .85, "visible": "overlapping connecting walk between two corridor blocks"},
]


def contact_sheet(directory: Path, output: Path):
    import cv2
    import numpy as np
    names = sorted((directory / "images").glob("*.jpg"))
    indices = sorted(set(round(i * (len(names) - 1) / 11) for i in range(min(12, len(names)))))
    frames = []
    for index in indices:
        frame = cv2.resize(cv2.imread(str(names[index])), (384, 216))
        cv2.rectangle(frame, (0, 0), (170, 28), (0, 0, 0), -1)
        cv2.putText(frame, names[index].name, (8, 20), cv2.FONT_HERSHEY_SIMPLEX, .6, (255,255,255), 1)
        frames.append(frame)
    sheet = np.zeros((((len(frames)+3)//4)*216, 4*384, 3), dtype=np.uint8)
    for i, frame in enumerate(frames):
        sheet[i//4*216:(i//4+1)*216, i%4*384:(i%4+1)*384] = frame
    cv2.imwrite(str(output), sheet)


def dense_point_qa(path: Path):
    """Read OpenMVS variable visibility-list PLY without treating it as fixed rows."""
    import numpy as np
    with path.open("rb") as stream:
        header=[]
        while True:
            line=stream.readline().decode("ascii").strip();header.append(line)
            if line=="end_header":break
        metric.require("format binary_little_endian 1.0" in header,"Unexpected dense PLY encoding")
        count=int(next(line.split()[-1] for line in header if line.startswith("element vertex ")))
        expected=["property float32 x","property float32 y","property float32 z","property uint8 red",
                  "property uint8 green","property uint8 blue","property float32 nx","property float32 ny","property float32 nz",
                  "property list uint8 uint32 view_indices","property list uint8 float32 view_weights"]
        metric.require([line for line in header if line.startswith("property ")]==expected,"Unexpected dense PLY layout")
        positions=np.empty((count,3));visibility=np.empty(count,dtype=int)
        for index in range(count):
            data=stream.read(27);metric.require(len(data)==27,"Truncated dense point")
            positions[index]=struct.unpack_from("<fff",data)
            n=stream.read(1);metric.require(n,"Missing dense visibility length")
            visibility[index]=n[0];metric.require(len(stream.read(n[0]*4))==n[0]*4,"Truncated dense visibility")
            n=stream.read(1);metric.require(n,"Missing dense visibility weights length")
            metric.require(len(stream.read(n[0]*4))==n[0]*4,"Truncated dense visibility weights")
        metric.require(not stream.read(1),"Unexpected bytes after dense point cloud")
    return {"dense_points":count,"dense_coordinates_finite":bool(np.isfinite(positions).all()),
            "dense_support_view_count_min":int(visibility.min()),"dense_support_view_count_median":float(np.median(visibility)),
            "dense_bounds_relative_units":[positions.min(0).tolist(),positions.max(0).tolist()]}


def geometry_qa(directory: Path):
    directory = directory.resolve()
    import numpy as np
    import pycolmap
    import trimesh
    study = json.loads((directory / "reference-study.json").read_text())
    result = {"role": "reference_only", "metric_verified": False, "placement_export_allowed": False,
              "status": study["status"], "sampling": study["sampling"], "source": study["source"]}
    if study["status"] != "reconstructed_relative_reference":
        result["failure_reason"] = study.get("failure_reason")
        return result
    model_dirs = [p for p in (directory / "sparse").iterdir() if (p/"cameras.bin").exists()]
    selected_id = study.get("connected_candidate_selection",{}).get("selected_model_id")
    reconstruction = pycolmap.Reconstruction(directory/"sparse"/selected_id if selected_id else model_dirs[0])
    undistorted = pycolmap.Reconstruction(directory/"colmap/sparse")
    cameras = []
    track_lengths = []
    for image_id, image in reconstruction.images.items():
        pose = image.cam_from_world()
        world_from_cam = pose.inverse().matrix()
        source_camera = reconstruction.cameras[image.camera_id]
        undistorted_image = undistorted.images[image_id]
        camera = undistorted.cameras[undistorted_image.camera_id]
        cameras.append({"image_id": image_id, "image": image.name, "width": camera.width,
                        "height": camera.height, "calibration_matrix": camera.calibration_matrix().tolist(),
                        "world_from_camera_cv": world_from_cam.tolist(),
                        "projection_centre": image.projection_center().tolist(),
                        "observations3d": image.num_points3D,
                        "source_camera_model": source_camera.model_name,
                        "source_camera_parameters": source_camera.params.tolist(),
                        "render_calibration_basis": "retained COLMAP undistorted camera",
                        "undistorted_image":str((directory/"colmap/images"/image.name).relative_to(metric.ROOT))})
    for point in reconstruction.points3D.values():
        track_lengths.append(point.track.length())
    cameras.sort(key=lambda item: item["image"])
    centres = np.asarray([item["projection_centre"] for item in cameras])
    mesh = trimesh.load(directory / "scene_mesh.ply", force="mesh", process=False)
    textured_path=directory/"reference_observed_surfaces.obj"
    if not textured_path.exists():textured_path=directory/"reference_photographic.obj"
    if not textured_path.exists():textured_path=directory/"reference_textured.obj"
    textured = trimesh.load(textured_path, force="mesh", process=False)
    dense = dense_point_qa(directory / "scene_dense.ply")
    result.update(
        camera_count=len(cameras), sparse_points=reconstruction.num_points3D(),
        sparse_reprojection_error_pixels=reconstruction.compute_mean_reprojection_error(),
        sparse_track_length_median=float(np.median(track_lengths)),
        **dense, mesh_vertices=len(mesh.vertices), mesh_triangles=len(mesh.faces),
        textured_vertices_with_uv_seams=len(textured.vertices),
        observed_surface_triangles=len(textured.faces),
        observed_surface_bounds_relative_units=textured.bounds.tolist(),
        preferred_textured_mesh=str(textured_path.relative_to(metric.ROOT)),
        mesh_coordinates_finite=bool(np.isfinite(mesh.vertices).all()),
        mesh_boundary_edge_count=int(np.sum(np.bincount(mesh.edges_unique_inverse)==1)),
        mesh_bounds_relative_units=mesh.bounds.tolist(), camera_bounds_relative_units=[centres.min(0).tolist(),centres.max(0).tolist()],
        camera_path_length_relative_units=float(np.linalg.norm(np.diff(centres,axis=0),axis=1).sum()),
        mesh_is_watertight=bool(mesh.is_watertight), mesh_winding_consistent=bool(mesh.is_winding_consistent),
        relative_units_have_unknown_metre_scale=True,
        anchors={"anchor_basis": "registered photogrammetry camera poses only",
                 "first_observed_camera": cameras[0]["image"], "first_camera_position": cameras[0]["projection_centre"]},
        connected_candidate_selection=study.get("connected_candidate_selection"),
        coverage_limits=["mask excludes image bottom; moving people and reflected surfaces can remain in retained upper region",
                         "camera visibility is local to this block; no verified cross-block datum",
                         "mesh holes/unseen back faces preserved; watertight geometry is not assumed"])
    (directory / "camera-poses.relative.json").write_text(json.dumps({"schema":"chooguard.relative-camera-poses.v1",
        "coordinate_convention":"COLMAP right-handed, camera x-right/y-down/z-forward, unknown scale/origin",
        "metric_verified":False,"cameras":cameras},indent=2)+"\n")
    result["artifact_hashes"] = [{"path": str(p.relative_to(metric.ROOT)), "bytes": p.stat().st_size,
                                 "sha256": metric.digest(p)} for p in directory.iterdir()
                                if p.suffix.lower() in {".obj", ".ply", ".mtl", ".jpg", ".json"}]
    return result


def recover_nested_candidates(directory: Path):
    """Select one observed connected model when smaller candidates are subsets.

    COLMAP can retain a tiny initialization model and later reconstruct those
    same image IDs into a complete larger model. That is different from
    merging disconnected relative gauges. Preserve both sparse models and the
    original failed receipt, selecting only the larger single model.
    """
    directory = directory.resolve()
    import cv2
    import numpy as np
    import pycolmap
    study_path = directory / "reference-study.json"
    study = json.loads(study_path.read_text())
    if study["status"] == "reconstructed_relative_reference":
        return
    metric.require(study.get("failure_reason") == "Pilot yielded disconnected/no models; do not merge relative gauges",
                   "Recovery only handles explicit COLMAP candidate-model count failure")
    candidates = {}
    for path in (directory / "sparse").iterdir():
        if (path/"cameras.bin").exists():
            candidates[path.name] = pycolmap.Reconstruction(path)
    metric.require(candidates, "No actual sparse candidates to recover")
    selected_id = max(candidates,key=lambda key: candidates[key].num_reg_images())
    selected = candidates[selected_id]
    selected_ids = set(selected.reg_image_ids())
    image_count = len(list((directory/"images").glob("*.jpg")))
    metric.require(len(selected_ids)/image_count >= .9, "Connected selected model does not cover 90% of extracted images")
    metric.require(selected.num_points3D() >= 100, "Insufficient selected sparse geometry")
    metric.require(all(set(model.reg_image_ids()).issubset(selected_ids) for model in candidates.values()),
                   "Candidate models contain distinct unconnected cameras; do not merge")
    selection = {"selected_model_id":selected_id,"basis":"one connected >=90% model; every other candidate camera ID is its subset",
                 "merged_relative_models":False,"candidates":[{"model_id":key,"camera_ids":sorted(model.reg_image_ids()),
                 "registered_images":model.num_reg_images(),"points3d":model.num_points3D()} for key,model in candidates.items()]}
    shutil.copyfile(study_path,directory/"reference-study.before-connected-selection.json")
    receipt_path = directory/"execution-receipt.json"
    shutil.copyfile(receipt_path,directory/"execution-receipt.before-connected-selection.json")
    receipt = json.loads(receipt_path.read_text())
    study["connected_candidate_selection"] = selection
    study["status"] = "running_connected_candidate_selection"
    study.pop("failure_reason",None)
    def save():
        study_path.write_text(json.dumps(study,indent=2)+"\n")
        receipt["status"] = study["status"]
        receipt["connected_candidate_selection"] = selection
        receipt_path.write_text(json.dumps(receipt,indent=2)+"\n")
    def external(argv,label):
        start = time.monotonic()
        with (directory/(label+".log")).open("w") as stream:
            run = subprocess.run(argv,cwd=directory,stdout=stream,stderr=subprocess.STDOUT,check=False)
        receipt["stages"].append({"stage":label,"argv":argv,"exit_code":run.returncode,
            "elapsed_seconds":time.monotonic()-start,"log":str(directory/(label+".log"))})
        save()
        metric.require(run.returncode==0,f"Connected candidate recovery {label} failed")
    try:
        save()
        selected.export_PLY(directory/"reference_sparse.ply")
        start = time.monotonic()
        pycolmap.undistort_images(directory/"colmap",directory/"sparse"/selected_id,directory/"images",num_threads=2,
            undistort_options=pycolmap.UndistortCameraOptions(max_image_size=study["sampling"]["width"]))
        receipt["stages"].append({"stage":"undistort","exit_code":0,"elapsed_seconds":time.monotonic()-start,
                                  "api":"pycolmap "+pycolmap.__version__})
        width = study["sampling"]["width"]
        for image in (directory/"colmap/images").glob("*.jpg"):
            pixels = cv2.imread(str(image)); mask=np.zeros(pixels.shape[:2],np.uint8)
            mask[:int(mask.shape[0]*study["sampling"]["static_top_fraction"]),:] = 255
            cv2.imwrite(str(image.with_name(image.stem+".mask.png")),mask)
        mvs=metric.ROOT/".tools/openmvs-v2.4.0"
        external([str(mvs/"InterfaceCOLMAP"),"-i","colmap","-o","scene.mvs","--image-folder","images","--max-threads","2"],"interface")
        external([str(mvs/"DensifyPointCloud"),"scene.mvs","--resolution-level","1","--min-resolution","160",
            "--max-resolution",str(width),"--number-views","4","--number-views-fuse","2","--ignore-mask-label","0",
            "--max-threads","2","--tower-mode","0","--estimate-roi","0","--crop-to-roi","0","-o","scene_dense.mvs"],"dense")
        external([str(mvs/"ReconstructMesh"),"scene_dense.mvs","--max-threads","2","--close-holes","0","-o","scene_mesh.ply"],"mesh")
        external([str(mvs/"TextureMesh"),"scene_dense.mvs","--mesh-file","scene_mesh.ply","--max-threads","2",
            "--ignore-mask-label","0","--close-holes","0","--export-type","obj","-o","reference_textured.obj"],"texture")
        study["status"]="reconstructed_relative_reference"
        study["artifacts"]=[{"path":str(p.relative_to(directory)),"sha256":metric.digest(p),"bytes":p.stat().st_size}
                             for p in directory.glob("*") if p.suffix.lower() in {".ply",".obj",".mtl",".png"}]
        save()
    except Exception as exc:
        study.update(status="failed_quality_or_stage",failure_reason=str(exc)); save(); raise


def align_corridors(output: Path, evidence: Path):
    """Fit relative Sim(3) from identical retained frames and shared camera poses.

    Camera orientations constrain rotation even for a mostly straight walk.
    Withheld shared-camera positions check the relative fit. These are derived
    visual observations, never independent field-survey controls.
    """
    import numpy as np
    output=output.resolve()
    def observations(block):
        directory=output/block
        poses=json.loads((directory/"camera-poses.relative.json").read_text())["cameras"]
        return {metric.digest(directory/"images"/pose["image"]):pose for pose in poses}
    def pair(src_name,target_name):
        src,tgt=observations(src_name),observations(target_name)
        common=sorted(set(src)&set(tgt),key=lambda key:src[key]["image"])
        result={"source_block":src_name,"target_block":target_name,"identical_frame_count":len(common),
                "role":"reference_only","metric_verified":False,"independent_field_measurements":False,
                "source_frame_hashes":common,"fit_basis":"identical JPEG bytes and their independently reconstructed camera poses"}
        if len(common)<8:
            return dict(result,status="insufficient_shared_frames")
        xs=np.asarray([src[key]["projection_centre"] for key in common])
        ys=np.asarray([tgt[key]["projection_centre"] for key in common])
        rs=np.asarray([src[key]["world_from_camera_cv"] for key in common])[:,:3,:3]
        rt=np.asarray([tgt[key]["world_from_camera_cv"] for key in common])[:,:3,:3]
        holdout=np.arange(len(common))%3==1;fit=~holdout
        basis=np.einsum("nij,nkj->nik",rt[fit],rs[fit]).sum(axis=0)
        u,_,vt=np.linalg.svd(basis);rotation=u@np.diag([1,1,np.linalg.det(u@vt)])@vt
        xmean,ymean=xs[fit].mean(axis=0),ys[fit].mean(axis=0)
        xrot=(xs[fit]-xmean)@rotation.T;ycentre=ys[fit]-ymean
        denom=np.sum(xrot*xrot)
        metric.require(denom>1e-8,"No actual shared-camera baseline")
        scale=float(np.sum(xrot*ycentre)/denom)
        translation=ymean-scale*(rotation@xmean)
        predicted=scale*(xs@rotation.T)+translation
        residual=np.linalg.norm(predicted-ys,axis=1)
        extent=float(np.linalg.norm(np.ptp(ys,axis=0)))
        metric.require(extent>1e-8,"No target-camera baseline")
        orientation=rotation[None,:,:]@rs
        delta=rt@np.swapaxes(orientation,1,2)
        angles=np.degrees(np.arccos(np.clip((np.trace(delta,axis1=1,axis2=2)-1)/2,-1,1)))
        transform=np.eye(4);transform[:3,:3]=rotation*scale;transform[:3,3]=translation
        accepted=scale>0 and float(residual.max()/extent)<=.05 and float(np.median(residual)/extent)<=.02 and float(angles.max())<=3
        result.update(status="relative_overlap_aligned" if accepted else "relative_alignment_rejected",
            scale_relative_units=scale, rotation=rotation.tolist(),translation_relative_units=translation.tolist(),
            target_from_source_relative_matrix=transform.tolist(), shared_camera_extent_target_relative_units=extent,
            residual_max_relative_units=float(residual.max()),residual_median_relative_units=float(np.median(residual)),
            holdout_residual_max_relative_units=float(residual[holdout].max()),
            residual_max_fraction_of_shared_camera_extent=float(residual.max()/extent),
            camera_orientation_error_max_degrees=float(angles.max()),
            fit_frame_hashes=[common[i] for i in np.flatnonzero(fit)],holdout_frame_hashes=[common[i] for i in np.flatnonzero(holdout)],
            comparison_limits={"max_residual_fraction_of_shared_camera_extent":.05,"median_fraction":.02,"max_camera_orientation_degrees":3},
            limitations=["unknown metre scale and no building survey datum","shared visual frames are not independent physical holdouts",
                         "no mesh fusion, missing surface completion or production equipment placement"])
        return result
    comparisons=[pair("retail-corridor","corridor-connector"),pair("information-corridor-static","corridor-connector")]
    result={"schema":"chooguard.relative-block-alignment.v1","role":"reference_only","metric_verified":False,
            "placement_export_allowed":False,"target_block":"corridor-connector","comparisons":comparisons}
    evidence.mkdir(parents=True,exist_ok=True)
    (evidence/"corridor-relative-alignment.json").write_text(json.dumps(result,indent=2)+"\n")
    print(json.dumps(result,indent=2))
    return 0 if all(item["status"]=="relative_overlap_aligned" for item in comparisons) else 2


def retexture_photographic(directory: Path):
    """Preserve initial color artifacts and texture observed faces without seam solving."""
    directory=directory.resolve()
    mesh=directory/"scene_mesh.ply"
    metric.require(mesh.exists(),"Actual reconstructed source mesh required")
    output=directory/"reference_photographic.obj"
    metric.require(not output.exists(),"Fresh photographic texture output required")
    before=metric.digest(mesh)
    argv=[str(metric.ROOT/".tools/openmvs-v2.4.0/TextureMesh"),"scene_dense.mvs","--mesh-file","scene_mesh.ply",
          "--max-threads","2","--ignore-mask-label","0","--close-holes","0","--global-seam-leveling","0",
          "--local-seam-leveling","0","--sharpness-weight","0","--export-type","obj","-o",output.name]
    start=time.monotonic()
    with (directory/"texture-photographic.log").open("w") as stream:
        completed=subprocess.run(argv,cwd=directory,stdout=stream,stderr=subprocess.STDOUT,check=False)
    receipt={"stage":"texture-photographic","argv":argv,"exit_code":completed.returncode,
             "elapsed_seconds":time.monotonic()-start,"source_mesh_sha256":before,
             "source_mesh_unchanged":metric.digest(mesh)==before,
             "basis":"original observed photographic colors; global/local seam solver and sharpening disabled after rendered color artifacts",
             "log":str((directory/"texture-photographic.log").relative_to(metric.ROOT))}
    (directory/"photographic-texture-receipt.json").write_text(json.dumps(receipt,indent=2)+"\n")
    metric.require(completed.returncode==0 and receipt["source_mesh_unchanged"],"Photographic texture failed or changed source geometry")
    return receipt


def retain_observed_surfaces(directory: Path):
    """Remove identifiable OpenMVS NO_ID face patches without filling anything.

    OpenMVS v2.4.0 SceneTexture.cpp assigns all three UVs the same coordinate
    for faces with no photographic view. Retain originals and emit a subset;
    this does not identify reflections or all dynamic reconstruction errors.
    """
    directory=directory.resolve();source=directory/"reference_photographic.obj"
    target=directory/"reference_observed_surfaces.obj"
    metric.require(source.exists() and not target.exists(),"Actual source and fresh observed subset required")
    lines=source.read_text().splitlines(keepends=True);uvs=[]
    for line in lines:
        if line.startswith("vt "):uvs.append(tuple(float(v) for v in line.split()[1:3]))
    retained=[];removed=[];face_count=0
    for line in lines:
        if not line.startswith("f "):
            retained.append(line);continue
        indices=[int(part.split("/")[1])-1 for part in line.split()[1:]]
        metric.require(len(indices)==3 and all(0<=index<len(uvs) for index in indices),"Expected triangulated valid textured faces")
        points=[uvs[index] for index in indices]
        if all(abs(points[i][j]-points[0][j])<1e-10 for i in range(1,3) for j in range(2)):
            removed.append(face_count)
        else:retained.append(line)
        face_count+=1
    metric.require(face_count>0 and len(removed)<face_count,"No observed source faces remain")
    target.write_text("".join(retained))
    receipt={"schema":"chooguard.observed-surface-subset.v1","role":"reference_only","metric_verified":False,
        "placement_export_allowed":False,"source_obj_sha256":metric.digest(source),"derived_obj_sha256":metric.digest(target),
        "original_triangle_count":face_count,"retained_triangle_count":face_count-len(removed),
        "removed_no_photographic_view_triangles":len(removed),"removed_original_face_indices_zero_based":removed,
        "source_implementation":"https://github.com/cdcseacave/openMVS/blob/v2.4.0/libs/MVS/SceneTexture.cpp#L2034-L2043",
        "basis":"all three texture coordinates identical, the explicit uniform NO_ID patch emitted by OpenMVS",
        "geometry_completion_performed":False,"limitations":"This only excludes identifiable no-view patches, not every reflective/dynamic/ambiguous surface."}
    (directory/"observed-surface-subset-receipt.json").write_text(json.dumps(receipt,indent=2)+"\n")
    return receipt


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--block", action="append", choices=[b["id"] for b in BLOCKS])
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--recover-existing", action="store_true")
    parser.add_argument("--align-corridors", action="store_true")
    parser.add_argument("--retexture-photographic", action="store_true")
    parser.add_argument("--retain-observed-surfaces", action="store_true")
    args = parser.parse_args(argv)
    if args.align_corridors:
        return align_corridors(args.output,args.evidence)
    selected = [b for b in BLOCKS if not args.block or b["id"] in args.block]
    args.evidence.mkdir(parents=True, exist_ok=True)
    result = {"schema":"chooguard.interior-reference-blocks.v1", "role":"reference_only",
              "metric_verified":False, "placement_export_allowed":False, "blocks":[]}
    for block in selected:
        directory = args.output / block["id"]
        spec = Namespace(reference_study=True,source=metric.ROOT/"asset-library/research-public/2026-10-08/measured-twin/clips"/block["clip"],
             source_url="https://www.youtube.com/watch?v=RY2qvE0Tugk",source_id="RY2qvE0Tugk",
             capture_date=None,upload_date="2023-05-01",clip_start=block["start"],duration=block["duration"],
             fps=2,width=960,static_top_fraction=block["mask"],output=directory,texture=True)
        if not args.execute:
            result["blocks"].append({"block": block,"plan":reference_study.plan(spec)})
            continue
        print(f"Starting actual block {block['id']}",flush=True)
        start = time.monotonic()
        try:
            if args.retain_observed_surfaces:
                retain_observed_surfaces(directory)
            elif args.retexture_photographic:
                retexture_photographic(directory)
            elif args.recover_existing:
                recover_nested_candidates(directory)
            else:
                reference_study.run_reference(spec)
        except Exception as exc:
            print(f"Block {block['id']} retained failure: {exc}",flush=True)
            study_path=directory/"reference-study.json"
            if study_path.exists() and json.loads(study_path.read_text()).get("failure_reason")=="Pilot yielded disconnected/no models; do not merge relative gauges":
                try:
                    recover_nested_candidates(directory)
                except Exception as recovery_exc:
                    print(f"Block {block['id']} connected candidate not accepted: {recovery_exc}",flush=True)
        if (directory/"images").exists():
            contact_sheet(directory,args.evidence/(block["id"]+"-frames.jpg"))
        if (directory/"reference-study.json").exists():
            qa = geometry_qa(directory)
        else:
            qa = {"status":"failed_before_stage", "metric_verified":False}
        qa.update(block=block,elapsed_seconds=time.monotonic()-start)
        (args.evidence/(block["id"]+"-qa.json")).write_text(json.dumps(qa,indent=2)+"\n")
        result["blocks"].append(qa)
        (args.evidence/"blocks-summary.json").write_text(json.dumps(result,indent=2)+"\n")
        print(json.dumps({"block":block["id"],"status":qa["status"],"cameras":qa.get("camera_count"),
                          "triangles":qa.get("mesh_triangles"),"elapsed_seconds":qa["elapsed_seconds"]}),flush=True)
    print(json.dumps(result,indent=2))
    return 0 if all(b.get("status","preview")=="reconstructed_relative_reference" for b in result["blocks"]) or not args.execute else 2


if __name__ == "__main__":
    sys.exit(main())
