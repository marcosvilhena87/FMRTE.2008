# F16 — Robustez controlada de handles Windows locais

## Base e propósito
Base: F15 integrado na main em `1f3b0c2bd716bbc2c68fe4687cc8385a4e57058a`, com os quatro workflows pós-merge de push concluídos com sucesso. F16 complementa os modelos de recursos fictícios com objetos de sincronização **criados exclusivamente pelo processo de teste**. Não se conecta ao jogo.

## Suíte Windows
`tests/FMRTE2008.WindowsResilience` é um executável .NET 10 restrito a Windows e executado por `memory-simulation.yml`. Faz 512 iterações finitas, cada qual criando dois `ManualResetEvent` independentes (1.024 objetos kernel no total, sem simultaneidade de 1.024 handles).
- A cada quarta iteração, testa uma referência `SafeWaitHandle.DangerousAddRef` adquirida antes do descarte do proprietário; libera a lease em `finally`. Não usa `DangerousGetHandle` nem efetua operações nativas por ponteiro.
- Outros modos exercitam falha injetada e cancelamento pré-sinalizado, verificando que a limpeza é mantida.
- Verifica contabilidade lógica independente de aquisições e descartes de objetos gerenciados.
- Compara `Process.GetCurrentProcess().HandleCount` antes e depois, após GC, com tolerância de 64 handles para variações de CI. Uma medição final e esse limiar detectam apenas crescimento grosseiro, não provam ausência de vazamentos.
- Não abre processos, enumera PIDs, interage com `fm.exe`, nem usa qualquer mapa de jogadores.

## Restrições preservadas
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`. Nenhuma escrita, nenhuma autorização operacional, nenhum `WriteProcessMemory` e nenhuma modificação no repositório `FM2008.mapping`.

## Limitações e critérios de revisão
Esta etapa mede eventos Windows criados pelo próprio teste, **não** o comportamento da camada real de abertura de processo, dos callbacks de leitura nativa ou do `CloseHandle` sob falhas externas. As iterações são sequenciais; a suíte F15 cobre interleavings simulados. A comparação de contagem é ruidosa e seu limite não é orçamento de produção. Reavaliar limites após séries de runs em runners Windows e acrescentar cenários realmente paralelos somente com oráculo determinístico e sem dependências do jogo.

Manter o PR em Draft até revisão de segurança, execução 4/4 workflows no HEAD do PR, análise dos logs e revisão de concorrência/contabilidade. Não executar merge nesta etapa.
