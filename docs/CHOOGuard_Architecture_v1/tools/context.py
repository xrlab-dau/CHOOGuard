#!/usr/bin/env python3
"""Print a bounded architecture EP packet. Not a scheduler or execution agent."""
import json, sys
from pathlib import Path
root = Path(__file__).resolve().parents[1]
if len(sys.argv) != 2 or sys.argv[1] not in {f"EP{i:02d}" for i in range(12)}:
    raise SystemExit("Usage: python tools/context.py EP00..EP11")
p = root / "agent-packets" / (sys.argv[1] + ".json")
print(json.dumps(json.loads(p.read_text(encoding="utf-8")), ensure_ascii=False, indent=2))
