#!/usr/bin/env python3
"""Run license-free Foundation data tests; report Unity verification separately."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def main() -> int:
    sys.stderr.reconfigure(errors='backslashreplace')
    test_program = (
        'import sys, unittest; '
        'suite = unittest.defaultTestLoader.discover("scripts/foundation/tests"); '
        'count = suite.countTestCases(); '
        'print("Discovered Foundation tests:", count, file=sys.stderr); '
        'sys.exit(1) if count == 0 else None; '
        'result = unittest.TextTestRunner(verbosity=2).run(suite); '
        'sys.exit(0 if result.wasSuccessful() else 1)'
    )
    checks = [
        ('synthetic_contract', [sys.executable, 'scripts/foundation/validate.py']),
        ('synthetic_negative_tests', [sys.executable, '-c', test_program]),
    ]
    results = []
    for name, command in checks:
        run = subprocess.run(command, cwd=ROOT, text=True, encoding='utf-8',
                             env=dict(os.environ, PYTHONIOENCODING='utf-8'), capture_output=True)
        # Keep the JSON summary machine-readable; test diagnostics go to stderr.
        print(f'[{name}] exit={run.returncode}', file=sys.stderr)
        print(run.stdout + run.stderr, end='', file=sys.stderr)
        results.append({'name': name, 'exitCode': run.returncode,
                        'status': 'passed' if run.returncode == 0 else 'failed'})

    inputs = {}
    for directory in ['foundation', 'scripts/foundation', 'scripts/dev',
                      'Packages/com.xrlab.chooguard.foundation']:
        for path in sorted((ROOT / directory).rglob('*')):
            if path.is_file() and '__pycache__' not in path.parts and path.suffix in {
                '.json', '.cs', '.asmdef', '.py', '.md', '.xml'}:
                inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    schema = ROOT / 'schemas/foundation-scenario.schema.json'
    if schema.is_file():
        inputs[schema.relative_to(ROOT).as_posix()] = hashlib.sha256(schema.read_bytes()).hexdigest()
    failed = any(row['exitCode'] != 0 for row in results)
    print(json.dumps({
        'scope': 'foundation_python_data_checks',
        'status': 'failed' if failed else 'passed',
        'checks': results,
        'inputSha256': dict(sorted(inputs.items())),
        'csharpCompilation': 'not_run',
        'unityEditMode': 'not_run',
        'unityPlayMode': 'not_run',
        'unitySceneGeneration': 'not_run',
        'desktopPlaythrough': 'not_run',
        'playableDemoVerified': False,
        'hmd': 'not_run',
        'independentProviderReview': 'not_run',
        'issueDone': False,
    }, ensure_ascii=True, indent=2))
    return 1 if failed else 0


if __name__ == '__main__':
    raise SystemExit(main())
