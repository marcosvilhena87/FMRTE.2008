# F12 — Injeção determinística de falhas combinadas e recuperação

## Objetivo
Consolidar a proteção das fases F07, F10 e F11 executando cenários combinados do budget de leituras e da sessão com identidade associada ao handle **sem acessar nenhum processo nativo**.

## Matriz reproduzível
`CombinedFaultStressTests` executa 128 iterações fixas por modo e dez modos, totalizando **1.280 cenários determinísticos**:
0. Leitura completa.
1. `ReadProcessMemory` simulado falha mesmo alegando contagem completa.
2. Contagem de bytes parcial.
3. Contagem de bytes superior ao pedido.
4. Exceção na primeira inspeção de identidade.
5. Exceção na segunda inspeção de identidade.
6. Cancelamento com leitura pendente.
7. `Dispose()` repetido durante leitura pendente.
8. Duas leituras concorrentes com uma já ativa.
9. Falha na inspeção de identidade por outra chamada com leitura pendente.

Os modos 6–9 usam `TaskCompletionSource` para controlar explicitamente as interleavings; sem `Sleep`, relógio ou scheduling probabilístico. Cada cenário cria um handle fictício novo, passa-o por `NativeReadOnlyGuard` e `BudgetedReadOnlySession`, verifica a não publicação de dados inválidos e **uma única tentativa de fechamento por handle adquirido**. A recuperação usa um segundo handle fictício separado, nunca reabilita o primeiro.

## Revisão final — falhas encadeadas e isolamento paralelo
A auditoria identificou que os 1.280 cenários iniciais exercitavam uma falha por sessão seguida de recuperação bem-sucedida, sem cobrir tentativa de recuperação também falha. `RecoveryChainStressTests` acrescenta **384 cenários** (64 identidades × 6 modos) com:
- falha nativa, seguida de outra sessão com leitura parcial, seguida de terceira sessão nova que recupera;
- duas sessões independentes em uso paralelo enquanto uma leitura permanece pendente;
- fechamento da camada externa ou interna durante leitura pendente e cancelamento;
- falha de consulta isolada de identidade enquanto uma leitura está pendente;
- conferência de fechamento único e não publicação de resultado após invalidação.
A repetição usa seeds e modos explícitos, sem espera por relógio e sem escalonamento aleatório. Uma falha de sessão nunca reativa handles anteriores.

## Limites dos resultados
- Testes determinísticos cobrem interleavings enumerados, não o espaço infinito de threads e escalonamentos.
- `Dispose()` da camada de budget é intencionalmente diferido enquanto uma leitura está em voo.
- Nenhum teste de stress depende de APIs Win32 ou de `fm.exe`.
- Este marco não prova ausência universal de vazamentos, deadlocks ou efeitos ABA; chamadas nativas travadas continuam sendo um risco residual.
- A validação do F11 pós-merge no commit `1185633e9687799922c66053ecbb98a05907a313` teve HEAD idêntico confirmado, mas os quatro runs de push ainda não puderam ser observados pelo conector restrito a eventos de PR. Não apresentar esse silêncio como CI aprovada.
- O F12 não modifica políticas operacionais, catálogo de mapas, fingerprint de pesquisa ou direitos Win32.

## Política de segurança
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`. Sem `PROCESS_VM_WRITE`, `WriteProcessMemory`, acesso real a jogo ou promoção de mapeamentos experimentais.

## Critérios de integração
PR em draft; quatro workflows por HEAD com sucesso, análise de concorrência e comparação de diff de segurança antes de qualquer Ready for review ou merge.
