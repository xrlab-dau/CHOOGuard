"""Build the editable, one-page A4 KORAIL development questionnaire."""
from datetime import datetime, timezone
from pathlib import Path
from docx import Document
from docx.shared import Mm, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'docs/korail/CHOOGuard_KORAIL_개발질의서_A4_1p.docx'
TITLE = '「비상대응 훈련 VR 프로그램 개발」 질의서'
INTRO = '실제 현장에 적용 가능한 프로그램 개발을 위해 아래 사항의 확인 및 관련 자료 제공을 요청드립니다.'
QUESTIONS = [
    ('훈련 목표·범위', '현재 훈련의 주요 어려움과 개선 목표, 우선 구현할 비상상황·대상 장소·직무·훈련 인원은 무엇입니까? 시제품과 최종 구축물의 필수 기능·범위(교관 통제 등)도 확인 부탁드립니다.'),
    ('실제 대응 절차', '최신 비상대응 매뉴얼·SOP, 직무별 조치·지휘/보고/인계 체계, 장치 사용 조건, 교육자료·평가표를 제공받을 수 있습니까? 개정일과 적용 범위도 부탁드립니다.'),
    ('공간·설비 자료', '보유 구상도, 최신 평면/준공/피난/설비도면(CAD/BIM/PDF), 주요 치수·기준좌표, 안전설비 목록·설명서, 사진·영상·3D 자료를 제공 또는 열람할 수 있습니까?'),
    ('평상시 운영·돌발 상황', '이용객 동선·혼잡도·설비 운영 자료와 비식별 사고·장애·훈련 기록을 제공받을 수 있습니까? 반드시 재현할 변칙 상황, 요구 정확도와 실시간 설비/API 연동 필요 여부도 알려주십시오.'),
    ('VR 기기·사용 여건', '보유·사용 가능한 VR 기종·수량, 독립형/PC 연결형, 컨트롤러·트래킹, 유무선 허용 여부와 훈련 공간은 어떻게 됩니까? 장비 대여·현장 시험과 Desktop 병행이 가능하거나 필요합니까?'),
    ('구축 장비·폐쇄망 사양', '개발·시험·교육용 PC/서버(폐쇄망 포함)의 CPU·GPU 모델/VRAM, RAM, SSD/HDD·여유 공간, OS·버전/비트수, 장비 수량을 알려주십시오. 시험 장비 제공·대여 및 외부 장비 반입이 가능합니까?'),
    ('망 구성·설치·연계', '실행 환경은 폐쇄망·내부망·독립 PC 중 무엇입니까? 인터넷·장치 간 통신·클라우드/API 사용, SW/VR 실행환경 설치 권한·오프라인 인증, 반입·보안검사·업데이트 절차와 동시훈련/서버·기존 교육시스템 연계 조건은 무엇입니까?'),
    ('보안·촬영·자료 활용', '원본·중간 결과·3D 맵·훈련 기록의 보안등급, 저장 위치·보관 기간·처리/반출·외부 AI 처리·재사용·공개·권리/라이선스 조건은 무엇입니까? 현장 촬영·측량의 허용 구역·기기·시간·용도·금지 대상과 서면 승인 절차도 부탁드립니다.'),
    ('검수·일정·협조', '필수 성능·정확도·훈련 평가/이수 기준, 검수 주체·구축 일정·산출물 형식·유지보수 범위는 어떻게 됩니까? 현업 인터뷰·시범훈련과 실제 현장 적용 효과의 검증에 협조가 가능합니까?'),
]
REPLY = '회신 안내: 항목별 서면 답변과 자료의 형식·개정일·제공 시점·담당 부서를 부탁드립니다. 미정 또는 제공이 어려운 항목은 확인 가능 시점과 대체자료·열람 방법을 안내해 주시면 감사하겠습니다.'

def font(run, size=12, bold=False, color='111111'):
    run.font.name = 'Arial'
    run.font.size = Pt(size)
    run.bold = bold
    run.font.color.rgb = RGBColor.from_string(color)
    run._element.get_or_add_rPr().rFonts.set(qn('w:eastAsia'), '맑은 고딕')
    lang = OxmlElement('w:lang'); lang.set(qn('w:val'), 'ko-KR'); lang.set(qn('w:eastAsia'), 'ko-KR')
    run._element.get_or_add_rPr().append(lang)

def main():
    doc = Document()
    sec = doc.sections[0]
    sec.page_width = Mm(210); sec.page_height = Mm(297)
    sec.top_margin = Mm(17); sec.bottom_margin = Mm(18)
    sec.left_margin = Mm(19); sec.right_margin = Mm(19)
    sec.header_distance = Mm(8); sec.footer_distance = Mm(8)
    normal = doc.styles['Normal']; normal.font.name = 'Arial'; normal.font.size = Pt(12)
    normal._element.get_or_add_rPr().rFonts.set(qn('w:eastAsia'), '맑은 고딕')
    normal.paragraph_format.line_spacing_rule = WD_LINE_SPACING.EXACTLY
    normal.paragraph_format.line_spacing = Pt(16.5)
    normal.paragraph_format.space_after = Pt(4.5)
    doc.core_properties.title = TITLE
    doc.core_properties.subject = '코레일 개발 요구사항·자료·구축 환경 확인'
    doc.core_properties.author = 'CHOOGuard 개발팀'
    doc.core_properties.last_modified_by = 'CHOOGuard 개발팀'
    doc.core_properties.created = datetime(2026, 9, 7, tzinfo=timezone.utc)
    doc.core_properties.modified = datetime(2026, 9, 7, tzinfo=timezone.utc)
    doc.core_properties.keywords = 'KORAIL, VR, 개발 질의서'
    doc.core_properties.comments = ''
    p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.line_spacing = Pt(25); p.paragraph_format.space_after = Pt(5); p.paragraph_format.keep_with_next = True
    font(p.add_run(TITLE), 17, True, '17365D')
    p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_after = Pt(11); p.paragraph_format.keep_with_next = True
    font(p.add_run('수신: 한국철도공사 담당부서  |  작성: CHOOGuard 개발팀  |  2026. 9. 7.'), 9, False, '555555')
    p = doc.add_paragraph(); p.paragraph_format.space_after = Pt(10); p.paragraph_format.keep_with_next = True
    font(p.add_run(INTRO), 11)
    for i, (heading, body) in enumerate(QUESTIONS, 1):
        p = doc.add_paragraph(); p.paragraph_format.keep_together = True
        p.paragraph_format.left_indent = Mm(6); p.paragraph_format.first_line_indent = Mm(-6)
        p.paragraph_format.space_after = Pt(7)
        font(p.add_run(f'{i}. {heading}  '), 12, True, '17365D')
        font(p.add_run(body), 12)
    p = doc.add_paragraph(); p.paragraph_format.space_before = Pt(5); p.paragraph_format.keep_together = True
    p.paragraph_format.line_spacing = Pt(13)
    font(p.add_run(REPLY), 9.5, False, '444444')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True); doc.save(OUTPUT)
    source = f'# {TITLE}\n\n{INTRO}\n\n' + '\n\n'.join(f'{i}. **{h}** {b}' for i,(h,b) in enumerate(QUESTIONS,1)) + f'\n\n{REPLY}\n'
    (OUTPUT.parent/'development-questionnaire-v1.md').write_text(source, encoding='utf-8')
    print(OUTPUT.relative_to(ROOT)); print('Korean characters including headings:',len(INTRO+REPLY+''.join(h+b for h,b in QUESTIONS)))

if __name__ == '__main__': main()
