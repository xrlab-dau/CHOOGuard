# choo guard GitHub 거버넌스 감사 v1.1

- 관측일: 2026-09-06. 실행 당시 조회 결과이며 이후 변경을 실시간 반영하지 않는다.
- 대상: 프로젝트 저장소의 Git Flow, 팀 접근 상태, rulesets, CI, CODEOWNERS.
- 방법: GitHub CLI의 저장소·rulesets·팀 구성·PR·이슈 조회와 추적 파일 정적 검토.
- 공개 범위: 역할·비식별 상태만 기록한다. 개인 로그인과 역할 매핑은 GitHub 권한 설정 또는 저장소 밖 TEAM_INTERNAL 기록에 둔다.
- 상태: 1차 검토 changes_required. 아래 권고를 적용하거나 권한을 변경하지 않았다. 문서 정정 후 독립 재검토 미완료.

## 1. 관측 상태

| 항목 | 값 |
|---|---|
| 기본 브랜치 | `develop` |
| 공통 보호 규칙 | `Protected branches · main + develop`, active |
| develop 규칙 | `develop · squash-only linear history`, active. 승인 1건, CODEOWNER 검토, 필수 검사 `Policy, security and repository hygiene` |
| main 규칙 | `main · merge-commit releases only`, active |
| 병합 설정 | squash·merge commit·rebase 허용, auto-merge 비활성, 병합 후 브랜치 자동 삭제 비활성 |
| 보안 기능 | secret scanning·push protection·Dependabot security updates 비활성으로 관측 |
| 팀 접근 | 멤버 4명 관측. 초대 대기 1명은 이전 세션 관측이며 수락 여부 재확인 필요 |
| 열린 작업 | Dependabot PR #1~#5, 기준선 등록 이슈 #10. 관련 PR #11은 병합됨 |
| 브랜치 | main·develop 외 잔류 `main-1`과 Dependabot 브랜치가 있음 |
| CI | nightly-gpu, pr-triage, reconstruction-ci, release, required-quality-gate, unity-build, unity-tests |
| CODEOWNERS | 전역 팀 규칙과 일부 리드 규칙이 있음. 에이전트 정책·증거 경로의 명시적 리드 규칙은 불완전 |

## 2. 발견 및 미실행 권고

| ID | 심각도 | 발견 | 권고와 완료 조건 | 결정 역할 |
|---|---|---|---|---|
| G-01 | HIGH | `.pi/`, `.specify/memory/`, `scripts/ci/`, `docs/evidence/`에 명시적 리드 규칙이 없다. 전역 팀 규칙만으로 정책 소유권을 보장하지 못한다. | PM이 보호 범위·리드 역할을 승인한 뒤 사람이 변경한다. 마지막 일치 규칙으로 각 보호 경로를 대조한다. 명세 001 FR-004는 보류다. | PM, 저장소 관리자 |
| G-02 | HIGH | 서버 측 비밀 탐지·push protection이 비활성이다. | 조직·요금제의 가용성을 확인하고 사람이 활성화 여부를 결정한다. 현행 정책 검사는 일부 비밀 패턴·경로·확장자·Unity·Actions도 검사하지만 모든 자격·자료등급을 검증하지 않는다. | PM, 저장소 관리자 |
| G-03 | MEDIUM | 저장소의 rebase 허용과 브랜치별 병합 방식이 다르게 보인다. | 브랜치 ruleset의 실효 동작을 검증하고 rebase 비활성 여부를 결정한다. main용 merge commit을 일괄 비활성화하지 않는다. | PM |
| G-04 | MEDIUM | 병합 후 브랜치가 자동 삭제되지 않는다. | 보존 정책을 먼저 정한다. 자동 삭제 설정 변경은 별도 승인이다. | PM |
| G-05 | MEDIUM | Dependabot PR과 보안 업데이트 설정 검토가 남았다. | 각 변경·필수 검사·리드 승인을 확인한다. 일괄 병합하지 않는다. | PM, 팀 검토자 |
| G-06 | LOW | 기준선 이슈가 관련 PR 병합 후에도 열려 있다. | 이슈 수용 기준과 잔여 작업을 확인한 뒤 사람이 종료 여부를 정한다. | PM |
| G-07 | LOW | 잔류 브랜치가 있다. | 고유 커밋과 사용 여부를 확인한다. 승인 없이 삭제하지 않는다. | PM |
| G-08 | MEDIUM | 로그인과 실제 승인 역할의 매핑이 확정되지 않았다. | 개인 매핑은 비공개로 유지한다. 공개 제공자 기록에는 작성·검토·조사와 자료 접근·모델 전송·공개 및 병합 승인 역할만 기록한다. | PM, 각 역할 책임자 |
| G-09 | MEDIUM | 초대 수락 여부가 미확인이다. | GitHub에서 본인 접근을 확인하고 공개 문서에는 비식별 완료 상태만 남긴다. | 초대 대상 팀원 |
| G-10 | LOW | 감사 당시 저장소 secrets·variables·milestones·projects가 없었다. | 현재 개발 준비에 자격 등록을 요구하지 않는다. 향후 필요한 경우 저장 위치·접근·잔류·전송 경계를 먼저 승인한다. | PM |
| G-11 | MEDIUM | PR 작성자는 자기 PR을 승인할 수 없다. | 독립 팀 검토자가 필요하다. 브랜치 clone은 문서 검토용이며 승인 우회가 아니다. 실행은 검토·필수 검사·PM 승인이 확인된 불변 커밋으로 제한한다. | PM, 독립 팀 검토자 |

## 3. 재확인과 승인 경계

아래는 저장소 루트에서 사람이 실행할 읽기 전용 조회다. 인증된 세션 출력에 개인 정보가 있을 수 있으므로 원문을 공개 파일에 복사하지 않는다.

```bash
gh repo view --json defaultBranchRef,mergeCommitAllowed,rebaseMergeAllowed,squashMergeAllowed
gh api repos/{owner}/{repo}/rulesets --jq '.[] | {name,enforcement}'
gh pr list --state open
gh issue list --state open
```

보호 경로·승인 역할·GitHub 설정·초대·이슈 종료·브랜치 삭제·병합은 이 감사와 푸시 요청만으로 승인되지 않는다. 에이전트의 문서 수정도 사람의 승인 영수증이 아니다.

## 변경 이력

| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-09-06 | 최초 감사 |
| 1.1 | 2026-09-06 | 개인 로그인 제거, 정적 정책 검사 범위 정정, clone 승인 우회 제거, 미실행 권고와 관측 상태 분리 |
