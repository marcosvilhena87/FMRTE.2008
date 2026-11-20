# F05 — Consistência de páginas simuladas

## Objetivo

Evitar que uma página de identidades seja publicada quando alterações
**observáveis** de cabeçalho, tabela, quantidade ou ponteiros ocorrerem
durante a decodificação. Toda a proteção opera sobre `IProcessReader`
injetado; os testes não abrem processos do jogo.

## Fluxo de leitura

1. Validar a página solicitada e capturar `PlayerListHeader`.
2. Ler e preservar os ponteiros de **todos os índices da página** (até 100).
3. Revalidar o cabeçalho.
4. Para cada jogador, reconferir o cabeçalho, decodificar sua identidade,
   verificar o ponteiro capturado e rejeitar IDs ou ponteiros repetidos
   dentro da mesma página. Reconferir cabeçalho e ponteiro após a decodificação.
5. Antes de publicar qualquer resultado, reconferir novamente o cabeçalho,
   **todos os ponteiros capturados** e o cabeçalho final.
6. Somente publicar uma lista de leitura estável após o retorno integral da
   função. Exceções e cancelamentos descartam todo o lote.

O `VerifiedPlayerReadSession` executa esse fluxo sob a proteção de
`GuardedReadAsync` introduzida no F04: falhas de leitura e cancelamento
invalidam a sessão e impedem sua reutilização.

## Casos de teste determinísticos

- Página estável com dois jogadores, nomes e página vazia.
- Mudança do endereço da tabela após o primeiro ID.
- Mudança da quantidade de jogadores após o primeiro ID.
- Mudança do ponteiro do segundo jogador antes de sua decodificação.
- Mudança **tardia do primeiro slot** durante o segundo registro.
- Leitura parcial do ID do último jogador.
- Cancelamento no último jogador.
- ID duplicado e ponteiro duplicado dentro da página.
- Descarte da sessão após falha, impossibilidade de reutilizá-la e
  recuperação apenas por nova sessão fictícia.
- Catálogo sem mapas aprovados impede abertura de processo.

## Riscos e limites

A conferência repetida de bytes **não é atômica** e não detecta toda
sequência ABA (A→B→A) ocorrida entre observações. A checagem cobre
somente os slots **da página requisitada**, não todos os jogadores do jogo.
IDs repetidos na mesma página são tratados como inconsistência conservadora;
isso não constitui prova sobre identidade estável fora da amostra.

Não foram modificadas permissões Windows nem introduzidas interfaces de escrita.
O `NoApprovedPlayerMaps` permanece ativo. Mapas de pesquisa não foram promovidos.

**approved=false; operational=false; somente leitura.**
