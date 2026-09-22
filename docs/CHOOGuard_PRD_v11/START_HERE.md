# CHOOGuard v11 · 개발 AI 시작점

제품은 시나리오 작성 담당자의 RTS형 다기관 운영 실험실이다. 운영 시뮬레이션이 본체,로그가 근거,대본이 결과다. 이 문서 작성은 구현·기관 승인·GitHub 변경이 아니다.

1. PRD §00–03에서 문제·타겟·진입·현재 상태를 읽는다.
2. 지정 EP의 `agent-packets/EPxx.json`과 선택 lane만 읽는다.
3. 해당 REQ·AT, 정확한 source ID와 현재 입력·쓰기 경계를 확인한다.
4. `candidate` 산출물과 전체 이슈 종료를 혼동하지 않는다. 실제 작업 소스·권한·claim은 저장소 최신 상태로 확인한다.

두 사용자 모드는 TUTORIAL, RANDOM_OPERATIONS_LAB이다. A/B/C는 연구 비교 조건이며 세 번째 모드가 아니다. 무료 우선 자산을 사용해도 정확도·물성·검수 비용이 사라지지 않는다.

v11의 중요한 변경: 반복 작성팀, 총인시·초기/반복비용, 고객 양식, 재사용·변경 영향·다음 판단 지점, 동일 코어 비교, 독립 자격축. 전체 고정밀 계산이 끝날 때까지 사용자 연구를 기다리지 않되 미검증 결과를 실제 예측으로 승인하지 않는다.

첫 구현은 EP00의 개발 경계를 고정한 뒤 EP01/02 코어와 EP03 작은 공간을 병행한다. 고객 진입자료·검수자 협의는 별도 track이다. 문서의 가상 데이터나 미구현 파일을 실제 산출물로 가장하지 않는다.

문서 구조 검사(제품 실행 아님):
```sh
python -m pip install -r requirements.txt
python tests/validate_contracts.py
python -m unittest discover -s tests -p 'test_*.py' -v
```

코어 제품 테스트91개는 명세이며 현재 NOT_RUN. 실제 문서검사 결과는 `review/`에서 범위를 확인한다. READ_HISTORY나 전체 인터넷 조사는 기본 필수문맥이 아니다. 새 CLI나 자동 작업 스케줄러를 추가하지 않았다.

반환: EP/lane/phase, 실제 branch·commit·경로, 입력·출력ref/hash, 실행 검사·원시결과, 실패·미실행·적용범위, 다음 인계·claim 해제.
