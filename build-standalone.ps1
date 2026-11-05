#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $PSScriptRoot 'dist' }
$project = Join-Path $PSScriptRoot 'src\RapidFetch.App\RapidFetch.App.csproj'
$stage = Join-Path $OutputRoot 'standalone'

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

$version = '1.0.0'
$match = [regex]::Match((Get-Content $project -Raw), '<Version>\s*([^<]+)')
if ($match.Success) { $version = $match.Groups[1].Value.Trim() }

$final = Join-Path $OutputRoot "RapidFetch-$version-standalone-win-x64.exe"
$running = Get-CimInstance Win32_Process | Where-Object {
    $_.ExecutablePath -and (
        $_.ExecutablePath.Equals($final, [StringComparison]::OrdinalIgnoreCase) -or
        $_.ExecutablePath.Equals((Join-Path $stage 'RapidFetch.exe'), [StringComparison]::OrdinalIgnoreCase))
}
if ($running) {
    throw "The standalone executable is running. Close it before rebuilding."
}

Write-Host "RapidFetch $version - standalone single-file ($Configuration)" -ForegroundColor Cyan
Write-Host "Publishing to $stage"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-o', $stage,
    '--nologo',
    '-v', 'minimal',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:RapidFetchStandalone=true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-p:SatelliteResourceLanguages=en'
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Get-ChildItem $stage -Recurse -File -Filter *.pdb | Remove-Item -Force

$exe = Join-Path $stage 'RapidFetch.exe'
if (-not (Test-Path $exe)) { throw "Publish output is missing RapidFetch.exe" }

$extras = @(Get-ChildItem $stage -Recurse -File | Where-Object { $_.FullName -ne $exe })
if ($extras.Count -gt 0) {
    $names = ($extras | ForEach-Object { $_.FullName.Substring($stage.Length + 1) }) -join ', '
    throw "Single-file publish left extra files: $names"
}

Copy-Item $exe $final -Force
$hash = (Get-FileHash $final -Algorithm SHA256).Hash
"$hash  $(Split-Path $final -Leaf)" | Set-Content "$final.sha256" -Encoding ASCII

$sizeMb = [math]::Round((Get-Item $final).Length / 1MB, 1)
Write-Host "Standalone: $final ($sizeMb MB)" -ForegroundColor Green
Write-Host "SHA256: $hash"
Write-Host "No installer, runtime, extension folder, or other files required." -ForegroundColor Green
