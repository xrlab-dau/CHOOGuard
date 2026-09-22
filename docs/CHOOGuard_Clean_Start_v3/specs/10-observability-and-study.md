# K10 · 작업량 계측과 실제 효용 검증
**생산:** CS-OPS.07. **연구 인터페이스:** CS-PLAY.07. **분석:** CS-PROOF.01/.03.

총인시는 wall elapsed나 simulation tick이 아니다. ActivityInterval은 monotonic start/end·UTC anchor·가명 participant·case·category·assistance·consent·completeness를 기록한다. 동의 이전 telemetry는 off다. 원문 대본이나 key stroke를 필요 없이 수집하지 않는다. UI가 focus를 잃은 것은 자동으로 무근무를 의미하지 않으며 attended/unattended 판정을 사용자 표시·관찰근거와 분리한다.

같은 사람의 [0,10)과 [5,15)분은 총 15분이다. 다른 사람의 같은 10분은 각각 합산해 20인분이다. 강제종료로 end가 없으면 censored이고 last heartbeat만으로 완료시간을 확정하지 않는다. 기관 회신 대기·무인 계산은 총인시에 더하지 않되 경과시간·계산비에는 별도로 제시한다. 고객별 개발자 대리 입력·규칙 검수·교육·출력 재편집·유지비도 포함한다.

A는 고객의 실제 기존 도구이며 기존 AI를 금지해 상대편을 약화하지 않는다. B는 동일 코어/자료/AI/도움 정책의 표·타임라인 UI, C는 3D RTS를 더한 화면이다. B도 동일 명령·조건·분기·대본 과제를 수행해야 한다. 연구 condition은 mode enum에 추가하지 않으며 연구용 접근에서만 활성화한다.

평가는 동의된 실제 최근 과제, 순서/난이도 균형화, 원시 시간, 중도탈락/실패/도움, 의미/서식 수정량을 보존한다. 20% 절감은 사전에 합의할 탐색 목표이고 현재 성과가 아니다. 검수자는 가능한 한 도구 조건을 모르게 산출물을 비교한다. 작은 표본의 선호를 안전성·전국 효과로 일반화하지 않는다.

초기 구축비 회수는 순절감 Δ>0일 때만 ceil(initial_person_hours/Δ)로 추정하고 실제 반복 빈도·유지비와 함께 보고한다. Δ≤0이거나 frequency가 없으면 INCONCLUSIVE다. 구매 의사·자료 협조·실증 참여·기관 승인·계약은 별도 상태로 남긴다.
