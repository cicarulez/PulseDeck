# Isolated Aura virtual-device experiment

This is a development probe, not an installed Aura device or a PulseDeck provider.
The native x86 executable hosts the installed ASUS SDK and our own minimal HAL.
No vendor binaries, logs or proprietary inspection output belong in Git.

## Build and run

Build in WSL with an i686 MinGW-w64 C compiler:

```bash
./tools/aura-probe/build.sh
# Override the compiler path when it is not on PATH:
AURA_PROBE_CC=/path/to/i686-w64-mingw32-gcc ./tools/aura-probe/build.sh /path/to/output
```

The default output is `artifacts/aura-probe`. Copy the output directory to Windows,
then run in a normal Windows PowerShell session (administrator rights not needed):

```powershell
./Test-HalDiscovery.ps1
./Test-HalDiscovery.ps1 -EmptyDevices
```

Only the development build needs MinGW; running the probe needs the locally
installed, registered x86 ASUS SDK. The executable uses Windows system libraries,
without a .NET or MinGW runtime installation. It is not included in the agent build.

## Isolation and assertions

PowerShell launches a disposable native process with a 20-second limit. The child
registers the COM factory only inside its own process, temporarily redirects HKCR
reads to a private `HKCU\Software\PulseDeck\AuraProbe\<random-id>` tree, and asks
`EumerateHalInfo` to discover it. The normal registry view is restored before
activation. Exactly one entry with our GUID must be present before `CreateHal`.

By default the SDK enumerates the HAL three times. Each pass must return exactly
one device named **PulseDeck Virtual Probe**, width/height 1x1 and one virtual LED.
`-EmptyDevices` exercises the zero-device case and verifies that no capabilities
are requested. Factory/enumeration/capability counters are checked and returned
as JSON; any failed assertion or HRESULT makes the child exit nonzero.

No physical HAL, `SwitchMode`, `Apply`, LED setter or service control is invoked.
The virtual HAL advertises no effects; its mandatory effect/synchronization
callbacks return `E_NOTIMPL`, and the test requires zero calls to them. No RGB
values are sampled, synthesized or represented as the PC's colors.

The parent waits for process exit and removes the private tree on success, failure
or timeout. No system-wide COM registration, startup task or ASUS setting is changed.
The native executable also refuses scratch paths outside its exact private prefix.

## Windows results, SDK 3.07.05.0 — 2026-09-17

- SDK discovery, factory activation, device enumeration, name, dimensions and LED
  count: **passed**, including three consecutive enumerations in one process.
- Empty HAL: **passed**; zero devices, zero capability reads.
- Color/effect/synchronization requests: **zero**.
- The previous managed host crashed at enumeration (`0xC0000005`, `clr.dll`). A
  native HAL alone still crashed in the managed host. Moving both host and HAL to
  native code passed. This avoids the failing path; the exact original cause is
  not established. The previous reproducer is in commit `7455c33`.
- SDK references remain after releasing returned objects: after three one-device
  passes, counters were HAL=5, device=4, factory=1 (each includes one intentional
  root reference). Empty enumeration gave 2/1/1. `referencesAtExit` exposes these
  counts. Do not over-release to force zero or treat this as a verified continuous
  host: process exit bounds the lifetime of all test resources.
- Detection by the running LightingService, an Armoury Crate tile, live Aura color
  reception and physical color matching: **not verified**.

Next: investigate reference ownership and an isolated receiver process before
attempting live service discovery. The probe does not provide installation or
registration commands for LightingService. Do not load it into that service.

The installed GmAcc HAL's virtual branch uses loopback 11000, already owned by
Aura Wallpaper, and precedes the wallpaper branch. Its global settings remain
unchanged; it is not a verified independent PulseDeck destination.

## Inspect contracts without activating COM

```powershell
./Inspect-TypeLibrary.ps1 -Path 'C:\Program Files\ASUS\AuraSDK\AuraSdk_x86.dll'
./Inspect-TypeLibrary.ps1 -Path 'C:\Program Files\ASUS\ASUS_Aac_DRAM\Aac3572DramHal.tlb' -TypePattern '^IAacLedDeviceHal'
```

This reads metadata using `LoadTypeLibEx(REGKIND_NONE)` and never registers the
library. Store results under `%LOCALAPPDATA%\PulseDeck`, not Git. Interface GUIDs
and method signatures describe interoperability contracts; the C implementation
and virtual-device capability description are our own.

Microsoft references: [process-local registry redirection](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regoverridepredefkey),
[COM registration](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-coregisterclassobject),
[type-library loading](https://learn.microsoft.com/en-us/windows/win32/api/oleauto/nf-oleauto-loadtypelibex).
