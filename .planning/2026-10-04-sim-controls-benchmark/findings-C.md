# findings-C — 현장 대응 시뮬 4종의 조작·플레이 방식 정밀 분석 (Lane C)

조사 기준일 2026-10-04. 대상: **Firefighting Simulator – The Squad(2020) / Ignite(2025)**, **Ambulance Life: A Paramedic Simulator(2025)**, **Police Simulator: Patrol Officers(2022)**, **Arma 3 ACE3 의료(+KAT/KAM AED)**. 작성: LaneC_FirstResponders가 소방 부분을 직접 조사·관찰하고, 구급·경찰·ACE3는 하위 3개 에이전트(LaneC_AmbulanceLife·LaneC_PoliceSim·LaneC_ACE3Medical)가 조사한 결과를 대조·통합했다.

- 증거 태그: **[공식]** 공식 사이트·스토어·뉴스·변경기록·개발자/퍼블리셔 직원 답변·공식 소스코드, **[2차]** 가이드·리뷰·커뮤니티·단축키 표, **[관찰 URL t=mm:ss]** 영상 프레임을 직접 확인, **[추론]** 조사자의 해석.
- 미디어는 `media/C/`(git 무시)에만 있고 문서에는 URL·시각만 적는다. 표의 `FS-n`·`AL-n`·`PS-n`·`ACE-n`은 각 게임 절의 n번 소절을 가리키고, 소절 안의 `§n`은 *그 게임 절 내부* 번호다.
- 이 문서의 `CHOOGuard 적용` 표는 **제안 근거**이며 구현 지시가 아니다. 하드 제약(역무원 권한·종류 비의존·미지각 위험 비노출)은 모든 표에서 점검했다.

---

## 0. 한눈에 보는 결론 (Main용)

1. **"지시대로 진행"이 되지 않는 4종의 공통점은 정답 이름이 붙은 프롬프트·다음 단계 체크리스트가 *튜토리얼 또는 보조 모드에만* 있다는 것이다.** Ignite는 첫 임무에서 `Task completed — Task "Equip Halligan bar" has been completed` 토스트와 `Equip Hose — Equip the hose using [E] …` 패널로 지시하지만 [관찰 QHg_V0neE3Q t=02:00, 03:15], 튜토리얼 이후 본 임무의 HUD는 좌상단 목표 라벨+불 아이콘 진행 바와 우상단 팀 상태 바뿐이다 [관찰 OqOfD9CK3iU t=04:10; K7OxcFthrKk 9:58–10:14 클립]. Ambulance Life는 Classic 모드에서만 "Recommended" 치료 체크리스트와 증거 적/녹 평가를 보여 주고 Patch 1.3(2025-03-06)이 Simulation에서 그것을 끈다 [공식]. PS:PO는 Casual에서만 위반자 머리 위 ↓ 마커와 초록 힌트를 준다 [관찰 649uI00KAeg t=0:24]. ACE3는 다음 할 일도, 병명("심정지")도 기본값에서는 말하지 않는다 [공식 소스].
2. **오답 선택지를 목록에 *보여 주고 실행시킨 뒤 결과를 말한다.** PS:PO 방사형 메뉴는 해당 없는 위반까지 전부 나열하고(주차 7종 `Parking Meter Expired … Position & Alignment`) 사후에 `Justified: ID Check — Your suspicion was strong enough …`를 보여 준다 [관찰 p46792Lsswo 22:30, 23:10]. ACE3 `canCPR`는 기본값에서 의식 없는 모든 환자에게 CPR을 노출한다(소스 주석 "if basic diagnose, then only show action if appropriate")[공식 소스]. Ambulance Life는 비심정지에 CPR을 고르면 경고창만 띄우고 실행을 허락한다 [관찰 5G1Ucc8zF6I ≈48:31]. 이는 CHOOGuard의 "Q 휠은 지금 맞는 다음 단계만 보임"과 정반대다.
3. **난이도 = 안내 토글의 묶음.** PS:PO(Casual/Simulation + `Immediate Points`·`Detailed Score Explanations`·`Radial Menu Tooltips`·`CP Deduction`), Ambulance Life(Classic/Simulation/Custom + `World Markers`·`HUD Treatment Guidance`·`Show new evidence markers`·`Extend triage time`·`Disable Medical Procedure Minigames` 등), ACE3(`advancedDiagnose` 0–3 등 + 공식 프리셋), Squad(`Hide Objective Indicators`·`Hide Victim Indicators`·`Establish Supply Line by Yourself`), Ignite(Easy/Medium/Hard + AI 자율 3종 + `Simplified inputs`). **노브는 "게임이 *무엇을 말해 주는가*"를 바꾸지 "플레이어가 *무엇을 할 수 있는가*(권한)"를 바꾸지 않는다.**
4. **지각·점검은 "홀드(포커스)" 한 입력으로 모인다.** PS:PO Focus(LMB 약 0.75 s, 공식 변경기록 1.25→0.75 s), Ambulance Life `Focus (Hold)`(시야 안 부위에 레티클이 붙으며 `New Evidence` 칩 생성), Ignite 열화상(LMB 홀드 동안 열 신호), ACE3 인터랙션(Win 홀드→호버→해제). 모두 *시야·거리 안*의 대상에서만 정보가 나온다.
5. **손 조작의 깊이는 Ignite만 갖는다.** 물리 호스(소방차↔소화전 급수 호스·공격 호스가 바닥에 보임), 노즐 RMB 스트림↔스프레이, 소화전 캡 위 링 게이지, 도구 한 개 독점(호스를 들면 할리건이 자동 낙하), E 탭=장비/E 홀드=내려놓기·게이지 행동. 그 대가로 *탭/홀드 혼재*가 사용자 혼란의 1순위였고 퍼블리셔 직원도 "its fairly complicated to put so many options like this simulation has on buttons that make sense for a player"라고 인정했다 [공식 Steam 토론 2025-09-09].
6. **CPR은 4종이 4가지 방식이고 어느 것도 "압박 깊이" 입력을 두지 않는다.** Ambulance Life = 도구 선택→경고창→"Your partner will take over CPR… Focus on driving!"로 *이송 모드 전환*(기회비용, 입력 없음); PS:PO = 하단 액션 라인에 박자가 흐르고 정해진 창 안에서 버튼(`Perform CPR — Progress` 바); ACE3 core = 15 s 고정 진행바 × 라운드당 확률(기본 40 %, 심정지→의식불명 전이일 뿐 깨어나는 게 아님); KAT = 연속 수행, LMB로 중지, 비훈련자 10–20 %/15 s. AED는 ACE3 core에 없고 KAT만 있으며 *기기 음성*이 유일한 지시다(패드 6 s → 분석 5–8 s → "Shock Advised" → 20 s 내 충격). Ambulance Life 제세동은 *수동 패들*이라 실제 AED와 다르다.
7. **평가는 "결과 + 근거 줄"이 4종의 수렴점이다.** Ignite 종료 카드 3체크(`Mission successfully completed` / `No casualties` / `Completed in 31:30`) + `STATS`(`MISSION TIME`·`VICTIMS RESCUED`·`WATER USED`) [관찰 OqOfD9CK3iU t=07:36]; Ambulance Life는 "Scoring is based entirely on VITALS"(퇴원 시 바이탈 개선) [공식]; PS:PO는 SP/CP와 `Justified:` 줄 [관찰]; ACE3는 점수 없이 `ACTIVITY LOG`/`QUICK VIEW`/triage card 감사 기록만 남겨 다음 처치자가 읽는다 [공식 소스]. 반대로 **규칙표 기반 평가는 취약**하다: PS:PO는 CP 오작동 수정이 50여 건이고 2025-11-05 16개 규칙을 되돌리며 "Losing CP isn't fun"이라고 썼다 [공식]; Ambulance Life는 어떤 트리아지가 맞았는지 알려 주지 않는다는 비판 끝에 1.4에서 점수 툴팁을 추가했다 [공식·2차].
8. **신체 위험은 수치 바 없이 연출**로 간다. ACE3: 통증 `White Flashing`·저혈량 `Color Fading`·심박 효과음·서서히 암전, HP 바 없음 [공식 소스]. Ignite: "If you don't wear [the mask] in fire and smoke situations you will get hurt. Because of gameplay reasons the air tanks aren't limited" [공식 개발자 답변 2025-09-05], 체력은 몇 초 뒤 회복 [2차].
9. **역무원이 가져오면 안 되는 것**: 열화상·SCBA·할리건·쇠톱·유압구조(소방), 약물·IV·삽관·봉합·PAK·4색 트리아지 전체(구급), 수색·체포·수갑·무기·견인(경찰). 가져와도 되는 것: 소화기·옥내소화전 호스 *문법*, 연기 노출 연출, 문 개방 전 단서 확인, 의식·호흡 확인, 119 보고, AED(기기 음성 안내 4단계)와 CPR(일반인 수준), 군중 안내·통제(경찰 PS:PO의 `Give Traffic Orders` 문법), 인수인계 기록.
10. **조사 중 바로잡은 사실**: (a) PS:PO 정식 출시는 *2022-11-10*(Steam API 확인), Early Access는 2021-06-17 — 과제 컨텍스트의 "2021-01/2022-08"은 오류. (b) Ambulance Life는 2025-02-06 출시(Aesir/Nacon). (c) Ignite는 출시 완료(2025-09-09, weltenbauer/astragon), 조사 시점 최신 = Update #9(2026-09-17)/Hotfix #9(2026-10-01). (d) ACE3 core에는 AED가 *없다*(KAT/KAM에만 있고, 과제 컨텍스트의 `Advanced-Combat-Medicine/KAT-Medical` URL은 404).

---

## 1. Firefighting Simulator — The Squad (Chronos, 2020-11-17) / Ignite (weltenbauer, 2025-09-09)

> 범위: Ignite는 2025-09-09 출시 확인(Steam 스토어 [공식]). 조사 시점 2026-10-04 기준 최신 공식 업데이트 = Update #9(2026-09-17) / Hotfix #9(2026-10-01). 아래 핵심 관찰은 Ignite(PC)이고, The Squad는 비교용(마지막 콘텐츠 업데이트 Mission Update #5=2022-11-29, 마지막 기능 업데이트 Crossplay Update=2025-05-06 [공식 Steam 뉴스]).

### FS-1 출처 표

| # | URL | 유형 | 날짜·버전 | 검증한 내용 |
|---|---|---|---|---|
| F1 | https://store.steampowered.com/app/1669480/Firefighting_Simulator_Ignite/ | [공식] | 2025-09-09 출시, 조사시 가격 할인 중 | 출시 사실, 4인 협동/NPC 크루, 기능 목록(허브 소방서, 훈련장, 미션 에디터), "ventilating … cooling … sawing … selecting the proper extinguishing agent", "thick smoke finding its way through closed doors" |
| F2 | https://www.firefighting-simulator.com/en/changelog.php (로컬 사본 media/C/ignite-changelog.txt) | [공식] | Hotfix #1(2025-09-16) ~ Hotfix #9(2026-10-01); Update #1=2025-10-16 … Update #9=2026-09-17 | Glossary·First Encounter 텍스트(#9), `Simplified inputs` 설정(#6), "Training lot reset interaction is now a long press action"·"New interaction UI for aiming (e.g. using an axe on a door, wall, Hydraulic Rescue Tool)"·Command View 아이콘(#8), 차량 급수 소모(#9), 승무원 AI 자율 규칙(#4~#6), 연기 속 자세(#7) |
| F3 | Steam 뉴스 "The Alarm Mode Update" (gid 1815580768260488) | [공식] | 2025-11-06 | Alarm Mode(스테이션에서 임의 간격 출동 알람), 단일 플레이 일시정지, AI 자율 우선순위 3종 |
| F4 | Steam 뉴스 "No One Left Behind – The Rescue Trailer" (gid 1806064758616831) | [공식] | 2025-07-25 | 요구조자 상태(의식/무의식), "the only way to locate victims is by listening closely – shouts or coughing", 에어리얼 플랫폼 정원 3(소방관 1·따라오는 의식 환자 1·업힌 무의식 환자 1) |
| F5 | Steam 토론 "SCBA and heat gun." (개발자 배지 PsychoCow) | [공식 개발자 답변] | 2025-09-05 | "You can put your mask on and off whenever you want to. If you don't wear it in fire and smoke situations you will get hurt. Because of gameplay reasons the air tanks aren't limited." "In command view you can see all important objects that got already spotted by you or your team members…" |
| F6 | Steam 토론 "Backdrafts....Help How to open the door/window?" (개발자 PsychoCow) | [공식 개발자 답변] | 2025-09-11 | "Open the door and fast step to the right or left before the hell comes out ;-)" |
| F7 | Steam 토론 "Confusing controls" (퍼블리셔 직원 답변) | [공식 직원 답변] | 2025-09-09 | 사용자: "Some you have to hold down E to fill the bar, other times you just need to press E once …" 직원: "its fairly complicated to put so many options like this simulation has on buttons that make sense for a player." |
| F8 | Steam 토론 "Irons and Air Tanks" | [2차+개발사 로킹] | 2025-04~07 | SCBA 공기 무제한, PASS 경보 없음(요청글), 개발사가 분쟁으로 잠금 |
| F9 | Steam 가이드 ModerNertum "Definitive Guide" (갱신 2026-09-27) | [2차] | Update #9 반영 | 평가/실패 규칙, 난이도 Easy/Medium/Hard 차이, 도구·약제 규칙표, 호스 스트림/스프레이, 취소 가능한 제스처 서술(수압 확보 시 마우스 반시계→시계→반시계) |
| F10 | Steam 가이드 bben "Quick Guide"(전 스테이지 골드), Cynikal "Tips from a Former Firefighter"(★4, 81평), LockedandFiring "Beginners Guide", ModerNertum "solo guide"(AI) | [2차] | 2025-09~2026-05 | 노즐 사용처, 천장 먼저, 유리 파괴, 열화상 H, 명령 1–4/Space, 지시 휠 Q |
| F11 | Spottis "Firefighting Simulator: Ignite – Controls" | [2차, 저신뢰: 내용에 자기모순·AI작성 흔적] | 2026-08 | PC 기본키 후보표. 열화상 키가 T로 표기돼 F10·영상(H)과 충돌 → 열화상은 H 채택, 나머지는 교차검증 가능한 것만 사용 |
| F12 | YouTube OqOfD9CK3iU "Into the Flames – Mission Walkthrough Trailer"(astragon 공식) | [공식 영상] | 2025-08-21 | 노즐 스트림/미스트 프레임, 훈련 목표 바, 미션 종료 평가 카드 |
| F13 | YouTube QHg_V0neE3Q "Firefighting Simulator – Ignite Gameplay Walkthrough FULL GAME (4K) No Commentary"(TheSixGame) 0:00~9:00 | [관찰 원본 영상(2차 업로더)] | 2025-09 | 튜토리얼 미션 프롬프트 원문, 호스·소화전 연결 UI |
| F14 | YouTube K7OxcFthrKk "ULTIMATE … Beginner Guide"(Born 2 Game), 자막 + 9:58–10:14 | [2차 영상] | 2025-09 | 열화상 H·LMB 홀드 확인, 호스 소리로 명중 판별, 홀드 E로 도구 내려놓기 |
| F15 | YouTube B2KE-ov2BEY(공식 Co-op 트레일러), 9TbYOuMVRZE(공식 MVA 트레일러) | [공식 영상] | 2025-06 / 2026-07 | 방독면 마스크 시점, 바닥에 깔린 호스, 아군 이름표, 호스 직사 |
| S1 | https://store.steampowered.com/app/420560/ | [공식] | 2020-11-17 | "intuitive command UI … assigning tasks to your AI colleagues", 40곳 출동, 랜덤 |
| S2 | Steam 뉴스 "Mission Update #1 – Available now!" | [공식] | 2022-08-16 | 도전 설정 4종(지표 숨김 등), 랜덤 시드 공개, 출동 주행 건너뛰기(Skip drives) |
| S3 | YouTube 4c9bwl6v0P0 "Firefighting Simulator – The Squad – Livestream with developer"(astragon) 자막 | [공식 영상] | 2020-11-17 | 산소·연료 기반 화재 확산, 백드래프트는 튜토리얼에서 가르침, 랜덤 미션(주사위 아이콘) |
| S4 | gamepretty "Controls (Key Bindings)"(원문 크레딧 PsychoCow=Chronos 개발자 계정) | [2차, 원저자는 개발자 계정] | 2020-11 | 기본 키/패드 |
| S5 | gamepretty "How to Control the AI Properly"(ERO/CaptainGeo) | [2차] | 2020-11 | 명령 문법 전체 |
| S6 | YouTube zM6On8oXhYU(Establishing Hoses 튜토리얼), 9z0Y60i4UZE(AI Squad Commands 튜토리얼) | [관찰 영상(2차 업로더)] | 2020 | 호스 설치 6단계 UI, AI 명령 UI |
| S7 | Steam 토론 "Supply line"(2020-11~2025-04) | [2차] | | AI가 급수를 선행, 끌 수 있는 옵션 |

### FS-2 조작 표 (PC 기본값; 키보드·마우스)

| 동사 | Ignite | The Squad | 근거 |
|---|---|---|---|
| 이동/달리기 | WASD / Left Shift. 달리기는 멈추면 풀려 다시 눌러야 한다는 불만 | WASD / Left Shift | [2차 F7·F11], [2차 S4(개발자 계정 원문)] |
| 웅크리기 | Left Ctrl (연기 속 이동 수단으로 권장) | Left Ctrl | [2차 F9·F11], [2차 S4(개발자 계정 원문)] |
| 상호작용 | E. 탭=즉시(예: 도구 장비), 홀드=게이지 채우기(문·밸브류) | E | [관찰 F13 t=03:15], [공식 F7] |
| 도구 사용 | LMB | LMB | [관찰 F13 t=04:00], [2차 S4(개발자 계정 원문)] |
| 도구 보조 기능 | RMB: 노즐 "switch between spray patterns"(스트림↔스프레이) | — (노즐 단일: 속이 빈 제트 노즐뿐) | [관찰 F13 t=04:00], [2차 F9], [2차 gameplay.tips "only … the hollow jet pipe"] |
| 도구 내려놓기 | E 홀드 (튜토리얼 문구), G (2차표) | 기본키 목록(S4)에 별도 항목 없음 — 확인 못 함 | [관찰 F13 t=03:15 "Tools can also be dropped manually by holding [E]"], [2차 F11 G] |
| 핑(위치 표시) | 마우스 가운데 버튼 | — | [2차 F11] |
| 지휘 메뉴 | Q(홀드/휠) — 무전·지시 | Space 홀드=명령 휠, 휠 스크롤로 다음/이전 AI, LMB 선택 | [2차 F10 solo], [2차 S4(개발자 계정 원문)] |
| 개별 AI 지시 | 대상 조준 + 1·2·3(팀원)·4(아무나) | 대상(손잡이 마커) 조준 + 1·2·3, 숫자 3초 홀드=따라오기 토글 | [2차 F9·F10], [2차 S5] |
| 지휘 시점 | Tab "Command View" | Tab 퀵 명령 메뉴 | [2차 F11], [공식 F5] |
| 열화상 | H, LMB 홀드로 열 신호 표시, 호스를 든 채 사용 가능 | — | [2차 F10·F14] (F11은 T → 충돌) |
| 마스크(SCBA) 착탈 | 착탈 가능(키 미확인; F11은 B) | — | [공식 F5 기능], 키 [2차 F11 저신뢰] |
| 지도 | M | M(메뉴) | [2차 F10 LockedandFiring], [2차 S4(개발자 계정 원문)] |
| 소화전/호스 | 아래 §3 | 아래 §3 | |

### FS-3 조작 문법

**프리미티브** (Ignite)
1. *탭 E = 줍기/장비*, *홀드 E = 게이지형 행동 또는 내려놓기*. 한 손 도구 독점: 호스를 들면 할리건이 자동으로 떨어진다("which will automatically drop your Halligan") [관찰 F13 t=03:15]. 호스와 도구는 동시 소지 불가 [2차 F10 댓글].
2. *조준형 상호작용*: 대상 외곽선(소화전 = 분홍 외곽선+중앙 아이콘), 대상에 달린 프롬프트 "Attach the supply hose to a hydrant by pressing [마우스 아이콘]" [관찰 F13 t=08:00]. 소화전 캡 위에 점선 원형 게이지가 뜨며 장갑 낀 손이 손잡이를 돌림 [관찰 F13 t=08:15].
3. *마우스 제스처*: 급수 개방 = "Swing your mouse counter clockwise, clockwise, then counter clockwise" [2차 F9·F10 두 가이드(같은 저자)]. 영상에서 제스처 자체는 미관측(게이지 링만 확인). `Simplified inputs` 설정이 있고 켜면 상호작용에서 "tap" 표시가 "hold"로 표시되도록 고쳤다는 수정 기록 → 제스처/탭을 홀드로 단순화하는 접근성 옵션 [공식 F2 Update #6].
4. *호스는 물리 객체*: 소방차↔소화전 급수 호스(노랑)·공격 호스(빨강)가 바닥에 깔려 보임 [관찰 F13 t=08:45, F15 coop t≈00:05 소화전에서 이어지는 호스]. 호스 길이 제한·꼬임은 어떤 공식·2차 자료에서도 확인되지 않음(Steam 토론에서 "set hose length … extend hose"가 *요청*으로만 등장 → 미구현으로 보임 [2차 F8, 추론]).
5. *소화약제 규칙*: 물=일반화재(스트림·스프레이), 포=인화성 액체(스트림만), 소화기=기름(스프레이만, 유한), 전기·가스는 "suppress but not eliminate" — 전원 차단/밸브 잠금이 필요 [2차 F9 ×3 가이드 일치].
6. *방향 자세*: 천장부터 쓸고 바닥으로(플래시오버 방지) [2차 F9·F10]; 벽 달아오름(glow)은 재발화 징후 [2차 F10]; 소방호스 소리로 실제 명중 판별("sounds completely different") [2차 F14].
7. *수압/팬 각도 조절*: 마우스 휠로 조절해 달라는 요청글(2025-07-21)만 있고 응답이 없다 → 없는 것으로 보임 [2차, 토론 "Nozzle control ?", 추론].
8. *조준 UI*: 도끼·유압 구조기구용 새 조준형 상호작용 UI(Update #8, 2026-07-21) [공식 F2].

**프리미티브** (The Squad): E = 줍기/연결, 호스 설치는 6단계 물리 절차 — 캡 제거("Remove Cap — Connect Supply Line here") → 커플러 집기 → 소화전 연결 → 공격 호스 연결 → 하부 격실에서 커플러를 노즐로 교체 → 소화 [관찰 S6 zM6On8oXhYU t=00:36~02:00]. 노즐은 *물 흐름을 열고 닫는* 도구로 소개 [관찰 t=01:48 자막].

**연속 입력→상태**
- 노즐 에임 = 카메라. 스트림은 사거리·창문 파괴 가능, 스프레이는 면적↑·사거리↓·진화 느림 [2차 F9·F10, 영상 자막 F14 um4XGKErdh4 "jet … smash the window with the water pressure"].
- 소화는 지속 조준(물량 누적) 방식. 불꽃 스프라이트가 핫스팟 소거 후 몇 초 더 보이는 지연이 있음 [2차 F10].
- 열화상은 LMB를 *누르고 있는 동안* 열 표시 [2차 F14 t=10:05].

**상태 피드백**
- 좌상단: 임무 라벨(`ENTER THE HOUSE`/`EXTINGUISH FIRE`)+하위 목표 한 줄+불 아이콘 진행 바 [관찰 F12 t=04:10, F13 t=04:00]. 우상단: 아군 3명 상태 바(파랑·노랑·초록) [관찰 F12 t=02:20~04:30].
- 우하단: `Task completed — Task "Equip Halligan bar" has been completed`(초록 체크), `Glossary entry unlocked — Halligan` [관찰 F13 t=02:00, t=08:00 `Supplying Water`].
- 연기: 실내 시야가 갈색 헤이즈로 떨어짐, 열화상 시 파랑 팔레트에 빨강/초록 열점 [관찰 F14 영상 9:58–10:14 (media/C/ignite-thermal.webm)].
- 마스크 시점: 1인칭 SCBA 마스크 둥근 비네트 [관찰 F15 MVA t=00:35, coop t=00:21].
- 소화 표적에 *소리*(불꽃 타는 소리/기름 소리)와 지휘 보이스("Fire Extinguished" 등) [공식 F2 Update #6(2026-03-17): "Squad AI will no longer play 'Fire Extinguished' voice lines if no valid attack location can be found … to reduce spam and wrong feedback"].

### FS-4 안내·교육 모델

| 계층 | Ignite | The Squad | 근거 |
|---|---|---|---|
| 튜토리얼 | 첫 임무(가이드)=해야 할 일 목록+상황별 교육 패널(`Equip Hose`, `Extinguish the fire`, `Laying the Attack line` …), 귀환 후 훈련관 드릴, 소방서 뒤 훈련장(화재 종류·도구 커스텀 시나리오) | 6단계 "Establishing Hoses" 등 훈련 임무. 자막식 내레이션 + 좌측 키 범례 + 목표 라벨 | [관찰 F13/F12/S6], [2차 F10], [2차 gamepretty] |
| 사전 학습 | `Glossary` 항목이 첫 마주침에 해금("Glossary entry unlocked — Supplying Water"), `First Encounter` 텍스트 | 튜토리얼에서 백드래프트를 가르침 | [관찰 F13 t=08:00], [공식 F2], [공식 S3 t≈00:12:41] |
| 임무 표지 | 목표 라벨·미니맵 마커·차량 경로 | 위치 마커, 요구조자 마커 | [관찰], [공식 S2] |
| 지휘 시점 | Command View: *이미 발견한(본인·팀원) 객체만* 표시 — 사다리 설치점, 퓨즈 박스, 가스 밸브, 창, 문, 도구, 요구조자 | 팀 명령 휠 | [공식 F5] |
| 난이도/보조 | Easy/Medium/Hard(불 번짐·재발화 속도, 실패 규칙, 자가 리스폰 가능 여부), AI 자율 우선순위 3종, 단일 플레이 일시정지, `Simplified inputs` | 도전 설정 4종: `Hide Objective Indicators`("you will only see the indicators when being in close interaction range"), `Hide Victim Indicators`("their locations will be hidden … You also won't see the number of victims to be rescued anymore"), `Establish Supply Line by Yourself`, `Refill Empty Extinguisher`(3분 재생) | [2차 F9], [공식 F3·F2], [공식 S2 2022-08-16] |
| 반복 플레이 | 사이드 미션 랜덤("side missions appear randomly with each containing randomized challenges"), 알람 모드 | 랜덤 미션(주사위 아이콘): 구조 인원·할 일 랜덤, 시드 공개·공유 | [2차 F14 B2G UI 낭독], [공식 F3], [공식 S2·S3] |

**진단형 vs 지시형**: 임무 중 지시는 *목표 한 줄*뿐이고 어떤 도구·약제·순서를 쓸지는 플레이어가 판단한다. 지시가 가장 많은 구간은 오히려 튜토리얼(목록형 `Task completed` 토스트·자막 내레이션)이다 [관찰 F13, S6]. 임무에서의 "무엇이 문제인가"는 *환경 단서*로 준다: 문 아래로 새는 연기·쿵쿵거리는 소리(백드래프트), 바닥 균열(붕괴 — 밟으면 배차 무전 경고), 불꽃 색(인화성/기름), 소리(기름 타는 소리), 외침·기침 소리(요구조자) [2차 F10 가이드, 공식 F4].

### FS-5 실패·결과 모델

- *플레이어 위험*: 연기·화염에서 마스크를 벗으면 피해; 공기통은 *무제한* — "Because of gameplay reasons the air tanks aren't limited" [공식 F5]. 체력은 몇 초 뒤 회복 [2차 F9]. 쓰러짐은 `incapacitated`→`unconscious`; 쉬움은 스스로 집결지 리셋, 보통/어려움은 동료가 구급차까지 옮겨야 하며 "timer before you are considered dead" [2차 F9]. 
- *즉사형 지형·사건*: 백드래프트(문을 연 사람 즉시 무력화, 옆으로 비켜야 함 [공식 F6]), 연료 탱크 폭발, 한 층 넘는 붕괴 낙하 [2차 F9].
- *임무 실패*(쉬움은 대부분 비활성): 접근 바 소진(직접 운전 선택 시), 화재 바 가득 참(escalated), 요구조자 과다 사망(보통=절반 이상, 어려움=1명), 팀 전멸 [2차 F9·F10 두 가이드 일치].
- *지연 결과*: 산소(환기)로 재점화·번짐 — 창을 깨고 문을 열면 불이 다시 커짐; 창 파괴 시 백드래프트는 해소 [2차 F9·F10·F14, 공식 S3 설명 "oxygen and fuel"].
- *요구조자*: 인계 전까지 구조로 치지 않음, 불붙은 차량 속 요구조자는 불을 끄기 전엔 구조 불가 [2차 F9]. Squad 튜토리얼은 훈련에서 죽지 않도록 수정 [공식 S2 패치노트: "it was actually never intended to die in these trainings"].

### FS-6 평가 모델

- *Ignite*: 임무 종료 카드 — `THE HEART OF THE CITY COMPLETED` / "You have completed all required tasks and completed the mission." / `SILVER RATING` / ✔`Mission successfully completed` ✔`No casualties` ✘`Completed in 31:30`(시간 임계 미달) / `STATS`: `MISSION TIME 57:54`, `APPROACH TIME 0:00`, `VICTIMS RESCUED 12`, `WATER USED 9,111 gal` [관찰 F12 t=07:36]. Gold=전 조건, Silver=1개 미달, Bronze=2개 미달; 어려움에서는 사상자 조건이 실패 조건이라 동메달 불가 [2차 F9·F10]. 통계의 `WATER USED`는 소화 효율 지표(자원 소모)로 노출.
- *The Squad*: 훈련은 시간 기록("Lowest time score gets bragging rights!") [관찰 S6 t=02:00]. 본편 점수 체계는 미확인(공식 패치노트·2차에서 평가 항목 서술 못 찾음).
- 보상은 경력 진행/해금(주요 미션이 사이드 8개 완료로 해금: `The Smoke Show`) [2차 F14 BbZJYkK6-Fc 자막].

### FS-7 관찰 기록

| 영상 | t | 본 것 | 파일 |
|---|---|---|---|
| F12 OqOfD9CK3iU | 03:42 | 훈련장에서 직사(스트림) 하얀 선, 호스(빨강) 연결 노즐, 좌상단 목표 바 | frames-ignite/ (스틸은 `ignite-walkthrough-trailer.webm:222s`) |
| F12 | 04:10 | 넓은 안개형(스프레이)와 객체(드럼통) 화염, 목표 `EXTINGUISH …` 진행 바 | frames-ignite/f250.png |
| F12 | 07:36 | 임무 종료 평가 카드(위 §6) | frames-ignite/trailer-t456*.png |
| F13 QHg_V0neE3Q | 03:15 | `Equip Hose` 패널(탭 E 장비 / 홀드 E 내려놓기) | frames-ignite/six-t195.png, c195.png |
| F13 | 04:00 | `Extinguish the fire` 패널: LMB Use / RMB spray patterns, 목표 `ENTER THE HOUSE / Extinguish fire inside` | six-t240.png, six-t240-prompt.png |
| F13 | 07:45~08:15 | `ESTABLISH WATER SUPPLY`, 소화전 분홍 외곽선+게이지 링+프롬프트, `Glossary entry unlocked — Supplying Water` | six-t465/480/495.png, six-t480-prompt.png |
| F13 | 08:18~08:50 | `Laying the Attack line`, `Task … has been completed`, `Find a water pistol and attach it to the end of the attack hose by pressing [마우스]` | six-t498…t525.png, c525.png |
| F14 K7OxcFthrKk | 9:58–10:14(발췌) | 연기로 갈색 헤이즈 → 열화상(파랑 팔레트, 열점) 전환, 호스 든 채 | ignite-thermal.webm, frames-ignite/thermal-sheet.png |
| F15 coop B2KE-ov2BEY | 00:21 | 팀원 이름표(Mia), 호스를 든 소방관, 불길이 보이는 계단 | ignite-coop-trailer.webm:21s |
| F15 MVA 9TbYOuMVRZE | 00:30 | 직사 호스, 차량 화재, 바닥에 깔린 호스 | ignite-mva-trailer.webm:30s |
| S6 zM6On8oXhYU | 00:36 | `REMOVE THE INDICATED CAP`, 하단 자막 내레이션, 좌측 키 범례(`E Interact`, `Use Tool`, `Sprint`, `Crouch`, `Toggle Camera Zoom`, `Flashlight`, `Open Command Wheel`, `Queue Commands`, `Global AI Shortcut`, `Attack Fire Command + AI Shortcut`) | frames-squad/hoses-6.png, legend-t36.png |
| S6 | 02:00 | `CONNECT NOZZLE TO ATTACK HOSE / QUENCH FIRE`, "…we're timing you! Lowest time score gets bragging rights!" | frames-squad/hoses-t120.png |
| S6 9z0Y60i4UZE | 00:16~01:36 | 우상단 AI 슬롯 표시 `1`, 웨이포인트 녹색 링, `Command finished` | frames-squad/ai-6.png |

### FS-8 CHOOGuard 적용

| 판정 | 기제 | CHOOGuard 동사 | 구체 방법(입력·피드백·실패) | 근거 |
|---|---|---|---|---|
| Adopt | 탭 E=장비/줍기, 홀드 E=게이지 행동·내려놓기를 *한 문법으로 통일하고 프롬프트에서 시각적으로 구분* | 소화기·옥내소화전·밸브·셔터·문 전반 | 프롬프트 아이콘이 탭(원)/홀드(채워지는 링)로 다르게 보이게. 유저 혼란 보고("Some you have to hold down E to fill the bar, other times … press E once")를 반면교사 삼아 *한 화면에서 두 종류를 섞지 않는다* | [관찰 F13 t=03:15], [공식 F7] |
| Adopt | 대상에 붙는 *공간 고정형 프롬프트*(외곽선+중앙 아이콘+링 게이지)가 *대상의 동사*를 말하되(예: `Open compartment`, `Attach the supply hose to a hydrant`), *지금 해야 할 정답*이라서가 아니라 *물리적으로 가능해서* 뜬다. 임무 목표는 별도 한 줄(`Extinguish fire inside`) | 모든 상호작용 | 현재 `IFpsInteraction.CanInteract/TryInteract`는 "정답일 때만 `CanInteract`=true"로 쓰임 → "물리적으로 가능하면 항상 true, 맞고 그름은 결과로" 로 의미 전환. 프롬프트는 *대상이 무엇인지*(밸브, 차단기, 셔터)와 *동사 아이콘*만, `가스 중간밸브 잠그기`처럼 정답을 이름 붙인 문구는 제거. 인터랙션 계약에 `InteractionStyle(Tap/Hold/Gesture)`·진행도 노출 | [관찰 F13 t=08:00~08:15, t=08:18(`Open compartment (water gun)`)] |
| Adopt | 대상 발견은 *플레이어가 본 것만* 지휘 시점/지도에 누적 | 위험 발견·보고·지도·Tab 상황판 | Tab 상황판을 "사전 체크리스트"에서 "내가 본 것·들은 것 기록"으로 바꾸고 미지각 위험 비노출. Ignite Command View가 "already spotted" 객체만 표시하는 것과 동일 원리 | [공식 F5] |
| Adopt | 소화약제·급수 규칙을 *환경 단서로 학습*(소리·불꽃 색·연기) | 소화기·옥내소화전 | 이미 구현된 약제 규칙(물/분말/K급)에 *식별 단서*(불꽃색·기름 소리·감전 스파크)를 입히고 정답 토스트 대신 *잘못 쓴 결과*를 보여줌(번짐·감전) | [2차 F9 약제표, F14 "sound"] |
| Adopt | 호스 사용 피드백: 소리·물줄기가 실제 명중했는지 오디오로 판별 | 소화기·호스 | 명중=진화 소리/증기, 빗나감=다른 소리. 숫자 게이지 없이 감각 피드백 | [2차 F14] |
| Adapt | 호스 *물리 객체+소화전 개방 제스처* | 옥내소화전(역무원 실무) | 호스 함 열기(홀드 E) → 호스 끌어내기(30 m 줄) → 밸브 개방(마우스 원 그리기 *또는* `Simplified inputs`=홀드) → 노즐 LMB. 소방차 급수·커플러 6단계는 *소방관 전용*이므로 제외(Squad 튜토리얼 6단계는 참고만) | [관찰 S6], [2차 F9] |
| Adapt | 연기 노출·마스크 | 플레이어 위험(연기/열) | SCBA·열화상은 소방 장비라 *제외*. 대신 낮은 자세(웅크리기 신설) 시 연기층 아래 시야/노출 감소, 노출 누적은 기침·시야 헤이즈·어지러움 연출 후 *행동 불능(자력 대피 가능)*. 공기통 무제한 선례처럼 "수치 관리 부담 없는" 단순 모델 | [공식 F5], [2차 F9 "navigate through smoke by crouching"] |
| Adapt | 문 열기 전 *단서→선택→결과* | 문·셔터·방화문 개방, 위험 발견 | 문 틈 연기/소리/열기(손등 확인 모션 홀드 E)를 *지각 단서*로. 단서를 무시하고 열면 즉시 피해(비켜서기 입력으로 회피 가능). *종류 고정 금지*: "닫힌 문 뒤 위험 지각 단서"라는 공통 데이터 필드로 어떤 Hazard에도 부착 | [공식 F6], [2차 F9] |
| Adapt | 무전/명령 휠의 맥락 지시 | Q 무전 휠 | 현재 "맞는 다음 단계만" 보이는 휠을 *조준 대상 맥락 휠*(대상을 보며 Q → 가능한 모든 보고·요청 문장 노출, 오답 포함)으로. 오답은 *결과가 생김*(허위 신고→현장 혼란, 불필요 출동) | [공식 F5 Command View], [2차 S5 맥락 명령] |
| Adapt | 요구조자 "따라오기 / 있기" 상태 | 승객 대피 안내·진정 | 승객은 *의식 있음* 상태에서 안내 신호(손짓/외침 홀드)에 따라 따라오기·정지를 전환. 무의식 요구조자 어깨 운반은 *제외*(구급 전문·무리한 이동), 단 연기·화염 즉시 위협 시 *끌어 옮기기*는 일반인 행동요령 수준으로 가능 [추론] | [공식 F4], [2차 F10] |
| Adapt | 안내 강도 *설정 노브* | 길 안내·지표·보고 | Squad `Hide Objective Indicators`(근접 시에만 표시)/`Hide Victim Indicators`(인원 수 숨김)를 차용: 기본=근접 시에만 마커, 숨김=지도 경로·바닥 점선 끄기. *기본값을 현행(34 m 경로 켜짐)에서 한 단계 낮춤* 권장 | [공식 S2] |
| Adapt | 종료 카드 *체크 3줄+통계* | 근무 종료 평가 | `Mission successfully completed`/`No casualties`/`Completed in …` 3체크 구조 + 통계(근무 시간·구한 인원·물 사용량 대신 소화기 약제 사용률 등). 시간 조건은 사건 종류가 가변이므로 *임계 고정 금지*(사건 난이도에서 도출) | [관찰 F12 t=07:36] |
| Adapt | 사이드 이벤트 알람 | 근무 중 발생 간격 | Alarm Mode처럼 근무 내 임의 간격 호출. 단 JEV 합성 규칙 유지(종류·시각 가정 금지) | [공식 F3] |
| Adopt | 웅크리기(Ctrl)로 연기층 아래 이동 + AI/군중이 연기 속에서 스스로 몸을 낮춤 | 플레이어 위험, 승객 대피 안내 | 현행 입력에 없는 웅크리기를 신설: 연기층 높이 아래에서 시야·노출이 개선. 연기 속 승객도 자동으로 낮은 자세 → 플레이어가 *행동으로 상황을 읽음*(군중 자세=연기 단서). 단 건물 밖에서는 낮추지 않음(Ignite Update #7 동일) | [2차 F9], [공식 F2 Update #7(2026-05-12)] |
| Adopt | 재발화 원천(전기·가스)은 "suppress but not eliminate" — 불은 줄지만 원천을 끊을 때까지 되살아남 | 가스 밸브·기기 차단, 분전반·차단기 | 이미 구현된 라이브 전원·가스 공급 규칙과 일치. 추가: *원천 미차단 상태의 소화 진행 바는 일정 수준에서 정체*하고 다시 오르게 하여 플레이어가 "원인이 따로 있다"를 *스스로 추론*하게 함(문구 안내 금지) | [2차 F9·F10 일치], 현행 `FireHazard.Feed` |
| Adapt | 문·셔터 같은 개폐 오브젝트의 *플레이어 의도 vs 산소/확산* | 셔터·문·에스컬레이터 | Ignite의 "창/문을 열면 화재가 커진다" 모델을 *보편 개폐 규칙*(열린 개구부 = 연기·열 확산 경로)으로 일반화: 어떤 Hazard 종류든 개구부 상태를 입력으로 받음. 별도 종류 분기 없음 | [2차 F9·F10·F14], [공식 S3] |
| Reject | 열화상·SCBA·할리건·쇠톱·유압 구조·진입 파괴 | — | 소방관 전용 절차·장비. 문 파괴·지붕 환기 등 역무원 권한 밖 | 제약조건 |
| Reject | 팀 AI 6단계 명령 UI(소방 분대 지휘) | — | 역무원은 분대 지휘자가 아니다. 대신 무전으로 동료/기관에 *요청*(수락·지연 결과가 있는) | [2차 S5] |
| Reject | 임무당 시간 기반 메달, 하드 페일(요구조자 사망=즉시 실패) | 평가 | CHOOGuard는 사건 가변이므로 시간 임계를 고정할 수 없음; 실패 연출은 *결과 서술*로(§5 교차 결론 X8·X10 참조) | [2차 F9] |
| Reject | 호스를 든 채 도구 소지 불가 같은 *과도한 장비 인벤토리 규칙* | — | 역무원은 소화기 1개 + 무전기 정도가 현실. 도구 독점(한 손 1개)만 유지하고 상세 인벤토리 관리는 제외 | [관찰 F13 t=03:15] |

### FS-9 미확인·한계

1. *소화전 개방 제스처*("counter clockwise, clockwise, then counter clockwise")는 같은 저자(ModerNertum)의 두 가이드에만 있고 영상으로 확인하지 못했다. 관찰한 것은 `Attach the supply hose to a hydrant by pressing [마우스]` 프롬프트와 소화전 캡 위의 점선 원형 링뿐이다(F13 t=08:00~08:15). 마우스 아이콘은 왼쪽 버튼이 하이라이트된 것으로 읽혔으나 480p 해상도 한계로 확정하지 않는다.
2. *SCBA 착탈 키*, *PC 기본키 일부*(핑 MMB, 지휘 Q/Tab, 드롭 G, 카메라 V)는 Spottis(저신뢰·AI 작성 흔적, 열화상 키가 T로 표기돼 다른 두 출처의 H와 충돌)에만 있다. 표에서는 교차검증된 항목만 확정으로 쓰고 나머지는 비고 처리했다. Spottis의 "Squad Command Controls" 표는 콘솔 열이 같은 버튼을 여러 동작에 중복 배정하고 있어 신뢰하지 않았고, PC 열은 다른 가이드와 일치한 숫자키 1–4만 채택했다(`Enter/Backspace/Delete` 수락·취소·거절 등은 미검증).
   - 미해결 충돌: Cynikal 가이드는 "Hold Spacebar while looking at a truck. Press 1–4 to order a teammate"(차량 조준 + Space 홀드 + 1–4)라 쓰고, ModerNertum 솔로 가이드는 "command wheel (Q by default)", 대상 조준 후 1·2·3·4로 맥락 지시라 쓴다. Spottis는 Space=Traversal. 지휘 입력의 정확한 기본 조합은 미확정 → 표의 "Q/Tab/숫자 1–4"는 2차 합의치로만 취급.
3. *체력·쓰러짐·타이머 수치*(회복 속도, 사망 타이머 길이, 난이도별 차이)는 2차(Steam 가이드)뿐이다. 개발자 발언으로 확인된 것은 "마스크 안 쓰면 다침", "공기통 무제한" 두 가지.
4. *Command View* 화면 프레임은 확보하지 못했고, 동작은 개발자 서술(F5)과 변경 기록의 "Command View icons"(F2)로만 확인했다.
5. *호스 길이 제한·꼬임*: 공식·2차 어디서도 발견하지 못했다(요청글만). "없다"의 확정이 아니라 "확인 못 함".
6. *Ignite 임무별 시간 임계*(예: `Completed in 31:30`)의 결정 방식, 평가 점수 외 보상 곡선은 미확인. 접근(approach) 단계가 선택 사항이라 종료 카드의 `APPROACH TIME`은 건너뛰기 시 0:00으로 보인다(F12 t=07:36) [추론].
7. *The Squad 본편 점수·평가*와 산소/SCBA 모델은 공식·2차에서 찾지 못했다. 튜토리얼의 시간 기록만 관찰했다.
8. F13은 제3자 업로더(TheSixGame)의 풀 플레이 4K 영상에서 0:00~9:00만 받은 것으로, 원본 시각 = 파일 시각이다. 영상 해상도 480p라 작은 글자는 4~6배 확대해 판독했다. 저작권 보호를 위해 영상은 `media/C/`(git 무시)에만 두고 인용은 URL·시각으로만 한다.
9. ofzenandcomputing·clashiverse·spot.monster 류 사이트의 Ignite 가이드는 출시 전 미리보기·허위 연도·존재하지 않는 기능(Steam Workshop 등) 서술이 확인되어 *근거에서 제외*했다.

---

## 2. Ambulance Life: A Paramedic Simulator (Aesir Interactive / Nacon, 2025-02-06)

조사일 2026-10-04. Aesir Interactive / Nacon, Steam app 1926520, 출시 2025-02-06. 확인 가능한 최신 공식 패치 = 2025-04-09 "Patch 1.4" (Steam News API 상 이후 패치 노트 없음; 2026-03-03 할인 공지까지 확인).
태그: [공식] Steam 상점·뉴스·개발 블로그·개발사 직원(Troy_Aesir) 포럼 답변 / [2차] 리뷰·유저 글 / [관찰 URL t=] 영상 프레임 직접 확인 / [추론].
영상 프레임 시각 주의: yt-dlp `--download-sections`가 키프레임에 맞춰 시작점이 최대 약 5~10초 앞당겨질 수 있다. 파일명의 `tMMSS`는 "요청 시작점 + 클립 내 초"로 계산한 원본 시각이라 ±10초 오차 가능. 클립 내 초는 정확.

---
### AL-1 출처 표

| URL | 유형 | 날짜·버전 | 검증한 내용 |
|---|---|---|---|
| https://store.steampowered.com/app/1926520/ | 공식 | 2025-02-06 출시, 접속 2026-10-04 | "36 different complaints", "17 medical instruments", "2 game modes: Simulation mode for experienced paramedics or classic mode to guide you through your first shifts", 재난 시 "triage their care according to the severity" |
| https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=1926520 (Steam News 전체 수집) | 공식 | 2023-12 ~ 2026-03 | 아래 패치/블로그 원문 전체. 로컬 사본 `media/C/ambulance-news-json.txt` |
| 패치 "Ambulance Life Patch \| 2025-02-07" (gid 1790848102666975) | 공식 | 2025-02-08 게시 | 트리아지 태그 가이던스 위젯 추가, "Added Penalties if the patient is admitted untreated", "Added rewards and penalty for life saving measures", "Patient protocol doesn't spoil the callout type any more" |
| "Performance Status Update" (gid 1790848102657959) | 공식 | 2025-02-08 | "Change of the Guidance Widget about the Triage Tags", "Addition of a score for the application, or not, of life saving measures" |
| 패치 2025-02-25 "Hotfix 1.2.2" (gid 1792116353359476) | 공식 | 2025-02-26 게시 | Patient Protocol에 미해금 진단 표시, 핸드북 열면 일시정지, 파트너 말수 감소, 대재난 S-score 완화, Game Over 설정 안내 박스 |
| 패치 2025-03-06 "Patch 1.3" (gid 1793384379374262) | 공식 | 2025-03-06 | **Simulation Mode 가이던스 추가 비활성화**(적/녹 평가, 추천 치료 HUD, 체크포인트 재시작), 툴 툴팁, 'Life saving measures' 하이라이트, VFib 피드백, 근거리 환자 자동 발견 |
| 패치 2025-04-09 "Patch 1.4" (gid 1796551539186304) | 공식 | 2025-04-09 | 점수 화면 툴팁 추가, 비선택 환자 "another ambulance will pick them up" 마커, CPR 심전도 표시 처리, 충격(jolt) 부상, 파트너 질책 조건 축소 |
| Developer Blog #3 / #4 (gid 5749481331454531994, 5766372999943207277) | 공식 | 2024-04-02 / 2024-05-02 | 플레이테스트 환자 38% 사망, 서비스 성공 50%; "instant fail options include running pedestrians over..., crashing your ambulance, or you somehow get yourself fatally wounded! ... Don't stand in fire" |
| Steam 토론 "Is this correct?" /discussions/0/601909079151006765/ | 공식(Troy_Aesir 답변) | 2025-07-14 | CPR=치료 단계 종료, 제세동은 수동 유지 |
| Steam 토론 "Selected CPR while inside the ambulance..." /discussions/3/601910081412552429/ | 공식(Troy_Aesir) | 2025-07-20 | CPR 선택 시 경고창, 파트너 인계, "Scoring is based entirely on vitals" |
| Steam 토론 "I dont get how this game scores treatment" /discussions/0/546740620659614073/ | 공식(Troy_Aesir) | 2025-07-14~16 | 점수=바이탈 개선, 돋보기 색 의미, 호흡 문제 우선 |
| Steam 토론 "I don't like the rating system" /discussions/0/601908863574336905/ | 공식(Troy_Aesir) | 2025-07-14 | 동일 + "red magnifying glass ... do NOT affect your Score" |
| Steam 토론 "Triage tutorial" /discussions/0/595140423952923997/, "Multiplie casualties" /discussions/3/727997287883621096/, "Always Bad Triage" /discussions/0/601897727418239155/, "Redo triage" /discussions/0/595140423952901063/ | 공식(Troy_Aesir) | 2025-02 ~ 03 | 4색 태그 정의, 다중 환자 절차 8단계, 태그 기준 비공개("vibes"), 태그 재지정 불가(요청 접수) |
| Steam 토론 "Deactivate Mini-Games..." /discussions/3/601896133034259370/, "I can't complete the bandage mini game" /discussions/0/601900765808735045/ | 공식(Troy_Aesir) + 유저 | 2025-02 ~ 04 | 미니게임 끄기 옵션 이름·위치, 미니게임 의도, 붕대 미니게임 조작 |
| Steam 토론 "NOT FOR THE EUROPEAN MARKET" (개발사 답변) /discussions/0/601892024948378533/ | 공식 + 유저 | 2024-12-28 | "you can certainly intubate conscious patients or administer 10 doses of morphine, but there will be consequences in your performance review"; 치료 "close to 100%" vs 시간 압박 |
| Steam 토론 "had fun playing" /discussions/0/595137109171440818/ (데모) | 공식(Troy_Aesir) | 2025-01-03~05 | 다중 환자 시 1명만 이송, 15분 시프트, 나머지 "implied" 이송 |
| Steam 토론 "Feedback from Actual Providers" /discussions/0/601892123631970940/, "Potential Treatment Issues" /discussions/3/595140510531759105/, "Burn victim Dies..." /discussions/3/601896133034291976/, "KeyBinding PROBLEMs" /discussions/0/601891815092693018/, "Controller Inverted Axis" /discussions/3/595140258825260372/ | 공식(Troy_Aesir)+유저 | 2024-12 ~ 2025-02 | 제작진에 전직 구급대원 2명, 이송 속도 점수 비중, "Faster patient deterioration" 토글, 키 리바인드는 설정 메뉴 |
| https://www.gamingnexus.com/Article/14174/Ambulance-Life-A-Paramedic-Simulator | 2차(리뷰, PS5) | 2025-02-11 | 시프트 15~45분, 대재난 triage 제한시간, 태그 4색, 미니게임 서술, "scorecard ... does not give details on which patients you correctly or incorrectly triaged" |
| https://www.tech-gaming.com/ambulance-life-paramedic-simulator | 2차(리뷰, PC) | 2025-02-22 | 경직된 anamnesis 절차, 태블릿 "interim diagnosis", 2단계 튜토리얼 |
| https://driffle.com/blog/ambulance-life-a-paramedic-simulator-tips-and-tricks | 2차(저품질) | 2025-02-11 | Classic/Simulation 설명만 인용. 메카닉 주장에는 사용 안 함 |
| https://www.magicgameworld.com/ambulance-life-a-paramedic-simulator-pc-keyboard-controls-guide/ | 2차(저품질) | 2024-11-02 | **Cloudflare 403으로 본문 못 읽음.** 키 목록은 같은 채널 영상(아래)의 설정 화면 프레임으로 대체 확인 |
| YouTube c87W_4Y6_r0 "PC Settings & Controls" (Magic Game World) | 영상(2차 채널, 게임 화면은 1차 증거) | 업로드 2024-12-20, 데모 빌드 추정 | 설정 Game/Controls 탭 프레임(키 바인딩·가이던스 토글) |
| YouTube H90F5LHg2AI "Tutorial Mission & Basics Guide" (Magic Game World) | 영상 | 2024-12-20 | 튜토리얼 전체 흐름 |
| YouTube HP8vyDnmipc (Ashu's World, 구급차 심정지 Part 1) / S7rqivMjNZw (Cardiac Arrest Shocked 2 Times) | 영상(PS 컨트롤러 글리프) | 2025-04-21 / 2025-03-20 | 제세동기·CPR 선택 UI |
| YouTube 5G1Ucc8zF6I (Steezy C, EP8) | 영상(PS 글리프) | 2025-05-25 | CPR 확인창, 트리아지 태그, 결과/디브리프 화면 |
| YouTube s_H7wv4PrSs "Collapsed Highway - S Rank" (ReplacementGaming) | 영상(PC 키 글리프) | 2025-02-18 | 대재난 triage HUD·타이머·결과 화면 |
| Aesir 공식 채널 기능 클립: Q2uxDKWQPAk(Bandage 2024-11-13), koFoCs4XDA0(Vascular Access 2024-11-25), OkFByw8Wl9w / SRc3xL8rLqo / CJegwCKNdo8(Anamnesis 1~3, 2024-10-27 / 11-01 / 11-11), julxW0nxMYk(Progression 2024-10-16) | 공식 영상(프리릴리즈 빌드) | 2024-10 ~ 11 | 미니게임·진단 UI·결과 화면 초기형 |

버전 충돌 1건: 2025-02-07 패치 노트는 "Added Triage Tag Guidance Widget"인데, Troy_Aesir 포럼 글(2025-02-16)은 이를 "Update 1.2.0"이라 부른다. 02-07 패치의 정식 버전 번호는 확인 못 함.

---
### AL-2 조작 표 (PC 기본값; verb → exact input)

근거 = 설정 화면 프레임 [관찰 c87W_4Y6_r0 t≈2:16–4:10, 데모 빌드 추정] + 튜토리얼/대재난 HUD 프레임 [관찰 H90F5LHg2AI, s_H7wv4PrSs]. 리바인드 가능(공식, KeyBinding 스레드). 정식 1.4 빌드에서 바뀌었는지는 미확인.

| Verb | 입력 | 증거 |
|---|---|---|
| 이동 / 시야 / 달리기 | W A S D / 마우스 / "Run" 키(튜토리얼 박스 "Movement controls: Move · Look around · Run", 키 글리프 식별 불가) | 설정 "Move Forward W, Backward S, Left A, Right D"; 튜토리얼 t=0:02 |
| 환자와 대화(Anamnesis) | **E 탭** 프롬프트 "Anamnesis [E]"가 환자 위에 뜸 → 전체화면 오버레이 "ANAMNESIS" + 질문 선택 (오렌지 선택지 "Can you tell me what happened?"), 취소 버튼 "Cancel" | 튜토리얼 t=3:16, 3:26, 공식 Anamnesis1 clip 6s |
| 신체 검사(Inspection/Focus) | **마우스 버튼 홀드** HUD 표기 "Focus (Hold)" (설정 "Inspection Mode" = 마우스 글리프; PS5는 L2). 홀드 중 환자 주위에 원형 레티클+몸 부위 점 표시, 조준한 부위의 증거가 우상단 "New Evidence: Seems restless" 등으로 쌓임 | 대재난 HUD t≈2:03 `ambulance-catastrophic-t0203.png`, 튜토리얼 t=3:55. 어느 버튼(좌/우)인지 480p로 식별 불가 → [추론] RMB |
| 태블릿(Patient Protocol) 열기 | **Tab** (HUD "Patient Protocol" 옆 Tab형 글리프, 설정 "Switch Handbook / Patient Protocol") | 교차확인: 위 영상 + 유저 글 "I hit tab and select the diagnosis" [2차] |
| 핸드북 / 지도 | H / M | HUD 글리프 "Open Handbook [H]" "Map [M]"; 설정 "Open Handbook H", "Open Map M" |
| 태블릿 환자 전환·탭 전환 | Q / E (좌우 탭·환자), 보조 1 / 3; 스크롤 = 마우스 휠; 클릭 = LMB; 클릭(보조) = Space; 적용 F; 되돌리기 R; 뒤로 Esc | 설정 UI 섹션 프레임 |
| 일반 상호작용 슬롯 | Interaction 1=F, 2=E, 3=Q, 4=R. 실제 쓰임: 튜토리얼 팝업 "Continue [F]", 구급차 "Toggle Siren [Q]", "Exit Ambulance [R]", 도구가방 "Close Toolbag [R]", 들것 "Leave Stretcher [R]" | 설정 + 튜토리얼 프레임 |
| 도구가방 열기 / 도구 선택 | **T** (손 아이콘 옆 "T"; 설정 "Select Treatment Tool T"). 카테고리는 상하, 도구는 좌우로 이동 (튜토리얼 문구: "Tools are grouped into categories. Navigate up or down to switch between categories - left or right to select different tools within each category") | 튜토리얼 t≈7:05 |
| 도구 장착 → 시술 | 툴팁의 "Equip" → 몸 부위 라벨("Right Thorax" 등) 표시 → 시술 확정(설정: Confirm Procedure = 마우스, Confirm Treatment = F, Cancel Treatment = R, Treatment Positive F / Negative R) ; 치료 카메라 회전 A / D | 설정 Treatment 섹션 |
| 붕대 미니게임 | 점선 나선 경로를 마우스 커서로 따라가고 방향 화살표를 경로 쪽으로 돌린 뒤 체크포인트에서 F(PC; Xbox Y, PS ✕). 완료 표기 "Perfect" → "Continue". 느리면 화살표가 경로를 이탈 | 공식 clip Bandage 6s; 유저 글 "Sometimes I make it to the 1st place I hit F" |
| 정맥로 미니게임 | 슬라이더 위 마커가 구간에 들어올 때 버튼. 문구 "Press ◯ when the needle is in the right spot" → "Start" → "Perfect" → "Continue" | 공식 clip Vascular Access 6~12s |
| CPR | 도구가방 Circulation에서 "CPR (Cardiopulmonary Resuscitation)" 장착 → 확인창 [Confirm/Cancel] **1회 클릭**. 압박 입력 없음 | 3절·7절 참조 |
| 제세동 | 도구가방에서 Defibrillator 장착 → 양손에 패들 2개 → 가슴 위로 옮김 → 충격. **충격 발사 키는 영상에서 식별 못 함** | 7절 |
| 트리아지 태그 | 환자 위 2번째 상호작용 "Triage Package" → 4칸 방사형: Immediate(빨강) / Delayed(노랑) / Minor(초록) / Deceased(회색) (PS5는 □ △ ✕ ◯ 각각). PC 키 미확인 | `ambulance-steezy-triage-t1626-interactmenu.png`, `...t1628-tagselect.png` |
| 운전 | W/S, A/D 조향, 마우스 카메라 회전, Camera Reset, Handbrake(Space형 글리프), Q 사이렌, R 하차 | 튜토리얼 t=2:33 "Driving Controls" 박스 |
| 컷신 스킵 / UI 숨김 / 확대 | X / U / Z | 설정 |

---
### AL-3 조작 문법

- 원시 입력: (a) 탭 E = "대화/상호작용" (프롬프트는 대상 위에 이름 라벨 `Anamnesis [E]` 형태로, 가까울 때만); (b) 마우스 홀드 = "집중 검사"(연속 입력→ 레티클이 환자 신체 부위 점에 스냅, 점 위에 머무르면 증거 칩 생성); (c) 메뉴 조작 = 탭 전환 Q/E + 휠 스크롤 + 클릭; (d) 방사형 선택(트리아지 4색) ; (e) 치료는 장비-선택(도구가방 T) → 부위 선택 → 시술 확정의 3단 파이프라인; (f) 일부 시술 후반부에 타이밍/경로 추종 미니게임(붕대, 정맥로). 연속 입력(홀드·커서 추종)이 물리/게임 상태로 이어지는 곳은 **검사 레티클과 미니게임 둘뿐**이고 CPR·제세동은 연속 입력이 아니다.
- 이동 제약: 환자 응급처치는 구급차 밖/안 위치 제약이 있다. 구급차 안에서는 일부 시술 불가·밖에서는 일부 시술 불가(유저 글 "a trach can only be performed outside the ambulance"; 개발사 답변 "neck brace or a tourniquet, which should be placed BEFORE entering the ambulance"). 들것으로 환자 이동 후 차에 싣는 단계가 필수.
- 상태 피드백: ① 환자 카드 HUD(우상단)에 혈압/심박/SpO2/혈당/체온/기도 6칸 + 상시 ECG 띠(심정지 시 적색 배경 "Cardiac Arrest", 심박 0). 충격 후 ECG 녹색 정상 리듬, 토스트 "Defibrillator used - Heart shocked!", 파트너 대사 "Heart rate stabilizing." [관찰 S7rqivMjNZw t≈0:11, 0:23, 0:26]. ② 증거 토스트(검사 중 "New Evidence: ..."). ③ 도구 툴팁: 이름, 분류(DIAGNOSTIC/CIRCULATION/BREATHING/MEDICATION), 설명, "USED FOR: ...", "MAY CAUSE: ..."(붉은 글씨 = 부작용), "Equip". ④ 체크리스트(좌측 Emergency Callout 패널의 Required/Recommended, 체크 표시). ⑤ 파트너 NPC 음성 + 자막.
- 이 구조의 약점(공식 인정): 개발사는 "We thought the game was more accessible than it is... player guidance is lacking significantly" (Troy_Aesir, 2025-02-17) 라고 답했다. 유저 사례: 기준이 안 보여서 태그 점수 항상 마이너스("Always Bad Triage").

---
### AL-4 안내·교육 모델

- 튜토리얼: "Introductory Training" 2단계 구성, 파트너 Angela Johnson가 대사로 안내. 화면 좌상단 단계 바 ("Get to the site of the accident" → "Park your ambulance near the patient and exit the vehicle" → "Locate the patient" → "Question the patient" → "Inspect the patient" → "Take a look at the Patient Protocol" → "Retrieve stretcher from the ambulance" → "Put the patient on the stretcher" → "Get the patient into an ambulance" → "Use Blood Oximeter" → "Use Blood Pressure Cuffs" → "Open the Patient Protocol"). 말풍선 필수 팝업은 "Continue [F]". 각 조작은 화면 박스("Movement controls", "Driving Controls")로 키를 알려줌. [관찰 H90F5LHg2AI t=0:02~8:06]
- 핸드북(H): 탭 General / Conditions / Interventions / Vitals / Evidence, General 항목 Ambulance, Game Modes, How-to Paramedic, Inspection, Medical Procedures, Patient Protocol, Scoring, Shift, Transport, Treatment, Triage Tags 순 (t≈5:05 프레임, 작은 글씨라 일부 오독 가능). 열면 일시정지(1.2.2). 개발사: "the in-game Player Manual ... is incredibly important, especially when playing Simulation Mode" [공식, 포럼]. 별도 DLC "Paramedic Handbook Dev's cut" 존재(상점 설명: "learn more about the various tools, procedures and systems").
- 진단 = **플레이어 선택 + 게임 보조의 혼합**. 태블릿 "Diagnosis": 좌측 Vitals·Patient Conditions, 우측 상단 **Category 드롭다운 + Interim Diagnosis 드롭다운**, 탭 Inspection / Treatment / Statistics. Inspection 탭: "Key Evidence", "Patient Examination"(몸 부위 번호 1~5별 증거 목록), "Situational Evidence". 분류 옆 숫자(예 "17", "2")는 증거 일치도 점수. 파트너가 "This patient does not appear to be in any immediate danger. Choose a category and an interim diagnosis that fits the evidence." 라고 말한다.
- Classic 모드에서는 게임이 증거 일치를 적/녹으로 칠하고(돋보기 아이콘: 초록=발견, 빨강=미발견/미치료), Treatment HUD에 "Recommended: Attach Oximeter ... Establish Vascular Access ... Administer Saline Solution" 체크리스트를 보여준다 [관찰 S7rqivMjNZw t≈0:11]. 패치 1.3 이후 **Simulation은 적/녹 평가와 추천 치료 HUD를 끈다** [공식].
- 설정 > Game(인게임 진입 후에만 표시; Troy 2025-03-07) 어시스트 토글 [관찰 c87W t≈0:38~1:08]: USER INTERFACE: World Markers / Abstract Treatment UI("Whether additional abstract UI elements should be shown during the treatment gameplay"); GAMEPLAY: Mode = Classic (Recommended) / Simulation / Custom; GENERAL: Allow Loading From Last Checkpoint, Turn off siren when exiting the ambulance, **Extend triage time**, **Disable Medical Procedure Minigames**("Skips the minigames for medical procedures when enabled"), **Fast patient deterioration**, Ignore pedestrian collision, Disable game over by totaled car; PLAYER GUIDANCE: Show map markers for inspection targets, Show already checked inspection targets, Show inspection hint, HUD Treatment Guidance, Show new evidence markers("Enable to highlight newly collected evidence with a marker in the patient protocol."), Show collected evidence in HUD. 난이도는 하나의 슬라이더가 아니라 **개별 어시스트 on/off의 묶음**.
- 월드 마커: 점검 대상에 지도 마커가 기본 On(Classic). 1.4: 비선택 환자에 "another ambulance will pick them up" 마커. 1.3: "automatic Discovery for very close patients".
- 개발사 설계 의도: "We want players to take the necessary steps to treat their patients as close to 100% as possible, but still feel the pressure of time against them. This is where the grading system comes into play" [공식, 포럼 2024-12-28]. 

---
### AL-5 실패·결과 모델

- 환자 사망: 명시 규칙 미공개. 플레이테스트에서 38% 환자 사망, 서비스 성공 50% [공식 블로그 #3]. 바이탈이 시간에 따라 악화(설정 "Fast patient deterioration" 토글). 버그성 급사 보고에 개발사가 "Hmm, yeah, that definitely sounds too fast" (감전 노동자 콜, 2025-02-12). 심정지는 CPR/제세동 필요.
- **플레이어 즉시 실패(Game Over)** 조건 [공식 블로그 #4, 설정 토글]: 보행자 치기, 구급차 파손("Disable game over by totaled car" 토글), 본인 치명상(불 속에 오래 서 있기). "Ignore pedestrian collision" 토글로 완화.
- 타이머: 시프트 15~45분 [2차 Gaming Nexus], 오버타임은 점수 감점(개발사 글). 다음 콜 대기 최대 30초(1.3에서 50→30). 대재난은 triage 제한시간 카운트다운(HUD 상단 중앙 "09:26:97" 형식, "Extend triage time" 토글). 
- 다중 환자: 1명만 이송, 나머지는 "other ambulances based on your triage choices"로 암묵 이송(개발사). "Missed Patient - Penalty" 감점 항목 존재.
- 치료 안 하고 입원: 패치 02-07 "Added Penalties if the patient is admitted untreated".
- 이송 중 사건: "Crashing into obstacles while transporting patients can now cause several different Injuries" (1.4), 파트너 "will only snap at you if you actually hurt the patient while driving" (1.4).
- Simulation: Game Over 후 체크포인트 재개 불가(1.3).
- 자동 적용 조치: 목 보호대·지혈대는 놓치면 게임이 구급차 진입 시 자동 적용하고 점수에서는 감점("An incorrect life saving measure usually means a treatment was missed, but automatically applied for you") [공식].

---
### AL-6 평가 모델

- 점수는 **결과 바이탈 중심**: "The Scoring is based entirely on VITALS. So, if you improve a patient's vitals by the time they reach the hospital, you will receive a higher score than if they stayed steady or deteriorated" [공식, 2025-07-14]. 이송 속도도 "scores very highly" [공식 2025-02-08]. 붉은 돋보기는 점수에 영향 없음.
- 환자별 등급 S/A/B/C 등: 결과 화면 "Patients" 표에서 Found → Treated → Admitted 3단계 타임라인, 각 단계 태그 아이콘(노랑/빨강 등), 우측 환자별 문자 등급(A/B/C/S). [관찰 s_H7wv4PrSs ~20:10, 5G1Ucc8zF6I ~22:44]
- 라벨 정확 문자열(일반 콜): "SHIFT RESULT" → "SELECT CALLOUT:" (목록 예 "Object Inhaled", "Drug Overdose") → 우측 패널 "Callout Info", "Patients", 열 머리글 "Found / Treated / Admitted", "Patient Rating", "Patient Score 1,200", "Difficulty", "Correct Life Saving Intervention", "Total Score: 1,586", "Total Rank: S", 버튼 "Show Patients". 좌측 "District XP Rewards: 2× S Rank 600, Shift Length +200, Total District XP: 800", 지구 레벨 바(Amon Heights, Lv 4).
- 대재난 결과 "CALLOUT RESULTS": 항목 "Patient Score 1,000", "Difficulty", "Missed Patient - Penalty", "Triage Score", "80% Patients Located", "Triaged Deceased Casualties", "Total Score", "Total Rank" (값은 480p 흐림, 해독 불가). 대재난 HUD 목표: "Find and tag all living patients with an appropriate triage tag before time runs out." / Recommended: "Total patients found", "Total patients triaged", "Bonus: Total corpses triaged".
- 콜 직후 디브리프 = 태블릿 **"Recommended Interventions"**(예 "Substance Interaction - Amphetamine Overdose: Attach Oximeter (monitor O2 saturation) / Optional: Administer O2 Mask / Establish Vascular Access (enable medication) / Administer Lorazepam")과 **"Treatment Provided"**(First Aid: "Head Checked", "Bleeding Checked", "Patient calmed"; Ambulance Treatment; Transport Complications), 바이탈 전/후 화살표, 동일 카드 "Statistics" 탭 [관찰 5G1Ucc8zF6I ~22:12~22:27].
- 1.4에서 "additional Tooltips to the Scoring screen to provide more context on why a certain score was achieved" → 이전에는 이유가 안 보였다는 반증. 리뷰도 "does not give details on which patients you correctly or incorrectly triaged" [2차].
- 임계값(S/A/B/C)·정확 공식·트리아지 정답 기준: 개발사가 공개 거부 ("I don't have any black-and-white guidelines for triage... it can really come down to vibes") → 미확인.
- XP: 시프트 완료로 XP → 해금(질환·도구·지구) [2차 + 상점 서술].

---
### AL-7 관찰 기록 (프레임 단위; 모두 `.planning/2026-10-04-sim-controls-benchmark/media/C/`)

| 영상(URL) | t | 보인 것 | 파일 |
|---|---|---|---|
| https://www.youtube.com/watch?v=HP8vyDnmipc (2025-04-21) | 0:36 | 도구가방 Circulation 툴팁 "Vascular Access 22G / CIRCULATION / Can be used to enable administering medicine intravenously." ✕ Equip | `ambulance-cardiac1-t0036.png` |
| 〃 | 0:56 | ECG 띠 적색 "Cardiac Arrest", 심박칸 0, 하단 HUD "Open 'Defibrillator' Handbook Page", "Unequip" (제세동기 장착 상태) | `ambulance-cardiac1-t0056.png` |
| 〃 | 1:14 | **CPR 툴팁**: "CPR (Cardiopulmonary Resuscitation) / CIRCULATION / Used to counter cardiac arrest. Immediate transport is required, as no further treatment is possible once your partner initiates CPR. USED FOR: Cardiac Arrest ✕ Equip" | `ambulance-cardiac1-t0114-cpr-tooltip.png` |
| 〃 | 1:04 / 1:36 / 1:44 | 양손 패들(코일 케이블) 들고 가슴 상부 위에 조준; "Right Thorax" 라벨과 원형 조준점; ECG는 여전히 "Cardiac Arrest"(이 클립에서는 심정지 해소 장면 없음) | `ambulance-cardiac1-t0104.png`, `-t0136.png`, `-t0144.png` |
| https://www.youtube.com/watch?v=S7rqivMjNZw (2025-03-20) | 0:11 | 환자 가슴 노출, ECG 적색 "Cardiac Arrest" 심박 0, 패널 Recommended 체크리스트(Inspect Patient 1 / Attach Oximeter (monitor temperature) / Establish Vascular Access (enable medication) / Administer Saline Solution / Administer Ketamine) | `ambulance-shock2-t0011.png` |
| 〃 | 0:17, 0:20, 0:21 | 오른손 패들 → 양손 패들이 가슴 위/아래로 이동, 케이블 | `ambulance-shock2-t0017.png`, `-t0020.png`, `-t0021.png` |
| 〃 | 0:23 | 토스트 "Defibrillator used - Heart shocked!", 파트너 Elijah Davis: "Heart rate stabilizing." | `ambulance-shock2-t0023.png` |
| 〃 | 0:26 | ECG 녹색 리듬 전환, 심박칸 텍스트 "...cular Fib"(잘림) | `ambulance-shock2-t0026.png` |
| https://www.youtube.com/watch?v=5G1Ucc8zF6I (2025-05-25) | ≈48:31 | **CPR 확인창** "CONFIRMATION — Your partner will take over CPR (Cardiopulmonary Resuscitation). Focus on driving! Warning: Performing CPR on someone without Cardiac Arrest is not recommended! Do you want to continue?" [Confirm][Cancel]; 뒤 패널 Recommended 목록에 "Apply Bandages", "Establish Vascular Access", "Administer Morphine (relieve pain)"가 보임(완료 여부는 식별 불가) | `ambulance-steezy-cpr-t4831-confirm.png` |
| 〃 | ≈48:34 | 확인 직후 토스트 "CPR performed - Partner started CPR", 목표 "Enter ambulance via side door" | `ambulance-steezy-cpr-t4834-toast.png` |
| 〃 | ≈49:02 | 운전 중 환자 카드 아래 "Perform CPR" 상태 표시, 목표 "Proceed to Hospital! / Careful Emergency Transport advised" | `ambulance-steezy-cpr-t4902-hudcrop.png`, `-t4902.png` |
| 〃 | ≈15:12 | 다중 환자 목표 패널 "Emergency Callout #8 Assess situation! Inspect location and patients, perform anamnesis, apply life-saving interventions if needed. Required: Place patient on stretcher. Recommended: Find patients (2 Found) / Triage patients (0 Triaged) / Anamnesis on Patient 1 / Inspect Patient 2 / Choose Interim Diagnosis (change anytime)"; 하단 HUD "Focus (Hold) L2 / Patient Protocol / Open Handbook / Map" | `ambulance-steezy-triage-t1512.png` |
| 〃 | ≈15:20 | 환자 위 프롬프트 "Anamnesis ◇ / Triage Package ✕"; 도구가방 툴팁 "Tracheostomy / BREATHING ... USED FOR: Object in airway / MAY CAUSE: (적색)" | `ambulance-steezy-triage-t1520.png` |
| 〃 | ≈15:36 | 태블릿 Diagnosis: Category "Substance Interaction", Interim "Amphetamine Overdose", Key Evidence "Hyperpnea", Patient Examination 목록(Uncontrolled Movement, Hyperpnea, Looks happy, Seems restless; Mydriasis, Rapid eye movement ...), Situational Evidence | `ambulance-steezy-triage-t1536.png`, `-t1544.png`, `-t1552.png` |
| 〃 | ≈16:26 / 16:28 | 트리아지 방사형: Immediate(빨강, "Life-threatening") / Delayed(노랑, "Serious, not life-threatening") / Minor(초록, "Walking Wounded") / Deceased(회색, "No Respiration, no Pulse") | `ambulance-steezy-triage-t1626-interactmenu.png`, `-t1628-tagselect.png`, `-t1628-tagselect-crop.png` |
| 〃 | ≈22:12~22:27 | 콜 종료 태블릿: "Recommended Interventions ... Treatment Provided (First Aid: Head Checked, Bleeding Checked, Patient calmed)" | `ambulance-steezy-score-t2218.png`, `-t2227.png` |
| 〃 | ≈22:44~22:49 | "SHIFT RESULT" S 등급, XP 패널, 환자 패널(Patient Score 1,200 / Correct Life Saving Intervention / Total Score 1,586) | `ambulance-steezy-score-t2249.png`, `-t2249-crop.png`, `-t2249-xpcrop.png` |
| https://www.youtube.com/watch?v=s_H7wv4PrSs (2025-02-18) | ≈2:03 (클립 3s) | 대재난 HUD: "Catastrophic Event — Emergency Triage Situation", "Find and tag all living patients ... before time runs out", 카운트다운 09:26:97, Dispatch 대사 "Keep yourself safe first. People depend on you for a swift and accurate assessment...", HUD 키: Focus (Hold) 마우스 글리프 / Patient Protocol / Open Handbook [H] / Map [M] / [T] | `ambulance-catastrophic-t0203.png`, `-t0203-hudcrop.png` |
| 〃 | ≈2:17, 2:58 | 환자에 홀드 시 원형 레티클+부위 점, "New Evidence: Inflamed skin / Obese", "The patient is losing blood"; Found/Triaged 카운터 | `ambulance-catastrophic-t0217.png`, `-t0258.png` |
| 〃 | 클립 c020s | 야전병원: "Select a patient by stretcher and drive them to a hospital. Required: Patient stowed" | `ambulance-catastrophic-end-c020s-fieldhospital.png` |
| 〃 | 클립 c106s(≈20:14 근방) | "CALLOUT RESULTS" 환자 표 Found→Treated→Admitted + 항목 "Missed Patient - Penalty / Triage Score / 80% Patients Located / Triaged Deceased Casualties" | `ambulance-catastrophic-end-c106s-results.png`, `-results-crop.png` |
| https://www.youtube.com/watch?v=c87W_4Y6_r0 (2024-12-20) | 0:38, 0:52, 1:04, 1:08 | Game 탭 설정(위 4절 토글 목록 원문 확인) | `ambulance-settings-t0038.png`, `-t0052.png`, `ambulance-settingsg-t0104.png`, `-t0108.png` |
| 〃 | ≈2:16~4:10 | Controls 탭 전체 키 바인딩 | `ambulance-settingskb-t0136.png` … `-t0250.png` |
| https://www.youtube.com/watch?v=H90F5LHg2AI (2024-12-20) | 0:02, 2:33, 2:52, 3:16~3:55 | Movement/Driving Controls 박스, 사이렌 Q·하차 R, Anamnesis [E] 프롬프트와 환자 대사 "My whole body's aching!", 검사 레티클 | `ambulance-tutorial-t0002-movement-controls.png`, `-t0233.png`, `-t0252.png`, `-t0316.png`, `-t0326.png`, `-t0355.png` |
| 〃 | ≈4:57 / 7:05 / 7:33 | Patient Protocol 첫 오픈(파트너 대사), 도구가방 설명 "This is your toolbag. It contains all instruments you have at your disposal..." [F Continue], 혈압 커프 지시 | `ambulance-tutorialc-t0457.png`, `ambulance-tutoriald-t0705.png`, `-t0733.png` |
| Aesir 공식 클립 (Bandage / Vascular Access / Progression / Anamnesis) | 클립 6~15s | 붕대 "Bandage" 점선 경로 + 화살표 + "Perfect/Continue"; 정맥로 슬라이더 "Press ◯ when the needle is in the right spot"; 초기형 결과 화면(Callout Info "Accident with sharp tool; One person injured", 환자 3명 B/C/A, Patient Score 800, Total Score 349, Rank B) | `ambulance-bandage-t0006.png`, `ambulance-vascular-t0006.png`, `-t0012.png`, `ambulance-progression-t0008.png`, `ambulance-anamnesis3-t0012.png` |

세 요구 영상 장면 결론: (a) CPR = **연속 입력 없음, 선택→경고창→파트너 인계→이송 모드**. (b) 제세동기 = **있음, 수동 패들 배치 방식, "분석(analyse)/클리어(stand clear)" 단계 영상에서 미관찰**. (c) 평가 태블릿·(d) 결과 화면 확인(5G1Ucc8zF6I, s_H7wv4PrSs).

---
### AL-8 CHOOGuard 적용 표

(제약 점검: 역무원 = 일반인 응급처치자(119 신고·소화기·AED+CPR 허용), 약물·삽관 등 구급대 전용 절차 금지; 모든 메카닉은 재난 종류에 비의존; 지각하지 않은 위험은 표시 금지.)

| 판정 | 기제 | CHOOGuard 동사 | 구체 방법(입력·피드백·실패) | 근거 |
|---|---|---|---|---|
| **Adopt** | 홀드 집중검사 + 부위/지점 스냅 + "New Evidence" 칩 | 위험 발견·점검 | 마우스 홀드(0.6~1.5초) 동안 원형 게이지가 차고, 시야 안 + 근거리인 대상(사람·문·기기·연기원)에만 증거 칩("문 손잡이 뜨거움", "가스 냄새")이 하나씩 쌓임. 시야·거리 밖 단서는 생성 안 함. 위치 마커·지도 핀(`Show map markers for inspection targets`)은 **쓰지 않음**. 실패: 검사 안 한 단서는 보고서에 빠짐 | [관찰 s_H7wv4PrSs t≈2:03, H90F5LHg2AI t=3:55]; 설정 토글 목록 [관찰 c87W t≈1:04] |
| **Adapt** | 플레이어가 카테고리+임시진단을 고르는 태블릿 | 무전 보고·119 신고 | Tab(수첩): 모은 증거 칩 중 선택해 "상황 유형(화재/가스/전기/인명/군중…)+규모(인원)+위치"를 구성, 틀려도 제출 가능. 사무소 응답은 **지시문이 아니라 도착 자원 변화**(잘못된 유형→잘못된 장비 도착). 추천 다음 단계 문구 없음. 증거 일치도 숫자("17") 같은 보조는 Classic형 어시스트 옵션에서만 | [공식 Patch 1.3 Simulation 비활성 목록]; [관찰 H90F5LHg2AI t≈4:57] |
| **Adopt** | 가이던스를 개별 토글로 분해(Classic/Simulation/Custom) | 모든 안내 | 기본값을 Simulation 쪽으로: 다음 단계 체크리스트, 경로 안내, 증거 마커를 각각 끌 수 있게. 현재 CHOOGuard의 ○/● 체크리스트·Q 휠의 "정답만 표시"·기본 켜진 경로 안내를 어시스트 옵션으로 격하 | [공식 Patch 1.3]; [관찰 c87W t≈1:04] |
| **Adapt** | CPR을 선택하면 "다른 모든 치료 종료"되는 기회비용 + 오용 경고 | CPR | Ambulance의 1클릭+파트너 인계는 **Reject**(지시 따라가기 그대로). 대신 기회비용만 가져옴: CPR 시작 → 플레이어가 환자 곁에 묶여 다른 동사(소화·신고·유도) 불가, 그 사이 다른 사건은 진행. 입력은 홀드/리듬 압박(속도 100~120/분은 일반 CPR 지침 수치를 적용한 [추론]; 메트로놈 SFX+가슴 상하 이동), 중단하면 즉시 상태 표시 하락. 구조대/AED 인계 시 해제. 무반응·무호흡 확인 전에 시작하면 경고(오용 감점) | [공식 포럼 2025-07-20: "Because CPR is perpetual, it ends all other treatment options"]; [관찰 5G1Ucc8zF6I ≈48:31 경고창 원문] |
| **Adapt** | 제세동 장비 장착→패들 배치→충격 | AED 사용 | Ambulance는 수동 패들이고 분석/클리어 단계가 없어 실제 AED 절차와 다름 → **AED 4단계로 교체**: ①케이스 찾기·가져오기 ②패드 2장을 가슴 두 지점에 붙이기(맨 가슴, 이물 제거 후 클릭 2회) ③ "분석 중, 환자에서 떨어지세요"(CPR 중단 강제) ④ 기기가 "충격 필요/불필요"를 **판단**, 필요 시 "물러나세요" 후 플레이어가 버튼; 접촉 중이면 충격 취소. 충격 여부는 플레이어가 아니라 기기가 결정 | [관찰 S7rqivMjNZw 0:11~0:26, HP8vyDnmipc 0:56~1:44]; [공식 포럼: "We kept manual defibrillation ... rather than ... automatic defibrillation"] ; [2차 유저 불만: 현대 제세동기는 자동 판정] |
| **Adapt** | 4색 트리아지 태그 + 카운터 HUD("found / triaged") | 다수 사상자 분류·인계 | 역무원 범위에서는 약식: 사상자에게 색 리본/태그(적·황·녹·흑 대신 "즉시/지연/경상/반응없음" 4칸 방사형) 붙이고, 119 도착 시 인계 요약. 카운터는 **본 사상자만** 카운트. 제한시간 시계 대신 신고 후 도착 ETA가 압박. 태그 재지정 허용(Ambulance 불만 해소) | [공식 포럼 triage 8단계]; [관찰 5G1Ucc8zF6I ≈16:28]; [2차 "Redo triage" 요청] |
| **Adopt** | 점수 = 결과 바이탈 개선 + 사건별 타임라인 표 | 교대 종료 평가 | 평가는 체크리스트 준수가 아니라 결과 중심: 사상자 상태 변화, 확산 정도, 대피 완료율, 도착 기관 인계 품질. 결과 화면에 사건/사상자별 Found→Treated→Handed over 타임라인과 항목별 가감점 툴팁 표시. Ambulance의 결함(어떤 판단이 맞았는지 비공개)은 반복하지 않고 사유 문장을 보임 | [공식 포럼 "Scoring is based entirely on vitals"]; [관찰 5G1Ucc8zF6I ≈22:49, s_H7wv4PrSs]; [공식 Patch 1.4 툴팁]; [2차 Gaming Nexus 불만] |
| **Reject** | 타이밍/경로 추종 미니게임(붕대, 정맥로) | 응급처치 동작 | 얕은 반응 테스트이고 개발사도 "make gameplay feel a little less repetitive" 목적에 "inconsequential"로 설계, 끄기 옵션까지 둠. CHOOGuard는 같은 자리에 **상태에 묶인 홀드 동작**(직접 압박 지혈 = 홀드 유지, 놓으면 출혈 재개; 소화기·밸브 기존 방식과 동일 문법)을 사용. 미니게임식 "Perfect" 판정은 도입 안 함 | [공식 포럼 2025-02-12, 04-27]; [관찰 공식 Bandage clip]; [2차 Gaming Nexus "death by a thousand minigames"] |
| **Adapt** | 도구 툴팁(USED FOR / MAY CAUSE) + 틀린 도구도 선택 가능, 성과 평가에서 감점 | 도구 선택·응급처치 오선택 | Q 무전휠이 "정답만" 보이는 문제를 해소: 도구 목록은 상황과 무관한 후보를 함께 보여주되(담요, 장갑, 물수건, 소화기 등), 툴팁에 "용도"와 "부작용"이 표기되고 오사용이 가능(예: 척추 손상 의심 환자를 억지로 이동 → 악화). 약물·침습 항목은 **목록에서 제외**(역무원 범위) | [관찰 5G1Ucc8zF6I ≈15:20 툴팁, 〃 cardiac1 t=0:36]; [공식 포럼: "you can certainly intubate conscious patients ... consequences in your performance review"] |
| **Adapt** | 플레이어 위험: 불 속 체류=사망/Game Over, 파트너 "Keep yourself safe first" | 플레이어 위험(연기·열) | 연기/열 노출 게이지는 HUD 숫자 없이 기침 SFX·화면 가장자리 어두워짐·이동 속도 저하로 전달(지각한 위험만). 일정 임계 초과 → 기절/실패. 소화기 사용 시 거리/바람 영향은 기존 심화 시스템에 통합 | [공식 블로그 #4]; [관찰 s_H7wv4PrSs ≈2:03 Dispatch 대사] |
| **Adopt** | 비선택 환자에 "다른 구급차가 데려간다" 마커 / 도착 기관 인계 | 도착 기관 인계 | 119·소방 도착 시 짧은 인계 대화(어떤 사상자가 몇 명, 무엇을 봤고 무엇을 했는지)를 말풍선에서 고르는 방식으로 입력, 품질이 점수에 반영. Ambulance는 "implied"로 숨겨 불분명하다고 개발사가 인정 → CHOOGuard는 실제 도착 개체로 명시 | [공식 Patch 1.4]; [공식 포럼 2025-01-04 "implication ... not very clear"] |
| **Adapt** | 환자 대화 질문 선택(Anamnesis) → 증거 칩 + "Patient calmed" 기록 | 사람과 대화·진정 | E 탭으로 대화창 열고 질문 3~5개 중 고름(관련 없는 질문 포함). 답변은 증거 칩이 되지만, 잘못된 질문에 불안 증가(진정 항목 평가). 질문 순서·누락이 평가 대상 | [관찰 H90F5LHg2AI t≈3:16~3:26, 5G1Ucc8zF6I ≈22:27 "Patient calmed"] |

---
### AL-9 미확인·한계

1. 키 바인딩은 데모 시점 영상(2024-12-20)의 설정 화면이다. 1.4 정식 빌드와 동일한지 미확인. Magic Game World 키 가이드 본문은 403이라 못 읽음(텍스트 교차확인 실패 → 영상 프레임 + 유저 글로만 교차).
2. "Focus (Hold)"가 마우스 좌/우 어느 버튼인지, "Run" 키가 Shift인지 480p 글리프로는 식별 불가. 트리아지 패키지의 PC 키, 제세동 충격 발사 입력, 치료 카메라 이동 외 일부 Navigate(Treatment) 키 미확인(영상이 PS 컨트롤러 글리프).
3. 제세동기에서 "분석/클리어/충전" UI가 없는 것은 **영상 길이 내에서 못 봤다는 것**이지, 전체 부재 확정은 아님. 개발사는 "defibrillator pads are unlocked much later"라 말하는데 영상에서 본 것은 **패들**이다. 용어 불일치 미해소. 충격 후 ECG가 녹색이고 심박칸에 "...cular Fib"가 찍힌 의미(충격 성공인지 VF 전환인지)는 확정 못 함.
4. CPR은 압박 횟수/호흡 비율/깊이 판정이 없다는 것까지만 확인(개발사 포함 3중 증거). 구급차 안/밖 모두 가능하다는 개발사 답변, CPR 이후 치료 불가가 일관된다.
5. 점수식·S/A/B/C 컷오프·사망 확률·악화 속도 수치 미공개/미확인. 대재난 결과 항목 수치(감점량)는 해상도 때문에 해독 불가.
6. 2025-04-09 이후 공식 패치 노트는 Steam News에서 발견 못 함 (이후 변경 여부는 불명).
7. 트리아지 정답 기준: 개발사가 공개 거부. 4색 정의 문구는 480p 크롭에서 읽은 것이라 일부 글자(괄호 설명) 오독 가능.
8. 02-07 패치의 정식 버전 번호(1.1 vs 1.2.0) 불일치 미해소.
9. 영상 업로더는 모두 제3자(공식 채널 클립 제외). UI 문자열은 게임 화면에서 직접 읽었으나 번역 설정은 영어(US).

---

## 3. Police Simulator: Patrol Officers (Aesir Interactive / astragon, 정식 2022-11-10)

> 작성 2026-10-04. 대상: Steam app 997010 (Aesir Interactive / astragon). **정정**: Steam 스토어 페이지 [공식]는 Early Access 2021-06-17, 정식 출시 2022-11-10로 표기(Steam 뉴스 "End of Early Access - Official Release OUT NOW" 2022-11-11). 과제문의 "EA 2021-01 / 1.0 2022-08"은 틀림. 마지막 업데이트는 **22.3.2 (2026-08-04)**, 개발 종료 공지 2026-08-21 [공식 Steam 뉴스]. 아래 인용에는 각 소스의 버전/날짜를 따로 적음(EA 초기 ~ 2025 빌드가 섞여 있어 규칙이 변한 항목은 병기).
> 핵심 한 줄: 이 게임이 "지시를 따라가는" 느낌이 아닌 이유는 **(1) 플레이어가 관찰 → 진단(분류) → 전체 선택지 중 고르기 → 사후 정당성 판정**을 하기 때문이다. 게임은 "무엇을 해라"를 말하지 않고 "방금 한 행동이 정당했는가"를 사후에 말한다.

---

### PS-1 출처 표

| URL | 유형 | 날짜·버전 | 검증한 내용 |
|---|---|---|---|
| https://store.steampowered.com/app/997010/Police_Simulator_Patrol_Officers/ | 공식 | 조회 2026-10-04 (EA 2021-06-17, 1.0 2022-11-10) | 기능 목록 "Intuition System", "Casual and Simulation game modes", "Progression to unlock districts, cars and modes", 카테고리/발매일 |
| http://news.patrol-officers.com/docs/Police%20Handbook%20Launch.pdf | 공식 (개발사 호스팅 PDF, 2쪽) | PDF 생성 2021-07-28, 서버 Last-Modified 2022-09-29; 본문은 EA 초기(≈v1.2) 규칙 | 교대/콜아웃/CP/SP/도구/Intuition/Focus/증거 목록/조작키 전부. 아래 "핸드북"으로 약칭. Steam 뉴스(2021-06-17)가 "in-game에도 있음, 가이드·위키 제작에 자유롭게 사용" 명시 |
| https://www.scribd.com/document/674986320/Police-Handbook-Launch | 2차(위 공식 PDF의 재업로드) | 동일 | 읽기 편의용. 공식 PDF 텍스트(pdftotext)와 주요 문구 대조 완료 |
| Steam 뉴스 API `ISteamNews/GetNewsForApp?appid=997010` (194건, 2021-02-11~2026-08-27). 개별 글 URL 형식: `https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/<gid>` | 공식 | 아래 gid별 | 아래 6건 묶음 |
| ↳ gid 4058279008612616458 | 공식 | 2021-03-12 | "You start each shift with 100 Conduct Points … Once you reach 0 … you're fired … you start each shift with 0 Shift Points … converted to experience points" |
| ↳ gid 4026754911345463159 | 공식(개발 블로그) | 2021-03-26 | 주차 위반 카테고리, "Follow the Tooltips … completely optional. With a press of a button, you can hide the tooltips", 개발자 인용(맵을 알면 위반을 효율적으로 찾음) |
| ↳ gid 4072920612519977801 | 공식(개발 블로그) | 2021-05-11 | 심문 규칙, "detaining … optional action which is rewarded with extra points" 설계 이유(플레이테스트), Intuition 텍스트 위치 |
| ↳ gid 5019805792179450478 | 공식 | 2021-06-15 | 교통 위반/풀오버/Casual 힌트 규칙 |
| ↳ gid 4056035920051668704 | 공식 | 2021-06-17 (EA 전날) | "Focus Mode time changed from 1.25s to 0.75s and transition duration from 0.5s to 0.2s", "Fixed … player lost conduct points for arresting a wanted person…", 핸드북 링크 |
| ↳ gid 4032392655110947598 | 공식 | 2021-07-07 | Casual vs Simulation 정의, 설정 경로 "Press Escape → Settings → Game", 4개 예시 옵션 |
| ↳ gid 4036897704070122478 / 4038024692304364216 | 공식 | 2021-07-13 (v1.1.0) / 2021-07-27 (v1.2.0) | Police Computer 도입, "Casual Mode is now the default", 휴무 2→4일, Focus Mode 기본키 RMB→**LMB**, CP 수치 변경(−40→−20, −5→−2, −10→−5) |
| ↳ gid 4516534149070207621 / 4018887653979666968 | 공식 | 2021-08-16 / 08-26 (v2.0) | "CP loss and SP gain are now displayed immediately", "Adjusted XP calculation to reduce the effect of CP loss" |
| ↳ gid 4237325463689846085 | 공식 | 2022-01-26 (Keys-to-the-City) | "We now have an explanation system for the loss of Conduct Points so that the process is more transparent"; Open Patrol |
| ↳ gid 4259848535846245632 | 공식 | 2022-03-23 (Holding Cells beta) | "Added CP Loss intuition feedback during encounters" |
| ↳ gid 4437747060844964580 | 공식 | 2022-05-31 (Traffic Mgmt beta) | NPC/NPV 명령 목록(대기/이동 후 대기/보도 금지/되돌아가기), 콘·바리케이드 |
| ↳ gid 5133583642646851312 | 공식 | 2023-04-26 (v9.0.0) | 'Select Violations' 창(복수 위반 누적), "You will of course receive SP (or lose CP!) for every handed-out violation" |
| ↳ gid 5679673637670811687 (+ 5688680204374200727) | 공식 | 2024-03-13 (First Aid Update, v13) | 응급처치/CPR 미니게임 규칙, 7 Duty Stars (district 2) 해금, 구급대 2명·들것·3명 이송 |
| ↳ gid 1803527891616966 / 1805431065493413 | 공식 | 2025-06-30 / 2025-07-22 (v18.0 QoL) | "refactoring the entire Conduct Points Systems", Space로 대화 스킵, CP 오차 다수 수정 |
| ↳ gid 1811138915356953 | 공식 | 2025-09-17 (v19.0 Self-Defense) | 무기 사용: 항복 요구 → 불응 시에만 사격, 위반 시 CP 손실, 3발 피격 시 교대 종료 |
| ↳ gid 1816849002007368 / 1815034433038217 | 공식 | 2025-10-31 (v20.0.4) / 2025-11-05 | "Interaction wheel restructure …"; **"On CP Loss and the Laws of Brighton"**: 16개 CP 규칙 변경을 되돌린다는 사과문 |
| ↳ gid 1839676055897194 / 1841579228668589 | 공식 | 2026-08-04 (v22.3.2) / 2026-08-21 | 최종 패치 / 개발 종료 공지 |
| https://steamcommunity.com/sharedfiles/filedetails/?id=2754369537 | 공식(개발사 직원 "wladi \| astragon" 가이드) | 댓글 2023–2025, 갱신 시점 미상 | SP/District XP/CP/Duty Stars 산식, 전체 해금 목록 |
| https://steamcommunity.com/app/997010/discussions/0/3884973963042159516/ ; /3068621701756214290/ ; /4030222382919934749/ ; /3832044617119923376/ | 2차 (플레이어 포럼) | 2021-06 ~ 2023-12 | Tab 길게 눌러 도구 휠(≈1초), 숫자 단축키, Police Computer 여는 법(Tab 홀드/차량 정지 후 M), 복수 위반 선택(PC: E / 패드: X), Fake ID vs Stolen ID 구분 |
| https://defkey.com/police-simulator-patrol-officers-shortcuts | 2차 (사용자 제출 단축키 표) | 페이지 갱신 2025-06-25 | F=Interact, Tab=tool wheel, LMB/RMB=tool primary/secondary, R=reload, I=Show encounter details, Z/X=콜아웃 수락/거절, Left Shift=toggle sprint (스크린샷으로 F 일부 교차확인) |
| https://policesimulatorpatrolofficers.fandom.com/wiki/Shift_Points ; /wiki/Conduct_Points | 2차 (팬 위키) | 조회 2026-10-04 | 행동별 SP 표(관찰값과 교차확인함 — §6) |
| https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PoliceSimulatorPatrolOfficers | 2차 | 조회 2026-10-04 | 잘못된 체포 CP −10(5+5), 부당 테이저 −20, CPR 미니게임 세부(미스 시 진행바 하락, 의식 있는 사람에게 CPR 시 거절·감점) — **공식 미확인** |
| 영상(표 §7): youtube t7X4G6aTP7Y, cmCIvDDoK1Q, 6gK8APyNEyM, 649uI00KAeg (astragon 공식 개발 클립 2021-03~07), p46792Lsswo (3CNoob, 2023-06-13, 게임내 날짜 11/07/2022), hGoCvVJomPE (HBTang, 2022-11-14), i0vNX8wKHq4 (2Cat Media, 2021-08-22), KN1vpG64TUI (Get in the Game 5000!, 2025-11-28), 5XxJS1q_D3k (Image_72, 2025-09-24), AYK8g1-eUz4 (GN Game Trailers, First Aid trailer) | 공식(astragon) / 2차(플레이 영상) | 각각 | 화면 관찰 — §7 |
| 접근 실패 | — | — | Fandom `/wiki/Actions` 403, Reddit(2건)·Facebook 그룹 글 차단 → 해당 글은 검색 결과 스니펫만 확인 |

---

### PS-2 조작 표 (PC 기본값 중심)

| 동사 | 입력(정확한 제스처) | 증거 |
|---|---|---|
| 이동 / 달리기 | WASD / Left Shift **토글** | [2차 DefKey] |
| 상호작용(대화 시작·앉기·책상 "End Shift" 등) | **F 탭** — 화면에 `F  Talk`, `F  End Shift` 식 프롬프트 | [관찰 §7-E] + [2차 DefKey] |
| 사람/차량 방사형 메뉴(interaction wheel) | F로 대화 시작 → 휠이 대상 주위에 열림. **마우스 방향으로 항목 하이라이트(파란 호가 가리킴) → 클릭 확정**, 하단 `Back`(마우스 버튼 아이콘), `Toggle Tooltips`(키 아이콘). 패드: `A Select / B Back / Y Toggle Tooltips` | [관찰 §7-A, E]. PC 확정/뒤로 키의 정확한 이름은 [미확인] |
| 포커스 모드(번호판·수배자·지나가는 차량 스캔) | **LMB**(v1.2.0 이후; 이전엔 RMB). 패드 LT. 문서에 "Focus Mode time 1.25 s→0.75 s, transition 0.5→0.2 s" → **약 0.75 s 유지(dwell)** 해야 활성 | [공식 뉴스 2021-06-17, 2021-07-27], [추론: dwell] |
| 도구 휠 열기 | **Tab 길게 누르기**(핸드북 "hold TAB", 포럼 "about 1 second"); 패드 LB(핸드북)/Y(v1.2.0 이후) | [공식 핸드북 5.2], [2차 포럼], [공식 뉴스 2021-07-27] |
| 도구 직접 선택 | 숫자키. 핸드북: 스턴건 = `2`. 포럼 목록: 1 레이더건, 2 테이저, 3 권총, 4 카메라, 5 손전등, 6 풀오버 사인, 7 로드블록 도구, 8 이동/대기?, 9 U턴, 0 후진. DefKey는 1·2 = "Inventory item", 9 = 모자 → **소스 간 불일치**(빌드/컨텍스트 차이 추정) | [공식 핸드북 일부] / [2차] |
| 도구 사용 | LMB = primary(레이더건 트리거·사진), RMB = secondary, R = reload. 카메라: "aim at damaged car parts and press the displayed button"; 증거가 촬영 가능할 때만 **아이콘이 뜸**(거리·오토줌 충족 시) | [공식 핸드북 5.3], [2차 DefKey] |
| Police Computer(지도/Background Checks/Handbook/Reports) | 도보: Tab 홀드 → 도구 "Police Computer" 장착. 차량(정지): **M**. 앱 전환 **Q / E**, 선택 **F**, 닫기 **Esc**(하단 바에 표시) | [공식 핸드북 5.6], [2차 포럼], [관찰 §7-E] |
| 콜아웃 수락 / 거절 | Z / X(패드는 HUD의 `Accept / Decline` 아이콘). 하나만 활성, 새 수락 시 기존 취소 | [2차 DefKey], [공식 핸드북 3.3], [관찰 §7-B 0:07 (좌상단 `Accept or Decline`)] |
| 대화 건너뛰기 | **Space** (v18.0, 2025-07) | [공식 뉴스 2025-07-22] |
| 복수 위반 누적 | 위반 항목에서 **E**(패드 X)로 'Select Violations' 창에 추가 → `Issue All Violations`(선택 전엔 회색) | [공식 뉴스 2023-04-26], [2차 포럼] |
| 차량: 풀오버 / ELS / 사이렌 / 경적 / 크루즈 / 시점 | R / 1 / 2 / 3(사이렌 중 경적=사이렌 톤 변경) / Shift / V | [공식 핸드북 8.7] |
| 근무 종료 | 사무실 책상에서 `F End Shift` → 로그인 연출 → 보고서 | [관찰 §7-E] |
| 응급처치 | 부상자 휠: `First Aid`, `Interview Accident Witness`, `Release to Ambulance` → 하위 `Call Ambulance` / `Dismiss and Call Medic`. **CPR = 리듬 미니게임**(아래). CPR 입력 키 이름 [미확인] | [공식 뉴스 2024-03-13], [관찰 §7-I] |
| 일시정지/설정 | Esc → Settings → Game | [공식 뉴스 2021-07-07] |

**제스처 유형 정리**: 탭(F) · 길게 누르기(Tab ≈1 s; Focus ≈0.75 s) · 조준+클릭(레이더건·카메라·풀오버 사인·스턴건) · 마우스 방향 선택 방사형 · **타이밍 입력**(CPR 액션 라인). **드래그·스크롤·원형 마우스 동작은 없음** [관찰/공식에서 발견 못함].

---

### PS-3 조작 문법

**3.1 기본 원시 동작**
- (a) **접근 → 지각**: 걷다가 눈·귀로 위반을 발견(§4). (b) **F** = 대화/상호작용 시작. (c) **방사형 메뉴** = 모든 사람·차량·사건의 단일 동사 선택 UI(2025-10 "Interaction wheel restructure for all driver and truck driver states / injured NPCs / pedestrians"로 재편). (d) **Focus(LMB, ≈0.75 s 유지)** = 대상 정보 질의(번호판 만료일, 수배자 일치 여부, 달리는 차량 위반) — 도구를 든 채 가능("You're holding the radar gun? … you can now still keep it in your hands and focus on stuff", 2021-07-27). (e) **도구 휠**(Tab 홀드) = 장비 교체.
- 선택 결과는 **즉시 일어난다**(되돌리기 없음). 플레이어 캐릭터가 선택한 항목을 **대사로 말하고 자막이 뜬다**: `I'm issuing you a ticket for littering, which will be about $25.` / `Please show me your ID.` / NPC `Sure officer, here you go.` / `Have a nice day and goodbye.` [관찰 §7-E]. 이 대사는 Space로 스킵 가능(2025).

**3.2 방사형 메뉴의 계층 (관찰)**
- 보행자 L0: `Ask for ID` · `Detain` · `Search` · `Handcuffing`(아이콘 옆 숫자 배지 ③/②, 의미 [미확인]) · `Issue Violation` · `Let Go` · `Give Traffic Orders`. 사건 컨텍스트에서 추가/활성: `Give accident report`, `DUI Tests`, `Interview Accident Witness`, `Interview Witness`.
- `Issue Violation` L1(보행자 민간 위반 전부 나열): `Littering` · `Jaywalking` · `Expired ID` · `Drinking in Public` · `Vandalism/ Spraying Graffiti` · `Possessing Drugs` · `Distributing Drugs` · `Match a Description` + 회색 `Issue All Violations`. L2: `Ticket` / `Verbal Warning`.
- `Handcuffing` L1(체포 사유): `Background Checks and Documents` · `Crime Scene` · `Narcotics` · `Illegal Weapon Possession` · `Fleeing`.
- 주차 차량 L1: `Parking Meter Expired` · `Too Close to Crosswalk` · `By Fire Hydrant` · `Expired License Plate` · `No Parking Area` · `Special Parking Zone` · `Position & Alignment`; 그 아래 세부 + `Call Tow Truck`.
- 운전자 L1: `Detain Car` · `Detain Driving` · `Background Checks and Documents` (+ 교통정지).
- **잘못된 선택지의 처리**: 숨기지 않는다. 해당 상황에 해당 없는 항목도 **전부 나열**되고 클릭 가능(→ 부당 판정). 회색 처리는 "아직 불가능"(예: `Interview Accident Witness`가 증인 아닐 때, `Issue All Violations`가 선택 0개일 때)만 [관찰]. 툴팁(하단 `i` 줄: "Jaywalking is a civil infraction.")이 분류 힌트를 주며 **끌 수 있음**(설정 `Radial Menu Tooltips`, 2025 빌드 스크린샷에서 Off) [관찰 §7-A, H].

**3.3 피드백 채널(소리·텍스트·수치)**
- **좌상단**: 시계·날짜, 활성 콜아웃 목록(`Issue Parking Tickets — North Point — Bonus: 90`), 진행 바(분절). **우상단**: 아이콘 2개 = SP / CP 실시간 카운터(`232 | 100`), 점수 변화 시 초록 `+10`이 SP 아래에 잠깐 뜸 [관찰 §7-E t=22:34]. **상단 나침반**: 콜아웃·POI 거리 마커(`412m` 등) + 그 아래 **Intuition 텍스트 한 줄**.
- 음성: 플레이어/NPC 보이스 + 자막(EA 전날 "VOICEOVERS! … German and English", 2021-06-17).
- 사건 상태 토스트: 예 `Fire Extinguished` [관찰 §7-I t=0:41] — 소방 NPC가 자동 처리.

**3.4 CPR 미니게임(2024-03 First Aid)** — [공식]: "a pattern on an action line, requiring exact timing to hit the action button within a defined threshold to earn points … Gather a sufficient number of points … to stabilize"; 실패 시 "the injured person will lose consciousness while still regaining their breath". [관찰 §7-I]: 화면 하단 가로 트랙에 쪼그려 앉은 사람 아이콘(박자)이 흘러가고 오른쪽에 ECG 파형, 좌측 목표 목록 `Perform CPR — Progress` 바가 히트마다 차오름(노랑→초록), 하단 `Cancel` 버튼. 순서 규칙: 경상 → 붕대 후 인터뷰 / 의식불명 위중 → "bandages and calling the medics, … do not require CPR" / 심정지 → "top priority is to administer CPR", 안정되면 붕대. 구급 "marked for dismissal" 후 들것, "Up to 3 injured people can be transported … at once". 해금: **district 2에서 Duty Star 7개**.

---

### PS-4 안내·교육 모델

| 층 | 내용 | 비고 |
|---|---|---|
| 튜토리얼 팝업 | 최초 조우 시 카드형 팝업 `Issue Violations` / `Fire Hydrant` 등, 하단 버튼 `H Handbook`(핸드북 열기) / `Confirm`. 팝업이 뜰 때 해당 차량에 **주황 외곽선** 하이라이트(튜토리얼 중). 본문 예: "After gaining **evidence** of a committed **violation** punish the criminal. Either issue them a **ticket**, leave it with a **verbal warning** or **arrest** them for severe felonies. You can issue violations one by one or **select them** to **issue all violations** at once. Even after handcuffing someone, it is still possible to issue additional violations or liabilities." / "Parking near a fire hydrant is prohibited in Brighton. If you come across a vehicle that is parked too close, you must give it a parking ticket and call for a tow truck to move it. Issue a ticket by opening the Radial Tool, selecting Parking Ticket, and then Fire Hydrant. You can also select Call Tow Truck from the Radial Tool menu." | [관찰 §7-E 22:09, 24:34]. 뉴스: "Replaced coded keys in tutorial texts with dynamic button prompts"(2021-06-17) |
| 핸드북 | 인게임 앱 + PDF. 법·증거 허용 행동을 "Evidence" 항목에 전수 목록화(예: Allowed **Detaining, Pulling over** ← Broken brake light / Expired ID / Ignored red light …; Allowed **Frisking** ← Angry / Nervous / Shaking …; Allowed **Searching** ← Dilated pupils / Smells like alcohol …; Allowed **Handcuffing** ← Carrying illegal items / Fake ID / Open warrant …). "Must Read": Focus Mode, Reasonable Suspicion, Probable Cause, Detaining, Asking for ID, Handcuffing, Casual & Simulation | [공식 핸드북 6.1] |
| **Intuition System** | 나침반 아래 텍스트. **색 의미**: Blue = 사람과 상호작용 중 얻는 추가 정보("I smell alcohol"); **Red = 경찰 자신의 행실/위험**("I'm driving too fast"); Green = Casual에서 추가되는 힌트("This person littered"); Yellow = 기타 | [공식 핸드북 6.2]. 관찰: 초록 `Someone just greeted you!`, `That man just littered!`; `I have enough info to start a search, but I could interview the remaining witnesses for more details.` |
| 월드 마커 | **Casual에서만**: 위반자 머리 위 **빨간 ↓ 화살표** + 초록 `That man just littered!` (Simulation은 아무 것도 없음 — 같은 장면 분할 비교). 인사하는 NPC 쪽 바닥에 **주황 빗금 구역**(설정 `Show pedestrians greeting you`) | [관찰 §7-D t=0:24], [관찰 §7-E t=29:07] |
| 사후 설명 | `Encounter Reports` 앱(Police Computer): 사건별 NARRATIVE에 `Justified: ID Check — Your suspicion was strong enough to ask for the person's ID.` 식 한 줄 설명 + 점수. 뉴스: "explanation system for the loss of Conduct Points" (2022-01), "CP Loss intuition feedback during encounters" (2022-03). 설정 토글 `Detailed Score Explanations`, `Immediate Points` | [공식 + 관찰 §7-E] |
| 진단 주체 | 대부분 **플레이어**: 신분증 사진 vs 얼굴 대조("people never wear any accessories on ID photos"), 주차미터 `Valid`(초록)/`Expired`(빨강), 번호판 만료일은 Focus로 확인, 표지판·연석 색(빨/노). 개발자 팁: "You can more efficiently check for parking violations when you know the map in detail … street cleaning on certain days" | [공식 핸드북 6.8, 8.5; 뉴스 2021-03-26] |
| 게임이 "직접 지시"하는 부분 | 콜아웃 목표문(`Clear the accident and write a report`), 진행바, 나침반 거리 마커, 증거 아이콘(카메라). 그러나 **무엇이 잘못됐는지는 지시하지 않음**(순찰 중 위반은 스스로 발견; Casual은 마커/텍스트 보조) | [관찰] |
| 난이도/보조 | 신규 경력 시작 시 Casual/Simulation 프리셋(Casual이 기본, v1.1.0), 이후 `Esc → Settings → Game`에서 개별 토글 "a quadrillion". 2025 빌드 설정: `Casual Mode` > `General Casual Settings`(Automated Car Repair, **Immediate Points**, **Detailed Score Explanations**, Ignore player damage, Ignore car damage, Ignore pedestrian collisions, No consequences from patrol car accidents, Show pedestrians greeting you, Show pedestrians asking for help, Show names of points of interest, Show current speed limit, Limited Tools, Automatic Refill Snare Net, Highlight crime scene evidence, Highlight spike strips, Show critically injured persons …), 위반 탐지 토글(Littering, Jaywalking, Carrying illegal items, Selling drugs, Drinking in Public, Car Theft, Wallet Thieves, Vagrancy, Trespassing), `Player violations`(Speeding, Ignored stop sign, Ignored red light, Siren on for too long, collisions, Discharged weapon without target, Crossing on a red light, Driving Off-road, Ignore towing penalty, Ignore dangerous fire extinguisher use …), `UI > Radial Menu Tooltips`, `CONDUCT POINTS > CP Deduction` | [관찰 §7-H] |
| CP Deduction 설명문(원문) | "Conduct Points represent proper police behavior. Misconduct or unjustified actions reduce these points. If your Conduct Points reach zero, the game ends and your shift resets… **Default (Mode-Based):** Points are deducted only in Simulation Mode. No points are deducted in Casual Mode or custom configurations. **Enabled:** Points are always deducted whenever misconduct occurs, regardless of mode or settings. **This is the intended way to play.** **Disabled:** Points are never deducted…" | [관찰 §7-H t=6:25, 빌드 2025-11] |
| 공식 정의 | Casual: "offer more help … lift up certain restrictions as well as reasons for punishment"; Simulation: "players who prefer a pure experience without any guidance from the game … learn … by trial and error" | [공식 뉴스 2021-07-07] |

---

### PS-5 실패·결과 모델

**5.1 CP (Conduct Points)**
- 매 교대 **100**에서 시작, 불법/부당 행동마다 차감, **0이면 해고(fired) → 교대 소실, 게임 시간은 교대 전으로 되감기, XP·Duty Star 없음** [공식 핸드북 3.4, 4.1].
- 교대 즉시 소실 사유: 해고(CP 0), 체포(부당 발포·차량 사격·보행자 치기), 중상(차량에 치임), 대형 사고 유발, 순찰차 파손 [공식 핸드북 3.4]. **부당 발포 = 100 CP 한 번에 차감**("unjustified shooting at somebody will subtract all 100 conduct points at once").
- 처벌 대상(핸드북 4.1): 운전 위반(속도·정지·적색·사이렌 무단); 도보 위반(무단횡단·적색 무시); **"Unjustified encounters and instructions: Aiming or shooting with a weapon, arresting, searching, frisking, detaining, pulling over, demanding ID, ordering drug/alcohol tests"**; 교대 소실 사유들.
- **수치(공식)**: v1.2.0에서 `shooting while not targeting` −20(전 −40), `unjustified detaining` −2(전 −5), `unjustified reporting of speeders with the radar gun` −5(전 −10) [공식 2021-07-27]. **관측**: 오접수 주차 티켓 1건 = 보고서 행 `8:51:22 AM Parking −5 +0`(CP −5, SP +0) [관찰 §7-F]; 보행자 행 `3:16:12 pm Pedestrian −10 +0` [관찰 §7-E]. [2차 TVTropes] 잘못된 체포 −10 = 수갑 5 + 이송 호출 5, 부당 테이저 −20 — 공식 미확인.
- **되돌리기**: 없음. 행동은 실행되고(체포/티켓 발급) 로그에 `Unjustified` 줄이 남으며 CP만 감소. 같은 행동 반복은 `Duplicate` 라벨로 점수 없음 [2차: FB 검색 스니펫 "Duplicate: Check"], 포인트 파밍은 패치로 차단("pulling over a car multiple times could be used to farm points", "Fixed issues where SP could get farmed") [공식].
- **지연/사후 실패**: 교대 시간 종료 후에도 "losing Conduct Points is still possible even after the shift ended" [공식 핸드북 3.1]. 콜아웃 시간 초과 CP는 "Fixed incorrect CP Loss for allegedly not arriving at the callout location in time"(2024-03) → 지각 페널티가 존재했음을 시사 [추론].
- **무행동 페널티 없음**: 콜아웃 거절 무페널티("declining doesn't have any negative consequences", 핸드북 3.3). 법규 위반을 못 본 척해도 CP 손실 없음("You only lose Conduct Points for doing unjustified actions rather than inactivity") [2차 TVTropes — 공식 미확인]. → 세계가 악화되지 않는 구조.
- **XP 환산**: 교대 끝에 CP가 줄었으면 SP→XP 변환이 줄어듦. 공식 가이드: "Losing 1 CP means 0,5% less XP" (80/100 CP → 90%); 단 v2.0에서 "Adjusted XP calculation to reduce the effect of CP loss". [관찰 §7-E/F]: (SP 401, CP −10) → `Lost CP Penalty −20`(=4.99%), (SP 106, CP −10) → `−4`(=3.8%) — **두 관측이 0.5%/CP 단순식과 일치하지 않음** [미확인: 정확한 산식].
- **설정으로 끌 수 있음**: `CP Deduction`: Default(모드 기준) / Enabled / Disabled. 단 "police brutality will also be punished, even when casual mode is turned on" [공식 뉴스 2021-07-07; 핸드북 2].
- **소화기 오사용도 CP 대상**: 2025 빌드 `Player violations` 토글 목록에 `Ignore dangerous fire extinguisher use`가 있음 → 소화기를 위험하게 쓰면(조건 미확인) 플레이어 위반으로 CP 손실. 구체 발동 조건·수치는 영상·문서에서 확인 못 함 [관찰 §7-H 4:30; 조건 [미확인]].
- **CP 규칙은 반복적으로 오작동·되돌려짐**: Steam 뉴스 42개 글에 `CP loss` 문구가 있고 "Fixed … CP loss" 류 수정 항목이 50여 개(EA 2021-06 ~ 2025-12; beta/정식 이중 게시 포함 — 본 조사자의 문자열 집계이므로 대략치) — 예: 2024-03 First Aid 패치 7건, 2025-07 "numerous CP Loss issues connected to car thieves", 2025-10 "Fixed CP Loss for checking ID of an injured NPC". 2025-06-30 "We're also refactoring the entire Conduct Points Systems … ensure no unintended loss of Conduct Points". **2025-11-05 16개 규칙 변경 전부 revert** ("Procedures are always part of routines. Breaking up routines can be very frustrating. In Police Simulator terms: Losing CP isn't fun."; 되돌린 항목에 `CP Loss after performing CPR and providing a bandage afterwards`, `CP Loss for DUI Test but the driver was driving suspiciously (according to the game) and alcohol test turned out positive`, `CP Loss for fake ID if asking was legal and you have conducted a background check for gathering the evidence` 포함) [공식].

**5.2 플레이어 신체 위험**
- Casual: 플레이어·차·보행자 무피해(`General`). Simulation: 차 중파손 = 교대 종료, 대파손 = 즉시 소실(핸드북 8.7), 차에 치임 = 교대 소실.
- v19 이후 무기 사건: 불응 범죄자에게 사격 가능, 단 "After a maximum of three hits, you will become critically injured and your shift will automatically end" [공식 2025-09-17].

**5.3 시간**
- 표준 교대 = 현실 **20분 = 게임 8시간**; 프리 로밍 교대는 핸드북에 "8 hours (also 8 hours in the game)"로 적혀 있어 현실/게임 시간 구분이 모호 [공식 핸드북 3.1 — 해석 [미확인]]. 교대 종료 후 "small buffer time" 뒤에는 새 SP 불가, 진행 중 의무는 마무리하면 보상. 종료 후 콜아웃은 오지 않음.

---

### PS-6 평가 모델

**6.1 점수 두 축**: SP (Shift Points, "justified actions … a good job", 교대 시작 0) ⟂ CP (Conduct Points, 시작 100, 감점 전용). 교대 후 `District XP`로 환산 → `Duty Star`(Melting Pot 예: 바에 `85/300 … 2/10` 식 표시, 별 10개/구역) → 해금 [공식 핸드북 4.2–4.3; 관찰].

**6.2 SP 항목(팬 위키 [2차] — 아래 ✓는 영상 관찰로 교차 확인한 값)**
정당한 행동은 보고서에 `Justified: <이름>`으로 기록되며 점수는 다음과 같다:
`Justifiably asking for ID` 5 ✓(`Justified: ID Check +5`); `Justifiably frisking person` 5 ✓(`Justified: Frisk (Found No Illegal Item)` +5); `Justifiably issuing parking ticket` 5/위반 ✓(`Parking +5`); `Justifiably issuing ticket` 10/위반 ✓(`Ticket for Littering` +10, `Ticket for Using an Expired ID` +10); `Justifiably giving verbal warning` 8; `Justifiably searching person` 5 — **관찰 상이**: `Justified: Body Search (Found Illegal Item)` **+10** ✓; `Justifiably handcuffing` 20/charge; `Booking a suspect` 45; `Interviewing witness` 10; `Arriving at callout location` 15; `Issuing sufficient accident report` 10 / `extensive` 15; `Greeting citizen` 2 (보고서 `Greeting` 행 +6 관측); `Giving directions` 4–15; `Taking picture of evidence` 1(피해 부위당 1); `Removing debris` 2; `Justifiably performing DUI test` 5(음성)/10(양성); `Justifiably calling tow truck` 10. 보고서 `Points of Interest` +40 [관찰]. 선행 `Detain` 시 추가 SP(설계 의도: "detaining will be made an optional action which is rewarded with extra points" — 플레이테스트에서 아무도 안 해서) [공식 2021-05-11].
- 사건 완료도 4단계: **Poor / sufficient / extensive / complete**; 진행 요인: 증인 인터뷰(보행자·운전자), 증인 ID 확인, 번호판 포커스(유효 번호판만), 증거 촬영, 음주/약물 검사(양성이 음성보다 가치↑). 콜아웃 UI에 분절 진행바 [공식 핸드북 6.6/8.3; 관찰 §7-G].
- 교대 미션 보너스("Shift Callouts", 비필수): `Issue parking tickets`, `Report speeders with the radar gun`, `Show police presence`(고범죄 지역 정화 — 임계 SP 달성 시 high crime → low crime, 추가 District XP) [공식 핸드북 3.3, 3.2]. HUD에 `Bonus: 90 → 135`처럼 실시간 누적.

**6.3 교대 종료 보고서 (관찰, v7.x 빌드 2022-11)**
- 흐름: 교대는 필드에서 끝낼 수 없다 — 시간 만료 후 **사무실 책상까지 돌아와 `F End Shift`** → 경찰서 로그인 화면(`Noah J. Jones … Welcome!`) → 문서 앱 `ShiftReport.mwdoc – MW Viewer Pro`.
- 본문: `Shift Report - 11/07/2022`, `SUMMARY`, `Reporting Officers: …`, 표 `Time | Category | [CP 아이콘] | [SP 아이콘]` 행이 애니메이션으로 쌓임(예: `2:16:09 pm Parking +0 +5 +5`, `3:16:12 pm Pedestrian −10 +0`, `3:31:36 pm Pedestrian +0 +25`, `Greeting +0 +6`, `Points of Interest +0 +40`). **카테고리 행 이름**: `Parking`, `Pedestrian`, `Greeting`, `Points of Interest` (이 클립에서 확인된 것만).
- 우측 패널 `Shift Evaluation`: **`Total Shift Points`** 401, **`Total Conduct Points`** −10, **`Lost CP Penalty`** −20, **`No Partner Bonus`** 80, **`District XP`** 461, 진행바 `Melting Pot  2/10`. 두 번째 리포트: 106 / −10 / −4 / 21 / 123. 산식 확인: **District XP = Total SP − Lost CP Penalty + No Partner Bonus** (401−20+80=461, 106−4+21=123 모두 성립); No Partner Bonus ≈ SP의 20%(80/401, 21/106) — 공식 문구 "Players receive a bonus on District XP when playing in single player mode"와 합치.
- 하단 바: `Skip`(애니메이션 중) → `Show/Hide Details` / `Leave`. 별 획득 시 `NEW UNLOCK!` 팝업 `You unlocked a new tool!`(Photo Camera) / `new callout type!`(Minor Accident) / `new neighborhood!`(Chester) / `Tool: Radar Gun` / `Report Speeders` / `Beaufort Landing` + `Confirm` — Steam 가이드의 1★/2★ 해금표와 정확히 일치.
- **합격 기준 없음**(통과/불통과 임계 없음): CP가 0이 아니면 항상 진행, 단 CP/SP 비율이 XP를 깎는다.

**6.4 진행(Duty Stars)** [공식 가이드 wladi|astragon]: 첫 구역 Melting Pot — 0★ Northpoint + Handcuffs + Background Check App + Greetings + 위반(Parking, Fake/Stolen/Expired ID, Jaywalking, Littering, Drinking in Public); 1★ Beaufort Landing + Report Speeders + Radar Gun; 2★ Chester + Minor Accident + Photo Camera; 3★ Historic Downtown + Cruiser + 보험/면허/ID 점검 + 차량 위반 12종; 4★ Alcombey + Traffic Stops + Pull Over Sign; 5★ Major Accident + Asking for Directions; 6★ Nightshifts + Flashlight + Road Flares; 7★ Graffiti; 8★ Traffic Cones + Road Barriers; 10★ High Crime Areas. 2구역 해금 = 첫 구역 7★; 3구역 Brickston = 합계 14★; **Open Patrol = 합계 16★**(가이드; 2022-01 공지는 "14 Duty Stars" — 소스 간 상이). **응급처치는 2구역 7★** [공식 2024-03].
- 주(週) 구조: 매주 월요일 구역 선택, 휴무 가능 일수 2→4(v1.1.0), 교대 속성 = 도보/차량 × 고범죄/저범죄 × 요일 영향 [공식 핸드북 3.1].

---

### PS-7 관찰 기록 (영상 프레임)

저장 위치: `.planning/2026-10-04-sim-controls-benchmark/media/C/` (git 무시). 파일명의 tMMSS는 **해당 mp4 내부 시각**이다. 긴 영상에서 `--download-sections`로 잘라낸 경우 "원본 시각" = 구간 시작 + 파일 내부 시각이며, 키프레임 컷 때문에 **±수 초 오차 가능**(프레임 내용 자체는 파일로 확인).

**A. astragon 공식 개발 클립 "Police Duties: Interrogations"** — https://www.youtube.com/watch?v=t7X4G6aTP7Y (2021-05-11, 사전 EA 개발 빌드, 인게임 시계 2020-05/06, 패드 프롬프트) — `police-interrogation.mp4`
| 시각 | 보이는 것 | 파일 |
|---|---|---|
| 0:09 | 보행자 방사형 L0: `Ask for ID`, `Detain`, `Search`, `Handcuffing`(배지 3), `Issue Violation`, `Let Go`. 하단 `A Select  B Back  Y Toggle Tooltips`, 정보줄 `Issue a ticket or leave it with a verbal warning. It's your choice.` HUD 우상단 `0 \| 100`, 미션 `Issue Parking Tickets / Alcombey / Bonus: 0` | `police-interrogation-t0009.png` |
| 0:10 | `Issue Violation` L1: `Littering`, `Jaywalking`, `Expired ID` + 정보줄 `Jaywalking is a civil infraction.` | `police-interrogation-t0010.png` |
| 0:33 | 사고 운전자 휠: `Give accident report`, `DUI Tests`, 회색 `Interview Accident Witness`; 미션 `Clear the accident and write a report — Progress`; HUD `0 \| 98`(CP 2 감소) | `police-interrogation-t0033.png` |
| 0:52–0:54 | 범죄 증인 휠에 회색 `Interview Witness`; 상단 텍스트 `I have enough info to start a search, but I could interview the remaining witnesses for more details.`; HUD `50 \| 98` | `police-interrogation-t0052.png`, `-t0054.png` |
| 1:00 | `Ticket` / `Verbal Warning` L2, HUD `15 \| 80` | `police-interrogation-t0100.png` |

**B. astragon 공식 "Police Duties: Parking Violations"** — https://www.youtube.com/watch?v=cmCIvDDoK1Q (2021-03-25) — `police-parking.mp4`: 0:07 주차 차량 휠에 **전 카테고리 나열** `Parking Meter Expired / Too Close to Crosswalk / By Fire Hydrant / Expired License Plate / No Parking Area / Special Parking Zone / Position & Alignment`, 하단 `Select / Back / Toggle Tooltips`, 좌상 `Manage a minor accident … Accept or Decline`(`police-parking-t0007.png`); 0:40 견인(`police-parking-t0040.png`).

**C. astragon 공식 "Police Duties: Traffic Violations"** — https://www.youtube.com/watch?v=6gK8APyNEyM (2021-06-15) — `police-traffic.mp4`: 0:34 정차 차량 휠 `Detain Car / Detain Driving / Background Checks and Documents`, 나침반 거리 마커 `412m / 766m / 970m / 70m`(`police-traffic-t0034.png`).

**D. astragon 공식 "Casual versus Simulation"** — https://www.youtube.com/watch?v=649uI00KAeg (2021-07-07) — `police-casualvssim.mp4`: 0:24 화면 분할(CASUAL/SIMULATION) — Casual은 빨간 ↓ 마커 + 초록 `That man just littered!` + 자막 "MORE INTUITION FEEDBACK", Simulation은 표시 없음(`police-casualvssim-t0024.png`); 1:04 Casual은 방사형 휠, Simulation은 현장(`police-casualvssim-t0104.png`). 같은 영상 캡션: "NO PLAYER INFRACTIONS - NO CONDUCT LOSS", "GET INTUITION FEEDBACK FOR ILLEGAL ITEMS AND MORE", SHIFT OVER 화면(contact sheet 확인).

**E. 3CNoob "Police Simulator Patrol Officers Gameplay Part 1 [No Commentary]"** — https://www.youtube.com/watch?v=p46792Lsswo (2023-06-13; 인게임 `MON, 11/07/2022`, 키보드 프롬프트 `F/Q/E/Esc`; 360p가 최대 ≤480p 옵션)
| 원본 시각 | 보이는 것 | 파일 |
|---|---|---|
| 21:55 | `Handcuffing` L1: `Background Checks and Documents`, `Crime Scene`, `Narcotics`, `Illegal Weapon Possession`, `Fleeing`, 회색 `Issue All Violations`; 하단 `[마우스 아이콘] Back`, `[키] Toggle Tooltips` | `police-tutorial-t0000.png` |
| 22:09 | 튜토리얼 팝업 `Issue Violations` (본문 §4) + `Confirm` | `police-tutorial-t0014.png` |
| 24:34 (구간 24:10 시작 + 24 s) | 튜토리얼 팝업 `Fire Hydrant` + `H Handbook` / `Confirm`, 대상 택시에 주황 외곽선 | `police-encounter2-t0024.png` |
| 22:30 | `Issue Violation` L1 (PC): `Littering`(파란 호 선택), `Jaywalking`, `Drinking in Public`, `Expired ID`, `Vandalism/ Spraying Graffiti`, 회색 `Issue All Violations` | `police-encounter-t0005.png` |
| 22:34 | 플레이어 대사 자막 `I'm issuing you a ticket for littering, which will be about $25.`, HUD `232 \| 100`, SP 아래 초록 `+10` | `police-encounter-t0009.png` |
| 22:58 | NPC 대사 `Have a nice day and goodbye.` | `police-encounter-t0033.png` |
| 23:10 | `Encounter Reports`: 목록 `Report #020: Pedestrian … #019: Parking`, NARRATIVE: `That person just littered!`(Intuition), `Justified: ID Check — Your suspicion was strong enough to ask for the person's ID. +5`, `This ID is expired.`, `Justified: Frisk (Found No Illegal Item) — You searched a person and everything seems in order. You didn't find any illegal items. +5`, `Justified: Ticket for Littering — You trusted in your evidence and detained the person for the correct reason. +10`, `Justified: Ticket for Using an Expired ID … +10`. 하단 바 `Esc Close  Q Previous Tab  E Next Tab  F Select` | `police-encounter-t0045.png` |
| 24:11 | `Issue Violation` L1 (7항목): `Littering`, `Jaywalking`, `Match a Description`, `Possessing Drugs`, `Distributing Drugs`, `Vandalism/ Spraying Graffiti`, `Drinking in Public` | `police-encounter2-t0001.png` |
| 24:31 | `Encounter Report #021`: Incident No / Date / Reporting Officer `Noah J. Jones (Me)` / Initial Incident `Pedestrian` / **Total Score `+15 / 0`** / Involved Person; `That person just jaywalked!`, `Justified: ID Check +5`, `Justified: Body Search (Found Illegal Item) — Your search discovered some illegal items carried by the person. You can use this evidence for further investigation +10` | `police-encounter2-t0021.png` |
| 29:07 | 초록 Intuition `Someone just greeted you!` + 주황 빗금 바닥 구역, HUD `359 \| 100` | `police-cploss-t0017.png` |
| 33:41 | 사무실 책상 `F End Shift`, HUD `401 \| 90`, 미션 `Issue Parking Tickets / North Point / Bonus: 135` | `police-shiftreport2-t0031.png` |
| 33:49 | 로그인 `Noah J. Jones … Welcome!` | `police-shiftreport2-t0039.png` |
| 33:53 | `Shift Report - 11/07/2022 / SUMMARY` 표 머리글, 하단 `Skip` | `police-shiftreport2-t0043.png` |
| 34:01 | 표 행 + `Shift Evaluation` (401 / −10 / −20 / 80 / 461, `Melting Pot 2/10`), 하단 `Show/Hide Details` | `police-shiftreport-t0001.png` |
| 34:05 | `NEW UNLOCK!  You unlocked a new tool!  Tool: Photo Camera  Confirm` | `police-shiftreport-t0005.png` |

**F. HBTang "Losing Conduct Points - Part 2"** — https://www.youtube.com/watch?v=hGoCvVJomPE (2022-11-14; 인게임 `11/08/2022`) — `police-cploss2.mp4`: 8:22 `Shift Evaluation` 106 / −10 / −4 / 21 / 123 (`police-cploss2-t0822r.png`, 크롭); 8:24 표 행 **`8:51:22 AM Parking −5 +0`** vs 정상행 `+0 +5` (`police-cploss2-t0824.png`).

**G. 2Cat Media "Minor Accident Report with Max Score!"** — https://www.youtube.com/watch?v=i0vNX8wKHq4 (2021-08-22, 초기 빌드) — `police-minoracc.mp4`: 1:52 목표 목록(`Report Speeders with the Radar Gun`, `Manage a minor accident — Bonus: 10`, `Clear the accident and write a report — Progress`)(`police-minoracc-t0152.png`); 4:56 / 7:10 분절 진행바가 채워짐 + `A Talk` 프롬프트(`police-minoracc-t0456.png`, `-t0710.png`).

**H. Get in the Game 5000! "New Conduct Point Settings!"** — https://www.youtube.com/watch?v=KN1vpG64TUI (2025-11-28) — `police-cpsettings.mp4`: 1:40 `GAMEPLAY` 목록(`Casual Mode Off`, `General Casual Settings`, `Automated Car Repair`, `Immediate Points`, `Detailed Score Explanations`, …; 설명 "No punishment for not clearing up a patrol car accident before leaving the area.")(`police-cpsettings-t0140.png`); 2:04 `Casual Mode: Makes different aspects of the game easier.`, UI에 `Radial Menu Tooltips Off`(`-t0204.png`); 3:10 `Player violations` 하위 목록(`-t0310.png`); 4:30 `Ignored red light` 등(`-t0430.png`); **6:25 `CONDUCT POINTS — CP Deduction  Default (Mode-Based)` 설명문 원문**(`-t0625.png`).

**I. Image_72 "Full CPR & First Aid procedure"** — https://www.youtube.com/watch?v=5XxJS1q_D3k (2025-09-24; 인게임 `MON, 05/08/2023`, HUD `1,864 \| 95`) — `police-cpr.mp4`: 0:21 / 0:30 / 0:39 CPR 액션 라인(비트 아이콘 + ECG + `Cancel` + 좌측 `Perform CPR — Progress` 바, 목표 목록 `Take care of severely injured persons`)(`police-cpr-t0021.png`, `-t0030.png`, `-t0039.png`); 0:41 토스트 `Fire Extinguished`(`-t0041.png`); 0:54 휠 `First Aid / Interview Accident Witness / Release to Ambulance`(`-t0054.png`); 1:03 `Call Ambulance` / `Dismiss and Call Medic` + 툴팁 "Once you have finished providing medical care and, if possible, interviewing them, release the person to the medic if they are on location; if not, an ambulance is called to transport the person to the nearest hospital. The ambulance will only depart once everyone has been released."(`-t0103.png`).

*(참고: `police-firstaid-trailer.mp4` = AYK8g1-eUz4는 UI가 없는 트레일러라 근거로 쓰지 않음. `police-scan1-*sheet*.png`는 위치 탐색용 contact sheet.)*

---

### PS-8 CHOOGuard 적용 표

전제(하드 제약): 역무원이 실제로 하는 일만; 모든 hazard 종류에서 동작하는 일반형; 지각하지 못한 hazard는 노출 금지. PS:PO의 경찰 전용 권한(수색·체포·수갑·총기·DUI 검사·견인)은 전부 제외.

| 판정 | 기제 | CHOOGuard 동사 | 구체 방법(입력·피드백·실패) | 근거 |
|---|---|---|---|---|
| **Adapt** | **증거-게이트 행동 + 사후 "Justified/Unjustified" 한 줄 설명(Encounter Report)** | 신고, 대피 유도, 통제선, 차단기/밸브/셔터 조작 등 모든 개입 | 사건·hazard 종류별 표를 만들지 말고 **percept 태그**(연기 보임, 탄내/가스 냄새, 쓰러진 사람, 군중 밀집, 이상 소음 …)를 플레이어가 *지각했을 때만* 기록 → 개입마다 "근거로 삼은 percept"를 대조. 사건 종료 후 Tab 보드 "조치 기록"에 `근거 충분: 짙은 연기를 직접 보고 승강장 대피를 요청했습니다` / `근거 부족: …` / `중복` 줄 + 점수. **되돌리기 없음**, 결과는 세계 반응(불필요한 대피 → 열차 지연)과 로그. 미지각 정보는 로그에 넣지 않음(사후 "놓친 것" 공개 여부는 별도 결정 필요) | [관찰 §7-E 23:10, 24:31], [공식 2022-01-26, 2022-03-23], [공식 2021-07-13 v1.1.0 "background checking an NPC that was asked for ID without justification gave the NPC evidence to make it justified" 수정 = NPC별 증거 플래그 모델], [추론] |
| **Adapt** | **전체 목록 방사형 = 플레이어 진단**(오답도 보이고 선택 가능) | 무전 보고, 방송/기관/열차 정지 요청 | Q-hold 휠을 "지금 맞는 다음 대사"에서 → **지각한 *증상* 전체 목록**(연기·불꽃·가스 냄새·쓰러진 사람·군중 밀집·누수·스파크·이상 소음·방치 물품 …, 사건 종류가 아닌 원자 관찰어)으로 교체. 플레이어가 고른 증상으로 관제가 출동 편성(틀리면 되묻기/잘못된 출동/지연). 하단 `Toggle Tooltips`로 분류 힌트 on/off, 해당 없는 항목은 회색이 아니라 선택 가능(회색은 "지금 불가"만) | [관찰 §7-B 0:07, §7-E 22:30, 24:11], [공식 뉴스 2021-03-26] |
| **Adopt** | **Focus(유지 0.75 s) = 관찰 질의, 도구 든 채 가능** | 발견/점검(hazard inspect), 플레이어 위험 판단 | LMB는 소화기 hold가 점유 → **RMB 길게(≈0.75 s)** 로 시야 내 대상 스캔 → Intuition 한 줄(관찰 문장). E-탭이 "정확한 처방"을 이름 붙이는 프롬프트는 제거하고 관찰만 제공. 시야·거리·가림 조건 필수(안 보이면 텍스트 없음) | [공식 2021-06-17, 2021-07-27], [공식 핸드북 6.3] |
| **Adapt** | **Intuition 색상 채널**(Blue 타인 관찰 / **Red 자기 행실·위험** / Green 보조 / Yellow 기타) | 발견, 플레이어 위험(연기·열), 난이도 보조 | 빨강 = 자기 안전(`연기가 짙어 시야가 흐려진다`, `열기가 느껴진다`), 파랑 = 사람 관련(`저 사람 숨소리가 거칠다`), 초록 = 보조 모드에서만 확인 강조. **월드 마커/화살표는 이미 지각한 대상에만** — PS:PO Casual의 원거리 빨간 ↓ 마커(미지각 위반자까지 표시)는 **채택 금지** | [공식 핸드북 6.2], [관찰 §7-D 0:24], [추론: 미지각 노출 금지 제약] |
| **Adapt** | **CPR 리듬 미니게임 + 단계 규칙** | 사상자 평가, CPR, AED | 레벨 0–4는 플레이어가 *관찰*(의식/호흡 확인)로 진단 → 심정지에서만 CPR. 입력: 하단 액션 라인에 박자 아이콘, 타이밍 창 안 탭 → `Progress` 바↑, 미스 → 바↓/정체, 임계 충족 → 안정 상태. AED: 켜기 → 패드 부착(길게 누르기) → 분석 대기 → 기기 음성이 "쇼크" 지시할 때만 버튼. 실패는 재시도 가능하되 시간 경과가 곧 상태 악화(레벨 연동). 순서 오류(예: CPR 후 붕대)에 감점 두지 않음(PS:PO가 되돌린 규칙). 약물·삽관 등 의료 전용 행위 없음. 의식 있는 사람에게 CPR 시도 시 거절 반응(2차 정보) | [공식 2024-03-13], [관찰 §7-I], [공식 2025-11-05 revert 목록], [2차 TVTropes] |
| **Adapt** | **교대 종료 보고서**(문서 앱, 행 단위 `Time/Category/±`, 요약 패널, `Skip`, `Show/Hide Details`, `NEW UNLOCK!`) | 교대 종료 평가 | 사무실/역무실 책상에서 `F  교대 종료` → 로그인 연출 → "근무 보고서". 행: 시각 · 카테고리(발견/보고/초동조치/대피유도/응급처치/인계 — CHOOGuard 동사로 교체) · 가점/감점. 우측: 합계·감점·보너스·누적 경험. 해금 팝업 `Confirm`. PS:PO처럼 합격 임계는 두지 않고 *비율*이 진행에 영향 | [관찰 §7-E 33:41–34:05, §7-F], [공식 핸드북 3.6] |
| **Adapt** | **인계 완성도 4단계**(Poor/Sufficient/Extensive/Complete, 분절 진행바, 질적 단계만 표시) | 기관 도착 시 인계 | 소방/구급/경찰 도착 시 *내가 본 것·한 조치·부상자·위험 요소*를 구두 브리핑 항목으로 전달(radial). 진행바는 채워지는 정도만 보이고 **빠진 항목 목록은 숨김**(미지각 노출 방지). 인계가 `Poor`면 기관 초기 대응 지연 | [공식 핸드북 6.6, 8.3], [관찰 §7-G] |
| **Adopt** | **군중/보행자 명령 + 장애물 도구** | 대피 유도, 통제선/출입통제, 엘리베이터·셔터 앞 대기선 | 방사형 `명령`: 멈춰서 대기 / 지정 지점으로 이동 후 대기(조준해 지면 마커 지정) / 이 통로 사용 금지 / 되돌아가 다른 경로 — PS:PO 목록 그대로. 콘·펜스·테이프는 도구 휠에서 장착해 설치. 군중 에이전트가 대사+자막("Please move back")으로 반응, 대체 경로가 없으면 정체·불복종 확률↑. 물리력·구속 없음(역무원 범위) | [공식 2022-05-31], [관찰 §7-E 22:30 Give Traffic Orders] |
| **Adapt** | **도구 휠(Tab 홀드) + 숫자 단축키 + 카메라/레이더식 "조준 → 표시 아이콘 뜨면 촬영"** | 소화기, 무전기, AED, 손전등, 점검 카메라 | 현재 숫자키/휠 없음 → Tab 홀드(≈1초)로 도구 휠, 1–9 직접 선택. 증거 촬영형 점검: 충분히 가까울 때만 촬영 아이콘이 나타남 = *지각 게이팅*. 이미 있는 소화기(LMB hold)는 유지 | [공식 핸드북 5.2–5.3], [2차 포럼] |
| **Adapt** | **Casual/Simulation 프리셋 + 개별 토글**(Immediate Points, Detailed Score Explanations, Radial Menu Tooltips, CP Deduction) | 난이도/보조 | 기본 프리셋 "표준"에서 경로 안내·즉시 점수·상세 설명·툴팁을 *각각* 토글. 문구는 PS:PO처럼 "Enabled … intended way to play"를 명시해 **권장 설정을 숨기지 않음**. 현재 "경로 안내 기본 ON"은 "표준=OFF, 교육=ON"로 분리 제안(결정은 Main) | [관찰 §7-H 6:25], [공식 2021-07-07] |
| **Adopt** | **교대 내 비필수 보너스 목표 + 커버리지 목표(`Show police presence`)** | 순찰, 점검 | 지시가 아니라 *선택 목표*: 점검 구역 N곳 시야 확인(걷기·시야 기반 발견)으로 보너스. "순찰 범위"를 채우면 해당 구역 위험 지표↓. HUD 목표 옆 `Bonus: n` 누적 | [공식 핸드북 3.2–3.3], [관찰 §7-E 33:41] |
| **Adapt** | **Duty Star식 단계적 동사 해금 + 튜토리얼 카드 + 핸드북** | 교육 | 소화기→호스→차단기→CPR/AED→방화셔터 순으로 교육 이수 교대 후 해금(PS:PO 응급처치는 2구역 7★). 해금 시 카드형 튜토리얼 `Confirm` + 핸드북 항목 추가. 실제 역무원 교육 이수와 대응 | [공식 2024-03-13], [공식 가이드 wladi], [관찰 §7-E 22:09] |
| **Adopt** | **선택 항목을 캐릭터 대사로 발화 + NPC 반응 보이스, Space 스킵** | 보고, 안내 방송, 대피 안내 | 휠 선택 = 무전/육성 문장으로 자막+음성(`승강장 3번 선로에서 연기 발생, 대피 안내 시작합니다`). 잘못된 선택은 HUD 문구가 아니라 상대 반응(관제의 되묻기, 승객의 의아한 반응)으로 *먼저* 전달. Space로 스킵 | [관찰 §7-E 22:34], [공식 2025-07-22], [2차 TVTropes "unique voice clip" — 공식 미확인] |
| **Reject** | **CP 0 = 해고 + 교대 되감기 + 사전 열거된 합법/불법 표** | 실패 모델 | PS:PO는 이 표 때문에 CP 오작동 수정이 50여 건(§5.1), 2025-06 전면 리팩터링, 2025-11 16규칙 revert ("Losing CP isn't fun"). CHOOGuard는 사건을 LLM이 즉석 합성하므로 *사전 열거 불가* → **결과(세계 상태) 중심 평가 + 행동 로그 설명**으로 대체. 되감기·해고 없음. (PS:PO 스스로도 `CP Deduction` 설정에서 Enabled를 "intended way to play"로 두되 끌 수 있게 함) | [공식 2025-06-30, 2025-11-05], [관찰 §7-H 6:25] |
| **Reject** | **콜아웃 거절 무페널티 / 무행동 무영향** | 우선순위 판단 | PS:PO는 방치해도 세계가 악화되지 않음. CHOOGuard는 동시다발 사건에서 *선택하지 않은 쪽이 실제로 악화*되도록 해야 "지시 수행"이 아닌 판단이 됨 | [공식 핸드북 3.3], [2차 TVTropes], [추론] |
| **Reject** | 체포·수갑·수색·몸수색·무기·음주/약물검사·견인 | — | 역무원 범위 밖(경찰 전용). 대응 동사는 "안내·통제·인계(경찰 도착 시)"로 한정 | 제약 조건 |

---

### PS-9 미확인·한계

1. **PC에서 방사형 확정/뒤로/툴팁 토글의 정확한 키 이름**: 스크린샷은 마우스·키 아이콘만 보임(LMB 확정·RMB 뒤로로 추정). 패드 버튼은 확인됨.
2. **CPR 입력 키**와 박자 간격(초 단위), 히트 판정 창 폭, 점수 임계값: 공식은 "exact timing … defined threshold"만 서술. 영상에서 입력 키·판정 피드백(소리/글자)은 확인 못 함.
3. **CP 산식**: 관측 2건이 0.5%/CP 단순식과 불일치(§5.1). `Total Conduct Points`(−10)와 `Lost CP Penalty` 사이의 정확한 함수는 미확인.
4. **`Handcuffing` 아이콘 옆 숫자 배지(③, ②)의 의미** 미확인.
5. **실제 "Unjustified: …" 보고서 줄을 영상으로 직접 확인하지 못함**. 확인한 것은 `Justified:` 줄(영상) + 보고서 행 `−5 +0`(영상) + 검색 스니펫상 `Unjustified: Ticket for Not Having Valid Insurance`, `Duplicate: Check`(Facebook 그룹 글 스니펫, 2차 — 본문 접근 차단).
6. **부당 행동 선택 순간의 즉시 피드백**(빨간 Intuition 문구/효과음)을 영상으로 직접 보지 못함. 공식 문헌(2021-08 "CP loss … displayed immediately", 2022-03 "CP Loss intuition feedback")과 우상단 카운터 감소(영상 0:09→0:33에서 100→98)로만 추정.
7. **핸드북 내부 불일치**: 6.3은 Focus=left click이나 다른 항목은 "Mouse: Right click"(v1.2.0 이전 문구 잔존). 숫자키 매핑도 소스 간 불일치(§2).
8. Open Patrol 해금 별 수 14(2022-01 공지) vs 16(가이드) 불일치 — 최신 값 미확인. 프리 로밍 교대 길이 표기("8 hours … in the game")도 모호.
9. 추가 확장(Highway Patrol, Contraband, Mission Update: CCTV·가정폭력·무단노숙 등)의 새 상호작용 문법은 패치 노트로만 확인, 영상 관찰 안 함.
10. 영상 해상도: 3CNoob 원본은 ≤480p 요청에도 최대 360p 제공 → 작은 글씨 일부 판독 제한(`Searched / Switch Blade …` 패널 등). 소스 1(Reddit 2건, Facebook), Fandom `Actions` 페이지는 접근 차단/403.
11. TV Tropes의 수치(−10, −20, CPR 세부)는 **2차**이며 공식 확인 못 함.

---

## 4. Arma 3 ACE3 의료 (+ KAT/KAM AED)

조사일 2026-10-04. 기준 버전: **ACE3 master `6f5e12a` (커밋일 2026-10-01, `script_version.hpp` = 3.21.2.113)** — 소스를 직접 클론해 `addons/medical*`, `addons/interact_menu`, `addons/common`을 읽음. 공식 문서 페이지는 "Added in ACE3 v3.0.0" 표기이며 일부 문장이 소스와 어긋남(§9 참조). **KAT = 저장소명 `KAT-Advanced-Medical/KAM`, master 브랜치명 `dev-Tomcat`, `ba7a5a1` (2026-09-20), `script_version` 3.2.1.77.** (과제문의 `Advanced-Combat-Medicine/KAT-Medical` URL은 HTTP 404라 사용하지 못함.)

---

### ACE-1 출처 표

| URL | 유형 | 날짜·버전 | 검증한 내용 |
|---|---|---|---|
| https://ace3.acemod.org/wiki/feature/medical-system | 공식 | 문서 "Added in ACE3 v3.0.0", 2026-10-04 열람 | 설정 서술 전문, "Curated Medical Settings" 프리셋(Co-Op Preset 1 / "Basic" / "Advanced" / PvP), Medical Menu 기본키 "H", 5분 Cardiac Arrest Time, CPR 40% 서술 |
| https://ace3.acemod.org/wiki/framework/medical-framework | 공식 | "Added in ACE3 v3.13.7" | damage→wound 처리, `ace_medical_const_*` 내부 상수 존재 |
| https://ace3.acemod.org/wiki/framework/medical-treatment-framework | 공식 | "Added in ACE3 v3.14.2" | `ace_medical_treatment_setSpO2UponCPRSuccess` (default true), 의료 차량/시설 |
| https://ace3.acemod.org/wiki/feature/interaction | 공식 | v3.0.0 | "Press and hold `Ctrl`+`⊞ Win`" = self, "Press and hold `⊞ Win`" = interaction menu |
| https://ace3.acemod.org/wiki/user/shortcuts | 공식 | — | `⊞ Win` Interaction menu, `Ctrl`+`⊞ Win` Self-interaction menu |
| https://ace3.acemod.org/wiki/framework/interactionmenu-framework | 공식 | v3.0.0 | action 필드(`condition`, `statement`, `runOnHover`, `distance`, `modifierFunction`), `progress bar duration default 10` |
| https://github.com/acemod/ACE3 (소스, master `6f5e12a`) | 공식(소스) | 3.21.2.113 | `medical_treatment/initSettings.inc.sqf`, `ACE_Medical_Treatment_Actions.hpp`, `fnc_cprLocal/cprSuccess/cprProgress/canCPR/checkPulseLocal/checkResponse/getBandageTime/treatment`, `medical_statemachine/Statemachine.hpp` + `fnc_handleStateCardiacArrest/enteredStateCardiacArrest/handleStateUnconscious`, `medical_vitals/fnc_updateHeartRate`, `medical_gui/*`(initKeybinds, InteractionBodyParts, fnc_onKeyDown, updateInjuryList), `medical_feedback/*`, `interact_menu/XEH_clientInit.sqf + fnc_keyDown/keyUp`, `common/fnc_progressBar` |
| https://ace3.acemod.org/2016/11/04/ace3-version381.html (ACEREP #00007) | 공식 | v3.8.1, 2016-11-04 | 의료 재작성 예고: CBA state machine으로 "unconsciousness or cardiac arrest" 상태·전이 엄밀 정의 |
| https://ace3.acemod.org/2019/12/18/medical-rewrite.html | 공식 | 2019-12-18 (rc → 3.13.0) | "removing the concept of 'Basic vs Advanced Medical', instead opting to provide users a set of settings" |
| https://ace3.acemod.org/2019/12/31/ace3-version3130.html (ACEREP #00010) | 공식 | v3.13.0, 2019-12-31 | 재작성 공개: 바디 이미지 색 규칙, 골절, 통증→심박, 심정지 대응 권고, 피드백 효과 |
| https://raw.githubusercontent.com/acemod/ACE3/v3.12.6/docs/wiki/feature/medical-system.md | 공식(구 문서) | v3.12.6 (재작성 직전) | Basic vs Advanced 기능 목록·치료 절차 6단계 |
| https://github.com/acemod/ACE3/pull/7983 | 공식(개발) | 2020-11 개설 | CPR 성공률을 혈액량 기준으로 min~max 보간 ("Interpolates ... between 60% ... and 85%") |
| https://github.com/KAT-Advanced-Medical/KAM/blob/dev-Tomcat/docs/en/Cardiac/02_aed_manual.md | 공식 | KAM 3.2.1.77 | AED 4단계(Preparation/Apply Pads/Analyze Rhythm/Re-evaluate), 리듬 4종, "Shock Advised"/"No Shock Advised" |
| 같은 저장소 `addons/circulation/*` (initSettings, ACE_Medical_Treatment_Actions.hpp, fnc_AED_Analyze/Charge/Shock/CPRStart/cprLocal) | 공식(소스) | 3.2.1.77 | AED/CPR 타이밍·기본값·입력 처리 |
| YouTube `x1vZbZW0l88` "Arma 3 ACE Medical Basics - Soldier Medical" (Shalazan) | 2차(영상) | 2022-09-15 | 의료 메뉴·CPR 실플레이 |
| YouTube `hD1cAe71GIw` "ACE3 Interaction MADE SIMPLE" (LeonGremory) | 2차(영상) | 2020-09-05 | 인터랙션 메뉴 hold/release, "Do action when releasing menu key" |
| YouTube `GbNw6IoZh_M` "Arma 3 Ace 12.6 BASIC" (Kadava) | 2차(영상) | 2021-03-12 | 3D body 선택 노드 |
| YouTube `tH15xkWFTr8` "Arma 3 KAT medical rundown/tutorial" (Polarwhisper6) | 2차(영상) | 2024-09-04 | KAT AED: pads → Analyze Rhythm → Administer Shock |
| YouTube `YexB3e0SsKQ` "Arma 3 moments: AED to the rescue" (BipolarBear) | 2차(영상) | 2019-03-11 | 구버전 KAT의 단일 항목 "Automated External Defibrillator(s)" |

(영상 자막 구간 탐색은 yt-dlp auto-sub 사용 — 2차. 영상은 UI·진행바 관찰 증거로만 사용, 메커니즘 수치는 모두 소스/공식문서에서 취함.)

---

### ACE-2 조작 표 (verb → 정확한 입력)

#### ACE-2.1 ACE3 인터랙션 메뉴 (모든 의료 행동의 진입점 중 하나)

| 동사 | 입력 | 증거 |
|---|---|---|
| 외부 인터랙션 메뉴 열기 | **`⊞ Win`(Left Windows, keycode 219) 누른 채 유지** | [공식] interaction 문서 "Press and hold `⊞ Win` (ACE3 default)"; [공식 소스] `XEH_clientInit.sqf`: `[219,[false,false,false]]` |
| 자기 인터랙션 메뉴 | **`Ctrl`+`⊞ Win` 누른 채 유지** | [공식] 동일; 소스 `[219,[false,true,false]]` |
| 토글 방식 | "Interact Key (Toggle)" / "Self Interaction Key (Toggle)" — **기본 미바인딩** (`-1`) | [공식 소스] |
| 노드 선택 | 키를 유지한 채 **마우스 커서를 노드 위로 이동**(커서가 풀림), 하이라이트 = 빨간 원형 링 | [관찰 hD1cAe71GIw t=05:13] |
| 실행(확정) | **키를 놓으면 호버 중인 노드 실행**, 또는 LMB 클릭. 설정 `ace_interact_menu_actionOnKeyRelease` 기본 **true**, UI 문구 "Do action when releasing menu key" | [공식 소스] `fnc_keyUp` + initSettings; [관찰 hD1cAe71GIw t=05:35] |
| 열 수 없는 상태 | 텍스트 입력 중, 또는 기본 `canInteractWith` 조건(사망·기절·Zeus 카메라 등)에 걸리면 안 열림. `fnc_keyDown`은 `isNotInside, isNotDragging, isNotCarrying, isNotSwimming, notOnMap, isNotEscorting, isNotSurrendering, isNotHandcuffed, isNotSitting, isNotOnLadder, isNotRefueling`를 *예외(=무시)* 로 넘기므로 차량 안·수영·끌기·사다리·지도 보는 중에도 열림. 기절하면 열린 메뉴 강제 종료(`ace_unconscious` 이벤트) | [공식 소스] `fnc_keyDown`, `common/fnc_canInteractWith`, `XEH_clientInit.sqf` |
| 의료 3D 노드 | 다른 유닛에 대고 `⊞ Win` 유지 → 몸 위에 `Head/Torso/Left Arm/Right Arm/Left Leg/Right Leg` 십자 노드 + 메인에 `Medical Menu`, `Interactions`. **노드 도달 거리 `MEDICAL_ACTION_DISTANCE` = 1.75 m**. 노드에 호버(`runOnHover = 1`)하면 환자 정보 패널(`displayPatientInformation`)이 뜸. 기본 설정 `ace_medical_gui_enableActions` = 0 "Selections (3D)" (1 = "Radial", 2 = Disabled) | [공식 소스] `InteractionBodyParts.hpp`, `medical_gui/initSettings`; [관찰 GbNw6IoZh_M t=01:30] |
| 자기 치료 | `Ctrl`+`⊞ Win` → `Medical` → 부위 → 치료. 설정 `ace_medical_gui_enableSelfActions` 기본 true | [공식 소스] |

#### ACE-2.2 의료 메뉴(다이얼로그, 월드는 계속 진행)

| 동사 | 입력 | 증거 |
|---|---|---|
| 의료 메뉴 열기 | **`H`** (CBA 키바인드 "Open Medical Menu"). 대상 결정 순서: `cursorTarget` → `cursorObject` → 카메라 전방 `lineIntersectsSurfaces`(`maxDistance`까지) → 없으면 **자기 자신**. 사람에게 시선 없이 누르면 본인 메뉴가 열려서 혼동이 흔함 | [공식 소스] `medical_gui/initKeybinds.inc.sqf`; 영상 자막 x1vZbZW0l88 t=04:48–05:09 "[2차]" |
| 닫기 | `H` 다시 누르기, 또는 **연 지 0.5 초 초과 후 키 업 시 닫힘**(= 짧게 탭하면 열린 채 유지, 길게 누르고 떼면 닫힘) | [공식 소스] 키 업 핸들러 `CBA_missionTime - lastOpenedOn > 0.5` |
| 자동 닫힘 | 환자가 `ace_medical_gui_maxDistance`(기본 **3 m**) 밖이면 닫히고 "DistanceToFar" 메시지 | [공식 소스] `fnc_menuPFH` |
| 자기 정보 훑어보기 | **`Ctrl`+`H` 유지**(Peek Medical Info) → 자기 환자정보 HUD, 키 놓은 뒤 `ace_medical_gui_peekMedicalInfoReleaseDelay`(기본 1 s) 뒤 페이드아웃 | [공식 소스] |
| 부위 선택 | 바디 도형 클릭, 또는 **W=머리 / S=몸통 / D=왼팔 / A=오른팔 / X=왼다리 / Z=오른다리** | [공식 소스] `fnc_onKeyDown.sqf` |
| 카테고리 선택 | 아이콘 클릭 또는 **숫자키 1…0** — 보이는 카테고리 순서대로 동적 할당: `triage, examine, bandage, medication, airway, advanced, drag, toggle("Switch to target/self")`. 사용 가능한 행동이 없는 카테고리는 회색 비활성 | [공식 소스] `fnc_onKeyDown`, `fnc_updateCategories` |
| 행동 실행 | 목록 항목 클릭 → 진행바(§3). 성공 후 메뉴 자동 재오픈 `ace_medical_gui_openAfterTreatment` 기본 true("Reopen Medical Menu") | [공식 소스]; [관찰 ace3-cpr t=0:38] |
| 트리아지 | `Triage` 카테고리에서 `None / Minimal / Delayed / Immediate / Deceased` 버튼으로 수동 태그. 트리아지 색이 인터랙션 메뉴 아이콘에도 반영(설정 `interactionMenuShowTriage` 기본 1 "Anyone") | [공식 소스] |

#### ACE-2.3 치료 행동별 소요 시간(ACE3 core 기본값, 단위 초)

| 행동 | 시간 | 허용 부위 / 비고 | 증거 |
|---|---|---|---|
| Diagnose(basic) / Check Pulse / Check Blood Pressure / Check Response | **2.5** (`Diagnose.treatmentTime`) | Pulse = All; BP = 팔·다리만; Response = Head만, 자기 대상 불가 | [공식 소스] `ACE_Medical_Treatment_Actions.hpp` |
| Bandage | 상처당 소 **4** / 중 **6** / 대 **8** (advancedBandages≠0이면 잔량·효율에 따라 ×0.666~1), 의무병 −2, 자기 자신 +4, 상처 2개↑이면 −2×개수, 하한 **2.25** | 전 부위 | [공식 소스] `BANDAGE_TIME_*`, `fnc_getBandageTime` |
| Tourniquet 적용 | **7** (`treatmentTimeTourniquet`) | 팔·다리 | [공식 소스] |
| Splint | **7** | 팔·다리 | [공식 소스] |
| Autoinjector(morphine/epinephrine/adenosine) | **5** | 팔·다리 | [공식 소스] |
| IV | **12** | | [공식 소스] |
| Stitch(Surgical Kit) | 상처당 **5** | 기본 시설에서만 | [공식 소스] |
| PAK | `max(10, 총 부위 피해×5(상한180)×timeCoefficientPAK)` | | [공식 소스] `fnc_getHealTime` |
| **CPR(ACE core)** | **15** (`treatmentTimeCPR`, 슬라이더 0.1–60) | **Body만**, 자기 대상 불가, `medicRequired = 0`(누구나), 아이템 불필요, 애니 `AinvPknlMstpSnonWnonDr_medic0` | [공식 소스] |
| Body bag / Grave | 15 / 30 | | [공식 소스] |
| **KAT AED 패드 부착** | **6** (`DefibrillatorPads_AttachTime`) / 제거 3 | Torso | [공식 소스 KAM] |
| **KAT Analyze Rhythm** | `5 max (random 8)` 초 (분석 중 접촉 감지 시 중단) | Examine 메뉴 | [공식 소스 KAM] `fnc_AED_Analyze` |
| **KAT 충전** | 약 `4.1 + 1.3` 초 후 "ready"(자동 충전), **20 초 안에 Administer Shock 안 누르면 자동 해제** | | [공식 소스 KAM] `fnc_AED_Charge` |

#### ACE-2.4 CPR/AED 입력 방식 비교

| | ACE3 core CPR | KAT(KAM) CPR | KAT AED |
|---|---|---|---|
| 시작 | 메뉴 `Advanced Treatments` → `CPR` | 같은 메뉴 → `CPR` (treatmentTime 0.01 s, 즉 바 없음) | `Advanced Treatments`→ `Place AED Pads` (바 6 s) → `Examine`→ `Analyze Rhythm` → (충전 완료 후) `Administer Shock` |
| 진행 | **15 s 진행바 1 라운드**, 라운드마다 확률 판정 | **연속 수행**: 메뉴가 닫히고 무릎 꿇는 루프 애니, 시작 약 2.1 s 뒤 `CPR Started`(1.5 s 텍스트) + **마우스 힌트 LMB `Stop CPR` / RMB `Change Monitoring Device`**, 첫 판정은 시작 후 `CPR_ChanceInterval + 2.5`초, 이후 `CPR_ChanceInterval`(기본 **15 s**)마다 판정 | 음성·효과음이 단계를 안내 |
| 중단 | `Esc`(진행바 다이얼로그가 닫히면 failure, errorCode 1) | **`Esc` 또는 LMB**; 환자가 깨어남/수행자 기절/거리>`maxDistance`/다이얼로그 열림 시 자동 종료, "CPR Cancelled" | 분석 중 CPR/BVM 접촉이 감지되면 "stopmotion" 음성 후 5 s 재시도, 지속되면 취소(`3beep`) |
| 완료 기록 | Activity log `%1 performed CPR` | `%1 performed CPR (%2)` — **경과 mm:ss 포함** | `attached defibrillator pads (AED)`, `administered shock (AED)` |

---

### ACE-3 조작 문법 (primitives)

1. **hold → hover → release 선택(인터랙션 메뉴).** 연속 입력(마우스 위치)이 "어떤 노드가 조준 중인가"로 이산화되고, 키 해제가 확정. 오조작 취소는 노드 밖에서 해제. 시간 압박이 없는 대신 *공간 위치*가 의미를 가짐(몸의 어느 부위에 호버했는가가 곧 `bodyPart` 인자). [공식 소스, 관찰]
2. **상태 조건 목록(condition-gated verb list).** 각 treatment는 `condition` 함수·`allowedSelections`·`items`·`medicRequired`·`treatmentLocations`·`allowSelfTreatment`로 가시성이 결정(`fnc_canTreat`). 합법인 모든 동사가 *옳든 그르든* 나열됨. 핵심 노브: `canCPR`는 `advancedDiagnose != 0`이면 **의식이 없는 모든 환자에게 CPR을 보여주고**, 0(basic)이면 **심정지일 때만 보여줌** — 소스 주석: "if basic diagnose, then only show action if appropriate (they can't tell difference between uncon/ca)". 즉 ACE는 "프롬프트가 정답일 때만 뜨는 설계"를 *가장 쉬운 보조 옵션*으로 두고, 기본값은 오답 선택지가 존재하는 쪽. [공식 소스]
3. **고정 시간 진행바(비신체).** 모든 치료는 `[_totalTime, args, onFinish, onFail, title, condition, exceptions]` 형태의 진행바(`common/fnc_progressBar`, 다이얼로그 방식). 연속 조작 없음: 플레이어 입력은 `Esc`(다이얼로그를 닫아 실패 처리)뿐이고, 그 외 취소는 수행자 사망·기절 등 `canInteractWith`(예외 `isNotInside, isNotSwimming, isNotInZeus`) 실패나 callbackProgress 거짓일 때. 제목 `Performing CPR...`에 기본으로 `Time left: Ns`(설정 `ace_common_progressBarInfo` 기본 2; 0=없음, 1=퍼센트). 진행바 위치 기본 Top. 코드 주석: 진행바는 "target fell unconscious, target died, target moved ... target moves at all"를 *검사하지 않음* — 대신 callbackProgress(`cprProgress`: 환자 각성/다른 CPR 제공자 존재 시 false)가 개별 취소 조건을 담당. [공식 소스]
4. **상태 피드백 채널.**
   - 동사 수행 중: 진행바 텍스트 + 시간, 수행자 애니메이션(무릎 꿇기), 3D 효과음.
   - 환자 상태(메뉴 내): 바디 도형 색(밝은 노랑→진한 빨강 = 출혈 속도, 진한 파랑 = 붕대, 연한 파랑 = 봉합, 뼈 오버레이 빨강 = 미치료 골절/파랑 = 부목, 토니켓 = 십자 주변 원), Overview 텍스트(`There is 1 Large Open Wound` 류, `[B]` 붕대, `[S]` 봉합), 상태 항목(`Lost a lot of blood`, `In severe pain`, `Tourniquet [CAT]`, `Fractured`, `No IV`), 수동 `Triage` 바(`None/Minimal/Delayed/Immediate/Deceased`).
   - 기록: `ACTIVITY LOG`(수행한 처치) / `QUICK VIEW`(검사 결과: `12:01 Shalazan checked Heart Rate: Normal`) / Triage card(사용 아이템 자동 누적 `%1x - %2 (%3m)`; 빈 경우 `No entries on this triage card.`). [공식 소스, 관찰]
   - **환자 본인(플레이어가 환자일 때)**: 통증=`White Flashing`(기본)/`Pulsing Blur`/`Chromatic Aberration`/`Only high pain effect` 중 선택, 저혈량=`Color Fading`(기본)/`Icon`/`Icon + Color Fading`, 출혈=화면 피 오버레이(출혈량 0~0.002에 비례한 페이드), 심박 <60 또는 >160이면 심박 효과음 반복(`60/HR`초 간격), 기절=블러+블랙아웃으로 서서히 암전 후 15–20 s마다 눈뜨는 연출, 골절/토니켓/부목 HUD 아이콘(`enableHUDIndicators` 기본 true), 비명 옵션. 이 중 수치 바는 **없음**(HP 바·체온 바 없음). [공식 소스, 공식 ACEREP #00010]
5. **제약.** `Holster Required`(`ace_medical_treatment_holsterRequired` 기본 0=Disabled; 1–4 = 무기 내림/집어넣기 요구, "Exam" 변종은 검진만 예외) → 손이 차 있으면 치료 불가; `treatmentLocations`(Anywhere / Vehicle / Medical Facilities / Vehicles & Facilities / Disabled); `Allow Shared Equipment`(환자 장비 우선 등); `medicRequired` 등급(Anyone/Medics/Doctors). 툴팁에 "in your inventory / in patient's inventory" 아이템 수 표시. [공식 소스, 관찰 kat-aed-a-t0015]
6. **병행 작업.** 설계 의도 "multiple people": 한 명이 CPR, 다른 이가 출혈·수액·검진 — ACEREP #00010 "designate multiple people to work on the patient. One being assigned to procuring CPR non-stop until vitals return". [공식]

---

### ACE-4 안내·교육 모델

- **인게임 튜토리얼 없음.** `addons/medical*`에 "tutorial" 문자열이 없음(grep 0건) [공식 소스 검증]. 학습은 공식 Wiki 문서·커뮤니티 영상·서버 교육에 의존. 문서의 치료 절차(구 v3.12.6 Advanced): "Step 1: Is the patient responsive? … Step 3: Does the patient have a pulse? **No:** If you are alone provide CPR…" [공식 구 문서] — 이 6단계는 **문서(외부)로 존재**하고 게임이 순서를 지시하지 않음.
- **"무엇이 잘못됐나"는 플레이어가 진단.** 게임은 환자 상태를 *직접 이름 붙여 말하지 않는 것*이 기본:
  - `Check Response` (advancedDiagnose=2 기본): 응답 없음 → `%1 is not responsive`, 심정지면 `%1 is not responsive, taking shallow gasps and convulsing`, 사망이면 `%1 is not responsive, motionless and cold`. 값 3("Enabled & Can Diagnose Death/Cardiac Arrest [Directly]")이면 `%1 is in cardiac arrest` / `%1 is dead` / `%1 is unconscious`로 *직접* 말해 줌.
  - `Check Pulse`: 기본 `numericalPulse`=Medics → 비의무병은 `You find a weak Heart Rate`(≤60) / `normal`(61–100) / `strong`(>100) / `no Heart Rate`(0); 의무병만 `You find a Heart Rate of %2` 수치.
  - CPR 중 맥박을 짚으면 가짜 심박 `random [25,30,35]`을 반환 → 영상 해설: "never check the pulse while CPR is in progress ... you will always find a pulse" [2차 자막 x1vZbZW0l88 t=11:04]; 소스 `checkPulseLocal`·`updateHeartRate`와 일치 [공식 소스].
  - 출혈 정도는 `Show Bleeding State` 설정(Disabled / Enabled / `Show Bleeding Rate`, 기본 2)에 따라 `Slow/Moderate/Severe/Massive bleeding`로 표시, `Show Blood Loss`(기본 true): `No blood loss / Lost some blood / Lost a lot of blood / Lost a large amount of blood / Lost a fatal amount of blood`. `Show Trauma Sustained`는 기본 false.
- **다이제틱 vs HUD.** 환자 정보(메뉴·툴팁)는 *근접(≤3 m)·검진 후*에만 의미 있고, 자신의 상태는 화면 효과·소리·HUD 아이콘으로 암시. 월드 위 마커·화살표 없음. 단, 출혈 흔적은 월드 오브젝트: `Blood Drops`(기본 Enabled) "this can be a good indicator for teams that somebody hasn't noticed they are bleeding", 최대 500개, 수명 15분 [공식 문서]. 환자 측 메시지 `%1 is treating you`(`medicalHintEnabled` 기본 1) [공식 소스].
- **지시형 요소는 의도적으로 최소.** 공식 문서가 노브로 노출하는 *안내 수준*:

| 설정(내부명) | 기본값 | 의미(공식 문구 인용) | 성격 [추론] |
|---|---|---|---|
| `ace_medical_treatment_advancedDiagnose` | **2** (0 Disabled / 1 Enabled / 2 "Enabled & Can Diagnose Death/Cardiac Arrest" / 3 "...[Directly]") | "Enables the Check Pulse, Check Blood Pressure, and Check Response treatment actions instead of the generic Diagnose action. When disabled, the CPR action will only be shown when performing CPR is appropriate. The actions this setting enables are needed to determine whether a person is unconscious or in cardiac arrest." | **보조(assist) ↔ 현실성** 핵심 노브 |
| `ace_medical_treatment_numericalPulse` | 1 (Anyone/**Medics**/Doctors) | "Makes Check Pulse action give a numerical value based on setting" | 보조 |
| `ace_medical_gui_showBleeding` | 2 (Disabled/Enabled/Show Bleeding Rate) | "Display if the patient is bleeding, optionally with rate" | 보조 |
| `ace_medical_gui_showBloodlossEntry` / `showDamageEntry` | true / false | 질적 혈액 손실 / 부위 외상 등급(`Minor/Major/Severe/Chronic Trauma`) 표시 | 보조 |
| `ace_medical_gui_interactionMenuShowTriage` | 1 (Disabled/Anyone/Medics) | "Shows the patient's triage level by changing the color of the main and medical menu actions." | 보조 |
| `ace_medical_gui_enableActions` / `enableSelfActions` / `enableMedicalMenu` | 0 "Selections (3D)" / true / 1 Enabled | 상호작용 UI 형태 | UI 취향 |
| `ace_medical_gui_openAfterTreatment` | true | "Reopen the Medical Menu after successful treatment." | 편의 |
| `ace_medical_gui_maxDistance` | 3 m (0–10) | 메뉴 가능 최대 거리 | 현실성 |
| `ace_medical_feedback_painEffectType` / `bloodVolumeEffectType` / `enableHUDIndicators` | White Flashing / Color Fading / true | 개인 취향(클라이언트 전용) | 취향 |
| `ace_medical_treatment_advancedBandages` | 1 (0 Disabled/1 Enabled/2 "Enabled & Can Reopen") | "Enables treatment actions for different bandage types instead of the generic Bandage action. Additionally, the reopening of bandaged wounds can also be enabled." | 현실성 |
| `ace_medical_treatment_advancedMedication` | true | "Enables extended, more in-depth medication handling. Also, enables the use of Adenosine." | 현실성 |
| `ace_medical_treatment_woundReopenChance` / `bandageEffectiveness` / `bandageRollover` | 1 / 1 / true | 재개방 계수 / 붕대 효율 / 한 붕대로 여러 상처 | 난이도 |
| `ace_medical_treatment_clearTrauma` | 1 (Never/After Stitch/After Bandage) | "Controls when hitpoint damage from wounds is healed." | 난이도 |
| `ace_medical_limping` / `fractures` / `fractureChance` | 1 (Limp on open wounds) / 1 (Splints Fully Heal) / 0.8 | 현실성 | 현실성 |
| `ace_medical_treatment_holsterRequired` | 0 | "Controls whether weapons must be holstered / lowered in order to perform medical actions." | 현실성 |
| `ace_medical_treatment_medicEpinephrine/Morphine/Adenosine/Splint` | 0 Anyone (PAK/Surgical Kit/IV는 1 Medics) | 시술 자격 | 현실성 |
| `ace_medical_treatment_allowSelfIV` / `allowSelfPAK` / `allowSelfStitch` | 1 Yes / 0 No / 0 No | 자기 시술 허용 | 현실성 |
| `ace_medical_treatment_allowSharedEquipment` | 0 ("Patient's Equipment First") | 장비 공유 | 현실성 |
| `ace_medical_treatment_locationPAK/SurgicalKit/Epinephrine/Morphine/IV` | 3 / 2 / 0 / 0 / 0 | 시술 가능 장소 | 현실성 |
| `ace_medical_bleedingCoefficient` / `painCoefficient` / `ivFlowRate` | 1 / 1 / 1 | "Coefficient for controlling the bleeding speed." 등 | 난이도 |
| `ace_medical_playerDamageThreshold` / `AIDamageThreshold` | 1 / 1 | "Sets the amount of damage a player can receive before going unconscious (and dying if 'Sum of Trauma' is enabled)." | 난이도 |
| `ace_medical_fatalDamageSource` | 2 ("Either"; 0 vital shots only / 1 sum of trauma) | 치명 피해원 | 난이도 |

  **프리셋(공식 문서 인용).** `"Basic" Preset`: `advancedBandages=0`, `advancedDiagnose=0`, `advancedMedication=false`, `fractures=0`, `limping=0`, `fatalInjuriesPlayer=2`(Never), `bleedingCoefficient=0.25`, `playerDamageThreshold=3.5`, `AIDamageThreshold=0.2`, `fatalDamageSource=1`. `"Advanced" Preset`: `advancedBandages=2`, `advancedDiagnose=1`, `advancedMedication=true`, `fractures=1`, `limping=1`, `fatalInjuriesPlayer=1`(In Cardiac Arrest), `spontaneousWakeUpChance=0.15`, `allowSelfIV/PAK/Stitch=1`, `clearTraumaAfterBandage=true`, `cprSuccessChance=0.4`. `Co-Op Preset 1`: `bleedingCoefficient=0.25`, `spontaneousWakeUpChance=0.85`("Stabilised players will wake up fast"), `spontaneousWakeUpEpinephrineBoost=3`, `cardiacArrestTime=630`. `PvP Preset 1`: `cardiacArrestTime=300`("players should die fast once they are down and not treated"), `spontaneousWakeUpChance=0.15`. [공식 문서] → **난이도는 "기능을 끄는 것"이 아니라 "얼마나 직접 말해 주느냐 + 얼마나 느리게 죽느냐"의 조합으로 조절.**
- **구 Basic vs Advanced 목록(v3.12.6, 재작성 전)** [공식 구 문서]:
  - Basic: 상처 2종(Yellow 소·중 / Red 대), 모든 붕대 동일, Tourniquet·Saline·Plasma 무용, Blood IV만 혈액 회복, Morphine=통증 제거, Epinephrine=각성, **vitals/심정지/CPR 없음**, 치료 5단계(응답→상처→통증→혈액→Epinephrine). 비의무병은 Epinephrine·IV 불가, 시간 더 걸림.
  - Advanced(CSE 계승): 상처 8종(Abrasion/Avulsion/Contusion/Crush/Cut/Laceration/Velocity/Puncture), 정확한 출혈량, **vitals(심박·혈압)**, **Cardiac Arrest**, CPR·IV 3종·토니켓, 약물 시뮬, 재개방/봉합, PAK. 심정지 진입 조건(구): HR<20, HR>200, 수축기>260 등.
  - 재작성(3.13.0) 이후: "removing the concept of 'Basic vs Advanced Medical'" — 위 "설정 표"가 그 대체물.
- **지시 vs 진단 요약.** 게임이 *말하는* 것: 검진 결과 문장, 도구/바디 색, 수동 트리아지. 게임이 *말하지 않는* 것: 다음에 무엇을 하라, 심정지라는 이름(기본값 2에서는 "convulsing" 서술만), 사망 임박 타이머. 음성 지시는 KAT AED 장치(§5.3)뿐이고 그것도 장치의 diegetic 음성이다.

---

### ACE-5 실패·결과 모델

#### ACE-5.1 상태기계(`ACE_Medical_StateMachine`, 소스 `Statemachine.hpp`)

`Default → Injured → Unconscious → CardiacArrest → Dead` + 임시 `FatalInjury`.

| 전이 | 조건/이벤트 |
|---|---|
| Default→Injured | `injured`, `LoweredVitals` |
| →Unconscious | `CriticalInjury`, `CriticalVitals`, `knockOut` (과다 통증·출혈·약물 과다(`overDose` → `CriticalVitals`)) |
| →CardiacArrest | `FatalVitals`, `Bleedout` |
| →FatalInjury | 머리/몸통 치명 피해 → 즉시 같은 프레임에 `SecondChance`(조건 `conditionSecondChance`: `fatalInjuriesPlayer != Always` → CardiacArrest) 또는 `Death`. 설정 `ace_medical_statemachine_fatalInjuriesPlayer` 기본 **0 "Always"**(= 즉사 가능), 1 "In Cardiac Arrest", 2 "Never" |
| Unconscious→Injured(WakeUp) | `hasStableVitals` 참 + `WakeUp` 이벤트 |
| CardiacArrest→Unconscious(`Reanimation`) | `CPRSucceeded` 이벤트(= CPR 성공 또는 AED 성공) |
| CardiacArrest→Dead | `Timeout`(타이머 ≤ 0), `Bleedout`(`cardiacArrestBleedoutEnabled` 기본 true), `Execution`(재차 치명 피해) |

- `hasStableVitals` 조건: 혈액량 ≥ **5.1 L**(class II 하한), 심정지 아님, 출혈량 ≤ `BLOOD_LOSS_KNOCK_OUT_THRESHOLD × 심박출/2`, 혈압 L ≥ 50 & H ≥ 60, HR ≥ 40. [공식 소스 `fnc_hasStableVitals`]
- **깨어남 판정**: 안정 상태일 때 **15 초마다**(`const_wakeUpCheckInterval` 15) `spontaneousWakeUpChance` 확률(기본 **0.10**, 문서는 5% — §9)로 각성. Epinephrine이 체내에 있으면 점검 주기를 `spontaneousWakeUpEpinephrineBoost`(기본 1.5)로 나눔("the time between spontaneous wake up checks is divided by this value"). 불안정하면 판정 연기. [공식 소스 `handleStateUnconscious`]

#### ACE-5.2 심정지(Cardiac Arrest)와 CPR — 정확한 규칙

| 항목 | 값/규칙 | 증거 |
|---|---|---|
| 타이머 | `ace_medical_statemachine_cardiacArrestTime` 기본 **300 s**(슬라이더 1–3600), 진입 시 **±10% 랜덤**(`_time + _time*random[-0.1,0,0.1]`)로 `cardiacArrestTimeLeft` 설정. 문서: "Once a patient's heart has stopped this begins a 5 minute countdown" | [공식 소스 `enteredStateCardiacArrest`; 문서] |
| 틱 | ≥1 s 마다 `_timeDiff`(최대 10) 차감, **CPR 제공자가 살아 있으면 차감 ×0.5**("if being cpr'ed, then time decrease is reduced") | [공식 소스 `handleStateCardiacArrest`] |
| 타이머 노출 | **플레이어에게 표시되지 않음**(UI 코드 없음; 영상 해설에서도 "whatever the cardiac arrest timer is, so in this server it's five minutes" — 서버 설정을 아는 경우만 추정) | [공식 소스 검색 + 2차 자막 x1vZbZW0l88 t=10:27] |
| CPR 라운드 | 진행바 **15 s**(`treatmentTimeCPR`) 종료 시 `cprSuccess`→(환자 생존 & 심정지일 때만) `cprLocal` | [공식 소스] |
| **성공 확률** | `p = linearConversion[ 3.6 L, 5.1 L, 혈액량, cprSuccessChanceMin, cprSuccessChanceMax, clamp ]`. 기본 `cprSuccessChanceMin = cprSuccessChanceMax = 0.4` → **혈액량과 무관하게 40 %/라운드**. 의미(공식 문구): Max = "used when the patient has at most 'Lost some blood'", Min = "used when the patient has at least 'Lost a fatal amount of blood'". 보조: `if (random 1) < p` | [공식 소스 `fnc_cprLocal`, stringtable] |
| 성공 효과 | `CPRSucceeded` 이벤트 → 상태 `CardiacArrest→Unconscious`(**깨어나는 것이 아니라 심장이 다시 뜀**). SpO2가 48.5(=DEFAULT_SPO2/2=97/2) 미만이면 48.5로 끌어올려 즉시 재정지 방지(`setSpO2UponCPRSuccess` 기본 true) | [공식 소스, 문서] |
| 실패 효과 | 아무 일도 없음(타이머만 반감 속도로 흐름). 라운드 반복. 비심정지·사망 대상에 CPR은 15 s 소비만 하고 효과 없음 | [공식 소스 `cprSuccess`: "not alive or in cardiac arrest" 분기] |
| CPR 중 생체 신호 | 심박 = `random[25,30,35]`(가짜), 제공자가 사라지면 0 | [공식 소스 `updateHeartRate`] |
| 사용자 서술 | "CPR ... until pulse check says heart rate normal" 순환: CPR→Check Pulse→CPR (영상 해설) | [2차 자막 x1vZbZW0l88 t=10:39–11:20] |
| 자격 | `medicRequired = 0`(누구나), `allowSelfTreatment = 0`, `treatmentLocations = ALL` | [공식 소스] |

**KAT(KAM) 변종**: CPR 확률이 *훈련 등급별* min–max(혈액량 보간): `CPR_MinChance/MaxChance` Default **10/20 %**, RegularMedic **15/30 %**, Doctor **20/40 %**; 판정 주기 `CPR_ChanceInterval` **15 s**; `AdvRhythm_CPR_ROSC_Chance` 5 %; 심장 리듬 4종(VT/VF/PEA/Asystole)이 시간에 따라 악화(`deteriorateTimeMax` 900 s, `deteriorateTimeWeight` 180 s), "Each successful AED shock/CPR brings the rhythm up by one 'level'", "Timer till death slows down by 50% while CPR is conducted". 혈액 "Lost a Fatal Amount of Blood"는 곧바로 Asystole(쇼크 불가). [공식 문서 KAM `01_cardiac_arrest.md`, `02_aed_manual.md` + 소스]

#### ACE-5.3 AED(KAT) 절차 — 문서와 코드 대조

문서 4단계(공식 KAM): ① Preparation "Ensure nobody touches the patient (no CPR or BVM should be performed during analysis)." ② Apply Pads "Click on the patient's chest and select **Attach AED pads** in the *Advanced Treatment* section." ③ Analyze Rhythm "Select **Analyze Rhythm** in the *Examine Patient* section." → **"Shock Advised"**: "The AED will charge automatically and give a sound notification when ready. It is then possible to perform a shock … The AED will disarm itself automatically if no shock is administered within a certain timeframe." / **"No Shock Advised"**: "Perform CPR, administer Epinephrine (optional/not essential), and check the pulse every 2 minutes." ④ Re-evaluate "after administering the shock or after 2 minutes of CPR".

코드(소스 `fnc_AED_Analyze/Charge/Shock`): 음성 파일 `analyzingnow`(분석 시작) → (5~8 s) → `shockadvised` 또는 `noshockadvised` → shock이면 1.7 s 뒤 `charging`(4.1 s) → `standclear_pushtoshock`(1.3 s 후 `Defibrillator_Charged = true`, 0.528 s 간격 `alarm` 반복) → 20 s 내 Administer Shock 없으면 `3beep` 자동 해제. 충격 시 환자 근처 플레이어에게 통증 이벤트, 기록 `administered shock (AED)`, 2 s 뒤 `pushanalyze` 음성으로 재분석 유도. 성공률(리듬 비활성 시) AED `AED_MinChance/MaxChance` 기본 **45/80 %**, AED-X **50/90 %**, `AdvRhythm_AED_ROSC_Chance` 50 %. 메뉴 항목 `Place AED Pads`(아이템 `kat_AED` 필요, 시설용 `AEDStationPlacePads`는 아이템 불필요), `Remove Defibrillator Pads`, `Administer Shock`(충전 완료 때만 표시), `Cancel Charge`; 거리 제한 `Defibrillator_DistanceLimit` 기본 6 m; 사용 자격 `medLvl_AED` 기본 0 Anyone, 위치 `useLocation_AED` 기본 0 Anywhere; "Hardcore" 옵션(`AdvRhythm_Hardcore_Enable` 기본 false): 오사용 시 리듬 악화. [공식 소스 KAM]

**ACE3 core에는 AED/제세동기가 없음**: `addons/`·`docs/wiki/` 전체에 대해 "defib|aed|automated external" 대소문자 무시 grep 결과 0건 [공식 소스 검증]. 과제문의 'Use AED-X'/`ace_medical_treatment_convertItems`는 core가 아니라 KAT 쪽 개념이고, `convertItems`는 core에서 *바닐라 구급 아이템 변환*(기본 0 Enabled)일 뿐 AED와 무관.

#### ACE-5.4 그 밖의 지연 실패·결과
- **출혈 → 저혈량 → 기절/심정지**: 혈액 6.0 L 기준 class I(<15 %) / II 5.1 L / III 4.2 L / IV 3.6 L / 치명 3.0 L. 출혈은 지속 감소(`bleedingCoefficient`). 토니켓 장시간 방치 시 통증. 붕대는 `advancedBandages=2`에서 재개방 확률(FieldDressing 기본 `reopeningChance 0.1`, 지연 120–200 s 등) → 재출혈 → 주기적 재확인 필요. [공식 소스 `ACE_Medical_Treatment.hpp`]
- **통증→심박 상승**: 통증이 0.2 초과면 목표 심박 `80 + 50×통증`, 심한 통증+출혈은 극단적 심박→심정지 위험(ACEREP #00010 "Severe amounts of pain and blood loss can cause the heart rate to reach extremely high values"). 대응: Adenosine·Morphine.
- **오답 시술의 결과**: 약물 과다(`maxDose` 초과) → `CriticalVitals`(기절); KAT 혈액형 불일치 수혈 → `wrongBloodTreatment`; KAT AED hardcore 오사용 → 리듬 악화. 그러나 *CPR을 비심정지에 하는 것*은 15 s 낭비 외 페널티 없음.
- **패널티/점수/게임오버**: 없음. 플레이어(환자)가 죽으면 리스폰/관전은 미션 규칙. [공식 소스 — 득점 코드 grep 0건]

---

### ACE-6 평가 모델

- **ACE3/KAT에는 점수·등급·합격선·교대 종료 보고서가 없다.** `addons/medical*`에서 score/debrief/scoreboard 문자열 0건. 평가 수단은 **감사 기록**으로만 존재:
  - `ACTIVITY LOG`: 수행자 이름 + 행위(`%1 has bandaged patient`, `%1 applied a tourniquet`, `%1 performed CPR`, `%1 used Personal Aid Kit`, `%1 has given an IV`, `%1 used %2`).
  - `QUICK VIEW`: 검사 결과(`checked Heart Rate: None`, `Pulse Oximeter: PR: 94 SpO2: 99` 등) — 타임스탬프 `HH:MM`(미션 시계).
  - `Triage card`: 사용 아이템 자동 누적 `1x - Tourniquet (2m)`.
  - 수동 트리아지 4단계: `Minimal`(초록 0,0.5,0) / `Delayed`(노랑 1,0.84,0, 글자 검정) / `Immediate`(빨강) / `Deceased`(검정), 미지정 `None`.
- 이 기록은 *플레이어에게 점수가 되지 않으나* **다음 처치자(인수인계)가 읽는 정보원**이라는 점이 핵심: 환자 메뉴를 연 다른 의무병에게 그대로 노출된다(같은 변수 `ace_medical_triageCard`, 로그 변수 글로벌 동기화). [공식 소스]

---

### ACE-7 관찰 기록 (영상 프레임)

모든 파일: `/Users/um-yunsang/CHOOGuard/.planning/2026-10-04-sim-controls-benchmark/media/C/` (git-ignored). `t=`는 *클립 내* 시간, 괄호는 원본 영상 시각. 원본은 480p 이하(640×360) 다운로드 구간이라 소형 글자는 4배 업스케일 크롭.

| # | URL | t | 프레임이 보여주는 것 | 파일 |
|---|---|---|---|---|
| 1 | https://www.youtube.com/watch?v=hD1cAe71GIw (LeonGremory, ACE3 Interaction MADE SIMPLE, 2020-09-05) | 0:48 (≈05:13) | 연료 드럼 위에서 `⊞ Win` 유지: 중심 노드 `Interactions`, 자식 노드 `Refuel`(호버 중, 빨간 선택 링), `Check remaining fuel`, `Check fuel counter`, `Take fuel nozzle`. 커서가 풀려 있음 | `ace3-interact-t0048-full.png` |
| 2 | 동일 | 1:10 (≈05:35) | 설정 팝업 `Do action when releasing menu key:` 체크박스 — 키 해제=실행 옵션. 해설자가 "if you have that enabled what you simply do is just to let go your windows key" | `ace3-interact-t0110-full.png` |
| 3 | https://www.youtube.com/watch?v=GbNw6IoZh_M (Kadava, 2021-03-12) | 0:40 (≈01:30) | 다른 유닛 몸에 3D 선택 노드(십자): `Torso`, `Left Arm`, `Left Leg`, `Right Leg` 등 + 메인 `Medical Menu`, `Interactions`(링), `Join group Alpha 1-3`, `Pass magazine` | `ace3-medinteract-t0040-sel3d-crop.png` |
| 4 | https://www.youtube.com/watch?v=x1vZbZW0l88 (Shalazan, ACE Medical Basics, 2022-09-15) | 0:24 (≈05:09) | `H`로 연 자기 의료 메뉴: 제목줄에 환자 이름, 열 머리글 `EXAMINE & TREATMENT / STATUS / OVERVIEW`, 카테고리 아이콘 줄, 좌측 `No entries on this triage card.`, 가운데 바디 도형 + `None`(트리아지 바), 우측 `Head / No injuries on this bodypart.`, 하단 `ACTIVITY LOG` / `QUICK VIEW` | `ace3-medmenu-t0024-crop.png` |
| 5 | 동일 | 1:06 (≈05:51) | Examine 카테고리 + 머리 선택: 행동 `Check Pulse` 한 줄 | `ace3-medmenu-t0106-full.png` |
| 6 | 동일 | 1:16 (≈06:01) | Check Pulse 수행 후 메뉴가 자동 재오픈, `QUICK VIEW`에 `12:01 Shalazan checked Heart Rate: Normal` | `ace3-medmenu-t0116-full.png` |
| 7 | 동일 | 1:26 (≈06:11) | 팔 선택: 행동 `Check Pulse`, `Check Blood Pressure`(팔·다리에서만 노출) | `ace3-medmenu-t0126-full.png` |
| 8 | https://www.youtube.com/watch?v=x1vZbZW0l88 | 0:23 (≈10:38, CPR 클립) | 환자 `Max Wilson`: Advanced Treatments에 항목 **`CPR` 하나뿐**. Overview `Torso / Lost a lot of blood / [B] 1x Large Scrape`, 바디 도형 파랑(붕대)+토니켓 원, `ACTIVITY LOG` `12:13 Shalazan applied a tourniquet` ×4, `12:14 Shalazan has bandaged patient`, `QUICK VIEW` `12:12/12:14 Shalazan checked Heart Rate: None` | `ace3-cpr-t0023-menu-crop.png` |
| 9 | 동일 | 0:28 (≈10:43) | **CPR 진행바**: 화면 상단 중앙 `Performing CPR... Time left: 10s`(앰버 채움), 메뉴 닫힘, 우상단 토스트 `New Patient incoming, good luck!`(서버 메시지) — 15 s 바의 중간 | `ace3-cpr-t0028-full.png` |
| 10 | 동일 | 0:38 (≈10:53) | 한 라운드 후 메뉴 재오픈, 툴팁 `Advanced Treatments`, `Check Pulse` 항목, `QUICK VIEW`에 `Heart Rate: None` 3회(12:12/12:14/12:15) — "CPR → 맥박 확인" 순환 | `ace3-cpr-t0038-menu-crop.png` |
| 11 | https://www.youtube.com/watch?v=tH15xkWFTr8 (Polarwhisper6, KAT medical tutorial, 2024-09-04) | 0:15 (≈1:06:15, 클립 a) | KAT Advanced Treatments 목록 `Place body in black bodybag / CPR / Place AED Pads / Establish FAST IO`; 호버 툴팁 `1 in your inventory / 1 in patient's inventory` | `kat-aed-a-t0015-pads-hover-crop.png` |
| 12 | 동일 | 0:27 (≈1:06:27, 클립 a) | AED 패드 부착 진행바(상단, 글자 일부만 판독: `Placing Defibrillator Pads …`; 소스 문자열 `Placing Defibrillator Pads`로 대조), 시점이 환자에게 숙인 1인칭 | `kat-aed-a-t0027-progress-full.png` |
| 13 | 동일 | 0:33 (≈1:06:33, 클립 a) | 부착 후 목록이 `Place AED Pads` → `Remove Defibrillator Pads`로 바뀌고, 바디 도형에 파란 패드·케이블 표시, 호버 툴팁 `Select Torso` | `kat-aed-a-t0033-menu-crop.png` |
| 14 | 동일 | 1:15 (≈1:07:15, 클립 a) | Examine 카테고리에 **`Check Pulse`, `Analyze Rhythm`** 두 행동; Overview `No bleeding / Lost a lot of blood / No IV / Torso / No injuries on this bodypart.` | `kat-aed-a-t0115-analyze-crop.png` |
| 15 | 동일 | 0:45 (≈1:09:05, 클립 b `kat-aed-polarwhisper-010820-010940`) | 충전 후 Advanced Treatments에 **`Administer Shock`(하이라이트), `Cancel Charge`** 추가(충전 전엔 없음) | `kat-aed-b-t0045-shock-crop.png` |
| 16 | 동일 | 0:57 (≈1:09:17, 클립 b) | `ACTIVITY LOG`: `15:37 Instructor performed CPR (…)`(경과 시간 병기), `15:46 PolarWhisper6 attached defibrillator pads (AED)`, `16:07 PolarWhisper6 administered shock (AED)`; `QUICK VIEW`: `16:08 Pulse Oximeter: PR: 94 SpO2: 99` | `kat-aed-b-t0057-log-crop.png` |
| 17 | https://www.youtube.com/watch?v=YexB3e0SsKQ (BipolarBear, 2019-03-11, 구 KAT) | 0:12 (≈01:27) | 구 KAT에서는 Advanced Treatments에 `Use Surgical Kit / CPR / Automated External Defibrillator(s)`(AED 단일 항목) | `kat-aed-old2019-t0012-crop.png` |

원본 클립(웹엠): `ace3-interact-leongremory-0425-0550.webm`, `ace3-medinteract-kadava-0050-0210.webm`, `ace3-medmenu-shalazan-0445-0615.webm`, `ace3-cpr-shalazan-1015-1125.webm`, `kat-aed-polarwhisper-010600-010730.webm`, `kat-aed-polarwhisper-010820-010940.webm`, `kat-aed-bipolarbear-0115-0210.webm`.

---

### ACE-8 CHOOGuard 적용 표

(CHOOGuard 맥락: 역무원=훈련받은 일반인 응급처치자. 119 신고·소화기·AED+CPR는 허용, 약물·삽관·봉합·수액 등 의료인 시술은 금지. 모든 메커니즘은 위험 종류와 무관하게 동작해야 하고, 플레이어가 지각하지 못한 위험·부상자는 드러내지 않는다.)

| 판정 | 기제 | CHOOGuard 동사 | 구체 방법(입력·피드백·실패) | 근거(tag) |
|---|---|---|---|---|
| **Adapt** | hold→hover→release 선택 메뉴 | 부상자·설비 대상의 *부위/구성요소별* 행동 선택, 상태 확인, 요청 | 현행 `E` 탭=단일 행동은 유지. **`E` 길게(≈0.3 s) → 대상 주위 3D 노드가 펼쳐지고 마우스로 조준, `E` 해제=실행**(해제 실행 on/off 옵션 병기). 노드는 *이미 지각한* 부위만(시야 내 확인한 부상 부위, 점검으로 확인한 구성요소). 오조작 취소=빈 곳에서 해제. 안내문 대신 *노드 이름만*이 보이고 "정답 표시"는 없음 | [공식] interaction 문서 + `fnc_keyUp` `actionOnKeyRelease` 기본 true; [관찰 hD1cAe71GIw t=05:13, 05:35; GbNw6IoZh_M t=01:30] |
| **Adopt** | *검진 결과는 서술 문장*, 진단 이름은 플레이어 몫 | 부상자 의식·호흡·맥박 확인 → 119/무전 보고 내용 결정 | 부위별 검진 행동(각 2.5 s 진행바). 결과는 관찰 문장만("반응 없음, 숨을 거의 못 쉬며 경련") — `심정지` 라벨 금지(기본). 가이드 수준 노브: 서술형(기본) / 직접 진단형(쉬움). 한국 일반인 BLS 범위(의식·호흡 확인)와 부합. 어떤 위험(연기·낙상·감전)에서 생긴 부상자든 동일 검진 | [공식 소스] `fnc_checkResponse`(`%1 is not responsive, taking shallow gasps and convulsing`), `advancedDiagnose` 0–3; [관찰 x1vZbZW0l88 t=05:51–06:11] |
| **Adapt** | 보이는 동사가 정답일 때만(basic) vs 의식 없으면 모두 표시(advanced) | CPR/AED 시작 가능 여부 | CHOOGuard 현행은 basic에 해당. 제안: **"의식 없음"이면 CPR 동사는 항상 노출**(호흡이 정상인 환자에게도). 오답 비용은 *시간 소모*(15 s)와 *핵심 순서 지연*(AED 도착·119 보고 지연)뿐, 위해 없음. 쉬운 난이도에서만 "정답일 때만 표시" | [공식 소스] `fnc_canCPR` 주석 "if basic diagnose, then only show action if appropriate", `fnc_cprSuccess` 비심정지 분기; 문서 `Advanced Diagnose` 설명 |
| **Adapt** | CPR = 고정 시간 라운드 + 확률 + 도착 전까지 *시간 늦추기* | 가슴압박(심정지 대응) | 일반인 CPR은 병원 외 ROSC가 낮으므로 *ROSC보다 "사망 타이머 반감"*을 주 효과로. 입력: 메뉴 선택 후 **연속 수행(LMB 유지 or 진행바 15 s 라운드, `Esc`/이동 시 중단)**, 수행 중 환자 사망 타이머 감소 ×0.5(ACE 규칙 인용), 라운드마다 낮은 확률(KAT 비훈련 기본 10–20 %/15 s 참고, ACE core 40 %는 의무병 가정이라 과함 [추론])로 자발 순환 회복. 손에 든 소화기 등은 먼저 `G`로 내려놓아야 함(Holster Required 유사) | [공식 소스] `handleStateCardiacArrest`(×0.5), `fnc_cprLocal`, KAM `CPR_Min/MaxChance_Default` 10/20, `CPR_ChanceInterval` 15 s; ACE `holsterRequired` |
| **Adopt** | 사망 타이머는 *숨김*, 단서만 노출 | 부상자 악화·사망 | 환자별 숨은 시계(예: 심정지 진입 300 s ±10 % 샘플)와 출혈 감소. UI에 숫자 없음. 단서: "맥박 없음", 안색/출혈 서술, 호흡 소리. 사망 시 즉시 게임오버 없이 *결과 기록*(교대 종료 평가로 연결). 위험 종류와 무관(연기 흡입·압궤·감전 모두 같은 상태기계 입력) | [공식 소스] `enteredStateCardiacArrest`, UI에 타이머 없음(검색), 문서 "5 minute countdown"; [관찰 ace3-cpr t=0:23 `Lost a lot of blood`] |
| **Adopt** | AED = 장치가 지시하는 diegetic 절차 | AED 패드 부착·분석·쇼크 | 순서: AED 위치(역사 비치 설비—위치 표지는 설비이므로 위험 공개 아님)에서 `E`로 가져오기 → 가슴 노드에서 **패드 부착(6 s 바)** → `분석`(5–8 s, **이 동안 환자 접촉(CPR) 시 "움직임 감지" 음성과 함께 지연/취소**) → 장치 음성 "충격이 필요합니다/필요하지 않습니다" → 충전 후 **20 s 내 `충격` 버튼**(미실행 시 자동 해제) → 재분석 유도. 장치 *음성*이 유일한 지시이며 한국 공공 AED 음성과 일치하는 일반인 절차. 마지막 충격 후 CPR 재개를 *스스로* 판단 | [공식] KAM `02_aed_manual.md` ①–④; [공식 소스] `fnc_AED_Analyze/Charge/Shock`; [관찰 kat-aed-a-t0015, t0033, t0115; kat-aed-b-t0045, t0057] |
| **Adapt** | 인수인계 카드·활동 로그 | 도착한 기관(119)에게 인수인계, 무전 보고 | 플레이어가 수행·관찰한 것이 **타임스탬프 기록**(`12:14 맥박 확인: 없음`, `12:15 CPR 시작 (경과 mm:ss)`, `AED 충격`)으로 자동 누적. 119 도착 시 이 카드가 인수인계 내용이 되고, **무전 보고 문장도 이 기록에서 고른다**(사무실이 다음 지시를 내리지 않음). 수동 트리아지 태그(Minimal/Delayed/Immediate/Deceased)는 역무원 범위 밖이므로 **"의식 있음/없음, 호흡 있음/없음" 단순 태그**로 축소 | [공식 소스] `ACTIVITY LOG`/`QUICK VIEW`/Triage card(`%1x - %2 (%3m)`); [관찰 ace3-cpr t=0:23; kat-aed-b t=0:57] |
| **Adapt** | 가이던스 노브 묶음(보조 vs 현실성 분리) | 난이도 설정 | 보조 노브=*"무엇을 말해 주는가"*(직접 진단, 수치 vs 질적, 경로 안내, 무전 지시)만. 현실성 노브=*"얼마나 빨리 죽는가·무엇이 필요한가"*(타이머 길이, 도구 필요, 손 비우기). 공식 프리셋 방식으로 3묶음(쉬움/표준/실전) 제공. 절대 *행동 가능 범위 확장*(약물 등)은 노브로 두지 않음 | [공식] 재작성 포스트 "removing the concept of Basic vs Advanced ... set of settings", 문서 프리셋 3종 |
| **Adopt** | 월드가 흐르는 비일시정지 메뉴 + 거리 이탈 자동 종료 | 환자 곁 선택 UI | 노드 UI/상태 패널은 *시간을 멈추지 않음*, 환자가 3 m 밖이면 닫힘(`maxDistance`). 처치 중 연기·열 위험은 계속 진행 → 플레이어 위험 판단이 곧 선택 비용 | [공식 소스] `fnc_menuPFH`, `maxDistance` 기본 3 |
| **Adopt** | 시야 기반 흔적(출혈 자국) | 미보고 부상자 발견 | 부상자가 *남긴 흔적*(핏자국·그을음 발자국)을 월드 오브젝트로(시야에 들어와야 인지). 보이지 않으면 힌트 없음 → "지각하지 않은 위험 비공개" 준수 | [공식] 문서 `Blood Drops` "good indicator for teams that somebody hasn't noticed they are bleeding" |
| **Adapt** | 본인 상태는 화면 효과·소리, 수치 바 없음 | 플레이어 위험(연기·열) | 연기 농도→기침 소리·시야 가장자리 백색 번쩍임/색 바램, 열→맥동 블러+심박음(HR>160 효과음 방식), 극한에서 서서히 암전 후 기절(15–20 s마다 눈뜨는 연출). HUD 바 없음. 기절 시 열린 메뉴 강제 종료 | [공식 소스] `fnc_effectPain/effectBloodVolume/effectHeartBeat/effectUnconscious`, `ace_unconscious` 핸들러 |
| **Reject** | 의료인 시술(IV, 약물, 봉합 Surgical Kit, PAK, 삽관/기도 확보, FAST IO) | — | 일반 역무원 권한 밖. 구현 제외. 지혈은 *직접 압박/붕대* 수준만(토니켓은 KORAIL 비치·교육 여부 미확인이라 보류 [추론]) | [공식 소스] `ACE_Medical_Treatment_Actions.hpp` 목록; 하드 제약 |
| **Reject** | 의료 등급(Anyone/Medics/Doctors)별 성공률 차등 | — | 역무원 모두 *일반인 등급* 단일. 등급 노브 불필요 | [공식 소스] KAM `CPR_*_Default/RegularMedic/Doctor`, ACE `medicRequired` |
| **Reject** | 수동 4단계 트리아지 색 표(Minimal/Delayed/Immediate/Deceased) 전체 | — | 일반 역무원 업무 아님(다수 사상자 트리아지는 소방/구급 권한). 위 "의식/호흡" 단순 태그로 대체 | [공식 소스] `fnc_getTriageStatus`; 하드 제약 |

---

### ACE-9 미확인·한계

- **문서와 소스 불일치(두 값 병기)**: (a) 공식 문서 "CPR Success Chance … By default this is set to 40%" vs 소스는 `cprSuccessChanceMin/Max` 두 슬라이더(둘 다 기본 0.4)이며 문서 프리셋은 폐기된 `ace_medical_treatment_cprSuccessChance` 이름을 사용. (b) 문서 "By default … 5%" 깨어남 확률 vs 소스 기본 `spontaneousWakeUpChance = 0.1`. (c) 문서 "Advanced Diagnose: Checkbox" vs 소스 LIST 0–3 기본 2. (d) 문서 `Fatal Damage Source` 기본 불명 vs 소스 기본 2 "Either". → 본 보고서는 **소스(3.21.2.113) 값을 우선**.
- CPR min/max 보간이 처음 들어간 정확한 ACE3 릴리스 번호는 미확인(PR #7983은 2020-11 개설, 2021-07 갱신; 얕은(--depth 1) 클론이라 blame 불가). `medical-treatment-framework` 문서가 3.14.2에서 추가되었으므로 3.14.x 전후로만 추정.
- **영상은 모두 2차**. 2022–2024 영상은 3.13 재작성 이후 UI라 일치하나, Kadava(2021-03-12)·LeonGremory(2020-09-05) 영상의 정확한 ACE 버전은 확인 못 함(둘 다 재작성 이후 시기). 3D 노드 UI는 영상 1건만 확인(관찰 #3).
- AED 영상: KAT 2024 튜토리얼에서 *분석 결과 음성*(`shock advised`)과 충전 음 구간은 해설 자막으로만 확인했고 프레임으로는 `Administer Shock` 항목 등장과 로그 기록을 확인(음성 자체는 영상 오디오 미청취). 진행바 글자는 640×360 압축으로 일부만 판독. CPR 로그의 경과 시간 숫자(`(…)`)는 판독 불가.
- KAT 쪽 문서는 KAM `dev-Tomcat` 브랜치 사본 기준(2026-09-20). 과제문에 있던 `Advanced-Combat-Medicine/KAT-Medical`은 404.
- ACE3 인터랙션 메뉴의 **라디얼 vs 목록(`useListMenu` 기본 true) 표시 차이**, 메뉴 애니 속도 등은 소스 설정값만 확인, 영상 비교는 안 함.
- 한국 역무원 교육 범위(토니켓·압박지혈 포함 여부, KORAIL 비치 AED 음성 문구)는 본 레인 범위 밖이라 검증 못 함 — 적용 표의 "한국 AED 음성과 일치", "토니켓 보류"는 **[추론]**.
- "Hold-E 길이 0.3 s", "사망 타이머 ×0.5", "일반인 CPR 확률" 등 CHOOGuard 쪽 수치는 ACE/KAT 값에서 가져온 *제안*일 뿐 실측·플레이테스트 근거 없음.


---

## 5. 교차 결론 — 4종이 공유하는 패턴과 CHOOGuard에 주는 시사

| # | 패턴 | 관찰한 곳(증거) | CHOOGuard 시사 |
|---|---|---|---|
| X1 | **단서를 주고 *진단은 플레이어*** | Ambulance Life `Category`+`Interim Diagnosis` 선택(태블릿), `New Evidence` 칩 [관찰 H90F5LHg2AI t≈4:57, s_H7wv4PrSs t≈2:17]; PS:PO Intuition 텍스트(나침반 아래)와 방사형 전체 목록 [공식 핸드북, 관찰]; ACE3 검진 문장(`is not responsive, taking shallow gasps and convulsing`) [공식 소스]; Ignite 문 아래 연기·쿵쿵 소리·균열 [2차 F10] | 현 상황판 "조치 ○/●"와 `주변 접근 통제` 같은 *정답 이름 프롬프트*를 보조 모드로 격하. 기본은 *관찰 문장*(색 채널 포함)만 제공하고 보고 내용·방송·기관 요청은 플레이어가 고른다 |
| X2 | **오답이 목록에 있고 실행되며, 결과는 세계 반응 + 사후 한 줄** | PS:PO `Justified:` 줄+`−5 +0` 행 [관찰 p46792Lsswo 23:10, hGoCvVJomPE 8:24]; Ambulance Life 도구 툴팁 `USED FOR`/`MAY CAUSE`, CPR 오용 경고 [관찰]; ACE3 `cprSuccess` 비심정지 분기(15 s 소비만) [공식 소스] | Q 휠에 *지각한 증상* 전체를 원자 관찰어로 나열(종류 비의존). 틀린 선택은 HUD 문구가 아니라 *상대 반응*(관제의 되묻기, 잘못된 편성, 지연)과 Tab 기록의 사후 줄로 전달. 오답 비용은 *시간·순서 지연* 위주(ACE3/Ambulance 선례) |
| X3 | **난이도 = 안내 토글 묶음, 단 권한은 고정** | PS:PO `Casual`/`Simulation`+개별 토글; Ambulance Life `Classic`/`Simulation`/`Custom`+9토글; ACE3 `advancedDiagnose` 0–3과 공식 프리셋 "Basic/Advanced/Co-Op/PvP"; Squad `Hide Objective Indicators` 등; Ignite Easy/Medium/Hard·AI 자율·`Simplified inputs` | 현재 켜져 있는 *경로 안내 34 m·체크리스트·휠 정답만*을 각각 독립 토글로 분해. 노브에 *행동 권한 확장*(약물 등)은 두지 않음(ACE3 선례). 기본값 선택은 아래 X10 참조 |
| X4 | **홀드 = 지각·점검·집중(공간 게이팅)** | PS:PO Focus LMB ≈0.75 s; Ambulance Life `Focus (Hold)`; Ignite 열화상 LMB 홀드; ACE3 Win 홀드→호버→해제(`actionOnKeyRelease`) | 발견/점검 verb를 "대상 바라보며 홀드(≈0.6–1.0 s)"로 통일. 시야·거리·가림 조건 필수, 미충족이면 *아무 단서도 없음*(미지각 비노출). LMB는 소화기 홀드가 점유 → RMB 홀드 후보 |
| X5 | **손 한 개 독점 + 대상 고정 프롬프트** | Ignite: 호스 장비 시 할리건 자동 드롭, `Tools can also be dropped manually by holding [E]` [관찰 QHg_V0neE3Q t=03:15]; ACE3 `Holster Required`(치료 전 무기 내림); PS:PO 도구 휠(Tab 홀드 ≈1 s); Ambulance Life 도구가방 T→Equip→부위→확정 | 한 손 1개(소화기/AED/무전기). 장비 교체는 홀드 휠 또는 `G` 드롭. 프롬프트는 대상에 붙이고 *동사 아이콘*만 |
| X6 | **연속 물리 입력은 *물리 도구*(호스·노즐·소화기)에만, 의료는 선택·진행바·약한 미니게임** | Ignite 노즐 RMB·조준; ACE3 모든 치료 고정 진행바(`Performing CPR... Time left: 10s`) [관찰 x1vZbZW0l88]; Ambulance Life 붕대/정맥로 미니게임은 개발사가 "inconsequential"로 설계, 끄기 옵션 제공 [공식] | 소화기·호스는 지금의 심화 모델을 유지하고, CPR/AED/지혈은 *상태에 묶인 홀드/대기/기기 단계*로(리듬 정밀도 평가는 선택). 미니게임식 "Perfect" 판정은 제외 |
| X7 | **숨은 시계 + 지각 가능한 단서** | ACE3 심정지 300 s ±10 %·CPR 중 감소 ×0.5, UI에 타이머 없음 [공식 소스]; Ambulance Life 바이탈 악화·`Fast patient deterioration`; Ignite 백드래프트·붕괴 바닥(배차 경고)·불꽃색 | 사상자·화재·군중 각각에 *숨은 악화 시계*를 두고 단서(호흡 소리·안색·연기 농도·소음)만 노출. 사망·악화는 즉시 게임오버가 아니라 결과로 기록 |
| X8 | **평가: 결과 중심 + 이유 줄, 규칙표 의존은 취약** | Ignite 3체크+통계; Ambulance Life 바이탈 점수·Found→Treated→Admitted 표(1.4 점수 툴팁); PS:PO SP/CP·`Encounter Reports`·16개 규칙 되돌림; ACE3 감사 로그 | LLM이 즉석 합성하는 사건에는 *사전 열거 합법표*를 쓸 수 없다 → **percept 태그 기반 증거 게이트** + 결과 지표 + 사유 문장 로그. 시간 임계 고정 금지 |
| X9 | **인수인계 = 상태 기록** | ACE3 triage card·`ACTIVITY LOG`(`%1 performed CPR (mm:ss)`)·`QUICK VIEW`; PS:PO `Encounter Reports`; Ambulance Life `Treatment Provided`; Ignite `STATS` | 플레이어가 *본 것·한 것*이 타임스탬프로 자동 기록되고, 119/소방 도착 시 인계 항목이 되며 *무전 보고 문장도 이 기록에서 고른다*. 빠진 항목 목록은 공개하지 않음 |
| X10 | **실패는 단계적, 기본 안내 수준의 선례와 그 한계** | Ignite Easy: 자가 리셋·번짐 느림, Hard: 한 명 사망=실패 [2차]; PS:PO CP 0=해고(Casual은 꺼짐); Ambulance Life 게임오버(보행자/구급차 파손/본인 치명상) + `Disable game over by totaled car`; PS:PO·Ambulance 기본은 *안내 켬*(Casual 기본 v1.1.0, Classic "Recommended")인데도 둘 다 "가이던스 부족" 비판(Ambulance 개발사 "player guidance is lacking significantly", 2025-02-17) | *낮은 안내를 기본*으로 하려면 안내 대신 **학습 채널**(핸드북/첫 마주침 카드/Glossary 해금)이 필요하다. Ignite `Glossary entry unlocked`·PS:PO `H Handbook` 팝업·Ambulance `H` 핸드북이 선례. 실패는 하드 게임오버보다 *결과 서술*로 |
| X11 | **랜덤 구성의 선례는 있으나 모두 *사전 제작 표*** | Squad 랜덤 미션(주사위 아이콘, 시드 공개·공유) [공식 S2·S3]; Ignite Alarm Mode(스테이션 안 임의 간격 출동) [공식 F3]; Ambulance/PS:PO 콜아웃 | JEV 합성은 이 선례들보다 한 단계 더 열려 있어 *규칙표 기반 채점/안내가 불가능*. X8과 같은 percept 게이트로 일반화 |
| X12 | **대화·진정은 질문/문장 선택 + 발화** | Ambulance Life Anamnesis(E 탭→질문 목록→`Patient calmed` 기록); PS:PO 선택 항목을 캐릭터가 대사로 말함(`I'm issuing you a ticket for littering…`), Space 스킵(2025-07) | 보고·안내·진정 문장을 휠에서 고르면 자막+음성으로 발화되고 상대가 반응. 질문 순서·누락은 평가 대상 |
| X13 | **CPR 입력 모델이 4종 모두 다르다** | Ambulance Life 1클릭+인계(이송 모드), PS:PO 박자 액션 라인, ACE3 15 s 바×확률, KAT 연속+LMB 중지 | 일반인 CPR은 *커밋 홀드(손을 떼면 중단) + 기회비용(다른 verb 불가, 다른 사건 진행) + 사망 시계 반감(ACE) + 선택적 박자*의 조합이 가장 CHOOGuard 문법(이미 홀드를 쓰는 소화기)과 일관 |

### 5.1 CHOOGuard에 직접 옮길 수 있는 *최소 조합* (Main 합성용 후보, 구현 지시 아님)

1. **인터랙션 계약 확장**: `Tap / Hold(링) / Aim+Hold / Gesture(Simplified=Hold로 대체)`와 진행도 노출. 프롬프트는 *대상의 동사*를, `CanInteract`는 *물리적 가능성*을 뜻하게(정답 여부는 결과로). [FS-8 1·2행, X5]
2. **포커스 홀드**로 발견·점검·질의를 통합, 관찰 문장은 색 채널(타인 / 자기 위험 / 보조). 시야 밖에는 단서 없음. [X1, X4, PS-8]
3. **Q 휠 재설계**: 지각한 원자 관찰어 전체 + 결과 기반 반응 + 선택 문장 발화 + 사후 한 줄. [X2, X12]
4. **쓰러짐 체인**: 반응·호흡 확인(검진 문장, `심정지` 라벨 금지) → 119 보고 → AED 가져오기(설비 위치) → 패드 부착(길게) → 분석 대기(접촉 시 지연) → 기기 음성 → 충격 버튼 → CPR 재개. CPR은 X13 조합. 모든 단계의 기록이 인수인계 카드로 누적. [ACE-8, AL-8, PS-8]
5. **연기·열 노출**: 웅크리기 신설, 연기층 아래 이동·헤이즈·기침·시야 가장자리 연출, 수치 바 없음, 마스크 없음, 임계 초과 시 *자력 대피 가능한 행동 불능*. [FS-8, ACE-8, AL-8]
6. **문/개구부**: 열기 전 단서(연기·소리·열) 지각 → 열면 확산/피해 → 비켜서기로 회피. *Hazard 종류 비의존* 공통 필드. [FS-8]
7. **종료 평가**: 3체크 + 통계 + 사유 문장 + 인계 완성도(Poor/Sufficient/Extensive/Complete처럼 *질적 단계만*, 빠진 항목 비공개). 시간 임계 고정·해고·되감기 없음. [FS-6, PS-6, X8]
8. **안내 노브 + 학습 채널**: 경로 안내·체크리스트·휠 정답 표시·점수 즉시 표시·툴팁을 각각 토글. 기본은 낮은 안내 + 첫 마주침 카드/핸드북. 권한은 노브 아님. [X3, X10]

---

## 6. 미확인·한계 (집계)

### 6.1 조사 공백
- **Ignite**: 소화전 개방 제스처 영상 미관측(가이드 서술만); SCBA 착탈 키 미확인; Command View 프레임 미확보; 호스 길이·꼬임 정보 없음; 체력·타이머 수치는 2차뿐; Space/Q 지휘 입력 충돌 미해결; 임무별 시간 임계 결정 방식 미확인. **Squad**: 본편 평가·산소 모델 미확인. (FS-9)
- **Ambulance Life**: 키 바인딩은 2024-12 데모 시기 설정 화면에서 읽음(1.4와 동일 여부 미확인); `Focus (Hold)`가 마우스 어느 버튼인지, 트리아지 PC 키, 제세동 충격 입력 미확인; 점수 공식·S/A/B/C 컷오프·사망 확률 비공개; 2025-04-09 이후 패치 노트 없음. (AL-9)
- **PS:PO**: 방사형 확정/뒤로/툴팁 PC 키 이름, CPR 입력 키·박자 간격·판정 창, CP 산식(관측 2건이 0.5 %/CP와 불일치), `Unjustified:` 보고서 줄 원문을 영상으로 직접 보지 못함. (PS-9)
- **ACE3/KAT**: 문서와 소스 불일치 4건(CPR 40 % vs min/max 슬라이더, 깨어남 5 % vs 소스 0.1, `Advanced Diagnose` 체크박스 vs 목록, `Fatal Damage Source`) — 소스(3.21.2.113) 우선; KAT AED 음성은 청취 못 함; 한국 역무원 교육 범위(토니켓 등)는 레인 밖. (ACE-9)

### 6.2 방법 한계
- 영상은 모두 ≤480p라 작은 글자는 4–6배 확대로 판독했고 일부 숫자는 판독 불가였다. 구간 다운로드는 키프레임에 맞춰 시작이 최대 약 10 s 당겨질 수 있어 하위 에이전트가 정리한 절대 시각은 ±10 s 오차가 있다.
- 많은 "가이드 사이트"(magicgameworld, spottis, ofzenandcomputing, clashiverse, spot.monster, driffle)는 403·AI 작성 흔적·허위 서술로 *메카닉 근거에서 제외*했고 키 목록도 다른 출처와 교차되는 것만 사용했다.
- 대다수 수치는 *게임별 규칙*이지 현실 절차가 아니다. 한국 역무원의 실제 교육·장비(옥내소화전 호스 조작, AED·CPR 교육, 방연마스크 비치 여부)는 이 레인에서 검증하지 않았다 — 적용 표의 "일반인 행동요령 수준" 문구는 모두 [추론]이다.

### 6.3 다른 레인과의 경계
- 오브젝트 조작의 질감(밸브·레버·볼트의 물리 손 조작)은 레인 A·B(마이썸머카·Amnesia·PowerWash 등)가 더 깊다. 이 레인이 공급하는 것은 *현장 절차 순서·평가·안내 노브·응급 verb 문법*이다.
- 절차·판단·보고 문법(Papers Please, 112 Operator, Ready or Not)은 레인 D와 겹친다. PS:PO의 증거 게이트·Encounter Report, Ambulance의 Interim Diagnosis는 그쪽과 교차검증이 필요하다.
