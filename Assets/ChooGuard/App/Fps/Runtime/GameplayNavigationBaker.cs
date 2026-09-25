using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DotRecast.Core;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    public static class GameplayNavigationBaker
    {
        internal const float CellHeight = .05f;
        private const string Profile = "DotRecast-2026.3.1|cs=.2|ch=.05|slope=40|height=1.7|radius=.3|climb=.15|watershed|region=2,4|edge=12,1.1|nvp=6|detail=6,1|filters=all";

        // Explicit real mesh selection is required. This never invents floor/portal endpoints or changes a scene.
        // Call once after geometry is ready, not from an NPC update loop. Frame=null bakes world coordinates.
        public static byte[] BakeGeometry(Transform frame, IReadOnlyList<MeshFilter> geometry, out string digest)
        {
            if (geometry == null || geometry.Count == 0) throw new ArgumentException("Actual geometry is required", nameof(geometry));
            if (frame != null && ((frame.lossyScale - Vector3.one).sqrMagnitude > .000001f || Vector3.Dot(frame.up, Vector3.up) < .99999f))
                throw new ArgumentException("Navigation frame requires unit scale and y-up", nameof(frame));
            var vertices = new List<float>();
            var triangles = new List<int>();
            var points = new List<Vector3>();
            var indices = new List<int>();
            var seen = new HashSet<MeshFilter>();
            for (var m = 0; m < geometry.Count; m++)
            {
                var filter = geometry[m];
                if (filter == null || filter.sharedMesh == null || !seen.Add(filter))
                    throw new ArgumentException("Missing or duplicate geometry mesh", nameof(geometry));
                var mesh = filter.sharedMesh;
                if (!mesh.isReadable) throw new InvalidOperationException("Geometry mesh must be readable for navigation baking: " + mesh.name);
                var matrix = (frame == null ? Matrix4x4.identity : frame.worldToLocalMatrix) * filter.transform.localToWorldMatrix;
                var offset = vertices.Count / 3;
                mesh.GetVertices(points);
                foreach (var vertex in points)
                {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    vertices.Add(point.x); vertices.Add(point.y); vertices.Add(point.z);
                }
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    mesh.GetTriangles(indices, submesh);
                    foreach (var index in indices) triangles.Add(offset + index);
                }
            }
            return BakeTriangles(vertices, triangles, out digest);
        }

        // Also used by the existing editor selector, which deliberately includes only the walkable slab submesh.
        public static byte[] BakeTriangles(List<float> vertices, List<int> triangles, out string digest)
        {
            if (vertices == null || triangles == null || vertices.Count < 9 || vertices.Count % 3 != 0 ||
                triangles.Count < 3 || triangles.Count % 3 != 0) throw new InvalidDataException("No valid geometry triangles to bake");
            foreach (var coordinate in vertices)
                if (!DetourNavigationSurface.Finite(coordinate)) throw new InvalidDataException("Geometry contains non-finite coordinates");
            foreach (var index in triangles)
                if (index < 0 || index >= vertices.Count / 3) throw new InvalidDataException("Geometry triangle index is out of range");
            digest = GeometryHash(vertices, triangles);
            var geometry = new RcSampleInputGeomProvider(vertices, triangles);
            var config = new RcConfig(RcPartition.WATERSHED, .2f, CellHeight, 40, 1.7f, .3f, .15f,
                2, 4, 12, 1.1f, 6, 6, 1, true, true, true, new RcAreaModification(1), true);
            var result = new RcBuilder().Build(geometry,
                new RcBuilderConfig(config, geometry.GetMeshBoundsMin(), geometry.GetMeshBoundsMax()), false);
            var mesh = result.Mesh;
            var detail = result.MeshDetail;
            if (mesh == null || mesh.npolys == 0) throw new InvalidDataException("No walkable polygons in supplied geometry");
            for (var i = 0; i < mesh.npolys; i++) mesh.flags[i] = 1;
            var options = new DtNavMeshCreateParams
            {
                verts = mesh.verts, vertCount = mesh.nverts, polys = mesh.polys, polyAreas = mesh.areas,
                polyFlags = mesh.flags, polyCount = mesh.npolys, nvp = mesh.nvp,
                detailMeshes = detail.meshes, detailVerts = detail.verts, detailVertsCount = detail.nverts,
                detailTris = detail.tris, detailTriCount = detail.ntris,
                walkableHeight = 1.7f, walkableRadius = .3f, walkableClimb = .15f,
                bmin = mesh.bmin, bmax = mesh.bmax, cs = .2f, ch = CellHeight, buildBvTree = true
            };
            var data = DtNavMeshBuilder.CreateNavMeshData(options);
            if (data == null) throw new InvalidDataException("Empty navigation bake");
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    new DtMeshDataWriter().Write(writer, data, RcByteOrder.LITTLE_ENDIAN, false);
                return stream.ToArray();
            }
        }

        private static string GeometryHash(List<float> vertices, List<int> triangles)
        {
            using (var hash = SHA256.Create())
            using (var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Profile);
                    writer.Write(vertices.Count);
                    foreach (var value in vertices) writer.Write(value);
                    writer.Write(triangles.Count);
                    foreach (var value in triangles) writer.Write(value);
                }
                stream.FlushFinalBlock();
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
