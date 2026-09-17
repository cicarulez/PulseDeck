# Isolated Aura HAL discovery experiment

This is a development probe, not an installed Aura device or a PulseDeck provider.
It uses the installed ASUS SDK; no vendor binaries or proprietary assets belong here.

Copy this directory outside the repository to a Windows folder, then run in a
normal Windows PowerShell session (administrator rights are not needed):

```powershell
./Test-HalDiscovery.ps1
```

The parent launches a separate 32-bit PowerShell process with a 20-second limit.
The child creates the SDK development facade, registers our COM factory **only
inside that process**, and temporarily redirects its HKCR reads to a private
`HKCU\Software\PulseDeck\AuraProbe\<random-id>` registry tree. The SDK discovers
only our HAL entry. The normal registry view is restored before activating the
HAL. The parent removes the private tree even if the child crashes or times out.
No COM class, HAL entry, startup task or service is installed system-wide.

The default check requires exactly our GUID and one factory activation. It does
not call hardware HALs, `SwitchMode`, `Apply`, LED setters, or ASUS service controls.
Our HAL has no hardware/network access and advertises zero devices. No RGB data
is produced or presented as a reading from the PC.

## Results on SDK 3.07.05.0, 2026-09-17

- Private entry discovered by ASUS `EumerateHalInfo`: yes.
- Our factory activated by ASUS `CreateHal`: yes.
- Actual device enumeration: **blocked by a native process crash**. With the
  standard `IAacLedDeviceHal` interface, calling `EumerateDevices` exits the child
  with `0xC0000005`, before our enumeration callback. Windows reports `clr.dll`
  4.8.9345.0 as the faulting module. This does not establish the root cause.
- Detection by the running LightingService, an Armoury Crate tile, receiving
  real Aura colors and physical color matching: **not verified**.

The crash can be reproduced for debugging with `-EnumerateDevices`; this option
is deliberately excluded from the default check. Do not load this experimental
HAL into LightingService. Next step: resolve the COM boundary in the isolated
host (potentially a native host), then implement a virtual color destination and
establish a supported live discovery path before testing the actual service.

The installed GmAcc HAL also has a virtual-device branch, but its loopback port
11000 is already owned by Aura Wallpaper and that branch precedes the wallpaper
branch. Its global configuration was not changed: it is not a proven independent
PulseDeck destination.

## Inspecting contracts without activating COM

```powershell
./Inspect-TypeLibrary.ps1 -Path 'C:\Program Files\ASUS\AuraSDK\AuraSdk_x86.dll'
./Inspect-TypeLibrary.ps1 -Path 'C:\Program Files\ASUS\ASUS_Aac_DRAM\Aac3572DramHal.tlb' -TypePattern '^IAacLedDeviceHal'
```

This reads type metadata using `LoadTypeLibEx(REGKIND_NONE)` and never registers
the library. Store inspection results under `%LOCALAPPDATA%\PulseDeck`, not Git.
The COM GUIDs/signatures in `DiscoveryHal.cs` describe interoperability contracts;
the implementation is our own minimal test component.

Microsoft references: [process-local registry redirection](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regoverridepredefkey),
[COM class registration](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-coregisterclassobject),
[type-library loading](https://learn.microsoft.com/en-us/windows/win32/api/oleauto/nf-oleauto-loadtypelibex).
