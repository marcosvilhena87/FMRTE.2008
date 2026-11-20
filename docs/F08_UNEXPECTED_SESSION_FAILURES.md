# F08 — Robustez contra falhas inesperadas e isolamento de sessões

## Objetivo
Ampliar o comportamento fail-closed de `VerifiedPlayerReadSession` sem abrir processos reais, sem promover mapas e sem ampliar permissões ou escrita.

## Alteração
Após adquirir o lease de leitura, **qualquer exceção** da operação interna invalida a sessão lógica. O bloco `finally` existente libera a fonte uma única vez, apenas quando não há outra leitura em andamento. Erros de argumentos da API e rejeição de leitura concorrente ocorrem antes da aquisição de trabalho interno e não mudam a política de abertura.

Durante `OpenAsync`, se o token for cancelado após o opener entregar o handle e antes da publicação da sessão, a abertura é rejeitada e o handle é descartado. A construção da sessão está protegida pelo mesmo bloco de limpeza.

## Testes F08 (somente fixtures)
- Exceções injetadas `InvalidOperationException`, `NotSupportedException` e `Exception` enquanto o leitor tenta obter o cabeçalho.
- A falha original é preservada, o handle fictício é liberado uma vez e tentativas posteriores são rejeitadas sem novas leituras.
- Uma nova sessão fictícia recupera independentemente, sem reaproveitar o estado anterior.
- Uma falha em uma sessão não contamina outra sessão simultaneamente aberta.
- Cancelamento ocorrido dentro do opener, imediatamente antes de entregar o handle: a sessão não é publicada e o handle é descartado uma única vez.
- `NoApprovedPlayerMaps` continua barrando o opener.
- A suíte anterior F04/F07 continua verificando cancelamento, orçamento e corridas de `Dispose()`.

## Limitações
Não foi demonstrada ausência universal de deadlocks, leitura atômica de páginas, resistência a ABA ou recuperação de um processo real. Um provedor que nunca conclui sua operação pode manter a liberação do handle pendente. A camada de processo real continua inacessível pelo catálogo padrão.

Não mudar `approved=false` nem `operational=false`. Nenhum `WriteProcessMemory`, nenhum save modificado, nenhuma promoção de offsets de pesquisa.
