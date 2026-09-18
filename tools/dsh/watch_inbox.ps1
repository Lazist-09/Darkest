# Inbox watchdog -- poll the designer inbox; exit (and print) as soon as it is NON-EMPTY.
#
# WHY (encoding lesson, 2026-09-15):
#   This file is UTF-8 WITHOUT BOM.  Windows PowerShell 5.1 reads .ps1 as ANSI when there is no BOM,
#   so ANY non-ASCII literal in here (e.g. a Chinese path) gets corrupted and Test-Path fails.
#   => Rule: NO non-ASCII literals in this script.  The Chinese folder name is built from codepoints.
#   => Rule: all console output is ASCII (the console decodes as ANSI anyway -> mojibake).
#
# DESIGN: the job EXITS when the inbox becomes non-empty -> the completion notice IS the wake-up.
#         Never loop forever: MaxHours caps it.

param(
    [int]$IntervalS = 15,
    [int]$MaxHours  = 24
)

# --- build  F:\GithubPro\Darkest\doc\windows\<U+7B56 U+5212 U+7A97 U+53E3>.txt  without literals ---
$dir  = 'F:\GithubPro\Darkest\doc\windows'
$name = [string][char]0x7B56 + [char]0x5212 + [char]0x7A97 + [char]0x53E3 + '.txt'   # inbox file
$inbox = Join-Path $dir $name

$enc      = New-Object System.Text.UTF8Encoding($false)
$deadline = (Get-Date).AddHours($MaxHours)

Write-Output "[watchdog] start $(Get-Date -Format 'HH:mm:ss')  pid=$PID"
Write-Output "[watchdog] target exists: $(Test-Path $inbox)"
$all = @(Get-ChildItem $dir -Filter *.txt -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
foreach ($n in $all) {
    $cp = ($n.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' '
    Write-Output "[watchdog]   window file: $cp"
}
Write-Output "[watchdog] interval=${IntervalS}s cap=${MaxHours}h"

while ((Get-Date) -lt $deadline) {
    if (Test-Path $inbox) {
        $txt = ''
        try { $txt = [System.IO.File]::ReadAllText($inbox, $enc) } catch { $txt = '' }
        if ($txt.Trim().Length -gt 0) {
            Write-Output ""
            Write-Output "================================================================"
            Write-Output "INBOX_NONEMPTY  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  chars=$($txt.Length)"
            Write-Output "================================================================"
            Write-Output $txt
            Write-Output "================================================================"
            Write-Output "[watchdog] exit -- inbox non-empty, designer must process it"
            exit 0
        }
    } else {
        Write-Output "[watchdog] WARN inbox missing: $inbox"
    }
    Start-Sleep -Seconds $IntervalS
}

Write-Output "[watchdog] WATCH_TIMEOUT after ${MaxHours}h -- relaunch if still needed"
exit 1
