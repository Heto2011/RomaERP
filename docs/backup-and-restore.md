# Backups and restore

## What runs automatically
| Workflow | When | What it does | If it fails |
|---|---|---|---|
| `db-backup.yml` | every night 23:00 UTC | Backs up every database (with checksums), proves each file readable (`RESTORE VERIFYONLY`), gzips it, archives `App_Data` (contract PDFs, face photos, expense proofs), uploads everything to Dropbox `/RomaERP-backups` (AES-256 encrypted when `BACKUP_ENCRYPTION_KEY` is set), keeps 7 days | email to support@romagroup.app |
| `db-restore-test.yml` | Sundays 05:00 UTC | Restores the newest backup of the central DB + up to 2 tenants into a scratch database, runs `DBCC CHECKDB`, drops it | email |
| `server-health.yml` | every 6 hours | Site + API answer, disk < 80 %, memory available ≥ 10 %, `romaerp-api`/`nginx`/SQL container running, newest backup < 36 h old | email |

Local copies live in `/root/db-backups` on the server (7 days). Off-server copies are in Dropbox.

## The encryption key
Secret `BACKUP_ENCRYPTION_KEY` (GitHub → Settings → Secrets → Actions). Use letters and digits only, 32+ characters.
**Keep a copy in a password manager.** Without it the encrypted Dropbox backups cannot be opened — GitHub never shows a secret again.
Changing the key only affects future backups.

## Restoring from Dropbox (disaster recovery)
1. Download the wanted files from Dropbox `/RomaERP-backups` (`<Database>_<date>.bak.gz[.enc]`, `appdata_<date>.tar.gz[.enc]`).
2. Decrypt (skip if the name has no `.enc`):
   `openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -in FILE.enc -out FILE -pass pass:YOUR_KEY`
3. `gunzip <Database>_<date>.bak.gz`
4. Copy the `.bak` into the SQL container (`docker cp`), then in `sqlcmd`:
   `RESTORE DATABASE [<Database>] FROM DISK = N'/var/opt/mssql/backup/<file>.bak' WITH REPLACE, CHECKSUM`
5. Uploaded files: `tar -xzf appdata_<date>.tar.gz -C /var/www/romaerp-api`, then `systemctl restart romaerp-api`.
