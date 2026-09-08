---
type: academic-lit
topic: Foundation CV 관측 복원과 재사용 자산 전달
decision: 기존 엔진 결과에 기하 검증과 객체별 관측 표현을 연결하고 Blender·Unity 전달을 실제 실행한다
source: Exa 및 alphaXiv/OpenAlex 발견, 원 논문·공식 코드·HF 메타데이터 확인
status: research-ready-local-implementation-pending
previous_cancelled_on: 2026-09-08
resumed_on: 2026-09-08
created: 2026-09-08
updated: 2026-09-08
---

# 최신 CV를 Foundation에 적용하는 결정 — 로컬 구현 재개

> 2026-09-08 VARCO 전환으로 한때 중단했으나, 사용자가 크레딧 비용 때문에 VARCO를 폐기하고 이 연구에 근거한 파이프라인을 현재 로컬 세션에서 재개하도록 지시했다. 아래 조사·제안은 기존 엔진 결과에서 CV-01~06을 구현하기 위한 근거다. 아직 실제 기법 적용·새 Blender/Unity 전달 완료를 뜻하지 않는다. 학교에는 로컬과 독립적인 고부하 작업을 배정한다.

## 적용 결론

**엔진을 전부 교체하지 않고, 기존 DA3/MapAnything 예측을 검증 가능한 관측 자산으로 만드는 전달 계층부터 구현한다.** 최신 연구의 공통된 방향은 긴 영상을 국소 단위로 처리하고, 여러 시점의 기하와 객체 정체성을 분리 보존해 정합·재사용하는 것이다. 단순히 점군을 합치거나 오브젝트마다 유료 생성기를 호출하는 것이 아니다.[1][3][4][5]

2026-09-08 기준 조사이며 전 세계 모든 모델의 종합 SOTA 순위를 확정한 보고서가 아니다. 논문에서 보고한 성능은 저자 실험이다. 이번 검색 범위에서 독립 재현을 확인하지 못했고 부산역 실측 정확도·실제 HMD 성능을 측정하지 않았다. **연구 확인, 기법의 부분 적용, 전체 논문 재현, 시설 수용을 구분한다.**

## 확인한 핵심 연구와 실제 적용 범위

| 연구/공개 상태 | 원문에서 확인한 접근 | 프로젝트의 적용 판단 |
|---|---|---|
| MapAnything, 3DV2026; Apache 코드/별도 Apache 가중치 | 여러 입력 조합과 엔진에 공통 기하 출력을 제공. optical-z, ray depth, camera-to-world pose를 구분. multi-view consistency 옵션 제공.[1][2] | 현재 고정한 Apache 모델 revision이 HF 최신 revision과 같음을 재확인. 재다운로드 대신 원시 예측·mask·카메라·전처리 provenance를 연결하는 engine-neutral export를 구현한다. |
| DA3-Streaming, 공개 구현 | 중첩 chunk와 정합으로 긴 영상 처리. 원시 depth/conf 저장 옵션 제공.[3] | 장거리 연결의 우선 재사용 후보. 이번 동일4장 전달을 완료하기 전에 전체 영상을 재추론하거나 새로운 SLAM을 작성하지 않는다. |
| LIST3R, 2026-07-01 preprint | local instance ID, mask tracks, visible frames, 기하/feature를 유지하고 전역 instance library로 통합. 정적·반복 관측 가능한 anchor에 의존한다.[4] | 객체 관측 기록과 원본/편집 자산의 연결에 설계 원리를 활용. 상류 실행 코드는 현재 확인되지 않아 LIST3R 구현 완료로 표시하지 않는다. 실제 자동 cross-chunk association은 후속 단계다. |
| MonoEM-GS, 2026-04 preprint | MapAnything 관측의 시점별 불일치를 EM 기반 맵으로 처리. normals·기하 일치와 appearance 목적을 구분.[5] | 바로 모든 GS/EM을 포팅하지 않는다. 첫 적용은 시점 간 관측 지원/가림/모순 진단과 필터 전후 기하 기록. 이는 논문의 전체 EM 알고리즘 재현이 아니다. |
| SimRecon, CVPR2026 Highlight(저자 repo 표기) | 객체의 mesh/PBR·pose와 객체 간 관계를 분리. 관측→생성→시뮬레이션 연결, 최적 시점 선택.[6] | 관측 자산·편집 자산·배치의 표현을 분리하는 설계 근거. 저자 실험 Rodin 호출과 최종 수동 배치 경로를 그대로 복제하지 않는다. |
| SAM3.1 Object Multiplex, 2026-03-27 공개 | 객체별 독립 처리 대신 고정 용량 bucket의 공동 추적으로 공유 계산·메모리 사용.[7] | 후속 CUDA 환경의 mask/track 생산자 후보. 지금은 명시적 instance-mask 입력 계약을 준비하되 자동 segmentation 실행 완료로 보고하지 않는다. |

### 실행 가능성의 직접 확인

- **MapAnything Apache:** HF API revision `00f9c245bbcb60522d1ed7f9e9d88462c6e3f38a`, ungated, `model.safetensors` 4,914,062,480 bytes, SHA256 `fa06c0fdccefc5048e072c85935d5789b1e36b307f3859033c17f9dcb9fd5201`. 이 공개 메타데이터가 기존 고정값과 일치한다. 기존 로컬 성공 여부는 별도의 프로젝트 실행 증거를 따른다.[8]
- **LIST3R:** Git tree `9dc19b4edc22f8bbca954cdd1d84aa9b5f1b386b`의 전체 목록(`truncated=false`)은 README·웹 페이지·시각화 자료다. Python/CUDA 추론 코드·패키지·가중치·라이선스 파일을 확인하지 못했다. 논문의 “code available” 표현만으로 실행 가능 판정을 내리지 않는다.[9]
- **SAM3.1:** HF repo `facebook/sam3.1`, revision `daa63191845a41281374e725f4c9e51c7a824460`, `gated=manual`, `sam3.1_multiplex.pt` 3,502,755,717 bytes, SHA256 `0567debeec80ba4ac6369540c6c248025283cb3ff2b92827509e57e2b3541cb6`. 코드 HEAD `660a5e9e1b8b4c02c0ad97229b88a09a6e4ff5b7`(2026-08-26). 실제 파일은 존재하지만 다운로드·계정 승인·실행은 수행하지 않았다.[10]
- SAM은 별도 **SAM License**이며 Apache/MIT로 부르면 안 된다. 공식 Python3.12+/PyTorch2.7+/CUDA12.6+ 경로가 있고 접근 승인 안내가 있다. 로컬 M1 지원을 입증한 자료는 이번 범위에서 확인하지 못했다.[11]

## 벤치마크를 읽을 때의 차이와 한계

- LIST3R은 TUM/ETH3D/BONN의 ATE/RTE/RRE, ETH3D/NRGBD의 geometry 지표를 사용하며 RTX4090에서 실험했다. 모든 지표에서 최고는 아니며, 논문 부록은 **반복 복도·객체가 드문 공간·큰 무특징 표면에서 anchor가 부족할 수 있음**을 명시한다. 역사의 반복 좌석·복도에 직접 관련된 한계다.[4]
- MonoEM-GS는 알려진 intrinsics를 가정하고 RTX3090 24GB에서 실험한다. 비교 MapAnything는25장 일괄 입력, 다른 방법은 stream으로 처리하며 normals 산출 방식도 다르다. loop closure가 없어 room-scale에 한정되며 앞쪽의 잘못된 관측을 합칠 수 있다는 한계를 밝힌다. 따라서 역 전체의 최상위 대체 SLAM으로 확정하지 않는다.[5]
- SimRecon의 ScanNet20개 장면 실험은 Rodin, Qwen2.5-VL, Blender/Isaac Sim을 사용한다. A6000에서 객체당 약30초는 **AVO 단계**이지 준비·생성·편집·Unity 전환의 총비용이 아니다. 공식 repo는 생성 모델과 최종 정합/시뮬레이터 배치를 사용자에게 남긴다.[6]
- SAM3.1의 약7배 속도는 H100·128개 객체 조건의 저자 결과다. 공개 표에서도 LVVIS/BURST 등 일부 PCS 수치는 이전 모델보다 낮다. 모든 정확도에서 일괄 향상했다고 하지 않는다.[7]
- DA3-Streaming의 TUM chunk30도 저자 표상 peak VRAM18.7GB이다. 속도 표는 A100에서 준비·모델 로드·PLY 저장을 제외했다. 이 수치가 로컬 Mac의 실행 가능성이나 종단간 지연을 증명하지 않는다.[3]

## 첫 구현: 예측 → 검증된 관측 패킷 → Blender → Unity

이는 프로젝트 적용 판단이며 위 논문들의 전체 시스템을 재현했다는 주장이 아니다.

1. **엔진과 출력 계층 분리:** 기존 DA3 및 MapAnything 원시/adapter 예측과 각자의 receipt를 읽는다. 모델 ID·입력 순서/SHA·grid와 crop/resize mapping·scale provenance는 유지하며 MapAnything 결과를 DA3라고 재표기하지 않는다.
2. **관측 지원 기반 표면 선택:** learned confidence에 더해 여러 시점의 재투영 depth 지원·가림·모순·미관측을 기록한다. raw 배열은 변경하지 않고 후처리 keep mask와 필터 전후 coverage를 따로 보존한다. 상대 단위에 미터 임계값을 무비판적으로 적용하지 않는다.
3. **객체 관측과 자산 분리:** 같은 실제 instance의 mask/관측 ID를 전달할 수 있는 경계를 둔다. 단순 mesh 연결 성분이나 frame ID를 의미적으로 검증된 객체라고 부르지 않는다. 지원된 instance mask가 없으면 per-view 관측 표면으로 정직하게 전달한다. 자동 mask tracking·객체 재식별은 아직 별도다.
4. **Blender:** 원본·선택된 관측 표면·편집본을 구분하고 원본 hash와 좌표 변환을 결속한다. 관측하지 않은 면을 메우거나 calibrated 모델로 재분류하지 않는다. 최신 결과에 실제 `.blend`·FBX를 만든다.
5. **Unity:** 기존 Editor builder를 재사용해 새 결과를 가져오고 실제 triangle/bounds/color/hierarchy와 no-physics 경계를 검사한다. 현재4시점과 임의 ID가 B/A 맞이방 문구로 바뀌지 않게 한다. compile/console·EditMode/PlayMode·로컬 native captures를 남긴다.
6. **재실행:** 입력·설정·코드·산출물 해시로 이미 완성된 단계만 재사용하고, 변경/손상/중간 실패는 성공 캐시로 읽지 않는다. 기존 추론/실패 폴더를 덮어쓰지 않는다.

### 상류 코드를 그대로 믿으면 안 되는 구체적 사례

고정 MapAnything의 `compute_multiview_depth_confidence`는 단일 뷰 또는 중첩 frustum이 없으면 ones를 반환한다. 이 값은 함수의 fallback이며 외부 정확도나 관측 지원의 증거가 아니다. 또한 전달된 `depth_masks`는 source validity에 사용되고 target sampling에서 같은 mask를 직접 검사하지 않는다.[2] 프로젝트의 후처리는 **지원 없음/unknown과 실제 일치**를 구분하고 양쪽 마스크·가림을 시험한다. 필요한 경우 작은 NumPy 구현으로 의미 차이를 명시한다. 이를 새 SOTA 알고리즘으로 홍보하지 않는다.

## 다음 확장과 남은 결정

첫 연결 이후: 공개 DA3-Streaming/Map-Long의 중첩·정합 경로와 SAM3.1 track 생산자를 고정 환경에서 평가 → 실제 instance library 구축 → 관측 객체의 canonical 자산·반복 배치·LOD. 새로운 상용 생성 계정·오브젝트당 API 지출은 필요하지 않다. 모델 계산 비용과 정리/검수 비용이 없어진다는 뜻은 아니다.

남은 항목은 SAM 접근 승인과 학교 CUDA 자원/의존성 검증, 장거리 loop 평가, 자동 instance 일치, 실제 미터 축척과 현장 제어점, 개인정보·정면/측면 시각 검수, VR 기기다. 이번 연구·로컬 전달이 이를 대신하지 않는다.

## 출처

모든 접근일은 2026-09-08. 연구 성능은 독립 재현 미확인(`unverified`); 코드/메타데이터 관찰은 각 원본과 snapshot에 한정한다.

| 번호 | 지지하는 내용 | 원 출처 | 발표/갱신 | 신뢰·범위 |
|---|---|---|---|---|
| [1] | 통일된 기하 인터페이스와 모델/라이선스 구분 | [Meta/CMU MapAnything](https://github.com/facebookresearch/map-anything) | 2025~2026 | 공식 설명; 독립 성능 검증 아님 |
| [2] | multi-view fallback·mask 적용의 실제 동작 | [Meta 고정 소스](https://github.com/facebookresearch/map-anything/blob/3d10cf7a3016fc0f9bb13a071ee66c47b10be0d9/mapanything/utils/multiview_confidence.py) | 고정 revision | 소스 직접 확인; 프로젝트 적용은 시험 필요 |
| [3] | 긴 영상 chunking와 GPU/속도 조건 | [ByteDance DA3-Streaming](https://github.com/ByteDance-Seed/Depth-Anything-3/blob/main/da3_streaming/README.md) | README 날짜 표기 2025-11; 다른 upstream 공지와 차이 있음 | 공식 구현; 수치 unverified |
| [4] | instance library, 실내 평가, 반복 복도 한계 | [Gao 등, LIST3R](https://www.alphaxiv.org/abs/2607.00375) | 2026-07-01 preprint | 방법/부록 직접 읽음; 수치 unverified |
| [5] | 기하 일치·EM/GS·calibration/loop 한계 | [Kruzhkov/Behnke, MonoEM-GS](https://www.alphaxiv.org/abs/2604.10593) | 2026-04 preprint | 방법/평가/결론 직접 읽음; 수치 unverified |
| [6] | object primitive와 공개 구현의 수동/외부 단계 | [SimRecon 원문](https://www.alphaxiv.org/abs/2603.02133), [코드](https://github.com/xiac20/SimRecon) | 2026-03; CVPR2026 표기 | 원문/README 직접 확인; 수치 unverified |
| [7] | SAM3.1 공동 추적과 혼합된 정확도 결과 | [Meta SAM3.1 release](https://github.com/facebookresearch/sam3/blob/main/RELEASE_SAM3p1.md) | 2026-03-27 | 저자 release; 수치 unverified |
| [8] | 실제 Apache checkpoint revision/파일 | [Hugging Face API](https://huggingface.co/api/models/facebook/map-anything-apache?blobs=true) | 2026-02-04 수정 | 조회 시점 메타데이터 |
| [9] | 공개 LIST3R tree에는 실행 코드 없음 | [GitHub tree API](https://api.github.com/repos/yixn965/LIST3R/git/trees/9dc19b4edc22f8bbca954cdd1d84aa9b5f1b386b?recursive=1) | 조회 시점 고정 tree | 누락 없는 목록; 다른 비공개 repo는 판단하지 않음 |
| [10] | SAM3.1 실제 가중치·manual gate | [Hugging Face API](https://huggingface.co/api/models/facebook/sam3.1?blobs=true) | 2026-03-27 수정 | 파일 metadata만, 다운로드/실행 안 함 |
| [11] | SAM 실행 조건과 별도 license | [SAM3 README](https://github.com/facebookresearch/sam3), [SAM License](https://github.com/facebookresearch/sam3/blob/main/LICENSE) | code 2026-08-26 / license 2025-11-19 | 조건 확인; 법률 자문 아님 |

## 탐색·보존·재확인

정확한 질문/기간은 `brief.md`다. 검색 결과·논문 원문·전체 HF/GitHub 응답은 로컬 ignored `imports/`에 보존하고 공개 Git에는 넣지 않는다. 공유 가능한 가용성 요약과 고정값은 `source-availability.json`에 남긴다. Exa 발견 기록은 `digests/discovery-r1.md`에 보존했다. 관련 없는 human-pose/의료/동물 궤적 및 관측 복원이 아닌 순수 세계/비디오 생성 결과는 제외했다. S-MUSt3R/LongStream/ShapeR/OVOW 등 발견 후보는 이번 첫 전달의 교체 엔진으로 선택하지 않아 전체 가중치 검증·실행을 하지 않았다.

ML 성능 주장6개월, 버전/호환/접근조건1개월 재확인 기준을 적용한다. 게시일이 오래된 구현 설명과 현재 HF revision 확인을 구분한다. 재개 시 `staleness.json`을 계산한다. 오래된 성능 표는 저자 결과로만 사용하고 최신 독립 재현으로 표시하지 않는다. 추가 독립 문헌 검증은 수행하지 않았으며, 학교의 별도 benchmark가 로컬 구현의 선행조건은 아니다.
