# ADR 0006: 공식 Unity CLI의 Editor MCP 사용

Status: accepted — 2026-09-08 PM 사용자 결정. 설치·연결·테스트 완료 판정은 별도다.
Supersedes: ADR 0001·0002·0005와 기존 SPEC/파이프라인의 CoplayDev/unity-mcp 선택 부분.
Tracking: M0-03 #16, M1-03 #19, M1-04 #20, M2-01 #24, R-02 #42, FND-01 #55.

## 결정

Unity 저작 제어면은 **공식 Unity CLI에 포함된 Editor MCP 서버**로 통일한다. MCP 클라이언트는 `unity mcp --project-path <절대 프로젝트 경로>`를 stdio로 실행하고, Editor 연결에는 공식 `com.unity.pipeline` 패키지를 사용한다. CoplayDev/unity-mcp v10.2.0을 신규 설치하거나 그 UPM 경로·도구 이름을 공식 서버 설정에 재사용하지 않는다.

Codex와 Claude Code처럼 MCP를 직접 지원하는 클라이언트는 공식 서버에 바로 연결한다. `pi-mcp-adapter`는 Pi에서 실제 필요할 때만 별도로 검토할 선택 사항이며, 직접 연결의 선행조건이 아니다. 서버 선택 변경은 Pi를 포함한 전체 오케스트레이션 도구 교체를 뜻하지 않는다.

선택 이유는 사용자가 요청한 공식 배포 경로 통일과 별도 커뮤니티 서버 설치 제거다. 비교 성능 우위나 전체 도구 호환성이 검증됐다는 의미는 아니다. CLI의 MCP, Pipeline, 기존 AI Assistant 내 MCP 및 제3자 Unity MCP를 같은 제품·버전으로 취급하지 않는다.

## 확인한 근거와 버전

2026-09-08 학교 PC에서 다음을 직접 확인했다.

| 항목 | 확인 결과 | 남은 확인 |
|---|---|---|
| 공식 Unity CLI | `unity --version`: `1.0.0-beta.8`; `unity mcp --help`가 Editor MCP stdio 서버와 client configure를 제공 | 해당 바이너리의 무결성·배포 조건은 M0-03/M1-03 기록에 결속 |
| Unity 프로젝트 | `6000.3.23f1`, revision `09d2ecc7fb28`; Windows Editor/모듈 설치와 import/compile 확인 | 전체 Windows 빌드·HMD 수용은 별도 |
| Pipeline | 사용자 연결 검증 지시에 따라 `0.6.0-exp.1` 설치, manifest/lock 고정. Test Framework 1.6.0 유지, Mono.Cecil 1.11.6·Newtonsoft.Json 3.2.2 해석 | 전체 공급망·실행 경계 검토는 #19에서 추적 |
| 클라이언트 | Codex·Claude Code에 실제 등록. Claude Code health `Connected`; Codex와 동일 실행 파일/인자를 사용하는 stdio 검증 성공 | 기존 대화의 도구 목록에는 클라이언트 재연결 필요 |
| Editor 연결 | 명시한 프로젝트의 `editor_status=ready`, MCP 초기화·149개 도구 조회·씬/Console 읽기 성공. EditMode 52/52, PlayMode 6/6, 종료 후 오류 0 | 전체 테스트·제한 편집·우회 차단 DoD와 제품 수용을 대신하지 않음 |

CLI beta와 Pipeline experimental 상태를 기록한다. `latest` 관측은 자동 업그레이드 지시나 승인된 패키지 고정값이 아니다. `Packages/manifest.json`과 `packages-lock.json`을 함께 검토하고, 기존 Test Framework 등 의존성 변경도 확인한다. Coplay의 MIT 표기를 공식 CLI/Pipeline의 라이선스로 복사하지 않는다.

공식 근거: [Unity CLI 소개와 Pipeline 구조](https://unity.com/blog/meet-the-unity-cli), [CLI 사용 문서](https://docs.unity.com/en-us/unity-cli/use-unity-cli), [명령 참조](https://docs.unity.com/en-us/unity-cli/unity-cli-reference), [공식 Pipeline registry](https://packages.unity.com/com.unity.pipeline). 명령 문법은 설치된 `1.0.0-beta.8`의 도움말과 dry-run 결과로 대조했다.

[연결 검증 기록](../evidence/foundation/2026-09-08-official-editor-mcp.json)은 실제 작업 프로젝트와 분리 사본의 결과 및 실패 이력을 구분한다. 첫 PlayMode 검증 전 작업 manifest에서 Pipeline 항목이 제거됐고, 후속 domain reload가 `Microsoft.CodeAnalysis`를 불러오지 못해 6개가 실패했다. 변경 주체는 확정하지 않았다. 정확 버전 복구 후 분리 사본과 실제 프로젝트에서 각각 6/6 통과했고 실제 프로젝트의 EditMode 52/52를 다시 확인했다. 다른 세션에서 변경한 패키지 등록을 임의로 원복하지 않는다.

이번 패키지의 `LICENSE.md`에는 Unity Package Distribution License가 명시되어 있고 README의 라이선스 문구와 차이가 있다. 배포본 SHA-1을 대조하고 SHA-256을 기록했으며, 이 확인을 전체 라이선스·보안 검토 완료로 확대하지 않는다. 임의 C# 및 미승인 패키지 변경 도구는 Codex의 `disabled_tools`와 Claude Code의 MCP deny 항목에 기록했다. 중첩 호출·직접 셸·OS 경계까지 차단됐다는 증거는 아니다.

## 연결 절차

작업 대상 Unity 프로젝트 루트에서 PowerShell로 먼저 조회한다. 이미 설치 중인 Editor의 다운로드/설치를 중복 실행하지 않는다.

```powershell
$projectPath = (Resolve-Path .).Path
unity --version
unity mcp --help
unity pipeline install --help
unity mcp configure --list --format json
unity mcp configure codex --project-path $projectPath --dry-run
unity mcp configure claude-code --project-path $projectPath --dry-run
unity command --project-path $projectPath editor_status --format json
```

1. M1-03에서 공식 CLI/Pipeline의 배포·라이선스·의존성과 호출 경로를 확인한다. M2-01에서 `unity pipeline install --project-path <PROJECT> --package-version <EXACT_VERSION>`로 **검토한 정확 버전**을 설치하고 manifest/lock diff를 남긴다. 꺾쇠 값은 실제 경로·고정 버전으로 치환한다. 다른 에이전트가 프로젝트를 쓰고 있으면 작성자를 먼저 하나로 정한다.
2. 위 dry-run의 등록 이름·실행 파일·인자를 확인하고 선택한 클라이언트에 `unity mcp configure <CLIENT> --project-path <PROJECT>`를 적용한다. `codex`와 `claude-code`는 서로 다른 값이다. 이번 CLI의 Claude Code 등록은 user scope이므로 실제 절대 프로젝트 경로를 사용하고 다른 프로젝트 등록과 충돌하지 않는지 확인한다.
3. 개인 MCP 설정·설치 경로·토큰은 Git에 넣지 않는다. 클라이언트를 새로 시작한 뒤 정확한 프로젝트로 서버가 연결되는지 확인한다.
4. Editor의 패키지 import와 compilation이 끝난 뒤 `unity command --project-path <PROJECT> editor_status --format json`과 MCP의 `initialize`/`tools/list` 응답을 확인한다. 전역 `unity status`가 인스턴스를 누락하는 동안에도 명시한 프로젝트는 응답한 사례가 있으므로 실제 대상 응답으로 판단한다. 시작 중 도구 목록이 비어 있으면 ready 여부를 다시 읽는다. 실제 제공된 도구 목록으로 읽기·Console·제한 편집·테스트 호출을 매핑한다. Coplay 또는 별도 broker의 도구 이름을 추측해 호출하지 않는다.
5. 읽기 → 제한된 idempotent builder → compile/Console 오류 0 → 관련 EditMode/PlayMode 순서로 검증한다. CLI·Pipeline·Editor 버전, 기준 SHA, 테스트 결과와 정제 증거를 #20에 연결한다. stdio handshake 성공만으로 Editor 동작 또는 M1-04 Done을 선언하지 않는다.

## 실행 원칙과 작업 분담

Git의 C#·데이터·Editor builder가 계속 원본이다. Editor 하나당 쓰기 에이전트 하나, timeout 후 상태 재조회, Unity YAML 수동 편집 금지를 유지한다. 임의 C# 실행(`execute_code`뿐 아니라 공식 `eval`/`eval_file` 등 동등 경로), 원격 패키지 설치와 외부 자산 생성은 해당 명시 승인 범위에서만 실행한다. 클라이언트 allowlist와 실제 실행 경계를 검증하지 않은 상태를 '차단 구현 완료'로 표시하지 않는다.

| 작업 | 이번 변경 이후 범위 |
|---|---|
| M0-03 #16 | 공식 CLI·Pipeline을 각각 등재하고 정확 버전·배포·라이선스·의존성 기록. Coplay 선택은 superseded |
| M1-03 #19 | 공식 stdio/CLI/Pipeline 호출 경로와 도구 노출·결과 전송 검토. Pi adapter는 사용하는 경우에만 추가 |
| M1-04 #20 | 공식 Editor MCP 등록·연결·도구 스모크와 정제 증거. 완료 전 기존 실행 검증 조건 유지 |
| M2-01 #24 | Unity 6000.3.23f1 유지, 공식 Pipeline의 검토한 버전과 package lock 결속 |
| R-02 #42 / FND-01 #55 | 학교 PC 실제 자원에 맞춘 실행 프로파일과 팀원 연결 안내 |

담당자·최종 수용은 기존 보드 계약을 따른다. 문서 변경으로 관련 이슈를 자동 종료하거나 미실행 테스트를 체크하지 않는다. 과거 Coplay 조사·리뷰·실행 영수증은 당시 이력으로 보존하며 새 결정의 근거로 소급 수정하지 않는다.
