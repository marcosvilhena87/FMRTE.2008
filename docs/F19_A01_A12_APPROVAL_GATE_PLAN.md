# F19 — Plano de revisão A01–A12 e preparação do gate formal do mapa mínimo (NÃO OPERACIONAL)

## Marco de partida e decisão
- F18 integrado à `main` por *Squash and merge* no PR #19, commit `9a0122f322f38bf82ef511bc33e28d61825e3589`.
- Quatro workflows de `push` na `main` concluídos com `completed/success`: compilação Windows, leitor x86, diagnóstico somente leitura e simulação de memória x86.
- O log Windows pós-merge reportou F16/F17/F18 `OK`; F18: 64 timeouts, 256 estágios de aquisição, 64 recuperações com duas falhas, 10 controles negativos e teste de segunda barreira.
- Referência de evidências: matriz `research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md` de `marcosvilhena87/FM2008.mapping` e auditoria `fm2008-8.0.2v1-a01-a12-code-audit-2026-11-19.md`.
- Esta fase é **somente planejamento, rastreabilidade e evidência de segurança**. O gate A12 continua bloqueado. `approved=false`, `operational=false`, `NoApprovedPlayerMaps` obrigatório; não há autorização para mudar esses estados.

## Objetivo
Consolidar um checklist reproduzível, por critério A01–A12, que diferencia evidências reais declaradas, simulações, inspeções estáticas, lacunas e decisão humana. A preparação do gate **não é sua aprovação** e não torna o mapa de pesquisa operacional.

## Plano de investigação e aceite por critério
| Critério | Foco de revisão F19 | Evidência exigida / regra de não extrapolação |
| --- | --- | --- |
| A01 — Build | Confirmar negação por hash, arquitetura, versão, variante, PID e troca de identidade | Evidência de fixtures adversariais e proveniência; somente fingerprint exato 8.0.2v1 |
| A02 — Base/header | Verificar cálculo `ModuleBase + HeaderRVA` e limites x86, erro ao perder identidade da base | Bases reais registradas foram `0x00400000`; realocação só simulada, não declarar validação real |
| A03 — Tabela | Rejeitar ponteiros nulos, contagem imprópria, leitura parcial e overflow | Amostras reais com contagens 5154, 6697 e 6698; não afirmar cobertura integral |
| A04 — IDs/nomes | Separar ID, primeiro nome, sobrenome e nome comum opcional; procedência da comparação | 9/9 pares de ID/nome declarados conferidos em duas partidas, não auditoria independente dos campos opcionais |
| A05 — Ponteiros/UTF-16 | Verificar fronteiras, strings inválidas, nomes obrigatórios e ausência de NUL | Negativos offline, sem injetar corrupção em `fm.exe` |
| A06 — Consistência | Rever estabilidade entre páginas, timeout, leitura concorrente, in-place/ABA | Rechecagens são best-effort, nunca snapshot atômico |
| A07 — Ciclo de vida | Auditar interrupção, `Dispose`, `SafeHandle`, cancelamento, falhas encadeadas e reabertura | Evidências F10–F18 simuladas e eventos locais Windows não substituem desligamento real em andamento |
| A08 — Consulta incremental | Verificar limites de lote, reinício do cursor, erro fatal versus erro de registro | WPF continua exclusivamente demonstrativa, sem fonte live |
| A09 — Somente leitura | Inspecionar catálogo deny-all, permissões do opener, ausência de escrita e recusa de operação | Acesso real bloqueado; sem `OpenProcess`/leitura de jogo nos testes F19 |
| A10 — Cobertura | Consolidar plano de índices distribuídos e critérios quantitativos de amostragem | 9/9 declarações em dois saves não representam todos os jogadores |
| A11 — Compatibilidade | Matriz de negativas: hashes, arquitetura, variante, módulo/PID e drift entre versões | Não transferir resultados para build não idêntica |
| A12 — Governança | Elaborar minuta de checklist formal com revisor, aprovador, data, escopo, versão imutável e riscos | **Gate bloqueado até ato humano explícito posterior**; sem promoção automática |

## Sequência proposta
1. **Inventário somente leitura:** referenciar para cada A01–A12 o arquivo/teste, evidência, ambiente, data e limitações. Confrontar versão atual da `main` com a auditoria histórica de 19/11 sem copiar estados obsoletos.
2. **Testes negativos offline/Windows:** priorizar A01/A02/A05/A06/A07/A08/A09/A11, usando fixtures e handles criados pelo próprio teste. Nenhuma execução real de jogo, nenhum mapeamento aprovado.
3. **Riscos RA-01–RA-08 e R01–R07:** distinguir mitigado em simulação, demonstrado em amostra real, ainda parcial e bloqueio crítico; tratar especialmente RA-02 (cache de ponteiros/índices entre sessões), RA-03 (ABA) e RA-05 (identidade fora das amostras).
4. **Dossiê de decisão:** propor formulário de aceite humano separado do código, com evidência reproduzível e risco residual explícito. Sem assinatura ou aprovação nesta etapa.
5. **Revisão final e CI:** executar quatro workflows nos eventos `push` e `pull_request` (8/8) no HEAD da futura implementação, inspecionar logs Windows e manter PR Draft até revisão. Nenhum merge sem autorização específica.

## Critérios de conclusão desta fase de planejamento
- Checklist A01–A12 com rastreabilidade e lacunas, sem status de aprovação presumido.
- Backlog de testes associado a riscos RA-01–RA-08, distinguindo ensaios simulados e reais já documentados.
- Evidência pós-merge F18 anexada como baseline, sem a interpretar como aprovação do mapa.
- Nenhuma alteração em `FM2008.mapping`, manifesto, catálogo operacional, flags, UI de conexão, bytes do jogo ou processos externos.
- **A12 = GATE-BLOQUEADO**, `NoApprovedPlayerMaps` ativo, `approved=false`, `operational=false`.

## Proibições explícitas
Nenhum contato com `fm.exe` em planejamento/testes; nenhuma leitura ou escrita da memória real, `WriteProcessMemory`, edição de atributos ou saves, ligação da WPF a fonte live, habilitação por percentual amostral ou promoção dos 229 offsets legados. Este documento não autoriza aprovação operacional, nem um futuro merge.

## Situação
**F19 em implementação no PR #20 (Draft).** Adicionados testes negativos determinísticos offline em `tests/FMRTE2008.MemorySimulation/F19ApprovalGateTests.cs`, integrados ao runner de memória simulada, e matriz de rastreabilidade `docs/F19_A01_A12_EVIDENCE_TRACEABILITY.md`. A suíte cobre diretamente negativos de A01/A02/A09/A10/A11/A12 e referencia testes existentes para A03–A08. Aprovação A12 e uso operacional permanecem BLOQUEADOS; a integração depende de CI 8/8 no HEAD e revisão final, sem merge. Nenhuma alteração em `FM2008.mapping`.
