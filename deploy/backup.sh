#!/usr/bin/env bash
# Nightly backup: the database and the Data Protection keys (without the keys, the shops' API keys in
# the database can't be read, so one is useless without the other). Kept BACKUP_KEEP_DAYS days on the
# server and copied to BACKUP_REMOTE with rclone, if set. Run by cron (see docs/DEPLOY.md):
#
#   deploy/backup.sh
set -euo pipefail
cd "$(dirname "$0")"

set -a
. ./.env
set +a

dir="${BACKUP_DIR:-/var/backups/zalihe}"
keep_days="${BACKUP_KEEP_DAYS:-30}"
stamp="$(date -u +%Y-%m-%dT%H%MZ)"
target="$dir/$stamp"
compose=(docker compose -f docker-compose.yml)

mkdir -p "$target"
chmod 700 "$dir" "$target"

# The custom format is compressed and restored with pg_restore (restore.sh). Written to a temporary
# name first, so a failed dump never looks like a finished one.
"${compose[@]}" exec -T postgres pg_dump -U zalihe -d zalihe --format=custom > "$target/zalihe.dump.part"
mv "$target/zalihe.dump.part" "$target/zalihe.dump"
"${compose[@]}" exec -T app tar -C /data -cf - keys > "$target/keys.tar"
chmod 600 "$target"/*

# A dump that can't be read is no backup: list its contents as a check.
"${compose[@]}" exec -T postgres pg_restore --list < "$target/zalihe.dump" > /dev/null

find "$dir" -mindepth 1 -maxdepth 1 -type d -mtime +"$keep_days" -exec rm -rf {} +

if [[ -n "${BACKUP_REMOTE:-}" ]]; then
  rclone copy "$target" "$BACKUP_REMOTE/$stamp"
  rclone delete --min-age "${keep_days}d" "$BACKUP_REMOTE"
  rclone rmdirs --leave-root "$BACKUP_REMOTE"
fi

echo "Backup $stamp: $(du -sh "$target" | cut -f1)"
