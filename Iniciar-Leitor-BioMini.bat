@echo off
title BioMini Touch & Punch Daemon
cd /d "%~dp0"
echo ==================================================
echo  INICIANDO SUPREMA BIOMINI DAEMON (ADMIN)
echo ==================================================
powershell -Command "Start-Process -FilePath '.\BioMiniDaemon.exe' -Verb RunAs"
