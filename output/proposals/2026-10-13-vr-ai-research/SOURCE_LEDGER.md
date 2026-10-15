# CHOOGuard 제안서 출처 원장

정리일: 2026-10-13. CV·시뮬레이션 연구, 현재 소스 점검, 수업 확인 기록을 통합했다. 이번 통합 작업에서는 추가 웹 조사, 모델 호출, Unity 실행을 하지 않았다. 아래 논문이 존재한다는 사실과 CHOOGuard에서 해당 기술을 실행·검증했다는 사실은 별개다.

유형은 **학술논문 / 연구보고서·프리프린트 / 공식 제품·개발 문서 / 현재 로컬 코드 / 과거 내부 기록 / 사용자 제공·미확인**으로 구분한다. `직접 근거`는 각 행에 적힌 범위에만 적용된다. 설계 권고는 제안이며, 효과 수치나 실제 안전 유효성의 입증으로 확대하지 않는다.

## 1. 공간 복원·시점 보완·객체 생성

| ID·유형 | 확인된 서지·원문 | 직접 뒷받침하는 내용 | 한계·사용 조건 |
|---|---|---|---|
| CV01 · 학술논문 | Schönberger & Frahm, **Structure-from-Motion Revisited**, CVPR 2016, pp. 4104–4113. [CVF 원문](https://openaccess.thecvf.com/content_cvpr_2016/html/Schonberger_Structure-From-Motion_Revisited_CVPR_2016_paper.html), [COLMAP](https://colmap.github.io/) | 순서 없는 이미지 집합에서 카메라와 3D 구조를 추정하는 정합 기준선. | 웹 사진 전부의 자동 정합, 실제 단위 확보, 서로 다른 시기의 구조 통합을 보장하지 않는다. |
| CV02 · 학술논문 | Mildenhall et al., **NeRF: Representing Scenes as Neural Radiance Fields for View Synthesis**, ECCV 2020. [저자 프로젝트](https://www.matthewtancik.com/nerf), [저자 원문](https://arxiv.org/abs/2003.08934) | 카메라 pose를 이용한 연속 방사장과 새로운 시점 합성; mesh 추출 경로. | NeRF 결과가 바로 치수·topology·collider를 갖춘 게임용 객체라는 뜻은 아니다. |
| CV03 · 학술논문 | Wang et al., **NeuS: Learning Neural Implicit Surfaces by Volume Rendering for Multi-view Reconstruction**, NeurIPS 2021. [공식 원문](https://proceedings.nips.cc/paper/2021/hash/e41e164f7485ec4a28741a2d0ea41c74-Abstract.html) | SDF zero-level surface에 기반한 다중뷰 표면 복원. | 미관측 면, 유리·반사 금속, 게임용 UV·LOD·충돌 형상의 자동 완성을 입증하지 않는다. |
| CV04 · 학술논문 | Kerbl et al., **3D Gaussian Splatting for Real-Time Radiance Field Rendering**, ACM TOG 42(4), Article 139 / SIGGRAPH 2023, 2023-07-26. DOI **10.1145/3592433**. [출판사](https://doi.org/10.1145/3592433), [저자 원문](https://arxiv.org/abs/2308.04079) | 정합된 카메라·희소 점에서 초기화한 Gaussian 장면의 빠른 새로운 시점 렌더링. | 미촬영 뒷면의 사실성, 독립 관측의 증가, watertight mesh·collider 확보를 의미하지 않는다. |
| CV05 · 학술논문 | Huang et al., **2D Gaussian Splatting for Geometrically Accurate Radiance Fields**, SIGGRAPH 2024 Conference Papers. DOI **10.1145/3641519.3657428**. [저자 프로젝트](https://surfsplatting.github.io/), [공식 코드](https://github.com/hbb1/2d-gaussian-splatting) | 표면 지향 Gaussian과 depth·normal 정규화, textured mesh 복원 경로. | 모든 재질·실내 장면에서 정확하거나 후처리가 필요 없다는 주장을 뒷받침하지 않는다. |
| CV06 · 학술논문 | Wang et al., **VGGT: Visual Geometry Grounded Transformer**, CVPR 2025, pp. 5294–5306. [CVF 원문](https://openaccess.thecvf.com/content/CVPR2025/html/Wang_VGGT_Visual_Geometry_Grounded_Transformer_CVPR_2025_paper.html), [공식 코드](https://github.com/facebookresearch/vggt) | 영상에서 카메라·depth·point map·tracks를 함께 추론하는 geometry 초기화 후보. | 현장 웹 사진의 pose·metric scale 정확도나 프로젝트 채택·검증을 입증하지 않는다. |
| CV07 · 학술논문 | Kong, Yang & Wang, **Generative Sparse-View Gaussian Splatting**, CVPR 2025, pp. 26745–26755. [CVF 원문](https://openaccess.thecvf.com/content/CVPR2025/html/Kong_Generative_Sparse-View_Gaussian_Splatting_CVPR_2025_paper.html), [PDF](https://openaccess.thecvf.com/content/CVPR2025/papers/Kong_Generative_Sparse-View_Gaussian_Splatting_CVPR_2025_paper.pdf) | diffusion prior·pseudo-view·semantic correspondence로 희소 관측의 artifacts를 보완. | vanilla 3DGS만으로 새 실측 정보가 생기는 것은 아니다. 합성 뷰는 현장 증거와 구별해야 한다. |
| CV08 · 학술논문 | Chen et al., **GIFSplat: Generative Prior-Guided Iterative Feed-Forward 3D Gaussian Splatting from Sparse Views**, CVPR 2026, pp. 26031–26040. [CVF 원문](https://openaccess.thecvf.com/content/CVPR2026/html/Chen_GIFSplat_Generative_Prior-Guided_Iterative_Feed-Forward_3D_Gaussian_Splatting_from_Sparse_CVPR_2026_paper.html), [저자 원문](https://arxiv.org/html/2602.22571v1) | sparse unposed views에 feed-forward refinement와 frozen diffusion prior를 결합하는 최신 연구 방향. | 모든 도메인 공통 최고 성능, 현장 검증, 합성 뷰 누적의 무조건적 이득을 주장할 수 없다. |
| CV09 · 연구보고서 | Team Hunyuan3D, **Hunyuan3D 2.1: From Images to High-Fidelity 3D Assets with Production-Ready PBR Material**, arXiv:2506.15442, 2025-06-18; 공식 release 2025-06-13. [보고서](https://arxiv.org/abs/2506.15442), [공식 저장소](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1) | shape 생성과 PBR texture 합성으로 구성한 이미지→3D 자산 경로. | 현장 치수·뒷면 정확도, 전체 2026 SOTA, 사용자가 실제 쓴 버전의 증거가 아니다. |
| CV10 · 연구보고서 | Team Hunyuan3D, **Hunyuan3D-Omni: A Unified Framework for Controllable Generation of 3D Assets**, arXiv:2509.21245, 2025-09-25. [보고서](https://arxiv.org/abs/2509.21245), [공식 저장소](https://github.com/Tencent-Hunyuan/Hunyuan3D-Omni) | image 외 point cloud·voxel·bounding box·skeleton 제약을 사용하는 생성 방향. | 제약 입력이 독립 치수 검증을 대체하거나 프로젝트에서 채택됐다는 뜻은 아니다. |
| CV11 · 프리프린트 | Ye et al., **Hunyuan3D-Buffalo 1.0: A Unified Multimodal Model for Scalable 3D Generation, Understanding, and Editing**, arXiv:2608.02711, v1 2026-08-03 / v3 2026-08-13. [보고서](https://arxiv.org/abs/2608.02711), [공식 저장소](https://github.com/Tencent-Hunyuan/Hunyuan3D-Buffalo1.0) | 3D 이해·생성·편집·부품 생성 통합이라는 2026 Tencent 연구 방향. | 동료 심사 완료, weights·code 완전 공개, 기존 image-to-3D의 대체 준비, 현장 정확도를 입증하지 않는다. |
| CV12 · 공식 제품 문서 | Tencent **HY-3D 호출 가이드 / HY-3D-3.1 서비스**, 2026-09-29 수정 문서 및 서비스 공지. [API 문서](https://cloud.tencent.com.cn/document/product/1823/137181), [서비스 공지](https://cloud.tencent.cn/announce/detail/2403) | 공식 API의 3.0·3.1 존재와 제품 기능 식별. | 학술논문·동료 심사·정량 SOTA 근거가 아니다. 2.1 보고서의 결과를 3.1 성능으로 옮기지 않는다. |

정리 원칙: **관측 기반 NVS / 생성 선험의 pseudo-view / 실제 held-out 검증 사진**을 분리한다. NeRF·표면 복원과 Hunyuan3D 객체 생성은 자료 조건에 따른 분기다. `3DGS → NeRF → Hunyuan3D`를 필수 직렬 단계로 표현하지 않는다. 실제 사용한 Tencent 모델 버전은 미확인이다.

## 2. NPC 이동·시나리오 생성·재현성

| ID·유형 | 확인된 서지·원문 | 직접 뒷받침하는 내용 | 한계·사용 조건 |
|---|---|---|---|
| SIM-01 · 학술논문 | van den Berg, Guy, Lin & Manocha, **Reciprocal n-body collision avoidance**, *Robotics Research: The 14th International Symposium ISRR*, STAR 70, pp. 3–19, May 2011. [저자 서지](https://gamma-web.iacs.umd.edu/ORCA/), [원문](https://gamma.cs.unc.edu/ORCA/publications/ORCA.pdf) | ORCA의 상호 책임 분담에 따른 국소 충돌 회피 기준선. | 수학적 조건이 있으며 Unity·비협조 플레이어·밀집 접촉 군중에 대한 포괄적 무충돌 보장이나 최신 SOTA가 아니다. |
| SIM-02 · 학술논문 | Helbing & Molnár, **Social force model for pedestrian dynamics**, *Physical Review E* 51, 4282–4286, 1995-05-01. DOI **10.1103/PhysRevE.51.4282**. [출판사](https://journals.aps.org/pre/abstract/10.1103/PhysRevE.51.4282) | 목표 이동·보행자 간격·벽 상호작용의 해석 가능한 모델. | 파라미터 보정이 필요하다. 실제 역사의 비상 대피·연기·접근성·심리를 검증한 결과가 아니다. |
| SIM-03 · 학술논문 | Biswas et al., **SocNavBench: A Grounded Simulation Testing Framework for Evaluating Social Navigation**, *ACM Transactions on Human-Robot Interaction* 11(3), pp. 1–24, 2022. DOI **10.1145/3476413**. [저자 원문](https://harplab.github.io/assets/pubs/biswas_thri21.pdf), [출판사 기탁 서지](https://api.crossref.org/works/10.1145/3476413) | 실제 보행 궤적 기반의 반복 가능한 시나리오와 여러 이동 지표. | 기록된 보행자가 로봇에 반응하지 않는다. 상호작용 군중의 검증으로 확대하지 않는다. 공식 기탁 서지: online 2022-07-13, print 2022-09-30. 2021 저자 preprint의 placeholder 서지를 쓰지 않는다. |
| SIM-04 · 학술논문 | Cobbe, Hesse, Hilton & Schulman, **Leveraging Procedural Generation to Benchmark Reinforcement Learning**, ICML 2020, PMLR 119:2048–2056. [공식 원문](https://proceedings.mlr.press/v119/cobbe20a.html) | Procgen의 절차적 다양성과 학습/미관측 평가 환경 분리 원칙. | 게임 RL 일반화의 실험 근거다. 보행 대피 물리·교육 효과·현장 대응 절차의 근거가 아니다. |
| SIM-05 · 학술논문 | **Demonstrating Arena 5.0: A Photorealistic ROS2 Simulation Framework for Developing and Benchmarking Social Navigation**, RSS XXI, June 2025. DOI **10.15607/RSS.2025.XXI.092**. [공식 원문·서지](https://roboticsproceedings.org/rss21/p092.html), [PDF](https://www.roboticsproceedings.org/rss21/p092.pdf) | 모듈형 환경·시나리오 생성, social navigation 평가, emergency/rescue 예시. | 로봇 내비게이션 연구다. 사람의 대피 정확도나 CHOOGuard 구현의 검증이 아니다. 공식 HTML/PDF의 저자 배열 차이가 있어 짧은 인용은 제목·venue·DOI를 사용한다. |
| SIM-06 · 학술논문 | Munje et al., **SocialNav-SUB: Benchmarking VLMs for Scene Understanding in Social Robot Navigation**, CoRL 2025, PMLR 305:1120–1143. [공식 원문](https://proceedings.mlr.press/v305/munje25a.html) | 공간·시간·사회적 이해를 평가하며, 해당 실험의 최상위 VLM도 규칙 기준선·인간 합의보다 낮았다. | 모든 VLM·JEV·대화 모델의 실패를 뜻하지 않는다. 명시적 상태/실행 계층을 두자는 결론은 설계상 추론이다. |
| SIM-07 · 학술논문 | Kunchi, Lakshmidevi & Reddy, **PeopleFlow: a reproducible geometry-aware multi-agent framework for evacuation safety evaluation across diversified building environments**, *Frontiers in Built Environment* 12, 2026-08-26. DOI **10.3389/fbuil.2026.1850477**. [출판사 원문](https://www.frontiersin.org/journals/built-environment/articles/10.3389/fbuil.2026.1850477/full) | 도면·시나리오·geometry hash·seed·timestep·결과 기록을 연결하는 비교 평가 파이프라인. | 현재 2D이며 계단실·연기·집단 행동 등 한계가 명시돼 있다. 현장 검증은 공개 훈련 범위 일관성 점검이다. 다층 확장은 개념적이며 수치를 우리 역사에 옮기지 않는다. |
| SIM-08 · 공식 개발 문서 | Unity Technologies, **ECS Galaxy Sample — Determinism Overview**, living documentation, 2026-10-13 확인. [공식 저장소](https://github.com/Unity-Technologies/ECSGalaxySample/blob/main/_Documentation/determinism.md) | 고정 시간 간격, 난수 호출 순서·빈도, 시스템·entity 순서, 수치/병렬 실행 제어의 필요성. | 이 ECS sample의 결정론 설정이 현재 GameObject·PhysX·NavMesh 프로젝트에 이미 적용됐다는 증거가 아니다. 학술논문이 아니다. |

정리 원칙: 동일 시드는 동일 결과의 충분조건이 아니다. 초기 상태·build·에셋/이동 그래프·모델 응답·사용자 입력·실행 순서도 보존하고 검증해야 한다. 시나리오 재생, 동일 입력 재시뮬레이션, 기록 궤적 재생은 서로 다른 계약이다.

## 3. 공식 제작 도구·제품 문서

| ID·유형 | 문서·URL | 직접 근거와 한계 |
|---|---|---|
| DOC01 · 공식 개발 문서 | Unity **MeshCollider**, Unity 6000.0. [문서](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/meshcollider) | 충돌용 mesh는 별도로 지정한다. 외형 렌더 품질이 통과성·충돌 품질을 보증한다는 근거가 아니다. |
| DOC02 · 공식 개발 문서 | COLMAP **FAQ — Geo-registration**. [문서](https://colmap.github.io/faq.html#geo-registration) | 정합 결과를 외부 좌표 기준과 연결하는 절차. 이 프로젝트의 도면/현장 좌표 정합 오차를 측정한 자료가 아니다. |
| DOC03 · 공식 개발 문서 | Blender Manual 5.1 **glTF 2.0**. [문서](https://docs.blender.org/manual/en/5.1/addons/import_export/scene_gltf2.html) | 자산 교환·export의 공식 참고. 실제 후처리 완료나 게임 적합성 평가를 입증하지 않는다. |
| DOC04 · 공식 개발 문서 | Unity 6000.7 **AI Navigation**. [문서](https://docs.unity.com/en-us/engine/6000.7/manual/packages-list/packages-all/pack-safe/com-unity-ai-navigation) | 이동 가능 영역·경로 작성 도구의 공식 식별. 현재 프로젝트 설치 버전과 동작은 로컬 manifest/코드/실행으로 별도 확인한다. |
| XR01 · 공식 개발 문서 | Unity XR Interaction Toolkit 3.3 **XR Interaction Simulator overview**. [문서](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/xr-interaction-simulator-overview.html) | 키보드·마우스의 headset/controller/hand 입력 시뮬레이션과 구성된 XR Origin 연결의 참고. 편집기·build 포함 조건을 구분해야 한다. 프로젝트 실행 증거나 HMD tracking·stereo·comfort 검증이 아니다. |
| XR02 · 공식 개발 문서 | Unity XR Interaction Toolkit 3.3 **Components**. [문서](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/components.html) | interactor·interactable·locomotion 등 XR 상호작용 구성요소의 공식 참고. 현재 XRI 설치·구현 완료 또는 특정 package 조합의 검증을 뜻하지 않는다. |
| VENDOR01 · 공식 제품 홈페이지 | **TypeSafe AI**. [공식 홈페이지](https://typesafe.ai/) | 공급사 명칭·정체성 확인에만 사용한다. JEV endpoint·alias·게임 통합은 LOC01–03의 로컬 근거로 설명하며, 홈페이지를 행동 정합성·안전·정량 성능 근거로 쓰지 않는다. |

## 4. 현재 구현과 과거 기록

로컬 코드는 “현재 소스에 구현돼 있음”을, 과거 실행 기록은 “그 날짜·설정에서 기록된 실행이 있었음”을 뒷받침한다. 이번 작업에서 새 실행이나 모델 API 점검은 하지 않았다.

| ID·유형 | 로컬 근거 | 직접 뒷받침하는 내용 | 주장 한계 |
|---|---|---|---|
| LOC01 · 현재 코드 | [JevClient.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/JevClient.cs:99) | TypeSafe System One endpoint와 `jev-latest` alias, 후보 Choice·ordinal Score, 응답/요청 logging. | JEV는 이 코드의 구조화 판단 계층이며 JEPA/CV 복원 모델이 아니다. `latest`를 고정된 `1.13.0`으로 부르거나 현재 접속 성공·정합성을 주장하지 않는다. |
| LOC02 · 현재 코드 | [CrowdMind.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/CrowdMind.cs:8), [Passenger.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/Passenger.cs:114), [StationWorld.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/StationWorld.cs:49) | 관측 변화에 반응한 후보 판단, 오래된 응답 배제, Unity NavMesh 경로/행동 실행의 분리. | 승객 특성·행동 가중치는 작성된 heuristic을 포함한다. 실제 군중 성격 학습·대피 현실성·무결한 실행의 검증이 아니다. |
| LOC03 · 현재 코드 | [EmergencySession.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/EmergencySession.cs:165), [IncidentDirector.Compose.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/IncidentDirector.Compose.cs:98), [DirectorRates.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/DirectorRates.cs:64), [ShiftLog.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/ShiftLog.cs:91) | seed 선택, 분리 RNG, 제한된 인과 후보군, competing-risk 사건 샘플링, seed·판단·사건 기록. | 현재 rate는 게임의 훈련 노출/진행 속도를 위한 값이며 실제 사고확률이 아니다. 완전한 response-tape replay executor는 확인하지 못했다. |
| LOC04 · 과거 내부 실험 | [JEV_NPC_FEASIBILITY.md](/Users/um-yunsang/CHOOGuard/docs/CHOOGuard_FPS_Prior_Research_20260925/JEV_NPC_FEASIBILITY.md:44) | 2026-09-25 실험에서 `jev-1.13.0`을 명시한 직접 probe와 당시 검토 범위. | 당시 workload-only 상태 설명을 현재 구현에 적용하면 최신 소스와 모순된다. 내부 보고서이며 peer-reviewed 검증이 아니다. |
| LOC05 · 과거 내부 실행 기록 | [.planning/2026-09-29-jev-all-emergencies/plan.md](/Users/um-yunsang/CHOOGuard/.planning/2026-09-29-jev-all-emergencies/plan.md:120), [기록 예시](/Users/um-yunsang/CHOOGuard/.planning/2026-09-29-jev-all-emergencies/director-calibration/records/incident-1x-83f961.json) | 2026-09-30 전후 Unity·JEV 연결과 NPC/사건 판단 실행의 내부 기록. | 누적 spawned 인원을 동시 인원으로 쓰지 않는다. 현재 장면·API·성능·교육 효과의 새 측정이 아니다. 일부 재현 스크립트 경로는 현재 checkout에 없다. |
| LOC06 · 프로젝트 기획 문서 | [MVP_DIRECTION.md](/Users/um-yunsang/CHOOGuard/docs/CHOOGuard_Story_Plan_v5/MVP_DIRECTION.md:5) | 역무 직원 역할의 1인칭 대응 게임, 신고·안내·통제·NPC 협업이라는 기존 방향. | 부산역 현재 내부 매뉴얼이나 모든 사건의 전문기관 승인 절차를 입증하지 않는다. |
| LOC07 · 과거 별도 제안서 | [VR Term Project 제안서](/Users/um-yunsang/CHOOGuard/output/proposals/2026-10-05-vr-term/CHOOGuard_VR_Term_Project_제안서.md) | 2026-10-05에 작성된 한 구역·한 시나리오 XR 수업 제안. | 이번 넓은 파이프라인 제안의 범위를 자동 대체하지 않는다. 수업 공식 요구사항으로 인용하지 않는다. |
| LOC08 · 내부 개발 화면 | [에스컬레이터·계단](/Users/um-yunsang/CHOOGuard/workers/twin/output/claude-main1f-completion-20261012/_writer/022-OPT1-CLEAT/captures-after/B5-E-1F-FOOT.png), [유리 전면](/Users/um-yunsang/CHOOGuard/workers/twin/output/claude-main1f-completion-20261012/_writer/023-GLZ-B7/captures-after/B7-S1-P.png), [회전문](/Users/um-yunsang/CHOOGuard/workers/twin/output/claude-main1f-completion-20261012/_writer/002-WP5-B52B53/captures-after/01-EAST-current-official-and-paired-full-front.png) | `부산역 1F 재구성 개발 화면 · 2026-10-12`로 표시할 수 있는 배치·외형 자료. | 3DGS·NeRF·Hunyuan 실행, 완성된 전체 시설 twin, 경로·대피·NPC 성능의 증거가 아니다. 계단 상부 등 미해결 보정이 존재한다. |
| LOC09 · 현재 설정 | [ProjectVersion.txt](/Users/um-yunsang/CHOOGuard/ProjectSettings/ProjectVersion.txt:1), [manifest.json](/Users/um-yunsang/CHOOGuard/Packages/manifest.json:36), [packages-lock.json](/Users/um-yunsang/CHOOGuard/Packages/packages-lock.json:39) | Unity `6000.3.23f1`, URP `17.3.0`, Input System `1.20.0`. built-in VR/XR engine modules은 있으나 XRI·OpenXR·XR Plugin Management는 두 manifest에 없다. | 엔진 모듈 존재를 XR interaction/provider 설치나 실행 성공으로 쓰지 않는다. XRI 3.3 문서는 제안할 도구의 참고이며 현재 설치 버전이 아니다. |
| LOC10 · 현재 코드 | [FirstPersonResponder.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/FirstPersonResponder.cs:60), [IFpsInteraction.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/IFpsInteraction.cs:46), [StaffTools.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/StaffTools.cs:338) | 데스크톱 카메라·키보드/마우스 입력, camera ray, 장비 상태·hold 동작이라는 재사용 가능한 기반. | `StepInput` 검증 adapter는 6DoF 머리·손 포즈 입력이나 XR simulator가 아니다. tracked camera 소유권, controller ray target, grab/hold mapping은 새 작업이다. |
| LOC11 · 현재 코드 | [FpsUiFactory.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/FpsUiFactory.cs:22), [GameHud.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Hud/GameHud.cs:44), [ShiftLog.cs](/Users/um-yunsang/CHOOGuard/Assets/ChooGuard/App/Fps/Emergency/ShiftLog.cs:348) | 현재 screen-space overlay HUD와 종료 화면·JSON 저장. 기존 기록/정보 상태는 XR UI에 재사용할 수 있다. | 현재 종료 화면에는 점수가 없다. 공간 UI·tracked pointer·한국어 가독성·자동 채점·학습 효과를 구현 완료로 쓰지 않는다. |

XR 범위 판정: **공통 공간·NPC·사건·장비·기록 기반은 재사용하고, 머리·양손 입력·이동·공간 UI와 XR 검증을 수업의 제안 기여로 둔다.** XRI 설치, simulator 편집기/build 실행, 실제 HMD stereo·성능·comfort 검증은 구별한다. 현재 코드 점검은 실행 검증이 아니다.

## 5. 수업·기관 문제정의의 미확인 출처

| ID·유형 | 지정된 출처 | 현재 확인 상태·제안서 표기 |
|---|---|---|
| COURSE01 · 미확인 수업 페이지 | [VR 과제 URL](https://canvas.donga.ac.kr/courses/140132/assignments/1329928) | 로그인 페이지 본문을 읽지 못했다. Aside CLI와 daemon 사이의 로컬 인증 브리지 실패(키체인/서명 helper 문제)로 페이지 접근 전 중단됐다. 요구 항목·기한·형식·rubric은 **확인 대기**다. |
| COURSE02 · 미확인 수업 페이지 | [AI 시스템 설계·개발 II 과정 URL](https://canvas.donga.ac.kr/courses/138443) | 같은 로컬 인증 장애로 수업 본문·첨부를 확인하지 못했다. 과정 이름/관계는 사용자 설명이며 공식 과제 요건을 대신하지 않는다. |
| CONTEXT01 · 사용자 제공 | KORAIL 문제정의에서 같은 기반 프로젝트가 출발했다는 사용자 설명. 원문 제목·작성자·날짜·문서/URL 미확보. | **사용자 제공 맥락·기관 원문 확인 대기**로 표기한다. KORAIL의 공식 요청문, 협약·지원·실증·성능 승인으로 확대하지 않는다. |

수업 준수 확인과 KORAIL 문제정의 검증은 별도 미완료 항목이다. 제안된 목차·평가기준을 교수자 요구사항처럼 쓰지 않는다. 지원되는 로컬 인증 연결 복구 후 원래 페이지와 첨부의 제목·기한·rubric·원문 식별자를 확인해야 한다.

## 6. 출처별 판정과 검증 기록

- **직접 근거:** 각 논문/공식 문서의 좁은 주장 범위, 현재 로컬 source의 존재, 날짜가 있는 내부 실행 기록.
- **현재 전제와 모순:** 2026-09-25 보고서의 당시 workload-only 상태를 현재 JEV 통합 범위로 재사용하는 것; 과거 rate·pacing을 현재 값으로 쓰는 것.
- **가까운 참고:** 과거 XR 수업 제안서, 로봇 social navigation 결과를 사람의 비상 대피 설계에 참고하는 경우. 원래 평가 도메인을 함께 적는다.
- **근거 부족:** 실시간 JEV 접속/적합성, 프로젝트 3DGS·NeRF·Hunyuan 실행 receipt, 완전 결정론 replay, 현재 시설 대피 정확도, 실제 교육 효과, 수업 rubric 및 KORAIL 원문.

상세 검증 기록: [CV 연구](/Users/um-yunsang/CHOOGuard/.planning/2026-10-13-research-proposal/cv_research.md), [시뮬레이션 연구](/Users/um-yunsang/CHOOGuard/.planning/2026-10-13-research-proposal/simulation_research.md), [구현 점검](/Users/um-yunsang/CHOOGuard/.planning/2026-10-13-research-proposal/implementation_audit.md), [XR 준비도 점검](/Users/um-yunsang/CHOOGuard/.planning/2026-10-13-research-proposal/xr_course_fit.md), [수업 접근 확인](/Users/um-yunsang/CHOOGuard/.planning/2026-10-13-research-proposal/course_requirements.md).

CVF의 일부 직접 open은 403으로 보완 원문/검색 반환 자료를 사용했다. SocNavBench DOI 페이지 역시 403으로 publisher-deposited Crossref 서지와 저자 원문을 대조했다. 추가 성능 수치를 인용하려면 논문의 해당 표·입력 수·실험 조건을 함께 확인해야 한다.

XR01·XR02·VENDOR01의 정확한 공식 URL과 문서 내용/브랜드는 root의 같은 세션 웹 확인 결과를 전달받아 통합했다. 이 원장 작성 단계에서 별도 웹 요청을 반복하지 않았다.


## 사용자 직접 제공 과제 요건 (2026-10-13 추가)

이 대화에서 사용자가 가상현실 Term Project의 필수 항목으로 제작 취지, 개발 요구사항 및 개발 환경, 기술적 특징, 주단위 구현계획, 결과물 형상(구현 중간 결과물 또는 계획한 완성 모습 그림 포함), 기대효과를 직접 제공했다. 이 여섯 항목은 제안서에 반영한 확인된 사용자 제공 요구사항이다. Canvas 페이지 직접 열람, 추가 형식·마감, 코레일 문제정의서 원문 확인과 구분한다.

## 평가 계획에 추가한 근거

- NeurIPS 2021 [Agarwal et al.](https://proceedings.neurips.cc/paper/2021/hash/f514cec81cb148559cf475e7426eed5e-Abstract.html): 적은 반복의 불확실성·구간 추정 근거. RL 표본 수·성능을 게임에 이식하지 않는다. 원문 검증 후 평가 계획에 반영.
- CoRL 2025 [SocialNav-SUB](https://proceedings.mlr.press/v305/munje25a.html): 공통 입력·사람/규칙 기준선의 평가 구조. JEV 정합성이나 철도역의 공식 대응을 검증한 자료가 아니다. R17에 포함.
- 2024–2026 XR 연구 VR01–03은 EVALUATION_PROTOCOL.md에 원문 링크·적용 한계와 함께 수록. 장치·패널·노출 시간의 수치는 프로젝트 설계값이며 문헌이 보장한 안전·가독성 한계가 아니다.
- [Unity 6 Meta Quest 개발 안내](https://docs.unity3d.com/6000.0/Documentation/Manual/xr-meta-quest-develop.html): Meta Quest 3 지원 및 패키지 경로 확인. 기기 보유·접근·현 프로젝트 HMD 빌드 성공을 뜻하지 않는다.

현재 발표자료는 Unity 씬 캡처를 사용하지 않는다. 이미지들은 image2.5 생성 개념도이며 실제 형상·구현 결과의 증거가 아니다. 모델명·생성 원본·알파 배경 처리·파일 해시는 presentation/assets/generated/GENERATION_PROVENANCE.json에 기록한다. 원본 생성은 gpt-image-2.5-sunburst, 투명 배경 처리는 별도 image_gen 편집이다. Python으로 이미지 내용을 편집하지 않았다.

## FPS·시드 전환 설계에 추가한 근거

| ID | 1차 자료 | 직접 근거 | 프로젝트 적용 한계 |
|---|---|---|---|
| R18 | [Scenic 3.0, CAV 2023](https://people.eecs.berkeley.edu/~sseshia/pubs/b2hd-vin-cav23.html), [CVPR 2024 튜토리얼](https://scenic-lang.org/cvpr24/), [공식 생성 문서](https://docs.scenic-lang.org/en/3.x/reference/scene_generation.html) | 3D·시공간 제약과 조건부 확률 장면, 거부 추출·가지치기 | 튜토리얼과 논문 구별. Unity 어댑터·역 비상 대응·결정론 입증 없음 |
| R19 | [RESCUE 저자 프로젝트, ICCV 2025](https://cic.tju.edu.cn/faculty/likun/projects/RESCUE/), [저자 원문 §3](https://arxiv.org/html/2507.20117v2) | 인지·판단·운동 분리, A*와 힘 기반 속도·보행 제어 | 출구가 입력되며 임의 사고·위험 심리 생성의 근거가 아님. 전체 학습 보행 이식은 필수 아님 |
| R20 | [AutoGPT+P, RSS 2024](https://roboticsproceedings.org/rss20/p112.html), DOI10.15607/RSS.2024.XX.112 | 객체별 가능한 행동으로 상징적 계획 영역 구성 | 원 연구는 LLM 사용. 로컬 사건 조건표 전환·토큰 감소는 설계 추론·미측정 |
| R21 | [Infinigen Indoors, CVPR 2024 저자 원문 §3.2](https://arxiv.org/html/2406.11824v1) | 의미·관계·기하 제약과 생성 검사 | 사건 후보 검사에 적용하는 추론. 승인된 역 형상 재생성이나 실제 사고 빈도 근거 아님 |

현재 코드 재확인: IncidentDirector.Compose.cs의 BeginCompose는 사건 RNG를 이미 세계 시드에서 분리하고, Compose는 JEV Available을 요구한다. JevClient.cs Draw/DrawLevel은 모델 분포의 난수 추출이다. Observation은 NPC별 인지 정보만 전달한다. 사용자의 전체 오브젝트 입력 설명을 현재 매 요청마다 모든 원시 메시를 전송한다는 주장으로 확대하지 않는다. 새 설계는 전체 로컬 객체 목록·상태 갱신·조건 기반 가중치와 독립 난수 흐름이다. Unity·게임 코드 변경 없이 제안만 작성했다.

## 제작 취지·사용 흐름·최종 정보 위계 정정

사용자 직접 제공: 현실 훈련의 대본·자원 동원·재현 범위의 제약을 문제로 삼고, 디지털트윈의 랜덤 상황 플레이를 통한 경험 축적과 실제 대응 속도·정확도 향상을 기대한다. 발표의 최상위 핵심은 디지털트윈을 왜·어떻게·어떤 기술로 만드는가다. 현실 훈련 전체의 무효나 실제 향상 효과를 검증한 자료로 취급하지 않는다.

현재 프로젝트 결정과 구현 확인: docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/DESIGN.md 및 docs/CHOOGuard_FPS_Prior_Research_20260925/DECISIONS_AND_DEVELOPMENT_STRUCTURE.md는 완성 사건 pack·고정 결말 대신 현재 객체의 가능한 원자 전이와 자율 NPC·FPS 개입으로 다음 전개를 갱신하는 구조다. 현재 JevWorldInventory는 등록된 실내 객체를 집계하고 Compose는 실제 원인·전개를 열거·평가·재검사·실행한다. 모든 원시 메시가 매 요청 전송되거나 모든 설비의 원인이 연결됐다고 확대하지 않는다. 예전에 root가 만든 통로막힘/우회안내 대표 사용 사례와 한 사건 범위는 사용자에게 부정됐고 철회했다. 해당 r4–r7 서사/AAA는 현재 승인 근거가 아니다.

R22 Stefan et al., How effective is virtual reality for electrical safety training? Evaluating trainees’ reactions, learning, and training duration, Journal of Safety Research90:48–61(2024), DOI10.1016/j.jsr.2024.06.002. [저자 기관 원문](https://dro.deakin.edu.au/articles/journal_contribution/How_effective_is_virtual_reality_for_electrical_safety_training_Evaluating_trainees_reactions_learning_and_training_duration/26172529):18명 사전·사후·4주 지식 평가, 대조군 없이 실제 대응 전이·암묵지·속도 정확도 근거 아님.
R23 Narciso et al., Assessing the perceptual equivalence of a firefighting training exercise across virtual and real environments, Virtual Reality28:14(2024), DOI10.1007/s10055-023-00917-6. [출판사](https://link.springer.com/article/10.1007/s10055-023-00917-6):12명 가상/현실 과제 비교, 지식 전이의 통계적 한계·생리적 스트레스 차이. 본 게임의 학습 효과로 옮기지 않음.

## 생성·판단 모델과 오픈월드 적용 판단 정정

사용자는 JEV 같은 decision 모델이 완전 랜덤 오픈월드에 가까워질 수 있는지 학술자료로 판단해 적용하라고 요청했다. 실행 중 모델 호출0 목표를 철회했다. 전체 객체 상태 등록·생성 계획·JEV 판단·조건 검사·Unity 실행·결과 피드백, 상태 변화 호출·계획 재사용을 제안에 반영했다. 코드와 Unity 장면은 변경하지 않았다. 표지 복원 지시에 따라 최종 발표는 표지1장+필수본문6장이다.

- R24 SimWorld, NeurIPS2025, DOI10.52202/085713-5519. [공식 논문](https://proceedings.nips.cc/paper_files/paper/2025/hash/f1eff44d59d9c66cc6e359d36d80b0b9-Abstract-Conference.html): Unreal5·모델 행동·피드백, 항법과 음식 배달의 실험. Unity·철도역 비상 전개 입증 아님.
- R25 AgenticCache, MLSys8(2026), Kim/Wu/Tambe. [공식 논문](https://proceedings.mlsys.org/paper_files/paper/2026/hash/c66a9db149261435664284a20b6f1d42-Abstract-Conference.html): 계획 전이 재사용과 비동기 갱신. 코드 공개. 맥락 변화에 따른 무효화가 필요하며 프로젝트 절감률은 미측정.
- R26 ADaPT, NAACL Findings2024, pp4226–4252, DOI10.18653/v1/2024.findings-naacl.264. [공식 논문](https://aclanthology.org/2024.findings-naacl.264/): 실행이 어려울 때 분해·계획. ALFWorld/WebShop/TextCraft의 조건으로, 실시간 Unity 군중 검증이 아님.
- R27 Voyager. [저자 프로젝트](https://voyager.minedojo.org/):2023 공개 원문 확인. 기술 저장·조합·피드백을 연구 참고로만 유지하며 venue를 본 세션에서 재확인하지 않아 TMLR 서지로 단정하지 않음.
- R28 Virtual Community, ICLR2026. [저자 프로젝트](https://virtual-community-ai.github.io/), [원문](https://virtual-community-ai.github.io/paper.pdf): 공간 연계 목표·기억·사회 관계·계층적 계획, Genesis. 행동 범위 선택·kinematic attachment와 진행 추적 한계. 엔진 전체 교체나 역 사고 인과 검증 근거 아님.
- R29 TypeSafe. [Introduction](https://docs.typesafe.ai/introduction), [API](https://docs.typesafe.ai/api), [OpenAPI](https://api.typesafe.ai/openapi.json),2026-10-13 확인: Choice는 제공된 후보, Score는 제공된 기준, Noul은 yes 판단. 모델·usage 응답 제공. 계획·새 실행 의미·물리 법칙을 생성하는 API는 아님. 제품 문서이지 peer-reviewed 행동 타당성 결과가 아님.

원문 확인은 직접 지원하는 범위로만 사용했다. 모델이 새로운 목표와 행동 조합을 제안하면 사용자의 오픈월드 목표에 기여할 가능성이 있지만, 모든 객체의 실행 의미·현장 사고의 빈도·무제한 임의 상호작용이 자동으로 제공된다는 근거는 없다. 적용은 제안 구조이며 완료나 비용 절감을 주장하지 않는다.

## 부품 직접 조작과99% 요구사항
사용자는 My Summer Car처럼 부품·도구를 직접 조작하고 현실 대응 조작99%와 복잡성을 재현하도록 요구했다. 이는 개발 목표이며 현재 성능·구현으로 확인한 사실이 아니다. [공식 게임 페이지](https://store.steampowered.com/app/516750/My_Summer_Car/)에서 부품 조립·운전·엔진 시뮬레이션을 확인했다. [Unity HingeJoint](https://docs.unity3d.com/6000.0/Documentation/Manual/class-HingeJoint.html), [ConfigurableJoint](https://docs.unity3d.com/6000.0/Documentation/Manual/class-ConfigurableJoint.html), [XRI3.3](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/index.html)의 관절·입력·잡기를 구현 근거로 확인했다. [OpenHOI NeurIPS2025](https://proceedings.neurips.cc/paper_files/paper/2025/hash/f376f5dff6f6ec6364aea7a46ab49574-Abstract-Conference.html)는 손·객체 부위 연결 연구의 참고로만 유지하며 플레이어 조작 자동 구현이나99% 근거로 쓰지 않았다.

그림 object-interaction은 gpt-image-2.5-sunburst 생성이며 배경 제거는 image_gen으로 수행했다. 첫 편집은 도구 출력심사에서 거절됐고, 배경 제거만 수행하는 호출은 완료됐다. 원본·알파 파생본과 경로·해시를 보존했다. 색상 치환은 하지 않았으며 기존 Python 색상 편집 질문의 승인도 추정하지 않았다.

## 최종 색상 보정 승인과 적용
이전의 Python 미편집·승인대기는 당시 기록이다. 사용자가 “Python으로 네 색상 고정”을 명시적으로 승인한 뒤, 사용 그림1종에 한해 네 지정색의 최근접 RGB로 색상만 치환했다. 원본·알파 파생본은 보존했다. 보정본은 RGB정확4값·1536×1024·동일 알파·동일 비투명 마스크로 확인했다. palette_correction_checks.json과 생성 이력에 원본/출력 해시·승인·방법을 남겼다. Figma·HTML·PDF·OpenDesign에는 보정본을 사용한다. 이 처리는 개념도의 색상 변경이며 실제 현장 형상·구현·99% 재현율의 근거가 아니다.

## 디지털 트윈의 대상 명시
사용자가 한국철도공사(코레일)의 부산역과 주변 지역이 디지털 트윈 대상이라고 직접 확인했다. 표지·제작 취지·요구사항·기술 과정·주별 계획·결과물 설명에 반영했다. 주변 지역의 반경·면적·경계는 임의로 정하지 않으며1주차 지도·도면의 구축 범위로 표시할 계획이다. 현재 내부 구축 작업을 대상 전체의 완성으로 확대하지 않는다. 그림은 공간 표현과 조작 방식의 생성 개념도이며 부산역의 실제 형상·주변 지역 측량 결과는 아니다.
