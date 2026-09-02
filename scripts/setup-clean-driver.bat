@echo off
title Restaurador PnP Suprema BioMini
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "setup-clean-driver.ps1"
