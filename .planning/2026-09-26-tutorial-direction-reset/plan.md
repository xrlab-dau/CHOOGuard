# 2026-09-26 방향 재정렬 — 본게임 MVP 우선

사용자 확정 원문과 반영은 [결정 원장 4차](../../docs/CHOOGuard_FPS_Prior_Research_20260925/DECISIONS_AND_DEVELOPMENT_STRUCTURE.md)가 원본이다. 이 문서는 실행 계획·근거 색인과 [구현 결과](#구현-결과-2026-09-26)를 담는다.

## 이탈 원인

- 2026-09-25 설계문서가 “튜토리얼은 본게임과 별도 실행 공간”(INTERACTION_TUTORIAL §1), “연습 지그는 부산역 복원 모델과 분리”(§9), “모델링 세션의 씬·prefab·메시를 변경하지 않고 별도 임시 Unity 프로젝트에서 실행”(EXECUTION_PLAN §0)을 지시했다.
- 구현은 이를 별도 맵으로 해석해 `GameplayPracticeLaboratory`(허공의 박스)·300 캡슐 NPC·41버튼 패널·메뉴로 만드는 미저장 진입 장면을 만들었다. 빌드 목록의 Bootstrap → FpsStation 흐름, 기존 FPS 코어(`FirstPersonResponder`·`TutorialSession`·`FacilityInspectable`), 사용자가 9/24 에디터에서 기존 튜토리얼 루트를 직접 삭제한 사실(`2026-09-24-interior-twin/verification.json`)이 반영되지 않았다.

## JEV 판정 (jev-1.13.0, authority 0)

| 기록 | 사용자 답과의 관계 | 핵심 |
|---|---|---|
| `jev-direction-tutorial-001` | 공간·진입·Astra 처리는 사용자 확정 | 같은 맵·모드 분리 0.82, 현장 첫 근무 구조 0.94, 디제틱 최소 1.00, 역무 먼저 0.97, 타이틀→역 0.65, 기존 코어 재사용 0.88, Astra 화면 제거·백엔드 선별 0.61, 가산 게임플레이 씬 0.85, 승객 0.44, Astra 적합도 0.99/4 |
| `jev-first-lesson-hud-002` | 첫 레슨 판정은 튜토리얼 MVP 제외로 보류 | 소화기 외관점검 0.90, 부산역 OJT 틀 0.92, 공개 점검표 재구성 1.00, 지적확인 0.68(사용자: 제외), PUBG 가장자리+MSC 중앙 0.98 |
| `jev-main-game-mvp-003` | 첫 사건 판정은 ‘고정 사건 없음’으로 대체 | 화재 0.87, NPC 행동 기인 0.52, 역무원만 0.55, 약 100명+핵심 JEV 0.76, 동적 전개 0.60, 인계 종료 0.91, 기존 스타일 외형 0.92(사용자: 사실적 캐릭터 확보) |
| `jev-mvp-scope-order-004` | 사용자 답 반영 후 판정 | 위험 계열 사고·테러 징후·자연재해 각 1종 0.94, 빌드 순서 shell_first 1.00, JEV 미연결 시 로컬 규칙 지속 1.00 |
| `jev-hud-code-source-005` | 002의 HUD 코드 출처 판정 대체 | 공개 저장소이므로 자체 구현+구조 참고 1.00 (FPS Microgame 스크립트 복사 0) |

## MVP 정의

- **진입:** Bootstrap을 타이틀(시작·설정·종료)로 재구성 → FpsStation 로드 → 역무원으로 근무 시작.
- **플레이어:** KORAIL 역무원 한 역할. 이동·시점·E 상호작용.
- **세계:** 승객 약 100명. 이동·일상 행동은 로컬 내비게이션, 이상 인지·반응·협조·의도 전환과 사건 전개만 JEV. JEV 미연결 시 로컬 규칙으로 계속하고 기록한다.
- **비상상황:** 대본·고정 첫 사건 없음. 원자적 인과 전이 3계열을 구현하고 NPC 행동·환경 상태에서 JEV가 현재 유효한 후보를 골라 전개한다.
  - 사고: NPC 소지품·부주의 발화 → 연기 확산.
  - 테러 징후: 적대 NPC의 거동수상 물건 방치·위협. 징후와 대응(신고·접근 통제·대피·인계)만 표현한다.
  - 자연재해: 지진동 → 낙하물·에스컬레이터 정지.
- **대응:** 관찰, 무전 신고, 승객 유도, 접근 통제, 법규 배치 소화기 12대 초기 진화, 전문기관 인계. 공개 행동요령 근거이며 코레일 내부 SOP로 표시하지 않는다.
- **종료:** 초기 대응 후 소방·경찰 도착과 인계로 판 종료. 실제 일어난 결과만 짧게 표시하고 별도 복기 기능은 두지 않는다.
- **HUD:** PUBG식 가장자리(상단 나침반+업무 마커, 우상단 무전 피드, 하단 손에 든 장비) + MSC식 중앙(대상 이름+행동, 홀드 진행 링). TAB 작업지시서·장비, M 역사 안내도, Esc 일시정지. 그 외 상시 표시 없음.

## 구현 순서

1. Astra 표현층 제거, 타이틀 → FpsStation 흐름, PUBG/MSC HUD, 일시정지 메뉴.
2. 보존 백엔드 검토와 FpsStation 결속(내비게이션, 월드 상태, JEV 클라이언트).
3. 라이선스 확인된 사실적 리깅 캐릭터 확보 → 승객 약 100명 일상 행동.
4. JEV 핵심 순간 판단과 동적 전개.
5. 위험 3계열과 역무원 대응 동사.
6. 인계 종료.

## Astra 작업 처리

브랜치 `work/fps-local-integration-20260925`(로컬), `origin/cloud/fps-gameplay-20260925`, `backup/astra-worktree-20260926`에 보존돼 있다.

- **제거(작업 트리):** 표현층(`GameplayBootstrap`·`GameplayHud`·`GameplayPracticeLaboratory`·`GameplayLaunchMenu`), 합성 직무 커리큘럼(`RoleTraining*`), `practice-seed.json`, 해당 시험, `BuildBaseline`의 결선. 선별 검토 대상이던 `Contracts/Domain/Application/Gameplay`, `App/Fps/Runtime`, `workers/prediction/gameplay-*` 도 FpsStation 결속에 쓰지 않았고 작업 트리에서 뺐다(브랜치 보존).
- **새로 구현:** 본게임 런타임은 `App/Fps/Emergency`(세계·군중·사건·도구·기록)에 새로 썼다. JEV 호출은 게임이 만든 후보만 순위를 매기게 하는 직접 클라이언트(`JevClient`)다.
- **유지:** 기존 FPS 코어와 소화기 12대. 튜토리얼 코드(`TutorialSession`·`AuditTerminal`)는 MVP에서 쓰지 않지만 삭제하지 않는다.

## 선행조건·한계

- 게임 런타임 JEV: 키는 저장소 밖(`TYPESAFE_API_KEY` 또는 `~/.chooguard/typesafe.key`)에서 읽는다. 키가 없거나 응답이 늦으면(2.4 s) 로컬 가중치 규칙으로 계속하고, 결과 화면과 기록에 JEV/규칙 판단 수를 남긴다.
- 사실적 리깅 캐릭터: Microsoft Rocketbox(MIT) 19종·동작 24개. 고지는 `ThirdParty/Licenses/NOTICE.txt`.
- 물리 워커는 MVP 에서 쓰지 않는다(30×20×4 m 기준 홀·0–120 s 범위만 검증된 상태 그대로).
- 공개 행동요령은 시민·일반 기준이다. 코레일 내부 SOP·부산역 비상대응계획은 확보 불가(사용자 확인). 게임 안 문구도 내부 SOP 로 표시하지 않는다.
- 시간 압축: 불 성장(0.0035/s), 연기 흡입 부상(90 s), 기관 도착(50–140 s)은 게임 조정값이며 소방·의학 수치가 아니다.

## 조사 산출물

- [코레일 교육·매뉴얼](research-korail-training.md): 인재개발원(의왕 본원+5개 교육원), 역무원 법정 안전교육(분기 6시간), 공개 매뉴얼 범위와 공백.
- [HUD 레퍼런스](research-hud-references.md): PUBG·MSC·TSW·PowerWash 관찰, UI Extensions(BSD-3)·Interaction Toolkit(MPL-2.0).
- FPS Microgame(Unity, Asset Store EULA): `Compass`·`CompassMarker`·`NotificationToast`·`ObjectiveHUDManager`·`InGameMenuManager`·`LoadSceneButton` 구조 참고용. 공개 저장소이므로 소스 복사 금지.

## 구현 결과 (2026-09-26)

### 실행

- 빌드 순서: `Bootstrap`(타이틀) → `FpsStation`(부산역 트윈, 수정하지 않음) + `StationEmergency`(가산 로드, 근무 세션). 에디터에서 `FpsStation` 을 바로 재생해도 세션이 붙는다.
- 생성 메뉴와 산출물은 역 전체 단계에서 바뀌었다: [world-npc-incidents 실행](../2026-09-26-world-npc-incidents/plan.md#실행).

### 세계와 군중 (MVP 당시 — 역 전체로 대체, [world-npc-incidents](../2026-09-26-world-npc-incidents/plan.md#세계열차승객))

- 2층 맞이방 전용 navmesh(`StationHall.navmesh.asset`, 런타임에만 추가)와 지점 910개: 출입 4, 매표창구 9, 점포 19, 대기 85, **대합실 의자 792**(의자 132개를 0.1 m 격자로 분리해 팔걸이 사이 3칸×양쪽), 역무실 1. 승강장 에스컬레이터 5곳은 navmesh 로 닿지 않아 제외.
- 승객 약 100명(Rocketbox 성인 12종): 표 구매·점포 둘러보기·의자에 앉기/서서 기다리기·떠나기, 출입구로 계속 드나듦. 앉기 동작은 표본 측정한 엉덩이 오프셋(뒤 0.46 m/앞 0.42 m)으로 의자 높이 0.45 m 에 맞췄다.

### 사건과 JEV (MVP 당시 — 원자 전이 합성으로 대체, [world-npc-incidents](../2026-09-26-world-npc-incidents/plan.md#사건-합성))

- 고정 첫 사건 없음: 55–110 s 평온 뒤 20 s 마다 **현재 가능한 후보**(휴대전화를 쓰며 앉은 승객의 보조배터리, 여행가방을 곁에 둔 승객, 지진)와 `none_yet` 을 JEV 에 묻는다. 3회 뒤에는 `none_yet` 을 빼며, 12–16 s 마다 가능한 전개(불 번짐·연기 확산·근처 승객 부상·감지기 동작·재발화 / 호기심 승객 접근·승객 신고 / 안내판 낙하·여진·에스컬레이터 정지)를 다시 묻는다. 같은 전개는 40 s 안에 되풀이하지 않는다.
- 승객 핵심 순간(발견·간접 신호·역무원 지시·안내방송·재판단·지진 중/후)만 JEV 에 8문항씩 묶어 묻고, 응답 확률에서 표본을 뽑아 군중이 한 행동으로 몰리지 않게 했다. 이동·일상은 로컬 navmesh.
- 실측(에디터, 화재 1판): JEV 156요청 모두 HTTP 200, 지연 중앙값 0.23 s·최대 0.57 s. 방송 직후 100명이 한꺼번에 판단할 때는 일부가 로컬 규칙으로 처리된다(판마다 결과 화면에 표시).

### 역무원이 하는 일

- 발견(직접 보기·승객 제보·감지기/119·112 통보 무전) → Q 무전: 역무실 보고(119/112 출동), 대피 안내방송, 부상자 구급 요청, 낙하물 보고, 에스컬레이터 이용 통제, 진화·통제 완료 보고.
- E: 승객 말 걸기 / 사건 뒤에는 주변 5 m 승객 대피 안내, 부상 승객 상태 확인, 방치 가방·낙하물 주변 통제선(벨트식 차단봉) 설치. 가방은 열거나 옮길 수 없다.
- 소화기: 역 소화기 12대를 그대로 들고(E) 안전핀(좌클릭 0.8 s) → 누른 채 분사(14 s 분량), G 로 내려놓기. 1.5–5 m·불 아랫부분 조준이 가장 잘 듣고, 불이 초기 단계(세기 1)를 넘으면 거의 듣지 않는다. 점검 데이터에서 압력 이상인 소화기는 약제가 나오지 않는다.
- 기관 도착(소방대·철도경찰·구급대·시설 담당) 뒤 대장에게 E `현장 인계` → 요약 무전 → 결과 화면(실제 일어난 기록·대피/부상/분사 수치, 점수 없음). 인계하지 않으면 100 s 뒤 기관이 현장을 넘겨받고 끝난다. 기록은 `persistentDataPath/shift-logs`, JEV 요청·응답은 `jev-runs` 에 남는다.

### 검증

- 세 계열을 각각 강제 시작해 인계·결과 화면까지 재생 확인(화재: 발견·소화기 진화·소방대 인계 / 방치 가방: 112 통보·통제선·철도경찰 인계 / 지진: 안내판 3곳 낙하·통제선·시설 담당 인계). 캡처: `evidence/`.
- 타이틀 → 로딩 → 근무 시작 경로에서 콘솔 오류 0, 승객 100명, 에디터 프레임 약 17 ms(수직동기 60).
- PlayMode 34/34 통과(신규 `EmergencyRulesTests` 4: 초기 단계 이후 소화기 효과, 꺼진 불의 비위험화, 조준 품질, 의자 한 칸 한 사람). EditMode 489 중 실패 46 — 이번 세션 첫 전체 실행에도 실패하던 시험뿐이다: 새 스크래치 픽스처 경로가 필요한 빌드 경계 시험 38, MVP 호스트 모달 4, 기존 어셈블리 참조 규칙 2, `LOCAL_PLUGIN_UNDECLARED` 1, TMP 정적 목록 1(Bootstrap 원본 글꼴 단정을 타이틀 전환으로 걷어낸 뒤 드러난 기존 `ThirdPartyNotices/KoreanDigitalKiosk-CC-BY-4.0.txt` 목록 불일치). 건너뜀 5: SQLite 해시 3, 더러운 Untitled 씬 전제 2. Bootstrap 글꼴 손상 시험은 타이틀 기준으로 바꿔 통과한다.
- 함께 고친 것: 런타임 `SceneFlow` 의 `UnityEditor` 언급 제거(에디터 종료는 `PlayModeQuitHook`), Bootstrap 검증기를 타이틀 글꼴 기준으로 전환, `PreparedAssetSetup.ConfigureFont` 가 도메인 재시작 뒤 한국어 글꼴의 원본 연결을 지우던 문제.

### 남은 것

- 소리(비상벨·방송·연기) 없음. 연기는 입자 표현이며 천장 축적·시야 차단을 계산하지 않는다.
- 역무원 외 직무 전환·튜토리얼은 MVP 이후. 역 전체 단계의 남은 것은 [world-npc-incidents](../2026-09-26-world-npc-incidents/plan.md#남은-것).
