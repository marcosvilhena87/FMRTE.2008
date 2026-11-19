# Sessão de leitura somente leitura — build 8.0.2v1

## Integração

`VerifiedPlayerReadSession.OpenAsync`:
1. Rejeita PID inválido e executável não reconhecido.
2. Exige `IApprovedPlayerMapCatalog` e um mapa correspondente a SHA-256 e variante.
3. Rejeita mapas sem referência de aprovação, endereço de cabeçalho ou offset válido.
4. Somente então chama `IVerifiedProcessOpener`.
5. No Windows, `VerifiedWindowsProcessOpener` confere o executável do PID (`fm.exe`), seu hash, versão e arquitetura antes e depois da abertura somente leitura.
6. `ReadFirstPlayerIdAsync` decodifica header -> tabela -> índice zero -> ID com `PlayerListReader`.

O handle abre com `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION`, sem permissão de escrita; a classe implementa `IDisposable`.

## Bloqueio de produção

`NoApprovedPlayerMaps` é a única implementação de catálogo de produção disponível. Ela nunca devolve mapas. Os arquivos de pesquisa no `FM2008.mapping/research` têm `operational: false`, sem promoção ou validação dinâmica: **nenhum processo real será aberto por esse fluxo no estágio atual**.

Os testes usam um catálogo e processo fictícios (injetados), não concedem aprovação à build real.

## Para validar com jogo real futuramente

1. Confirmar endereço, semântica VA/RVA e offset do ID em uma build específica.
2. Obter evidências reproduzíveis e criar release de mapeamento com aprovação explícita, revisão e hash do jogo.
3. Implementar leitor de catálogo com verificação de integridade e autorização controlada.
4. Só então habilitar comando de leitura manual para um PID conferido; sem escrita ou congelamento de memória.

### Limites
A abertura do processo e a leitura não são transações atômicas; para cenários de segurança maior, vincular a sessão a identidade estável do processo e validar módulos/regiões antes de cada leitura. O código atual é uma infraestrutura de pesquisa, não um editor de memória pronto para produção.
