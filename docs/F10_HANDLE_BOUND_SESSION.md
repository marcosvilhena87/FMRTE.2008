# F10 — Vínculo entre processo, handle e sessão

## Objetivo
Aprofundar F09 por **identidade lida do handle adquirido**, sem habilitar mapas, leitura operacional ou escrita. Verificações por PID continuam existentes, mas a abertura Windows agora exige também `IProcessHandleBoundReadSession`.

## Contrato defensivo
- Após adquirir um handle de processo, consultar PID e horário de criação **do próprio handle** e comparar à identidade observada antes e depois da aquisição.
- Um handle sem prova, com PID diferente, criação diferente, fechado ou inacessível é rejeitado e descartado.
- O leitor Windows verifica identidade e estado do processo antes e depois da leitura; exceções ou encerramento detectável não publicam bytes.
- O fluxo de sessão dos marcos F04–F09 continua sendo responsável por invalidar a sessão lógica após falha e evitar descartes prematuros.
- Não reutilizar handles entre sessões ou saves; recuperação somente por nova abertura.
- O teste de compatibilidade F09 mantém assinatura padrão da etapa simulada; o opener Windows explicitamente exige prova de handle.

## Evidência offline
`HandleBindingTests`: aceitação de handle correto, rejeição de handle não vinculado, PID trocado, início de processo divergente, morte detectada, mudança entre inspeções, cancelamento pendente, duas sessões com PID igual e instâncias distintas, interrupção no meio da leitura e recuperação por nova sessão.

`HandleLifecycleRaceTests` cobre adicionalmente o caminho completo `VerifiedPlayerReadSession` com leitura em andamento e encerramento do processo, duas chamadas concorrentes, `Dispose()` simultâneos, cancelamento e isolamento de outra sessão. Confirma que nenhum resultado pendente é publicado após falha, que o descarte é único e que uma sessão independente permanece utilizável.

Testes usam somente objetos injetados e sincronização controlada por `TaskCompletionSource`. Nenhum acesso real a `fm.exe`.

## Limitações e riscos residuais
- `GetProcessId`, `GetProcessTimes` e `GetExitCodeProcess` interrogam o handle real apenas no adaptador Windows; este comportamento não está validado por testes de integração com processo externo.
- O estado `STILL_ACTIVE=259` de `GetExitCodeProcess` pode coincidir com um código de saída de valor 259. Essa ambiguidade impede alegar prova absoluta de processo vivo; falhas do leitor devem continuar fail-closed.
- O uso de tempo de criação + PID não constitui identidade criptográfica. As inspeções antes/depois não são instantâneas, e não há garantia universal contra ABA dentro da memória do jogo.
- Uma saída após a última checagem não pode invalidar dados já devolvidos. Sem transação ou snapshot atômico.
- `GetProcessTimes` não usa a data de saída indefinida do processo ainda vivo para inferir estado.
- Nenhuma permissão de escrita, `PROCESS_VM_WRITE`, `WriteProcessMemory` ou mapa promovido.

## Segurança
`NoApprovedPlayerMaps` permanece como bloqueio operacional. **approved=false, operational=false, somente leitura**. PR separado, revisão de diff e quatro workflows obrigatórios antes de considerar integração.
