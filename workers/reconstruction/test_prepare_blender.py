"""Receipt/precision boundaries and optional genuine Blender export round trips."""
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import numpy as np
import trimesh

from prepare_blender import local_mesh, prepare
from surface_cloud import convert, sha256_file


class BlenderHandoffTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="blender handoff ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / "원본 mesh.ply"
        self.bundle = self.root / "sampling"
        self.output = self.root / "결과 handoff"
        self.blender = self.root / "blender.exe"
        self.blender.write_bytes(b"mock executable; never run")
        self.mesh = trimesh.Trimesh(
            vertices=[[.2, .3, .4], [2.2, .3, .4], [.2, 3.3, .4], [.2, .3, 4.4]],
            faces=[[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]],
            vertex_colors=[[255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255], [255, 255, 0, 255]],
            process=False)
        self.mesh.export(self.source)
        self.sample()

    def sample(self, scale=1, origin=None):
        return convert(self.source, self.bundle, count=100, seed=3, meters_per_unit=scale,
                       origin=origin or [0, 0, 0], axes="declared right-handed fixture axes",
                       source_kind="test-fixture", source_reference="analytic tetrahedron; not a captured building",
                       scale_reference="analytically defined metres")

    def run_prepare(self, **kwargs):
        options = dict(source_forward="Y", source_up="Z", frame_reference="analytic right-handed Z-up fixture")
        options.update(kwargs)
        return prepare(self.source, self.bundle / "receipt.json", self.output, self.blender, **options)

    def parent(self):
        return json.loads((self.bundle / "receipt.json").read_text(encoding="utf-8"))

    def test_local_mesh_preserves_faces_colors_and_source(self):
        original = self.source.read_bytes()
        payload, stats = local_mesh(self.source, self.parent())
        mesh = trimesh.load(io.BytesIO(payload), file_type="ply", process=False)
        np.testing.assert_array_equal(mesh.faces, self.mesh.faces)
        np.testing.assert_array_equal(mesh.visual.vertex_colors, self.mesh.visual.vertex_colors)
        np.testing.assert_allclose(mesh.vertices, self.mesh.vertices, atol=1e-6)
        self.assertEqual(stats["triangles"], 4)
        self.assertEqual(original, self.source.read_bytes())

    def test_global_double_coordinates_recenter_before_float32(self):
        origin = [10000000, 20000000, 30000000]
        offsets = np.array([[.125, .25, .375], [2.125, .25, .375], [.125, 3.25, .375]])
        # Double ASCII fixture retains fractional metres that a global float32 loses.
        header = "ply\nformat ascii 1.0\nelement vertex 3\nproperty double x\nproperty double y\nproperty double z\nelement face 1\nproperty list uchar int vertex_indices\nend_header\n"
        self.source.write_text(header + "".join(" ".join(map(str, row)) + "\n" for row in offsets + origin) + "3 0 1 2\n", encoding="ascii")
        self.bundle = self.root / "large-origin-sampling"
        parent = self.sample(scale=.1, origin=origin)
        payload, stats = local_mesh(self.source, parent)
        mesh = trimesh.load(io.BytesIO(payload), file_type="ply", process=False)
        np.testing.assert_allclose(mesh.vertices, offsets * .1, atol=1e-7)
        self.assertLess(stats["max_float32_error_m"], 1e-7)

    def test_source_hash_and_coordinate_matrix_must_match_receipt(self):
        parent = self.parent()
        parent["coordinates"]["source_to_local_m"][0][0] = 2
        with self.assertRaisesRegex(ValueError, "matrix"):
            local_mesh(self.source, parent)
        with self.source.open("ab") as stream:
            stream.write(b"changed")
        with self.assertRaisesRegex(ValueError, "source hash"):
            local_mesh(self.source, self.parent())

    def test_invalid_frame_ratio_and_output_never_start_blender(self):
        with patch("prepare_blender.subprocess.run") as run:
            for options in ({"source_up": "Y"}, {"source_up": "NEGATIVE_Y"}, {"source_forward": "BAD"},
                            {"frame_reference": " "}, {"decimate_ratio": 0}, {"decimate_ratio": 1.1},
                            {"decimate_ratio": float("nan")}, {"timeout_seconds": -1}):
                with self.subTest(options=options), self.assertRaises(ValueError):
                    self.run_prepare(**options)
            self.output.mkdir()
            sentinel = self.output / "keep.txt"
            sentinel.write_text("keep", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "already exists"):
                self.run_prepare()
            self.assertEqual(sentinel.read_text(encoding="utf-8"), "keep")
            run.assert_not_called()

    def test_failed_process_and_missing_report_have_no_success_receipt(self):
        with patch("prepare_blender.subprocess.run", return_value=subprocess.CompletedProcess([], 2)):
            with self.assertRaisesRegex(ValueError, "exited 2"):
                self.run_prepare()
        self.assertFalse((self.output / "receipt.json").exists())
        self.output = self.root / "missing-report"
        with patch("prepare_blender.subprocess.run", return_value=subprocess.CompletedProcess([], 0)):
            with self.assertRaises(OSError):
                self.run_prepare()
        self.assertFalse((self.output / "receipt.json").exists())

    def test_timeout_leaves_diagnostic_files_without_receipt(self):
        with patch("prepare_blender.subprocess.run", side_effect=subprocess.TimeoutExpired("blender", 1)):
            with self.assertRaises(subprocess.TimeoutExpired):
                self.run_prepare(timeout_seconds=1)
        self.assertTrue((self.output / "request.json").is_file())
        self.assertFalse((self.output / "receipt.json").exists())

    @unittest.skipUnless(os.environ.get("BLENDER_EXE"), "optional real Blender integration")
    def test_real_blender_axis_scale_color_and_fbx_roundtrip(self):
        self.blender = Path(os.environ["BLENDER_EXE"])
        original_hash = sha256_file(self.source)
        receipt = self.run_prepare(source_forward="NEGATIVE_Z", source_up="Y",
                                   frame_reference="analytic right-handed Y-up, -Z forward fixture")
        mesh = trimesh.load(self.output / "prepared.ply", process=False)
        np.testing.assert_allclose(mesh.bounds, [[.2, -4.4, .3], [2.2, -.4, 3.3]], atol=1e-5)
        expected_colors = np.unique(self.mesh.visual.vertex_colors, axis=0)
        np.testing.assert_allclose(np.unique(mesh.visual.vertex_colors, axis=0), expected_colors, atol=1)
        self.assertEqual(receipt["source"]["kind"], "test-fixture")
        self.assertEqual(receipt["blender"]["roundtrip"]["triangles"], 4)
        self.assertLess(receipt["blender"]["roundtrip_max_bounds_error_m"], .001)
        self.assertEqual(original_hash, sha256_file(self.source))

    @unittest.skipUnless(os.environ.get("BLENDER_EXE"), "optional real Blender decimation")
    def test_real_blender_decimation_reduces_surface_without_empty_output(self):
        self.blender = Path(os.environ["BLENDER_EXE"])
        # An asymmetric analytic surface tests the native modifier; never a game asset.
        mesh = trimesh.creation.icosphere(subdivisions=2)
        mesh.vertices *= [2, 3, 4]
        mesh.export(self.source)
        self.bundle = self.root / "dense-sampling"
        self.sample()
        receipt = self.run_prepare(decimate_ratio=.5)
        before = receipt["blender"]["before"]["triangles"]
        after = receipt["blender"]["after"]["triangles"]
        self.assertEqual(before, 320)
        self.assertGreater(after, 0)
        self.assertLess(after, before)
        self.assertEqual(after, receipt["blender"]["roundtrip"]["triangles"])


if __name__ == "__main__":
    unittest.main()
