# R-07 정책 기준선과 검증 증거

추적: [#52](https://github.com/xrlab-dau/CHOOGuard/issues/52), Windows 범위는 [#44](https://github.com/xrlab-dau/CHOOGuard/issues/44). 작성일: 2026-09-08. 이 문서는 구현·검증 인계이며 사람 승인이나 독립 검토 판정이 아니다.

## 단계별 정책 검사

`verify_toolchain.py`는 현재 파일에서 관측한 해시와 승인된 기준선을 구분한다. 별도 기준선과 신뢰된 SHA-256 핀이 없으면 `policy_approval_pending`, `required_ok=false`, 종료 코드 1이다. 이 경우 Node/Pi/uv와 설치된 Pi 패키지 검사를 실행하지 않는다. Git 메타데이터와 파일 정적 검사는 수행한다.

현재 `.pi/`와 `.specify/`는 Git에 추적되어 있다. 이들 정책 계열, 두 Unity 패키지 manifest, 재구성 의존성, 조사 실행기 등을 포함한 정확한 후보 목록을 검토한다. 추적 여부만으로 승인되었다고 판단하지 않는다. `.github/workflows/*.yaml`은 대체 확장자로 선택적이며 `.yml` 계열은 필수다.

```bash
python3 scripts/bootstrap/verify_toolchain.py --label local \
  --write-policy-candidate docs/evidence/R-07/policy-candidate-NEW-RUN.json
```

후보 생성은 Git HEAD만 조회하고 도구·설치를 실행하지 않는다. 생성 성공의 종료 코드 0은 **후보 파일 생성 성공**이다. 후보는 `status=candidate`, `approvalReference=null`이며, 자신의 해시를 핀으로 제공해도 승인으로 통과하지 않는다. 기존 증거 파일은 덮어쓸 수 없다.

정책 소유자가 정확한 파일 목록·바이트를 검토한 뒤 별도 승인 manifest와 그 SHA-256을 관리한다. 승인 manifest는 `schemaVersion`, `status`, `approvalReference`, `sourceCommit`, `policySha256`만 갖는다. `status=approved`와 유효한 승인 참조는 형식 조건이다. 이 도구는 서명자의 신원을 인증하지 않으므로 **신뢰된 호출자가 작업본과 독립적으로 확보한 승인 핀**을 제공해야 한다. 현재 작업본으로 새 manifest와 핀을 만들어 자기 승인하는 것은 신뢰 모델 밖이다.

```bash
python3 scripts/bootstrap/verify_toolchain.py --label local \
  --policy-manifest "$APPROVED_POLICY_MANIFEST" \
  --policy-manifest-sha256 "$APPROVED_POLICY_SHA256" \
  --write docs/evidence/R-07/verify-NEW-RUN.json
```

정확한 경로 집합이나 파일 SHA-256이 하나라도 달라지면 실패한다. 경로 누락·추가·변경을 따로 기록하며, 누락된 계열·빈 파일·읽기 실패·정책 링크도 거부한다. 기준선 일치 결과는 `baseline_match_not_runtime_approval`이다. M0-00 사람 분류, 자료 접근·전송, 설치, Unity 실행, 병합 승인이나 최종 DoD를 대신하지 않는다. 기존 bootstrap 진입점은 승인 manifest를 자동 선택하지 않으므로 새 게이트에서 대기할 수 있다. Pi 버전 정책은 이 변경에서 승인·상향하지 않는다.

## 순회 범위와 한계

- 저장소 루트의 Git 객체 디렉터리만 제외한다. 중첩 Git 디렉터리, venv와 node_modules도 이름·링크 검사 대상이다.
- 파일 200,000개, 방문 디렉터리 20,000개, 경과 15초 중 하나라도 넘으면 미완료로 실패한다. 경과시간 검사는 시스템 호출 사이에서 작동하므로 멈춘 OS 호출을 강제 중단하는 시간 제한은 아니다.
- 외부 심볼릭 링크를 차단한다. 탐지된 mount와 Windows reparse point는 순회하지 않고 미검증 오류로 처리한다. 모든 OS의 bind mount를 완전히 판별한다고 주장하지 않는다.
- 의존성 예외는 지정된 조사 venv의 `certifi/cacert.pem`과 `_virtualenv.pth`뿐이다. 임의의 `.pth` 또는 다른 디렉터리의 `cacert.pem`은 예외가 아니다. 이 이름 예외 자체는 내용의 진위를 인증하지 않는다.
- 읽기 실패나 예산 초과 시 발견 개수는 `null`이다. 증거에는 민감한 작업본 파일명·링크 대상·오류 경로를 기록하지 않는다.
- 정책 glob에 포함된 FIFO·소켓·장치 파일은 조용히 제외하지 않고 오류로 처리한다. 승인 manifest와 정책 파일은 일반 파일만 읽으며, POSIX에서는 비차단·no-follow 열기와 열린 파일의 종류 확인으로 FIFO 치환도 거부한다.
- 순회 전후 정책 해시를 비교하고, 실제 파싱하는 설정 바이트도 승인된 해시와 대조한다. 버전 검사 뒤 정책이 바뀌면 실행 사실은 보존하되 성공 영수증을 만들지 않는다. 이 검사는 파일시스템의 원자적 스냅샷이나 도구 실행 sandbox가 아니며, 검사 사이에 변경 후 복구되는 임의의 동시 공격을 완전히 탐지한다고 주장하지 않는다.

## 이관 항목 처리

| 항목과 원 지적 | 이번 처리 | 남은 수용 조건 |
|---|---|---|
| 1 · R-01/safety/R2-SAF-1 | 미승인 단계와 기준선 비교 단계 분리 | PM의 실제 정책 경로·핀 승인 |
| 2 · R-01/adversarial/R2-ADV-2, safety/SAF-3 | 정확 집합·해시·외부 핀 비교, 변조·추가·삭제 부정 시험 | 승인 manifest 운영과 독립 검토 |
| 3 · R-01/adversarial/R2-ADV-6 | 새 RED/GREEN 로그를 정제 후 소스·로그 해시와 결속 | 기존 실행 이력을 소급 복원하지 않음 |
| 4 · R-01/spec/SPC-3 | 원본 105키 저널 미제공을 명시 | 원본 제공 또는 검증 불가 범위의 명시적 사람 수용. 이 문서는 수용을 대신하지 않음 |
| 5 · R-01/safety/SAF-4 | 링크·접근 오류·탐지된 mount/reparse 경계 차단, native Windows CI 추가 | 해당 OS의 실행 결과 확인. 실제 학교 PC ACL·junction·mount 점검은 #44에 유지 |
| 6 · R-01/safety/R2-SAF-2 | 디렉터리·경과시간 예산과 초과 부정 시험 | 멈춘 OS 호출과 탐지 불가능한 mount는 한계로 유지 |
| 7 · R-01/safety/SAF-1, adversarial/ADV-4 | 기존 Foundation 스키마·검증기의 역할/행동 ID, Anchor, provisional 강제와 부정 시험 확인 | 전체 M4-01 DoD 및 실제 절차 수용과 구분 |
| 8 · R-01 §1 | 별도 문서 변경 단위 `R-07-GRAPH`로 처리 | 별도 대상 manifest·문서 검토 |
| 9 · R-01 §7.3 | 면제 루트·정확 파일 예외·순회 실패 차단을 이번 회귀 범위에 포함 | 작성자와 다른 제공자의 필수 세 관점 검토 |

M4-01의 실제 강제는 `scripts/foundation/validate.py`, `schemas/foundation-scenario.schema.json`, `scripts/foundation/tests/test_validate.py`를 따른다. 소스 검사 성공은 C# 컴파일, Unity 실행, 학교 PC 성능 또는 현장 정확성 통과가 아니다.

## 검토 인계

`run-20260908-01.json`은 첫 소스와 새 정제 로그를 결속한다. native Windows junction 회귀는 `run-20260908-02.json`, 최종 diff에서 발견한 Git 추적 목록 실패 시 도구 실행 차단은 `run-20260908-03.json`에 순서대로 결속했다. 최신 기준은 `run-20260908-03.json`과 `policy-candidate-20260908-03.json`이다. 최신 Linux 결과는 68개 중 58개 통과·10개 환경별 제외다. `policy-scope-20260908-03.json`은 최종 검증기에서 19개 계열 누락을 차단한 합성 시험이다.

첫 PR 커밋의 실제 Windows 결과는 `windows-ci-20260908-01.json`에 고정했다(66개 통과·POSIX 1개 제외, PowerShell 8개와 native junction 포함, profile 검사 9개 통과). 최종 Git 목록 게이트 보강 전의 커밋 범위이며, 이후 CI 결과는 PR의 실제 실행 기록을 확인한다. [일괄 검증 인계](../../context/pm-validation-2026-09-08.md)에 설치·추가 검사·잔여 항목을 정리했다.

이전 실행 파일은 보존한다. 로그를 새 파일로 생성하고 Git 커밋으로 고정한다. SHA-256은 변경 탐지 수단이며 신뢰된 타임스탬프나 외부 서명은 아니다. 이전 R-01 저널과 학교 PC 영수증은 덮어쓰거나 새 소스의 PASS로 갱신하지 않는다.

2026-09-08 사용자 요청으로 새 세션의 명세·적대·보안 리뷰어와 별도 그래프 리뷰어를 실행했다. [1차 원 판정](../R-07-REVIEW/round1-summary.json)은 P2 세 건으로 `changes_required`이며, 세션 분리와 시작·종료 대상 해시를 기록했다. FIFO 정책 누락, FIFO manifest 정지, 설정 바이트와 승인 해시의 불일치를 재현하고 회귀 검사 후 보강했다. 새 실행은 `run-20260908-04.json`에 결속한다. 기존 01~03 실행과 Windows 기록은 당시 대상의 역사적 증거다.

이번 팀은 작성자와 같은 OpenAI 제공자이며 파일시스템 격리는 별도로 강제되지 않았다. 다른 제공자의 spec/adversarial/safety 판정은 아직 없다. 이번 팀 리뷰를 필수 다른 제공자 승인으로 세거나 R-01의 소진된 3라운드를 재개하지 않는다. 해당 게이트의 `cannot_proceed`와 이슈 미완료를 유지한다.

수정 커밋 `00dcd1625b3d45374d0a4232a012244831445343`의 [2차 세 관점 재검토](../R-07-REVIEW/round2-summary.json)는 추가 지적 없이 종료됐다. [최종 팀 리뷰 기록](../../reviews/2026-09-08-agent-team-review.md)에 세션 수, 대상, 세 결함의 수정, 실제 CI와 남은 조건을 정리했다. 첫 판정과 run04의 재검토 대기 상태는 당시 기록으로 보존한다.
