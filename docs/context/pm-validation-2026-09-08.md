# PM 담당 작업 일괄 검증 · 2026-09-08

기준 `develop`: `e7a6bb7a7fff0c301c04f93aeea8850dbde34779`. [개발 실행 보드](https://github.com/orgs/xrlab-dau/projects/1)와 [개발 준비·승인 보드](https://github.com/orgs/xrlab-dau/projects/2)의 PM 담당·연관 항목을 확인했다. 이 기록은 이번 실행 범위이며 과거 학교 PC 영수증을 갱신하지 않는다.

| 이슈·범위 | 이번 결과 | 남은 조건 |
|---|---|---|
| #52 · R-07 | [PR #97](https://github.com/xrlab-dau/CHOOGuard/pull/97): 정책 기준선·외부 핀 비교, 미승인 단계, 순회 예산·링크·정확 파일 예외, RED/GREEN 증거 | 실제 정책 승인과 다른 제공자의 spec/adversarial/safety 검토, 105키 원본 또는 검증 불가의 명시적 사람 수용 |
| #52 항목 7·8 | [PR #98](https://github.com/xrlab-dau/CHOOGuard/pull/98): 별도 R-07-GRAPH 단위, 29개 노드·M5 누락·잘못된 간선 정정, 기존 M4-01 스키마 강제 연결 | 별도 문서 검토·수용 |
| #44 · Windows 회귀 | Windows Server 2022 CI 실행, PowerShell 8개 및 실제 junction 차단 통과. Linux CI도 통과 | 학교 PC ACL·mount 범위, 독립 제공자 검토. 호스팅된 Windows를 학교 PC로 표시하지 않음 |
| #36 · 자동 검사 | Foundation 31, Context 17, Art 11, CI 정책 7, profile 9, 사진 참조 처리 19개 통과. 재구성 91개 통과·RSS 감시 1개 환경 실패 | Unity 시험 및 전체 수용 경로. RSS 감시 시험은 `/proc` 없는 현재 환경에서 실패를 유지 |
| #24·#42 · 실행 환경 | 사용자 설치 지시에 따라 Unity 6000.3.23f1, 공식 CLI 1.0.0-beta.8, Windows Mono 지원, Blender 4.5.9 설치. 버전 실행과 공식 다운로드 체크섬 확인 | Unity EditMode는 라이선스 IPC 초기화·Package Manager 경로/연결 오류로 결과 생성 전 종료. 이 환경의 `/proc/self/exe` 부재도 관측. 학교 PC 로그인·HMD 결정은 별도 |
| #54·#65 · 기존 Blender 자산 | 설치된 Blender에서 추적된 장면을 읽음: 메시 125개, 삼각형 81,094개, 비정상 좌표 0. manifest의 총 삼각형 수와 일치 | 새 시각 비교·Unity import·플레이 완주·실시설 정확성 검증은 수행하지 않음 |
| #55·#64·#68 · 인계 | 구현·검사·문서 변경을 구분해 현재 소스와 증거 연결. 두 보드의 #52 추적 정합성 보완 | 다른 개발자의 #69 재현, 팀원 수용 및 독립 검토 |
| #13·#14·#41·#43 및 외부/실물 대기 작업 | 기존 초안·정책·검토 실패·보류 이유를 확인 | 사람 정적 분류·기관 회신·정책 소유권 결정·HMD·현장 수용을 에이전트가 대신 승인하지 않음. 기관 질문을 외부로 전송하지 않음 |
| #66 · CV 후속 | 재구성·참조 처리 회귀 검사 실행 | 이전 개인 기기의 입력/중간 결과가 이 작업본에 없어 CV-01~06 실제 데이터 실행을 재현하지 못함 |

## 새 실행 증거

- 검증기 구현과 해시 결속: [R-07](../evidence/R-07/README.md). 최신 로컬 부트스트랩은 68개 중 58개 통과·10개 제외다. 실제 Windows·Linux CI의 커밋 범위는 각각의 실행 기록을 따른다.
- 설치·Blender 정적 기하·추가 검사: [PM 실행 관측](../evidence/PM-VALIDATION/run-20260908-01.json).
- 최종 검증기의 정책 누락 합성 시험: [19개 계열 누락 차단](../evidence/R-07/policy-scope-20260908-03.json). 승인된 실제 프로젝트 기준선 시험이 아니다.
- 작업 그래프의 별도 대상은 PR #98의 `docs/evidence/R-07-GRAPH/target-20260908-01.json`이다.

Unity와 Blender는 현재 Linux 작업 환경의 사용자 설치 경로에 설치했다. Windows 실행기는 GitHub Actions의 `windows-2022`로 구성·사용했다. Windows 운영체제나 학교 PC를 이 Linux 환경에 설치했다고 주장하지 않는다. Unity Console 0건, EditMode/PlayMode PASS, Windows 게임 실행, HMD 성능 PASS를 만들지 못한 상태로 명확히 남긴다.

이슈 종료·Done·정책 승인·독립 검토 PASS·PR 병합은 수행하지 않았다. 구현 가능한 부분을 PR로 제출하고 실제 완료 조건과 연결된 보드의 상태·차단 이유를 갱신한다.
