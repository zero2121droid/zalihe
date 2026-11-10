#!/usr/bin/env bash
# Turns a scene into a 1080x1920 clip: the frame (vertical_frame.py) with a 4:3 crop of the scene,
# scaled to 1080x810, at y=555. The crop is 1440x1080 of a 1920x1080 scene and 1080x810 of a scene
# recorded in record.js's VERTICAL window.
#
#   vertical.sh scene.mp4 out.mp4 frame.png [crop_x]
#
# crop_x: 232 for a usual app screen (default; see STIL.md), 0 for a zoomed shot or content on the left, 240 centered.
# ZOOM="w:h:x:y" first cuts that part of the scene and blows it up to 1920x1080.
# PHONE=1: a phone clip (record.js PHONE); the 780x1160 scene goes into the frame's screen whole, under
# the screen's edge (<frame>-rub.png from vertical_frame.py). crop_x and ZOOM don't apply.
# FADE_IN=1 and FADE_OUT=1 add the clip's opening and closing fades.
set -euo pipefail

input="$1"
output="$2"
frame="$3"
crop_x="${4:-232}"

duration="$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$input")"
filter=""
IFS=, read -r width height < <(ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 "$input")
[ -n "${ZOOM:-}" ] && filter="crop=${ZOOM},scale=${width}:${height}:flags=lanczos,"
filter="[1:v]${filter}crop=ih*4/3:ih:${crop_x}:0,scale=1080:810:flags=lanczos[shot];[0:v][shot]overlay=0:555:shortest=1,format=yuv420p"
inputs=(-loop 1 -framerate 30 -i "$frame" -i "$input")
if [ "${PHONE:-0}" = 1 ]; then
  inputs+=(-loop 1 -framerate 30 -i "${frame%.png}-rub.png")
  filter="[1:v]scale=780:1160:flags=lanczos[shot];[0:v][shot]overlay=150:400:shortest=1[screen];[screen][2:v]overlay=0:0,format=yuv420p"
fi
[ "${FADE_IN:-0}" = 1 ] && filter="${filter},fade=t=in:s=0:n=10"
if [ "${FADE_OUT:-0}" = 1 ]; then
  filter="${filter},fade=t=out:st=$(echo "$duration - 0.5" | bc -l):d=0.5"
fi

temporary="${output%.mp4}.part.mp4"
ffmpeg -y -v error "${inputs[@]}" -filter_complex "$filter" -r 30 -t "$duration" -an \
  -c:v libx264 -crf 20 -preset veryfast -pix_fmt yuv420p -movflags +faststart "$temporary"
mv "$temporary" "$output"
