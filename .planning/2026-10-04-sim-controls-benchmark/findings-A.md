# Lane A — 손 조작 정비 시뮬 벤치마크 (My Summer Car · My Winter Car · Car Mechanic Simulator 2021 · Pacific Drive)

조사 기준일 2026-10-04. 읽기 전용 조사(코드·씬·설정 무수정). 영상·프레임은 `media/A/`(git 제외)에만 있고 문서에는 URL+시점으로만 인용한다.
증거 태그: **[공식]** 개발사 사이트·Steam 상점/뉴스/패치노트·개발자 발언 / **[2차]** 위키·가이드·리뷰·커뮤니티(키 바인딩은 교차 표기) / **[관찰 URL t=mm:ss]** 직접 본 프레임 / **[추론]** 내 해석.
소리는 영상에서 들을 수 없었다(프레임만 분석). 소리 피드백은 위키 음원 설명·공식 패치노트에 근거하며 각 항목에 그렇게 표시한다.

## 0. 먼저 읽는 요약 (Main용)

CHOOGuard 현행 진단(findings.md §0)과 Lane A 4개 게임의 결정적 차이는 한 문장이다.
**이 게임들은 "무엇을 해야 하는가"를 말해 주지 않고 "이 도구/부품이 이 대상에 맞는가"만 검증해 준다.** 그리고 맞게 했는지의 결과는 즉시 UI가 아니라 소리·연기색·누유·검사표로 늦게, 간접적으로 돌아온다.
CHOOGuard는 반대다. 프롬프트가 정답 행동명(`가스 중간밸브 잠그기`)을 먼저 말하고, 무전 답신이 다음 할 일을 명령하고, 결과는 즉시 체크박스(●)로 확인된다.

Lane A에서 가져올 수 있는 구체 패턴(상세·근거는 각 게임 'CHOOGuard 적용' 표와 §5):
1. **이름이 아니라 '맞음(fit)'으로 게이팅** — MSC: 마우스를 올린 볼트가 *손에 든 렌치 규격과 맞을 때만* 초록, 커서 아래 글자는 부품 이름뿐. CMS: 윤곽 초록=분해 가능/노랑=다른 부품에 막힘/갈색=고착. 정답 행동을 문장으로 알려 주지 않는다.
2. **연속 입력 → 물리 상태(틱)** — MSC 볼트는 휠 8틱, CMS는 홀드 시간, PD는 홀드 스캔/홀드 제거. 한계에서 멈춘다. CHOOGuard의 E 탭 계약(`IFpsInteraction.CanInteract/TryInteract`)으로는 표현 불가능한 영역.
3. **조용한 불완전 상태 + 지연·진단 가능한 결과** — MSC: 덜 조인 볼트는 UI가 없고, 부품 이탈·누유·화재·검사 불합격으로 나중에 드러나며, 연기색/소리/누유 표시/계기판으로 추리한다.
4. **진단과 조치의 분리 + 제한된 추리 UI** — PD Tinker Station: 4열 문장 빌더(`What seems to be the problem?`, 트리거 부품→상태→작동 부품→거동) [관찰 V-C1 t=1:12], 오답 배너 `*** INCORRECT DIAGNOSIS ***` [관찰 V-C1 t=1:35], 위키 서술상 오답마다 `BAD GUESS - x/4 CORRECT`·판정 횟수 제한 [2차 P6]. CHOOGuard 무전 휠을 '인지한 사실로 보고 문장 조립' 형태로 바꿀 수 있는 가장 가까운 선례.
5. **안내 수준 = 정보량 프리셋** — CMS Easy(작업 목록 전부 공개) vs Normal/Expert(`part not discovered`), PD 7개 프리셋·약 50개 옵션·HUD 3요소 개별 끄기·'Free Quirk Hints'. Tab ○/● 체크리스트는 'Easy'에 해당하는 한 단계일 뿐이다.
6. **평가 = 문서** — MSC 검사 영수증(항목별 X, `HYLÄTTY`/`HYVÄKSYTTY` 도장, 불합격=스트레스↑+재검비), CMS `EXAMINED PARTS REPORT`(%), PD 점수 없음(로그북·자원). 숫자 점수 없이도 평가가 성립한다.
7. **주의(반례)**: CMS Test Path(`press the gas pedal or the brake pedal according to the pop-ups`)는 사용자가 비판한 '지시대로 진행'과 동형이다. MSC의 '구글 필수' 무지시 학습은 CHOOGuard 요건(역무원이 알 법한 것)과 맞지 않으므로 diegetic 문서·해금형 수첩(PD User Manual)으로 대체해야 한다.

미검증(§6 공백 참고): PD 차 내부 손 조작(시동 키/핸드브레이크) 입력, CMS Expert 난이도의 정확한 차이, MSC의 실제 스크롤 감촉·소리.

---

## 1. My Summer Car (Amistech, EA 2016-10-24 → 1.0 2025-01-08)

### 1.1 출처 표

| ID | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| M1 | https://www.amistech.com/msc/ | [공식] 개발사 사이트 | 뉴스 31.12.2024 | `Small error and you die. It could be that you forgot to tighten brake linings, or that you forgot to bolt wheels properly.` 설계 의도 명문화, PERMADEATH, 기능 목록(Car inspection process, Extensive damage system, Brake fluid/oil/coolant/fuel/carburetor management, First person controls, no immersion breakers) |
| M2 | https://www.amistech.com/msc/changelog.html | [공식] 전체 변경 이력(BUILD 139 2014 ~ v.241231-01) | 2014-2024 | 볼트 규격 게이팅(BUILD 159), 볼트 느슨→부품 이탈(BUILD 162), 렌치/래칫/스크루드라이버 도입 시점, 홀드 2초 입력, 누유 시각 효과, 검사 항목, 영구사망 삭제 시점, 손 점유 규칙 등 |
| M3 | https://store.steampowered.com/app/516750/My_Summer_Car/ | [공식] Steam 상점 | 열람 2026-10-04 | 1.0 출시 2025-01-08, `permadeath life survival simulator`, `You start the game with hundreds of loose parts`, `Severe car fever is required … meticulous approach`, 번들 My Bundle Car(MSC+MWC) |
| M4 | https://my-summer-car.fandom.com/wiki/Settings_menu | [2차] 위키(게임 내 설정 화면 기능 목록) | 열람 2026-10-04 | 마우스 기능명 Pick/Drop/Assemble/Use/Rotate objects/Fasten bolts/Turn knobs/Throw/Disassemble, Hand mode/Car tool mode, Auto clutch, Steering help 등 |
| M5 | https://en.namu.wiki/w/My%20Summer%20Car/%EC%A1%B0%EC%9E%91%EB%B2%95 | [2차] 나무위키(MSC·MWC 공용 조작) | 최종수정 2026-07-14 | 기본 키: 1/2 모드, 좌/우클릭, 휠, WASD, 차량 키. 기계번역투 문장 있음 → 교차 확인 |
| M6 | https://defkey.com/my-summer-car-shortcuts | [2차] 단축키 목록 | 2026-06 | 키 교차 확인(Mouse wheel = Turn object / screw-unscrew bolt / rotate dial) |
| M7 | https://my-summer-car.fandom.com/wiki/MY_SUMMER_CAR_GUIDE_2:_ELECTRIC_BOOGALOO | [2차] 위키 공식 가이드 | 열람 2026-10-04 | 조립·튜닝·배선·저장·욕구·시동 방법, 체크마크/분해 아이콘 설명, 진단표 |
| M8 | https://my-summer-car.fandom.com/wiki/Bolt_sizes | [2차] | 열람 | 볼트 수량·규격표, `A bolt/nut will turn green when it is hovered over with the correct sized spanner/socket` |
| M9 | https://my-summer-car.fandom.com/wiki/Spanner_set , …/Ratchet_set | [2차] | 열람 | 도구 상자 F 열기 → 2 → LMB 선택, 휠↔렌치 재장착 대응, 래칫 F로 방향 전환 |
| M10 | https://my-summer-car.fandom.com/wiki/Tuning , …/Carburator | [2차] | 열람(캬브 표 2025-08-22 측정) | 캬브/배전기/밸브/발전기 벨트 튜닝 틱 수·소리·배기색 |
| M11 | https://my-summer-car.fandom.com/wiki/Satsuma/Troubleshooting | [2차] | 열람 | 증상→원인 표(액체 샘=느슨한 볼트 등) |
| M12 | https://my-summer-car.fandom.com/wiki/Player_attributes , …/Death | [2차] | 열람 | 욕구 6종·증가율·사망 목록·영구사망·저장 삭제 규칙 |
| M13 | https://my-summer-car.fandom.com/wiki/Lindell_inspection_shop , …/Inspection_receipt | [2차] | 열람 | 검사 절차·통과 조건·영수증 항목 A–D·합격/불합격 도장 |
| M14 | …/Service_brochure , …/Wheel , …/Fridge_note | [2차] | 열람 | 정비소 서비스, `Not fully tightening all of the bolts will cause the wheel to promptly fly off while driving.`, 냉장고 메모 |
| M15 | https://steamcommunity.com/app/516750/discussions/2/591764534063006751/ | [2차] 커뮤니티 | 2025-02-28 | `open toolbox by pressing F on it, press 2 and click on a wrench, once you have the correct size (bolt will turn green when you hover over it) you can scroll up or down` |
| M16 | https://www.rockpapershotgun.com/my-summer-car-review-a-sordid-sim-of-piss-and-pistons-that-wont-hold-your-disgusting-little-hand | [2차] 리뷰 | 2025-01-22 | `Like the rest of the game there's no tutorial`, `a cluttered controls screen`, LMB 누르기/RMB 당기기/휠 노브, 위키·유튜브 의존, 모드로 볼트 규격 표시·다중 아이템 휴대 |
| M17 | https://www.nexusmods.com/mysummercar/mods/11298 | [2차] 모드 설명(MagicHandScrews) | 2026-05-29 | 모드가 `Tightness Stage (e.g., [ 7mm WRENCH | TIGHTNESS: 4/8 ])` HUD를 *추가* → 바닐라엔 없음 [추론] |
| M18 | https://www.reddit.com/r/MySummerCar/comments/tpuy43/ | [2차] 커뮤니티(검색 스니펫만; 본문 로딩 실패) | 2022 | `1 screw took 8 tics to fully tighten` |
| V-A1 | https://www.youtube.com/watch?v=ZVpLCoOwHBs | [관찰] Ogygia Vlogs, 엔진 블록 조립+볼트 규격 튜토리얼(2022 업데이트판), 2568 s | 2022 | 볼트·렌치 작업 프레임(§1.7) |
| V-A2 | https://www.youtube.com/watch?v=GUwhocP0cRA | [관찰] T8dyi, 배전기 튜닝 49 s | 2024-12-10 | 손 모드 vs 도구 모드 커서, 초록 나사 |
| V-A3 | https://www.youtube.com/watch?v=OeZRhOy2RHE | [관찰] Ginauz, Satsuma 튜닝 169 s | 2024-05-06 | 밸브 조정 나사, `MORTAL` 표시 |
| (미관찰) | https://www.youtube.com/embed/r0IZ_TEzg7M | 공식 사이트 'Currently featured video'(RoyalJohnLove, 2015, 3065 s) | 2015 | 구버전이라 분석 제외 |

### 1.2 조작표 (동사 → 정확한 입력)

기본 바인딩은 전부 [2차](M4/M5/M6/M7/M15)이며 교차 일치한 것만 확정으로 쓴다. 설정 화면에서 전부 리바인드 가능 [공식 M2 `you might need to RE-BIND YOUR CONTROLS`; M4]. 메뉴(F1/Esc)는 게임을 일시정지하지 않는다 [2차 M5].

| 동사 | 입력 | 비고·증거 |
|---|---|---|
| 걷기/달리기/웅크리기 | WASD / Shift(달리기, 허기·갈증 증가) / C(서기→앉기→엎드림 순환) / Space(점프) | [2차 M5 M6]. 최저 웅크림에서 점프=운동 [공식 M2 29.05.2020] |
| 손 모드 ↔ 도구 모드 | **1 = 손(일반) 모드, 2 = 도구 모드** | [2차 M5 M7 M9 M15]. 도구 모드에서는 물건을 집을 수 없다 [2차 M5]. 전용 UI 버튼 없음. 도구 모드일 때 HUD 우상단에 렌치/드라이버 글리프 [관찰 V-A1 t=2:05, 2:25; V-A3 t=1:38] |
| 집기/조립 | **LMB** | 집은 물건을 설치 지점에 대면 체크마크 ✔, LMB로 설치 [2차 M7] [관찰 V-A1 t=1:40] |
| 던지기/분해/되돌리기 | **RMB** | 풀린 부품 위에 분해 아이콘이 뜨면 RMB로 제거 [2차 M7]. 차 내부 레버/버튼은 LMB로 밀고 RMB로 당긴다 [2차 M16]. 핸드브레이크는 레버를 보고 **RMB 홀드** [2차 M11] |
| 사용(F) | **F** | 도구 상자·음식·병뚜껑·문서·전선 커넥터 등 `Most actions, like eating food, are activated by using [F], since LMB will usually just pick the item up. However, you need to press LMB to open doors and push buttons inside the car.` [2차 M7] |
| 홀드 F/버튼(2초) | 수면, 차량 안 수면, 앞유리 깨기, 쇼핑백 열기 | `now require holding down a button for 2 seconds` [공식 M2 2019-05-09 항목] |
| 볼트/노브/회전 | **마우스 휠** | 휠 위=조임(시계), 휠 아래=풂. 같은 휠이 '들고 있는 물건 회전'과 사우나·라디오 다이얼도 담당 [2차 M4 M5 M6 M7] |
| 도구 선택 | 도구 상자를 보고 F → 2 → 도구 LMB | 스패너 세트에는 렌치 여러 규격·스크루드라이버·점화플러그 렌치·자(타이어 마모)가 들어 있고, 상자에 되돌려 놓을 수 없고 교체만 된다 [2차 M9] |
| 래칫 방향 | 래칫을 든 채 F | `Pressing F while holding the ratchet changes the direction of torque.` 휠을 손가락 떼지 않고 빠르게 왕복하면 래칫처럼 빨리 돌아간다 [2차 M9] |
| 말/손 흔들기 | K 짧게=말(술 취하면 횡설수설), K 길게=손 흔들기(걷는 NPC가 반응) | [2차 M5] [공식 M2 29.05.2020 `Player can now wave its hand by holding "K" and roaming NPC's will react`] |
| 욕/중지 | N(스트레스 소량↓) / M | [2차 M5] |
| 밀기/때리기 | J / H | 차 밀기·NPC 넘어뜨리기 / 창문 파괴·싸움 [2차 M5] |
| 소변·흡연·시계 | P(길게 눌러 세게) / I(F로 담배 장착 후; 두 번=버림, 흡연 중엔 렌치 사용 불가) / U | [2차 M5] |
| 차 탑승 | **Enter** = 운전 모드 ↔ 보행 | 운전 모드 아니어도 차 안 스위치는 조작되지만 가속·브레이크·기어는 불가 [2차 M5]. 시동 키는 **키를 누르고 있는 동안 크랭크**, 손쓰로틀·초크는 레버 클릭 [2차 M7] |
| 운전(키보드) | W 액셀 / S 브레이크 / A·D 조향 / Z 핸드브레이크 / X 클러치 / G 기어↑ / B 기어↓ / R 레인지(트랙터·Gifu) / Q·E 몸 기울이기 | [2차 M5 M6 M4]. 자동 클러치·조향 보조·H-시프터·브레이크/스로틀 지연·클러치 정밀도는 설정 [2차 M4]; BUILD 159(2015-03-02)부터 `Steering assistance for digital inputs` [공식 M2] |
| 시스템 | F1/Esc 메뉴, F2 HUD, F3 거울, F9 TV 카메라 | [2차 M5]. F2 HUD 끄기로 욕구 패널과 자막이 사라짐 [2차 M12] |
| 저장 | 변기·옥외 화장실에서 `save and quit`; 저장하면 시간이 다음 짝수 시각으로 진행 | [2차 M7]. `Saving the game during ongoing housefire results whole house burning down` [공식 M2 21.11.2021] |

### 1.3 조작 문법 (primitives)

**프리미티브**: 집기(LMB) / 분해(RMB) / 사용(F) / 휠 회전 / 모드 전환(1·2) / 도구 선택(상자→2→클릭) / 홀드(키 유지) / 몸 기울이기(Q·E로 벽 너머·창밖) / 흔들기(마우스를 격하게 흔들어 기절에서 깨어남) [2차 M12].

**연속 입력 ↔ 물리 상태**
- 볼트: 휠 1틱 = 1단계, **한 볼트가 완전히 조여지기까지 8틱** [2차 M18 스니펫+M17 모드 HUD `TIGHTNESS: 4/8`; 두 출처가 일치하나 위키 직접 서술은 없음 → 확정도 중]. 오픈 스패너는 틱마다 재장착 개념, 래칫은 왕복(M9). 볼트가 돌아가는 모습이 보이다가 한계에서 멈춘다.
- 조절 부품은 같은 휠에 *다른 의미*를 입힌다: 스티어링 로드(14mm 스패너, 휠이 멈출 때까지 내린 뒤 60틱 올림), 밸브 8개(드라이버, 끝까지 내린 뒤 7틱 올림, 소리가 안 나면 정상), 배전기(풀고 손 모드로 휠 회전, 27틱), 캬브(드라이버, 1틱≈0.2 AFR, 44틱 내림 후 22틱 올림 ≈ 14.4), 캠샤프트 기어(볼트를 조인 뒤 계속 올리면 기어가 회전, 노치 정렬), 발전기(알터네이터) 벨트(풀고 손 모드 휠로 이동, 끝까지 내린 뒤 2틱 올림) [2차 M7 M10; 캬브 표 M10]. 같은 문법의 반복이 학습 비용을 낮춘다 [추론].
- 틱 한계에서 소리가 멈춘다: MWC 패치 `Fixed bug with bolting sound playing when Bolt is already fully tight or loose` (v.260917-01) [공식 W2] → 볼트 틱 소리가 기본 피드백이고 한계에서는 무음이 정상 [추론].
- 밸브 나사 회전은 조정값과 일치시켰다: `Valve screw rotations also now match the adjustment` [공식 M2 12.08.2021] [관찰 V-A3 t=1:38: 나사 높이가 제각각].

**제약(조작 가능 조건)**
- 규격 게이팅: `All bolts are now accessible only with correct size spanner` [공식 M2 BUILD 159, 2015-03-02]. 틀린 렌치는 볼트가 선택되지 않는다(초록 없음) [2차 M8 M15] [관찰 V-A1 t=2:05 vs t=2:25].
- 단일 아이템 휴대: 손에 든 한 개만 물리적으로 옮긴다(여러 개를 들게 하는 모드가 인기) [2차 M16]. 두 손이 차 있으면 전화 못 받음 `Phone cannot be answered with helmet on or if both hands are in use` [공식 M2 24.04.2021].
- 도구 모드에서는 집을 수 없음, 흡연 중에는 렌치 사용 불가 [2차 M5].
- 부품 의존: 반축은 디스크 브레이크 설치 전엔 못 붙이고, 공유 볼트(예: 반축 14mm는 디스크와 공유)는 다른 쪽 설치 후에 조여야 한다 [2차 M7 M8]. 가시성 규칙도 있다: `Masked out bolts from engine that should not be accessed through other parts` [공식 M2 2019] — 다른 부품에 가려진 볼트는 선택 불가.
- 전기 안전 순서: 배터리 (+)를 먼저, (−)는 나중. (−)만 조인 채 두면 화재/감전 [2차 M7 M11] [공식 M2 30.07.2021 `Fixed bug with car electric fire`, 21.11.2021 `Raging car fire can now kill Player`].

**상태 피드백 채널 (UI 없음 → 세계에 의존)**
- 커서 하단 중앙에 **부품 이름만** 노란 글씨(예: `CRANKSHAFT`, `MAIN BEARING1`, `DISTRIBUTOR`) [관찰 V-A1 t=0:56, 1:10; V-A2 t=0:27]. 행동·상태·정답은 말해 주지 않는다.
- 설치 가능: 흰 체크마크 ✔ [관찰 V-A1 t=1:40], 분해 가능: 분해 아이콘 [2차 M7].
- 규격이 맞는 볼트: 초록 점/육각 하이라이트 [관찰 V-A1 t=2:25, V-A2 t=0:40].
- 손 모드에서 휠로 돌릴 수 있는 대상엔 손바닥 커서 [관찰 V-A2 t=0:27].
- 볼트별 조임 단계는 **표시되지 않는다**(모드가 추가) [2차 M17, 추론].
- 시스템 증상: 소리(배전기 '고음 처핑', 밸브 '딸깍', 벨트 '끽', 캠 '쿵', 린 '총소리'), 배기 연기색(검정=농후, 흰색=희박, 파랑=피스톤 마모, 흰 연기=헤드가스켓), 계기판 경고등(직사각 빨강=오일압, 원형 빨강=발전기), 배터리 충전기 바늘, 딥스틱 오일 색(`Press F on the dipstick`), `Added visual leaks to help to diagnose car problems` [공식 M2 20.08.2019] [2차 M7 M10 M11].

### 1.4 안내·교육 모델

- **튜토리얼 없음**: `Like the rest of the game there's no tutorial. You are at the mercy of a cluttered controls screen` [2차 M16]. 리뷰어는 위키와 유튜브에 크게 의존했다고 서술.
- **게임 내 지식원(diegetic)**: 냉장고 메모(`Fix your dad's old car … if you repair the car and make it pass the inspection, you can keep it`) [2차 M14], 부품 카탈로그(잡지→봉투→우체통→배송 전화) [2차 M16], Fleetari 정비 안내서(영/핀/스웨덴어, 7쪽) [2차 M14], 검사소 영수증, TV 텔레텍스트 [2차 M16]. 엔진 조립도·튜닝 인포그래픽은 **커뮤니티 제작물**(위키)이라 게임 안에는 없다 [2차 M7 M16].
- **보조는 기능 설정과 구매 아이템**: Steering help·Auto clutch(설정), 래칫 세트(`Ratchet Tool set for faster bolt fastening, available from parts magazine`) [공식 M2 31.07.2017], 연료혼합 게이지(AFR)를 사서 달면 튜닝이 쉬워진다 [2차 M7 M10], 스패너 세트의 `Added Ruler … to check tire wear` [공식 M2 30.04.2018], 딥스틱 [공식 M2 05.12.2017].
- **모드 커뮤니티가 보완**: 볼트 규격 표시·볼트 레이더·조임 단계 HUD·전체 조임 모드 [2차 M16 M17] → 플레이어가 *원하는* 정보가 바닐라엔 없다는 증거.
- 난이도 노브는 **영구사망 on/off(새 프로필 생성 시)** 와 위 설정뿐 [2차 M12 `Permanent death can be disabled by unchecking the permadeath option while creating a new profile`].

### 1.5 실패·결과 모델

| 범주 | 규칙 | 증거 |
|---|---|---|
| 조임 불량의 지연 결과 | 볼트가 헐거우면 부품이 *주행 중* 떨어짐, 엔진 블록은 *가동 중* 격렬하게 이탈, 휠은 `promptly fly off while driving` | [공식 M2 BUILD 162 2015-04-23 `Many of the parts … can now drop off if bolts are too loose`; BUILD 146/147 2014 `Motor can drop if not correctly bolted`, `violent motor drop when running`] [2차 M14] |
| 충격 | `Heavy impact might now loosen a random bolt` | [공식 M2 29.05.2020] |
| 유체 | 브레이크 라이닝 너트를 안 조이면 브레이크액이 샌다, 호스 클램프 `make sure they are tightened`, 라디에이터 냉각수 누수 계산 | [공식 M2 29.05.2020 `Added hose clamp screws … Coolant leak calculations are now more detailed`] [2차 M7 M11] — '액체가 안 찬다 → 어딘가 볼트 하나가 헐겁다'는 비국소 원인 |
| 화재 | 캬브를 덜 조이면 화재 가능, 배터리 (−) 선조임 화재, 불붙은 차에 있으면 사망, 집 화재 중 저장하면 집 전소 | [2차 M10 M11] [공식 M2 21.11.2021] |
| 튜닝 불량 | 린 믹스=총소리+배기 흰색+전력 하락, 캠 오정렬=엔진 손상, 밸브 불량=딸깍·'1개든 8개든 소리가 같다'(어느 밸브인지 소리로 구별 불가) | [2차 M7 M10 M11] |
| 정비소 | 부품은 분해해 사무실 안으로 가져가야 수리해 준다(조립 상태로 밖에 두면 안 됨), 엔진 조정/튜닝/브레이크 점검 유료 | [2차 M14] |
| 욕구 | 갈증·허기·스트레스·소변·피로·더러움. 증가율/45 s(실시간): 갈증 1.83%, 허기 1.42%, 스트레스 1.20%, 소변 0.24%, 피로 0.98%, 더러움 0.27%. 빨간 구간 이후 방치 시 사망(피로는 기절). 사망 임계는 문서 불일치: Guide 2는 `1.5x the bar`, Player attributes는 빨간 100% 후 한도 180%·갈증 사망까지 1시간 32분(증가율로 환산하면 220%대, 추론)로 기술 | [2차 M7 M12] — **수치는 불일치로 채택하지 않음** |
| 사망 | 충돌·열차·익사·감전(퓨즈 테이블, 소변으로 TV 등)·폭발(주유 중 흡연)·화재 등. 신문 1면 + 부검 보고서 | [2차 M12] |
| 영구사망 | 켜면 사망 순간 세이브 삭제(`Save is now deleted (Mortal mode) at the exact moment of death`). 끄면 묘지에서 재시작, 진행 유지. `MORTAL` 표시가 HUD에 뜬다 | [공식 M1 M2 29.05.2020] [관찰 V-A3 t=1:38 HUD `MORTAL`/`MONDAY`] |
| 안전 지연 | 동승자가 영구사망 대상이 되기 전 5초 지연 | [공식 M2 10.12.2023] |
| 법적/사회 | 경찰 검문(속도·번호판·음주), 과태료·징역, NPC 보복(Fleetari가 차를 오래 안 돌려주면 회수) | [공식 M2 2020-2024] |

### 1.6 평가 모델 (검사소=diegetic 채점)

- **Lindell 검사소**(월–금 08:00–16:00, 325 mk): 차를 리프트에 올리면 검사관이 두드려 보고(`magical tool will reveal any problem`), 영수증이 책상에 나타난다. 영어 전환은 영국 국기 클릭 [2차 M13].
- **영수증 구조**: 항목마다 X 체크박스, 왼쪽 부품 목록. A 브레이크(서비스 브레이크=라이닝 누유/마스터실린더 액 부족, 주차 브레이크=핸드브레이크 미조임), B 조명(전·후 램프, 경고 삼각대), C 섀시·스티어링(서스펜션 암·쇼크·타이로드·조향축·연료 라인·연료 탱크·배기·서브프레임·타이어·차체), D 구동계(앞유리, 운전석 시트, 앞바퀴 정렬(1틱 초과 오차), 속도계, 변속, 핸들, 엔진, 배기가스(AFR<14.05 검정/피스톤 마모 파랑)). 손글씨 메모는 항상 `This car is just a huge pile of waste`(합격해도 동일) [2차 M13]. → **원인(느슨한 볼트)을 지목하지 않고 '결과 항목'만 X로 표시**한다.
- **판정**: `HYLÄTTY`(불합격)=스트레스↑ + 재검 325 mk, `HYVÄKSYTTY`(합격)=스트레스↓ + 28일 유효, 번호판 지급. 타이어 통과 임계는 세이브마다 45–65% 난수. 28일마다 재검(7일 전 우편 전단) [2차 M13] [공식 M2 29.05.2020].
- **랠리 결과표**: 플레이어 페널티 전체 나열 [공식 M2 29.05.2020 `Rally results now show a listing of all Player penalties`].
- 점수·랭크 없음(랠리 외). 합격/불합격과 돈·스트레스·사회 결과가 곧 평가.

### 1.7 영상 관찰 (V-A1/A2/A3)

| 시점 | 관찰 | 의미 |
|---|---|---|
| V-A1 t=0:56 | 좌상단 `THIRST/HUNGER/STRESS/URINE/FATIGUE/DIRTINESS/MONEY 3000` + `MONDAY`; 하단 중앙 노란 `CRANKSHAFT` | 욕구 패널은 6개 막대+돈뿐, 커서 텍스트는 이름만 |
| V-A1 t=1:10 | `MAIN BEARING1` | 부품 번호까지 이름 |
| V-A1 t=1:40 | 크랭크샤프트 위에 흰 체크마크 ✔ | 설치 가능 확인 아이콘 |
| V-A1 t=2:05 | 렌치를 든 상태, 우상단 도구 모드 글리프, 십자선 점만 | 초록 없음 = 렌치 규격이 안 맞거나 볼트를 겨냥하지 않음 [추론]. 어느 쪽인지 텍스트로 알려 주지 않음 |
| **V-A1 t=2:25** | **렌치를 든 채 베어링 볼트에 초록 하이라이트** | **규격 일치 시에만 초록(M8의 서술과 일치) — 볼트/도구 작업 결정적 프레임** |
| V-A2 t=0:27 | 손 모드, 손바닥 커서, 배전기 | 손 모드 휠 회전 대상 |
| V-A2 t=0:40 | 도구 모드, 드라이버, 초록 나사 | 풀고→손으로 돌리고→다시 조이기 순서 |
| V-A3 t=1:38 | 밸브 조정 나사(나사산 길이 제각각), 드라이버, HUD `MORTAL` | 나사 노출 길이가 조정값을 시각화 |

### 1.8 CHOOGuard 적용 (판정 × 메커닉 × 동사 × 방법 × 증거)

공통 전제: 역무원 직무만, 모든 hazard 종류에서 일반 동작, 지각하지 않은 위험은 숨김, 부가 기능이 디렉터·세계 난수를 소비하지 않음(task_plan 제약).

| 판정 | 메커닉 | CHOOGuard 동사 | 구체 방법 | 증거 |
|---|---|---|---|---|
| **Adapt** | 이름만 표시 + '맞음'만 하이라이트 | 소화기·호스·밸브·분전반·셔터/에스컬레이터 조작, 점검 | 프롬프트를 `가스 중간밸브`(대상 이름)로 줄이고 '조작 방식'(돌림/당김/누름)은 월드 손잡이 형태로 읽게 한다. 손에 든 것·선택한 행동이 규격에 맞는 순간에만 윤곽 초록; 틀리면 무반응(오답 문구 금지). 대상 표시 조건은 현행 시야 기반 발견(IncidentDirector.cs:456-474)과 `CanInteract`를 그대로 사용 | [관찰 V-A1 t=2:05 vs 2:25; t=0:56] [공식 M2 BUILD 159] [2차 M8] |
| **Adopt** | 휠 틱으로 연속 상태, 한계에서 정지음 | 가스 중간밸브·분전반 레버·수동 셔터/문·에스컬레이터 복구 스위치 | E **홀드**+휠 N틱(또는 마우스 드래그)으로 개도(0–1)를 올리고, 틱마다 소리·손잡이 회전, 한계 도달 시 딸깍(정지음)과 무음. 4–5개 핵심 조작에만 적용(30개 전체 아님). 계약은 `IFpsInteraction`에 '진행도' 개념을 추가하는 형태가 필요(현행 탭 전용) | [2차 M5 M6 M9 M18 M17] [공식 W2 v.260917-01] |
| **Adapt** | 조용한 불완전 상태 + 지연된 결과 | 밸브 반만 잠금 → 잔류 누출 지속, 분전반 재투입 재발화(이미 있음), 잠금표 누락 | 각 조작 대상에 숨은 '완료도'(제안 명칭)를 두고 UI에 표시하지 않는다. 결과는 감각 채널(소리 약해짐/남음, 연기색, 눈에 보이는 누출)으로 점진 노출. 모든 Hazard 공통 '잔류량'+증상 채널로 구현하고, 증상 변화는 결정적 감쇠(난수 소비 금지). 인계 시 완료도가 점검표에 반영 | [공식 M2 BUILD 162/147/146, 29.05.2020 'loosen a random bolt'] [2차 M11 `loose/untightened bolt somewhere`] [추론] |
| **Adopt** | 진단 근거 사다리: 눈→소리→연기색→계기 | 위험 발견/점검, 사람 상태 | 새 정보 채널을 만들기보다 hazard가 *이미 가진* 값(농도, 온도, 압력, 접근 가능성)을 소리·연기색·표시등으로 렌더하고, 플레이어가 보고 들은 만큼만 '지각함' 상태로 올라가게 한다. 정답 이름은 주지 않는다 | [2차 M10 M11] [공식 M2 'visual leaks'] |
| **Adapt** | 검사 영수증(항목별 X, 도장, 판정의 사회·금전·스트레스 영향) | 현장 인계, 교대 종료 평가 | `Shift Log`(현행 `No score`)를 '인수 기관 확인표'로: 인계 시점에 *관찰 가능했던* 상태 항목만 ○/X, 원인 지목 금지, 합/불 도장 대신 기관의 한 줄 메모. 숫자 점수 없음. **주의**: 플레이어가 인지하지 못한 위험은 인계 *후* 요약에서만(진행 중 노출 금지) | [2차 M13] [추론] |
| **Adapt** | 손 점유·단일 아이템·두 손 사용 시 전화 불가 | 소화기 휴대, 무전, 환자 처치, 문 조작 | 소화기를 든 채로는 무전(Q 휠)·문 열쇠·CPR이 제한되고 G로 내려놓기. 이미 G 드롭이 있으므로 '손이 찬 상태' 규칙만 추가. 어떤 hazard에서도 동일 | [공식 M2 24.04.2021] [2차 M16] |
| **Adapt** | 의도적 2초 홀드(수면·유리 깨기·봉투 열기) | 되돌리기 어렵거나 위험한 조작: 분전반 개방, 문 강제, 셔터 비상 정지/복구 | 일반 상호작용(약 25개)은 탭 유지, **위험·비가역** 조작에만 홀드를 요구해 오조작 방지와 '한 번 더 생각'을 만든다 | [공식 M2 2019-05-09] |
| **Adapt** | 손짓·말 제스처의 NPC 반응(길게 K=손 흔들기, 말하면 운전자 진정) | 승객 안내, 진정, 대피 유도 | 대상 NPC를 보고 탭=말 걸기, 홀드=손짓 유도. 효과는 NPC 이동/표정으로 관찰되고 로그에 '말로 진정'만 남기던 현행(IncidentDirector.Casualty.cs:370-387)을 행동 결과로 바꾼다 | [공식 M2 29.05.2020 'wave', 'Using Speech gesture … reasonable speed'] |
| **Adapt** | 욕구 막대 + 빨간 구간 + 사망(느린 증가) | 플레이어 연기·열 노출 | 욕구 6종은 가져오지 않는다. 연기·열 노출 *하나*의 완만한 증가(비네트+기침+걸음 느림)를 두고, 한계 도달 시 '현장 이탈(동료가 부축)'로 처리. HUD 끄기(F2형)로 막대 없이 증상만 보게 하는 옵션 | [2차 M12] [공식 M2 'Non-lethal Player status bars won't turn red'] [추론] |
| **Adapt** | 구매하면 편해지는 계측기·래칫(설정 슬라이더가 아닌 '물건') | 점검·안내 보조 | 길 안내(현재 기본 켜짐)를 '역 안내도'라는 휴대 물건으로 이동: 지도를 챙기면 M 지도가 뜨고, 안 챙기면 역내 사인과 기억에 의존. 역무원이 실제 지니는 물품 범위만 사용(Main이 실제 비품 확인 필요) | [2차 M7 M10] [공식 M2 31.07.2017] [추론] |
| **Adapt** | diegetic 지식 문서(냉장고 메모·카탈로그·안내서) | 교육, 브리핑 | 브리핑 문장(`이상을 발견하면 알리고…`)을 *보관함 속 메모/게시물*로 옮겨 플레이어가 읽으러 가야 한다. 공개 안전 행동요령을 '내부 SOP'로 표기하지 않는 제약 준수 | [2차 M14] [공식 W3 MWC VIN·딜러 시트 공개] |
| **Reject** | 튜토리얼 부재·위키/유튜브 의존 | — | 역무원 직무 시뮬은 '구글 필수' 구조를 채택하지 않는다. 대신 PD식 해금형 수첩(§4) | [2차 M16] |
| **Reject** | 영구사망 기본·6종 욕구·저장 장소 제한+시간 점프 | — | 한 교대(shift) 단위 구조에 부적합. 영구사망은 선택형 '엄격 모드'로도 이번 제안에서는 제외(근거: 교대 단위 반복 플레이 구조, [추론]). 최종 선택은 Main/사용자 몫 | [공식 M1] [2차 M7 M12] |

---

## 2. My Winter Car (Amistech, Steam 얼리 액세스 2025-12-29 — 2026-10-04 현재도 EA)

### 2.1 출처 표

| ID | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| W1 | https://store.steampowered.com/app/4164420/My_Winter_Car/ | [공식] Steam 상점 | 열람 2026-10-04 | `Early Access` 2025-12-29, 12,500원, `BE WARNED: This game is for players who already master the challenge of My Summer Car`, `Player body temperature simulation`, `almost 200 unique parts`, `Permanent Death`, 최근 30일 평가 89% |
| W2 | https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=4164420 | [공식] Steam 뉴스 피드(패치노트 12건) | 2026-01-08 ~ 2026-09-17 | 최신 v.260917-01(17.09.2026): `Added Lug Wrench (Required to change tires!)`, `Tire condition can be now checked without removing/unbolting Wheel`, `Fixed bug with bolting sound playing when Bolt is already fully tight or loose`, `Added Intro cutscene`. 그 외 v.260504-01 `Added carbon monoxide poisoning and death`, v.260917-01 `Excessive consumption of Energy Drinks might now cause death` |
| W3 | https://www.amistech.com/mwc/ , …/vinplate.html , …/dealersheet.html | [공식] 사이트 | 2026 | 사이트 본문이 사실상 **VINPLATE 해독표·딜러 옵션표 두 장** (인게임 문서의 현실판) |
| W4 | https://my-winter-car.fandom.com/wiki/Player_attributes , …/Lug_wrench | [2차] 위키 | 열람 / 2026-09-17 | `Problem/Thirst` 바, 체온 바 색(파랑~빨강), 땀, 숨은 `alcoholism`, 도구 모드 `2`로 러그 렌치 선택 |
| W5 | 위 M5(나무위키 조작 문서) | [2차] | 2026-07-14 | `This document covers player controls for My Summer Car and My Winter Car` → 조작 체계 공용 |
| W6 | https://mywintercar.com/msc-vs-mwc | [2차·저신뢰] 팬 사이트 | 2026 | 임의 서술 다수(예: `50-60% of parts can break`) → 참고용으로만, 수치 미채택 |

### 2.2 상태 (2026-10 기준)
- Steam 표기 **Early Access**, 마지막 공식 패치 **v.260917-01 (2026-09-17)**. 뉴스 12건: 2026-01-08(출시 기사)~2026-05-16은 2주 안팎 간격, 이후 4개월 공백 뒤 2026-09-17 대형 패치 [공식 W2]. 완성본 아님 → 인용한 변경은 *현재 시점 스냅샷*.

### 2.3 MSC 대비 조작·상호작용·생존 변경점

| 영역 | 변경 | 증거 |
|---|---|---|
| 조작 체계 | 키 구성 공용(1/2 모드, 휠, F). 도구 모드에서 러그 렌치 선택 | [2차 W5 W4] |
| 도구 특화 | 러그 렌치 **필수**(타이어 교환), 타이어 상태는 휠을 안 풀고도 확인 | [공식 W2 v.260917-01] |
| 볼트 피드백 | 볼트 틱 소리를 한계 상태에선 내지 않도록 수정 | [공식 W2 v.260917-01] |
| 생존 | `Player body temperature simulation`, 옷·난방·땀, 추위 사망, 일산화탄소 중독, 에너지드링크 과다 사망 | [공식 W1 W2] [2차 W4] |
| 욕구 | `Thirst`가 `Problem`(알코올 의존)으로 대체될 수 있음, 의존도 0이면 이름이 Thirst로 돌아감, 체온 바 색상은 외기 온도만 반영 | [2차 W4] |
| 위험 신호 | 연료 불량 증상(캬브 마모 시 스로틀 고착), 부품 내구값 부분 도입 | [공식 W2 v.260504-01] |
| 검사 | 일반 + **Historical/Museum/Ice Racing** 검사가 별도 존재, 엄격도 조정 | [공식 W2 v.260126-01, v.260313-01] |
| 지식 문서 | VIN 플레이트(24개 필드) 해독으로 부품·트림을 결정; 개발자가 해독표·딜러표를 인게임 문서처럼 공개 | [공식 W3] |
| 시간·저장 | 23–24시 저장 시 1시간 진행 수정 등 | [공식 W2 v.260126-01] |
| 보조 | `Corris Grip Helper` 슬라이더 설정 | [공식 W2 v.260126-01] |

변경되지 않은 것(내 판단): 도구 모드·손 모드 이분법, 휠 틱, 영구사망 옵션 [2차 W5 W1].

**계약 항목별 요약 (MWC는 MSC 대비 델타로 기술)**

| 항목 | 내용 | 증거 |
|---|---|---|
| 조작표 | MSC와 동일(1/2 모드, 휠, F, LMB/RMB). 도구 선택 시 러그 렌치 추가. 새 입력 키는 확인된 것이 없음 | [2차 W5 W4] [공식 W2] |
| 조작 문법 | 휠 틱 볼트, 도구 모드 이분법 유지. `Fixed issue with active Bolts getting stuck`(첫 2주 패치)처럼 볼트 상태 기계가 EA에서 계속 조정 중 | [공식 W2 11.01.2026 항목] |
| 안내·교육 | 튜토리얼 없음 유지 + 경고문(`for players who already master … My Summer Car`), VIN 해독표·딜러표 공개, `Added Intro cutscene`(2026-09) | [공식 W1 W2 W3] |
| 실패·결과 | 영구사망 선택, 저체온·일산화탄소·에너지드링크 과다 사망, 알코올 의존. 얼음 위 차량 침몰 등 | [공식 W1 W2] [2차 W4 W6(저신뢰)] |
| 평가 | MSC형 검사 + Historical/Museum/Ice Racing 검사, 랠리 3스테이지 결과표, 얼음 레이스 결과표 저장 | [공식 W2 v.260210-01, v.260126-01, v.260917-01] |

### 2.4 CHOOGuard 적용

| 판정 | 메커닉 | CHOOGuard 동사 | 구체 방법 | 증거 |
|---|---|---|---|---|
| **Adapt** | 체온·땀·환경 연동 위험 | 연기·열 노출 | 노출 게이지를 '외기 조건(열원 근접·연기 농도)+보호 행동(자세 낮춤, 문 닫음)'의 합으로 두어, 한 행동이 다른 위험을 만들 수 있게 한다(옷 입은 채 실내→땀 같은 *상호작용 비용*). 모든 hazard에서 동일 변수 사용 | [2차 W4] [공식 W1] |
| **Adopt** | 개발자가 인게임 문서의 현실판(VIN 해독표·딜러표)을 공개 | 교육·브리핑 | CHOOGuard 브리핑·안내 문서도 *종이 문서 모양*의 읽기 자료로 만들고, 정답 문장이 아니라 '조회표'(어떤 표지/표시등이 무슨 뜻인지)를 준다 | [공식 W3] |
| **Adapt** | 도구 특화(러그 렌치 필수)와 '풀지 않고 점검' 개선 | 소화기 약제 구분, 점검 | '맞는 도구가 있어야 한다'와 '가볍게 확인 가능한 상태 정보'를 분리: 약제 라벨은 눈으로 읽기, 압력계는 판독, 조작은 맞는 도구 필요 | [공식 W2 v.260917-01] |
| **Adapt** | 상황 종류별 검사 기준 변형(Historical/Museum/Ice) | 교대 종료 평가 | 인계 확인표의 항목은 hazard가 *스스로 노출하는* 항목 목록으로 구성(종류를 코드에 고정하지 않음). 모르는 종류는 공통 항목만 | [공식 W2] [추론] |
| **Reject** | 알코올 의존·Problem 바 | — | 직무 시뮬과 무관 | [2차 W4] |
| **주의** | EA — 규칙이 계속 바뀜 | — | 인용 시 `v.260917-01` 시점 명시 | [공식 W2] |

---

## 3. Car Mechanic Simulator 2021 (Red Dot Games / PlayWay, 2021-08-11)

### 3.1 출처 표

| ID | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| C1 | https://store.steampowered.com/app/1190000/Car_Mechanic_Simulator_2021/ | [공식] Steam | 열람 2026-10-04 | 신규 `Revised part-examine mode`, `Rusted bolts`, `Minigames`, `Skills and garage upgrade system`, `Fuseboxes`, `An infinite number of randomly-generated orders` + `handcrafted story missions` |
| C2 | https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=1190000 | [공식] Steam 뉴스 | 2021–2026 | 최신 패치 1.0.40 `Pickup DLC`(2026-07), 차기작 `Car Mechanic Simulator 2026`(데모 2026-03-30, 코옵) 예고 |
| C3 | https://www.gamepressure.com/car-mechanic-simulator-2021/controls-keybinds/z8eb98 | [2차] | 2021-08-18 | 키 이미지 표(텍스트 기능만 추출), `Pie menu`, `Interact` |
| C4 | https://defkey.com/car-mechanic-simulator-2021-shortcuts , https://www.followchain.org/car-mechanic-simulator-2021-controls/ | [2차] | 2021 / 2024-12-27 | W/S/A/D, C, E/Q, T, I, Left Shift(Special option), Space(핸드브레이크/미니게임), M, X, F, O |
| C5 | https://www.gamepressure.com/car-mechanic-simulator-2021/malfunction-diagnostics/z1ebf4 , …/replacing-parts/z2ebf5 , …/commission-types/z0ebf3 , …/user-interface/z3eb93 , …/beginners-guide/z2eb92 , …/an-undetected-part-what-to-do/zfec70 | [2차] 가이드 | 2021-08 ~ 09 | 조작·진단·주문 구조·UI 아이콘 |
| C6 | https://car-mechanic-simulator-2021.fandom.com/wiki/Tools | [2차] 위키 | 열람 | 진단 도구 6종 가격(Tablet 500, OBD 1500, Fuel Pressure 300, Multimeter 150, Compression 300, Tire Tread 150 CR) + 접근 위치(파이 메뉴 `part unmount` 모드) |
| C7 | https://steamcommunity.com/app/1190000/discussions/0/5503948370876735290/ | [2차] | 2021-08-16 | 점검 모드가 CMS18 대비 '그룹 단위'로 바뀜 |
| V-B1 | https://www.youtube.com/watch?v=KoS0PecyKy0 | [관찰] DG, `Part not discovered` 튜토리얼 170 s | 2021-08-29 | 프롬프트 바·파이 메뉴·주문서·점검 모드 |
| V-B2 | https://www.youtube.com/watch?v=R2mt67PQEPg | [관찰] TacticalTurbo, 진단 워크스루(0:00–5:00 받음) | 2023-11-16 | `EXAMINED PARTS REPORT` |
| V-B3 | https://www.youtube.com/watch?v=wguF6yI2JUI | [관찰] redfirekeep, 점검 모드 58 s | 2023-11-30 | 서스펜션 그룹 점검 |

### 3.2 조작표 (PC)

| 동사 | 입력 | 비고·증거 |
|---|---|---|
| 이동/카메라 | WASD(걷기, 차 운전 시 가속·브레이크·조향, 궤도 카메라), 마우스 | [2차 C4] |
| 웅크리기/차 카메라 | C | [2차 C4] |
| 줌 | E 확대 / Q 축소 | [2차 C4] |
| 태블릿 | T(구매 후), 파이 메뉴 `Tablet` 항목 | [2차 C4 C5] |
| 인벤토리·주문·지도 | I / O / M | [2차 C4] |
| 쇼핑 리스트에 추가 | X (프롬프트 바 `ADD TO SHOPPING LIST`) | [관찰 V-B1 t=0:06] |
| **상호작용** | **홀드(마우스 버튼)**: 프롬프트 바 `USE | TAKE OFF PART (HOLD) | ADD TO SHOPPING LIST` | [관찰 V-B1 t=0:06]. 가이드는 `hold down the interact button`이라고만 기술, PC 키 이름은 목록에 없음 → 마우스 아이콘 [2차 C5] |
| 파이 메뉴 | 마우스 오른쪽 클릭(패드 Y/Triangle) | 차 위: Inventory/Car Status/Interior and Additional Parts/Assembly Mode/Examination Mode/Move Car/Shopping List. 부품 위: Inventory/Car Status/Additional Tools/Tablet/Part Mount-Unmount/Shopping List [2차 C5] [관찰 V-B1 t=0:14, `CAR STATUS — Check current car status`] |
| 특수 옵션 | Left Shift; 고착(갈색 윤곽) 나사엔 `WD-04 Rust Remover`를 우클릭 | [2차 C4 C5] |
| 미니게임 | Space | [2차 C4] |
| 핸드브레이크 | Space | [2차 C4] |

### 3.3 조작 문법

- **프리미티브**: 홀드로 제거/설치, 파이 메뉴로 모드 전환(Assembly/Examination/Part Mount-Unmount/Body Disassemble), 홀드로 '점검', 구매 목록에 담기, 미니게임(휠 밸런서·부품 수리대·차체 수리대·Test Path) [2차 C5 C6].
- **연속 입력 → 상태**: 홀드 시간 동안 나사가 풀리고 부품이 분리된다. 일부는 즉시, 나사 달린 것은 홀드로 풀기 [2차 C5]. 상태 피드백은 **윤곽 색**: 초록=제거 가능, 노랑=다른 부품이 막고 있음(먼저 제거해야 함), 갈색=고착 나사(Rust Remover), 녹 표시=교체 신호 [2차 C5].
- **제약**: 올바른 순서만 허용(`Parts can't be installed in incorrect order`), 리프트가 필요한 부품, 단계별 모드 제한. 올바른 설치 순서는 초록 윤곽으로 안내 [2차 C5].
- **상태 피드백**: 카메라 아래 이름 라벨(`Hood`), 상단 모드 태그(`Body Disassemble Mode`, `Part Unmount Mode`, `Examination Mode`), 하단 프롬프트 바 [관찰 V-B1 t=0:06, t=1:52 / t=1:02].

### 3.4 안내·교육 모델 — 안내 수준이 플레이를 바꾸는 방식 (핵심)

| 안내 수준 | 설명 | 증거 |
|---|---|---|
| Easy | 교체할 부품 목록이 **항상 완전히 보임** | [2차 C5 `On the Easy difficulty level, the list of parts to fix is always fully visible`] |
| Normal / Expert | 일부 또는 전부가 `part not discovered`로 숨음. 점검 모드·진단 도구·시험 주행·진단 경로(Test Path)로 발견 | [2차 C5] [관찰 V-B1 t=0:18: 8줄이 모두 `part not discovered`, 빨간 X] |
| 샌드박스 | 주문 없음, 모든 해금, 무한 크레딧 | [2차 C5 beginner's guide] |

- **스토리 주문**(30개): 고객 서술(`Hello, I saw that you've got quite good reviews … The things that each expert said and did didn't match`) + 작업 제목(`Change oil (drain old and refill with new)`) + `Repair with parts of minimum condition 72%`. 만료 없음, **모든 고장을 고치기 전엔 제출 불가**. **일반 주문**은 서술 없음·제한시간·도중 포기 가능(보상 없음) [2차 C5] [관찰 V-B1 t=0:18].
- **진단 4경로** [2차 C5]: ① 점검 모드 ② 진단 도구 ③ 시험 주행 트랙 ④ Test Path. 해결 후에도 남는 `part not discovered`는 대개 **고무 부싱**(서스펜션 곳곳), 크랭크 베어링 캡, 캠 캡이라 직접 분해해야 한다 [2차 C5 undetected].
- **점검 모드**: 파이 메뉴에서 선택 → **상호작용 버튼 홀드**로 한 그룹(서스펜션/배기/엔진) 부품을 훑으면 흰색 → 초록(정상)/노랑·주황(마모)/빨강(교체) 색이 입혀지고 `Examined part  Outer Tie Rod (79%)` 같은 줄이 쌓인다 [2차 C5] [관찰 V-B1 t=1:02]. 시각 점검은 서스펜션·배기·엔진에만 유효 [2차 C5].
- **진단 도구가 '알 수 있는 범위'를 결정**: OBD(점화플러그·코일·촉매·ABS·ECU, OBD 포트 있는 최신 차만), 압축 테스터(크랭크·피스톤·블록), 멀티미터(배터리·알터네이터·스타터·퓨즈·릴레이), 타이어 트레드, 연료압 테스터. 잠긴 도구도 파이 메뉴에 *설명과 함께 잠금 아이콘*으로 표시된다(`TIRE TREAD TESTER — You can check the condition of the tires using this tool.`) [2차 C5 C6] [관찰 V-B1 t=1:40].
- **Test Path**(10,000 CR 해금): `press the gas pedal or the brake pedal according to the pop-ups on your screen` → 끝나면 상세 보고서 [2차 C5]. → *지시를 따르는 조작*이라 CHOOGuard가 피해야 할 형태.
- **UI 규약**: 상단 바 XP/LVL/Scrap/CR, `Car Status`, 쇼핑 리스트, 태블릿 [2차 C5].

### 3.5 실패·결과 모델
- 시간제한·체력 없음. 비용은 **크레딧**: 고객 목록의 부품을 사고, 일반 주문 포기 시 무보상, 부품 수리 미니게임 실패 시 품질↓(15% 미만이면 수리 불가) [2차 C6]. 실패는 경제적이고 되돌릴 수 있다.

### 3.6 평가 모델
- **Finish Order**: 목록의 모든 항목이 초록 체크일 때 활성, CR 탭에서 항목별 지급액·`Bonus 0% CR / 0% XP` [2차 C5] [관찰 V-B1 t=0:18].
- **`EXAMINED PARTS REPORT`**: 점검한 부품과 % 목록 [관찰 V-B2 t=2:30 `Fuel Rail MPI / Fuel Filter / Fuel Pump 100%`, t=3:35 `Starter / Alternator / Medium Fuse type … / Battery 100%`].
- XP 1점/점검 부품, 레벨업이 장소·시설을 해금 [2차 C5]. 점수·등급 없음.

### 3.7 영상 관찰 요약
| 시점 | 관찰 |
|---|---|
| V-B1 t=0:06 | 상단 `Body Disassemble Mode`, 커서 라벨 `Hood`, 하단 `USE | TAKE OFF PART (HOLD) | ADD TO SHOPPING LIST` |
| V-B1 t=0:14 | 파이 메뉴 `CAR STATUS` |
| **V-B1 t=0:18** | **`ISSUES TO FIX` 고객 서술 + `PARTS TO FIX` 8줄 모두 `part not discovered` + 빨간 X, `Repair with parts of minimum condition 72%`** |
| V-B1 t=1:02 | `Examination Mode` 태그, 초록 줄 `Examined part Outer Tie Rod (79%)`, 하단 `EXAMINED PART (HOLD)` |
| V-B1 t=1:40 | 파이 메뉴 `TIRE TREAD TESTER`(잠금 아이콘+설명) |
| V-B2 t=2:30, 3:35 | `EXAMINED PARTS REPORT` 100% 목록 |

### 3.8 CHOOGuard 적용

| 판정 | 메커닉 | CHOOGuard 동사 | 구체 방법 | 증거 |
|---|---|---|---|---|
| **Adopt** | 정보량 난이도(Easy 전체 공개 ↔ Normal/Expert `not discovered`) | Tab 상황판 '조치' 체크리스트, 안내 방송·보고 흐름 | 3단계 프리셋: ①현행(○/● 항목 전체) ②증상만(`가스 냄새 신고 — 원인 미확인`, 조치 목록은 인지한 만큼만 채워짐) ③비움(기록만). 목록 항목 *자체*가 hazard에서 오므로 종류 비의존 | [2차 C5] [관찰 V-B1 t=0:18] |
| **Adopt** | 홀드 입력 + 윤곽 색 3종(가능/막힘/고착) | 문·셔터·에스컬레이터·분전반 | 선행 조건이 있는 조작은 '막힘' 색으로 *무엇이 먼저인지*를 시각화(예: 셔터는 정지 먼저, 문은 열쇠 먼저). 이름은 그대로, 선행 대상에 노랑 윤곽 | [2차 C5] |
| **Adapt** | 점검 모드(홀드로 그룹 훑기 → 색+%, 로그) | 위험 발견·점검, 환자 상태 확인 | 일정 시간 시선을 유지(또는 홀드)하면 대상의 *지각 가능한* 상태가 `정상/주의/위험`+간단 단서로 기록. 시야 밖 대상은 대상이 안 됨(지각 규칙 준수). `상태 확인`이 로그만 남기던 현행을 대체 | [2차 C5] [관찰 V-B1 t=1:02] |
| **Adapt** | 진단 도구가 '볼 수 있는 범위'를 결정 | 점검 | 직무 범위의 계측만 사용: 소화기 압력계 읽기, 분전반 표시등 읽기, 현수막/표시. 새 전문 장비는 부여하지 않음 | [2차 C5 C6] |
| **Adapt** | 주문서 = 서술 + 작업 + 최소 상태 % | 신고·접수 | 상황 개시를 '증상 서술' 한 건으로 주고(JEV가 합성), 조치 문장은 주지 않는다. 일반/스토리 구분은 가져오지 않음 | [2차 C5] [관찰 V-B1 t=0:18] |
| **Adapt** | `Finish Order` 게이트(모든 항목 체크 시 제출) | 현장 인계 | 현행 '지휘 기관 도착 100 s 후 자동 종료'를 *인계 행위*가 있을 때만 진행되도록 바꾸고(인계 안 하면 대기 상태 지속), 인계 시 기관이 확인표를 읽는다 | [2차 C5] [추론] |
| **Adapt** | 파이 메뉴=모드 허브(잠긴 항목도 설명 표시) | Q 무전 휠 | 휠이 정답만 보이던 구조를 '모드별 전체 후보'로: 통신/점검/도구 상위 + 하위 후보. 잠긴 항목은 *능력 설명*만 보이고 **특정 hazard를 암시하는 문구는 금지**(미지각 위험 노출 방지) | [관찰 V-B1 t=1:40] |
| **Reject** | 크레딧·XP·스킬트리·상점 경제 | — | 직무 시뮬과 무관, 목표 전복 | [공식 C1] |
| **Reject** | Test Path형 팝업 지시 따라 누르기 | — | 사용자가 비판한 '지시대로 진행' 구조와 동일 | [2차 C5] |

---

## 4. Pacific Drive (Ironwood Studios / Kepler, 2024-02-21)

### 4.1 출처 표

| ID | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| P1 | https://store.steampowered.com/app/1458140/Pacific_Drive/ | [공식] Steam | 열람 2026-10-04 | `Keep your gas tank filled and your panels intact to withstand the radiation`, `Your car, your way`, 최근 30일 평가 81% |
| P2 | https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=1458140 | [공식] 패치노트·뉴스(60건) | 2024-02 ~ 2026-09 | v1.7.1(2024-09-16) 프리셋 7종·약 50개 신규 설정, v1.9.1(2025-04-03) HUD 3요소 옵션·Jumpstart·Expeditions, v1.15.0(2026-02-23) 최신 패치 |
| P3 | https://www.gamedeveloper.com/business/how-pacific-drive-uses-a-diegetic-ui-to-help-players-fall-in-love-with-a-car- | [공식 발언(게임 디렉터 Seth Rosen)] 인터뷰 기사 | 2023-09-06 | 메뉴 방식 정비를 물리 작업으로 교체, `keyword diagnosis UI, similar to Return of the Obra Dinn`, `We tried to place as much as we could physically into the world` |
| P4 | https://www.gamedeveloper.com/design/did-you-know-the-car-in-pacific-drive-has-a-soul- | [공식 발언] MIT Boston GameDev Q&A 기사 | 2024-05-13 | `IA_Internal_Soul`, quirk=`if-then` 규칙, 마찰(friction)을 의도적으로 배치, 메뉴를 걷는 수리로 바꾼 이유 |
| P5 | https://www.gamedeveloper.com/design/the-secret-sauce-of-pacific-drive-s-spooky-vibes-maintaining-your-car | [공식 발언(Alex Dracott)] | 2022-11-01 | 운전석에서의 *재미있고 공정한* 상호작용, 습관·루틴, 비밀 레벨 `we do think of the car as a character`, 정비 지식 요구를 낮춤(`Top Gear` 접근) |
| P6 | https://pacificdrive.wiki/view/Quirks | [2차] 위키 | 수정 2024-03-22 | Tinker Station 4열 구조, 8판 한도, `BAD GUESS - x/4 CORRECT`, 모듈·난이도 옵션 |
| P7 | https://www.pacificdrive.wiki/view/User_Manual | [2차] 위키(인게임 매뉴얼 전사) | 수정 2026-07-24 | 항목별 **해금 조건** 열(Unlock Condition), 계기·경고등 의미 |
| P8 | https://pacificdrive.wiki/view/Auto_Shop , …/Car | [2차] | 2025-09 / 2025-12 | Status Monitor `Driver's Checklist`, 정거장, 차 부품 19개, Logbook entry |
| P9 | https://progameguides.com/pacific-drive/all-controls-keybinds-in-pacific-drive/ | [2차] 키 목록 | 2024-02-12 | PC 기본 키 |
| P10 | https://www.gamespot.com/articles/pacific-drive-repair-car-guide/1100-6521063/ | [2차] 가이드 | 2024-02-21 | 스캔(`C` 홀드)·수리 키트 5종·Friendly Dumpster |
| P11 | https://steamcommunity.com/app/1458140/discussions/0/4348858679331437277/ , …/4309453072031710361/ | [2차] 커뮤니티 | 2024-02-24 / 2024-03-02 | 루트 플래너 스캔, 부품 제거 입력 `hold the same key you use to remove parts` |
| V-C1 | https://www.youtube.com/watch?v=S-AW-OG2UQc | [관찰] DeLindsay Gaming, Quirks/Tinker 178 s | 2024-02-25 | HUD, Tinker 진단·FIXES 화면 |
| V-C2 | https://www.youtube.com/watch?v=aalti65E8pY | [관찰] GameRiot Part 1(0:00–7:00 받음) | 2024-02-20 | 오프닝 계기판, 첫 미션 트래커 |

### 4.2 조작표 (PC 기본, [2차 P9] — 입력 이름은 ProGameGuides 표기)

| 동사 | 입력 | 비고 |
|---|---|---|
| 상호작용 | **E** | 문·키패드·스테이션 등 |
| 도구/아이템 사용 | **M1(좌클릭)** | 손에 든 도구 |
| 부품 분리/예치 | **R**(제거는 **홀드**) | `hold the same key you use to remove parts from your own car` [2차 P11] |
| 스캔 | **C 홀드** | 손상 부품을 스캔하면 `what type of item is needed` 표시 [2차 P10] |
| 발로 차기 | F | |
| 퀵슬롯 | 마우스 휠(`Cycle Hotbar Left/Right`), `Q` 홀스터 | [2차 P9]. HUD에 슬롯 번호 1–6이 표기됨 [관찰 V-C1 t=0:20] — 숫자키 바인딩 여부는 미확인 |
| 인벤토리 | Tab | 인벤토리 안: M1 집기/놓기, R 회전, Space 스택 이동 |
| 차 안 | W/S 가속·브레이크, 라디오·와이퍼·헤드라이트·경적 키, 능력 1–4 슬롯 | 시프터 P/D 전환·시동 키는 매뉴얼에 **기능은 있으나 입력은 미확인**(`In-Car Controls`, [2차 P7]) |
| 기타 | T = Warp Car(ARC 워프), V = 체력 아이템 | [2차 P9] |

### 4.3 조작 문법

- **물리 설치**: 메뉴 기반이던 정비를 *걸어 다니며* 하는 작업으로 바꿨다. 제작은 워크벤치, 수리는 도구를 부품에 사용, 교체는 홀드 제거+설치 프롬프트(`using the indicated prompt on the spot while holding the wheel` [2차 IGN 검색 스니펫]). 의도: 속도보다 *마찰(friction)* 이 투자·유대를 만든다 [공식 발언 P4].
- **'차는 방패'**: 부위별 HP(문 5, 패널 5, 바퀴 4, 범퍼 2, 엔진·배터리·헤드라이트·창 등), 한 부위가 독립적으로 손상 [2차 P8 P10]. 조건이 나쁘면 위험해진다(`Keeping your doors closed and car parts in good condition will give you a dependable barrier`) [2차 P7].
- **스캔 → 진단 → 수리 분리**: 스캔은 *필요한 아이템 종류*만 알려 주고, 수리는 별도 제작·소모 자원이 든다(수리 퍼티는 여러 번, 나머지 키트는 1회용) [2차 P10].
- **퀵슬롯 + 사용 키 통일**: 손에 든 도구를 M1으로 쓴다. 도구 교체는 마우스 휠(숫자키 바인딩은 미확인).

### 4.4 안내·교육 모델

- **User Manual(인게임)**: 항목마다 **해금 조건**이 붙어 있다. 예) `Car Dashboard` ← 루트를 계획한 뒤 차에 타기; `Quirks` ← 퀴크를 가진 채 정비소에 복귀; `Fatigue` ← 처음 부품에 피로 상태가 생김; `Storms` ← 처음 폭풍에 따라잡힘; `Zone Conditions` ← 특이 상황을 처음 통과. 즉 **처음 마주친 순간 해금되어 로그에 남는 just-in-time 매뉴얼** [2차 P7].
- **Logbook**: 스캔한 이상 현상·자원이 채워지고, `Zone Conditions` 항목은 겪을 때마다 갱신 [2차 P7 P8].
- **Status Monitor / Driver's Checklist**: 정비소 대형 화면 상호작용으로 `Driver's Checklist of recommended tasks before setting out`을 *표시/숨김* [2차 P8].
- **경고등 색 규약**: `An orange light signals a developing problem. Red calls for immediate action.` (Brake/Fuel/LIM/Health/Maintenance/Steep/Surface/Status effect/Hazard/Storm) [2차 P7].
- **미션 트래커 최소화**: `The Olympic Exclusion Zone / Get To Safety / ☐ Find transportation` 한 줄 체크박스 [관찰 V-C2 t=6:55], `THE MID-ZONE CROSSING / GET THROUGH THE EXPANSION WALL / ☐ FIND A WAY INTO THE WALL` + `COLLAPSE` 토글 [관찰 V-C1 t=0:20]. 퀘스트 마커는 점진적으로 *늘림*(`Quest markers now appear in more situations`, v1.13.0) [공식 P2].
- **조절 노브(공식)**: v1.7.1 `7 brand new presets` + `50 additional new settings` — `Scenic Tour`(제작·소모·위험·폭풍·피해 하향 또는 해제, `The player cannot die`), `Joyride`(긴장은 유지, 수집·제작 요구 하향) 등. v1.9.1 `HUD now has three display options under Accessibility Settings … Player Info HUD, Mission HUD, Item & Interaction HUD`, `Jumpstart` (경험자는 첫 퀘스트 건너뜀), `Field Guide Quests … used as a tutorial` [공식 P2]. Quirk 난이도: `Free Quirk Guesses`, `Free Quirk Hints` [2차 P6].
- **개발자 의도**: `keyword diagnosis UI, similar to Return of the Obra Dinn, where you have to figure out what's going on with your car if you want to fix it` [공식 발언 P3]; 지식 요구는 `not hard to swap out core parts`, `Ironwood Studios doesn't want to judge players for not knowing what ratchet to use` [공식 발언 P5 기사 서술].

### 4.5 실패·결과 모델

| 범주 | 규칙 | 증거 |
|---|---|---|
| 플레이어 HP | 상단 좌 HP 바(100%)와 방사선 바(0.0) | [관찰 V-C1 t=0:20] |
| 방치 결과 | 차 부위 HP가 0이면 해당 방어 상실, 문이 열린 채로면 방사선·이상 현상 노출 | [2차 P7] |
| 치명 시 | `When your condition becomes critical, the car returns you to the garage. However, this isn't without a cost … the resources you've collected have been lost.` → **런은 끝나지만 캐릭터 삭제는 아님** | [2차 P7 `Critical Harm in the Zone`] |
| 퀴크(숨은 규칙) | `When 1 does 2, 3 does 4` 형태의 *숨은 인과*(예: 트렁크가 닫히면 오른쪽 뒷문이 열림). 시간이 지나며 생기고, 손상·이상 현상 접촉 시 빨리 생김. 일부는 유리, 일부는 악의적 | [2차 P6] [관찰 V-C1 t=2:00 `TRUNK IS CLOSED ▶ REAR RIGHT DOOR OPENS`] |
| 마모 | 모든 부품이 마모, 결국 교체 필요 | [2차 P7 `Fatigue`] |
| 쉬운 모드 | Scenic Tour는 사망 불가 | [공식 P2 v1.7.1] |

### 4.6 평가 모델
- 점수·등급 **없음**. 진행은 로그북·자원·블루프린트·미션. 런 종료 평가는 *수집한 것*과 *복귀 성공 여부* [2차 P7]. 퀴크 진단은 판정마다 즉시 정오 피드백(§4.7).

### 4.7 Tinker Station — 진단 UI 정확한 형태 (결정적)
[2차 P6] + [관찰 V-C1]
- 열: **1 빨강=트리거 부품**, **2 초록=트리거 동작**(`IS CLOSED/IS OPENED/IS EMPTY/IS FULL`), **3 주황=작동 부품**, **4 파랑=거동**(`TOGGLES/SWITCHES ON/SWITCHES OFF/BRIGHTENS/DIMS/FLICKERS/OPENS/CLOSES`) [관찰 V-C1 t=1:12, t=1:35].
- 제출: 화면 하단 `Submit Diagnosis? You have 5 guesses remaining.` Y/N [관찰 V-C1 t=1:12]. 오답: 배너 `*** INCORRECT DIAGNOSIS ***` [관찰 V-C1 t=1:35]; 위키는 오답마다 `BAD GUESS - x/4 CORRECT`(4칸 중 n칸 일치)로 표시된다고 서술하며 Investigator Module은 *어느 칸이* 맞는지를 알려 준다 [2차 P6, 이 문구 자체는 미관찰]. 정답: `CORRECT DIAGNOSIS - GUESS REFUNDED` [2차 P6].
- 한도: 런당 8판(모듈마다 +1) [2차 P6]. 보조: Investigator(맞는 부분 알림), Analysis(Anchor 에너지로 한 칸 확정, 0.5 kLIM씩 증가) [2차 P6] [관찰 V-C1 t=1:35 `INVESTIGATE 0.0/0.5 kLIM`].
- 결과: `FIXES` 탭 `ERROR LOG 0xD3ADBE3F`에서 진단된 항목은 `Here's what you'll need to fix this: 1: Mechanic's Kit [01]`, 미진단 항목은 `DIAGNOSTICS REQUIRED` [관찰 V-C1 t=2:00]. 퀴크를 고치지 않고 *그대로 둘 수도 있다* [2차 P6].
- 권장 탐색법(위키): 차고에서 주차 상태로 시동 → **한 번에 하나만** 바꿔(와이퍼·헤드라이트·라디오·조향·가속) 원인을 격리 [2차 P6].

### 4.8 CHOOGuard 적용

| 판정 | 메커닉 | CHOOGuard 동사 | 구체 방법 | 증거 |
|---|---|---|---|---|
| **Adapt (최우선)** | 4열 문장 빌더 + `x/4 CORRECT` + 제한 판정 | 무전 보고·안내 방송·기관 요청 | Q 휠의 '현재 정답 문장'(오답 없음)을 **슬롯형 보고**로 교체: `[어디] [무엇이] [어떤 상태] [몇 명/얼마나]`. 후보는 **플레이어가 지각한 사실만** 채워지고, 사무소는 정답을 말해 주지 않고 *확인 질문*(`위치가 불명확합니다`, `몇 명입니까`)과 '4칸 중 n칸 일치' 수준의 부분 피드백만 돌려준다. 판정 횟수 제한이나 보조 모듈 대신 '보고 지연'이 비용. 슬롯은 장소·대상·상태·인원 등 *종류 비의존* | [관찰 V-C1 t=1:12, 1:35] [2차 P6] [공식 발언 P3] |
| **Adopt** | 진단/조치 분리(`FIXES`가 필요한 *자원*만 목록) | 무전 답신 | 사무소 답신을 '지시문'이 아니라 *동원 가능 자원 목록*(소방 출동, 방송 가능, 열차 보류 가능 등)으로 바꾸고 **어떻게 쓸지는 플레이어 판단**. 현행 `초기 진화 가능하면 시도하고 무리하지 마십시오` 류 명령 제거 | [관찰 V-C1 t=2:00] |
| **Adopt** | 진단 보조를 *자원·설정*으로(Investigator/Analysis, Free Hints) | 난이도 | 보고 정오 힌트를 기본 모듈로 두고, 설정에서 끄기/완전 공개로 조정. §3의 정보량 프리셋과 합쳐 '안내 3단계' 구성 | [2차 P6] [공식 P2] |
| **Adopt** | 해금형 User Manual(처음 마주친 순간 항목 해금) + 로그북 | 교육·인지 | 수첩 항목이 *처음 지각한 hazard 유형*(예: 연기, 쓰러진 사람)에서 해금되어 공개 행동요령으로 기록. 내부 SOP로 표기 금지. 미지각 유형은 항목 자체가 존재하지 않음 | [2차 P7 P8] |
| **Adapt** | Status Monitor + 상호작용으로 표시/숨김하는 체크리스트 | Tab 상황판 | Tab 체크리스트를 *사무실 모니터·휴대 수첩*으로 이동하고, 기본은 숨김. 보면 시간이 흐르는(다른 일을 못 하는) 비용이 있어도 좋다 | [2차 P8] [추론] |
| **Adapt** | HUD 3요소 개별 끄기(Player Info/Mission/Item & Interaction) | HUD 전반 | `상호작용 HUD` 끄면 프롬프트 없이 월드 단서만으로 조작. 기본값은 켜짐 유지, 설정에서 전환 | [공식 P2 v1.9.1] |
| **Adapt** | 프리셋 7종·약 50 설정, `Scenic Tour`(사망 불가) | 난이도·접근성 | 안내 3단계 × 위험 강도 × 탈락 규칙 3축 프리셋(예: '견학/실습/실전'). '견학'은 노출 한계 없음 | [공식 P2 v1.7.1] |
| **Adapt** | 치명 시 복귀(자원 손실, 런 지속) | 플레이어 위험 | 노출 한계 도달 시 동료가 철수시키고 교대는 계속(이후 조치는 인계 로그에 `현장 이탈`로 기록). 영구 사망·즉시 종료 대신 *기록 손실* | [2차 P7] [추론] |
| **Adapt** | 부위별 HP '방패' 인과 | 플레이어 위험, 대피 | 방화문·셔터·출입문 상태가 노출 속도에 영향(`문이 열려 있으면 연기가 샘`). 문 상태를 플레이어가 *행동으로 바꾸는* 변수로 | [2차 P7] |
| **Adapt** | 경고 색 2단계(주황=진행 중, 빨강=즉시) | 현장 표시등·소리 | 현장 단서의 *색·소리 규약* 통일. 일반 사람이 알 수 있는 범용 규약만 | [2차 P7] |
| **Adapt** | 인과 격리 탐색(퀴크 진단법) | 위험 발견 | 한 행동이 다른 곳에 만드는 *2차 효과*(문 열기→연기 이동)를 플레이어가 관찰해 추리하게 하되, 조합은 hazard 공통 규칙으로 | [2차 P6] [추론] |
| **Reject** | 제작·자원 경제, 블루프린트 해금 | — | 직무 범위 밖 | [공식 P1] |
| **Reject** | 숨은 인과를 8판으로 *맞히기*(블랙박스 추리) 그대로 | — | 현실 직무에서 임의 규칙을 추리하는 것은 부적합. 슬롯형 보고에서 '사실 확인'으로 변환할 때만 채택 | [추론] |

---

## 5. 교차 종합 — 4개 게임이 공유하는 패턴

| # | 패턴 | MSC | MWC | CMS | PD | CHOOGuard 현행과의 격차 |
|---|---|---|---|---|---|---|
| 1 | **맞음(fit) 검증, 의도 안내 없음** | 볼트 규격 일치 시 초록, 커서 글자=부품 이름 | 동일 + 러그 렌치 필수 | 윤곽 초록/노랑/갈색 | 프롬프트로 조작 가능 대상만 | CHOOGuard는 정답 행동명을 선제 제공 |
| 2 | **연속 입력=물리 상태, 한계에서 정지** | 휠 8틱, 래칫 왕복, 한계 무음 | 동일 | 홀드 시간, 점검 홀드 | 스캔·제거 홀드 | E 탭 계약은 표현 불가 |
| 3 | **조용한 불완전 상태, 지연·간접 결과** | 이탈/누유/화재/검사 X | 동일 + 체온 | 즉시 % (지연 없음) | 퀴크(숨은 인과) | 결과는 즉시 ●, 또는 없음 |
| 4 | **진단 사다리**(눈→소리/색→계기→서비스) | 연기색·소리·경고등·충전기 바늘·영수증 | 동일 | 점검 모드→도구→시험 주행→Test Path | 스캔→Tinker→Fixes | 사다리 없음(발견 즉시 이름 제공) |
| 5 | **안내=문서·계기, 마커 아님** | 냉장고 메모·카탈로그·영수증 | VIN 해독표·딜러표 공개 | 주문서·EXAMINED REPORT | User Manual(해금형)·Logbook·Checklist | 길 안내 기본 켜짐·고정 체크리스트 |
| 6 | **난이도=정보·보조 구성** | 설정+구매 보조품 | 슬라이더 | Easy/Normal/Expert, Sandbox | 7 프리셋·50 옵션·HUD 3요소 | 안내 수준 1단계뿐 |
| 7 | **실패=소프트(진행 유지) 또는 선택형 하드** | 영구사망 선택 | 동일 | 경제적 | 복귀+자원 손실, Scenic Tour | 실패 없음 |
| 8 | **물리 제약이 결정을 만든다** | 도구 모드/단일 아이템/두 손 | 동일 | 순서·리프트 | 도구 슬롯·전용 키트 | 손 상태 제약 약함 |
| 9 | **평가=문서** | 영수증 X | 역사·빙상 검사 | 보고서·Finish Order | 로그북 | 점수 없음(좋음)이나 문서도 없음 |

**CHOOGuard에 옮기는 우선순위 제안(근거 위 표)**
1. 프롬프트 = 대상 이름 + 맞음 하이라이트(§1.8 1행) — 지시 느낌을 가장 직접 줄임.
2. 핵심 조작 4–5개만 홀드+틱(밸브·분전반 레버·수동 셔터/문·복구 스위치) + 불완전 상태 허용(§1.8 2–3행).
3. 무전 휠 → 슬롯형 보고(§4.8 1행). 사무소 답신은 *자원 목록+확인 질문*(§4.8 2행).
4. 안내 3단계 프리셋 + HUD 요소별 끄기(§3.8 1행, §4.8 6–7행).
5. 인계 확인표(영수증형, 점수 없음)(§1.8 5행).
6. 노출 게이지 + 소프트 실패(§1.8 9행, §4.8 8행).

**하드 제약 점검**
- *역무원이 실제 하는 일만*: 모든 제안은 밸브·분전반·문·무전·소화기·안내·환자 곁 지키기 범위. 새 전문 장비(OBD, 압축 테스터, 방사선계) 불가 — CMS/PD의 도구 사다리는 **'눈·손·읽는 계기'**로만 번역.
- *종류 비의존*: 슬롯형 보고·확인표·완료도·잔류·노출은 hazard 공통 변수에 얹는다. MSC/CMS/PD 자체가 '부품·퀴크 목록'을 종류별로 가지므로 *문법*만 가져온다.
- *미지각 위험 비노출*: 보고 후보·수첩 해금·체크리스트 항목은 모두 '지각함' 상태 이후. 잠긴 파이 항목의 설명은 능력만(§3.8 7행). 인계 후 요약에서만 '놓친 것'을 보여준다.
- *디렉터·세계 난수 비소모*: 완료도 감쇠·증상 변화·노출 증가는 결정적 함수로.

---

## 6. 공백·한계 (확인하지 못한 것)

1. **소리**: 영상 소리를 듣지 못했다. 틱음·정지음·배기음·쿵 소리의 *음색·크기*는 위키 설명과 공식 패치노트(`bolting sound`)로만 근거한다.
2. **MSC 스크롤 체감**: 휠 1틱의 시간·감도·가속(빠른 왕복 래칫 효과)의 실측은 없다.
3. **MSC 볼트 8틱**: 위키 직접 서술이 없고 Reddit 검색 스니펫(본문 로딩 실패)+모드 HUD(`4/8`)로 추정. 확정도 중.
4. **MSC 욕구 사망 임계**: Guide 2(1.5배)와 Player attributes(증가율 환산 220%대)가 불일치. 수치 미채택.
5. **MSC 키 바인딩**: 모든 기본 키는 [2차]. 게임 내 컨트롤 화면 직접 캡처 없음. `Use = F`는 Steam 스레드·위키 가이드로만.
6. **MWC**: EA이고 영상 미관찰. `mywintercar.com` 서술 다수는 저신뢰라 미채택. 조작 변경은 패치노트에 나온 범위만.
7. **CMS 2021**: PC '상호작용' 키 이름이 키 목록에 없음(프롬프트 바 마우스 아이콘 확인). Expert 난이도의 세부 차이, 고착 나사 해제·수리 미니게임·Test Path 직접 관찰 못 함. `Examination`이 그룹 단위로 바뀐 점은 커뮤니티 증언+영상 일치.
8. **Pacific Drive**: 차 내부 손 조작(시동 키, 시프터, 핸드브레이크) **입력과 연출 미확인**(`In-Car Controls`는 기능 서술만). 스캐너 UI·Driver's Checklist 화면 직접 관찰 못 함. 공식 컨트롤 페이지 못 찾음. 첫 1시간 튜토리얼(Oppy 무전 안내)은 오프닝 7분만 관찰.
9. **Reddit/IGN/MagicGameWorld**: 일부 페이지는 로딩 실패(403/빈 본문)로 스니펫 수준.
10. **평가·점수 카테고리**: MSC 검사 외엔 4게임 모두 숫자 점수가 없다 — 즉 '점수 카테고리' 벤치마크는 성립하지 않으며 그 자체가 결론이다.

---

## 부록 — 미디어 인덱스 (`media/A/`, git 제외: `.gitignore:34 .planning/**`, 합계 약 86 MB)

| 파일 | 원본 | 용도 |
|---|---|---|
| `msc_ogygia_ZVpLCoOwHBs_0000-0400.webm` | V-A1 0:00–4:00 | MSC 볼트·렌치 |
| `msc_frame_t56/t70/t100/t125/t145.png` | V-A1 | 이름 라벨·✔·렌치·초록 볼트(t=145) |
| `msc_T8dyi_GUwhocP0cRA_full.webm`, `msc_distributor_t27/t40.png` | V-A2 | 손 모드 vs 도구 모드 |
| `msc_Ginauz_OeZRhOy2RHE_full.webm`, `msc_tuning_t98.png` | V-A3 | 밸브 조정 나사·`MORTAL` |
| `cms21_DG_KoS0PecyKy0_full.webm`, `cms21_orders_t14/t18.png`, `cms21_notdiscovered_t6/t62/t100/t112.png` | V-B1 | 프롬프트 바·주문서·점검 모드·파이 메뉴 |
| `cms21_TacticalTurbo_R2mt67PQEPg_0000-0500.webm`, `cms21_diag_t150/t215.png` | V-B2 | EXAMINED PARTS REPORT |
| `cms21_redfirekeep_wguF6yI2JUI_full.webm` | V-B3 | 점검 모드 |
| `pd_DeLindsay_S-AW-OG2UQc_full.webm`, `pd_tinker_t20/t72/t95/t120.png` | V-C1 | HUD, Tinker 진단·FIXES |
| `pd_GameRiot_aalti65E8pY_0000-0700.webm`, `pd_open_t415.png` | V-C2 | 오프닝, 미션 트래커 |
