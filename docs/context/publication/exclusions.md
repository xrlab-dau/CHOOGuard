# 게시 제외 항목과 사유

작성 2026-09-13 · 이슈 #120 (R-08) · 기준 ref `bdcb714` (`feature/54-native-foundation`)

게시 판단은 PM이 한다. 이 문서는 제외 대상과 사유를 기록할 뿐 삭제·정리를 수행하지 않는다.

## 1. 정책상 항상 제외

`SECURITY.md`와 `AGENTS.md`가 정한 범위다. 게시 ref에도 포함하지 않는다.

- 철도 원본 영상·사진과 비식별이 끝나지 않은 캡처
- 민감 복원 기하와 제한 시설 자산
- 모델 가중치·체크포인트, 접근 토큰, 자격 증명, `.env`
- Unity 라이선스 파일과 계정 정보

## 2. 원시 계획 로그 (299건, 비공개 유지)

`.planning/2026-09-09-aaa-review/**` 아래 299건이다. 작업 단위 기록(2026-09-08)의 "`.planning` 원시 로그·캡처·일회성 scratch는 게시하지 않는다" 경계를 그대로 적용한다. 삭제하지 않고 보유자의 로컬에 보존한다.

## 3. PM 판단이 필요한 보류 항목 (32건)

캡처 시점에 미게시였고 지금도 팀 ref에 없다. 민감 자료로 분류된 것은 아니며, 아래 묶음별로 게시 예정인지 의도된 제외인지 확인이 필요하다.

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

`docs/context/source-availability.json`은 이번 작업에서 갱신하지 않았다. 가용성 분류는 미게시 파일을 보유한 작업트리를 직접 관측해야 계산할 수 있고, 그 작업트리는 이 작업자의 것이 아니다. 현재 파일의 스냅샷 `localHead`는 `e7a6bb7`이고 게시 head는 `bdcb714`이라 값이 어긋난다. 보유자가 다시 캡처하거나 PM이 갱신 경로를 지정해야 한다.

## 5. 경로 정정

이슈 본문의 0커밋 목록은 `Multiplayer/Runtime/AuthoritativeShift.cs`를 가리키지만 게시 ref의 실제 경로는 `Runtime/Multiplayer/AuthoritativeShift.cs`이며 `Tests/Editor/AuthoritativeShiftTests.cs`가 함께 있다. manifest는 실제 경로로 결속했다.
