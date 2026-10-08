"""Verify a ShiftSampleTests summary and its JSONL copies before learning.

Offline, standard library only; never starts Unity or sends JEV requests. A pass
establishes capture completeness, not incident coverage or permission to collect
more data. Logged HTTP failures are warnings; unlogged director rejections fail.
"""
import argparse
import collections
import json
from pathlib import Path, PureWindowsPath


def strict_json(text):
    def reject_constant(value):
        raise ValueError("non-finite JSON constant")

    return json.loads(text, parse_constant=reject_constant)


def read_log(path):
    """Count actual records using the harness's HTTP-200/nonempty-object rule."""
    lines, answered = 0, 0
    by_purpose = collections.Counter()
    errors = []
    try:
        # Preserve terminators: universal newline translation would accept a
        # trailing lone CR that ReadRunLog does not consider a completed line.
        with path.open(encoding="utf-8-sig", newline="") as handle:
            for number, line in enumerate(handle, 1):
                if not line.endswith("\n"):
                    errors.append(f"JSONL line {number} is not newline-terminated")
                if not line.strip():
                    if line.strip("\r\n"):
                        errors.append(f"JSONL line {number} is not a JSON object")
                    continue
                lines += 1
                try:
                    record = strict_json(line)
                except ValueError:
                    errors.append(f"JSONL line {number} is not valid JSON")
                    continue
                if not isinstance(record, dict):
                    errors.append(f"JSONL line {number} is not a JSON object")
                    continue
                answers = record.get("answers")
                if record.get("http") == 200 and isinstance(answers, dict) and answers:
                    purpose = record.get("purpose")
                    if purpose is None:
                        purpose = ""  # ReadRunLog uses the same default.
                    if not isinstance(purpose, str):
                        errors.append(f"JSONL line {number} has an invalid purpose")
                        continue
                    answered += 1
                    by_purpose[purpose] += 1
    except (OSError, UnicodeError) as error:
        errors.append(f"cannot read JSONL ({type(error).__name__})")
    if not lines:
        errors.append("JSONL has no records")
    return lines, answered, dict(by_purpose), errors


def validate(summary_path, expected_shifts):
    """Return a JSON-safe report; missing evidence is an error, never a zero."""
    errors, warnings = [], []
    report = {"schema": "chooguard.capture-validation.v1", "ok": False,
              "expected_shifts": expected_shifts, "shifts": 0,
              "errors": errors, "warnings": warnings}
    if type(expected_shifts) is not int or expected_shifts <= 0:
        errors.append("expected_shifts must be a positive integer")
        return report
    summary_path = Path(summary_path)
    try:
        rows = strict_json(summary_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, ValueError) as error:
        errors.append(f"cannot read summary ({type(error).__name__})")
        return report
    if not isinstance(rows, list) or not rows:
        errors.append("summary must be a nonempty array of shifts")
        return report
    report["shifts"] = len(rows)
    if len(rows) != expected_shifts:
        errors.append(f"summary has {len(rows)} shifts; expected {expected_shifts}")
    root = summary_path.resolve().parent
    indices, copies = [], set()
    for position, row in enumerate(rows):
        prefix = f"summary row {position}"

        def fail(message):
            errors.append(f"{prefix}: {message}")

        def count(container, key, label=None):
            value = container.get(key)
            if type(value) is not int or value < 0:
                fail(f"{label or key} must be a nonnegative integer")
                return None
            return value

        if not isinstance(row, dict):
            fail("shift must be a JSON object")
            continue
        index = count(row, "shift")
        if index is not None:
            indices.append(index)
            prefix = f"shift {index}"
        for field, required in (("keyPresent", True), ("jevRejected", False), ("drained", True)):
            if row.get(field) is not required:
                fail(f"{field} must be {str(required).lower()}")
        for field in ("pendingRequests", "malformedLines", "errorLogs"):
            value = count(row, field)
            if value is not None and value != 0:
                fail(f"{field} must be 0; found {value}")
        director = row.get("director")
        if not isinstance(director, dict):
            fail("director snapshot is missing; use the current collection harness")
        else:
            value = count(director, "unanswered_rounds", "director.unanswered_rounds")
            if value is not None and value != 0:
                fail(f"director.unanswered_rounds must be 0; found {value} unrecorded/unanswered rounds")
        counters = {field: count(row, field) for field in ("requests", "jevLogLines", "answeredLines")}
        if counters["answeredLines"] == 0:
            fail("answeredLines must be greater than 0")
        if counters["requests"] is not None and counters["jevLogLines"] is not None:
            if counters["requests"] != counters["jevLogLines"]:
                fail("requests must equal jevLogLines after draining; records are missing or inconsistent")
        claimed_by = row.get("answeredByPurpose")
        valid_by = isinstance(claimed_by, dict) and all(
            type(value) is int and value >= 0 for value in claimed_by.values())
        if not valid_by:
            fail("answeredByPurpose must map purpose names to nonnegative integer counts")
        relative = row.get("jsonl")
        if not isinstance(relative, str) or not relative:
            fail("jsonl copy path is missing")
            continue
        relative_path = Path(relative.replace("\\", "/"))
        if (relative_path.is_absolute() or PureWindowsPath(relative).drive
                or "discarded" in (part.lower() for part in relative_path.parts)):
            fail("jsonl must name a selected copy inside the capture directory, outside discarded/")
            continue
        try:
            path = (root / relative_path).resolve()
            path.relative_to(root)
        except (OSError, ValueError, RuntimeError):
            fail("jsonl path must stay inside the capture directory")
            continue
        if path in copies:
            fail("multiple shifts refer to the same JSONL copy")
        copies.add(path)
        lines, answered, by_purpose, log_errors = read_log(path)
        for message in log_errors:
            fail(message)
        for field, actual in (("jevLogLines", lines), ("answeredLines", answered)):
            if counters[field] is not None and counters[field] != actual:
                fail(f"{field} is {counters[field]} in summary, but JSONL contains {actual}")
        if valid_by and claimed_by != by_purpose:
            fail("answeredByPurpose does not match the JSONL copy")
        if lines > answered:
            warnings.append(f"{prefix}: {lines - answered} logged request(s) have no usable answer; inspect before learning")
    if sorted(indices) != list(range(expected_shifts)):
        errors.append("shift indices must cover 0..expected_shifts-1 exactly once")
    report["ok"] = not errors
    return report


def positive_int(text):
    try:
        value = int(text)
    except ValueError as error:
        raise argparse.ArgumentTypeError("must be a positive integer") from error
    if value <= 0:
        raise argparse.ArgumentTypeError("must be a positive integer")
    return value


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("summary", type=Path, help="exact shifts-<UTC>.json file; copies are relative to its directory")
    parser.add_argument("--expected-shifts", required=True, type=positive_int, help="CG_SHIFT_COUNT used for this run")
    args = parser.parse_args(argv)
    report = validate(args.summary, args.expected_shifts)
    print(json.dumps(report, indent=2, allow_nan=False))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
