# 기술 조사 기록: 에이전틱 도구 체인 (2026-09-06)

- 자료 등급: PUBLIC_SYNTHETIC. 역사·촬영·KORAIL 정보 없음.
- 방법: Exa Search API(`/search`, type=auto, 질의당 3건)를 curl로 호출하고, 로컬에 설치한 패키지의 README·package.json·dist-info를 직접 읽어 교차 확인했다. 원시 결과는 `2026-09-06-agentic-toolchain-research.json`.
- 한계: Pydantic AI Harness `Researcher`는 로컬에 LLM API 키가 없어 실행하지 않았다(`tools/research/research.py --dry-run`만 통과). 현재 하네스와 프롬프트는 전송·모델·출력 통제가 미완료여서 실행 보류다. 키의 존재는 실행 승인이 아니다. 학교 PC 조사·Exa는 기본 비허용이다. Exa 결과 본문 발췌는 보관하지 않았다.
- 조회일: 모두 2026-09-06.

## 기록 유형과 한계

이 Markdown은 사람이 읽을 주장 표이고 JSON은 원시 검색 목록이다. `ResearchRecord` 실행 결과가 아니다. 과거 직접 curl 조회 사실은 보존하지만 전송·공개 승인 영수증이 있었던 것으로 소급하지 않는다. 본문 발췌를 보관하지 않은 주장은 공급망 승인 근거로 사용할 수 없다. 라이선스 열은 메타데이터 관측이며 원문·고지 의무의 판정이 아니다. 구조화된 정정 요약은 `2026-09-06-agentic-toolchain-claims.json`에 둔다.

## 주장·근거·한계

| ID | 주장 | 근거 URL | 로컬 교차 확인 | 한계·상충 | 라이선스 | 확신 |
|---|---|---|---|---|---|---|
| C-01 | Pi는 프로젝트 `.pi/settings.json`의 `packages`를 프로젝트 신뢰 후 시작 시 자동 설치하며, 설치 결과는 `.pi/npm/`에 놓인다. | https://github.com/earendil-works/pi/blob/main/packages/coding-agent/docs/packages.md | 로컬 설치본 packages.md와 과거 Pi 0.85.0 버전 출력 관측 | 업스트림 main 문서는 설치본보다 새로울 수 있다. | MIT | 높음 |
| C-02 | Pi 프로젝트 프롬프트(`.pi/prompts/*.md`)는 `$ARGUMENTS`를 받는 슬래시 명령이 되고, 스킬은 `.pi/skills/`·`.agents/skills/`에서 읽힌다. | https://github.com/earendil-works/pi/blob/main/packages/coding-agent/README.md | 로컬 `.pi/prompts/research.md`, `.agents/skills/bmad-*` 인식 | Pi에는 내장 샌드박스·승인창이 없어 패키지 소스 검토가 필요하다. | MIT | 높음 |
| C-03 | pi-agents는 YAML 워크플로(`agent/sequence/parallel/map/loop/while/switch/value/workflow` 노드, `json` 스키마, 예산 maxAgents 50·maxParallelism 8·maxIterations 10·maxDepth 5)를 지원하고 각 노드를 별도 Pi RPC 하위 프로세스로 실행한다. | https://pi.dev/packages/pi-agents , https://www.npmjs.com/package/pi-agents | 로컬 `.pi/npm/node_modules/pi-agents/README.md`(0.21.0), `validateFlow`로 `.pi/workflows/*.yaml` 3건 검증 통과(2026-09-06) | Exa가 반환한 jsdelivr README는 0.16.1로 구버전. 기능 차이는 로컬 README 기준. | Apache-2.0 | 높음 |
| C-04 | pi-subagents 0.65.1은 분리 세션 서브에이전트 실행을 제공한다. | https://pi.dev/packages/pi-subagents | 로컬 package.json | 워크플로 그래프는 pi-agents가 담당하므로 pi-subagents는 단발 위임용으로만 쓴다. | MIT | 중간 |
| C-05 | pi-exa 0.6.1은 `deep_search_exa` 도구와 `/exa-*` 명령을 제공하며 `EXA_API_KEY`를 쓴다. | https://pi.dev/packages/pi-exa | 로컬 package.json, 명령 목록 | 외부 전송 경로이므로 PUBLIC_SYNTHETIC만 입력 가능. | MIT | 높음 |
| C-06 | Spec Kit Pi 통합 파일이 로컬에 존재한다. 로컬 `create-new-feature.sh`는 feature 디렉터리·spec·상태를 생성하며 Git 브랜치를 생성하지 않는다. | https://github.com/github/spec-kit , https://github.github.com/spec-kit/reference/agentic-sdd.html | `.specify/scripts/bash/create-new-feature.sh` 전체 정적 읽기. mkdir, spec 출력, `_persist_feature_json`이 있고 checkout/switch 호출은 없다. | BRANCH_NAME은 출력 필드이며 Git 생성 증거가 아니다. 이 로컬 파일과 upstream v1.0.4의 byte 일치·수정 이력은 미검증이다. | MIT 메타데이터, 원문 검토 미완료 | 높음(로컬 정적 동작) |
| C-07 | BMAD Method 6.12.0은 `.agents/skills/bmad-*` 스킬 29개와 `_bmad/` 설정, `_bmad-output/` 산출 경로를 쓴다. | https://docs.bmad-method.org/reference/workflow-map/ , https://github.com/bmad-code-org/bmad-method | 로컬 `.agents/skills/`, `_bmad/config.toml` | GitHub SPDX가 NOASSERTION이라 라이선스는 저장소 LICENSE 원문으로 수동 판정해야 한다(M0-03). | NOASSERTION(수동 검토) | 중간 |
| C-08 | Pydantic AI Harness 0.29.0은 `Researcher(instructions, subagents)`와 `ExaSearch(num_results, include_deep_search, include_domains, ...)` 능력을 제공하고 `Agent(model, output_type, capabilities=[...])`에 붙인다. | https://pydantic.dev/docs/ai/harness/researcher/ , https://pydantic.dev/docs/ai/harness/exa-search/ , https://pydantic.dev/articles/harness-exa | 로컬 `.venv` 시그니처 확인, `research.py --dry-run` 통과 | LLM 키 없이 실제 조사는 미실행. 실행 시 모델 비용 발생. | MIT | 중간 |
| C-09 | Exa `/search`는 `type: auto`, `numResults`, `contents.text.maxCharacters`를 받고 `results[].title/url/publishedDate/text`를 돌려준다. | https://exa.ai/docs/reference/search , https://exa.ai/docs/reference/search-api-guide | 원시 로그는 질의 항목 10개·결과 URL 30개다. HTTP 호출·재시도 수와 성공 응답 영수증은 별도 확인되지 않았다 | 유료 API. 질의 문자열이 외부로 나간다. | 상용 약관 | 높음 |
| C-10 | CoplayDev unity-mcp v10.2.0은 이전 릴리스 조회에서 관측한 고정 후보다. 지속적인 최신 버전 주장이 아니다. | https://github.com/CoplayDev/unity-mcp/releases , https://coplaydev.github.io/unity-mcp/releases | 이전 `gh api` 관측 태그 v10.2.0, v10.1.2, v10.1.0, v10.0.2 | 로컬 미설치·미실행. M1-03 공급망 검토와 M2-01 이후 학교 PC M1-04에서 검증. | MIT 메타데이터, 원문 검토 미완료 | 높음(과거 존재 관측), 미검증(동작) |
| C-11 | OpenSpec은 변경 제안(proposal) 중심의 경량 SDD 프레임워크로 Spec Kit의 대안이다. | https://openspec.dev/ , https://openspec.dev/docs/opsx | 미설치 | 파이프라인 v1.2가 Spec Kit을 채택했으므로 대안 기록만. | MIT | 중간 |
| C-12 | GitHub 규칙(rulesets)은 필수 상태 검사, 승인 수, CODEOWNER 검토, 병합 방식 제한을 지원하고 REST로 조회할 수 있다. | https://docs.github.com/en/rest/repos/rules , https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets | 본 저장소 규칙 `gh api` 조회(거버넌스 감사 문서) | 없음 | 문서 | 높음 |
| C-13 | 생성자-비평자(generator-critic) 루프는 LangGraph 커뮤니티에서 자기 수정 패턴으로 통용된다. 동일 구조를 pi-agents `while` 노드로 구현할 수 있다. | https://activewizards.com/blog/a-deep-dive-into-langgraph-for-self-correcting-ai-agents/ , https://github.com/dhar174/langgraph_system_generator/blob/main/docs/patterns.md | `.pi/workflows/plan-review.yaml` | 근거가 블로그·개인 저장소라 권위가 낮다. 공식 LangGraph 문서로 보강 필요. | 각 저장소 | 낮음 |

## 미결 질문

- Pi 0.85.0에서 `openai-codex` 제공자의 OAuth 로그인 절차가 학교 PC(공유 계정 가능성)에서 안전한가? → D-08.
- unity-mcp v10.2.0의 UPM 설치 경로 문자열(`?path=/MCPForUnity`)이 v10에서도 같은가? → M1-04 착수 시 릴리스 노트로 확인.
- BMAD LICENSE 원문 확인 → M0-03.
