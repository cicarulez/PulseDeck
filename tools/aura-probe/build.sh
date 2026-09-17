#!/usr/bin/env bash
set -euo pipefail
probe_source=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
probe_output=${1:-"$probe_source/../../artifacts/aura-probe"}
probe_compiler=${AURA_PROBE_CC:-i686-w64-mingw32-gcc}
mkdir -p -- "$probe_output"
"$probe_compiler" -std=c11 -O2 -Wall -Wextra -Werror -static-libgcc \
    "$probe_source/DiscoveryHal.c" "$probe_source/NativeHost.c" "$probe_source/Receiver.c" \
    -o "$probe_output/PulseDeck.AuraProbe.exe" -lole32 -loleaut32 -luuid
cp -- "$probe_source/Test-HalDiscovery.ps1" "$probe_source/Inspect-TypeLibrary.ps1" \
    "$probe_source/Read-ServiceCapabilities.ps1" "$probe_source/Test-ComIsolation.ps1" \
    "$probe_source/Test-SystemComIsolation.ps1" "$probe_output/"
