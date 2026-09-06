# R-03 보호 경로·GitHub 거버넌스 변경안 검토

- 날짜: 2026-09-06.
- 작업 단위: R-03 (이슈 #43). 상위 단위 M0-00 (#13).
- 유형: 읽기 전용 재확인 관측과 변경안이다. **이 문서는 어떤 GitHub 설정도 변경하지 않았고 어떤 변경 승인도 생성하지 않는다.**
- 기준 커밋: `6798032c839336f2c059a81db12a60602e731eae` (develop).
- 작성 제공자: anthropic. 다른 제공자의 독립 재검토는 미완료다.
- 공개 범위: 역할·비식별 상태만 기록한다. 개인 로그인·호스트명·민감 경로는 넣지 않는다.

## 0. 불변 대상 manifest

이 검토는 저장소 파일 3개와 **조회 시점의 GitHub 서버 설정**을 대상으로 한다. 서버 설정은 해시로 고정할 수 없으므로 조회 시각과 식별자를 기록한다.

| SHA-256 | 경로 |
|---|---|
| `5c4e40e09a7b84f41ab3cdbfa754a5c343cf2083eb4690c8f6ec952abce1ddcb` | `docs/choo-guard-github-governance-audit-v1.md` |
| `a6d085e0296e538ed8b34ea34cb73e87efd6c5e385303f8700e8ccddabaa3ec2` | `.github/CODEOWNERS` |
| `4bb8b4bbc846677963b4f391ef02f100d77e372f3a9bdc2580a36fb324a86413` | `scripts/bootstrap/verify_toolchain.py` (`POLICY_GLOBS`) |

| 해시 없는 대상 | 고정 방식 |
|---|---|
| GitHub ruleset `22267761`, `22268345`, `22268552` | 2026-09-06 조회. 서버 상태는 가변이며 **적용 직전 재조회가 필요하다** |
| 저장소 설정·보안 기능·브랜치 목록·팀 인원 | 2026-09-06 조회. 같음 |

## 1. G-01~G-11 재확인

읽기 전용 조회만 사용했다. 감사 v1.1의 관측과 2026-09-06 재조회를 대조한다.

| ID | 재확인 결과 | 판정 |
|---|---|---|
| G-01 | `.github/CODEOWNERS`에 `.pi/`·`.specify/memory/`·`scripts/ci/`·`docs/evidence/` 규칙이 여전히 없다. 리드 규칙 8건은 모두 다른 경로다 | **미해결.** §2 변경안 |
| G-02 | `secret_scanning`·`push_protection`·`non_provider_patterns`·`validity_checks`·`dependabot_security_updates` 5개 모두 `disabled`. **저장소는 public이다** | **가용성 확인 완료, 결정만 남음.** §3.1 |
| G-03 | 원인이 감사 서술과 다르다. §4 참조 | **원인 정정 필요** |
| G-04 | `delete_branch_on_merge: false` | 미해결. §3.3 |
| G-05 | Dependabot PR #1~#5 열림. `dependabot_security_updates: disabled` | 미해결. §3.4 |
| G-06 | 이슈 #10(M0-01) 열림 | 미해결이나 **의도된 상태.** R-01(#41)이 링크·fragment·독립 검토 미완료를 확인했다 |
| G-07 | `main-1`에 고유 커밋 1개(`c3db2e9 Fix README title and formatting`). 관련 PR #6은 병합이 아니라 **CLOSED**다 | **사실 확인 완료.** 폐기된 변경이며 삭제 여부는 PM 결정. §3.5 |
| G-08 | 리드 규칙 8건이 팀이 아닌 개인 로그인 1개를 가리키며 이는 public 저장소에 공개돼 있다 | **부분.** §2.2 |
| G-09 | 팀 멤버 4명, 초대 대기 1건 | 미해결. 감사 시점과 변화 없음 |
| G-10 | Actions secrets 0건, variables 0건 | **확인 완료, 변화 없음** |
| G-11 | `pull_request` 규칙에 승인 1건·CODEOWNER 검토가 있다. 다만 §4.1의 bypass actor가 이를 무력화할 수 있다 | **규칙은 유효하나 조건부.** §4.1 |

## 2. 보호 경로·리드 소유권 변경안

### 2.1 POLICY_GLOBS와 CODEOWNERS의 범위 불일치

`verify_toolchain.py`의 `POLICY_GLOBS`(15개 패턴)는 해시로 감시하는 정책 파일 목록이고, `CODEOWNERS`의 리드 규칙(8개 경로)은 변경 승인 주체다. 두 목록이 상당히 어긋난다.

**POLICY_GLOBS에 있으나 리드 규칙이 없는 경로**

| 경로 | 성격 | 현재 승인 주체 |
|---|---|---|
| `.pi/settings.json`, `.pi/workflows.json`, `.pi/agents/*.md`, `.pi/workflows/*.yaml`, `.pi/prompts/*.md` | 에이전트 실행 정책 | 전역 팀 규칙만 |
| `.specify/memory/constitution.md` | 헌법 | 전역 팀 규칙만 |
| `docs/adr/*.md` | 승인된 결정 기록 | 전역 팀 규칙만 |
| `scripts/ci/*.py` | **정책 검사기 자신** | 전역 팀 규칙만 |
| `scripts/bootstrap/*` | 부트스트랩·검증기 | 전역 팀 규칙만 |
| `tools/research/pyproject.toml`, `tools/research/uv.lock` | 공급망 lock | 전역 팀 규칙만 |

`scripts/ci/*.py`가 특히 문제다. 필수 검사 `Policy, security and repository hygiene`이 이 스크립트를 실행하므로, 리드 승인 없이 검사기 자신을 완화하는 변경이 통과할 수 있다.

`docs/evidence/`(`RECEIPT_ROOT`)는 **POLICY_GLOBS에도 CODEOWNERS 리드 규칙에도 없다.** 감사 G-01이 지목한 경로다.

**리드 규칙이 있으나 POLICY_GLOBS에 없는 경로:** `/SECURITY.md`, `/Packages/`, `/ProjectSettings/`, `/schemas/`, `/Assets/ChooGuard/Runtime/Scoring/`, `/Assets/ChooGuard/Runtime/Safety/`.

### 2.2 변경안

**변경안 A — 리드 규칙 추가.** `CODEOWNERS` 끝에 아래를 덧붙인다. CODEOWNERS는 **마지막 일치 규칙**이 이기므로 반드시 파일 끝에 둔다.

```
# 정책·증거 경로. POLICY_GLOBS와 범위를 맞춘다.
/.pi/                    <리드>
/.specify/memory/        <리드>
/docs/adr/               <리드>
/docs/evidence/          <리드>
/docs/reviews/           <리드>
/scripts/ci/             <리드>
/scripts/bootstrap/      <리드>
/tools/research/pyproject.toml  <리드>
/tools/research/uv.lock         <리드>
```

**변경안 B — 개인 로그인을 팀으로 교체(G-08).** 리드 규칙 8건이 개인 로그인을 public 파일에 노출한다. 전용 팀(예: `@xrlab-dau/choo-guard-lead`)을 만들어 교체하면 G-08의 공개 매핑 문제가 해소되고 인원 변동에도 규칙이 유지된다. 팀 생성·구성원 지정은 별도 승인이다.

**변경안 C — POLICY_GLOBS에 `docs/evidence/` 포함 여부 결정.** 증거 영수증은 append-only 생성물이라 해시 감시 대상이 되면 매 실행마다 정책 해시가 바뀐다. **포함하지 않기를 제안**하고, 대신 CODEOWNERS 리드 규칙으로만 쓰기를 통제한다.

### 2.3 적용 전 반드시 확인할 제약

**보호 대상 경로 상당수가 아직 저장소에 없다.** CODEOWNERS 규칙은 추적되는 파일에만 작동하므로 아래는 지금 규칙을 넣어도 무효다.

| 경로 | 작업본 | 추적 파일 |
|---|---|---|
| `.pi/`, `.specify/`, `docs/evidence/` | 존재 | **0개 (미추적)** |
| `Packages/`, `ProjectSettings/`, `Assets/` | 없음 | 0개 |
| `docs/adr/`, `scripts/ci/`, `scripts/bootstrap/`, `tools/research/` | 존재 | 5·4·4·4개 |

`.pi/`·`.specify/`·`docs/evidence/`는 PM 승인 대기로 의도적으로 미추적 상태다. 이 경로들의 리드 규칙은 **선제적 등록**이며 실제 효력은 커밋 시점부터다. 규칙 추가만으로 "보호됨"이라고 주장하지 않는다.

## 3. 개별 검토 항목

각 항목은 개별 결정이며 일괄 적용하지 않는다.

### 3.1 비밀 탐지·push protection (G-02)

- 관측: 5개 기능 전부 `disabled`. 저장소는 **public**이다.
- 감사의 "조직·요금제 가용성 확인" 조건은 해소됐다. public 저장소에서 secret scanning과 push protection은 추가 비용 없이 사용할 수 있다.
- 위험: 이 저장소는 public이며 프로젝트가 KORAIL 자료·자격증명 유출을 명시적 위험으로 다룬다. 현행 `scripts/ci/repository_policy.py`는 **커밋된 뒤에** 일부 패턴을 잡는 사후 검사이며, push protection처럼 푸시 자체를 막지 못한다.
- **제안: push protection 포함 활성화.** 결정 주체는 PM·저장소 관리자. 설정 변경은 별도 승인이며 이 문서로 실행하지 않는다.

### 3.2 병합 방식 (G-03)

§4.2 참조. 저장소 수준 `allow_rebase_merge: true`는 보호 브랜치에서는 ruleset이 우선하므로 실효가 없다. 명확성을 위해 비활성화를 제안하되 **main용 merge commit은 유지**한다(감사 권고와 동일).

### 3.3 브랜치 보존 (G-04)

- 관측: `delete_branch_on_merge: false`.
- 자동 삭제는 병합된 브랜치만 지운다. ruleset의 `deletion` 규칙이 main·develop을 보호하므로 기본 브랜치는 영향받지 않는다.
- **결정 선행 사항:** 증거·검토 추적이 브랜치 존재에 의존하는지 먼저 확인해야 한다. R-01 기록은 커밋 해시로 대상을 고정하므로 브랜치 삭제에 영향받지 않는다. 다만 작업 그래프 §3.2가 "한 노드 한 브랜치 한 PR"을 요구하므로 **보존 기간 정책을 먼저 정한 뒤** 자동 삭제를 결정하기를 제안한다.

### 3.4 Dependabot PR (G-05)

- 관측: PR #1~#5 열림(actions/cache, github-script, upload-artifact, checkout, setup-python). `dependabot_security_updates: disabled`.
- 다섯 건 모두 GitHub Actions 메이저 버전 상향이다. `.github/workflows/`는 리드 소유이고 `POLICY_GLOBS`의 해시 대상이다.
- **일괄 병합하지 않는다.** 각 PR은 (a) 변경된 액션의 major 호환성, (b) 필수 검사 통과, (c) 리드 승인을 개별 확인한다.
- `dependabot_security_updates` 활성화는 §3.1과 별개 결정이다. 활성화 시 보안 PR이 자동 생성되나 **자동 병합은 되지 않는다**(`allow_auto_merge: false`).

### 3.5 잔류 브랜치 (G-07)

- `main-1`은 고유 커밋 `c3db2e9`(README 제목·서식) 1개를 갖고 있고, 관련 PR #6은 **병합되지 않고 닫혔다.**
- 해당 내용은 이후 PR #8(`docs(readme): update project branding`)로 대체됐을 가능성이 높으나 **바이트 단위 대조는 하지 않았다.**
- **제안: 삭제 전 `c3db2e9`의 내용이 현재 README에 반영됐는지 사람이 확인한다.** 확인 뒤 삭제는 별도 승인이다. Dependabot 브랜치 5개는 해당 PR 처리에 따라간다.

## 4. 새 발견

감사 v1.1에 없던 항목이다. G-12~G-14로 부여한다.

### 4.1 G-12 (HIGH) — 보호 ruleset에 bypass actor가 있다

- 관측: ruleset `22267761`(`Protected branches · main + develop`)에 `actor_type=User`, `bypass_mode=pull_request` bypass actor 1건이 등록돼 있다. 나머지 두 ruleset의 bypass 목록은 비어 있다.
- 의미: 이 actor는 PR 경로에서 해당 ruleset의 규칙을 우회할 수 있다. 그 ruleset이 승인 1건·CODEOWNER 검토·필수 검사를 담고 있는 유일한 규칙이다.
- **G-11과 직접 충돌한다.** 감사 G-11은 "PR 작성자는 자기 PR을 승인할 수 없고 독립 팀 검토자가 필요하다"고 적었으나, bypass actor는 독립 검토 없이 자기 PR을 병합할 수 있다. 명세 001 FR-004의 리드 소유권 검사와 ADR 0003의 독립 검토 요구도 같은 이유로 서버 측에서 강제되지 않는다.
- **제안: bypass actor 제거 또는 `bypass_mode`를 명시적으로 축소.** 단, 팀 멤버가 4명이고 초대 1건이 대기 중이므로 **제거 시 긴급 복구 경로가 사라진다.** 제거 전에 (a) 독립 검토 가능한 인원이 실제로 확보됐는지, (b) 복구 절차를 어떻게 둘지 함께 결정해야 한다. 신원은 이 공개 문서에 기록하지 않는다.

### 4.2 G-13 (MEDIUM) — ruleset 이름과 실제 규칙이 다르고 main에 중복 규칙이 있다

- ruleset `22268345`의 이름은 `develop · squash-only linear history`이지만 **실제 규칙은 `deletion`·`non_fast_forward`·`required_linear_history` 셋뿐이고 병합 방식 규칙이 없다.**
- develop의 병합 방식은 ruleset `22267761`의 `allowed_merge_methods: ["merge","squash"]`에서 온다. develop이 사실상 squash 전용이 되는 것은 `required_linear_history`가 merge commit을 막기 때문이며, **명시적 설정이 아니라 두 ruleset의 상호작용 결과다.** `required_linear_history`가 빠지면 merge commit이 즉시 허용된다.
- main에는 `pull_request` 규칙이 **두 개** 걸려 있다. `22267761`은 승인 1건·CODEOWNER 검토·`["merge","squash"]`, `22268552`는 승인 0건·CODEOWNER 검토 없음·`["merge"]`다. GitHub는 가장 제한적인 값을 적용하므로 실효는 승인 1건·CODEOWNER 검토·merge 전용이지만, **설정만 읽으면 main이 무승인으로 보인다.**
- **제안: 규칙을 이름과 일치시킨다.** develop ruleset에 `allowed_merge_methods: ["squash"]`를 명시하고, main ruleset의 중복 `pull_request` 규칙을 정리한다. 실효 동작을 바꾸지 않는 정리이지만 **설정 변경이므로 별도 승인이다.**

### 4.3 G-14 (MEDIUM) — 경로 필터 워크플로는 필수 검사가 될 수 없다

- 필수 검사는 `Policy, security and repository hygiene` 1개이며 `required-quality-gate.yml`의 `policy` job과 정확히 일치한다. 이 연결은 **정상이다.**
- `Unity Tests`·`Reconstruction CI`는 `paths` 필터가 있어 문서 PR에서는 실행되지 않는다. 필수 검사로 지정하면 문서 PR이 영구 대기한다. Unity·재구성 코드가 들어올 때 **경로별 필수 검사 또는 skip job 패턴**을 별도로 설계해야 한다. 지금 변경할 사항은 아니다.
- `PR Risk Triage`는 `pull_request_target`으로 동작하며 필수 검사가 아니다. 현 상태를 유지한다.

## 5. PM 결정 요청 목록

| 항목 | 결정 | 근거 절 |
|---|---|---|
| CODEOWNERS 리드 규칙 추가 (변경안 A) | | §2.2 |
| 개인 로그인 → 팀 교체 (변경안 B) | | §2.2 |
| `docs/evidence/`를 POLICY_GLOBS에서 제외 (변경안 C) | | §2.2 |
| secret scanning·push protection 활성화 | | §3.1 |
| `allow_rebase_merge` 비활성화 | | §3.2 |
| 병합 후 브랜치 자동 삭제 | | §3.3 |
| Dependabot PR #1~#5 개별 처리, security updates 활성화 | | §3.4 |
| `main-1` 삭제 | | §3.5 |
| **bypass actor 제거 여부와 복구 경로** | | §4.1 |
| ruleset 이름·규칙 정합화 | | §4.2 |

## 6. 잔여 게이트

- 이 문서는 GitHub 설정·권한·CODEOWNERS를 **변경하지 않았다.** 조회는 모두 읽기 전용이었다.
- 승인된 변경은 별도 PR로 수행하고, 마지막 일치 CODEOWNERS 검사·필수 CI·독립 팀 검토 증거로 확인한다. 문서 승인이 설정 변경 승인을 대신하지 않는다.
- 개인 로그인과 역할 매핑은 비공개로 확인한다. 확인 전 타인의 공개 Assignee를 추측하지 않았다.
- §0.1의 GitHub 서버 설정은 가변이므로 **적용 직전 재조회**가 필요하다. 이 문서의 관측은 2026-09-06 시점이다.
- 작성 제공자 anthropic과 다른 제공자의 독립 재검토가 미완료다.

## 변경 이력

| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-09-06 | 최초 작성. G-01~G-11 재확인, G-12~G-14 신규 발견, 변경안 A~C |
