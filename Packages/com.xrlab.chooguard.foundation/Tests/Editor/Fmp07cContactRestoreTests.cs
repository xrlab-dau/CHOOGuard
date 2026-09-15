using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    /// <summary>
    /// FMP-07c (#127): contact, dense packing and stop on the declared connected-world geometry, plus the
    /// same-geometry-epoch restore invariants, pinned as EditMode regressions.
    ///
    /// FIXTURE PROVENANCE. Every dimension used here is read at test time from the declared profile
    /// foundation/world/connected-world-profile.json (13 regions, 12 portals, 3 frames, declared synthetic
    /// design). The bottleneck is the declared corridor underground_connector (99.6 m x 8.0 m, floor Y = -4,
    /// enclosure 3.2 m, contact plane X in [80, 179.6]) narrowing into the declared
    /// underground_connector--underground_shopping_passage portal (3.2 m clear, X in [179.6, 196]) and opening
    /// into underground_shopping_passage (28 m x 12 m, floor Y = -4). The declared
    /// rail_terminal_public--underground_connector ramp, gradient -10/24, is the support plane used by the
    /// restore and rollback tests. The wall list is derived from those declared region envelopes and portal
    /// clear widths; no coordinate is invented by this file.
    ///
    /// WORLD-ACTUAL PREDECESSOR INPUT. #127's declared predecessor input is the #125 (FMP-07a) candidate
    /// fixture foundation/tests/FMP-07a/world-crowd-fixture.json, and this file consumes it rather than
    /// assuming it is absent. DeclaredPredecessorFixtureIsConsumedAsWorldActualGeometryAndNotAsTheAbstractLaboratory
    /// reads it at test time, asserts its contract (FixtureKind world_actual_collider_nav, Body.RadiusM equal to
    /// WorldBodyPresentation.NpcRadiusM = 0.41 m, AbstractUnitRadiusM = 0.15 m kept separate, 13 region scenes,
    /// 12 portals, 3 frames, the frozen scene manifest hash, and the geometry-load contract), cross-checks its
    /// region scene manifest against this profile's own regions, and records the observed bytes and sha256.
    /// The radius that scenario drives is read from that fixture, never from the 0.15 m abstract unit radius.
    ///
    /// SCOPE BOUNDARY. The bottleneck WALLS here are the declared synthetic profile's region envelopes and
    /// portal clear widths, NOT a capture of the authored scene collider instances: ConnectedWorldGeometry.Capture
    /// runs against the 13 loaded region scenes and is the #125 (FMP-07a) path. What this file consumes from the
    /// world-actual fixture is its declared body radius, its region scene manifest and its geometry-load
    /// contract. The declared profile is the shared data anchor both paths consume. These tests fix the motion
    /// core's response to that declared geometry and are not facility, railway or field acceptance.
    ///
    /// NEGATIVE CONTRACT (must fail if ever relaxed): a same-epoch restore may never change pin or support
    /// binding, a failed tick may never be partially committed, and nonfinite / overlapping inputs may never
    /// be accepted.
    /// </summary>
    public sealed class Fmp07cContactRestoreTests
    {
        private const string ProfilePath = "foundation/world/connected-world-profile.json";
        private const string EvidenceDirectory = "Temp/ChooGuardCrowdBottleneck";
        private const string EditModeCommand = "-batchmode -nographics -projectPath . -runTests -testPlatform EditMode " +
            "-testFilter \"ChooGuard.Foundation.Tests.CrowdWorldContactTests;ChooGuard.Foundation.Tests.PhysicalCheckpointTests;" +
            "ChooGuard.Foundation.Tests.Fmp07cContactRestoreTests\" " +
            "-testResults \"$EVIDENCE_DIR/FMP-07c-edit.xml\" -logFile \"$EVIDENCE_DIR/FMP-07c-unity.log\"";

        // Frozen identity of the declared profile bytes. A profile edit must fail this anchor and force the
        // fixture, the recipe and the recorded measurements to be re-derived rather than silently shift.
        private const string DeclaredGeometryHash = "e2cae663ce04b63b197c99dda7a26a394619430e47cb071834254c3d23f94cba";

        // Declared topology anchors. A profile change must fail these loudly rather than shift the fixture silently.
        private const int FixedRegionCount = 13;
        private const int FixedPortalCount = 12;
        private const int FixedFrameCount = 3;
        private const string DeclaredProfileId = "native-connected-v1";
        private const string DeclaredClassification = "synthetic_design_not_facility_acceptance";
        private const string ConnectorRegionId = "underground_connector";
        private const string PassageRegionId = "underground_shopping_passage";
        private const string TerminalRegionId = "rail_terminal_public";
        private const string PlatformRegionId = "rail_platforms_mainline";
        private const string ConcourseRegionId = "station_concourse_2f";
        private const string MetroVehicleRegionId = "rolling_stock_metro";
        private const string ChokePortalId = "underground_connector--underground_shopping_passage";
        private const string RampFromRegionId = TerminalRegionId, RampToRegionId = ConnectorRegionId;
        private const string WorldSpaceId = "world";
        private const string TrainSpaceId = "train-metro";

        // Declared dimensions of the bottleneck, asserted in test 1 and then used as the fixture contract.
        private const double DeclaredConnectorLengthM = 99.6;
        private const double DeclaredConnectorWidthM = 8.0;
        private const double DeclaredConnectorFloorY = -4.0;
        private const double DeclaredConnectorWallHeightM = 3.2;
        private const double DeclaredChokeClearWidthM = 3.2;
        private const double DeclaredChokeClearHeightM = 2.7;
        private const double DeclaredChokeEntryX = 179.6;
        private const double DeclaredChokeExitX = 196.0;
        private const double DeclaredPassageLengthM = 28.0;
        private const double DeclaredPassageWidthM = 12.0;
        private const double DeclaredPassageCenterX = 210.0;
        private const double DeclaredPlatformWallHeightM = 1.15;
        private const double DeclaredPlatformFloorY = 2.0;
        private const double DeclaredConcourseFloorY = 6.0;
        private const double DeclaredRampStartX = 56.0, DeclaredRampStartY = 6.0;
        private const double DeclaredRampEndX = 80.0, DeclaredRampEndY = -4.0;

        // The profile stores float32, so a decimal literal such as 99.6 widens to 99.59999847412109. A millimetre
        // scale guard still fails on any real geometry edit while absorbing the storage format.
        private const double DeclaredToleranceM = 1e-4;
        private const double RatioTolerance = 1e-6;

        // The declared-profile bottleneck scenarios below drive the ABSTRACT laboratory unit body, radius 0.15 m.
        // That is exactly the counterpart radius the #125 fixture records separately under
        // AbstractCounterpart.RadiusM, and it is never presented as the authored body size. The world-actual
        // scenarios read 0.41 m out of that fixture instead. The two radii must never be interchanged.
        private const double NpcRadiusM = .15;
        private const double PlayerRadiusM = .3;
        private const double BodyHeightM = 1.8;
        private const double StepSeconds = .05;
        private const long RecipeSeed = 127;
        // A body is "in contact" when the centre clearance between two touching bodies is at most this. The
        // solver projects to exact complementarity, so asserted contact is a measured near-zero gap, not a
        // tuned stand-off. One millimetre is far below any authored geometry scale here.
        private const double ContactToleranceM = 1e-3;

        // ---------------------------------------------------------------- world-actual predecessor ----

        // #127's declared predecessor input: the #125 (FMP-07a) candidate fixture. It is a separate work item's
        // artifact, so this file consumes it as data and validates its declared contract at test time; it never
        // hard-codes the file's own sha256, because an unpinned predecessor must stay free to be re-derived and
        // a stale pin would only be a false anchor. The observed bytes and digest are recorded instead.
        private const string WorldCrowdFixturePath = "foundation/tests/FMP-07a/world-crowd-fixture.json";
        private const string WorldActualFixtureKind = "world_actual_collider_nav";
        private const string AbstractUnitFixtureKind = "abstract_unit_laboratory_corridor";
        private const int WorldActualRegionCount = 13;
        private const int WorldActualPortalCount = 12;
        private const int WorldActualFrameCount = 3;
        private const int WorldActualBodyCount = 100;
        private const double WorldActualAbstractUnitRadiusM = .15;
        private const double WorldActualHeightM = 1.8;
        private const int WorldActualMeasurementItemCount = 15;
        // The fixture's own frozen region-scene manifest hash, restated here so a manifest edit fails loudly.
        private const string WorldActualSceneManifestSha256 =
            "830d9bfadcaace0d768360ed5f43be3d8fbaa605ed7b34c1ad3cbfb9abed9541";

        // ---------------------------------------------------------------- declared fixture ----

        private sealed class Bottleneck
        {
            public ConnectedWorldDefinition Declared;
            public CrowdDefinition Definition;
            public double ConnectorMinX, ConnectorMaxX, ConnectorMinZ, ConnectorMaxZ, FloorY, WallTopY;
            public double ChokeEntryX, ChokeExitX, ChokeHalfWidthM, ChokeLengthM, ChokeClearWidthM;
            public double PassageMinX, PassageMaxX, PassageMinZ, PassageMaxZ, PassageCenterX;
            public double PlatformMinX, PlatformMaxX, PlatformMinZ, PlatformMaxZ, PlatformFloorY, PlatformRailTopY;
            public double RampStartX, RampEndX, RampGradientX;
            public string DeclaredHash, DefinitionHash;
        }

        private static Bottleneck Build()
        {
            var declared = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ProfilePath));
            declared.Validate();
            var connector = declared.Region(ConnectorRegionId);
            var passage = declared.Region(PassageRegionId);
            var platform = declared.Region(PlatformRegionId);
            var vehicle = declared.Region(MetroVehicleRegionId);
            var choke = declared.Portals.Single(p => p.Id == ChokePortalId);
            var ramp = declared.Portals.Single(p => p.From == RampFromRegionId && p.To == RampToRegionId);

            var fixture = new Bottleneck { Declared = declared, DeclaredHash = Sha256OfFile(ProfilePath) };
            fixture.ConnectorMinX = connector.Center.X - connector.SizeX / 2.0;
            fixture.ConnectorMaxX = connector.Center.X + connector.SizeX / 2.0;
            fixture.ConnectorMinZ = connector.Center.Z - connector.SizeZ / 2.0;
            fixture.ConnectorMaxZ = connector.Center.Z + connector.SizeZ / 2.0;
            fixture.FloorY = connector.Center.Y;
            fixture.WallTopY = connector.Center.Y + connector.WallHeight;
            fixture.ChokeEntryX = fixture.ConnectorMaxX;
            fixture.ChokeExitX = passage.Center.X - passage.SizeX / 2.0;
            fixture.ChokeClearWidthM = choke.ClearWidth;
            fixture.ChokeHalfWidthM = choke.ClearWidth / 2.0;
            fixture.ChokeLengthM = fixture.ChokeExitX - fixture.ChokeEntryX;
            fixture.PassageMinX = passage.Center.X - passage.SizeX / 2.0;
            fixture.PassageMaxX = passage.Center.X + passage.SizeX / 2.0;
            fixture.PassageMinZ = passage.Center.Z - passage.SizeZ / 2.0;
            fixture.PassageMaxZ = passage.Center.Z + passage.SizeZ / 2.0;
            fixture.PassageCenterX = passage.Center.X;
            fixture.PlatformMinX = platform.Center.X - platform.SizeX / 2.0;
            fixture.PlatformMaxX = platform.Center.X + platform.SizeX / 2.0;
            fixture.PlatformMinZ = platform.Center.Z - platform.SizeZ / 2.0;
            fixture.PlatformMaxZ = platform.Center.Z + platform.SizeZ / 2.0;
            fixture.PlatformFloorY = platform.Center.Y;
            fixture.PlatformRailTopY = platform.Center.Y + platform.WallHeight;
            fixture.RampStartX = ramp.FromPoint.X;
            fixture.RampEndX = ramp.ToPoint.X;
            fixture.RampGradientX = (ramp.ToPoint.Y - ramp.FromPoint.Y) / (ramp.ToPoint.X - ramp.FromPoint.X);

            var walls = new List<CrowdWall>();
            // Corridor enclosure, floor to enclosure top, on the declared axis-aligned side faces.
            walls.Add(Wall("connector-north", fixture.ConnectorMinX, fixture.ConnectorMaxZ, fixture.ConnectorMaxX, fixture.ConnectorMaxZ, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("connector-south", fixture.ConnectorMinX, fixture.ConnectorMinZ, fixture.ConnectorMaxX, fixture.ConnectorMinZ, fixture.FloorY, fixture.WallTopY));
            // Corridor west face: the declared 3.2 m ramp mouth centred on Z = 0 is left open.
            walls.Add(Wall("connector-west-south", fixture.ConnectorMinX, fixture.ConnectorMinZ, fixture.ConnectorMinX, -fixture.ChokeHalfWidthM, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("connector-west-north", fixture.ConnectorMinX, fixture.ChokeHalfWidthM, fixture.ConnectorMinX, fixture.ConnectorMaxZ, fixture.FloorY, fixture.WallTopY));
            // Corridor east face: the piers either side of the declared choke mouth.
            walls.Add(Wall("connector-east-south", fixture.ConnectorMaxX, fixture.ConnectorMinZ, fixture.ConnectorMaxX, -fixture.ChokeHalfWidthM, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("connector-east-north", fixture.ConnectorMaxX, fixture.ChokeHalfWidthM, fixture.ConnectorMaxX, fixture.ConnectorMaxZ, fixture.FloorY, fixture.WallTopY));
            // The declared choke itself: its two piers are the declared 3.2 m clear width apart.
            walls.Add(Wall("choke-south", fixture.ChokeEntryX, -fixture.ChokeHalfWidthM, fixture.ChokeExitX, -fixture.ChokeHalfWidthM, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("choke-north", fixture.ChokeEntryX, fixture.ChokeHalfWidthM, fixture.ChokeExitX, fixture.ChokeHalfWidthM, fixture.FloorY, fixture.WallTopY));
            // Shopping passage on the same declared floor, west opening aligned with the choke.
            walls.Add(Wall("passage-north", fixture.PassageMinX, fixture.PassageMaxZ, fixture.PassageMaxX, fixture.PassageMaxZ, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("passage-south", fixture.PassageMinX, fixture.PassageMinZ, fixture.PassageMaxX, fixture.PassageMinZ, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("passage-west-south", fixture.PassageMinX, fixture.PassageMinZ, fixture.PassageMinX, -fixture.ChokeHalfWidthM, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("passage-west-north", fixture.PassageMinX, fixture.ChokeHalfWidthM, fixture.PassageMinX, fixture.PassageMaxZ, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("passage-east-south", fixture.PassageMaxX, fixture.PassageMinZ, fixture.PassageMaxX, -fixture.ChokeHalfWidthM, fixture.FloorY, fixture.WallTopY));
            walls.Add(Wall("passage-east-north", fixture.PassageMaxX, fixture.ChokeHalfWidthM, fixture.PassageMaxX, fixture.PassageMaxZ, fixture.FloorY, fixture.WallTopY));
            // Declared platform railing: the profile's authored 1.15 m enclosure on rail_platforms_mainline.
            walls.Add(Wall("platform-railing-north", fixture.PlatformMinX, fixture.PlatformMaxZ, fixture.PlatformMaxX, fixture.PlatformMaxZ, fixture.PlatformFloorY, fixture.PlatformRailTopY));
            walls.Add(Wall("platform-railing-south", fixture.PlatformMinX, fixture.PlatformMinZ, fixture.PlatformMaxX, fixture.PlatformMinZ, fixture.PlatformFloorY, fixture.PlatformRailTopY));
            // Declared metro vehicle shell (42 m x 5 m), modelled as its own contact space in the train-metro frame.
            var halfLength = vehicle.SizeX / 2.0; var halfWidth = vehicle.SizeZ / 2.0;
            walls.Add(Wall("vehicle-north", -halfLength, halfWidth, halfLength, halfWidth, 0, vehicle.WallHeight, TrainSpaceId));
            walls.Add(Wall("vehicle-south", -halfLength, -halfWidth, halfLength, -halfWidth, 0, vehicle.WallHeight, TrainSpaceId));
            walls.Add(Wall("vehicle-end", halfLength, -halfWidth, halfLength, halfWidth, 0, vehicle.WallHeight, TrainSpaceId));

            fixture.Definition = new CrowdDefinition {
                ProfileId = "fmp07c-declared-bottleneck/" + declared.ProfileId,
                Parameters = new CrowdParameters { MaxAgents = 120 },
                Spaces = new[] { new CrowdSpace { Id = WorldSpaceId }, new CrowdSpace { Id = TrainSpaceId } },
                Walls = walls.ToArray() };
            fixture.DefinitionHash = Sha256OfDefinition(fixture.Definition);
            return fixture;
        }

        private static CrowdWall Wall(string id, double ax, double ay, double bx, double by, double bottom, double top, string space = WorldSpaceId) =>
            new CrowdWall { Id = id, ContactSpaceId = space, A = new CrowdVector(ax, ay), B = new CrowdVector(bx, by),
                Enabled = true, HasVerticalBounds = true, BottomElevationM = bottom, TopElevationM = top };

        private static CrowdAgent Npc(string id, double x, double z, double elevation, double vx = 0, double vz = 0,
            double speed = 1.2, double radius = NpcRadiusM)
        {
            var body = new CrowdAgent { Id = id, ContactSpaceId = WorldSpaceId, RegionId = ConnectorRegionId, FrameId = WorldSpaceId,
                SurfaceId = "connector-floor", RadiusM = radius, HeightM = BodyHeightM, PreferredSpeedMS = speed,
                Position = new CrowdVector(x, z), FootElevationM = elevation };
            if (vx != 0 || vz != 0) { body.IntentMode = CrowdIntentMode.DesiredVelocity; body.DesiredVelocity = new CrowdVector(vx, vz); }
            return body;
        }

        /// <summary>A body on the declared rail_terminal_public--underground_connector ramp: its elevation is the
        /// declared portal plane and its support gradient is the declared ramp gradient, so it is genuinely on
        /// that authored plane rather than merely labelled with it.</summary>
        private static CrowdAgent RampBody(string id, double x, double vx)
        {
            var body = Npc(id, x, 0, DeclaredRampStartY + (x - DeclaredRampStartX) / (DeclaredRampEndX - DeclaredRampStartX) * (DeclaredRampEndY - DeclaredRampStartY), vx);
            body.RegionId = TerminalRegionId; body.SurfaceId = "ramp-floor";
            body.SupportGradient = new CrowdVector((DeclaredRampEndY - DeclaredRampStartY) / (DeclaredRampEndX - DeclaredRampStartX), 0);
            return body;
        }

        /// <summary>A corridor body walking the declared corridor toward the declared choke mouth under the
        /// solver's own navigation rule (Goal mode), not a scripted velocity.</summary>
        private static CrowdAgent Queued(string id, double x, double z, double speed = 1.4)
        {
            var body = Npc(id, x, z, DeclaredConnectorFloorY, 0, 0, speed);
            body.Goal = new CrowdVector(400, z); body.IntentMode = CrowdIntentMode.Goal;
            return body;
        }

        private static CrowdAgent PinnedPlayer(string id, double x, double z, string regionId = PassageRegionId)
        {
            var body = Npc(id, x, z, DeclaredConnectorFloorY);
            body.RadiusM = PlayerRadiusM; body.Pinned = true; body.RegionId = regionId; body.SurfaceId = "passage-floor";
            return body;
        }

        /// <summary>A pinned body at an explicit radius. The world-actual scenarios pin bodies at the authored NPC
        /// radius rather than the declared player radius, so the blockage and the column share one body profile.</summary>
        private static CrowdAgent PinnedBody(string id, double x, double z, double radius, string regionId = PassageRegionId)
        {
            var body = Npc(id, x, z, DeclaredConnectorFloorY, 0, 0, 1.2, radius);
            body.Pinned = true; body.RegionId = regionId; body.SurfaceId = "passage-floor";
            return body;
        }

        /// <summary>A corridor body at an explicit radius walking the declared corridor toward the declared choke
        /// under the solver's own navigation rule (Goal mode), not a scripted velocity.</summary>
        private static CrowdAgent QueuedAtRadius(string id, double x, double z, double radius, double speed = 1.4)
        {
            var body = Npc(id, x, z, DeclaredConnectorFloorY, 0, 0, speed, radius);
            body.Goal = new CrowdVector(400, z); body.IntentMode = CrowdIntentMode.Goal;
            return body;
        }

        private static string Sha256OfFile(string path)
        { using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(File.ReadAllBytes(path))); }

        private static string Sha256OfDefinition(CrowdDefinition definition)
        { using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(definition)))); }

        private static string Hex(byte[] bytes)
        { var text = new StringBuilder(bytes.Length * 2); foreach (var b in bytes) text.Append(b.ToString("x2", CultureInfo.InvariantCulture)); return text.ToString(); }

        private static string ProjectRoot()
        {
            var parent = Directory.GetParent(Application.dataPath);
            return parent == null ? Directory.GetCurrentDirectory() : parent.FullName;
        }

        private static string WorldCrowdFixtureFullPath() => Path.Combine(ProjectRoot(), WorldCrowdFixturePath);

        // The #125 fixture stores PascalCase keys, matching the declared profile. Unity's JsonUtility matches
        // field names exactly and silently leaves every field at its default when a key is camelCase, so an
        // all-defaults read must fail loudly rather than read as "the fixture declares nothing".
        [Serializable] private sealed class WorldCrowdSourceProfile
        {
            public string Path, Sha256, ProfileId, Classification;
            public int SchemaVersion, GeometrySchemaVersion, Regions, Portals, Frames;
        }
        [Serializable] private sealed class WorldCrowdRegionScene { public string RegionId, SceneName, FrameId; }
        [Serializable] private sealed class WorldCrowdSceneHash
        {
            public string ManifestRule, ManifestSha256, ProfileSha256;
            public WorldCrowdRegionScene[] RegionScenes;
        }
        [Serializable] private sealed class WorldCrowdGeometryLoad
        {
            public int LoadedRegionScenes, DeclaredRegionScenes, MinimumCapturedSupports, MinimumCapturedColliders;
            public bool MustMatchDeclaredRegionSetExactly, EveryRegionMustContributeAtLeastOneSupport,
                EveryCapturedColliderMustBePresentEnabledAndUnchanged, NoColliderMayBeSilentlySkipped;
            public string MissingRegionPolicy, UnloadedColliderPolicy;
        }
        [Serializable] private sealed class WorldCrowdBody
        {
            public string Kind, RadiusConstant;
            public int Count, PlayerCount;
            public double RadiusM, HeightM, PreferredSpeedMS, AbstractUnitRadiusM, PlayerRadiusM;
        }
        [Serializable] private sealed class WorldCrowdPlacement
        {
            public string Method, InitialOverlapPolicy, PerRegionCountsNote, SlotSpacingNote;
            public int[] PerRegionCounts;
            public int RegionsCovered;
            public double SlotSpacingM;
        }
        [Serializable] private sealed class WorldCrowdAbstractCounterpart
        {
            public string FixtureKind, Rule, DeletionPolicy;
            public bool ActualGeometry, RecordedSeparately;
            public double RadiusM, HeightM, PersonRepulsion, RepulsionRangeM;
            public string[] Sources;
        }
        [Serializable] private sealed class WorldCrowdFixture
        {
            public int SchemaVersion;
            public string WorkId, Title, Classification, GeometricClassification, FixtureKind, ContractNature;
            public long Seed;
            public WorldCrowdSourceProfile SourceProfile;
            public WorldCrowdSceneHash SceneHash;
            public WorldCrowdGeometryLoad GeometryLoad;
            public WorldCrowdBody Body;
            public WorldCrowdPlacement Placement;
            public WorldCrowdAbstractCounterpart AbstractCounterpart;
            public string[] MeasurementItems;
        }

        /// <summary>Reads #127's declared predecessor input. A missing or unreadable file is reported, never
        /// silently skipped: the world-actual provenance of this work item depends on that input being present
        /// and actually consumed.</summary>
        private static WorldCrowdFixture LoadWorldCrowdFixture(out string failure)
        {
            failure = "";
            var path = WorldCrowdFixtureFullPath();
            if (!File.Exists(path))
            { failure = "Missing declared predecessor input " + WorldCrowdFixturePath + " (looked at " + path + ")."; return null; }
            try { return JsonUtility.FromJson<WorldCrowdFixture>(File.ReadAllText(path)); }
            catch (Exception error) { failure = "Unreadable predecessor input " + WorldCrowdFixturePath + ": " + error.Message; return null; }
        }

        // ---------------------------------------------------------------- measurement helpers ----

        /// <summary>Smallest centre clearance (centre distance minus both radii) over every pair that shares a
        /// contact space and whose vertical envelopes overlap. Recomputed from the exported snapshot, not read
        /// from the solver report, so a stale report cannot mask penetration. Every fixture body involved in
        /// these tests stands on one declared floor per contact space, so an overlapping pair here is a genuine
        /// three-dimensional overlap and not a vertical-window artefact.</summary>
        private static double MinimumCentreClearance(CrowdSnapshot state)
        {
            var minimum = double.PositiveInfinity;
            for (var i = 0; i < state.Agents.Length; i++)
            for (var j = i + 1; j < state.Agents.Length; j++)
            {
                var a = state.Agents[i]; var b = state.Agents[j];
                if (a.ContactSpaceId != b.ContactSpaceId) continue;
                if (a.FootElevationM + a.HeightM <= b.FootElevationM || b.FootElevationM + b.HeightM <= a.FootElevationM) continue;
                minimum = Math.Min(minimum, (a.Position - b.Position).Length - a.RadiusM - b.RadiusM);
            }
            return minimum;
        }

        /// <summary>Smallest clearance to any enabled wall in the same contact space whose vertical window the
        /// body's own envelope touches. Independent of the swept-geometry path used inside the solver.</summary>
        private static double MinimumWallClearance(CrowdSnapshot state)
        {
            var minimum = double.PositiveInfinity;
            foreach (var a in state.Agents)
            foreach (var wall in state.Definition.Walls)
            {
                if (!wall.Enabled || wall.ContactSpaceId != a.ContactSpaceId) continue;
                if (wall.HasVerticalBounds && !(a.FootElevationM + a.HeightM > wall.BottomElevationM && wall.TopElevationM > a.FootElevationM)) continue;
                minimum = Math.Min(minimum, PointSegmentDistance(a.Position, wall.A, wall.B) - a.RadiusM);
            }
            return minimum;
        }

        private static double PointSegmentDistance(CrowdVector p, CrowdVector a, CrowdVector b)
        {
            var delta = b - a; var lengthSquared = delta.LengthSquared;
            var t = lengthSquared > 0 ? Math.Max(0, Math.Min(1, CrowdVector.Dot(p - a, delta) / lengthSquared)) : 0;
            return (p - (a + delta * t)).Length;
        }

        private static int CountInsideChoke(CrowdSnapshot state, Bottleneck fixture) => state.Agents.Count(a =>
            a.ContactSpaceId == WorldSpaceId && a.Position.X >= fixture.ChokeEntryX && a.Position.X <= fixture.ChokeExitX);

        private static CrowdSnapshot StepCommitted(CrowdMotionModel model, int ticks, double seconds, out List<CrowdStepReport> reports)
        {
            reports = new List<CrowdStepReport>();
            for (var i = 0; i < ticks; i++)
            {
                Assert.That(model.TryAdvance(seconds, null, out var report), Is.True, report.Failure);
                Assert.That(report.Committed, Is.True, "A committed tick must report Committed.");
                reports.Add(report);
            }
            return model.ExportSnapshot();
        }

        private static void AssertNoPenetration(CrowdSnapshot state, string context)
        {
            Assert.That(MinimumCentreClearance(state), Is.GreaterThanOrEqualTo(-1e-9),
                context + ": two bodies interpenetrate beyond the declared geometry tolerance.");
            Assert.That(MinimumWallClearance(state), Is.GreaterThanOrEqualTo(-1e-9),
                context + ": a body interpenetrates an enabled wall beyond the declared geometry tolerance.");
        }

        /// <summary>Steps the model and re-derives non-penetration from the EXPORTED SNAPSHOT on every single
        /// tick, so a transient overlap between two committed ticks cannot hide behind an end-of-window check.</summary>
        private static CrowdSnapshot StepCommittedChecked(CrowdMotionModel model, int ticks, double seconds, string context,
            out List<CrowdStepReport> reports)
        {
            reports = new List<CrowdStepReport>();
            for (var i = 0; i < ticks; i++)
            {
                Assert.That(model.TryAdvance(seconds, null, out var report), Is.True, context + " tick " + i + ": " + report.Failure);
                Assert.That(report.Committed, Is.True, context + " tick " + i + ": a committed tick must report Committed.");
                AssertNoPenetration(model.ExportSnapshot(), context + " tick " + i);
                reports.Add(report);
            }
            return model.ExportSnapshot();
        }

        /// <summary>A contact-only variant of the declared definition: the declared soft person-repulsion field is
        /// switched off so the CSM time-gap speed cap is the only thing separating two bodies. The declared field
        /// holds an approaching body at a soft stand-off (measured 0.016974 m at the 0.41 m world-actual radius
        /// under the shipped PersonRepulsion 5 / range 0.1), which is a real property of the declared model but is
        /// NOT contact. That 0.41 m column is the only configuration whose declared-field stand-off this file
        /// measures: no stand-off was measured at the 0.15 m laboratory radius, so none is stated here. With the
        /// field on, the exponential term
        /// `PersonRepulsion * exp(-clearance / range)` outweighs the unit goal direction once the clearance falls
        /// below `range * ln(PersonRepulsion)` = 0.161 m and the body is pushed back out, so a touching
        /// configuration is unreachable in finite time. Zeroing the coefficient (PersonRepulsion = 0, which the
        /// contract allows because it requires only Nonnegative) is how this file reaches a genuinely touching
        /// configuration; the stand-off under the declared field is recorded as its own measured fact by
        /// DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact.</summary>
        private static CrowdDefinition ContactOnly(CrowdDefinition declared)
        {
            var definition = declared.Copy();
            // The range must stay strictly positive: CrowdMotionModel.Validate requires Positive(PersonRepulsionRangeM).
            definition.Parameters.PersonRepulsion = 0;
            return definition;
        }

        /// <summary>Smallest centre clearance between an unpinned body and a pinned body that share a contact
        /// space and whose vertical envelopes overlap. Recomputed from the exported snapshot, never read from the
        /// solver report.</summary>
        private static double MinimumClearanceToPinned(CrowdSnapshot state)
        {
            var minimum = double.PositiveInfinity;
            foreach (var free in state.Agents.Where(a => !a.Pinned))
            foreach (var pinned in state.Agents.Where(a => a.Pinned))
            {
                if (free.ContactSpaceId != pinned.ContactSpaceId) continue;
                if (free.FootElevationM + free.HeightM <= pinned.FootElevationM ||
                    pinned.FootElevationM + pinned.HeightM <= free.FootElevationM) continue;
                minimum = Math.Min(minimum, (free.Position - pinned.Position).Length - free.RadiusM - pinned.RadiusM);
            }
            return minimum;
        }

        /// <summary>Contact is asserted, not presumed: this is the positive dual of AssertNoPenetration. It fails
        /// when a scenario never actually reaches contact, so a test may only claim "contact" if the measured
        /// closest centre-to-pin gap is within ContactToleranceM of a touching configuration.</summary>
        private static void AssertContactWithAPinnedBody(CrowdSnapshot state, string context)
        {
            var gap = MinimumClearanceToPinned(state);
            Assert.That(gap, Is.LessThanOrEqualTo(ContactToleranceM),
                context + ": no body ever reached contact with a pinned body; the closest centre-to-pin clearance " +
                gap + " m exceeds the " + ContactToleranceM + " m contact tolerance, so this scenario is not a contact case.");
        }

        private static void AssertCommittedReports(List<CrowdStepReport> reports, string context)
        {
            Assert.That(reports.All(r => r.Committed), Is.True, context + ": every tick in this window must commit.");
            Assert.That(reports.Max(r => r.MaximumConstraintViolationMS), Is.LessThanOrEqualTo(1e-8),
                context + ": projection residual left a constraint violation beyond the solver tolerance.");
            Assert.That(reports.Min(r => r.MinimumSweptGapM), Is.GreaterThanOrEqualTo(-1e-9),
                context + ": the a posteriori swept certificate reported penetration.");
        }

        private static string WriteReceipt(string name, object receipt)
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var directory = Path.Combine(root, EvidenceDirectory); Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name);
            var json = JsonUtility.ToJson(receipt, true);
            File.WriteAllText(path, json);
            // Temp/ is removed when the editor exits, so every recorded number also travels in the run log.
            Debug.Log("FMP07C-RECEIPT " + name + " " + JsonUtility.ToJson(receipt));
            return path;
        }

        private static string[] Notes(params string[] lines) => lines;

        [Serializable] private sealed class RecipeReceipt
        {
            public string WorkId = "FMP-07c", ProfilePath = Fmp07cContactRestoreTests.ProfilePath, Command = EditModeCommand;
            public string DeclaredGeometryHash, FixtureGeometryHash;
            public int DeclaredRegionCount, DeclaredPortalCount, DeclaredFrameCount;
            public string ConnectorRegionId, ChokePortalId, PassageRegionId, PlatformRegionId, MetroVehicleRegionId;
            public double ConnectorLengthM, ConnectorWidthM, ChokeClearWidthM, ChokeLengthM, PassageLengthM, PassageWidthM;
            public double FloorElevationM, StepSeconds, SimulatedSeconds;
            public int BodyCount, TickCount, NpcRadiusMm;
            public long Seed;
            public int BodiesPastChokeMouth, BodiesPastChokeExit, PeakChokeOccupancy;
            public double PeakChokeDensityPM2, FinalChokeMeanSpeedMS, MinimumCentreClearanceM, MinimumWallClearanceM;
            public double MaximumConstraintViolationMS, MaximumGroundSpeedMS, MinimumSweptGapM;
            public string[] Notes;
        }

        [Serializable] private sealed class DensitySpeedReceipt
        {
            public string WorkId = "FMP-07c", Command = EditModeCommand, DeclaredGeometryHash;
            public double FreeBodyMeanSpeedMS, JammedChokeMeanSpeedMS, SpeedRatio;
            public int JammedBodiesInChoke, BlockingPlayerCount, BodiesInChoke;
            public double BlockingRowX, SimulatedSeconds;
            public double MinimumCentreClearanceM, MinimumWallClearanceM, ChokeClearWidthM;
            public double MinimumClearanceToPinnedM, ContactToleranceM;
            public bool ContactAsserted;
            public string[] Notes;
        }

        /// <summary>Receipt for the same-epoch restore refusals. It deliberately carries no atomicity field, so an
        /// unset default cannot be misread as a measurement of the failed-tick claim.</summary>
        [Serializable] private sealed class RestoreRejectionReceipt
        {
            public string WorkId = "FMP-07c", Command = EditModeCommand, DeclaredGeometryHash;
            public int SameEpochRejections, RejectionReasonsRecorded;
            public string[] RejectedChanges = Array.Empty<string>();
            public bool CheckpointByteIdenticalAcrossAllRejections;
            public long GeometryRevisionBefore, GeometryRevisionAfter;
            public string[] Notes;
        }

        /// <summary>Receipt for the refused-tick case. It carries its own tick decomposition, so the atomicity
        /// claim is self-describing, and no restore-rejection counter that was never measured.</summary>
        [Serializable] private sealed class AtomicityReceipt
        {
            public string WorkId = "FMP-07c", Command = EditModeCommand, DeclaredGeometryHash;
            public double TickSeconds, MaximumSubstepSeconds;
            public int SubstepsPerTick;
            public bool FailedTickCommitted;
            public int FailedTickAttemptedSubsteps;
            public string FailedTickReason = "";
            public bool StateByteIdenticalAfterFailure, RecoveryMatchesUntouchedTwin;
            public string[] Notes;
        }

        [Serializable] private sealed class StepSensitivityReceipt
        {
            public string WorkId = "FMP-07c", Command = EditModeCommand, DeclaredGeometryHash;
            public double[] DeclaredSubstepSeconds = Array.Empty<double>();
            public int[] BodiesPastChokeMouth = Array.Empty<int>();
            public double[] MaximumX = Array.Empty<double>();
            public double MaximumPositionSpreadM, SimulatedSeconds;
            public int BodyCount, TickCountPerRun;
            public string[] Notes;
        }

        /// <summary>Receipt for the consumed #125 (FMP-07a) predecessor fixture: which bytes this run read, what
        /// that file declares, and what was cross-checked against this profile. The predecessor is another work
        /// item's artifact, so its observed digest travels here rather than as a constant in this file.</summary>
        [Serializable] private sealed class WorldActualFixtureReceipt
        {
            public string WorkId = "FMP-07c", Command = EditModeCommand, DeclaredGeometryHash;
            public string PredecessorRelativePath, PredecessorSha256, PredecessorFixtureKind, PredecessorWorkId;
            public long PredecessorBytes;
            public string RadiusConstant = "WorldBodyPresentation.NpcRadiusM";
            public double RadiusConstantValue, WorldActualRadiusM, AbstractUnitRadiusM;
            public int DeclaredRegionScenes, DeclaredPortals, DeclaredFrames, DeclaredBodyCount, RegionsCovered;
            public string SceneManifestSha256, SourceProfileSha256;
            public bool GeometryLoadContractHolds, RegionManifestMatchesThisProfile, AbstractKindIsSeparate;
            public string[] Notes;
        }

        /// <summary>Receipt for the world-actual contact/stop scenario.</summary>
        [Serializable] private sealed class WorldActualContactReceipt
        {
            public string WorkId = "FMP-07c", Command = EditModeCommand, DeclaredGeometryHash;
            public string WorldActualRadiusSource = Fmp07cContactRestoreTests.WorldCrowdFixturePath;
            public double BodyRadiusM, SealSpacingM, PierGapM, RequiredPassageWidthM;
            public double BlockingRowX, ChokeClearWidthM, SimulatedSeconds, StepSeconds;
            public int SealBodyCount, ColumnBodyCount, TickCount;
            public int PeakBodiesInsideChoke, FinalBodiesInsideChoke;
            public double MinimumCentreClearanceM, MinimumWallClearanceM, MinimumClearanceToPinnedM;
            public double MaximumUnpinnedXM, FreeRowX;
            public bool ContactAsserted, NoBodyPassedTheSeal;
            public string[] Notes;
        }

        // ---------------------------------------------------------------- 1. declared anchor ----

        [Test]
        public void DeclaredProfileStillCarriesTheThirteenRegionTwelvePortalDeclaredChoke()
        {
            var fixture = Build();
            Assert.That(fixture.Declared.Regions.Length, Is.EqualTo(FixedRegionCount));
            Assert.That(fixture.Declared.Portals.Length, Is.EqualTo(FixedPortalCount));
            Assert.That(fixture.Declared.Frames.Length, Is.EqualTo(FixedFrameCount));
            Assert.That(fixture.Declared.ProfileId, Is.EqualTo(DeclaredProfileId));
            Assert.That(fixture.Declared.Classification, Is.EqualTo(DeclaredClassification));
            Assert.That(fixture.DeclaredHash, Is.EqualTo(DeclaredGeometryHash),
                "The declared profile bytes changed; the FMP-07c fixture and its recorded measurements must be re-derived.");

            var connector = fixture.Declared.Region(ConnectorRegionId);
            var passage = fixture.Declared.Region(PassageRegionId);
            var platform = fixture.Declared.Region(PlatformRegionId);
            var concourse = fixture.Declared.Region(ConcourseRegionId);
            var choke = fixture.Declared.Portals.Single(p => p.Id == ChokePortalId);
            var ramp = fixture.Declared.Portals.Single(p => p.From == RampFromRegionId && p.To == RampToRegionId);

            Assert.That((double)connector.SizeX, Is.EqualTo(DeclaredConnectorLengthM).Within(DeclaredToleranceM));
            Assert.That((double)connector.SizeZ, Is.EqualTo(DeclaredConnectorWidthM).Within(DeclaredToleranceM));
            Assert.That((double)connector.Center.Y, Is.EqualTo(DeclaredConnectorFloorY).Within(DeclaredToleranceM));
            Assert.That((double)connector.WallHeight, Is.EqualTo(DeclaredConnectorWallHeightM).Within(DeclaredToleranceM));
            Assert.That((double)choke.ClearWidth, Is.EqualTo(DeclaredChokeClearWidthM).Within(DeclaredToleranceM));
            Assert.That((double)choke.ClearHeight, Is.EqualTo(DeclaredChokeClearHeightM).Within(DeclaredToleranceM));
            Assert.That((double)choke.FromPoint.X, Is.EqualTo(DeclaredChokeEntryX).Within(DeclaredToleranceM));
            Assert.That((double)choke.ToPoint.X, Is.EqualTo(DeclaredChokeExitX).Within(DeclaredToleranceM));
            Assert.That((double)passage.SizeX, Is.EqualTo(DeclaredPassageLengthM).Within(DeclaredToleranceM));
            Assert.That((double)passage.SizeZ, Is.EqualTo(DeclaredPassageWidthM).Within(DeclaredToleranceM));
            Assert.That((double)passage.Center.X, Is.EqualTo(DeclaredPassageCenterX).Within(DeclaredToleranceM));
            Assert.That((double)platform.WallHeight, Is.EqualTo(DeclaredPlatformWallHeightM).Within(DeclaredToleranceM));
            Assert.That((double)platform.Center.Y, Is.EqualTo(DeclaredPlatformFloorY).Within(DeclaredToleranceM));
            Assert.That((double)concourse.Center.Y, Is.EqualTo(DeclaredConcourseFloorY).Within(DeclaredToleranceM));
            Assert.That((double)ramp.FromPoint.X, Is.EqualTo(DeclaredRampStartX).Within(DeclaredToleranceM));
            Assert.That((double)ramp.ToPoint.X, Is.EqualTo(DeclaredRampEndX).Within(DeclaredToleranceM));
            Assert.That((double)ramp.FromPoint.Y, Is.EqualTo(DeclaredRampStartY).Within(DeclaredToleranceM));

            // The fixture's own derived values agree with the raw authored fields, so a later assertion on the
            // derived value is an assertion on the profile and not on this file's arithmetic.
            Assert.That(fixture.ConnectorMaxX - fixture.ConnectorMinX, Is.EqualTo(DeclaredConnectorLengthM).Within(DeclaredToleranceM));
            Assert.That(fixture.ConnectorMaxZ - fixture.ConnectorMinZ, Is.EqualTo(DeclaredConnectorWidthM).Within(DeclaredToleranceM));
            Assert.That(fixture.FloorY, Is.EqualTo(DeclaredConnectorFloorY).Within(DeclaredToleranceM));
            Assert.That(fixture.WallTopY - fixture.FloorY, Is.EqualTo(DeclaredConnectorWallHeightM).Within(DeclaredToleranceM));
            Assert.That(fixture.ChokeEntryX, Is.EqualTo((double)choke.FromPoint.X).Within(DeclaredToleranceM));
            Assert.That(fixture.ChokeExitX, Is.EqualTo((double)choke.ToPoint.X).Within(DeclaredToleranceM));
            Assert.That(fixture.ChokeClearWidthM, Is.EqualTo(DeclaredChokeClearWidthM).Within(DeclaredToleranceM));
            Assert.That(fixture.PassageMaxX - fixture.PassageMinX, Is.EqualTo(DeclaredPassageLengthM).Within(DeclaredToleranceM));
            Assert.That(fixture.PassageMaxZ - fixture.PassageMinZ, Is.EqualTo(DeclaredPassageWidthM).Within(DeclaredToleranceM));
            Assert.That(fixture.PlatformRailTopY - fixture.PlatformFloorY, Is.EqualTo(DeclaredPlatformWallHeightM).Within(DeclaredToleranceM));

            // The declared ramp slope, asserted as the ratio the profile states: a 10 m drop over a 24 m run.
            Assert.That(fixture.RampGradientX, Is.EqualTo((DeclaredRampEndY - DeclaredRampStartY) / (DeclaredRampEndX - DeclaredRampStartX)).Within(RatioTolerance));
        }

        [Test]
        public void FixtureWallsAreDerivedFromTheDeclaredRegionsAndReproduceTheDeclaredClearWidth()
        {
            var fixture = Build();
            // The choke piers are the declared clear width apart, measured from the emitted definition rather
            // than from the constant they were built from.
            var chokeNorth = fixture.Definition.Walls.Single(w => w.Id == "choke-north");
            var chokeSouth = fixture.Definition.Walls.Single(w => w.Id == "choke-south");
            Assert.That(chokeNorth.A.Y, Is.EqualTo(fixture.ChokeHalfWidthM));
            Assert.That(chokeSouth.A.Y, Is.EqualTo(-fixture.ChokeHalfWidthM));
            Assert.That(chokeNorth.A.Y - chokeSouth.A.Y, Is.EqualTo(DeclaredChokeClearWidthM).Within(DeclaredToleranceM));
            Assert.That(chokeNorth.A.X, Is.EqualTo(fixture.ConnectorMaxX));
            Assert.That(chokeNorth.B.X, Is.EqualTo(fixture.PassageMinX));

            // Every world-frame wall vertex must lie inside a declared region envelope: nothing is invented.
            var declared = fixture.Declared.Regions.Where(r => r.FrameId == WorldSpaceId).ToArray();
            foreach (var wall in fixture.Definition.Walls.Where(w => w.ContactSpaceId == WorldSpaceId))
            {
                foreach (var point in new[] { wall.A, wall.B })
                {
                    var inside = declared.Any(r => point.X >= r.Center.X - r.SizeX / 2.0 - 1e-3 && point.X <= r.Center.X + r.SizeX / 2.0 + 1e-3 &&
                                                   point.Y >= r.Center.Z - r.SizeZ / 2.0 - 1e-3 && point.Y <= r.Center.Z + r.SizeZ / 2.0 + 1e-3);
                    Assert.That(inside, Is.True, wall.Id + " leaves every declared world-frame envelope at (" + point.X + "," + point.Y + ")");
                }
                Assert.That(wall.HasVerticalBounds, Is.True, "Declared enclosure walls must carry an authored vertical window.");
                Assert.That(wall.BottomElevationM, Is.GreaterThanOrEqualTo(fixture.FloorY - DeclaredToleranceM),
                    wall.Id + " starts below the declared floor of its own enclosure.");
            }

            // The definition is reproducible: a second derivation gives the same geometry hash.
            Assert.That(Build().DefinitionHash, Is.EqualTo(fixture.DefinitionHash),
                "The declared fixture must be derived deterministically from the profile.");
        }

        // ---------------------------------------------------------------- 2. contact / density ----

        [Test]
        public void DenseColumnEnteringTheDeclaredChokePassesWithoutPenetration()
        {
            // Named for what it asserts. This scenario places one body radius of clearance between lanes
            // (0.8 m lanes, 0.30 m bodies) and lets the column pass the 3.2 m choke without lateral
            // compression, so it does NOT claim contact; the contact claim lives in the jam and world-actual
            // scenarios, where a pinned blockage actually drives bodies into each other.
            var fixture = Build();
            var bodies = new List<CrowdAgent>();
            for (var lane = 0; lane < 4; lane++)
            for (var column = 0; column < 5; column++)
                bodies.Add(Npc("npc" + (lane * 5 + column).ToString("D2"), 170 + column * .32, -1.2 + lane * .8, fixture.FloorY, 1.5, 0));
            var model = new CrowdMotionModel(fixture.Definition, bodies.ToArray(), RecipeSeed);
            var snapshot = StepCommittedChecked(model, 200, StepSeconds, "dense column", out var reports);

            AssertNoPenetration(snapshot, "dense column after 10.0 simulated seconds");
            AssertCommittedReports(reports, "dense column");
            Assert.That(snapshot.Agents.Count(a => a.Position.X > fixture.ChokeEntryX), Is.GreaterThanOrEqualTo(4),
                "The commanded column must actually reach and enter the declared choke; measured " +
                snapshot.Agents.Count(a => a.Position.X > fixture.ChokeEntryX) + " of " + snapshot.Agents.Length);

            // Every body between the declared mouth and the declared exit is inside the declared 3.2 m clear width.
            foreach (var body in snapshot.Agents.Where(a => a.Position.X > fixture.ChokeEntryX + 1e-6 && a.Position.X < fixture.ChokeExitX - 1e-6))
                Assert.That(Math.Abs(body.Position.Y), Is.LessThanOrEqualTo(fixture.ChokeHalfWidthM - NpcRadiusM + 1e-6),
                    body.Id + " occupies a Z outside the declared choke clear width.");
        }

        [Test]
        public void LocalDensityIsCappedByTheHardDiscBoundAndCongestionActuallyForms()
        {
            var fixture = Build();
            var bodies = new List<CrowdAgent>();
            for (var lane = 0; lane < 4; lane++)
            for (var column = 0; column < 6; column++)
                bodies.Add(Npc("npc" + (lane * 6 + column).ToString("D2"), 172 + column * .32, -1.2 + lane * .8, fixture.FloorY, 1.2, 0));
            var model = new CrowdMotionModel(fixture.Definition, bodies.ToArray(), RecipeSeed);

            var peakOccupancy = 0; var peakDensity = 0.0; var minimumClearance = double.PositiveInfinity;
            for (var tick = 0; tick < 200; tick++)
            {
                Assert.That(model.TryAdvance(StepSeconds, null, out var report), Is.True, report.Failure);
                var state = model.ExportSnapshot();
                peakOccupancy = Math.Max(peakOccupancy, CountInsideChoke(state, fixture));
                peakDensity = Math.Max(peakDensity, CountInsideChoke(state, fixture) / (fixture.ChokeLengthM * fixture.ChokeClearWidthM));
                minimumClearance = Math.Min(minimumClearance, MinimumCentreClearance(state));
            }

            Assert.That(peakOccupancy, Is.GreaterThanOrEqualTo(6),
                "Congestion must actually form inside the declared choke; a sparse pass cannot certify the density cap. Measured peak " + peakOccupancy);
            // The tight cap is the hard-disc separation itself, checked at every tick rather than at the mean:
            // the areal average over the whole 16.4 m choke is far below the packing limit and would not catch
            // a radius or contact defect on its own.
            Assert.That(minimumClearance, Is.GreaterThanOrEqualTo(-1e-9),
                "Density is capped by non-penetration, not by a tuned constant. Minimum centre clearance " + minimumClearance);
            // An informational ceiling: non-penetrating equal discs cannot exceed the hexagonal closest packing.
            Assert.That(peakDensity, Is.LessThanOrEqualTo(1.0 / (2 * Math.Sqrt(3) * NpcRadiusM * NpcRadiusM)),
                "Measured choke density " + peakDensity + " exceeds the geometric packing bound, so radii or contact are wrong.");
        }

        [Test]
        public void AJammedDeclaredChokeRunsSlowerThanTheSameCorridorAtFreeSpeed()
        {
            var fixture = Build();
            // Both sides of the comparison use the same contact-only definition, so the free/jammed contrast is
            // measured under one model rather than across two.
            var contactDefinition = ContactOnly(fixture.Definition);
            var free = new CrowdMotionModel(contactDefinition, new[] { Queued("npc00", 150, 0) }, RecipeSeed);
            var freeSnapshot = StepCommitted(free, 100, StepSeconds, out _);
            var freeSpeed = freeSnapshot.Agents.Single().Velocity.Length;

            // A stalled group standing inside the declared choke: five pinned bodies at the declared player
            // radius span the declared 3.2 m clear width (|Z| <= 1.2 with the 0.3 m declared body radius closes
            // the 1.6 m half width to a 0.1 m slit), so the choke is blocked by people rather than by invented geometry.
            var blockingRowX = fixture.ChokeEntryX + (fixture.ChokeExitX - fixture.ChokeEntryX) / 3;
            var bodies = new List<CrowdAgent>();
            for (var i = 0; i < 5; i++) bodies.Add(PinnedPlayer("stalled" + i.ToString("D2"), blockingRowX, -1.2 + i * .6));
            for (var lane = 0; lane < 4; lane++)
            for (var column = 0; column < 6; column++)
                bodies.Add(Queued("npc" + (lane * 6 + column).ToString("D2"), 176 + column * .32, -1.2 + lane * .8));
            var model = new CrowdMotionModel(contactDefinition, bodies.ToArray(), RecipeSeed);
            var snapshot = StepCommittedChecked(model, 240, StepSeconds, "stalled choke queue", out var reports);

            var inside = snapshot.Agents.Where(a => a.ContactSpaceId == WorldSpaceId &&
                a.Position.X >= fixture.ChokeEntryX && a.Position.X <= fixture.ChokeExitX).ToArray();
            var jammed = inside.Where(a => !a.Pinned).Select(a => a.Velocity.Length).DefaultIfEmpty(0).Average();

            AssertNoPenetration(snapshot, "stalled choke queue");
            AssertCommittedReports(reports, "stalled choke queue");
            // Contact is asserted here, not presumed by the test name: the queue must actually be pressed against
            // the pinned row. This is the positive dual of the non-penetration assertion above, and it is measured
            // under the contact-only variant, because under the declared soft field the same queue settles at the
            // stand-off that DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact measures.
            AssertContactWithAPinnedBody(snapshot, "stalled declared choke queue against the pinned row");
            Assert.That(MinimumClearanceToPinned(snapshot), Is.LessThanOrEqualTo(ContactToleranceM));
            Assert.That(freeSpeed, Is.GreaterThan(0), "A lone body on the declared corridor must make way.");
            Assert.That(inside.Length, Is.GreaterThanOrEqualTo(6), "The queue must actually stand inside the declared choke.");
            Assert.That(jammed, Is.LessThan(.5 * freeSpeed),
                "A blocked declared choke must run far slower than the free corridor; measured free=" + freeSpeed + " jammed=" + jammed);
            Assert.That(snapshot.Agents.Where(a => !a.Pinned).Max(a => a.Position.X), Is.LessThan(blockingRowX),
                "No body may pass the stalled group standing inside the declared choke.");

            var receipt = new DensitySpeedReceipt {
                DeclaredGeometryHash = fixture.DeclaredHash,
                FreeBodyMeanSpeedMS = freeSpeed, JammedChokeMeanSpeedMS = jammed,
                SpeedRatio = freeSpeed > 0 ? jammed / freeSpeed : 0,
                JammedBodiesInChoke = inside.Count(a => !a.Pinned), BlockingPlayerCount = 5, BodiesInChoke = inside.Length,
                BlockingRowX = blockingRowX, SimulatedSeconds = 240 * StepSeconds,
                MinimumCentreClearanceM = MinimumCentreClearance(snapshot), MinimumWallClearanceM = MinimumWallClearance(snapshot),
                ChokeClearWidthM = fixture.ChokeClearWidthM,
                MinimumClearanceToPinnedM = MinimumClearanceToPinned(snapshot), ContactToleranceM = ContactToleranceM,
                ContactAsserted = MinimumClearanceToPinned(snapshot) <= ContactToleranceM,
                Notes = Notes(
                    "FreeBodyMeanSpeedMS is the measured ground speed of one declared corridor walker under the solver's own Goal rule.",
                    "JammedChokeMeanSpeedMS is the measured mean ground speed of every unpinned body standing between the declared choke mouth and exit.",
                    "MinimumClearanceToPinnedM is the measured closest centre-to-pin clearance; ContactAsserted requires it within ContactToleranceM, so this is a real contact case and not only a non-penetration case. Both free and jammed legs use the contact-only variant (PersonRepulsion zeroed, range untouched); the declared field's stand-off for the same seal is measured separately by DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact.",
                    "Non-penetration is re-derived from the exported snapshot on every tick, not only at the end of the window.",
                    "The blockage is a row of pinned bodies at the declared player radius, not a closed portal or invented wall.",
                    "Declared synthetic profile geometry; no facility or field validation is claimed.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-density-speed.json", receipt)), Is.True);
        }

        // ---------------------------------------------------------------- 3. wall / railing stop ----

        [Test]
        public void WallAndRailingStopBodiesOnTheDeclaredEnvelopeOnly()
        {
            var fixture = Build();
            var sideWall = Npc("side", 120, 3.0, fixture.FloorY, 0, 1);
            var railing = Npc("rail", (fixture.PlatformMinX + fixture.PlatformMaxX) / 2, 3.0, fixture.PlatformFloorY, 0, 1);
            railing.RegionId = PlatformRegionId; railing.SurfaceId = "platform-floor";
            var model = new CrowdMotionModel(fixture.Definition, new[] { sideWall, railing }, RecipeSeed);
            var snapshot = StepCommitted(model, 120, StepSeconds, out var reports);

            var stoppedSide = snapshot.Agents.Single(a => a.Id == "side");
            var stoppedRail = snapshot.Agents.Single(a => a.Id == "rail");
            // Exact contact, not an overshoot and not a tuned stand-off: the projection lands the centre at radius.
            Assert.That(stoppedSide.Position.Y, Is.EqualTo(fixture.ConnectorMaxZ - NpcRadiusM).Within(1e-6));
            Assert.That(stoppedRail.Position.Y, Is.EqualTo(fixture.PlatformMaxZ - NpcRadiusM).Within(1e-6));
            Assert.That(stoppedRail.FootElevationM, Is.EqualTo(fixture.PlatformFloorY).Within(1e-12));
            Assert.That(stoppedRail.Position.Y, Is.GreaterThan(fixture.ConnectorMaxZ),
                "The platform railing must stop the body on the platform envelope, not on the corridor envelope below it.");
            AssertNoPenetration(snapshot, "wall and railing stop");
            AssertCommittedReports(reports, "wall and railing stop");
        }

        [Test]
        public void PinnedBodiesHoldUnderContactFromTheDeclaredCorridorColumn()
        {
            var fixture = Build();
            // A declared player standing in the corridor: contact may not move it, whatever the column behind wants.
            var pinned = PinnedPlayer("player", 174, 0, ConnectorRegionId);
            var bodies = new List<CrowdAgent> { pinned };
            for (var lane = 0; lane < 4; lane++)
            for (var column = 0; column < 4; column++)
                bodies.Add(Npc("npc" + (lane * 4 + column).ToString("D2"), 168 + column * .32, -.6 + lane * .4, fixture.FloorY, 1.5, 0));
            var model = new CrowdMotionModel(fixture.Definition, bodies.ToArray(), RecipeSeed);
            var snapshot = StepCommitted(model, 200, StepSeconds, out var reports);

            var held = snapshot.Agents.Single(a => a.Id == "player");
            Assert.That(held.Position.X, Is.EqualTo(174.0), "A pinned body is an exact constraint and cannot be pushed by a touching body.");
            Assert.That(held.Position.Y, Is.EqualTo(0.0));
            Assert.That(held.Velocity.Length, Is.Zero, "A pinned body carries no velocity.");
            Assert.That(held.FootElevationM, Is.EqualTo(fixture.FloorY));
            AssertNoPenetration(snapshot, "pinned player under contact");
            AssertCommittedReports(reports, "pinned player under contact");
        }

        // ---------------------------------------------------------------- 4. same-epoch restore ----

        [Test]
        public void SameEpochRestoreRefusesToChangePinOrSupportBinding()
        {
            var fixture = Build();
            var ramp = RampBody("ramp", 70, 1);
            var walker = Npc("walker", 120, 0, fixture.FloorY, .5, 0);
            var model = new CrowdMotionModel(fixture.Definition, new[] { ramp, walker }, RecipeSeed);
            StepCommitted(model, 4, StepSeconds, out _);
            var before = model.ExportCheckpoint();
            var epoch = model.ReadState().GeometryRevision;
            var reasons = new List<string>();

            // 1. pin flip is an authority/geometry change, never a restore. The velocity is zeroed as well so
            //    that the refusal is attributable to the binding change and not to the pinned-with-velocity rule.
            var altered = model.ExportSnapshot();
            var flipped = altered.Agents.Single(a => a.Id == "walker"); flipped.Pinned = true; flipped.Velocity = new CrowdVector();
            AssertRejected(model, altered, before, "pin binding", reasons);
            // 2. support plane change.
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "ramp").SupportGradient = new CrowdVector(0, 0);
            AssertRejected(model, altered, before, "support gradient", reasons);
            // 3. surface / region / frame / contact-space relabelling.
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "ramp").SurfaceId = "different-floor";
            AssertRejected(model, altered, before, "surface id", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "ramp").RegionId = PassageRegionId;
            AssertRejected(model, altered, before, "region id", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "ramp").FrameId = TrainSpaceId;
            AssertRejected(model, altered, before, "frame id", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "walker").ContactSpaceId = TrainSpaceId;
            AssertRejected(model, altered, before, "contact space", reasons);
            // 4. a foot elevation that is off the identical declared ramp plane.
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "ramp").FootElevationM += .5;
            AssertRejected(model, altered, before, "off-plane foot elevation", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "ramp").FootElevationM += 1e-6;
            AssertRejected(model, altered, before, "off-plane foot elevation at 1e-6", reasons);
            // 5. body profile and the clock/seed are not restorable state.
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "walker").RadiusM = .2;
            AssertRejected(model, altered, before, "radius", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "walker").HeightM = 2.0;
            AssertRejected(model, altered, before, "height", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "walker").PreferredSpeedMS = 3;
            AssertRejected(model, altered, before, "preferred speed", reasons);
            altered = model.ExportSnapshot(); altered.Seed = 999;
            AssertRejected(model, altered, before, "seed", reasons);
            // 6. roster and geometry epoch.
            altered = model.ExportSnapshot(); altered.Agents = altered.Agents.Take(1).ToArray();
            AssertRejected(model, altered, before, "roster", reasons);
            altered = model.ExportSnapshot(); altered.GeometryRevision++;
            AssertRejected(model, altered, before, "geometry revision", reasons);
            // 7. overlapping and nonfinite poses.
            altered = model.ExportSnapshot();
            altered.Agents.Single(a => a.Id == "walker").Position = altered.Agents.Single(a => a.Id == "ramp").Position;
            altered.Agents.Single(a => a.Id == "walker").FootElevationM = altered.Agents.Single(a => a.Id == "ramp").FootElevationM;
            AssertRejected(model, altered, before, "overlapping pose", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "walker").Position = new CrowdVector(double.NaN, 0);
            AssertRejected(model, altered, before, "nonfinite position", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "walker").Velocity = new CrowdVector(0, double.PositiveInfinity);
            AssertRejected(model, altered, before, "nonfinite velocity", reasons);
            altered = model.ExportSnapshot(); altered.Agents.Single(a => a.Id == "walker").FootElevationM = double.NaN;
            AssertRejected(model, altered, before, "nonfinite foot elevation", reasons);

            Assert.That(model.ExportCheckpoint(), Is.EqualTo(before), "Rejected restores must leave the checkpoint byte-identical.");
            Assert.That(model.ReadState().GeometryRevision, Is.EqualTo(epoch),
                "No rejected restore may advance the geometry epoch.");
            Assert.That(reasons.Count, Is.EqualTo(18));
            Assert.That(reasons.All(r => !string.IsNullOrEmpty(r)), Is.True);

            var receipt = new RestoreRejectionReceipt {
                DeclaredGeometryHash = fixture.DeclaredHash, SameEpochRejections = 18, RejectionReasonsRecorded = reasons.Count,
                RejectedChanges = reasons.ToArray(),
                CheckpointByteIdenticalAcrossAllRejections = model.ExportCheckpoint() == before,
                GeometryRevisionBefore = epoch, GeometryRevisionAfter = model.ReadState().GeometryRevision,
                Notes = Notes(
                    "Every entry is a same-epoch (GeometryRevision unchanged) restore attempt that must be refused.",
                    "The refusal is reported as a failure string; the live checkpoint must stay byte-identical across all of them.",
                    "A change of pin, support plane, label, body profile, clock, roster or epoch is an authorised geometry operation, not a restore.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-restore-rejections.json", receipt)), Is.True);
        }

        [Test]
        public void SameEpochRestoreAcceptsOnlyPosesOnTheIdenticalSupportPlane()
        {
            var fixture = Build();
            var ramp = RampBody("ramp", 70, 1);
            var model = new CrowdMotionModel(fixture.Definition, new[] { ramp }, RecipeSeed);
            var earlier = model.ExportSnapshot();
            StepCommitted(model, 40, StepSeconds, out _);
            var later = model.ExportSnapshot();
            Assert.That(later.Agents[0].Position.X, Is.GreaterThan(earlier.Agents[0].Position.X));
            Assert.That(later.Agents[0].FootElevationM, Is.LessThan(earlier.Agents[0].FootElevationM),
                "Walking down the declared ramp toward the corridor must lower the foot elevation.");

            Assert.That(model.TryRestore(earlier, out var failure), Is.True, failure);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(CrowdMotionModel.FromSnapshot(earlier).ExportCheckpoint()),
                "An earlier pose on the identical declared ramp plane must restore exactly.");

            // The same displacement with an elevation off that plane must be refused.
            var offPlane = later.Copy();
            offPlane.Agents[0].FootElevationM += 1e-6;
            Assert.That(model.TryRestore(offPlane, out var offPlaneFailure), Is.False, "An off-plane elevation must be refused.");
            Assert.That(offPlaneFailure, Is.Not.Empty);
            // A different point on the identical declared ramp plane: the lateral coordinate is held and only
            // the along-slope coordinate moves, with the foot elevation carried by the declared gradient. The
            // restore identity is |dFootElevation - dot(gradient, dPosition)| <= tolerance, so this must pass.
            var onPlane = later.Copy();
            onPlane.Agents[0].Position = new CrowdVector(later.Agents[0].Position.X + 5, later.Agents[0].Position.Y);
            onPlane.Agents[0].FootElevationM = later.Agents[0].FootElevationM + 5 * fixture.RampGradientX;
            Assert.That(model.TryRestore(onPlane, out var onPlaneFailure), Is.True,
                "A different point on the identical declared ramp plane must restore. " + onPlaneFailure);

            // A flat-floor body may restore within its plane, but a floor change may not use the same door.
            var flat = Npc("flat", 120, 0, fixture.FloorY);
            var flatModel = new CrowdMotionModel(fixture.Definition, new[] { flat }, RecipeSeed);
            var flatEarlier = flatModel.ExportSnapshot();
            StepCommitted(flatModel, 20, StepSeconds, out _);
            Assert.That(flatModel.TryRestore(flatEarlier, out var flatFailure), Is.True, flatFailure);
            var lifted = flatModel.ExportSnapshot(); lifted.Agents[0].FootElevationM += 1e-3;
            Assert.That(flatModel.TryRestore(lifted, out _), Is.False, "A different floor is a different support binding.");
        }

        // ---------------------------------------------------------------- 5. tick atomicity ----

        [Test]
        public void FailedTickIsNotPartiallyCommittedAndTheStateIsByteIdentical()
        {
            var fixture = Build();
            // The rising body climbs the declared ramp toward the terminal under a 4 m/s wish; a pinned body
            // stands exactly one body-height plus 30 mm above it. The pair is not eligible at first, so the tick
            // runs several substeps before the declared swept vertical reach makes the start configuration
            // three-dimensionally overlapping: the tick must fail without committing any of those substeps.
            var rising = RampBody("rising", 60, -4);
            var overhead = Npc("overhead", 60, 0, rising.FootElevationM + BodyHeightM + .03);
            overhead.Pinned = true; overhead.RegionId = TerminalRegionId; overhead.SurfaceId = "ramp-floor";
            var definition = fixture.Definition.Copy(); definition.Parameters.MaxSubstepSeconds = .005;
            var model = new CrowdMotionModel(definition, new[] { rising, overhead }, RecipeSeed);
            var before = model.ExportCheckpoint();
            var beforeState = model.ReadState();
            Assert.That(rising.SupportGradient.X, Is.LessThan(0), "The declared ramp must descend toward the corridor.");

            Assert.That(model.TryAdvance(StepSeconds, null, out var report), Is.False, "The swept vertical reach must fail this tick.");
            Assert.That(report.AttemptedSubsteps, Is.GreaterThan(1), "The failure must occur after substeps were already projected, not before the first.");
            Assert.That(report.Committed, Is.False);
            Assert.That(report.Failure, Is.Not.Empty);
            var identicalAtFailure = model.ExportCheckpoint() == before;
            Assert.That(identicalAtFailure, Is.True, "A failed tick must not commit a partial tick.");
            var afterState = model.ReadState();
            var beforeRising = beforeState.Agents.Single(a => a.Id == "rising");
            var afterRising = afterState.Agents.Single(a => a.Id == "rising");
            Assert.That(afterState.Tick, Is.EqualTo(beforeState.Tick));
            Assert.That(afterState.AcceptedSubsteps, Is.EqualTo(beforeState.AcceptedSubsteps));
            Assert.That(afterState.ElapsedSeconds, Is.EqualTo(beforeState.ElapsedSeconds));
            Assert.That(afterState.GeometryRevision, Is.EqualTo(beforeState.GeometryRevision));
            Assert.That(afterRising.Position.X, Is.EqualTo(beforeRising.Position.X), "No substep of the refused tick may advance a body.");
            Assert.That(afterRising.FootElevationM, Is.EqualTo(beforeRising.FootElevationM), "No substep of the refused tick may raise a body.");
            Assert.That(afterRising.Velocity.Length, Is.Zero);

            // The unchanged state then advances identically to an untouched twin: no partial substep survived.
            var twin = CrowdMotionModel.FromCheckpoint(before);
            var descend = new[] { new CrowdIntent { AgentId = "rising", Mode = CrowdIntentMode.DesiredVelocity, DesiredVelocity = new CrowdVector(4, 0) } };
            Assert.That(model.TryAdvance(StepSeconds, descend, out var moved), Is.True, moved.Failure);
            Assert.That(twin.TryAdvance(StepSeconds, descend, out var twinMoved), Is.True, twinMoved.Failure);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(twin.ExportCheckpoint()),
                "The recovery tick must be the same function of the refused state as of an untouched twin.");
            Assert.That(model.ExportSnapshot().Agents.Single(a => a.Id == "rising").FootElevationM,
                Is.LessThan(beforeRising.FootElevationM), "Walking down the declared ramp must lower the foot elevation.");

            var receipt = new AtomicityReceipt {
                DeclaredGeometryHash = fixture.DeclaredHash,
                TickSeconds = StepSeconds, MaximumSubstepSeconds = definition.Parameters.MaxSubstepSeconds,
                SubstepsPerTick = (int)Math.Round(StepSeconds / definition.Parameters.MaxSubstepSeconds),
                FailedTickCommitted = report.Committed, FailedTickAttemptedSubsteps = report.AttemptedSubsteps,
                FailedTickReason = report.Failure, StateByteIdenticalAfterFailure = identicalAtFailure,
                RecoveryMatchesUntouchedTwin = model.ExportCheckpoint() == twin.ExportCheckpoint(),
                Notes = Notes(
                    "The refused tick projected substeps onto a fork and never assigned it to the live state.",
                    "FailedTickAttemptedSubsteps > 1 proves the refusal happened after work was already projected, not before the first substep.",
                    "The recovery tick from the refused state matches an untouched twin decoded from the same checkpoint.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-atomicity.json", receipt)), Is.True);
        }

        [Test]
        public void NonfiniteOverlappingAndOutOfRangeInputsAreRejected()
        {
            var fixture = Build();
            void Rejected(CrowdAgent[] bodies, string context)
            {
                Assert.Throws<ArgumentException>(() => new CrowdMotionModel(fixture.Definition, bodies), context);
            }
            var bad = Npc("bad", 120, 0, fixture.FloorY); bad.Position = new CrowdVector(double.NaN, 0);
            Rejected(new[] { bad }, "nonfinite position");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.Velocity = new CrowdVector(0, double.PositiveInfinity);
            Rejected(new[] { bad }, "nonfinite velocity");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.DesiredVelocity = new CrowdVector(double.NaN, 0); bad.IntentMode = CrowdIntentMode.DesiredVelocity;
            Rejected(new[] { bad }, "nonfinite desired velocity");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.FootElevationM = double.PositiveInfinity;
            Rejected(new[] { bad }, "nonfinite foot elevation");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.FootElevationM = 1e8;
            Rejected(new[] { bad }, "foot elevation outside the supported metric range");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.SupportGradient = new CrowdVector(2, 0);
            Rejected(new[] { bad }, "support gradient beyond unit length");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.HeightM = .2;
            Rejected(new[] { bad }, "height below the body diameter");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.Pinned = true; bad.Velocity = new CrowdVector(1, 0);
            Rejected(new[] { bad }, "pinned body with nonzero velocity");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.RadiusM = .001;
            Rejected(new[] { bad }, "radius below the metric floor");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.PreferredSpeedMS = 21;
            Rejected(new[] { bad }, "preferred speed beyond the declared limit");
            bad = Npc("bad", 120, 0, fixture.FloorY); bad.IntentMode = (CrowdIntentMode)7;
            Rejected(new[] { bad }, "undefined intent mode");
            Rejected(new[] { Npc("a", 120, 0, fixture.FloorY), Npc("b", 120, 0, fixture.FloorY) }, "overlapping roster");
            Rejected(new[] { Npc("a", 120, 0, fixture.FloorY), Npc("a", 130, 0, fixture.FloorY) }, "duplicate roster id");
            Rejected(new[] { Npc("a", 120, 0, fixture.FloorY), Npc("b", 120, 0, fixture.FloorY + 1) }, "vertically overlapping stack");
            Rejected(new[] { Npc("a", fixture.ChokeEntryX + 5, -fixture.ChokeHalfWidthM, fixture.FloorY) }, "body standing on a declared choke pier face");

            var penetratingWall = Wall("wall", 100, 0, 140, 0, fixture.FloorY, fixture.WallTopY);
            var walls = fixture.Definition.Copy(); walls.Walls = walls.Walls.Concat(new[] { penetratingWall }).ToArray();
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(walls, new[] { Npc("a", 120, 0, fixture.FloorY) }),
                "A body inside a wall must be refused, not nudged out.");
            var degenerate = fixture.Definition.Copy();
            degenerate.Walls = degenerate.Walls.Concat(new[] { Wall("degenerate", 5, 5, 5, 5, fixture.FloorY, fixture.WallTopY) }).ToArray();
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(degenerate, new[] { Npc("a", 120, 0, fixture.FloorY) }),
                "A zero-length wall segment must be refused.");

            var model = new CrowdMotionModel(fixture.Definition, new[] { Npc("a", 120, 0, fixture.FloorY, 1, 0) }, RecipeSeed);
            var before = model.ExportCheckpoint();
            Assert.That(model.TryAdvance(double.NaN, null, out var nan), Is.False); Assert.That(nan.Failure, Is.Not.Empty);
            Assert.That(model.TryAdvance(0, null, out var zero), Is.False); Assert.That(zero.Failure, Is.Not.Empty);
            Assert.That(model.TryAdvance(-1, null, out var negative), Is.False); Assert.That(negative.Failure, Is.Not.Empty);
            Assert.That(model.TryAdvance(61, null, out var huge), Is.False); Assert.That(huge.Failure, Is.Not.Empty);
            Assert.That(model.TryAdvance(StepSeconds, new[] { new CrowdIntent { AgentId = "a", Mode = CrowdIntentMode.DesiredVelocity, DesiredVelocity = new CrowdVector(double.NaN, 0) } }, out var intentFailure), Is.False);
            Assert.That(intentFailure.Failure, Is.Not.Empty);
            Assert.That(model.TryAdvance(StepSeconds, new[] { new CrowdIntent { AgentId = "missing", Mode = CrowdIntentMode.Goal, Goal = new CrowdVector(1, 1) } }, out var unknown), Is.False);
            Assert.That(unknown.Failure, Is.Not.Empty);
            Assert.That(model.TryAdvance(StepSeconds, null, out _, new[] { new CrowdMovementWindow { AgentId = "a", MinX = 200, MaxX = 300, MinY = -1, MaxY = 1 } }), Is.False,
                "A body outside its declared support window must be refused.");
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(before), "Every rejected tick must leave the checkpoint byte-identical.");
            Assert.That(model.ReadState().Tick, Is.Zero, "No rejected tick may advance the clock.");
        }

        // ---------------------------------------------------------------- 6. explicit rebind / legacy ----

        [Test]
        public void ExplicitRebindIsTheOnlyGeometryEpochThatMayChangeSupport()
        {
            var fixture = Build();
            var model = new CrowdMotionModel(fixture.Definition, new[] { Npc("a", 120, 0, fixture.FloorY, 1, 0) }, RecipeSeed);
            StepCommitted(model, 4, StepSeconds, out _);
            var before = model.ExportSnapshot();

            var request = Rebind(model);
            request.Bodies.Single().Pinned = true;
            request.Bodies.Single().SurfaceId = "concourse-floor";
            request.Bodies.Single().RegionId = ConcourseRegionId;
            request.Bodies.Single().FootElevationM = DeclaredConcourseFloorY;
            request.Bodies.Single().SupportGradient = new CrowdVector(.2, 0);
            Assert.That(model.TryRebind(request, out var failure), Is.True, failure);
            var after = model.ExportSnapshot();
            Assert.That(after.GeometryRevision, Is.EqualTo(before.GeometryRevision + 1), "One rebind is exactly one geometry epoch.");
            Assert.That(after.Tick, Is.EqualTo(before.Tick));
            Assert.That(after.Seed, Is.EqualTo(before.Seed));
            Assert.That(after.AcceptedSubsteps, Is.EqualTo(before.AcceptedSubsteps));
            Assert.That(after.ElapsedSeconds, Is.EqualTo(before.ElapsedSeconds));
            Assert.That(after.Agents[0].RadiusM, Is.EqualTo(before.Agents[0].RadiusM), "A rebind may not resize the body.");
            Assert.That(after.Agents[0].HeightM, Is.EqualTo(before.Agents[0].HeightM));
            Assert.That(after.Agents[0].PreferredSpeedMS, Is.EqualTo(before.Agents[0].PreferredSpeedMS));
            Assert.That(after.Agents[0].Pinned, Is.True);
            Assert.That(after.Agents[0].Velocity.Length, Is.Zero, "A pinned body carries no velocity through a rebind.");
            Assert.That(model.TryRestore(before, out _), Is.False, "A previous epoch's snapshot is no longer restorable.");

            // The rebound body is now bound to the new support and holds there.
            StepCommitted(model, 4, StepSeconds, out _);
            Assert.That(model.ExportSnapshot().Agents[0].FootElevationM, Is.EqualTo(DeclaredConcourseFloorY).Within(1e-12));
        }

        [Test]
        public void RebindAndRestoreRejectStaleOrForeignRequestsAtomically()
        {
            var fixture = Build();
            var model = new CrowdMotionModel(fixture.Definition, new[] { Npc("a", 120, 0, fixture.FloorY, 1, 0), Npc("b", 130, 0, fixture.FloorY, 1, 0) }, RecipeSeed);
            StepCommitted(model, 4, StepSeconds, out _);
            var before = model.ExportCheckpoint();

            AssertRejectedRebind(model, before, r => r.ExpectedTick = 0, "stale tick");
            // GeometryRevision starts at zero and only advances on a rebind, so "stale" here means one ahead.
            AssertRejectedRebind(model, before, r => r.ExpectedGeometryRevision = r.ExpectedGeometryRevision + 1, "stale revision");
            AssertRejectedRebind(model, before, r => r.Bodies = r.Bodies.Take(1).ToArray(), "roster change");
            AssertRejectedRebind(model, before, r => r.Bodies[1].AgentId = r.Bodies[0].AgentId, "duplicate roster id");
            AssertRejectedRebind(model, before, r => r.Bodies[0].Velocity = new CrowdVector(double.NaN, 0), "nonfinite velocity");
            AssertRejectedRebind(model, before, r => r.Bodies[0].FootElevationM = 1e8, "foot elevation outside the supported metric range");
            AssertRejectedRebind(model, before, r => r.Bodies[0].Position = r.Bodies[1].Position, "overlapping rebind");
            AssertRejectedRebind(model, before, r => r.Spaces = new[] { new CrowdSpace { Id = "elsewhere" } }, "unknown contact space");
            AssertRejectedRebind(model, before, r => { r.Walls = r.Walls.Concat(new[] { Wall("intruding", 100, 0, 140, 0, fixture.FloorY, fixture.WallTopY) }).ToArray(); }, "rebind into a wall");
            AssertRejectedRebind(model, before, r => r.Walls = new[] { Wall("degenerate", 120, -1, 120, -1, fixture.FloorY, fixture.WallTopY) }, "degenerate wall");
            Assert.That(model.TryRebind(null, out var nullFailure), Is.False); Assert.That(nullFailure, Is.Not.Empty);

            // Foreign checkpoints are refused by the read path, which reports rather than throws.
            var foreignIdentity = fixture.Definition.Copy(); foreignIdentity.ProfileId = "foreign-geometry";
            var foreign = new CrowdMotionModel(foreignIdentity, new[] { Npc("a", 200, 0, fixture.FloorY) }, RecipeSeed);
            Assert.That(model.TryRestoreCheckpoint(foreign.ExportCheckpoint(), out var foreignFailure), Is.False,
                "A checkpoint from a differently identified geometry must be refused.");
            Assert.That(foreignFailure, Is.Not.Empty);
            var foreignClock = new CrowdMotionModel(fixture.Definition.Copy(), new[] { Npc("a", 200, 0, fixture.FloorY) }, RecipeSeed + 1);
            Assert.That(model.TryRestoreCheckpoint(foreignClock.ExportCheckpoint(), out var clockFailure), Is.False,
                "A checkpoint from a differently seeded run must be refused.");
            Assert.That(clockFailure, Is.Not.Empty);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(before), "Rejected rebinds and restores must change nothing.");
        }

        [Test]
        public void LegacyCheckpointCannotBeAdoptedByTheDeclaredGeometry()
        {
            // The frozen CGC1 fixture belongs to its own declared legacy profile; it may still be read on its
            // own terms, but the declared bottleneck model must not adopt it through the restore path.
            var legacy = CrowdMotionModel.FromCheckpoint(CrowdLegacyFixture.Before);
            Assert.That(legacy.ExportCheckpoint(), Does.StartWith("CGC2:"), "A legacy CGC1 read must migrate to the current envelope.");
            Assert.That(legacy.ExportSnapshot().Definition.ProfileId, Is.Not.EqualTo("fmp07c-declared-bottleneck/" + DeclaredProfileId));

            var fixture = Build();
            var model = new CrowdMotionModel(fixture.Definition, new[] { Npc("a", 120, 0, fixture.FloorY, 1, 0) }, RecipeSeed);
            var before = model.ExportCheckpoint();
            Assert.That(model.TryRestoreCheckpoint(CrowdLegacyFixture.Before, out var legacyFailure), Is.False);
            Assert.That(legacyFailure, Is.Not.Empty);
            Assert.That(model.TryRestoreCheckpoint(CrowdLegacyFixture.After, out var afterFailure), Is.False);
            Assert.That(afterFailure, Is.Not.Empty);
            var corrupt = CrowdLegacyFixture.Before.Substring(0, CrowdLegacyFixture.Before.Length - 8);
            Assert.That(model.TryRestoreCheckpoint(corrupt, out var corruptFailure), Is.False);
            Assert.That(corruptFailure, Is.Not.Empty);
            Assert.That(model.TryRestoreCheckpoint("CGC9:" + CrowdLegacyFixture.Before.Substring(5), out _), Is.False);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
        }

        // ---------------------------------------------------------------- 7. repeatability / recipe ----

        [Test]
        public void TwoModelsWithTheSameSeedAndCommandProduceTheSameBottleneckState()
        {
            var fixture = Build();
            CrowdAgent[] Roster() => Enumerable.Range(0, 24)
                .Select(i => Npc("npc" + i.ToString("D2"), 160 + (i % 6) * .32, -1.2 + (i / 6) * .8, fixture.FloorY, .7, 0)).ToArray();
            var first = new CrowdMotionModel(fixture.Definition, Roster(), RecipeSeed);
            var second = new CrowdMotionModel(fixture.Definition, Roster(), RecipeSeed);
            var otherSeed = new CrowdMotionModel(fixture.Definition, Roster(), RecipeSeed + 1);
            StepCommitted(first, 60, StepSeconds, out _);
            StepCommitted(second, 60, StepSeconds, out _);
            StepCommitted(otherSeed, 60, StepSeconds, out _);
            Assert.That(first.ExportCheckpoint(), Is.EqualTo(second.ExportCheckpoint()),
                "The declared bottleneck recipe must reproduce byte-identically for a fixed geometry, roster, seed and command.");

            // The motion core consumes no stochastic term, so the seed must not change the trajectory. The seed
            // is still part of the checkpoint identity, so it is compared through the poses, not through the bytes.
            var a = first.ExportSnapshot(); var b = otherSeed.ExportSnapshot();
            Assert.That(b.Seed, Is.EqualTo(RecipeSeed + 1));
            Assert.That(b.Agents.Select(x => x.Position.X).ToArray(), Is.EqualTo(a.Agents.Select(x => x.Position.X).ToArray()),
                "A different seed must not change the declared recipe trajectory.");
            Assert.That(b.Agents.Select(x => x.Position.Y).ToArray(), Is.EqualTo(a.Agents.Select(x => x.Position.Y).ToArray()));
        }

        [Test]
        public void StepSensitivityAcrossDeclaredSubstepSizesIsRecorded()
        {
            var fixture = Build();
            const int tickCount = 120, bodyCount = 12;
            var mouths = new List<int>(); var maxima = new List<double>();
            var substeps = new[] { .01, .005 };
            foreach (var substep in substeps)
            {
                var definition = fixture.Definition.Copy(); definition.Parameters.MaxSubstepSeconds = substep;
                var bodies = new List<CrowdAgent>();
                for (var lane = 0; lane < 3; lane++)
                for (var column = 0; column < 4; column++)
                    bodies.Add(Npc("npc" + (lane * 4 + column).ToString("D2"), 176 + column * .32, -.8 + lane * .8, fixture.FloorY, 1.2, 0));
                var model = new CrowdMotionModel(definition, bodies.ToArray(), RecipeSeed);
                for (var i = 0; i < tickCount; i++)
                {
                    Assert.That(model.TryAdvance(StepSeconds, null, out var report), Is.True, "substep=" + substep + " " + report.Failure);
                    AssertNoPenetration(model.ExportSnapshot(), "substep=" + substep + " tick " + i);
                }
                var snapshot = model.ExportSnapshot();
                mouths.Add(snapshot.Agents.Count(a => a.Position.X > fixture.ChokeEntryX));
                maxima.Add(snapshot.Agents.Max(a => a.Position.X));
            }
            var spread = Math.Abs(maxima[0] - maxima[1]);
            Assert.That(mouths[0], Is.GreaterThan(0), "No body reached the declared choke mouth, so the step comparison is empty.");
            Assert.That(mouths[0], Is.EqualTo(mouths[1]),
                "Halving the substep changed which bodies reached the declared choke mouth: " + mouths[0] + " vs " + mouths[1]);
            // Recorded sensitivity envelope rather than a claimed equality: the same command at 2x finer substeps
            // must stay inside a recorded displacement band. The measured band travels in the receipt.
            Assert.That(spread, Is.LessThanOrEqualTo(.25),
                "Substep sensitivity outside the recorded band: 0.01 s -> " + maxima[0] + ", 0.005 s -> " + maxima[1]);

            var receipt = new StepSensitivityReceipt {
                DeclaredGeometryHash = fixture.DeclaredHash, DeclaredSubstepSeconds = substeps,
                BodiesPastChokeMouth = mouths.ToArray(), MaximumX = maxima.ToArray(),
                MaximumPositionSpreadM = spread, SimulatedSeconds = tickCount * StepSeconds,
                BodyCount = bodyCount, TickCountPerRun = tickCount,
                Notes = Notes(
                    "Both runs advance the identical declared substep-size-independent command for the identical simulated time.",
                    "MaximumPositionSpreadM is the measured displacement band between the two substep sizes, not an asserted exact equality.",
                    "A spread beyond the recorded band means the declared bottleneck result depends on the substep size and must be re-derived.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-step-sensitivity.json", receipt)), Is.True);
        }

        [Test]
        public void ReverseFlowThroughTheDeclaredChokeTraversesWithoutPenetration()
        {
            var fixture = Build();
            // Evacuation direction: a dense column inside the declared shopping passage walks back out through
            // the declared choke into the connector. Every other contact case here flows the other way, so this
            // pins that the choke is not a one-way artefact of the corridor-to-passage ordering.
            var bodies = new List<CrowdAgent>();
            for (var lane = 0; lane < 4; lane++)
            for (var column = 0; column < 5; column++)
                bodies.Add(Npc("npc" + (lane * 5 + column).ToString("D2"),
                    fixture.PassageCenterX - 2 - column * .32, -1.2 + lane * .8, fixture.FloorY, -1.5, 0));
            var model = new CrowdMotionModel(fixture.Definition, bodies.ToArray(), RecipeSeed);
            var snapshot = StepCommittedChecked(model, 200, StepSeconds, "reverse declared choke flow", out var reports);

            AssertNoPenetration(snapshot, "reverse declared choke flow");
            AssertCommittedReports(reports, "reverse declared choke flow");
            Assert.That(snapshot.Agents.Count(a => a.Position.X < fixture.ChokeExitX), Is.GreaterThanOrEqualTo(4),
                "The reverse column must actually re-enter the declared choke from the shopping passage; measured " +
                snapshot.Agents.Count(a => a.Position.X < fixture.ChokeExitX) + " of " + snapshot.Agents.Length);
            foreach (var body in snapshot.Agents.Where(a => a.Position.X > fixture.ChokeEntryX + 1e-6 && a.Position.X < fixture.ChokeExitX - 1e-6))
                Assert.That(Math.Abs(body.Position.Y), Is.LessThanOrEqualTo(fixture.ChokeHalfWidthM - NpcRadiusM + 1e-6),
                    body.Id + " occupies a Z outside the declared choke clear width while flowing in reverse.");
        }

        [Test]
        public void BottleneckRecipeReproducesOnceWithRegionCountSeedAndGeometryHash()
        {
            var fixture = Build();
            const int bodyCount = 24;
            const int tickCount = 400;
            var bodies = Enumerable.Range(0, bodyCount)
                .Select(i => Queued("npc" + i.ToString("D2"), 176 + (i % 6) * .32, -1.2 + (i / 6) * .8)).ToArray();
            var model = new CrowdMotionModel(fixture.Definition, bodies, RecipeSeed);

            var peakOccupancy = 0; var peakDensity = 0.0;
            var minimumClearance = double.PositiveInfinity; var minimumWall = double.PositiveInfinity;
            var maximumViolation = 0.0; var maximumGroundSpeed = 0.0; var minimumSwept = double.PositiveInfinity;
            for (var tick = 0; tick < tickCount; tick++)
            {
                Assert.That(model.TryAdvance(StepSeconds, null, out var report), Is.True, report.Failure);
                var state = model.ExportSnapshot();
                var inside = CountInsideChoke(state, fixture);
                peakOccupancy = Math.Max(peakOccupancy, inside);
                peakDensity = Math.Max(peakDensity, inside / (fixture.ChokeLengthM * fixture.ChokeClearWidthM));
                minimumClearance = Math.Min(minimumClearance, MinimumCentreClearance(state));
                minimumWall = Math.Min(minimumWall, MinimumWallClearance(state));
                maximumViolation = Math.Max(maximumViolation, report.MaximumConstraintViolationMS);
                maximumGroundSpeed = Math.Max(maximumGroundSpeed, report.MaximumGroundSpeedMS);
                minimumSwept = Math.Min(minimumSwept, report.MinimumSweptGapM);
            }
            var snapshot = model.ExportSnapshot();
            var pastMouth = snapshot.Agents.Count(a => a.Position.X > fixture.ChokeEntryX);
            var pastExit = snapshot.Agents.Count(a => a.Position.X > fixture.ChokeExitX);

            AssertNoPenetration(snapshot, "recipe reproduction");
            Assert.That(pastMouth, Is.GreaterThan(0), "The recorded recipe must actually move bodies through the declared choke mouth.");
            Assert.That(pastExit, Is.GreaterThan(0), "The recorded recipe must actually clear the declared choke exit into the passage.");
            Assert.That(peakOccupancy, Is.GreaterThan(0));

            var receipt = new RecipeReceipt {
                DeclaredGeometryHash = fixture.DeclaredHash, FixtureGeometryHash = fixture.DefinitionHash,
                DeclaredRegionCount = fixture.Declared.Regions.Length, DeclaredPortalCount = fixture.Declared.Portals.Length,
                DeclaredFrameCount = fixture.Declared.Frames.Length,
                ConnectorRegionId = ConnectorRegionId, ChokePortalId = ChokePortalId, PassageRegionId = PassageRegionId,
                PlatformRegionId = PlatformRegionId, MetroVehicleRegionId = MetroVehicleRegionId,
                ConnectorLengthM = fixture.ConnectorMaxX - fixture.ConnectorMinX, ConnectorWidthM = fixture.ConnectorMaxZ - fixture.ConnectorMinZ,
                ChokeClearWidthM = fixture.ChokeClearWidthM, ChokeLengthM = fixture.ChokeLengthM,
                PassageLengthM = fixture.PassageMaxX - fixture.PassageMinX, PassageWidthM = fixture.PassageMaxZ - fixture.PassageMinZ,
                FloorElevationM = fixture.FloorY, StepSeconds = StepSeconds, SimulatedSeconds = tickCount * StepSeconds,
                BodyCount = bodyCount, TickCount = tickCount, NpcRadiusMm = (int)Math.Round(NpcRadiusM * 1000), Seed = RecipeSeed,
                BodiesPastChokeMouth = pastMouth, BodiesPastChokeExit = pastExit,
                PeakChokeOccupancy = peakOccupancy, PeakChokeDensityPM2 = peakDensity,
                FinalChokeMeanSpeedMS = snapshot.Agents.Where(a => a.ContactSpaceId == WorldSpaceId &&
                    a.Position.X >= fixture.ChokeEntryX && a.Position.X <= fixture.ChokeExitX).Select(a => a.Velocity.Length).DefaultIfEmpty(0).Average(),
                MinimumCentreClearanceM = minimumClearance, MinimumWallClearanceM = minimumWall,
                MaximumConstraintViolationMS = maximumViolation, MaximumGroundSpeedMS = maximumGroundSpeed,
                MinimumSweptGapM = minimumSwept,
                Notes = Notes(
                    "Declared synthetic profile geometry (native-connected-v1), not a captured scene-collider geometry: ConnectedWorldGeometry.Capture over the 13 loaded region scenes is the #125 (FMP-07a) path.",
                    "The fixture is derived from foundation/world/connected-world-profile.json at test time; DeclaredGeometryHash identifies those exact bytes and the test asserts it against a frozen constant.",
                    "MaximumGroundSpeedMS is the largest ground speed the solver itself reported during the recipe, not a surveyed design speed.",
                    "No facility, railway or field validation is claimed.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-bottleneck.json", receipt)), Is.True);
            Assert.That(receipt.DeclaredGeometryHash, Is.EqualTo(Sha256OfFile(ProfilePath)));
            Assert.That(minimumWall, Is.GreaterThanOrEqualTo(-1e-9));
        }

        // ---------------------------------------------------------------- 0. world-actual predecessor ----

        [Test]
        public void DeclaredPredecessorFixtureIsConsumedAsWorldActualGeometryAndNotAsTheAbstractLaboratory()
        {
            var predecessor = LoadWorldCrowdFixture(out var loadFailure);
            Assert.That(predecessor, Is.Not.Null, loadFailure);

            var path = WorldCrowdFixtureFullPath();
            var observedBytes = new FileInfo(path).Length;
            var observedSha256 = Sha256OfFile(path);

            // A camelCase or truncated read leaves every field at its default value rather than throwing, so
            // these assertions are on VALUES: an all-defaults read fails here instead of reading as "declares nothing".
            Assert.That(predecessor.SchemaVersion, Is.EqualTo(1), "Predecessor fixture schema version.");
            Assert.That(predecessor.WorkId, Is.EqualTo("FMP-07a"));
            Assert.That(predecessor.Classification, Is.EqualTo("PUBLIC_PROJECT_EVIDENCE"));
            Assert.That(predecessor.FixtureKind, Is.EqualTo(WorldActualFixtureKind),
                "The predecessor fixture must be the world-actual collider/navigation kind, not the abstract laboratory kind.");
            Assert.That(predecessor.FixtureKind, Is.Not.EqualTo(AbstractUnitFixtureKind));

            Assert.That(predecessor.Body, Is.Not.Null);
            Assert.That(predecessor.Body.Kind, Is.EqualTo("npc"));
            Assert.That(predecessor.Body.Count, Is.EqualTo(WorldActualBodyCount));
            Assert.That(predecessor.Body.PlayerCount, Is.Zero);
            Assert.That(predecessor.Body.RadiusConstant, Is.EqualTo("WorldBodyPresentation.NpcRadiusM"));
            Assert.That(predecessor.Body.RadiusM, Is.EqualTo(WorldBodyPresentation.NpcRadiusM).Within(1e-12),
                "The world-actual body radius must be exactly the authored NPC radius constant.");
            Assert.That(predecessor.Body.RadiusM, Is.EqualTo(.41).Within(1e-12));
            Assert.That(predecessor.Body.HeightM, Is.EqualTo(WorldBodyPresentation.NpcHeightM).Within(1e-12));
            // The two body profiles must never be interchanged. 0.15 m is the abstract laboratory unit radius and
            // stays strictly below the authored size; the world-actual scenario below drives 0.41 m, never 0.15 m.
            Assert.That(predecessor.Body.AbstractUnitRadiusM, Is.EqualTo(WorldActualAbstractUnitRadiusM).Within(1e-12));
            Assert.That(predecessor.Body.AbstractUnitRadiusM, Is.EqualTo(NpcRadiusM).Within(1e-12),
                "This file's declared-profile scenarios drive exactly the predecessor's declared abstract counterpart radius.");
            Assert.That(predecessor.Body.RadiusM, Is.GreaterThan(predecessor.Body.AbstractUnitRadiusM),
                "The abstract unit radius must stay strictly below the world-actual radius.");

            var counterpart = predecessor.AbstractCounterpart;
            Assert.That(counterpart, Is.Not.Null);
            Assert.That(counterpart.FixtureKind, Is.EqualTo(AbstractUnitFixtureKind));
            Assert.That(counterpart.FixtureKind, Is.Not.EqualTo(predecessor.FixtureKind),
                "The abstract counterpart must be a different fixture kind from the world-actual one.");
            Assert.That(counterpart.ActualGeometry, Is.False);
            Assert.That(counterpart.RecordedSeparately, Is.True);
            Assert.That(counterpart.RadiusM, Is.EqualTo(WorldActualAbstractUnitRadiusM).Within(1e-12));
            Assert.That(counterpart.Sources, Is.Not.Empty);
            Assert.That(counterpart.DeletionPolicy, Is.Not.Empty);

            Assert.That(predecessor.GeometryLoad, Is.Not.Null);
            Assert.That(predecessor.GeometryLoad.LoadedRegionScenes, Is.EqualTo(WorldActualRegionCount));
            Assert.That(predecessor.GeometryLoad.DeclaredRegionScenes, Is.EqualTo(WorldActualRegionCount));
            Assert.That(predecessor.GeometryLoad.MustMatchDeclaredRegionSetExactly, Is.True);
            Assert.That(predecessor.GeometryLoad.EveryRegionMustContributeAtLeastOneSupport, Is.True);
            Assert.That(predecessor.GeometryLoad.EveryCapturedColliderMustBePresentEnabledAndUnchanged, Is.True);
            Assert.That(predecessor.GeometryLoad.NoColliderMayBeSilentlySkipped, Is.True);

            Assert.That(predecessor.SourceProfile, Is.Not.Null);
            Assert.That(predecessor.SourceProfile.Regions, Is.EqualTo(WorldActualRegionCount));
            Assert.That(predecessor.SourceProfile.Portals, Is.EqualTo(WorldActualPortalCount));
            Assert.That(predecessor.SourceProfile.Frames, Is.EqualTo(WorldActualFrameCount));
            Assert.That(predecessor.SourceProfile.Sha256, Is.EqualTo(DeclaredGeometryHash),
                "The predecessor fixture was captured against different declared profile bytes than this file consumes.");

            Assert.That(predecessor.SceneHash, Is.Not.Null);
            Assert.That(predecessor.SceneHash.ManifestSha256, Is.EqualTo(WorldActualSceneManifestSha256),
                "The predecessor fixture's region scene manifest changed; the world-actual scenario must be re-derived.");
            Assert.That(predecessor.SceneHash.RegionScenes, Is.Not.Null);
            Assert.That(predecessor.SceneHash.RegionScenes.Length, Is.EqualTo(WorldActualRegionCount));
            Assert.That(predecessor.SceneHash.RegionScenes.Select(r => r.RegionId).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(WorldActualRegionCount), "The predecessor fixture names a region scene twice.");

            // Genuine consumption: every region this profile declares must appear in the predecessor's manifest
            // with the same authored scene and frame, so this file's EditMode geometry is anchored to that roster.
            var declared = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ProfilePath));
            declared.Validate();
            foreach (var region in declared.Regions)
            {
                var scene = predecessor.SceneHash.RegionScenes.SingleOrDefault(r => r.RegionId == region.Id);
                Assert.That(scene, Is.Not.Null, "The predecessor fixture carries no region scene for declared region " + region.Id + ".");
                Assert.That(scene.SceneName, Is.EqualTo(region.SceneName), "Wrong authored scene for " + region.Id + ".");
                Assert.That(scene.FrameId, Is.EqualTo(region.FrameId), "Wrong declared frame for " + region.Id + ".");
            }

            Assert.That(predecessor.Placement, Is.Not.Null);
            Assert.That(predecessor.Placement.RegionsCovered, Is.EqualTo(WorldActualRegionCount));
            Assert.That(predecessor.Placement.PerRegionCounts, Is.Not.Null);
            Assert.That(predecessor.Placement.PerRegionCounts.Length, Is.EqualTo(WorldActualRegionCount));
            Assert.That(predecessor.Placement.PerRegionCounts.All(count => count > 0), Is.True,
                "Every declared region must carry at least one body so no region can be dropped.");
            Assert.That(predecessor.Placement.PerRegionCounts.Sum(), Is.EqualTo(WorldActualBodyCount));
            Assert.That(predecessor.MeasurementItems, Is.Not.Null);
            Assert.That(predecessor.MeasurementItems.Length, Is.EqualTo(WorldActualMeasurementItemCount));

            var receipt = new WorldActualFixtureReceipt {
                DeclaredGeometryHash = DeclaredGeometryHash,
                PredecessorRelativePath = WorldCrowdFixturePath, PredecessorSha256 = observedSha256,
                PredecessorFixtureKind = predecessor.FixtureKind, PredecessorWorkId = predecessor.WorkId,
                PredecessorBytes = observedBytes,
                RadiusConstantValue = WorldBodyPresentation.NpcRadiusM,
                WorldActualRadiusM = predecessor.Body.RadiusM, AbstractUnitRadiusM = predecessor.Body.AbstractUnitRadiusM,
                DeclaredRegionScenes = predecessor.SceneHash.RegionScenes.Length,
                DeclaredPortals = predecessor.SourceProfile.Portals, DeclaredFrames = predecessor.SourceProfile.Frames,
                DeclaredBodyCount = predecessor.Body.Count, RegionsCovered = predecessor.Placement.RegionsCovered,
                SceneManifestSha256 = predecessor.SceneHash.ManifestSha256,
                SourceProfileSha256 = predecessor.SourceProfile.Sha256,
                GeometryLoadContractHolds = predecessor.GeometryLoad.MustMatchDeclaredRegionSetExactly &&
                    predecessor.GeometryLoad.EveryRegionMustContributeAtLeastOneSupport &&
                    predecessor.GeometryLoad.EveryCapturedColliderMustBePresentEnabledAndUnchanged &&
                    predecessor.GeometryLoad.NoColliderMayBeSilentlySkipped,
                RegionManifestMatchesThisProfile = true,
                AbstractKindIsSeparate = counterpart.FixtureKind != predecessor.FixtureKind,
                Notes = Notes(
                    "This is the CONSUMED #125 (FMP-07a) candidate fixture, not a claim that this checkout lacks it.",
                    "PredecessorSha256 and PredecessorBytes are the bytes THIS run observed. The predecessor is another work item's artifact, so its digest travels in this receipt rather than as a constant in this file.",
                    "WorldActualRadiusM (0.41 m, WorldBodyPresentation.NpcRadiusM) is the authored body size; AbstractUnitRadiusM (0.15 m) is the separate laboratory counterpart. The two are never interchanged.",
                    "What is consumed here is the predecessor's declared body radius, region scene manifest and geometry-load contract. The captured collider instances are the #125 PlayMode path and are not re-captured in EditMode.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-world-actual-fixture.json", receipt)), Is.True);
        }

        [Test]
        public void WorldActualRadiusColumnContactsTheDeclaredChokeBlockageWithoutPenetration()
        {
            var fixture = Build();
            var predecessor = LoadWorldCrowdFixture(out var loadFailure);
            Assert.That(predecessor, Is.Not.Null, loadFailure);

            // The radius is READ from the predecessor fixture, not written here, and it is asserted to be the
            // authored constant rather than this file's abstract 0.15 m laboratory unit radius.
            var radius = predecessor.Body.RadiusM;
            Assert.That(radius, Is.EqualTo(WorldBodyPresentation.NpcRadiusM).Within(1e-12),
                "The world-actual scenario must drive the radius the predecessor fixture declares.");
            Assert.That(radius, Is.Not.EqualTo(NpcRadiusM), "The 0.15 m abstract unit radius is a different body profile.");

            // Seal the declared 3.2 m choke with three pinned bodies at the world-actual radius. Consecutive
            // centres sit exactly one diameter (2r) apart, so the row is a touching seal with no overlap, and the
            // 3.2 - 3r = 0.37 m left at each pier is narrower than the 2r = 0.82 m a body of this radius needs to
            // pass. This is a people blockage inside the declared choke, not a closed portal or an invented wall.
            const int tickCount = 240;
            var blockingRowX = fixture.ChokeEntryX + (fixture.ChokeExitX - fixture.ChokeEntryX) / 3;
            var bodies = new List<CrowdAgent>();
            for (var i = -1; i <= 1; i++)
                bodies.Add(PinnedBody("seal" + (i + 1).ToString("D2"), blockingRowX, i * 2 * radius, radius));
            for (var lane = 0; lane < 3; lane++)
            for (var column = 0; column < 5; column++)
                bodies.Add(QueuedAtRadius("npc" + (lane * 5 + column).ToString("D2"),
                    174 + column * .9, -.9 + lane * .9, radius));

            var model = new CrowdMotionModel(ContactOnly(fixture.Definition), bodies.ToArray(), RecipeSeed);
            var peakInside = 0; var minimumCentre = double.PositiveInfinity;
            for (var tick = 0; tick < tickCount; tick++)
            {
                Assert.That(model.TryAdvance(StepSeconds, null, out var report), Is.True,
                    "world-actual choke tick " + tick + ": " + report.Failure);
                Assert.That(report.Committed, Is.True, "world-actual choke tick " + tick + " must commit.");
                var live = model.ExportSnapshot();
                // Per-tick, from the exported snapshot: a transient overlap between two committed ticks cannot
                // hide behind an end-of-window check.
                AssertNoPenetration(live, "world-actual radius choke tick " + tick);
                peakInside = Math.Max(peakInside, CountInsideChoke(live, fixture));
                minimumCentre = Math.Min(minimumCentre, MinimumCentreClearance(live));
            }
            var snapshot = model.ExportSnapshot();

            var minimumCentreToPinned = MinimumClearanceToPinned(snapshot);
            var maximumUnpinnedX = snapshot.Agents.Where(a => !a.Pinned).Max(a => a.Position.X);

            Assert.That(fixture.ChokeHalfWidthM - (2 * radius + radius), Is.LessThan(2 * radius),
                "The pinned seal must leave a gap too narrow for a body of the world-actual radius to pass.");
            Assert.That(peakInside, Is.GreaterThanOrEqualTo(6),
                "The column must actually crowd into the declared choke; measured peak " + peakInside + " bodies inside.");
            AssertContactWithAPinnedBody(snapshot, "world-actual radius column against the declared choke seal");
            Assert.That(minimumCentreToPinned, Is.LessThanOrEqualTo(ContactToleranceM));
            // Stop: no body of this radius may pass a people blockage standing inside the declared choke.
            Assert.That(maximumUnpinnedX, Is.LessThan(blockingRowX),
                "No body may pass the pinned seal standing inside the declared choke; furthest unpinned X was " + maximumUnpinnedX);

            var receipt = new WorldActualContactReceipt {
                DeclaredGeometryHash = fixture.DeclaredHash,
                BodyRadiusM = radius, SealSpacingM = 2 * radius,
                PierGapM = fixture.ChokeHalfWidthM - (2 * radius + radius), RequiredPassageWidthM = 2 * radius,
                BlockingRowX = blockingRowX, ChokeClearWidthM = fixture.ChokeClearWidthM,
                SimulatedSeconds = tickCount * StepSeconds, StepSeconds = StepSeconds,
                SealBodyCount = 3, ColumnBodyCount = 15, TickCount = tickCount,
                PeakBodiesInsideChoke = peakInside, FinalBodiesInsideChoke = CountInsideChoke(snapshot, fixture),
                MinimumCentreClearanceM = minimumCentre, MinimumWallClearanceM = MinimumWallClearance(snapshot),
                MinimumClearanceToPinnedM = minimumCentreToPinned,
                MaximumUnpinnedXM = maximumUnpinnedX, FreeRowX = fixture.ChokeEntryX,
                ContactAsserted = minimumCentreToPinned <= ContactToleranceM, NoBodyPassedTheSeal = maximumUnpinnedX < blockingRowX,
                Notes = Notes(
                    "BodyRadiusM is read from the #125 (FMP-07a) predecessor fixture (WorldBodyPresentation.NpcRadiusM), not from this file's 0.15 m abstract laboratory unit radius.",
                    "ContactAsserted is the measured centre-to-pin clearance against a one millimetre tolerance; it is the positive dual of the non-penetration assertion and fails when the scenario never reaches contact. It is measured under the contact-only variant (PersonRepulsion zeroed, range untouched): under the declared soft field the same column settles at the stand-off recorded by DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact.",
                    "Non-penetration is re-derived from the exported snapshot on every tick, not only at the end of the window.",
                    "The blockage is a pinned row of world-actual-radius bodies inside the declared choke, not a closed portal and not an invented wall.",
                    "Declared synthetic profile geometry; the authored scene collider capture is the #125 PlayMode path. No facility, railway or field validation is claimed.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-world-actual-contact.json", receipt)), Is.True);
        }

        [Serializable] private sealed class SoftStandOffReceipt
        {
            public string WorkId = "FMP-07c", Command = EditModeCommand, DeclaredGeometryHash;
            public double DeclaredPersonRepulsion, DeclaredPersonRepulsionRangeM;
            public double MeasuredSoftStandOffM, AuthoredRadiusM, ContactToleranceM, SimulatedSeconds;
            public int TickCount, ColumnBodyCount, SealBodyCount;
            public bool ContactReachedUnderTheDeclaredField, NoBodyPassedTheSeal;
            public string[] Notes;
        }

        /// <summary>The measured reason the contact cases need a contact-only variant. Under the DECLARED soft
        /// person-repulsion field an approaching body settles at a stand-off well outside contact, and this test
        /// asserts that rather than leaving it as a claim in a comment. It is also the negative control for the
        /// contact cases: if the declared field ever stopped holding bodies apart, the contact-only variant would
        /// no longer be measuring anything the field itself does not.</summary>
        [Test]
        public void DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact()
        {
            var fixture = Build();
            var predecessor = LoadWorldCrowdFixture(out var loadFailure);
            Assert.That(predecessor, Is.Not.Null, loadFailure);
            var radius = predecessor.Body.RadiusM;
            var field = fixture.Definition.Parameters;
            Assert.That(field.PersonRepulsionRangeM, Is.GreaterThan(0),
                "This negative control is only meaningful while the declared definition carries a soft repulsion field.");

            const int tickCount = 240;
            var blockingRowX = fixture.ChokeEntryX + (fixture.ChokeExitX - fixture.ChokeEntryX) / 3;
            var bodies = new List<CrowdAgent>();
            for (var i = -1; i <= 1; i++)
                bodies.Add(PinnedBody("seal" + (i + 1).ToString("D2"), blockingRowX, i * 2 * radius, radius));
            for (var lane = 0; lane < 3; lane++)
            for (var column = 0; column < 5; column++)
                bodies.Add(QueuedAtRadius("npc" + (lane * 5 + column).ToString("D2"),
                    174 + column * .9, -.9 + lane * .9, radius));

            var model = new CrowdMotionModel(fixture.Definition, bodies.ToArray(), RecipeSeed);
            for (var tick = 0; tick < tickCount; tick++)
            {
                Assert.That(model.TryAdvance(StepSeconds, null, out var report), Is.True,
                    "declared-field stand-off tick " + tick + ": " + report.Failure);
                AssertNoPenetration(model.ExportSnapshot(), "declared-field stand-off tick " + tick);
            }
            var snapshot = model.ExportSnapshot();
            var standOff = MinimumClearanceToPinned(snapshot);
            var maximumUnpinnedX = snapshot.Agents.Where(a => !a.Pinned).Max(a => a.Position.X);

            // The identical scenario that reaches contact under the contact-only variant does NOT reach it here.
            Assert.That(standOff, Is.GreaterThan(ContactToleranceM),
                "Under the declared soft repulsion field the queue is expected to settle short of contact; the " +
                "measured centre-to-pin clearance " + standOff + " m is within the " + ContactToleranceM +
                " m contact tolerance, so the contact-only variant is no longer an independent control.");
            Assert.That(standOff, Is.LessThanOrEqualTo(field.PersonRepulsionRangeM),
                "A body settled outside the declared repulsion range " + field.PersonRepulsionRangeM +
                " m is not being held by the declared field; measured " + standOff + " m.");
            // The stand-off is a stop, not a leak: nothing passes the seal even though contact is never reached.
            Assert.That(maximumUnpinnedX, Is.LessThan(blockingRowX),
                "The soft stand-off must still stop the column at the seal; furthest unpinned X was " + maximumUnpinnedX);

            var receipt = new SoftStandOffReceipt {
                DeclaredGeometryHash = fixture.DeclaredHash,
                DeclaredPersonRepulsion = field.PersonRepulsion, DeclaredPersonRepulsionRangeM = field.PersonRepulsionRangeM,
                MeasuredSoftStandOffM = standOff, AuthoredRadiusM = radius, ContactToleranceM = ContactToleranceM,
                SimulatedSeconds = tickCount * StepSeconds, TickCount = tickCount,
                ColumnBodyCount = 15, SealBodyCount = 3,
                ContactReachedUnderTheDeclaredField = standOff <= ContactToleranceM,
                NoBodyPassedTheSeal = maximumUnpinnedX < blockingRowX,
                Notes = Notes(
                    "This is the negative control for the contact cases: the same geometry and command that reaches contact under the contact-only variant settles short of it under the declared soft field.",
                    "Only the person-repulsion coefficient is zeroed in the contact-only variant; every dimension, wall, radius and repulsion range is the declared one.",
                    "MeasuredSoftStandOffM is derived from the exported snapshot, not read from the solver report.") };
            Assert.That(File.Exists(WriteReceipt("fmp07c-soft-standoff.json", receipt)), Is.True);
        }

        // ---------------------------------------------------------------- shared assertions ----

        private static CrowdRebind Rebind(CrowdMotionModel model)
        {
            var snapshot = model.ExportSnapshot();
            return new CrowdRebind { ExpectedTick = snapshot.Tick, ExpectedGeometryRevision = snapshot.GeometryRevision,
                Spaces = snapshot.Definition.Spaces, Walls = snapshot.Definition.Walls,
                Bodies = snapshot.Agents.Select(CrowdBodyBinding.FromAgent).ToArray() };
        }

        private static void AssertRejected(CrowdMotionModel model, CrowdSnapshot altered, string before, string context, List<string> reasons)
        {
            Assert.That(model.TryRestore(altered, out var failure), Is.False, "A same-epoch restore must refuse a " + context + " change.");
            Assert.That(failure, Is.Not.Empty, "A rejected restore must report why: " + context);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(before), "A rejected restore must not alter the " + context + " state.");
            reasons.Add(context + ": " + failure);
        }

        private static void AssertRejectedRebind(CrowdMotionModel model, string before, Action<CrowdRebind> mutate, string context)
        {
            var request = Rebind(model); mutate(request);
            Assert.That(model.TryRebind(request, out var failure), Is.False, "A rebind must refuse " + context + ".");
            Assert.That(failure, Is.Not.Empty, "A rejected rebind must report why: " + context);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(before), "A rejected rebind must not alter the " + context + " state.");
        }
    }
}
