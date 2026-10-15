# 연구 적용 범위와 현재 근거

2026-10-13 현재 원문·공식 API를 확인했다. 연구 엔진을 Unity에 설치했다는 뜻은 아니다.

| 근거 | 이 구현에 적용하는 원리 | 검증 경계 |
|---|---|---|
| [SimWorld, NeurIPS 2025](https://proceedings.nips.cc/paper_files/paper/2025/hash/f1eff44d59d9c66cc6e359d36d80b0b9-Abstract-Conference.html) | 세계 상태·목표·행동·실행 피드백 분리 | 원 연구는 Unreal. 본 Unity 어댑터는 별도 구현 |
| [Virtual Community, ICLR 2026](https://virtual-community-ai.github.io/) | 공간에 연결한 개인 관측·기억·목표와 사회 관계 | Genesis 연구이며 본 게임의 군중·비상대응 타당성을 입증하지 않음 |
| [AgenticCache, MLSys 2026](https://proceedings.mlsys.org/paper_files/paper/2026/hash/c66a9db149261435664284a20b6f1d42-Abstract-Conference.html), [공식 코드](https://github.com/hojoonleokim/MLSys26_AgenticCache) | 의존 조건이 유효한 계획 전이 재사용, 비동기 갱신 | 연구 패키지를 이식한 것이 아니라 원리를 구현. 연구 절감률을 본 프로젝트에 적용하지 않음 |
| [TypeSafe API](https://docs.typesafe.ai/api) | 실제 객체와 실행 가능한 조작에 대한 Choice·Score 평가 | JEV는 제공한 선택지를 평가하며 새로운 실행 의미·물리 법칙을 만들지 않음 |
| [Ollama 호환 API](https://docs.ollama.com/api/openai-compatibility) | 로컬 OpenAI 호환 생성기 연결 경로 | 로컬 모델 서버·가중치 설치와 실제 모델 응답의 품질 검증은 별도 조건 |
| [Unity NavMeshBuilder](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AI.NavMeshBuilder.BuildNavMeshData.html), [완전 경로 판정](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/ai/navmeshpathstatus) | 격리된 승강장 경로 데이터, PathComplete 검사 | 기본 NavMesh 회피를 ORCA라고 부르지 않음. 경로·충돌·문 통과는 실행 검증 필요 |

제안서는 `output/proposals/2026-10-13-vr-ai-research/`의 현재 파일을 기준으로 대조했다. 상세 현재 소스 근거는 `.planning/2026-10-13-platform-seeded-simulator/runtime-audit.md`, 자원·좌표·출처는 `asset-audit.md`에 있다.

## 인과 시뮬레이션의 의미

고정된 완성 사건 목록을 추첨하지 않는다. 등록한 물체의 재료·전원·열·수분·구조 상태, 공간 근접·연결 관계, NPC 상태와 관측, 외부 연속장의 변화가 원자 전이의 조건이 된다. 원자 조작과 물리적 의미는 구현된 범위로 제한하며 모델은 지원 조작을 조합한다. 무한한 실제 현상이나 모든 미등록 객체의 인과 의미를 자동으로 알아낸다고 주장하지 않는다.

전시 범위는 민방위 경보·대피/보호·통신/전원 장애 등 훈련용 추상 상태다. 자연재해·건강·고장·군중 등과 함께 객체 상태로 연결한다. 확률과 강도는 게임 파라미터이며 코레일 실측 발생률이나 현장 절차의 정확성을 뜻하지 않는다.

## AS-IS 근거의 경계

사용자는 코레일의 사전 각본형 비상대응훈련을 AS-IS로 지정했다. [코레일 공식 2018 안전한국훈련 보도자료](https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=6942)는 지진으로 고속열차가 탈선하고 화재·인명 피해가 발생한 가정을 두고 유관기관이 대응한 사례를 보여 준다. 이 사례는 정해진 가정에 따른 훈련의 존재를 뒷받침하지만 현재 모든 코레일 훈련이 대본에 불과하다거나 효과가 없다는 결론을 입증하지 않는다. TO-BE는 알려진 단일 전개를 재연하는 범위를 넘어 객체 상태·관계와 참가자 개입에 따라 전개가 달라지는 반복 훈련 환경이다. 훈련 효과의 우월성은 향후 평가할 가설로 남긴다.
