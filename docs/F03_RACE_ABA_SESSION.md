# F03 — Condições de corrida, ABA e invalidação de sessão

## Escopo

Este marco protege o controlador **de pesquisa sobre fontes fictícias** contra
publicação de resultados defasados e operações simultâneas. Não habilita acesso
operacional ao FM2008 e não altera permissões de processos Windows.

## Políticas

1. Só uma chamada de `NextAsync` pode estar ativa por controlador.
2. `Restart` incrementa a geração lógica da sessão. Uma leitura pendente da
   geração anterior não pode publicar resultados nem modificar a nova sessão.
3. Uma falha fatal ou cancelamento invalida cursor, resultados e erros da
   geração afetada; a recuperação exige `Restart` e uma nova fonte.
4. Fontes `IsLiveProcessSource=true` continuam recusadas.
5. O diagnóstico isolado do FM2008 permanece estritamente somente leitura.
   `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` não foi ampliado.

## Testes simulados

- Duas chamadas de varredura simultâneas: a segunda é rejeitada; o cursor
  avança uma única vez.
- Reinício com leitura pendente: resultado antigo é descartado; novo resultado
  da nova fonte é aceito.
- Falha fatal depois de um lote previamente aceito: a sessão é invalidada e
  resultados/cursor anteriores são apagados.
- ABA A→B→A antes de uma observação: o guard de F02 ainda vê A e não consegue
  inferir a mudança intermediária. O teste demonstra **limitação**, não
  segurança comprovada.

## Limites e riscos residuais

O modelo de leitura não dispõe de contador monotônico de versão, lock do jogo
nem snapshot atômico fornecido pelo processo. A simples comparação de bytes,
mesmo repetida, **não prova ausência de ABA**. As verificações também não
asseguram consistência global da tabela nem detectam mutações feitas depois
da última observação.

As proteções de geração se aplicam ao controlador fictício; não se deve
generalizá-las automaticamente para `VerifiedPlayerReadSession` ou
`ReadOnlyProbe`. Para ampliar a garantia desses caminhos serão necessários
ensaios e revisão separados, preservando o bloqueio de mapas não aprovados.

**Decisão:** `approved=false`, `operational=false`; nenhuma escrita habilitada.
