# K12 · 단계별 증거·성능·품질 gate
**사전 동결한 내부 명세 검수 기준이며 외부 AAA 인증 규격이 아니다.**

## 주장별 증거
문서 구조/Unity 기능/실제 사용자 효용/현상별 정량 검증/기관 활용/현실 관측 갱신을 별도 판정한다. `contracts/claim-gates.json`에 required evidence를 정의한다. 파일 hash는 동일성을 보여줄 뿐 자료 진실성·매뉴얼 적용성·실제 실행을 인증하지 않는다.

## 런타임 성능 목표 (제안 설계값, 측정값 아님)
CS-BOOT에서 사양을 고정한 Windows PC를 기록한다. 기본 목표 1920×1080·60Hz 화면, 16팀/가상 대응인력64명/일반인120명/차량10대/열차1편성은 개발 부하이며 공식 편성이 아니다. 60분 soak·부하 단계별 p50/p95/p99·peak memory·event backlog·worker latency를 기록한다. 평균 FPS만으로 수용하지 않는다.
UI 입력 시각 피드백 p95≤100ms, 화면 프레임 p95≤16.7ms/p99≤33.3ms를 초기 검토 목표로 두며 backend·해상도·설정·모델을 함께 저장한다. command ACCEPTED는 commit 완료 전 표시하지 않는다. reference 계산은 real-time factor 목표를 사전에 별도 정하고, 미달은 honest computing 상태이지 물리 dt 확대 이유가 아니다. 표본 부족·사양 미확정은 NOT_EVALUATED이며 숫자 목표를 달성했다고 쓰지 않는다.

## 정량 정확도
각 QoI에 unit·observation uncertainty·calibration/holdout split·absolute tolerance·near-zero policy·수치수렴·결합오차·중단조건을 독립 검증 전에 동결한다. 여기서 실제 철도 안전 임계값·의료 임계값을 만들어 넣지 않는다. 값 미확정은 해당 FIELD gate의 차단 입력으로 owner/제공처/대안/소비단계를 기록한다. synthetic UI/계약 개발 전체를 차단하지 않는다.

## 기술 반복 검수
정적 관계 검사→직접 반례→구체 계약 보완→같은 반례 재시험→새 관점의 반례 순으로 반복한다. 동일 연구자 self-review를 독립 심사라고 부르지 않는다. 품질 기준을 통과하기 위해 실패 시험을 삭제하거나 기준을 약화하지 않는다. 장치·현장·제3자 입력이 필요한 시험을 문서 수정만으로 PASS로 바꾸지 않는다.

## 결정 보류 기록
Bootstrap exact patch/provider/폰트: CS-BOOT/CS-OPS에서 실행 가능한 조합 하나 선택·lock 후 compile gate.
실제 source/매뉴얼 판본/치수: CS-PACK/CS-WORLD에서 입고·검수 후 named-site gate.
현상 QoI tolerance: CS-SIM/CS-PROOF에서 독립시험 전에 동결.
실제 사용자·기관 검수자: CS-PROOF에서 참여·권한 확인 후 efficacy/field gate.
각 보류는 기술선정 미완료 또는 외부 입력 미확인이다. 자동으로 성공한 값이나 임의 일정으로 채우지 않는다.
