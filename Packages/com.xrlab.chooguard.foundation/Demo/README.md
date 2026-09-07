# 상시 현장 Foundation

실제 공간과 매뉴얼에 최대한 충실한 flat 3D 비상대응 게임을 목표로 한다. 현재는 실제 자료 수신 전의 합성 기반이다. 기본 플레이는 훈련 종류를 고르는 방식에서 **평상시 현장 운영 → 불시 사건 → 관측/대응 → 복구/다음 사건**으로 변경됐다.

## 실행

1. 저장소 전체를 Unity Hub에서 **6000.3.23f1**로 연다. Git LFS의 실제 FBX가 필요하다.
2. 컴파일 후 `CHOOguard > Foundation > Build Playable Demo`를 실행하고 Play를 누른다.
3. 임시 직무를 정하고 **현장 입장**을 누른다. 사건 종류나 시작 시간은 고르지 않는다.
4. WASD 이동, 마우스 시점, E 설비 확인/가까운 이용객 인솔, Esc 일시정지/대응 기록.
5. 평상시 NPC가 이동하며, 조건을 만족하는 위치에서 통행 제한 또는 안내·통신 이상이 불시에 발생한다. 현장 신호나 상황 패널로 상태를 확인하고 사용 가능한 경로를 판단한다.
6. 경로는 실제 공간의 선택 단말에서 고른다. 현재 기본 배치에서 세 경로 선택 단말은 대합실 입구 쪽에 있다. 플레이 중 정답 행동 목록이나 다음 목표 텍스트박스는 없다.
7. 대상 이용객에게 직접 다가가 E로 인솔한다. 집결 확인 후 같은 공간에서 운영이 복구되고 다음 사건을 기다린다.

Mac 실행본: `Builds/FoundationMac/ChooGuardFoundation.app`. 메뉴: `Build Mac Player`.
Windows 빌드·VR/HMD 실기는 학교 PC에서 수행한다. Desktop 입력과 별도로 VR의 실제 입력/조작 검증이 필요하다.

## 데이터와 코드

- `foundation/world/station-twin-profile.json`: 단위/좌표, 통로·센서·설비 연결, 순회 지점, 사건 변형 시간, 근거 상태.
- `StationWorldSession`과 `StationWorldController`: 사건 상태와 관측 정보, 조건부 대응, 연속 근무, 기록/복기.
- `StationPortal`, `DemoWalkGraph`, `DemoEvacuee`: 공간 상태, 경로 버전, 평상시 순회/직접 인솔/집결/복구.
- `StationWorldBuilder`: 같은 자산과 데이터를 사용해 기본 장면을 반복 생성.
- 기존 `DemoFlow`/선택형 드릴은 기존 5직무 계약의 회귀 시험에 남긴다. 기본 게임 화면에는 드릴 선택이 없다.

완료한 사건과 중간 근무 기록은 이 기기의 `Application.persistentDataPath/StationSessions`에 저장한다. 새 근무 ID를 써서 같은 시드의 이전 기록을 덮어쓰지 않는다. 실명·직원번호·외부 전송은 없다.

## Flat 모델

Blender 4.5.9 LTS 원본 `foundation/art/station-kit.blend`, 생성기 `scripts/art/build_station_assets.py`, FBX 22종 `Assets/CHOOguardArt/Blender`를 제공한다. Unity 개발자는 FBX만 있으면 Blender 없이 실행할 수 있다. 재생성은 Unity를 닫고 Blender `--background --threads 2 --python scripts/art/build_station_assets.py`로 수행한다. 단색·무광·면 단위 법선을 유지하며 무거운 렌더·베이크는 수행하지 않는다.

## 검증

Test Runner 실행 전 `Build Playable Demo`로 현재 장면을 생성한다. `StationWorldSessionTests`는 시드/시간/관측/조건/기록을, `StationWorldSceneTests`는 공간 연결·동적 제한·통과를, `StationWorldPlayTests`는 실제 장면의 일상 순회와 여러 사건·직접 인솔·연속 복구를 검사한다. 자동 검사는 카메라 방향과 CharacterController 이동을 코드로 제어하며 사람의 수동 완주 증거가 아니다.

[상시 현장 설계와 실제 자료 검증 범위](../../../docs/choo-guard-open-world-training.md) · [실행·최신 증거](../../../docs/choo-guard-fps-foundation-progress.md)
