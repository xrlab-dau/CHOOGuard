"""Geometry/format contract tests on analytic fixtures, never building claims."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest

import numpy as np
import trimesh

from surface_cloud import convert, load_surface, sample_surface


def triangle():
    return trimesh.Trimesh(
        vertices=[[0, 0, 0], [2, 0, 0], [0, 2, 0]], faces=[[0, 1, 2]],
        vertex_colors=[[255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255]], process=False,
    )


class SurfaceTests(unittest.TestCase):
    def test_surface_interior_and_interpolated_vertex_colors(self):
        cloud, stats = sample_surface(triangle(), 1000, 7, 1.0, [0, 0, 0])
        xyz = cloud.vertices
        self.assertTrue(np.all(xyz[:, :2] >= 0))
        self.assertTrue(np.all(xyz[:, :2].sum(axis=1) <= 2.000001))
        np.testing.assert_array_equal(xyz[:, 2], 0)
        self.assertGreater(np.min(xyz[:, 0]), 0)
        expected = np.column_stack((1-xyz[:, 0]/2-xyz[:, 1]/2, xyz[:, 0]/2, xyz[:, 1]/2))*255
        np.testing.assert_allclose(cloud.colors[:, :3], np.rint(expected), atol=1)
        np.testing.assert_allclose(cloud.vertex_normals, np.tile([0, 0, 1], (1000, 1)))
        self.assertEqual(stats["colors"], "barycentric_vertex_rgba")

    def test_sampling_weights_are_area_not_vertex_count(self):
        mesh = trimesh.Trimesh(vertices=[[0,0,0],[2,0,0],[0,2,0],[10,0,0],[14,0,0],[10,4,0]],
                               faces=[[0,1,2],[3,4,5]], process=False)
        cloud, _ = sample_surface(mesh, 20000, 91, 1, [0,0,0])
        large_fraction = (cloud.vertices[:,0] > 5).mean()
        self.assertAlmostEqual(large_fraction, .8, delta=.015)

    def test_same_seed_reproduces_points_and_new_seed_changes_them(self):
        a, _ = sample_surface(triangle(), 200, 8, 1, [0,0,0])
        b, _ = sample_surface(triangle(), 200, 8, 1, [0,0,0])
        c, _ = sample_surface(triangle(), 200, 9, 1, [0,0,0])
        np.testing.assert_array_equal(a.vertices, b.vertices)
        self.assertFalse(np.array_equal(a.vertices, c.vertices))

    def test_scale_origin_and_winding_are_preserved(self):
        mesh = triangle()
        mesh.vertices += [1000000, 2000000, 3000000]
        mesh.faces = mesh.faces[:, ::-1]
        cloud, stats = sample_surface(mesh, 100, 5, .01, [1000000,2000000,3000000])
        self.assertLessEqual(cloud.vertices.max(), .0200001)
        np.testing.assert_allclose(cloud.vertex_normals, np.tile([0,0,-1], (100,1)))
        np.testing.assert_allclose(stats["source_mesh_bounds_m"], [[0,0,0],[.02,.02,0]])

    def test_degenerate_faces_are_counted_but_not_sampled(self):
        mesh=triangle()
        mesh.faces=np.vstack((mesh.faces, [0,0,1]))
        cloud, stats=sample_surface(mesh, 50, 1, 1, [0,0,0])
        self.assertEqual(len(cloud.vertices), 50)
        self.assertEqual(stats["ignored_zero_area_faces"], 1)
        mesh.faces=[[0,0,1]]
        with self.assertRaisesRegex(ValueError, "non-degenerate"):
            sample_surface(mesh, 5, 1, 1, [0,0,0])

    def test_no_color_input_does_not_fabricate_rgb(self):
        mesh=trimesh.Trimesh(vertices=triangle().vertices, faces=[[0,1,2]], process=False)
        cloud, stats=sample_surface(mesh, 5, 1, 1, [0,0,0])
        self.assertEqual(stats["colors"], "none")
        data=trimesh.exchange.ply.export_ply(cloud, vertex_normal=True)
        self.assertNotIn(b"property uchar red", data.split(b"end_header")[0])

    def test_face_color_input(self):
        mesh=trimesh.Trimesh(vertices=triangle().vertices, faces=[[0,1,2]], face_colors=[[8,16,32,255]], process=False)
        cloud, stats=sample_surface(mesh, 10, 1, 1, [0,0,0])
        np.testing.assert_array_equal(cloud.colors, np.tile([8,16,32,255], (10,1)))
        self.assertEqual(stats["colors"], "face_rgba")

    def test_invalid_parameters_and_float32_precision_rejected(self):
        for count, seed, scale, origin in [(0,1,1,[0,0,0]), (2,-1,1,[0,0,0]), (2,1,0,[0,0,0]),
            (2,1,float("nan"),[0,0,0]), (2,1,float("inf"),[0,0,0]), (2,1,1,[0,float("inf"),0])]:
            with self.subTest(count=count, seed=seed, scale=scale, origin=origin), self.assertRaises(ValueError):
                sample_surface(triangle(),count,seed,scale,origin)
        mesh=triangle()
        mesh.vertices += 1e8
        with self.assertRaisesRegex(ValueError, "precision"):
            sample_surface(mesh, 10, 1, 1, [0,0,0])

    def test_numerically_degenerate_normals_rejected(self):
        mesh=triangle()
        mesh.vertices *= 1e-20
        with self.assertRaisesRegex(ValueError, "normals"):
            sample_surface(mesh, 10, 1, 1, [0,0,0])


class BundleTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        self.source=self.root/"input.ply"
        triangle().export(self.source)
        self.options=dict(count=64, seed=42, meters_per_unit=.5, origin=[1,0,0], axes="right handed; +Z up",
                          source_kind="test-fixture", source_reference="analytic triangle", scale_reference="fixture definition")

    def run_convert(self, name="bundle", **changes):
        return convert(self.source, self.root/name, **(self.options | changes))

    def test_binary_ply_independent_decode_hash_and_transform(self):
        receipt=self.run_convert()
        raw=(self.root/"bundle/surface_raw.ply").read_bytes()
        header, body=raw.split(b"end_header\n", 1)
        self.assertIn(b"format binary_little_endian 1.0", header)
        self.assertIn(b"element vertex 64", header)
        self.assertNotIn(b"element face", header)
        # Independent struct decoder checks xyz/normals/RGBA and exact payload length.
        values=list(struct.iter_unpack("<ffffffBBBB", body))
        self.assertEqual(len(values),64)
        for x,y,z,nx,ny,nz,r,g,b,a in values:
            self.assertGreaterEqual(x,-.5)
            self.assertLessEqual(x+y,.500001)
            self.assertEqual((z,nx,ny,nz,a),(0,0,0,1,255))
            self.assertAlmostEqual(r+g+b,255,delta=1)
        self.assertEqual(receipt["output"]["sha256"],hashlib.sha256(raw).hexdigest())
        forward=np.array(receipt["coordinates"]["source_to_local_m"])
        inverse=np.array(receipt["coordinates"]["local_m_to_source"])
        np.testing.assert_allclose(forward @ inverse, np.eye(4))
        np.testing.assert_allclose(forward @ [1,2,0,1], [0,1,0,1])
        saved=json.loads((self.root/"bundle/receipt.json").read_text(encoding="utf-8"))
        self.assertEqual(saved,receipt)

    def test_existing_outputs_and_source_are_untouched(self):
        source_bytes=self.source.read_bytes()
        self.run_convert()
        before={p.name:p.read_bytes() for p in (self.root/"bundle").iterdir()}
        with self.assertRaisesRegex(ValueError,"already exists"):
            self.run_convert()
        self.assertEqual(before,{p.name:p.read_bytes() for p in (self.root/"bundle").iterdir()})
        self.assertEqual(source_bytes,self.source.read_bytes())

    def test_ascii_and_binary_inputs_agree(self):
        a=self.run_convert("binary")
        self.source.write_bytes(trimesh.exchange.ply.export_ply(triangle(),encoding="ascii"))
        b=self.run_convert("ascii")
        self.assertEqual(a["output"]["sha256"],b["output"]["sha256"])

    def test_point_cloud_and_gaussian_are_rejected(self):
        trimesh.points.PointCloud([[0,0,0],[1,0,0],[0,1,0]]).export(self.source)
        with self.assertRaisesRegex(ValueError,"No surface faces"):
            self.run_convert()
        self.source.write_text("ply\nformat ascii 1.0\nelement vertex 1\nproperty float x\nproperty float f_dc_0\nend_header\n0 0\n")
        with self.assertRaisesRegex(ValueError,"Gaussian"):
            self.run_convert()
        self.assertFalse((self.root/"bundle").exists())

    def test_nan_mesh_rejected_without_outputs(self):
        self.source.write_text("ply\nformat ascii 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\nelement face 1\nproperty list uchar int vertex_indices\nend_header\nnan 0 0\n1 0 0\n0 1 0\n3 0 1 2\n")
        with self.assertRaisesRegex(ValueError,"NaN"):
            self.run_convert()
        self.assertFalse((self.root/"bundle").exists())

    def test_truncated_and_out_of_range_meshes_rejected(self):
        raw=self.source.read_bytes()
        self.source.write_bytes(raw[:-12])
        with self.assertRaises((ValueError,IndexError)):
            self.run_convert()
        mesh=triangle()
        mesh.faces=[[0,1,99]]
        mesh.export(self.source)
        with self.assertRaises((ValueError,IndexError)):
            self.run_convert()

    def test_metadata_required_and_receipt_does_not_claim_building_accuracy(self):
        for key in ("axes","source_reference","scale_reference"):
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.run_convert(**{key:" "})
        result=self.run_convert()
        self.assertEqual(result["source"]["kind"],"test-fixture")
        self.assertIn("not validated", result["validation_scope"])

    def test_cli_rejects_missing_scale(self):
        script=Path(__file__).with_name("surface_cloud.py")
        result=subprocess.run([sys.executable,str(script),str(self.source),str(self.root/"bundle")],capture_output=True,text=True)
        self.assertEqual(result.returncode,2)
        self.assertIn("--meters-per-unit",result.stderr)
        self.assertFalse((self.root/"bundle").exists())


if __name__ == "__main__":
    unittest.main()
