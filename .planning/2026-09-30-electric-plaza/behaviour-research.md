# Electric-plaza behaviour research (Korea) — 2026-09-30

Scope: how electrical fires / accidents at public equipment (분전반, 자판기, 충전기, 쓰레기통 등) are really handled in Korea, for the CHOOGuard training game (Busan Station twin).
Rules used: every fact has a URL and a quote or exact paraphrase; `[INFERENCE]` = my arithmetic/reasoning, not a source statement; "no source found" = searched, nothing usable.
Source tiers: **P** = primary/official (law text, agency data, agency page), **S** = secondary (news quoting an agency, association, mirror, blog). Tier is given per fact.
Evidence dates: "today" = 2026-09-30, so the KESCO 2025 report (published Sep 2026) exists; detailed cause tables are only public for 2022–2024 (open-data CSV), so 2024 is the "latest full year" below and 2025 headline numbers are added.

---

## 1. Electrical-fire causes and where they start

- **Volume (P-via-S).** KESCO 전기재해 통계분석 2024: 화재 37,610건 중 전기화재 8,634건 = 22.9% ("최근 10년 중 가장 높은 수치"). 사상자 372명(사망 40, 부상 332), 재산피해 1,701억원. https://www.electimes.com/news/articleView.html?idxno=360346 (KESCO report summarised by 전기신문). 2023: 8,871건 = 22.8% https://www.electimes.com/news/articleView.html?idxno=343100
- **Ignition factor, 2024 (P, official open data CSV, 공공누리1).** 미확인 단락 3,034 (35.1%); 절연열화에 의한 단락 1,683 (19.5%); 트래킹에 의한 단락 1,292 (15.0%); **접촉 불량 1,076 (12.5%)**; **과부하 및 과전류 720 (8.3%)**; 압착·손상 324 (3.8%); 누전·지락 253 (2.9%); 반단선 147 (1.7%); 층간 단락 105 (1.2%); 기타 0 (sum = 8,634). https://www.data.go.kr/data/15069690/fileData.do (CSV: https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003243265&fileDetailSn=1&insertDataPrcus=N). 2023: 미확인 3,020 (34.0%), 절연열화 1,611, 트래킹 1,307, 접촉불량 901 (10.2%), 과부하 698 (7.9%), 기타 563 — same file.
- "미확인 단락" is a bucket, not a cause: the news report defines it as "접촉 불량이나 절연열화, 압착ㆍ손상 등 명확한 원인 규명이 어려운 전기화재"; KESCO's reason: 건물 대형·고층화로 현장이 소실돼 원인 규명 한계. 2020–Jun 2025 = 15,297건, 179 dead. https://www.fpn119.co.kr/240696 (S). 2025 headline: 전기적 요인 9,376건 = 24.5% of 38,332 (기타 전기적 요인 포함 — restated basis; the same report restates 2024 as 24.7%), 미확인 3,422 (36.5%), 절연열화 1,646 (17.6%). https://m.blog.naver.com/ymirsoft/224401505414 (S, blog transcription of KESCO 2025 report).
- **Where (2024, KESCO).** 설비별: 배선 및 배선기구 2,050 (23.7%) — 콘센트 437, 옥내배선 전선 330, 전기기기용 전선·코드 326; 전기설비 1,063 (12.3%). 장소별: 주거 2,990 (34.6%), 산업시설 1,535 (17.7%). https://www.electimes.com/news/articleView.html?idxno=360346. 2023 also lists 계절용기기 960 (10.8%), 주방기기 499, 조명·간판 417, 생활기기 296. https://www.electimes.com/news/articleView.html?idxno=343100
- **Panels.** 배전반·분전반 = 2,685건 = 39.4% of 전기설비 fires (2014–2018, NFDS via peer-reviewed paper). https://www.j-kosham.or.kr/journal/view.php?number=8201&viewtype=pubreader (S). 2014: 363건 = 45% of 803 전기설비 fires; causes "절연불량과 보호장치 오동작, 수해·수분, 과부하, 부식". https://www.electimes.com/news/articleView.html?idxno=133054 (S). No 2024 panel-only count found.
- **When.** 전기화재 peak 04–06시, least 14–16시 ("사람이 활동하지 않는 심야"); peak month 8월 1,074건 (2024). https://m.blog.naver.com/ymirsoft/223997291276 (S), electimes 360346.
- **자판기:** no figure found in KESCO 2023/2024/2025 summaries, 소방청 or 소비자원 pages (see §2).
- Design consequence: weight the game's fault generator roughly as 접촉불량/절연열화/트래킹/과부하 (≈55% of classified fires in 2024) plus a large "cause unknown" tail, and put most electrical hazards at outlets, cords and panels, with fires developing while nobody is watching.

## 2. Vending machines (자판기)

- **Statistics: no source found.** Searched KESCO 통계분석 (2023–2025 summaries), 소방청 NFDS/e-나라지표, 한국소비자원 CISS, news for "자판기/자동판매기 화재·감전" — no vending-specific count, no snack-vs-drink split, no compressor/condenser-fan/power-cord/relay breakdown. (The only "자판기" fire hit was a factory that makes vending machines, Pyeongtaek 2024-09-05 — irrelevant. https://www.ytn.co.kr/_ln/0103_202409060321592896)
- **Closest official proxy — refrigeration appliances (김치냉장고, 정책브리핑 card news; the fetched page does not name the data agency).** 2020–22: 화재 909건, 590건 (65%) 전기적 요인; 세부: 트래킹 34%, 절연열화 24%, 미확인 단락 24%, 기타 7%, 접촉불량 6%, 과부하 3%, 압착·손상 2%. Advice: 습한/물 튀는/먼지 많은 곳 설치 금지, 누전차단기·접지단자 콘센트, 단독 콘센트. https://www.korea.kr/news/policyNewsView.do?newsId=148922999 (S)
- **Typical mechanism (local paper, 2018, fire-investigator style article — author/agency not verified).** "냉장고 뒷면 하부에 설치된 컴프레셔와 냉각팬, 전기배선이 있는 부분에 먼지가 쌓여 있다가 과전류로 인한 스파크에 의해 화재", plus "노후 전선이 눌리거나 꺽인 부분의 절연열화", 제상히터/기판 이상; advice: 3년마다 뒷면 청소, 10년 이상 노후품 점검. https://www.gocj.net/news/articleView.html?idxno=93099 (S)
- Legal hooks that apply to any plug-in vending cabinet: 금속제 외함 접지 (산안규칙 제302조), 습윤장소 저압 기기 누전차단기 (제304조). https://www.law.go.kr/DRF/lawService.do?OC=test&target=law&ID=007363&type=XML (P)
- Design consequence: model the vending machine as a refrigeration-type load (compressor/fan area with dust, cord, plug) whose signature fault is dust-plus-tracking/insulation ageing, and state in the UI that the fault weights are game assumptions, not vending statistics.

## 3. Distribution / panel (분전반) fires and public-building rules

- **Causes (peer-reviewed survey, Seoul 100 self-use sites, thermography 2018).** 14 of 100 panels showed abnormal heat: 절연파괴 7, 접촉불량 5, 복수결함 2; "분기회로 단자의 볼트가 헐거워지면서 전선과 단자의 접촉불량으로 이상과열"; "배전반과 분전반은 절연파괴, 접촉불량, 먼지 등에 의해 화재의 위험이 상시 내재"; 사용연한 "권고사항은 있으나 강제규정이 없어 노후 … 교체되지 않고 그대로 사용". https://www.j-kosham.or.kr/journal/view.php?number=8201&viewtype=pubreader (S)
- **Fire-investigator view (서울소방재난본부 화재조사관, 2026-01).** "먼지·거미줄·유증기가 쌓인 분전반과 천장 속 접속함", 누전차단기가 반복해서 떨어져도 원인 확인 없이 다시 올려 쓰는 행위 = "전형적인 전기화재의 출발점". https://www.keaj.kr/news/articleView.html?idxno=6147 (S)
- **KEC 232.84 (P, 한국전기설비규정 2023-12-14 개정본 text; later revisions not checked).** "노출된 충전부가 있는 배전반 및 분전반은 취급자 이외의 사람이 쉽게 출입할 수 없도록 설치하여야 한다"; "한 개의 분전반에는 한 가지 전원(1회선의 간선)만 공급"; "옥내에 설치하는 배전반 및 분전반은 불연성 또는 난연성 … 시설". https://www.eom.co.kr/documents/1214%20%20%ED%95%9C%EA%B5%AD%EC%A0%84%EA%B8%B0%EC%84%A4%EB%B9%84%EA%B7%9C%EC%A0%95%20%EA%B0%9C%EC%A0%95%20%EC%A0%84%EB%AC%B8.pdf (copy of 산업통상자원부 공고 제2023-875호)
- **Circuit labelling — KEC 231.2.4 식별 (same PDF).** "혼동 가능성이 있는 곳은 개폐장치 및 제어장치에 표찰이나 기타 식별 수단을 적용하여 그 용도를 표시"; "보호 장치는 보호되는 회로를 쉽게 알아볼 수 있도록 배치하고 식별할 수 있도록 배치". (232.84 itself names only 사용전압 표시 when two supplies share a board; no numeric label rule found.)
- **Doors / clearance (산업안전보건기준규칙, P).** 제301조③ 분전반·제어반 문·경첩 패널 "견고하게 고정"; 제310조① 조작부 점검·보수 시 "폭 70센티미터 이상의 작업공간". https://www.law.go.kr/DRF/lawService.do?OC=test&target=law&ID=007363&type=XML
- **Who operates.** 전기안전관리법 시행규칙 제30조②: 전기안전관리자 직무 = 운전·조작 또는 그 감독 + "전기재해의 발생을 예방하거나 그 피해를 줄이기 위하여 필요한 응급조치". https://www.law.go.kr/DRF/lawService.do?OC=test&target=law&ID=014058&type=XML (P). Public advice: "전기기기의 스위치 조작은 아무나 함부로 하지 않도록" https://www.chuncheon.go.kr/disaster-safety/safety-behavior-tips/life-safety/electric-gas/electric/ (P, municipal).
- Design consequence: a panel should be a locked cabinet that only staff/전기 담당 open, with a labelled circuit list on the door, and the "loose terminal / dust / aged breaker" faults should show up as hot terminals visible only after opening (or by thermography).

## 4. Bystander / staff response to an electrical fire; extinguishers

- **Order (MOEL 울산지청 doc quoting NFSC 101 해설, P).** "전기기계·기구 또는 전선에서 화재가 발생한 경우, 먼저 차단기를 내린 후 소화"; "전기가 차단되지 않았을 경우 전기설비 및 전선에 방수하지 말 것"; 초기 소화 실패 시 "지체 없이 대피". https://www.moel.go.kr/local/ulsan/common/downloadFile.do?file_seq=21171100454&bbs_seq=1508307338806&bbs_id=LOCAL5 (PDF parsed locally)
- **Why no water.** "전기화재 시에는 감전의 위험이 있으므로 물을 진화용으로 사용하면 안된다" (KAIST safety manual 3.4.2.3, S) https://safety.kaist.ac.kr/filePortalViewer.do?file=%2Fportal%2Fmanual%2Fkor%2F306048e6-cfbf-4566-b449-f3a35b70faf0.pdf ; same wording in 대한안전교육협회 https://m.blog.naver.com/ksea1004/222227528709 (S).
- **National action guide (안전디딤돌/국민안전24, P).** 불을 발견하면 "불이야" 또는 비상벨; "불길이 천정까지 닿지 않는 작은 불이라면 소화기나 물양동이 등을 활용"; 커지면 젖은 수건으로 계단 대피; "안전하게 대피한 후 119에 신고 … 신고하느라 대피시간을 놓치지 않도록". http://mepv2.safekorea.go.kr/safety_guide/safeGuide/showDetail_02011_22.html . Seoul's 전기사고 행동요령: "가능한 경우 전기 스위치를 차단", 소화기·옥내소화전으로 초기소화. https://safecity.seoul.go.kr/file/ACT/DHTY324.pdf (P)
- **Law: what may be used on 전기화재 (NFTC 101, 시행 2024-07-25, table 2.1.1.1 read from law.go.kr image; P).** C급 = "전류가 흐르고 있는 전기기기, 배선과 관련된 화재" (1.7.1.9). ○ = 이산화탄소, 할론, 할로겐화합물, **분말 인산염류(ABC)·중탄산염류(BC)**, 고체에어로졸; 산알칼리·강화액·포·물침윤 = "*" (only if type-approved for that fire class); 마른모래/팽창질석 = –. https://www.law.go.kr/LSW/admRulInfoR.do?admRulSeq=2100000244810&chrClsCd=010201 (image flSeq=142858069)
- **Electrical rooms / panels (NFTC 101 table 2.1.1.3, image flSeq=163994719; P).** 발전실·변전실·송전실·변압기실·배전반실·통신기기실·전산기기실: "해당 용도의 바닥면적 50㎡마다 적응성이 있는 소화기 1개 이상" or gas/powder/solid-aerosol/cabinet 자동소화장치; 관리자 출입이 곤란한 변전·배전반실: "25㎡마다 능력단위 1단위 이상"; 1.7.1.3(3) defines 전기설비용 자동확산소화기 for "변전실, 송전실, 변압기실, 배전반실, 제어반, 분전반". 2.1.1.4: 소형 소화기 보행거리 20 m 이내; 2.1.1.6: 높이 1.5 m 이하, "소화기" 표지. 2.1.3: CO2/할로겐 소화기는 지하층·무창층·밀폐된 거실 20㎡ 미만에 설치 불가 (배기 개구부 있으면 가능).
- Design consequence: enforce the sequence 알림/119 → 차단기 내림 → C급(ABC powder or CO2) 소화기 only for a small fire → 대피, make water/hose a hard fail while the panel is live, and give panel/electrical rooms a powder extinguisher within 20 m.

## 5. Phone / power-bank lithium battery thermal runaway; charging kiosks

- **Statistics (소방청).** 2020–2024: 리튬이온 배터리 화재 678건 (2020: 98 → 2024: 117); 전동킥보드 485 + 전기자전거 111 + 전동오토바이 31 = PM 92%. https://www.yna.co.kr/view/AKR20250829074800518 (S quoting 소방청). 2019–2023: 612건, "312건(51%)이 과충전"; 소방청 이용수칙: 냄새·소리·변색 시 사용 중지; "가연물이 없는 곳에 배터리를 두고 안전한 장소로 이동한 후 119에 신고". https://v.daum.net/v/20240721161110952 (S quoting 소방청)
- **보조배터리 specifically.** 소방청 has no 보조배터리 category ("발화 열원 통계 … 보조배터리는 빠져있다"). https://www.khan.co.kr/article/202605200600121 (S). Seoul 소방재난본부: 보조배터리 화재 2023: 15 → 2024: 37 → 2025: 55 = 107 (사망 2, 부상 5); 소비자원 위해사례 22건(2021) → 136건(2024). https://www.yna.co.kr/view/AKR20260619068300004 (S)
- **Response guidance.** 경남소방본부: 즉시 119 신고, "안전거리를 확보하고 배터리 인근 접근금지"; "배터리 전용 소화기"로 팔리는 제품은 미인증. https://www.kmib.co.kr/article/view.asp?arcid=0028856565 (S). KBS 재난포털 (경일대 이영주 교수, S): 겉불이 꺼져도 내부 열반응이 계속돼 재발화; 소형 배터리는 물/일반 소화기로 불꽃 진압 후 "배터리를 물에 완전히 담가" 냉각; 담요·이불 덮기는 위험. https://md.kbs.co.kr/prepare/talktalkView?seq=16 . 국립소방연구원 (news, Dec 2025; 12일/15일 dates differ between outlets): 가정용 배터리 화재는 "배터리를 물로 침수시켜 신속하게 냉각시키는 방식" 개발. https://www.safetimes.co.kr/news/articleView.html?idxno=237448 (S). Field firefighters (KBS 2024): large-pack fires got bigger with water at peak, but water "잔불 정리"에 효과. https://v.daum.net/v/HcVcozwMvy?f=p (S)
- **Delay hazard.** 서대문 경비초소: 경비원 "충전을 마친 지 6시간 정도 지난 후 배터리가 터졌다" (testimony, 2026-05-14). https://www.khan.co.kr/article/202605200600121 (S). No official re-ignition time window found.
- **Stations (서울교통공사, closest to a station operator).** "공사 관할 전 역사에 배터리 냉각을 위한 수조를 비치", 방열장갑·방열집게; rule: 연기 난 객실에서 다른 객실로 이동 후 비상통화장치로 직원에게 알림 (선로 대피 금지). https://www.mt.co.kr/policy/2026/06/08/2026060813431695846 (S). 최근 2년 9건 (연신내·고속터미널·신림·서울·신당·삼각지역 등). https://www.korearailroad.kr/news/articleView.html?idxno=184720 (S). 2026-05-18 신림역: 역무원이 보조배터리를 역사 내 수조에 넣어 확산 방지, 승객 3명 경상. khan 202605200600121.
- **Public phone-charging kiosk / locker fire: no source found.** Closest: 안성 고교 교실 휴대폰 보관함 2025-11-10 (휴대전화 20개 + 보조배터리 1개), 직원이 소화기로 약 15분 만에 자체 진화 후 119, 피해 272만원, 원인 조사 중. https://www.mt.co.kr/society/2025/11/10/2025111023074820649 ; http://anseongnews.com/front/news/view.do?articleId=ARTICLE_00036491 . Charger-terminal tracking fire with no phone attached (5 V, 0.2 mm electrode gap): https://www.kfpa.or.kr/webzine/202205/sub/disasters9.html (S)
- Design consequence: script a power-bank event as smoke → keep away and tell staff (no grabbing) → staff use heat gloves/tongs to drop it into the cooling tub → 119 if it spreads, and add a "hot again" relapse timer to the drawer/locker after a battery incident.

## 6. Electric-shock rescue (감전)

- **Bystander order (KOSHA official blog).** "가장 먼저 전원을 차단합니다"; if not possible, rescuer wears 고무장갑·고무장화·마른 면양말, stands on dry board, uses 전류가 통하지 않는 나무 막대; "전원이 차단되지 않았다면 환자와 접촉하면 안됩니다"; 의식·호흡 확인, 호흡 정지 시 인공호흡, 맥박 없으면 심장마사지; 심정지 시 현장 CPR; "모든 전기화상은 병원에서 치료"; 의식이 분명해 보여도 반드시 진찰. https://m.blog.naver.com/koshablog/221292170156 (P/S: agency's own blog)
- **KESCO (2026-07-09 press).** "감전사고가 발생하면 우선 전원을 차단한 뒤 119에 신고하고 구조 과정에서는 고무장갑이나 마른 목재 등 절연체를 이용해 2차 감전사고를 예방". http://www.safetoday.kr/news/articleView.html?idxno=107899 (S). Seoul action guide: "감전이 된 사람을 바로 도우려 하지 마세요 – 2차 감전의 위험"; current table 1 mA 짜릿, 5 통증, 10 자제불능, 20 경련, 50 호흡곤란, 100 치명적. https://safecity.seoul.go.kr/file/ACT/DHTY324.pdf (P)
- **Numbers (KESCO 2024).** 감전 사상 371명 (사망 28, 부상 343): 충전부 직접접촉 162 (43.7%), 아크 122 (32.9%), 플래시오버 55 (14.8%); 저압 266 (사망 15) vs 고압·특고압 82 (사망 12); 전기기술자 128명 (34.5%) — 공사·보수 중 104. https://www.electimes.com/news/articleView.html?idxno=360346 . 2025: 공사·보수 중 104명 (31%), 운전·점검 중 51명 (15.2%); 저압 차단기/개폐기 28명, 콘센트 22명. https://m.blog.naver.com/ymirsoft/224401505414 (S)
- **Vending-machine 누전 shock cases: no source found.** Generic prevention only: 접지, 누전차단기, 젖은 손 금지 (safekorea "전기 안전"). http://mepv2.safekorea.go.kr/safety_guide/safeGuide/showDetail_02019_18.html (P). KESCO help: 전기안전 콜센터 1588-7500; 고장 123. Same page.
- Design consequence: a shock victim must be freed only after the correct breaker is off (or with an insulating tool), never by touching; wrong touch = second victim; then check breathing, CPR/AED, 119, and remind that all electrical burns need a hospital.

## 7. Lock-out / tag-out (잠금·표지) for breakers

- **Law (P).** 산업안전보건기준규칙 제92조②: 기동장치에 "잠금장치를 하고 그 열쇠를 별도 관리하거나 표지판을 설치하는 등 필요한 방호 조치". 제319조②: 전원 도면 확인 → 차단 후 단로기 개방·확인 → "차단장치나 단로기 등에 잠금장치 및 꼬리표를 부착" → 잔류전하 방전 → 검전기로 충전 여부 확인 → 필요 시 단락접지. 제319조③: 재투입 시 작업기구 제거·통전 확인, 작업자 이격 확인, "잠금장치와 꼬리표는 설치한 근로자가 직접 철거할 것", 이상 유무 확인 후 투입. https://law.go.kr/LSW//lsLinkCommonInfo.do?lsJoLnkSeq=1020911383&chrClsCd=010202 (시행 2026-03-02) ; 제92조: https://www.law.go.kr/DRF/lawService.do?OC=test&target=law&ID=007363&type=XML
- **KOSHA GUIDE E-91-2016 (text via mirror; official kosha.or.kr copy not located).** 6.3(1) "각 잠금장치·표지는 이를 설치한 작업자에 의하여 … 철거"; (2) exception only if 사업주 procedure: confirm the installer is not on site, notify installer, confirm installer knows before work resumes; 4.5(2)(라) 장치에 "작업자의 소속, 성명, 전화번호" 기재; 4.5(3) 표지에 "작동금지·개방금지·기동금지" 등; 4.7(2) 표지는 "하나의 경고 장치에 불과"하고 책임 승인 작업자 허가 없이 제거·무시 불가; 표지 부착 위치는 잠금과 같은 장소. https://psm-safety-environment.tistory.com/entry/%EC%97%90%EB%84%88%EC%A7%80-%EC%B0%A8%EB%8B%A8%EC%9E%A5%EC%B9%98%EC%9D%98-%EC%9E%A0%EA%B8%88%C2%B7%ED%91%9C%EC%A7%80%EC%97%90-%EA%B4%80%ED%95%9C-%EA%B8%B0%EC%88%A0%EC%A7%80%EC%B9%A8E-91-2016 (S). Scope excludes 활선작업 by controlled specialists.
- **Procedure summaries.** KOSHA blog, 6 steps: 작업·전원차단 공지 → 전원 확실 차단 → 잔류에너지 확인 → 잠금장치·표지판 설치 (담당작업자가 열쇠 보관) → 정지·작업자 안전 확인 → "담당작업자가 직접 잠금장치와 표지판을 해제". https://m.blog.naver.com/koshablog/223238223811 ; tag content "작업 내용, 근로자 이름, 작업 종료 예정 시간, 경고 메시지". https://m.blog.naver.com/koshablog/223627778437 . MOEL 8-step leaflet (부산): …"7 LOTO 해제: 담당작업자가 직접", "8 기계 설비 재가동 종료 후 관련 작업자에게 공지". https://www.moel.go.kr/local/busan/common/downloadFile.do?bbs_id=LOCAL1&bbs_seq=20210500425&file_seq=20210500561 (P, PDF parsed locally)
- Design consequence: breaker-off must be a sequence (announce → off → verify with tester → lock/tag with name+time → work → inspect → announce → the same person removes it), and other NPCs/players must be blocked from re-energising a tagged breaker.

## 8. Who comes for an electrical fire in a station

- **119 소방대.** KORAIL passenger guidance: "화재 발생 시 119에 신고하고, 여유가 있다면 … 소화기로 불을 끕니다". https://info.korail.com/info/contents.do?key=970 (P). Recent station example (서울교통공사 이대역 2026-09-22): 전기실 배터리 화재·연기 20:06; 마포소방서 차량 14대·50명, 초진 20:25, 완진 20:56; 시민 대피, 인명피해 없음; 열차 무정차 통과 (~40분, 20:24–21:03 per 뉴스핌). https://www.nocutnews.co.kr/news/6582387 ; https://www.newspim.com/news/view/20260922001267 (S). 2016 마곡나루역 기계실 배전반 화재: "직원과 승객 20여 명이 긴급 대피". https://www.electimes.com/news/articleView.html?idxno=133054 (S)
- **KORAIL internal chain (P).** 사고현장 → 역장(인접역) 또는 바로 철도교통관제센터장 → … ; 현장사고수습본부 units include "차량복구반/시설복구반/전기복구반, 섭외지원반"; 유관기관 "군·경·소방서·지자체·국가철도공단 등" are dispatched via 철도교통관제센터; 운영상황실 has 전기상황팀 (전기안전기술단). https://info.korail.com/info/contents.do?key=969 . No station-level electrical-fire SOP naming 전기 담당/한전 found.
- **전기안전관리자 / 전기 담당.** 전기안전관리법 제22조: 자가용전기설비 소유·점유자는 "전기안전관리자를 선임"(또는 위탁·대행); 시행규칙 제30조 duties = 운전·조작·응급조치 (see §3). https://www.law.go.kr/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1027725465 (P)
- **한국전력 (incoming supply).** 기본공급약관 제27조: 수급지점 = "한전의 전선로 또는 인입선과 고객 전기설비와의 연결점"; 제28조② 한전 설비는 수급지점까지; 제33조① 인입구배선 및 구내 설비는 고객 소유. So a fault in the station's own panel is the customer's/전기안전관리자's problem; KEPCO handles the line to the boundary. https://home.kepco.co.kr/kepco/front/html/CY/D/C/CYDCHP00104.html (P). 고장신고 123 / KESCO 1588-7500: https://safecity.seoul.go.kr/file/ACT/DHTY324.pdf
- Design consequence: on a panel fire the player's calls must be 119 first, station chief/관제센터 and the 전기 담당 (전기복구반) next, and 한전 only when the fault is on the supply side of the 수급지점 (e.g. meter/incoming line).

---

## Design facts to use (≤25 rows)

| # | Fact | Number | Source URL |
|---|---|---|---|
| 1 | Electrical fires, Korea 2024 | 8,634 = 22.9% of 37,610 | https://www.electimes.com/news/articleView.html?idxno=360346 |
| 2 | Ignition mix 2024 (of 8,634) | 미확인 35.1%, 절연열화 19.5%, 트래킹 15.0%, 접촉불량 12.5%, 과부하 8.3% | https://www.data.go.kr/data/15069690/fileData.do (CSV) |
| 3 | Equipment 2024 | 배선·배선기구 2,050 (23.7%), 콘센트 437; 전기설비 1,063 (12.3%) | https://www.electimes.com/news/articleView.html?idxno=360346 |
| 4 | Panels' share of 전기설비 fires | 39.4% (2,685/6,814, 2014–18); 45% (363/803, 2014) | https://www.j-kosham.or.kr/journal/view.php?number=8201&viewtype=pubreader ; https://www.electimes.com/news/articleView.html?idxno=133054 |
| 5 | Panel thermography survey | 14/100 abnormal: 절연파괴 7, 접촉불량 5, 복수 2; loose branch-terminal bolts | https://www.j-kosham.or.kr/journal/view.php?number=8201&viewtype=pubreader |
| 6 | Time of electrical fires | peak 04–06h, low 14–16h; Aug 2024 = 1,074 (most) | https://m.blog.naver.com/ymirsoft/223997291276 ; https://www.electimes.com/news/articleView.html?idxno=360346 |
| 7 | KEC 232.84 panel access | 노출 충전부 배·분전반 = 취급자 이외 출입 불가; 불연/난연 캐비닛 | https://www.eom.co.kr/documents/1214%20%20%ED%95%9C%EA%B5%AD%EC%A0%84%EA%B8%B0%EC%84%A4%EB%B9%84%EA%B7%9C%EC%A0%95%20%EA%B0%9C%EC%A0%95%20%EC%A0%84%EB%AC%B8.pdf |
| 8 | KEC 231.2.4 labelling | 보호장치는 보호되는 회로를 식별 가능하게 배치; 혼동 가능 개폐장치에 표찰 | same KEC PDF |
| 9 | Panel door / workspace | 문 견고 고정 (제301조③); 조작부 작업공간 폭 ≥ 70 cm (제310조①) | https://www.law.go.kr/DRF/lawService.do?OC=test&target=law&ID=007363&type=XML |
| 10 | 국민행동요령 fire | 작은 불(천정 미도달)만 소화기; 대피 후 119; 신고로 대피 지연 금지 | http://mepv2.safekorea.go.kr/safety_guide/safeGuide/showDetail_02011_22.html |
| 11 | Electrical fire order | 차단기 먼저 → 소화; 미차단 시 방수 금지; 실패 시 즉시 대피 | https://www.moel.go.kr/local/ulsan/common/downloadFile.do?file_seq=21171100454&bbs_seq=1508307338806&bbs_id=LOCAL5 |
| 12 | Extinguisher classes for C급 | CO2/할론/할로겐/ABC·BC 분말/고체에어로졸 ○; 물·강화액·포 only if approved (*) | https://www.law.go.kr/LSW/admRulInfoR.do?admRulSeq=2100000244810&chrClsCd=010201 (image 142858069) |
| 13 | Extinguishers in electrical rooms | 50 ㎡당 적응성 소화기 ≥1; 출입 곤란 변전·배전반실 25 ㎡당 1단위; 보행 20 m; 높이 ≤1.5 m | same NFTC 101 (image 163994719; 2.1.1.4, 2.1.1.6) |
| 14 | CO2/halon restriction | 지하층·무창층·밀폐 20 ㎡ 미만 설치 불가 (배기 개구부 있으면 가능) | same NFTC 101 text 2.1.3 |
| 15 | Li-ion fires (소방청) | 678 in 2020–24 (98→117/yr); PM 92%; 51% during over-charge (2019–23, n=612) | https://www.yna.co.kr/view/AKR20250829074800518 ; https://v.daum.net/v/20240721161110952 |
| 16 | Power-bank fires, Seoul | 15 → 37 → 55 (2023–25) = 107; 소방청 has no category | https://www.yna.co.kr/view/AKR20260619068300004 ; https://www.khan.co.kr/article/202605200600121 |
| 17 | Station power-bank kit (Seoul Metro) | 냉각 수조 + 방열장갑 + 방열집게 in every station; passengers move to another car and call staff | https://www.mt.co.kr/policy/2026/06/08/2026060813431695846 |
| 18 | Shinrim station 2026-05-18 | staff put power bank in tub; no spread; 3 minor injuries | https://www.khan.co.kr/article/202605200600121 |
| 19 | Delayed failure | battery burst ~6 h after charging ended (guard testimony) | https://www.khan.co.kr/article/202605200600121 |
| 20 | Phone-locker fire analogue | 20 phones + 1 power bank; staff extinguished ~15 min; 272만원 | https://www.mt.co.kr/society/2025/11/10/2025111023074820649 |
| 21 | Shock casualties 2024 | 371 (28 dead); 충전부 접촉 43.7%, 아크 32.9%, 플래시오버 14.8%; 저압 266 vs 고압 82 | https://www.electimes.com/news/articleView.html?idxno=360346 |
| 22 | Shock rescue | 전원 차단 먼저; 미차단 시 접촉 금지; 절연 장비·마른 나무 막대; CPR; 모든 전기화상 병원 | https://m.blog.naver.com/koshablog/221292170156 |
| 23 | LOTO legal sequence | 도면 확인 → 차단·개방 확인 → 잠금+꼬리표 → 방전 → 검전; 설치자 본인이 철거 | https://law.go.kr/LSW//lsLinkCommonInfo.do?lsJoLnkSeq=1020911383&chrClsCd=010202 |
| 24 | Tag content | 작업 내용, 이름, 종료 예정 시간, 경고; 연락처(소속·성명·전화번호); "작동금지" | https://m.blog.naver.com/koshablog/223627778437 ; KOSHA GUIDE E-91 mirror (§7) |
| 25 | Responders | 119; 역장/관제센터 → 전기복구반; 전기안전관리자 (운전·조작·응급조치); 한전 up to 수급지점 | https://info.korail.com/info/contents.do?key=969 ; https://www.law.go.kr/DRF/lawService.do?OC=test&target=law&ID=014058&type=XML ; https://home.kepco.co.kr/kepco/front/html/CY/D/C/CYDCHP00104.html |

## Assumptions with no public source

- Any vending-machine statistic: fire/shock counts, snack vs drink split, share of compressor / condenser fan / power cord / relay faults, typical 누전 shock scenario. Use §2 refrigeration proxy and label as assumption. Hot-drink machines (heater) vs cold machines (compressor) difference = [INFERENCE].
- A Korean public phone-charging kiosk/locker fire at a station or terminal (none found; only the school phone-locker case and the charger-terminal tracking paper).
- A numeric re-ignition window for a small power bank (only KBS expert "재발화" and one 6-hour anecdote); a numeric 119 arrival time for station electrical rooms (only 이대역: report ~20:06, 초진 20:25).
- A KORAIL station-level electrical-fire SOP that names 전기 담당, 전기안전관리자 or 한전 (KORAIL page names 역장, 관제센터, 전기복구반, 소방서 only). The cooling-tub/heat-glove kit is documented for 서울교통공사 stations, not KORAIL stations.
- Public-building inspection statistics on missing 회로 표시 on panel doors, or on locked vs unlocked panels; KEC gives the rule but no compliance figure.
- 2024 panel-only fire count and current cause split for panel fires (only 2014–2018 data and one 100-site survey).
- Game numbers such as breaker trip time, temperature curves, chance of fault per hour, walking time to an extinguisher (rules give 20 m distance only).

## Data caveats

- KESCO counts (8,634 in 2024) differ from 소방청 electrical counts (5-year average 10,140/yr, ~27% of 37,526, per 화재조사관 column https://www.keaj.kr/news/articleView.html?idxno=6147): KESCO excludes vehicles, trains, ships, aircraft, lightning and no-damage fires. The 2025 KESCO report re-bases to 9,376 (24.5%) and restates 2024 as 24.7%; do not mix the two bases.
- 2024 "기타 = 0" in the open-data CSV (2023: 563) suggests a reclassification into other buckets — [INFERENCE].
- KEC text is the 2023-12-14 (제8차 개정) copy; NFTC 101 is the 2024-07-25 version; 전기안전관리법 시행규칙 fetched via law.go.kr API shows 시행 2026-09-18. No later amendment check was done for KEC.

## URL status

Opened and used (content read): all URLs cited above, except those below. Notes on how some were obtained: Naver blogs only through the `m.blog.naver.com` mirror; NFTC 101 tables through law.go.kr `admRulInfoR.do` + `flDownload.do` images (viewed); law.go.kr XML through the public `DRF/lawService.do` endpoint; KAIST, MOEL 울산, MOEL 부산 PDFs downloaded and parsed with `pdftotext`; KEC via `pdftotext` of the eom.co.kr copy.

Failed, blocked or unusable:
- https://www.gimi9.com/dataset/www-data-go-kr-data-filedata-15069690 — access restricted (Korean-locale only); replaced by the data.go.kr page + CSV.
- https://www.kesco.or.kr/bbs/selectPageListBbs.do?bbs_code=MCB00522 (KESCO 전기재해통계 board) — page opens but list/PDF attachments are dynamic; the full 통계분석 PDF was not obtained (numbers come from 전기신문 / the open-data CSV / blog transcriptions).
- https://blog.naver.com/ymirsoft/224401505414, https://blog.naver.com/koshablog/* desktop URLs — redirect stub only (used `m.` URLs); https://blog.naver.com/ymirsoft/223600241980 (2023 report blog) not read.
- https://www.isafe.go.kr/DATA/bbs/86/20250825032929346.pdf — TLS certificate error.
- https://www.brcn.go.kr/safety/sub02_03_02_09.do — "Request Blocked".
- https://www.nfa.go.kr/nfa/news/pressrelease/press/?mode=view&cntId=2289 and https://www.nfa.go.kr/nfa/safetyinfo/lifesafety/stats/0010 — pages open but body is an image/attachment; NFA numbers taken from news that quotes them.
- https://www.chosun.com/national/national_general/2026/09/27/OIY7SWRZTVBCRG4REVGKFYAE4M/ — paywalled (search snippet only, not used as a fact).
- https://www.kdhc.co.kr/kdhc/cmmn/file/fileDown.do?atchFileId=b89f0018414f440099f34d0277cd010a&bbsId&fileSn=3&menuNo=200484 (KOSHA LOTO 지침 2022) — binary download, not parsed.
- https://cyber.kepco.co.kr/... — TLS handshake failure (used home.kepco.co.kr instead).
- https://www.law.go.kr/LSW/admRulInfoP.do?admRulSeq=2100000244810&chrClsCd=010201 — JS shell without body (used the `admRulInfoR.do` route).
- Official kosha.or.kr copy of KOSHA GUIDE E-91-2016 not located; text read from a tistory mirror (S).
- https://www.ytn.co.kr/_ln/0103_202409060321592896 — opened; irrelevant (factory).
