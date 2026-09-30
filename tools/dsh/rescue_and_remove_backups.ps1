# rescue_and_remove_backups.ps1  (ASCII-only on purpose: a no-BOM CJK .ps1 gets
# parsed as GBK by this shell and dies -- see discipline BP/BX.)
#
# Two steps, in this order, because deleting is irreversible (discipline AC/BC):
#   1) RESCUE: for every backup dir, take the files that differ from THAT backup's
#      own HEAD, hash them, and keep only those whose blob is NOT in the live
#      repo's object store (= the content exists nowhere else). Copy those into
#      $Dest\<backup>\<relpath> and write a manifest.
#   2) VERIFY: re-hash every copied file and compare to the recorded blob sha1.
#   3) REMOVE: only if step 2 is 100% clean, delete the backup dirs.
#
# Commits are not rescued: the earlier audit showed 0 commits unique to backups.
#
# Usage:
#   pwsh -File tools/dsh/rescue_and_remove_backups.ps1 -Remove <dir>[,<dir>...] [-DryRun]

param(
    [string]$Live = 'F:\GithubPro\Darkest',
    [Parameter(Mandatory = $true)][string[]]$Remove,
    [string]$Dest = '',
    [switch]$DryRun
)

$ErrorActionPreference = 'Continue'
if (-not $Dest) { $Dest = Join-Path $Live '.tools\rescue\backup-20260921' }
$manifest = Join-Path $Live 'reports\backup_rescue_manifest_20260926.tsv'

function Get-GitBlobHash([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $header = [System.Text.Encoding]::ASCII.GetBytes("blob $($bytes.Length)`0")
    $all = New-Object byte[] ($header.Length + $bytes.Length)
    [Array]::Copy($header, 0, $all, 0, $header.Length)
    [Array]::Copy($bytes, 0, $all, $header.Length, $bytes.Length)
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try { return (($sha1.ComputeHash($all) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha1.Dispose() }
}

function Test-HashesInRepo([string]$repo, [string[]]$hashes) {
    $tmpIn = Join-Path $env:TEMP ("dsh_h_{0}.txt" -f ([guid]::NewGuid().ToString('N')))
    $tmpOut = "$tmpIn.out"
    Set-Content -Path $tmpIn -Value $hashes -Encoding ascii
    cmd /c "git -C `"$repo`" cat-file --batch-check < `"$tmpIn`" > `"$tmpOut`" 2>nul"
    $res = @{}
    foreach ($line in (Get-Content $tmpOut -Encoding UTF8)) {
        $p = $line -split ' '
        if ($p.Count -ge 2) { $res[$p[0]] = $p[1] }
    }
    Remove-Item $tmpIn, $tmpOut -ErrorAction SilentlyContinue
    return $res
}

$rows = New-Object System.Collections.Generic.List[string]
$totRescued = 0
$totBytes = 0
$bad = @()

foreach ($b in $Remove) {
    $leaf = Split-Path $b -Leaf
    if ($leaf -eq 'Darkest' -or $leaf -like '*Dungeon-Unity*') {
        Write-Output "REFUSE: $b looks like the live repo or the reference project; not touching it."
        $bad += $b
        continue
    }
    if (-not (Test-Path $b)) { Write-Output "SKIP (absent): $b"; continue }

    $isRepo = Test-Path (Join-Path $b '.git')
    $targets = @()
    if ($isRepo) {
        $status = & git -C $b -c core.quotepath=false status --porcelain 2>$null
        foreach ($line in $status) {
            if ($line.Length -lt 4) { continue }
            $p = $line.Substring(3).Trim('"')
            if ($p -match ' -> ') { $p = ($p -split ' -> ')[-1].Trim('"') }
            $targets += $p
        }
    }
    else {
        # not a repo: everything in it is 'content', rescue all of it
        $targets = Get-ChildItem $b -Recurse -File -ErrorAction SilentlyContinue |
                   ForEach-Object { $_.FullName.Substring($b.Length).TrimStart('\') }
    }
    $targets = $targets | Sort-Object -Unique

    $hash = @{}; $size = @{}
    foreach ($p in $targets) {
        $full = Join-Path $b $p
        if (-not (Test-Path $full -PathType Leaf)) { continue }
        if ((Get-Item $full).Length -gt 50MB) { continue }
        $hash[$p] = Get-GitBlobHash $full
        $size[$p] = (Get-Item $full).Length
    }

    $uniq = @($hash.Keys)
    if ($isRepo -and $uniq.Count) {
        $chk = Test-HashesInRepo -repo $Live -hashes @($hash.Values | Sort-Object -Unique)
        $uniq = @($uniq | Where-Object { $chk[$hash[$_]] -eq 'missing' })
    }

    $n = 0; $nb = 0
    foreach ($p in $uniq) {
        $src = Join-Path $b $p
        $dst = Join-Path (Join-Path $Dest $leaf) $p
        $dir = Split-Path $dst -Parent
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Copy-Item $src $dst -Force
        $rows.Add(("{0}`t{1}`t{2}`t{3}" -f $leaf, $p, $size[$p], $hash[$p]))
        $n++; $nb += $size[$p]
    }
    Write-Output ("RESCUED {0,-42} files={1,-4} bytes={2}" -f $leaf, $n, $nb)
    $totRescued += $n; $totBytes += $nb
}

# ---- manifest ----
$hdr = "# backup rescue manifest -- files whose content exists NOWHERE in the live repo" + "`n" +
       "# rescued from: " + ($Remove -join ' , ') + "`n" +
       "# dest: $Dest`n" +
       "# columns: backup`trelpath`tbytes`tgit-blob-sha1"
Set-Content -Path $manifest -Value ($hdr + "`n" + ($rows -join "`n")) -Encoding UTF8
Write-Output "manifest -> $manifest  rows=$($rows.Count)"

# ---- verify the copy ----
$verifyBad = 0
foreach ($r in $rows) {
    $c = $r -split "`t"
    $dst = Join-Path (Join-Path $Dest $c[0]) $c[1]
    if (-not (Test-Path $dst)) { $verifyBad++; Write-Output "VERIFY MISSING: $($c[0])/$($c[1])"; continue }
    if ((Get-GitBlobHash $dst) -ne $c[3]) { $verifyBad++; Write-Output "VERIFY HASH-MISMATCH: $($c[0])/$($c[1])" }
}
Write-Output ("VERIFY: rescued={0} bytes={1} mismatches={2}" -f $totRescued, $totBytes, $verifyBad)

if ($verifyBad -gt 0) { Write-Output 'ABORT: verification failed -- nothing deleted.'; exit 2 }
if ($DryRun) { Write-Output 'DRY-RUN: nothing deleted.'; exit 0 }

# ---- remove ----
foreach ($b in $Remove) {
    $leaf = Split-Path $b -Leaf
    if ($leaf -eq 'Darkest' -or $leaf -like '*Dungeon-Unity*') { continue }
    if (-not (Test-Path $b)) { continue }
    $sz = (Get-ChildItem $b -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    $t0 = Get-Date
    try {
        Remove-Item $b -Recurse -Force -ErrorAction Stop
        Write-Output ("REMOVED {0,-42} freed={1} MB in {2:n1}s" -f $leaf, [math]::Round($sz/1MB,1), ((Get-Date)-$t0).TotalSeconds)
    }
    catch {
        Write-Output ("REMOVE-FAILED {0,-42} {1}" -f $leaf, $_.Exception.Message)
        $script:failed = $true
    }
}
if (-not (Test-Path (Join-Path $Live '.tools\rescue'))) { Write-Output 'WARN: resort dir missing' }
Write-Output 'DONE'
