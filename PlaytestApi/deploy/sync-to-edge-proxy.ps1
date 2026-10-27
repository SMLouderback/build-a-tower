$ErrorActionPreference = "Stop"
$HostName = "ubuntu@192.168.0.35"
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$api = Join-Path $repo "PlaytestApi"
$site = Join-Path (Split-Path $repo -Parent) "homelab\escapeproductions-site"

ssh -o BatchMode=yes $HostName "mkdir -p /opt/playtest/src/Playtest.Api /opt/playtest/data /tmp/playtest-site"
scp -o BatchMode=yes "$api\Dockerfile" "${HostName}:/opt/playtest/Dockerfile"
scp -o BatchMode=yes "$api\deploy\set-key.sh" "${HostName}:/opt/playtest/set-key.sh"
Get-ChildItem "$api\src\Playtest.Api" -File | Where-Object { $_.Extension -in ".cs", ".csproj", ".json" } | ForEach-Object {
    scp -o BatchMode=yes $_.FullName "${HostName}:/opt/playtest/src/Playtest.Api/$($_.Name)"
}
ssh -o BatchMode=yes $HostName "mkdir -p /opt/playtest/src/Playtest.Api/Email"
Get-ChildItem "$api\src\Playtest.Api\Email" -File | ForEach-Object {
    scp -o BatchMode=yes $_.FullName "${HostName}:/opt/playtest/src/Playtest.Api/Email/$($_.Name)"
}

Get-ChildItem $site -File | ForEach-Object {
    scp -o BatchMode=yes $_.FullName "${HostName}:/opt/escapeproductions-site/$($_.Name)"
}

ssh -o BatchMode=yes $HostName @'
set -eu
chmod +x /opt/playtest/set-key.sh
if [ ! -f /opt/playtest/.env ]; then
  printf "Smtp__Host=\n" > /opt/playtest/.env
fi
cd /opt/playtest
docker build -t playtest-api:local .
docker rm -f playtest-api >/dev/null 2>&1 || true
docker run -d --name playtest-api --restart unless-stopped \
  -p 127.0.0.1:5080:8080 \
  --env-file /opt/playtest/.env \
  -e Playtest__DataDirectory=/data \
  -v /opt/playtest/data:/data \
  playtest-api:local
docker restart escape-web
curl -fsS http://127.0.0.1:5080/playtest/health
echo
'@
