"""Bounded public KoreaToDo station gallery collection into local private storage."""

import argparse
from datetime import datetime, timezone
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import urllib.parse
import urllib.request

from PIL import Image


class Gallery(HTMLParser):
    def __init__(self):
        super().__init__()
        self.images = {}

    def handle_starttag(self, tag, attrs):
        if tag != 'img':
            return
        attrs = dict(attrs)
        for key in ['src', 'data-src']:
            url = attrs.get(key, '')
            parsed = urllib.parse.urlparse(url)
            if parsed.scheme != 'https' or parsed.netloc != 'static.wixstatic.com':
                continue
            match = re.match(r'(/media/[^/]+\.(?:jpg|jpeg|png|webp|gif))(?:/|$)', parsed.path, re.I)
            if match:
                canonical = 'https://static.wixstatic.com' + match.group(1)
                self.images.setdefault(canonical, set()).add(attrs.get('alt', ''))


def digest(value):
    return hashlib.sha256(value).hexdigest()


def collect(page, directory, limit=30, max_bytes=120*1024**2):
    directory.mkdir(parents=True, exist_ok=True)
    req = urllib.request.Request(page, headers={'User-Agent': 'CHOOGuard-public-reference-collector/0.1'})
    with urllib.request.urlopen(req, timeout=30) as response:
        html = response.read(15_000_001)
    if len(html) > 15_000_000:
        raise ValueError('Source HTML exceeds collection bound')
    (directory/'source-page.html').write_bytes(html)
    parser = Gallery()
    parser.feed(html.decode('utf-8', errors='replace'))
    # Descriptive station captions come before logos/related-place thumbnails.
    candidates = sorted(u for u in parser.images if any(
        a.startswith('Busan Station') or a.startswith('Getting to Busan Station')
        for a in parser.images[u]))
    records, hashes, total = [], {}, 0
    for index, url in enumerate(candidates[:limit]):
        filename = f'{index:02d}-' + Path(urllib.parse.urlparse(url).path).name
        target = directory/filename
        cached = list(directory.glob('*-' + Path(urllib.parse.urlparse(url).path).name))
        if not target.exists() and cached:
            target = cached[0]
            filename = target.name
        try:
            if target.exists():
                data = target.read_bytes()
            else:
                request = urllib.request.Request(url, headers={'User-Agent': 'CHOOGuard-public-reference-collector/0.1'})
                with urllib.request.urlopen(request, timeout=30) as response:
                    data = response.read(8_000_001)
            if len(data) > 8_000_000 or total+len(data) > max_bytes:
                raise ValueError('Image or total collection budget exceeded')
            checksum = digest(data)
            if checksum not in hashes:
                target.write_bytes(data)
                hashes[checksum] = filename
                total += len(data)
            with Image.open(directory/hashes[checksum]) as im:
                width, height = im.size
                exif = im.getexif()
                exif_details = exif.get_ifd(34665)
                record = {'sourcePage': page, 'imageUrl': url,
                          'captions': sorted(parser.images[url]), 'file': hashes[checksum],
                          'sha256': checksum, 'bytes': len(data), 'width': width, 'height': height,
                          'format': im.format, 'exifDateTime': exif.get(306),
                          'exifDateTimeOriginal': exif_details.get(36867),
                          'exifDateTimeDigitized': exif_details.get(36868),
                          'cameraMake': exif.get(271), 'cameraModel': exif.get(272),
                          'status': 'downloaded_not_spatially_assessed',
                          'rights': 'Publicly viewable; redistribution/derivative license not verified'}
                records.append(record)
        except Exception as error:
            records.append({'sourcePage': page, 'imageUrl': url, 'status': 'failed',
                            'error': type(error).__name__ + ': ' + str(error)[:300]})
    manifest = {'schemaVersion': 1, 'collectedAt': datetime.now(timezone.utc).isoformat(),
                'page': page, 'sourceHtmlSha256': digest(html), 'discoveredImages': len(parser.images),
                'eligibleStationImages': len(candidates),
                'uniqueDownloadedFiles': len(hashes), 'uniqueBytes': total,
                'records': records, 'scope': 'Local source collection, not reconstruction or metric acceptance'}
    (directory/'collection.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--page', default='https://www.koreatodo.com/busan-station')
    parser.add_argument('--output', type=Path, default=Path('private-data/facility-sources/photos/koreatodo'))
    parser.add_argument('--limit', type=int, default=40)
    args = parser.parse_args()
    if not 1 <= args.limit <= 40:
        raise ValueError('This bounded collector accepts 1-40 images')
    manifest = collect(args.page, args.output, args.limit)
    print(json.dumps({k: manifest[k] for k in ['discoveredImages','uniqueDownloadedFiles','uniqueBytes']}))


if __name__ == '__main__':
    main()
