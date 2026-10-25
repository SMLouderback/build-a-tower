# Playtest sidecar on edge-proxy

## SSH into the right host

All Docker / `/opt/playtest` commands below run on **edge-proxy**, not your Windows PC.

From PowerShell on your PC:

```powershell
ssh ubuntu@192.168.0.35
```

You should see a Linux prompt on that machine before running any `docker` / `cd /opt/playtest` steps.

| What | Where |
|------|--------|
| Host | `ubuntu@192.168.0.35` (edge-proxy LAN) |
| API data | `/opt/playtest/data` (also referenced historically as `/home/ubuntu/playtest/data`) |
| API listen | `172.17.0.1:5080` → container `:8080` (docker0 — required so `escape-web` can reach it; not `127.0.0.1`) |
| Site proxy | `escape-web` nginx proxies `/playtest/` → `172.17.0.1:5080` |

## First install

**On edge-proxy** (`ssh ubuntu@192.168.0.35`):

```bash
sudo mkdir -p /opt/playtest/data
sudo chown -R ubuntu:ubuntu /opt/playtest
printf 'Smtp__Host=\n' > /opt/playtest/.env
cd /opt/playtest
docker build -t playtest-api:local .
docker rm -f playtest-api 2>/dev/null || true
docker run -d --name playtest-api --restart unless-stopped \
  -p 172.17.0.1:5080:8080 \
  --env-file /opt/playtest/.env \
  -e Playtest__DataDirectory=/data \
  -v /opt/playtest/data:/data \
  playtest-api:local
```

Copy `homelab/escapeproductions-site` (including `nginx-default.conf`) over `/opt/escapeproductions-site`, then `docker restart escape-web`.

## Key-request anti-spam (deploy this update)

Requests require **name + email** (optional note). The API emails the **requester** a confirmation link; the studio inbox is notified only after confirm. Gmail dot/+ aliases collapse to one mailbox (`valenciaoprapl@gmail.com` style spam). Rate limit: **8 requests / 10 minutes / IP** (in-memory; restarting `playtest-api` clears it). Pending duplicates within 24h return 409.

### 1. Copy sources from your PC

From PowerShell **on your PC**, first `cd` into the Escape repo (not `C:\WINDOWS\system32`):

```powershell
cd 'C:\OldPC\Importaint Docs\Work\Steve\Escape'

# /opt is root-owned — create + hand ownership to ubuntu
ssh ubuntu@192.168.0.35 "sudo mkdir -p /opt/playtest /opt/escapeproductions-site && sudo chown -R ubuntu:ubuntu /opt/playtest /opt/escapeproductions-site"

# Copy whole folders (no * — Windows OpenSSH scp does not expand globs reliably)
scp -r ".\Build-A-Tower\PlaytestApi\." ubuntu@192.168.0.35:/opt/playtest/
scp -r ".\homelab\escapeproductions-site\." ubuntu@192.168.0.35:/opt/escapeproductions-site/
```

### 2. Rebuild API + restart site on edge-proxy

```powershell
ssh ubuntu@192.168.0.35
```

Then **on that SSH session**:

```bash
cd /opt/playtest
# Prefer the older working SMTP env if /opt one is missing/empty
if [ ! -s /opt/playtest/.env ] || ! grep -q '^Smtp__Host=.\+' /opt/playtest/.env; then
  if [ -f /home/ubuntu/playtest/.env ]; then
    cp /home/ubuntu/playtest/.env /opt/playtest/.env
  else
    printf 'Smtp__Host=\n' > /opt/playtest/.env
  fi
fi
mkdir -p /opt/playtest/data

docker build -t playtest-api:local .
docker rm -f playtest-api
docker run -d --name playtest-api --restart unless-stopped \
  -p 172.17.0.1:5080:8080 \
  --env-file /opt/playtest/.env \
  -e Playtest__DataDirectory=/data \
  -e Playtest__PublicBaseUrl=https://escapeproductions.biz \
  -v /opt/playtest/data:/data \
  playtest-api:local

docker restart escape-web
curl -sS http://172.17.0.1:5080/playtest/health
curl -sS -o /tmp/rk.json -w '%{http_code}\n' -H 'Content-Type: application/json' \
  -d '{"name":"Smoke","email":"smoke@example.com","note":"","website":""}' \
  http://127.0.0.1:8088/playtest/request-key
cat /tmp/rk.json; echo
```

Expect health `{"status":"ok"}` and request-key `200` + `confirm_email_sent` (not `502`). Then smoke-test **Request a Key** on https://escapeproductions.biz/#download (name + email → confirmation mail, **not** a Gmail popup).

### If the site still opens a Gmail popup

That popup is the **503 fallback**: API SMTP is empty/broken. Health can still be `ok`.

**On edge-proxy**, check (Host must be non-empty; do not paste passwords into chat):

```bash
grep -E '^Smtp__' /opt/playtest/.env
# Expect something like:
# Smtp__Host=smtp.gmail.com
# Smtp__Port=587
# Smtp__User=...
# Smtp__Password=...
# Smtp__From=no-reply@escapeproductions.biz
# Smtp__EnableSsl=true

curl -sS -o /tmp/rk.json -w '%{http_code}\n' \
  -H 'Content-Type: application/json' \
  -d '{"name":"Test","email":"you@example.com","note":"diag","website":""}' \
  http://127.0.0.1:5080/playtest/request-key
cat /tmp/rk.json; echo
docker logs --tail 40 playtest-api
```

- `503` + `mail_unavailable` → fill SMTP in `/opt/playtest/.env`, then re-run the `docker rm` / `docker run` block above.
- `200` + `confirm_email_sent` → API is good; hard-refresh the site (Ctrl+F5). Popup should stop.
- Look for an older working env at `/home/ubuntu/playtest/.env` and copy it if needed:  
  `cp /home/ubuntu/playtest/.env /opt/playtest/.env`

## Set or rotate the family key

**On edge-proxy:**

```bash
printf '%s' 'the-new-key' | sh /opt/playtest/set-key.sh
```

Does not change the zip. Old zips still play.

## Publish a new Windows build

From the PC that ran Unity (not edge-proxy):

```powershell
.\PlaytestApi\deploy\publish.ps1 -PlayerDir 'Build\Win64' -Version '1.0.1'
```

Failed scp leaves the previous zip and version in place.
