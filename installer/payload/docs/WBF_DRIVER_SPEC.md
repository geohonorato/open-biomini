# 🛡️ Windows Biometric Framework (WBF) Adapter — Technical Specification & R&D Report

This document details the architectural design, implementation, and empirical research findings regarding the **Windows Biometric Framework (WBF)** integration for the **Suprema BioMini (1st Gen, PID 0400)**.

---

## 1. WBF Architecture Overview

The Windows Biometric Framework separates biometric operations into three specialized user-mode adapter DLLs managed by the Windows Biometric Service (`WbioSrvc`):

```
 ┌────────────────────────────────────────────────────────┐
 │            Windows Hello / Logon Subsystem             │
 └───────────────────────────┬────────────────────────────┘
                             │ WinBio C API
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │         Windows Biometric Service (WbioSrvc)           │
 └──────┬────────────────────┬────────────────────┬───────┘
        │                    │                    │
 ┌──────▼──────────┐  ┌──────▼──────────┐  ┌──────▼──────────┐
 │ Sensor Adapter  │  │ Engine Adapter  │  │ Storage Adapter │
 │ (Optical Sweep) │  │ (Minutiae Match)│  │ (Win Credentials│
 └─────────────────┘  └─────────────────┘  └─────────────────┘
```

1. **Sensor Adapter (`WbioQuerySensorInterface`)**: Controls physical hardware (turning on LEDs, capturing raw grayscale frame buffers via USB pipe).
2. **Engine Adapter (`WbioQueryEngineInterface`)**: Extracts ISO 19794-2 / ANSI 378 minutiae vectors and executes biometric feature matching.
3. **Storage Adapter (`WbioQueryStorageInterface`)**: Securely saves and queries encrypted templates in the Windows Biometric Database (`.winbio-db`).

---

## 2. Implementation in OpenBioMini (`wbf/BioMiniSensorAdapter.cpp`)

To support x64 Windows 10 and 11 environments:
1. We authored a unified C++ 64-bit adapter DLL (`BioMiniSensorAdapter.dll`) implementing both `WINBIO_SENSOR_INTERFACE` and `WINBIO_ENGINE_INTERFACE`.
2. Built using MSVC 2022 with the `/INTEGRITYCHECK` linker flag and self-signed with a local SHA-256 certificate.
3. Configured the registry under:
   ```text
   HKLM:\SYSTEM\CurrentControlSet\Services\WbioSrvc\Service Providers\Fingerprint\Virtual Sensors\{E48D0813-CD19-4A9B-A08D-CF28189D2278}
   ```
4. Created a dedicated biometric database with `BiometricType = 8` (Fingerprint).

---

## 3. Real-World Findings & Windows 11 Security Boundaries (The Reality)

During hardware deployment on Windows 11, the Windows Biometric Service reported the following log:

```text
The Windows Biometric Service failed to load module: C:\Windows\System32\WinBioPlugIns\BioMiniSensorAdapter.dll
Error: 0x80070241 (ERROR_INVALID_IMAGE_HASH)
"Windows cannot verify the digital signature for this file."
```

### Root Cause Analysis:
* **UEFI Secure Boot & Kernel Code Integrity (HVCI):** On Windows 11, `WbioSrvc` operates as a Protected Process / System Service. Under default security configurations with Secure Boot active, Windows strictly rejects loading third-party WBF adapter DLLs unless they carry a genuine **Microsoft WHQL Hardware Certification Signature**.
* Custom test-certificates and self-signed certificates are blocked unless the system is booted into Developer / Testsigning mode with Secure Boot disabled in the BIOS.

---

## 4. Current Status & Recommended Developer Approach

| Component | Status | Production Ready? |
|---|---|---|
| **REST API & WebSocket Bridge (`:8080`)** | ✅ Working 100% | **Yes** — Works in any WebApp, Electron, Python, C# |
| **Native C# SDK (`OpenBioMini.Core`)** | ✅ Working 100% | **Yes** — Direct P/Invoke, zero overhead |
| **CLI Tool (`biomini.exe`)** | ✅ Working 100% | **Yes** — Terminal & automated testing |
| **Windows Hello Lock Screen Integration** | 🔬 Experimental (R&D) | **No** — Requires WHQL signature or disabled Secure Boot |

### Conclusion:
For all practical biometric applications (Attendance systems, Web Login, Desktop Auth, POS, Electron software), developers should use the **REST Bridge (`:8080`)** or the **C# Core**, which are completely unaffected by Windows Hello WHQL kernel restrictions and work with 100% reliability on real hardware.
