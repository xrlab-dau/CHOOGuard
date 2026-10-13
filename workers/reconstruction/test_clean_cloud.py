"""Cleanup failure boundaries, plus opt-in tests using a real CloudCompare binary."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import numpy as np
import trimesh

from clean_cloud import cleanup, read_cloud, verify_subset
from surface_cloud import convert, sha256_file


def write_cloud(path, xyz, colors=None, normals=None):
    cloud = trimesh.points.PointCloud(xyz, colors=colors)
    if normals is not None:
        cloud.vertex_normals = normals
    path.write_bytes(trimesh.exchange.ply.export_ply(cloud, vertex_normal=normals is not None))


class CleanupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="cloud cleanup ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bundle = self.root / "원본 raw bundle"
        self.output = self.root / "정리 clean bundle"
        self.exe = self.root / "CloudCompare.exe"
        self.exe.write_bytes(b"mock only: never executed")
        self.mesh = self.root / "triangle.ply"
        trimesh.Trimesh(vertices=[[0, 0, 0], [2, 0, 0], [0, 2, 1]], faces=[[0, 1, 2]],
                        vertex_colors=[[255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255]],
                        process=False).export(self.mesh)
        convert(self.mesh, self.bundle, count=300, seed=9, meters_per_unit=1, origin=[0, 0, 0],
                axes="test right-handed +Z up", source_kind="test-fixture",
                source_reference="analytic triangle; not a captured building", scale_reference="metre fixture")

    def run_cleanup(self, **kwargs):
        options = dict(tool_version="unit-test mock", neighbors=16, sigma=2, spacing_m=.02, timeout_seconds=30)
        options.update(kwargs)
        return cleanup(self.bundle, self.output, self.exe, **options)

    def write_stage(self, path, indices, *, shifted=False, recolored=False):
        cloud = trimesh.load(self.bundle / "surface_raw.ply", process=False)
        xyz = cloud.vertices[indices].copy()
        colors = cloud.colors[indices].copy()
        data = cloud.metadata["_ply_raw"]["vertex"]["data"]
        normals = np.column_stack([data[name][indices] for name in ("nx", "ny", "nz")])
        if shifted:
            xyz[0, 0] += .001
        if recolored:
            colors[0, 0] ^= 1
        write_cloud(path, xyz, colors, normals)

    def successful_process(self, command, **kwargs):
        self.write_stage(self.output / "surface_sor.ply", np.arange(200))
        self.write_stage(self.output / "surface_clean.ply", np.arange(0, 200, 2))
        return subprocess.CompletedProcess(command, 0)

    def test_counts_provenance_and_argument_boundaries(self):
        original = (self.bundle / "surface_raw.ply").read_bytes()
        with patch("clean_cloud.subprocess.run", side_effect=self.successful_process) as run:
            receipt = self.run_cleanup()
        self.assertEqual(receipt["status"], "cleanup_complete_review_required")
        self.assertEqual([s["points"] for s in receipt["stages"]], [200, 100])
        self.assertEqual(receipt["stages"][1]["removed_fraction"], .5)
        self.assertEqual(receipt["source"]["kind"], "test-fixture")
        self.assertEqual(receipt["coordinates"]["axis_conversion"],
                         "none; source handedness and axis directions preserved")
        self.assertEqual((self.output / "sampling_receipt.json").read_bytes(),
                         (self.bundle / "receipt.json").read_bytes())
        self.assertEqual((self.bundle / "surface_raw.ply").read_bytes(), original)
        argv = run.call_args.args[0]
        self.assertIn(str(self.bundle / "surface_raw.ply"), argv)
        self.assertNotIn("shell", run.call_args.kwargs)
        self.assertEqual(receipt["tool"]["executable_sha256"], sha256_file(self.exe))
        self.assertEqual(receipt["stages"][-1]["sha256"], sha256_file(self.output / "surface_clean.ply"))

    def test_mismatching_receipt_rejected_before_launch(self):
        with (self.bundle / "surface_raw.ply").open("ab") as stream:
            stream.write(b"modified")
        with patch("clean_cloud.subprocess.run") as run, self.assertRaisesRegex(ValueError, "receipt"):
            self.run_cleanup()
        run.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_existing_output_and_invalid_parameters_never_launch(self):
        with patch("clean_cloud.subprocess.run") as run:
            for options in ({"neighbors": 300}, {"neighbors": True}, {"neighbors": 1}, {"sigma": float("nan")},
                            {"sigma": 0}, {"spacing_m": -1}, {"timeout_seconds": float("inf")}, {"tool_version": " "}):
                with self.subTest(options=options), self.assertRaises(ValueError):
                    self.run_cleanup(**options)
            self.output.mkdir()
            sentinel = self.output / "keep.txt"
            sentinel.write_text("keep", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "already exists"):
                self.run_cleanup()
            self.assertEqual(sentinel.read_text(encoding="utf-8"), "keep")
            run.assert_not_called()

    def test_process_failure_or_timeout_never_gets_success_receipt(self):
        with patch("clean_cloud.subprocess.run", return_value=subprocess.CompletedProcess([], 3)):
            with self.assertRaisesRegex(ValueError, "exited 3"):
                self.run_cleanup()
        self.assertTrue((self.output / "request.json").exists())
        self.assertFalse((self.output / "receipt.json").exists())
        self.output = self.root / "timeout"
        with patch("clean_cloud.subprocess.run", side_effect=subprocess.TimeoutExpired("cc", 30)):
            with self.assertRaises(subprocess.TimeoutExpired):
                self.run_cleanup()
        self.assertFalse((self.output / "receipt.json").exists())

    def test_zero_exit_without_outputs_is_not_success(self):
        with patch("clean_cloud.subprocess.run", return_value=subprocess.CompletedProcess([], 0)):
            with self.assertRaises((ValueError, OSError)):
                self.run_cleanup()
        self.assertFalse((self.output / "receipt.json").exists())

    def test_input_change_during_process_is_not_success(self):
        def mutate(command, **kwargs):
            result = self.successful_process(command, **kwargs)
            with (self.bundle / "receipt.json").open("a") as stream:
                stream.write(" ")
            return result
        with patch("clean_cloud.subprocess.run", side_effect=mutate), self.assertRaisesRegex(ValueError, "changed"):
            self.run_cleanup()
        self.assertFalse((self.output / "receipt.json").exists())

    def test_movement_rgb_changes_and_duplicate_output_are_rejected(self):
        raw = read_cloud(self.bundle / "surface_raw.ply")
        path = self.root / "changed.ply"
        for options in ({"shifted": True}, {"recolored": True}):
            self.write_stage(path, np.arange(20), **options)
            with self.subTest(options=options), self.assertRaisesRegex(ValueError, "moved/added"):
                verify_subset(raw, read_cloud(path))
        self.write_stage(path, np.zeros(20, dtype=int))
        with self.assertRaisesRegex(ValueError, "duplicated"):
            verify_subset(raw, read_cloud(path))
        self.write_stage(path, np.arange(299, -1, -1))
        verify_subset(raw, read_cloud(path))  # Point order is not a coordinate change.

    def test_normals_colors_and_cloud_shape_are_explicit(self):
        path = self.root / "bad.ply"
        xyz = [[0, 0, 0], [1, 0, 0]]
        cases = [(None, None), (None, [[0, 0, 0]] * 2), (None, [[0, 0, float("nan")]] * 2),
                 ([[1, 2, 3, 254]] * 2, [[0, 0, 1]] * 2)]
        for colors, normals in cases:
            write_cloud(path, xyz, colors, normals)
            with self.assertRaises(ValueError):
                read_cloud(path)
        with self.assertRaisesRegex(ValueError, "without faces"):
            read_cloud(self.mesh)
        write_cloud(path, xyz, None, [[0, 0, 1]] * 2)
        self.assertFalse(read_cloud(path)["rgb"])

    @unittest.skipUnless(os.environ.get("CLOUDCOMPARE_EXE"), "optional real CloudCompare integration")
    def test_real_cloudcompare_colored_and_uncolored(self):
        self.exe = Path(os.environ["CLOUDCOMPARE_EXE"])
        original_hash = sha256_file(self.bundle / "surface_raw.ply")
        receipt = self.run_cleanup(tool_version=os.environ.get("CLOUDCOMPARE_VERSION", "local binary"))
        self.assertEqual(receipt["source"]["kind"], "test-fixture")
        self.assertEqual(original_hash, sha256_file(self.bundle / "surface_raw.ply"))
        self.assertGreater(receipt["stages"][-1]["points"], 0)
        self.assertTrue(receipt["stages"][-1]["rgb"])
        self.assertTrue((self.output / "cloudcompare.log").is_file())
        # A second genuine run must not fabricate colors for an uncolored cloud.
        self.bundle = self.root / "uncolored raw"
        self.output = self.root / "uncolored clean"
        trimesh.Trimesh(vertices=[[0, 0, 0], [2, 0, 0], [0, 2, 1]], faces=[[0, 1, 2]],
                        process=False).export(self.mesh)
        convert(self.mesh, self.bundle, count=1000, seed=10, meters_per_unit=1, origin=[0, 0, 0],
                axes="test right-handed +Z up", source_kind="test-fixture",
                source_reference="uncolored triangle", scale_reference="metre fixture")
        receipt = self.run_cleanup(tool_version=os.environ.get("CLOUDCOMPARE_VERSION", "local binary"))
        self.assertFalse(receipt["stages"][-1]["rgb"])


if __name__ == "__main__":
    unittest.main()
