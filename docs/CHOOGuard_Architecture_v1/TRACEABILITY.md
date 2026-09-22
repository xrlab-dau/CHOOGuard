# PRD 91개 요구 → 아키텍처 책임

연결은 구현 완료를 뜻하지 않는다. 원래 AT 식별자를 유지한다.

| 요구 | 제목 | 작업 | 책임 모듈 | 인수시험 |
|---|---|---|---|---|
| REQ-001 | 작성자 중심 전체 운영 여정 | EP10 | EXPERIMENT, EVIDENCE | AT-001 |
| REQ-002 | 두 사용자 모드와 동일 코어 | EP06 | CONTENT, OPS | AT-002 |
| REQ-003 | 튜토리얼 출처·재구성 표시 | EP06 | CONTENT, OPS | AT-003 |
| REQ-004 | 부분순서와 유효 대안 학습 | EP06 | CONTENT, OPS | AT-004 |
| REQ-005 | 조건부 랜덤 초기 상태 | EP06 | CONTENT, OPS | AT-005 |
| REQ-006 | 불가능·범위밖 상황 분리 | EP06 | CONTENT, OPS | AT-006 |
| REQ-007 | 생성기 재현·거부 기록 | EP06 | CONTENT, OPS | AT-007 |
| REQ-008 | RTS 팀·차량 선택 | EP04 | UI, APP | AT-008 |
| REQ-009 | 업무 우선 배정 | EP04 | UI, APP | AT-009 |
| REQ-010 | 혼합기관 선택 결과 확인 | EP04 | UI, APP | AT-010 |
| REQ-011 | 작성자 전지적 보기와 기관 정보 | EP04 | UI, APP | AT-011 |
| REQ-012 | 취소·큐·지원요청 조작 | EP04 | UI, APP | AT-012 |
| REQ-013 | 다축 상태·복수 원인 표시 | EP04 | UI, APP | AT-013 |
| REQ-014 | 현실 기반 연결 맵 | EP03 | CONTENT, UI, LEGACY | AT-014 |
| REQ-015 | 기존 13구역 식별자 보존 | EP11 | BOOT, CONTENT, STORE | AT-015 |
| REQ-016 | 층·지붕·LOD와 운영 분리 | EP03 | CONTENT, UI, LEGACY | AT-016 |
| REQ-017 | 네 표현과 같은 객체 결속 | EP03 | CONTENT, UI, LEGACY | AT-017 |
| REQ-018 | 좌표·단위·도킹 시험 | EP03 | CONTENT, UI, LEGACY | AT-018 |
| REQ-019 | 이동 엔진 단일 권위 | EP07 | WORKERS, SIM, LEGACY | AT-019 |
| REQ-020 | 구역 미준비·로딩 실패 처리 | EP03 | CONTENT, UI, LEGACY | AT-020 |
| REQ-021 | 소속 권한·공조 요청 분리 | EP02 | OPS, LEGACY | AT-021 |
| REQ-022 | 지휘 인계 버전·범위 | EP02 | OPS, LEGACY | AT-022 |
| REQ-023 | 도착·등록·대기자원 | EP02 | OPS, LEGACY | AT-023 |
| REQ-024 | 복합 자원 원자 예약 | EP02 | OPS, LEGACY | AT-024 |
| REQ-025 | 팀 분리와 capability 재계산 | EP02 | OPS, LEGACY | AT-025 |
| REQ-026 | 업무 완료와 재가용 분리 | EP02 | OPS, LEGACY | AT-026 |
| REQ-027 | 메시지 수명·중복·만료 | EP02 | OPS, LEGACY | AT-027 |
| REQ-028 | 지식 불확실성·시점 보존 | EP02 | OPS, LEGACY | AT-028 |
| REQ-029 | 수용·인계·종료 조건 | EP02 | OPS, LEGACY | AT-029 |
| REQ-030 | 순환대기·외부대기 분류 | EP02 | OPS, LEGACY | AT-030 |
| REQ-031 | 매뉴얼 출처·판본·적용범위 | EP01 | CONTENT, EVIDENCE | AT-031 |
| REQ-032 | 원문 기반 후보 추출 | EP01 | CONTENT, EVIDENCE | AT-032 |
| REQ-033 | 필수·재량·공학제약 구별 | EP01 | CONTENT, EVIDENCE | AT-033 |
| REQ-034 | 규칙 개정 영향분석 | EP01 | CONTENT, EVIDENCE | AT-034 |
| REQ-035 | 시뮬레이션 시간 통제 | EP05 | EXPERIMENT, STORE, SIM | AT-035 |
| REQ-036 | 완전 checkpoint/정직한 재시작 | EP05 | EXPERIMENT, STORE, SIM | AT-036 |
| REQ-037 | 원본 불변·분기 혈통 | EP05 | EXPERIMENT, STORE, SIM | AT-037 |
| REQ-038 | 공정한 A/B 외생 조건 | EP05 | EXPERIMENT, STORE, SIM | AT-038 |
| REQ-039 | 다목적·불확도 비교 | EP05 | EXPERIMENT, STORE, SIM | AT-039 |
| REQ-040 | 선정·평가 자료 분리 | EP10 | EXPERIMENT, EVIDENCE | AT-040 |
| REQ-041 | 운영 모델-런타임 의미 일치 | EP02 | OPS, LEGACY | AT-041 |
| REQ-042 | 기준 화재 solver와 실제 입력 | EP08 | SIM, WORKERS, EVIDENCE | AT-042 |
| REQ-043 | 보행 현상별 검증 | EP07 | WORKERS, SIM, LEGACY | AT-043 |
| REQ-044 | 차량·교통 shortcut 탐지 | EP07 | WORKERS, SIM, LEGACY | AT-044 |
| REQ-045 | 의료 운영과 임상 범위 구분 | EP07 | WORKERS, SIM, LEGACY | AT-045 |
| REQ-046 | SOTA 후보의 공정 선정 | EP08 | SIM, WORKERS, EVIDENCE | AT-046 |
| REQ-047 | 대체모델 독립·장기 검증 | EP08 | SIM, WORKERS, EVIDENCE | AT-047 |
| REQ-048 | OOD 정량 승인 차단 | EP08 | SIM, WORKERS, EVIDENCE | AT-048 |
| REQ-049 | 다중엔진 단일 소유권·결합 | EP08 | SIM, WORKERS, EVIDENCE | AT-049 |
| REQ-050 | 실측·가상 관측 분리 | EP03 | CONTENT, UI, LEGACY | AT-050 |
| REQ-051 | 로그 사실·의도·가설 구분 | EP09 | AUTHOR, AI, EVIDENCE | AT-051 |
| REQ-052 | 인과 주장과 계산 근거 | EP09 | AUTHOR, AI, EVIDENCE | AT-052 |
| REQ-053 | 운영안 일반화·조건부 단계 | EP09 | AUTHOR, AI, EVIDENCE | AT-053 |
| REQ-054 | 대본·실행 데이터 동등성 | EP09 | AUTHOR, AI, EVIDENCE | AT-054 |
| REQ-055 | 근거를 여는 역추적 | EP09 | AUTHOR, AI, EVIDENCE | AT-055 |
| REQ-056 | 승인 분리·변경 무효화 | EP09 | AUTHOR, AI, EVIDENCE | AT-056 |
| REQ-057 | 새 실행과 리플레이 구별 | EP05 | EXPERIMENT, STORE, SIM | AT-057 |
| REQ-058 | LLM 실패시 운영 지속 | EP09 | AUTHOR, AI, EVIDENCE | AT-058 |
| REQ-059 | 외부 문서 지시 격리 | EP09 | AUTHOR, AI, EVIDENCE | AT-059 |
| REQ-060 | 온톨로지·ID·버전 추적 | EP01 | CONTENT, EVIDENCE | AT-060 |
| REQ-061 | 기계 친화형 최소 문맥 | EP01 | CONTENT, EVIDENCE | AT-061 |
| REQ-062 | 자료 발견·확보·검사 분리 | EP03 | CONTENT, UI, LEGACY | AT-062 |
| REQ-063 | KTX·역사 원본 재검사 | EP03 | CONTENT, UI, LEGACY | AT-063 |
| REQ-064 | 무료 우선 자산·한국화·기능 검수 | EP03 | CONTENT, UI, LEGACY | AT-064 |
| REQ-065 | 실제훈련 인원·영상 근거 | EP06 | CONTENT, OPS | AT-065 |
| REQ-066 | 기존코드 재사용·범위 보존 | EP00 | APP, EVIDENCE | AT-066 |
| REQ-067 | 초기버전과 검증출시 분리 | EP10 | EXPERIMENT, EVIDENCE | AT-067 |
| REQ-068 | 사전 QoI·오차·독립 검증 | EP08 | SIM, WORKERS, EVIDENCE | AT-068 |
| REQ-069 | 작성시간·수정량·교육평가 | EP10 | EXPERIMENT, EVIDENCE | AT-069 |
| REQ-070 | 정직한 성능 프로파일 | EP10 | EXPERIMENT, EVIDENCE | AT-070 |
| REQ-071 | 접수된 이벤트 내구성 | EP05 | EXPERIMENT, STORE, SIM | AT-071 |
| REQ-072 | 접근성·한국어 용어 | EP04 | UI, APP | AT-072 |
| REQ-073 | 개인정보·배포·원본 접근 | EP01 | CONTENT, EVIDENCE | AT-073 |
| REQ-074 | 엄격한 수용·한정 재작업 | EP10 | EXPERIMENT, EVIDENCE | AT-074 |
| REQ-075 | 최종 13구역·유형 확대 | EP11 | BOOT, CONTENT, STORE | AT-075 |
| REQ-076 | 현장 적용 passport | EP11 | BOOT, CONTENT, STORE | AT-076 |
| REQ-077 | 실제 반복 작성팀과 업무 근거 | EP00 | APP, EVIDENCE | AT-077 |
| REQ-078 | 고객 실증 진입 입력 | EP00 | APP, EVIDENCE | AT-078 |
| REQ-079 | 네 시계와 사람 작업량 원장 | EP05 | EXPERIMENT, STORE, SIM | AT-079 |
| REQ-080 | 최초·반복 비용과 회수 가능성 | EP10 | EXPERIMENT, EVIDENCE | AT-080 |
| REQ-081 | 동일 코어 A/B/C 효용 비교 | EP10 | EXPERIMENT, EVIDENCE | AT-081 |
| REQ-082 | 품질·미완료를 보존한 효용 판정 | EP10 | EXPERIMENT, EVIDENCE | AT-082 |
| REQ-083 | 검수된 현장·규칙 묶음 재사용 | EP01 | CONTENT, EVIDENCE | AT-083 |
| REQ-084 | 변경 영향·부분 재실행의 안전성 | EP05 | EXPERIMENT, STORE, SIM | AT-084 |
| REQ-085 | 다음 판단 지점으로의 정직한 진행 | EP04 | UI, APP | AT-085 |
| REQ-086 | 실제 제출 양식과 의미 변경 회수 | EP09 | AUTHOR, AI, EVIDENCE | AT-086 |
| REQ-087 | 실제 배포·반출 환경에서의 작동 | EP11 | BOOT, CONTENT, STORE | AT-087 |
| REQ-088 | 반복 사용·유지비·도입 증거 분리 | EP00 | APP, EVIDENCE | AT-088 |
| REQ-089 | 결과별 한계의 사용자 이해 | EP04 | UI, APP | AT-089 |
| REQ-090 | 개발·효용·정량·기관·도입 gate 분리 | EP10 | EXPERIMENT, EVIDENCE | AT-090 |
| REQ-091 | 대체재·맞춤 구축 비용의 실제 비교 | EP00 | APP, EVIDENCE | AT-091 |
