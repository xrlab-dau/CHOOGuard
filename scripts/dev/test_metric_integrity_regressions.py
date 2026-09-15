"""Synthetic parser regressions, not Unity/20-client execution evidence."""
from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from foundation_load_metrics import (  # noqa: E402
    Histogram, Interval, MetricsError, MetricsReader, aggregate, assess,
    assert_fault_dispatch_agreement, dispatch_fault_case, strict_json,
)


def metric(role="server"):
    row = {
        "Schema": 1, "Role": role, "Kind": "interval", "DurationSeconds": 1,
        "TickCount": 20, "TickMilliseconds": {"Bounds": [25, 50], "Counts": [20, 0, 0]},
        "SentPayloadBytes": 100, "ReceivedPayloadBytes": 100,
        "ConnectedClients": 20 if role == "server" else 1, "NpcCount": 100,
        "ActiveIncidents": 2, "Batch": True, "NullGraphics": True,
        "UtcStart": "2026-09-15T00:00:00Z", "UtcEnd": "2026-09-15T00:00:01Z",
    }
    for key in ("ConnectedClients", "NpcCount", "ActiveIncidents"):
        for suffix in ("Min", "Max", "Mean"):
            row[key + suffix] = row[key]
    if role == "server":
        row.update(SimulationTickStart=0, SimulationTickEnd=20,
                   PausedAny=False, MaximumBacklogSeconds=0)
    return row


def readers():
    result = {}
    for label in ["server"] + [f"client-{i:02d}" for i in range(20)]:
        role = "server" if label == "server" else "client"
        result[label] = MetricsReader(Path("unused-synthetic-fixture"), role)
        result[label].intervals.append(Interval.parse(metric(role), role))
    return result


class MetricIntegrityTests(unittest.TestCase):
    def test_legacy_pairs_absent_remain_explicitly_unknown(self):
        interval = Interval.parse(metric(), "server")
        self.assertIsNone(interval.simulation_tick_count)
        self.assertIsNone(interval.frame_count)

    def test_complete_engine_pairs_are_preserved(self):
        row = metric()
        row.update(SimulationTickCount=20, SimulationTickMilliseconds={"Bounds": [50], "Counts": [20, 0]},
                   FrameCount=60, FrameMilliseconds={"Bounds": [20], "Counts": [60, 0]})
        interval = Interval.parse(row, "server")
        self.assertEqual(interval.simulation_tick_count, 20)
        self.assertEqual(interval.frame_count, 60)
        self.assertEqual(interval.frame_histogram.counts, (60, 0))

    def test_simulation_count_without_histogram_is_rejected(self):
        row = metric(); row["SimulationTickCount"] = 20
        with self.assertRaisesRegex(MetricsError, "SIMULATION_TIMING_FIELDS_INCOMPLETE"):
            Interval.parse(row, "server")

    def test_simulation_histogram_without_count_is_rejected(self):
        row = metric(); row["SimulationTickMilliseconds"] = {"Bounds": [50], "Counts": [20, 0]}
        with self.assertRaisesRegex(MetricsError, "SIMULATION_TIMING_FIELDS_INCOMPLETE"):
            Interval.parse(row, "server")

    def test_frame_count_without_histogram_is_rejected(self):
        row = metric(); row["FrameCount"] = 60
        with self.assertRaisesRegex(MetricsError, "FRAME_TIMING_FIELDS_INCOMPLETE"):
            Interval.parse(row, "server")

    def test_frame_histogram_without_count_is_rejected(self):
        row = metric(); row["FrameMilliseconds"] = {"Bounds": [20], "Counts": [60, 0]}
        with self.assertRaisesRegex(MetricsError, "FRAME_TIMING_FIELDS_INCOMPLETE"):
            Interval.parse(row, "server")

    def test_incomplete_pair_rejected_for_clients_too(self):
        row = metric("client"); row["SimulationTickCount"] = 0
        with self.assertRaises(MetricsError):
            Interval.parse(row, "client")

    def test_engine_histogram_still_checks_sample_count(self):
        row = metric(); row.update(FrameCount=5, FrameMilliseconds={"Bounds": [50], "Counts": [4, 0]})
        with self.assertRaisesRegex(MetricsError, "HISTOGRAM_TICK_COUNT"):
            Interval.parse(row, "server")

    def test_zero_complete_engine_pair_is_valid_zero_not_absent(self):
        row = metric(); row.update(FrameCount=0, FrameMilliseconds={"Bounds": [20], "Counts": [0, 0]})
        interval = Interval.parse(row, "server")
        self.assertEqual(interval.frame_count, 0)
        self.assertIsNone(interval.frame_histogram.percentile_bounds())

    def test_float_schema_is_rejected(self):
        row = metric(); row["Schema"] = 1.0
        with self.assertRaisesRegex(MetricsError, "UNSUPPORTED_SCHEMA"):
            Interval.parse(row, "server")

    def test_bool_schema_is_rejected(self):
        row = metric(); row["Schema"] = True
        with self.assertRaisesRegex(MetricsError, "UNSUPPORTED_SCHEMA"):
            Interval.parse(row, "server")

    def test_missing_and_string_schema_are_rejected(self):
        for value in (None, "1", 2, [], {}):
            with self.subTest(value=value):
                row = metric(); row["Schema"] = value
                with self.assertRaises(MetricsError):
                    Interval.parse(row, "server")

    def test_mixed_role_aggregation_is_rejected(self):
        intervals = [Interval.parse(metric(role), role) for role in ("server", "client")]
        with self.assertRaisesRegex(MetricsError, "AGGREGATE_ROLE_MISMATCH"):
            aggregate(intervals)

    def test_same_role_aggregation_remains_valid(self):
        summary = aggregate([Interval.parse(metric(), "server")] * 2)
        self.assertEqual(summary["TickCount"], 40)
        self.assertEqual(summary["DurationSeconds"], 2)

    def test_empty_aggregation_is_still_unknown(self):
        self.assertIsNone(aggregate([]))

    def test_unhashable_fault_variant_is_safe_error(self):
        for value in ([], {}, 1, True):
            with self.subTest(value=value):
                with self.assertRaisesRegex(MetricsError, "FAULT_KIND_UNKNOWN"):
                    dispatch_fault_case(expected_kind=value)

    def test_nontext_grouped_fault_is_safe_error(self):
        for value in ([], {}, 1, b"startup failed"):
            with self.subTest(value=value):
                with self.assertRaisesRegex(MetricsError, "FAULT_MARKER_INVALID"):
                    dispatch_fault_case(grouped_marker=value)

    def test_fault_channel_agreement_remains_valid(self):
        self.assertTrue(all(assert_fault_dispatch_agreement().values()))

    def test_fault_dispatch_ambiguity_still_rejected(self):
        with self.assertRaisesRegex(MetricsError, "FAULT_DISPATCH_AMBIGUOUS"):
            dispatch_fault_case(grouped_marker="startup failed; exception:")

    def test_reader_records_malformed_fault_and_continues(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "metrics.jsonl"
            fault = {"Schema": 1, "Role": "server", "Kind": "fault", "FaultKind": []}
            path.write_text(json.dumps(fault) + "\n" + json.dumps(metric()) + "\n", encoding="utf-8")
            reader = MetricsReader(path, "server")
            reader.poll(final=True)
            self.assertEqual(reader.errors, ["FAULT_KIND_UNKNOWN"])
            self.assertEqual(len(reader.intervals), 1)

    def test_reader_records_partial_engine_pair(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "metrics.jsonl"
            row = metric(); row["FrameCount"] = 60
            path.write_text(json.dumps(row) + "\n", encoding="utf-8")
            reader = MetricsReader(path, "server")
            reader.poll(final=True)
            self.assertEqual(reader.errors, ["FRAME_TIMING_FIELDS_INCOMPLETE"])
            self.assertEqual(reader.intervals, [])

    def test_nonfinite_requested_duration_cannot_record_a_run(self):
        for value in (float("nan"), float("inf"), -1, True, "1"):
            with self.subTest(value=value):
                with self.assertRaises(MetricsError):
                    assess(readers(), {}, value, 1)

    def test_nonfinite_observed_wall_cannot_record_a_run(self):
        for value in (float("nan"), float("inf"), -1, True, "1"):
            with self.subTest(value=value):
                with self.assertRaises(MetricsError):
                    assess(readers(), {}, 1, value)

    def test_zero_observed_wall_reports_incomplete_not_crash(self):
        result = assess(readers(), {}, 1, 0)
        self.assertEqual(result["Status"], "METRICS_INCOMPLETE")

    def test_valid_fixture_is_not_product_acceptance(self):
        result = assess(readers(), {}, 1, 1)
        self.assertEqual(result["Status"], "LOCAL_PROTOCOL_RUN_RECORDED")
        self.assertEqual(result["Acceptance"], "NOT_ASSESSED")
        self.assertIn("whole Foundation acceptance", result["NotVerified"])

    def test_missing_client_does_not_reduce_target(self):
        data = readers(); del data["client-19"]
        result = assess(data, {}, 1, 1)
        self.assertEqual(result["Status"], "METRICS_INCOMPLETE")
        self.assertIn({"Code": "EXACT20CLIENT_ROSTER_NOT_OBSERVED"}, result["Issues"])

    def test_failure_is_preserved(self):
        result = assess(readers(), {}, 1, 1, process_failure="synthetic crash")
        self.assertEqual(result["Status"], "RUN_FAILED")

    def test_strict_json_duplicate_fields_remain_rejected(self):
        with self.assertRaisesRegex(MetricsError, "DUPLICATE_JSON_FIELD"):
            strict_json('{"Schema":1,"Schema":1}')

    def test_strict_json_nan_remains_rejected(self):
        with self.assertRaisesRegex(MetricsError, "NONFINITE_JSON"):
            strict_json('{"value":NaN}')

    def test_server_reader_cannot_impersonate_a_client_process(self):
        data = readers()
        data["client-00"] = data["server"]
        result = assess(data, {}, 1, 1)
        self.assertEqual(result["Status"], "METRICS_INCOMPLETE")
        self.assertIn({"Process": "client-00", "Code": "PROCESS_ROLE_MISMATCH"}, result["Issues"])

    def test_interval_role_must_match_its_process_even_when_reader_role_matches(self):
        data = readers()
        data["client-00"].intervals = [Interval.parse(metric(), "server")]
        result = assess(data, {}, 1, 1)
        self.assertEqual(result["Status"], "METRICS_INCOMPLETE")
        self.assertIn({"Process": "client-00", "Code": "PROCESS_ROLE_MISMATCH"}, result["Issues"])

    def test_zero_required_duration_is_rejected(self):
        with self.assertRaises(MetricsError):
            assess(readers(), {}, 0, 1)

    def test_p95_and_budget_boundary_unchanged(self):
        histogram = Histogram.parse({"Bounds": [25, 50], "Counts": [18, 1, 1]}, 20)
        self.assertEqual(histogram.percentile_bounds(), {"LowerExclusiveMs": 25, "UpperInclusiveMs": 50})
        self.assertEqual(histogram.above_budget_range(), {"DefiniteSamples": 1, "PossibleSamples": 1})


if __name__ == "__main__":
    unittest.main()
