# JEV 연결 기록의 오프라인 진단

`diagnose_jev.py`는 이미 저장한 `JevClient` JSONL 사본을 읽어 HTTP 응답, 파싱된 답변의
질문 범위, 목적별 대기 시간을 집계한다. [Issue #280](https://github.com/xrlab-dau/CHOOGuard/issues/280)의
원인 구분을 위한 도구다. 네트워크에 요청하거나 재시도하지 않으며, 게임의 판단·예산·사건 합성은 바꾸지 않는다.

## 실행

Python 3.9 이상, 추가 패키지 없이 실행한다. 수집이 끝난 **변하지 않는 사본**을 명시한다.

```powershell
py -3 workers/learning/diagnose_jev.py 'D:\cg-data\jev-run\jev-shift-00.jsonl' --connection-path direct
```

파일 여러 개를 위치 인자로 줄 수 있다. 연결 경로는 `direct`, `relay`, `unspecified` 중
호출자가 지정한 값이며 도구가 판별한 결과가 아니다. 사설 중계 주소나 키는 입력하지 않는다.
출력은 JSON이고 경로·요청 본문·답변 내용·질문 ID·모델 이름·인증정보를 복사하지 않는다.
알려지지 않은 목적과 lane 문자열도 그대로 출력하지 않고 `other`로 묶는다.

공유할 결과를 저장할 때는 새 파일을 선택한다. 원본 JSONL에는 세계 상태 등 상세 정보가
있으므로 진단 결과와 같은 공개 범위라고 가정하지 않는다.

## 결과 해석

| 분류 | 관찰한 사실 |
|---|---|
| `complete_answers` | HTTP 200이고 기록된 모든 질문 ID에 파싱된 답변이 있음 |
| `partial_answers` | 일부 질문 ID에만 파싱된 답변이 있음 |
| `http200_without_parsed_answers` | HTTP 200이나 파싱된 답변이 없음 |
| `answers_coverage_unknown` | 답변은 있으나 원래 질문 목록이 없어 완전성을 확인하지 못함 |
| `unexpected_answer_ids` | 원래 질문 목록 밖의 답변 ID가 있음 |
| `http_auth_rejected` | HTTP 401 또는 403. 인증·권한 거부 응답이며 키 자체의 오류로 단정하지 않음 |
| `http_rate_limited` | HTTP 429. 전송 전 게임 내부 예산 제한과 구분 |
| `http_server_error` | HTTP 5xx. 원격 서버/중계 등의 오류 응답이며 발생 지점은 별도 확인 |
| `no_http_response` | HTTP 코드 0 또는 -1. 시간 초과·DNS·TLS·취소 중 무엇인지 이 필드만으로 구분 불가 |
| `other_http_status` | 나머지 유효 HTTP 코드 |

답변 범위 검사는 클라이언트가 이미 파싱해 기록한 객체의 ID를 비교한다. 답변의 의미·확률·
유효 범위를 재검증하거나 원래 서버 응답을 복구하지 않는다. 기존 `validate_capture.py`의
"HTTP 200 + 비어 있지 않은 답변" 집계보다 부분 답변을 자세히 분리하며, 기존 수집 검증은 그대로 유지한다.

목적별 `seconds`는 표본 수, 중앙값, **nearest-rank 방식 p95**, 최댓값이다. 소수 표본의
p95를 장기 성능으로 일반화하지 않는다. `JevClient`의 기록된 대기 시간이며 요청 전 대기까지
포함한 사용자의 전체 지연이라고 가정하지 않는다.

`response_transitions`는 **파일·목적별** 응답 완료 시각을 정렬해 연속 미완전 응답과 이후
완전 응답을 센다. 파일 쓰기는 백그라운드에서 뒤섞일 수 있어 줄 순서로 복구를 판단하지 않는다.
시각이 없거나 같아서 순서를 확정할 수 없을 때, 목적이 `other`일 때, 입력이 손상됐을 때는
이 수치를 제공하지 않는다. 다른 파일 사이를 하나의 연속 실패로 잇지 않는다. 완전 응답이
다시 관찰돼도 특정 요청의 재시도 성공이나 게임의 판단 흐름 회복을 입증한 것은 아니다.

`evidence_valid`는 자료를 해석할 수 있었는지 나타낸다. 잘린 마지막 줄, 잘못된 UTF-8/JSON/
필드, 읽는 중 변경, 중복 파일 등은 false이며 문제 코드는 원문 없이 기록한다. 같은 SHA256의
사본은 totals에 한 번만 센다. 손상된 파일에서도 분류 가능한 줄의 집계는 진단용으로 남으므로
**false인 결과의 수치를 정상 수집 통계로 사용하지 않는다**.

같은 JSON 객체 안에서 항목 이름이 중복되면 마지막 값으로 덮어쓰지 않고 입력 오류로 거부한다.
이는 중첩된 질문·답변 객체에도 적용하며, 중복된 이름이나 원문은 오류 결과에 포함하지 않는다.
`validate_capture.py`도 같은 파서를 사용하므로 수집 요약과 JSONL에 동일한 기준을 적용한다.

종료 코드 **0은 자료 읽기·집계 완료**, 1은 입력 증거 문제다. 모든 요청이 HTTP 504여도
형식이 올바르면 0이 될 수 있다. 서비스 정상 판정이나 학습 자료 합격은 `evidence_valid`의 의미가 아니다.

## 이 기록만으로 알 수 없는 것

현재 `JevClient.Ask`는 키/가용성, in-flight 한도, 예산, 준비 실패로 전송 전에 돌아간 요청을
JSONL에 쓰지 않는다. 따라서 **로그에 없다고 예산 차단 0건이라고 추정하면 안 된다**.
수집 요약의 `director.unanswered_rounds`, pending/drained 상태와 `validate_capture.py` 결과를 함께 본다.
예산 증가나 재시도 빈도 변경의 근거로 HTTP 실패 집계만 사용하지 않는다.

2026-10-13 이 작업에서는 현재 실행의 실제 JSONL을 확보하지 못했다. 분류·시간 순서·중복·
잘린 파일·민감 문자열 배제는 10개 계약 검사로 확인했다. Issue #280에 적힌 과거 5분 기록의
실패율을 현재 실패율로 보고하지 않았으며, 직접 API/중계 비교와 실제 회복 검증은 여전히 필요하다.

```sh
python -m unittest discover -s workers/learning -p 'test_diagnose_jev.py' -v
```
