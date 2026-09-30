# Inbox watchdog -- poll ONE role's INBOX; exit (and print) as soon as it is NON-EMPTY.
#
# FIX 1 (2026-09-26, defect found the hard way):
#   This script used to watch U+7B56 U+5212 == the DESIGNER window, i.e. the file the architect
#   WRITES to.  So it (a) fired instantly on the architect's OWN delivery and (b) would never fire
#   on mail addressed TO the architect.  The watchdog was pointed at the outbox, not the inbox.
#   => Rule: the watcher must target the file OTHER PEOPLE append to.
#   => Target default is U+67B6 U+6784 (architect window).  Codepoints, never literals (see below).
#
# FIX 2 (2026-09-26, same shape one level up -- found by the designer):
#   Fix 1 hard-coded the ARCHITECT inbox, so when the DESIGNER ran this script with no arguments it
#   watched the ARCHITECT's inbox: it fired on the designer's own outbound mail and never fired on
#   mail addressed TO the designer (5 unread letters piled up behind a "green" watchdog).
#   => Rule: an inbox watcher must be told WHOSE inbox; "someone else's target" is still the wrong
#      target.  A guard that watches the wrong thing reports OK forever.
#   => Fix: -Role selects the inbox (additive; default 'architect' == previous behaviour).
#   => Callers: designer runs  -Role designer   (U+7B56 U+5212 window).
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
    [int]$MaxHours  = 12,
    [ValidateSet('architect','designer','lead','ui')]
    [string]$Role   = 'architect',
    # Read-watermark: SHA256 of the content this role has ALREADY read.
    # Empty => fire on any non-empty content (previous behaviour).
    # WHY: since 2026-09-26 the inbox rule is "delete only COMPLETED items at task end",
    #      so a non-empty inbox is NORMAL (pending items stay as a to-do list).
    #      Firing on "non-empty" would then be a FALSE WAKE every single time.
    #      => we must fire on "content CHANGED since I last read it", not on "non-empty".
    [string]$SeenHash = ''
)

# --- role -> window-name codepoints (NEVER literals: see encoding note above) ---
#   architect = U+67B6 U+6784   (the architect's inbox -- other roles append here)
#   designer  = U+7B56 U+5212   (the designer's  inbox)
#   lead      = U+4E3B U+7A0B U+5E8F   (lead programmer)
#   ui        = U+0055 U+0049 U+8BBE U+8BA1 U+5E08   (UI designer)
$roleMap = @{
    'architect' = @(0x67B6, 0x6784)
    'designer'  = @(0x7B56, 0x5212)
    'lead'      = @(0x4E3B, 0x7A0B, 0x5E8F)
    'ui'        = @(0x0055, 0x0049, 0x8BBE, 0x8BA1, 0x5E08)
}

# --- build  F:\GithubPro\Darkest\doc\windows\<Role window>.txt  without literals ---
$dir  = 'F:\GithubPro\Darkest\doc\windows'
$name = (($roleMap[$Role] | ForEach-Object { [char]$_ }) -join '') + [char]0x7A97 + [char]0x53E3 + '.txt'
$inbox = Join-Path $dir $name

$enc      = New-Object System.Text.UTF8Encoding($false)
$deadline = (Get-Date).AddHours($MaxHours)

Write-Output "[watchdog] role=$Role  start $(Get-Date -Format 'HH:mm:ss')  pid=$PID"
Write-Output "[watchdog] target exists: $(Test-Path $inbox)"
$all = @(Get-ChildItem $dir -Filter *.txt -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
foreach ($n in $all) {
    $cp = ($n.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' '
    Write-Output "[watchdog]   window file: $cp"
}
Write-Output "[watchdog] interval=${IntervalS}s cap=${MaxHours}h"
if ($SeenHash -ne '') {
    Write-Output "[watchdog] watermark mode: fire only when content CHANGES (seen=$($SeenHash.Substring(0,[Math]::Min(12,$SeenHash.Length)))...)"
} else {
    Write-Output "[watchdog] watermark mode: OFF (fire on any non-empty content)"
}

function Get-Sha256([string]$s) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $enc.GetBytes($s)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally { $sha.Dispose() }
}

while ((Get-Date) -lt $deadline) {
    if (Test-Path $inbox) {
        $txt = ''
        try { $txt = [System.IO.File]::ReadAllText($inbox, $enc) } catch { $txt = '' }
        $nonEmpty = $txt.Trim().Length -gt 0
        $changed  = $true
        if ($SeenHash -ne '' -and $nonEmpty) {
            $changed = ((Get-Sha256 $txt) -ne $SeenHash.ToLowerInvariant())
        }
        if ($nonEmpty -and $changed) {
            Write-Output ""
            Write-Output "================================================================"
            Write-Output "INBOX_NONEMPTY  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  chars=$($txt.Length)"
            Write-Output "================================================================"
            Write-Output $txt
            Write-Output "================================================================"
            Write-Output "[watchdog] exit -- role=$Role inbox has UNREAD content, that role must process it"
            exit 0
        }
    } else {
        Write-Output "[watchdog] WARN inbox missing: $inbox"
    }
    Start-Sleep -Seconds $IntervalS
}

Write-Output "[watchdog] WATCH_TIMEOUT after ${MaxHours}h -- relaunch if still needed"
exit 1
