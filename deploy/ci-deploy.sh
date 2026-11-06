#!/usr/bin/env bash
# The only thing the GitHub Actions SSH key may run on the server (installed as
# /usr/local/bin/lagerko-ci, see docs/DEPLOY.md, section 15): SSH passes the requested instance here.
set -euo pipefail

case "${SSH_ORIGINAL_COMMAND:-}" in
  beta) exec /usr/local/bin/app deploy lagerko ;;
  dev) exec /usr/local/bin/app deploy lagerko-dev ;;
  *) echo "Unknown instance: ${SSH_ORIGINAL_COMMAND:-}" >&2; exit 1 ;;
esac
