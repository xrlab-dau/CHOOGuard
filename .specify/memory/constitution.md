# choo guard Constitution

<!--
Sync Impact Report
- Version: (template) → 1.0.0
- 근거 문서: AGENTS.md, docs/choo-guard-ai-native-pipeline-v1.md v1.2 (§2·§5·§8), docs/choo-guard-execution-backlog-v1.md v1.3 (§2 DoD),
  docs/choo-guard-requirements-baseline-v1.md v1.1 (§1·§8), docs/adr/0001-agentic-unity-development.md
- 추가된 원칙: I~VII 전부 신규
- 템플릿 정합성: .specify/templates/spec-template.md ✅, plan-template.md ✅ (Constitution Check 절에서 본 문서 §Ⅰ~Ⅶ 참조), tasks-template.md ✅
- 후속 TODO: 없음
-->

<!-- 2026-09-08 Sync Impact: 1.0.0 → 1.0.1. PM 지시에 따라 §VI Unity 공급자를 공식 CLI/Pipeline으로 변경. ADR 0006, AGENTS, 파이프라인, 백로그, SPEC 카탈로그·004를 함께 갱신. 템플릿의 특정 MCP 공급자 의존 없음. 설치/실행 수용은 별도. -->

## Core Principles

### I. 요구사항 기준선이 권위다 (NON-NEGOTIABLE)

- 제품 범위의 권위는 `docs/choo-guard-requirements-baseline-v1.md`(v1.1)의 확정 요구(BR/FR/MAP/RUN)와 P0·P1 수용 기준이다. 명세·계획·코드는 이 ID를 인용해야 하며, ID 없는 기능 요구는 미결정으로 등재한다.
- KORAIL 회신 전 미결정(O-01~O-12)은 팀이 임의로 확정하지 않는다. 명세에서 미결정을 전제해야 하면 `[NEEDS CLARIFICATION: O-nn]` 마커로 남기고 가정으로 분리한다.
- 확정 요구를 바꾸려면 변경 사유, 대체안, 승인자, 영향을 받는 테스트·검토·산출물 해시를 기록하고 사용자(PM) 승인을 받는다. 구현 편의를 위한 요구 축소는 금지다.

### II. Git이 진실이고 변경은 작업 단위로 묶인다

- Git에 추적되는 C#, 테스트, 선언적 시나리오 데이터, Editor 빌더, SceneBundle manifest, 문서가 진실이다. 기록되지 않은 Unity Editor 상태나 채팅 결과는 근거가 아니다.
- 모든 변경은 백로그 작업 ID(M0-00 …) 하나에 대응하는 명세(`specs/NNN-<unit>/spec.md`) → 계획 → 작업 목록 → PR 순서로 진행한다. 한 PR은 한 작업 단위만 담는다.
- 브랜치는 git flow(`feature/`, `release/`, `hotfix/`)를 따르고 `main`·`develop` 직접 push는 없다. Spec Kit의 번호 브랜치 자동 생성은 사용하지 않는다.
- `.unity`·`.prefab`·`.asset`·`.meta` YAML은 손으로 편집하지 않는다. 멱등 Editor 빌더와 프로젝트 범위 MCP 도구를 우선한다.

### III. 테스트와 증거 없이는 완료가 아니다

- 테스트를 먼저 쓰고 실제로 실행한다. 실행하지 않은 테스트, 재현 불가능한 성능 수치, 스크린샷 없는 UI 변경은 완료 증거가 되지 않는다.
- 완료 정의는 백로그 §2의 12개 DoD 항목 전부다. 특히: 문서·ADR·검증 ID 추적, 책임자 1명, 허용 경로, 산출물 해시, 실행 기록, 라이선스·민감자료·외부 전송 점검, 대상 manifest 고정.
- 검토 대상 해시가 바뀌면 영향 테스트와 독립 검토를 다시 수행한다.

### IV. 독립 검토는 다른 제공자·다른 모델·읽기 전용이다

- 작성 에이전트(anthropic 계열)와 다른 모델 제공자(openai-codex 계열)가 읽기 전용 세션에서 검토한다. 이 검토는 생략할 수 없으며 병합 게이트(G3)의 전제다.
- 검토는 분리된 Pi 자식 세션(`.pi/workflows/adversarial-review.yaml`)에서 병렬로 수행하고, 판정은 `approved | changes_required | cannot_proceed` 로 기록한다. 반대가 남아 있으면 완료를 차단한다.
- 리뷰 판정과 최종 승인 영수증은 같은 대상 manifest 해시를 참조한다.

### V. 데이터 등급과 외부 전송 경계는 코드보다 우선한다 (NON-NEGOTIABLE)

- 원본 촬영 자료, 얼굴·차량번호·민감 표지, 모델 가중치, 자격 정보, `.ulf`, `.env`, 제한 시설의 PLY/SPZ/GLB, Unity 빌드 비밀은 Git·일반 CI·외부 LLM/MCP/에셋 API로 보내지 않는다.
- 외부 모델·검색·MCP 서버에는 `PUBLIC_SYNTHETIC` 등급 자료만 보낸다. 각 도구의 네트워크 목적지를 문서에 열거하고 변경 시 갱신한다.
- `execute_code`, 외부 에셋 생성, 원격 패키지 설치는 사람이 명시적으로 승인하기 전까지 비활성이다.
- LLM 출력은 철도 절차·물리 안전의 권위가 아니다. 채점 규칙, 안전 절차, 좌표 변환, 충돌 경계는 테스트와 리드 검토 없이 바꾸지 않는다.

### VI. 도구는 고정 버전·검토된 소스·명시된 우위로만 채택한다

- Pi 확장·npm·Python 패키지는 정확한 버전과 해시를 기록하고 설치 전에 소스를 검토한다(설치 스크립트, 네트워크 목적지, 환경 변수, 자식 프로세스).
- 도구 교체는 파이프라인 v1 §2 SOTA 기준(의미 있는 우위, 라이선스·보안 검토, 이행 계획, 재현 가능한 비교)을 만족하고 ADR로 남긴다. 제한·유료 도구는 사전 승인이 필요하다.
- 채택된 기준선: BMAD Method(요구 발견), GitHub Spec Kit(요구 품질), Pi + pi-subagents + pi-agents(코딩·그래프 오케스트레이션), Pydantic AI Harness Researcher + Exa(기술 조사), 공식 Unity CLI Editor MCP + com.unity.pipeline(Unity 저작; 2026-09-08 PM 결정, ADR 0006).

### VII. 사람 승인 게이트와 한 명의 책임자

- 병합, 릴리스, 삭제, 권한·보안 설정 변경, 보호 경로(`.github/`, `scripts/ci/`, `docs/adr/`, `.pi/settings.json`) 변경, 제한 라이선스 사용은 사전 사람 승인이 필요하다.
- 작업 단위마다 책임자 1명을 두고, 에이전트는 그 사람의 세션과 worktree 안에서만 쓴다. Unity Editor 하나에는 쓰기 에이전트 하나만 붙는다.
- 게이트 G0(요구 확정)~G4(릴리스)의 승인자는 `docs/choo-guard-ai-native-pipeline-v1.md` §5 를 따른다.

## 명세 작성 규칙 (Spec Kit 적용)

- `specs/NNN-<unit>/spec.md` 는 백로그 ID, 요구사항 ID, 검증 ID, 책임자, 허용 경로, 비목표를 필수로 적는다.
- 명확성·완전성·일관성 체크리스트(`checklists/`)는 명세와 같은 디렉터리에 두고, `[NEEDS CLARIFICATION]` 마커가 0개가 되기 전에는 계획(`plan.md`)으로 넘어가지 않는다. 단, KORAIL 미결정에 의존하는 마커는 가정으로 전환하고 O-ID를 남기면 넘어갈 수 있다.
- `speckit.analyze` 교차 검사는 spec·plan·tasks 사이의 ID 불일치를 P1로 다룬다.
- 문서 언어는 한국어, 코드 식별자와 도구 이름은 원문을 유지한다.

## 실행 환경 규칙

- 코딩 에이전트는 Pi 0.85.0 이상, 프로젝트 신뢰(`~/.pi/agent/trust.json`) 이후에만 프로젝트 확장을 로드한다.
- 작성 에이전트는 사람별 worktree에서, 리뷰어는 읽기 전용 도구(`read, grep, find, ls`)로, 검증기는 편집 도구 없이 실행한다.
- 실행 기록(작업 ID, 담당자, 모델, 브랜치, 변경 파일, 테스트, 해시)은 `docs/reviews/` 와 PR 본문에 남긴다.

## Governance

- 본 헌법은 AGENTS.md와 파이프라인 v1을 요약·구속하는 문서이며, 충돌 시 AGENTS.md → 파이프라인 v1 → 본 헌법 순으로 우선한다. 충돌을 발견하면 본 헌법을 개정한다.
- 개정은 PM(엄윤상) 승인과 ADR 또는 변경 이력 기록이 필요하다. MAJOR: 원칙 삭제·재정의, MINOR: 원칙·절 추가, PATCH: 문구 정정.
- 모든 PR 리뷰는 §I~§VII 준수 여부를 확인한다. 복잡성 추가는 명세의 Complexity Tracking 절에 근거를 남겨야 한다.

**Version**: 1.0.1 | **Ratified**: 2026-09-06 | **Last Amended**: 2026-09-08
