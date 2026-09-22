# K08 · 두 모드·조건부 상황 생성·의사결정 구간
**소유:** CS-MODES. 두 사용자 모드는 TUTORIAL과 RANDOM_OPERATIONS_LAB뿐이다.

Tutorial: 공개 훈련 재구성과 원 대본 재현을 구분하고 source revision·교육목표·부분순서·expected evidence를 고정한다. 자유 순서 가능한 독립업무는 다른 클릭 순서로도 통과한다. 힌트 on/off가 core/model/resource/information policy digest를 바꾸면 실패다. 교육상의 보여주기와 가상 기관이 수신한 정보는 다른 projection이다.

Random: grammar version, site/rule/model lock, seed manifest, correlated condition graph를 입력으로 받는다. 합법성 검사→표본 생성→물리/기관/자원 제약 검사→성공 또는 상세 거부를 기록한다. 초기 maxAttempts=64는 개발 상한이며 실행 조건에 기록한다. 상한 도달은 요청 조건 변경을 요구하고 성공할 때까지 몰래 돌리지 않는다. 발생확률 자료가 없으면 coverage sampling이라고 표시한다. 무기를 설계하거나 공격 효율을 최적화하는 생성 목표는 사용하지 않는다.

FEASIBLE_WITHIN_MODEL/ESCALATION_REQUIRED/CONFLICTED_INPUT/UNSUPPORTED_DOMAIN를 분리한다. 어려운 사례와 계산 불가능한 사례를 같은 실패 점수로 처리하지 않는다. 지원 없는 불가능 사례를 생성했으면 추가지원 학습용이라는 사실을 보존한다.

NextDecision: 사용자가 요청한 다음 의미 이벤트까지 운영 큐와 활성 워커를 정상 step으로 진행한다. 다음 점은 guard 변화·지원 요청·완료 보고·사용자 선택 필요·계산 실패 등으로 정의한다. rendering frame을 뛰어넘을 수 있지만 가상 사건을 삭제하거나 물리 dt를 확대하지 않는다. 다음 판단이 없으면 horizon에 도달했다고 알리고 무한 실행하지 않는다. 이 runner는 local core와 configured worker adapter를 소비하며 실제 매뉴얼 승인 완료를 구현 선행으로 요구하지 않는다.
