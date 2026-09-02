@echo off
title BioMini PnP Watchdog & REST Bridge
cd /d "%~dp0"
echo ==================================================
echo  INICIANDO OPEN-BIOMINI PNP WATCHDOG (ADMIN)
echo ==================================================
powershell -Command "Start-Process -FilePath '.\BioMiniPnPService.exe' -Verb RunAs"
