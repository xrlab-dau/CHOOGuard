# Agora-2와 shared realtime multi-agent world: CHOOGuard 선행조사

> 조사 기준: 2026-09-25. **개발계획이 아니라 공개자료 검증 및 선택지 비교**다. CHOOGuard 프로젝트 실행·설치·빌드·테스트·모델링 변경은 하지 않았으며, 제품 통합과 성능은 실행 미검증이다. 외부 Agora 웹 데모의 시작/캐릭터 선택 화면과 공식 영상의 표본 프레임은 직접 확인했다. 최신 사용자 지시에 따라 **라이선스·상용 여부를 검색 제외 기준으로 사용하지 않았다**. 유료·회원·연구 프리뷰도 포함하고 권리 정보는 메타데이터로만 기록한다.

## 1. 결론부터: 무엇이 확인되었나

1. **Agora-2는 실제 Odyssey의 2026-09-21 공개 연구 프리뷰**다. Agora.io의 RTC/음성 SDK나 Microsoft Agora Workbench와 다른 제품이다. 공식 문구 `up to 20 humans and agents`는 존재한다. 그러나 기술보고서의 배포 구성은 **최대 인간 조작자 4명 + 자율 엔터티 16개**다. **20명의 인간이 동시에 서로 다른 화면으로 플레이한다는 뜻으로 읽으면 틀린다.** [공식 발표 본문](https://odyssey.systems/introducing-agora-2), [기술보고서 §1, §4.3, §6](https://agora-2.odyssey.systems/agora-2.pdf).
2. **단순히 그럴듯한 영상을 각자 생성하는 방식만은 아니다.** 명시적 shared state, 학습된 상태전이 모델, 서버의 상태 통합/생명주기 처리, 각 플레이어의 별도 생성 렌더러가 있다. 다만 **학습 모델만으로 이루어진 범용 게임 엔진도 아니다.** 맵·시나리오·스폰·일부 world-object 효과는 native-engine scaffolding에 남는다. [기술보고서 §3–6](https://agora-2.odyssey.systems/agora-2.pdf).
3. **실시간 수치는 목표와 측정을 구분해야 한다.** 640×480, 표시 30 FPS, 30 Hz 시뮬레이션 timebase는 publisher-reported 구성/목표다. 보고서 스스로 **실제 멀티에이전트 상호작용의 sustained FPS와 input-to-display latency를 정량 평가하지 않았음**을 명시한다. [§8, Appendix A.4–A.6](https://agora-2.odyssey.systems/agora-2.pdf).
4. **공유 상태가 같아도 생성 화면의 완전 일치는 보장되지 않는다.** 개별 화면의 외형·가시성·사건 타이밍은 어긋날 수 있다. 장기 시뮬레이션 정확도, cross-view consistency, end-to-end action adherence, 전체 learned system에서의 agent-policy 성능은 이 보고서에서 평가하지 않았다. [§8](https://agora-2.odyssey.systems/agora-2.pdf).
5. **Odyssey의 공개 API 문서는 있지만 Agora-2 API라고 확인되지 않았다.** 문서는 Odyssey-2 Pro, 실제 개발자 입구는 Odyssey-2 Max의 기존 고객 롤아웃을 안내한다. Broadcast는 같은 영상의 다중 시청이지 Agora식 다중 제어자/다중 시점 shared world가 아니다. [API 소개](https://documentation.api.odyssey.ml/), [FAQ](https://documentation.api.odyssey.ml/faq.md), [개발자 입구](https://developer.odyssey.ml/).
6. **CHOOGuard에 현재 가장 실용적인 해석은 '인간과 AI가 같은 검증된 Unity 세계에서 행동한다'이다.** 생성 영상이 전부인 세계를 철도 정비/안전 절차의 정답 엔진으로 삼을 공개 근거는 없다. 이는 생성 세계 연구를 버리자는 말이 아니라, 정답 상태·AI 의사결정·영상/월드 생성 실험을 분리하자는 연구상 권고다. 상세 구현 결정은 사용자 선택 이후의 별도 단계다.

## 2. Agora-2 식별·접근·공개 상태

| 항목 | 확인 결과 | 출처·확인 수준 |
|---|---|---|
| 정확한 명칭 | `Agora-2: An Action-Conditioned World Model for Real-Time Multiplayer Environments`, Odyssey | [기술보고서 PDF](https://agora-2.odyssey.systems/agora-2.pdf), 다운로드 원문 텍스트 확인 |
| 발표일 | 본문 **September 21st, 2026**. 사이트 공통 metadata는 Sep 24, 2026 23:02 UTC로 표시되어 본문 발표일과 분리 | [공식 발표](https://odyssey.systems/introducing-agora-2), 본문/metadata 확인. PDF 자체 별도 발행일은 확인하지 못함 |
| 전작 | Agora-1은 본문 **May 18th, 2026**, GoldenEye 기반 최대 4인 FPS 연구 프리뷰. Agora-2는 Diablo II 캡처로 학습했다고 소개하는 등각 시점 action-game 환경 | [Agora-1](https://odyssey.ml/introducing-agora-1), [Agora-2](https://odyssey.systems/introducing-agora-2), 본문 확인 |
| 출시 형태 | playable research preview. 논문·공식 페이지·브라우저 데모가 공개되어 있음 | [데모](https://agora.odyssey.systems/), 브라우저 시작 버튼 클릭 후 이름 입력/5개 클래스 선택 UI까지 실제 확인. 전투 세션 입장·성능 측정은 안 함 |
| 동시 인원 | `up to 20 humans and agents`는 공식 주장. 배포는 **human-controlled 1–4 + autonomous 16** | [PDF §4.3, §6.4](https://agora-2.odyssey.systems/agora-2.pdf). **publisher-reported**, 현장 부하 시험 아님 |
| API/SDK | Agora-2 전용 공개 API, entity/state 조작 API, C#/Unity SDK를 이번 조사에서 확보하지 못함 | [공식 문서 전체 index](https://documentation.api.odyssey.ml/llms.txt), [JS SDK](https://documentation.api.odyssey.ml/sdk/javascript/introduction.md) 확인. 아래 Odyssey API와 혼동 금지 |
| 코드·weights·훈련 코드 | Agora-2의 공식 공개 다운로드/저장소를 확보하지 못함. 논문에 구조/훈련 설정은 있으나 재현 가능한 소스·weights 릴리스 확인과 다름 | 공식 발표·PDF·문서 index 및 `Agora-2 Odyssey github weights code SDK API` 검색. **비공개 확정이 아니라 공개 확보 미확인** |
| 가격·상용 접근 | Agora-2 세션 가격표, SLA, 전용 상용 계약 조건 확보 미확인 | [법무 페이지](https://odyssey.systems/legal), 본문 확인. 일반 API 계약과 Agora 연구 프리뷰를 구분해야 함 |
| 접근 문의 | 일반 Odyssey API는 기존 사용자에게 Max 롤아웃, 새 사용자는 priority-access 문의 경로 | [실제 입구](https://developer.odyssey.ml/)에서 직접 읽음. 링크는 [공식 문의](https://odyssey.ml/contact). Agora-2 전용 제공 약속은 아님 |
| 라이선스 메타데이터 | 독점 서비스/연구 프리뷰. 일반 Terms와 API 계약이 있으며 공개 OSS 라이선스 미확인 | [Terms 2025-11-17, API License 2026-01-22](https://odyssey.systems/legal). 코드·모델·Diablo II/GoldenEye 표현물 권리는 같은 것이 아님 |

**날짜 주의:** Odyssey의 Agora-1과 Agora-2 페이지가 같은 사이트 metadata 시각을 내보낸다. 이를 각 모델의 최초 발표일로 일괄 인용하지 않았다. 현재 접근 화면과 과거 발표문을 함께 읽되, 과거 발표의 접근 조건을 오늘의 보장으로 바꾸지 않았다.

## 3. 실제 아키텍처: '생성형 영상'과 '공유 세계' 사이

### 3.1 공식 설명을 기술보고서로 분해

[PDF §3–6](https://agora-2.odyssey.systems/agora-2.pdf)의 루프는 다음과 같다.

1. 인간 브라우저 입력과 자율 정책의 action intent를 entity ID에 연결한다.
2. simulation model이 구조화된 상태 이력, action, local geometry로 이동·방향·상호작용 결과를 예측한다.
3. world server가 예측 출력을 실제 shared state에 통합한다. 체력 변화, 사망, corpse expiry, spawn/respawn 및 session rule을 처리한다.
4. shared state와 맵·외형 정보를 각 시점의 condition으로 구성한다.
5. 각 플레이어의 rendering model이 그 condition과 **자기 시점의 유한한 visual history**를 이용해 새로운 video latent를 만든다.
6. decoder가 frame으로 풀고 H.264/WebRTC로 전달한다. 상태는 다음 step의 모델/agent observation에도 제공한다.

핵심 식은 `U = D(state history, joint actions, geometry)` 다음 `S_next = H(S, U)`이다. **D의 추정과 H의 규칙 적용은 다른 책임**이다. `learned game engine`이라는 홍보 표현을 '맵, 충돌, 월드 오브젝트 효과, 규칙까지 아무 수작업 구조 없이 영상만으로 생긴다'로 번역하면 보고서와 어긋난다.

### 3.2 내부 geometry/state가 있다는 사실과 공개 object API는 다르다

| 내부 표현·제어 | 기술보고서 내용 | CHOOGuard 해석 |
|---|---|---|
| local geometry | 캐릭터별 **11×11 obstacle distance grid** | 단순 비디오 입력만으로 모든 물리 규칙을 추론하는 형태가 아님 |
| 상태 | identity, 위치/방향, health, animation, death/respawn 및 effects | 열차 제동계, 전원 격리, 작업허가, 선로 점유 등 철도 상태가 기본 제공된다는 근거 없음 |
| movement | **14 predefined movement branches** 중 선택 후 fixed decoding | 충돌/겹침 회복에도 정해진 구조가 존재. 일반 강체 solver나 continuum physics라는 뜻이 아님 |
| 렌더링 condition | **12×12 local tile grid**, entity/effect token, view/roster | 의미 있는 geometry conditioning은 있으나 Unity `GameObject`/`Collider`/`Rigidbody`나 편집 가능한 철도 object schema 제공은 미확인 |
| 슬롯 | simulation: human 4 + other character 16, projectile 32. renderer token 표에는 별도로 four user-controlled + 32 other entity slots | **렌더러의 entity slot 수를 36명 플레이어 지원으로 오해하면 안 됨** |
| native scaffolding | maps, scenarios, spawning, scenario-owned world-object effects | 보고서 보스전의 portal 생성도 scenario-owned rule. '모든 이벤트를 AI가 발견해 만들었다'는 증거 아님 |

위 숫자는 모두 **publisher-reported**이며 [PDF §4–6](https://agora-2.odyssey.systems/agora-2.pdf)의 구성 설명이다.

**외부 개발자 관점 미확인:** entity 생성/삭제·권한·형상 업로드·정밀 object transform·충돌 질의·물성값·사용자 정의 상태 schema·Unity 장면 import/export·rail-domain fine-tuning·save/restore API. 내부 논문 표현이 있다고 외부 API도 있다고 간주하지 않는다.

### 3.3 세션·기억·동시 시점

- shared entity state는 **세션 내** 유지된다. renderer cache/history는 시점마다 독립적이며 alive/non-alive 전환, respawn, 큰 camera discontinuity에서 reset된다. **상태의 지속과 화면 기억의 지속이 같지 않다.** [§6.3–6.5, Appendix A.4](https://agora-2.odyssey.systems/agora-2.pdf).
- 네트워크 재연결 후 상태 복원, 영구 저장, 세션 상한 시간, 호스트 장애 복구, 장시간 월드 persistence는 공식 공개 자료에서 확보 미확인이다.
- training window의 40 video frames/20 latent positions와 decoder의 three-latent ring cache가 서술되어 있으나, 이를 곧바로 '전체 월드 기억은 N초'라고 환산하면 안 된다. 구조화된 state와 temporal cache의 역할이 다르다. [§4.6, Appendix A.1/A.4](https://agora-2.odyssey.systems/agora-2.pdf).
- Agora-1은 state/dynamics/rendering이 더 결합된 루프였고 Agora-2는 independent simulation model로 재구성되었다. 전작 소개의 '둘 다 entirely learned' 표현을 Agora-2의 전체 서버 구성에 그대로 적용하지 않는다. [Agora-1 본문](https://odyssey.ml/introducing-agora-1), [Agora-2 PDF §1](https://agora-2.odyssey.systems/agora-2.pdf).

## 4. 성능·일관성: 검증된 수치와 미검증 주장

모든 표 수치는 **publisher-reported**이다. 독립 재현/실측을 수행하지 않았다.

| 항목 | 공개값 | 실제 의미/주의 |
|---|---|---|
| 해상도 | 640×480 per-view | renderer 출력 사양. 홍보 MP4의 좌우 합성 화면 해상도와 별개 |
| 표시/시뮬레이션 | 목표 30 displayed FPS, 15 latent updates/s; simulation 30 Hz timebase | **라이브 sustained FPS 검증 아님** |
| 사람 시점당 GPU | RTX PRO 4500 1장; 4인 시점 세션은 4장, 한 장이 shared simulation/policy도 담당 | **20인 인간용 GPU 비용이라고 외삽 금지**. 전체 서비스 비용/큐 대기/SLA 자료 없음 |
| renderer-only 최적화 | 평균 62.92 ms → 56.69 ms per two-frame call | RTX PRO 4500 Blackwell, MXFP8, TeaCache 등 지정 조건. encode/network/input latency가 빠진 값 |
| conditioning cost | structured-prefix core 합 35.37 ms | 다른 GPU(RTX PRO 6000), synthetic input/randomly initialized denoiser cost test. 운영 지연으로 인용 금지 |
| 영상 reconstruction | PSNR 30.11 dB vs baseline 20.67 dB | 30 clip×3 seeds, 짧은 reference window. complete-system 비교이고 학습 budget/context가 맞춰진 architecture ablation 아님 |
| 이동 single-step 정확도 | 전체 branch 85.17%, blocked subset 68.17% | held-out 기록의 한 step 평가. 장기 경로 성공률/충돌 안전성/철도 대응 정확도 아님 |
| animation | mode accuracy 95.84%; mode-switch 7.49% vs recorded 3.54% | 높은 frame 정확도와 시간적 불안정이 동시에 존재할 수 있음 |
| AI 정책 cadence | 4 simulation ticks마다 action, timebase상 7.5 decisions/s | 측정된 wall-clock inference rate 아님 |

출처: [기술보고서 §6–8, Appendix A.5–A.6](https://agora-2.odyssey.systems/agora-2.pdf), 다운로드 원문 확인.

### 가장 중요한 제한 — 저자가 직접 명시

- predefined state schema, fixed appearance vocabulary, instrumented environment가 필요하다.
- 새 assets/rules/environments로의 전이는 **untested**다. 여러 procedural layout/theme를 지원한다는 것과 범용 철도 세계를 프롬프트만으로 지원한다는 것은 다르다.
- state-transition 오류는 rollout에서 누적될 수 있고, native-recorded state로 학습된 renderer가 오류가 섞인 predicted state를 받으면 input shift가 생긴다.
- shared state가 동일해도 독립 생성 화면의 appearance, visibility, event timing이 불일치할 수 있다.
- long-horizon visual quality, cross-view agreement, end-to-end action adherence는 보고서에서 정량 평가하지 않았다.
- policy 평가용 native-engine episode/case 집합이 있어도 전체 learned dynamics+renderer 세계에서의 policy 성능을 입증한 것은 아니다.

모두 [PDF §8 및 Appendix B.2](https://agora-2.odyssey.systems/agora-2.pdf). **따라서 '멀티플레이가 가능하다'는 발표는 존중하되, 완전한 월드 물리/정답 엔진/동기화 품질이 입증됐다고 과장하지 않는다.**

## 5. Odyssey 일반 API를 Agora-2 API로 착각하지 않기

[공개 API index](https://documentation.api.odyssey.ml/llms.txt)와 [소개](https://documentation.api.odyssey.ml/)는 Odyssey-2 Pro용 interactive streams / viewable streams / asynchronous simulations를 설명한다. [JS SDK](https://documentation.api.odyssey.ml/sdk/javascript/introduction.md)는 `connect`, `startStream`, `interact(prompt)`, `simulate`, recordings, broadcast spectator 연결을 제공한다. Python 문서도 index에 포함된다. **읽은 자료에서 C#/Unity SDK, Agora-2 selector, shared entity command API는 확인하지 못했다.** 문서 페이지별 게시일은 표시되지 않았다.

- **Broadcast:** 다수가 같은 running stream을 본다. spectator는 prompt를 보내지 못한다. 여러 사람 입력의 중재는 애플리케이션 측 orchestration 몫이다. 이는 서로 다른 시점에서 각자 움직이는 Agora-2와 다르다. [FAQ](https://documentation.api.odyssey.ml/faq.md).
- **Session 숫자:** 일반 API는 기본 stream 150초, connection 60분; free-tier concurrent streams 5를 문서가 예로 들고, batch simulation queue는 10개라고 한다. 모두 **publisher-reported 일반 Odyssey API 값이며 Agora-2 세션 제한으로 전용하면 안 된다.** [duration](https://documentation.api.odyssey.ml/stream-duration-limits.md), [session management](https://documentation.api.odyssey.ml/session-management.md).
- **문서 내 불일치:** duration 표는 stream 종료 후 connection을 유지해 재시작할 수 있다고 하고 같은 페이지의 자동 재연결 설명은 lease expiry가 connection도 닫는다고 한다. 계약 전 확인할 사항이다. 동일 prompt 재시작 설명도 shared semantic state의 영속성을 증명하지 않는다.
- **현재 접근:** 실제 JS 렌더링된 [developer portal](https://developer.odyssey.ml/)에서 `Odyssey-2 Max is now being rolled out to existing API users`를 읽었다. 신규자는 [priority access 문의](https://odyssey.ml/contact)로 연결된다. 문서 Pro와 입구 Max의 차이를 숨기지 않는다.
- **가격:** 현재 공개 문서 index와 비로그인 입구에서 Agora-2 단가 및 일반 API의 확정 최신 단가표는 확보하지 못했다. 검색의 `odysseyapi.tech` 가격표는 이 Odyssey 공식 서비스라는 근거가 없어 채택하지 않았다.
- **Unity 가능성 [INFERENCE]:** 영상 스트림을 표시하고 외부 서비스를 연결하는 통합 자체는 원리상 생각할 수 있지만, 그것이 Unity 씬의 형상/충돌/상태를 Agora로 상호 교환한다는 뜻은 아니다. 공식 connector와 프로젝트 호환성은 미검증이다.
- **권리/데이터 한 줄:** [일반 서비스/API 계약](https://odyssey.systems/legal)은 상용·통합·개인정보·model-training 용도와 데이터 재사용에 조건을 두므로 계약 확인 항목이며, 조사 후보 배제 사유로 사용하지 않았다.

## 6. 비교군: 같은 'world model'이라는 이름 아래 다른 산출물

### 6.1 Google Genie 3 / Project Genie

- **무엇인가:** 텍스트/이미지로 만든 시각 세계를 action으로 탐색하는 실시간 생성 모델. 발표는 2025-08-05; [공식 발표](https://deepmind.google/blog/genie-3-a-new-frontier-for-world-models/) 본문에서 **720p, 24 FPS, 수 분 일관성**을 주장했다. 현재 [모델 소개](https://deepmind.google/models/genie/)는 **20–24 FPS**라고 표기한다. 모두 **publisher-reported**, CHOOGuard나 독립 실측값 아님.
- **접근 상태:** [2026-01-29 Project Genie 발표](https://blog.google/innovation-and-ai/models-and-research/google-deepmind/project-genie/)는 미국의 18세 이상 Google AI Ultra 사용자에게 rollout, 60초 생성 제한을 안내한다. 현재 모델 페이지에도 [Project Genie](https://labs.google/projectgenie)가 연결된다. **현재 한국 계정의 사용 가능 여부/가격·지역 확대는 로그인 실확인하지 않았다.** 옛 limited-preview만을 오늘 접근상태라고 쓰는 것도, 미국 발표만 보고 한국에서 사용 가능하다고 쓰는 것도 피한다.
- **제한:** 공식적으로 제한된 action space, 여러 독립 agent의 상호작용, 정확한 지리, 글자 표현, 지속시간 문제가 있다. Project Genie는 물리 불일치와 조작 latency도 명시한다.
- **적용 위치 [INFERENCE]:** 철도 공간 분위기·재난 시각 연출·탐색 경험의 연구 sandbox. 계기판 숫자/표지판/정비 판정을 맡길 엔진으로 확인된 것은 아님.
- **형태·권리:** hosted research prototype; 공개 Unity object API/weights는 이번 자료에서 확보 미확인. 상용 서비스/지역별 계정 조건은 별도 확인 대상.

### 6.2 World Labs Marble / World API — '영상만'과 다르게 내보낼 3D 자산이 있음

- **실제 공개 계약:** [World generation API](https://docs.worldlabs.ai/api/reference/worlds/generate)는 text/image/multi-image/video에서 world 생성 job을 시작하고 operation completion을 기다리는 **비동기 자산 생성 API**다. 모델 enum에 `marble-1.0-draft`, `marble-1.0`, `marble-1.1`, `marble-1.1-plus`가 공개되어 있다. 문서 발행일 미표시, 본문/OpenAPI 확인.
- **export:** [Gaussian splat export](https://docs.worldlabs.ai/marble/export/gaussian-splat), [collider/HQ mesh GLB export](https://docs.worldlabs.ai/marble/export/mesh)가 실제 문서화되어 있다. splat은 외형, collider mesh는 단순 물리 충돌용이며 똑같은 것이 아니다. 고품질 mesh에도 구멍·floaters·투명/반사면/가는 구조물의 재구성 오류를 문서가 경고한다.
- **Unity:** [공식 Unity export 안내](https://docs.worldlabs.ai/marble/export/gaussian-splat/unity)는 community GaussianSplatting plugin과 수정 fork를 연결한다. Unity 6.1 사용 사례와 draw-order/import 문제를 함께 적고, VR에서는 6.3 비호환 사례를 기록한다. **CHOOGuard 6000.3.23f1/URP 17.3.0에서 호환된다고 검증한 것은 아니다.** FAQ 일부는 collider 지원을 'soon'이라고 하는 반면 현재 mesh export 문서는 이미 지원하므로 문서 시대 차이도 주의해야 한다.
- **가격/획득:** [API 가격](https://docs.worldlabs.ai/api/pricing)은 **publisher-reported** 1 USD=1,250 credits, 최소 5 USD; standard world generation 1,500 credits에 input panorama 비용이 추가되고 HQ mesh export 3,500 credits를 제시한다. Marble 앱 구독과 API 크레딧은 별도다. 실제 결제·호출은 안 했다.
- **플랜:** [billing 문서](https://docs.worldlabs.ai/marble/support/account-billing)는 Free/Standard/Pro/Max 및 enterprise 문의를 설명한다. Standard export, Pro HQ mesh 및 commercial rights라는 현재 문서 구분만 메타데이터로 남긴다.
- **적용 위치 [INFERENCE]:** 배경 공간/콘셉트 blockout/비교용 시각 레퍼런스. 선로·장비의 치수, 작업구역 접근성, 충돌면, 위험 의미는 별도 제작·검증이 필요하다. 내보낼 mesh가 있다는 것만으로 기능적 철도 설비, 절차, multiplayer state가 생성되는 것은 아니다.

### 6.3 NVIDIA Cosmos 3 — 공개 모델/훈련 도구와 물리 AI 연구

- **최신 확인:** [공식 소개](https://www.nvidia.com/en-us/ai/cosmos/), [2026-05-31 본문 발표/2026-06-01 UTC metadata](https://blogs.nvidia.com/blog/cosmos-3-physical-ai-open-world-foundation-model/)는 Cosmos 3의 reasoning·world/action generation을 소개한다. 현재 [공식 GitHub](https://github.com/NVIDIA/cosmos), [Hugging Face 모델 collection](https://huggingface.co/collections/nvidia/cosmos3)도 실제 읽었다.
- **접근 상태:** 공식 repo에 inference cookbooks, evaluation, 후학습용 [Cosmos Framework](https://github.com/NVIDIA/cosmos-framework) 경로가 있고 모델 collection은 Super/Nano/Edge를 나열한다. **저장소/모델 페이지 공개 확보**, 실제 weights 다운로드·훈련은 하지 않음. Framework는 연결 경로 확인 수준이며 별도 실행 검증 아님.
- **중요한 구분:** repo가 말하는 reasoner는 text/vision→text, generator는 text/vision/sound/action→vision/sound/action이다. 드라이버 예제의 `fps=24` 출력이나 Edge의 'real-time policy'를 **일반 다중 사용자 게임의 24 FPS 생성·낮은 네트워크 지연**으로 바꾸면 안 된다.
- **제한:** repo 자체가 temporal inconsistency, object morphing, implausible dynamics를 경고하고 safety-critical application에 추가 검증과 system-level safety analysis를 요구한다.
- **적용 위치 [INFERENCE]:** 장면 변화/희귀 상황 synthetic data, 시각 인식·정책 연구, 정비 손동작의 action-conditioned 시각 연구. 철도 정비 정답 엔진이나 Unity multiplayer backend를 즉시 대체할 turnkey 제품으로 확인되지 않았다.
- **라이선스 한 줄:** 현재 [repo LICENSE 원문](https://github.com/NVIDIA/cosmos/blob/main/LICENSE)은 **OpenMDW-1.1**이다. 옛 Cosmos 세대의 Apache/NVIDIA 모델 약관을 최신 전체 family에 일괄 적용하지 않는다. 개별 모델/데이터 부속물은 각각의 메타데이터가 기준이다.

### 6.4 Microsoft Muse / WHAM / WHAM-RT

- [2025-02-19 Muse 발표](https://www.microsoft.com/en-us/research/blog/introducing-muse-our-first-generative-ai-model-designed-for-gameplay-ideation/)는 Bleeding Edge로 학습한 visual/action model 및 weights, sample data, WHAM Demonstrator 제공을 안내한다. [현재 WHAM 페이지](https://www.microsoft.com/en-us/research/project/wham/wham/)는 [Azure AI Foundry의 Muse](https://ai.azure.com/explore/models/Muse/version/1/registry/azureml) 획득 경로를 유지한다. **공식 제공 안내 확인**, 모델 다운로드/계정 내부 확인은 안 했다.
- 초기 Muse 모델 해상도는 발표 본문상 **300×180**이고, [WHAM-RT 공식 소개](https://www.microsoft.com/en-us/research/project/wham/wham-rt/)는 Quake II 데모 **640×360, 10+ FPS**를 주장한다. 모두 **publisher-reported**. 초기 Muse, 실시간 WHAM-RT와 모든 게임의 일반화를 혼동하지 않는다.
- WHAM-RT는 [Copilot Labs 데모](https://aka.ms/muse-quakeii-whamm) 획득 경로가 있다. 현재 access 가능 여부와 동시인원, Unity API는 이번에 직접 확인하지 않았다.
- **적용 위치 [INFERENCE]:** FPS 조작/생성영상/action-conditioned continuation의 연구 비교, 시나리오 ideation. 공개 weights가 있다고 철도 물리·절차가 학습되어 있거나 사용자 규칙으로 쉽게 이식된다는 뜻은 아니다.
- **권리 한 줄:** 공식 open-weights 제공 안내는 확인했지만 이번 조사에서 내려받은 배포물 LICENSE는 확보하지 않았으므로 구체 라이선스/상용 가능을 확정하지 않는다.

### 6.5 OpenAI Sora 자료의 올바른 위치

[2024-02-15 기술 글 'Video generation models as world simulators'](https://openai.com/index/video-generation-models-as-world-simulators/)은 emerging 3D consistency/object permanence와 Minecraft 형태의 영상 생성 예를 설명한다. 동시에 유리 파손 등 기본 물리, 음식 섭취 후 object state 변화, 장기 incoherence·갑작스러운 object 등장 실패를 직접 밝힌다. **본문 확인 수준의 역사적 개념 비교**이며 2026년 Sora 제품의 최신 접근/성능 상태로 인용하지 않는다. 이 글만으로 공개 shared realtime multiplayer API나 철도 physics object API가 있다는 결론은 낼 수 없다.

### 6.6 Agora-2가 인용한 직접 비교 연구: 실제 공개 구현까지 구분

추가로 네 연구의 arXiv 초록과 저자 제공 페이지/저장소를 확인했다. 아래 성능은 모두 **publisher-reported**이며 독립 실행·재현하지 않았다. 비교 용도는 shared-state 표현, multi-view 평가, action-conditioned 생성 연구이고 철도 정답 모델의 후보 인증은 아니다.

| 연구·발표일 | 공개 확인 수준·획득 경로 | 무엇을 비교할 수 있나 / 제한 |
|---|---|---|
| **MIRA**, 2026-07-06 | [arXiv 초록](https://arxiv.org/abs/2607.05352), [공식 코드 repo README/파일 목록](https://github.com/mira-wm/mira), [LICENSE 원문](https://github.com/mira-wm/mira/blob/main/LICENSE) 확인. Apache-2.0 코드, codec/world-model 훈련 및 평가 entrypoint가 실제 공개. [Rocket Science dataset](https://huggingface.co/datasets/kyutai/rocket-science)은 repo가 제공하는 획득 경로; dataset 본문/다운로드는 별도 미확인. | Rocket League 네 플레이어 action에 조건화한 생성 세계. 초록의 5B, 10,000시간 데이터, B200 1장 20 FPS, 5분 분포 품질 평가 주장은 **publisher-reported**. '수 시간 collapse 관찰 안 됨'은 저자 관찰이지 시간 전체의 gameplay 정확성 증명 아님. 공개 pretrained checkpoint의 직접 다운로드는 이번 읽은 범위에서 미확인. [라이브 데모](https://mira-wm.com)는 페이지/로딩 화면만 확인, 플레이 안 함. |
| **Solaris**, 2026-02-25 | [arXiv 초록](https://arxiv.org/abs/2602.22208), [공식 JAX 구현 repo](https://github.com/solaris-wm/solaris), [weights model card](https://huggingface.co/nyu-visionx/solaris), [LICENSE 원문](https://github.com/solaris-wm/solaris/blob/main/LICENSE) 확인. final/intermediate model weights와 훈련/추론/VLM-as-judge 평가 경로가 명시되어 있음. 코드 Apache-2.0. | Minecraft 다중 시점 생성, movement/memory/grounding/building/view consistency 비교 연구. README는 GPU inference 최소 48GB, 지원 training은 TPU 최소 95GB/device를 안내(**publisher-reported 요구조건**, 현 프로젝트 Mac에서 가능하다고 주장하지 않음). GPU 훈련을 이미 지원한다고 쓰면 안 됨. weights card BibTeX의 2025 표기는 arXiv의 2026-02-25 게시일과 달라 발표일로 채택하지 않음. |
| **MultiGen**, 2026-03-03 | [arXiv 초록](https://arxiv.org/abs/2603.06679), [Stanford/Google 저자 프로젝트 페이지](https://ryanpo.com/multigen/) 본문 확인. PDF·도해·영상은 공개. 해당 공식 페이지에서 code/weights 배포 링크는 확보 미확인. | Memory(map geometry/pose), Observation, Dynamics 분리; 각 참가자가 같은 external memory를 읽고 갱신. 약 20 FPS, 4인 30분 timelapse 주장은 **publisher-reported**. 'arbitrary number of players'는 인터페이스 확장 주장이지 무제한 동시인원 비용/성능 벤치마크 아님. [멀티플레이 도해](https://ryanpo.com/multigen/static/images/multiplayer_method.png)는 **이미지 직접 확인**; [level-design 영상](https://ryanpo.com/multigen/static/videos/level_design_demo.mp4), [4인 timelapse](https://ryanpo.com/multigen/static/videos/timelapse.mp4)는 공식 링크/설명 확인·미재생. |
| **MASS**, 2026-08-06 | [arXiv 초록](https://arxiv.org/abs/2608.06257), [저자 프로젝트 페이지](https://alaya-lab.github.io/MASS/), [repo](https://github.com/alaya-lab/MASS) 본문·파일 목록 확인. 이 repo는 **project website**(HTML/CSS/JS/assets)이며 학습/추론 구현 공개로 세면 안 됨. 모델 코드·weights·라이선스 공개 확보 미확인. | learned Logic Engine이 authoritative typed state를 갱신하고 camera-conditioned renderer가 view를 생성. 1,024 players/10,000 recurrent ticks는 **publisher-reported**; project page는 execution-scale 실험과 matched Snake accuracy benchmark가 별개라고 명시. 여러 game world도 각자 schema/weights를 사용한다. rail-domain 일반화를 뜻하지 않음. 페이지의 pipeline/gallery/action-fork/long-horizon 그림은 본문·캡션 확인, 이미지 직접 관찰은 안 함. |

**연구 자료로의 우선순위 [INFERENCE]:** 당장 소스/평가 구조를 읽어볼 수 있는 쪽은 **MIRA·Solaris**이고, **MultiGen·MASS**는 이번 확보 수준에서는 외부 메모리·typed shared state의 설계와 시각 증거를 비교하는 자료다. 이를 설치·채택·개발하겠다는 결정은 아니다. 코드가 공개돼도 원작 게임의 에셋/데이터 권리와는 별개이며, 권리 조건을 탐색 필터로 쓰지는 않았다.

## 7. Explicit simulation agents는 generative visual world와 다르다

| 프로젝트 | 실제 역할/접근 상태 | CHOOGuard에서 검토할 층 | 근거·제한·라이선스 |
|---|---|---|---|
| Stanford Generative Agents | LLM memory·planning·reflection을 쓰는 사회적 agent 연구와 별도 Smallville environment. 공개 코드 | 시민/NPC의 일상, 기억, 정보 전달, 대화와 의도 | [repo README](https://github.com/joonspk-research/generative_agents) 본문 확인, 2023 연구. base 25 agents와 3 agents; step=게임 내 10초는 **publisher-reported** 설정이지 realtime 처리속도가 아님. [실제 LICENSE](https://github.com/joonspk-research/generative_agents/blob/main/LICENSE) Apache-2.0, 아트 권리는 별도 |
| Microsoft TinyTroupe | 실험용 Python persona simulation. TinyPerson/TinyWorld, 대화·행동/비즈니스 가상 피드백 | 시민/승객 persona, 안내문 반응, 상황 토론의 오프라인 연구 | [repo](https://github.com/microsoft/TinyTroupe) 본문; README 최신 항목 2026-03-28 release 0.7.0. real population과 비교 검증 필요, 물리/실시간 군중 엔진이 아님. [LICENSE](https://github.com/microsoft/TinyTroupe/blob/main/LICENSE) MIT |
| Unity ML-Agents | Unity 환경을 RL/imitation learning 훈련 환경으로 사용, NPC policy inference | 관측/action이 정의된 협동·회피·수색 등 제한된 역할의 행동 학습 | [공식 repo](https://github.com/Unity-Technologies/ml-agents) 본문; Release 23=2025-08-28, Unity package 4.0.0이라고 표기. 물리/규칙은 Unity environment가 제공. [LICENSE](https://github.com/Unity-Technologies/ml-agents/blob/release/4.0.0/LICENSE.md) Apache-2.0. 프로젝트 버전 호환 미검증 |
| PettingZoo | multi-agent RL 환경의 AEC/Parallel API 및 reference environments. 공개 Python library | 실험 관측·행동·보상 인터페이스, 정책 비교 연구 | [repo](https://github.com/Farama-Foundation/PettingZoo) 본문, 최신 발행일 미확인. 세계 생성 모델/렌더러/네트워크 서버가 아님. [LICENSE](https://github.com/Farama-Foundation/PettingZoo/blob/master/LICENSE) MIT |

**의미상 구분:**
- '시민이 말하고 목표를 고른다' → LLM/persona/planner agent.
- '시민이 실제 위치에서 문/통로/장비와 상호작용한다' → explicit simulation, navigation, action execution.
- '각 행동 뒤의 픽셀을 모델이 만든다' → generative visual world/renderer.
- '여러 사람이 같은 실제 상태를 본다' → shared state + 권한/네트워크/시간 처리.

이 네 가지는 함께 쓸 수 있지만 어느 하나를 설치했다고 나머지가 자동 해결되지는 않는다. 특히 believable behavior는 실제 재난 군중 통계나 공인 안전 절차의 정확성을 뜻하지 않는다.

## 8. 'single shared state'와 'Unity server authoritative'의 차이

다음은 위 자료에 근거한 **[INFERENCE] 개념 비교**이며 현 프로젝트 구조를 감사하거나 새 아키텍처를 확정한 것이 아니다.

| 관점 | Agora-2의 model-serving world server | Unity authoritative simulation을 설계할 때의 의미 |
|---|---|---|
| 단일 상태의 목적 | 모델/각 view가 같은 entity identity·위치·interaction outcome을 참조 | 승인된 action만 실제 game state를 바꾸고 모든 client가 그 결과를 공유 |
| 다음 상태 결정 | learned transition prediction + 고정 decoding + session/native scenario rule | 명시적 gameplay rule·physics·작업 권한/선행조건을 개발자가 정의하고 검증할 수 있음 |
| 'authoritative'가 보장하지 않는 것 | 공유한 추정 결과 자체가 잘못될 수 있음 | 서버가 하나라고 철도 지식이 저절로 정확하거나 Unity 물리가 공학 검증된 것도 아님 |
| 화면과 의미 | 상태는 같아도 생성된 픽셀은 다를 수 있음 | 명시적 object/state로 렌더링하더라도 network interpolation·LOD·UI 차이는 별도 관리 |
| 검증·재현 | 모델 version, sampling, cache/reset, runtime state가 영향 | 규칙 version, seed, event 기록 등 명시적 증거를 설계할 여지가 큼. 자동 결정성 보장은 별도 문제 |
| 외부 연동 | 논문 internal state가 공개 programmable API라는 보장 없음 | 프로젝트가 소유한 의미 state와 외부 AI 입출력의 경계를 정의 가능 |

**핵심:** authoritative는 **누가 최종 상태를 확정하는가**이고, validation은 **그 상태전이가 요구한 규칙/현실 모델에 맞는가**다. 둘을 구분해야 한다. CHOOGuard에서는 전원을 끊지 않은 상태에서 특정 작업이 완료됐다고 말하는 NPC나, 화면상 도구를 쓴 것처럼 보이지만 의미상 작업이 안 된 상황이 정답 판정에 들어오면 안 된다.

## 9. CHOOGuard용 실용 대안 비교 — 구현 승인 전 선택 자료

| 대안 | 장점 | 실제 부담/위험 | 현재 연구상 위치 |
|---|---|---|---|
| A. Unity explicit world + 기존 AI(FSM/BT/utility 등) | 정비 상태·도구·작업 순서·대피/안내 결과를 추적하기 쉬움; 네트워크와 입력 지연을 통제할 여지 | 규칙·콘텐츠·의미 상태를 사람이 만들고 검증해야 함 | 교육/협동 대응 본체 후보 |
| B. A + planner/director + sparse LLM NPC | 사건 조합, 비정형 대화, 시민의 목표/오해/요청을 풍부하게 만들 수 있음 | LLM은 제안자여야 하며 무제한 state edit나 안전 정답 판정 권한을 주면 위험. 비용·지연·오류 처리 필요 | 사용자가 말하는 '살아 있는 AI 세계'가 이 뜻이면 검토 우선 |
| C. A/B + ML-Agents/PettingZoo 연구 환경 | 명시된 관측/행동과 평가 아래 협동 정책을 비교·학습 | reward hacking, 분포 이동, 데이터/평가 환경 제작; LLM 없이도 가능 | 특정 행동 연구가 실제로 필요한 경우 |
| D. A/B + Marble 등 offline world-generation | 빠른 공간 분위기 탐색/배경 후보 생성, export 가능 | 치수/충돌/접근성/철도 의미 재구성; Unity/URP plugin 호환 검증 | 모델링 대체 확정이 아닌 시각/제작 보조 후보 |
| E. Agora/Genie/Cosmos 별도 생성-world sandbox | 인간/agent/생성 세계의 가능성과 한계를 직접 연구 | API 접근, GPU, 지연, 장기/다중 시점 일관성, 도메인 전이 미검증 | 본게임 정답 엔진과 격리한 탐색 후보 |
| F. 생성 영상 세계가 본게임 전체 | 시각적 자유와 연구 신선도 | 정비 정답·절차·physics·세션 재현/안정성·외부 프로그래밍 표면을 현재 자료로 보증 못함 | 현재 확보 근거만으로 CHOOGuard 본체 권고 불가 |

**검토할 수 있는 층별 분리 [INFERENCE/권고]:**

1. **Validated world state:** 설비/도구/작업 단계/위험/권한/주체별 관측을 명시적으로 보유. 정답 근거는 검토된 자료와 버전 규칙에서 온다.
2. **Planner/director:** 허용된 사건 후보와 목표를 제안한다. '무작위'는 인과·선행조건·난이도·가용 자원을 무시하는 임의 변경이 아니다.
3. **Sparse LLM NPC:** 선택된 시민/역무원/협력자의 대화·기억·고수준 의도에 사용하고, 프레임마다 모든 군중을 LLM으로 돌리는 것과 구분한다.
4. **Action validator/executor:** 인간과 AI 모두 같은 허용 action·선행조건·권한을 거친다. 대화상 '했다'와 실제 완료 state를 분리한다.
5. **Optional world-generation sandbox:** 이미지/영상/3D world/model rollout 비교 실험. 생성 결과가 본체의 정비 정답이나 실제 위험 수치를 덮어쓰지 않는다.

이는 상세 개발 계획·패키지 선정·서버 topology 확정이 아니다. 현재 Unity 버전, 기존 FPS responder와 TutorialSession의 존재는 제공받은 맥락이며 코드를 변경하거나 실행하지 않았다. 공격적 사건은 오직 방어·대피·통보·권한 인계 관점에서만 취급한다.

## 10. 이미지·영상·도해 자료 묶음

상용/권리 조건 때문에 후보를 제외하지 않았다. 아래는 **관찰할 수 있는 것**과 **실제로 확인한 수준**을 분리했다. 생성 영상은 실제 철도 현장 사진이나 공학 측정자료가 아니다.

| 자료 | 직접 링크/획득 경로 | 관찰 포인트 | 실제 확인 수준·날짜 |
|---|---|---|---|
| Agora-2 공식 상태전이 도해 | [JPEG](https://framerusercontent.com/images/HJcMhUvIJqcnLBnlhPeieC39tmI.jpeg?width=2047&height=784) · [출처 페이지](https://odyssey.systems/introducing-agora-2) | motion/action history, local geometry, relative entity state → transformer → movement/interactions → world server/session rules → shared state | **이미지 직접 열어 확인**. 공식 2026-09-21 발표 연결 |
| Agora-2 공식 rendering 도해 | [JPEG](https://framerusercontent.com/images/Bk9JC4kLsaxPoSt8DvOaSwjLdE.jpeg?width=2027&height=952) | structured scene tokens, spatial attention, causal temporal attention, previous latent/history, decoder 2-frame 출력 | **이미지 직접 열어 확인**. 'state와 view history는 별개'를 설명할 때 유용 |
| Agora-2 인간 2인+agent 데모 | [공식 MP4](https://framerusercontent.com/assets/ZfcIM6TLKcPbbzfURMa87Lo6ww.mp4) | 좌우 두 화면의 등각 시점, 같은 환경/사건을 서로 다른 camera로 묘사하는 예 | **MP4 접근 및 브라우저에서 4초 프레임 직접 확인**. 메타데이터 20초/1280×578 확인; 전체 재생·프레임별 일관성/지연 측정은 안 함. 영상 크기는 모델의 per-view 해상도가 아님 |
| Agora-2 policy 영상 | [공식 MP4](https://framerusercontent.com/assets/z3gExHAXOB0QV5qxElfWVOSR80.mp4) · [본문](https://odyssey.systems/introducing-agora-2) | 공식 설명상 추적, 장애물 회피, stuck/separated recovery | 공식 페이지의 video element URL과 설명 확인, **미재생** |
| Agora-2 playable entrance | [웹 데모](https://agora.odyssey.systems/) | 이름 입력, Amazon/Necromancer/Barbarian/Paladin/Sorceress 선택, UI와 연구 프리뷰의 실제 접근성 | **시작→캐릭터 선택 화면 직접 확인/스크린샷 관찰**. 이름 제출·전투 진입은 안 함 |
| Agora technical report Figure 1–4, 8 | [PDF](https://agora-2.odyssey.systems/agora-2.pdf) | 전체 아키텍처, simulation branch, 2-view boss encounter, renderer, 시간 흐름 | **PDF 원문 추출 및 캡션 확인**. Figure 3은 저자도 qualitative, cross-view metric 아님을 명시 |
| Genie 3 공개 소개 영상 | [공식 YouTube embed](https://www.youtube.com/embed/PDKhUknuQDg) · [공식 원문](https://deepmind.google/blog/genie-3-a-new-frontier-for-world-models/) | prompt별 다양한 생성환경/탐색, agent 연구 사례 | 공식 본문·영상 링크/설명 확인, **미재생** |
| Marble → Unity 영상 | [MP4](https://mintcdn.com/worldlabs/MLFIZQFuNhLp0_rH/media/marble-export-unity.mp4?fit=max&auto=format&n=MLFIZQFuNhLp0_rH&q=85&s=2bdc9ac35a56a7e3b6bc929c8af3ad17) · [공식 Unity 안내](https://docs.worldlabs.ai/marble/export/gaussian-splat/unity) | 생성 3D splat의 Unity 표시 워크플로 | 공식 문서 및 video source 확인, **미재생**. 정확한 현재 프로젝트 호환 증거 아님 |
| Marble mesh export 설명 영상 | [YouTube](https://www.youtube.com/watch?v=I7rYwrgXRmg) · [mesh 문서](https://docs.worldlabs.ai/marble/export/mesh) | collider와 HQ mesh export의 차이, 게임엔진용 자산 획득 | 공식 embed/본문 확인, **미재생** |
| WHAM 원본 입력 vs 학습 단계별 영상 | [원본](https://www.microsoft.com/en-us/research/wp-content/uploads/2025/01/Media1.mp4), [10k](https://www.microsoft.com/en-us/research/wp-content/uploads/2025/01/Media2.mp4), [100k](https://www.microsoft.com/en-us/research/wp-content/uploads/2025/01/Media3.mp4), [1M](https://www.microsoft.com/en-us/research/wp-content/uploads/2025/01/Media4.mp4) | 원본 게임과 생성 영상의 동역학 차이; '잘 보임'과 드문 mechanic 재현의 차이 | [2025-02-19 공식 설명/비교표](https://www.microsoft.com/en-us/research/blog/introducing-muse-our-first-generative-ai-model-designed-for-gameplay-ideation/) 확인, **영상 미재생** |
| WHAM Demonstrator tutorial | [공식 영상 페이지](https://www.microsoft.com/en-us/research/video/wham-demonstrator-tutorial/) · [직접 데모 MP4](https://www.microsoft.com/en-us/research/wp-content/uploads/2025/01/Video-Demo-3.mp4) | 시작 이미지, 여러 continuation, controller로 탐색하는 ideation 도구 | 발표 본문에서 연결·설명 확인, tutorial 별도 본문/재생은 미확인 |
| WHAM-RT Quake II | [공식 MP4 1](https://www.microsoft.com/en-us/research/wp-content/uploads/2026/05/vid1_square.mp4), [2](https://www.microsoft.com/en-us/research/wp-content/uploads/2026/05/vid2_square.mp4), [3](https://www.microsoft.com/en-us/research/wp-content/uploads/2026/05/vid3_square.mp4) | 생성 FPS 탐색/조작 화면과 전통 FPS가 다른 부분 비교 | [공식 WHAM-RT 소개](https://www.microsoft.com/en-us/research/project/wham/wham-rt/) 본문/영상 URL 확인, **미재생** |
| Cosmos action-conditioned 정비 시점 GIF | [공식 repo GIF](https://github.com/NVIDIA/cosmos/blob/main/assets/demos/fd_egocentric_repair_poses.gif) | input camera+hand pose로 egocentric forward dynamics를 생성하는 연구 예 | [README](https://github.com/NVIDIA/cosmos)의 이미지/캡션 확인, **GIF 프레임 직접 관찰 안 함**. 실제 정비 절차 정답은 아님 |
| Cosmos 로봇 도구 작업 GIF | [공식 GIF](https://github.com/NVIDIA/cosmos/blob/main/assets/demos/policy_screwdriver.gif) | 도구와 장갑을 용기에 넣는 policy 예, 도구 affordance 연구 아이디어 | 공식 README 캡션 확인, **GIF 직접 관찰 안 함** |
| Cosmos 산업/로봇·안전 장면 | [공식 발표](https://blogs.nvidia.com/blog/cosmos-3-physical-ai-open-world-foundation-model/) · [소개 영상](https://www.youtube.com/watch?v=1QPh70Es_oU) | perception/prediction/action 도해, 창고·로봇·교통 synthetic scenario | 본문/그림 캡션/영상 링크 확인, **영상 미재생** |
| Generative Agents Smallville | [커버 이미지](https://github.com/joonspk-research/generative_agents/blob/main/cover.png) · [repo](https://github.com/joonspk-research/generative_agents) | 별도 명시적 지도 안에서 움직이는 사회적 agent라는 구조 | README 이미지 경로/리플레이 설명 확인, **이미지 직접 관찰 안 함** |

권리 메타데이터 공통: 공식 홍보물/논문 도해/게임 footage의 열람과 자산 재사용 권한은 별개다. 지금은 참고 후보를 넓게 모으는 단계이며 라이선스로 선별하지 않았다. 제공자의 모델 또는 코드 공개가 underlying game/사진/음악까지 포괄해 허락한다는 뜻은 아니다.

## 11. 사용자에게 물을 'AI world'의 의미 선택지

부모 세션이 다음 중 무엇을 가장 원하는지 확인하면 이후 조사의 우선순위가 명확해진다. 이 보고서는 선택을 대신 확정하지 않는다.

1. **AI 주민형:** Unity 철도 공간은 고정/절차 검증, 시민·역무원·협력자가 대화하고 기억하며 목표를 선택한다.
2. **AI 감독형:** 정비 업무와 비상상황의 조합·순서·난이도를 AI가 제안하되 허용 규칙 안에서 실행한다.
3. **AI 동료/학습형:** 인간과 AI가 같은 조작/도구 action으로 협업하고 정책을 학습·평가한다.
4. **AI 제작형:** 사진·프롬프트에서 공간/영상/배경 자산을 만들어 모델링을 보조한다.
5. **AI 생성세계형:** Agora처럼 state transition과 시각 세계 자체를 모델이 생성하는 별도 연구가 핵심이다.
6. **혼합형:** 본체는 1–3, 제작에는 4, 별도 sandbox로 5를 연구한다.

함께 확인할 것은 인간 동시 플레이어 수/AI 시민 수, 온라인 필수 여부, 현장 정비의 '정답' 검증 수준, FPS에서 빌릴 것이 조작감인지 전투인지, 외부 GPU/API 허용 여부다. **Agora의 최대 20 entities와 본게임의 원하는 20명 인간은 다른 요구**다.

## 12. 미확인 항목·검증 범위·조사 방법

### 미확인 항목을 '없음'이라고 쓰지 않은 것

- Agora-2 public SDK/API, 공개 weights/train code, 상용 단가/SLA, 영구 save/restore, session 최대시간, Unity connector, custom object/physics/schema API.
- Agora-2의 실제 multi-user sustained FPS·input latency·장기 causal correctness·cross-view 정량 benchmark.
- 한국에서의 Project Genie 실계정 접근, Microsoft 데모 로그인 이후 실행, Marble/Cosmos 모델의 로컬 실행 및 현재 Unity/URP 호환.
- 공개 철도 도메인에서 위 생성 세계 모델이 정비 절차를 검증된 수준으로 수행한다는 자료.

### 실제 한 일 / 하지 않은 일

- 공식 발표·기술 PDF·공식 API docs·실제 LICENSE·공식 repo/모델 catalog를 읽었다. 검색 snippet은 발견용으로만 사용했다.
- Agora 데모 시작/캐릭터 선택 화면, Odyssey developer 접근 화면을 실제 브라우저로 확인했다. 공식 Agora 도해 2개를 이미지로 열고, 공식 MP4의 4초 표본 프레임을 직접 보았다. 모든 조사 탭은 닫았다.
- 여러 후보 영상의 실제 URL과 공식 설명은 확보했지만 전체 재생이나 프레임 단위 벤치마크를 하지 않았다. 보고서 표에 이 차이를 명시했다.
- API 키 생성·회원가입·결제·훈련·설치·클론·CHOOGuard 실행/빌드/테스트는 하지 않았다. 기존 Assets/Packages/ProjectSettings/기존 문서는 수정하지 않았다.
- 이 보고서와 요청된 새 조사 폴더의 동일 보고서만 생성한다. 개발 roadmap, active plan, 모델링 작업은 건드리지 않는다.

### 읽는 순서 권고

1. Agora-2 PDF **§6, §8**: 실제 서버 책임/제한을 먼저 읽는다.
2. 발표문/영상/도해로 공유 상태와 여러 시점의 연구 의도를 파악한다.
3. Odyssey API **FAQ Broadcast**를 읽어 데모와 상용 API 표면을 혼동하지 않는다.
4. 실제 자산 제작이 목적이면 Marble export/API, 시민 행동이 목적이면 explicit-agent 자료로 갈라진다.
5. 이후에만 'CHOOGuard에서 AI world가 무슨 뜻인가'를 결정한다.

## 13. 추가 확인: 실제 공개된 다중 시점 월드 모델 코드

부모 세션이 Agora-2 기술보고서 참고문헌에서 이어서 확인한 자료다. 일반 persona agent가 아니라 multiplayer visual world 자체의 공개 구현 후보를 별도로 남긴다. 설치·weights 다운로드·실행은 하지 않았다.

| 후보 | 직접 확인한 공개물 | 구체적 재사용/연구 지점 | 한계 |
|---|---|---|---|
| [MIRA](https://github.com/mira-wm/mira) · [프로젝트](https://mira-wm.com/) | 공식 코드 저장소와 README, [train_world_model.py](https://github.com/mira-wm/mira/blob/main/scripts/train_world_model.py) 도입부/실제 코드. Rocket League 4인 2v2·5B·single GPU 20 FPS는 저자 주장 | `RocketScienceDataset`의 동기화된 4-view frames/actions/events/physics, `scripts/train_codec.py`, `scripts/train_world_model.py`, `scripts/eval_world_model_offline.py`; `dataset.n_players=4` 학습 구성 | README 설치 경로는 NVIDIA GPU를 요구하고 codec 학습용 DINOv3 weights는 별도 접근승인 대상. 일반 철도/Unity connector가 아님. 최종 world-model weights 공개 확보는 이번 열람으로 확정하지 않음 |
| [Solaris](https://github.com/solaris-wm/solaris) · [공식 weights](https://huggingface.co/nyu-visionx/solaris) | JAX 구현, GPU inference/TPU training 안내, pretrained/final/intermediate weights 모델카드, [multiplayer/world_model.py](https://github.com/solaris-wm/solaris/blob/main/src/models/multiplayer/world_model.py) 코드 도입부 | `MPSelfAttention`, `src/inference.py`, `vlm_eval/`, multiplayer Duet 데이터 및 평가 데이터 획득 경로 | README상 GPU inference는 장치당 최소 48GB, TPU training은 장치당 최소 95GB 메모리. 저자 실행 요구사항이며 이번 환경 실측 아님. Minecraft 도메인 결과를 철도 정비 정답으로 전이한 증거 없음 |
| [MASS](https://alaya-lab.github.io/MASS/) · [논문](https://arxiv.org/abs/2608.06257) | 공식 프로젝트 본문·도해 설명. typed authoritative state와 camera-conditioned renderer 분리 | 정답 state와 화면을 분리하는 연구 설계, action fork·entity conservation 평가 아이디어 | 공식 페이지의 1,024 simulated players/10,000 ticks는 저자 연구 주장. 단순 게임 세계 실험이며 1,024명의 실제 FPS 사용자 또는 렌더 스트림 성능이 아님. 공개 코드/weights 경로는 이 페이지에서 확보하지 못함 |

권리 정보는 검색 필터가 아니다. MIRA README와 Solaris 저장소/모델카드는 Apache-2.0을 표기한다. 코드·학습 데이터·기반 게임·제3자 weights는 별도 항목이다. Solaris 모델카드의 BibTeX에는 2025, 연결 arXiv ID에는 2602가 표시되어 서지 작성 시 원 논문 날짜를 확인해야 한다.

**[INFERENCE] 자료 활용:** 실제 학습 시스템을 해부하려면 closed Agora 프리뷰만 보는 것보다 MIRA/Solaris의 데이터 정렬·다중 시점 conditioning·평가 코드를 함께 보는 편이 유용하다. 이것도 바로 가져다 붙일 Unity FPS 스니펫과는 다른 규모의 연구 코드다.
