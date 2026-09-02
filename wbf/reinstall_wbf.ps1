# OpenBioMini WBF Reinstall Script
# Run as Administrator

try {
    $ErrorActionPreference = 'Continue'
    $wbfDir = "C:\Users\Geovanni\Documents\Hermes\open-biomini\wbf"
    $thumbprint = "E44BC835F9B48D2CE9D911DDB8DFE739C030F382"

    Write-Host "=== OpenBioMini WBF Reinstalacao ===" -ForegroundColor Cyan
    Write-Host ""

    # 1. Parar WbioSrvc
    Write-Host "[1/6] Parando servico biometrico..."
    Stop-Service WbioSrvc -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "  -> OK" -ForegroundColor Green

    # 2. Remover oem*.inf antigos
    Write-Host ""
    Write-Host "[2/6] Removendo drivers WBF antigos..."
    $driverList = pnputil /enum-drivers 2>&1 | Out-String
    $lines = $driverList -split "`r?`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "OpenBioMini|biomini_wbf") {
            for ($j = [Math]::Max(0, $i - 5); $j -lt $i; $j++) {
                if ($lines[$j] -match "(oem\d+\.inf)") {
                    $oemName = $Matches[1].Trim()
                    Write-Host "  -> Removendo $oemName..."
                    pnputil /delete-driver $oemName /force 2>&1 | Out-Null
                }
            }
        }
    }
    Write-Host "  -> OK" -ForegroundColor Green

    # 3. Copiar DLL
    Write-Host ""
    Write-Host "[3/6] Copiando DLLs..."
    $dllSrc = Join-Path $wbfDir "BioMiniSensorAdapter.dll"
    $dest1 = "C:\Windows\System32\BioMiniSensorAdapter.dll"
    $dest2 = "C:\Windows\System32\WinBioDatabase\BioMiniSensorAdapter.dll"

    foreach ($dst in @($dest1, $dest2)) {
        try {
            if (Test-Path $dst) {
                takeown /f "$dst" /a 2>&1 | Out-Null
                icacls "$dst" /grant Administrators:F 2>&1 | Out-Null
            }
            Copy-Item $dllSrc $dst -Force -ErrorAction Stop
            Write-Host "  -> $dst OK" -ForegroundColor Green
        } catch {
            Write-Host "  -> AVISO $dst : $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    # 4. Catalogo e assinatura
    Write-Host ""
    Write-Host "[4/6] Gerando e assinando catalogo..."
    $catPath = Join-Path $wbfDir "biomini_wbf.cat"
    if (Test-Path $catPath) { Remove-Item $catPath -Force }
    try {
        New-FileCatalog -Path $wbfDir -CatalogFilePath $catPath -CatalogVersion 2.0 -ErrorAction Stop
        Write-Host "  -> Catalogo gerado" -ForegroundColor Green
    } catch {
        Write-Host "  -> AVISO Catalogo: $_" -ForegroundColor Yellow
    }

    $signtoolPaths = @(
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe",
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe",
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe"
    )
    $signtool = $null
    foreach ($p in $signtoolPaths) { if (Test-Path $p) { $signtool = $p; break } }

    if ($signtool -and (Test-Path $catPath)) {
        & $signtool sign /sha1 $thumbprint /fd SHA256 "$catPath" 2>$null
        Write-Host "  -> Catalogo assinado" -ForegroundColor Green
    }

    # 5. Instalar driver
    Write-Host ""
    Write-Host "[5/6] Instalando biomini_wbf.inf..."
    $infPath = Join-Path $wbfDir "biomini_wbf.inf"
    pnputil /add-driver "$infPath" /install 2>&1 | ForEach-Object { Write-Host "  $_" }

    # 6. Iniciar WbioSrvc
    Write-Host ""
    Write-Host "[6/6] Iniciando WbioSrvc..."
    Start-Service WbioSrvc -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    Write-Host "  -> OK" -ForegroundColor Green

    # Status
    Write-Host ""
    Write-Host "=== RESULTADO ===" -ForegroundColor Cyan
    $dev = Get-PnpDevice -Class Biometric -PresentOnly -ErrorAction SilentlyContinue
    if ($dev) {
        foreach ($d in $dev) {
            Write-Host "  Dispositivo : $($d.FriendlyName)"
            Write-Host "  Status      : $($d.Status)"
            Write-Host "  ProblemCode : $($d.Problem)"
        }
    } else {
        Write-Host "  Nenhum dispositivo na classe Biometric" -ForegroundColor Red
    }

} catch {
    Write-Host "ERRO FATAL: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Pressione qualquer tecla para fechar..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
