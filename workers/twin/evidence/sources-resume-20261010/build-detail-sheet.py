"""Retained, directly inspected finish references; writes only this source lane."""
from pathlib import Path
import hashlib, html, json, os, re, subprocess
from PIL import Image

ROOT = Path('/Users/um-yunsang/CHOOGuard')
OUT = ROOT / 'workers/twin/output/sources-resume-20261010'
EVIDENCE = ROOT / 'workers/twin/evidence/sources-resume-20261010'
PREFIX = 'Assets/ChooGuard/Art/StationInterior/'

materials = {
    'M01': ('Materials/Lane/floor_granite_tile.mat', '회색 점무늬 석재처럼 보이는 광택 바닥의 재사용 후보. 실제 암종·타일 치수·UV 반복률은 확인하지 않음.'),
    'M02': ('Materials/Lane/metal_stainless_hairline.mat', '은색 금속 패널·문틀·손잡이의 후보. 사진의 색띠는 조명 반사이므로 색상 무늬로 굽지 않음.'),
    'M03': ('Materials/Lane/column_stainless_brushed.mat', '은색 기둥 피복의 보조 후보. 밝은 도장 기둥에 일괄 적용하지 않음.'),
    'M04': ('Generated/Hall_Column_White.mat', '2F 맞이방 밝고 매끈한 기둥 표면의 후보. 현재 색은 따뜻한 갈색 기운이 있으므로 동일 조명 비교 필요.'),
    'M05': ('Generated/Tenant_FasciaBand.mat', '짙은 회색 매장 띠·프레임의 후보. 상호/간판 텍스처나 구조 배치를 가져오지 않음.'),
    'M06': ('Generated/Tenant_Glass.mat', '투명 매장 유리 후보. 현재 투명 설정이며 약한 청색 색조가 있음; 현장 투과율의 측정값은 아님.'),
    'M07': ('Kit/Materials/Kit_GlassClear.mat', '출입문 투명 유리의 보조 후보. 유리의 실제 사양은 사진으로 확인하지 않음.'),
    'M08': ('Materials/Lane/wall_painted_gypsum_fine.mat', '라운지 내부 밝은 무광 벽/패널의 시각 후보. 실제 바탕재가 석고라는 뜻은 아님.'),
    'M09': ('ArchitectureCorrections/ObservedLowCeiling20261009/ObservedPerimeterMetal.mat', '무늬 없는 은회색 금속 프레임의 대안. _Metallic=.78, _Smoothness=.76; 기존 승인된 소유면에서만 비교.'),
}

refs = [
    dict(id='D01', title='1F 인생네컷 옆 보관함 표지 / 금속 피복',
         path='asset-library/research-public/2026-10-09/floor1-multiview-next/source01-leedh960830/img_5.jpg',
         source_url='https://m.blog.naver.com/leedh960830/224021169842',
         receipt='asset-library/research-public/2026-10-09/floor1-multiview-next/receipts.json',
         expected_sha256='758311480d8aa3c9eb42a38ece796a02cd17511805be69b681442219759493ae',
         floor='1F', zone='인생네컷 옆 보관함 안내판이 붙은 금속 피복',
         confidence='층/구역: 본문 맥락에 의한 중간 이상. 사진 단독 층 표지는 없음.',
         identity_basis='본문이 인생네컷을 1층 B&C 및 보관함 안내판 옆으로 명시. 사진에는 보관함 표지와 부스 측면이 보임. 본문은 1F와 2F를 모두 다루므로 다른 통로 사진까지 같은 층으로 확장하지 않음.',
         metadata_path='asset-library/research-public/2026-10-09/floor1-multiview-next/source01-leedh960830/blog-aside.json',
         published='2025-09-25 11:51 KST', capture='촬영일 미확인. 게시일은 촬영일이 아니며 현재성도 보증하지 않음.',
         observations=['은색 반사 금속 패널과 세로 이음선이 선명함.', '빛이 넓게 번지는 새틴 금속처럼 보이며 조명 색이 표면에 반사됨. 실제 합금·연마 방향은 미확인.'],
         material_ids=['M02','M09'], limits='피복의 외관만 사용. 기둥/코어 구조 분류, 두께, 배치, 크기, 뒤쪽 공간은 추정하지 않음.'),
    dict(id='D02', title='2F 맞이방 / 밝은 기둥·회색 광택 바닥',
         path='workers/twin/evidence/sources-resume-20261010/RY2-long-local-120.00.png',
         source_url='https://www.youtube.com/watch?v=RY2qvE0Tugk',
         receipt='asset-library/research-public/2026-10-08/measured-twin/receipts.json',
         expected_sha256='0398909799a0a5788b49bffc500183c3878b7fdfaf4f2a6eb1e4c7cf3fb12235',
         floor='2F', zone='표지에 2층 맞이방이라고 명시된 실내 공용 구역',
         confidence='층/구역: 높음. 프레임에 이곳은 2층 맞이방 입니다 표지 직접 보임.',
         identity_basis='화면의 층 안내 문구를 직접 확인. 다른 승강장 연결 구역이나 특정 native 소유면과의 대응은 만들지 않음.',
         metadata_path='asset-library/research-public/2026-10-08/measured-twin/receipts.json',
         published='2023-05-01 (retained YouTube upload_date metadata)', capture='촬영일 미확인. 2023 게시 자료이며 2026의 마감 현황으로 단정하지 않음.',
         clip_path='asset-library/research-public/2026-10-08/measured-twin/clips/RY2qvE0Tugk_215-670.webm',
         clip_sha256='f97224a05428d56586dee0c311852bb4caa90fd727a1e34ac3225e85350f5c0c',
         time_basis='retained clip-local request 120.00 s. Source offset/keyframe preroll and exact showinfo PTS not established for this old frame.',
         observations=['전경 기둥은 밝은 아이보리/회백색의 매끈한 비금속 표면처럼 보임. 현장 도장/피복 종류는 미확인.', '바닥은 회색 계열의 분할 이음과 광택 반사가 보임. 금속 난간은 은색, 매장 앞은 투명 유리와 어두운 프레임이 보임.'],
         material_ids=['M04','M01','M02','M06'], limits='매장 상호의 현재성, 기둥 위치/단면/높이, 바닥 치수, 천장 및 공간 형태의 근거로 사용하지 않음.'),
    dict(id='D03', title='2F KORAIL LOUNGE / 바닥·금속 패널·매장 전면',
         path='asset-library/research-public/2026-10-09/floor2-facility-field-sources/lounge-spring-018.jpg',
         source_url='https://m.blog.naver.com/springlll8/223252084280',
         receipt='asset-library/research-public/2026-10-09/floor2-facility-field-sources/receipts.json',
         floor='2F', zone='KORAIL LOUNGE BUSAN / tripin 전면',
         confidence='층/구역: 높음. 사진 상호와 본문의 2층 맞이방 라운지 설명이 일치.',
         identity_basis='원문 components[26]에 해당 이미지와 2층 맞이방 라운지 문장이 함께 있음. 사진에 KORAIL LOUNGE BUSAN 표지 직접 확인.',
         metadata_path='workers/twin/evidence/floor2-facility-field-sources-20261009/lounge-spring.json',
         published='2023-10-31 23:36 KST', capture='촬영일 미확인. 2023 라운지 마감 상태이며 2026 현황 미확인.',
         observations=['바닥은 회색 점무늬와 짙은 이음선, 뚜렷한 광택 반사를 보임. 실제 석종·치수는 미확인.', '왼쪽 금속 패널은 은색 반사와 가로 이음이 보임.', '전면 띠는 짙은 회색, 내부 벽과 파티션은 밝은 무광 계열. 오른쪽 창에는 투명 영역과 반투명 띠가 보임.'],
         material_ids=['M01','M02','M05','M08','M06'], limits='반투명 띠는 일반 투명 유리 재질만으로 재현된다는 뜻이 아님. 띠/가구/개구부의 위치·치수 및 라운지 배치는 추정하지 않음.'),
    dict(id='D04', title='2F 남천할매떡볶이 / 유리·어두운 프레임·은색 측면',
         path='asset-library/research-public/2026-10-09/floor2-guide-passages/source09-namcheon-000.jpg',
         source_url='https://m.blog.naver.com/suziesuzie/224046364232',
         receipt='asset-library/research-public/2026-10-09/floor2-guide-passages/receipts.json',
         floor='2F', zone='남천할매떡볶이 매장 전면',
         confidence='층/구역: 높음. 본문이 부산역 2층이라고 명시하고 사진에 동일 상호 확인.',
         identity_basis='본문의 부산역 2층 문장 바로 다음 components[4] 이미지. 상호가 직접 읽힘.',
         metadata_path='workers/twin/evidence/floor2-capture-sources-20261009/source09-suzie-namcheon.json',
         published='2025-10-19 15:29 KST', capture='촬영일 미확인. 게시 epoch만 확인됨.',
         observations=['넓은 전면 띠와 유리 프레임은 짙은 회색/검정 계열. 유리 너머 실내가 보이고 반사가 겹침.', '측면 및 상부에는 은색 금속 패널이 보임. 전면 바닥에는 회색 점무늬 광택 표면과 검은 광택 띠가 보임.', '유리 너머 벽은 밝은 작은 직사각형 타일처럼 보이나 상세 타일/줄눈 재질은 이 해상도로 확정하지 않음.'],
         material_ids=['M05','M06','M02','M01'], limits='검은 바닥 띠 및 내부 벽 타일은 이번에 검증한 기존 재질 중 정확한 대응을 확정하지 않음. 매장 크기·문 작동·인접 공간 배치는 추정하지 않음.'),
    dict(id='D05', title='2F 9번 출구 / 은색 문틀·투명 유리·손잡이',
         path='asset-library/research-public/2026-10-09/floor2-facility-field-sources/parking-himawari-013.jpg',
         source_url='https://m.blog.naver.com/himawari_tw/224348442114',
         receipt='asset-library/research-public/2026-10-09/floor2-facility-field-sources/receipts.json',
         expected_sha256='a3f1df4a9a7616e8b9eb49f1f37464836c73d5b785b5618b3a7a39f14d3a2c42',
         floor='2F', zone='9번 출구 출입문 전면',
         confidence='구역: 높음(사진의 9번 표지). 층: 중간 이상(보존된 MAIN2F 구역 분류); 사진 자체에 층 숫자는 없음.',
         identity_basis='9번 출구 표지가 원본에 명확함. 보존 source-controls의 MAIN2F Exit9 actual frontal door bank 분류를 층 맥락으로 사용. 8번 출구/승강장/승강기로 확장하지 않음.',
         metadata_path='workers/twin/output/floor2-facility-field-sources-20261009/source-controls.json',
         published='2026-07-16 13:54 KST', capture='정확한 촬영일 미확인. EXIF 306=2026:07:12 18:27:34는 수정 시각이며 DateTimeOriginal 아님. 파일명 20260709도 검증된 촬영일 아님.',
         observations=['문틀, 가로 프레임, 긴 세로 손잡이는 은색 반사 금속처럼 보임.', '문과 고정창에 투명 유리, 청색 인쇄 띠가 보임. 사진의 푸른 빛을 유리 자체의 정확한 색으로 단정하지 않음.', '바닥에는 회색 점무늬 광택 표면과 짙은 띠가 보임. 사진 속 노란 점/선 표면은 별도 디테일로 존재.'],
         material_ids=['M02','M09','M07','M01'], limits='문틀/유리 외관만 사용. 문 작동 방식·개수·크기·구조 위치·외부 공간 형태는 이 lane의 제안 대상이 아님. 승강기 프레임 참조가 아님.'),
]

excluded = [dict(id='X01', path='asset-library/research-public/2026-10-09/floor1-multiview-next/source01-leedh960830/img_2.jpg',
                expected_sha256='daf59bace3676dfbcbf69a0a2128ae2d747a9c9d75fa3b2e9a63632b7168c438',
                directly_viewed=True, reason='본문은 1F와 2F를 함께 다루며 이 통로 사진의 층을 직접 묶는 캡션/표지가 충분하지 않아 층별 추천에서 제외. 이전 lane의 1F 분류를 재사용하지 않음.')]

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

receipts = {}
for f in set(r['receipt'] for r in refs):
    data=json.loads((ROOT/f).read_text())
    if isinstance(data,list):
        for item in data:
            if isinstance(item,dict) and item.get('path'): receipts[item['path']]=item

for r in refs+excluded:
    p=ROOT/r['path']; r['sha256']=sha(p); r['bytes']=p.stat().st_size
    with Image.open(p) as im: r['dimensions']=list(im.size); im.verify()
    expected=r.get('expected_sha256') or receipts.get(r['path'],{}).get('sha256')
    assert expected and expected==r['sha256'], (r['id'],'hash mismatch')
    r['hash_matches_retained_receipt']=True
    if r in refs:
        r['directly_viewed_original']=True
        r['classification']='directly_supported_visible_detail'
        r['acquired_utc']=receipts.get(r['path'],{}).get('acquired_utc') or receipts.get(r['path'],{}).get('retrieved_at_utc') or receipts.get(r['path'],{}).get('acquired_at_utc')
        assert (ROOT/r['metadata_path']).exists()
        if r.get('clip_path'): assert sha(ROOT/r['clip_path'])==r['clip_sha256']

mat_records=[]
for mid,(rel,note) in materials.items():
    p=ROOT/(PREFIX+rel); s=p.read_text()
    def find(key):
        m=re.search(r'^\s*- '+re.escape(key)+r': (.+)$',s,re.M)
        return m.group(1) if m else None
    record=dict(id=mid,path=PREFIX+rel,sha256=sha(p),note=note,
                metallic=find('_Metallic'),smoothness=find('_Smoothness'),surface=find('_Surface'),base_color=find('_BaseColor'))
    m=re.search(r'm_CustomRenderQueue: (.+)',s); record['render_queue']=m.group(1) if m else None
    for tex in ['_BaseMap','_BumpMap']:
        m=re.search(re.escape(tex)+r':\s*\n\s*m_Texture: \{([^}]+)\}',s)
        record[tex]=m.group(1) if m else None
        if m and 'guid:' in m.group(1):
            guid=re.search(r'guid: ([^, ]+)',m.group(1)).group(1)
            cmd=['rg','-l','-F','guid: '+guid,'Assets','--glob','*.meta']
            result=subprocess.run(cmd,cwd=ROOT,text=True,capture_output=True)
            paths=result.stdout.splitlines()
            assert paths,(mid,tex,'unresolved texture',guid)
            assets=[x.removesuffix('.meta') for x in paths]
            assert all((ROOT/x).exists() for x in assets)
            record[tex+'_resolved_assets']=assets
    mat_records.append(record)

ledger=dict(schema='chooguard.finish-detail-reference-only.v1',
            task='Retained originals only; no new searches/downloads; no Unity or Assets writes.',
            directly_viewed_count=6,accepted_count=5,rejected_count=1,
            geometry_authority='None. All structure must come from floorplans. Do not infer layout, positions, room envelopes, floor voids, heights or metric dimensions from these images.',
            evidence_epoch='Retained metadata verified locally on 2026-10-10; web not refreshed by design.',
            material_status='Existing file/YAML candidates only. No material assignment, render comparison, field composition or optical measurement certified.',
            elevator_status='Insufficient floor/zone-secure reference in this bounded six-original pass; no elevator-specific material recommendation.',
            rights='Original references retain author/host rights; no open reuse license established. Local review only; no reference image imported or published.',
            accepted=refs,excluded=excluded,materials=mat_records)
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'detail-reference-ledger.json').write_text(json.dumps(ledger,ensure_ascii=False,indent=2)+'\n')

def esc(x): return html.escape(str(x))
def relurl(p): return os.path.relpath(ROOT/p,OUT)
cards=[]
for r in refs:
    items=''.join('<li>'+esc(x)+'</li>' for x in r['observations'])
    mats=' · '.join('<a href="#'+mid+'">'+mid+'</a>' for mid in r['material_ids'])
    cards.append(f'''<article><h2>{r['id']} {esc(r['title'])}</h2>
<a href="{esc(relurl(r['path']))}"><img src="{esc(relurl(r['path']))}" alt="{esc(r['title'])}"></a>
<p class="confidence">{esc(r['confidence'])}</p><p>게시: {esc(r['published'])}<br>{esc(r['capture'])}</p>
<ul>{items}</ul><p><b>기존 재질 후보:</b> {mats}</p><p class="limit">{esc(r['limits'])}</p>
<details><summary>정확한 원본 경로·식별 근거</summary><code>{esc(r['path'])}</code><p>{r['dimensions'][0]} × {r['dimensions'][1]} · SHA256 <code>{r['sha256']}</code></p><p>{esc(r['identity_basis'])}</p><a href="{esc(r['source_url'])}">출처</a></details></article>''')
rows=[]
for m in mat_records:
    props=f"Metallic {m['metallic']}, Smoothness {m['smoothness']}, Surface {m['surface']}"
    rows.append(f'<tr id="{m["id"]}"><th>{m["id"]}</th><td><a href="{esc(relurl(m["path"]))}"><code>{esc(m["path"])}</code></a><br>{esc(props)}<br>BaseColor {esc(m["base_color"])}</td><td>{esc(m["note"])}</td></tr>')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>Busan Station 1F/2F finish-detail references</title>
<style>body{margin:28px auto;padding:0 22px;max-width:1300px;color:#20272c;background:#f4f3ef;font:15px/1.55 system-ui,sans-serif}h1{font-size:27px}h2{font-size:18px;margin:0 0 10px}a{color:#1a5980}code{font-size:12px;overflow-wrap:anywhere}header{border-bottom:2px solid #263b47;margin-bottom:22px}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}article{background:white;border:1px solid #d6d6d0;border-radius:8px;padding:18px}img{width:100%;height:330px;object-fit:contain;background:#eaeae4}.confidence{font-weight:650}.limit{color:#654b36;font-size:13px}details{border-top:1px solid #ddd;padding-top:8px}table{width:100%;border-collapse:collapse;background:white}th,td{text-align:left;vertical-align:top;border:1px solid #ddd;padding:10px}td:nth-child(2){width:50%}footer{margin-top:24px;color:#59626a}@media(max-width:800px){.grid{grid-template-columns:1fr}}@media print{body{margin:0;max-width:none}article{break-inside:avoid}img{height:240px}details{display:none}}</style>
<header><h1>부산역 1·2층 마감 디테일 참조</h1><p>기존 원본 6개 직접 확인 → 층·구역이 확인되는 5개 채택. 신규 검색·다운로드 0. 원본을 클릭하면 그대로 열립니다.</p><p><b>바닥·벽·기둥 피복·출입문·매장 전면의 외관 참고만 허용.</b> 구조는 도면을 따릅니다. 이미지로 배치·위치·공간 외곽·바닥 개구부·높이를 추정하지 않습니다.</p><p>게시일과 촬영일은 구분했습니다. 사진의 빛과 반사로 재료 성분·색·투과율을 측정하지 않았으며, 재질 후보는 아직 적용하거나 렌더 비교하지 않았습니다.</p></header>
<main><div class="grid">'''+''.join(cards)+'''</div><h2 style="margin-top:28px">실제로 존재하는 기존 재질 후보</h2><table><thead><tr><th>ID</th><th>정확한 경로 · 현재 YAML 값</th><th>추천 범위</th></tr></thead><tbody>'''+''.join(rows)+'''</tbody></table><p><b>확인된 제한:</b> glass_lowe_tint는 현재 _Surface=0, alpha=1, 기본 불투명 queue라 투명 매장 유리의 그대로 재사용 후보에서 제외했습니다. X01 B&C 통로 사진은 층을 확정하기 부족하여 제외했습니다. 승강기 프레임은 층·구역이 확보된 참조가 부족하여 추천하지 않습니다. 검은 광택 바닥 띠/내부 작은 타일도 정확한 기존 재질 대응을 확정하지 않았습니다.</p></main><footer>2026-10-10 로컬 검토. 원본 경로·SHA256·출처·층 식별 근거·기존 재질 속성은 detail-reference-ledger.json에 기록. 참조 원본의 공개 재사용 허가 미확인; 로컬 검토용. 사진·재질·Unity 장면 변경 없음.</footer></html>'''
(OUT/'detail-reference-sheet.html').write_text(page)
summary='''# 1F/2F finish detail handoff

Retained originals only: directly viewed 6; accepted 5; excluded 1 for ambiguous floor. No new browser search, downloads, Unity calls or Assets/canonical writes.

- Open `detail-reference-sheet.html` for five original-image cards, exact paths, floor/zone/date confidence and nine existing material candidates.
- `detail-reference-ledger.json` contains original SHA256 and retained receipt matches, provenance and material YAML values/texture resolution.
- Structural authority is **zero**. All structure comes from floorplans; no image-derived layout, positions, room envelopes, voids, heights or dimensions.
- D01: 1F Life4Cuts/locker-sign metal cladding (source-context floor confidence; no floor sign in photo).
- D02: 2F hall, explicit visible floor sign; pale column surface and glossy gray floor; 2023 source.
- D03: 2F lounge, source-caption floor identification; granular gray glossy floor, silver panel, dark fascia and pale matte wall; 2023 source.
- D04: 2F Namcheon, source says 2F and pictured name matches; dark fascia/frame, transparent glass, silver side panels; published 2025.
- D05: Exit9 door frames/glass/handles; exact photographed zone; 2F association from retained MAIN2F classification, no floor digit in image; published 2026.
- X01: B&C corridor img_2 excluded. Earlier lane called it 1F, but the retained article covers both floors without a sufficient image-specific floor link. Its finish cannot drive floor-specific changes.
- No secure elevator-frame recommendation. No native owner assignment or material application performed.

Use `floor_granite_tile`, `metal_stainless_hairline`, `Hall_Column_White`, `Tenant_FasciaBand`, `Tenant_Glass`/`Kit_GlassClear`, and `wall_painted_gypsum_fine` as appearance candidates only. Existing `glass_lowe_tint` is opaque (`_Surface=0`, alpha=1, queue=-1) and is not recommended unchanged for transparent storefront glazing. Material names do not establish the physical material in a photograph. Exact paths/properties and supplemental alternatives are in the sheet/ledger. Root/integrator own any native-face selection and same-lighting comparison.

Original source rights remain with authors/hosts; no open reuse license established. No photos imported into Unity or published.
'''
(OUT/'detail-reference-handoff.md').write_text(summary)

audit=dict(schema='chooguard.finish-detail-audit.v1',directly_viewed_count=6,accepted_count=5,excluded_count=1,
           original_hash_checks=6,original_hash_checks_passed=6,material_file_count=len(mat_records),
           material_paths_exist=True,all_nonzero_base_normal_texture_guids_resolved=True,
           source_metadata_paths_exist=True,no_new_searches_or_downloads=True,
           writes_only_owned_scope=True,unity_or_assets_writes=False,geometry_proposals=0,
           artifacts=[dict(path=str(p.relative_to(ROOT)),sha256=sha(p),bytes=p.stat().st_size) for p in [OUT/'detail-reference-ledger.json',OUT/'detail-reference-sheet.html',OUT/'detail-reference-handoff.md']])
(OUT/'detail-reference-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in audit.items() if k!='artifacts'},ensure_ascii=False))
