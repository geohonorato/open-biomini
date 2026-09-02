#
# install_wbf.ps1
# Script de instalacao e registro do WBF Sensor Adapter para Windows Hello
#

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "🛡️ INSTALADOR WBF SENSOR ADAPTER (WINDOWS HELLO)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Copia a DLL do Sensor Adapter para o diretorio do sistema
$sysDir = "$env:windir\System32\WinBioPlugins"
if (!(Test-Path $sysDir)) {
    New-Item -ItemType Directory -Force -Path $sysDir | Out-Null
}

$adapterSrc = "$PSScriptRoot\BioMiniSensorAdapter.dll"
Copy-Item $adapterSrc "$sysDir\BioMiniSensorAdapter.dll" -Force
Write-Host "[✓] BioMiniSensorAdapter.dll instalada em $sysDir" -ForegroundColor Green

# 2. Registra o Sensor Adapter no Windows Biometric Service (Registry)
$guid = "{B3F484B6-6B22-4D3B-983C-111122223333}"
$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WinBio\SensorAdapters\$guid"
if (!(Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
}

Set-ItemProperty -Path $regPath -Name "SensorAdapterBinary" -Value "BioMiniSensorAdapter.dll" -Type String
Set-ItemProperty -Path $regPath -Name "Vendor" -Value "Suprema Inc." -Type String
Set-ItemProperty -Path $regPath -Name "Description" -Value "Suprema BioMini WBF Sensor Adapter" -Type String
Write-Host "[✓] Registro WBF configurado em $regPath" -ForegroundColor Green

# 3. Reinicia o servico Windows Biometric Service (WbioSrvc)
Write-Host "[*] Reiniciando Windows Biometric Service..." -ForegroundColor Yellow
try {
    Restart-Service -Name "WbioSrvc" -Force -ErrorAction SilentlyContinue
    Write-Host "[✓] Windows Biometric Service reiniciado com sucesso!" -ForegroundColor Green
} catch {
    Write-Host "[!] Nao foi possivel reiniciar WbioSrvc diretamente (pode exigir permissao de Administrador)." -ForegroundColor Yellow
}

Write-Host "`n>>> WBF SENSOR ADAPTER INSTALADO COM SUCESSO!" -ForegroundColor Cyan
Write-Host ">>> Certifique-se de que o 'OpenBioMini.Bridge.exe' esteja rodando em segundo plano para atender as requisicoes do Windows Hello." -ForegroundColor White
