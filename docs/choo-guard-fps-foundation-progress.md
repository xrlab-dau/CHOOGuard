> 이 문서는 flat 적용 당시의 이력이다. 2026-09-07 후속 지시로 flat 고정은 폐기되었다. 현재 방향은 [공개 역사 레퍼런스](art/public-station-references.md)와 [context graph](context/README.md)를 따른다. 아래 시험은 기록 당시 소스에만 적용된다.

# Foundation — 상시 현장과 Flat Art

2026-09-07. 목표는 실제 역사 배치·설비 위치·동선과 실제 비상대응 매뉴얼에 최대한 충실한 3D 게임이다. 외형은 **단색·무광·면 단위 음영의 flat 스타일**로 통일한다. Desktop으로 기반을 개발하고, 이후 VR에서 같은 공간 Anchor와 행동 의미를 공유한다.

실제 역사 촬영/도면과 직원 매뉴얼은 요구사항 기준선 O-01~O-03에서 수신 대기다. 현재 맵·세부 순서는 교체 가능한 검증 전 초안이다. Flat Art 적용이나 자동 시험 통과가 실제 시설·매뉴얼 일치 검증을 대신하지 않는다.

## 현재 기본 플레이 변경

최신 기본 경험은 [상시 현장 운영·불시 사건·조건부 대응·복기](choo-guard-open-world-training.md)다. 입장 후 NPC가 평상시 이동하고, 조건을 만족하는 장소에서 예상하지 못한 사건이 발생한다. 대응 후 같은 공간이 복구되고 다음 사건으로 이어진다. 훈련 선택과 사건 시작 버튼은 기본 플레이에서 제거했다.

아래 3개 선택형 드릴과 15조합 기록은 이전 회귀 기준이다. 기존 기록을 새 상시 현장 모드의 완료 증거로 대신 사용하지 않는다. 최신 상시 현장 검증은 `2026-09-07-open-world.json`에 기록한다.

## 실행

저장소 루트를 Unity Hub에서 **6000.3.23f1**로 연다. embedded Foundation 패키지와 `Assets/CHOOguardArt/Blender`의 FBX가 필요하다. Git LFS 바이너리를 받아야 하며 Unity 플레이에 Blender 설치는 필요 없다.

- Editor: `CHOOguard > Foundation > Build Playable Demo` → Play.
- Mac: `Builds/FoundationMac/ChooGuardFoundation.app`.
- Windows 빌드와 VR/HMD 실기는 학교 PC에서 진행한다.

임시 직무를 정하고 현장 입장을 누른다. WASD·마우스·E로 조작하고 Esc로 일시정지·복기한다. 사건을 관측하고 직접 선택한 경로만 맵 위에서 안내한다. 사건의 영향을 받은 이용객에게 가까이 다가가 직접 인솔하며, 실제 도착 인원을 집결 단말에서 확인한다. 나머지 이용객은 평상시 이동을 유지한다.

## Blender 자산과 공간

Blender **4.5.9 LTS ARM64**를 공식 배포 SHA256·코드 서명 확인 후 설치했다. 무거운 렌더·베이크 없이 메시를 생성했다.

- 편집 원본: `foundation/art/station-kit.blend` — 전체 카탈로그와 자산별 장면.
- 제작 스크립트: `scripts/art/build_station_assets.py`.
- FBX 22종: `Assets/CHOOguardArt/Blender/`.
- 형상·해시 목록: `foundation/art/asset-manifest.json` — 전체 원형 자산 63,152 삼각형.
- 생성된 재사용 프리팹 17종: `Assets/CHOOguardGenerated/FoundationDemo/Prefabs/`.

장치: 상황 패널, 경보 장치, 무전 콘솔, 방향 표지, 게이트, 우회 안내 단말, 인원 확인 단말, 인솔 지점. 가구/소품: 벤치, 기둥, 키오스크, 이상 표시, 집결 표지, 수하물. 구조/시각: 벽·바닥 모듈, 문틀, 천장등, 바닥 화살표·집결 링, 장갑, 동일 인간형 대피자.

장치의 하우징·베젤·키패드·환기구·볼트·케이블과 가구 외곽 형상을 메시로 구성했다. 소재별 결합으로 작은 부품마다 렌더러를 만들지 않으며, 상태등·게이트 팔·관절은 분리했다. 모든 가시 MeshFilter는 Blender FBX를 참조한다. Collider는 별도 단순 형상이다.

맵은 대합실·양측 대기실·분기/우회 통로·집결 공간으로 연결된다. 문틀, 중간 파티션, 벤치, 수하물이 경로를 구분한다. 전체 목표와 NPC 출발점에서 집결까지 바닥 지지와 장애물 여유를 검사한다.

## 훈련과 NPC

`foundation/scenarios/evacuation-drills.json`의 대합실 이상 신호, 서측 우회, 분산 인원 합류의 세 가지 게임 초안을 제공한다. 한 번에 8~10개의 조작을 수행하며 선택 역할에 따라 첫 장치가 달라진다. 첫 역할의 기존 `TrainingSession` 계약만 한 번 제출하고 이후 조작·인솔은 `DemoExercise`에서 관리한다. 다른 네 역할에는 기존 알림 의미를 유지한다.

동일한 대피자 6명은 표정·감정 없이 소규모 배회 → 인솔자 추종 → 집결 대기만 수행한다. 이들은 직원 직무를 대행하지 않는다. 길은 작은 CPU 격자로 탐색하며 NavMesh 설치·베이크를 사용하지 않는다. 모서리 경로와 인원 간 우선순위 회피를 적용했다. 이는 엄격한 비중첩 군중 물리나 재난 군중 행동 모델이 아니다.

## 검증

- EditMode **88/88**: 5직무 계약, 순서, 재생성/참조, FBX 미터 크기/앞면, flat 법선/무광 재질, 통로/접근/바닥 및 출력 소유권.
- PlayMode **14/14**: 실제 생성 장면의 **3훈련 × 5역할 총 15개 조합** 이동·장치 조작·6명 추종·집결·재시작. 인솔 도중 일시정지/재개, 누락 인원/중복 등록, 순서/거리/가림/조기 집결 차단 포함.
- Python 데이터 검사 31개, 저장소 정책 위반 0.
- Mac universal 빌드 성공: Renderer **408**, 삼각형 **181,804**, 빌드 보고 **113,485,244 bytes**.
- 실제 Mac 화면에서 모델·바닥 내비게이션·대상 위 한글 표식·큰 퀘스트 패널 제거를 관측했다. 캡처 당시 1/8 진행 화면을 확인했지만 사용자 입력과 자동 클릭이 섞일 수 있어 독립적인 수동 완주로 계산하지 않는다.

자동 장면 시험은 카메라 방향과 CharacterController 이동을 코드로 제어하며, 사람의 15조합 WASD 완주를 뜻하지 않는다. 최종 실제 시설/매뉴얼 대조, 팀 리드 승인, Windows/VR/HMD, 성능 합격선 검증은 남아 있다. 소스 전달 경로는 `feature/foundation-starter`의 `develop` 대상 PR이다. 현재 커밋·PR 상태는 #54/#60의 최신 기록을 확인한다. 검증 JSON의 미푸시 값은 과거 관측 시점의 상태다.

[정제 검증 기록](evidence/foundation/2026-09-07-flat-evacuation.json) · [상위 작업 #54](https://github.com/xrlab-dau/CHOOGuard/issues/54) · [인솔 작업 #59](https://github.com/xrlab-dau/CHOOGuard/issues/59)
