using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    /// The declared contract of foundation/tests/FMP-07a/world-crowd-fixture.json. Every negative case this
    /// issue names is enforced here, so a mutated or mislabelled fixture fails before any captured geometry
    /// would be trusted. The live anchors (profile bytes, region scene manifest, authored mesh envelope) are
    /// passed in by the caller as measured values; nothing here is trusted from memory.
    /// </summary>
    internal static class WorldCrowdFixtureContract
    {
        internal const string RelativePath = "foundation/tests/FMP-07a/world-crowd-fixture.json";
        internal const string WorldActualKind = "world_actual_collider_nav";
        internal const string AbstractUnitKind = "abstract_unit_laboratory_corridor";
        /// <summary>
        /// The kind the world-geometry radius control is recorded under. It is deliberately NOT the
        /// laboratory corridor kind: the control is measured on the SAME captured thirteen-region world
        /// geometry with only the body radius changed, so recording it under the laboratory kind labels world
        /// measurements as laboratory ones - the reverse-direction violation the counterpart rule names.
        /// </summary>
        internal const string AbstractControlKind = "abstract_unit_radius_control_on_world_geometry";
        internal const int RegionCount = 13;
        internal const int NpcCount = 100;
        internal const double NpcRadiusM = .41;
        internal const double NpcHeightM = 1.8;
        internal const double AbstractUnitRadiusM = .15;
        internal const int MeasurementItemCount = 15;

        [Serializable] internal sealed class SourceProfile
        {
            public string Path, Sha256, ProfileId, Classification;
            public int SchemaVersion, GeometrySchemaVersion, Regions, Portals, Frames;
        }
        [Serializable] internal sealed class RegionScene
        {
            public string RegionId, SceneName, FrameId;
        }
        [Serializable] internal sealed class SceneHash
        {
            public string ManifestRule, ManifestSha256, ProfileSha256;
            public RegionScene[] RegionScenes;
        }
        [Serializable] internal sealed class GeometryLoad
        {
            public int LoadedRegionScenes, DeclaredRegionScenes, MinimumCapturedSupports, MinimumCapturedColliders;
            public bool MustMatchDeclaredRegionSetExactly, EveryRegionMustContributeAtLeastOneSupport,
                EveryCapturedColliderMustBePresentEnabledAndUnchanged, NoColliderMayBeSilentlySkipped,
                DeclaredStructuralCollidersAreASubsetOfTheCapture;
            public string MissingRegionPolicy, UnloadedColliderPolicy, AuthoredGeometryRule;
        }
        [Serializable] internal sealed class Body
        {
            public string Kind, RadiusConstant;
            public int Count, PlayerCount;
            public double RadiusM, HeightM, PreferredSpeedMS, AbstractUnitRadiusM, PlayerRadiusM;
        }
        [Serializable] internal sealed class Placement
        {
            public string Method, InitialOverlapPolicy;
            public double InitialOverlapMarginM;
            public int[] PerRegionCounts;
            public int RegionsCovered;
            public double SlotSpacingM;
        }
        [Serializable] internal sealed class RegressionAssertions
        {
            public bool EveryTickCommitted, AllBodiesStayInTheWorldContactSpace, MinimumSweptGapMNonNegative,
                MinimumCentreClearanceMNonNegative, MaximumGroundSpeedAtMostPreferredSpeed,
                NoBodyEverIntersectsAnAuthoredCollider, BodyCountAndRadiiUnchangedAtTheEnd,
                MinimumSweptGapMMustBeFinite, MinimumCentreClearanceMMustBeFinite, MeasuredPairCountPositive,
                MaximumGroundSpeedWithinCommandedSpeed;
            public double CommandedSpeedMS, MaximumGroundSpeedToleranceMS;
            public int RegionCoverageAfterRun;
        }
        [Serializable] internal sealed class Regression
        {
            public string ExecutableTest;
            public double TickSeconds, SimulatedSeconds, DesiredVelocityX, DesiredVelocityZ;
            public int Ticks, PinnedEveryNthBody;
            public RegressionAssertions Assertions;
        }
        [Serializable] internal sealed class AbstractCounterpart
        {
            public string FixtureKind, Rule, DeletionPolicy, ReverseDirectionRule, LaboratoryCorridorKind;
            public bool ActualGeometry, RecordedSeparately;
            public double RadiusM, HeightM, PersonRepulsion, RepulsionRangeM;
            public string[] Sources;
        }
        [Serializable] internal sealed class NegativeContract
        {
            public string MissingRegion, UnloadedCollider, WrongFrame, UndersizedRadius, MislabeledAbstract, Deletion, NonExecution,
                MissingRegionGeometry, WrongGeometryFrame, UndeclaredStructuralCollider, UndersizedRadiusNavLeak;
        }
        /// <summary>An axis-aligned authored box in profile coordinates: the world AABB of the eight
        /// authored corners. Flat, unrotated authored boxes (floor, exclusion bed, lintel, ceiling) declare
        /// their top elevation in TopY, which is also the supporting plane's elevation.</summary>
        [Serializable] internal sealed class AuthoredBox
        {
            public double MinX, MaxX, MinZ, MaxZ, BottomY, TopY;
        }
        [Serializable] internal sealed class AuthoredRegionEnvelope
        {
            public double CenterX, CenterY, CenterZ, SizeX, SizeZ, HeightM, WallHeightM;
            public bool Ceiling;
            public string EquipmentAsset;
        }
        [Serializable] internal sealed class AuthoredFloorSupport
        {
            public string SurfaceId;
            public double MinX, MaxX, MinZ, MaxZ, TopY;
        }
        [Serializable] internal sealed class AuthoredWallSegment
        {
            public bool AlongX;
            public double Edge, Start, End;
        }
        [Serializable] internal sealed class AuthoredBoundaryWalls
        {
            public string ColliderRole;
            public double ThicknessM, HeightM, SolidLengthM;
            public int SegmentCount;
            public AuthoredWallSegment[] Segments;
        }
        [Serializable] internal sealed class AuthoredLintel
        {
            public string PortalId;
            public bool AlongX;
            public double MinX, MaxX, MinZ, MaxZ, BottomY, TopY;
        }
        [Serializable] internal sealed class AuthoredCeilingCollider
        {
            public bool Present;
            public double MinX, MaxX, MinZ, MaxZ, BottomY, TopY;
        }
        [Serializable] internal sealed class AuthoredPassageSupport
        {
            public string PortalId, SurfaceId, FrameId, FloorColliderRole;
            public AuthoredSupportRect Support;
            public AuthoredBox Collider;
            public double ClearWidthM, ClearHeightM, WallHeightM, LengthM, GradeRise;
            public int GuardPrismCount, GuardPrismVertexCount;
            public bool PassageCeiling, DoorPanelCollider, StaticBoarding;
        }
        /// <summary>The upper face of an authored walkable solid: the AABB of its four upper corners, and the
        /// elevation of that plane at the centre of the rectangle. On a sloped passage the upper face is tilted,
        /// so its elevation at the centre is not the same as the collider AABB, whose top is the highest corner.</summary>
        [Serializable] internal sealed class AuthoredSupportRect
        {
            public double MinX, MaxX, MinZ, MaxZ, CentreElevationM;
        }
        /// <summary>An authored procedural furniture box whose count and AABB are pinned by the builder's own
        /// loop bounds rather than by a float32 accumulation step, so it is declared exactly.</summary>
        [Serializable] internal sealed class AuthoredFurnitureBox
        {
            public string Role;
            public double MinX, MaxX, MinZ, MaxZ, BottomY, TopY;
        }
        [Serializable] internal sealed class AuthoredRegion
        {
            public string RegionId, SceneName, FrameId, Template;
            public AuthoredRegionEnvelope Envelope;
            public AuthoredFloorSupport FloorSupport;
            public AuthoredBox[] NonWalkableBeds;
            public AuthoredBoundaryWalls BoundaryWalls;
            public AuthoredLintel[] PortalLintels;
            public AuthoredCeilingCollider CeilingCollider;
            public string[] WalkableSupportIds;
            public AuthoredPassageSupport[] PassageSupports;
            public AuthoredFurnitureBox[] DerivedFurnitureBoxes;
            public string[] AuthoredColliderRolesWithDerivedCount, MeshDerivedColliderRoles;
        }
        [Serializable] internal sealed class SourceAnchor
        {
            public string Path, Sha256, Role;
        }
        [Serializable] internal sealed class AuthoredGeometry
        {
            public int SchemaVersion, DeclaredWalkableSupportCount;
            public string Rule, CoordinateSpace, NotEnumeratedRule, SlopedSupportRule, MeshDerivedRule;
            public double ToleranceM;
            public SourceAnchor SourceBuilder;
            public AuthoredRegion[] Regions;
        }
        [Serializable] internal sealed class NavGeometry
        {
            public string Rule, SourcePath, SourceSha256, Builder, SettingsSource, SlopeConstant, SlopeConstantSha256,
                QueryOffsetAxis, QueryOffsetRule, CacheKeyRule, SourceSelectionRule, BoundsRule, BodySizeSource,
                BodySizeSourceSha256, WorldActualQueryKey, AbstractUnitQueryKey, NegativeRule;
            public double AgentRadiusMarginM, AgentRadiusForWorldActualM, AgentRadiusForAbstractUnitM, AgentHeightM,
                AgentClimbM, AgentSlopeDegrees, VoxelSizeM, MinRegionAreaM2, QueryOffsetStepM, SampleToleranceM;
            public bool OverrideVoxelSize, OverrideTileSize, RequirePathComplete;
            public int TileSize, WalkableArea, BarrierArea;
        }
        [Serializable] internal sealed class Fixture
        {
            public int SchemaVersion;
            public string WorkId, Title, Classification, GeometricClassification, FixtureKind, ContractNature;
            public long Seed;
            public SourceProfile SourceProfile;
            public SceneHash SceneHash;
            public GeometryLoad GeometryLoad;
            public AuthoredGeometry AuthoredGeometry;
            public NavGeometry NavGeometry;
            public Body Body;
            public Placement Placement;
            public Regression Regression;
            public AbstractCounterpart AbstractCounterpart;
            public NegativeContract NegativeContract;
            public string[] MeasurementItems;
        }

        internal static Fixture Load(string projectRoot) =>
            JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(projectRoot, RelativePath)));

        /// <summary>False with the first violated clause, so a mutation reports which negative case it broke.</summary>
        internal static bool Validate(Fixture fixture, ConnectedWorldDefinition world, string liveProfileSha256,
            string liveManifestSha256, double measuredModelEnvelopeM, out string failure)
        {
            failure = "";
            if (fixture == null || fixture.SchemaVersion != 1) { failure = "Fixture schema version."; return false; }
            if (fixture.FixtureKind != WorldActualKind)
            { failure = "Fixture kind is not the world actual collider/nav kind: '" + fixture.FixtureKind + "'."; return false; }
            if (fixture.Classification != "PUBLIC_PROJECT_EVIDENCE") { failure = "Fixture classification."; return false; }
            if (fixture.SourceProfile == null || fixture.SourceProfile.Sha256 != liveProfileSha256)
            { failure = "Declared profile hash changed."; return false; }
            if (fixture.SourceProfile.Regions != RegionCount || fixture.SourceProfile.Portals != 12 || fixture.SourceProfile.Frames != 3)
            { failure = "Declared profile topology is not thirteen regions, twelve portals, three frames."; return false; }
            if (fixture.SceneHash == null || fixture.SceneHash.ManifestSha256 != liveManifestSha256)
            { failure = "Region scene manifest changed."; return false; }
            if (fixture.SceneHash.RegionScenes == null || fixture.SceneHash.RegionScenes.Length != RegionCount)
            { failure = "Missing region scene in fixture: expected " + RegionCount + "."; return false; }
            if (fixture.SceneHash.RegionScenes.Select(r => r.RegionId).Distinct().Count() != RegionCount)
            { failure = "Duplicate region in the fixture scene manifest."; return false; }
            foreach (var region in world.Regions)
            {
                var declared = fixture.SceneHash.RegionScenes.SingleOrDefault(r => r.RegionId == region.Id);
                if (declared == null) { failure = "Missing region " + region.Id + "."; return false; }
                if (declared.SceneName != region.SceneName) { failure = "Wrong scene for " + region.Id + "."; return false; }
                if (declared.FrameId != region.FrameId) { failure = "Wrong frame for " + region.Id + ": '" + declared.FrameId + "'."; return false; }
            }
            if (fixture.GeometryLoad == null || fixture.GeometryLoad.LoadedRegionScenes != RegionCount ||
                fixture.GeometryLoad.DeclaredRegionScenes != RegionCount || !fixture.GeometryLoad.MustMatchDeclaredRegionSetExactly ||
                !fixture.GeometryLoad.EveryRegionMustContributeAtLeastOneSupport ||
                !fixture.GeometryLoad.EveryCapturedColliderMustBePresentEnabledAndUnchanged ||
                !fixture.GeometryLoad.NoColliderMayBeSilentlySkipped ||
                !fixture.GeometryLoad.DeclaredStructuralCollidersAreASubsetOfTheCapture ||
                string.IsNullOrEmpty(fixture.GeometryLoad.AuthoredGeometryRule))
            { failure = "Geometry load must be the declared thirteen region scenes with every authored collider present and unchanged."; return false; }
            // The authored collider and navigation geometry of the thirteen regions must be DECLARED here,
            // not merely policied. A fixture that only carries rules cannot be compared against a capture.
            var geometry3d = fixture.AuthoredGeometry;
            if (geometry3d == null || geometry3d.Regions == null || geometry3d.Regions.Length != RegionCount)
            { failure = "Missing region geometry: AuthoredGeometry must declare all " + RegionCount + " regions, found " +
                (geometry3d?.Regions?.Length ?? 0) + "."; return false; }
            if (geometry3d.Regions.Select(r => r.RegionId).Distinct().Count() != RegionCount)
            { failure = "Duplicate region geometry entry in AuthoredGeometry."; return false; }
            if (geometry3d.SchemaVersion != 1 || geometry3d.ToleranceM <= 0 ||
                geometry3d.SourceBuilder == null || string.IsNullOrEmpty(geometry3d.SourceBuilder.Path) ||
                geometry3d.SourceBuilder.Sha256 == null || geometry3d.SourceBuilder.Sha256.Length != 64 ||
                string.IsNullOrEmpty(geometry3d.Rule) || string.IsNullOrEmpty(geometry3d.CoordinateSpace) ||
                string.IsNullOrEmpty(geometry3d.NotEnumeratedRule) || string.IsNullOrEmpty(geometry3d.SlopedSupportRule) ||
                string.IsNullOrEmpty(geometry3d.MeshDerivedRule))
            { failure = "Authored geometry must declare its provenance, tolerance and non-enumerated rule."; return false; }
            var declaredSupportTotal = 0;
            foreach (var region in world.Regions)
            {
                var authored = geometry3d.Regions.SingleOrDefault(r => r.RegionId == region.Id);
                if (authored == null) { failure = "Missing region " + region.Id + " geometry."; return false; }
                if (authored.SceneName != region.SceneName) { failure = "Wrong geometry scene for " + region.Id + "."; return false; }
                if (authored.FrameId != region.FrameId)
                { failure = "Wrong frame for " + region.Id + " in AuthoredGeometry: '" + authored.FrameId + "'."; return false; }
                // The authored profile numbers are float32 in Unity: the declaration is compared after the same
                // rounding, so a value the profile cannot represent exactly (3.1 m of carriage wall) is still
                // an exact match rather than a binary-float artefact.
                if (authored.Envelope == null || authored.Envelope.SizeX <= 0 || authored.Envelope.SizeZ <= 0 ||
                    (float)authored.Envelope.CenterX != region.Center.X || (float)authored.Envelope.CenterY != region.Center.Y ||
                    (float)authored.Envelope.CenterZ != region.Center.Z || (float)authored.Envelope.WallHeightM != region.WallHeight)
                { failure = "Authored envelope for " + region.Id + " is not the authored region rectangle."; return false; }
                if (authored.FloorSupport == null || authored.FloorSupport.SurfaceId != "floor." + region.Id ||
                    authored.FloorSupport.MinX >= authored.FloorSupport.MaxX || authored.FloorSupport.MinZ >= authored.FloorSupport.MaxZ ||
                    (float)authored.FloorSupport.TopY != region.Center.Y)
                { failure = "Authored floor support for " + region.Id + " is not a well-formed authored rectangle."; return false; }
                if (authored.WalkableSupportIds == null || authored.WalkableSupportIds.Length == 0 ||
                    authored.WalkableSupportIds.Any(string.IsNullOrEmpty) ||
                    authored.WalkableSupportIds.Distinct().Count() != authored.WalkableSupportIds.Length)
                { failure = "Authored walkable support ids for " + region.Id + " are incomplete or duplicated."; return false; }
                var walls = authored.BoundaryWalls;
                if (walls == null || walls.Segments == null || walls.ThicknessM <= 0 || (float)walls.HeightM != region.WallHeight ||
                    walls.SegmentCount != walls.Segments.Length ||
                    Math.Abs(walls.Segments.Sum(s => s.End - s.Start) - walls.SolidLengthM) > 1e-4 ||
                    walls.Segments.Any(s => s.End - s.Start < .02 || !s.AlongX && Math.Abs(s.Edge) > region.SizeX))
                { failure = "Authored boundary wall envelope for " + region.Id + " is inconsistent."; return false; }
                var expectedWalls = region.WallHeight > 0 && walls.SegmentCount > 0;
                if (walls.ColliderRole != (region.Template == "vehicle" ? "Provisional_CarriageShell" : "BoundaryWall") && expectedWalls)
                { failure = "Authored boundary wall role for " + region.Id + " is not the authored role."; return false; }
                if (authored.CeilingCollider == null || authored.CeilingCollider.Present != region.Ceiling)
                { failure = "Authored ceiling collider for " + region.Id + " does not match the authored ceiling."; return false; }
                if ((authored.PassageSupports ?? Array.Empty<AuthoredPassageSupport>()).Any(p => p == null || p.Support == null || p.Collider == null ||
                    p.Support.MinX >= p.Support.MaxX || p.Support.MinZ >= p.Support.MaxZ || p.Collider.BottomY >= p.Collider.TopY))
                { failure = "Authored passage support for " + region.Id + " is not a well-formed authored rectangle."; return false; }
                if ((authored.DerivedFurnitureBoxes ?? Array.Empty<AuthoredFurnitureBox>()).Any(b => b == null || string.IsNullOrEmpty(b.Role) ||
                    b.MinX >= b.MaxX || b.MinZ >= b.MaxZ || b.BottomY >= b.TopY) ||
                    !(authored.DerivedFurnitureBoxes ?? Array.Empty<AuthoredFurnitureBox>()).All(b =>
                        authored.AuthoredColliderRolesWithDerivedCount.Contains(b.Role)) ||
                    authored.MeshDerivedColliderRoles == null || authored.MeshDerivedColliderRoles.Length == 0)
                { failure = "Authored derived furniture box for " + region.Id + " is not a well-formed declared role."; return false; }
                var supportIds = new[] { "floor." + region.Id }
                    .Concat((authored.PassageSupports ?? Array.Empty<AuthoredPassageSupport>()).Select(p => p.SurfaceId)).ToArray();
                if (!supportIds.SequenceEqual(authored.WalkableSupportIds))
                { failure = "Authored walkable support ids for " + region.Id + " are not the floor and passage surfaces."; return false; }
                declaredSupportTotal += authored.WalkableSupportIds.Length;
            }
            if (declaredSupportTotal != geometry3d.DeclaredWalkableSupportCount)
            { failure = "The declared authored walkable support count is not the sum of the per-region ids."; return false; }
            var navigation = fixture.NavGeometry;
            var margin = navigation?.AgentRadiusMarginM ?? 0;
            if (navigation == null || navigation.AgentSlopeDegrees != GroundedWorldMotor.MaximumSlopeDegrees ||
                navigation.AgentHeightM != NpcHeightM || margin <= 0 || navigation.AgentClimbM <= 0 ||
                navigation.VoxelSizeM <= 0 || navigation.TileSize <= 0 || navigation.MinRegionAreaM2 <= 0 ||
                navigation.SampleToleranceM <= 0 || navigation.QueryOffsetStepM <= 0 || navigation.WalkableArea != 0 ||
                navigation.BarrierArea != 1 || !navigation.OverrideVoxelSize || !navigation.OverrideTileSize ||
                Math.Abs(navigation.AgentRadiusForWorldActualM - (NpcRadiusM + margin)) > 1e-12 ||
                Math.Abs(navigation.AgentRadiusForAbstractUnitM - (AbstractUnitRadiusM + margin)) > 1e-12 ||
                !(navigation.AgentRadiusForWorldActualM > navigation.AgentRadiusForAbstractUnitM) ||
                navigation.SourceSha256 == null || navigation.SourceSha256.Length != 64 ||
                navigation.SlopeConstant != "GroundedWorldMotor.MaximumSlopeDegrees" ||
                navigation.SlopeConstantSha256 == null || navigation.SlopeConstantSha256.Length != 64 ||
                string.IsNullOrEmpty(navigation.SourceSelectionRule) || string.IsNullOrEmpty(navigation.NegativeRule))
            { failure = "The declared navigation geometry is not the runtime path-query configuration."; return false; }
            if (fixture.Body == null || fixture.Body.Kind != "npc" || fixture.Body.Count != NpcCount || fixture.Body.PlayerCount != 0)
            { failure = "Body roster must be exactly " + NpcCount + " authored npc bodies and no players."; return false; }
            if (fixture.Body.RadiusM < measuredModelEnvelopeM)
            { failure = "Radius " + fixture.Body.RadiusM.ToString("R", CultureInfo.InvariantCulture) +
                " is smaller than the authored model envelope " + measuredModelEnvelopeM.ToString("R", CultureInfo.InvariantCulture) + "."; return false; }
            if (fixture.Body.RadiusM != NpcRadiusM || fixture.Body.RadiusM != WorldBodyPresentation.NpcRadiusM)
            { failure = "World actual radius must equal the authored NPC radius constant."; return false; }
            if (fixture.Body.HeightM != NpcHeightM || fixture.Body.HeightM != WorldBodyPresentation.NpcHeightM)
            { failure = "World actual body height is not the declared contract."; return false; }
            if (fixture.Body.AbstractUnitRadiusM != AbstractUnitRadiusM || AbstractUnitRadiusM >= measuredModelEnvelopeM)
            { failure = "The abstract unit radius must stay below the authored model envelope."; return false; }
            // The path query key is the body size the run is actually measured at. A shrink that kept the
            // world actual label but re-used the abstract key would find the abstract unit's navmesh, so the
            // keys must differ and must be the keys the declared body size produces.
            var worldKey = fixture.Body.RadiusM.ToString("R", CultureInfo.InvariantCulture) + "/" +
                fixture.Body.HeightM.ToString("R", CultureInfo.InvariantCulture);
            var abstractKey = fixture.Body.AbstractUnitRadiusM.ToString("R", CultureInfo.InvariantCulture) + "/" +
                fixture.Body.HeightM.ToString("R", CultureInfo.InvariantCulture);
            if (navigation.WorldActualQueryKey != worldKey || navigation.AbstractUnitQueryKey != abstractKey ||
                navigation.WorldActualQueryKey == navigation.AbstractUnitQueryKey)
            { failure = "The declared navigation query keys are not the declared body sizes."; return false; }
            if (fixture.Placement == null || fixture.Placement.PerRegionCounts == null ||
                fixture.Placement.PerRegionCounts.Length != RegionCount ||
                fixture.Placement.PerRegionCounts.Any(count => count <= 0) ||
                fixture.Placement.PerRegionCounts.Sum() != NpcCount || fixture.Placement.RegionsCovered != RegionCount)
            { failure = "Placement must cover all thirteen regions and place exactly " + NpcCount + " bodies."; return false; }
            if (fixture.Regression == null ||
                fixture.Regression.ExecutableTest != "Fmp07aWorldCrowdTests.Npc100OnActualAuthoredGeometryAdvancesAndEmitsTheFixtureReceipt" ||
                fixture.Regression.Ticks <= 0 || fixture.Regression.TickSeconds <= 0 || fixture.Regression.PinnedEveryNthBody <= 0 ||
                fixture.Regression.Assertions == null || !fixture.Regression.Assertions.EveryTickCommitted ||
                !fixture.Regression.Assertions.NoBodyEverIntersectsAnAuthoredCollider ||
                !fixture.Regression.Assertions.AllBodiesStayInTheWorldContactSpace ||
                fixture.Regression.Assertions.RegionCoverageAfterRun != RegionCount)
            { failure = "The declared executable regression is not the world actual roster test."; return false; }
            if (fixture.MeasurementItems == null || fixture.MeasurementItems.Length != MeasurementItemCount ||
                fixture.MeasurementItems.Any(string.IsNullOrEmpty))
            { failure = "The declared measurement item list is incomplete."; return false; }
            if (fixture.AbstractCounterpart == null || fixture.AbstractCounterpart.FixtureKind == WorldActualKind ||
                fixture.AbstractCounterpart.FixtureKind == AbstractUnitKind ||
                fixture.AbstractCounterpart.ActualGeometry || !fixture.AbstractCounterpart.RecordedSeparately ||
                fixture.AbstractCounterpart.RadiusM >= measuredModelEnvelopeM ||
                fixture.AbstractCounterpart.Sources == null || fixture.AbstractCounterpart.Sources.Length == 0 ||
                string.IsNullOrEmpty(fixture.AbstractCounterpart.DeletionPolicy))
            { failure = "Abstract counterpart must be a different kind and must not claim actual geometry."; return false; }
            if (string.IsNullOrEmpty(fixture.AbstractCounterpart.Rule) ||
                !fixture.AbstractCounterpart.Rule.Contains("fixtureIdentitySha256"))
            { failure = "The abstract counterpart rule must name the separation axis the records implement."; return false; }
            if (fixture.Placement.InitialOverlapMarginM <= 0 ||
                fixture.Regression.Assertions.CommandedSpeedMS <= 0 ||
                fixture.Regression.Assertions.MaximumGroundSpeedToleranceMS <= 0 ||
                !fixture.Regression.Assertions.MinimumSweptGapMMustBeFinite ||
                !fixture.Regression.Assertions.MinimumCentreClearanceMMustBeFinite ||
                !fixture.Regression.Assertions.MeasuredPairCountPositive)
            { failure = "The fixture must declare a positive overlap margin, a commanded speed bound and the finiteness rules."; return false; }
            if (fixture.NegativeContract == null || new[] { fixture.NegativeContract.MissingRegion, fixture.NegativeContract.UnloadedCollider,
                fixture.NegativeContract.WrongFrame, fixture.NegativeContract.UndersizedRadius,
                fixture.NegativeContract.MislabeledAbstract, fixture.NegativeContract.Deletion,
                fixture.NegativeContract.NonExecution, fixture.NegativeContract.MissingRegionGeometry,
                fixture.NegativeContract.WrongGeometryFrame, fixture.NegativeContract.UndeclaredStructuralCollider,
                fixture.NegativeContract.UndersizedRadiusNavLeak }.Any(string.IsNullOrEmpty))
            { failure = "The fixture must declare every negative clause this work names."; return false; }
            return true;
        }
    }

    /// <summary>
    /// FMP-07a (#125): an NPC100 crowd fixture on the ACTUAL authored collider/navigation geometry of the
    /// thirteen connected-world region scenes, with the actual NPC body size, kept separate from the
    /// abstract-unit fixtures.
    ///
    /// GEOMETRY PROVENANCE. Nothing here is derived from the declared profile's envelopes. The thirteen region
    /// scenes are generated by ConnectedWorldSceneBuilder.Build into an isolated folder, opened additively, and
    /// ConnectedWorldGeometry.Capture reads the real BoxCollider/MeshCollider instances out of those loaded
    /// scenes together with their authored transforms. Every body position is chosen by
    /// ConnectedWorldGeometry.TryFindStandingPoint against those captured colliders, so no coordinate and no
    /// support plane is invented.
    ///
    /// BODY SIZE. The roster radius is WorldBodyPresentation.NpcRadiusM (.41 m). The authored Evacuee envelope
    /// is measured from Assets/CHOOguardArt/Blender/Evacuee.fbx at run time and the fixture radius must not be
    /// smaller than it. The abstract-unit fixtures use the fixed .15 m laboratory corridor calibration radius;
    /// they are a different fixture kind and are neither re-run as this kind nor re-labelled here.
    ///
    /// KIND SEPARATION. The fixture declares fixtureKind = world_actual_collider_nav. Its abstract counterpart
    /// is declared as abstract_unit_radius_control_on_world_geometry with actualGeometry = false, and the
    /// pre-existing laboratory calibration fixture kind abstract_unit_laboratory_corridor is named inside that
    /// counterpart so the two controls are never confused for each other. Both rosters are measured
    /// with the SAME measurement item list and recorded under DIFFERENT kinds and DIFFERENT fixture hashes.
    ///
    /// AUTHORED GEOMETRY. This file declares the authored collider and navigation geometry of all thirteen
    /// regions, computed from foundation/world/connected-world-profile.json as the builder's own arithmetic;
    /// the declared values are checked against the captured ConnectedWorldGeometry region by region.
    ///
    /// NEGATIVE CONTRACT: a capture missing a region, a hidden unloaded/disabled collider, a wrong or moving
    /// frame, a radius below the authored model, and an abstract-labeled-as-actual record must all fail.
    /// </summary>
    public sealed class Fmp07aWorldCrowdTests
    {
        private const string ProfilePath = ConnectedWorldSceneBuilder.DefinitionPath;
        private const string AuthoredModelPath = "Assets/CHOOguardArt/Blender/Evacuee.fbx";
        private const string ReceiptDirectory = "Temp/ChooGuardWorldCrowd";
        private const string Command = "-batchmode -nographics -projectPath . -runTests -testPlatform EditMode " +
            "-testFilter \"ChooGuard.Foundation.Multiplayer.Tests.WorldMotionAdapterTests;ChooGuard.Foundation.Tests.CrowdWorldContactTests;" +
            "ChooGuard.Foundation.Multiplayer.Tests.Fmp07aWorldCrowdTests\" " +
            "-testResults \"$EVIDENCE_DIR/FMP-07a-edit.xml\" -logFile \"$EVIDENCE_DIR/FMP-07a-unity.log\"";

        // Frozen identity of the declared world profile bytes. A profile edit must break these anchors.
        private const string AuthoredProfileSha256 = "e2cae663ce04b63b197c99dda7a26a394619430e47cb071834254c3d23f94cba";
        private const string AuthoredSceneManifestSha256 = "830d9bfadcaace0d768360ed5f43be3d8fbaa605ed7b34c1ad3cbfb9abed9541";

        // Declared recipe. Pinned so a fixture edit cannot silently reshape the roster.
        private const long WorldSeed = 107;
        private const double TickSeconds = .05;
        private const int TickCount = 20;
        private const double CommandX = .2, CommandZ = .05;
        private const int PinnedEveryNthBody = 10;
        private static readonly int[] PerRegionCounts = { 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7 };

        // sqrt(.2^2 + .05^2): the magnitude this run actually commands. The ground-speed bound is tied to it,
        // never to the declared PreferredSpeedMS (1.5), which the command never approaches.
        private static readonly double CommandedSpeedMS = Math.Sqrt(CommandX * CommandX + CommandZ * CommandZ);

        // Files that carry the abstract-unit and mixed-roster fixtures this work must not delete or re-label
        // (non-goal), each with the body-size anchor that must survive in it. The anchor differs per file
        // because only the two abstract fixtures use the .15 m calibration radius; the third is the mixed
        // world-motion roster and keeps the authored NPC radius constant.
        private sealed class AbstractFixtureSource
        {
            public string Path, Anchor;
        }
        private static readonly AbstractFixtureSource[] AbstractFixtureSources =
        {
            new AbstractFixtureSource { Path = "Packages/com.xrlab.chooguard.foundation/Tests/Editor/Fmp07cContactRestoreTests.cs", Anchor = ".15" },
            new AbstractFixtureSource { Path = "Packages/com.xrlab.chooguard.foundation/Tests/Editor/CrowdBenchmarkTests.cs", Anchor = ".15" },
            new AbstractFixtureSource { Path = "Packages/com.xrlab.chooguard.foundation/Multiplayer/Tests/Editor/WorldMotionAdapterTests.cs", Anchor = "WorldBodyPresentation.NpcRadiusM" }
        };

        [Serializable] private sealed class RunReceipt
        {
            public string fixtureKind, fixtureIdentitySha256, executableTest, sourceProfileSha256, sceneManifestSha256, geometrySignature;
            public int bodyCount, distinctRegionCount, committedTicks, supportCount, colliderCount, wallCount, pinnedCount,
                sweptGapSamples, centreClearanceSamples;
            public long seed, coupledSteps;
            public double radiusM, heightM, tickSeconds, simulatedSeconds, maximumGroundSpeedMS, minimumSweptGapM, minimumCentreClearanceM;
            public string[] measurementItems, contactSpaceIds, regionIds, sceneFilePaths, sceneFileSha256;
        }
        [Serializable] private sealed class SeparationReceipt
        {
            public int schemaVersion = 1;
            public string workId = "FMP-07a", classification = "PUBLIC_PROJECT_EVIDENCE", receiptKind = "declared_fixture_plus_measured_run";
            public string fixturePath, fixtureSha256, fixtureKind, separationRule, command;
            public RunReceipt actualFixture, abstractUnitCounterpart;
        }

        private string folder;
        private SceneSetup[] previous;
        private ConnectedWorldDefinition world;
        private ConnectedWorldGeometry geometry;
        private ConnectedRegionView[] views;
        private string[] scenePaths;
        private List<WorldBodySpawn> actualRoster;

        [OneTimeSetUp] public void Build()
        {
            previous = EditorSceneManager.GetSceneManagerSetup();
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Where(i => SceneManager.GetSceneAt(i).isDirty)
                .Select(i => SceneManager.GetSceneAt(i).name).ToArray();
            // Hard failure, never Assert.Ignore: a skip turns all sixteen cases - the negative contracts
            // included - green-invisible while the runner still exits with failed=0. An unrun suite must be
            // indistinguishable from a failing one, so refusing to run is a failure of every case here.
            Assert.That(dirty, Is.Empty,
                "Refusing to run: this fixture generates and deletes scenes and must not do that over unsaved editor state. " +
                "Dirty scenes: " + string.Join(", ", dirty) + ". EditMode scenes must be saved first.");
            folder = "Assets/CHOOguardGenerated/WorldCrowdTest_" + Guid.NewGuid().ToString("N");
            scenePaths = ConnectedWorldSceneBuilder.Build(folder);
            foreach (var path in scenePaths.Skip(1)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ProfilePath)); world.Validate();
            LoadedWorld = world; LoadedSceneManifest = SceneManifest(world);
            Physics.SyncTransforms();
            views = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None);
            geometry = ConnectedWorldGeometry.Capture(world, views);
            actualRoster = PlaceRoster();
        }

        [OneTimeTearDown] public void Clear()
        {
            geometry?.Dispose();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (previous != null && previous.Any(s => s.isLoaded && s.isActive) && previous.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }

        // ------------------------------------------------------------------ anchors and helpers ----

        private static string Root => Directory.GetParent(Application.dataPath).FullName;
        private static string ProjectFile(string relative) => Path.Combine(Root, relative);
        /// <summary>
        /// The fixture is read with the same JsonUtility path the rest of the project uses for
        /// connected-world-profile.json, whose keys are PascalCase. JsonUtility matches keys by exact name,
        /// so a camelCase fixture deserialises to a fully default object instead of failing loudly: every
        /// declared bound would then read as zero/absent. Pin the parse here so an unreadable fixture reports
        /// itself as unreadable rather than as a roster of zeros.
        /// </summary>
        private static WorldCrowdFixtureContract.Fixture Fixture()
        {
            var fixture = WorldCrowdFixtureContract.Load(Root);
            Assert.That(fixture, Is.Not.Null, "The world crowd fixture must parse.");
            Assert.That(fixture.SchemaVersion, Is.EqualTo(1),
                "The fixture must deserialise under the project's PascalCase JSON key convention; " +
                "a key that does not match the field name deserialises to a default object.");
            Assert.That(fixture.WorkId, Is.EqualTo("FMP-07a"), "The fixture must declare this work id.");
            return fixture;
        }
        private static string LiveProfileHash() => FileHash(ProjectFile(ProfilePath));

        private static string Hash(string text)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(new UTF8Encoding(false).GetBytes(text)).Select(b => b.ToString("x2")));
        }
        private static string FileHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        /// <summary>The exact rule the fixture declares: one 'regionId|sceneName|frameId' line per declared
        /// region in profile order, joined by '\n' with a trailing '\n', hashed as UTF-8.</summary>
        private static string SceneManifest(ConnectedWorldDefinition definition) =>
            Hash(string.Join("\n", definition.Regions.Select(r => r.Id + "|" + r.SceneName + "|" + r.FrameId)) + "\n");

        /// <summary>The real-model oracle: the maximum horizontal vertex radius of the authored Evacuee mesh.</summary>
        private static double MeasureAuthoredEnvelopeM()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AuthoredModelPath);
            Assert.That(model, Is.Not.Null, AuthoredModelPath);
            var maximum = 0.0;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                var p = model.transform.worldToLocalMatrix.MultiplyPoint3x4(filter.transform.localToWorldMatrix.MultiplyPoint3x4(vertex));
                maximum = Math.Max(maximum, Math.Sqrt(p.x * p.x + p.z * p.z));
            }
            return maximum;
        }

        // Bound once in OneTimeSetUp so the fixture validator always compares against the declaration that was
        // actually loaded into this run, never against a remembered literal.
        private static ConnectedWorldDefinition LoadedWorld;
        private static string LoadedSceneManifest;

        private static bool FixtureFails(WorldCrowdFixtureContract.Fixture fixture, double envelope, out string failure) =>
            !WorldCrowdFixtureContract.Validate(fixture, LoadedWorld, LiveProfileHash(), LoadedSceneManifest, envelope, out failure);

        private List<WorldBodySpawn> PlaceRoster()
        {
            var spawns = new List<WorldBodySpawn>();
            for (var index = 0; index < world.Regions.Length; index++)
            {
                var region = world.Regions[index];
                for (var slot = 0; slot < PerRegionCounts[index]; slot++)
                {
                    // Placement queries the actual captured colliders, never a declared envelope.
                    Assert.That(geometry.TryFindStandingPoint(region.Id, region.Hub, WorldBodyPresentation.NpcRadiusM,
                        WorldBodyPresentation.NpcHeightM, spawns.ToArray(), out var pose), Is.True,
                        "No actual standing point for " + region.Id + " slot " + slot);
                    spawns.Add(new WorldBodySpawn { BodyId = "npc" + spawns.Count.ToString("D3"), Pose = pose,
                        RadiusM = WorldBodyPresentation.NpcRadiusM, HeightM = WorldBodyPresentation.NpcHeightM, PreferredSpeedMS = 1.5,
                        Pinned = spawns.Count % PinnedEveryNthBody == 0 });
                }
            }
            Assert.That(spawns.Count, Is.EqualTo(WorldCrowdFixtureContract.NpcCount));
            return spawns;
        }

        /// <summary>The abstract counterpart shares the actual slots and the captured world geometry, and differs
        /// from the world actual roster on the body-size axis only. Its record therefore carries the radius
        /// control kind, never the world actual kind and never the laboratory corridor kind.</summary>
        private List<WorldBodySpawn> AbstractRoster() => actualRoster.Select((s, i) => new WorldBodySpawn
        {
            BodyId = "unit" + i.ToString("D3"), Pose = s.Pose, RadiusM = WorldCrowdFixtureContract.AbstractUnitRadiusM,
            HeightM = WorldBodyPresentation.NpcHeightM, PreferredSpeedMS = 1.5, Pinned = s.Pinned
        }).ToList();

        private static WorldMotionCommand[] Commands(IEnumerable<WorldBodySpawn> spawns) => spawns
            .Select(s => new WorldMotionCommand { BodyId = s.BodyId, Pinned = s.Pinned,
                DesiredVelocity = s.Pinned ? new Point3() : new Point3((float)CommandX, 0, (float)CommandZ) }).ToArray();

        private static double MinimumClearanceM(CrowdAgent[] agents)
        {
            var minimum = double.PositiveInfinity;
            for (var i = 0; i < agents.Length; i++)
            for (var j = i + 1; j < agents.Length; j++)
            {
                var a = agents[i]; var b = agents[j];
                // Bodies on separate floors never share a contact plane and cannot penetrate each other.
                if (Math.Abs(a.FootElevationM - b.FootElevationM) >= Math.Max(a.HeightM, b.HeightM)) continue;
                var distance = Math.Sqrt(Math.Pow(a.Position.X - b.Position.X, 2) + Math.Pow(a.Position.Y - b.Position.Y, 2));
                minimum = Math.Min(minimum, distance - (a.RadiusM + b.RadiusM));
            }
            return minimum;
        }

        private static bool IsMeasured(double value) => !double.IsNaN(value) && !double.IsPositiveInfinity(value);

        /// <summary>
        /// The real per-record fixture identity the MeasurementItems list names. It is computed from THIS
        /// record's own declaration at run time - never copied from the fixture file and never inherited from
        /// the other record - so two rosters that disagree on the declared separation axis (fixture kind, body
        /// size or seed) always disagree here.
        /// </summary>
        private static string FixtureIdentitySha256(string fixtureKind, int bodyCount, double radiusM, double heightM, long seed) =>
            Hash(fixtureKind + "|" + bodyCount.ToString(CultureInfo.InvariantCulture) + "|" +
                radiusM.ToString("R", CultureInfo.InvariantCulture) + "|" + heightM.ToString("R", CultureInfo.InvariantCulture) +
                "|" + seed.ToString(CultureInfo.InvariantCulture) + "\n");

        /// <summary>
        /// The fixture's MislabeledAbstract rule as an executable predicate, so a test can prove it refuses in
        /// BOTH directions. `measuredRosterKind` is the kind the roster actually is; `declaredKind` is the kind
        /// the record presents itself as. Presenting one roster's numbers under the other roster's kind is
        /// refused, and so is a record whose fixture identity hash does not describe its declared kind.
        /// </summary>
        private static bool KindMismatchFails(string measuredRosterKind, string declaredKind, RunReceipt record, out string failure)
        {
            failure = "";
            if (declaredKind != measuredRosterKind)
            { failure = "A " + measuredRosterKind + " roster cannot be recorded under " + declaredKind + "."; return true; }
            if (record.fixtureKind != declaredKind)
            { failure = "Record kind '" + record.fixtureKind + "' is not the declared kind '" + declaredKind + "'."; return true; }
            if (record.fixtureIdentitySha256 !=
                FixtureIdentitySha256(declaredKind, record.bodyCount, record.radiusM, record.heightM, record.seed))
            { failure = "The fixture identity hash does not describe the declared kind '" + declaredKind + "'."; return true; }
            return false;
        }

        private RunReceipt Run(List<WorldBodySpawn> roster, string kind, string label)
        {
            var receipt = new RunReceipt
            {
                fixtureKind = kind, executableTest = nameof(Fmp07aWorldCrowdTests), seed = WorldSeed,
                bodyCount = roster.Count, radiusM = roster[0].RadiusM, heightM = roster[0].HeightM,
                pinnedCount = roster.Count(s => s.Pinned), tickSeconds = TickSeconds, simulatedSeconds = TickCount * TickSeconds,
                fixtureIdentitySha256 = FixtureIdentitySha256(kind, roster.Count, roster[0].RadiusM, roster[0].HeightM, WorldSeed),
                measurementItems = Fixture().MeasurementItems, sourceProfileSha256 = LiveProfileHash(),
                sceneManifestSha256 = LoadedSceneManifest, supportCount = geometry.Supports.Count,
                colliderCount = geometry.Colliders.Count,
                minimumSweptGapM = double.PositiveInfinity, minimumCentreClearanceM = double.PositiveInfinity
            };
            var commands = Commands(roster);
            using var adapter = new ConnectedWorldMotionAdapter(geometry, roster.ToArray(), WorldSeed);
            for (var tick = 0; tick < TickCount; tick++)
            {
                Assert.That(adapter.TryAdvance(TickSeconds, commands, out var report), Is.True, label + " tick " + tick + ": " + report.Failure);
                Assert.That(report.Committed, Is.True, label + " committed tick " + tick);
                receipt.committedTicks++;
                receipt.coupledSteps += report.CoupledSteps;
                if (IsMeasured(report.MinimumSweptGapM))
                { receipt.sweptGapSamples++; receipt.minimumSweptGapM = Math.Min(receipt.minimumSweptGapM, report.MinimumSweptGapM); }
                var snapshot = adapter.Capture();
                var agents = snapshot.Crowd.Agents;
                receipt.geometrySignature = snapshot.GeometrySignature;
                receipt.wallCount = snapshot.Crowd.Definition.Walls.Length;
                receipt.contactSpaceIds = agents.Select(a => a.ContactSpaceId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
                receipt.regionIds = agents.Select(a => a.RegionId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
                foreach (var agent in agents)
                {
                    receipt.maximumGroundSpeedMS = Math.Max(receipt.maximumGroundSpeedMS, agent.Velocity.Length);
                    if (agent.Pinned) Assert.That(agent.Velocity.Length, Is.Zero, agent.Id);
                }
                var clearance = MinimumClearanceM(agents);
                if (IsMeasured(clearance))
                { receipt.centreClearanceSamples++; receipt.minimumCentreClearanceM = Math.Min(receipt.minimumCentreClearanceM, clearance); }
                Assert.That(adapter.ValidateBodies(out var failure), Is.True, label + " validation " + failure);
            }
            // A surviving sentinel means nothing was measured. Writing it back as a measured-looking zero would
            // let every ">= 0" bound below pass on an empty measurement, so the receipt fails instead.
            Assert.That(double.IsPositiveInfinity(receipt.minimumSweptGapM), Is.False,
                label + " minimum swept gap: no finite swept-gap sample was produced in any of the " + TickCount +
                " committed ticks, so the non-negativity bound would be vacuous.");
            Assert.That(double.IsPositiveInfinity(receipt.minimumCentreClearanceM), Is.False,
                label + " minimum centre clearance: no finite body pair shared a contact plane in any committed tick.");
            Assert.That(receipt.sweptGapSamples, Is.GreaterThan(0), label + " swept-gap sample count");
            Assert.That(receipt.centreClearanceSamples, Is.GreaterThan(0), label + " centre-clearance sample count");
            receipt.distinctRegionCount = receipt.regionIds.Length;
            return receipt;
        }

        private static void AssertReceiptContract(RunReceipt receipt, int expectedBodies, double expectedRadius, string label)
        {
            Assert.That(receipt.bodyCount, Is.EqualTo(expectedBodies), label + " body count");
            Assert.That(receipt.radiusM, Is.EqualTo(expectedRadius), label + " radius");
            Assert.That(receipt.committedTicks, Is.EqualTo(TickCount), label + " committed ticks");
            Assert.That(receipt.distinctRegionCount, Is.EqualTo(WorldCrowdFixtureContract.RegionCount), label + " region coverage");
            Assert.That(receipt.contactSpaceIds, Is.EquivalentTo(new[] { "world" }), label + " contact space");
            Assert.That(receipt.minimumSweptGapM, Is.GreaterThanOrEqualTo(0), label + " minimum swept gap");
            Assert.That(receipt.minimumCentreClearanceM, Is.GreaterThanOrEqualTo(0), label + " centre clearance");
            Assert.That(receipt.geometrySignature, Is.Not.Null.And.Not.Empty, label + " geometry signature");
            Assert.That(receipt.measurementItems.Length, Is.EqualTo(WorldCrowdFixtureContract.MeasurementItemCount), label + " measurement items");
            // The declared separation axis must be an axis the record actually implements, not a name in a list.
            Assert.That(receipt.measurementItems, Does.Contain(nameof(RunReceipt.fixtureIdentitySha256)), label + " identity axis declared");
            Assert.That(receipt.fixtureIdentitySha256,
                Is.EqualTo(FixtureIdentitySha256(receipt.fixtureKind, receipt.bodyCount, receipt.radiusM, receipt.heightM, receipt.seed)),
                label + " fixture identity hash must describe this record's own declaration");
            Assert.That(receipt.sweptGapSamples, Is.GreaterThan(0), label + " swept-gap samples");
            Assert.That(receipt.centreClearanceSamples, Is.GreaterThan(0), label + " centre-clearance samples");
            // Bound tied to the commanded speed this run actually issued, not a remembered 1.5 m/s literal that
            // is 7.3x the command and therefore checks nothing.
            var bound = Fixture().Regression.Assertions;
            Assert.That(bound.CommandedSpeedMS, Is.EqualTo(CommandedSpeedMS).Within(1e-12), label + " declared commanded speed");
            Assert.That(bound.MaximumGroundSpeedToleranceMS, Is.GreaterThan(0), label + " speed tolerance");
            Assert.That(receipt.maximumGroundSpeedMS,
                Is.LessThanOrEqualTo(bound.CommandedSpeedMS + bound.MaximumGroundSpeedToleranceMS), label + " ground speed");
        }

        /// <summary>The record identity: kind, body size, seed and the captured geometry hash. Two records that
        /// disagree on any of these must never be presented as the same fixture.</summary>
        private static string Identity(RunReceipt receipt) => Hash(JsonUtility.ToJson(new RunReceipt
        {
            fixtureKind = receipt.fixtureKind, bodyCount = receipt.bodyCount, radiusM = receipt.radiusM, heightM = receipt.heightM,
            seed = receipt.seed, tickSeconds = receipt.tickSeconds, sceneManifestSha256 = receipt.sceneManifestSha256,
            fixtureIdentitySha256 = receipt.fixtureIdentitySha256,
            geometrySignature = receipt.geometrySignature, measurementItems = receipt.measurementItems
        }));

        private static Collider[] AuthoredColliders(IEnumerable<ConnectedRegionView> regionViews) => regionViews
            .SelectMany(v => v.GetComponentsInChildren<Collider>(true))
            .Where(c => !c.isTrigger && !WorldBodyPresentation.IsPresentation(c)).ToArray();

        private static void Write(string name, string json)
        {
            var directory = Path.Combine(Root, ReceiptDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), json);
            Debug.Log("FMP07A-RECEIPT " + name + " " + json);
        }

        // ------------------------------------------------------------------ fixture contract ----

        [Test] public void DeclaredProfileBytesAndTheThirteenRegionSceneManifestStillMatchTheFixtureAnchors()
        {
            Assert.That(LiveProfileHash(), Is.EqualTo(AuthoredProfileSha256));
            Assert.That(world.Regions.Length, Is.EqualTo(WorldCrowdFixtureContract.RegionCount));
            Assert.That(world.Portals.Length, Is.EqualTo(12));
            Assert.That(world.Frames.Length, Is.EqualTo(3));
            Assert.That(LoadedSceneManifest, Is.EqualTo(AuthoredSceneManifestSha256));

            var fixture = Fixture();
            Assert.That(fixture.WorkId, Is.EqualTo("FMP-07a"));
            Assert.That(fixture.SourceProfile.Path, Is.EqualTo(ProfilePath));
            Assert.That(fixture.SourceProfile.Sha256, Is.EqualTo(AuthoredProfileSha256));
            Assert.That(fixture.SceneHash.ManifestSha256, Is.EqualTo(AuthoredSceneManifestSha256));
            Assert.That(fixture.SceneHash.ProfileSha256, Is.EqualTo(AuthoredProfileSha256));
            Assert.That(fixture.SourceProfile.ProfileId, Is.EqualTo(world.ProfileId));
        }

        [Test] public void AllThirteenGeneratedRegionScenesAreLoadedWithTheDeclaredSceneNameAndFrame()
        {
            var fixture = Fixture();
            Assert.That(fixture.SceneHash.RegionScenes.Length, Is.EqualTo(WorldCrowdFixtureContract.RegionCount));
            Assert.That(views.Select(v => v.RegionId).OrderBy(x => x, StringComparer.Ordinal),
                Is.EqualTo(world.Regions.Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal)));
            Assert.That(scenePaths.Length, Is.EqualTo(WorldCrowdFixtureContract.RegionCount + 1));
            foreach (var declared in fixture.SceneHash.RegionScenes)
            {
                var region = world.Region(declared.RegionId);
                Assert.That(declared.SceneName, Is.EqualTo(region.SceneName), declared.RegionId);
                Assert.That(declared.FrameId, Is.EqualTo(region.FrameId), declared.RegionId);
                var view = views.Single(v => v.RegionId == declared.RegionId);
                Assert.That(view.FrameId, Is.EqualTo(region.FrameId), declared.RegionId);
                Assert.That(view.gameObject.scene.path, Is.EqualTo(folder + "/" + declared.SceneName + ".unity"), declared.RegionId);
                Assert.That(File.Exists(ProjectFile(folder + "/" + declared.SceneName + ".unity")), Is.True, declared.RegionId);
            }
            Assert.That(views.All(v => v.gameObject.scene.isLoaded), Is.True, "Every declared region scene must be loaded.");
        }

        [Test] public void FixtureDeclaresTheWorldActualKindWithAnActualSizedNpcHundredRosterAndASeparatedAbstractCounterpart()
        {
            var fixture = Fixture();
            Assert.That(FixtureFails(fixture, MeasureAuthoredEnvelopeM(), out var failure), Is.False, failure);
            Assert.That(fixture.FixtureKind, Is.EqualTo(WorldCrowdFixtureContract.WorldActualKind));
            Assert.That(fixture.Body.Count, Is.EqualTo(WorldCrowdFixtureContract.NpcCount));
            Assert.That(fixture.Body.RadiusM, Is.EqualTo(WorldBodyPresentation.NpcRadiusM));
            Assert.That(fixture.Body.HeightM, Is.EqualTo(WorldBodyPresentation.NpcHeightM));
            Assert.That(fixture.Body.PlayerCount, Is.Zero);
            Assert.That(fixture.Seed, Is.EqualTo(WorldSeed));
            Assert.That(fixture.Placement.PerRegionCounts, Is.EqualTo(PerRegionCounts));
            Assert.That(fixture.Placement.PerRegionCounts.Sum(), Is.EqualTo(WorldCrowdFixtureContract.NpcCount));
            Assert.That(fixture.Placement.RegionsCovered, Is.EqualTo(WorldCrowdFixtureContract.RegionCount));
            Assert.That(fixture.Regression.Ticks, Is.EqualTo(TickCount));
            Assert.That(fixture.Regression.TickSeconds, Is.EqualTo(TickSeconds));
            Assert.That(fixture.Regression.PinnedEveryNthBody, Is.EqualTo(PinnedEveryNthBody));
            Assert.That(fixture.AbstractCounterpart.FixtureKind, Is.EqualTo(WorldCrowdFixtureContract.AbstractControlKind));
            Assert.That(fixture.AbstractCounterpart.ActualGeometry, Is.False);
            Assert.That(fixture.AbstractCounterpart.RecordedSeparately, Is.True);
            Assert.That(actualRoster.Count(s => s.Pinned), Is.EqualTo(WorldCrowdFixtureContract.NpcCount / PinnedEveryNthBody));
        }

        [Test] public void MeasuredAuthoredModelEnvelopeExceedsTheAbstractUnitAndNeverExceedsTheAuthoredRadius()
        {
            var envelope = MeasureAuthoredEnvelopeM();
            Assert.That(envelope, Is.GreaterThan(0), "The authored model must have a measurable envelope.");
            Assert.That(envelope, Is.LessThanOrEqualTo(WorldBodyPresentation.NpcRadiusM), "NpcRadiusM must bound the authored mesh.");
            Assert.That(envelope, Is.GreaterThan(WorldCrowdFixtureContract.AbstractUnitRadiusM),
                "The .15 m abstract calibration body is smaller than the authored model; the two must never be conflated.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AuthoredModelPath);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                var p = model.transform.worldToLocalMatrix.MultiplyPoint3x4(filter.transform.localToWorldMatrix.MultiplyPoint3x4(vertex));
                Assert.That(p.y, Is.InRange(-.001f, (float)WorldBodyPresentation.NpcHeightM));
            }
        }

        // ------------------------------------------------------------------ actual captured geometry ----

        [Test] public void CapturedGeometryIsTheActualAuthoredColliderSetWithEveryStaticColliderLoadedAndUnchanged()
        {
            var fixture = Fixture();
            var authored = AuthoredColliders(views);
            Assert.That(geometry.Colliders.Count, Is.EqualTo(authored.Length),
                "Every loaded authored collider must be captured; none may be silently skipped.");
            Assert.That(geometry.Colliders.Count, Is.GreaterThanOrEqualTo(fixture.GeometryLoad.MinimumCapturedColliders));
            Assert.That(geometry.Supports.Count, Is.GreaterThanOrEqualTo(fixture.GeometryLoad.MinimumCapturedSupports));
            foreach (var descriptor in geometry.Colliders)
            {
                Assert.That(descriptor.Source, Is.Not.Null, descriptor.SourcePath);
                Assert.That(descriptor.Source.enabled || descriptor.PortalId != "", Is.True, "Unloaded authored collider " + descriptor.SourcePath);
                Assert.That(descriptor.Vertices.Length, Is.GreaterThan(0), descriptor.Id);
            }
            foreach (var region in world.Regions)
                Assert.That(geometry.Supports.Any(s => s.RegionId == region.Id), Is.True, "No captured support in " + region.Id);
            foreach (var support in geometry.Supports)
            {
                Assert.That(support.Source, Is.Not.Null, support.Id);
                Assert.That(support.Source.enabled, Is.True, "Disabled authored support " + support.Id);
                Assert.That(support.FrameId, Is.EqualTo(world.Region(support.RegionId).FrameId), support.Id);
            }
            Assert.That(geometry.Signature, Is.Not.Null.And.Not.Empty);
            Assert.That(geometry.ValidateSourceGeometry(out var failure), Is.True, failure);
        }

        /// <summary>The authored role name of a captured collider: ConnectedWorldGeometry records the authored
        /// transform path from the region root plus the collider index, so the role is the last path segment
        /// that is not the collider index.</summary>
        private static string AuthoredRole(ConnectedWorldGeometry.ColliderDescriptor collider)
        {
            var segments = collider.SourcePath.Split('/');
            var role = segments.Length >= 2 ? segments[segments.Length - 2] : segments[segments.Length - 1];
            var bracket = role.IndexOf('[');
            return bracket < 0 ? role : role.Substring(0, bracket);
        }

        /// <summary>
        /// Every declared authored box must be the box the capture actually holds. The declaration is written in
        /// the authored profile coordinates, which are exactly the capture-time world coordinates because
        /// ConnectedWorldGeometry.Capture applies the definition's own frames: a region that only turns when a
        /// train moves is captured at zero frame delta, so its colliders are still where the builder authored
        /// them. A declaration that guessed a moving frame's runtime position would not match this capture.
        /// </summary>
        [Test] public void DeclaredAuthoredGeometryMatchesTheCapturedAuthoredCollidersRegionByRegion()
        {
            var fixture = Fixture();
            var declared = fixture.AuthoredGeometry;
            var tolerance = declared.ToleranceM;
            var captured = geometry.Colliders.ToArray();
            var structuralRoles = new HashSet<string>(StringComparer.Ordinal);
            var inventory = new StringBuilder();
            var declaredSupportTotal = 0;

            bool IsBox(ConnectedWorldGeometry.ColliderDescriptor c, double minX, double maxX, double minZ, double maxZ, double bottom, double top) =>
                Math.Abs(c.MinX - minX) <= tolerance && Math.Abs(c.MaxX - maxX) <= tolerance &&
                Math.Abs(c.MinZ - minZ) <= tolerance && Math.Abs(c.MaxZ - maxZ) <= tolerance &&
                Math.Abs(c.Bottom - bottom) <= tolerance && Math.Abs(c.Top - top) <= tolerance;
            string Text(double minX, double maxX, double minZ, double maxZ, double bottom, double top) =>
                "[" + minX + "," + maxX + "]x[" + minZ + "," + maxZ + "]x[" + bottom + "," + top + "]";

            foreach (var authored in declared.Regions)
            {
                var envelope = authored.Envelope;
                var mine = captured.Where(c => c.SourcePath.StartsWith(authored.RegionId + "/", StringComparison.Ordinal)).ToArray();
                Assert.That(mine, Is.Not.Empty, "No captured collider belongs to " + authored.RegionId);
                inventory.Append(authored.RegionId).Append(':').Append(string.Join(",",
                    mine.Select(AuthoredRole).GroupBy(x => x).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + "=" + g.Count()))).Append('\n');

                Assert.That(geometry.Supports.Where(s => s.RegionId == authored.RegionId).Select(s => s.Id),
                    Is.EquivalentTo(authored.WalkableSupportIds), authored.RegionId + " authored walkable supports");
                declaredSupportTotal += authored.WalkableSupportIds.Length;

                var floor = geometry.Supports.Single(s => s.Id == authored.FloorSupport.SurfaceId);
                Assert.That(floor.FrameId, Is.EqualTo(authored.FrameId), authored.RegionId + " floor frame");
                Assert.That(new[] { floor.MinX, floor.MaxX, floor.MinZ, floor.MaxZ },
                    Is.EqualTo(new[] { authored.FloorSupport.MinX, authored.FloorSupport.MaxX, authored.FloorSupport.MinZ, authored.FloorSupport.MaxZ })
                        .Within(tolerance), authored.RegionId + " authored floor rectangle");
                Assert.That(floor.Elevation((floor.MinX + floor.MaxX) / 2, (floor.MinZ + floor.MaxZ) / 2),
                    Is.EqualTo(authored.FloorSupport.TopY).Within(tolerance), authored.RegionId + " floor elevation");
                Assert.That(mine.Any(c => AuthoredRole(c) == "Floor" && IsBox(c, authored.FloorSupport.MinX, authored.FloorSupport.MaxX,
                    authored.FloorSupport.MinZ, authored.FloorSupport.MaxZ, authored.FloorSupport.TopY - .3, authored.FloorSupport.TopY)), Is.True,
                    authored.RegionId + " authored floor collider " + Text(authored.FloorSupport.MinX, authored.FloorSupport.MaxX,
                        authored.FloorSupport.MinZ, authored.FloorSupport.MaxZ, authored.FloorSupport.TopY - .3, authored.FloorSupport.TopY));
                structuralRoles.Add("Floor");

                foreach (var bed in authored.NonWalkableBeds)
                {
                    Assert.That(mine.Any(c => AuthoredRole(c) == "Provisional_NonWalkableRailBed" &&
                        IsBox(c, bed.MinX, bed.MaxX, bed.MinZ, bed.MaxZ, bed.BottomY, bed.TopY)), Is.True,
                        authored.RegionId + " authored exclusion bed " + Text(bed.MinX, bed.MaxX, bed.MinZ, bed.MaxZ, bed.BottomY, bed.TopY));
                    structuralRoles.Add("Provisional_NonWalkableRailBed");
                }

                var walls = authored.BoundaryWalls;
                foreach (var segment in walls.Segments)
                {
                    var minX = segment.AlongX ? envelope.CenterX + segment.Start : envelope.CenterX + segment.Edge - walls.ThicknessM / 2;
                    var maxX = segment.AlongX ? envelope.CenterX + segment.End : envelope.CenterX + segment.Edge + walls.ThicknessM / 2;
                    var minZ = segment.AlongX ? envelope.CenterZ + segment.Edge - walls.ThicknessM / 2 : envelope.CenterZ + segment.Start;
                    var maxZ = segment.AlongX ? envelope.CenterZ + segment.Edge + walls.ThicknessM / 2 : envelope.CenterZ + segment.End;
                    Assert.That(mine.Any(c => AuthoredRole(c) == walls.ColliderRole &&
                        IsBox(c, minX, maxX, minZ, maxZ, envelope.CenterY, envelope.CenterY + walls.HeightM)), Is.True,
                        authored.RegionId + " authored wall segment " + Text(minX, maxX, minZ, maxZ, envelope.CenterY, envelope.CenterY + walls.HeightM));
                    structuralRoles.Add(walls.ColliderRole);
                }
                var capturedWalls = mine.Where(c => AuthoredRole(c) == walls.ColliderRole).ToArray();
                Assert.That(capturedWalls.Length, Is.EqualTo(walls.SegmentCount), authored.RegionId + " authored wall segment count");
                Assert.That(capturedWalls.Sum(c => Math.Max(c.MaxX - c.MinX, c.MaxZ - c.MinZ)),
                    Is.EqualTo(walls.SolidLengthM).Within(tolerance), authored.RegionId + " authored wall solid length");

                foreach (var lintel in authored.PortalLintels)
                {
                    Assert.That(mine.Any(c => AuthoredRole(c) == "Provisional_PortalLintel" &&
                        IsBox(c, lintel.MinX, lintel.MaxX, lintel.MinZ, lintel.MaxZ, lintel.BottomY, lintel.TopY)), Is.True,
                        authored.RegionId + " authored lintel " + lintel.PortalId + " " +
                            Text(lintel.MinX, lintel.MaxX, lintel.MinZ, lintel.MaxZ, lintel.BottomY, lintel.TopY));
                    structuralRoles.Add("Provisional_PortalLintel");
                }

                var ceiling = authored.CeilingCollider;
                Assert.That(mine.Any(c => AuthoredRole(c) == "Ceiling"), Is.EqualTo(ceiling.Present), authored.RegionId + " authored ceiling presence");
                if (ceiling.Present)
                {
                    Assert.That(mine.Any(c => AuthoredRole(c) == "Ceiling" &&
                        IsBox(c, ceiling.MinX, ceiling.MaxX, ceiling.MinZ, ceiling.MaxZ, ceiling.BottomY, ceiling.TopY)), Is.True,
                        authored.RegionId + " authored ceiling " + Text(ceiling.MinX, ceiling.MaxX, ceiling.MinZ, ceiling.MaxZ, ceiling.BottomY, ceiling.TopY));
                    structuralRoles.Add("Ceiling");
                }

                foreach (var passage in authored.PassageSupports)
                {
                    var support = geometry.Supports.Single(s => s.Id == passage.SurfaceId);
                    Assert.That(support.RegionId, Is.EqualTo(authored.RegionId), passage.PortalId + " corridor owner region");
                    Assert.That(support.FrameId, Is.EqualTo("world"), passage.PortalId + " corridor contact frame");
                    Assert.That(new[] { support.MinX, support.MaxX, support.MinZ, support.MaxZ },
                        Is.EqualTo(new[] { passage.Support.MinX, passage.Support.MaxX, passage.Support.MinZ, passage.Support.MaxZ }).Within(tolerance),
                        passage.PortalId + " authored corridor upper face rectangle");
                    Assert.That(support.Elevation((support.MinX + support.MaxX) / 2, (support.MinZ + support.MaxZ) / 2),
                        Is.EqualTo(passage.Support.CentreElevationM).Within(tolerance), passage.PortalId + " authored corridor centre elevation");

                    // The corridor's floor, guards and ceiling hang off the portal group, so they are counted
                    // inside that group only: a region with three corridors must not let one corridor's boxes
                    // satisfy another's declaration.
                    var underPortal = mine.Where(c => c.SourcePath.Contains("/Portal_" + passage.PortalId + "[", StringComparison.Ordinal)).ToArray();
                    Assert.That(underPortal, Is.Not.Empty, passage.PortalId + " must author a corridor group");
                    Assert.That(underPortal.Any(c => AuthoredRole(c) == passage.FloorColliderRole &&
                        IsBox(c, passage.Collider.MinX, passage.Collider.MaxX, passage.Collider.MinZ,
                            passage.Collider.MaxZ, passage.Collider.BottomY, passage.Collider.TopY)), Is.True,
                        passage.PortalId + " authored corridor floor collider " + Text(passage.Collider.MinX, passage.Collider.MaxX,
                            passage.Collider.MinZ, passage.Collider.MaxZ, passage.Collider.BottomY, passage.Collider.TopY));
                    structuralRoles.Add(passage.FloorColliderRole);

                    var guards = underPortal.Where(c => AuthoredRole(c) == "Provisional_RampGuard").ToArray();
                    Assert.That(guards.Length, Is.EqualTo(passage.GuardPrismCount), passage.PortalId + " authored ramp guard count");
                    Assert.That(guards.All(g => g.Vertices.Length == passage.GuardPrismVertexCount), Is.True,
                        passage.PortalId + " authored ramp guard vertex count");
                    if (passage.GuardPrismCount > 0) structuralRoles.Add("Provisional_RampGuard");

                    Assert.That(underPortal.Any(c => AuthoredRole(c) == "Provisional_PassageCeiling"), Is.EqualTo(passage.PassageCeiling),
                        passage.PortalId + " passage ceiling presence");
                    if (passage.PassageCeiling) structuralRoles.Add("Provisional_PassageCeiling");

                    // The closure is authored under the owning region, not under the portal group, so it is
                    // matched by the barrier's portal id.
                    Assert.That(mine.Any(c => AuthoredRole(c) == "Provisional_DoorPanel" && c.PortalId == passage.PortalId),
                        Is.EqualTo(passage.DoorPanelCollider), passage.PortalId + " door panel presence");
                    if (passage.DoorPanelCollider) structuralRoles.Add("Provisional_DoorPanel");
                }

                foreach (var derived in authored.DerivedFurnitureBoxes)
                {
                    var boxes = mine.Where(c => AuthoredRole(c) == derived.Role).ToArray();
                    Assert.That(boxes.Length, Is.GreaterThan(0), authored.RegionId + " declared " + derived.Role + " but the capture holds none");
                    Assert.That(boxes.Any(c => IsBox(c, derived.MinX, derived.MaxX, derived.MinZ, derived.MaxZ, derived.BottomY, derived.TopY)),
                        Is.True, authored.RegionId + " authored " + derived.Role + " " +
                            Text(derived.MinX, derived.MaxX, derived.MinZ, derived.MaxZ, derived.BottomY, derived.TopY));
                    structuralRoles.Add(derived.Role);
                }
                Assert.That(mine.Count(c => AuthoredRole(c) == "Provisional_RailAdapter" || AuthoredRole(c) == "Provisional_BoardingScreen"),
                    Is.EqualTo(authored.DerivedFurnitureBoxes.Count(b => b.Role == "Provisional_RailAdapter" || b.Role == "Provisional_BoardingScreen")),
                    authored.RegionId + " declared exactly-countable furniture boxes");
            }
            Assert.That(declaredSupportTotal, Is.EqualTo(declared.DeclaredWalkableSupportCount), "declared walkable support total");
            Assert.That(declaredSupportTotal, Is.EqualTo(geometry.Supports.Count), "capture is not the declared authored support set");

            // The other direction of the negative case: a captured collider may not carry a declared structural
            // role in a region that did not declare it. Roles the declaration deliberately leaves to the authored
            // meshes are named separately and are not in this set.
            foreach (var authored in declared.Regions)
            {
                var roles = new HashSet<string>(authored.AuthoredColliderRolesWithDerivedCount, StringComparer.Ordinal);
                foreach (var collider in captured.Where(c => c.SourcePath.StartsWith(authored.RegionId + "/", StringComparison.Ordinal)))
                    if (structuralRoles.Contains(AuthoredRole(collider)))
                        Assert.That(roles.Contains(AuthoredRole(collider)), Is.True,
                            "Captured structural collider '" + AuthoredRole(collider) + "' is not declared for " + authored.RegionId + ": " + collider.SourcePath);
                Assert.That(authored.MeshDerivedColliderRoles, Is.Not.Null, authored.RegionId + " mesh-derived role list");
                // The declaration is the whole vocabulary: a captured collider is either a role this region
                // declares, or one of the FoundationAssetPhysics proxies the declaration says it leaves to the
                // authored meshes. A collider the declaration has never heard of is a stale declaration.
                foreach (var collider in captured.Where(c => c.SourcePath.StartsWith(authored.RegionId + "/", StringComparison.Ordinal)))
                {
                    var role = AuthoredRole(collider);
                    Assert.That(roles.Contains(role) || role.StartsWith("Collision_", StringComparison.Ordinal), Is.True,
                        "Captured collider '" + role + "' is neither declared nor an authored mesh proxy in " + authored.RegionId + ": " + collider.SourcePath);
                    if (role.StartsWith("Collision_", StringComparison.Ordinal))
                        Assert.That(authored.MeshDerivedColliderRoles.Length, Is.GreaterThan(0),
                            authored.RegionId + " captures mesh proxies but declares no mesh-derived role");
                }
            }
            Debug.Log("FMP07A-GEOMETRY " + inventory.ToString().Replace("\n", " | "));
        }

        /// <summary>
        /// Negative cases for the declared authored geometry itself. Each probe is one mutation of the real
        /// file, and each must be refused with the clause that names it, so a later edit cannot quietly drop a
        /// region, move a region into the wrong frame, or reshape a collider envelope to match a bad capture.
        /// </summary>
        [Test] public void DeclaredAuthoredGeometryRefusesMissingRegionWrongFrameAndReshapedEnvelopes()
        {
            var envelope = MeasureAuthoredEnvelopeM();
            var probes = new List<(string Name, Action<WorldCrowdFixtureContract.Fixture> Mutate, string Clause)>
            {
                ("twelve regions", f => f.AuthoredGeometry.Regions = f.AuthoredGeometry.Regions.Take(12).ToArray(), "Missing region geometry"),
                ("duplicate region", f => f.AuthoredGeometry.Regions[1].RegionId = f.AuthoredGeometry.Regions[0].RegionId, "Duplicate region geometry entry"),
                ("no provenance", f => f.AuthoredGeometry.SourceBuilder.Sha256 = "", "provenance"),
                ("zero tolerance", f => f.AuthoredGeometry.ToleranceM = 0, "provenance"),
                ("state the frame twice", f => f.AuthoredGeometry.Regions.First(r => r.FrameId != "world").FrameId = "world", "in AuthoredGeometry"),
                ("envelope moved", f => f.AuthoredGeometry.Regions[0].Envelope.CenterX += 1, "Authored envelope"),
                ("floor inverted", f => f.AuthoredGeometry.Regions[0].FloorSupport.MinX = f.AuthoredGeometry.Regions[0].FloorSupport.MaxX, "Authored floor support"),
                ("floor renamed", f => f.AuthoredGeometry.Regions[0].FloorSupport.SurfaceId = "floor.somewhere_else", "Authored floor support"),
                ("support ids emptied", f => f.AuthoredGeometry.Regions[0].WalkableSupportIds = Array.Empty<string>(), "Authored walkable support ids"),
                ("wall length no longer its segments", f => f.AuthoredGeometry.Regions[0].BoundaryWalls.SolidLengthM += 1, "Authored boundary wall envelope"),
                ("segment count no longer its segments", f => f.AuthoredGeometry.Regions[0].BoundaryWalls.SegmentCount += 1, "Authored boundary wall envelope"),
                ("carriage wall relabelled", f => f.AuthoredGeometry.Regions.First(r => r.Template == "vehicle").BoundaryWalls.ColliderRole = "BoundaryWall", "Authored boundary wall role"),
                ("ceiling contradicted", f => f.AuthoredGeometry.Regions[0].CeilingCollider.Present = !f.AuthoredGeometry.Regions[0].CeilingCollider.Present, "Authored ceiling collider"),
                ("passage inverted", f => f.AuthoredGeometry.Regions.First(r => r.PassageSupports.Length > 0).PassageSupports[0].Support.MinX = 999, "Authored passage support"),
                ("furniture role undeclared", f => f.AuthoredGeometry.Regions.First(r => r.DerivedFurnitureBoxes.Length > 0).DerivedFurnitureBoxes[0].Role = "Provisional_Elsewhere", "Authored derived furniture box"),
                ("furniture box inverted", f => f.AuthoredGeometry.Regions.First(r => r.DerivedFurnitureBoxes.Length > 0).DerivedFurnitureBoxes[0].MinX = 1e6, "Authored derived furniture box"),
                ("support total understated", f => f.AuthoredGeometry.DeclaredWalkableSupportCount -= 1, "declared authored walkable support count")
            };
            foreach (var probe in probes)
            {
                var fixture = Fixture();
                probe.Mutate(fixture);
                Assert.That(FixtureFails(fixture, envelope, out var failure), Is.True, probe.Name + " must be refused");
                Assert.That(failure, Does.Contain(probe.Clause), probe.Name + " must be refused by its own clause, got: " + failure);
            }

            // The declared per-region support ids must be the floor plus the corridors, in that order: a
            // region that claimed a corridor it does not own would otherwise pass the total.
            var borrowed = Fixture();
            var corridorOwner = borrowed.AuthoredGeometry.Regions.First(r => r.PassageSupports.Length > 0);
            corridorOwner.WalkableSupportIds = corridorOwner.WalkableSupportIds.Reverse().ToArray();
            Assert.That(FixtureFails(borrowed, envelope, out var order), Is.True);
            Assert.That(order, Does.Contain("not the floor and passage surfaces"));
        }

        /// <summary>
        /// Negative cases for the declared navigation geometry. Every probe is a value copied from the runtime
        /// path query in ConnectedWorldGeometry.TryPath; changing any one of them must be refused, because a
        /// declaration that no longer matches the query cannot bound the paths a run actually walked.
        /// </summary>
        [Test] public void DeclaredNavigationGeometryRefusesAMarginSlopeVoxelOrRadiusThatIsNotTheQuery()
        {
            var envelope = MeasureAuthoredEnvelopeM();
            var probes = new List<(string Name, Action<WorldCrowdFixtureContract.Fixture> Mutate)>
            {
                ("no agent margin", f => f.NavGeometry.AgentRadiusMarginM = 0),
                ("slope is not the motor limit", f => f.NavGeometry.AgentSlopeDegrees = 30),
                ("height is not the body height", f => f.NavGeometry.AgentHeightM = 1.7),
                ("no climb", f => f.NavGeometry.AgentClimbM = 0),
                ("voxel size no longer overridden", f => f.NavGeometry.OverrideVoxelSize = false),
                ("zero voxel", f => f.NavGeometry.VoxelSizeM = 0),
                ("tile size no longer overridden", f => f.NavGeometry.OverrideTileSize = false),
                ("zero tile", f => f.NavGeometry.TileSize = 0),
                ("zero min region", f => f.NavGeometry.MinRegionAreaM2 = 0),
                ("zero query offset step", f => f.NavGeometry.QueryOffsetStepM = 0),
                ("zero sample tolerance", f => f.NavGeometry.SampleToleranceM = 0),
                ("walkable area is not zero", f => f.NavGeometry.WalkableArea = 1),
                ("barrier area is not one", f => f.NavGeometry.BarrierArea = 0),
                ("world radius is the abstract one", f => f.NavGeometry.AgentRadiusForWorldActualM = f.NavGeometry.AgentRadiusForAbstractUnitM),
                ("slope constant renamed", f => f.NavGeometry.SlopeConstant = ""),
                ("source hash dropped", f => f.NavGeometry.SourceSha256 = ""),
                ("negative rule dropped", f => f.NavGeometry.NegativeRule = "")
            };
            foreach (var probe in probes)
            {
                var fixture = Fixture();
                probe.Mutate(fixture);
                Assert.That(FixtureFails(fixture, envelope, out var failure), Is.True, probe.Name + " must be refused");
                Assert.That(failure, Is.EqualTo("The declared navigation geometry is not the runtime path-query configuration."),
                    probe.Name + " must be refused by the navigation clause, got: " + failure);
            }

            // The radius-shrink concealment: swapping the world actual query key for the abstract unit's key
            // is refused, even though every other navigation value still matches the runtime query.
            var shrunk = Fixture();
            shrunk.NavGeometry.WorldActualQueryKey = shrunk.NavGeometry.AbstractUnitQueryKey;
            Assert.That(FixtureFails(shrunk, envelope, out var key), Is.True);
            Assert.That(key, Does.Contain("navigation query keys"));
            Assert.That(Fixture().NavGeometry.WorldActualQueryKey, Is.Not.EqualTo(Fixture().NavGeometry.AbstractUnitQueryKey));
        }

        /// <summary>
        /// The declared role lists are the vocabulary the capture is checked against, so a captured structural
        /// collider that its region does not declare must fail even when its box is right. The predicate is run
        /// against the live capture in both directions: it holds for the real declaration and fails for the same
        /// capture once one region's declared Floor role is trimmed.
        /// </summary>
        [Test] public void AStructuralColliderInARegionThatDidNotDeclareItsRoleIsRefused()
        {
            var declared = Fixture().AuthoredGeometry;
            var captured = geometry.Colliders.ToArray();
            // The declaration has two role vocabularies: the roles it enumerates (every procedural box it
            // creates) and the roles it names but leaves to the authored meshes. Take the difference, so the
            // rule is exercised on exactly the roles the declaration promises to enumerate, and a role added
            // to either list changes this test rather than slipping past it.
            var meshDerived = new HashSet<string>(declared.Regions.SelectMany(r => r.MeshDerivedColliderRoles), StringComparer.Ordinal);
            var enumerated = new HashSet<string>(declared.Regions.SelectMany(r => r.AuthoredColliderRolesWithDerivedCount), StringComparer.Ordinal);
            enumerated.ExceptWith(meshDerived);
            Assert.That(enumerated, Does.Contain("Floor").And.Contains("Ceiling"), "The enumerated vocabulary must be the procedural authored boxes.");

            string Undeclared(string regionId, ISet<string> roles)
            {
                foreach (var collider in captured.Where(c => c.SourcePath.StartsWith(regionId + "/", StringComparison.Ordinal)))
                {
                    var role = AuthoredRole(collider);
                    if (enumerated.Contains(role) && !roles.Contains(role)) return role + " " + collider.SourcePath;
                }
                return null;
            }

            var withFloor = declared.Regions.Single(r => r.RegionId == "rail_platforms_mainline");
            var floorRoles = new HashSet<string>(withFloor.AuthoredColliderRolesWithDerivedCount, StringComparer.Ordinal);
            Assert.That(Undeclared(withFloor.RegionId, floorRoles), Is.Null, "The real declaration must name every structural role its region captures.");

            floorRoles.Remove("Floor");
            Assert.That(Undeclared(withFloor.RegionId, floorRoles), Is.Not.Null,
                "A captured Floor must be refused once its region stops declaring the role.");

            // Every region in the file passes the same predicate, so the rule is not accidentally satisfied by
            // one region's list alone.
            foreach (var region in declared.Regions)
                Assert.That(Undeclared(region.RegionId, new HashSet<string>(region.AuthoredColliderRolesWithDerivedCount, StringComparer.Ordinal)), Is.Null,
                    region.RegionId + " must declare every structural role it captures.");
        }

        [Test] public void Npc100IsPlacedOnActualAuthoredSupportsAcrossAllThirteenRegionsWithNoOverlap()
        {
            Assert.That(actualRoster.Count, Is.EqualTo(WorldCrowdFixtureContract.NpcCount));
            Assert.That(actualRoster.Select(s => s.Pose.RegionId).Distinct().Count(), Is.EqualTo(WorldCrowdFixtureContract.RegionCount));
            Assert.That(actualRoster.Select(s => s.BodyId).Distinct().Count(), Is.EqualTo(WorldCrowdFixtureContract.NpcCount));
            Assert.That(actualRoster.All(s => s.RadiusM == WorldBodyPresentation.NpcRadiusM), Is.True);
            Assert.That(actualRoster.All(s => s.HeightM == WorldBodyPresentation.NpcHeightM), Is.True);
            Assert.That(actualRoster.Any(s => s.RadiusM == WorldCrowdFixtureContract.AbstractUnitRadiusM), Is.False,
                "No abstract unit body may enter the world actual roster.");
            var counts = actualRoster.GroupBy(s => s.Pose.RegionId).ToDictionary(g => g.Key, g => g.Count());
            for (var index = 0; index < world.Regions.Length; index++)
            {
                var region = world.Regions[index];
                Assert.That(counts[region.Id], Is.EqualTo(PerRegionCounts[index]), region.Id);
                Assert.That(region.Contains(actualRoster.First(s => s.Pose.RegionId == region.Id).Pose.Position), Is.True, region.Id);
            }
            // The declared no-overlap placement invariant, re-measured on the same axis rule
            // TryFindStandingPoint enforces. The bound is the fixture's declared margin, not "sum of radii":
            // a bound at the radius sum would pass on a placement that ignores the declared clearance.
            var margin = Fixture().Placement.InitialOverlapMarginM;
            Assert.That(margin, Is.GreaterThan(0), "The fixture must declare a positive initial-overlap margin.");
            for (var i = 0; i < actualRoster.Count; i++)
            for (var j = i + 1; j < actualRoster.Count; j++)
            {
                var a = actualRoster[i].Pose.Position; var b = actualRoster[j].Pose.Position;
                if (Math.Abs(a.Y - b.Y) >= WorldBodyPresentation.NpcHeightM) continue;
                var distance = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
                // The 1e-9 slack is floating-point representation only, not a share of the margin.
                Assert.That(distance,
                    Is.GreaterThanOrEqualTo(actualRoster[i].RadiusM + actualRoster[j].RadiusM + margin - 1e-9),
                    actualRoster[i].BodyId + "/" + actualRoster[j].BodyId);
            }
            using var adapter = new ConnectedWorldMotionAdapter(geometry, actualRoster.ToArray(), WorldSeed);
            Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
            var snapshot = adapter.Capture();
            Assert.That(snapshot.Crowd.Agents, Has.Length.EqualTo(WorldCrowdFixtureContract.NpcCount));
            Assert.That(snapshot.Crowd.Agents.Select(a => a.RegionId).Distinct().Count(), Is.EqualTo(WorldCrowdFixtureContract.RegionCount));
            Assert.That(snapshot.Crowd.Agents.Select(a => a.ContactSpaceId).Distinct(), Is.EquivalentTo(new[] { "world" }));
            // Every body sits on a captured authored support plane at that captured elevation.
            foreach (var agent in snapshot.Crowd.Agents)
            {
                var support = geometry.Supports.Single(s => s.Id == agent.SurfaceId);
                Assert.That(support.RegionId, Is.EqualTo(agent.RegionId), agent.Id);
                Assert.That(support.Source.enabled, Is.True, agent.Id);
                var pose = adapter.Pose(agent.Id);
                Assert.That(pose.Position.Y, Is.EqualTo(support.Elevation(pose.Position.X, pose.Position.Z)).Within(1e-4), agent.Id);
                Assert.That(pose.RegionId, Is.EqualTo(agent.RegionId), agent.Id);
            }
        }

        [Test] public void Npc100OnActualAuthoredGeometryAdvancesAndEmitsTheFixtureReceipt()
        {
            var fixture = Fixture();
            Assert.That(fixture.Regression.ExecutableTest,
                Is.EqualTo("Fmp07aWorldCrowdTests." + nameof(Npc100OnActualAuthoredGeometryAdvancesAndEmitsTheFixtureReceipt)));
            Assert.That(fixture.Regression.DesiredVelocityX, Is.EqualTo(CommandX));
            Assert.That(fixture.Regression.DesiredVelocityZ, Is.EqualTo(CommandZ));

            var actual = Run(actualRoster, WorldCrowdFixtureContract.WorldActualKind, "actual");
            AssertReceiptContract(actual, WorldCrowdFixtureContract.NpcCount, WorldBodyPresentation.NpcRadiusM, "actual");
            Assert.That(actual.pinnedCount, Is.EqualTo(WorldCrowdFixtureContract.NpcCount / PinnedEveryNthBody));
            Assert.That(actual.wallCount, Is.GreaterThan(0));
            Assert.That(actual.supportCount, Is.EqualTo(geometry.Supports.Count));
            Assert.That(actual.colliderCount, Is.EqualTo(geometry.Colliders.Count));

            // The abstract counterpart is measured with the SAME item list but recorded under a different
            // kind and a different fixture hash; it is never reported as the world actual fixture.
            var counterpart = Run(AbstractRoster(), WorldCrowdFixtureContract.AbstractControlKind, "abstract");
            AssertReceiptContract(counterpart, WorldCrowdFixtureContract.NpcCount, WorldCrowdFixtureContract.AbstractUnitRadiusM, "abstract");
            Assert.That(counterpart.fixtureKind, Is.Not.EqualTo(actual.fixtureKind));
            Assert.That(counterpart.measurementItems, Is.EqualTo(actual.measurementItems));
            Assert.That(Identity(counterpart), Is.Not.EqualTo(Identity(actual)),
                "Fixture kind and body size must make the two records distinguishable.");
            Assert.That(actual.radiusM, Is.GreaterThan(counterpart.radiusM));
            Assert.That(actual.geometrySignature, Is.EqualTo(counterpart.geometrySignature),
                "Both records read the same captured geometry; only the fixture kind and body size differ.");

            actual.sceneFilePaths = scenePaths; counterpart.sceneFilePaths = scenePaths;
            actual.sceneFileSha256 = scenePaths.Select(FileHash).ToArray();
            counterpart.sceneFileSha256 = actual.sceneFileSha256;
            var receipt = new SeparationReceipt
            {
                fixturePath = WorldCrowdFixtureContract.RelativePath,
                fixtureSha256 = FileHash(ProjectFile(WorldCrowdFixtureContract.RelativePath)),
                fixtureKind = actual.fixtureKind, separationRule = fixture.AbstractCounterpart.Rule, command = Command,
                actualFixture = actual, abstractUnitCounterpart = counterpart
            };
            var json = JsonUtility.ToJson(receipt);
            Write("fmp07a-world-crowd.json", json);
            Assert.That(File.Exists(ProjectFile(ReceiptDirectory + "/fmp07a-world-crowd.json")), Is.True);
            Assert.That(json, Does.Contain(WorldCrowdFixtureContract.WorldActualKind));
            Assert.That(json, Does.Contain(WorldCrowdFixtureContract.AbstractControlKind));
            // Neither measured record was read out of a laboratory corridor, so no record in this receipt may
            // be filed under the laboratory corridor kind. The kind may only ever appear as the named source
            // of the control, never as a measured record identity.
            Assert.That(json, Does.Not.Contain(WorldCrowdFixtureContract.AbstractUnitKind),
                "No record measured on the captured world geometry may be filed under the laboratory corridor kind.");
            Assert.That(json, Does.Contain(AuthoredProfileSha256));
            Assert.That(json, Does.Contain(AuthoredSceneManifestSha256));
        }

        [Test] public void SameSeedAndCommandsProduceTheSameActualFixtureCheckpoint()
        {
            var commands = Commands(actualRoster);
            string reference;
            using (var first = new ConnectedWorldMotionAdapter(geometry, actualRoster.ToArray(), WorldSeed))
            {
                for (var tick = 0; tick < 5; tick++)
                    Assert.That(first.TryAdvance(TickSeconds, commands, out var report), Is.True, report.Failure);
                reference = first.ExportCheckpoint();
            }
            using (var twin = new ConnectedWorldMotionAdapter(geometry, actualRoster.ToArray(), WorldSeed))
            {
                for (var tick = 0; tick < 5; tick++)
                    Assert.That(twin.TryAdvance(TickSeconds, commands, out var report), Is.True, report.Failure);
                Assert.That(twin.ExportCheckpoint(), Is.EqualTo(reference));
                Assert.That(twin.TryRestoreCheckpoint(reference, out var failure), Is.True, failure);
                Assert.That(twin.ExportCheckpoint(), Is.EqualTo(reference));
            }
        }

        [Test] public void PinnedNpcBodiesOnTheActualFixtureHoldUnderNeighbourContact()
        {
            var commands = Commands(actualRoster);
            using var adapter = new ConnectedWorldMotionAdapter(geometry, actualRoster.ToArray(), WorldSeed);
            var before = adapter.Capture().Crowd.Agents.Where(a => a.Pinned).ToDictionary(a => a.Id, a => a.Position);
            Assert.That(before.Count, Is.EqualTo(WorldCrowdFixtureContract.NpcCount / PinnedEveryNthBody));
            for (var tick = 0; tick < TickCount; tick++)
                Assert.That(adapter.TryAdvance(TickSeconds, commands, out var report), Is.True, report.Failure);
            foreach (var agent in adapter.Capture().Crowd.Agents.Where(a => a.Pinned))
            {
                Assert.That(agent.Velocity.Length, Is.Zero, agent.Id);
                Assert.That(agent.Position.X, Is.EqualTo(before[agent.Id].X), agent.Id);
                Assert.That(agent.Position.Y, Is.EqualTo(before[agent.Id].Y), agent.Id);
            }
        }

        // ------------------------------------------------------------------ negative cases ----

        [Test] public void TwelveOrDuplicateRegionViewsCannotBeCapturedAsTheWorldFixture()
        {
            foreach (var partial in new[]
            {
                views.Where(v => v.RegionId != "rail_tracks_mainline").ToArray(),
                views.Where(v => v.RegionId != "rolling_stock_metro" && v.RegionId != "metro_concourse").ToArray(),
                views.Concat(new[] { views[0] }).ToArray()
            })
            {
                var failure = Assert.Throws<ArgumentException>(() => { ConnectedWorldGeometry.Capture(world, partial); });
                Assert.That(failure.Message, Does.Contain("13 region scenes exactly once"));
            }
            Assert.That(geometry.Colliders.Count, Is.EqualTo(AuthoredColliders(views).Length));

            // Declaring only twelve regions in the fixture is the same negative case on the file side.
            var fixture = Fixture();
            fixture.SceneHash.RegionScenes = fixture.SceneHash.RegionScenes.Take(WorldCrowdFixtureContract.RegionCount - 1).ToArray();
            Assert.That(FixtureFails(fixture, MeasureAuthoredEnvelopeM(), out var missing), Is.True);
            Assert.That(missing, Does.Contain("Missing region"));
        }

        [Test] public void UnloadedOrDisabledAuthoredColliderIsDetectedAndCannotBeHidden()
        {
            Assert.That(geometry.ValidateSourceGeometry(out var before), Is.True, before);
            var wall = geometry.Colliders.First(c => !c.Walkable && !c.HeadroomOnly && c.PortalId == "" && c.Source is BoxCollider && c.Source.enabled);
            wall.Source.enabled = false;
            try
            {
                Physics.SyncTransforms();
                Assert.That(geometry.ValidateSourceGeometry(out var hidden), Is.False);
                Assert.That(hidden, Does.Contain("Changed static collider availability"));
            }
            finally { wall.Source.enabled = true; Physics.SyncTransforms(); }
            Assert.That(geometry.ValidateSourceGeometry(out var restored), Is.True, restored);

            var fixture = Fixture();
            fixture.GeometryLoad.LoadedRegionScenes = WorldCrowdFixtureContract.RegionCount - 1;
            Assert.That(FixtureFails(fixture, MeasureAuthoredEnvelopeM(), out var inert), Is.True);
            Assert.That(inert, Does.Contain("Geometry load"));
        }

        [Test] public void BodyOnADisabledAuthoredSupportIsRefusedInsteadOfBeingSilentlySimulated()
        {
            var sample = actualRoster.First(s => s.Pose.RegionId == "station_concourse_2f");
            var covering = geometry.Supports.Where(s => s.Contains(sample.Pose.Position.X, sample.Pose.Position.Z, 1e-6) &&
                Math.Abs(s.Elevation(sample.Pose.Position.X, sample.Pose.Position.Z) - sample.Pose.Position.Y) <= .34).ToArray();
            Assert.That(covering, Is.Not.Empty);
            foreach (var support in covering) support.Source.enabled = false;
            try
            {
                Physics.SyncTransforms();
                var failure = Assert.Throws<ArgumentException>(() => { new ConnectedWorldMotionAdapter(geometry, new[] { sample }, WorldSeed); });
                Assert.That(failure.Message, Does.Contain("No authored support"));
            }
            finally { foreach (var support in covering) support.Source.enabled = true; Physics.SyncTransforms(); }
            using var adapter = new ConnectedWorldMotionAdapter(geometry, new[] { sample }, WorldSeed);
            Assert.That(adapter.ValidateBodies(out var restored), Is.True, restored);
        }

        [Test] public void WrongFrameIsRefusedByTheRegionViewAndByTheAdapterFrameValidation()
        {
            // Eleven of the thirteen regions share the "world" frame, so this is First, not Single.
            var view = views.First(v => v.FrameId == "world");
            var mismatch = Assert.Throws<ArgumentException>(() => view.ApplyFrame(world, world.Frame("train-mainline")));
            Assert.That(mismatch.Message, Does.Contain("Region frame mismatch"));

            var roster = actualRoster.Where(s => s.Pose.FrameId == "world").Take(4).ToList();
            using var adapter = new ConnectedWorldMotionAdapter(geometry, roster.ToArray(), WorldSeed);
            var commands = Commands(roster);
            var checkpoint = adapter.ExportCheckpoint();
            foreach (var corrupt in new Action<SpatialFrame[]>[]
            {
                frames => frames.Single(f => f.FrameId == "train-mainline").Origin.Y += 1,
                frames => frames.Single(f => f.FrameId == "train-metro").YawDegrees += 90,
                frames => frames.Single(f => f.FrameId == "world").Origin.X += 1
            })
            {
                var frames = world.Frames.Select(f => f.Copy()).ToArray();
                corrupt(frames);
                Assert.That(adapter.TryAdvance(TickSeconds, commands, frames, Array.Empty<string>(), out var report), Is.False);
                Assert.That(report.Failure, Does.Contain("Physical frames must preserve"));
                Assert.That(adapter.ExportCheckpoint(), Is.EqualTo(checkpoint), "A rejected frame must not be partially committed.");
            }
            Assert.That(adapter.TryAdvance(TickSeconds, commands, out var accepted), Is.True, accepted.Failure);

            var fixture = Fixture();
            fixture.SceneHash.RegionScenes[0].FrameId = "train-metro";
            Assert.That(FixtureFails(fixture, MeasureAuthoredEnvelopeM(), out var wrongFrame), Is.True);
            Assert.That(wrongFrame, Does.Contain("Wrong frame"));
        }

        [Test] public void RadiusSmallerThanTheAuthoredModelAndMislabeledAbstractFixturesAreRefused()
        {
            var envelope = MeasureAuthoredEnvelopeM();
            // Every probe is derived from the envelope measured in this run, so no threshold here is a
            // remembered literal that could be wrong about the authored mesh.
            foreach (var undersized in new[] { WorldCrowdFixtureContract.AbstractUnitRadiusM, envelope / 2, envelope - .001 })
            {
                var fixture = Fixture();
                fixture.Body.RadiusM = undersized;
                Assert.That(FixtureFails(fixture, envelope, out var radius), Is.True,
                    "A radius below the authored model must fail: " + undersized);
                Assert.That(radius, Does.Contain("smaller than the authored model envelope"));
            }
            Assert.That(WorldCrowdFixtureContract.AbstractUnitRadiusM, Is.LessThan(envelope),
                "The abstract unit radius must be genuinely below the authored model.");

            foreach (var mislabel in new[] { WorldCrowdFixtureContract.AbstractUnitKind, "world_actual", "actual", "" })
            {
                var fixture = Fixture();
                fixture.FixtureKind = mislabel;
                Assert.That(FixtureFails(fixture, envelope, out var kind), Is.True);
                Assert.That(kind, Does.Contain("world actual collider/nav kind"));
            }

            var crossed = Fixture();
            crossed.AbstractCounterpart.FixtureKind = WorldCrowdFixtureContract.WorldActualKind;
            Assert.That(FixtureFails(crossed, envelope, out var abstractKind), Is.True);
            Assert.That(abstractKind, Does.Contain("Abstract counterpart"));

            var claimed = Fixture();
            claimed.AbstractCounterpart.ActualGeometry = true;
            Assert.That(FixtureFails(claimed, envelope, out var geometryClaim), Is.True);
            Assert.That(geometryClaim, Does.Contain("Abstract counterpart"));
        }

        [Test] public void AbstractUnitNumbersCannotBeRecordedUnderTheWorldActualFixtureKind()
        {
            var envelope = MeasureAuthoredEnvelopeM();
            var actual = Run(actualRoster, WorldCrowdFixtureContract.WorldActualKind, "actual");
            var counterpart = Run(AbstractRoster(), WorldCrowdFixtureContract.AbstractControlKind, "abstract");
            Assert.That(counterpart.radiusM, Is.LessThan(envelope));
            Assert.That(actual.radiusM, Is.GreaterThanOrEqualTo(envelope));
            Assert.That(Identity(counterpart), Is.Not.EqualTo(Identity(actual)));
            Assert.That(WorldCrowdFixtureContract.WorldActualKind, Is.Not.EqualTo(WorldCrowdFixtureContract.AbstractUnitKind));

            // Re-declaring the abstract roster numbers as the world actual fixture must fail the same contract.
            var mislabeled = Fixture();
            mislabeled.Body.RadiusM = counterpart.radiusM;
            mislabeled.Body.Count = counterpart.bodyCount;
            Assert.That(FixtureFails(mislabeled, envelope, out var failure), Is.True);
            Assert.That(failure, Does.Contain("smaller than the authored model envelope"));
        }

        [Test] public void ExistingAbstractUnitFixturesAreStillPresentAndCarryTheirOwnKind()
        {
            foreach (var source in AbstractFixtureSources)
            {
                Assert.That(File.Exists(ProjectFile(source.Path)), Is.True, "Abstract fixture deleted: " + source.Path);
                Assert.That(File.ReadAllText(ProjectFile(source.Path)), Does.Contain(source.Anchor),
                    "The declared anchor '" + source.Anchor + "' must survive in " + source.Path);
            }
            var fixture = Fixture();
            Assert.That(fixture.AbstractCounterpart.Sources.Length, Is.EqualTo(AbstractFixtureSources.Length));
            // The fixture names each source with a trailing parenthetical reason; the file path must lead it.
            foreach (var source in AbstractFixtureSources)
                Assert.That(fixture.AbstractCounterpart.Sources.Any(s => s == source.Path || s.StartsWith(source.Path + " ", StringComparison.Ordinal)),
                    Is.True, "Fixture does not name the abstract source " + source.Path);
            Assert.That(fixture.AbstractCounterpart.RadiusM, Is.EqualTo(WorldCrowdFixtureContract.AbstractUnitRadiusM));
            Assert.That(fixture.AbstractCounterpart.DeletionPolicy, Is.Not.Null.And.Not.Empty);
            Assert.That(fixture.NegativeContract.Deletion, Is.Not.Null.And.Not.Empty);
            Assert.That(fixture.NegativeContract.NonExecution, Does.Contain("Assert.Ignore"),
                "The fixture must forbid the silent non-execution gate this file used to carry.");
            Assert.That(fixture.AbstractCounterpart.ReverseDirectionRule, Does.Contain(WorldCrowdFixtureContract.AbstractControlKind));
            Assert.That(fixture.AbstractCounterpart.LaboratoryCorridorKind,
                Is.EqualTo(WorldCrowdFixtureContract.AbstractUnitKind));
            Assert.That(fixture.FixtureKind, Is.Not.EqualTo(fixture.AbstractCounterpart.FixtureKind));
            Assert.That(fixture.AbstractCounterpart.FixtureKind,
                Is.Not.EqualTo(WorldCrowdFixtureContract.AbstractUnitKind),
                "The world-geometry control must not be labelled as the laboratory corridor fixture it names.");
            Assert.That(fixture.AbstractCounterpart.ReverseDirectionRule, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// The REVERSE direction the fixture's counterpart rule declares. The forward direction (an abstract
        /// record presented as the world actual fixture) is covered above; here the world actual roster is
        /// presented as the abstract counterpart, and as the laboratory corridor kind, and both are refused.
        /// </summary>
        [Test] public void WorldActualNumbersCannotBeRecordedUnderTheAbstractCounterpartKind()
        {
            var envelope = MeasureAuthoredEnvelopeM();
            var actual = Run(actualRoster, WorldCrowdFixtureContract.WorldActualKind, "actual");
            var counterpart = Run(AbstractRoster(), WorldCrowdFixtureContract.AbstractControlKind, "abstract");

            // Both records satisfy their own kind, so neither refusal below is an accident of a broken record.
            Assert.That(KindMismatchFails(WorldCrowdFixtureContract.WorldActualKind,
                WorldCrowdFixtureContract.WorldActualKind, actual, out _), Is.False);
            Assert.That(KindMismatchFails(WorldCrowdFixtureContract.AbstractControlKind,
                WorldCrowdFixtureContract.AbstractControlKind, counterpart, out _), Is.False);

            // Forward: the abstract control cannot be presented as the world actual fixture.
            Assert.That(KindMismatchFails(WorldCrowdFixtureContract.AbstractControlKind,
                WorldCrowdFixtureContract.WorldActualKind, counterpart, out var forward), Is.True);
            Assert.That(forward, Does.Contain("cannot be recorded under"));

            // Reverse: the world actual roster cannot be presented as the abstract control, nor as the
            // laboratory corridor kind whose fixtures never read the captured world geometry at all.
            Assert.That(KindMismatchFails(WorldCrowdFixtureContract.WorldActualKind,
                WorldCrowdFixtureContract.AbstractControlKind, actual, out var reverse), Is.True);
            Assert.That(reverse, Does.Contain("cannot be recorded under"));
            Assert.That(KindMismatchFails(WorldCrowdFixtureContract.WorldActualKind,
                WorldCrowdFixtureContract.AbstractUnitKind, actual, out var laboratory), Is.True);
            Assert.That(laboratory, Does.Contain("cannot be recorded under"));

            // A record that keeps its own kind but carries the other roster's identity hash is refused too,
            // because the declared axis would then not describe the record it is attached to.
            var borrowed = JsonUtility.FromJson<RunReceipt>(JsonUtility.ToJson(actual));
            borrowed.fixtureIdentitySha256 = counterpart.fixtureIdentitySha256;
            Assert.That(KindMismatchFails(WorldCrowdFixtureContract.WorldActualKind,
                WorldCrowdFixtureContract.WorldActualKind, borrowed, out var borrowedFailure), Is.True);
            Assert.That(borrowedFailure, Does.Contain("identity hash"));

            // The declarations agree with the measured records.
            Assert.That(counterpart.fixtureKind, Is.EqualTo(WorldCrowdFixtureContract.AbstractControlKind));
            Assert.That(counterpart.fixtureKind, Is.Not.EqualTo(WorldCrowdFixtureContract.AbstractUnitKind));
            Assert.That(counterpart.fixtureIdentitySha256, Is.Not.EqualTo(actual.fixtureIdentitySha256));
            Assert.That(Identity(counterpart), Is.Not.EqualTo(Identity(actual)));
            Assert.That(counterpart.radiusM, Is.LessThan(envelope));
            // The control shares the captured world geometry with the world actual record - which is exactly
            // why it must not be labelled as a laboratory corridor fixture.
            Assert.That(counterpart.geometrySignature, Is.EqualTo(actual.geometrySignature));

            var fixture = Fixture();
            Assert.That(fixture.AbstractCounterpart.FixtureKind,
                Is.EqualTo(WorldCrowdFixtureContract.AbstractControlKind));
            Assert.That(fixture.AbstractCounterpart.FixtureKind,
                Is.Not.EqualTo(WorldCrowdFixtureContract.AbstractUnitKind));
            Assert.That(fixture.AbstractCounterpart.Rule, Does.Contain(nameof(RunReceipt.fixtureIdentitySha256)));
            Assert.That(fixture.AbstractCounterpart.Rule, Does.Not.Contain("fixtureHashSha256"));
        }

        /// <summary>
        /// Closes the shared contract gap: the fixture's MeasurementItems list must name fields the record
        /// really implements. A declared separation axis that occurs zero times in the implementation makes the
        /// receipt's own contract vacuous, so this is enforced reflectively and not by a length check alone.
        /// </summary>
        [Test] public void TheDeclaredMeasurementItemsAreImplementedByTheRecordItDescribes()
        {
            var declared = Fixture().MeasurementItems;
            Assert.That(declared.Length, Is.EqualTo(WorldCrowdFixtureContract.MeasurementItemCount));
            Assert.That(declared.Distinct().Count(), Is.EqualTo(declared.Length),
                "A measurement item must not be declared twice.");
            var implemented = typeof(RunReceipt).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var item in declared)
                Assert.That(implemented.Contains(item), Is.True,
                    "The fixture declares the measurement item '" + item + "', but no RunReceipt field implements " +
                    "it: a declared separation axis that occurs zero times in the implementation is a vacuous contract.");
            Assert.That(declared, Does.Contain(nameof(RunReceipt.fixtureIdentitySha256)),
                "The axis the counterpart rule names must be one of the declared measurement items.");
            Assert.That(declared, Does.Not.Contain("fixtureHashSha256"),
                "The old name had no implementation; it must not survive as a declared axis.");
        }
    }
}
