# DEPRECATED: flat procedural placeholders. Use AI-generated art via GenerateImage +
# Install-AgentWalkSheet.ps1 / Install-AllAgentGenSheets.ps1 instead.
# Generates 38 agent walk sprite sheets (384x96, 4 frames) as PNG + bytes.
param(
    [string]$OutDir = "$PSScriptRoot\..\Resources\Art\Agents"
)

Add-Type -AssemblyName System.Drawing

$frameW = 96
$frameH = 96
$frames = 4
$stripW = $frameW * $frames
$stripH = $frameH

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
    filterMode: 1
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

function Get-Palette([string]$Key) {
    switch -Regex ($Key) {
        'office_worker' { return @{ Skin = [Drawing.Color]::FromArgb(255,220,185); Hair = [Drawing.Color]::FromArgb(60,45,35); Suit = [Drawing.Color]::FromArgb(55,85,140); Accent = [Drawing.Color]::FromArgb(180,150,90) } }
        'hotel_guest'   { return @{ Skin = [Drawing.Color]::FromArgb(230,200,170); Hair = [Drawing.Color]::FromArgb(80,55,40); Suit = [Drawing.Color]::FromArgb(120,70,110); Accent = [Drawing.Color]::FromArgb(200,180,120) } }
        'condo_resident'{ return @{ Skin = [Drawing.Color]::FromArgb(225,195,165); Hair = [Drawing.Color]::FromArgb(50,40,30); Suit = [Drawing.Color]::FromArgb(70,120,95); Accent = [Drawing.Color]::FromArgb(160,130,90) } }
        'street_visitor'{ return @{ Skin = [Drawing.Color]::FromArgb(235,205,175); Hair = [Drawing.Color]::FromArgb(70,50,35); Suit = [Drawing.Color]::FromArgb(180,110,55); Accent = [Drawing.Color]::FromArgb(90,130,170) } }
        'event_visitor' { return @{ Skin = [Drawing.Color]::FromArgb(228,198,168); Hair = [Drawing.Color]::FromArgb(45,35,28); Suit = [Drawing.Color]::FromArgb(90,55,95); Accent = [Drawing.Color]::FromArgb(210,185,110) } }
        'maid'          { return @{ Skin = [Drawing.Color]::FromArgb(225,195,165); Hair = [Drawing.Color]::FromArgb(55,40,30); Suit = [Drawing.Color]::FromArgb(240,240,245); Accent = [Drawing.Color]::FromArgb(120,160,200) } }
        'handyman'      { return @{ Skin = [Drawing.Color]::FromArgb(210,175,140); Hair = [Drawing.Color]::FromArgb(65,45,30); Suit = [Drawing.Color]::FromArgb(160,110,55); Accent = [Drawing.Color]::FromArgb(80,80,85) } }
        'security'      { return @{ Skin = [Drawing.Color]::FromArgb(215,180,145); Hair = [Drawing.Color]::FromArgb(35,30,28); Suit = [Drawing.Color]::FromArgb(45,50,60); Accent = [Drawing.Color]::FromArgb(190,170,80) } }
        'criminal'      { return @{ Skin = [Drawing.Color]::FromArgb(200,170,140); Hair = [Drawing.Color]::FromArgb(30,28,26); Suit = [Drawing.Color]::FromArgb(35,38,42); Accent = [Drawing.Color]::FromArgb(120,30,35) } }
        default         { return @{ Skin = [Drawing.Color]::FromArgb(220,190,160); Hair = [Drawing.Color]::FromArgb(50,40,35); Suit = [Drawing.Color]::FromArgb(100,100,110); Accent = [Drawing.Color]::FromArgb(150,150,150) } }
    }
}

function Tier-Mult([string]$Key) {
    if ($Key -match '_basic$') { return 0.85 }
    if ($Key -match '_mid$') { return 1.0 }
    if ($Key -match '_upper$') { return 1.15 }
    return 1.0
}

function Draw-Figure([System.Drawing.Graphics]$g, [int]$frame, [hashtable]$pal, [bool]$female, [float]$tier) {
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $cx = 48; $footY = 90
    $legSwing = @(8, 0, -8, 0)[$frame % 4]
    $armSwing = @(-6, 0, 6, 0)[$frame % 4]

    $bodyColor = [Drawing.Color]::FromArgb(
        255,
        [Math]::Min(255, [int]($pal.Suit.R * $tier)),
        [Math]::Min(255, [int]($pal.Suit.G * $tier)),
        [Math]::Min(255, [int]($pal.Suit.B * $tier)))

    # Shadow
    $shadow = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40,0,0,0))
    $g.FillEllipse($shadow, $cx - 14, $footY - 4, 28, 6)

    # Legs
    $legBrush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 45, 45, 50))
    $g.FillRectangle($legBrush, $cx - 10 + $legSwing, $footY - 28, 8, 28)
    $g.FillRectangle($legBrush, $cx + 2 - $legSwing, $footY - 28, 8, 28)

    # Torso
    $torsoW = if ($female) { 22 } else { 26 }
    $bodyBrush = New-Object Drawing.SolidBrush $bodyColor
    $g.FillRectangle($bodyBrush, $cx - ($torsoW/2), $footY - 58, $torsoW, 32)

    # Accent (tie/scarf/badge)
    $accent = New-Object Drawing.SolidBrush $pal.Accent
    $g.FillRectangle($accent, $cx - 2, $footY - 54, 4, 18)

    # Head
    $skin = New-Object Drawing.SolidBrush $pal.Skin
    $headR = if ($female) { 11 } else { 12 }
    $g.FillEllipse($skin, $cx - $headR, $footY - 78, $headR*2, $headR*2)

    # Hair
    $hair = New-Object Drawing.SolidBrush $pal.Hair
    if ($female) {
        $g.FillEllipse($hair, $cx - 13, $footY - 82, 26, 16)
        $g.FillRectangle($hair, $cx - 13, $footY - 74, 8, 18)
        $g.FillRectangle($hair, $cx + 5, $footY - 74, 8, 18)
    } else {
        $g.FillEllipse($hair, $cx - 13, $footY - 84, 26, 14)
    }

    # Arms
    $armBrush = New-Object Drawing.SolidBrush $bodyColor
    $g.FillRectangle($armBrush, $cx - ($torsoW/2) - 6, $footY - 54 + $armSwing, 6, 22)
    $g.FillRectangle($armBrush, $cx + ($torsoW/2), $footY - 54 - $armSwing, 6, 22)
}

function New-WalkSheet([string]$Key) {
    $bmp = New-Object Drawing.Bitmap $stripW, $stripH
    $pal = Get-Palette $Key
    $tier = Tier-Mult $Key
    $female = $Key -match '_female'
    for ($f = 0; $f -lt $frames; $f++) {
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.Clear([Drawing.Color]::FromArgb(0,0,0,0))
        $g.Dispose()
    }
    for ($f = 0; $f -lt $frames; $f++) {
        $frameBmp = New-Object Drawing.Bitmap $frameW, $frameH
        $fg = [Drawing.Graphics]::FromImage($frameBmp)
        $fg.Clear([Drawing.Color]::FromArgb(0,0,0,0))
        Draw-Figure $fg $f $pal $female $tier
        $fg.Dispose()
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.DrawImage($frameBmp, $f * $frameW, 0)
        $g.Dispose()
        $frameBmp.Dispose()
    }
    return $bmp
}

$economyRoles = @('office_worker','hotel_guest','condo_resident','street_visitor','event_visitor')
$genders = @('male','female')
$tiers = @('basic','mid','upper')
$staff = @('maid','handyman','security')

$keys = New-Object System.Collections.Generic.List[string]
foreach ($role in $economyRoles) {
    foreach ($g in $genders) {
        foreach ($t in $tiers) { $keys.Add("${role}_${g}_${t}") }
    }
}
foreach ($role in $staff) {
    foreach ($g in $genders) { $keys.Add("${role}_${g}_uniform") }
}
foreach ($g in $genders) { $keys.Add("criminal_${g}") }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$folderMeta = Join-Path (Split-Path $OutDir -Parent) "Agents.meta"
if (-not (Test-Path $folderMeta)) {
    Write-TextureMeta $folderMeta (New-GuidHex)
}

foreach ($key in $keys) {
    $pngPath = Join-Path $OutDir "$key.png"
    $bytesPath = Join-Path $OutDir "$key.bytes"
    $bmp = New-WalkSheet $key
    $bmp.Save($pngPath, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [IO.File]::WriteAllBytes($bytesPath, [IO.File]::ReadAllBytes($pngPath))
    if (-not (Test-Path "$pngPath.meta")) { Write-TextureMeta "$pngPath.meta" (New-GuidHex) }
    if (-not (Test-Path "$bytesPath.meta")) { Write-BytesMeta "$bytesPath.meta" (New-GuidHex) }
    Write-Host "Wrote $key"
}

Write-Host "Generated $($keys.Count) agent walk sheets in $OutDir"
