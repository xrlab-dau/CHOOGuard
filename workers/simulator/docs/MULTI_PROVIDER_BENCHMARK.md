# Provider 로그인·모델 연결 벤치마크

조사 기준: 2026-10-14. 코딩 하네스의 인증·모델 연결 구조를 시뮬레이터의 모델 계층에 적용한다. 코딩용 파일·셸·MCP 도구를 NPC에 넘기지 않는다. 이 문서는 구현 방향과 근거이며 모든 provider의 실제 구독 추론 성공을 뜻하지 않는다.

## 현재 원본과 비교

| 하네스 | 확인한 원본 commit | 채택할 구조 | 그대로 가져오지 않을 동작 |
|---|---|---|---|
| OMP | [can1357/oh-my-pi, b91db66e](https://github.com/can1357/oh-my-pi/tree/b91db66e9b0b65a2c977e6a5bc74b0f4b1d58d1c) | provider별 OAuth adapter, 계정·조직 identity, 모델 registry, 계정별 discovery cache, refresh 직렬화 | 다른 계정으로 quota rotation, API 키·환경변수 fallback, 코딩 도구 실행 |
| Prime Agent | [PrimeIntellect-ai/prime-agent, 33da15a2](https://github.com/PrimeIntellect-ai/prime-agent/tree/33da15a2c15c4e903c372f69ae8e1156d77d53da) | Rust provider transport, scripted HTTP 검증, 검증된 catalog snapshot과 transport pinning, 갱신해도 활성 모델 유지 | 오래된 TS 문서를 현행 구현으로 인용, catalog에 있다고 계정 권한을 가정, provider failover |
| pi | [earendil-works/pi, 3daa2028](https://github.com/earendil-works/pi/tree/3daa20280f6a7c7cbe0935680c9a38fdc3863005) | provider/auth 분리, login/refresh/toAuth, 비밀 없는 계정 상태 조회, 잠금 안에서 refresh, 모델별 transport·capability | provider당 단일 credential 저장 제한, ambient credential 자동 탐색 |

`badlogic/pi-mono`는 현재 `earendil-works/pi`로 이동했다. OMP 검색 결과의 다른 조직 mirror와 Prime Agent의 예전 `packages/coding-agent` TS 문서는 현행 원본 증거로 사용하지 않았다.

OMP의 [provider 문서](https://github.com/can1357/oh-my-pi/blob/b91db66e9b0b65a2c977e6a5bc74b0f4b1d58d1c/docs/providers.md)는 bundled/custom/discovered/extension 모델을 합성한다. 인증된 provider라는 사실과 개별 모델의 현재 account entitlement는 다르다. 계정·조직·지역별 cache, 권위 있는 discovery, 오래된 cache 표시를 채택한다. [OAuth 계약](https://github.com/can1357/oh-my-pi/blob/b91db66e9b0b65a2c977e6a5bc74b0f4b1d58d1c/packages/ai/src/registry/oauth/types.ts)은 로그인 provider ID와 credential 저장 provider ID도 구분한다.

Prime Agent의 [OAuth Rust 모듈](https://github.com/PrimeIntellect-ai/prime-agent/blob/33da15a2c15c4e903c372f69ae8e1156d77d53da/crates/pa-ai/src/oauth/mod.rs)은 Codex·Anthropic·Copilot·xAI 흐름을 transport 인터페이스로 분리한다. [catalog 계층](https://github.com/PrimeIntellect-ai/prime-agent/blob/33da15a2c15c4e903c372f69ae8e1156d77d53da/crates/pa-models/README.md)은 schema/크기/시간/redirect를 제한하고 compiled transport에 endpoint를 고정한다. [picker](https://github.com/PrimeIntellect-ai/prime-agent/blob/33da15a2c15c4e903c372f69ae8e1156d77d53da/crates/pa-daemon/src/model_catalog.rs)는 모델 목록과 사용 가능 집합을 구분한다. Prime Inference의 별도 API 인증을 소비자 구독 인증이라고 부르지 않는다.

pi의 [auth 계약](https://github.com/earendil-works/pi/blob/3daa20280f6a7c7cbe0935680c9a38fdc3863005/packages/ai/src/auth/types.ts)은 credential storage와 모델 요청 auth를 분리하고 refresh를 직렬화한다. [ChatGPT OAuth](https://github.com/earendil-works/pi/blob/3daa20280f6a7c7cbe0935680c9a38fdc3863005/packages/ai/src/auth/oauth/openai-chatgpt.ts)는 PKCE, callback state, 동적으로 발급된 client identity, direct-token scope, refresh를 다룬다. [registry](https://github.com/earendil-works/pi/blob/3daa20280f6a7c7cbe0935680c9a38fdc3863005/packages/coding-agent/src/core/model-registry.ts)는 provider별 요청 시점 인증과 streaming을 감싼다. 우리 앱에서는 계정 복수 저장으로 확장한다.

## 최종사용자 흐름과 불변식

1. provider 목록에서 하나를 선택하고 본 앱의 브라우저 로그인으로 인증한다. 여러 provider와 여러 계정을 독립적으로 유지한다.
2. 모든 로그인 계정의 **권한이 확인된 모델**을 한 목록에 합친다. 행은 `(provider_id, account_id, model_id)`로 식별하고 provider·계정 별칭을 표시한다. 같은 이름의 모델을 합쳐버리지 않는다.
3. 플레이 시작 전에 딱 하나를 선택한다. 초기 인과조건 생성과 NPC 계획은 같은 selection을 사용한다.
4. 플레이 세션의 선택은 고정한다. 로그아웃·권한 만료·모델 선택 변경·계정 변경은 세션을 무효화하거나 명시적 새 세션을 요구한다. 대기 중인 이전 응답은 적용하지 않는다.
5. quota·정책·연결 실패를 이유와 함께 보여준다. API 키 과금이나 다른 계정/provider/model로 자동 전환하지 않는다.

공용 catalog는 후보 메타데이터이며 계정 권한 증거가 아니다. 전체 목록 API가 없는 provider는 검증할 수 있는 범위와 누락 가능성을 표시한다. 접근 불가 모델은 비활성 이유를 보여준다. `auto`·router 모델은 사용자가 하나의 구체 모델을 선택한다는 요구를 만족하는지 별도 판단하며, 조용히 선택하지 않는다.

## 구현 계약

- `ProviderAdapter`: 비밀 없는 provider 설명, 지원 로그인 방법, 로그인 시작/완료, account refresh/logout, account-scoped model discovery, streaming/complete, usage/error 정규화.
- `ProviderAccount`: provider와 계정·조직·grant/client identity, 비공개 credential 참조, 인증/권한 상태. 브라우저·Unity·tape에는 token 원문을 보내지 않는다.
- `AccountModel`: provider/account/model identity, context/output/structured JSON/tool capability, entitlement 근거와 갱신 시각, 사용 불가 이유. endpoint/header는 provider 구현이 정하고 원격 catalog가 임의로 바꾸지 못한다.
- `SessionSelection`: provider/account/model + 선택 generation + 인증 세대 + catalog/권한 fingerprint. 초기 생성과 NPC가 동일 값을 검증한다. refresh는 같은 grant/account를 유지해야 한다.
- `InferenceResult`: provider/account/model, 용도, request ID, bounded usage, 종료/오류 상태, 검증된 JSON 계획. 비밀 없는 provenance를 기록한다.
- 캐시는 seed, world/source digest, observation, 선택 identity, model/contract 버전으로 격리한다. 다른 모델의 결과를 같은 선택의 실추론이라고 기록하지 않는다.

입력은 전체 월드의 오브젝트·NPC·관계에서 구성한다. 모델은 상위 인과조건과 NPC의 제한된 행동 계획을 제안하고, local seeded causal kernel·접근성·충돌·도구 상태가 실행을 결정한다. 모델 호출은 매 프레임/키 입력마다 하지 않는다. JSON/schema/참조/권한 검증에 실패한 결과는 원자적으로 거절하고, 응답이 없는 상태도 재생 가능한 실패로 기록한다. 초기 생성의 빈 변경안은 유효하며 임의 사고를 강제로 넣거나 자동 재호출하지 않는다.

## Provider별 지원 판단

| provider 경로 | 확인한 근거와 현재 의미 | 프로젝트 처리 |
|---|---|---|
| OpenAI ChatGPT direct OAuth | 본 앱에서 end-user OAuth와 구독 모델 초기 추론 1회가 실제 검증됨. pi도 direct scope와 발급 client identity를 구현함 | 기존 연결 보존. 다중 계정 목록과 session binding으로 확장. 이 한 번이 NPC 실추론/새 전체 월드 완료 증거는 아님 |
| GitHub Copilot | [공식 SDK 인증](https://docs.github.com/en/copilot/how-tos/copilot-sdk/auth/authenticate)과 [자체 OAuth App 설정](https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/github-oauth)이 사용자 구독으로 앱 요청하는 경로를 설명함 | 본 앱 소유 OAuth App/GitHub App 등록과 실제 계정 검증 필요. client ID는 공개 앱 식별자이며 LLM API 키가 아님. 다른 하네스 앱 ID 차용 금지 |
| Anthropic 소비자 구독 직접 OAuth | [공식 credential 정책](https://code.claude.com/docs/en/legal-and-compliance)은 제3자 앱의 Claude.ai 로그인·구독 credential 라우팅/중개를 허용하지 않음. 원본 Claude Code binary에 사용자가 직접 로그인하는 별도 예외가 있음 | 직접 adapter는 정책상 비활성 사유 표시. 원본 binary 위임 예외를 자체 OAuth 허가로 해석하지 않음. API 키 fallback 없음 |
| Google Gemini | [공식 OAuth API 문서](https://ai.google.dev/gemini-api/docs/oauth)는 Cloud project 기반 API 인증을 설명함 | OAuth 성공을 Google AI 소비자 구독 quota 이용 가능으로 해석하지 않음. 별도 공식 경로·본 앱 권한·entitlement 검증 전 미지원 |
| xAI / Grok | 하네스 OAuth 구현은 존재하지만 [공식 usage 문서](https://docs.x.ai/x-subscription/usage-limits)는 포함 pool을 지정된 first-party surfaces와 연결함 | 본 앱의 공개 OAuth client/구독 inference 권한 확인 전 미지원/조건 표시. 하네스 구현 존재만으로 지원 확정하지 않음 |
| 기타 provider / API-key-only gateway | 로그인·entitlement·subscription inference의 공식 계약을 provider별로 확인해야 함 | registry에 확장 지점과 구체적 미지원 이유를 남김. 모든 모델을 무조건 사용 가능으로 표시하거나 개발자 키를 삽입하지 않음 |

위 판단은 2026-10-14 문서 시점이다. 정책·플랜·catalog가 바뀌면 근거 날짜와 adapter 지원 상태를 함께 갱신한다.

## Copilot 적용 시 도구 차단

공식 SDK [commit 810c483a](https://github.com/github/copilot-sdk/tree/810c483ae426344c2a5fee61025f9e3bd457cca9)에서 [Python client](https://github.com/github/copilot-sdk/blob/810c483ae426344c2a5fee61025f9e3bd457cca9/python/copilot/client.py)는 `github_token`, `use_logged_in_user=False`, `list_models()`, `create_session()`를 제공한다. [Python 사용법](https://github.com/github/copilot-sdk/blob/810c483ae426344c2a5fee61025f9e3bd457cca9/python/README.md)의 `mode="empty"`와 빈 `available_tools`를 검토하고, permission handler도 거절하도록 설정한다. `tools=[]`만 지정하면 builtin 도구까지 없어졌다고 가정하지 않는다.

세션은 private 별도 실행/config 디렉터리, 정제된 환경, explicit account token을 사용한다. 기존 Copilot/gh 로그인·MCP·skills·repository instructions·파일·셸·network tool을 자동 상속하지 않는다. SDK 내부 자동 compaction/retry가 별도 모델 호출·선택 변경을 유발하는지 검증한다. timeout은 임의 재시도하지 않고 세션을 종료한다. 모델 catalog 조회와 실패 fixture는 실제 사용자 추론 quota 소모 없이 검증한다.

## 검증과 증거 범위

- 실제로 완료된 기존 검증: ChatGPT direct OAuth, `gpt-6.1-sol` 초기 요청 1회(입력 36,145 / 출력 53), 빈 변경안 수락, 40 tick 오프라인 재생.
- 기존 NPC 코드와 synthetic localhost 추론 시험은 있다. 실제 최종사용자 구독 NPC 추론 → Unity NavMesh 이동 연결은 아직 확인하지 않았다.
- 이번 다중 provider UI/catalog/session 계약 작업은 worker가 구현한다. 실제 provider 추가 로그인이나 추론 없이 fixture/단위 검증으로 구분한다.
- Unity는 사용자 요청으로 보류했다. 이번 작업은 Unity 실행·원본 장면 변경·추가 실제 모델 호출을 하지 않는다.

원본 파일 commit·URL·SHA256 증거는 `.planning/2026-10-14-multi-provider-issue/source-receipt.json`과 `copilot-source-receipt.json`에 보존했다. 계획 디렉터리의 원본 복사본은 연구 증거이며 실행 dependency가 아니다.
