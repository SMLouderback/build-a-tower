# Normalizes a generated walk strip and installs PNG + bytes into Resources.
param(
    [Parameter(Mandatory = $true)][string]$SourcePath,
    [Parameter(Mandatory = $true)][string]$SheetKey,
    [string]$OutDir
)

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = (Resolve-Path (Join-Path $PSScriptRoot "..\Resources\Art\Agents")).Path
}

Add-Type -AssemblyName System.Drawing

$frameCount = 4
$frameW = 96

function New-GuidHex { return ([guid]::NewGuid().ToString("N")) }

function Write-TextureMeta([string]$Path, [string]$Guid) {
@"

fileFormatVersion: 2
guid: $Guid
TextureImporter:
  serializedVersion: 12
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 1
  nPOTScale: 0
  maxTextureSize: 8192
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    wrapU: 1
    wrapV: 1
  spriteMode: 1
  spritePixelsToUnits: 128
  spritePivot: {x: 0, y: 0}
  alphaIsTransparency: 1
  textureType: 8
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 8192
    textureCompression: 0
    overridden: 0
"@ | Set-Content -Path $Path -Encoding UTF8
}

function Write-BytesMeta([string]$Path, [string]$Guid) {
@"

fileFormatVersion: 2
guid: $Guid
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -Path $Path -Encoding UTF8
}

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

function New-WalkStripBitmap([Drawing.Image]$src) {
    $srcFrameW = [Math]::Max(1, [int][Math]::Round($src.Width / [double]$frameCount))
    $frameH = [Math]::Max($frameW, [int][Math]::Round($src.Height * ($frameW / [double]$srcFrameW)))
    $stripW = $frameW * $frameCount
    $stripH = $frameH

    $destBmp = New-Object Drawing.Bitmap $stripW, $stripH
    $g = [Drawing.Graphics]::FromImage($destBmp)
    $g.Clear([Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($src, 0, 0, $stripW, $stripH)
    $g.Dispose()

    Clear-FlatBackground $destBmp
    return $destBmp
}

if (-not (Test-Path $SourcePath)) { throw "Missing source: $SourcePath" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$src = [Drawing.Image]::FromFile($SourcePath)
$destBmp = New-WalkStripBitmap $src
$stripW = $destBmp.Width
$stripH = $destBmp.Height
$src.Dispose()

$pngPath = Join-Path $OutDir "$SheetKey.png"
$bytesPath = Join-Path $OutDir "$SheetKey.bytes"
$tempPath = Join-Path $OutDir "$SheetKey._install_tmp.png"
$destBmp.Save($tempPath, [Drawing.Imaging.ImageFormat]::Png)
$destBmp.Dispose()
if (Test-Path $tempPath) {
    Copy-Item $tempPath $pngPath -Force
    Remove-Item $tempPath -Force
}
[IO.File]::WriteAllBytes($bytesPath, [IO.File]::ReadAllBytes($pngPath))

if (-not (Test-Path "$pngPath.meta")) { Write-TextureMeta "$pngPath.meta" (New-GuidHex) }
if (-not (Test-Path "$bytesPath.meta")) { Write-BytesMeta "$bytesPath.meta" (New-GuidHex) }

Write-Host "Installed $SheetKey ($stripW x $stripH)"
