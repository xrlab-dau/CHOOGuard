# CHOOGuard · 검수 개정 v3 · 빈 저장소에서 새 구축

현재 기준은 이 묶음이다. 이전 코드·작업번호·구역 ID·성공 기록을 요구하지 않는다. 원격 초기화·삭제·push·이슈 변경도 수행하지 않는다.

## 읽기
1. PRODUCT_BASELINE.md → EPICS.md.
2. CONTRACTS.md → 지정 tasks/CS-*.json → 연결 specs만 읽는다.
3. sources는 해당 작업의 sourceIds만 찾는다. 원본 입고는 별도 확인한다.

## 문서·합성 설계 검사
```sh
python -m pip install -r requirements.txt
python tools/check_plan.py
python tools/render_specs.py --check
python -m unittest discover -s tests -v
python tools/task_context.py CS-BOOT.01 --phase prepare --profile fixture
```
문맥 `complete:true`는 출력이 잘리지 않았다는 뜻이지 입력 준비·실행 허가·제품 완료가 아니다. 큰 응답은 budget 초과로 종료코드3과 다시 읽을 명령을 반환한다.

## 처음 개발할 것
CS-BOOT.01의 정적 native smoke는 후행 CompositionRoot 없이 생성한다. CS-BOOT.02의 assembly/typed ports를 받으면 core/fixture/UI를 계약별로 병행한다. CS-PROOF.01 사용자 조사는 독립적으로 시작할 수 있다. 실제 자료가 없어도 합성 기술 개발은 가능하지만 현장 수용을 주장할 수 없다.

## 검수 증거의 범위
review/REVIEW.md에서 결함과 반복 결과를 읽는다. 이 패키지에서 실행한 것은 Python 문서·wire·합성 SQL 설계 검사다. Unity/C# 컴파일, 실제 Player, 물리 solver, 독립 LLM, 실제 사용자·기관 시험은 NOT_RUN이다. Python sqlite 버전은 배포용 보안·내구성 수용 증거가 아니다. 폰트·외부 모델 바이너리는 포함하지 않는다.
