# Feature Specification: M0-01 v3 폐기와 v4 기준선 등록

**Feature Branch**: `feature/m0-01-v4-baseline`

**Created**: 2026-09-06

**Status**: Draft (1차 검토 지적 반영 중, 독립 재검토 미완료)

**Input**: 백로그 v1.3 §4 M0-01, 아키텍처 v4.2 §13~14, 기준선 v1.1 §6~8, 요구사항 발견 v1

**Data Class**: PUBLIC_SYNTHETIC

**ID 규칙**: `FR-00n`, `SC-00n`은 이 변경 단위 로컬 ID다.

## User Scenarios & Testing

### User Story 1 - 기준선 카탈로그의 버전·링크 일치 (Priority: P1)

검토자는 루트 README의 기준선 카탈로그에서 요구사항 v1.1, 아키텍처 v4.2, 파이프라인 v1.2, 백로그 v1.3을 연다. 기존 기준선 파일끼리 완전한 상호참조가 있다고 가정하지 않는다.

**Why this priority**: 변경 단위가 참조하는 권위 문서와 버전을 검증 가능하게 고정한다.

**Independent Test**: README 카탈로그의 실제 Markdown 링크 목적지가 존재하고 각 문서의 제목·버전과 일치하는지 검사한다.

**Acceptance Scenarios**:

1. **Given** 루트 README의 기준선 카탈로그, **When** 링크 네 개를 열면, **Then** 아래 FR-001의 정확한 파일과 버전에 도달한다.
2. **Given** 검토 대상 Markdown 파일 집합, **When** 실제 상대 Markdown 링크를 검사하면, **Then** 깨진 링크가 없다. 코드 블록의 명령 인수, glob, 아직 생성 전이라고 명시한 산출물 예시는 파일 존재 검사 대상이 아니다.

### User Story 2 - v3 폐기 고지 (Priority: P2)

저장소에는 v3 파일이 확인되지 않았다. 외부 보관 위치를 추측하거나 공개하지 않고 루트 README 첫 20줄에 v3 폐기와 현행 기준선 링크를 둔다.

**Independent Test**: README 첫 20줄에 폐기 고지와 현행 기준선 네 링크가 있는지 검사한다.

**Acceptance Scenarios**:

1. **Given** v3 파일이 없는 현재 저장소, **When** README를 읽으면, **Then** 저장소 내 v3 파일 미존재와 v3가 현행 개발 기준이 아니라는 사실을 알 수 있다.
2. **Given** 이후 v3 파일이 발견되는 경우, **When** 사람이 변경을 제안하면, **Then** 해당 파일의 고지는 별도 검토를 거쳐 추가한다. 미확인 외부 경로는 기록하지 않는다.

### User Story 3 - 충돌·미결정 목록 추적 (Priority: P3)

PM은 발견 기록에서 기준선 §6 O-01~O-12와 §7 충돌 목록, 환경 D-01~D-09를 연결한다. 백로그에 아직 없는 절 번호 참조를 이미 존재한다고 주장하지 않는다.

**Independent Test**: 추적표에서 상위 목록의 모든 ID가 포함되고 각 ID에 결정 역할·필요 게이트가 존재하는지 확인한다. 미정 답변은 미정으로 유지한다.

## Requirements

### Functional Requirements

- **FR-001**: 루트 README에 아래 기준선 카탈로그를 두고 링크와 버전을 단일 진입점으로 검증한다. 기준선 네 문서의 본문 변경은 이 단위에서 요구하지 않는다.

  | 파일 | 버전 |
  |---|---|
  | `docs/choo-guard-requirements-baseline-v1.md` | v1.1 |
  | `docs/choo-guard-platform-architecture-v4.md` | v4.2 |
  | `docs/choo-guard-ai-native-pipeline-v1.md` | v1.2 |
  | `docs/choo-guard-execution-backlog-v1.md` | v1.3 |

- **FR-002**: README와 이 단위가 수정한 Markdown의 실제 상대 링크를 검사한다. 검사기 버전, 대상 목록·해시, 링크 수, 제외 사유, 실패 수, 종료 코드를 `docs/evidence/M0-01/link-check.json`에 남긴다. 본문 링크의 fragment는 대상 heading과도 대조한다. 실행 명령은 검사기 구현·검토 후 확정하며 미구현 상태를 PASS로 취급하지 않는다.
- **FR-003**: 저장소에 v3 문서가 없으므로 루트 README 첫 20줄에 “v3 폐기”, 저장소 내 파일 미존재, 현행 기준선 네 링크를 넣는다. 외부 v3 위치 확인은 이 고지의 선행 조건이 아니다.
- **FR-004**: README가 발견 기록 `_bmad-output/planning-artifacts/requirements-discovery-v1.md`와 `specs/README.md`도 링크해야 한다.
- **FR-005**: 기준선 네 파일과 README·발견 기록·명세·검사기의 해시를 대상 manifest에 포함한다. 기준선 네 SHA-256은 `docs/evidence/M0-01/baseline-docs.sha256`에도 정렬된 상대 경로로 저장한다.
- **FR-006**: 발견 기록에 기준선 §6 O-01~O-12 및 §7 충돌 목록의 명시적 참조를 두고, `docs/evidence/M0-01/decision-trace.md`에 O-01~O-12와 D-01~D-09의 결정 역할·필요 게이트·미결 상태를 기록한다. 기존 백로그의 인용 누락은 현재 사실로 기록하며 이를 이 단위가 이미 수정했다고 주장하지 않는다.
- **FR-007**: 검토 후 대상 파일·검사기·검사 결과 해시가 바뀌면 영향 링크·버전 검증과 독립 재검토를 다시 수행한다. 이전 승인 결과를 새 내용에 재사용하지 않는다.

### Key Entities

- **기준선 카탈로그**: 네 권위 문서의 상대 링크와 버전 목록.
- **링크 검사 영수증**: 검사기·대상 해시, 검사 범위·제외 사유, 실패 수·종료 코드.
- **결정 추적표**: ID, 결정 역할, 필요 게이트, 현재 상태. 실제 KORAIL 회신·개인 식별정보는 별도 보관.

## Success Criteria

- **SC-001**: FR-002의 실제 링크·fragment 검사 실패가 0건이다.
- **SC-002**: README 카탈로그의 네 목적지·버전이 FR-001 및 실제 파일과 모두 일치한다.
- **SC-003**: README 첫 20줄에 v3 폐기 고지와 현행 기준선 네 링크가 있다.
- **SC-004**: O 목록과 D 목록의 모든 ID에 결정 역할·필요 게이트가 있으며 미응답을 승인으로 바꾸지 않는다.
- **SC-005**: 발견 기록의 §6·§7 참조가 있고 FR-007의 대상 변경 사례는 재검증·재검토 없이는 승인되지 않는다.

## Applicable Definition of Done

백로그 §2 중 1, 2, 3, 4, 7, 8, 12가 적용된다. 특히 DoD 12는 문서 전용 변경에도 적용된다.

## Assumptions

- 기존 기준선 등록은 완료됐지만 README 카탈로그·폐기 고지·추적표·링크 검사·해시·독립 검토는 별도 완료 조건이다.
- 승인된 기준선의 내용과 버전은 유지한다. 검사기를 `scripts/ci/`에 추가한다면 보호 경로 승인부터 받는다.

## 검토 이력

| 날짜 | 변경 | 상태 |
|---|---|---|
| 2026-09-06 | 완전 상호참조 대신 README 카탈로그로 범위 고정, v3 고지 결정, 추적·링크 검사 범위·DoD 12 정정 | 명세 수정, 구현 및 독립 재검토 미완료 |
