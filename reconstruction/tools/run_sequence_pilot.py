#!/usr/bin/env python3
"""Compare DA3 SMALL/BASE, independent SfM and MapAnything on the exact same four frames.

This orchestrates each engine's own audited runner in its own pinned virtual environment
(they are not merged into one dependency set) and assembles one comparison receipt. Per
this project's acceptance rule, an engine that did not actually execute (for example a
MapAnything run blocked on a missing local weight) is recorded and excluded from ranking,
never silently reported as tied or as a failure of that engine.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
import shutil
import signal
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
DEFAULT_PYTHONS = {
    'da3': ROOT / '.local/reconstruction/venv/bin/python',
    'sfm': ROOT / '.local/reconstruction/sfm-venv/bin/python',
    'mapanything': ROOT / '.local/reconstruction/mapanything-venv/bin/python',
}


def sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def write_json(path: Path, value: dict) -> None:
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    os.replace(temporary, path)


def validate_input_provenance(images: list[Path]) -> list[dict]:
    """Confirm each frame's hash still matches its sibling extraction receipt.

    This reuses scripts/references' acquisition/extraction output (samples.json) as the
    trust anchor for "same input" across engines, instead of only trusting file bytes
    with no link back to how/when the frame was actually sampled from source video.
    """
    from PIL import Image
    if len(set(images)) != len(images) or len({p.parent for p in images}) != 1:
        raise ValueError('Duplicate frames or mixed source directories')
    rows = []
    previous_pts = -math.inf
    for path in images:
        samples_path = path.parent / 'samples.json'
        if not samples_path.is_file():
            raise ValueError(f'No sibling extraction receipt (samples.json) for {path}; '
                              'refusing an unverified input frame.')
        samples = json.loads(samples_path.read_text())
        matching = [f for f in samples['frames'] if f['file'] == path.name]
        if len(matching) > 1:
            raise ValueError('Duplicate frame records')
        record = matching[0] if matching else None
        if record is None:
            raise ValueError(f'{path.name} is not a recorded frame in {samples_path}')
        actual = sha(path)
        if actual != record['sha256']:
            raise ValueError(f'{path.name} sha256 differs from its extraction receipt; '
                              'refusing a stale or edited input frame.')
        pts = record['sourcePtsSeconds']
        if isinstance(pts, bool) or not math.isfinite(pts) or pts <= previous_pts:
            raise ValueError('Source PTS must be finite and strictly increasing')
        previous_pts = pts
        with Image.open(path) as image:
            if image.size != (record['width'], record['height']) or image.getexif().get(274, 1) != 1:
                raise ValueError('Frame dimensions/orientation differ from receipt')
        rows.append({'path': str(path), 'sha256': actual,
                     'sourcePtsSeconds': pts, 'width': record['width'], 'height': record['height'],
                     'samplesReceiptSha256': sha(samples_path)})
    if len({r['sha256'] for r in rows}) != len(rows):
        raise ValueError('Duplicate frame content')
    source = (samples_path.parent / samples.get('sourceVideoPath', '')).resolve()
    if not source.is_file() or sha(source) != samples.get('sourceVideoSha256'):
        raise ValueError('Source video SHA/path mismatch')
    check = samples.get('sequenceVerification', {})
    if (check.get('method') != 'ffmpeg-scene-score' or check.get('cutCount') != 0
            or check.get('threshold') != .3
            or check.get('sourceVideoSha256') != samples['sourceVideoSha256']):
        raise ValueError('Missing continuous-interval cut check or detected cut')
    start, end = check.get('startPtsSeconds', math.nan), check.get('endPtsSeconds', math.nan)
    source_start = samples.get('sourceStartPtsSeconds', math.nan)
    duration = samples.get('sourceDurationSeconds', math.nan)
    if (not all(math.isfinite(x) for x in (start, end, source_start, duration)) or duration <= 0
            or not source_start <= start <= rows[0]['sourcePtsSeconds']
            or not rows[-1]['sourcePtsSeconds'] <= end < source_start + duration):
        raise ValueError('Cut scan/source interval does not cover ordered frames')
    return rows


def process_tree_rss(pid):
    """RSS for the launched process and its descendants (macOS/Linux ps reports KiB)."""
    text = subprocess.check_output(['ps', '-eo', 'pid=,ppid=,rss='], text=True, timeout=5)
    processes = [tuple(map(int, line.split())) for line in text.splitlines() if line.strip()]
    children = {pid}
    while True:
        expanded = children | {child for child, parent, _ in processes if parent in children}
        if expanded == children:
            return sum(rss * 1024 for child, _, rss in processes if child in children)
        children = expanded


def run_stage(python_bin: Path, script: Path, args: list[str], *, timeout: float) -> dict:
    if not python_bin.is_file():
        return {'status': 'not_executed', 'reason': f'No interpreter at {python_bin}.'}
    return run_bounded_command([str(python_bin), str(script), *args], timeout=timeout)


def run_bounded_command(command, *, timeout, max_rss_bytes=8 * 1024**3):
    started = time.perf_counter()
    free = shutil.disk_usage(ROOT).free
    if free < 8 * 1024**3:
        return {'status': 'resource_budget_exceeded', 'freeDiskBytes': free,
                'reason': 'Less than 8 GiB free disk before launch'}
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, start_new_session=True)
    reason = None
    peak_rss = 0
    while True:
        try:
            remaining = max(.001, timeout - (time.perf_counter() - started))
            stdout, stderr = process.communicate(timeout=min(.5, remaining))
            break
        except subprocess.TimeoutExpired:
            try:
                peak_rss = max(peak_rss, process_tree_rss(process.pid))
            except (OSError, subprocess.SubprocessError, ValueError):
                reason = 'resource_monitor_failed'
            if time.perf_counter() - started > timeout:
                reason = 'timed_out'
            elif peak_rss > max_rss_bytes or shutil.disk_usage(ROOT).free < 8 * 1024**3:
                reason = 'resource_budget_exceeded'
            if reason:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                stdout, stderr = process.communicate()
                break
    return {'status': reason or 'exited', 'returncode': process.returncode,
            'elapsedSeconds': time.perf_counter() - started, 'initialFreeDiskBytes': free,
            'peakSampledProcessTreeRssBytes': peak_rss,
            'stdoutTail': stdout[-2000:], 'stderrTail': stderr[-2000:]}


def ranking_exclusion(receipt, stage, common_hashes, directory):
    if stage.get('status') != 'exited' or stage.get('returncode') != 0:
        return 'Process did not exit successfully'
    if receipt.get('status') not in ('succeeded', 'succeeded_fully_registered'):
        return 'Not a complete successful reconstruction; partial/split/blocked runs excluded'
    if [i.get('sha256') for i in receipt.get('inputs', [])] != common_hashes:
        return 'Receipt input SHA/order mismatch'
    artifacts = receipt.get('outputs', []) or [receipt.get('output', {})]
    if not artifacts or any(not a.get('file') or not a.get('sha256') for a in artifacts):
        return 'Missing artifact hash records'
    for artifact in artifacts:
        path = (directory / artifact['file']).resolve()
        if not path.is_relative_to(directory.resolve()):
            return 'Artifact escaped run directory'
        if not path.is_file() or sha(path) != artifact['sha256']:
            return 'Artifact SHA/file mismatch'
    return None


def read_receipt(path: Path) -> dict | None:
    if not path.is_file():
        return None
    try:
        value = json.loads(path.read_text())
        if not isinstance(value, dict):
            raise ValueError('Expected receipt object')
        return value
    except (ValueError, OSError) as error:
        return {'status': 'invalid_receipt', 'error': str(error)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', action='append', required=True,
                        help='Ordered local input path (with a sibling samples.json); repeat exactly four times.')
    parser.add_argument('--output', type=Path, required=True,
                        help='New base output directory under reconstruction/output/.')
    parser.add_argument('--skip-mapanything', action='store_true',
                        help='Skip MapAnything entirely instead of writing its own blocked receipt.')
    args = parser.parse_args()
    if len(args.image) != 4:
        raise ValueError('The comparison requires exactly 4 continuous ordered frames.')
    images = [Path(p).resolve() for p in args.image]
    output = args.output.resolve()
    output.relative_to((ROOT / 'reconstruction/output').resolve())
    if output.exists():
        raise ValueError('Refuse to overwrite a previous pilot comparison; choose a new output directory.')
    input_rows = validate_input_provenance(images)
    output.mkdir(parents=True)
    image_args = []
    for path in images:
        image_args += ['--image', str(path)]

    stages = {}
    stages['da3_small'] = run_stage(
        DEFAULT_PYTHONS['da3'], TOOLS / 'run_da3.py',
        image_args + ['--model', 'SMALL', '--process-res', '336', '--device', 'auto',
                      '--cpu-fallback', '--output', str(output / 'da3-small')], timeout=300)
    stages['da3_base'] = run_stage(
        DEFAULT_PYTHONS['da3'], TOOLS / 'run_da3.py',
        image_args + ['--model', 'BASE', '--process-res', '504', '--device', 'auto',
                      '--cpu-fallback', '--output', str(output / 'da3-base')], timeout=300)
    stages['sfm'] = run_stage(
        DEFAULT_PYTHONS['sfm'], TOOLS / 'run_sfm_sequence.py',
        image_args + ['--output', str(output / 'sfm')], timeout=300)
    if args.skip_mapanything:
        stages['mapanything'] = {'status': 'not_executed', 'reason': '--skip-mapanything was passed.'}
    else:
        mapanything_python = DEFAULT_PYTHONS['mapanything']
        stages['mapanything'] = run_stage(
            mapanything_python, TOOLS / 'run_mapanything.py',
            image_args + ['--output', str(output / 'mapanything')], timeout=1800)

    engines = {}
    for name, receipt_name, out_name in [
        ('da3_small', 'inference-receipt.json', 'da3-small'),
        ('da3_base', 'inference-receipt.json', 'da3-base'),
        ('sfm', 'sfm-receipt.json', 'sfm'),
        ('mapanything', 'mapanything-receipt.json', 'mapanything'),
    ]:
        receipt = read_receipt(output / out_name / receipt_name)
        stage = stages[name]
        if receipt is None:
            engines[name] = {'included_in_ranking': False, 'stage': stage,
                             'reason': 'No receipt was produced; excluded from ranking, not scored as a loss.'}
            continue
        status = receipt.get('status', 'unknown')
        exclusion = ranking_exclusion(receipt, stage, [r['sha256'] for r in input_rows], output / out_name)
        included = exclusion is None
        engines[name] = {
            'included_in_ranking': included, 'status': status, 'stage': stage,
            'receiptSha256': sha(output / out_name / receipt_name),
            'settings': receipt.get('settings'),
            'device': receipt.get('deviceUsed') or receipt.get('device'),
            'elapsedSeconds': receipt.get('elapsedSeconds'),
            'peakMemory': receipt.get('peakMemory') or receipt.get('peakRssBytes'),
            'inputsSha256': [i.get('sha256') for i in receipt.get('inputs', [])],
            'registeredImages': receipt.get('registeredImages'),
            'unregisteredImages': receipt.get('unregisteredImages'),
            'trackLengthDistribution': (receipt.get('models', {}).get(receipt.get('bestModel'), {})
                                        .get('trackLengthDistribution') if receipt.get('bestModel') else None),
            'reprojectionErrorPixelsDistribution': (
                receipt.get('models', {}).get(receipt.get('bestModel'), {})
                .get('reprojectionErrorPixelsDistribution') if receipt.get('bestModel') else None),
            'observationReprojectionPixels': receipt.get('models', {}).get(
                receipt.get('bestModel'), {}).get('observationReprojectionPixels'),
            'parallaxDegrees': receipt.get('models', {}).get(
                receipt.get('bestModel'), {}).get('parallaxDegrees'),
            'diagnosticLimits': 'Null registered-model diagnostics mean unavailable, never zero error.',
            'reasonIfNotIncluded': exclusion,
        }

    common_input_sha = [row['sha256'] for row in input_rows]
    comparison = {
        'schemaVersion': 'sequence-pilot-comparison-1',
        'generatedAt': datetime.now(timezone.utc).isoformat(),
        'inputs': input_rows, 'commonInputSha256': common_input_sha,
        'runnerSha256': sha(Path(__file__)), 'python': sys.version,
        'engines': engines,
        'rankingNote': 'Engines with included_in_ranking=false (not executed, timed out, or budget-'
                       'exceeded) are excluded from any ranking, not treated as a tied or failed result.',
        'note': 'Cross-engine agreement/registration counts are diagnostics between predictions, not '
                'survey-grade accuracy; no metric scale or collision approval is established here.',
    }
    write_json(output / 'sequence-pilot-comparison.json', comparison)
    print(json.dumps({name: engine.get('status', engine.get('stage', {}).get('status'))
                      for name, engine in engines.items()}, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
