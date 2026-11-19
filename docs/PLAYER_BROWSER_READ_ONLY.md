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


## Preparação da conexão operacional (não habilitada)

A classe `VerifiedPlayerReadSession` agora expõe `ReadIdentityPageAsync(pageNumber, pageSize)`, cujo acesso só é possível após `OpenAsync` validar a build exata, obter mapa por `IApprovedPlayerMapCatalog` e abrir a sessão por `IVerifiedProcessOpener`.

- O método verifica ID offset `+256`, variante e SHA da build `8.0.2v1`, usa `MinimalPlayerIdentityReader` e limita `pageSize` a 1..100.
- Somente os registros da página são decodificados. A lista inteira **não** é carregada na memória da interface.
- `NoApprovedPlayerMaps` continua retornando `null`: na versão distribuída, a abertura **sempre é recusada**.
- A interface exibe o controle de conexão em estado permanentemente desabilitado. Não existem botões que possam contornar a aprovação.
- `ApprovedIdentityPageTests` exercita a paginação via aprovação **fictícia em memória simulada**, além de verificar que o catálogo real bloqueia a abertura antes de chamar o opener.
- A busca global por nome/ID em uma fonte real ainda não existe. A pesquisa atual continua limitada à demonstração, pois a fonte aprovada futura exigirá uma estratégia incremental de busca (sem leitura ilimitada de todos os jogadores).

**Pendências de aprovação:** confrontar IDs no jogo com outra fonte, revisar gerenciamento de ciclo de vida de sessão, revalidar endereço base/ASLR no conector do Windows, testar falhas de leitura em páginas reais, revisar política de aprovação e então integrar explicitamente a tela com uma fonte paginada. Nenhuma dessas pendências é automaticamente resolvida pelos testes de fixture.


## Pesquisa incremental e tratamento de falhas (etapa experimental)

A classe `IncrementalPlayerSearch` implementa uma pesquisa retomável sobre `IIndexedPlayerSource`, com cursor `NextIndex`, lote limitado a **100 registros** e resultados parciais. `DemonstrationIndexedPlayerSource` fornece dados demonstrativos para uso em testes, sem processo real.

- A consulta recebe termo, índice inicial e tamanho do lote; devolve encontrados, falhas por índice, posição seguinte, contagem e sinal de conclusão.
- Busca por ID exato ou nomes com comparação sem diferenciação de maiúsculas e diacríticos.
- Erros locais de decodificação (`InvalidDataException`, `OverflowException` e `ArgumentOutOfRangeException`) são relatados como falhas do respectivo registro e o lote continua.
- Erros de leitura do processo (`IOException`, `Win32Exception`), descarte da sessão (`ObjectDisposedException`) ou cancelamento **não** são ignorados: a busca aborta e seu chamador deve descartar a sessão.
- Fontes com `IsLiveProcessSource == true` são rejeitadas antes de qualquer tentativa de leitura.
- Uma chamada processa apenas seu lote; a pesquisa global exige chamadas repetidas do cliente, sem construir previamente uma lista de milhares de jogadores.
- A janela WPF existente continua usando `PlayerQueryService` com uma lista fixa de demonstração; a pesquisa incremental está **no núcleo**, ainda não conectada à interface.

`IncrementalSearchTests` cobre cursor, limites, busca por ID/nome/acento, falha de registro, interrupção em falhas de processo e fonte real proibida. Nenhum teste usa processos externos.
