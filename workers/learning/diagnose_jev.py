"""Summarize JEV JSONL evidence without exporting request/answer text or credentials."""
import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path

from validate_capture import strict_json


PURPOSES = {"judge", "magnitude", "crowd-routine", "crowd-urgent", "compose", "warm-up"}
LANES = {"Director", "CrowdRoutine", "CrowdUrgent"}
LIMITATIONS = [
    "Only recorded, completed request lines are observed; this is not a connection-health or training-quality verdict.",
    "Budget/in-flight/key/preparation early returns are not logged by JevClient; their counts cannot be inferred here.",
    "No HTTP status does not distinguish timeout, DNS, TLS or cancellation. HTTP200 without parsed answers has multiple possible causes.",
    "Answer coverage compares logged question IDs with parsed-answer IDs; it does not revalidate answer semantics or recover raw server replies.",
    "Response transitions are per file/purpose, ordered by completion timestamps; they do not prove a particular request retried or gameplay recovered.",
    "Connection path is declared by the caller, not detected. No requests, retries or paid model calls are made.",
]


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def response_time(value):
    if not isinstance(value, str):
        return None
    try:
        at = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return at.astimezone(timezone.utc) if at.tzinfo else None
    except (ValueError, OverflowError):
        return None


def classify(row):
    if not isinstance(row, dict):
        raise ValueError("record_not_object")
    status, seconds = row.get("http"), row.get("seconds")
    if type(status) is not int or (status not in (-1, 0) and not 100 <= status <= 599):
        raise ValueError("invalid_http")
    if type(seconds) not in (int, float):
        raise ValueError("invalid_seconds")
    try:
        if not math.isfinite(seconds) or seconds < 0:
            raise ValueError("invalid_seconds")
    except OverflowError:
        raise ValueError("invalid_seconds") from None
    answers = row.get("answers")
    if answers is not None and (not isinstance(answers, dict) or
                               any(not isinstance(value, dict) or not value for value in answers.values())):
        raise ValueError("invalid_parsed_answers")
    if status in (-1, 0):
        outcome = "no_http_response"
    elif status in (401, 403):
        outcome = "http_auth_rejected"
    elif status == 429:
        outcome = "http_rate_limited"
    elif status >= 500:
        outcome = "http_server_error"
    elif status != 200:
        outcome = "other_http_status"
    elif not answers:
        outcome = "http200_without_parsed_answers"
    else:
        request = row.get("request")
        questions = request.get("questions") if isinstance(request, dict) else None
        if not isinstance(questions, dict) or not questions:
            outcome = "answers_coverage_unknown"
        elif set(answers) - set(questions):
            outcome = "unexpected_answer_ids"
        elif set(answers) == set(questions):
            outcome = "complete_answers"
        else:
            outcome = "partial_answers"
    purpose = row.get("purpose")
    lane = row.get("lane")
    # Arbitrary values may contain endpoint/key/user text; only fixed labels leave the reader.
    purpose = purpose if isinstance(purpose, str) and purpose in PURPOSES else "other"
    lane = lane if isinstance(lane, str) and lane in LANES else "other"
    return purpose, lane, outcome, seconds, response_time(row.get("at"))


def duration_summary(values):
    if not values:
        return {"samples": 0, "median": None, "p95_nearest_rank": None, "max": None}
    ordered = sorted(values)
    middle = len(ordered) // 2
    median = ordered[middle]
    if len(ordered) % 2 == 0:
        low, high = ordered[middle - 1], ordered[middle]
        median = low + (high - low) / 2  # Avoid overflowing the sum of two large durations.
    return {"samples": len(values), "median": median,
            "p95_nearest_rank": ordered[math.ceil(.95 * len(values)) - 1], "max": ordered[-1]}


def transitions(events, evidence_valid, known_purpose=True):
    if not evidence_valid or not known_purpose or any(at is None for at, _ in events):
        return {"available": False, "reason": "invalid_evidence_or_unknown_purpose_or_missing_timestamp"}
    ordered = sorted(events, key=lambda event: event[0])
    if len({at for at, _ in ordered}) != len(ordered):
        return {"available": False, "reason": "duplicate_completion_timestamps"}
    streak = longest = recoveries = 0
    for _, outcome in ordered:
        if outcome == "complete_answers":
            recoveries += int(streak > 0)
            streak = 0
        else:
            streak += 1
            longest = max(longest, streak)
    return {"available": True, "max_consecutive_not_complete": longest,
            "trailing_not_complete": streak, "complete_after_not_complete": recoveries}


def diagnose_file(path, index):
    counts, lanes, problems = Counter(), Counter(), Counter()
    durations, by_purpose, events = defaultdict(list), defaultdict(Counter), defaultdict(list)
    issue_lines, valid, lines = [], 0, 0
    value = hashlib.sha256()

    def problem(code, line=None):
        problems[code] += 1
        if line is not None and len(issue_lines) < 20:
            issue_lines.append({"line": line, "code": code})

    try:
        with Path(path).open("rb") as stream:
            for number, raw in enumerate(stream, 1):
                value.update(raw)
                if not raw.strip():
                    continue
                lines += 1
                if not raw.endswith(b"\n"):
                    problem("unterminated_line", number)
                    continue
                try:
                    text = raw.decode("utf-8-sig" if number == 1 else "utf-8")
                except UnicodeError:
                    problem("invalid_utf8", number)
                    continue
                try:
                    row = strict_json(text)
                except ValueError:
                    problem("invalid_json", number)
                    continue
                try:
                    purpose, lane, outcome, seconds, at = classify(row)
                except ValueError as error:
                    problem(str(error), number)  # classify only emits fixed error codes.
                    continue
                valid += 1
                counts[outcome] += 1
                lanes[lane] += 1
                by_purpose[purpose][outcome] += 1
                durations[purpose].append(seconds)
                events[purpose].append((at, outcome))
        if digest(Path(path)) != value.hexdigest():
            problem("file_changed_during_read")
    except OSError:
        problem("cannot_read_file")
    if not lines:
        problem("no_records")
    evidence_valid = not problems and valid > 0
    groups = {purpose: {"outcomes": dict(sorted(count.items())),
                        "seconds": duration_summary(durations[purpose]),
                        "response_transitions": transitions(events[purpose], evidence_valid, purpose != "other")}
              for purpose, count in sorted(by_purpose.items())}
    return {"file_index": index, "sha256": value.hexdigest() if "cannot_read_file" not in problems else None,
            "evidence_valid": evidence_valid, "nonblank_lines": lines, "classified_records": valid,
            "outcomes": dict(sorted(counts.items())), "lanes": dict(sorted(lanes.items())),
            "by_purpose": groups, "input_problems": dict(sorted(problems.items())),
            "first_problem_lines": issue_lines}


def diagnose(paths, connection_path="unspecified"):
    if connection_path not in ("direct", "relay", "unspecified"):
        raise ValueError("connection_path must be direct, relay or unspecified")
    files = [diagnose_file(path, index) for index, path in enumerate(paths, 1)]
    total, first_by_hash = Counter(), {}
    duplicates = False
    for file in files:
        fingerprint = file["sha256"]
        if fingerprint is not None and fingerprint in first_by_hash:
            file["duplicate_of_file_index"] = first_by_hash[fingerprint]
            duplicates = True
            continue
        if fingerprint is not None:
            first_by_hash[fingerprint] = file["file_index"]
        total.update(file["outcomes"])
    return {"schema": "chooguard.jev-diagnostics.v1",
            "evidence_valid": bool(files) and all(file["evidence_valid"] for file in files) and not duplicates,
            "connection_path_declared": connection_path, "duplicate_input_content": duplicates,
            "files": files, "totals": dict(sorted(total.items())),
            "totals_scope": "Classified records from unique input hashes; inspect evidence_valid before interpreting counts",
            "limitations": LIMITATIONS}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("jsonl", type=Path, nargs="+", help="explicit immutable log copies; paths/payloads are not echoed")
    parser.add_argument("--connection-path", choices=("direct", "relay", "unspecified"), default="unspecified")
    args = parser.parse_args()
    report = diagnose(args.jsonl, args.connection_path)
    print(json.dumps(report, indent=2, allow_nan=False))
    return 0 if report["evidence_valid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
