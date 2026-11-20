# F23 — Plano de proveniência B03, decisão humana e rastreabilidade de riscos residuais

> **PLANEJAMENTO / DOCUMENTAÇÃO — NÃO OPERACIONAL.**
> Não constitui aprovação de mapa, declaração de observações históricas independentes, autorização de acesso ao processo do jogo ou decisão de merge.

## 1. Linha de base imutável

- Repositório de trabalho: `marcosvilhena87/FMRTE.2008`; F22 integrado à `main` no commit `0b1b2214f3d7b03c4bf27f2ea44d194958be35b0` (PR #23).
- Quatro workflows pós-merge do commit acima: `44830850553` (Windows), `44830850520` (diagnóstico somente leitura), `44830850779` (leitor x86), `44830850768` (simulação de memória x86) — todos `completed/success`, verificados antes da criação do F23.
- Evidências F21: `tests/FMRTE2008.MemorySimulation/F21B03Evidence.json`, observações **R01–R07**; conservadas como relatos históricos não auditados independentemente.
- Matriz F22: `tests/FMRTE2008.MemorySimulation/F22ApprovalGates.json` e `F22ApprovalGates.schema.json`, portões **G1–G5**, negativos offline e vínculos ao F21.
- Fonte histórica congelada: `marcosvilhena87/FM2008.mapping@df5be0493a1ac7be57208ff0e91c2d7193dfec73`. Não modificar este repositório. O identificador de blob Git confere a identidade do documento na revisão especificada, **não** a execução ou autenticidade de uma observação.
- Condições obrigatórias, até uma aprovação humana distinta e específica: `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, A12 `GATE-BLOQUEADO`, B03 `PENDING-DECLARED`.

## 2. Escopo do F23

Consolidar um **dossiê de pendências verificáveis** para que um revisor humano possa posteriormente avaliar a qualidade e suficiência documental do mapa mínimo, **sem** decidir nem habilitar o mapa neste marco. A organização das lacunas é uma saída de engenharia; não é comprovação de que as lacunas foram sanadas.

### 2.1. B03 — proveniência observacional faltante

A tabela F21 contém os campos `executor`, `observedAt`, `environment`, `collectionMethod` e `externalCorroboration` como `null`. **Todos permanecem `null`**, salvo se nova evidência primária for fornecida, avaliada e registrada em uma entrega futura autorizada. Para cada R01–R07, catalogar:

| Registro | Evidência histórica atual | Proveniência ainda exigida | Estado inicial |
| --- | --- | --- | --- |
| R01 | Reconhecimento de build e PID relatados | Executor, data/hora, ambiente, método, eventual corroboração externa | PENDING-DECLARED |
| R02 | Base do módulo e VA relatados, sem relocação natural demonstrada | Mesmos cinco campos; condição da sessão observada | PENDING-DECLARED |
| R03 | Primeira amostra distribuída, pares ID/nome completo relatados | Mesmos cinco campos; metadados da sessão e limites de conferência de nome comum | PENDING-DECLARED |
| R04 | CSV com confirmações adicionais de perfil | Mesmos cinco campos; origem e método de conferência manual | PENDING-DECLARED |
| R05 | Contagem após semana e interseção estável relatadas | Mesmos cinco campos; contexto temporal da sessão e limitações ABA | PENDING-DECLARED |
| R06 | Reinício normal e nova sessão relatados | Mesmos cinco campos; proveniência de sessão e distinção de falha durante leitura | PENDING-DECLARED |
| R07 | Duas partidas e amostragens distribuídas relatadas | Mesmos cinco campos; distinção entre saves e extrapolação indevida | PENDING-DECLARED |

O GitHub Actions pode comprovar **qual commit, job e artefato foram executados**, não quem realizou uma observação histórica. Autoria de commits, `GITHUB_ACTOR`, `GITHUB_RUN_ID`, data do commit, timestamps de logs ou adendos editoriais não devem preencher automaticamente nenhum campo B03.

### 2.2. Critérios de uma **futura** revisão humana — NÃO executada no F23

Qualquer futura deliberação sobre um mapa mínimo precisaria de ato humano explícito **separado** deste PR, contendo:

1. Identificação do revisor e escopo da sua competência para decidir;
2. Data e versão exata do dossiê e de cada fonte/documento avaliado;
3. Referência individual às observações R01–R07 e descrição do que **não** foi independentemente verificado;
4. Portões G1–G5 e critérios A01–A12 analisados, inclusive o estado de A12;
5. Tabela RA-01–RA-08 com riscos remanescentes, justificativas específicas e aceites/rejeições humanos — sem aceite implícito por CI;
6. Decisão explícita, limitada e revogável, registrada em documento próprio e separada de teste automatizado, revisão de PR e estado `Ready for review`.

**Até a deliberação real**, um rascunho de decisão deve conter `reviewer: null`, `reviewedAt: null`, `decision: "NOT_MADE"`, `approved: false`, `operational: false`. Nenhum template, fixture ou teste pode ser interpretado como ato de aprovação.

## 3. Rastreabilidade RA-01–RA-08 / G1–G5

| Risco | Portão F22 | Critérios referidos | Referências documentais | Pendente / restrição |
| --- | --- | --- | --- | --- |
| RA-01 | G1 | A01–A02 | R01, R02 | Identidade da build/sessão e lacunas de proveniência |
| RA-02 | G3 | A06–A08 | R05–R07 | Consistência de snapshots em mudanças reais, distinta dos testes offline |
| RA-03 | G3 | A06–A08 | R05, R06 | Resposta a interrupção durante leitura não observada historicamente |
| RA-04 | G3 | A06–A08 | R05, R06 | Distinção de troca de sessão/ABA e recuperação |
| RA-05 | G2 | A03–A05, A10 | R03, R04, R07 | Amostragem ID/nome completo não equivale à tabela inteira nem valida nomes comuns isolados |
| RA-06 | G1 | A01–A02 | R01, R02 | Base naturalmente relocada não demonstrada |
| RA-07 | G4 | A09, A11 | R01, R07 | Somente leitura e limites de compatibilidade; vedada promoção de offsets |
| RA-08 | G4 | A09, A11 | R01, R07 | Isolamento de versões/escopo e recusa a mapas não aprovados |

G5/A12 é o **portão transversal de decisão humana** para todos os riscos; não é evidência de que os riscos foram mitigados ou aceitos. A matriz se refere às **pendências**, não estabelece probabilidade, severidade aceita ou aprovação operacional.

## 4. Entregáveis propostos — após este primeiro commit

- **F23.1** — manifesto estruturado B03 de pendências R01–R07, com referência direta à versão F21 e representação explícita dos cinco campos não comprovados (`null`); esquema fechado, proibição de atribuição retroativa fabricada e testes negativos.
- **F23.2** — registro estruturado de RA-01–RA-08 com associação exata G1–G5/A01–A12/R01–R07, sem alteração de classificações documentais preexistentes e sem `approved=true`.
- **F23.3** — formulário de **decisão não realizada**, com identidade e data do revisor nulas, ausência de aceite e `decision=NOT_MADE`; negativos para CI apresentado como assinatura humana ou `Ready for review` tratado como aprovação.
- **F23.4** — rastreabilidade automática à fonte congelada F21/F22 e resultados de testes estruturados; hash do manifesto, ID do run e HEAD separados; revisão técnica independente do novo HEAD.

**Critério de aceitação:** arquivos fechados e referenciados, mutações relevantes rejeitadas, verificações CI do novo HEAD, fonte imutável e **nenhuma** alteração de estado operacional. A documentação inicial deste PR **não afirma que F23.1–F23.4 já foram implementados**.

## 5. Segurança, integridade e limites de aprovação

- Proibido abrir `fm.exe` para leitura operacional, acoplar WPF a sessão real, alterar processo/saves, escrever memória, promover offset ou mapa experimental, editar `FM2008.mapping`.
- `NoApprovedPlayerMaps`, `approved=false`, `operational=false` permanecem. B03 `PENDING-DECLARED` e A12 `GATE-BLOQUEADO`.
- Não usar um commit/CI bem-sucedido ou um PR revisado para inferir atestação humana, preenchimento de lacunas de origem, aceite de riscos ou permissão operacional.
- O PR começa em **Draft**; nenhuma autorização automática de `Ready for review` ou merge.
