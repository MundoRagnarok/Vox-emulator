# Como Contribuir para o Vox Emulador 🇧🇷

Ficamos muito felizes pelo seu interesse em contribuir com o **Vox Emulador**! Este é um projeto aberto e movido pela comunidade brasileira com o objetivo de aprimorar a estabilidade, corrigir bugs e alcançar paridade com o servidor oficial do Ragnarok Online 3.

Toda ajuda é bem-vinda: correção de bugs no servidor C#, melhorias em scripts, relatórios detalhados de problemas, tradução de textos, testes de carga e documentação.

---

## 🌟 Diretrizes Gerais e Créditos

1. **Respeito ao Upstream:** O **Vox Emulador** é um fork comunitário baseado no trabalho de **[aethai/ro3emu-server](https://github.com/aethai/ro3emu-server)**. Sempre que encontrar ou resolver um problema que afete o projeto upstream, incentivamos o reporte e a colaboração mútua.
2. **Propriedade Intelectual e Segurança:**
   - **NUNCA** faça commit ou envie arquivos proprietários do cliente do jogo, DLLs protegidas da desenvolvedora, pastas `data/` exportadas completas ou credenciais.
   - O repositório já possui um `.gitignore` configurado para ignorar arquivos temporários e de exportação. Sempre revise seu `git status` e `git diff` antes de enviar.
3. **Comunicação:** Caso queira discutir uma ideia antes de implementar, abra uma **Issue** ou converse diretamente conosco no nosso canal de desenvolvedores no Discord:
   👉 **[Comunidade Discord Mundo Ragnarok](https://discord.gg/UvvJF4W3yK)**

---

## 🛠️ Ambiente de Desenvolvimento e Validação

Para compilar e validar alterações no código:

1. Instale o [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Para compilar os projetos principais sem precisar dos arquivos do cliente:
   ```powershell
   dotnet build src/Ro3Emu.Server/Ro3Emu.Server.csproj -c Release
   dotnet build tools/LuaDecode/LuaDecode.csproj -c Release
   dotnet build tools/ConfigExport/ConfigExport.csproj -c Release
   dotnet test tests/Ro3Emu.Patcher.Tests/Ro3Emu.Patcher.Tests.csproj -c Release
   ```
3. Se você possuir os dados exportados do jogo e quiser rodar os testes de integração do servidor:
   ```powershell
   dotnet test tests/Ro3Emu.Tests/Ro3Emu.Tests.csproj -c Release --filter "Category!=Slow"
   ```

---

## 🌿 Fluxo de Trabalho (Git Workflow)

1. **Fork do Repositório:** Crie um fork do projeto para a sua conta pessoal no GitHub.
2. **Clone:** Clone seu fork em sua máquina local usando o terminal ou o **GitHub Desktop**.
3. **Crie uma Branch:** Nunca envie alterações diretamente na branch `main`. Crie uma branch com nome descritivo:
   ```bash
   git checkout -b fix/correcao-monstro-spawn
   # ou
   git checkout -b feature/suporte-nova-skill
   ```
4. **Faça Commits Claros:** Escreva mensagens de commit objetivas em português:
   ```bash
   git commit -m "Corrige cálculo de dano mágico em alvos sob efeito de silêncio"
   ```
5. **Abra um Pull Request (PR):**
   - Envie a branch para o seu fork (`git push origin fix/correcao-monstro-spawn`).
   - Abra um Pull Request contra a branch `main` do repositório Vox Emulador.
   - Preencha o template de PR detalhando o problema resolvido, como testou e se há evidências no cliente (Lua/Protobuf).

---

## 🐛 Relatando Bugs e Propondo Melhorias

- **Para relatar um Bug:** Utilize a aba [Issues](../../issues) e selecione o modelo **Relato de Bug**. Descreva o passo a passo para reproduzir o erro, logs gerados e seu ambiente.
- **Para sugerir Recursos / Paridade Oficial:** Abra uma Issue utilizando o modelo **Sugestão de Funcionalidade**. Forneça o máximo de detalhes sobre como o comportamento funcionava ou deveria funcionar.

Agradecemos a todos que dedicam tempo para tornar o Vox Emulador e o Ragnarok Online 3 cada vez melhores!
