@echo off
title Painel de Configuracoes - Vox Emulador
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Painel-Vox.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Abrindo em modo console...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Painel-Vox.ps1"
    pause
)
