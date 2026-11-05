#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputRoot = '',
    [switch]$SkipStandalone
)

# Builds the installer as a renamed copy of the self-contained single-file app.
# Setup mode is selected by the file name containing "setup" (or by --setup),
# so the same bytes install RapidFetch and, once copied as RapidFetch.exe, run it.
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $PSScriptRoot 'dist' }
$project = Join-Path $PSScriptRoot 'src\RapidFetch.App\RapidFetch.App.csproj'

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

$version = '1.0.0'
$match = [regex]::Match((Get-Content $project -Raw), '<Version>\s*([^<]+)')
if ($match.Success) { $version = $match.Groups[1].Value.Trim() }

$setup = Join-Path $OutputRoot "RapidFetch-Setup-$version.exe"
$source = Join-Path $OutputRoot "RapidFetch-$version-standalone-win-x64.exe"

# The installer cannot replace itself while it is running.
$running = Get-CimInstance Win32_Process | Where-Object {
    $_.ExecutablePath -and $_.ExecutablePath.Equals($setup, [StringComparison]::OrdinalIgnoreCase)
}
if ($running) {
    throw "The RapidFetch setup is running. Close it before rebuilding."
}

if (-not $SkipStandalone) {
    Write-Host "Building the self-contained app used as the installer..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'build-standalone.ps1') `
        -Configuration $Configuration -OutputRoot $OutputRoot
    if ($LASTEXITCODE -ne 0) { throw "Standalone build failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path $source)) {
    throw "Missing $source. Run without -SkipStandalone first."
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
Copy-Item $source $setup -Force
try { Unblock-File -Path $setup -ErrorAction SilentlyContinue } catch { }

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
"$hash  $(Split-Path $setup -Leaf)" | Set-Content "$setup.sha256" -Encoding ASCII

$sizeMb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "Setup: $setup ($sizeMb MB)" -ForegroundColor Green
Write-Host "SHA256: $hash"
Write-Host ""
Write-Host "Silent install:  RapidFetch-Setup-$version.exe --silent --launch" -ForegroundColor Cyan
Write-Host "Choose a folder: RapidFetch-Setup-$version.exe --silent --install-dir ""D:\Apps\RapidFetch""" -ForegroundColor Cyan
Write-Host "Uninstall:       %LOCALAPPDATA%\Programs\RapidFetch\uninstall.ps1" -ForegroundColor Cyan
