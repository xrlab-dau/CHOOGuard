# 복원 엔진 선택과 VR 연결 상태

> **2026-09-08 최신 변경:** VARCO는 비용으로 폐기했고 CV/SOTA 파이프라인을 **현재 로컬 세션에서 재개**한다. 기존 엔진 결과를 재사용하며, 학교에는 [독립 고용량·고부하 작업](../context/school-pc-handoff-2026-09-08.md)을 배정한다. 완료한 실험·실패·미검증 상태는 그대로 보존한다.

2026-09-07 공식 코드·모델 카드·패키지 registry와 로컬 실행으로 확인했다. 최종 목표는 구역 전체를 연결한 철도 현장 시뮬레이션이다. 엔진의 출력, 플레이 가능한 공간, 실측 등록과 현장 수용은 구분한다.

| 엔진/도구 | 현재 판단 | 근거 |
|---|---|---|
| DA3-SMALL / BASE | 실제 설치·MPS 추론. 같은 입력 비교 후 BASE를 검토용으로 채택 | [공식 코드](https://github.com/ByteDance-Seed/Depth-Anything-3), [BASE 카드](https://huggingface.co/depth-anything/DA3-BASE). 정확한 리비전·가중치 SHA는 [실행 경로](../../reconstruction/THIRD_PARTY.md) |
| trimesh 4.8.3 + Blender 4.5.9 | 실제 PLY/삼각형 GLB 변환과 표면 정리·FBX 전달 | [trimesh 고정 소스](https://github.com/mikedh/trimesh/tree/4.8.3). 모델 정합 엔진 자체가 아니라 결과 처리와 편집에 사용 |
| PyCOLMAP 4.2.0 | 실제 설치·CPU 특징 추출/매칭/incremental mapping+BA 실행. 2026-09-08 지하연결통로 4프레임에서 초기 쌍조차 등록 실패 | [공식 릴리스](https://github.com/colmap/colmap/releases/tag/4.2.0), [Python 문서](https://github.com/colmap/colmap/blob/be5e29168d4aff238409d60424812df66aac919f/python/README.md). CPU 경로와 CUDA dense 처리를 구분 |
| MapAnything v1.1.4 (Apache 체크포인트) | 별도 hash-lock 환경·고정 local backbone 설치 및 두4프레임 구간 CPU 실제 추론 성공. 원시/adapter 출력은 상대 검토용이며 전달·시각 검수는 미완료 | [공식 코드](https://github.com/facebookresearch/map-anything/tree/3d10cf7a3016fc0f9bb13a071ee66c47b10be0d9), [Apache 체크포인트](https://huggingface.co/facebook/map-anything-apache/tree/00f9c245bbcb60522d1ed7f9e9d88462c6e3f38a). 기본 비상업 가중치와 Apache 변형의 조건은 다름 |
| VGGT | 중복 설치하지 않음 | [공식 코드/조건](https://github.com/facebookresearch/vggt/tree/a288dd0f14786c93483e45524328726ab7b1b4ce). 공식 예제의 CUDA 의존 가정과 별도 모델 조건을 M1 지원으로 오해하지 않음 |

사용 가능한 엔진이 있었으므로 실제 DA3 실행을 먼저 수행했다. 최신 논문이라는 이유만으로 대체하지 않는다. [DA3 논문](https://arxiv.org/abs/2511.10647)의 일반 평가 결과가 이 사진 두 장의 시설 정확도를 보장하지 않는다. 다음 비교는 동일 입력·정적 대응점·카메라 등록/재투영·깊이 경계·구역별 누락·메모리/시간을 기준으로 진행한다. 한 지표가 좋다는 이유로 관측하지 않은 공간을 복원했다고 선언하지 않는다.

SMALL/BASE 336px 비교에서는 상대 깊이 차이 5% 이내 표본이 각각 약53~57%,91~93%였다. BASE504에서는 관측 표본이 더 많아지고 중간값 깊이 차이는 약1.1~1.2%였다. 이는 마스크 후 **예측끼리의 일치도**이며 실제 도면/측량 오차가 아니다. 모델별 최초 실행·캐시 상태가 달라 시간으로 성능 순위를 정하지 않았다. 최종 실행과 소스 범위는 [증거](../evidence/foundation/2026-09-07-reconstruction.json)에 결속한다.

## 2026-09-08 최초 시퀀스 파일럿 — 부모 수용 차단

아래 수치는 역사적 실패 실험이며 [사후 감사](../evidence/foundation/2026-09-08-sequence-parent-audit.json)가 원 해석을 정정한다. 외부 이미지 첨부 위반의 회수·삭제는 보장할 수 없다.

`reconstruction/tools/run_sequence_pilot.py`로 부산역 KTX-지하철 지하연결통로 공개 영상(`39PANJWghAk`)의 컷이 섞인 4프레임(0/10/20/30초)에서 DA3-SMALL/BASE, 독립 PyCOLMAP 4.2.0 SfM(`run_sfm_sequence.py`), MapAnything(`run_mapanything.py`)을 같은 입력 해시로 비교했다. 증거: [`docs/evidence/foundation/2026-09-08-sequence-pilot.json`](../evidence/foundation/2026-09-08-sequence-pilot.json), 원 receipt는 `reconstruction/output/busan-underground-connector-pilot/sequence-pilot-comparison.json`.

- **DA3-SMALL(336px)/BASE(504px)**: 실제 MPS 추론 성공(각 0.97초/2.65초). 마스크된 시점별 삼각형 표면을 만들었다(`review-surfaces.glb` 4시점 128k~220k 삼각형). 그러나 시점 간 깊이 일치도는 낮다(예: t020↔t030 tolerance 내 비율 약1%) — 10초 간격의 큰 전진 baseline 때문이며 이는 예측 자기일치도이지 실측 정확도가 아니다.
- **독립 SfM (PyCOLMAP 4.2.0, DA3 보정값 미사용)**: 4프레임 전체는 물론, t000을 제외한 연속 3프레임 부분집합도 초기 이미지 쌍을 찾지 못해 `no_reconstruction_registered`로 종료했다. 감사된 임계값(`init_min_num_inliers=100` 등)을 완화해 억지로 성공시키지 않았다; 큰 baseline·반복 천장/바닥 패턴·소성 캡션 오버레이가 원인으로 추정된다. 이는 이 표본의 실제 음성 결과이며 SfM 자체의 결함을 일반화하지 않는다.
- **MapAnything (Apache 체크포인트)**: `setup_mapanything.py`가 코드 커밋`3d10cf7a3016fc0f9bb13a071ee66c47b10be0d9`을 고정 클론하고 LICENSE(Apache-2.0)를 재확인했으며, 모델 config.json(재감사 SHA256 `65701d09d99ed37a21d295f0d138978b3d584ab3bccdbcb4a2853da212b676c5`)에서 `encoder_config.uses_torch_hub=true`(dinov2_giant_24_layers)를 재확인했다. 최초 작성자는 자원 측정 없이 가중치/전용 venv를 학교 작업으로 해석했고 실제 추론 경로는 자리표시자였다. 이 해석은 사후 감사 SEQ-02/04에서 거부되었다. 설치는 승인된 로컬 대상이며 재개 구현은 실제 설치·추론 또는 측정된 차단 영수증을 남긴다.
- 희소 SfM 점군과 DA3 삼각형 표면은 `export_sparse_reconstruction`/`export_prediction`으로 항상 분리 전달되며 융합하지 않는다. 이 파일럿의 Blender/Unity 임포트·충돌 검사는 최초 세션에서 수행하지 않았다(로컬 Blender/Unity 실행 파일은 존재하므로 도구 부재라는 당시 설명은 잘못되었다); 기존 맞이방 파일럿의 Blender/Unity 경로는 재사용 가능하지만 이 시퀀스에 대해 아직 호출하지 않았다.

## 텍스트 전용 재개 단계

[새 실행 증거](../evidence/foundation/2026-09-08-sequence-resume.json)는 최대 두 구간의 동일4장·유한 증가 PTS·원 영상SHA·실제 크기·전체 구간 hard-cut scan을 결속한다. 0.3 scene-score 미검출은 사람의 연속성/개인정보 시각 검수 PASS가 아니다. [자료 회복 기록](../evidence/foundation/2026-09-08-sequence-source-recovery.json)은 보유 영상4개와 기존 목록에서 누락된 사진13개를 캐시 원문 URL/해시로 복구하며 새 영상 취득은0이다.

MapAnything 환경은 Python3.11/CPU·float32와 hash-lock을 사용한다. DINOv2 코드`7764ea0f912e53c92e82eb78a2a1631e92725fc8`을 local source로 강제하고 backbone 가중치 자동 취득 없이 전체 Apache 체크포인트를 strict load한다. 추론 socket 차단과 자원 감시를 적용했다. 등록/미등록·실제 관측별 재투영 median/p90·track길이·시차는 SfM 모델이 있을 때만 계산하며 없는 모델의 지표는 null이다. 엔진 성공표시만으로 순위에 넣지 않고 프로세스 exit0/공통입력SHA/실제산출물SHA를 검사한다. 실측 정확도 순위는 산출하지 않는다.

최종 로컬 실행은 `sequence-resume-connector-final`(24~25.5초)과 `sequence-resume-ry-final`(60~63초)이다. SMALL MPS는1.18/1.14초, BASE MPS는2.09/2.10초, MapAnything CPU는52.24/57.90초이며 MapAnything peak RSS는6,940,999,680/6,122,700,800B였다. 처리 grid는 SMALL336×196, BASE/MapAnything504×280이다. 독립 SfM은 두 구간 모두0/4 미등록으로 native camera model/BA 결과·track/재투영/시차 수치는 없다(null). 같은 입력이어도 처리해상도·장치·전처리가 다르므로 시간만으로 품질 순위를 정하지 않는다. 최초 MapAnything forward 뒤 `mask` adapter 오류와 이후 성공은 각각 별도 출력 폴더에 보존했다.

선행 writer는 당시 사용자 중간 지시로 Blender asset 작성/실행 및 Unity 전달 전에 중단했다. 현재는 로컬의 CV-01~06에서 새 마스크/다중 시점 기하 검증, 표면 export, Blender 편집본/FBX, Unity compilation/tests/hierarchy/native capture를 이어 한다. 최신 [연구 메모](../../_bmad-output/planning-artifacts/research/academic-lit-foundation-cv-pipeline-2026-09-08/research.md)의 일부 원리를 적용하되 전체 논문 재현으로 표시하지 않는다. 학교의 별도 자원 집약 작업은 로컬 진행과 독립이며, 현재 단계는 전체 spec 완료나 학교/HMD 검증이 아니다.

## VR 기기 미정 상태

현재 프로젝트는 Unity6000.3.23f1/Built-in, 기존 Desktop 입력이다. 엔진 VR/XR 모듈만 있으며 실제 OpenXR loader, XR Origin, 컨트롤러 입력은 아직 연결하지 않았다. 인터랙션 메서드에 VR이라고 넘기는 것만으로 HMD 입력이 구현되는 것은 아니다.

확인한 안정 버전 후보는 OpenXR1.18.0, XR Management4.7.0, XRI3.6.0, Input System1.20.0이다. 이번에 설치/컴파일된 패키지로 표시하지 않는다. [공식 registry](https://packages.unity.com/com.unity.xr.openxr), [OpenXR1.18 문서](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.18/manual/index.html), [XRI3.6 문서](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/index.html).

다음 구현은 같은 공간 anchor/행동 의미에 XR Origin과 head/hand ray adapter를 연결하고 시뮬레이터로 입력 경계를 검사하는 것이다. 선택된 기기의 Windows/Android runtime에서 이동, world-space 안내, 상호작용, 스테레오 렌더링과 프레임 시간을 검증한다. Mac의 복원/검토 앱 실행은 HMD 수용을 대신하지 않는다. 실제 축척이 없는 현재 맞이방 표면을 사람 높이의 VR 공간으로 임의 확대하지 않는다.
