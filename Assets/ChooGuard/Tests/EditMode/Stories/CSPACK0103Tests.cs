#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Content;
using ChooGuard.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSPACK0103Tests
    {
        private static byte[] ReadFixture() => File.ReadAllBytes(Path.Combine(
            Directory.GetParent(global::UnityEngine.Application.dataPath).FullName, "content", "fixtures", "two-agency.json"));

        private static TwoAgencyFixture.Loaded LoadFixture() => TwoAgencyFixture.Load(ReadFixture());

        [Test]
        public void RealFixtureUsesContentValidatorAndPreservesIndependentLockedRevisions()
        {
            var loaded = LoadFixture();
            Assert.That(loaded.Content.IsValid, Is.True);
            Assert.That(loaded.Content.Errors, Is.Empty);
            Assert.That(loaded.Content.Site.Id, Is.EqualTo(loaded.Data.operationalPlan.siteRef.id));
            Assert.That(loaded.Content.Scenario.Id, Is.EqualTo(loaded.Data.operationalPlan.scenarioRef.id));
            Assert.That(new[] { loaded.Content.Site.Revision, loaded.Content.Scenario.Revision,
                loaded.Data.operationalPlan.revision }, Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(loaded.Content.Site.Entities, Is.Empty);
            Assert.That(loaded.Content.Site.SourceRefs, Is.Empty);
            Assert.That(loaded.Content.Site.Qualification, Is.Empty);
        }

        [Test]
        public void SameDisplayNamesDoNotMergeAgenciesOrTheirSpaces()
        {
            var loaded = LoadFixture();
            var plan = loaded.Data.operationalPlan;
            Assert.That(plan.agencies.Select(a => a.displayName).Distinct().Count(), Is.EqualTo(1));
            Assert.That(plan.agencies.Select(a => a.id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(plan.agencies.Select(a => a.role), Is.EqualTo(new[] { "RAIL", "COOPERATING" }));
            Assert.That(plan.spaces.Select(s => s.agencyId), Is.EqualTo(plan.agencies.Select(a => a.id)));
            Assert.That(plan.spaces.Select(s => s.regionId), Is.EqualTo(loaded.Content.Site.Regions.Select(r => r.Id)));
            Assert.That(plan.tasks.Select(t => t.spaceId), Is.EqualTo(plan.spaces.Select(s => s.id)));
        }

        [Test]
        public void HalfOpenIntervalsProduceNormalZeroAndConflictOneWithExpectedSegment()
        {
            var plan = LoadFixture().Data.operationalPlan;
            var normal = plan.cases.Single(c => c.id == "case-normal");
            var conflict = plan.cases.Single(c => c.id == "case-resource-conflict");
            Assert.That(TwoAgencyFixture.PairOverlaps(normal), Is.Empty);
            var overlap = TwoAgencyFixture.PairOverlaps(conflict).Single();
            Assert.That(overlap.StartUs, Is.EqualTo(1500));
            Assert.That(overlap.EndUs, Is.EqualTo(2000));
            Assert.That(overlap.ResourceId, Is.EqualTo(plan.resources.Single().id));
            Assert.That(normal.assignments[0].endUs, Is.EqualTo(normal.assignments[1].startUs));
        }

        [Test]
        public void WholeFixtureHashIsSharedByCasesAndChangesEvenForTrailingWhitespace()
        {
            var first = LoadFixture();
            var second = LoadFixture();
            Assert.That(first.HashForCase("case-normal"), Is.EqualTo(second.HashForCase("case-resource-conflict")));
            Assert.That(first.InputHash, Has.Length.EqualTo(64));
            Assert.That(JsonUtility.ToJson(first.Data), Is.EqualTo(JsonUtility.ToJson(second.Data)));
            var bytes = ReadFixture();
            using (var sha = SHA256.Create())
                Assert.That(first.InputHash, Is.EqualTo(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant()));
            var changed = TwoAgencyFixture.Load(bytes.Concat(new byte[] { 32, 10 }).ToArray());
            Assert.That(changed.InputHash, Is.Not.EqualTo(first.InputHash));
            Assert.That(JsonUtility.ToJson(changed.Data), Is.EqualTo(JsonUtility.ToJson(first.Data)));
        }

        [Test]
        public void FalseFieldApprovalInJsonIsRejectedAtLoadBoundary()
        {
            var json = Encoding.UTF8.GetString(ReadFixture());
            Assert.That(json, Does.Contain("NOT_FIELD_APPROVED"));
            var changed = json.Replace("NOT_FIELD_APPROVED", "FIELD_APPROVED");
            var error = Assert.Throws<InvalidDataException>(() => TwoAgencyFixture.Load(Encoding.UTF8.GetBytes(changed)));
            Assert.That(error.Message, Does.Contain("provenance"));
        }

        [TestCase(true, ContentErrorCode.RevisionMismatch, "/site/revision")]
        [TestCase(false, ContentErrorCode.MissingPortalEndpoint, "/portals/0/toRegionId")]
        public void ProjectedContentFailuresPreserveExactProductionCodeAndPointer(bool scenario,
            ContentErrorCode code, string pointer)
        {
            var data = LoadFixture().Data;
            if (scenario) data.scenarioSpec.site.revision++;
            else data.siteBundle.portals[0].toRegionId = "missing-region";
            var result = TwoAgencyFixture.ValidateContent(data);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Site, Is.Null);
            Assert.That(result.Scenario, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            Assert.That(result.Errors[0].Document, Is.EqualTo(scenario ? ContentDocumentKind.Scenario : ContentDocumentKind.Site));
            Assert.That(result.Errors[0].Code, Is.EqualTo(code));
            Assert.That(result.Errors[0].JsonPointer, Is.EqualTo(pointer));
            var error = Assert.Throws<InvalidDataException>(() => TwoAgencyFixture.Load(Encode(data)));
            Assert.That(error.Message, Does.Contain(code + ":" + pointer));
        }

        [TestCase("fixture-marker")][TestCase("data-class")][TestCase("geometry")]
        [TestCase("hash-algorithm")][TestCase("hash-scope")]
        [TestCase("timing-missing")][TestCase("timing-wrong")]
        [TestCase("capacity-zero")][TestCase("capacity-negative")][TestCase("capacity-two")]
        [TestCase("duplicate-task")][TestCase("duplicate-resource")][TestCase("duplicate-report")]
        [TestCase("duplicate-assignment")][TestCase("invalid-id")][TestCase("blank-name")]
        [TestCase("agency-ref")][TestCase("space-ref")][TestCase("task-ref")]
        [TestCase("resource-ref")][TestCase("region-ref")][TestCase("required-resource-ref")]
        [TestCase("participant-ref")][TestCase("task-space-agency")][TestCase("agency-role")]
        [TestCase("share-mode")][TestCase("site-revision")][TestCase("scenario-revision")]
        [TestCase("site-id")][TestCase("scenario-id")][TestCase("negative-plan-revision")]
        [TestCase("case-report-ref")][TestCase("report-case-ref")][TestCase("report-kind")]
        [TestCase("report-outcome")][TestCase("interval-zero")][TestCase("interval-reversed")]
        [TestCase("interval-negative")][TestCase("null-provenance")][TestCase("null-integrity")]
        [TestCase("null-site")][TestCase("null-scenario")][TestCase("null-plan")]
        [TestCase("null-site-ref")][TestCase("null-scenario-ref")]
        [TestCase("null-tasks")][TestCase("null-assignments")][TestCase("null-required-resources")]
        public void MeaningfulFixtureCorruptionIsExplicitlyRejected(string mutation)
        {
            // 매번 실제 파일에서 독립 투영을 만들어 다른 시험을 오염시키지 않는다.
            var data = LoadFixture().Data;
            var p = data.operationalPlan;
            var a = p.cases[0].assignments[0];
            switch (mutation)
            {
                case "fixture-marker": data.fixtureKind = null; break;
                case "data-class": data.provenance.dataClass = "FIELD"; break;
                case "geometry": data.provenance.geometry = "SURVEYED"; break;
                case "hash-algorithm": data.integrity.inputHashAlgorithm = "MD5"; break;
                case "hash-scope": data.integrity.hashScope = "CASE_ONLY"; break;
                case "timing-missing": a.timingBasis = null; break;
                case "timing-wrong": a.timingBasis = "OBSERVED"; break;
                case "capacity-zero": p.resources[0].capacityUnits = 0; break;
                case "capacity-negative": p.resources[0].capacityUnits = -1; break;
                case "capacity-two": p.resources[0].capacityUnits = 2; break;
                case "duplicate-task": p.tasks[1].id = p.tasks[0].id; break;
                case "duplicate-resource": p.resources[0].id = p.tasks[0].id; break;
                case "duplicate-report": p.reports[1].id = p.reports[0].id; break;
                case "duplicate-assignment": p.cases[1].assignments[0].id = a.id; break;
                case "invalid-id": p.tasks[0].id = "invalid id"; break;
                case "blank-name": p.agencies[0].displayName = " "; break;
                case "agency-ref": p.spaces[0].agencyId = "missing-agency"; break;
                case "space-ref": p.tasks[0].spaceId = "missing-space"; break;
                case "task-ref": a.taskId = "missing-task"; break;
                case "resource-ref": a.resourceId = "missing-resource"; break;
                case "region-ref": p.spaces[0].regionId = "missing-region"; break;
                case "required-resource-ref": p.tasks[0].requiredResourceIds[0] = "missing-resource"; break;
                case "participant-ref": p.resources[0].participantAgencyIds[0] = "missing-agency"; break;
                case "task-space-agency": p.tasks[0].agencyId = p.agencies[1].id; break;
                case "agency-role": p.agencies[1].role = "RAIL"; break;
                case "share-mode": p.resources[0].shareMode = "PRIVATE"; break;
                case "site-revision": p.siteRef.revision++; break;
                case "scenario-revision": p.scenarioRef.revision++; break;
                case "site-id": p.siteRef.id = "missing-site"; break;
                case "scenario-id": p.scenarioRef.id = "missing-scenario"; break;
                case "negative-plan-revision": p.revision = -1; break;
                case "case-report-ref": p.cases[0].reportId = p.reports[1].id; break;
                case "report-case-ref": p.reports[0].caseId = p.cases[1].id; break;
                case "report-kind": p.reports[0].kind = "RUNTIME_RECEIPT"; break;
                case "report-outcome": p.reports[1].expectedOutcome = "NO_RESOURCE_CONFLICT"; break;
                case "interval-zero": a.endUs = a.startUs; break;
                case "interval-reversed": a.endUs = a.startUs - 1; break;
                case "interval-negative": a.startUs = -1; break;
                case "null-provenance": data.provenance = null; break;
                case "null-integrity": data.integrity = null; break;
                case "null-site": data.siteBundle = null; break;
                case "null-scenario": data.scenarioSpec = null; break;
                case "null-plan": data.operationalPlan = null; break;
                case "null-site-ref": p.siteRef = null; break;
                case "null-scenario-ref": p.scenarioRef = null; break;
                case "null-tasks": p.tasks = null; break;
                case "null-assignments": p.cases[0].assignments = null; break;
                case "null-required-resources": p.tasks[0].requiredResourceIds = null; break;
                default: Assert.Fail("Unknown mutation: " + mutation); break;
            }
            Assert.Throws<InvalidDataException>(() => TwoAgencyFixture.Load(Encode(data)), mutation);
        }

        [Test]
        public void NullEnvelopeIsGuardFailureNotNullReferenceException()
        {
            Assert.Throws<InvalidDataException>(() => TwoAgencyFixture.Guard(null));
            Assert.Throws<InvalidDataException>(() => TwoAgencyFixture.Load(null));
        }

        private static byte[] Encode(TwoAgencyFixture.Envelope data) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
    }

    // 버전 관리된 신뢰 fixture의 시험 전용 JsonUtility 투영이다. 외부 wire 검증기가 아니다.
    // unknown field/duplicate key/누락 숫자의 엄격한 원문 검증은 제공하지 않는다.
    // OPS 시험에서 재사용할 수 있지만 production loader나 현장 승인/실행 receipt가 아니다.
    internal static class TwoAgencyFixture
    {
        internal const string Synthetic = "SYNTHETIC_FIXTURE";

        internal sealed class Loaded
        {
            internal readonly Envelope Data;
            internal readonly ContentValidationResult Content;
            internal readonly string InputHash;
            internal Loaded(Envelope data, ContentValidationResult content, string hash)
            { Data = data; Content = content; InputHash = hash; }
            internal string HashForCase(string id)
            {
                Require(Data.operationalPlan.cases.Any(c => c.id == id), "case id");
                return InputHash;
            }
        }

        internal static Loaded Load(byte[] bytes)
        {
            Require(bytes != null && bytes.Length > 0, "fixture bytes");
            // 해시와 투영은 동일한 입력 스냅샷을 사용하며 JSON에 해시를 self-embed하지 않는다.
            var snapshot = (byte[])bytes.Clone();
            Envelope data;
            try { data = JsonUtility.FromJson<Envelope>(new UTF8Encoding(false, true).GetString(snapshot)); }
            catch (ArgumentException ex) { throw new InvalidDataException("fixture projection", ex); }
            var content = Guard(data);
            using (var sha = SHA256.Create())
                return new Loaded(data, content, BitConverter.ToString(sha.ComputeHash(snapshot)).Replace("-", "").ToLowerInvariant());
        }

        internal static ContentValidationResult ValidateContent(Envelope data)
        {
            Require(data != null && data.siteBundle != null && data.scenarioSpec != null, "site/scenario");
            return ContentValidator.Validate(JsonUtility.ToJson(data.siteBundle), JsonUtility.ToJson(data.scenarioSpec),
                Array.Empty<ContentRevisionRef>());
        }

        internal static ContentValidationResult Guard(Envelope d)
        {
            Require(d != null, "envelope");
            Require(d.fixtureKind == Synthetic, "fixtureKind");
            Require(d.provenance != null && d.provenance.dataClass == Synthetic &&
                d.provenance.approval == "NOT_FIELD_APPROVED" && d.provenance.geometry == "LOGICAL_ONLY", "provenance");
            Require(d.integrity != null && d.integrity.inputHashAlgorithm == "SHA-256" &&
                d.integrity.hashScope == "WHOLE_FIXTURE_BYTES", "integrity");
            var content = ValidateContent(d);
            Require(content.IsValid, "content: " + string.Join(";", content.Errors.Select(e => e.Code + ":" + e.JsonPointer)));
            var p = d.operationalPlan;
            Require(p != null, "operationalPlan");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            AddId(ids, p.id);
            Require(p.revision >= 0, "plan revision");
            LockedRef(p.siteRef, content.Site.Id, content.Site.Revision, "siteRef");
            LockedRef(p.scenarioRef, content.Scenario.Id, content.Scenario.Revision, "scenarioRef");
            var agencies = Index(p.agencies, x => x.id, ids, 2, "agencies");
            Require(new HashSet<string>(p.agencies.Select(x => x.role)).SetEquals(new[] { "RAIL", "COOPERATING" }), "agency roles");
            foreach (var agency in p.agencies) Require(!string.IsNullOrWhiteSpace(agency.displayName), "agency displayName");
            var regions = new HashSet<string>(content.Site.Regions.Select(r => r.Id), StringComparer.Ordinal);
            var spaces = Index(p.spaces, x => x.id, ids, 2, "spaces");
            foreach (var space in p.spaces)
                Require(Has(agencies, space.agencyId) && space.regionId != null && regions.Contains(space.regionId), "space references");
            Require(p.spaces.Select(x => x.agencyId).Distinct().Count() == 2 &&
                p.spaces.Select(x => x.regionId).Distinct().Count() == 2, "two distinct spaces/regions/agencies");
            var resources = Index(p.resources, x => x.id, ids, 1, "resources");
            foreach (var resource in p.resources)
            {
                Require(!string.IsNullOrWhiteSpace(resource.displayName), "resource displayName");
                Require(resource.shareMode == "SHARED" && resource.capacityUnits == 1, "shared capacityUnits=1");
                Require(resource.participantAgencyIds != null && resource.participantAgencyIds.Length == 2 &&
                    new HashSet<string>(resource.participantAgencyIds, StringComparer.Ordinal).SetEquals(agencies.Keys), "resource participants");
            }
            var tasks = Index(p.tasks, x => x.id, ids, 2, "tasks");
            foreach (var task in p.tasks)
            {
                Require(Has(agencies, task.agencyId) && Has(spaces, task.spaceId), "task references");
                Require(spaces[task.spaceId].agencyId == task.agencyId, "task space agency");
                Require(task.requiredResourceIds != null && task.requiredResourceIds.Length == 1 &&
                    Has(resources, task.requiredResourceIds[0]), "task requiredResourceIds");
            }
            Require(p.tasks.Select(t => t.agencyId).Distinct().Count() == 2, "two agency tasks");
            var cases = Index(p.cases, x => x.id, ids, 2, "cases");
            var reports = Index(p.reports, x => x.id, ids, 2, "reports");
            foreach (var c in p.cases)
            {
                Index(c.assignments, x => x.id, ids, 2, "assignments");
                foreach (var assignment in c.assignments)
                {
                    Require(Has(tasks, assignment.taskId) && Has(resources, assignment.resourceId), "assignment references");
                    var task = tasks[assignment.taskId];
                    var resource = resources[assignment.resourceId];
                    Require(task.requiredResourceIds.Contains(resource.id) && resource.participantAgencyIds.Contains(task.agencyId), "assignment participation");
                    Require(assignment.startUs >= 0 && assignment.startUs < assignment.endUs, "positive half-open interval");
                    Require(assignment.timingBasis == Synthetic, "timingBasis");
                }
                Require(new HashSet<string>(c.assignments.Select(a => a.taskId)).SetEquals(tasks.Keys), "case task coverage");
                Require(Has(reports, c.reportId), "case report reference");
                var report = reports[c.reportId];
                Require(report.caseId == c.id && report.kind == "SYNTHETIC_FIXTURE_EXPECTATION", "report reciprocal/kind");
                // 실행 성공을 주장하지 않는 fixture 관계 oracle: 보고서 문자열과 독립적으로 쌍별 겹침을 계산한다.
                var expected = PairOverlaps(c).Count == 0 ? "NO_RESOURCE_CONFLICT" : "RESOURCE_CONFLICT";
                Require(report.expectedOutcome == expected, "report outcome");
            }
            foreach (var report in p.reports)
                Require(Has(cases, report.caseId) && cases[report.caseId].reportId == report.id, "report case reciprocal reference");
            return content;
        }

        internal sealed class Overlap
        {
            internal readonly string ResourceId;
            internal readonly long StartUs, EndUs;
            internal Overlap(string resourceId, long start, long end) { ResourceId = resourceId; StartUs = start; EndUs = end; }
        }

        internal static List<Overlap> PairOverlaps(Case c)
        {
            Require(c != null && c.assignments != null && c.assignments.All(a => a != null), "overlap assignments");
            var overlaps = new List<Overlap>();
            for (var i = 0; i < c.assignments.Length; i++)
                for (var j = i + 1; j < c.assignments.Length; j++)
                {
                    var a = c.assignments[i];
                    var b = c.assignments[j];
                    var start = Math.Max(a.startUs, b.startUs);
                    var end = Math.Min(a.endUs, b.endUs);
                    if (a.resourceId == b.resourceId && start < end) overlaps.Add(new Overlap(a.resourceId, start, end));
                }
            return overlaps;
        }

        private static void LockedRef(RevisionRef reference, string id, long revision, string name)
        { Require(reference != null && reference.revision >= 0 && reference.id == id && reference.revision == revision, name); }

        private static bool Has<T>(Dictionary<string, T> index, string id) => id != null && index.ContainsKey(id);

        private static Dictionary<string, T> Index<T>(T[] values, Func<T, string> id,
            HashSet<string> allIds, int count, string name) where T : class
        {
            Require(values != null && values.Length == count, name);
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                Require(value != null, name + " entry");
                var key = id(value);
                AddId(allIds, key);
                result.Add(key, value);
            }
            return result;
        }

        private static void AddId(HashSet<string> ids, string id)
        {
            try { _ = new StableId(id); }
            catch (ArgumentException ex) { throw new InvalidDataException("invalid fixture ID", ex); }
            Require(ids.Add(id), "duplicate fixture ID: " + id);
        }

        private static void Require(bool valid, string message)
        { if (!valid) throw new InvalidDataException(message); }

        // 빈 배열도 유지하여 실제 ContentValidator에 전달되는 site/scenario 모양을 보존한다.
        [Serializable] internal sealed class Envelope
        {
            public string fixtureKind;
            public Provenance provenance;
            public Integrity integrity;
            public Site siteBundle;
            public Scenario scenarioSpec;
            public Plan operationalPlan;
        }
        [Serializable] internal sealed class Provenance { public string dataClass, approval, geometry; }
        [Serializable] internal sealed class Integrity { public string inputHashAlgorithm, hashScope; }
        [Serializable] internal sealed class RevisionRef { public string id; public long revision; }
        [Serializable] internal sealed class Site
        {
            public string id; public long revision;
            public Frame[] frames; public Region[] regions; public Portal[] portals;
            public Entity[] entities; public RevisionRef[] sourceRefs; public Qualification[] qualification;
        }
        [Serializable] internal sealed class Frame { public string id; }
        [Serializable] internal sealed class Region { public string id, frameId; }
        [Serializable] internal sealed class Portal { public string id, fromRegionId, toRegionId; }
        [Serializable] internal sealed class Entity { public string id, frameId, regionId; }
        [Serializable] internal sealed class Qualification { public string id; public string[] sourceRefIds; }
        [Serializable] internal sealed class Scenario { public string id; public long revision; public RevisionRef site; }
        [Serializable] internal sealed class Plan
        {
            public string id; public long revision;
            public RevisionRef siteRef, scenarioRef;
            public Agency[] agencies; public Space[] spaces; public Resource[] resources;
            public Task[] tasks; public Case[] cases; public Report[] reports;
        }
        [Serializable] internal sealed class Agency { public string id, displayName, role; }
        [Serializable] internal sealed class Space { public string id, regionId, agencyId; }
        [Serializable] internal sealed class Resource
        {
            public string id, displayName, shareMode; public int capacityUnits; public string[] participantAgencyIds;
        }
        [Serializable] internal sealed class Task { public string id, agencyId, spaceId; public string[] requiredResourceIds; }
        [Serializable] internal sealed class Case { public string id, reportId; public Assignment[] assignments; }
        [Serializable] internal sealed class Assignment { public string id, taskId, resourceId, timingBasis; public long startUs, endUs; }
        [Serializable] internal sealed class Report { public string id, caseId, kind, expectedOutcome; }
    }
}
#endif
