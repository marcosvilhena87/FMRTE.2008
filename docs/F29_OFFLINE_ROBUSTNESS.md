# F29 — Robustez da navegação e recuperação WPF exclusivamente offline

Base: `main@75a3ffe8be51f372541f266fd9574d2ef31882a0` (F28 pós-merge).

## Objetivo e fronteira
O modo normal continua com os três perfis fictícios F26–F28. Um **cenário adicional fechado `OfflineRosterScenario.CollisionStress`** contém seis registros igualmente fictícios para testes: jogadores de IDs diferentes com nomes iguais, múltiplos jogadores no mesmo clube, nomes de clubes similares e nome de jogador contendo o ID de outro clube. Não existe injeção de `IOfflineSource`, processo, caminho, memória, save ou rede. O construtor com cenário é **internal** e a abertura é restrita a assemblies de testes nomeados e ao Desktop.

## Comportamento testado
- `FilterByClubId` usa somente comparação `StringComparison.Ordinal` pelo ID `CLUB-NNN`, sem admitir prefixos, espaços ou campos textuais conflitantes.
- `ShowClub` exige que o clube esteja visível e retorna todos os jogadores fictícios daquele clube, inclusive quando o filtro anterior mostrava um subconjunto de nomes.
- Jogadores com mesmo nome são diferenciados por `DEMO-NNN`; filtros não permitem selecionar outro clube; navegação anterior/próximo respeita os limites.
- `OfflineWorkspaceWindow` encapsula ações de pesquisa, navegação, seleção e clube em `RunSafely`. Uma falha esperada `ArgumentException`/`InvalidOperationException` reinicia exclusivamente filtro e seleção fictícios e exibe texto de recuperação sem vazar detalhes da exceção. Exceções inesperadas **não são ocultadas**. A rotina interna de teste provoca falha sintética controlada, sem I/O.
- O painel WPF continua somente leitura; não executa `fm.exe`, `FMRTE.exe`, ou qualquer jogo.

## CI determinístico
- Linux: `tests/FMRTE2008.OfflineRobustness` exercita seis perfis, identidade estável, colisões de ID, seleção inválida, membros de clube, imutabilidade e recuperação após busca inválida.
- Windows: `tests/FMRTE2008.OfflineRobustness.Windows` instancia o WPF em STA, aciona eventos `Click` e seleção de `DataGrid`, confirma sincronização de clube e pesquisa, limpeza de detalhes e recuperação após erro intencional.
- Workflows em `push`/`pull_request` usam caminhos simétricos e inspeção de APIs de processo/escrita.
- O smoke é **in-process**, não substitui teste visual com a janela exibida, mouse/teclado real ou acessibilidade.

## Governança
Sem alterações em `FM2008.mapping`, sem mapas de jogadores aprovados, acesso a memória, alteração de saves ou escrita operacional. Permanecem `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE` e RA-01–RA-08 sem aceite. Este PR deverá permanecer Draft até revisão técnica independente.

## F29-R — Remediação da revisão independente #6006393788

- **R01 — recuperação sem recursão:** `RunSafely` não chama a si mesmo. Ao falhar uma ação, executa no máximo uma restauração. Se esta falhar, `ShowUnavailable` limpa os detalhes do jogador, clube, contrato, grades e filtro, desativa pesquisa/seleção/navegação e deixa somente **Mostrar todos** ativo para tentativa explícita posterior. Falhas inesperadas fora das exceções permitidas não são escondidas.
- **R02 — reset protegido:** `Reset_Click` usa a mesma entrada `RunSafely` com `isReset: true`. Erro no próprio reset vai diretamente ao estado seguro sem segunda tentativa automática.
- **Testes Windows adversariais:** falha sintética única com recuperação; falha do reset automático; falha persistente que atinge também uma tentativa manual; UI segura sem detalhes antigos; clique de navegação ignorado no estado indisponível; sucesso após tentativa manual subsequente; falha diretamente no handler `Reset_Click`. Os gatilhos sintéticos são `internal`, acessíveis apenas ao assembly de teste Windows explicitamente nomeado.
- **Limite da prova:** os testes STA disparam eventos WPF em processo e não substituem automação visual da janela mostrada, teclado/mouse físicos ou testes de acessibilidade. O escopo permanece fechado e exclusivamente fictício.
- **Governança:** preservar `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, RA-01–RA-08 não aceitos; nenhum `fm.exe`, memória, save, fonte live ou escrita.
