# F19 — Rastreabilidade A01–A12: evidências, limitações e ensaios negativos

**Referência de código:** base F18 pós-merge `9a0122f322f38bf82ef511bc33e28d61825e3589`; execução F19 no PR #20 (Draft).  
**Origem documental, sem modificar o repositório externo:** `marcosvilhena87/FM2008.mapping`, `research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md` e `fm2008-8.0.2v1-a01-a12-code-audit-2026-11-19.md`, consultados em 20/11/2026.  
**Natureza:** rastreabilidade do código e fontes existentes, **não** aprovação ou relato de novos ensaios no jogo. `NoApprovedPlayerMaps` ativo; `approved=false`; `operational=false`; A12 bloqueado.

## Critérios e proveniência

| Critério | Código/ensaio verificável no FMRTE.2008 | Evidência e origem | Limitação, pendência ou gate |
| --- | --- | --- | --- |
| A01 — Identidade | `SupportedBuildCatalog`, `BuildRecognitionTests`, `ProcessOpeningGateTests`, `F19ApprovalGateTests` | Fingerprint fixo 8.0.2v1; casos negativos de hash, arquitetura, versão e private part **simulados**; identificações de PIDs em relatos R01/R06 | Troca concorrente e identidade física real exigem revisão; não se infere autorização de leitura |
| A02 — Cabeçalho | `MinimalPlayerIdentityMap.ResolveHeaderAddress`, `AddressStabilityTests`, `F19ApprovalGateTests` | RVA `0x0171ED4C`; VA `0x01B1ED4C` nas amostras com base `0x00400000`; base `0x08000000` somente **simulação** | Não foi observada realocação verdadeira; rejeitar base 0/overflow |
| A03 — Tabela | `PlayerListReader`, `PointerTableReader`, `MinimalIdentityTests`, `AddressStabilityTests` | Contagens reais relatadas 5154, 6697, 6698; testes de header nulo/parcial, limites e overflow **simulados** | Sem verificação de todas as entradas e de todas as corridas reais |
| A04 — ID/nomes | `MinimalPlayerIdentityReader`, `MinimalIdentityTests`, `ApprovedIdentityPageTests` | Conferência **declarada pelo usuário** de 9/9 pares índice→ID/nome em duas partidas; testes UTF-16 e ausência de nome em fixture | Nome comum e componentes internos separados ainda requerem comprovação; nenhuma auditoria externa foi executada aqui |
| A05 — Ponteiros/strings | `PointerTableReader`, `ReadFailureTests`, `MinimalIdentityTests` | Null, falta de NUL, leitura parcial, índices inválidos e limites em **fixtures** | TOCTOU e páginas desaparecendo em processo real não foram demonstrados pelo F19 |
| A06 — Estabilidade | `StablePlayerIdentitySnapshotReader`, `RecordConsistencyTests`, `CrossPageSnapshotTests`, `PageConsistencyTests` | Corridas controladas e mudanças de header/slot/registro em **simulação** | Mesmo conteúdo rechecado não prova snapshot atômico nem elimina ABA entre observações |
| A07 — Ciclo de vida | `VerifiedPlayerReadSession`, `BudgetDisposeRaceTests`, `ResourceBudgetAuditTests`, `F17ResourceConcurrencyTests`, `F18TimeoutCleanupTests` | Testes de `Dispose`, timeout e falhas, inclusive Windows com eventos **locais** | Não equivalem ao encerramento real de `fm.exe` durante leitura |
| A08 — Consulta incremental | `IncrementalSearchController`, `IncrementalControllerTests`, `DistributedSamplingTests` | Limite de 100 registros/lote, cursor e falhas estruturais em **fontes simuladas** | UI somente demonstração; sem consulta live aprovada |
| A09 — Exclusividade de leitura | `NoApprovedPlayerMaps`, `VerifiedPlayerReadSession.OpenAsync`, `ApprovedIdentityPageTests`, `F19ApprovalGateTests` | **Teste negativo novo:** mapa não aprovado sempre retorna null e opener não é chamado, mesmo para build reconhecida | Auditar permissões/código futuro antes de aprovação; nenhuma escrita ou conexão criada neste PR |
| A10 — Cobertura | `DistributedPlayerSampling`, `DistributedSamplingTests`, `F19ApprovalGateTests` | Amostras distribuídas de nove índices em saves com 6697 e 5154 registros, com IDs e nomes **declarados conferidos** | 9/9 é amostra, não cobertura de 100%; índice não é identidade persistente entre sessões |
| A11 — Compatibilidade | `SupportedBuildCatalog`, `VerifiedProcessOpeningGate`, `BuildRecognitionTests`, `F19ApprovalGateTests` | Negativos de build divergente/arquivo/PID em **simulação**, variante fixa | Sem herdar validação para outras versões, arquitetura ou binário modificado |
| A12 — Decisão formal | `NoApprovedPlayerMaps`, `SupportedBuildCatalog`, `MinimalPlayerIdentityMap.IsApprovedForOperationalUse`, `F19ApprovalGateTests` | **Bloqueio comprovado no código**, inclusive quando fixtures passam | **GATE-BLOQUEADO**: falta parecer humano, aprovação explícita separada, escopo e riscos formalmente aceitos |

## Matriz dos riscos residuais — RA-01 a RA-08

| Risco | Critérios correlatos | Situação prudente | Próximo ensaio seguro |
| --- | --- | --- | --- |
| RA-01 — base/módulo incorretos | A01/A02/A11 | Parcial, realocação real não observada | Negativos de base e fingerprints offline |
| RA-02 — ponteiros/índices obsoletos | A06/A07/A08 | Risco material nas amostras entre sessões; mitigação simulada | Verificar invalidação de cursor em mudança de origem e sessão |
| RA-03 — inconsistência/ABA | A05/A06 | Parcial; impossibilidade de provar atomicidade com rechecagem apenas | Testes determinísticos de ABA e alteração in-place sem prometer prova absoluta |
| RA-04 — ponteiro/UTF-16 inválido | A03/A04/A05 | Fixtures aprovadas, processo real desconhecido | Negativos de limites/strings e falha parcial |
| RA-05 — ID incorreto fora da amostra | A04/A10 | Cobertura amostral ampliada, não estatística populacional | Revisar variedade, nome comum e proveniência das 9/9 conferências |
| RA-06 — troca de build/PID | A01/A07/A11 | Parcial, com testes offline de rejeição | Identidade pré/pós-aquisição e troca simulada de sessão |
| RA-07 — extrapolação de offsets | A04/A09/A12 | **Bloqueio crítico mantido** | Proibir promoção dos 229 offsets legados e comparar diffs |
| RA-08 — escrita/permissões | A09/A12 | **Bloqueio crítico mantido** | Assegurar `NoApprovedPlayerMaps` e UI demonstrativa sem abrir processos |

## Checklist de governança (modelo, não decisão)

- [ ] Identificar revisor humano, aprovador, data e ambiente.
- [ ] Registrar SHA imutável e variante única do manifesto candidato.
- [ ] Anexar evidências por A01–A11 com origem, executor, data e reprodução.
- [ ] Documentar resultados de R01–R07 e exceções justificadas.
- [ ] Registrar riscos RA-01–RA-08, aceitação explícita e limites do escopo.
- [ ] Auditar ausência de escrita, permissões somente leitura, catálogo de produção, UI e filtros.
- [ ] Formalizar decisão A12 separada do PR e dos resultados de CI.

**Estado de todos os itens de autorização: não assinalados.** Nenhum aceite é presumido por completar testes, registrar checklist ou elaborar este documento.

## Evidência automatizada F19

`tests/FMRTE2008.MemorySimulation/F19ApprovalGateTests.cs` introduz negativos controlados de A01/A02/A09/A10/A11/A12 e os integra no runner `Program.cs`. A03–A08 já possuem as suítes mapeadas na tabela; a referência a essas suites não significa execução real do jogo nem aprovação automática. O relatório de CI do HEAD correspondente deve ser anexado na revisão técnica, sem extrapolar o resultado.

## Decisão vigente

**NÃO APROVADO; A12 GATE-BLOQUEADO; `approved=false`; `operational=false`; `NoApprovedPlayerMaps` obrigatório.** Nenhum mapa real foi alterado, nenhuma leitura ou escrita de `fm.exe` foi introduzida e nenhuma conexão WPF live foi habilitada.


## Revisão independente C01–C04 — inventário e proveniência (20/11/2026)

A auditoria de revisão `5999528725` motivou controles novos, **sem aprovação operacional**. Os casos abaixo são referências a **testes negativos nomeados** registrados no runner; os testes efetivamente exercem os cenários enquanto o `F19CoverageInventoryTests` fiscaliza nomes, arquivos e inscrição de cada suíte. A verificação documental de presença **não substitui** os resultados executados. Todos os cenários citados são de *fixtures* ou análise de código, não de jogo real.

### C01 — Um caso negativo reproduzível por critério

| Critério | Caso negativo e comportamento esperado | Suíte efetivamente chamada pelo runner | Natureza / limite |
| --- | --- | --- | --- |
| A01 | `Sha256` divergente é rejeitado | `BuildRecognitionTests.Run()` | Reconhecimento sintético; não testa processo externo |
| A02 | Base + RVA com overflow produz `OverflowException` | `AddressStabilityTests.RunAsync()` | Base deslocada sintética; não comprova relocação real |
| A03 | Header com tabela nula produz `InvalidDataException` | `Program.cs`: caso `tabela nula` | Memória simulada; não cobre tabela real integral |
| A04 | Nome obrigatório ausente ou inválido rejeitado | `MinimalIdentityTests.RunAsync()` | Strings sintéticas; comparação 9/9 real é declaração independente |
| A05 | Falhas de leitura/pointer rejeitadas e sessão invalidada | `ReadFailureTests.RunAsync()` | Provider falso, não acesso físico ao jogo |
| A06 | Mudança de tabela durante leitura rejeitada | `RecordConsistencyTests.Run()` | Mutação coordenada, não prova snapshot atômico/ABA impossível |
| A07 | Falha `IOException` invalida sessão/cursor | `SessionRaceTests.RunAsync()` | Reinício fictício; F16–F18 complementam recursos locais Windows |
| A08 | Fonte live proibida produz `UnauthorizedAccessException` | `IncrementalSearchTests.RunAsync()` | Controller inerte, não UI conectada a processo |
| A09 | Catálogo deny-all impede chamada do opener (`acquisitions=0`) | `F19ApprovalGateTests.RunAsync()` | Bloqueio runtime da fixture; composição auditada separadamente |
| A10 | Amostragem `BuildIndices(3,4)` fora dos limites é rejeitada | `DistributedSamplingTests.Run()` | Planejamento de índices; 9/9 declarados não provam a população |
| A11 | Hash alterado é negado antes da aquisição | `ProcessOpeningGateTests.RunAsync()` | Identidade simulada; não revalida processo real em execução |
| A12 | Alteração simulada do retorno do catálogo é detectada | `F19CompositionRegressionTests.Run()` | Vigia textual de regressão; não efetua decisão humana de aprovação |

`F19CoverageInventoryTests.Run()` verifica a existência dos 12 casos acima e a inscrição das respectivas suítes em `Program.cs`, falhando se algum estiver ausente; a própria suíte contém um controle negativo de remoção de registro. A matriz não afirma *cobertura negativa exaustiva*: há outras falhas e concorrências possíveis.

### C02 — Fontes versionadas, executor e resultado

**Fonte de código CI:** `marcosvilhena87/FMRTE.2008`, base `9a0122f322f38bf82ef511bc33e28d61825e3589` (pós-merge F18), branch `plan/f19-a01-a12-approval-gate`; o HEAD de implementação e os IDs dos workflows são registrados no comentário de validação do PR #20 depois de cada atualização final. **Executor dos testes:** GitHub Actions, ambiente `windows-latest`, .NET 10, eventos `push` e `pull_request`; CI comprova somente resultados de fixtures e leitura estática do código-fonte em checkout.

**Documentos de pesquisa utilizados como referências imutáveis**, apenas leitura, repositório `marcosvilhena87/FM2008.mapping` em `df5be0493a1ac7be57208ff0e91c2d7193dfec73`:

| Documento e escopo | Blob SHA Git | Commit de última edição | Data Git do commit (UTC) | Autor registrado |
| --- | --- | --- | --- | --- |
| `research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md`: A01–A12, adendos R07, 9/9 declarados, gate A12 | `b29d4e21d97209a52fc009ab3d4eac03b4956f49` | `95173ff65ed2c4145b9c84b784bcf34142447ac8` | 2026-11-19 03:15:14 | marcosvilhena87 |
| `research/builds/fm2008-8.0.2v1-a01-a12-code-audit-2026-11-19.md`: auditoria estática histórica | `59e0513a23272ed8e569043edcee762b1b4ad06f` | `b1f19d94e0509f0bd94a12568b8439cc06bb676a` | 2026-11-19 03:14:45 | marcosvilhena87 |
| `research/builds/fm2008-8.0.2v1-second-save-r07-profile-check-template.csv`: conferência relatada, não auditada | `4b3d275e43daa9c3a8b5c98237412d899f192374` | `1d4eb14fcea0f5dab23b7f79b948479701a68d72` | 2026-11-19 03:09:02 | marcosvilhena87 |

**Distinção crucial de executores:** a autoria Git dos documentos é atribuída à conta `marcosvilhena87`; **isso não prova auditoria independente** nem data da observação original. A conferência de perfis 9/9 nos dois saves foi *declarada pelo usuário*, sem auditoria externa nesta implementação. Os resultados de casos negativos são de GitHub Actions, nunca atribuídos a execução do FM2008 real. Para evidências pessoais históricas R01–R07, ambiente e executor individuais ainda requerem confirmação documental; manter status **PARCIAL** ou **PENDENTE**, não inventá-los.

### C03 — Composição efetiva: limite do controle novo

`F19CompositionRegressionTests` confere, no **código-fonte real da aplicação incluído no checkout do CI**:

- WPF constrói `IncrementalSearchController` somente com `DemonstrationIndexedPlayerSource`;
- catálogo `NoApprovedPlayerMaps` retorna `null` de `FindApprovedAsync`;
- reconhecimento `8.0.2v1` mantém `MemoryMapApproved=false`;
- mapa experimental mantém `IsApprovedForOperationalUse=false`;
- alterações sintéticas da expressão de WPF, do retorno do catálogo e da flag são rejeitadas pelo vigia.

O `F19ApprovalGateTests` verifica separadamente o bloqueio **em runtime** antes de atingir um `IVerifiedProcessOpener` falso. A conferência das declarações é um alarme textual que falha fechado quando fonte não está disponível, **não** uma prova de completude de todas as configurações possíveis, DI, bins ou futuras alterações; revisão de diff continua mandatória. Nenhum binário de `fm.exe` é lido, escrito ou executado.

### C04 — Status factual vs. critério de aprovação

| Dimensão | Comprovado nesta etapa | Expressamente NÃO comprovado |
| --- | --- | --- |
| A02 | Cálculo x86 em bases conhecidas e simuladas | Relocação da base do jogo real |
| A04/A10 | Amostra distribuída conforme relatos do usuário e geração de índices offline | Identidade correta de todos os 5154/6697/6698 jogadores ou auditoria externa de cada campo |
| A06/A07 | Consistência e encerramento em fixtures, F16–F18 em eventos kernel locais | Snapshot atômico, ausência universal de ABA, teardown físico do processo real |
| A09/A12 | Gate atual nega mapas e WPF segue demonstrativa | Aprovação humana, abertura operacional autorizada, aceitação dos riscos |

**Decisão final deste documento:** `NoApprovedPlayerMaps` continua deny-all, `approved=false`, `operational=false`; A12 = **GATE-BLOQUEADO**. O PR #20 permanece Draft; nenhuma aprovação de mapa, integração live ou edição de `FM2008.mapping` foi solicitada.
