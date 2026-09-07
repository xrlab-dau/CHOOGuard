# ChooGuard Foundation 0.1.0



현재 Foundation의 결과물은 **임시 맵·3D 소품·이동·상호작용·비상상황·집결·결과를 연결한 싱글플레이 3D 게임**이다. 아래 코어는 내부 기반이며, 게임 런타임과 학교 Editor 장면/Windows 실행본 생성 방법은 [Demo 안내](Demo/README.md)를 따른다. 메뉴는 `CHOOguard → Foundation → Build Playable Demo`다. 소스 작성만으로 실제 플레이 검증을 완료했다고 표시하지 않는다.

코레일 자료 없이 UI·입력·시나리오 담당자가 연결할 수 있는 공통 훈련 코어다. 임시 역할 5개, 역할별 예시 행동 1개, 가상 팀 상태와 설명형 피드백을 제공한다. 실제 철도 절차·공식 점수·NPC 행동·네트워크는 구현하지 않는다. 모든 화면에 `KORAIL 검증 전 예시`를 표시해야 한다.

Runtime은 Unity API나 추가 패키지를 참조하지 않는 C# 7.3 코드다. `.asmdef`의 `noEngineReferences`가 이 경계를 선언한다. Unity·Desktop·VR 입력 담당자는 퀘스트를 직접 변경하지 않고 같은 `ITrainingActionHandler.Submit(in TrainingAction)`에 제출한다. 이 패키지는 Unity 프로젝트나 시각 장면 자체가 아니다.

## 로컬 또는 학교 PC에서 연결

저장소 전체를 유지한 상태에서 Unity Package Manager의 **Add package from disk**로 이 디렉터리의 `package.json`을 선택한다. Unity 프로젝트의 `Packages/manifest.json`에서 `testables` 배열에 `com.xrlab.chooguard.foundation`을 추가해야 패키지 테스트가 검색된다. Unity Editor 버전과 Test Framework 버전은 프로젝트 기준선에서 정확한 버전을 고정한다. 이 패키지의 `unity: 2022.3`은 최소 호환성 선언이며 실제 검증한 버전이라는 뜻은 아니다.

정식 예시 데이터는 저장소의 `foundation/scenarios/foundation-demo.json`이다. UI 프로젝트에서 해당 JSON을 `TextAsset`으로 가져온 뒤 다음처럼 연결할 수 있다. 실제 대상의 Anchor ID는 별도 맵/입력 계층이 제공해야 한다.

```csharp
using ChooGuard.Foundation;
using UnityEngine;

// TextAsset scenarioJson is supplied by the host project.
var profile = JsonUtility.FromJson<ScenarioProfile>(scenarioJson.text);
var session = new TrainingSession(profile); // validates and deep-copies all DTOs
var briefing = session.SelectRole("role-01");
// Show briefing.Text and briefing.Disclaimer, then acknowledge the UI interaction.
session.AcknowledgeBriefing();
var before = session.Snapshot;
var action = new TrainingAction(
    System.Guid.NewGuid().ToString("N"), session.ScenarioVersion, before.RoleId,
    before.PreStateHash, briefing.ActionId, briefing.TargetAnchorId,
    InputModality.Desktop); // VR adapter changes only modality and how input is collected.
var result = session.Submit(action);
// Present result.Feedback only when result.Accepted is true.
```

위 예시는 API 연결을 위한 코드이며, UI가 자동으로 안내를 확인 처리하거나 입력 전에 행동을 제출하도록 구성해서는 안 된다. `AvailableRoles`로 선택 목록을 만들고 `Snapshot.Phase`, `TeamStateProvider.States`, `Feedback`을 읽어 표현한다. 모든 호출은 한 소유 스레드(일반적으로 Unity 메인 스레드)에서 실행한다.

## 계약과 변경 경계

- `TrainingAction`은 `AttemptId / ScenarioVersion / RoleId / PreStateHash / ActionId / TargetAnchorId / Modality`를 갖는다.
- 흐름은 `RoleSelection → Briefing → Ready → Feedback`이다. 역할 재선택이나 재시도 흐름은 새 `TrainingSession`으로 시작한다.
- `ScenarioProfile`, `RoleDefinition`, `VirtualTeamEventDefinition`은 JSON과 일치하는 공개 필드를 가진 DTO다. 생성자 검증 이후 원본 DTO를 변경해도 진행 중 세션에 영향을 주지 않는다.
- 이 파운데이션 fixture는 역할 5개, 서로 다른 행동·Anchor 1개씩, 사용자의 `completed` 퀘스트와 다른 4개 역할의 `notified` 이벤트를 요구한다. 다른 4개의 상태 변경은 예시 UI 반응이며 그 직무의 퀘스트를 대신 수행한 결과가 아니다. 다른 직무의 이벤트 상태를 `completed`로 지정한 데이터는 거부한다.
- `RoleActionFixture`는 선택·안내 확인 후 `CreateRoleActionFixture()`로 얻는다. 대표 여부와 고정된 사전 상태 해시, 기대 퀘스트·팀 이벤트·피드백 코드를 담는다.
- 잘못된 시나리오 버전, 역할, 행동, Anchor, 입력 방식, 사전 상태, 빈 시도 ID, 성공 시도의 재전송을 거부한다. 거부는 퀘스트·팀·성공 시도 기록을 바꾸지 않는다. 재전송 방지는 현재 메모리 세션 안에서만 적용된다.
- `ActionResult.StateHash`는 처리 이후 상태 해시이며, 거부 시 처리 이전 해시다. 행동을 생성할 때는 그 시점의 `Snapshot.PreStateHash`를 사용한다.
- 해시는 동등성·오래된 입력 판정을 위한 값이다. 서명이나 네트워크 인증 수단이 아니다. 실제 Anchor 존재·장면 계층·충돌 경계는 호스트 프로젝트에서 별도로 검증한다.

## 결정적 해시와 테스트

문자열 토큰을 UTF-8로 인코딩한 뒤 각 토큰 앞에 UTF-8 바이트 길이의 ASCII 십진수와 `:`를 붙이고 이어 붙여 SHA-256을 계산한다. 결과는 소문자 16진수다. 문화권별 숫자 표현을 사용하지 않는다.

프로필 토큰은 `chooguard.foundation.profile.v1`, `schemaVersion`, `scenarioId`, `scenarioVersion`, `mapId`, `provisional`의 `true`/`false`, `disclaimer`, `representativeRoleId` 순서다. 다음으로 `roleId`의 ordinal 순서로 각 역할의 `roleId`, `temporaryDisplayName`, `briefing`, `actionId`, `targetAnchorId`, `expectedQuestState`, `expectedFeedbackCode`, `feedbackText`를 붙이고, 그 역할의 이벤트를 `roleId` ordinal 순서로 정렬하여 `roleId`, `eventCode`, `state`를 붙인다.

세션 토큰은 `chooguard.foundation.state.v1`, 프로필 해시, `TrainingPhase` 열거형 이름, 선택 역할 ID(선택 전 빈 문자열), 퀘스트 상태(`pending`/`completed`) 순서다. 마지막으로 역할 ID ordinal 순서의 팀 상태 `RoleId`, `State`를 붙인다. 시작 팀 상태는 전부 `idle`이다. `AttemptId`와 `Modality`는 의미 동등성을 위해 해시에 포함하지 않는다.

NUnit EditMode 테스트는 유효·무효 행동, 무변경 거부, 재전송, 안내 단계, DTO 변경 격리, 5개 역할의 VR/Desktop 의미 동등성을 확인한다. `CanonicalScenarioTests`는 Unity `JsonUtility`로 실제 저장소 fixture를 읽어 10개 경로와 Python으로 독립 계산한 역할별 golden 사전 상태 해시를 검증한다. 실제 fixture 테스트는 외부 로컬 UPM 의존성 기준으로 패키지에서 `../../foundation/scenarios/foundation-demo.json`을 읽으므로 패키지만 별도 복사하면 실패한다.

현재 로컬 Unity에서 패키지 컴파일과 EditMode·PlayMode 자동 시험을 통과했고, 합성 장면을 생성해 역할 선택 화면을 확인했다. 전체 수동 완주와 학교 PC의 Windows/VR 검증은 남아 있다. Python 검사 결과는 해당 명령의 데이터 검사 범위만 나타낸다.
