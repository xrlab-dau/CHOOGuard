"""Fetch the two fixed public reference photos into ignored local storage."""

import argparse
import hashlib
import json
from pathlib import Path
import urllib.request


def main():
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=root / 'private-data/public-concourse-pilot')
    args = parser.parse_args()
    recipe = json.loads((root/'reconstruction/recipes/busan-concourse-public-pilot.json').read_text())
    args.output.mkdir(parents=True, exist_ok=True)
    for item in recipe['fixed_image_set']:
        path = args.output / (item['id'] + '.jpg')
        if path.exists():
            data = path.read_bytes()
        else:
            request = urllib.request.Request(item['image_url'], headers={'User-Agent': 'CHOOGuard-reference-review/0.1'})
            with urllib.request.urlopen(request, timeout=40) as response:
                data = response.read(10_000_001)
            if len(data) > 10_000_000:
                raise ValueError('Reference exceeds the bounded download size')
        if hashlib.sha256(data).hexdigest() != item['sha256']:
            raise ValueError('Public image changed or existing file differs; re-audit the source')
        if not path.exists():
            path.write_bytes(data)
        print(path)


if __name__ == '__main__':
    main()
