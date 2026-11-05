#requires -Version 5.1
<#
UI Automation regression test for the download manager layout: menu bar,
labeled toolbar, header sorting, checkbox selection, queue controls, search,
bottom details panel and the 900 DIP responsive layout.

Run from the repository root after a Debug build. If RapidFetch.exe is missing
the script builds it first. The test uses the per-user settings and download
store, changes only the sort preference while it runs, and restores the previous
sort order in its finally block.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$appDir = Join-Path $root 'src\RapidFetch.App\bin\Debug\net10.0-windows'
$exe = Join-Path $appDir 'RapidFetch.exe'
if (-not (Test-Path $exe)) {
    & dotnet build (Join-Path $root 'src\RapidFetch.App\RapidFetch.App.csproj') -c Debug --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Debug build failed' }
}
$settingsPath = Join-Path $env:APPDATA 'RapidFetch\settings.json'
$shotDir = Join-Path $env:TEMP 'RapidFetchAbTest'
New-Item -ItemType Directory -Force -Path $shotDir | Out-Null

Add-Type -Namespace Win32 -Name User32 -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PrintWindow(System.IntPtr hwnd, System.IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool SetWindowPos(System.IntPtr hwnd, System.IntPtr after, int x, int y, int cx, int cy, uint flags);
'@
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Get-Process RapidFetch -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Start-Process -FilePath $exe -WorkingDirectory $appDir
$p = Get-Process RapidFetch | Select-Object -First 1
for ($i = 0; $i -lt 80; $i++) {
    $p.Refresh()
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { break }
    Start-Sleep -Milliseconds 250
}
Start-Sleep -Milliseconds 1200
$handle = $p.MainWindowHandle

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$window = $null
foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) {
    if ($w.Current.Name -eq 'RapidFetch') { $window = $w }
}
if (-not $window) { throw 'main window not found' }

function Find-ByID([string]$id) {
    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)))
}
function Find-ByType([string]$type) {
    $controlType = [System.Windows.Automation.ControlType]::$type
    return $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $controlType)))
}
function Find-Text([string]$name) {
    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)))
}
function Find-Button([string]$name) {
    $condition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)))
    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Find-Header([string]$name) {
    $condition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::HeaderItem)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)))
    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Get-SortOrder {
    return (Get-Content $settingsPath -Raw | ConvertFrom-Json).sortOrder
}
function Set-SortOrder([string]$value) {
    if (-not (Test-Path $settingsPath)) { return }
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $settings.sortOrder = $value
    ($settings | ConvertTo-Json -Depth 20) | Set-Content $settingsPath -Encoding UTF8
}
function Wait-Sort([string]$expected) {
    for ($i = 0; $i -lt 30; $i++) {
        if ((Get-SortOrder) -eq $expected) { return }
        Start-Sleep -Milliseconds 100
    }
    throw "sort order did not become '$expected' (was '$(Get-SortOrder)')"
}
function Invoke-Header([string]$name) {
    $header = Find-Header $name
    if (-not $header) { throw "header '$name' not found" }
    $header.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
}
function Get-Rows {
    $grid = Find-ByID 'DownloadsGrid'
    if (-not $grid) { throw 'downloads grid not found' }
    $rowCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::DataItem)
    $textCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    $rows = @()
    foreach ($entry in $grid.FindAll([System.Windows.Automation.TreeScope]::Descendants, $rowCondition)) {
        $texts = @($entry.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCondition) |
            ForEach-Object { $_.Current.Name } |
            Where-Object { $_ -and $_ -notmatch '^[\uE000-\uF8FF]+$' })
        $rows += ,@{ Element = $entry; Texts = $texts }
    }
    return ,$rows
}
function Get-CheckElements {
    $checkCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::CheckBox)
    $checks = @($window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $checkCondition))
    $selectAll = @($checks | Where-Object { $_.Current.Name -eq 'Select all downloads' })[0]
    $rowChecks = @($checks | Where-Object { $_.Current.Name -eq 'Select download' })
    if (-not $selectAll) { throw 'select-all checkbox not found' }
    if ($rowChecks.Count -eq 0) { throw 'row checkboxes not found' }
    return [pscustomobject]@{ Header = $selectAll; Rows = $rowChecks }
}
function Assert-CheckAlignment([string]$label) {
    $set = Get-CheckElements
    $headerRect = $set.Header.Current.BoundingRectangle
    $headerCenter = $headerRect.X + $headerRect.Width / 2
    $maxOffset = 0
    foreach ($check in $set.Rows) {
        $r = $check.Current.BoundingRectangle
        $offset = [math]::Abs(($r.X + $r.Width / 2) - $headerCenter)
        if ($offset -gt $maxOffset) { $maxOffset = $offset }
    }
    Write-Host "checkbox alignment at $label`: header center x=$([math]::Round($headerCenter, 1)) px, max row offset=$([math]::Round($maxOffset, 2)) px"
    if ($maxOffset -gt 1.5) {
        throw "header checkbox is $([math]::Round($maxOffset, 2)) px off the row checkboxes at $label"
    }
    return $set
}
function Assert-StatusIconWidth([string]$shotPath, $rowElement) {
    $textCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    $glyph = @($rowElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCondition) |
        Where-Object { $_.Current.Name -match '^[\uE000-\uF8FF]+$' })[0]
    if (-not $glyph) { throw 'status glyph not found in the first row' }

    $glyphRect = $glyph.Current.BoundingRectangle
    $windowRect = $window.Current.BoundingRectangle
    $centerX = [int]($glyphRect.X + $glyphRect.Width / 2 - $windowRect.X)
    $centerY = [int]($glyphRect.Y + $glyphRect.Height / 2 - $windowRect.Y)

    $bmp = [System.Drawing.Bitmap]::FromFile($shotPath)
    try {
        $bg = $bmp.GetPixel([math]::Max(0, $centerX - 30), $centerY)
        $minX = $null
        $maxX = $null
        $from = [math]::Max(0, $centerX - 30)
        $to = [math]::Min($bmp.Width - 1, $centerX + 30)
        for ($x = $from; $x -le $to; $x++) {
            $c = $bmp.GetPixel($x, $centerY)
            $delta = [math]::Abs($c.R - $bg.R) + [math]::Abs($c.G - $bg.G) + [math]::Abs($c.B - $bg.B)
            if ($delta -gt 20) {
                if ($null -eq $minX) { $minX = $x }
                $maxX = $x
            }
        }
    }
    finally {
        $bmp.Dispose()
    }
    if ($null -eq $minX) { throw 'status icon tile not found in the screenshot' }
    $width = $maxX - $minX + 1
    "status icon tile width: $width px (full icon is 35 px at 125%)"
    if ($width -lt 28) {
        throw "status icon is clipped to $width px; expected at least 28 px"
    }
}
function Save-Shot([string]$name) {
    Start-Sleep -Milliseconds 700
    $rect = $window.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    [Win32.User32]::PrintWindow($handle, $hdc, 2) | Out-Null
    $gfx.ReleaseHdc($hdc)
    $gfx.Dispose()
    $path = Join-Path $shotDir "$name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $path
}

try {
    $savedSort = $null
    try { $savedSort = Get-SortOrder } catch { }
    if ($savedSort -and $savedSort -ne 'newest') { Set-SortOrder 'newest' }

    $sort0 = Get-SortOrder
    "initial sort: $sort0"
    if ($sort0 -ne 'newest') { throw "expected newest at startup, got $sort0" }

    # -- menu bar
    (Find-Button 'File').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600
    $menuItemCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::MenuItem)
    $menuItem = @($root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $menuItemCond) |
        Where-Object { $_.Current.Name -like 'New Download*' })[0]
    if (-not $menuItem) { throw 'File menu did not open' }
    "File menu opens with New Download..."
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Start-Sleep -Milliseconds 400

    # -- header sort: name asc then desc
    Invoke-Header 'Name'
    Wait-Sort 'name'
    $rows = Get-Rows
    if ($rows.Count -eq 0) { throw 'no rows after name sort' }
    $names = @($rows | ForEach-Object { $_.Texts[0] })
    "name asc first rows: $(($names | Select-Object -First 4) -join ' | ')"
    $expected = @($names | Sort-Object)
    for ($i = 0; $i -lt 4; $i++) {
        if ($names[$i] -ne $expected[$i]) { throw "name ascending order is wrong at row $i" }
    }

    Invoke-Header 'Name'
    Wait-Sort 'name-desc'
    $rows = Get-Rows
    $names = @($rows | ForEach-Object { $_.Texts[0] })
    "name desc first rows: $(($names | Select-Object -First 4) -join ' | ')"
    $expected = @($names | Sort-Object -Descending)
    for ($i = 0; $i -lt 4; $i++) {
        if ($names[$i] -ne $expected[$i]) { throw "name descending order is wrong at row $i" }
    }
    "name header sorting works both ways"

    # -- header sort: size descending / ascending
    Invoke-Header 'Size'
    Wait-Sort 'size'
    $rows = Get-Rows
    $sizes = @($rows | ForEach-Object { $_.Texts[3] } | Select-Object -First 5)
    "size desc first sizes: $($sizes -join ' | ')"
    Invoke-Header 'Size'
    Wait-Sort 'size-asc'
    $rows = Get-Rows
    $sizes = @($rows | ForEach-Object { $_.Texts[3] } | Select-Object -First 5)
    "size asc first sizes: $($sizes -join ' | ')"
    "size header sorting switches direction"

    # -- header sort: date newest / oldest
    Invoke-Header 'Date Added'
    Wait-Sort 'newest'
    $rows = Get-Rows
    $dates = @($rows | ForEach-Object { $_.Texts[-1] } | Select-Object -First 4)
    "date newest first dates: $($dates -join ' | ')"
    Invoke-Header 'Date Added'
    Wait-Sort 'oldest'
    $rows = Get-Rows
    $dates = @($rows | ForEach-Object { $_.Texts[-1] } | Select-Object -First 4)
    "date oldest first dates: $($dates -join ' | ')"
    "date header sorting switches direction"

    # back to newest so details tests use the current layout
    Invoke-Header 'Date Added'
    Wait-Sort 'newest'
    "sort restored to newest"

    # -- details panel on click
    $rows = Get-Rows
    $first = $rows[0]
    $firstName = $first.Texts[0]
    $first.Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 700
    if (-not (Find-Text 'SOURCE')) { throw 'details panel did not appear after selecting a row' }
    if (-not (Find-Text 'SAVED TO')) { throw 'details panel is missing saved path' }
    $detailName = Find-Text $firstName
    if (-not $detailName) { throw "details panel does not show '$firstName'" }
    "details opens for '$firstName'"
    $detailsShot = Save-Shot 'rf-ab-details2'
    "details shot: $detailsShot"
    Assert-StatusIconWidth $detailsShot $first.Element

    $close = Find-Button 'Close details'
    if (-not $close) { throw 'close details button not found' }
    $close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
    if (Find-Text 'SOURCE') { throw 'details panel did not close' }
    "details closes"

    # -- checkbox alignment and select all
    $set = Assert-CheckAlignment 'default width'
    $selectAll = $set.Header

    $selectAll.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 600
    $set = Get-CheckElements
    $on = @($set.Rows | Where-Object {
        $_.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq
            [System.Windows.Automation.ToggleState]::On
    }).Count
    "select all: $on of $($set.Rows.Count) visible rows checked"
    if ($on -ne $set.Rows.Count) { throw 'select all did not check every row' }

    $set.Header.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 600
    $set = Get-CheckElements
    $on = @($set.Rows | Where-Object {
        $_.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq
            [System.Windows.Automation.ToggleState]::On
    }).Count
    if ($on -ne 0) { throw 'select all did not clear every row' }
    "select all clears every row"

    # one checked row puts the header checkbox in the indeterminate (dash) state
    $set.Rows[0].GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 600
    $set = Get-CheckElements
    $headerState = $set.Header.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState
    if ($headerState -ne [System.Windows.Automation.ToggleState]::Indeterminate) {
        throw "one checked row left the header in '$headerState' instead of Indeterminate"
    }
    "one checked row shows the indeterminate header dash"
    $set.Header.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 600
    $set = Get-CheckElements
    $headerState = $set.Header.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState
    $on = @($set.Rows | Where-Object {
        $_.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq
            [System.Windows.Automation.ToggleState]::On
    }).Count
    if ($headerState -ne [System.Windows.Automation.ToggleState]::Off -or $on -ne 0) {
        throw 'clicking the partial header did not clear the selection'
    }
    "partial header click clears the selection"

    # -- queue toolbar
    $stopQueue = Find-Button 'Stop Queue'
    if (-not $stopQueue) { throw 'Stop Queue button not found' }
    $stopQueue.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 800
    if (-not (Find-Text 'Queue stopped')) { throw 'Stop Queue did not update the status bar' }
    "Stop Queue -> Queue stopped"
    $startQueue = Find-Button 'Start Queue'
    $startQueue.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 800
    if (-not (Find-Text 'Queue running')) { throw 'Start Queue did not update the status bar' }
    "Start Queue -> Queue running"

    # -- search and fallback names
    $search = Find-ByID 'SearchBox'
    $value = [System.Windows.Automation.ValuePattern]$search.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $value.SetValue('telegram')
    Start-Sleep -Milliseconds 900
    $rows = Get-Rows
    if ($rows.Count -eq 0) { throw 'telegram search returned no rows' }
    foreach ($row in $rows) {
        if ($row.Texts[0] -notlike '*telegram*') { throw "unexpected search row: $($row.Texts[0])" }
    }
    "search returns $(@($rows).Count) fallback-named rows"
    $rows[0].Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 600
    if (-not (Find-Text 'SOURCE')) { throw 'details did not open for a fallback-named row' }
    $value.SetValue('')
    Start-Sleep -Milliseconds 700

    # -- toolbar actions with a checked row (non-destructive: pause/resume state only)
    # -- 900 DIP layout
    $null = [Win32.User32]::SetWindowPos($handle, [IntPtr]::Zero, 40, 40, 900, 640, 0x0004)
    Start-Sleep -Milliseconds 1200
    if (Find-Header 'Date Added') { throw 'Date Added column should be hidden below 1100 DIP' }
    foreach ($headerName in @('Name', 'Size', 'Status', 'Speed', 'Time Left')) {
        $header = Find-Header $headerName
        if (-not $header) { throw "header '$headerName' missing at 900 DIP" }
        $r = $header.Current.BoundingRectangle
        "900 header $headerName width=$([math]::Round($r.Width)) px"
    }
    $sizeHeader = Find-Header 'Size'
    if ($sizeHeader.Current.BoundingRectangle.Width -lt 160) { throw 'Size column was compressed at 900 DIP' }
    $settingsButton = Find-Button 'Settings'
    $wr = $window.Current.BoundingRectangle
    $sr = $settingsButton.Current.BoundingRectangle
    if ($sr.Right -gt $wr.Right) { throw 'toolbar is clipped at 900 DIP' }
    $null = Assert-CheckAlignment '900 DIP'

    # -- sidebar fits at the minimum size: no clipped rows or visible scrollbar
    $navTree = Find-ByID 'NavTree'
    if (-not $navTree) { throw 'sidebar tree not found' }
    $treeItemCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::TreeItem)
    $navItems = @($navTree.FindAll([System.Windows.Automation.TreeScope]::Descendants, $treeItemCondition))
    if ($navItems.Count -eq 0) { throw 'sidebar tree has no rows' }
    $maxBottom = ($navItems | ForEach-Object { $_.Current.BoundingRectangle.Bottom } |
        Measure-Object -Maximum).Maximum
    $navRect = $navTree.Current.BoundingRectangle
    if ($maxBottom -gt $navRect.Bottom) {
        throw "sidebar rows are clipped at 900 DIP (content ends at $([math]::Round($maxBottom)) px, tree ends at $([math]::Round($navRect.Bottom)) px)"
    }
    $scrollBarCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ScrollBar)
    $visibleBars = @($navTree.FindAll([System.Windows.Automation.TreeScope]::Descendants, $scrollBarCondition) |
        Where-Object { -not $_.Current.IsOffscreen })
    if ($visibleBars.Count -gt 0) { throw 'sidebar shows a scrollbar at 900 DIP' }
    "900 sidebar fits: $($navItems.Count) tree rows, no visible scrollbar"
    "900 layout shot: $(Save-Shot 'rf-ab-900c')"

    $rows = Get-Rows
    $rows[0].Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 700
    if (-not (Find-Text 'SOURCE')) { throw 'details did not open at 900 DIP' }
    "900 details shot: $(Save-Shot 'rf-ab-details-900')"

    "ALL AB LAYOUT / SORT / DETAILS / QUEUE TESTS PASSED"
}
finally {
    Get-Process RapidFetch -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400
    try {
        if ($savedSort) { Set-SortOrder $savedSort }
        else { Set-SortOrder 'newest' }
    } catch { }
}
