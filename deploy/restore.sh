#!/usr/bin/env bash
# Restores a backup made by backup.sh, replacing the current database and keys. The app is stopped
# meanwhile. Asks before doing anything:
#
#   deploy/restore.sh /var/backups/zalihe/2026-10-20T0300Z
set -euo pipefail
cd "$(dirname "$0")"

backup="${1:?Usage: restore.sh <backup folder with zalihe.dump and keys.tar>}"
[[ -f "$backup/zalihe.dump" && -f "$backup/keys.tar" ]] || { echo "No zalihe.dump and keys.tar in $backup" >&2; exit 1; }
compose=(docker compose -f docker-compose.yml)

read -r -p "This replaces the database and keys with $backup. Type 'restore' to continue: " answer
[[ "$answer" == "restore" ]] || { echo "Nothing changed."; exit 1; }

"${compose[@]}" stop app
"${compose[@]}" exec -T postgres psql -U zalihe -d postgres -v ON_ERROR_STOP=1 \
  -c "DROP DATABASE IF EXISTS zalihe WITH (FORCE)" -c "CREATE DATABASE zalihe OWNER zalihe"
"${compose[@]}" exec -T postgres pg_restore -U zalihe -d zalihe --no-owner --exit-on-error < "$backup/zalihe.dump"
# The keys folder is replaced as a whole, so keys made after the backup don't stay behind.
"${compose[@]}" run --rm --no-deps -T --entrypoint bash app \
  -c 'find /data/keys -mindepth 1 -delete && tar -C /data -xf -' < "$backup/keys.tar"
"${compose[@]}" start app

echo "Restored $backup. Check the app, then the shops' connections."
