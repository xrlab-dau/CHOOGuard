# CI/CD 구축 — 2026-09-28

브랜치 `ci/production-pipeline` (develop `b8098810` 기준). 요구: OSS·OpenSSF·벤치마크·JEV 판정 기반의 프로덕션 수준 CI/CD 구축과 적용. #245(검증기 실패)·#246(CI 부재) 해소.

## 판정 기록 (JEV authority 0, 최종 결정은 메인·결정론적 검증)

| 항목 | 판정 | 근거 파일 |
|---|---|---|
| Unity 활성화 스위치 | 비밀값 3개 + 저장소 변수 `UNITY_CI_ENABLED=true` (0.84) | jev-cicd-014, jev-cicd-016 |
| Unity 시험 호스트 | GitHub macOS arm64 러너 네이티브 설치 (0.99) | jev-cicd-016 |
| Unity 빌드 | 같은 macOS 러너, 프로젝트 자체 `PlayerBuild.BuildMac` (0.93) | jev-cicd-016 |
| 시트 동시성 | 저장소 전체 단일 그룹, 실행 중 취소 금지 (0.76) → `queue: max` | jev-cicd-016 |
| PR 범위 | 같은 저장소 브랜치 PR 중 Assets/Packages/ProjectSettings 변경만 (0.89) | jev-cicd-016 |
| Unity 필수 체크 | 첫 녹색 라이선스 실행 후 'Unity tests' 필수화 (0.83) | jev-cicd-016 |
| 시험/빌드 분리 | PR은 시험, develop push·야간·태그는 시험+빌드 (0.80) | jev-cicd-014 |
| Library 캐시 | 시험 잡 하나만 캐시, 빌드는 새로 임포트 (0.82) | jev-cicd-014 |
| 빌드 대상 | macOS만 (0.50) | jev-cicd-014 |
| 필수 체크 | 정책 게이트 + 도구 시험 (0.56) | jev-cicd-014 |
| 릴리스 | main 태그 → 초안 릴리스 + 빌드 출처 증명 (0.97) | jev-cicd-014 |
| 보안 | 전체 벤치마크 묶음 (0.98) | jev-cicd-014 |
| graphify | 신선도 경고만 (0.91) | jev-cicd-014 |
| PR 제목 규칙 | 없음 (0.91) | jev-cicd-014 |
| PR 라벨 | 복원 (bool 0.61) | jev-cicd-014 |
| progress.json 잔여 키 | 제거 (0.60) | jev-progress-key-015 |

## 전제 변경 (2026-09-28)

- 사용자: Unity 계정이 학생 플랜으로 변경. Unity 학생 플랜 안내는 메일로 받은 라이선스 키를 Hub에 추가하라고 한다 → 시리얼 방식. GameCI 문서상 Professional 경로(`UNITY_SERIAL`·`UNITY_EMAIL`·`UNITY_PASSWORD`).
- Unity FAQ: Pro/Enterprise 시트 하나는 동시에 두 대까지 활성화(학생 플랜 동일 가정, 미검증). 이 Mac이 한 자리 사용.
- 호스트 가정: `SqliteProvider`는 macOS 전용(다른 OS는 `PlatformNotSupportedException`), `CSOPS0201/0203` 사용, `CSBOOT0101Tests:447`은 `OSXEditor` 단정 → GameCI 리눅스 컨테이너에서는 구조적으로 실패.
- GameCI `unity-test-runner` v4.3.2는 리눅스/윈도 도커 전용, 컨테이너 종료 시 반납 → 취소·시간초과 시 반납 누락.
- GitHub 2026 변경: `queue: max`(동시성 대기열 100), `cache-mode`, 공개 저장소 `pull_request_target` 기본 차단(2026-11-02 강제) → 라벨러는 `pull_request`로.

## 확인된 사실

- 문서 도구 시험 6묶음 로컬 통과(218건). 워커는 단위 시험 없음; 물리 탐침 `run_probes.py`는 실패해도 종료코드 0 → CI가 `passed`를 단정.
- Assets 15,596파일 중 `.meta` 누락 0, 고아 1(`VideoFirstFloor/PropMaterials.meta`, 빈 폴더; `AgentScripts/VideoFloorOne.cs`의 `Folder()`가 재생성).
- 바이너리 `.asset`은 NavMesh·LightingData 3개뿐(Unity 기본). 씬·프리팹·머티리얼은 전부 YAML. `m_SerializationMode: 2`.
- 추적 중인데 무시 규칙에 걸리는 파일 1,800여 개(연구 자료·`Assets/.planning` 과거분) → 위생 규칙은 PR 변경분에만 적용(래칫).
- 추적 중인 `.pyc` 8개, `.gitignore`에 바이트코드 규칙 없음.
- graphify 보고서의 기준 커밋 `6408d1c8`은 스쿼시 병합으로 develop에서 도달 불가 → 신선도는 `graphify-out/graph.json`을 마지막으로 바꾼 커밋 기준.
- `PlayerBuild.Build`는 실패해도 종료코드 0 → CI는 `CG_PLAYER_BUILD result=Succeeded` 표식과 `.app` 존재를 단정.
- 보안 설정: 비밀 스캔·푸시 보호·비공개 취약점 신고·Dependabot 보안 업데이트 모두 꺼짐, CodeQL 기본 설정 미구성.
- Unity 6000.3.23f1 (09d2ecc7fb28) arm64 에디터 pkg 5.1 GB, 설치 9.4 GB.
