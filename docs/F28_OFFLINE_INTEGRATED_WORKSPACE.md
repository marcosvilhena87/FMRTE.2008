# F28 — Consolidação da experiência offline

**Base:** `FMRTE.2008/main@fb328f83e5a02715c6a158cacb7499ff4a910270` (F27 pós-merge). Esta etapa acrescenta somente funcionalidades sintéticas e de apresentação; não depende de processos, de leitura/escrita de memória ou de saves.

## Experiência integrada
- `OfflineWorkspacePresenter` agrupa, por seleção, elenco filtrado, jogador, clube, contrato e os três grupos de atributos técnicos, mentais e físicos.
- A composição reutiliza o `OfflineRosterPresenter` e o catálogo fictício fechado do F27; não recebe injeção de fonte, arquivo, PID, map, handle ou endereço.
- `OfflineWorkspaceWindow` mostra pesquisa, navegação anterior/próximo, seleção no DataGrid, detalhes de clube, localização totalmente fictícia, contrato e as três categorias de atributos.
- O botão **Ver jogadores deste clube fictício** restringe a lista ao clube atualmente visível; IDs externos não são aceitos pelo presenter. Pesquisa vazia ou seleção inválida limpa os detalhes e desabilita a navegação.
- A tela principal passa a oferecer acesso independente à nova janela F28, sem modificar a infraestrutura Windows e sem habilitar o botão de conexão ao FM2008.

## Testes de regressão
- Linux: projeto `tests/FMRTE2008.OfflineWorkspace` comprova invariantes determinísticos para busca, navegação, clube, contrato, atributo, imutabilidade, vazamento de seleção e fonte fechada.
- Windows: projeto `tests/FMRTE2008.OfflineWorkspace.Windows`, com `[STAThread]`, instancia a janela WPF em processo próprio de testes e aciona controles reais com eventos WPF, **sem abrir `fm.exe` ou qualquer executável histórico**. Confere inicialização, seleção, filtros, navegação, detalhes, limpeza e controles desabilitados.
- Esses testes são **smoke de componente WPF em processo**, não prova de renderização visual/screenshot, acessibilidade completa, instalação, entrada do usuário por mouse/teclado ou comportamento com o jogo real.
- Workflow F28 usa GitHub Actions Ubuntu (presenter) e Windows (compilação + smoke STA), com filtros idênticos para push/PR.

## Restrições de segurança
- Os dados são três jogadores e três clubes totalmente inventados.
- Não alterar `FM2008.mapping`, nem `Infrastructure.Windows`, nem reconhecer offsets operacionais.
- Permanecem `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE` e os oito riscos RA-01–RA-08 sem aceite.
- Sem acesso à memória, escrita, ligação live com o Football Manager, alteração de save ou execução de binário legado.
- O PR deverá permanecer **Draft** até revisão técnica independente e reconfirmação do HEAD/CI.
