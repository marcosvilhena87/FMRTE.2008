# F18 — Plano: timeouts, limpeza excepcional e oráculo de recursos Windows locais

## Marco inicial e evidências
- F17 integrado à `main` via PR #18, SHA `f2831dc4aa0126e5ec6de2da6fe6a87e55337884`.
- Os quatro workflows pós-merge de `push` no SHA citado terminaram em `completed/success`: leitor x86, simulação de memória x86, diagnóstico somente leitura e compilação do protótipo Windows.
- O log da suíte Windows confirmou: F17, 512 cenários de aquisição parcial; 64 falhas modeladas de limpeza; 64 interleavings de lease/`Dispose`; seis controles negativos e um controle de fechamento independente; `maxHandleDelta=33` (limiar 64).
- A aprovação de CI verifica os cenários presentes, não garante ausência de vazamentos em todas as situações.

## Objetivo F18
Endurecer o teste de **interrupção/timeout e limpeza excepcional** quando tarefas ficam pendentes, sem confundir timeout do observador com término do trabalho. Provar, com oráculos independentes, que cada recurso local fica com estado conhecido ou incerteza explícita e que nenhuma tarefa pendente é abandonada durante teardown.

## Pacotes de implementação propostos
1. **Timeout por barreira com liberação garantida.** Fixtures de `TaskCompletionSource` em estados controlados com `RunContinuationsAsynchronously`. Para cada cenário, a rotina de limpeza deve sempre sinalizar a saída da tarefa, tentar aguardar sua conclusão e somente declarar ausência de pendência após observá-la. O timeout de `WaitAsync` não deve ser interpretado como cancelamento da tarefa subjacente.
2. **Separação dos estados do ledger.** Estados `Created`, `DisposeAttempted`, `DisposeCompleted`, `ReleaseUncertain` e `LeaseActive`. Fechamentos registrados somente após retorno efetivo; fechamento incerto não vira sucesso. Rejeitar transições duplicadas ou retroativas; medir recursos ativos e tentativas pendentes após cada estágio.
3. **Erros concorrentes e prioridade de diagnóstico.** Injetar falhas *gerenciadas e locais* na atividade, no callback de limpeza e no oráculo. Registrar separadamente falha primária e erro de limpeza, sem ocultar a primeira. Não induzir falha real do SO em `CloseHandle`.
4. **Corridas entre descarte e lease.** Organizar cronogramas reproduzíveis de tarefa A com lease, descarte do proprietário e tarefa B independente. Liberar todas as barreiras em `finally`, não usar `Thread.Sleep` como mecanismo de ordenação, e nunca operar um evento após descartá-lo.
5. **Recuperação sucessiva.** Em cada falha simulada, adquirir novos eventos próprios do processo e validar que não são reutilizados objetos invalidados. Confirmar contadores estáveis, incluindo cenários com duas falhas seguidas antes da recuperação.
6. **Recursos e variabilidade de CI.** Definir orçamento explícito de iterações, tarefas simultâneas e handles ativos; amostrar `Process.GetCurrentProcess().HandleCount` por lote após coleta e registrar linha de base, picos e deltas. Tratar a tolerância como detector grosseiro, não como prova física por handle.
7. **Testes negativos do oráculo.** Injectar propositalmente omissão de liberação da barreira, lease ativa, tentativa de fechamento duplicado, descarte retroativo e uma tarefa que produz exceção depois do sinal de cancelamento; exigir que o detector reprove o histórico, com teardown garantido.

## Critérios de aceitação
- Execução finita em runners Windows, com limite de duração por cenário e orçamento de recursos verificáveis; evitar hang da CI.
- Nenhuma tarefa pendente após teardown de cenário (verificada por conclusão real da `Task`); exceções observadas e contadas de forma independente.
- Oráculo identifica estados incertos sem afirmar indevidamente liberação física. Falhas modeladas devem preservar limpeza dos eventos reais criados pelo próprio teste.
- Os quatro workflows de `push` e os quatro de `pull_request` passam no HEAD exato da eventual implementação.
- Revisão explícita de fechamento excepcional, comportamento em timeout e concorrência, sem regressão do F16/F17.
- PR de implementação mantido em Draft até verificação 8/8 e revisão final. Nenhum merge nesta fase de planejamento.

## Segurança e restrições permanentes
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`. Não abrir, listar, ler ou escrever processos externos; nenhum acesso operacional a `fm.exe`, `OpenProcess`, `ReadProcessMemory`, `WriteProcessMemory`, nenhum mapa aprovado, nenhum contato com `FM2008.mapping`.

## Limites residuais
Testes locais de eventos kernel não comprovam a segurança de leituras do processo do jogo, a ausência de todos os interleavings, nem a liberação física sob falha real do SO. Qualquer conclusão deve ficar limitada aos cenários e aos logs auditados.

## Situação
**Somente planejamento F18**. Nenhuma suíte F18 foi implementada, aprovada ou integrada à `main` neste marco.
