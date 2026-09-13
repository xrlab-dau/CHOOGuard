# 13구역 자료 범위·제작 경계·연결 portal 초안

2026-09-13 개정(초안 2026-09-12, 착수 commit 8bb8077), [#70](https://github.com/xrlab-dau/CHOOGuard/issues/70). 기준 데이터는 [`foundation/world/zone-boundaries.json`](../../foundation/world/zone-boundaries.json)이며 이 문서는 요약이다.

> **이 후보는 `isolated_proposal` 이며 `canonicalWriteAllowed: false` 다.** 스스로를 accepted/final/complete/merged 로 표기하지 않는다. **실측 배치·안전 경계·완료된 재구성이 아니다.** 좌표·충돌·연결 변경은 PM 검토 후에만 적용한다. 아래 검증 통과는 문서 정합성일 뿐 시설 일치·안전 수용·제품 보증이 아니다.

[`facility-coverage.json`](../../foundation/world/facility-coverage.json)의 13구역·12연결·출처(P01~P09)는 **읽기만** 했고 그대로 둔다. 구역 ID를 키로 다음을 덧붙인다: 입력별 허용 이용 범위, 측정 근거 구분, 자료 공백, 제작 이슈, 인접 구역, 번들 경계, portal 초안. coverage 파일이 바뀌면 sha256 결속 불일치로 검증이 실패하므로 다시 읽고 갱신한다.

후보 쓰기 범위는 이 4경로뿐이다: `foundation/world/zone-boundaries.json`, `scripts/art/validate_zone_boundaries.py`, `scripts/art/test_zone_boundaries.py`, `docs/art/zone-boundaries.md`. `facility-coverage.json`, `foundation/README.md` 등 범위 밖 파일은 만들지도 고치지도 않았다.

## 세 번들과 제작 책임

| 번들 | 구역 | 모듈 제작 | 조립 | anchor portal |
|---|---|---|---|---|
| `rail_train` 열차·철도 | 일반철도 차량, 선로, 철도 승강장 | #78 | #80 | 승강장↔2층 맞이방 |
| `station_forecourt` 역사·광장 | 터미널 공용구역, 2층 맞이방, 1층 맞이방, 매표, 광장·유라시아플랫폼 | #76 #77 | #79 | 승강장↔2층 맞이방 |
| `connector_metro` 연결부·지하철 | 지하연결통로, 지하상가, 도시철도 대합실·승강장·차량 | #76 #77 #78 | #81 | 터미널↔지하연결통로 |

각 번들은 **portal 평면까지만** 제작한다. 번들을 넘는 portal 3개(승강장↔2층 맞이방, 터미널↔지하연결통로, 광장↔지하연결통로)의 월드 변환은 PM 통합(#82)에서 확정한다. 같은 `.blend`/scene 동시 편집을 피하기 위해 번들 경계가 곧 작업 분담 경계다.

## 구역별 요약

측정 구분: **공표** = 운영자 공표 수치(프로젝트 측량 아님), **추정** = 기록된 합성 제작값, **없음** = 쓸 수 있는 수치 없음.

| 구역 | 입력(허용 이용) | 측정 | 핵심 공백 |
|---|---|---|---|
| 일반철도 차량 | V04 편집 여정 영상(검수 전 후보) | 없음 | 차량 형식 미선정, 치수 없음, 선로와의 연결 미정의 |
| 선로 | 없음 | 없음 | 배선도·궤간·승강장 간격 출처 없음 |
| 철도 승강장 | V04(검수 전 후보) | 없음 | 수·길이·폭·높이, 맞이방 연결 위치 |
| 터미널 공용구역 | P01(층 소속), P02(2014 1층 배치, 변경금지) | 없음 | 맞이방과의 경계 정의, 현행 평면 |
| 2층 맞이방 | 사진 A/B(외형·비계량 표면), R04·R05(외형), P01 | 없음 | 준공 평면·천장고·기둥 간격. A/B는 상부 한 구간뿐, 합성 추정치를 만들 수치 입력이 없음 |
| 1층 맞이방 | P02(과거 배치), P01 | 없음 | 현행 형상·축척 |
| 매표 구역 | R06(외형), P01 | 없음 | 층·연결 위치·폭 |
| 광장·유라시아플랫폼 | P05·P06·P07·P09(연결 관계) | 없음 | 역사와의 지상 연결 미정의, 치수 |
| 지하연결통로 | P04(99.6×8.0m 공표·연결), P09, V01(비계량 표면) | 공표 | 끝점·경사·층고, Blender/Unity 미전달 |
| 지하상가 | P04·P09(연결 관계) | 없음 | 점포·폭·분기, 운영 경계 |
| 도시철도 대합실 | P03(연결), P09, F04(역 전체 수량) | 없음 | 출구 1~7 vs 8·10번 불일치, 치수 |
| 도시철도 승강장 | P03, F03(대피마스크 수량), F05(안전문 설치일) | 없음 | 길이·폭·선로 간격, 보관함 좌표 |
| 도시철도 차량 | F02(설비 도해 외형) | 없음 | 차량 형식, #78 범위 포함 여부 |

지하연결통로의 99.6×8.0m는 **그 통로에만** 적용한다. 맞이방·승강장의 스케일 기준으로 쓰지 않는다(coverage `not_scale_anchor_for`). 이 값은 운영자 공표값이며 프로젝트 측량이 아니다.

**측정 근거 없는 값은 확정하지 않는다.** 구역에는 폭·높이·층고 필드 자체를 두지 않았고, portal의 `opening_width_m`·`clear_height_m`는 12개 전부 `null` 이다. 검사기는 coverage에 운영자 공표 개구값이 없는 portal 치수가 채워지면 거부한다.

## 좌표·portal 규약 초안

- 단위 metres, Unity +Y up +Z forward — [`station-twin-profile.json`](../../foundation/world/station-twin-profile.json)과 같다. Blender 축 변환·pivot은 #74에서 정한다.
- 구역 원점: y=0은 그 구역의 주 보행 바닥면, x/z는 번들 anchor portal 평면의 중심, +Z는 portal을 지나 구역 안쪽.
- 높이: 수치 대신 상대 층 가정만 둔다(예: 2층 맞이방 `above_ground_2f`, 도시철도 승강장 `underground_below_concourse`). 층고·접속고·portal 폭은 출처가 생길 때까지 `null`이다.
- portal ID는 `portal.<from>--<to>`로 coverage connections의 방향을 그대로 쓴다. 기존 합성 맵의 `StationPortal` siteId(central/west/east)와 다른 이름공간이다.

12개 portal의 소유·수직 이동 여부·메모는 JSON의 `portals`에 있다. 수직 연결로 표시한 것은 1층↔2층 맞이방, 터미널↔지하연결통로, 광장↔지하연결통로, 도시철도 대합실↔승강장이며 모두 위치 미검증이다.

## PM 질문 7개 처리표

각 항목은 답변 / 미결 / 후속 issue 중 하나로 분류한다. **결정 출처 없는 항목을 임의로 답변 처리하지 않는다.** 현재 답변 0건, 미결(open) 4건, 후속 issue 3건이다.

| ID | 질문 | 분류 | 결정 출처 / 미결 사유 / 후속 issue | 인계 |
|---|---|---|---|---|
| Q1 | #82 로컬 구현(미푸시)의 구역/portal ID와 이 초안의 명명·방향 대조 | 후속 issue | **#82** — 미푸시 로컬 구현은 이 워크트리에서 볼 수 없다 | #82 담당이 ID 목록을 공개하면 1:1 대조. 대조 전 이 명명을 확정으로 쓰지 않음 |
| Q2 | 일반철도 차량↔선로 연결을 coverage에 추가할지 | 미결 | coverage connections 변경은 shared coverage 직접 수정이며 이 후보 쓰기 범위 밖 — PM 결정 필요 | 결정 시 `rail_train` 소비자가 선로-차량 연결을 씀 |
| Q3 | 광장·유라시아플랫폼↔역사 건물의 지상 연결을 추가할지 | 미결 | Q2와 같이 coverage 직접 수정 필요. 공개 근거(P05~P09)는 상대 구성만 주고 지상 출입구 위치를 주지 않음 | 결정 시 `station_forecourt` 번들 경계가 바뀌어 #76·#79 재확인 |
| Q4 | 도시철도 차량 제작을 #78 범위에 포함할지 | 후속 issue | **#78** — 현재 미선정·치수 없음·provisional | #78에서 포함 여부 확정, 포함 시 차량 형식 근거 추가 확보 |
| Q5 | 번들을 넘는 portal 3개의 월드 변환과 anchor 방향 | 후속 issue | **#82** — coverage connections 12건 전부 `world_transform: null`, `world_transform_verified: false` | #82 통합에서 world transform 확정 |
| Q6 | `rail_terminal_public`의 범위(맞이방·1층과의 경계) | 미결 | 공개 근거는 층 소속(P01)·2014년 배치(P02)뿐이라 경계를 그을 평면·치수가 없음 | 정의되면 터미널↔2층·1층 맞이방 portal 소유가 바뀔 수 있음 |
| Q7 | V02·V03 영상의 구역 귀속 | 미결 | 영상·이미지 검수는 사람 검수자의 육안 판정이 필요하며 코딩 에이전트가 대신할 수 없음 | 검수 결과가 오면 `unassigned_sources`에서 해당 구역 `inputs`로 옮김 |

## 드리프트·미관측 기록 (H1)

| 항목 | 기록 |
|---|---|
| `sourceRef` | 동결 기준 파일의 `기준 ref`가 **빈 값**이라 대조할 source ref 가 없다. 후보는 워크트리 HEAD `c1a7b78`에서 작성했다. 이 드리프트를 숨기지 않는다. |
| 초안 ↔ HEAD | 4경로의 마지막 변경 커밋은 `8bb8077`이고 후보 base `c1a7b78` 사이에 내용 변경이 없다(`git log --oneline -- <4 paths>` = 1건, 작업트리 clean). 초안 = `8bb8077` 내용. |
| claim holder 브랜치 | `docs/70-zone-coverage-boundaries` 원격 현재 내용은 **익명 비교를 위해 의도적으로 조회하지 않았다.** SOURCE_CONFLICT 로 단정하지 않고 미관측으로 남긴다. |
| 워크오더 | `docs/context/work-orders/070.json`은 이 워크트리에 존재한다. 브리프가 말한 "source ref 워크트리에 없을 수 있음"은 source ref 가 비어 확인할 수 없다. |
| 상류 계약 | `facility-coverage.json`은 읽기만 했다. 후보 작성 중 수정하지 않았다. |

## 검증

```sh
python3 scripts/art/validate_zone_boundaries.py            # 13구역·12연결·3번들·7질문 대조
python3 -m unittest scripts.art.test_zone_boundaries       # 25개 시험(착수 13개 + 수정·추가)
python3 -m unittest discover -s scripts/art -p 'test_*.py' # 다른 art 시험까지 포함(36개)
python3 scripts/art/validate_zone_boundaries.py --data <path>  # 다른 후보 문서를 같은 계약으로 검사
```

### 종료코드 계약

| 코드 | 의미 | 예 |
|---|---|---|
| `0` | `pass` — 모든 규칙을 **평가했고** 위반이 없다 | 현재 4경로 |
| `1` | `fail` — 문서는 잘 형성됐으나 규칙 위반 | coverage 해시 변경, portal 누락, 근거 없는 portal 치수, 답변 없는 질문의 임의 확정 |
| `2` | `not_run` — 입력을 **평가할 수 없다** | 없는 파일, 비 UTF-8, 잘린/깨진 JSON, 최상위가 객체가 아님(배열·null·문자열), 문서 수준 리스트 필드의 타입 불일치 |

`not_run`은 규칙 위반이 **아니다.** 평가 불가 입력을 `pass`로 보고하지 않으며, 위반과 같은 코드로 뭉개지도 않는다. 내부 항목 타입 오류(예: 구역 항목이 객체가 아님)는 문서 수준 규칙을 평가할 수 있으므로 `fail`(`1`)이다.

### 검사 범위

coverage sha256 결속, `isolated_proposal`/`canonical_write_allowed:false`/자기 승격 금지, provenance(base commit·워크오더 경로·드리프트 항목), 13구역이 정확히 한 번씩 한 번들에 속함, 출처 참조·허용 이용 값, 미사용 출처, 인접 구역 = coverage 연결, portal = coverage 연결 1:1, portal ID 형식, 번들 경계 portal의 PM 통합 소유, 공표 수치는 해당 구역 제약에만, 미검증 구역·portal의 provisional 유지와 gaps, portal 치수 `null`(공표 개구값 없을 때), 합성 추정치의 기록 요구, PM 질문 7개 분류.

### 실행하지 않은 것 (NOT_RUN)

| 항목 | 이유 |
|---|---|
| claim holder 원격 브랜치 대조 | 익명 비교 유지 — 조회하지 않음 |
| #82 로컬 구현 ID 대조 | 구현이 미푸시라 이 워크트리에서 접근 불가 |
| V02·V03 구역 귀속 | 사람 육안 검수 필요 |
| 실측 배치·시설 일치·안전 수용 검증 | 이 후보 범위 밖(증거 없음) |
| GitHub 이슈/프로젝트 상태 재조회 | 이 후보는 저장소 파일만 다룸 |

**검사 통과는 정책 PASS 가 아니다.** 위 시험은 문서 정합성만 단언하며, PM 수용·실측·안전 판단을 대신하지 않는다.
