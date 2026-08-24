# Driver installation — Suprema BioMini (classic)

The BioMini classic (`VID_16D1&PID_0400`) uses the **`SFRUSB.sys`** kernel driver. It is distributed as part of the official driver package:

- `Sup_Fingerprint_Driver_v2.2.x.exe` (installer, InstallShield)
- or the raw driver bundle (`SFRUSB.sys`, `SFR500.sys`, `SFR500DL.sys`, `SFR.inf`, `sfr500.cat`) — the "combo" package also includes Linux drivers.

The same `.inf` covers the whole BioMini family:

| PID   | Install section | Driver |
|-------|-----------------|--------|
| `0400`| `SFRUSB`        | `SFRUSB.sys`  |
| `0401`| `SFRUSB`        | `SFRUSB.sys`  |
| `0402`| `SFR500`        | `SFR500.sys`  |
| `0406`| `SFR500`        | `SFR500.sys`  |
| `0407`| `SFR500`        | `SFR500.sys`  |
| `0408`| `SFR500`        | `SFR500.sys`  |

## Install

1. Plug the reader into USB.
2. Run `Sup_Fingerprint_Driver_v2.2.x.exe` and follow the wizard (UAC prompt appears — accept).
3. Verify the device is healthy:

```powershell
Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like "USB\VID_16D1*" } | Format-List
```

Expected:

```
Status       : OK
Class        : USB
FriendlyName : Suprema Fingerprint Scanner
```

Driver details:

```powershell
Get-PnpDeviceProperty -InstanceId "<instance-id>" -KeyName "DEVPKEY_Device_DriverInfPath","DEVPKEY_Device_Service","DEVPKEY_Device_ProblemCode"
```

Expected: `oem2xx.inf` (sfr.inf), service `SFRUSB`, problem `0`.

> On Windows 10/11 the driver is often picked up automatically by Windows Update when the reader is plugged in. If the FriendlyName shows as `Unknown device` with an empty service, install the package manually.

## Notes

- The reader is **not** a Windows Hello (WBF) device with this driver — it is a vendor-specific USB device. Windows Hello support is an experimental OpenBioMini roadmap item.
- No COM port is created by this driver. Communication happens exclusively through the proprietary SDK DLLs (see `docs/reverse-engineering.md`).
- Driver source package (for reference): `suprema-biomini-combo.zip` (contains `SupremaBioMini/Windows/{Windows 7, 8|Windows XP, Vista}/{x86|amd64}`).
