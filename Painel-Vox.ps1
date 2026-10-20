Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = Get-Location }

$settingsFile = Join-Path $scriptDir "server\appsettings.json"
$devSettingsFile = Join-Path $scriptDir "server\appsettings.Development.json"

if (-not (Test-Path $settingsFile)) {
    if (Test-Path (Join-Path $scriptDir "appsettings.json")) {
        $settingsFile = Join-Path $scriptDir "appsettings.json"
    }
}

# Funcao segura para definir propriedades sem erro de PSCustomObject
function Set-Prop($target, $name, $val) {
    if ($null -eq $target.PSObject.Properties[$name]) {
        $target | Add-Member -MemberType NoteProperty -Name $name -Value $val -Force
    } else {
        $target.$name = $val
    }
}

# Carregar JSON existente
$jsonObj = $null
if (Test-Path $settingsFile) {
    try {
        $raw = Get-Content $settingsFile -Raw -Encoding UTF8
        $jsonObj = $raw | ConvertFrom-Json
    } catch {
        $jsonObj = $null
    }
}

if ($jsonObj -eq $null) {
    $jsonObj = [PSCustomObject]@{
        Logging = [PSCustomObject]@{
            LogLevel = [PSCustomObject]@{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        AllowedHosts = "*"
        Ro3Emu = [PSCustomObject]@{
            ClusterName = "Vox Emulador"
            AreaName = "Prontera"
            PublicHost = "127.0.0.1"
            HttpPort = 5380
            MicroGatePort = 20000
            GameGatePort = 33100
            DataDir = "data"
            ClusterId = 1
            GmOpenWhenEmpty = $false
            GmAccounts = @("admin")
        }
    }
}

if (-not $jsonObj.Ro3Emu) {
    $jsonObj | Add-Member -MemberType NoteProperty -Name "Ro3Emu" -Value ([PSCustomObject]@{}) -Force
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "Painel de Configuracoes - Vox Emulador (Brasil)"
$form.Size = New-Object System.Drawing.Size(570, 710)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.BackColor = [System.Drawing.Color]::FromArgb(15, 23, 42)
$form.ForeColor = [System.Drawing.Color]::White

# Estilos e Fontes
$fontTitle = New-Object System.Drawing.Font("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)
$fontSection = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$fontNormal = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
$fontSmall = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Regular)

$bgDark = [System.Drawing.Color]::FromArgb(30, 41, 59)
$bgCard = [System.Drawing.Color]::FromArgb(45, 55, 72)
$gold = [System.Drawing.Color]::FromArgb(234, 179, 8)
$textLight = [System.Drawing.Color]::FromArgb(241, 245, 249)

# Titulo Principal
$lblHeader = New-Object System.Windows.Forms.Label
$lblHeader.Text = "VOX EMULADOR - CONFIGURACOES"
$lblHeader.Font = $fontTitle
$lblHeader.ForeColor = $gold
$lblHeader.Location = New-Object System.Drawing.Point(20, 15)
$lblHeader.Size = New-Object System.Drawing.Size(510, 28)
$form.Controls.Add($lblHeader)

$lblSub = New-Object System.Windows.Forms.Label
$lblSub.Text = "Emulador Brasileiro de Ragnarok Online 3 - Comunidade Mundo Ragnarok"
$lblSub.Font = $fontSmall
$lblSub.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$lblSub.Location = New-Object System.Drawing.Point(22, 45)
$lblSub.Size = New-Object System.Drawing.Size(510, 20)
$form.Controls.Add($lblSub)

# TabControl
$tabControl = New-Object System.Windows.Forms.TabControl
$tabControl.Location = New-Object System.Drawing.Point(20, 72)
$tabControl.Size = New-Object System.Drawing.Size(515, 520)
$tabControl.Font = $fontNormal

# ==================== ABA 1: SERVIDOR ====================
$tabGeral = New-Object System.Windows.Forms.TabPage
$tabGeral.Text = "Servidor e Nome"
$tabGeral.BackColor = $bgDark

# Nome do Servidor
$lblCluster = New-Object System.Windows.Forms.Label
$lblCluster.Text = "Nome do Servidor (exibido na tela de login do jogo):"
$lblCluster.Location = New-Object System.Drawing.Point(20, 15)
$lblCluster.Size = New-Object System.Drawing.Size(460, 20)
$lblCluster.Font = $fontSection
$lblCluster.ForeColor = $gold
$tabGeral.Controls.Add($lblCluster)

$txtCluster = New-Object System.Windows.Forms.TextBox
$txtCluster.Location = New-Object System.Drawing.Point(20, 38)
$txtCluster.Size = New-Object System.Drawing.Size(465, 25)
$txtCluster.Text = if ($jsonObj.Ro3Emu.ClusterName) { [string]$jsonObj.Ro3Emu.ClusterName } else { "Vox Emulador" }
$tabGeral.Controls.Add($txtCluster)

# Texto de informacao de onde mudar depois
$lblOndeMudar = New-Object System.Windows.Forms.Label
$lblOndeMudar.Text = "Onde mudar depois: Voce pode alterar este nome a qualquer momento por este painel ou diretamente no arquivo 'server\appsettings.json' na linha 'ClusterName'."
$lblOndeMudar.Location = New-Object System.Drawing.Point(20, 68)
$lblOndeMudar.Size = New-Object System.Drawing.Size(465, 34)
$lblOndeMudar.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$lblOndeMudar.Font = $fontSmall
$tabGeral.Controls.Add($lblOndeMudar)

# Nome da Area / Regiao
$lblArea = New-Object System.Windows.Forms.Label
$lblArea.Text = "Nome da Regiao / Canal (ex: Prontera, Brasil):"
$lblArea.Location = New-Object System.Drawing.Point(20, 110)
$lblArea.Size = New-Object System.Drawing.Size(460, 20)
$lblArea.ForeColor = $textLight
$tabGeral.Controls.Add($lblArea)

$txtArea = New-Object System.Windows.Forms.TextBox
$txtArea.Location = New-Object System.Drawing.Point(20, 132)
$txtArea.Size = New-Object System.Drawing.Size(465, 25)
$txtArea.Text = if ($jsonObj.Ro3Emu.AreaName) { [string]$jsonObj.Ro3Emu.AreaName } else { "Prontera" }
$tabGeral.Controls.Add($txtArea)

# IP do Servidor
$lblHost = New-Object System.Windows.Forms.Label
$lblHost.Text = "Endereco IP do Servidor (127.0.0.1 para solo ou IP de rede/publico):"
$lblHost.Location = New-Object System.Drawing.Point(20, 170)
$lblHost.Size = New-Object System.Drawing.Size(460, 20)
$lblHost.ForeColor = $textLight
$tabGeral.Controls.Add($lblHost)

$txtHost = New-Object System.Windows.Forms.TextBox
$txtHost.Location = New-Object System.Drawing.Point(20, 192)
$txtHost.Size = New-Object System.Drawing.Size(465, 25)
$txtHost.Text = if ($jsonObj.Ro3Emu.PublicHost) { [string]$jsonObj.Ro3Emu.PublicHost } else { "127.0.0.1" }
$tabGeral.Controls.Add($txtHost)

# Portas
$lblHttpPort = New-Object System.Windows.Forms.Label
$lblHttpPort.Text = "Porta HTTP / Login (5380):"
$lblHttpPort.Location = New-Object System.Drawing.Point(20, 230)
$lblHttpPort.Size = New-Object System.Drawing.Size(210, 20)
$lblHttpPort.ForeColor = $textLight
$tabGeral.Controls.Add($lblHttpPort)

$txtHttpPort = New-Object System.Windows.Forms.TextBox
$txtHttpPort.Location = New-Object System.Drawing.Point(20, 252)
$txtHttpPort.Size = New-Object System.Drawing.Size(210, 25)
$txtHttpPort.Text = if ($jsonObj.Ro3Emu.HttpPort) { [string]$jsonObj.Ro3Emu.HttpPort } else { "5380" }
$tabGeral.Controls.Add($txtHttpPort)

$lblGamePort = New-Object System.Windows.Forms.Label
$lblGamePort.Text = "Porta Game Gate (33100):"
$lblGamePort.Location = New-Object System.Drawing.Point(260, 230)
$lblGamePort.Size = New-Object System.Drawing.Size(220, 20)
$lblGamePort.ForeColor = $textLight
$tabGeral.Controls.Add($lblGamePort)

$txtGamePort = New-Object System.Windows.Forms.TextBox
$txtGamePort.Location = New-Object System.Drawing.Point(260, 252)
$txtGamePort.Size = New-Object System.Drawing.Size(225, 25)
$txtGamePort.Text = if ($jsonObj.Ro3Emu.GameGatePort) { [string]$jsonObj.Ro3Emu.GameGatePort } else { "33100" }
$tabGeral.Controls.Add($txtGamePort)

# Opcoes de Conteudo
$chkOpenContent = New-Object System.Windows.Forms.CheckBox
$chkOpenContent.Text = "Liberar todo o conteudo do jogo sem travas de temporada (Open Content)"
$chkOpenContent.Location = New-Object System.Drawing.Point(20, 295)
$chkOpenContent.Size = New-Object System.Drawing.Size(465, 30)
$chkOpenContent.ForeColor = $textLight
$chkOpenContent.Checked = $true
$tabGeral.Controls.Add($chkOpenContent)

$chkPass = New-Object System.Windows.Forms.CheckBox
$chkPass.Text = "Passe de Batalha e recompensas premium liberadas gratuitamente"
$chkPass.Location = New-Object System.Drawing.Point(20, 335)
$chkPass.Size = New-Object System.Drawing.Size(465, 30)
$chkPass.ForeColor = $textLight
$chkPass.Checked = $true
$tabGeral.Controls.Add($chkPass)

$tabControl.TabPages.Add($tabGeral)

# ==================== ABA 2: CONTAS E GAME MASTER ====================
$tabGm = New-Object System.Windows.Forms.TabPage
$tabGm.Text = "Contas e Game Master"
$tabGm.BackColor = $bgDark

# Checkbox Toda Conta ser GM
$chkGmOpen = New-Object System.Windows.Forms.CheckBox
$chkGmOpen.Text = "Modo Livre: Toda conta criada tem poderes de GM automaticamente"
$chkGmOpen.Location = New-Object System.Drawing.Point(20, 15)
$chkGmOpen.Size = New-Object System.Drawing.Size(465, 28)
$chkGmOpen.ForeColor = $gold
$isGmOpen = $false
if ($jsonObj.Ro3Emu.PSObject.Properties['GmOpenWhenEmpty'] -ne $null) {
    $isGmOpen = [bool]$jsonObj.Ro3Emu.GmOpenWhenEmpty
}
$chkGmOpen.Checked = $isGmOpen
$tabGm.Controls.Add($chkGmOpen)

# Painel de Criacao de Conta Admin (Login e Senha)
$grpAdmin = New-Object System.Windows.Forms.GroupBox
$grpAdmin.Text = "Criar Nova Conta de Administrador (GM)"
$grpAdmin.Location = New-Object System.Drawing.Point(20, 50)
$grpAdmin.Size = New-Object System.Drawing.Size(465, 155)
$grpAdmin.ForeColor = $gold
$grpAdmin.Font = $fontNormal

$lblNewUser = New-Object System.Windows.Forms.Label
$lblNewUser.Text = "Usuario (Login Admin):"
$lblNewUser.Location = New-Object System.Drawing.Point(15, 25)
$lblNewUser.Size = New-Object System.Drawing.Size(180, 18)
$lblNewUser.ForeColor = $textLight
$grpAdmin.Controls.Add($lblNewUser)

$txtNewUser = New-Object System.Windows.Forms.TextBox
$txtNewUser.Location = New-Object System.Drawing.Point(15, 45)
$txtNewUser.Size = New-Object System.Drawing.Size(190, 25)
$txtNewUser.Text = "admin"
$grpAdmin.Controls.Add($txtNewUser)

$lblNewPass = New-Object System.Windows.Forms.Label
$lblNewPass.Text = "Senha Desejada:"
$lblNewPass.Location = New-Object System.Drawing.Point(220, 25)
$lblNewPass.Size = New-Object System.Drawing.Size(180, 18)
$lblNewPass.ForeColor = $textLight
$grpAdmin.Controls.Add($lblNewPass)

$txtNewPass = New-Object System.Windows.Forms.TextBox
$txtNewPass.Location = New-Object System.Drawing.Point(220, 45)
$txtNewPass.Size = New-Object System.Drawing.Size(190, 25)
$txtNewPass.PasswordChar = '*'
$txtNewPass.Text = "admin123"
$grpAdmin.Controls.Add($txtNewPass)

$btnCadastrarAdmin = New-Object System.Windows.Forms.Button
$btnCadastrarAdmin.Text = "Adicionar a Lista de GMs"
$btnCadastrarAdmin.Location = New-Object System.Drawing.Point(15, 78)
$btnCadastrarAdmin.Size = New-Object System.Drawing.Size(395, 30)
$btnCadastrarAdmin.BackColor = [System.Drawing.Color]::FromArgb(16, 185, 129)
$btnCadastrarAdmin.ForeColor = [System.Drawing.Color]::White
$btnCadastrarAdmin.FlatStyle = "Flat"
$grpAdmin.Controls.Add($btnCadastrarAdmin)

$lblAdminExpl = New-Object System.Windows.Forms.Label
$lblAdminExpl.Text = "Como as contas sao criadas no primeiro login do jogo, basta digitar esse usuario e senha ao abrir o Ragnarok 3 para entrar com sua conta GM!"
$lblAdminExpl.Location = New-Object System.Drawing.Point(15, 114)
$lblAdminExpl.Size = New-Object System.Drawing.Size(435, 34)
$lblAdminExpl.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$lblAdminExpl.Font = $fontSmall
$grpAdmin.Controls.Add($lblAdminExpl)

$tabGm.Controls.Add($grpAdmin)

# Lista de Contas GM Autorizadas
$lblGmList = New-Object System.Windows.Forms.Label
$lblGmList.Text = "Lista de Contas GM Autorizadas (uma por linha):"
$lblGmList.Location = New-Object System.Drawing.Point(20, 215)
$lblGmList.Size = New-Object System.Drawing.Size(400, 20)
$lblGmList.ForeColor = $textLight
$tabGm.Controls.Add($lblGmList)

$txtGmAccounts = New-Object System.Windows.Forms.TextBox
$txtGmAccounts.Multiline = $true
$txtGmAccounts.ScrollBars = "Vertical"
$txtGmAccounts.Location = New-Object System.Drawing.Point(20, 238)
$txtGmAccounts.Size = New-Object System.Drawing.Size(465, 180)
$existingGms = @()
if ($jsonObj.Ro3Emu.GmAccounts) {
    $existingGms = @($jsonObj.Ro3Emu.GmAccounts)
}
if ($existingGms.Count -eq 0) {
    $existingGms = @("admin")
}
$txtGmAccounts.Text = ($existingGms -join "`r`n")
$tabGm.Controls.Add($txtGmAccounts)

$lblHelpGm = New-Object System.Windows.Forms.Label
$lblHelpGm.Text = "Dica: Com o Modo Livre desmarcado acima, SOMENTE os usuarios desta lista tem comandos (!item, !zeny, !lvl, !tele, etc.) no jogo."
$lblHelpGm.Location = New-Object System.Drawing.Point(20, 425)
$lblHelpGm.Size = New-Object System.Drawing.Size(465, 40)
$lblHelpGm.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$lblHelpGm.Font = $fontSmall
$tabGm.Controls.Add($lblHelpGm)

# Acao do botao Adicionar a Lista de GMs
$btnCadastrarAdmin.Add_Click({
    $u = $txtNewUser.Text.Trim()
    if ($u -ne "") {
        $lines = @($txtGmAccounts.Lines | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
        if ($lines -notcontains $u) {
            $lines += $u
            $txtGmAccounts.Text = ($lines -join "`r`n")
        }
        $chkGmOpen.Checked = $false
        [System.Windows.Forms.MessageBox]::Show(
            "Conta '$u' adicionada com sucesso a lista de Game Masters!`r`n`r`nLembre-se de digitar esse usuario e a senha desejada na tela de login do jogo.",
            "Conta GM Pronta",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information
        )
    }
})

$tabControl.TabPages.Add($tabGm)

# ==================== ABA 3: RATES E DROPS ====================
$tabRates = New-Object System.Windows.Forms.TabPage
$tabRates.Text = "Rates e Drops"
$tabRates.BackColor = $bgDark

$lblRatesIntro = New-Object System.Windows.Forms.Label
$lblRatesIntro.Text = "Ajuste os multiplicadores de progressao do Vox Emulador:"
$lblRatesIntro.Location = New-Object System.Drawing.Point(20, 20)
$lblRatesIntro.Size = New-Object System.Drawing.Size(465, 25)
$lblRatesIntro.ForeColor = $gold
$tabRates.Controls.Add($lblRatesIntro)

# Rate Exp Base
$lblRateBase = New-Object System.Windows.Forms.Label
$lblRateBase.Text = "Multiplicador de Experiencia Base (EXP):"
$lblRateBase.Location = New-Object System.Drawing.Point(20, 60)
$lblRateBase.Size = New-Object System.Drawing.Size(300, 20)
$lblRateBase.ForeColor = $textLight
$tabRates.Controls.Add($lblRateBase)

$cmbRateBase = New-Object System.Windows.Forms.ComboBox
$cmbRateBase.Items.AddRange(@("1x (Original)", "2x", "5x", "10x", "25x", "50x", "100x"))
$cmbRateBase.SelectedIndex = 0
$cmbRateBase.Location = New-Object System.Drawing.Point(20, 85)
$cmbRateBase.Size = New-Object System.Drawing.Size(200, 25)
$tabRates.Controls.Add($cmbRateBase)

# Rate Exp Job
$lblRateJob = New-Object System.Windows.Forms.Label
$lblRateJob.Text = "Multiplicador de Experiencia de Classe (Job):"
$lblRateJob.Location = New-Object System.Drawing.Point(20, 130)
$lblRateJob.Size = New-Object System.Drawing.Size(300, 20)
$lblRateJob.ForeColor = $textLight
$tabRates.Controls.Add($lblRateJob)

$cmbRateJob = New-Object System.Windows.Forms.ComboBox
$cmbRateJob.Items.AddRange(@("1x (Original)", "2x", "5x", "10x", "25x", "50x", "100x"))
$cmbRateJob.SelectedIndex = 0
$cmbRateJob.Location = New-Object System.Drawing.Point(20, 155)
$cmbRateJob.Size = New-Object System.Drawing.Size(200, 25)
$tabRates.Controls.Add($cmbRateJob)

# Rate Drop
$lblRateDrop = New-Object System.Windows.Forms.Label
$lblRateDrop.Text = "Multiplicador de Chance de Drop de Itens e Cartas:"
$lblRateDrop.Location = New-Object System.Drawing.Point(20, 200)
$lblRateDrop.Size = New-Object System.Drawing.Size(350, 20)
$lblRateDrop.ForeColor = $textLight
$tabRates.Controls.Add($lblRateDrop)

$cmbRateDrop = New-Object System.Windows.Forms.ComboBox
$cmbRateDrop.Items.AddRange(@("1x (Original)", "2x", "3x", "5x", "10x", "20x"))
$cmbRateDrop.SelectedIndex = 0
$cmbRateDrop.Location = New-Object System.Drawing.Point(20, 225)
$cmbRateDrop.Size = New-Object System.Drawing.Size(200, 25)
$tabRates.Controls.Add($cmbRateDrop)

$lblRatesNote = New-Object System.Windows.Forms.Label
$lblRatesNote.Text = "Nota: Para servidores oficiais 1:1, mantenha os multiplicadores em 1x."
$lblRatesNote.Location = New-Object System.Drawing.Point(20, 280)
$lblRatesNote.Size = New-Object System.Drawing.Size(465, 45)
$lblRatesNote.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$tabRates.Controls.Add($lblRatesNote)

$tabControl.TabPages.Add($tabRates)

$form.Controls.Add($tabControl)

# ==================== BOTOES INFERIORES ====================
$btnSalvar = New-Object System.Windows.Forms.Button
$btnSalvar.Text = "SALVAR CONFIGURACOES"
$btnSalvar.Location = New-Object System.Drawing.Point(20, 605)
$btnSalvar.Size = New-Object System.Drawing.Size(250, 48)
$btnSalvar.BackColor = [System.Drawing.Color]::FromArgb(22, 163, 74)
$btnSalvar.ForeColor = [System.Drawing.Color]::White
$btnSalvar.Font = $fontSection
$btnSalvar.FlatStyle = "Flat"
$form.Controls.Add($btnSalvar)

$btnIniciar = New-Object System.Windows.Forms.Button
$btnIniciar.Text = "INICIAR O JOGO"
$btnIniciar.Location = New-Object System.Drawing.Point(285, 605)
$btnIniciar.Size = New-Object System.Drawing.Size(250, 48)
$btnIniciar.BackColor = [System.Drawing.Color]::FromArgb(37, 99, 235)
$btnIniciar.ForeColor = [System.Drawing.Color]::White
$btnIniciar.Font = $fontSection
$btnIniciar.FlatStyle = "Flat"
$form.Controls.Add($btnIniciar)

# Acao Salvar com Set-Prop seguro
$btnSalvar.Add_Click({
    try {
        Set-Prop $jsonObj.Ro3Emu "ClusterName" $txtCluster.Text.Trim()
        Set-Prop $jsonObj.Ro3Emu "AreaName" $txtArea.Text.Trim()
        Set-Prop $jsonObj.Ro3Emu "PublicHost" $txtHost.Text.Trim()
        Set-Prop $jsonObj.Ro3Emu "HttpPort" ([int]$txtHttpPort.Text.Trim())
        Set-Prop $jsonObj.Ro3Emu "GameGatePort" ([int]$txtGamePort.Text.Trim())
        Set-Prop $jsonObj.Ro3Emu "GmOpenWhenEmpty" ([bool]$chkGmOpen.Checked)

        # Processar Lista de GMs
        $rawLines = @($txtGmAccounts.Lines | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
        Set-Prop $jsonObj.Ro3Emu "GmAccounts" $rawLines

        # Salvar JSON formatado
        $newJsonText = $jsonObj | ConvertTo-Json -Depth 10
        [System.IO.File]::WriteAllText($settingsFile, $newJsonText, [System.Text.Encoding]::UTF8)

        # Atualizar devSettings se existir
        if (Test-Path $devSettingsFile) {
            $devJson = (Get-Content $devSettingsFile -Raw -Encoding UTF8) | ConvertFrom-Json
            if (-not $devJson.Ro3Emu) {
                $devJson | Add-Member -MemberType NoteProperty -Name "Ro3Emu" -Value ([PSCustomObject]@{}) -Force
            }
            if ($chkOpenContent.Checked) {
                if (-not $devJson.Ro3Emu.OpenContent) {
                    $devJson.Ro3Emu | Add-Member -MemberType NoteProperty -Name "OpenContent" -Value ([PSCustomObject]@{}) -Force
                }
                Set-Prop $devJson.Ro3Emu.OpenContent "Enabled" $true
            }
            [System.IO.File]::WriteAllText($devSettingsFile, ($devJson | ConvertTo-Json -Depth 10), [System.Text.Encoding]::UTF8)
        }

        [System.Windows.Forms.MessageBox]::Show(
            "Configuracoes do Vox Emulador salvas com sucesso no arquivo server\appsettings.json!",
            "Sucesso",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information
        )
    } catch {
        [System.Windows.Forms.MessageBox]::Show(
            "Erro ao salvar configuracoes: $($_.Exception.Message)",
            "Erro",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        )
    }
})

# Acao Iniciar Jogo
$btnIniciar.Add_Click({
    $exePath = Join-Path $scriptDir "VoxEmulador.exe"
    if (-not (Test-Path $exePath)) {
        $exePath = Join-Path $scriptDir "RO3Emu.exe"
    }

    if (Test-Path $exePath) {
        Start-Process -FilePath $exePath
        $form.Close()
    } else {
        [System.Windows.Forms.MessageBox]::Show(
            "Executavel VoxEmulador.exe nao encontrado nesta pasta!",
            "Aviso",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
    }
})

[void]$form.ShowDialog()
