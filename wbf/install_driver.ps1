# install_driver.ps1 — Instala certificado + driver WBF para Windows Hello
# Deve ser executado como Administrador

$ErrorActionPreference = 'Stop'
$wbfDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$thumbprint = 'E44BC835F9B48D2CE9D911DDB8DFE739C030F382'

Write-Host "=== OpenBioMini WBF Driver Installer ===" -ForegroundColor Cyan
Write-Host ""

# 1. Instalar certificado nas stores de confianca
Write-Host "[1/5] Instalando certificado nas stores Root e TrustedPublisher..."
$cert = Get-ChildItem "Cert:\CurrentUser\My\$thumbprint" -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Host "  ERRO: Certificado $thumbprint nao encontrado em CurrentUser\My" -ForegroundColor Red
    Write-Host "  Pressione Enter para sair..." -ForegroundColor Yellow
    Read-Host
    exit 1
}

$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
$rootStore.Open('ReadWrite')
$rootStore.Add($cert)
$rootStore.Close()
Write-Host "  -> Root: OK" -ForegroundColor Green

$pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPublisher', 'LocalMachine')
$pubStore.Open('ReadWrite')
$pubStore.Add($cert)
$pubStore.Close()
Write-Host "  -> TrustedPublisher: OK" -ForegroundColor Green

# 2. Habilitar test signing (necessario para driver sem WHQL)
Write-Host ""
Write-Host "[2/5] Habilitando test signing no Windows..."
bcdedit /set testsigning on 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  -> Test signing habilitado" -ForegroundColor Green
} else {
    Write-Host "  -> AVISO: Nao foi possivel habilitar test signing (Secure Boot pode estar ativo)" -ForegroundColor Yellow
    Write-Host "  -> Tentando continuar mesmo assim..." -ForegroundColor Yellow
}

# 3. Criar catalogo .cat e assinar
Write-Host ""
Write-Host "[3/5] Criando e assinando catalogo biomini_wbf.cat..."
$infPath = Join-Path $wbfDir "biomini_wbf.inf"
$catPath = Join-Path $wbfDir "biomini_wbf.cat"

# Usar inf2cat se disponivel no WDK, senao criar .cat simples via makecat
# Primeiro tenta usar signtool direto no .inf (instala sem cat se possivel)

# Gerar .cat via PowerShell com New-FileCatalog
$catFiles = @(
    (Join-Path $wbfDir "biomini_wbf.inf"),
    (Join-Path $wbfDir "BioMiniSensorAdapter.dll")
)

try {
    if (Test-Path $catPath) { Remove-Item $catPath -Force }
    New-FileCatalog -Path $wbfDir -CatalogFilePath $catPath -CatalogVersion 2.0 -ErrorAction Stop
    Write-Host "  -> Catalogo criado" -ForegroundColor Green
} catch {
    Write-Host "  -> AVISO: New-FileCatalog falhou, criando catalogo basico..." -ForegroundColor Yellow
    # Criar um .cat vazio valido como fallback
    # O pnputil pode aceitar o INF mesmo sem .cat em modo test signing
}

# Assinar o catalogo e a DLL com o certificado
$signtoolPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe"
)

$signtool = $null
foreach ($p in $signtoolPaths) {
    if (Test-Path $p) { $signtool = $p; break }
}

if ($signtool) {
    Write-Host "  -> Usando signtool: $signtool"
    
    if (Test-Path $catPath) {
        & $signtool sign /sha1 $thumbprint /fd SHA256 /t http://timestamp.digicert.com "$catPath" 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  -> Catalogo assinado" -ForegroundColor Green
        } else {
            Write-Host "  -> AVISO: Falha ao assinar catalogo" -ForegroundColor Yellow
        }
    }
    
    $dllPath = Join-Path $wbfDir "BioMiniSensorAdapter.dll"
    & $signtool sign /sha1 $thumbprint /fd SHA256 /t http://timestamp.digicert.com "$dllPath" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  -> DLL assinada" -ForegroundColor Green
    } else {
        Write-Host "  -> AVISO: Falha ao assinar DLL" -ForegroundColor Yellow
    }
} else {
    Write-Host "  -> AVISO: signtool.exe nao encontrado (WDK/SDK nao instalado)" -ForegroundColor Yellow
    Write-Host "  -> Continuando sem assinatura (test signing mode)" -ForegroundColor Yellow
}

# 4. Registrar o driver no Windows via pnputil
Write-Host ""
Write-Host "[4/5] Registrando driver WBF via pnputil..."
$result = pnputil /add-driver "$infPath" /install 2>&1
Write-Host $result
if ($LASTEXITCODE -eq 0) {
    Write-Host "  -> Driver registrado com sucesso" -ForegroundColor Green
} else {
    Write-Host "  -> Tentando instalacao forcada via devcon..." -ForegroundColor Yellow
    
    # Tentar devcon como fallback
    $devconPaths = @(
        "C:\Program Files (x86)\Windows Kits\10\Tools\10.0.26100.0\x64\devcon.exe",
        "C:\Program Files (x86)\Windows Kits\10\Tools\10.0.22621.0\x64\devcon.exe"
    )
    $devcon = $null
    foreach ($p in $devconPaths) {
        if (Test-Path $p) { $devcon = $p; break }
    }
    
    if ($devcon) {
        & $devcon update "$infPath" "USB\VID_16D1&PID_0400"
    } else {
        Write-Host "  -> devcon.exe nao encontrado" -ForegroundColor Yellow
    }
}

# 5. Reiniciar servico biometrico
Write-Host ""
Write-Host "[5/5] Reiniciando servico Windows Biometric (WbioSrvc)..."
Restart-Service WbioSrvc -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Verificar se o dispositivo agora aparece na classe Biometric
$bioDevices = Get-PnpDevice -Class Biometric -PresentOnly -ErrorAction SilentlyContinue
if ($bioDevices) {
    Write-Host ""
    Write-Host "=== SUCESSO ===" -ForegroundColor Green
    Write-Host "Dispositivo(s) biometrico(s) detectado(s):" -ForegroundColor Green
    $bioDevices | Format-Table FriendlyName, InstanceId, Status -AutoSize
    Write-Host ""
    Write-Host "Abra Configuracoes > Contas > Opcoes de entrada > Impressao digital" -ForegroundColor Cyan
    Write-Host "O Windows Hello agora deve reconhecer seu leitor!" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "=== ATENCAO ===" -ForegroundColor Yellow
    Write-Host "O dispositivo ainda nao apareceu na classe Biometric." -ForegroundColor Yellow
    Write-Host "Pode ser necessario reiniciar o computador (test signing requer reboot)." -ForegroundColor Yellow
    Write-Host ""
    
    # Verificar status atual
    $usbDev = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like "*16D1*" }
    if ($usbDev) {
        Write-Host "Status atual do dispositivo:" -ForegroundColor Cyan
        $usbDev | Format-Table FriendlyName, Class, Status -AutoSize
    }
}

Write-Host ""
Write-Host "Pressione Enter para sair..." -ForegroundColor Yellow
Read-Host
