#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo 'Usage: bash create-test-clip.sh /absolute/runtime/path/pulsedeck-playback-test.mp4' >&2
    exit 2
fi
command -v ffmpeg >/dev/null
command -v ffprobe >/dev/null
clip_path=$1
if [[ "$clip_path" != /* || "$clip_path" != *.mp4 ]]; then
    echo 'Expected an absolute .mp4 output path outside source control.' >&2
    exit 2
fi
repo_path=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
clip_parent=$(realpath -m "$(dirname "$clip_path")")
if [[ "$clip_parent" == "$repo_path" || "$clip_parent" == "$repo_path/"* ]]; then
    echo 'Place the generated clip in a runtime directory outside the repository.' >&2
    exit 2
fi
mkdir -p "$clip_parent"
ffmpeg -hide_banner -loglevel error \
    -f lavfi -i 'color=c=0x0a1012:s=480x1920:r=24:d=3' \
    -vf "drawbox=x=40:y=100:w=400:h=1720:color=0x20404c:t=4,drawgrid=w=80:h=80:t=1:c=0x20404c,drawtext=text='PULSEDECK VIDEO TEST':fontcolor=white:fontsize=26:x=(w-tw)/2:y=100,drawtext=text='LOCAL PLAYBACK':fontcolor=0xb4f58d:fontsize=24:x=(w-tw)/2:y=150,drawtext=text='O':fontcolor=0xb4f58d:fontsize=100:x=190:y=300+1100*t/3" \
    -an -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p \
    -movflags +faststart -n "$clip_path"
ffprobe -v error -count_frames -select_streams v:0 \
    -show_entries stream=codec_name,width,height,avg_frame_rate,nb_read_frames:format=duration,size \
    -of json "$clip_path"
