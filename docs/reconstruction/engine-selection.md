# 복원 엔진 선택과 VR 연결 상태

2026-09-07 공식 코드·모델 카드·패키지 registry와 로컬 실행으로 확인했다. 최종 목표는 구역 전체를 연결한 철도 현장 시뮬레이션이다. 엔진의 출력, 플레이 가능한 공간, 실측 등록과 현장 수용은 구분한다.

| 엔진/도구 | 현재 판단 | 근거 |
|---|---|---|
| DA3-SMALL / BASE | 실제 설치·MPS 추론. 같은 입력 비교 후 BASE를 검토용으로 채택 | [공식 코드](https://github.com/ByteDance-Seed/Depth-Anything-3), [BASE 카드](https://huggingface.co/depth-anything/DA3-BASE). 정확한 리비전·가중치 SHA는 [실행 경로](../../reconstruction/THIRD_PARTY.md) |
| trimesh 4.8.3 + Blender 4.5.9 | 실제 PLY/삼각형 GLB 변환과 표면 정리·FBX 전달 | [trimesh 고정 소스](https://github.com/mikedh/trimesh/tree/4.8.3). 모델 정합 엔진 자체가 아니라 결과 처리와 편집에 사용 |
| PyCOLMAP 4.2.0 | 다음 촬영 묶음의 특징 정합·희소 카메라 검증 후보, 현재 미설치 | [공식 릴리스](https://github.com/colmap/colmap/releases/tag/4.2.0), [Python 문서](https://github.com/colmap/colmap/blob/be5e29168d4aff238409d60424812df66aac919f/python/README.md). CPU 경로와 CUDA dense 처리를 구분 |
| MapAnything v1.1.3 | DA3가 실패하는 동일 입력의 비교 후보, 현재 미설치 | [공식 코드](https://github.com/facebookresearch/map-anything/tree/9d1db2dd728bd8a10e74b15d2eb646e1bf933791), [Apache 체크포인트](https://huggingface.co/facebook/map-anything-apache). 기본 비상업 가중치와 Apache 변형의 조건은 다름 |
| VGGT | 중복 설치하지 않음 | [공식 코드/조건](https://github.com/facebookresearch/vggt/tree/a288dd0f14786c93483e45524328726ab7b1b4ce). 공식 예제의 CUDA 의존 가정과 별도 모델 조건을 M1 지원으로 오해하지 않음 |

사용 가능한 엔진이 있었으므로 실제 DA3 실행을 먼저 수행했다. 최신 논문이라는 이유만으로 대체하지 않는다. [DA3 논문](https://arxiv.org/abs/2511.10647)의 일반 평가 결과가 이 사진 두 장의 시설 정확도를 보장하지 않는다. 다음 비교는 동일 입력·정적 대응점·카메라 등록/재투영·깊이 경계·구역별 누락·메모리/시간을 기준으로 진행한다. 한 지표가 좋다는 이유로 관측하지 않은 공간을 복원했다고 선언하지 않는다.

SMALL/BASE 336px 비교에서는 상대 깊이 차이 5% 이내 표본이 각각 약53~57%,91~93%였다. BASE504에서는 관측 표본이 더 많아지고 중간값 깊이 차이는 약1.1~1.2%였다. 이는 마스크 후 **예측끼리의 일치도**이며 실제 도면/측량 오차가 아니다. 모델별 최초 실행·캐시 상태가 달라 시간으로 성능 순위를 정하지 않았다. 최종 실행과 소스 범위는 [증거](../evidence/foundation/2026-09-07-reconstruction.json)에 결속한다.

## VR 기기 미정 상태

현재 프로젝트는 Unity6000.3.23f1/Built-in, 기존 Desktop 입력이다. 엔진 VR/XR 모듈만 있으며 실제 OpenXR loader, XR Origin, 컨트롤러 입력은 아직 연결하지 않았다. 인터랙션 메서드에 VR이라고 넘기는 것만으로 HMD 입력이 구현되는 것은 아니다.

확인한 안정 버전 후보는 OpenXR1.18.0, XR Management4.7.0, XRI3.6.0, Input System1.20.0이다. 이번에 설치/컴파일된 패키지로 표시하지 않는다. [공식 registry](https://packages.unity.com/com.unity.xr.openxr), [OpenXR1.18 문서](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.18/manual/index.html), [XRI3.6 문서](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/index.html).

다음 구현은 같은 공간 anchor/행동 의미에 XR Origin과 head/hand ray adapter를 연결하고 시뮬레이터로 입력 경계를 검사하는 것이다. 선택된 기기의 Windows/Android runtime에서 이동, world-space 안내, 상호작용, 스테레오 렌더링과 프레임 시간을 검증한다. Mac의 복원/검토 앱 실행은 HMD 수용을 대신하지 않는다. 실제 축척이 없는 현재 맞이방 표면을 사람 높이의 VR 공간으로 임의 확대하지 않는다.
