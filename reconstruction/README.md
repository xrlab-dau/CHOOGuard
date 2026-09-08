# 로컬 사진 복원 → Blender → Unity 검토

> **2026-09-08 현재 방향:** 사용자가 VARCO를 비용으로 폐기하고 **CV/SOTA 파이프라인을 현재 로컬 세션에서 재개**하도록 지시했다. 기존 두구간 엔진 결과를 재사용해 sequence spec의 CV-01~06을 진행한다. 학교 PC는 [독립 고용량 작업](../docs/context/school-pc-handoff-2026-09-08.md)을 맡으며 로컬 개발을 기다리게 하지 않는다. VARCO 결과는 [폐기한 시제품 이력](../docs/art/varco-production.md)으로만 보존한다.

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

## 영상 시퀀스 파일럿: 독립 SfM·MapAnything 비교

단일 사진 두 장을 넘어 실제 보유 영상에서 뽑은 연속 프레임을 비교하려면 `run_sequence_pilot.py`를 쓴다. 같은 연속 4프레임 입력으로 DA3-SMALL/BASE, 독립 PyCOLMAP 4.2.0 SfM, MapAnything(Apache 체크포인트)을 각자의 고정 환경에서 실행하고 결과를 하나의 receipt로 모은다.

```sh
.local/reconstruction/geometry-venv/bin/python reconstruction/tools/run_sequence_pilot.py \
  --image private-data/facility-sources/frames/<videoId>/<frame-a>.jpg \
  --image private-data/facility-sources/frames/<videoId>/<frame-b>.jpg \
  --image private-data/facility-sources/frames/<videoId>/<frame-c>.jpg \
  --image private-data/facility-sources/frames/<videoId>/<frame-d>.jpg \
  --output reconstruction/output/<새 실험 이름>
```

각 입력 프레임은 `scripts/references/prepare_sequence.py --video <보유영상> --output <새폴더> --times 24,24.5,25,25.5`로 만든 `samples.json`이 같은 디렉터리에 있어야 한다. 원 영상 SHA·순서·유한한 증가 PTS·실제 크기·중복·전체 구간의 hard-cut 검사를 추론 전에 검증한다. FFmpeg scene-score 0.3은 컷 휴리스틱이며 fade/시각/개인정보 검수를 대신하지 않는다. 일반 overview samples만으로 연속 입력 수용을 주장하지 않는다. 원본·이전 출력은 덮어쓰지 않는다. `reconstruction/tools/run_sfm_sequence.py`는 `probe_colmap.py`의 두 장짜리 pair-only 프로브와 달리 실제 incremental mapping+BA를 수행하며 DA3 보정값을 전혀 읽지 않는 독립군이다. 컷이 섞여 있거나 등록에 실패하면 `no_reconstruction_registered`/부분 등록으로 정직하게 남기며, 합쳐서 성공으로 바꾸지 않는다.

MapAnything(Apache 4.9GB)은 승인된 로컬 설치 대상이다. `python3 reconstruction/tools/setup_mapanything.py --output .local/reconstruction/mapanything/setup-sequence-resume.json`은 별도 `requirements-mapanything.lock` 환경·고정 DINOv2 소스·전체 체크포인트를 준비한다. 설치는 측정한 디스크와 30분 한도를 기록한다. 추론은 CPU/float32, 최대30분/RSS8GiB/여유디스크8GiB로 제한하며 검증한 local Torch Hub source와 `pretrained=False`만 사용한다. 전체 Apache state dict를 strict load하며 socket 네트워크를 차단한다. 원시 tensor NPZ와 optical-z/K/camera-to-world→world-to-camera adapter 출력을 분리 보존한다. 없는 가중치를 임의로 학교 전용 결정으로 바꾸지 않는다. 차단/미등록/부분·분리 모델과 프로세스 실패는 순위에서 제외한다. 성공 receipt도 공통 입력SHA·실제 산출물SHA·프로세스 exit0을 모두 대조한다.

SfM의 희소 점군은 `chooguard_reconstruction.export.export_sparse_reconstruction`으로, DA3의 조밀 삼각형 표면은 `export_prediction`으로 각각 별도 파일(`sparse-points.ply`/`sparse-manifest.json` vs `review-surfaces.glb`/`review-manifest.json`)에 전달되며 서로 융합하지 않는다. 기존 컷 혼합 실험은 [원 영수증](../docs/evidence/foundation/2026-09-08-sequence-pilot.json)과 [사후 감사](../docs/evidence/foundation/2026-09-08-sequence-parent-audit.json)를 함께 읽는다. 재개 실행은 [별도 단계 증거](../docs/evidence/foundation/2026-09-08-sequence-resume.json)에 결속한다. 새 시퀀스의 마스크 audit·CV 기법 적용·Blender/Unity 전달은 로컬의 미완료 CV-01~06 단계다. native 단일 작성자와 검증 가능한 프로필을 사용하고 모델/추론 배정은 현재 허용 범위에서 판단한다. 학교에는 독립 베이크/대용량 benchmark/Windows 검증을 배정한다. 과거 맞이방의 Unity PASS를 새 시퀀스에 재사용하지 않는다.

## Unity 검토 장면

Unity 6000.3.23f1에서 `ChooGuard.Foundation.Demo.Editor.ReconstructionReviewBuilder.BuildBatch`를 실행하며 `--choo-reconstruction-dir reconstruction/output/<실험 이름>`을 지정한다. `BuildMacReviewBatch`는 같은 입력으로 `Builds/ReconstructionReview/ChooGuardReconstructionReview.app`을 만든다. 생성기는 FBX·GLB·manifest 해시, 실제 삼각형/bounds/색상 샘플을 검사하고 전용 ignored `Assets/CHOOguardGenerated/ReconstructionReview`에 저장한다. 기존 훈련 장면의 배치를 변경하지 않는다.

앱은 B/A 시점을 하나씩 표시한다. 원점·프레이밍·궤도/자유 이동으로 열린 표면을 확인하며, 검토 카메라는 실측 보정된 카메라가 아니다. 실행 인수 `--choo-reconstruction-captures <새 로컬 폴더>`는 각 시점의 세 검토 각도와 FBX 해시에 묶인 캡처 기록을 만든다. `ReconstructionReviewTests`는 합성 fixture와 로컬 소스가 있을 때의 실제 임포트를 검사한다. 공개 CI에는 사진과 복원 기하가 없으므로 실제 소스 임포트 항목은 건너뛰며, 그 상태를 로컬 임포트 PASS로 대체하지 않는다.

VR 기기는 미정이다. 현재 검토 앱은 Desktop용이고 OpenXR loader/XR rig/HMD 입력은 아직 구현·실행 검증되지 않았다. [엔진 선택과 XR 공백](../docs/reconstruction/engine-selection.md)에 고정 후보와 다음 통합 범위를 기록한다.
