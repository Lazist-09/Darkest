# 🔴 UI 命名空间门禁（防再生）—— 规范名 = `Darkest.UI`（大写 I · 用户 2026-09-21 拍板）
# 用法：pwsh -File tools/dsh/check_ui_namespace.ps1   （CI / 提交前跑；命中即退出码 1）
#
# 🔴 本脚本修掉原版的两个真缺陷（架构 2026-09-21）：
#   ① **无 BOM** ⇒ 🔴 Windows PowerShell 5.1 按 ANSI 解码本文件 ⇒ **中文乱码、语法直接断** ⚠️
#      ⇒ 本文件必须保存为 **UTF-8 with BOM**（**与 `.tres` 必须不带 BOM 正相反 —— BOM 的取舍取决于【读者】**）
#   ② 🔴 **判据不区分大小写** ⇒ 默认 `Select-String` 是【不敏感】的 ⇒ `'Darkest.Ui.'` 会匹配到
#      **合法的 `Darkest.UI.`** ⇒ **门禁对正确代码报错（假阳性）** ⚠️ ⇒ 本脚本一律显式 `-CaseSensitive` ✓

#   ③ 🔴 **我自己又踩了一个：`Select-String` 在 PS 5.1 里【没有 `-Recurse`】** ⇒
#      参数绑定报错（**非终止**）⇒ **脚本仍打印 ✅ 且 exit 0 = 假绿** ⚠️
#      ⇒ 正解：**用 `Get-ChildItem -Recurse | Select-String`** ✓（见下）
#      📌 教训：**门禁必须做【负向自检】**（注入一个违规样本 ⇒ 它必须红）—— 否则"绿"可能只是"没跑到" ✓

$ErrorActionPreference = 'Stop'   # 🔴 关键：让参数/命令错误**终止**，不许"报错但继续 → 假绿"

$uiDir  = Join-Path $PSScriptRoot '..\..\darkest\scripts\ui'
$allDir = Join-Path $PSScriptRoot '..\..\darkest\scripts'

# 判据 ①：小写 namespace 声明（规范名是大写 UI）
$badDecl = @(Get-ChildItem -Path $uiDir -Filter *.cs -Recurse -File |
             Select-String -Pattern 'namespace Darkest.Ui' -SimpleMatch -CaseSensitive)

# 判据 ②：小写限定引用 / 提及（**含注释与反引号里的裸名** —— 注释里留旧名同样误导人）
#   ⚠️ 模式**不带尾点**：`Darkest.Ui.` 与 `` `Darkest.Ui` `` 都要抓（带点写法会漏后者 ⇒ 我的第 4 个漏网）
#   ✅ 安全性：`-CaseSensitive` 下 `Darkest.UI` **不会**匹配 `Darkest.Ui`（末字符 `I` ≠ `i`）✓
$badRef = @(Get-ChildItem -Path $allDir -Filter *.cs -Recurse -File |
            Select-String -Pattern 'Darkest.Ui' -SimpleMatch -CaseSensitive)

$bad = @($badDecl) + @($badRef)
if ($bad.Count -gt 0) {
    Write-Output '🔴 发现 Darkest.Ui（小写 i）—— 规范名是 Darkest.UI：'
    $bad | ForEach-Object { "   $($_.Filename):$($_.LineNumber)" }
    exit 1
}

Write-Output '✅ UI 命名空间统一：小写声明与限定引用均为 0'
exit 0
