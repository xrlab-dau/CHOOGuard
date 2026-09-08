# 1차 발견과 즉시 확인 사항

접근일 2026-09-08. Exa request `512274e92b0b80afff92530be1a4886a` 및 `imports/alphaxiv-discovery.txt`. 검색 서비스 요약을 원 논문의 검증된 결론으로 인용하지 않는다.

## 발견 후보 (추가 원문 확인 전)

- VGGT-SLAM2, LongStream, S-MUSt3R, VGGT-Motion: 짧은 feed-forward 기하를 긴 시퀀스로 확장하는 submap/streaming 계열. 연구별 변환군·입력 보정·평가 데이터가 다르다.
- LIST3R (2607.00375, 2026-07-01): 중첩 구간별 instance library와 반복 관측 anchor를 쓰는 최신 후보. alphaXiv 초록을 발견했으며 아래 원문 읽기 대상으로 선정한다.
- MonoVoc (2607.28300, 2026-07-30): 기하와 객체 단위 의미 정보의 분리를 제안. 초록만으로 성능/라이선스를 확정하지 않는다.
- MonoEM-GS (2604.10593): MapAnything 관측의 시점별 불일치를 EM+GS로 처리. 원문 일부를 확인했고 뒤의 실험·한계를 추가 읽는다.
- SimRecon (2603.02133, 공식 repo CVPR2026 Highlight 표기): perception-generation-simulation 및 instance-level assembly. 공개 코드의 생략 단계를 직접 확인해야 한다.
- ShapeR, OVOW, InstaScene: 관측+생성 보완 후보. 온전한 객체 mesh는 가려진 면의 실제 관측 증거와 다르다.
- LongDPM: 동적 영역을 정적 정합과 분리하는 중첩구간 방식. GPU/학습 모델/실제 코드 가용성을 미확인 상태로 둔다.

## 직접 읽은 공식 README의 중요한 사실

1. [DA3-Streaming](https://github.com/ByteDance-Seed/Depth-Anything-3/blob/main/da3_streaming/README.md)은 chunk streaming·중첩 정합·원시 depth/conf 저장 옵션을 이미 제공한다. NVIDIA A100에서 측정한 FPS는 준비/모델 로드/PLY 저장을 제외한다. TUM RGB-D 표의 최소 chunk30 설정도 peak VRAM18.7GB를 보고하므로 이를 M1 실행 속도/메모리 근거로 옮기지 않는다. 결과를 모으는 기능을 독자 SOTA 발명으로 부르지 않는다.
2. [MapAnything](https://github.com/facebookresearch/map-anything)은 통일된 출력과 `use_multiview_confidence` 옵션을 공개했다. `depth_z`와 ray depth, OpenCV camera-to-world pose를 구분한다. 로컬에 이미 고정한 Apache checkpoint와 기존 adapter를 활용할 수 있는지 조사한다. README의 최대2000뷰/140GB 표기는 우리 하드웨어의 수용량이 아니다.
3. [SAM3](https://github.com/facebookresearch/sam3)은 **2026-03-27 SAM3.1 Object Multiplex** 공개를 공지했다. 공유 메모리로 여러 객체를 추적하는 방향은 재사용 instance 분리와 관련 있다. Python3.12+, CUDA12.6+ 공식 경로와 HF checkpoint 접근 승인 안내가 있으므로, 현재 CPU/MPS 환경에서 실행했다고 쓰지 않는다.
4. [SimRecon](https://github.com/xiac20/SimRecon)은 CUDA2DGS·분할·AVO·SGS 코드를 제공하지만 생성기는 저자 실험에서 Rodin을 사용했고, 최종 자산 정합/시뮬레이터 배치를 사용자가 구현/수동 수행하도록 안내한다. 따라서 저장소 존재가 바로 Unity 연결 완료나 무료 단일 실행을 증명하지 않는다.

## 범위 제어

사용자는 최신 CV 연구 확인을 새로 명시했다. 따라서 연구 자체는 현재 요구에 해당한다. 다만 watchdog의 '완료한 엔진 비교를 반복하지 말라'는 지적은 적용한다: 새로운 계획 승인 게이트나 무제한 문헌 수집 없이, 찾은 기법을 기존 export/Blender/Unity 전달의 구체적 빈틈에 연결하고 첫 구현을 실행한다. 새 SOTA 모델 전체 설치·학교 GPU 실행·실측 수용을 이번 로컬 결과로 대체하지 않는다.
