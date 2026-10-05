# Lane B — 물리 손조작 · 운전대(캡) 조작 · 잔여 작업 피드백 · 노-HUD 정보 설계

조사일 2026-10-04 (작성: LaneB_PhysicsCabFeedback). 연구 전용: 코드·씬·설정 변경 없음. 미디어는 `media/B/`(git-ignored).
대상: ① Frictional Games 물리 상호작용(Amnesia TDD/AMfP/Bunker, Penumbra) ② Train Sim World 5(+현행 6/7 지원 문서) ③ PowerWash Simulator ④ Escape from Tarkov.

## 0. 한눈 요약 (CHOOGuard "지시 따라가기" 진단과의 연결)

CHOOGuard 진단(`findings.md` §0): 상호작용 ~25/30이 *프롬프트가 정답을 말해 주는 E 탭*, 상태는 토스트/체크리스트로 통지, 위험·보조 정보는 상시 표시.
네 게임이 공통으로 반대 방향을 취한다.

1. **조작이 "물리 상태 + 연속 입력"이다.** Frictional은 LMB를 쥐고 마우스를 움직여 문·서랍·레버·밸브의 실제 관절을 움직인다(§1.3). 정답 이름은 어디에도 없고, *한계(min/max)와 저항*이 결과를 알려 준다.
2. **상태는 대상 자체에 있다.** TSW 십자선은 `Master Key  Off → On`처럼 *이름 + 현재 값*만 보여 주고 "무엇을 하라"는 말은 하지 않는다(§2.3 관찰). Frictional 레버는 슬롯 모양이 가동 범위를 그린다(§1.7 관찰).
3. **정보는 요청해야 나오고, 정밀도는 비용·숙련으로 오른다.** Tarkov 탄창 확인은 `Full / Nearly full / About half…` → `Approx. 15` → 정확 값(§4.3), 미확인 아이템은 조사 시간이 든다. PWS 잔여 오염 하이라이트는 *버튼을 눌러야* 0.9–5 s 동안 켜진다(§3.3).
4. **보조(어시스트)는 "잡일"을 줄일 뿐 판단은 남긴다.** TSW Beginner/Standard/Expert/Custom은 자동 연결·자동 분기·승강문 자동화를 끄고 켠다. 안전장치(AWS/TPWS/경계 확인)는 *기본 꺼짐(표준) / 기본 켜짐(Expert)*로 숙련 스킬이 된다(§2.5).
5. **실패는 점진적·회복 가능·진단 가능.** TSW 고장은 "non-fatal… easily reset", 단서는 물리적(승강문이 "slightly ajar")(§2.6). Amnesia 사망은 재시작이 아니라 "something in the game world will change"(§1.5). PWS는 실패 자체가 없다.
6. **평가는 사후 분해 보고.** TSW Action Points·메달·디브리프 그래프, PWS 부위별 `ding`+`Rail Cleaned! +$10.00`+상단 % 바(§3.5).

⇒ CHOOGuard에 가장 직접 이식 가능한 5가지(상세 §5, §6): (a) 홀드·드래그 조작 계약 `IFpsHoldInteraction`, (b) 중앙 판독을 "이름 + 현재 상태"로 바꾸고 동작 문구 제거, (c) 보조 수준 프리셋(Beginner/Standard/Expert/Custom) — 기본값을 Standard로, (d) 정밀도 단계형 정보(소화기 게이지·환자·군중 수), (e) 인지한 미해결 항목에 한정한 온디맨드 재확인 핑.

### 0.1 증거 태그 · 버전 주의
- [공식] 개발사/공식 매뉴얼/공식 지원 문서/스팀 뉴스. [2차] 위키·가이드·포럼·크리에이터 영상. [관찰 URL t=] 직접 프레임 확인. [추론] 본 조사자 해석. [미확정] 확인 못 함.
- **버전**: TSW 공식 지원 문서는 2026-09 갱신본이며 "Train Sim World"로 통칭(TSW7 정식 출시 2026-09-15 [공식 live.dovetailgames.com 업데이트 노트 검색 스니펫], TSW6는 2025-09 출시로 추정 — 공식 설정 이미지 경로 `media-cdn.dovetailgames.com/2025/092025/…`·"introduced with Train Sim World 6" 문구 기반 [추론]). TSW5 출시 2024-09-17 [공식 Steam]. 관찰 영상(Class 375/3, 2026-03-25, GoTraction)은 TSW6 시대 콘텐츠 — **캡 상호작용 UI는 TSW 시리즈 공통이라 TSW5에 준용**하되 이는 [추론].
- Tarkov는 2025-11-15 Steam 출시, 패치 1.1.0.0(2026-08-03)·1.1.5.0(2026-09-08) 시점 [공식 Steam 뉴스]. 관찰 영상은 0.12.12 빌드(화면 하단 `0.12.12.30.18965 Beta version`) — UI 세부는 구버전일 수 있음.
- 직접 플레이 불가. 조작 제스처는 매뉴얼·개발자 글·영상으로만 확인했고, 확인 못 한 부분은 [미확정]로 둠(§7).

---

## 1. Frictional Games — Amnesia: The Dark Descent / A Machine for Pigs / The Bunker (+Penumbra)

### 1.1 출처
| # | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| F1 | https://archive.org/stream/amnesia-tdd-manual-english/Amnesia-TDD-Manual_djvu.txt | [공식] TDD PC 매뉴얼(OCR) | 2010 | 상호작용 전 제스처, 기본 키표, 힌트 기본 ON, 사용 아이템→십자선 교체, 사망 규칙, 정신력 |
| F2 | https://cdn.fastly.steamstatic.com/steam/apps/239200/manuals/Manual.pdf | [공식] AMfP Steam 매뉴얼(`t=1670246309`) | 2022-12 | TDD와 거의 동일한 상호작용 절 + Wheel/Lever 문장 |
| F3 | https://frictionalgames.com/2010-09-lets-not-forget-about-physics | [공식] Thomas Grip 블로그 | 2010-09-03 | 문·서랍·휠 설계 이유, Penumbra 대비 개선 |
| F4 | https://oldwiki.frictionalgames.com/hpl2/amnesia/entities?do=export_pdf | [공식] HPL2 엔티티 문서 | 2019 판 | SwingDoor/Lever/Wheel/Slide/Button 타입 제약 |
| F5 | https://frictionalgames.com/2024-01-amnesia-the-bunker-accessibility-patch/ | [공식] Bunker 접근성 패치 노트 | 2024-01-08 | "Physics Interaction Mode", "Charge Flashlight Mode", 간단 아이콘, 십자선 숨김, "no input holds" |
| F6 | https://frictionalgames.com/2023-10-amnesia-the-bunker-halloween-update-launch/ | [공식] Bunker 할로윈 업데이트 | 2023-10-25 | Custom Mode 30+ 설정, Shell Shock(하드코어), 저장이 연료 소모 |
| F7 | https://gameranx.com/features/id/467193/article/amnesia-the-bunker-generator-system-explained/ | [2차] | 2023-06-05 | 발전기: 연료 투입 홀드, 레버 내림, 연료 게이지 틱, 회중시계 동기 |
| F8 | https://www.gamepressure.com/amnesia-the-bunker/controls/zb10cb5 | [2차] | 2023-06-15 | Bunker 조작 *이름* 목록(키 아이콘은 이미지) |
| F9 | https://www.youtube.com/watch?v=2ve0eVwjv5k | [공식] Frictional 물리 영상(F3 본문 임베드) | 2010-09 | **프레임 관찰**(레버·휠·서랍·커서) |

### 1.2 조작표 (TDD/AMfP 공식 기본값)
| 동사 | 입력 (정확) | 출처 |
|---|---|---|
| 모든 상호작용의 시작 | **LMB**로 시작. "certain special actions can be made by pressing the right mouse button *while the left is kept down*" | F1 |
| 초점 | 중앙 십자선을 대상에 맞추고 가까이. "the possible interaction depends on the icon shown" | F1 |
| 문 | LMB 누른 채 *열고 싶은 방향*으로 마우스 이동. RMB(누른 채) = 문을 놓고 *바라보는 방향*으로 강하게 밈 | F1/F2 |
| 서랍 | 문과 동일(누른 채 이동). RMB = 강하게 밈 | F1/F2 |
| 레버 | "Simply move the mouse, while holding down the left mouse button, to move the lever to either end." | F1/F2 |
| 휠(밸브 등) | "move the mouse in a circular motion in the direction (clockwise or counter-clockwise) you want it to move" | F1/F2 |
| 물건 집기(Grab) | LMB 누르고 유지 → 들고 이동(무거우면 느려짐). **R 유지(또는 휠클릭) + 마우스** = 회전, **마우스 휠** = 가까이/멀리, **RMB** = 던지기 | F1 |
| 밀기(Push) | LMB 누른 채 W/A/S/D로 밀기, RMB = 세게 | F1 |
| 줍기(Pick) | LMB 클릭 → 인벤토리 | F1 |
| 정적 대상 | LMB 클릭만. "A sound and/or text should instantly be presented… it will be very clear that no further action is required." | F1 |
| 점화(Ignite) | 광원에 초점 → 아이콘에 *틴더박스 남은 개수* 함께 표시 → LMB | F1 |
| 레벨 문 | LMB 클릭 → 맵 로드(별도 아이콘 "Enter Level Door") | F1 |
| 사다리 | LMB 클릭(유지 불필요), 내리기 = RMB 또는 Space | F1 |
| 인벤토리/저널/기억 | Tab / J / M(기억 스크리블), 최신 텍스트 N | F1 |
| 이동·자세 | WASD, Space 점프, Ctrl 웅크리기(토글), Shift 달리기(유지) | F1 |
| **아이템 사용** | 인벤토리에서 아이템 *더블클릭* → 게임 복귀 시 "the icon of the item will have replaced your cross hair… move so close that the icon starts **blinking**" → LMB로 사용 | F1 |
| 아이템 조합 | 인벤토리에서 LMB 누른 채 다른 아이템 위로 드래그&드롭 | F1 |

**Bunker**(조작 이름만 [2차] F8): Move / Rotate Camera / Crouch / Put on a gas mask / Run / Lean out / **Throw object / ready weapon** / **Interact / use selected item** / **Take out the flashlight / charge the flashlight** / Holster weapon / Jump / Switch weapons / Main menu / Equipment. 키 배정은 이미지라 텍스트 확인 [미확정]. 접근성 패치(F5)에 `Physics Interaction Mode`, `Charge Flashlight Mode`, `Aim/Use Item Mode`, `Sprint Mode`, `Reload Weapon Mode`, `Check Health Mode` 등 *각 홀드를 토글로 바꾸는 모드*가 있음 → 동작이 원래 홀드였다는 간접 증거.
발전기(F7 [2차]): 연료 장비 장착 → 깔때기 위에서 "hold the aim button and the Interact button" → 연료 투입(통/병 모두 게이지 2틱), "turning the lever down to power the system", 근처 회중시계가 연료 게이지와 동기.

### 1.3 조작 문법 (Manipulation grammar)
- **원시 동작은 하나: "쥐고 움직인다".** 대상의 *타입*(Door/Drawer/Lever/Wheel/Grab/Slide)이 마우스 2D 이동을 어떤 3D 힘으로 바꿀지 결정한다. 설계자 서술: "estimating how to convert 2D mouse input into 3D forces. As you will never be able to get perfect correlation… (you will always lose a dimension)" [공식 F3].
- **어디를 잡아도 된다.** Penumbra는 "interact with the door at the right place"가 필요해 "hard to close/open doors in stressful situations"였고, Amnesia는 "click anywhere on the door" [F3]. 휠도 Penumbra 최다 불만("interacting at center zero leverage… impossible to turn")이라, "interact wherever you like"로 재설계하고 "special system that analyzes your mouse movements and can quickly and correctly determine which way you want to rotate. It can even approximate the speed and there is as good as no lag" [F3].
- **물리는 시뮬레이션 그대로, 결과 상태는 안정적.** "doors will now stay closed / opened"(되튐 제거), 서랍은 "react faster… (in Penumbra drawers could continue sliding after you stopped moving)" [F3].
- **구속(constraints)은 관절 한계.** HPL2: SwingDoor는 힌지 *joint min/max*(대략 90° 간격), Lever는 "swung between states to trigger callbacks", Wheel은 "turned indefinitely" 또는 밸브처럼 "fixed amount in order to trigger an event", Slide는 "single direction for a certain amount", Button은 "calls a callback" [F4]. 공식 위키 Engine Scripts 문서(검색 스니펫만 확인, 본문 미열람)는 Wheel→문 연결에서 MoveObject가 휠과 함께 연속 이동하고 레버는 min/max/middle에서만 이동한다고 구분 [공식, 스니펫 한정 https://wiki.frictionalgames.com/page/HPL2/Engine_Scripts].
- **상태 피드백 = 월드 + 커서 + 짧은 텍스트.** 커서 아이콘이 타입을 알려 줌(Default/Pick/Ignite/Enter Level Door/Climb) [F1]. 정적 대상은 *즉시 소리/텍스트*. 아이템 주울 때 화면에 `Picked up Tinderbox`, `Picked up Drill Part` [관찰 F9 t≈0:14–0:20, 1:30±].
- **퍼즐 아이템 사용 = 십자선 교체 + 근접 시 점멸(범위 신호).** 정답 여부는 알려 주지 않고 "닿는 거리인가"만 알려 준다 [F1].

### 1.4 안내 · 교육 모델
- **힌트 기본 ON, 해당 동작을 처음 만났을 때만**: "if hints are active (which they are by default) then the game will give a tutorial of all these movements as you play" [F1]. 매뉴얼은 "a quick glance should be enough… instructions should only be used as a reference".
- **막혔을 때의 안전망은 저널 "Mementos"**: "extremely important memory scribbles that can be of great help if you find yourself stuck"; 추가되면 "journal-icon flashing in the lower right corner of the screen and hear a scribble-like sound", M으로 열람 [F1]. 즉 *플레이어가 요청하면 읽는 내러티브 요약*이지 목표 마커가 아니다.
- **지역성 규칙**: "almost all puzzles can be solved using objects or items from the same level"(예외는 mementos로 암시) [F1].
- **디에제틱 원칙**: HUD 없음(건강·정신력 수치는 Tab 인벤토리 안에만 [F1]). 발전기 연료는 게이지 바늘 + 회중시계 [F7 2차].
- **접근성**: Bunker에서 "simple interact icons instead of the default, detailed ones", "hide the default crosshair", "Lock Code Quick Access", 입력 홀드 대체 모드 [F5].

### 1.5 실패 · 결과 모델
- 체력 0 → 사망이지만 "instead of simply restarting, you will not lose any progress, but something in the game world will change… you will never be sure what it will be" [F1]. (소프트 실패.)
- 정신력: 어둠 속에 있거나 끔찍한 것을 오래 보면 하락, "physical and mental problems" 발생; 올리는 유일한 방법은 퍼즐·장애물 해결 [F1] → *행동이 아닌 상태* 압박 + 해결 보상.
- 탐색 자원 경제: 틴더박스는 아껴 쓰되 너무 안 쓰면 정신력에 악영향 [F1].
- Bunker: 연료 게이지가 0이 되면 조명 상실·괴물 위험 상승(F7 [2차]); Shell Shock에서는 "Saving your progress costs fuel" [F6].
- 지연 실패: 발전기 꺼짐은 *게이지가 줄어드는 동안* 이미 진행 중이고 시계로만 확인 → 정보를 읽는 습관이 비용을 줄인다 [F7].

### 1.6 평가 모델
- 점수·등급 없음. AMfP 매뉴얼: "It is not a game that is meant to be won. The goal is the immersion itself" [F2]. Bunker는 Custom Mode/Shell Shock로 *난이도 매개변수*를 플레이어가 조정(30+ 설정) [F6].

### 1.7 영상 관찰 (Frictional 공식 물리 영상)
URL https://youtu.be/2ve0eVwjv5k (2분 15초). 저장: `media/B/frictional_physics_2ve0eVwjv5k.mkv`. 추출 프레임: `phys_wheel_t98.png`, `phys_wheel_t106.png`, `phys_acc_grid.png`, `phys_10_34.png`.
- **[관찰 t≈1:37–1:41]** 석조 기둥의 *슬롯 하우징 속 레버*(`phys_wheel_t98.png`): 슬롯 형태가 가동 범위를 그대로 그림, 레버가 수직(아래) → 수평(옆) 끝단으로 이동. 오버레이/게이지 없음. → "한계 = 눈에 보이는 기구".
- **[관찰 t≈1:41–1:42]** 사다리를 향하자 십자선이 *작은 사다리 글리프*로 바뀜(Climb 아이콘) (`phys_acc_grid.png` 2행 1열).
- **[관찰 t≈1:42–1:46]** 수조 위 *스포크 휠(밸브)* — 프레임마다 스포크 각도가 달라 회전 중, 휠 위에 작은 손 모양 커서 (`phys_acc_grid.png` 2행 2열, `phys_wheel_t106.png`). HUD·수치 없음.
- **[관찰 t≈0:14–0:20]** 문을 잡고 열 때 손/랜턴 시점, 화면 중앙 하단에 `Picked up Drill Part` 한 줄 텍스트 (`phys_door_14_24.png`).
- 한계: 영상은 2010 프리릴리스 트레일러라 *마우스 궤적*은 보이지 않는다 — 제스처 사양은 매뉴얼(F1)에 의존.

### 1.8 CHOOGuard 적용
| 판정 | 메커닉 | CHOOGuard 동사 | 구체적 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 홀드+마우스 이동으로 밸브·레버·핸들 조작 | 가스 중간밸브·기기 전원, 분전반 차단기, 셔터·에스컬레이터 조작함, 옥내소화전 밸브, 문 | 계약에 `IFpsHoldInteraction { Begin(responder); Tick(responder, Vector2 mouseDelta, float dt, out string feedback); End(responder); }` 추가. 홀드 중 `SuppressLookInput=true`로 시선 고정 후 마우스 Δ를 대상 관절에 적용(레버=선형 투영, 휠=각속도 부호의 누적 각). 가스 볼밸브=1/4회전(≈90°), 소화전 게이트밸브=수 회전 등 *대상별 한계각*을 데이터로. 한계 도달이 곧 결과. 현재 `IFpsInteraction`은 에지 E만 지원(`IFpsInteraction.cs:3-8`, `FirstPersonResponder.cs:130-132`) | F1, F3, F4 |
| **Adopt** | 어디를 잡아도 되는 문/휠 | 문·셔터 손잡이 | 콜라이더 전체를 핸들로 간주하고 레이캐스트 히트 지점 상관없이 각속도 기반(레버암 0 문제 회피) | F3 |
| **Adapt** | 한계·저항·되튐 없음의 피드백 | 밸브·셔터·분전반 | 끝단 도달 시 "툭" 소리 + 정지(되튐 금지), 힘이 더 필요한 대상(굳은 밸브)은 마우스 이동량 대비 각도 이득을 낮춤 | F3 |
| **Adapt** | 상태는 대상에 표시(레버 위치·핸들 방향) | 위 전체 | 토스트 대신 핸들 각도·차단기 플래그 위치·지시등으로 상태 표시. 밸브 핸들이 배관과 *나란하면 열림 / 직각이면 닫힘*(일반 볼밸브 관례, [추론]이며 실제 역사 설비 형식은 현장 확인 필요) | F4, §1.7 |
| **Adapt** | 정적 대상 클릭 = 즉시 소리/텍스트 | 발견·점검(inspect) | `살펴보기`를 *지시가 없는 관찰 문장*으로(예: "배관 접합부에서 쉭 소리가 난다") — 현재 정답형 프롬프트(`가스 중간밸브 잠그기`, `Kitchen/GasValvePoint.cs:44`) 대체. 인지한 사실만 출력(시야·거리 조건 유지: `IncidentDirector.cs:456-474`) | F1 |
| **Adapt** | 들고 있는 아이템이 십자선을 교체, 닿는 거리에서 점멸 | 소화기 조준, AED 패드, 접근 통제(라바콘·안전띠) 설치 | 보유 도구 아이콘을 십자선에 표시, *유효 거리/각도 안*에서만 점멸(정답 여부 아님). 소화기 조준 품질은 이미 거리·각도 규칙 있음(`StaffTools.cs:274-336`) → 신호만 시각화 | F1 |
| **Adopt** | 홀드를 토글로 바꾸는 접근성 모드, 단순 아이콘, 십자선 숨김 | 모든 홀드 동사 | 설정에 `조작 방식: 홀드/토글`(동사군별), `간단한 아이콘`, `십자선 숨김` | F5 |
| **Adapt** | "사망 대신 월드 변화" | 방치·오조작의 결과 | 이미 *실패 상태 없음*이라 유지하되, 방치 시 *역무실·기관이 대신 해결*(§0.3)하지 말고 **위험이 실제로 커지거나 환자 단계가 오르는** 월드 변화로 연결 | F1 |
| **Adapt** | 저널 Mementos(막혔을 때만 읽는 요약, 깜박이는 아이콘) | Tab 상황판 | ○/● 조치 체크리스트(`IncidentDirector.cs:878-891`) 대신 *플레이어가 인지·기록한 관찰*(본 것·들은 것·보고한 것)을 시간순 열람; 새 항목 추가 시 소리+아이콘 | F1 |
| **Reject** | 물건 던지기·세게 밀기(RMB), 정신력 수치 | — | 역무원 절차 아님·시나리오 일반성 없음. 정신력 대신 §4.4식 *본인 노출 상태* 사용 | F1 |

---

## 2. Train Sim World 5 (Dovetail Games) — 캡 조작 · 어시스트 · 안전장치 스킬 · 차장 모드

### 2.1 출처
| # | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| T1 | https://support.dovetailgames.com/hc/en-us/articles/36105734473234 | [공식] Controls Guide | 2026-09-08 갱신 | 키보드 전체 표(On-foot/Driving/Cameras/HUD/Safety/Guard), Beginner Controls |
| T2 | https://support.dovetailgames.com/hc/en-us/articles/28572764655122 | [공식] Settings Explained | 2026-09-09 | HUD 항목, Player Assist 프리셋, Control Change Notification, 안전장치 기본값 |
| T3 | https://support.dovetailgames.com/hc/en-us/articles/36927727591186 | [공식] Train Faults & Random Events | 2026-07-23 | 고장 목록·해결 절차·단서 |
| T4 | https://support.dovetailgames.com/hc/en-us/articles/29235998868370 | [공식] Conductor Mode | 2026-04-22 | 승객·표 검사·문 |
| T5 | https://support.dovetailgames.com/hc/en-us/articles/29236025155602 | [공식] Scenario Mode | 2026-04-22 | 목표 표식·컨트롤 표식·T 목록 |
| T6 | https://support.dovetailgames.com/hc/en-us/articles/28572759077394 | [공식] Getting Started | 2026-09-11 | 안전장치 기본 켜기, AI Conductor, Reset Simulation Physics, 디지털 디스플레이 |
| T7 | https://media.dovetailgames.com/Manuals/Train%20Sim%20World%204%20DB%20BR%20101%20Expert%20Driver%27s%20Manual.pdf | [공식] BR 101 Expert 매뉴얼 (TSW4/5) | 2024 | Expert Mode 정의, 고장 시뮬레이션 |
| T8 | https://live.trainsimworld.com/news/tsw-20-detail | [공식] TSW2020 상세 | 2019-07-26 | Action Points, 메달, 디브리프 |
| T9 | https://store.steampowered.com/app/2967990/Train_Sim_World_5/ | [공식] TSW5 스토어 | 2024-09-17 출시 | Conductor Mode 소개 문구 |
| T10 | https://forums.dovetailgames.com/threads/tsw4-whats-the-state-of-the-scoring-system.77765/ | [2차] 포럼 | 2024-01 | TSW4 점수 분해·플래티넘 |
| T11 | https://train-sim-world.fandom.com/wiki/Intro_Sequence_Tutorial | [2차] | TSW 초대 | 튜토리얼 목표 문구 구조 |
| T12 | https://www.youtube.com/watch?v=Zt3L8xbqIg8, https://www.youtube.com/watch?v=yAEIObsoZdk | [2차] GoTraction 영상 | 2026-03-25 | **프레임 관찰**(툴팁·알림·가드 패널) + 자막 |

### 2.2 조작표 (공식 키보드 기본값, T1)
| 동사 | 입력 |
|---|---|
| 캡 컨트롤 조작(상호작용) | **Left Mouse** = Interact (On Foot 표). **Right Mouse** = Toggle Cursor |
| 시점 | Mouse = Look, Wheel = Zoom, 1 = 1인칭, 2/3 = 붐/외부, 8 = 자유, 0 = 카메라 메뉴(홀드), ←/→ = 캡 카메라 전환 |
| 좌석 | E = Enter/Exit Seat/Climb Up |
| 방향·출력·제동(기본) | W/S = Reverser, A/D = Throttle +/−, `;` / `'` = Automatic Brake +/−, Space = Horn |
| 경계 확인 | **Q = Alerter / AWS / DSD / DVD / Sifa / Speed Supervision Reset** |
| 문 | Y = 좌측, U = 우측 개폐 |
| **Train Interaction Menu** | **Tab** |
| 지도·일정 | 9 = Live Map, T = Toggle Schedule |
| HUD 토글 | Ctrl+1 Objective Marker, Ctrl+2 Next Speed Limit, Ctrl+3 Next Signal, Ctrl+4 Speed Limit & Signal Cycle, Ctrl+5 Speedometer/Compass, **Ctrl+6 Score Toggle**, Ctrl+7 Stop Indicator, Ctrl+8 Reticle Opacity; **F1 = Hide HUD** |
| 안전장치(영국) | Ctrl+Return = AWS/TPWS Toggle, **Shift+Return = DSD/DVD/Vigilance Toggle**, End = AWS/TPWS Brake Release, Delete = TPWS Train Stop Override, PgDn = DRA Reset |
| 가드/차장(키) | H/N = 앞/뒤 승강문 열기, J/M = 닫기, O/L = 로컬 문, F/V = 앞/뒤 문 Enable, G = Guard Signal Buzzer, Ctrl+W/Ctrl+S = 가드 키 |
| 일시정지·초기화 | Esc = Pause/Clipboard, Pause→Overview→**Reset Simulation Physics** |

**Beginner Controls**(Settings>Controls): "completely remove the need for setting up the cab"; Timetable·Free Roam에서만(그 외 모드는 "warning… swap to Immersive Controls"). 키: W/S 방향, A/D 출력, `;`/`'` 제동, E 하차, Y/U 문, Space 경적 [T1]. 패드는 "Immersion/Classic" 두 스킴 [T2].

**마우스 캡 조작의 정확한 제스처(드래그 vs 휠 vs 클릭-유지)는 공식 문서에서 확인 못 함** — 공식은 "Interact = Left Mouse"까지만(T1). 관찰(§2.3)로는 *레티클 링 + 이름·값 툴팁* 확인. 드래그/스크롤 사양은 [미확정], 일부 스위치는 "no global shortcut and must be operated with the mouse"(마스터 키·차단기·안전장치 스위치·무전 등) [2차 flyawaysimulation 요약].
공통 구조: 대부분의 컨트롤에 **직접 조작 경로 + 키 단축키**가 *병행*(튜토리얼 문구 "You can also use W to set the Reverser to Forward." [T11 2차]).

### 2.3 영상 관찰 (GoTraction, Class 375/3, TSW6 시대, 2026-03-25)
URL https://youtu.be/Zt3L8xbqIg8 — 저장 `media/B/tsw_Zt3L8xbqIg8.webm`.
- **[관찰 t≈0:21]** 십자선이 *링(4눈금)*으로 바뀌고 오른쪽에 **마우스 아이콘 + `Master Key  Off`** (`tsw_375_t21_reverser_crop.png`). **[t≈0:22]** 클릭 후 같은 자리에 `Master Key  On` (`tsw_375_t22_crop.png`) — *이름 + 현재 값*만 표시, 행동 안내 문구 없음.
- **[관찰 t≈0:23–0:26]** 리버서에 링이 퍼져 *노치 점(양옆)*이 나타나고 `Reverser  Off → Forward` (`tsw_375_12_30.png` 하단 행). 상태가 위치로 표시됨.
- **[관찰 t≈4:06–4:30]** 화면 우측에 **알림 줄**이 연속: `Combined Power Handle: Off`, `Combined Power Handle: P2`, `AWS Reset`, `Vigilance Pedal` (`tsw_375_vigilance_246_270.png`). 설정 `Notifications`("Displays a prompt on the right-hand side… each time a control is interacted with")와 일치 [T2].
- **[관찰 상단 좌측 상시]** 목표 HUD `STOP AT LOCATION  Gerton Platform 2 – 06:07:00`, 남은 거리(예 `940 yd`), 우측 상단 트랙 모니터 (`tsw_375_340_364.png`).
- 자막(자동): "let's turn on vigilance which reminds you every now and again make sure you're awake. DSD, so if you get up it yells at you… AWS if you do anything naughty with signals" (0:47–0:56), "Hands up, doing nothing. TPWS should go any moment now. There it goes. Brake" (4:11–4:14), "That's how you acknowledge an AWS. You just press Q, circle, or B" (6:23) [2차 narration].
- 가드 영상 https://youtu.be/yAEIObsoZdk: 상단 HUD 목표 `LOAD PASSENGERS`, `WAIT`; 가드 패널(버저·문 버튼) (`tsw_guard_grid.png`) — 세부 제스처는 영상 초반 설명에만 의존 [미확정].

### 2.4 조작 문법
- **호버 → 툴팁(이름+값) → 클릭 상호작용.** `Control Change Notification`(ON/OFF): "Displays a tooltip of the name of a control, and its current set value, as you move the cursor over an operable control" [T2].
- **상태는 월드 오브젝트의 위치/표시등**(리버서 노치, 속도계, 점등). 대표 복합 계기는 *디지털 디스플레이*: 설정 `Digital Display Screen View: Classic/Enhanced` — "Enhanced mode allows you to click on a display to zoom into the screen"; 가상 커서로 조작 [T2/T6].
- **제약**: Alerter/AWS는 *시간 제한 확인*(미응답 시 페널티 브레이크) — 관찰 4:11–4:28; 토글 키는 별도(Shift+Return, Ctrl+Return).
- **실수 복구용 안전밸브**: "Reset Physics Simulation Button… reset the physics simulation using the dedicated button in the pause menu… reset your loco to the state in which it was found" [T6]. 단 BR 101 Expert의 고급 시뮬은 이 기능과 비호환(T7 Limitations).

### 2.5 안내 · 교육 · 어시스트
- **어시스트는 3층**: ① Beginner Controls(캡 세팅 생략) ② **Player Assist** `Default Level: Beginner / Standard / Expert / Custom` — "Beginner: Auto Coupling & Auto Set Junctions ON; Standard: Auto Coupling ON, Auto Set Junctions OFF; Expert: all player assists OFF"; 개별 `Automatic Coupling`, `Automatic Set Manual Junctions`, `Notifications`, `Expert Mode (Expert Locos Only)`, `AI Conductor Mode: Off / Always / Realistic` ③ **HUD 설정**: `HUD Type OFF / Normal / Simple / Minimal`, `Objective Marker`, `Stop Marker`, `Scenario Marker`, `Next Speed Limit/Signal Marker·HUD`, `Track Monitor`, `Display Safety System Helper` ("HUD elements help with the operation of Safety Systems") 등 항목별 ON/OFF [T2].
- **HUD Type별로 정확히 무엇이 숨겨지는지의 공식 표는 확인 못 함** [미확정]. `Minimal HUD Objective Distance/Traction Lock/Distance` 옵션명으로 Minimal은 *목표 거리 중심* 축약 HUD임을 추정 [추론].
- **Expert Mode(전문가 기관차 한정)**: "most of the locomotive is simulated… improved physics, default activated safety systems as well as random faults and failures"; PZB/LZB/SIFA 기본 켜짐, 열차 데이터(ZDE)를 *수동 입력*해야 모드가 맞춰짐, 고장 시뮬 기본 활성 [T7 공식]. 토글 중 변경은 "not guaranteed to work. Please restart the service".
- **안전장치 기본값**: `Enable Safety Systems by default`(설정) — "all safety systems will be enabled when taking control of a train", "useful for more advanced player who want the additional challenge" [T2/T6]. → *초보에게는 꺼 둔 채 시작*이 기본.
- **시나리오 안내**: "The blue markers will show you where to go. The control markers show you what you will need to interact with to complete the current objective… You can view the current list of objectives by pressing the T Key." 대기 폴 "striped pole… wait till the pole fills up" (승객 승차) [T5]. 모두 설정에서 끌 수 있음(Objective/Scenario Marker).
- **튜토리얼 구조**: 목표 문구가 *상태 지정형* — "REVERSER: Set Reverser to 'Forward'", "AUTOMATIC BRAKE: Set Automatic Brake to 'Initial Reduction'" [T11 2차, TSW 초대]. Training Center에서 기관차별 튜토리얼(T6).
- **자동화 가능한 잡일만 자동**: `AI Conductor Mode`는 문 취급을 "Always" 또는 "Realistic"(현실에서 차장 담당일 때만)로 대행; 일부 열차는 "put the train into neutral before the AI Conductor takes over" [T6]. 판단은 남김.
- **Conductor(차장) 모드**: Timetable 일부 서비스(티켓 아이콘)에서 Driver/Conductor 선택. "clearing luggage from the isles by clicking on it and checking passenger tickets. Click on a marked passenger to check their ticket. Then, make sure the ticket date matches today's date and their destination is included in the list of upcoming stops. If everything is as it should be, you can thank the passenger and move on. If the ticket isn't valid, you may inform the passenger, and they will depart at the next stop." 첫 몇 정차는 안내, `Guard Mode Control Guide`(설정)로 끔 [T4/T2]. Steam 소개: "check tickets, operate the doors and ensure passenger safety" [T9]. RailAdvent 리뷰(2차): "check tickets, operate the doors and move baggage out of the gangway".

### 2.6 실패 · 결과 모델
- **고장은 비치명·진단 가능**: "Train Faults will be a series of non-fatal faults that can be easily reset. Players will get a pop up… " (Player Assist 알림 켜야 표시) [T3]. 해결은 *물리 단서 → 절차*: `Passenger Door Stuck` — "Manually inspect every door for gaps. The faulty door will be slightly ajar. Open and close the door to resolve."; `Headlight Fuse` — "Open the rear fuse panel and find the Headlight fuse. Flip the fuse to the On position."; `Diesel Engine stalls` — 완전 정지·브레이크·컨트롤 원위치 후 엔진 정지, 이후 "hold the Engine Start button"; `Pantograph Drops`는 "different vehicle of the formation"일 수도 있음 [T3]. 빈도 `Random Behaviour – Train Faults: Disabled/Low/Normal/High`, `…Notifications: Enabled/Disabled` [T2].
- **안전장치 위반 = 즉각 제동/페널티**: DSD/AWS/TPWS 응답 지연 시 TPWS 제동 개입 (관찰 4:11–4:28, 자막). 해제 절차: End = AWS/TPWS Brake Release 등 [T1].
- **신호 지연·임시 속도 제한**: 랜덤 이벤트로 일정 지연 누적("knock-on effect… build up of delays") [T3].
- 사망/게임오버 개념은 확인되지 않음 [미확정]; 탈선 옵션 `Disable Junction Derail ON/OFF`("The Off option will mean that the train will react as it would in reality and derail") [T2].

### 2.7 평가 모델
- TSW2020: **Action Points** — "rewarded a set amount of points for completing tasks and driving by the rules of the railway"; 점수에 따라 **bronze/silver/gold**; 디브리프는 "breakdown of your attained action points… a graph that shows you how well you performed against the various speed limits… how on time you were, how accurate your stopping was" [T8 공식].
- 초기 규칙(2019 개발자 스트림을 전한 플레이어 설명 [2차 Steam 토론]): 블록당 속도 위반 없음 30점 / 경미 15점 / 대량 위반 0점; 정차 위치 정확도·일정 준수도 이벤트 점수.
- TSW4+: 점수 분해 화면(speeding, safety systems, hard impact 등) + **Platinum** 등급, 정차 750/750·승객 적재 500/500 항목, PZB·SIFA 켠 채 완주해도 골드가 나올 수 있음 — *재작업 후 난이도 상향*; 구 루트는 수동 업데이트 필요 [T10 2차].
- 점수 표시는 설정(`Show Score`, Ctrl+6)으로 숨김 가능 [T1/T2]. **TSW5 전용 카테고리 목록은 공식 문서로 확인 못 함** [미확정].

### 2.8 CHOOGuard 적용
| 판정 | 메커닉 | CHOOGuard 동사 | 구체적 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 레티클 링 + `이름  현재값` 툴팁, 동작 문구 없음 | 모든 점검·조작 대상 | 현재 중앙 표시 `E · {정답형 동작}`(`FirstPersonResponder.cs` `CurrentPrompt="E · "+InteractionPrompt`)를 `IFpsNamed.DisplayName` + *관찰 가능한 현재 상태*(예 `가스 중간밸브  열림`, `차단기 3  내려감`)로 교체. 상태 문자열은 대상이 *지각된 경우에만* 갱신(미인지 위험 무노출) | T2, §2.3 |
| **Adopt** | 직접 조작 + 단축키 병행 | 무전·상황 메뉴·도구 | Q 휠/Tab을 "Train Interaction Menu"처럼 *대상에 대한 조회/단축 메뉴*로 두고, 공간 조작(밸브·레버)과 병행. 단축은 편의, 판단 대체 아님 | T1, T11 |
| **Adopt** | Player Assist 프리셋(Beginner/Standard/Expert/Custom) + 항목별 토글, 안전장치는 Expert에서 기본 ON | 길 안내·체크리스트·무전 휠 정답 필터·보고 문장 | 현재 모두 기본 ON(`Shell/GameSettings.cs:31` 등). **기본값 = Standard**: 길 안내 OFF, ○/● 체크리스트 OFF, 무전 휠에 *해당 상황에서 가능한 모든 문장*(오답 포함) 표시, 보고 문장은 인지한 사실로 조합. Beginner에서만 현재 동작. 각 항목이 무엇을 숨기는지 설정창에 표 형태로 명시(TSW는 HUD Type이 불분명해 불만 소지) | T2 |
| **Adopt** | 고장 = 비치명 + 물리 단서 + 정해진 복구 절차 | 발견·점검, 승강문·셔터·에스컬레이터 | "승강문이 살짝 열려 있음", "분전반 차단기가 중간 위치", "셔터가 2/3 지점에서 멈춤" 같은 *관찰 단서*를 점검(inspect)으로 발견 → 실제 직원 복구 절차(재투입·재가동)로 해결. 복구 절차의 순서 오류는 *지연·재발*로 귀결(기존 재투입 재발화 `IncidentDirector.ElectricPlaza.cs:274-312`와 일치) | T3 |
| **Adapt** | 시간 제한 확인(Alerter/AWS/Vigilance) = 숙련 스킬 | 현장 안전 확인·무전 응답 | 역무실의 *주기적 안전 확인 호출*("현장 응답 바랍니다")에 일정 시간 내 무전으로 응답하지 않으면 역무실이 인력 급파·상황 격상(기관 자동 해결이 아니라 *판단 박탈* 비용). 실제 절차 근거는 KORAIL 현장 규정 확인 필요 [추론, 현장 검증 미실시] | T1, 관찰 4:11 |
| **Adapt** | 차장 모드 승객 개별 상호작용(클릭해 표 확인, 부적격이면 안내→다음 정차에 하차) + 짐 치우기 | 대피 유도, 접근 통제, 승객 진정 | 군중 개체에 *안내* 동작을 주면 *즉시가 아니라 일정 시간 뒤* 이동·하차(지연 결과). 대피로의 장애물(짐·가방)을 직접 치우는 동작 추가 → 현재 로그만 남는 진정·안내(`Casualty.cs:370-387`)에 가시적 결과 부여 | T4 |
| **Adopt** | 점수 분해 + 디브리프 그래프 | 근무 종료 평가 | `ShiftLog.cs:348`의 `No score`를 *카테고리 분해*로 교체: 보고 지연, 접근 통제 시점, 인계 품질, 인명 상태 변화, 본인 노출. TSW식 *속도-제한 그래프* → "시간축 위 사건·내 행동 타임라인". 메달/숫자는 선택, 점수 숨김 토글(Ctrl+6) 제공 | T8, T10 |
| **Adapt** | 시나리오 목표 표식/컨트롤 표식을 *모드로* 켜기 | Beginner에서만 | 컨트롤 표식은 *이미 인지한 위험에 관련된 설비*에 한정(인지 전 위치 표시 금지). Expert는 전면 OFF | T5, 제약 |
| **Adapt** | `Reset Simulation Physics` 같은 상태 꼬임 탈출구 | 조작 오류 복구 | 일시정지 메뉴 "조작 상태 초기화"(예: 손에서 놓친 관창 복구) — 시나리오 자체 리셋 아님 | T6 |
| **Reject** | 목표 문구 상태 지정형("Set X to Y") | — | JEV가 사건을 실시간 합성하므로 사전 목표 문구 불가·정답 노출 | T11, 도메인 규칙 |
| **Reject** | Beginner Controls식 "세팅 생략" | — | 캡 시동 개념 없음. 대신 잡일 자동화(보행 경로 길 안내 등)는 Beginner 프리셋으로 | T1 |

---

## 3. PowerWash Simulator (FuturLab) — 노즐 · 잔여 오염 하이라이트 · 부위별 완료 피드백

### 3.1 출처
| # | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| P1 | Steam 뉴스 API `ISteamNews/GetNewsForApp?appid=1290000` (원문: https://store.steampowered.com/news/app/1290000/view/4588693747026283654) | [공식] Dev Log 71 "Dirt Highlight Changes" | 2024-09-16 | 하이라이트 색 7종, 지속 0.9–5 s |
| P2 | 동일 API, Dev Log #7 "Rewind, Ladders & Highlights" | [공식] | 2021-04-07 | 노란 플래시 하이라이트(버튼 한 번), 세부 메뉴 %/부위 |
| P3 | 동일, Dev Log #8 "Soap & Rotations" | [공식] | 2021-04-14 | 노즐 회전, 소프트 종류, 65° 소프 노즐 |
| P4 | 동일, Dev Log #4 "Dirt!" / #10 "Here's The Dirt" | [공식] | 2021-03-17 / 04-28 | 오염 7층, 노즐 폭과 층 대응 |
| P5 | 동일, Dev Log #16 Soap Rework | [공식] | 2021-06-18 | 재질-소프 일치 규칙, 빈 소프 메시지 |
| P6 | 동일, Dev Log 47 "Aim Mode" | [공식] | 2022-03-10 | 고정 시점 세척 모드(멀미) |
| P7 | 동일, Dev Log 53 "Tutorial Pop-ups" | [공식] | 2022-04-22 | 첫 노즐 노랑으로 변경 이유, 튜토리얼 팝업 |
| P8 | 동일, Dev Log 58 "Name that Ding!" | [공식] | 2022-06-02 | 하위 과제(ding) 분할·명명 원칙 |
| P9 | 동일, Dev Log 70 "Getting Dirty With Washing Data" | [공식] | 2024-09-09 | 노즐 사용 순위, ding 밀도 |
| P10 | 동일, "PowerWash Simulator x Oxford Study Final Results" / Steam Awards 글 | [공식] | 2024-09-26 / 2024-11-27 | 72% 기분 상승, 하이라이트 변경 출시 확인 |
| P11 | https://defkey.com/powerwash-simulator-shortcuts | [2차] 기본 키 | 2022-07-21 최초/2025-10-30 갱신 | PC 기본 키 |
| P12 | https://www.youtube.com/watch?v=rG7Iu_29xag | [2차] Cheevo Guides(콘솔 컨트롤러) | — | **프레임 관찰**(HUD·하이라이트·소프 휠) |

### 3.2 조작표
[2차 P11 DefKey, 사용자 변경 가능]: 이동 WASD, Shift 달리기, **Ctrl 서기/웅크리기/엎드리기**, Space 점프, **LMB = Use washer**, **RMB = Toggle washer**, **휠 위/아래 = 다음/이전 노즐**, **X / Z = 장비 시계/반시계 회전**, **R = Rotate nozzle / Refill cleaning liquid**, **C = Aim mode**, **F = Pick up / Place equipment**, **Tab = Show dirt**, E = Equip, 1 = Select extension, 2 = Select nozzle, 3 = Select soap, Esc = Raise/Lower tablet.
*충돌*: Ludo.guide 표(Tab=태블릿, M=지도, 가운데 클릭=Clean specific area, Q/E=수압)는 DefKey·개발자 서술과 맞지 않아 신뢰하지 않음 [2차 충돌]. 콘솔 영상 자막에서 "you can rotate the nozzle by pressing the left trigger" 확인(P12).
노즐(공식 P3/P9): 색으로 구분 — 흰(가장 넓고 약함)·노랑(균형, 첫 노즐)·초록·빨강(좁고 강함), 소프 전용 65° 노즐, 터보(회전 펜슬 제트). 소프는 *소프 노즐에서만* 사용.

### 3.3 잔여 오염 표시 (하이라이트) — 핵심
- **버튼 한 번(키 Tab, P11)으로 모든 *미세척* 오염을 색칠**: "highlight any unwashed grime to you with a yellow flash… at a tap of a button and can quickly lead you to that 0.1% of a job you still need to complete" [P2]. 변경(P1): 색 7종 `Orange(Default) / Red / Green / Blue / Yellow / Pink / White`, **지속 시간 0.9–5 s 슬라이더**("should decrease the need to spam that highlight button"), 동기: "seeing content creators struggle to find some dirt due to the colours… playing games with people that have some form of colour vision deficiency"; *태블릿에서 하위 과제를 고를 때의 펄스는 별개로 유지*.
- **세부 메뉴**: 부위별 이미 세척한 비율(%) 목록 [P2].
- **부위명이 단서**: "Upper Wing Ailerons"처럼 *위치 정보가 이름에 포함*되어 "hunt down those final pesky pieces" [P8]; 99%일 때 이름으로 좁힘.
- **[관찰 https://youtu.be/rG7Iu_29xag t≈6:12]** 레일 전체가 *주황색으로 덮임*(기본색 하이라이트), **t≈6:14** 우측에 토스트 `Rail Cleaned!  +$10.00`, **t≈6:18–6:20** 벽에 *남은 오염만 주황 패치*로 표시 (`pws_highlight_372_392.png`).
- **[관찰 상시 HUD]** 좌상단 원형 `2% CLEANED`(작업 전체 %), 그 옆에 *현재 조준 중인 부위명*(Rail / Skatepark Floor / Upper Floor / Ramp / Wall)과 바, 오염 분류 라벨(`MULTI-PURPOSE`, `METAL`), 우상단 `★★★★★ $1,315.00`, 좌하단 `Open/Close Inventory` 힌트, 중앙은 작은 *링+점* 십자선. **t≈2:30 / 5:00** 방사형 `CLEANING LIQUIDS` 휠(`Stone Cleaner`, `Multi-Purpose Cleaner`) (영상 contact sheet).

### 3.4 조작 문법 · 왜 단순 동사가 숙련처럼 느껴지는가
- **하나의 동사(분사) + 세 개의 *연속 변수***: 노즐 폭(각도), 거리/각도, 방향(회전: 가로/세로 빔 — "Seamlessly switch the nozzle between horizontal and vertical water beams", 회전은 노즐 간 기억 [P3]).
- **오염은 7개 층으로 쌓임**: Layer 1 Surface(BirdPoo, Dust, Egg, Grime, Mud, Sand) → 2 Encrusted(Dirt Streaks) → 3 Embedded(Algae, Lichen, Moss) → 4 Tough(Grime Stains, Mould) → 5 Stubborn(Graffiti) → 6 Ingrained(Rust, Chewing gum) → 7 Oily. "the tougher the dirt is the narrower the nozzle needs to be… Part of the satisfaction of spraying will come in essentially peeling each layer back" [P4]. 따라서 *도구 적합*이 효율을 좌우하고 틀려도 *느릴 뿐* 실패는 없다.
- **재질-소프 일치 규칙**: "using glass cleaner on a wooden fence now will do much less cleaning" [P5]; 화면 좌상단이 "which category the surface falls under and which soap type will clean the fastest" 알려 줌 [P3] — 힌트가 *지각 정보*(재질 분류)로만 제공됨.
- **기본 선택으로 오류 예방**: 스트리머들이 첫 노즐 빨강으로 밴 전체를 닦는 것을 보고 "We changed the first nozzle attached to be yellow" [P7] — 가르치려 하지 않고 *기본값을 바꿔* 잘못 시작을 줄임.
- **쾌감 설계(ding)**: 작업을 하위 과제로 잘게 나눠 "satisfying combo of dings"; 작은 작업은 "one ding every eight seconds"(Dirt Bike), 큰 스케이트파크는 "around two dings per minute" [P8/P9].
- **데이터로 본 도구 선호**: 사용량 노랑 > 초록(3배 이상) / 빨강 3위 [P9] — 균형형 기본 도구가 실제로 가장 쓰임.

### 3.5 안내 · 실패 · 평가
- **안내**: 첫 로드 때 한 번 뜨는 *튜토리얼 팝업*("guide new players through many mapped controls… rotating nozzles, selecting attachments, aim mode… never be seen again unless you start a new save") [P7]; 이후 위키/커뮤니티.
- **실패 없음**: 스토어 문구 "Absorb the relaxing atmosphere and stress-free pace"; 소프 소진은 메시지 팝업 + 보급 프롬프트로 "to help combat confusion when the spray stops cleaning" [P5]. 시간·체력 압박 없음(챌린지 모드는 별도: best time / least water [Dev Log #14 공식]).
- **평가**: 상시 작업 %·부위별 %·돈(부위 완료마다 팝업 "how much you received… and your current total cash" [Dev Log #3 공식]), 작업 종료 후 *와시 리플레이*(고정 카메라 녹화 [P2]), 별 등급, Free Play·특별 작업 보너스 25%(Dev Log 49는 "coming in the future"로 예고 — 출시 여부 [미확정]).
- **웰빙 연구**: Oxford Internet Institute, 8,695명·162,325 기분 보고: "a predicted 72% of players experience an uplift in mood", 15분 후 기분이 시작보다 높음 [P10].
- **접근성**: Aim Mode(고정 시점 세척; 멀미) — "your washer will move independently of your camera… view only turns if your crosshair reaches the edge of your screen" [P6].

### 3.6 CHOOGuard 적용
| 판정 | 메커닉 | CHOOGuard 동사 | 구체적 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 온디맨드 하이라이트(색·지속시간 조절) | 소화(잔불), 대피 유도(남은 인원), 발견 | 키 하나(예: Tab 짧게)로 **이미 인지했으나 미해결**인 대상만 0.9–5 s 동안 윤곽 표시. 인지하지 못한 위험은 어떤 경우에도 하이라이트 금지(시야 기반 발견 `IncidentDirector.cs:456-474` 재사용). 색·지속 설정 제공 | P1, P2, 제약 |
| **Adopt** | 상시 부위 % + 이름 + 분류 라벨 | 소화·대피·환자 | 조준 중인 대상(인지된 것만)의 *상태 바*: 불 크기, 환자 단계(0–4), 대피 인원 n/N. 라벨은 지각 가능한 분류(재질·불 종류 색/연기)로, 정답 약제명 직접 표기는 금지 | §3.3 관찰 |
| **Adopt** | 부위(하위 과제) 완료 시 개별 `ding` + 토스트 | 모든 동사 | 소화기 불 구역 소화, 한 승강장 대피 완료, 밸브 잠금 완료 각각에 고유 SFX + `OO 완료`(금액 대신 **기여 항목**). 현재는 토스트 오답 지적(`KitchenGas.cs:311`)이 주 → 성공 신호 위주로 | P8, §3.3 |
| **Adapt** | 도구 폭·층 대응(좁을수록 센 오염), 재질-소프 일치 | 소화기 약제, 호스 수압, 지연 진화 | 이미 약제 종류 규칙·숨은 불량 있음(`FireHazard.Oil.cs:41-53`). **오류 시 "실패" 대신 진행 속도 저하**와 이펙트 차이로 표현, 지시문 금지. 불을 *층(표면불→심부불)*으로 확장해 같은 도구로 한 번에 안 꺼지게 | P4, P5 |
| **Adapt** | 분사가 *칠하기식 마스크* 제거 | 소화기 조준 | 불 베이스를 셀 마스크로 보고 쓸기(sweep)로 면적 제거 → 단순 체류시간(14 s 약제) 대신 *커버리지* 과제. 현재 거리·각도·베이스 조준 품질에 얹음 [추론] | P4, §3.4 |
| **Adopt** | 위치가 이름에 포함된 하위 과제 | 무전 보고 | 보고 문장 선택지를 *인지한 위치명*으로 조합("승강장 3-1 동쪽 벤치 뒤") → 정답 문구를 미리 쓰지 않고 위치 정확도가 보고 품질. `IncidentDirector.cs:552-553`의 미리 쓰인 문장 제거 | P8 |
| **Adapt** | 방사형 장비 휠, 방향 회전 기억 | 도구 선택(소화기/호스/AED) | 기존 Q 무전 휠과 분리된 도구 휠(숫자/휠 스크롤) 도입 — 현재 숫자·휠 입력 없음(§0.1). 도구는 *손에 든 것만* 작동 | P3, P11 |
| **Adopt** | 튜토리얼은 새 문법 처음 만날 때 1회 팝업 | 홀드·드래그 첫 사용 | 밸브를 처음 쥘 때 "마우스를 돌려 보세요" 1회, 설정에서 끄기/재표시 | P7, F1 |
| **Adapt** | Aim Mode(고정 시점) | 호스·소화기 | 멀미 민감자용 *시점 고정 조준* 옵션 | P6 |
| **Reject** | 돈·상점·별 등급·시간 압박 없음의 극단 | — | CHOOGuard는 현실 책임 판단이 핵심이므로 *완전 무결과*는 부적합. PWS의 "무실패"는 **도구 숙련 영역(소화기 등) 한정**으로만 차용 | 비교 |

---

## 4. Escape from Tarkov (Battlestate Games) — 노-HUD 정보 설계

### 4.1 출처
| # | URL | 종류 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| E1 | https://store.steampowered.com/api/appdetails?appids=3932890 (스토어 https://store.steampowered.com/app/3932890/) | [공식] 스토어 | 출시 2025-11-15 | 핵심 위험-보상 문구 |
| E2 | Steam 뉴스 API appid=3932890, "Patch 1.1.0.0" | [공식] | 2026-08-03 | 자물쇠 문, 폭발 소화기·가스통, 엘리베이터 문 |
| E3 | https://escapefromtarkov.fandom.com/wiki/Controls (API parse) | [2차] | 2026-10 조회 | 기본 키 |
| E4 | https://escapefromtarkov.fandom.com/wiki/Health_system | [2차] | 2026-10 조회 | 부위별 규칙, 출혈·골절·통증 |
| E5 | https://escapefromtarkov.fandom.com/wiki/Mag_Drills, /Attention, /Intellect | [2차] | 2026-10 조회 | 확인 정확도 단계, 조사·수색 속도 |
| E6 | https://escapefromtarkov.fandom.com/wiki/How_to_Play_Guide_for_Escape_from_Tarkov | [2차] | 2026-10 조회 | MMB 조사, 탈출구 단서 |
| E7 | https://forum.escapefromtarkov.com/topic/108057-cannot-figure-it-out/ (페이지 본문은 로드 실패, **검색 스니펫으로만 확인**: "go near it, then use the scroll wheel to select the action (opening, kicking, ...) and then press F"); 보강: r/EscapefromTarkov 스레드 제목 "How do you select between Open/Breach or Search/Take in EFT?" | [공식 포럼 사용자 답변 + 2차 reddit 제목] | 2019-12 / 2024 | 문 = 접근 후 휠로 동작 선택, F 실행 |
| E8 | https://www.reddit.com/r/SPTarkov/comments/11owlh4/ | [2차] | 2023 | 조사 상태는 프로필 목록(예: ExaminedByDefault) |
| E9 | https://www.youtube.com/watch?v=eEx40JSuDKU | [2차] JonnyBooSock 건강 시스템 영상 | — | **프레임 관찰**(Health 탭, 인-레이드 HUD) |

### 4.2 조작표 (E3 [2차] — 공식 지식베이스 페이지는 JS 렌더링으로 텍스트 확인 불가)
| 동사 | 입력 |
|---|---|
| 상호작용 | **F = Interact** (문 앞에서 *마우스 휠 위/아래*로 동작 선택 후 F — "Mousewheel up, down: … go up, down in context menus. E.g. for doors") |
| 이동 속도 | 휠 = 속도 증감(느린 걷기는 소음 최소), Caps Lock = 최소 속도 토글 |
| 자세 | Q/E 유지 = 기울이기(최대), Left Alt + A/D = 가변 기울이기, C+휠 = 자세 높이 |
| 아이템 조사 | **Middle Mouse Button** = 인터페이스에서 *아이템 조사(examine)*, 탄창 확인/잔량 추정, 개머리판 접기 / 자유 시점 |
| 탄창 확인 | **Left Alt + T** = Check magazine for remaining ammo / Left Shift + T = Check weapon chamber / L = Inspect weapon (고장 식별) |
| 기타 | Tab 인벤토리, M 마지막 노트/오디오, O 남은 레이드 시간 확인(2회: 탈출구) |

### 4.3 정보 설계 (노-HUD)
- **인-레이드 HUD가 극소**: [관찰 https://youtu.be/eEx40JSuDKU t≈2:23] 상단 퀵 슬롯(`SR-25 / IFAK / Propital / CALOK-B`), 좌하단 *자세 실루엣 + 스태미나 바*, 우하단 탄 수 `50`, 좌하단 구석 빌드 문자열. 체력 바·미니맵·목표 마커 없음 (`eft_health_t143.png`). 시간 확인도 O 키 요청식 [E3].
- **체력은 Tab > Health 탭에서만**: [관찰 t≈0:41] 7개 부위 `HEAD 35/35`, `THORAX 85/85`, `STOMACH 70/70`, `RIGHT ARM 60/60`, `LEFT ARM 60/60`, `RIGHT LEG 65/65`, `LEFT LEG 65/65` (합 440) (`eft_health_t41.png`).
- **부위별 규칙**(E4): Head/Thorax는 골절 불가, 파괴 시 즉사. 팔 골절 = 아이템 사용·컨테이너 수색 50%↓ 느림, 재장전·조준 67%↑ 시간. 다리 골절 = 속도 −45%, 점프↓, 진통제 없으면 달리기 불가, 달리면 추가 피해. 위(Stomach) 파괴 = 탈수·에너지 손실 5배. 출혈: 경(1.36 HP/6 s, 에너지 5/min)·중(1.53 HP/4 s, 에너지 6/min, 혈흔), 붕대 후 Fresh Wound(480 s), 통증·탈수·피로(탈수 −1 HP/부위/15 s, 피로 −1 HP/부위/5 s)와 *각각 다른 치료*(부목·붕대·진통제·수술키트: 파괴된 부위는 CMS/Surv12로만 복구, 최대 HP 제한).
- **상태는 지각 단서로도 전달**: 진통제 = "Black-and-white peripheral vision, ignores fractures, and ignores pain"; 탈수 = "Grants Hands tremor effect"; 피로 = "Tunnel vision"; 부상 걸음 = 소음·속도 변화 [E4].
- **조사(examine)와 탐색은 *시간 비용 있는 요청***: 미확인 아이템은 이름·가치가 가려지고 `?`로 보임(프로필 단위 상태 저장 [E8 2차]); MMB로 조사; `Attention` 스킬 "Increases item examination speed (2% per level → +100% at Elite)", `Intellect`도 조사 속도 증가, "Increases looting speed" [E5]. 조사·수색 중엔 무방비.
- **정보 정밀도 단계(Mag Drills)**: 레벨 0 = `Full / Nearly full / About half / Fewer than half / Almost empty / Empty`; 레벨 10 = `Approx. 15`식 추정; 레벨 20 = 정확한 발수. 투명 탄창·관찰창은 숙련과 무관하게 레벨 1 [E5 2차]. 확인 속도도 숙련이 올림(+40% at Elite).
- **환경 단서가 마커를 대신**: 탈출구가 때로 활성화됨("Some exits are always available, whilst others are only occasionally (indicated by question marks)… look for active lamps, spotlights, or green smoke") [E6 2차]. 문은 접근하면 *가능한 동작 목록*(Open/Breach/kick 등)이 뜨고 휠로 선택 후 F [E7, 스니펫 한정]. 목표 표식 부재는 관찰 영상(§4.3 첫 항목)으로만 확인 — 공식 설명 문서는 못 찾음 [미확정].
- **공식 패치(1.1.0.0, 2026-08-03)**: "Added new interactive mechanics across locations: Padlocks on certain doors; Padlocks on certain containers; Openable elevator doors… Breakable bottles; **Exploding fire extinguishers**; Vehicles that emit smoke after being hit. Exploding gas cylinders have also been added to additional locations." [E2] — 환경 설비가 *플레이어 행동에 반응*하는 위험원이 되는 방향.

### 4.4 실패 · 결과
- 사망 = 가지고 간 장비 상실: "only extraction decides if you live or lose it all" [E1 공식]. 무지의 비용은 (a) 시간(조사·수색 중 무방비), (b) 정보 오류(탄창 잔량 오판), (c) 신체 디버프, (d) 소음.
- 상태 악화는 *시간 경과*로 진행(출혈·탈수·피로 틱) → 방치하면 사망으로 이어지는 지연 실패 [E4].
- 학습 경로는 위키·커뮤니티 의존; 튜토리얼 부재의 대가가 크다는 점이 이 게임의 진입장벽 [2차].

### 4.5 평가 모델
- 레이드 결과(생존/전리품)와 XP·스킬 상승이 사실상 평가. 스킬은 *사용을 통한 향상*(달리면 지구력 등 [영상 자막 2차]). 공식 점수/디브리프는 확인 못 함 [미확정].

### 4.6 CHOOGuard 적용
| 판정 | 메커닉 | CHOOGuard 동사 | 구체적 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 정보 정밀도 단계(대략→근사→정확), 확인에 시간 소요 | 환자 평가, 소화기 잔량, 군중 수, 보고 | 소화기 게이지는 *바늘 영역*(충분/부족/빔)→ 가까이 확인 시 수치. 환자 평가(상태 확인)는 0–4단계 모델(`Casualty.cs:42-79`)을 "의식 있음/없음→호흡 이상→정확한 평가 결과"로 *시간 투자에 따라 정밀화*. 보고 품질이 정밀도에 비례 | E5 |
| **Adopt** | 조사(examine) = 시간 비용·중단 가능한 홀드 | 발견·점검 | 미확인 물체/가방/표지는 `?`(미확인)로, *N초 홀드*로 지각 가능한 정보만 공개(보이는 라벨·냄새·소리). 홀드 중 사건이 커지면 중단, 그동안 무방비 → 판단의 비용. 숨은 위험 투시 금지 | E5, E8 |
| **Adopt** | 휠 문맥 메뉴가 *대상이 허용하는 모든 동사*를 나열(정답 필터 없음) | 문·셔터·밸브·방송·무전 | `Q` 무전 휠(`EmergencySession.cs:338-344`)을 "지금 맞는 문장"만이 아니라 *가능한 모든 보고/요청*으로 확장; 문은 열기/닫기/잠금확인/두드려 확인 등. 현실 절차 밖(차 부수기·파괴)은 제외 — 직원 절차만 [도메인 규칙] | E3, E7 |
| **Adopt** | 부위별 상태 + 상태별 다른 치료, HUD에는 최소 정보 | 본인 위험(연기·열 노출) | 본인 노출 상태 모델: 연기 흡입(기침·시야 흐림·시야 좁아짐), 열(탈수·피부), 피로(스태미나). 상시 HUD 수치 없이 **지각 단서**(기침 SFX, 가장자리 비네트 현재 `GameHud.cs:120,192-194`를 상태별 분화, 홀드 조작 떨림)와 Tab "내 상태". 치료/회복은 *대피·환기·휴식* 등 역무원 가능 행동 | E4 |
| **Adopt** | 시계·지도를 요청식으로 | 길 안내·시간 | 바닥 점선·나침반·지도 경로 기본 ON(`GameSettings.cs:31`)을 Standard에서 OFF, 지도는 M으로 *열람만*(경로선 없음), 시계는 손목시계 키(O)로 확인 | E3, 관찰 |
| **Adapt** | 환경 설비가 위험원이 됨(폭발 소화기·가스통) | 위험 일반화 | 위험은 *객체 속성*(가연, 압력용기, 전원 활성)으로 정의하고 플레이어의 오조작이 확산(이미 활성 전원+물=감전 `IncidentDirector.ElectricPlaza.cs:318-323`). 시나리오 고정 목록 아님 | E2 |
| **Adopt** | 자물쇠·열쇠로 접근 제어 | 접근 통제 | 자물쇠 상태가 문에 보이고, 열쇠 보유 여부가 선택지를 바꿈 | E2 |
| **Adapt** | 무지의 비용을 *죽음*이 아닌 *시간·확대·본인 노출*로 환산 | 전체 | 무조건 영구 손실은 거부. 미조사·미확인으로 인한 지연이 위험 확대로 이어지는 연쇄를 설계 | E1, E4 |
| **Adapt** | 학습은 외부 위키 → 게임 내 *매뉴얼 소품* | 학습 | 역무실 책상의 업무 매뉴얼/현장 수첩을 *읽는 데 시간*이 드는 오브젝트로(가스 밸브 위치, 소화기 종류 표). 프롬프트 대신 사용자가 능동 참조. 읽는 동안 사건은 계속 진행 | E7 비교, T6 |
| **Reject** | 사망 = 전 장비 상실, PvP 위협, 소음-AI | — | 장르·기능 불일치 | E1 |

---

## 5. 교차 패턴 (Lane B 네 게임 공통)

1. **"이름 + 상태"만 보여 주고, 동작은 플레이어가 선택한다.** TSW 툴팁(`Master Key  Off`), Tarkov 부위 HP, PWS 부위 % 바, Amnesia 아이템 아이콘+점멸. 정답을 지시하는 문구는 어디에도 없다. CHOOGuard의 `E · 가스 중간밸브 잠그기`는 이 패턴의 반대.
2. **연속 입력으로 상태를 만든다.** 마우스 이동량(Amnesia), 클릭-드래그 노치(TSW 추정), 분사 폭/거리(PWS), 홀드 시간(Tarkov 조사). 완료는 *물리 한계 도달* 또는 *진행 바*이지 "성공 메시지"가 아니다.
3. **정보는 요청식 + 정밀도 비용.** PWS Tab 하이라이트(0.9–5 s), Tarkov MMB 조사/Alt+T 탄창, Amnesia M 기억 열람, TSW Tab 메뉴·T 목표 목록. 상시 표시는 *최소*(Tarkov 스태미나·탄 수, PWS 작업 %).
4. **어시스트는 잡일/보조 UI를 줄일 뿐 판단은 남긴다**(TSW AI Conductor: 문 취급만, Beginner Controls: 캡 세팅만; 안전장치는 숙련 선택). 프리셋 이름과 항목 분해를 병행.
5. **소프트 실패 + 단서 있는 지연 결과.** Amnesia 월드 변화, TSW 비치명 고장(문이 약간 열림 → 열고 닫기), 안전장치 응답 지연 → 제동, Tarkov 출혈 틱, PWS는 실패 없음. *방치해도 시스템이 대신 해결*하는 CHOOGuard 현행(§0.3)과 정반대.
6. **성공 피드백은 작게 자주(ding)**: PWS 부위 완료 소리+토스트, TSW 객관 알림(`AWS Reset`), Amnesia `Picked up…`. 잘못했을 때의 교정 문구가 아니라.
7. **홀드 대안은 필수 접근성 옵션**(Bunker "no input holds"/Physics Interaction Mode; PWS Aim Mode; TSW 패드·Beginner).
8. **사후 분해 평가**: TSW 디브리프(점수·그래프·정차 정확도), PWS 리플레이·% 목록, Amnesia/ Tarkov는 사실상 없음 → 평가는 *선택*이어야 함.

### 5.1 Lane B 관점 우선순위 제안 (사용자 불만 "그냥 지시 따라가기" 해소 기준)
| 순위 | 변경 | 효과 | 영향 코드 지점 |
|---|---|---|---|
| 1 | 중앙 판독을 `이름 + 현재 상태`로, 동작 문구·정답 동사 제거 | 정답 노출 제거 | `FirstPersonResponder.RefreshInteraction` 프롬프트 생성부, 각 `IFpsInteraction.InteractionPrompt` (약 30곳) |
| 2 | `IFpsHoldInteraction`(홀드+마우스 Δ) 도입 → 밸브·레버·문·소화전 밸브에 우선 적용 | 조작이 *손기술*로 | `IFpsInteraction.cs`, `FirstPersonResponder.Simulate`(`interactHeld` 이미 전달됨), `SuppressLookInput` |
| 3 | 보조 수준 프리셋 + 항목별 토글, 기본 Standard | "지시 루프" 완화 | `Shell/GameSettings.cs`, `Hud/WorldRouteGuide.cs`, 무전 휠, Tab 상황판 |
| 4 | 정밀도 단계형 정보(게이지·환자·군중) + 조사 홀드 | 판단의 재료 확보 비용 | `Casualty.cs`, `StaffTools.cs`, 발견 로직 |
| 5 | 방치 시 대리 해결 제거/약화 + 월드 변화형 결과 | 결과가 생김 | `IncidentDirector.Facility.cs:381-396`, `Responders.cs:273-283`, `IncidentDirector.cs:176-182` |
| 6 | 근무 종료 카테고리 분해 + 타임라인 | 평가 | `ShiftLog.cs:348` |

(근거 코드 줄 번호는 `findings.md` §0.1–0.3의 감사 결과를 그대로 사용; 본 레인에서는 `IFpsInteraction.cs`, `FirstPersonResponder.cs:95-175`만 직접 읽음.)

---

## 6. 제약 점검 (CHOOGuard 하드 제약과의 정합)
- *역무원이 실제로 하는 일만*: 문 부수기·강제 진입(Tarkov Breach/Kick), 던지기·밀치기(Amnesia RMB)는 **제외**. 소화기·옥내소화전·밸브·차단기·접근 통제·대피 유도·환자 곁 지키기·AED는 허용(기존 범위).
- *시나리오 일반성*: 제안 메커닉(홀드 조작, 상태 툴팁, 조사 홀드, 정밀도 단계, 하이라이트)은 모두 *대상 속성·상태*로 정의 가능 → 사건 종류 고정 가정 없음.
- *미인지 위험 비노출*: 하이라이트/마커/상태 표시는 `perceived` 플래그를 통과한 객체에만. PWS식 전체 오염 하이라이트를 그대로 가져오지 말 것(PWS는 "남은 오염"이 이미 플레이어 목표로 알려져 있는 구조).

## 7. 공백 · 한계 (미해결 또는 불확실)
1. **TSW 마우스 캡 조작의 정확한 제스처**(클릭 홀드 드래그 vs 휠 스텝 vs 클릭 토글)를 공식 문서·영상 자막에서 확인하지 못했다. 관찰된 것은 호버 툴팁·링·노치 점·클릭 후 값 변경뿐.
2. **TSW5 전용 점수 카테고리 표**와 HUD Type별 숨김 항목의 공식 표 없음. TSW4 이후 분해 화면 존재만 2차로 확인.
3. **관찰 영상이 TSW6 시대**(Class 375/3, 2026-03). TSW5 직접 영상은 없음 — 캡 UI 공통성은 [추론].
4. **Frictional**: 공식 영상은 2010 트레일러로 마우스 궤적 비가시. Bunker의 *정확한* 키·홀드 사양은 이미지 표(F8)라 텍스트로 확인 못 함. SOMA 상호작용은 추가 조사 안 함.
5. **Tarkov**: 공식 지식베이스(escapefromtarkov.com/support/knowledge/450)는 JS 렌더링으로 본문 비확인 — 키/건강 규칙은 Fandom [2차]. 문 컨텍스트 메뉴의 정확한 항목(Open/Breach/Kick/Unlock)·소요 시간은 영상 프레임으로 확인 못 함(휠+F만 포럼 답변 근거). 관찰 빌드 0.12.12 vs 현행 1.1.5.
6. **PWS**: 영상은 콘솔 컨트롤러 플레이. PC 키는 DefKey [2차] 한 곳 — 개발자 글(노즐 회전·Aim Mode·하이라이트)과 모순 없음을 확인. 하이라이트 *키 이름*은 Tab(DefKey `Show dirt`)이나 현행 기본값 확인 못 함.
7. **직접 플레이 없음**: 저항감·지연·마우스 감도 등 "느낌" 수치는 확인 불가. 특히 CHOOGuard 홀드-드래그의 각도 이득/데드존은 프로토타입에서 튜닝 필요.
8. **현장 근거 필요한 제안**: 밸브 핸들 열림/닫힘 시각 관례, 주기적 안전 확인 호출은 [추론]이며 KORAIL 실제 설비·규정 확인 전 구현 금지.

## 8. 미디어 색인 (`media/B/`, 전부 git-ignored)
| 파일 | 내용 | 출처 |
|---|---|---|
| `frictional_physics_2ve0eVwjv5k.mkv` | Frictional 공식 물리 영상 전체(2:15, 480p급) | https://youtu.be/2ve0eVwjv5k |
| `phys_wheel_t98.png` | 슬롯 레버(t≈1:38) | 위 영상 |
| `phys_acc_grid.png` | t≈1:37–1:48 contact sheet(레버→사다리 커서→휠) | 위 영상 |
| `phys_wheel_t105/106/107.png` | 스포크 휠 | 위 영상 |
| `phys_10_34.png`, `phys_34_70.png`, `phys_70_136.png`, `phys_door_14_24.png`, `phys_grid.png` | 구간 contact sheet(문 `Picked up Drill Part` 등) | 위 영상 |
| `tsw_Zt3L8xbqIg8.webm` | GoTraction Class 375/3 튜토리얼 | https://youtu.be/Zt3L8xbqIg8 |
| `tsw_375_t21_reverser_crop.png`, `tsw_375_t22_crop.png` | `Master Key  Off` → `On` 툴팁 | 위 영상 t≈0:21/0:22 |
| `tsw_375_12_30.png` | 리버서 `Off → Forward` 노치 링 포함 | t≈0:12–0:30 |
| `tsw_375_vigilance_246_270.png` | 우측 알림 줄(`AWS Reset`, `Vigilance Pedal`) | t≈4:06–4:30 |
| `tsw_375_340_364.png`, `tsw_375_grid.png` | 목표 HUD / 전체 contact sheet | |
| `tsw_yAEIObsoZdk.webm`, `tsw_guard_grid.png` | 가드(차장) 모드 | https://youtu.be/yAEIObsoZdk |
| `pws_rG7Iu_29xag.webm`, `pws_highlight_372_392.png` | PWS 하이라이트·`Rail Cleaned! +$10.00`·HUD | https://youtu.be/rG7Iu_29xag t≈6:12–6:32 |
| `eft_eEx40JSuDKU.webm`, `eft_health_t41.png`, `eft_health_t143.png`, `eft_health_grid.png`, `eft_health_660_720.png` | Tarkov Health 탭 / 인-레이드 HUD | https://youtu.be/eEx40JSuDKU t≈0:41, 2:23 |
