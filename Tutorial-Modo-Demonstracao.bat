@echo off
cd /d "%~dp0"
title Vox Emulador - Modo Gravacao Tutorial
echo ========================================================================
echo  VOX EMULADOR - MODO TUTORIAL / GRAVACAO YOUTUBE
echo ========================================================================
echo  Simulando primeira execucao sem pasta do jogo detectada para
echo  exibir o menu de download oficial e selecao manual.
echo ========================================================================
start "" "%~dp0VoxEmulador.exe" --tutorial
exit /b 0
