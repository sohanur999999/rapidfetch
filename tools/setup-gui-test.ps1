#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Setup = '',
    [string]$InstallDir = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Setup)) {
    $Setup = Join-Path $PSScriptRoot '..\dist\RapidFetch-Setup-1.0.0.exe'
}
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = Join-Path $env:LOCALAPPDATA 'Temp\RapidFetchSetupGuiTest'
}
$Setup = [IO.Path]::GetFullPath($Setup)
$InstallDir = [IO.Path]::GetFullPath($InstallDir)
if (-not (Test-Path $Setup)) { throw "Setup not found: $Setup" }
if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-Window([string]$title, [int]$seconds = 20) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $title)
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $window = $root.FindFirst(
            [System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window) { return $window }
        Start-Sleep -Milliseconds 250
    }
    throw "Window '$title' did not appear."
}

function Find-Element($window, [string]$name, $controlType, [int]$seconds = 10) {
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $typeCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $controlType)
    $condition = New-Object System.Windows.Automation.AndCondition($nameCondition, $typeCondition)
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $element = $window.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 200
    }
    throw "Control '$name' did not appear."
}

$process = Start-Process -FilePath $Setup -PassThru
try {
    $window = Find-Window 'RapidFetch Setup'
    Write-Host "Setup window: $($window.Current.Name)"

    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $edits = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants, $editCondition)
    if ($edits.Count -eq 0) { throw 'Install path field was not found.' }
    $value = $edits[0].GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $value.SetValue($InstallDir)
    Write-Host "Install path set to: $InstallDir"

    $desktop = Find-Element $window 'Create a desktop shortcut' `
        ([System.Windows.Automation.ControlType]::CheckBox)
    $toggle = $desktop.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    if ($toggle.Current.ToggleState -ne
        [System.Windows.Automation.ToggleState]::Off) {
        $toggle.Toggle()
    }
    Write-Host "Desktop shortcut disabled for the test."

    $install = Find-Element $window 'Install RapidFetch' `
        ([System.Windows.Automation.ControlType]::Button)
    $install.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Write-Host "Install button pressed."

    $installed = Find-Element $window 'RapidFetch is installed' `
        ([System.Windows.Automation.ControlType]::Text) 90
    Write-Host "Success page: $($installed.Current.Name)"

    $exe = Join-Path $InstallDir 'RapidFetch.exe'
    if (-not (Test-Path $exe)) { throw "Installed exe missing: $exe" }
    foreach ($relative in @(
        'extension\manifest.json',
        'host\extension-id.txt',
        'uninstall.ps1',
        'README.md',
        'docs\screenshots\main.png')) {
        if (-not (Test-Path (Join-Path $InstallDir $relative))) {
            throw "Installed file missing: $relative"
        }
    }
    Write-Host "Installed files verified: $InstallDir"

    $manifest = Get-Content (Join-Path $env:APPDATA 'RapidFetch\host\com.rapidfetch.host.json') -Raw |
        ConvertFrom-Json
    if ($manifest.path -ne $exe) {
        throw "Native host points at '$($manifest.path)', expected '$exe'."
    }
    Write-Host "Native host points at the installed exe."

    $uninstall = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RapidFetch'
    if ($uninstall.InstallLocation -ne $InstallDir) {
        throw "Uninstall entry points at '$($uninstall.InstallLocation)'."
    }
    Write-Host "Apps & Features entry verified."

    $close = Find-Element $window 'Close' ([System.Windows.Automation.ControlType]::Button) 10
    $close.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1
    Write-Host "GUI install test passed."
}
finally {
    if (-not $process.HasExited) {
        try { $process.CloseMainWindow() | Out-Null } catch { }
        Start-Sleep -Seconds 1
        if (-not $process.HasExited) {
            try { $process.Kill() } catch { }
        }
    }
}
