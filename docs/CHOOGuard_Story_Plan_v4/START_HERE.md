# CHOOGuard · 실행 스토리·단기 계획 v4

**플랫폼:** Unity native PC · **개발:** 빈 저장소 · **이번 산출물:** 계획·온톨로지·조회/검사 도구

운영 시뮬레이션이 본체다. 새 CS 에픽 11개/상위 작업 48개를 실행 story 109개로 나눴다. 원래 제품 요구 91개·무료 자산 25개·기술/자료 기록은 `basis/v3`에 보존했다. 과거 저장소 코드나 에픽을 복원하지 않는다.

## 지금 시작할 한 작업
**`CS-BOOT.01.01` — 새 Unity 프로젝트에서 정적 PC 부팅을 재현한다.**

```sh
python -m pip install -r requirements.txt
python tools/plan.py validate
python tools/plan.py brief CS-BOOT.01.01 --phase prepare --profile fixture
```

`GLOBAL_CONTRACT.md`와 packet의 `nextReads`를 읽고 해당 입력·작업 경계를 확인한다. 같은 CLI에 `--phase candidate`를 주면 구현에 필요한 실제 입력의 부재를 확인할 수 있다. 조회 성공은 실행 권한이 아니다.

## 최소 읽기
1. `SHORT_TERM_PLAN.md`: 첫 28개 story와 4개 수용창.
2. `GLOBAL_CONTRACT.md`: 제품·쓰기·시간·검수 불변식.
3. `stories/<storyId>.md` 또는 `python tools/plan.py brief <storyId>`.
4. 해당 `basis/v3/specs`와 sourceRefs만 추가로 읽는다. 전체 109개 문서를 한 번에 LLM에 넣지 않는다.

## 다음 작업·병렬 확인
```sh
python tools/plan.py next --phase prepare --profile fixture
python tools/plan.py order
python tools/plan.py parallel CS-BOOT.01.01 CS-PROOF.01.01
python -m unittest discover -s tests -v
python tools/render_plan.py --check
```

`order`는 계획 순서이며 실제 Ready 대기열이 아니다. `next`는 제출된 입력·검토 기록을 검사한 추천이다. 둘 다 원격 이슈·코드·락을 바꾸지 않는다. 기본 WIP는 작성 1개/리뷰대기 1개다.

## 어떤 것이 정본인가
`plan.json`: 작업 계약. `ontology/work-graph.jsonld`: 관계 탐색용 파생 그래프. `state/progress.json`: 현재 **비어 있는** 실제 실행·검토 장부. `review/`: 이 계획의 검사 결과이며 제품 실행 증거가 아니다.

## 이번에 실행하지 않은 것
Unity/C# 컴파일·Player·physics worker·실제 사용자·기관 검수는 NOT_RUN이다. 독립 개발 LLM의 생산성 비교도 실행하지 않았다. Python 문서·JSON Schema·관계·합성 receipt 검사를 제품 AAA로 승격하지 않는다. SHACL 파일은 제공하지만 엔진 실행은 환경 제한으로 NOT_RUN이며 RDF 문법 검사와 구분한다.
