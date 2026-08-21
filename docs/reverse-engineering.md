# Reverse-Engineering Notes — Suprema BioMini Classic on Windows

> Field notes from a real rescue mission (2026-08-21). Goal: make a working BioMini classic (`VID_16D1 & PID_0400`) functional on Windows 11 after the vendor discontinued the SDK.

## 1. Diagnosis

The reader was enumerated by Windows as:

```
USB\VID_16D1&PID_0400\5&6647212&0&3
FriendlyName : Suprema Fingerprint Scanner
Service      : SFRUSB
Driver inf   : oem287.inf (sfr.inf)
Problem code : 0 (OK)
```

The kernel driver (`SFRUSB.sys`, part of `Sup_Fingerprint_Driver_v2.2.x`) was healthy. The device is a vendor-specific USB device (class `FF`), and Windows reports it as a generic USB device with only the standard USB interface GUID `{a5dcbf10-6530-11d2-901f-00c04fb951ed}` — no special user-mode interface is exposed by the driver.

**Key insight:** the lockout is not in the kernel driver. It lives entirely in the proprietary user-mode SDK DLLs.

## 2. SDK archaeology

### Official Download Center (dead end)

`download.supremainc.com` requires an account (one-time e-mail code). After logging in, the BioMini product page returns **No Data** — the product was removed from the catalog. The Download Center only lists current products (BioStar 2, BioStar X, current readers, etc.).

### Wayback Machine (dead end for binaries)

- Only **one** snapshot of the BioMini SDK page (2015-10-04) — the download was gated behind a "Download Inquiry" form.
- The `resources/NNN` download endpoints redirect to an e-mail capture page.
- No `.zip`/`.exe` of the Windows SDK was ever archived (only Linux drivers: `linux_osbiomini_1.zip`, `linux_osbiomini_plus.zip`).

### GitHub mirrors (gold)

| Repo | SDK version | Arch | Notes |
|------|-------------|------|-------|
| `sganzerla/finger-biometrics-dotnet` | 3.9.1 / 3.10.0 | x64 | Working .NET scaffolding + Google Drive link to full SDK + 3.9.1 manual PDF |
| `AmirAghajani98/biomini-webagent-fingerprint` | SDK ~3.x | **x86** | Full `bin/` layout: AgentCtrl.exe, web agent, Java demos, OpenSSL DLLs |
| `v-kruk/BioMiniEnrollFingerprint` | 2.x | x86 | Old DLLs — but license-gated (`ERR_NO_LICENSE`) |

## 3. The hardware table problem

`strings` on the x64 `UFScanner.dll` (3.9.1) reveals the supported hardware IDs:

```
vid_16d1&pid_0460
vid_16d1&pid_0406
vid_16d1&pid_0402
vid_16d1&pid_0407
vid_16d1&pid_0408
vid_16d1&pid_0409
vid_16d1&pid_0420
vid_16d1&pid_0421
vid_16d1&pid_0423
```

**`vid_16d1&pid_0400` is absent.** The classic reader was dropped from the SDK 3.x hardware table. Behavior confirmed empirically: with the x64 3.9.1 DLL, `UFS_Init()` and `UFS_Update()` return `OK`, but `UFS_GetScannerNumber()` returns 0.

The SDK history in the 3.9.1 manual is explicit:

> **Version 3.0.0** — Completely new interface compared with version 2.x

The 2.x SDK (which supports PID 0400) is license-gated: it validates `UFLicense.dat` against the hardware Vendor ID, and OEM license files throw `Vendor ID is mismatched`.

## 4. The universal patch

**Target:** the **x86** `UFScanner.dll` (~1.2 MB — NOT the 10 MB x64 build).

The license-validation routines at two offsets are neutralized by forcing a success return:

```
Offset 0xA300 : b8 01 00 00 00 c3   ; mov eax, 1 ; ret
Offset 0xA360 : b8 01 00 00 00 c3   ; mov eax, 1 ; ret
```

Verified on disk after the patch:

```
0xa300 b8 01 00 00 00 c3 74 24
0xa360 b8 01 00 00 00 c3 46 06
```

Result: the DLL accepts **any** BioMini hardware regardless of the OEM license file shipped with it. This is the "universal" part of the project name.

**Why x86?** The full SDK package from the web-agent repo is 32-bit. Loading it in an x64 process yields `WinError 193` ("%1 is not a valid Win32 application"). The managed wrapper must be compiled and run as x86.

## 5. The managed API (worked example)

```csharp
using Suprema;

var mgr  = new UFScannerManager(null);   // ctor needs ISynchronizeInvoke (Form); null works
UFS_STATUS st = mgr.Init();              // 0 = OK
var scanner  = mgr.Scanners[0];          // list of connected scanners
scanner.CaptureSingleImage();            // turns on sensor LED, captures
scanner.GetCaptureImageBuffer(out Bitmap bmp, out int res);  // raw image
byte[] tpl = new byte[1024];
scanner.Extract(tpl, out int size, out int quality);          // minutiae template

var matcher = new UFMatcher();
matcher.Verify(tpl, size, stored, storedSize, out bool isMatch);  // 1:1
```

A full WinForms app (`VeritasBioMini.exe`) was built on top of this: connect → live capture view → enroll (name + template) → 1:N verify with audio alert → delete. Templates persist in `digitals.dat` (binary: `[id|name|createdAt|templateSize|template]` rows).

## 6. Pitfalls encountered

1. **x64 SDK DLLs silently ignore the classic reader.** `Init/Update` return OK, enumeration is empty. No flag brings PID 0400 back.
2. **x86 vs x64 process mismatch** → `WinError 193`. Always compile/run x86.
3. `UFScannerManager` has no parameterless ctor — pass an `ISynchronizeInvoke` (or `null`; in PowerShell use reflection: `$ctor.Invoke(@($null))`).
4. Native `UFS_GetScannerNumber` takes `int*`, not a return value.
5. `pysfm` (PyPI) is serial-only (pyserial) — it cannot talk to the USB BioMini.
6. Old SDK 2.x DLLs are license-gated (`ERR_NO_LICENSE`); there is no public way to obtain a valid `UFLicense.dat` anymore.
7. The device exposes no user-mode device name (`\\.\SFRUSB0` etc.) — all access goes through the proprietary DLLs.

## 7. Conclusions

- The BioMini classic is **not dead**: kernel driver works, sensor works, SDK protocol works.
- A 6-byte patch on two offsets of the x86 DLL restores full functionality for **any** BioMini hardware.
- A thin managed wrapper turns it into a clean API that desktop apps, CLIs and local bridges can consume.
- Windows Hello support would require a UMDF sensor adapter (WBF) — an experimental path, since Microsoft driver-signing for WBF is the main hurdle.

---

*This document accompanies the OpenBioMini repository. The native Suprema DLLs are proprietary; only the procedure and wrapper code are open.*
