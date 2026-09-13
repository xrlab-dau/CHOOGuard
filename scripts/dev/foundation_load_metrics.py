"""Strict, provisional Schema1 interval metrics; never equates log presence with acceptance."""
from __future__ import annotations

import json
import math
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any

MAX_LINE_BYTES = 64 * 1024
MAX_FILE_BYTES = 256 * 1024 * 1024


class MetricsError(ValueError):
    """Safe code only: never includes raw metrics or private values."""


def _pairs(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise MetricsError("DUPLICATE_JSON_FIELD")
        value[key] = item
    return value


def strict_json(raw: str | bytes):
    try:
        return json.loads(raw, object_pairs_hook=_pairs,
                          parse_constant=lambda _: (_ for _ in ()).throw(MetricsError("NONFINITE_JSON")))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise MetricsError("INVALID_JSON") from exc


def number(value, *, integer=False, positive=False):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise MetricsError("INVALID_NUMBER")
    if integer and not isinstance(value, int):
        raise MetricsError("NONINTEGER_COUNTER")
    if isinstance(value, int) and abs(value) > 2**63 - 1:
        raise MetricsError("COUNTER_TOO_LARGE")
    if not math.isfinite(value) or value < 0 or positive and value <= 0:
        raise MetricsError("INVALID_NUMBER")
    return value


def utc(value):
    if not isinstance(value, str) or len(value) > 64:
        raise MetricsError("INVALID_UTC")
    try:
        result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise MetricsError("INVALID_UTC") from exc
    if result.tzinfo is None or result.utcoffset() is None:
        raise MetricsError("UTC_TIMEZONE_REQUIRED")
    return result


@dataclass(frozen=True)
class Histogram:
    # Bins: [0, b0], (b0,b1], ..., (last,+infinity). Counts are interval deltas.
    bounds: tuple[float, ...]
    counts: tuple[int, ...]

    @classmethod
    def parse(cls, raw, ticks):
        if not isinstance(raw, dict) or not isinstance(raw.get("Bounds"), list) or not isinstance(raw.get("Counts"), list):
            raise MetricsError("HISTOGRAM_MISSING")
        bounds = tuple(float(number(v, positive=True)) for v in raw["Bounds"])
        counts = tuple(number(v, integer=True) for v in raw["Counts"])
        if not bounds or len(bounds) > 256 or len(counts) != len(bounds) + 1:
            raise MetricsError("HISTOGRAM_SHAPE")
        if any(a >= b for a, b in zip(bounds, bounds[1:])):
            raise MetricsError("HISTOGRAM_BOUNDS")
        if sum(counts) != ticks:
            raise MetricsError("HISTOGRAM_TICK_COUNT")
        return cls(bounds, counts)

    def percentile_bounds(self, percentile=.95):
        total = sum(self.counts)
        if not total:
            return None
        rank = math.ceil(total * percentile)
        at = 0
        for i, count in enumerate(self.counts):
            at += count
            if at >= rank:
                return {"LowerExclusiveMs": self.bounds[i - 1] if i else 0,
                        "UpperInclusiveMs": self.bounds[i] if i < len(self.bounds) else None}
        raise MetricsError("HISTOGRAM_INTERNAL")

    def above_budget_range(self, milliseconds=50):
        definite = possible = 0
        for i, count in enumerate(self.counts):
            lower = self.bounds[i - 1] if i else 0
            upper = self.bounds[i] if i < len(self.bounds) else math.inf
            if lower >= milliseconds:
                definite += count
            if upper > milliseconds:
                possible += count
        return {"DefiniteSamples": definite, "PossibleSamples": possible}


@dataclass(frozen=True)
class Interval:
    role: str
    seconds: float
    ticks: int
    histogram: Histogram
    sent: int
    received: int
    connected: int
    npcs: int
    incidents: int
    population_ranges: dict[str, tuple[int | None, int | None, float | None]]
    utc_start: str | None
    utc_end: str | None
    utc_jump: bool
    utc_backwards: bool
    simulation_start: int | None = None
    simulation_end: int | None = None
    paused_any: bool | None = None
    maximum_backlog_seconds: float | None = None
    batch: bool | None = None
    null_graphics: bool | None = None

    @classmethod
    def parse(cls, row, expected_role):
        if not isinstance(row, dict) or row.get("Schema") != 1 or isinstance(row.get("Schema"), bool):
            raise MetricsError("UNSUPPORTED_SCHEMA")
        if row.get("Role") != expected_role or expected_role not in ("server", "client"):
            raise MetricsError("ROLE_MISMATCH")
        if row.get("Kind") == "fault":
            raise MetricsError("RUNTIME_METRIC_FAULT")
        if row.get("Kind") != "interval":
            raise MetricsError("UNSUPPORTED_METRIC_KIND")
        try:
            seconds = float(number(row["DurationSeconds"], positive=True))
            if seconds > 86400:
                raise MetricsError("INTERVAL_TOO_LONG")
            ticks = number(row["TickCount"], integer=True)
            histogram = Histogram.parse(row["TickMilliseconds"], ticks)
            counters = [number(row[k], integer=True) for k in
                        ("SentPayloadBytes", "ReceivedPayloadBytes", "ConnectedClients", "NpcCount", "ActiveIncidents")]
        except KeyError as exc:
            raise MetricsError("REQUIRED_METRIC_MISSING") from exc
        targets = {"ConnectedClients": 20, "NpcCount": 100, "ActiveIncidents": 2}
        population_ranges = {}
        for key, current in zip(("ConnectedClients", "NpcCount", "ActiveIncidents"), counters[2:]):
            low = high = mean = None
            if expected_role == "server" and current > targets[key]:
                raise MetricsError("WORLD_LOAD_TARGET_EXCEEDED")
            if key + "Min" in row:
                low = number(row[key + "Min"], integer=True)
                if low > current:
                    raise MetricsError("MINIMUM_EXCEEDS_CURRENT")
            if key + "Max" in row:
                high = number(row[key + "Max"], integer=True)
                if high < current or low is not None and high < low:
                    raise MetricsError("MAXIMUM_BELOW_CURRENT")
                if expected_role == "server" and high > targets[key]:
                    raise MetricsError("WORLD_LOAD_TARGET_EXCEEDED")
            if key + "Mean" in row:
                mean = number(row[key + "Mean"])
                if low is not None and mean < low or high is not None and mean > high:
                    raise MetricsError("MEAN_OUTSIDE_RANGE")
            population_ranges[key] = (low, high, mean)
        if expected_role == "client" and counters[2] != 1:
            raise MetricsError("CLIENT_POPULATION_INVALID")
        mode_keys = ("Batch", "NullGraphics")
        provided_modes = [row[key] for key in mode_keys if key in row]
        if any(not isinstance(value, bool) for value in provided_modes):
            raise MetricsError("INVALID_RUNTIME_MODE")
        if any(value is False for value in provided_modes):
            raise MetricsError("HEADLESS_RUNTIME_MODE_REQUIRED")
        if len(provided_modes) == len(mode_keys):
            batch, null_graphics = (row[key] for key in mode_keys)
        else:
            batch = null_graphics = None
        start, end = row.get("UtcStart"), row.get("UtcEnd")
        if (start is None) != (end is None):
            raise MetricsError("UTC_PAIR_INCOMPLETE")
        jump = backwards = False
        if start is not None:
            # Monotonic DurationSeconds remains authoritative even when civil UTC jumps.
            utc_seconds = (utc(end) - utc(start)).total_seconds()
            jump = abs(utc_seconds - seconds) > .25
            backwards = utc_seconds < 0
        sim_start = sim_end = paused = backlog = None
        progress_keys = ("SimulationTickStart", "SimulationTickEnd", "PausedAny", "MaximumBacklogSeconds")
        if expected_role == "server" and any(key in row for key in progress_keys):
            if not all(key in row for key in progress_keys):
                raise MetricsError("SIMULATION_PROGRESS_FIELDS_INCOMPLETE")
            sim_start = number(row["SimulationTickStart"], integer=True)
            sim_end = number(row["SimulationTickEnd"], integer=True)
            paused = row["PausedAny"]
            backlog = float(number(row["MaximumBacklogSeconds"]))
            if sim_end < sim_start or not isinstance(paused, bool):
                raise MetricsError("INVALID_SIMULATION_PROGRESS")
        return cls(expected_role, seconds, ticks, histogram, *counters, population_ranges, start, end, jump, backwards,
                   sim_start, sim_end, paused, backlog, batch, null_graphics)


@dataclass(frozen=True)
class MetricsWatermark:
    """Observed file identity and inclusive byte boundary captured before cleanup."""

    identity: tuple[int, int]
    end_offset: int


@dataclass
class MetricsReader:
    path: Path
    role: str
    offset: int = 0
    pending: bytes = b""
    intervals: list[Interval] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    identity: tuple[int, int] | None = None
    seen_intervals: set[tuple[str, str]] = field(default_factory=set)

    def capture_watermark(self):
        """Freeze an observed byte boundary without claiming producer-time attribution."""
        if not self.path.exists():
            raise MetricsError("METRICS_FILE_MISSING_AT_STOP")
        stat = self.path.stat()
        identity = (stat.st_dev, stat.st_ino)
        if self.identity is not None and identity != self.identity or stat.st_size < self.offset:
            raise MetricsError("METRICS_REPLACED_OR_TRUNCATED")
        if stat.st_size > MAX_FILE_BYTES:
            raise MetricsError("METRICS_FILE_TOO_LARGE")
        self.identity = identity
        return MetricsWatermark(identity, stat.st_size)

    def drain_available(self):
        """Drain currently written bytes without reading past the present file size."""
        watermark = self.capture_watermark()
        MetricsReader.poll(self, through=watermark)
        return watermark

    def poll(self, *, final=False, through=None):
        if through is not None and not isinstance(through, MetricsWatermark):
            raise MetricsError("INVALID_METRICS_WATERMARK")
        if not self.path.exists():
            if final or through is not None:
                self.errors.append("METRICS_FILE_MISSING")
            return
        stat = self.path.stat()
        identity = (stat.st_dev, stat.st_ino)
        if self.identity is not None and identity != self.identity or stat.st_size < self.offset:
            raise MetricsError("METRICS_REPLACED_OR_TRUNCATED")
        if through is not None and (identity != through.identity or stat.st_size < through.end_offset):
            raise MetricsError("METRICS_STOP_BOUNDARY_LOST")
        if stat.st_size > MAX_FILE_BYTES:
            raise MetricsError("METRICS_FILE_TOO_LARGE")
        self.identity = identity
        limit = through.end_offset if through is not None else stat.st_size
        with self.path.open("rb") as stream:
            stream.seek(self.offset)
            while self.offset < limit:
                new = stream.read(min(1024 * 1024, limit - self.offset))
                if not new:
                    raise MetricsError("METRICS_STOP_BOUNDARY_LOST")
                self.offset += len(new)
                pending = self.pending + new
                lines = pending.split(b"\n")
                self.pending = lines.pop()
                if len(self.pending) > MAX_LINE_BYTES:
                    raise MetricsError("METRIC_LINE_TOO_LARGE")
                for raw in lines:
                    if not raw.strip():
                        continue
                    if len(raw) > MAX_LINE_BYTES:
                        raise MetricsError("METRIC_LINE_TOO_LARGE")
                    try:
                        interval = Interval.parse(strict_json(raw), self.role)
                        if interval.utc_start is not None:
                            key = (utc(interval.utc_start), utc(interval.utc_end))
                            if key in self.seen_intervals:
                                raise MetricsError("DUPLICATE_UTC_INTERVAL")
                            self.seen_intervals.add(key)
                        self.intervals.append(interval)
                    except MetricsError as exc:
                        self.errors.append(str(exc))
                # Ordinary polling stays bounded to one chunk.  A final poll or
                # explicit pre-cleanup watermark is the only full-drain path.
                if not final and through is None:
                    break
        if final and self.pending:
            self.errors.append("UNTERMINATED_METRIC_LINE")


def aggregate(intervals: list[Interval]):
    if not intervals:
        return None
    bounds = intervals[0].histogram.bounds
    if any(r.histogram.bounds != bounds for r in intervals):
        raise MetricsError("HISTOGRAM_BOUNDS_CHANGED")
    histogram = Histogram(bounds, tuple(sum(r.histogram.counts[i] for r in intervals) for i in range(len(bounds) + 1)))
    seconds = math.fsum(r.seconds for r in intervals)
    ticks = sum(r.ticks for r in intervals)
    has_simulation = all(r.simulation_start is not None for r in intervals)
    continuous = has_simulation and all(a.simulation_end == b.simulation_start for a, b in zip(intervals, intervals[1:]))
    simulation_ticks = sum(r.simulation_end - r.simulation_start for r in intervals) if has_simulation else None
    return {"Intervals": len(intervals), "DurationSeconds": seconds, "TickCount": ticks,
            "ObservedTickCallsPerSecond": ticks / seconds,
            "SimulationTicks": simulation_ticks, "SimulationContinuous": continuous,
            "SimulationProgressMissingIntervals": sum(r.simulation_start is None for r in intervals),
            "PausedAny": any(r.paused_any for r in intervals) if has_simulation else None,
            "MaximumBacklogSeconds": max(r.maximum_backlog_seconds for r in intervals) if has_simulation else None,
            "SentPayloadBytes": sum(r.sent for r in intervals),
            "ReceivedPayloadBytes": sum(r.received for r in intervals),
            "TickMilliseconds": {"Bounds": list(bounds), "Counts": list(histogram.counts)},
            "P95Bounds": histogram.percentile_bounds(), "Above50ms": histogram.above_budget_range(),
            "UtcMissingIntervals": sum(r.utc_start is None for r in intervals),
            "UtcJumpIntervals": sum(r.utc_jump for r in intervals),
            "UtcBackwardsIntervals": sum(r.utc_backwards for r in intervals),
            "PopulationRangeMissingIntervals": sum(any(any(value is None for value in r.population_ranges[key])
                                                       for key in ("ConnectedClients", "NpcCount", "ActiveIncidents"))
                                                   for r in intervals),
            "RuntimeModeMissingIntervals": sum(r.batch is None or r.null_graphics is None for r in intervals)}


def ready_for_measurement(readers, clients=20):
    expected = {"ConnectedClients": clients, "NpcCount": 100, "ActiveIncidents": 2}
    server = readers["server"]
    if server.errors or not server.intervals or len(readers) != clients + 1:
        return False
    latest = server.intervals[-1]
    if any(latest.population_ranges.get(field) != (target, target, target) or
           getattr(latest, {"ConnectedClients": "connected", "NpcCount": "npcs", "ActiveIncidents": "incidents"}[field]) != target
           for field, target in expected.items()):
        return False
    if latest.batch is not True or latest.null_graphics is not True:
        return False
    if latest.paused_any is not False or latest.simulation_start is None or latest.simulation_end <= latest.simulation_start:
        return False
    return all(reader.intervals and not reader.errors and
               all(row.ticks > 0 and row.batch is True and row.null_graphics is True
                   for row in reader.intervals[-1:])
               for reader in readers.values())


def assess(readers, starts, required_duration, observed_wall_seconds, *, process_failure=None, stops=None):
    issues = []
    diagnostics = []
    summaries = {}
    rows_by_label = {}
    stops = stops or {}
    for label, reader in readers.items():
        if reader.errors:
            issues.append({"Process": label, "Code": reader.errors[0]})
        rows = reader.intervals[starts.get(label, 0):stops.get(label)]
        rows_by_label[label] = rows
        try:
            summary = aggregate(rows)
        except MetricsError as exc:
            summary = None
            issues.append({"Process": label, "Code": str(exc)})
        summaries[label] = summary
        if summary is None:
            issues.append({"Process": label, "Code": "NO_MEASURED_INTERVALS"})
            continue
        if summary["DurationSeconds"] + 1e-9 < required_duration:
            issues.append({"Process": label, "Code": "DURATION_COVERAGE_SHORT"})
        if summary["UtcMissingIntervals"]:
            issues.append({"Process": label, "Code": "UTC_COVERAGE_MISSING"})
        if summary["TickCount"] <= 0:
            issues.append({"Process": label, "Code": "POSITIVE_SAMPLE_COVERAGE_MISSING"})
        if summary["SentPayloadBytes"] == 0 or summary["ReceivedPayloadBytes"] == 0:
            issues.append({"Process": label, "Code": "BIDIRECTIONAL_PAYLOAD_NOT_OBSERVED"})
    if set(readers) != {"server"} | {f"client-{i:02d}" for i in range(20)}:
        issues.append({"Code": "EXACT20CLIENT_ROSTER_NOT_OBSERVED"})
    if observed_wall_seconds + 1e-6 < required_duration:
        issues.append({"Code": "MONOTONIC_RUN_DURATION_SHORT"})
    for label, rows in rows_by_label.items():
        previous = None
        seen_utc = set()
        for row in rows:
            if row.utc_start is not None:
                key = (utc(row.utc_start), utc(row.utc_end))
                if key in seen_utc:
                    issues.append({"Process": label, "Code": "DUPLICATE_UTC_INTERVAL"})
                seen_utc.add(key)
            if (previous is not None and previous.utc_end is not None and row.utc_start is not None and
                    not (previous.utc_backwards or row.utc_backwards)):
                previous_end, current_start = utc(previous.utc_end), utc(row.utc_start)
                if current_start < previous_end:
                    diagnostics.append({"Process": label, "Code": "UTC_INTERVAL_OVERLAP"})
                elif current_start > previous_end:
                    diagnostics.append({"Process": label, "Code": "UTC_INTERVAL_GAP"})
            previous = row
    server_rows = rows_by_label.get("server", [])
    fields = (("ConnectedClients", "connected", 20), ("NpcCount", "npcs", 100), ("ActiveIncidents", "incidents", 2))
    for field, attribute, target in fields:
        if any(getattr(row, attribute) != target or row.population_ranges.get(field) != (target, target, target)
               for row in server_rows):
            issues.append({"Process": "server", "Code": field.upper() + "_TARGET_RANGE_UNVERIFIED"})
    if any(summary and summary["PopulationRangeMissingIntervals"] for summary in summaries.values()):
        issues.append({"Code": "POPULATION_RANGE_FIELDS_MISSING"})
    if any(summary and summary["RuntimeModeMissingIntervals"] for summary in summaries.values()):
        issues.append({"Code": "RUNTIME_MODE_UNVERIFIED"})
    server = summaries.get("server")
    over = server["Above50ms"] if server else {"DefiniteSamples": 0, "PossibleSamples": 0}
    if server and not server["TickCount"]:
        issues.append({"Process": "server", "Code": "SERVER_TICKS_MISSING"})
    if over["PossibleSamples"] and not over["DefiniteSamples"]:
        issues.append({"Process": "server", "Code": "50MS_HISTOGRAM_BIN_AMBIGUOUS"})
    progress_missing = bool(server and server["SimulationProgressMissingIntervals"])
    if progress_missing:
        issues.append({"Process": "server", "Code": "PHYSICS_PROGRESS_UNVERIFIED"})
    if server and not progress_missing and not server["SimulationContinuous"]:
        issues.append({"Process": "server", "Code": "SIMULATION_TICK_DISCONTINUITY"})
    rate_short = bool(server and not progress_missing and server["SimulationTicks"] < max(1, math.floor(server["DurationSeconds"] * 20) - 1))
    paused = bool(server and server["PausedAny"])
    if process_failure:
        status = "RUN_FAILED"
    elif issues:
        status = "METRICS_INCOMPLETE"
    elif over["DefiniteSamples"] or rate_short or paused:
        status = "LOAD_BUDGET_EXCEEDED"
    else:
        status = "LOCAL_PROTOCOL_RUN_RECORDED"
    return {"Schema": 1, "Status": status, "Acceptance": "NOT_ASSESSED",
            "RequiredDurationSeconds": required_duration, "ObservedWallSeconds": observed_wall_seconds,
            "ProcessFailure": process_failure, "Issues": issues, "Diagnostics": diagnostics, "Processes": summaries,
            "ServerSimulationRate": {"RequiredHz": 20, "AllowedAggregateBoundaryTick": 1,
                                     "BelowRequired": rate_short if server and not progress_missing else None,
                                     "PausedDuringMeasurement": paused if server and not progress_missing else None},
            "RuntimeTimingScope": {"server": "entire TickServer call including physics/projection/persistence",
                                   "client": "Update frame interval; requested Batch/NullGraphics true; producer flags still require root integration",
                                   "not_measured": "GPU/wire/voice/rendered action latency"},
            "Scope": "20 headless local protocol clients plus dedicated server; reported interval evidence only",
            "NotVerified": ["human operation", "WAN", "100ms RTT/1% loss", "voice/microphones", "20 desktop renderers",
                            "field procedures/fidelity", "whole Foundation acceptance"],
            "MetricContract": {"Status": "provisional awaiting runtime producer integration",
                               "Duration": "monotonic interval wall seconds", "Counters": "interval deltas",
                               "Histogram": "inclusive upper Bounds; final overflow Counts; sum Counts=TickCount",
                               "Continuity": "UTC duplicates rejected; no monotonic interval index yet"}}
