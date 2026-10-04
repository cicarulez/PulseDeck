#!/usr/bin/env bash
set -euo pipefail
probe_source=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
probe_output=${1:-"$probe_source/../../artifacts/aura-probe"}
probe_compiler=${AURA_PROBE_CC:-i686-w64-mingw32-gcc}
probe_led_count=${AURA_PROBE_LED_COUNT:-1}
if [[ ! "$probe_led_count" =~ ^([1-9]|[1-5][0-9]|6[0-4])$ ]]; then
    echo 'AURA_PROBE_LED_COUNT must be between 1 and 64.' >&2
    exit 2
fi
mkdir -p -- "$probe_output"
"$probe_compiler" -DPROBE_LED_COUNT="$probe_led_count" -std=c11 -O2 -Wall -Wextra -Werror -static-libgcc \
    "$probe_source/DiscoveryHal.c" "$probe_source/NativeHost.c" "$probe_source/Receiver.c" "$probe_source/IncomingSample.c" \
    -o "$probe_output/PulseDeck.AuraProbe.exe" -lole32 -loleaut32 -luuid
"$probe_compiler" -DPROBE_LED_COUNT="$probe_led_count" -std=c11 -O2 -Wall -Wextra -Werror -static-libgcc -mwindows \
    "$probe_source/DiscoveryHal.c" "$probe_source/InstalledHost.c" "$probe_source/Receiver.c" "$probe_source/IncomingSample.c" \
    -o "$probe_output/PulseDeck.AuraHal.exe" -lole32 -loleaut32 -luuid -lshell32
cp -- "$probe_source/Test-HalDiscovery.ps1" "$probe_source/Inspect-TypeLibrary.ps1" \
    "$probe_source/Read-ServiceCapabilities.ps1" "$probe_source/Test-ComIsolation.ps1" \
    "$probe_source/Test-SystemComIsolation.ps1" "$probe_source/Manage-InstalledProbe.ps1" "$probe_output/"
