# 팀과 기기 사이에서 이어 읽는 프로젝트 컨텍스트

[프로젝트 그래프](project-context.json)는 결정을 다시 찾고 필요한 파일로 이동하는 지도다. [로컬 인터랙티브 뷰](index.html)는 검색·작업별 필터·기기 선택·관계 탐색을 제공한다. HTML 파일을 브라우저에서 직접 열 수 있으며 서버·외부 스크립트·네트워크 조회가 필요 없다. 생성 당시의 스냅샷이므로 현재 해시는 CLI로 확인한다.

## 시작

저장소 루트에서 설치된 Python 3.10 이상을 사용한다. Windows에서는 같은 명령의 `python3`을 `python` 또는 `py -3`로 바꿀 수 있다. 추가 Python 패키지는 필요 없다.

```sh
python3 scripts/context/context_graph.py validate
python3 scripts/context/context_graph.py brief --topic runtime --machine local
python3 scripts/context/context_graph.py brief --topic art --machine school-pc
python3 scripts/context/context_graph.py brief --topic handoff --machine school-pc
```

출력의 규칙·결정, 변경된 참조, 과거 증거의 범위를 먼저 확인하고 `Read next`의 파일을 읽는다. 새 팀원과 서브에이전트도 같은 진입점을 사용한다. `--history`는 폐기된 결정까지 명시적으로 조회하며, `--json`은 구조화된 읽기 묶음을 출력한다. 다음 작업은 근거 파일과 현재 작업 지시로 정한다.

`validate`의 종료 코드 0은 **그래프 형식과 관계가 유효함**을 뜻한다. `stale`은 참조 파일이 기록 후 변경되었다는 뜻이며 형식 오류와 별개다. `historical_source_match`도 해당 파일들이 과거 시험의 해시와 일치한다는 뜻일 뿐, 지금 시험을 실행했거나 수용되었다는 뜻이 아니다. 그래프는 Unity·현업 승인·현장 효과·외부 작업 권한에 PASS를 부여하지 않는다.

## 출처와 현재성

- 현재 프로젝트 규칙은 AGENTS.md, 구현은 추적 소스·데이터·Editor 생성기에서 확인한다. [결정 출처 요약](accepted-decisions.md)은 최신 사실적 모델링 지시와 폐기된 flat 지시를 구분한다.
- `sources`의 저장소 경로와 SHA-256은 읽은 자료를 식별한다. 경로는 저장소 상대 경로이며 호스트 이름이나 개인 절대 경로가 없다.
- 증거 노드는 당시 영수증과 그 안의 `sourceSha256`으로 적용 범위를 확인한다. 새 소스가 달라지면 과거 시험은 `source_drift`가 된다. 과거 영수증의 `sourcePushed: false` 같은 상태를 현재 전달 상태로 쓰지 않는다.
- 보드/PR 참조는 언제나 날짜가 있는 외부 스냅샷이다. 이 CLI는 GitHub를 조회하지 않으며 `live_status_unknown`을 유지한다. 현재 상태는 필요한 작업 직전에 실제 보드/PR에서 읽는다.
- 원문 대외비 지류 문제정의서는 사용자 확인에 따른 분류만 알려져 있고 직접 대조하지 않았다. 실제 시설·매뉴얼·기관 승인·현장 효과는 별도 미확인 사항이다. 이 미확인은 합성 Foundation 개발 전체를 막는 선행조건이 아니다.
- 그래프에 게시 승인, 설치 권한, 계정 매핑, 민감자료 접근 권한을 보존하지 않는다. 미래 세션이나 다른 팀원의 외부 행동을 자동 승인하지 않는다.

## 검토 후 현재 참조 갱신

먼저 변경된 원문을 읽고 요약·상태·관계를 실제 내용에 맞게 고친다. 그 다음 **검토한 현재 노드만 이름으로 지정**한다.

```sh
python3 scripts/context/context_graph.py refresh --node component.art_scene --reviewed --reason "검토한 모델 생성기와 공개 레퍼런스 변경 반영"
python3 scripts/context/context_graph.py validate
python3 scripts/context/context_graph.py render
python3 -m unittest discover -s scripts/context -p 'test_*.py' -v
```

`--reviewed`는 명령 실행자가 원문을 읽었다는 명시적 기록이며 새 승인이나 독립 검토를 생성하지 않는다. 여러 `--node`를 지정할 수 있다. `refresh`는 현재 참조 해시와 검토 날짜만 갱신한다. 과거 증거, 폐기된 결정, 외부 스냅샷은 갱신 대상에서 거부한다. 새 실행은 새 영수증/노드로 연결하고 기존 증거의 커버리지 해시를 바꾸지 않는다. `render`는 그래프와 현재성 결과를 HTML 안에 담는다.

## 스키마와 관계

[JSON Schema](../../scripts/context/context-graph.schema.json)는 형태를 정의하며 CLI가 사용하는 명시적 부분집합은 `$ref`, `oneOf`, 타입·상수·열거·필수/추가 속성·문자/배열 길이·패턴·날짜다. 표준 라이브러리 검증기에 의미 검사를 추가하여 중복 ID, 끊긴 관계, 폐기 순환, 동일 범위의 복수 활성 결정, 저장소 밖 경로를 거부한다.

| 관계 | 의미 |
|---|---|
| `governed_by` | 적용할 결정/규칙 |
| `implements` | 구성요소가 구현하는 목표/계약; 시험 수용을 뜻하지 않음 |
| `depends_on` | 읽기·구성에 필요한 다른 구성요소 |
| `verified_by` | 해당 소스 버전의 과거 실행 증거 |
| `supersedes` | 동일 결정 범위의 이전 지시를 폐기 |
| `tracked_by` | 현재 상태를 확인할 이슈/보드/PR 위치 |
| `limits_claims` | 실물·절차·효과 등에 관한 주장 제한; 합성 개발 전역 차단 아님 |

노드는 한두 문장과 소수의 근거로 유지한다. 활성 계획·채팅 전체·원시 로그를 그래프에 복제하지 않는다. HTML은 파생 결과이고 JSON·근거 파일이 원본이다.
