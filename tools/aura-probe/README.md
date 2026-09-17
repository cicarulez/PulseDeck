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
./Read-ServiceCapabilities.ps1
./Test-ComIsolation.ps1 -WithSdk
./Test-ComIsolation.ps1 -WithSdk -TerminateServer
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
Read-only effect getters must report exactly one `Static` descriptor, ID 1, with
synchronization disabled (`staticEffectDescriptorVerified=true`).
`-EmptyDevices` exercises the zero-device case and verifies that no capabilities
are requested. Factory/enumeration/capability counters are checked and returned
as JSON; any failed assertion or HRESULT makes the child exit nonzero.

No physical HAL, `SwitchMode`, `Apply`, LED setter or service control is invoked.
The virtual HAL advertises only Static. Its incoming effect callback can forward
one raw word for ID 1 as an unverified sample to the receiver, but the test never calls
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
hardware. The Static descriptor is verified through SDK getters, but packed-color
semantics and live delivery remain unverified. Other effect IDs are rejected with
E_NOTIMPL. No real Aura color was received. Direct COM interface marshaling between
the two experimental processes failed (empty enumeration and a stack-overflow in
an adapter experiment); private IPC was selected instead of relying on those vendor
proxy contracts. No such experiments ran inside LightingService.

For lifecycle diagnostics, `-ObserveSeconds 10` keeps the separate-process test alive
briefly. Running it with `-TimeoutSeconds 3` intentionally fails. On Windows, both
native processes were observed before that timeout; afterwards neither process nor
scratch registry key remained. This is a supervisor test, not a successful discovery
run. `-CheckContracts` cannot be combined with the receiver/discovery options.

Next: establish isolated live HAL discovery and the packed-color contract before
any physical color comparison. There are
no persistent installation commands for LightingService; the optional SYSTEM test
below can publish a temporary category entry. No persistent receiver or agent provider
has been installed.

The installed GmAcc HAL's virtual branch uses loopback 11000, already owned by
Aura Wallpaper, and precedes the wallpaper branch. Its global settings remain
unchanged; it is not a verified independent PulseDeck destination.

## Read capabilities from the running service

`Read-ServiceCapabilities.ps1` requires LightingService to be running and launches
a separate native process with a 20-second deadline. It activates the registered
`ServiceMediator` COM local server and calls only `get_QueryAllDeviceCap`, then
checks that the service PID is unchanged. It validates the returned XML and saves
it under `%LOCALAPPDATA%\PulseDeck\aura-investigation` using a unique filename.
Only the path, PID and presence of our probe name are printed; the full inventory
can contain hardware identifiers and must stay outside Git. No SDK HAL discovery,
system registration, refresh request, profile change or LED setter occurs in this mode.

On this PC, the call succeeded with LightingService PID 6840 unchanged. Its response
contains Aura Wallpaper locations and no PulseDeck entry. These are capabilities,
not live colors or proof that every listed location represents connected hardware.
Static inspection of the installed service's manual refresh path shows teardown
of effect executors; that path was not invoked. The inspected signature check in
DoEnumerateHalInfo targets the SDK DLL, so it does not establish that unsigned HALs
are rejected. The process-local factory and private IPC alone do not expose our HAL
to LightingService; the newer SYSTEM test below checks cross-session activation and
temporary category publication separately from this read-only capability query.

## Native COM server in another process

`Test-ComIsolation.ps1` starts our native STA server and a direct native client.
With `-WithSdk`, it also runs the existing SDK metadata checks using that external
server (`externalComHal=true`). Only the client uses a private discovery category;
the server registers a running class using CLSCTX_LOCAL_SERVER, without Classes
or ASUS registry entries. The class is usable by another process in the same user
session while the server runs. This is not a LightingService installation.

Direct Enumerate2/GetCapability worked initially, but the SDK path crashed at
AuraSdk_x86.dll+0x16b92 because it requires IAacLedDeviceOpt2 from the returned
SAFEARRAY. Added the actual inherited Opt/Device2/Opt2 vtable slots and interface
identities, rather than returning a base-only vtable under a different IID. Direct
and SDK metadata checks now pass. All three additional incoming effect methods
return E_NOTIMPL and increment the request counter; no such method is called in
these tests. Static metadata recognition does not imply working remote colors.

The server pumps STA messages, waits for stop or supervisor exit and has a
15-second deadline. The supervisor bounds each client, stops its server in finally,
and checks that CoGetClassObject subsequently returns REGDB_E_CLASSNOTREG.
`-TerminateServer` deliberately kills only our server after successful checks and
verifies the same registration cleanup. Both normal and forced-exit paths passed.
`-ObserveSeconds 10` permits independent lifecycle observation; terminating that
PowerShell supervisor also caused its observed server to exit within three seconds,
with the class absent afterwards. The external observer could not retrieve a numeric
server exit code, so this last check verifies process/registration cleanup only.

Normal `-WithSdk` server totals: two activations, seven Enumerate2 calls, four
capability reads and zero effect/synchronization requests. The SDK client's local
HAL counters must remain zero. Its local reference roots of 1/1/1 do not measure
remote references or resolve the previously observed SDK lifetime issue.

LightingService runs as LocalSystem. The next test now verifies activation across
that boundary, but real service discovery, color payloads and continuous lifetime
remain unverified. That temporary workflow installs no persistent server or agent provider.
For the subsequent on-demand installation, see below.

Microsoft reference: [running EXE class registration and revocation](https://learn.microsoft.com/en-us/windows/win32/com/registering-a-running-exe-server).

## SYSTEM client and optional temporary Aura publication

Run these sequentially from a normal Windows PowerShell session. Each command
requests UAC elevation for a short-lived helper; the native COM server continues
under the signed-in user's token. No passwords or credentials are supplied.

```powershell
# Diagnostic baseline: expected class-not-registered from the SYSTEM client.
./Test-SystemComIsolation.ps1
# Temporary identity mapping, direct and private-category SDK checks.
./Test-SystemComIsolation.ps1 -InteractiveIdentity -WithSdk
# Publish our HAL category only after those checks succeed; observe, then remove it.
./Test-SystemComIsolation.ps1 -InteractiveIdentity -WithSdk -ObserveAuraSeconds 30
```

The helper creates a unique `PulseDeck-AuraProbe-<run-id>` scheduled task to run our
native client as SYSTEM in session 0. It never changes the existing PulseDeck task.
`-InteractiveIdentity` temporarily creates only the probe CLSID's AppID mapping and
our AppID's `RunAs=Interactive User` value under machine Classes. Existing keys cause
an abort; no replacement occurs. No LocalServer32 autostart entry, global COM ACL,
ASUS setting, SDK control token or service restart is needed.

With `-ObserveAuraSeconds` (1–60), successful direct and SDK checks are prerequisites
for publishing one entry for our CLSID in the SDK third-party HAL category. The
helper reads LightingService capabilities every few seconds; it never calls refresh,
engine/profile methods or LED setters. Cleanup removes its category, CLSID, AppID
and unique task, then verifies absence. Output distinguishes SYSTEM test results from
actual service observations. Full audit JSON and capability XML remain under the
interactive user's LOCALAPPDATA/PulseDeck/aura-investigation, explicitly passed to
the SYSTEM phase; nothing is stored alongside source files.
Before announcing publication, a bounded native `--registered-hal-info` read also
requires exactly one probe GUID in the SDK's real registry inventory. It never calls
CreateHal on those entries, so no vendor hardware module is activated by this check.

The native server supports a validated 1–120-second lifetime (default 15). This
workflow uses 120, watches supervisor exit and normally shuts down sooner. Its added
effect methods still return E_NOTIMPL: advertising Static metadata does not yet make
it a usable live color receiver.

Windows results:

- Baseline SYSTEM/session-0 activation: REGDB_E_CLASSNOTREG, zero server activations.
- Interactive identity mapping: direct metadata read S_OK; three SDK enumerations
  also passed from SYSTEM. Server recorded the expected two activations, seven array
  enumerations, four capabilities and zero effect/sync callbacks.
- 30-second passive category publication: 13 reads without the probe name and no
  additional server activation. Normal handled cleanup verified no keys/task left.
- A second 60-second window was announced for user entry into Aura Sync: 26 reads
  without the probe name and again no additional activation. The user confirmed the
  same devices were visible after entering; this does not establish a HAL rescan.
- A subsequent short publication verified 17 real SDK registry entries, exactly one
  matching the probe GUID. This rules out visibility limited to our private registry
  view; it does not demonstrate that LightingService refreshed its own inventory.
- No probe processes or temporary tasks remained. Configurator returned HTTP 200;
  physical-display transport stayed connected and acknowledgement counts advanced.

The absence of activation does not establish an ASUS signature rejection. The next
question is when/how the running service refreshes its HAL inventory under the user's
normal Armoury Crate workflow. Do not turn the existing read-only query into an
unverified service refresh call.

Microsoft references: [RunAs identity](https://learn.microsoft.com/en-us/windows/win32/com/runas),
[CLSID/AppID mapping](https://learn.microsoft.com/en-us/windows/win32/com/appid-key).

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

## Reversible on-demand installation (experimental)

The development PC now has an experimental registration for a separate
`PulseDeck.AuraHal.exe` under `C:\Program Files\PulseDeck Aura Probe`.
This does not establish an Armoury Crate tile or usable RGB receiver. Effect
callbacks remain unsupported, and no PulseDeck agent provider is enabled.

Run from the built native-probe directory in **elevated Windows PowerShell**:

```powershell
./Manage-InstalledProbe.ps1 -Action Install
./Manage-InstalledProbe.ps1 -Action Status
./Manage-InstalledProbe.ps1 -Action Remove
```

The installed copy of `Manage-InstalledProbe.ps1` also supports Status/Remove.
Install refuses an existing directory or any of our class/AppID/category keys.
It copies only our executables/scripts into an admin-owned folder, applies explicit
read/execute-only access for Users and records ownership plus SHA-256 hashes.
It registers the exact probe CLSID's x86 LocalServer32 path, fully quoted, plus
ServerExecutable. No AppID/RunAs, permanent scheduled task or Windows service is
created. COM launches the HAL as its caller, including SYSTEM/session 0, rather than
requiring a server already running in an interactive login.

Before category publication, a unique bounded SYSTEM task verifies automatic startup,
direct COM metadata, three private-category SDK enumerations and idle exit. The
server retains no ASUS SDK and no hardware ownership. It pumps its STA, honors COM
references/locks, and exits after 15 idle seconds. It suspends class activation before
final shutdown. The task is removed; failures roll back owned registration/files.
If removal cannot observe our server exit within ten seconds, files are retained for
retry instead of forcibly terminating another process or removing locked binaries.
Unexpected files in the directory are preserved. Abrupt termination/power loss of
the installer is not a tested transactional rollback guarantee.

Reports contain metadata/counters only, one latest file per account/session:
`%LOCALAPPDATA%\PulseDeck\aura-installed-probe\session-<id>.json`.
The x86 SYSTEM process writes under
`C:\Windows\SysWOW64\config\systemprofile\AppData\Local\PulseDeck`.
Status reads available reports without activating the HAL; run elevated to read
SYSTEM's report. Installation audit results remain in the installer's LOCALAPPDATA
under `PulseDeck\aura-investigation\installed-runs`. Preserve the audit as the
baseline: two activations/seven enumerations/four capability reads came from our
SYSTEM test, not ASUS. Report timestamps and PIDs distinguish later starts.

After a user-initiated reboot, read Status **before** running any direct/SDK activation
test, then read existing service capabilities and inspect Aura Sync. No reboot or
ASUS service restart is performed by these scripts. Detection after reboot and
physical RGB correspondence remain unverified. Do not invoke the old temporary COM,
SYSTEM identity or class-absence tests while installed: they share the same CLSID.

Windows validation: SYSTEM automatic launch and idle exit passed; attempting a
second Install was refused; Remove left REGDB_E_CLASSNOTREG and no category/class
keys; reinstall passed the same SYSTEM checks. No temporary tasks remained and
ASUS service PIDs were unchanged. The service still did not report PulseDeck.

Microsoft references: [LocalServer32 command registration](https://learn.microsoft.com/en-us/windows/win32/com/localserver32),
[launching-user identity](https://learn.microsoft.com/en-us/windows/win32/com/launching-user),
[class suspension](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-cosuspendclassobjects).

### Subsequent user reboot result

The user still sees no PulseDeck tile. Before any activation self-test, elevated
Status found a new session-0 HAL process and a post-boot report with one activation,
two enumerations, one capability read and two effect callbacks. Thus startup now
loads the component and reaches its incoming methods; the caller PID and specific
callback payloads are not recorded yet. The existing service capability getter
still omits the probe name. E_NOTIMPL callbacks may matter to initialization, but
that is an unverified hypothesis. No real RGB sample has been received/validated.
See the dated validation record before interpreting the earlier negative results.
