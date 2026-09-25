#!/usr/bin/env python3
"""Explicit offline FDS execution, not a live or bidirectionally coupled Player solver."""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import pathlib
import platform
import subprocess


def sha(path: pathlib.Path) -> str:
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def run(manifest_path: pathlib.Path, case: pathlib.Path) -> dict:
    manifest_path = manifest_path.resolve(strict=True)
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest.get('schemaVersion') != 1 or manifest.get('solverVersion') != '6.11.1':
        raise ValueError('An explicit supported FDS 6.11.1 solver package manifest is required')
    relative = pathlib.PurePosixPath(manifest['executable'])
    if relative.is_absolute() or '..' in relative.parts or '\\' in str(relative) or ':' in str(relative):
        raise ValueError('Solver executable must be relative to its package manifest')
    binary = (manifest_path.parent / str(relative)).resolve(strict=True)
    if not binary.is_relative_to(manifest_path.parent) or sha(binary) != manifest['binarySha256']:
        raise ValueError('FDS solver package binary identity mismatch')
    case = case.resolve(strict=True)
    deck = case / 'hall.fds'
    if not deck.is_file():
        raise FileNotFoundError('Explicit --case must contain the reviewed hall.fds deck')
    # Do not overwrite the bundled completed reference or earlier execution evidence.
    if (case / 'receipt.json').exists() or (case / 'solver.stderr.log').exists():
        raise FileExistsError('Use a fresh case directory; previous solver evidence is immutable')
    if sha(deck) != 'cd4c230bb8a7ce1801002c178a02abac0c3137cff4b52ce03fd15a1fd6deb895':
        raise ValueError('Only the reviewed 0–120 s reference-hall deck is supported')
    command = [str(binary), 'hall.fds']
    env = dict(os.environ, OMP_NUM_THREADS='2')
    with (case / 'solver.stdout.log').open('w', encoding='utf-8') as out, (case / 'solver.stderr.log').open('w', encoding='utf-8') as err:
        result = subprocess.run(command, cwd=case, env=env, stdout=out, stderr=err, check=False)
    completed = result.returncode == 0 and 'FDS completed successfully' in (case / 'solver.stderr.log').read_text(encoding='utf-8')
    receipt = {
        'caseId': 'reference-hall-30x20-v1', 'recordedAt': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'solver': 'FDS', 'solverVersion': manifest['solverVersion'], 'solverRevision': manifest['solverRevision'],
        'solverExitCode': result.returncode, 'completed': completed, 'platform': platform.platform(),
        'binarySha256': sha(binary), 'installerSha256': manifest['artifactSha256'], 'source': manifest['source'],
        'command': command, 'environment': {'OMP_NUM_THREADS': '2'}, 'deckSha256': sha(deck),
        'outputSha256': {p.name: sha(p) for p in sorted(case.iterdir()) if p.suffix in ('.sf', '.smv', '.out', '.csv')},
        'simulationIntervalSeconds': [0, 120], 'gridMeters': [.5, .5, .5], 'cells': [60, 40, 8], 'sliceHeightMeters': 1.5,
        'scope': 'Uncalibrated reference hall; no mesh/time convergence or Busan validation'
    }
    (case / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--solver-manifest', type=pathlib.Path, required=True)
    parser.add_argument('--case', type=pathlib.Path, required=True)
    args = parser.parse_args()
    outcome = run(args.solver_manifest, args.case)
    print(json.dumps({'completed': outcome['completed'], 'solverExitCode': outcome['solverExitCode']}))
    raise SystemExit(0 if outcome['completed'] else 1)
