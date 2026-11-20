# F15 — Resistência prolongada e contabilidade de recursos simulados

## Escopo
O F15 testa somente `NativeReadOnlyGuard` com callbacks, identidades e recursos fictícios. Nenhuma camada de produção, processo real, mapa operacional ou escrita de memória é introduzida. Base `main`: F14 merge `e170e61379d434295f7e1451b5dc2f32bf1664a8`.

## Modelo independente de recursos
`LongRunResourceRecoveryTests` utiliza um ledger independente que registra cada recurso simulado adquirido e exige remoção exata no callback de fechamento. O ledger rejeita aquisição duplicada, fechamento duplicado e fechamento de recurso desconhecido. Ao final de cada estágio verifica que o número de ativos é zero e aquisições igualam fechamentos.

O ledger representa o sucesso de callbacks simulados de fechamento: **não é prova de ausência de vazamentos de handles reais do Windows**. Também não demonstra comportamento com `CloseHandle` falhando ou sendo interrompido pelo SO.

## Resistência determinística
- **10.000 ciclos** sequenciais e reproduzíveis, com **três recursos novos por ciclo**: primeira sessão falha, segunda sessão também falha e terceira sessão recupera a leitura.
- **30.000 aquisições** de recursos simulados, **20.000 falhas** encadeadas e **10.000 recuperações**, em identidades distintas e orçamento fixo.
- Erros alternados determinísticamente entre falha nativa, leitura parcial, exceção na primeira inspeção, exceção na segunda inspeção e cancelamento após entrada em leitura pendente.
- Cada etapa exige ausência de reutilização da sessão terminada, rejeição de leitura e inspeção após descarte, fechamento único e saldo zero de recursos ativos.
- **128 interleavings adicionais** usam dois recursos independentes e uma leitura pendente. `Dispose` invalida o primeiro recurso, mantém seu lease durante a pendência e não impede a segunda sessão de progredir. Ambos devem terminar com fechamento único.

## Limitações
A execução é finita, determinística e restrita aos casos definidos. Repetir ciclos não prova segurança contra toda condição de corrida, vazamentos de memória gerenciada, falhas do SO ou comportamento de `SafeHandle` nativo. Para falha do callback de fechamento, os testes F13/F14 existentes verificam tentativas únicas; esta suíte F15 mede fechamento bem-sucedido somente no simulador.

A validação do F14 pós-merge confirmou o commit da `main` e os arquivos de teste. Os quatro workflows disparados por `push` continuam **não verificáveis pelo conector disponível**, que retorna execuções associadas a pull requests.

## Restrições permanentes
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`; nenhuma permissão de escrita, nenhum `WriteProcessMemory`, nenhum acesso operacional ao `fm.exe` e nenhuma promoção de mapas experimentais.

## Gate
PR em Draft até 4/4 workflows aprovados no HEAD e revisão final de concorrência, integridade e orçamento de recursos. Não executar merge nesta etapa.
