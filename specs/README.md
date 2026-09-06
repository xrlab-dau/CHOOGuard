# 변경 단위 명세 카탈로그 (GitHub Spec Kit)

- 기준: 백로그 v1.3 실행 순서, 요구사항 발견 v1 §8
- 규칙: 로컬 Spec Kit `create-new-feature.sh`는 명세 디렉터리와 feature 상태를 생성하며 Git 브랜치를 만들지 않는다. 이번 명세 디렉터리는 직접 작성했다. Git 브랜치는 별도로 `feature/<unit>` 규칙을 따른다. `SPECIFY_FEATURE_DIRECTORY`는 후속 프롬프트의 대상 지정에 사용하며 실행 승인이나 프롬프트 실행 완료의 증거가 아니다.
- 명세 ID: 각 spec의 `FR-00n`, `SC-00n`은 로컬 ID다. 기준선 ID는 "기준선 FR-0n"으로 쓴다.
- 증거: `docs/evidence/<unit>/`. 검토 판정: `docs/reviews/`.

## 작성된 초안

1차 검토는 changes_required이며 일부 필수 관점 실패로 전체 승인 상태는 cannot_proceed다. 문서 수정 후 독립 재검토는 미완료다. 체크리스트의 자체 점검을 구현·승인·M0 완료로 해석하지 않는다.

| 순서 | 단위 | 명세 | 브랜치 | 백로그 | 추적 | 상태 |
|---|---|---|---|---|---|---|
| 1 | M0-00 | `001-m0-00-safe-bootstrap/` | `feature/m0-00-safe-bootstrap` | §4 M0-00 | 파이프라인 §6~9, AIN-01~06 | Draft, 1차 지적 반영·독립 재검토 미완료 |
| 2 | M0-01 | `002-m0-01-v4-baseline/` | `feature/m0-01-v4-baseline` | §4 M0-01 | 아키텍처 §13~14, 기준선 §6~8 | Draft, 1차 지적 반영·독립 재검토 미완료 |
| 3 | M0-02a | `003-m0-02a-korail-filming-questions/` | `feature/m0-02a-filming-questions` | §4 M0-02a | MAP-01~03, O-02~05 | Draft, 1차 지적 반영·독립 재검토 미완료 |
| 4 | M0-03 | `004-m0-03-ai-tool-allowlist/` | `feature/m0-03-ai-tool-allowlist` | §4 M0-03 | MAP-07, O-04~05, AIN-01~06 | Draft, 1차 지적 반영·독립 재검토 미완료 |

## 착수 시 생성 (백로그 실행 순서)

| 순서 | 단위 | 예정 디렉터리 | 예정 브랜치 | 선행 | 주 실행 위치 |
|---|---|---|---|---|---|
| 5 | M1-02 Permission Gate와 실행 경계 | `005-m1-02-permission-gate/` | `feature/m1-02-permission-gate` | M0-00, M0-03 | local, school-pc |
| 6 | M1-03 Pi MCP 어댑터 공급망·우회 경로 검토 | `006-m1-03-mcp-adapter-review/` | `feature/m1-03-mcp-adapter-review` | M1-02 | local |
| 7 | M1-05 실행 기록·정제·공개 manifest | `007-m1-05-run-manifest/` | `feature/m1-05-run-manifest` | M1-02 | local |
| 8 | M1-01 Pi 버전·세션 계약·Writer Lease 기준선 | `008-m1-01-session-contract/` | `feature/m1-01-session-contract` | M0-00, M0-03 | school-pc |
| 9 | M1-06 Agent Team 세션·Writer Lease 검증 | `009-m1-06-writer-lease-test/` | `feature/m1-06-writer-lease-test` | M1-01 | school-pc |
| 10 | M1-07 외부 검증기·독립 리뷰 경로 | `010-m1-07-independent-review/` | `feature/m1-07-independent-review` | M1-02 | local |
| 11 | M2-01 Unity 프로젝트·패키지 기준선 | `011-m2-01-unity-project/` | `feature/m2-01-unity-project` | M1-01 | school-pc |
| 12 | M1-04 Unity MCP v10.2.0 연결 | `012-m1-04-unity-mcp/` | `feature/m1-04-unity-mcp` | M1-03, M2-01 | school-pc |
| 13 | M2-02 공통 TrainingAction 계약 | `013-m2-02-training-action/` | `feature/m2-02-training-action` | M2-01 | school-pc |
| 14 | M4-01 임시 5직무·판정 fixture 정의 | `014-m4-01-role-fixtures/` | `feature/m4-01-role-fixtures` | M2-02 | local, school-pc |
| 15 | M2-03 Quest·Feedback 역할 연동 | `015-m2-03-quest-feedback/` | `feature/m2-03-quest-feedback` | M2-02, M4-01 | school-pc |
| 16 | M2-04 가상 팀 상태 Provider | `016-m2-04-virtual-team/` | `feature/m2-04-virtual-team` | M2-03 | school-pc |
| — | M0-02b, M3-01~04, M4-02~04 | 착수 시 부여 | — | KORAIL 승인·선행 단위 | school-pc |

M1-04의 순서는 백로그 실행 순서(M1-02 → M1-03 → M1-05 → M1-01 → M1-06 → M1-07 → M2-01 → M2-02 → M4-01 → M2-03 → M2-04)에는 없으나 M2-01 이후 Unity Editor가 있어야 연결 가능하므로 M2-01 뒤에 둔다. 이 배치는 analysis 보고서 F-05에서 다룬다.

## 명세 → 브랜치 → PR 규칙

1. 명세 디렉터리를 만들고 `spec.md`, `checklists/requirements.md`를 작성한다.
2. 목표는 `plan-review` 독립 검토와 정제 판정 보존이다. 현 YAML은 운영 보류이므로 보호 변경 승인·실패 차단 시험 전 실행하지 않는다. 별도 검토 경로도 ADR 0003의 불변 대상·실제 제공자·스키마 검증을 충족해야 한다.
3. `feature/<unit>` 브랜치에서 구현한다. PR 제목은 Conventional Commit, 본문에 명세 경로와 백로그 ID를 적는다.
4. develop 병합은 squash, 승인 1 + CODEOWNER + 필수 검사 통과. 작성자 자기 승인은 불가하다.
