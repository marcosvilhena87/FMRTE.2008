# F22 — Matriz de lacunas e decisão de aprovação do mapa mínimo (PLANEJAMENTO NÃO OPERACIONAL)

## Objetivo e limite
Organizar as lacunas que impedem sequer **propor** a aprovação de um mapa mínimo de identificação de jogadores do FM2008 8.0.2v1. Este documento **não** aprova o mapa, não autoriza diagnóstico no jogo, não habilita sessão operacional ou edição e não modifica `FM2008.mapping`.

**Linha de base:** F21 integrado por Squash and merge do PR #22 no commit `5f5728edc5253937d27cb4beda283bcde8fb1b1b` da `main`. Os quatro workflows pós-merge exigem conferência específica e seu estado deverá ser registrado em comentário no PR F22 após conclusão.

**Fonte de pesquisa:** `marcosvilhena87/FM2008.mapping`, revisão congelada `df5be0493a1ac7be57208ff0e91c2d7193dfec73`. Utilizar a matriz `research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md`, a revisão de riscos residuais `research/builds/fm2008-8.0.2v1-residual-risk-review-after-r07.md` e as referências R01–R07 do inventário F21. Revisar adendos históricos em ordem; não confundir estágios de coleta anteriores com o estado documental consolidado.

## Matriz inicial de pendências (não é avaliação de risco aceita)
| Portão | Escopo | Evidência atual | Lacuna para eventual deliberação |
| --- | --- | --- | --- |
| G1 — A01/A02 e RA-01/RA-06 | Identidade exata da build, instância e base | Reconhecimento de 8.0.2v1, PIDs documentados e módulo em base preferencial relatados | Reconciliação da cadeia documental e limites das variantes; relocação real não observada, jamais forçar |
| G2 — A03/A04/A05/A10 e RA-05 | Identidade e componentes dos nomes | Duas partidas amostradas em 9 posições distribuídas cada; 9/9 conferências de ID/nome completo **declaradas** em cada | Auditoria de proveniência B03, conferência de componentes/nome comum separadamente; não extrapolar a tabelas inteiras |
| G3 — A06/A07/A08 e RA-02/RA-03/RA-04 | Consistência entre sessões, erros e cancelamento | Ensaios offline F01–F21 e relatos de alteração natural de contagem/reinício | Diferenciar falha durante leitura real não reproduzida, risco ABA e recuperação de sessão; não elevar simulação à categoria de evidência histórica |
| G4 — A09/A11 e RA-07/RA-08 | Exclusividade de leitura, compatibilidade e não promoção de offsets | `NoApprovedPlayerMaps` e CI Windows com bloqueio; WPF demonstrativa | Revisão final do escopo e compatibilidade; outros offsets continuam não validados |
| G5 — A12 | Revisão externa e decisão humana | F20/F21 verificaram integridade documental e CI, sem aprovar mapa | Um ato formal posterior e explícito precisaria indicar escopo, revisor, data, evidências e riscos residuais aceitos; **não automatizável pelo CI** |

## B03 — proveniência R01–R07
O inventário estruturado F21 é **documental**. Os campos originais `executor`, `observedAt`, `environment`, `collectionMethod` e `externalCorroboration` permanecem `null` e `PENDING-DECLARED`. Hash Git do documento e digest de artefato CI não autenticam a pessoa ou o ambiente da coleta histórica. Qualquer complemento deverá estar ligado a uma fonte primária verificável e passar por revisão humana separada; nada deve ser inferido do autor do commit ou de `GITHUB_RUN_ID`.

## Entregáveis sugeridos para a implementação F22
1. Uma matriz estruturada **fechada** de G1–G5 com referência documental por lacuna, classificação `DOCUMENTED`/`PENDING` para o estado da *documentação*, e separação explícita de execução CI versus evidência histórica.
2. Testes negativos offline para omissão, troca de portão, fonte incorreta, elevação implícita a `approved`/`operational`, declaração fabricada de executor/data, e introdução de offsets de edição.
3. Um registro de versões de documentos usados, com compromisso de conteúdo e resultados estruturados de testes, sem converter sucesso de CI em aceite de riscos.
4. Uma revisão independente técnica e uma **etapa humana posterior**, caso todas as lacunas possam ser justificadamente tratadas.

## Aceitação do planejamento
- Cinco portões descritos, cada um com objetivos, evidências e lacunas; nenhuma autorização operacional embutida.
- Coerência com R01–R07 e os adendos posteriores: amostragem distribuída dos dois saves declarada, sem autenticidade externa.
- `NoApprovedPlayerMaps`, `approved=false`, `operational=false`; A12 `GATE-BLOQUEADO`; B03 `PENDING-DECLARED`.
- Proibido ativar acesso ao processo `fm.exe`, editar memória ou saves, promover offsets legados, conectar fonte live à WPF ou alterar `FM2008.mapping`.

**Estado F22:** iniciado somente no planejamento. Não existe novo mapa aprovado, aceite humano ou decisão de merge.
