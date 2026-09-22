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
