# tools/dsh/check_placeholder_geometry.ps1
# Read-only diagnostic for UI placeholder blocks.
# For every placeholder node (ColorRect / PanelContainer) in darkest/scenes/ui/*.tscn:
#   rect   = anchors * viewport (1920x1080)
#   eff    = max(rect, custom_minimum_size)      # Godot: min size wins
#   flags  : OUT        -> effective rect leaves the viewport
#            MISMATCH   -> custom_minimum_size larger than the anchor box
#            NESTED     -> parent is not the scene root (rect is parent-relative, OUT is not conclusive)
#   STATE-DUP  -> another block shares the same rounded anchor position (likely mutually exclusive states)
# ASCII only. No file writes except stdout.

param(
    [int]$ViewW = 1920,
    [int]$ViewH = 1080
)

$rows = @()
$files = Get-ChildItem darkest\scenes\ui\*.tscn | Sort-Object Name

foreach ($f in $files) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    for ($k = 0; $k -lt $lines.Count; $k++) {
        $m = [regex]::Match($lines[$k], '^\[node name="([^"]+)" type="(ColorRect|PanelContainer)"(?: parent="([^"]*)")?')
        if (-not $m.Success) { continue }

        $node = $m.Groups[1].Value
        $type = $m.Groups[2].Value
        $parent = $m.Groups[3].Value
        if ($null -eq $parent) { $parent = '' }

        $al = $null; $at = $null; $ar = $null; $ab = $null
        $cw = $null; $ch = $null
        $tip = ''

        for ($j = $k + 1; $j -lt [Math]::Min($lines.Count, $k + 20); $j++) {
            if ($lines[$j] -match '^\[node') { break }
            if ($lines[$j] -match '^anchor_left = ([-\d\.]+)') { $al = [double]$Matches[1] }
            if ($lines[$j] -match '^anchor_top = ([-\d\.]+)') { $at = [double]$Matches[1] }
            if ($lines[$j] -match '^anchor_right = ([-\d\.]+)') { $ar = [double]$Matches[1] }
            if ($lines[$j] -match '^anchor_bottom = ([-\d\.]+)') { $ab = [double]$Matches[1] }
            if ($lines[$j] -match '^custom_minimum_size = Vector2\(([\d\.]+), ([\d\.]+)\)') {
                $cw = [double]$Matches[1]; $ch = [double]$Matches[2]
            }
            if ($lines[$j] -match '^tooltip_text = "(.*)"') { $tip = $Matches[1] }
        }

        if ($null -eq $al -and $null -eq $cw) { continue }

        if ($null -eq $al) { $al = 0.0 }
        if ($null -eq $at) { $at = 0.0 }
        if ($null -eq $ar) { $ar = $al }
        if ($null -eq $ab) { $ab = $at }

        $x = [math]::Round($al * $ViewW, 1)
        $y = [math]::Round($at * $ViewH, 1)
        $w = [math]::Round(($ar - $al) * $ViewW, 1)
        $h = [math]::Round(($ab - $at) * $ViewH, 1)

        $ew = $w; $eh = $h
        $mismatch = $false
        if ($null -ne $cw) {
            if ($cw -gt $w + 1) { $mismatch = $true }
            if ($ch -gt $h + 1) { $mismatch = $true }
            if ($cw -gt $ew) { $ew = $cw }
            if ($ch -gt $eh) { $eh = $ch }
        }

        $out = ($x + $ew -gt $ViewW + 1) -or ($y + $eh -gt $ViewH + 1) -or ($x -lt -1) -or ($y -lt -1)
        $nested = ($parent -ne '')
        $sec = ''
        $ms = [regex]::Match($tip, 'DD [^ ]*\[([a-z_0-9]+)\]')
        if ($ms.Success) { $sec = $ms.Groups[1].Value }
        if ($sec -eq '') {
            $ms2 = [regex]::Match($tip, 'DD ([a-z_0-9\.]+)')
            if ($ms2.Success) { $sec = $ms2.Groups[1].Value }
        }

        $rows += [pscustomobject]@{
            File = $f.Name; Node = $node; Parent = $parent; Sec = $sec
            X = $x; Y = $y; W = $w; H = $h
            MinW = $cw; MinH = $ch
            EffW = $ew; EffH = $eh
            Out = $out; Mismatch = $mismatch; Nested = $nested
            Key = ("{0}|{1}" -f $x, $y)
        }
    }
}

# state-duplicate detection: same rounded (x,y) used by different DD sections
$dupKeys = @{}
foreach ($r in $rows) {
    if ($r.Sec -eq '') { continue }
    if (-not $dupKeys.ContainsKey($r.Key)) { $dupKeys[$r.Key] = @() }
    $dupKeys[$r.Key] += $r.Sec
}

Write-Output ("== placeholder geometry report ==  viewport " + $ViewW + "x" + $ViewH + "  blocks=" + $rows.Count)
Write-Output ""

Write-Output "--- A. OUT OF VIEWPORT (effective rect leaves the viewport) ---"
$outRows = @($rows | Where-Object { $_.Out })
if ($outRows.Count -eq 0) { Write-Output "  (none)" }
foreach ($r in ($outRows | Sort-Object File, Node)) {
    $nest = if ($r.Nested) { "nested:" + $r.Parent } else { "root" }
    Write-Output ("  {0,-30} {1,-24} x={2,6} y={3,6} w={4,6} h={5,6} eff={6,6}x{7,-6} min={8}x{9}  {10}  {11}" -f `
        $r.File, $r.Node, $r.X, $r.Y, $r.W, $r.H, $r.EffW, $r.EffH, $r.MinW, $r.MinH, $nest, $r.Sec)
}
Write-Output ""

Write-Output "--- B. ANCHOR BOX SMALLER THAN DD SIZE (mismatch) ---"
$misRows = @($rows | Where-Object { $_.Mismatch })
if ($misRows.Count -eq 0) { Write-Output "  (none)" }
foreach ($r in ($misRows | Sort-Object File, Node)) {
    Write-Output ("  {0,-30} {1,-24} box={2}x{3}  min={4}x{5}  {6}" -f $r.File, $r.Node, $r.W, $r.H, $r.MinW, $r.MinH, $r.Sec)
}
Write-Output ""

Write-Output "--- C. SAME POSITION, DIFFERENT DD SECTIONS (likely mutually exclusive states) ---"
$shown = 0
foreach ($key in ($dupKeys.Keys | Sort-Object)) {
    $secs = @($dupKeys[$key] | Select-Object -Unique)
    if ($secs.Count -le 1) { continue }
    $sample = @($rows | Where-Object { $_.Key -eq $key })
    $names = ($sample | ForEach-Object { $_.Node }) -join ','
    Write-Output ("  pos=" + $key + "  sections=" + ($secs -join '/') + "  nodes=" + $names)
    $shown++
}
if ($shown -eq 0) { Write-Output "  (none)" }
Write-Output ""

Write-Output "--- D. SUMMARY ---"
Write-Output ("  blocks=" + $rows.Count + "  out=" + $outRows.Count + "  mismatch=" + $misRows.Count + "  state-dup positions=" + $shown)
Write-Output ("  nested(parent!=root)=" + @($rows | Where-Object { $_.Nested }).Count + "  root-level=" + @($rows | Where-Object { -not $_.Nested }).Count)
