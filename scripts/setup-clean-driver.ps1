# ==============================================================================
# SCRIPT DE NORMALIZAÇÃO E LIMPEZA PROFUNDA DE DRIVER PNP — SUPREMA BIOMINI
# ==============================================================================

# 1. Garante privilégios de Administrador
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] Elevando privilégios para Administrador..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "🛡️  RESTAURAÇÃO & CONFIGURAÇÃO LIMPA DE DRIVER PNP" -ForegroundColor Cyan
Write-Host "   Suprema BioMini (USB\VID_16D1&PID_0400)" -ForegroundColor Cyan
Write-Host "==================================================`n" -ForegroundColor Cyan

# 2. Finaliza processos conflitantes
Write-Host "[*] Finalizando processos do BioMini..." -ForegroundColor Gray
Get-Process | Where-Object { $_.ProcessName -like "*BioMini*" -or $_.ProcessName -like "*test_*" } | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Limpeza de resíduos de registro (travas WBF, Exclusive=1, SDDL)
Write-Host "[*] Limpando travas e parâmetros residuais do registro..." -ForegroundColor Gray
$regRoot = "HKLM:\SYSTEM\CurrentControlSet\Enum\USB\VID_16D1&PID_0400"

if (Test-Path $regRoot) {
    Get-ChildItem $regRoot | ForEach-Object {
        $devParam = Join-Path $_.PSPath "Device Parameters"
        if (Test-Path $devParam) {
            Remove-ItemProperty -Path $devParam -Name "DeviceInterfaceGUIDs" -ErrorAction SilentlyContinue
            Write-Host "    -> Chaves de interface antigas limpas em: $($_.PSChildName)" -ForegroundColor Green
        }
        Remove-ItemProperty -Path $_.PSPath -Name "Exclusive" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $_.PSPath -Name "Security" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $_.PSPath -Name "DeviceCharacteristics" -ErrorAction SilentlyContinue
    }
}

# 4. Reinstalação limpa do driver oficial SFRUSB (sfr.inf)
$infPath = Join-Path $PSScriptRoot "..\installer\payload\driver\sfr.inf"
if (Test-Path $infPath) {
    Write-Host "[*] Instalando driver oficial SFRUSB da Suprema via pnputil..." -ForegroundColor Gray
    $res = pnputil.exe /add-driver "$infPath" /install
    Write-Host "    $res" -ForegroundColor DarkGray
}

# 5. Re-escaneamento do barramento USB (Plug'n'Play)
Write-Host "[*] Re-escaneando barramento USB para atualização de hardware..." -ForegroundColor Gray
pnputil.exe /scan-devices | Out-Null
Start-Sleep -Milliseconds 800

# 6. Verificação do dispositivo
$dev = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like "*16D1*" }
if ($dev) {
    Write-Host "`n[✓] DISPOSITIVO SUPREMA BIOMINI RECONHECIDO!" -ForegroundColor Green
    Write-Host "    Nome: $($dev.FriendlyName)" -ForegroundColor Green
    Write-Host "    Status: $($dev.Status)" -ForegroundColor Green
    Write-Host "    Instância: $($dev.InstanceId)" -ForegroundColor Green
} else {
    Write-Host "`n[⏳ AVISO] Leitor desconectado. Conecte o cabo USB do BioMini para concluir a validação." -ForegroundColor Yellow
}

Write-Host "`n[✓] Configuração PnP normalizada com sucesso. Pressione qualquer tecla para sair..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
