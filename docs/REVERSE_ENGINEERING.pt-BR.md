# 🔬 Engenharia Reversa do BioMini SDK (UFScanner.dll)

Este documento descreve detalhadamente o processo de análise binária e engenharia reversa realizado na biblioteca `UFScanner.dll` para neutralizar a trava de validação de **Vendor ID / Licença OEM** do Suprema BioMini.

---

## 1. O Problema: `Vendor ID is mismatched`

Ao inicializar o SDK da Suprema via `UFScannerManager.Init()` ou `UFS_Init()`, o subsistema realiza a leitura do arquivo `UFLicense.dat` presente na pasta de execução.

Quando um leitor BioMini padrão (ou de um OEM específico) é utilizado com um arquivo de licença gerado para outro integrador, o SDK aborta a inicialização com uma caixa de diálogo nativa do Windows:

```text
MessageBoxA: "Vendor ID is mismatched." (Title: "License")
```

Isso inutilizava milhões de leitores de 1ª geração quando os portais da Suprema/Xperix foram descontinuados ou restritos.

---

## 2. Análise do Desmonte x86

Analisando a seção `.text` da biblioteca `UFScanner.dll` de 32-bit (ImageBase `0x10000000`):

### Rotina de Validação de Vendor ID (Offset `0xA300`):
```assembly
.text:1000A300  8B 44 24 04          mov     eax, [esp+4]     ; Carrega ponteiro do buffer do Vendor ID
.text:1000A304  56                   push    esi
.text:1000A305  8B 74 24 0C          mov     esi, [esp+12]    ; Carrega ponteiro dos dados esperados
.text:1000A309  33 C9                xor     ecx, ecx
.text:1000A30B  0F BE 10             movsx   edx, byte ptr [eax]
.text:1000A30E  8A 0E                mov     cl, [esi]
.text:1000A310  3B CA                cmp     ecx, edx
.text:1000A312  75 0F                jnz     loc_1000A323
...
.text:1000A336  B8 01 00 00 00       mov     eax, 1           ; SUCESSO (Retorna 1)
.text:1000A33B  5E                   pop     esi
.text:1000A33C  C3                   ret
.text:1000A33D  6A 10                push    10h              ; MB_ICONHAND
.text:1000A33F  68 98 F6 11 10       push    offset aLicense  ; "License"
.text:1000A344  68 50 F9 11 10       push    offset aVendorId ; "Vendor ID is mismatched."
.text:1000A349  6A 00                push    0                ; hWnd
.text:1000A34B  FF 15 14 F3 02 10    call    ds:MessageBoxA
.text:1000A351  33 C0                xor     eax, eax         ; FALHA (Retorna 0)
.text:1000A353  5E                   pop     esi
.text:1000A354  C3                   ret
```

Quatro pontos de chamada no código (`0xAD96`, `0xB0B2`, `0xB3CA` e `0xB6D7`) invocam essa rotina para cada variante de sensor (BioMini, BioMini Plus, BioMini Slim).

---

## 3. O Patch Aplicado

Para universalizar a biblioteca e permitir a inicialização imediata com qualquer hardware, substituímos o preâmbulo das rotinas em `0xA300` e `0xA360` por um retorno incondicional de sucesso:

```assembly
mov eax, 1   ; B8 01 00 00 00
ret          ; C3
```

### Bytes Hexadecimais Alterados:
* **Offset `0xA300`**: `B8 01 00 00 00 C3`
* **Offset `0xA360`**: `B8 01 00 00 00 C3`

### Resultado:
1. A rotina **sempre retorna `1` (Sucesso)**.
2. O alerta `MessageBoxA` é permanentemente neutralizado.
3. O leitor conecta, acende o sensor óptico e realiza extração/comparação com 100% de precisão de hardware.
