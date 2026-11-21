# F26 — Primeira reconstrução funcional, exclusivamente offline

> **Escopo funcional demonstrativo**: os três jogadores, atributos e contratos aqui usados são fictícios. Nenhum registro é extraído do Football Manager 2008 nem deriva de offsets históricos.

## Linha de base e limites
- F25 integrado na `main@6b0cf45f11f4ae9e271e41e9349618d6d1e7b254`.
- F25 identificou apenas símbolos/metadados estáticos do FMRTE v1.0.0.5; a reconstrução F26 não atribui equivalência funcional a eles.
- `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE` e oito riscos residuais sem aceite. Não alterar `FM2008.mapping`.

## O que foi construído
1. **Domain**: `OfflinePlayerProfile`, `OfflineAttribute`, `OfflineContract`, validações de campos, período e faixa 1–20. Identidades reservadas a prefixo `DEMO-` e atributos copiados em coleção somente leitura.
2. **Application**: `DemonstrationProfileSource` produz três personagens inventados; `OfflineProfileQueryService` recusa `IsLiveProcessSource=true` antes de ler qualquer snapshot, congela a lista, impede IDs duplicados, limita tamanho de consulta e paginação.
3. **Desktop**: nova `OfflineProfilesWindow` WPF com listagem, pesquisa e detalhes de atributos/contrato, explicitamente rotulada como fictícia. A janela é aberta por novo botão em `MainWindow`. Nenhum campo de edição ou operação de processo foi acrescentado.
4. **Testes**: projeto de console determinístico `tests/FMRTE2008.OfflineProfiles` e workflow F26 para validação no Linux e compilação Windows da interface WPF, sem iniciar o aplicativo legado ou `fm.exe`.

## Segurança e integridade
- Fonte de perfis do F26 não importa `Infrastructure.Windows`, não expõe PID, endereço, ponteiro, offset, handle ou escrita.
- A infraestrutura Windows existente não é modificada.
- A nova janela é somente leitura; edição e conexão ao jogo continuam indisponíveis.
- Nenhuma tentativa de validar bytes históricos como funcionalidade real.
- Revisão de código e CI não constituem aprovação humana das observações B03.

## Critérios para conclusão do F26 inicial
- Verificar resultados de CI e eventuais avisos/erros de compilação de C# e XAML.
- Revisar código das classes, a identidade fictícia dos três perfis e a rejeição de fontes live.
- Executar checks de regressão anteriores e conferir o HEAD do PR, sem merge nesta fase.
- Corrigir quaisquer regressões no próprio PR Draft antes de sugerir Ready for review.
