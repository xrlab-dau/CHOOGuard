# CHOOGuard 개발 머신 부트스트랩 (Windows PowerShell 5.1 / 7)
# 사용: powershell -ExecutionPolicy Bypass -File scripts\bootstrap\bootstrap.ps1 -MachineLabel school-pc
# 원칙: 비밀값을 출력하지 않는다. Unity·공식 CLI Editor MCP는 안내만 한다.
#       설치·동기화(npm install, uv sync)는 사람이 -AutoInstall 로 명시 승인했을 때만 실행한다.
#       -MachineLabel 은 local | school-pc 만 허용한다. 호스트명은 기록하지 않는다.
#       검증기의 종료 코드를 그대로 전파한다 (필수 항목 실패 = 1).
param(
  [ValidateSet("local", "school-pc")]
  [Parameter(Mandatory = $true)]
  [string]$MachineLabel,
  [switch]$AutoInstall,
  [string[]]$VerifyArgs = @()
)
$ErrorActionPreference = "Stop"
$PiVersion = "0.85.0"
$NodeMinMajor = 22
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $Root

function Step($m) { Write-Host "`n== $m" }
function Ok($m)   { Write-Host "   [ok] $m" }
function Warn($m) { Write-Host "   [warn] $m" }
function Has($c)  { return [bool](Get-Command $c -ErrorAction SilentlyContinue) }

Step "0. 저장소"
git rev-parse --is-inside-work-tree | Out-Null
Ok ("branch={0} head={1} label={2}" -f (git branch --show-current), (git rev-parse --short HEAD), $MachineLabel)

Step "1. Node >= $NodeMinMajor"
if (Has node) {
  $major = [int](node -p 'process.versions.node.split(".")[0]')
  if ($major -ge $NodeMinMajor) { Ok ("node {0}" -f (node --version)) } else { Warn "node 가 낮다. https://nodejs.org LTS 설치" }
} else { Warn "node 없음. https://nodejs.org 에서 LTS(22 이상) 설치" }

Step "2. Pi coding agent $PiVersion (고정)"
$piNow = if (Has pi) { (pi --version 2>$null | Select-Object -First 1) } else { "" }
if ($piNow -eq $PiVersion) { Ok "pi $PiVersion" }
elseif (Has npm) {
  if ($AutoInstall) {
    npm install -g "@earendil-works/pi-coding-agent@$PiVersion"
    if ($LASTEXITCODE -ne 0) { Write-Error "npm install 실패 (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
    Ok "pi 설치됨"
  }
  else { Warn "pi $PiVersion 이 아니다. 설치: npm install -g @earendil-works/pi-coding-agent@$PiVersion  (-AutoInstall 로 자동)" }
} else { Warn "npm 없음" }

Step "3. uv"
if (Has uv) { Ok (uv --version) } else { Warn "uv 없음. https://docs.astral.sh/uv/getting-started/installation/ 참고 (설치 스크립트를 확인 후 실행)" }

Step "4. 조사 하네스 (tools/research)"
if (-not (Has uv)) { Warn "uv 없음 → 건너뜀" }
elseif ($AutoInstall) {
  Push-Location tools\research
  uv sync --frozen | Out-Null
  $syncExit = $LASTEXITCODE
  Pop-Location
  if ($syncExit -ne 0) { Write-Error "uv sync 실패 (exit $syncExit)"; exit $syncExit }
  Ok "uv sync --frozen"
} else { Warn "동기화 미실행. 승인 후 직접 실행: cd tools\research; uv sync --frozen  (-AutoInstall 로 자동 실행 가능)" }
if (Test-Path tools\research\.env) { Ok ".env 존재 (내용 비출력)" } else { Warn "tools\research\.env 없음. .env.example 복사 후 키 입력 (커밋 금지)" }

Step "5. Pi 프로젝트 신뢰와 패키지 자동 설치"
if (Test-Path .pi\npm\node_modules\pi-agents\package.json) {
  Ok ("pi-agents {0} 설치됨" -f (node -p "require('./.pi/npm/node_modules/pi-agents/package.json').version"))
} else { Warn "이 디렉터리에서 'pi' 실행 → 프로젝트 신뢰 승인 → .pi/settings.json 의 packages 자동 설치" }

Step "6. 모델 제공자 자격 (이름만 확인)"
foreach ($v in @("ANTHROPIC_API_KEY","OPENAI_API_KEY","EXA_API_KEY")) {
  if ([Environment]::GetEnvironmentVariable($v)) { Ok "$v 설정됨" } else { Warn "$v 미설정 (Pi 는 /login OAuth 가능)" }
}

Step "7. Unity 워크스테이션 (school-pc 전용, 수동)"
if (Test-Path ProjectSettings\ProjectVersion.txt) { Ok ("Unity 프로젝트 존재: {0}" -f (Get-Content ProjectSettings\ProjectVersion.txt -First 1)) }
else { Warn "Unity 프로젝트 없음 (M2-01 에서 생성). Unity Hub·에디터 설치는 docs/choo-guard-school-pc-bootstrap-v1.md §4" }
Warn "공식 Unity CLI의 unity mcp와 com.unity.pipeline을 사용한다. ADR 0006 및 M1-03/M1-04의 고정 버전·연결 검증을 따른다. pi-mcp-adapter는 Pi에 필요한 경우만 검토한다"

Step "8. 검증 영수증"
$py = if (Has python) { "python" } elseif (Has py) { "py" } else { $null }
if (-not $py) { Warn "python 없음. https://www.python.org 3.11+ 설치 또는 'uv python install 3.12'"; exit 2 }
& $py scripts\bootstrap\verify_toolchain.py --label $MachineLabel @VerifyArgs
exit $LASTEXITCODE
