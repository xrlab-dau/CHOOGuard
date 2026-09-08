---
description: 기술 조사 하네스 실행 (Pydantic AI Harness Researcher + Exa) 후 구조화 기록을 docs/research/에 저장
argument-hint: <공개 기술 주제> [--slug <slug>] [--model provider:model] [--deep]
---
다음 주제를 choo guard 기술 조사 하네스로 조사한다: $ARGUMENTS

절차:
1. 주제가 PUBLIC_SYNTHETIC 등급의 공개 기술 주제인지 확인한다. 철도 촬영 자료, 재구성 지오메트리, KORAIL 내부 자료, 개인정보가 섞여 있으면 실행하지 않고 이유를 말한다.
2. `cd tools/research && uv run research.py "$ARGUMENTS"` 를 실행한다. `--model`, `--slug`, `--deep` 인자가 있으면 그대로 넘긴다.
3. 실행 결과 `docs/research/<날짜>-<slug>.md` 를 열어 각 주장에 근거 URL·조회일·제한·상충 자료·라이선스가 채워졌는지 확인한다. 비어 있는 열이 있으면 `deep_search_exa` 도구나 `/exa-deep-search` 로 보강하고 파일을 갱신한다.
4. 조사 결과가 기존 문서(`docs/choo-guard-ai-native-pipeline-v1.md` §3 도구 기준, ADR)와 충돌하면 충돌 항목을 별도 목록으로 보고한다. 문서를 자동으로 고치지 않는다.
5. 마지막에 주장 수, 상충 자료 수, 미확인 라이선스 수를 한 줄로 요약한다.
