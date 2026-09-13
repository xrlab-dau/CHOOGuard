# Graphify 전달 상태

2026-09-13 작업 범위는 **PM 오케스트레이션 계약과 코드 탐색 그래프**입니다. 음성·게임 기능이나 오픈월드 제작을 재개하지 않았습니다.

## 원격 develop 반영과 기존 작업 보존

PR #148 병합 commit `3861eab7404a7648182293a00b0ac2ac1e017a00`을 기준으로 기존 feature의 6개 커밋을 rebase했습니다. range-diff에서 6개 모두 동일 변경으로 대응했고 develop ancestry를 확인했습니다. 원래 HEAD 복구 branch와 named stash는 삭제하지 않았습니다.

기존 미커밋 작업은 비공개 복구 백업 후 복원했습니다. 충돌한 문서·context·builder의 양쪽 의도를 보존했고 unmerged/index 변경은 남기지 않았습니다. CI workflow 삭제 복원은 별도 권한 차단 때문에 수행하지 않았으며 upstream workflow를 유지했습니다. 이는 Unity compile 또는 시험 재실행 기록이 아닙니다. 비공개 복구 파일·개인 설정·원시 로그는 이 전달에 포함하지 않습니다.

Graphify 변경은 develop 기반 별도 일반 clone의 `feature/graphify-pm-context-20260913`에서 수행했습니다. 기존 음성 PR #153의 원격 branch는 강제 갱신하거나 병합하지 않았습니다.

## 작업 정본

- 실제 open issue 105개와 정본 105개를 대조했습니다.
- #149–#152의 산출물/phase/쓰기·검사 계약과 관련 선행·후행을 등록했습니다.
- #143의 diagnostic build-attempt와 실제 runnable payload를 구분했습니다.
- PR #153의 261경로는 exact ref의 Git blob과 bytes digest로 확인하고 source-only `qualifiedRefs`로 추가했습니다. accepted checkout baseline을 자동 충족시키지 않습니다.
- #70의 기존 실제 claim은 보존했습니다. 개인 사전배정은 추가하지 않았습니다.
- #106/#107은 현재 외부 생성·자동 AAA/REWORK 실행 보류를 명시하되 과거 기록을 지우지 않았습니다.

## Graphify

`graphifyy==0.9.61` wheel digest를 PyPI 메타데이터와 실제 bytes로 대조한 뒤 격리 Python 3.12.11에 설치했습니다. C# smoke fixture 후 실제 허용 목록 65개에서 로컬 AST를 추출했습니다. LLM API/semantic pass/외부 DB/watcher/hook은 사용하지 않았습니다.

원시 AST는 1,850 nodes / 5,114 edges입니다. 65개 파일 모두 심벌이 추출됐습니다. 정의가 없는 외부 심벌 168개와 endpoint 정의가 누락된 관계 282개를 발견하여 명시적 미해결 참조로 보존합니다. 통합 결과의 최신 수량은 [coverage.json](coverage.json)이 기준입니다. 이는 모든 소스의 정확한 해석이나 runtime coverage를 뜻하지 않습니다.

## 수행한 검사

- Node context 회귀: 228/228 PASS, skip 0.
- Python context 회귀: 34/34 PASS. Graphify adapter의 14개 경계 시험 포함.
- PM graph 구조 validator와 Graphify `assert_valid`: PASS. freshness/실행 수용은 별도 미판정.
- packet/ontology/PM view 109개 파일과 통합 graph/coverage 2개, 총 111개 파일의 동일 입력 재생성: byte digest 동일.
- #149–#152의 prepare/candidate/accept 총 12개 brief 조회: complete, authorization false.
- #150 candidate `RuntimeMetricAccumulator` 한정 조회: 34 nodes / 41 edges, 직접 미해결 이웃 3개 포함, 25,795 chars. 32,000-char 예산에서 complete이며 부족한 예산은 내용 없이 incomplete와 동일 symbol 후속 명령으로 반환.
- 한정 검토에서 재현된 PM/private 경로 누락, 문서 attribution 연결, scoped 미해결 참조 누락, code pointer 출처 자격 누락을 수정하고 실패 회귀시험 후 통과를 확인했습니다. #91 code pointer에도 source-only ref/digest/access/limits가 표시되며 accepted는 false입니다.
- 정본의 Requirement 15개는 명시된 작업 매핑 근거가 없어 여전히 미연결입니다. `unboundRequirements`로 노출하며 임의 연결하거나 온톨로지 매핑 완료로 보고하지 않습니다.
- Graphify 자체 query도 실행했으며 600-token 예산에서 truncation 경고를 실제 확인했습니다. 이를 작업 계약의 필수조건 조회로 사용하지 않습니다.
- GitHub 변경안 로컬 시뮬레이션: 이슈 105개 적용 후 재계획 변경 0, 원문·댓글·assignee 보존. 보드 448개 필드의 before 일치와 #70 상태/claim 보존 확인. 실제 원격 적용 시험이 아닙니다.

## GitHub 등록 경계

현재 전달 clone의 `origin`은 원본 로컬 저장소입니다. 이를 `https://github.com/xrlab-dau/CHOOGuard.git`으로 바꾸려는 단계가 명시적 원격 변경 승인 필요로 차단됐습니다. 다른 remote/직접 URL push로 우회하지 않았습니다.

따라서 **이 전달의 원격 push·새 PR·이슈 본문/보드 적용은 아직 하지 않았습니다.** 새 로컬 SHA를 실제 원격에서 읽히는 링크로 가장하지 않습니다. 게시 후 다음 변경안을 최신 snapshot으로 다시 생성·검증해야 합니다.

- 생성기 변경으로 갱신된 105개 packet의 이슈 본문 링크/조건부 관계. 이전 본문·댓글·실제 assignee 보존.
- 보드 Context/Dependencies/Successors/Parallel/Conflicts 및 새 작업 필드. 실제 status는 #106/#107 현행 보류만 조정.
- 남은 `machine:*`와 모호한 `owner: team` 라벨 제거. 실제 #70 claim/담당자는 유지.
- before-value drift 확인, 필드별 receipt, 원격 readback, 재실행 변경 0 검증.

## 미완료인 제품 검증

#149–#152 제품 결함 자체, 새 checkout Unity resolve/compile, 실제 마이크/PTT/LiveKit/WAN, 20-client·NPC100·동시사건2·60분 시험, Windows/Linux fresh Player, 오픈월드 완성과 Foundation 전체 수용은 미완료입니다. 구조·단위시험 성공이나 source-only 접근으로 이 상태를 바꾸지 않습니다.
