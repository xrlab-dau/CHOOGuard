# Feature Specification: M0-00 사람 주도 안전 부트스트랩

**Feature Branch**: `feature/m0-00-safe-bootstrap`

**Created**: 2026-09-06

**Status**: Draft (1차 검토 changes_required, 승인·실행 경계 검증 및 독립 재검토 미완료)

**Input**: 백로그 v1.3 §4 M0-00, 파이프라인 v1.2 §6~9, 요구사항 발견 v1 §2 Q7·Q8, AIN-01~06

**Data Class**: PUBLIC_SYNTHETIC. 이 명세와 산출물은 실제 역사 자료·경로·호스트명을 담지 않는다.

**ID 규칙**: 이 문서의 `FR-00n`, `SC-00n`은 변경 단위 로컬 ID다. 기준선의 `FR-0n`은 항상 "기준선 FR-0n"으로 쓴다.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 비민감 작업공간 확인 (Priority: P1)

PM은 로컬(계획 머신)과 학교 PC(Unity 워크스테이션) 각각에서 저장소 clone이 PUBLIC_SYNTHETIC 자료만 담고 있고, 실제 역사 자료나 KORAIL 경로가 마운트·링크되지 않았음을 확인하고 기록한다.

**Why this priority**: 이 확인이 끝나기 전에는 Pi·MCP를 어떤 자료에도 연결할 수 없다 (백로그 M0-00 DoD 3).

**Independent Test**: 두 머신에서 FR-001~003의 위생 검사·입력 분류·링크·mount/junction 점검을 각각 수행한다. 대상 manifest·범위·실패·사람 확인이 연결된 새 영수증이 모두 있어야 검증된다. 현 검사기 실행 또는 파일 존재만으로 통과하지 않는다.

**Acceptance Scenarios**:

1. **Given** 검토 대상으로 고정한 커밋과 작업공간 manifest, **When** 사람이 승인된 실행기로 저장소 위생 검사를 수행하고 FR-001의 분류 manifest를 대조하면, **Then** 위생 검사 종료 코드가 0이고 미분류 입력이 없다. 브랜치 이름이나 검사 성공만으로 자료 접근·설치·모델 실행을 승인하지 않는다.
2. **Given** 같은 작업공간의 추적·비추적·ignored 입력, **When** 사람이 FR-002의 심볼릭 링크·mount·junction 범위와 조사 실패를 확인하면, **Then** 비허용 연결과 미검증 항목이 없다. 범위·수·결과를 새로운 실행별 영수증으로 보존하며 기존 파일을 덮어쓰지 않는다.
3. **Given** 기존 자격 파일과 촬영 원본이 입력될 수 있는 작업공간, **When** 사람이 FR-003의 대소문자 비구분 경로 검사와 입력 분류를 수행하면, **Then** 비밀파일·라이선스·키·가중치·재구성 자산·촬영 원본이 공개 추적 대상에 없고 실제 자격은 모델 입력에서 분리돼 있다. `.env` 값은 읽지 않으며 추적되지 않았다는 사실만으로 분류를 통과하지 않는다.

---

### User Story 2 - 보호 경로 소유권과 쓰기 권한 (Priority: P2)

PM은 정책 파일·보호 경로·증거 저장소의 소유자와 쓰기 권한을 GitHub CODEOWNERS와 규칙에 맞춰 기록한다.

**Why this priority**: 에이전트가 정책을 스스로 바꾸는 경로를 막아야 이후 통제가 의미를 가진다 (AIN-02).

**Independent Test**: 보호 경로마다 명시적 CODEOWNERS 패턴이 있고 마지막 일치 소유자가 PM이 승인한 리드 역할인지 검사한다. 전역 팀 규칙만으로는 실패다. develop의 CODEOWNER 요구와 별도 로컬 쓰기 경계도 구분한다.

**Acceptance Scenarios**:

1. **Given** 보호 경로 목록(AGENTS.md, `.pi/`, `.specify/memory/`, `scripts/ci/`, `.github/`, `docs/evidence/`), **When** 실제 경로의 마지막 일치 CODEOWNERS 규칙을 검사하면, **Then** 각각 명시적 패턴과 PM 승인 리드 역할이 일치한다. 전역 `*`만 일치하거나 승인 역할이 미정이면 실패한다.
2. **Given** GitHub develop 규칙, **When** `gh api`로 규칙을 조회하면, **Then** 승인 1건·CODEOWNER 검토·필수 검사 "Policy, security and repository hygiene"가 켜져 있다.

---

### User Story 3 - 검토 제공자·승인 주체·기준 해시 기록 (Priority: P3)

PM은 초기 독립 검토 제공자·모델, 승인 주체 3종(자료 접근, 모델 전송, 공개·병합), 정책 파일 기준 해시를 기록하고 수동 서명한다.

**Why this priority**: M0-03과 M1-01이 이 기록을 입력으로 쓴다.

**Independent Test**: 보완·검토된 단일 생성기로 필수 대상 manifest를 두 번 생성해 동일한지 확인한다. 공개 서명에는 승인 역할·날짜·전체 커밋 SHA·확인 FR과 비공개 원본의 비식별 참조를 기록한다. 개인 이름·로그인 매핑은 공개하지 않는다.

**Acceptance Scenarios**:

1. **Given** Pi 프로파일, **When** 작성·검토의 선언 모델을 비교하면, **Then** 서로 다른 제공자 조합을 기록한다. 이 정적 비교가 실제 호출의 제공자 분리 증거는 아니다.
2. **Given** FR-006 필수 정책 집합, **When** 보완·검토된 생성기를 두 번 실행하면, **Then** 정렬된 상대 경로·SHA-256이 일치하고 누락 파일은 실패한다. 확정 기록은 새 파일로 저장한다.
3. **Given** 실제 사람 검사·승인 증거, **When** PM이 확인 FR·날짜·커밋·역할을 서명하면, **Then** 확인된 M0-00 범위만 완료한다. M0-03·M1 실행 경계의 별도 선행 조건을 대신하지 않는다.

### Edge Cases

- 학교 PC에 이전 clone과 오래된 `.pi/npm/`이 남아 있으면 자동 부트스트랩·설치·갱신을 실행하지 않는다. 사람이 고정 대상·공급망·실행 승인을 먼저 대조한다. 승인된 검증기를 사용할 수 있을 때만 새 실행별 영수증을 추가하며 이전 파일은 이동하거나 덮어쓰지 않는다.
- 외부 드라이브를 가리키는 심볼릭 링크가 발견된 경우: 링크를 제거하기 전에 PM이 대상 등급을 확인하고 기록한다. 자동 삭제하지 않는다.
- 로컬 `.env`에 EXA_API_KEY가 있는 경우: 추적되지 않음을 확인하고 값은 어떤 산출물에도 복사하지 않는다.
- 학교 PC 호스트명·사용자명은 TEAM_INTERNAL로 취급해 영수증에는 `machine-label`(local, school-pc)만 적는다.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 두 머신(local, school-pc) 각각에서 `python3 scripts/ci/repository_policy.py`가 종료 코드 0이어야 한다. 이는 현행 저장소 위생 검사만 의미한다. 별도로 PM이 추적 파일 전체와 작업공간 입력을 분류 manifest와 대조해 공개 코드·문서 또는 PUBLIC_SYNTHETIC만 포함하는지 확인한다. 미분류 파일이 있으면 후속 연결을 금지한다. 증거에는 대상 커밋·파일 수·분류 manifest 해시·미분류 수·승인 역할을 남기며 민감 원문·경로는 쓰지 않는다.
- **FR-002**: 추적·비추적·ignored를 포함한 에이전트 가시 작업공간에서 외부 심볼릭 링크, Windows junction/reparse point, 저장소 아래 추가 mount를 조사한다. `.git` 내부는 검사에서 제외하되 `.pi/npm`과 `.venv`라는 이름만으로 링크·mount를 검사에서 누락하지 않는다. 도구 가상환경은 에이전트 입력과 분리된 별도 승인 실행 프로파일로 제한한다. POSIX `mount`, Windows `Get-Volume` 및 `Get-ChildItem -Force -Recurse`의 ReparsePoint 결과를 사람이 환경별로 대조한다. 접근 거부·해석 실패는 미검증이며 통과가 아니다. 현행 Python 검사기는 mount·junction 전체를 증명하지 못하므로 보완 시험과 사람 확인 없이는 FR-002가 완료되지 않는다. 공개 영수증에는 조사 범위·수·결과만 기록한다.
- **FR-003**: `git ls-files -z`와 에이전트 가시 작업공간을 검사한다. `.env` 및 `.env.*` 비밀파일, 라이선스·키·가중치, PLY/SPZ/GLB, 촬영 원본 MP4/MOV/MXF/ARW/CR2/NEF/DNG/RAW는 대소문자와 무관하게 공개 추적 대상에서 제외한다. 값이 없는 `.env.example`만 템플릿 예외이며 내용 검토가 필요하다. ignored는 안전 분류가 아니다. 기존 로컬 자격 파일은 모델 입력에서 분리하고 내용을 읽거나 자동 삭제하지 않는다. 현행 정책 검사기가 이 전체 범위를 검사한다는 주장은 하지 않는다.
- **FR-004**: 보호 경로 `AGENTS.md`, `.pi/`, `.specify/memory/`, `scripts/ci/`, `.github/`, `docs/evidence/` 각각에 명시적 CODEOWNERS 패턴이 있어야 한다. 마지막 일치 규칙의 소유자가 PM이 승인한 리드 역할이어야 하며 전역 `*` 팀 매칭만으로는 실패다. [NEEDS CLARIFICATION: 누락된 네 경로의 리드 배정과 CODEOWNERS 변경에 대한 PM 승인 기록이 없다. 이 명세·푸시 요청을 권한 변경 승인으로 간주하지 않는다. 승인 전에는 FR-004와 후속 실행 게이트를 보류한다.] `docs/adr/`, `docs/research/`, `tools/research/`의 추가 보호 범위도 같은 승인 검토에서 판단한다. 로그인과 실명 매핑은 공개 문서에 기록하지 않는다.
- **FR-005**: `docs/evidence/M0-00/review-providers.md`에 작성 제공자·모델, 검토 제공자·모델, 조사 제공자·모델, 승인 주체 3종(자료 접근, 모델 전송, 공개·병합)의 담당자 역할을 기록한다.
- **FR-006**: `docs/evidence/M0-00/baseline.sha256`의 대상 집합은 검토·수정된 `verify_toolchain.py`의 `POLICY_GLOBS`를 단일 출처로 사용한다. 필수 범위는 AGENTS.md, Pi settings·agents·workflows·prompts, constitution, CI·bootstrap 스크립트, CODEOWNERS, GitHub workflow yml/yaml, ADR, 조사 하네스와 추적된 의존성 manifest·lockfile이다. M0-03은 이 집합에 허용목록 정책 문서를 추가한다. 필수 파일 누락·읽기 실패는 검증 실패다. 현행 생성기의 누락을 수정·시험하기 전 해시 결과는 불완전한 관측값이다. 정렬된 상대 경로와 SHA-256을 두 번 생성해 비교하며 파일 변경 뒤 이전 승인은 무효다.
- **FR-007**: M0-00은 사람의 정적 검사 단계다. npm 패키지가 설정에 선언됐다는 사실과 실행·자동 설치 승인은 구분한다. 현재 packages 선언을 검사하되 이 단계에서 Pi를 실행·신뢰하거나 설치하지 않는다. M0-03 공급망·무결성·라이선스·목적지 승인과 M1-02 실행 경계 시험 뒤에만 승인 프로파일의 고정 패키지를 사용한다. 비허용 패키지·MCP·원격 실행·`execute_code`는 계속 금지다. 설정 키 검사만으로 호스트 확장·하위 프로세스 통제를 증명하지 않는다.
- **FR-008**: 사람이 `docs/evidence/M0-00/signoff.md` 또는 새 실행별 서명 파일에 승인 역할·날짜·전체 커밋 SHA·확인한 항목(FR-001~007) 목록과 비공개 서명 원본의 비식별 참조를 적는다. 실제 PM 이름·개인 로그인 매핑은 저장소 밖에 보관한다. 에이전트가 사람의 승인이나 서명을 생성하지 않는다.
- **FR-009**: 이 변경 단위는 에이전트를 사용하지 않는다. 스크립트 실행과 기록은 사람이 수행한다. 에이전트가 만든 초안이 있으면 "초안"으로 표시하고 사람이 검증한다.

### Key Entities

- **증거 영수증**: 명령, 종료 코드, 출력 요약, 대상 파일 해시, machine-label, 날짜를 담은 JSON 또는 Markdown. `docs/evidence/<unit>/`에 저장.
- **보호 경로**: 사람 승인 없이 에이전트가 수정할 수 없는 경로 집합. CODEOWNERS와 세션 계약이 동시에 참조.
- **machine-label**: `local`, `school-pc`. 호스트명 대신 쓰는 공개 라벨.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 두 머신 모두 위생 검사 종료 코드 0과 FR-001~003의 입력 분류·링크·mount/junction 사람 확인이 완료됐다. 미분류·접근 실패·해석 실패가 남으면 통과하지 않는다.
- **SC-002**: 최소 보호 경로 6개 각각의 명시적 패턴과 마지막 일치 소유자가 PM 승인 리드 역할과 일치한다. 전역 팀 규칙만으로는 실패다.
- **SC-003**: FR-006 전체 필수 집합의 정렬된 상대 경로·해시를 두 번 생성해 일치한다. 누락·읽기 실패·대상 변경 fixture는 실패한다.
- **SC-004**: M1 착수 전 M0-00의 실제 사람 서명과 각 단위의 별도 선행 게이트가 확인됐다. 서명 파일 존재만으로 다른 승인·실행 검증을 대체하지 않는다.

## Applicable Definition of Done

백로그 §2 중 1, 2, 3, 4, 6, 7, 8, 11, 12가 적용된다. DoD 12는 정책·세션·검토 대상 해시 변경 후 영향 검증과 독립 재검토를 요구하므로 제품 코드가 없어도 적용한다. 확정 영수증은 append-only로 보존하며 새 실행은 새 파일을 사용한다. 기존 파일 또는 증거 디렉터리 밖 경로를 덮어쓸 수 있는 현행 `--write`는 수정·회귀 시험 전 사용하지 않는다.

## Assumptions

- 학교 PC OS는 Windows로 가정한다. 명령 예시의 `python3`는 local 표기이며 학교 PC는 관리자가 확인한 `python` 또는 `py -3` 실행기에 같은 인수를 전달한다. 해당 Python이 없으면 미검증이다. sh/ps1 파일 제공은 설치·실행 정책 해결이나 native 실패 처리의 검증이 아니다.
- 저장소가 유일한 세팅 운반체다 (A-07).
- 독립 검토 조합은 작성 `anthropic/*`, 검토 `openai-codex/*`다 (A-08). M0-03에서 바꿀 수 있다.
