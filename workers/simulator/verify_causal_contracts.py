#!/usr/bin/env python3
"""Fresh portable causal contract evidence. Does not launch Unity or providers.

The full 5105 fixture is the parsed historical tape, not recovered native bits.
Original source geometry/runtime failure remains an external acceptance gate.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/simulator/causal-contracts'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    run = subprocess.run([sys.executable, str(ROOT / 'workers/simulator/run_checks.py'), 'test'],
                         cwd=ROOT, capture_output=True, text=True)
    print(run.stdout, end='')
    if run.stderr:
        print(run.stderr, end='', file=sys.stderr)
    baseline = ROOT / 'output/simulator/checks/last-test.json'
    receipt = json.loads(baseline.read_text()) if baseline.exists() else {}
    stdout = run.stdout
    required = ['PASS ChooGuard.Seeded.Tests.InitialConditionMaterialTests',
                'PASS ChooGuard.Seeded.Tests.EmergenceGenerationTests',
                'PASS ChooGuard.Seeded.Tests.KernelTests',
                'PASS ChooGuard.Seeded.Tests.WorldStateCodecTests',
                'PASS ChooGuard.Seeded.Tests.LocalTapeCodecTests',
                'PASS ChooGuard.Seeded.Tests.MaterialStoreTests',
                'PASS ChooGuard.Seeded.Tests.PlanningTests']
    checks = {
        'required_suites_passed': all(line in stdout for line in required),
        'predeclared_rng_cases': stdout.count('RNG probability fixture r=') == 9,
        'time_step_sensitivity_cases': stdout.count('Synthetic thermal dt sensitivity') == 3,
        'all_5105_parsed_entities_and_20tick_replay': 'Whole5105 parsed-baseline v2 exact codec+20tick ordered-input causal replay PASS' in stdout,
        'fresh_source_stable_during_run': receipt.get('source_changed_during_run') is False,
    }
    current_drift = [path for path, expected in receipt.get('sources', {}).items()
                     if not (ROOT / path).exists() or hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != expected]
    report = {
        'passed': run.returncode == 0 and all(checks.values()) and not current_drift,
        'exit_code': run.returncode, 'checks': checks, 'current_source_drift': current_drift,
        'portable_receipt': str(baseline.relative_to(ROOT)),
        'portable_receipt_sha256': hashlib.sha256(baseline.read_bytes()).hexdigest() if baseline.exists() else None,
        'sources': receipt.get('sources', {}),
        'scope': 'portable contracts only; parsed historical5105 world, not original native baseline recovery',
        'Unity_executions': 0, 'real_provider_requests': 0, 'helper_restarts': 0,
        'actual_whole_world_Unity': 'FAIL/PENDING preserved; this verifier does not replace native acceptance',
        'fresh_LLM_seed_determinism': 'not guaranteed; fixed material plus seed is the reproducibility input',
    }
    (OUT / 'portable-receipt.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
