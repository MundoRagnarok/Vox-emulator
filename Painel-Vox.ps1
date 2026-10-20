Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    if ($MyInvocation.MyCommand -and $MyInvocation.MyCommand.Path) {
        $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    } else {
        $scriptDir = (Get-Location).Path
    }
}

$settingsFile = Join-Path $scriptDir "server\appsettings.json"
$devSettingsFile = Join-Path $scriptDir "server\appsettings.Development.json"

if (-not (Test-Path $settingsFile)) {
    if (Test-Path (Join-Path $scriptDir "appsettings.json")) {
        $settingsFile = Join-Path $scriptDir "appsettings.json"
    }
}

# Funcao para definir propriedades em PSCustomObject com seguranca
function Set-Prop($target, $name, $val) {
    if ($null -eq $target.PSObject.Properties[$name]) {
        $target | Add-Member -MemberType NoteProperty -Name $name -Value $val -Force
    } else {
        $target.$name = $val
    }
}

# Carregar JSON de configuracoes
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

# ==================== PALETA DE CORES DARK GAMER ====================
$cBgMain       = [System.Drawing.Color]::FromArgb(15, 23, 42)      # Slate 900
$cHeader       = [System.Drawing.Color]::FromArgb(11, 15, 25)      # Top bar
$cCard         = [System.Drawing.Color]::FromArgb(30, 41, 59)      # Slate 800
$cCardBorder   = [System.Drawing.Color]::FromArgb(51, 65, 85)      # Slate 700
$cInputBg      = [System.Drawing.Color]::FromArgb(24, 32, 47)      # Input dark
$cTextLight    = [System.Drawing.Color]::FromArgb(248, 250, 252)   # Slate 50
$cTextMuted    = [System.Drawing.Color]::FromArgb(148, 163, 184)   # Slate 400
$cGold         = [System.Drawing.Color]::FromArgb(250, 204, 21)    # Yellow 400
$cGoldDark     = [System.Drawing.Color]::FromArgb(202, 138, 4)     # Yellow 600
$cGreen        = [System.Drawing.Color]::FromArgb(16, 185, 129)    # Emerald 500
$cGreenDark    = [System.Drawing.Color]::FromArgb(5, 150, 105)     # Emerald 600
$cBlue         = [System.Drawing.Color]::FromArgb(37, 99, 235)     # Blue 600
$cBlueDark     = [System.Drawing.Color]::FromArgb(29, 78, 216)     # Blue 700
$cRed          = [System.Drawing.Color]::FromArgb(239, 68, 68)     # Red 500
$cRedDark      = [System.Drawing.Color]::FromArgb(185, 28, 28)     # Red 700
$cTabActive    = [System.Drawing.Color]::FromArgb(30, 41, 59)      # Aba ativa
$cTabInactive  = [System.Drawing.Color]::FromArgb(15, 23, 42)      # Aba inativa

# Fontes
$fontTitle    = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$fontSubTitle = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Regular)
$fontTab      = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$fontSection  = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$fontNormal   = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
$fontInput    = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Regular)
$fontSmall    = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Regular)
$fontBtn      = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

# ==================== JANELA PRINCIPAL ====================
$form = New-Object System.Windows.Forms.Form
$form.Text = "Vox Emulador - Painel de Controle e Configuracoes"
$form.Size = New-Object System.Drawing.Size(620, 720)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.BackColor = $cBgMain
$form.ForeColor = $cTextLight

# ==================== TOPO: CABECALHO ====================
$pnlHeader = New-Object System.Windows.Forms.Panel
$pnlHeader.Location = New-Object System.Drawing.Point(0, 0)
$pnlHeader.Size = New-Object System.Drawing.Size(620, 68)
$pnlHeader.BackColor = $cHeader
$form.Controls.Add($pnlHeader)

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "VOX EMULADOR  ·  PAINEL DE CONTROLE"
$lblTitle.Font = $fontTitle
$lblTitle.ForeColor = $cGold
$lblTitle.Location = New-Object System.Drawing.Point(20, 12)
$lblTitle.Size = New-Object System.Drawing.Size(420, 26)
$pnlHeader.Controls.Add($lblTitle)

$lblBadge = New-Object System.Windows.Forms.Label
$lblBadge.Text = "[ BRASIL 1:1 ]"
$lblBadge.Font = $fontSmall
$lblBadge.ForeColor = $cGreen
$lblBadge.Location = New-Object System.Drawing.Point(440, 16)
$lblBadge.Size = New-Object System.Drawing.Size(140, 20)
$lblBadge.TextAlign = "TopRight"
$pnlHeader.Controls.Add($lblBadge)

$lblSub = New-Object System.Windows.Forms.Label
$lblSub.Text = "Configuracao Rapida, Servidor, Taxas & Gerenciamento de Contas e GMs"
$lblSub.Font = $fontSubTitle
$lblSub.ForeColor = $cTextMuted
$lblSub.Location = New-Object System.Drawing.Point(22, 38)
$lblSub.Size = New-Object System.Drawing.Size(560, 20)
$pnlHeader.Controls.Add($lblSub)

# Linha dourada sutil separadora abaixo do header
$pnlDivider = New-Object System.Windows.Forms.Panel
$pnlDivider.Location = New-Object System.Drawing.Point(0, 67)
$pnlDivider.Size = New-Object System.Drawing.Size(620, 2)
$pnlDivider.BackColor = $cGoldDark
$pnlHeader.Controls.Add($pnlDivider)

# ==================== BARRA DE NAVEGACAO / ABAS CUSTOMIZADAS ====================
$pnlNavBar = New-Object System.Windows.Forms.Panel
$pnlNavBar.Location = New-Object System.Drawing.Point(20, 78)
$pnlNavBar.Size = New-Object System.Drawing.Size(565, 42)
$pnlNavBar.BackColor = $cBgMain
$form.Controls.Add($pnlNavBar)

$btnTab1 = New-Object System.Windows.Forms.Button
$btnTab1.Text = "Servidor e Rede"
$btnTab1.Location = New-Object System.Drawing.Point(0, 0)
$btnTab1.Size = New-Object System.Drawing.Size(185, 40)
$btnTab1.Font = $fontTab
$btnTab1.FlatStyle = "Flat"
$btnTab1.FlatAppearance.BorderSize = 0
$btnTab1.BackColor = $cCard
$btnTab1.ForeColor = $cGold
$btnTab1.Cursor = [System.Windows.Forms.Cursors]::Hand
$pnlNavBar.Controls.Add($btnTab1)

$btnTab2 = New-Object System.Windows.Forms.Button
$btnTab2.Text = "Contas e Game Master"
$btnTab2.Location = New-Object System.Drawing.Point(190, 0)
$btnTab2.Size = New-Object System.Drawing.Size(185, 40)
$btnTab2.Font = $fontTab
$btnTab2.FlatStyle = "Flat"
$btnTab2.FlatAppearance.BorderSize = 0
$btnTab2.BackColor = $cTabInactive
$btnTab2.ForeColor = $cTextMuted
$btnTab2.Cursor = [System.Windows.Forms.Cursors]::Hand
$pnlNavBar.Controls.Add($btnTab2)

$btnTab3 = New-Object System.Windows.Forms.Button
$btnTab3.Text = "Rates e Drops"
$btnTab3.Location = New-Object System.Drawing.Point(380, 0)
$btnTab3.Size = New-Object System.Drawing.Size(185, 40)
$btnTab3.Font = $fontTab
$btnTab3.FlatStyle = "Flat"
$btnTab3.FlatAppearance.BorderSize = 0
$btnTab3.BackColor = $cTabInactive
$btnTab3.ForeColor = $cTextMuted
$btnTab3.Cursor = [System.Windows.Forms.Cursors]::Hand
$pnlNavBar.Controls.Add($btnTab3)

# Container central de conteudo
$pnlContent = New-Object System.Windows.Forms.Panel
$pnlContent.Location = New-Object System.Drawing.Point(20, 126)
$pnlContent.Size = New-Object System.Drawing.Size(565, 480)
$pnlContent.BackColor = $cBgMain
$form.Controls.Add($pnlContent)

# ==================== PAINEL 1: SERVIDOR E REDE ====================
$pnlServidor = New-Object System.Windows.Forms.Panel
$pnlServidor.Location = New-Object System.Drawing.Point(0, 0)
$pnlServidor.Size = New-Object System.Drawing.Size(565, 480)
$pnlServidor.BackColor = $cBgMain
$pnlContent.Controls.Add($pnlServidor)

# Card: Nome do Servidor
$grpNome = New-Object System.Windows.Forms.Panel
$grpNome.Location = New-Object System.Drawing.Point(0, 0)
$grpNome.Size = New-Object System.Drawing.Size(565, 120)
$grpNome.BackColor = $cCard
$pnlServidor.Controls.Add($grpNome)

$lblCluster = New-Object System.Windows.Forms.Label
$lblCluster.Text = "Nome do Servidor (exibido na tela de login do Ragnarok 3):"
$lblCluster.Font = $fontSection
$lblCluster.ForeColor = $cGold
$lblCluster.Location = New-Object System.Drawing.Point(15, 12)
$lblCluster.Size = New-Object System.Drawing.Size(535, 20)
$grpNome.Controls.Add($lblCluster)

$txtCluster = New-Object System.Windows.Forms.TextBox
$txtCluster.Location = New-Object System.Drawing.Point(15, 36)
$txtCluster.Size = New-Object System.Drawing.Size(535, 26)
$txtCluster.Font = $fontInput
$txtCluster.BackColor = $cInputBg
$txtCluster.ForeColor = $cTextLight
$txtCluster.BorderStyle = "FixedSingle"
$txtCluster.Text = if ($jsonObj.Ro3Emu.ClusterName) { [string]$jsonObj.Ro3Emu.ClusterName } else { "Vox Emulador" }
$grpNome.Controls.Add($txtCluster)

# Box explicativo Onde Mudar Depois
$pnlOndeMudar = New-Object System.Windows.Forms.Panel
$pnlOndeMudar.Location = New-Object System.Drawing.Point(15, 70)
$pnlOndeMudar.Size = New-Object System.Drawing.Size(535, 40)
$pnlOndeMudar.BackColor = [System.Drawing.Color]::FromArgb(20, 30, 48)
$grpNome.Controls.Add($pnlOndeMudar)

$lblOndeMudar = New-Object System.Windows.Forms.Label
$lblOndeMudar.Text = "[DICA] Voce pode alterar este nome por aqui ou editando server\appsettings.json na chave 'ClusterName'."
$lblOndeMudar.Location = New-Object System.Drawing.Point(8, 8)
$lblOndeMudar.Size = New-Object System.Drawing.Size(518, 25)
$lblOndeMudar.ForeColor = [System.Drawing.Color]::FromArgb(56, 189, 248) # Cyan 400
$lblOndeMudar.Font = $fontSmall
$pnlOndeMudar.Controls.Add($lblOndeMudar)

# Card: Conexao e Rede
$grpRede = New-Object System.Windows.Forms.Panel
$grpRede.Location = New-Object System.Drawing.Point(0, 130)
$grpRede.Size = New-Object System.Drawing.Size(565, 175)
$grpRede.BackColor = $cCard
$pnlServidor.Controls.Add($grpRede)

$lblRedeTitle = New-Object System.Windows.Forms.Label
$lblRedeTitle.Text = "Parametros de Rede e Enderecos:"
$lblRedeTitle.Font = $fontSection
$lblRedeTitle.ForeColor = $cGold
$lblRedeTitle.Location = New-Object System.Drawing.Point(15, 12)
$lblRedeTitle.Size = New-Object System.Drawing.Size(535, 20)
$grpRede.Controls.Add($lblRedeTitle)

$lblArea = New-Object System.Windows.Forms.Label
$lblArea.Text = "Regiao / Canal (ex: Prontera, Brasil):"
$lblArea.Font = $fontSmall
$lblArea.ForeColor = $cTextMuted
$lblArea.Location = New-Object System.Drawing.Point(15, 38)
$lblArea.Size = New-Object System.Drawing.Size(255, 18)
$grpRede.Controls.Add($lblArea)

$txtArea = New-Object System.Windows.Forms.TextBox
$txtArea.Location = New-Object System.Drawing.Point(15, 58)
$txtArea.Size = New-Object System.Drawing.Size(255, 25)
$txtArea.Font = $fontInput
$txtArea.BackColor = $cInputBg
$txtArea.ForeColor = $cTextLight
$txtArea.BorderStyle = "FixedSingle"
$txtArea.Text = if ($jsonObj.Ro3Emu.AreaName) { [string]$jsonObj.Ro3Emu.AreaName } else { "Prontera" }
$grpRede.Controls.Add($txtArea)

$lblHost = New-Object System.Windows.Forms.Label
$lblHost.Text = "IP do Servidor (127.0.0.1 ou IP publico/LAN):"
$lblHost.Font = $fontSmall
$lblHost.ForeColor = $cTextMuted
$lblHost.Location = New-Object System.Drawing.Point(290, 38)
$lblHost.Size = New-Object System.Drawing.Size(260, 18)
$grpRede.Controls.Add($lblHost)

$txtHost = New-Object System.Windows.Forms.TextBox
$txtHost.Location = New-Object System.Drawing.Point(290, 58)
$txtHost.Size = New-Object System.Drawing.Size(260, 25)
$txtHost.Font = $fontInput
$txtHost.BackColor = $cInputBg
$txtHost.ForeColor = $cTextLight
$txtHost.BorderStyle = "FixedSingle"
$txtHost.Text = if ($jsonObj.Ro3Emu.PublicHost) { [string]$jsonObj.Ro3Emu.PublicHost } else { "127.0.0.1" }
$grpRede.Controls.Add($txtHost)

$lblHttpPort = New-Object System.Windows.Forms.Label
$lblHttpPort.Text = "Porta HTTP / Web Login (Padrao: 5380):"
$lblHttpPort.Font = $fontSmall
$lblHttpPort.ForeColor = $cTextMuted
$lblHttpPort.Location = New-Object System.Drawing.Point(15, 95)
$lblHttpPort.Size = New-Object System.Drawing.Size(255, 18)
$grpRede.Controls.Add($lblHttpPort)

$txtHttpPort = New-Object System.Windows.Forms.TextBox
$txtHttpPort.Location = New-Object System.Drawing.Point(15, 115)
$txtHttpPort.Size = New-Object System.Drawing.Size(255, 25)
$txtHttpPort.Font = $fontInput
$txtHttpPort.BackColor = $cInputBg
$txtHttpPort.ForeColor = $cTextLight
$txtHttpPort.BorderStyle = "FixedSingle"
$txtHttpPort.Text = if ($jsonObj.Ro3Emu.HttpPort) { [string]$jsonObj.Ro3Emu.HttpPort } else { "5380" }
$grpRede.Controls.Add($txtHttpPort)

$lblGamePort = New-Object System.Windows.Forms.Label
$lblGamePort.Text = "Porta Game Gate (Padrao: 33100):"
$lblGamePort.Font = $fontSmall
$lblGamePort.ForeColor = $cTextMuted
$lblGamePort.Location = New-Object System.Drawing.Point(290, 95)
$lblGamePort.Size = New-Object System.Drawing.Size(260, 18)
$grpRede.Controls.Add($lblGamePort)

$txtGamePort = New-Object System.Windows.Forms.TextBox
$txtGamePort.Location = New-Object System.Drawing.Point(290, 115)
$txtGamePort.Size = New-Object System.Drawing.Size(260, 25)
$txtGamePort.Font = $fontInput
$txtGamePort.BackColor = $cInputBg
$txtGamePort.ForeColor = $cTextLight
$txtGamePort.BorderStyle = "FixedSingle"
$txtGamePort.Text = if ($jsonObj.Ro3Emu.GameGatePort) { [string]$jsonObj.Ro3Emu.GameGatePort } else { "33100" }
$grpRede.Controls.Add($txtGamePort)

# Card: Conteudo e Opcoes
$grpConteudo = New-Object System.Windows.Forms.Panel
$grpConteudo.Location = New-Object System.Drawing.Point(0, 315)
$grpConteudo.Size = New-Object System.Drawing.Size(565, 150)
$grpConteudo.BackColor = $cCard
$pnlServidor.Controls.Add($grpConteudo)

$lblOptTitle = New-Object System.Windows.Forms.Label
$lblOptTitle.Text = "Opcoes de Temporada & Desbloqueios:"
$lblOptTitle.Font = $fontSection
$lblOptTitle.ForeColor = $cGold
$lblOptTitle.Location = New-Object System.Drawing.Point(15, 12)
$lblOptTitle.Size = New-Object System.Drawing.Size(535, 20)
$grpConteudo.Controls.Add($lblOptTitle)

$chkOpenContent = New-Object System.Windows.Forms.CheckBox
$chkOpenContent.Text = "Liberar todo o conteudo do jogo sem travas de temporada (Open Content)"
$chkOpenContent.Font = $fontNormal
$chkOpenContent.Location = New-Object System.Drawing.Point(15, 40)
$chkOpenContent.Size = New-Object System.Drawing.Size(535, 26)
$chkOpenContent.ForeColor = $cTextLight
$chkOpenContent.Checked = $true
$grpConteudo.Controls.Add($chkOpenContent)

$chkPass = New-Object System.Windows.Forms.CheckBox
$chkPass.Text = "Passe de Batalha e recompensas de eventos liberadas gratuitamente"
$chkPass.Font = $fontNormal
$chkPass.Location = New-Object System.Drawing.Point(15, 75)
$chkPass.Size = New-Object System.Drawing.Size(535, 26)
$chkPass.ForeColor = $cTextLight
$chkPass.Checked = $true
$grpConteudo.Controls.Add($chkPass)

# ==================== PAINEL 2: CONTAS E GAME MASTER ====================
$pnlContas = New-Object System.Windows.Forms.Panel
$pnlContas.Location = New-Object System.Drawing.Point(0, 0)
$pnlContas.Size = New-Object System.Drawing.Size(565, 480)
$pnlContas.BackColor = $cBgMain
$pnlContas.Visible = $false
$pnlContent.Controls.Add($pnlContas)

# Card: Modo Livre
$grpModoLivre = New-Object System.Windows.Forms.Panel
$grpModoLivre.Location = New-Object System.Drawing.Point(0, 0)
$grpModoLivre.Size = New-Object System.Drawing.Size(565, 52)
$grpModoLivre.BackColor = $cCard
$pnlContas.Controls.Add($grpModoLivre)

$chkGmOpen = New-Object System.Windows.Forms.CheckBox
$chkGmOpen.Text = "Modo Livre: Toda nova conta criada recebe poderes de GM automaticamente"
$chkGmOpen.Font = $fontNormal
$chkGmOpen.Location = New-Object System.Drawing.Point(15, 12)
$chkGmOpen.Size = New-Object System.Drawing.Size(535, 26)
$chkGmOpen.ForeColor = $cGold
$isGmOpen = $false
if ($jsonObj.Ro3Emu.PSObject.Properties['GmOpenWhenEmpty'] -ne $null) {
    $isGmOpen = [bool]$jsonObj.Ro3Emu.GmOpenWhenEmpty
}
$chkGmOpen.Checked = $isGmOpen
$grpModoLivre.Controls.Add($chkGmOpen)

# Card: Criar / Cadastrar Nova Conta Admin
$grpNovoAdmin = New-Object System.Windows.Forms.Panel
$grpNovoAdmin.Location = New-Object System.Drawing.Point(0, 60)
$grpNovoAdmin.Size = New-Object System.Drawing.Size(565, 140)
$grpNovoAdmin.BackColor = $cCard
$pnlContas.Controls.Add($grpNovoAdmin)

$lblCadTitle = New-Object System.Windows.Forms.Label
$lblCadTitle.Text = "Cadastrar / Promover Conta de Administrador (GM):"
$lblCadTitle.Font = $fontSection
$lblCadTitle.ForeColor = $cGold
$lblCadTitle.Location = New-Object System.Drawing.Point(15, 10)
$lblCadTitle.Size = New-Object System.Drawing.Size(535, 20)
$grpNovoAdmin.Controls.Add($lblCadTitle)

$lblNewUser = New-Object System.Windows.Forms.Label
$lblNewUser.Text = "Usuario / Login:"
$lblNewUser.Font = $fontSmall
$lblNewUser.ForeColor = $cTextMuted
$lblNewUser.Location = New-Object System.Drawing.Point(15, 34)
$lblNewUser.Size = New-Object System.Drawing.Size(170, 16)
$grpNovoAdmin.Controls.Add($lblNewUser)

$txtNewUser = New-Object System.Windows.Forms.TextBox
$txtNewUser.Location = New-Object System.Drawing.Point(15, 52)
$txtNewUser.Size = New-Object System.Drawing.Size(170, 25)
$txtNewUser.Font = $fontInput
$txtNewUser.BackColor = $cInputBg
$txtNewUser.ForeColor = $cTextLight
$txtNewUser.BorderStyle = "FixedSingle"
$txtNewUser.Text = "admin"
$grpNovoAdmin.Controls.Add($txtNewUser)

$lblNewPass = New-Object System.Windows.Forms.Label
$lblNewPass.Text = "Senha Desejada:"
$lblNewPass.Font = $fontSmall
$lblNewPass.ForeColor = $cTextMuted
$lblNewPass.Location = New-Object System.Drawing.Point(195, 34)
$lblNewPass.Size = New-Object System.Drawing.Size(160, 16)
$grpNovoAdmin.Controls.Add($lblNewPass)

$txtNewPass = New-Object System.Windows.Forms.TextBox
$txtNewPass.Location = New-Object System.Drawing.Point(195, 52)
$txtNewPass.Size = New-Object System.Drawing.Size(160, 25)
$txtNewPass.Font = $fontInput
$txtNewPass.BackColor = $cInputBg
$txtNewPass.ForeColor = $cTextLight
$txtNewPass.BorderStyle = "FixedSingle"
$txtNewPass.PasswordChar = '*'
$txtNewPass.Text = "admin123"
$grpNovoAdmin.Controls.Add($txtNewPass)

$btnAdicionarGm = New-Object System.Windows.Forms.Button
$btnAdicionarGm.Text = "+ Adicionar GM"
$btnAdicionarGm.Location = New-Object System.Drawing.Point(365, 48)
$btnAdicionarGm.Size = New-Object System.Drawing.Size(185, 32)
$btnAdicionarGm.Font = $fontTab
$btnAdicionarGm.BackColor = $cGreen
$btnAdicionarGm.ForeColor = [System.Drawing.Color]::White
$btnAdicionarGm.FlatStyle = "Flat"
$btnAdicionarGm.FlatAppearance.BorderSize = 0
$btnAdicionarGm.Cursor = [System.Windows.Forms.Cursors]::Hand
$grpNovoAdmin.Controls.Add($btnAdicionarGm)

$lblAdminExpl = New-Object System.Windows.Forms.Label
$lblAdminExpl.Text = "O servidor registra a conta no primeiro login: digite esse usuario e a senha desejada na tela de login do Ragnarok 3 para entrar como GM."
$lblAdminExpl.Font = $fontSmall
$lblAdminExpl.ForeColor = $cTextMuted
$lblAdminExpl.Location = New-Object System.Drawing.Point(15, 88)
$lblAdminExpl.Size = New-Object System.Drawing.Size(535, 42)
$grpNovoAdmin.Controls.Add($lblAdminExpl)

# Card: Lista de GMs e Botoes de Exclusao / Gerenciamento
$grpListaGm = New-Object System.Windows.Forms.Panel
$grpListaGm.Location = New-Object System.Drawing.Point(0, 208)
$grpListaGm.Size = New-Object System.Drawing.Size(565, 265)
$grpListaGm.BackColor = $cCard
$pnlContas.Controls.Add($grpListaGm)

$lblListaTitle = New-Object System.Windows.Forms.Label
$lblListaTitle.Text = "Contas com Privilegios de Game Master (GM):"
$lblListaTitle.Font = $fontSection
$lblListaTitle.ForeColor = $cGold
$lblListaTitle.Location = New-Object System.Drawing.Point(15, 10)
$lblListaTitle.Size = New-Object System.Drawing.Size(350, 20)
$grpListaGm.Controls.Add($lblListaTitle)

$lstGmAccounts = New-Object System.Windows.Forms.ListBox
$lstGmAccounts.Location = New-Object System.Drawing.Point(15, 34)
$lstGmAccounts.Size = New-Object System.Drawing.Size(350, 175)
$lstGmAccounts.Font = $fontInput
$lstGmAccounts.BackColor = $cInputBg
$lstGmAccounts.ForeColor = $cTextLight
$lstGmAccounts.BorderStyle = "FixedSingle"

$existingGms = @()
if ($jsonObj.Ro3Emu.GmAccounts) {
    $existingGms = @($jsonObj.Ro3Emu.GmAccounts)
}
if ($existingGms.Count -eq 0) {
    $existingGms = @("admin")
}
foreach ($item in $existingGms) {
    $trimmed = [string]$item.Trim()
    if ($trimmed -ne "") {
        [void]$lstGmAccounts.Items.Add($trimmed)
    }
}
if ($lstGmAccounts.Items.Count -gt 0) {
    $lstGmAccounts.SelectedIndex = 0
}
$grpListaGm.Controls.Add($lstGmAccounts)

# Botoes ao lado da Lista de GMs (Deletar, Limpar)
$btnDeletarGm = New-Object System.Windows.Forms.Button
$btnDeletarGm.Text = "Deletar Usuario"
$btnDeletarGm.Location = New-Object System.Drawing.Point(380, 34)
$btnDeletarGm.Size = New-Object System.Drawing.Size(170, 36)
$btnDeletarGm.Font = $fontNormal
$btnDeletarGm.BackColor = $cRed
$btnDeletarGm.ForeColor = [System.Drawing.Color]::White
$btnDeletarGm.FlatStyle = "Flat"
$btnDeletarGm.FlatAppearance.BorderSize = 0
$btnDeletarGm.Cursor = [System.Windows.Forms.Cursors]::Hand
$grpListaGm.Controls.Add($btnDeletarGm)

$btnLimparGms = New-Object System.Windows.Forms.Button
$btnLimparGms.Text = "Limpar Todos"
$btnLimparGms.Location = New-Object System.Drawing.Point(380, 78)
$btnLimparGms.Size = New-Object System.Drawing.Size(170, 32)
$btnLimparGms.Font = $fontNormal
$btnLimparGms.BackColor = $cCardBorder
$btnLimparGms.ForeColor = $cTextLight
$btnLimparGms.FlatStyle = "Flat"
$btnLimparGms.FlatAppearance.BorderSize = 0
$btnLimparGms.Cursor = [System.Windows.Forms.Cursors]::Hand
$grpListaGm.Controls.Add($btnLimparGms)

$lblDicaGm = New-Object System.Windows.Forms.Label
$lblDicaGm.Text = "Comandos GM no jogo:`n!item <id> [qtd]`n!zeny <qtd>`n!lvl <nv>`n!tele <mapa> <x> <y>`n!speed <vel>`n!heal / !kill"
$lblDicaGm.Font = $fontSmall
$lblDicaGm.ForeColor = $cTextMuted
$lblDicaGm.Location = New-Object System.Drawing.Point(380, 115)
$lblDicaGm.Size = New-Object System.Drawing.Size(170, 105)
$grpListaGm.Controls.Add($lblDicaGm)

$lblInfoExclusao = New-Object System.Windows.Forms.Label
$lblInfoExclusao.Text = "Para remover um GM, selecione o nome na lista e clique em 'Deletar Usuario'. O usuario perdera todos os privilegios administrativos de comandos."
$lblInfoExclusao.Font = $fontSmall
$lblInfoExclusao.ForeColor = $cTextMuted
$lblInfoExclusao.Location = New-Object System.Drawing.Point(15, 218)
$lblInfoExclusao.Size = New-Object System.Drawing.Size(535, 38)
$grpListaGm.Controls.Add($lblInfoExclusao)

# ==================== PAINEL 3: RATES E DROPS ====================
$pnlRates = New-Object System.Windows.Forms.Panel
$pnlRates.Location = New-Object System.Drawing.Point(0, 0)
$pnlRates.Size = New-Object System.Drawing.Size(565, 480)
$pnlRates.BackColor = $cBgMain
$pnlRates.Visible = $false
$pnlContent.Controls.Add($pnlRates)

# Card: Presets Rapidos
$grpPresets = New-Object System.Windows.Forms.Panel
$grpPresets.Location = New-Object System.Drawing.Point(0, 0)
$grpPresets.Size = New-Object System.Drawing.Size(565, 85)
$grpPresets.BackColor = $cCard
$pnlRates.Controls.Add($grpPresets)

$lblPresetTitle = New-Object System.Windows.Forms.Label
$lblPresetTitle.Text = "Presets Rapidos de Servidor:"
$lblPresetTitle.Font = $fontSection
$lblPresetTitle.ForeColor = $cGold
$lblPresetTitle.Location = New-Object System.Drawing.Point(15, 10)
$lblPresetTitle.Size = New-Object System.Drawing.Size(535, 20)
$grpPresets.Controls.Add($lblPresetTitle)

$btnPresetLow = New-Object System.Windows.Forms.Button
$btnPresetLow.Text = "Low Rate (1x Original)"
$btnPresetLow.Location = New-Object System.Drawing.Point(15, 36)
$btnPresetLow.Size = New-Object System.Drawing.Size(165, 34)
$btnPresetLow.Font = $fontNormal
$btnPresetLow.BackColor = $cInputBg
$btnPresetLow.ForeColor = $cTextLight
$btnPresetLow.FlatStyle = "Flat"
$btnPresetLow.FlatAppearance.BorderColor = $cCardBorder
$btnPresetLow.Cursor = [System.Windows.Forms.Cursors]::Hand
$grpPresets.Controls.Add($btnPresetLow)

$btnPresetMid = New-Object System.Windows.Forms.Button
$btnPresetMid.Text = "Mid Rate (25x)"
$btnPresetMid.Location = New-Object System.Drawing.Point(195, 36)
$btnPresetMid.Size = New-Object System.Drawing.Size(165, 34)
$btnPresetMid.Font = $fontNormal
$btnPresetMid.BackColor = $cInputBg
$btnPresetMid.ForeColor = $cTextLight
$btnPresetMid.FlatStyle = "Flat"
$btnPresetMid.FlatAppearance.BorderColor = $cCardBorder
$btnPresetMid.Cursor = [System.Windows.Forms.Cursors]::Hand
$grpPresets.Controls.Add($btnPresetMid)

$btnPresetHigh = New-Object System.Windows.Forms.Button
$btnPresetHigh.Text = "High Rate (100x)"
$btnPresetHigh.Location = New-Object System.Drawing.Point(375, 36)
$btnPresetHigh.Size = New-Object System.Drawing.Size(175, 34)
$btnPresetHigh.Font = $fontNormal
$btnPresetHigh.BackColor = $cInputBg
$btnPresetHigh.ForeColor = $cTextLight
$btnPresetHigh.FlatStyle = "Flat"
$btnPresetHigh.FlatAppearance.BorderColor = $cCardBorder
$btnPresetHigh.Cursor = [System.Windows.Forms.Cursors]::Hand
$grpPresets.Controls.Add($btnPresetHigh)

# Card: Seletores de Rates
$grpRatesSelect = New-Object System.Windows.Forms.Panel
$grpRatesSelect.Location = New-Object System.Drawing.Point(0, 95)
$grpRatesSelect.Size = New-Object System.Drawing.Size(565, 260)
$grpRatesSelect.BackColor = $cCard
$pnlRates.Controls.Add($grpRatesSelect)

$lblCustomRateTitle = New-Object System.Windows.Forms.Label
$lblCustomRateTitle.Text = "Multiplicadores de Progressao & Drop:"
$lblCustomRateTitle.Font = $fontSection
$lblCustomRateTitle.ForeColor = $cGold
$lblCustomRateTitle.Location = New-Object System.Drawing.Point(15, 12)
$lblCustomRateTitle.Size = New-Object System.Drawing.Size(535, 20)
$grpRatesSelect.Controls.Add($lblCustomRateTitle)

# Rate Exp Base
$lblRateBase = New-Object System.Windows.Forms.Label
$lblRateBase.Text = "Experiencia Base (EXP de Monstros e Quests):"
$lblRateBase.Font = $fontSmall
$lblRateBase.ForeColor = $cTextMuted
$lblRateBase.Location = New-Object System.Drawing.Point(15, 42)
$lblRateBase.Size = New-Object System.Drawing.Size(535, 18)
$grpRatesSelect.Controls.Add($lblRateBase)

$cmbRateBase = New-Object System.Windows.Forms.ComboBox
$cmbRateBase.Items.AddRange(@("1x (Original Oficial)", "2x (Dobre de EXP)", "5x (Moderado)", "10x (Acelerado)", "25x (Dinâmico)", "50x (Rápido)", "100x (Instantâneo)"))
$cmbRateBase.SelectedIndex = 0
$cmbRateBase.DropDownStyle = "DropDownList"
$cmbRateBase.Location = New-Object System.Drawing.Point(15, 62)
$cmbRateBase.Size = New-Object System.Drawing.Size(535, 28)
$cmbRateBase.Font = $fontInput
$grpRatesSelect.Controls.Add($cmbRateBase)

# Rate Exp Job
$lblRateJob = New-Object System.Windows.Forms.Label
$lblRateJob.Text = "Experiencia de Classe (Job / Habilidades):"
$lblRateJob.Font = $fontSmall
$lblRateJob.ForeColor = $cTextMuted
$lblRateJob.Location = New-Object System.Drawing.Point(15, 105)
$lblRateJob.Size = New-Object System.Drawing.Size(535, 18)
$grpRatesSelect.Controls.Add($lblRateJob)

$cmbRateJob = New-Object System.Windows.Forms.ComboBox
$cmbRateJob.Items.AddRange(@("1x (Original Oficial)", "2x (Dobre de Job)", "5x (Moderado)", "10x (Acelerado)", "25x (Dinâmico)", "50x (Rápido)", "100x (Instantâneo)"))
$cmbRateJob.SelectedIndex = 0
$cmbRateJob.DropDownStyle = "DropDownList"
$cmbRateJob.Location = New-Object System.Drawing.Point(15, 125)
$cmbRateJob.Size = New-Object System.Drawing.Size(535, 28)
$cmbRateJob.Font = $fontInput
$grpRatesSelect.Controls.Add($cmbRateJob)

# Rate Drops
$lblRateDrop = New-Object System.Windows.Forms.Label
$lblRateDrop.Text = "Chance de Drop de Equipamentos, Itens e Cartas:"
$lblRateDrop.Font = $fontSmall
$lblRateDrop.ForeColor = $cTextMuted
$lblRateDrop.Location = New-Object System.Drawing.Point(15, 170)
$lblRateDrop.Size = New-Object System.Drawing.Size(535, 18)
$grpRatesSelect.Controls.Add($lblRateDrop)

$cmbRateDrop = New-Object System.Windows.Forms.ComboBox
$cmbRateDrop.Items.AddRange(@("1x (Drop Oficial 1:1)", "2x (Drop Moderado)", "3x (Equilibrado)", "5x (Fácil)", "10x (Abundante)", "20x (Muito Alto)"))
$cmbRateDrop.SelectedIndex = 0
$cmbRateDrop.DropDownStyle = "DropDownList"
$cmbRateDrop.Location = New-Object System.Drawing.Point(15, 190)
$cmbRateDrop.Size = New-Object System.Drawing.Size(535, 28)
$cmbRateDrop.Font = $fontInput
$grpRatesSelect.Controls.Add($cmbRateDrop)

$lblRatesNote = New-Object System.Windows.Forms.Label
$lblRatesNote.Text = "Nota: Para a experiencia autentica e fiel de Ragnarok 3, mantenha os multiplicadores no modo Low Rate (1x)."
$lblRatesNote.Location = New-Object System.Drawing.Point(15, 226)
$lblRatesNote.Size = New-Object System.Drawing.Size(535, 25)
$lblRatesNote.ForeColor = $cTextMuted
$lblRatesNote.Font = $fontSmall
$grpRatesSelect.Controls.Add($lblRatesNote)

# Acoes dos Presets
$btnPresetLow.Add_Click({
    $cmbRateBase.SelectedIndex = 0
    $cmbRateJob.SelectedIndex = 0
    $cmbRateDrop.SelectedIndex = 0
})

$btnPresetMid.Add_Click({
    $cmbRateBase.SelectedIndex = 4 # 25x
    $cmbRateJob.SelectedIndex = 4  # 25x
    $cmbRateDrop.SelectedIndex = 3 # 5x
})

$btnPresetHigh.Add_Click({
    $cmbRateBase.SelectedIndex = 6 # 100x
    $cmbRateJob.SelectedIndex = 6  # 100x
    $cmbRateDrop.SelectedIndex = 5 # 20x
})

# ==================== NAVEGACAO ENTRE ABAS ====================
function Switch-Tab($activeTab) {
    $btnTab1.BackColor = $cTabInactive
    $btnTab1.ForeColor = $cTextMuted
    $btnTab2.BackColor = $cTabInactive
    $btnTab2.ForeColor = $cTextMuted
    $btnTab3.BackColor = $cTabInactive
    $btnTab3.ForeColor = $cTextMuted

    $pnlServidor.Visible = $false
    $pnlContas.Visible = $false
    $pnlRates.Visible = $false

    if ($activeTab -eq 1) {
        $btnTab1.BackColor = $cCard
        $btnTab1.ForeColor = $cGold
        $pnlServidor.Visible = $true
    } elseif ($activeTab -eq 2) {
        $btnTab2.BackColor = $cCard
        $btnTab2.ForeColor = $cGold
        $pnlContas.Visible = $true
    } elseif ($activeTab -eq 3) {
        $btnTab3.BackColor = $cCard
        $btnTab3.ForeColor = $cGold
        $pnlRates.Visible = $true
    }
}

$btnTab1.Add_Click({ Switch-Tab 1 })
$btnTab2.Add_Click({ Switch-Tab 2 })
$btnTab3.Add_Click({ Switch-Tab 3 })

# ==================== ACOES DE GERENCIAMENTO DE GMS / CONTAS ====================
# Adicionar GM
$btnAdicionarGm.Add_Click({
    $u = $txtNewUser.Text.Trim()
    if ($u -eq "") {
        [System.Windows.Forms.MessageBox]::Show(
            "Por favor, digite o nome de usuario para adicionar.",
            "Campo Vazio",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
        return
    }

    # Verificar se ja existe na lista
    $alreadyExists = $false
    foreach ($item in $lstGmAccounts.Items) {
        if ([string]$item -eq $u) {
            $alreadyExists = $true
            break
        }
    }

    if (-not $alreadyExists) {
        [void]$lstGmAccounts.Items.Add($u)
        $lstGmAccounts.SelectedItem = $u
        $chkGmOpen.Checked = $false
        [System.Windows.Forms.MessageBox]::Show(
            "Conta '$u' adicionada com sucesso a lista de Game Masters!`r`n`r`nLembre-se de digitar esse usuario e a senha na tela de login do Ragnarok 3.",
            "Conta GM Cadastrada",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information
        )
    } else {
        [System.Windows.Forms.MessageBox]::Show(
            "A conta '$u' ja esta cadastrada na lista de Game Masters.",
            "Aviso",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information
        )
    }
})

# Deletar GM Selecionado
$btnDeletarGm.Add_Click({
    if ($lstGmAccounts.SelectedIndex -ge 0) {
        $selectedUser = [string]$lstGmAccounts.SelectedItem
        $res = [System.Windows.Forms.MessageBox]::Show(
            "Deseja realmente remover os privilegios de GM do usuario '$selectedUser'?",
            "Confirmar Exclusao",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        if ($res -eq [System.Windows.Forms.DialogResult]::Yes) {
            $lstGmAccounts.Items.RemoveAt($lstGmAccounts.SelectedIndex)
            if ($lstGmAccounts.Items.Count -gt 0) {
                $lstGmAccounts.SelectedIndex = 0
            }

            # Verificar se existe banco ro3emu.db para oferecer reset de cadastro
            $dbCandidates = @(
                Join-Path $scriptDir "data\ro3emu.db",
                Join-Path $scriptDir "server\data\ro3emu.db"
            )
            $dbFound = $null
            foreach ($db in $dbCandidates) {
                if (Test-Path $db) {
                    $dbFound = $db
                    break
                }
            }

            $extraMsg = ""
            if ($dbFound -ne $null) {
                $dbRes = [System.Windows.Forms.MessageBox]::Show(
                    "Foi detectado o banco de dados do jogo em '$dbFound'.`r`n`r`nDeseja tambem excluir o cadastro de login deste usuario do banco para que ele possa ser recriado do zero?",
                    "Limpar Cadastro do Banco de Dados",
                    [System.Windows.Forms.MessageBoxButtons]::YesNo,
                    [System.Windows.Forms.MessageBoxIcon]::Question
                )
                if ($dbRes -eq [System.Windows.Forms.DialogResult]::Yes) {
                    try {
                        $sqliteCmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
                        if ($sqliteCmd) {
                            $safeUser = $selectedUser.Replace("'", "''")
                            & sqlite3 "$dbFound" "DELETE FROM auth_token WHERE account = '$safeUser'; DELETE FROM account WHERE name = '$safeUser';"
                            $extraMsg = "`r`n`r`nCadastro da conta tambem foi removido do banco de dados."
                        }
                    } catch {
                        $extraMsg = "`r`n`r`n(Nao foi possivel acessar sqlite3: $($_.Exception.Message))"
                    }
                }
            }

            [System.Windows.Forms.MessageBox]::Show(
                "Usuario '$selectedUser' removido com sucesso!$extraMsg",
                "Usuario Removido",
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Information
            )
        }
    } else {
        [System.Windows.Forms.MessageBox]::Show(
            "Selecione um usuario na lista para deletar.",
            "Nenhum Selecionado",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
    }
})

# Limpar Todos os GMs
$btnLimparGms.Add_Click({
    if ($lstGmAccounts.Items.Count -eq 0) { return }
    $res = [System.Windows.Forms.MessageBox]::Show(
        "Tem certeza que deseja remover todos os usuarios da lista de GMs?",
        "Limpar Lista",
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Warning
    )
    if ($res -eq [System.Windows.Forms.DialogResult]::Yes) {
        $lstGmAccounts.Items.Clear()
    }
})

# ==================== BOTOES INFERIORES ====================
$pnlFooter = New-Object System.Windows.Forms.Panel
$pnlFooter.Location = New-Object System.Drawing.Point(0, 615)
$pnlFooter.Size = New-Object System.Drawing.Size(620, 75)
$pnlFooter.BackColor = $cHeader
$form.Controls.Add($pnlFooter)

# Linha dourada sutil separadora acima do footer
$pnlFooterDivider = New-Object System.Windows.Forms.Panel
$pnlFooterDivider.Location = New-Object System.Drawing.Point(0, 0)
$pnlFooterDivider.Size = New-Object System.Drawing.Size(620, 2)
$pnlFooterDivider.BackColor = $cCardBorder
$pnlFooter.Controls.Add($pnlFooterDivider)

$btnSalvar = New-Object System.Windows.Forms.Button
$btnSalvar.Text = "SALVAR CONFIGURACOES"
$btnSalvar.Location = New-Object System.Drawing.Point(20, 14)
$btnSalvar.Size = New-Object System.Drawing.Size(275, 46)
$btnSalvar.BackColor = $cGreen
$btnSalvar.ForeColor = [System.Drawing.Color]::White
$btnSalvar.Font = $fontBtn
$btnSalvar.FlatStyle = "Flat"
$btnSalvar.FlatAppearance.BorderSize = 0
$btnSalvar.Cursor = [System.Windows.Forms.Cursors]::Hand
$pnlFooter.Controls.Add($btnSalvar)

$btnIniciar = New-Object System.Windows.Forms.Button
$btnIniciar.Text = "INICIAR O JOGO"
$btnIniciar.Location = New-Object System.Drawing.Point(310, 14)
$btnIniciar.Size = New-Object System.Drawing.Size(275, 46)
$btnIniciar.BackColor = $cBlue
$btnIniciar.ForeColor = [System.Drawing.Color]::White
$btnIniciar.Font = $fontBtn
$btnIniciar.FlatStyle = "Flat"
$btnIniciar.FlatAppearance.BorderSize = 0
$btnIniciar.Cursor = [System.Windows.Forms.Cursors]::Hand
$pnlFooter.Controls.Add($btnIniciar)

# Efeitos de Hover nos botoes
$btnSalvar.Add_MouseEnter({ $btnSalvar.BackColor = $cGreenDark })
$btnSalvar.Add_MouseLeave({ $btnSalvar.BackColor = $cGreen })
$btnIniciar.Add_MouseEnter({ $btnIniciar.BackColor = $cBlueDark })
$btnIniciar.Add_MouseLeave({ $btnIniciar.BackColor = $cBlue })

# Acao Salvar
$btnSalvar.Add_Click({
    try {
        Set-Prop $jsonObj.Ro3Emu "ClusterName" $txtCluster.Text.Trim()
        Set-Prop $jsonObj.Ro3Emu "AreaName" $txtArea.Text.Trim()
        Set-Prop $jsonObj.Ro3Emu "PublicHost" $txtHost.Text.Trim()
        Set-Prop $jsonObj.Ro3Emu "HttpPort" ([int]$txtHttpPort.Text.Trim())
        Set-Prop $jsonObj.Ro3Emu "GameGatePort" ([int]$txtGamePort.Text.Trim())
        Set-Prop $jsonObj.Ro3Emu "GmOpenWhenEmpty" ([bool]$chkGmOpen.Checked)

        # Processar Lista de GMs atualizada do ListBox
        $gmList = @()
        foreach ($item in $lstGmAccounts.Items) {
            $t = [string]$item.Trim()
            if ($t -ne "") {
                $gmList += $t
            }
        }
        Set-Prop $jsonObj.Ro3Emu "GmAccounts" $gmList

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

# Exibir Formulario
[void]$form.ShowDialog()
