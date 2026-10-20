# Vox Emulador: Projeto Open-Source de Emulador para Ragnarok Online 3 (RO3) PC 🇧🇷

<p align="center">
  <img src="docs/assets/banner.svg" alt="Arquitetura do Vox Emulador e Mundo do Jogo" width="100%">
</p>

<p align="center">
  <em>Projeto open-source de emulador de Ragnarok Online 3 (RO3) PC em C# / .NET 10, baseado no ro3emu de aethai. Protocolo documentado pelo cliente, sistemas multiplayer, ferramentas, foco em correção de bugs e paridade com o jogo oficial pós-lançamento.</em>
</p>

<p align="center">
  <a href="https://discord.gg/UvvJF4W3yK"><img src="https://img.shields.io/badge/Discord-Vox%20Emulador-5865F2?logo=discord&logoColor=white&style=for-the-badge" alt="Discord da Comunidade"></a>
  <img src="https://img.shields.io/badge/C%23-.NET%2010-512BD4?logo=dotnet&logoColor=white&style=for-the-badge" alt=".NET 10">
  <img src="https://img.shields.io/badge/Licen%C3%A7a-GPL--3.0-blue?style=for-the-badge" alt="GPL-3.0">
  <img src="https://img.shields.io/badge/Status-Em%20Desenvolvimento-orange?style=for-the-badge" alt="Status">
</p>

<p align="center">
  <a href="#-sobre-o-projeto-e-créditos">Sobre e Créditos</a> ·
  <a href="#-nosso-foco-e-roadmap">Roadmap</a> ·
  <a href="#-tutorial-primeira-execução">Tutorial Passo a Passo</a> ·
  <a href="#-jogando-com-amigos">Jogar com Amigos</a> ·
  <a href="#-o-que-já-funciona">Sistemas Ativos</a> ·
  <a href="#-para-desenvolvedores-e-contribuidores">Como Contribuir</a> ·
  <a href="https://discord.gg/UvvJF4W3yK">Entrar no Discord</a>
</p>

---

## 📌 Sobre o Projeto e Créditos

O **Vox Emulador** é um **projeto aberto de emulador** voltado ao cliente PC de **Ragnarok Online 3 (Genesis)**, desenvolvido em C# (.NET 10 e SQLite) e mantido colaborativamente pela comunidade brasileira. O projeto permite rodar e estudar o jogo em seu próprio computador, realizar testes de sistemas multiplayer e apoiar na reconstrução dos sistemas do jogo.

> [!IMPORTANT]
> ### 🌟 Créditos ao Projeto Original (Upstream)
> O **Vox Emulador** é um **fork do projeto ro3emu** criado e documentado por **[aethai](https://github.com/aethai)** no repositório original:
> 👉 **[https://github.com/aethai/ro3emu-server](https://github.com/aethai/ro3emu-server)**
>
> Registramos nosso reconhecimento e agradecimento a **aethai** e a todos os pesquisadores e contribuidores do projeto original por toda a base técnica, protocolo documentado e engenharia reversa. O código segue sob os termos da licença livre **GNU General Public License v3.0 (GPL-3.0)**.

---

## 🎯 Nosso Foco e Roadmap

O desenvolvimento comunitário do **Vox Emulador** está estruturado em duas etapas claras:

1. **Fase 1: Correção de Bugs e Estabilidade (Fase Atual)**
   - Corrigir falhas de execução, exceções e incompatibilidades no emulador.
   - Aprimorar scripts de inicialização, ferramentas de diagnóstico e documentação em português do Brasil.
   - Facilitar para que jogadores e novos desenvolvedores consigam rodar o projeto localmente com facilidade.

2. **Fase 2: Paridade com o Servidor Oficial (Pós-lançamento)**
   - Após o lançamento oficial de Ragnarok Online 3 pela desenvolvedora/distribuidora, nosso objetivo principal será alcançar a **paridade 1:1** de pacotes, mecânicas de classes, monstros, instâncias, economia e combate com o servidor oficial.

---

## 🚀 Tutorial: Primeira Execução

Siga este tutorial passo a passo para configurar o emulador e abrir o Ragnarok Online 3 no seu computador pela primeira vez.

### 📋 Requisitos do Sistema

- **Sistema Operacional:** Windows 10 ou 11 (64-bit) ou Linux (x64).
- **Processador:** 2 núcleos ou superior.
- **Memória RAM:** Mínimo de 4 GB livres (2 GB para o servidor + memória para o cliente do jogo).
- **Espaço em Disco:** Cerca de 30 GB a 35 GB livres (para os arquivos do jogo de ~24 GB + dados do emulador).
- **Sem necessidade de pré-instalação de dependências adicionais:** A versão compilada já conta com o runtime do .NET embutido. (Caso queira compilar a partir do código-fonte, instale o [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)).

---

### Método 1: Executando pela Release Pré-Compilada (Recomendado para Jogadores)

1. **Baixe o Emulador:**
   - Faça o download do arquivo `RO3Emu-win-x64.zip` na seção de [Releases](https://github.com/aethai/ro3emu-server/releases/latest).
   - Extraia o conteúdo em uma pasta de sua escolha (ex: `C:\VoxEmulador\`).

2. **Execute o `RO3Emu.exe`:**
   - Dê dois cliques em **`RO3Emu.exe`**.
   - *Aviso do Windows Defender:* Como executáveis comunitários de código aberto não possuem certificado comercial pago, o SmartScreen pode exibir o alerta *"O Windows protegeu o seu computador"*. Clique em **Mais informações** e depois em **Executar assim mesmo**.

3. **O que acontece na Primeira Execução (Automático):**
   - **Se você já possui o jogo:** O executável tentará localizar a pasta do Ragnarok 3 (`ROO_PC` contendo `ro3.exe`) ou pedirá para você informar o caminho.
   - **Se você NÃO possui o jogo instalado:** O próprio executável se oferecerá para baixar o cliente diretamente dos servidores oficiais da Genesis (cerca de 24 GB) com validação de integridade.
   - **Preparação de dados:** O emulador levará cerca de 1 a 2 minutos para exportar tabelas de configuração, mapas e habilidades, aplicando o patch seguro nas DLLs do cliente (todas as DLLs originais são salvas intactas na pasta `ro3emu-backup`).
   - O emulador iniciará os serviços locais e abrirá a janela do Ragnarok Online 3 automaticamente!

4. **Entrando no Jogo:**
   - Na tela de login do Ragnarok 3, digite **qualquer usuário e senha**. A sua conta será criada automaticamente no primeiro login!
   - Mantenha a janela do console aberta enquanto joga. Para fechar o emulador com segurança, pressione **Q** na janela ou feche-a.

---

### Método 2: Executando a partir do Código-Fonte (Para Desenvolvedores)

Se você clonou este repositório para estudar o código ou implementar melhorias:

1. Instale o [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Abra um terminal na pasta raiz deste repositório.
3. Execute o script de inicialização:
   ```cmd
   start.cmd
   ```
   *(Ou no PowerShell: `.\start.ps1`)*
4. O script compilará a solução completa em modo Release (`Ro3Emu.Server`, `Ro3Emu.Setup`, etc.) e executará a inicialização completa.

---

### 🎮 Tabela de Comandos do Executável

| Comando | O que faz |
|---|---|
| `RO3Emu.exe` | Inicia o emulador local e abre o jogo automaticamente (equivalente a `start`) |
| `RO3Emu.exe join <endereço>` | Conecta o seu cliente ao servidor de um amigo (`IP`, `IP:porta` ou `http://IP:porta`) |
| `RO3Emu.exe host` | Inicia apenas o servidor dedicado para seus amigos entrarem (adicione `--gm <conta>` para dar poderes de GM) |
| `RO3Emu.exe install [pasta]` | Baixa o cliente limpo diretamente dos servidores oficiais verificando as assinaturas |
| `RO3Emu.exe restore` | Desfaz os patches no cliente do jogo, restaurando os arquivos originais de backup |
| `RO3Emu.exe check` | Verifica se a versão instalada do seu jogo é compatível com esta versão do emulador |
| `RO3Emu.exe diag` | Gera um arquivo `RO3Emu-diagnostics.zip` com logs limpos para anexar em relatórios de bugs |

> [!WARNING]
> **Atenção:** Nunca utilize o launcher oficial do jogo para "reparar" a pasta enquanto estiver usando o emulador, pois ele restaurará as DLLs originais. Caso isso aconteça, basta rodar o `RO3Emu.exe` novamente para reaplicar o patch.

---

## 🌐 Jogando com Amigos

### No Windows:
1. O anfitrião (Host) executa:
   ```cmd
   RO3Emu.exe host --gm SeuNomeDeUsuario
   ```
2. O console exibirá o endereço IP que seus amigos devem usar. O Windows solicitará permissão de firewall na primeira vez: clique em **Permitir acesso**.
3. Se os amigos estiverem na mesma rede local (ou via VPN/Radmin/Hamachi):
   ```cmd
   RO3Emu.exe join IP_DO_HOST
   ```
4. Se os amigos forem jogar pela internet via IP público, o anfitrião precisa redirecionar no roteador (Port Forward) as portas TCP:
   - **`5380`** (HTTP / Login / Lista de Servidores)
   - **`20000`** (Micro Gate / Contas e Social)
   - **`33100`** (Game Gate / Mundo do Jogo)
   - **`33101`** *(Opcional)*: Porta de chat de voz da guilda/grupo (ative com `--voice-port 33101`).

### No Linux (Servidor Dedicado):
Baixe o pacote `RO3Emu-server-linux-x64.tar.gz`, descompacte e execute:
```bash
./server.sh --client /caminho/para/ROO_PC --host <SEU_IP_PUBLICO> --gm <nome_da_conta_gm>
```

---

## 💡 Informações Úteis

- **Comandos de GM:** No modo local (jogando sozinho), **todas** as contas têm comandos de Game Master liberados. Digite `!help` no chat do jogo para ver a lista de comandos. Em modo compartilhado (`host`), apenas contas passadas pelo parâmetro `--gm <conta>` têm acesso de GM.
- **Backups Automáticos:** O banco de dados SQLite é salvo automaticamente em `data/backups/` na inicialização e a cada 6 horas (mantendo os últimos 20 backups).
- **Uma sessão por conta:** O cliente suporta 1 instância por PC. Para simular múltiplos jogadores em testes, utilize a ferramenta `Ro3Emu.BotCli`.

---

## 🛡️ O que já Funciona

O emulador responde ao cliente exatamente como o servidor original: mesmos pacotes, ordem e regras comprovadas pela engenharia reversa do cliente.

<table>
<tr><td valign="top" width="50%">

### 🌍 Mundo e Personagens
- Login, seleção de servidor, criação e exclusão de personagens.
- Mapas, portais, NPCs, teleporte de Kafra, asas e canais de mapa.
- Monstros comuns e MVPs com invocações, campo de visão e renascimento.
- Reconexão silenciosa, emotes, sentar e descansar.
- Árvores de habilidades, buffs, debuffs, dano contínuo (DoT), curas e auras.
- Sistema de fadiga de crowd control, ressonância de classe e talentos.
- Nível máximo 99 com sistema de bônus de EXP.

### ⚔️ Itens e Progressão
- Equipamentos, identificação, afixos, encantamento, aprimoramento e refino.
- Criação (crafting) com contadores de garantia, caixas de recompensa e escolha.
- Cartas (estrelas de coleção, regras de inserção, cartas Flash e efeitos).
- Soul Echo, sistema de mascotes e incubadora, montarias, guarda-roupa e totens.
- Conquistas, codex, sala de treinamento e metas de 7 dias.

</td><td valign="top" width="50%">

### 🤝 Social e Economia
- Centro de trocas, lojinhas de jogadores, leilões (guilda, mundo e eventos), correio.
- Lojas de NPC, loja de pontos, loja de guilda e passe de batalha.
- Sistema de amigos, presentes, grupos e incursões (raids), canais de chat e cornetas globais.
- Chat de voz de grupo nativo pelo servidor.

### 🏰 Guildas e Instâncias
- Gerenciamento de guilda, diário, recrutamento, fundação e sede da guilda.
- Festa da fogueira, caravana, mercado negro e desafio de Ilmata.
- Liga de Guildas e Guerras de Território (GvG / Guerra do Emperium básica).
- Calabouços de história e cenas roteirizadas.
- Torre dos Espíritos, Reino Fantasma, Provas de Valhalla e Reino dos Deuses.
- Chefes com fases, invocações e habilidades especiais.
- Batalhas de sobrevivência, crise das fendas, caçadas e eventos cronometrados.

</td></tr>
</table>

---

## 🛠️ Para Desenvolvedores e Contribuidores

Deseja colaborar com correções de bugs, código ou documentação no **Vox Emulador**? Todas as contribuições são muito bem-vindas!

### Estrutura do Repositório

| Diretório | Responsabilidade |
|---|---|
| `src/Ro3Emu.Server` | Servidor principal: HTTP, micro gate, game gate, loop do jogo, serviços e banco SQLite |
| `src/Ro3Emu.Setup` | Executável `RO3Emu.exe`: instalador, exportador de dados, inicializador do servidor e patcher |
| `src/Ro3Emu.DataExport` | Exportador de dados do cliente (máquina virtual Lua, leitor de navegação, mapas) |
| `src/Ro3Emu.ClientTools` | Utilitários de patch e restauração de DLLs do cliente |
| `src/Ro3Emu.ClientHooks` · `src/Ro3Emu.Loc` | Código injetado no cliente (interceptador de rede, voz de grupo, textos) |
| `src/Ro3Emu.Bot` · `src/Ro3Emu.BotCli` | Bots de teste de carga e simulação de jogadores |
| `tests/` | Bateria de testes unitários e de integração de rede e banco de dados |
| `tools/` | Ferramentas de descompilação de Lua, exportação de tabelas e empacotamento de releases |
| `docs/` | Guias completos, notas técnicas de sistemas e relatórios de engenharia reversa |

### Como Contribuir:
1. Faça um **Fork** deste repositório para o seu perfil no GitHub.
2. Clone o seu fork no seu computador (via terminal ou **GitHub Desktop**).
3. Crie uma branch para a sua alteração: `git checkout -b fix/correcao-bug` ou `git checkout -b feature/minha-melhoria`.
4. Valide a compilação:
   ```powershell
   dotnet build src/Ro3Emu.Server/Ro3Emu.Server.csproj -c Release
   dotnet test tests/Ro3Emu.Patcher.Tests/Ro3Emu.Patcher.Tests.csproj -c Release
   ```
5. Faça o commit com mensagens claras em português.
6. Envie para o seu fork e abra um **Pull Request (PR)** detalhando o que foi alterado e como testar.

Consulte o nosso guia detalhado em [CONTRIBUTING.md](CONTRIBUTING.md) e o nosso [Código de Conduta](CODE_OF_CONDUCT.md).

---

## 💬 Comunidade e Discord

Venha tirar dúvidas, compartilhar logs, organizar jogatinas e ajudar a desenvolver o projeto de emulador:

👉 **[Entre no nosso Discord do Vox Emulador](https://discord.gg/UvvJF4W3yK)**

---

## ⚖️ Licença

O Vox Emulador é um software livre distribuído sob os termos da [GNU General Public License v3.0 (GPL-3.0)](LICENSE).

## ⚠️ Aviso Legal (Disclaimer)

O projeto Vox Emulador é uma pesquisa independente e um projeto educacional de interoperabilidade. Não possui qualquer afiliação, vínculo ou endosso da Gravity Co., Ltd. ou de quaisquer distribuidoras oficiais de Ragnarok Online. Este repositório não inclui, hospeda ou distribui arquivos de propriedade intelectual do cliente do jogo, artes ou dados proprietários. Você deve possuir sua própria cópia do cliente e é responsável pelo cumprimento das leis e termos aplicáveis. *Ragnarok Online* é uma marca registrada de Gravity Co., Ltd.
