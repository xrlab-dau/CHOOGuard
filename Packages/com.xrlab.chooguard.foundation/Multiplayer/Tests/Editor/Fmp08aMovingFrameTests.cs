using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer.Editor;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-08a: one authored transform table carries the rail alignment, the moving carriage frame, the
    /// stationary boarding bridge and the platform frame, so movement, boarding/alighting and the report
    /// pose stay continuous. Boarding and alighting are both pinned: the station-to-carriage walk runs
    /// SimulatedSpatialWorld.TryLocate's station arm, and the carriage-to-station walk runs its train arm
    /// (the arm opened by the previous-carriage-region guard, whose rebind decision is located by content
    /// in CarriageToPlatformAlightingStepsThroughTheStationBridgeOnce rather than by line number), so
    /// neither direction stands in for the other. The three
    /// forbidden outcomes are pinned as failures here: a position jump at a frame boundary,
    /// support-surface confusion while boarding/alighting, and a stored report whose observation-time
    /// carriage pose is replaced by the current one.
    /// <para>
    /// Every continuity claim is a numbered tolerance, tabulated in
    /// docs/evidence/work-items/FMP-08a/result.json under continuityToleranceTable and asserted under
    /// the same identifier here. Scene and geometry provenance is labelled, not assumed:
    /// SceneAndGeometryProvenanceIsLabelledByTheOwnerEditorBuilder binds every loaded region view and
    /// every captured support to a scene file the owner Editor builder ConnectedWorldSceneBuilder
    /// wrote, and binds the report snapshot to its own instance in
    /// StoredReportKeepsItsObservationTimeCarriagePose.
    /// </para>
    /// </summary>
    public sealed class Fmp08aMovingFrameTests
    {
        // The authored synthetic table in foundation/world/connected-world-profile.json.
        private const string WorldFrame = "world";
        private const string CarFrame = "train-mainline";
        private const string MetroFrame = "train-metro";
        private const string CarRegion = "rolling_stock_mainline";
        private const string PlatformRegion = "rail_platforms_mainline";
        private const string BoardingPortalId = "rolling_stock_mainline--rail_platforms_mainline";
        private const string CarSupportId = "floor.rolling_stock_mainline";
        private const string PlatformSupportId = "floor.rail_platforms_mainline";
        private const string BridgeSupportId = "passage.rolling_stock_mainline--rail_platforms_mainline";
        // The authored boarding seam: the carriage doorway sits at Z = 12.5 and the bridge body at 5.0 -> 12.5.
        private const double SeamZ = 12.5;
        // The authored boarding portal spans ToPoint.Z = 5.0 (station side) to FromPoint.Z = 12.5 (carriage
        // side). The platform floor ends and the fixed bridge begins at the station edge, which is not the
        // carriage doorway: the two handovers are separate authored Z planes.
        private const double BridgeStationEdgeZ = 5.0;
        // SimulatedSpatialWorld.BoardingClearanceM: the footprint must clear the bridge by this much before
        // a station pose becomes a carriage pose.
        private const double BoardingClearanceM = .035;
        // Every body in this fixture is this radius, so both doorway rebinding planes below are radius-bound.
        private const double BodyRadiusM = .3;
        // The authored boarding portal is one level plane at Y = 2 (FromPoint.Y = ToPoint.Y = 2); the carriage
        // floor, the fixed bridge and the platform floor are all authored on it. The elevation is asserted
        // against this authored value, not only against the other decks.
        private const double AuthoredSeamY = 2;
        // Boarding (station -> carriage): TryLocate hands the pose to the carriage only once the whole
        // footprint has cleared the doorway by radius + clearance.
        private const double BoardingRebindZ = SeamZ + BodyRadiusM + BoardingClearanceM;
        // Alighting (carriage -> station): the mirror plane, SeamZ - radius - clearance. The two planes are
        // deliberately different thresholds; treating one as the other is exactly the support/frame confusion
        // this fixture exists to pin.
        private const double AlightingRebindZ = SeamZ - BodyRadiusM - BoardingClearanceM;
        private const string MetroRegion = "rolling_stock_metro";
        private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private string folder;
        private string[] paths;
        private SceneSetup[] previous;
        private ConnectedWorldDefinition world;
        private ConnectedWorldGeometry geometry;

        [OneTimeSetUp] public void Build()
        {
            previous = EditorSceneManager.GetSceneManagerSetup();
            // A fixture that silently skips reports green while nothing ran. The dirty-scene guard must
            // therefore fail loudly: a run in which none of this fixture's tests execute is not a pass.
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                Assert.Fail("The fixture refuses to run over unsaved scene edits; a skipped fixture must never be reported as a pass.");
            folder = "Assets/CHOOguardGenerated/WorldMotionFrameTest_" + Guid.NewGuid().ToString("N");
            paths = ConnectedWorldSceneBuilder.Build(folder);
            foreach (var path in paths.Skip(1)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath)); world.Validate();
            Physics.SyncTransforms();
            geometry = ConnectedWorldGeometry.Capture(world, UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None));
        }

        [OneTimeTearDown] public void Clear()
        {
            geometry?.Dispose();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (previous != null && previous.Any(s => s.isLoaded && s.isActive) && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }

        private WorldBodySpawn Spawn(string id, string region, Point3 point, double radius = BodyRadiusM, double speed = 1.5) =>
            new WorldBodySpawn { BodyId = id, Pose = world.Pose(region, point), RadiusM = radius, HeightM = 1.8, PreferredSpeedMS = speed };
        private ConnectedWorldMotionAdapter Adapter(params WorldBodySpawn[] bodies) => new ConnectedWorldMotionAdapter(geometry, bodies, 31);
        private static double Flat(Point3 a, Point3 b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
        private string SupportOf(ConnectedWorldMotionAdapter adapter, string bodyId) =>
            adapter.ExportCrowdSnapshot().Agents.Single(a => a.Id == bodyId).SurfaceId;
        private string SpaceOf(ConnectedWorldMotionAdapter adapter, string bodyId) =>
            adapter.ExportCrowdSnapshot().Agents.Single(a => a.Id == bodyId).ContactSpaceId;
        private static SpatialFrame[] Target(ConnectedWorldDefinition world, string frameId, float x, float y, float z)
        {
            var frames = world.Frames.Select(f => f.Copy()).ToArray();
            frames.Single(f => f.FrameId == frameId).Origin = new Point3(x, y, z);
            return frames;
        }
        private static string ProjectPath(string relative)
        {
            var direct = Path.GetFullPath(relative);
            return File.Exists(direct) ? direct : Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        }

        [Test]
        public void AuthoredRailAlignmentIsOneSyntheticTableAndTheBuilderRegeneratesIt()
        {
            Assert.That(paths.Length, Is.EqualTo(14), "The owner builder writes the bootstrap plus one scene per region; no part of the fixture is authored by hand.");
            var source = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
            source.Validate();
            Assert.That(File.ReadAllText(folder + "/connected-world-profile.json"), Is.EqualTo(JsonUtility.ToJson(source, true) + "\n"),
                "The generated profile is the canonical serialization of foundation/world/connected-world-profile.json, so the geometry under test is the authored table and no generated file was edited afterwards.");
            Assert.That(world.Classification, Is.EqualTo("synthetic_design_not_facility_acceptance"));
            Assert.That(world.Regions.All(r => r.GeometryStatus.StartsWith("synthetic_design", StringComparison.Ordinal)), Is.True,
                "Every authored region declares a synthetic-design geometry status; no region geometry is claimed as a surveyed facility measurement.");

            var root = world.Frame(WorldFrame); var mainline = world.Frame(CarFrame); var metro = world.Frame(MetroFrame);
            Assert.That(root.Origin.DistanceSquared(new Point3(0, 0, 0)), Is.Zero); Assert.That(root.YawDegrees, Is.Zero);
            Assert.That(mainline.Origin.DistanceSquared(new Point3(-50, 2, 15)), Is.Zero);
            Assert.That(mainline.YawDegrees, Is.Zero, "The mainline carriage is level along +Z; its frame carries no yaw.");
            Assert.That(metro.Origin.DistanceSquared(new Point3(300, -8, 15)), Is.Zero);
            Assert.That(metro.YawDegrees, Is.EqualTo(180), "The metro carriage keeps its authored 180 degree base yaw.");
            Assert.That(world.Region(CarRegion).FrameId, Is.EqualTo(CarFrame));
            Assert.That(world.Region(PlatformRegion).FrameId, Is.EqualTo(WorldFrame));

            using var adapter = Adapter(Spawn("passenger", CarRegion, world.Region(CarRegion).Hub, BodyRadiusM));
            var applied = adapter.Capture().Frames;
            Assert.That(applied.Select(f => f.FrameId).OrderBy(x => x, StringComparer.Ordinal),
                Is.EqualTo(world.Frames.Select(f => f.FrameId).OrderBy(x => x, StringComparer.Ordinal)));
            foreach (var frame in world.Frames)
            {
                var live = applied.Single(f => f.FrameId == frame.FrameId);
                Assert.That(live.Origin.DistanceSquared(frame.Origin), Is.Zero, frame.FrameId + " must start on its authored origin.");
                Assert.That(live.YawDegrees, Is.EqualTo(frame.YawDegrees), frame.FrameId + " must start on its authored yaw.");
            }

            // Before/after alignment: only the carriage origin moves, and the body keeps exactly one transform.
            var target = Target(world, CarFrame, -37.5f, 2, 15);
            Assert.That(adapter.TryApplyFrames(target, Array.Empty<string>(), out var failure), Is.True, failure);
            var moved = adapter.Capture().Frames;
            Assert.That(moved.Single(f => f.FrameId == CarFrame).Origin.DistanceSquared(new Point3(-37.5f, 2, 15)), Is.Zero);
            Assert.That(moved.Single(f => f.FrameId == CarFrame).YawDegrees, Is.Zero);
            Assert.That(moved.Single(f => f.FrameId == CarFrame).Origin.Y, Is.EqualTo(world.Frame(CarFrame).Origin.Y), "Alignment never changes the level height.");
            foreach (var frame in moved.Where(f => f.FrameId != CarFrame))
                Assert.That(frame.Origin.DistanceSquared(world.Frame(frame.FrameId).Origin), Is.Zero, frame.FrameId + " must stay on its authored origin.");
            var pose = adapter.Pose("passenger");
            Assert.That(pose.FrameId, Is.EqualTo(CarFrame));
            Assert.That(pose.LocalPosition.DistanceSquared(new Point3()), Is.LessThan(1e-9), "The carriage-local pose is unchanged by alignment.");
            Assert.That(pose.Position.DistanceSquared(moved.Single(f => f.FrameId == CarFrame).ToWorld(pose.LocalPosition)), Is.LessThan(1e-9),
                "World pose = frame.ToWorld(local pose) under the same authored transform for geometry, movement and reporting.");
            Assert.That(pose.Position.X, Is.EqualTo(-37.5f).Within(1e-4));
        }

        /// <summary>
        /// The metadata labels that make "the owner Editor builder generated this geometry" a checked
        /// claim rather than an assertion in prose. Six labels are read back off the artefact itself:
        /// the builder's own public constants, the isolation of the fixture root from the builder's
        /// shared output root (enforced by the builder's own guard), the builder's scene-file naming
        /// loop over the authored table, the runtime ConnectedRegionView labels inside the loaded
        /// scenes, the scene file every captured support collider actually lives in, and the
        /// synthetic-design label on the generated profile. Nothing here can be satisfied by a
        /// hand-authored scene dropped into the fixture folder: the colliders would then live in a
        /// scene path the builder's loop never produced, and (4) and (5) fail.
        /// </summary>
        [Test]
        public void SceneAndGeometryProvenanceIsLabelledByTheOwnerEditorBuilder()
        {
            // (1) The builder's own public constants are the identity this fixture was generated under.
            Assert.That(ConnectedWorldSceneBuilder.DefinitionPath, Is.EqualTo("foundation/world/connected-world-profile.json"),
                "The authored table is read from the one path the owner builder declares.");
            Assert.That(ConnectedWorldSceneBuilder.Root, Is.EqualTo("Assets/CHOOguardGenerated/ConnectedWorld"));
            Assert.That(ConnectedWorldSceneBuilder.BootstrapPath, Is.EqualTo(ConnectedWorldSceneBuilder.Root + "/ConnectedWorld.unity"));

            // (2) Isolation label: the fixture never wrote into the builder's shared output root, so every
            // scene under test is a fresh builder product rather than a checked-in or hand-edited artefact.
            Assert.That(folder.StartsWith("Assets/CHOOguardGenerated/", StringComparison.Ordinal), Is.True,
                "The fixture root sits in the owner builder's own output tree.");
            Assert.That(folder, Is.Not.EqualTo(ConnectedWorldSceneBuilder.Root));
            Assert.That(folder.StartsWith(ConnectedWorldSceneBuilder.Root + "/", StringComparison.Ordinal), Is.False,
                "The fixture writes outside the builder's shared output root.");
            // The builder enforces that isolation itself, so "the builder produced this folder" is a claim
            // the builder's own code refuses to let a custom geometry source satisfy anywhere else.
            var authoredTable = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
            Assert.That(() => { ConnectedWorldSceneBuilder.Build(ConnectedWorldSceneBuilder.Root, authoredTable); }, Throws.ArgumentException,
                "ConnectedWorldSceneBuilder.Build refuses a custom geometry source inside its shared output root.");

            // (3) Scene-file-name labels: the bootstrap plus one scene per authored region, named by the
            // builder's own loop over the authored table, in the authored region order.
            Assert.That(paths.Length, Is.EqualTo(world.Regions.Length + 1));
            Assert.That(paths[0], Is.EqualTo(folder + "/ConnectedWorld.unity"));
            for (var i = 0; i < world.Regions.Length; i++)
                Assert.That(paths[i + 1], Is.EqualTo(folder + "/" + world.Regions[i].SceneName + ".unity"),
                    "Region " + i + " (" + world.Regions[i].Id + ") must carry the builder's own scene file name for that authored region.");
            Assert.That(paths.Distinct().Count(), Is.EqualTo(paths.Length), "One scene file per authored region, no duplicates.");

            // (4) Runtime labels: every region view found in the loaded scenes lives in a builder-written
            // scene file and agrees with the authored region it is labelled with.
            var loaded = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None);
            Assert.That(loaded.Select(v => v.RegionId).OrderBy(x => x, StringComparer.Ordinal),
                Is.EqualTo(world.Regions.Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal)),
                "The loaded region views are exactly the authored regions, one builder scene each.");
            foreach (var view in loaded)
            {
                Assert.That(paths, Does.Contain(view.gameObject.scene.path),
                    view.RegionId + " must live in a scene file the owner builder wrote, not in a handwritten scene or a prefab asset.");
                Assert.That(Path.GetFileNameWithoutExtension(view.gameObject.scene.path), Is.EqualTo(world.Region(view.RegionId).SceneName));
                Assert.That(view.FrameId, Is.EqualTo(world.Region(view.RegionId).FrameId));
            }

            // (5) Geometry label: every support under test is a collider inside one of those scenes, and
            // its identity is the ConnectedWalkableSurface marker the builder itself attached to it.
            Assert.That(geometry.Supports.Count, Is.GreaterThanOrEqualTo(world.Regions.Length));
            foreach (var support in geometry.Supports)
            {
                Assert.That(paths, Does.Contain(support.Source.gameObject.scene.path),
                    "Support " + support.Id + " must be captured from an owner-builder scene, not from a prefab asset.");
                var marker = support.Source.GetComponent<ConnectedWalkableSurface>();
                Assert.That(marker, Is.Not.Null, "Support " + support.Id + " must carry the owner builder's walkable-surface marker.");
                Assert.That(marker.SurfaceId, Is.EqualTo(support.Id));
                Assert.That(marker.RegionId, Is.EqualTo(support.RegionId));
                Assert.That(marker.FrameId, Is.EqualTo(support.FrameId));
                Assert.That(marker.PortalId, Is.EqualTo(support.PortalId));
            }
            Assert.That(geometry.Supports.Select(s => s.Source.gameObject.scene.path).Distinct().Count(), Is.EqualTo(world.Regions.Length),
                "The captured supports cover every builder-written region scene.");

            // (6) The generated profile carries the synthetic-design label this work item's receipt repeats.
            Assert.That(File.Exists(folder + "/connected-world-profile.json"), Is.True,
                "The builder writes the profile it generated at the fixture root.");
            Assert.That(world.Classification, Is.EqualTo("synthetic_design_not_facility_acceptance"));
            Assert.That(world.Regions.All(r => r.GeometryStatus.StartsWith("synthetic_design", StringComparison.Ordinal)), Is.True);
            Assert.That(world.Regions.Select(r => r.SceneName).Distinct().Count(), Is.EqualTo(world.Regions.Length));
        }

        [Test]
        public void VehicleBridgeAndPlatformFramesStaySeparateIdentities()
        {
            var bridge = geometry.Supports.Single(s => s.Id == BridgeSupportId);
            var carFloor = geometry.Supports.Single(s => s.Id == CarSupportId);
            var platformFloor = geometry.Supports.Single(s => s.Id == PlatformSupportId);
            Assert.That(bridge.FrameId, Is.EqualTo(WorldFrame), "The fixed boarding bridge is station geometry in the world frame.");
            Assert.That(bridge.RegionId, Is.EqualTo(PlatformRegion));
            Assert.That(bridge.PortalId, Is.EqualTo(BoardingPortalId));
            Assert.That(carFloor.FrameId, Is.EqualTo(CarFrame));
            Assert.That(carFloor.RegionId, Is.EqualTo(CarRegion));
            Assert.That(carFloor.PortalId, Is.Empty, "The carriage floor is not a portal-owned surface.");
            Assert.That(platformFloor.FrameId, Is.EqualTo(WorldFrame));
            var portal = world.Portals.Single(p => p.Id == BoardingPortalId);
            Assert.That(portal.StaticBoarding, Is.True);
            Assert.That(world.Region(portal.From).FrameId, Is.EqualTo(CarFrame));
            Assert.That(world.Region(portal.To).FrameId, Is.EqualTo(WorldFrame));
            Assert.That(bridge.RegionId, Is.EqualTo(portal.To), "The bridge belongs to the station, so it can never be carried by the carriage frame.");

            // One authored level plane at Y = 2 across both seams: the support handover introduces no step.
            // Each deck is asserted against the authored seam elevation first, and only then against the other
            // decks through the geometry slack, so this is provenance rather than three colliders compared
            // against each other, which would stay green even if all three were built at the same wrong height.
            Assert.That(portal.FromPoint.Y, Is.EqualTo(AuthoredSeamY).Within(1e-6), "The carriage doorway is authored at Y = 2.");
            Assert.That(portal.ToPoint.Y, Is.EqualTo(AuthoredSeamY).Within(1e-6), "The station-side edge is authored at Y = 2.");
            var seam = bridge.Elevation(bridge.MinX, bridge.MinZ);
            Assert.That(seam, Is.EqualTo(AuthoredSeamY).Within(1e-3), "The fixed bridge deck is authored at Y = 2, not surveyed from a facility.");
            Assert.That(carFloor.Elevation(carFloor.MinX, carFloor.MinZ), Is.EqualTo(AuthoredSeamY).Within(1e-3), "The carriage floor is authored at Y = 2.");
            Assert.That(platformFloor.Elevation(platformFloor.MinX, platformFloor.MinZ), Is.EqualTo(AuthoredSeamY).Within(1e-3), "The platform floor is authored at Y = 2.");
            Assert.That(carFloor.Gradient.Length, Is.LessThan(1e-6), "The carriage floor is a level synthetic plane.");
            Assert.That(bridge.Gradient.Length, Is.LessThan(1e-6), "The bridge deck is a level synthetic plane.");
            Assert.That(platformFloor.Gradient.Length, Is.LessThan(1e-6), "The platform floor is a level synthetic plane.");
            Assert.That(Math.Abs(carFloor.Elevation(carFloor.MinX, carFloor.MinZ) - seam), Is.LessThanOrEqualTo(ConnectedWorldGeometry.GeometrySlackM));
            Assert.That(Math.Abs(platformFloor.Elevation(platformFloor.MinX, platformFloor.MinZ) - seam), Is.LessThanOrEqualTo(ConnectedWorldGeometry.GeometrySlackM));

            using var adapter = Adapter(Spawn("porter", PlatformRegion, new Point3(-50, 2, 2), BodyRadiusM), Spawn("passenger", CarRegion, world.Region(CarRegion).Hub, BodyRadiusM));
            var bridgeAt = bridge.Source.transform.position;
            var carFloorAt = carFloor.Source.transform.position;
            var porterAt = adapter.Pose("porter").Position;
            var commands = new[] { new WorldMotionCommand { BodyId = "passenger", Pinned = true } };
            Assert.That(adapter.TryAdvance(.05, commands, Target(world, CarFrame, -46, 2, 15), Array.Empty<string>(), out var report), Is.True, report.Failure);

            Assert.That(Vector3.Distance(bridge.Source.transform.position, bridgeAt), Is.LessThan(1e-6), "The station bridge must not follow the carriage.");
            Assert.That(carFloor.Source.transform.position.x - carFloorAt.x, Is.EqualTo(4).Within(.001), "The carriage floor travels with the carriage frame.");
            Assert.That(adapter.Pose("porter").FrameId, Is.EqualTo(WorldFrame));
            Assert.That(adapter.Pose("porter").Position.DistanceSquared(porterAt), Is.LessThan(1e-9), "A station body is not dragged by the carriage frame.");
            Assert.That(SupportOf(adapter, "porter"), Is.EqualTo(PlatformSupportId));
            Assert.That(SpaceOf(adapter, "porter"), Is.EqualTo(WorldFrame));
            Assert.That(adapter.Capture().Crowd.Agents.Single(a => a.Id == "passenger").FrameId, Is.EqualTo(CarFrame));
            Assert.That(adapter.Pose("passenger").LocalPosition.DistanceSquared(new Point3()), Is.LessThan(1e-9));
            Assert.That(adapter.BoardingOccupied(BoardingPortalId), Is.False,
                "The platform porter and the pinned carriage passenger are both clear of the fixed boarding bridge, so the bridge is unoccupied.");
            Assert.That(adapter.ValidateBodies(out var validation), Is.True, validation);
        }

        [Test]
        public void AlignmentToleranceFlipsTheContactSpaceWithoutMovingTheBody()
        {
            var authored = world.Frame(CarFrame).Origin;
            using var adapter = Adapter(Spawn("passenger", CarRegion, world.Region(CarRegion).Hub, BodyRadiusM));
            var commands = new[] { new WorldMotionCommand { BodyId = "passenger", Pinned = true } };
            // 0.0499 and 0.0501 bracket the authored alignment tolerance origin.DistanceSquared <= .0025 (0.05 m).
            var offsets = new[] { .02, .04, .0499, .0501, .06, .08 };
            var spaces = new List<string>();
            var advances = new List<double>();
            var previousX = adapter.Pose("passenger").Position.X;
            foreach (var offset in offsets)
            {
                var target = Target(world, CarFrame, authored.X + (float)offset, authored.Y, authored.Z);
                Assert.That(adapter.TryAdvance(.05, commands, target, Array.Empty<string>(), out var report), Is.True, report.Failure);
                var pose = adapter.Pose("passenger");
                spaces.Add(SpaceOf(adapter, "passenger"));
                advances.Add(pose.Position.X - previousX);
                previousX = pose.Position.X;
                Assert.That(pose.Position.X - authored.X, Is.EqualTo(offset).Within(5e-5), "offset " + offset + " must not jump.");
                Assert.That(pose.FrameId, Is.EqualTo(CarFrame));
                Assert.That(pose.LocalPosition.DistanceSquared(new Point3()), Is.LessThan(1e-9), "offset " + offset + " keeps the carriage-local pose.");
            }
            Assert.That(spaces.Take(3).All(s => s == WorldFrame), Is.True, "Inside the authored alignment tolerance the carriage shares the station contact space.");
            Assert.That(spaces.Skip(3).All(s => s == CarFrame), Is.True, "Past the authored tolerance the carriage owns its contact space.");
            for (var i = 0; i < advances.Count; i++)
                Assert.That(advances[i], Is.EqualTo(offsets[i] - (i == 0 ? 0 : offsets[i - 1])).Within(1e-4),
                    "tick " + i + " advances by the carriage frame delta only; the contact-space change adds no displacement.");
        }

        [Test]
        public void PlatformToCarriageBoardingStepsThroughTheStationBridgeOnce()
        {
            var carHub = world.Region(CarRegion).Hub;
            using var adapter = Adapter(Spawn("boarder", PlatformRegion, world.Region(PlatformRegion).Hub, BodyRadiusM));
            var command = new[] { new WorldMotionCommand { BodyId = "boarder", Goal = world.Pose(CarRegion, carHub) } };
            var surfaces = new List<string>();
            var frames = new List<string>();
            var supportFlipZs = new List<double>(); var supportFlipTicks = new List<int>();
            var frameFlipZ = double.NaN; var frameFlipTick = -1;
            var maxStep = 0.0; var previous = adapter.Pose("boarder"); var tick = 0;
            while (tick++ < 300)
            {
                Assert.That(adapter.TryAdvance(.1, command, out var report), Is.True, report.Failure);
                var pose = adapter.Pose("boarder");
                var surface = SupportOf(adapter, "boarder");
                maxStep = Math.Max(maxStep, Flat(pose.Position, previous.Position));
                Assert.That(maxStep, Is.LessThan(.25), "tick " + tick + " moved the boarder by a frame-sized jump.");
                if (surfaces.Count == 0 || surfaces[surfaces.Count - 1] != surface)
                {
                    surfaces.Add(surface);
                    if (surfaces.Count > 1) { supportFlipZs.Add(pose.Position.Z); supportFlipTicks.Add(tick); }
                }
                if (frames.Count == 0 || frames[frames.Count - 1] != pose.FrameId)
                {
                    frames.Add(pose.FrameId);
                    if (frames.Count > 1 && double.IsNaN(frameFlipZ)) { frameFlipZ = pose.Position.Z; frameFlipTick = tick; }
                }
                previous = pose;
                if (Flat(pose.Position, carHub) <= .08) break;
            }
            Assert.That(tick, Is.LessThan(300), "The boarder must reach the carriage through the fixed bridge.");
            Assert.That(Flat(adapter.Pose("boarder").Position, carHub), Is.LessThanOrEqualTo(.09));
            Assert.That(surfaces, Is.EqualTo(new[] { PlatformSupportId, BridgeSupportId, CarSupportId }),
                "Boarding steps on the platform floor, then the fixed station bridge, then the carriage floor, and on nothing else; the carriage floor is never entered through the platform.");
            Assert.That(frames, Is.EqualTo(new[] { WorldFrame, CarFrame }),
                "The boarder changes frame exactly once, at the authored boarding seam, and never returns.");
            Assert.That(supportFlipZs.Count, Is.EqualTo(2),
                "Exactly two surface handovers: platform floor to fixed bridge, then fixed bridge to carriage floor.");
            Assert.That(supportFlipZs[0], Is.EqualTo(BridgeStationEdgeZ).Within(.25),
                "The platform floor hands over to the fixed bridge at its authored station-side edge Z = 5.0 (the portal ToPoint), never at the carriage doorway.");
            Assert.That(supportFlipZs[1], Is.EqualTo(SeamZ).Within(.25),
                "The fixed bridge hands over to the carriage floor at the authored doorway Z = 12.5 (the portal FromPoint), never at the platform edge.");
            Assert.That(frameFlipZ, Is.EqualTo(BoardingRebindZ).Within(.25),
                "The pose keeps station coordinates until the whole footprint has left the fixed bridge (Z = 12.5 + radius + clearance).");
            Assert.That(supportFlipTicks[0], Is.LessThan(supportFlipTicks[1]),
                "The two authored surface planes are crossed in order; the carriage floor is never entered before the bridge.");
            Assert.That(supportFlipTicks[1], Is.LessThanOrEqualTo(frameFlipTick),
                "The carriage floor binds before the carriage frame, so the surface handover and the frame rebinding can never be swapped.");
            Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
        }

        [Test]
        public void StationBodyOnTheFixedBridgeBlocksDepartureWithoutASupportSwap()
        {
            using var adapter = Adapter(Spawn("porter", PlatformRegion, new Point3(-50, 2, 7.5f), BodyRadiusM),
                Spawn("straddler", PlatformRegion, new Point3(-50, 2, 12.2f), BodyRadiusM));
            Assert.That(SupportOf(adapter, "porter"), Is.EqualTo(BridgeSupportId),
                "A body between the platform edge and the carriage doorway stands on the fixed station bridge.");
            Assert.That(SupportOf(adapter, "straddler"), Is.EqualTo(BridgeSupportId));
            Assert.That(adapter.Pose("porter").FrameId, Is.EqualTo(WorldFrame));
            Assert.That(adapter.Pose("straddler").FrameId, Is.EqualTo(WorldFrame),
                "A footprint still spanning the fixed bridge keeps station coordinates while the carriage is aligned.");
            Assert.That(adapter.Pose("straddler").PortalId, Is.EqualTo(BoardingPortalId));
            Assert.That(adapter.BoardingOccupied(BoardingPortalId), Is.True);
            Assert.That(adapter.CanClosePortal(BoardingPortalId), Is.False);

            var before = adapter.ExportCheckpoint();
            Assert.That(adapter.TryApplyFrames(Target(world, CarFrame, -44, 2, 15), Array.Empty<string>(), out var failure), Is.False);
            Assert.That(failure, Does.Contain("Occupied portal prevents closure/departure"));
            Assert.That(adapter.ExportCheckpoint(), Is.EqualTo(before),
                "A refused departure is atomic: no frame, pose, support or contact space is partially committed.");
            Assert.That(adapter.Pose("porter").FrameId, Is.EqualTo(WorldFrame), "The bridge body is never rebound to the departing carriage.");
            Assert.That(adapter.Pose("straddler").FrameId, Is.EqualTo(WorldFrame));
            Assert.That(SupportOf(adapter, "straddler"), Is.EqualTo(BridgeSupportId));
            Assert.That(adapter.Capture().Frames.Single(f => f.FrameId == CarFrame).Origin.DistanceSquared(world.Frame(CarFrame).Origin), Is.Zero);

            // The same alignment still boards the straddling body once it walks clear of the bridge.
            var carHub = world.Region(CarRegion).Hub;
            var command = new[] { new WorldMotionCommand { BodyId = "straddler", Goal = world.Pose(CarRegion, carHub) } };
            var boarded = false;
            var floorTick = -1; var floorZ = double.NaN; var frameTick = -1;
            for (var tick = 0; tick < 120 && !boarded; tick++)
            {
                Assert.That(adapter.TryAdvance(.1, command, out var report), Is.True, report.Failure);
                var pose = adapter.Pose("straddler");
                if (SupportOf(adapter, "straddler") == CarSupportId)
                {
                    if (floorTick < 0) { floorTick = tick; floorZ = pose.Position.Z; }
                    // The previous revision asserted position.Z >= SeamZ - .01 here. With a refused
                    // departure the carriage delta is zero, so the carriage floor support cannot contain
                    // world Z < 12.4998 and that assertion could never fail. It is removed rather than
                    // kept as unreachable coverage; the handover plane is pinned by the reachable floorZ
                    // assertion below (which fails if the handover happened at Z = 5.0 or Z = 0 instead)
                    // and by CarriageToPlatformAlightingStepsThroughTheStationBridgeOnce, which runs the
                    // same handover with the carriage frame moved to a non-zero authored delta.
                }
                if (pose.FrameId == CarFrame) { if (frameTick < 0) frameTick = tick; boarded = true; }
            }
            Assert.That(floorTick, Is.GreaterThanOrEqualTo(0),
                "The refused departure must not damage the boarding bridge: the straddler still reaches the carriage floor.");
            Assert.That(floorZ, Is.EqualTo(SeamZ).Within(.25),
                "The floor handover happens at the authored doorway, never at the platform edge or the bridge's station-side edge.");
            Assert.That(boarded, Is.True, "Walking clear of the bridge rebinds the straddler to the carriage frame.");
            Assert.That(frameTick, Is.GreaterThanOrEqualTo(floorTick),
                "The carriage floor binds before the carriage frame; a body past the doorway is never left holding station coordinates.");
            var straddlerPose = adapter.Pose("straddler");
            Assert.That(adapter.Capture().Frames.Single(f => f.FrameId == straddlerPose.FrameId).ToWorld(straddlerPose.LocalPosition).DistanceSquared(straddlerPose.Position), Is.LessThan(1e-7),
                "The boarded pose stays one coordinate transformation: the carriage-local pose and the carriage-frame world pose agree.");
            Assert.That(SupportOf(adapter, "porter"), Is.EqualTo(BridgeSupportId), "The remaining bridge body keeps its station support.");
            Assert.That(adapter.Pose("porter").FrameId, Is.EqualTo(WorldFrame));
        }

        [Test]
        public void DepartingCarriageCarriesItsPinnedBodyAndLeavesStationBodiesFixed()
        {
            using var adapter = Adapter(Spawn("passenger", CarRegion, world.Region(CarRegion).Hub, BodyRadiusM), Spawn("porter", PlatformRegion, new Point3(-50, 2, 2), BodyRadiusM));
            var commands = new[] { new WorldMotionCommand { BodyId = "passenger", Pinned = true } };
            var porterAt = adapter.Pose("porter").Position;
            var local = adapter.Pose("passenger").LocalPosition;
            var worldX = adapter.Pose("passenger").Position.X;
            var spaces = new List<string>();
            var bridge = geometry.Supports.Single(s => s.Id == BridgeSupportId);
            var bridgeAt = bridge.Source.transform.position;
            var deltas = new[] { .02, .02, .02, .44, .5, .5 };
            var offset = 0.0;
            for (var i = 0; i < deltas.Length; i++)
            {
                offset += deltas[i];
                var target = Target(world, CarFrame, world.Frame(CarFrame).Origin.X + (float)offset, 2, 15);
                Assert.That(adapter.TryAdvance(.05, commands, target, Array.Empty<string>(), out var report), Is.True, report.Failure);
                var pose = adapter.Pose("passenger");
                spaces.Add(SpaceOf(adapter, "passenger"));
                Assert.That(pose.Position.X - worldX, Is.EqualTo(deltas[i]).Within(1e-4),
                    "tick " + i + " must advance the carriage body by exactly the carriage frame delta.");
                worldX = pose.Position.X;
                Assert.That(pose.LocalPosition.DistanceSquared(local), Is.LessThan(1e-9),
                    "tick " + i + " must not change the carriage-local pose while the carriage frame moves.");
                Assert.That(adapter.Pose("porter").Position.DistanceSquared(porterAt), Is.LessThan(1e-9),
                    "tick " + i + " must not drag a platform body with the carriage frame.");
                Assert.That(SupportOf(adapter, "porter"), Is.EqualTo(PlatformSupportId));
            }
            Assert.That(spaces[0], Is.EqualTo(WorldFrame), "Inside the authored alignment tolerance the carriage still shares the station contact space.");
            Assert.That(spaces[spaces.Count - 1], Is.EqualTo(CarFrame));
            Assert.That(adapter.Pose("passenger").FrameId, Is.EqualTo(CarFrame));
            Assert.That(adapter.Pose("porter").FrameId, Is.EqualTo(WorldFrame));
            Assert.That(Vector3.Distance(bridge.Source.transform.position, bridgeAt), Is.LessThan(1e-6), "The fixed boarding bridge never follows the carriage.");
            Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
        }

        [Test]
        public void StoredReportKeepsItsObservationTimeCarriagePose()
        {
            var authored = world.Frame(CarFrame).Copy();
            var seat = new Point3(-46, 2, 14.2f);
            var local = authored.ToLocal(seat);
            var initial = new WorldState { SchemaVersion = 3, WorldId = "connected-world", ShiftId = "shift-001", SpatialProfileId = world.ProfileId,
                SimulationDefinitionHash = Hash, SimulationCheckpoint = Physical(0), Frames = world.Frames.Select(f => f.Copy()).ToArray(),
                Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "team", RoleId = "role-01", RegionId = CarRegion, FrameId = CarFrame,
                    Position = seat, LocalPosition = local, ObservedIds = new[] { "incident-old" } } },
                Entities = new[] { new EntityState { EntityId = "incident-old", Kind = EntityKind.Incident, RegionId = CarRegion, FrameId = CarFrame,
                    Position = seat, LocalPosition = local } } };
            var sink = new Sink();
            var shift = new AuthoritativeShift(initial, sink, (_, __) => true);
            Assert.That(shift.Submit("p", Command(CommandKind.Report, "report-old", "incident-old")).Code, Is.EqualTo(CommandCode.Accepted));
            var reported = shift.ExportCheckpoint().Reports.Single();
            Assert.That(reported.ObservedFrame.Origin.DistanceSquared(authored.Origin), Is.Zero);
            Assert.That(reported.ObservedFrame.YawDegrees, Is.Zero);
            Assert.That(reported.ObservedSimulationTick, Is.Zero);
            Assert.That(reported.Position.DistanceSquared(seat), Is.Zero);

            // The carriage departs 42 m: the crew keeps its carriage-local seat pose, so the world pose moves.
            var departed = world.Frames.Select(f => f.Copy()).ToArray();
            var departedFrame = departed.Single(f => f.FrameId == CarFrame);
            departedFrame.Origin = new Point3(authored.Origin.X + 42, authored.Origin.Y, authored.Origin.Z);
            shift.ApplyServerSimulation(new ServerSimulationUpdate { Tick = 1, DefinitionHash = Hash, Checkpoint = Physical(1), Frames = departed,
                Actors = new[] { new ServerActorPose { ParticipantId = "p", Pose = new SpatialPose { RegionId = CarRegion, FrameId = CarFrame,
                    Position = departedFrame.ToWorld(local), LocalPosition = local } } },
                Entities = new[] { new EntityState { EntityId = "incident-old", Kind = EntityKind.Incident, RegionId = CarRegion, FrameId = CarFrame,
                    Position = departedFrame.ToWorld(local), LocalPosition = local } } });

            var live = shift.ReadSimulation();
            Assert.That(live.Frames.Single(f => f.FrameId == CarFrame).Origin.DistanceSquared(departedFrame.Origin), Is.Zero);
            Assert.That(live.Tick, Is.EqualTo(1));
            Assert.That(shift.Observe("p").Entities.Single(e => e.EntityId == "incident-old").Position.DistanceSquared(departedFrame.ToWorld(local)), Is.Zero);

            var kept = shift.ExportCheckpoint().Reports.Single(r => r.ReportId == "report-1");
            Assert.That(kept.ObservedFrame.Origin.DistanceSquared(authored.Origin), Is.Zero,
                "The stored report keeps the observation-time carriage pose; it is never overwritten with the current one.");
            Assert.That(kept.ObservedFrame.Origin.DistanceSquared(departedFrame.Origin), Is.Not.Zero);
            Assert.That(kept.Position.DistanceSquared(seat), Is.Zero);
            Assert.That(kept.LocalPosition.DistanceSquared(local), Is.Zero);
            Assert.That(kept.ObservedSimulationTick, Is.Zero);
            Assert.That(kept.Sequence, Is.EqualTo(1));

            // A new observation of the same entity legitimately captures the current carriage pose.
            Assert.That(shift.Submit("p", Command(CommandKind.Report, "report-new", "incident-old")).Code, Is.EqualTo(CommandCode.Accepted));
            var fresh = shift.ExportCheckpoint().Reports.Single(r => r.ReportId == "report-2");
            Assert.That(fresh.ObservedFrame.Origin.DistanceSquared(departedFrame.Origin), Is.Zero, "A new report captures the current carriage pose.");
            Assert.That(fresh.ObservedSimulationTick, Is.EqualTo(1));
            Assert.That(shift.ExportCheckpoint().Reports.Single(r => r.ReportId == "report-1").ObservedFrame.Origin.DistanceSquared(authored.Origin), Is.Zero,
                "Publishing a new report must not rewrite the older observation-time pose.");

            // Invariance probes. SpatialFrame and TeamReport are mutable reference types with public
            // fields, so a report that aliased a live frame - or a checkpoint that handed out the stored
            // instance instead of a copy - would silently follow the carriage. Each probe clobbers a
            // reachable object in place and then re-reads the report from the shift.
            var handle = shift.ExportCheckpoint().Reports.Single(r => r.ReportId == "report-1");
            Assert.That(ReferenceEquals(handle.ObservedFrame, kept.ObservedFrame), Is.False,
                "Two checkpoints must hand out distinct frame instances; a shared instance would mean the report is not a snapshot.");
            handle.ObservedFrame.Origin = new Point3(-1, -1, -1); handle.ObservedFrame.YawDegrees = 90;
            var afterHandleClobber = shift.ExportCheckpoint().Reports.Single(r => r.ReportId == "report-1");
            Assert.That(afterHandleClobber.ObservedFrame.Origin.DistanceSquared(authored.Origin), Is.Zero,
                "Clobbering one checkpoint's frame in place must not reach the stored observation-time pose.");
            Assert.That(afterHandleClobber.ObservedFrame.YawDegrees, Is.Zero);
            Assert.That(afterHandleClobber.Position.DistanceSquared(seat), Is.Zero);
            Assert.That(afterHandleClobber.ObservedFrame.Origin.DistanceSquared(departedFrame.Origin), Is.Not.Zero,
                "The stored observation-time pose stays distinguishable from the current carriage pose.");

            var liveHandle = shift.ReadSimulation().Frames.Single(f => f.FrameId == CarFrame);
            Assert.That(ReferenceEquals(liveHandle, departedFrame), Is.False, "The simulation view hands out its own frame instances.");
            liveHandle.Origin = new Point3(-1, -1, -1); liveHandle.YawDegrees = 90;
            Assert.That(shift.ReadSimulation().Frames.Single(f => f.FrameId == CarFrame).Origin.DistanceSquared(departedFrame.Origin), Is.Zero,
                "Clobbering a simulation-view frame in place must not reach the live carriage frame.");
            Assert.That(shift.ExportCheckpoint().Frames.Single(f => f.FrameId == CarFrame).Origin.DistanceSquared(departedFrame.Origin), Is.Zero);
            Assert.That(shift.ExportCheckpoint().Reports.Single(r => r.ReportId == "report-1").ObservedFrame.Origin.DistanceSquared(authored.Origin), Is.Zero,
                "Neither moving nor clobbering the live carriage frame rewrites the observation-time pose.");
            var frozen = shift.ExportCheckpoint().Reports.Single(r => r.ReportId == "report-1");
            Assert.That(frozen.ObservedFrame.FrameId, Is.EqualTo(frozen.FrameId));
            Assert.That(frozen.ObservedFrame.ToWorld(frozen.LocalPosition).DistanceSquared(frozen.Position), Is.LessThan(.0001),
                "The frozen pose is internally consistent against its own ObservedFrame, which is exactly the invariant AuthoritativeShift.ValidReportPose checks (AuthoritativeShift.cs:383-390) - and it is checked against report.ObservedFrame, never the live frame.");

            // Approved replay rebuilds the same historical pose next to the moved live frame.
            var restored = AuthoritativeShift.Restore(initial, new Sink(), (_, __) => true);
            foreach (var commit in sink.Commits) restored.Replay(commit);
            var recovered = restored.ExportCheckpoint();
            Assert.That(recovered.Reports.Single(r => r.ReportId == "report-1").ObservedFrame.Origin.DistanceSquared(authored.Origin), Is.Zero);
            Assert.That(recovered.Reports.Single(r => r.ReportId == "report-2").ObservedFrame.Origin.DistanceSquared(departedFrame.Origin), Is.Zero);
            Assert.That(recovered.Frames.Single(f => f.FrameId == CarFrame).Origin.DistanceSquared(departedFrame.Origin), Is.Zero);
            Assert.That(recovered.SimulationTick, Is.EqualTo(1));
            Assert.That(restored.Paused, Is.True);

            // A third carriage pose coexists with both stored observations. The live frame moves again, so
            // at the end report-1 holds the authored origin, report-2 holds the tick-1 origin and only the
            // live frame holds the tick-2 origin: the current pose overwrites neither stored one.
            var third = departed.Select(f => f.Copy()).ToArray();
            var thirdFrame = third.Single(f => f.FrameId == CarFrame);
            thirdFrame.Origin = new Point3(departedFrame.Origin.X + 8, departedFrame.Origin.Y, departedFrame.Origin.Z);
            shift.ApplyServerSimulation(new ServerSimulationUpdate { Tick = 2, DefinitionHash = Hash, Checkpoint = Physical(2), Frames = third,
                Actors = new[] { new ServerActorPose { ParticipantId = "p", Pose = new SpatialPose { RegionId = CarRegion, FrameId = CarFrame,
                    Position = thirdFrame.ToWorld(local), LocalPosition = local } } },
                Entities = new[] { new EntityState { EntityId = "incident-old", Kind = EntityKind.Incident, RegionId = CarRegion, FrameId = CarFrame,
                    Position = thirdFrame.ToWorld(local), LocalPosition = local } } });
            var final = shift.ExportCheckpoint();
            Assert.That(shift.ReadSimulation().Tick, Is.EqualTo(2));
            Assert.That(final.Frames.Single(f => f.FrameId == CarFrame).Origin.DistanceSquared(thirdFrame.Origin), Is.Zero);
            Assert.That(final.Reports.Single(r => r.ReportId == "report-1").ObservedFrame.Origin.DistanceSquared(authored.Origin), Is.Zero,
                "The observation-time pose survives a second carriage move; the current pose never overwrites it.");
            Assert.That(final.Reports.Single(r => r.ReportId == "report-1").ObservedFrame.Origin.DistanceSquared(thirdFrame.Origin), Is.Not.Zero);
            Assert.That(final.Reports.Single(r => r.ReportId == "report-2").ObservedFrame.Origin.DistanceSquared(departedFrame.Origin), Is.Zero,
                "report-2 keeps the tick-1 carriage pose after the carriage moves again.");
            Assert.That(final.Reports.Single(r => r.ReportId == "report-2").ObservedFrame.Origin.DistanceSquared(thirdFrame.Origin), Is.Not.Zero);
            Assert.That(final.Reports.Select(r => r.ObservedFrame.Origin.DistanceSquared(authored.Origin)).Count(d => d == 0), Is.EqualTo(1),
                "Exactly one stored observation is anchored on the authored carriage pose.");
        }

        /// <summary>
        /// The mirror of boarding, and the case the boarding-only revision never exercised:
        /// SimulatedSpatialWorld.TryLocate's train-to-station arm (SimulatedSpatialWorld.cs:60-66, the
        /// decision at line 65) runs only when the body is still located in the carriage region and the
        /// pose is being handed back to the station. A body spawned on the carriage floor walks out over
        /// the fixed bridge onto the platform, and every claim below is read off that walk.
        /// </summary>
        [Test]
        public void CarriageToPlatformAlightingStepsThroughTheStationBridgeOnce()
        {
            var carHub = world.Region(CarRegion).Hub;
            var platformHub = world.Region(PlatformRegion).Hub;
            // SimulatedSpatialWorld: the pose keeps carriage coordinates until the whole 0.3 m footprint
            // has cleared the carriage doorway, which is the mirror of the 12.835 m station-side plane.
            // Every body in this fixture is 0.3 m; the plane is therefore 12.165 m, the mirror of the
            // 12.835 m plane at which a boarding body is rebound to the carriage frame.
            var carriageFootprintPlaneZ = AlightingRebindZ;
            var surfaces = new List<string>();
            var frames = new List<string>();
            var supportFlipZ = new List<double>();
            var supportFlipTick = new List<int>();
            var bridgeTickZ = new List<double>();
            var bridgeTickFrame = new List<string>();
            var bridgeTickIndex = new List<int>();
            // The three authored doorway planes the alighting walk crosses, in the order it crosses them.
            var landmarks = new[] { BoardingRebindZ, SeamZ, BridgeStationEdgeZ };
            var crossingTick = new[] { -1, -1, -1 };
            var crossingZ = new double[landmarks.Length];
            var frameFlipZ = double.NaN;
            var frameFlipTick = -1;
            var maxStep = 0.0;

            using (var adapter = Adapter(Spawn("alighter", CarRegion, carHub, BodyRadiusM)))
            {
                var start = adapter.Pose("alighter");
                Assert.That(start.RegionId, Is.EqualTo(CarRegion), "The alighter starts located in the carriage region.");
                Assert.That(start.FrameId, Is.EqualTo(CarFrame), "The alighter starts in the carriage frame.");
                Assert.That(SupportOf(adapter, "alighter"), Is.EqualTo(CarSupportId), "The alighter starts on the carriage floor.");

                var liveFrames = adapter.Capture().Frames;
                var command = new[] { new WorldMotionCommand { BodyId = "alighter", Goal = world.Pose(PlatformRegion, platformHub) } };
                var previous = start;
                var tick = 0;
                while (tick++ < 400)
                {
                    Assert.That(adapter.TryAdvance(.1, command, out var report), Is.True, report.Failure);
                    var pose = adapter.Pose("alighter");
                    var surface = SupportOf(adapter, "alighter");
                    var step = Flat(pose.Position, previous.Position);
                    maxStep = Math.Max(maxStep, step);
                    Assert.That(maxStep, Is.LessThan(.25),
                        "tick " + tick + " moved the alighter by a frame-sized jump; the per-tick world delta stays bounded across the whole walk.");
                    // Geometry, movement and the reported pose stay one transform for the carriage frame too.
                    Assert.That(pose.Position.DistanceSquared(liveFrames.Single(f => f.FrameId == pose.FrameId).ToWorld(pose.LocalPosition)), Is.LessThan(1e-7),
                        "tick " + tick + " must keep world pose = frame.ToWorld(local pose).");
                    if (surfaces.Count == 0 || surfaces[surfaces.Count - 1] != surface)
                    {
                        surfaces.Add(surface);
                        if (surfaces.Count > 1) { supportFlipZ.Add(pose.Position.Z); supportFlipTick.Add(tick); }
                    }
                    if (frames.Count == 0 || frames[frames.Count - 1] != pose.FrameId)
                    {
                        frames.Add(pose.FrameId);
                        if (frames.Count > 1 && double.IsNaN(frameFlipZ)) { frameFlipZ = pose.Position.Z; frameFlipTick = tick; }
                    }
                    for (var i = 0; i < landmarks.Length; i++)
                        if (crossingTick[i] < 0 && pose.Position.Z <= landmarks[i]) { crossingTick[i] = tick; crossingZ[i] = pose.Position.Z; }
                    if (surface == BridgeSupportId) { bridgeTickZ.Add(pose.Position.Z); bridgeTickFrame.Add(pose.FrameId); bridgeTickIndex.Add(tick); }
                    previous = pose;
                    if (Flat(pose.Position, platformHub) <= .08) break;
                }

                Assert.That(tick, Is.LessThan(400), "The alighter must reach the platform through the fixed bridge.");
                Assert.That(Flat(adapter.Pose("alighter").Position, platformHub), Is.LessThanOrEqualTo(.09));
                Assert.That(adapter.ValidateBodies(out var validation), Is.True, validation);

                // (a) the support sequence is exactly the reverse of boarding, with nothing in between.
                Assert.That(surfaces, Is.EqualTo(new[] { CarSupportId, BridgeSupportId, PlatformSupportId }),
                    "Alighting steps off the carriage floor, then the fixed station bridge, then the platform floor, and on nothing else; the platform floor is never entered directly from the carriage.");
                Assert.That(supportFlipZ.Count, Is.EqualTo(2), "Exactly two surface handovers, the reverse pair of boarding.");
                Assert.That(supportFlipZ[0], Is.EqualTo(SeamZ).Within(.25),
                    "The carriage floor hands over to the fixed bridge at the authored doorway Z = 12.5 (the portal FromPoint), never at the platform edge.");
                Assert.That(supportFlipZ[1], Is.EqualTo(BridgeStationEdgeZ).Within(.25),
                    "The fixed bridge hands over to the platform floor at its authored station-side edge Z = 5.0 (the portal ToPoint), never at the carriage doorway.");
                Assert.That(supportFlipZ[1], Is.LessThan(supportFlipZ[0]), "The two authored planes are crossed in the reverse order from boarding.");

                // (b) exactly one frame rebind, carriage -> station, and it never precedes the handover.
                Assert.That(frames, Is.EqualTo(new[] { CarFrame, WorldFrame }),
                    "The alighter changes frame exactly once, carriage to station, and never returns.");
                Assert.That(supportFlipTick[0], Is.LessThan(frameFlipTick),
                    "The carriage floor is released strictly before the carriage frame; a body still standing on the carriage floor is never left holding station coordinates.");
                Assert.That(frameFlipZ, Is.EqualTo(carriageFootprintPlaneZ).Within(.25),
                    "The carriage frame is released only once the whole footprint has cleared the carriage doorway (Z = 12.5 - radius - clearance), the mirror of the 12.835 m boarding plane.");

                // (c) the doorway crossings are crossed monotonically 12.835 -> 12.5 -> 5.0.
                Assert.That(crossingTick.All(t => t > 0), Is.True, "The alighting walk crosses all three authored doorway planes.");
                Assert.That(crossingTick[0], Is.LessThan(crossingTick[1]), "The 12.835 m carriage-side approach plane is crossed before the doorway.");
                Assert.That(crossingTick[1], Is.LessThan(frameFlipTick),
                    "The pose still holds carriage coordinates at both carriage-side crossings (the 12.835 m approach plane and the 12.5 m doorway); the carriage frame is released below them, once the footprint has cleared.");
                Assert.That(frameFlipTick, Is.LessThan(crossingTick[2]),
                    "The carriage frame is already released well before the station-side bridge edge at 5.0 m, where the body is on world coordinates.");
                for (var i = 0; i < landmarks.Length; i++)
                    Assert.That(crossingZ[i], Is.EqualTo(landmarks[i]).Within(.25), "crossing " + i + " of the authored plane " + landmarks[i] + " m.");

                // The bridge is crossed in one direction, and the frame arms split exactly on the footprint.
                Assert.That(bridgeTickZ.Count, Is.GreaterThan(1), "The alighter actually stands on the fixed bridge.");
                for (var i = 1; i < bridgeTickZ.Count; i++)
                    Assert.That(bridgeTickZ[i], Is.LessThanOrEqualTo(bridgeTickZ[i - 1] + 1e-6),
                        "The alighting walk crosses the fixed bridge monotonically toward the station.");
                var carriageSide = Enumerable.Range(0, bridgeTickZ.Count).Where(i => bridgeTickFrame[i] == CarFrame).ToArray();
                var stationSide = Enumerable.Range(0, bridgeTickZ.Count).Where(i => bridgeTickFrame[i] == WorldFrame).ToArray();
                Assert.That(carriageSide.Length, Is.GreaterThan(0),
                    "A body on the bridge whose footprint still spans the carriage doorway keeps carriage coordinates: SimulatedSpatialWorld.cs:65 takes its train arm because inward >= -radius - BoardingClearanceM. No other arm of TryLocate can produce a carriage-frame pose at world Z below the doorway.");
                Assert.That(stationSide.Length, Is.GreaterThan(0),
                    "The same line 65 takes its station arm once the footprint clears, and the pose becomes world coordinates while the body is still on the station side of the bridge.");
                Assert.That(carriageSide.Max(), Is.LessThan(stationSide.Min()),
                    "The carriage frame is released exactly once on the bridge and is never re-entered.");
                Assert.That(carriageSide.Min(i => bridgeTickZ[i]), Is.GreaterThanOrEqualTo(carriageFootprintPlaneZ - .01),
                    "The carriage frame is held for every bridge tick at or above the footprint-clearance plane Z = 12.5 - radius - clearance.");
                Assert.That(stationSide.Max(i => bridgeTickZ[i]), Is.LessThan(carriageFootprintPlaneZ + .01),
                    "World coordinates resume strictly below that plane, so the frame rebind never precedes the surface handover.");
                Assert.That(frameFlipTick, Is.EqualTo(carriageSide.Max(i => bridgeTickIndex[i]) + 1),
                    "The rebind tick is the very next tick after the last carriage-frame bridge tick: the surface handover and the frame handover are adjacent and in that order. A rebind that ran even one tick early would land on a carriage-frame bridge tick and fail here.");
                Assert.That(carriageSide.Max(i => bridgeTickIndex[i]), Is.GreaterThanOrEqualTo(supportFlipTick[0]),
                    "The carriage-frame bridge ticks cannot begin before the carriage floor has handed the body over: the frame rebind never precedes the surface handover.");

                // (same walk, non-zero carriage delta) The floor handover plane must follow the live
                // transform, not the authored constant. The previous revision asserted this over a
                // refused departure, where the carriage delta is zero and the claim could never fail.
                const double carriageDeltaZ = -.048;
                using (var moved = Adapter(Spawn("slow-alighter", CarRegion, carHub, .3, .2)))
                {
                    var departed = Target(world, CarFrame, world.Frame(CarFrame).Origin.X, 2, world.Frame(CarFrame).Origin.Z + (float)carriageDeltaZ);
                    Assert.That(moved.TryApplyFrames(departed, Array.Empty<string>(), out var moveFailure), Is.True, moveFailure);
                    Assert.That(moved.Capture().Frames.Single(f => f.FrameId == CarFrame).Origin.Z - world.Frame(CarFrame).Origin.Z,
                        Is.EqualTo(carriageDeltaZ).Within(1e-6), "The walk below runs with a non-zero, still-aligned carriage delta.");
                    var slow = new[] { new WorldMotionCommand { BodyId = "slow-alighter", Goal = world.Pose(PlatformRegion, platformHub) } };
                    var handoverZ = double.NaN;
                    for (var slowTick = 0; slowTick < 400 && double.IsNaN(handoverZ); slowTick++)
                    {
                        Assert.That(moved.TryAdvance(.1, slow, out var slowReport), Is.True, slowReport.Failure);
                        if (SupportOf(moved, "slow-alighter") == CarSupportId) continue;
                        handoverZ = moved.Pose("slow-alighter").Position.Z;
                    }
                    Assert.That(double.IsNaN(handoverZ), Is.False, "The slowly walking alighter still releases the carriage floor.");
                    Assert.That(handoverZ, Is.LessThan(SeamZ - .04),
                        "With the carriage frame at a non-zero delta the carriage floor ends at SeamZ + delta = " + (SeamZ + carriageDeltaZ).ToString("R") + " and the handover follows it; a handover at the authored Z = 12.5 would mean the live frame delta was ignored.");
                    Assert.That(handoverZ, Is.EqualTo(SeamZ + carriageDeltaZ).Within(.06),
                        "The carriage-floor handover plane tracks the live carriage frame delta, so it depends on the transform and not only on the authored table.");
                }
            }

            // Direct witness for the walk above. The same previous carriage pose resolves to the carriage while
            // the footprint still spans the carriage doorway and to the station once the footprint has cleared
            // it. The station-side point is outside the carriage region and inside no exclusion, so no arm of
            // TryLocate other than the train-to-station branch of lines 60-66 can produce a station pose for it.
            var spatial = new SimulatedSpatialWorld(world);
            var location = spatial.At(world.Frames.Select(f => f.Copy()).ToArray());
            var fromCarriage = world.Pose(CarRegion, new Point3(-50, 2, 13));
            var noClosures = new HashSet<string>();
            Assert.That(spatial.TryLocate(location, fromCarriage, new Point3(-50, 2, 12.30f), (float)BodyRadiusM, noClosures, out var inDoorway), Is.True,
                "A point still under the carriage doorway resolves from a carriage pose.");
            Assert.That(inDoorway.RegionId, Is.EqualTo(CarRegion));
            Assert.That(inDoorway.FrameId, Is.EqualTo(CarFrame));
            Assert.That(inDoorway.PortalId, Is.EqualTo(BoardingPortalId));
            Assert.That(spatial.TryLocate(location, fromCarriage, new Point3(-50, 2, 12.10f), (float)BodyRadiusM, noClosures, out var pastDoorway), Is.True,
                "Once the footprint has cleared the doorway the same carriage pose resolves to the station.");
            Assert.That(pastDoorway.RegionId, Is.EqualTo(PlatformRegion));
            Assert.That(pastDoorway.FrameId, Is.EqualTo(WorldFrame));
            Assert.That(pastDoorway.PortalId, Is.EqualTo(BoardingPortalId));
            Assert.That(location.Region(PlatformRegion).Contains(new Point3(-50, 2, 12.10f)), Is.False,
                "The station-side witness point lies outside the platform region, so the station pose is produced by the doorway arm and not by ordinary containment.");
            Assert.That(new Point3(-50, 2, 12.10f).Z, Is.LessThan((float)AlightingRebindZ),
                "The witness point is on the station side of the alighting plane the walk asserts.");

            // The arm is re-read from the implementation by path, so a regression that moved or removed it fails
            // here rather than leaving a green test that no longer touches the alighting branch.
            var arm = File.ReadAllLines(ProjectPath("Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/SimulatedSpatialWorld.cs"));
            var guard = Array.FindIndex(arm, line => line.Trim() == "if (previous.RegionId == train.Id)");
            var branch = Array.FindIndex(arm, line => line.Trim() == "result = current.Pose(inward >= -radius - BoardingClearanceM ? train.Id : station, point, portal.Id);");
            Assert.That(guard, Is.GreaterThanOrEqualTo(0),
                "SimulatedSpatialWorld.cs must still open its train-to-station arm on the previous-carriage-region guard.");
            Assert.That(branch, Is.EqualTo(guard + 5),
                "The train-to-station decision must still be the fifth statement after that guard (the authored arm spans six lines); found at index " + branch + " against the guard at " + guard + ".");
            // Tie the two planes this fixture asserts to the contract's own clearance constant, and pin the
            // mirror relation between them. Redefining either plane on one side only must fail here.
            Assert.That(BoardingRebindZ - SeamZ, Is.EqualTo(BodyRadiusM + SimulatedSpatialWorld.BoardingClearanceM).Within(1e-9),
                "The station-to-carriage rebinding plane is the authored doorway plus radius plus the contract's BoardingClearanceM.");
            Assert.That(SeamZ - AlightingRebindZ, Is.EqualTo(BodyRadiusM + SimulatedSpatialWorld.BoardingClearanceM).Within(1e-9),
                "The carriage-to-station rebinding plane is the mirror of the boarding plane about the authored doorway; the branch boundary -radius - BoardingClearanceM of the line located here is its world Z.");
            Assert.That(BoardingRebindZ, Is.GreaterThan(SeamZ));
            Assert.That(AlightingRebindZ, Is.LessThan(SeamZ));
        }

        /// <summary>
        /// The authored alignment tolerance is 0.05 m AND 0 degrees. The other tests vary only distance,
        /// so a regression that dropped the yaw term would keep all of them green; this case presents a
        /// yawed and a raised frame and requires both to be refused.
        /// </summary>
        [Test]
        public void YawIsHalfOfTheAlignmentToleranceAndTheMetroFrameRoundTripsAt180Degrees()
        {
            var authored = world.Frame(CarFrame);
            var spatial = new SimulatedSpatialWorld(world);
            // 0.01 m is inside the 0.05 m distance tolerance, so only the 1 degree yaw can refuse this frame.
            var yawed = world.Frames.Select(f => f.Copy()).ToArray();
            yawed.Single(f => f.FrameId == CarFrame).Origin = new Point3(authored.Origin.X + .01f, authored.Origin.Y, authored.Origin.Z);
            yawed.Single(f => f.FrameId == CarFrame).YawDegrees = 1;
            Assert.That(() => { spatial.At(yawed); }, Throws.ArgumentException,
                "A frame whose yaw differs from the authored value is refused even when its origin is inside the distance tolerance.");
            // Levelness is the third half of the same check: a raised carriage is refused too.
            var raised = world.Frames.Select(f => f.Copy()).ToArray();
            raised.Single(f => f.FrameId == CarFrame).Origin = new Point3(authored.Origin.X, authored.Origin.Y + .1f, authored.Origin.Z);
            Assert.That(() => { spatial.At(raised); }, Throws.ArgumentException, "The straight level route is part of the same contract.");

            using var adapter = Adapter(Spawn("passenger", CarRegion, world.Region(CarRegion).Hub, BodyRadiusM));
            var before = adapter.ExportCheckpoint();
            Assert.That(adapter.TryApplyFrames(yawed, Array.Empty<string>(), out var failure), Is.False, "A yawed target frame is refused by the same contract the movement path uses.");
            Assert.That(failure, Is.Not.Empty, "The refusal carries a reason; it is not a silent no-op.");
            Assert.That(adapter.ExportCheckpoint(), Is.EqualTo(before), "A refused yawed frame commits nothing.");
            Assert.That(adapter.Capture().Frames.Single(f => f.FrameId == CarFrame).YawDegrees, Is.Zero);
            Assert.That(adapter.Pose("passenger").FrameId, Is.EqualTo(CarFrame));

            // The metro carriage is the non-zero-yaw identity in the authored table: at yaw 180 its local
            // +Z is world -Z, so a dropped yaw term would flip the sign of every local offset.
            var metro = world.Frame(MetroFrame);
            Assert.That(metro.YawDegrees, Is.EqualTo(180));
            var local = new Point3(1.25f, .5f, -3.75f);
            var worldPoint = metro.ToWorld(local);
            Assert.That(metro.ToLocal(worldPoint).DistanceSquared(local), Is.LessThan(1e-6), "ToLocal(ToWorld(p)) == p for the yawed metro frame.");
            Assert.That(worldPoint.X, Is.EqualTo(metro.Origin.X - 1.25f).Within(1e-3), "At yaw 180 the local +X offset lands on world -X.");
            Assert.That(worldPoint.Z, Is.EqualTo(metro.Origin.Z + 3.75f).Within(1e-3), "At yaw 180 the local -Z offset lands on world +Z.");
            var level = authored.ToWorld(new Point3(0, 0, 1));
            Assert.That(level.Z, Is.EqualTo(authored.Origin.Z + 1).Within(1e-4),
                "The yaw-free mainline frame maps local +Z to world +Z; the two carriages cannot share one transform that ignores yaw.");
        }

        private sealed class Sink : ICommitSink
        {
            public readonly List<ShiftCommit> Commits = new List<ShiftCommit>();
            public void Append(ShiftCommit commit) => Commits.Add(commit.Copy());
        }

        private static string Physical(long tick)
        {
            var fire = new ZoneFireModel(new FireNetworkDefinition { Cells = new[] { new FireCellDefinition { Id = "cell", WidthM = 5, DepthM = 5, HeightM = 4 } } });
            var crowd = new CrowdMotionModel(new CrowdDefinition { ProfileId = "fixture", Spaces = new[] { new CrowdSpace { Id = "floor" } } },
                new[] { new CrowdAgent { Id = "body", ContactSpaceId = "floor", RegionId = "train", FrameId = "train", SurfaceId = "floor" } });
            return PhysicalCheckpoint.Encode(new PhysicalWorldState { DefinitionHash = Hash, SimulationTick = tick, Fire = fire.ExportState(), CrowdCheckpoint = crowd.ExportCheckpoint() });
        }

        private static WorldCommand Command(CommandKind kind, string id, string target) => new WorldCommand
        { WorldId = "connected-world", ShiftId = "shift-001", ParticipantId = "p", TeamId = "team", CommandId = id, Kind = kind, TargetId = target };
    }
}
