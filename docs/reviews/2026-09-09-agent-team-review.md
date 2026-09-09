# 2026-09-09 독립 세션 팀 리뷰

사용자가 요청한 별도 세션 적대적 리뷰와 재현·수정을 수행했다. 이 기록은 작성자와 같은 OpenAI 제공자의 보조 검토이며, R-07의 다른 제공자 필수 게이트를 승인하지 않는다. Fal은 사용하지 않았다.

## 고정 대상과 분리 방식

- PR #97 검토 소스: `f783255908d9d3cf66a110e01f17c9deb6c6efd2`; base `e7a6bb7a7fff0c301c04f93aeea8850dbde34779`.
- 대상 manifest: [45개 파일과 SHA-256](../evidence/R-07-REVIEW/20260909/target-pr97-final-20260909.json), SHA-256 `c5a715fc5ba8b43c45f1ea03449440c2021fb30a71cfb36feb39a5767a6c04df`, 미커밋 대상 `{}`.
- 최종 spec/adversarial/safety 리뷰는 각각 `fork_turns=none`으로 생성한 새 세션이다. 같은 소스·manifest를 직접 읽고 시작·종료 해시와 clean 상태를 확인하도록 했다. 이전 판정을 승인 근거로 사용하지 않는다.
- 리뷰 중 소스 worktree를 고정하고 이 보고서·CI 결과·집계는 별도 worktree에서 작성했다. 파일시스템은 공유되므로 읽기 전용은 작업 지시와 해시 감사이며 별도 sandbox 강제가 아니다. 정확한 자식 모델 식별자가 노출되지 않으면 `not_exposed`로 기록한다.

## 발견 사항과 수정

| 대상 | 발견 사항 | 처리 |
|---|---|---|
| `3b0dd51` | evidence 링크를 통해 정책 경로에 새 파일 생성 가능 | 입력 자체의 evidence 경계, 부모 검사, 쓰기 직전 재검사와 배타적 생성 |
| `3b0dd51` | 순회 제외 트리를 가리키는 내부 별칭을 검사한 것으로 오인 | 실제 순회한 정규 경로에 연결됐는지 검사 |
| `3b0dd51` | wrapper가 최종 정책 검사 전에 도구 호출 | 도구/설치 앞에 정적 정책 preflight 추가 |
| `3b0dd51` | 무제한·비객체 package metadata가 예외 또는 임의 출력 유발 | 일반 파일·크기·객체·버전 형식 검사, 실패 영수증에 임의 metadata 제외 |
| `f76b55f` | 빈 영수증/후보 경로가 미지정으로 처리됨 | 빈 값 선행 거부와 None 기반 옵션 존재 검사, 두 wrapper 회귀 |
| `f76b55f` | wrapper에 남은 직접 Node metadata 출력 | 직접 읽기 제거, 마지막 제한된 검증기로 일원화 |
| 동시 게시 `39e5dc2` | 도구 뒤 작업본 변화와 Windows 콜론 경로 보강 | 해당 재순회·경로 차단·13개 회귀 및 기존 run06을 보존해 통합 |

첫 적대 관점은 예비 관찰 후 도구의 cybersecurity 위험 판정으로 중단되어 최종 JSON을 받지 못했다. [실패 기록](../evidence/R-07-REVIEW/20260909/fresh-adversarial-failure.json)은 reviewer 승인이 아닌 orchestrator 기록이다. 예비 관찰 두 건은 writer가 별도로 재현·수정했다. 두 번째 대상의 적대 관점도 [최종 판정 누락](../evidence/R-07-REVIEW/20260909/fixed-adversarial-missing.json)을 보존했다. 과거 누락이나 실패를 이후 세션의 승인으로 덮어쓰지 않는다.

[처음 원 판정](../evidence/R-07-REVIEW/20260909/initial-summary.json), [후속 spec 지적](../evidence/R-07-REVIEW/20260909/fixed-spec.json), [후속 safety 지적](../evidence/R-07-REVIEW/20260909/fixed-safety.json), [run05](../evidence/R-07/run-20260909-05.json), [동시 세션 run06](../evidence/R-07/run-20260909-06.json), [통합 run07](../evidence/R-07/run-20260909-07.json)은 대상과 당시 상태를 각각 보존한다. RED 실패 수·subtest 수·실행 중단 로그를 같은 수치로 취급하지 않는다.

## 최종 세 관점 결과

| 세션 | 원 판정 | 지적 |
|---|---|---:|
| `/root/final_spec_review` | approved (명세 범위) | 0 |
| `/root/final_adversarial_review` | changes_required | P2 2건 |
| `/root/final_safety_review` | changes_required | P2 2건 |

[결정적 집계와 원본](../evidence/R-07-REVIEW/20260909/final-summary.json)은 필수 스키마·세 관점·head·manifest·시작/종료 45개 해시 일치를 모델 밖 Python에서 확인했다. 집계는 **changes_required**다.

writer가 네 건을 재현·보완했다. 도구 이후 Git 추적 목록 재조회와 조회 실패 차단, 마지막 OS 호출 뒤 시간 예산 검사, `.env.<이름>`·추적 금지 파일·정책 목록에서 금지 이름의 증거 노출 차단, Bash `uv sync` 실패 상태 전달을 수정했다. [run08](../evidence/R-07/run-20260909-08.json)에 RED 8개 실패 및 수정 후 로컬 135개 중 115개 통과·20개 제외를 결속했다.

이것은 **검토 후 writer 수정**이다. 세 번째 보조 검토에서 남은 지적을 수정했다고 원 판정을 승인으로 갱신하지 않으며 추가 승인 라운드도 자동으로 시작하지 않는다. 최종 수정 소스의 새 세 관점 판정은 미완료이고 다른 제공자 게이트도 `cannot_proceed`다.

## 검토 대상 f783255의 실행 증거

[GitHub Actions 실행 34297451434](https://github.com/xrlab-dau/CHOOGuard/actions/runs/34297451434)의 세 job이 성공했다. 체크아웃한 PR merge commit `3ea329b9b1eb14b019a3d5dda3aef222964ddd3b`의 tree는 검토 소스와 같은 `851aba40f5d5e1894ad38b43653a5578525648e8`임을 Git commit API로 확인했다. 실제 develop 병합은 하지 않았다.

| 환경 | Bootstrap 통과 | 제외 | 실패 | Profile 통과 |
|---|---:|---:|---:|---:|
| 로컬 Linux | 107 | 20 | 0 | 이번 통합 실행에서 별도 재실행 안 함 |
| GitHub Ubuntu 24.04 | 108 | 19 | 0 | 9 |
| GitHub Windows 2022 | 112 | 15 | 0 | 9 |

Windows는 PowerShell 회귀 17개와 native junction 2개를 포함한다. 제외 항목은 통과가 아니다. [최종 CI 기록과 정제 로그](../evidence/R-07-REVIEW/20260909/final-ci.json)에 job ID와 해시를 연결했다. [정책 후보07](../evidence/R-07/policy-candidate-20260909-07.json)은 실제 정책 소유자의 승인·외부 핀이 없는 후보이며 `policy_approval_pending`이다. 기존 후보와 실행 기록을 소급 승인하지 않는다.

## 검토 후 수정의 실행 증거

writer 수정은 로컬 135개 중 115개 통과·20개 제외다. 새 커밋과 실제 native CI는 후속 실행 기록에 별도로 연결하며, 위 f783255의 결과를 새 소스로 소급하지 않는다.

## 별도 PR #98 그래프

변경 없는 `61309b3f83457d9b7e89de2ca409b18358bebe61`을 새 graph 세션이 직접 검토했다. [원 판정](../evidence/R-07-REVIEW/20260909/fresh-graph.json)은 범위 내 추가 지적 없이 종료됐다. 29개 노드·57개 의존 관계, Mermaid 10개 간선과 백로그/candidate가 일치하며 Context 17개·Foundation 31개 시험 및 부정 입력 5개를 확인했다. 이 정적 graph 승인은 R-07의 필수 세 관점이나 다른 제공자의 판정을 대신하지 않는다.

## 남은 수용 조건

GitHub Actions 임시 토큰과 고정 Copilot CLI 1.0.83로 Anthropic 모델 인증을 시도했으나 [실행 34294249798](https://github.com/xrlab-dau/CHOOGuard/actions/runs/34294249798)이 `Access denied by policy settings`로 종료됐다. 소스나 리뷰 프롬프트를 모델에 제출하지 못했고 모델 판정도 없다. [preflight 증거](../evidence/R-07-REVIEW/20260909/copilot-preflight-20260909.json)를 보존한다. 앞선 secret 목록·조직 billing 조회는 자동 승인 검토에서 거부되어 중단했으며 이후 해당 조회를 반복하거나 정책을 변경하지 않았다.

다른 제공자의 실제 spec/adversarial/safety 판정 수는 **0**이다. ADR-0003의 제공자 불일치 규칙에 따라 정식 게이트는 `cannot_proceed`다. 이 보조 검토로 R-01의 소진된 라운드를 재개하거나 R-07의 독립 제공자 예산을 승인으로 바꾸지 않는다. 조직에서 허용된 다른 제공자 실행 경로가 필요하다.

실제 정책 경로·핀 승인, 원본 105키 저널의 복구 또는 검증 불가에 대한 명시적 사람 수용, 학교 PC·Unity/HMD·현장 수용은 여전히 남는다. 로컬 도구 설치 이력과 현재 실행 상태를 혼동하지 않는다. 이번 Python/wrapper 검사는 새로운 Unity·Blender 실행을 필요로 하지 않았으며 실제 Windows runner에서 변경된 PowerShell을 검증했다. 두 PR은 draft, #52는 Review로 유지하고 이슈를 닫지 않는다.
