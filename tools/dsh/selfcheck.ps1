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

Run-Step '1/4 kernel stays Godot-free (check_godot_refs.py)' {
    python tools/check_godot_refs.py
}

Run-Step '2/4 number discipline (check_data_discipline.py)' {
    python tools/check_data_discipline.py --numbers
}

Run-Step '3/4 UI namespace unified (check_ui_namespace.ps1)' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/check_ui_namespace.ps1
}

Run-Step '4/4 CI gate script works (smoke_gate.ps1 -SelfTest)' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke_gate.ps1 -SelfTest
}

Write-Output ""
if ($failed -gt 0) {
    Write-Output ("[selfcheck] RESULT: FAIL (" + $failed + " of 4 checks failed)")
    exit 1
}

Write-Output "[selfcheck] RESULT: OK (all 4 checks passed)"
exit 0
