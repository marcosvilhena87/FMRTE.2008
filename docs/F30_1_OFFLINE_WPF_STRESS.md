# F30.1 — Resistência WPF determinística (somente offline)

## Escopo
Exercita a janela WPF em processo STA, sem execução do FM2008, fontes externas,
PID, API nativa de memória, offsets, saves ou gravação. O cenário fechado
`OfflineRosterScenario.CollisionStress` contém exclusivamente seis jogadores
inventados, incluindo colisões deliberadas de nome e identificador textual.

## Protocolo reprodutível
`F30Stress.Run` é executado no mesmo binário Windows de testes F29, após as
verificações de regressão existentes. Executa exatamente 120 ciclos, sem
aleatoriedade nem temporizadores, incluindo pesquisas textuais, nomes
duplicados, seleção por `DEMO-NNN`, filtro exato por `CLUB-NNN`, limites
anterior/próximo, lista vazia e limpeza integral dos detalhes. A cada dez
ciclos, injeta erro recuperável, falha persistente de reset, segunda tentativa
falha e recuperação por uma nova tentativa explícita. Falhas interrompem
a execução com número do ciclo e invariante violado.

## Limites da evidência
Testes de componentes WPF instanciados em STA com `RaiseEvent`; não representam
automação física de mouse/teclado, renderização por screenshot, acessibilidade,
medição de vazamentos de memória ou execução prolongada em tempo real.

## Critérios de aceitação
- Mensagem `F30.1 WPF PASS` com 120 ciclos e contagem das asserções.
- Regressões F27/F28/F29 e compilação Windows aprovadas.
- Sem recursão na recuperação nem vazamento de dados após reset falho.
- `FM2008.mapping` permanece inalterado.
- `NoApprovedPlayerMaps`, `approved=false`, `operational=false`,
  `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`,
  `decision=NOT_MADE` e RA-01–RA-08 sem aceite permanecem intocados.
- PR em Draft até revisão independente do HEAD final e CI completo.
