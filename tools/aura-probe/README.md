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
./Test-HalDiscovery.ps1 -CheckContracts
./Test-HalDiscovery.ps1 -SeparateProcess
./Test-HalDiscovery.ps1 -TestTransport
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
The virtual HAL advertises no effects. Its incoming effect callback can forward
one raw word as an unverified sample to the receiver, but the test never calls
it and requires zero effect/synchronization requests. Synchronization remains
`E_NOTIMPL`. No PC RGB values are sampled. Synthetic transport patterns are generated only
by the explicit `-TestTransport` mode and are labeled `synthetic-test`.

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

## Receiver process and reference ownership

`-CheckContracts` checks our factory/HAL/device ownership directly, without loading
ASUS code: 100 iterations exercise both pointer-array and SAFEARRAY return paths,
release BSTRs/interfaces/arrays, and assert all root reference counts return to one.
This passed. With the SDK, 30 device enumerations left counters 32/31/1 instead of
5/4/1 after three. The excess is specific to the SDK integration path; its exact
ownership contract remains unresolved. Do not compensate with arbitrary Release
calls or keep that enumeration loop alive indefinitely.

`-SeparateProcess` starts a receiver child using an inherited, anonymous shared
memory mapping and events. The HAL and SDK remain together in the original native
host. The receiver does not load ASUS code, enumerate devices or access hardware.
Only an explicit handle list is inherited. A job object terminates the receiver
when the host exits or crashes; the child is assigned while suspended, before it
can execute. An independent 15-second deadline also bounds the receiver lifetime.
Normal stop waits for exit before closing its mapping/events.

The IPC buffer stores only the latest sample. Publication does not wait for the
consumer; sequence/version checks protect snapshots. `-TestTransport` sends two
opaque test patterns through this transport, verifies their acknowledgements and
exact contents, then runs the ordinary SDK discovery checks. This passed with
`syntheticSamples=2`, `unverifiedCallbacks=0`, `colorSource=synthetic-test`. The
ordinary receiver mode produces `hasColorSample=false`, `colorSource=unavailable`.

**The receiving HAL callback is wired but has not been exercised by Aura.** It
accepts only one packed word, forwards it as unverified, and never writes to
hardware. Its effect/packed-color semantics and live delivery remain unverified;
no effect is advertised yet. No real Aura color was received. Direct COM interface marshaling between
the two experimental processes failed (empty enumeration and a stack-overflow in
an adapter experiment); private IPC was selected instead of relying on those vendor
proxy contracts. No such experiments ran inside LightingService.

For lifecycle diagnostics, `-ObserveSeconds 10` keeps the separate-process test alive
briefly. Running it with `-TimeoutSeconds 3` intentionally fails. On Windows, both
native processes were observed before that timeout; afterwards neither process nor
scratch registry key remained. This is a supervisor test, not a successful discovery
run. `-CheckContracts` cannot be combined with the receiver/discovery options.

Next: determine the effect/packed-color contract and supported-effect descriptor,
and establish live HAL discovery before any physical color comparison. There are
no installation or registration commands for LightingService; no persistent receiver
or agent provider has been installed.

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
