param(
    [Parameter(Mandatory = $true)]
    [string] $PlayerDir,
    [Parameter(Mandatory = $true)]
    [string] $Version,
    [string] $HostName = "ubuntu@192.168.0.35"
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be MAJOR.MINOR.PATCH, got: $Version"
}
if (-not (Test-Path -LiteralPath $PlayerDir)) {
    throw "PlayerDir not found: $PlayerDir"
}

$temp = Join-Path $env:TEMP ("bat-playtest-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null
$zip = Join-Path $temp "Build-A-Tower.zip"
$versionFile = Join-Path $temp "version.json"
Compress-Archive -Path (Join-Path $PlayerDir "*") -DestinationPath $zip -Force
$releasedAt = [DateTimeOffset]::UtcNow.ToString("o")
@{
    version      = $Version
    releasedAt   = $releasedAt
    downloadPage = "https://escapeproductions.biz/#download"
} | ConvertTo-Json | Set-Content -Path $versionFile -Encoding utf8

scp -o BatchMode=yes $zip "${HostName}:/home/ubuntu/playtest/data/Build-A-Tower.zip.tmp"
scp -o BatchMode=yes $versionFile "${HostName}:/home/ubuntu/playtest/data/version.json.tmp"
ssh -o BatchMode=yes $HostName "mv /home/ubuntu/playtest/data/Build-A-Tower.zip.tmp /home/ubuntu/playtest/data/Build-A-Tower.zip && mv /home/ubuntu/playtest/data/version.json.tmp /home/ubuntu/playtest/data/version.json"
Remove-Item -Recurse -Force $temp
Write-Host "Published $Version to $HostName"
