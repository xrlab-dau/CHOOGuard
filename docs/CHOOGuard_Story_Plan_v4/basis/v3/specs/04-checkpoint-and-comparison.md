# K04 · 분기·재생·동일조건 비교
**소유:** CS-LAB.01–.04, 시각 표현 CS-PLAY.06.

## 변경되지 않는 원본
run event stream은 append-only다. 과거 annotation 추가는 새로운 사실이며 원래 시각에 있었던 의도를 소급 작성하지 않는다. ScenarioSpec(외생 조건), OperationalPlan(작성자의 행동 규칙), Run(실제로 적용된 사건), ScriptIR(검토용 문서)은 별도 version이다.

## 완전 checkpoint 절차
입력 접수를 pause-request로 전환하고 이미 수락한 명령을 정해진 cutSequence까지 정리한다. pause ack 전에 화면에 정지 확정이라 쓰지 않는다. domain·eventQueue·reservations·knowledge·messages·randomStreams·contentLock과 활성 worker의 상태/재계산 recipe를 동일 tick에 수집한다. 필수 worker 목록은 run capability에서 얻고 누락은 허용하지 않는다. hash·inputDigest·tick·cut을 검증하고 manifest-last로 게시한다. 워커 하나가 앞서면 staging 결과를 폐기하고 마지막 공통 checkpoint부터 다시 실행한다.

workerStates의 순서·파일명만 검사하지 않고 workerId 집합의 정확 일치, duplicate 없음, tick/cut/input 일치, 실제 blob 존재를 검사한다. FROM_ORIGIN recipe는 원점→cut까지의 모든 외생 입력과 확정 의사결정을 다시 적용하여 의미 상태 digest와 지정 QoI를 비교한 경우에만 정확한 복원 경로로 인정한다.

## branch generation과 이전 결과
BranchRequest의 parent checkpoint와 새 plan을 고정한다. 새 runId·generation·outbox jobId를 만든다. 부모의 완료된 외부 효과를 재전송하지 않는다. 부모 실행의 worker 결과는 child current에 쓸 수 없다. 정확 state가 없으면 EXACT_FORK를 거부하고 REEXECUTE_FROM_ORIGIN을 명시 선택하게 한다.

## 비교 계약
동일 site/rule/model/단위/QoI/exogenous process contract를 비교 basis로 고정한다. 외생 난수는 streamId/processId/occurrenceIndex로 주소화한다. 각 run이 RNG를 호출한 순서에 결합하지 않는다. 사용자 행동으로 달라지는 군중·후속 메시지·이동은 각 run에서 재계산한다. 하나의 seed가 동일하다는 사실만으로 fair compare를 승인하지 않는다.

constraints를 먼저 비교하고 그 뒤 시간·자원·복잡도·불확도별 비지배 대안을 표시한다. 실패/중단/모델 범위 밖을 평균에서 조용히 빼지 않는다. 개발용 선정 set과 평가 set을 분리한다. 사전 결정된 중단 규칙을 보존하고 rank가 입력 가정에 따라 뒤집히면 우열 불명으로 남긴다.

## 부분 재실행
표시/서식만 변경은 계산을 유지할 수 있다. 의미·물리·외생 조건 변경은 dependency/invalidations를 전파한다. 영향 그래프의 완전성을 입증하지 못하면 전 구간/전체 run으로 넓혀 재계산한다. 캐시 키는 content+model+inputHistory+boundary+code digest다. 부분 재실행은 fresh full run과 의미 상태 및 선언 QoI tolerance를 비교해야 한다. 차이가 생기면 캐시를 비활성화하고 입력 근거를 남긴다.
