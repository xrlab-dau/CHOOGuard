# TEAM-25 공개 절차 → 직무·사건 매핑 양식

2026-09-13 작성 · 이슈 [#93](https://github.com/xrlab-dau/CHOOGuard/issues/93) · candidate 후보

공개 출처의 절·쪽에서 임시 직무·행동·Anchor·기대 관측까지 역추적하는 빈 양식이다. 합성 예시는 [`foundation/team-fixtures/TEAM-25/mapping-examples.json`](../../../foundation/team-fixtures/TEAM-25/mapping-examples.json)에 있다.

**이 양식은 실제 기관 절차를 정의하지 않는다.** 작성은 근거를 추적 가능하게 기록하는 일이며, 현업 검수·절차 승인과 별개다. 의미 변경은 양식 작성으로 수용하지 않고 별도 구현·검토로 보낸다.

## 1. 결속하는 입력

| 경로 | 용도 | sha256 |
|---|---|---|
| `foundation/procedures/source-registry.json` | 출처·적용 범위·허용 용도 | `acd217eade028eb6b067233327afe6230e718465dec473c39fbf86d81d5c31f4` |
| `foundation/scenarios/foundation-demo.json` | 임시 직무·행동·Anchor 식별자 | `bd7dfa87529f2d3e94f517e28d23929ed693129406f6f1823090bff73fef3969` |
| `foundation/anchors/synthetic-room.json` | Anchor 집합 | `e6051a3d9131e0e920c4976e197033d5c541f9452d4b4c1f3733d07ad593b5c5` |

기준 커밋 `e480e69`. 세 파일은 읽기만 하고 수정하지 않는다. 출처 등록부에 없는 문서는 근거로 쓸 수 없다.

> 작업 지시 패킷은 `source-registry.json`을 `local_unpublished`로 표시하지만 2026-09-13 기준 `develop`에 게시돼 있다. 패킷의 가용성 표기가 낡았다.

## 2. 필수 필드

| 필드 | 내용 |
|---|---|
| `exampleId` | 항목 식별자 |
| `state` | 아래 다섯 검토 상태 중 하나 |
| `source.sourceId` | 등록부의 출처 ID. 등록부에 없으면 작성 불가 |
| `source.locator` | 절·쪽·문서 위치. 등록부 원문과 **글자 그대로** 일치해야 한다 |
| `source.revisionStatus` | `dated` / `undated` / `historical` |
| `source.verification` | 원문 확인 방식 |
| `source.procedureAcceptance` | 현업 검수 상태 |
| `source.allowedUse` | 등록부가 허용한 용도 |
| `simulation.roleId` · `actionId` · `targetAnchorId` | 현행 임시 식별자. 삼중쌍이 시나리오와 일치해야 한다 |
| `simulation.expectedQuestState` · `expectedFeedbackCode` | 시나리오의 기대값 |
| `mapping.condition` | 전제 조건 |
| `mapping.staffObservation` | 출처가 실제로 말하는 내용 |
| `mapping.expectedSimulationObservation` | 게임에서 관측할 기대 결과 |
| `mapping.reportHandoff` | 보고·인계 처리와 차용 범위 |
| `applicability.operator` · `station` · `role` · `incidentKind` | 적용 범위. 비어 있으면 "미지정"으로 적는다 |
| `verdict` | 판정 |
| `reverseTrace` | 출처 → 절·쪽 → 직무 → 행동 → Anchor → 기대 관측의 연결 사슬 |
| `notAccepted` | 이 항목이 주장하지 않는 것 |

## 3. 검토 상태 다섯 가지

| 상태 | 의미 |
|---|---|
| `applicable` | 허용 용도 안에서 연결 가능. 연결이 기관 승인은 아니다 |
| `prohibited_transfer` | 대상 독자·운영기관이 달라 전용 불가 |
| `gap` | 적용 범위가 비어 있거나 해당 역·직무를 특정하지 못함 |
| `conflict` | 둘 이상의 출처가 역할 명칭·보고 경로에서 어긋남 |
| `authority_unresolved` | 현업 검수 미응답으로 권위 미확정 |

## 4. 거부 코드

| 코드 | 조건 |
|---|---|
| `SOURCE_NOT_REGISTERED` | 등록부에 없는 문서를 근거로 입력 |
| `EMPTY_APPLICABILITY_ASSUMED_UNIVERSAL` | 적용 목록이 비었는데 보편 적용으로 해석 |
| `OPERATOR_MISMATCH` | 다른 운영기관 문서를 대상 기관 절차로 전용 |
| `AUDIENCE_MISMATCH` | 승객 안내를 직원 직무 절차로 전용 |
| `ACCEPTANCE_PENDING` | 검수 미완료를 확정된 절차로 표기 |
| `REVISION_UNDATED_OR_HISTORICAL` | 개정일 없음·과거판을 현행으로 표기 |
| `MEANING_CHANGE_REQUIRES_IMPLEMENTATION` | 행동·안전·판정 의미를 양식으로 변경 시도 |

**적용 목록이 비어 있다는 것은 "어디에나 적용 가능"이 아니다.** 등록부의 `emptyApplicabilityMeans`가 `unspecified_not_universal_permission`이며, 양식은 이 의미를 그대로 따른다.

## 5. 빈 양식

```json
{
  "exampleId": "",
  "state": "applicable | prohibited_transfer | gap | conflict | authority_unresolved",
  "source": {
    "sourceId": "", "locator": "", "revisionStatus": "",
    "verification": "", "procedureAcceptance": "", "allowedUse": []
  },
  "simulation": {
    "roleId": "", "actionId": "", "targetAnchorId": "",
    "expectedQuestState": "", "expectedFeedbackCode": ""
  },
  "mapping": {
    "condition": "", "staffObservation": "",
    "expectedSimulationObservation": "", "reportHandoff": ""
  },
  "applicability": { "operator": "", "station": "", "role": "", "incidentKind": "" },
  "defects": [],
  "verdict": "",
  "reverseTrace": [],
  "notAccepted": []
}
```

## 6. 작성 순서

1. 출처를 등록부에서 고른다. 없으면 멈추고 등록부 보강을 요청한다.
2. `locator`를 등록부 원문에서 복사한다. 요약하거나 공백을 바꾸지 않는다.
3. 적용 범위를 확인한다. 운영기관·독자·역·직무·사건이 대상과 맞는지 각각 본다. 비었으면 "미지정"으로 적고 결손으로 처리한다.
4. 현행 시나리오에서 직무·행동·Anchor를 고른다. 삼중쌍과 기대 Quest·피드백은 시나리오 값을 그대로 쓴다.
5. `reverseTrace`를 출처부터 기대 관측까지 끊기지 않게 적는다.
6. `notAccepted`에 이 항목이 주장하지 않는 것을 적는다.
7. 의미를 바꿔야 한다면 여기서 멈추고 별도 구현·검토로 보낸다.

## 7. 검증 결과 (2026-09-13)

합성 예시 5건에 양식을 적용하고 역추적을 검사했다. **오류 0건.**

| 검사 | 결과 |
|---|---|
| 다섯 상태 각 1건 | 통과 |
| 필수 필드 누락 | 없음 |
| 인용한 `sourceId`가 등록부에 실재 | 5건 모두 통과 |
| `locator`·`revisionStatus`·`verification`·`procedureAcceptance`·`allowedUse`가 등록부와 일치 | 통과 (초안의 불일치 2건을 잡아 수정) |
| role/action/anchor 삼중쌍이 시나리오와 일치 | 통과 |
| 기대 Quest·피드백이 시나리오와 일치 | 통과 |
| `defects` 코드가 전부 선언됨 | 통과 |
| 결속한 입력 digest 일치 | 통과 |
| 권위 미확정 노출 | 5건 모두 `pending_operator_review` |

검사기는 이 이슈의 쓰기 범위 밖이라 저장소에 넣지 않았다. 위 항목은 등록부·시나리오·Anchor 파일과 fixture만으로 재현할 수 있다.

초안에서 실제로 잡힌 결함은 `locator` 두 건이었다. 세미콜론 뒤 공백을 넣은 것과 중간 구간(`166–167쪽 보고서식`)을 빠뜨린 것이다. 양식이 요구하는 "원문 그대로"가 작동한 사례다.

## 8. 한계

- 합성 예시다. 실제 직원 매뉴얼을 수신하거나 대조하지 않았다.
- 출처 5건 모두 `procedureAcceptance`가 `pending_operator_review`다. 현업 검수 완료를 주장하지 않는다.
- 임시 직무·행동·Anchor는 `foundation-demo`의 provisional 값이며 `KORAIL 검증 전 예시`다.
- 실제 원문은 복사하지 않고 등록부의 locator와 URL로만 참조한다.
- 이 양식은 canonical 행동·안전·판정 의미를 바꾸지 않는다.

## 9. 후행 인계

[#62](https://github.com/xrlab-dau/CHOOGuard/issues/62)가 공개 자료 적용을 시작할 때 이 양식 버전과 5상태 예시, 거부 코드, 미확정 표현을 그대로 전달한다. #62는 실제 출처가 늘어나면 등록부를 먼저 보강하고 같은 순서로 작성하면 된다.
