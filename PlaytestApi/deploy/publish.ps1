param(
    [Parameter(Mandatory = $true)]
    [string] $PlayerDir,
    [Parameter(Mandatory = $true)]
    [string] $Version,
    # EliteDesk (lab edge) — replaced Harvester edge-proxy VM at .35
    [string] $HostName = "smlouderback@192.168.0.25",
    [string] $RemoteDataDir = "/opt/playtest/data",
    [string] $IdentityFile = ""
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be MAJOR.MINOR.PATCH, got: $Version"
}
if (-not (Test-Path -LiteralPath $PlayerDir)) {
    throw "PlayerDir not found: $PlayerDir"
}

$sshIdentityArgs = @()
if ($IdentityFile -and (Test-Path -LiteralPath $IdentityFile)) {
    $sshIdentityArgs = @("-i", $IdentityFile)
}
elseif (Test-Path -LiteralPath (Join-Path $env:USERPROFILE ".ssh\id_ed25519")) {
    $sshIdentityArgs = @("-i", (Join-Path $env:USERPROFILE ".ssh\id_ed25519"))
}

$temp = Join-Path $env:TEMP ("bat-playtest-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $zip = Join-Path $temp "Build-A-Tower.zip"
    $versionFile = Join-Path $temp "version.json"
    Compress-Archive -Path (Join-Path $PlayerDir "*") -DestinationPath $zip -Force
    $releasedAt = [DateTimeOffset]::UtcNow.ToString("o")
    @{
        version      = $Version
        releasedAt   = $releasedAt
        downloadPage = "https://escapeproductions.biz/#download"
    } | ConvertTo-Json | Set-Content -Path $versionFile -Encoding utf8

    $remoteZipTmp = "${RemoteDataDir}/Build-A-Tower.zip.tmp"
    $remoteVerTmp = "${RemoteDataDir}/version.json.tmp"
    $remoteZip = "${RemoteDataDir}/Build-A-Tower.zip"
    $remoteVer = "${RemoteDataDir}/version.json"

    & scp @sshIdentityArgs -o BatchMode=yes $zip "${HostName}:${remoteZipTmp}"
    if ($LASTEXITCODE -ne 0) { throw "scp zip failed (exit $LASTEXITCODE)" }
    & scp @sshIdentityArgs -o BatchMode=yes $versionFile "${HostName}:${remoteVerTmp}"
    if ($LASTEXITCODE -ne 0) { throw "scp version.json failed (exit $LASTEXITCODE)" }
    & ssh @sshIdentityArgs -o BatchMode=yes $HostName "mv '$remoteZipTmp' '$remoteZip' && mv '$remoteVerTmp' '$remoteVer'"
    if ($LASTEXITCODE -ne 0) { throw "remote mv failed (exit $LASTEXITCODE)" }

    Write-Host "Published $Version to $HostName ($RemoteDataDir)"
}
finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
