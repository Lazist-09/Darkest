# tools/dsh/smoke_gate.ps1 -- CI gate for the one-click smoke verdict.
#
# WHY THIS EXISTS (project lesson, 2026-09-21):
#   `smoke.ps1`'s process exit code proved UNRELIABLE under `powershell -File`
#   (it returned 1 even for green cases). So the CI gate must NOT depend on
#   $LASTEXITCODE: it reads the machine-readable verdict line that smoke.ps1
#   writes into its summary file:  "RESULT: OK"  or  "RESULT: BAD n=<N>".
#
# ASCII-ONLY ON PURPOSE: a PowerShell file containing non-ASCII text must be
#   saved as UTF-8 *with BOM*, otherwise Windows PowerShell 5.1 decodes it as
#   ANSI and the parser breaks (we hit that twice: check_ui_namespace.ps1 and
#   smoke.ps1). This script keeps every character ASCII so the BOM question
#   cannot bite it.
#
# USAGE
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke_gate.ps1
#       -> newest reports/smoke_summary_*.txt
#   powershell ... -File tools/dsh/smoke_gate.ps1 -Summary <path>
#   powershell ... -File tools/dsh/smoke_gate.ps1 -SelfTest     # no Godot needed
#
# EXIT CODES: 0 = verdict OK, 1 = verdict BAD / missing verdict / no summary.
param(
    [string]$Summary = '',
    [switch]$SelfTest
)

function Read-Verdict([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    $hit = Select-String -Path $path -Pattern '^RESULT:\s*(OK|BAD\s+n=(\d+))\s*$' -CaseSensitive |
        Select-Object -Last 1
    if ($null -eq $hit) { return $null }
    return $hit.Matches[0].Groups[1].Value.Trim()
}

if ($SelfTest) {
    $tmp = Join-Path $env:TEMP ("gate_selftest_" + [guid]::NewGuid().ToString('N') + ".txt")

    Set-Content -Path $tmp -Value "noise" -Encoding ASCII
    Add-Content -Path $tmp -Value "RESULT: OK" -Encoding ASCII
    $okCase = (Read-Verdict $tmp) -eq 'OK'

    Set-Content -Path $tmp -Value "RESULT: BAD n=2" -Encoding ASCII
    $badCase = (Read-Verdict $tmp) -like 'BAD*'

    Set-Content -Path $tmp -Value "no verdict here" -Encoding ASCII
    $noneCase = $null -eq (Read-Verdict $tmp)

    Remove-Item $tmp -Force

    if ($okCase -and $badCase -and $noneCase) {
        Write-Output "[gate] SELFTEST OK (accepts OK / rejects BAD / flags missing verdict)"
        exit 0
    }

    Write-Output "[gate] SELFTEST FAIL ok=$okCase bad=$badCase missing=$noneCase"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Summary)) {
    $Summary = (Get-ChildItem -Path 'reports' -Filter 'smoke_summary_*.txt' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

if ([string]::IsNullOrWhiteSpace($Summary) -or -not (Test-Path $Summary)) {
    Write-Output "[gate] FAIL: no smoke summary found (run tools/dsh/smoke.ps1 first)"
    exit 1
}

$verdict = Read-Verdict $Summary
if ($null -eq $verdict) {
    Write-Output "[gate] FAIL: no RESULT line in $Summary (summary predates the verdict line; re-run smoke.ps1)"
    exit 1
}

if ($verdict -eq 'OK') {
    Write-Output "[gate] OK   ($Summary)"
    exit 0
}

Write-Output "[gate] FAIL: $verdict   ($Summary)"
exit 1
