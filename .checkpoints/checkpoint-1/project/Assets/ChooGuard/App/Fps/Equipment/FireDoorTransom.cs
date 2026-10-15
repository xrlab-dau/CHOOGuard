using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The fixed transom above a fire door frame, from the head of the frame up to the ceiling: a steel frame in the door's paint (two stiles, a rail on the head of the door frame and one
    /// under the ceiling, the same profile as the door frame) with a plaster infill recessed between the rails, so the fire door assembly closes the whole opening like the wall around it.
    /// Built for the height of the ceiling at the door (a door of 2.1 m in a 3.1-4.2 m ceiling), in the frame's own coordinates (x across the door, y up from the floor, z through it).
    /// Submesh 0 is the steel frame, submesh 1 the infill.
    /// </summary>
    internal static class FireDoorTransom
    {
        /// <summary>Width of the frame profile seen from the front (the door frame's stiles), and how deep the infill sits behind the front face of the frame.</summary>
        public const float Profile = .07f, InfillDepth = .08f;

        /// <summary>The transom mesh from the frame head (<see cref="FireDoorPoint.FrameHeight"/>) up to <paramref name="ceiling"/> metres above the floor; null when there is no room for one.</summary>
        public static Mesh Build(float ceiling)
        {
            float bottom = FireDoorPoint.FrameHeight;
            if (ceiling - bottom < 2.5f * Profile) return null;
            float x = FireDoorPoint.FrameWidth * .5f, z = FireDoorPoint.FrameDepth * .5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var frame = new List<int>();
            var infill = new List<int>();
            void Box(List<int> into, Vector3 min, Vector3 max, bool onlyFaces)
            {
                Quad(into, new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), Vector3.forward);
                Quad(into, new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.back);
                if (onlyFaces) return;
                Quad(into, new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.left);
                Quad(into, new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), Vector3.right);
                Quad(into, new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Vector3.up);
                Quad(into, new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), Vector3.down);
            }
            void Quad(List<int> into, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int first = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                for (int i = 0; i < 4; i++) normals.Add(normal);
                // 1 UV unit per metre: the plaster keeps its scale whatever the height.
                foreach (var p in new[] { a, b, c, d })
                    uvs.Add(Mathf.Abs(normal.z) > .5f ? new Vector2(p.x, p.y) : Mathf.Abs(normal.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.z));
                into.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            Box(frame, new Vector3(-x, bottom, -z), new Vector3(-x + Profile, ceiling, z), false);
            Box(frame, new Vector3(x - Profile, bottom, -z), new Vector3(x, ceiling, z), false);
            Box(frame, new Vector3(-x + Profile, bottom, -z), new Vector3(x - Profile, bottom + Profile, z), false);
            Box(frame, new Vector3(-x + Profile, ceiling - Profile, -z), new Vector3(x - Profile, ceiling, z), false);
            Box(infill, new Vector3(-x + Profile, bottom + Profile, -z + (FireDoorPoint.FrameDepth - InfillDepth) * .5f), new Vector3(x - Profile, ceiling - Profile, z - (FireDoorPoint.FrameDepth - InfillDepth) * .5f), true);
            var mesh = new Mesh { name = "FireDoorTransom" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(frame, 0);
            mesh.SetTriangles(infill, 1);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
