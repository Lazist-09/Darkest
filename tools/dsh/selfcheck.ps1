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

Run-Step '1/6 kernel stays Godot-free (check_godot_refs.py)' {
    python tools/check_godot_refs.py
}

Run-Step '2/6 number discipline (check_data_discipline.py)' {
    python tools/check_data_discipline.py --numbers
}

Run-Step '3/6 UI namespace unified (check_ui_namespace.ps1)' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/check_ui_namespace.ps1
}

Run-Step '4/6 CI gate script works (smoke_gate.ps1 -SelfTest)' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke_gate.ps1 -SelfTest
}

# ---- 5/5 placeholder compliance (ASCII-only; no Godot needed) -------------------
# WHY: borrowed art (borrow/) and the placeholder heroes must never enter git, the
#   build products, or resources/ (assets_credits.md A1 family). This is a LEGAL
#   invariant, so it belongs in the one-command self check.
Write-Output ""
Write-Output "=== 5/7 placeholder compliance ==="
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
    Write-Output "[selfcheck] FAIL (5/7 placeholder compliance)"
    $failed++
}
else {
    Write-Output "[selfcheck] ok   (5/7 placeholder compliance: not tracked, gitignored, not in resources/)"
}

# ---- 6/6 powershell syntax (ASCII-only; no Godot needed) ------------------------
# WHY: on 2026-09-21 my own edit to smoke.ps1 left an unbalanced brace, so the script
#   did not even parse -- and it stayed silent for several rounds (it LOOKED like an
#   "exit code quirk"). A script that cannot parse must be caught by the self check.
Write-Output ""
Write-Output "=== 6/7 powershell syntax ==="
$parseFail = 0
$psFiles = @(Get-ChildItem tools -Recurse -Filter *.ps1 -ErrorAction SilentlyContinue)
foreach ($s in $psFiles) {
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($s.FullName, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) {
        Write-Output ("[selfcheck] PARSE ERROR in " + $s.FullName + " : " + $errors[0].Message)
        $parseFail = 1
    }
}
if ($parseFail -ne 0) {
    Write-Output "[selfcheck] FAIL (6/7 powershell syntax)"
    $failed++
}
else {
    Write-Output ("[selfcheck] ok   (6/7 powershell syntax: " + $psFiles.Count + " scripts parsed)")
}

# ---- 7/7 solution builds (ASCII-only) -------------------------------------------
# WHY: twice on 2026-09-21 a stray ASCII quote inside a Chinese string literal broke the
#   build, and it was only caught by an ad-hoc "dotnet build" I happened to run.
#   A check that must be run by hand is a check that gets skipped, so it lives here.
Write-Output ""
Write-Output "=== 7/7 solution builds ==="
Push-Location darkest
dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore -m:1 -nodeReuse:false -tl:off -v:q 2>&1 |
    Select-String -Pattern 'error' -CaseSensitive | Select-Object -First 5 | ForEach-Object { Write-Output ("[build] " + $_.Line.Trim()) }
$buildCode = $LASTEXITCODE
Pop-Location
if ($buildCode -ne 0) {
    Write-Output "[selfcheck] FAIL (7/7 build) exit=$buildCode"
    $failed++
}
else {
    Write-Output "[selfcheck] ok   (7/7 build: 0 errors)"
}

Write-Output ""
if ($failed -gt 0) {
    Write-Output ("[selfcheck] RESULT: FAIL (" + $failed + " of 7 checks failed)")
    exit 1
}

Write-Output "[selfcheck] RESULT: OK (all 7 checks passed)"
exit 0
