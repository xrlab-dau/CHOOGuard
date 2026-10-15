"""Seal this worker's bounded public-source research; never touch scene files."""
from pathlib import Path
import base64
import datetime
import hashlib
import json
import re
import urllib.parse

import olefile

E = Path(__file__).resolve().parent
O = E.parent / '20261010-missing-floorplans-deep-research'
now = datetime.datetime.now(datetime.timezone.utc).isoformat()


def read(name):
    return json.loads((E / name).read_text())


def write(name, data):
    (E / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')


specs = [
    ('official-4f-supervision-design-2024', 'hwp', '4F construction management',
     'c3e14c75b003eb951f5306f62a26d91116ddc78bdb8fa9cfc598bf1abc9ec79a',
     '9242205-00', '2024-01-25', 'MAIN / 4F staff renovation',
     'Management specification; no floor plan, section or room geometry.'),
    ('official-4f-electric-supervision-design-2024', 'hwp', '4F electrical supervision',
     'b57619749c87a6e37d8dc6666fd1bc59751d630033b5c935e4617a9286e2aabb',
     '9243757-00', '2024-04-18', 'MAIN / 4F staff renovation',
     'Management specification; embedded arrow, schedule and cover, no architectural drawing.'),
    ('official-5f-telecom-design-2019', 'xls', '5F telecom staff renovation',
     'c5d351cb6ba2078727f34ae43b4d9f8163076d8ebf4ddb136104aec956f32fd3',
     '20191134046-000', '2019-11-22', 'MAIN / 5F telecom team, 106.00 square metres',
     'Scope and nine-title annex index; actual nine annex drawings absent.'),
    ('official-main-fireproof-2019-combined', 'xls', 'MAIN fireproofing combined design',
     '78e5993a20ee74dc5361e9e072d00d7cc66304342211c6ee679ecaf49aac7eb7',
     '20191036544-000', '2019-10-29', 'MAIN body / roof / concourse upper slab',
     'Scope, costing and eleven-title annex index; actual eleven drawings absent.'),
    ('official-main-fireproof-2019-total', 'xls', 'MAIN fireproofing total design',
     '9d10b323074042938f849cb73a427bc5e476b23909481b27e01eabb09e1fd46d',
     '20191036544-000', '2019-10-29', 'MAIN body / roof / concourse upper slab',
     'Same eleven-title annex index; actual eleven drawings absent.'),
    ('official-main-fireproof-2019-first', 'xls', 'MAIN fireproofing first-stage design',
     '9baf1d7d0dd3039137b14a80f186d818d3689bf97e51f803d1db9bc1f705a306',
     '20191036544-000', '2019-10-29', 'MAIN body / roof / concourse upper slab',
     'Same eleven-title annex index; actual eleven drawings absent.'),
    ('official-main-fireproof-2019-notice', 'hwp', 'MAIN fireproofing public notice',
     '92e5006c4ab65d73e354389781b24c8b786d37ec56b9b4bc32c619cac311b6d6',
     '20191036544-000', '2019-10-29', 'MAIN body / administrative notice',
     'Design-document inspection directed to Busan Architecture Office; no additional public drawing URL.'),
]

old_text = (O / 'new-blueprint-source-ledger.json').read_text()
old_hashes = set(re.findall(r'\b[0-9a-f]{64}\b', old_text))
sources, checks = [], []
for stem, ext, title, expected, notice, published, region, limit in specs:
    receipt = read(stem + '.json')
    original = E / (stem + '.' + ext)
    payload = original.read_bytes()
    digest = hashlib.sha256(payload).hexdigest()
    inspect_name = ('5f-telecom-xls-inspection.json' if stem == 'official-5f-telecom-design-2019'
                    else stem + '-inspection.json')
    inspection = read(inspect_name)
    check = {
        'id': stem, 'sha256': digest, 'bytes': len(payload),
        'expected_hash_matches': digest == expected,
        'inspection_hash_matches': digest == inspection['sha256'],
        'receipt_bytes_match': len(payload) == receipt['bytes'],
        'http_status_200': receipt['status'] == 200,
        'valid_ole_container': olefile.isOleFile(original),
        'not_an_excluded_prior_source_hash': digest not in old_hashes,
    }
    if 'public_file_row' in receipt:
        row = receipt['public_file_row']
        data = json.loads(base64.b64decode(urllib.parse.parse_qs(
            urllib.parse.urlparse(receipt['url']).query)['data'][0]))
        check['observed_public_file_identifiers_match'] = (
            data['untyAtchFileNo'] == row['untyAtchFileNo']
            and data['atchFileSqno'] == row['atchFileSqno'])
        check['all_session_fields_null'] = all(v is None for k, v in data.items()
                                               if k.startswith('sess'))
        check['observed_public_file_size_matches'] = len(payload) == row['fileSz']
    check['pass'] = all(v for k, v in check.items() if isinstance(v, bool))
    assert check['pass'], check
    checks.append(check)
    source = {
        'id': stem, 'title': title, 'path': str(original), 'sha256': digest,
        'bytes': len(payload), 'notice': notice, 'publication_date': published,
        'acquired_at': receipt['acquired_at'], 'source_page': receipt['source_page'],
        'download_url': receipt['url'], 'receipt': stem + '.json',
        'inspection': inspect_name, 'region_and_floor': region,
        'evidence_status': 'directly_supported',
        'supported_claim': limit, 'actual_architectural_drawing_pages_acquired': 0,
        'plan_registration': None, 'world_storey_y': None,
        'current_as_built_status': 'Not established by a tender design or management specification.',
    }
    if ext == 'xls':
        source['worksheets'] = len(inspection['sheets'])
        source['embedded_image_pixels'] = [i['pixels'] for i in inspection['images']]
    else:
        source['extracted_image_count_including_preview'] = inspection['image_count']
    sources.append(source)

assert len({s['sha256'] for s in sources}) == len(sources)
fire = read('official-main-fireproof-2019-combined-inspection.json')
fire_sheets = {s['name']: s for s in fire['sheets']}
annex = [c for c in fire_sheets['도면목록']['cells']
         if c.get('col') == 2 and 3 <= c.get('row', 0) <= 13]
assert len(annex) == 11
fire_summary = {
    'notice': '20191036544-000', 'publication_date': '2019-10-29',
    'project_codes': ['KR-CNA-19-005', 'KR-CND-19-068'],
    'region': 'MAIN body, not MH and not a staff-room floor plan',
    'scope_cells': fire_sheets['개요']['cells'],
    'annex_index': {'sheet': '도면목록', 'cells': 'B3:B13', 'titles': annex},
    'actual_annex_drawing_pages_acquired': 0,
    'review': {
        'combined_workbook_worksheets': 27, 'total_workbook_worksheets': 23,
        'first_stage_workbook_worksheets': 23,
        'each_workbook_actual_embedded_images': 'One 61x37 logo; no plan image.',
        'combined_rendered_pdf_pages': 88, 'drawing_list_pdf_page': 77,
        'drawing_cover_pdf_page': 75,
        'direct_visual_review': ['main-fireproof-readable-p077.png',
                                 'main-fireproof-all-pages-001-044.jpg',
                                 'main-fireproof-all-pages-045-088.jpg',
                                 'official-main-fireproof-2019-notice-images-contact-sheet.jpg'],
        'source_authority': 'Original XLS hashes and sheet/cell pins; converted pagination is derivative.',
    },
    'notice_design_inspection_pin': 'official-main-fireproof-2019-notice-extracted/body-text.txt:12-14',
    'notice_design_inspection_result': 'Physical inspection at Busan Architecture Office; no new public annex URL.',
    'height_status': 'No section geometry, EL, storey height or world Y acquired.',
    'claim_guard': 'Eleven section/plan titles do not constitute eleven acquired drawings.',
}
write('main-fireproof-scope-and-missing-annex.json', fire_summary)

queries = []
for path in sorted(E.glob('s[0-9][0-9].json')):
    d = json.loads(path.read_text())
    status = 'insufficient'
    lane = 'No additional qualified MAIN upper-staff drawing identified.'
    if path.stem in ['s05', 's13']:
        status, lane = 'directly_supported', 'Lead to 2019 5F telecom public design workbook; nine annex drawings absent.'
    elif path.stem in ['s15', 's20']:
        status, lane = 'directly_supported', 'Lead to 2019 MAIN fireproofing notice; three design workbooks, eleven annex drawings absent.'
    elif path.stem == 's17':
        status, lane = 'near_match_only', 'Station-history ebook snippet; no exact current MAIN upper-staff plan.'
    queries.append({'receipt': path.name, 'query': d['query'],
                    'url': d.get('url'), 'acquired_at': d.get('acquired_at'),
                    'classification': status, 'result_limit': lane})
assert len(queries) == 21
annual = read('3f-annual-search-coverage.json')
assert len(annual['queries']) == 8
assert all(q['result_count_text'] == '전체0건' and not q['validation_dialogs']
           for q in annual['queries'])

failures = [
    {'lane': 'MAIN 3F employee space', 'classification': 'insufficient',
     'evidence': ['s01.json', 's06.json', 's15.json', 's18.json', '3f-annual-search-coverage.json'],
     'result': 'No new qualified MAIN 3F staff plan. Exact title query 부산역 3층 returned zero in eight valid annual G2B windows 2019–2026.',
     'guard': 'Does not cover every spelling, body text, archive or other office name. Existing reviewed tenant plans are MH public 3F, not MAIN staff 3F.'},
    {'lane': 'MAIN 4F staff architecture annexes', 'classification': 'insufficient',
     'evidence': ['official-4f-supervision-design-2024-inspection.json', 'official-4f-electric-supervision-design-2024-inspection.json'],
     'result': 'Both new 2024 supervision sources are management specifications; no new plan/section geometry.',
     'guard': 'Existing 2023 thirty-room quantity evidence and absent thirty-five drawing sheets remain the prior lane; no reacquisition.'},
    {'lane': 'MAIN 5F telecom nine annex drawings', 'classification': 'contradicts_premise',
     'evidence': ['5f-telecom-scope-and-missing-annex.json', '5f-telecom-original-embedded-images.json', '5f-telecom-rendered-pdf-review-index.json'],
     'result': '2019 source establishes 106.00m² staff telecom renovation scope, with nine named annex titles. Actual nine drawing pages absent.',
     'guard': 'No spatial correspondence to the 2018 P101 partial plan or current built completion established.'},
    {'lane': 'MAIN fireproofing eleven plan/section annexes', 'classification': 'contradicts_premise',
     'evidence': ['main-fireproof-scope-and-missing-annex.json', 'g2b-main-fireproof-2019-files.json'],
     'result': 'All three actual public design XLS files inspected; eleven annex titles only, no acquired sections. Public notice directs design inspection to an office.',
     'guard': 'No employee/private access and no contact made. No height or registration control extracted from titles.'},
    {'lane': 'Information disclosure status', 'classification': 'directly_supported',
     'evidence': ['official-disclosure2025-actual-xhtml.json'],
     'result': 'Actual row280: request15401541, 부산역 층별 평면도 요청, requested2025-11-07, 비공개 decision2025-11-10.',
     'guard': 'This individual request outcome is not proof that all plans are unavailable or that public tender drawings are prohibited.'},
]
procedural = [
    {'issue': 'Old Aside session daemon/auth metadata errors', 'resolution': 'Fresh Aside REPL succeeded; no account, application or config changes; failed attempt counted as no acquisition.'},
    {'issue': 'G2B multi-year date search invalid', 'resolution': 'Actual dialog maximum one year; default rows excluded; replaced by eight valid annual windows.'},
    {'issue': 'Linked-agency G2B attachment list empty', 'resolution': 'Followed actual observed public Korail notice link; public attachments acquired. Zero G2B local files did not prove zero source files.'},
    {'issue': 'BIFF image groups split across records', 'resolution': 'Combined MSODRAWINGGROUP records; 5F source has two original images, cover and logo; no nine plan images.'},
    {'issue': 'Initial XLS PDF missing Korean glyphs', 'resolution': 'Own Fontconfig cache/local fonts and isolated macro-disabled LibreOffice profile; final 5F PDF124pages, original sheet/cell authority retained.'},
    {'issue': 'Search snippets and AI overviews overstate absence', 'resolution': 'Only actual primary source contents support floor, region and availability claims; false no4F/all-confidential generalizations rejected.'},
]
write('search-and-failure-ledger.json', {
    'sealed_at': now, 'network_method': 'Aside CLI browser only',
    'excluded_prior_queries_receipt': 'excluded-prior-query-list.json',
    'new_search_queries': queries, 'valid_3f_annual_searches': annual,
    'bounded_closed_lanes': failures, 'procedural_corrections': procedural,
    'stop_condition_met': True,
    'stop_reason': 'All concrete new public attachment leads closed; no outstanding qualified public drawing link. No repetition of prior source acquisitions or indefinite expansion.',
})
write('new-upper-staff-source-ledger.json', {
    'sealed_at': now, 'owner_write_scope': str(E),
    'sources': sources,
    'counts': {'new_original_attachments': 7, 'design_attachments': 6,
               'administrative_notice_attachments': 1,
               'new_actual_architectural_drawing_pages': 0,
               'new_metric_registration_controls': 0, 'new_world_height_controls': 0,
               'original_bytes': sum(s['bytes'] for s in sources)},
    'prior_source_exclusion_reference': str(O / 'new-blueprint-source-ledger.json'),
    'claims': {
        'directly_supported': ['4F2024 supervision project identity', '5F2019 telecom 106.00m² project scope and nine annex titles', 'MAIN2019 fireproofing project scope and eleven annex titles'],
        'contradicts_premise': ['A drawing index is not an acquired drawing set.', 'MH public3F tenant plans do not establish MAIN employee3F layout.'],
        'near_match_only': ['Station history ebook generic office reference.'],
        'insufficient': ['Full MAIN3F staff plan', '4F assembled staff adjacency', '5F2019 nine annex plans', 'Current as-built changes or P101 correspondence', 'Absolute EL and inter-floor heights'],
    },
    'fallback_boundary': 'User-authorized 1F-based fallback construction belongs to scene authors; this research does not verify that upper floors share 1F layout.',
    'scene_boundary': 'No Unity, Assets, scene, canonical authority or root planning file writes.',
})
write('acquisition-integrity-audit.json', {
    'verified_at': now, 'result': 'PASS', 'original_count': len(checks),
    'checks': checks, 'all_original_hashes_unique': True,
    'prior_source_reacquisitions': 0,
    'scene_geometry_installed_by_this_lane': False,
    'public_only_no_employee_authentication': True,
    'live_child_agents_created': 0,
})

rows = '\n'.join(f"| {s['title']} | {s['notice']} / {s['publication_date']} | `{Path(s['path']).name}` | {s['bytes']:,} | `{s['sha256']}` |" for s in sources)
handoff = f'''# Upper staff-floor public-source research handoff

Sealed {now}. New evidence directory only; no scene or canonical writes. Aside CLI public browsing/downloads only. Seven new original attachments (six design documents and one public notice) were acquired and passed byte-count, OLE-container and SHA-256 checks. **No additional actual architectural drawing pages, region-registration controls or absolute/storey height controls were acquired.**

## Findings for scene authors

- **MAIN 3F:** no new qualified employee-space plan. Eight valid G2B annual searches2019–2026 for the exact title keyword `부산역 3층` each returned zero; this is bounded keyword coverage only. Previously retained tenant/fire plans belong to **MH public3F**, according to the3F author's direct region review, and do not establish MAIN employee-space topology.
- **MAIN 4F:** new2024 architectural construction-management and electrical-supervision HWP originals confirm the renovation project and staff use, but contain management specifications and covers/schedule images only. The old2023 quantity workbook remains separate evidence; it does not gain adjacency or world-Y authority from these new documents.
- **MAIN 5F:** new2019 telecom design establishes a **106.00m²** project for female employee accommodation, WC and shower renovation. Exact pins: `개요!B9:B10`, `도면목록!B3:B11`. It lists nine annex drawings, including existing/proposed floor plans, but those nine pages are absent from the23-sheet workbook. Two original embedded images are the cover and logo. Readable PDF derivative124pages: p1/p11 scope, p115 drawing cover, p117 drawing list, p121 quantity cover. No spatial linkage to2018 P101 or current as-built completion is established.
- **MAIN sections:** the final2019 fireproofing lead yielded three design XLS originals (27/23/23worksheets). `도면목록!B3:B13` lists11annexes including roof/rooftop structural plans, partial sectionsA2–A6/A11–A13/A14–A20 and stair1/2sections. **Actual11drawing pages are absent from all three workbooks**; each embeds only a61×37logo. Combined PDF88pages, p75drawing cover/p77index; all88page thumbnails reviewed. The public notice directs design inspection to Busan Architecture Office; it supplies no additional public annex URL. No EL/storey control follows from drawing titles.

The user's1F-based fallback authorization remains available to scene authors where upper-floor evidence is missing. It does not make an upper-floor layout a verified1F copy. Preserve source-backed geometry versus fallback assumptions explicitly, with separate MAIN/MH registration domains.

## Directly openable evidence

- [Machine-readable source ledger](new-upper-staff-source-ledger.json), [integrity audit](acquisition-integrity-audit.json), [bounded searches and failure ledger](search-and-failure-ledger.json).
- [5F source-cell scope and missing annex](5f-telecom-scope-and-missing-annex.md), [readable5F PDF](rendered/official-5f-telecom-design-2019.pdf), [5F original-image inventory](5f-telecom-original-embedded-images.json).
- [MAIN fireproofing source-cell scope and missing annex](main-fireproof-scope-and-missing-annex.json), [readable MAIN PDF](rendered/official-main-fireproof-2019-combined.pdf), [directly inspected11-title list](main-fireproof-readable-p077.png).
- Existing prior seven architecture/location PDFs and4F thirty-room quantity evidence were read-only exclusion references, not redownloaded: `../20261010-missing-floorplans-deep-research/HANDOFF.md`.

## New original attachment identities

| Original | Official notice / publication | Local original | Bytes | SHA-256 |
| --- | --- | --- | ---: | --- |
{rows}

Each original's same-basename JSON preserves the actual download URL, acquisition timestamp and public attachment metadata. G2B identifiers came from observed public file rows; every session field in the public download template was null. Korail deep links came from the actual linked-agency UI and actual public file anchors.

## Public source and coverage limits

The2024 construction-management notice is [9242205-00](https://ebid.korail.com/popup/bidDetailPopup.do?zzbidinv=9242205-00&zzstnum=00); electrical supervision is [9243757-00](https://ebid.korail.com/popup/bidDetailPopup.do?zzbidinv=9243757-00&zzstnum=00). The2019 sources are [G2B public notices](https://www.g2b.go.kr/)20191134046-000and20191036544-000, with observed identifiers and exact original names in their receipts.

An actual[Korail disclosure ledger](https://info.korail.com/info/previewInfoRelFile.do?atchmnflNo=2646)row280 records request15401541, “부산역 층별 평면도 요청,” requested2025-11-07and marked비공개 on2025-11-10. This single request outcome is not a universal claim about all station plans; public tender documents were acquired successfully. No employee site, access bypass or third-party contact was used.

Twenty-one additional indexed queries and the eight valid annual official3F searches are logged. Invalid multi-year G2B queries were excluded from negative evidence. Old Aside-session failures were recovered through a fresh REPL, without account/config changes. Missing Korean font derivatives were superseded; original sheets/cells remain authority. Station-history near matches and search AI overviews were not promoted to geometric facts.

All concrete public attachment leads are closed. Remaining gaps: full MAIN3F employee plan;4F35architecture annex sheets and assembled adjacency;5F2019nine annex plans; MAIN2019eleven section/plan annexes; current as-built changes; independently source-pinned inter-floor/absolute heights. Research does not hold any scene-author lane open.
'''
(E / 'HANDOFF.md').write_text(handoff)
with (E / 'findings.md').open('a') as f:
    f.write('\nFinal MAIN2019 fireproofing lane closed: notice20191036544-000, three XLS originals (949248/830976/783872bytes), 27/23/23worksheets. Eleven roof/partial-section/stair-section annex titles in 도면목록!B3:B13, no actual annex drawings in any workbook; each one61x37logo. Combined PDF88pages, p77list directly inspected and all88pages overviewed. Public notice59392bytes directs office inspection, no additional public annex URL. No public contacts made, no geometry/EL control.\n\nSealed new-source/failure ledgers and HANDOFF; seven new original attachments, zero new architectural drawing pages. Integrity audit PASS; no prior source hash reacquired. Bounded public research complete.\n')
(E / 'progress.md').write_text('''# Progress

All four bounded research phases complete. Existing seven PDFs and4F quantity workbook were read-only exclusions; no duplicate acquisition. Aside fresh REPL restored public browsing without account/config changes. Twenty-one new indexed searches and eight valid annual G2B3F searches completed.

Seven new original public attachments acquired and hash/container/receipt audited: two2024 4F supervision HWP; one2019 5F telecom XLS; three2019 MAIN fireproofing XLS; one MAIN public notice HWP. Actual source-cell/index and embedded-image review completed. Readable5F PDF124pages and MAIN PDF88pages are derivatives; originals remain authority. No new actual architectural drawing pages, EL/world-Y or registration controls.

New evidence and material gaps shared immediately with applicable floor/height authors and root. Final HANDOFF/source ledger/failure ledger/integrity audit written in own evidence directory. Final notification is the only remaining handoff action. No Unity/Assets/canonical/root-plan writes, no child agents or third-party contacts. This research lane does not hold scene authors open.
''')
plan = (E / 'task_plan.md').read_text().replace('[in_progress]', '[complete]').replace('[pending]', '[complete]')
plan += '\n## Completion\nAll concrete public leads closed; sealed HANDOFF and ledgers. Seven original attachments passed integrity audit, zero new architectural drawing pages. Missing upper staff plans and heights remain explicit; no upper-floor1F equivalence inferred.\n'
(E / 'task_plan.md').write_text(plan)
print(json.dumps({'result': 'PASS', 'originals': len(sources),
                  'original_bytes': sum(s['bytes'] for s in sources),
                  'new_architectural_pages': 0, 'new_queries': len(queries),
                  'valid_3f_annual_queries': len(annual['queries']),
                  'handoff': str(E / 'HANDOFF.md')}, ensure_ascii=False))
