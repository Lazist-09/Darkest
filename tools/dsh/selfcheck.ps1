# tools/dsh/selfcheck.ps1 -- one command, four checks, no Godot needed.
#
# ASCII-ONLY ON PURPOSE: a PowerShell file with non-ASCII text must be saved as
#   UTF-8 *with BOM*, or Windows PowerShell 5.1 decodes it as ANSI and the parser
#   breaks (we hit that twice). Keeping every character ASCII removes the risk.
#
# USAGE
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/selfcheck.ps1
#
# EXIT CODE: 0 = all four checks passed, 1 = at least one failed.
# Checks:
#   1) tools/check_godot_refs.py     -- kernel stays Godot-free (+ no System.IO in ui/scene)
#   2) tools/check_data_discipline.py-- number discipline (suspicious literals must be 0)
#   3) tools/dsh/check_ui_namespace.ps1 -- UI namespace unified as Darkest.UI
#   4) tools/dsh/smoke_gate.ps1 -SelfTest -- the CI gate script itself works

$ErrorActionPreference = 'Continue'
$failed = 0

function Run-Step([string]$title, [scriptblock]$body) {
    Write-Output ""
    Write-Output ("=== " + $title + " ===")
    & $body
    $code = $LASTEXITCODE
    if ($null -eq $code) { $code = 0 }
    if ($code -ne 0) {
        Write-Output ("[selfcheck] FAIL (" + $title + ") exit=" + $code)
        $script:failed++
    }
    else {
        Write-Output ("[selfcheck] ok   (" + $title + ")")
    }
}

Run-Step '1/5 kernel stays Godot-free (check_godot_refs.py)' {
    python tools/check_godot_refs.py
}

Run-Step '2/5 number discipline (check_data_discipline.py)' {
    python tools/check_data_discipline.py --numbers
}

Run-Step '3/5 UI namespace unified (check_ui_namespace.ps1)' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/check_ui_namespace.ps1
}

Run-Step '4/5 CI gate script works (smoke_gate.ps1 -SelfTest)' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke_gate.ps1 -SelfTest
}

# ---- 5/5 placeholder compliance (ASCII-only; no Godot needed) -------------------
# WHY: borrowed art (borrow/) and the placeholder heroes must never enter git, the
#   build products, or resources/ (assets_credits.md A1 family). This is a LEGAL
#   invariant, so it belongs in the one-command self check.
Write-Output ""
Write-Output "=== 5/5 placeholder compliance ==="
$compliance = 0
$tracked = @(git ls-files)
if (@($tracked | Select-String -Pattern 'borrow/').Count -gt 0) {
    Write-Output "[selfcheck] VIOLATION: borrowed files are tracked by git"
    $compliance = 1
}
if (@($tracked | Select-String -Pattern 'assets/heroes_placeholder/').Count -gt 0) {
    Write-Output "[selfcheck] VIOLATION: placeholder hero files are tracked by git"
    $compliance = 1
}
$gi = @(Get-Content .gitignore -ErrorAction SilentlyContinue)
if (@($gi | Where-Object { $_ -match '^borrow/' }).Count -eq 0) {
    Write-Output "[selfcheck] VIOLATION: .gitignore has no 'borrow/' rule"
    $compliance = 1
}
if (@($gi | Where-Object { $_ -match 'heroes_placeholder' }).Count -eq 0) {
    Write-Output "[selfcheck] VIOLATION: .gitignore has no 'heroes_placeholder' rule"
    $compliance = 1
}
$leak = @(Get-ChildItem resources -Recurse -File -ErrorAction SilentlyContinue |
    Select-String -Pattern 'placeholder|playwright' -List)
if ($leak.Count -gt 0) {
    Write-Output "[selfcheck] VIOLATION: placeholder/borrowed assets leaked into resources/"
    $compliance = 1
}
if ($compliance -ne 0) {
    Write-Output "[selfcheck] FAIL (5/5 placeholder compliance)"
    $failed++
}
else {
    Write-Output "[selfcheck] ok   (5/5 placeholder compliance: not tracked, gitignored, not in resources/)"
}

Write-Output ""
if ($failed -gt 0) {
    Write-Output ("[selfcheck] RESULT: FAIL (" + $failed + " of 5 checks failed)")
    exit 1
}

Write-Output "[selfcheck] RESULT: OK (all 5 checks passed)"
exit 0
