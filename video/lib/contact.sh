#!/usr/bin/env bash
# Joins a clip's check frames into one image, for the check before handing it over.
#   contact.sh video/izvestaji/kadrovi out.jpg [columns]
set -euo pipefail
ffmpeg -y -v error -pattern_type glob -i "$1/kadar-*.jpg" -vf "crop=1080:810:0:555,scale=720:-1,tile=${3:-3}x3:padding=8:color=white" -frames:v 1 -q:v 3 "$2"
