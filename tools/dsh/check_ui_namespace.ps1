# 🔴 UI 命名空间静态检查（防再生 · 统一为 Darkest.UI）—— 2026-09-21
# 用法：pwsh -File tools/dsh/check_ui_namespace.ps1   （CI/提交前跑；命中即退出码 1）
$bad = Select-String -Path darkest/scripts/ui/*.cs -Pattern 'namespace Darkest.Ui;' -SimpleMatch
if ($bad) { Write-Output "🔴 发现 Darkest.Ui（应统一为 Darkest.UI）："; $bad | ForEach-Object { "   $($_.Filename):$($_.LineNumber)" }; exit 1 }
$bad2 = Select-String -Path darkest/scripts -Recurse -Include *.cs -Pattern 'Darkest.Ui.' -SimpleMatch | Where-Object { $_.Line -notmatch 'Darkest\.Ui\.(X|YYY)' }
if ($bad2) { Write-Output "🔴 仍有 Darkest.Ui. 限定引用："; $bad2 | ForEach-Object { "   $($_.Filename):$($_.LineNumber)" }; exit 1 }
Write-Output "✅ UI 命名空间统一：全部为 Darkest.UI"