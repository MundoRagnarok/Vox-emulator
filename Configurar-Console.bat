@echo off
chcp 65001 >nul
title Configurador - Vox Emulador 🇧🇷
cd /d "%~dp0"

:MENU
cls
echo ========================================================================
echo       VOX EMULADOR - PAINEL DE CONFIGURAÇÕES RÁPIDAS 🇧🇷
echo ========================================================================
echo.
echo  [1] Abrir Painel Visual Grafico (Recomendado)
echo  [2] Alterar Nome do Servidor no Jogo (ClusterName)
echo  [3] Ativar / Desativar Modo Livre (Toda Conta ser GM)
echo  [4] Adicionar Nova Conta GM
echo  [5] Listar Contas GM Atuais
echo  [6] Iniciar o Servidor e o Jogo (VoxEmulador.exe)
echo  [7] Abrir Discord da Comunidade (Mundo Ragnarok)
echo  [0] Sair
echo.
echo ========================================================================
set /p OPCAO="Escolha uma opção [0-7]: "

if "%OPCAO%"=="1" goto ABRIR_GUI
if "%OPCAO%"=="2" goto MUDAR_NOME
if "%OPCAO%"=="3" goto TOGGLE_GM
if "%OPCAO%"=="4" goto ADD_GM
if "%OPCAO%"=="5" goto LISTAR_GM
if "%OPCAO%"=="6" goto INICIAR
if "%OPCAO%"=="7" goto DISCORD
if "%OPCAO%"=="0" exit /b 0

echo Opção inválida!
timeout /t 2 >nul
goto MENU

:ABRIR_GUI
start "" "%~dp0Painel-Vox.bat"
goto MENU

:MUDAR_NOME
cls
echo ------------------------------------------------------------------------
echo  ALTERAR NOME DO SERVIDOR
echo ------------------------------------------------------------------------
echo.
set /p NOVO_NOME="Digite o novo nome para o servidor: "
if "%NOVO_NOME%"=="" goto MENU

powershell -Command "$p = 'server\appsettings.json'; $j = Get-Content $p -Raw | ConvertFrom-Json; $j.Ro3Emu.ClusterName = '%NOVO_NOME%'; $j | ConvertTo-Json -Depth 10 | Set-Content $p -Encoding UTF8"
echo.
echo [OK] Nome do servidor alterado com sucesso para "%NOVO_NOME%"!
pause
goto MENU

:TOGGLE_GM
cls
echo ------------------------------------------------------------------------
echo  MODO LIVRE DE GM
echo ------------------------------------------------------------------------
echo.
echo  [1] ATIVAR Modo Livre (Toda conta criada tem poderes de GM)
echo  [2] DESATIVAR Modo Livre (Apenas contas da lista tem poderes de GM)
echo  [0] Voltar
echo.
set /p GM_OPT="Escolha [1 ou 2]: "

if "%GM_OPT%"=="1" (
    powershell -Command "$p = 'server\appsettings.json'; $j = Get-Content $p -Raw | ConvertFrom-Json; $j.Ro3Emu.GmOpenWhenEmpty = $true; $j | ConvertTo-Json -Depth 10 | Set-Content $p -Encoding UTF8"
    echo.
    echo [OK] Modo Livre ATIVADO: Todas as contas terao poderes de GM!
    pause
    goto MENU
)
if "%GM_OPT%"=="2" (
    powershell -Command "$p = 'server\appsettings.json'; $j = Get-Content $p -Raw | ConvertFrom-Json; $j.Ro3Emu.GmOpenWhenEmpty = $false; $j | ConvertTo-Json -Depth 10 | Set-Content $p -Encoding UTF8"
    echo.
    echo [OK] Modo Livre DESATIVADO: Apenas contas autorizadas terao GM!
    pause
    goto MENU
)
goto MENU

:ADD_GM
cls
echo ------------------------------------------------------------------------
echo  ADICIONAR CONTA GM
echo ------------------------------------------------------------------------
echo.
set /p CONTA_GM="Digite o nome da conta de usuario para receber GM: "
if "%CONTA_GM%"=="" goto MENU

powershell -Command "$p = 'server\appsettings.json'; $j = Get-Content $p -Raw | ConvertFrom-Json; $gms = @($j.Ro3Emu.GmAccounts); if ($gms -notcontains '%CONTA_GM%') { $gms += '%CONTA_GM%' }; $j.Ro3Emu.GmAccounts = $gms; $j.Ro3Emu.GmOpenWhenEmpty = $false; $j | ConvertTo-Json -Depth 10 | Set-Content $p -Encoding UTF8"
echo.
echo [OK] Conta '%CONTA_GM%' adicionada a lista oficial de GMs!
pause
goto MENU

:LISTAR_GM
cls
echo ------------------------------------------------------------------------
echo  LISTA DE CONTAS COM PODERES DE GM
echo ------------------------------------------------------------------------
echo.
powershell -Command "$p = 'server\appsettings.json'; $j = Get-Content $p -Raw | ConvertFrom-Json; Write-Host 'Modo Livre (todas contas sao GM):' $j.Ro3Emu.GmOpenWhenEmpty; Write-Host '--- Contas GM autorizadas ---'; if ($j.Ro3Emu.GmAccounts) { $j.Ro3Emu.GmAccounts | ForEach-Object { Write-Host ' *' $_ } } else { Write-Host '(Nenhuma conta cadastrada)' }"
echo.
pause
goto MENU

:INICIAR
cls
echo Iniciando o Vox Emulador...
start "" "%~dp0VoxEmulador.exe"
exit /b 0

:DISCORD
start https://discord.gg/UvvJF4W3yK
goto MENU
