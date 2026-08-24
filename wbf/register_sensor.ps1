# Register-BioMiniVirtualSensor-v4.ps1
# Complete registration with Force Integrity signed DLL

$dbGuid = "E48D0813-CD19-4A9B-A08D-CF28189D2278"
$sensorGuid = "{B3F484B6-6B22-4D3B-983C-111122223333}"
$wbfDir = "C:\Users\Geovanni\Documents\Hermes\open-biomini\wbf"

Write-Host "=== OpenBioMini WBF - Instalacao com Force Integrity ===" -ForegroundColor Cyan
Write-Host ""

# 1. Parar WbioSrvc
Write-Host "[1/5] Parando WbioSrvc..."
Stop-Service WbioSrvc -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Write-Host "  -> OK" -ForegroundColor Green

# 2. Copiar DLL assinada com Force Integrity
Write-Host ""
Write-Host "[2/5] Copiando BioMiniSensorAdapter.dll assinada para WinBioPlugIns..."
$dllSrc = Join-Path $wbfDir "BioMiniSensorAdapter.dll"
$dst = "C:\Windows\System32\WinBioPlugIns\BioMiniSensorAdapter.dll"

try {
    if (Test-Path $dst) {
        takeown /f "$dst" /a 2>&1 | Out-Null
        icacls "$dst" /grant Administrators:F 2>&1 | Out-Null
    }
    Copy-Item $dllSrc $dst -Force -ErrorAction Stop
    Write-Host "  -> $dst OK" -ForegroundColor Green
} catch {
    Write-Host "  -> ERRO $dst : $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Criar Database de Impressao Digital
Write-Host ""
Write-Host "[3/5] Configurando Database de Impressao Digital..."
$dbKey = "HKLM:\SYSTEM\CurrentControlSet\Services\WbioSrvc\Databases\{$dbGuid}"
if (-not (Test-Path $dbKey)) { New-Item -Path $dbKey -Force | Out-Null }
Set-ItemProperty -Path $dbKey -Name "Attributes" -Value 1 -Type DWord
Set-ItemProperty -Path $dbKey -Name "AutoCreate" -Value 1 -Type DWord
Set-ItemProperty -Path $dbKey -Name "AutoName" -Value 0 -Type DWord
Set-ItemProperty -Path $dbKey -Name "BiometricType" -Value 8 -Type DWord
Set-ItemProperty -Path $dbKey -Name "ConnectionString" -Value "" -Type String
Set-ItemProperty -Path $dbKey -Name "Format" -Value "00000000-0000-0000-0000-000000000000" -Type String
Set-ItemProperty -Path $dbKey -Name "InitialSize" -Value 32 -Type DWord
Write-Host "  -> Database configurada" -ForegroundColor Green

# 4. Configurar Sensor e Engine
Write-Host ""
Write-Host "[4/5] Configurando Sensor e Engine no WbioSrvc..."
$sensorKey = "HKLM:\SYSTEM\CurrentControlSet\Services\WbioSrvc\Service Providers\Fingerprint\Virtual Sensors\$sensorGuid"
if (-not (Test-Path $sensorKey)) { New-Item -Path $sensorKey -Force | Out-Null }

Set-ItemProperty -Path $sensorKey -Name "DeviceDescription" -Value "Suprema BioMini Fingerprint Sensor" -Type String
Set-ItemProperty -Path $sensorKey -Name "Manufacturer" -Value "Suprema Inc." -Type String
Set-ItemProperty -Path $sensorKey -Name "ModelName" -Value "BioMini SFR300v2" -Type String
Set-ItemProperty -Path $sensorKey -Name "SerialNumber" -Value "hrBioMini001" -Type String
Set-ItemProperty -Path $sensorKey -Name "Capabilities" -Value 129 -Type DWord
Set-ItemProperty -Path $sensorKey -Name "SubType" -Value 2 -Type DWord
Set-ItemProperty -Path $sensorKey -Name "Version" -Value 144115188092633088 -Type QWord

$cfgKey = "$sensorKey\Configurations"
if (-not (Test-Path $cfgKey)) { New-Item -Path $cfgKey -Force | Out-Null }
Set-ItemProperty -Path $cfgKey -Name "DefaultConfiguration" -Value 0 -Type DWord

$cfg0Key = "$cfgKey\0"
if (-not (Test-Path $cfg0Key)) { New-Item -Path $cfg0Key -Force | Out-Null }
Set-ItemProperty -Path $cfg0Key -Name "SensorAdapterBinary" -Value "BioMiniSensorAdapter.dll" -Type String
Set-ItemProperty -Path $cfg0Key -Name "EngineAdapterBinary" -Value "BioMiniSensorAdapter.dll" -Type String
Set-ItemProperty -Path $cfg0Key -Name "StorageAdapterBinary" -Value "winbiostorageadapter.dll" -Type String
Set-ItemProperty -Path $cfg0Key -Name "DatabaseId" -Value $dbGuid -Type String
Set-ItemProperty -Path $cfg0Key -Name "SensorMode" -Value 1 -Type DWord
Set-ItemProperty -Path $cfg0Key -Name "SystemSensor" -Value 1 -Type DWord
Write-Host "  -> Sensor e Engine registrados" -ForegroundColor Green

# 5. Iniciar servico
Write-Host ""
Write-Host "[5/5] Iniciando WbioSrvc..."
Start-Service WbioSrvc -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
Write-Host "  -> OK" -ForegroundColor Green

# Status dos logs
Write-Host ""
Write-Host "=== STATUS DO WINDOWS BIOMETRIC SERVICE ===" -ForegroundColor Cyan
$logs = Get-WinEvent -LogName "Microsoft-Windows-Biometrics/Operational" -MaxEvents 6 -ErrorAction SilentlyContinue
foreach ($l in $logs) {
    Write-Host "[$($l.TimeCreated.ToString('HH:mm:ss'))] $($l.Message)"
}

Write-Host ""
Write-Host "Pressione qualquer tecla para fechar..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
