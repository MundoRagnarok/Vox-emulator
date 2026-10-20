Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = Get-Location }

$settingsFile = Join-Path $scriptDir "server\appsettings.json"
$devSettingsFile = Join-Path $scriptDir "server\appsettings.Development.json"

if (-not (Test-Path $settingsFile)) {
    # Tenta procurar na raiz caso executado de outro ponto
    if (Test-Path (Join-Path $scriptDir "appsettings.json")) {
        $settingsFile = Join-Path $scriptDir "appsettings.json"
    }
}

# Carregar JSON existente ou criar padrao
$jsonObj = [ordered]@{}
if (Test-Path $settingsFile) {
    try {
        $raw = Get-Content $settingsFile -Raw -Encoding UTF8
        $jsonObj = $raw | ConvertFrom-Json
    } catch {
        $jsonObj = [ordered]@{}
    }
}

# Garantir estrutura Ro3Emu
if (-not $jsonObj.Ro3Emu) {
    $jsonObj | Add-Member -MemberType NoteProperty -Name "Ro3Emu" -Value ([PSCustomObject]@{})
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "Painel de Configurações - Vox Emulador 🇧🇷"
$form.Size = New-Object System.Drawing.Size(560, 680)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.BackColor = [System.Drawing.Color]::FromArgb(15, 23, 42)
$form.ForeColor = [System.Drawing.Color]::White

# Estilos e Fontes
$fontTitle = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$fontSection = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$fontNormal = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
$fontSmall = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Regular)

$bgDark = [System.Drawing.Color]::FromArgb(30, 41, 59)
$gold = [System.Drawing.Color]::FromArgb(234, 179, 8)
$textLight = [System.Drawing.Color]::FromArgb(241, 245, 249)

# Titulo
$lblHeader = New-Object System.Windows.Forms.Label
$lblHeader.Text = "⚙️ VOX EMULADOR - CONFIGURAÇÕES"
$lblHeader.Font = $fontTitle
$lblHeader.ForeColor = $gold
$lblHeader.Location = New-Object System.Drawing.Point(20, 15)
$lblHeader.Size = New-Object System.Drawing.Size(500, 30)
$form.Controls.Add($lblHeader)

$lblSub = New-Object System.Windows.Forms.Label
$lblSub.Text = "Emulador Brasileiro de Ragnarok Online 3 · Comunidade Mundo Ragnarok"
$lblSub.Font = $fontSmall
$lblSub.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$lblSub.Location = New-Object System.Drawing.Point(22, 45)
$lblSub.Size = New-Object System.Drawing.Size(500, 20)
$form.Controls.Add($lblSub)

# TabControl
$tabControl = New-Object System.Windows.Forms.TabControl
$tabControl.Location = New-Object System.Drawing.Point(20, 75)
$tabControl.Size = New-Object System.Drawing.Size(505, 480)
$tabControl.Font = $fontNormal

# ----------------- ABA 1: SERVIDOR -----------------
$tabGeral = New-Object System.Windows.Forms.TabPage
$tabGeral.Text = "Servidor & Nome"
$tabGeral.BackColor = $bgDark

# Nome do Servidor
$lblCluster = New-Object System.Windows.Forms.Label
$lblCluster.Text = "Nome do Servidor (exibido na tela de login):"
$lblCluster.Location = New-Object System.Drawing.Point(20, 20)
$lblCluster.Size = New-Object System.Drawing.Size(400, 20)
$lblCluster.ForeColor = $textLight
$tabGeral.Controls.Add($lblCluster)

$txtCluster = New-Object System.Windows.Forms.TextBox
$txtCluster.Location = New-Object System.Drawing.Point(20, 42)
$txtCluster.Size = New-Object System.Drawing.Size(450, 25)
$txtCluster.Text = if ($jsonObj.Ro3Emu.ClusterName) { $jsonObj.Ro3Emu.ClusterName } else { "Vox Emulador" }
$tabGeral.Controls.Add($txtCluster)

# Nome da Area / Regiao
$lblArea = New-Object System.Windows.Forms.Label
$lblArea.Text = "Nome da Região / Canal (ex: Prontera, Brasil):"
$lblArea.Location = New-Object System.Drawing.Point(20, 80)
$lblArea.Size = New-Object System.Drawing.Size(400, 20)
$lblArea.ForeColor = $textLight
$tabGeral.Controls.Add($lblArea)

$txtArea = New-Object System.Windows.Forms.TextBox
$txtArea.Location = New-Object System.Drawing.Point(20, 102)
$txtArea.Size = New-Object System.Drawing.Size(450, 25)
$txtArea.Text = if ($jsonObj.Ro3Emu.AreaName) { $jsonObj.Ro3Emu.AreaName } else { "Prontera" }
$tabGeral.Controls.Add($txtArea)

# IP do Servidor
$lblHost = New-Object System.Windows.Forms.Label
$lblHost.Text = "Endereço IP do Servidor (127.0.0.1 para jogar sozinho):"
$lblHost.Location = New-Object System.Drawing.Point(20, 140)
$lblHost.Size = New-Object System.Drawing.Size(400, 20)
$lblHost.ForeColor = $textLight
$tabGeral.Controls.Add($lblHost)

$txtHost = New-Object System.Windows.Forms.TextBox
$txtHost.Location = New-Object System.Drawing.Point(20, 162)
$txtHost.Size = New-Object System.Drawing.Size(450, 25)
$txtHost.Text = if ($jsonObj.Ro3Emu.PublicHost) { $jsonObj.Ro3Emu.PublicHost } else { "127.0.0.1" }
$tabGeral.Controls.Add($txtHost)

# Porta HTTP
$lblHttpPort = New-Object System.Windows.Forms.Label
$lblHttpPort.Text = "Porta HTTP / Login (Padrão: 5380):"
$lblHttpPort.Location = New-Object System.Drawing.Point(20, 200)
$lblHttpPort.Size = New-Object System.Drawing.Size(200, 20)
$lblHttpPort.ForeColor = $textLight
$tabGeral.Controls.Add($lblHttpPort)

$txtHttpPort = New-Object System.Windows.Forms.TextBox
$txtHttpPort.Location = New-Object System.Drawing.Point(20, 222)
$txtHttpPort.Size = New-Object System.Drawing.Size(200, 25)
$txtHttpPort.Text = if ($jsonObj.Ro3Emu.HttpPort) { $jsonObj.Ro3Emu.HttpPort } else { "5380" }
$tabGeral.Controls.Add($txtHttpPort)

# Porta Game Gate
$lblGamePort = New-Object System.Windows.Forms.Label
$lblGamePort.Text = "Porta Game Gate (Padrão: 33100):"
$lblGamePort.Location = New-Object System.Drawing.Point(250, 200)
$lblGamePort.Size = New-Object System.Drawing.Size(200, 20)
$lblGamePort.ForeColor = $textLight
$tabGeral.Controls.Add($lblGamePort)

$txtGamePort = New-Object System.Windows.Forms.TextBox
$txtGamePort.Location = New-Object System.Drawing.Point(250, 222)
$txtGamePort.Size = New-Object System.Drawing.Size(220, 25)
$txtGamePort.Text = if ($jsonObj.Ro3Emu.GameGatePort) { $jsonObj.Ro3Emu.GameGatePort } else { "33100" }
$tabGeral.Controls.Add($txtGamePort)

# Opcoes de Conteudo Aberto
$chkOpenContent = New-Object System.Windows.Forms.CheckBox
$chkOpenContent.Text = "Liberar todo o conteúdo do jogo sem travas de temporada (Open Content)"
$chkOpenContent.Location = New-Object System.Drawing.Point(20, 270)
$chkOpenContent.Size = New-Object System.Drawing.Size(450, 30)
$chkOpenContent.ForeColor = $textLight
$chkOpenContent.Checked = $true
$tabGeral.Controls.Add($chkOpenContent)

$chkPass = New-Object System.Windows.Forms.CheckBox
$chkPass.Text = "Passe de Batalha e recompensas liberadas gratuitamente"
$chkPass.Location = New-Object System.Drawing.Point(20, 310)
$chkPass.Size = New-Object System.Drawing.Size(450, 30)
$chkPass.ForeColor = $textLight
$chkPass.Checked = $true
$tabGeral.Controls.Add($chkPass)

$tabControl.TabPages.Add($tabGeral)

# ----------------- ABA 2: GERENCIAMENTO DE GM -----------------
$tabGm = New-Object System.Windows.Forms.TabPage
$tabGm.Text = "Contas & Game Master"
$tabGm.BackColor = $bgDark

# Checkbox Toda Conta ser GM
$chkGmOpen = New-Object System.Windows.Forms.CheckBox
$chkGmOpen.Text = "Modo Livre: Toda conta criada tem poderes de GM automaticamente"
$chkGmOpen.Location = New-Object System.Drawing.Point(20, 20)
$chkGmOpen.Size = New-Object System.Drawing.Size(450, 30)
$chkGmOpen.ForeColor = $gold
$isGmOpen = $true
if ($jsonObj.Ro3Emu.PSObject.Properties['GmOpenWhenEmpty'] -ne $null) {
    $isGmOpen = [bool]$jsonObj.Ro3Emu.GmOpenWhenEmpty
}
$chkGmOpen.Checked = $isGmOpen
$tabGm.Controls.Add($chkGmOpen)

$lblGmExpl = New-Object System.Windows.Forms.Label
$lblGmExpl.Text = "💡 Desmarque a opção acima caso queira um servidor comum onde apenas as contas listadas abaixo tenham acesso a comandos (!item, !zeny, !lvl, etc.)."
$lblGmExpl.Location = New-Object System.Drawing.Point(20, 55)
$lblGmExpl.Size = New-Object System.Drawing.Size(450, 40)
$lblGmExpl.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$tabGm.Controls.Add($lblGmExpl)

# Lista de Contas GM
$lblGmList = New-Object System.Windows.Forms.Label
$lblGmList.Text = "Contas com Permissão de GM (uma por linha):"
$lblGmList.Location = New-Object System.Drawing.Point(20, 110)
$lblGmList.Size = New-Object System.Drawing.Size(400, 20)
$lblGmList.ForeColor = $textLight
$tabGm.Controls.Add($lblGmList)

$txtGmAccounts = New-Object System.Windows.Forms.TextBox
$txtGmAccounts.Multiline = $true
$txtGmAccounts.ScrollBars = "Vertical"
$txtGmAccounts.Location = New-Object System.Drawing.Point(20, 135)
$txtGmAccounts.Size = New-Object System.Drawing.Size(450, 180)
$existingGms = @()
if ($jsonObj.Ro3Emu.GmAccounts) {
    $existingGms = $jsonObj.Ro3Emu.GmAccounts
}
$txtGmAccounts.Text = ($existingGms -join "`r`n")
$tabGm.Controls.Add($txtGmAccounts)

$lblHelpGm = New-Object System.Windows.Forms.Label
$lblHelpGm.Text = "Exemplo:`r`nadmin`r`ncoordenador`r`ngm_brasil"
$lblHelpGm.Location = New-Object System.Drawing.Point(20, 325)
$lblHelpGm.Size = New-Object System.Drawing.Size(450, 60)
$lblHelpGm.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$tabGm.Controls.Add($lblHelpGm)

$tabControl.TabPages.Add($tabGm)

# ----------------- ABA 3: RATES & MULTIPLICADORES -----------------
$tabRates = New-Object System.Windows.Forms.TabPage
$tabRates.Text = "Rates & Drops"
$tabRates.BackColor = $bgDark

$lblRatesIntro = New-Object System.Windows.Forms.Label
$lblRatesIntro.Text = "Ajuste os multiplicadores de progressão do Vox Emulador:"
$lblRatesIntro.Location = New-Object System.Drawing.Point(20, 20)
$lblRatesIntro.Size = New-Object System.Drawing.Size(450, 25)
$lblRatesIntro.ForeColor = $gold
$tabRates.Controls.Add($lblRatesIntro)

# Rate Exp Base
$lblRateBase = New-Object System.Windows.Forms.Label
$lblRateBase.Text = "Multiplicador de Experiência Base (EXP):"
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
$lblRateJob.Text = "Multiplicador de Experiência de Classe (Job):"
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

# Info sobre rates
$lblRatesNote = New-Object System.Windows.Forms.Label
$lblRatesNote.Text = "ℹ️ As taxas são aplicadas diretamente no motor de resolução de recompensas do servidor. Para servidores oficiais 1:1, mantenha tudo em 1x."
$lblRatesNote.Location = New-Object System.Drawing.Point(20, 280)
$lblRatesNote.Size = New-Object System.Drawing.Size(450, 45)
$lblRatesNote.ForeColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
$tabRates.Controls.Add($lblRatesNote)

$tabControl.TabPages.Add($tabRates)

$form.Controls.Add($tabControl)

# ----------------- BOTOES DE ACAO -----------------
# Botao Salvar
$btnSalvar = New-Object System.Windows.Forms.Button
$btnSalvar.Text = "💾 SALVAR CONFIGURAÇÕES"
$btnSalvar.Location = New-Object System.Drawing.Point(20, 570)
$btnSalvar.Size = New-Object System.Drawing.Size(245, 45)
$btnSalvar.BackColor = [System.Drawing.Color]::FromArgb(22, 163, 74)
$btnSalvar.ForeColor = [System.Drawing.Color]::White
$btnSalvar.Font = $fontSection
$btnSalvar.FlatStyle = "Flat"
$form.Controls.Add($btnSalvar)

# Botao Iniciar
$btnIniciar = New-Object System.Windows.Forms.Button
$btnIniciar.Text = "▶️ INICIAR O JOGO"
$btnIniciar.Location = New-Object System.Drawing.Point(280, 570)
$btnIniciar.Size = New-Object System.Drawing.Size(245, 45)
$btnIniciar.BackColor = [System.Drawing.Color]::FromArgb(37, 99, 235)
$btnIniciar.ForeColor = [System.Drawing.Color]::White
$btnIniciar.Font = $fontSection
$btnIniciar.FlatStyle = "Flat"
$form.Controls.Add($btnIniciar)

# Acao Salvar
$btnSalvar.Add_Click({
    try {
        $jsonObj.Ro3Emu.ClusterName = $txtCluster.Text.Trim()
        $jsonObj.Ro3Emu.AreaName = $txtArea.Text.Trim()
        $jsonObj.Ro3Emu.PublicHost = $txtHost.Text.Trim()
        $jsonObj.Ro3Emu.HttpPort = [int]$txtHttpPort.Text.Trim()
        $jsonObj.Ro3Emu.GameGatePort = [int]$txtGamePort.Text.Trim()
        $jsonObj.Ro3Emu.GmOpenWhenEmpty = $chkGmOpen.Checked

        # Processar Lista de GMs
        $rawLines = $txtGmAccounts.Lines | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
        $jsonObj.Ro3Emu.GmAccounts = @($rawLines)

        # Salvar JSON formatado
        $newJsonText = $jsonObj | ConvertTo-Json -Depth 10
        [System.IO.File]::WriteAllText($settingsFile, $newJsonText, [System.Text.Encoding]::UTF8)

        # Atualizar devSettings se existir
        if (Test-Path $devSettingsFile) {
            $devJson = (Get-Content $devSettingsFile -Raw -Encoding UTF8) | ConvertFrom-Json
            if (-not $devJson.Ro3Emu) { $devJson | Add-Member -MemberType NoteProperty -Name "Ro3Emu" -Value ([PSCustomObject]@{}) }
            if ($chkOpenContent.Checked) {
                if (-not $devJson.Ro3Emu.OpenContent) { $devJson.Ro3Emu | Add-Member -MemberType NoteProperty -Name "OpenContent" -Value ([PSCustomObject]@{}) }
                $devJson.Ro3Emu.OpenContent.Enabled = $true
            }
            [System.IO.File]::WriteAllText($devSettingsFile, ($devJson | ConvertTo-Json -Depth 10), [System.Text.Encoding]::UTF8)
        }

        [System.Windows.Forms.MessageBox]::Show(
            "Configurações do Vox Emulador salvas com sucesso!",
            "Sucesso",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information
        )
    } catch {
        [System.Windows.Forms.MessageBox]::Show(
            "Erro ao salvar configurações: $($_.Exception.Message)",
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
            "Executável VoxEmulador.exe não encontrado nesta pasta!",
            "Aviso",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
    }
})

[void]$form.ShowDialog()
