using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Twin
{
    /// <summary>Physical survey evidence for an exact object pose, exported by workers/twin. Reference images alone do not authorize placement.</summary>
    [Serializable]
    public sealed class PlacementVerification
    {
        public const float MaximumErrorMetres = .02f;
        public string object_id, status, validation_id, capture_epoch, coordinate_frame_id;
        public string measurement_basis, manifest_sha256, manifest_path, handedness;
        public string[] fit_control_ids = Array.Empty<string>();
        public EvidenceSource[] sources = Array.Empty<EvidenceSource>();
        public PositionCheck[] checks = Array.Empty<PositionCheck>();
        public DimensionCheck[] dimension_checks = Array.Empty<DimensionCheck>();
        public Vector3 validated_rotation;
        public float target_max_error_m;

        [Serializable]
        public sealed class EvidenceSource
        {
            public string id, path, sha256, captured_at, capture_epoch, role;
        }

        [Serializable]
        public sealed class PositionCheck
        {
            public string id, object_id, role;
            public Vector3 predicted_position, measured_position;
            public float uncertainty_m;
            public string[] evidence_source_ids = Array.Empty<string>();
        }

        [Serializable]
        public sealed class DimensionCheck
        {
            public string id, object_id, axis;
            public float predicted_length_m, measured_length_m, uncertainty_m;
            public string[] evidence_source_ids = Array.Empty<string>();
        }

        /// <summary>Recomputes conservative errors and binds the certificate to the object, epoch, Unity coordinate basis and exact pose.</summary>
        public bool Validate(string objectId, Vector3 position, Vector3 rotation, out string reason)
        {
            reason = "";
            if (status != "validated" || string.IsNullOrWhiteSpace(objectId) || object_id != objectId)
                return Reject("object_not_independently_verified", out reason);
            if (string.IsNullOrWhiteSpace(validation_id) || string.IsNullOrWhiteSpace(capture_epoch) || string.IsNullOrWhiteSpace(coordinate_frame_id))
                return Reject("missing_epoch_or_coordinate_frame", out reason);
            if (measurement_basis != "independent_field_survey" || handedness != "unity_left_handed_y_up")
                return Reject("unsupported_measurement_or_coordinate_basis", out reason);
            if (!Hash(manifest_sha256) || !Positive(target_max_error_m) || target_max_error_m > MaximumErrorMetres)
                return Reject("missing_manifest_or_invalid_target", out reason);
            if (!Finite(position) || !Finite(rotation) || !Finite(validated_rotation) || Quaternion.Angle(Quaternion.Euler(rotation), Quaternion.Euler(validated_rotation)) > .01f)
                return Reject("pose_changed_since_validation", out reason);
            var evidence = new Dictionary<string, EvidenceSource>(StringComparer.Ordinal);
            foreach (var source in sources ?? Array.Empty<EvidenceSource>())
            {
                if (source == null || string.IsNullOrWhiteSpace(source.id) || evidence.ContainsKey(source.id) || !Hash(source.sha256) ||
                    string.IsNullOrWhiteSpace(source.path) || source.capture_epoch != capture_epoch || !DateTimeOffset.TryParse(source.captured_at, out _) ||
                    (source.role != "field_survey" && source.role != "field_capture"))
                    return Reject("invalid_or_mixed_epoch_source", out reason);
                evidence.Add(source.id, source);
            }
            bool captured = false;
            foreach (var source in evidence.Values) captured |= source.role == "field_capture";
            if (evidence.Count == 0 || !captured) return Reject("missing_physical_sources", out reason);
            var fit = new HashSet<string>(fit_control_ids ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (fit.Count < 3) return Reject("missing_registration_controls", out reason);
            foreach (string id in fit) if (string.IsNullOrWhiteSpace(id)) return Reject("invalid_registration_control", out reason);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool positionChecked = false;
            foreach (var check in checks ?? Array.Empty<PositionCheck>())
            {
                if (check == null || string.IsNullOrWhiteSpace(check.id) || !ids.Add(check.id) || fit.Contains(check.id) || check.role != "holdout" || check.object_id != objectId)
                    return Reject("nonindependent_or_wrong_object_check", out reason);
                if (!Finite(check.predicted_position) || !Finite(check.measured_position) || !Positive(check.uncertainty_m) ||
                    Vector3.Distance(check.predicted_position, position) > .0005f)
                    return Reject("invalid_or_stale_position_check", out reason);
                if (!SurveyEvidence(check.evidence_source_ids, evidence)) return Reject("position_without_field_survey", out reason);
                if (Vector3.Distance(check.predicted_position, check.measured_position) + check.uncertainty_m > target_max_error_m + 1e-6f)
                    return Reject("position_error_exceeds_target", out reason);
                positionChecked = true;
            }
            if (!positionChecked) return Reject("missing_exact_object_holdout", out reason);
            var axes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var check in dimension_checks ?? Array.Empty<DimensionCheck>())
            {
                if (check == null || check.object_id != objectId || string.IsNullOrWhiteSpace(check.id) || !ids.Add(check.id) || fit.Contains(check.id) ||
                    (check.axis != "x" && check.axis != "y" && check.axis != "z") || !axes.Add(check.axis) ||
                    !Positive(check.predicted_length_m) || !Positive(check.measured_length_m) || !Positive(check.uncertainty_m))
                    return Reject("invalid_dimension_check", out reason);
                if (!SurveyEvidence(check.evidence_source_ids, evidence)) return Reject("dimension_without_field_survey", out reason);
                if (Mathf.Abs(check.predicted_length_m - check.measured_length_m) + check.uncertainty_m > target_max_error_m + 1e-6f)
                    return Reject("dimension_error_exceeds_target", out reason);
            }
            if (axes.Count != 3) return Reject("missing_three_axis_dimensions", out reason);
            return true;
        }

        /// <summary>Checks actual model dimensions in metres, including import scale; it does not certify surface detail.</summary>
        public bool ValidateModelDimensions(GameObject model, out string reason)
        {
            reason = "";
            if (model == null || dimension_checks == null || dimension_checks.Length != 3)
                return Reject("missing_model_or_dimension_checks", out reason);
            bool hasBounds = false;
            Bounds bounds = default;
            Matrix4x4 frame = Matrix4x4.TRS(model.transform.position, model.transform.rotation, Vector3.one).inverse;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 matrix = frame * filter.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int bits = 0; bits < 8; bits++)
                {
                    var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((bits & 1) == 0 ? -1 : 1, (bits & 2) == 0 ? -1 : 1, (bits & 4) == 0 ? -1 : 1)));
                    if (!hasBounds) { bounds = new Bounds(p, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (!hasBounds) return Reject("model_has_no_measurable_bounds", out reason);
            foreach (var check in dimension_checks)
            {
                if (check == null) return Reject("invalid_dimension_check", out reason);
                float size = check.axis == "x" ? bounds.size.x : check.axis == "y" ? bounds.size.y : bounds.size.z;
                if (Mathf.Abs(size - check.predicted_length_m) > .0005f)
                    return Reject("model_dimensions_changed:" + check.axis, out reason);
            }
            return true;
        }

        private static bool SurveyEvidence(string[] refs, Dictionary<string, EvidenceSource> sources)
        {
            if (refs == null || refs.Length == 0) return false;
            bool surveyed = false;
            foreach (string id in refs)
            {
                if (id == null || !sources.TryGetValue(id, out var source)) return false;
                surveyed |= source.role == "field_survey";
            }
            return surveyed;
        }

        public static bool Hash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Nonnegative(float value) => Finite(value) && value >= 0;
        private static bool Positive(float value) => Finite(value) && value > 0;
        private static bool Reject(string value, out string reason) { reason = value; return false; }
    }
}
