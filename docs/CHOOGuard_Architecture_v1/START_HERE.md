# CHOOGuard 아키텍처 개발 진입점

**정본:** PRD v11 + Unity Native UX v2 → 이 아키텍처 1.0.0.

제품은 Unity PC 네이티브 운영 실험실이다. 웹 앱이 아니다. 작성자가 기관·팀·차량·업무를 운영하고, A/B안을 시험·비교한 뒤 선택 운영안을 훈련 대본으로 만든다.

## 먼저 읽을 것

1. `ARCHITECTURE.md` §00–02와 `diagrams/A01-overview.svg`.
2. 자신에게 지정된 `agent-packets/EPxx.json`.
3. 그 packet이 지정한 짧은 장과 `INTERFACES.md`.
4. 필요한 `schemas/`·`examples/`·원문·기존 소스만 읽는다.

전체 과거 대화나 모든 basis 파일을 기본 문맥에 넣지 않는다. EP/REQ/AT/S 식별자는 기존 PRD/UX를 유지한다. 새로운 architecture component ID는 실행 작업을 자동 생성하는 스케줄러가 아니다.

## 실행 진입

Python 3.10 이상에서 `python -m pip install -r requirements.txt`로 검사 의존성을 준비한다. 이 기록은 Python 3.13.5에서 실행했다.

`python -m unittest discover -s tests -v`는 이 전달 패키지의 구조·예제·설계 SQL fixture를 검사한다. Unity 제품 시험이 아니다.

`python tools/context.py EP04`는 EP04의 문맥 경로·책임·인계 필드를 출력한다. 작업 실행·승인·원격 소스 변경을 수행하지 않는다.

## 절대 바꾸지 않을 경계

- 사용자 모드: `TUTORIAL`, `RANDOM_OPERATIONS_LAB` 두 개, 같은 계산 코어.
- 주 UI: uGUI/TMP + WorldCamera. Input routing과 한글 IME는 실제 Player 시험.
- 단일 run/field owner, durable acceptance, 재전송 멱등성, 완전 checkpoint.
- AI는 근거 설명·제안·초안만. 명령·물리·기관 승인을 직접 변경하지 않는다.
- 관측/가정/합성/미검증을 구분한다. 무료 에셋은 물리적 사실의 근거가 아니다.

## 현행 소스와 작업 권한

조사 기준은 `aae4867c71c96f34f8fe8a252cddd9aa0412394f`. 실제 작업 전에 최신 source/work-order/branch/claim을 확인한다. `contracts/repository-baseline.json`은 선택 열람 기록이며 전체 코드 검증이 아니다. 이 문서의 경로·역할은 쓰기 권한이 아니다. #222·기존 담당자·보드·코드는 이 작업에서 수정하지 않았다.

## 미완료를 그대로 반환

Unity compile/Player, SQLite native binding·power loss, 외부 solver coupling, 기관 매뉴얼 승인, 실제 사용자 효과는 이 전달물에서 NOT_RUN/NOT_EVALUATED다. 정적 시험으로 승격하지 않는다. `review/VERIFICATION.md`에서 실제 검사 범위를 확인한다.
