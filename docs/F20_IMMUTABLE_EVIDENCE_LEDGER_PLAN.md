# F20 — Plano: ledger imutável de evidências e reprodução determinística (NÃO OPERACIONAL)

## Linha de base concluída
- F19 integrado por *Squash and merge* no PR #20, commit da `main` `0e1c0eb7b15059e150ba54c936d1161e52d6572c`.
- Quatro workflows pós-merge da `main`, no commit exato acima: **4/4 `completed/success`** (protótipo Windows, diagnóstico somente leitura, leitor x86 e simulação de memória x86).
- Log Windows `Simulacao de memoria x86` run `44821226957`, job `136582299030`: 34 asserções do gate F19, 14 verificações de composição, inventário 12/12, 12 casos `OK F19 NEG A01–A12`, F16/F17/F18 OK.
- Referências de pesquisa **não editadas**: `marcosvilhena87/FM2008.mapping` em `df5be0493a1ac7be57208ff0e91c2d7193dfec73`.
- Status conservador: **B03 PENDENTE**, **A12 GATE-BLOQUEADO**, `NoApprovedPlayerMaps`, `approved=false`, `operational=false`.

## Objetivo do F20
Consolidar um ledger offline, estruturado e auditável de evidências A01–A12, que amarre **ID do caso + fonte imutável + executor + ambiente + resultado + limitação** a uma execução determinística, sem converter resultados CI em aprovação de mapa. O novo milestone é dedicado a **rastreabilidade e validade dos dados de teste**, não a leitura de processos reais.

## Entregáveis propostos
1. **Manifesto offline versionado** (JSON ou CSV local ao FMRTE) com schema explícito para os doze critérios: `criterion`, `caseId`, `testSuite`, `expectedOutcome`, `evidenceKind`, `sourceRepo`, `sourceCommit`, `sourcePath`, `executor`, `observedAt`, `status`, `limits`. Em caso de dados ausentes, usar `null`/PENDENTE; nunca preenchê-los por suposição.
2. **Validador determinístico** que recuse IDs duplicados, referências ausentes, tipo de evidência incompatível, `sourceCommit` inválido, ambiguidade de estado, aprovação A12 inferida de testes, campos obrigatórios omitidos sem indicação de pendência e `approved=true`/`operational=true`.
3. **Casos negativos de mutação** sobre uma cópia em memória do ledger para comprovar que o validador de produção detecta alterações em `caseId`, tipo, proveniência, expectativa de erro e campos de aprovação, sem alterar arquivos externos.
4. **Vínculo ao runner e aos logs Windows:** registrar os 12 casos A01–A12 e hashes das versões do ledger; confirmar que os eventos `push` e `pull_request` consumiram o manifesto da mesma revisão que gerou a execução. Não confundir log de runtime com assinatura humana.
5. **Pendência B03 explícita e individual:** subitens R01–R07 devem preservar estado `PENDENTE/DECLARADO` quando faltarem executor histórico, data da observação ou ambiente. SHA/autoria de commit NÃO substituem metadados da observação.
6. **Parecer técnico e rastreabilidade de decisão:** checklist separado para aceitação humana futura; F20 não inclui promoção de mapa nem alteração de catálogo operacional.

## Ordem de implementação sugerida
- **F20.1 — Schema e baseline:** introduzir modelo de ledger somente de teste offline; registrar 12 casos F19 e distinguir GitHub Actions de observações manuais históricas.
- **F20.2 — Invariantes:** implementar validação fail-closed da proveniência, IDs, estado, limites e campos de aprovação.
- **F20.3 — Negativos:** testes de integridade e mutações reais do ledger por referência, identidade, contexto e exceções; não apenas `Contains` textual.
- **F20.4 — CI:** executar os quatro workflows em `push` e em `pull_request` (**8/8**), inspecionar logs Windows F16–F20 e orçamentos de recursos sem novos handles/processos.
- **F20.5 — Revisão independente:** examinar o ledger, o próprio validador, falsos positivos/negativos, completude por critério e separação entre evidência técnica e autorização humana. Manter PR Draft até revisão e autorização específicas.

## Critérios de aceite do F20
- Evidência determinística de que **todas as 12 entradas** estão presentes, são distintas e correspondem aos casos executáveis existentes;
- Validação negativa da estrutura e da proveniência com mutações controladas; resultados verificáveis nos logs Windows;
- Qualquer campo desconhecido do histórico R01–R07 explicitamente **PENDENTE**, nunca aprovado;
- A12 **GATE-BLOQUEADO**, `NoApprovedPlayerMaps` ativo, `approved=false`, `operational=false`;
- Compilações e testes 8/8 em HEAD estável, PR em Draft até revisão; sem merge não autorizado.

## Proibições
Nenhum acesso a `fm.exe`, `OpenProcess` de jogo, `ReadProcessMemory`/escrita em memória externa, execução de binário do jogo, UI live, promoção de offsets experimentais/legados, alteração no repositório `FM2008.mapping` ou liberação operacional por CI. O ledger **não** é um cadastro de mapas aprovados e não deve alimentar `IApprovedPlayerMapCatalog`.

## Estado desta etapa
**F20 apenas planejado.** Nenhuma implementação do ledger, nenhuma assinatura B03, nenhuma aprovação A12, nenhuma mudança operacional e nenhum merge realizados.
