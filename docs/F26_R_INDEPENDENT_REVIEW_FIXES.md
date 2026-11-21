# F26-R — Correções da revisão independente

## Base de revisão
- PR #28, iniciado sobre `main@6b0cf45f11f4ae9e271e41e9349618d6d1e7b254`.
- Achados R01–R04 da revisão #6006067755, no HEAD inicial `ecca58784356cce533bb0d39a3a6cfdd98bb3e6f`.
- Escopo estritamente offline, demonstrativo, sem fontes de memória do jogo, save ou binário histórico.

## Correções implementadas
- **R01 (alta)**: removida a interface injetável `IOfflinePlayerProfileSource` da API do serviço. `OfflineProfileQueryService` oferece somente construtor público sem argumentos; instancia internamente a fonte fixa `DemonstrationProfileSource`. Não existe mais parâmetro, delegate ou factory arbitrária `Snapshot()` no serviço. Testes por reflexão confirmam ausência de pontos de injeção; um provedor externo, mesmo declarando `IsLiveProcessSource=false`, não pode ser recebido pelo construtor.
- **R02 (média)**: testes exercitam mutação da lista de atributos original após a cópia, `IList<T>` com set/add/clear em listas expostas, preservação do snapshot e independência entre pesquisas.
- **R03 (média)**: `OfflineProfilesPresenter` controla a busca e autoriza seleção apenas entre os perfis da página atual; testes cobrem seleção válida, `null`, busca vazia, remoção de resultados antigos e seleção cruzada. Testes XML do XAML conferem duas grades readonly, aviso de ficção, ausência de ações de edição e botão operacional desabilitado. Isto é cobertura estrutural e de presenter, **não** teste visual automatizado de interação real da janela WPF.
- **R04 (baixa)**: identidades aceitam exclusivamente `DEMO-NNN` (três dígitos ASCII); testes negativos para prefixo real, sufixo vazio, espaços, letras, dígitos Unicode e largura excessiva.

## Limites de garantia
A fonte pública `DemonstrationProfileSource` é a implementação efetivamente fixa, composta apenas de três perfis inventados. A construção fechada do serviço F26 não aceita implementações externas. Isso não equivale a provar que código arbitrário malicioso em execução na mesma aplicação nunca acessará processos: o compromisso é restringir as **novas funcionalidades F26**.

## Validação e governança
O workflow `f26-offline-profiles.yml` testa `Domain/Application` no Linux, o presenter e a interface Windows com verificações estáticas do XAML e compilação WPF. Os workflows históricos permanecem exigidos. Revisar o HEAD e os checks após a última modificação; não converter sucesso de CI em aprovação humana.

`NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE` e oito riscos RA-01–RA-08 não aceitos. Nenhuma edição de `FM2008.mapping`, nenhum uso de `fm.exe`, memória ou save, e nenhuma autorização operacional. Manter PR Draft e sem merge.
