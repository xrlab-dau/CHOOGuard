raise SystemExit("Superseded by novelty-audited compact source-ledger.json; do not rerun acquisition builder.")
import hashlib
import json
from pathlib import Path
from urllib.parse import unquote, urlparse
from PIL import Image

base = Path(__file__).resolve().parent
output = base.parents[1] / 'output' / base.name

def pin(path):
    data = path.read_bytes()
    result = {'path': str(path), 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)}
    if path.suffix.lower() in ['.jpg', '.jpeg', '.png']:
        with Image.open(path) as image:
            result['dimensions'] = list(image.size)
            image.verify()
    return result

def save(name, data):
    (output / name).write_text(json.dumps(data, ensure_ascii=False, indent=2))

queries = []
for n in range(1, 13):
    path = base / f'q{n:02d}.json'
    if path.exists():
        item = json.loads(path.read_text())
        queries.append({'id': item['id'], 'query': item['query'], 'result_capture': pin(path), 'url': item['url'], 'acquired_at': item['acquired_at']})
    elif n == 7:
        queries.append({'id': 'q07', 'query': '부산역 증축 감리실무사례 2012', 'result_capture': None, 'status': 'Search tab opened in original fetch-engineering attempt; title recorded, result DOM not retained. Exact primary PDF subsequently downloaded and reviewed.'})
assert len(queries) == 12
save('query-ledger.json', {'initial_bounded_query_count': 12, 'queries': queries})

originals = []
for path in sorted(base.glob('*.json')):
    item = json.loads(path.read_text())
    if 'filename' not in item:
        continue
    target = base / item['filename']
    if not target.exists():
        continue
    entry = {**item, **pin(target), 'classification': 'directly_supported', 'license': 'Internal reference only; no redistribution or texture rights inferred'}
    entry['direct_visual_review'] = True
    if item['filename'].endswith('.pdf'):
        entry.update({'building_scope': 'Historical2009/2010 phase2 station extension plus retained mainbuilding', 'date_scope': 'Construction Management News2012Mar-Apr; current asbuilt survey not established', 'reviewed_original_pdf_pages': [1, 2, 3, 4]})
    else:
        entry.update({'building_scope': 'Busan KTX station mainbuilding5F meeting-room wing', 'floor_identity': 'Explicit5F destination in corresponding own-room modal', 'date_scope': 'Current served2026-10-10; photo paths20220506 or20250327, guide filename20260921; capture/drawing date unverified'})
        if item.get('alt') == '회의실 약도' or item.get('id') == '512-guide':
            entry['direct_visual_review'] = 'Canonical guide directly reviewed; variants verified byte-identical'
    originals.append(entry)

reused = []
asset_base = base.parents[3] / 'asset-library' / 'research-public' / '2026-10-09'
live = json.loads((base / 'kgnwrite-live.json').read_text())
live_paths = {unquote(urlparse(im['src']).path) for im in live['images']}
for receipt_name in ['current-guide-3f-acquisition-receipts.json', 'current-guide-acquisition-receipts.json']:
    receipt_path = asset_base / 'floor3-guide' / receipt_name
    for item in json.loads(receipt_path.read_text())['receipts']:
        path = asset_base / 'floor3-guide' / item['path']
        entry = {**item, **pin(path), 'reused_existing_original': True, 'direct_visual_review_this_lane': True,
                 'source_context_refreshed': pin(base / 'kgnwrite-live.json'), 'previous_receipt': pin(receipt_path),
                 'live_DOM_image_path_present': unquote(urlparse(item['dom_original_url']).path) in live_paths}
        assert entry['sha256'] == item['sha256']
        if 'image6-' in item['path']:
            entry.update({'classification': 'directly_supported appearance; floor inferred by storefront-to-board name match', 'floor_region': '1F restaurant corridor unit13채선당 by exact visible name matching photographed operator board', 'conflicts_with_previous_receipt': 'Prior receipt calls2F. Current original wholeboard lists exact tenant only1Funit13; no calibratedXY/camera claim.'})
        elif 'image3-' in item['path']:
            entry.update({'classification': 'directly_supported', 'floor_region': '1Fguide detail, not3F'})
        else:
            entry.update({'classification': 'directly_supported', 'floor_region': 'Separate3F restaurant L-wing operator board; not ambiguous ad3Fconcourse'})
        reused.append(entry)

psh_receipts = base.parents[0] / 'current-upper-floor-20261009' / 'source-receipts.json'
psh = json.loads(psh_receipts.read_text())
for name in ['pshbadgirl-224373114211-01.jpg', 'pshbadgirl-224373114211-02.jpg']:
    path = asset_base / 'current-upper-floor' / name
    reused.append({**pin(path), 'source_page': 'https://m.blog.naver.com/pshbadgirl/224373114211',
                   'reused_existing_original': True, 'direct_visual_review_this_lane': True,
                   'source_context_refreshed': pin(base / 'pshbadgirl-live.json'), 'previous_receipt': pin(psh_receipts),
                   'publication': '2026-08-09 18:21', 'capture_date': None, 'classification': 'directly_supported local tenant envelope/appearance',
                   'floor_region': '3FBusanok; explicit floor in author text. No metriccamera or worldXY.'})

pdf_pin = pin(base / 'ekacem-2012-busan-extension.pdf')
derived = []
for path in sorted(base.glob('ekacem-p*-xref*.*')):
    xref = int(path.stem.split('xref')[1])
    derived.append({**pin(path), 'derived_from': pdf_pin, 'method': 'PyMuPDF extract_image; unchanged embedded image bytes',
                    'direct_native_image_review': xref in [11, 12, 13, 14, 15, 16], 'reviewed_in_parent_page': True})

packet = json.loads((output / 'official-5f-source-packet.json').read_text())
packet['context_file_pins'] = [pin(base / name) for name in ['official-room.json', 'room512-detail.json', 'room530-detail.json', 'room531-detail.json', 'room532-detail.json', 'room533-detail.json']]
save('official-5f-source-packet.json', packet)

section_packet = {
    'schema': 'chooguard.command-center.historical-section-source.v1', 'classification': 'directly_supported historical relations',
    'reference_only': True, 'original_pdf': pdf_pin, 'source_url': originals[0].get('url') if originals else None,
    'title': '경부고속철도2단계 부산역사증축공사 감리실무사례', 'author': '한상진, (주)혜원까치종합건축사사무소 전무',
    'publication': '건설감리2012March-April; printedpages38-43', 'construction_scope': '2009start/2010opening; retained existing station vs extension',
    'diagrams': [
        {'role': 'before/after sanitary and mezzanine facilities plan', 'pdf_page': 3, 'printed_page': 40, 'native_image': pin(base / 'ekacem-p03-image05-xref15.jpeg')},
        {'role': 'before/after structural section and axonometric', 'pdf_page': 3, 'printed_page': 40, 'native_image': pin(base / 'ekacem-p03-image06-xref16.jpeg')},
    ],
    'supported_relations': ['Existing mainbuilding4F/5F offices separated from added station volume', 'Revised design removes original lightpillars/transferbeams', 'Roof openings replaced by lightducts', 'Distributed WC/HVAC groups centralized; mezzanine facilities concentrated', 'Retained connecting passages reused with partial ES/EL interventions'],
    'whole_project_summary_only': {'storeys': 'B1/5aboveground', 'maximum_height_m': 31.85, 'pdf_page': 2, 'printed_page': 39},
    'not_supported': ['Current office room coordinates', 'Current floor heights', 'Full4F/5Fperimeter', 'Direct model registration', 'Applying original proposal left diagram as built'],
}
section_packet['source_url'] = json.loads((base / 'engineering-download.json').read_text())['url']
save('historical-section-source-packet.json', section_packet)

ledger = {'schema': 'chooguard.command-center.source-ledger.v1', 'browser': 'Aside CLI repl only; no remote agents', 'new_originals': originals,
          'reused_originals': reused, 'derived_embedded_images': derived,
          'scope_limits': ['No4Ffloorplan found within12queries; not proof noneexists', '5Fpartial guide only; no entirefloor completion', 'No physical scale/north/height/worldXY established', 'No current5Fmodel owner verified', 'NoUnity/Assets/canonical edits; no paidjobs/uploads/publishing'],
          'methods_primary_context': [pin(base / 'method-raster-vector.json'), pin(base / 'method-floornet.json')]}
save('source-ledger.json', ledger)
print('sealed', len(originals), 'new original files;', len(reused), 'reused originals;', len(derived), 'embedded-image derivatives;', len(queries), 'queries')
print('livekgnwriteDOMimages', len(live['images']), 'alloriginalpaths', all(x.get('live_DOM_image_path_present', True) for x in reused))
