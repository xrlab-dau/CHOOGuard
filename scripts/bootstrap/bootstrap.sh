#!/usr/bin/env bash
# CHOOGuard 개발 머신 부트스트랩 (macOS / Linux / Git Bash)
# 목적: 저장소만으로 학교 PC(Unity 워크스테이션)와 로컬(계획 머신)을 같은 상태로 만든다.
# 원칙: 비밀값을 출력하거나 파일에 쓰지 않는다. Unity·공식 CLI Editor MCP는 안내만 하고 자동 설치하지 않는다.
#       설치·동기화(npm install, uv sync)는 사람이 AUTO_INSTALL=1 로 명시 승인했을 때만 실행한다.
#       MACHINE_LABEL 은 local | school-pc 만 허용한다. 호스트명은 기록하지 않는다.
set -euo pipefail

PI_VERSION="0.85.0"
NODE_MIN_MAJOR=22
MACHINE_LABEL="${MACHINE_LABEL:-}"   # local | school-pc (필수)
AUTO_INSTALL="${AUTO_INSTALL:-0}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

step() { printf '\n== %s\n' "$*"; }
ok()   { printf '   [ok] %s\n' "$*"; }
warn() { printf '   [warn] %s\n' "$*"; }
need() { command -v "$1" >/dev/null 2>&1; }

step "0. 저장소"
case "$MACHINE_LABEL" in
  local|school-pc) ;;
  *) printf '   [error] MACHINE_LABEL 은 local 또는 school-pc 여야 한다. 예: MACHINE_LABEL=school-pc bash scripts/bootstrap/bootstrap.sh\n' >&2; exit 2 ;;
esac
# Only receipt-mode arguments may reach both verifier calls. Standalone modes,
# help and argparse abbreviations could exit successfully without the gate.
verify_args=("$@")
for ((i=0; i<${#verify_args[@]}; i++)); do
  case "${verify_args[i]}" in
    --strict|--write=*|--policy-manifest=*|--policy-manifest-sha256=*) ;;
    --write|--policy-manifest|--policy-manifest-sha256)
      if (( i + 1 >= ${#verify_args[@]} )) || [[ "${verify_args[i+1]}" = -* ]]; then
        printf '   [error] 검증 인수 값이 필요하다\n' >&2; exit 2
      fi
      i=$((i + 1))
      ;;
    *) printf '   [error] 지원하지 않는 검증 인수. --strict, --write, --policy-manifest, --policy-manifest-sha256만 허용한다. 독립 모드는 검증기를 직접 실행한다\n' >&2; exit 2 ;;
  esac
done
if ! need python3; then
  warn "python3 없음. 검토한 Python 3.11+ 실행기를 설치한 뒤 다시 실행"
  exit 2
fi
git rev-parse --is-inside-work-tree >/dev/null
ok "branch=$(git branch --show-current) head=$(git rev-parse --short HEAD) label=$MACHINE_LABEL"

step "0.1 정책 사전 검사 (영수증 아님)"
# set -e propagates failure before any Node/Pi/uv/npm command or package read.
# --write is checked here but only the final verification may write a receipt.
python3 scripts/bootstrap/verify_toolchain.py --policy-preflight --label "$MACHINE_LABEL" "$@"

step "1. Node >= $NODE_MIN_MAJOR"
if need node; then
  major="$(node -p 'process.versions.node.split(".")[0]')"
  if [ "$major" -ge "$NODE_MIN_MAJOR" ]; then ok "node $(node --version)"; else warn "node $(node --version) 는 너무 낮다. https://nodejs.org 에서 LTS 설치"; fi
else
  warn "node 없음. https://nodejs.org 에서 LTS(22 이상) 설치 후 다시 실행"
fi

step "2. Pi coding agent $PI_VERSION (고정)"
if need pi && [ "$(pi --version 2>/dev/null | head -1)" = "$PI_VERSION" ]; then
  ok "pi $PI_VERSION"
elif need npm; then
  if [ "$AUTO_INSTALL" = "1" ]; then
    npm install -g "@earendil-works/pi-coding-agent@$PI_VERSION"; ok "pi 설치됨"
  else
    warn "pi $PI_VERSION 이 아니다. 설치: npm install -g @earendil-works/pi-coding-agent@$PI_VERSION  (AUTO_INSTALL=1 로 자동 실행 가능)"
  fi
else
  warn "npm 없음"
fi

step "3. uv (Python 도구·조사 하네스)"
if need uv; then ok "$(uv --version)"; else warn "uv 없음. 설치 안내: https://docs.astral.sh/uv/getting-started/installation/ (스크립트를 직접 파이프 실행하지 말고 설치 파일을 확인 후 실행)"; fi

step "4. 조사 하네스 (tools/research)"
if ! need uv; then
  warn "uv 없음 → 건너뜀"
elif [ "$AUTO_INSTALL" = "1" ]; then
  (cd tools/research && uv sync --frozen >/dev/null) && ok "uv sync --frozen"
else
  warn "동기화 미실행. 승인 후 직접 실행: (cd tools/research && uv sync --frozen)  (AUTO_INSTALL=1 로 자동 실행 가능)"
fi
[ -f tools/research/.env ] && ok ".env 존재 (내용은 출력하지 않음)" || warn "tools/research/.env 없음. .env.example 을 복사해 키를 채운다 (커밋 금지)"

step "5. Pi 프로젝트 신뢰와 패키지 자동 설치"
if [ -d .pi/npm/node_modules/pi-agents ]; then
  ok "pi-agents $(node -p "require('./.pi/npm/node_modules/pi-agents/package.json').version") 설치됨"
else
  warn "아직 설치 전. 이 디렉터리에서 'pi' 를 실행해 프로젝트 신뢰(trust)를 승인하면 .pi/settings.json 의 packages 가 자동 설치된다."
fi

step "6. 모델 제공자 자격 (이름만 확인)"
for v in ANTHROPIC_API_KEY OPENAI_API_KEY EXA_API_KEY; do
  if [ -n "${!v:-}" ]; then ok "$v 설정됨"; else warn "$v 미설정 (Pi 는 /login 으로 OAuth 도 가능)"; fi
done

step "7. Unity 워크스테이션 (school-pc 전용, 수동)"
if [ -f ProjectSettings/ProjectVersion.txt ]; then ok "Unity 프로젝트 존재: $(head -1 ProjectSettings/ProjectVersion.txt)"; else warn "Unity 프로젝트 없음 (M2-01 에서 생성). Unity Hub 와 에디터(D-09 에서 고정) 설치는 docs/choo-guard-school-pc-bootstrap-v1.md §4 참조"; fi
warn "공식 Unity CLI의 unity mcp와 com.unity.pipeline을 사용한다. ADR 0006 및 M1-03/M1-04의 고정 버전·연결 검증을 따른다. pi-mcp-adapter는 Pi에 필요한 경우만 검토한다"

step "8. 검증 영수증"
python3 scripts/bootstrap/verify_toolchain.py --label "$MACHINE_LABEL" "$@"
