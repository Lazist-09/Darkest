# tools/dsh/reports_hygiene.ps1 -- keep reports/ findable (no Godot needed).
#
# WHY (planner #405, external review):
#   reports/ had grown into a raw dump graveyard: 96 .txt files, no "latest" pointer,
#   no README, and near-identical rounds (13:09 vs 13:12) sitting side by side.
#   Review quote: "no latest pointer and no cleanup mechanism -- in three months nobody
#   will be able to dig through it".
#
# WHAT THIS DOES (three things, all reversible):
#   1) reports/latest/  -> a copy of the MOST RECENT round (always "this round")
#   2) prune: for each <prefix>, keep only the newest file per prefix+case pair
#      (old rounds are moved under reports/archive/<stamp>/ instead of deleted)
#   3) it never touches non-report files and never deletes anything outright
#
# ASCII-ONLY ON PURPOSE (a .ps1 with non-ASCII needs a BOM or PS 5.1 breaks on it).
#
# USAGE
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/reports_hygiene.ps1
#   powershell ... -File tools/dsh/reports_hygiene.ps1 -KeepPerPrefix 2
#   powershell ... -File tools/dsh/reports_hygiene.ps1 -DryRun

param(
    [int]$KeepPerPrefix = 1,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$reports = 'reports'
if (-not (Test-Path $reports)) {
    Write-Output "[hygiene] no reports/ directory -- nothing to do"
    exit 0
}

$archive = Join-Path $reports 'archive'
$latest = Join-Path $reports 'latest'

# ---- 1) pick the newest round --------------------------------------------------
# A "round" is identified by the stamp suffix shared by all files written together.
$files = @(Get-ChildItem -Path $reports -File -Filter *.txt | Sort-Object LastWriteTime -Descending)
if ($files.Count -eq 0) {
    Write-Output "[hygiene] reports/ has no .txt files -- nothing to do"
    exit 0
}

# 🔴 pick the newest round by the STAMP IN THE NAME, not by mtime:
#   mtime lies (a `git checkout` of an old file makes it look newest -- I hit exactly that).
$stamps = @($files | ForEach-Object {
    if ($_.BaseName -match '(\d{8}_\d{4})$') { $Matches[1] } else { '' }
} | Where-Object { $_ -ne '' } | Sort-Object -Unique)
$stamp = if ($stamps.Count -gt 0) { $stamps[-1] } else { '' }
$round = @($files | Where-Object { $_.BaseName -like "*$stamp*" })
Write-Output ("[hygiene] newest round = " + $stamp + " (" + $round.Count + " files)")

# ---- 2) latest/ points at this round -------------------------------------------
if (-not $DryRun) {
    if (Test-Path $latest) { Remove-Item $latest -Recurse -Force }
    New-Item -ItemType Directory -Path $latest | Out-Null
    foreach ($f in $round) { Copy-Item $f.FullName -Destination $latest -Force }
    # a human-readable pointer too (so `cat reports/LATEST.md` answers "which file is authoritative")
    $pointer = @(
        "# LATEST -- this round's smoke output",
        "",
        ("stamp: " + $stamp),
        ("files: " + $round.Count),
        "",
        $(if ($round | Where-Object { $_.Name -like "*_summary_*" }) { "authoritative verdict: smoke_summary_$stamp.txt (its RESULT line is the CI gate input)" } else { "note: this round has no smoke summary (e.g. a ui_sweep round) -- the CI gate input is the newest smoke_summary_*.txt" }),
        "",
        "see reports/README.md for naming and retention rules"
    ) -join "`n"
    Set-Content -Path (Join-Path $reports 'LATEST.md') -Value $pointer -Encoding UTF8
}
Write-Output ("[hygiene] latest/ -> " + $stamp)

# ---- 3) prune: keep the newest KeepPerPrefix per prefix+stamp ------------------
# 🔴 NEVER archive the authoritative summaries (*_summary_*) -- they are the CI gate input
#   and the only reports/*.txt that are tracked in git (see .gitignore). I archived one by
#   accident the first time; this line is the fix.
$pruneable = @($files | Where-Object { $_.BaseName -notlike '*_summary_*' })
$groups = $pruneable | Group-Object { ($_.BaseName -replace '_\d{8}_\d{4}$', '') }
$moved = 0
foreach ($g in $groups) {
    $sorted = @($g.Group | Sort-Object LastWriteTime -Descending)
    $keep = $sorted | Select-Object -First $KeepPerPrefix
    $drop = $sorted | Select-Object -Skip $KeepPerPrefix
    foreach ($d in $drop) {
        if ($DryRun) {
            Write-Output ("[hygiene][dry] would archive " + $d.Name)
            continue
        }

        $sub = Join-Path $archive $stamp
        if (-not (Test-Path $sub)) { New-Item -ItemType Directory -Path $sub -Force | Out-Null }
        Move-Item $d.FullName -Destination $sub -Force
        $moved++
    }
}

Write-Output ("[hygiene] archived " + $moved + " older file(s) under reports/archive/" + $stamp)
Write-Output "[hygiene] done"
exit 0
