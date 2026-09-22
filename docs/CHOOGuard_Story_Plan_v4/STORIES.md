# 신규 실행 스토리 목록

11개 에픽 / 48개 상위 작업 / 109개 실행 story. 숫자는 제품 진척률이나 일정 추정치가 아니다.

| 에픽 | 상위작업 | 실행 story |
|---|---:|---:|
| CS-BOOT | 3 | 7 |
| CS-PACK | 4 | 9 |
| CS-OPS | 7 | 18 |
| CS-WORLD | 4 | 9 |
| CS-PLAY | 7 | 15 |
| CS-LAB | 4 | 8 |
| CS-MODES | 3 | 7 |
| CS-SIM | 5 | 12 |
| CS-SCRIPT | 4 | 9 |
| CS-PROOF | 4 | 9 |
| CS-SHIP | 3 | 6 |

## CS-BOOT · 새 Unity 제품의 부팅·입력·빌드 기반

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-BOOT.01.01](stories/CS-BOOT.01.01.md) | 새 Unity 프로젝트에서 정적 PC 부팅을 재현한다 | W0 |
| [CS-BOOT.01.02](stories/CS-BOOT.01.02.md) | 한글 UI와 UI 전용 입력 smoke를 추가한다 | W0 |
| [CS-BOOT.02.01](stories/CS-BOOT.02.01.md) | 13개 모듈과 시험 assembly의 의존 경계를 고정한다 | W0 |
| [CS-BOOT.02.02](stories/CS-BOOT.02.02.md) | 명령·조회·응답 DTO와 비동기 ports를 정의한다 | W0 |
| [CS-BOOT.02.03](stories/CS-BOOT.02.03.md) | 실제 운영 코어를 새 Scene의 단일 CompositionRoot에 연결한다 | W2 |
| [CS-BOOT.03.01](stories/CS-BOOT.03.01.md) | EditMode·PlayMode 실행기의 실패 전파를 만든다 | W0 |
| [CS-BOOT.03.02](stories/CS-BOOT.03.02.md) | PC Player 빌드와 실행 영수증을 생성한다 | W0 |

## CS-PACK · 현장·기관 매뉴얼·자료 패키지 제작

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-PACK.01.01](stories/CS-PACK.01.01.md) | 새 객체 ID·SI단위·시간 타입을 정의한다 | W0 |
| [CS-PACK.01.02](stories/CS-PACK.01.02.md) | SiteBundle와 ScenarioSpec의 참조를 검사한다 | W1 |
| [CS-PACK.01.03](stories/CS-PACK.01.03.md) | 두 기관·두 공간·공유 자원 fixture를 만든다 | W1 |
| [CS-PACK.02.01](stories/CS-PACK.02.01.md) | 판본·절·예외가 있는 규칙 후보를 로드한다 | W1 |
| [CS-PACK.02.02](stories/CS-PACK.02.02.md) | 규칙 개정과 검수 범위를 전파한다 | LATER |
| [CS-PACK.03.01](stories/CS-PACK.03.01.md) | 무료판 원본을 격리 검사하고 입고 증거를 남긴다 | LATER |
| [CS-PACK.03.02](stories/CS-PACK.03.02.md) | 부품·rig·clip과 Unity 임포트 자격을 기록한다 | LATER |
| [CS-PACK.04.01](stories/CS-PACK.04.01.md) | 첫 현장의 관측·가정·독립 치수를 등록한다 | LATER |
| [CS-PACK.04.02](stories/CS-PACK.04.02.md) | 현실 관측을 시각과 revision에 맞춰 갱신한다 | LATER |

## CS-OPS · 영속 상태를 가진 다기관 운영 커널

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-OPS.01.01](stories/CS-OPS.01.01.md) | 단일 run writer와 순서 있는 명령 큐를 만든다 | W1 |
| [CS-OPS.01.02](stories/CS-OPS.01.02.md) | 부작용 없는 preview와 Submit 재검사를 구현한다 | W1 |
| [CS-OPS.01.03](stories/CS-OPS.01.03.md) | 요청키 멱등성과 응답 손실 후 조회를 구현한다 | W1 |
| [CS-OPS.02.01](stories/CS-OPS.02.01.md) | 지원 native SQLite를 Player에서 열고 스키마를 준비한다 | W1 |
| [CS-OPS.02.02](stories/CS-OPS.02.02.md) | 이벤트·예약·receipt를 원자 트랜잭션으로 저장한다 | W1 |
| [CS-OPS.02.03](stories/CS-OPS.02.03.md) | 트랜잭션 outbox와 중복 결과 방지를 구현한다 | W3 |
| [CS-OPS.02.04](stories/CS-OPS.02.04.md) | 동시 예약·취소·재시작 장애를 주입해 검증한다 | W3 |
| [CS-OPS.03.01](stories/CS-OPS.03.01.md) | 기관 내부 지시와 외부 지원요청을 분리한다 | W1 |
| [CS-OPS.03.02](stories/CS-OPS.03.02.md) | 지휘권 인계를 revision과 확인에 결속한다 | LATER |
| [CS-OPS.04.01](stories/CS-OPS.04.01.md) | 보고의 송신·접수·확인·만료를 분리한다 | LATER |
| [CS-OPS.04.02](stories/CS-OPS.04.02.md) | 작성자 보기와 기관 수신 정보 projection을 분리한다 | LATER |
| [CS-OPS.05.01](stories/CS-OPS.05.01.md) | 선행조건·정보·공간 도달이 있는 업무망을 실행한다 | LATER |
| [CS-OPS.05.02](stories/CS-OPS.05.02.md) | 인력·차량·장비의 점유와 반환 생명주기를 연결한다 | LATER |
| [CS-OPS.05.03](stories/CS-OPS.05.03.md) | 두 기관의 요청부터 보고·인계까지 수직 구간을 완성한다 | LATER |
| [CS-OPS.06.01](stories/CS-OPS.06.01.md) | 복수 원인을 원인축·근거·해결조건으로 반환한다 | LATER |
| [CS-OPS.06.02](stories/CS-OPS.06.02.md) | 순환대기와 외부 대기를 구분하고 형식 모델과 대조한다 | LATER |
| [CS-OPS.07.01](stories/CS-OPS.07.01.md) | 동의 기반 활동구간과 개발자 도움을 기록한다 | LATER |
| [CS-OPS.07.02](stories/CS-OPS.07.02.md) | 총인시를 합집합과 검열구간 기준으로 집계한다 | LATER |

## CS-WORLD · 현실 기반 철도 운영 공간

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-WORLD.01.01](stories/CS-WORLD.01.01.md) | 새 2공간·문·1m 기준체 fixture를 생성한다 | W2 |
| [CS-WORLD.01.02](stories/CS-WORLD.01.02.md) | 문과 anchor를 읽기 전용 projection에 결속한다 | W2 |
| [CS-WORLD.02.01](stories/CS-WORLD.02.01.md) | 대표 무료 모델을 격리 임포트하고 실제 부품을 기록한다 | LATER |
| [CS-WORLD.02.02](stories/CS-WORLD.02.02.md) | 첫 철도 구간을 근거가 있는 기하와 연결로 구축한다 | LATER |
| [CS-WORLD.02.03](stories/CS-WORLD.02.03.md) | 관측기반 복원과 독립 치수 잔차를 비교한다 | LATER |
| [CS-WORLD.03.01](stories/CS-WORLD.03.01.md) | 올바른 run·revision의 projection만 월드에 적용한다 | LATER |
| [CS-WORLD.03.02](stories/CS-WORLD.03.02.md) | 층별 표시와 pooled 월드마커를 구현한다 | LATER |
| [CS-WORLD.04.01](stories/CS-WORLD.04.01.md) | 구역 준비상태에 따라 진입과 additive 로딩을 제어한다 | LATER |
| [CS-WORLD.04.02](stories/CS-WORLD.04.02.md) | 정차·도킹·문과 차량 좌표계를 연결한다 | LATER |

## CS-PLAY · Unity 네이티브 RTS 운영 작업공간

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-PLAY.01.01](stories/CS-PLAY.01.01.md) | 포인터 입력의 최초 소유자를 끝까지 유지한다 | W2 |
| [CS-PLAY.01.02](stories/CS-PLAY.01.02.md) | 한글 IME와 모달 키보드 포커스를 격리한다 | W2 |
| [CS-PLAY.01.03](stories/CS-PLAY.01.03.md) | Player에서 재배정·포인터/키보드 충돌을 확인한다 | W2 |
| [CS-PLAY.02.01](stories/CS-PLAY.02.01.md) | 팀/차량과 업무 선택을 같은 SelectionSet에 연결한다 | W2 |
| [CS-PLAY.02.02](stories/CS-PLAY.02.02.md) | 대상별 조건을 보여주고 확인한 요청만 제출한다 | W2 |
| [CS-PLAY.03.01](stories/CS-PLAY.03.01.md) | 실제 Game View 위에 기본 운영 HUD를 조립한다 | W2 |
| [CS-PLAY.03.02](stories/CS-PLAY.03.02.md) | RTS 카메라를 입력 컨텍스트와 연결한다 | LATER |
| [CS-PLAY.03.03](stories/CS-PLAY.03.03.md) | 실제 위치를 사용하는 층별 미니맵을 만든다 | LATER |
| [CS-PLAY.04.01](stories/CS-PLAY.04.01.md) | 여러 대기 원인을 인스펙터와 기관 타임라인에 표시한다 | LATER |
| [CS-PLAY.04.02](stories/CS-PLAY.04.02.md) | 기관 보기와 오버레이의 복귀·정지 상태를 관리한다 | LATER |
| [CS-PLAY.05.01](stories/CS-PLAY.05.01.md) | 글자 확대·키 재배정·고대비 설정을 구현한다 | LATER |
| [CS-PLAY.05.02](stories/CS-PLAY.05.02.md) | 두 모드·최근 현장 런처와 반복 진입을 연결한다 | LATER |
| [CS-PLAY.06.01](stories/CS-PLAY.06.01.md) | 복원 가능성을 검사하는 분기 대화상자를 만든다 | LATER |
| [CS-PLAY.06.02](stories/CS-PLAY.06.02.md) | 비교불가·불확도·선택 이유를 A/B 화면에 표시한다 | LATER |
| [CS-PLAY.07.01](stories/CS-PLAY.07.01.md) | 동일 코어의 표·타임라인 연구 비교군을 만든다 | LATER |

## CS-LAB · 저장된 운영안의 분기·비교 실험

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-LAB.01.01](stories/CS-LAB.01.01.md) | 공통 cut의 코어·메시지·예약·난수 checkpoint를 저장한다 | LATER |
| [CS-LAB.01.02](stories/CS-LAB.01.02.md) | 이벤트 재생과 worker 복원능력 검사를 연결한다 | LATER |
| [CS-LAB.02.01](stories/CS-LAB.02.01.md) | 불변 parent에서 새 run으로 분기한다 | LATER |
| [CS-LAB.02.02](stories/CS-LAB.02.02.md) | 정확 재개와 처음부터 재계산을 구분한다 | LATER |
| [CS-LAB.03.01](stories/CS-LAB.03.01.md) | 같은 비교 basis와 외생 혁신을 결속한다 | LATER |
| [CS-LAB.03.02](stories/CS-LAB.03.02.md) | 제약·불확도·비지배 결과와 우열 불명을 표시한다 | LATER |
| [CS-LAB.04.01](stories/CS-LAB.04.01.md) | 의미 변경의 영향과 자격 만료를 계산한다 | LATER |
| [CS-LAB.04.02](stories/CS-LAB.04.02.md) | 안전한 캐시와 전체 재실행 대조를 구현한다 | LATER |

## CS-MODES · 매뉴얼 교육과 제약 기반 랜덤 실험

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-MODES.01.01](stories/CS-MODES.01.01.md) | 매뉴얼 근거가 있는 튜토리얼 목표를 구성한다 | LATER |
| [CS-MODES.01.02](stories/CS-MODES.01.02.md) | 안내·힌트 축소·복기를 같은 코어에 연결한다 | LATER |
| [CS-MODES.02.01](stories/CS-MODES.02.01.md) | 제약과 인과를 지키는 랜덤 사건 그래프를 생성한다 | LATER |
| [CS-MODES.02.02](stories/CS-MODES.02.02.md) | 거부 이력·지원 필요·미지원 영역을 설명한다 | LATER |
| [CS-MODES.03.01](stories/CS-MODES.03.01.md) | 검수된 현장과 두 모드의 세션을 재사용한다 | LATER |
| [CS-MODES.03.02](stories/CS-MODES.03.02.md) | 계산을 보존하며 다음 판단 지점까지 진행한다 | LATER |
| [CS-MODES.03.03](stories/CS-MODES.03.03.md) | 현장 선택·상황 설정을 native 프리팹으로 연결한다 | LATER |

## CS-SIM · 다중 물리·행동 계산과 정량 검증

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-SIM.01.01](stories/CS-SIM.01.01.md) | capability handshake와 제한된 JSONL transport를 만든다 | LATER |
| [CS-SIM.01.02](stories/CS-SIM.01.02.md) | job·generation·출력 수량의 상관관계를 검사한다 | LATER |
| [CS-SIM.01.03](stories/CS-SIM.01.03.md) | 시험 worker 종료·timeout·재시작을 검증한다 | LATER |
| [CS-SIM.02.01](stories/CS-SIM.02.01.md) | 보행 어댑터의 단일 위치 소유권을 구현한다 | LATER |
| [CS-SIM.02.02](stories/CS-SIM.02.02.md) | 병목·층간·보조 이동의 독립 보행 벤치마크를 수행한다 | LATER |
| [CS-SIM.03.01](stories/CS-SIM.03.01.md) | 접근교통·차량·승무원 어댑터를 연결한다 | LATER |
| [CS-SIM.03.02](stories/CS-SIM.03.02.md) | 현지 접근시간·운행 범위를 독립 자료와 비교한다 | LATER |
| [CS-SIM.04.01](stories/CS-SIM.04.01.md) | FDS batch 입력·경계 이력·출력 파일을 결속한다 | LATER |
| [CS-SIM.04.02](stories/CS-SIM.04.02.md) | 수렴·독립 관측·화재 QoI 오차를 평가한다 | LATER |
| [CS-SIM.05.01](stories/CS-SIM.05.01.md) | 공동시간과 수량 소유권 barrier를 구현한다 | LATER |
| [CS-SIM.05.02](stories/CS-SIM.05.02.md) | 전체 worker 복구와 결합오차를 대조한다 | LATER |
| [CS-SIM.05.03](stories/CS-SIM.05.03.md) | 검증된 가속모델과 유효범위 이탈을 처리한다 | LATER |

## CS-SCRIPT · 선택 운영안의 근거 기반 대본 저작

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-SCRIPT.01.01](stories/CS-SCRIPT.01.01.md) | 허용된 기관·판본의 근거를 제한 조회한다 | LATER |
| [CS-SCRIPT.01.02](stories/CS-SCRIPT.01.02.md) | AI 제안을 검증하고 실패시 수동 작업을 유지한다 | LATER |
| [CS-SCRIPT.02.01](stories/CS-SCRIPT.02.01.md) | 선택 운영안을 조건부 ScriptIR로 변환한다 | LATER |
| [CS-SCRIPT.02.02](stories/CS-SCRIPT.02.02.md) | 대본과 실행형 데이터의 의미 동등성을 검사한다 | LATER |
| [CS-SCRIPT.03.01](stories/CS-SCRIPT.03.01.md) | native 대본 편집과 의미·서식 diff를 만든다 | LATER |
| [CS-SCRIPT.03.02](stories/CS-SCRIPT.03.02.md) | 검토 주체·범위·revision과 승인 만료를 연결한다 | LATER |
| [CS-SCRIPT.04.01](stories/CS-SCRIPT.04.01.md) | 같은 ScriptIR에서 JSON·Markdown 초안을 출력한다 | LATER |
| [CS-SCRIPT.04.02](stories/CS-SCRIPT.04.02.md) | 고객 DOCX 양식을 결속하고 출력 워커를 검증한다 | LATER |
| [CS-SCRIPT.04.03](stories/CS-SCRIPT.04.03.md) | 외부 편집의 의미 차이를 운영안으로 조정한다 | LATER |

## CS-PROOF · 사용자 가치·현장 적합성 검증

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-PROOF.01.01](stories/CS-PROOF.01.01.md) | 실제 작성 과제·시간·동의 조사 프로토콜을 고정한다 | LATER |
| [CS-PROOF.01.02](stories/CS-PROOF.01.02.md) | 허용된 최근 대본·양식과 관찰 결과를 입고한다 | LATER |
| [CS-PROOF.02.01](stories/CS-PROOF.02.01.md) | 첫 native 요청·저장·재열기 수직구간을 검수한다 | W3 |
| [CS-PROOF.02.02](stories/CS-PROOF.02.02.md) | 전체 A/B·두 모드·대본·새 run 흐름을 검수한다 | LATER |
| [CS-PROOF.02.03](stories/CS-PROOF.02.03.md) | 고정 부하에서 장애·입력·복구·성능을 대조한다 | LATER |
| [CS-PROOF.03.01](stories/CS-PROOF.03.01.md) | A/B/C 평가를 사전 등록하고 독립 과제를 고정한다 | LATER |
| [CS-PROOF.03.02](stories/CS-PROOF.03.02.md) | 총인시·품질·미완료·반복비용을 분석한다 | LATER |
| [CS-PROOF.04.01](stories/CS-PROOF.04.01.md) | 지정 용도의 독립 정확도·불확도 프로토콜을 고정한다 | LATER |
| [CS-PROOF.04.02](stories/CS-PROOF.04.02.md) | 기관 검토와 새로운 관측의 지정용도 자격을 기록한다 | LATER |

## CS-SHIP · 로컬 배포·복구·현장 확장

| Story | 결과 | 작업창 |
|---|---|---|
| [CS-SHIP.01.01](stories/CS-SHIP.01.01.md) | 로컬 배포물과 지원 기능 manifest를 생성한다 | LATER |
| [CS-SHIP.01.02](stories/CS-SHIP.01.02.md) | 원자 업데이트·실패 롤백·호환성을 검사한다 | LATER |
| [CS-SHIP.02.01](stories/CS-SHIP.02.01.md) | 일관된 DB snapshot·blob pin 백업을 만든다 | LATER |
| [CS-SHIP.02.02](stories/CS-SHIP.02.02.md) | staging 복구·스키마 갱신·실패 롤백을 검증한다 | LATER |
| [CS-SHIP.03.01](stories/CS-SHIP.03.01.md) | 새 현장·기관·사건 패키지를 독립 등록한다 | LATER |
| [CS-SHIP.03.02](stories/CS-SHIP.03.02.md) | 유지비·데이터 책임·사용 한계를 인계한다 | LATER |
