#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Content;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSPACK0102Tests
    {
        // 전부 가상의 참조 시험 자료이며 실제 역 모델이나 승인 자료가 아니다.
        private const string SiteJson = "{\"id\":\"site-1\",\"revision\":2,\"frames\":[{\"id\":\"frame-1\"}],\"regions\":[{\"id\":\"region-1\",\"frameId\":\"frame-1\"},{\"id\":\"region-2\",\"frameId\":\"frame-1\"}],\"portals\":[{\"id\":\"portal-1\",\"fromRegionId\":\"region-1\",\"toRegionId\":\"region-2\"}],\"entities\":[{\"id\":\"entity-1\",\"frameId\":\"frame-1\",\"regionId\":\"region-1\"}],\"sourceRefs\":[{\"id\":\"source-1\",\"revision\":4}],\"qualification\":[{\"id\":\"declared-evidence-1\",\"sourceRefIds\":[\"source-1\"]}]}";
        private const string ScenarioJson = "{\"id\":\"scenario-1\",\"revision\":9,\"site\":{\"id\":\"site-1\",\"revision\":2}}";
        private const string EmptySite = "{\"id\":\"site-1\",\"revision\":2,\"frames\":[],\"regions\":[],\"portals\":[],\"entities\":[],\"sourceRefs\":[],\"qualification\":[]}";

        private static ContentRevisionRef[] Catalog() => new[] { new ContentRevisionRef("source-1", 4) };
        private static ContentValidationResult Validate(string site = SiteJson, string scenario = ScenarioJson) => ContentValidator.Validate(site, scenario, Catalog());

        private static void Rejected(ContentValidationResult result)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Site, Is.Null);
            Assert.That(result.Scenario, Is.Null);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Errors.All(e => !string.IsNullOrWhiteSpace(e.Message)), Is.True);
        }

        private static void Single(ContentValidationResult result, ContentDocumentKind document, ContentErrorCode code, string pointer = null)
        {
            Rejected(result);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            Assert.That(result.Errors[0].Document, Is.EqualTo(document));
            Assert.That(result.Errors[0].Code, Is.EqualTo(code));
            if (pointer != null) Assert.That(result.Errors[0].JsonPointer, Is.EqualTo(pointer));
        }

        private static string Key(ContentValidationError e) => e.Document + ":" + e.Code + ":" + e.JsonPointer;

        [Test]
        public void ValidSkeletonPreservesValuesAndDoesNotRequireSiteOrScenarioInCatalog()
        {
            var r = Validate();
            Assert.That(r.IsValid, Is.True);
            Assert.That(r.Errors, Is.Empty);
            Assert.That(r.Site.Id, Is.EqualTo("site-1"));
            Assert.That(r.Site.Revision, Is.EqualTo(2));
            Assert.That(r.Scenario.Revision, Is.EqualTo(9));
            Assert.That(r.Scenario.Site.Id, Is.EqualTo(r.Site.Id));
            Assert.That(r.Scenario.Site.Revision, Is.EqualTo(r.Site.Revision));
            Assert.That(r.Site.Regions.Select(x => x.Id), Is.EqualTo(new[] { "region-1", "region-2" }));
            Assert.That(r.Site.Portals[0].FromRegionId, Is.EqualTo("region-1"));
            Assert.That(r.Site.Portals[0].ToRegionId, Is.EqualTo("region-2"));
            Assert.That(r.Site.Entities[0].FrameId, Is.EqualTo("frame-1"));
            Assert.That(r.Site.Entities[0].RegionId, Is.EqualTo("region-1"));
            Assert.That(r.Site.SourceRefs[0].Revision, Is.EqualTo(4));
            Assert.That(r.Site.Qualification[0].SourceRefIds, Is.EqualTo(new[] { "source-1" }));
        }

        [Test]
        public void EmptyTopologyAndEvidenceArraysAreValidReferenceSkeleton()
        {
            var r = ContentValidator.Validate(EmptySite, ScenarioJson, new ContentRevisionRef[0]);
            Assert.That(r.IsValid, Is.True);
            Assert.That(r.Site.Frames, Is.Empty);
            Assert.That(r.Site.Qualification, Is.Empty);
        }

        [TestCase(null)][TestCase("")][TestCase(" \r\n\t")]
        public void EmptyDocumentsHaveDocumentSpecificError(string text)
        {
            Single(Validate(text), ContentDocumentKind.Site, ContentErrorCode.EmptyInput, "");
            Single(Validate(SiteJson, text), ContentDocumentKind.Scenario, ContentErrorCode.EmptyInput, "");
        }

        [TestCase("{\"id\":\"a\",\"id\":\"b\"}", "/id")]
        [TestCase("{\"id\":\"a\",\"\\u0069d\":\"b\"}", "/id")]
        [TestCase("{\"frames\":[{\"id\":\"a\",\"id\":\"b\"}]}", "/frames/0/id")]
        [TestCase("{\"frames\":[{\"id\":\"a\",\"\\u0069d\":\"b\"}]}", "/frames/0/id")]
        public void DecodedDuplicateKeysAreRejectedAtEveryDepth(string json, string pointer)
            => Single(Validate(json), ContentDocumentKind.Site, ContentErrorCode.DuplicateProperty, pointer);

        [Test]
        public void NestedScenarioDuplicateIsNotSilentlyOverwritten()
            => Single(Validate(SiteJson, ScenarioJson.Replace("\"revision\":2", "\"revision\":2,\"\\u0072evision\":2")), ContentDocumentKind.Scenario, ContentErrorCode.DuplicateProperty, "/site/revision");

        [TestCase("\"a~/b\":0,", "/a~0~1b")]
        [TestCase("\"\\u0078\":0,", "/x")]
        public void UnknownRootKeyUsesDecodedRfc6901Pointer(string field, string pointer)
            => Single(Validate("{" + field + SiteJson.Substring(1)), ContentDocumentKind.Site, ContentErrorCode.UnknownProperty, pointer);

        [Test]
        public void NestedShapesAreClosed()
        {
            Single(Validate(SiteJson.Replace("\"id\":\"frame-1\"", "\"id\":\"frame-1\",\"a~/b\":true")), ContentDocumentKind.Site, ContentErrorCode.UnknownProperty, "/frames/0/a~0~1b");
            Single(Validate(SiteJson, ScenarioJson.Replace("\"revision\":2", "\"revision\":2,\"extra\":0")), ContentDocumentKind.Scenario, ContentErrorCode.UnknownProperty, "/site/extra");
        }

        [TestCase("\"id\":\"site-1\",", "", ContentErrorCode.MissingProperty, "/id")]
        [TestCase("\"revision\":2,", "", ContentErrorCode.MissingProperty, "/revision")]
        [TestCase("\"id\":\"site-1\"", "\"id\":null", ContentErrorCode.WrongType, "/id")]
        [TestCase("\"revision\":2", "\"revision\":\"2\"", ContentErrorCode.WrongType, "/revision")]
        [TestCase("\"frames\":[]", "\"frames\":null", ContentErrorCode.WrongType, "/frames")]
        [TestCase("\"frames\":[]", "\"frames\":{}", ContentErrorCode.WrongType, "/frames")]
        [TestCase("\"frames\":[]", "\"frames\":[null]", ContentErrorCode.WrongType, "/frames/0")]
        [TestCase("\"frames\":[]", "\"frames\":[{}]", ContentErrorCode.MissingProperty, "/frames/0/id")]
        public void RequiredPropertiesAndTypesAreStrict(string before, string after, ContentErrorCode code, string pointer)
            => Single(Validate(EmptySite.Replace(before, after)), ContentDocumentKind.Site, code, pointer);

        [TestCase("")][TestCase("기관")][TestCase("-a")][TestCase("a b")][TestCase("a\\n")][TestCase("\\uD83D\\uDE00")]
        public void InvalidIdsIncludingValidSurrogatePairAreShapeErrors(string id)
            => Single(Validate(SiteJson.Replace("site-1", id)), ContentDocumentKind.Site, ContentErrorCode.InvalidId, "/id");

        [Test]
        public void IdLengthAndAlphabetAreBounded()
        {
            Single(Validate(SiteJson.Replace("site-1", new string('a', 129))), ContentDocumentKind.Site, ContentErrorCode.InvalidId, "/id");
            var id = "A0._:-" + new string('a', 122);
            Assert.That(Validate(SiteJson.Replace("site-1", id), ScenarioJson.Replace("site-1", id)).IsValid, Is.True);
        }

        [TestCase("-0")][TestCase("-1")][TestCase("1.0")][TestCase("1e0")][TestCase("1E+0")]
        public void RevisionRequiresCanonicalUnsignedIntegerLexeme(string token)
            => Single(Validate(SiteJson.Replace("\"revision\":2", "\"revision\":" + token)), ContentDocumentKind.Site, ContentErrorCode.InvalidRevision, "/revision");

        [TestCase("9223372036854775808")][TestCase("1e999")]
        public void RevisionOverflowIsNotRoundedOrCoerced(string token)
            => Single(Validate(SiteJson.Replace("\"revision\":2", "\"revision\":" + token)), ContentDocumentKind.Site, ContentErrorCode.NumberOutOfRange, "/revision");

        [TestCase("0", "0")]
        [TestCase("9223372036854775807", "9223372036854775807")]
        [TestCase("0", "9223372036854775807")]
        [TestCase("9223372036854775807", "0")]
        [TestCase("2", "9")]
        public void RevisionInclusiveBoundsAndIndependentScenarioRevisionAreValid(string siteRevision, string scenarioRevision)
        {
            var scenario = "{\"id\":\"scenario-1\",\"revision\":" + scenarioRevision
                + ",\"site\":{\"id\":\"site-1\",\"revision\":" + siteRevision + "}}";
            var r = Validate(SiteJson.Replace("\"revision\":2", "\"revision\":" + siteRevision), scenario);
            Assert.That(r.IsValid, Is.True);
            Assert.That(r.Site.Revision, Is.EqualTo(long.Parse(siteRevision)));
            Assert.That(r.Scenario.Revision, Is.EqualTo(long.Parse(scenarioRevision)));
            Assert.That(r.Scenario.Site.Revision, Is.EqualTo(long.Parse(siteRevision)));
        }

        [TestCase("00")][TestCase("01")][TestCase("+1")][TestCase("NaN")][TestCase("Infinity")][TestCase("1.")][TestCase("1e")][TestCase("tru")]
        public void InvalidNumberAndLiteralGrammarIsInvalidJson(string token)
            => Single(Validate(SiteJson.Replace("\"revision\":2", "\"revision\":" + token)), ContentDocumentKind.Site, ContentErrorCode.InvalidJson);

        [TestCase("\uFEFF{}")][TestCase("/*comment*/{}")][TestCase("{\"id\":\"a\",}")][TestCase("[1,]")][TestCase("{\"id\":}")][TestCase("\u00a0{}")]
        public void InvalidJsonGrammarIsRejected(string json)
            => Single(Validate(json), ContentDocumentKind.Site, ContentErrorCode.InvalidJson);

        [TestCase("{}")][TestCase("true")][TestCase("x")]
        public void TrailingDataIsRejected(string suffix)
            => Single(Validate(SiteJson + suffix), ContentDocumentKind.Site, ContentErrorCode.TrailingData);

        [TestCase("\\x")][TestCase("\\uZZZZ")][TestCase("\\uD800")][TestCase("\\uDC00")][TestCase("\\uD800a")]
        public void InvalidEscapesAndUnpairedEscapedSurrogatesAreInvalidStrings(string text)
            => Single(Validate(SiteJson.Replace("site-1", text)), ContentDocumentKind.Site, ContentErrorCode.InvalidString);

        [Test]
        public void RawControlsAndUnpairedSurrogatesAreInvalidStrings()
        {
            foreach (var text in new[] { "a\n", "a\u0000", new string((char)0xd800, 1), new string((char)0xdc00, 1) })
                Single(Validate(SiteJson.Replace("site-1", text)), ContentDocumentKind.Site, ContentErrorCode.InvalidString);
            Single(Validate(SiteJson.Replace("site-1", char.ConvertFromUtf32(0x1f600))), ContentDocumentKind.Site, ContentErrorCode.InvalidId, "/id");
        }

        [Test]
        public void WireFailuresSuppressSemanticsAndKeepDocumentCatalogOrder()
        {
            var r = ContentValidator.Validate("{", "{", null);
            Rejected(r);
            Assert.That(r.Errors.Select(e => e.Document + ":" + e.Code), Is.EqualTo(new[] { "Site:InvalidJson", "Scenario:InvalidJson", "RevisionCatalog:WrongType" }));
            Single(Validate(SiteJson.Replace("\"frameId\":\"frame-1\"", "\"frameId\":\"missing\""), "{"), ContentDocumentKind.Scenario, ContentErrorCode.InvalidJson);
        }

        [Test]
        public void CatalogArgumentsAreCheckedBeforeSemanticWork()
        {
            Single(ContentValidator.Validate(SiteJson, ScenarioJson, null), ContentDocumentKind.RevisionCatalog, ContentErrorCode.WrongType, "");
            Single(ContentValidator.Validate(SiteJson, ScenarioJson, new ContentRevisionRef[] { null }), ContentDocumentKind.RevisionCatalog, ContentErrorCode.WrongType, "/0");
            Single(ContentValidator.Validate(SiteJson, ScenarioJson, new[] { new ContentRevisionRef("source-1", 4), new ContentRevisionRef("source-1", 4) }), ContentDocumentKind.RevisionCatalog, ContentErrorCode.DuplicateId, "/1/id");
            Assert.That(ContentValidator.Validate(SiteJson, ScenarioJson, new[] { new ContentRevisionRef("source-1", 3), new ContentRevisionRef("source-1", 4) }).IsValid, Is.True);
        }

        [Test]
        public void RevisionRefConstructorRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => new ContentRevisionRef(null, 0));
            Assert.Throws<ArgumentException>(() => new ContentRevisionRef("a\n", 0));
            Assert.That(() => new ContentRevisionRef("source-1", -1), Throws.InstanceOf<ArgumentException>());
            Assert.That(new ContentRevisionRef("source-1", long.MaxValue).Revision, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void SourceCatalogDistinguishesUnknownIdFromUnknownRevisionAndCase()
        {
            Single(ContentValidator.Validate(SiteJson, ScenarioJson, new ContentRevisionRef[0]), ContentDocumentKind.Site, ContentErrorCode.UnknownReference, "/sourceRefs/0/id");
            Single(ContentValidator.Validate(SiteJson, ScenarioJson, new[] { new ContentRevisionRef("Source-1", 4) }), ContentDocumentKind.Site, ContentErrorCode.UnknownReference, "/sourceRefs/0/id");
            Single(ContentValidator.Validate(SiteJson, ScenarioJson, new[] { new ContentRevisionRef("source-1", 3) }), ContentDocumentKind.Site, ContentErrorCode.UnknownRevision, "/sourceRefs/0/revision");
        }

        [Test]
        public void ExternalCatalogCannotSpoofPairedSiteBinding()
        {
            var catalog = new[] { new ContentRevisionRef("source-1", 4), new ContentRevisionRef("other-site", 2) };
            Single(ContentValidator.Validate(SiteJson, ScenarioJson.Replace("site-1", "other-site"), catalog), ContentDocumentKind.Scenario, ContentErrorCode.UnknownReference, "/site/id");
            Single(Validate(SiteJson, ScenarioJson.Replace("\"revision\":2", "\"revision\":3")), ContentDocumentKind.Scenario, ContentErrorCode.RevisionMismatch, "/site/revision");
            Single(Validate(SiteJson, ScenarioJson.Replace("site-1", "Site-1")), ContentDocumentKind.Scenario, ContentErrorCode.UnknownReference, "/site/id");
        }

        [TestCase("frames", "{\"id\":\"x\"}")]
        [TestCase("regions", "{\"id\":\"x\",\"frameId\":\"frame-1\"}")]
        [TestCase("portals", "{\"id\":\"x\",\"fromRegionId\":\"region-1\",\"toRegionId\":\"region-2\"}")]
        [TestCase("entities", "{\"id\":\"x\",\"frameId\":\"frame-1\",\"regionId\":\"region-1\"}")]
        [TestCase("sourceRefs", "{\"id\":\"source-1\",\"revision\":4}")]
        [TestCase("qualification", "{\"id\":\"x\",\"sourceRefIds\":[\"source-1\"]}")]
        public void TypedCollectionsRejectLaterDuplicateId(string collection, string item)
        {
            var start = SiteJson.IndexOf("\"" + collection + "\":[", StringComparison.Ordinal) + collection.Length + 4;
            var site = SiteJson.Insert(start, item + "," + item + ",");
            var r = Validate(site);
            Rejected(r);
            Assert.That(Key(r.Errors[0]), Is.EqualTo("Site:DuplicateId:/" + collection + "/1/id"));
        }

        [Test]
        public void SameIdAcrossTypedCollectionsIsAllowed()
            => Assert.That(Validate(SiteJson.Replace("portal-1", "entity-1")).IsValid, Is.True);

        [Test]
        public void SemanticErrorsHaveDeterministicPhaseAndInputOrder()
        {
            var site = SiteJson.Replace("\"frameId\":\"frame-1\"", "\"frameId\":\"missing-frame\"")
                .Replace("\"fromRegionId\":\"region-1\"", "\"fromRegionId\":\"missing-from\"")
                .Replace("\"toRegionId\":\"region-2\"", "\"toRegionId\":\"missing-to\"")
                .Replace("\"regionId\":\"region-1\"", "\"regionId\":\"missing-region\"")
                .Replace("[\"source-1\"]", "[\"source-1\",\"source-1\",\"absent-source\"]");
            var expected = new[] {
                "Site:UnknownReference:/regions/0/frameId", "Site:UnknownReference:/regions/1/frameId",
                "Site:MissingPortalEndpoint:/portals/0/fromRegionId", "Site:MissingPortalEndpoint:/portals/0/toRegionId",
                "Site:UnknownReference:/entities/0/frameId", "Site:UnknownReference:/entities/0/regionId",
                "Site:UnknownReference:/sourceRefs/0/id", "Site:DuplicateId:/qualification/0/sourceRefIds/1",
                "Site:MissingSourceReference:/qualification/0/sourceRefIds/2", "Scenario:UnknownReference:/site/id" };
            for (var i = 0; i < 3; i++)
            {
                var r = ContentValidator.Validate(site, ScenarioJson.Replace("site-1", "different-site"), new ContentRevisionRef[0]);
                Rejected(r);
                Assert.That(r.Errors.Select(Key), Is.EqualTo(expected));
            }
        }

        [Test]
        public void SourceRefOrderAndQualificationInputOrderArePreserved()
        {
            var site = SiteJson.Replace("{\"id\":\"source-1\",\"revision\":4}", "{\"id\":\"source-z\",\"revision\":1},{\"id\":\"source-1\",\"revision\":4}")
                .Replace("[\"source-1\"]", "[\"source-z\",\"source-1\"]");
            var r = ContentValidator.Validate(site, ScenarioJson, new[] { new ContentRevisionRef("source-1", 4), new ContentRevisionRef("source-z", 1) });
            Assert.That(r.IsValid, Is.True);
            Assert.That(r.Site.SourceRefs.Select(x => x.Id), Is.EqualTo(new[] { "source-z", "source-1" }));
            Assert.That(r.Site.Qualification[0].SourceRefIds, Is.EqualTo(new[] { "source-z", "source-1" }));
        }

        [Test]
        public void DocumentDepthAndArrayLimitsAreEnforced()
        {
            Single(Validate(new string(' ', 1048577)), ContentDocumentKind.Site, ContentErrorCode.LimitExceeded);
            var boundary = SiteJson + new string(' ', 1048576 - SiteJson.Length);
            Assert.That(Validate(boundary).IsValid, Is.True);
            Single(Validate(boundary + " "), ContentDocumentKind.Site, ContentErrorCode.LimitExceeded);
            // 알려지지 않은 필드의 중첩에도 reader 제한이 먼저 적용되어야 한다.
            Single(Validate("{\"x\":" + new string('[', 32) + "0" + new string(']', 32) + "}"), ContentDocumentKind.Site, ContentErrorCode.LimitExceeded);
            var frames = string.Join(",", Enumerable.Range(0, 4096).Select(i => "{\"id\":\"f" + i + "\"}"));
            var site = EmptySite.Replace("\"frames\":[]", "\"frames\":[" + frames + "]");
            Assert.That(Validate(site).IsValid, Is.True);
            Single(Validate(site.Replace("\"frames\":[", "\"frames\":[{\"id\":\"extra\"},")), ContentDocumentKind.Site, ContentErrorCode.LimitExceeded);
        }

        [Test]
        public void CatalogLimitAccepts4096AndRejects4097()
        {
            var catalog = Enumerable.Range(0, 4096).Select(i => new ContentRevisionRef("s" + i, 0)).ToList();
            Assert.That(ContentValidator.Validate(EmptySite, ScenarioJson, catalog).IsValid, Is.True);
            catalog.Add(new ContentRevisionRef("overflow", 0));
            Single(ContentValidator.Validate(EmptySite, ScenarioJson, catalog), ContentDocumentKind.RevisionCatalog, ContentErrorCode.LimitExceeded, "");
        }

        [Test]
        public void NestedReferenceShapesUseTheSameRevisionAndRequiredFieldRules()
        {
            Single(Validate(SiteJson.Replace("\"revision\":4", "\"revision\":4.0")), ContentDocumentKind.Site, ContentErrorCode.InvalidRevision, "/sourceRefs/0/revision");
            Single(Validate(SiteJson, ScenarioJson.Replace("\"revision\":2", "\"revision\":-0")), ContentDocumentKind.Scenario, ContentErrorCode.InvalidRevision, "/site/revision");
            Single(Validate(SiteJson, ScenarioJson.Replace("\"id\":\"site-1\",", "")), ContentDocumentKind.Scenario, ContentErrorCode.MissingProperty, "/site/id");
            Single(Validate(SiteJson, "{\"id\":\"scenario-1\",\"revision\":9,\"site\":null}"), ContentDocumentKind.Scenario, ContentErrorCode.WrongType, "/site");
        }

        [Test]
        public void DuplicateCollectionPhasePrecedesReferencePhaseRegardlessOfJsonFieldOrder()
        {
            var site = SiteJson.Replace("\"frames\":[{\"id\":\"frame-1\"}]", "\"frames\":[{\"id\":\"frame-1\"},{\"id\":\"frame-1\"}]")
                .Replace("\"qualification\":[", "\"qualification\":[{\"id\":\"declared-evidence-1\",\"sourceRefIds\":[\"source-1\"]},")
                .Replace("\"frameId\":\"frame-1\"", "\"frameId\":\"missing\"");
            var r = Validate(site);
            Rejected(r);
            Assert.That(r.Errors.Select(Key), Is.EqualTo(new[] {
                "Site:DuplicateId:/frames/1/id", "Site:DuplicateId:/qualification/1/id",
                "Site:UnknownReference:/regions/0/frameId", "Site:UnknownReference:/regions/1/frameId",
                "Site:UnknownReference:/entities/0/frameId" }));
        }

        [Test]
        public void NestedSourceReferenceArrayAlsoHasWireLimit()
        {
            var values = string.Join(",", Enumerable.Repeat("\"source-1\"", 4097));
            Single(Validate(SiteJson.Replace("[\"source-1\"]", "[" + values + "]")), ContentDocumentKind.Site, ContentErrorCode.LimitExceeded);
        }

        private static void ReadOnly<T>(IReadOnlyList<T> items)
        {
            Assert.That(items, Is.Not.InstanceOf<T[]>());
            var list = items as IList<T>;
            if (list == null) return;
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list.Add(default(T)));
            if (list.Count > 0) Assert.Throws<NotSupportedException>(() => list[0] = default(T));
        }

        [Test]
        public void AllExposedCollectionsAreReadOnlyAndCatalogMutationCannotChangeResult()
        {
            var catalog = Catalog().ToList();
            var r = ContentValidator.Validate(SiteJson, ScenarioJson, catalog);
            Assert.That(r.IsValid, Is.True);
            catalog.Clear();
            ReadOnly(r.Site.Frames); ReadOnly(r.Site.Regions); ReadOnly(r.Site.Portals);
            ReadOnly(r.Site.Entities); ReadOnly(r.Site.SourceRefs); ReadOnly(r.Site.Qualification);
            ReadOnly(r.Site.Qualification[0].SourceRefIds); ReadOnly(r.Errors);
            ReadOnly(Validate("{").Errors);
            Assert.That(r.Site.SourceRefs[0].Id, Is.EqualTo("source-1"));
            Assert.That(r.IsValid, Is.True);
            foreach (var type in new[] { typeof(SiteBundle), typeof(ScenarioSpec), typeof(ContentFrame), typeof(ContentRegion), typeof(ContentPortal), typeof(ContentEntity), typeof(ContentRevisionRef), typeof(ContentQualification), typeof(ContentValidationResult), typeof(ContentValidationError) })
                Assert.That(type.GetProperties().All(p => p.GetSetMethod() == null), Is.True, type.Name);
        }
    }
}
#endif
