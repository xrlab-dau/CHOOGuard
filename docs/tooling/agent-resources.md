# 공유 에이전트 도구와 검증 범위

2026-09-08의 소스 전달 기록이다. 개인 계정·모델 인증·원본 자료·게시 권한을 공유하는 설정이 아니다.

## 포함하는 소스

| 구성 | 설치 기록/고정값 | 공유 경로 |
|---|---|---|
| BMAD | 설치 manifest6.12.0 | `.agents/skills/bmad-*`, `_bmad/`의 공통 소스·설정·manifest |
| SpecKit | 설치 기록1.0.4 | `.specify/`, `.pi/prompts/speckit.*` |
| Pi 패키지 | `pi-subagents@0.65.1`, `pi-agents@0.21.0`, `pi-exa@0.6.1` | `.pi/settings.json`의 정확 버전 및 `.pi/agents`, `.pi/workflows` |
| 프로필 회귀 | 실제 설치된 pi-subagents0.65.1 parser | `scripts/dev/test_agent_profiles.mjs` |

BMAD/SpecKit은 기존 설치 소스를 기록하는 것이며 이 작업에서 설치기를 재실행하지 않았다. 라이선스는 각각 상류의 `v6.12.0`/`v1.0.4` 태그에서 확인한 원문을 `_bmad/LICENSE`, `.specify/LICENSE`에 보존했다. BMAD의 `CONTRIBUTORS.md`·`TRADEMARK.md`도 함께 포함한다. BMAD 라이선스/저작권은 `.agents/skills/bmad-*`에도 적용되며, SpecKit 라이선스는 해당 generated Pi prompts에도 적용된다. 별도 프로젝트 소스의 권리와 혼동하지 않는다.

공식 출처: [BMAD6.12.0](https://github.com/bmad-code-org/BMAD-METHOD/tree/v6.12.0), [SpecKit1.0.4](https://github.com/github/spec-kit/tree/v1.0.4).

## 개인/기기 상태 제외

`.pi/npm`, `.pi/git`, `.pi/subagents`, `.planning`, `.claude/worktrees`, `_bmad/config.user.toml`, 연구 `imports`, session HTML은 전달하지 않는다. 이미 진행 중인 다른 worktree와 로컬 원본은 삭제하지 않는다. 개인 전역 Pi 설정·계정·API키·쿠키·브라우저 기록은 복사하지 않는다.

Mac에서는 Pi0.85.1과 Node26.7.0에서 작업했다. Pi의 native async host 의존성(`pi-server`/`pi-client`/`pi-protocol`0.85.1)을 설치·점검한 과거 복구 기록이 있지만, `.pi/settings.json`만으로 다른 PC의 호스트 설치를 증명하지 않는다. 학교는 승인된 설치 경로에서 해당 런타임/패키지를 준비하고 실제 기능을 확인한다. 전역 Astra 컨텍스트900,000 설정과 Exa 인증 선택은 개인 설정이므로 원본 JSON을 Git에 옮기지 않았다.

## native 프로필 실패와 수정

첫 게시 전 검증 workflow `84c628be-f0cd-420a-94c0-984b9d3cb9f8`, child `fb6df634-1054-4175-975b-6c30b3e544e9`가 `Agent 'verifier' requested unavailable child tools: [read, ls]`로 실패했다. stdout 일부가 있더라도 이 실행을 검토 PASS로 쓰지 않는다.

상류의 실제 `parseFrontmatterList`는 CSV 또는 줄별 block-list를 지원하며 YAML flow-list 괄호를 제거하지 않는다. 따라서 `tools: [read, …, ls]`는 `[read`/`ls]`로 해석됐다. `skills: []`도 가짜 스킬 이름이었다.7개 프로젝트 프로필을 의도한 권한 그대로 CSV tools·빈 skills 표기로 수정했고 실제 parser 회귀시험이7실패에서7통과로 바뀌었다. 허용 도구나 reasoning ceiling을 넓히지 않았다.

```sh
node --test scripts/dev/test_agent_profiles.mjs
```

이 시험은 설치된 고정 parser와 Node의 `stripTypeScriptTypes`를 사용한다. 모든 role/model의 실제 시작·인증·작업 수용을 보장하는 시험은 아니다. native 경로로만 재시도하고 실패·부분 diff를 보존한다. 외부 CLI나 foreground agent를 자동 대체 수단으로 사용하지 않는다.

현재 프로젝트 reasoning 상한은 `high`다. 과거 Astra `max` 실행은 이 상한으로 시작 전 실패했으며 해당 실패도 이력으로 남긴다. 모델/단계 배정은 최신 사용자 방향과 실제 실행 조건에 맞춰 판단하되, 도구·권한 제한을 우회하지 않는다.

CodeScene MCP는 이 환경에서 제공되지 않아 점수를 산출하지 않았다. 프로젝트의 실제 Python/Unity 시험, 소스 검토와 정책 검사를 사용했으며 미실행 도구를 PASS로 표시하지 않는다.

## 공식 Unity Editor MCP (2026-09-08)

Unity 연결 기준은 [ADR 0006](../adr/0006-official-unity-editor-mcp.md)의 공식 Unity CLI `unity mcp` + `com.unity.pipeline`이다. Codex/Claude Code의 native MCP 설정은 기기별 private 설정으로 등록하며 Git에 복사하지 않는다. Pi adapter는 Pi에 필요한 경우만 별도 검토한다. 기존 native Pi 검토 워크플로와 Unity 클라이언트 선택은 서로 다른 범위다.
