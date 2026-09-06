# 독립 검토 지적 처리 기록

- 날짜: 2026-09-06. 원본 실행: wf_97c85971-263.
- 유형: 컨트롤러의 정제된 사후 처리 기록. 독립 승인·실행 영수증이 아니다.
- 원본 구조화 결과: 리뷰어 14개, 지적 105개. 명세 관점 1개 실패, 수정 에이전트 5개 실패, 워크플로 내 재검토 0개.
- 원문 프롬프트·계정·절대 경로·tool transcript는 재게시하지 않는다. 개별 지적 키는 `bundle/lens/id`다. 아래 행에 여러 ID가 있으면 각각의 출처를 보존한 동일 처리군이며 원본 지적을 삭제·합치지 않는다.
- 상태 기록: [workflow-status](2026-09-06-workflow-status.json).

## 상태의 뜻

- **문서 반영, 재검토 대기:** 요구·설명·계획을 정정했다. 코드 구현이나 독립 승인 완료를 뜻하지 않는다.
- **부분 반영, 실행 보류:** 문서는 고쳤지만 실행 코드·검사·라이선스 증거가 미완료다.
- **승인 필요:** 보호 경로·권한·학교 PC·자료 접근의 실제 결정이 필요하며 문서로 대체하지 않는다.
- **실행 차단:** 테스트·검증·git 변경 명령이 도구 안전 판정 서비스 장애로 실행되지 않았거나 실제 실행 결과가 없다. 실패 테스트를 관측한 RED와 다르다.

모든 처리군은 새 불변 manifest의 독립 재검토 전 미종결이다. 최초 manifest 부재를 사후 hash로 소급 보완하지 않는다. 여기서 “반영”은 컨트롤러의 자체 서술이며 리뷰어 판정이 아니다.

## 요구사항 발견과 명세 001·002

| 관점·원본 ID | 정제 요지 | 처리 | 대상 |
|---|---|---|---|
| spec SPC-1 | 위생 검사는 비민감 분류를 증명하지 못함 | 문서 반영, PM 입력 분류·증거 미완료 | 001 FR-001, 학교 PC 절차 |
| spec SPC-2, SPC-3 | 기준선 상호 인용·추적의 존재를 잘못 가정 | 카탈로그 중심으로 범위 결정, O/D·충돌 목록 추적 요구 정정. 링크 검사·decision-trace 미완료 | 002 FR-001~007, README, 발견 §5 |
| spec SPC-4 | mount 검사 범위·OS 증거 불명 | 문서 반영, 실행 보류 | 001 FR-002 |
| spec SPC-5 | Windows 가정 영향 과소평가 | 문서 반영, 재검토 대기 | 발견 A-01, 학교 PC 절차 |
| spec SPC-6 | 전역 CODEOWNERS만으로 통과 가능 | 명시적 last-match와 승인 리드 요구. 실제 변경 승인 필요 | 001 FR-004·SC-002 |
| spec SPC-7 | M1-04 순번이 선행 관계와 모순 | M2-01 뒤로 카탈로그 정정 | specs/README |
| spec SPC-8 | DoD 12 누락 | 각 명세에 변경 후 재검토·증거 보존 적용 | 001~004 |
| safety SAF-1 | 보호 소유권 미정 | 승인 필요, CODEOWNERS 미변경 | 001 FR-004, 감사 G-01 |
| safety SAF-2 | 패키지 선언과 M0-00 실행 금지가 모순 | 사람 정적 검사와 공급망·실행 승인 분리. Pi 설정 미변경 | 001 FR-007, ADR 0002 |
| safety SAF-3 | 학교 PC 외부 네트워크 기본 허용 | 기본 거부로 문서 정정, 실제 프로파일 승인 필요 | 발견 A-03·D-03, 학교 PC 절차 |
| safety SAF-4 | clone을 PR 승인 우회로 기술 | 검토용 clone과 승인된 불변 커밋 실행 분리 | 발견 A-10, 감사 G-11 |
| safety SAF-5 | v3 고지 위치 미정 | README 첫 20줄로 결정. 외부 경로 추측 제거 | 002 FR-003, README |
| safety SAF-6 | 촬영 확장자·미추적 입력 검사 누락 | 명세 반영, 코드 시험 미완료 | 001 FR-002~003 |
| safety SAF-7 | 거버넌스 hash 누락 | 단일 POLICY_GLOBS 요구. 생성기 미수정 | 001 FR-006, 004 FR-009 |
| safety SAF-8 | 기준선 상호 인용 가정 오류 | README 카탈로그로 문서 반영 | 002 FR-001 |
| adversarial ADV-1, ADV-2, ADV-3 | ignored 자료·mount/junction·확장자 누락 | 명세 반영, OS·자료 검사 미완료 | 001 FR-001~003 |
| adversarial ADV-4 | clean clone에 새 파일이 없음 | 커밋·원격 전달을 선행 단계로 명시. 실제 전달 상태는 git 결과로만 판정 | 인계 계획 Task 1 |
| adversarial ADV-5 | 기준선 등록의 미완료 과소평가 | 카탈로그·링크·hash·추적·재검토를 별도 조건으로 분리 | 002 전체 |
| adversarial ADV-6 | Windows python3 고정 명령 | 관리자 확인 Python/py 실행기와 미설치 실패를 명시 | 001 Assumptions, 학교 PC 절차 |

## 촬영 질문과 도구 허용목록

| 관점·원본 ID | 정제 요지 | 처리 | 대상 |
|---|---|---|---|
| safety SAF-1 | 민감자료 처리·verifier·reviewer 주체 누락 | 실행 주체 목록 보완, 실제 프로파일 미확정 | 004 FR-005 |
| safety SAF-2 | 촬영 용도·재사용 제한 누락 | 문서 반영, 질문·양식 구현 미완료 | 003 FR-001~003 |
| safety SAF-3, SAF-4 | 합성 선언뿐이며 전송·정제 증거 부족 | 모델 밖 경계·실제 제공자·등급·hash·unknown·사고 기록 요구 | 003 FR-004~008 |
| safety SAF-5 | npm lock·integrity hash 누락 | 추적 직접 manifest·전체 lock·integrity 요구, 실제 산출 미완료 | 004 FR-009 |
| safety SAF-6 | 목적지·포트·프로토콜 경계 누락 | 세부 열·기본 거부 명시, 환경 승인 필요 | 004 FR-005 |
| safety SAF-7 | reviewer와 reducer 스키마 혼동 | 계층·필수 필드 정정, 런타임 수정 보류 | 004 FR-003, ADR 0003 |
| safety SAF-8 | 단순 BT 유형으로 우회 경로 누락 | 보호 대상×자료등급×실행 경로, 고정 argv·비적용 근거 요구 | 004 FR-008 |
| spec SPC-1 | 판정 스키마 불일치 | 문서 반영, 코드 검증 미완료 | 004 FR-003 |
| spec SPC-2 | slim·MCP 어댑터 누락 | 직접 의존성·미채택 행 요구 | 004 FR-001 |
| spec SPC-3 | 미정 런타임을 고정값처럼 요구 | 호환 조건·정확 pin·미정 구분 | 004 FR-001 |
| spec SPC-4 | 정책 hash 범위 누락 | 단일 출처와 필수 누락 실패 요구 | 004 FR-009 |
| spec SPC-5 | 승인 종류 축소 | KORAIL·보안·라이선스·병합·공개·삭제 범위 복원 | 004 FR-006~007 |
| spec SPC-6 | verifier를 순수 읽기 리뷰어로 오기술 | 불변 소스와 격리 생성·빌드·로그 쓰기 분리 | 004 FR-003 |
| spec SPC-7 | 모든 질문에 MAP와 O 강제 | 의미 있는 선택 참조와 N/A 근거로 정정 | 003 FR-002 |
| spec SPC-8 | 체크리스트의 성급한 완전성 승인 | 자체 서술 점검과 미완료 실행·승인 체크 분리 | 003·004 checklists |
| adversarial ADV-1 | 학교 PC 환경 미정 상태에서 프로파일 완료 가능 | 미확정 프로파일 M1 실행 금지 | 004 FR-005 |
| adversarial ADV-2 | 조사 의존성 hash 누락 | pyproject·uv.lock 명시, 생성기 미수정 | 004 FR-009 |
| adversarial ADV-3 | pydantic-ai-slim 누락 | 직접 의존성 등록 요구 | 004 FR-001 |
| adversarial ADV-4 | uv·Node 고정값 없음 | 승인 미정으로 명시, 임의값 채우지 않음 | 004 FR-001 |
| adversarial ADV-5 | 순수 reviewer에 bash 쓰기 경로 | reviewer/verifier 경계 구분, OS 검증 미완료 | 004 FR-003 |
| adversarial ADV-6 | 실제 JSON 계약 불일치 | 문서 반영, 실제 provider·schema 시험 필요 | 004 FR-003 |
| adversarial ADV-7, ADV-8 | 중간·정제 저장 경계와 용도 누락 | 문서 반영, 양식 구현 미완료 | 003 FR-001~003 |

## ADR과 기술 조사

| 관점·원본 ID | 정제 요지 | 처리 | 대상 |
|---|---|---|---|
| safety SAF-1 | direct curl fallback이 승인 경계 우회 | 미래 fallback 제거, 과거 조회 사실·승인 증거 부재 보존 | ADR 0004, 조사 기록 |
| safety SAF-2, SAF-3 | 조사 미정제 게시·임의 모델 | 격리 출력·정제·공개 승인·모델 허용목록 요구. 코드 미수정·실행 보류 | ADR 0004 |
| safety SAF-4 | 보호 정책·증거 소유권 누락 | 승인 필요, 운영 보류 | ADR 0002, 감사 G-01 |
| safety SAF-5 | JSON은 구조화 연구 결과가 아님 | raw_search_log 라벨과 별도 수동 claims JSON. 실제 Researcher 미실행 명시 | docs/research |
| safety SAF-6 | 설치 무결성 기록 없이 재현 주장 | 후보·승인·전체 lock 구분, 공급망 미완료 | ADR 0002 |
| safety SAF-7 | 사람 처리 뒤 재검토 생략 가능 | 새 manifest 독립 재검토 필수 | ADR 0003 |
| safety SAF-8 | 학교 PC 모델·Exa 기본 차단 누락 | 문서 반영, 실제 네트워크 변경 없음 | ADR 0005 |
| spec SPC-1 | 실제 pi-agents 제공자 강제 미증명 | modelScope·watchdog 선언과 M1-07 검증 분리 | ADR 0003 |
| spec SPC-2 | 연구 전송 gate 미충족 | 요구와 현 코드 차이 명시, 실행 보류 | ADR 0004 |
| spec SPC-3 | 연구 JSON 스키마 주장 오류 | 원시 로그·수동 주장·Researcher 결과 구분 | docs/research |
| spec SPC-4 | 로컬 Spec Kit 브랜치 생성 주장 반대 | 전체 script 정적 읽기로 정정. upstream byte 동일성은 미확인 | C-06, 카탈로그 |
| spec SPC-5 | 초기 리뷰+max 3은 총 4회 | 불일치 명시. 총 3라운드 목표 유지, 보호 YAML 미변경 | ADR 0003 |
| spec SPC-6 | unity-mcp 설치 게이트 순환 | M1-03·M2-01 완료 뒤 M1-04 연결 | ADR 0002·0005 |
| spec SPC-7 | AIN·Unity 마일스톤 오참조 | AIN-01, M1-01 세션 계약, M2-01 Unity, M1-05 기록 단위로 정정 | ADR·발견·그래프 |
| spec SPC-8 | 판정 저장 주체·절차 없음 | controller 정제·스키마·append-only·저장 실패 차단 요구. 현 YAML 저장 미구현 | ADR 0003 |
| adversarial ADV-1 | 금칙어 계약을 하네스가 충족 못함 | 블랙리스트는 분류 증명 아님을 명시. sys.exit 문자열의 실제 종료 코드는 1로 정정. 코드 미수정 | ADR 0004 |
| adversarial ADV-2 | reviewer 차단 규칙의 실제 적용 미확인 | M1-07 시험 필요, 운영 승인 금지 | ADR 0003 |
| adversarial ADV-3 | 판정 저장 경로 없음 | 위 저장 계약 요구, 실행 구현 미완료 | ADR 0003 |
| adversarial ADV-4 | 연구 JSON 구조 불일치 | 수동 정정 기록 추가, 하네스 결과로 위장하지 않음 | docs/research |
| adversarial ADV-5 | 학교 PC에서 local 조사·자격 기본 실행 | 설치·로그인·조사 기본 안내 제거, 스크립트 미수정·실행 보류 | 학교 PC 절차 |

## Pi 개발 명세와 설정

| 관점·원본 ID | 정제 요지 | 처리 | 대상 |
|---|---|---|---|
| spec 관점 실패 | Prompt is too long | cannot_proceed 유지. 나머지 판정으로 승인하지 않음 | workflow-status |
| safety SAF-1 | 보호 경로 실제 쓰기 차단 없음 | OS 경계 요구, 보호 설정 승인 필요 | 개발 명세·004 |
| safety SAF-2 | 자유 tests 문자열을 셸로 실행 | 고정 test ID/argv 요구. 현 YAML 미변경·운영 보류 | 개발 명세 §3 |
| safety SAF-3, SAF-4, SAF-5 | 임의 모델·블랙리스트·미정제 연구 출력 | ADR 계약과 현 코드 차이 명시, 실행 보류 | ADR 0004 |
| safety SAF-6 | 필수 오류 후 승인 가능 | cannot_proceed 요구, reducer 수정 승인·시험 필요 | ADR 0003 |
| safety SAF-7 | 불변 target manifest 없음 | 시작·종료·적용 hash 계약 요구. 최초 검토 귀속 미검증 보존 | ADR 0003, workflow-status |
| safety SAF-8 | 소유자·불변 lock 누락 | 보호 승인·공급망 작업 미완료 | 004, 감사 |
| adversarial ADV-1 | 미승인 뒤 verifier 진행 | 현 동작과 목표 gate 차이 명시, YAML 운영 보류 | 개발 명세 §3 |
| adversarial ADV-2 | 실제 reviewer 모델 강제 없음 | 선언과 실제 검증 구분 | 개발 명세 §1 |
| adversarial ADV-3 | plan-review가 보호 경로 수정 가능 | 보호 변경 사람 승인, 문서만으로 강제 완료 주장 금지 | 개발 명세 §4 |
| adversarial ADV-4 | research 인자 문자열이 flags 무력화 | 프롬프트 미수정, 실행 보류와 고정 입력 계약 요구 | 개발 명세 §4 |
| adversarial ADV-5 | 중첩 .env 위생 검사 누락 | 명세·회귀 테스트 요구, 보호 CI 미변경 | 001, 분석 F-02 |
| adversarial ADV-6 | 보호 목록이 문서·헌법에서 다름 | 최소 범위·추가 보호 PM 결정, 실제 보호 설정 미변경 | 004 FR-006 |

## 부트스트랩·거버넌스

| 관점·원본 ID | 정제 요지 | 처리 | 대상 |
|---|---|---|---|
| adversarial ADV-1 | 공개 문서 개인 계정 | 감사·계획의 로그인 제거, 역할·비공개 매핑으로 정정 | 감사·인계 계획 |
| adversarial ADV-2 | clone에 새 파일 없음 | 전달 선행 명시, 커밋·푸시는 실제 도구 결과 필요 | 인계 계획 Task 1 |
| adversarial ADV-3, ADV-4 | mount/junction·policy 결과 미입증 | 현 영수증 한계와 사람/OS 검사 요구 | 학교 PC 절차·001 |
| adversarial ADV-5 | PowerShell native 실패를 OK 표시 | native exitcode 보완·시험 전 실행 금지 | 인계 계획 Task 2 |
| adversarial ADV-6 | 권한·네트워크 실패를 repo만으로 해결 못함 | 관리자 결정·기본 거부, Bypass·사용자 경로 설치 우회 제거 | 학교 PC 절차 |
| adversarial ADV-7 | 비밀 검사 실패 시 값 출력 | grep 원문 출력 명령 제거, 경로·수만 보고 요구 | 인계 계획 |
| adversarial ADV-8 | 에이전트 없음 서명은 허위 | 초안 사용 사실과 실제 사람 검사·서명 분리 | 인계 계획·001 FR-009 |
| safety SAF-1 | 공개 GitHub 로그인 포함 | 문서 반영, 재검토 대기 | 감사·계획 |
| safety SAF-2 | gate 전 원격 자동 설치 | 문서 실행 안내 제거. 기존 wrapper·Pi 설정 미수정·운영 보류 | 학교 PC 절차 |
| safety SAF-3 | receipt 경로 이탈·덮어쓰기 | 합성 회귀 테스트 작성, 실행 차단. --write 사용 금지 | scripts/bootstrap/test_verify_toolchain.py |
| safety SAF-4 | .env 변형 누락 | 테스트 작성, 실행 차단 | 같은 테스트, 001 FR-003 |
| safety SAF-5 | 필수 도구 누락도 성공 | uv/환경/exit 테스트 작성, 실행 차단 | 같은 테스트 |
| safety SAF-6 | 거버넌스 hash 누락 | 범위 테스트 작성, 실행 차단 | 같은 테스트, 001 FR-006 |
| safety SAF-7 | 패키지·venv 링크 예외 | 패키지 루트 링크 테스트 작성, 실행 차단. venv·OS 범위 보완 필요 | 같은 테스트 |
| safety SAF-8 | 자유 machine-label에 개인정보 | 허용값 요구, 실제 parser·시험 보완 미완료 | 001·학교 PC 절차 |
| spec SPC-1 | 사람만 실행 조건·agent 지시 충돌 | 자동 실행 header 제거, 초안·사람 승인 구분 | 인계 계획 |
| spec SPC-2 | 전달 commit/push 단계 없음 | 명시 경로 stage·검증·commit·push·원격 확인 선행 | 인계 계획 Task 1 |
| spec SPC-3 | 중첩 환경 파일 우회 | 명세·테스트 요구, 보호 CI 미변경 | 001·분석 |
| spec SPC-4 | 영수증이 전체 정책·mount 증거 아님 | required_ok의 제한과 별도 사람 증거 명시 | 001·학교 PC 절차 |
| spec SPC-5 | bootstrap 실패 성공 종료 | 회귀 테스트 RED 미확보, 생산 코드 미수정 | 인계 계획 Task 2 |
| spec SPC-6 | 설정 키 최상위만 검사 | 재귀 검사 요구, 코드·시험 미완료 | 인계 계획 Task 2 |
| spec SPC-7 | receipt hash가 HEAD에 귀속 안 됨 | clean 대상·미커밋 manifest·새 증거 요구, 기존 receipt 보존 | 001 FR-006, workflow-status |
| spec SPC-8 | 제공자·승인·hash·서명 필드 누락 | 조사·승인 3종·대체자·전체 hash·실제 확인 FR 보완 | 인계 계획 Task 4·5 |

## 구조화 목록 밖 보충과 잔여 작업

원본 workflow 외 agent 메시지는 구조화 결과의 105건에 자동 합산하지 않았다. 보충으로 보고된 계정 정제·증거 덮어쓰기·학교 PC 자격·재귀 설정 키·사후 승인 위험은 위 처리군에 반영했다. 메시지 전문과 개별 추가 ID의 완전한 대조는 미완료이며 새 지적을 임의로 생성하거나 원본 수에 포함하지 않는다.

- 보호 경로·CODEOWNERS·네트워크·자료·승인 원본은 사람이 결정해야 한다. 현재 세션은 실제 변경·서명하지 않았다.
- 생산 코드 수정은 실행된 RED 미확보로 보류다. 테스트 실행 도구의 장애를 회귀 테스트 실패로 기록하지 않는다.
- 도구·vendored 파일은 원문 라이선스·고지·불변 설치 manifest 검토가 끝나지 않았다. 실행 가능한 학교 PC 번들로 배포 승인하지 않는다.
- 수정된 문서에 대한 새 독립 검토도 스키마·실제 모델·불변 대상 검증 전에는 승인 영수증이 아니다.
- git 전송 상태는 별도의 최종 실행 결과를 따른다. 본 문서의 “전달 선행 추가”는 커밋·푸시 성공 주장이 아니다.

## 후속 정적 점검과 전달 결과

컨트롤러가 원본 journal에서 추출한 지적 ID를 묶음·관점별로 처리표와 수동 대조했다. 구조화 105건의 키가 대응하며 추가 agent 메시지 전체 대조와 자동 비교는 여전히 미완료다. JSON 파싱·선택 불변식과 추적 diff 공백 검사는 통과했다. 실제 상대 Markdown 링크는 본문과 파일 목록으로 수동 확인했으나 자동 링크·fragment 검사기의 구현·실행 증거는 아니다.

ADR 0003의 총 검토 횟수, 작업 그래프의 문서 workflow와 M0-00 증거 요약, 명세 001의 수용 시나리오·오래된 설치본 재실행 안내, 발견 문서의 설치 승인 기본값·보호 목록, 조사 Markdown의 최신 릴리스 단정을 추가 정정했다. 이 정정은 새 독립 검토 승인이 아니다.

후속 읽기 전용 리뷰 요청은 Agent 도구의 안전 판정 서비스 시간 초과로 agent 생성 전에 차단됐다. 테스트와 저장소 위생 검사도 프로세스 시작 전에 차단됐으므로 FAIL·RED·GREEN을 기록하지 않는다. 검토한 비보호 문서만 명시한 git stage도 같은 장애로 실행되지 않았다. 이후 인덱스가 비어 있고 HEAD가 기존 커밋임을 확인했다. 새 커밋·푸시는 미실행이며 원격 조회도 차단됐다. 보호 설정·도구·증거·조사 파일을 삭제하거나 완료 상태로 바꾸지 않았다.

## 검증 재실행 결과 (2026-09-06, 후속 구간)

이전 구간에서 차단됐던 실행을 같은 날 다시 수행했다. 안전 판정 서비스 시간 초과는 재발하지 않았다.

- 테스트 실행: `python3 -m unittest discover -s scripts/bootstrap -p 'test_*.py' -v`. RED 12개 중 11 실패 → 테스트 2개 추가 후 14개 중 13 실패 → `verify_toolchain.py` 재작성 후 14/14 GREEN → bootstrap 테스트 2개 추가 RED(승인 없는 `uv sync`, 라벨 미검증) → `bootstrap.sh` 수정 후 16/16 GREEN.
- 수정한 생산 코드: `scripts/bootstrap/verify_toolchain.py`(returncode·`ls-files -z`·`.env` 변형·중첩 설정 키·저장소 밖 symlink·영수증 경로·필수 항목 exit 1), `scripts/bootstrap/bootstrap.sh`(라벨 필수·도구 호출 전 중단, `AUTO_INSTALL=1` 없으면 `uv sync` 안내만), `scripts/bootstrap/bootstrap.ps1`(같은 정책, `exit $LASTEXITCODE` 전파). ps1 은 pwsh 부재로 정적 검토만 했다.
- 검증기 스모크: `--label local`, `--write` 없음, 필수 8항목 ok, `unity_project` FAIL(비필수), exit 0.
- 저장소 위생: 커밋 전 추적 파일 35개 검사, 위반 0. 미추적 파일은 커밋 전에는 검사 범위 밖이다.
- 원격: 작업 브랜치는 원격에 없었다.
- stage 범위: 보호 경로(`.pi/`, `.specify/`, `docs/evidence/`)·vendored 도구(`.agents/`, `_bmad/`)·실명이 포함된 `.memlog.md` 는 제외했다. 나머지 후보에서 제한 패턴 매치는 없었다.
- 미실행: 수정본에 대한 다른 제공자의 독립 재검토, ps1 실행 검증, `tools/research/research.py` 결함(자유 `--model`·`--topic`, 금칙어 종료 문자열, 출력 경로) 수정.

이 절은 실행 관측 기록이며 승인 영수증이 아니다. 커밋·푸시 결과는 git 이력과 최종 보고를 따른다.
