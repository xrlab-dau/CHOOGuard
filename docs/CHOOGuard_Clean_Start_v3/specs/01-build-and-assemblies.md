# K01 · 새 Unity 빌드와 assembly 경계
**소유:** CS-BOOT.01–.03. 이 문서는 설계이며 새 Unity 프로젝트·Player를 실행한 기록이 아니다.

## 최초 부팅의 순환 제거
CS-BOOT.01은 Camera + Canvas + EventSystem + 정적 표식만 있는 smoke scene을 만든다. 아직 없는 CompositionRoot나 Domain을 참조하지 않는다. CS-BOOT.02의 InstallCompositionRoot가 나중에 런타임을 조립한다. 원 scene 수동 편집은 해당 산출물 담당자에게 인계하며 서로 다른 작업이 동시에 scene 파일을 쓴다고 가정하지 않는다.

## 재현 가능한 환경
설계 기준은 Unity 6.3 LTS 계열/URP/Windows x64 native/Mono 개발 build다. 정확한 patch와 uGUI·TMP·Input System·Test Framework는 CS-BOOT.01에서 설치 가능한 정식 호환 조합을 하나 선택해 ProjectVersion·manifest·lock·OS·backend·native library 해시에 고정한다. 잠금 전에는 임의 버전으로 실행한 결과를 비교하지 않는다. IL2CPP는 별도 qualification이며 Mono 성공을 이식하지 않는다. URP 도입으로 실제 렌더 정확도나 성능이 입증되는 것은 아니다.

## 컴파일 구조
`contracts/assembly-layout.json`이 13개 production/editor root와 의존성을 정의한다. Contracts/Domain/Application/Content/Persistence/Experiments/Scenarios/Simulation/Authoring은 UnityEngine을 참조하지 않는다. World/Presentation은 Contracts를 통해 읽기 상태와 명령을 사용한다. App만 구현을 조립한다. Editor 코드는 Editor-only assembly 아래 둔다. 예: FixtureBuilder와 AssetImportPolicy는 World runtime 디렉터리가 아니라 Editor에 둔다.

SQLite 네이티브 provider·문서 exporter는 adapter다. Domain은 provider/Unity/solver/화면 클래스를 참조하지 않는다. reflection-based service locator로 의존성 규칙을 우회하지 않는다. 각 C#은 가장 깊은 root의 asmdef에 속해야 하며 의도치 않은 Assembly-CSharp 유입을 compile gate에서 실패시킨다. Unity custom assembly에서 predefined assembly를 참조할 수 없으므로 빠진 root는 단순 미관 문제가 아니다. [AUD-UNITY-ASSEMBLY]

## 시험 실행 계약
CS-BOOT.03이 아래 스크립트를 실제 구현한다. 지금 이 명세 ZIP에는 Unity 실행기나 제품 시험 소스가 없으며 아래 명령의 실행을 보고하지 않았다.

```powershell
pwsh scripts/Run-UnityTests.ps1 -EditorPath $env:UNITY_EDITOR_PATH -ProjectPath . -Platform EditMode -Category CS-OPS -ResultsPath artifacts/editmode
pwsh scripts/Run-UnityTests.ps1 -EditorPath $env:UNITY_EDITOR_PATH -ProjectPath . -Platform PlayMode -Category CS-PLAY -ResultsPath artifacts/playmode
pwsh scripts/Build-Player.ps1 -EditorPath $env:UNITY_EDITOR_PATH -ProjectPath . -OutputPath artifacts/player
pwsh scripts/Run-PlayerAcceptance.ps1 -PlayerPath artifacts/player/CHOOGuard.exe -Protocol qualification/player/CS-PLAY.01.json
```

스크립트는 경로·editor·프로젝트를 검사하고 새 result 디렉터리에 NUnit XML·Editor log·exit code·source/package digest를 기록한다. XML 없음/이전 실행 XML/실행 시험 0개/timeout은 PASS가 아니다. 필요한 런타임이 없으면 status=NOT_RUN, nonzero exit를 반환한다. CI에는 실패 또는 별도 차단 상태가 보이며 녹색 합격으로 대체하지 않는다.

EditMode는 순수 코드·직렬화·알고리즘, PlayMode는 Scene·Canvas·포커스·레이캐스트, Player acceptance는 OS IME·native SQLite·파일권한·입력·렌더를 검증한다. PlayMode 성공도 Windows Player 시험을 대체하지 않는다.
