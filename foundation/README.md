# 합성 파운데이션 데이터

**KORAIL 검증 전 예시.** 이 디렉터리는 코레일 자료 없이 개발할 수 있는 공개 합성 데이터만 포함한다. 역할명, 입력, 팀 이벤트, 피드백은 임시 개발 데이터이며 실제 철도 업무 절차나 안전 적합성을 정의하지 않는다.

- `scenarios/foundation-demo.json`: 같은 `synthetic-room`에서 임시 역할 5개를 제공한다. 각 역할에는 입력 1개, 대상 참조 1개, 나머지 4개 역할의 가상 팀 이벤트, 예시 피드백이 있다. `representativeRoleId`는 기본 시연 역할이며 5개 중 어느 역할로도 바꿀 수 있다.
- `anchors/synthetic-room.json`: `anchor-01`부터 `anchor-05`까지의 참조 ID와 임시 표시명이다. geometry, 좌표 변환, 충돌 경계, 실제 역 정보는 포함하지 않는다. Unity 장면의 실제 Transform 연결은 학교 PC의 별도 장면 통합 작업이다.
- `../schemas/foundation-scenario.schema.json`: Unity DTO와 공유하는 JSON 필드 계약이다. ID 중복, 대상 참조, 역할 간 팀 이벤트 관계는 Python 검증기가 추가 검사한다.

저장소 루트에서 실행한다. Python 표준 라이브러리만 사용하며 설치, 네트워크, Unity 실행이 필요하지 않다.

```sh
python3 scripts/foundation/validate.py
python3 -m unittest discover -s scripts/foundation/tests -v
```

Windows에서 실행 명령이 `py`이면 `python3` 대신 `py -3`을 사용한다. 검증기는 인자로 외부 경로를 받지 않고 이 저장소의 고정 파일 3개만 읽는다. 출력의 `PASS`는 합성 JSON 계약과 참조 무결성에만 해당한다.

검증기는 파일 크기, UTF-8, JSON 문법, 중복 키, 비표준 숫자, 필수/미지원 필드, 타입, 임시 예시 표기, 공백뿐인 문구, ID 중복과 참조 오류를 검사한다. 저장소 스키마에서 실제 사용하는 명시적 JSON Schema 키워드만 구현한다. 알 수 없는 키워드나 외부 `$ref`는 실패 처리하며 범용 JSON Schema 검증기로 사용하지 않는다.

행위자의 `expectedQuestState: completed`는 해당 역할의 예시 입력을 기록한 소프트웨어 상태다. 나머지 4개 역할의 가상 팀 이벤트는 `eventCode: synthetic-action-observed`, `state: notified`로 입력 사실을 통지하며 다른 역할의 퀘스트를 완료하지 않는다. 점수, 합격/불합격, 철도 업무 준수 판정이 아니다. 실제 절차, 역할명, 평가 기준의 승인 여부는 이 데이터나 검증 결과로 판단할 수 없다.
