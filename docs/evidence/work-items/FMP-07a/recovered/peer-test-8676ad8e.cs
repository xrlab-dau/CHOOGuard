using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
                EveryCapturedColliderMustBePresentEnabledAndUnchanged, NoColliderMayBeSilentlySkipped;
            public string MissingRegionPolicy, UnloadedColliderPolicy;
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
            public int[] PerRegionCounts;
            public int RegionsCovered;
            public double SlotSpacingM;
        }
        [Serializable] internal sealed class RegressionAssertions
        {
            public bool EveryTickCommitted, AllBodiesStayInTheWorldContactSpace, MinimumSweptGapMNonNegative,
                MinimumCentreClearanceMNonNegative, MaximumGroundSpeedAtMostPreferredSpeed,
                NoBodyEverIntersectsAnAuthoredCollider, BodyCountAndRadiiUnchangedAtTheEnd;
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
            public string FixtureKind, Rule, DeletionPolicy;
            public bool ActualGeometry, RecordedSeparately;
            public double RadiusM, HeightM, PersonRepulsion, RepulsionRangeM;
            public string[] Sources;
        }
        [Serializable] internal sealed class NegativeContract
        {
            public string MissingRegion, UnloadedCollider, WrongFrame, UndersizedRadius, MislabeledAbstract, Deletion;
        }
        [Serializable] internal sealed class Fixture
        {
            public int SchemaVersion;
            public string WorkId, Title, Classification, GeometricClassification, FixtureKind, ContractNature;
            public long Seed;
            public SourceProfile SourceProfile;
            public SceneHash SceneHash;
            public GeometryLoad GeometryLoad;
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
                !fixture.GeometryLoad.NoColliderMayBeSilentlySkipped)
            { failure = "Geometry load must be the declared thirteen region scenes with every authored collider present and unchanged."; return false; }
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
                fixture.AbstractCounterpart.ActualGeometry || !fixture.AbstractCounterpart.RecordedSeparately ||
                fixture.AbstractCounterpart.RadiusM >= measuredModelEnvelopeM ||
                fixture.AbstractCounterpart.Sources == null || fixture.AbstractCounterpart.Sources.Length == 0 ||
                string.IsNullOrEmpty(fixture.AbstractCounterpart.DeletionPolicy))
            { failure = "Abstract counterpart must be a different kind and must not claim actual geometry."; return false; }
            if (fixture.NegativeContract == null || new[] { fixture.NegativeContract.MissingRegion, fixture.NegativeContract.UnloadedCollider,
                fixture.NegativeContract.WrongFrame, fixture.NegativeContract.UndersizedRadius,
                fixture.NegativeContract.MislabeledAbstract, fixture.NegativeContract.Deletion }.Any(string.IsNullOrEmpty))
            { failure = "The fixture must declare all six negative clauses."; return false; }
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
    /// KIND SEPARATION. The fixture declares fixtureKind = world_actual_collider_nav; the abstract counterpart
    /// is declared as abstract_unit_laboratory_corridor with actualGeometry = false. Both rosters are measured
    /// with the SAME measurement item list and recorded under DIFFERENT kinds and DIFFERENT fixture hashes.
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
            public string fixtureKind, executableTest, sourceProfileSha256, sceneManifestSha256, geometrySignature;
            public int bodyCount, distinctRegionCount, committedTicks, supportCount, colliderCount, wallCount, pinnedCount;
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
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) Assert.Ignore("Preserve unsaved scenes.");
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

        /// <summary>The abstract counterpart shares the actual slots and differs only in the body-size axis.</summary>
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

        private RunReceipt Run(List<WorldBodySpawn> roster, string kind, string label)
        {
            var receipt = new RunReceipt
            {
                fixtureKind = kind, executableTest = nameof(Fmp07aWorldCrowdTests), seed = WorldSeed,
                bodyCount = roster.Count, radiusM = roster[0].RadiusM, heightM = roster[0].HeightM,
                pinnedCount = roster.Count(s => s.Pinned), tickSeconds = TickSeconds, simulatedSeconds = TickCount * TickSeconds,
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
                receipt.minimumSweptGapM = Math.Min(receipt.minimumSweptGapM, report.MinimumSweptGapM);
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
                receipt.minimumCentreClearanceM = Math.Min(receipt.minimumCentreClearanceM, MinimumClearanceM(agents));
                Assert.That(adapter.ValidateBodies(out var failure), Is.True, label + " validation " + failure);
            }
            if (double.IsPositiveInfinity(receipt.minimumSweptGapM)) receipt.minimumSweptGapM = 0;
            if (double.IsPositiveInfinity(receipt.minimumCentreClearanceM)) receipt.minimumCentreClearanceM = 0;
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
            Assert.That(receipt.maximumGroundSpeedMS, Is.LessThanOrEqualTo(1.5), label + " ground speed");
            Assert.That(receipt.geometrySignature, Is.Not.Null.And.Not.Empty, label + " geometry signature");
            Assert.That(receipt.measurementItems.Length, Is.EqualTo(WorldCrowdFixtureContract.MeasurementItemCount), label + " measurement items");
        }

        /// <summary>The record identity: kind, body size, seed and the captured geometry hash. Two records that
        /// disagree on any of these must never be presented as the same fixture.</summary>
        private static string Identity(RunReceipt receipt) => Hash(JsonUtility.ToJson(new RunReceipt
        {
            fixtureKind = receipt.fixtureKind, bodyCount = receipt.bodyCount, radiusM = receipt.radiusM, heightM = receipt.heightM,
            seed = receipt.seed, tickSeconds = receipt.tickSeconds, sceneManifestSha256 = receipt.sceneManifestSha256,
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
            Assert.That(fixture.AbstractCounterpart.FixtureKind, Is.EqualTo(WorldCrowdFixtureContract.AbstractUnitKind));
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
            // The declared no-overlap placement invariant, on the same axis rule TryFindStandingPoint enforces.
            for (var i = 0; i < actualRoster.Count; i++)
            for (var j = i + 1; j < actualRoster.Count; j++)
            {
                var a = actualRoster[i].Pose.Position; var b = actualRoster[j].Pose.Position;
                if (Math.Abs(a.Y - b.Y) >= WorldBodyPresentation.NpcHeightM) continue;
                var distance = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
                Assert.That(distance, Is.GreaterThanOrEqualTo(actualRoster[i].RadiusM + actualRoster[j].RadiusM - 1e-9),
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
            var counterpart = Run(AbstractRoster(), WorldCrowdFixtureContract.AbstractUnitKind, "abstract");
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
            Assert.That(json, Does.Contain(WorldCrowdFixtureContract.AbstractUnitKind));
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
            var counterpart = Run(AbstractRoster(), WorldCrowdFixtureContract.AbstractUnitKind, "abstract");
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
            Assert.That(fixture.FixtureKind, Is.Not.EqualTo(fixture.AbstractCounterpart.FixtureKind));
        }
    }
}
