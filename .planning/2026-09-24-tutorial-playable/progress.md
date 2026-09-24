# Progress Log

## Session: 2026-09-24

### Current Status
- **Phase:** 완료. 브랜치 `feature/tutorial-playable` (PR #238 의 `feature/tutorial-maintenance-layer` 위에 쌓음).
- PlayMode **75/75** 통과, 실패·건너뜀 0.
- G9 는 여전히 `NOT_VERIFIED`. 사람 플레이 검수가 없는 한 올리지 않는다.

### Actions Taken

**① 판정 선택.** 절차를 v2 → v3 로 올리고 `record-verdict` 의 조건에 `verdict-selected` 를 추가. `ProcedureRunner` 에 `FactsAddedInV3` 를 넣어 버전이 어휘를 통제하게 유지(v1 절차가 v3 사실을 쓰면 거부). 생성기는 `PendingVerdict = NOT_RECORDED` 로 둔다 — 정답을 미리 넣지 않는다.

**② 점검표.** `BuildTagTemplate(host)` 가 종이 재질의 비활성 Quad 를 만들고, 유닛마다 `점검표 부착 지점` 앵커를 판독면 앞에 둔다. 콜라이더는 제거 — 점검표가 판독면 조준을 가리면 안 된다. `AttachTag` 가 앵커에 붙이고 로컬 트랜스폼을 0 으로 맞춘 뒤 활성화한다.

**③ 입력.** `TutorialInput`(1 = 적합, 2 = 부적합, F = 현장 수리 시도) 신규. `FirstPersonResponder` 에 키를 더하지 않았다 — 그쪽은 이동·시선 계층이고 판정이나 역할 경계를 알아서는 안 된다(설비가 절차를 모르는 것과 같은 분리). 키 읽기와 매핑을 `HandleKeys` 로 나눠, OS 입력을 합성하지 않고도 게이트를 포함한 실제 경로를 시험한다.

**⑤ 진입.** `SceneEntryPoint` 신규 + `BootstrapEntryBuilder` 메뉴. 씬 이름으로 부르고(빌드 인덱스는 재정렬 때 조용히 어긋난다), 빌드 설정에 없으면 `LogError` 로 크게 알린다. Enter 만 받는다.

**ECC 리뷰 대응.** 판정 유닛별 분리, 되감기의 월드 상태 복원 — 상세는 `findings.md` 3 절.

**씬 재생성.** `FpsStation.unity`(`pendingVerdict: 0`, `InspectionTagPrefab` 연결, `TutorialInput` 1, `FacilityInspectable` 12, `TechnicianDispatch` 12), `Bootstrap.unity`(`SceneName: FpsStation`, `LoadOnStart: 0`, `WaitForInput: 1`) 확인.

### 시험 결과

| 시험 클래스 | 결과 |
|---|---|
| `CSBOOT0101PlayModeTests` | 1/1 |
| `FpsAuditTerminalTests` | 8/8 |
| `FpsCarryoverTests` | 8/8 |
| `FpsMultiTargetTests` | 10/10 |
| `FpsPlayableTests` (신규) | 12/12 |
| `FpsProcedureTests` | 7/7 |
| `FpsPromptPipelineTests` | 4/4 |
| `FpsTechnicianHandoffTests` | 13/13 |
| `IntegratedInputPlayModeTests` | 12/12 |
| **합계** | **75/75** |

작업 중 `CSBOOT0101` 과 `IntegratedInput` 이 각각 한 번씩 깨졌고, 둘 다 내 설계 오류를 드러냈다(`findings.md` 2 절). 최종 실행에서 모두 통과.

### 기록
- 영수증 `docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-24-playable.json`
- 설계 문서 `TUTORIAL_MAINTENANCE_DESIGN.md` 5.5 절 신설, 6 절 순서 갱신
- 루브릭 `QUALITY_A_RUBRIC.md` G9 갱신 — PASS 아닌 이유를 넷에서 **다섯**으로(소리 없음 추가)

### Next
④ 기술자에게 몸 주기 → ⑥ 소리 → 사람 플레이 검수. 본편 쪽 순서는 `.planning/2026-09-24-mission-design/` 과 `MISSION_DESIGN.md` 8 절.
