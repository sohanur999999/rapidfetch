#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputRoot = '',
    [switch]$FrameworkDependent,
    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $PSScriptRoot 'dist' }
$project = Join-Path $PSScriptRoot 'src\RapidFetch.App\RapidFetch.App.csproj'
$stage = Join-Path $OutputRoot 'RapidFetch'

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

$running = Get-Process RapidFetch -ErrorAction SilentlyContinue
if ($running) {
    throw "RapidFetch is running. Close the app (and any browser native-host process) before building."
}

$version = '1.0.0'
$match = [regex]::Match((Get-Content $project -Raw), '<Version>\s*([^<]+)')
if ($match.Success) { $version = $match.Groups[1].Value.Trim() }

Write-Host "RapidFetch $version - $Configuration" -ForegroundColor Cyan
Write-Host "Publishing to $stage"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-o', $stage,
    '--nologo',
    '-v', 'minimal',
    '-p:SatelliteResourceLanguages=en'
)

if (-not $FrameworkDependent) {
    $publishArgs += @('-r', 'win-x64', '--self-contained', 'true')
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# Symbols are not useful in a release package.
Get-ChildItem $stage -Recurse -File -Filter *.pdb | Remove-Item -Force

# Ship documentation and the installers next to the executable.
Copy-Item (Join-Path $PSScriptRoot 'README.md') $stage -Force
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') $stage -Force
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $stage -Force
if (Test-Path (Join-Path $PSScriptRoot 'docs')) {
    Copy-Item (Join-Path $PSScriptRoot 'docs') $stage -Recurse -Force
}

$required = @(
    'RapidFetch.exe',
    'install.ps1',
    'uninstall.ps1',
    'README.md',
    'host\extension-id.txt',
    'extension\manifest.json'
)
foreach ($item in $required) {
    if (-not (Test-Path (Join-Path $stage $item))) {
        throw "Publish output is missing $item"
    }
}

$sizeMb = [math]::Round(((Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "Staged $sizeMb MB in $stage" -ForegroundColor Green

if (-not $SkipZip) {
    $flavor = if ($FrameworkDependent) { 'slim' } else { 'win-x64' }
    $zip = Join-Path $OutputRoot "RapidFetch-$version-$flavor.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open(
        $zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $base = $stage.TrimEnd('\') + '\'
        $rootName = Split-Path $stage -Leaf
        Get-ChildItem $stage -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($base.Length).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $_.FullName, "$rootName/$relative",
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally {
        $archive.Dispose()
    }

    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash
    $hashFile = "$zip.sha256"
    "$hash  $(Split-Path $zip -Leaf)" | Set-Content $hashFile -Encoding ASCII

    $zipMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "Zip: $zip ($zipMb MB)" -ForegroundColor Green
    Write-Host "SHA256: $hash"
}
