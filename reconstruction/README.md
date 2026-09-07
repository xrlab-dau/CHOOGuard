# 로컬 사진 복원 → Blender → Unity 검토

DA3의 실제 깊이·카메라 출력을 PLY 점군과 **삼각형 GLB**로 전처리하고 Blender 편집본·FBX로 전달한다. 상류 DA3의 기본 `glb`는 점군이므로 본 경로는 optical-z 역투영과 깊이 경계 삼각분할을 별도로 수행한다. 원본 사진·가중치·복원 기하는 로컬 ignored 경로에만 남는다. CI에는 합성 배열만 사용한다.

기존 훈련 게임의 33개 Blender 자산과 합성 배치는 사진으로 복원한 실제 부산역이 아니다. 이 구현은 실제 복원 경로를 추가한 것이며 전체 역사/승강장/열차/지하철 모델링의 완성을 뜻하지 않는다. 전체 구역의 자료와 연결 상태는 [시설 범위](../foundation/world/facility-coverage.json), 공개 도면은 [조사 기록](../docs/art/public-facility-plans.md)에서 확인한다.

## 재현

Python 3.11과 `uv`, Blender 4.5 LTS를 사용한다. 상류 코드와 모델 리비전·파일 해시는 setup 도구와 [외부 코드 기록](THIRD_PARTY.md)에 고정했다. M1용 최소 추론 환경은 CUDA 확장·Gaussian 렌더러·웹 앱을 설치하지 않는다.

선택한 로컬 검토 결과는 DA3-BASE, 504px이다. 아래 SMALL의 첫 실행 절차를 그대로 따라 작은 검사를 시작하거나, setup과 run에 각각 `--model BASE`를 추가하고 `--process-res 504`, 별도의 출력 이름을 사용한다. 2026-09-07 의존성 감사에서 확인된 항목은 Torch2.13/vision0.28, Pillow12.3, setuptools83 고정으로 수정했고 고정 목록과 실제 추론 환경의 재감사에서 알려진 취약점 0건을 확인했다.

```sh
mkdir -p .local/reconstruction
git clone --filter=blob:none --no-checkout https://github.com/ByteDance-Seed/Depth-Anything-3.git .local/reconstruction/Depth-Anything-3
git -C .local/reconstruction/Depth-Anything-3 checkout --detach 3d835ec1a5802d64a8b8b15f817a1ab54809bfe4
python3 reconstruction/tools/setup_da3.py
python3 reconstruction/tools/fetch_public_pilot.py
.local/reconstruction/venv/bin/python reconstruction/tools/run_da3.py \
  --image private-data/public-concourse-pilot/busan-concourse-b.jpg \
  --image private-data/public-concourse-pilot/busan-concourse-a.jpg \
  --process-res 336 --device auto --cpu-fallback \
  --output reconstruction/output/busan-concourse-pilot

uv venv --python 3.11 .local/reconstruction/geometry-venv
uv pip install --python .local/reconstruction/geometry-venv/bin/python -e './reconstruction[dev]'
.local/reconstruction/geometry-venv/bin/choo-reconstruct \
  --prediction reconstruction/output/busan-concourse-pilot/prediction.npz \
  --audit reconstruction/recipes/busan-concourse-public-pilot.json \
  --output reconstruction/output/busan-concourse-pilot
blender --background --factory-startup --python-exit-code 1 \
  --python reconstruction/tools/refine_in_blender.py -- \
  --input-dir reconstruction/output/busan-concourse-pilot
```

macOS의 `blender`가 PATH에 없으면 `/Applications/Blender.app/Contents/MacOS/Blender`를 사용한다. 기존 결과가 있는 추론 디렉터리는 덮어쓰지 않으므로 새 실험은 다른 이름을 지정한다. 학교 PC는 Python/플랫폼 wheel 및 선택한 GPU 경로를 다시 검증하며, 로컬 MPS 실행 기록을 Windows나 HMD PASS로 옮기지 않는다.

## 각 산출물의 의미

| 파일 | 내용 |
|---|---|
| `prediction.npz` / `inference-receipt.json` | 원래 DA3 좌표의 깊이·confidence·world-to-camera pose·intrinsics·처리된 RGB, 입력/모델/출력 해시·실행 시간·메모리 |
| `review-points.ply` | 보수적 사람·유리·바닥 반사 마스크와 confidence 필터를 통과한 점 |
| `review-surfaces.glb` | 시점별 독립 삼각형 표면. 경계의 8% 초과 상대 깊이 점프 제거, 빈 영역 미봉합 |
| `concourse-refinement.blend` | 숨긴 raw collection과 편집 가능한 관측 표면. 0.5도 이내 planar dissolve 후 삼각분할 |
| `refined-review.glb` / `.fbx` | Blender 정리 결과와 Unity 전달용 모델 |
| `review-manifest*.json` | 축척 미정·중력 미보정·변환행렬·예상 Unity bounds·출력 해시·검토 범위 |

입력은 같은 원문의 B→A 두 사진이며 EXIF 시각 필드는 8초 차이다. 사람·반사·유리를 넓게 제외한 첫 복원은 **맞이방 상부 일부**다. 카운터 근접사진, 다른 매표구역, 제조사 사진, 생성된 Unity 화면을 같은 camera graph에 섞지 않는다.

DA3-SMALL/BASE는 상대 깊이 모델이다. 모델 단위를 미터로 바꾸거나 기존 합성 천장 높이에 억지로 맞추지 않는다. `reviewFromRawRowMajor`는 첫 카메라 기준의 glTF 방향 변환이다. Blender FBX 준비 단계의 회전 후 Unity는 `(review.x, review.y, -review.z)`를 사용하며 실제 임포트 bounds 검사를 요구한다. 중력·북쪽·바닥 원점·치수는 별도 제어점으로 등록해야 한다.

JPEG/sRGB 색은 PLY에 그대로 두고 glTF `COLOR_0`에는 선형 RGB로 변환한다. Blender→FBX도 명시적으로 LINEAR를 사용하며, Unity는 프로젝트가 Gamma일 때만 출력 변환한다. Blender가 남긴 위치별 RGBA 샘플을 실제 임포트 정점과 대조한다. 최초 밝아진 색상 출력은 이 수정 전의 검토 이력이다.

서로 다른 시점의 깊이 차이 지표는 모델 내부 일치도다. 실측 오차가 아니며 높은 값도 현장 정확도를 입증하지 않는다. 현재 표면을 합쳐 닫힌 맵으로 발표하지 않고 시점별로 검수한다. 실측 축척과 제어점이 없으므로 미터 단위 `scene-bundle.schema.json` 대신 별도 `reconstruction-review-1` manifest를 사용한다. 검토 모델에는 훈련 Collider를 생성하지 않는다.

## 검사

```sh
.local/reconstruction/geometry-venv/bin/python -m pytest reconstruction/tests -q
.local/reconstruction/geometry-venv/bin/ruff check reconstruction
```

광학 깊이/회전 pose 역투영, 잘못된 intrinsics, 좌표계와 winding, 경계/마스크 제거, NPZ 해시·입력 순서 결속, 삼각형 GLB/PLY 왕복, 빈 overlap을 PASS로 처리하지 않는 검사를 포함한다. 실제 Unity 임포트와 시각 확인, VR 입력·HMD 검증은 각각 별도 증거다.

## Unity 검토 장면

Unity 6000.3.23f1에서 `ChooGuard.Foundation.Demo.Editor.ReconstructionReviewBuilder.BuildBatch`를 실행하며 `--choo-reconstruction-dir reconstruction/output/<실험 이름>`을 지정한다. `BuildMacReviewBatch`는 같은 입력으로 `Builds/ReconstructionReview/ChooGuardReconstructionReview.app`을 만든다. 생성기는 FBX·GLB·manifest 해시, 실제 삼각형/bounds/색상 샘플을 검사하고 전용 ignored `Assets/CHOOguardGenerated/ReconstructionReview`에 저장한다. 기존 훈련 장면의 배치를 변경하지 않는다.

앱은 B/A 시점을 하나씩 표시한다. 원점·프레이밍·궤도/자유 이동으로 열린 표면을 확인하며, 검토 카메라는 실측 보정된 카메라가 아니다. 실행 인수 `--choo-reconstruction-captures <새 로컬 폴더>`는 각 시점의 세 검토 각도와 FBX 해시에 묶인 캡처 기록을 만든다. `ReconstructionReviewTests`는 합성 fixture와 로컬 소스가 있을 때의 실제 임포트를 검사한다. 공개 CI에는 사진과 복원 기하가 없으므로 실제 소스 임포트 항목은 건너뛰며, 그 상태를 로컬 임포트 PASS로 대체하지 않는다.

VR 기기는 미정이다. 현재 검토 앱은 Desktop용이고 OpenXR loader/XR rig/HMD 입력은 아직 구현·실행 검증되지 않았다. [엔진 선택과 XR 공백](../docs/reconstruction/engine-selection.md)에 고정 후보와 다음 통합 범위를 기록한다.
