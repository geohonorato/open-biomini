# 🔌 Guia de Instalação de Drivers — Suprema BioMini

Este documento explica como o driver de kernel PnP é instalado para o **Suprema BioMini (1ª Geração, PID 0400)** no Windows 10 e Windows 11.

---

## 1. Como Funciona (A Realidade Técnica)

O leitor clássico Suprema BioMini (`USB\VID_16D1&PID_0400`) utiliza o driver de kernel oficial assinado da Suprema (`SFR.inf` / `SFR500.sys` / `sfr500.cat`).

### Instalação Automatizada (Recomendada)
Ao utilizar o assistente gráfico **OpenBioMini Setup (`Setup-OpenBioMini-v1.0.3.exe`)**, o instalador executa automaticamente o utilitário nativo do Windows:

```cmd
pnputil.exe /add-driver "driver\SFR.inf" /install
```

Isso extrai e registra o driver assinado diretamente no repositório de drivers do sistema (`C:\Windows\System32\DriverStore\FileRepository\`), sem necessidade de procurar arquivos ou clicar com botão direito em arquivos `.inf`.

---

## 2. Instalação Manual via Terminal

Caso deseje instalar o driver manualmente pelo terminal:

1. Abra o PowerShell ou Prompt de Comando como **Administrador**.
2. Navegue até a pasta `driver/` do repositório.
3. Execute o comando:

```powershell
pnputil /add-driver "SFR.inf" /install
```

### Verificação do Status do Driver

Para verificar se o leitor foi reconhecido corretamente:

```powershell
Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like "USB\VID_16D1*" } | Format-List FriendlyName, Status, ProblemCode
```

**Resultado esperado:**
```text
FriendlyName : Suprema Fingerprint Scanner
Status       : OK
ProblemCode  : CM_PROB_NONE
```

---

## 3. Fatos e Limitações Importantes

* **Dispositivo USB PnP:** O hardware conecta-se como dispositivo USB de transferência por pacotes bulk (`Class GUID: {a5dcbf10-6530-11d2-901f-00c04fb951ed}`).
* **Sem Porta COM:** O leitor não cria porta serial virtual COM. Toda comunicação óptica ocorre via bulk USB gerenciada pela DLL `UFScanner.dll` com patch.
* **Windows Hello:** A instalação do driver habilita o hardware para 100% dos softwares customizados (CLI, Bridge REST, Web, Python, C#). A integração com a tela de bloqueio do Windows Hello é um item experimental de P&D devido às exigências de assinatura WHQL da Microsoft no Windows 11 (consulte `docs/WBF_DRIVER_SPEC.pt-BR.md`).
