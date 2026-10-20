## 📝 Descrição da Alteração

Descreva de forma clara e objetiva o que este Pull Request altera (correção de bug, nova mecânica, melhoria de desempenho, documentação) e qual problema ele resolve. Se houver uma Issue associada, mencione-a aqui (ex: `Corrige #12`).

---

## 🔍 Evidências e Referências

- Para comportamentos de jogo, aponte os scripts Lua do cliente, mensagens protobuf, tabelas de configuração ou IDs de mensagens relevantes.
- Caso o comportamento do servidor oficial não possa ser provado diretamente pelos arquivos do cliente, marque como *(inferido)*.
- Descreva quaisquer limitações conhecidas da sua implementação.

---

## 🧪 Validação e Testes Realizados

- [ ] Compilação limpa do servidor (`dotnet build src/Ro3Emu.Server/Ro3Emu.Server.csproj -c Release`)
- [ ] Testes de patcher executados (`dotnet test tests/Ro3Emu.Patcher.Tests/Ro3Emu.Patcher.Tests.csproj -c Release`)
- [ ] Testado no cliente local com o jogo aberto
- [ ] Testes de integração do servidor executados (se possuir dados exportados)

> **Lembrete de Segurança:** Certifique-se de que nenhum arquivo proprietário do jogo, dados exportados completos, senhas ou tokens estejam presentes no diff deste PR.
