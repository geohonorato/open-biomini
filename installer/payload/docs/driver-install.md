# 🔌 Driver Installation Guide — Suprema BioMini

This document explains how the kernel-level PnP driver is installed for the **Suprema BioMini (1st Gen, PID 0400)** on Windows 10 and Windows 11.

---

## 1. How It Works (The Reality)

The classic Suprema BioMini (`USB\VID_16D1&PID_0400`) relies on the official signed kernel driver (`SFR.inf` / `SFR500.sys` / `sfr500.cat`).

### Automated Installation (Recommended)
When using the **OpenBioMini Setup Wizard (`Setup-OpenBioMini-v1.0.3.exe`)**, the installer automatically invokes Windows PnP utility:

```cmd
pnputil.exe /add-driver "driver\SFR.inf" /install
```

This extracts and registers the signed driver directly into the Windows Driver Store (`C:\Windows\System32\DriverStore\FileRepository\`) without requiring any manual INF clicking or third-party tools.

---

## 2. Manual Installation via Terminal

If you prefer installing the driver manually without the GUI setup wizard:

1. Open PowerShell or Command Prompt as **Administrator**.
2. Navigate to the `driver/` folder of this repository.
3. Execute:

```powershell
pnputil /add-driver "SFR.inf" /install
```

### Verifying Driver Status

To check if the driver was successfully bound to the scanner:

```powershell
Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like "USB\VID_16D1*" } | Format-List FriendlyName, Status, ProblemCode
```

**Expected output:**
```text
FriendlyName : Suprema Fingerprint Scanner
Status       : OK
ProblemCode  : CM_PROB_NONE
```

---

## 3. Important Facts

* **PnP Device:** The hardware connects as a vendor-specific USB bulk device (`Class GUID: {a5dcbf10-6530-11d2-901f-00c04fb951ed}`).
* **No Virtual COM Port:** The BioMini does not use a serial COM port. All communication occurs via USB bulk transfers managed through the patched `UFScanner.dll` runtime.
* **Windows Hello:** Standard driver installation enables the scanner for all custom software (CLI, REST Bridge, Web, Python, C#). It does not automatically enable Windows Hello lock screen login due to Windows 11 WHQL kernel signing policies (see `docs/WBF_DRIVER_SPEC.md`).
