# CS-PACK.02.01 · 판본·절·예외가 있는 규칙 후보를 로드한다

**상위:** CS-PACK.02 / CS-PACK · **작업창:** W1 · **상태:** NOT_STARTED / NOT_RUN

**Goal:** REQUIRED/DISCRETIONARY/ADVISORY/INVARIANT/UNKNOWN을 나누고 scope·guard·sourceLocator를 저장한다.

**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.
**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.
**Spec:** [basis/v3/tasks/CS-PACK.02.json](../basis/v3/tasks/CS-PACK.02.json)

> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.

## 사용자 또는 후행 작업이 받는 결과
판본·절·예외가 있는 규칙 후보를 로드한다

## 입력·출력 인터페이스
RuleClause {id,revision,kind,scope,guard,effect,handoff,exception,sourceLocator,review}; DTO는 예외를 검토자료로 유지.

**직접 담당 요구:** REQ-031, REQ-033, REQ-034
**지원 요구:** REQ-031, REQ-033, REQ-034
**부모 제품 시험:** AT-031, AT-033, AT-034

## 정확 수정 경로
| 작업 | 경로 | 최초 작성 story |
|---|---|---|
| CREATE | `Assets/ChooGuard/Contracts/RuleTypes.cs` | CS-PACK.02.01 |
| CREATE | `Assets/ChooGuard/Content/RuleCatalog.cs` | CS-PACK.02.01 |
| CREATE | `content/schemas/rule.schema.json` | CS-PACK.02.01 |
| CREATE | `content/rules/catalog.json` | CS-PACK.02.01 |

**이 story 전용 시험:** `Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0201Tests.cs`
**시험 매체:** EDIT_MODE
Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.

## 단계별 선행 산출물
| 생산 story | 산출물 단계 | 소비 단계 | 조건 |
|---|---|---|---|
| CS-PACK.01.02 | `OUT-CS-PACK.01.02@candidate` | candidate | ALWAYS |

## 외부 입력과 보류 범위
추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.

## 구현 순서
```text
1. REQUIRED/DISCRETIONARY/ADVISORY/INVARIANT/UNKNOWN을 나누고 scope·guard·sourceLocator를 저장한다.
2. 경계에서 전달받은 식별자·버전과 해당 specRefs의 실패/취소 의미를 확인한다.
3. 공개 API/출력 형식 변경은 계약 소유 스토리에 반영하고 소비자 회귀를 함께 실행한다.
```

## 시험 벡터
### AC-CS-PACK.02.01-P · POSITIVE · NOT_RUN
Given: 고정한 입력/환경과 판본·절·예외가 있는 규칙 후보를 로드한다의 유효 조건
When: 판본·절·예외가 있는 규칙 후보를 로드한다를 실행한다
Then: 기관·사건 범위로 규칙을 조회하고 원문 절과 미검수 여부를 구별한다.

### AC-CS-PACK.02.01-N · NEGATIVE · NOT_RUN
Given: 규칙 후보에 검수자·판본 없이 approved를 붙이면 활성화되지 않는다.
When: 명시한 반례를 주입하고 동일 경계를 실행한다
Then: 규칙 후보에 검수자·판본 없이 approved를 붙이면 활성화되지 않는다.

## 실행 체크리스트
- [ ] 지정 specRefs와 입력 artifact의 revision·SHA-256·실제 수용 범위를 읽는다.
- [ ] acceptance 반례와 정상 fixture를 Assets/ChooGuard/Tests/EditMode/Stories/CSPACK0201Tests.cs에 먼저 작성하고 예상 assertion 실패를 기록한다.
- [ ] writes의 정확 경로만 수정해 goal의 동작과 오류 의미를 구현한다. 자동 .meta는 같은 claim으로 취급한다.
- [ ] 해당 testKinds와 영향받은 부모 제품 시험을 실제 환경에서 실행하고 raw output·실패·미실행을 남긴다.
- [ ] 서로 다른 리뷰 실행이 계약·경계·제품 의도를 대조한다. 독립 검수자가 없으면 SELF_REVIEW_ONLY로 남긴다.
- [ ] patch digest·테스트 로그·산출물 hash·미지원 범위를 execution receipt로 반환한다. merge/원격 쓰기는 별도 사용자 권한이다.

제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.
```sh
python tools/plan.py brief CS-PACK.02.01 --phase candidate --profile fixture
```

## 반려·재분할 조건
- 해당 단계의 실제 입력이 없으면 영향받는 단계만 HOLD한다; prepare/다른 독립 작업을 전역 차단하지 않는다.
- 동일 결함이 재검수에서 두 번 반복되면 같은 시도를 무한 반복하지 않고 API/스토리 분할 원인을 기록한다. 의도적 TDD red는 이에 포함하지 않는다.
- 다른 활성 claim·지원되지 않는 Unity/worker·검증되지 않은 현장 가정을 발견하면 성공을 선언하지 않는다.

## 인계 계약
`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.

## 필요한 읽기
- [GLOBAL_CONTRACT.md](../GLOBAL_CONTRACT.md)
- [basis/v3/specs/07-content-and-rule-contract.md](../basis/v3/specs/07-content-and-rule-contract.md)
- [basis/v3/specs/02-wire-and-ports.md](../basis/v3/specs/02-wire-and-ports.md)
- [MAN-KORAIL](https://info.korail.com/info/contents.do?key=969) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-MEDICAL](https://www.mohw.go.kr/board.es?act=view&bid=0009&list_no=1478957&mid=a10402000000&nPage=1&tag=) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
- [MAN-SOP](https://www.daegu.go.kr/cmsh/daegu.go.kr/119/files/%EC%9E%AC%EB%82%9C%ED%98%84%EC%9E%A5%ED%91%9C%EC%A4%80%EC%9E%91%EC%A0%84%EC%A0%88%EC%B0%A8.pdf) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님
