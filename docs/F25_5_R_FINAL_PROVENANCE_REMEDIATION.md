# F25.5-R — Correções de rastreabilidade, proveniência e relatório consolidado

> **Offline/documental.** Esta etapa não autentica observações históricas nem substitui deliberação humana. O ZIP original e o executável não são enviados ao GitHub Actions. O código não executa nenhum binário histórico ou `fm.exe`.

## Linha de base
- PR #27, base `main@ccd621fd620ffee456bfe4eccfa9fc512e7b7c54`; HEAD e IDs de execução devem ser obtidos do próprio GitHub no momento de cada auditoria. Não preencher um HEAD futuro artificialmente neste documento.
- Artefato ZIP de referência SHA-256 `491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd`.
- `FMRTE.exe` SHA-256 `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07`.
- Catálogo F25.3 completo (local, 100 TypeDef / 1421 MethodDef) SHA-256 `ced806e946e3d08fefd2a828ad8281769f425906393bc78dfd8c9cf70d4348d9`.
- Manifestos históricos e operacionalidade anteriores continuam **não aprovados**.

## Resolução dos achados
| Achado | Correção verificável |
| --- | --- |
| F25.5-R01 | `F25CatalogTypeProvenance.json` contém os 16 pares TypeDef token/nome extraídos do catálogo F25.3; `f25_final_provenance.py` compara a matriz com a prova independente, com SHA fixo do catálogo e identidade do ZIP/EXE; opção `--catalog` e `--zip` verifica o catálogo completo **localmente** e o regenera a partir dos bytes históricos. Não há binário no CI. |
| F25.5-R02 | Todas as 16 associações em `F25FunctionalReconstructionMatrix.json` possuem `equivalenceStatus=INFERRED`; o símbolo é observado, o vínculo moderno não é. |
| F25.5-R03 | `observedArtifacts` deve corresponder exatamente a quatro caminhos reais e à ordem declarada; casos negativos contra omissão, troca e reordenação. |
| F25.5-R04 | Relatório estruturado `F25FinalProvenance.json` gerado por execução CI contém SHA-256 de inventário, matriz, prova e parsers; `GITHUB_RUN_ID`, `GITHUB_RUN_ATTEMPT`, `GITHUB_EVENT_NAME`, SHA do checkout e, em eventos PR, HEAD real do PR. Artefatos públicos carregados por workflow Windows/Linux; CI não autentica origem histórica. |

## Separação estrita das provas
1. **CI sintético**: verifica que os manifestos e campos documentais são consistentes, que os pares no JSON correspondem à prova versionada, que estados indevidos falham e que nenhum FMRTE.exe histórico está no repositório.
2. **Verificação dos bytes do usuário**: exige que ZIP e catálogo completo sejam fornecidos explicitamente; recalcula os hashes e extrai novamente os 16 TypeDef, comparando com a prova. Essa verificação foi feita no ambiente local e deve ser repetida por outro auditor com acesso aos arquivos históricos.
3. **Autenticidade/proveniência humana**: **não atestada**. Reproduzir o mesmo SHA não demonstra autoria do executável, legitimidade de offsets, aprovação de mapas ou revisão humana.

### Reproduzir a auditoria local (sem executar o binário)
```powershell
python tools/f25_final_provenance.py --catalog "C:\caminho\F25SymbolCatalog.json" --zip "C:\caminho\FMRTE v1.0.0.5(1).zip" --report "F25FinalProvenance-local.json"
python -m unittest discover -s tests/FMRTE2008.MemorySimulation -p test_f25_final_provenance.py -v
```
O script abre o ZIP apenas em modo leitura e interpreta bytes PE32/CLR. Não faz `subprocess`, não executa assemblies, não abre processos do jogo e não grava save/memória.

## Regras de fechamento
- Revisar os controles da implementação, reler os relatórios, reconfirmar HEAD e base sem conflitos.
- Confirmar todos os checks aplicáveis do HEAD (16 anteriores e 4 novos checks de proveniência por push/PR, Windows/Linux).
- Comparar hashes de resultados locais e identificar cada evento/run CI; um workflow `success` não é um ato de revisão humana.
- Manter PR em Draft e sem merge até nova revisão independente e decisão expressa distinta.
- Preservar `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, oito riscos não aceitos.
