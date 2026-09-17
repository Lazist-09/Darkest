# UI namespace gate (regeneration guard) -- 2026-09-21
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/check_ui_namespace.ps1
#         (also works in PowerShell 7 / pwsh). ASCII-only output so PS 5.1 can parse it.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$uiDir = Join-Path $root 'darkest/scripts/ui'
$bad = Select-String -Path (Join-Path $uiDir '*.cs') -Pattern 'namespace Darkest.Ui;' -SimpleMatch -CaseSensitive
if ($bad) {
  Write-Output 'FAIL: found namespace Darkest.Ui; (must be Darkest.UI)'
  $bad | ForEach-Object { Write-Output ('   {0}:{1}' -f $_.Filename, $_.LineNumber) }
  exit 1
}
$bad2 = Select-String -Path (Join-Path $root 'darkest/scripts/*.cs'), (Join-Path $root 'darkest/scripts/*/*.cs'), (Join-Path $root 'darkest/scripts/*/*/*.cs') -Pattern 'Darkest.Ui.' -SimpleMatch -CaseSensitive -ErrorAction SilentlyContinue
if ($bad2) {
  Write-Output 'FAIL: found qualified reference Darkest.Ui.'
  $bad2 | ForEach-Object { Write-Output ('   {0}:{1}' -f $_.Filename, $_.LineNumber) }
  exit 1
}
Write-Output 'OK: UI namespace unified as Darkest.UI'
exit 0