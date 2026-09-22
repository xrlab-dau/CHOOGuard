#!/usr/bin/env python3
"""acquisition-receipt.json 으로부터 Poly Haven 에셋을 재취득한다.

저장소는 바이너리를 추적하지 않고 영수증만 추적한다. 이 스크립트가 "재현 가능"을
말뿐이 아니게 만든다. 인증·토큰 없이 동작한다 (Poly Haven CC0).

    python3 asset-library/models-materials/downloads/polyhaven/restore.py
    python3 .../restore.py --verify      # 받지 않고 해시만 대조
    python3 .../restore.py --slug korean_fire_extinguisher_01

receipt 의 sha256 과 대조하므로 원본이 바뀌면 MISMATCH 로 드러난다.
"""
import argparse
import hashlib
import json
import os
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
RECEIPT = os.path.join(HERE, 'acquisition-receipt.json')
UA = {'User-Agent': 'Mozilla/5.0'}


def sha256(path, buf=1 << 20):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        while chunk := f.read(buf):
            h.update(chunk)
    return h.hexdigest()


def fetch(url, timeout=180):
    req = urllib.request.Request(url, headers=UA)
    return urllib.request.urlopen(req, timeout=timeout).read()


def resolve_urls(item):
    """영수증에는 파일별 URL 대신 API 메타데이터 URL 만 둔다.
    (Poly Haven 의 CDN 경로가 바뀌어도 API 가 현재 URL 을 알려주기 때문.)"""
    meta = json.loads(fetch(item['api_metadata_url'], 30))
    res = item['resolution']
    out = {}
    # 지오메트리: FBX
    fbx = (meta.get('fbx') or {}).get(res)
    if fbx:
        node = fbx.get('fbx') or list(fbx.values())[0]
        out[os.path.basename(node['url'])] = node['url']
    # 텍스처: glTF 번들 쪽 JPG (FBX 번들의 EXR/PNG 는 arm 맵과 중복이라 쓰지 않는다)
    gltf = (meta.get('gltf') or {}).get(res)
    if gltf:
        node = gltf.get('gltf') or list(gltf.values())[0]
        for rel, v in (node.get('include') or {}).items():
            if rel.lower().endswith(('.jpg', '.jpeg')):
                out[rel] = v['url']
    return out


def restore_item(item, verify_only):
    slug = item['slug']
    base = os.path.join(HERE, item['group'], slug)
    expected = {e['path'].split('/', 2)[-1]: e for e in item['files']}
    ok = miss = bad = got = 0
    urls = None
    for rel, e in expected.items():
        dst = os.path.join(base, rel)
        if os.path.exists(dst) and os.path.getsize(dst) == e['bytes']:
            if sha256(dst) == e['sha256']:
                ok += 1
                continue
            bad += 1
            if verify_only:
                continue
        elif verify_only:
            miss += 1
            continue
        if urls is None:
            try:
                urls = resolve_urls(item)
            except Exception as exc:
                return slug, f'META_FAIL {exc}', 0, 0, 0, 0
        url = urls.get(rel) or urls.get(os.path.basename(rel))
        if not url:
            miss += 1
            continue
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        try:
            open(dst, 'wb').write(fetch(url))
            got += 1
        except Exception:
            miss += 1
    return slug, 'OK', ok, got, miss, bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--verify', action='store_true', help='받지 않고 해시만 대조')
    ap.add_argument('--slug', help='이 에셋만 처리')
    ap.add_argument('--jobs', type=int, default=6)
    a = ap.parse_args()

    if not os.path.exists(RECEIPT):
        sys.exit(f'영수증 없음: {RECEIPT}')
    r = json.load(open(RECEIPT, encoding='utf-8'))
    items = [i for i in r['items'] if not a.slug or i['slug'] == a.slug]
    if not items:
        sys.exit(f'해당 slug 없음: {a.slug}')

    print(f"{r['source']['name']} · {r['source']['license']}")
    print(f"대상 {len(items)}건 · {'검증만' if a.verify else '재취득'}\n")

    with ThreadPoolExecutor(max_workers=a.jobs) as ex:
        results = list(ex.map(lambda i: restore_item(i, a.verify), items))

    t_ok = t_got = t_miss = t_bad = 0
    for slug, status, ok, got, miss, bad in sorted(results):
        if status != 'OK':
            print(f'  ! {slug:<32} {status}')
            continue
        t_ok += ok; t_got += got; t_miss += miss; t_bad += bad
        flag = '  ' if not (miss or bad) else ' !'
        print(f'{flag}{slug:<32} 일치={ok:<3} 받음={got:<3} 없음={miss:<3} 불일치={bad}')
    print(f'\n일치 {t_ok} · 받음 {t_got} · 없음 {t_miss} · 해시불일치 {t_bad}')
    sys.exit(1 if (t_miss or t_bad) else 0)


if __name__ == '__main__':
    main()
