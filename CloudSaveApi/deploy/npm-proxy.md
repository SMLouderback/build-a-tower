# Nginx Proxy Manager — `api.escapeproductions.biz`

Public TLS terminates on the existing **edge-proxy** VM. The Cloud Save API VM is LAN-only; only the ASP.NET `api` service is proxied. **Do not** add Postgres (5432) or any database port to NPM.

## Prerequisites

- `cloud-save-api` VM running Compose (`api` listening on **8080** inside the VM; not published to the host WAN).
- From **edge-proxy**, confirm reachability: `curl -sf http://<cloud-save-api-lan-ip>:8080/health` → `{"status":"ok"}`.
- Resolve `<cloud-save-api-lan-ip>` via Harvester VM IP, DHCP lease, or `/etc/hosts` on edge-proxy if you use a stable name.

## Proxy host (NPM UI on edge-proxy, port 81)

| Field | Value |
|-------|--------|
| Domain names | `api.escapeproductions.biz` |
| Scheme | `http` |
| Forward hostname / IP | `<cloud-save-api-lan-ip>` |
| Forward port | `8080` |
| Cache assets | Off |
| Block common exploits | On (default) |
| Websockets support | **Off** (REST API only) |
| Access list | Public (default) |

### SSL tab

- Request a new Let's Encrypt certificate for `api.escapeproductions.biz`.
- Force SSL / HTTP/2: **On**.
- HSTS: optional once stable.

### Advanced (optional health check)

If your NPM build supports custom locations, keep the default `/` forward unchanged. Uptime monitors should hit:

```text
GET https://api.escapeproductions.biz/health
```

Expected: `200` with body `{"status":"ok"}`. Do not expose `/health` on a separate Postgres upstream.

## DNS

Point `api.escapeproductions.biz` **A** (or **AAAA**) at the **edge-proxy** WAN address — same pattern as other Escape Production hosts. Clients never connect to `cloud-save-api` directly.

## Checklist

- [ ] Only one proxy host for `api.escapeproductions.biz` → `http://<lan>:8080`
- [ ] No proxy host for Postgres or raw VM SSH
- [ ] Websockets disabled on this host
- [ ] Force HTTPS enabled
- [ ] `/health` returns OK through the public URL

See also: [`backup.md`](backup.md), [`harvester-vm.yaml`](harvester-vm.yaml).
