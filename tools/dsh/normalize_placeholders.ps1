# tools/dsh/normalize_placeholders.ps1
# Goal: make every placeholder ColorRect use the theme semi-transparent fill.
# Theme source: UiPalette.PlaceholderFill = Color(0.62, 0.60, 0.58, 0.22)  (warm gray, alpha 0.22)
# Idempotent: nodes that already set color = ... are skipped.
# ASCII only on purpose: PS 5.1 parses ASCII without BOM correctly.

param([switch]$WhatIf)

$fill = 'color = Color(0.62, 0.6, 0.58, 0.22)   ; UiPalette.PlaceholderFill (theme, alpha 0.22)'
$enc = New-Object System.Text.UTF8Encoding($false)
$total = 0
$changed = 0
$files = 0

foreach ($f in (Get-ChildItem darkest\scenes\ui\*.tscn | Sort-Object Name)) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    $out = @()
    $n = 0
    $i = 0
    while ($i -lt $lines.Count) {
        $out += $lines[$i]
        if ($lines[$i] -match 'type="ColorRect"') {
            $total++
            $end = $i + 1
            while ($end -lt $lines.Count -and $lines[$end] -notmatch '^\[node') { $end++ }
            $has = $false
            for ($j = $i + 1; $j -lt $end; $j++) {
                if ($lines[$j] -match '^color = ') { $has = $true }
            }
            if (-not $has) {
                $out += $fill
                $n++
            }
            for ($j = $i + 1; $j -lt $end; $j++) { $out += $lines[$j] }
            $i = $end
            continue
        }
        $i++
    }
    if ($n -gt 0) {
        if (-not $WhatIf) { [System.IO.File]::WriteAllLines($f.FullName, [string[]]$out, $enc) }
        $files++
        $changed += $n
        Write-Output ("  " + $f.Name + "  +" + $n + " color")
    }
}

Write-Output ("placeholder ColorRect total=" + $total + "  filled=" + $changed + "  files=" + $files)
