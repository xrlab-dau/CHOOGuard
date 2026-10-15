#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Security.Cryptography;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Twin;
using ChooGuard.Editor.Twin;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    public sealed class MeasuredPlacementTests
    {
        private static PlacementVerification Proof()
        {
            string hash = new string('a', 64);
            var proof = new PlacementVerification
            {
                object_id = "fixture", status = "validated", validation_id = "fixture-only", capture_epoch = "test-epoch",
                coordinate_frame_id = "test-unity", handedness = "unity_left_handed_y_up", measurement_basis = "independent_field_survey",
                manifest_sha256 = hash, target_max_error_m = .02f, fit_control_ids = new[] { "fit-a", "fit-b", "fit-c" },
                sources = new[] {
                    new PlacementVerification.EvidenceSource { id = "survey", path = "fixture-survey", sha256 = hash, role = "field_survey", captured_at = "2026-10-08T00:00:00Z", capture_epoch = "test-epoch" },
                    new PlacementVerification.EvidenceSource { id = "photo", path = "fixture-photo", sha256 = hash, role = "field_capture", captured_at = "2026-10-08T00:00:00Z", capture_epoch = "test-epoch" }
                },
                checks = new[] { new PlacementVerification.PositionCheck { id = "holdout-anchor", object_id = "fixture", role = "holdout", predicted_position = Vector3.zero, measured_position = Vector3.right * .01f, uncertainty_m = .002f, evidence_source_ids = new[] { "survey" } } },
                dimension_checks = new PlacementVerification.DimensionCheck[3]
            };
            for (int i = 0; i < 3; i++) proof.dimension_checks[i] = new PlacementVerification.DimensionCheck { id = "dimension-" + i, object_id = "fixture", axis = new[] { "x", "y", "z" }[i], predicted_length_m = 1, measured_length_m = 1, uncertainty_m = .002f, evidence_source_ids = new[] { "survey" } };
            return proof;
        }

        [Test] public void NarrativeEvidenceCannotAuthorizeAPlacement()
        {
            Assert.That(new EquipmentPlacement { id = "fixture", placementEvidence = "photo inspected" }.HasPlacementEvidence, Is.False);
        }

        [Test] public void SurveyUncertaintyCountsAgainstTheAcceptanceLimit()
        {
            var proof = Proof();
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out _), Is.True);
            proof.checks[0].measured_position = Vector3.right * .019f;
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("position_error_exceeds_target"));
        }

        [Test] public void CertificateCannotBeReusedForAnotherObjectPoseOrFitControl()
        {
            var proof = Proof();
            Assert.That(proof.Validate("other", Vector3.zero, Vector3.zero, out _), Is.False);
            Assert.That(proof.Validate("fixture", Vector3.forward, Vector3.zero, out _), Is.False);
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.up * 90, out _), Is.False);
            proof.checks[0].id = "fit-a";
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("nonindependent_or_wrong_object_check"));
        }

        [Test] public void OldEpochAndReferenceOnlyFilesDoNotBecomePhysicalSurveyEvidence()
        {
            var proof = Proof();
            proof.sources[0].capture_epoch = "old-epoch";
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out _), Is.False);
            proof.sources[0].capture_epoch = "test-epoch";
            proof.sources[0].role = "reference_only";
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out _), Is.False);
        }

        [Test] public void EveryLocalDimensionAxisRequiresIndependentEvidence()
        {
            var proof = Proof();
            proof.dimension_checks[2].axis = "x";
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out _), Is.False);
            proof.dimension_checks[2].axis = "z";
            proof.dimension_checks[2].measured_length_m = 1.019f;
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("dimension_error_exceeds_target"));
        }

        [Test] public void ActualModelImportScaleAndChildGeometryAreChecked()
        {
            var root = new GameObject("synthetic model fixture");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            try
            {
                var proof = Proof();
                Assert.That(proof.ValidateModelDimensions(root, out _), Is.True);
                root.transform.localScale = Vector3.one * .01f;
                Assert.That(proof.ValidateModelDimensions(root, out _), Is.False, "Import scale must affect metre dimensions.");
                root.transform.localScale = Vector3.one;
                cube.transform.localScale = new Vector3(2, 1, 1);
                Assert.That(proof.ValidateModelDimensions(root, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void ModifiedEvidenceBytesAndEscapedPathsAreRejected()
        {
            string relative = "Temp/measured-twin-tests-" + Guid.NewGuid().ToString("N") + ".txt";
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "synthetic provenance test only");
            try
            {
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                var proof = Proof(); proof.manifest_path = relative; proof.manifest_sha256 = hash;
                foreach (var source in proof.sources) { source.path = relative; source.sha256 = hash; }
                Assert.That(MeasuredTwinImport.VerifySources(proof, out _), Is.True);
                File.AppendAllText(path, "changed");
                Assert.That(MeasuredTwinImport.VerifySources(proof, out var reason), Is.False);
                Assert.That(reason, Does.StartWith("evidence_hash_changed:"));
                proof.manifest_path = "../outside-project.txt";
                Assert.That(MeasuredTwinImport.VerifySources(proof, out _), Is.False);
            }
            finally { File.Delete(path); }
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-.1f)] [TestCase(0f)]
        public void InvalidNumericUncertaintyIsRejected(float uncertainty)
        {
            var proof = Proof(); proof.checks[0].uncertainty_m = uncertainty;
            Assert.That(proof.Validate("fixture", Vector3.zero, Vector3.zero, out _), Is.False);
        }
    }
}
#endif
