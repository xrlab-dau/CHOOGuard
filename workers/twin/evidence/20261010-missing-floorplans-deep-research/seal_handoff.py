"""Seal source acquisition provenance and check against retained originals; no network or scene writes."""
import datetime, hashlib, json, pathlib, posixpath, re, zipfile
import xml.etree.ElementTree as ET
import pymupdf as fitz, openpyxl, olefile
E = pathlib.Path(__file__).resolve().parent
ROOT = E.parents[3]
REL = E.relative_to(ROOT)
def jread(name): return json.loads((E / name).read_text())
def dump(name, data): (E / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def relative(path): return str(path.relative_to(ROOT))
def absolute(path): return str(ROOT / path)
ledger = jread('new-blueprint-source-ledger.json')
ledger['sources'] = [x for x in ledger['sources'] if x.get('pages')]
for x in ledger['sources']:
    x['source_kind'] = 'architectural_or_location_pdf'
    x['evidence_class'] = 'directly_supported'
    x['registration_status'] = 'UNREGISTERED; separate current official FBX correspondence required'
    if x['path'].endswith('05.매장 위치도 및 디자인안.pdf'):
        x['image_resolution'] = 'Mixed PDF: p1 has embedded914x831 location diagram and401x59 image plus vector annotations; p2 has1500x858 proposal rendering. Location is not a fully vector architectural plan.'
    if '3f-construction-2018/01.' in x['path']:
        x['dependent_review'] = {'owner': '3F ground worker 9337', 'status': 'CEILING_VOID_SUPPORTED_FLOOR_VOID_NOT_PROVEN', 'scope': 'Local tenant p2 VOID; p4/p5/p6 천정 VOID LINE and p7 section. Distinct from central atrium and user-confirmed 2F large X slab voids.'}
arch_receipt = jread('official-4f-architecture-2023.json')
arch_dir = E / 'official-4f-architecture-2023'
quantity = next(arch_dir.glob('03_*.xlsx'))
quantity_sha = '43cda33251138286490415359f53d34c7194838f9ed75b768aa3044bb29000ac'
quantity_entry = {
    'path': relative(quantity), 'bytes': quantity.stat().st_size, 'sha256': quantity_sha,
    'source_kind': 'measured_room_quantity_diagrams', 'evidence_class': 'directly_supported',
    'floor': '4F explicitly labeled 지상4층',
    'region': 'Busan Station main building crew-office/dorm renovation; no scene frame binding',
    'architectural_currentness': '2023 proposed renovation, not verified present as-built',
    'drawing_date': '2023-10-02', 'publication_date': '2023-12-01',
    'source_notice': '20231147064-001', 'source_page': 'https://www.g2b.go.kr/',
    'public_file_identifiers': {k:arch_receipt['public_file_row'][k] for k in ['untyAtchFileNo','atchFileSqno','orgnlAtchFileNm']},
    'download_receipt': str(REL / 'official-4f-architecture-2023.json'),
    'download_url': arch_receipt['url'], 'acquired_at': arch_receipt['acquired_at'],
    'archive_path': str(REL / 'official-4f-architecture-2023.zip'),
    'archive_sha256': 'aa7a3e86ced2a69dc3daa45423535f38563fa4e0d371d632be190720a87e0f29',
    'review': {'sheet': '내부산출서', 'quantity_groups': 36, 'room_groups': 30, 'diagram_anchors':31, 'unique_diagrams':30, 'bindings':str(REL/'4f-quantity-image-room-bindings.json'), 'controls':str(REL/'4f-measured-room-controls.json')},
    'new_information': 'Room-local dimensions, areas, perimeters and finish heights; 6 opening schedule types. Room groups1–30 include male dorm2–9 repeated8; image11 reused for two female rooms; image30 belongs33부대공사.',
    'allowed_use': 'Source-local room-shape and quantity checks only; original source rows and labels retained.',
    'registration_status': 'UNREGISTERED',
    'prohibited_inference': ['room adjacency or placement','building exterior extent','north/world frame','storey/slab elevation from finish height','current as-built topology'],
}
ledger['sources'].append(quantity_entry)
supplement_specs = [
    (next(arch_dir.glob('01-1_*.hwp')), '550fdd1e253c478d46e70b29343075ffba5af7325416ae3912f5c17b8b9a4bbf', '20231147064-001', '2023-12-01', 'official-4f-architecture-2023.json', 'Design explanation confirms main-building 4F crew-office/dorm scope896m²;35 drawing sheets listed as separate annex; actual23 embedded images are forms/charts, not plans.'),
    (next(arch_dir.glob('01-2_*.xlsx')), '5045ce48575c1a2cc0d065ef38e07a1ee809804cbdd19bc5917c55af49cd652a', '20231147064-001', '2023-12-01', 'official-4f-architecture-2023.json', '10 cost worksheets, no embedded drawing media.'),
    (E/'official-4f-fire-design-2024.hwp', '333d7463c540e7a2a3be5b03157c66374a7c03370ef6e1236f4be98da2cfd585', '20240334034-000', '2024-03-20', 'official-4f-fire-design-2024.json', '15 drawing sheets listed as separate annex;16 actual embedded images reviewed, forms/charts rather than floorplans. Viewer reports68 document pages.'),
    (E/'official-4f-electric-design-2024.xlsm', '5cf36eeb56ee2414a1e01640528e41b6311d7b3507dd2e60d4afacabe5d89fac', '20240431396-000', '2024-04-18', 'official-4f-electric-design-2024.json', '29 budget/phasing sheets; only embedded image is cover; no geometry, no macros executed.'),
    (E/'official-4f-electric-spec-2024.hwp', 'ef6c67a4240de6dad410e1f0fcfc077c95baf27666d2286a7edd380a68129e57', '20240431396-000', '2024-04-18', 'official-4f-electric-spec-2024.json', 'Electrical specification;3 BMP images are forms, not site geometry.'),
]
ledger['supplementary_sources'] = []
for path, expected, notice, published, receipt_name, reason in supplement_specs:
    rec=jread(receipt_name)
    ledger['supplementary_sources'].append({'path':relative(path),'sha256':expected,'bytes':path.stat().st_size,'source_notice':notice,'publication_date':published,'source_page':'https://www.g2b.go.kr/','download_receipt':str(REL/receipt_name),'download_url':rec['url'],'acquired_at':rec['acquired_at'],'evidence_class':'directly_supported','use_boundary':reason})
ledger['gaps'] = {
    '4F': 'Measured room diagrams acquired; assembled floorplan and adjacency absent from both original000 and changed001 architecture archives.35-sheet architecture annex and15-sheet fire annex still missing.',
    'wholebuilding': 'No complete current all-floor set; MAIN/MH/BOARDING need independent regional registration and exact official FBX controls.',
    'heights': 'Finish-height and local slab annotations do not establish generic storey Y.',
}
ledger['review_errors_corrected'] += [
    {'issue':'1F p2 전체배치도 loosely called whole floor','correction':'Project layout covers partial MAIN sector; cropped edges are not exterior walls.'},
    {'issue':'4F36 quantity groups could be mistaken for36rooms','correction':'30 room groups; groups31–36 works/materials;31 image anchors,30 unique images.'},
    {'issue':'Original000 capture status stale in working summary','correction':'Actual JSON contains public000 file rows. Downloaded original ZIP and verified all3 member hashes match changed001; archive timestamps and SHA differ.'},
]
ledger['review_errors_corrected'] = list({x['issue']:x for x in ledger['review_errors_corrected']}.values())
# Independent verification against files and original archive structures.
audit={'checked_at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sources':[],'archives':[],'quantity_bindings':{},'limitations':'Acquisition integrity and source-local binding only; no scene registration or geometry QA.'}
for item in ledger['sources']+ledger['supplementary_sources']:
    path=ROOT/item['path']; actual=sha(path); ok=actual==item['sha256']
    assert ok, item['path']
    r={'path':item['path'],'sha256':actual,'matches_recorded_sha256':ok,'bytes':path.stat().st_size}
    item['live_sha256_matches']=ok
    if path.suffix=='.pdf':
        with fitz.open(path) as d:
            r['pages']=len(d); assert len(d)==item['pages'];item['live_pages']=len(d)
    elif path.suffix in ['.xlsx','.xlsm']:
        with zipfile.ZipFile(path) as z: assert z.testzip() is None
        r['container_integrity']='PASS ZIP'
    else:
        assert olefile.isOleFile(path);r['container_integrity']='PASS OLE signature'
    audit['sources'].append(r)
archive_expected={x['archive_path']:x['archive_sha256'] for x in ledger['sources']}
archive_expected[str(REL/'official-4f-architecture-2023-original000.zip')]='8621da8ad1c6d78ffacbc90af34b342d442f62c5384b7ad783e72a0cb9e57873'
for path_string, expected in archive_expected.items():
    path=ROOT/path_string;assert sha(path)==expected
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None
        audit['archives'].append({'path':path_string,'sha256':expected,'testzip':'PASS','members':len(z.infolist())})
controls=jread('4f-measured-room-controls.json');bindings=jread('4f-quantity-image-room-bindings.json')
assert sha(quantity)==controls['sha256']
wb=openpyxl.load_workbook(quantity,data_only=True,read_only=True)
ws=wb['내부산출서']
headers=[(r[0].row,r[0].value) for r in ws.iter_rows() if isinstance(r[0].value,str) and r[0].value.startswith('실명 :')]
assert headers==[(g['source_row_1based'],g['label']) for g in controls['rooms_and_quantity_groups']]
ns={'xdr':'http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing','a':'http://schemas.openxmlformats.org/drawingml/2006/main','r':'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
with zipfile.ZipFile(quantity) as z:
    rels={e.attrib['Id']:posixpath.normpath(posixpath.join('xl/drawings',e.attrib['Target'])) for e in ET.fromstring(z.read('xl/drawings/_rels/drawing1.xml.rels'))}
    anchors=[]
    for anchor in ET.fromstring(z.read('xl/drawings/drawing1.xml')):
        row=int(anchor.find('xdr:from/xdr:row',ns).text)+1
        blip=anchor.find('.//a:blip',ns);imagepath=rels[blip.attrib['{'+ns['r']+'}embed']]
        anchors.append((row,imagepath))
    assert anchors==[(b['anchor_row_1based'],b['image_zip_path']) for b in bindings]
    for b in bindings:
        path=ROOT/b['extracted_image'];assert path.read_bytes()==z.read(b['image_zip_path'])
        expected_header=max((h for h in headers if h[0]<=b['anchor_row_1based']),key=lambda x:x[0])
        assert expected_header==(b['room_header']['row'],b['room_header']['cells'][0][1])
audit['quantity_bindings']={'status':'PASS','quantity_groups':len(headers),'anchors':len(anchors),'unique_embedded_diagrams':len(set(i for _,i in anchors)),'checks':['actual workbook room-header rows match controls','actual drawing XML anchors and relationships match binding list','all extracted JPEG bytes equal original workbook media','each anchor maps to preceding actual source room or quantity-group header']}
# Preserve the source Door/Window labels verbatim, including surprising classifications.
rows=[]
for cells in wb['창호리스트'].iter_rows(min_row=4,max_row=9):
    rows.append({'source_row_1based':cells[0].row,'source_cells':{str(c.column):c.value for c in cells if c.value is not None},'type':cells[1].value,'width':cells[2].value,'height':cells[3].value,'source_classification':cells[8].value})
dump('4f-door-window-schedule.json',{'source_path':relative(quantity),'sha256':quantity_sha,'sheet':'창호리스트','dimensions':'Source width/height numeric values; corresponding quantity formulas use metres. Preserve source Door/Window classification; these rows do not place any opening.','rows':rows})
comparison=jread('4f-architecture-original-vs-change-comparison.json');assert comparison['same_member_names_and_bytes']
ledger['archive_version_comparison']={'path':str(REL/'4f-architecture-original-vs-change-comparison.json'),'original_notice':'20231147064-000','original_publication':'2023-11-29 17:12:19','changed_notice':'20231147064-001','changed_publication':'2023-12-01 15:47:51','same_member_names_and_bytes':True,'note':'Archive hashes differ; all3 member hashes and names are identical; neither contains separate35-sheet annex.'}
ledger['counts']={'architectural_or_location_pdfs':7,'pdf_pages':sum(x.get('pages',0) for x in ledger['sources']),'measured_room_workbooks':1,'supplementary_original_documents':5,'downloaded_zip_archives':len(audit['archives'])}
ledger['verified_at']=audit['checked_at'];audit['status']='PASS'
dump('new-blueprint-source-ledger.json',ledger);dump('acquisition-integrity-audit.json',audit)
# Unambiguous copy name; preserve earlier contact-sheet path for existing handoffs.
old=E/'4f-architecture-window-schedule-images-contact-sheet.jpg'
(E/'4f-room-quantity-diagrams-contact-sheet.jpg').write_bytes(old.read_bytes())
lines=['# 부산역 추가 원도면 조사 인계','', '공식 공사 첨부에서 건축·위치 PDF **7개, 122쪽**과 4층 실별 치수 자료를 확보했다. 공식 ZIP은 원공고·변경공고를 포함해7개다. 확보·원본 식별 조사이며 현재 씬과의 정합이나 전체 건물 완성을 뜻하지 않는다.','', '## 바로 검토할 자료','', '| 자료 | 실제 검토 지면 | 확인된 범위 |','|---|---|---|']
labels=['2018 1층 건축','2018 5층 회의실 건축','2017 3층 전문점2개소','2018 3층 전문점 건축','2018 3층 위치·디자인','2018 3층 푸드코트 소방','2026 굿즈 전문점 건축']
scopes=['I-101 p2: 곡면 전면·기둥·문·EPS/PS·계단·쌍EV가 있는 MAIN 일부. 상단·좌측 잘림을 외벽으로 쓰지 않는다.','P-101 p2: 사무실·코어·OPEN; p3 기존 CH2400/슬라브4230, p5 신설 CH3000을 구분. 현재 회의실 번호 대응 미확정.','p3: 화장실·에스컬레이터·전문점 띠. 초기2F 유사성 가설은 철회;3F L-wing 지역 검증 담당자 확인 필요.','11쪽 벡터 원도면. 국소 VOID는 3F 담당자 검토상 천정 VOID이며 바닥 개방의 증거로 쓰지 않는다.','p1: 3F L형 날개와453.5㎡ 대상 위치. p2는 제안 렌더링.','p12 MF-02: 치수 있는 푸드코트 띠와 KEYPLAN. 14쪽 벡터 도면.','p5 I-001: WC·상점·EV·계단 맥락, NON SCALE; 층명 미표기. p8 I-004: 5500×6000mm 국소 점포. 실제50쪽.']
for label,item,scope in zip(labels,ledger['sources'][:7],scopes):
    filename=pathlib.Path(item['path']).name
    lines.append(f'| [{label}](<{absolute(item["path"])}>) | '+(', '.join('p'+str(x) for x in item.get('review_pages',[])) or 'contact sheet')+f' | {scope} |')
lines += ['', '## 4층에서 새로 확인한 내용','', '공식 나라장터20231147064-000/001의2023년 내부개수 자료다. 설계설명서가 부산역 본체4층 부산고속철도열차승무사업소 사무실·숙직·샤워실 개수896㎡를 명시한다. 현재 준공 상태는 확인하지 않았다.','', f'- [실별 근거표](<{E}/4f-measured-room-controls.md>) / [기계 판독 JSON](<{E}/4f-measured-room-controls.json>): 30개 실 그룹의 치수·면적·둘레·마감높이와 원본 행을 보존했다.', f'- [치수 도형 모음](<{E}/4f-room-quantity-diagrams-contact-sheet.jpg>) / [원본 그림 연결](<{E}/4f-quantity-image-room-bindings.json>): 31개 배치 앵커,30개 고유JPEG. image11은 여성 숙직실2/3 공용; image30은33부대공사다. 원본에는36개 수량 그룹이 있으므로36개 방으로 읽지 않는다.', f'- [창호 원문표](<{E}/4f-door-window-schedule.json>): ABS1/2/3, FSD1, SD1/2 여섯 항목. 원문 Door/Window 분류를 그대로 보존했다.', '- 소장실 예시:6×7.429m,44.574㎡, 마감높이2.9m. 숙직실 마감높이2.4m, 화장실·샤워실2.7m 등은 층고·슬라브Y가 아니다.', '- 실 사이 인접관계, 좌표, 방위, 외곽, 현장 프레임은 자료에 없으므로 배치하지 않는다.', f'- [원공고·변경공고 비교](<{E}/4f-architecture-original-vs-change-comparison.json>): ZIP해시는 다르지만 내부3파일의 이름과 바이트해시는 동일하다. 두 버전 모두 별첨35매 도면이 없다. 별도2024 소방 설계서의15매 도면도 별첨 목록만 확인했다.','', '## 출처와 검증','', f'[전체 출처 원장](<{E}/new-blueprint-source-ledger.json>)에 각 원본 경로·공고 URL·게시일·다운로드 URL·취득일·SHA256·검토 지면·허용 범위를 기록했다. [무결성 검증](<{E}/acquisition-integrity-audit.json>)은7ZIP,7PDF쪽수,4층 원본6문서, 실제XLSX XML앵커·관계·추출JPEG 바이트를 확인해 PASS했다. 매크로는 실행하지 않았다.','', '온라인 검색·열람·다운로드는 Aside CLI 브라우저만 사용했다. 이번에 수집한 사진9장과 모음은 루트 요청에 따라 정리했고, 출처·해시 기록만 남겼다.2011 잡지3쪽(목차에만 부산역), AURIC404 응답, Google AI개요는 구조 근거에서 제외했다. 과거 설계와 제안 도면을 현재 상태로 자동 승격하지 않는다.','', '## 남은 공백','', '- 4층 전체 배치도 및35매 건축·15매 소방 별첨 원도면.', '- MAIN/MH/BOARDING별 독립 정합, 공식 FBX 외곽·기둥·문틀·층간 접점 대응.', '- 5층 현재 회의실 번호 및 구획과2018 도면의 대응. 새 자료는 이전1118×633 크롭보다 넓지만 전체 건물 범위로 단정할 수 없다.', '- 모든 층의 현재 전체 토폴로지와 측정 층고. 국소 수량·사진에서 추론하지 않는다.','', '작성·다운로드·검증 산출물은 이 evidence 디렉터리 안에만 있다. Unity, Assets, 정본 문서, 루트 계획은 변경하지 않았다.','']
coverage = [
    '## 1~5층 확보·누락 범위', '',
    '| 층 | 이번 확보 / 기존 보유 | 남은 공백 |',
    '|---|---|---|',
    '| 1F | 2018 원본10쪽, p2 MAIN 일부; 기존960×572 임대도면 | 전체1F 범위, 별도MH 프레임, 독립 현장 정합 |',
    '| 2F | 기존631×496 임대도면 유지. 2026 굿즈 p5는2F MAIN 맥락 후보, 층명 미표기 | 전체 고해상도2F 및 MAIN/MH/BOARDING 접점; 후보를 승인 도면 대체물로 쓰지 않음 |',
    '| 3F | 2017/2018 공사·소방·위치 PDF4개44쪽; 기존4000×2252 안내도 | 현재 전체 실 배치와 공식 FBX 정합; 국소 천정 VOID와 바닥 개방 구분 |',
    '| 4F | 2023 실별 수량 치수 도형,30실 그룹; 원·변경공고 모두 확인 | 전체 배치도,35매 건축·15매 소방 별첨, 인접관계·외곽·층고 |',
    '| 5F | 2018 원본18쪽 p2/p3/p5; 기존1118×633 회의실 크롭 | 현재 구획·실번호 대응, 전체 범위, 정합 |', '',
    f'[조사 시작 시 보유자료 목록](<{E}/retained-floorplan-inventory.md>)은 이전 상태의 스냅샷이다. 이번 추가 확보 후 범위는 위 표와 출처 원장을 따른다.', '',
]
insert_at=lines.index('## 4층에서 새로 확인한 내용')
lines[insert_at:insert_at]=coverage
provenance=['## 원본 날짜·해시', '', '아래 p번호는 PDF 실제 페이지(1부터)다. PDF 표제의 도면번호·날짜와 공고 게시일을 구분했다. 전체 다운로드 URL·취득시각은 출처 원장에 있다.', '', '| 원본 | 공고 게시일 / 도면 날짜 | SHA256 |', '|---|---|---|']
dates=['2018-12-19 / p2 2018.11','2018-12-19 / p2 2018.11; p3 2018.08','2017-02-07 / 도면 내 날짜 별도 검토','2018-01-26 / 도면 내 날짜 별도 검토','2018-01-26 / 위치안','2018-12-19 / p12 2018.10','2026-07-30 / 표지2026.07; p5·p8 2026.05']
for label,item,date in zip(labels,ledger['sources'][:7],dates):
    provenance.append(f'| {label} | {date} | `{item["sha256"]}` |')
provenance += [f'| 4F 수량XLSX | 원공고2023-11-29 / 변경2023-12-01 / 시트20231002 | `{quantity_sha}` |', '', '[1F·5F 공고](https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100045066), [3F2017](https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100038714), [3F2018](https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100041854), [3F소방](https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100045068), [2026굿즈](https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100081621). 4F는 [나라장터](https://www.g2b.go.kr/) SPA에서20231147064/20240334034/20240431396을 조회했다. 실제 공고 DOM과 공개 첨부행을 보존했다.', '']
insert_at=lines.index('## 출처와 검증')
lines[insert_at:insert_at]=provenance
(E/'HANDOFF.md').write_text('\n'.join(lines))
print(json.dumps({'status':audit['status'],'counts':ledger['counts'],'bindings':audit['quantity_bindings'],'handoff':str(E/'HANDOFF.md')},ensure_ascii=False,indent=2))
