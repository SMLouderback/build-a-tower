# Repairs installed agent strips: restore vertical aspect + remove white backgrounds.
param(
    [string]$AgentsDir
)

if ([string]::IsNullOrWhiteSpace($AgentsDir)) {
    $AgentsDir = (Resolve-Path (Join-Path $PSScriptRoot "..\Resources\Art\Agents")).Path
}

Add-Type -AssemblyName System.Drawing

$frameCount = 4
$frameW = 96
$targetFrameH = 256
$targetStripH = $targetFrameH

function Clear-FlatBackground([Drawing.Bitmap]$bmp, [int]$threshold = 240) {
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -eq 0) { continue }
            $maxDiff = [Math]::Max([Math]::Abs($c.R - $c.G), [Math]::Abs($c.G - $c.B))
            if ($c.R -ge $threshold -and $c.G -ge $threshold -and $c.B -ge $threshold) {
                $bmp.SetPixel($x, $y, [Drawing.Color]::FromArgb(0, 0, 0, 0))
            }
            elseif ($maxDiff -le 12 -and $c.R -ge 175 -and $c.G -ge 175 -and $c.B -ge 175) {
                $bmp.SetPixel($x, $y, [Drawing.Color]::FromArgb(0, 0, 0, 0))
            }
        }
    }
}

Get-ChildItem $AgentsDir -Filter "*.png" | ForEach-Object {
    if ($_.Name -like "*._*") { return }

    $src = [Drawing.Image]::FromFile($_.FullName)
    $stripW = $frameW * $frameCount
    if ($src.Width -ne $stripW) {
        Write-Host "Skip $($_.Name): unexpected width $($src.Width)"
        $src.Dispose()
        return
    }

    if ($src.Height -eq $targetStripH) {
        $destBmp = New-Object Drawing.Bitmap $src
        $src.Dispose()
        Clear-FlatBackground $destBmp
    }
    else {
        $destBmp = New-Object Drawing.Bitmap $stripW, $targetStripH
        $g = [Drawing.Graphics]::FromImage($destBmp)
        $g.Clear([Drawing.Color]::FromArgb(0, 0, 0, 0))
        $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
        $g.DrawImage($src, 0, 0, $stripW, $targetStripH)
        $g.Dispose()
        $src.Dispose()
        Clear-FlatBackground $destBmp
    }

    $tempPath = Join-Path $AgentsDir ($_.BaseName + "._reprocess_tmp.png")
    $destBmp.Save($tempPath, [Drawing.Imaging.ImageFormat]::Png)
    $destBmp.Dispose()
    Copy-Item $tempPath $_.FullName -Force
    Remove-Item $tempPath -Force

    $bytesPath = Join-Path $AgentsDir ($_.BaseName + ".bytes")
    [IO.File]::WriteAllBytes($bytesPath, [IO.File]::ReadAllBytes($_.FullName))
    Write-Host "Reprocessed $($_.BaseName)"
}

Write-Host "Done reprocess."
