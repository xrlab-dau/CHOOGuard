# CHOOGuard: JEV 공식 모델·API·SDK의 NPC 적합성 조사

- 조사 기준일: 2026-09-25.
- 범위: 공개된 TypeSafe 1차자료, 공식 SDK 소스, Vercel·Cloudflare의 자사 통합 문서. 구현 제안은 검증된 기능과 분리했다.
- **이 조사에서는 JEV 추론 API, Playground 실행, SDK 설치, 빌드, lint, 테스트를 실행하지 않았다. 현환경의 모델 성능·지연·요금은 미측정이다.** 공개 문서와 공개 소스의 읽기만 수행했다. 부모 작업의 별도 live 실험 결과와 이 보고서를 혼동하지 않는다.
- 게임 코드, Assets, Packages, ProjectSettings, 씬, 모델링, 기존 문서는 변경하지 않았다. 이 파일만 새로 작성했다. 키·토큰 값은 열람·복사·저장하지 않았다.
- 표기: **[공식 명세]** 공개 API/문서 계약, **[공급사 발표 측정]** 공급사가 공개한 실험 결과, **[소스 관찰]** 직접 읽은 공개 코드, **[INFERENCE]** CHOOGuard 적용 판단, **[미확인]** 공개 근거로 확정하지 못한 사항.

## 1. 결론: 대화 모델 대체가 아니라, 제한된 행동정책의 유력 후보

**[INFERENCE] JEV는 AI 동료·시민 NPC의 의미 기반 의도 분류, 상황 평가, 이미 허용된 행동 중 선택에 적합한 후보다. 그러나 JEV 하나만으로 자유대화·장기기억·학습·물리조작까지 갖춘 NPC 세계가 완성되지는 않는다.** 이것은 “챗봇이 아니므로 NPC에 불가”라는 결론이 아니다. 게임 엔진이 상태·행동·권한·실행을 소유하고 JEV가 좁은 의미 판단을 하는 형태는 공식 권장 사용법과 일치한다.[S1][S2][S7][S10]

- **AI 동료:** 핑과 발화의 의도, 현재 역할과 요청의 관련성, 관측 범위 내 상황의 우선순위를 평가하고 `follow / observe / assist / report / handoff / wait` 같은 코드 소유 행동 후보를 고르는 데 활용 가능하다. 실제 이동·조작·협동 자원 배정은 게임 코드가 담당한다.
- **시민:** 목적지·일상 과업·동행·도움 요청·대피/안내 등의 선택은 후보 집합을 갖춘 경우 가능하다. 생활 스케줄, 관계·기억의 저장, 여러 NPC의 동시 충돌 해소는 JEV API에 내장된 기능으로 확인되지 않았다.
- **직무 전환·정비·청소:** 역할·도구·작업 단계에 맞는 의미 판단은 후보지만, 작업 순서의 필수 조건·설비 상태·수치 계산·실제 도구 조작·점수 산정의 확정 규칙은 코드와 검증된 작업 명세가 소유해야 한다.
- **사건 대응:** 사고·자연재해·공격 사건의 탐지 단서 해석, 대피·신고·접근 제한·전문 담당자 인계 지원은 평가 대상이다. 공격 수행·위해 실행 절차를 이 모델의 행동 후보로 설계하지 않는다.
- **자유대화가 필수라면 혼합형:** 대사 템플릿만으로 충분한 부분은 JEV+게임 코드로 가능하고, 새 자연어 발화가 필요하면 별도의 생성 모델이 필요하다. 생성영상은 이 구성의 전제조건이 아니다.[S8][S11]

권고 판정은 **“통제된 NPC 행동정책 실험에 조건부 적합; 게임의 유일한 지능·안전·실행 계층으로는 부적합”**이다. 한국어, 권한, 부분관측, 다중 NPC 협동, 실제 배포망 지연을 실험하기 전에는 제품 채택을 확정하지 않는다.

## 2. 판본과 확인 수준

| 대상 | 직접 확인한 판본·상태 | 한계 |
|---|---|---|
| 공식 모델 목록 | `jev-1.13.0`; `jev-latest`, `jev-preview` 모두 현재 이 버전을 가리킨다고 명시 | 계정별 `GET /v1/models`는 실행하지 않음. alias는 이동 가능 |
| 알려진 약점 문서 | `Jev 1.13 jaggedness`, **Last reviewed 2026-09-17** | 공식 약점 설명이며 CHOOGuard 측정이 아님 |
| API | `/v1/systemone`, 공개 API reference | 상세 HTTP 계약을 읽었지만 인증 요청·경계값 검증 미실행 |
| JavaScript SDK | 공식 docs가 연결한 **`v0.6.0` 태그**의 `src/client.ts`, `src/types.ts`, `src/retry.ts` 직접 열람. Changelog: 2026-09-15 | 설치본 실행 없음. 타입 정의와 서버 실제 허용 범위는 동일하다고 단정하지 않음 |
| Python SDK | 공식 저장소 `main`의 `pyproject.toml`: **0.7.1**, Python >=3.10. Changelog: 2026-09-21. `constants.py`, `_version.py` 직접 열람 | `main`은 가변 URL이며 commit SHA 고정 아님. `_version.py` 자체는 설치 배포판 메타데이터를 읽음 |
| 함수 호출·병렬 질문 cookbook | 코드에 **`TYPESAFE_MODEL = "jev-1.12"`** 명시 | 현재 1.13의 직접 벤치마크로 바꾸어 인용하지 않음 |
| Vercel TypeSafe 호환 문서 | `last_updated: 2026-09-21` | gateway alias 뒤의 실제 JEV 버전 보장은 별도 확인 필요 |
| Vercel Evaluation 문서 | `last_updated: 2026-09-22`, AI SDK 7+의 experimental evaluation | TypeSafe 호환 API와 필드명이 다름 |
| Cloudflare 모델 페이지 | `typesafe/jev`, 공개 응답 예시는 `jev-1.13.0` | 명시적 문서 수정일 미확인. 예시 응답은 live 결과가 아님 |
| TypeSafe 소개 블로그 | 본문 게시일 **2026-09-15**, 작성자 Diogo Almeida | 읽기 응답의 `published` 메타는 2026-09-25로 달랐다. 본문 날짜와 메타 날짜를 구분 |

이 보고서에서 “모델카드”는 공식 Models 페이지와 SDK의 `ModelCard { name, description, release_date }` 계약 수준을 뜻한다. 학습 데이터 전체, 파라미터 수, 공개 가중치, 한국어/NPC 분야별 calibration 검증이 담긴 별도 상세 모델카드를 확인했다는 뜻이 아니다.[S1][S12][S13][S14][S15][S16]

## 3. 입력·질문·출력의 정확한 의미

### 3.1 공통 요청

**[공식 명세]** 직접 API는 `POST https://api.typesafe.ai/v1/systemone`이며 본문은 `state`, `model`, `questions`다. `questions`는 질문 ID를 키로 하는 map이다. 질문 ID는 응답을 매칭하기 위한 것이며 **모델 추론에 전달되지 않는다.** 따라서 `npc17_must_not_enter`라는 ID만 붙이고 실제 지시문에서 대상·금지 조건을 생략하면 의미가 전달되지 않는다.[S2][S3]

`state`는 문자열, JSON 객체, 배열로 제공한다. 객체 안에 숫자·boolean 등 구조화 필드를 포함하는 공식 예제가 있다. “text only”란 이미지·오디오·비디오 입력을 직접 이해한다는 뜻이 아니라 텍스트/JSON 상태를 평가한다는 뜻이다. 게임 화면, 음성, 영상은 별도 인식이나 엔진 관측으로 텍스트/구조화 상태를 만들어야 한다. JSON 숫자가 허용되는 것과 모델이 수치 비교·계산을 신뢰성 있게 수행하는 것은 별개다.[S1][S4][S7]

질문의 `instructions`는 문자열·객체·배열을 받을 수 있고, 기준 설명에도 구조화 자료를 넣을 수 있다. NPC 식별자, 역할, 관측의 출처, 판단 조건은 실제 instructions/criteria/state에 명시해야 한다.[S2]

| primitive | 질문 스키마 | 응답 | 정확한 해석과 금지할 오해 |
|---|---|---|---|
| **Choice** | `type: "choice"`, `instructions`, `criteria: { option: description }`; 설명은 문자열/객체/배열/null. 최대 **255개** 옵션 | `choice`, 모든 옵션의 `probabilities`, `confidence` | `choice`는 최고 확률 옵션. 확률 합은 1. 상대적인 단일 선택이며 multi-select나 “해당 행동이 절대적으로 적절하다”의 증명 아님. 적합한 후보가 없을 때를 위한 `none/unknown/ask` 등을 의도적으로 설계해야 함 |
| **Score** | `type: "score"`, `instructions`, 순서 있는 `criteria` 배열. **2~10단계** | `score`, `legend`, 단계별 `probabilities`, `confidence` | 단계 번호는 배열 순서대로 **0..L-1**. `score = Σ(i × p_i)`이며 연속값 가능. 0~1 고정 점수가 아님. 서로 다른 단계 수의 점수를 합치려면 `score/(L-1)` 등 명시적 정규화 필요 |
| **Noul** | `type: "noul"`, `instructions`, 선택적 `criteria: { true: ..., false: ... }` | `type`, `noul` | `noul = P(yes)`의 0~1 값. boolean 자체도, 위험의 강도도 아님. 0은 강한 no, 1은 강한 yes, 0.5 부근은 yes/no가 비슷함. **별도 confidence 없음** |

출처: [S2][S3][S5][S6]. Noul은 “위험한가?”의 긍정 확률이고 Score는 정의한 위험 단계의 기대값이다. 이를 확률 0.7 = 위험도 70%로 바꾸지 않는다. Score 역시 거리·시간·온도·피해량의 정밀 추정기로 사용하지 말라는 공식 경고가 있다.[S7]

**문서/SDK 차이:** 공개 API reference는 instructions를 required로 설명하지만 JS v0.6.0 타입은 `instructions?: EntryType` 및 null을 허용하고, `EntryType`에는 null도 있다. 소스가 더 느슨하다는 사실이지 모든 gateway에서 null·생략을 검증했다는 뜻이 아니다. CHOOGuard 실험 요청은 공통적으로 명시된 정상형인 non-null state, 명확한 instructions, 비어 있지 않은 questions를 쓰는 편이 안전하다.[S2][S13]

### 3.2 한 요청의 여러 질문: 공유 상태 + 독립 평가

**[공식 명세]** 질문들은 **같은 state를 보고 각각 독립적으로, 병렬로** 평가된다. Choice/Score/Noul을 한 요청에 섞을 수 있다. 한 질문의 답이 같은 요청의 다른 질문의 숨은 입력으로 쓰이지 않는다.[S4][S9][S10]

- 가능한 것: “발화 의도”, “도움 요청 여부”, “긴급성”, 각 후보 역할의 관련성 등을 동시에 평가한 뒤 게임 코드가 유효한 결과만 채택한다.
- 불가능한 가정: Q1에서 선택된 동행자의 정보를 Q2가 자동으로 알고 그 동행자의 행동을 결정한다. 미리 모든 후보별 질문을 만들거나, Q1 이후 새 상태로 두 번째 요청을 해야 한다.
- **평가 독립성은 사건들의 통계적 독립성을 뜻하지 않는다.** `P(A and B)=P(A)P(B)`로 임의 곱하거나, 여러 NPC가 독립 선택했으므로 협동 배정이 충돌하지 않는다고 판단할 근거가 없다.
- 한 요청의 여러 NPC 질문은 토큰을 아낄 수 있지만 **모든 질문이 같은 state를 본다.** NPC별로 비밀·시야·청각·정보 출처가 다르면 전체 세계 상태를 공유한 뒤 “너는 모른다”라고 적는 것만으로 정보 격리를 보장하지 못한다. [INFERENCE] 기본은 actor별 허용 관측 snapshot을 만들고 그 actor의 질문들을 묶는 방식이다.
- 공개된 토큰 상한 외에 별도 최대 질문 개수·옵션×질문 조합별 latency 곡선은 확인하지 못했다. “병렬이므로 무한 질문도 동일 지연”은 아니다.

### 3.3 확률 보정과 confidence의 한계

**[공식 설명]** RLCD(Reinforcement Learning for Calibrated Decisions)는 생성 문자열 대신 보정된 결정 확률을 학습하는 방식이라고 설명한다. 잘 보정된 예측 집합에서 p=0.8인 결과가 약 80% 맞는 것이 calibration이지, 개별 판단의 무오류 보장이 아니다.[S17]

`confidence`는 Choice/Score의 **기존 확률분포에서 계산한 통계량**이다. 별도의 검증기나 permission 증명서가 아니다. confidence 문서의 대화형 3옵션 예시는 `(3 × max_probability - 1)/2`를 **approximate** 계산으로 설명한다. 이 예시를 일반적인 생산 API의 확정 공식으로 복제하지 않는다.[S18]

공식 약점 문서가 직접 제시한 반례:[S7]

1. 같은 환불 의도를 물어도 Noul은 0.22, yes/no Choice의 yes 확률은 0.01일 수 있다. 유형을 바꾸면서 threshold를 그대로 재사용하면 안 된다.
2. “환불인가?” 0.72와 “환불 아닌가?” 0.47의 합이 **1.19**인 예가 있다. 별도 질문 간 논리적 보완 관계를 모델이 강제하지 않는다.
3. 잘못된 기준·유도 문구·부정·큰 잡음 상태에 취약하다. confidence가 높은 잘못된 판단도 배제하지 못한다.

따라서 홈페이지의 **“Zero Hallucinations”**는 공개 블로그가 설명하는 schema/type 제약의 주장으로 읽어야 한다. 사실 판단, 안전성, 역할 적합성, NPC의 세계 이해가 언제나 맞다는 보장은 아니다. 이 보고서는 자체 ECE/Brier score, 한국어 calibration, 위험별 false-positive/false-negative를 측정하지 않았다.[S7][S16][S17][S18]

## 4. 대화·도구·기억·학습·자가호스팅 구분

| 기능 | 공개 근거로 확인된 지원 수준 | CHOOGuard에서 필요한 별도 책임 |
|---|---|---|
| 자연어 자유대화 생성 | **지원하지 않음.** 공식 coding-agents와 jaggedness 문서가 text generation·conversation 비대상이라고 명시 | 생성 LLM 또는 검증된 대사 템플릿. JEV는 발화 목적·대사 후보 선택 가능 |
| 임의 코드/문장 생성 | **지원하지 않음.** choice를 연쇄해 억지 문자열 생성하는 것은 느리고 잘 안 된다고 경고 | 사건/대사의 새 텍스트·새 코드가 필요하면 다른 도구 |
| 도구 호출 | **bounded function dispatch는 공식 cookbook로 시연.** 함수명과 enum 인수를 Choice, boolean을 Noul 등으로 판단 | 실제 함수 실행, 인수 교차 검증, 권한, 최신 상태 확인은 코드. OpenAI식 자유 인수 생성·자율 도구 루프와 다름 |
| 임의 숫자/자유 텍스트 인수 추출 | cookbook에서 일반 int·free text·dates는 해당 질문을 만들지 않고 함수 기본값 유지 | regex/parser/엔진 후보 생성 또는 다른 모델 후 JEV 선택·검증 |
| 영속 memory | 공개 평가 API에 session/memory/thread 자원·지속 기억 기능 **미확인**. 매 요청 state 제공 | 기억 저장·검색·요약·망각·관계 갱신, episode ID와 관측 시점 관리 |
| NPC별 성격/역할 | 요청의 state/instructions/criteria로 조건화 가능 | 일관된 persona 자료, 역할별 허용 행동, 과거 약속의 외부 저장 |
| fine-tuning/LoRA | **고객별 fine-tuning/LoRA를 하지 않는다고 명시.** 모든 계정에 같은 weights | 도메인 규칙·사례·상태로 조정. 가중치가 플레이 중 스스로 학습한다고 설명하면 안 됨 |
| 행동학습·실험 | JEV 확률을 downstream 고전 모델의 feature로 쓰는 공식 안내 존재. JEV 자체 온라인 학습 API는 미확인 | episode 기록·실험·정책 비교·외부 learner. 행동 변화가 prompt/context 변화인지 정책 학습인지 구분 |
| self-host/온디바이스 | 검토한 공식 모델·API·SDK·문서 인덱스에 JEV weights/로컬 추론 패키지/온프레미스 배포 절차 **미확인** | 공개 SDK는 원격 API client이며 inference engine이 아님. 현재 증거로 오프라인 JEV 실행 계획을 잡을 수 없음 |
| 이미지·음성·영상 | 현재 모델 입력 **text only** | 게임 엔진 관측, 필요시 STT/별도 perception. 모델 스스로 화면을 본다는 가정 금지 |

출처: [S1][S2][S7][S8][S11][S19]. Self-host는 “영구적으로 제공하지 않는다”로 단정하지 않았고, **현재 확인한 공개 지원 근거가 없다**고 판정했다. SDK 소스 공개와 모델 weights 공개는 별개다.

### 직접 읽은 공개 코드가 보여 주는 것

- **JS `client.ts` v0.6.0:** 기본 base URL은 `https://api.typesafe.ai`, 기본 model은 `jev-latest`. `systemOne()`은 질문 검증 후 `/v1/systemone`에 POST하고 JSON 응답을 반환한다. 브라우저에서는 기본적으로 키 노출을 막기 위해 생성자를 거부한다. **모델을 로컬 실행하는 코드가 아니다.** 응답 파싱 후 `as T`로 반환하는 부분은 TypeScript 정적 타입이지 runtime에서 모든 의미 제약을 검증하는 증명이 아니다.[S12]
- **JS `types.ts` v0.6.0:** Choice/Noul/Score 요청·응답, 모델 ID·usage, retry·AbortSignal 계약을 직접 확인했다. request ID 질문 map과 각 primitive를 native하게 노출한다.[S13]
- **JS `retry.ts` v0.6.0:** 기본 timeout 10,000ms, 최초 시도 뒤 최대 2회 retry, 408/429/5xx·연결 오류·timeout 재시도, 초기 500ms exponential backoff, 최대 5,000ms, jitter 0.25. 허용된 Retry-After는 최대 60,000ms까지 존중한다. timeout은 **시도별**이며 총 retry 시간 예산은 별도다.[S14]
- **Python `constants.py`, `_version.py`, `pyproject.toml`:** 기본 API/model, HTTP operation timeout 10초, 설치 metadata 기반 버전, 배포판 0.7.1을 확인했다. JS의 정확한 retry 동작을 Python에도 동일하다고 확장하지 않는다.[S15]
- **공개 샘플:** Python quickstart는 한 state에 세 primitive를 함께 요청한다. function-calling cookbook는 10개 함수/28개 closed-set 인수에 대한 **54개 질문**을 한 요청으로 보내고 선택된 함수의 답만 사용한다. 이는 tool dispatch의 실질적인 예이지만 모델은 `jev-1.12`이고 이 조사에서 실행하지 않았다.[S19][S20]
- **Smart-home 문서:** 복합 요청 분해와 자유 대화는 LLM으로 넘기는 혼합형을 명시한다. “full source will be available”라고 적혀 있어 이 데모의 전체 GitHub 소스를 읽었다고 주장하지 않는다.[S11]
- SDK의 debug 로그는 headers를 일부 redact해도 **본문은 redact하지 않는다.** NPC 대화·개인정보·민감 상태를 불필요하게 전송/로그하지 않아야 한다.[S13][S21]

## 5. 운영 제약: 지연·문맥·동시성·언어

### 5.1 현재 공식 모델 수치

| 항목 | 공식 Models 페이지 수치 | 게임 적용상의 해석 |
|---|---|---|
| 입력 요금 | **$0.042 / 1M tokens = $42 / 1B tokens** | 질문·상태 등 실제 input usage에 과금. output tokens 무료 |
| 요청 속도 | **1,200 requests/minute** | 평균 환산 20 requests/sec일 뿐 burst allowance·동시 연결 수 보장 아님 |
| 토큰 속도 | **250,000 tokens/sec** | 별도 rate limit. 요청/토큰 한도를 함께 고려 |
| 전체 요청 문맥 | **64k**: state + 모든 질문 합계 | 최대 크기이지 권장 상태 크기 아님 |
| 개별 질문 관점 문맥 | **32k**: state + 가장 긴 질문 | 64k 상태를 한 질문에 보내도 된다는 의미 아님 |
| 언어 | 영어가 주 학습 언어, CJK 등도 처리하나 정확도 약함 | 한국어·영어·혼합 고유명사/직무약어를 따로 검증 |
| 오류 | 401, 422, 429, 529 overload 등을 문서화 | 실패·과부하·재시도 동안 게임이 멈추면 안 됨 |
| 한도 안정성 | 수요에 따라 예고 없이 조정될 수 있다고 경고. custom/enterprise 높은 한도 가능 | 실험 당시 한도·계정 상태를 기록하고 SLA로 오인하지 않음 |

출처: [S1][S2]. 최대 in-flight concurrency, 안정적인 burst 한도, 계정/조직/키별 합산 기준, 대기열 길이, 보장 p95/p99 SLA, 한국/아시아 regional endpoint·region pinning은 검토한 공개 자료에서 확정하지 못했다.

### 5.2 지연 발표의 조건과 과장 방지

| 수치 | 출처·실험 조건 | 이 수치로 말할 수 없는 것 |
|---|---|---|
| end-to-end **70~500ms**, 흔히 약 100ms라는 설명 | 소개 블로그·how-to-build. 공급사 eval은 일반적으로 **미국 서부 노트북**, 당시 서비스도 미국 서부 기반이라고 설명 | 한국 사용자→proxy→provider→게임 적용 전체 경로의 p95가 100ms라는 보장 아님 |
| **193.6배 빠름 / 444.6배 저렴** | 4개 structured workflow 비교의 발표. 회사는 실제 이득의 높은 쪽일 것으로 인정. reference는 강한 두 외부 모델의 평균, 자체 ground truth 검증이 아님 | 모든 NPC 상황·모든 모델·모든 한국어 요청에서 같은 이득이라는 뜻 아님 |
| 단순 데모의 큰 이득 | 짧고 밀도 높은 state가 JEV에 유리하다고 공개. 상대 모델은 기본 reasoning 설정 | 입력 길이·reasoning·답 형식이 다른 결과의 무조건 비교 불가 |
| **0.27초/$0.000497** vs **2.71초/$0.006090** | `jev-1.12`, GDPR 문서 **53,777문자**, 13질문(8 Noul/2 Choice/3 Score), 전략별 5회 반복, 공급사 cache 기록 평균 | 1.13/CHOOGuard 측정 아님. 10배 지연 차이는 **13개 single 호출을 순차 실행한 합**과 비교한 것 |
| 게임 Doom 예시 10 calls/sec, 약 $7/hour | 공급사 블로그 데모. **영상 입력이 아닌 구조화 텍스트 상태**. 비AI bot가 더 잘할 수 있다고 인정 | 게임 품질·협동 NPC·안전성·실시간 FPS 프레임 제어의 검증 아님 |

출처: [S9][S10][S16][S22]. 4개 workflow eval 사이트는 비용·시간·정확도를 워크플로별 동일 가중 평균으로 설명하며, reference 두 모델에는 high thinking, 비교 모델은 provider 기본 reasoning을 사용했다고 명시한다. 상세 지역별 percentile, 동시 요청 부하, GPU 구성, 한국어 NPC 세트는 공개한 이 표에서 확인되지 않았다.

**병렬 cookbook의 제목을 문자 그대로 과대해석하지 않는다.** “답이 바뀌지 않는다”는 공급사 설명이지만 공개 표의 `breach_72h` 평균은 batch 0.804와 single 0.814로 같지 않다. 대부분 항목은 동일하며 일부에는 run-to-run 변동이 있고, 표본도 5회다. API의 독립 평가 설명과 별개로 bitwise deterministic·완전 무분산을 검증한 자료는 아니다.[S9]

**[INFERENCE] 게임 루프 권고:** FPS 입력·물리·충돌·직접 도구 조작은 로컬의 deterministic loop에 남기고, JEV는 이벤트 발생/관측 변화/목표 변경 시의 상위 판단으로 둔다. 응답 전에도 NPC가 대기·기존 안전 행동을 할 수 있어야 한다. 늦게 도착한 snapshot의 선택은 최신 세계 상태·권한과 대조한 뒤 폐기하거나 다시 판단한다. 이 권고는 구현되지 않았다.

## 6. Direct API, Vercel, Cloudflare는 같은 계약이 아니다

| 경로 | 정확한 공개 경로·model | 질문/응답 차이 | 요금·한도·확인 주의 |
|---|---|---|---|
| TypeSafe direct | `https://api.typesafe.ai/v1/systemone`; `jev-1.13.0` 또는 alias | `noul/choice/score`, `usage.input_tokens`, Choice/Score의 confidence | TypeSafe 키와 direct 요금. 버전 ID 직접 pin 가능하다고 명시 |
| Vercel TypeSafe-compatible | `https://ai-gateway.vercel.sh/typesafe/v1/systemone`; 예시 `typesafe-ai/jev` | TypeSafe 형태 유지; `provider_metadata.gateway`에 routing/cost 추가 가능 | AI Gateway key 또는 OIDC. gateway 청구, BYOK이면 provider 직접 청구 가능. gateway의 alias 응답이 JEV 상세 버전이라는 보장은 없음 |
| Vercel Evaluation HTTP / AI SDK | `https://ai-gateway.vercel.sh/v1/evaluate`; `typesafe-ai/jev`; AI SDK `experimental_evaluate` | `noul` 대신 **`boolean`**, 응답 **`probability`**; `inputTokens`, `providerMetadata` 등 camelCase. 공급사 confidence는 별도 provider metadata에 제공된다고 안내 | AI SDK 7+ experimental. **OpenAI/Anthropic/Cohere 호환 endpoint에서는 evaluation 미지원** |
| Cloudflare 공개 Jev 모델 통합 | `env.AI.run('typesafe/jev', {state, questions})`; HTTP `https://api.cloudflare.com/client/v4/accounts/$CLOUDFLARE_ACCOUNT_ID/ai/run`, body `{model, input}` | input에 TypeSafe primitive를 넣는 예제. 응답 예시는 model `jev-1.13.0` 및 TypeSafe answers | `Third-party`, ZDR Yes, context **32,000**, 가격은 dashboard 링크만 제공. API token/계정 권한 필요 |

출처: [S23][S24][S25][S26][S27]. 변수 이름은 문서의 식별자일 뿐 실제 자격증명 값이 아니다.

주요 차이와 미확인 사항:

1. **문맥 차이:** direct 문서는 64k total/32k longest를 구분하지만 Vercel 모델 카탈로그·Cloudflare는 32,000 context를 표시한다. 동일한 내부 한도를 단순 표기한 것인지 gateway에서 더 제한하는 것인지 **미실행·미확인**이다. 보수적으로 경로별 제한을 검증해야 한다.
2. **output tokens 0의 해석:** Vercel 카탈로그의 maximum output tokens 0은 자유 텍스트 생성 capability 항목이다. 구조화 결과와 usage의 output_tokens까지 존재하지 않는다는 뜻이 아니다. 같은 공식 문서 예시에 구조화 answers와 output usage가 있다.[S23][S25]
3. **라우팅:** Vercel 카탈로그는 providers `typesafe-ai`, `digitalocean`을 나열한다. provider별 성능·내부 버전·지역이 모두 같다고 추정하지 않는다. 실제 provider metadata를 실험 기록에 남겨야 한다.
4. **Cloudflare는 구분 필요:** 확인한 것은 Cloudflare AI의 third-party 모델 및 `AI.run` 계약이다. 이를 별도 AI Gateway의 native TypeSafe pass-through URL이나 Cloudflare 전세계 edge에서 JEV weights를 로컬 추론한다는 증거로 바꾸지 않는다. 별도 gateway passthrough 명세는 확보하지 못했다.
5. **프록시는 새 모델 능력을 만들지 않는다:** gateway의 chat/이미지/도구 등 일반 기능 목록이 JEV의 대화 생성·멀티모달 지원을 의미하지 않는다.
6. **데이터 보관도 경로별:** TypeSafe는 customer 요청/응답으로 학습하지 않는다고 명시하고 enterprise ZDR을 안내한다. Cloudflare 모델 페이지는 ZDR Yes, Vercel은 ZDR/no-training routing 옵션을 안내한다. gateway 로그까지 전부 자동 무보관이라는 뜻은 아니며 각 경로의 실제 설정이 필요하다.[S1][S28][S26][S27]
7. **무료 홍보 문구 충돌:** 검색 결과에는 Vercel의 “September 25th까지 무료”라는 이전 문구가 있었지만 직접 읽은 현재 launch 문서에는 그 문구가 없고 모델 카탈로그는 $0.042/M input을 표시했다. 오늘의 무료 여부·종료 시각을 확정하지 않고 유료 단가로 예산을 산정한다.[S25][S27]

### 현재 로컬 어댑터와 공식 기능을 혼동하지 않기

**[부모 전달 정보, 이 조사자가 로컬 소스/런타임 독립 검증하지 않음]** 현재 CHOOGuard의 `workers/prediction/jev-proxy.mjs`는 Vercel 경로를 사용하고 `/turnaround`만 노출하며 Noul 전용 어댑터, 1 in-flight, 3초 timeout이라는 제약이 있다고 전달받았다. 이 값들은 **현재 어댑터의 제한이지 JEV 모델의 공식 제한이 아니다.** 공식 API와 SDK에는 Choice/Score/Noul이 모두 존재한다. 어댑터 수정과 모든 호출부 검토는 별도 구현 과제이며 이 보고서에서 수행하지 않았다.

## 7. 가격 산식, 배치, 미확인 요금

**[공식 단가]** direct input 가격 `r = $0.042 / 1,000,000 tokens`, output 무료.[S1]

- 요청 j의 기본 추론 비용: `C_j = usage.input_tokens_j × 0.042 / 1,000,000`.
- 총 기본 비용: `C = Σ C_j`. 최종 실측은 실제 usage와 provider/gateway 청구 기준으로 대조한다.
- 동일 state S와 질문 Q1..Qk를 나누어 요청하면 대략 `kS + ΣQ_i + Σoverhead_i`; 하나로 묶으면 `S + ΣQ_i + overhead_batch`. **질문은 공짜가 아니고 state 중복을 줄이는 것**이다.
- 이것은 한 요청의 multi-question batching이다. 비동기 파일 제출형 Batch API, 지연 처리 할인, 별도 batch 요율, prompt-cache 할인은 검토한 TypeSafe 문서에서 **미확인**이다. “batch니까 50% 할인” 같은 일반 LLM 요금 가정을 적용하지 않는다.

다음은 **[INFERENCE: 가정 계산]**이며 게임 실행 결과가 아니다. 각 요청이 실제 청구 입력 2,000 tokens이고 retry·proxy 비용이 없다고 가정했다.

| 가정 | 요청/분 | 입력 tokens/sec | 기본 추론 비용/시간 | direct 공표 한도와 비교 |
|---|---:|---:|---:|---|
| NPC 1명, 1Hz | 60 | 2,000 | **$0.3024** | 공표 한도 내 산술상 가능 |
| NPC 20명, 각 0.2Hz | 240 | 8,000 | **$1.2096** | 공표 한도 내 산술상 가능 |
| NPC 20명, 각 1Hz | 1,200 | 40,000 | **$6.048** | RPM 한도와 같아 여유 없음 |
| NPC 100명, 각 1Hz | 6,000 | 200,000 | **$30.24** | TPS 아래라도 RPM 초과 |

NPC당 요청 주기 f, NPC수 N, 입력 T일 때 `시간당 C = N × f × 3600 × T × 0.042 / 1e6`. 서버가 여러 사용자 세션을 처리하면 동시 세션 수도 곱한다. 실제로 NPC별 질문 수·기억 검색 결과·언어별 tokenization·사건 밀도에 따라 T와 f가 달라진다.

**미확인 비용:** Cloudflare Jev 실청구 단가는 공개 모델 페이지에서 dashboard로 연결되어 확인하지 못했다. Vercel catalog 기본 단가는 확인했지만 계정별 크레딧·무료기간·BYOK surcharge·gateway 사용료·호스팅/egress/관측 로그·retry 중복 과금·실패 요청 과금·세금·약정 할인은 이 조사에서 확정하지 못했다. 서비스 전체 비용은 `JEV 입력 + gateway/hosting + 저장/관측 + 선택적 생성대화/STT/TTS`로 구분해야 한다. output 무료는 서비스 전체가 무료라는 뜻이 아니다.

## 8. 단독/혼합 역할 매트릭스

여기서 “JEV 단독 정책”은 **다른 생성 모델 없이 JEV+보통의 게임 코드/콘텐츠를 쓰는 것**이다. 게임 실행기까지 JEV로 대체한다는 뜻이 아니다. 아래 판정은 모두 [INFERENCE]이며 직접 NPC 실험 미수행이다.

| NPC 요구 | JEV 단독 정책 | 혼합형 | 코드가 소유할 불변조건 |
|---|---|---|---|
| 자연어 핑/짧은 요청 의도 분류 | 유력 후보. finite intent Choice + 불명확 분기 | 복합 요청 분해가 필요하면 생성 모델/규칙 파서 | 지원하지 않는 요청을 행동으로 강제 변환하지 않기 |
| 역할별 행동 선택 | 유력 후보. 역할·현재 과업·도구·허용 후보 제시 | 계획 모델이 후보 제안, JEV가 후보 평가 가능 | 자격·역할·도구 보유·작업 단계 사전조건 |
| 세부 청소/정비 직접조작 | 어떤 절차가 관련 있는지 조언/분류 가능 | 자연어 설명 생성은 별도 | 물리·안전 순서·도구 pose·입력 판정·완료 조건 |
| 관측과 상황 판단 | 텍스트/JSON 관측의 의미 평가 가능 | 시각/음성은 엔진 또는 별도 perception | 실제 센서 범위, 오래된 관측 표시, 보지 못한 사실 차단 |
| 동행자 선택·지원 역할 분담 | 후보별 적합성/Choice는 가능 | 복잡한 계획은 planner 결합 가능 | 자원 reservation, 한 대상 중복 배정·충돌 방지 |
| 일상생활 자율성 | 일정·목적 후보가 준비되면 선택 가능 | 동적 이야기/새 목표 설명은 다른 생성기 | 시간 계산, 이동, 욕구/관계 상태, 세이브 |
| 불확실성에 따른 질문/보류 | 분포·confidence 기반 routing 후보 | 생성 모델로 확인 질문 표현 | threshold의 도메인별 검증, 정보 부족 분기 |
| permission/안전 승인 | 의미상 규칙 충돌의 **보조 신호**만 | 다른 모델을 붙여도 보조 판단일 뿐 | authoritative allowlist/deny, state version, 권한 검사 |
| 자유대화·즉흥 설명 | 새 문장 생성 불가. 템플릿/대사 선택만 가능 | 생성 LLM + JEV 의도/대사 적합성 평가 | 발화와 실제 행동 일치, 확인되지 않은 사실 금지 |
| 기억·관계의 장기 일관성 | 저장된 관련 기억을 state로 전달하면 조건화 가능 | 외부 memory/RAG와 필요시 요약기 | 저장·검색·시간·출처·NPC별 접근권한 |
| 사건 구성 | 준비된 사고/재해/방어 시나리오 후보 선택·긴급성 평가 | 새 서술은 생성기, 실제 사건 발생은 게임 규칙 | 콘텐츠 한계·발생 조건·재현 seed·훈련 목표 |
| 행동학습/정책 실험 | 고정 모델의 결정 비교·feature 추출 가능 | 외부 learner/planner + 반복 평가 | episode 수집, holdout, 정책 버전, 학습과 평가 분리 |

사용자 목표인 “AI/NPC가 활동하는 세계에서 사람이 대응”은 JEV의 유한 선택 인터페이스와 양립한다. 다만 선택 가능한 행동의 의미·결과를 게임이 이미 구현해 두어야 하며, JEV가 생성 API 한 번으로 생활 세계를 만들어 준다고 기대하면 안 된다.

## 9. 구현 전 반드시 실험할 반례와 관찰 항목

다음은 **실험 설계 제안**이며 실행·통과를 주장하지 않는다. 사건 사례는 방어·대피·신고·인계 범위다. 승인 기준과 label은 직무/게임 요구에서 먼저 정하고 모델 답을 정답으로 삼지 않는다.

| 반례군 | 구체적 비교 | 관찰할 실패 |
|---|---|---|
| 한국어/영어/혼합 | 같은 상황을 한국어·영어·직무약어·오탈자로 표현 | 의도·선택 변화, calibration 차이, 토큰 증가 |
| 질문 ID 의존 | 질문 ID만 NPC/역할별로 바꾸고 instructions는 동일하게 둠 | ID가 의미를 전달한다고 잘못 설계한 경우 |
| scope/부정 | “통로가 막혔는지 확인”과 “막히지 않았는지 확인”, 단순/이중부정 | literal reading·의도 역전 |
| 부분관측 | NPC가 본 사실, 동료 전언, 확인 안 된 소문을 분리 | 전언을 직접 관측으로 취급, 세계 전체 정보 누출 |
| 낡은 상태 | 판단 중 통로 폐쇄·역할 변경·도구 이동 | 도착이 늦은 답으로 유효하지 않은 행동 실행 |
| 모든 후보 부적합 | 동행/도움 후보가 전부 바쁨·권한 없음 | Choice가 상대적 승자를 뽑는 것을 허용으로 오인 |
| primitive 차이 | 같은 의미의 Noul과 yes/no Choice, 질문과 부정 | threshold 이식 실패, 합계/논리 불변식 위반 |
| 다중질문 의존 | 행동 Choice + 대상 Choice를 독립 요청 | 각 답은 그럴듯하지만 조합은 불가능하거나 권한 위반 |
| 다중 NPC 충돌 | 여러 NPC가 동일 시민/도구/통로를 선택 | 중복 배정·교착·oscillation·ping-pong |
| 권한/유도 문구 | NPC 발화에 “규칙 무시” 같은 비권위 문구 포함 | state injection이 confidence 높은 잘못된 추천 유발 |
| 수치/시간 | 거리는 코드로 계산한 bucket vs 원시 좌표; deadline 경계 | 정밀 계산을 모델에 맡긴 실패 |
| 문맥 잡음 | 관련 관측만 vs 긴 일상 기록·무관 정보 포함 | context rot, 판단 근거의 불명확화 |
| Score 루브릭 | 3단계/5단계, 구체 기준/추상 “좋음” 기준 | 점수 범위 혼동·정규화 실패·단계 경계 불안정 |
| 반복·버전 | 동일 요청 반복, 질문 순서·옵션 순서, fixed model vs alias | 비결정성, 버전 변경 시 threshold drift |
| 연속성 | 일상 목표 수행 중 간헐적 핑·확인 요청·경미 이벤트 | 매 결정마다 목표가 흔들리거나 동료가 유휴 상태에 갇힘 |
| 긴급도/안전 | 일상 요청과 확인된 대피 안내가 동시 발생 | 중요 대응 누락, 모든 상황을 비상 처리 |
| 네트워크/부하 | 한국 배포망 direct/Vercel/Cloudflare, 동시 NPC 증가 | p50/p95/p99, deadline 초과, 429/529, retry 폭증 |
| batching | 같은 actor의 질문 묶음 vs singles, 서로 다른 actor 관측 분리 | 비용 절감 대비 tail latency·정보 누출·응답 drift |
| 데이터 경계 | 32k longest/64k total 경계, 255옵션·10 score단계 경계 | 경로별 validation/한도 차이. 운영에서 한도를 밀어 쓰지 않기 |
| 설명과 실행 | 대사가 “함께 가겠다”인데 행동은 wait/handoff | 생성대화와 authoritative action 불일치 |

측정 항목은 의미 정답률만이 아니다. 역할/관측/권한 위반률, inappropriate-action rate, 보류율, 불필요한 확인 빈도, 목표 유지율, 업무 완료율, 대피/신고 인계 누락률, 여러 NPC 자원 충돌률, calibration(Brier/ECE 등), latency percentiles, 기한 내 유효결정 비율, 실제 입력 토큰·총 비용을 함께 본다. 상위 의미 판단이 빠르더라도 조작감·협동 재미가 좋아졌다는 증거는 별도의 플레이 관찰이 필요하다.

**정확성 vs 게임성 질의에 대한 해석 원칙:** JEV에 좁은 선택·평가 질문으로 의견을 받을 수는 있다. 그러나 그 답은 제품 방향을 결정하는 권위나 플레이테스트의 대체물이 아니다. 안전·역할·관측 일관성은 확정 규칙으로 두고, 반응 빈도·실수 허용·대사 다양성·지원 적극성 등은 사용자 의도와 실제 플레이 관찰로 평가해야 한다. 이 조사자는 해당 질의를 실행하지 않았다.

## 10. 1차 출처 목록

아래 URL은 모두 실제로 직접 읽은 자료다. 검색 요약만 나온 자료는 근거 목록에 넣지 않았다. 길이가 긴 문서는 관련 본문·스키마·결과 구간을 읽었으며, 문서에 내장된 UI 압축기 전체를 연구한 것은 아니다.

- **[S1]** TypeSafe Models: https://docs.typesafe.ai/models (markdown: https://docs.typesafe.ai/models.md) — 현재 버전, 단가, 64k/32k, rate limits, 언어, fine-tuning, 데이터 처리.
- **[S2]** API reference: https://docs.typesafe.ai/api.md — 요청/응답·primitive·오류 계약.
- **[S3]** Choice: https://docs.typesafe.ai/primitives/choice.md — 옵션과 질문 ID의 의미.
- **[S4]** State: https://docs.typesafe.ai/concepts/state.md — 입력 구조·공유 상태·독립 평가.
- **[S5]** Score: https://docs.typesafe.ai/primitives/score.md — 단계·기대값·정규화·루브릭.
- **[S6]** Noul: https://docs.typesafe.ai/primitives/noul.md — 긍정 확률, 별도 confidence 없음, threshold 해석.
- **[S7]** Jev 1.13 jaggedness: https://docs.typesafe.ai/model-jaggedness/jev-1.13.md — 2026-09-17 검토, 공식 반례·생성 제한.
- **[S8]** Jev with coding agents: https://docs.typesafe.ai/introduction/coding-agents.md — 대화/코드 생성 모델과 구분.
- **[S9]** Parallel questions cookbook: https://docs.typesafe.ai/cookbooks/parallel_questions.md — jev-1.12, 13질문/5회/순차 대비 결과.
- **[S10]** How to build: https://docs.typesafe.ai/concepts/how-to-build-with-system-one.md — code-owned workflow와 primitive 합성.
- **[S11]** Smart home demo: https://docs.typesafe.ai/demos/smart-home.md — compound splitting·자유대화의 LLM 분담.
- **[S12]** 공식 JS client v0.6.0: https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/client.ts — HTTP client 실제 소스.
- **[S13]** 공식 JS types v0.6.0: https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/types.ts — 요청·응답·설정 타입 실제 소스.
- **[S14]** 공식 JS retry v0.6.0: https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/retry.ts — timeout/backoff/재시도 실제 소스.
- **[S15]** 공식 Python 소스: https://github.com/typesafe-ai/typesafe-sdk-python/blob/main/pyproject.toml ; https://github.com/typesafe-ai/typesafe-sdk-python/blob/main/src/typesafe_sdk/constants.py ; https://github.com/typesafe-ai/typesafe-sdk-python/blob/main/src/typesafe_sdk/_version.py — 배포 버전·요구사항·기본값.
- **[S16]** 소개 블로그: https://typesafe.ai/blog/introducing-system-one-models-and-jev — 2026-09-15 본문, 속도/비용 주장과 한계·West Coast·Doom.
- **[S17]** AI primer: https://docs.typesafe.ai/introduction/machine-learning-primer.md — RLCD와 calibration 정의.
- **[S18]** Confidence: https://docs.typesafe.ai/confidence.md — probability와 confidence 차이·threshold.
- **[S19]** Function calling cookbook: https://docs.typesafe.ai/cookbooks/function_calling.md — jev-1.12, closed-set dispatcher·54질문·미지원 인수.
- **[S20]** SDK 설치/quickstart 문서(실제 설치 안 함): https://docs.typesafe.ai/sdk.md ; https://docs.typesafe.ai/sdk/python.md ; https://docs.typesafe.ai/sdk/javascript.md .
- **[S21]** Python usage: https://docs.typesafe.ai/sdk/python/usage.md — gateway base URL, logging, typed response.
- **[S22]** 공식 workflow evals: https://evals.typesafe.ai/ — 4 workflow 평균·consensus 기준·reasoning 설정.
- **[S23]** Vercel TypeSafe-compatible API: https://vercel.com/docs/ai-gateway/sdks-and-apis/typesafe — 2026-09-21 수정.
- **[S24]** Vercel Evaluation: https://vercel.com/docs/ai-gateway/modalities/evaluation — 2026-09-22 수정, boolean/evaluate 및 비호환 endpoint.
- **[S25]** Vercel Jev catalog: https://vercel.com/ai-gateway/models/jev — model ID, providers, 32k, input 단가.
- **[S26]** Cloudflare Jev: https://developers.cloudflare.com/ai/models/typesafe/jev/ — AI.run·HTTP input wrapper·ZDR·dashboard 가격 링크.
- **[S27]** Vercel launch: https://vercel.com/changelog/typesafe-ai-jev-now-available-on-ai-gateway — 2026-09-16, AI SDK experimental evaluate·confidence metadata·ZDR 안내.
- **[S28]** TypeSafe Legal 안내: https://docs.typesafe.ai/legal.md — 데이터 처리 문서 링크·enterprise ZDR 안내. 상세 약관 전체를 검토한 법률 의견은 아님.
- **[S29]** 문서 탐색 인덱스: https://docs.typesafe.ai/llms.txt ; 공식 홈페이지: https://typesafe.ai/ .
- **[S30]** SDK changelogs: https://docs.typesafe.ai/sdk/javascript/changelog.md ; https://docs.typesafe.ai/sdk/python/changelog.md — JS 0.6.0 및 Python 0.7.1 판본 근거.
- **[S31]** Fan-out: https://docs.typesafe.ai/patterns/fan-out.md — speculative 질문을 병렬 평가한 뒤 코드가 관련 결과 채택.
- **[S32]** Vercel models/providers: https://vercel.com/docs/ai-gateway/models-and-providers — provider별 성능·요금 차이와 모델/라우팅 메타데이터 개념.

## 11. 최종 판단의 경계

공개 자료와 코드가 입증하는 것은 **텍스트 상태를 typed decision으로 바꾸는 원격 모델 인터페이스와 공식 공급사 실험 사례**다. CHOOGuard에서 시민 수십 명의 생활·협동·한국어 대화·사건 대응이 요구 수준을 만족한다는 사실까지 입증하지 않는다. 반대로 현재 로컬 어댑터가 Noul만 지원한다는 사정도 JEV 전체가 NPC 정책에 부적합하다는 근거가 아니다.

다음 의사결정에 필요한 구분은 명확하다: **JEV=좁은 의미 판단; 게임 코드=상태·권한·실행·안전 불변조건; 필요할 때만 생성 모델=새 대화/서술; 외부 memory/learner=장기 기억과 학습 실험.** 이 구성이 사용자가 원하는 1인 플레이어+AI 동료·시민 세계에 맞는지는 위 반례와 실제 플레이를 통해 판단한다. 본 보고서는 그 실험을 위한 1차자료 근거이며 구현 승인이나 실측 성능 보증이 아니다.
