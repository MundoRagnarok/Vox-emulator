@echo off
cd /d "%~dp0"
start "" powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Painel-Vox.ps1"
exit /b 0
