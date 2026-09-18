# UI namespace gate (regeneration guard) -- 2026-09-21, revised by architect (v4)
# Canonical name: Darkest.UI  (user ruling 2026-09-21)
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/check_ui_namespace.ps1
#         (PowerShell 5.1 and pwsh 7 both fine.)  ASCII-only output => no BOM hazard.
#
# History of this gate (every state was real; see doc/architecture/README.md RED LINE 20 item 6):
#   v1  no BOM + case-insensitive patterns  -> parse error / false positive
#   v2  ASCII rewrite                       -> pattern had a trailing dot => MISSED backticked
#                                              bare name => FALSE GREEN (exit 0 with 2 residuals)
#   v3  pattern w/o dot + -CaseSensitive    -> correct, but red on EXPLANATORY COMMENTS that
#                                              necessarily mention the old lowercase name
#   v4  (this file) SPLIT code hits from comment hits:
#         * code hit     => FAIL, exit 1        (real violation)
#         * comment-only => WARN, exit 0        (a comment that says "do not use Darkest.Ui"
#                                                must be allowed to write it)
#
# Rule that made v4 necessary (external review, #405):
#   the credibility of a guard depends on whether what it flags is a REAL VIOLATION or a COMMENT.
#   Same family as discipline R (self-reference): a verdict must not treat meta-information
#   about itself (comments / summary count lines) as evidence.
#
# Invariant: any change here MUST be verified in BOTH directions
#   (clean => pass ; injected code violation => fail) plus the comment case (=> warn, exit 0).

$ErrorActionPreference = 'Stop'

$root   = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$uiDir  = Join-Path $root 'darkest/scripts/ui'
$allDir = Join-Path $root 'darkest/scripts'

# A match is a "comment hit" if its line is only a comment, or the match sits after a // on that line.
#
# NOTE (v4b bug found by three-way self-check): with -SimpleMatch, Select-String leaves
#   .Matches EMPTY, so ".Matches[0].Index" is null => cast to 0 => every hit looked like code.
#   Do NOT read .Matches with -SimpleMatch. Compute the offset ourselves; note that
#   .NET String.IndexOf is case-sensitive by default, which is what we want here.
function Test-CommentHit {
    param([string]$Line, [int]$Index)
    $t = $Line.TrimStart()
    if ($t.StartsWith('//') -or $t.StartsWith('/*') -or $t.StartsWith('*')) { return $true }
    $slashes = $Line.IndexOf('//')
    if ($slashes -ge 0 -and $Index -gt $slashes) { return $true }
    return $false
}

# Pattern intentionally has NO trailing dot (v2 bug): it must also catch the backticked bare name.
# Safe because -CaseSensitive: 'Darkest.UI' does not contain 'Darkest.Ui' (last char I != i).
$all = @(Get-ChildItem -Path $allDir -Filter *.cs -Recurse -File |
         Select-String -Pattern 'Darkest.Ui' -SimpleMatch -CaseSensitive)

$codeHits = @(); $commentHits = @()
foreach ($m in $all) {
    $idx = $m.Line.IndexOf('Darkest.Ui')          # .NET IndexOf = case-sensitive (see NOTE above)
    if ($idx -lt 0) { $idx = 0 }
    if (Test-CommentHit -Line $m.Line -Index $idx) { $commentHits += $m } else { $codeHits += $m }
}

if ($codeHits.Count -gt 0) {
    Write-Output 'FAIL: Darkest.Ui (lowercase i) used in CODE; canonical name is Darkest.UI'
    $codeHits | ForEach-Object { Write-Output ('   {0}:{1}' -f $_.Filename, $_.LineNumber) }
    exit 1
}

if ($commentHits.Count -gt 0) {
    Write-Output ('WARN: {0} comment(s) mention the old lowercase name Darkest.Ui (allowed, not fatal):' -f $commentHits.Count)
    $commentHits | ForEach-Object { Write-Output ('   {0}:{1}' -f $_.Filename, $_.LineNumber) }
    Write-Output 'WARN: prefer removing such mentions when they no longer explain anything; code is clean.'
    exit 0
}

Write-Output 'OK: UI namespace unified as Darkest.UI (code clean, no stale mentions)'
exit 0
