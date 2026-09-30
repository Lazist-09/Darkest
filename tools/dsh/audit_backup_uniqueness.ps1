# 备份目录"可安全删除"取证工具（只读）
#
# 为什么需要它：删一个 3.8 GB 的仓库副本之前，必须先证明【它的内容没有一处是别处没有的】
# （纪律 AC 裁前先量 / 破坏性操作前先量）。
#
# 它做两件事，都对着【活仓库】的 object store 查：
#   ① 提交级：备份里所有 ref 指向的提交，活仓库里有没有？（没有 ⇒ 那份历史只在备份里）
#   ② 工作树级：备份里【相对它自己 HEAD 有改动/未跟踪】的文件，其内容（git blob sha1）
#      活仓库的 object store 里有没有？（没有 ⇒ 那段"在飞"内容只在备份里）
#
# 用法：
#   pwsh -File tools/dsh/audit_backup_uniqueness.ps1 -Live F:\GithubPro\Darkest `
#        -Backup F:\GithubPro\Darkest-backup-20260921_000640 ...
#
# 只读：不写、不删、不 fetch。

param(
    [Parameter(Mandatory = $true)][string]$Live,
    [Parameter(Mandatory = $true)][string[]]$Backup,
    [string]$Out = '',
    [int]$SkipOverMB = 50
)

$ErrorActionPreference = 'Continue'

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
    # 一次 git 调用查全部：cat-file --batch-check 对每个 sha 回一行，缺失的回 "<sha> missing"
    $tmpIn = Join-Path $env:TEMP ("dsh_hashes_{0}.txt" -f ([guid]::NewGuid().ToString('N')))
    $tmpOut = "$tmpIn.out"
    Set-Content -Path $tmpIn -Value $hashes -Encoding ascii
    cmd /c "git -C `"$repo`" cat-file --batch-check < `"$tmpIn`" > `"$tmpOut`" 2>nul"
    $res = @{}
    foreach ($line in (Get-Content $tmpOut -Encoding UTF8)) {
        $p = $line -split ' '
        if ($p.Count -ge 2) { $res[$p[0]] = $p[1] }   # 'missing' 或 'blob'
    }
    Remove-Item $tmpIn, $tmpOut -ErrorAction SilentlyContinue
    return $res
}

$report = New-Object System.Collections.Generic.List[string]
$report.Add("# 备份目录可删性取证（只读 · 对照活仓库 object store）")
$report.Add("")
$report.Add("- 活仓库：``$Live``")
$report.Add("- 时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm')")
$report.Add("- 判据：**提交/内容在活仓库 object store 里存在 ⇒ 备份里那份不是唯一来源**")
$report.Add("")

$grandUniqueCommits = 0
$grandUniqueFiles = 0

foreach ($b in $Backup) {
    $name = Split-Path $b -Leaf
    $report.Add("## $name")
    $report.Add("")

    if (-not (Test-Path (Join-Path $b '.git'))) {
        $files = Get-ChildItem $b -Recurse -File -ErrorAction SilentlyContinue
        $sum = ($files | Measure-Object Length -Sum).Sum
        $report.Add("- 无 ``.git`` ⇒ 不是仓库副本；$($files.Count) 个文件 / $([math]::Round($sum/1MB,1)) MB")
        $report.Add("- 🔴 **无 git 溯源 ⇒ 内容是否别处有，本工具无法证明** ⇒ 删前需人工确认（或整包保留）")
        $report.Add("")
        continue
    }

    # ① 提交级
    $refs = & git -C $b for-each-ref --format='%(objectname)' 2>$null | Sort-Object -Unique
    $refCheck = Test-HashesInRepo -repo $Live -hashes $refs
    $refMiss = @($refs | Where-Object { $refCheck[$_] -eq 'missing' })
    $head = (& git -C $b rev-parse HEAD 2>$null)
    $report.Add("- refs 指向的提交：**$($refs.Count)** 个 · 活仓库里**缺 $($refMiss.Count)** 个" + $(if ($refMiss.Count) { " ⇒ 🔴 " + ($refMiss -join ', ') } else { " ✅" }))
    $report.Add("- HEAD = ``$($head.Substring(0,8))``")

    # ② 工作树级：相对它自己 HEAD 有改动 / 未跟踪的文件（尊重 .gitignore）
    $status = & git -C $b -c core.quotepath=false status --porcelain 2>$null
    $paths = @()
    foreach ($line in $status) {
        if ($line.Length -lt 4) { continue }
        $p = $line.Substring(3).Trim('"')
        if ($p -match ' -> ') { $p = ($p -split ' -> ')[-1].Trim('"') }   # rename: 取新名
        $paths += $p
    }
    $paths = $paths | Sort-Object -Unique

    $hashOf = @{}; $sizeOf = @{}; $skipped = @()
    foreach ($p in $paths) {
        $full = Join-Path $b $p
        if (-not (Test-Path $full -PathType Leaf)) { continue }
        $len = (Get-Item $full).Length
        if ($len -gt ($SkipOverMB * 1MB)) { $skipped += "$p ($([math]::Round($len/1MB,1)) MB)"; continue }
        $hashOf[$p] = Get-GitBlobHash $full
        $sizeOf[$p] = $len
    }
    $check = Test-HashesInRepo -repo $Live -hashes @($hashOf.Values | Sort-Object -Unique)
    $unique = @($hashOf.Keys | Where-Object { $check[$hashOf[$_]] -eq 'missing' })
    $uniqBytes = ($unique | ForEach-Object { $sizeOf[$_] } | Measure-Object -Sum).Sum
    if (-not $uniqBytes) { $uniqBytes = 0 }

    $report.Add("- 相对自身 HEAD 有改动/未跟踪：**$($paths.Count)** 个文件（>${SkipOverMB}MB 跳过 $($skipped.Count) 个）")
    if ($unique.Count -eq 0) {
        $report.Add("- ✅ **其中内容在活仓库里【找不到的】：0 个** ⇒ 这份备份的内容【不是唯一来源】")
    } else {
        $report.Add("- 🔴 **内容只在备份里的：$($unique.Count) 个 / $([math]::Round($uniqBytes/1KB,1)) KB** ⇒ 删前必须先救出来：")
        foreach ($u in $unique) { $report.Add("  - ``$u``  ($([math]::Round($sizeOf[$u]/1KB,1)) KB)") }
    }
    if ($skipped.Count) { $report.Add("- ⚠️ 跳过大文件（未判）：$(($skipped -join ' · '))") }
    $report.Add("")

    $grandUniqueCommits += $refMiss.Count
    $grandUniqueFiles += $unique.Count
}

$report.Add("## 合计")
$report.Add("")
$report.Add("- 只在备份里的提交：**$grandUniqueCommits** 个")
$report.Add("- 只在备份里的文件内容：**$grandUniqueFiles** 个")

if ($Out) {
    Set-Content -Path $Out -Value $report -Encoding UTF8
    Write-Output "report -> $Out"
}
$report | ForEach-Object { Write-Output $_ }
