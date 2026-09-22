# K11 · 오프라인 패키지·백업·업데이트
**소유:** CS-SHIP. 기술 배포와 현장 활용을 분리한다.

offline package는 새 build/source/package lock, native SQLite binary, 선택한 worker·모델·가중치, content/rule/template digests, qualification과 capability를 명시한다. 외부 AI 없이도 local operations·저장·기존 근거·수동 편집은 유지한다. unsupported physics를 숨겨 대체하지 않는다. 실행파일/키/제한 원본을 임의 자동 배포하지 않는다. font 파일은 이 문서 묶음에 포함하지 않는다.

## 일관된 백업
live .db만 복사하지 않는다. SQLite Backup API 등 일관된 snapshot 경로를 사용한다. snapshot에서 referenced blob set을 읽고 GC pin을 잡아 모든 blob의 hash/size를 검증한다. backup manifest는 마지막에 쓴다. 불완전 bundle은 backup 목록에 ready로 노출하지 않는다. WAL을 분리하면 commit된 상태를 잃을 수 있다는 공식 제약을 따른다. [AUD-SQLITE-BACKUP][AUD-SQLITE-WAL]

복원은 새 staging 디렉터리에서 수행한다. schema support, DB integrity, FK, event sequence, receipt/예약, blob, worker recipe, content locks를 검증한 뒤 활성 포인터를 전환한다. 기존 DB를 먼저 삭제하지 않는다. install/update 중 power interruption 각각에서 현재본 또는 이전본으로 재시작 가능해야 한다. 초기화 전의 옛 저장소와 호환하라는 요구가 아니라 이 새 제품의 향후 revision을 관리하는 정책이다.

출력에 현장용 표시가 있다면 그 scope evidence가 실제 존재해야 한다. 개발 fixture 버전은 설치 과정에서도 SYNTHETIC/NOT_VALIDATED 표시가 남는다. 기술 시연판 설치에 구매계약이나 모든 기관의 승인까지 요구하지 않는다. 반대로 기관 수용 없는 package를 공식 현장판으로 배포하지 않는다.
