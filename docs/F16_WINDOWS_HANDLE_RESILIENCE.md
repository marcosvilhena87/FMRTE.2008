# F16 — Robustez controlada de handles Windows locais

## Proveniência
Base `main` F15: `1f3b0c2bd716bbc2c68fe4687cc8385a4e57058a`, validada 4/4 pós-merge.
O F16 permanece inteiramente não operacional e é executado no Windows dentro do workflow de simulação.

## Falha inicial diagnosticada
A primeira versão falhou no step F16 com `System.Exception: F16 owner not marked closed`. Ela exigia `SafeHandle.IsClosed` imediatamente depois de `ManualResetEvent.Dispose()`, embora ainda houvesse uma referência adicional feita por `DangerousAddRef`. O teste foi corrigido para verificar fechamento **após** `DangerousRelease`. A lease é liberada em `finally`.

## Testes de robustez
- 512 ciclos sequenciais, dois eventos kernel locais por ciclo (1.024 aquisições).
- 128 leases em `SafeWaitHandle`, 128 falhas injetadas e 128 cancelamentos antes da operação.
- Ledger externo às instâncias testadas: IDs únicos de aquisição, registro de tentativa de descarte antes da chamada, registro de sucesso **apenas após retorno de `Dispose`**, rejeição de duplicatas, saldo zero após cada ciclo e total esperado.
- Verificação do estado de fechamento de `SafeHandle` depois da liberação da lease, sem acesso a ponteiros brutos ou operações no processo de jogo.
- Amostragem do número de handles do próprio processo a cada 64 ciclos após GC, com tolerância de aumento máximo de 64 sobre a linha de base para detectar deriva grosseira na CI.

## Limites
Um retorno de `Dispose` não prova individualmente que `CloseHandle` foi bem sucedido. `Process.HandleCount` pode flutuar por razões ambientais; amostragens finitas não provam ausência geral de vazamentos. Não há injeção real de falhas em `CloseHandle`, nem teste de concorrência nativa; o cancelamento é pré-sinalizado. O F15 contém testes de concorrência determinística em objetos fictícios.

## Segurança
Nenhum processo externo é aberto, enumerado ou acessado. Nenhuma leitura ou escrita de memória do jogo. Restrições: `NoApprovedPlayerMaps`, `approved=false`, `operational=false`; sem `WriteProcessMemory`, sem `fm.exe`, sem mudança em `FM2008.mapping`.

## Gate
PR Draft até o HEAD atualizado passar nos quatro workflows (push/PR), revisão final do ledger e da limpeza excepcional e ausência de conflitos. Não realizar merge nesta etapa.
