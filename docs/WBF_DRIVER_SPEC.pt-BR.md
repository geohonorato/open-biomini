# 🛡️ Adaptador Windows Biometric Framework (WBF) — Especificação Técnica e Relatório de P&D

Este documento detalha o design arquitetural, implementação e conclusões da pesquisa de engenharia reversa sobre a integração do **Windows Biometric Framework (WBF)** para o **Suprema BioMini (1ª Geração, PID 0400)**.

---

## 1. Visão Geral da Arquitetura do WBF

O Windows Biometric Framework separa as operações biométricas em três adaptadores no espaço de usuário, gerenciados pelo serviço do Windows `WbioSrvc`:

```
 ┌────────────────────────────────────────────────────────┐
 │           Windows Hello / Subsistema de Logon          │
 └───────────────────────────┬────────────────────────────┘
                             │ WinBio C API
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │        Serviço Biométrico do Windows (WbioSrvc)        │
 └──────┬────────────────────┬────────────────────┬───────┘
        │                    │                    │
 ┌──────▼──────────┐  ┌──────▼──────────┐  ┌──────▼──────────┐
 │ Sensor Adapter  │  │ Engine Adapter  │  │ Storage Adapter │
 │(Captura Óptica) │  │(Match/Minúcias) │  │(Credenciais Win)│
 └─────────────────┘  └─────────────────┘  └─────────────────┘
```

1. **Sensor Adapter (`WbioQuerySensorInterface`)**: Controla o hardware físico (acendimento de LEDs, captura de imagens brutas via pipe USB).
2. **Engine Adapter (`WbioQueryEngineInterface`)**: Extrai vetores de minúcias nos formatos ISO 19794-2 / ANSI 378 e executa o algoritmo de comparação biométrica.
3. **Storage Adapter (`WbioQueryStorageInterface`)**: Salva e consulta com segurança os templates criptografados na base do Windows (`.winbio-db`).

---

## 2. Implementação no OpenBioMini (`wbf/BioMiniSensorAdapter.cpp`)

Para suportar sistemas Windows 10 e 11 de 64 bits:
1. Desenvolvemos uma DLL unificada em C++ 64-bit (`BioMiniSensorAdapter.dll`) exportando `WINBIO_SENSOR_INTERFACE` e `WINBIO_ENGINE_INTERFACE`.
2. Compilada no MSVC 2022 com a diretiva de integridade de código `/INTEGRITYCHECK` e assinada digitalmente com certificado SHA-256 local.
3. Registrada no Registro do Windows sob:
   ```text
   HKLM:\SYSTEM\CurrentControlSet\Services\WbioSrvc\Service Providers\Fingerprint\Virtual Sensors\{E48D0813-CD19-4A9B-A08D-CF28189D2278}
   ```
4. Configurada uma base de dados biométrica dedicada com `BiometricType = 8` (Impressão Digital).

---

## 3. Descobertas Reais e Limitações de Segurança do Windows 11 (A Realidade)

Durante os testes em hardware real no Windows 11, o serviço `WbioSrvc` registrou a seguinte ocorrência:

```text
O Serviço de Biometria do Windows falhou ao carregar o módulo: BioMiniSensorAdapter.dll
Erro: 0x80070241 (ERROR_INVALID_IMAGE_HASH)
"O Windows não pode verificar a assinatura digital deste arquivo."
```

### Análise da Causa-Raiz:
* **UEFI Secure Boot e Integridade de Código de Kernel (HVCI):** No Windows 11, o `WbioSrvc` roda com proteção elevada de processo do sistema. Com o Secure Boot habilitado por padrão em computadores modernos, o Windows rejeita o carregamento de adapters biométricos WBF de terceiros que não possuam uma **Assinatura de Hardware WHQL oficial da Microsoft**.
* Certificados autoassinados só são aceitos se o Windows for colocado no modo de teste (`TESTSIGNING`) com o Secure Boot desativado na BIOS/UEFI.

---

## 4. Status Atual e Caminho Recomendado para Desenvolvedores

| Componente | Status | Pronto para Produção? |
|---|---|---|
| **REST API & WebSocket Bridge (`:8080`)** | ✅ Funcionando 100% | **Sim** — Integra com qualquer WebApp, Electron, Python, C# |
| **SDK Nativo C# (`OpenBioMini.Core`)** | ✅ Funcionando 100% | **Sim** — P/Invoke direto, zero latência |
| **CLI (`biomini.exe`)** | ✅ Funcionando 100% | **Sim** — Terminal e testes automatizados |
| **Windows Hello na Tela de Bloqueio** | 🔬 Experimental (P&D) | **Não** — Exige assinatura WHQL ou Secure Boot desativado |

### Conclusão:
Para todas as aplicações práticas (Sistemas de Ponto, Login em Sistemas Web/Desktop, Clínicas, Automação), os desenvolvedores devem utilizar a **REST Bridge (`:8080`)** ou o **C# Core**, que funcionam com 100% de estabilidade sem depender de certificados WHQL da Microsoft.
