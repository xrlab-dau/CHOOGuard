# CHOOGuard 작은 실행 스토리 구현 명세

# 단기 작업 계획 · 신규 구축 v4

**Goal:** 새 Unity에서 두 기관 fixture의 요청을 실제 디스크에 원자 저장하고, 응답을 화면에 표시한 뒤 재시작해도 동일 상태를 복구한다.
**Architecture:** native UI와 순수 C# 운영 코어를 분리하고 SQLite의 durable receipt를 표시한다. 초기화 이전 코드·에픽을 가져오지 않는다.
**Tech Stack:** Unity 6 LTS 후보의 실제 호환 버전 잠금, uGUI/TMP/Input System, C#, SQLite, JSON Schema.
**Spec:** `GLOBAL_CONTRACT.md`, `basis/v3/PRODUCT_BASELINE.md`, 각 story의 명시적 specRefs.

> 인원·가용시간·납기·실제 처리량이 제공되지 않았다. W0–W3은 수용 산출물 중심의 작업창이며 4일·4주·2주 Sprint 약속이 아니다. 먼저 W0를 착수 대상으로 두고 다음 창은 수용·실측 작업량에 따라 갱신한다.

## 변경하지 않은 목표
최종 공조 운영·A/B·두 모드·대본·독립 정량·기관 활용 목표는 그대로다. 이 단기 창은 작지만 실제 저장과 native 조작을 포함한다. 모형 데모 성공을 최종 정확도로 바꾸지 않는다.

## 단기 창과 수용점
| 창 | 이야기 수 | 통과할 결과 |
|---|---:|---|
| W0 · 새 Native 부팅·타입·시험 경로 | 7 | 같은 입력으로 새 Player 부팅·한글 표시·단일 입력 모듈·테스트 실패 전파를 확인한다. |
| W1 · 두 기관 fixture의 영속 요청·권한·예약 | 9 | 실제 파일 DB의 원자 commit·거부 무변경·requester 포함 멱등 키를 확인한다. |
| W2 · 2공간 fixture와 native 조작 연결 | 9 | UI뒤클릭0·IME 누출0·한 요청 한 receipt·요청수락과 완료를 분리한다. |
| W3 · 재시작·장애 반례까지 첫 구간 수용 | 3 | 종료/재시작 후 reservation·receipt·outbox 일치. 기술 첫 구간만 수용; 전체 공조/A-B/물리/현장 승인은 아님. |

## W0 · 새 Native 부팅·타입·시험 경로
새 Player와 경계 타입의 기준을 만든다. 후행 도메인 미구현으로 부팅을 막지 않는다.

| 순서 후보 | Story | 검수할 결과 |
|---:|---|---|
| 1 | [CS-BOOT.01.01](stories/CS-BOOT.01.01.md) | 새 Unity 프로젝트에서 정적 PC 부팅을 재현한다 |
| 2 | [CS-BOOT.01.02](stories/CS-BOOT.01.02.md) | 한글 UI와 UI 전용 입력 smoke를 추가한다 |
| 3 | [CS-BOOT.02.01](stories/CS-BOOT.02.01.md) | 13개 모듈과 시험 assembly의 의존 경계를 고정한다 |
| 4 | [CS-PACK.01.01](stories/CS-PACK.01.01.md) | 새 객체 ID·SI단위·시간 타입을 정의한다 |
| 5 | [CS-BOOT.02.02](stories/CS-BOOT.02.02.md) | 명령·조회·응답 DTO와 비동기 ports를 정의한다 |
| 6 | [CS-BOOT.03.01](stories/CS-BOOT.03.01.md) | EditMode·PlayMode 실행기의 실패 전파를 만든다 |
| 7 | [CS-BOOT.03.02](stories/CS-BOOT.03.02.md) | PC Player 빌드와 실행 영수증을 생성한다 |

**종료 기준:** 같은 입력으로 새 Player 부팅·한글 표시·단일 입력 모듈·테스트 실패 전파를 확인한다.

## W1 · 두 기관 fixture의 영속 요청·권한·예약
요청을 저장 전에 성공으로 알리지 않고 자원 중복·응답 손실을 처리한다.

| 순서 후보 | Story | 검수할 결과 |
|---:|---|---|
| 8 | [CS-PACK.01.02](stories/CS-PACK.01.02.md) | SiteBundle와 ScenarioSpec의 참조를 검사한다 |
| 9 | [CS-PACK.01.03](stories/CS-PACK.01.03.md) | 두 기관·두 공간·공유 자원 fixture를 만든다 |
| 10 | [CS-PACK.02.01](stories/CS-PACK.02.01.md) | 판본·절·예외가 있는 규칙 후보를 로드한다 |
| 11 | [CS-OPS.01.01](stories/CS-OPS.01.01.md) | 단일 run writer와 순서 있는 명령 큐를 만든다 |
| 12 | [CS-OPS.01.02](stories/CS-OPS.01.02.md) | 부작용 없는 preview와 Submit 재검사를 구현한다 |
| 13 | [CS-OPS.03.01](stories/CS-OPS.03.01.md) | 기관 내부 지시와 외부 지원요청을 분리한다 |
| 14 | [CS-OPS.02.01](stories/CS-OPS.02.01.md) | 지원 native SQLite를 Player에서 열고 스키마를 준비한다 |
| 15 | [CS-OPS.02.02](stories/CS-OPS.02.02.md) | 이벤트·예약·receipt를 원자 트랜잭션으로 저장한다 |
| 16 | [CS-OPS.01.03](stories/CS-OPS.01.03.md) | 요청키 멱등성과 응답 손실 후 조회를 구현한다 |

**종료 기준:** 실제 파일 DB의 원자 commit·거부 무변경·requester 포함 멱등 키를 확인한다.

## W2 · 2공간 fixture와 native 조작 연결
웹/브라우저가 아닌 Unity에서 입력을 분리하고 실제 port를 연결한다.

| 순서 후보 | Story | 검수할 결과 |
|---:|---|---|
| 17 | [CS-WORLD.01.01](stories/CS-WORLD.01.01.md) | 새 2공간·문·1m 기준체 fixture를 생성한다 |
| 18 | [CS-WORLD.01.02](stories/CS-WORLD.01.02.md) | 문과 anchor를 읽기 전용 projection에 결속한다 |
| 19 | [CS-PLAY.01.01](stories/CS-PLAY.01.01.md) | 포인터 입력의 최초 소유자를 끝까지 유지한다 |
| 20 | [CS-PLAY.01.02](stories/CS-PLAY.01.02.md) | 한글 IME와 모달 키보드 포커스를 격리한다 |
| 21 | [CS-PLAY.01.03](stories/CS-PLAY.01.03.md) | Player에서 재배정·포인터/키보드 충돌을 확인한다 |
| 22 | [CS-PLAY.03.01](stories/CS-PLAY.03.01.md) | 실제 Game View 위에 기본 운영 HUD를 조립한다 |
| 23 | [CS-BOOT.02.03](stories/CS-BOOT.02.03.md) | 실제 운영 코어를 새 Scene의 단일 CompositionRoot에 연결한다 |
| 24 | [CS-PLAY.02.01](stories/CS-PLAY.02.01.md) | 팀/차량과 업무 선택을 같은 SelectionSet에 연결한다 |
| 25 | [CS-PLAY.02.02](stories/CS-PLAY.02.02.md) | 대상별 조건을 보여주고 확인한 요청만 제출한다 |

**종료 기준:** UI뒤클릭0·IME 누출0·한 요청 한 receipt·요청수락과 완료를 분리한다.

## W3 · 재시작·장애 반례까지 첫 구간 수용
native 요청과 durable 상태를 실제 재열기·중복·저장실패로 검수한다.

| 순서 후보 | Story | 검수할 결과 |
|---:|---|---|
| 26 | [CS-OPS.02.03](stories/CS-OPS.02.03.md) | 트랜잭션 outbox와 중복 결과 방지를 구현한다 |
| 27 | [CS-OPS.02.04](stories/CS-OPS.02.04.md) | 동시 예약·취소·재시작 장애를 주입해 검증한다 |
| 28 | [CS-PROOF.02.01](stories/CS-PROOF.02.01.md) | 첫 native 요청·저장·재열기 수직구간을 검수한다 |

**종료 기준:** 종료/재시작 후 reservation·receipt·outbox 일치. 기술 첫 구간만 수용; 전체 공조/A-B/물리/현장 승인은 아님.

## 병행 후보 — 단기 수용의 숨은 선행이 아님
| Story | 별도 진행할 결과 |
|---|---|
| [CS-PROOF.01.01](stories/CS-PROOF.01.01.md) | 실제 작성 과제·시간·동의 조사 프로토콜을 고정한다 |
| [CS-PACK.04.01](stories/CS-PACK.04.01.md) | 첫 현장의 관측·가정·독립 치수를 등록한다 |
| [CS-PACK.03.01](stories/CS-PACK.03.01.md) | 무료판 원본을 격리 검사하고 입고 증거를 남긴다 |
| [CS-SIM.01.01](stories/CS-SIM.01.01.md) | capability handshake와 제한된 JSONL transport를 만든다 |
| [CS-SIM.01.02](stories/CS-SIM.01.02.md) | job·generation·출력 수량의 상관관계를 검사한다 |
| [CS-SIM.01.03](stories/CS-SIM.01.03.md) | 시험 worker 종료·timeout·재시작을 검증한다 |

자료 취득은 원본이 없으면 그 story의 입고/현장 수용만 HOLD한다. 실제 사용자 관찰은 동의된 자료가 있어야 하며 합성 인터뷰로 채우지 않는다. 워커 연구는 별도 checkout/process를 쓰며 core/UI의 첫 요청 수용을 막지 않는다.

## 순서 결정 원칙
① 첫 작동 구간에 필요한 위험부터(부팅·타입·영속성) → ② 실제 dependency 선행 → ③ 입력 준비 → ④ 공유파일·Editor 경합 제거 → ⑤ 검수 가능한 작은 결과 순으로 진행한다. 중요도가 높아도 선행 artifact를 건너뛰지 않는다.
기본 실행은 단일 작성자/agent, WIP=1이다. 병렬 후보를 2~3개로 늘리려면 입력·checkout·Editor·빌드출력·DB·검수능력이 분리돼야 한다. 문서의 정적 병렬 계산은 실제 잠금이 아니다.

## 작업시간을 확인한 뒤 달력 계획을 만든다
실제 story 시작/완료, 능동 구현시간, 리뷰·대기·재작업, 중단·미완료를 따로 기록한다. 첫 수용 결과 뒤 비슷한 story의 관측 범위를 이용해 나머지 forecast를 갱신한다. 임의 고정 속도나 LLM 수로 납기를 계산하지 않는다.

## 단기 이후의 순서
1. 보고/지식/업무망/원인 분석을 연결해 두 기관의 요청→수행→회신 전체 운영 흐름.
2. 완전 checkpoint와 불변 A/B, 비교의 native 화면, 동일 코어 두 모드.
3. 근거 AI·조건부 ScriptIR·수동 검수·JSON/Markdown 출력·새 run. 이후 고객 DOCX 템플릿과 수정 회수.
4. 실제 자료가 준비되는 범위에서 첫 철도 공간·보행·위험장·결합·독립 QoI 검증과 사용자 평가.
5. 오프라인 배포·복구·새 현장/기관 확장. 과거 고정 구역 수나 코드 호환은 요구하지 않는다.

## 매 작업의 인계
plan/story digest, 실제 새 revision, artifact 경로·hash, 실패·green 로그, 영향 회귀, 입력 receipt, reviewer/scope, 실패·NOT_RUN·현재 blockers를 남긴다. 원래 91개 제품 AT는 leaf 시험으로 대체하지 않는다.

## 현재 진행 상태
**계획·온톨로지·검사 도구를 생성한 상태이며 제품 story 109개는 모두 NOT_STARTED다.** 단기 목표가 아직 달성됐다고 표시하지 않는다. 저장소 초기화·원격 이슈·코드·assignee는 변경하지 않았다.


---

# CS-BOOT.01.01 · 새 Unity 프로젝트에서 정적 PC 부팅을 재현한다

**상위:** CS-BOOT.01 / CS-BOOT · **작업창:** W0 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 정식 Unity 6 LTS/URP 새 프로젝트와 editor·backend·package lock을 기록한다. Camera/Canvas/EventSystem만으로 부팅하고 도메인 조립은 요구하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-BOOT.01.json](basis/v3/tasks/CS-BOOT.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
새 Unity 프로젝트에서 정적 PC 부팅을 재현한다

## 입력·출력 인터페이스
BuildBaseline {editorVersion, packageLockSha256, renderPipeline, backend, os, nativePlugins, buildReceiptRef}; 모든 값은 새 실행에서 기록.

**직접 담당 요구:** REQ-066
**지원 요구:** REQ-066
**부모 제품 시험:** AT-066

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `ProjectSettings/ProjectVersion.txt` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/GraphicsSettings.asset` | CS-BOOT.01.01 |
| CREATE | `Packages/manifest.json` | CS-BOOT.01.01 |
| CREATE | `Packages/packages-lock.json` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Scenes/Bootstrap.unity` | CS-BOOT.01.01 |
| CREATE | `docs/build/baseline.json` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/QualitySettings.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/ProjectSettings.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/EditorBuildSettings.asset` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/BootstrapURP.asset` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/BootstrapRenderer.asset` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/BootstrapURPGlobalSettings.asset` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/ChooGuard.Editor.asmdef` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/Bootstrap/BootstrapProject.cs` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/Bootstrap/BootstrapValidator.cs` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/Bootstrap/BuildBaseline.cs` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/EditMode/ChooGuard.EditModeTests.asmdef` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0101Tests.cs` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/PlayMode/ChooGuard.PlayModeTests.asmdef` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/PlayMode/Stories/CSBOOT0101PlayModeTests.cs` | CS-BOOT.01.01 |
| CREATE | `docs/build/baseline.schema.json` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/AudioManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/ClusterInputManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/DynamicsManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/EditorSettings.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/InputManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/MemorySettings.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/MultiplayerManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/NavMeshAreas.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/Physics2DSettings.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/PresetManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/ShaderGraphSettings.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/TagManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/TimeManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/UnityConnectSettings.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/VFXManager.asset` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/VersionControlSettings.asset` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/Resources/TMP Settings.asset` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Fonts/ChooGuard Bootstrap SDF.asset` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Shaders/TMP_SDF-Mobile.shader` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Shaders/TMPro_Properties.cginc` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/LineBreaking/Leading Characters.txt` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/LineBreaking/Following Characters.txt` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/ThirdPartyNotices/LiberationSans-SDF-OFL.txt` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/Resources.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/Resources/TMP Settings.asset.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Fonts.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Fonts/ChooGuard Bootstrap SDF.asset.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/LineBreaking.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/LineBreaking/Following Characters.txt.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/LineBreaking/Leading Characters.txt.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Shaders.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Shaders/TMP_SDF-Mobile.shader.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/TMP/Shaders/TMPro_Properties.cginc.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/ThirdPartyNotices.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/ThirdPartyNotices/LiberationSans-SDF-OFL.txt.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/Bootstrap.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/Bootstrap/BootstrapProject.cs.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/ChooGuard.Editor.asmdef.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/EditMode.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/EditMode/ChooGuard.EditModeTests.asmdef.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/EditMode/Stories.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0101Tests.cs.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/PlayMode.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/PlayMode/ChooGuard.PlayModeTests.asmdef.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/Bootstrap/BootstrapValidator.cs.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Editor/Bootstrap/BuildBaseline.cs.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Scenes.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Scenes/Bootstrap.unity.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/BootstrapRenderer.asset.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/BootstrapURP.asset.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Settings/BootstrapURPGlobalSettings.asset.meta` | CS-BOOT.01.01 |
| CREATE | `ProjectSettings/SceneTemplateSettings.json` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/PlayMode/Stories.meta` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/PlayMode/Stories/CSBOOT0101PlayModeTests.cs.meta` | CS-BOOT.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0101Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| 제품 선행 산출물 없음 | — | — | 환경/권한 확인은 별도 |

## 외부 입력과 보류 범위
- `EXT-UNITY` / candidate / ALWAYS: 본인이 사용할 Unity Editor 실행경로·사용 가능한 라이선스·지원 OS. 설치 probe 기록 없이는 Player 실행했다고 주장하지 않는다. — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 정식 Unity 6 LTS/URP 새 프로젝트와 editor·backend·package lock을 기록한다. Camera/Canvas/EventSystem만으로 부팅하고 도메인 조립은 요구하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-BOOT.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 새 Unity 프로젝트에서 정적 PC 부팅을 재현한다의 유효 조건
When: 새 Unity 프로젝트에서 정적 PC 부팅을 재현한다를 실행한다
Then: 빈 폴더에서 생성한 Player가 부팅되고 version receipt와 EventSystem 1개가 확인된다.

### AC-CS-BOOT.01.01-N · NEGATIVE · NOT_RUN
Given: 로컬 전용 DLL·누락 패키지·두 EventSystem을 넣으면 원인을 남기고 baseline 수용을 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 로컬 전용 DLL·누락 패키지·두 EventSystem을 넣으면 원인을 남기고 baseline 수용을 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-BOOT.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)


---

# CS-BOOT.01.02 · 한글 UI와 UI 전용 입력 smoke를 추가한다

**상위:** CS-BOOT.01 / CS-BOOT · **작업창:** W0 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 허용된 한글 font asset 공급 경로와 glyph sample을 확인한다. uGUI/TMP와 Input System 모듈을 하나씩 구성하며 라이선스·폰트파일을 문서 패키지에 넣지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-BOOT.01.json](basis/v3/tasks/CS-BOOT.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
한글 UI와 UI 전용 입력 smoke를 추가한다

## 입력·출력 인터페이스
BuildBaseline {editorVersion, packageLockSha256, renderPipeline, backend, os, nativePlugins, buildReceiptRef}; 모든 값은 새 실행에서 기록.

**직접 담당 요구:** REQ-066
**지원 요구:** REQ-066
**부모 제품 시험:** AT-066

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Packages/manifest.json` | CS-BOOT.01.01 |
| MODIFY | `Packages/packages-lock.json` | CS-BOOT.01.01 |
| MODIFY | `Assets/ChooGuard/Scenes/Bootstrap.unity` | CS-BOOT.01.01 |
| MODIFY | `docs/build/baseline.json` | CS-BOOT.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0102Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.01.01 | `OUT-CS-BOOT.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-FONT` / integration / ALWAYS: 허용된 한글 font asset과 glyph 검수 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 허용된 한글 font asset 공급 경로와 glyph sample을 확인한다. uGUI/TMP와 Input System 모듈을 하나씩 구성하며 라이선스·폰트파일을 문서 패키지에 넣지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-BOOT.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 한글 UI와 UI 전용 입력 smoke를 추가한다의 유효 조건
When: 한글 UI와 UI 전용 입력 smoke를 추가한다를 실행한다
Then: 같은 Player에서 한국어 라벨과 버튼 입력이 보이고 사용한 패키지·폰트 참조가 기록된다.

### AC-CS-BOOT.01.02-N · NEGATIVE · NOT_RUN
Given: missing glyph나 이중 입력 모듈이면 한글/입력 검증을 미통과로 남긴다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: missing glyph나 이중 입력 모듈이면 한글/입력 검증을 미통과로 남긴다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-BOOT.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)


---

# CS-BOOT.02.01 · 13개 모듈과 시험 assembly의 의존 경계를 고정한다

**상위:** CS-BOOT.02 / CS-BOOT · **작업창:** W0 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** assembly-layout을 새 asmdef로 옮긴다. Contracts/Domain의 UnityEngine 참조를 금지하고 Editor 코드를 분리한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-BOOT.02.json](basis/v3/tasks/CS-BOOT.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
13개 모듈과 시험 assembly의 의존 경계를 고정한다

## 입력·출력 인터페이스
IOperationsPort.PreviewAsync(CommandIntent,CancellationToken); SubmitAsync(CommandIntent,CancellationToken); ReadReceiptAsync(ReceiptKey,CancellationToken); ReadProjectionAsync(ProjectionQuery,CancellationToken). 정확 타입은 specs/02-wire-and-ports.md.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-066
**부모 제품 시험:** AT-066

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Contracts/ChooGuard.Contracts.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Domain/ChooGuard.Domain.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Application/ChooGuard.Application.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Presentation/ChooGuard.Presentation.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Content/ChooGuard.Content.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Persistence/ChooGuard.Persistence.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/World/ChooGuard.World.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Experiments/ChooGuard.Experiments.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Scenarios/ChooGuard.Scenarios.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Simulation/ChooGuard.Simulation.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/Authoring/ChooGuard.Authoring.asmdef` | CS-BOOT.02.01 |
| CREATE | `Assets/ChooGuard/App/ChooGuard.App.asmdef` | CS-BOOT.02.01 |
| MODIFY | `Assets/ChooGuard/Editor/ChooGuard.Editor.asmdef` | CS-BOOT.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0201Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.01.01 | `OUT-CS-BOOT.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. assembly-layout을 새 asmdef로 옮긴다. Contracts/Domain의 UnityEngine 참조를 금지하고 Editor 코드를 분리한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-BOOT.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 13개 모듈과 시험 assembly의 의존 경계를 고정한다의 유효 조건
When: 13개 모듈과 시험 assembly의 의존 경계를 고정한다를 실행한다
Then: 각 신규 C# 경로가 정확히 한 asmdef에 속하고 의존 그래프가 순환하지 않는다.

### AC-CS-BOOT.02.01-N · NEGATIVE · NOT_RUN
Given: Domain에 UnityEngine 참조 또는 default Assembly-CSharp 의존을 추가하면 검사 실패한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: Domain에 UnityEngine 참조 또는 default Assembly-CSharp 의존을 추가하면 검사 실패한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-BOOT.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-UNITY-ASSEMBLY](https://docs.unity.cn/6000.3/Documentation/Manual/assembly-definitions-referencing.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-BOOT.02.02 · 명령·조회·응답 DTO와 비동기 ports를 정의한다

**상위:** CS-BOOT.02 / CS-BOOT · **작업창:** W0 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** v3 wire 스키마에서 요청키·nullable·enum·취소 의미를 보존해 C# DTO/IOperationsPort를 만든다. JSON과 C# 왕복에서 알 수 없는 필드와 중복키를 거부한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-BOOT.02.json](basis/v3/tasks/CS-BOOT.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
명령·조회·응답 DTO와 비동기 ports를 정의한다

## 입력·출력 인터페이스
IOperationsPort.PreviewAsync(CommandIntent,CancellationToken); SubmitAsync(CommandIntent,CancellationToken); ReadReceiptAsync(ReceiptKey,CancellationToken); ReadProjectionAsync(ProjectionQuery,CancellationToken). 정확 타입은 specs/02-wire-and-ports.md.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-066
**부모 제품 시험:** AT-066

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Contracts/OperationsPorts.cs` | CS-BOOT.02.02 |
| CREATE | `Assets/ChooGuard/Contracts/OperationMessages.cs` | CS-BOOT.02.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0202Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.01 | `OUT-CS-BOOT.02.01@candidate` | candidate | ALWAYS |
| CS-PACK.01.01 | `OUT-CS-PACK.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. v3 wire 스키마에서 요청키·nullable·enum·취소 의미를 보존해 C# DTO/IOperationsPort를 만든다. JSON과 C# 왕복에서 알 수 없는 필드와 중복키를 거부한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-BOOT.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 명령·조회·응답 DTO와 비동기 ports를 정의한다의 유효 조건
When: 명령·조회·응답 DTO와 비동기 ports를 정의한다를 실행한다
Then: Preview/Submit/ReadReceipt/ReadProjection의 타입과 ReceiptKey 3필드가 일치한다.

### AC-CS-BOOT.02.02-N · NEGATIVE · NOT_RUN
Given: requesterId 누락 또는 found=false에 non-null receipt면 거부하고 DTO를 자동 보정하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: requesterId 누락 또는 found=false에 non-null receipt면 거부하고 DTO를 자동 보정하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-BOOT.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-UNITY-ASSEMBLY](https://docs.unity.cn/6000.3/Documentation/Manual/assembly-definitions-referencing.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-BOOT.02.03 · 실제 운영 코어를 새 Scene의 단일 CompositionRoot에 연결한다

**상위:** CS-BOOT.02 / CS-BOOT · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 이야기별로 준비된 운영·저장·UI 인스턴스를 composition root에서 결속한다. 첫 정적 smoke를 재생성하지 않고 installer로 참조를 연결한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-BOOT.02.json](basis/v3/tasks/CS-BOOT.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
실제 운영 코어를 새 Scene의 단일 CompositionRoot에 연결한다

## 입력·출력 인터페이스
IOperationsPort.PreviewAsync(CommandIntent,CancellationToken); SubmitAsync(CommandIntent,CancellationToken); ReadReceiptAsync(ReceiptKey,CancellationToken); ReadProjectionAsync(ProjectionQuery,CancellationToken). 정확 타입은 specs/02-wire-and-ports.md.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-066
**부모 제품 시험:** AT-066

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/App/CompositionRoot.cs` | CS-BOOT.02.03 |
| CREATE | `Assets/ChooGuard/Editor/InstallCompositionRoot.cs` | CS-BOOT.02.03 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0203Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |
| CS-BOOT.01.01 | `OUT-CS-BOOT.01.01@candidate` | integration | ALWAYS |
| CS-OPS.01.03 | `OUT-CS-OPS.01.03@candidate` | candidate | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | candidate | ALWAYS |
| CS-PLAY.03.01 | `OUT-CS-PLAY.03.01@candidate` | candidate | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 이야기별로 준비된 운영·저장·UI 인스턴스를 composition root에서 결속한다. 첫 정적 smoke를 재생성하지 않고 installer로 참조를 연결한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-BOOT.02.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 실제 운영 코어를 새 Scene의 단일 CompositionRoot에 연결한다의 유효 조건
When: 실제 운영 코어를 새 Scene의 단일 CompositionRoot에 연결한다를 실행한다
Then: Scene 재진입 후에도 run writer와 EventSystem이 각각 하나이고 버튼이 실제 port로 간다.

### AC-CS-BOOT.02.03-N · NEGATIVE · NOT_RUN
Given: 두 root 생성이나 fixture double이 production binding에 남으면 통합을 실패시킨다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 두 root 생성이나 fixture double이 production binding에 남으면 통합을 실패시킨다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0203Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-BOOT.02.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-UNITY-ASSEMBLY](https://docs.unity.cn/6000.3/Documentation/Manual/assembly-definitions-referencing.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-BOOT.03.01 · EditMode·PlayMode 실행기의 실패 전파를 만든다

**상위:** CS-BOOT.03 / CS-BOOT · **작업창:** W0 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 설치 editor/project 경로를 명시 인자로 받아 결과 XML·exit code·로그를 저장한다. 테스트 0개를 성공으로 집계하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-BOOT.03.json](basis/v3/tasks/CS-BOOT.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
EditMode·PlayMode 실행기의 실패 전파를 만든다

## 입력·출력 인터페이스
TestReceipt {testSet, commit, environment, startedAt, command, resultFiles, status, notRunReason}; CI credential은 저장소에 넣지 않는다.

**직접 담당 요구:** REQ-061
**지원 요구:** REQ-061
**부모 제품 시험:** AT-061

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `scripts/Run-UnityTests.ps1` | CS-BOOT.03.01 |
| CREATE | `.github/workflows/verify.yml` | CS-BOOT.03.01 |
| MODIFY | `Assets/ChooGuard/Tests/EditMode/ChooGuard.EditModeTests.asmdef` | CS-BOOT.01.01 |
| MODIFY | `Assets/ChooGuard/Tests/PlayMode/ChooGuard.PlayModeTests.asmdef` | CS-BOOT.01.01 |
| CREATE | `Assets/ChooGuard/Tests/Acceptance/ChooGuard.AcceptanceTests.asmdef` | CS-BOOT.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0301Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.01 | `OUT-CS-BOOT.02.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 설치 editor/project 경로를 명시 인자로 받아 결과 XML·exit code·로그를 저장한다. 테스트 0개를 성공으로 집계하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-BOOT.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 EditMode·PlayMode 실행기의 실패 전파를 만든다의 유효 조건
When: EditMode·PlayMode 실행기의 실패 전파를 만든다를 실행한다
Then: 정상 테스트와 의도적 assertion 실패가 다른 종료코드·결과로 기록된다.

### AC-CS-BOOT.03.01-N · NEGATIVE · NOT_RUN
Given: 라이선스 없음·XML 없음·테스트 0개면 PASS 대신 환경 오류/NOT_RUN을 반환한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 라이선스 없음·XML 없음·테스트 0개면 PASS 대신 환경 오류/NOT_RUN을 반환한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-BOOT.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-UNITY-ASSEMBLY](https://docs.unity.cn/6000.3/Documentation/Manual/assembly-definitions-referencing.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-BOOT.03.02 · PC Player 빌드와 실행 영수증을 생성한다

**상위:** CS-BOOT.03 / CS-BOOT · **작업창:** W0 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** BuildCommands를 Editor assembly로 만들고 실제 build target·backend·output hash를 기록한다. 실행시험과 단순 파일생성을 나눈다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-BOOT.03.json](basis/v3/tasks/CS-BOOT.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
PC Player 빌드와 실행 영수증을 생성한다

## 입력·출력 인터페이스
TestReceipt {testSet, commit, environment, startedAt, command, resultFiles, status, notRunReason}; CI credential은 저장소에 넣지 않는다.

**직접 담당 요구:** REQ-061
**지원 요구:** REQ-061
**부모 제품 시험:** AT-061

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `scripts/Build-Player.ps1` | CS-BOOT.03.02 |
| CREATE | `Assets/ChooGuard/Editor/BuildCommands.cs` | CS-BOOT.03.02 |
| CREATE | `scripts/Run-PlayerAcceptance.ps1` | CS-BOOT.03.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0302Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.03.01 | `OUT-CS-BOOT.03.01@candidate` | candidate | ALWAYS |
| CS-BOOT.01.01 | `OUT-CS-BOOT.01.01@candidate` | integration | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | integration | ALWAYS |
| CS-BOOT.01.02 | `OUT-CS-BOOT.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. BuildCommands를 Editor assembly로 만들고 실제 build target·backend·output hash를 기록한다. 실행시험과 단순 파일생성을 나눈다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-BOOT.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 PC Player 빌드와 실행 영수증을 생성한다의 유효 조건
When: PC Player 빌드와 실행 영수증을 생성한다를 실행한다
Then: 새 출력 폴더의 Player 실행·종료·로그가 입력 commit과 묶인다.

### AC-CS-BOOT.03.02-N · NEGATIVE · NOT_RUN
Given: 이전 executable이나 Editor 실행 로그만 있으면 Player 수용 증거로 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 이전 executable이나 Editor 실행 로그만 있으면 Player 수용 증거로 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-BOOT.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-UNITY-ASSEMBLY](https://docs.unity.cn/6000.3/Documentation/Manual/assembly-definitions-referencing.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.01.01 · 새 객체 ID·SI단위·시간 타입을 정의한다

**상위:** CS-PACK.01 / CS-PACK · **작업창:** W0 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 표시명과 stable ID를 분리하고 simulation microseconds·sequence·UTC 관측시각을 다른 값으로 보관한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.01.json](basis/v3/tasks/CS-PACK.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
새 객체 ID·SI단위·시간 타입을 정의한다

## 입력·출력 인터페이스
SiteBundle {id,revision,frames,regions,portals,entities,sourceRefs,qualification}; ScenarioSpec와 OperationalPlan은 별도 revision.

**직접 담당 요구:** REQ-015, REQ-060
**지원 요구:** REQ-015, REQ-060
**부모 제품 시험:** AT-015, AT-060

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Contracts/ContentTypes.cs` | CS-PACK.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0101Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.01 | `OUT-CS-BOOT.02.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 표시명과 stable ID를 분리하고 simulation microseconds·sequence·UTC 관측시각을 다른 값으로 보관한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 새 객체 ID·SI단위·시간 타입을 정의한다의 유효 조건
When: 새 객체 ID·SI단위·시간 타입을 정의한다를 실행한다
Then: 같은 한국어 이름의 서로 다른 기관을 별도 ID로 저장하고 단위·시간 종류가 보존된다.

### AC-CS-PACK.01.01-N · NEGATIVE · NOT_RUN
Given: ID 공백·NaN·음수 tick·서로 다른 시간 축의 암묵 변환을 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: ID 공백·NaN·음수 tick·서로 다른 시간 축의 암묵 변환을 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [STD-JSONLD](https://www.w3.org/TR/json-ld11/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-PROV](https://www.w3.org/TR/prov-o/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.01.02 · SiteBundle와 ScenarioSpec의 참조를 검사한다

**상위:** CS-PACK.01 / CS-PACK · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** frame/region/portal/entity의 참조와 revision을 검사한다. 시나리오 조건과 사용자 운영안을 다른 문서로 둔다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.01.json](basis/v3/tasks/CS-PACK.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
SiteBundle와 ScenarioSpec의 참조를 검사한다

## 입력·출력 인터페이스
SiteBundle {id,revision,frames,regions,portals,entities,sourceRefs,qualification}; ScenarioSpec와 OperationalPlan은 별도 revision.

**직접 담당 요구:** REQ-015, REQ-060
**지원 요구:** REQ-015, REQ-060
**부모 제품 시험:** AT-015, AT-060

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Content/ContentValidator.cs` | CS-PACK.01.02 |
| CREATE | `content/schemas/site.schema.json` | CS-PACK.01.02 |
| CREATE | `content/schemas/scenario.schema.json` | CS-PACK.01.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0102Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.01.01 | `OUT-CS-PACK.01.01@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. frame/region/portal/entity의 참조와 revision을 검사한다. 시나리오 조건과 사용자 운영안을 다른 문서로 둔다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 SiteBundle와 ScenarioSpec의 참조를 검사한다의 유효 조건
When: SiteBundle와 ScenarioSpec의 참조를 검사한다를 실행한다
Then: 누락 없는 새 공간 정의를 읽고 참조 대상과 버전을 확인할 수 있다.

### AC-CS-PACK.01.02-N · NEGATIVE · NOT_RUN
Given: 없는 portal endpoint·중복 ID·알 수 없는 revision이면 invalid 위치를 반환한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 없는 portal endpoint·중복 ID·알 수 없는 revision이면 invalid 위치를 반환한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [STD-JSONLD](https://www.w3.org/TR/json-ld11/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-PROV](https://www.w3.org/TR/prov-o/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.01.03 · 두 기관·두 공간·공유 자원 fixture를 만든다

**상위:** CS-PACK.01 / CS-PACK · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 철도·협조 기관의 기술 시험용 데이터에 task·resource·report와 고유 ID를 넣는다. 사실 근거 없는 시간은 SYNTHETIC_FIXTURE로 명시한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.01.json](basis/v3/tasks/CS-PACK.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
두 기관·두 공간·공유 자원 fixture를 만든다

## 입력·출력 인터페이스
SiteBundle {id,revision,frames,regions,portals,entities,sourceRefs,qualification}; ScenarioSpec와 OperationalPlan은 별도 revision.

**직접 담당 요구:** REQ-015, REQ-060
**지원 요구:** REQ-015, REQ-060
**부모 제품 시험:** AT-015, AT-060

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `content/fixtures/two-agency.json` | CS-PACK.01.03 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0103Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.01.02 | `OUT-CS-PACK.01.02@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 철도·협조 기관의 기술 시험용 데이터에 task·resource·report와 고유 ID를 넣는다. 사실 근거 없는 시간은 SYNTHETIC_FIXTURE로 명시한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.01.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 두 기관·두 공간·공유 자원 fixture를 만든다의 유효 조건
When: 두 기관·두 공간·공유 자원 fixture를 만든다를 실행한다
Then: 정상 배정과 자원 충돌을 재현하는 데이터가 같은 입력 해시로 로드된다.

### AC-CS-PACK.01.03-N · NEGATIVE · NOT_RUN
Given: fixture를 실제 현장 승인 콘텐츠로 표시하면 검사에서 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: fixture를 실제 현장 승인 콘텐츠로 표시하면 검사에서 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0103Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.01.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [STD-JSONLD](https://www.w3.org/TR/json-ld11/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-PROV](https://www.w3.org/TR/prov-o/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.02.01 · 판본·절·예외가 있는 규칙 후보를 로드한다

**상위:** CS-PACK.02 / CS-PACK · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** REQUIRED/DISCRETIONARY/ADVISORY/INVARIANT/UNKNOWN을 나누고 scope·guard·sourceLocator를 저장한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.02.json](basis/v3/tasks/CS-PACK.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
판본·절·예외가 있는 규칙 후보를 로드한다

## 입력·출력 인터페이스
RuleClause {id,revision,kind,scope,guard,effect,handoff,exception,sourceLocator,review}; DTO는 예외를 검토자료로 유지.

**직접 담당 요구:** REQ-031, REQ-033, REQ-034
**지원 요구:** REQ-031, REQ-033, REQ-034
**부모 제품 시험:** AT-031, AT-033, AT-034

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Contracts/RuleTypes.cs` | CS-PACK.02.01 |
| CREATE | `Assets/ChooGuard/Content/RuleCatalog.cs` | CS-PACK.02.01 |
| CREATE | `content/schemas/rule.schema.json` | CS-PACK.02.01 |
| CREATE | `content/rules/catalog.json` | CS-PACK.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0201Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.01.02 | `OUT-CS-PACK.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. REQUIRED/DISCRETIONARY/ADVISORY/INVARIANT/UNKNOWN을 나누고 scope·guard·sourceLocator를 저장한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 판본·절·예외가 있는 규칙 후보를 로드한다의 유효 조건
When: 판본·절·예외가 있는 규칙 후보를 로드한다를 실행한다
Then: 기관·사건 범위로 규칙을 조회하고 원문 절과 미검수 여부를 구별한다.

### AC-CS-PACK.02.01-N · NEGATIVE · NOT_RUN
Given: 규칙 후보에 검수자·판본 없이 approved를 붙이면 활성화되지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 규칙 후보에 검수자·판본 없이 approved를 붙이면 활성화되지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [MAN-KORAIL](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-MEDICAL](https://www.mohw.go.kr/board.es?act=view&bid=0009&list_no=1478957&mid=a10402000000&nPage=1&tag=) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.02.02 · 규칙 개정과 검수 범위를 전파한다

**상위:** CS-PACK.02 / CS-PACK · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 새 rule revision은 원본을 보존한다. 의존 초안·qualification의 현재 유효성만 만료시키고 영향 목록을 반환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.02.json](basis/v3/tasks/CS-PACK.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
규칙 개정과 검수 범위를 전파한다

## 입력·출력 인터페이스
RuleClause {id,revision,kind,scope,guard,effect,handoff,exception,sourceLocator,review}; DTO는 예외를 검토자료로 유지.

**직접 담당 요구:** REQ-031, REQ-033, REQ-034
**지원 요구:** REQ-031, REQ-033, REQ-034
**부모 제품 시험:** AT-031, AT-033, AT-034

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Content/RuleCatalog.cs` | CS-PACK.02.01 |
| MODIFY | `content/rules/catalog.json` | CS-PACK.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0202Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 새 rule revision은 원본을 보존한다. 의존 초안·qualification의 현재 유효성만 만료시키고 영향 목록을 반환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 규칙 개정과 검수 범위를 전파한다의 유효 조건
When: 규칙 개정과 검수 범위를 전파한다를 실행한다
Then: 권한/예외 변경 시 해당 초안만 STALE되고 원시 로그는 같다.

### AC-CS-PACK.02.02-N · NEGATIVE · NOT_RUN
Given: 단어 승인 또는 다른 기관의 검토가 현재 scope를 승인한 것으로 처리되지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 단어 승인 또는 다른 기관의 검토가 현재 scope를 승인한 것으로 처리되지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [MAN-KORAIL](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-MEDICAL](https://www.mohw.go.kr/board.es?act=view&bid=0009&list_no=1478957&mid=a10402000000&nPage=1&tag=) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.03.01 · 무료판 원본을 격리 검사하고 입고 증거를 남긴다

**상위:** CS-PACK.03 / CS-PACK · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 원문 URL·무료 tier·취득시각·파일해시·실제 format을 기록하고 archive traversal·실행파일을 검사한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.03.json](basis/v3/tasks/CS-PACK.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
무료판 원본을 격리 검사하고 입고 증거를 남긴다

## 입력·출력 인터페이스
AssetReceipt {id,sourceUrl,tier,rawHash,formats,rigClips,parts,importStatus,limits}; 실제 바이너리가 없으면 rawHash=null.

**직접 담당 요구:** REQ-062, REQ-063, REQ-064
**지원 요구:** REQ-062, REQ-063, REQ-064
**부모 제품 시험:** AT-062, AT-063, AT-064

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Content/AssetIntakeValidator.cs` | CS-PACK.03.01 |
| CREATE | `sources/assets/catalog.json` | CS-PACK.03.01 |
| CREATE | `sources/assets/receipts.schema.json` | CS-PACK.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0301Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.01.01 | `OUT-CS-PACK.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-FREE-RAW` / integration / ALWAYS: 선택한 무료판 원본 파일과 실제 취득 해시 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 원문 URL·무료 tier·취득시각·파일해시·실제 format을 기록하고 archive traversal·실행파일을 검사한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 무료판 원본을 격리 검사하고 입고 증거를 남긴다의 유효 조건
When: 무료판 원본을 격리 검사하고 입고 증거를 남긴다를 실행한다
Then: 취득 파일의 크기·해시·구성을 재검사할 수 있고 구매비와 가공비가 분리된다.

### AC-CS-PACK.03.01-N · NEGATIVE · NOT_RUN
Given: 파일 미취득 또는 중첩 traversal·실행코드가 있으면 hash를 발명하거나 실행하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 파일 미취득 또는 중첩 traversal·실행코드가 있으면 hash를 발명하거나 실행하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [FREE-001](https://rgsdev.itch.io/free-low-poly-vehicles-pack) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-010](https://quaternius.com/packs/animatedmen.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-011](https://quaternius.com/packs/animatedwomen.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-018](https://polyhaven.com/a/korean_fire_extinguisher_01) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-BIPA-BUSAN3](https://bwebtoon.com/webtoon-home/core-businesses/digital-location/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-KTX-I](https://haesangang-0317.tistory.com/268) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.03.02 · 부품·rig·clip과 Unity 임포트 자격을 기록한다

**상위:** CS-PACK.03 / CS-PACK · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 대표 차량/인물/소품 하나씩 확인한 구조와 모르는 항목을 저장한다. import 결과는 파일 취득과 다른 상태다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.03.json](basis/v3/tasks/CS-PACK.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
부품·rig·clip과 Unity 임포트 자격을 기록한다

## 입력·출력 인터페이스
AssetReceipt {id,sourceUrl,tier,rawHash,formats,rigClips,parts,importStatus,limits}; 실제 바이너리가 없으면 rawHash=null.

**직접 담당 요구:** REQ-062, REQ-063, REQ-064
**지원 요구:** REQ-062, REQ-063, REQ-064
**부모 제품 시험:** AT-062, AT-063, AT-064

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Content/AssetIntakeValidator.cs` | CS-PACK.03.01 |
| MODIFY | `sources/assets/catalog.json` | CS-PACK.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0302Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.03.01 | `OUT-CS-PACK.03.01@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 대표 차량/인물/소품 하나씩 확인한 구조와 모르는 항목을 저장한다. import 결과는 파일 취득과 다른 상태다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 부품·rig·clip과 Unity 임포트 자격을 기록한다의 유효 조건
When: 부품·rig·clip과 Unity 임포트 자격을 기록한다를 실행한다
Then: 선택한 무료판의 실제 parts·rig·clips·import receipt가 분리된다.

### AC-CS-PACK.03.02-N · NEGATIVE · NOT_RUN
Given: 제작자 전체팩 설명을 무료판 clip 목록으로 복사하면 입고 수용을 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 제작자 전체팩 설명을 무료판 clip 목록으로 복사하면 입고 수용을 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [FREE-001](https://rgsdev.itch.io/free-low-poly-vehicles-pack) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-010](https://quaternius.com/packs/animatedmen.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-011](https://quaternius.com/packs/animatedwomen.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-018](https://polyhaven.com/a/korean_fire_extinguisher_01) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-BIPA-BUSAN3](https://bwebtoon.com/webtoon-home/core-businesses/digital-location/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-KTX-I](https://haesangang-0317.tistory.com/268) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.04.01 · 첫 현장의 관측·가정·독립 치수를 등록한다

**상위:** CS-PACK.04 / CS-PACK · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 보정점과 holdout 치수를 분리하고 기관·역·층·촬영 시기별 근거를 묶는다. 접속 실패는 그대로 남긴다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.04.json](basis/v3/tasks/CS-PACK.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
첫 현장의 관측·가정·독립 치수를 등록한다

## 입력·출력 인터페이스
Observation {targetId,observedAt,receivedAt,value,unit,sourceHash,quality}; 현실 자료 없음은 기술 개발의 전역 blocker가 아님.

**직접 담당 요구:** REQ-050
**지원 요구:** REQ-050
**부모 제품 시험:** AT-050

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `content/sites/first-site/source-manifest.json` | CS-PACK.04.01 |
| CREATE | `content/sites/first-site/observation-policy.json` | CS-PACK.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0401Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| 제품 선행 산출물 없음 | — | — | 환경/권한 확인은 별도 |

## 외부 입력과 보류 범위
- `EXT-CS-PACK.04-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.

## 구현 순서
```text
1. 보정점과 holdout 치수를 분리하고 기관·역·층·촬영 시기별 근거를 묶는다. 접속 실패는 그대로 남긴다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 첫 현장의 관측·가정·독립 치수를 등록한다의 유효 조건
When: 첫 현장의 관측·가정·독립 치수를 등록한다를 실행한다
Then: 필요 입력과 그 부재가 차단하는 현장 주장 범위를 확인할 수 있다.

### AC-CS-PACK.04.01-N · NEGATIVE · NOT_RUN
Given: 다른 역/차종·가상 치수를 실제 측정으로 결속하면 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 다른 역/차종·가상 치수를 실제 측정으로 결속하면 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0401Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [MAN-KORAIL](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-DTC](https://www.digitaltwinconsortium.org/initiatives/the-definition-of-a-digital-twin/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-SOSA](https://www.w3.org/TR/vocab-ssn/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PACK.04.02 · 현실 관측을 시각과 revision에 맞춰 갱신한다

**상위:** CS-PACK.04 / CS-PACK · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 관측시각·수신시각·품질을 검사한 후 새 baseline revision을 만든다. 늦은 과거 관측은 최신값과 분리한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.04.json](basis/v3/tasks/CS-PACK.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
현실 관측을 시각과 revision에 맞춰 갱신한다

## 입력·출력 인터페이스
Observation {targetId,observedAt,receivedAt,value,unit,sourceHash,quality}; 현실 자료 없음은 기술 개발의 전역 blocker가 아님.

**직접 담당 요구:** REQ-050
**지원 요구:** REQ-050
**부모 제품 시험:** AT-050

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Content/RealityBaselineUpdater.cs` | CS-PACK.04.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0402Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.04.01 | `OUT-CS-PACK.04.01@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | integration | ALWAYS |
| CS-PACK.01.02 | `OUT-CS-PACK.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-PACK.04-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.

## 구현 순서
```text
1. 관측시각·수신시각·품질을 검사한 후 새 baseline revision을 만든다. 늦은 과거 관측은 최신값과 분리한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 현실 관측을 시각과 revision에 맞춰 갱신한다의 유효 조건
When: 현실 관측을 시각과 revision에 맞춰 갱신한다를 실행한다
Then: 새 관측은 영향받는 모델 자격만 재검토하고 이전 revision은 보존한다.

### AC-CS-PACK.04.02-N · NEGATIVE · NOT_RUN
Given: 가상 문 개방 이벤트를 현실 관측으로 넣거나 늦은 보고로 최신값을 덮지 못한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 가상 문 개방 이벤트를 현실 관측으로 넣거나 늦은 보고로 최신값을 덮지 못한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0402Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [MAN-KORAIL](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-DTC](https://www.digitaltwinconsortium.org/initiatives/the-definition-of-a-digital-twin/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-SOSA](https://www.w3.org/TR/vocab-ssn/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.01.01 · 단일 run writer와 순서 있는 명령 큐를 만든다

**상위:** CS-OPS.01 / CS-OPS · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** run별 mailbox에서 tick·priority·sequence를 고정하고 불변 입력을 복사한다. projection은 읽기 전용이다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.01.json](basis/v3/tasks/CS-OPS.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
단일 run writer와 순서 있는 명령 큐를 만든다

## 입력·출력 인터페이스
CommandIntent→CommandReceipt, stateVersion/readSet; 수락은 수행완료와 다르다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-001, REQ-071
**부모 제품 시험:** AT-001, AT-071

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Application/OperationsSession.cs` | CS-OPS.01.01 |
| CREATE | `Assets/ChooGuard/Domain/RunState.cs` | CS-OPS.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0101Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. run별 mailbox에서 tick·priority·sequence를 고정하고 불변 입력을 복사한다. projection은 읽기 전용이다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 단일 run writer와 순서 있는 명령 큐를 만든다의 유효 조건
When: 단일 run writer와 순서 있는 명령 큐를 만든다를 실행한다
Then: 같은 순서의 명령이 같은 의미 상태를 만들고 writer가 하나다.

### AC-CS-OPS.01.01-N · NEGATIVE · NOT_RUN
Given: 재진입 mutation·다른 run 입력·외부 DTO 변경으로 상태가 바뀌지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 재진입 mutation·다른 run 입력·외부 DTO 변경으로 상태가 바뀌지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-OPS.01.02 · 부작용 없는 preview와 Submit 재검사를 구현한다

**상위:** CS-OPS.01 / CS-OPS · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** Preview는 guard/readSet을 계산만 한다. Submit 시 새 revision·대상·역할을 다시 검사한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.01.json](basis/v3/tasks/CS-OPS.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
부작용 없는 preview와 Submit 재검사를 구현한다

## 입력·출력 인터페이스
CommandIntent→CommandReceipt, stateVersion/readSet; 수락은 수행완료와 다르다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-001, REQ-071
**부모 제품 시험:** AT-001, AT-071

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Application/CommandDispatcher.cs` | CS-OPS.01.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0102Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.01.01 | `OUT-CS-OPS.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. Preview는 guard/readSet을 계산만 한다. Submit 시 새 revision·대상·역할을 다시 검사한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 부작용 없는 preview와 Submit 재검사를 구현한다의 유효 조건
When: 부작용 없는 preview와 Submit 재검사를 구현한다를 실행한다
Then: 미리보기 전후 상태 해시가 같고 오래된 미리보기로 제출하면 STALE_STATE다.

### AC-CS-OPS.01.02-N · NEGATIVE · NOT_RUN
Given: Preview가 예약/outbox를 생성하거나 변경된 자원 상태를 무시하면 실패한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: Preview가 예약/outbox를 생성하거나 변경된 자원 상태를 무시하면 실패한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-OPS.01.03 · 요청키 멱등성과 응답 손실 후 조회를 구현한다

**상위:** CS-OPS.01 / CS-OPS · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** runId/requesterId/intentId와 의미 fingerprint로 같은 요청을 조회한다. 저장 후 취소는 재적용이나 rollback이 아니다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.01.json](basis/v3/tasks/CS-OPS.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
요청키 멱등성과 응답 손실 후 조회를 구현한다

## 입력·출력 인터페이스
CommandIntent→CommandReceipt, stateVersion/readSet; 수락은 수행완료와 다르다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-001, REQ-071
**부모 제품 시험:** AT-001, AT-071

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Application/OperationsSession.cs` | CS-OPS.01.01 |
| MODIFY | `Assets/ChooGuard/Application/CommandDispatcher.cs` | CS-OPS.01.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0103Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.01.02 | `OUT-CS-OPS.01.02@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | integration | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. runId/requesterId/intentId와 의미 fingerprint로 같은 요청을 조회한다. 저장 후 취소는 재적용이나 rollback이 아니다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.01.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 요청키 멱등성과 응답 손실 후 조회를 구현한다의 유효 조건
When: 요청키 멱등성과 응답 손실 후 조회를 구현한다를 실행한다
Then: 같은 key/payload는 같은 durable receipt, 다른 requester는 독립 key다.

### AC-CS-OPS.01.03-N · NEGATIVE · NOT_RUN
Given: 같은 key 다른 payload는 INTENT_CONFLICT이며 응답 손실 재시도도 effect 1회다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 같은 key 다른 payload는 INTENT_CONFLICT이며 응답 손실 재시도도 effect 1회다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0103Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.01.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-OPS.02.01 · 지원 native SQLite를 Player에서 열고 스키마를 준비한다

**상위:** CS-OPS.02 / CS-OPS · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** v3 SQLite 최소 안전조건·source-id·binary hash를 실제 provider로 확인한다. WAL/FULL/foreign_keys와 migration version을 읽는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.02.json](basis/v3/tasks/CS-OPS.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
지원 native SQLite를 Player에서 열고 스키마를 준비한다

## 입력·출력 인터페이스
IRunStore.CommitAsync(CommitBatch,CancellationToken) → Task<CommitReceipt>. 정확 필드는 schemas/CommitBatch.schema.json 및 schemas/CommitReceipt.schema.json; K03 원자성 규칙 적용.

**직접 담당 요구:** REQ-024, REQ-071
**지원 요구:** REQ-024, REQ-071
**부모 제품 시험:** AT-024, AT-071

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Persistence/SqliteRunStore.cs` | CS-OPS.02.01 |
| CREATE | `Assets/ChooGuard/Persistence/SqliteProvider.cs` | CS-OPS.02.01 |
| CREATE | `Assets/ChooGuard/Persistence/Schema.sql` | CS-OPS.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0201Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-SQLITE` / integration / ALWAYS: 실제 native SQLite provider·source-id·hash·지원 backend — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. v3 SQLite 최소 안전조건·source-id·binary hash를 실제 provider로 확인한다. WAL/FULL/foreign_keys와 migration version을 읽는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 지원 native SQLite를 Player에서 열고 스키마를 준비한다의 유효 조건
When: 지원 native SQLite를 Player에서 열고 스키마를 준비한다를 실행한다
Then: 실제 Player의 파일 DB 생성·재열기·설정 receipt가 남는다.

### AC-CS-OPS.02.01-N · NEGATIVE · NOT_RUN
Given: P/Invoke 실패·구버전 바이너리·in-memory만 성공한 경우 디스크 통합을 통과시키지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: P/Invoke 실패·구버전 바이너리·in-memory만 성공한 경우 디스크 통합을 통과시키지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [TECH-TAPAAL](https://www.tapaal.net/features/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [AUD-SQLITE-WAL](https://sqlite.org/wal.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.02.02 · 이벤트·예약·receipt를 원자 트랜잭션으로 저장한다

**상위:** CS-OPS.02 / CS-OPS · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** BEGIN IMMEDIATE에서 readSet과 자원 전체를 검사한다. 실패는 자신의 변경만 되돌리고 commit 뒤 acceptance를 게시한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.02.json](basis/v3/tasks/CS-OPS.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
이벤트·예약·receipt를 원자 트랜잭션으로 저장한다

## 입력·출력 인터페이스
IRunStore.CommitAsync(CommitBatch,CancellationToken) → Task<CommitReceipt>. 정확 필드는 schemas/CommitBatch.schema.json 및 schemas/CommitReceipt.schema.json; K03 원자성 규칙 적용.

**직접 담당 요구:** REQ-024, REQ-071
**지원 요구:** REQ-024, REQ-071
**부모 제품 시험:** AT-024, AT-071

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Persistence/SqliteRunStore.cs` | CS-OPS.02.01 |
| MODIFY | `Assets/ChooGuard/Persistence/Schema.sql` | CS-OPS.02.01 |
| CREATE | `Assets/ChooGuard/Domain/ReservationPlanner.cs` | CS-OPS.02.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0202Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.02.01 | `OUT-CS-OPS.02.01@candidate` | candidate | ALWAYS |
| CS-OPS.01.02 | `OUT-CS-OPS.01.02@candidate` | candidate | ALWAYS |
| CS-OPS.03.01 | `OUT-CS-OPS.03.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. BEGIN IMMEDIATE에서 readSet과 자원 전체를 검사한다. 실패는 자신의 변경만 되돌리고 commit 뒤 acceptance를 게시한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 이벤트·예약·receipt를 원자 트랜잭션으로 저장한다의 유효 조건
When: 이벤트·예약·receipt를 원자 트랜잭션으로 저장한다를 실행한다
Then: 정상 요청에서 event/reservation/receipt/revision이 한 commit으로 일치한다.

### AC-CS-OPS.02.02-N · NEGATIVE · NOT_RUN
Given: 장비 하나 부족·commit 실패에서 부분 예약과 성공 응답이 0이다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 장비 하나 부족·commit 실패에서 부분 예약과 성공 응답이 0이다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [TECH-TAPAAL](https://www.tapaal.net/features/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [AUD-SQLITE-WAL](https://sqlite.org/wal.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.02.03 · 트랜잭션 outbox와 중복 결과 방지를 구현한다

**상위:** CS-OPS.02 / CS-OPS · **작업창:** W3 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 같은 트랜잭션에 job을 기록하고 전달·응답 수신을 idempotent하게 관리한다. 미전송 job은 재시도할 수 있다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.02.json](basis/v3/tasks/CS-OPS.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
트랜잭션 outbox와 중복 결과 방지를 구현한다

## 입력·출력 인터페이스
IRunStore.CommitAsync(CommitBatch,CancellationToken) → Task<CommitReceipt>. 정확 필드는 schemas/CommitBatch.schema.json 및 schemas/CommitReceipt.schema.json; K03 원자성 규칙 적용.

**직접 담당 요구:** REQ-024, REQ-071
**지원 요구:** REQ-024, REQ-071
**부모 제품 시험:** AT-024, AT-071

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Persistence/SqliteRunStore.cs` | CS-OPS.02.01 |
| MODIFY | `Assets/ChooGuard/Persistence/Schema.sql` | CS-OPS.02.01 |
| CREATE | `Assets/ChooGuard/Application/OutboxDispatcher.cs` | CS-OPS.02.03 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0203Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 같은 트랜잭션에 job을 기록하고 전달·응답 수신을 idempotent하게 관리한다. 미전송 job은 재시도할 수 있다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.02.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 트랜잭션 outbox와 중복 결과 방지를 구현한다의 유효 조건
When: 트랜잭션 outbox와 중복 결과 방지를 구현한다를 실행한다
Then: crash 후 미전송 작업이 다시 전달돼도 result 효과가 한 번 적용된다.

### AC-CS-OPS.02.03-N · NEGATIVE · NOT_RUN
Given: 다른 run/job 또는 구 generation 결과로 상태를 갱신하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 다른 run/job 또는 구 generation 결과로 상태를 갱신하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0203Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.02.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [TECH-TAPAAL](https://www.tapaal.net/features/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [AUD-SQLITE-WAL](https://sqlite.org/wal.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.02.04 · 동시 예약·취소·재시작 장애를 주입해 검증한다

**상위:** CS-OPS.02 / CS-OPS · **작업창:** W3 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실제 파일 DB와 실제 provider에서 경쟁 요청·취소 재전송·강제종료 후 복구를 시험한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.02.json](basis/v3/tasks/CS-OPS.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
동시 예약·취소·재시작 장애를 주입해 검증한다

## 입력·출력 인터페이스
IRunStore.CommitAsync(CommitBatch,CancellationToken) → Task<CommitReceipt>. 정확 필드는 schemas/CommitBatch.schema.json 및 schemas/CommitReceipt.schema.json; K03 원자성 규칙 적용.

**직접 담당 요구:** REQ-024, REQ-071
**지원 요구:** REQ-024, REQ-071
**부모 제품 시험:** AT-024, AT-071

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Persistence/SqliteRunStore.cs` | CS-OPS.02.01 |
| MODIFY | `Assets/ChooGuard/Persistence/Schema.sql` | CS-OPS.02.01 |
| MODIFY | `Assets/ChooGuard/Domain/ReservationPlanner.cs` | CS-OPS.02.02 |
| MODIFY | `Assets/ChooGuard/Application/OutboxDispatcher.cs` | CS-OPS.02.03 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0204Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.02.03 | `OUT-CS-OPS.02.03@candidate` | candidate | ALWAYS |
| CS-OPS.01.03 | `OUT-CS-OPS.01.03@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 실제 파일 DB와 실제 provider에서 경쟁 요청·취소 재전송·강제종료 후 복구를 시험한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.02.04-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 동시 예약·취소·재시작 장애를 주입해 검증한다의 유효 조건
When: 동시 예약·취소·재시작 장애를 주입해 검증한다를 실행한다
Then: 한 자원에 한 예약만 남고 재열기 후 receipt·outbox·revision이 일치한다.

### AC-CS-OPS.02.04-N · NEGATIVE · NOT_RUN
Given: 쓰기 실패/손상 receipt를 정상 성공으로 cache하거나 남의 예약을 취소하지 못한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 쓰기 실패/손상 receipt를 정상 성공으로 cache하거나 남의 예약을 취소하지 못한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0204Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.02.04 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [TECH-TAPAAL](https://www.tapaal.net/features/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [AUD-SQLITE-WAL](https://sqlite.org/wal.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.03.01 · 기관 내부 지시와 외부 지원요청을 분리한다

**상위:** CS-OPS.03 / CS-OPS · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 작성자 편집권과 acting agency/team 업무권을 따로 평가한다. 요청과 배정 이벤트를 분리한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.03.json](basis/v3/tasks/CS-OPS.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
기관 내부 지시와 외부 지원요청을 분리한다

## 입력·출력 인터페이스
AuthorityGrant {holder,agency,scope,revision,ruleRef}; RequestSupport와 AssignTask는 구별.

**직접 담당 요구:** REQ-021, REQ-022
**지원 요구:** REQ-021, REQ-022
**부모 제품 시험:** AT-021, AT-022

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Domain/AuthorityPolicy.cs` | CS-OPS.03.01 |
| CREATE | `Assets/ChooGuard/Domain/SupportRequest.cs` | CS-OPS.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0301Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 작성자 편집권과 acting agency/team 업무권을 따로 평가한다. 요청과 배정 이벤트를 분리한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 기관 내부 지시와 외부 지원요청을 분리한다의 유효 조건
When: 기관 내부 지시와 외부 지원요청을 분리한다를 실행한다
Then: 내부 지시와 외부 요청에 서로 다른 receipt/진행 경로가 남는다.

### AC-CS-OPS.03.01-N · NEGATIVE · NOT_RUN
Given: 상대 기관의 권한을 사용자 분석권으로 우회할 수 없다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 상대 기관의 권한을 사용자 분석권으로 우회할 수 없다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [MAN-KORAIL](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.03.02 · 지휘권 인계를 revision과 확인에 결속한다

**상위:** CS-OPS.03 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 위임범위·원 revision·수신 확인·사유를 검사해 권한 변경을 기록한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.03.json](basis/v3/tasks/CS-OPS.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
지휘권 인계를 revision과 확인에 결속한다

## 입력·출력 인터페이스
AuthorityGrant {holder,agency,scope,revision,ruleRef}; RequestSupport와 AssignTask는 구별.

**직접 담당 요구:** REQ-021, REQ-022
**지원 요구:** REQ-021, REQ-022
**부모 제품 시험:** AT-021, AT-022

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Domain/AuthorityPolicy.cs` | CS-OPS.03.01 |
| CREATE | `Assets/ChooGuard/Domain/HandoverPolicy.cs` | CS-OPS.03.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0302Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.03.01 | `OUT-CS-OPS.03.01@candidate` | candidate | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | integration | ALWAYS |
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 위임범위·원 revision·수신 확인·사유를 검사해 권한 변경을 기록한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 지휘권 인계를 revision과 확인에 결속한다의 유효 조건
When: 지휘권 인계를 revision과 확인에 결속한다를 실행한다
Then: 유효 인계 후 해당 scope만 새 holder에게 연결된다.

### AC-CS-OPS.03.02-N · NEGATIVE · NOT_RUN
Given: 도착했다는 이유나 오래된 grant로 모든 기관 권한이 변경되지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 도착했다는 이유나 오래된 grant로 모든 기관 권한이 변경되지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [MAN-KORAIL](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.04.01 · 보고의 송신·접수·확인·만료를 분리한다

**상위:** CS-OPS.04 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** message ID·recipient·observedTick·receivedTick·TTL로 지식 상태를 갱신한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.04.json](basis/v3/tasks/CS-OPS.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
보고의 송신·접수·확인·만료를 분리한다

## 입력·출력 인터페이스
Message {id,sender,recipient,observedTick,receivedTick,expiry,payloadRef,ack}; projection은 read-only.

**직접 담당 요구:** REQ-011, REQ-027, REQ-028
**지원 요구:** REQ-011, REQ-027, REQ-028
**부모 제품 시험:** AT-011, AT-027, AT-028

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Domain/MessageLifecycle.cs` | CS-OPS.04.01 |
| CREATE | `Assets/ChooGuard/Domain/KnowledgeState.cs` | CS-OPS.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0401Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. message ID·recipient·observedTick·receivedTick·TTL로 지식 상태를 갱신한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 보고의 송신·접수·확인·만료를 분리한다의 유효 조건
When: 보고의 송신·접수·확인·만료를 분리한다를 실행한다
Then: 중복은 무효과, 역순/상충은 구별되며 확인 전 상태를 완료로 쓰지 않는다.

### AC-CS-OPS.04.01-N · NEGATIVE · NOT_RUN
Given: 미래 관측·잘못된 수신기관·만료 보고를 최신 확정 정보로 사용하지 못한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 미래 관측·잘못된 수신기관·만료 보고를 최신 확정 정보로 사용하지 못한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0401Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-OPS.04.02 · 작성자 보기와 기관 수신 정보 projection을 분리한다

**상위:** CS-OPS.04 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** backend에서 viewScope·requester 권한을 확인하고 기관별 허용 정보만 내보낸다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.04.json](basis/v3/tasks/CS-OPS.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
작성자 보기와 기관 수신 정보 projection을 분리한다

## 입력·출력 인터페이스
Message {id,sender,recipient,observedTick,receivedTick,expiry,payloadRef,ack}; projection은 read-only.

**직접 담당 요구:** REQ-011, REQ-027, REQ-028
**지원 요구:** REQ-011, REQ-027, REQ-028
**부모 제품 시험:** AT-011, AT-027, AT-028

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Domain/KnowledgeState.cs` | CS-OPS.04.01 |
| CREATE | `Assets/ChooGuard/Application/AgencyProjection.cs` | CS-OPS.04.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0402Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.04.01 | `OUT-CS-OPS.04.01@candidate` | candidate | ALWAYS |
| CS-OPS.03.02 | `OUT-CS-OPS.03.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. backend에서 viewScope·requester 권한을 확인하고 기관별 허용 정보만 내보낸다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 작성자 보기와 기관 수신 정보 projection을 분리한다의 유효 조건
When: 작성자 보기와 기관 수신 정보 projection을 분리한다를 실행한다
Then: 같은 run이라도 agency projection은 수신된 정보만 포함한다.

### AC-CS-OPS.04.02-N · NEGATIVE · NOT_RUN
Given: 분석/리플레이 화면 열기로 기관 knowledge가 바뀌거나 미래 이벤트가 노출되지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 분석/리플레이 화면 열기로 기관 knowledge가 바뀌거나 미래 이벤트가 노출되지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0402Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-OPS.05.01 · 선행조건·정보·공간 도달이 있는 업무망을 실행한다

**상위:** CS-OPS.05 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 요청→준비→수행→완료는 명시 guard로 진행한다. 미확인 조건을 0초/true로 대체하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.05.json](basis/v3/tasks/CS-OPS.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
선행조건·정보·공간 도달이 있는 업무망을 실행한다

## 입력·출력 인터페이스
TaskState {lifecycle,guards,reasons,reservations,outcome,handoff}; GuardResult는 truth/status와 evidence refs를 가진다.

**직접 담당 요구:** REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045
**지원 요구:** REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045
**부모 제품 시험:** AT-012, AT-023, AT-025, AT-026, AT-029, AT-045

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Domain/WorkflowEngine.cs` | CS-OPS.05.01 |
| CREATE | `Assets/ChooGuard/Domain/TaskConditions.cs` | CS-OPS.05.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0501Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.03.01 | `OUT-CS-OPS.03.01@candidate` | candidate | ALWAYS |
| CS-OPS.04.01 | `OUT-CS-OPS.04.01@candidate` | candidate | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 요청→준비→수행→완료는 명시 guard로 진행한다. 미확인 조건을 0초/true로 대체하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.05.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 선행조건·정보·공간 도달이 있는 업무망을 실행한다의 유효 조건
When: 선행조건·정보·공간 도달이 있는 업무망을 실행한다를 실행한다
Then: 필수 보고 전에는 대기하고 전달 확인 후에만 수행 단계에 들어간다.

### AC-CS-OPS.05.01-N · NEGATIVE · NOT_RUN
Given: UI 애니메이션 종료만으로 완료하거나 미확인 소요시간을 사실값으로 계산하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: UI 애니메이션 종료만으로 완료하거나 미확인 소요시간을 사실값으로 계산하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0501Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.05.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [MAN-MEDICAL](https://www.mohw.go.kr/board.es?act=view&bid=0009&list_no=1478957&mid=a10402000000&nPage=1&tag=) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.05.02 · 인력·차량·장비의 점유와 반환 생명주기를 연결한다

**상위:** CS-OPS.05 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 팀 분리/결합·취소·교대·보급에서 소유 예약과 능력을 재평가한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.05.json](basis/v3/tasks/CS-OPS.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
인력·차량·장비의 점유와 반환 생명주기를 연결한다

## 입력·출력 인터페이스
TaskState {lifecycle,guards,reasons,reservations,outcome,handoff}; GuardResult는 truth/status와 evidence refs를 가진다.

**직접 담당 요구:** REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045
**지원 요구:** REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045
**부모 제품 시험:** AT-012, AT-023, AT-025, AT-026, AT-029, AT-045

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Domain/WorkflowEngine.cs` | CS-OPS.05.01 |
| CREATE | `Assets/ChooGuard/Domain/ResourceLifecycle.cs` | CS-OPS.05.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0502Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.05.01 | `OUT-CS-OPS.05.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 팀 분리/결합·취소·교대·보급에서 소유 예약과 능력을 재평가한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.05.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 인력·차량·장비의 점유와 반환 생명주기를 연결한다의 유효 조건
When: 인력·차량·장비의 점유와 반환 생명주기를 연결한다를 실행한다
Then: 업무 종료·반환·재투입 가능 상태가 분리되고 수량이 보존된다.

### AC-CS-OPS.05.02-N · NEGATIVE · NOT_RUN
Given: 팀 분리로 능력/장비가 복제되거나 취소 재전송이 자원을 추가 생성하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 팀 분리로 능력/장비가 복제되거나 취소 재전송이 자원을 추가 생성하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0502Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.05.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [MAN-MEDICAL](https://www.mohw.go.kr/board.es?act=view&bid=0009&list_no=1478957&mid=a10402000000&nPage=1&tag=) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.05.03 · 두 기관의 요청부터 보고·인계까지 수직 구간을 완성한다

**상위:** CS-OPS.05 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 합성 two-agency 입력에서 실제 authority/store/message/workflow를 연결해 무중단 한 흐름을 수행한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.05.json](basis/v3/tasks/CS-OPS.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
두 기관의 요청부터 보고·인계까지 수직 구간을 완성한다

## 입력·출력 인터페이스
TaskState {lifecycle,guards,reasons,reservations,outcome,handoff}; GuardResult는 truth/status와 evidence refs를 가진다.

**직접 담당 요구:** REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045
**지원 요구:** REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045
**부모 제품 시험:** AT-012, AT-023, AT-025, AT-026, AT-029, AT-045

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Domain/WorkflowEngine.cs` | CS-OPS.05.01 |
| MODIFY | `Assets/ChooGuard/Domain/ResourceLifecycle.cs` | CS-OPS.05.02 |
| MODIFY | `Assets/ChooGuard/Domain/TaskConditions.cs` | CS-OPS.05.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0503Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.05.02 | `OUT-CS-OPS.05.02@candidate` | candidate | ALWAYS |
| CS-OPS.04.02 | `OUT-CS-OPS.04.02@candidate` | integration | ALWAYS |
| CS-OPS.04.02 | `OUT-CS-OPS.04.02@candidate` | candidate | ALWAYS |
| CS-OPS.03.02 | `OUT-CS-OPS.03.02@candidate` | candidate | ALWAYS |
| CS-OPS.02.04 | `OUT-CS-OPS.02.04@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 합성 two-agency 입력에서 실제 authority/store/message/workflow를 연결해 무중단 한 흐름을 수행한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.05.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 두 기관의 요청부터 보고·인계까지 수직 구간을 완성한다의 유효 조건
When: 두 기관의 요청부터 보고·인계까지 수직 구간을 완성한다를 실행한다
Then: 요청·배정·수행·완료·회신의 각 이벤트와 상태가 저장돼 재현된다.

### AC-CS-OPS.05.03-N · NEGATIVE · NOT_RUN
Given: 정보 누락·예약 경쟁을 넣으면 정확한 단계에서 멈추며 전체 완료를 위조하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 정보 누락·예약 경쟁을 넣으면 정확한 단계에서 멈추며 전체 완료를 위조하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0503Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.05.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [MAN-MEDICAL](https://www.mohw.go.kr/board.es?act=view&bid=0009&list_no=1478957&mid=a10402000000&nPage=1&tag=) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.06.01 · 복수 원인을 원인축·근거·해결조건으로 반환한다

**상위:** CS-OPS.06 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 22개 원인 catalog와 lifecycle을 분리하고 causes/evidence/resolutionConditions를 함께 반환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.06.json](basis/v3/tasks/CS-OPS.06.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
복수 원인을 원인축·근거·해결조건으로 반환한다

## 입력·출력 인터페이스
Reason {axis,code,subject,causes,evidence,resolutionConditions}; 설명 모델은 권한·결과를 변경하지 못함.

**직접 담당 요구:** REQ-013, REQ-030, REQ-041
**지원 요구:** REQ-013, REQ-030, REQ-041
**부모 제품 시험:** AT-013, AT-030, AT-041

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Domain/ReasonEngine.cs` | CS-OPS.06.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0601Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.05.01 | `OUT-CS-OPS.05.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 22개 원인 catalog와 lifecycle을 분리하고 causes/evidence/resolutionConditions를 함께 반환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.06.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 복수 원인을 원인축·근거·해결조건으로 반환한다의 유효 조건
When: 복수 원인을 원인축·근거·해결조건으로 반환한다를 실행한다
Then: 자원 대기와 보고 미확인이 동시에 표시되며 하나를 해결해도 나머지는 유지된다.

### AC-CS-OPS.06.01-N · NEGATIVE · NOT_RUN
Given: 설명기가 실행 권한을 바꾸거나 원문 없는 원인을 승인 규칙처럼 표시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 설명기가 실행 권한을 바꾸거나 원문 없는 원인을 승인 규칙처럼 표시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0601Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.06.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [TECH-TAPAAL](https://www.tapaal.net/features/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.06.02 · 순환대기와 외부 대기를 구분하고 형식 모델과 대조한다

**상위:** CS-OPS.06 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** wait-for SCC와 외부 해제조건을 평가한다. 검증 가능한 부분을 TAPAAL trace로 투영한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.06.json](basis/v3/tasks/CS-OPS.06.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
순환대기와 외부 대기를 구분하고 형식 모델과 대조한다

## 입력·출력 인터페이스
Reason {axis,code,subject,causes,evidence,resolutionConditions}; 설명 모델은 권한·결과를 변경하지 못함.

**직접 담당 요구:** REQ-013, REQ-030, REQ-041
**지원 요구:** REQ-013, REQ-030, REQ-041
**부모 제품 시험:** AT-013, AT-030, AT-041

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Domain/WaitForAnalyzer.cs` | CS-OPS.06.02 |
| CREATE | `benchmarks/operations/model-projection.json` | CS-OPS.06.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0602Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.06.01 | `OUT-CS-OPS.06.01@candidate` | candidate | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | integration | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. wait-for SCC와 외부 해제조건을 평가한다. 검증 가능한 부분을 TAPAAL trace로 투영한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.06.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 순환대기와 외부 대기를 구분하고 형식 모델과 대조한다의 유효 조건
When: 순환대기와 외부 대기를 구분하고 형식 모델과 대조한다를 실행한다
Then: 확정 교착·외부 도착 대기·정보 부족을 별도 판정한다.

### AC-CS-OPS.06.02-N · NEGATIVE · NOT_RUN
Given: 모델검사 timeout이나 부분 state space를 deadlock-free로 승인하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 모델검사 timeout이나 부분 state space를 deadlock-free로 승인하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0602Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.06.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [TECH-TAPAAL](https://www.tapaal.net/features/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-OPS.07.01 · 동의 기반 활동구간과 개발자 도움을 기록한다

**상위:** CS-OPS.07 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 가명 참가자의 명시적 작업 전환만 기록한다. 키로깅·대본 원문을 계측 데이터로 수집하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.07.json](basis/v3/tasks/CS-OPS.07.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
동의 기반 활동구간과 개발자 도움을 기록한다

## 입력·출력 인터페이스
ActivityInterval {participantCode, activityId, category, startMonotonicUs, endMonotonicUs, observedWallAt, consentRef, assistance, completeness}; 가상시각을 사람 시간으로 집계하지 않음.

**직접 담당 요구:** REQ-079
**지원 요구:** REQ-079
**부모 제품 시험:** AT-079

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Application/ActivityRecorder.cs` | CS-OPS.07.01 |
| CREATE | `Assets/ChooGuard/Persistence/ActivityStore.cs` | CS-OPS.07.01 |
| CREATE | `Assets/ChooGuard/Contracts/ActivityTypes.cs` | CS-OPS.07.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0701Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 가명 참가자의 명시적 작업 전환만 기록한다. 키로깅·대본 원문을 계측 데이터로 수집하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.07.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 동의 기반 활동구간과 개발자 도움을 기록한다의 유효 조건
When: 동의 기반 활동구간과 개발자 도움을 기록한다를 실행한다
Then: case/participant/activity 구간과 도움 여부를 UTC·monotonic 시간으로 저장한다.

### AC-CS-OPS.07.01-N · NEGATIVE · NOT_RUN
Given: 동의가 없거나 철회되면 새 개인 계측을 중지하고 정책대로 처리한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 동의가 없거나 철회되면 새 개인 계측을 중지하고 정책대로 처리한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0701Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.07.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-OPS.07.02 · 총인시를 합집합과 검열구간 기준으로 집계한다

**상위:** CS-OPS.07 / CS-OPS · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 동일인의 중첩은 합집합, 다른 사람 시간은 합산한다. 무인계산/회신 대기를 분리한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-OPS.07.json](basis/v3/tasks/CS-OPS.07.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
총인시를 합집합과 검열구간 기준으로 집계한다

## 입력·출력 인터페이스
ActivityInterval {participantCode, activityId, category, startMonotonicUs, endMonotonicUs, observedWallAt, consentRef, assistance, completeness}; 가상시각을 사람 시간으로 집계하지 않음.

**직접 담당 요구:** REQ-079
**지원 요구:** REQ-079
**부모 제품 시험:** AT-079

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Application/ActivityRecorder.cs` | CS-OPS.07.01 |
| MODIFY | `Assets/ChooGuard/Persistence/ActivityStore.cs` | CS-OPS.07.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0702Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.07.01 | `OUT-CS-OPS.07.01@candidate` | candidate | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 동일인의 중첩은 합집합, 다른 사람 시간은 합산한다. 무인계산/회신 대기를 분리한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-OPS.07.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 총인시를 합집합과 검열구간 기준으로 집계한다의 유효 조건
When: 총인시를 합집합과 검열구간 기준으로 집계한다를 실행한다
Then: 한 사람의 겹친 두 10분 구간을 두 사람의 20인분으로 잘못 더하지 않는다.

### AC-CS-OPS.07.02-N · NEGATIVE · NOT_RUN
Given: 강제종료의 열린 구간이나 미완료 참가자를 평균에서 조용히 제외하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 강제종료의 열린 구간이나 미완료 참가자를 평균에서 조용히 제외하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSOPS0702Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-OPS.07.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-WORLD.01.01 · 새 2공간·문·1m 기준체 fixture를 생성한다

**상위:** CS-WORLD.01 / CS-WORLD · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** Editor builder가 stable ID의 새 Scene을 만든다. 시각·충돌·의미 레이어를 구분한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.01.json](basis/v3/tasks/CS-WORLD.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
새 2공간·문·1m 기준체 fixture를 생성한다

## 입력·출력 인터페이스
WorldAnchorMap {entityId,transformRef,frameId,representationKind}; Unity Transform은 표시 수단.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-001
**부모 제품 시험:** AT-001

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Editor/FixtureBuilder.cs` | CS-WORLD.01.01 |
| CREATE | `Assets/ChooGuard/World/WorldEntityAnchor.cs` | CS-WORLD.01.01 |
| CREATE | `Assets/ChooGuard/Scenes/OperationsFixture.unity` | CS-WORLD.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0101Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.01 | `OUT-CS-BOOT.02.01@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. Editor builder가 stable ID의 새 Scene을 만든다. 시각·충돌·의미 레이어를 구분한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 새 2공간·문·1m 기준체 fixture를 생성한다의 유효 조건
When: 새 2공간·문·1m 기준체 fixture를 생성한다를 실행한다
Then: SYNTHETIC_FIXTURE 장면의 두 구역과 문이 정의된 좌표에 있다.

### AC-CS-WORLD.01.01-N · NEGATIVE · NOT_RUN
Given: 다른 Scene 덮어쓰기·중복 anchor·음수 scale이면 생성을 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 다른 Scene 덮어쓰기·중복 anchor·음수 scale이면 생성을 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)


---

# CS-WORLD.01.02 · 문과 anchor를 읽기 전용 projection에 결속한다

**상위:** CS-WORLD.01 / CS-WORLD · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 선택/표현 상태는 entityId로 조회하고 문 renderer와 계산 경계를 독립 연결한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.01.json](basis/v3/tasks/CS-WORLD.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
문과 anchor를 읽기 전용 projection에 결속한다

## 입력·출력 인터페이스
WorldAnchorMap {entityId,transformRef,frameId,representationKind}; Unity Transform은 표시 수단.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-001
**부모 제품 시험:** AT-001

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Editor/FixtureBuilder.cs` | CS-WORLD.01.01 |
| MODIFY | `Assets/ChooGuard/World/WorldEntityAnchor.cs` | CS-WORLD.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0102Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-WORLD.01.01 | `OUT-CS-WORLD.01.01@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | integration | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 선택/표현 상태는 entityId로 조회하고 문 renderer와 계산 경계를 독립 연결한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 문과 anchor를 읽기 전용 projection에 결속한다의 유효 조건
When: 문과 anchor를 읽기 전용 projection에 결속한다를 실행한다
Then: projection revision에 따라 문 표현이 바뀌지만 UI가 domain에 쓰지 않는다.

### AC-CS-WORLD.01.02-N · NEGATIVE · NOT_RUN
Given: 층 숨김·scene 표시 변경으로 collision·예약·실제 문 상태가 없어지지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 층 숨김·scene 표시 변경으로 collision·예약·실제 문 상태가 없어지지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)


---

# CS-WORLD.02.01 · 대표 무료 모델을 격리 임포트하고 실제 부품을 기록한다

**상위:** CS-WORLD.02 / CS-WORLD · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 차량/인물/안전소품 하나씩 단위·축·pivot·rig·clips·material을 검사해 preset을 만든다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.02.json](basis/v3/tasks/CS-WORLD.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
대표 무료 모델을 격리 임포트하고 실제 부품을 기록한다

## 입력·출력 인터페이스
GeometryReceipt {assetId,sourceHash,transform,units,parts,residuals,scope}; 실제 맵 수용에는 현장 입력이 추가로 필요.

**직접 담당 요구:** REQ-014, REQ-017, REQ-018
**지원 요구:** REQ-014, REQ-017, REQ-018
**부모 제품 시험:** AT-014, AT-017, AT-018

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Editor/AssetImportPolicy.cs` | CS-WORLD.02.01 |
| CREATE | `Assets/ChooGuard/Art/first-site.asset-manifest.json` | CS-WORLD.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0201Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.03.01 | `OUT-CS-PACK.03.01@candidate` | candidate | ALWAYS |
| CS-WORLD.01.01 | `OUT-CS-WORLD.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-WORLD.02-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.

## 구현 순서
```text
1. 차량/인물/안전소품 하나씩 단위·축·pivot·rig·clips·material을 검사해 preset을 만든다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 대표 무료 모델을 격리 임포트하고 실제 부품을 기록한다의 유효 조건
When: 대표 무료 모델을 격리 임포트하고 실제 부품을 기록한다를 실행한다
Then: 실제 import 결과·해시·parts·보완 필요점이 남는다.

### AC-CS-WORLD.02.01-N · NEGATIVE · NOT_RUN
Given: 무료판 미확보·missing material을 완성 asset으로 등록하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 무료판 미확보·missing material을 완성 asset으로 등록하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [FREE-001](https://rgsdev.itch.io/free-low-poly-vehicles-pack) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-003](https://kenney.nl/assets/train-kit) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-018](https://polyhaven.com/a/korean_fire_extinguisher_01) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-BIPA-BUSAN3](https://bwebtoon.com/webtoon-home/core-businesses/digital-location/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-KTX-I](https://haesangang-0317.tistory.com/268) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-MAPANYTHING](https://github.com/facebookresearch/map-anything) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-UNITY-FBX](https://docs.unity3d.com/6000.3/Documentation/Manual/3D-formats.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-WORLD.02.02 · 첫 철도 구간을 근거가 있는 기하와 연결로 구축한다

**상위:** CS-WORLD.02 / CS-WORLD · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 관측/도면으로 외부 도착-맞이방-승강장-객실을 제한 범위에서 제작한다. 추정 구간은 분리 표기한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.02.json](basis/v3/tasks/CS-WORLD.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
첫 철도 구간을 근거가 있는 기하와 연결로 구축한다

## 입력·출력 인터페이스
GeometryReceipt {assetId,sourceHash,transform,units,parts,residuals,scope}; 실제 맵 수용에는 현장 입력이 추가로 필요.

**직접 담당 요구:** REQ-014, REQ-017, REQ-018
**지원 요구:** REQ-014, REQ-017, REQ-018
**부모 제품 시험:** AT-014, AT-017, AT-018

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/World/CoordinateValidator.cs` | CS-WORLD.02.02 |
| CREATE | `content/sites/first-site/geometry.json` | CS-WORLD.02.02 |
| CREATE | `Assets/ChooGuard/Scenes/FirstSite.unity` | CS-WORLD.02.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0202Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-WORLD.02.01 | `OUT-CS-WORLD.02.01@candidate` | candidate | ALWAYS |
| CS-PACK.04.01 | `OUT-CS-PACK.04.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-WORLD.02-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-SITE-GEOMETRY` / integration / ALWAYS: 첫 현장의 허용된 관측/도면; 없으면 fixture 작업으로 되돌림, 실물 제작이라고 부르지 않음 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 관측/도면으로 외부 도착-맞이방-승강장-객실을 제한 범위에서 제작한다. 추정 구간은 분리 표기한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 첫 철도 구간을 근거가 있는 기하와 연결로 구축한다의 유효 조건
When: 첫 철도 구간을 근거가 있는 기하와 연결로 구축한다를 실행한다
Then: 경계·층·좌표와 문/portal가 하나의 geometry revision에 결속된다.

### AC-CS-WORLD.02.02-N · NEGATIVE · NOT_RUN
Given: 다른 역의 모델 또는 복원 confidence만으로 실제 치수를 확정하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 다른 역의 모델 또는 복원 confidence만으로 실제 치수를 확정하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [FREE-001](https://rgsdev.itch.io/free-low-poly-vehicles-pack) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-003](https://kenney.nl/assets/train-kit) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-018](https://polyhaven.com/a/korean_fire_extinguisher_01) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-BIPA-BUSAN3](https://bwebtoon.com/webtoon-home/core-businesses/digital-location/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-KTX-I](https://haesangang-0317.tistory.com/268) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-MAPANYTHING](https://github.com/facebookresearch/map-anything) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-UNITY-FBX](https://docs.unity3d.com/6000.3/Documentation/Manual/3D-formats.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-WORLD.02.03 · 관측기반 복원과 독립 치수 잔차를 비교한다

**상위:** CS-WORLD.02 / CS-WORLD · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 학습기반 복원 후보와 COLMAP 대조의 입력·가중치·전처리를 고정한다. holdout 잔차를 별도 평가한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.02.json](basis/v3/tasks/CS-WORLD.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
관측기반 복원과 독립 치수 잔차를 비교한다

## 입력·출력 인터페이스
GeometryReceipt {assetId,sourceHash,transform,units,parts,residuals,scope}; 실제 맵 수용에는 현장 입력이 추가로 필요.

**직접 담당 요구:** REQ-014, REQ-017, REQ-018
**지원 요구:** REQ-014, REQ-017, REQ-018
**부모 제품 시험:** AT-014, AT-017, AT-018

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/World/CoordinateValidator.cs` | CS-WORLD.02.02 |
| MODIFY | `Assets/ChooGuard/Art/first-site.asset-manifest.json` | CS-WORLD.02.01 |
| MODIFY | `content/sites/first-site/geometry.json` | CS-WORLD.02.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0203Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-WORLD.02.02 | `OUT-CS-WORLD.02.02@candidate` | candidate | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | integration | ALWAYS |
| CS-PACK.03.02 | `OUT-CS-PACK.03.02@candidate` | integration | ALWAYS |
| CS-PACK.04.02 | `OUT-CS-PACK.04.02@candidate` | qualification | NAMED_SITE_ACCURACY |

## 외부 입력과 보류 범위
- `EXT-CS-WORLD.02-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-HOLDOUT` / qualification / ALWAYS: 보정에 쓰지 않은 독립 기준 치수·오차 기준 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 학습기반 복원 후보와 COLMAP 대조의 입력·가중치·전처리를 고정한다. holdout 잔차를 별도 평가한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.02.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 관측기반 복원과 독립 치수 잔차를 비교한다의 유효 조건
When: 관측기반 복원과 독립 치수 잔차를 비교한다를 실행한다
Then: 검증점을 보정에 쓰지 않고 오차·범위·실패 구간을 보고한다.

### AC-CS-WORLD.02.03-N · NEGATIVE · NOT_RUN
Given: 기준점 재사용·누락 표면 은폐·정밀도 불충분이면 현장 기하 수용을 보류한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 기준점 재사용·누락 표면 은폐·정밀도 불충분이면 현장 기하 수용을 보류한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0203Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.02.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [FREE-001](https://rgsdev.itch.io/free-low-poly-vehicles-pack) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-003](https://kenney.nl/assets/train-kit) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-018](https://polyhaven.com/a/korean_fire_extinguisher_01) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-BIPA-BUSAN3](https://bwebtoon.com/webtoon-home/core-businesses/digital-location/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAT-KTX-I](https://haesangang-0317.tistory.com/268) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-MAPANYTHING](https://github.com/facebookresearch/map-anything) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-UNITY-FBX](https://docs.unity3d.com/6000.3/Documentation/Manual/3D-formats.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-WORLD.03.01 · 올바른 run·revision의 projection만 월드에 적용한다

**상위:** CS-WORLD.03 / CS-WORLD · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** main thread DTO queue에서 늦은/다른 branch 결과를 버리고 최신 수용 상태만 표시한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.03.json](basis/v3/tasks/CS-WORLD.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
올바른 run·revision의 projection만 월드에 적용한다

## 입력·출력 인터페이스
SessionProjection: schemas/SessionProjection.schema.json의 entitiesRef/tasksRef/reasonsRef를 해석하며 run/revision/viewScope를 확인한다. 표현 계층은 domain을 직접 쓰지 않는다.

**직접 담당 요구:** REQ-016
**지원 요구:** REQ-016
**부모 제품 시험:** AT-016

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/World/WorldProjectionApplier.cs` | CS-WORLD.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0301Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. main thread DTO queue에서 늦은/다른 branch 결과를 버리고 최신 수용 상태만 표시한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 올바른 run·revision의 projection만 월드에 적용한다의 유효 조건
When: 올바른 run·revision의 projection만 월드에 적용한다를 실행한다
Then: branch 전환 뒤 이전 응답이 위치나 상태를 덮어쓰지 않는다.

### AC-CS-WORLD.03.01-N · NEGATIVE · NOT_RUN
Given: worker thread가 Unity API를 호출하거나 임의 transform이 정본이 되지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: worker thread가 Unity API를 호출하거나 임의 transform이 정본이 되지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [TECH-UNITY-LOAD](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-WORLD.03.02 · 층별 표시와 pooled 월드마커를 구현한다

**상위:** CS-WORLD.03 / CS-WORLD · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 현재 층·기관 권한·카메라 방향으로 라벨을 필터링하고 선택 대상을 우선한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.03.json](basis/v3/tasks/CS-WORLD.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
층별 표시와 pooled 월드마커를 구현한다

## 입력·출력 인터페이스
SessionProjection: schemas/SessionProjection.schema.json의 entitiesRef/tasksRef/reasonsRef를 해석하며 run/revision/viewScope를 확인한다. 표현 계층은 domain을 직접 쓰지 않는다.

**직접 담당 요구:** REQ-016
**지원 요구:** REQ-016
**부모 제품 시험:** AT-016

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/World/WorldMarkerPool.cs` | CS-WORLD.03.02 |
| CREATE | `Assets/ChooGuard/World/FloorVisibility.cs` | CS-WORLD.03.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0302Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-WORLD.03.01 | `OUT-CS-WORLD.03.01@candidate` | candidate | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | integration | ALWAYS |
| CS-OPS.01.03 | `OUT-CS-OPS.01.03@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 현재 층·기관 권한·카메라 방향으로 라벨을 필터링하고 선택 대상을 우선한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 층별 표시와 pooled 월드마커를 구현한다의 유효 조건
When: 층별 표시와 pooled 월드마커를 구현한다를 실행한다
Then: 숨김·LOD·pool 재사용 후에도 라벨과 실제 EntityId가 일치한다.

### AC-CS-WORLD.03.02-N · NEGATIVE · NOT_RUN
Given: 다른 층/기관의 숨은 정보나 pooled 이전 이름이 새 객체에 남지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 다른 층/기관의 숨은 정보나 pooled 이전 이름이 새 객체에 남지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [TECH-UNITY-LOAD](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-WORLD.04.01 · 구역 준비상태에 따라 진입과 additive 로딩을 제어한다

**상위:** CS-WORLD.04 / CS-WORLD · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** visual/collision/path/state readiness를 따로 검사한다. unload는 운영 수명을 종료하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.04.json](basis/v3/tasks/CS-WORLD.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
구역 준비상태에 따라 진입과 additive 로딩을 제어한다

## 입력·출력 인터페이스
RegionReadiness {regionId,visual,collision,path,state}; DockLink {vehicleFrame,siteFrame,stopConfirmed,doorState}.

**직접 담당 요구:** REQ-020
**지원 요구:** REQ-020
**부모 제품 시험:** AT-020

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/World/RegionLoader.cs` | CS-WORLD.04.01 |
| CREATE | `Assets/ChooGuard/World/RegionReadiness.cs` | CS-WORLD.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0401Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-WORLD.03.01 | `OUT-CS-WORLD.03.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. visual/collision/path/state readiness를 따로 검사한다. unload는 운영 수명을 종료하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 구역 준비상태에 따라 진입과 additive 로딩을 제어한다의 유효 조건
When: 구역 준비상태에 따라 진입과 additive 로딩을 제어한다를 실행한다
Then: 필수 충돌·경로·상태 준비 후에만 진입이 허용된다.

### AC-CS-WORLD.04.01-N · NEGATIVE · NOT_RUN
Given: 로딩 실패·해제된 구역이 진행 중 메시지/예약을 삭제하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 로딩 실패·해제된 구역이 진행 중 메시지/예약을 삭제하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0401Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [TECH-UNITY-LOAD](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-WORLD.04.02 · 정차·도킹·문과 차량 좌표계를 연결한다

**상위:** CS-WORLD.04 / CS-WORLD · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 차량 local frame과 현장 frame의 변환·정차 확인·개방 상태로 통행을 결정한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-WORLD.04.json](basis/v3/tasks/CS-WORLD.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
정차·도킹·문과 차량 좌표계를 연결한다

## 입력·출력 인터페이스
RegionReadiness {regionId,visual,collision,path,state}; DockLink {vehicleFrame,siteFrame,stopConfirmed,doorState}.

**직접 담당 요구:** REQ-020
**지원 요구:** REQ-020
**부모 제품 시험:** AT-020

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/World/VehicleFrameBinding.cs` | CS-WORLD.04.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0402Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-WORLD.04.01 | `OUT-CS-WORLD.04.01@candidate` | candidate | ALWAYS |
| CS-WORLD.03.02 | `OUT-CS-WORLD.03.02@candidate` | integration | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 차량 local frame과 현장 frame의 변환·정차 확인·개방 상태로 통행을 결정한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-WORLD.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 정차·도킹·문과 차량 좌표계를 연결한다의 유효 조건
When: 정차·도킹·문과 차량 좌표계를 연결한다를 실행한다
Then: 정차 연결 중 객실 개체가 같은 도킹 경계를 사용한다.

### AC-CS-WORLD.04.02-N · NEGATIVE · NOT_RUN
Given: 움직이는 차량에 고정 승강장 통로가 남거나 인물이 이전 좌표에 잔류하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 움직이는 차량에 고정 승강장 통로가 남거나 인물이 이전 좌표에 잔류하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0402Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-WORLD.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/01-build-and-assemblies.md](basis/v3/specs/01-build-and-assemblies.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/07-content-and-rule-contract.md](basis/v3/specs/07-content-and-rule-contract.md)
- [TECH-UNITY-LOAD](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PLAY.01.01 · 포인터 입력의 최초 소유자를 끝까지 유지한다

**상위:** CS-PLAY.01 / CS-PLAY · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 모달→텍스트→HUD→월드→카메라 우선순위를 두고 pointer-down에서 owner를 고정한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.01.json](basis/v3/tasks/CS-PLAY.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
포인터 입력의 최초 소유자를 끝까지 유지한다

## 입력·출력 인터페이스
InputOwner {context,pointerId,focus,modal}; 소비한 event를 하위 입력에 재전달하지 않는다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Input/InputContextRouter.cs` | CS-PLAY.01.01 |
| CREATE | `Assets/ChooGuard/Presentation/Input/PointerCaptureOwner.cs` | CS-PLAY.01.01 |
| CREATE | `Assets/ChooGuard/Presentation/Input/Operations.inputactions` | CS-PLAY.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0101Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.01.02 | `OUT-CS-BOOT.01.02@candidate` | candidate | ALWAYS |
| CS-BOOT.02.01 | `OUT-CS-BOOT.02.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 모달→텍스트→HUD→월드→카메라 우선순위를 두고 pointer-down에서 owner를 고정한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 포인터 입력의 최초 소유자를 끝까지 유지한다의 유효 조건
When: 포인터 입력의 최초 소유자를 끝까지 유지한다를 실행한다
Then: HUD 클릭/드래그/휠은 뒤 월드 선택·줌을 발생시키지 않는다.

### AC-CS-PLAY.01.01-N · NEGATIVE · NOT_RUN
Given: 패널 밖 release·focus 상실·capture 취소에서 월드 명령이 튀어나오지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 패널 밖 release·focus 상실·capture 취소에서 월드 명령이 튀어나오지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.01.02 · 한글 IME와 모달 키보드 포커스를 격리한다

**상위:** CS-PLAY.01 / CS-PLAY · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 조합·확정·취소·Enter/Esc/Space를 TMP focus에 먼저 전달한다. 모달 닫기 전 포커스를 보존한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.01.json](basis/v3/tasks/CS-PLAY.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
한글 IME와 모달 키보드 포커스를 격리한다

## 입력·출력 인터페이스
InputOwner {context,pointerId,focus,modal}; 소비한 event를 하위 입력에 재전달하지 않는다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Presentation/Input/InputContextRouter.cs` | CS-PLAY.01.01 |
| MODIFY | `Assets/ChooGuard/Presentation/Input/Operations.inputactions` | CS-PLAY.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0102Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.01.01 | `OUT-CS-PLAY.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 조합·확정·취소·Enter/Esc/Space를 TMP focus에 먼저 전달한다. 모달 닫기 전 포커스를 보존한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 한글 IME와 모달 키보드 포커스를 격리한다의 유효 조건
When: 한글 IME와 모달 키보드 포커스를 격리한다를 실행한다
Then: 한글 입력 중 WASD/Space가 카메라·pause를 실행하지 않는다.

### AC-CS-PLAY.01.02-N · NEGATIVE · NOT_RUN
Given: IME Esc가 조합취소와 모달종료를 동시에 수행하거나 focus가 배경으로 이탈하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: IME Esc가 조합취소와 모달종료를 동시에 수행하거나 focus가 배경으로 이탈하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.01.03 · Player에서 재배정·포인터/키보드 충돌을 확인한다

**상위:** CS-PLAY.01 / CS-PLAY · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실제 build에서 동일 actionmaps와 이벤트 소유권을 검사한다. 브라우저/Editor 결과로 대체하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.01.json](basis/v3/tasks/CS-PLAY.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
Player에서 재배정·포인터/키보드 충돌을 확인한다

## 입력·출력 인터페이스
InputOwner {context,pointerId,focus,modal}; 소비한 event를 하위 입력에 재전달하지 않는다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Presentation/Input/InputContextRouter.cs` | CS-PLAY.01.01 |
| MODIFY | `Assets/ChooGuard/Presentation/Input/PointerCaptureOwner.cs` | CS-PLAY.01.01 |
| MODIFY | `Assets/ChooGuard/Presentation/Input/Operations.inputactions` | CS-PLAY.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0103Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.01.02 | `OUT-CS-PLAY.01.02@candidate` | candidate | ALWAYS |
| CS-BOOT.01.01 | `OUT-CS-BOOT.01.01@candidate` | integration | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 실제 build에서 동일 actionmaps와 이벤트 소유권을 검사한다. 브라우저/Editor 결과로 대체하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.01.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 Player에서 재배정·포인터/키보드 충돌을 확인한다의 유효 조건
When: Player에서 재배정·포인터/키보드 충돌을 확인한다를 실행한다
Then: 사용한 OS·resolution·IME의 입력 결과와 기록이 남는다.

### AC-CS-PLAY.01.03-N · NEGATIVE · NOT_RUN
Given: 중복 binding이나 누락된 native runtime 시험이면 지원 완료로 표시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 중복 binding이나 누락된 native runtime 시험이면 지원 완료로 표시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0103Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.01.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.02.01 · 팀/차량과 업무 선택을 같은 SelectionSet에 연결한다

**상위:** CS-PLAY.02 / CS-PLAY · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 목록과 월드가 같은 entityId 집합을 읽고 혼합기관과 승무원 연계를 표시한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.02.json](basis/v3/tasks/CS-PLAY.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
팀/차량과 업무 선택을 같은 SelectionSet에 연결한다

## 입력·출력 인터페이스
SelectionSet→CommandIntent→PreviewResult→CommandReceipt; UI 수락은 업무 완료가 아님.

**직접 담당 요구:** REQ-008, REQ-009, REQ-010
**지원 요구:** REQ-008, REQ-009, REQ-010
**부모 제품 시험:** AT-008, AT-009, AT-010

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Selection/SelectionService.cs` | CS-PLAY.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0201Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.01.01 | `OUT-CS-PLAY.01.01@candidate` | candidate | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 목록과 월드가 같은 entityId 집합을 읽고 혼합기관과 승무원 연계를 표시한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 팀/차량과 업무 선택을 같은 SelectionSet에 연결한다의 유효 조건
When: 팀/차량과 업무 선택을 같은 SelectionSet에 연결한다를 실행한다
Then: 두 경로로 같은 팀을 선택하면 동일 대상으로 명령 preview를 만든다.

### AC-CS-PLAY.02.01-N · NEGATIVE · NOT_RUN
Given: 숨긴/다른 층/권한 밖 객체가 드래그에 조용히 포함되지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 숨긴/다른 층/권한 밖 객체가 드래그에 조용히 포함되지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [GAME-EMERGENCY](https://www.world-of-emergency.com/news/2023-09-15/234-the_new_emergency) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [GAME-RESCUEHQ](https://store.playstation.com/en-us/concept/10002295) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PLAY.02.02 · 대상별 조건을 보여주고 확인한 요청만 제출한다

**상위:** CS-PLAY.02 / CS-PLAY · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** PreviewResult의 적격·부적격·잔여 조건을 표시하고 명시 확인 뒤 동일 intent를 보낸다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.02.json](basis/v3/tasks/CS-PLAY.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
대상별 조건을 보여주고 확인한 요청만 제출한다

## 입력·출력 인터페이스
SelectionSet→CommandIntent→PreviewResult→CommandReceipt; UI 수락은 업무 완료가 아님.

**직접 담당 요구:** REQ-008, REQ-009, REQ-010
**지원 요구:** REQ-008, REQ-009, REQ-010
**부모 제품 시험:** AT-008, AT-009, AT-010

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Commands/CommandPreviewPresenter.cs` | CS-PLAY.02.02 |
| CREATE | `Assets/ChooGuard/Presentation/Commands/CommandPreview.prefab` | CS-PLAY.02.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0202Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.02.01 | `OUT-CS-PLAY.02.01@candidate` | candidate | ALWAYS |
| CS-PLAY.01.03 | `OUT-CS-PLAY.01.03@candidate` | integration | ALWAYS |
| CS-OPS.01.03 | `OUT-CS-OPS.01.03@candidate` | integration | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | integration | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |
| CS-OPS.01.03 | `OUT-CS-OPS.01.03@integration` | integration | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@integration` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. PreviewResult의 적격·부적격·잔여 조건을 표시하고 명시 확인 뒤 동일 intent를 보낸다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 대상별 조건을 보여주고 확인한 요청만 제출한다의 유효 조건
When: 대상별 조건을 보여주고 확인한 요청만 제출한다를 실행한다
Then: 수락/거부/대기와 commit 상태를 구분하고 반복 클릭은 같은 key로 조회한다.

### AC-CS-PLAY.02.02-N · NEGATIVE · NOT_RUN
Given: 대상 일부 거부를 전체 성공으로 표시하거나 요청 수락을 업무 완료로 표시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 대상 일부 거부를 전체 성공으로 표시하거나 요청 수락을 업무 완료로 표시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [GAME-EMERGENCY](https://www.world-of-emergency.com/news/2023-09-15/234-the_new_emergency) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [GAME-RESCUEHQ](https://store.playstation.com/en-us/concept/10002295) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PLAY.03.01 · 실제 Game View 위에 기본 운영 HUD를 조립한다

**상위:** CS-PLAY.03 / CS-PLAY · **작업창:** W2 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** Canvas·TMP roster/inspector/dock을 구성하고 중앙 빈 HUD는 raycast를 받지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.03.json](basis/v3/tasks/CS-PLAY.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
실제 Game View 위에 기본 운영 HUD를 조립한다

## 입력·출력 인터페이스
UIState {selection,camera,openPanels,textScale}; 운영 상태와 수명을 분리.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Hud/OperationsHud.prefab` | CS-PLAY.03.01 |
| CREATE | `Assets/ChooGuard/Presentation/Hud/HudPresenter.cs` | CS-PLAY.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0301Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.01.01 | `OUT-CS-PLAY.01.01@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |
| CS-OPS.01.03 | `OUT-CS-OPS.01.03@integration` | integration | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@integration` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. Canvas·TMP roster/inspector/dock을 구성하고 중앙 빈 HUD는 raycast를 받지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 실제 Game View 위에 기본 운영 HUD를 조립한다의 유효 조건
When: 실제 Game View 위에 기본 운영 HUD를 조립한다를 실행한다
Then: 실제 3D 뷰와 접이식 패널에서 목록 기반 핵심 조작이 가능하다.

### AC-CS-PLAY.03.01-N · NEGATIVE · NOT_RUN
Given: 웹 화면 임베딩·단일 거대 캔버스 rebuild·배경 raycast 차단을 기본 구조로 넣지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 웹 화면 임베딩·단일 거대 캔버스 rebuild·배경 raycast 차단을 기본 구조로 넣지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.03.02 · RTS 카메라를 입력 컨텍스트와 연결한다

**상위:** CS-PLAY.03 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실제 WorldCamera의 pan/zoom/focus와 pixelRect 변환을 input owner 뒤에 수행한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.03.json](basis/v3/tasks/CS-PLAY.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
RTS 카메라를 입력 컨텍스트와 연결한다

## 입력·출력 인터페이스
UIState {selection,camera,openPanels,textScale}; 운영 상태와 수명을 분리.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Camera/OperationsCamera.cs` | CS-PLAY.03.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0302Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.03.01 | `OUT-CS-PLAY.03.01@candidate` | candidate | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 실제 WorldCamera의 pan/zoom/focus와 pixelRect 변환을 input owner 뒤에 수행한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 RTS 카메라를 입력 컨텍스트와 연결한다의 유효 조건
When: RTS 카메라를 입력 컨텍스트와 연결한다를 실행한다
Then: 선택 초점·회전·줌이 UI 편집 중 차단되고 복귀 시 카메라가 보존된다.

### AC-CS-PLAY.03.02-N · NEGATIVE · NOT_RUN
Given: scene 전환 뒤 이중 카메라 제어 또는 UI wheel 중복 zoom이 발생하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: scene 전환 뒤 이중 카메라 제어 또는 UI wheel 중복 zoom이 발생하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.03.03 · 실제 위치를 사용하는 층별 미니맵을 만든다

**상위:** CS-PLAY.03 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** camera texture 또는 동일 월드 데이터 평면 투영을 쓰고 클릭→frame 변환을 정의한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.03.json](basis/v3/tasks/CS-PLAY.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
실제 위치를 사용하는 층별 미니맵을 만든다

## 입력·출력 인터페이스
UIState {selection,camera,openPanels,textScale}; 운영 상태와 수명을 분리.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Hud/MinimapPresenter.cs` | CS-PLAY.03.03 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0303Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.03.02 | `OUT-CS-PLAY.03.02@candidate` | candidate | ALWAYS |
| CS-PLAY.01.03 | `OUT-CS-PLAY.01.03@candidate` | integration | ALWAYS |
| CS-WORLD.03.02 | `OUT-CS-WORLD.03.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. camera texture 또는 동일 월드 데이터 평면 투영을 쓰고 클릭→frame 변환을 정의한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.03.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 실제 위치를 사용하는 층별 미니맵을 만든다의 유효 조건
When: 실제 위치를 사용하는 층별 미니맵을 만든다를 실행한다
Then: DPI/pixelRect/층이 달라도 같은 위치로 초점을 옮긴다.

### AC-CS-PLAY.03.03-N · NEGATIVE · NOT_RUN
Given: 미니맵 그림을 현재 상태로 가장하거나 다른 층 좌표로 이동하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 미니맵 그림을 현재 상태로 가장하거나 다른 층 좌표로 이동하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0303Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.03.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.04.01 · 여러 대기 원인을 인스펙터와 기관 타임라인에 표시한다

**상위:** CS-PLAY.04 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** Reason 목록의 원인·영향·근거·해결조건을 펼쳐 읽고 메시지/업무와 연결한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.04.json](basis/v3/tasks/CS-PLAY.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
여러 대기 원인을 인스펙터와 기관 타임라인에 표시한다

## 입력·출력 인터페이스
ReasonView/AgencyTimeline는 read-only; pause는 코어 ack 이후 표시.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Inspector/ReasonInspector.cs` | CS-PLAY.04.01 |
| CREATE | `Assets/ChooGuard/Presentation/Timeline/AgencyTimeline.cs` | CS-PLAY.04.01 |
| CREATE | `Assets/ChooGuard/Presentation/Inspector/ReasonInspector.prefab` | CS-PLAY.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0401Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.03.01 | `OUT-CS-PLAY.03.01@candidate` | candidate | ALWAYS |
| CS-OPS.06.01 | `OUT-CS-OPS.06.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. Reason 목록의 원인·영향·근거·해결조건을 펼쳐 읽고 메시지/업무와 연결한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 여러 대기 원인을 인스펙터와 기관 타임라인에 표시한다의 유효 조건
When: 여러 대기 원인을 인스펙터와 기관 타임라인에 표시한다를 실행한다
Then: 한 업무의 두 원인을 모두 확인하고 해당 이벤트로 이동할 수 있다.

### AC-CS-PLAY.04.01-N · NEGATIVE · NOT_RUN
Given: 순환대기·외부대기·미확인을 단일 빨간 FAIL로 바꾸지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 순환대기·외부대기·미확인을 단일 빨간 FAIL로 바꾸지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0401Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.04.02 · 기관 보기와 오버레이의 복귀·정지 상태를 관리한다

**상위:** CS-PLAY.04 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 전체/기관 projection을 읽기 전용으로 바꾼다. 창 닫기와 pause ack는 다른 상태다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.04.json](basis/v3/tasks/CS-PLAY.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
기관 보기와 오버레이의 복귀·정지 상태를 관리한다

## 입력·출력 인터페이스
ReasonView/AgencyTimeline는 read-only; pause는 코어 ack 이후 표시.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Presentation/Timeline/AgencyTimeline.cs` | CS-PLAY.04.01 |
| CREATE | `Assets/ChooGuard/Presentation/Overlays/ToolOverlayRouter.cs` | CS-PLAY.04.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0402Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.04.01 | `OUT-CS-PLAY.04.01@candidate` | candidate | ALWAYS |
| CS-PLAY.02.02 | `OUT-CS-PLAY.02.02@candidate` | integration | ALWAYS |
| CS-OPS.06.02 | `OUT-CS-OPS.06.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 전체/기관 projection을 읽기 전용으로 바꾼다. 창 닫기와 pause ack는 다른 상태다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 기관 보기와 오버레이의 복귀·정지 상태를 관리한다의 유효 조건
When: 기관 보기와 오버레이의 복귀·정지 상태를 관리한다를 실행한다
Then: 비교/근거창 복귀 시 선택·스크롤·카메라가 유지된다.

### AC-CS-PLAY.04.02-N · NEGATIVE · NOT_RUN
Given: 창 닫기로 업무가 취소되거나 Time.timeScale만 0으로 다중 계산을 정지했다고 표시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 창 닫기로 업무가 취소되거나 Time.timeScale만 0으로 다중 계산을 정지했다고 표시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0402Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.05.01 · 글자 확대·키 재배정·고대비 설정을 구현한다

**상위:** CS-PLAY.05 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 100/150/200% 글자와 좁은 창에서 핵심 제어를 보존하고 패널 대체 조작을 제공한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.05.json](basis/v3/tasks/CS-PLAY.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
글자 확대·키 재배정·고대비 설정을 구현한다

## 입력·출력 인터페이스
Preferences는 물리·정보·업무 결과를 수정하지 않는다.

**직접 담당 요구:** REQ-072
**지원 요구:** REQ-072
**부모 제품 시험:** AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Accessibility/UiPreferences.cs` | CS-PLAY.05.01 |
| CREATE | `Assets/ChooGuard/Presentation/Accessibility/FocusManager.cs` | CS-PLAY.05.01 |
| CREATE | `Assets/ChooGuard/Presentation/Accessibility/Preferences.prefab` | CS-PLAY.05.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0501Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.03.01 | `OUT-CS-PLAY.03.01@candidate` | candidate | ALWAYS |
| CS-PLAY.01.02 | `OUT-CS-PLAY.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 100/150/200% 글자와 좁은 창에서 핵심 제어를 보존하고 패널 대체 조작을 제공한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.05.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 글자 확대·키 재배정·고대비 설정을 구현한다의 유효 조건
When: 글자 확대·키 재배정·고대비 설정을 구현한다를 실행한다
Then: 글자 확대·키보드 포커스로 배정/취소/근거 조회가 가능하다.

### AC-CS-PLAY.05.01-N · NEGATIVE · NOT_RUN
Given: 큰 글씨를 autoshrink하거나 설정 변화로 운영 결과를 바꾸지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 큰 글씨를 autoshrink하거나 설정 변화로 운영 결과를 바꾸지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0501Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.05.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.05.02 · 두 모드·최근 현장 런처와 반복 진입을 연결한다

**상위:** CS-PLAY.05 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 최근 SiteBundle·설정·접근 가능한 콘텐츠를 표시하고 두 사용자 모드만 노출한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.05.json](basis/v3/tasks/CS-PLAY.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
두 모드·최근 현장 런처와 반복 진입을 연결한다

## 입력·출력 인터페이스
Preferences는 물리·정보·업무 결과를 수정하지 않는다.

**직접 담당 요구:** REQ-072
**지원 요구:** REQ-072
**부모 제품 시험:** AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Presentation/Accessibility/FocusManager.cs` | CS-PLAY.05.01 |
| CREATE | `Assets/ChooGuard/Presentation/Menu/WorkspaceLauncher.cs` | CS-PLAY.05.02 |
| CREATE | `Assets/ChooGuard/Presentation/Menu/WorkspaceLauncher.prefab` | CS-PLAY.05.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0502Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.05.01 | `OUT-CS-PLAY.05.01@candidate` | candidate | ALWAYS |
| CS-PLAY.03.01 | `OUT-CS-PLAY.03.01@candidate` | integration | ALWAYS |
| CS-PLAY.04.02 | `OUT-CS-PLAY.04.02@candidate` | integration | ALWAYS |
| CS-PACK.01.02 | `OUT-CS-PACK.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 최근 SiteBundle·설정·접근 가능한 콘텐츠를 표시하고 두 사용자 모드만 노출한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.05.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 두 모드·최근 현장 런처와 반복 진입을 연결한다의 유효 조건
When: 두 모드·최근 현장 런처와 반복 진입을 연결한다를 실행한다
Then: 같은 현장을 열어 변경값만 편집하고 이전 작업으로 복귀한다.

### AC-CS-PLAY.05.02-N · NEGATIVE · NOT_RUN
Given: 연구 A/B/C 조건을 세 번째 제품 모드로 노출하거나 미지원 현장 자격을 감추지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 연구 A/B/C 조건을 세 번째 제품 모드로 노출하거나 미지원 현장 자격을 감추지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0502Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.05.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.06.01 · 복원 가능성을 검사하는 분기 대화상자를 만든다

**상위:** CS-PLAY.06 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** parent checkpoint·시각·누락 worker·kind를 표시하고 허용된 BranchRequest만 전송한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.06.json](basis/v3/tasks/CS-PLAY.06.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
복원 가능성을 검사하는 분기 대화상자를 만든다

## 입력·출력 인터페이스
CheckpointCapabilities→BranchRequest; ComparisonReport→read-only UI; 기록 replay와 새 branch·운영안 수정 구분.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Experiments/BranchDialog.cs` | CS-PLAY.06.01 |
| CREATE | `Assets/ChooGuard/Presentation/Experiments/BranchDialog.prefab` | CS-PLAY.06.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0601Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.04.02 | `OUT-CS-PLAY.04.02@candidate` | candidate | ALWAYS |
| CS-LAB.01.01 | `OUT-CS-LAB.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. parent checkpoint·시각·누락 worker·kind를 표시하고 허용된 BranchRequest만 전송한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.06.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 복원 가능성을 검사하는 분기 대화상자를 만든다의 유효 조건
When: 복원 가능성을 검사하는 분기 대화상자를 만든다를 실행한다
Then: 정확 복원 가능할 때만 EXACT_RESUME를 선택할 수 있다.

### AC-CS-PLAY.06.01-N · NEGATIVE · NOT_RUN
Given: 누락된 상태의 분기를 버튼 확인만으로 정확 분기로 승격시키지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 누락된 상태의 분기를 버튼 확인만으로 정확 분기로 승격시키지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0601Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.06.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.06.02 · 비교불가·불확도·선택 이유를 A/B 화면에 표시한다

**상위:** CS-PLAY.06 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 비교 basis와 공통 QoI·제약 위반·우열 불명을 표시하고 사용자 선택 이유를 저장한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.06.json](basis/v3/tasks/CS-PLAY.06.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
비교불가·불확도·선택 이유를 A/B 화면에 표시한다

## 입력·출력 인터페이스
CheckpointCapabilities→BranchRequest; ComparisonReport→read-only UI; 기록 replay와 새 branch·운영안 수정 구분.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-008, REQ-072
**부모 제품 시험:** AT-008, AT-072

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Experiments/ComparisonPresenter.cs` | CS-PLAY.06.02 |
| CREATE | `Assets/ChooGuard/Presentation/Experiments/ComparisonOverlay.prefab` | CS-PLAY.06.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0602Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.06.01 | `OUT-CS-PLAY.06.01@candidate` | candidate | ALWAYS |
| CS-PLAY.05.02 | `OUT-CS-PLAY.05.02@candidate` | integration | ALWAYS |
| CS-LAB.03.02 | `OUT-CS-LAB.03.02@candidate` | integration | ALWAYS |
| CS-LAB.03.01 | `OUT-CS-LAB.03.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 비교 basis와 공통 QoI·제약 위반·우열 불명을 표시하고 사용자 선택 이유를 저장한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.06.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 비교불가·불확도·선택 이유를 A/B 화면에 표시한다의 유효 조건
When: 비교불가·불확도·선택 이유를 A/B 화면에 표시한다를 실행한다
Then: 선택한 운영안·실행·근거가 대본 편집으로 전달된다.

### AC-CS-PLAY.06.02-N · NEGATIVE · NOT_RUN
Given: 없는 개선율을 UI가 생성하거나 과거 탐색 결과를 기관 지식에 쓰지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 없는 개선율을 UI가 생성하거나 과거 탐색 결과를 기관 지식에 쓰지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0602Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.06.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-PLAY.07.01 · 동일 코어의 표·타임라인 연구 비교군을 만든다

**상위:** CS-PLAY.07 / CS-PLAY · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 연구 빌드에서만 B/C를 구분하며 동일 OperationsPort·AI·도움·정보 정책으로 배정부터 출력까지 수행한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PLAY.07.json](basis/v3/tasks/CS-PLAY.07.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
동일 코어의 표·타임라인 연구 비교군을 만든다

## 입력·출력 인터페이스
StudyConfiguration {condition, coreLock, assistancePolicy, informationScope, caseId}; 연구 전용 condition이며 사용자 모드가 아님.

**직접 담당 요구:** REQ-081
**지원 요구:** REQ-081
**부모 제품 시험:** AT-081

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Research/TabularStudyPresenter.cs` | CS-PLAY.07.01 |
| CREATE | `Assets/ChooGuard/Presentation/Research/TabularStudy.prefab` | CS-PLAY.07.01 |
| CREATE | `Assets/ChooGuard/Contracts/StudyConfiguration.cs` | CS-PLAY.07.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0701Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PLAY.05.02 | `OUT-CS-PLAY.05.02@candidate` | integration | ALWAYS |
| CS-OPS.07.02 | `OUT-CS-OPS.07.02@candidate` | integration | ALWAYS |
| CS-PLAY.05.01 | `OUT-CS-PLAY.05.01@candidate` | candidate | ALWAYS |
| CS-PLAY.06.02 | `OUT-CS-PLAY.06.02@candidate` | candidate | ALWAYS |
| CS-OPS.07.02 | `OUT-CS-OPS.07.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 연구 빌드에서만 B/C를 구분하며 동일 OperationsPort·AI·도움·정보 정책으로 배정부터 출력까지 수행한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PLAY.07.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 동일 코어의 표·타임라인 연구 비교군을 만든다의 유효 조건
When: 동일 코어의 표·타임라인 연구 비교군을 만든다를 실행한다
Then: 3D만 다른 비교 과제가 동일 정보와 같은 조작 결과를 만든다.

### AC-CS-PLAY.07.01-N · NEGATIVE · NOT_RUN
Given: B에서 비교/취소를 제거하거나 제품 메뉴에 세 번째 모드를 추가하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: B에서 비교/취소를 제거하거나 제품 메뉴에 세 번째 모드를 추가하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPLAY0701Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PLAY.07.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/06-native-surfaces.md](basis/v3/specs/06-native-surfaces.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)


---

# CS-LAB.01.01 · 공통 cut의 코어·메시지·예약·난수 checkpoint를 저장한다

**상위:** CS-LAB.01 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** in-flight commit과 publication barrier를 정리한 cut에서 목록과 파일해시를 수집하고 manifest-last로 게시한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.01.json](basis/v3/tasks/CS-LAB.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
공통 cut의 코어·메시지·예약·난수 checkpoint를 저장한다

## 입력·출력 인터페이스
Checkpoint: schemas/Checkpoint.schema.json. cutSequence/tickUs/coreStateRef/eventQueueRef/randomStreamsRef/contentLockRef와 필수 workerStates를 검증한다.

**직접 담당 요구:** REQ-036
**지원 요구:** REQ-036
**부모 제품 시험:** AT-036

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Experiments/CheckpointService.cs` | CS-LAB.01.01 |
| CREATE | `Assets/ChooGuard/Experiments/CheckpointManifest.cs` | CS-LAB.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0101Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.02.04 | `OUT-CS-OPS.02.04@candidate` | candidate | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. in-flight commit과 publication barrier를 정리한 cut에서 목록과 파일해시를 수집하고 manifest-last로 게시한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 공통 cut의 코어·메시지·예약·난수 checkpoint를 저장한다의 유효 조건
When: 공통 cut의 코어·메시지·예약·난수 checkpoint를 저장한다를 실행한다
Then: 같은 cut의 상태·queue·RNG·content lock이 하나의 manifest로 복원된다.

### AC-CS-LAB.01.01-N · NEGATIVE · NOT_RUN
Given: 누락/다른 revision·혼합 tick·커밋 중 복사면 exact capability를 주지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 누락/다른 revision·혼합 tick·커밋 중 복사면 exact capability를 주지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-LAB.01.02 · 이벤트 재생과 worker 복원능력 검사를 연결한다

**상위:** CS-LAB.01 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** recorded apply와 새 simulation 실행을 분리하고 required worker 목록을 run config에서 얻는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.01.json](basis/v3/tasks/CS-LAB.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
이벤트 재생과 worker 복원능력 검사를 연결한다

## 입력·출력 인터페이스
Checkpoint: schemas/Checkpoint.schema.json. cutSequence/tickUs/coreStateRef/eventQueueRef/randomStreamsRef/contentLockRef와 필수 workerStates를 검증한다.

**직접 담당 요구:** REQ-036
**지원 요구:** REQ-036
**부모 제품 시험:** AT-036

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Experiments/CheckpointService.cs` | CS-LAB.01.01 |
| CREATE | `Assets/ChooGuard/Experiments/ReplayReader.cs` | CS-LAB.01.02 |
| MODIFY | `Assets/ChooGuard/Experiments/CheckpointManifest.cs` | CS-LAB.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0102Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-LAB.01.01 | `OUT-CS-LAB.01.01@candidate` | candidate | ALWAYS |
| CS-OPS.02.02 | `OUT-CS-OPS.02.02@candidate` | integration | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | integration | ALWAYS |
| CS-SIM.01.02 | `OUT-CS-SIM.01.02@candidate` | integration | EXTERNAL_WORKER_USED |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. recorded apply와 새 simulation 실행을 분리하고 required worker 목록을 run config에서 얻는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 이벤트 재생과 worker 복원능력 검사를 연결한다의 유효 조건
When: 이벤트 재생과 worker 복원능력 검사를 연결한다를 실행한다
Then: 모든 필요 상태가 있을 때만 정확 재개되고 replay는 read-only다.

### AC-CS-LAB.01.02-N · NEGATIVE · NOT_RUN
Given: worker 상태 파일명만 있고 내용/hash가 틀리면 복원을 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: worker 상태 파일명만 있고 내용/hash가 틀리면 복원을 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-LAB.02.01 · 불변 parent에서 새 run으로 분기한다

**상위:** CS-LAB.02 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 새 ID·generation·parent checkpoint·변경 plan을 원자 기록한다. 원본 이벤트를 수정하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.02.json](basis/v3/tasks/CS-LAB.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
불변 parent에서 새 run으로 분기한다

## 입력·출력 인터페이스
BranchReceipt {newRunId,parentRunId,checkpointHash,kind,changedPlan}; 리플레이는 새 실험이 아니다.

**직접 담당 요구:** REQ-037, REQ-057
**지원 요구:** REQ-037, REQ-057
**부모 제품 시험:** AT-037, AT-057

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Experiments/BranchService.cs` | CS-LAB.02.01 |
| CREATE | `Assets/ChooGuard/Experiments/BranchLineage.cs` | CS-LAB.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0201Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-LAB.01.01 | `OUT-CS-LAB.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 새 ID·generation·parent checkpoint·변경 plan을 원자 기록한다. 원본 이벤트를 수정하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 불변 parent에서 새 run으로 분기한다의 유효 조건
When: 불변 parent에서 새 run으로 분기한다를 실행한다
Then: B의 변경 뒤에도 A의 이벤트 digest와 예약 기록이 동일하다.

### AC-CS-LAB.02.01-N · NEGATIVE · NOT_RUN
Given: 중복 branch request나 child의 write로 parent 로그가 달라지지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 중복 branch request나 child의 write로 parent 로그가 달라지지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-LAB.02.02 · 정확 재개와 처음부터 재계산을 구분한다

**상위:** CS-LAB.02 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** adapter capabilities에 따라 EXACT_RESUME/RECOMPUTE_FROM_START를 선택하고 누락을 사용자에게 반환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.02.json](basis/v3/tasks/CS-LAB.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
정확 재개와 처음부터 재계산을 구분한다

## 입력·출력 인터페이스
BranchReceipt {newRunId,parentRunId,checkpointHash,kind,changedPlan}; 리플레이는 새 실험이 아니다.

**직접 담당 요구:** REQ-037, REQ-057
**지원 요구:** REQ-037, REQ-057
**부모 제품 시험:** AT-037, AT-057

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Experiments/BranchService.cs` | CS-LAB.02.01 |
| MODIFY | `Assets/ChooGuard/Experiments/BranchLineage.cs` | CS-LAB.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0202Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-LAB.02.01 | `OUT-CS-LAB.02.01@candidate` | candidate | ALWAYS |
| CS-LAB.01.02 | `OUT-CS-LAB.01.02@candidate` | integration | ALWAYS |
| CS-LAB.01.02 | `OUT-CS-LAB.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. adapter capabilities에 따라 EXACT_RESUME/RECOMPUTE_FROM_START를 선택하고 누락을 사용자에게 반환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 정확 재개와 처음부터 재계산을 구분한다의 유효 조건
When: 정확 재개와 처음부터 재계산을 구분한다를 실행한다
Then: 선택한 방식의 입력·변경 이력·결과 자격이 남는다.

### AC-CS-LAB.02.02-N · NEGATIVE · NOT_RUN
Given: restore 불가 solver를 seed만 복사해 정확 분기로 부르지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: restore 불가 solver를 seed만 복사해 정확 분기로 부르지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-LAB.03.01 · 같은 비교 basis와 외생 혁신을 결속한다

**상위:** CS-LAB.03 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 맵/규칙/모델/QoI·외생 조건을 비교하고 stable process key로 RNG 스트림을 묶는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.03.json](basis/v3/tasks/CS-LAB.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
같은 비교 basis와 외생 혁신을 결속한다

## 입력·출력 인터페이스
Comparison {basis,inputs,qoi,violations,uncertainty,status,selectionReason}; 현실 전체 최적을 주장하지 않음.

**직접 담당 요구:** REQ-038, REQ-039
**지원 요구:** REQ-038, REQ-039
**부모 제품 시험:** AT-038, AT-039

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Experiments/ComparisonService.cs` | CS-LAB.03.01 |
| CREATE | `Assets/ChooGuard/Experiments/ExogenousScenarioStreams.cs` | CS-LAB.03.01 |
| CREATE | `Assets/ChooGuard/Experiments/ComparisonReport.cs` | CS-LAB.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0301Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-LAB.02.01 | `OUT-CS-LAB.02.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 맵/규칙/모델/QoI·외생 조건을 비교하고 stable process key로 RNG 스트림을 묶는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 같은 비교 basis와 외생 혁신을 결속한다의 유효 조건
When: 같은 비교 basis와 외생 혁신을 결속한다를 실행한다
Then: 같은 외생 조건에 다른 운영안을 실행하되 내생 결과는 별도로 계산된다.

### AC-CS-LAB.03.01-N · NEGATIVE · NOT_RUN
Given: 맵/규칙 불일치를 숨기거나 조치가 바꾼 도착을 고정 외생 사건으로 강제하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 맵/규칙 불일치를 숨기거나 조치가 바꾼 도착을 고정 외생 사건으로 강제하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-LAB.03.02 · 제약·불확도·비지배 결과와 우열 불명을 표시한다

**상위:** CS-LAB.03 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실패/미완료를 분모에 포함하고 허용 제약을 충족한 안에서 다목적 비교한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.03.json](basis/v3/tasks/CS-LAB.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
제약·불확도·비지배 결과와 우열 불명을 표시한다

## 입력·출력 인터페이스
Comparison {basis,inputs,qoi,violations,uncertainty,status,selectionReason}; 현실 전체 최적을 주장하지 않음.

**직접 담당 요구:** REQ-038, REQ-039
**지원 요구:** REQ-038, REQ-039
**부모 제품 시험:** AT-038, AT-039

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Experiments/ComparisonService.cs` | CS-LAB.03.01 |
| MODIFY | `Assets/ChooGuard/Experiments/ComparisonReport.cs` | CS-LAB.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0302Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-LAB.03.01 | `OUT-CS-LAB.03.01@candidate` | candidate | ALWAYS |
| CS-LAB.02.02 | `OUT-CS-LAB.02.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 실패/미완료를 분모에 포함하고 허용 제약을 충족한 안에서 다목적 비교한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 제약·불확도·비지배 결과와 우열 불명을 표시한다의 유효 조건
When: 제약·불확도·비지배 결과와 우열 불명을 표시한다를 실행한다
Then: 순위 역전·오차 내 차이는 우열 불명이며 사용자 이유가 기록된다.

### AC-CS-LAB.03.02-N · NEGATIVE · NOT_RUN
Given: 빠른 시간으로 권한 위반을 상쇄하거나 일부 좋은 실행만 선택하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 빠른 시간으로 권한 위반을 상쇄하거나 일부 좋은 실행만 선택하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-LAB.04.01 · 의미 변경의 영향과 자격 만료를 계산한다

**상위:** CS-LAB.04 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 표현/계획/경계/관측 변경을 나누고 source dependency를 따라 dirty 집합을 만든다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.04.json](basis/v3/tasks/CS-LAB.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
의미 변경의 영향과 자격 만료를 계산한다

## 입력·출력 인터페이스
Impact {changedInputs,dirtyOutputs,evidence,allowedReuse,requiredRecompute}; 관계 부재는 영향 없음이 아님.

**직접 담당 요구:** REQ-084
**지원 요구:** REQ-084
**부모 제품 시험:** AT-084

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Experiments/ImpactGraph.cs` | CS-LAB.04.01 |
| CREATE | `Assets/ChooGuard/Experiments/RunInvalidation.cs` | CS-LAB.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0401Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-LAB.03.01 | `OUT-CS-LAB.03.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 표현/계획/경계/관측 변경을 나누고 source dependency를 따라 dirty 집합을 만든다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 의미 변경의 영향과 자격 만료를 계산한다의 유효 조건
When: 의미 변경의 영향과 자격 만료를 계산한다를 실행한다
Then: 변경된 근거에 연결된 결과만 STALE로 표시되고 원본은 남는다.

### AC-CS-LAB.04.01-N · NEGATIVE · NOT_RUN
Given: 관계가 등록되지 않았다는 이유로 영향 없음으로 단정하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 관계가 등록되지 않았다는 이유로 영향 없음으로 단정하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0401Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-LAB.04.02 · 안전한 캐시와 전체 재실행 대조를 구현한다

**상위:** CS-LAB.04 / CS-LAB · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 버전·입력해시·유효영역이 같은 파생값만 재사용한다. 파급 범위가 불명확하면 전체 재계산한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-LAB.04.json](basis/v3/tasks/CS-LAB.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
안전한 캐시와 전체 재실행 대조를 구현한다

## 입력·출력 인터페이스
Impact {changedInputs,dirtyOutputs,evidence,allowedReuse,requiredRecompute}; 관계 부재는 영향 없음이 아님.

**직접 담당 요구:** REQ-084
**지원 요구:** REQ-084
**부모 제품 시험:** AT-084

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Experiments/ReplayCache.cs` | CS-LAB.04.02 |
| MODIFY | `Assets/ChooGuard/Experiments/RunInvalidation.cs` | CS-LAB.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0402Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-LAB.04.01 | `OUT-CS-LAB.04.01@candidate` | candidate | ALWAYS |
| CS-LAB.03.02 | `OUT-CS-LAB.03.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 버전·입력해시·유효영역이 같은 파생값만 재사용한다. 파급 범위가 불명확하면 전체 재계산한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-LAB.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 안전한 캐시와 전체 재실행 대조를 구현한다의 유효 조건
When: 안전한 캐시와 전체 재실행 대조를 구현한다를 실행한다
Then: 부분 재실행의 의미 상태/QoI가 전체 결과와 사전 허용차 내에서 같다.

### AC-CS-LAB.04.02-N · NEGATIVE · NOT_RUN
Given: stale field 또는 다른 조건의 과거 결과를 최신 계산으로 표시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: stale field 또는 다른 조건의 과거 결과를 최신 계산으로 표시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSLAB0402Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-LAB.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/03-durable-operations.md](basis/v3/specs/03-durable-operations.md)


---

# CS-MODES.01.01 · 매뉴얼 근거가 있는 튜토리얼 목표를 구성한다

**상위:** CS-MODES.01 / CS-MODES · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 부분순서 목표와 정보/인계 조건을 실제 규칙 locator에 연결한다. 공개 사례 재구성과 정확 재현을 구분한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-MODES.01.json](basis/v3/tasks/CS-MODES.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
매뉴얼 근거가 있는 튜토리얼 목표를 구성한다

## 입력·출력 인터페이스
TutorialCourse {source,scope,goals,partialOrder,hints,assessment}; 공식 이수·훈련 정확재현은 별도 검수.

**직접 담당 요구:** REQ-003, REQ-004, REQ-065
**지원 요구:** REQ-003, REQ-004, REQ-065
**부모 제품 시험:** AT-003, AT-004, AT-065

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Scenarios/TutorialDirector.cs` | CS-MODES.01.01 |
| CREATE | `content/exercises/tutorial/course.json` | CS-MODES.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0101Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | candidate | ALWAYS |
| CS-OPS.05.01 | `OUT-CS-OPS.05.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 부분순서 목표와 정보/인계 조건을 실제 규칙 locator에 연결한다. 공개 사례 재구성과 정확 재현을 구분한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-MODES.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 매뉴얼 근거가 있는 튜토리얼 목표를 구성한다의 유효 조건
When: 매뉴얼 근거가 있는 튜토리얼 목표를 구성한다를 실행한다
Then: 서로 다른 유효 업무 순서가 같은 목표를 충족할 수 있다.

### AC-CS-MODES.01.01-N · NEGATIVE · NOT_RUN
Given: 미확인 현장 대본을 공식 재현이나 법적 수료로 표시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 미확인 현장 대본을 공식 재현이나 법적 수료로 표시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-MODES.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/08-modes-and-scenario-generation.md](basis/v3/specs/08-modes-and-scenario-generation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [CASE-READY2026](https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=26899) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [CASE-YULHYEON](https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=26342) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-MODES.01.02 · 안내·힌트 축소·복기를 같은 코어에 연결한다

**상위:** CS-MODES.01 / CS-MODES · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 교육 진도는 presentation state로 저장하고 실제 운영 상태를 변경하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-MODES.01.json](basis/v3/tasks/CS-MODES.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
안내·힌트 축소·복기를 같은 코어에 연결한다

## 입력·출력 인터페이스
TutorialCourse {source,scope,goals,partialOrder,hints,assessment}; 공식 이수·훈련 정확재현은 별도 검수.

**직접 담당 요구:** REQ-003, REQ-004, REQ-065
**지원 요구:** REQ-003, REQ-004, REQ-065
**부모 제품 시험:** AT-003, AT-004, AT-065

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Scenarios/TutorialDirector.cs` | CS-MODES.01.01 |
| CREATE | `Assets/ChooGuard/Presentation/Tutorial/TutorialOverlay.prefab` | CS-MODES.01.02 |
| CREATE | `Assets/ChooGuard/Presentation/Tutorial/TutorialPresenter.cs` | CS-MODES.01.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0102Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-MODES.01.01 | `OUT-CS-MODES.01.01@candidate` | candidate | ALWAYS |
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | integration | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | integration | ALWAYS |
| CS-PLAY.03.01 | `OUT-CS-PLAY.03.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 교육 진도는 presentation state로 저장하고 실제 운영 상태를 변경하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-MODES.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 안내·힌트 축소·복기를 같은 코어에 연결한다의 유효 조건
When: 안내·힌트 축소·복기를 같은 코어에 연결한다를 실행한다
Then: 튜토리얼과 일반 모드의 같은 입력은 같은 운영·물리 결과를 낸다.

### AC-CS-MODES.01.02-N · NEGATIVE · NOT_RUN
Given: 힌트로 속도·자원을 바꾸거나 유효 대안을 오답으로 취급하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 힌트로 속도·자원을 바꾸거나 유효 대안을 오답으로 취급하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-MODES.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/08-modes-and-scenario-generation.md](basis/v3/specs/08-modes-and-scenario-generation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [CASE-READY2026](https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=26899) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [CASE-YULHYEON](https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=26342) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-MODES.02.01 · 제약과 인과를 지키는 랜덤 사건 그래프를 생성한다

**상위:** CS-MODES.02 / CS-MODES · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 기관/공간/능력/정보·사건 의존으로 생성하고 RNG seed·조건을 보존한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-MODES.02.json](basis/v3/tasks/CS-MODES.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
제약과 인과를 지키는 랜덤 사건 그래프를 생성한다

## 입력·출력 인터페이스
GeneratedScenario {spec,seedManifest,provenance,constraintReport,qualification}; 무작위 발생확률은 현실 빈도와 별개.

**직접 담당 요구:** REQ-005, REQ-006, REQ-007
**지원 요구:** REQ-005, REQ-006, REQ-007
**부모 제품 시험:** AT-005, AT-006, AT-007

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Scenarios/ScenarioGenerator.cs` | CS-MODES.02.01 |
| CREATE | `Assets/ChooGuard/Scenarios/ScenarioConstraintChecker.cs` | CS-MODES.02.01 |
| CREATE | `content/exercises/random/grammar.json` | CS-MODES.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0201Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | candidate | ALWAYS |
| CS-OPS.06.01 | `OUT-CS-OPS.06.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 기관/공간/능력/정보·사건 의존으로 생성하고 RNG seed·조건을 보존한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-MODES.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 제약과 인과를 지키는 랜덤 사건 그래프를 생성한다의 유효 조건
When: 제약과 인과를 지키는 랜덤 사건 그래프를 생성한다를 실행한다
Then: 같은 설정이 재현되며 내생/외생 사건 종류가 구분된다.

### AC-CS-MODES.02.01-N · NEGATIVE · NOT_RUN
Given: 알 수 없는 조건·없는 시설·권한 위반을 정상 랜덤 난이도로 허용하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 알 수 없는 조건·없는 시설·권한 위반을 정상 랜덤 난이도로 허용하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-MODES.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/08-modes-and-scenario-generation.md](basis/v3/specs/08-modes-and-scenario-generation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)


---

# CS-MODES.02.02 · 거부 이력·지원 필요·미지원 영역을 설명한다

**상위:** CS-MODES.02 / CS-MODES · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** attempt 한도를 두고 FEASIBLE/ESCALATION_REQUIRED/CONFLICTED/UNSUPPORTED를 반환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-MODES.02.json](basis/v3/tasks/CS-MODES.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
거부 이력·지원 필요·미지원 영역을 설명한다

## 입력·출력 인터페이스
GeneratedScenario {spec,seedManifest,provenance,constraintReport,qualification}; 무작위 발생확률은 현실 빈도와 별개.

**직접 담당 요구:** REQ-005, REQ-006, REQ-007
**지원 요구:** REQ-005, REQ-006, REQ-007
**부모 제품 시험:** AT-005, AT-006, AT-007

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Scenarios/ScenarioGenerator.cs` | CS-MODES.02.01 |
| MODIFY | `Assets/ChooGuard/Scenarios/ScenarioConstraintChecker.cs` | CS-MODES.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0202Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-MODES.02.01 | `OUT-CS-MODES.02.01@candidate` | candidate | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |
| CS-OPS.06.02 | `OUT-CS-OPS.06.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. attempt 한도를 두고 FEASIBLE/ESCALATION_REQUIRED/CONFLICTED/UNSUPPORTED를 반환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-MODES.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 거부 이력·지원 필요·미지원 영역을 설명한다의 유효 조건
When: 거부 이력·지원 필요·미지원 영역을 설명한다를 실행한다
Then: 어려운 정당한 사례와 생성 결함이 다른 사유로 기록된다.

### AC-CS-MODES.02.02-N · NEGATIVE · NOT_RUN
Given: 항상 쉬운 조합만 남긴 편향이나 지원불가를 사용자 실패로 숨기지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 항상 쉬운 조합만 남긴 편향이나 지원불가를 사용자 실패로 숨기지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-MODES.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/08-modes-and-scenario-generation.md](basis/v3/specs/08-modes-and-scenario-generation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)


---

# CS-MODES.03.01 · 검수된 현장과 두 모드의 세션을 재사용한다

**상위:** CS-MODES.03 / CS-MODES · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** SiteBundle와 모드 정책을 같은 core factory에 전달한다. 변경한 조건만 새 revision으로 저장한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-MODES.03.json](basis/v3/tasks/CS-MODES.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
검수된 현장과 두 모드의 세션을 재사용한다

## 입력·출력 인터페이스
Mode={TUTORIAL,RANDOM_OPERATIONS_LAB}; RuntimeConfig는 같은 코어와 lock을 공유.

**직접 담당 요구:** REQ-002, REQ-083, REQ-085
**지원 요구:** REQ-002, REQ-083, REQ-085
**부모 제품 시험:** AT-002, AT-083, AT-085

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Scenarios/WorkspaceSessionFactory.cs` | CS-MODES.03.01 |
| CREATE | `Assets/ChooGuard/Scenarios/ModePolicy.cs` | CS-MODES.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0301Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-MODES.01.02 | `OUT-CS-MODES.01.02@candidate` | candidate | ALWAYS |
| CS-MODES.02.02 | `OUT-CS-MODES.02.02@candidate` | candidate | ALWAYS |
| CS-LAB.02.01 | `OUT-CS-LAB.02.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. SiteBundle와 모드 정책을 같은 core factory에 전달한다. 변경한 조건만 새 revision으로 저장한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-MODES.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 검수된 현장과 두 모드의 세션을 재사용한다의 유효 조건
When: 검수된 현장과 두 모드의 세션을 재사용한다를 실행한다
Then: 두 모드의 동일 입력과 core lock이 같은 운영 결과를 낸다.

### AC-CS-MODES.03.01-N · NEGATIVE · NOT_RUN
Given: 유료화면·연구조건이 세 번째 모드로 추가되거나 이전 상태가 새 run으로 섞이지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 유료화면·연구조건이 세 번째 모드로 추가되거나 이전 상태가 새 run으로 섞이지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-MODES.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/08-modes-and-scenario-generation.md](basis/v3/specs/08-modes-and-scenario-generation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)


---

# CS-MODES.03.02 · 계산을 보존하며 다음 판단 지점까지 진행한다

**상위:** CS-MODES.03 / CS-MODES · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** scheduler의 다음 decision boundary까지 실제 사건·worker를 진행한다. 미계산 미래 결과는 표시하지 않는다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-MODES.03.json](basis/v3/tasks/CS-MODES.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
계산을 보존하며 다음 판단 지점까지 진행한다

## 입력·출력 인터페이스
Mode={TUTORIAL,RANDOM_OPERATIONS_LAB}; RuntimeConfig는 같은 코어와 lock을 공유.

**직접 담당 요구:** REQ-002, REQ-083, REQ-085
**지원 요구:** REQ-002, REQ-083, REQ-085
**부모 제품 시험:** AT-002, AT-083, AT-085

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Scenarios/DecisionBoundaryRunner.cs` | CS-MODES.03.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0302Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-MODES.03.01 | `OUT-CS-MODES.03.01@candidate` | candidate | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. scheduler의 다음 decision boundary까지 실제 사건·worker를 진행한다. 미계산 미래 결과는 표시하지 않는다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-MODES.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 계산을 보존하며 다음 판단 지점까지 진행한다의 유효 조건
When: 계산을 보존하며 다음 판단 지점까지 진행한다를 실행한다
Then: 일반 실행과 건너뛰기의 event order·최종 상태가 같다.

### AC-CS-MODES.03.02-N · NEGATIVE · NOT_RUN
Given: 배속 요청이 물리 dt를 바꾸거나 메시지를 누락하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 배속 요청이 물리 dt를 바꾸거나 메시지를 누락하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-MODES.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/08-modes-and-scenario-generation.md](basis/v3/specs/08-modes-and-scenario-generation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)


---

# CS-MODES.03.03 · 현장 선택·상황 설정을 native 프리팹으로 연결한다

**상위:** CS-MODES.03 / CS-MODES · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 현장 자격·두 모드·초기조건을 검토한 후 새 세션을 요청한다. 누락자료는 해당 주장 범위만 차단한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-MODES.03.json](basis/v3/tasks/CS-MODES.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
현장 선택·상황 설정을 native 프리팹으로 연결한다

## 입력·출력 인터페이스
Mode={TUTORIAL,RANDOM_OPERATIONS_LAB}; RuntimeConfig는 같은 코어와 lock을 공유.

**직접 담당 요구:** REQ-002, REQ-083, REQ-085
**지원 요구:** REQ-002, REQ-083, REQ-085
**부모 제품 시험:** AT-002, AT-083, AT-085

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Scenarios/ScenarioSetupPresenter.cs` | CS-MODES.03.03 |
| CREATE | `Assets/ChooGuard/Presentation/Scenarios/SiteSelectionPresenter.cs` | CS-MODES.03.03 |
| CREATE | `Assets/ChooGuard/Presentation/Scenarios/ScenarioSetup.prefab` | CS-MODES.03.03 |
| CREATE | `Assets/ChooGuard/Presentation/Scenarios/SiteSelection.prefab` | CS-MODES.03.03 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0303Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-MODES.03.02 | `OUT-CS-MODES.03.02@candidate` | candidate | ALWAYS |
| CS-MODES.01.02 | `OUT-CS-MODES.01.02@candidate` | integration | ALWAYS |
| CS-MODES.02.02 | `OUT-CS-MODES.02.02@candidate` | integration | ALWAYS |
| CS-LAB.02.02 | `OUT-CS-LAB.02.02@candidate` | integration | ALWAYS |
| CS-PLAY.05.02 | `OUT-CS-PLAY.05.02@candidate` | integration | ALWAYS |
| CS-SIM.01.02 | `OUT-CS-SIM.01.02@candidate` | integration | ALWAYS |
| CS-PLAY.05.02 | `OUT-CS-PLAY.05.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 현장 자격·두 모드·초기조건을 검토한 후 새 세션을 요청한다. 누락자료는 해당 주장 범위만 차단한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-MODES.03.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 현장 선택·상황 설정을 native 프리팹으로 연결한다의 유효 조건
When: 현장 선택·상황 설정을 native 프리팹으로 연결한다를 실행한다
Then: 선택→설정→작업공간이 올바른 run/현장 revision으로 이어진다.

### AC-CS-MODES.03.03-N · NEGATIVE · NOT_RUN
Given: 닫기/재열기로 draft 손실·이중세션·정량자격 자동승격이 생기지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 닫기/재열기로 draft 손실·이중세션·정량자격 자동승격이 생기지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSMODES0303Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-MODES.03.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/08-modes-and-scenario-generation.md](basis/v3/specs/08-modes-and-scenario-generation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)


---

# CS-SIM.01.01 · capability handshake와 제한된 JSONL transport를 만든다

**상위:** CS-SIM.01 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** allowlisted child process와 UTF-8 JSONL에 줄크기·깊이·stdout/stderr 한도를 적용한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.01.json](basis/v3/tasks/CS-SIM.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
capability handshake와 제한된 JSONL transport를 만든다

## 입력·출력 인터페이스
WorkerCapability, SimulationJob, FieldBatch; 임의 shell 문자열을 입력으로 실행하지 않는다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-049
**부모 제품 시험:** AT-049

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Contracts/WorkerTypes.cs` | CS-SIM.01.01 |
| CREATE | `Assets/ChooGuard/Simulation/WorkerProcessClient.cs` | CS-SIM.01.01 |
| CREATE | `workers/protocol/worker.schema.json` | CS-SIM.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0101Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | candidate | ALWAYS |
| CS-PACK.01.02 | `OUT-CS-PACK.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. allowlisted child process와 UTF-8 JSONL에 줄크기·깊이·stdout/stderr 한도를 적용한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 capability handshake와 제한된 JSONL transport를 만든다의 유효 조건
When: capability handshake와 제한된 JSONL transport를 만든다를 실행한다
Then: 시험 worker와 capability/schema/version을 확인할 수 있다.

### AC-CS-SIM.01.01-N · NEGATIVE · NOT_RUN
Given: oversize·malformed·unknown executable 입력은 실행/파싱 전에 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: oversize·malformed·unknown executable 입력은 실행/파싱 전에 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-FMI](https://fmi-standard.org/docs/3.0.2/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.01.02 · job·generation·출력 수량의 상관관계를 검사한다

**상위:** CS-SIM.01 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** request/run/job/attempt/generation/inputDigest와 field owner/unit/frame을 요청 결과에서 대조한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.01.json](basis/v3/tasks/CS-SIM.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
job·generation·출력 수량의 상관관계를 검사한다

## 입력·출력 인터페이스
WorkerCapability, SimulationJob, FieldBatch; 임의 shell 문자열을 입력으로 실행하지 않는다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-049
**부모 제품 시험:** AT-049

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Contracts/WorkerTypes.cs` | CS-SIM.01.01 |
| MODIFY | `Assets/ChooGuard/Simulation/WorkerProcessClient.cs` | CS-SIM.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0102Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.01.01 | `OUT-CS-SIM.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. request/run/job/attempt/generation/inputDigest와 field owner/unit/frame을 요청 결과에서 대조한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 job·generation·출력 수량의 상관관계를 검사한다의 유효 조건
When: job·generation·출력 수량의 상관관계를 검사한다를 실행한다
Then: 유효 결과만 수용하고 duplicate 결과는 멱등 처리된다.

### AC-CS-SIM.01.02-N · NEGATIVE · NOT_RUN
Given: 취소 전 generation·다른 단위·다른 좌표계 응답은 격리된다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 취소 전 generation·다른 단위·다른 좌표계 응답은 격리된다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-FMI](https://fmi-standard.org/docs/3.0.2/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.01.03 · 시험 worker 종료·timeout·재시작을 검증한다

**상위:** CS-SIM.01 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** core와 독립 프로세스의 hang/crash/corrupt를 주입하고 적법한 복구 지점을 반환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.01.json](basis/v3/tasks/CS-SIM.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
시험 worker 종료·timeout·재시작을 검증한다

## 입력·출력 인터페이스
WorkerCapability, SimulationJob, FieldBatch; 임의 shell 문자열을 입력으로 실행하지 않는다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-049
**부모 제품 시험:** AT-049

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Simulation/WorkerProcessClient.cs` | CS-SIM.01.01 |
| CREATE | `workers/fixture/worker.py` | CS-SIM.01.03 |
| MODIFY | `workers/protocol/worker.schema.json` | CS-SIM.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0103Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.01.02 | `OUT-CS-SIM.01.02@candidate` | candidate | ALWAYS |
| CS-BOOT.02.02 | `OUT-CS-BOOT.02.02@candidate` | integration | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. core와 독립 프로세스의 hang/crash/corrupt를 주입하고 적법한 복구 지점을 반환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.01.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 시험 worker 종료·timeout·재시작을 검증한다의 유효 조건
When: 시험 worker 종료·timeout·재시작을 검증한다를 실행한다
Then: worker 장애가 거짓 새 경계를 게시하지 않고 다른 독립 기능은 유지된다.

### AC-CS-SIM.01.03-N · NEGATIVE · NOT_RUN
Given: stdout 로그 혼입이나 죽은 process의 이전 결과로 current state를 덮지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: stdout 로그 혼입이나 죽은 process의 이전 결과로 current state를 덮지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0103Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.01.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-FMI](https://fmi-standard.org/docs/3.0.2/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.02.01 · 보행 어댑터의 단일 위치 소유권을 구현한다

**상위:** CS-SIM.02 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 고정 geometry·모델 revision으로 외부 보행 결과를 변환하고 Unity는 표시만 하게 한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.02.json](basis/v3/tasks/CS-SIM.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
보행 어댑터의 단일 위치 소유권을 구현한다

## 입력·출력 인터페이스
PedestrianState {entityId,frameId,position,velocity,tick,modelRef}; 독립 관측 전에는 현장 정확도 미판정.

**직접 담당 요구:** REQ-019, REQ-043
**지원 요구:** REQ-019, REQ-043
**부모 제품 시험:** AT-019, AT-043

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Simulation/PedestrianAdapter.cs` | CS-SIM.02.01 |
| CREATE | `workers/pedestrian/runner.py` | CS-SIM.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0201Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.01.03 | `OUT-CS-SIM.01.03@candidate` | candidate | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.02-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.

## 구현 순서
```text
1. 고정 geometry·모델 revision으로 외부 보행 결과를 변환하고 Unity는 표시만 하게 한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 보행 어댑터의 단일 위치 소유권을 구현한다의 유효 조건
When: 보행 어댑터의 단일 위치 소유권을 구현한다를 실행한다
Then: 같은 entity의 위치가 한 owner에서만 나오고 이동 인원수가 보존된다.

### AC-CS-SIM.02.01-N · NEGATIVE · NOT_RUN
Given: NavMesh와 worker 이중 이동·frame 반전이면 결과를 거부한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: NavMesh와 worker 이중 이동·frame 반전이면 결과를 거부한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [TECH-JPS14](https://github.com/PedestrianDynamics/jupedsim/discussions/1567) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-JUPED](https://www.jupedsim.org/stable/pedestrian_models/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-PEDDATA](https://www.fz-juelich.de/en/ias/ias-7/research-1/projects/fair2ped-1) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.02.02 · 병목·층간·보조 이동의 독립 보행 벤치마크를 수행한다

**상위:** CS-SIM.02 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 선정/보정/holdout을 분리하고 모델이 지원하는 현상만 비교한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.02.json](basis/v3/tasks/CS-SIM.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
병목·층간·보조 이동의 독립 보행 벤치마크를 수행한다

## 입력·출력 인터페이스
PedestrianState {entityId,frameId,position,velocity,tick,modelRef}; 독립 관측 전에는 현장 정확도 미판정.

**직접 담당 요구:** REQ-019, REQ-043
**지원 요구:** REQ-019, REQ-043
**부모 제품 시험:** AT-019, AT-043

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Simulation/PedestrianAdapter.cs` | CS-SIM.02.01 |
| CREATE | `benchmarks/pedestrian/protocol.json` | CS-SIM.02.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0202Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.02.01 | `OUT-CS-SIM.02.01@candidate` | candidate | ALWAYS |
| CS-SIM.01.02 | `OUT-CS-SIM.01.02@candidate` | integration | ALWAYS |
| CS-WORLD.01.02 | `OUT-CS-WORLD.01.02@candidate` | integration | ALWAYS |
| CS-WORLD.02.03 | `OUT-CS-WORLD.02.03@candidate` | qualification | NAMED_SITE_ACCURACY |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.02-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-QOI-CS-SIM.02.02` / qualification / ALWAYS: 독립 입력 split·수량·단위·사전 허용오차와 검수자 — 수치모델의 해당 주장만 차단

## 구현 순서
```text
1. 선정/보정/holdout을 분리하고 모델이 지원하는 현상만 비교한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 병목·층간·보조 이동의 독립 보행 벤치마크를 수행한다의 유효 조건
When: 병목·층간·보조 이동의 독립 보행 벤치마크를 수행한다를 실행한다
Then: 통과량·대기·궤적의 오차와 미지원 범위가 보고된다.

### AC-CS-SIM.02.02-N · NEGATIVE · NOT_RUN
Given: 평면 테스트 성공을 계단·임상 피해 예측의 검증으로 승격하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 평면 테스트 성공을 계단·임상 피해 예측의 검증으로 승격하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [TECH-JPS14](https://github.com/PedestrianDynamics/jupedsim/discussions/1567) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-JUPED](https://www.jupedsim.org/stable/pedestrian_models/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-PEDDATA](https://www.fz-juelich.de/en/ias/ias-7/research-1/projects/fair2ped-1) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.03.01 · 접근교통·차량·승무원 어댑터를 연결한다

**상위:** CS-SIM.03 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 고정 SUMO 구성과 route/crew/시각을 사용하고 도착 시나리오 대체를 별도 profile로 둔다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.03.json](basis/v3/tasks/CS-SIM.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
접근교통·차량·승무원 어댑터를 연결한다

## 입력·출력 인터페이스
TrafficState {vehicleId,crewIds,routeId,frameId,tick,provenance}; 철도 운행은 충돌/탈선 동역학과 다름.

**직접 담당 요구:** REQ-044
**지원 요구:** REQ-044
**부모 제품 시험:** AT-044

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Simulation/TrafficAdapter.cs` | CS-SIM.03.01 |
| CREATE | `workers/traffic/runner.py` | CS-SIM.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0301Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.01.03 | `OUT-CS-SIM.01.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.03-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.

## 구현 순서
```text
1. 고정 SUMO 구성과 route/crew/시각을 사용하고 도착 시나리오 대체를 별도 profile로 둔다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 접근교통·차량·승무원 어댑터를 연결한다의 유효 조건
When: 접근교통·차량·승무원 어댑터를 연결한다를 실행한다
Then: 차량·승무원이 중복 생성되지 않고 승인된 route로 결과가 게시된다.

### AC-CS-SIM.03.01-N · NEGATIVE · NOT_RUN
Given: silent teleport·비공개 우회가 발생하면 정량 자격을 제한한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: silent teleport·비공개 우회가 발생하면 정량 자격을 제한한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [TECH-SUMO](https://sumo.dlr.de/docs/Simulation/Emergency.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-SUMO-RAIL](https://sumo.dlr.de/docs/Simulation/Railways.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.03.02 · 현지 접근시간·운행 범위를 독립 자료와 비교한다

**상위:** CS-SIM.03 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 도로/철도 운영 QoI별 근거와 설정·불확도를 고정한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.03.json](basis/v3/tasks/CS-SIM.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
현지 접근시간·운행 범위를 독립 자료와 비교한다

## 입력·출력 인터페이스
TrafficState {vehicleId,crewIds,routeId,frameId,tick,provenance}; 철도 운행은 충돌/탈선 동역학과 다름.

**직접 담당 요구:** REQ-044
**지원 요구:** REQ-044
**부모 제품 시험:** AT-044

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Simulation/TrafficAdapter.cs` | CS-SIM.03.01 |
| CREATE | `benchmarks/traffic/protocol.json` | CS-SIM.03.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0302Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.03.01 | `OUT-CS-SIM.03.01@candidate` | candidate | ALWAYS |
| CS-SIM.01.02 | `OUT-CS-SIM.01.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.03-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-QOI-CS-SIM.03.02` / qualification / ALWAYS: 독립 입력 split·수량·단위·사전 허용오차와 검수자 — 수치모델의 해당 주장만 차단

## 구현 순서
```text
1. 도로/철도 운영 QoI별 근거와 설정·불확도를 고정한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 현지 접근시간·운행 범위를 독립 자료와 비교한다의 유효 조건
When: 현지 접근시간·운행 범위를 독립 자료와 비교한다를 실행한다
Then: 검증한 출동/정차 범위와 실패·가정이 구분된다.

### AC-CS-SIM.03.02-N · NEGATIVE · NOT_RUN
Given: 도착 가정 입력을 실제 출동시간 예측이나 탈선 동역학으로 부르지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 도착 가정 입력을 실제 출동시간 예측이나 탈선 동역학으로 부르지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [TECH-SUMO](https://sumo.dlr.de/docs/Simulation/Emergency.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-SUMO-RAIL](https://sumo.dlr.de/docs/Simulation/Railways.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.04.01 · FDS batch 입력·경계 이력·출력 파일을 결속한다

**상위:** CS-SIM.04 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 기준 solver version·mesh·재료·개구·열원 입력을 고정하고 결과 checksum을 기록한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.04.json](basis/v3/tasks/CS-SIM.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
FDS batch 입력·경계 이력·출력 파일을 결속한다

## 입력·출력 인터페이스
HazardRun {inputHash,solverLock,boundaryHistory,grid,qoi,resultRefs,validation}; 실제 관측없는 기준계산 일치는 현실 검증이 아님.

**직접 담당 요구:** REQ-042
**지원 요구:** REQ-042
**부모 제품 시험:** AT-042

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Simulation/HazardReferenceAdapter.cs` | CS-SIM.04.01 |
| CREATE | `workers/hazard/reference_runner.py` | CS-SIM.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0401Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.01.03 | `OUT-CS-SIM.01.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.04-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.

## 구현 순서
```text
1. 기준 solver version·mesh·재료·개구·열원 입력을 고정하고 결과 checksum을 기록한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 FDS batch 입력·경계 이력·출력 파일을 결속한다의 유효 조건
When: FDS batch 입력·경계 이력·출력 파일을 결속한다를 실행한다
Then: 입력해시와 output/QoI가 결속된 재현 가능한 기준 계산이 나온다.

### AC-CS-SIM.04.01-N · NEGATIVE · NOT_RUN
Given: 서로 다른 사전 연기 영상을 갈아끼우거나 live rollback을 지원한다고 가장하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 서로 다른 사전 연기 영상을 갈아끼우거나 live rollback을 지원한다고 가장하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0401Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [TECH-FDS](https://pages.nist.gov/fds-smv/manuals.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-FDS-SCOPE](https://pages.nist.gov/fds-smv/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.04.02 · 수렴·독립 관측·화재 QoI 오차를 평가한다

**상위:** CS-SIM.04 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** mesh·time-step 수렴과 holdout 비교를 분리하고 오차 기준을 사전에 고정한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.04.json](basis/v3/tasks/CS-SIM.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
수렴·독립 관측·화재 QoI 오차를 평가한다

## 입력·출력 인터페이스
HazardRun {inputHash,solverLock,boundaryHistory,grid,qoi,resultRefs,validation}; 실제 관측없는 기준계산 일치는 현실 검증이 아님.

**직접 담당 요구:** REQ-042
**지원 요구:** REQ-042
**부모 제품 시험:** AT-042

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Simulation/HazardReferenceAdapter.cs` | CS-SIM.04.01 |
| CREATE | `benchmarks/hazard/reference-protocol.json` | CS-SIM.04.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0402Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.04.01 | `OUT-CS-SIM.04.01@candidate` | candidate | ALWAYS |
| CS-SIM.01.02 | `OUT-CS-SIM.01.02@candidate` | integration | ALWAYS |
| CS-WORLD.02.03 | `OUT-CS-WORLD.02.03@candidate` | qualification | NAMED_SITE_ACCURACY |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.04-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-QOI-CS-SIM.04.02` / qualification / ALWAYS: 독립 입력 split·수량·단위·사전 허용오차와 검수자 — 수치모델의 해당 주장만 차단

## 구현 순서
```text
1. mesh·time-step 수렴과 holdout 비교를 분리하고 오차 기준을 사전에 고정한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 수렴·독립 관측·화재 QoI 오차를 평가한다의 유효 조건
When: 수렴·독립 관측·화재 QoI 오차를 평가한다를 실행한다
Then: 수량별 단위·오차·불확도·지원 범위가 보고된다.

### AC-CS-SIM.04.02-N · NEGATIVE · NOT_RUN
Given: FDS 근사 일치를 현장 검증이나 폭발/구조붕괴 예측으로 바꾸지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: FDS 근사 일치를 현장 검증이나 폭발/구조붕괴 예측으로 바꾸지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0402Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [TECH-FDS](https://pages.nist.gov/fds-smv/manuals.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-FDS-SCOPE](https://pages.nist.gov/fds-smv/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.05.01 · 공동시간과 수량 소유권 barrier를 구현한다

**상위:** CS-SIM.05 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 참여 worker·현상만 선택하고 integer time·valid interval·boundary/input revision을 확인한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.05.json](basis/v3/tasks/CS-SIM.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
공동시간과 수량 소유권 barrier를 구현한다

## 입력·출력 인터페이스
CommittedBoundary {tick,sequence,inputs,fieldOwners,resultHashes,capabilities}; 독립 reference/모델 결과는 runtime schema와 별도로 qualification.

**직접 담당 요구:** REQ-035, REQ-046, REQ-047, REQ-048, REQ-049
**지원 요구:** REQ-035, REQ-046, REQ-047, REQ-048, REQ-049
**부모 제품 시험:** AT-035, AT-046, AT-047, AT-048, AT-049

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Simulation/CosimulationCoordinator.cs` | CS-SIM.05.01 |
| CREATE | `Assets/ChooGuard/Simulation/QuantityOwnership.cs` | CS-SIM.05.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0501Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.01.03 | `OUT-CS-SIM.01.03@candidate` | candidate | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | candidate | ALWAYS |
| CS-LAB.01.01 | `OUT-CS-LAB.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.05-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.

## 구현 순서
```text
1. 참여 worker·현상만 선택하고 integer time·valid interval·boundary/input revision을 확인한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.05.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 공동시간과 수량 소유권 barrier를 구현한다의 유효 조건
When: 공동시간과 수량 소유권 barrier를 구현한다를 실행한다
Then: 모든 required 결과가 같은 cut을 만족할 때만 원자 게시된다.

### AC-CS-SIM.05.01-N · NEGATIVE · NOT_RUN
Given: 필수 worker 하나 실패 또는 이전 문 상태 결과면 부분 전진을 current로 만들지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 필수 worker 하나 실패 또는 이전 문 상태 결과면 부분 전진을 current로 만들지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0501Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.05.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [STD-FMI](https://fmi-standard.org/docs/3.0.2/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-MAPANYTHING](https://github.com/facebookresearch/map-anything) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-PHYSICSNEMO](https://docs.nvidia.com/physicsnemo/latest/index.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.05.02 · 전체 worker 복구와 결합오차를 대조한다

**상위:** CS-SIM.05 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 공통 checkpoint 또는 검증된 재실행 recipe로 영향 worker 모두를 복구한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.05.json](basis/v3/tasks/CS-SIM.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
전체 worker 복구와 결합오차를 대조한다

## 입력·출력 인터페이스
CommittedBoundary {tick,sequence,inputs,fieldOwners,resultHashes,capabilities}; 독립 reference/모델 결과는 runtime schema와 별도로 qualification.

**직접 담당 요구:** REQ-035, REQ-046, REQ-047, REQ-048, REQ-049
**지원 요구:** REQ-035, REQ-046, REQ-047, REQ-048, REQ-049
**부모 제품 시험:** AT-035, AT-046, AT-047, AT-048, AT-049

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Simulation/CosimulationCoordinator.cs` | CS-SIM.05.01 |
| CREATE | `Assets/ChooGuard/Simulation/ModelQualification.cs` | CS-SIM.05.02 |
| CREATE | `benchmarks/coupling/protocol.json` | CS-SIM.05.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0502Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.05.01 | `OUT-CS-SIM.05.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.05-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-QOI-CS-SIM.05.02` / qualification / ALWAYS: 독립 입력 split·수량·단위·사전 허용오차와 검수자 — 수치모델의 해당 주장만 차단

## 구현 순서
```text
1. 공통 checkpoint 또는 검증된 재실행 recipe로 영향 worker 모두를 복구한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.05.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 전체 worker 복구와 결합오차를 대조한다의 유효 조건
When: 전체 worker 복구와 결합오차를 대조한다를 실행한다
Then: 같은 초기 입력의 전체 재실행과 결합 결과가 사전 기준을 만족한다.

### AC-CS-SIM.05.02-N · NEGATIVE · NOT_RUN
Given: 한 worker만 복원한 뒤 다른 worker의 미래 상태를 섞지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 한 worker만 복원한 뒤 다른 worker의 미래 상태를 섞지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0502Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.05.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [STD-FMI](https://fmi-standard.org/docs/3.0.2/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-MAPANYTHING](https://github.com/facebookresearch/map-anything) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-PHYSICSNEMO](https://docs.nvidia.com/physicsnemo/latest/index.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SIM.05.03 · 검증된 가속모델과 유효범위 이탈을 처리한다

**상위:** CS-SIM.05 / CS-SIM · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 선택적 ROM/PhysicsNeMo에 형상·이력·장기 rollout·보존량·OOD 검사를 적용한다. 기준 계산은 계속 대안이다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SIM.05.json](basis/v3/tasks/CS-SIM.05.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
검증된 가속모델과 유효범위 이탈을 처리한다

## 입력·출력 인터페이스
CommittedBoundary {tick,sequence,inputs,fieldOwners,resultHashes,capabilities}; 독립 reference/모델 결과는 runtime schema와 별도로 qualification.

**직접 담당 요구:** REQ-035, REQ-046, REQ-047, REQ-048, REQ-049
**지원 요구:** REQ-035, REQ-046, REQ-047, REQ-048, REQ-049
**부모 제품 시험:** AT-035, AT-046, AT-047, AT-048, AT-049

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Simulation/ModelQualification.cs` | CS-SIM.05.02 |
| CREATE | `workers/hazard/surrogate_runner.py` | CS-SIM.05.03 |
| MODIFY | `benchmarks/coupling/protocol.json` | CS-SIM.05.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0503Tests.cs`
**시험 매체:** EDIT_MODE, WORKER_INTEGRATION
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SIM.05.02 | `OUT-CS-SIM.05.02@candidate` | candidate | ALWAYS |
| CS-SIM.01.02 | `OUT-CS-SIM.01.02@candidate` | integration | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | integration | ALWAYS |
| CS-LAB.01.02 | `OUT-CS-LAB.01.02@candidate` | integration | ALWAYS |
| CS-SIM.02.02 | `OUT-CS-SIM.02.02@candidate` | integration | PEDESTRIAN_USED |
| CS-SIM.03.02 | `OUT-CS-SIM.03.02@candidate` | integration | TRAFFIC_USED |
| CS-SIM.04.02 | `OUT-CS-SIM.04.02@candidate` | integration | HAZARD_USED |

## 외부 입력과 보류 범위
- `EXT-CS-SIM.05-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-QOI-CS-SIM.05.03` / qualification / ALWAYS: 독립 입력 split·수량·단위·사전 허용오차와 검수자 — 수치모델의 해당 주장만 차단

## 구현 순서
```text
1. 선택적 ROM/PhysicsNeMo에 형상·이력·장기 rollout·보존량·OOD 검사를 적용한다. 기준 계산은 계속 대안이다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SIM.05.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 검증된 가속모델과 유효범위 이탈을 처리한다의 유효 조건
When: 검증된 가속모델과 유효범위 이탈을 처리한다를 실행한다
Then: 수용 범위에서만 가속 결과를 쓰고 이탈은 계산대기/재계산/정량 보류로 보인다.

### AC-CS-SIM.05.03-N · NEGATIVE · NOT_RUN
Given: 가속기가 없다고 reference를 삭제하거나 낮은 confidence 값을 계속 승인하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 가속기가 없다고 reference를 삭제하거나 낮은 confidence 값을 계속 승인하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSIM0503Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SIM.05.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/05-worker-and-cosimulation.md](basis/v3/specs/05-worker-and-cosimulation.md)
- [basis/v3/specs/04-checkpoint-and-comparison.md](basis/v3/specs/04-checkpoint-and-comparison.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [STD-FMI](https://fmi-standard.org/docs/3.0.2/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-MAPANYTHING](https://github.com/facebookresearch/map-anything) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-PHYSICSNEMO](https://docs.nvidia.com/physicsnemo/latest/index.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SCRIPT.01.01 · 허용된 기관·판본의 근거를 제한 조회한다

**상위:** CS-SCRIPT.01 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** source ID와 clause revision·scope·접근권으로 결과를 제한하고 누락을 반환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.01.json](basis/v3/tasks/CS-SCRIPT.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
허용된 기관·판본의 근거를 제한 조회한다

## 입력·출력 인터페이스
AssistantProposal {type,claims,evidenceRefs,missing,patchDraft}; 결정적 상태 계산은 코어 담당.

**직접 담당 요구:** REQ-032, REQ-052, REQ-058, REQ-059
**지원 요구:** REQ-032, REQ-052, REQ-058, REQ-059
**부모 제품 시험:** AT-032, AT-052, AT-058, AT-059

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Authoring/EvidenceQuery.cs` | CS-SCRIPT.01.01 |
| CREATE | `workers/authoring/retrieve.py` | CS-SCRIPT.01.01 |
| CREATE | `workers/authoring/contracts.json` | CS-SCRIPT.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0101Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | candidate | ALWAYS |
| CS-OPS.06.01 | `OUT-CS-OPS.06.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. source ID와 clause revision·scope·접근권으로 결과를 제한하고 누락을 반환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 허용된 기관·판본의 근거를 제한 조회한다의 유효 조건
When: 허용된 기관·판본의 근거를 제한 조회한다를 실행한다
Then: 문장/제안의 근거가 실제 원문 locator까지 이어진다.

### AC-CS-SCRIPT.01.01-N · NEGATIVE · NOT_RUN
Given: 권한 밖 원문·미취득 내용·다른 기관 기준을 몰래 합치지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 권한 밖 원문·미취득 내용·다른 기관 기준을 몰래 합치지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)


---

# CS-SCRIPT.01.02 · AI 제안을 검증하고 실패시 수동 작업을 유지한다

**상위:** CS-SCRIPT.01 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** allowlisted proposal 타입만 허용하고 budget·timeout·반출 정책을 적용한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.01.json](basis/v3/tasks/CS-SCRIPT.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
AI 제안을 검증하고 실패시 수동 작업을 유지한다

## 입력·출력 인터페이스
AssistantProposal {type,claims,evidenceRefs,missing,patchDraft}; 결정적 상태 계산은 코어 담당.

**직접 담당 요구:** REQ-032, REQ-052, REQ-058, REQ-059
**지원 요구:** REQ-032, REQ-052, REQ-058, REQ-059
**부모 제품 시험:** AT-032, AT-052, AT-058, AT-059

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Authoring/AssistantGateway.cs` | CS-SCRIPT.01.02 |
| MODIFY | `workers/authoring/contracts.json` | CS-SCRIPT.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0102Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.01.01 | `OUT-CS-SCRIPT.01.01@candidate` | candidate | ALWAYS |
| CS-PACK.02.01 | `OUT-CS-PACK.02.01@candidate` | integration | ALWAYS |
| CS-OPS.06.02 | `OUT-CS-OPS.06.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. allowlisted proposal 타입만 허용하고 budget·timeout·반출 정책을 적용한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 AI 제안을 검증하고 실패시 수동 작업을 유지한다의 유효 조건
When: AI 제안을 검증하고 실패시 수동 작업을 유지한다를 실행한다
Then: 실행·승인 권한 없이 설명·patch draft·미확인만 반환한다.

### AC-CS-SCRIPT.01.02-N · NEGATIVE · NOT_RUN
Given: prompt injection·명령어·DB쓰기·위조 승인 출력은 적용하지 않고 기록한다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: prompt injection·명령어·DB쓰기·위조 승인 출력은 적용하지 않고 기록한다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)


---

# CS-SCRIPT.02.01 · 선택 운영안을 조건부 ScriptIR로 변환한다

**상위:** CS-SCRIPT.02 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 로그 사실·주석·가설·매뉴얼 제안·검수된 지시를 다른 claim type으로 연결한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.02.json](basis/v3/tasks/CS-SCRIPT.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
선택 운영안을 조건부 ScriptIR로 변환한다

## 입력·출력 인터페이스
ScriptIR {revision,plan,scenario,steps,roles,conditions,evidence,qualifications}; 내용변경은 새 revision.

**직접 담당 요구:** REQ-051, REQ-053, REQ-054
**지원 요구:** REQ-051, REQ-053, REQ-054
**부모 제품 시험:** AT-051, AT-053, AT-054

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Authoring/ScriptCompiler.cs` | CS-SCRIPT.02.01 |
| CREATE | `Assets/ChooGuard/Authoring/ScriptIR.cs` | CS-SCRIPT.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0201Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.01.01 | `OUT-CS-SCRIPT.01.01@candidate` | candidate | ALWAYS |
| CS-LAB.02.01 | `OUT-CS-LAB.02.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 로그 사실·주석·가설·매뉴얼 제안·검수된 지시를 다른 claim type으로 연결한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 선택 운영안을 조건부 ScriptIR로 변환한다의 유효 조건
When: 선택 운영안을 조건부 ScriptIR로 변환한다를 실행한다
Then: 고정 시간과 선행조건·역할·예외가 별도 필드로 남는다.

### AC-CS-SCRIPT.02.01-N · NEGATIVE · NOT_RUN
Given: 관측 한 번의 완료시간을 모든 상황의 의무시각으로 변환하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 관측 한 번의 완료시간을 모든 상황의 의무시각으로 변환하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [REF-MSEL](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SCRIPT.02.02 · 대본과 실행형 데이터의 의미 동등성을 검사한다

**상위:** CS-SCRIPT.02 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** entity/rule/role·분기·조건·revision 존재를 검사하고 같은 IR로 양쪽을 생성한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.02.json](basis/v3/tasks/CS-SCRIPT.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
대본과 실행형 데이터의 의미 동등성을 검사한다

## 입력·출력 인터페이스
ScriptIR {revision,plan,scenario,steps,roles,conditions,evidence,qualifications}; 내용변경은 새 revision.

**직접 담당 요구:** REQ-051, REQ-053, REQ-054
**지원 요구:** REQ-051, REQ-053, REQ-054
**부모 제품 시험:** AT-051, AT-053, AT-054

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Authoring/ScriptCompiler.cs` | CS-SCRIPT.02.01 |
| CREATE | `Assets/ChooGuard/Authoring/ScriptSemanticValidator.cs` | CS-SCRIPT.02.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0202Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.02.01 | `OUT-CS-SCRIPT.02.01@candidate` | candidate | ALWAYS |
| CS-SCRIPT.01.02 | `OUT-CS-SCRIPT.01.02@candidate` | integration | ALWAYS |
| CS-LAB.02.02 | `OUT-CS-LAB.02.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. entity/rule/role·분기·조건·revision 존재를 검사하고 같은 IR로 양쪽을 생성한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 대본과 실행형 데이터의 의미 동등성을 검사한다의 유효 조건
When: 대본과 실행형 데이터의 의미 동등성을 검사한다를 실행한다
Then: 문서 단계와 실행 조건을 양방향 추적할 수 있다.

### AC-CS-SCRIPT.02.02-N · NEGATIVE · NOT_RUN
Given: 없는 객체·근거·권한 또는 누락된 분기를 유창한 문장으로 통과시키지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 없는 객체·근거·권한 또는 누락된 분기를 유창한 문장으로 통과시키지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [REF-MSEL](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SCRIPT.03.01 · native 대본 편집과 의미·서식 diff를 만든다

**상위:** CS-SCRIPT.03 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** TMP overlay에서 입력·커서·selection·근거를 보존하고 변경 분류를 반환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.03.json](basis/v3/tasks/CS-SCRIPT.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
native 대본 편집과 의미·서식 diff를 만든다

## 입력·출력 인터페이스
ReviewDecision {reviewer,role,scope,revision,evidence,time,status}; focus와 UI가 승인권을 부여하지 않음.

**직접 담당 요구:** REQ-055, REQ-056
**지원 요구:** REQ-055, REQ-056
**부모 제품 시험:** AT-055, AT-056

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Presentation/Authoring/ScriptEditorPresenter.cs` | CS-SCRIPT.03.01 |
| CREATE | `Assets/ChooGuard/Presentation/Authoring/ScriptEditor.prefab` | CS-SCRIPT.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0301Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.02.01 | `OUT-CS-SCRIPT.02.01@candidate` | candidate | ALWAYS |
| CS-PLAY.01.02 | `OUT-CS-PLAY.01.02@candidate` | candidate | ALWAYS |
| CS-PLAY.04.02 | `OUT-CS-PLAY.04.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. TMP overlay에서 입력·커서·selection·근거를 보존하고 변경 분류를 반환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 native 대본 편집과 의미·서식 diff를 만든다의 유효 조건
When: native 대본 편집과 의미·서식 diff를 만든다를 실행한다
Then: 서식과 의미 편집을 구별하고 원본 로그가 변하지 않는다.

### AC-CS-SCRIPT.03.01-N · NEGATIVE · NOT_RUN
Given: IME·닫기/복귀로 dirty text가 손실되거나 문구 승인으로 자격이 바뀌지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: IME·닫기/복귀로 dirty text가 손실되거나 문구 승인으로 자격이 바뀌지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)


---

# CS-SCRIPT.03.02 · 검토 주체·범위·revision과 승인 만료를 연결한다

**상위:** CS-SCRIPT.03 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실행 검토·기관 지정훈련 수용을 분리하고 변경 시 관련 검토만 만료시킨다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.03.json](basis/v3/tasks/CS-SCRIPT.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
검토 주체·범위·revision과 승인 만료를 연결한다

## 입력·출력 인터페이스
ReviewDecision {reviewer,role,scope,revision,evidence,time,status}; focus와 UI가 승인권을 부여하지 않음.

**직접 담당 요구:** REQ-055, REQ-056
**지원 요구:** REQ-055, REQ-056
**부모 제품 시험:** AT-055, AT-056

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Authoring/ReviewLedger.cs` | CS-SCRIPT.03.02 |
| CREATE | `Assets/ChooGuard/Authoring/QualificationPolicy.cs` | CS-SCRIPT.03.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0302Tests.cs`
**시험 매체:** EDIT_MODE, PLAY_MODE, PLAYER_ACCEPTANCE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.03.01 | `OUT-CS-SCRIPT.03.01@candidate` | candidate | ALWAYS |
| CS-SCRIPT.02.02 | `OUT-CS-SCRIPT.02.02@candidate` | integration | ALWAYS |
| CS-PLAY.05.02 | `OUT-CS-PLAY.05.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 실행 검토·기관 지정훈련 수용을 분리하고 변경 시 관련 검토만 만료시킨다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 검토 주체·범위·revision과 승인 만료를 연결한다의 유효 조건
When: 검토 주체·범위·revision과 승인 만료를 연결한다를 실행한다
Then: 승인한 정확 revision·scope·근거·검수자가 기록된다.

### AC-CS-SCRIPT.03.02-N · NEGATIVE · NOT_RUN
Given: 다른 판본 승인·자기 승인으로 독립검수 요구를 충족했다고 처리하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 다른 판본 승인·자기 승인으로 독립검수 요구를 충족했다고 처리하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0302Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)


---

# CS-SCRIPT.04.01 · 같은 ScriptIR에서 JSON·Markdown 초안을 출력한다

**상위:** CS-SCRIPT.04 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** export preview에 실제 지원 형식·자격을 표시하고 atomic 파일 출력·오류 회복을 제공한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.04.json](basis/v3/tasks/CS-SCRIPT.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
같은 ScriptIR에서 JSON·Markdown 초안을 출력한다

## 입력·출력 인터페이스
TemplateProfile {format,revision,fields,exporter,qualification}; 결과는 임의 script를 실행하지 않는다.

**직접 담당 요구:** REQ-086
**지원 요구:** REQ-086
**부모 제품 시험:** AT-086

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Authoring/ExportService.cs` | CS-SCRIPT.04.01 |
| CREATE | `content/templates/profiles.json` | CS-SCRIPT.04.01 |
| CREATE | `Assets/ChooGuard/Presentation/Authoring/ExportDialog.prefab` | CS-SCRIPT.04.01 |
| CREATE | `Assets/ChooGuard/Presentation/Authoring/ExportDialogPresenter.cs` | CS-SCRIPT.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0401Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.02.02 | `OUT-CS-SCRIPT.02.02@candidate` | candidate | ALWAYS |
| CS-SCRIPT.03.02 | `OUT-CS-SCRIPT.03.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SCRIPT.04-CUSTOMER_WORKFLOW_OR_TEMPLATE` / qualification / ALWAYS: CUSTOMER_WORKFLOW_OR_TEMPLATE — 모집/실제양식 확인 전 고객 효용/지원완료 주장 금지.

## 구현 순서
```text
1. export preview에 실제 지원 형식·자격을 표시하고 atomic 파일 출력·오류 회복을 제공한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 같은 ScriptIR에서 JSON·Markdown 초안을 출력한다의 유효 조건
When: 같은 ScriptIR에서 JSON·Markdown 초안을 출력한다를 실행한다
Then: 형식별 같은 조건·역할·이벤트를 내보내고 새 run에 reload 가능하다.

### AC-CS-SCRIPT.04.01-N · NEGATIVE · NOT_RUN
Given: 부분 파일·권한 없는 승인본·임의 실행 코드가 포함되면 게시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 부분 파일·권한 없는 승인본·임의 실행 코드가 포함되면 게시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0401Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [DISC-05](https://preptoolkit.fema.gov/web/hseep-resources/design-and-development) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-07](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SCRIPT.04.02 · 고객 DOCX 양식을 결속하고 출력 워커를 검증한다

**상위:** CS-SCRIPT.04 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실제 template 필수열·스타일·버전을 적용해 문서의 의미·서식을 검수한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.04.json](basis/v3/tasks/CS-SCRIPT.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
고객 DOCX 양식을 결속하고 출력 워커를 검증한다

## 입력·출력 인터페이스
TemplateProfile {format,revision,fields,exporter,qualification}; 결과는 임의 script를 실행하지 않는다.

**직접 담당 요구:** REQ-086
**지원 요구:** REQ-086
**부모 제품 시험:** AT-086

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Authoring/ExportService.cs` | CS-SCRIPT.04.01 |
| CREATE | `workers/authoring/export_document.py` | CS-SCRIPT.04.02 |
| MODIFY | `content/templates/profiles.json` | CS-SCRIPT.04.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0402Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.04.01 | `OUT-CS-SCRIPT.04.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SCRIPT.04-CUSTOMER_WORKFLOW_OR_TEMPLATE` / qualification / ALWAYS: CUSTOMER_WORKFLOW_OR_TEMPLATE — 모집/실제양식 확인 전 고객 효용/지원완료 주장 금지.
- `EXT-TEMPLATE` / integration / ALWAYS: 실제 고객 DOCX template와 검수 경로 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 실제 template 필수열·스타일·버전을 적용해 문서의 의미·서식을 검수한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 고객 DOCX 양식을 결속하고 출력 워커를 검증한다의 유효 조건
When: 고객 DOCX 양식을 결속하고 출력 워커를 검증한다를 실행한다
Then: 고객 검수 가능한 DOCX와 출력 후 수정량이 남는다.

### AC-CS-SCRIPT.04.02-N · NEGATIVE · NOT_RUN
Given: DOCX 성공을 HWPX/PDF 왕복 지원으로 표시하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: DOCX 성공을 HWPX/PDF 왕복 지원으로 표시하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0402Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [DISC-05](https://preptoolkit.fema.gov/web/hseep-resources/design-and-development) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-07](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SCRIPT.04.03 · 외부 편집의 의미 차이를 운영안으로 조정한다

**상위:** CS-SCRIPT.04 / CS-SCRIPT · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** ScriptStepId와 revision으로 변경을 연결하고 불가능한 importer는 수동 조정임을 명시한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SCRIPT.04.json](basis/v3/tasks/CS-SCRIPT.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
외부 편집의 의미 차이를 운영안으로 조정한다

## 입력·출력 인터페이스
TemplateProfile {format,revision,fields,exporter,qualification}; 결과는 임의 script를 실행하지 않는다.

**직접 담당 요구:** REQ-086
**지원 요구:** REQ-086
**부모 제품 시험:** AT-086

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Authoring/ImportReconciliation.cs` | CS-SCRIPT.04.03 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0403Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SCRIPT.04.02 | `OUT-CS-SCRIPT.04.02@candidate` | candidate | ALWAYS |
| CS-SCRIPT.03.02 | `OUT-CS-SCRIPT.03.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-SCRIPT.04-CUSTOMER_WORKFLOW_OR_TEMPLATE` / qualification / ALWAYS: CUSTOMER_WORKFLOW_OR_TEMPLATE — 모집/실제양식 확인 전 고객 효용/지원완료 주장 금지.

## 구현 순서
```text
1. ScriptStepId와 revision으로 변경을 연결하고 불가능한 importer는 수동 조정임을 명시한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SCRIPT.04.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 외부 편집의 의미 차이를 운영안으로 조정한다의 유효 조건
When: 외부 편집의 의미 차이를 운영안으로 조정한다를 실행한다
Then: 의미 수정은 재검토·재실행에 연결되고 이력이 유지된다.

### AC-CS-SCRIPT.04.03-N · NEGATIVE · NOT_RUN
Given: 외부 문서 편집이 원본 실행로그나 승인된 plan을 조용히 덮지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 외부 문서 편집이 원본 실행로그나 승인된 plan을 조용히 덮지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSCRIPT0403Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SCRIPT.04.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/09-script-and-approval.md](basis/v3/specs/09-script-and-approval.md)
- [basis/v3/specs/02-wire-and-ports.md](basis/v3/specs/02-wire-and-ports.md)
- [DISC-05](https://preptoolkit.fema.gov/web/hseep-resources/design-and-development) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-07](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PROOF.01.01 · 실제 작성 과제·시간·동의 조사 프로토콜을 고정한다

**상위:** CS-PROOF.01 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 대본 수정과 기관 협의의 실제 과업·관찰 시계·동의·검열을 정의한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.01.json](basis/v3/tasks/CS-PROOF.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
실제 작성 과제·시간·동의 조사 프로토콜을 고정한다

## 입력·출력 인터페이스
StudyIntake {consent,roles,caseRefs,workflow,template,measurements,missing}; 표본 제안과 모집 완료를 분리.

**직접 담당 요구:** REQ-077, REQ-078
**지원 요구:** REQ-077, REQ-078
**부모 제품 시험:** AT-077, AT-078

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `research/discovery/protocol.md` | CS-PROOF.01.01 |
| CREATE | `research/discovery/intake.schema.json` | CS-PROOF.01.01 |
| CREATE | `research/discovery/time-observation.schema.json` | CS-PROOF.01.01 |

**이 story 전용 시험:** `research/checks/CS-PROOF.01.01.json`
**시험 매체:** DOCUMENT_REVIEW
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| 제품 선행 산출물 없음 | — | — | 환경/권한 확인은 별도 |

## 외부 입력과 보류 범위
- `EXT-CS-PROOF.01-CUSTOMER_WORKFLOW_OR_TEMPLATE` / qualification / ALWAYS: CUSTOMER_WORKFLOW_OR_TEMPLATE — 모집/실제양식 확인 전 고객 효용/지원완료 주장 금지.

## 구현 순서
```text
1. 대본 수정과 기관 협의의 실제 과업·관찰 시계·동의·검열을 정의한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 실제 작성 과제·시간·동의 조사 프로토콜을 고정한다의 유효 조건
When: 실제 작성 과제·시간·동의 조사 프로토콜을 고정한다를 실행한다
Then: 가설·관찰·모집 현황을 구분한 조사 계획이 있다.

### AC-CS-PROOF.01.01-N · NEGATIVE · NOT_RUN
Given: 가상 인터뷰나 미모집 인원을 실제 사용자 결과로 채우지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 가상 인터뷰나 미모집 인원을 실제 사용자 결과로 채우지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 research/checks/CS-PROOF.01.01.json에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-01](https://www.mois.go.kr/frt/sub/a06/b11/disasterTraining_4/screen.do) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-03](https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=26342) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-05](https://preptoolkit.fema.gov/web/hseep-resources/design-and-development) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-18](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PROOF.01.02 · 허용된 최근 대본·양식과 관찰 결과를 입고한다

**상위:** CS-PROOF.01 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실제 작성자·협조·검수자 업무와 도움·대리입력·회신 지연을 구분해서 기록한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.01.json](basis/v3/tasks/CS-PROOF.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
허용된 최근 대본·양식과 관찰 결과를 입고한다

## 입력·출력 인터페이스
StudyIntake {consent,roles,caseRefs,workflow,template,measurements,missing}; 표본 제안과 모집 완료를 분리.

**직접 담당 요구:** REQ-077, REQ-078
**지원 요구:** REQ-077, REQ-078
**부모 제품 시험:** AT-077, AT-078

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `research/discovery/protocol.md` | CS-PROOF.01.01 |
| MODIFY | `research/discovery/intake.schema.json` | CS-PROOF.01.01 |
| MODIFY | `research/discovery/time-observation.schema.json` | CS-PROOF.01.01 |

**이 story 전용 시험:** `research/checks/CS-PROOF.01.02.json`
**시험 매체:** DOCUMENT_REVIEW
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PROOF.01.01 | `OUT-CS-PROOF.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-PROOF.01-CUSTOMER_WORKFLOW_OR_TEMPLATE` / qualification / ALWAYS: CUSTOMER_WORKFLOW_OR_TEMPLATE — 모집/실제양식 확인 전 고객 효용/지원완료 주장 금지.
- `EXT-CONSENTED-CASE` / candidate / ALWAYS: 동의된 작성자·최근 실제 대본/수정기록. 합성 인터뷰 금지 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 실제 작성자·협조·검수자 업무와 도움·대리입력·회신 지연을 구분해서 기록한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 허용된 최근 대본·양식과 관찰 결과를 입고한다의 유효 조건
When: 허용된 최근 대본·양식과 관찰 결과를 입고한다를 실행한다
Then: 공백 입력과 차단되는 실증 범위가 기록된다.

### AC-CS-PROOF.01.02-N · NEGATIVE · NOT_RUN
Given: 동의 없는 자료나 훈련 참가자 수를 제품 사용자 표본으로 사용하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 동의 없는 자료나 훈련 참가자 수를 제품 사용자 표본으로 사용하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 research/checks/CS-PROOF.01.02.json에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-01](https://www.mois.go.kr/frt/sub/a06/b11/disasterTraining_4/screen.do) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-03](https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=26342) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-05](https://preptoolkit.fema.gov/web/hseep-resources/design-and-development) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-18](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PROOF.02.01 · 첫 native 요청·저장·재열기 수직구간을 검수한다

**상위:** CS-PROOF.02 / CS-PROOF · **작업창:** W3 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 두 기관 fixture에서 UI 요청→원자 기록→receipt 표시→프로세스 재열기를 실제 Player로 실행한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.02.json](basis/v3/tasks/CS-PROOF.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
첫 native 요청·저장·재열기 수직구간을 검수한다

## 입력·출력 인터페이스
Qualification별 evidence bundle; 기능/성능/정량/현장 수용은 별도.

**직접 담당 요구:** REQ-001, REQ-070, REQ-074
**지원 요구:** REQ-001, REQ-070, REQ-074
**부모 제품 시험:** AT-001, AT-070, AT-074

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Tests/Acceptance/EndToEndFlowTests.cs` | CS-PROOF.02.01 |
| CREATE | `qualification/technical-profile.json` | CS-PROOF.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPROOF0201Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.02.03 | `OUT-CS-BOOT.02.03@candidate` | candidate | ALWAYS |
| CS-BOOT.03.02 | `OUT-CS-BOOT.03.02@candidate` | candidate | ALWAYS |
| CS-PLAY.02.02 | `OUT-CS-PLAY.02.02@candidate` | candidate | ALWAYS |
| CS-PLAY.01.03 | `OUT-CS-PLAY.01.03@candidate` | candidate | ALWAYS |
| CS-OPS.02.04 | `OUT-CS-OPS.02.04@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 두 기관 fixture에서 UI 요청→원자 기록→receipt 표시→프로세스 재열기를 실제 Player로 실행한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 첫 native 요청·저장·재열기 수직구간을 검수한다의 유효 조건
When: 첫 native 요청·저장·재열기 수직구간을 검수한다를 실행한다
Then: 같은 key 재요청이 재적용되지 않고 예약·receipt가 재열기 후 일치한다.

### AC-CS-PROOF.02.01-N · NEGATIVE · NOT_RUN
Given: UI double 성공·인메모리 DB만으로 이 구간 통합을 승인하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: UI double 성공·인메모리 DB만으로 이 구간 통합을 승인하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPROOF0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)


---

# CS-PROOF.02.02 · 전체 A/B·두 모드·대본·새 run 흐름을 검수한다

**상위:** CS-PROOF.02 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 본체 운영을 A에서 B로 수정하고 비교·사람선택·대본출력·새 실행으로 연결한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.02.json](basis/v3/tasks/CS-PROOF.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
전체 A/B·두 모드·대본·새 run 흐름을 검수한다

## 입력·출력 인터페이스
Qualification별 evidence bundle; 기능/성능/정량/현장 수용은 별도.

**직접 담당 요구:** REQ-001, REQ-070, REQ-074
**지원 요구:** REQ-001, REQ-070, REQ-074
**부모 제품 시험:** AT-001, AT-070, AT-074

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Tests/Acceptance/EndToEndFlowTests.cs` | CS-PROOF.02.01 |
| MODIFY | `qualification/technical-profile.json` | CS-PROOF.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPROOF0202Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PROOF.02.01 | `OUT-CS-PROOF.02.01@candidate` | candidate | ALWAYS |
| CS-LAB.03.02 | `OUT-CS-LAB.03.02@candidate` | candidate | ALWAYS |
| CS-MODES.03.03 | `OUT-CS-MODES.03.03@candidate` | candidate | ALWAYS |
| CS-SCRIPT.04.01 | `OUT-CS-SCRIPT.04.01@candidate` | candidate | ALWAYS |
| CS-PLAY.06.02 | `OUT-CS-PLAY.06.02@candidate` | candidate | ALWAYS |
| CS-OPS.07.02 | `OUT-CS-OPS.07.02@candidate` | candidate | ALWAYS |
| CS-OPS.05.03 | `OUT-CS-OPS.05.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 본체 운영을 A에서 B로 수정하고 비교·사람선택·대본출력·새 실행으로 연결한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 전체 A/B·두 모드·대본·새 run 흐름을 검수한다의 유효 조건
When: 전체 A/B·두 모드·대본·새 run 흐름을 검수한다를 실행한다
Then: 모드 공통 코어와 lineage·정보범위·대본 의미가 보존된다.

### AC-CS-PROOF.02.02-N · NEGATIVE · NOT_RUN
Given: replay·고정 버튼재생을 새 시나리오 실행으로 인정하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: replay·고정 버튼재생을 새 시나리오 실행으로 인정하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPROOF0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)


---

# CS-PROOF.02.03 · 고정 부하에서 장애·입력·복구·성능을 대조한다

**상위:** CS-PROOF.02 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 저장실패·worker crash·IME·뒤클릭·network loss·부하를 고정 환경에서 주입한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.02.json](basis/v3/tasks/CS-PROOF.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
고정 부하에서 장애·입력·복구·성능을 대조한다

## 입력·출력 인터페이스
Qualification별 evidence bundle; 기능/성능/정량/현장 수용은 별도.

**직접 담당 요구:** REQ-001, REQ-070, REQ-074
**지원 요구:** REQ-001, REQ-070, REQ-074
**부모 제품 시험:** AT-001, AT-070, AT-074

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Tests/Acceptance/FaultInjectionTests.cs` | CS-PROOF.02.03 |
| MODIFY | `qualification/technical-profile.json` | CS-PROOF.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPROOF0203Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PROOF.02.02 | `OUT-CS-PROOF.02.02@candidate` | candidate | ALWAYS |
| CS-OPS.06.02 | `OUT-CS-OPS.06.02@candidate` | integration | ALWAYS |
| CS-PLAY.05.02 | `OUT-CS-PLAY.05.02@candidate` | integration | ALWAYS |
| CS-LAB.04.02 | `OUT-CS-LAB.04.02@candidate` | integration | ALWAYS |
| CS-MODES.03.03 | `OUT-CS-MODES.03.03@candidate` | integration | ALWAYS |
| CS-SCRIPT.04.03 | `OUT-CS-SCRIPT.04.03@candidate` | integration | ALWAYS |
| CS-PLAY.06.02 | `OUT-CS-PLAY.06.02@candidate` | integration | ALWAYS |
| CS-OPS.07.02 | `OUT-CS-OPS.07.02@candidate` | integration | ALWAYS |
| CS-SIM.01.03 | `OUT-CS-SIM.01.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 저장실패·worker crash·IME·뒤클릭·network loss·부하를 고정 환경에서 주입한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.02.03-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 고정 부하에서 장애·입력·복구·성능을 대조한다의 유효 조건
When: 고정 부하에서 장애·입력·복구·성능을 대조한다를 실행한다
Then: 계산/렌더/AI/운영 비용과 실패·미실행이 분리된 raw 결과가 있다.

### AC-CS-PROOF.02.03-N · NEGATIVE · NOT_RUN
Given: 성능을 맞추려고 인원·현상·정확도를 숨겨 낮추지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 성능을 맞추려고 인원·현상·정확도를 숨겨 낮추지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPROOF0203Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.02.03 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)


---

# CS-PROOF.03.01 · A/B/C 평가를 사전 등록하고 독립 과제를 고정한다

**상위:** CS-PROOF.03 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 동일 코어 B/C, 실제 방식 A의 정보·AI·도움·순서·난이도를 고정한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.03.json](basis/v3/tasks/CS-PROOF.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
A/B/C 평가를 사전 등록하고 독립 과제를 고정한다

## 입력·출력 인터페이스
StudyResult {conditions,participants,quality,time,censoring,assistance,limits}; 제품정확도를 선호도로 증명하지 않음.

**직접 담당 요구:** REQ-069, REQ-080, REQ-082, REQ-089
**지원 요구:** REQ-069, REQ-080, REQ-082, REQ-089
**부모 제품 시험:** AT-069, AT-080, AT-082, AT-089

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `research/usability/protocol.json` | CS-PROOF.03.01 |
| CREATE | `research/usability/analysis.py` | CS-PROOF.03.01 |

**이 story 전용 시험:** `research/checks/CS-PROOF.03.01.json`
**시험 매체:** DOCUMENT_REVIEW
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PROOF.01.01 | `OUT-CS-PROOF.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-PROOF.03-CUSTOMER_WORKFLOW_OR_TEMPLATE` / qualification / ALWAYS: CUSTOMER_WORKFLOW_OR_TEMPLATE — 모집/실제양식 확인 전 고객 효용/지원완료 주장 금지.

## 구현 순서
```text
1. 동일 코어 B/C, 실제 방식 A의 정보·AI·도움·순서·난이도를 고정한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 A/B/C 평가를 사전 등록하고 독립 과제를 고정한다의 유효 조건
When: A/B/C 평가를 사전 등록하고 독립 과제를 고정한다를 실행한다
Then: 실험 전 종료·품질·표본·분석 기준을 기록한다.

### AC-CS-PROOF.03.01-N · NEGATIVE · NOT_RUN
Given: 좋아 보이는 결과를 보고 20% 목표나 완료 분모를 바꾸지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 좋아 보이는 결과를 보고 20% 목표나 완료 분모를 바꾸지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 research/checks/CS-PROOF.03.01.json에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-05](https://preptoolkit.fema.gov/web/hseep-resources/design-and-development) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-07](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-08](https://www.conducttr.com/ai-assistance) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-09](https://www.xvrsim.com/en/platform/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-11](https://www.nist.gov/publications/credibility-consideration-digital-twins-manufacturing) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-001](https://rgsdev.itch.io/free-low-poly-vehicles-pack) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PROOF.03.02 · 총인시·품질·미완료·반복비용을 분석한다

**상위:** CS-PROOF.03 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 중첩 합집합·초기비용·유지비·도움·검열·서식수정을 포함한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.03.json](basis/v3/tasks/CS-PROOF.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
총인시·품질·미완료·반복비용을 분석한다

## 입력·출력 인터페이스
StudyResult {conditions,participants,quality,time,censoring,assistance,limits}; 제품정확도를 선호도로 증명하지 않음.

**직접 담당 요구:** REQ-069, REQ-080, REQ-082, REQ-089
**지원 요구:** REQ-069, REQ-080, REQ-082, REQ-089
**부모 제품 시험:** AT-069, AT-080, AT-082, AT-089

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `research/usability/analysis.py` | CS-PROOF.03.01 |
| CREATE | `research/usability/report-template.md` | CS-PROOF.03.02 |

**이 story 전용 시험:** `research/checks/CS-PROOF.03.02.json`
**시험 매체:** DOCUMENT_REVIEW
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PROOF.03.01 | `OUT-CS-PROOF.03.01@candidate` | candidate | ALWAYS |
| CS-PROOF.01.01 | `OUT-CS-PROOF.01.01@candidate` | integration | ALWAYS |
| CS-PROOF.02.03 | `OUT-CS-PROOF.02.03@candidate` | integration | ALWAYS |
| CS-PLAY.07.01 | `OUT-CS-PLAY.07.01@candidate` | integration | ALWAYS |
| CS-PROOF.02.02 | `OUT-CS-PROOF.02.02@candidate` | candidate | ALWAYS |
| CS-PLAY.07.01 | `OUT-CS-PLAY.07.01@candidate` | candidate | ALWAYS |
| CS-PROOF.01.02 | `OUT-CS-PROOF.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-PROOF.03-CUSTOMER_WORKFLOW_OR_TEMPLATE` / qualification / ALWAYS: CUSTOMER_WORKFLOW_OR_TEMPLATE — 모집/실제양식 확인 전 고객 효용/지원완료 주장 금지.
- `EXT-PARTICIPANTS` / integration / ALWAYS: 동의된 표본·독립 과제·전체 완료/중단 기록 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 중첩 합집합·초기비용·유지비·도움·검열·서식수정을 포함한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 총인시·품질·미완료·반복비용을 분석한다의 유효 조건
When: 총인시·품질·미완료·반복비용을 분석한다를 실행한다
Then: 효용과 불충분 결론·초기비용 회수 여부가 구분된다.

### AC-CS-PROOF.03.02-N · NEGATIVE · NOT_RUN
Given: 선호도나 완료자만의 시간으로 전체 사용자 효용·현장 안전성을 주장하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 선호도나 완료자만의 시간으로 전체 사용자 효용·현장 안전성을 주장하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 research/checks/CS-PROOF.03.02.json에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-05](https://preptoolkit.fema.gov/web/hseep-resources/design-and-development) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-07](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-08](https://www.conducttr.com/ai-assistance) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-09](https://www.xvrsim.com/en/platform/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-11](https://www.nist.gov/publications/credibility-consideration-digital-twins-manufacturing) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [FREE-001](https://rgsdev.itch.io/free-low-poly-vehicles-pack) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PROOF.04.01 · 지정 용도의 독립 정확도·불확도 프로토콜을 고정한다

**상위:** CS-PROOF.04 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 현장/사건/QoI/관측/holdout/오차·기관 역할을 사전 결속한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.04.json](basis/v3/tasks/CS-PROOF.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
지정 용도의 독립 정확도·불확도 프로토콜을 고정한다

## 입력·출력 인터페이스
UsePassport {site,scope,manuals,models,qoi,observations,uncertainty,reviews}; 실제 설비 제어 기능 없음.

**직접 담당 요구:** REQ-040, REQ-067, REQ-068, REQ-076, REQ-090
**지원 요구:** REQ-040, REQ-067, REQ-068, REQ-076, REQ-090
**부모 제품 시험:** AT-040, AT-067, AT-068, AT-076, AT-090

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `qualification/field/protocol.json` | CS-PROOF.04.01 |
| CREATE | `qualification/field/passport.schema.json` | CS-PROOF.04.01 |

**이 story 전용 시험:** `research/checks/CS-PROOF.04.01.json`
**시험 매체:** DOCUMENT_REVIEW
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PROOF.01.01 | `OUT-CS-PROOF.01.01@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-PROOF.04-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-QOI-CS-PROOF.04.01` / qualification / ALWAYS: 독립 입력 split·수량·단위·사전 허용오차와 검수자 — 수치모델의 해당 주장만 차단

## 구현 순서
```text
1. 현장/사건/QoI/관측/holdout/오차·기관 역할을 사전 결속한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.04.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 지정 용도의 독립 정확도·불확도 프로토콜을 고정한다의 유효 조건
When: 지정 용도의 독립 정확도·불확도 프로토콜을 고정한다를 실행한다
Then: 수량별 검증 범위와 미지원 주장이 명시된다.

### AC-CS-PROOF.04.01-N · NEGATIVE · NOT_RUN
Given: 같은 보정자료 재사용·임의 임계값 또는 모델간 일치를 현장 검증으로 대체하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 같은 보정자료 재사용·임의 임계값 또는 모델간 일치를 현장 검증으로 대체하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 research/checks/CS-PROOF.04.01.json에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.04.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-11](https://www.nist.gov/publications/credibility-consideration-digital-twins-manufacturing) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-DTC](https://www.digitaltwinconsortium.org/initiatives/the-definition-of-a-digital-twin/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-VV](https://www.nist.gov/publications/verification-and-validation-process-fire-model) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-PROOF.04.02 · 기관 검토와 새로운 관측의 지정용도 자격을 기록한다

**상위:** CS-PROOF.04 / CS-PROOF · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 별도 검수자의 근거·범위·판본·시각을 기록하고 동적 트윈 주장에는 실제 갱신 증거를 요구한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PROOF.04.json](basis/v3/tasks/CS-PROOF.04.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
기관 검토와 새로운 관측의 지정용도 자격을 기록한다

## 입력·출력 인터페이스
UsePassport {site,scope,manuals,models,qoi,observations,uncertainty,reviews}; 실제 설비 제어 기능 없음.

**직접 담당 요구:** REQ-040, REQ-067, REQ-068, REQ-076, REQ-090
**지원 요구:** REQ-040, REQ-067, REQ-068, REQ-076, REQ-090
**부모 제품 시험:** AT-040, AT-067, AT-068, AT-076, AT-090

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `qualification/field/passport.schema.json` | CS-PROOF.04.01 |
| CREATE | `qualification/field/review-ledger.json` | CS-PROOF.04.02 |

**이 story 전용 시험:** `research/checks/CS-PROOF.04.02.json`
**시험 매체:** DOCUMENT_REVIEW
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PROOF.04.01 | `OUT-CS-PROOF.04.01@candidate` | candidate | ALWAYS |
| CS-PACK.04.02 | `OUT-CS-PACK.04.02@candidate` | integration | ALWAYS |
| CS-PROOF.02.03 | `OUT-CS-PROOF.02.03@candidate` | integration | ALWAYS |
| CS-SIM.05.03 | `OUT-CS-SIM.05.03@candidate` | integration | ALWAYS |
| CS-WORLD.02.03 | `OUT-CS-WORLD.02.03@candidate` | integration | ALWAYS |
| CS-PACK.04.02 | `OUT-CS-PACK.04.02@candidate` | candidate | ALWAYS |
| CS-SIM.05.02 | `OUT-CS-SIM.05.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
- `EXT-CS-PROOF.04-FIELD_DATA_AND_INDEPENDENT_REFERENCE` / qualification / CLAIM_FIELD_USE: FIELD_DATA_AND_INDEPENDENT_REFERENCE — 해당 주장/검수만 제한하며 기술 fixture 개발을 전역 차단하지 않는다.
- `EXT-AGENCY-REVIEW` / qualification / ALWAYS: 해당 사용범위의 실제 독립 검수자·기관 권한과 결정 — 해당 story/phase만 보류; 다른 독립 개발은 지속

## 구현 순서
```text
1. 별도 검수자의 근거·범위·판본·시각을 기록하고 동적 트윈 주장에는 실제 갱신 증거를 요구한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PROOF.04.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 기관 검토와 새로운 관측의 지정용도 자격을 기록한다의 유효 조건
When: 기관 검토와 새로운 관측의 지정용도 자격을 기록한다를 실행한다
Then: 승인된 정확 사용범위와 새 revision 재검토가 남는다.

### AC-CS-PROOF.04.02-N · NEGATIVE · NOT_RUN
Given: 한 장소의 검토를 모든 재난·기관·모델로 확대하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 한 장소의 검토를 모든 재난·기관·모델로 확대하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 research/checks/CS-PROOF.04.02.json에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PROOF.04.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/10-observability-and-study.md](basis/v3/specs/10-observability-and-study.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-11](https://www.nist.gov/publications/credibility-consideration-digital-twins-manufacturing) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [STD-DTC](https://www.digitaltwinconsortium.org/initiatives/the-definition-of-a-digital-twin/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [TECH-VV](https://www.nist.gov/publications/verification-and-validation-process-fire-model) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SHIP.01.01 · 로컬 배포물과 지원 기능 manifest를 생성한다

**상위:** CS-SHIP.01 / CS-SHIP · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** build/content/model lock·프로필·한계를 결속하고 키·개인정보·제한 원본을 제외한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SHIP.01.json](basis/v3/tasks/CS-SHIP.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
로컬 배포물과 지원 기능 manifest를 생성한다

## 입력·출력 인터페이스
ProductManifest {buildLock,contentRefs,modelRefs,capabilities,qualification}; 구매승인은 개발선행이 아님.

**직접 담당 요구:** REQ-073, REQ-087
**지원 요구:** REQ-073, REQ-087
**부모 제품 시험:** AT-073, AT-087

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `packaging/product-manifest.json` | CS-SHIP.01.01 |
| CREATE | `scripts/release/Build-Installer.ps1` | CS-SHIP.01.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0101Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-BOOT.03.02 | `OUT-CS-BOOT.03.02@candidate` | candidate | ALWAYS |
| CS-PROOF.02.02 | `OUT-CS-PROOF.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. build/content/model lock·프로필·한계를 결속하고 키·개인정보·제한 원본을 제외한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SHIP.01.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 로컬 배포물과 지원 기능 manifest를 생성한다의 유효 조건
When: 로컬 배포물과 지원 기능 manifest를 생성한다를 실행한다
Then: 새 PC에서 지원한 로컬 실행·저장·수동 편집이 가능하다.

### AC-CS-SHIP.01.01-N · NEGATIVE · NOT_RUN
Given: 외부 AI 부재를 세션 손실로 처리하거나 미검증 표식을 삭제하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 외부 AI 부재를 세션 손실로 처리하거나 미검증 표식을 삭제하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0101Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SHIP.01.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/11-release-and-backup.md](basis/v3/specs/11-release-and-backup.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)


---

# CS-SHIP.01.02 · 원자 업데이트·실패 롤백·호환성을 검사한다

**상위:** CS-SHIP.01 / CS-SHIP · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 새 버전을 staging에 검증하고 기존 실행/데이터 보존 후 전환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SHIP.01.json](basis/v3/tasks/CS-SHIP.01.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
원자 업데이트·실패 롤백·호환성을 검사한다

## 입력·출력 인터페이스
ProductManifest {buildLock,contentRefs,modelRefs,capabilities,qualification}; 구매승인은 개발선행이 아님.

**직접 담당 요구:** REQ-073, REQ-087
**지원 요구:** REQ-073, REQ-087
**부모 제품 시험:** AT-073, AT-087

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `packaging/product-manifest.json` | CS-SHIP.01.01 |
| CREATE | `packaging/update-policy.json` | CS-SHIP.01.02 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0102Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SHIP.01.01 | `OUT-CS-SHIP.01.01@candidate` | candidate | ALWAYS |
| CS-BOOT.03.02 | `OUT-CS-BOOT.03.02@candidate` | integration | ALWAYS |
| CS-PROOF.02.03 | `OUT-CS-PROOF.02.03@candidate` | integration | ALWAYS |
| CS-PROOF.04.02 | `OUT-CS-PROOF.04.02@candidate` | qualification | CLAIM_FIELD_USE |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 새 버전을 staging에 검증하고 기존 실행/데이터 보존 후 전환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SHIP.01.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 원자 업데이트·실패 롤백·호환성을 검사한다의 유효 조건
When: 원자 업데이트·실패 롤백·호환성을 검사한다를 실행한다
Then: 실패 업데이트 뒤 기존 제품·DB를 다시 열 수 있다.

### AC-CS-SHIP.01.02-N · NEGATIVE · NOT_RUN
Given: 부분 다운로드나 다른 hash를 실행하거나 active run 파일을 덮지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 부분 다운로드나 다른 hash를 실행하거나 active run 파일을 덮지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0102Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SHIP.01.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/11-release-and-backup.md](basis/v3/specs/11-release-and-backup.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)


---

# CS-SHIP.02.01 · 일관된 DB snapshot·blob pin 백업을 만든다

**상위:** CS-SHIP.02 / CS-SHIP · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 실행 DB의 backup API로 일관된 cut을 만들고 참조 blob을 GC에서 pin한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SHIP.02.json](basis/v3/tasks/CS-SHIP.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
일관된 DB snapshot·blob pin 백업을 만든다

## 입력·출력 인터페이스
BackupManifest {cut,dbHash,blobHashes,schema,format,scope}; 삭제는 별도 명시된 운영정책에 따른다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-001
**부모 제품 시험:** AT-001

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Persistence/BackupService.cs` | CS-SHIP.02.01 |
| CREATE | `packaging/restore-policy.json` | CS-SHIP.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0201Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-OPS.02.04 | `OUT-CS-OPS.02.04@candidate` | candidate | ALWAYS |
| CS-LAB.01.02 | `OUT-CS-LAB.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 실행 DB의 backup API로 일관된 cut을 만들고 참조 blob을 GC에서 pin한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SHIP.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 일관된 DB snapshot·blob pin 백업을 만든다의 유효 조건
When: 일관된 DB snapshot·blob pin 백업을 만든다를 실행한다
Then: manifest-last로 DB·blob 해시를 함께 검증할 수 있다.

### AC-CS-SHIP.02.01-N · NEGATIVE · NOT_RUN
Given: WAL을 무시한 .db 복사·누락 blob·부분 게시를 정상 백업으로 인정하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: WAL을 무시한 .db 복사·누락 blob·부분 게시를 정상 백업으로 인정하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SHIP.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/11-release-and-backup.md](basis/v3/specs/11-release-and-backup.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-SQLITE-BACKUP](https://sqlite.org/backup.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [AUD-SQLITE-WAL](https://sqlite.org/wal.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SHIP.02.02 · staging 복구·스키마 갱신·실패 롤백을 검증한다

**상위:** CS-SHIP.02 / CS-SHIP · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** integrity/foreign key/receipt/reservation/outbox/lineage를 검사하고 성공 후 전환한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SHIP.02.json](basis/v3/tasks/CS-SHIP.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
staging 복구·스키마 갱신·실패 롤백을 검증한다

## 입력·출력 인터페이스
BackupManifest {cut,dbHash,blobHashes,schema,format,scope}; 삭제는 별도 명시된 운영정책에 따른다.

**직접 담당 요구:** 없음 — 아래 지원 요구를 위한 기반
**지원 요구:** REQ-001
**부모 제품 시험:** AT-001

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| MODIFY | `Assets/ChooGuard/Persistence/BackupService.cs` | CS-SHIP.02.01 |
| CREATE | `Assets/ChooGuard/Persistence/SchemaUpgrade.cs` | CS-SHIP.02.02 |
| MODIFY | `packaging/restore-policy.json` | CS-SHIP.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0202Tests.cs`
**시험 매체:** EDIT_MODE, PLAYER_ACCEPTANCE, FILE_SYSTEM_RECOVERY
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SHIP.02.01 | `OUT-CS-SHIP.02.01@candidate` | candidate | ALWAYS |
| CS-SHIP.01.02 | `OUT-CS-SHIP.01.02@candidate` | integration | ALWAYS |
| CS-LAB.01.02 | `OUT-CS-LAB.01.02@candidate` | integration | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. integrity/foreign key/receipt/reservation/outbox/lineage를 검사하고 성공 후 전환한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SHIP.02.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 staging 복구·스키마 갱신·실패 롤백을 검증한다의 유효 조건
When: staging 복구·스키마 갱신·실패 롤백을 검증한다를 실행한다
Then: 신규 버전 migration 실패도 원본 백업과 현재 DB를 보존한다.

### AC-CS-SHIP.02.02-N · NEGATIVE · NOT_RUN
Given: 초기화 이전 코드 호환을 요구하거나 손상 DB 위에 그대로 덮어쓰지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 초기화 이전 코드 호환을 요구하거나 손상 DB 위에 그대로 덮어쓰지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0202Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SHIP.02.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/11-release-and-backup.md](basis/v3/specs/11-release-and-backup.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [AUD-SQLITE-BACKUP](https://sqlite.org/backup.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [AUD-SQLITE-WAL](https://sqlite.org/wal.html) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SHIP.03.01 · 새 현장·기관·사건 패키지를 독립 등록한다

**상위:** CS-SHIP.03 / CS-SHIP · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 새 ID·기하·규칙·모델 scope와 지원 콘텐츠를 기록한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SHIP.03.json](basis/v3/tasks/CS-SHIP.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
새 현장·기관·사건 패키지를 독립 등록한다

## 입력·출력 인터페이스
SiteRegistry+ReleaseScope; 물리·기관 수용은 그 범위의 별도 근거로 결속한다.

**직접 담당 요구:** REQ-075, REQ-088, REQ-091
**지원 요구:** REQ-075, REQ-088, REQ-091
**부모 제품 시험:** AT-075, AT-088, AT-091

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `content/releases/site-registry.json` | CS-SHIP.03.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0301Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.04.02 | `OUT-CS-PACK.04.02@candidate` | candidate | ALWAYS |
| CS-MODES.03.03 | `OUT-CS-MODES.03.03@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 새 ID·기하·규칙·모델 scope와 지원 콘텐츠를 기록한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SHIP.03.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 새 현장·기관·사건 패키지를 독립 등록한다의 유효 조건
When: 새 현장·기관·사건 패키지를 독립 등록한다를 실행한다
Then: 과거 구역수·ID 없이 새 범위가 정의되고 자격을 별도 검토한다.

### AC-CS-SHIP.03.01-N · NEGATIVE · NOT_RUN
Given: 기존 현장 정확도/승인을 새 장소로 자동 상속하지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 기존 현장 정확도/승인을 새 장소로 자동 상속하지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSSHIP0301Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SHIP.03.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/11-release-and-backup.md](basis/v3/specs/11-release-and-backup.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-02](https://www.mois.go.kr/frt/bbs/type010/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000008&nttId=117612) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-04](https://www.srail.or.kr/cms/article/view.do?pageId=KR0502000000&postNo=1361) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-07](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-08](https://www.conducttr.com/ai-assistance) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-09](https://www.xvrsim.com/en/platform/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-10](https://www.dcc.edu/workforce-development/maritime/virtual-reality-training.aspx) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님


---

# CS-SHIP.03.02 · 유지비·데이터 책임·사용 한계를 인계한다

**상위:** CS-SHIP.03 / CS-SHIP · **작업창:** LATER · **상태:** NOT_STARTED / NOT_RUN

**Goal:** 설정/콘텐츠와 코어 변경 비용·업데이트 책임·관심/파일럿/구매를 구분한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-SHIP.03.json](basis/v3/tasks/CS-SHIP.03.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
유지비·데이터 책임·사용 한계를 인계한다

## 입력·출력 인터페이스
SiteRegistry+ReleaseScope; 물리·기관 수용은 그 범위의 별도 근거로 결속한다.

**직접 담당 요구:** REQ-075, REQ-088, REQ-091
**지원 요구:** REQ-075, REQ-088, REQ-091
**부모 제품 시험:** AT-075, AT-088, AT-091

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `packaging/operations-handbook.md` | CS-SHIP.03.02 |
| CREATE | `research/adoption/maintenance-ledger.json` | CS-SHIP.03.02 |

**이 story 전용 시험:** `research/checks/CS-SHIP.03.02.json`
**시험 매체:** DOCUMENT_REVIEW
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-SHIP.03.01 | `OUT-CS-SHIP.03.01@candidate` | candidate | ALWAYS |
| CS-SHIP.02.02 | `OUT-CS-SHIP.02.02@candidate` | integration | ALWAYS |
| CS-PACK.01.03 | `OUT-CS-PACK.01.03@candidate` | integration | ALWAYS |
| CS-MODES.03.03 | `OUT-CS-MODES.03.03@candidate` | integration | ALWAYS |
| CS-PROOF.04.02 | `OUT-CS-PROOF.04.02@candidate` | qualification | CLAIM_FIELD_USE |
| CS-SHIP.02.02 | `OUT-CS-SHIP.02.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. 설정/콘텐츠와 코어 변경 비용·업데이트 책임·관심/파일럿/구매를 구분한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-SHIP.03.02-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 유지비·데이터 책임·사용 한계를 인계한다의 유효 조건
When: 유지비·데이터 책임·사용 한계를 인계한다를 실행한다
Then: 실제 사용범위·유지비·복구·미지원 현상 설명이 인계된다.

### AC-CS-SHIP.03.02-N · NEGATIVE · NOT_RUN
Given: 학부 개발 전체를 구매계약 확보로 막거나 유지보수 비용을 무료 에셋에 숨기지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 학부 개발 전체를 구매계약 확보로 막거나 유지보수 비용을 무료 에셋에 숨기지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 research/checks/CS-SHIP.03.02.json에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-SHIP.03.02 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](GLOBAL_CONTRACT.md)
- [basis/v3/specs/11-release-and-backup.md](basis/v3/specs/11-release-and-backup.md)
- [basis/v3/specs/12-evidence-and-performance.md](basis/v3/specs/12-evidence-and-performance.md)
- [DISC-02](https://www.mois.go.kr/frt/bbs/type010/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000008&nttId=117612) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-04](https://www.srail.or.kr/cms/article/view.do?pageId=KR0502000000&postNo=1361) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-07](https://preptoolkit.fema.gov/web/exercise) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-08](https://www.conducttr.com/ai-assistance) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-09](https://www.xvrsim.com/en/platform/) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [DISC-10](https://www.dcc.edu/workforce-development/maritime/virtual-reality-training.aspx) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
