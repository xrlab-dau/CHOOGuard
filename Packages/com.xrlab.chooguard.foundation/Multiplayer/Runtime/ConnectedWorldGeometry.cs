using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Foundation.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Immutable descriptors captured from actual authored colliders. No renderer bounds,
    /// NavMeshAgent motion, or region-isolated contact simulations.</summary>
    public sealed class ConnectedWorldGeometry : IDisposable
    {
        public const double GeometrySlackM = .0002;
        public sealed class Support
        {
            public string Id, RegionId, FrameId, PortalId;
            public BoxCollider Source;
            public double MinX, MaxX, MinZ, MaxZ, Intercept;
            public CrowdVector Gradient;
            public double Elevation(double x, double z) => Intercept + Gradient.X * x + Gradient.Y * z;
            public bool Contains(double x, double z, double slack = GeometrySlackM) => x >= MinX - slack && x <= MaxX + slack && z >= MinZ - slack && z <= MaxZ + slack;
        }
        public sealed class ColliderDescriptor
        {
            public string Id, SourcePath, FrameId, PortalId;
            public Collider Source;
            public Vector3[] Vertices;
            public CrowdVector[] Footprint;
            public double Bottom, Top, MinX, MaxX, MinZ, MaxZ;
            public bool Walkable, HeadroomOnly, InitiallyEnabled;
            public Matrix4x4 AuthoredTransform;
        }
        private sealed class Navigation : IDisposable
        {
            public int AgentType;
            public Vector3 QueryOffset;
            public NavMeshData Data;
            public NavMeshDataInstance Instance;
            public void Dispose() { Instance.Remove(); if (Data != null) UnityEngine.Object.DestroyImmediate(Data); }
        }
        public ConnectedWorldDefinition Definition { get; private set; }
        public string Signature { get; private set; }
        public IReadOnlyList<Support> Supports => supports;
        public IReadOnlyList<ColliderDescriptor> Colliders => colliders;
        private readonly List<Support> supports = new List<Support>();
        private readonly List<ColliderDescriptor> colliders = new List<ColliderDescriptor>();
        private readonly Dictionary<string, Navigation> navigation = new Dictionary<string, Navigation>();
        private ConnectedRegionView[] views;
        private SpatialFrame[] appliedFrames;
        private bool disposed;
        public static ConnectedWorldGeometry Capture(ConnectedWorldDefinition definition, ConnectedRegionView[] loadedViews)
        {
            definition.Validate();
            if (loadedViews == null || !loadedViews.Select(v => v.RegionId).OrderBy(x => x).SequenceEqual(definition.Regions.Select(r => r.Id).OrderBy(x => x)))
                throw new ArgumentException("Server geometry requires each of the 13 region scenes exactly once.");
            var result = new ConnectedWorldGeometry { Definition = definition, views = loadedViews.ToArray() };
            result.ApplyFrames(definition.Frames, new HashSet<string>());
            foreach (var view in loadedViews.OrderBy(v => v.RegionId, StringComparer.Ordinal))
            foreach (var collider in view.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger || WorldBodyPresentation.IsPresentation(collider)) continue;
                if (!(collider is BoxCollider) && !(collider is MeshCollider)) throw new ArgumentException("Unsupported authored collider: " + collider.name);
                var marker = collider.GetComponent<ConnectedWalkableSurface>();
                var barrier = collider.GetComponentInParent<ConnectedPortalBarrier>();
                var vertices = Vertices(collider);
                var descriptor = new ColliderDescriptor { Id = Path(collider.transform, view.transform) + "/collider" + Array.IndexOf(collider.GetComponents<Collider>(), collider),
                    FrameId = marker != null ? marker.FrameId : view.FrameId, PortalId = barrier != null ? barrier.PortalId : "", Source = collider,
                    Vertices = vertices, Footprint = Hull(vertices.Select(p => new CrowdVector(p.x, p.z))), Bottom = vertices.Min(p => p.y), Top = vertices.Max(p => p.y),
                    MinX = vertices.Min(p => p.x), MaxX = vertices.Max(p => p.x), MinZ = vertices.Min(p => p.z), MaxZ = vertices.Max(p => p.z),
                    Walkable = marker != null, HeadroomOnly = collider.name == "Ceiling" || collider.name == "Provisional_PassageCeiling", InitiallyEnabled = collider.enabled || barrier != null, AuthoredTransform = collider.transform.localToWorldMatrix };
                descriptor.SourcePath = descriptor.Id; descriptor.Id = "collider." + Digest(descriptor.Id).Substring(0, 32);
                result.colliders.Add(descriptor);
                if (marker == null) continue;
                if (!(collider is BoxCollider box) || string.IsNullOrEmpty(marker.SurfaceId)) throw new ArgumentException("Support requires an identified box upper face.");
                var normal = box.transform.up.normalized;
                if (normal.y < Mathf.Cos(GroundedWorldMotor.MaximumSlopeDegrees * Mathf.Deg2Rad)) throw new ArgumentException("Unsupported ground slope.");
                var top = box.transform.TransformPoint(box.center + Vector3.up * (box.size.y / 2));
                var face = new List<Vector3>();
                foreach (var x in new[] { -1, 1 }) foreach (var z in new[] { -1, 1 })
                    face.Add(box.transform.TransformPoint(box.center + new Vector3(x * box.size.x / 2, box.size.y / 2, z * box.size.z / 2)));
                var gradient = new CrowdVector(-normal.x / normal.y, -normal.z / normal.y);
                // Current authored rooms and ramps have axis-aligned horizontal projections. Reject future diagonal geometry instead of silently using an AABB.
                var minX = face.Min(p => p.x); var maxX = face.Max(p => p.x); var minZ = face.Min(p => p.z); var maxZ = face.Max(p => p.z);
                if (face.Any(p => Math.Min(Math.Abs(p.x - minX), Math.Abs(p.x - maxX)) > GeometrySlackM || Math.Min(Math.Abs(p.z - minZ), Math.Abs(p.z - maxZ)) > GeometrySlackM))
                    throw new ArgumentException("Support-window adapter requires axis-aligned authored upper faces.");
                result.supports.Add(new Support { Id = marker.SurfaceId, Source = box, RegionId = marker.RegionId, FrameId = marker.FrameId, PortalId = marker.PortalId,
                    MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ, Gradient = gradient, Intercept = top.y - gradient.X * top.x - gradient.Y * top.z });
            }
            if (result.supports.Select(s => s.Id).Distinct().Count() != result.supports.Count) throw new ArgumentException("Duplicate authored support IDs.");
            result.JoinNumericalSeams();
            result.Signature = result.SourceSignature();
            return result;
        }
        private void JoinNumericalSeams()
        {
            // Float transform errors must not create topology cracks. A join is permitted only for
            // physically coincident edges; the crowd keeps double coordinates and a common hinge.
            foreach (var a in supports) foreach (var b in supports)
            {
                if (a == b || a.FrameId != b.FrameId && a.FrameId != "world" && b.FrameId != "world") continue;
                if (Math.Abs(a.MaxX - b.MinX) < GeometrySlackM && Math.Min(a.MaxZ, b.MaxZ) > Math.Max(a.MinZ, b.MinZ))
                { var z = (Math.Max(a.MinZ, b.MinZ) + Math.Min(a.MaxZ, b.MaxZ)) / 2; var x = (a.MaxX + b.MinX) / 2;
                    if (Math.Abs(a.Elevation(x, z) - b.Elevation(x, z)) < GeometrySlackM) a.MaxX = b.MinX = x; }
                if (Math.Abs(a.MaxZ - b.MinZ) < GeometrySlackM && Math.Min(a.MaxX, b.MaxX) > Math.Max(a.MinX, b.MinX))
                { var x = (Math.Max(a.MinX, b.MinX) + Math.Min(a.MaxX, b.MaxX)) / 2; var z = (a.MaxZ + b.MinZ) / 2;
                    if (Math.Abs(a.Elevation(x, z) - b.Elevation(x, z)) < GeometrySlackM) a.MaxZ = b.MinZ = z; }
            }
        }
        public bool ValidateSourceGeometry(out string failure)
        {
            failure = "";
            foreach (var c in colliders)
            {
                if (c.Source == null) { failure = "Missing collider " + c.Id; return false; }
                var delta = Delta(c.FrameId, appliedFrames); var expectedTransform = Matrix4x4.Translate(new Vector3(delta.X, 0, delta.Z)) * c.AuthoredTransform;
                for (var k = 0; k < 16; k++) if (Math.Abs(c.Source.transform.localToWorldMatrix[k] - expectedTransform[k]) > .0003f)
                { failure = "Changed collider transform " + c.SourcePath; return false; }
                if (c.PortalId == "" && c.Source.enabled != c.InitiallyEnabled)
                { failure = "Changed static collider availability " + c.SourcePath; return false; }
                // Compare source-local vertices after checking the supplied rigid frame delta.
                var now = LocalVertices(c.Source); var expected = c.Vertices.Select(c.AuthoredTransform.inverse.MultiplyPoint3x4).ToArray();
                if (now.Length != expected.Length || now.Where((p, i) => Vector3.Distance(p, expected[i]) > .0003f).Any())
                { failure = "Changed collider geometry " + c.Id; return false; }
            }
            return true;
        }
        public void ApplyFrames(SpatialFrame[] frames, ISet<string> closed)
        {
            foreach (var view in views) view.ApplyFrame(Definition, frames.Single(f => f.FrameId == view.FrameId));
            foreach (var barrier in views.SelectMany(v => v.GetComponentsInChildren<ConnectedPortalBarrier>(true))) barrier.SetOpen(!closed.Contains(barrier.PortalId));
            appliedFrames = frames.Select(f => f.Copy()).ToArray();
            Physics.SyncTransforms();
        }
        internal void RestoreSurvivingSceneViews()
        {
            // Additive scene roots may be destroyed before the bootstrap's OnDestroy.
            // Normal simulation remains strict; teardown restores only surviving views.
            foreach (var view in views.Where(v => v != null))
            {
                view.ApplyFrame(Definition, Definition.Frame(view.FrameId));
                foreach (var barrier in view.GetComponentsInChildren<ConnectedPortalBarrier>(true)) barrier.SetOpen(true);
            }
            appliedFrames = Definition.Frames.Select(f => f.Copy()).ToArray();
            Physics.SyncTransforms();
        }
        internal Support FindSupport(double worldX, double worldZ, double nearY, SpatialFrame[] frames, CrowdVector direction, string preferred = null)
        {
            var candidates = supports.Where(s => { var d = Delta(s.FrameId, frames); return s.Source != null && s.Source.enabled && s.Contains(worldX - d.X, worldZ - d.Z, 1e-9) && Math.Abs(s.Elevation(worldX - d.X, worldZ - d.Z) - nearY) <= .34; }).ToArray();
            if (candidates.Length == 0) return null;
            if (direction.Length > 1e-10)
            {
                var probe = direction / direction.Length * .0005;
                var ahead = candidates.Where(s => { var d = Delta(s.FrameId, frames); return s.Contains(worldX + probe.X - d.X, worldZ + probe.Y - d.Z, 1e-8); }).ToArray();
                if (ahead.Length > 0) candidates = ahead;
            }
            return candidates.OrderByDescending(s => { var d = Delta(s.FrameId, frames); return s.Elevation(worldX - d.X, worldZ - d.Z); }).ThenBy(s => s.Id == preferred ? 0 : 1).ThenBy(s => s.Id, StringComparer.Ordinal).First();
        }
        internal Point3 Delta(string frameId, SpatialFrame[] frames)
        { if (frameId == "world") return new Point3(); var f = frames.Single(f => f.FrameId == frameId); var a = Definition.Frame(frameId); return new Point3(f.Origin.X - a.Origin.X, 0, f.Origin.Z - a.Origin.Z); }
        internal CrowdWall[] Walls(SpatialFrame[] frames, ISet<string> closed)
        {
            var result = new List<CrowdWall>();
            foreach (var c in colliders.Where(c => !c.Walkable && !c.HeadroomOnly && c.InitiallyEnabled && (c.PortalId == "" || closed.Contains(c.PortalId))))
            {
                var space = Space(c.FrameId, frames, closed); var delta = Delta(c.FrameId, frames);
                var points = c.Footprint.Select(p => ToContact(new CrowdVector(p.X + delta.X, p.Y + delta.Z), space, frames)).ToArray();
                var bottom = c.Bottom - (space == "world" ? 0 : frames.Single(f => f.FrameId == space).Origin.Y);
                for (var i = 0; i < points.Length; i++) if ((points[i] - points[(i + 1) % points.Length]).Length > 1e-7)
                    result.Add(new CrowdWall { Id = c.Id + "/edge" + i, ContactSpaceId = space, A = points[i], B = points[(i + 1) % points.Length],
                        HasVerticalBounds = true, BottomElevationM = bottom, TopElevationM = bottom + c.Top - c.Bottom });
            }
            // The moving car's doorway is closed in its own frame. Station-side bridge remains fixed.
            foreach (var p in Definition.Portals.Where(p => p.StaticBoarding))
            {
                var train = Definition.Region(Definition.Region(p.From).FrameId == "world" ? p.To : p.From);
                if (Space(train.FrameId, frames, closed) == "world") continue;
                var threshold = train.Id == p.From ? p.FromPoint : p.ToPoint; var delta = Delta(train.FrameId, frames);
                var axis = new CrowdVector(p.ToPoint.X - p.FromPoint.X, p.ToPoint.Z - p.FromPoint.Z); axis /= axis.Length;
                var side = new CrowdVector(-axis.Y, axis.X) * (p.ClearWidth / 2);
                var center = new CrowdVector(threshold.X + delta.X, threshold.Z + delta.Z);
                result.Add(new CrowdWall { Id = "car-door/" + p.Id, ContactSpaceId = train.FrameId, A = ToContact(center + side, train.FrameId, frames), B = ToContact(center - side, train.FrameId, frames),
                    HasVerticalBounds = true, BottomElevationM = threshold.Y - frames.Single(f => f.FrameId == train.FrameId).Origin.Y, TopElevationM = threshold.Y + p.ClearHeight - frames.Single(f => f.FrameId == train.FrameId).Origin.Y });
                // No bridge-to-void step while train is absent, even if an external caller omitted its closed flag.
                result.Add(new CrowdWall { Id = "station-door/" + p.Id, ContactSpaceId = "world", A = new CrowdVector(threshold.X, threshold.Z) + side, B = new CrowdVector(threshold.X, threshold.Z) - side,
                    HasVerticalBounds = true, BottomElevationM = threshold.Y, TopElevationM = threshold.Y + p.ClearHeight });
            }
            foreach (var w in result)
                if (w.A.X > w.B.X || w.A.X == w.B.X && w.A.Y > w.B.Y) { var a = w.A; w.A = w.B; w.B = a; }
            return result.ToArray();
        }
        internal string Space(string frameId, SpatialFrame[] frames, ISet<string> closed)
        {
            if (frameId == "world") return "world";
            var p = Definition.Portals.Single(p => p.StaticBoarding && (Definition.Region(p.From).FrameId == frameId || Definition.Region(p.To).FrameId == frameId));
            var delta = Delta(frameId, frames);
            return !closed.Contains(p.Id) && delta.DistanceSquared(new Point3()) <= .0025 ? "world" : frameId;
        }
        internal static CrowdVector ToContact(CrowdVector point, string space, SpatialFrame[] frames)
        {
            if (space == "world") return point;
            var f = frames.Single(f => f.FrameId == space); var a = f.YawDegrees * Math.PI / 180; var c = Math.Cos(a); var s = Math.Sin(a);
            var x = point.X - f.Origin.X; var z = point.Y - f.Origin.Z;
            return new CrowdVector(c * x - s * z, s * x + c * z);
        }
        internal static CrowdVector ToWorld(CrowdVector point, string space, SpatialFrame[] frames)
        {
            if (space == "world") return point;
            var f = frames.Single(f => f.FrameId == space); var a = f.YawDegrees * Math.PI / 180; var c = Math.Cos(a); var s = Math.Sin(a);
            return new CrowdVector(f.Origin.X + c * point.X + s * point.Y, f.Origin.Z - s * point.X + c * point.Y);
        }
        internal bool IsClear(double x, double y, double z, double radius, double height, SpatialFrame[] frames, ISet<string> closed)
        {
            foreach (var c in colliders.Where(c => !c.Walkable && !c.HeadroomOnly && c.InitiallyEnabled && (c.PortalId == "" || closed.Contains(c.PortalId))))
            {
                if (y + height <= c.Bottom + GeometrySlackM || y >= c.Top - GeometrySlackM) continue;
                var delta = Delta(c.FrameId, frames); var p = new CrowdVector(x - delta.X, z - delta.Z);
                if (p.X < c.MinX - radius || p.X > c.MaxX + radius || p.Y < c.MinZ - radius || p.Y > c.MaxZ + radius) continue;
                if (Inside(p, c.Footprint)) return false;
                for (var i = 0; i < c.Footprint.Length; i++) if (Distance(p, c.Footprint[i], c.Footprint[(i + 1) % c.Footprint.Length]) < radius - GeometrySlackM) return false;
            }
            return true;
        }
        internal PreparedClearance PrepareClearance(SpatialFrame[] frames,ISet<string> closed)
        {
            var selected=colliders.Where(c=>!c.Walkable&&!c.HeadroomOnly&&c.InitiallyEnabled&&(c.PortalId==""||closed.Contains(c.PortalId))).ToArray();
            var deltas=frames.ToDictionary(f=>f.FrameId,f=>Delta(f.FrameId,frames));
            return new PreparedClearance(selected,selected.Select(c=>deltas[c.FrameId]).ToArray());
        }
        /// <summary>One synchronous validation pass over the same immutable descriptors and supplied frames.
        /// Only repeated selection/translation lookup is cached; exact bounds/inside/edge tests and their order are unchanged.</summary>
        internal sealed class PreparedClearance
        {
            private readonly ColliderDescriptor[] selected;
            private readonly Point3[] deltas;
            internal PreparedClearance(ColliderDescriptor[] selected,Point3[] deltas){this.selected=selected;this.deltas=deltas;}
            internal bool IsClear(double x,double y,double z,double radius,double height)
            {
                for(var at=0;at<selected.Length;at++)
                {
                    var c=selected[at];
                    if(y+height<=c.Bottom+GeometrySlackM||y>=c.Top-GeometrySlackM)continue;
                    var delta=deltas[at];var p=new CrowdVector(x-delta.X,z-delta.Z);
                    if(p.X<c.MinX-radius||p.X>c.MaxX+radius||p.Y<c.MinZ-radius||p.Y>c.MaxZ+radius)continue;
                    if(Inside(p,c.Footprint))return false;
                    for(var i=0;i<c.Footprint.Length;i++)if(Distance(p,c.Footprint[i],c.Footprint[(i+1)%c.Footprint.Length])<radius-GeometrySlackM)return false;
                }
                return true;
            }
        }
        public bool TryFindStandingPoint(string regionId, Point3 near, double radius, double height, WorldBodySpawn[] occupied, out SpatialPose pose)
        {
            pose = null;
            for (var ring = 0; ring < 35; ring++)
            for (var x = -ring; x <= ring; x++) for (var z = -ring; z <= ring; z++)
            {
                if (ring > 0 && Math.Abs(x) != ring && Math.Abs(z) != ring) continue;
                var p = new Point3(near.X + (float)(x * (2 * radius + .08)), near.Y, near.Z + (float)(z * (2 * radius + .08)));
                var r = Definition.Region(regionId);
                if (!r.Contains(p) || r.Exclusions.Any(e => e.Contains(p, (float)radius)) ||
                    occupied.Any(b => Math.Abs(b.Pose.Position.Y - p.Y) < height && Math.Pow(b.Pose.Position.X - p.X, 2) + Math.Pow(b.Pose.Position.Z - p.Z, 2) < Math.Pow(b.RadiusM + radius + .02, 2))) continue;
                if (!IsClear(p.X, p.Y, p.Z, radius + .035, height, Definition.Frames, new HashSet<string>()) ||
                    !GroundedWorldMotor.TryGroundedStep(new Vector3(p.X, p.Y, p.Z), Vector3.zero, out _, (float)radius, (float)height)) continue;
                pose = Definition.Pose(regionId, p); return true;
            }
            return false;
        }
        internal bool TryPath(Point3 from, Point3 to, double radius, double height, out Point3[] corners)
        {
            corners = Array.Empty<Point3>();
            var key = radius.ToString("R", CultureInfo.InvariantCulture) + "/" + height.ToString("R", CultureInfo.InvariantCulture);
            if (!navigation.TryGetValue(key, out var nav))
            {
                var settings = NavMesh.GetSettingsByIndex(0); settings.agentRadius = (float)radius + .045f; settings.agentHeight = (float)height;
                settings.agentClimb = .06f; settings.agentSlope = GroundedWorldMotor.MaximumSlopeDegrees;
                settings.overrideVoxelSize = true; settings.voxelSize = .055f; settings.overrideTileSize = true; settings.tileSize = 256; settings.minRegionArea = .02f;
                var sources = new List<NavMeshBuildSource>(); var bounds = new Bounds(Vector3.zero, Vector3.one);
                foreach (var c in colliders.Where(c => c.InitiallyEnabled && c.PortalId == ""))
                {
                    foreach (var p in c.Vertices) bounds.Encapsulate(p);
                    if (c.Source is BoxCollider box) sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = c.AuthoredTransform * Matrix4x4.Translate(box.center), size = box.size, area = c.Walkable ? 0 : 1 });
                    else if (c.Source is MeshCollider mesh) sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, transform = c.AuthoredTransform, sourceObject = mesh.sharedMesh, area = 1 });
                }
                bounds.Expand(2);
                var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (data == null) return false;
                // Reuse an existing registered type without mutating NavMeshAreas.asset. Each
                // radius-specific data set has an isolated query elevation; physical geometry stays fixed.
                var queryOffset = Vector3.up * (1000 * (navigation.Count + 1));
                nav = new Navigation { AgentType = settings.agentTypeID, Data = data, QueryOffset = queryOffset, Instance = NavMesh.AddNavMeshData(data, queryOffset, Quaternion.identity) }; navigation.Add(key, nav);
            }
            var filter = new NavMeshQueryFilter { agentTypeID = nav.AgentType, areaMask = NavMesh.AllAreas }; var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(V(from) + nav.QueryOffset, out var start, .3f, filter) || !NavMesh.SamplePosition(V(to) + nav.QueryOffset, out var end, .3f, filter) ||
                !NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            corners = new[] { from }.Concat(path.corners.Skip(1).Select(p => new Point3(p.x, p.y - nav.QueryOffset.y, p.z))).Concat(new[] { to }).ToArray(); return true;
        }
        private string SourceSignature()
        {
            var text = new StringBuilder(Definition.ProfileId + "\n" + JsonUtility.ToJson(Definition));
            foreach (var c in colliders.OrderBy(c => c.Id, StringComparer.Ordinal))
            { text.Append('\n').Append(c.Id).Append('|').Append(c.SourcePath).Append('|').Append(c.FrameId).Append('|').Append(c.PortalId).Append('|').Append(c.Walkable);
                foreach (var p in c.Vertices) text.Append('|').Append(p.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(p.z.ToString("R", CultureInfo.InvariantCulture)); }
            using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        private static string Digest(string value)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        private static string Path(Transform t, Transform root) => t == root ? root.name : Path(t.parent, root) + "/" + t.name + "[" + t.GetSiblingIndex() + "]";
        private static Vector3[] Vertices(Collider c) => LocalVertices(c).Select(c.transform.TransformPoint).ToArray();
        private static Vector3[] LocalVertices(Collider c)
        {
            if (c is MeshCollider m) return m.sharedMesh.vertices;
            var b = (BoxCollider)c; var points = new List<Vector3>();
            foreach (var x in new[] { -1, 1 }) foreach (var y in new[] { -1, 1 }) foreach (var z in new[] { -1, 1 }) points.Add(b.center + Vector3.Scale(b.size / 2, new Vector3(x, y, z)));
            return points.ToArray();
        }
        private static CrowdVector[] Hull(IEnumerable<CrowdVector> input)
        {
            var p = input.OrderBy(v => v.X).ThenBy(v => v.Y).Distinct().ToArray(); var h = new List<CrowdVector>();
            foreach (var v in p) { while (h.Count >= 2 && CrowdVector.Cross(h[h.Count - 1] - h[h.Count - 2], v - h[h.Count - 1]) <= 1e-9) h.RemoveAt(h.Count - 1); h.Add(v); }
            var low = h.Count;
            for (var i = p.Length - 2; i >= 0; i--) { while (h.Count > low && CrowdVector.Cross(h[h.Count - 1] - h[h.Count - 2], p[i] - h[h.Count - 1]) <= 1e-9) h.RemoveAt(h.Count - 1); h.Add(p[i]); }
            if (h.Count > 1) h.RemoveAt(h.Count - 1); return h.ToArray();
        }
        private static bool Inside(CrowdVector p, CrowdVector[] polygon) => polygon.Length >= 3 && polygon.Select((v, i) => CrowdVector.Cross(polygon[(i + 1) % polygon.Length] - v, p - v)).All(v => v >= 0);
        private static double Distance(CrowdVector p, CrowdVector a, CrowdVector b) { var d = b - a; var t = Math.Max(0, Math.Min(1, CrowdVector.Dot(p - a, d) / d.LengthSquared)); return (p - (a + d * t)).Length; }
        private static Vector3 V(Point3 p) => new Vector3(p.X, p.Y, p.Z);
        public void Dispose() { if (disposed) return; foreach (var n in navigation.Values) n.Dispose(); navigation.Clear(); disposed = true; }
    }
}
