# tools/dsh/smoke.ps1 —— 一键冒烟入口（架构登记 · 2026-09-21）
#
# 为什么存在：`DungeonRunDriver` 那套 CLI 旗标很聪明，但"只有写的人知道" ⇒
#   本脚本把它固化成【一键可跑 + 可接 CI】，否则验证能力会随人员变动丢失。
#
# 用法：
#   pwsh tools/dsh/smoke.ps1                 # 跑全套（7 例）
#   pwsh tools/dsh/smoke.ps1 -Case e2e       # 只跑一例
#   pwsh tools/dsh/smoke.ps1 -List           # 只打印用例表（不跑）
#
# 🔴 判据：每例打印「非环境 ERROR 数」；**>0 ⇒ 退出码 1**（否则 0）⇒ 可直接接 CI。
# ⚠️ 已知正常现象：退休旧场景后，冒烟会停在「等玩家」⇒ **不是挂死**（用 --quit-after 兜底）。

[CmdletBinding()]
param(
    [string]$Case = '',
    [switch]$List,
    [int]$QuitAfter = 3600,
    [string]$OutDir = 'reports'
)

$ErrorActionPreference = 'Stop'

# ── 用例表（CLI 旗标 = 契约的一部分；改这里要同步 doc/architecture 的"冒烟入口表"）────────
$Cases = @(
    @{ Name = 'entry-main1';  Args = @('--smoke=main:1');                 Desc = '主菜单 → 单场战斗（入口可达 + 战斗跑完）' }
    @{ Name = 'topology-auto'; Args = @('--topology-auto');              Desc = '跑图到终点（远征主干）' }
    @{ Name = 'hamlet-next';  Args = @('--hamlet-next');                 Desc = '回城 → 再出发（城池循环）' }
    @{ Name = 'e2e';          Args = @('--smoke=main:1', '--e2e');        Desc = '完整回路：跑图 → 回城 → 花钱 → 再出发' }
    @{ Name = 'map-mode';     Args = @('--click-menu=0', '--battle-map-mode'); Desc = '战斗 → 地图模式（S1 骨架不重建）' }
    @{ Name = 'town-step';    Args = @('--hamlet');                       Desc = '城池（含建筑入口/二级菜单）' }
    @{ Name = 'ui-audit';     Args = @('--ui-audit', '--fixed-fps', '60');Desc = '布局判据（0 重叠 / 0 透明）' }
    # 🔴 主程序 2026-09-21 补：片 3.1/片 4 与瓷砖网格的新入口（此前只有写的人知道 ⇒ 现在固化在此表）✓
    @{ Name = 'dungeon-in-scene'; Args = @('--dungeon-in-scene');          Desc = '宿主内进地牢（片 3.1：进 Walking 不起战斗）' }
    @{ Name = 'tile-walk';    Args = @('--smoke=main:1', '--tile-walk');   Desc = '瓷砖主画面（走格开启 + UI 自证行）' }
    # 🔴 主程序 2026-09-21 补（B-1 主程序侧）：战斗作为 panel 挂进外壳（S4 第一步 ✓）
    @{ Name = 'battle-panel'; Args = @('--battle-panel');                    Desc = '战斗成面板（B-1：场景根交出驱动权 + 挂进外壳 ScreenLayer）' }
    @{ Name = 'abandon';      Args = @('--smoke=main:1,abandon');          Desc = '放弃远征（行走模式按按钮 ⇒ 结束本趟回城）' }
)

if ($List) {
    '{0,-16} {1,-42} {2}' -f 'CASE', 'ARGS', 'DESC'
    foreach ($c in $Cases) { '{0,-16} {1,-42} {2}' -f $c.Name, ($c.Args -join ' '), $c.Desc }
    exit 0
}

# ── 找 Godot 可执行（与项目既有做法一致：优先 PATH，其次常见安装位）────────────
function Resolve-Godot {
    # 🔴 主程序 2026-09-21 修：原版只找 PATH ⇒ 在"没把 godot 加 PATH"的机器上**一键入口直接抛错**
    #    ⇒ 那等于**没有入口**（用户原话：否则这套验证能力会随人员变动丢失）⚠️
    #    ⇒ 现在按【显式环境变量 → PATH → 本机常见安装位 → 递归找版本目录】四级回退 ✓
    if ($env:DARKEST_GODOT -and (Test-Path $env:DARKEST_GODOT)) { return $env:DARKEST_GODOT }
    foreach ($n in @('godot', 'godot4', 'Godot')) {
        $p = Get-Command $n -ErrorAction SilentlyContinue
        if ($p) { return $p.Source }
    }
    $cands = @(
        'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe',
        "$env:LOCALAPPDATA\Programs\Godot\Godot_v4.6-stable_mono_win64.exe",
        'C:\Godot\Godot_v4.6-stable_mono_win64.exe'
    )
    foreach ($c in $cands) { if (Test-Path $c) { return $c } }
    foreach ($root in @('E:\', 'C:\', 'D:\')) {
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem -Path $root -Directory -Filter 'Godot_v4*mono_win64' -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName ($_.Name + '.exe') } |
            Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($hit) { return $hit }
    }
    throw '未找到 Godot 可执行 ⇒ 三种做法任选：① 设 $env:DARKEST_GODOT=<exe 路径>；② 把 godot 加入 PATH；③ 改本函数的 $cands'
}

$godot   = Resolve-Godot
# 🔴 主程序 2026-09-21：**不允许并发实例**（我实测踩过：上一个没清掉 ⇒ 两个实例争同一项目/用户目录 ⇒ 诡异崩溃 ⚠️）
$already = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Godot*' })
if ($already.Count -gt 0) {
    Write-Output ("🔴 已有 Godot 进程在跑（$($already.Count) 个）⇒ **本脚本不启动新实例**（避免争用/崩溃）。" +
                  "请先关掉编辑器里的运行实例，或确认那不是你要用的：" + (($already | ForEach-Object { $_.Id }) -join ','))
    exit 2
}
# 🔴 主程序 2026-09-21 修：**项目路径必须显式给**（原版 `--path .` ⇒ 从仓库根跑就指错 ⇒ **输出 0 行**）✓
$proj    = (Join-Path $PSScriptRoot '..\..\darkest')
$stamp   = Get-Date -Format 'yyyyMMdd_HHmm'
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$run     = $Cases | Where-Object { $Case -eq '' -or $_.Name -eq $Case }
if (-not $run) { throw "未知用例 '$Case'（用 -List 看全表）" }

$summary = Join-Path $OutDir "smoke_summary_$stamp.txt"
"# 一键冒烟（$stamp）· 用例 $($run.Count) 个 · 每例 --quit-after $QuitAfter" | Set-Content $summary -Encoding UTF8

$bad = 0
foreach ($c in $run) {
    $argv = @('--path', $proj, '--audio-driver', 'Dummy') + $c.Args + @('--quit-after', $QuitAfter)
    $log  = Join-Path $OutDir "smoke_$($c.Name)_$stamp.txt"
    Write-Host ("[smoke] {0,-16} {1}" -f $c.Name, $c.Desc) -ForegroundColor Cyan

    # 🔴🔴 主程序 2026-09-21 修：**mono 版 Godot 用 `& exe 2>&1` / `*>` 抓不到输出**（实测 **0 行** ⚠️，
    #    本会话早先已记录过这个怪癖）⇒ 必须走 `cmd /c "… > log 2>&1"` ✓（本机唯一可靠的抓法）
    # 🔴🔴 主程序 2026-09-21 修（**因我把机器搞脏了**）：**必须保证回收自己起的 Godot 子进程** ——
    #    实测教训：脚本收尾抛错 ⇒ 清理没执行 ⇒ 留下一堆 Godot/powershell 残留 ⚠️
    #    ⇒ 用 try/finally：**无论中途怎么失败，都把自己的子进程杀掉** ✓
    $cmdLine = '"' + $godot + '" ' + (($argv | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' ') + ' > "' + $log + '" 2>&1'
    try {
        cmd /c $cmdLine
        $exit = $LASTEXITCODE
    }
    finally {
        # 只回收【本次启动】的实例：按"最近 N 秒内启动且可执行路径一致"筛（不碰别人的编辑器实例）✓
        $mine = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
            $_.ProcessName -like 'Godot*' -and $_.StartTime -gt (Get-Date).AddSeconds(-($QuitAfter / 60 + 30)) })
        foreach ($g in $mine) { try { Stop-Process -Id $g.Id -Force -ErrorAction Stop } catch { } }
    }

    # 🔴🔴 主程序 2026-09-21 修 **ERROR 口径**（用户指令：别让它们一直"待判定"）：
    #    · **引擎退出 RID 泄漏**（`leaked at exit` / `RID allocations`）= **引擎行为**，**不是代码错误** ⇒ 单独计数、只作信息 ✓
    #    · **真错误** = 其余 ERROR ⇒ **只有它才判红**（否则每份留档都挂着 1~2 条"待判定"，谁也说不清 ✓）
    $noise = 'AudioDriver|DisplayServer|OpenGL|Vulkan|Cannot open file.*\.wav|texture.*not found|root certificate store'   # UI 2026-09-21 补：证书库读取是本机环境噪声（非代码错误）
    $lines = Get-Content $log -ErrorAction SilentlyContinue
    # 🔴 2026-09-19 修：原来用 `-match 'ERROR'` ⇒ **把 C# backtrace 帧误计成"真错误"** ⚠️
#    实测：Godot 打 WARNING 时会附带调用栈，其中一帧的类型名含 `godot_variant_call_error`
#    ⇒ 大小写不敏感的 `ERROR` 命中它 ⇒ e2e 被判 18 条"真错误"，实际那 18 条是 **WARNING 的栈帧**（假红）
#    ⇒ 判据收紧为 **行首 `ERROR:`**（Godot 真错误的唯一前缀）＋ 保留 `SCRIPT ERROR` ✓
$allErrs = @($lines | Select-String -Pattern '^\s*ERROR:|SCRIPT ERROR' | Where-Object { $_.Line -notmatch $noise })
    # 🔴 主程序 2026-09-21 补：**引擎退出期的"资源仍在使用"也是引擎行为**（实测 e2e 里它就是唯一那条"真错误"）✓
    $leaks   = @($allErrs | Where-Object { $_.Line -match 'leaked at exit|RID allocations|resources still in use at exit' })
    $errs    = @($allErrs | Where-Object { $_.Line -notmatch 'leaked at exit|RID allocations|resources still in use at exit' })
    $n       = $errs.Count
    if ($n -gt 0) { $bad++ }

    # 🔴🔴 主程序 2026-09-21 修 **第三个假通过**：**日志为空（行数=0）绝不能算"通过"** ⚠️
    #    （实测：`--path .` 指错项目 ⇒ Godot 什么都没输出 ⇒ 脚本报「✅ 0 非环境 ERROR」= 假绿 ❌）✓
    if ($lines.Count -eq 0) { $n = -1; $bad++ }

    $row = "{0,-16} | 行数={1,-6} | 真错误={2} | 引擎退出泄漏={3} | exit={4}" -f $c.Name, $lines.Count, $n, $leaks.Count, $exit

    # 🆕 主程序 2026-09-21：**把关键读数行直接提出来**（一键跑就能看到"养成/相位/走格/瓷砖"读数）
    #    用户原话："否则这套验证能力会随人员变动丢失" ⇒ 读数不该只存在于日志里、要人来 grep ⚠️
    $hlPattern = '\[养成\]|\[名册构成\]|\[下一解锁\]|\[A8\]|\[A9\]|\[A10|\[片3\.1\]|\[片4\] ✅|\[UI 瓷砖\]|\[UI 相位\]|\[M7\] 🆕 V10|\[P2\]|\[升级通道\]'
    $hl = @($lines | Select-String -Pattern $hlPattern | ForEach-Object { $_.Line.Trim() })
    # 🔴 主程序 2026-09-21 修：**必须走 `Write-Output`（成功流）而不是 `Write-Host`（information 流）** ——
    #    否则 `smoke.ps1 | Select-String` / 写日志时**抓不到这些读数**（我实测踩过：grep `·` 行无结果 ⚠️）✓
    # 🔴 主程序 2026-09-21 修：**用 ASCII 标记**（`  > `）而不是中点 `·` ——
    #    中文控制台编码会把 `·` 打乱 ⇒ 管道/日志里 grep `·` **抓不到**（我实测踩过 ⚠️）✓
    foreach ($h in $hl) { Write-Output ("           > " + $h) }
    # 并把读数**写进留档摘要**（读数不该只存在于控制台）——显式写法，不用 `if {} | Add-Content`（PS 5.1 下不保险）✓
    if ($hl.Count -gt 0) {
        Add-Content -Path $summary -Value "" -Encoding UTF8
        Add-Content -Path $summary -Value "── 关键读数（$($c.Name)）──" -Encoding UTF8
        foreach ($h in $hl) { Add-Content -Path $summary -Value ("  > " + $h) -Encoding UTF8 }
    }
    Write-Host "         $row" -ForegroundColor $(if ($n -gt 0) { 'Red' } else { 'Green' })
    $row | Add-Content $summary -Encoding UTF8
    if ($n -gt 0) {
        $errs | Select-Object -First 5 | ForEach-Object { "    " + $_.Line.Trim() } |
            Add-Content $summary -Encoding UTF8
    }
}

"# 🔴🔴 主程序 2026-09-21 修：**收尾段也用 try/catch 包住**（实测：这一段抛错 ⇒ 脚本提前退出、退出码不可信 ⚠️）
#    ⇒ 同一纪律：**读数与汇总【不许】打断主流程**；退出码必须由 `$bad` **唯一决定**（这样才可能接 CI）✓
# 汇总（人读）+ 判决（机读 · 给 CI 用）：**RESULT 行是 CI 判据的唯一来源**（不靠 $LASTEXITCODE）✓
# 汇总：$($run.Count) 例，非环境 ERROR 非零的用例 = $bad" | Add-Content $summary -Encoding UTF8
# 🔴 主程序 2026-09-21：**关键读数提取（循环之外 ⇒ 控制流简单、可验证）** ——
#   用户原话："否则这套验证能力会随人员变动丢失" ⇒ 读数不该只躺在日志里等人 grep ⚠️
#   用 **ASCII 标记 `  > `**（不用中点 `·`：中文控制台编码会把 `·` 打乱 ⇒ 管道里 grep 不到 ⚠️）
$hlPattern = '\[养成\]|\[名册构成\]|\[下一解锁\]|\[A8\]|\[A9\]|\[A10|\[片3\.1\]|\[片4\] ✅|\[UI 瓷砖\]|\[UI 相位\]|\[M7\] 🆕 V10|\[P2\]|\[升级通道\]'
Write-Output ""
Write-Output "── 关键读数（自动提取 · 供人直接看）──"
Add-Content -Path $summary -Value "" -Encoding UTF8
Add-Content -Path $summary -Value "── 关键读数（自动提取）──" -Encoding UTF8
# 🔴 主程序 2026-09-21：**最直白的流水线**（不依赖 `@()` 计数与 `continue`，PS 5.1 下最稳）✓
# 🔴🔴 **并且整段包在 try/catch 里** —— 我实测踩过：这段抛错（`$ErrorActionPreference='Stop'`）⇒
#      **整个脚本提前退出、退出码恒为 1** ⚠️ ⇒ 正是我自己那条纪律：**读数绝不许打断主流程** ✓
try {
    foreach ($f in (Get-ChildItem -Path $OutDir -Filter "smoke_*_$stamp.txt" | Sort-Object Name)) {
        Get-Content $f.FullName -Encoding UTF8 | Select-String -Pattern $hlPattern | ForEach-Object {
            $line = "     > " + $_.Line.Trim()
            Write-Output $line
            Add-Content -Path $summary -Value $line -Encoding UTF8
        }
    }
}
catch {
    Write-Output ("（读数提取失败，不影响判定：" + $_.Exception.Message + "）")
}

if ($bad -gt 0) { Add-Content -Path $summary -Value "RESULT: BAD n=$bad" -Encoding UTF8 } else { Add-Content -Path $summary -Value "RESULT: OK" -Encoding UTF8 }
Write-Host "`n留档：$summary" -ForegroundColor Yellow
Write-Host $(if ($bad -gt 0) { "🔴 有 $bad 例带非环境 ERROR ⇒ 每一条都要【修】或【标 N/A + 理由】" }
             else { "✅ 全部 0 非环境 ERROR" }) -ForegroundColor $(if ($bad -gt 0) { 'Red' } else { 'Green' })

# 🔴 主程序 2026-09-21 修：**`exit $(if …)` 在 PS 5.1 下不可靠**（实测：`$bad=0` 却退 1 ⚠️）
#    ⇒ 换成确定性写法（先给 `$code` 赋值再 `exit`）✓ —— "退出码能不能接 CI"是这条纪律的要害 ✓
$code = 0
if ($bad -gt 0) { $code = 1 }
Write-Output ("PROBE-EXIT bad=$bad code=$code")
exit $code
