# F17 — Plano de testes de aquisição excepcional e concorrência Windows local

## Estado inicial
- F16 integrado à `main` pelo PR #17, commit `8c50c37ba5b1c7fefc35c47196374127becba68c`.
- Quatro workflows de `push` pós-merge `completed/success` no mesmo SHA.
- Log `memory-simulation` pós-merge: 512 ciclos, 1.024 eventos locais, 1.024 tentativas e 1.024 conclusões de `Dispose`, 128 leases liberadas, 128 falhas e 128 cancelamentos. `baseline=214`, `maxBatchDelta=0`, `lastBatchDelta=0` nesta execução.
- F16 é sequencial; `Dispose` concluído não prova `CloseHandle` físico para todos os objetos; variação amostral de handles não prova ausência universal de vazamentos.

## Objetivo F17
Testar, **exclusivamente no processo do próprio teste Windows**, integridade de recursos em falhas entre aquisições e em interleavings controlados de lease, descarte e sinalização. Nenhum acesso a outros processos, incluindo `fm.exe`.

## Pacotes propostos de implementação (não implementados neste PR)
1. **Aquisição parcialmente concluída:** injetar exceção gerenciada antes/depois da criação do primeiro evento e entre a primeira e segunda aquisições. Usar `try/finally` separados e ledger com estados adquirido, descarte tentado, descarte concluído e liberação incerta. Exigir saldo de recursos efetivamente adquiridos zerado para caminhos sem exceções no descarte.
2. **Concorrência controlada:** usar `TaskCompletionSource` com `RunContinuationsAsynchronously` e barreiras com timeout limitado para forçar ordem de aquisição, lease, sinalização e `Dispose` entre tarefas. Não depender de `Thread.Sleep` ou sorteio para o oráculo. Não executar `WaitOne` em objeto já descartado como cenário de sucesso.
3. **Lease SafeHandle:** testar fechamento somente após a última `DangerousRelease`, e não inferir fechamento físico do mero retorno de `Dispose`. Garantir um único `DangerousRelease` para cada `DangerousAddRef` bem-sucedido, inclusive quando ocorre exceção em passos intermediários.
4. **Métricas auditáveis:** produzir por cenário aquisições, tentativas, conclusões, pendências/incertezas, leases ativas, contagem de handles por lote e pico de objetos simultâneos. Diferenciar tentativas de confirmação e evitar o denominador de aquisições fixo quando aquisição parcial falha.
5. **Testes negativos:** incluir falha proposital de um ledger fictício para comprovar que o oráculo detecta duplicidade de descarte, descarte sem aquisição, lease não liberada e fechamento excepcional não confirmado. Não simular `CloseHandle` real com ponteiro arbitrário.
6. **Recuperação isolada:** depois de cada falha simulada, abrir eventos novos e comprovar que o segundo objeto independente continua operacional; nenhum reaproveitamento de handle ou identidade descartada.

## Critérios de aceitação
- Testes Windows determinísticos e finitos na CI, com limites explícitos de ciclos, tempo e handles ativos.
- Oráculos independentes verificam cada caminho e distinguem sucesso de estados incertos; logs permitem revisão sem confundir tentativas com liberação física.
- Quatro workflows `push` e quatro `pull_request` aprovados no SHA exato do eventual PR de implementação.
- Revisão de concorrência, limpeza excepcional e integridade dos contadores antes de `Ready for review`; merge somente após nova autorização.
- Não alterar código operacional, APIs de abertura de processo, mapeamentos nem habilitar escrita.

## Restrições imutáveis
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`. Sem `fm.exe`, sem `OpenProcess`, `ReadProcessMemory`, `WriteProcessMemory`, enumeração de PID externo, detecção de mapas reais, habilitação operacional ou mudanças em `FM2008.mapping`.

## Riscos residuais
Mesmo com concorrência controlada de eventos Windows locais, não se demonstra ausência geral de vazamentos, ausência de todas as corridas ou robustez de leitura de memória nativa do jogo. A passagem pela CI mostra somente os cenários executados.

## Status
Planejamento do F17 somente: **nenhuma suíte F17 implementada** neste documento. Manter eventual PR em Draft até implementação, revisão e 4/4 workflows no HEAD.
