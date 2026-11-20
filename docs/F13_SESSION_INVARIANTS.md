# F13 — Modelo de estados e invariantes de sessões (offline)

## Escopo
O F13 valida em fixtures determinísticas os contratos de ciclo de vida de `NativeReadOnlyGuard`. **Não** altera a implementação de produção nem habilita interação operacional com processos.

## Modelo de referência
Estados lógicos observáveis:
- **Ativa**: leitura completa e consulta de identidade são permitidas; handle ainda não foi fechado.
- **Leitura pendente**: o handle está reservado; uma segunda leitura é rejeitada. `Dispose()`, cancelamento e falha de identidade determinam diferentes saídas, sem fechamento antes da operação terminar.
- **Invalidada**: erro nativo ou erro de identidade impede qualquer nova operação. O handle é fechado exatamente uma vez depois de terminar a leitura pendente.
- **Descartada**: `Dispose()` torna as próximas operações inválidas e não deve liberar o mesmo recurso duas vezes.

Não há retorno de **Invalidada** ou **Descartada** para **Ativa**. Recuperação requer recurso independente.

## Exploração determinística
`StateInvariantModelTests` enumera todas as **3.125 sequências** de cinco passos sobre o alfabeto de cinco operações: leitura válida, leitura com erro nativo, falha de inspeção de identidade, `Dispose()` e inspeção de identidade válida. Em cada transição confere fechamento e proibição de atividades depois do estado terminal. Ao encerrar cada sequência faz `Dispose()` repetido e verifica novamente a rejeição de leituras.

Adicionalmente, são **384 interleavings controlados** (64 seeds × 6 modos) com leitura pendente: `Dispose()`, cancelamento, falha de leitura nativa, segunda leitura concorrente, exceção de identidade, e sessão independente em paralelo. A sincronização usa `TaskCompletionSource`, sem `Sleep` ou temporização probabilística.

## Invariantes testadas
1. Sessão invalidada ou descartada não publica novos bytes nem alcança provedor nativo.
2. Sessão ativa não fecha seu handle prematuramente.
3. Sessão terminada tenta fechar o recurso exatamente uma vez.
4. Operação pendente retém o handle até terminar.
5. Falha da inspeção de identidade invalida a sessão; segunda leitura concorrente é rejeitada.
6. Sessão independente pode avançar sem interferência de outra sessão pendente ou falha.

## Limitações
Trata-se de uma exploração exaustiva **somente dentro do alfabeto e profundidade delimitados**. Não demonstra cobertura de todos os interleavings, escalonamentos reais, travamentos nativos, ABA ou consistência atômica de memória.

O F12 foi confirmado integrado à `main` no commit `8b3ad1ecdd3cd02019562b3fb36edeac83bfb665`; o conector disponível não expõe execuções `push` pós-merge, de modo que os quatro workflows pós-merge permanecem **não confirmados**. Os quatro workflows do PR F12 foram aprovados antes da integração.

## Política operacional
`NoApprovedPlayerMaps` continua ativo, `approved=false`, `operational=false`; nenhum direito `PROCESS_VM_WRITE`, nenhuma API de escrita, nenhum uso de `fm.exe` real ou promoção de mapas experimentais.

## Gate do PR
Manter em Draft até 4/4 workflows no HEAD do F13 e revisão final dos testes, transições e políticas; não executar merge antecipadamente.
