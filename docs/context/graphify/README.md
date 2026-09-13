# Graphify 코드·작업 문맥

PM 작업 지시의 정본은 [work-graph.json](../work-graph.json)과 [작업 packet](../work-orders/README.md)입니다. 이 그래프는 코드 탐색용 파생물이며 작업 배정·실제 claim·입력 수용을 승인하지 않습니다. 개인 사전할당이나 학교 PC 제한은 없습니다.

## 팀원 LLM의 시작점

```sh
node scripts/context/task_context.mjs brief --issue 150 --phase candidate
node scripts/context/task_context.mjs brief --issue 150 --phase candidate --section sources
node scripts/context/task_context.mjs brief --issue 150 --phase candidate --section inputs
node scripts/context/task_context.mjs brief --issue 150 --phase candidate --section writes
node scripts/context/task_context.mjs brief --issue 150 --phase candidate --section checks
python3 scripts/context/graphify_context.py query --issue 150 --phase candidate --symbol RuntimeMetricAccumulator --max-chars 32000
```

기본 작업 brief는 해당 packet만 읽습니다. Graphify adapter query는 로컬 graph를 읽되 해당 issue/phase에 선언된 문맥만 반환합니다. `--symbol`은 그 단계에 연결된 소스 안에서 심벌을 명시적으로 좁힙니다. `complete`는 **선택한 조회 범위**의 반환 완전성일 뿐 전체 작업 계약/코드의 완전성이 아닙니다. 예산 초과 시 내용을 조용히 자르지 않고 `complete:false`, 필요한 글자 수와 다음 명령을 반환합니다. 전체 그래프를 LLM에 붙이지 않습니다.

## 출처와 관계의 의미

- [source-manifest.json](source-manifest.json): 고정 commit/path/SHA-256으로 선택한 코드 65개. 전체 저장소를 추출한 것이 아닙니다.
- [graph.json](graph.json): 실제 Graphify AST와 명시적 작업/phase/artifact/context 관계의 결정론적 결합.
- [coverage.json](coverage.json): 실제 노드·관계·소스 수, 미추출 파일과 원시 dangling 관계 수.
- 소스 ref `bdcb714…`는 PR #153의 source-only 전달이고 `3861eab…`는 PR #148 병합 기준의 context 도구입니다. 현재 변경 도구 자체의 AST를 자동 포함했다고 주장하지 않습니다.
- `Source`는 ref/digest가 있는 파일, `CodeSymbol`은 그 파일에서 추출한 심벌입니다. `ExternalSymbol`은 정의가 없는 외부 심벌, `UnresolvedReference`는 원시 AST가 endpoint 정의를 남기지 않은 참조입니다. 둘 다 검증된 정의·실행 증거가 아닙니다.
- `WorkPhase`는 prepare/candidate/accept, `Artifact`는 선언된 산출물 계약입니다. `produces/requires_artifact/one_of`는 계약 의미를 보존하며 실제 완료 여부를 계산하지 않습니다. OR는 선택 대안이지 전체 AND가 아닙니다.
- `DocumentReference`는 작업 pointer와 Decision/Evidence의 문서 경로를 연결합니다. 문서 bytes를 읽거나 검증한 `Source`가 아니며, 같은 경로여도 ref/digest/selector가 다르면 같은 근거나 현행 승인으로 취급하지 않습니다. attribution edge에 기존 digest/anchor를 보존합니다.
- `Decision/Constraint/Evidence`는 출처 레코드의 포인터입니다. 해당 phase의 문서에 직접 귀속된 기록만 조회에 포함하고 superseded 기록과 freshness 미확인은 현행 승인으로 재활용하지 않습니다.
- 선택된 코드 심벌의 직접 `ExternalSymbol/UnresolvedReference` 이웃을 조회에 남깁니다. 일반 호출 그래프와 선행 이슈 문맥까지 재귀 확장하지 않으며 `scopeLimits`에 범위를 표시합니다.
- Requirement 15개는 정본의 명시적 `implements` 16개로 기존 작업 15개에 연결합니다. 양쪽 역색인은 동일한 관계 집합이어야 하며 배열에서 별도 `addresses_requirement`를 합성하지 않습니다. `F-SOURCE`는 #10의 근거 적용 범위 계약과 #60의 사건 근거에 각각 연결하며 #120 소스코드 게시와 혼동하지 않습니다. 자식 작업으로 자동 상속하지 않습니다.
- 정의·추적표는 미게시 `docs/choo-guard-foundation-multiplayer.md` snapshot에서 직접 대조했습니다. SHA-256 `29bdf9230d328bdb6611434ee9e885fc8ea012f8cfb64463b3b8a97286a1b85b`, `availability:local_snapshot`, `ref:null`이며 필요한 인용만 정본에 내장합니다. 과거 manifest hash를 덮어쓰거나 로컬 원문을 읽을 새 자격을 부여하지 않습니다. 고정 G `83f6a8818b304ee7ebf480e5d481681e56a87a29`의 작업 목표·명시 selector도 정확한 path/digest와 함께 내장합니다.
- 원래 `reviews/foundation-map.json`과 원생성기는 미복구이며 `legacySource`의 `unavailable`로 보존합니다. 이번 작업은 정의·추적표·현재 작업 목표에 근거한 의미 대응 재구성이지 원생성기의 복구가 아닙니다.
- `coverage.json`은 3,975노드·10,403관계, 요구 매핑 16개·미연결 0개를 기록합니다. `context`/출처 간선은 bound 판정에서 제외합니다. `heldRequirementMappings:2`와 `heldRequirements`는 F-ART-NATIVE/#106, F-AAA/#107의 `historical_on_hold`를 별도로 나타냅니다. **unbound=0은 실행 가능·요구 구현 완료·Foundation 수용 완료가 아닙니다.** 현재 정책 포인터와 #106/#107 중단·보류를 유지합니다.
- `node scripts/context/task_context.mjs brief --issue 60 --section requirements`는 해당 packet만으로 정의·근거·한계를 반환합니다. 기본 brief는 코드·상태·후속 조회만 보여줍니다. Graphify `query --issue 60 --phase accept`도 선택한 이슈의 요구 근거만 포함하며, 공유 F-SOURCE의 #10 근거·하위 작업·선행 전체는 확장하지 않습니다.
- 같은 노드 쌍의 여러 관계를 보존합니다. Graphify 자체의 양방향 BFS/DFS 또는 centrality를 PM의 선행순서·우선순위·병렬 허가로 해석하지 않습니다.

## 재현

이번 요구 매핑 갱신은 기존 AST가 이전 graph 전체를 재현하고, manifest/coverage provenance와 corpus 65개 digest가 모두 일치함을 확인한 뒤 **adapter build만** 실행했습니다. 재추출·추가 설치·LLM API 호출은 없었습니다. 아래 전체 초기 구축 절차는 기존 재현 참고이며 새 설치·추출 승인으로 해석하지 않습니다.

[도구 manifest](../../../scripts/context/graphify/tool.json)와 버전 고정 requirements를 사용합니다. 관측 환경은 Python 3.12.11이며 Python ≥3.10에서 호환성을 별도로 확인합니다. 설치는 별도 가상환경에 한정하고 사용자 전역 설정·provider·hook은 수정하지 않습니다.

```sh
python3 -m venv "$GRAPHIFY_ENV"
"$GRAPHIFY_ENV/bin/python" -m pip download --no-deps --only-binary=:all: graphifyy==0.9.61 -d "$GRAPHIFY_WHEELS"
# tool.json의 wheelSha256와 다운로드 bytes를 대조한 뒤 설치합니다.
"$GRAPHIFY_ENV/bin/python" -m pip install "$GRAPHIFY_WHEELS/graphifyy-0.9.61-py3-none-any.whl" -r scripts/context/graphify/requirements.txt
export PATH="$GRAPHIFY_ENV/bin:$PATH"
export GRAPHIFY_QUERY_LOG_DISABLE=1
python scripts/context/graphify_context.py corpus --destination "$GRAPHIFY_CORPUS"
python scripts/context/graphify_context.py extract --corpus "$GRAPHIFY_CORPUS" --output "$GRAPHIFY_OUTPUT"
python scripts/context/graphify_context.py build --raw "$GRAPHIFY_OUTPUT/graphify-out/graph.json"
python scripts/context/graphify_context.py validate
```

`GRAPHIFY_*` 경로는 사용자가 선택한 저장소 밖 임시 경로이며 corpus/output은 매번 새 경로여야 합니다. Windows에서는 가상환경의 `Scripts/python.exe`와 `Scripts` PATH를 사용합니다. 장소·머신 종류는 배정 조건이 아닙니다. Git에 해당 고정 commit 객체가 없으면 먼저 원격에서 그 ref를 가져와야 하며, 다른 bytes로 대체하지 않습니다.

Graphify raw output에는 절대 경로와 로컬 sidecar가 있을 수 있으므로 게시하지 않습니다. adapter는 필요한 필드만 정제해 저장하며 private 경로·허용 목록 외 소스·symlink 이탈·중복 ID를 거부합니다. JSON 읽기는 bounded이며 출력 graph의 private text를 검사합니다. source 내용은 고정 Git blob에서 가져오고 현재 dirty checkout을 원격 소스로 가장하지 않습니다.

LLM API, 문서/이미지/음성 semantic extraction, 외부 graph DB, watcher, `graphify install --project`는 실행하지 않습니다. Graphify query를 직접 사용할 때도 `GRAPHIFY_QUERY_LOG_DISABLE=1`을 유지합니다. query 결과는 실행 계약을 대체하지 않습니다.

## 수용 한계

구조 검증과 소스 접근 가능성은 새 checkout Unity compile, 실제 음성/WAN, 20-client/60분 부하, Windows/Linux Player, 현장 정확성 또는 Foundation 수용 완료가 아닙니다. #149–#152의 결함 해결도 별도 작업입니다. 실제 Editor·출력·포트의 단일 writer와 현재 claim은 착수 시 검사합니다.

Graphify는 별도 Apache-2.0 및 과거 MIT 고지를 따릅니다. 배포에 포함된 LICENSE/LICENSE-MIT/NOTICE를 [도구 폴더](../../../scripts/context/graphify/)에 보존합니다. 프로젝트 자체에 새 오픈소스 라이선스를 부여하지 않습니다.
