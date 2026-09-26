# Cloud Save API — Postgres backups

Player towers and account metadata live in PostgreSQL on the **cloud-save-api** VM (Compose volume `postgres_data`). Treat backups as part of going live; **Harvester VM snapshots are not backups** — they lack point-in-time recovery, off-cluster isolation, and tested restore paths.

## What to back up

1. **Logical dumps** — `pg_dump` (custom or directory format) of the `cloudsave` database on a schedule.
2. **WAL archiving** — continuous WAL segments to off-VM storage so you can replay to a timestamp between dumps.
3. **Secrets inventory** — document where JWT signing keys and production `.env` live (not in git); restore requires the same secrets or a controlled rotation plan.

Compose dev defaults use a local password; production must override via `.env` and match `ConnectionStrings__CloudSavePostgres`.

## Suggested schedule (ops)

| Job | Frequency | Retention |
|-----|-----------|-----------|
| `pg_dump -Fc` | Daily (off-peak) | 30 days off-cluster |
| WAL archive sync | Continuous | Align with RPO target (e.g. 15 min) |
| Restore rehearsal | Before `PublicRegistration: true` | Prove last good dump + WAL |

Store artifacts on storage **outside** the Harvester cluster (NAS, object store, encrypted backup target). Encrypt in transit and at rest.

## Example dump (on VM, adjust credentials from `.env`)

```bash
cd /opt/cloud-save-api
docker compose exec -T postgres pg_dump -U cloudsave -Fc cloudsave > "/var/backups/cloudsave-$(date -u +%Y%m%dT%H%M%SZ).dump"
```

Copy the file off the VM immediately. Do not rely on the VM disk as the only copy.

## WAL

Enable `archive_mode` and `archive_command` in Postgres for production (custom `postgresql.conf` or Compose override). Ship WAL files to the same off-cluster target as dumps. Document base-backup + WAL replay procedure for your Postgres version.

## Restore rehearsal checklist (required before public registration)

Complete on a **non-production** database or disposable VM clone:

- [ ] Restore latest `pg_dump` into empty Postgres; API starts and `/health` is OK.
- [ ] Run `dotnet test` smoke or manual login against restored DB (LAN/Tailscale).
- [ ] Register a test account, upload a slot, download, force a conflict, restore a revision archive row.
- [ ] Verify invite codes and `PublicRegistration: false` still gate registration.
- [ ] Record restore duration and who performed the drill.

Only after a successful rehearsal should ops flip `PublicRegistration` to `true` (see `appsettings.json` / environment override).

## What snapshots are good for

VM snapshots help **rollback a bad deploy** on the same VM. They do **not** replace dump + WAL for accidental deletion, ransomware, or cluster loss. Use both: immutable backups for data, snapshots sparingly for compose image rollbacks.
