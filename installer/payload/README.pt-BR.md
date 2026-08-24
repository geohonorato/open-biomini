# 🔬 OpenBioMini (Português do Brasil)

> **Suíte Universal Open-Source de Driver, SDK, CLI e REST Bridge para o Leitor Biométrico Suprema BioMini.**  
> *Resgate de hardware biométrico óptico para Windows 10 e 11 com instalador automatizado de 1 clique, integração moderna para Web/Electron/Python e drivers de runtime com engenharia reversa.*

[![Licença: MIT](https://img.shields.io/badge/Licen%C3%A7a-MIT-blue.svg)](LICENSE)
[![Plataforma](https://img.shields.io/badge/Plataforma-Windows%2010%20%2F%2011%20(x86%20%7C%20x64)-brightgreen.svg)]()
[![Hardware](https://img.shields.io/badge/Hardware-Suprema%20BioMini%20(PID%200400)-cyan.svg)]()
[![Instalador GUI](https://img.shields.io/badge/Instalador-WPF%20Fluent%20Dark-blueviolet.svg)]()
[![REST Bridge](https://img.shields.io/badge/REST%20API-Porta%208080%20(CORS)-orange.svg)]()
[![Autor](https://img.shields.io/badge/Autor-Geovanni%20Honorato-purple.svg)](https://github.com/geohonorato)

---

## 📑 Sumário

- [Sobre o Projeto](#-sobre-o-projeto)
- [Principais Recursos](#-principais-recursos)
- [Compatibilidade de Hardware](#-compatibilidade-de-hardware)
- [Visão Geral da Arquitetura](#-visão-geral-da-arquitetura)
- [Instalação Automatizada (Assistente WPF)](#-instalação-automatizada-assistente-wpf)
- [Componentes e Como Usar](#-componentes-e-como-usar)
  - [1. Ferramenta de Linha de Comando (`biomini.exe`)](#1-ferramenta-de-linha-de-comando-biominiexe)
  - [2. REST API & WebSocket Bridge (`:8080`)](#2-rest-api--websocket-bridge-8080)
  - [3. SDK Nativo em C# (`OpenBioMini.Core`)](#3-sdk-nativo-em-c-openbiominicore)
  - [4. Integração Direta em Python](#4-integração-direta-em-python)
  - [5. Adaptador Windows Hello WBF (Experimental)](#5-adaptador-windows-hello-wbf-experimental)
- [Referência Completa da API REST](#-referência-completa-da-api-rest)
- [Códigos de Erro da Suprema e Resolução de Problemas](#-códigos-de-erro-da-suprema-e-resolução-de-problemas)
- [Engenharia Reversa e Patch de Hardware](#-engenharia-reversa-e-patch-de-hardware)
- [Licença e Isenção de Responsabilidade](#-licença-e-isenção-de-responsabilidade)
- [Autor e Créditos](#-autor-e-créditos)

---

## 📌 Sobre o Projeto

O **Suprema BioMini** (`USB\VID_16D1&PID_0400`, modelo SFR300-S / SFR300v2) é um dos leitores biométricos ópticos mais confiáveis e difundidos no mundo, muito utilizado em órgãos públicos, cartórios, universidades, clínicas e sistemas de ponto.

Com a descontinuação da 1ª geração pelo fabricante e a transição para pacotes SDK modernos de 64 bits:
1. O suporte ao PID `0400` foi removido das tabelas internas das DLLs oficiais v3.9.1/v3.10.0.
2. Desenvolvedores enfrentavam erros como `Vendor ID is mismatched`, licenças corrompidas (`UFLicense.dat missing`) e instaladores de driver incompatíveis com o Windows 11 64-bit.
3. Milhares de dispositivos em perfeito estado físico corriam o risco de virar lixo eletrônico.

O **OpenBioMini** resolve todos esses problemas fornecendo uma suíte completa, modular e pronta para uso em aplicações web, Electron, desktop e scripts de terminal.

---

## ✨ Principais Recursos

* 📦 **Instalador Offline em WPF:** Assistente visual moderno em Dark Mode que instala drivers assinados pelo `pnputil`, configura o `PATH` do sistema e registra serviços sem depender de internet.
* 🌐 **REST API Local com CORS Liberado:** Capture digitais e faça comparações 1:1 diretamente do seu navegador (**React, Vue, Angular, Electron ou Node.js**) usando chamadas `fetch()` simples.
* ⚡ **Sem Dependências Externas Pesadas:** Não requer a instalação do instalador oficial de 400 MB da Suprema nem dongles de licença.
* 💻 **Multi-Linguagem:** Exemplos prontos para **JavaScript, TypeScript, Python, C#, Electron e PowerShell**.
* 🛠️ **Patch de Interoperabilidade:** Bibliotecas Core modificadas cirurgicamente para eliminar bloqueios de OEM e Vendor ID.

---

## 🔌 Compatibilidade de Hardware

| Parâmetro | Especificação Técnica |
|---|---|
| **Modelo Suportado** | Suprema BioMini 1ª Geração (SFR300-S / SFR300v2) |
| **Hardware ID USB** | `USB\VID_16D1&PID_0400` |
| **Tipo de Sensor** | Sensor Óptico de Alta Resolução (Prisma resistente a riscos) |
| **Resolução** | 500 DPI / 256 níveis de cinza |
| **Área do Sensor** | 16,0 mm × 18,0 mm |
| **Resolução da Imagem** | 320 × 480 pixels (8-bit Grayscale & PNG) |
| **Formatos de Template** | ISO 19794-2, ANSI-378, Suprema Standard (384 bytes) |
| **Sistemas Operacionais** | Windows 11, Windows 10, Windows 8.1, Windows 7 (x86 e x64) |

---

## 🏗️ Visão Geral da Arquitetura

```
 ┌────────────────────────────────────────────────────────┐
 │           Sua Aplicação (Frontend / Backend)           │
 │   Web (React/Vue)  │  Electron (Veritas)  │  Python    │
 └───────────────────────────┬────────────────────────────┘
                             │ HTTP JSON / CORS (:8080)
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │       OpenBioMini.Bridge.exe (REST / Named Pipe)       │
 │           Servidor HTTP ultraleve em segundo plano     │
 └───────────────────────────┬────────────────────────────┘
                             │ Interop Gerenciado (.NET)
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │         OpenBioMini.Core (Motor Wrapper em C#)         │
 └─────────────┬────────────────────────────┬─────────────┘
               │                            │
               ▼                            ▼
 ┌───────────────────────────┐ ┌──────────────────────────┐
 │     UFScanner.dll         │ │      UFMatcher.dll       │
 │ (Captura Óptica com Patch)│ │ (Verificação 1:1 / 1:N)  │
 └─────────────┬─────────────┘ └──────────────────────────┘
               │
               ▼
 ┌────────────────────────────────────────────────────────┐
 │        Driver PnP de Kernel do Windows (SFRUSB.sys)    │
 └───────────────────────────┬────────────────────────────┘
                             │ USB Bulk Endpoint
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │          Hardware Suprema BioMini (PID 0400)           │
 └────────────────────────────────────────────────────────┘
```

---

## 🚀 Instalação Automatizada (Assistente WPF)

A forma mais simples e recomendada de instalar o OpenBioMini é através do instalador unificado:

1. Baixe ou execute `Setup-OpenBioMini-v1.0.3.exe` (localizado na pasta `dist/` ou nos Releases).
2. Execute como Administrador.
3. O assistente visual guiará você por:
   - **Termos da Licença MIT**
   - **Pasta de Instalação** (padrão: `C:\Program Files\OpenBioMini`)
   - **Seleção de Módulos:**
     - `Driver USB PnP Oficial`: Registro automático do driver via `pnputil.exe`.
     - `OpenBioMini CLI`: Utilitário de linha de comando `biomini.exe`.
     - `REST API & WebSocket Bridge`: Serviço local na porta `:8080`.
     - `Adaptador WBF para Windows Hello`: Adaptador biométrico experimental.
     - `SDK e Documentação`: Wrappers C#, headers C/C++ e guias técnicos.
     - `Adicionar ao PATH`: Permite chamar `biomini` de qualquer terminal.
4. Clique em **🚀 Instalar** e o leitor estará imediatamente pronto para uso!

---

## 💻 Componentes e Como Usar

### 1. Ferramenta de Linha de Comando (`biomini.exe`)

Permite testar o leitor, capturar imagens e verificar digitais diretamente pelo terminal:

```bash
# 1. Verificar se o leitor físico está conectado
biomini status

# 2. Disparar captura óptica (acende o LED e salva a imagem PNG + template)
biomini scan -o digital.png

# 3. Comparar dois templates biométricos (Verificação 1:1)
biomini match template1.b64 template2.b64
```

**Exemplo de Saída no Terminal:**
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

Inicie o `OpenBioMini.Bridge.exe` para expor o leitor como um microserviço HTTP local:

#### Exemplo em JavaScript / Web / Electron:

```javascript
// 1. Verificar o status do leitor
const status = await fetch('http://localhost:8080/api/status').then(r => r.json());
console.log('Status do Leitor:', status);
// Retorno: { connected: true, model: "SFR300v2", serial: "hrBioMini..." }

// 2. Disparar a captura óptica da digital
const capture = await fetch('http://localhost:8080/api/scan', { method: 'POST' }).then(r => r.json());

if (capture.success) {
  // Exibir a imagem diretamente numa tag <img> HTML
  document.getElementById('fotoDigital').src = `data:image/png;base64,${capture.imageBase64}`;
  
  // Salvar o template no banco de dados para autenticação futura
  console.log('Template Base64:', capture.template);
  console.log('Qualidade da Captura:', capture.quality + '%');
}

// 3. Comparar o dedo capturado com um template cadastrado no banco (Match 1:1)
const matchResult = await fetch('http://localhost:8080/api/match', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    templateA: templateSalvoNoBanco,
    templateB: capture.template
  })
}).then(r => r.json());

console.log('Digital confere?', matchResult.match); // true ou false
```

---

### 3. SDK Nativo em C# (`OpenBioMini.Core`)

Para aplicações desktop .NET (WPF, WinForms, Avalonia), referencie `OpenBioMini.Core.dll` diretamente:

```csharp
using OpenBioMini;

using (var controller = new BioMiniController()) {
    if (!controller.Initialize()) {
        Console.WriteLine("Leitor não encontrado. Verifique a conexão USB.");
        return;
    }

    Console.WriteLine($"Conectado ao leitor {controller.ScannerModel} (Serial: {controller.ScannerSerial})");

    // Capturar digital com timeout de 5 segundos
    ScanResult result = controller.Capture(timeoutMs: 5000);
    if (result.Success) {
        // Salvar imagem no disco
        File.WriteAllBytes("digital_capturada.png", result.ImageBytes);
        
        // Obter template para autenticação
        string templateBase64 = result.TemplateBase64;
        Console.WriteLine($"Captura concluída com sucesso! Qualidade: {result.QualityScore}%");
    }
}
```

---

### 4. Integração Direta em Python

Perfeito para scripts de automação, backends em Flask/FastAPI/Django ou processamento com IA:

```python
import requests
import base64

BRIDGE_URL = "http://localhost:8080/api"

def capturar_digital():
    # 1. Verifica se o leitor está pronto
    status = requests.get(f"{BRIDGE_URL}/status").json()
    if not status.get("connected"):
        print("Leitor biométrico desconectado!")
        return None

    print(f"Posicione o dedo no sensor {status['model']}...")
    
    # 2. Dispara a captura
    scan = requests.post(f"{BRIDGE_URL}/scan").json()
    if scan.get("success"):
        # Decodifica e salva a imagem PNG da digital
        img_bytes = base64.b64decode(scan["imageBase64"])
        with open("digital.png", "wb") as f:
            f.write(img_bytes)
        
        print(f"Digital capturada! Qualidade: {scan['quality']}%")
        return scan["template"]
    else:
        print("Falha na captura:", scan.get("error"))
        return None

if __name__ == "__main__":
    template = capturar_digital()
```

---

### 5. Adaptador Windows Hello WBF (Experimental)

O arquivo `wbf/BioMiniSensorAdapter.dll` é um sensor adapter em C++ x64 que implementa o Windows Biometric Framework (`WINBIO_SENSOR_INTERFACE` e `WINBIO_ENGINE_INTERFACE`).

* **Status:** Compilado com a diretiva `/INTEGRITYCHECK` e registrado sob o serviço `WbioSrvc`.
* **Nota de Compatibilidade (Windows 11):** O Windows 11 com UEFI Secure Boot ativo restringe drivers na tela de bloqueio do Windows Hello apenas a binários com assinatura WHQL da Microsoft. Para softwares próprios, recomendamos o uso da **REST Bridge** ou do **OpenBioMini.Core**, que funcionam perfeitamente sem restrições de assinatura de kernel.

---

## 📡 Referência Completa da API REST

| Endpoint | Método | Payload da Requisição | Resposta JSON | Descrição |
|---|---|---|---|---|
| `/api/status` | `GET` | *Nenhum* | `{"connected": bool, "model": string, "serial": string}` | Verifica se o leitor está conectado e retorna modelo/serial. |
| `/api/scan` | `POST` | *Nenhum* | `{"success": bool, "quality": int, "template": string, "imageBase64": string}` | Acende o LED, aguarda o dedo, captura a digital e extrai o template. |
| `/api/match` | `POST` | `{"templateA": string, "templateB": string}` | `{"match": bool, "score": int}` | Compara dois templates Base64 e retorna se pertencem à mesma pessoa. |

---

## 🔍 Códigos de Erro da Suprema e Resolução de Problemas

| Código de Erro | Nome da Constante | Significado e Resolução |
|---|---|---|
| `0` | `UFS_OK` | Operação executada com sucesso. |
| `-1` | `UFS_ERR_NOT_INITIALIZED` | O leitor não foi inicializado antes da captura. |
| `-2` | `UFS_ERR_ALREADY_INITIALIZED` | O leitor já está aberto por outro processo ou thread. |
| `-101` | `UFS_ERR_NO_LICENSE` | Falha de licença. Resolvido pelo Core com patch do OpenBioMini. |
| `-201` | `UFS_ERR_CANNOT_EXTRACT` | Dedo muito seco, sujo ou movido durante a varredura óptica. |
| `0x80070002` | `ERROR_FILE_NOT_FOUND` | DLLs nativas ausentes no diretório (`UFScanner.dll` / `UFMatcher.dll`). |

---

## 🔬 Engenharia Reversa e Patch de Hardware

Para entender o processo de engenharia reversa, análise em desassemblador e neutralização do bloqueio de `Vendor ID is mismatched`, consulte:
* [`docs/REVERSE_ENGINEERING.md`](docs/REVERSE_ENGINEERING.md)
* [`docs/WBF_DRIVER_SPEC.md`](docs/WBF_DRIVER_SPEC.md)

---

## 📄 Licença e Isenção de Responsabilidade

* **Código-Fonte e Wrappers:** Licenciados sob a **[Licença MIT](LICENSE)**.
* **Isenção Legal:** Este projeto é uma iniciativa independente de código aberto da comunidade para fins de interoperabilidade, pesquisa e preservação de hardware. Suprema e BioMini são marcas registradas de seus respectivos proprietários.

---

## 👤 Autor e Créditos

Desenvolvido e mantido com ❤️ por **Geovanni Honorato**
* 🐙 **GitHub:** [@geohonorato](https://github.com/geohonorato)
* 📦 **Repositório:** [geohonorato/open-biomini](https://github.com/geohonorato/open-biomini)
* 📸 **Instagram:** [@geovannihonorato](https://www.instagram.com/geovannihonorato/)
