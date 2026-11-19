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

## Integração da interface com a pesquisa incremental

A janela WPF agora utiliza `IncrementalSearchController` ligado **exclusivamente** a `DemonstrationIndexedPlayerSource`. Ao abrir, examina o primeiro lote de três jogadores. `Ler próximo lote` avança mais três posições; `Anterior` e `Próxima` navegam entre as páginas de resultados **já examinados**, sem disparar leituras ocultas. `Pesquisar`, `Limpar busca` ou Enter reiniciam o cursor e consultam o primeiro lote do termo atualizado.

- A grade exibe a contagem de correspondências e o estado da varredura (índice examinado, total, falhas, conclusão).
- `Cancelar` sinaliza o `CancellationToken` da varredura em andamento; uma operação cancelada exige reinício explícito.
- `IncrementalSearchController` bloqueia a continuidade após erro fatal (`IOException`, `Win32Exception`, `ObjectDisposedException`, `InvalidOperationException`) ou cancelamento; `Restart` obtém nova fonte e zera cursor e resultados.
- Falhas isoladas de um registro são contabilizadas sem derrubar o restante da pesquisa; são apresentadas na contagem de falhas.
- Ao identificar `fm.exe`, a UI continua apenas lendo metadados do arquivo; **não** abre o processo do jogo e não usa o catálogo operacional.
- `IncrementalControllerTests` reproduz encerramento, interrupção e recuperação com uma fonte simulada; ainda não há teste real de fechamento do FM2008 em sessão aprovada.
- `PageSize=3` e lote de três servem à demonstração; o scanner permite até 100 entradas por chamada. A pesquisa não antecipa resultados que ainda não foram varridos.

**Limite:** esta integração não aprova o mapa mínimo e não implementa acesso à memória real. `NoApprovedPlayerMaps` permanece ativo.


## Validação de endereçamento e estabilidade (memória simulada)

Foi adicionado `StablePlayerIdentitySnapshotReader`, **somente uma classe experimental com `IProcessReader` injetado**, sem qualquer código de abertura de processos ou aprovação automática. Cada leitura:

1. Calcula `HeaderVA = ModuleBase + HeaderRVA` usando `MinimalPlayerIdentityMap.ResolveHeaderAddress`, com validação contra base nula e overflow x86.
2. Lê o cabeçalho (`tablePointer`, `count`) e o ponteiro da entrada desejada antes de decodificar o ID/nome.
3. Faz a decodificação limitada por índice e UTF-16.
4. Lê novamente o cabeçalho e o ponteiro da entrada; se algum deles mudou, **rejeita a amostra**, sem retornar identificação potencialmente incoerente.

`AddressStabilityTests` exercita duas bases fictícias (`0x00400000`, `0x08000000`), confirmando respectivamente `HeaderVA=0x01B1ED4C` e `0x0971ED4C`, além de base inválida, overflow, ponteiro nulo, região ausente, overflow de ponteiro, leitura parcial, alteração de cabeçalho/ponteiro no meio da leitura, interrupção da sessão, abertura de **nova** memória simulada e cancelamento.

**Limites:** as bases realocadas são sintéticas; os dois saves reais anteriores usaram a base preferencial `0x00400000`. Um cabeçalho/ponteiro estável antes/depois não comprova ausência de todas as mudanças intermediárias (problema ABA), nem consistência de todas as strings. Não há reconexão automática e nenhum teste desta etapa autoriza acesso operacional a `fm.exe`.

`NoApprovedPlayerMaps` permanece sem entradas; WPF continua ligada somente à demonstração e não permite edição.
