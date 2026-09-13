# TEAM-04 운영·사건·직무 사용자 흐름 수용 대조표

2026-09-13 작성 · 이슈 [#72](https://github.com/xrlab-dau/CHOOGuard/issues/72) · candidate 후보

평상시 운영부터 관측·보고·인솔·집결·복기·복구까지의 정상 및 실패 기대값을 fixture와 화면·행동·검증에 대응시킨 표다. 기대값 명세이며 **실행 결과나 실기 검증이 아니다.** 완료 판정은 PM이 한다.

fixture는 [`foundation/team-fixtures/TEAM-04/user-flows.json`](../../../foundation/team-fixtures/TEAM-04/user-flows.json)이다.

## 1. 범위 고정

| 항목 | 값 |
|---|---|
| 대상 범위 | `legacy-single-operator` — 실제 조작자 1명, 동일 외형 NPC 6명, 사건 2유형 × 위치 3곳 |
| 섞지 않는 것 | FMP 20클라이언트·NPC100·동시사건2 목표. 이 표의 어떤 항목도 그 수용 근거가 아니다 |
| 면책 | `KORAIL 검증 전 예시`. 공식 직무·SOP·점수를 확정하지 않는다 |

## 2. 결속한 입력

| 경로 | 용도 | sha256 |
|---|---|---|
| `docs/choo-guard-open-world-training.md` | 상시 현장 흐름과 금지 사항 | `190b73d47e5eba8add586c3a4590679e7b1d7d414ba446453cb0ed1979015f4e` |
| `foundation/scenarios/foundation-demo.json` | 임시 5직무·행동·Anchor | `bd7dfa87529f2d3e94f517e28d23929ed693129406f6f1823090bff73fef3969` |
| `foundation/world/station-twin-profile.json` | 통로·설비·순회 지점·시간 | `d727285e50f9ab8c1e13b6a84f32a6934d1fee1e21ab4f7b0d621b5a71a0f9e2` |
| `foundation/anchors/synthetic-room.json` | Anchor 집합 | `e6051a3d9131e0e920c4976e197033d5c541f9452d4b4c1f3733d07ad593b5c5` |

기준 커밋 `e480e69`. 네 파일은 읽기만 했다.

## 3. 흐름별 대조

| 흐름 | 갈래 | 직무 | 화면·입력 | 기대 결과 |
|---|---|---|---|---|
| F01 정상 완주 | `normal` | role-01 | 순회 → 상황 패널 → 주 무전 → 경로 단말 → 직접 인솔 → 집결 단말 | `completed`. 복구 15초 후 같은 공간에서 다음 사건 대기 |
| F02 일시정지 복귀 | `pause` | role-01 | Esc 대응 기록 | `resumed`. 관측·보고 상태 보존, 사건 재추첨 없음 |
| F03 입력 오류 | `input_error` | role-04 | 거리 밖·가림·미등록 객체 | `rejected_input`. 행동 미성립 |
| F04 미관측 대응 | `unobserved` | role-05 | 관측 전 조작·인솔, 통신 이상 시 주 무전 | `rejected_precondition`. 사건 위치·유형 비노출, 보조 단말 경로만 성립 |
| F05 집결 실패 | `assembly_failure` | role-02 | 통로 앞 접근만, 일부만 도착 | `incomplete_assembly`. 미도착 인원을 완료로 세지 않음 |
| F06 재시작 | `restart` | role-03 | 재시작 | `restarted`. 새 근무 ID로 이전 기록 미덮어씀 |

각 흐름은 fixture에서 초기 → 행동 → 관측 → 기대 상태 순으로 단계를 적고 출처를 연결한다.

## 4. 금지 사례

| 사례 | 시도 | 판정 |
|---|---|---|
| N01 | 미관측 상태에서 사건 위치·유형·정답 순서 표시 | `refused_by_design` |
| N02 | 원격 인솔 버튼으로 떨어진 NPC 이동 | `refused_by_design` |
| N03 | NPC의 직무 대행, 공식 점수·이수 판정 표시 | `refused_by_design` |

세 사례 모두 사유와 출처를 fixture에 함께 기록했다.

## 5. Desktop / VR 의미 대조

같은 `roleId`·`actionId`·`targetAnchorId`와 같은 기대 Quest·피드백 의미를 쓴다. 입력 장치의 동일성이 아니라 **기대 결과의 일치**를 본다.

- Desktop: WASD 이동, 마우스 시점, E 상호작용, Esc 일시정지·복기
- VR: **미검증.** 실제 HMD 시험은 이 이슈의 nonGoal이며 #37·#89에서 다룬다. fixture의 `verifiedOnDevice`는 `false`다.

## 6. 검증 결과 (2026-09-13)

fixture를 세계 프로필·시나리오·Anchor와 대조했다. **오류 0건.**

| 검사 | 결과 |
|---|---|
| 여섯 갈래가 각 1건, 중복 없음 | 통과 |
| 각 단계에 초기·행동·관측·기대가 모두 있음 | 통과 |
| `outcome` 코드가 전부 선언됨 | 통과 |
| role/action/anchor 삼중쌍이 시나리오와 일치 | 통과 |
| 설비 anchor와 종류가 세계 프로필과 일치 | 통과 (7종) |
| 통로 3곳·시간 4값·순회 지점 9개가 프로필과 일치 | 통과 |
| 금지 사례 3건에 사유·출처 있음 | 통과 |
| VR이 기기 미검증으로 유지됨 | 통과 |
| 결손 입력이 기록됨 | 5건 기록 |
| 결속한 입력 digest 일치 | 통과 (초안의 불일치 2건을 잡아 수정) |

검사기는 이 이슈의 쓰기 범위 밖이라 저장소에 넣지 않았다. 결속한 입력만으로 재현할 수 있다.

초안에서 실제로 잡힌 결함은 digest 2건이다. `open-world-training.md`와 `station-twin-profile.json`의 sha256을 계산 전에 적어 두었고, 실제 값과 달라 검사에서 걸렸다. 수정 후 재실행해 통과했다.

## 7. 입력 결손 5건

| 결손 | 영향 |
|---|---|
| `artifact:32:M4-01-fixture:candidate` (#32) | 5직무 확정 fixture 없음. 현행 provisional 값 사용, #32 확정 시 대조 필요 |
| `artifact:131:reviewed-incident-catalog:candidate` (#131) | 검토된 사건 변형 catalog 없음. 새 schema 적용 보류 |
| `docs/choo-guard-native-acceptance.md` | develop 미게시. 상태별 관찰·기대 결과 원문 대조 미수행 |
| `docs/choo-guard-native-team-contracts.md` | develop 미게시. 관측 전 정보 분리·인솔 접점 대조 미수행 |
| `docs/choo-guard-foundation-multiplayer.md` | develop 미게시. FMP 목표 대조 미수행, legacy 범위로 한정 |

뒤의 세 파일은 [#120의 보류 32건](https://github.com/xrlab-dau/CHOOGuard/issues/120#issuecomment-5651516624) 중 설계·계약 문서 그룹에 해당한다. stopConditions에 따라 그 입력을 쓰는 수용만 멈추고 기록했다.

## 8. 한계

- 기대값 명세다. 자동 시험 통과나 실기 검증을 대신하지 않는다.
- 세계 값은 합성이다. `station-twin-profile`의 `evidenceStatus`가 `synthetic-unverified`다.
- canonical 사건·점수·안전 판정 의미를 바꾸지 않는다. 변경이 필요하면 별도 구현·검토로 보낸다.
- VR 기대값은 의미 대조까지이며 실제 기기에서 확인하지 않았다.

## 9. 후행 인계

- `#35`(다직무 협업·보고·인계), `#88`(다층 네비게이터·연습/평가), `#33`·`#34`(대표 직무·나머지 네 직무)에 화면별 입력·관측 fixture를 넘긴다.
- `#131`에는 사건 계약 차이를 환류한다. 이 fixture는 사건 2유형을 공개 설계 기술로만 다뤘으므로, 검토된 변형 catalog가 나오면 갈래별 기대값을 다시 대조해야 한다.
