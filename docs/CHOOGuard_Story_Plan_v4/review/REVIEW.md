# 실행 스토리·단기 계획 검수 기록

## 검수 범위
업로드된 클린 스타트 v3를 바탕으로 스토리 분할·실행 순서·문맥·온톨로지·준비도 조회기를 검수했다. 제품 구현·Unity·물리·사용자 시험은 수행하지 않았다. 같은 작성자의 자체 검수이며 독립 LLM/기관 심사가 아니다.

## 분할 기준
48개 부모의 구현·시험 계약을 읽고 관찰 가능한 결과와 별도 반례 단위로 109개 story를 정의했다. 함수 작성 한 줄씩을 이슈로 만드는 식으로 쪼개지 않았고, 묶인 화면/세션 작업은 추가 분할했다. 첫 초안 107개에서 상황 세션 재사용·다음 판단 지점 진행·native 설정 화면을 세 결과로 나눠 109개로 고쳤다.
부모의 직접 요구 소유권과 지원 요구를 구별한다. 모든 helper를 직접 소유자로 바꾸지 않았다. 91개 제품 요구 및 기존 제품 AT 내용은 보존했다. 전체 부모 완료에는 자식과 원래 통합/자격 검수가 별도로 필요하다.

## 실제 발견과 수정
| 차수 | 관측 | 수정/재시험 |
|---|---|---|
| 초기 스키마 | 기반 작업의 직접 요구 배열이 비어 있다는 이유로 오류. 원본에는 supportsRequirementIds가 있음 | 직접 소유와 지원 요구를 별도 필드로 보존하고 합집합의 시험 연결 검사. 검사 기준을 단순 삭제하지 않음 |
| R1 | 41개 테스트 중 3개 반례 실패: 외부 입력 없이 ACCEPTED, 상충 external 상태 중복, own candidate 없이 integration receipt | 입력 증거·unique external·자기 선행 단계 검증을 추가 |
| R2 | 41개 통과 | 중복 accepted receipt와 receipt 의존 순환도 방어 |
| R3 | 단기 candidate뿐 아니라 integration 선행조건 닫힘, 명시적 spec 경로 존재 시험을 추가 | 43개 최종 통과. 전 스토리의 공통 계약 hash도 requiredReads에 결속 |

## 현재 확인한 결과
43개 Python 자동시험은 계획 참조·프로필 단계 DAG·원문 링크 바인딩·경로/.meta 충돌·준비도·합성 실행기록 검사 범위다. 실제 제품 테스트가 아니다.
fixture 프로필에서 109 story × 4 phase = 436개의 문맥을 생성해 최대 크기와 필수조건을 확인했다. 6개 profile의 단계 그래프는 각 436 node이며 순환이 없었다. 이 그래프 통과가 실제 입력 확보·일정 가능성·실행 권한을 뜻하지는 않는다.
`review/final-summary.json`, `final-tests.txt`, `context-matrix.json`, `document-drift.json`에 실제 결과를 보관했다.

## 표준 검증 상태
- JSON Schema: story/progress 구조 검사 실행.
- JSON-LD/RDF: rdflib로 파싱, story 수·실제 Execution 활동 미생성 확인.
- Turtle vocabulary/shapes: 문법 파싱.
- SHACL engine: **NOT_RUN**. pyshacl 설치를 시도했으나 패키지 저장소 DNS 연결 실패. shapes 파일의 문법과 응용 검사 결과를 SHACL engine 통과로 표시하지 않음.
- 라이브러리 버전은 final-summary에 기록. 제품 Unity·SQLite·solver 설치 버전의 승인 증거가 아님.

## 정적 검수의 제한
readiness 도구는 제출된 record와 파일 hash·단계·profile을 검사할 뿐 원시 로그의 진실성, reviewer 권한, 실제 독립 심사, 현실 모델 정확도까지 인증하지 않는다. state/progress.json은 비어 있고 제품 산출물이 만들어진 것으로 표시하지 않았다. 해시는 데이터 진실성·안전 인증이 아니다.
단기 28개 story는 첫 native 요청·실제 디스크·복구 범위다. 전체 운영 공조·A/B·대본·정밀 현장 수용은 후속 story에 남아 있다. W0–W3을 일수나 주수로 환산하지 않았다.

## 변경하지 않은 것
원격 저장소·GitHub 이슈·할당·코드·제품 데이터는 수정하지 않았다. 과거 저장소 초기화나 삭제도 수행하지 않았다. basis/v3의 128개 포함 파일은 업로드된 기준과 바이트가 같다. 이들 중 출처 링크/모델 후보는 이번 작업에서 새로 내려받거나 현행성을 검증하지 않았다.
