#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$RemoveData,
    [switch]$KeepInstallDir
)

$ErrorActionPreference = 'Stop'
$install = [IO.Path]::GetFullPath($PSScriptRoot)
$appData = Join-Path $env:APPDATA 'RapidFetch'

Write-Host "RapidFetch uninstaller" -ForegroundColor Cyan
Write-Host "  program: $install"

$running = Get-Process RapidFetch -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping RapidFetch..."
    foreach ($proc in $running) {
        try { $proc.CloseMainWindow() | Out-Null } catch { }
    }
    Start-Sleep -Seconds 2
    Get-Process RapidFetch -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# ---------------------------------------------------------------- registry cleanup
$browserKeys = @(
    'Software\Google\Chrome\NativeMessagingHosts',
    'Software\Microsoft\Edge\NativeMessagingHosts',
    'Software\BraveSoftware\Brave-Browser\NativeMessagingHosts',
    'Software\Chromium\NativeMessagingHosts',
    'Software\Vivaldi\NativeMessagingHosts',
    'Software\Opera Software\NativeMessagingHosts'
)
foreach ($browser in $browserKeys) {
    try {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree(
            "$browser\com.rapidfetch.host", $false)
    } catch { }
}

foreach ($verb in @(
    '*\shell\RapidFetch',
    'Directory\shell\RapidFetch',
    'InternetShortcut\shell\RapidFetch'
)) {
    try {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree(
            "Software\Classes\$verb", $false)
    } catch { }
}

try {
    $run = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(
        'Software\Microsoft\Windows\CurrentVersion\Run', $true)
    if ($run) {
        $run.DeleteValue('RapidFetch', $false)
        $run.Close()
    }
} catch { }

try {
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree(
        'Software\Microsoft\Windows\CurrentVersion\Uninstall\RapidFetch', $false)
} catch { }

# ---------------------------------------------------------------- shortcuts
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\RapidFetch.lnk'
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'RapidFetch.lnk'
foreach ($shortcut in @($startMenu, $desktop)) {
    if (Test-Path $shortcut) { Remove-Item $shortcut -Force }
}
Write-Host "Removed shortcuts and browser/Explorer registrations." -ForegroundColor Green

# ---------------------------------------------------------------- data
if ($RemoveData) {
    if (Test-Path $appData) {
        Remove-Item $appData -Recurse -Force
        Write-Host "Removed settings and logs from $appData." -ForegroundColor Green
    }
} else {
    Write-Host "Kept settings, logs and download history in $appData."
}

# ---------------------------------------------------------------- program files
if (-not $KeepInstallDir) {
    if ([IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') -eq $install.TrimEnd('\')) {
        $cmd = "ping -n 3 127.0.0.1 >nul & rmdir /s /q `"$install`""
        Start-Process -FilePath "$env:SystemRoot\System32\cmd.exe" `
            -ArgumentList @('/c', $cmd) -WindowStyle Hidden
        Write-Host "Program folder will be removed in a moment."
    } else {
        Remove-Item $install -Recurse -Force
        Write-Host "Removed $install." -ForegroundColor Green
    }
} else {
    Write-Host "Kept program files in $install."
}

Write-Host ""
Write-Host "RapidFetch has been uninstalled." -ForegroundColor Cyan
Write-Host "Remove the RapidFetch extension from the browser's extensions page if it is still loaded."
