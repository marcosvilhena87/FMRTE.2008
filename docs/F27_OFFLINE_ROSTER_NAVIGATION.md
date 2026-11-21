# F27 — Navegação offline por jogadores, categorias e clubes fictícios

## Referência
- Base: `main@5dc32ad1d7a3e2bfeaddaf56b60d71c0508fb2f8` (F26 integrado).
- **Todo o conteúdo é inventado** e não representa atributos, clubes, contratos ou estruturas de memória observados no Football Manager 2008.
- O PR é independente e deverá permanecer Draft até auditoria técnica; nenhuma modificação de `FM2008.mapping`.

## Componentes
1. **Domain:** `OfflineAttributeCategory` (Technical/Mental/Physical), `OfflineCategorizedAttribute`, `OfflineClub` (IDs `CLUB-NNN`), `OfflineExpandedPlayer` e suas listas protegidas por `Array.AsReadOnly`. A consistência entre clube e contrato é conferida.
2. **Application:** `OfflineExpandedRoster` compõe os três perfis inventados do F26 com três clubes e a classificação dos respectivos atributos. A construção é fechada: não há injeção de provedores, leitura de processos, endpoints ou dados externos.
3. **Application / navegação:** `OfflineRosterPresenter` pesquisa nome, identificador, posição e clube; mantém exclusivamente o filtro atual e navega por `Next`, `Previous`, `Select`, sem permitir seleção de itens fora dos resultados.
4. **Desktop:** `OfflineRosterWindow`, WPF somente leitura, mostra elenco, listas separadas de atributos técnicos, mentais e físicos, além de informações do clube e contrato fictícios. A navegação tem estado de primeiro/último jogador e limpa os detalhes em buscas vazias. A janela principal abre o painel por botão independente.
5. **Testes:** `tests/FMRTE2008.OfflineRoster/Program.cs` verifica contratos e transições com dados fixos e cenários adversariais. `test_f27_xaml_safety.py` confere estrutura XAML, botão operacional desabilitado, grupos e ausência de chamadas nativas no code-behind.

## Escopo da validação
- Linux: executa testes de Domain/Application em .NET 10.
- Windows: valida estaticamente o XAML, compila WPF e inspeciona fonte contra APIs nativas.
- Os testes não abrem a janela graficamente nem executam `fm.exe`, `FMRTE.exe` ou conectam ao jogo. Compilação de WPF não comprova interação visual real.
- Classificações são escolhas fictícias de demonstração; não declarar equivalência funcional com o FMRTE original ou legitimidade de offsets.
- A seleção fica dentro do conjunto filtrado; as listas expostas são imutáveis para consumidores normais da API.

## Bloqueios permanentes
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, oito riscos RA-01–RA-08 sem aceite. Nenhum mapa operacional, processo real, memória ou escrita.
