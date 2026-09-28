# 설비 조작 자료조사 (2026-09-28)

등급: A 공식·1차, S 2차(언론·전문 블로그·Q&A). 게임에 넣는 값과 근거를 나눈다.

## 열차(KTX-산천)

- [K1] 위키트리 「KTX 자주 타도 몰랐다…문 위에 달린 ‘초록색 버튼’ 정체」(2026-09-25, S, 코레일 KTX매거진 인용): 객실 출입문 위 초록색 ‘1분 열림’ 버튼은 **객실과 객실·통로 사이 내부 자동문**을 1분 열어 둔다. “열차에서 승하차할 때 사용하는 **외부 출입문을 임의로 열어두는 장치가 아니다**.” <https://www.wikitree.co.kr/articles/1158291>
- [K2] 외부 출입문은 열차팀장이 취급(닫힘 확인·닫음). 영상 「[철도풍경] KTX가 출입문을 닫는 법 (서울역 KTX 열차 열차팀장 출입문 취급)」 <https://www.youtube.com/watch?v=5VB6ZTDBQJQ> (S), 네이버 지식iN “모든 여객열차는 열차팀장이 문을 닫는다, KTX-산천·청룡도 같다” <https://m.kin.naver.com/qna/dirs/81204/docs/481197998> (S, 약함).
- [K3] 도시철도 차량은 비상시 승객용 **출입문 수동 개방 장치**가 있다(인천교통공사 안전장비 사용요령 「출입문수동개방법」, A). KTX 외부 문의 같은 장치 위치·절차는 이번에 1차 자료로 확인하지 못했다. <https://www.ictr.or.kr/main/safety/subway/equipment_open.jsp>
- 이전 조사(2026-09-26 research.md R3): 4호차 출입문은 열리지 않음, 트윈은 1~3·5~8호차에 승강장 쪽 문짝.

## 역사 출입문

- [D1] 슬라이딩 자동문 컨트롤러 운전 모드: 자동(센서, 통과 뒤 1~5 s 닫힘) · 상시 개방 · 잠금 · 비상 개방(화재 수신기 연동 시 전원과 무관하게 전개, 전원 차단 시 브레이크 풀려 손으로 밈). 모드는 컨트롤러 버튼이나 **키스위치**로 바꾼다. (S: 업계 설명 2건) <https://ko.chinaautomaticdoor.org/info/how-to-install-and-use-the-automatic-door-cont-78922828.html> · <https://plus-s.tistory.com/10993540>

## 개집표기

- [G1] 서울문화투데이 「교통약자 이동통로 자동화…서울교통공사 플랩형 개집표기 교체」(2026-06-01, S): 기존 철제형 비상게이트는 **단방향 여닫이 수동문**(역 직원 도움), 플랩형 개집표기는 **화재수신반과 연동해 자동 개방**이 가능. <https://www.sctoday.co.kr/news/articleView.html?idxno=47896>

## 에스컬레이터

- [E1] 한국승강기안전공단 블로그 「에스컬레이터 사고 예방! 비상정지 버튼 사용법」(S): 사고 시 승강구의 비상정지 버튼을 누르고 관리자에게 알린다. <https://m.blog.naver.com/koelsa1671/224372189243>
- [E2] 철도산업정보센터 「에스컬레이터 응급처지 요령」(A, PDF): 컨트롤 박스 키스위치(MAN ↔ AUTO UP/DOWN)로 재가동, 열쇠로 조작. <https://www.kric.go.kr/servlet/ContentDownloadServlet?type=pdf&object_id=0900271a80115056>
- [E3] 서울교통공사 비상정지 스위치 사용 안내 강화(투데이신문, S): 넘어짐 사고 2차 피해 예방. <https://www.tdaily.co.kr/news/view.php?idx=59584>

## 엘리베이터

- [L1] 행정안전부 「화재 시 승강기 사용하지 마세요!」(2024-03-18, A): 전국 승강기에 ‘화재 시 사용 금지’ 안내표지. 승강로로 연기 유입, 정전 시 갇힘. <https://www.mois.go.kr/frt/bbs/type010/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000008&nttId=107805>
- [L2] 국내에는 일반 승강기의 화재 시 운전 규정이 따로 없고(소방구조용·피난용 제외), 유럽 EN81-73 은 화재 신호 시 지정층 복귀. 화재수신기 연동 건물은 기준층 강제 복귀. (S) <https://elevatorlaboratory.tistory.com/308>

## 소방 설비

- 발신기: 누르면 수신기에 화재 신호, 지구경종 동작(NFTC 203, 이전 차수 research 의 비상벨 근거와 같음).
- 옥내소화전함: 관계인·발견자가 호스·관창으로 초기 소화(S). <https://rlqns2966.tistory.com/468>
- AED: 보건복지부 「자동심장충격기 설치 및 관리 지침」 7판(2024) · 8판(2026-09 배포) 확인, 보관함 경보 등 세부는 미독해. <https://health.muan.go.kr/health/medical_info/notice?idx=15205382&mode=view>

## 추가 조사 (JEV 011 전후)

### 엘리베이터 착층

- [L3] 네이버 실내 지도 POI(층별 picker, 2026-09-25 수집, 내부 참고용): `asset-library/research-public/2026-09-26/Services23F/naver-indoor-harvest/picker_all.json`, 1층 좌표 변환 `.planning/2026-09-24-interior-twin/specs/full/public1f/naver-1f-uv.json`(16개 점포 정합, 중앙값 0.17 u). 같은 UV 에 층마다 엘리베이터 POI 가 있다: 1층 코어 3곳(차 5대) 1·2층, 날개·모서리 승강기 1·2·3층, 승강장 쪽 6대 1·2층, 동측 외부 1대 2·3층. 2·3층 래스터는 트윈과 1-8 m 어긋난다(services-23f-report).
- 2층 안내도(`.planning/2026-09-23-video-twin/sinmajibang/board2f-plan.png`): #12 옆, #1 옆, 오른쪽 타는 곳 화살표 옆, 남쪽 가장자리 두 쌍.
- 트윈 형상 대조(2026-09-28 측정): 1층 코어 바로 위 2층은 트인 본관 바닥·중정 난간 옆·외벽, 3층 모서리 승강기 아래 2층은 평면도 화장실 상자, 날개 승강기 아래 1층은 선로, 모서리 아래 1층은 바닥 없음. 근거는 있으나 위치가 트윈과 맞지 않아 착층을 더하지 않았다(JEV 011).

### 에스컬레이터 발치 게이트

- [G2] 1F 영상 프레임 `.planning/2026-09-23-video-twin/1f-2026-mvs/native-images/000121.jpg`: 스테인리스 플랩 게이트, 오름 쪽 초록 화살표·내림 쪽 빨간 진입 금지 표시, 카드 단말 없음 → 요금 게이트가 아니다([G1] 은 해당 없음).
- [G3] 「승강기안전부품 안전기준 및 승강기 안전기준」 별표24 5.12.3.3.2: 운행 방향은 미리 설정되어 이용자에게 명확히 보여야 한다(부산엘리베이터 자료실 2024-02-05 인용, S). <https://xn--oy2b27cft42huuiysco40a.kr/bbs/board.php?bo_table=file&wr_id=1122>
- [G4] SD산업 「에스컬레이터스윙게이트」(2026-07-13, S, 제조사): 평소 열려 있다가 이상·비상정지 때 닫아 진입로를 막는다(2차 사고 예방). <https://blog.naver.com/supercafe/224345293752>

### AED

- [A1] 2025 한국 심폐소생술 가이드라인(질병관리청): 반응이 없으면 주변 사람을 지목해 119 신고, 다른 사람을 지목해 AED 를 가져오게 한다. 헬스조선 2026-01-13(S) <https://health.chosun.com/site/data/html_dir/2026/01/13/2026011303667.html>, 정책브리핑 2026-02-03 <https://www.korea.kr/news/policyNewsView.do?newsId=148958887>.
- 보관함 개방 경보: 「자동심장충격기 설치 및 관리 지침」 7판 본문을 받지 못해 확인하지 못했다 → 넣지 않음.
