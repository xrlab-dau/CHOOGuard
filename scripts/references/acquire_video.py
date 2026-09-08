#!/usr/bin/env python3
"""Acquire one public YouTube reference locally with a sanitized public receipt.

Run in .local/references/video-venv (yt-dlp 2026.08.19). Full extractor metadata,
logs and media stay in ignored private-data. No cookies or remote JS components.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time
from urllib.parse import parse_qs, urlparse

ROOT = Path(__file__).resolve().parents[2]
PRIVATE = ROOT / 'private-data/facility-sources/videos'
REPORT = ROOT / '.planning/2026-09-07-facility-source-collection/video-acquisition.json'
BUDGET = 1024 ** 3


def video_id(url: str) -> str:
    parsed = urlparse(url)
    if (parsed.scheme != 'https' or parsed.hostname not in {'youtube.com', 'www.youtube.com'}
            or parsed.username or parsed.password or parsed.path != '/watch'):
        raise ValueError('Use a public HTTPS YouTube watch URL without credentials.')
    candidate = parse_qs(parsed.query).get('v', [''])[0]
    if not re.fullmatch(r'[A-Za-z0-9_-]{11}', candidate):
        raise ValueError('Expected one 11-character public video ID.')
    return candidate


def safe_text(value) -> str:
    return re.sub(r'https?://\S+', '[URL omitted]', str(value or ''))


def public_metadata(info: dict) -> dict:
    identifier = info['id']
    video_id('https://www.youtube.com/watch?v=' + identifier)
    date = info.get('upload_date')
    date = f'{date[:4]}-{date[4:6]}-{date[6:]}' if date and len(date) == 8 else None
    return {'videoId': identifier, 'watchUrl': 'https://www.youtube.com/watch?v=' + identifier,
            'title': safe_text(info.get('title')), 'uploader': safe_text(info.get('uploader')),
            'uploadDate': date, 'recordingDate': None,
            'recordingDateStatus': 'not established by extractor; upload date is not capture date',
            'durationSeconds': info.get('duration'), 'license': safe_text(info.get('license')) or 'unknown',
            'licenseInterpretation': 'field only; public availability is not reuse authorization',
            'chapters': [{'startSeconds': c.get('start_time'), 'endSeconds': c.get('end_time'),
                          'title': safe_text(c.get('title'))} for c in info.get('chapters') or []]}


def select_format(formats: list[dict], available_bytes: int) -> dict:
    candidates = []
    for item in formats:
        size = item.get('filesize') or item.get('filesize_approx')
        if (item.get('has_drm') or item.get('vcodec') in {None, 'none'}
                or item.get('acodec') != 'none' or not item.get('height')
                or item['height'] > 1080 or not size or size > available_bytes):
            continue
        candidates.append(item)
    if not candidates:
        raise ValueError('No bounded non-DRM video-only ≤1080p format with known size.')
    return max(candidates, key=lambda f: (f['height'],
               f.get('vcodec', '').startswith(('avc', 'h264')), f.get('fps') or 0))


def sha(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def local_bytes() -> int:
    return sum(p.stat().st_size for p in PRIVATE.rglob('*') if p.is_file())


def write_json(path: Path, content: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(content, ensure_ascii=False, indent=2) + '\n')
    os.replace(temporary, path)


def run_private(command: list[str], stdout: Path, stderr: Path, timeout: int) -> dict:
    start = time.monotonic()
    stopped = None
    with stdout.open('w') as out, stderr.open('w') as err:
        process = subprocess.Popen(command, stdout=out, stderr=err, start_new_session=True)
        while process.poll() is None:
            if time.monotonic() - start > timeout:
                stopped = 'time_budget_exceeded'
            elif local_bytes() > BUDGET - 8 * 1024 ** 2:
                stopped = 'storage_budget_exceeded'
            elif shutil.disk_usage(PRIVATE).free < 10 * 1024 ** 3:
                stopped = 'minimum_free_space_reached'
            if stopped:
                os.killpg(process.pid, signal.SIGTERM)
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait()
                break
            time.sleep(.2)
    return {'exitCode': process.returncode, 'elapsedSeconds': time.monotonic() - start,
            'stoppedReason': stopped, 'stderr': safe_text(stderr.read_text(errors='replace'))[-4000:]}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('url')
    parser.add_argument('--use-existing-metadata', action='store_true',
                        help='Reuse this lane\'s prior CLI raw-info.json, without re-extraction.')
    args = parser.parse_args()
    identifier = video_id(args.url)
    version = importlib.metadata.version('yt-dlp')
    if version != '2026.8.19':
        raise ValueError('Use the isolated yt-dlp 2026.8.19 environment for this acquisition.')
    destination = PRIVATE / identifier
    destination.mkdir(parents=True, exist_ok=True)
    if any(destination.glob('video.*')):
        raise ValueError('Media/partial output already exists; inspect it before any deliberate retry.')
    if shutil.disk_usage(PRIVATE).free < 11 * 1024 ** 3:
        raise ValueError('Need at least 11 GiB free before starting this 1 GiB lane.')
    raw_info = destination / 'raw-info.json'
    attempt = {'videoId': identifier, 'watchUrl': 'https://www.youtube.com/watch?v=' + identifier,
               'startedAt': datetime.now(timezone.utc).isoformat(), 'status': 'running',
               'toolVersion': version, 'runnerSha256': sha(Path(__file__)),
               'cookiesUsed': False, 'accountCredentialsUsed': False,
               'remoteJavascriptComponents': False, 'imageUploads': 0,
               'budgetBytes': BUDGET, 'audioDownloaded': False,
               'metadataMode': 'reuse prior CLI extraction' if args.use_existing_metadata else 'extract',
               'stages': []}
    report = json.loads(REPORT.read_text()) if REPORT.exists() else {'schemaVersion': 1, 'attempts': []}
    report['attempts'].append(attempt)
    write_json(REPORT, report)
    base = [sys.executable, '-m', 'yt_dlp', '--ignore-config', '--no-playlist',
            '--no-cookies', '--no-cookies-from-browser', '--no-remote-components',
            '--js-runtimes', 'deno', '--socket-timeout', '20', '--retries', '1',
            '--extractor-retries', '1', '--fragment-retries', '1', '--no-progress']
    try:
        if not args.use_existing_metadata:
            stage = run_private(base + ['--skip-download', '--dump-single-json', attempt['watchUrl']],
                                raw_info, destination / 'metadata.log', 90)
            attempt['stages'].append({'stage': 'metadata', **stage})
            if stage['exitCode']:
                raise RuntimeError('Public metadata extraction failed; see sanitized stage error.')
        info = json.loads(raw_info.read_text())
        if info.get('id') != identifier or info.get('live_status') not in {None, 'not_live', 'was_live'}:
            raise ValueError('Metadata video ID/live status does not match this bounded archival request.')
        attempt['source'] = public_metadata(info)
        available = min(BUDGET - local_bytes() - 32 * 1024 ** 2, 900 * 1024 ** 2)
        selected = select_format(info.get('formats', []), available)
        attempt['selectedFormat'] = {key: selected.get(key) for key in (
            'format_id', 'ext', 'width', 'height', 'fps', 'vcodec', 'acodec',
            'filesize', 'filesize_approx', 'protocol')}
        write_json(REPORT, report)
        stage = run_private(base + ['--load-info-json', str(raw_info), '--no-simulate',
                            '--format', selected['format_id'], '--max-filesize', str(available),
                            '--no-overwrites', '--output', str(destination / 'video.%(ext)s')],
                            destination / 'download.log', destination / 'download-errors.log', 900)
        attempt['stages'].append({'stage': 'download', **stage})
        if stage['exitCode']:
            raise RuntimeError('Public media download failed; see sanitized stage error.')
        media = destination / ('video.' + selected['ext'])
        if not media.is_file() or not media.stat().st_size:
            raise ValueError('Extractor exited without a complete media file.')
        probe = subprocess.run(['ffprobe', '-v', 'error', '-show_streams', '-show_format',
                                '-of', 'json', str(media)], text=True, capture_output=True,
                               check=True, timeout=30)
        probe_info = json.loads(probe.stdout)
        write_json(destination / 'ffprobe.json', probe_info)
        streams = [s for s in probe_info['streams'] if s['codec_type'] == 'video']
        if len(streams) != 1 or streams[0]['height'] > 1080:
            raise ValueError('Actual video stream does not satisfy the bounded format contract.')
        duration = float(probe_info['format']['duration'])
        if abs(duration - float(info['duration'])) > 2:
            raise ValueError('Downloaded full-video duration differs from source metadata by >2 seconds.')
        attempt['media'] = {'path': str(media.relative_to(ROOT)), 'bytes': media.stat().st_size,
                            'sha256': sha(media), 'durationSeconds': duration,
                            'container': probe_info['format']['format_name'],
                            'codec': streams[0]['codec_name'], 'width': streams[0]['width'],
                            'height': streams[0]['height'], 'frameRate': streams[0]['avg_frame_rate'],
                            'audioStreams': sum(s['codec_type'] == 'audio' for s in probe_info['streams'])}
        attempt['rawMetadata'] = {'privatePath': str(raw_info.relative_to(ROOT)),
                                  'sha256': sha(raw_info), 'bytes': raw_info.stat().st_size,
                                  'publicationAllowed': False}
        attempt['dependencies'] = {d.metadata['Name']: d.version for d in importlib.metadata.distributions()}
        attempt['ffmpeg'] = subprocess.check_output(['ffmpeg', '-version'], text=True).splitlines()[0]
        attempt['deno'] = subprocess.check_output(['deno', '--version'], text=True).splitlines()[0]
        attempt['status'] = 'downloaded_and_ffprobe_verified'
        attempt['frameQualityReviewed'] = False
        attempt['cutsAndOverlapReviewed'] = False
        attempt['metricGeometryValidated'] = False
    except Exception as error:
        attempt['status'] = 'failed'
        attempt['error'] = safe_text(f'{type(error).__name__}: {error}')
        return 1
    finally:
        attempt['finishedAt'] = datetime.now(timezone.utc).isoformat()
        attempt['totalPrivateVideoBytes'] = local_bytes()
        write_json(REPORT, report)
        print(json.dumps({'status': attempt['status'], 'receipt': str(REPORT.relative_to(ROOT)),
                          'privateBytes': attempt['totalPrivateVideoBytes']}))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
