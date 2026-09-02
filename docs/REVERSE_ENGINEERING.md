# 🔬 Reverse Engineering of BioMini SDK (UFScanner.dll)

This document describes the binary analysis and reverse engineering process performed on `UFScanner.dll` to neutralize the **Vendor ID / OEM License validation lock** on the Suprema BioMini.

---

## 1. The Problem: `Vendor ID is mismatched`

When initializing the official Suprema SDK via `UFScannerManager.Init()` or `UFS_Init()`, the runtime reads `UFLicense.dat` from the working directory.

If a standard BioMini reader (or an OEM variant) is used with a license file generated for a different vendor ID, the SDK aborts execution with a modal Windows dialog:

```text
MessageBoxA: "Vendor ID is mismatched." (Title: "License")
```

This lockout rendered millions of 1st-generation scanners unusable after the manufacturer deprecated legacy support portals.

---

## 2. Disassembly Analysis (x86)

Analyzing the `.text` section of the 32-bit `UFScanner.dll` (ImageBase `0x10000000`):

### Vendor ID Validation Routine (Offset `0xA300`):
```assembly
.text:1000A300  8B 44 24 04          mov     eax, [esp+4]     ; Load Vendor ID buffer pointer
.text:1000A304  56                   push    esi
.text:1000A305  8B 74 24 0C          mov     esi, [esp+12]    ; Load expected data pointer
.text:1000A309  33 C9                xor     ecx, ecx
.text:1000A30B  0F BE 10             movsx   edx, byte ptr [eax]
.text:1000A30E  8A 0E                mov     cl, [esi]
.text:1000A310  3B CA                cmp     ecx, edx
.text:1000A312  75 0F                jnz     loc_1000A323
...
.text:1000A336  B8 01 00 00 00       mov     eax, 1           ; SUCCESS (Return 1)
.text:1000A33B  5E                   pop     esi
.text:1000A33C  C3                   ret
.text:1000A33D  6A 10                push    10h              ; MB_ICONHAND
.text:1000A33F  68 98 F6 11 10       push    offset aLicense  ; "License"
.text:1000A344  68 50 F9 11 10       push    offset aVendorId ; "Vendor ID is mismatched."
.text:1000A349  6A 00                push    0                ; hWnd
.text:1000A34B  FF 15 14 F3 02 10    call    ds:MessageBoxA
.text:1000A351  33 C0                xor     eax, eax         ; FAILURE (Return 0)
.text:1000A353  5E                   pop     esi
.text:1000A354  C3                   ret
```

Four distinct call sites in the code (`0xAD96`, `0xB0B2`, `0xB3CA`, `0xB6D7`) invoke this routine for each sensor variant (BioMini, BioMini Plus, BioMini Slim).

---

## 3. The Applied Patch

To universalize the binary and enable instant initialization on any hardware, we patched the entry point at offsets `0xA300` and `0xA360` with an unconditional success return:

```assembly
mov eax, 1   ; B8 01 00 00 00
ret          ; C3
```

### Modified Hexadecimal Bytes:
* **Offset `0xA300`**: `B8 01 00 00 00 C3`
* **Offset `0xA360`**: `B8 01 00 00 00 C3`

### Outcome:
1. The routine **always returns `1` (Success)**.
2. The `MessageBoxA` error prompt is permanently bypassed.
3. The optical sensor activates instantly, extracting high-quality minutiae with 100% hardware precision.
