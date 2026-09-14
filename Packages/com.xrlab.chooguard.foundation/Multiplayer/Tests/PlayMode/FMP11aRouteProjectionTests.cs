using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-11a candidate acceptance: the 13-region / 12-portal authored profile drives the
    /// connected-world navigation display, including explicit unavailable states for observed
    /// closures. Every geometric input below is the synthetic design profile; nothing here is a
    /// surveyed facility or a facility acceptance claim.
    /// </summary>
    public sealed class FMP11aRouteProjectionTests
    {
        private const string ProfilePath = "foundation/world/connected-world-profile.json";
        private const string TestSourcePath =
            "Packages/com.xrlab.chooguard.foundation/Multiplayer/Tests/PlayMode/FMP11aRouteProjectionTests.cs";

        private static ConnectedWorldDefinition Definition()
        {
            var definition = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ProfilePath));
            Assert.That(definition, Is.Not.Null, "synthetic profile must parse");
            definition.Validate();
            return definition;
        }

        private static FieldView View(ConnectedWorldDefinition definition, string regionId, params string[] closedPortalIds)
        {
            var closed = new HashSet<string>(closedPortalIds ?? Array.Empty<string>());
            return new FieldView
            {
                RegionId = regionId,
                FrameId = definition.Region(regionId).FrameId,
                SpatialProfileId = definition.ProfileId,
                Portals = definition.Portals
                    .Select(p => new ObservedPortalState { PortalId = p.Id, Open = !closed.Contains(p.Id) }).ToArray()
            };
        }

        private static string[] ReachableFrom(ConnectedWorldDefinition definition, string startRegionId, params string[] closedPortalIds)
        {
            var closed = new HashSet<string>(closedPortalIds ?? Array.Empty<string>());
            return definition.Regions.Where(r => definition.Route(startRegionId, r.Id, closed).Length > 0)
                .Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        // Acceptance 1: 13 profile region IDs each map to one displayed current-location state.
        [Test]
        public void EachOfThirteenRegionIdsMapsToExactlyOneCurrentLocationState()
        {
            var world = Definition();
            Assert.That(world.Regions, Has.Length.EqualTo(13), "profile region count");
            Assert.That(world.Portals, Has.Length.EqualTo(12), "profile portal count");

            var first = world.Regions.Select(r => NetworkFieldRuntime.ProjectCurrentLocation(world, View(world, r.Id))).ToArray();
            var second = world.Regions.Select(r => NetworkFieldRuntime.ProjectCurrentLocation(world, View(world, r.Id))).ToArray();

            for (var index = 0; index < world.Regions.Length; index++)
            {
                var region = world.Regions[index]; var state = first[index];
                Assert.That(state.RegionId, Is.EqualTo(region.Id), region.Id);
                Assert.That(state.RegionLabel, Is.EqualTo(region.Label), region.Id);
                Assert.That(state.FrameId, Is.EqualTo(region.FrameId), region.Id);
                Assert.That(state.GeometryStatus, Is.EqualTo(region.GeometryStatus), region.Id);
                Assert.That(state.Synthetic, Is.True, region.Id);
                Assert.That(state.SyntheticLabel, Is.Not.Empty, region.Id);
                Assert.That(state.Available, Is.False, "no target means no route");
                Assert.That(state.UnavailableReason, Is.Not.Empty, region.Id);
                Assert.That(second[index].RegionId, Is.EqualTo(state.RegionId), "projection must be pure: " + region.Id);
                Assert.That(second[index].RegionLabel, Is.EqualTo(state.RegionLabel), "projection must be pure: " + region.Id);
            }

            Assert.That(first.Select(s => s.RegionId).Distinct().Count(), Is.EqualTo(13), "13 distinct current-location states");
            Assert.That(first.Select(s => s.RegionLabel).Distinct().Count(), Is.EqualTo(13), "13 distinct displayed labels");
            Assert.That(first.Select(s => s.FrameId).Distinct().Count(), Is.EqualTo(3), "3 authored frames");
        }

        // Acceptance 2: 12 portal IDs drive inter-floor / inter-region route transition in an executed test.
        [Test]
        public void EachOfTwelvePortalIdsDrivesInterFloorOrInterRegionRouteTransition()
        {
            var world = Definition();
            var levelChanging = new List<string>(); var frameChanging = new List<string>();

            foreach (var portal in world.Portals)
            {
                var forward = NetworkFieldRuntime.ProjectRoute(world, View(world, portal.From), portal.To);
                Assert.That(forward.Available, Is.True, "adjacent portal must be routable: " + portal.Id);
                CollectionAssert.AreEqual(new[] { portal.From, portal.To }, forward.RouteRegionIds, portal.Id);
                CollectionAssert.AreEqual(new[] { portal.Id }, forward.RoutePortalIds, portal.Id);
                CollectionAssert.AreEqual(new[] { portal.GeometryStatus }, forward.RouteGeometryStatuses, portal.Id);
                Assert.That(forward.RouteLabels, Has.Length.EqualTo(2), portal.Id);
                Assert.That(forward.UnavailableReason, Is.Empty, portal.Id);

                var reverse = NetworkFieldRuntime.ProjectRoute(world, View(world, portal.To), portal.From);
                Assert.That(reverse.Available, Is.True, portal.Id);
                CollectionAssert.AreEqual(new[] { portal.To, portal.From }, reverse.RouteRegionIds, portal.Id);

                var toFrom = NetworkFieldRuntime.ProjectRoute(world, View(world, world.StartRegionId), portal.From);
                var toTo = NetworkFieldRuntime.ProjectRoute(world, View(world, world.StartRegionId), portal.To);
                Assert.That(toFrom.RoutePortalIds.Contains(portal.Id) || toTo.RoutePortalIds.Contains(portal.Id), Is.True,
                    "portal must carry an actual route from the start region: " + portal.Id);

                if (portal.FromPoint.Y != portal.ToPoint.Y) { levelChanging.Add(portal.Id); Assert.That(forward.CrossesLevel, Is.True, portal.Id); }
                else Assert.That(forward.CrossesLevel, Is.False, portal.Id);
                if (world.Region(portal.From).FrameId != world.Region(portal.To).FrameId) { frameChanging.Add(portal.Id); Assert.That(forward.CrossesFrame, Is.True, portal.Id); }
                else Assert.That(forward.CrossesFrame, Is.False, portal.Id);
            }

            Assert.That(world.Portals, Has.Length.EqualTo(12), "12 authored portals");
            Assert.That(ReachableFrom(world, world.StartRegionId), Has.Length.EqualTo(13), "complete coverage tree");
            CollectionAssert.AreEquivalent(new[]
            {
                "rail_tracks_mainline--rail_platforms_mainline",
                "rail_platforms_mainline--station_concourse_2f",
                "station_hall_1f--station_concourse_2f",
                "rail_terminal_public--underground_connector",
                "forecourt_eurasia--underground_connector",
                "metro_concourse--metro_platforms"
            }, levelChanging, "authored level-changing portals");
            CollectionAssert.AreEquivalent(new[]
            {
                "rolling_stock_mainline--rail_platforms_mainline",
                "rolling_stock_metro--metro_platforms"
            }, frameChanging, "authored frame-changing portals");

            var longRoute = NetworkFieldRuntime.ProjectRoute(world, View(world, world.StartRegionId), "metro_platforms");
            CollectionAssert.AreEqual(new[]
            {
                "station_concourse_2f", "rail_terminal_public", "underground_connector",
                "underground_shopping_passage", "metro_concourse", "metro_platforms"
            }, longRoute.RouteRegionIds, "authored start-to-metro route");
            Assert.That(longRoute.RoutePortalIds, Has.Length.EqualTo(5));
            Assert.That(longRoute.CrossesLevel, Is.True, "start-to-metro route crosses authored levels");
            Assert.That(longRoute.CrossesFrame, Is.False, "start-to-metro route stays inside one frame");
            var metroLineRoute = NetworkFieldRuntime.ProjectRoute(world, View(world, world.StartRegionId), "rolling_stock_metro");
            Assert.That(metroLineRoute.CrossesFrame, Is.True, "metro rolling stock uses the train-metro frame");
        }

        // Acceptance 3: blocked equipment/portal state changes the route result or produces an explicit unavailable state.
        [Test]
        public void BlockedPortalProducesRouteChangeOrExplicitUnavailableState()
        {
            var world = Definition();
            var start = world.StartRegionId;
            const string tracksEdge = "rail_tracks_mainline--rail_platforms_mainline";
            const string concourseEdge = "rail_terminal_public--station_concourse_2f";

            var openRoute = NetworkFieldRuntime.ProjectRoute(world, View(world, start), "metro_platforms");
            var leafBlocked = NetworkFieldRuntime.ProjectRoute(world, View(world, start, tracksEdge), "metro_platforms");
            Assert.That(leafBlocked.Available, Is.True, "blocking a leaf edge must not detour the trunk route");
            CollectionAssert.AreEqual(openRoute.RouteRegionIds, leafBlocked.RouteRegionIds, "no invented bypass");
            CollectionAssert.AreEqual(openRoute.RoutePortalIds, leafBlocked.RoutePortalIds, "no invented bypass");
            Assert.That(ReachableFrom(world, start), Has.Length.EqualTo(13));
            Assert.That(ReachableFrom(world, start, tracksEdge), Has.Length.EqualTo(12));
            var orphaned = NetworkFieldRuntime.ProjectRoute(world, View(world, start, tracksEdge), "rail_tracks_mainline");
            Assert.That(orphaned.Available, Is.False, "a closed bridge edge strands its far side");
            Assert.That(orphaned.UnavailableReason, Is.Not.Empty);
            Assert.That(orphaned.UnavailableReason, Does.Contain("사용 불가"));

            var closed = new[] { concourseEdge };
            var explicitUnavailable = NetworkFieldRuntime.ProjectRoute(world, View(world, start, concourseEdge), "metro_platforms");
            Assert.That(explicitUnavailable.RegionId, Is.EqualTo(start), "current location survives an unreachable target");
            Assert.That(explicitUnavailable.RegionLabel, Is.EqualTo(world.Region(start).Label));
            Assert.That(explicitUnavailable.Available, Is.False, "explicit unavailable state instead of an empty route line");
            Assert.That(explicitUnavailable.UnavailableReason, Is.Not.Empty);
            Assert.That(explicitUnavailable.UnavailableReason, Does.Contain("사용 불가"));
            Assert.That(explicitUnavailable.RouteRegionIds, Is.Empty);
            Assert.That(explicitUnavailable.Synthetic, Is.True);
            Assert.That(explicitUnavailable.RouteSyntheticLabel, Is.Not.Empty);
            CollectionAssert.AreEquivalent(new[]
            {
                "rolling_stock_mainline", "rail_tracks_mainline", "rail_platforms_mainline",
                "station_concourse_2f", "station_hall_1f"
            }, ReachableFrom(world, start, closed), "closed trunk edge leaves exactly one component");
            var stillReachable = NetworkFieldRuntime.ProjectRoute(world, View(world, start, concourseEdge), "rail_platforms_mainline");
            Assert.That(stillReachable.Available, Is.True, "the surviving component is still routable");
            CollectionAssert.AreEqual(new[] { start, "rail_platforms_mainline" }, stillReachable.RouteRegionIds);

            // The three portals with no linked door equipment can never close from equipment state, so the
            // observed view is the only closure channel. A synthetic injected closure must still be honoured.
            var unlinked = world.Portals.Where(p => p.LinkedDoorEntityIds.Length == 0).Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "forecourt_eurasia--underground_connector",
                "rail_terminal_public--underground_connector",
                "underground_shopping_passage--metro_concourse"
            }, unlinked, "portals without linked door equipment");
            var injected = NetworkFieldRuntime.ProjectRoute(world, View(world, start, unlinked), "metro_platforms");
            Assert.That(injected.Available, Is.False, "injected synthetic closure must still produce an unavailable state");
            Assert.That(injected.UnavailableReason, Does.Contain("사용 불가"));
            Assert.That(ReachableFrom(world, start, unlinked), Has.Length.EqualTo(7));
        }

        // Acceptance 4: render evidence labels all facility geometry and route data synthetic.
        [Test]
        public void RouteDisplayCarriesSyntheticLabelForObservedGeometry()
        {
            var world = Definition();
            Assert.That(world.Classification, Is.EqualTo("synthetic_design_not_facility_acceptance"));

            foreach (var region in world.Regions)
            {
                var state = NetworkFieldRuntime.ProjectCurrentLocation(world, View(world, region.Id));
                Assert.That(state.Synthetic, Is.True, region.Id);
                Assert.That(state.Classification, Is.EqualTo(world.Classification), region.Id);
                Assert.That(state.GeometryStatus, Is.EqualTo(region.GeometryStatus), region.Id);
                Assert.That(state.SyntheticLabel, Does.Contain("합성"), region.Id);
                Assert.That(state.SyntheticLabel, Does.Contain(world.Classification), region.Id);
                Assert.That(state.SyntheticLabel, Does.Contain(region.GeometryStatus), region.Id);
                Assert.That(state.SyntheticLabel, Does.Contain("인증 아님"), region.Id);
            }

            foreach (var portal in world.Portals)
            {
                var state = NetworkFieldRuntime.ProjectRoute(world, View(world, portal.From), portal.To);
                Assert.That(state.Synthetic, Is.True, portal.Id);
                Assert.That(state.RouteSyntheticLabel, Does.Contain("합성"), portal.Id);
                Assert.That(state.RouteSyntheticLabel, Does.Contain("인증 아님"), portal.Id);
                Assert.That(state.RouteSyntheticLabel, Does.Contain(portal.GeometryStatus), portal.Id);
            }

            var unreachable = NetworkFieldRuntime.ProjectRoute(world, View(world, world.StartRegionId,
                "rail_terminal_public--station_concourse_2f"), "metro_platforms");
            Assert.That(unreachable.Synthetic, Is.True);
            Assert.That(unreachable.SyntheticLabel, Does.Contain("합성"));
            Assert.That(unreachable.RouteSyntheticLabel, Does.Contain("합성"));
            Assert.That(world.Limits, Is.Not.Empty, "authored synthetic limits are declared");
            Assert.That(string.Join(" ", world.Limits).ToLowerInvariant(), Does.Contain("synthetic"));
        }

        // Acceptance 5: PlayMode XML and render evidence include source/build hash and the actual test result.
        [Test]
        public void RunRecordBindsSourceAndBuildHashToActualResult()
        {
            var world = Definition();
            var record = new RunRecord
            {
                TestClass = GetType().FullName,
                ProfilePath = ProfilePath,
                ProfileSha256 = Sha256OfFile(ProfilePath),
                TestSourcePath = TestSourcePath,
                TestSourceSha256 = Sha256OfFile(TestSourcePath),
                EditorVersion = Application.unityVersion,
                BuildGUID = Application.buildGUID ?? "",
                Platform = Application.platform.ToString(),
                Result = "this test is reported only when the PlayMode XML records it"
            };
            var location = typeof(NetworkFieldRuntime).Assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                record.BuildHash = Sha256OfFile(location);
                record.BuildHashSource = "assembly:" + location;
                record.AssemblyPath = location;
            }
            else
            {
                record.BuildHash = Sha256OfBytes(Encoding.UTF8.GetBytes(Application.unityVersion + "|" + record.BuildGUID));
                record.BuildHashSource = "editor-identity-fallback(no assembly location)";
                record.AssemblyPath = "";
            }
            record.RegionCount = world.Regions.Length;
            record.PortalCount = world.Portals.Length;
            record.Synthetic = true;
            record.Classification = world.Classification;
            record.SourceRef = Option("--cg-fmp11a-source-ref", "");
            var pinnedProfile = Option("--cg-fmp11a-pinned-profile-sha256", "");
            record.PinnedProfileSha256 = pinnedProfile;
            record.ExternalProfileSha256Pin = !string.IsNullOrEmpty(pinnedProfile);

            Assert.That(record.ProfileSha256, Has.Length.EqualTo(64), "profile sha256 must be a real digest");
            Assert.That(record.ProfileSha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(record.TestSourceSha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(record.BuildHash, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(record.EditorVersion, Is.Not.Empty);
            Assert.That(record.RegionCount, Is.EqualTo(13));
            Assert.That(record.PortalCount, Is.EqualTo(12));
            if (!string.IsNullOrEmpty(pinnedProfile))
                Assert.That(record.ProfileSha256, Is.EqualTo(pinnedProfile),
                    "profile digest must match the externally pinned digest recorded before this run");
            if (!string.IsNullOrEmpty(record.SourceRef))
                Assert.That(record.SourceRef, Does.Match("^[0-9a-f]{40}$"), "source ref must be a git commit sha");

            record.RenderEvidence = world.Regions.Select(region =>
            {
                var state = NetworkFieldRuntime.ProjectRoute(world, View(world, region.Id), world.StartRegionId);
                return "현재: " + state.RegionLabel + " · " + state.FrameId + " · N 구역 길찾기 | " + state.SyntheticLabel +
                    " | 공개 연결: " + (state.Available ? string.Join(" → ", state.RouteLabels) : state.UnavailableReason) +
                    " | " + state.RouteSyntheticLabel;
            }).ToArray();
            record.PortalRenderEvidence = world.Portals.Select(portal =>
            {
                var state = NetworkFieldRuntime.ProjectRoute(world, View(world, portal.From), portal.To);
                return portal.Id + " => 공개 연결: " + string.Join(" → ", state.RouteLabels) + " | " + state.RouteSyntheticLabel;
            }).ToArray();
            var blockedRender = NetworkFieldRuntime.ProjectRoute(world,
                View(world, world.StartRegionId, "rail_terminal_public--station_concourse_2f"), "metro_platforms");
            record.UnavailableRenderEvidence = "현재: " + blockedRender.RegionLabel + " | 공개 연결: " +
                blockedRender.UnavailableReason + " | " + blockedRender.RouteSyntheticLabel;

            Assert.That(record.RenderEvidence, Has.Length.EqualTo(13), "one rendered line per profile region");
            Assert.That(record.PortalRenderEvidence, Has.Length.EqualTo(12), "one rendered line per profile portal");
            Assert.That(record.RenderEvidence.All(line => line.Contains("합성") && line.Contains("인증 아님")), Is.True,
                "every rendered location line must carry the synthetic banner");
            Assert.That(record.PortalRenderEvidence.All(line => line.Contains("합성") && line.Contains("인증 아님")), Is.True,
                "every rendered route line must carry the synthetic banner");
            Assert.That(record.UnavailableRenderEvidence, Does.Contain("사용 불가"));
            Assert.That(record.UnavailableRenderEvidence, Does.Contain("합성"));
            Assert.That(record.UnavailableRenderEvidence, Does.Contain("인증 아님"));

            var target = Option("--cg-fmp11a-run-record", "");
            if (string.IsNullOrEmpty(target)) { Debug.Log("FMP-11a run record was not requested: " + JsonUtility.ToJson(record)); return; }
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(target, JsonUtility.ToJson(record, true));
            var reread = JsonUtility.FromJson<RunRecord>(File.ReadAllText(target));
            Assert.That(reread, Is.Not.Null);
            Assert.That(reread.ProfileSha256, Is.EqualTo(record.ProfileSha256));
            Assert.That(reread.TestSourceSha256, Is.EqualTo(record.TestSourceSha256));
            Assert.That(reread.BuildHash, Is.EqualTo(record.BuildHash));
            Assert.That(reread.EditorVersion, Is.EqualTo(record.EditorVersion));
            Assert.That(reread.SourceRef, Is.EqualTo(record.SourceRef));
            Assert.That(reread.RenderEvidence, Has.Length.EqualTo(13));
            Assert.That(reread.PortalRenderEvidence, Has.Length.EqualTo(12));
            Assert.That(reread.UnavailableRenderEvidence, Is.EqualTo(record.UnavailableRenderEvidence));
            Assert.That(reread.RegionCount, Is.EqualTo(13));
            Assert.That(reread.PortalCount, Is.EqualTo(12));
        }

        [Serializable] private sealed class RunRecord
        {
            public string TestClass, ProfilePath, ProfileSha256, TestSourcePath, TestSourceSha256;
            public string EditorVersion, BuildGUID, Platform, BuildHash, BuildHashSource, AssemblyPath;
            public string PinnedProfileSha256 = "", SourceRef = "", Result = "", Classification = "";
            public string[] RenderEvidence = Array.Empty<string>(), PortalRenderEvidence = Array.Empty<string>();
            public string UnavailableRenderEvidence = "";
            public bool ExternalProfileSha256Pin, Synthetic;
            public int RegionCount, PortalCount;
        }

        private static string Option(string name, string fallback)
        {
            // Unity receives these as custom switches; accept either -name or --name so a
            // single-dash invocation on the Unity command line is not silently ignored.
            var bare = name.TrimStart('-');
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index].TrimStart('-'), bare, StringComparison.Ordinal)) continue;
                return index + 1 < args.Length ? args[index + 1] : fallback;
            }
            return fallback;
        }

        private static string Sha256OfFile(string path)
        {
            Assert.That(File.Exists(path), Is.True, "hash input must exist: " + path);
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        private static string Sha256OfBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }
    }
}
