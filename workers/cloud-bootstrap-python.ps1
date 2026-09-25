param([Parameter(Mandatory = $true)][string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest
if ($env:CG_CLOUD_BUILD -ne 'win-x64' -or $env:IS_BUILDER -ne 'true' -or $env:BUILDER_OS -ne 'WINDOWS' -or [Environment]::OSVersion.Platform -ne 'Win32NT') {
    throw 'Python bootstrap requires explicit Windows UBA configuration.'
}
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
if ($root -ne (Split-Path -Parent $PSScriptRoot)) {
    throw 'Bootstrap project root must be the checkout containing this script.'
}
$physics = Join-Path $PSScriptRoot 'physics'
$lock = Get-Content -LiteralPath (Join-Path $physics 'runtime-artifacts.lock.json') -Raw | ConvertFrom-Json
$artifact = $lock.artifacts.($lock.platforms.'win-x64'.python)
$url = [Uri]$artifact.url
if ($lock.schemaVersion -ne 1 -or $artifact.sha256 -cnotmatch '^[0-9a-f]{64}$' -or $url.Scheme -ne 'https' -or $url.Host -ne 'www.python.org' -or -not $url.AbsolutePath.EndsWith('-embed-amd64.zip')) {
    throw 'Bootstrap requires the reviewed official Windows x64 embeddable Python artifact.'
}

# Match package_runtime.fetch's content-addressed cache; it will reverify and reuse
# these bytes when assembling the Player runtime rather than downloading twice.
$cache = Join-Path $physics '.package-cache'
[void][IO.Directory]::CreateDirectory($cache)
$archive = Join-Path $cache $artifact.sha256
if (-not (Test-Path -LiteralPath $archive)) {
    $download = Join-Path $cache ('.download-' + [Guid]::NewGuid().ToString('N'))
    try {
        [Console]::Error.WriteLine('Downloading locked cloud packaging Python: ' + $artifact.url)
        Invoke-WebRequest -UseBasicParsing -Uri $artifact.url -OutFile $download -TimeoutSec 120
        if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.sha256) {
            throw 'Bootstrap Python artifact SHA256 mismatch.'
        }
        Move-Item -LiteralPath $download -Destination $archive
    } finally {
        if (Test-Path -LiteralPath $download) {
            Remove-Item -LiteralPath $download
        }
    }
} elseif ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.sha256) {
    throw 'Cached bootstrap Python artifact SHA256 mismatch.'
}

# Fresh extraction outside the checkout; no machine install, pip, venv or SDK.
# Keep the original isolated ._pth; cloud_prepare adds its own worker import path.
$destination = Join-Path ([IO.Path]::GetTempPath()) ('chooguard-uba-python-' + [Guid]::NewGuid().ToString('N'))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $destination)
$python = Join-Path $destination 'python.exe'
if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
    throw 'Pinned embedded Python archive is missing python.exe.'
}
[Console]::WriteLine($python)
