# 🔬 OpenBioMini

> **Universal Open-Source Driver, SDK, CLI, and REST Bridge Suite for the Suprema BioMini Fingerprint Scanner.**  
> *Rescuing legacy optical biometric hardware for Windows 10/11 with zero-click driver installation, modern Web/Electron/Python integrations, and reverse-engineered runtime drivers.*

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x86%20%7C%20x64)-brightgreen.svg)]()
[![Hardware](https://img.shields.io/badge/Hardware-Suprema%20BioMini%20(PID%200400)-cyan.svg)]()
[![GUI Setup](https://img.shields.io/badge/Setup-WPF%20Fluent%20Installer-blueviolet.svg)]()
[![REST Bridge](https://img.shields.io/badge/REST%20API-Port%208080%20(CORS)-orange.svg)]()
[![Author](https://img.shields.io/badge/Author-Geovanni%20Honorato-purple.svg)](https://github.com/geohonorato)

---

## 📑 Table of Contents

- [About the Project](#-about-the-project)
- [Key Features](#-key-features)
- [Hardware Compatibility](#-hardware-compatibility)
- [Architecture Overview](#-architecture-overview)
- [One-Click Installation (WPF Setup Wizard)](#-one-click-installation-wpf-setup-wizard)
- [Components & Usage](#-components--usage)
  - [1. CLI Automation Tool (`biomini.exe`)](#1-cli-automation-tool-biominiexe)
  - [2. REST API & WebSocket Bridge (`:8080`)](#2-rest-api--websocket-bridge-8080)
  - [3. Native C# SDK (`OpenBioMini.Core`)](#3-native-c-sdk-openbiominicore)
  - [4. Python Direct Integration](#4-python-direct-integration)
  - [5. Windows Hello WBF Adapter (Experimental)](#5-windows-hello-wbf-adapter-experimental)
- [REST API Reference](#-rest-api-reference)
- [Suprema Error Codes & Troubleshooting](#-suprema-error-codes--troubleshooting)
- [Reverse Engineering Findings](#-reverse-engineering-findings)
- [License & Disclaimer](#-license--disclaimer)
- [Author & Credits](#-author--credits)

---

## 📌 About the Project

The **Suprema BioMini** (`USB\VID_16D1&PID_0400`, SFR300-S/v2) is an industrial optical fingerprint scanner widely deployed across government, education, healthcare, and enterprise environments.

However, after vendor deprecation and transition to newer 64-bit software suites:
1. Legacy driver installers became inaccessible or incompatible with modern 64-bit Windows 11.
2. The official v3.9.1/v3.10.0 SDK removed hardware support for PID `0400` from their internal device tables.
3. Applications crashed with cryptic `Vendor ID is mismatched`, `UFLicense.dat missing`, or `Error 0x80070002` codes.

**OpenBioMini** resolves all of these issues by providing a modern, plug-and-play, 100% open-source software stack that transforms your existing BioMini hardware into a fully integrated scanner ready for web applications, desktop software, and automated scripts.

---

## ✨ Key Features

* 📦 **All-in-One Offline WPF Installer:** Modern Dark-Mode wizard that automatically registers signed kernel drivers via `pnputil`, adds tools to system `PATH`, and sets up services in seconds.
* 🌐 **REST API & CORS Enabled:** Capture fingerprints, stream raw image Base64 buffers, and perform 1:1 matching from **any browser (Chrome/Edge/Firefox), Electron app, or backend service**.
* ⚡ **Zero External Dependencies:** Standalone native binaries that do not require bulky third-party SDK installations or license dongles.
* 💻 **Cross-Language Support:** First-class examples and wrappers for **JavaScript/TypeScript, Python, C# (.NET), Electron, and PowerShell**.
* 🛠️ **Reverse-Engineered Runtime:** Patched native binaries that remove OEM vendor locks and license validation roadblocks.

---

## 🔌 Hardware Compatibility

| Parameter | Specification |
|---|---|
| **Device Model** | Suprema BioMini 1st Gen (SFR300-S / SFR300v2) |
| **USB Hardware ID** | `USB\VID_16D1&PID_0400` |
| **Sensor Type** | High-precision Optical Sensor (Scratch-resistant prism) |
| **Resolution** | 500 DPI / 256 gray levels |
| **Platen / Sensing Area** | 16.0 mm × 18.0 mm |
| **Image Output** | 320 × 480 pixels (Raw grayscale 8-bit & PNG) |
| **Template Formats** | ISO 19794-2, ANSI-378, Suprema Standard (384-byte template) |
| **Operating Systems** | Windows 11, Windows 10, Windows 8.1, Windows 7 (x86 & x64) |

---

## 🏗️ Architecture Overview

```
 ┌────────────────────────────────────────────────────────┐
 │           Client Layer (Your Applications)             │
 │  Web Apps (React/Vue) │ Electron (Veritas) │ Python    │
 └───────────────────────────┬────────────────────────────┘
                             │ HTTP JSON (Port 8080)
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │       OpenBioMini.Bridge.exe (REST / Named Pipe)       │
 │            Self-hosted lightweight HTTP server         │
 └───────────────────────────┬────────────────────────────┘
                             │ Managed Interop (.NET)
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │         OpenBioMini.Core (C# Wrapper Engine)           │
 └─────────────┬────────────────────────────┬─────────────┘
               │                            │
               ▼                            ▼
 ┌───────────────────────────┐ ┌──────────────────────────┐
 │     UFScanner.dll         │ │      UFMatcher.dll       │
 │ (Patched Optical Capture) │ │ (1:1 / 1:N Verification) │
 └─────────────┬─────────────┘ └──────────────────────────┘
               │
               ▼
 ┌────────────────────────────────────────────────────────┐
 │         Windows Kernel PnP Driver (SFRUSB.sys)         │
 └───────────────────────────┬────────────────────────────┘
                             │ USB Bulk Endpoint
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │         Suprema BioMini Hardware (PID 0400)            │
 └────────────────────────────────────────────────────────┘
```

---

## 🚀 One-Click Installation (WPF Setup Wizard)

The fastest way to get started is using the bundled **OpenBioMini Setup Wizard**:

1. Download or locate `Setup-OpenBioMini-v1.0.3.exe` from `dist/` or releases.
2. Run as Administrator.
3. The modern Fluent Dark setup wizard will guide you through:
   - **MIT License Agreement**
   - **Target Installation Folder** (default: `C:\Program Files\OpenBioMini`)
   - **Modular Component Selection:**
     - `Driver USB PnP Oficial`: Auto-registers kernel drivers with `pnputil.exe`.
     - `OpenBioMini CLI`: Command-line tool `biomini.exe`.
     - `REST API & WebSocket Bridge`: Background service on `:8080`.
     - `Windows Hello WBF Adapter`: Experimental biometric provider.
     - `SDK & Documentation`: C# wrappers, headers, and reverse engineering guides.
     - `Add to System PATH`: Enables running `biomini` anywhere in terminal.
4. Click **🚀 Install** and your scanner is immediately ready!

---

## 💻 Components & Usage

### 1. CLI Automation Tool (`biomini.exe`)

The CLI tool allows instant terminal operations, automated testing, and CI/CD validation:

```bash
# 1. Check scanner connection status
biomini status

# 2. Capture a fingerprint (activates optical sensor LED, waits for finger)
biomini scan -o fingerprint.png

# 3. Match two extracted templates (1:1 Verification)
biomini match template1.b64 template2.b64
```

**CLI Output Example:**
```text
==================================================
🔬 OPEN-BIOMINI CLI TOOL v1.0.0
   Criado por: Geovanni Honorato (@geohonorato)
   GitHub: https://github.com/geohonorato/open-biomini
==================================================
[*] Verificando leitor USB... CONECTADO!
    Modelo : SFR300v2
    Serial : hrBioMini11090800000000000039482
```

---

### 2. REST API & WebSocket Bridge (`:8080`)

Launch `OpenBioMini.Bridge.exe` to expose a local HTTP microservice:

#### JavaScript / Web / Electron Example:

```javascript
// 1. Check scanner readiness
const status = await fetch('http://localhost:8080/api/status').then(r => r.json());
console.log('Scanner status:', status);
// Output: { connected: true, model: "SFR300v2", serial: "hrBioMini..." }

// 2. Trigger biometric optical scan
const capture = await fetch('http://localhost:8080/api/scan', { method: 'POST' }).then(r => r.json());

if (capture.success) {
  // Render captured image directly in an HTML image tag
  document.getElementById('fingerprintView').src = `data:image/png;base64,${capture.imageBase64}`;
  
  // Store template for database enrollment or authentication
  console.log('Template Base64:', capture.template);
  console.log('Quality Score:', capture.quality);
}

// 3. Perform 1:1 Match between two templates
const matchResult = await fetch('http://localhost:8080/api/match', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    templateA: userStoredTemplate,
    templateB: capture.template
  })
}).then(r => r.json());

console.log('Match confirmed:', matchResult.match); // true or false
```

---

### 3. Native C# SDK (`OpenBioMini.Core`)

For desktop applications (.NET Framework, WPF, WinForms, Avalonia), reference `OpenBioMini.Core.dll` directly without HTTP overhead:

```csharp
using OpenBioMini;

using (var controller = new BioMiniController()) {
    if (!controller.Initialize()) {
        Console.WriteLine("Scanner not found. Check USB connection.");
        return;
    }

    Console.WriteLine($"Connected to {controller.ScannerModel} (Serial: {controller.ScannerSerial})");

    // Capture with a 5000ms timeout
    ScanResult result = controller.Capture(timeoutMs: 5000);
    if (result.Success) {
        // Save image to disk
        File.WriteAllBytes("fingerprint.png", result.ImageBytes);
        
        // Template Base64
        string templateBase64 = result.TemplateBase64;
        Console.WriteLine($"Captured successfully! Quality: {result.QualityScore}%");
    }
}
```

---

### 4. Python Direct Integration

Integrate easily into Python scripts, Flask/Django backends, or AI pipelines:

```python
import requests
import base64

BRIDGE_URL = "http://localhost:8080/api"

def capture_fingerprint():
    # 1. Check status
    res = requests.get(f"{BRIDGE_URL}/status").json()
    if not res.get("connected"):
        print("Scanner disconnected!")
        return None

    print(f"Place your finger on {res['model']}...")
    
    # 2. Trigger capture
    scan = requests.post(f"{BRIDGE_URL}/scan").json()
    if scan.get("success"):
        # Decode and save PNG image
        img_data = base64.b64decode(scan["imageBase64"])
        with open("fingerprint.png", "wb") as f:
            f.write(img_data)
        
        print(f"Fingerprint captured! Quality: {scan['quality']}%")
        return scan["template"]
    else:
        print("Capture failed:", scan.get("error"))
        return None

if __name__ == "__main__":
    template = capture_fingerprint()
```

---

### 5. Windows Hello WBF Adapter (Experimental)

`wbf/BioMiniSensorAdapter.dll` is an x64 C++ biometric sensor adapter implementing the Windows Biometric Framework (`WINBIO_SENSOR_INTERFACE` and `WINBIO_ENGINE_INTERFACE`).

* **Status:** The adapter compiles with `/INTEGRITYCHECK` and configures under `WbioSrvc`.
* **Important Note for Windows 11:** Windows 11 with UEFI Secure Boot enabled requires a Microsoft WHQL driver signature to load kernel-level biometrics in the Windows Hello lock screen. For custom software development, we recommend using the **REST Bridge** or **OpenBioMini.Core**, which work flawlessly without WHQL restrictions.

---

## 📡 REST API Reference

| Endpoint | Method | Request Payload | Response | Description |
|---|---|---|---|---|
| `/api/status` | `GET` | *None* | `{"connected": bool, "model": string, "serial": string}` | Checks scanner readiness and serial number. |
| `/api/scan` | `POST` | *None* | `{"success": bool, "quality": int, "template": string, "imageBase64": string}` | Turns on optical sensor, captures live finger, extracts template. |
| `/api/match` | `POST` | `{"templateA": string, "templateB": string}` | `{"match": bool, "score": int}` | Performs 1:1 biometric comparison between two Base64 templates. |

---

## 🔍 Suprema Error Codes & Troubleshooting

| Error Code | Name | Meaning & Resolution |
|---|---|---|
| `0` | `UFS_OK` | Success. |
| `-1` | `UFS_ERR_NOT_INITIALIZED` | Scanner was not initialized. Check if `Initialize()` was called. |
| `-2` | `UFS_ERR_ALREADY_INITIALIZED` | Scanner handle is already active. |
| `-101` | `UFS_ERR_NO_LICENSE` | License missing. Solved by OpenBioMini's patched core runtime. |
| `-201` | `UFS_ERR_CANNOT_EXTRACT` | Finger placement was too dry or moved during optical sweep. |
| `0x80070002` | `ERROR_FILE_NOT_FOUND` | Missing core DLL dependencies (`UFScanner.dll` / `UFMatcher.dll`). |

---

## 🔬 Reverse Engineering Findings

For a complete breakdown of disassembly offsets, binary patching of Vendor ID tables, and PnP driver migration steps, refer to:
* [`docs/REVERSE_ENGINEERING.md`](docs/REVERSE_ENGINEERING.md)
* [`docs/WBF_DRIVER_SPEC.md`](docs/WBF_DRIVER_SPEC.md)

---

## 📄 License & Legal Disclaimer

* **OpenBioMini Code & Wrappers:** Licensed under the open-source **[MIT License](LICENSE)**.
* **Disclaimer:** This project is an independent community open-source initiative created for interoperability, research, and hardware preservation. Suprema and BioMini are registered trademarks of their respective owners.

---

## 👤 Author & Credits

Developed and maintained with ❤️ by **Geovanni Honorato**
* 🐙 **GitHub:** [@geohonorato](https://github.com/geohonorato)
* 📦 **Repository:** [geohonorato/open-biomini](https://github.com/geohonorato/open-biomini)
* 📸 **Instagram:** [@geovannihonorato](https://www.instagram.com/geovannihonorato/)
