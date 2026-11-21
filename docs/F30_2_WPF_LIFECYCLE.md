# F30.2 — Ciclo de vida e integridade de recursos WPF (somente offline)

## Objetivo e base
Base `main@288aefb66aadd23842355f1ea8601c96eba48201`, com F30.1-R integrado e aprovado. Exercitar repetidas criações e fechamentos da janela WPF com **somente os perfis fictícios fechados** (`Baseline` e `CollisionStress`). Não abrir FM2008, consultar PID, memória de processo, saves, rede ou arquivos de usuário.

## Protocolo determinístico
- Projeto Windows STA separado em `tests/FMRTE2008.OfflineLifecycle.Windows`.
- 48 janelas criadas/fechadas sequencialmente em 48 threads STA isoladas, cada uma com desligamento explícito do Dispatcher, alternando cenário básico de três perfis e adversarial de seis perfis, em três fluxos de operação: busca vazia/reset, navegação/recuperação, falha persistente/fechamento no estado indisponível.
- Verificação de identidade `DEMO-001`/`CLUB-001` em cada janela recém-criada, garantindo que um estado de falha da janela anterior não persiste em nova janela.
- Observação explícita de uma ocorrência dos eventos `Closing` e `Closed` por ciclo e ausência de duplicação no evento do botão reset durante o ensaio.
- Após encerrar e fazer `Join` de cada thread STA, e sair dos métodos `NoInlining` que criam/fecham a janela, guardar somente `WeakReference`; executar três rodadas de GC forçado e exigir retenção de no máximo quatro entre 48 janelas fechadas. A primeira execução no mesmo Dispatcher manteve 48/48 referências, motivando o isolamento de Dispatcher para distinguir caches por thread de retenção de janelas.
- A falha persistente é injetada somente pelo gancho interno de testes. Uma janela deve fechar mesmo em estado indisponível.

## Limitações interpretativas
A verificação por `WeakReference` sinaliza retenção forte gerenciada *sob as condições de GC deste runner*, mas não substitui instrumentos de detecção de vazamento nem garante liberação de handles nativos. Os eventos são disparados com `RaiseEvent` em STA e a janela não é exibida visualmente: não comprova interação física de teclado/mouse, automação visual, foco, layout real, alternância de threads ou acessibilidade. Eventos `Closing`/`Closed` provam ciclo de vida lógico, não a ausência de vazamentos de recursos de plataforma.

## Critérios de aceitação
1. Check `f30-2-wpf-lifecycle` `completed/success`, com relatório determinístico de 48 janelas, contagem de asserções e retenção gerenciada.
2. Testes F27/F28/F29/F30.1 e workflows de segurança pré-existentes sem regressões.
3. Varredura negativa de operações proibidas abrangendo código novo, incluindo mutação em memória que confirme a ação do padrão.
4. `FM2008.mapping` inalterado, e nenhuma habilitação operacional, leitura/escrita de memória ou save.
5. Manter `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE` e RA-01–RA-08 sem aceite.
6. PR em **Draft**, sem merge, até CI completo e revisão técnica independente do HEAD final.

## F30.2-R — Revisão R01–R03
- **R01:** modo controle com 48 STAs/Dispatchers encerrados e modo adicional com **48 janelas no mesmo Dispatcher STA ainda ativo**, processando a fila WPF por `DispatcherFrame` e `ApplicationIdle` entre lotes de oito janelas. Coleta de referências fracas após drenagem/GC em seis checkpoints e ao final; retenção permitida de no máximo quatro janelas, a ser verificada nos logs CI. A retenção antiga de 48/48 sem bombeamento era um achado específico daquele protocolo, não prova de vazamento.
- **R02:** o reset real `Reset_Click` incrementa contador `internal` exclusivamente diagnóstico, de modo que o teste confira execução exata do handler por clique, não apenas um observador registrado pelo teste. Os eventos `Closing` e `Closed` continuam contados. O heap gerenciado é medido em cada lote no Dispatcher compartilhado com limite superior de crescimento de 64 MiB desde a linha de base, sem inferir monotonicidade da memória alocada. O processamento dos callbacks do Dispatcher ocorre em checkpoints antes da inspeção da coleta; mede-se também a permanência ativa do Dispatcher. Não se simulam eventos externos de mouse/teclado.
- **R03:** a verificação regex é apenas um detector limitado de assinaturas nativas/processo/escrita previamente reconhecidas. A mutação negativa é **em memória** e testa somente esse padrão, não autorização por semântica, assemblies externos, reflexão ou chamadas indiretas. O novo teste usa `GC.GetTotalMemory` (processo de teste gerenciado) e não acessa memória de outro processo, arquivos/saves, endereços, PIDs ou APIs nativas.
- **Limites ainda existentes:** 48 janelas nunca exibidas em tela, sem comprovação visual/UI automation, medição de GDI/USER handles, `HwndSource` real exibido, WPF `Application.Run` ou fuga de referências em plugins externos. `WeakReference` e limite de heap não substituem profiling de memória/handles. Os fluxos são sintéticos e sequenciais, não concorrentes.
