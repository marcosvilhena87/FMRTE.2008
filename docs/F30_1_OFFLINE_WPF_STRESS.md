# F30.1-R — Resistência WPF determinística (somente offline)

## Escopo
Testes de componentes WPF em STA com o cenário fechado `OfflineRosterScenario.CollisionStress` de seis perfis inteiramente inventados. Não há FM2008 executado, fonte externa, acesso a processo, offsets, saves, leitura ou escrita de memória.

## Sequências e invariantes
- 120 ciclos determinísticos, compostos por três sequências distintas, alternadas por `iteration % 3` (40 repetições de cada).
- Colisões de nomes, IDs embutidos em texto de outro jogador, clubes com nomes próximos, navegação nos limites e seleção por ID único.
- Em cada estado selecionado, igualdade **exata** dos IDs `DEMO-NNN`/`CLUB-NNN` na linha e nos detalhes, com presença de contrato e atributos fictícios.
- Em lista vazia ou estado indisponível, ausência de seleção e limpeza de identificação, cidade, contrato, salário, membros e grades de atributos, além da verificação dos botões.
- A cada dez ciclos, erro sintético recuperável seguido de falha **sustentada** durante cinco resets explícitos. Eventos sintéticos de navegação, pesquisa e clube nesse estado devem permanecer inertes. O teste remove explicitamente a falha e exige novo clique em reset para recuperar a tela.
- Todas as asserções usam ciclo e descrição na mensagem de erro. A contagem de asserções é informada no log da execução.

## CI e proteção
O workflow F29 existente compila e executa o teste F30 no Windows, preserva o conjunto Linux e verifica operações proibidas nas fontes da UI/aplicação **e no novo F30Stress.cs**. Uma mutação em memória com assinatura proibida deve ser detectada pela mesma expressão regular, sem alterar o workspace.

## Limitações
`RaiseEvent` em STA não constitui teste de mouse/teclado físico, renderização visual, acessibilidade, concorrência real entre threads, cronômetro de longa duração ou medições de alocação e vazamentos. O ensaio reutiliza uma janela; ciclos de abertura/fechamento e orçamento de recursos ficam para fase posterior.

## Segurança e aceitação
- PR em Draft até CI integral e revisão técnica independente do HEAD final.
- `FM2008.mapping` intacto.
- `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE` e RA-01–RA-08 sem aceite.
- Nenhum `fm.exe`, conexão live, acesso a memória, modificação de save ou autorização de mapas.
