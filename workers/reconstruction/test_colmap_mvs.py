"""Offline contracts; these fixtures do not stand in for a real COLMAP run."""
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import numpy as np
import trimesh

import colmap_mvs


def write_outputs(directory: Path, *, bad_normals: bool = False) -> None:
    cloud = trimesh.points.PointCloud([[0, 0, 0], [2, 0, 0], [0, 3, 0]],
                                     colors=[[255, 0, 0, 255]] * 3)
    cloud.vertex_normals = np.tile([0, 0, 0 if bad_normals else 1], (3, 1))
    (directory / "fused.ply").write_bytes(trimesh.exchange.ply.export_ply(cloud, vertex_normal=True))
    mesh = trimesh.Trimesh(vertices=cloud.vertices, faces=[[0, 1, 2]],
                           vertex_colors=cloud.colors, process=False)
    (directory / "meshed-poisson.ply").write_bytes(trimesh.exchange.ply.export_ply(mesh))


class ColmapMvsTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "공개 건물"
        self.images = self.root / "images"
        self.model = self.root / "sparse"
        self.output = self.root / "new run"
        self.images.mkdir(parents=True)
        self.model.mkdir()
        (self.images / "one.JPG").write_bytes(b"test image identity, not a photograph")
        for name in ("cameras", "images", "points3D"):
            (self.model / (name + ".txt")).write_text("fixture", encoding="utf-8")
        self.exe = self.root / "colmap.exe"
        self.exe.write_bytes(b"fake process boundary; never executed")
        self.calls = []

    def process(self, command, **kwargs):
        self.calls.append((command, kwargs))
        if command[1] == "poisson_mesher":
            write_outputs(Path(kwargs["cwd"]))
        return subprocess.CompletedProcess(command, 0)

    def run_pipeline(self, **overrides):
        options = dict(source_reference="analytic contract fixture; supplied sparse model")
        options.update(overrides)
        return colmap_mvs.reconstruct(self.images, self.model, self.output, self.exe, **options)

    def test_success_preserves_unknown_units_and_hashes_real_ply_records(self):
        before = colmap_mvs.inventory(self.images)
        with patch.object(colmap_mvs.subprocess, "run", side_effect=self.process):
            receipt = self.run_pipeline()
        self.assertEqual(receipt["coordinates"]["unit"], "unknown_model_units")
        self.assertIsNone(receipt["coordinates"]["meters_per_unit"])
        self.assertEqual(receipt["source"]["images"], before)
        self.assertEqual(colmap_mvs.inventory(self.images), before)
        self.assertEqual(receipt["artifacts"][0]["geometry"]["points"], 3)
        self.assertEqual(receipt["artifacts"][1]["geometry"]["triangles"], 1)
        self.assertEqual(receipt["artifacts"][0]["geometry"]["bounds_model_units"], [[0, 0, 0], [2, 3, 0]])
        self.assertEqual(receipt["artifacts"][0]["sha256"], colmap_mvs.sha256_file(self.output / "fused.ply"))
        for argv, kwargs in self.calls:
            self.assertEqual(kwargs["cwd"], self.output)
            self.assertEqual(kwargs["timeout"], 3600)
            self.assertNotIn("shell", kwargs)
            if argv[1] == "patch_match_stereo":
                self.assertEqual(argv[argv.index("--PatchMatchStereo.geom_consistency") + 1], "1")
            if argv[1] == "stereo_fusion":
                self.assertEqual(argv[argv.index("--input_type") + 1], "geometric")

    def test_failure_and_timeout_never_write_success_receipt(self):
        for behavior in (subprocess.CompletedProcess([], 7), subprocess.TimeoutExpired("colmap", 1)):
            with self.subTest(behavior=type(behavior).__name__):
                self.output = self.root / ("failure " + type(behavior).__name__)
                def fail(command, **kwargs):
                    if command[1] == "patch_match_stereo":
                        if isinstance(behavior, Exception):
                            raise behavior
                        return behavior
                    return self.process(command, **kwargs)
                with patch.object(colmap_mvs.subprocess, "run", side_effect=fail):
                    with self.assertRaises((ValueError, subprocess.TimeoutExpired)):
                        self.run_pipeline()
                self.assertTrue((self.output / "request.json").is_file())
                self.assertTrue((self.output / "patch_match.log").is_file())
                self.assertFalse((self.output / "receipt.json").exists())
                self.assertFalse((self.output / "fused.ply").exists())

    def test_changed_input_or_executable_invalidates_completed_processes(self):
        for path in (self.images / "one.JPG", self.model / "cameras.txt", self.exe):
            with self.subTest(path=path.name):
                self.output = self.root / ("mutated " + path.name)
                def mutate(command, **kwargs):
                    result = self.process(command, **kwargs)
                    if command[1] == "poisson_mesher":
                        path.write_bytes(b"changed during processing")
                    return result
                with patch.object(colmap_mvs.subprocess, "run", side_effect=mutate):
                    with self.assertRaisesRegex(ValueError, "changed during reconstruction"):
                        self.run_pipeline()
                self.assertFalse((self.output / "receipt.json").exists())

    def test_existing_output_and_nested_output_are_rejected_before_execution(self):
        self.output.mkdir()
        sentinel = self.output / "keep.txt"
        sentinel.write_text("original", encoding="utf-8")
        with patch.object(colmap_mvs.subprocess, "run") as process:
            with self.assertRaisesRegex(ValueError, "already exists"):
                self.run_pipeline()
            self.output = self.images / "nested"
            with self.assertRaisesRegex(ValueError, "outside input"):
                self.run_pipeline()
            process.assert_not_called()
        self.assertEqual(sentinel.read_text(encoding="utf-8"), "original")
        self.assertFalse(self.output.exists())

    def test_invalid_parameters_and_incomplete_model_do_not_execute(self):
        for options in ({"cache_gb": float("nan")}, {"timeout_seconds": -1},
                        {"iterations": 0}, {"threads": True}, {"gpu_index": -1},
                        {"source_reference": " "}):
            with self.subTest(options=options), patch.object(colmap_mvs.subprocess, "run") as process:
                with self.assertRaises(ValueError):
                    self.run_pipeline(**options)
                process.assert_not_called()
                self.assertFalse(self.output.exists())
        (self.model / "points3D.txt").unlink()
        with patch.object(colmap_mvs.subprocess, "run") as process:
            with self.assertRaisesRegex(ValueError, "complete COLMAP"):
                self.run_pipeline()
            process.assert_not_called()

    def test_successful_exit_without_geometry_is_not_success(self):
        with patch.object(colmap_mvs.subprocess, "run", return_value=subprocess.CompletedProcess([], 0)):
            with self.assertRaises((ValueError, OSError)):
                self.run_pipeline()
        self.assertFalse((self.output / "receipt.json").exists())

    def test_invalid_normals_and_mesh_in_place_of_cloud_are_rejected(self):
        self.output.mkdir()
        write_outputs(self.output, bad_normals=True)
        with self.assertRaisesRegex(ValueError, "unit vectors"):
            colmap_mvs.inspect_outputs(self.output)
        (self.output / "fused.ply").write_bytes((self.output / "meshed-poisson.ply").read_bytes())
        with self.assertRaisesRegex(ValueError, "point cloud"):
            colmap_mvs.inspect_outputs(self.output)


if __name__ == "__main__":
    unittest.main()
