# 🛡️ Windows Biometric Framework (WBF) Driver Specification

Este documento define a arquitetura para o desenvolvimento de um **Driver UMDF v2 (User-Mode Driver Framework)** que integra o **Suprema BioMini** ao **Windows Biometric Framework (WBF)** para permitir o login via **Windows Hello**.

---

## 1. Arquitetura do WBF no Windows

O Windows Biometric Framework é estruturado em três adaptadores principais (WBDI - *Windows Biometric Driver Interface*):

```
┌────────────────────────────────────────────────────────┐
│                   Windows Hello / Winlogon             │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│            Windows Biometric Service (WbioSrvc)        │
└──────┬────────────────────┬────────────────────┬───────┘
       │                    │                    │
┌──────▼──────────┐  ┌──────▼──────────┐  ┌──────▼──────────┐
│  Sensor Adapter │  │  Engine Adapter │  │ Storage Adapter │
│ (Captura Óptica)│  │ (Match/Minúcias)│  │ (Templates Win) │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

1. **Sensor Adapter**: Gerencia o hardware físico (acende LED, captura imagem bruta via `UFScanner.dll` ou `SFRUSB.sys`).
2. **Engine Adapter**: Extrai os vetores de minúcias ISO/ANSI e executa o algoritmo de matching (`UFMatcher.dll`).
3. **Storage Adapter**: Persiste e consulta os templates criptografados pelo subsistema de credenciais do Windows.

---

## 2. Implementação do Driver UMDF v2

Para o BioMini, o driver deve expor a interface de dispositivo WBDI com o GUID de classe:
```c
// GUID_DEVINTERFACE_BIOMETRIC_READER
DEFINE_GUID(GUID_DEVINTERFACE_BIOMETRIC_READER,
    0xE2830510, 0x487B, 0x4760, 0x8C, 0x2B, 0x5E, 0x04, 0x6E, 0x6E, 0x3F, 0x8D);
```

### IOCTLs Críticos a Implementar:
* `IOCTL_BIOMETRIC_GET_ATTRIBUTES` -> Retorna resolução (500 DPI), dimensões do sensor e formato de imagem.
* `IOCTL_BIOMETRIC_GET_SENSOR_STATUS` -> Retorna `WINBIO_SENSOR_READY` / `WINBIO_SENSOR_BUSY`.
* `IOCTL_BIOMETRIC_CAPTURE_DATA` -> Dispara a captura óptica do BioMini e retorna o buffer de imagem ou minúcias.
