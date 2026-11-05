#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\RapidFetch'),
    [switch]$NoShellIntegration,
    [switch]$NoStartWithWindows,
    [switch]$DesktopShortcut,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$sourceExe = Join-Path $source 'RapidFetch.exe'
if (-not (Test-Path $sourceExe)) {
    throw "Run install.ps1 from the extracted RapidFetch folder (RapidFetch.exe not found)."
}

$install = [IO.Path]::GetFullPath($InstallDir)
$exe = Join-Path $install 'RapidFetch.exe'
$extensionIdFile = Join-Path $source 'host\extension-id.txt'
if (-not (Test-Path $extensionIdFile)) {
    throw "The package is incomplete: host\extension-id.txt is missing."
}
$extensionId = (Get-Content $extensionIdFile -Raw).Trim()

Write-Host "RapidFetch installer" -ForegroundColor Cyan
Write-Host "  source: $source"
Write-Host "  target: $install"

# ---------------------------------------------------------------- stop running app
$running = Get-Process RapidFetch -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping RapidFetch so files can be replaced..."
    foreach ($proc in $running) {
        try { $proc.CloseMainWindow() | Out-Null } catch { }
    }
    Start-Sleep -Seconds 2
    Get-Process RapidFetch -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# ---------------------------------------------------------------- copy program files
if ([IO.Path]::GetFullPath($source).TrimEnd('\') -ne $install.TrimEnd('\')) {
    New-Item -ItemType Directory -Force -Path $install | Out-Null
    Get-ChildItem -Path $source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $install -Recurse -Force
    }
    Write-Host "Copied program files." -ForegroundColor Green
} else {
    Write-Host "Already running from the install folder; skipping the copy."
}

if (-not (Test-Path $exe)) {
    throw "Install failed: $exe was not created."
}

# ---------------------------------------------------------------- native host
$appData = Join-Path $env:APPDATA 'RapidFetch'
$hostDir = Join-Path $appData 'host'
New-Item -ItemType Directory -Force -Path $hostDir | Out-Null
$manifestPath = Join-Path $hostDir 'com.rapidfetch.host.json'
$manifest = [ordered]@{
    name            = 'com.rapidfetch.host'
    description     = 'RapidFetch native messaging bridge'
    path            = $exe
    type            = 'stdio'
    allowed_origins = @("chrome-extension://$extensionId/")
}
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifestPath,
    ($manifest | ConvertTo-Json -Depth 5), $utf8NoBom)

$browserKeys = @(
    'Software\Google\Chrome\NativeMessagingHosts',
    'Software\Microsoft\Edge\NativeMessagingHosts',
    'Software\BraveSoftware\Brave-Browser\NativeMessagingHosts',
    'Software\Chromium\NativeMessagingHosts',
    'Software\Vivaldi\NativeMessagingHosts',
    'Software\Opera Software\NativeMessagingHosts'
)
foreach ($browser in $browserKeys) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("$browser\com.rapidfetch.host")
    if ($key) {
        $key.SetValue('', $manifestPath)
        $key.Close()
    }
}
Write-Host "Registered native messaging host for Chromium browsers." -ForegroundColor Green

# ---------------------------------------------------------------- shell verbs
function Set-ShellVerb([string]$relativeKey, [string]$label) {
    $base = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\$relativeKey")
    if (-not $base) { return }
    $base.SetValue('', $label)
    $base.SetValue('Icon', $exe)
    $command = $base.CreateSubKey('command')
    if ($command) {
        $command.SetValue('', "`"$exe`" --add `"%1`"")
        $command.Close()
    }
    $base.Close()
}

function Remove-ShellVerb([string]$relativeKey) {
    try {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree(
            "Software\Classes\$relativeKey", $false)
    } catch { }
}

if (-not $NoShellIntegration) {
    Set-ShellVerb 'InternetShortcut\shell\RapidFetch' 'Download with RapidFetch'
    Set-ShellVerb 'Directory\shell\RapidFetch' 'RapidFetch download here'
    Write-Host "Added Explorer context menu entries." -ForegroundColor Green
} else {
    Remove-ShellVerb 'InternetShortcut\shell\RapidFetch'
    Remove-ShellVerb 'Directory\shell\RapidFetch'
}
# Remove the "every file" verb used by earlier preview builds.
Remove-ShellVerb '*\shell\RapidFetch'

# ---------------------------------------------------------------- startup
$run = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey(
    'Software\Microsoft\Windows\CurrentVersion\Run')
if ($run) {
    if (-not $NoStartWithWindows) {
        $run.SetValue('RapidFetch', "`"$exe`" --minimized")
        Write-Host "Enabled start with Windows." -ForegroundColor Green
    } else {
        $run.DeleteValue('RapidFetch', $false)
    }
    $run.Close()
}

# ---------------------------------------------------------------- settings defaults
$settingsPath = Join-Path $appData 'settings.json'
$settings = $null
if (Test-Path $settingsPath) {
    Copy-Item $settingsPath "$settingsPath.bak" -Force
    try { $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json } catch { }
}
if ($null -eq $settings) { $settings = New-Object psobject }

function Set-Setting([object]$object, [string]$name, [object]$value) {
    if ($object.PSObject.Properties[$name]) {
        $object.$name = $value
    } else {
        $object | Add-Member -NotePropertyName $name -NotePropertyValue $value
    }
}

Set-Setting $settings 'BrowserIntegrationEnabled' $true
Set-Setting $settings 'ShellIntegration' (-not $NoShellIntegration)
Set-Setting $settings 'RunAtStartup' (-not $NoStartWithWindows)
[IO.File]::WriteAllText($settingsPath,
    ($settings | ConvertTo-Json -Depth 20), $utf8NoBom)

# ---------------------------------------------------------------- shortcuts
$shell = New-Object -ComObject WScript.Shell
function New-RapidFetchShortcut([string]$path) {
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $install
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = 'RapidFetch Download Manager'
    $shortcut.Save()
}

$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null
New-RapidFetchShortcut (Join-Path $startMenuDir 'RapidFetch.lnk')

if ($DesktopShortcut) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    New-RapidFetchShortcut (Join-Path $desktop 'RapidFetch.lnk')
    Write-Host "Created desktop shortcut." -ForegroundColor Green
}

# ---------------------------------------------------------------- Apps & Features entry
$uninstallKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey(
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\RapidFetch')
if ($uninstallKey) {
    $uninstallKey.SetValue('DisplayName', 'RapidFetch Download Manager')
    $uninstallKey.SetValue('DisplayVersion', '1.0.0')
    $uninstallKey.SetValue('Publisher', 'RapidFetch')
    $uninstallKey.SetValue('DisplayIcon', $exe)
    $uninstallKey.SetValue('InstallLocation', $install)
    $uninstallKey.SetValue('UninstallString',
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$install\uninstall.ps1`"")
    $uninstallKey.SetValue('NoModify', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
    $uninstallKey.SetValue('NoRepair', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
    $uninstallKey.Close()
}

Write-Host ""
Write-Host "RapidFetch is installed." -ForegroundColor Cyan
Write-Host "Extension ID: $extensionId"
Write-Host "Load this folder once in your browser:"
Write-Host "  $install\extension"
Write-Host ""
Write-Host "Edge:   edge://extensions   -> Developer mode -> Load unpacked"
Write-Host "Chrome: chrome://extensions -> Developer mode -> Load unpacked"
Write-Host ""

if (-not $NoLaunch) {
    Start-Process -FilePath $exe -WorkingDirectory $install
}
