<#
.SYNOPSIS
    Runs the Unity verification set requested on PR #110 in an isolated checkout and
    emits a redacted, re-readable receipt.

.DESCRIPTION
    The reviewer asked for evidence produced on a specific HEAD, not for earlier results
    copied forward. This script clones the target SHA into its own workspace, runs each
    check as a separate Unity batch process, and records the exact command, exit code and
    artifact hashes for every step.

    A step that cannot run on this machine is recorded as not_run with a reason.
    It is never reported as a pass.

    Personal paths, account and host names, licence lines, e-mail addresses and IP
    addresses are removed from every artifact before it is written to the receipt
    directory. Raw Unity logs stay in the workspace and are not part of the receipt.

.EXAMPLE
    powershell -File scripts/dev/verify_unity_receipt.ps1 -Sha eca36fb2d4ba0cf19df42226757de4443ba328bc
#>

[CmdletBinding()]
param(
    [string] $Sha = 'eca36fb2d4ba0cf19df42226757de4443ba328bc',
    [string] $RepoUrl = 'https://github.com/xrlab-dau/CHOOGuard.git',
    [string] $Workspace = (Join-Path $env:TEMP 'chooguard-verify'),
    [string] $MachineLabel = 'laptop-A',
    [string] $UnityPath = $env:UNITY_EDITOR_PATH,
    [ValidateSet('compile', 'editmode', 'playmode', 'ownership', 'build-negative', 'build-positive')]
    [string[]] $Steps = @('compile', 'editmode', 'playmode', 'ownership', 'build-negative', 'build-positive'),
    [int] $TimeoutMinutes = 90,
    [int] $MinFreeGiB = 20,
    [switch] $NoGraphics,
    [switch] $ReuseCheckout,
    [switch] $DryRun,
    # Dot-source the helpers without running a verification, so the redaction and the
    # result parsing can be tested on their own.
    [switch] $AsModule
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$BuildMethod = 'ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch'
$OwnershipTest = 'WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting'
$DesktopOutputRoot = 'Builds/FoundationDesktop'
$OwnershipMarker = 'generated-owner.json'
$SentinelName = 'team-file.txt'
$SentinelBody = 'keep'

# ---------------------------------------------------------------------------
# Redaction
# ---------------------------------------------------------------------------

$script:Redactions = New-Object System.Collections.ArrayList
$script:RedactionHits = @{}

function Add-Redaction {
    param([Parameter(Mandatory)][string] $Pattern, [Parameter(Mandatory)][string] $Replacement, [switch] $Literal)
    if ([string]::IsNullOrWhiteSpace($Pattern)) { return }
    $expression = $Pattern
    if ($Literal) {
        # Match the literal string with either slash direction, so Unity's mixed
        # separators cannot leak a path that the backslash form already covered.
        # One pass only: a second -replace would rewrite the character classes
        # this one just produced.
        $expression = [regex]::Escape($Pattern) -replace '\\\\|/', '[\\/]'
    }
    [void] $script:Redactions.Add([pscustomobject]@{ Pattern = $expression; Replacement = $Replacement })
}

function Protect-Text {
    param([string] $Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    foreach ($rule in $script:Redactions) {
        $found = [regex]::Matches($Text, $rule.Pattern, 'IgnoreCase')
        if ($found.Count -gt 0) {
            if (-not $script:RedactionHits.ContainsKey($rule.Replacement)) { $script:RedactionHits[$rule.Replacement] = 0 }
            $script:RedactionHits[$rule.Replacement] += $found.Count
            $Text = [regex]::Replace($Text, $rule.Pattern, $rule.Replacement, 'IgnoreCase')
        }
    }
    return $Text
}

function Initialize-Redactions {
    param([string] $CheckoutPath, [string] $UnityExe)
    # Most specific first: a later generic rule must not pre-empt a named label.
    Add-Redaction -Pattern $CheckoutPath -Replacement '<CHECKOUT>' -Literal
    Add-Redaction -Pattern $Workspace -Replacement '<WORKSPACE>' -Literal
    if ($UnityExe) { Add-Redaction -Pattern (Split-Path -Parent $UnityExe) -Replacement '<UNITY_INSTALL>' -Literal }
    if ($env:USERPROFILE) { Add-Redaction -Pattern $env:USERPROFILE -Replacement '<HOME>' -Literal }
    if ($env:APPDATA) { Add-Redaction -Pattern $env:APPDATA -Replacement '<APPDATA>' -Literal }
    if ($env:LOCALAPPDATA) { Add-Redaction -Pattern $env:LOCALAPPDATA -Replacement '<LOCALAPPDATA>' -Literal }
    Add-Redaction -Pattern '[A-Za-z]:[\\/]Users[\\/][^\\/\r\n"''<>|:*?]+' -Replacement '<HOME>'
    if ($env:USERNAME) { Add-Redaction -Pattern ('(?<![\w-])' + [regex]::Escape($env:USERNAME) + '(?![\w-])') -Replacement '<USER>' }
    if ($env:COMPUTERNAME) { Add-Redaction -Pattern ('(?<![\w-])' + [regex]::Escape($env:COMPUTERNAME) + '(?![\w-])') -Replacement '<MACHINE>' }
    Add-Redaction -Pattern '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}' -Replacement '<EMAIL>'
    Add-Redaction -Pattern '\b(?:\d{1,3}\.){3}\d{1,3}\b' -Replacement '<IP>'
    # Whole-line drops: licence and credential material must not reach the receipt at all.
    # The word boundaries matter. Without them 'serial' swallows every log line that
    # mentions a Serialized type, which hides the test names and exclusion reasons the
    # receipt exists to show.
    Add-Redaction -Pattern '(?m)^.*(?:\bserials?\b|\blicen[cs]\w*\b|\bactivation\b|\bentitlement\b|\baccess[_ -]?token\b|\bpasswords?\b|\brefresh[_ -]?token\b|\.ulf\b).*$' -Replacement '[redacted: licence/credential line]'
}

# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------

function Get-Sha256 {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-TextFile {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    return [System.IO.File]::ReadAllText($Path)
}

function Write-Utf8 {
    # Receipt artifacts are hashed byte for byte and then committed, so they are written
    # with LF and no BOM. Otherwise a CRLF artifact is normalised on commit and the
    # recorded SHA256 no longer verifies against the file in the repository.
    param([string] $Path, [string] $Content)
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    $normalised = $Content -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($Path, $normalised, (New-Object System.Text.UTF8Encoding($false)))
}

function Save-Artifact {
    # Redacts a workspace file into the receipt directory and returns its hash record.
    param([string] $SourcePath, [string] $Name)
    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return [pscustomobject]@{ name = $Name; present = $false; sha256 = $null; bytes = 0; source = 'not produced' }
    }
    $clean = Protect-Text (Read-TextFile $SourcePath)
    $target = Join-Path $script:ReceiptDir $Name
    Write-Utf8 -Path $target -Content $clean
    return [pscustomobject]@{
        name    = $Name
        present = $true
        sha256  = (Get-Sha256 $target)
        bytes   = (Get-Item -LiteralPath $target).Length
        source  = 'redacted copy of the raw Unity artifact'
    }
}

function New-Step {
    param([string] $Id, [string] $Title)
    return [pscustomobject]@{
        id              = $Id
        title           = $Title
        status          = 'not_run'
        reason          = $null
        command         = $null
        exitCode        = $null
        startedAt       = $null
        durationSeconds = $null
        results         = $null
        artifacts       = @()
        notes           = New-Object System.Collections.ArrayList
    }
}

function Add-Note {
    param([pscustomobject] $Step, [string] $Text)
    [void] $Step.notes.Add($Text)
}

function Invoke-Unity {
    # Starts one Unity batch process and returns its exit code, or $null if it did not run.
    param([pscustomobject] $Step, [string[]] $UnityArguments)
    $display = @($script:UnityExe) + $UnityArguments
    $Step.command = Protect-Text ($display -join ' ')
    $Step.startedAt = ([DateTimeOffset]::Now).ToString('o')
    if ($DryRun) {
        $Step.reason = 'dry run; no Unity process was started'
        Write-Host "  [dry-run] $($Step.command)"
        return $null
    }
    Write-Host "  -> $($Step.command)"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $script:UnityExe -ArgumentList $UnityArguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        try { $process.Kill() } catch { }
        $watch.Stop()
        $Step.durationSeconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)
        $Step.status = 'fail'
        $Step.reason = "timed out after $TimeoutMinutes minutes and was killed"
        return $null
    }
    $watch.Stop()
    $Step.durationSeconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)
    $Step.exitCode = $process.ExitCode
    Write-Host "     exit=$($process.ExitCode) in $($Step.durationSeconds)s"
    return $process.ExitCode
}

function Read-NUnitSummary {
    # Reads NUnit3 run totals plus every excluded case with the reason the run stated.
    param([string] $XmlPath)
    if (-not (Test-Path -LiteralPath $XmlPath)) { return $null }
    try { $doc = [xml] (Read-TextFile $XmlPath) } catch { return $null }
    $run = $doc.SelectSingleNode('/test-run')
    if (-not $run) { return $null }
    $excluded = @()
    foreach ($node in $doc.SelectNodes("//test-case[@result='Skipped' or @result='Inconclusive']")) {
        $reason = $node.SelectSingleNode('reason/message')
        $text = ''
        if ($reason) { $text = $reason.InnerText }
        $excluded += [pscustomobject]@{
            name     = (Protect-Text $node.GetAttribute('name'))
            fullName = (Protect-Text $node.GetAttribute('fullname'))
            reason   = (Protect-Text $text)
        }
    }
    return [pscustomobject]@{
        result          = $run.GetAttribute('result')
        total           = [int] $run.GetAttribute('total')
        passed          = [int] $run.GetAttribute('passed')
        failed          = [int] $run.GetAttribute('failed')
        skipped         = [int] $run.GetAttribute('skipped')
        inconclusive    = [int] $run.GetAttribute('inconclusive')
        durationSeconds = $run.GetAttribute('duration')
        excluded        = $excluded
    }
}

function Get-CompileErrorCount {
    param([string] $LogPath)
    $text = Read-TextFile $LogPath
    if ([string]::IsNullOrEmpty($text)) { return $null }
    return ([regex]::Matches($text, 'error CS\d{3,5}')).Count
}

function Test-LogContains {
    param([string] $LogPath, [string] $Needle)
    $text = Read-TextFile $LogPath
    if (-not $text) { return $false }
    return ($text.IndexOf($Needle, [StringComparison]::OrdinalIgnoreCase) -ge 0)
}

# ---------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------

function Resolve-UnityEditor {
    param([string] $RequiredVersion, [string] $Explicit)
    $candidates = New-Object System.Collections.ArrayList
    if ($Explicit) { [void] $candidates.Add($Explicit) }
    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, 'C:\Program Files')) {
        if ($root) { [void] $candidates.Add((Join-Path $root "Unity\Hub\Editor\$RequiredVersion\Editor\Unity.exe")) }
    }
    $secondary = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $secondary) {
        $custom = (Read-TextFile $secondary).Trim().Trim('"')
        if ($custom) { [void] $candidates.Add((Join-Path $custom "$RequiredVersion\Editor\Unity.exe")) }
    }
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    throw "Unity $RequiredVersion was not found. Pass -UnityPath or set UNITY_EDITOR_PATH."
}

function Get-WindowsModuleState {
    # Filesystem probe only. It never mutates the Editor installation or the project.
    param([string] $UnityExe)
    $editorDir = Split-Path -Parent $UnityExe
    $module = Join-Path $editorDir 'Data\PlaybackEngines\windowsstandalonesupport'
    if (Test-Path -LiteralPath $module) { return 'installed' }
    return 'absent'
}

function Get-LicenceExpiry {
    # Returns a date only. The licence file itself never reaches the receipt.
    $path = Join-Path $env:ProgramData 'Unity\Unity_lic.ulf'
    if (-not (Test-Path -LiteralPath $path)) { return [pscustomobject]@{ found = $false; expires = $null } }
    $text = Read-TextFile $path
    $values = [regex]::Matches($text, '<(?:Expires|StopDate)\s+Value="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $parsed = @()
    foreach ($value in $values) {
        $out = [datetime]::MinValue
        if ([datetime]::TryParse($value, [ref] $out)) { $parsed += $out }
    }
    if ($parsed.Count -eq 0) { return [pscustomobject]@{ found = $true; expires = $null } }
    return [pscustomobject]@{ found = $true; expires = (($parsed | Sort-Object -Descending)[0]).ToString('yyyy-MM-dd') }
}

function Get-ToolVersion {
    param([string] $Command, [string[]] $Arguments)
    try { return ((& $Command @Arguments 2>&1) | Select-Object -First 1).ToString().Trim() }
    catch { return 'not detected' }
}

function Invoke-Git {
    # git reports progress on stderr. Under $ErrorActionPreference = 'Stop' Windows
    # PowerShell turns that into a terminating NativeCommandError even when git exits 0,
    # so the preference is relaxed for the call and the exit code is checked instead.
    param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & git @Arguments 2>&1 }
    finally { $ErrorActionPreference = $previous }
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Lines    = @($output | ForEach-Object { $_.ToString() })
    }
}

function Invoke-GitChecked {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
    $result = Invoke-Git @Arguments
    if ($result.ExitCode -ne 0) {
        throw ("git " + ($Arguments -join ' ') + " failed with $($result.ExitCode):`n" + ($result.Lines -join "`n"))
    }
    return $result
}

if ($AsModule) { return }

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

$startedAt = [DateTimeOffset]::Now
$shortSha = $Sha.Substring(0, [Math]::Min(7, $Sha.Length))
$stamp = $startedAt.ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$checkout = Join-Path $Workspace "checkout-$shortSha"
$script:ReceiptDir = Join-Path $Workspace "receipts\$stamp-$shortSha"
$logDir = Join-Path $Workspace "logs\$stamp-$shortSha"

# The isolated checkout must not sit inside a working project, or the run would touch
# assets and build outputs that are in use. This is the reviewer's isolation condition.
$here = (Get-Location).Path
if ($checkout.StartsWith($here, [StringComparison]::OrdinalIgnoreCase) -or $here.StartsWith($Workspace, [StringComparison]::OrdinalIgnoreCase)) {
    throw "The workspace ($Workspace) overlaps the current project ($here). Choose a workspace outside it."
}

New-Item -ItemType Directory -Force -Path $Workspace, $logDir, $script:ReceiptDir | Out-Null

$drive = (Get-Item -LiteralPath $Workspace).PSDrive
$freeGiB = [math]::Round($drive.Free / 1GB, 1)
if ($freeGiB -lt $MinFreeGiB) {
    throw "Only $freeGiB GiB free on $($drive.Name):. A clone plus a Library plus a player build needs about $MinFreeGiB GiB."
}

Write-Host 'CHOOGuard verification receipt'
Write-Host "  target SHA : $Sha"
Write-Host "  workspace  : $Workspace"
Write-Host "  free space : $freeGiB GiB"

# --- isolated checkout -----------------------------------------------------
if ((Test-Path -LiteralPath $checkout) -and -not $ReuseCheckout) {
    Write-Host '  removing the previous checkout'
    Remove-Item -LiteralPath $checkout -Recurse -Force
}
if (-not (Test-Path -LiteralPath $checkout)) {
    Write-Host "  cloning $RepoUrl"
    if (-not $DryRun) {
        Invoke-GitChecked clone --no-checkout $RepoUrl $checkout | Out-Null
        Invoke-GitChecked -C $checkout checkout --detach $Sha | Out-Null
        Invoke-GitChecked -C $checkout lfs pull | Out-Null
    }
}

$headSha = $Sha
if (-not $DryRun) {
    $revParse = Invoke-GitChecked -C $checkout rev-parse HEAD
    $headSha = @($revParse.Lines | Where-Object { $_ -match '^[0-9a-f]{40}$' })[0]
    if ($headSha -ne $Sha) { throw "The checkout is at $headSha, not the requested $Sha." }
    $dirty = @((Invoke-GitChecked -C $checkout status --porcelain).Lines | Where-Object { $_ })
    if ($dirty.Count -gt 0) { throw 'The fresh checkout is already dirty; refusing to start from an unknown state.' }
}

$projectVersionPath = Join-Path $checkout 'ProjectSettings\ProjectVersion.txt'
$requiredVersion = '6000.3.23f1'
$projectRevision = $null
if (Test-Path -LiteralPath $projectVersionPath) {
    $versionText = Read-TextFile $projectVersionPath
    $versionMatch = [regex]::Match($versionText, 'm_EditorVersion:\s*(\S+)')
    if ($versionMatch.Success) { $requiredVersion = $versionMatch.Groups[1].Value }
    $revisionMatch = [regex]::Match($versionText, 'm_EditorVersionWithRevision:\s*\S+\s*\(([^)]+)\)')
    if ($revisionMatch.Success) { $projectRevision = $revisionMatch.Groups[1].Value }
}

$script:UnityExe = Resolve-UnityEditor -RequiredVersion $requiredVersion -Explicit $UnityPath
$moduleState = Get-WindowsModuleState -UnityExe $script:UnityExe
$licence = Get-LicenceExpiry
Initialize-Redactions -CheckoutPath $checkout -UnityExe $script:UnityExe

Write-Host "  editor     : $requiredVersion (windows standalone module: $moduleState)"
if ($licence.found -and $licence.expires) {
    Write-Host "  licence    : expires $($licence.expires)"
    if ([datetime]::Parse($licence.expires) -lt (Get-Date).AddDays(7)) {
        Write-Warning 'The Unity licence expires within a week. Renew it before relying on this run.'
    }
} elseif (-not $licence.found) {
    Write-Warning 'No machine licence file was found. Unity may refuse to start in batch mode.'
}

$commonArgs = @('-batchmode', '-projectPath', $checkout, '-accept-apiupdate')
if ($NoGraphics) { $commonArgs += '-nographics' }

$results = New-Object System.Collections.ArrayList

# --- step: import and compile ---------------------------------------------
if ($Steps -contains 'compile') {
    Write-Host '[compile] import and C# compile'
    $step = New-Step -Id 'compile' -Title 'Import and C# compile'
    $log = Join-Path $logDir 'compile.log'
    $code = Invoke-Unity -Step $step -UnityArguments ($commonArgs + @('-quit', '-logFile', $log))
    if ($null -ne $code) {
        $errorCount = Get-CompileErrorCount $log
        $step.results = [pscustomobject]@{ compilerErrors = $errorCount }
        if ($code -eq 0 -and $errorCount -eq 0) { $step.status = 'pass' }
        else { $step.status = 'fail'; $step.reason = "exit $code with $errorCount compiler errors" }
        Add-Note $step "The console judgement is the count of 'error CS####' matches in the Editor log."
    }
    $step.artifacts = @(Save-Artifact -SourcePath $log -Name 'compile.log')
    [void] $results.Add($step)
}

# --- steps: EditMode and PlayMode -----------------------------------------
foreach ($mode in @('EditMode', 'PlayMode')) {
    $stepId = $mode.ToLowerInvariant()
    if ($Steps -notcontains $stepId) { continue }
    Write-Host "[$stepId] full run"
    $step = New-Step -Id $stepId -Title "$mode test run"
    $log = Join-Path $logDir "$stepId.log"
    $xml = Join-Path $logDir "$stepId-results.xml"
    $code = Invoke-Unity -Step $step -UnityArguments ($commonArgs + @('-runTests', '-testPlatform', $mode, '-testResults', $xml, '-logFile', $log))
    if ($null -ne $code) {
        $summary = Read-NUnitSummary $xml
        $step.results = $summary
        if (-not $summary) { $step.status = 'fail'; $step.reason = "no result XML was produced (exit $code)" }
        elseif ($summary.failed -eq 0) { $step.status = 'pass' }
        else { $step.status = 'fail'; $step.reason = "$($summary.failed) failing tests" }
        Add-Note $step 'The verdict comes from the result XML, not from the exit code alone.'
    }
    $step.artifacts = @(
        (Save-Artifact -SourcePath $log -Name "$stepId.log"),
        (Save-Artifact -SourcePath $xml -Name "$stepId-results.xml")
    )
    [void] $results.Add($step)
}

# --- step: ownership adversarial test, split by module availability --------
if ($Steps -contains 'ownership') {
    Write-Host "[ownership] $OwnershipTest"
    $outputPath = Join-Path $checkout $DesktopOutputRoot
    if (Test-Path -LiteralPath $outputPath) {
        # The test ignores itself when a build output already exists, so a leftover
        # directory would turn this into a skip that reads like a pass.
        Remove-Item -LiteralPath $outputPath -Recurse -Force
    }
    $present = New-Step -Id 'ownership-module-installed' -Title "$OwnershipTest (Windows module installed)"
    $absent = New-Step -Id 'ownership-module-absent' -Title "$OwnershipTest (Windows module absent)"

    $ran = $present
    $other = $absent
    if ($moduleState -ne 'installed') { $ran = $absent; $other = $present }
    $other.reason = "this machine reports the Windows standalone module as '$moduleState'; the opposite condition needs a second machine and is left unrun"

    $log = Join-Path $logDir 'ownership.log'
    $xml = Join-Path $logDir 'ownership-results.xml'
    $code = Invoke-Unity -Step $ran -UnityArguments ($commonArgs + @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $OwnershipTest, '-testResults', $xml, '-logFile', $log))
    if ($null -ne $code) {
        $summary = Read-NUnitSummary $xml
        $ran.results = $summary
        if (-not $summary -or $summary.total -eq 0) { $ran.status = 'fail'; $ran.reason = 'the filter matched no test case' }
        elseif ($summary.skipped -gt 0 -or $summary.inconclusive -gt 0) { $ran.status = 'not_run'; $ran.reason = 'the test ignored itself; see the recorded reason' }
        elseif ($summary.failed -eq 0 -and $summary.passed -gt 0) { $ran.status = 'pass' }
        else { $ran.status = 'fail'; $ran.reason = "$($summary.failed) failing" }
    }
    $ran.artifacts = @(
        (Save-Artifact -SourcePath $log -Name 'ownership.log'),
        (Save-Artifact -SourcePath $xml -Name 'ownership-results.xml')
    )
    Add-Note $ran "Windows standalone module state on this machine: $moduleState (probed on the Editor installation; nothing was modified)."
    [void] $results.Add($present)
    [void] $results.Add($absent)
}

# --- step: negative build, a foreign output must be refused ----------------
if ($Steps -contains 'build-negative') {
    Write-Host '[build-negative] a foreign output must be refused'
    $step = New-Step -Id 'build-negative' -Title "$BuildMethod against a foreign output directory"
    $outputPath = Join-Path $checkout $DesktopOutputRoot
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    $sentinel = Join-Path $outputPath $SentinelName
    Write-Utf8 -Path $sentinel -Content $SentinelBody
    $before = Get-Sha256 $sentinel

    $log = Join-Path $logDir 'build-negative.log'
    $code = Invoke-Unity -Step $step -UnityArguments ($commonArgs + @('-quit', '-executeMethod', $BuildMethod, '-logFile', $log))
    if ($null -ne $code) {
        $after = Get-Sha256 $sentinel
        $marker = Join-Path $outputPath $OwnershipMarker
        $player = Join-Path $outputPath 'ChooGuardFoundation.exe'
        $step.results = [pscustomobject]@{
            sentinelSha256Before   = $before
            sentinelSha256After    = $after
            sentinelPreserved      = ($before -eq $after)
            ownershipMarkerCreated = (Test-Path -LiteralPath $marker)
            playerCreated          = (Test-Path -LiteralPath $player)
            refusalMessageInLog    = (Test-LogContains -LogPath $log -Needle 'not owned')
        }
        $ok = ($code -ne 0) -and $step.results.sentinelPreserved -and (-not $step.results.ownershipMarkerCreated) -and (-not $step.results.playerCreated)
        if ($ok) { $step.status = 'pass' }
        else { $step.status = 'fail'; $step.reason = "expected a non-zero exit with the foreign file untouched; got exit $code" }
        Add-Note $step 'A pass requires all four: a non-zero exit, the sentinel hash unchanged, no ownership marker, and no player written.'
    }
    $step.artifacts = @(Save-Artifact -SourcePath $log -Name 'build-negative.log')
    Remove-Item -LiteralPath $outputPath -Recurse -Force -ErrorAction SilentlyContinue
    [void] $results.Add($step)
}

# --- step: positive build --------------------------------------------------
if ($Steps -contains 'build-positive') {
    Write-Host '[build-positive] an owned output must build'
    $step = New-Step -Id 'build-positive' -Title "$BuildMethod against a clean output directory"
    $outputPath = Join-Path $checkout $DesktopOutputRoot
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Recurse -Force }

    if ($moduleState -ne 'installed') {
        $step.reason = "the Windows standalone module is '$moduleState' on this machine; a player build is left unrun rather than reported"
    } else {
        $log = Join-Path $logDir 'build-positive.log'
        $code = Invoke-Unity -Step $step -UnityArguments ($commonArgs + @('-quit', '-executeMethod', $BuildMethod, '-logFile', $log))
        if ($null -ne $code) {
            $player = Join-Path $outputPath 'ChooGuardFoundation.exe'
            $playerBytes = 0
            if (Test-Path -LiteralPath $player) { $playerBytes = (Get-Item -LiteralPath $player).Length }
            $step.results = [pscustomobject]@{
                playerSha256    = (Get-Sha256 $player)
                playerBytes     = $playerBytes
                dataFolder      = (Test-Path -LiteralPath (Join-Path $outputPath 'ChooGuardFoundation_Data'))
                unityPlayerDll  = (Test-Path -LiteralPath (Join-Path $outputPath 'UnityPlayer.dll'))
                ownershipMarker = (Test-Path -LiteralPath (Join-Path $outputPath $OwnershipMarker))
            }
            $ok = ($code -eq 0) -and ($playerBytes -gt 0) -and $step.results.dataFolder -and $step.results.unityPlayerDll -and $step.results.ownershipMarker
            if ($ok) { $step.status = 'pass' }
            else { $step.status = 'fail'; $step.reason = "expected exit 0 with a complete player; got exit $code" }
            Add-Note $step 'A successful build is not a playthrough. The player still needs a standalone run.'
        }
        $step.artifacts = @(Save-Artifact -SourcePath $log -Name 'build-positive.log')
    }
    [void] $results.Add($step)
}

# --- working tree effects --------------------------------------------------
$treeStep = New-Step -Id 'working-tree' -Title 'Regenerated assets in the isolated checkout'
if (-not $DryRun) {
    $changed = @((Invoke-GitChecked -C $checkout status --porcelain).Lines | Where-Object { $_ })
    $diffPath = Join-Path $logDir 'regenerated.diff'
    Write-Utf8 -Path $diffPath -Content (((Invoke-GitChecked -C $checkout diff --no-color).Lines -join "`n") + "`n")
    $treeStep.status = 'pass'
    $treeStep.results = [pscustomobject]@{
        changedEntries = $changed.Count
        entries        = @($changed | ForEach-Object { Protect-Text $_ })
    }
    $treeStep.artifacts = @(Save-Artifact -SourcePath $diffPath -Name 'regenerated.diff')
    Add-Note $treeStep 'The checkout is disposable, so nothing was restored. The primary working project was never opened.'
}
[void] $results.Add($treeStep)

# ---------------------------------------------------------------------------
# Receipt
# ---------------------------------------------------------------------------

$finishedAt = [DateTimeOffset]::Now
$receipt = [ordered]@{
    schema       = 'chooguard.verification-receipt/1'
    generatedAt  = $finishedAt.ToString('o')
    utcOffset    = $finishedAt.ToString('zzz')
    startedAt    = $startedAt.ToString('o')
    machineLabel = $MachineLabel
    # A local clone source is a personal path, so the origin goes through redaction too.
    repository   = (Protect-Text $RepoUrl)
    targetSha    = $headSha
    isolation    = 'a dedicated clone of the target SHA; no working project, build output or Library was shared'
    editor       = [ordered]@{
        version                 = $requiredVersion
        revision                = $projectRevision
        windowsStandaloneModule = $moduleState
        licenceFilePresent      = $licence.found
        licenceExpires          = $licence.expires
    }
    host         = [ordered]@{
        os      = ((Get-CimInstance Win32_OperatingSystem).Caption + ' ' + [System.Environment]::OSVersion.Version.ToString())
        git     = (Get-ToolVersion -Command 'git' -Arguments @('--version'))
        gitLfs  = (Get-ToolVersion -Command 'git' -Arguments @('lfs', 'version'))
        freeGiB = $freeGiB
    }
    steps        = @($results | ForEach-Object {
        [ordered]@{
            id = $_.id; title = $_.title; status = $_.status; reason = $_.reason
            command = $_.command; exitCode = $_.exitCode
            startedAt = $_.startedAt; durationSeconds = $_.durationSeconds
            results = $_.results; artifacts = $_.artifacts; notes = @($_.notes)
        }
    })
    redaction    = [ordered]@{
        policy       = 'personal paths, account and host names, e-mail addresses, IP addresses and any licence or credential line are removed before an artifact is written here'
        replacements = $script:RedactionHits
    }
    claim        = 'This receipt records what ran on one machine at one time. It is not an approval, and the author of the change is not an independent reviewer.'
}

$json = $receipt | ConvertTo-Json -Depth 12
$jsonPath = Join-Path $script:ReceiptDir 'receipt.json'
Write-Utf8 -Path $jsonPath -Content $json

# --- paste-ready summary ---------------------------------------------------
$statusLabel = @{ 'pass' = 'PASS'; 'fail' = 'FAIL'; 'not_run' = '미실행' }
$lines = New-Object System.Collections.ArrayList
[void] $lines.Add("## 검증 receipt — ``$headSha``")
[void] $lines.Add('')
[void] $lines.Add("- 기기: ``$MachineLabel`` · Unity $requiredVersion · Windows standalone 모듈 **$moduleState**")
[void] $lines.Add("- 시각: $($startedAt.ToString('yyyy-MM-dd HH:mm:ss')) ~ $($finishedAt.ToString('HH:mm:ss')) (UTC$($finishedAt.ToString('zzz')))")
[void] $lines.Add('- 격리: 대상 SHA 전용 clone에서 수행. 사용 중인 프로젝트·빌드 산출물은 열지 않았다.')
[void] $lines.Add("- receipt JSON SHA256: ``$(Get-Sha256 $jsonPath)``")
[void] $lines.Add('')
[void] $lines.Add('| 검사 | 결과 | 종료 코드 | 집계 | 근거 |')
[void] $lines.Add('|---|---|---|---|---|')
foreach ($step in $results) {
    $label = $statusLabel[$step.status]
    $exit = '—'
    if ($null -ne $step.exitCode) { $exit = "``$($step.exitCode)``" }
    $tally = '—'
    if ($step.results -and ($step.results.PSObject.Properties.Name -contains 'total')) {
        $tally = "$($step.results.total) 중 $($step.results.passed) 통과 / $($step.results.failed) 실패 / $($step.results.skipped) 스킵"
    }
    $evidence = (@($step.artifacts | Where-Object { $_.present } | ForEach-Object { "``$($_.name)`` ``$($_.sha256.Substring(0, 12))…``" }) -join '<br>')
    if (-not $evidence) { $evidence = '—' }
    $reason = ''
    if ($step.reason) { $reason = " — $($step.reason)" }
    [void] $lines.Add("| $($step.title) | **$label**$reason | $exit | $tally | $evidence |")
}
[void] $lines.Add('')
foreach ($step in $results) {
    if ($step.results -and ($step.results.PSObject.Properties.Name -contains 'excluded') -and $step.results.excluded.Count -gt 0) {
        [void] $lines.Add("### $($step.title) 제외 사유")
        foreach ($case in $step.results.excluded) { [void] $lines.Add("- ``$($case.name)`` — $($case.reason)") }
        [void] $lines.Add('')
    }
}
[void] $lines.Add('### 실행한 명령')
[void] $lines.Add('')
[void] $lines.Add('```')
foreach ($step in $results) { if ($step.command) { [void] $lines.Add($step.command) } }
[void] $lines.Add('```')
[void] $lines.Add('')
[void] $lines.Add('개인 경로·계정·호스트명·전자우편·IP와 라이선스 관련 줄은 게시 전 제거했다. 이 기록은 한 기기의 1회 실행 결과이며 승인이 아니다. 작성자 자체 보고는 독립 검토를 대신하지 않는다.')

$summaryPath = Join-Path $script:ReceiptDir 'receipt.md'
Write-Utf8 -Path $summaryPath -Content (($lines -join "`n") + "`n")

$manifest = @()
foreach ($file in Get-ChildItem -LiteralPath $script:ReceiptDir -File | Sort-Object Name) {
    if ($file.Name -eq 'SHA256SUMS.txt') { continue }
    $manifest += ('{0}  {1}' -f (Get-Sha256 $file.FullName), $file.Name)
}
Write-Utf8 -Path (Join-Path $script:ReceiptDir 'SHA256SUMS.txt') -Content (($manifest -join "`n") + "`n")

Write-Host ''
Write-Host "Receipt written to $script:ReceiptDir"
Write-Host '  receipt.json / receipt.md / SHA256SUMS.txt plus the redacted logs and XML'
foreach ($step in $results) { Write-Host ('  {0,-14} {1}' -f $step.status, $step.title) }
if (@($results | Where-Object { $_.status -eq 'fail' }).Count -gt 0) { exit 1 }
exit 0
