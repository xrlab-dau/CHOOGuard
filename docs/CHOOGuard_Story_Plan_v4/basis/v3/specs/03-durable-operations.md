# K03 · 영속 명령·자원·예약·취소
**소유:** CS-OPS.01–.06. **설계용 실행 스키마:** `design/sqlite.sql` (Python schema 시험용이며 Unity provider 구현은 아니다).

## native 저장 baseline
배포 SQLite는 3.51.3 이상을 기본 최소로 하며 이전 계열은 공식 3.44.6/3.50.7 WAL-reset backport와 바이너리 source-id/해시를 입증해야 한다. 2026-03-03 발견, 3.51.3에서 수정된 WAL 경합 문제를 반영한 조건이다. 라이브러리를 설치하지 않았으므로 현재 Unity가 이 조건을 만족한다고 주장하지 않는다. [AUD-SQLITE-WAL]

로컬 파일시스템, actor-owned connection, WAL, synchronous=FULL, foreign_keys=ON을 기본 계약으로 한다. 다중 PC 공유폴더의 동일 DB를 지원하지 않는다. 디스크의 flush 보장은 OS/storage 장애 주입으로 확인하며 pragma 설정만으로 정전 내구성을 인증하지 않는다.

## Submit 알고리즘
1. read-only preview와 별개로 immutable intent를 수신한다. 한 run의 mailbox가 state writer다.
2. BEGIN IMMEDIATE. receipt key가 있으면 fingerprint를 비교한다. 일치하면 저장된 receipt, 다르면 INTENT_CONFLICT를 반환한다.
3. authoritative run revision과 모든 readSet을 확인하고 기관 권한·능력·필요 정보·경로 조건을 재평가한다.
4. resource lock set을 stable ordinal order로 계산한다. 팀 구성원·차량 운전자·장비의 고유 ID를 확장한다. 수량형 자원은 unit을 일치시켜 active 예약 합+새 수량≤capacity를 검사한다.
5. 업무 한 개의 events/reservations/receipt/projectionRevision/outbox를 모두 저장한다. 복수 독립 업무는 각각 결과를 만들되 한 업무 내부를 부분 성공시키지 않는다.
6. COMMIT 완료 뒤에만 메모리 current와 UI receipt를 게시한다. 실패면 후보 상태를 폐기한다. commit 이후 전달 실패는 결과 미상 응답이고 재조회로 복구한다.

## 원자성의 경계
DB는 blob 파일시스템과 하나의 자동 transaction이 아니다. blob은 임시 파일→bounded write→flush→hash 확인→같은 filesystem atomic rename으로 먼저 준비한다. 그 hash 참조를 DB commit한다. 중간 실패로 생긴 unreferenced blob은 orphan으로 보존 후 유예 GC한다. 반대로 DB에만 등록되고 file이 없는 blob은 수용하지 않는다. run/백업/검수에 참조 중인 blob은 GC할 수 없다.

## 자원 생명주기
capacity는 정수 최소단위(개·인·mL 등)와 명시 unit이다. active 예약만 가용량을 줄인다. actual capacity 감소가 기존 예약 합보다 작으면 예약을 몰래 지우지 않고 RESOURCE_DEFICIT로 신규 배정 차단·재계획 요청을 기록한다. 완료·복귀·재보급은 다른 상태다. 취소는 자신의 reservationId와 version에만 작용한다. 기관별 정보 수신이 없으면 전지적 author view를 guard 충족으로 쓰지 않는다.

## 장애 시험
commit 전 종료→현재 revision/예약 변화 없음; commit 후 응답 전 종료→재시작 같은 receipt; 다른 요청 동시 예약→한 승자; cancel 반복→타 업무 예약 보존; disk-full→성공 미게시; network worker 재전송→의미 job 하나; outbox claimed 상태에서 process 종료→lease 만료 후 재시도하되 결과는 한 번 수용.

job lease/timeout은 wall clock, 업무 소요·정보 만료는 정책에 지정한 simulation clock이다. lease가 만료됐다고 시뮬레이션의 임무를 실패시키지 않는다.
