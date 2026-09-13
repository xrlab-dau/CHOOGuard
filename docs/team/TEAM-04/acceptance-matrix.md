# TEAM-04 사용자 흐름 수용 행렬 (fixture → UI/행동/검증대응)

- 산출물: `foundation/team-fixtures/TEAM-04/user-flows.json` (짝 문서)
- mode: `isolated_proposal` · canonicalWriteAllowed: `false` · phase: `candidate`
- sourceRef: `ac7f84edb56477b2d7039e20fec91e7e51b0d3e6` (작업공간 HEAD 와의 커밋 수준 대조는 미실행 — 아래 §7)
- 이 문서는 검토 가능한 유계 후보이며 accepted/approved/final/complete 로 표기하지 않는다. 어떤 PASS 도 런타임·설치·네트워크·Unity·게시·병합 권한을 만들지 않는다.
- KORAIL 검증 전 예시 수준의 합성 훈련 fixture다. 실제 철도 절차·공식 직무·공식 점수·현장 성능·기관 수용이 아니다.

## 1. 흐름 대응표 (정상·실패·재시작)

| flowId | 구분 | 초기 상태 | 행동(UI 표면) | 관측 | 기대 상태 | 계약 위치(file:line) |
|---|---|---|---|---|---|---|
| F01 | normal | `{"phase": "Ordinary", "paused": false, "activeIncident": null, "playerControl": "enabled"}` | **E 상호작용 (설비/이용객)** → `StationWorldController.TryInteract` | `{"feedback": "정상 운영", "traceAction": "routine-inspection", "accepted": true, "equipmentVisual": "normal"}` | `{"phase": "Ordinary", "revisionIncremented": false, "incidentCreated": false, "guideRoute": "hidden"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:203`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:209`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:211`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:212` |
| F02 | normal, failure | `{"phase": "Ordinary", "paused": false, "seconds": ">= nextOnset", "activeIncident": null}` | **프레임 진행 (Tick)** → `StationWorldSession.Tick` | `{"traceAction": "onset", "incidentIdFormat": "incident-NNN", "discovered": false, "affectedNpcIds": "site 후보 중 2명 이상"}` | `{"phase": "Incident", "revisionIncremented": true, "revealsLocationBeforeObservation": false, "escalationRule": "미보고 상태로 EscalatesAt 도달 시 escalated 기록. 이는 통신 두절 주입이며 물리 위험 전파로 서술하지 않는다."}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:93`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:108`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:109`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:111` |
| F03 | normal, unobserved | `{"phase": "Incident", "discovered": false, "paused": false}` | **관측 지점 확인 (anchor-01 또는 signal-<site>)** → `StationWorldController.DiscoverVisibleSignal / StationWorldSession.Act(Observe)` | `{"gate": "카메라 시야각 dot>=0.7, 거리<=10m, 큐 오브젝트 레이캐스트 히트", "accepted": true, "observedAt": "Seconds"}` | `{"discovered": true, "guideRouteBecomesVisible": true, "equipmentVisual": "indication"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:186`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:191`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:194`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:261`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:124`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:125` |
| F04 | normal | `{"phase": "Incident", "discovered": true, "reported": false}` | **보고 경로 상호작용 (anchor-03 또는 route-console)** → `StationWorldSession.Act(Report)` | `{"accepted": true, "reportedAt": "Seconds", "equipmentVisual": "reported"}` | `{"reported": true, "reportAnchor": "RequiresNotice 여부에 따라 route-console 또는 anchor-03"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:64`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:128`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:129` |
| F05 | normal | `{"phase": "Incident", "discovered": true, "notified": false, "selectedRoute": null}` | **안내 전파 장치 anchor-02, 경로 표지 choose-<site>** → `StationWorldSession.Act(Notify) → Act(SelectRoute)` | `{"accepted": true, "noticeActive": true, "selectedRoute": "현재 사건 site 가 아닌 구역"}` | `{"notified": true, "selectedRoute": "사건 site 와 다름", "canLeadUnlockedWhen": "Reported && SelectedRoute != null && (!RequiresNotice || Notified)"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:63`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:66`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:132`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:135`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:137` |
| F06 | normal | `{"phase": "Incident", "canLead": true, "recruited": "0"}` | **대상 이용객에게 다가가 E** → `StationWorldController.Aim → StationWorldSession.Act(Recruit)` | `{"accepted": true, "recruitedAdd": "npcId", "caption": "[E] 인솔 · 함께 이동"}` | `{"leaderAssignedTo": "제출자 자신", "routeLocked": true, "escortPortal": "선택 경로 waypoint"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:66`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:141`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:142`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:199`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:203`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:206`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:216`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:237` |
| F07 | normal | `{"phase": "Incident", "crossedChosenRoute": true, "arrived": "전원"}` | **집결 인원 확인 단말 (assembly-register)** → `StationWorldSession.Act(CloseIncident)` | `{"accepted": true, "feedback": "인솔 기록 완료 · 현장 운영 복구", "debriefRecorded": true}` | `{"phase": "Recovery", "recoverAt": "Seconds + recoverySeconds", "historyEntry": "StationDebrief 1건"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:223`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:245`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:148`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:152` |
| F08 | assembly-failure, failure | `{"phase": "Incident", "arrived": "< affected", "recruited": "< affected"}` | **집결 단말 조작** → `StationWorldSession.Act(CloseIncident)` | `{"accepted": false, "trace": "close-rejected"}` | `{"phase": "Incident 유지", "revisionIncremented": false, "debriefNotRecorded": true}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:223`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:224`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:145`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:146`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:148` |
| F09 | pause | `{"phase": "Incident", "paused": false, "playerControl": "enabled"}` | **Esc 토글, 창 포커스 상실, 입력 불가, 경계 밖 낙하** → `StationWorldController.Update / OnApplicationFocus → PauseWorld·ResumeWorld` | `{"uiPanel": "일시정지 / 대응 기록", "timeFrozen": true, "playerControl": "disabled"}` | `{"paused": true, "tickAdvance": 0, "actionsRejected": true, "resumeRequiresInputAvailable": true}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:89`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:90`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:291`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:292`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:295`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:297`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:117` |
| F10 | input-error | `{"phase": "Incident", "inputAvailable": false, "inputError": "입력 장치 초기화 실패: <원인>"}` | **입력 장치 초기화 실패 또는 읽기 예외** → `DemoInput.Available / DemoInput.Read / DemoPlayerController.InputAvailable` | `{"uiPanel": "일시정지", "controlEnabled": false, "fallbackValue": "Movement=zero, Look=zero, Interact=false"}` | `{"worldPaused": true, "noMovementOrInteraction": true, "recoverableByRetry": true}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoInput.cs:14`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoInput.cs:15`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoInput.cs:22`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoInput.cs:32`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoInput.cs:95`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoPlayerController.cs:20`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoPlayerController.cs:21`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:291` |
| F11 | restart | `{"phase": "Recovery 또는 임의", "historyReversed": "가능", "roleSelection": "role-01..05"}` | **새 근무 시작 버튼 / legacy Restart** → `StationWorldController.StartShift / DemoFlow.Restart` | `{"trace": "shift-start", "detail": "seed=...; profile=...; role=...; synthetic-unverified", "previousRecordSaved": "IsRunning 이면 SaveRecord"}` | `{"newSessionCreated": true, "phaseReset": "Ordinary", "equipmentReset": true, "portalsReset": true, "npcRoutineConfigured": true, "playerTeleported": true, "legacyFlowReset": "RoleSelection, SelectedRole=null, LastAction=null, team states idle"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:69`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:72`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:81`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:41` |
| F12 | stale-input, failure | `{"phase": "Incident", "expectedRevision": "N-1", "revision": "N"}` | **이전 revision 으로 행동 제출** → `StationWorldSession.Act` | `{"accepted": false}` | `{"phase": "Incident 유지", "revisionIncremented": false}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:118`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:119` |
| F13 | normal | `{"phase": "RoleSelection", "roles": ["role-01", "role-02", "role-03", "role-04", "role-05"]}` | **역할 선택 → 브리핑 → 사건 시작 → 목표 상호작용 → 집결** → `DemoFlow.SelectRole / BeginIncident / TryInteract / TryReachAssembly` | `{"accepted": true, "teamEvents": "다른 4역할에 notified 4건", "feedbackCode": "synthetic-action-recorded", "disclaimer": "ScenarioValidation.RequiredDisclaimer"}` | `{"phaseChain": ["RoleSelection", "Briefing", "Incident", "ReachAssembly", "Results"], "questState": "completed", "otherRolesQuestState": "notified (완료 아님)"}` | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:6`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:41`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:55`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:57`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:64`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:78`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/ScenarioValidation.cs:8`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/ScenarioValidation.cs:11`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/ScenarioValidation.cs:20`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/ScenarioValidation.cs:55`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/VirtualTeamSimulator.cs:8` |
| F14 | unobserved, failure | `{"commandKind": "Report", "targetIncident": "미발견 또는 비활성"}` | **WorldCommand(Report) 제출** → `AuthoritativeShift.Submit` | `{"receipt": "NotObserved (미발견 사건) / OutOfReach (원격)"}` | `{"accepted": false, "observerStateLeak": false, "remoteStreaming": false}` | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:124`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:131`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:209`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:211`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:213`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/WorldContracts.cs:108` |

## 2. 실패/복구 대응 (failure → recovery → restart)

| flowId | 실패 트리거 | 기대 | 계약 위치 | 복구 | 재시작 |
|---|---|---|---|---|---|
| F02 | 미발견 상태에서 관측 외 행동 시도 | 먼저 현장 또는 상황 패널에서 상태 확인 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:120` | {"required": false} | {"newSession": false} |
| F03 | 이미 관측됨 | 이미 관측한 상태 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:124` | {"required": false} | {"newSession": false} |
| F03 | 확인되지 않은 관측 지점 | 확인되지 않은 관측 지점 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:125` | {"required": false} | {"newSession": false} |
| F03 | 관측 전 안내 경로 노출 시도 | guide 는 Incident && Discovered && SelectedRoute!=null 일 때만 갱신되고 아니면 Hide | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:261` | {"required": false} | {"newSession": false} |
| F04 | 다른 통신 경로로 보고 | 이 통신 경로에서는 보고할 수 없음 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:128` | {"required": false} | {"newSession": false} |
| F04 | 중복 보고 | 보고 기록 있음 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:129` | {"required": false} | {"newSession": false} |
| F05 | 안내 전파 장치가 아닌 대상 | 안내 전파 장치가 아님 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:132` | {"required": false} | {"newSession": false} |
| F05 | 현재 사건 site 를 경로로 선택 | 현재 사용할 수 없는 경로 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:135` | {"required": false} | {"newSession": false} |
| F05 | 인솔 시작 후 경로 변경 | 인솔 시작 후 경로 유지 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:137` | {"required": false} | {"newSession": false} |
| F06 | 인솔 선행조건 미충족 | 보고·사용 가능 경로·필요한 안내 전파 확인 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:141` | {"required": false} | {"newSession": false} |
| F06 | 사건 영향 대상이 아님 | 현재 인솔 대상이 아님 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:142` | {"required": false} | {"newSession": false} |
| F06 | 원격/비근접 인솔 | Aim 이 InteractionReach 내 레이캐스트에 실패하면 상호작용 자체가 성립하지 않고 interaction-miss 만 기록된다 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:199` | {"required": false} | {"newSession": false} |
| F07 | 경로 통과/거리/도착 미충족 | 선택 경로 통과 · 인솔 인원 도착 확인 필요 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:224` | {"required": true, "detail": "recoverySeconds 경과 시 Ordinary 복귀, Active=null, recovered 기록, 다음 onset 예약"} | {"newSession": false} |
| F08 | 인솔하지 않은 인원 도착 기록 | 인솔하지 않은 인원 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:145` | {"required": true, "detail": "거부 후에도 phase 는 Incident 로 남고 재시도 가능"} | {"newSession": false} |
| F08 | 중복 도착 기록 | 이미 집결 기록 있음 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:146` | {"required": true, "detail": "거부 후에도 phase 는 Incident 로 남고 재시도 가능"} | {"newSession": false} |
| F08 | 도착 인원 미충족 상태에서 종료 | 집결 확인 또는 도착 인원 미충족 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:148` | {"required": true, "detail": "거부 후에도 phase 는 Incident 로 남고 재시도 가능"} | {"newSession": false} |
| F09 | 일시정지 중 행동 | 일시정지 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:117` | {"required": true, "detail": "ResumeWorld 가 paused=false, showDebrief=false, control enabled"} | {"newSession": false} |
| F09 | 입력 불가 상태에서 재개 | ResumeWorld 가 player.InputAvailable 을 요구 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:90` | {"required": true, "detail": "ResumeWorld 가 paused=false, showDebrief=false, control enabled"} | {"newSession": false} |
| F10 | 입력 장치 초기화 예외 | 입력 장치 초기화 실패: ... | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoInput.cs:32` | {"required": true, "detail": "DemoInput.Retry() 가 Error 를 지우고 입력 장치를 재구성"} | {"newSession": false} |
| F10 | 입력 읽기 예외 | 입력 장치를 읽을 수 없습니다: ... | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoInput.cs:95` | {"required": true, "detail": "DemoInput.Retry() 가 Error 를 지우고 입력 장치를 재구성"} | {"newSession": false} |
| F11 | 알 수 없는 역할로 시작 | Unknown role. | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:72` | {"required": false} | {"newSession": true, "historyCap": "history 20건 초과 시 선입 제거(StationWorldSession.cs:152)"} |
| F12 | 오래된 revision | 상태가 변경됨 · 다시 확인 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:119` | {"required": true, "detail": "최신 revision 으로 재시도"} | {"newSession": false} |
| F12 | 다른 사건 id 로 입력 | 현재 사건과 다른 입력 | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:118` | {"required": true, "detail": "최신 revision 으로 재시도"} | {"newSession": false} |
| F13 | 브리핑 전 사건 시작 | A displayed role briefing is required before the incident. | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoFlow.cs:57` | {"required": false} | {"newSession": true} |
| F13 | 다른 역할의 행동을 대신 수행 | VirtualTeamSimulator 는 다른 역할의 quest/procedure 를 수행하지 않음 | `Packages/com.xrlab.chooguard.foundation/Runtime/VirtualTeamSimulator.cs:8` | {"required": false} | {"newSession": true} |
| F14 | 관측 전 사건 보고 | NotObserved | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:211` | {"required": true, "detail": "Discover 로 ObservedIds 등록 후 재시도"} | {"newSession": false} |
| F14 | 관측을 원격 스트리밍 권한으로 사용 | OutOfReach — 보고는 포착된 관측이며 사건을 원격 스트리밍할 권한이 아니다 | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:213` | {"required": true, "detail": "Discover 로 ObservedIds 등록 후 재시도"} | {"newSession": false} |
| F14 | 미발견 사건 위치 노출 | Observe() 는 Incident 이면서 Active 이고 ObservedIds 에 포함될 때만 반환 | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:131` | {"required": true, "detail": "Discover 로 ObservedIds 등록 후 재시도"} | {"newSession": false} |

## 3. 부정 사례 (mustFail)

| caseId | 이름 | 이어야 하는 결과 | legacy 계약 | FMP 계약 | 관측 가능성 |
|---|---|---|---|---|---|
| N01 | 미발견 사건 위치 노출 | 거부/미생성 (mustFail) | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:120`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:261` | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:131`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/WorldContracts.cs:108` | ObservedState 에 시드/미래 사건/해법 단계/원장/서버 체크포인트 필드가 존재하지 않는다(WorldContracts.cs:108). 미발견 Incident 엔티티는 관측 결과에서 제외된다(AuthoritativeShift.cs:131). |
| N02 | 원격 인솔 | 거부/미생성 (mustFail) | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:199`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:141` | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:213`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:256`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/VoiceTokenIssuer.cs:35`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/VoiceTokenIssuer.cs:39`<br>`Packages/com.xrlab.chooguard.foundation/Multiplayer/Runtime/VoiceRadio.cs:120` | 관측/보고는 근접·시야 조건을 요구하고, 보고는 원격 스트리밍 권한이 아니다. command 음성 채널은 교관 외에는 수신 전용(CanPublish=false)이다. |
| N03 | 역할 대행 | 거부/미생성 (mustFail) | `Packages/com.xrlab.chooguard.foundation/Runtime/VirtualTeamSimulator.cs:8`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/ScenarioValidation.cs:55`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:256` | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:248`<br>`Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:254` | 가상 팀 시뮬레이터는 다른 역할의 quest 를 완료 처리하지 않는다(상태 notified 고정, ScenarioValidation.cs:55). HandOffEvacuee 는 소유자·근접·역할 조건 불충족 시 RoleDenied/OutOfReach. |
| N04 | 공식 점수/평가 생성 | 거부/미생성 (mustFail) | `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs:152`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:345`<br>`Packages/com.xrlab.chooguard.foundation/Demo/Runtime/DemoGameController.cs:405` | `Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs:72` | 대응 기록 화면은 '공식 평가 아님' 을 표기하고(StationWorldController.cs:345), 가상 팀 패널은 '점수·합격·철도 절차 준수 판정은 제공하지 않습니다' 를 명시한다(DemoGameController.cs:405). 기록에는 점수·등급·합격 필드가 없다. |

## 4. 5직무·Desktop/VR 의미 대조

| roleId | 임시 표시명 | actionId | targetAnchorId | 기대 quest | 이벤트(4, notified) | controller 대조 |
|---|---|---|---|---|---|---|
| role-01 | 상황 확인 · 임시 역할 | action-01 | anchor-01 | completed | 4 | Observe 는 anchor-01 또는 signal-<site> 를 관측 진입점으로 사용한다(StationWorldSession.cs:125). 역할 계약 targetAnchorId 와 충돌하지 않는다. |
| role-02 | 경보 전달 · 임시 역할 | action-02 | anchor-02 | completed | 4 | Notify 는 anchor-02 만 허용한다(StationWorldSession.cs:132). 대상 일치. |
| role-03 | 상황 보고 · 임시 역할 | action-03 | anchor-03 | completed | 4 | ReportAnchor 는 RequiresNotice 시 route-console, 그 외 anchor-03 이다(StationWorldSession.cs:64). 역할 계약 anchor-03 은 기본 경로와 일치한다. |
| role-04 | 방향 안내 · 임시 역할 | action-04 | anchor-04 | completed | 4 | 경로 선택은 choose-<site> 표지로 진입하고 역할 앵커와 별도 표면이다(StationWorldController.cs:220). 충돌 없음. |
| role-05 | 통로 확인 · 임시 역할 | action-05 | anchor-05 | completed | 4 | 역할 앵커가 관측/보고/전파 앵커가 아니면 device-inspected 기록 후 TryRoleAction 만 시도한다(StationWorldController.cs:229-230). 충돌 없음. |

- 대조: TrainingSession.ValidateAction `Packages/com.xrlab.chooguard.foundation/Runtime/TrainingSession.cs:106` / StationWorldController.TryRoleAction `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldController.cs:252` / ScenarioValidation.Validate `Packages/com.xrlab.chooguard.foundation/Runtime/ScenarioValidation.cs:11`
- M4-01 입력 상태: artifact:32:M4-01-fixture:candidate 는 작업공간에 없어 소비하지 않았다. 위 5행은 공개 baseline foundation/scenarios/foundation-demo.json /roles 에서만 파생했고 M4-01 을 근거로 인용하지 않는다.

| 항목 | 계약 수준 의미 | 실제 기기 |
|---|---|---|
| modality enum | ["VR", "Desktop"] `Packages/com.xrlab.chooguard.foundation/Runtime/TrainingContracts.cs:7` | 아래 미검증 참조 |
| 의미 대조 | 동일한 TrainingAction 계약(attemptId, scenarioVersion, roleId, preStateHash, actionId, targetAnchorId)을 두 modality 가 공유하며, modality 는 별도 필드이고 기대 상태·가상 팀 이벤트·피드백을 바꾸지 않는다. (근거 `Packages/com.xrlab.chooguard.foundation/Runtime/TrainingSession.cs:110`) | ValidateAction 은 modality 가 VR 도 Desktop 도 아니면 InvalidModality 를 반환할 뿐, 둘 사이에 다른 기대값을 두지 않는다. |
| legacy 프레젠테이션 한계 | legacy DemoFlow.TryInteract 는 Desktop 고정으로 제출한다(DemoFlow.cs:68). VR 경로는 StationWorldController.TryInteract 의 modality 파라미터 쪽에만 존재하며, 이 후보는 그 경로를 실제 기기로 검증하지 않았다. |  |

**실제 기기 미검증(구분)**: result=`NOT_RUN`, reasonKind=`조건 미성립`. 실제 HMD/기기 시험은 work-graph item 72 의 nonGoals 이며 이 phase 에서 원리상 수행 대상이 아니다. 본 후보는 문서·계약 fixture 이므로 기기 성능/착용감/설치를 검증하지 않는다. Desktop/VR 의미 대조는 계약 수준에서만 주장하며, 실제 기기 검증 결과로 승격하지 않는다.

## 5. legacy / FMP source layer 연결

| layer | systems | sourceIds | scope |
|---|---|---|---|
| legacy | DemoFlow, StationWorldSession, StationWorldController, TrainingSession, DemoInput | S-legacy-roles, S-legacy-station-session, S-legacy-station-controller, S-legacy-demo-flow, S-legacy-demo-input, S-legacy-demo-player, S-legacy-demo-ui, S-role-contract, S-action-contract, S-scenario-validation, S-virtual-team | 싱글플레이 합성 훈련. 5역할·3지점·6인원·동시 1사건. |
| fmp-target | AuthoritativeShift, IncidentDirector, SessionAdmission, VoiceTokenIssuer, VoiceRadio | S-fmp-world, S-fmp-regions, S-fmp-incident-director, S-fmp-command, S-fmp-contracts, S-fmp-admission, S-fmp-voice-token, S-fmp-voice-radio | 서버 권위 멀티플레이 목표. 13구역·NPC100·동시사건2·클라이언트 상한 20. legacy 자동 테스트 통과를 FMP 부하·WAN·음성 수용 증거로 사용하지 않는다. |

## 6. 검증 영수증 (C01)

`user-flows.json` 의 `verification.C01` 과 동일한 요약이다. 이 영수증은 유계 자기점검이며 정책 수용이 아니다.

| 필드 | 값 |
|---|---|
| checkId | C01 |
| declaredCommand | 각흐름을초기→행동→관측→기대상태로합성검토하고기존controller/role계약과대조한다. |
| expectation | 정상·pause·input오류·미관측·집결실패·재시작을구분 |
| negativeCase | 미발견사건위치노출/원격인솔/역할대행 |
| result | PASS (아래 실행 증거 있음) |
| artifactPath | `foundation/team-fixtures/TEAM-04/user-flows.json` (영수증을 담은 파일 자신) |
| artifactDigest | scheme=sha256, scope=self, value=null, notEmbeddableReason=파일은 자기 자신의 다이제스트를 담을 수 없다 |

실행한 검사(저장소 밖 스크래치패드의 검사기, 저장소에는 검사기를 추가하지 않음):

- `python3 <스크래치패드>/verify_team04_fixtures.py structure`
- `python3 <스크래치패드>/verify_team04_fixtures.py refs`
- `python3 <스크래치패드>/verify_team04_fixtures.py counts`
- `python3 <스크래치패드>/verify_team04_fixtures.py presence`
- `python3 <스크래치패드>/verify_team04_fixtures.py matrix`
- `python3 <스크래치패드>/verify_team04_fixtures.py hygiene`

## 7. 드리프트·중단 조건·미검증

### 드리프트

- sourceRef: `ac7f84edb56477b2d7039e20fec91e7e51b0d3e6` / HEAD 커밋 id: UNKNOWN (`NOT_RUN`, 조건 미성립)
- 요구된 local_unpublished 문서 3종(docs/choo-guard-native-acceptance.md, docs/choo-guard-native-team-contracts.md, docs/choo-guard-foundation-multiplayer.md)은 이 작업공간에 존재하지 않는다. work-order 072 는 이들을 local_unpublished, capture 시 local/develop 모두 null 로 선언하므로 부재 자체는 그 선언과 모순되지 않는다. 다만 이 후보는 이 문서들에서 파생한 내용을 포함하지 못한다.
- hard predecessor 3건(artifact:32, artifact:120, artifact:131)의 선언 경로가 작업공간에 없다. stopCondition 1 에 따라 해당 입력을 쓰는 수용만 중단하고 hardInputs 에 등록했다.
- artifact:120:foundation-published-source:accept 의 선언 경로는 docs/context/publication/team-baseline.json 이다(work-graph items[number=120].outputs). 이 후보는 그 경로를 hardInputs 에 정확히 기록했다.
- ref 트리 대조: `NOT_RUN` — 기준 ref 트리의 파일을 읽으려면 git show 가 필요한데 git 이 금지되어 있다. 'ref 에서 볼 수 없다'는 서술은 하지 않는다 — 대조를 시도하지 않았다는 사실만 기록한다.

### 중단 조건 준수

- **#1 필수 입력 부재** — `VIOLATED_INPUT_RECORDED`: artifact:32/120/131 의 선언 경로가 작업공간에 없고, guarded local_unpublished 3종도 없다. 이들을 쓰는 수용을 중단하고 hardInputs / guardedLocalUnpublishedReads 에 결함으로 남겼다. 그 입력 없이도 성립하는 부분(공개 baseline 기반 flows)만 작성했다.
- **#2 출력 lock holder** — `UNKNOWN`: lock holder/scope/base 대조는 git/lease 도구를 요구하는데 이 세션은 git 을 금지받았다. requiredLocks 는 비어 있고(#72), resourceBindings 는 workspace-files-v1 이다. 같은 작업공간에 이전의 미검증 초안이 남아 있었고, 이 후보가 그 경로를 전면 재검증·재작성했다. 다른 holder 와의 물리적 충돌 여부는 확인하지 못했으므로 UNKNOWN 으로 남긴다.

### 미검증/미실행

- sourceRef 트리와 작업공간 HEAD 의 커밋 수준 동일성(git 금지로 미확인).
- 선언 ref 6490956d 시점 foundation-demo.json 의 내용이 작업공간 사본과 같은지(다이제스트 일치만 확인).
- M4-01 5직무 fixture 의 실제 필드(부재로 소비하지 않음).
- FMP-09a 사건 카탈로그의 검토/미검토 구분(부재로 소비하지 않음).
- #120 qualified manifest 의 경로·다이제스트(부재).
- #72 출력 lock 의 기존 holder 존재 여부(lease 도구 접근 불가).
- 실제 HMD/기기 검증 결과(nonGoal, 미실행).
- FMP 부하·WAN·음성 수용(이 후보 범위 밖).

