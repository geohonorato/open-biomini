@echo off
title Veritas Biometria - Inicializador Completo
cd /d "%~dp0"
echo ==================================================
echo   VERITAS BIOMETRIA - SUPREMA BIOMINI & EPSON
echo ==================================================
echo.
echo [1/2] Iniciando Servidor Web (Porta 3300)...
start "Veritas Web Server" cmd /k "node server.js"
echo.
echo [2/2] Iniciando BioMini Daemon (Requer Administrador)...
powershell -Command "Start-Process -FilePath '.\BioMiniDaemon.exe' -Verb RunAs"
echo.
echo [✓] Abrindo Dashboard no navegador...
timeout /t 2 >nul
start http://localhost:3300
echo.
echo ==================================================
echo  Sistema iniciado com sucesso!
echo ==================================================
