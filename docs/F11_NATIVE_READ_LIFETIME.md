# F11 — Falhas nativas, SafeHandle e integridade de leitura

## Escopo
Fortalecer exclusivamente a camada **somente leitura**. O catálogo de produção segue `NoApprovedPlayerMaps`; não há mapas aprovados nem acesso operacional a `fm.exe`.

## Contratos
- `NativeReadOnlyGuard` é testável offline e fica entre o adaptador Windows e as operações de leitura nativa.
- Cada leitura deve obter uma lease antes de invocar a API; uma segunda leitura no mesmo handle é recusada.
- `Dispose()` invalida imediatamente a sessão. Se existir leitura em andamento, a liberação do recurso fica diferida até ela terminar. Chamadas repetidas nunca liberam duas vezes.
- Retornos nativos `Success=false` rejeitam os bytes independentemente de `BytesRead`; a exceção é `Win32Exception(ErrorCode)`.
- Mesmo em `Success=true`, a contagem deve ser exatamente igual à solicitada. Leituras parciais, vazias e contagens excessivas são invalidadas.
- Identidade do handle verificada antes e após leitura; alteração de PID/horário de criação ou falha da inspeção invalida a sessão.
- Cancelamento depois de iniciar a operação nativa não fecha prematuramente o handle; não publica bytes e invalida a sessão quando a operação terminar.
- Nenhum resultado pode ser publicado depois de um `Dispose()` concorrente.
- Exceção em `GetLiveHandleIdentity()` fora de `ReadAsync()` invalida e fecha a sessão; se houver leitura em curso, adia o fechamento até seu término.
- Uma leitura pendente percebe `Dispose()` ou invalidação de outra chamada antes da segunda consulta de identidade, evitando reutilizar um recurso já condenado.
- Uma tentativa de fechamento que lança exceção continua marcada como tentativa única (não há nova chamada ao `CloseHandle` na mesma sessão).

## Testes determinísticos
`NativeReadGuardTests` verifica leituras completas, retorno nativo falso mesmo com contagem completa, falha Win32, leitura parcial, contagem 0 ou superior ao solicitado, exceção inesperada, identidade alterada, argumentos inválidos, cancelamento antes/durante leitura, chamadas concorrentes, `Dispose()` concorrentes, liberação única e isolamento/recuperação por outra sessão. Inclui falha da consulta de identidade isolada, falha da consulta antes/depois de leitura, invalidação concorrente durante leitura, `Dispose()` paralelo e callback de fechamento que lança exceção. Usa delegates e `TaskCompletionSource`, não relógio nem `Sleep`.

## Riscos residuais
- As chamadas Win32 e `SafeHandle` reais foram compiladas, mas não testadas com um processo externo. O acesso ao processo real continua bloqueado pela política de produção.
- As verificações de vivacidade não são atômicas com a leitura e `GetExitCodeProcess` tem ambiguidade conhecida do código 259.
- Uma API nativa que nunca retorna continuará ocupando a lease; `Dispose()` não deve fechar o recurso prematuramente.
- O buffer e a contagem nativa não garantem snapshot de memória atômico, nem eliminam ABA.
- O fechamento nativo em exceção fora da camada simulada exige revisão adicional antes de qualquer uso operacional.

## Política
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`; nenhuma chamada a `WriteProcessMemory`, nenhuma permissão `PROCESS_VM_WRITE`, sem promoção de mapas de pesquisa.
