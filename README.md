# 🔬 OpenBioMini

> **Universal Open-Source Bridge, CLI, and SDK for the Suprema BioMini USB Fingerprint Scanner.**  
> *Rescuing legacy biometric hardware with modern REST APIs, CLI automation, and patched runtime drivers.*

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20x86%20%7C%20x64-brightgreen.svg)]()
[![Status](https://img.shields.io/badge/Hardware%20Status-Tested%20%26%20Working-success.svg)]()
[![C#](https://img.shields.io/badge/.NET-C%23-purple.svg)]()
[![REST API](https://img.shields.io/badge/REST%20API-Port%208080-orange.svg)]()

---

## 📌 About the Project

The **Suprema BioMini** (`VID_16D1 & PID_0400`, SFR300-S) is one of the most widely deployed optical fingerprint scanners in the world. However, after the manufacturer transition and deprecation of legacy portals, developers worldwide were locked out by missing SDK downloads, license corruption errors, and `Vendor ID is mismatched` popups.

**OpenBioMini** provides a complete, modern, and open-source solution:
1. **Patched Native SDK**: Eliminates OEM Vendor ID locks and license blocks via binary patching.
2. **REST API Bridge**: A lightweight background service (`OpenBioMini.Bridge.exe`) that exposes local HTTP endpoints (`http://localhost:8080/api/`) with CORS enabled—allowing **any WebApp, Electron app (like [Veritas](https://github.com/geohonorato)), Python, or Node.js service** to capture fingerprints with 3 lines of code.
3. **CLI Automation Tool**: A standalone command-line tool (`biomini.exe`) for terminal scripts and CI/CD pipelines.

---

## 🏗️ Architecture

```
                                  ┌────────────────────────┐
                                  │   Web Browser / SPA    │
                                  │ (React, Vue, Electron) │
                                  └───────────┬────────────┘
                                              │ HTTP JSON / CORS
                                              ▼
┌─────────────────────────┐       ┌────────────────────────┐
│  Python / CLI Scripts   │──────►│  OpenBioMini.Bridge    │
│    (biomini.exe)        │       │  (HTTP Server :8080)   │
└─────────────────────────┘       └───────────┬────────────┘
                                              │ C# Interop
                                              ▼
                                  ┌────────────────────────┐
                                  │   OpenBioMini.Core     │
                                  │(UFScanner + UFMatcher) │
                                  └───────────┬────────────┘
                                              │ Kernel I/O
                                              ▼
                                  ┌────────────────────────┐
                                  │  Suprema BioMini USB   │
                                  │ (SFR300v2 Optical HW)  │
                                  └────────────────────────┘
```

---

## 🚀 Quick Start

### 1. Requirements & Hardware Drivers
* Windows 10 or Windows 11 (64-bit / 32-bit).
* Suprema BioMini USB Driver (`SFRUSB.sys` / `oem286.inf`).
* Connected **Suprema BioMini Scanner** (`USB\VID_16D1&PID_0400`).

---

### 2. Using the CLI (`biomini.exe`)

Open your terminal in `cli/` and run:

```bash
# Check hardware connection
biomini.exe status

# Capture a fingerprint and save both PNG image and Base64 template
biomini.exe scan -o my_fingerprint.png

# Compare two extracted templates (1:1 Verification)
biomini.exe match template1.b64 template2.b64
```

**Output Example:**
```text
==================================================
🔬 OPEN-BIOMINI CLI TOOL v1.0.0
==================================================
[*] Verificando leitor USB... CONECTADO!
    Modelo : SFR300v2
    Serial : hrBioMini11090800000000000039482
```

---

### 3. Using the HTTP REST Bridge (Web & Electron)

Start the local bridge server:
```bash
OpenBioMini.Bridge.exe
```

Now from **any web page or Electron frontend**, capture fingerprints directly:

```javascript
// 1. Check if scanner is ready
const status = await fetch('http://localhost:8080/api/status').then(r => r.json());
console.log(status); // { connected: true, model: "SFR300v2", serial: "..." }

// 2. Trigger optical scan (LED turns on, captures physical finger)
const scan = await fetch('http://localhost:8080/api/scan', { method: 'POST' }).then(r => r.json());
if (scan.success) {
  // Display image directly in an <img> tag
  document.getElementById('myImg').src = `data:image/png;base64,${scan.imageBase64}`;
  console.log(`Quality: ${scan.quality}%, Template: ${scan.template}`);
}

// 3. Compare two templates (1:1 Match)
const match = await fetch('http://localhost:8080/api/match', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ templateA: scan1.template, templateB: scan2.template })
}).then(r => r.json());

console.log(match.match); // true / false
```

---

## 📡 REST API Reference

| Endpoint | Method | Description |
|---|---|---|
| `/api/status` | `GET` | Returns connection status, model, and serial number |
| `/api/scan` | `POST` | Triggers optical capture; returns PNG Base64 and ISO/ANSI template |
| `/api/match` | `POST` | Compares two templates (`templateA`, `templateB`) and returns `{ match: true/false }` |

---

## 🔬 Reverse Engineering & License Patch

For a detailed technical breakdown of how the `Vendor ID is mismatched` error was disassembled and patched at offsets `0xA300` and `0xA360` in `UFScanner.dll`, read [docs/REVERSE_ENGINEERING.md](docs/REVERSE_ENGINEERING.md).

---

## 🇧🇷 Resumo em Português

O **OpenBioMini** é uma solução completa para desenvolvedores que precisam integrar o leitor biométrico **Suprema BioMini** em aplicações modernas (Web, Electron, Python, C#, Node.js). 

Ele elimina a dependência dos SDKs descontinuados da fabricante e fornece:
* **Ponte REST Local (`:8080`)**: Permite capturar e verificar digitais pelo navegador com 3 linhas de JavaScript.
* **Ferramenta CLI (`biomini.exe`)**: Para automações de terminal e scripts.
* **Driver Patched Universal**: Sem bloqueios de `Vendor ID` ou erro de licença.

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.  
Maintained by **[Geovanni Honorato](https://github.com/geohonorato)**.
