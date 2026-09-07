# 복원 경로의 외부 코드와 모델

2026-09-07 확인. 원본 이미지·가중치·상류 저장소 전체·복원 기하는 이 패키지에 포함하지 않는다.

| 구성 요소 | 실제 고정 또는 후보 | 출처와 조건 |
|---|---|---|
| Depth Anything 3 코드 | `3d835ec1a5802d64a8b8b15f817a1ab54809bfe4` | [공식 Apache-2.0 LICENSE](https://github.com/ByteDance-Seed/Depth-Anything-3/blob/3d835ec1a5802d64a8b8b15f817a1ab54809bfe4/LICENSE). 태그 없는 현재 리비전이며 안정판 릴리스라고 부르지 않는다. |
| DA3-SMALL | `e08cab65ca0ec38e7826075418411ab90cab4da3` | [공식 모델 카드](https://huggingface.co/depth-anything/DA3-SMALL/tree/e08cab65ca0ec38e7826075418411ab90cab4da3), Apache-2.0. 상대 깊이/카메라 모델, Gaussian head 없음. |
| DA3-BASE | `f4a6c9b3c95e41c82048423d3493a81ec3fa810e` | [공식 모델 카드](https://huggingface.co/depth-anything/DA3-BASE/tree/f4a6c9b3c95e41c82048423d3493a81ec3fa810e), Apache-2.0. SMALL과 같은 상대 단위 제약. |
| NumPy / trimesh | `1.26.4` / `4.8.3` | [NumPy](https://github.com/numpy/numpy/blob/v1.26.4/LICENSE.txt), [trimesh](https://github.com/mikedh/trimesh/blob/4.8.3/LICENSE.md). BSD-3-Clause / MIT. 본 패키지의 메시 변환 경로에서 사용. |
| PyTorch / torchvision | `2.13.0` / `0.28.0` | [PyTorch](https://github.com/pytorch/pytorch/blob/v2.13.0/LICENSE), [torchvision](https://github.com/pytorch/vision/blob/v0.28.0/LICENSE). 정확한 종속성은 `requirements-da3.lock`. macOS wheel은 14 이상. |
| Blender | 로컬 `4.5.9 LTS` | [Blender 라이선스](https://www.blender.org/about/license/). 외부 실행기로 사용하며 배포 산출물의 권리는 입력 자료에 따라 따로 판단한다. |

`setup_da3.py`는 고정 상류 소스의 해시를 검증하고 `api.py`의 optional export/pose imports와 비 CUDA float32 실행 문맥만 수정한다. 모델 구조·가중치·추론 수식을 대체하지 않는다. 수정 표시와 상류 저작권 헤더는 로컬 checkout에 유지한다. 이 최소 경로에서 CUDA xformers/gsplat, 웹 앱 서버, 외부 이미지 API는 사용하지 않는다.

SMALL 가중치는 137,248,940 bytes, SHA-256 `364492e38a3a06d221ac75da7f6621ada3f2361cd24fde11ba79091e9f40efcf`; BASE는 541,518,028 bytes, SHA-256 `e01067dc1659613083d9145a9a2547ccdbe6ccbbf83c4fe7b3e8a4e2bdae78b5`다. 상위 모델이나 다른 제품에 이 라이선스 확인을 전용하지 않는다.
