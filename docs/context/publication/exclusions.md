# 게시 제외 항목과 사유

`mode: isolated_proposal` · `canonicalWriteAllowed: false` · 이 문서는 검토용 초안이며 스스로를 accepted/final/complete 로 표기하지 않는다.

작성 2026-09-13 · 이슈 #120 (R-08) · 기준 `e480e69` (`develop`)

`sourceRef` 값은 전달된 계약에서 공백이다. 임의로 메우지 않고, 대신 원격 기준 ref `origin/feature/54-native-foundation`(head `bdcb714`)를 사용했다. `sourceRef` 가 비어 있어 직접 비교할 드리프트 대상이 없다는 사실과 후보 워크트리·로컬 브랜치·develop 사이의 실제 드리프트는 `team-baseline.json` 의 `sourceRef.drift` 에 기록했다.

원본 ref `bdcb714`(`feature/54-native-foundation`)는 2026-09-13 08:34 UTC에 PR #153으로 develop에 squash 병합됐다. 전달된 261개 경로는 develop에서 다시 대조해 전부 일치했다.

게시 판단은 PM이 한다. 이 문서는 제외 대상과 사유를 기록할 뿐 삭제·정리를 수행하지 않는다.

## 1. 정책상 항상 제외

`SECURITY.md`와 `AGENTS.md`가 정한 범위다. 게시 ref에도 포함하지 않는다. 브리프 §1 이 요구하는 범주(raw/secret/generated/cache/진행중 변경)와 실제 대상을 아래에 맞춰 적는다.

**raw** — 철도 원본 영상·사진과 비식별이 끝나지 않은 캡처. 원본 파일은 보유자 로컬에만 있다.

**secret** — 모델 가중치·체크포인트, 접근 토큰, 자격 증명, `.env`, Unity 라이선스 파일과 계정 정보. 리포지토리에 복사하지 않는다.

**generated (의도적 생성물)** — 실제 대상:
- `Assets/CHOOguardGenerated/**` — Unity 가 생성한 씬·프리팹·머티리얼. 캡처 워크트리 관측 기준 미추적 389건·수정 22건이며, 기준 ref `bdcb714` 에는 118건이 추적돼 있고 전달 261경로에는 0건이다(manifest 에 `Assets/` 경로 없음).
- 루트의 `TestResults-*.xml` 13건 — 테스트 러너가 남긴 생성 로그.
게시 대상이 아니며, 필요하면 생성 스크립트로 재생성한다.

**cache (재생성 가능 캐시)** — 실제 대상: `.pi/npm/`, `Library/`, `Logs/`, `__pycache__/`, `.pytest_cache/`, `.mypy_cache/`, `.ruff_cache/`. 캡처 워크트리에 `Library/`·`Logs/`·`.pi/npm/` 이 존재함을 관측했다(`Temp/`·`obj/` 는 없음). 모두 `.gitignore` 제외 대상이라 보존하지 않는다.

**진행중 변경 (in-progress)** — 캡처 원본 워크트리의 미커밋 변경. 아래 §3 의 보류 32건과 원본 워크트리의 dirty 상태가 여기에 해당한다. 삭제하지 않는다.

### 보존 위치 유형

- `tracked-on-unpublished-branch` — 커밋 이력에 남아 있어 ref 로 복구 가능. 삭제하지 않는다.
- `holder-local-worktree` — 보유자 로컬 작업트리에만 존재. 이 작업자는 복사·삭제하지 않고 관측만 했다.
- `regenerable-ignored` — `.gitignore` 제외 대상의 재생성 가능 캐시. 보존하지 않는다.

## 2. 원시 계획 로그 (299건, 비공개 유지)

`.planning/2026-09-09-aaa-review/**` 아래 299건이다. 작업 단위 기록(2026-09-08)의 "`.planning` 원시 로그·캡처·일회성 scratch는 게시하지 않는다" 경계를 그대로 적용한다. 삭제하지 않고 보유자의 로컬에 보존한다.

## 3. PM 판단이 필요한 보류 항목 (32건)

캡처 시점에 미게시였고 병합된 develop에도 여전히 없다. 민감 자료로 분류된 것은 아니며, 아래 묶음별로 게시 예정인지 의도된 제외인지 확인이 필요하다.

### 증거 영수증 (11건)

- `docs/evidence/foundation/2026-09-08-cv-cache-fix.json`
- `docs/evidence/foundation/2026-09-08-cv-delivery.json`
- `docs/evidence/foundation/2026-09-08-cv-parent-audit.json`
- `docs/evidence/foundation/2026-09-08-input-recovery.json`
- `docs/evidence/foundation/2026-09-08-native-foundation-local.json`
- `docs/evidence/foundation/2026-09-08-rejected-cv-source-availability.json`
- `docs/evidence/foundation/2026-09-09-connected-world.json`
- `docs/evidence/foundation/2026-09-09-crowd-core.json`
- `docs/evidence/foundation/2026-09-09-fire-core.json`
- `docs/evidence/foundation/2026-09-09-multiplayer-slice.json`
- `docs/evidence/foundation/2026-09-09-simulation-components.json`

### 설계·계약 문서 (10건)

- `docs/art/aaa-review-rubric.md`
- `docs/art/aaa-review-workflow.md`
- `docs/choo-guard-connected-world.md`
- `docs/choo-guard-foundation-multiplayer.md`
- `docs/choo-guard-foundation-scope-discovery.md`
- `docs/choo-guard-native-acceptance.md`
- `docs/choo-guard-native-team-contracts.md`
- `docs/choo-guard-native-validation.md`
- `docs/context/sota-rejection-2026-09-08.md`
- `docs/context/upstream-foundation-sync-2026-09-08.md`

### 도구·작업 메모 (5건)

- `.pi/npm/package-lock.json`
- `.pi/npm/package.json`
- `scripts/context/.planning/findings.md`
- `scripts/context/.planning/progress.md`
- `scripts/context/.planning/task_plan.md`

### 물리 모델 문서 (3건)

- `docs/physics/crowd-motion-model.md`
- `docs/physics/crowd-world-contact.md`
- `docs/physics/heat-smoke-model.md`

### Native C# 소스 (2건)

- `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/NativePerformanceCapture.cs`
- `Packages/com.xrlab.chooguard.foundation/Demo/Tests/Editor/NativePerformanceTests.cs`

### 기타 (1건)

- `CODEX_HANDOFF.md`

물리 모델 문서와 증거 영수증은 #101·#122·#123·#59·#125~#127이 참조할 근거로 보인다. 제외로 확정하면 해당 이슈의 입력이 비는지 함께 확인해야 한다.

## 4. 재생성하지 못한 산출물

`docs/context/source-availability.json`은 이번 작업에서 갱신하지 않았다. 병합 후 다시 계산하면 미게시 508건 중 177건이 develop에 있고 331건이 남는다(.planning 299 + 보류 32). 가용성 분류는 미게시 파일을 보유한 작업트리를 직접 관측해야 계산할 수 있고, 그 작업트리는 이 작업자의 것이 아니다. 현재 파일의 스냅샷 `localHead`는 `e7a6bb7`이고 게시 head는 `bdcb714`이라 값이 어긋난다. 보유자가 다시 캡처하거나 PM이 갱신 경로를 지정해야 한다.

## 5. 경로 정정

이슈 본문의 0커밋 목록은 `Multiplayer/Runtime/AuthoritativeShift.cs`를 가리키지만 게시 ref의 실제 경로는 `Runtime/Multiplayer/AuthoritativeShift.cs`이며 `Tests/Editor/AuthoritativeShiftTests.cs`가 함께 있다. manifest는 실제 경로로 결속했다.
