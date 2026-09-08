"""Recover local photo records and validate four held videos without network/image output.

The old collection and originals are read-only. Cached page URL/caption matching is
provenance evidence, not redistribution permission or visual/facility verification.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from PIL import Image
from collect_public_images import Gallery

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def recover_photos(directory):
    original = json.loads((directory / 'collection.json').read_text())
    gallery = Gallery()
    gallery.feed((directory / 'source-page.html').read_text())
    recorded = {row.get('file') for row in original['records']}
    missing = sorted(p for p in directory.iterdir()
                     if p.suffix.lower() in ('.jpg', '.jpeg', '.png', '.gif', '.webp')
                     and p.name not in recorded)
    rows = []
    for path in missing:
        matches = [url for url in gallery.images if path.name.endswith(Path(url).name)]
        with Image.open(path) as image:
            width, height = image.size
            image.verify()
        rows.append(dict(file=path.name, sha256=sha(path), bytes=path.stat().st_size,
                         width=width, height=height, sourcePage=original['page'],
                         imageUrl=matches[0] if len(matches) == 1 else None,
                         captions=sorted(gallery.images[matches[0]]) if len(matches) == 1 else [],
                         status='cached_page_bound' if len(matches) == 1 else 'source_url_unresolved',
                         rights='Public viewing is not redistribution/derivative approval',
                         visualReview='not_performed'))
    return dict(originalCollectionSha256=sha(directory / 'collection.json'),
                cachedSourceHtmlSha256=sha(directory / 'source-page.html'),
                originalRecordCount=len(original['records']), recoveredCount=len(rows), records=rows)


def validate_video(path):
    before = sha(path)
    result = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-select_streams', 'v:0',
        '-show_entries', 'stream=width,height,duration,time_base:format=duration', '-of', 'json', str(path)],
        text=True, timeout=30))
    decode = subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin',
        '-threads', '2', '-i', str(path), '-frames:v', '90', '-an', '-f', 'null', '-'],
        capture_output=True, text=True, timeout=60)
    return dict(videoId=path.parent.name, sha256=before, bytes=path.stat().st_size, probe=result,
                decodeFirst90FramesExitCode=decode.returncode, sourceUnchanged=sha(path) == before,
                decodeError=decode.stderr[-500:], fullDecode=False, visualReview='not_performed')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('Refuse to overwrite inventory evidence')
    source = ROOT / 'private-data/facility-sources'
    photos = recover_photos(source / 'photos/koreatodo')
    videos = [validate_video(source / 'videos' / name / 'video.mp4')
              for name in ('RY2qvE0Tugk', 'DuD0MezcVjQ', '39PANJWghAk', 'Gv9hCGseu9o')]
    report = dict(schemaVersion=1, classification='PUBLIC_PROJECT_EVIDENCE', photos=photos,
                  videos=videos, newVideosAcquired=0, networkCalls=0,
                  limits='Hashes and decoder metadata only. No visual/privacy, surveyed layout or publication acceptance.')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(dict(recoveredPhotos=photos['recoveredCount'], heldVideos=len(videos),
                         unresolvedPhotoUrls=sum(r['status'] != 'cached_page_bound' for r in photos['records']))))


if __name__ == '__main__':
    main()
