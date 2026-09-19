# tools/dsh/check_offset_placement.ps1  (v2 - fixed parsing + counters)
# Read-only. Consumes the enriched spec (doc/ui_spec.json v3: fields carry 性质, sections carry 菜单层级).
# Lists, for ROOT-LEVEL placeholder blocks only:
#   A  out-of-viewport
#   B  anchor box smaller than DD size
#   C  DD field nature == offset (should live inside a parent container, not at scene root)
# Root-level means parent is "" or "." (Godot writes "." for the scene root).
# ASCII only. Writes nothing.

param(
    [string]$SpecPath = 'doc\ui_spec.json',
    [int]$ViewW = 1920,
    [int]$ViewH = 1080
)

$spec = Get-Content $SpecPath -Encoding utf8 -Raw | ConvertFrom-Json

# NOTE: this script is ASCII-only on purpose (PS 5.1 mis-parses non-ASCII without BOM).
# The spec's tag properties have Chinese names, so we identify them by VALUE SHAPE instead:
#   nature values  : absolute | offset | scale | offscreen   (plus one non-ascii class for 参数)
#   level values   : L1 | L2 | L3
# A property whose value is one of the known ASCII nature words is the nature tag.
$nature = @{}
$level = @{}
foreach ($s in $spec.sections) {
    $lv = ''
    foreach ($sp in $s.PSObject.Properties) {
        $sv = [string]$sp.Value
        if ($sv -match '^L[123]$') { $lv = $sv }
    }
    $level[[string]$s.section] = $lv

    foreach ($f in $s.fields.PSObject.Properties) {
        $n = ''
        foreach ($fp in $f.Value.PSObject.Properties) {
            $fv = [string]$fp.Value
            if ($fv -match '^(absolute|offset|scale|offscreen)$') { $n = $fv }
            elseif ($fv -match '^[\u4e00-\u9fa5]+$') { $n = 'param' }
        }
        if ($n -ne '') { $nature[([string]$s.section + '|' + $f.Name)] = $n }
    }
}

# key -> list of "section|nature"
$byKey = @{}
foreach ($kk in $nature.Keys) {
    $parts = $kk.Split('|')
    $k = $parts[1]
    if (-not $byKey.ContainsKey($k)) { $byKey[$k] = @() }
    $byKey[$k] += ($parts[0] + '|' + $nature[$kk])
}

$cntNodes = 0; $cntTip = 0; $cntKey = 0; $cntNat = 0; $cntAmbiguous = 0; $cntNoTip = 0
$rows = @()

foreach ($f in (Get-ChildItem darkest\scenes\ui\*.tscn | Sort-Object Name)) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $m = [regex]::Match($lines[$i], '^\[node name="([^"]+)" type="(ColorRect|PanelContainer)"')
        if (-not $m.Success) { continue }
        $node = $m.Groups[1].Value

        $pm = [regex]::Match($lines[$i], 'parent="([^"]*)"')
        $parent = ''
        if ($pm.Success) { $parent = $pm.Groups[1].Value }
        $isRoot = ($parent -eq '' -or $parent -eq '.')

        $al = $null; $at = $null; $ar = $null; $ab = $null; $cw = $null; $ch = $null; $tip = ''
        for ($j = $i + 1; $j -lt [Math]::Min($lines.Count, $i + 22); $j++) {
            if ($lines[$j] -match '^\[node') { break }
            if ($lines[$j] -match '^anchor_left = ([-\d\.]+)') { $al = [double]$Matches[1] }
            if ($lines[$j] -match '^anchor_top = ([-\d\.]+)') { $at = [double]$Matches[1] }
            if ($lines[$j] -match '^anchor_right = ([-\d\.]+)') { $ar = [double]$Matches[1] }
            if ($lines[$j] -match '^anchor_bottom = ([-\d\.]+)') { $ab = [double]$Matches[1] }
            if ($lines[$j] -match '^custom_minimum_size = Vector2\(([\d\.]+), ([\d\.]+)\)') { $cw = [double]$Matches[1]; $ch = [double]$Matches[2] }
            if ($lines[$j] -match '^tooltip_text = "(.*)"') { $tip = $Matches[1] }
        }
        if ($tip -eq '') { $cntNoTip++ ; if ($null -eq $al -and $null -eq $cw) { continue } }

        $cntNodes++
        if ($tip -ne '') { $cntTip++ }

        # --- key extraction, priority order ---
        $sec = ''; $key = ''
        $r = [regex]::Match($tip, '\[([a-z_0-9]+)\]\s+([a-z_0-9_]+)')
        if ($r.Success) { $sec = $r.Groups[1].Value; $key = $r.Groups[2].Value }
        if ($key -eq '') {
            $r = [regex]::Match($tip, 'DD\s+([a-z_0-9]+)\s+\.([a-z_0-9_]+)')
            if ($r.Success) { $sec = $r.Groups[1].Value; $key = $r.Groups[2].Value }
        }
        if ($key -eq '') {
            $r = [regex]::Match($tip, 'DD\s+([a-z_0-9]+)\s+([a-z_0-9_]+)')
            if ($r.Success) {
                $a = $r.Groups[1].Value; $b = $r.Groups[2].Value
                if ($a -match '_|-') { $key = $a; $sec = '' } else { $sec = $a; $key = $b }
            }
        }
        if ($key -eq '') {
            $r = [regex]::Match($tip, '[（(]DD\s+([a-z_0-9_]+)')
            if ($r.Success) { $key = $r.Groups[1].Value }
        }
        if ($key -eq '') { continue }
        $cntKey++

        # --- nature lookup ---
        $nat = ''
        if ($sec -ne '' -and $nature.ContainsKey($sec + '|' + $key)) { $nat = $nature[$sec + '|' + $key] }
        if ($nat -eq '' -and $byKey.ContainsKey($key)) {
            $cand = @($byKey[$key] | Select-Object -Unique)
            if ($cand.Count -eq 1) { $nat = $cand[0].Split('|')[1]; $sec = $cand[0].Split('|')[0] }
            elseif ($cand.Count -gt 1) {
                # require one unique nature among candidates
                $ns = @($cand | ForEach-Object { $_.Split('|')[1] } | Select-Object -Unique)
                if ($ns.Count -eq 1) { $nat = $ns[0] }
                else { $cntAmbiguous++ }
            }
        }
        if ($nat -eq '') { continue }
        $cntNat++

        $lv = ''
        if ($level.ContainsKey($sec)) { $lv = $level[$sec] }

        if ($null -eq $al) { $al = 0.0 }
        if ($null -eq $at) { $at = 0.0 }
        if ($null -eq $ar) { $ar = $al }
        if ($null -eq $ab) { $ab = $at }

        $x = [math]::Round($al * $ViewW, 1); $y = [math]::Round($at * $ViewH, 1)
        $w = [math]::Round(($ar - $al) * $ViewW, 1); $h = [math]::Round(($ab - $at) * $ViewH, 1)
        $ew = $w; $eh = $h; $mismatch = $false
        if ($null -ne $cw) {
            if ($cw -gt $w + 1 -or $ch -gt $h + 1) { $mismatch = $true }
            if ($cw -gt $ew) { $ew = $cw }
            if ($ch -gt $eh) { $eh = $ch }
        }
        $outFlag = (($x + $ew -gt $ViewW + 1) -or ($y + $eh -gt $ViewH + 1) -or ($x -lt -1) -or ($y -lt -1))

        $rows += [pscustomobject]@{
            File = $f.Name; Node = $node; Parent = $parent; Root = $isRoot
            Sec = $sec; Key = $key; Nat = $nat; Lv = $lv
            X = $x; Y = $y; W = $w; H = $h; MinW = $cw; MinH = $ch
            Out = $outFlag; Mismatch = $mismatch
        }
    }
}

Write-Output ("spec version=" + $spec.meta.version + "  nature keys=" + $nature.Count)
Write-Output ("counters: nodes=" + $cntNodes + "  with-tooltip=" + $cntTip + "  key-extracted=" + $cntKey + "  nature-resolved=" + $cntNat + "  ambiguous=" + $cntAmbiguous + "  no-tooltip=" + $cntNoTip)
Write-Output ""

$root = @($rows | Where-Object { $_.Root })
$nested = @($rows | Where-Object { -not $_.Root })
Write-Output ("root-level blocks=" + $root.Count + "   nested blocks=" + $nested.Count)
Write-Output ""

Write-Output "--- A. OUT OF VIEWPORT (root-level) ---"
$a = @($root | Where-Object { $_.Out })
if ($a.Count -eq 0) { Write-Output "  (none)" }
foreach ($r in ($a | Sort-Object File, Node)) {
    Write-Output ("  {0,-30} {1,-24} {2,-4} [{3}] {4,-28} x={5} y={6} eff={7}x{8}" -f $r.File, $r.Node, $r.Lv, $r.Sec, $r.Key, $r.X, $r.Y, $r.W, $r.H)
}
Write-Output ""

Write-Output "--- B. ANCHOR BOX < DD SIZE (root-level) ---"
$b = @($root | Where-Object { $_.Mismatch })
if ($b.Count -eq 0) { Write-Output "  (none)" }
foreach ($r in ($b | Sort-Object File, Node)) {
    Write-Output ("  {0,-30} {1,-24} {2,-4} box={3}x{4} min={5}x{6}  [{7}] {8}" -f $r.File, $r.Node, $r.Lv, $r.W, $r.H, $r.MinW, $r.MinH, $r.Sec, $r.Key)
}
Write-Output ""

Write-Output "--- C. OFFSET FIELDS AT SCENE ROOT (must move into a parent container) ---"
$c = @($root | Where-Object { $_.Nat -eq 'offset' })
if ($c.Count -eq 0) { Write-Output "  (none)" }
foreach ($r in ($c | Sort-Object File, Node)) {
    Write-Output ("  {0,-30} {1,-24} {2,-4} [{3}] {4,-28} ({5},{6})" -f $r.File, $r.Node, $r.Lv, $r.Sec, $r.Key, $r.X, $r.Y)
}
Write-Output ""

Write-Output "--- E. NATURE / LEVEL BREAKDOWN (root-level) ---"
$root | Group-Object Nat | Sort-Object Name | ForEach-Object { Write-Output ("  nature " + $_.Name + " = " + $_.Count) }
$root | Group-Object Lv | Sort-Object Name | ForEach-Object { Write-Output ("  level  " + $_.Name + " = " + $_.Count) }
Write-Output ""
Write-Output ("MUST-FIX: A=" + $a.Count + "  B=" + $b.Count + "  C=" + $c.Count)
