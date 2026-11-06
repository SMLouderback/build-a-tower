# Magenta contact-sheet crop helper for shop dollhouse art.
# Usage:
#   pwsh .superpowers/sdd/shop-dollhouse-crop.ps1 -Sheet path\to\sheet.png -ListOnly
#   pwsh .superpowers/sdd/shop-dollhouse-crop.ps1 -Sheet path\to\sheet.png -Leaf taco_counter_12x1 -StripIndex 0
param(
    [Parameter(Mandatory = $true)][string]$Sheet,
    [string]$Leaf = "",
    [int]$StripIndex = 0,
    [switch]$ListOnly
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$repoRoot = (git rev-parse --show-toplevel)
$dst = Join-Path $repoRoot "Assets/Resources/Art/Dollhouse"

function New-Guid32 { [guid]::NewGuid().ToString("N") }

function Test-IsMagenta([System.Drawing.Color]$c) {
    if ($c.A -lt 20) { return $true }
    # Classic chroma #FF00FF-ish
    if ($c.R -gt 200 -and $c.B -gt 200 -and $c.G -lt 120) { return $true }
    # AI contact-sheet hot pink / fuchsia (high R, low G, mid B)
    if ($c.R -gt 160 -and $c.G -lt 90 -and $c.B -gt 60 -and (($c.R - $c.G) -gt 80)) { return $true }
    return $false
}

function Get-StripBounds([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    # Row is content when enough non-key pixels exist (avoids AA pink edges merging strips).
    $minContentFrac = 0.04
    $rowHasContent = New-Object bool[] $h
    for ($y = 0; $y -lt $h; $y++) {
        $content = 0
        for ($x = 0; $x -lt $w; $x++) {
            if (-not (Test-IsMagenta $bmp.GetPixel($x, $y))) { $content++ }
        }
        $rowHasContent[$y] = (($content / [double]$w) -ge $minContentFrac)
    }

    $strips = @()
    $y = 0
    while ($y -lt $h) {
        while ($y -lt $h -and -not $rowHasContent[$y]) { $y++ }
        if ($y -ge $h) { break }
        $y0 = $y
        while ($y -lt $h -and $rowHasContent[$y]) { $y++ }
        $y1 = $y - 1
        $minX = $w
        $maxX = -1
        for ($yy = $y0; $yy -le $y1; $yy++) {
            for ($x = 0; $x -lt $w; $x++) {
                if (-not (Test-IsMagenta $bmp.GetPixel($x, $yy))) {
                    if ($x -lt $minX) { $minX = $x }
                    if ($x -gt $maxX) { $maxX = $x }
                }
            }
        }
        if ($maxX -ge $minX) {
            $strips += [pscustomobject]@{
                Index = $strips.Count
                X     = $minX
                Y     = $y0
                W     = ($maxX - $minX + 1)
                H     = ($y1 - $y0 + 1)
            }
        }
    }
    return $strips
}

$sheetPath = Resolve-Path $Sheet
$bmp = [System.Drawing.Bitmap]::FromFile($sheetPath)
try {
    $strips = @(Get-StripBounds $bmp)
    if ($strips.Count -eq 0) {
        throw "No non-magenta strips found in $sheetPath"
    }

    if ($ListOnly -or [string]::IsNullOrWhiteSpace($Leaf)) {
        $strips | ForEach-Object {
            "strip $($_.Index): $($_.W)x$($_.H) at ($($_.X),$($_.Y))"
        }
        return
    }

    if ($StripIndex -lt 0 -or $StripIndex -ge $strips.Count) {
        throw "StripIndex $StripIndex out of range 0..$($strips.Count - 1)"
    }

    $s = $strips[$StripIndex]
    $out = New-Object System.Drawing.Bitmap($s.W, $s.H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($yy = 0; $yy -lt $s.H; $yy++) {
            for ($xx = 0; $xx -lt $s.W; $xx++) {
                $c = $bmp.GetPixel($s.X + $xx, $s.Y + $yy)
                if (Test-IsMagenta $c) {
                    $c = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
                }
                $out.SetPixel($xx, $yy, $c)
            }
        }

        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        $png = Join-Path $dst "$Leaf.png"
        $out.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
        Copy-Item $png (Join-Path $dst "$Leaf.bytes") -Force
    }
    finally {
        $out.Dispose()
    }

    $tplPath = Join-Path $dst "fast_food_16x1.png.meta"
    $pngMeta = Join-Path $dst "$Leaf.png.meta"
    if (-not (Test-Path $pngMeta)) {
        $tpl = Get-Content $tplPath -Raw
        $body = $tpl -replace 'guid: [0-9a-f]{32}', ("guid: " + (New-Guid32))
        $body = $body -replace 'spriteID: [0-9a-f]{32}', ("spriteID: " + (New-Guid32))
        Set-Content -Path $pngMeta -Value $body -NoNewline
    }

    $bytesMeta = Join-Path $dst "$Leaf.bytes.meta"
    if (-not (Test-Path $bytesMeta)) {
        @"
fileFormatVersion: 2
guid: $(New-Guid32)
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content $bytesMeta
    }

    Write-Host "wrote $Leaf $($s.W)x$($s.H) from strip $StripIndex"
}
finally {
    $bmp.Dispose()
}
