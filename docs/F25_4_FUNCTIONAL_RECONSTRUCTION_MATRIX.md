# F25.4 — Matriz de reconstrução funcional (documental e offline)

> **PROPOSTA DE ARQUITETURA, NÃO EQUIVALÊNCIA IMPLEMENTADA.** A presença de símbolos CLR no FMRTE original comprova apenas nomes/tokens declarados; a relação com módulos do editor moderno é uma proposta técnica. Nenhuma classe antiga foi executada ou copiada. Sem alteração do `FM2008.mapping`.

## Evidências congeladas
- Referência histórica: `FMRTE.exe` SHA-256 `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07`.
- Catálogo F25.3: `F25SymbolCatalog.json`, SHA-256 `ced806e946e3d08fefd2a828ad8281769f425906393bc78dfd8c9cf70d4348d9`, 100 TypeDef / 1421 MethodDef.
- Arquitetura moderna inspecionada no repositório: `docs/ARCHITECTURE.md`, `src/FMRTE2008.Domain/Contracts.cs`, `src/FMRTE2008.Application/PlayerQuery.cs`, `src/FMRTE2008.Desktop/MainWindow.xaml`, `src/FMRTE2008.Infrastructure.Windows/VerifiedWindowsProcessOpener.cs`.
- Matriz estruturada: `tests/FMRTE2008.MemorySimulation/F25FunctionalReconstructionMatrix.json`. **16 correspondências conceituais**, todas sem autorização operacional.

## Proposta por módulo

| Módulo | Símbolos históricos (amostra comprovada por TypeDef) | Reconstrução proposta | Limite |
| --- | --- | --- | --- |
| **Domain** | `FMRTE.GameClasses.Player` (0x0200002f), `PlayerAttributes` (0x02000032), `Contract` (0x02000014), `League` (0x02000012), `Club` (0x02000038), `Nation` (0x0200001f) | Modelos próprios e objetos fictícios, separados das estruturas históricas | Não transportar offsets, representação binária ou lógica de gravação |
| **Application** | `FMRTE.GameClasses.Players` (0x02000030), `PlayersList` (0x0200003c), `PlayerInfo` (0x0200002e) | Consultas offline e paginação de snapshots em `PlayerQuery.cs` | Serviço existente utiliza `DemonstrationPlayerSource`, sem processo real |
| **Desktop** | `FMRTE.frmMain` (0x02000008), `FM_Editor.frmOptions` (0x02000005), `playersPositions` (0x02000017), `playersAttributes` (0x02000042), `leagueStandings` (0x0200000d), `contractsInformation` (0x0200000f) | Planejamento de telas WPF com dados sintéticos e navegação | Interface atual declara explicitamente acesso ao jogo bloqueado |
| **Infrastructure.Windows** | **Nenhum símbolo do legado associado a operação nativa** | Manter apenas abstrações Windows já existentes, auditadas e protegidas por gates | `VerifiedWindowsProcessOpener` não autoriza sessões de produção nem mapas |

As palavras `OBSERVED` e `INFERRED` são distintas: nomes de classes e tokens são observados no PE/CLR; a pertinência ao módulo novo é **inferência**, não compatibilidade ou reprodução comportamental. Os estados `OFFLINE_ONLY` e `PROPOSED` também não significam funcionalidade live.

## Encadeamento seguro de construção
1. **Domain:** definir DTOs e regras independentes de plataforma, começando por atributos de jogadores e contratos exclusivamente fictícios.
2. **Application:** ampliar `PlayerQueryService` de maneira determinística, com paginação/snapshots de fixtures e critérios de falha.
3. **Desktop:** desenhar telas WPF somente demonstrativas; manter conexão operacional desabilitada e avisos de dados fictícios.
4. **Infrastructure.Windows:** **não** trazer código de memória do programa histórico; restringir alterações futuras às guardas de segurança existentes, com autorização específica distinta.
5. **Testes:** preservar testes de regressão F01–F25.3, incluir contratos e mutações negativas no JSON F25.4. Reavaliar equivalência real somente com evidência apropriada e etapa formal posterior.

## Validação F25.4
`test_f25_functional_matrix.py` verifica caminhos reais do projeto, vínculo módulo/caminho, identidade da fonte, unicidade de linhas e tokens, enumerações de estado e invariantes. **21 testes** (1 válido e 20 mutações negativas) rodam no Windows e Linux com GitHub Actions, sem ZIP histórico no CI. Não há teste de escrita, interação com `fm.exe`, alteração de mapas ou execução do FMRTE original.

### Lacunas reconhecidas
- A matriz **não** demonstra equivalência semântica entre as classes antigas e o design atual.
- O manifesto registra apenas um subconjunto de 16 tipos escolhidos entre 100; métodos e recursos adicionais podem alimentar futuras hipóteses, mas não autorizam vinculação operacional.
- O inventário de nomes, embora vinculado a um hash de catálogo produzido fora do CI, não é revalidado byte a byte pelo CI sem o ZIP original; verificar o catálogo local com o script F25.3 quando necessário.
- `FieldDefinition` existe no Domain, mas nenhum offset histórico tem status aprovado; não converter proposta em mapa real.

## Invariantes
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, RA-01–RA-08 sem aceite. Nenhum merge do PR #27 nem alteração no `FM2008.mapping` nesta fase.
