#!/usr/bin/env bash
# Puts the latest pushed version on the server: pulls it, builds the image and restarts what changed.
# New migrations are applied as the app starts. Waits until everything is healthy:
#
#   deploy/deploy.sh
set -euo pipefail
cd "$(dirname "$0")"

git pull --ff-only
docker compose -f docker-compose.yml up -d --build --wait
docker image prune -f > /dev/null
echo "Deployed $(git rev-parse --short HEAD)."
