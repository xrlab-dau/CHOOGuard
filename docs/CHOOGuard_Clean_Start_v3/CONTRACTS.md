# 신규 구현 공통 계약 v3

제품 방향과 실제 요구는 [PRODUCT_BASELINE.md](PRODUCT_BASELINE.md), 에픽/작업 데이터는 [tasks.json](contracts/tasks.json)이 정본이다. 상세 기술·오류 의미는 아래 12개 문서와 엄격한 wire 스키마를 함께 읽는다. 이전 판의 필드 이름 나열만으로 구현하지 않는다.

| 계약 | 정의 |
|---|---|
| [K01 빌드·assembly](specs/01-build-and-assemblies.md) | 새 smoke→조립, 13 module roots, Editor/PlayMode/Player |
| [K02 타입·ports](specs/02-wire-and-ports.md) | 요청/응답/key/비동기/취소와 오류 |
| [K03 영속 운영](specs/03-durable-operations.md) | SQLite 최소 안전조건, commit·예약·outbox·blob |
| [K04 분기·비교](specs/04-checkpoint-and-comparison.md) | 완전한 cut, generation, 외생·내생·부분재실행 |
| [K05 계산 워커](specs/05-worker-and-cosimulation.md) | JSONL, correlation, 필드·단위·frame·rollback |
| [K06 네이티브 화면](specs/06-native-surfaces.md) | S01–S12, 입력 우선순위, 실제 Player |
| [K07 자료·규칙](specs/07-content-and-rule-contract.md) | 실제 source·무료판·RuleIR·현실갱신 |
| [K08 모드](specs/08-modes-and-scenario-generation.md) | 같은 코어, 부분순서, 조건부 생성 |
| [K09 대본·승인](specs/09-script-and-approval.md) | AI 제한·ScriptIR·원본 보존·사람 검토 |
| [K10 계측·연구](specs/10-observability-and-study.md) | 생산 계측기와 A/B/C parity |
| [K11 배포·복구](specs/11-release-and-backup.md) | offline·snapshot API·blob pin·staging restore |
| [K12 증거·성능](specs/12-evidence-and-performance.md) | 주장별 수용, 목표값과 실제 결과 구별 |

새 public contract는 CS-BOOT.02/CS-PACK.01이 생성한다. 각 implementation path는 쓰기 담당 작업 하나를 가진다. shared manifest·scene 변경은 담당자의 반영을 통해 이뤄지고 폴더 단위 임의 덮어쓰기를 허용하지 않는다. 이 문서는 원격 작업 권한이나 과거 이슈를 요구하지 않는다.

## 고정 불변식
제품은 Unity native PC/uGUI/TMP/Input System, 사용자 모드는 TUTORIAL과 RANDOM_OPERATIONS_LAB다. UI/AI는 계산·명령·승인의 정본이 아니다. 한 run과 한 physical field에는 지정 writer 하나가 있다. 받아들인 요청은 commit 뒤에만 공표한다. 과거 run은 불변이며 새 branch·revision으로 변경한다. 빈 실제 근거를 그럴듯한 값으로 채우지 않는다. 무료 모델·신규 문서·hash는 현장 정확도를 증명하지 않는다.

## 단계의 읽기와 실행
[profiles.json](contracts/profiles.json)의 condition/phase를 사용한다. 모르는 condition은 false가 아니라 오류다. candidate에서 test double로 개발한 결과를 integration/qualification까지 통과했다고 선언할 수 없다. 생산자 전체 완료가 아니라 필요한 산출물 단계만 소비한다.
