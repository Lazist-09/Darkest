# tools/dsh/check_placeholders.ps1
# HARD GATE wrapper: placeholder geometry must be clean.
# Runs check_offset_placement.ps1 and fails (exit 1) unless:
#   A (out of viewport, root-level) = 0
#   B (anchor box < DD size, root-level) = 0
#   C (offset field anchored at scene root) = 0
# ASCII only. Read-only.

param(
    [switch]$Verbose2
)

$inner = Join-Path $PSScriptRoot 'check_offset_placement.ps1'
if (-not (Test-Path $inner)) {
    Write-Output "[check_placeholders] FAIL: inner checker not found: $inner"
    exit 2
}

$out = & powershell -NoProfile -ExecutionPolicy Bypass -File $inner 2>&1
$text = ($out | Out-String)

$line = ($out | Select-String -Pattern 'MUST-FIX' | Select-Object -First 1)
if (-not $line) {
    Write-Output "[check_placeholders] FAIL: MUST-FIX line not found (checker did not run?)"
    Write-Output $text
    exit 2
}

$m = [regex]::Match($line.Line, 'A=(\d+)\s+B=(\d+)\s+C=(\d+)')
if (-not $m.Success) {
    Write-Output "[check_placeholders] FAIL: cannot parse MUST-FIX line: " + $line.Line
    exit 2
}

$a = [int]$m.Groups[1].Value
$b = [int]$m.Groups[2].Value
$c = [int]$m.Groups[3].Value

if ($Verbose2) { Write-Output $text }

Write-Output ("[check_placeholders] out-of-viewport=" + $a + "  size-mismatch=" + $b + "  offset-at-root=" + $c)

if ($a -eq 0 -and $b -eq 0 -and $c -eq 0) {
    Write-Output "[check_placeholders] PASS (all three criteria are zero)"
    exit 0
}

Write-Output "[check_placeholders] FAIL (must be 0/0/0)"
# print only the offending sections
$show = $false
foreach ($l in $out) {
    $s = [string]$l
    if ($s -match '^--- [ABC]\.') { $show = $true; Write-Output $s; continue }
    if ($s -match '^--- ') { $show = $false; continue }
    if ($show -and $s.Trim() -ne '' -and $s -notmatch '\(none\)') { Write-Output $s }
}
exit 1
