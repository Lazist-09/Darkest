# tools/dsh/ui_sweep.ps1 -- one-shot UI audit sweep (UI owner, 2026-09-21)
#
# Why: the UI audit caliber (camera 1280x720 content demand / overlap / transparent frames)
#      was only reproducible by hand-grepping logs. This script fixes the entrance so the
#      capability does not disappear when people change.
#
# Usage (works in Windows PowerShell 5.1 and PowerShell 7):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/ui_sweep.ps1
#   powershell ... -File tools/dsh/ui_sweep.ps1 -OutDir reports -QuitAfter 900
#
# Caliber (per entry) -- ui_spec 14.5:「相机口径内 + 可见 Label 两两不相交 + Panel/PanelContainer BgColor.a==1」
# NOTE: 14.5 的第三项「禁手写 Position/Size」尚未做成硬门（手绘的地图/迷你地图会误伤）⇒ 待定基线后再定，未接线就明说。
#   * content demand over camera must be 0        ("内容需求超出相机")
#   * overlap pairs / transparent frames must be 0 ("重叠对 N ／ 透明框 M")
#   * real ERROR (^ERROR:, excluding engine-exit noise) must be 0
#   * every entry must produce log lines (>0) -- an empty log is a FAIL, never a pass
# Exit code: 0 = all green, 1 = at least one entry failed.

[CmdletBinding()]
param(
    [int]$QuitAfter = 900,
    [string]$OutDir = 'reports',
    [string[]]$Only = @()
)

$ErrorActionPreference = 'Stop'

# -- entries: name + CLI args (the flags are the contract) -------------------
$Entries = @(
    @{ N = 'hamlet';          A = @('--hamlet') }
    @{ N = 'hamlet-longtext'; A = @('--hamlet', '--ui-longtext') }
    @{ N = 'hamlet-menu';     A = @('--hamlet', '--hamlet-menu') }
    @{ N = 'hamlet-provision'; A = @('--hamlet', '--hamlet-provision') }   # DD 1:1 供应屏（骨架采用留痕）
    @{ N = 'hamlet-quest-select'; A = @('--hamlet', '--hamlet-quest-select') }   # DD 1:1 任务选择屏
    @{ N = 'hamlet-building'; A = @('--hamlet', '--hamlet-building=tavern') }
    @{ N = 'hero-detail';     A = @('--hamlet', '--hamlet-hero-detail=0') }
    @{ N = 'hamlet-hover';     A = @('--hamlet', '--hamlet-hover=tavern') }
    @{ N = 'hamlet-hover-abbey';      A = @('--hamlet', '--hamlet-hover=abbey') }        # Track 4(a)：逐栋悬停读数（修道院）`n    @{ N = 'hamlet-hover-stagecoach'; A = @('--hamlet', '--hamlet-hover=stagecoach') }   # Track 4(a)：逐栋悬停读数（驿站）   # Track 4(a)：建筑悬停信息（名称/功能/等级/下级所需）正向留痕 ✓
    @{ N = 'hamlet-hover-stagecoach'; A = @('--hamlet', '--hamlet-hover=stagecoach') }   # Track 4(a)：逐栋悬停读数（驿站）
    @{ N = 'main-menu';       A = @() }
    @{ N = 'battle';          A = @('--click-menu=0') }
    @{ N = 'battle-longtext'; A = @('--click-menu=0', '--ui-longtext') }
    @{ N = 'battle-tab4';     A = @('--click-menu=0', '--battle-tab=4') }
    @{ N = 'map-mode';        A = @('--click-menu=0', '--battle-map-mode') }
    @{ N = 'tile-walk';       A = @('--click-menu=0', '--battle-map-mode', '--tile-walk') }
    @{ N = 'dungeon-in-scene';A = @('--dungeon-in-scene', '--smoke=main:0,map:0,auto,map:0,auto') }
    @{ N = 'settle';          A = @('--click-menu=0', '--battle-auto-finish') }
    @{ N = 'abandon';         A = @('--smoke=main:1,abandon') }
)

# -- find Godot (env -> PATH -> common install dirs) ------------------------
function Resolve-Godot {
    if ($env:DARKEST_GODOT -and (Test-Path $env:DARKEST_GODOT)) { return $env:DARKEST_GODOT }
    foreach ($n in @('godot', 'godot4', 'Godot')) {
        $p = Get-Command $n -ErrorAction SilentlyContinue
        if ($p) { return $p.Source }
    }
    $cands = @(
        'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe',
        'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe',
        "$env:LOCALAPPDATA\Programs\Godot\Godot_v4.6-stable_mono_win64.exe",
        'C:\Godot\Godot_v4.6-stable_mono_win64.exe'
    )
    foreach ($c in $cands) { if (Test-Path $c) { return $c } }
    throw 'Godot not found: set $env:DARKEST_GODOT=<exe path>, or add godot to PATH'
}

$godot = Resolve-Godot
$proj  = (Join-Path $PSScriptRoot '..\..\darkest')

# 🔴🔴 2026-09-21 修（实测崩溃）：**必须把 APPDATA 指到可写目录** —— 否则 Godot 写 `user://logs/…` 失败
#    ⇒ `Failed to open user://logs/…` ⇒ **Program crashed with signal 11**（Windows 弹"该内存不能 read"）⚠️
#    手动跑时一直设了它，脚本漏了 ⇒ 这里补上并把 user 数据与仓库隔离 ✓
$env:APPDATA = (Join-Path (Resolve-Path $proj).Path '.tmp_appdata')
if (-not (Test-Path $env:APPDATA)) { New-Item -ItemType Directory -Path $env:APPDATA | Out-Null }
$stamp = Get-Date -Format 'yyyyMMdd_HHmm'
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
if (-not (Test-Path $proj)) { throw "project path not found: $proj" }

# --- 每入口期望留痕（治本：入口"空跑"不得判绿；缺留痕即 FAIL）---
$TraceExpect = @{
    'hamlet'                  = '采用骨架'
    'hamlet-longtext'         = '采用骨架'
    'hamlet-menu'             = ''   # TODO：定义期望留痕（当前菜单开启不打印可断言串）
    'hamlet-building'         = 'UI 建筑弹窗'
    'hamlet-hover'            = '悬停建筑'
    'hamlet-hover-abbey'      = '悬停建筑'
    'hamlet-hover-stagecoach' = '悬停建筑'
    'hamlet-provision'        = 'UI 供应'
    'hamlet-quest-select'     = 'UI 任务选择'
    'hero-detail'             = 'UI 英雄面板'
    'main-menu'               = 'UI 骨架'
    'battle'                  = 'UI 战斗'
    'battle-longtext'         = 'UI 战斗'
    'battle-tab4'             = 'UI 战斗'
    'map-mode'                = 'UI 战斗'
    'tile-walk'               = ''   # TODO：瓷砖自证行仅在瓷砖分支进入时打印 ⇒ 待定断言
    'dungeon-in-scene'        = ''   # TODO：该入口走宿主内进地牢，未建战斗 UI ⇒ 待定断言
    'settle'                  = 'StatusTray'
    'abandon'                 = '放弃'
}

$run = $Entries   # 默认跑全表 ✓
$onlyList = @(); foreach ($o in $Only) { foreach ($x in ($o -split ',')) { if ($x.Trim() -ne '') { $onlyList += $x.Trim() } } }   # PS 5.1: -Only a,b arrives as one string ✓
if ($onlyList.Count -gt 0) { $run = @($Entries | Where-Object { $onlyList -contains $_.N }) }
if ($run.Count -eq 0) { throw 'no entries selected' }

$summary = Join-Path $OutDir "ui_sweep_$stamp.txt"
"# UI sweep ($stamp) -- entries=$($run.Count) -- quit-after=$QuitAfter" | Set-Content $summary -Encoding UTF8

$bad = 0
$rows = @()
foreach ($e in $run) {
    $argv = @('--path', $proj, '--headless', '--audio-driver', 'Dummy', '--fixed-fps', '60', '--ui-audit') + $e.A + @('--quit-after', $QuitAfter)
    $log  = Join-Path $OutDir "ui_$($e.N)_$stamp.txt"
    Write-Host ("[ui] {0,-18}" -f $e.N) -ForegroundColor Cyan

    # mono Godot: capturing via PS pipes yields 0 lines -> use cmd redirection (as smoke.ps1 learned)
    $cmdLine = '"' + $godot + '" ' + (($argv | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' ') + ' > "' + $log + '" 2>&1'
    cmd /c $cmdLine | Out-Null

    $lines = @(Get-Content $log -ErrorAction SilentlyContinue)
    $empty = ($lines.Count -eq 0)

    # demand over camera
    $demand = 0
    $dl = $lines | Select-String -Pattern 'content demand' -SimpleMatch
    $dl2 = $lines | Select-String -Pattern 'content demand over camera'
    $dl3 = $lines | Select-String -Pattern ([char]0x5185 + [char]0x5BB9 + [char]0x9700 + [char]0x6C42 + [char]0x8D85 + [char]0x51FA + [char]0x76F8 + [char]0x673A)
    $hit = @($dl) + @($dl2) + @($dl3)
    if ($hit.Count -gt 0) {
        $m = [regex]::Match($hit[0].Line, '(\d+)')
        if ($m.Success) { $demand = [int]$m.Value }
    }

    # overlap / transparent
    $ov = 0; $tr = 0
    $ol = $lines | Select-String -Pattern ([char]0x91CD + [char]0x53E0 + [char]0x5BF9)
    if ($ol.Count -gt 0) {
        $m2 = [regex]::Match($ol[-1].Line, ([char]0x91CD + [char]0x53E0 + [char]0x5BF9 + '\s*(\d+)'))
        if ($m2.Success) { $ov = [int]$m2.Groups[1].Value }
        $m3 = [regex]::Match($ol[-1].Line, ([char]0x900F + [char]0x660E + [char]0x6846 + '\s*(\d+)'))
        if ($m3.Success) { $tr = [int]$m3.Groups[1].Value }
    }

    # real ERROR (exclude engine-exit noise)
    $all = @($lines | Select-String -Pattern '^ERROR:')
    $noisePat = 'certificate store|leaked at exit|RID allocations|resources still in use at exit'
    $real = @($all | Where-Object { $_.Line -notmatch $noisePat })

    $expect = if ($TraceExpect.ContainsKey($e.N)) { [string]$TraceExpect[$e.N] } else { '' }
    $traceOk = ($expect -eq '') -or (@($lines | Select-String -Pattern $expect -SimpleMatch).Count -gt 0)   # 期望留痕必须出现
    $fail = ($empty -or $demand -gt 0 -or $ov -gt 0 -or $tr -gt 0 -or $real.Count -gt 0 -or (-not $traceOk))
    if ($fail) { $bad++ }

    $spec145 = if ($demand -eq 0 -and $ov -eq 0 -and $tr -eq 0) { 'ok' } else { 'FAIL' }   # §14.5: 相机口径+Label不相交+Panel不透明
    $row = "{0,-18} | lines={1,-6} | spec14.5={2} | demand={3} | overlap={4} | transparent={5} | realERROR={6} | trace={7} | {8}" -f `
        $e.N, $lines.Count, $spec145, $demand, $ov, $tr, $real.Count, $(if ($expect -eq '') { '(none)' } elseif ($traceOk) { $expect } else { "MISSING:" + $expect }), $(if ($empty) { 'FAIL(empty log)' } elseif ($fail) { 'FAIL' } else { 'ok' })
    $rows += $row
    Write-Host ("         " + $row) -ForegroundColor $(if ($fail) { 'Red' } else { 'Green' })
    $row | Add-Content $summary -Encoding UTF8
    if ($real.Count -gt 0) { $real | Select-Object -First 3 | ForEach-Object { ("    " + $_.Line.Trim()) | Add-Content $summary -Encoding UTF8 } }
}

"" | Add-Content $summary -Encoding UTF8
"# summary: entries=$($run.Count) failed=$bad" | Add-Content $summary -Encoding UTF8
Write-Host ""
Write-Host "log: $summary" -ForegroundColor Yellow
Write-Host $(if ($bad -gt 0) { "FAIL: $bad entry(ies) not green" } else { "OK: all entries green" }) -ForegroundColor $(if ($bad -gt 0) { 'Red' } else { 'Green' })
exit $(if ($bad -gt 0) { 1 } else { 0 })
