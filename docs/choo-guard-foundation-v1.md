# CHOOguard Foundation 개발 기준 v1.1 — 플레이 가능한 3D 시제품



2026-09-06 사용자 지시로 확정한 착수 범위. 제품 요구사항과 최종 MVP 수용 기준은 기존 요구사항 v1.1·아키텍처 v4.2를 유지한다.

Foundation은 **MVP의 MVP: 임시 맵과 각종 3D 오브젝트 안에서 혼자 이동·상호작용하고 임시 비상대응 상황을 끝까지 플레이할 수 있는 작은 3D 시뮬레이션 게임**이다. KORAIL 데이터·답변 없이 만든다. 공통 코어·계약·테스트·설정은 게임을 구성하는 내부 기반이며 그 파일들만으로 Foundation 완료를 선언하지 않는다.

## 제품 포지셔닝

1명의 실제 조작자가 VR 또는 Desktop으로 5개 임시 직무 중 하나를 자유롭게 선택한다. 대표 직무는 역할 선택 → 안내 → 행동 → 가상 팀 변화 → 설명형 피드백까지 완주하고, 나머지 네 직무는 서로 다른 핵심 행동을 수행한다. 입력 방식이 달라도 같은 계약·Quest·팀 이벤트·피드백 의미를 사용한다.

가상 팀은 규칙으로 반응하며 실제 네트워크 멀티플레이나 빈 직무를 대신 수행하는 NPC가 아니다. 공식 점수·이수 판정·SSO·LMS는 MVP 범위 밖이다. 직무 이름·행동·인계는 교체 가능한 데이터이며 모든 합성 예시에 `KORAIL 검증 전 예시`를 표시한다.

실제 환경과 비상대응 매뉴얼에 최대한 충실한 3D Flat Art 게임이 제품 방향이다. 공간 구조·설비 위치·동선·절차의 충실도를 유지하면서 외형은 단색·무광·면 단위 음영으로 단순화한다. Foundation은 확정 자료 수신 전 교체 가능한 합성 맵과 절차 초안으로 게임을 개발한다. 합성 경로 완성이 MAP-01 또는 최종 P1 촬영·처리 이력의 충족을 의미하지 않는다.

## 첫 playable의 완료 기준

첫 실행 경로는 Desktop 싱글플레이다. 전체 MVP의 VR 지원을 제거하는 결정이 아니며, VR은 같은 행동 코어에 후속 연결한다.

1. 작은 임시 역사형 맵에 대합실·통로·집결 공간과 통과 가능한 출입구가 있다.
2. 벽·기둥·벤치·키오스크·표지·가상 위험 표시 등 실제 3D 오브젝트가 배치된다.
3. 5개 임시 역할 중 하나를 선택하고 가상 사건 안내를 읽은 뒤 플레이한다.
4. WASD·마우스로 직접 이동·시점을 조작하고, 가까이서 바라본 3D 대상에 E로 상호작용한다.
5. 대상은 상황 패널·경보 체험 버튼·무전 콘솔·방향 안내 표지·체험 게이트다. 해당 역할의 목표 조작에 눈에 보이는 반응과 가상 팀 알림이 따른다.
6. 목표를 수행한 후 집결 공간에 도착하면 설명형 결과가 표시된다. 재시작하면 위치·오브젝트·진행 상태가 초기화된다.
7. 잘못된 대상·거리 밖·벽 뒤 대상은 완료되지 않고, 목표 수행 전 집결도 완료되지 않는다.
8. 안내·HUD·결과에 `KORAIL 검증 전 예시`를 표시한다. 사건·목표·설비는 게임용 가정이며 실제 대응 절차를 주장하지 않는다.
9. local 또는 school-pc에서 컴파일·콘솔·EditMode·PlayMode·Editor 플레이와 장면/직렬화 검사를 수행하고, Windows Standalone 플레이는 school-pc에서 확인한다. 실제 완주 증거가 있어야 playable로 판정한다.

상황 예시: 임시 역사에 가상 연기 표시가 발생한다. 플레이어는 선택 역할의 목표 장치를 조작해 예시 상태를 바꾸고 합성 맵의 집결 지점으로 이동한다. 다른 역할의 실제 행동은 공통 코어의 알림 상태로만 표현한다. 별도의 동일 인간형 대피자 6명은 플레이어의 인솔 행동에 반응해 이동하며, 업무를 대행하지 않는다. 실제 매뉴얼을 반영할 수 있는 순서·조건·대상 구조를 구현하되, 현재 세부 순서는 검증 전 초안이다.

## 구현 위치와 Unity 실행 경로

- `Packages/com.xrlab.chooguard.foundation/Demo/Runtime/`: 플레이어·상호작용·사건 흐름·HUD·결과.
- `Demo/Editor/`: 임시 맵·3D 소품·플레이어·UI 연결을 생성하는 Editor builder.
- `Demo/Tests/`: 생성 결과·진행 조건·거리/차폐·재시작 회귀 시험.
- 게임용 임시 명칭과 목표 문구는 `foundation/scenarios/foundation-demo.json`에서 관리한다.

local 또는 school-pc의 Unity 프로젝트에서 로컬 패키지를 연결한 뒤 **CHOOguard → Foundation → Build Playable Demo**를 실행하고 생성된 장면을 Play한다. 키보드·마우스로 한 역할을 완료한 후 나머지 역할도 재시작으로 확인한다. 구체 경로·기기 조건·빌드 방법은 [Demo 실행 안내](../Packages/com.xrlab.chooguard.foundation/Demo/README.md)를 따른다.

이 저장소에는 생성기·게임 코드가 있다. 설치·컴파일·장면 생성·테스트·실제 완주는 각각 실행 기록으로 확인하며, 소스나 설치 파일만으로 “플레이 검증 완료”로 표시하지 않는다. 로컬 Python 검사는 데이터만 검증한다.

## 현재 착수 경로

| 작업 | 기존/신규 추적 | 지금 작성할 산출물 | 후속 실제 검증 |
|---|---|---|---|
| 팀 시작 경로 | [FND-01 #55](https://github.com/xrlab-dau/CHOOGuard/issues/55) | 공통 검사 명령·패키지·온보딩 | 팀 checkout 재현 |
| Unity 기준선 | #24 | 모듈·UPM·test assembly 소스 | 정확 Editor 버전 고정·프로젝트·빈 씬 빌드 |
| 행동 계약 | #25 | TrainingAction·ActionResult·handler | C# 컴파일·EditMode |
| 합성 5직무 | #32 | scenario·Anchor·기대 결과 fixture | 코어 및 Unity 로딩 |
| Quest·피드백 | #26 | 상태 전이·설명형 결과 | fixture별 EditMode |
| 가상 팀 | #27 | ITeamStateProvider·결정론적 구현 | fixture 이벤트 일치 |
| 합성 맵 | [FND-02 #56](https://github.com/xrlab-dau/CHOOGuard/issues/56) | 임시 역사형 맵·다섯 조작 소품·Editor builder | Editor hierarchy·충돌·이동 |
| VR/Desktop 입력 | [FND-03 #57](https://github.com/xrlab-dau/CHOOGuard/issues/57) | 싱글플레이 이동·시점·거리/차폐 상호작용 | 실제 키보드·HMD |
| UI | #35 | 실제 장면의 역할 선택·HUD·집결/결과·재시작 | Editor UI·가독성 |
| 대표·네 핵심 행동 | #33·#34 | 가상 사건→장치 조작→집결의 실제 게임 흐름 | 대표 2경로·나머지 8경로 실제 완주 |
| 재구성 교체 경계 | [FND-04 #58](https://github.com/xrlab-dau/CHOOGuard/issues/58) | 합성 metadata recipe·provenance 검사 | #30 승인 자료 처리 |
| 회귀·증거·성능 | #36·#37 | 테스트·이벤트/성능 수집 코드 | Unity·Standalone·HMD 실행 |

#10·#13·#14·#16~23 및 R-01~07 등 KORAIL 회신 없이 할 수 있는 도구·질문·검토 준비도 Foundation 분류에 포함한다. 실제 회신 수신 #15, 승인 촬영 #28~29, 실제 처리 #30, 실제 촬영 기반 맵 #31과 최종 공개는 각 자료·실행 조건을 유지한다. #30/#31의 합성 선행 개발은 FND-04/FND-02로 분리해 중복 완료 처리하지 않는다.

## 의존성과 상태 적용

- #25·#32는 함께 계약을 고정하며 바로 소스·합성 데이터 작성을 시작한다. Unity 프로젝트 전체 완료를 기다리지 않는다.
- #26·#27은 계약·fixture 초안으로 개발하고, 통합 시 같은 버전의 fixture로 검증한다.
- #35·#33·#34 및 입력 어댑터는 mock handler·합성 맵으로 개발한다. 최종 통합 검증에는 실제 코어가 필요하다.
- Pi/MCP 제어면 조건은 해당 도구를 실제 사용하는 단계에 적용한다. 순수 C#·합성 데이터 작성 전체를 차단하는 전역 선행으로 확대하지 않는다. 운영 도구 검토 실패를 PASS로 바꾸지는 않는다.
- 기존 백로그/그래프의 선행 간선은 실제 통합·완료 검증 경로다. 이 문서가 정한 Foundation 소스 착수 경로에는 위 분할을 적용한다.
- Ready는 정의된 소스 작업을 시작할 수 있다는 뜻이다. 완료는 적용 시험·독립 검토·필요한 실제 실행 증거를 충족해야 한다.

## 작업 장소와 소유권

2026-09-06 사용자 지시로 로컬 Unity Hub·Apple Silicon용 안정 LTS Editor 설치를 허용한다. Hub 3.21.1 ARM64와 Editor 6000.3.23f1 LTS 설치를 2026-09-07 확인했다. 로컬은 문서·C#·합성 JSON과 작은 합성 맵·기본 도형·Desktop 게임 개발, 컴파일·EditMode·PlayMode·수동 플레이에 사용한다. 학교 PC의 Unity 개발 적합성은 사용자 기존 프로젝트 경험으로 확정했다. 무거운 모델링·렌더·베이크, Windows 실행본·HMD 검증은 학교 PC에서 수행한다. Pi/MCP 설치·연결 조건은 해당 도구를 사용하는 단계에 유지한다.

팀원은 미배정 이슈를 자율 선택하고 자기 배정한다. 기존 담당자를 임의 변경하지 않는다. Unity Editor 하나에는 쓰기 담당자 한 명만 둔다. `.unity`·`.prefab`·`.asset`·`.meta` YAML을 수동 작성하지 않고 Editor가 생성하도록 한다.

## 현재 구현과 팀원 시작 명령

```bash
python3 scripts/dev/check_foundation.py
```

Windows에서는 설치된 검토 대상 Python 실행기 `python` 또는 `py -3`를 사용한다. 위 명령은 합성 데이터·negative fixture 검사를 실행하며 Unity·모델 다운로드·3D 생성을 하지 않는다. stderr는 검사 로그, stdout은 검사 범위·입력 SHA-256·미실행 항목이 포함된 JSON이다. 영수증은 해당 파일들의 로컬 관측이며 보호된 독립 최종 증거를 대신하지 않는다.

```bash
python3 -m unittest discover -s scripts/ci/tests -v
```

C# 코어는 `Packages/com.xrlab.chooguard.foundation/`의 로컬 UPM 패키지다. 시나리오·Anchor 샘플은 `foundation/`, 검증기는 `scripts/foundation/`에 있다. 정확 파일과 API 사용 예시는 패키지 README 및 `foundation/README.md`를 따른다.

현재 저장소 루트에 Unity가 생성한 `ProjectSettings/ProjectVersion.txt`, 프로젝트 `Packages/manifest.json`과 lock 파일이 있다. Foundation은 포함된 embedded package이며 별도 사용자 경로를 연결할 필요가 없다. [현재 실행·모델·검증 안내](choo-guard-fps-foundation-progress.md)를 따른다.

#24 담당자는 정확 Editor 버전과 사용하는 URP/OpenXR/XRI 버전을 고정하고 로컬 패키지를 연결한다. 콘솔 오류 0, EditMode/PlayMode 및 빈 씬 빌드 결과와 실행 장소를 기록한다. 생성된 serialized assets와 `.meta`는 Editor 생성 결과를 검토한 뒤 추적한다. C# 컴파일이나 NUnit PASS는 실제 실행 결과가 있는 범위에만 기록한다.

## 첫 작업 선택

- 시나리오: #32의 합성 역할 문구·행동·기대 이벤트를 수정하고 검사 명령 실행.
- XR/Desktop: FND-03에서 `ITrainingActionHandler` mock에 행동 제출, Desktop 입력은 local 또는 school-pc에서 연결하고 HMD 입력은 학교에서 검증.
- UI: #35에서 역할·안내·팀 상태·피드백을 mock 기반으로 연결.
- 맵: FND-02에서 공유 Anchor ID를 유지하는 작은 합성 장면 builder 작성. 무거운 작업은 학교에서 실행.
- 코어/QA: #25~27·#36에서 상태 불변·중복 제출·버전 불일치·modality parity 시험 보강.

작업 브랜치는 `feature/foundation-starter`이며 `develop` 대상 PR로 전달한다. 실제 원격 커밋·PR·검사 상태는 GitHub #54와 #60의 최신 기록으로 확인한다. 과거 검증 영수증의 미푸시 표시는 당시 관측 상태로 보존한다.

## 근거

- [요구사항](choo-guard-requirements-baseline-v1.md): FR-01~10, MAP-01~07, RUN-01~05
- [아키텍처](choo-guard-platform-architecture-v4.md): §2~7·10
- [기존 백로그](choo-guard-execution-backlog-v1.md)
- [Foundation 통합 #54](https://github.com/xrlab-dau/CHOOGuard/issues/54)
- [Unity 로컬 패키지 설치](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-ui-local.html)
- [Unity 프로젝트 manifest testables](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-manifestPrj.html)
- [개발 실행 보드](https://github.com/orgs/xrlab-dau/projects/1)

## 2026-09-07 Blender·대피 인솔 확장

사용자는 Blender 기반 전 오브젝트 고도화, 맵 위 내비게이션, 더 복잡한 맵과 다중 오브젝트 상호작용, 동일 인간형 대피자를 명시 요청했다. 표정·감정은 제외하고 플레이어를 따라 이동하면 충분하다는 후속 지시를 적용한다. 소규모 로컬 Blender 메시 제작은 허용하며 무거운 렌더·베이크는 계속 학교 PC에 둔다.

추가 훈련은 `evacuation-drills.json`의 3개 게임 예시다. 시작 역할의 기존 코어 계약은 한 번만 제출하고, 나머지 장치 조작과 인솔은 별도 `DemoExercise` 상태로 관리한다. 대합실·양측 대기실·양측 우회 통로·집결 공간을 연결한다. 플레이 중 지시는 화면 패널 대신 맵의 경로와 대상 표식으로 제공한다. 자세한 제작·실행·검증은 `choo-guard-fps-foundation-progress.md`를 따른다.

## 2026-09-07 상시 현장·불시 사건 모드

최신 기본 경험은 훈련 선택 후 시작하는 흐름을 대체한다. 평상시 NPC와 설비가 운영되는 동일 공간에서, 사전 공개하지 않은 조건부 사건이 발생하고 관측·대응·복구·다음 사건으로 이어진다. 세부 설계와 실제 자료/현장 효용의 검증 범위는 [상시 현장 훈련](choo-guard-open-world-training.md)을 따른다. 기존 선택형 시나리오는 회귀 시험용으로 유지한다.
