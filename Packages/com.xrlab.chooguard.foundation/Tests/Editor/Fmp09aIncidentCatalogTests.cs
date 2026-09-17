using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    [Serializable]
    public sealed class IncidentCatalog
    {
        public string schemaVersion;
        public string catalogId;
        public string catalogVersion;
        public string classification;
        public string disclaimer;
        public string exaStatus;
        public string emptyApplicabilityMeans;
        public SourceEntry[] sources;
        public string[] executionAllowlist;
        public IncidentVariant[] reviewedVariants;
        public IncidentVariant[] unreviewedVariants;

        public IncidentCatalog Clone()
        {
            var json = JsonUtility.ToJson(this);
            return JsonUtility.FromJson<IncidentCatalog>(json);
        }
    }

    [Serializable]
    public sealed class SourceEntry
    {
        public string sourceId;
        public string publisherOperator;
        public string title;
        public string documentRevisionDate;
        public string revisionStatus;
        public string audience;
        public string[] applicableOperators;
        public string[] applicableStations;
        public string[] applicableLines;
        public string[] applicableVehicles;
        public string[] applicableRoles;
        public string[] applicableIncidentKinds;
        public string locator;
        public string originalUrl;
        public string sha256;
        public string[] allowedUse;
        public string[] limits;
        public string retrievedAt;
        public string verification;
        public string procedureAcceptance;
        public string discoveryMethod;
    }

    [Serializable]
    public sealed class IncidentVariant
    {
        public string id;
        public string choiceId;
        public string kind;
        public int kindOrdinal;
        public string regionId;
        public string resourceKey;
        public long onsetTick;
        public long durationTicks;
        public string recoveryCondition;
        public string sourceId;
        public IncidentApplicability applicability;
        public string reviewStatus;
        public string procedureAcceptance;
        public bool isExecutionCandidate;
        public string rationale;
    }

    [Serializable]
    public sealed class IncidentApplicability
    {
        public string @operator;
        public string[] stations;
        public string[] lines;
        public string[] vehicles;
        public string[] roles;
    }

    public static class IncidentCatalogValidator
    {
        public static readonly string[] ValidThirteenRegions = new[]
        {
            "rolling_stock_mainline",
            "rail_tracks_mainline",
            "rail_platforms_mainline",
            "rail_terminal_public",
            "station_concourse_2f",
            "station_hall_1f",
            "station_ticket_area",
            "forecourt_eurasia",
            "underground_connector",
            "underground_shopping_passage",
            "metro_concourse",
            "metro_platforms",
            "rolling_stock_metro"
        };

        public static readonly string[] ValidIncidentKinds = new[]
        {
            "Fire",
            "PowerLoss",
            "PublicAddressFailure",
            "RouteRestriction",
            "TrainFault",
            "EmergencyStop"
        };

        public static void Validate(IncidentCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentException("Catalog cannot be null.");

            if (catalog.schemaVersion != "1.0")
                throw new ArgumentException($"Invalid schemaVersion: {catalog.schemaVersion}");

            if (catalog.catalogId != "reviewed-incident-catalog")
                throw new ArgumentException($"Invalid catalogId: {catalog.catalogId}");

            if (catalog.catalogVersion != "1.0")
                throw new ArgumentException($"Invalid catalogVersion: {catalog.catalogVersion}");

            if (catalog.classification != "PUBLIC_REFERENCE_AND_SYNTHETIC_METADATA")
                throw new ArgumentException($"Invalid classification: {catalog.classification}");

            if (catalog.disclaimer != "KORAIL 검증 전 예시")
                throw new ArgumentException($"Invalid disclaimer: {catalog.disclaimer}");

            if (catalog.exaStatus != "blocked_403_1010")
                throw new ArgumentException($"Invalid exaStatus: {catalog.exaStatus}");

            if (catalog.emptyApplicabilityMeans != "unspecified_not_universal_permission")
                throw new ArgumentException($"Invalid emptyApplicabilityMeans: {catalog.emptyApplicabilityMeans}. Must be unspecified_not_universal_permission.");

            if (catalog.sources == null || catalog.sources.Length == 0)
                throw new ArgumentException("Catalog sources must not be empty.");

            var registeredSourceIds = new HashSet<string>(StringComparer.Ordinal);
            var sourceMap = new Dictionary<string, SourceEntry>(StringComparer.Ordinal);
            foreach (var s in catalog.sources)
            {
                if (string.IsNullOrWhiteSpace(s.sourceId))
                    throw new ArgumentException("Source entry missing sourceId.");
                if (!registeredSourceIds.Add(s.sourceId))
                    throw new ArgumentException($"Duplicate sourceId registered: {s.sourceId}");
                if (string.IsNullOrWhiteSpace(s.title))
                    throw new ArgumentException($"Source {s.sourceId} missing title.");
                if (string.IsNullOrWhiteSpace(s.publisherOperator))
                    throw new ArgumentException($"Source {s.sourceId} missing publisherOperator.");
                sourceMap[s.sourceId] = s;
            }

            if (catalog.executionAllowlist == null || catalog.executionAllowlist.Length == 0)
                throw new ArgumentException("executionAllowlist must not be empty.");

            var allowlistSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in catalog.executionAllowlist)
            {
                if (string.IsNullOrWhiteSpace(item))
                    throw new ArgumentException("executionAllowlist item cannot be empty.");
                if (!allowlistSet.Add(item))
                    throw new ArgumentException($"Duplicate choiceId in executionAllowlist: {item}");
            }

            if (catalog.reviewedVariants == null || catalog.reviewedVariants.Length == 0)
                throw new ArgumentException("reviewedVariants must not be empty.");

            if (catalog.unreviewedVariants == null)
                throw new ArgumentException("unreviewedVariants array must not be null.");

            var allIds = new HashSet<string>(StringComparer.Ordinal);
            var allChoiceIds = new HashSet<string>(StringComparer.Ordinal);
            var reviewedChoiceIds = new HashSet<string>(StringComparer.Ordinal);

            // Validate reviewed variants
            foreach (var v in catalog.reviewedVariants)
            {
                ValidateVariant(v, registeredSourceIds, sourceMap, isReviewed: true);
                if (!allIds.Add(v.id))
                    throw new ArgumentException($"Duplicate variant id: {v.id}");
                if (!allChoiceIds.Add(v.choiceId))
                    throw new ArgumentException($"Duplicate choiceId: {v.choiceId}");
                if (!reviewedChoiceIds.Add(v.choiceId))
                    throw new ArgumentException($"Duplicate reviewed choiceId: {v.choiceId}");

                if (!allowlistSet.Contains(v.choiceId))
                    throw new ArgumentException($"Reviewed variant choiceId {v.choiceId} missing from executionAllowlist.");
            }

            // 1:1 match between reviewedVariants and executionAllowlist
            if (allowlistSet.Count != reviewedChoiceIds.Count || !allowlistSet.SetEquals(reviewedChoiceIds))
                throw new ArgumentException("executionAllowlist must correspond 1:1 to reviewedVariants.");

            // Validate unreviewed variants
            foreach (var v in catalog.unreviewedVariants)
            {
                ValidateVariant(v, registeredSourceIds, sourceMap, isReviewed: false);
                if (!allIds.Add(v.id))
                    throw new ArgumentException($"Duplicate variant id: {v.id}");
                if (!allChoiceIds.Add(v.choiceId))
                    throw new ArgumentException($"Duplicate choiceId: {v.choiceId}");

                if (allowlistSet.Contains(v.choiceId))
                    throw new ArgumentException($"Unreviewed variant {v.choiceId} cannot be in executionAllowlist.");
            }
        }

        private static void ValidateVariant(IncidentVariant v, HashSet<string> registeredSources, Dictionary<string, SourceEntry> sourceMap, bool isReviewed)
        {
            if (v == null)
                throw new ArgumentException("Variant cannot be null.");

            if (string.IsNullOrWhiteSpace(v.id))
                throw new ArgumentException("Variant id cannot be empty.");
            if (string.IsNullOrWhiteSpace(v.choiceId))
                throw new ArgumentException("Variant choiceId cannot be empty.");

            // Review status checks
            if (isReviewed)
            {
                if (v.reviewStatus != "reviewed")
                    throw new ArgumentException($"Reviewed variant {v.id} has invalid reviewStatus: {v.reviewStatus}");
                if (v.procedureAcceptance != "provisional_synthetic_reviewed")
                    throw new ArgumentException($"Reviewed variant {v.id} must have procedureAcceptance provisional_synthetic_reviewed, got {v.procedureAcceptance}");
                if (!v.isExecutionCandidate)
                    throw new ArgumentException($"Reviewed variant {v.id} must have isExecutionCandidate == true.");
            }
            else
            {
                if (v.reviewStatus != "unreviewed")
                    throw new ArgumentException($"Unreviewed variant {v.id} has invalid reviewStatus: {v.reviewStatus}");
                if (v.procedureAcceptance != "pending_operator_review")
                    throw new ArgumentException($"Unreviewed variant {v.id} must have procedureAcceptance pending_operator_review, got {v.procedureAcceptance}");
                if (v.isExecutionCandidate)
                    throw new ArgumentException($"Unreviewed variant {v.id} cannot have isExecutionCandidate == true.");
            }

            // Kind and ordinal
            if (!ValidIncidentKinds.Contains(v.kind))
                throw new ArgumentException($"Undefined FoundationIncidentKind: {v.kind}");
            if (!Enum.TryParse(v.kind, out FoundationIncidentKind parsedKind))
                throw new ArgumentException($"Cannot parse FoundationIncidentKind: {v.kind}");
            if (v.kindOrdinal < 0 || v.kindOrdinal > 5 || v.kindOrdinal != (int)parsedKind)
                throw new ArgumentException($"Mismatched or invalid kindOrdinal {v.kindOrdinal} for kind {v.kind}");

            // Regions
            if (!ValidThirteenRegions.Contains(v.regionId))
                throw new ArgumentException($"Unknown or orphan regionId: {v.regionId}");
            if (!ValidThirteenRegions.Contains(v.resourceKey))
                throw new ArgumentException($"Unknown or orphan resourceKey: {v.resourceKey}");

            // Ticks
            if (v.onsetTick < 0)
                throw new ArgumentException($"Invalid onsetTick: {v.onsetTick}");
            if (v.durationTicks <= 0)
                throw new ArgumentException($"Invalid durationTicks: {v.durationTicks}");

            if (string.IsNullOrWhiteSpace(v.recoveryCondition))
                throw new ArgumentException($"Missing recoveryCondition for {v.id}");

            // Source check
            if (string.IsNullOrWhiteSpace(v.sourceId))
                throw new ArgumentException($"Missing sourceId for variant {v.id}");
            if (!registeredSources.Contains(v.sourceId))
                throw new ArgumentException($"Unregistered sourceId: {v.sourceId} for variant {v.id}");

            // Applicability
            if (v.applicability == null)
                throw new ArgumentException($"Missing applicability for variant {v.id}");

            var op = v.applicability.@operator;
            if (op != "KORAIL" && op != "HUMETRO" && op != "SR" && op != "SYNTHETIC")
                throw new ArgumentException($"Invalid applicability operator: {op}");

            var source = sourceMap[v.sourceId];

            // SR data conflation rule
            if (source.publisherOperator == "SR" || v.sourceId == "SR-LARGE-ACCIDENT-20240226")
            {
                if (op == "KORAIL")
                    throw new ArgumentException($"Cannot conflate SR operational data ({v.sourceId}) as KORAIL SOP.");
                if (op != "SR")
                    throw new ArgumentException($"SR operational source {v.sourceId} must have operator SR, got {op}");
            }

            // Universal permission rule for empty applicability
            bool emptyStations = v.applicability.stations == null || v.applicability.stations.Length == 0;
            bool emptyLines = v.applicability.lines == null || v.applicability.lines.Length == 0;
            bool emptyRoles = v.applicability.roles == null || v.applicability.roles.Length == 0;
            if (emptyStations && emptyLines && emptyRoles)
            {
                // Empty applicability cannot be used to promote an unreviewed or non-scoped item to universal execution
                if (v.isExecutionCandidate && op != "SYNTHETIC")
                    throw new ArgumentException("Empty applicability cannot confer universal execution permission for operator procedures.");
            }

            // Synthetic authority hallucination check
            if (v.sourceId == "CG-SYNTHETIC-FUNCTIONAL-INCIDENTS-V1" || source.publisherOperator == "SYNTHETIC")
            {
                if (op != "SYNTHETIC")
                    throw new ArgumentException($"Synthetic variant {v.id} cannot claim official operator authority ({op}).");
                if (v.procedureAcceptance != "provisional_synthetic_reviewed")
                    throw new ArgumentException($"Synthetic variant {v.id} cannot claim official procedure acceptance ({v.procedureAcceptance}).");
            }
        }

        public static IncidentSchedule BuildSchedule(IncidentCatalog catalog, string profileId = "reviewed-incident-schedule",
            int maxActive = 2, long firstOnsetTick = 1200, long minQuietTicks = 1200, long maxQuietTicks = 3600)
        {
            Validate(catalog);
            var choices = catalog.reviewedVariants.Select(v => new IncidentChoice
            {
                Id = v.choiceId,
                ResourceKey = v.resourceKey,
                Kind = (FoundationIncidentKind)v.kindOrdinal
            }).ToArray();

            return new IncidentSchedule
            {
                ProfileId = profileId,
                MaximumActive = maxActive,
                FirstOnsetTick = firstOnsetTick,
                MinimumQuietTicks = minQuietTicks,
                MaximumQuietTicks = maxQuietTicks,
                Choices = choices
            };
        }
    }

    [TestFixture]
    public sealed class Fmp09aIncidentCatalogTests
    {
        private static string ResolveCatalogPath()
        {
            const string relPath = "foundation/scenarios/reviewed-incident-catalog.json";
            if (File.Exists(relPath))
                return Path.GetFullPath(relPath);

            var package = PackageInfo.FindForAssembly(typeof(Fmp09aIncidentCatalogTests).Assembly);
            if (package != null)
            {
                var candidate = Path.GetFullPath(Path.Combine(package.resolvedPath, "..", "..", relPath));
                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException($"Could not locate {relPath}");
        }

        private static string ResolveSchemaPath()
        {
            const string relPath = "schemas/foundation-incident-catalog.schema.json";
            if (File.Exists(relPath))
                return Path.GetFullPath(relPath);

            var package = PackageInfo.FindForAssembly(typeof(Fmp09aIncidentCatalogTests).Assembly);
            if (package != null)
            {
                var candidate = Path.GetFullPath(Path.Combine(package.resolvedPath, "..", "..", relPath));
                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException($"Could not locate {relPath}");
        }

        private static IncidentCatalog LoadCanonicalCatalog()
        {
            var path = ResolveCatalogPath();
            var json = File.ReadAllText(path);
            var catalog = JsonUtility.FromJson<IncidentCatalog>(json);
            Assert.That(catalog, Is.Not.Null, "Failed to deserialize IncidentCatalog from JSON.");
            return catalog;
        }

        [Test]
        public void CanonicalCatalogLoadsAndPassesValidation()
        {
            var catalog = LoadCanonicalCatalog();
            Assert.That(catalog.schemaVersion, Is.EqualTo("1.0"));
            Assert.That(catalog.catalogId, Is.EqualTo("reviewed-incident-catalog"));
            Assert.That(catalog.catalogVersion, Is.EqualTo("1.0"));
            Assert.That(catalog.classification, Is.EqualTo("PUBLIC_REFERENCE_AND_SYNTHETIC_METADATA"));
            Assert.That(catalog.disclaimer, Is.EqualTo("KORAIL 검증 전 예시"));
            Assert.That(catalog.exaStatus, Is.EqualTo("blocked_403_1010"));
            Assert.That(catalog.emptyApplicabilityMeans, Is.EqualTo("unspecified_not_universal_permission"));

            Assert.That(catalog.sources.Length, Is.EqualTo(6));
            Assert.That(catalog.executionAllowlist.Length, Is.EqualTo(7));
            Assert.That(catalog.reviewedVariants.Length, Is.EqualTo(7));
            Assert.That(catalog.unreviewedVariants.Length, Is.EqualTo(5));

            Assert.DoesNotThrow(() => IncidentCatalogValidator.Validate(catalog));
        }

        [Test]
        public void CanonicalCatalogMatchesJsonSchemaIntegrity()
        {
            var schemaPath = ResolveSchemaPath();
            Assert.That(File.Exists(schemaPath), Is.True, "JSON schema file must exist: " + schemaPath);
            var schemaText = File.ReadAllText(schemaPath);
            Assert.That(schemaText, Does.Contain("foundation-incident-catalog.schema.json"));
            Assert.That(schemaText, Does.Contain("reviewedVariants"));
            Assert.That(schemaText, Does.Contain("unreviewedVariants"));
            Assert.That(schemaText, Does.Contain("executionAllowlist"));
            Assert.That(schemaText, Does.Contain("unspecified_not_universal_permission"));
            Assert.That(schemaText, Does.Contain("additionalProperties\": false"));

            var catalog = LoadCanonicalCatalog();
            var allowlist = new HashSet<string>(catalog.executionAllowlist);
            Assert.That(catalog.reviewedVariants.All(v => allowlist.Contains(v.choiceId)), Is.True);
            Assert.That(catalog.unreviewedVariants.All(v => !allowlist.Contains(v.choiceId)), Is.True);
        }

        [Test]
        public void CanonicalCatalogDrivesIncidentDirectorDeterministically()
        {
            var catalog = LoadCanonicalCatalog();
            var schedule = IncidentCatalogValidator.BuildSchedule(catalog, firstOnsetTick: 2, minQuietTicks: 1, maxQuietTicks: 3);
            Assert.That(schedule.Choices.Length, Is.EqualTo(7));

            var director = new IncidentDirector(schedule, 424242UL);
            Assert.That(director.AdvanceOne(), Is.Null, "No onset before tick 2");
            var ep1 = director.AdvanceOne();
            Assert.That(ep1, Is.Not.Null, "First onset at tick 2");
            Assert.That(schedule.Choices.Any(c => c.Id == ep1.ChoiceId), Is.True);

            // Advance further to accumulate active episodes bounded by MaximumActive (2)
            for (var i = 0; i < 20; i++)
            {
                director.AdvanceOne();
            }
            Assert.That(director.Active.Count, Is.EqualTo(2));

            var checkpoint = director.ExportCheckpoint();
            var restored = new IncidentDirector(schedule, 9999UL);
            restored.Restore(checkpoint);
            Assert.That(restored.ExportCheckpoint(), Is.EqualTo(checkpoint), "Restored director matches checkpoint state exactly.");

            // Mitigate and complete an episode
            var activeId = director.Active[0].Id;
            director.Mitigate(activeId);
            restored.Mitigate(activeId);
            director.Complete(activeId);
            restored.Complete(activeId);

            for (var i = 0; i < 10; i++)
            {
                director.AdvanceOne();
                restored.AdvanceOne();
            }
            Assert.That(director.ExportCheckpoint(), Is.EqualTo(restored.ExportCheckpoint()), "Post-recovery execution is deterministic.");
        }

        // --------------------------------------------------------------------------------
        // 8 Mandatory Negative Probes
        // --------------------------------------------------------------------------------

        [Test]
        public void Negative_RefuseUnreviewedVariantInExecutionAllowlist()
        {
            // Probe 1: Unreviewed variant choiceId added to execution allowlist
            var catalog = LoadCanonicalCatalog().Clone();
            var modifiedAllowlist = new List<string>(catalog.executionAllowlist) { "unreviewed-korail-public-report" };
            catalog.executionAllowlist = modifiedAllowlist.ToArray();

            var ex = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex.Message, Does.Contain("Unreviewed variant").Or.Contain("executionAllowlist"));

            // Probe 1b: Unreviewed variant marked as isExecutionCandidate == true
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.unreviewedVariants[0].isExecutionCandidate = true;
            Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
        }

        [Test]
        public void Negative_RefuseDuplicateChoiceOrIncidentId()
        {
            // Probe 2a: Duplicate choiceId
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[1].choiceId = catalog.reviewedVariants[0].choiceId;
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("Duplicate choiceId").IgnoreCase);

            // Probe 2b: Duplicate variant id
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[1].id = catalog2.reviewedVariants[0].id;
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("Duplicate variant id").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseConflationOfSRDataAsKORARILSOP()
        {
            // Probe 3: Conflating SR data as KORAIL SOP
            var catalog = LoadCanonicalCatalog().Clone();
            var srVariant = catalog.unreviewedVariants.First(v => v.sourceId == "SR-LARGE-ACCIDENT-20240226");
            srVariant.applicability.@operator = "KORAIL";

            var ex = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex.Message, Does.Contain("SR").And.Contains("KORAIL"));
        }

        [Test]
        public void Negative_RefuseUniversalPermissionForEmptyApplicability()
        {
            // Probe 4a: emptyApplicabilityMeans changed from unspecified_not_universal_permission
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.emptyApplicabilityMeans = "universal_permission";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("emptyApplicabilityMeans"));

            // Probe 4b: Empty applicability variant attempting universal candidate promotion
            var catalog2 = LoadCanonicalCatalog().Clone();
            var emptyVariant = catalog2.unreviewedVariants.First(v =>
                v.applicability.stations.Length == 0 &&
                v.applicability.lines.Length == 0 &&
                v.applicability.roles.Length == 0);
            emptyVariant.isExecutionCandidate = true;
            emptyVariant.reviewStatus = "reviewed";
            emptyVariant.procedureAcceptance = "provisional_synthetic_reviewed";
            catalog2.unreviewedVariants = catalog2.unreviewedVariants.Where(v => v.choiceId != emptyVariant.choiceId).ToArray();
            catalog2.reviewedVariants = catalog2.reviewedVariants.Concat(new[] { emptyVariant }).ToArray();
            var allowlist = new List<string>(catalog2.executionAllowlist) { emptyVariant.choiceId };
            catalog2.executionAllowlist = allowlist.ToArray();

            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("universal").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseUnknownOrOrphanRegionResourceKey()
        {
            // Probe 5a: Unknown regionId
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].regionId = "unknown_phantom_corridor";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("regionId").IgnoreCase);

            // Probe 5b: Unknown resourceKey
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[0].resourceKey = "orphan_track_segment";
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("resourceKey").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseMissingOrUnregisteredSourceId()
        {
            // Probe 6a: Unregistered sourceId
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].sourceId = "UNREGISTERED-UNKNOWN-SOURCE";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("sourceId").IgnoreCase);

            // Probe 6b: Empty sourceId
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[0].sourceId = "";
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("sourceId").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseUndefinedIncidentKindEnum()
        {
            // Probe 7a: Undefined kind enum name
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].kind = "UndefinedIncidentKind";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("FoundationIncidentKind").IgnoreCase);

            // Probe 7b: Out of range kindOrdinal
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[0].kindOrdinal = 99;
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("kindOrdinal").IgnoreCase);

            // Probe 7c: Mismatched kind and kindOrdinal
            var catalog3 = LoadCanonicalCatalog().Clone();
            catalog3.reviewedVariants[0].kindOrdinal = 3; // kind is Fire (0), ordinal mismatch
            var ex3 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog3));
            Assert.That(ex3.Message, Does.Contain("kindOrdinal").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseHallucinatedOfficialAuthorityForSyntheticVariant()
        {
            // Probe 8a: Synthetic variant claiming official operator authority
            var catalog = LoadCanonicalCatalog().Clone();
            var syntheticVariant = catalog.reviewedVariants.First(v => v.sourceId == "CG-SYNTHETIC-FUNCTIONAL-INCIDENTS-V1");
            syntheticVariant.applicability.@operator = "KORAIL";

            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("Synthetic variant").And.Contains("operator authority"));

            // Probe 8b: Synthetic variant claiming official accepted procedure
            var catalog2 = LoadCanonicalCatalog().Clone();
            var syntheticVariant2 = catalog2.reviewedVariants.First(v => v.sourceId == "CG-SYNTHETIC-FUNCTIONAL-INCIDENTS-V1");
            syntheticVariant2.procedureAcceptance = "accepted_railway_sop";

            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("procedureAcceptance"));
        }
    }
}
