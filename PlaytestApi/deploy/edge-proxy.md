# Playtest sidecar on edge-proxy

Live host: `ubuntu@192.168.0.35`  
Data: `/home/ubuntu/playtest/data`  
API: `172.17.0.1:5080` (nginx on `escape-web` proxies `/playtest/`)

## First install

```bash
sudo mkdir -p /opt/playtest/data
sudo chown -R ubuntu:ubuntu /opt/playtest
printf 'Smtp__Host=\n' > /opt/playtest/.env
cd /opt/playtest
docker build -t playtest-api:local .
docker rm -f playtest-api 2>/dev/null || true
docker run -d --name playtest-api --restart unless-stopped \
  -p 127.0.0.1:5080:8080 \
  --env-file /opt/playtest/.env \
  -e Playtest__DataDirectory=/data \
  -v /opt/playtest/data:/data \
  playtest-api:local
```

Copy `homelab/escapeproductions-site` (including `nginx-default.conf`) over `/opt/escapeproductions-site`, then `docker restart escape-web`.

## Set or rotate the family key

```bash
printf '%s' 'the-new-key' | sh /opt/playtest/set-key.sh
```

Does not change the zip. Old zips still play.

## Publish a new Windows build

From the PC that ran Unity:

```powershell
.\PlaytestApi\deploy\publish.ps1 -PlayerDir 'Build\Win64' -Version '1.0.1'
```

Failed scp leaves the previous zip and version in place.
