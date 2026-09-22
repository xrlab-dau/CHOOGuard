# CHOOGuard 클린 스타트 에픽·구현 명세 v3

**2026-09-19 · 신규 구축 · 검수 개정판 · 제품 실행 NOT_RUN**

빈 저장소에서 만드는 제품이며 초기화 이전 코드·에픽·구역 ID·성공 기록을 요구하지 않는다. 사용자 요구는 유지하고 누락된 기능 단위를 보완해 **11개 에픽·48개 작업·91개 제품 요구**로 연결한다.

## 에픽
| 에픽 | 목표 | 작업 수 |
|---|---|---:|
| [CS-BOOT](epics/CS-BOOT.md) | 새 Unity 제품의 부팅·입력·빌드 기반 | 3 |
| [CS-PACK](epics/CS-PACK.md) | 현장·기관 매뉴얼·자료 패키지 제작 | 4 |
| [CS-OPS](epics/CS-OPS.md) | 영속 상태를 가진 다기관 운영 커널 | 7 |
| [CS-WORLD](epics/CS-WORLD.md) | 현실 기반 철도 운영 공간 | 4 |
| [CS-PLAY](epics/CS-PLAY.md) | Unity 네이티브 RTS 운영 작업공간 | 7 |
| [CS-LAB](epics/CS-LAB.md) | 저장된 운영안의 분기·비교 실험 | 4 |
| [CS-MODES](epics/CS-MODES.md) | 매뉴얼 교육과 제약 기반 랜덤 실험 | 3 |
| [CS-SIM](epics/CS-SIM.md) | 다중 물리·행동 계산과 정량 검증 | 5 |
| [CS-SCRIPT](epics/CS-SCRIPT.md) | 선택 운영안의 근거 기반 대본 저작 | 4 |
| [CS-PROOF](epics/CS-PROOF.md) | 사용자 가치·현장 적합성 검증 | 4 |
| [CS-SHIP](epics/CS-SHIP.md) | 로컬 배포·복구·현장 확장 | 3 |

## 이번에 보완한 경계
입출력은 [타입·API](specs/02-wire-and-ports.md)와 [schemas](schemas/CommandIntent.schema.json), build는 [assembly](specs/01-build-and-assemblies.md), 저장은 [원자 계약](specs/03-durable-operations.md), 분기는 [복원·비교](specs/04-checkpoint-and-comparison.md), 물리는 [worker 계약](specs/05-worker-and-cosimulation.md)를 따른다. 화면은 [네이티브 화면 책임](specs/06-native-surfaces.md)을 따른다.

- CS-OPS.07: 실제 사람 작업량 계측 생산자.
- CS-PLAY.06: checkpoint/분기/A-B 비교의 실제 native presenter·prefab.
- CS-PLAY.07: 동일 코어의 연구용 표·타임라인. 세 번째 사용자 모드가 아니다.

## 시작과 병행
CS-BOOT.01의 첫 smoke는 후행 CompositionRoot를 요구하지 않는다. CS-BOOT.02에서 assembly/typed ports를 고정한 뒤 CS-PACK·CS-OPS·CS-WORLD·CS-PLAY·worker fixture가 필요한 계약을 소비한다. candidate는 명시된 test double로 단위 개발할 수 있다. integration은 선택 profile에서 활성화된 실제 산출물을 소비한다. 현장 qualification은 독립 자료·검수 범위가 필요하다. 전체 에픽 종료를 기다리는 관계로 자동 변환하지 않는다.

첫 완성 구간: 두 기관의 요청→자원 예약→업무→보고/인계→A 보존→B 변경→비교→대본 출력. 크게 보이는 맵이나 그럴듯한 UI만으로 완료하지 않는다.

## 수용 구분
문서 구조 / native 실행 / 사용자 효용 / 정량 모델 / 기관 활용 / 현실 갱신은 각각 [claim gate](contracts/claim-gates.json)로 검토한다. 이 패키지의 Python 검사는 Unity·물리·현장 성능시험이 아니다. 제품 인수시험과 48개 작업의 상태는 NOT_RUN / NOT_IMPLEMENTED다.
