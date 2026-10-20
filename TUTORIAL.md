# 📖 Guia Passo a Passo: Primeira Execução do Vox Emulador

Bem-vindo ao tutorial de primeira execução do **Vox Emulador**! Este guia foi feito para quem deseja rodar o emulador e abrir o cliente do **Ragnarok Online 3 (PC)** pela primeira vez em seu computador.

Se tiver qualquer dúvida durante o processo, venha para o nosso canal de suporte no Discord:
👉 **[Comunidade Discord Mundo Ragnarok](https://discord.gg/UvvJF4W3yK)**

---

## 📑 Sumário

1. [Visão Geral de Como Funciona](#1-visão-geral-de-como-funciona)
2. [Requisitos do Sistema](#2-requisitos-do-sistema)
3. [Instalação e Execução Fácil (Jogadores)](#3-instalação-e-execução-fácil-jogadores)
4. [Instalação e Execução via Código-Fonte (Desenvolvedores)](#4-instalação-e-execução-via-código-fonte-desenvolvedores)
5. [O que acontece na Primeira Execução](#5-o-que-acontece-na-primeira-execução)
6. [Criando sua Conta e Entrando no Jogo](#6-criando-sua-conta-e-entrando-no-jogo)
7. [Como Jogar com Amigos (Multiplayer)](#7-como-jogar-com-amigos-multiplayer)
8. [Perguntas Frequentes e Resolução de Problemas](#8-perguntas-frequentes-e-resolução-de-problemas)

---

## 1. Visão Geral de Como Funciona

O **Vox Emulador** é composto por:
1. **O Servidor do Emulador (`Ro3Emu.Server`):** Roda localmente na sua máquina escutando as portas 5380 (HTTP/Login), 20000 (Micro Gate / Contas) e 33100 (Game Gate / Mundo).
2. **O Cliente Ragnarok 3:** O jogo original para PC da versão Genesis.
3. **O Inicializador/Patcher (`RO3Emu.exe`):** Ele faz a ponte: localiza o jogo, exporta as tabelas necessárias de monstros, itens e habilidades, aplica um patch seguro e temporário no cliente para que ele se comunique com o seu servidor local (em vez dos servidores oficiais) e abre o jogo.

---

## 2. Requisitos do Sistema

- **Windows 10 ou 11 (64-bit)**
- **Processador:** Dual-core ou superior
- **Memória RAM:** 8 GB recomendados (mínimo de 4 GB livres)
- **Armazenamento:** 30 GB a 35 GB de espaço livre em disco (SSD recomendado para carregamento rápido de mapas)
- **Conexão com a Internet:** Apenas na primeira vez se você precisar que o instalador baixe os arquivos oficiais do jogo (~24 GB).

---

## 3. Instalação e Execução Fácil (Jogadores)

Se você só quer jogar localmente sem precisar compilar o código em C#:

### Passo 1: Baixar os arquivos do emulador
1. Acesse a página de lançamentos do projeto ou baixe o pacote **`RO3Emu-win-x64.zip`**.
2. Extraia o conteúdo do arquivo `.zip` em uma pasta de sua preferência (ex: `C:\VoxEmulador` ou `D:\Jogos\VoxEmulador`).
   > *Dica:* Evite caminhos com caracteres especiais ou pastas protegidas pelo sistema como `C:\Program Files`.

### Passo 2: Executar o `RO3Emu.exe`
1. Vá até a pasta onde extraiu e clique duas vezes em **`RO3Emu.exe`**.
2. **Aviso do Windows Defender SmartScreen:**
   - O Windows exibirá uma mensagem dizendo: *"O Windows protegeu o seu computador"*.
   - Isso é normal para softwares de código aberto independentes sem certificados digitais comerciais pagos.
   - Clique em **"Mais informações"** e em seguida no botão **"Executar assim mesmo"**.

### Passo 3: O Cliente do Jogo
Quando a janela preta do console abrir:
- **Cenário A (Você já tem o Ragnarok 3 instalado):**
  O programa tentará localizar automaticamente a pasta `ROO_PC` onde está o `ro3.exe`. Se não encontrar, ele solicitará no terminal que você digite ou cole o caminho completo da pasta do jogo.
- **Cenário B (Você ainda NÃO tem o jogo instalado):**
  O executável detectará a ausência do cliente e perguntará se você deseja que ele faça o download oficial dos servidores da Genesis.
  - Confirme pressionando `Enter` / `Y`.
  - Ele baixará os ~24 GB verificando cada arquivo com a assinatura oficial do distribuidor.

### Passo 4: Inicialização Automática
- Na primeira vez, o emulador levará cerca de **1 a 2 minutos** preparando as tabelas de dados, habilidades, navmeshes de monstros e aplicando o patch seguro nas DLLs do cliente (todas as DLLs originais são salvas intactas na pasta `ro3emu-backup`).
- Logo após, o console informará que os serviços estão online e **abrirá a janela do Ragnarok Online 3 automaticamente**.

---

## 4. Instalação e Execução via Código-Fonte (Desenvolvedores)

Se você é desenvolvedor e deseja modificar o código ou contribuir com correções de bugs:

1. **Instale o .NET 10 SDK:**
   - Baixe no site oficial da Microsoft: [Download .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. **Abra o repositório:**
   - Abra o terminal na pasta raiz deste repositório clonado.
3. **Execute o script de inicialização:**
   - No Prompt de Comando ou PowerShell:
     ```cmd
     start.cmd
     ```
   - O script compilará todas as bibliotecas (`Ro3Emu.Server`, `Ro3Emu.Setup`, `Ro3Emu.DataExport`, `Ro3Emu.ClientTools`) e executará o ambiente de desenvolvimento completo.

---

## 5. O que acontece na Primeira Execução

Durante o primeiro boot, duas pastas importantes serão criadas na raiz do emulador:
- `data/`: Armazena o banco de dados SQLite (`ro3emu.db`), suas contas, personagens, itens e dados exportados do cliente.
- `logs/`: Arquivos de log detalhados com as mensagens de depuração do servidor.
- Na pasta do jogo: O emulador cria uma subpasta `ro3emu-backup/` guardando as DLLs intactas do cliente.

---

## 6. Criando sua Conta e Entrando no Jogo

1. Quando o Ragnarok Online 3 carregar a tela de título e login:
2. No campo de **Usuário**, digite qualquer nome desejado (ex: `admin` ou `jogador1`).
3. No campo de **Senha**, digite uma senha de sua escolha.
4. Clique em **Entrar / Login**.
5. **Autocriação:** O emulador cria sua conta e armazena de forma segura no banco de dados SQLite local no primeiro login!
6. Escolha o servidor disponível na lista, crie seu personagem e comece sua jornada!

### 👑 Comandos de Administrador (GM)
No modo local solo, sua conta tem acesso imediato aos comandos de Game Master!
- Abra a caixa de chat no jogo (pressione `Enter`).
- Digite `!help` e pressione `Enter` para visualizar a lista completa de comandos disponíveis (teleporte, invocação de monstros, itens, alteração de nível, etc.).

---

## 7. Como Jogar com Amigos (Multiplayer)

### Se você for o Host (Dono da Sala):
1. Execute o comando no terminal na pasta do emulador:
   ```cmd
   RO3Emu.exe host --gm SuaContaPrincipal
   ```
2. O servidor exibirá o seu IP na tela.
3. Se seus amigos estiverem na mesma rede Wi-Fi/cabo ou utilizando redes virtuais (como Radmin VPN ou ZeroTier):
   - Apenas forneça o endereço IP exibido.
4. Se seus amigos forem se conectar via internet pública convencional:
   - Acesse as configurações do seu roteador de internet e redirecione (Port Forwarding) as seguintes portas TCP para o IP local do seu computador:
     - `5380` (HTTP)
     - `20000` (Micro Gate)
     - `33100` (Game Gate)
     - `33101` (Chat de Voz - se você ativou `--voice-port 33101`)

### Para os seus Amigos se Conectarem:
Seus amigos só precisam ter a pasta do emulador e executar:
```cmd
RO3Emu.exe join <IP_DO_HOST>
```
*Exemplo:* `RO3Emu.exe join 192.168.1.50` ou `RO3Emu.exe join meu-ip.exemplo.com:5380`

---

## 8. Perguntas Frequentes e Resolução de Problemas

### ❓ "O launcher oficial do Ragnarok 3 atualizou e o jogo parou de entrar no emulador"
Se você abrir o inicializador oficial da desenvolvedora, ele restaurará as DLLs originais. Para corrigir, basta fechar o jogo e rodar o `RO3Emu.exe` novamente; ele reaplicará as modificações necessárias automaticamente.

### ❓ "Como desfazer todas as alterações e deixar o jogo 100% original?"
Abra o terminal na pasta do emulador e digite:
```cmd
RO3Emu.exe restore
```
Isso copia os arquivos originais salvos na pasta `ro3emu-backup` de volta para o jogo, deixando-o exatamente como veio de fábrica.

### ❓ "O servidor fechou de repente ou deu erro"
Gere um pacote de diagnóstico executando:
```cmd
RO3Emu.exe diag
```
Ele gerará o arquivo `RO3Emu-diagnostics.zip`. Entre no nosso canal do Discord (`https://discord.gg/UvvJF4W3yK`) ou abra uma [Issue no GitHub](../../issues) anexando o arquivo.

---
Bom jogo e divirta-se desbravando o mundo de Rune-Midgard no Ragnarok Online 3 com o **Vox Emulador**!
