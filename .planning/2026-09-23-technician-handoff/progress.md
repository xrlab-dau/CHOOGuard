# Progress Log

## Session: 2026-09-23 ~ 2026-09-24

### Current Status
- **Phase:** Phase 1~6 완료 + 12개 좌표 주입·씬 배선 완료. 커밋하지 않음(로컬 변경).
- 기술자 인계 상태 기계 구현 + PlayMode **45/45** 통과(신규 13, 기존 32, 회귀 없음).
- 씬: `FacilityInspectable` 12 · `TechnicianDispatch` 1 · `TutorialSession` 1 · `AuditTerminal` 1.
- 슬라이스 생성기 사고는 해소 — 기본 메뉴가 역사 12개를 재현하고, 단일 배치는 확인 대화상자를 거친다.
- G9 판정: `NOT_EVALUATED` → `NOT_VERIFIED`.

### Actions Taken
- `.planning/2026-09-23-technician-handoff/task_plan.md` 작성 후 Phase 1~6 수행.
- **Phase 1** `App/Fps/Work/TechnicianDispatch.cs` 신규 — `HandoffStage{NONE,RECEIVED,TRAVELLING,WORKING,COMPLETED}`, `Request`/`Tick(seconds)`/`WitnessResult`/`ResetHandoff`. 벽시계 없음, 시간 초과 실패 조건 없음.
- **Phase 2** `FacilityInspectable`에 `ServiceCompleted`·`ReplacedFromSerial`·`ApplyReplacement(string)` 추가. `preReplacement` nullable struct로 교체 전 결함 상태를 기억해 `ResetInspection()`이 복원한다.
- **Phase 3** `TutorialSession`에 `Dispatch` 필드·`Update()`·`WitnessRepairResult()`, `Observe()`에 사실 5개, `ApplyEffect`에 `witness-replacement` 분기, `Audit()`에 결과 미확인 열거, `Restart()`에 인계 초기화. `DriveHandoffWithFrameTime` 플래그로 시험 결정론 유지.
- **Phase 4** `fire-extinguisher-monthly.json` v1 → v2, `witness-replacement` 조건부 단계 추가. `basis`에 "설계 선택 — 직접 대응하는 조문 없음"을 명시.
- **Phase 5** `Tests/PlayMode/FpsTechnicianHandoffTests.cs` 신규 13건.
- **Phase 6** ECC csharp-reviewer 리뷰 → HIGH 1·MEDIUM 1 반영, 겨냥 시험 2건 추가. 영수증 `state/evidence/2026-09-23-technician-handoff.json`, 설계 문서·루브릭 갱신.
- **2026-09-24** 씬 배선 시도 → 생성기 실행이 소화기 11개를 제거. 씬·머티리얼·생성기 전부 커밋본으로 복원. 사용자 결정 C(보류).

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| PlayMode 1차(인계 구현 전) | — | 32/32 | pass |
| PlayMode 2차(신규 9건 추가) | 41 pass | 32 pass / 9 fail | fail → 원인 수정 |
| PlayMode 3차(허용목록·판본 수정) | 41 pass | 41/41 | pass |
| PlayMode 4차(ECC 반영, 시험 4건 추가) | 45 pass | 43 pass / 1 fail / 1 skip | fail → 시험 작성 수정 |
| PlayMode 5차(최종) | 45 pass | **45/45** | pass |

### Errors
| Error | Resolution |
|-------|------------|
| 절차 자료를 v2로 올렸더니 신규 시험 9건 전부 `절차 구조 오류` | `ProcedureRunner.Load()`가 `version!=1`을 하드 거부. 이대로 두면 **런타임에 튜토리얼이 통째로 로드 실패**할 상태였다. 판본 허용 범위와 어휘 허용 목록 확장 |
| 기존 시험 32건이 위 결함을 못 잡음 | 전부 자체 축약본 JSON만 써서 실제 배포 자료를 한 번도 읽지 않았다. `배포된_절차_자료가_실제로_로드된다` 시험 추가(스킵 아닌 실패로) |
| ECC HIGH — `Tick` 시간 이월이 죽은 코드 | `Enter()`가 `SecondsInStage=0`으로 덮어써 초과분 소실. 주석이 거짓이었다. `Enter()` 이후 초과분 재설정으로 수정 + 겨냥 시험 2건 |
| ECC MEDIUM — 판본이 어휘를 제한하지 않음 | `FactsAddedInV2`/`EffectsAddedInV2` 분리, 거부 메시지에 판본 포함 |
| `v1_자료가_v2_어휘를_쓰면_거부된다` 시험 실패 | 구현은 정상. Unity가 예고 없는 `LogError`를 실패 처리 → `LogAssert.Expect` 선언 추가 |
| `배포된_절차_자료가_실제로_로드된다` 스킵됨 | 자료가 `Resources/` 밖에 있어 `Resources.Load` 실패 → 디스크 직접 읽기로 변경 |
| **슬라이스 생성기 실행이 소화기 11개 제거** | 메뉴가 단일 배치 경로를 부른다. 12개 좌표는 생성기에 주입되지 않은 상태였고 나는 그 기록을 보고도 읽지 않았다. 씬 복원, 배선 보류(결정 C) → 2026-09-24 좌표 주입·메뉴 수정으로 **해소** |

### Actions Taken (2026-09-24 추가)
- `PlacementsV3` 정적 배열 12개를 생성기에 주입. 출처·방법과 불일치 2건(설치 높이 10cm, 고유번호 무출처)을 주석에 명시.
- `BuildMenu` → 역사 12개 배치로 변경. 단일 배치는 `개발용 · 플레이어 앞 1개만 배치` 메뉴로 분리하고 `EditorUtility.DisplayDialog` 확인 추가.
- 기술자 인계 배선 복원. 실행 결과 `배치 12개`, 씬 컴포넌트 수 확인(`FacilityInspectable` 12 · `TechnicianDispatch` 1), PlayMode **45/45** 회귀 없음.
- 새 경고: 유닛 간 최소 간격 2.00m(index 3↔5) — 상호작용 상한 3m 안이라 조준 모호 가능. 원본 좌표라 옮기지 않고 기록만 했다.

### Next Step
1. **지속 상태 이월** (`layer_coupling = state_carryover`) — G6·G9 공동 요건. 다음 최우선.
2. **다중 대상** — 소화기 12개가 배치됐으나 세션·인계가 1개에만 걸린다. `findings.md`의 A/B안 중 선택 필요하며 `one_each_hoisted` Jev 판정 재검토를 동반한다.
3. 유닛 간 2m 간격이 실제 조준 모호를 만드는지 플레이로 확인.
4. 기술자에게 몸 주기(이동 NPC·애니메이션).
