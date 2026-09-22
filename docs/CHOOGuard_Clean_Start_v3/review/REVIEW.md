# 신규 구축 명세 v3 · 적대적 비평·수정·재검수 기록

**대상:** 사용자에게 전달된 Clean Start v2 ZIP과 새 CS 작업 명세.  
**수행자:** 같은 작성자의 자체 비평 및 로컬 실행 검사. 독립 전문가·기관·외부 LLM 심사가 아니다.  
**원본 SHA-256:** `298e9f4d202731dbd7b71bc37e525beef7ef66f6517feebeab819696f636ed52`  
**수정:** 새로운 v3 파일만 생성. 원본 ZIP·원격 저장소·담당자·이슈·제품 코드는 변경하지 않음.

## 요약
v2의 14개 자동시험은 재실행에서 모두 통과했다. 그러나 이것은 충분한 검수가 아니었다. 플랫폼/생산단계/조건/중복 ID/역방향 수용 연결/빈 알고리즘/시험 내용/Windows 경로 등 **12개 독립적인 손상**을 모두 놓쳤다. 구현 계약에서도 assembly, UI 생성자, 계측기, 비교군, IPC, runtime 시험 책임이 불충분했다.

v3는 기존 새 CS 에픽의 제품 목적을 유지하면서 3개 실제 누락 작업을 추가해 11개 에픽·48개 작업으로 개정했다. 원래 제품 요구 91개와 제품 인수시험91개는 유지한다. v2→v3는 이름 변경이나 이전 저장소 작업 복원이 아니라 실행 계약의 보완이다.

## 반복 이력
| 라운드 | 실제 행동 | 결과·해석 |
|---|---|---|
| R0 | v2 checker와 기존 시험 재실행 | 14개 통과. 검사 충분성은 별도 |
| R0 반례 | 12개 손상 입력을 v2 validator에 투입 | 12개 전부 MISSED. 원본 `round0-probes.json` |
| R1 | 구현 책임·스키마·assembly·단계조건·시험매체·IPC·저장 계약 보완 | 기존14개 통과. 이후 추가 반례 실시 |
| R2 | 새 문서·wire·SQL 설계 시험 추가 | 73개 중1개 실패. 실패 원인은 검수자가 candidate와 integration을 혼동해 만든 잘못된 cycle fixture였음 |
| R2 교정 | 두 integration 단계가 실제 상호 의존하도록 cycle fixture 수정 | 합법적 단계 분리를 유지하고 실제 cycle만 검사. 기준 완화·시험 삭제 없음 |
| R3 | 유효하지만 요청과 다른 unit/frame/fieldOwner와 다른 run의 receipt 반례 추가 | 77개 중4개 실패. schema만으로 상관관계가 검사되지 않음을 확인 |
| R3 수정 | outputContract·상관관계·commit run identity를 명시하고 검사 | 77개 통과. 실패 로그와 수정 후 로그 모두 보존 |
| R4 | 문서 projection 일치·ReceiptLookup·CommitReceipt·미확인 입력 담당 검사 추가 | 최종 실행 결과는 `final-tests.txt`와 `final-summary.json` 참조 |

반례 시험은 문서/설계 검사를 악의적으로 깨뜨리는 검사다. 실제 Unity 업무·물리 엔진·기관 대응 능력을 시험했다는 뜻이 아니다.

## 주요 결함과 반영 위치
| ID | 심각도 | 원래 문제 | 수정·재검수 근거 |
|---|---|---|---|
| F01 | P0 | 검사가 웹 UI 전환과 무의미한 단계·조건을 수용 | check_plan, 12개 역검사 |
| F02 | P0 | 필요한 worker가 runtime이 아니라 qualification의 조건으로만 연결 | profile별 phase graph, LAB.01/SIM.05 조건 분리 |
| F03 | P1 | 첫 부팅이 후행 CompositionRoot를 요구하는 문장 | BOOT.01 static smoke와 BOOT.02 조립 분리 |
| F04 | P0 | 4개 asmdef만 지정해 World 등 참조가 predefined assembly로 흘러갈 위험 | 13개 root·Editor 경계·모든 C# 소속 검사 |
| F05 | P0 | 필드 이름 요약뿐이며 requester를 포함한 receipt key·wire 타입 불완전 | 18개 엄격한 schema·예제·K02 |
| F06 | P0 | native SQLite 보안·WAL/백업·blob 원자성 입력이 추상적 | K03/K11·2026 공식 수정 버전 조건·SQL 설계 시험 |
| F07 | P1 | 실제 IME·HUD·disk 시험을 대부분 EditMode 경로 하나로 표현 | task별 PlayMode/Player/worker 매체 지정 |
| F08 | P1 | 분기·비교 API가 있어도 실제 S08/S09 화면 생산자가 없음 | CS-PLAY.06 및 surfaces registry |
| F09 | P1 | 시간 절감 연구는 있지만 실제 작업량 생산 코드 책임이 없음 | CS-OPS.07 ActivityRecorder/Store |
| F10 | P1 | 동일 코어 표/타임라인 비교군을 누가 만드는지 없음 | CS-PLAY.07, 연구 조건=제품모드가 아님 |
| F11 | P0 | IPC framing·bounded payload·취소·오래된 결과 정책 미결정 | JSONL v1 고정·limits·generation fence·K05 |
| F12 | P0 | 단위 enum 통과만으로 다른 단위·frame·owner 결과 수용 | SimulationJob.outputContract + R3 반례 |
| F13 | P0 | CommitBatch에 다른 run의 receipt가 들어가도 schema 통과 | commit/receipt 교차검사 + R3 반례 |
| F14 | P1 | 실제 첫 현장 산출물에 Scene 경로가 빠짐 | FirstSite.unity + source/geometry 동등 검수 |
| F15 | P1 | 렌더링 로딩 지연이 가상 이동 지연에 섞일 수 있는 표현 | K06 logical readiness와 renderer 상태 분리 |
| F16 | P1 | task JSON을 고쳐도 합본/에픽 문서가 과거 내용을 유지할 위험 | render_specs 정본 projection·일치 시험 |

**모든 F 항목의 현재 상태:** 명세/검사기에서 수정하고 자체 재검수함. 해당 제품 코드가 구현·실행됐다는 상태는 아님.

## AAA 요구에 대한 판정 방식
점수를 임의로 올려 AAA를 발급하지 않는다. 아래는 이번 명세를 다음 구현에 넘기기 위한 내부 수용축이다.

1. 신규 구축/네이티브/두 모드/무료 우선 요구를 위반하지 않는다.
2. 모든 원 요구의 생산자·시험이 역방향으로 연결된다.
3. 작업 입력·출력·오류·취소·반례가 정해져 있다.
4. assembly·typed port·test target·screen producer가 구체적이다.
5. 실제 현장 입력과 구현 fixture의 차단 범위를 구분한다.
6. 저장·중복·부분실패·백업·worker 결과 수용 조건이 명시돼 있다.
7. 재생/분기/동일조건/우열불명의 의미를 구분한다.
8. 인시 측정과 연구 비교군이 실제 구현 작업에 연결된다.
9. 공개자료·원본취득·실제실행·현장승인을 혼동하지 않는다.
10. 이전 검사기의 누락을 새 반례로 탐지하고 원시 결과를 보존한다.

현재 이 내부 명세 수용의 최종 상태는 `final-summary.json`에서만 확인한다. 실제 제품 AAA·독립 LLM 실행 성공·기관 수용은 NOT_EVALUATED다. 이 문서가 ‘현장에서 항상 안전한 최적안을 만든다’고 보장하지 않는다.

## 이번 실행 환경과 제한
Python·jsonschema로 schema/관계/합성 계약을 검사했다. SQL 스키마는 임시 디스크 DB에서 transaction·제약·backup API·reopen을 시험했다. 환경의 Python SQLite는 3.46.1이며 배포용 최소 수정 버전 조건을 충족하지 않는다. 여기서 실행한 단일 연결 설계시험을 patched native SQLite/다중연결 WAL race/정전 내구성 시험으로 주장하지 않는다.

Unity, C# compiler, 실제 Player, FDS/JuPedSim/SUMO, 인터뷰·독립 기관 검수는 이 작업에서 실행하지 않았다. 필요한 입력·owner·소비 단계·대안은 `contracts/open-inputs.json`에 기록했다.
