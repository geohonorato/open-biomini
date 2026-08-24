# OpenBioMini

> Integração universal e sem driver adicional para o leitor biométrico **Suprema BioMini** (legado) no Windows moderno.

**Status:** 🧪 Alpha — engenharia reversa validada, core nativo funcionando (captura / cadastro / verificação 1:N em hardware real)

[English version](README.md)

---

## O problema

O **Suprema BioMini** (clássico, `VID_16D1 & PID_0400`) foi um dos leitores USB de impressão digital mais vendidos do planeta — usado em massa em **cartórios, bancos, Detrans, universidades, academias e sistemas de ponto eletrônico** (principalmente no Brasil). Quando a Suprema descontinuou a 1ª geração e a transição para a Xperix trancou tudo:

- O SDK antigo (v2.x) sumiu da internet.
- Os pacotes que sobraram retornam `Vendor ID is mismatched` ou erro `101 (No license)`.
- O SDK moderno (3.9.x/3.10.x, x64) **removeu silenciosamente o suporte ao PID 0400** — `Init()`/`Update()` retornam OK, mas `GetScannerNumber()` devolve 0.
- Não existe integração simples para aplicações web modernas e o leitor não tem suporte a Windows Hello.

Resultado: **milhões de leitores ópticos funcionando viraram lixo eletrônico** por causa de uma trava de licenciamento — não de falha de hardware.

**Este projeto resolve isso.** O leitor volta a funcionar 100% no Windows 10/11 — para apps desktop, CLI e web — com uma solução de engenharia documentada e reproduzível.

---

## A solução

```
┌────────────────────────────────────────────────────────────────────┐
│                        SUA APLICAÇÃO                               │
│        (C# / Node.js / Electron / React / Python / etc.)           │
└───────────────┬──────────────────────────────┬─────────────────────┘
                │                              │
        ┌───────▼────────┐            ┌────────▼────────┐
        │  OpenBioMini   │            │ OpenBioMini CLI │
        │  Bridge        │            │  scan/cadastrar/│
        │  (REST+WS)     │            │  verificar/     │
        └───────┬────────┘            │  excluir/listar │
                │                     └────────┬────────┘
                │                              │
        ┌───────▼──────────────────────────────▼───────┐
        │            OpenBioMini Core (C#)             │
        │       BioMiniController (wrapper gerenciado) │
        │  UFScanner.dll │ UFMatcher.dll │ UFLicense   │
        │        (x86 nativo, com patch de Vendor ID)  │
        └───────┬──────────────────────────────────────┘
                │  USB
        ┌───────▼────────┐
        │   SFRUSB.sys   │  (driver kernel oficial)
        │   BioMini       │  VID_16D1 / PID_0400
        └─────────────────┘
```

### Estrutura do repositório

```text
open-biomini/
├── core/                 # Wrapper C# gerenciado (BioMiniController) + DLLs nativas x86 patchadas
│   ├── BioMiniController.cs
│   └── native/           # UFScanner.dll, UFMatcher.dll, Suprema.*.dll, UFLicense.dat
├── bridge/               # Microserviço local (REST + WebSocket) para apps web/Electron
├── cli/                  # Ferramenta de linha de comando: scan / enroll / verify / delete
├── wbf-driver/           # [Experimental] Adaptador UMDF para Windows Hello
├── examples/             # Exemplos por stack
└── docs/                 # Notas da engenharia reversa, protocolo, documentação do driver
```

---

## Início rápido

### Requisitos

- Windows 10/11 x64
- Suprema BioMini clássico conectado via USB (`VID_16D1&PID_0400`) — driver `SFRUSB.sys` instalado (veja [docs/driver-install.md](docs/driver-install.md))
- .NET Framework 4.x (já vem no Windows) — nenhum SDK necessário para o core

### Compilar o Core

O core é um wrapper C# de arquivo único compilado com o `csc.exe` x86 do Windows:

```bat
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /target:library /platform:x86 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:core\native\Suprema.UFScanner.dll /r:core\native\Suprema.UFMatcher.dll ^
  /out:core\OpenBioMini.Core.dll core\BioMiniController.cs
```

> ⚠️ **x86 é obrigatório.** As DLLs nativas do SDK são 32-bit. Compile e rode sua aplicação como **x86** — processo x64 falha com `WinError 193`.

### Usar o Core (C#)

```csharp
using OpenBioMini;

using (var bio = new BioMiniController())
{
    if (!bio.Initialize())
    {
        Console.WriteLine("Nenhum BioMini detectado. Está conectado?");
        return;
    }

    Console.WriteLine($"Leitor: {bio.ScannerModel} | Serial: {bio.ScannerSerial}");

    // 1) Captura a digital (o LED do sensor acende)
    ScanResult scan = bio.Capture(6000);
    if (scan.Success)
    {
        Console.WriteLine($"Capturada! Template size={scan.TemplateSize}, qualidade={scan.Quality}");
        File.WriteAllText("digital.png.b64", scan.ImageBase64);
    }

    // 2) Verificação 1:1 contra um template cadastrado
    bool isMatch = bio.Verify(scan.Template, scan.TemplateSize, cadastrada, cadastradaSize);
    Console.WriteLine(isMatch ? "✅ Compatível" : "❌ Não compatível");
}
```

---

## Bridge (integração web/Electron)

A bridge roda um servidor local HTTP + WebSocket, então **qualquer** app web captura digitais reais com um simples `fetch`:

```javascript
const res = await fetch('http://localhost:8080/api/scan');
const { imageBase64, template, quality } = await res.json();
// imageBase64 -> <img src={`data:image/png;base64,${imageBase64}`}>
```

Endpoints (planejados):

| Método | Endpoint             | Descrição                                       |
|--------|----------------------|-------------------------------------------------|
| GET    | `/api/status`        | Leitor conectado? modelo + serial               |
| GET    | `/api/scan`          | Captura + extração de template (espera o dedo)  |
| POST   | `/api/verify`        | Verificação 1:1 `{ templateA, templateB }`      |
| GET    | `/api/ws`            | Stream WebSocket: imagem ao vivo do dedo       |
| POST   | `/api/enroll`        | Cadastro multi-scan `{ name }` → template salvo |

---

## CLI

```bat
biomini status
biomini scan --output digital.bmp
biomini enroll --name "Geovanni"
biomini verify --user "Geovanni"
biomini delete --user "Geovanni"
biomini list
```

---

## Notas da engenharia reversa (por que funciona)

### Identificação do hardware

| PID      | Produto                        | Driver kernel  |
|----------|--------------------------------|----------------|
| `0400`   | BioMini (clássico, Ver. 01)    | `SFRUSB.sys`   |
| `0401`   | BioMini loader                 | `SFRUSB.sys`   |
| `0402`   | BioMini Plus                   | `SFR500.sys`   |
| `0406`   | BioMini (Ver. 02)              | `SFR500.sys`   |
| `0407`   | BioMini/SFU Slim (S20)         | `SFR500.sys`   |
| `0408`   | BioMini/SFU Slim (S10)         | `SFR500.sys`   |
| `0409+`  | BioMini Plus 2 / Slim 2 / etc. | `SFR500.sys`   |

O leitor clássico é um device USB vendor-specific (classe `FF`). O driver kernel (`SFRUSB.sys`) está presente e saudável no Windows moderno — todo o bloqueio acontece **uma camada acima**, nas DLLs proprietárias do SDK em modo usuário.

### A trava de licenciamento

O SDK do BioMini (todas as versões anteriores à 3.5.5) valida um arquivo `UFLicense.dat` contra o Vendor ID do hardware. Arquivos de licença distribuídos por OEM disparam:

```
Vendor ID is mismatched
```

As builds x64 3.9.x/3.10.x removeram a checagem de licença, mas **também removeram o PID `0400`** da tabela de hardware — por isso `GetScannerNumber()` sempre retorna 0 para o leitor clássico.

### O patch universal

Na `UFScanner.dll` **x86** (~1,2 MB), duas rotinas de validação de licença são neutralizadas forçando retorno de sucesso:

| Offset   | Patch (6 bytes)       | Assembly          |
|----------|-----------------------|-------------------|
| `0xA300` | `b8 01 00 00 00 c3`   | `mov eax, 1; ret` |
| `0xA360` | `b8 01 00 00 00 c3`   | `mov eax, 1; ret` |

```python
data = bytearray(open('UFScanner.dll', 'rb').read())
for addr in (0xA300, 0xA360):
    data[addr:addr+6] = bytes.fromhex('b8 01 00 00 00 c3')
open('UFScanner.dll', 'wb').write(data)
```

A DLL patchada aceita **qualquer** hardware BioMini, independente do arquivo de licença OEM — essa é a parte "universal". Um backup íntegro é mantido como `UFScanner.dll.bak`.

### Por que o SDK oficial 3.9.1/3.10.0 x64 não funciona (resumo)

`Init()` e `Update()` retornam `OK`, mas a enumeração de scanners vem vazia porque o PID `0400` não está na tabela de hardware da DLL. Não existe flag de configuração que traga o suporte de volta — o leitor clássico foi removido da linha de produtos.

---

## Roadmap

- [x] Engenharia reversa e patch universal de Vendor ID (validado em hardware real)
- [x] Core nativo em C#: captura, cadastro, verificação 1:N, exclusão
- [ ] CLI (`scan` / `enroll` / `verify` / `delete` / `list`)
- [ ] Bridge (REST + WebSocket) para apps web/Electron
- [ ] Stream de imagem ao vivo via WebSocket
- [ ] `wbf-driver/`: adaptador UMDF para Windows Hello (experimental, ressalvas de assinatura)
- [ ] Gerenciadores de pacote (NuGet / npm / winget) e CI
- [ ] Docs: notas de protocolo para implementação 100% software (libusb)

---

---

## Autor

Desenvolvido por **Geovanni Honorato**
- GitHub: [@geohonorato](https://github.com/geohonorato)
- Projeto: [open-biomini](https://github.com/geohonorato/open-biomini)

---

*OpenBioMini — porque leitor biométrico funcionando não deveria virar lixo eletrônico.*
