# Findings

## 0. 현행 진단 — 왜 '지시대로 진행'인가 (코드 근거, 2026-10-04 읽기 전용 감사)
경로 접두 `Assets/ChooGuard/App/Fps/`. 원 감사: agent://InteractionMechanicsAudit, agent://GuidanceLoopAudit.

### 0.1 입력
- E 한 번 누름(상승 에지)만 있다: `FirstPersonResponder.cs:130-132`. 계약 `IFpsInteraction`은 `CanInteract`/`TryInteract`뿐이라 홀드·조준·타이밍을 표현할 수 없다(`IFpsInteraction.cs:3-8`). R 보조(문 열쇠 스위치·셔터 복구·에스컬레이터 재가동 3곳).
- 좌클릭은 손에 든 소화기·관창에만(`EmergencySession.cs:275-276`), G 놓기, Q 홀드 무전 휠(마우스 방향), Tab 상황판, M 안내도. 우클릭·휠·숫자키·웅크리기 없음. 점프 비활성.
- 약 30개 상호작용 클래스 중 약 25개가 '프롬프트가 뜨면 E'.

### 0.2 지시의 출처
1. 프롬프트가 정답 이름을 말하고 맞을 때만 뜬다: `가스 중간밸브 잠그기`(Kitchen/GasValvePoint.cs:44), `튀김기 전원 끄기`(KitchenAppliancePoint.cs:47), `주변 접근 통제`(IncidentDirector.cs:794-801), `현장 인계`(Responders.cs:212).
2. 역무실 무전 답변이 보고마다 다음 할 일을 명령: 화재 `초기 진화 가능하면 시도하고 무리하지 마십시오.`(Hazards.cs:268-269), 쓰러짐 `환자 곁을 지키고 주변을 비워 주십시오 … 자동심장충격기를 가져와 환자 곁에 두십시오`(Hazards.cs:560-562), 셔터 `조작함의 정지 버튼으로 멈추고, 열쇠로 복구 스위치를 눌러 올리십시오.`(FacilityHazards.cs:406), 가스(FacilityHazards.cs:475), 주방 화재 K급 안내(IncidentDirector.KitchenGas.cs:169,181,193).
3. Q 무전 휠은 지금 맞는 다음 단계만 보여 준다(오답 없음): 보고는 인지한 위험만(IncidentDirector.cs:524-528), 방송은 보고 뒤(:530), `초기 진화 완료 보고`는 불이 꺼지고 보고한 뒤(IncidentDirector.Fire.cs:259-262). 휠은 Q마다 재구성(EmergencySession.cs:338-344). 말할 문장도 미리 쓰여 있다(IncidentDirector.cs:552-553).
4. Tab 상황판 '조치' 열이 ○/● 체크리스트(IncidentDirector.cs:878-891): 역무실 보고·안내방송·직접 대피 안내·접근 통제·초기 진화·열차 출발 보류·현장 인계.
5. 길 안내 기본 켜짐(Shell/GameSettings.cs:31): 바닥 점선 34 m, 나침반, 지도 경로(EmergencySession.cs:292-326; Hud/WorldRouteGuide.cs).
6. 브리핑이 곧 루프: `이상을 발견하면 알리고, 사람들을 지키고, 도착한 기관에 인계하세요.`(EmergencySession.cs:125-126). 실수 뒤 토스트가 정답을 알려 준다(KitchenGas.cs:311, ElectricPlaza.cs:~380, FireSafety.cs:208).

### 0.3 결과가 없다
- 체력·타이머·실패 없음. 위험은 화면 가장자리 비네트만(Hud/GameHud.cs:120,192-194). 종료 화면 주석 `No score`(Emergency/ShiftLog.cs:348).
- 방치해도 대신 처리: 역무실이 119 자동 신고(IncidentDirector.Facility.cs:381-396), 승객 신고(Fire.cs:241 등), 기관이 진화·처치·제압(Responders.cs:273-283), 지휘 기관 도착 100초 뒤 인계 없이 자동 종료(IncidentDirector.cs:176-182).
- 로그만 남는 행동: AED 내려놓기, 상태 확인, 말로 진정, 움직이지 말라고 안내(IncidentDirector.Casualty.cs:370-387 등). 지진·가방은 해결 개념이 없고 종료는 같다.

### 0.4 이미 깊이가 있는 것(살릴 것)
- 소화기: E→좌클릭 0.8 s 안전핀→재입력→분사 홀드, 거리 1.2–5 m·각도 8°→22°·불 아랫부분 조준, 14 s 약제, 숨은 불량(Emergency/StaffTools.cs:15-47,274-336). 약제 종류 규칙(FireHazard.Oil.cs:41-53), 살아 있는 전원·가스 공급(FireHazard.Feed.cs:17,44).
- 옥내소화전 호스: 30 m 줄, 2–10 m 조준, 전기에 물이면 감전·관창 놓침(StaffTools.cs:349-376; IncidentDirector.ElectricPlaza.cs:318-323).
- 분전반·차단기: 화재 중 개방 불가, 메인/분기 선택, 잠금표, 재투입 시 재발화(ElectricPlaza/*.cs; IncidentDirector.ElectricPlaza.cs:274-312).
- 시야 기반 발견(거리·시야각·가림, IncidentDirector.cs:456-474), 에스컬레이터·셔터 재가동 조건(StationLinks.cs:101-112; FireShutterPoint.Operate).
- 환자 상태 0–4단계(심정지 포함, IncidentDirector.Casualty.cs:42-79)는 CPR·AED 조작을 붙일 수 있는 모델이다.

## 1. 레인별 결과 파일
- A 손 조작 정비: `findings-A.md` (My Summer Car·My Winter Car·Car Mechanic Simulator 2021·Pacific Drive)
- B 물리 손·운전실·잔여 피드백·노-HUD: `findings-B.md` (Frictional/Amnesia·Train Sim World 5·PowerWash Simulator·Escape from Tarkov)
- C 현장 대응: `findings-C.md` (Firefighting Simulator The Squad/Ignite·Ambulance Life·Police Simulator: Patrol Officers·Arma 3 ACE3/KAT)
- D 절차·판단·보고·안내 노브: `findings-D.md` (SWAT 4·Ready or Not·Papers, Please·112/911 Operator·Hitman WoA·Phasmophobia)
- 영상·프레임은 `media/<lane>/`(git 제외, 약 1.2 GB, md/json 없음 확인).

## 2. Main 교차검증 (2026-10-04)
| 주장 | 확인 방법 | 결과 |
|---|---|---|
| MSC: 규격 맞는 렌치일 때만 볼트 초록, 좌상단 욕구 패널, 우상단 도구 모드 글리프 | `media/A/msc_frame_t145.png` 직접 열람 (https://www.youtube.com/watch?v=ZVpLCoOwHBs t=2:25) | 일치. 화면의 `main bearings 2x9` 상자는 업로더 자막이지 게임 UI가 아님 |
| Ready or Not: 점수 화면이 항목별 `x/y`+점수, 숨은 소프트 목표 이름은 사후 공개 | `media/D/ron_score_final.png` 열람 (https://www.youtube.com/watch?v=PNZWrJcsP90 t≈3:50) | 일치: `MISSION OBJECTIVES 4/4 2,000`, `SOFT OBJECTIVES 1/1 50 — REPORTED INCAPACITATED VETERAN`, `SUSPECTS 4/4 140`, `CIVILIANS 5/5 175`, `EVIDENCE 5/5 125`, `NO OFFICERS DEAD 4/4 400`, 총 2,890, S/HARD |
| Firefighting Simulator: Ignite 종료 카드 3체크+통계 | `media/C/frames-ignite/trailer-t456.png` 열람 (https://www.youtube.com/watch?v=OqOfD9CK3iU t=7:36) | 일치: `SILVER RATING`, ✔`Mission successfully completed` ✔`No casualties` ✘`Completed in 31:30`, STATS |
| Frictional 제스처 문법 | AMfP 공식 Steam 매뉴얼 PDF(https://cdn.fastly.steamstatic.com/steam/apps/239200/manuals/Manual.pdf) 직접 추출 | 일치: Door "move the mouse in the direction you want it to go", Lever "Simply move the mouse, while holding down the left mouse button, to move the lever to either end.", "When you are interacting with a wheel or a lever, move the mouse in a circular motion" |

## 3. 역무원 동사의 한국 공개 행동요령 근거 (Main 확인)
- 소화기: 안전핀 뽑기 → 바람(실내는 출입문)을 등지고 노즐을 불 쪽 → 손잡이 움켜쥐기 → 빗자루로 쓸듯이. 행정안전부 안전배움터 https://www.mois.go.kr/chd/sub/a06/fire_2/screen.do , 국민재난안전포털 http://mepv2.safekorea.go.kr/safety_guide/safeGuide/showDetail_02011_23.html
- 옥내소화전: 소화전함 문 열기 → 노즐·호스를 꺼내 꼬이지 않게 펴며 불 가까이 → 개폐밸브를 돌려 물이 나오게 → 물이 차면 노즐 끝을 돌려 분무/직선. 국민재난안전포털 http://mepv2.safekorea.go.kr/safety_guide/safeGuide/showDetail_02011_02.html (광명시 동일 문구 https://www.gm.go.kr/pt/partInfo/sf/emergencyact/lifesafety/PTMN775.jsp)
- 가스 누출: 점화콕·중간밸브·용기밸브를 잠가 공급 차단 → 창문·출입문 열어 환기 → 환풍기·선풍기 등 전기기구 스위치 금지(스파크). 한국가스안전공사 https://www.kgs.or.kr/kgs/abad/view.do
- 심폐소생술: 어깨 두드리며 반응 확인·호흡 관찰 → 주변에 도움·119·AED 요청(아무도 없으면 직접 119) → 가슴압박 30회(성인 분당 100~120회, 5~6 cm, 완전 이완) → 인공호흡 2회 또는 가슴압박만 → 구급대 도착까지 반복, 2인이면 5주기마다 교대. 광진구 보건소 https://www.gwangjin.go.kr/health/main/contents.do?menuNo=300180 . 2025 가이드라인(질병관리청 2026-01-29 카드뉴스 https://www.kdca.go.kr/bbs/kdca/375/305503/download.do): 순서는 기존 유지, 구급상황요원이 AED 확보·사용 지도, 압박 시 편한 손을 아래로.
- 자동심장충격기: ①전원 켜기 ②패드 2개(오른쪽 빗장뼈 아래 / 왼쪽 젖꼭지 아래 중간겨드랑선) ③"분석 중…" 음성 → CPR 멈추고 손 떼기, 필요 시 "심장충격(제세동)이 필요합니다"+자동 충전, 불필요 시 "환자의 상태를 확인하고, 심폐소생술을 계속 하십시오." ④깜빡이는 버튼 누르기 전 모두 떨어졌는지 확인 ⑤즉시 CPR 재개, AED는 2분마다 재분석, 구급대 도착까지. 서대문구 보건소 https://sdm.go.kr/health/contents/medicine/drug-safety/usingAutomatic
- 한계: 위 자료는 일반 국민 행동요령이다. 코레일 역무원 내부 SOP·비치 장비(차단봉·라바콘·확성기 보관 위치 등)는 확인하지 않았다.
