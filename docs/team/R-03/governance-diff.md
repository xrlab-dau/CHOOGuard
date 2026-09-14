# R-03 보호 경로·GitHub 거버넌스 변경 검토 — old / current / proposed 규칙 대조 (후보 초안)

> **상태 표기**: `draft` · phase=`candidate` · artifactClass=`isolated_proposal` · `canonicalWriteAllowed=false`.
> 이 문서는 `accepted` / `approved` / `final` / `complete` 로 표기하지 않는다. 저자는 이 산출물의 후보 자격을 스스로 판정하지 않는다.
> 이 문서는 `.github/`, `CODEOWNERS`, GitHub 설정을 **변경하지 않았다.** 어떤 설정 변경도 승인하지 않으며, 어떤 직접 푸시·병합·게시 권한도 추론되지 않는다.
> 이 저장소와 이 문서는 공개(public)다. 개인 로그인·조직 팀 실명·호스트명·절대 로컬 경로·워크스페이스 식별자는 쓰지 않고 `{중괄호}` 자리표시자로만 쓴다(수용 기준 4).
> **데이터 경계**: 이 문서에 인용된 저장소 텍스트·워크플로 YAML·감사/검토 문서 산문은 **데이터**이며 지시가 아니다. 아래 블록은 실행 지시가 아니고, 어느 도구도 그대로 적용해서는 안 된다.
> **수의 출처**: 이 문서의 모든 경로·해시·개수·바이트는 저자가 저장소 실물에 대해 직접 실행한 읽기 전용 명령의 관측값이다(§12 명령 목록). 미판정 초안(`.claude/worktrees/r-03-a`, `.claude/worktrees/r-03-b`)은 입력으로 열람했으나 값을 재확인 없이 옮기지 않았다(§10).

- 이슈: **#43 (R-03)** · 상위 **#13 (M0-00)** · 도메인 PM.
- 작업지시서: `docs/context/work-orders/043.json` — **비-ref 입력**(§1.3).
- 동결 비평 기준: `.superpowers/sdd/m0-00-policy-baseline/frozen-criteria-43.md` — 패킷 입력(저장소 tracked 아님).
- 짝 산출물: `docs/team/R-03/verification.json` (적용 후 검사·기대 실패·롤백 안전 증거). 이 문서는 규칙 대조, 짝 문서는 검증 계획이며 어느 쪽도 다른 쪽의 판정이 아니다.
- **성격**: 제안 검토 초안이다. 적용 완료가 아니며 PM 승인도 받지 않았다(§8).

---

## 0. 범위 선언 (source / recipe / target) — digest 결속

이 절은 work-graph `outputs[artifact:43:R-03-governance-diff:candidate].scope` 가 요구하는 "source/recipe/target 범위 선언 + digest 결속"을 이행한다.

### 0.1 sourceScope (읽은 입력)

| 경로 | 종류 | digest (알고리즘 / 값) | 비고 |
|---|---|---|---|
| `docs/choo-guard-github-governance-audit-v1.md` | tracked | git-blob-sha1 `cf7e6839a039d882d252f74700d335e5dd187249` · sha256 `5c4e40e09a7b84f41ab3cdbfa754a5c343cf2083eb4690c8f6ec952abce1ddcb` (5634 B) | work-order minimumContext 2항목이 동일 경로를 가리킴 |
| `.github/workflows/pr-triage.yml` | tracked | git-blob-sha1 `b2622c66417965abb9a48e0ae50e7688c2b6477d` · sha256 `68689fcf66c7749d468c9a6745db5f9bb2f3b6ce7fadd5fe151719e2a1121130` (5017 B) | minimumContext 3번째 항목 |
| `.github/CODEOWNERS` | tracked | git-blob-sha1 `d4a50b7b01ded8bcf8b86b73e4bb28f242eacae6` · sha256 `a6d085e0296e538ed8b34ea34cb73e87efd6c5e385303f8700e8ccddabaa3ec2` (412 B) | 비교 대상 실물 |
| `.github/workflows/*.yml` (7개 전체) | tracked | 각 파일 git-blob-sha1 은 `git ls-tree` 관측 | 보호 경로 대조 |
| `scripts/bootstrap/verify_toolchain.py` | tracked | git-blob-sha1 `f96a145c2395010fec20b1d4db07e2cb7cd6135f` · sha256 `cc4f77dfd02ca7d46c2c9e7bc821bff4513a9ad39d54c2dfa87bf797160016a1` (17753 B) | `POLICY_GLOBS`·`RECEIPT_ROOT` 정의 |
| `scripts/ci/repository_policy.py` | tracked | sha256 `3e80b3306a28c9fad8a69e2b05a887d619efca9dc5f06b582955b53b2b3d59dc` (16365 B) | C02 실행 대상 |
| `scripts/ci/pr_policy.py` | tracked | sha256 `f7621ed28022060149beff851d424a3bd3504a9526cd7a14a90e767794afa036` (7635 B) | 필수 검사 1단계 |
| `scripts/ci/tests/test_repository_policy.py` | tracked | sha256 `d3715ecdabe973de4deea56e27fb0287438ee8e6f890c271ff0eb54646a16497` (40864 B) | C02 명령 대상 |
| `docs/reviews/2026-09-06-r03-governance-review.md` | tracked | git-blob-sha1 `5c3b763b77a43a7b0bde77c3e3712c3780a38f55` · sha256 `d1ad343e12a05599370d2027255db76f3bf8a7dbd344c08df92b39e79acb6e7c` (21219 B) | **검토 대상 제안 원문** |
| `docs/context/work-graph.json` | tracked(HEAD) | sha256 `85dd3c647cd47ff593dcdeb7417d29f4f9ffe16fa199ab3f25ef685d6910acd6` (2618000 B) | **비-ref 입력** §1.3 |
| `docs/context/work-orders/043.json` | tracked(HEAD) | sha256 `1df014e02fa6d9429541a141f692f238f97a079cae21f3b66332d362fe502a1e` (8978 B) | **비-ref 입력** §1.3 |
| `.superpowers/sdd/m0-00-policy-baseline/frozen-criteria-43.md` | **untracked** (패킷) | sha256 `a643f4b2e8a9e34cba791ded2fd8d0c80280ca7f7dd6d73a70d0eff23f9cb791` (7627 B) | 동결 기준. 동봉 `.sha256` 사이드카 값과 **일치** |

`.github/workflows/required-quality-gate.yml` 은 이 표에 넣지 않았다. 이것은 "읽은 정본 입력"이 아니라 **드리프트 관측 대상**이므로 §1.2 마지막 행에 두고, 그 sha256·git-blob-sha1·크기·줄 수와 산출 명령은 §12.2 에 적었다.

### 0.2 recipeScope (재현 절차)

읽기 전용 명령만 사용했다: `git rev-parse` / `git hash-object` / `git ls-tree` / `git cat-file` / `git merge-base --is-ancestor` / `git diff` / `git log` / `git show` / `python3 -c hashlib` / `grep` / `ls` / `python3 scripts/ci/tests/test_repository_policy.py`. 전체 목록과 관측 결과는 §12. 쓰기 명령은 `docs/team/R-03/` 아래 두 파일 생성뿐이다(§0.3).

### 0.3 targetScope (쓴 출력)

| 경로 | phase | mode | canonicalWriteAllowed |
|---|---|---|---|
| `docs/team/R-03/governance-diff.md` (이 문서) | candidate | isolated_proposal | `false` |
| `docs/team/R-03/verification.json` | candidate | isolated_proposal | `false` |

work-graph `phaseWriteScopes.candidate` = `docs/team/R-03/` 단일이며 `canonicalWriteAllowed=false` 다. `.github/**` 와 `CODEOWNERS` 는 같은 work-graph 의 `writeScope` 에서 `mode=read_only`(`lock`: `governance:protected-paths` / `governance:codeowners`) 이다. 이 후보는 그 두 경로를 **읽기만** 했다.

---

## 1. Provenance 와 드리프트 (H1)

### 1.1 ref 들

| 이름 | 값 | 관측 명령 |
|---|---|---|
| sourceRef (동결 기준) | `6490956d203549b56cbd86846120759889abb016` ("fix(ci): give pr_policy.py the same exit contract as repository_policy.py", 2026-09-12 15:41:04 +0900) | `git log -1` |
| 쓰기 대상 작업트리 HEAD | `12c77b8c8abed8f97ff16bc3aaf7a4dca2bb5ce5` (2026-09-14 16:59:33 +0900) | `git rev-parse HEAD` |
| 쓰기 대상 브랜치 | `bugfix/151-staging-ownership-boundary` | `git rev-parse --abbrev-ref HEAD` |
| 제안 원문 기준 커밋 | `6798032c839336f2c059a81db12a60602e731eae` ("chore(pi): add agentic toolchain plans, specs and bootstrap verifier (#12)", 2026-09-06 17:02:16 +0900) | `git log -1` |
| sourceRef ⊑ HEAD | 성립 (`git merge-base --is-ancestor` → ANCESTOR) | — |
| 6798032 ⊑ sourceRef ⊑ HEAD | 성립 (둘 다 ANCESTOR) | — |

### 1.2 드리프트 — **존재한다** (조용히 최신 트리를 쓰지 않는다)

`sourceRef` 와 쓰기 대상 HEAD 는 **다르다.** 실측:

| 항목 | 값 | 명령 |
|---|---|---|
| 커밋 수 (sourceRef..HEAD) | **24** | `git rev-list --count` |
| 경로 변경 건수 (name-status 행) | **504** | `git diff --name-status \| wc -l` |

그러나 **이 work item 의 비교 대상 파일들은 드리프트가 0 또는 국소적이다.**

| 대상 | sourceRef | HEAD | 판정 |
|---|---|---|---|
| `.github/CODEOWNERS` blob | `d4a50b7b…` | `d4a50b7b…` | **동일** |
| `docs/reviews/2026-09-06-r03-governance-review.md` blob | `5c3b763b…` | `5c3b763b…` | **동일** |
| `.github/workflows/pr-triage.yml` blob | `b2622c66…` | b2622c66… | **동일** |
| `docs/choo-guard-github-governance-audit-v1.md` blob | `cf7e6839…` | `cf7e6839…` | **동일** |
| `.github/` 내 변경 | — | `required-quality-gate.yml` **1건만 수정** (sha256 `c96678f92dfa17…` → `d460e9c2e9a444…`; 대응 git-blob-sha1 `7179d62b…` → `6affba37…`; +12줄: Set up Node 스텝과 "Validate phase-qualified work graph" 스텝 추가) | 커밋 `3861eab` ("docs: orchestrate 101 PM work orders and scoped LLM context (#148)") |

값의 알고리즘은 값 앞에 라벨로 적는다. 위 네 행의 `…` 값은 **git-blob-sha1** 이고, 마지막 행은 **sha256** 과 그에 대응하는 git-blob-sha1 을 함께 적었다(두 값의 산출 명령은 §12.2).

즉 드리프트 24커밋 중 거버넌스 표면 변경은 `required-quality-gate.yml` 1건이고 나머지는 문서·컨텍스트 추가다.

### 1.3 **ref 트리 밖 입력** (별도 등록)

- `docs/context/work-graph.json` 은 HEAD 트리에는 있으나 sourceRef 트리에는 **없다**: `git cat-file -t 6490956d…:docs/context/work-graph.json` → `fatal: path ... exists on disk, but not in '6490956d…'`. HEAD 에서는 존재(`b371ea3cbf9cdd60daf8a6d4a8fa06dda62a0dfd`). 동결 기준서가 적은 대로 ref 이후 커밋 `3861eab` 에서 추가됐다.
- `docs/context/work-orders/043.json` 도 같은 이유로 sourceRef 트리에 **없다**: ref 트리의 `docs/context/` 에는 `work-graph.json`·`work-orders/` 디렉터리가 아예 없다(`git ls-tree 6490956d… docs/context/` 관측: README/accepted-decisions/index.html/machine-profile/pr110/project-context/school-pc-handoff/team01/work-units 9항목).
- 따라서 work-graph `items[number=43]` 의 **acceptance/checks/truth/stopConditions/하드 위상**은 **저장소 tracked 정본(HEAD)에서 읽은 값**이며, 동결 기준서 §1 G6 이 지적한 대로 ref 트리만으로는 재확인이 불가능하다. 이 사실을 은폐하지 않는다: **ref 트리 관점에서 이 항목들의 권위는 미확정**이며, 이 후보는 그 값을 `context-only / non-ref` 로 표시해 인용한다.
- 자기모순 회피: 위 파일들을 "ref 트리에서 읽을 수 없다"고 적은 것은 사실이고, "HEAD 트리에서 읽었다"도 사실이다. 두 서술은 서로 다른 트리에 대한 것이며 모순이 아니다.

### 1.4 제안 원문 §3.3 의 전제 드리프트 (사실 등록)

제안 원문은 "`.pi/`·`.specify/`·`docs/evidence/` 는 PM 승인 대기로 **미추적**이다"라고 적었다. 이는 **제안 원문의 기준 커밋 `6798032` 시점에는 참**이다(추적 파일 수 0/0/0). 그러나 **sourceRef/HEAD 시점에는 거짓**이다(실측):

| 경로 | 6798032 추적 수 | sourceRef 추적 수 | HEAD 추적 수 |
|---|---|---|---|
| `.pi/` | **0** | 23 | 23 |
| `.specify/` | **0** | 21 | 21 |
| `docs/evidence/` | **0** | 24 | 66 |
| `docs/reviews/` | (미측정) | 5 | 8 |
| `docs/adr/` | (미측정) | (미측정) | 6 |

→ 제안 원문 §3.3 의 "선제 등록" 논지는 **전제가 바뀐 상태**다. 보호 대상이 이미 커밋되어 있으므로, 변경안 A 를 지금 적용하면 "첫 커밋부터 리드 검토"가 아니라 **이미 들어간 커밋 이후의 변경부터** 리드 검토가 걸린다. 이 사실은 제안 적용 시 재평가가 필요하다(§9 SC-3).

---

## 2. 읽기 집합과 다이제스트 대조

### 2.1 work-order minimumContext 3항목 — 기대값 대 관측값

`minimumContext` 는 **항목 3건, 고유 경로 2건**이다(`docs/choo-guard-github-governance-audit-v1.md` 가 서로 다른 selector 로 두 번 실림).

| # | path | selector | 기대 digest (work-order 기재) | 관측 digest (직접 재계산) | 대조 |
|---|---|---|---|---|---|
| 1 | `docs/choo-guard-github-governance-audit-v1.md` | audit findings and policy scope | git-blob-sha1 `cf7e6839a039d882d252f74700d335e5dd187249` | git-blob-sha1 `cf7e6839a039d882d252f74700d335e5dd187249` | **일치** |
| 2 | `docs/choo-guard-github-governance-audit-v1.md` | protected-path and CODEOWNERS audit sections | git-blob-sha1 `cf7e6839a039d882d252f74700d335e5dd187249` | git-blob-sha1 `cf7e6839a039d882d252f74700d335e5dd187249` | **일치** (동일 파일) |
| 3 | `.github/workflows/pr-triage.yml` | workflow triggers and checks | git-blob-sha1 `b2622c66417965abb9a48e0ae50e7688c2b6477d` | git-blob-sha1 `b2622c66417965abb9a48e0ae50e7688c2b6477d` | **일치** |

기대값의 선언 provider 는 세 항목 모두 `git-at-captured-ref` / `ref: 6490956d…` 다. 그런데 그 기대값을 **담고 있는 파일 자체**(`docs/context/work-orders/043.json`)는 위 ref 트리에 없다(§1.3). 즉 기대값 문자열은 맞지만, **기대값의 출처는 비-ref 입력**이다. 이 사실을 병기한다.

### 2.2 최소 문맥 항목의 자격 상태

세 항목 모두 `qualification: null`, `qualifiedRefs: []`, `accepted: false` 로 선언돼 있다. 즉 작업지시서 스스로 이 입력들을 **미자격(unqualified)** 으로 표시한다. 이 후보는 그 미자격 사실을 `unknown`/`SOURCE_CONFLICT` 가 아니라 **입력 상태 그대로** 등록한다(§9 U-1).

### 2.3 selector 의 실체

- selector 1·2 ("audit findings and policy scope" / "protected-path and CODEOWNERS audit sections") 는 각각 감사 문서 §2(발견·권고 표)와 §1(관측 상태 표, `CODEOWNERS` 행)에 대응한다. 감사 문서는 **총 59행**이다(`wc -l`).
- selector 3 은 `pr-triage.yml` 전체 트리거·권한·검사 열거에 대응한다(§3.3).

---

## 3. current — 현재 규칙 실물 (sourceRef == HEAD 로 확인된 부분)

### 3.1 `CODEOWNERS` 실물

- 위치: **`.github/CODEOWNERS` 1건만.** 루트 `CODEOWNERS`, `docs/CODEOWNERS` 는 없다(`git ls-tree -r HEAD --name-only | grep -i codeowners` → `.github/CODEOWNERS` 1건; `git ls-tree 6490956d… CODEOWNERS docs/CODEOWNERS` → 출력 없음).
- 크기·해시: 412 B · git-blob-sha1 `d4a50b7b01ded8bcf8b86b73e4bb28f242eacae6`.
- 규칙 줄 수: **9** = 전역 1 + 리드 8.

| # | 규칙 패턴 | 소유자(자리표시자) | 매치 tracked 파일 수 (HEAD, 1340개 중) |
|---|---|---|---|
| 1 | `*` | `{dev-team}` (전역) | 전체 |
| 2 | `/.github/` | `{lead-account}` | 14 |
| 3 | `/SECURITY.md` | `{lead-account}` | 1 |
| 4 | `/AGENTS.md` | `{lead-account}` | 1 |
| 5 | `/Packages/` | `{lead-account}` | 357 |
| 6 | `/ProjectSettings/` | `{lead-account}` | 22 |
| 7 | `/schemas/` | `{lead-account}` | 2 |
| 8 | `/Assets/ChooGuard/Runtime/Scoring/` | `{lead-account}` | **0** |
| 9 | `/Assets/ChooGuard/Runtime/Safety/` | `{lead-account}` | **0** |

관측 사실 2건(수치를 직접 셈):
- 리드 규칙 8개 중 **2개(#8, #9)는 HEAD 추적 파일이 0건**이다. 즉 그 두 규칙은 현재 보호할 tracked 대상이 없다.
- 리드 규칙의 소유자는 **개인 로그인 1건**을 가리킨다(제안 원문 G-08 이 지적한 사실). 값은 공개 파일에 있으나 수용 기준 4에 따라 이 문서에는 값을 쓰지 않고 `{lead-account}` 로만 적는다.

### 3.2 정책 glob 실물 — `scripts/bootstrap/verify_toolchain.py`

`POLICY_GLOBS` (소스 48–63행) = **14개**:

```
AGENTS.md, .github/CODEOWNERS, .github/workflows/*.yml,
.pi/settings.json, .pi/workflows.json, .pi/agents/*.md, .pi/workflows/*.yaml, .pi/prompts/*.md,
.specify/memory/constitution.md, docs/adr/*.md,
scripts/ci/*.py, scripts/bootstrap/*,
tools/research/pyproject.toml, tools/research/uv.lock
```

- 이 14개는 **`6798032` 시점과 동일**하다(두 ref 에서 같은 목록을 확인). 즉 제안 원문의 "14개 패턴" 서술은 원문 기준에서도 지금도 성립한다.
- `RECEIPT_ROOT = "docs/evidence"` (소스 39행). `docs/evidence/` 는 **`POLICY_GLOBS` 에도 `CODEOWNERS` 에도 없다**(제안 원문 §3.1 의 주장과 일치).
- `POLICY_GLOBS` 각 패턴의 tracked 매치 수(HEAD): 1, 1, 7, 1, 1, 7, 3, 11, 1, 6, 4, 5, 1, 1 → **합계 50개**.
- `POLICY_GLOBS` **14개 중 리드 규칙이 있는 것은 3개**(`AGENTS.md`→`/AGENTS.md`, `.github/CODEOWNERS`→`/.github/`, `.github/workflows/*.yml`→`/.github/`)이고 **11개는 없다.**
- 리드 규칙 **8개 중 `POLICY_GLOBS` 안에 있는 것은 2개**이고 **6개**(`/SECURITY.md`, `/Packages/`, `/ProjectSettings/`, `/schemas/`, `/Assets/…/Scoring/`, `/Assets/…/Safety/`)는 밖이다.

→ 이 두 방향 불일치가 **변경안 A 의 근거**다. 제안 원문의 목록(`.pi/` 5패턴, `.specify/memory/constitution.md`, `docs/adr/*.md`, `scripts/ci/*.py`, `scripts/bootstrap/*`, `tools/research/{pyproject.toml,uv.lock}` = 11 glob)은 **직접 세어 확인했다.**

### 3.3 필수 CI 실물

`.github/workflows/` 는 **7개**다(`git ls-tree -r HEAD | grep -c '^\.github/workflows/.*\.yml$'` → 7). 감사 문서 §1 CI 행의 7개 열거와 일치한다.

| 워크플로 | 트리거 | `permissions` |
|---|---|---|
| `pr-triage.yml` | `pull_request_target` (opened/edited/synchronize/reopened/ready_for_review) | `contents: read`, `pull-requests: write`, `issues: write` |
| `required-quality-gate.yml` | `pull_request`(동일 types), `push`(main, develop), `workflow_dispatch` | `contents: read` |
| `reconstruction-ci.yml` | `pull_request`(paths), `push`(develop, paths) | (미측정) |
| `unity-tests.yml` | `pull_request`(paths), `push`(develop, paths) | (미측정) |
| `unity-build.yml` | `push`(develop, paths), `workflow_dispatch` | (미측정) |
| `release.yml` | `push`(tags `v*.*.*`) | (미측정) |
| `nightly-gpu.yml` | `workflow_dispatch` | (미측정) |

확인한 사실:
- **필수 검사 이름**(감사 관측): `Policy, security and repository hygiene` — 이 이름은 `required-quality-gate.yml` 의 job `name`(19행)과 **문자열 일치**한다. 그러나 **이 job 이 서버에서 실제 required check 로 지정돼 있는지는 live 설정**이며 이 봉인 기준선에서 재조회되지 않았다 → **unknown**(§9 U-2).
- `pr-triage.yml` 은 `pull_request_target` + 쓰기 권한을 갖지만 **PR 코드를 체크아웃하지 않는다**(파일 내 `checkout` 문자열 0건, job name "Classify risk without executing PR code"). 이는 **캡처된 파일 문면**의 사실이며 실제 실행 안전성 증명이 아니다.
- `pr-triage.yml` 이 댓글 본문에 넣는 "필수" 목록(`Required Quality Gate`, `Reconstruction CI`, `area-owner review and evidence`, `lead review`, `Windows build and physical-headset release gate`)은 **문자열 생성일 뿐 status check 를 만들지 않는다**. 제안 원문 §5.3 의 주장과 캡처 파일 문면이 일치한다.

### 3.4 CI 이력·실행 결과

이 후보는 **워크플로의 실제 CI 실행 이력(성공/실패)을 조회하지 않았다.** work-graph context 가 `.github/workflows/pr-triage.yml` 에 대해 선언한 한계("successful CI run을 증명하지 않는다")와 같은 이유다 → §9 U-3.

---

## 4. old / current / proposed 3단 대조

| 축 | **old** (제안·감사 이전 관측, 2026-09-06) | **current** (sourceRef/HEAD 실물) | **proposed** (제안 원문 변경안) |
|---|---|---|---|
| `CODEOWNERS` 파일 | git-blob-sha1 `d4a50b7b…` · sha256 `a6d085e0…` (6798032) | git-blob-sha1 `d4a50b7b…` · sha256 `a6d085e0…` — **동일** | 두 digest 모두 변경 없음. **리드 규칙 8개 추가**(변경안 A) + 개인 로그인 → 팀(변경안 B) |
| 리드 규칙 수 | 8 | 8 | 8 + 8 = 16 (A 채택 시) |
| 리드 규칙 소유자 | 개인 로그인 1건 | 개인 로그인 1건 | `{lead-team}` (B 채택 시) |
| `POLICY_GLOBS` | 14 (6798032 와 동일) | 14 | 변경 없음 |
| `POLICY_GLOBS` ∩ 리드 규칙 | 2 | 2 | 최대 13 (A 채택 시: 14 − `tools/research` 는 lock 파일만 추가하는 경우 12~13) |
| `.pi/` tracked | **0** | **23** | 보호 대상으로 추가 |
| `.specify/` tracked | **0** | **21** | 보호 대상으로 추가(`memory/` 한정) |
| `docs/evidence/` tracked | **0** | **66** | 보호 대상으로 추가(변경안 C 는 **보류**) |
| `docs/reviews/` tracked | (미측정) | 8 | 보호 대상으로 추가 |
| `docs/adr/` tracked | (미측정) | 6 | 보호 대상으로 추가 |
| 필수 검사 | `Policy, security and repository hygiene` (감사 관측) | 파일상 job name 일치. 실제 required 여부 **unknown** | 변경 없음(§5.3 은 "필수/권고 표기 분리 + status check 구현" 제안) |
| 상시 우회 주체 | ruleset 에 등록(감사·검토 관측, 상세 비공개) | **unknown** (live) | 제거 + break-glass 절차(§5.1) |
| 자기검사 게이트 | `scripts/ci/**` 리드 규칙 없음 | 리드 규칙 없음(확인) | 리드 보호 선행(§5.5) |

표의 digest 값은 §1.2 의 표기 규칙대로 값 앞에 알고리즘 라벨을 붙인다. `CODEOWNERS` 행의 두 값은 같은 파일의 두 알고리즘이며, git-blob-sha1 은 `git rev-parse <ref>:.github/CODEOWNERS`, sha256 은 `shasum -a 256`(= macOS 에서의 `sha256sum`)으로 계산한다(§12.2). 이 표에서 digest 를 쓰는 셀은 `CODEOWNERS` 행 하나뿐이다.

**old == current 인 축**: `CODEOWNERS` blob, `POLICY_GLOBS` 목록. 즉 이 변경안은 "실물 규칙이 바뀌어서"가 아니라 "**보호 범위가 정책 glob 대비 비어 있어서**" 발생한다. `old` 와 `current` 가 갈리는 축은 **보호 대상의 추적 상태**(`.pi`/`.specify`/`docs/evidence`: 0 → 23/21/66)다.

### 변경안 원문이 주지 않은 것 (제안 확정 시 결정 필요)

- 변경안 A 는 "CODEOWNERS 파일 끝에 둔다"까지만 말하고 **정확한 패턴 리터럴 라인을 주지 않는다.** 예: `.pi/` 를 디렉터리 글롭으로 쓸지, `POLICY_GLOBS` 5패턴을 그대로 옮길지 미정이다. 이 후보는 임의로 확정하지 않는다 → §9 U-4.
- 변경안 B 는 "전용 리드 팀"을 말하지만 **팀명·구성원 매핑을 주지 않는다**(의도적으로 비공개). → §9 U-5.

---

## 5. C01 커버리지 표 — 변경 경로별 owner / review / test

C01 기대: `every changed path has owner/review/test and no unapproved permission expansion`. 증거: `diff and coverage table`.

### 5.1 변경 경로 (diff)

제안(변경안 A/B/C)이 **바꾸는 tracked 파일은 `.github/CODEOWNERS` 1개**다. 다른 파일을 바꾸지 않는다(제안 원문 §§3.2–3.3, 4.x, 5.x 텍스트 범위 내 관측). `.github/workflows/*` 는 §5.3·§5.5·§5.6 에서 **제안 대상으로 언급**될 뿐 파일 변경은 제안되지 않았다.

| 변경 경로 | 변경 유형 | owner | review | test |
|---|---|---|---|---|
| `.github/CODEOWNERS` | 리드 규칙 추가(A) + 소유자 팀 교체(B) | 기존: `/.github/` → `{lead-account}` / 제안 후: 전역 `{dev-team}` + 리드 8+8 | PR 경로에서 리드 CODEOWNER 검토가 필요하려면 **서버 ruleset 이 CODEOWNER review 를 요구**해야 한다 → **live 여부 unknown**(U-2). 로컬 파일 문면만으로는 강제를 증명할 수 없다 | C02(`python3 scripts/ci/tests/test_repository_policy.py`) + `required-quality-gate` 의 `Validate repository and changed files` 스텝. 단 C02 는 캡처된 정책 테스트만 검증한다 |

### 5.2 신규 보호 경로 (변경안 A)의 owner / review / test

| # | 신규 보호 경로 | 현재 리드 규칙 | 제안 후 owner | review 요건 | test / 검사 | HEAD tracked 수 |
|---|---|---|---|---|---|---|
| 1 | `.pi/` | 없음 | `{lead-team}` | 리드 CODEOWNER 검토(서버 강제 여부 unknown) | C02 + 정책 해시(`POLICY_GLOBS` 안) | 23 |
| 2 | `.specify/memory/` | 없음 | `{lead-team}` | 위와 동일 | `POLICY_GLOBS` 가 `constitution.md` 1건만 덮음(디렉터리 tracked 2건 중 1건) | 2 (`.constitution-template.json` + `constitution.md`) |
| 3 | `docs/adr/` | 없음 | `{lead-team}` | 위와 동일 | `POLICY_GLOBS` `docs/adr/*.md` | 6 |
| 4 | `docs/evidence/` | 없음 | `{lead-team}` | 위와 동일 | **`POLICY_GLOBS` 밖**(`RECEIPT_ROOT`). 변경안 C 가 보류한 이유 | 66 |
| 5 | `docs/reviews/` | 없음 | `{lead-team}` | 위와 동일 | **`POLICY_GLOBS` 밖** | 8 |
| 6 | `scripts/ci/` | 없음 | `{lead-team}` | 위와 동일 | C02 자신 + `POLICY_GLOBS` `scripts/ci/*.py` | 4 |
| 7 | `scripts/bootstrap/` | 없음 | `{lead-team}` | 위와 동일 | `POLICY_GLOBS` `scripts/bootstrap/*` | 5 |
| 8 | `tools/research/` lock (`pyproject.toml`, `uv.lock`) | 없음 | `{lead-team}` | 위와 동일 | `POLICY_GLOBS` 2건 | 2 |

신규 보호가 덮는 tracked 파일 **합계 116개**(23+2+6+66+8+4+5+2) — 직접 셈(`git ls-tree -r HEAD --name-only -- <경로> | wc -l`, §12.2).

`HEAD tracked 수` 열은 **보호 경로 전체**의 tracked 파일 수다. 변경안 A 의 대상이 디렉터리이면 그 아래 tracked 파일 **전부**가 보호 대상이므로, `.specify/memory/` 아래 두 파일 — `.specify/memory/.constitution-template.json`(git-blob-sha1 `2fe4dee3…`)과 `.specify/memory/constitution.md`(git-blob-sha1 `d7f0bd99…`) — 이 **모두** 변경안 A 의 보호 대상이다. `POLICY_GLOBS` 가 덮는 것은 그중 `constitution.md` 1건뿐이다.

### 5.3 권한 확대 없음 검사

수용 기준 3("No direct push/merge/publication permission is inferred")에 대응한다. 제안 원문에서 관측한 것:

- 변경안 A/B/C 및 §§4–5 의 어떤 제안도 **ruleset bypass 주체 추가, admin 권한 부여, 직접 푸시 허용, 병합 권한 위임, 게시 승인**을 요구하지 않는다. 반대로 §5.1 은 우회 주체 **제거**를 기본값으로 제안한다.
- 변경안 B(개인 계정 → 팀)는 **권한 확대가 아니라 소유자 표기 변경**이다. 단 구성원 매핑이 공개되지 않았으므로 "동일 인원 등가"인지는 **확인 불가** → §5.4.
- 이 후보 자신은 어떤 권한도 생성하지 않는다. 이 문서는 `docs/team/R-03/` 밖에 아무것도 쓰지 않았고 `.github/`·`CODEOWNERS` 를 수정하지 않았다(§0.3).

### 5.4 이 표가 증명하지 **않는** 것 (C01 부정 사례 준수)

C01 의 부정 사례는 `live-state gap => unknown, not pass` 다. 이 후보는 다음을 **증명하지 않는다**:

- 서버 ruleset 이 실제로 CODEOWNER review·필수 검사·승인 1건을 요구하는지 (live)
- 상시 우회 주체의 실재·식별 (live)
- 기본 브랜치 참조로 develop 보호가 잡혀 있는지 (live)
- 필수 검사가 실제로 병합을 막는지 (live)

따라서 §5 표는 **"변경 경로가 로컬 파일 문면상 owner/review/test 대응을 갖는다"** 까지만 말하고, 그 이상은 `unknown` 으로 등록한다. C01 은 여기서 **PASS 로 기록하지 않는다**(§7).

---

## 6. 적용 순서 (제안 원문 §3.3 의 재평가)

제안 원문의 순서는 **(1) `CODEOWNERS` 변경 PR 병합·검증 → (2) 보호 경로 파일 커밋** 이다. 그러나 §1.4 대로 `.pi/`(23)·`.specify/`(21)·`docs/evidence/`(66) 는 **이미 커밋돼 있다.** 따라서 지금 A 를 적용하면 보호는 **앞으로의 변경**에만 걸리고 **이미 들어간 파일에는 소급되지 않는다.** 제안 확정 시 이 순서 논지는 재작성되어야 한다 → §9 SC-3.

---

## 7. 검사 영수증 (C01 / C02)

work-graph `checks` 2건의 적용 phase 는 **둘 다 candidate, accept** 다. 이 후보는 **candidate 위상만** 수행했고 **accept 위상은 수행하지 않았다.**

### 7.1 C01 — `inspection: compare CODEOWNERS/workflows to proposed diff and baseline policy files`

| receiptField | 값 |
|---|---|
| artifactPath | `docs/team/R-03/governance-diff.md` |
| artifactDigest | **자기 해시는 자기 파일에 담지 않는다**(자기참조 회피 — 담으면 그 값을 쓰는 순간 해시가 바뀐다). 이 문서의 최종 sha256 은 짝 산출물 `docs/team/R-03/verification.json` 의 `checks[0].artifactDigest` 에 기록했다. 소비자·비평자는 §12.5 의 명령으로 직접 재계산할 수 있다 |
| provenance | §0.1 의 sourceScope 실물 + 제안 원문 blob `5c3b763b…` |
| thresholdEvaluation | "every changed path has owner/review/test and no unapproved permission expansion" — **로컬 부분 대조 완료**(§5). 변경 경로 1건(`.github/CODEOWNERS`), 신규 보호 8그룹 각각 owner/review/test 행 존재. 권한 확대 문구 0건 |
| **result** | **`NOT_RUN`** |
| notRunReason | C01 의 기대는 **현재 강제되고 있는 규칙**과의 대조를 포함한다. live GitHub settings 를 이 봉인 기준선에서 재조회하지 않았으므로(work-graph truth `[unknown]` — "live GitHub settings are not re-read in this frozen baseline"), "필수 검토·필수 검사가 실제로 강제된다"는 절반은 **미실행**이다. C01 의 부정 사례가 `live-state gap => unknown, not pass` 로 명시돼 있으므로 PASS 로 기록하지 않는다 |
| executedPortion | 로컬 캡처 트리 대조는 **실행했다**: §3(현재 규칙 실물), §4(3단 대조), §5(커버리지 표), §5.3(권한 확대 없음). 관측 결과 위반 0건 |
| executedPortionIsNotAPass | 위 executedPortion 은 **C01 의 PASS 가 아니다.** 검사 전체 결과는 `NOT_RUN` 이다 |

### 7.2 C02 — `python3 scripts/ci/tests/test_repository_policy.py`

| receiptField | 값 |
|---|---|
| artifactPath | `docs/team/R-03/verification.json` |
| artifactDigest | 그 파일 sha256 |
| provenance | `scripts/ci/tests/test_repository_policy.py` sha256 `d3715ecd…`, `scripts/ci/repository_policy.py` sha256 `3e80b330…`, Python 3.14.5, git 2.51.0 |
| thresholdEvaluation | **실행했다.** 종료 코드 `0`. unittest 출력 `Ran 33 tests in 9.479s` / `OK`. stderr 에 의도된 시뮬레이션 traceback 2건(테스트가 `__main__` 이 아닌 트레이스백 경로를 검증하는 케이스)과 `fatal: … no merge base` 1건이 섞여 나오나 실패로 계상되지 않았고 종료 코드는 0 |
| **result** | **`PASS`** — **단 이는 명명된 명령의 실행 결과일 뿐이며 정책 PASS 가 아니다** |
| resultScope | "records captured policy tests only". 이 명령은 **캡처된 정책 테스트 33건**을 실행할 뿐이다 |
| doesNotProve | GitHub 브랜치 설정, live 병합 권한, ruleset enforcement, 실제 CI 러너에서의 통과를 증명하지 않는다 (C02 부정 사례) |
| policyPassClaimed | `false` |
| acceptPhase | **미실행.** accept 위상의 동일 검사는 이 후보가 수행하지 않았다 |

### 7.3 실행하지 않은 것

- live GitHub 조회(`gh repo view`, `gh api repos/.../rulesets`, `gh pr list`, `gh issue list`) — 감사 문서 §3 의 재확인 명령. **미실행**(자격증명·인증 세션을 다루지 않으며, 비공개 부록도 이 후보의 범위가 아니다). §9 U-2.
- 실제 CI 실행(runner 에서의 워크플로 통과) — 미실행. §9 U-3.
- `node scripts/context/work_graph.mjs validate` — 미실행(이 후보의 필수 검사가 아님).

---

## 8. 명시적 승인 소유자 (explicit approval owner)

수용 기준 2("Protected paths, required reviews, CI and rollback checks are explicit")와 work-graph `reviewNotes`("역할=review/approval과 개인 assignee를 분리한다")에 대응한다. **개인을 assignee 로 지정하지 않는다**(비목표 "사람을 사전 assignee로 지정").

| 결정/행위 | 승인 소유자(역할) | 근거 | 이 후보가 한 것 / 안 한 것 |
|---|---|---|---|
| 변경안 A(리드 규칙 추가) 채택 | **PM** + **저장소 관리자** | 감사 문서 §2 G-01 "결정 역할: PM, 저장소 관리자" | **안 했다.** 제안만 인용 |
| 변경안 B(개인 로그인 → 팀) 채택 | **PM** (팀 생성·구성원 지정은 **별도 승인**) | 제안 원문 §6 "PM 결정 요청 목록"의 `개인 로그인 → 팀 교체 (변경안 B)` 행 + §3.2 "팀 생성·구성원 지정은 별도 승인이다". 참고로 감사 문서 §2 G-08 의 결정 역할 기재는 `PM, 각 역할 책임자` 이고 그 발견은 "로그인과 실제 승인 역할의 매핑이 확정되지 않았다"이며, 팀 교체 결정 역할을 적은 행이 아니다 | **안 했다** |
| 변경안 C(증거 통제) 보류 해제 | **PM** (보상 통제 검증 선행) | 제안 원문 §3.2 | **안 했다** |
| `.github/CODEOWNERS` 실제 파일 수정 | **저장소 관리자**(사람) — 에이전트 아님 | 감사 문서 G-01 "PM이 보호 범위·리드 역할을 승인한 뒤 사람이 변경한다" | **안 했다.** read_only |
| ruleset / secret scanning / push protection 설정 변경 | **PM** + **저장소 관리자** | 감사 문서 §2 G-02 | **안 했다** |
| 리드(CODEOWNER) 검토 | **리드 역할** — `CODEOWNERS` 리드 항목이 가리키는 계정/팀(`{lead-account}` / 제안 후 `{lead-team}`). **개인이 아니라 역할** | `CODEOWNERS` 실물 + work-graph reviewNotes | 역할로만 기재. 값 비표기 |
| 독립 검토 | **비작성자 팀 검토자** | 감사 문서 G-11 | 역할로만 기재 |
| 적용 후 검증 | **PM** (+ 저장소 관리자) | work-graph `checks` / handoff "PM evaluates handoff" | **미실행.** 짝 문서 `verification.json` 에 계획만 등록 |
| 반환(handoff) 수용 | **PM** → 소비자 M0-00(#13), R-04(#44), #45, #52 | work-graph `handoff.consumer`, `hardSuccessors` | — |

**에이전트(이 후보 포함)는 위 어느 승인도 생성하지 않는다.** 감사 문서 §3 의 문면("에이전트의 문서 수정도 사람의 승인 영수증이 아니다")과 일치한다.

---

## 9. 중단 조건 · SOURCE_CONFLICT · unknown

work-graph `stopConditions` 2건과 동결 기준서 §3 H1·H5 에 대응한다.

### 9.1 중단 조건 판정

| stopCondition | 판정 | 서술 |
|---|---|---|
| "Live settings conflict with snapshot; surface SOURCE_CONFLICT and defer authority decision." | **조건 미성립, 잠재 위험 등록** | live 를 읽지 않았으므로 **충돌 여부를 판정할 수 없다.** 충돌을 지어내지 않고 `SOURCE_CONFLICT_POSSIBLE_UNVERIFIED` 로 등록한다(SC-1). 권위 판단은 PM 에게 유보한다 |
| "Another writer owns `.github`/CODEOWNERS; do not overwrite." | **조건 미발견** | 이 후보는 `.github/`·`CODEOWNERS` 를 쓰지 않았다(§0.3). 같은 시점에 다른 워커들이 같은 작업트리의 다른 경로(`docs/team/M0-00`, `M0-02a`, `M0-03`, `M1-05`, `R-02`, `scripts/team/M1-05`, `docs/evidence/…`)를 만들고 있었으나(`git status --porcelain` 관측), 그중 `.github`/`CODEOWNERS` 를 쓰는 워커는 **관측되지 않았다**. 이 관측은 워크트리 파일시스템 수준이며, 다른 브랜치/원격의 소유권은 증명하지 않는다 → SC-4 |

### 9.2 SOURCE_CONFLICT 등록

| id | 서술 | 처리 |
|---|---|---|
| **SC-1** | live GitHub 설정과 캡처 감사(2026-09-06) 사이의 충돌 여부가 이 봉인 기준선에서 **판정 불가**. 특히 미해결 항목(G-02 비밀 탐지 5기능 비활성, G-04 자동 삭제, §5.1 상시 우회 주체)은 제안 원문 이후 **변했을 수 있다** | `SOURCE_CONFLICT_POSSIBLE_UNVERIFIED`. 권위 판단 유보. 적용 직전 `gh` 로 재조회 필요(제안 원문 §1 "적용 직전 반드시 재조회한다") |
| **SC-2** | 제안 원문 manifest 의 `scripts/bootstrap/verify_toolchain.py` sha256 은 `4bb8b4bbc846677963b4f391ef02f100d77e372f3a9bdc2580a36fb324a86413`(11656 B) 인데, **현재 파일은 `cc4f77dfd02ca7d46c2c9e7bc821bff4513a9ad39d54c2dfa87bf797160016a1`(17753 B)** 다. 원문 기준 커밋 `6798032` 에서는 원문 값이 **정확히 맞음**(재계산 확인) → 원문은 자기 시점에 옳았고, 이후 드리프트다 | 기록. 원문 manifest 를 현재 값으로 갱신해야 함 |
| **SC-3** | 제안 원문 §3.3 의 전제 "`.pi/`·`.specify/`·`docs/evidence/` 는 미추적"이 현재 트리에서 **거짓**(23/21/66 추적). 원문 기준 커밋에서는 참 | 기록. §6 재평가 필요 |
| **SC-4** | `.github`/`CODEOWNERS` 의 다른 작성자 소유 여부를 원격·타 브랜치 수준에서 확인하지 않았다 | unknown 등록(§9.3 U-6) |
| **SC-5** | 심각도 Medium 이상이고 **역할 매핑이 필요한** 발견 4건(감사 G-02·G-03·G-08·G-09 및 검토 §5.1·§5.4)은 상세가 **비공개 부록**에 있어야 하는데, 제안 원문 §0 이 스스로 "**비공개 부록은 아직 존재하지 않는다**"고 적었다. 즉 권위 있는 사본이 어디에도 없다 | SOURCE_CONFLICT(권위 결손). 이 후보는 부록을 **생성하지 않는다**(쓰기 범위 밖) |

### 9.3 unknown 등록

| id | unknown | 왜 확인 못 하는가 |
|---|---|---|
| **U-1** | work-order `minimumContext` 3항목의 자격 상태가 `accepted: false`, `qualifiedRefs: []` 인데 이 후보가 그 자격을 부여할 수 있는지 | 자격 부여는 후보 저자의 권한이 아니며, 기대값 출처가 비-ref 입력이다(§2.1) |
| **U-2** | 서버 ruleset 의 실제 보호 브랜치·필수 검사·승인 수·CODEOWNER review 강제 여부, 상시 우회 주체의 실재 | live 재조회 미실행(자격증명·비공개 부록 경계) |
| **U-3** | 워크플로의 실제 CI 실행 이력(성공/실패) | 원격 Actions 이력 미조회 |
| **U-4** | 변경안 A 의 정확한 `CODEOWNERS` 패턴 리터럴 라인 | 제안 원문이 주지 않음(§4 말미) |
| **U-5** | 변경안 B 의 리드 팀명·구성원 매핑(공개 문서에 없음, 의도적) | 비공개 |
| **U-6** | 다른 작성자가 `.github`/`CODEOWNERS` 를 소유하는지(원격·타 브랜치) | 원격 미조회(SC-4) |
| **U-7** | `git diff` 의 504 name-status 행 중 `.github`/`CODEOWNERS` 외 항목이 거버넌스에 미치는 영향 | 이 후보의 범위(`R-03 / R-03-governance-diff only`) 밖 |
| **U-8** | `reconstruction-ci.yml` 등 5개 워크플로의 `permissions` 블록 | 이 후보의 대조 범위(보호 경로·필수 CI)에 필수적이지 않아 미측정 |
| **U-9** | work-graph `truth[0].source` 인 `docs/choo-guard-execution-backlog-v1.md:21-36` 과 target 문장 "governance changes require explicit proposal, approval and post-change verification." 의 대응 정확도 | **부분 대조**(전 588행 중). 21–36행은 §1 완료 상태 표 꼬리 3행 + `## 2. 공통 Definition of Done` 제목·도입 + DoD 항목 1–8 이다. 문장을 그대로 옮긴 행은 없다(truth 문장은 요약이다). 가장 근접한 항목은 4("문서·승인 준비 작업은 … 승인 기록의 해시를 … 연결한다")와 7("독립 검토 반대가 해소되고 재검토를 통과했다")이며, 병합·배포·삭제에 "사전 승인"을 직접 요구하는 항목 11 은 **39행**으로 선언 범위 **밖**이다. 범위가 절 경계가 아니라 행 경계라 저자 의도를 파일만으로 확정할 수 없으므로 **unknown 으로 유지**한다 |

---

## 10. 미판정 초안 출처 명시

- 이 후보는 `.claude/worktrees/r-03-a/docs/team/R-03/{governance-diff.md,verification.json}` 와 `.claude/worktrees/r-03-b/docs/team/R-03/{governance-diff.md,verification.json}` 을 **입력으로 열람**했다. 두 초안은 **미판정 후보이며 채택된 것이 아니다.**
- 두 초안의 워크트리 HEAD 는 `6490956d203549b56cbd86846120759889abb016` (= sourceRef) 이다(`git -C … rev-parse HEAD` 관측). 이 후보를 쓰는 작업트리의 HEAD 는 `12c77b8c8abed8f97ff16bc3aaf7a4dca2bb5ce5` 다 → **초안이 쓰인 트리와 이 후보가 쓰이는 트리가 다르다.**
- **옮겨 적은 사실은 없다.** 이 문서의 모든 수치·해시·경로는 §12 의 명령으로 저자가 직접 재계산·재관측한 값이다. 초안에서 값을 인용한 곳은 한 곳도 없다.
- 초안이 서술한 결론·판정도 채택하지 않았다. 예컨대 초안들은 각자 자기 방식으로 C 검사를 기록했으나, 이 후보는 §7 에서 **자기 관측에 근거해 다시** 기록했다.

---

## 11. 비목표 (주장하지 않는 것)

work-graph `nonGoals` 를 그대로 지킨다. 이 후보는 다음을 **하지 않았고 주장하지 않는다**:

- GitHub settings / `CODEOWNERS` 직접 변경
- 권한 확대·삭제·merge
- 사람을 사전 assignee 로 지정
- 변경안을 **적용 완료**로 표시 (제안 상태 그대로다)
- 제품 구현·게시·GitHub 변경
- 완료 / AAA 소프트웨어·안전·현장 효과 주장. **이 문서의 어떤 문장도 런타임·설치·네트워크·Unity·게시·병합 권한을 만들지 않는다.**
- 라이브 설정에 대한 검증된 PASS 주장

---

## 12. 실행한 명령과 관측 결과 (재현 가능)

### 12.1 ref / 드리프트

| 명령 | 관측 |
|---|---|
| `git rev-parse HEAD` | `12c77b8c8abed8f97ff16bc3aaf7a4dca2bb5ce5` |
| `git rev-parse --abbrev-ref HEAD` | `bugfix/151-staging-ownership-boundary` |
| `git cat-file -t 6490956d…` | `commit` |
| `git merge-base --is-ancestor 6490956d… HEAD` | ANCESTOR |
| `git merge-base --is-ancestor 6798032… HEAD` / `… 6490956d…` | 둘 다 ANCESTOR |
| `git rev-list --count 6490956d…..HEAD` | `24` |
| `git diff --name-status 6490956d…..HEAD \| wc -l` | `504` |
| `git diff --name-status 6490956d…..HEAD -- .github CODEOWNERS` | `M .github/workflows/required-quality-gate.yml` 1건 |
| `git cat-file -t 6490956d…:docs/context/work-graph.json` | `fatal: ... exists on disk, but not in '6490956d…'` |
| `git ls-tree 6490956d… docs/context/` | 9항목, `work-graph.json`·`work-orders/` 없음 |
| `git ls-tree HEAD docs/context/` | `work-graph.json`(`b371ea3c…`), `work-orders/` 트리 존재 |

### 12.2 해시·개수

| 명령 | 관측 |
|---|---|
| `git rev-parse 6490956d…:.github/CODEOWNERS` / `HEAD:.github/CODEOWNERS` / `6798032…:.github/CODEOWNERS` | 모두 `d4a50b7b01ded8bcf8b86b73e4bb28f242eacae6` |
| `git hash-object docs/choo-guard-github-governance-audit-v1.md` | `cf7e6839a039d882d252f74700d335e5dd187249` (minimumContext 기대값과 일치) |
| `git hash-object .github/workflows/pr-triage.yml` | `b2622c66417965abb9a48e0ae50e7688c2b6477d` (일치) |
| `git hash-object docs/reviews/2026-09-06-r03-governance-review.md` | `5c3b763b77a43a7b0bde77c3e3712c3780a38f55` |
| `python3 -c hashlib.sha256` (12개 파일, §0.1) | §0.1 표의 sha256 값 |
| `ls .superpowers/.../frozen-criteria-43.md.sha256` + `cat` | `a643f4b2…` — 재계산값과 **일치** |
| `ls .github/workflows/*.yml \| wc` / `git ls-tree -r HEAD \| grep -c '^\.github/workflows/.*\.yml$'` | `7` |
| `git ls-tree -r HEAD --name-only \| grep -i codeowners` | `.github/CODEOWNERS` **1건** |
| `git ls-tree 6490956d… CODEOWNERS docs/CODEOWNERS` | 출력 없음(루트/문서 CODEOWNERS 없음) |
| `python3` 로 `CODEOWNERS` 규칙 파싱 | 규칙 9 = 전역 `*` 1 + 리드 8 |
| `python3` 로 `POLICY_GLOBS` 파싱 | 14개 |
| `python3` 로 glob×tracked 매치 계산 | §3.1·§3.2 의 매치 수 (총 tracked 1340) |
| `git ls-tree -r <ref> --name-only \| grep -c` (`.pi/`, `.specify/`, `docs/evidence/`, `docs/reviews/`) | §1.4 표 (6798032: 0/0/0 · sourceRef: 23/21/24/5 · HEAD: 23/21/66/8) |
| `git ls-tree -r HEAD --name-only -- <경로> \| wc -l` (`.pi/`, `.specify/memory/`, `docs/adr/`, `docs/evidence/`, `docs/reviews/`, `scripts/ci/`, `scripts/bootstrap/`, `tools/research/{pyproject.toml,uv.lock}`) | §5.2 표 (23/2/6/66/8/4/5/2 → 합계 116) |
| `git ls-tree -r HEAD -- .specify/memory` | `2fe4dee368192e7902e41144b2f444fbbc0e15a9 .specify/memory/.constitution-template.json` / `d7f0bd99c52fddab99c7b7e94bdfac1eeeb5212a .specify/memory/constitution.md` — **2건** |
| `git show 6490956d…:.github/workflows/required-quality-gate.yml \| shasum -a 256` / `shasum -a 256 .github/workflows/required-quality-gate.yml` | sha256 `c96678f92dfa1738954481aeef0e05e4c2f0ec8ec8b7af7c1b25e7f1777eda4d` → `d460e9c2e9a44424dc355b58fcdfded5ef85b156f64252a81eff6c770b4ae4d6` (§1.2 마지막 행의 값 출처; macOS 에서 `shasum -a 256` = `sha256sum`) |
| `git rev-parse 6490956d…:.github/workflows/required-quality-gate.yml` / `git rev-parse HEAD:.github/workflows/required-quality-gate.yml` | git-blob-sha1 `7179d62bfb810cbf6e580f34e828cb967fc05407` → `6affba374bcc98aae6c22bb3ab31efe0d5f8c551` |
| `git cat-file -s <ref>:….github/workflows/required-quality-gate.yml` / `git diff --stat 6490956d…..HEAD -- .github/workflows/required-quality-gate.yml` | 크기 5974 B → 6393 B, 줄 수 131 → 143; `1 file changed, 12 insertions(+)` |
| `ls -la SECURITY.md AGENTS.md` | 각각 691 B / 9142 B |
| `wc -l docs/choo-guard-github-governance-audit-v1.md` | `59` |
| `ls .superpowers/.../packet/*.answer-key.json \| wc -l` / `verdicts/*.map.json \| wc -l` | 30 / 12 (**파일명만 나열, 내용 미열람**) |

### 12.3 산출물 자기 검증

자기 및 형제 해시를 자기 파일에 담으면 그 값을 쓰는 순간 값이 낡으므로 담지 않는다. 대신 **소비자가 직접 재계산하는 명령**을 남기고, 이 문서의 최종 sha256 과 바이트는 **뒤에 저작되는 짝 산출물** `docs/team/R-03/verification.json` 의 `checks[0].artifactDigest` / `handoffReturnFields.inputOutputHashes` 에 기록한다.

이 초안의 수선 이력은 **두 라운드**다. **1차 수선**이 대상으로 한 결함은 재검증자(round 2)가 확인한 바로 **5건**이며, 실제 목록은 (1) §5.2 tracked 수와 합계, (2) U-9 출처 경로, (3) §8 G-08 행, (4) §1.2 알고리즘 라벨(+§12.2 명령 추가), (5) 짝 산출물 `docs/team/R-03/verification.json` 의 `rollbackSafeEvidence` R4 참조 건수 — 이다. (이전 판은 다섯 번째를 "§12 명령 목록"으로 적었는데, 그것은 (4) 의 수정요구 일부이며 별개 항목인 (5) 를 목록에서 빼고 있었다.) 항목별 상태는 다음과 같으며, **해소 여부의 최종 판정은 이 문서가 아니라 독립 검증자가 한다.**

| 1차 결함 | 위치 | 1차 수선 후 관측된 상태 |
|---|---|---|
| (1) tracked 수와 합계 | 이 문서 §5.2 | 재검증 V04 가 `git ls-tree` 계열로 값을 재계산해 §5.2·§12.2 와 일치(합 116)함을 확인했고, 재지적되지 않았다 |
| (2) 출처 경로 | 이 문서 §9.3 U-9 | 재검증에서 재지적되지 않았다 |
| (3) G-08 행 | 이 문서 §8 | 재검증에서 재지적되지 않았다 |
| (4) 알고리즘 라벨 | 이 문서 §1.2 | §1.2 에 라벨을 붙이고 §12.2 에 산출 명령을 추가했다. 다만 **같은 계열의 미적용 위치(§4 표 `CODEOWNERS` 행)가 남아 있었고 재검증이 P2 로 반환했다** |
| (5) R4 참조 건수 | `verification.json` 의 R4 | **미해소.** 1차 수선이 수치를 실측과 다르게(추적 2건·히트 2건) 축소 기록했고, 재검증이 P1 로 반환했다. **2차 수선(이 판)** 에서 실측값(추적 5건·총 38행)으로 다시 썼다 |

**2차 수선(이 판)** 의 변경은 위 (4) 의 미적용 위치(§4 표 digest 라벨)와 (5)(`verification.json` R4) 두 곳이다. 위 두 곳에 기록된 이 문서의 다이제스트는 **2차 수선 후의 이 문서를 다시 계산한 값**이며, 1차 수선 전 값(`6bcc2c7b…` / 43844 B)과 1차 수선 후 값(`3908a735…` / 47758 B)은 모두 폐기됐다. 두 파일을 다 쓴 뒤 실제로 실행해 확인한 항목은 다음과 같다.

- **존재 확인**: `ls -la docs/team/R-03/` → 두 파일이 있다.
- **JSON 파싱**: `python3 -c "import json; json.load(open('docs/team/R-03/verification.json'))"` → 예외 없이 성공(짝 문서가 유효한 JSON).
- **구조 개수**: `len(postApplicationChecks)=8`, `len(expectedFailures)=6`, `len(rollbackSafeEvidence.entries)=5`.
- **V01 기준선(적용 전)**: 제안 A 대상 8그룹 샘플 경로가 현재 `CODEOWNERS` 에서 **리드 규칙 매치가 빈 리스트**다(전역 `*` 만 매치) → 제안 A 의 전제("이 8그룹에 리드 규칙이 없다")가 현재 트리에서 성립함을 실측으로 확인.
- **V02 기준선(적용 전)**: 현재 `CODEOWNERS` 는 `rules 9 ownerless 0 dupPatterns 0`.
- **R4 참조 재측정(2차 수선)**: `git grep -l "docs/team/R-03" HEAD` → **5개 파일** · `git grep -n "docs/team/R-03" HEAD | wc -l` → **38행** · 파일별 `git grep -c "docs/team/R-03" HEAD` → `docs/context/work-graph.json` **25**, `docs/context/work-orders/index.jsonld` **6**, `docs/context/graphify/graph.json` **4**, `docs/context/work-orders/043.json` **2**, `docs/context/index.html` **1**. 관측 시각 `2026-09-14 20:35:31 +0900`, 관측 HEAD `12c77b8c8abed8f97ff16bc3aaf7a4dca2bb5ce5`. `HEAD` 는 **이동 가능한 ref** 이므로 이 값은 그 시각의 스냅샷이며, 이후 커밋으로 값이 달라질 수 있다. 5건의 분류(계약 문서 / 파생 목록)와 롤백 파급 판정은 짝 문서 `verification.json` 의 `rollbackSafeEvidence.entries` R4 에 있다.
- **sha256 재계산 명령(값은 여기 적지 않는다)**: `python3 -c "import hashlib,sys;[print(hashlib.sha256(open(p,'rb').read()).hexdigest(), p) for p in sys.argv[1:]]" docs/team/R-03/governance-diff.md docs/team/R-03/verification.json`

### 12.4 미실행 목록

`gh` 계열 live 조회, 실제 CI 실행, `node scripts/context/work_graph.mjs validate`, accept 위상 검사 — **NOT_RUN**, 사유 §7.3·§9.3.

---

## 13. 수용 기준 대응표

work-graph `acceptance` 4항목을 항목별로 이 산출물의 어느 필드가 충족하는지 대응시킨다.

| # | 수용 기준 | 충족 필드 |
|---|---|---|
| 1 | Proposal is isolated from live `.github`/GitHub writes. | 헤더 상태 표기 · §0.3(targetScope, `canonicalWriteAllowed=false`, `.github`/`CODEOWNERS` read_only) · §5.3 마지막 항목 · §9.1 두 번째 행 · §11 |
| 2 | Protected paths, required reviews, CI and rollback checks are explicit. | §3.1(보호 경로 표) · §3.2(POLICY_GLOBS) · §3.3(CI 표) · §5.1–5.2(owner/review/test) · §8(승인 소유자) · 롤백 검사는 짝 산출물 `docs/team/R-03/verification.json` 의 `rollbackSafeEvidence` |
| 3 | No direct push/merge/publication permission is inferred. | §5.3 · §8(에이전트는 승인을 생성하지 않음) · §11 · 헤더 3번째 줄 |
| 4 | No named teammate or location route appears. | 헤더 공개 경계 줄 · §3.1(`{lead-account}`) · §5.2(`{lead-team}`) · §8(`{dev-team}`) — 개인 로그인·조직 팀 실명·호스트명·절대경로·워크스페이스 ID 미기재 |

`verification.json` 의 수용 기준("post-application checks, expected failures, rollback-safe evidence")은 짝 문서가 담당하며, 이 문서는 그것을 §7.2 와 §13 에서 참조만 한다.

---

## 14. 한계

- 이 문서는 **캡처된 트리**만 본다. live GitHub 상태는 보지 못했다(U-2).
- 드리프트 24커밋 중 `.github` 변경 1건 외의 영향은 범위 밖이다(U-7).
- 제안 원문은 **미승인 초안**이며 스스로 "R-03은 `cannot_proceed`를 유지한다"고 적었다(원문 §7). 이 후보는 그 결정을 **바꾸지 않는다.**
- 이 후보의 C 검사 기록은 **저자 자기 관측**이다. 독립 검토자의 판정이 아니다.
