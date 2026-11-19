# Tela WPF de consulta de jogadores — modo demonstrativo

## Estado

A janela `MainWindow` possui uma grade com colunas **ID**, **Primeiro nome**, **Sobrenome**, **Nome comum**, todas de leitura apenas (`IsReadOnly=true`, sem inserção/remoção de linhas). A fonte exibida é `DemonstrationPlayerSource`, um conjunto **inerte e fixo** de seis registros adaptados dos exemplos observados. Esses registros **não são obtidos de uma partida em execução**.

- Pesquisa textual por nome/sobrenome/nome comum/nome completo, ignorando maiúsculas/minúsculas e diacríticos; IDs numéricos são pesquisados por igualdade exata.
- Paginação de três registros por página na interface; busca trabalha sobre snapshot inerte. Uma fonte operacional futura precisará implementar paginação na própria origem para não carregar a tabela inteira.
- Botões `Anterior`, `Próxima`, `Pesquisar`, `Limpar busca` e busca com Enter.
- Seleção opcional de `fm.exe` somente para identificar o **arquivo**, sem abrir processo, sem alterar fonte da grade.
- Status sempre indica build-alvo `8.0.2v1`, mapa **não aprovado**, fonte **demonstração**.

## Segurança

`PlayerQueryService` rejeita `IPlayerIdentitySource.IsLiveProcessSource == true` antes de pedir qualquer snapshot. O processo real não é aberto pela interface e `NoApprovedPlayerMaps` permanece catálogo operacional padrão. A tela não expõe escrita, edição de registros, acesso a arquivos de save ou controle de processo. Não conectar uma fonte real sem revisão formal do mapa e do fluxo de autorização.

## Executar no Windows (.NET 10 SDK)

```powershell
git status --short
git pull --ff-only
dotnet run --project .\tests\FMRTE2008.MemorySimulation\FMRTE2008.MemorySimulation.csproj -c Release
dotnet run --project .\src\FMRTE2008.Desktop\FMRTE2008.Desktop.csproj -c Release
```

Se a atualização for bloqueada por mudanças locais, preservar com `git stash push` antes de sincronizar (sem restaurar automaticamente o stash sobre arquivos novos).

## Testes de consulta

`PlayerQueryTests` verifica paginação, limites, busca por nome, sobrenome, nome comum, ID exato, texto com acentos, resultados vazios e recusa de uma fonte marcada como processo real. São testes com fixtures, **não** validações dinâmicas do FM2008.
